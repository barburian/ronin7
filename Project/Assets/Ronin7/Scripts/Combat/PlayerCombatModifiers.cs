using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the player rig root. Holds the named multiplier slots <see cref="BladeDamager"/> reads
    /// (via <see cref="DamageMultiplier"/>) to scale the wielder's swing damage. Each slot defaults to
    /// 1 (no effect) so every existing scene/test that doesn't place this component — or an ability
    /// that hasn't unlocked yet — is unaffected. Ch7's weakpoint-sight (<c>Ronin7.Player.WeakpointSight</c>)
    /// drives <see cref="WeakpointSightActive"/>; Sunder Beat's parry-flow streak
    /// (<c>Ronin7.Player.ParryFlowController</c>) drives <see cref="ParryFlowMultiplier"/>; Follow-
    /// Through's combo counter (<c>Ronin7.Player.ComboMomentumController</c>) drives
    /// <see cref="ComboMultiplier"/>. The coupling runs Player -> writes -> this component -> Combat
    /// reads, since Combat doesn't reference Player.
    ///
    /// LIFETIME: a plain instance living on the player rig GameObject in the scene — no static state.
    /// Every field/property here re-initializes to its default the moment a fresh instance is created,
    /// which happens on every scene load (including "Enter Play Mode without domain reload," since the
    /// rig itself is a scene object, not a persisted singleton). Unlike EventBus/Health.Active/
    /// CombatActivity, there is nothing here that would leak stale values across Play sessions, so no
    /// explicit Reset()/RuntimeInitializeOnLoadMethod is needed.
    /// </summary>
    public class PlayerCombatModifiers : MonoBehaviour
    {
        /// <summary>Hard cap on the transient (ParryFlow x Combo x Overdrive) product (see
        /// <see cref="DamageMultiplier"/>'s doc).</summary>
        private const float MaxDamageMultiplier = 3f;

        /// <summary>A1.5: separate hard cap on <see cref="BoonMultiplier"/>, applied after the transient
        /// cap rather than folded into the same product (see <see cref="DamageMultiplier"/>'s doc for why).</summary>
        private const float MaxBoonMultiplier = 2f;

        /// <summary>Weakpoint Sight on: raises the precision-hit multiplier 1.25x -> 2x (see
        /// <see cref="BladeDamager.PrecisionMultiplier"/>). No longer a flat damage slot (BUG-12).</summary>
        public bool WeakpointSightActive { get; set; }

        /// <summary>Overdrive's explicit damage bonus (1.5x while active), in the transient group.</summary>
        public float OverdriveMultiplier { get; set; } = 1f;

        /// <summary>Sunder Beat's parry-flow-streak multiplier slot.</summary>
        public float ParryFlowMultiplier { get; set; } = 1f;

        /// <summary>Follow-Through's combo-chain multiplier slot.</summary>
        public float ComboMultiplier { get; set; } = 1f;

        /// <summary>Roguelike boon-inventory damage slot (<c>BoonInventory.BladeDamageMultiplier</c>).</summary>
        public float BoonMultiplier { get; set; } = 1f;

        /// <summary>A1.4: Roguelike boon-inventory parry-flow slot (<c>BoonInventory.ParryFlowBonus</c>),
        /// added to <c>ParryFlowController</c>'s per-stack bonus. Default 0 (no effect).</summary>
        public float BoonParryFlowBonus { get; set; }

        /// <summary>A1.4: Roguelike boon-inventory combo slot (<c>BoonInventory.ComboBonus</c>), added to
        /// <c>ComboMomentumController</c>'s per-stack bonus. Default 0 (no effect).</summary>
        public float BoonComboBonus { get; set; }

        /// <summary>P4-01: number of <see cref="BoonEffectKind"/> members (append-only, so contiguous from 0).</summary>
        public static readonly int BoonKindCount = System.Enum.GetValues(typeof(BoonEffectKind)).Length;

        /// <summary>P4-01 generic stat slots, indexed by <see cref="BoonEffectKind"/>. Each value is the
        /// bonus over the stat's base (a fraction for scale stats, points/charges for count stats);
        /// 0 is neutral. Written by RunDirector from <c>BoonInventory.Bonus</c> on every load and pick.
        /// Consumers read <see cref="Boon"/> at use time — never cache it in Awake (onLoaded runs after
        /// Awake).</summary>
        private readonly float[] boonStats = new float[BoonKindCount];

        /// <summary>The bonus held for <paramref name="kind"/>; 0 (neutral) for an out-of-range kind.</summary>
        public float Boon(BoonEffectKind kind) =>
            (uint)kind < (uint)boonStats.Length ? boonStats[(int)kind] : 0f;

        internal void SetBoon(BoonEffectKind kind, float value)
        {
            if ((uint)kind < (uint)boonStats.Length) boonStats[(int)kind] = value;
        }

        /// <summary>P4-02: AbilityHaste cap (40%).</summary>
        public const float MaxAbilityHaste = 0.4f;

        /// <summary>P4-02 AbilityHaste -> cooldown scale (1 = unchanged, floor 0.6).</summary>
        public static float CooldownScale(float haste) => 1f - Mathf.Clamp(haste, 0f, MaxAbilityHaste);

        /// <summary>P4-02 AbilityHaste -> charge-gain scale for build-up abilities (Overdrive), cap 1.4.</summary>
        public static float ChargeScale(float haste) => 1f + Mathf.Clamp(haste, 0f, MaxAbilityHaste);

        /// <summary>
        /// The multiplier <see cref="BladeDamager"/> actually applies:
        /// <c>min(ParryFlow * Combo * Overdrive, 3x) * min(BoonMultiplier, 2x)</c>.
        ///
        /// A1.5: these two groups are capped SEPARATELY, not folded into one flat 3x product. Max
        /// parry-flow is 1.40x and max combo is 1.60x — their product alone is already 2.24x, so under a
        /// single flat 3x cap a damage boon (BoonMultiplier) did literally nothing extra exactly when the
        /// player was playing well at peak flow/combo — the worst possible failure mode for a
        /// roguelike's most basic boon. Splitting the caps means the transient in-combat buffs keep their
        /// existing 3x ceiling, and the run-long boon investment gets its own 2x ceiling that always
        /// applies. Worst case is 6x, reached only by a fully-boon-stacked player at peak flow and combo
        /// on a weakpoint — a legitimate roguelike power fantasy, not an exploit. Read-only — set the
        /// individual slots instead.
        /// </summary>
        public float DamageMultiplier =>
            Mathf.Min(ParryFlowMultiplier * ComboMultiplier * OverdriveMultiplier, MaxDamageMultiplier)
            * Mathf.Min(BoonMultiplier * LastStandScale, MaxBoonMultiplier);

        /// <summary>P4-04: a one-hit skill bonus for a blade hit on <paramref name="victim"/>. Returns the damage
        /// scale (1 = none) and writes the posture scale. Implementations consume themselves on a bonus.</summary>
        public delegate float HitBonus(GameObject victim, out float postureScale);

        /// <summary>P4-04 S1 Riposte: multiplied INSIDE the boon group (under its 2x cap).</summary>
        public HitBonus RiposteBonus;

        /// <summary>P4-04 S2 Quick Draw: an opener multiplied on top of both caps (a x2.5 under the 2x boon cap
        /// would be a lie).</summary>
        public HitBonus QuickDrawBonus;

        /// <summary>Posture scale of the blade hit currently being applied; <see cref="BladeDamager"/> sets it
        /// around <c>Health.ApplyDamage</c> and <c>PostureMeter</c> reads it synchronously. 1 otherwise.</summary>
        public float HitPostureScale { get; set; } = 1f;

        /// <summary>P4-04: <see cref="DamageMultiplier"/> plus the per-victim skill bonuses. Call once per
        /// LANDED hit: it consumes Riposte / Quick Draw.</summary>
        public float DamageMultiplierFor(GameObject victim, out float postureScale)
        {
            postureScale = 1f;
            float riposte = 1f, draw = 1f;
            if (RiposteBonus != null) { riposte = RiposteBonus(victim, out float p); postureScale *= p; }
            if (QuickDrawBonus != null) { draw = QuickDrawBonus(victim, out float p); postureScale *= p; }
            if (riposte > 1f) { LastRiposteVictim = victim; LastRiposteFrame = Time.frameCount; } // P4-05 Thunder Riposte
            return Mathf.Min(ParryFlowMultiplier * ComboMultiplier * OverdriveMultiplier, MaxDamageMultiplier)
                   * Mathf.Min(BoonMultiplier * riposte * LastStandScale, MaxBoonMultiplier) * draw;
        }

        /// <summary>P4-05: the victim and frame of the last riposte-boosted hit (Thunder Riposte reads them).</summary>
        public GameObject LastRiposteVictim { get; private set; }
        public int LastRiposteFrame { get; private set; } = -1;

        /// <summary>P4-05 Last Stand: set by RunBoonProcs while the boon is held and HP is under 30%.</summary>
        public bool LastStandActive { get; set; }

        /// <summary>P4-05 Last Stand threshold: active strictly below 30% of max HP.</summary>
        public const float LastStandThreshold = 0.3f;

        /// <summary>P4-05 Last Stand incoming-damage cut while active.</summary>
        public const float LastStandIncoming = 0.15f;

        public static bool LastStandActiveAt(float current, float max) => max > 0f && current > 0f && current < LastStandThreshold * max;

        /// <summary>Boon-group damage scale from Last Stand (1 when inactive). Inside the 2x boon cap.</summary>
        public float LastStandScale => LastStandActive ? 1f + Boon(BoonEffectKind.LastStand) : 1f;

        /// <summary>P4-05 Crimson Tide: the live Follow-Through combo count (written by ComboMomentumController).</summary>
        public int ComboCount { get; set; }

        /// <summary>P4-04 S8: elements the player owns via boons (P4-05 sets it from Bleed/Storm tags).</summary>
        public ImbueElement OwnedElements { get; set; }

        /// <summary>P4-04 S8: the element the blade is imbued with right now (None when not imbued).</summary>
        public ImbueElement Imbued { get; set; }

        /// <summary>P4-04 S8: element procs (P4-05 RunBoonProcs) roll at this rate scale. x2 while imbued.</summary>
        public float ProcRateScale => Imbued != ImbueElement.None ? 2f : 1f;
    }

    /// <summary>P4-04 S8 Element Imbue elements. Flags so <see cref="PlayerCombatModifiers.OwnedElements"/>
    /// can hold both.</summary>
    [System.Flags]
    public enum ImbueElement
    {
        None = 0,
        Bleed = 1,
        Storm = 2,
    }
}

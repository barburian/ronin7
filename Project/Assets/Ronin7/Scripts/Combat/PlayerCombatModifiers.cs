using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the player rig root. Holds the named multiplier slots <see cref="BladeDamager"/> reads
    /// (via <see cref="DamageMultiplier"/>) to scale the wielder's swing damage. Each slot defaults to
    /// 1 (no effect) so every existing scene/test that doesn't place this component — or an ability
    /// that hasn't unlocked yet — is unaffected. Ch7's weakpoint-sight (<c>Ronin7.Player.WeakpointSight</c>)
    /// drives <see cref="WeakpointMultiplier"/>; Sunder Beat's parry-flow streak
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
        /// <summary>Hard cap on the transient (Weakpoint x ParryFlow x Combo) product (see
        /// <see cref="DamageMultiplier"/>'s doc).</summary>
        private const float MaxDamageMultiplier = 3f;

        /// <summary>A1.5: separate hard cap on <see cref="BoonMultiplier"/>, applied after the transient
        /// cap rather than folded into the same product (see <see cref="DamageMultiplier"/>'s doc for why).</summary>
        private const float MaxBoonMultiplier = 2f;

        /// <summary>Ch7 weakpoint-sight's multiplier slot.</summary>
        public float WeakpointMultiplier { get; set; } = 1f;

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

        /// <summary>
        /// The multiplier <see cref="BladeDamager"/> actually applies:
        /// <c>min(Weakpoint * ParryFlow * Combo, 3x) * min(BoonMultiplier, 2x)</c>.
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
            Mathf.Min(WeakpointMultiplier * ParryFlowMultiplier * ComboMultiplier, MaxDamageMultiplier)
            * Mathf.Min(BoonMultiplier, MaxBoonMultiplier);
    }
}

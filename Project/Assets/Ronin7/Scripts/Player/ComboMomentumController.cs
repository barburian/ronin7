using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Follow-Through: an opt-in rig component that rewards chaining hits across DISTINCT targets
    /// within a short window, à la a hack-and-slash combo counter. Subscribes to
    /// <see cref="SwordImpact"/> — published only by <c>BladeDamager</c>, which lives on the player's
    /// own blade (per <c>OverdriveController</c>'s doc: enemies deal damage through
    /// <c>MeleeAttacker.LandHit</c> directly and never publish this event), so every impact seen here is
    /// a hit the player landed.
    ///
    /// COMBO SCHEME (locked in — see <see cref="RegisterHit"/>; this needed a concrete decision because
    /// "same target resets the chain" and "Count represents chain length" pull in different directions):
    ///   - Count 0 = no active chain.
    ///   - A fresh hit (no chain yet) OR a hit arriving after the window has expired starts a brand-new
    ///     chain at Count 1 — this hit IS the first hit of the chain, and the multiplier it grants
    ///     applies to the player's NEXT swing (the buff is always one hit "behind").
    ///   - A hit on a DIFFERENT target than the last one, within the window, extends the chain
    ///     (Count + 1, capped at <see cref="maxCombo"/>).
    ///   - A hit on the SAME target as the last one (double-hitting one enemy instead of chaining across
    ///     targets) punishes the chain: Count resets to 0 (no multiplier), though the target/time
    ///     bookkeeping still updates so the NEXT hit's window/fresh-chain decision measures from this one.
    /// Multiplier: 1 + Count * perStack (Count 0..4, perStack 0.15 -> 1.0..1.6, i.e. cap +60%).
    ///
    /// TIME BASE: <see cref="Time.time"/> (scaled), matching the convention the rest of combat timing
    /// uses (MeleeAttacker's state timers, Enemy's attack cooldown, WeaponDefinition/BladeDamager's hit
    /// cooldown) — unlike Overdrive/CombatFeedbackController, this system doesn't itself drive
    /// Time.timeScale, so there's no self-referential-dilation reason to use unscaled time.
    ///
    /// DECAY: the multiplier resets lazily on the next hit (an expired-window hit starts a fresh Count-1
    /// chain, overwriting the old value) AND proactively in <see cref="Update"/>, so a buff from the last
    /// swing of a fight doesn't visibly linger once no more hits are coming.
    /// </summary>
    public class ComboMomentumController : MonoBehaviour
    {
        private const float PerStack = 0.15f;

        [SerializeField] private PlayerCombatModifiers combatModifiers;
        [SerializeField] private float comboWindowSeconds = 1.2f;
        [SerializeField] private int maxCombo = 4;

        private ComboState state;
        private Vector3 lastVelocity; // P4-05 Reversal

        // P4-05: Momentum Keeper widens the window (read at use time; boons land after Awake).
        private float Window => comboWindowSeconds + (combatModifiers != null ? Mathf.Max(0f, combatModifiers.Boon(BoonEffectKind.ComboWindow)) : 0f);

        private void Awake()
        {
            if (combatModifiers == null) combatModifiers = GetComponent<PlayerCombatModifiers>();
        }

        private void OnEnable() => EventBus.Subscribe<SwordImpact>(OnSwordImpact);

        private void OnDisable()
        {
            EventBus.Unsubscribe<SwordImpact>(OnSwordImpact);
            state = default;
            ApplyMultiplier();
        }

        private void Update()
        {
            if (state.Count == 0) return;
            if (Time.time - state.LastHitTime >= Window)
            {
                state = default;
                ApplyMultiplier();
            }
        }

        private void OnSwordImpact(SwordImpact e)
        {
            int targetId = e.Victim != null ? e.Victim.GetEntityId().GetHashCode() : 0;
            int previousCount = state.Count;
            bool reversal = combatModifiers != null && combatModifiers.Boon(BoonEffectKind.ComboReversal) > 0f
                            && IsReversal(lastVelocity, e.Velocity);
            lastVelocity = e.Velocity;
            state = RegisterHit(state, targetId, Time.time, Window, maxCombo, reversal);
            ApplyMultiplier();

            // H1 campaign-stats hook: publish only on forward progress (never on the same-target
            // reset to 0) so "best combo reached" tracking sees a monotonic climb.
            if (state.Count > previousCount) EventBus.Publish(new ComboChained(state.Count));
        }

        private void ApplyMultiplier()
        {
            if (combatModifiers == null) return;
            combatModifiers.ComboMultiplier = MultiplierForCombo(state.Count, PerStack + combatModifiers.BoonComboBonus);
            combatModifiers.ComboCount = state.Count; // P4-05 Crimson Tide
        }

        /// <summary>Pure combo-chain transition. See the class doc for the locked-in scheme.</summary>
        internal static ComboState RegisterHit(ComboState prev, int targetId, float now, float window, int maxCombo)
            => RegisterHit(prev, targetId, now, window, maxCombo, false);

        /// <summary>P4-05 Reversal (BUG-20): with <paramref name="reversal"/>, a same-target hit whose swing
        /// reversed direction advances the chain instead of resetting it, so combos work on a lone boss.</summary>
        internal static ComboState RegisterHit(ComboState prev, int targetId, float now, float window, int maxCombo, bool reversal)
        {
            bool freshOrExpired = prev.Count == 0 || now - prev.LastHitTime >= window;
            if (freshOrExpired) return new ComboState(1, targetId, now);
            if (targetId == prev.LastTargetId && !reversal) return new ComboState(0, targetId, now);
            return new ComboState(Mathf.Min(prev.Count + 1, maxCombo), targetId, now);
        }

        /// <summary>P4-05: the swing turned back on itself (velocities more than 90 degrees apart). Zero = unknown = no.</summary>
        internal static bool IsReversal(Vector3 previous, Vector3 current) =>
            previous.sqrMagnitude > 1e-4f && current.sqrMagnitude > 1e-4f && Vector3.Dot(previous, current) < 0f;

        /// <summary>1 + Count * perStack.</summary>
        internal static float MultiplierForCombo(int count, float perStack) => 1f + count * perStack;
    }

    /// <summary>Immutable combo-chain snapshot: hit count, last target hit, and when it landed.</summary>
    internal readonly struct ComboState
    {
        public readonly int Count;
        public readonly int LastTargetId;
        public readonly float LastHitTime;

        public ComboState(int count, int lastTargetId, float lastHitTime)
        {
            Count = count;
            LastTargetId = lastTargetId;
            LastHitTime = lastHitTime;
        }
    }
}

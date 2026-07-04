using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>Pure snapshot of the kill streak: how many kills in the current run and when the
    /// last one landed. Mirrors <c>HeartbeatPulser</c>'s "caller supplies now" idiom so the streak
    /// math is deterministic and unit-testable without a MonoBehaviour instance.</summary>
    internal readonly struct KillStreakState
    {
        public readonly int Count;
        public readonly float LastKillTime;

        public KillStreakState(int count, float lastKillTime)
        {
            Count = count;
            LastKillTime = lastKillTime;
        }
    }

    /// <summary>
    /// G5 "Adrenaline Flow": chaining kills within a short window heals the player a small fraction
    /// of max health per streak, up to a cap, ticking at most every few seconds.
    ///
    /// OFF-BY-DEFAULT: this is a scene-level opt-in, not an <c>AbilityId</c> unlock — component
    /// presence on the rig is the switch (mirrors <see cref="ParryFlowController"/>/base-kit texture,
    /// not the gated pattern in <see cref="UnbrokenWard"/>/<see cref="OverdriveController"/>). It ships
    /// disabled everywhere: no scene currently instantiates it, so existing encounters are unaffected
    /// until a scene builder deliberately adds it to a rig.
    ///
    /// ECONOMY: <see cref="healPerStreakFraction"/>/<see cref="minStreakToHeal"/>/
    /// <see cref="minTickInterval"/> are first-pass numbers, flagged for a design/headset tuning pass
    /// once this is actually placed in a scene — not treated as final balance here.
    ///
    /// DUEL BOSSES: 1v1 <see cref="DuelYield"/> encounters resolve via a single boss kill, so a streak
    /// here never exceeds 1 and this never triggers a heal in those fights — nothing extra to guard.
    /// </summary>
    public class AdrenalineFlow : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;

        [SerializeField] private float killWindowSeconds = 4f;
        [SerializeField] private float healPerStreakFraction = 0.03f;
        [SerializeField] private int minStreakToHeal = 2;
        [SerializeField] private int maxStreak = 6;
        [SerializeField] private float minTickInterval = 3f;

        private KillStreakState streak;
        private float lastHealTime = float.NegativeInfinity;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponent<Health>();
        }

        private void OnEnable() => EventBus.Subscribe<EntityDied>(OnEntityDied);

        private void OnDisable() => EventBus.Unsubscribe<EntityDied>(OnEntityDied);

        private void OnEntityDied(EntityDied evt)
        {
            // The player's own death is not a kill.
            if (VRRig.Instance != null && evt.Entity == VRRig.Instance.gameObject) return;
            if (playerHealth == null || !playerHealth.IsAlive) return;

            streak = RegisterKill(streak, Time.time, killWindowSeconds, maxStreak);

            float amount = HealForStreak(streak.Count, healPerStreakFraction, playerHealth.Max, minStreakToHeal);
            if (amount <= 0f) return;
            if (Time.time - lastHealTime < minTickInterval) return;

            playerHealth.Heal(amount);
            lastHealTime = Time.time;
            Haptics.Pulse(XRNode.LeftHand, 0.2f, 0.05f);
            Haptics.Pulse(XRNode.RightHand, 0.2f, 0.05f);
        }

        /// <summary>
        /// Advances the kill streak: a kill landing within <paramref name="window"/> seconds of the
        /// previous one extends the streak by one (capped at <paramref name="maxStreak"/>); otherwise
        /// the streak resets to 1. <paramref name="maxStreak"/> defaults to 6 so tests can exercise the
        /// cap without threading it through explicitly.
        /// </summary>
        internal static KillStreakState RegisterKill(KillStreakState prev, float now, float window, int maxStreak = 6)
        {
            bool withinWindow = prev.Count > 0 && now - prev.LastKillTime <= window;
            int count = withinWindow ? Mathf.Min(prev.Count + 1, maxStreak) : 1;
            return new KillStreakState(count, now);
        }

        /// <summary>
        /// Heal amount for a given streak: 0 below <paramref name="minStreakToHeal"/>, otherwise
        /// <paramref name="maxHealth"/> * <paramref name="healPerStreakFraction"/> * <paramref name="streakCount"/>.
        /// </summary>
        internal static float HealForStreak(int streakCount, float healPerStreakFraction, float maxHealth, int minStreakToHeal)
            => streakCount < minStreakToHeal ? 0f : maxHealth * healPerStreakFraction * streakCount;
    }
}

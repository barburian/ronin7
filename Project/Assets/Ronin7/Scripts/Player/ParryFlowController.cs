using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Sunder Beat: an opt-in rig component (no CampaignState ability gate — place it on the rig to turn
    /// the mechanic on, mirroring how <see cref="PlayerCombatModifiers"/> itself is just present-or-not)
    /// that turns a run of on-time parries into a stacking damage buff. Subscribes to
    /// <see cref="PerfectParry"/> (published by <c>MeleeAttacker.Deflect</c> only when the deflect landed
    /// inside the perfect-timing window) to build the streak, and to <see cref="PlayerHit"/> (an enemy
    /// attack landing on the player) to break it immediately — getting hit ends the flow state. A streak
    /// left untouched for <see cref="IdleResetSeconds"/> of real/unscaled time also decays to zero; the
    /// idle clock is deliberately unscaled so an unrelated slow-mo elsewhere (e.g.
    /// <see cref="CombatFeedbackController"/>'s own deflect slow-mo) can't stretch or shrink it.
    ///
    /// Writes <see cref="PlayerCombatModifiers.ParryFlowMultiplier"/>; the "assigned in inspector, else
    /// GetComponent on this rig" wiring mirrors <see cref="WeakpointSight"/>.
    ///
    /// HAPTICS: a short off-hand tick on every perfect parry, mirroring
    /// <see cref="CombatFeedbackController"/>'s use of <see cref="Haptics.Pulse"/>. "Off-hand" is
    /// hardcoded to <see cref="XRNode.LeftHand"/> — this codebase has no tracked handedness/dominant-hand
    /// concept anywhere (checked: <c>Grabber</c> only knows which hand IT is, not which hand is
    /// dominant), so there is nothing to branch on.
    /// </summary>
    public class ParryFlowController : MonoBehaviour
    {
        private const int MaxStreak = 5;
        private const float PerStackBonus = 0.08f;
        private const float IdleResetSeconds = 6f;
        private const float HapticDuration = 0.06f;

        [SerializeField] private PlayerCombatModifiers combatModifiers;

        private int streak;
        private float lastPerfectParryUnscaledTime = float.NegativeInfinity;

        private void Awake()
        {
            if (combatModifiers == null) combatModifiers = GetComponent<PlayerCombatModifiers>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<PlayerHit>(OnPlayerHit);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<PlayerHit>(OnPlayerHit);
            // Failsafe, mirrors WeakpointSight.OnDisable: never leave a stale buff active past teardown.
            streak = 0;
            ApplyMultiplier();
        }

        private void Update()
        {
            if (streak == 0) return;
            if (Time.unscaledTime - lastPerfectParryUnscaledTime >= IdleResetSeconds)
            {
                streak = 0;
                ApplyMultiplier();
            }
        }

        private void OnPerfectParry(PerfectParry e)
        {
            int previousStreak = streak;
            streak = ParryTiming.NextStreak(streak, e.Quality, MaxStreak);
            lastPerfectParryUnscaledTime = Time.unscaledTime;
            ApplyMultiplier();
            Haptics.Pulse(XRNode.LeftHand, 0.3f + 0.1f * streak, HapticDuration);

            // H1 campaign-stats hook: publish only on forward progress, mirroring
            // ComboMomentumController's ComboChained hook.
            if (streak > previousStreak) EventBus.Publish(new ParryStreakAdvanced(streak));
        }

        private void OnPlayerHit(PlayerHit e)
        {
            if (streak == 0) return;
            streak = 0;
            ApplyMultiplier();
        }

        private void ApplyMultiplier()
        {
            if (combatModifiers != null) combatModifiers.ParryFlowMultiplier = ParryTiming.FlowMultiplier(streak, PerStackBonus + combatModifiers.BoonParryFlowBonus);
        }
    }
}

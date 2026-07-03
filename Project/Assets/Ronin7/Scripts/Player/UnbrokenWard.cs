using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Ch11 ("Ghosts and Origins") permanent ability: Aldric/Knight-1's freed blade-shadow, inherited by
    /// Echo on the kill, wards Cipher against one otherwise-lethal blow per life. Self-disables in
    /// <see cref="Awake"/> unless <c>CampaignState.HasAbility(AbilityId.Unbroken)</c> — mirrors
    /// <see cref="WeakpointSight"/>/<see cref="OverdriveController"/>/<see cref="PhaseStepController"/> —
    /// so it is harmless to place on the rig in every scene, locked or not.
    ///
    /// PASSIVE: unlike the prior three abilities, Unbroken has no input action and no toggle/activation
    /// button — it registers itself as the player <see cref="Health"/>'s
    /// <see cref="Health.DeathInterceptor"/> in <see cref="OnEnable"/> and unregisters in
    /// <see cref="OnDisable"/> (but only if the hook still points at this instance — see below), then
    /// does its work silently whenever a lethal blow lands.
    ///
    /// ONCE PER LIFE: the ward fires at most once, tracked by <see cref="used"/>. The interceptor
    /// (<see cref="TryIntercept"/>) returns true (survive) the first time it is invoked, publishes
    /// <see cref="AbilityActivated"/> so <c>EchoPresence</c> reacts, and returns false on every
    /// subsequent lethal blow for that life — the ward is spent, not recharging, matching the source
    /// script's "once. Spend it well." The pure decision is extracted to
    /// <see cref="ShouldIntercept(bool)"/> (mirrors <c>DuelYield.ShouldYield</c>/
    /// <c>ChapterOutro.ShouldPublish</c>'s "pure decision seam" idiom) so it's testable without a
    /// MonoBehaviour instance.
    ///
    /// FEAR-WARD: the source script's "a mind it can't drown, a fear it can't pour into you, a narcosis
    /// that slides right off" is narrative only — there is no runtime fear/status/narcosis system in the
    /// codebase to hook (grepped: none exists), so per the Karpathy "no speculative systems" rule this
    /// class does NOT invent one. The fear-ward is delivered entirely in Chapter11Lines dialogue (Echo's
    /// "the haze just let go of you... the dream can't hold you now, Cipher").
    ///
    /// REGISTRATION LIFECYCLE: <see cref="OnDisable"/> only clears <c>health.DeathInterceptor</c> when it
    /// still references THIS instance's delegate, so a second ward (or any other interceptor) that took
    /// over the hook after this one is never clobbered by a stale unregister. If the player's
    /// <see cref="Health"/> is destroyed before this component is, the interceptor reference dies with
    /// it — nothing to clean up on this side.
    /// </summary>
    public class UnbrokenWard : MonoBehaviour
    {
        [SerializeField] private Health health;

        private bool used;
        private System.Func<bool> interceptorDelegate;

        private void Awake()
        {
            if (!CampaignState.HasAbility(AbilityId.Unbroken))
            {
                enabled = false;
                return;
            }

            if (health == null) health = GetComponent<Health>();
            interceptorDelegate = TryIntercept;
        }

        private void OnEnable()
        {
            if (health != null) health.DeathInterceptor = interceptorDelegate;
        }

        private void OnDisable()
        {
            // Only clear the hook if it's still ours — never clobber a different interceptor that took
            // over after this one registered.
            if (health != null && health.DeathInterceptor == interceptorDelegate)
                health.DeathInterceptor = null;
        }

        /// <summary>The Health.DeathInterceptor callback: spends the ward at most once per life.</summary>
        private bool TryIntercept()
        {
            if (!ShouldIntercept(used)) return false;

            used = true;
            EventBus.Publish(new AbilityActivated(AbilityId.Unbroken));
            return true;
        }

        /// <summary>Pure decision: intercept iff the ward hasn't been spent yet this life.</summary>
        public static bool ShouldIntercept(bool alreadyUsed) => !alreadyUsed;
    }
}

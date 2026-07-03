using System.Collections;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Ch10 ("The Ledger of Rust") permanent ability: Sever/Ninja-2's freed blade-shadow, inherited by
    /// Echo on the kill, lets Echo blink Cipher a short distance through solid matter and enemy bodies.
    /// Self-disables in <see cref="Awake"/> unless <c>CampaignState.HasAbility(AbilityId.PhaseStep)</c> —
    /// mirrors <see cref="WeakpointSight"/>/<see cref="OverdriveController"/> — so it is harmless to place
    /// on the rig in every scene, locked or not.
    ///
    /// INPUT: <see cref="activateAction"/> is wired at build time to a dedicated "Phase Step" action on
    /// the right controller's secondary button with a Tap interaction. That same physical button also
    /// hosts "Recenter" (now a Hold(0.35s) — see <c>Ronin7Input.inputactions</c>/
    /// <c>XRRigBuilder.RewireOpenScene</c>) and the contextual "Hack" action (an unconditional press,
    /// only ever consumed while standing at a <c>HackTerminal</c>) — a quick tap blinks, a held press
    /// recenters, matching the project's existing tap/hold-on-one-button convention (Dash+Overdrive on
    /// right A, Crouch+WeakpointSight on left X). Read via <c>WasPerformedThisFrame</c> so the Tap
    /// interaction is respected. A rare near-terminal tap-blink co-fire against Hack is accepted greybox
    /// (Hack requires a 1.5s hold to complete, so a Tap-length press is harmless to it).
    ///
    /// DIRECTION: horizontal-only, taken from the head's forward look direction (yaw only, pitch
    /// stripped) — mirrors <c>ContinuousLocomotion</c>'s own forward-vector idiom exactly. A simpler
    /// design than mirroring Dash's stick-blended direction: Phase-step needs only ONE
    /// InputActionReference to rewire (the activate action itself), and "blink forward through whatever's
    /// in front of you" matches the source script's staging ("takes a step toward a filed rack and
    /// PHASES") more directly than a joystick-aimed blink would.
    ///
    /// TELEPORT (VR-comfort, non-negotiable): the ONLY safe way to relocate the rig without inducing
    /// sickness is to hide the cut behind a full screen fade — no camera shake, no lerp/slide of the
    /// camera between origin and destination. Mirrors the exact idiom <c>MemoryDiveController</c> uses
    /// for its dive teleports: <c>ScreenFader.FadeOut</c> -> disable the CharacterController -> set the
    /// rig's position -> re-enable the CharacterController -> <c>ScreenFader.FadeIn</c>. The blink is
    /// horizontal, so before teleporting a downward ground probe at the destination snaps the rig onto
    /// the floor there and REFUSES the blink outright if there is no landable ground beneath it — a
    /// horizontal blink past a tier edge must never strand the rig over the shaft and drop it (a fall is
    /// a sickness trigger). A missing camera (no way to hide the cut) likewise refuses the blink.
    ///
    /// COOLDOWN: a trivial float timer, not extracted to a pure class — there is no branching/edge-case
    /// logic here worth unit-testing apart from the MonoBehaviour (unlike the destination math in
    /// <see cref="PhaseStepSolver"/>, which is genuinely worth isolating).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PhaseStepController : MonoBehaviour
    {
        [SerializeField] private InputActionReference activateAction;
        [Tooltip("Head/camera transform whose horizontal forward is the blink direction. Falls back to this transform's forward if unset.")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private float maxDistance = 4f;
        [SerializeField] private float cooldownSeconds = 2.5f;
        [SerializeField] private float fadeDuration = 0.12f;
        [Tooltip("Ground probe: how far above the destination the downward floor-check starts.")]
        [SerializeField] private float groundProbeUp = 1.5f;
        [Tooltip("Ground probe: max drop below the destination that still counts as landable ground. " +
                 "No ground within this range => the blink is refused (never drop the rig into a pit).")]
        [SerializeField] private float groundProbeDown = 6f;

        private CharacterController controller;
        private float cooldownRemaining;
        private bool blinking;

        /// <summary>True while the fade-hidden teleport coroutine is running.</summary>
        public bool IsBlinking => blinking;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!CampaignState.HasAbility(AbilityId.PhaseStep))
            {
                enabled = false;
            }
        }

        private void OnEnable() => activateAction?.action?.Enable();

        private void OnDisable() => activateAction?.action?.Disable();

        private void Update()
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= Time.unscaledDeltaTime;
            if (blinking || cooldownRemaining > 0f) return;

            var action = activateAction != null ? activateAction.action : null;
            if (action == null || !action.WasPerformedThisFrame()) return;

            StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            blinking = true;
            cooldownRemaining = cooldownSeconds;

            Vector3 forward = headTransform != null ? headTransform.forward : transform.forward;
            forward.y = 0f;
            Vector3 destination = PhaseStepSolver.ComputeDestination(transform.position, forward, maxDistance);

            // Ground validation (VR comfort): the blink is horizontal, so a destination past a tier edge
            // would strand the rig over the shaft and drop it — a fall is a sickness trigger. Probe
            // straight down at the destination; snap to the floor if found, REFUSE the blink (refund the
            // cooldown so the player can re-aim) if there is no landable ground beneath it.
            Vector3 probeStart = destination + Vector3.up * groundProbeUp;
            if (Physics.Raycast(probeStart, Vector3.down, out var groundHit, groundProbeUp + groundProbeDown,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                destination.y = groundHit.point.y;
            }
            else
            {
                cooldownRemaining = 0f;
                blinking = false;
                yield break;
            }

            // No camera => no way to hide the cut behind a fade; refuse rather than show a raw jump.
            var fader = ScreenFader.Ensure();
            if (fader == null)
            {
                cooldownRemaining = 0f;
                blinking = false;
                yield break;
            }

            yield return fader.FadeOut(fadeDuration);

            // CharacterController overrides direct transform writes while enabled (same idiom as
            // MemoryDiveController.TeleportRig / ZoneBounds' fall-reset teleport) — toggle it around
            // the position set.
            controller.enabled = false;
            transform.position = destination;
            controller.enabled = true;

            EventBus.Publish(new AbilityActivated(AbilityId.PhaseStep));

            yield return fader.FadeIn(fadeDuration);
            blinking = false;
        }
    }
}

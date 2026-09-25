using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Ch12 ("The Fracture") permanent ability, the FINAL unlock: the Ronin-7 Edition's freed
    /// blade-shadow, inherited by Echo on the kill, lets Echo summon a ghost-tinted phantom double of
    /// Cipher that fights alongside him for a limited duration. Self-disables in <see cref="Awake"/>
    /// unless <c>AbilityAccess.Has(AbilityId.Mirror)</c> (campaign unlock OR run-scoped boon grant) — mirrors <see cref="WeakpointSight"/>/
    /// <see cref="OverdriveController"/>/<see cref="PhaseStepController"/>/<see cref="UnbrokenWard"/> —
    /// so it is harmless to place on the rig in every scene, locked or not.
    ///
    /// INPUT: <see cref="activateAction"/> is wired at build time to a dedicated "Summon Mirror" action
    /// on the right controller's secondary button (B) with a Hold(0.4s) interaction. That same physical
    /// button already hosts "Phase Step" (a Tap, Ch10) and the "Recenter" action (a Hold(0.35s) whose
    /// <c>ContinuousLocomotion.recenterAction</c> wiring has no runtime consumer — confirmed dead in
    /// Ch10) — a quick tap still blinks, and a held press now summons Mirror instead of doing nothing,
    /// so the overlap with the inert Recenter binding is harmless. Read via <c>WasPerformedThisFrame</c>
    /// so the Hold interaction is respected.
    ///
    /// PHANTOM: a ghost-tinted greybox capsule (via <c>MemoryFlashbackController.MakeGhostMaterial</c>,
    /// the same translucent treatment Ch11 uses for its dreamscape ghosts) carrying an
    /// <see cref="AllyCombatant"/>, spawned a short distance in front of the rig along the head's
    /// horizontal forward (mirrors <see cref="PhaseStepController"/>'s own forward-vector idiom).
    /// <see cref="AllyCombatant"/> already retargets the nearest live enemy on its own, so the phantom
    /// needs no bespoke AI here. The capsule's collider is stripped — it reads as a ghost, not a
    /// physical body, and must never block the player's own movement.
    ///
    /// DESPAWN: the phantom is destroyed the instant <see cref="MirrorSummonLogic"/>'s active window
    /// ends (natural timeout) AND, as a failsafe, in <see cref="OnDisable"/> (component disable or scene
    /// teardown) — belt-and-suspenders, because a lingering phantom surviving a chapter transition would
    /// be a leaked GameObject haunting the next scene's ally/enemy targeting.
    ///
    /// COOLDOWN: extracted to <see cref="MirrorSummonLogic"/> (mirrors <c>OverdriveLogic</c>'s "pure
    /// state machine behind the MonoBehaviour" idiom) because, unlike Phase-step's trivial float timer,
    /// Mirror's active-window-then-cooldown sequencing has real branching worth isolating and
    /// unit-testing.
    /// </summary>
    public class MirrorSummonController : MonoBehaviour
    {
        [SerializeField] private InputActionReference activateAction;
        [Tooltip("Head/camera transform whose horizontal forward is the spawn direction. Falls back to this transform's forward if unset.")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private float spawnDistance = 2f;
        [SerializeField] private float activeDuration = 15f;
        [SerializeField] private float cooldownSeconds = 30f;

        private MirrorSummonLogic logic;
        private GameObject phantomInstance;

        /// <summary>True while the summoned phantom should still be alive in the world.</summary>
        public bool IsSummoned => logic != null && logic.IsActive;

        private void Awake()
        {
            if (!AbilityAccess.Has(AbilityId.Mirror))
            {
                enabled = false;
                return;
            }

            logic = new MirrorSummonLogic(activeDuration, cooldownSeconds);
        }

        private void OnEnable() => activateAction?.action?.Enable();

        private void OnDisable()
        {
            activateAction?.action?.Disable();
            // Failsafe: never leak a phantom across a component disable or scene transition.
            DespawnPhantom();
            logic?.ForceEnd();
        }

        private void Update()
        {
            if (logic == null) return;

            var action = activateAction != null ? activateAction.action : null;
            if (!logic.IsActive && action != null && action.WasPerformedThisFrame())
            {
                if (logic.TrySummon())
                {
                    phantomInstance = SpawnPhantom();
                    EventBus.Publish(new AbilityActivated(AbilityId.Mirror));
                }
            }

            if (logic.Tick(Time.deltaTime))
            {
                DespawnPhantom();
            }
        }

        private GameObject SpawnPhantom()
        {
            Vector3 forward = headTransform != null ? headTransform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var go = new GameObject("MirrorPhantom");
            go.transform.SetPositionAndRotation(transform.position + forward * spawnDistance, Quaternion.LookRotation(forward));

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col); // a ghost double, not a physical body — never blocks the player
            body.GetComponent<Renderer>().sharedMaterial = MemoryFlashbackController.MakeGhostMaterial();

            go.AddComponent<AllyCombatant>();
            return go;
        }

        private void DespawnPhantom()
        {
            if (phantomInstance == null) return;
            Destroy(phantomInstance);
            phantomInstance = null;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Grip-based wall climbing: while a grip is held with the hand over a <see cref="Climbable"/>
    /// collider, the rig is anchored to that hand and moves by the inverse of the hand's motion —
    /// the classic VR hand-over-hand climb (mirrors <see cref="ZeroGGrabLocomotion"/>'s grab loop,
    /// but in normal gravity). While climbing, <see cref="ContinuousLocomotion.MovementSuspended"/>
    /// freezes stick movement and gravity; on release the averaged hand velocity becomes a launch
    /// impulse (<see cref="ContinuousLocomotion.AddAirImpulse"/>) so throwing yourself upward off a
    /// ledge mantles you over it. A hand already holding a <see cref="Grabbable"/> (the katana)
    /// never doubles as a climbing hand. Hands/grabbers resolve from <see cref="VRRig"/> at runtime
    /// so scene wiring only needs the two grip actions.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class WallClimbLocomotion : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference leftGripAction;  // Left Hand / Select
        [SerializeField] private InputActionReference rightGripAction; // Right Hand / Select

        [Header("Climb")]
        [Tooltip("Sphere overlap radius for detecting Climbable colliders around the palm.")]
        [SerializeField] private float grabDetectRadius = 0.14f;
        [Tooltip("Frames of hand velocity averaged into the release fling.")]
        [SerializeField] private int velocityHistoryFrames = 5;
        [Tooltip("Multiplier from averaged hand speed to the release launch velocity.")]
        [SerializeField] private float flingScale = 1.1f;
        [Tooltip("Cap on the release launch speed (m/s) so no wrist flick can rocket the player.")]
        [SerializeField] private float maxFlingSpeed = 5.5f;

        private CharacterController controller;
        private Transform leftHand, rightHand;
        private Grabber leftGrabber, rightGrabber;
        private Transform anchorHand;
        private bool anchorIsLeft;
        private Vector3 lastAnchorPos;
        private ZeroGGrabLocomotion.CircularBuffer<Vector3> velocityHistory;

        private static readonly Collider[] OverlapBuf = new Collider[8];

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            velocityHistory = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(velocityHistoryFrames);
        }

        private void OnEnable()
        {
            leftGripAction?.action?.Enable();
            rightGripAction?.action?.Enable();
        }

        private void OnDisable()
        {
            leftGripAction?.action?.Disable();
            rightGripAction?.action?.Disable();
            // Teardown: never leave locomotion frozen; drop the anchor without a fling.
            if (anchorHand != null)
            {
                anchorHand = null;
                var loco = ContinuousLocomotion.Instance;
                if (loco != null) loco.MovementSuspended = false;
            }
        }

        private void Update()
        {
            ResolveRigRefs();

            if (anchorHand != null && !HandStillValid(anchorIsLeft))
                Release(applyFling: true);

            if (anchorHand == null)
            {
                if (TryAnchor(leftHand, leftGrabber, leftGripAction, true)) { }
                else TryAnchor(rightHand, rightGrabber, rightGripAction, false);
            }

            if (anchorHand == null) return;

            // Pull the rig by the inverse hand delta. Velocity is sampled BEFORE the move — the
            // hands are rig children, so moving the rig cancels the hand's world delta.
            Vector3 handDelta = anchorHand.position - lastAnchorPos;
            if (Time.deltaTime > 0f) velocityHistory.Add(handDelta / Time.deltaTime);
            controller.Move(-handDelta);
            lastAnchorPos = anchorHand.position;
        }

        private void ResolveRigRefs()
        {
            if (leftHand != null || VRRig.Instance == null) return;
            leftHand = VRRig.Instance.LeftHand;
            rightHand = VRRig.Instance.RightHand;
            if (leftHand != null) leftGrabber = leftHand.GetComponent<Grabber>();
            if (rightHand != null) rightGrabber = rightHand.GetComponent<Grabber>();
        }

        private bool TryAnchor(Transform hand, Grabber grabber, InputActionReference grip, bool isLeft)
        {
            if (hand == null) return false;
            if (grip == null || grip.action == null || !grip.action.IsPressed()) return false;
            if (grabber != null && grabber.IsHolding) return false; // katana hand can't climb
            if (!HandOverClimbable(hand)) return false;

            anchorHand = hand;
            anchorIsLeft = isLeft;
            lastAnchorPos = hand.position;
            velocityHistory = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(velocityHistoryFrames);
            var loco = ContinuousLocomotion.Instance;
            if (loco != null) loco.MovementSuspended = true;
            Haptics.Pulse(isLeft ? XRNode.LeftHand : XRNode.RightHand, 0.35f, 0.05f);
            return true;
        }

        private bool HandStillValid(bool isLeft)
        {
            var grip = isLeft ? leftGripAction : rightGripAction;
            if (grip == null || grip.action == null || !grip.action.IsPressed()) return false;
            // Once anchored, a small drift off the hold is forgiven (1.5x radius) so jittery
            // tracking doesn't drop the player mid-climb.
            return HandOverClimbable(anchorHand, grabDetectRadius * 1.5f);
        }

        private void Release(bool applyFling)
        {
            var wasLeft = anchorIsLeft;
            anchorHand = null;

            var loco = ContinuousLocomotion.Instance;
            if (loco == null) return;
            loco.MovementSuspended = false;

            if (!applyFling) return;
            Vector3 avg = velocityHistory.Average((a, b) => a + b, (sum, s) => sum * s);
            loco.AddAirImpulse(ClampFling(-avg, flingScale, maxFlingSpeed));
            Haptics.Pulse(wasLeft ? XRNode.LeftHand : XRNode.RightHand, 0.2f, 0.04f);
        }

        private bool HandOverClimbable(Transform hand, float radius = -1f)
        {
            if (hand == null) return false;
            if (radius <= 0f) radius = grabDetectRadius;
            int n = Physics.OverlapSphereNonAlloc(hand.position, radius, OverlapBuf,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (OverlapBuf[i].GetComponentInParent<Climbable>() != null) return true;
            }
            return false;
        }

        /// <summary>Pure release-fling rule: hand velocity inverted by the caller, scaled, and speed-
        /// capped so no tracking spike can launch the player unrealistically. Public + unit-tested.</summary>
        public static Vector3 ClampFling(Vector3 inverseHandVelocity, float scale, float maxSpeed)
        {
            Vector3 v = inverseHandVelocity * scale;
            float speed = v.magnitude;
            return speed > maxSpeed ? v * (maxSpeed / speed) : v;
        }
    }
}

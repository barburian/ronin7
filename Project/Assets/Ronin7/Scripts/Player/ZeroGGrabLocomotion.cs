using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Grab-based locomotion in zero-gravity. When grip is held over a ZeroGHandle trigger,
    /// anchors the grab and pulls the rig toward the hand. On release, applies accumulated
    /// hand velocity as a drift impulse via <see cref="ContinuousLocomotion.AddDriftImpulse"/>.
    /// Only active when <see cref="ContinuousLocomotion.SetZeroG"/> is enabled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ZeroGGrabLocomotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Input")]
        [SerializeField] private InputActionReference gripAction; // Button, either grip trigger

        [Header("Grab")]
        [Tooltip("Sphere overlap radius for detecting ZeroGHandle triggers.")]
        [SerializeField] private float grabDetectRadius = 0.12f;

        [Tooltip("Layer mask for ZeroGHandle colliders.")]
        [SerializeField] private LayerMask grabLayerMask;

        [Tooltip("Number of frames to average for hand velocity calculation.")]
        [SerializeField] private int velocityHistoryFrames = 5;

        private CharacterController controller;
        private bool zeroGActive = false;
        private Transform lastGrabbedHand = null;
        private Vector3 lastGrabbedHandPos = Vector3.zero;
        private CircularBuffer<Vector3> handVelocityHistory;

        private class CircularBuffer<T>
        {
            private T[] buffer;
            private int head = 0;

            public CircularBuffer(int capacity)
            {
                buffer = new T[capacity];
            }

            public void Add(T value)
            {
                buffer[head] = value;
                head = (head + 1) % buffer.Length;
            }

            public T Average(System.Func<T, T, T> add, System.Func<T, float, T> scale)
            {
                T result = buffer[0];
                for (int i = 1; i < buffer.Length; i++)
                    result = add(result, buffer[i]);
                return scale(result, 1f / buffer.Length);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            handVelocityHistory = new CircularBuffer<Vector3>(velocityHistoryFrames);
        }

        private void OnEnable()
        {
            gripAction?.action?.Enable();
        }

        private void OnDisable()
        {
            gripAction?.action?.Disable();
            ReleaseGrab();
        }

        /// <summary>Called by ZeroGCombatVolume when zero-g is enabled/disabled.</summary>
        public void SetActive(bool active)
        {
            zeroGActive = active;
            if (!active) ReleaseGrab();
        }

        private void Update()
        {
            if (!zeroGActive) return;

            bool gripHeld = gripAction != null && gripAction.action != null
                && gripAction.action.IsPressed();

            Transform activeHand = null;

            // Check both hands for grab overlap (prefer the currently grabbed hand if still holding).
            if (lastGrabbedHand != null && gripHeld && IsHandOverHandle(lastGrabbedHand))
            {
                activeHand = lastGrabbedHand;
            }
            else if (leftHand != null && gripHeld && IsHandOverHandle(leftHand))
            {
                activeHand = leftHand;
            }
            else if (rightHand != null && gripHeld && IsHandOverHandle(rightHand))
            {
                activeHand = rightHand;
            }

            if (activeHand != null && lastGrabbedHand == null)
            {
                // Begin grab.
                lastGrabbedHand = activeHand;
                lastGrabbedHandPos = activeHand.position;
            }
            else if (activeHand == null && lastGrabbedHand != null)
            {
                // Release grab.
                ReleaseGrab();
            }

            // If actively grabbing, move the rig by -hand delta each frame.
            if (lastGrabbedHand != null)
            {
                Vector3 handDelta = lastGrabbedHand.position - lastGrabbedHandPos;

                // Sample hand velocity BEFORE moving the rig — the hands are children of
                // the rig, so the Move below cancels the hand's world delta.
                Vector3 handVelocity = handDelta / Time.deltaTime;
                handVelocityHistory.Add(handVelocity);

                controller.Move(-handDelta);

                lastGrabbedHandPos = lastGrabbedHand.position;
            }
        }

        private bool IsHandOverHandle(Transform hand)
        {
            var hits = Physics.OverlapSphere(hand.position, grabDetectRadius, grabLayerMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<ZeroGHandle>() != null)
                    return true;
            }
            return false;
        }

        private void ReleaseGrab()
        {
            if (lastGrabbedHand == null) return;

            // Calculate average hand velocity and apply as drift impulse.
            Vector3 avgVelocity = handVelocityHistory.Average(
                (a, b) => a + b,
                (sum, scale) => sum * scale
            );

            var locomotion = ContinuousLocomotion.Instance;
            if (locomotion != null)
                locomotion.AddDriftImpulse(-avgVelocity);

            lastGrabbedHand = null;
            lastGrabbedHandPos = Vector3.zero;
        }
    }
}

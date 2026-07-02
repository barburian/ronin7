using System;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Anything a hand can pick up (props, the sword, etc.). Lives in Core so both Combat
    /// (sword) and Player (grabber) can use it without an assembly cycle.
    /// While held, the object is reparented to the hand and made kinematic so it follows
    /// the tracked hand exactly; on release it regains physics and inherits hand velocity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Where the hand grips this object (handle). Defaults to this transform.")]
        [SerializeField] private Transform attachPoint;

        public Transform AttachPoint => attachPoint != null ? attachPoint : transform;
        public bool IsHeld { get; private set; }
        public Transform HeldBy { get; private set; }

        /// <summary>Raised when grabbed; argument is the hand transform.</summary>
        public event Action<Transform> Grabbed;
        public event Action Released;

        private Rigidbody body;
        private Transform originalParent;

        private void Awake() => body = GetComponent<Rigidbody>();

        public void Grab(Transform hand)
        {
            if (IsHeld) return;
            IsHeld = true;
            HeldBy = hand;
            originalParent = transform.parent;

            body.isKinematic = true;
            transform.SetParent(hand, true);

            // Align our attach point to the hand's pose, then the transform parenting
            // keeps it locked to the hand each frame.
            Quaternion rotOffset = Quaternion.Inverse(AttachPoint.rotation) * transform.rotation;
            transform.rotation = hand.rotation * rotOffset;
            Vector3 posOffset = transform.position - AttachPoint.position;
            transform.position = hand.position + posOffset;

            Grabbed?.Invoke(hand);
        }

        public void Release(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (!IsHeld) return;
            IsHeld = false;
            HeldBy = null;

            transform.SetParent(originalParent, true);
            body.isKinematic = false;
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;

            Released?.Invoke();
        }
    }
}

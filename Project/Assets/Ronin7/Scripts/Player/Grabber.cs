using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// One per hand. On grip press, grabs the nearest <see cref="Grabbable"/> within reach;
    /// on release, throws it using the hand's tracked velocity. Input is read directly via
    /// the Input System (same reliable path as locomotion / pose).
    /// </summary>
    [RequireComponent(typeof(HandVelocityTracker))]
    public class Grabber : MonoBehaviour
    {
        [SerializeField] private InputActionReference gripAction; // Button (Select)
        [SerializeField] private float grabRadius = 0.12f;
        [SerializeField] private bool leftHand = true;
        [SerializeField] private HandVelocityTracker velocity;

        private Grabbable held;
        private bool wasPressed;

        /// <summary>True while this hand holds a <see cref="Grabbable"/> (e.g. the katana) — used by
        /// <see cref="WallClimbLocomotion"/> so a full hand can never also grip a climbing hold.</summary>
        public bool IsHolding => held != null;

        private static readonly Collider[] _grabHits = new Collider[8];

        private XRNode Node => leftHand ? XRNode.LeftHand : XRNode.RightHand;

        private void Awake()
        {
            if (velocity == null) velocity = GetComponent<HandVelocityTracker>();
        }

        private void OnEnable() => gripAction?.action?.Enable();
        private void OnDisable() => gripAction?.action?.Disable();

        private void Update()
        {
            bool pressed = gripAction != null && gripAction.action != null && gripAction.action.IsPressed();

            if (pressed && !wasPressed && held == null) TryGrab();
            else if (!pressed && wasPressed && held != null) ReleaseHeld();

            wasPressed = pressed;
        }

        private void TryGrab()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, _grabHits, Layers.GrabbableMask);
            Grabbable best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var g = _grabHits[i].GetComponentInParent<Grabbable>();
                if (g == null || g.IsHeld) continue;
                float sqr = (g.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = g; }
            }

            if (best == null) return;
            best.Grab(transform);
            held = best;
            Haptics.Pulse(Node, 0.4f, 0.06f);
        }

        private void ReleaseHeld()
        {
            Vector3 lin = velocity != null ? velocity.LinearVelocity : Vector3.zero;
            Vector3 ang = velocity != null ? velocity.AngularVelocity : Vector3.zero;
            held.Release(lin, ang);
            held = null;
        }
    }
}

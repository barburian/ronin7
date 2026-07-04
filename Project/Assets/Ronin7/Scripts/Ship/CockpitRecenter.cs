using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Ship
{
    /// <summary>
    /// Snaps the cockpit's horizontal position and yaw onto the player's current head pose
    /// when the bound action fires (a hold on the right secondary button by default). Height
    /// stays put so the floor doesn't jump under the player. Useful when the seated rig has
    /// drifted relative to the world-origin cockpit and the player wants the ship to face
    /// where they're looking.
    /// </summary>
    public class CockpitRecenter : MonoBehaviour
    {
        [SerializeField] private InputActionReference recenterAction;
        [Tooltip("Head/camera transform whose world XZ + yaw becomes the new cockpit pose.")]
        [SerializeField] private Transform head;
        [Tooltip("Cockpit root to move. Defaults to this transform if left empty.")]
        [SerializeField] private Transform cockpit;

        private InputAction resolved;

        private void Awake()
        {
            if (cockpit == null) cockpit = transform;
        }

        private void OnEnable()
        {
            resolved = InputResolver.Resolve(recenterAction, string.Empty, string.Empty, "CockpitRecenter");
            if (resolved == null) return;
            resolved.performed += OnPerformed;
        }

        private void OnDisable()
        {
            if (resolved != null) resolved.performed -= OnPerformed;
            resolved = null;
        }

        private void OnPerformed(InputAction.CallbackContext _)
        {
            if (head == null || cockpit == null) return;
            Vector3 p = head.position;
            cockpit.position = new Vector3(p.x, cockpit.position.y, p.z);
            if (TryFlattenYaw(head.forward, out Quaternion yaw)) cockpit.rotation = yaw;
        }

        /// <summary>
        /// Flattens a head-forward vector onto the horizontal (XZ) plane and returns the yaw-only
        /// look rotation for it. Using <c>head.eulerAngles.y</c> directly (the old approach) reads
        /// the Euler decomposition's yaw component, which can jump when the head is pitched/rolled
        /// (gimbal-order artifacts); projecting the forward vector onto the up plane before deriving
        /// yaw avoids that. Returns false (leaving <paramref name="yawOnly"/> at identity) when the
        /// head looks straight up/down — a flattened-to-zero forward has no defined yaw, so the
        /// caller should leave the cockpit's rotation unchanged rather than snap to an arbitrary
        /// heading. Pure, so it is unit-testable.
        /// </summary>
        internal static bool TryFlattenYaw(Vector3 headForward, out Quaternion yawOnly)
        {
            Vector3 flat = Vector3.ProjectOnPlane(headForward, Vector3.up);
            if (flat.sqrMagnitude <= 1e-6f)
            {
                yawOnly = Quaternion.identity;
                return false;
            }
            yawOnly = Quaternion.LookRotation(flat, Vector3.up);
            return true;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Drives a transform from XR pose input actions (device position + rotation), read
    /// directly through the Input System. Used for the head camera and the hands instead
    /// of XRI's TrackedPoseDriver so the wiring matches our (working) locomotion path and
    /// serializes reliably from the rig builder.
    /// </summary>
    [DisallowMultipleComponent]
    public class PoseFollower : MonoBehaviour
    {
        [SerializeField] private InputActionReference positionAction; // Vector3
        [SerializeField] private InputActionReference rotationAction; // Quaternion

        [Tooltip("Apply the tracked pose in local space (true for rigs parented under an XR Origin).")]
        [SerializeField] private bool useLocalSpace = true;

        private void OnEnable()
        {
            positionAction?.action?.Enable();
            rotationAction?.action?.Enable();
            Application.onBeforeRender += ApplyPose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyPose;
            positionAction?.action?.Disable();
            rotationAction?.action?.Disable();
        }

        // Applied in both Update (so the pose is correct for game logic / physics this frame) and
        // onBeforeRender (so the displayed pose uses the latest tracking just before rendering, cutting
        // perceived latency). The double-apply is intentional, not redundant.
        private void Update() => ApplyPose();

        private void ApplyPose()
        {
            if (positionAction != null && positionAction.action != null)
            {
                Vector3 p = positionAction.action.ReadValue<Vector3>();
                if (useLocalSpace) transform.localPosition = p;
                else transform.position = p;
            }

            if (rotationAction != null && rotationAction.action != null)
            {
                Quaternion r = rotationAction.action.ReadValue<Quaternion>();
                if (useLocalSpace) transform.localRotation = r;
                else transform.rotation = r;
            }
        }
    }
}

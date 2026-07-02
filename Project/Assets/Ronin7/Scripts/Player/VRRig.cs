using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Convenience reference holder for the player rig so other systems can find the
    /// head, hands, and origin without scene-wide searches. Populated by the rig builder.
    /// </summary>
    public class VRRig : MonoBehaviour
    {
        public static VRRig Instance { get; private set; }

        [Tooltip("The XR Origin root that gets moved through the world.")]
        public Transform Origin;
        [Tooltip("A transform we own between the XROrigin Camera Offset and the head; its Y is " +
                 "shifted to calibrate eye height (XROrigin would clobber the Camera Offset itself).")]
        public Transform EyeHeightRoot;
        [Tooltip("The tracked head/camera transform.")]
        public Transform Head;
        [Tooltip("Left controller anchor.")]
        public Transform LeftHand;
        [Tooltip("Right controller anchor.")]
        public Transform RightHand;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Origin == null) Origin = transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

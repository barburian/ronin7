using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// A sliding sci-fi door: two panels that slide apart when the player (Camera.main) comes
    /// within <see cref="triggerRadius"/> and slide shut again when they leave. The panels keep
    /// their colliders, so a closed door blocks the player.
    ///
    /// "Locking" a door is done by leaving this component's GameObject inactive — no proximity
    /// logic runs and the panels stay in their closed positions. Activating the GameObject unlocks
    /// it. This pairs with <c>MissionStepKind.Trigger</c>, which simply activates GameObjects.
    /// </summary>
    public class ProximityDoor : MonoBehaviour
    {
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;

        [Tooltip("Local-space offset the left panel slides by when opening. The right panel slides the opposite way.")]
        [SerializeField] private Vector3 openOffset = new Vector3(1.1f, 0f, 0f);

        [Tooltip("Horizontal distance from the player camera at which the door opens.")]
        [SerializeField] private float triggerRadius = 3f;

        [SerializeField] private float slideSpeed = 4f;

        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioSource audioSource;

        private Vector3 leftClosed;
        private Vector3 rightClosed;
        private bool captured;
        private bool wasOpen;

        private void Awake()
        {
            CaptureClosedPositions();
        }

        private void CaptureClosedPositions()
        {
            if (captured) return;
            if (leftPanel != null) leftClosed = leftPanel.localPosition;
            if (rightPanel != null) rightClosed = rightPanel.localPosition;
            captured = true;
        }

        private void Update()
        {
            CaptureClosedPositions();

            bool open = false;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                open = toCam.magnitude <= triggerRadius;
            }

            // Play audio on state transitions.
            if (open && !wasOpen && openClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(openClip);
            }
            else if (!open && wasOpen && closeClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(closeClip);
            }
            wasOpen = open;

            float step = slideSpeed * Time.deltaTime;
            if (leftPanel != null)
            {
                Vector3 target = open ? leftClosed + openOffset : leftClosed;
                leftPanel.localPosition = Vector3.MoveTowards(leftPanel.localPosition, target, step);
            }
            if (rightPanel != null)
            {
                Vector3 target = open ? rightClosed - openOffset : rightClosed;
                rightPanel.localPosition = Vector3.MoveTowards(rightPanel.localPosition, target, step);
            }
        }
    }
}

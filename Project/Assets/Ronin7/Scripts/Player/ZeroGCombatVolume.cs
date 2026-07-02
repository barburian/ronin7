using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Trigger volume that enables zero-gravity combat mode when the player enters.
    /// Multiple overlapping volumes are ref-counted; zero-g only disables when the last
    /// volume is exited. Enables/disables grab locomotion in sync.
    /// </summary>
    public class ZeroGCombatVolume : MonoBehaviour
    {
        [Header("Zero-G Settings")]
        [Tooltip("Exponential damping coefficient for drift velocity.")]
        [SerializeField] private float driftDamping = 0.6f;

        private static int volumeCounter = 0;
        private bool playerInside = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCounter()
        {
            volumeCounter = 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (playerInside) return; // Already counted
            playerInside = true;

            volumeCounter++;
            if (volumeCounter == 1)
            {
                // First volume entered: enable zero-g globally.
                var locomotion = ContinuousLocomotion.Instance;
                if (locomotion != null)
                    locomotion.SetZeroG(true, driftDamping);

                var grabLoco = FindAnyObjectByType<ZeroGGrabLocomotion>();
                if (grabLoco != null)
                    grabLoco.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (!playerInside) return; // Not counted
            playerInside = false;

            volumeCounter = Mathf.Max(0, volumeCounter - 1);
            if (volumeCounter == 0)
            {
                // Last volume exited: disable zero-g globally.
                var locomotion = ContinuousLocomotion.Instance;
                if (locomotion != null)
                    locomotion.SetZeroG(false, driftDamping);

                var grabLoco = FindAnyObjectByType<ZeroGGrabLocomotion>();
                if (grabLoco != null)
                    grabLoco.SetActive(false);
            }
        }

        private static bool IsPlayer(Collider other) =>
            other.GetComponentInParent<CharacterController>() != null;
    }
}

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

        /// <summary>Pure transition for entering a volume: bumps the ref count and reports whether
        /// zero-g should be (re)enabled (only on the first overlapping volume).</summary>
        public static (int count, bool shouldEnable) Enter(int counter)
        {
            int count = counter + 1;
            return (count, count == 1);
        }

        /// <summary>Pure transition for exiting a volume: drops the ref count (floored at zero) and
        /// reports whether zero-g should be disabled (only once the last overlapping volume exits).</summary>
        public static (int count, bool shouldDisable) Exit(int counter)
        {
            int count = Mathf.Max(0, counter - 1);
            return (count, count == 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (playerInside) return; // Already counted
            playerInside = true;

            var (count, shouldEnable) = Enter(volumeCounter);
            volumeCounter = count;
            if (shouldEnable)
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
            ExitVolume();
        }

        private void OnDestroy()
        {
            // Unity doesn't fire OnTriggerExit when a trigger is destroyed while something is still
            // inside it. Without this, a volume destroyed mid-overlap would leak its ref-count
            // increment forever, permanently breaking zero-g for the rest of the session.
            ExitVolume();
        }

        private void ExitVolume()
        {
            if (!playerInside) return; // Not counted
            playerInside = false;

            var (count, shouldDisable) = Exit(volumeCounter);
            volumeCounter = count;
            if (shouldDisable)
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

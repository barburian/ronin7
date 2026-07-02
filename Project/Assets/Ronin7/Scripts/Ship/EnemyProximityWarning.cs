using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Kessler co-pilot shouts a warning when a hostile first closes within range; hysteresis and
    /// cooldown prevent spam during a sustained dogfight. When armed and an enemy enters
    /// <see cref="warnDistance"/>, plays a warning clip; once all enemies clear beyond
    /// <see cref="rearmDistance"/> and the cooldown has elapsed, rearming allows the next warning.
    /// </summary>
    public class EnemyProximityWarning : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Warning VO variants, one picked per warning.")]
        private AudioClip[] clips;

        [SerializeField] private AudioSource audioSource;

        [SerializeField]
        [Tooltip("Distance at which a live enemy triggers a warning (must be less than rearmDistance).")]
        private float warnDistance = 160f;

        [SerializeField]
        [Tooltip("Distance at which the system rearms after a warning (must be greater than warnDistance).")]
        private float rearmDistance = 200f;

        [SerializeField]
        [Tooltip("Seconds after a warning before the system is eligible to warn again.")]
        private float cooldownSeconds = 25f;

        [SerializeField]
        [Tooltip("Seconds between nearest-enemy scans.")]
        private float pollInterval = 0.5f;

        private bool armed = true;
        private float lastWarnTime = -999f;
        private float nextPollTime;
        private int nextClipIndex;

        private void Update()
        {
            if (Time.time < nextPollTime) return;
            nextPollTime = Time.time + pollInterval;

            // Find nearest live enemy.
            Vector3 shipPos = ShipController.Instance != null ? ShipController.Instance.ShipPosition : Vector3.zero;
            float nearestDistance = float.PositiveInfinity;
            var list = EnemyShip.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive) continue;
                float d = (e.UniversePosition - shipPos).magnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                }
            }

            // Decision logic.
            if (ShouldWarn(armed, nearestDistance, warnDistance))
            {
                PlayWarning();
                armed = false;
                lastWarnTime = Time.time;
            }
            else if (ShouldRearm(armed, nearestDistance, rearmDistance, Time.time - lastWarnTime, cooldownSeconds))
            {
                armed = true;
            }
        }

        /// <summary>
        /// Determines if a warning should play: the system must be armed and an enemy must be inside warn distance.
        /// </summary>
        public static bool ShouldWarn(bool armed, float nearestEnemyDistance, float warnDistance)
        {
            return armed && nearestEnemyDistance < warnDistance;
        }

        /// <summary>
        /// Determines if the system should rearm: disarmed, all enemies beyond rearm distance,
        /// and sufficient cooldown time has elapsed since the last warning.
        /// </summary>
        public static bool ShouldRearm(bool armed, float nearestEnemyDistance, float rearmDistance, float timeSinceWarn, float cooldownSeconds)
        {
            return !armed && nearestEnemyDistance > rearmDistance && timeSinceWarn >= cooldownSeconds;
        }

        private void PlayWarning()
        {
            if (clips == null || clips.Length == 0) return;
            if (audioSource == null) return;

            AudioClip clip = clips[nextClipIndex % clips.Length];
            nextClipIndex++;

            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}

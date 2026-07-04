using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Slow "breathing" light driver for quiet scene beats — eases a <see cref="Light"/>'s intensity
    /// through a sine cycle between <see cref="minIntensity"/> and <see cref="maxIntensity"/>. Purely
    /// additive ambience; with no <see cref="targetLight"/> assigned it does nothing.
    /// </summary>
    public class AmbientLightPulse : MonoBehaviour
    {
        /// <summary>
        /// Intensity at <paramref name="time"/> seconds into a <paramref name="periodSeconds"/>-long sine
        /// cycle, eased between <paramref name="min"/> and <paramref name="max"/>. Guards against a
        /// non-positive period (returns <paramref name="min"/> rather than dividing by zero).
        /// </summary>
        public static float Intensity(float time, float periodSeconds, float min, float max)
        {
            if (periodSeconds <= 0f) return min;
            return Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * time / periodSeconds));
        }

        [SerializeField] private Light targetLight;
        [SerializeField] private float periodSeconds = 6f;
        [SerializeField] private float minIntensity;
        [SerializeField] private float maxIntensity;

        private float accumulator;

        private void Update()
        {
            if (targetLight == null) return;
            accumulator += Time.deltaTime;
            targetLight.intensity = Intensity(accumulator, periodSeconds, minIntensity, maxIntensity);
        }
    }
}

using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Perlin-noise console/cockpit flicker. Band-limited between <see cref="minIntensity"/> and
    /// <see cref="maxIntensity"/> and driven by continuous Perlin noise rather than random jumps, so
    /// it can never strobe. Additive; with no <see cref="targetLight"/> assigned it does nothing.
    /// </summary>
    public class ConsoleFlickerLight : MonoBehaviour
    {
        /// <summary>
        /// Flicker intensity at <paramref name="time"/> seconds, eased between <paramref name="min"/>
        /// and <paramref name="max"/> by sampling continuous Perlin noise along the seeded row. Guards
        /// a degenerate range (returns <paramref name="min"/> rather than an inverted lerp).
        /// </summary>
        public static float FlickerIntensity(float time, float speed, float seed, float min, float max) =>
            max <= min ? min : Mathf.Lerp(min, max, Mathf.PerlinNoise(seed, time * speed));

        [SerializeField] private Light targetLight;
        [SerializeField] private float minIntensity = 0.8f;
        [SerializeField] private float maxIntensity = 1.2f;
        [Tooltip("Keep <= 1.0 and the min/max intensity delta modest — VR photosensitivity/comfort. " +
                 "Smoothness comes from Perlin continuity, not this value.")]
        [SerializeField] private float speed = 0.6f;
        [SerializeField] private float seed;

        private float accumulator;

        private void Update()
        {
            if (targetLight == null) return;
            accumulator += Time.deltaTime;
            targetLight.intensity = FlickerIntensity(accumulator, speed, seed, minIntensity, maxIntensity);
        }
    }
}

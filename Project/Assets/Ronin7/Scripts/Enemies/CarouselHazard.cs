using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Carousel mechanic: spins a pivot (carrying orbiting enemies and decorative objects)
    /// around the player. Speed ramps from minSpeed to maxSpeed over time. The player's floor
    /// does not move (VR comfort) — only the pivot rotates.
    /// </summary>
    public class CarouselHazard : MonoBehaviour
    {
        [SerializeField] private Transform orbitPivot;
        [SerializeField] private Transform[] decor;
        [SerializeField] private float minSpeed = 12f;
        [SerializeField] private float maxSpeed = 60f;
        [SerializeField] private float rampDuration = 120f;

        private float elapsed;

        public float CurrentSpeed => SpeedAt(elapsed, minSpeed, maxSpeed, rampDuration);

        /// <summary>Pure helper: compute speed at elapsed time. For testing and inspection.</summary>
        public static float SpeedAt(float elapsed, float min, float max, float ramp)
        {
            if (ramp <= 0f) return max;
            float t = Mathf.Clamp01(elapsed / ramp);
            return Mathf.Lerp(min, max, t);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float speed = CurrentSpeed;

            if (orbitPivot != null)
            {
                orbitPivot.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
            }

            if (decor != null)
            {
                foreach (var d in decor)
                {
                    if (d != null)
                    {
                        d.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
                    }
                }
            }
        }
    }
}

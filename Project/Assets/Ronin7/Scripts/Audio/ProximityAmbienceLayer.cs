using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>
    /// Looping localized ambient bed whose volume fades with distance to the listener — full volume
    /// inside <see cref="innerRadius"/>, easing to silence by <see cref="outerRadius"/>. Volume eases
    /// toward the target via <see cref="Mathf.MoveTowards"/> so it never pops. Null-safe if
    /// <see cref="source"/> is unassigned or no listener can be resolved.
    /// </summary>
    public class ProximityAmbienceLayer : MonoBehaviour
    {
        /// <summary>
        /// Bed volume at <paramref name="distance"/> from the listener: <paramref name="maxVolume"/>
        /// inside <paramref name="innerRadius"/>, easing to 0 by <paramref name="outerRadius"/>.
        /// </summary>
        public static float ComputeLayerVolume(float distance, float innerRadius, float outerRadius, float maxVolume)
            => maxVolume * (1f - Mathf.Clamp01(Mathf.InverseLerp(innerRadius, outerRadius, distance)));

        [SerializeField] private AudioSource source;
        [SerializeField] private float innerRadius = 2f;
        [SerializeField] private float outerRadius = 12f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;
        [SerializeField] private float fadeSpeed = 1f;

        private Transform listener;

        private void Update()
        {
            if (source == null) return;
            if (listener == null)
            {
                var cam = Camera.main;
                listener = cam != null ? cam.transform : null;
            }
            if (listener == null) return;

            float distance = Vector3.Distance(transform.position, listener.position);
            float target = ComputeLayerVolume(distance, innerRadius, outerRadius, maxVolume);
            source.volume = Mathf.MoveTowards(source.volume, target, fadeSpeed * Time.deltaTime);
        }
    }
}

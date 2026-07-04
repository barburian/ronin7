using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Light counterpart to ProximityAmbienceLayer: a beacon that brightens as the player approaches —
    /// full brightness inside <see cref="innerRadius"/>, easing down to the minimum by
    /// <see cref="outerRadius"/>. Intensity eases toward the target via
    /// <see cref="Mathf.MoveTowards"/> so it never pops. Null-safe if <see cref="targetLight"/> is
    /// unassigned or no listener can be resolved.
    /// </summary>
    public class ProximityGlowLight : MonoBehaviour
    {
        /// <summary>
        /// Light intensity at <paramref name="distance"/> from the listener: <paramref name="maxIntensity"/>
        /// inside <paramref name="innerRadius"/>, easing to <paramref name="minIntensity"/> by
        /// <paramref name="outerRadius"/>.
        /// </summary>
        public static float GlowIntensity(float distance, float innerRadius, float outerRadius, float minIntensity, float maxIntensity) =>
            Mathf.Lerp(minIntensity, maxIntensity, 1f - Mathf.Clamp01(Mathf.InverseLerp(innerRadius, outerRadius, distance)));

        [SerializeField] private Light targetLight;
        [SerializeField] private float innerRadius = 2f;
        [SerializeField] private float outerRadius = 12f;
        [SerializeField] private float minIntensity;
        [SerializeField] private float maxIntensity = 2f;
        [SerializeField] private float responseSpeed = 2f;

        private Transform listener;

        private void Update()
        {
            if (targetLight == null) return;
            if (listener == null)
            {
                var cam = Camera.main;
                listener = cam != null ? cam.transform : null;
            }
            if (listener == null) return;

            float distance = Vector3.Distance(transform.position, listener.position);
            float target = GlowIntensity(distance, innerRadius, outerRadius, minIntensity, maxIntensity);
            targetLight.intensity = Mathf.MoveTowards(targetLight.intensity, target, responseSpeed * Time.deltaTime);
        }
    }
}

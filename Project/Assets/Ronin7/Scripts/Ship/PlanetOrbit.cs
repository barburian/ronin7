using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Slow circular orbit around a center body, driven entirely in the parent's LOCAL space so it
    /// composes with the moving-universe flight model (the universe root is rotated/translated by
    /// <see cref="ShipController"/>; children keep their localPosition semantics). Used for the
    /// Galaxy 1 planets orbiting the sun, and for moons orbiting a planet — pass the parent planet
    /// as <see cref="center"/> and the center's position is re-read every frame, so the moon
    /// follows its planet's own orbit (one frame of lag at these angular speeds is invisible).
    /// </summary>
    public class PlanetOrbit : MonoBehaviour
    {
        [Tooltip("Body to orbit (the sun visual, or a parent planet for a moon). Must live under the same universe root.")]
        [SerializeField] private Transform center;
        [Tooltip("Orbit radius in universe units.")]
        [SerializeField] private float radius = 850f;
        [Tooltip("Orbital angular speed in degrees per second. Keep small — planets should creep, not spin.")]
        [SerializeField] private float angularSpeedDeg = 0.2f;
        [Tooltip("Angle (degrees) along the orbit at scene start; pick so the t=0 pose matches the authored layout.")]
        [SerializeField] private float startAngleDeg;
        [Tooltip("Constant height offset above/below the center's orbital plane.")]
        [SerializeField] private float yOffset;

        private float angleDeg;

        private void Awake()
        {
            angleDeg = startAngleDeg;
            Apply();
        }

        private void Update()
        {
            angleDeg += angularSpeedDeg * Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            if (center == null || transform.parent == null) return;

            // Center position expressed in THIS transform's parent space. When both share the same
            // parent (the common case: planets and the sun are direct universe children) this is just
            // the center's localPosition; otherwise convert through world space.
            Vector3 c = center.parent == transform.parent
                ? center.localPosition
                : transform.parent.InverseTransformPoint(center.position);

            float rad = angleDeg * Mathf.Deg2Rad;
            transform.localPosition = c + new Vector3(Mathf.Cos(rad) * radius, yOffset, Mathf.Sin(rad) * radius);
        }
    }
}

using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// P5-03 blade trail. Sits next to the blade tip's TrailRenderer: emits only while the blade's smoothed swing
    /// speed (<see cref="BladeDamager.Speed"/>, the EMA) is above <see cref="MinSpeed"/>, spaces vertices 0.03 m
    /// apart and caps the trail at about 60 vertices (<see cref="MaxVertices"/> over the fastest swing), and swaps to a
    /// magenta gradient while Overdrive burns. No material change, so it costs no SetPass.
    /// A trail with no BladeDamager above it (the enemy blade) is left untouched.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public class BladeTrailDriver : MonoBehaviour
    {
        public const float MinSpeed = 2f;
        public const float MinVertexDistance = 0.03f;
        public const int MaxVertices = 60;
        public const float TopSpeed = 15f; // m/s a hard swing reaches; sets the trail time

        private static readonly Color Magenta = new Color(1f, 0.2f, 0.75f);

        private TrailRenderer trail;
        private BladeDamager blade;
        private Gradient normal, overdrive;
        private bool wasOverdrive;

        /// <summary>Trail lifetime that keeps the vertex count at or under <see cref="MaxVertices"/> at <see cref="TopSpeed"/>.</summary>
        public static float TrailTime => MaxVertices * MinVertexDistance / TopSpeed;

        public static bool ShouldEmit(float bladeSpeed) => bladeSpeed > MinSpeed;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
            blade = GetComponentInParent<BladeDamager>();
            if (blade == null) { enabled = false; return; }
            trail.minVertexDistance = MinVertexDistance;
            trail.time = TrailTime;
            normal = trail.colorGradient;
            overdrive = new Gradient();
            var hdr = Magenta * 2.5f;
            overdrive.SetKeys(new[] { new GradientColorKey(hdr, 0f), new GradientColorKey(hdr, 1f) },
                              new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        }

        private void OnDisable() { if (trail != null) trail.emitting = false; }

        private void Update()
        {
            bool emit = ShouldEmit(blade.Speed);
            if (trail.emitting != emit) trail.emitting = emit;
            bool od = OverdriveController.Burning;
            if (od == wasOverdrive) return;
            wasOverdrive = od;
            trail.colorGradient = od ? overdrive : normal;
        }
    }
}

using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Pillar 3 of the sun-navigation system (see <c>Docs/SunNavigation-Design.md</c>): glare / sun-side
    /// ambush. The static <see cref="Intensity"/> is the pure, unit-tested core; the component is a thin
    /// per-frame driver that washes the screen as the player looks sun-ward — discouraging a sun-ward
    /// charge and rewarding the classic "keep the sun at your back" counter-play.
    ///
    /// COMFORT: the wash is a gentle, smoothed white brighten — never a strobe. The ramp speed and a low
    /// <see cref="maxBrightness"/> cap keep it inside the comfort band; tune both in-headset.
    ///
    /// OFF BY DEFAULT: with no <see cref="sun"/> assigned the value stays 0 and nothing draws. The overlay
    /// target is a serialized <see cref="Renderer"/> hook (the design's in-headset hand-off #3): point it at
    /// a head-pinned, transparent-unlit white quad — e.g. a child of the head camera using
    /// <see cref="Core.VrUiMaterials.CreateTransparentUnlit"/> (the same factory the screen fader / comfort
    /// vignette use) with a white <see cref="glareColor"/>. Unassigned = <see cref="Current"/> still computes
    /// (usable by a future EnemyShip sun-side-approach hook) but nothing is drawn.
    /// </summary>
    [DisallowMultipleComponent]
    public class SunGlare : MonoBehaviour
    {
        /// <summary>
        /// Glare 0..1 from the angle between <paramref name="viewForward"/> and <paramref name="toSun"/>:
        /// 0 when the sun is outside <paramref name="startAngleDeg"/> of the look direction, ramping
        /// monotonically to 1 by <paramref name="fullAngleDeg"/> (looking straight at the sun).
        /// <paramref name="fullAngleDeg"/> is the tighter (smaller) angle near dead-centre.
        /// </summary>
        public static float Intensity(Vector3 viewForward, Vector3 toSun, float startAngleDeg, float fullAngleDeg)
            => Mathf.InverseLerp(startAngleDeg, fullAngleDeg, Vector3.Angle(viewForward, toSun));

        [Tooltip("The sun visual (a universe child). Glare is read from the head's angle to this. Unassigned = no glare.")]
        [SerializeField] private Transform sun;
        [Tooltip("Head/eye transform whose forward is the look direction. Defaults to Camera.main when unassigned.")]
        [SerializeField] private Transform head;
        [Tooltip("Sun-angle (deg from view centre) at which glare STARTS. Sun outside this = no wash.")]
        [SerializeField] private float startAngleDeg = 35f;
        [Tooltip("Sun-angle (deg) at which glare is FULL (looking near-straight at the sun).")]
        [SerializeField] private float fullAngleDeg = 8f;
        [Tooltip("Peak overlay alpha at full glare. Kept low for comfort — a wash, not a white-out.")]
        [SerializeField, Range(0f, 1f)] private float maxBrightness = 0.5f;
        [Tooltip("How fast the wash eases in/out (1/sec). Gentle ramp — never a strobe.")]
        [SerializeField] private float rampSpeed = 4f;
        [Tooltip("White overlay quad to brighten (head-pinned, transparent-unlit). Unassigned = value computed but nothing drawn.")]
        [SerializeField] private Renderer glareOverlay;
        [SerializeField] private Color glareColor = Color.white;

        private MaterialPropertyBlock mpb;
        private float current; // smoothed 0..1 glare

        /// <summary>Smoothed glare this frame (0 = clear, 1 = full wash). Exposed for HUD / AI sun-side hooks.</summary>
        public float Current => current;

        private void LateUpdate()
        {
            Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);

            float target = 0f;
            if (sun != null && h != null)
            {
                // Both already world-space (the sun renders at its swept world position, the head looks
                // along h.forward), so the raw angle between them is the look-to-sun angle we want.
                Vector3 toSun = sun.position - h.position;
                target = Intensity(h.forward, toSun, startAngleDeg, fullAngleDeg);
            }

            // Ease toward the target so the wash never pops on/off (comfort-safe, no strobe).
            current = Mathf.MoveTowards(current, target, rampSpeed * Time.deltaTime);
            ApplyOverlay();
        }

        private void ApplyOverlay()
        {
            if (glareOverlay == null) return;
            mpb ??= new MaterialPropertyBlock();
            glareOverlay.enabled = current > 0.001f;
            Color c = glareColor;
            c.a = current * maxBrightness;
            glareOverlay.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c); // URP
            mpb.SetColor("_Color", c);     // legacy fallback
            glareOverlay.SetPropertyBlock(mpb);
        }
    }
}

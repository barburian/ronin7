using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// VR comfort tunnelling vignette. Renders an inward-facing dark ring parented to the
    /// camera that closes in from the edges as <see cref="SetIntensity"/> rises, reducing the
    /// peripheral optical-flow that drives motion sickness during rotation and acceleration.
    ///
    /// Built entirely from code (no scene/prefab assets, no post-processing dependency) so it
    /// works under URP in stereo: it is a flat ring quad-strip placed just in front of the eyes
    /// on a transparent unlit material. The aperture radius shrinks with intensity; at 0 the ring
    /// is fully open (invisible), at 1 it tightens to <see cref="minAperture"/> of the view.
    /// </summary>
    [DisallowMultipleComponent]
    public class ComfortVignette : MonoBehaviour
    {
        [Tooltip("Distance in front of the eyes the vignette ring sits (metres).")]
        [SerializeField] private float distance = 0.5f;
        [Tooltip("Fraction of the view still visible at full intensity (smaller = tighter tunnel).")]
        [SerializeField, Range(0.1f, 0.9f)] private float minAperture = 0.45f;
        [Tooltip("How fast the aperture eases toward its target (1/sec).")]
        [SerializeField] private float responsiveness = 8f;
        [SerializeField] private Color color = new Color(0f, 0f, 0f, 1f);

        private const int Segments = 48;
        private MeshRenderer ring;
        private MaterialPropertyBlock mpb;
        private float current;   // smoothed 0..1 intensity
        private float target;    // requested 0..1 intensity
        private Mesh mesh;

        /// <summary>Request a vignette strength this frame (0 = open, 1 = tightest). Latest call wins.</summary>
        public void SetIntensity(float value) => target = Mathf.Clamp01(value);

        private void Awake()
        {
            BuildRing();
            mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            // Ease toward the requested intensity so the tunnel never pops on/off.
            current = Mathf.MoveTowards(current, target, responsiveness * Time.deltaTime);

            // Keep the ring pinned in front of the eyes and scale its hole with intensity.
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;

            // aperture goes 1 (fully open) -> minAperture (tight) as intensity rises.
            float aperture = Mathf.Lerp(1f, minAperture, current);
            transform.localScale = new Vector3(distance * aperture, distance * aperture, 1f);

            if (ring != null)
            {
                ring.enabled = current > 0.001f;
                ring.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color); // URP/Lit + Unlit
                mpb.SetColor("_Color", color);     // legacy fallback
                ring.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Build a flat annulus (ring) facing the eyes: opaque outer edge, hole in the centre.</summary>
        private void BuildRing()
        {
            var go = new GameObject("VignetteRing");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            ring = go.AddComponent<MeshRenderer>();
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.material = Ronin7.Core.VrUiMaterials.CreateTransparentUnlit();

            // The ring spans from the aperture edge (inner) out to well beyond the FOV (outer),
            // so the dark band always reaches the screen edge regardless of headset FOV.
            const float innerR = 1.0f;   // scaled by transform; this is the visible hole edge
            const float outerR = 6.0f;   // large enough to exceed any HMD FOV

            var verts = new Vector3[(Segments + 1) * 2];
            var tris = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, s * innerR, 0f);
                verts[i * 2 + 1] = new Vector3(c * outerR, s * outerR, 0f);
            }
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            mesh = new Mesh { name = "ComfortVignetteRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

    }
}

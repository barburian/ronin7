using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// R12: two red eyes that blink on an enemy while it winds up / swings a strike the player must DODGE
    /// (unparryable melee or ground slam). One shared 2-quad mesh + one shared unlit material = 1 draw per
    /// lit enemy; the child object is created lazily on the first dodge strike and only toggled afterwards.
    /// </summary>
    public sealed class DodgeEyes
    {
        public const float BlinkHz = 6f;
        /// <summary>Body tint / rim base for a dodge strike: pure red, distinct from the orange parry telegraph.</summary>
        public static readonly Color DodgeColor = new Color(1f, 0.05f, 0.05f);

        /// <summary>Pure: this strike must be dodged, not parried (unparryable melee, ground slam).</summary>
        public static bool IsDodge(Strike s) => s.unparryable || s.aoeRadius > 0f;

        /// <summary>Pure: eyes are on only while winding up / swinging a dodge strike (so off in Stagger, Dead, Idle).</summary>
        public static bool ShouldShow(bool windupOrActive, Strike s) => windupOrActive && IsDodge(s);

        /// <summary>Pure: 6 Hz square blink from strike start; steady when Reduce Flashes is on.</summary>
        public static bool BlinkOn(float elapsed, bool reduceFlashes)
            => reduceFlashes || elapsed * BlinkHz % 1f < 0.5f;

        /// <summary>Pure: the colour a strike's windup tints toward (dodge red vs the parry telegraph).</summary>
        public static Color TintFor(bool dodge, Color telegraph) => dodge ? DodgeColor : telegraph;

        /// <summary>Pure: root-local eye anchor and pair-scale from the body's root-local bounds (+Z = front):
        /// upper front of the body; the pair mesh is 1 m wide so the scale is the eye gap.</summary>
        public static void Layout(Vector3 center, Vector3 size, out Vector3 pos, out float gap)
        {
            pos = new Vector3(center.x, center.y + size.y * 0.5f - size.y * 0.12f, center.z + Mathf.Min(size.x, size.z) * 0.5f);
            gap = Mathf.Clamp(size.x * 0.3f, 0.2f, 0.45f);
        }

        private static Mesh mesh;
        private static Material mat;

        private static Mesh PairMesh()
        {
            if (mesh != null) return mesh;
            // Two 0.5 x 0.25 slits centred at x = +-0.5, facing +Z.
            var v = new Vector3[8];
            for (int e = 0; e < 2; e++)
            {
                float cx = e == 0 ? -0.5f : 0.5f;
                v[e * 4] = new Vector3(cx - 0.25f, -0.125f); v[e * 4 + 1] = new Vector3(cx - 0.25f, 0.125f);
                v[e * 4 + 2] = new Vector3(cx + 0.25f, 0.125f); v[e * 4 + 3] = new Vector3(cx + 0.25f, -0.125f);
            }
            mesh = new Mesh { name = "Ronin7_DodgeEyes", hideFlags = HideFlags.DontSave, vertices = v,
                triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 } };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material EyeMaterial()
        {
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            mat = new Material(shader) { name = "Ronin7_DodgeEyes", hideFlags = HideFlags.DontSave };
            var hdr = DodgeColor * 6f; // HDR so Bloom flares it at range
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
            return mat;
        }

        private readonly MeshRenderer renderer;
        private bool shown;

        private DodgeEyes(MeshRenderer r) { renderer = r; }

        /// <summary>Build the eye pair under <paramref name="parent"/> (the transform that moves with the head), placed
        /// once from <paramref name="body"/>'s bounds relative to <paramref name="root"/>. Starts hidden.</summary>
        public static DodgeEyes Create(Transform root, Transform parent, Renderer body)
        {
            var go = new GameObject("DodgeEyes") { hideFlags = HideFlags.DontSave };
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = PairMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = EyeMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Vector3 pos = new Vector3(0f, 1.6f, 0.2f); float gap = 0.25f;
            if (body != null)
            {
                var b = body.bounds;
                Vector3 s = root.lossyScale;
                Vector3 size = new Vector3(b.size.x / Mathf.Max(s.x, 1e-3f), b.size.y / Mathf.Max(s.y, 1e-3f), b.size.z / Mathf.Max(s.z, 1e-3f));
                Layout(root.InverseTransformPoint(b.center), size, out pos, out gap);
            }
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * gap;
            if (parent != root) go.transform.SetParent(parent, true);
            mr.enabled = false;
            return new DodgeEyes(mr);
        }

        public void Set(bool on)
        {
            if (on == shown) return;
            shown = on;
            renderer.enabled = on;
        }
    }
}

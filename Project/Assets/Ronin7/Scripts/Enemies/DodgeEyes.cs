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
            gap = Mathf.Clamp(size.x * 0.12f, 0.16f, 0.2f);
        }

        private static Mesh mesh;
        private static Material mat;

        /// <summary>Unity front faces are clockwise seen from the front; these face +Z (cross product +Z).</summary>
        public static readonly int[] Triangles = { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };

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
                triangles = Triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Resources path of the shared HDR-red unlit eye material (built by EnemyWeaponBuilder.BuildEyeMaterial).</summary>
        public const string MaterialResource = "DodgeEyes";

        private static Material EyeMaterial()
        {
            if (mat != null) return mat;
            // Real asset (Resources) so the shader is referenced and ships in a Quest build; Shader.Find can be stripped.
            mat = Resources.Load<Material>(MaterialResource); // HDR colour (DodgeColor x 6) is baked into the asset
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
                if (body is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    // Skinned world bounds are inflated by animation/arm spread and by the enemy's yaw, which put the eyes well
                    // above the head. The bind-pose mesh bounds measured in root space are tight and yaw-independent.
                    Bounds mb = smr.sharedMesh.bounds;
                    Matrix4x4 toRoot = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                    Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = toRoot.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1)));
                        lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c);
                    }
                    Layout((lo + hi) * 0.5f, hi - lo, out pos, out gap);
                }
                else
                {
                    var b = body.bounds;
                    Vector3 s = root.lossyScale;
                    Vector3 size = new Vector3(b.size.x / Mathf.Max(s.x, 1e-3f), b.size.y / Mathf.Max(s.y, 1e-3f), b.size.z / Mathf.Max(s.z, 1e-3f));
                    Layout(root.InverseTransformPoint(b.center), size, out pos, out gap);
                }
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

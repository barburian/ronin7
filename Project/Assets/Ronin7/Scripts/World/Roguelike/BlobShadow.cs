using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.World
{
    /// <summary>P5-05: Quest-tier contact shadow. Quest renders no realtime shadows (Mobile_RPAsset shadow distance 0),
    /// so each activated enemy gets a soft dark quad on the floor: one shared instanced material
    /// (Resources/Vfx/BlobShadow), 1 draw, follows the enemy's XZ, sized from its collider footprint.
    /// Hidden on the PC tier, which keeps its mixed realtime directional shadows.</summary>
    public class BlobShadow : MonoBehaviour
    {
        internal const float Lift = 0.02f, Pad = 1.3f, MinDiameter = 0.4f, MaxDiameter = 4f;

        private static Material material;
        private static Mesh quad;
        private Transform blob, arena;
        private Renderer blobRenderer;

        /// <summary>Pure: blobs only where realtime shadows are off (the Low / Quest tier).</summary>
        internal static bool Wants(GraphicsQuality tier) => tier == GraphicsQuality.Low;

        /// <summary>Pure: blob diameter from the enemy's world bounds size (XZ footprint, padded, clamped).</summary>
        internal static float Diameter(Vector3 boundsSize) =>
            Mathf.Clamp(Mathf.Max(boundsSize.x, boundsSize.z) * Pad, MinDiameter, MaxDiameter);

        /// <summary>Adds a blob to an activated enemy; parented to <paramref name="arenaRoot"/> so the enemy's
        /// rotation/scale never tilts it. The floor is the arena root's y (shell floor top = arena y).</summary>
        internal static void Attach(Health health, Transform arenaRoot)
        {
            if (material == null) material = Resources.Load<Material>("Vfx/BlobShadow");
            if (material == null) return;
            if (quad == null) quad = FlatQuad();

            var c = health.gameObject.AddComponent<BlobShadow>();
            c.arena = arenaRoot;
            var go = new GameObject("BlobShadow");
            c.blob = go.transform;
            c.blob.SetParent(arenaRoot, false);
            float d = Diameter(Footprint(health.gameObject));
            c.blob.localScale = new Vector3(d, 1f, d);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            c.blobRenderer = r;
            c.LateUpdate();
        }

        private static Vector3 Footprint(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Collider col in go.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                if (any) b.Encapsulate(col.bounds); else { b = col.bounds; any = true; }
            }
            return any ? b.size : Vector3.one;
        }

        // Unit quad in XZ facing +Y (no per-instance rotation needed), UVs 0..1 for the radial texture.
        private static Mesh FlatQuad()
        {
            var m = new Mesh { name = "BlobShadowQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white }; // URP Particles shaders multiply by vertex colour
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return m;
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            blob.position = new Vector3(p.x, arena.position.y + Lift, p.z);
            blobRenderer.enabled = Wants(GraphicsRuntime.Quality); // tier can change live (settings menu / perf probe)
        }

        private void OnEnable() { if (blob != null) blob.gameObject.SetActive(true); }
        private void OnDisable() { if (blob != null) blob.gameObject.SetActive(false); }

        private void OnDestroy()
        {
            if (blob == null) return;
            if (Application.isPlaying) Destroy(blob.gameObject); else DestroyImmediate(blob.gameObject);
        }
    }
}

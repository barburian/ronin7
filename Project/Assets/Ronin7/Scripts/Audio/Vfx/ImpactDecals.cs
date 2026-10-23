using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// P5-03 impact decals: a ring of <see cref="Capacity"/> pooled quads (the VfxPool / ProjectilePool pattern:
    /// the oldest is recycled), not the URP Decal feature. Each is an additive streak that fades out; colour and
    /// alpha ride a MaterialPropertyBlock on the shared effect material, so there is no new Material.
    /// A plain class so the cap is unit-testable; the owner calls <see cref="Tick"/> while <see cref="Live"/> is above 0.
    /// </summary>
    public class ImpactDecals
    {
        public const int Capacity = 16;
        public const float Life = 4f;
        public const float FadeTime = 1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Hot = new Color(2.2f, 1.1f, 0.35f, 1f);

        private readonly Transform parent;
        private readonly Material material;
        private readonly Renderer[] quads = new Renderer[Capacity];
        private readonly float[] bornAt = new float[Capacity];
        private readonly bool[] live = new bool[Capacity];
        private int next;
        private MaterialPropertyBlock mpb;

        public ImpactDecals(Transform parent, Material material) { this.parent = parent; this.material = material; }

        /// <summary>Quads currently showing; never exceeds <see cref="Capacity"/>.</summary>
        public int Live { get; private set; }

        public void Place(Vector3 point, Vector3 normal, float now)
        {
            if (material == null || normal.sqrMagnitude < 1e-6f) return;
            int i = next;
            next = (next + 1) % Capacity;
            if (quads[i] == null) quads[i] = Create(i);
            if (!live[i]) Live++;
            live[i] = true;
            bornAt[i] = now;
            normal.Normalize();
            // The quad's visible face is -Z, so look along -normal. Roll comes from the point (stable, not random).
            quads[i].transform.SetPositionAndRotation(point + normal * 0.004f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, (point.x * 97f + point.z * 53f) % 360f));
            quads[i].gameObject.SetActive(true);
            Paint(quads[i], 1f);
        }

        /// <summary>Fades and retires decals. Returns whether any are still live.</summary>
        public bool Tick(float now)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!live[i]) continue;
                float age = now - bornAt[i];
                if (age >= Life) { live[i] = false; Live--; quads[i].gameObject.SetActive(false); continue; }
                if (age > Life - FadeTime) Paint(quads[i], (Life - age) / FadeTime);
            }
            return Live > 0;
        }

        private Renderer Create(int i)
        {
            var go = new GameObject($"ImpactDecal_{i:00}");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.28f, 0.07f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void Paint(Renderer r, float k)
        {
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, new Color(Hot.r * k, Hot.g * k, Hot.b * k, k));
            r.SetPropertyBlock(mpb);
        }
    }
}

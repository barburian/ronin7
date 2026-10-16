using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>P3-08: non-boss enemy health bar. Two shared-unlit quads (no Canvas, no new Material, colour via
    /// MPB) shown on the first hit, hidden <see cref="HideDelay"/> s after the last. Yaw-only billboard
    /// <see cref="Lift"/> m above the art top (cached once at creation). Event-driven: the component is disabled
    /// (zero per-frame cost) except while its bar is visible.</summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        internal const float HideDelay = 4f, Lift = 0.25f, Width = 0.5f, Height = 0.05f;

        private Health health;
        private Transform bar, fill, cam;
        private float topOffset, lastHit = float.NegativeInfinity;

        /// <summary>Pure: bar is visible while less than <see cref="HideDelay"/> has passed since the last hit.</summary>
        internal static bool Visible(float now, float lastHit) => now - lastHit < HideDelay;

        /// <summary>Bosses use the north-wall bar (P3-07), never this one.</summary>
        internal static bool Wants(Health h, Health boss) => h != boss;

        /// <summary>Adds a bar to an activated (non-boss) enemy; <paramref name="arena"/> parents the bar so it is not scaled/rotated by the enemy.</summary>
        internal static void Attach(Health health, Transform arena)
        {
            var c = health.gameObject.AddComponent<EnemyHealthBar>();
            c.health = health;
            c.topOffset = ArtTop(health.gameObject) + Lift;
            c.bar = new GameObject("EnemyHealthBar").transform;
            c.bar.SetParent(arena, false);
            Quad(c.bar, new Vector3(0f, 0f, 0f), new Vector2(Width + 0.02f, Height + 0.02f), new Color(0.05f, 0.02f, 0.02f, 1f));
            c.fill = Quad(c.bar, new Vector3(0f, 0f, -0.001f), new Vector2(Width, Height), new Color(2.2f, 0.25f, 0.15f, 1f));
            c.bar.gameObject.SetActive(false);
            c.enabled = false;
            health.Damaged += c.OnDamaged;
        }

        // Top of the visible art relative to the root (drones sit at root y=0 with art lifted).
        private static float ArtTop(GameObject go)
        {
            float top = float.NegativeInfinity;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            if (float.IsNegativeInfinity(top))
                foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) top = Mathf.Max(top, col.bounds.max.y);
            return float.IsNegativeInfinity(top) ? 1.8f : top - go.transform.position.y;
        }

        private static Transform Quad(Transform parent, Vector3 local, Vector2 size, Color c)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad); // faces -Z
            go.name = "Quad";
            Discard(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = VrUiMaterials.SharedUnlit;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            RendererTint.Apply(r, c);
            return go.transform;
        }

        private static void Discard(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); } // edit-mode tests

        private void OnDamaged(DamageInfo _)
        {
            if (!health.IsAlive) { Hide(); return; }
            lastHit = Time.time;
            float f = RunArenaController.HealthBarFill(health.Current, health.Max);
            fill.localScale = new Vector3(Width * f, Height, 1f);
            fill.localPosition = new Vector3((f - 1f) * 0.5f * Width, 0f, -0.001f); // drains toward the left
            bar.gameObject.SetActive(true);
            enabled = true;
            LateUpdate();
        }

        private void Hide()
        {
            if (bar != null) bar.gameObject.SetActive(false);
            enabled = false;
        }

        private void LateUpdate()
        {
            if (!Visible(Time.time, lastHit)) { Hide(); return; }
            if (cam == null) { Camera m = Camera.main; if (m == null) return; cam = m.transform; }
            Vector3 p = transform.position + Vector3.up * topOffset;
            bar.position = p;
            Vector3 away = p - cam.position; away.y = 0f; // quad faces -Z, so look away from the viewer; yaw only
            if (away.sqrMagnitude > 1e-6f) bar.rotation = Quaternion.LookRotation(away);
        }

        private void OnDestroy()
        {
            if (bar != null) Discard(bar.gameObject);
        }
    }
}

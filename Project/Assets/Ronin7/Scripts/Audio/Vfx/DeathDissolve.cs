using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// P5-03 death dissolve. Added to a dying enemy by <see cref="CombatVfxController"/>: after the topple
    /// it swaps the body renderers to the saved <c>EnemyPBR_Dissolve.mat</c> (so only dying enemies pay
    /// the alpha-clip cost), copies the original albedo/tint into a MaterialPropertyBlock (no material
    /// clone, SRP batcher untouched for everyone else), drives <c>_Dissolve</c> 0 -> 1 over
    /// <see cref="Duration"/>, then hides the renderers. Pure time curve is <see cref="Progress"/>.
    /// </summary>
    public class DeathDissolve : MonoBehaviour
    {
        public const float Delay = 0.35f;    // let the topple finish
        public const float Duration = 0.6f;

        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock mpb;

        private readonly List<Renderer> renderers = new();
        private readonly List<Material[]> originals = new(); // reverse mode only: put the enemy's own materials back
        private readonly List<UnityEngine.Rendering.ShadowCastingMode> shadowModes = new();
        private bool reverse;
        private float t;

        /// <summary>0..1 dissolve amount after <paramref name="elapsed"/> seconds since death.</summary>
        public static float Progress(float elapsed) => Mathf.Clamp01((elapsed - Delay) / Duration);

        /// <summary>P5-03 spawn: the same shader run backwards (<c>_Dissolve</c> 1 -> 0 over <see cref="Duration"/>),
        /// then the enemy's own materials are restored.</summary>
        public static void BeginReverse(GameObject enemy, Material dissolve) => Begin(enemy, dissolve, true);

        public static void Begin(GameObject enemy, Material dissolve, bool reverse = false)
        {
            if (enemy == null || dissolve == null || enemy.GetComponent<DeathDissolve>() != null) return;
            var d = enemy.AddComponent<DeathDissolve>();
            d.reverse = reverse;
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
                var src = r.sharedMaterial;
                r.GetPropertyBlock(mpb); // keeps the death tint if one was applied
                if (src != null)
                {
                    if (src.HasProperty(BaseMapId)) mpb.SetTexture(BaseMapId, src.GetTexture(BaseMapId));
                    if (src.HasProperty(BaseColorId) && !mpb.HasColor(BaseColorId)) mpb.SetColor(BaseColorId, src.GetColor(BaseColorId));
                }
                mpb.SetFloat(DissolveId, reverse ? 1f : 0f);
                r.SetPropertyBlock(mpb);
                var slots = r.sharedMaterials;
                if (reverse) { d.originals.Add((Material[])slots.Clone()); d.shadowModes.Add(r.shadowCastingMode); }
                for (int i = 0; i < slots.Length; i++) slots[i] = dissolve;
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.renderers.Add(r);
            }
            if (d.renderers.Count == 0) Destroy(d);
        }

        private void Update()
        {
            t += reverse ? Time.unscaledDeltaTime : Time.deltaTime; // spawn fade ignores slow-mo so enemies are never left half-clipped
            float p = reverse ? 1f - Mathf.Clamp01(t / Duration) : Progress(t);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(DissolveId, p);
                r.SetPropertyBlock(mpb);
            }
            if (reverse && p <= 0f)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].sharedMaterials = originals[i];
                    renderers[i].shadowCastingMode = shadowModes[i];
                }
                Destroy(this);
            }
            else if (!reverse && p >= 1f)
            {
                foreach (var r in renderers) if (r != null) r.enabled = false;
                Destroy(this);
            }
        }
    }
}

using Ronin7.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Plasma-katana authoring: turns a built Blade child into a glowing energy blade — HDR-emissive
    /// material + a neon motion trail at the tip + a tier-gated point light. Baked into the prefab at
    /// build time so it rebuilds headlessly with the rest of the art. Gameplay-critical bits (the Blade
    /// trigger BoxCollider + BladeDamager, and the BladeTip marker Enemy.cs samples) are never touched.
    /// </summary>
    public static partial class ArtPrefabBuilder
    {
        /// <summary>Energy colours per wielder (LDR hue; emission strength applied inside).</summary>
        public static readonly Color PlasmaPlayer = new Color(0.15f, 0.85f, 1f);  // cyan
        public static readonly Color PlasmaEnemy = new Color(1f, 0.2f, 0.55f);    // magenta — Dominion

        /// <summary>
        /// Make <paramref name="blade"/> read as a plasma weapon: swap to an HDR-emissive material in
        /// <paramref name="energy"/>, ensure a tip anchor, and add an additive neon trail + a tier-gated
        /// light there. Idempotent — safe to call on a freshly built blade in either build site.
        /// </summary>
        internal static void ApplyPlasmaBlade(GameObject blade, Color energy)
        {
            if (blade == null) return;

            var renderer = blade.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = EnsurePlasmaBladeMaterial(energy);

            // Tip anchor: reuse the existing BladeTip (held-rig blades have one), else create it at the
            // +Z face of the unit-cube blade — same convention as AttachHeldKatanaRig.
            var tip = blade.transform.Find("BladeTip");
            if (tip == null)
            {
                var tipGo = new GameObject("BladeTip");
                tipGo.transform.SetParent(blade.transform, false);
                tipGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                tip = tipGo.transform;
            }

            AddBladeTrail(tip, energy);
            AddBladeLight(tip, energy);
        }

        private static void AddBladeTrail(Transform tip, Color energy)
        {
            if (tip.GetComponent<TrailRenderer>() != null) return; // idempotent

            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;                 // short neon streak; reads great under the deflect slow-mo
            trail.startWidth = 0.05f;
            trail.endWidth = 0f;                // tapered to nothing
            trail.numCapVertices = 2;
            trail.minVertexDistance = 0.01f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = EnsurePlasmaTrailMaterial();
            // HDR energy along the trail so Bloom turns it to glow; fade alpha to the tail.
            var hdr = energy * 2.5f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(hdr, 0f), new GradientColorKey(hdr, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
            };
        }

        private static void AddBladeLight(Transform tip, Color energy)
        {
            // The light always exists in the prefab; TierGatedLight disables it on the Low (Quest) tier.
            var lightGo = tip.Find("BladeLight");
            if (lightGo != null) return; // idempotent

            var go = new GameObject("BladeLight");
            go.transform.SetParent(tip, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = energy;
            light.intensity = 1.4f;
            light.range = 1.5f;
            light.shadows = LightShadows.None;
            go.AddComponent<TierGatedLight>();
        }

        private const string PlasmaBladeMatPrefix = MatFolder + "/SamuraiToon_PlasmaBlade";
        private const string PlasmaTrailMatPath = MatFolder + "/PlasmaTrail_Additive.mat";

        private static Material EnsurePlasmaBladeMaterial(Color energy)
        {
            string path = $"{PlasmaBladeMatPrefix}_{ColorTag(energy)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var m = new Material(Shader.Find("Ronin7/SamuraiToon"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.7f, 0.85f, 0.95f));
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", energy * 2.5f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 1.2f);
            if (m.HasProperty("_RimColor")) m.SetColor("_RimColor", energy);
            EnsureAssetFolder(MatFolder);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material EnsurePlasmaTrailMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PlasmaTrailMatPath);
            if (existing != null) return existing;

            // Additive, unlit, vertex-coloured — so the per-trail HDR gradient routes through Bloom.
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);      // transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);          // additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EnsureAssetFolder(MatFolder);
            AssetDatabase.CreateAsset(m, PlasmaTrailMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(PlasmaTrailMatPath);
        }
    }
}

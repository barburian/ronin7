using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Shared factory for the unlit, transparent, always-on-top overlay material used by the
    /// screen fader and the comfort vignette. Both overlays need the same shader setup — pulling
    /// the factory here keeps them in sync.
    /// </summary>
    public static class VrUiMaterials
    {
        /// <summary>Unlit, transparent, always-on-top material so nothing pokes through the overlay.</summary>
        public static Material CreateTransparentUnlit()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent (URP)
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // alpha blend
            // Setting _Surface alone doesn't reconfigure blend state at runtime (that's done by the
            // shader's editor GUI), so the material would stay opaque. Set the blend modes explicitly.
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay; // draw last
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // A fresh URP/Unlit material's _BaseColor defaults to white. Callers override it via a
            // MaterialPropertyBlock, but default to fully transparent so it never flashes opaque white
            // before/without that override.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            return mat;
        }
    }
}

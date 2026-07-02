using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Per-renderer colour tint via a shared <see cref="MaterialPropertyBlock"/>.
    ///
    /// Why this exists: assigning to <c>Renderer.material</c> (or reading <c>.material.color</c>)
    /// clones the shared material on first access. That defeats the SRP batcher and, on Quest,
    /// leaks a material instance per renderer for the lifetime of the GameObject. Routing the
    /// colour through an MPB keeps everyone on the original shared material while still letting
    /// each renderer show its own tint.
    ///
    /// Both <c>_BaseColor</c> (URP) and <c>_Color</c> (legacy / Unlit) are set so the helper
    /// works regardless of which shader the renderer happens to use.
    /// </summary>
    public static class RendererTint
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _mpb;

        /// <summary>Tint <paramref name="r"/> to <paramref name="c"/> without cloning its material.</summary>
        public static void Apply(Renderer r, Color c)
        {
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}

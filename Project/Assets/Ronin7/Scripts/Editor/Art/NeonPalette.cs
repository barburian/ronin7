using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Central techno-noir colour constants for the build pipeline, so neon hues and the cool-dark
    /// base tints live in one place instead of scattered inline literals. Editor-only: every consumer
    /// (plasma blade, VFX prefab builder, enemy/material re-skins) bakes these into assets at build
    /// time, so nothing references it at runtime.
    ///
    /// HDR accents are returned pre-multiplied by their emission strength — assign straight into a
    /// SamuraiToon material's <c>_EmissionColor</c> and the volume Bloom turns them into glow.
    /// </summary>
    public static class NeonPalette
    {
        // --- Neon accents (HDR, pre-scaled for emission). ---
        public static readonly Color Cyan = new Color(0.15f, 0.85f, 1f) * 2.5f;     // player plasma / allies / holograms
        public static readonly Color CyanDim = new Color(0.15f, 0.85f, 1f) * 1.0f;  // ally/civilian NPC trim (subtler than hero Cyan)
        public static readonly Color Magenta = new Color(1f, 0.2f, 0.7f) * 2.5f;    // Dominion accents
        public static readonly Color Amber = new Color(1f, 0.55f, 0.1f) * 2.5f;     // warning / enemy trim
        public static readonly Color Violet = new Color(0.6f, 0.3f, 1f) * 2.5f;     // secondary accent

        // --- Cool-dark base tints (LDR) — bodies/hulls drop into shadow so emissives pop. ---
        public static readonly Color BaseDark = new Color(0.07f, 0.08f, 0.10f);     // generic hull/body
        public static readonly Color BaseSteel = new Color(0.10f, 0.12f, 0.16f);    // lighter structural

        /// <summary>An accent at a custom emission strength (the constants above are at 2.5×).</summary>
        public static Color Accent(Color ldrHue, float strength) => ldrHue * strength;
    }
}

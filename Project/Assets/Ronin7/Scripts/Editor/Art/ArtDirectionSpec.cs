using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Editor-only single source of truth for three visual axes:
    /// (1) scale grammar (canonical size per object class, in meters — 1 Unity unit = 1 meter),
    /// (2) emission-strength quantization (dim vs. hero),
    /// (3) the canonical post-FX grade values + acceptable envelope bounds.
    ///
    /// Palette colors are centralized in NeonPalette — this spec references them, not re-declares.
    /// Nothing at runtime references this class.
    /// </summary>
    public static class ArtDirectionSpec
    {
        // ===== SCALE GRAMMAR (meters) =====
        public const float CharacterHeight = 1.8f;   // humanoid total height
        public const float HandSize        = 0.18f;  // VR hand bounding size
        public const float BladeLength     = 0.95f;  // katana blade length
        public const float ShipHullLength  = 9f;     // enemy/player ship hull length
        public const float BoltRadius      = 0.15f;  // projectile bolt radius
        public const float CockpitReach    = 2f;     // cockpit interior reach radius
        public const float ScaleTolerance  = 0.10f;  // ±10% counts as conforming

        // ===== ASTEROID TIERS =====
        public enum AsteroidTier
        {
            Small,
            Medium,
            Large
        }

        public static float AsteroidDiameter(AsteroidTier tier)
        {
            switch (tier)
            {
                case AsteroidTier.Small:
                    return 2f;
                case AsteroidTier.Medium:
                    return 5f;
                case AsteroidTier.Large:
                    return 12f;
                default:
                    return 5f;
            }
        }

        // ===== EMISSION QUANTIZATION =====
        public const float EmissionDim  = 1.0f;   // civilian/secondary
        public const float EmissionHero = 2.5f;   // hero/primary

        public static float QuantizeEmission(float strength)
        {
            float diffToDim = Mathf.Abs(strength - EmissionDim);
            float diffToHero = Mathf.Abs(strength - EmissionHero);
            return diffToDim < diffToHero ? EmissionDim : EmissionHero;
        }

        // ===== CANONICAL ACCENT & BASE TINTS =====
        public static readonly Color[] CanonicalAccents = { NeonPalette.Cyan, NeonPalette.CyanDim, NeonPalette.Magenta, NeonPalette.Amber, NeonPalette.Violet };
        public static readonly Color[] CanonicalBaseTints = { NeonPalette.BaseDark, NeonPalette.BaseSteel };

        // ===== POST-FX CANONICAL GRADE =====
        public const float BloomIntensity = 0.9f;
        public const float BloomThreshold = 0.9f;
        public const float BloomScatter   = 0.7f;
        public const float Contrast       = 18f;
        public const float Saturation     = 14f;
        public const float Vignette       = 0.2f;
        public static readonly Color CoolFilter     = new Color(0.86f, 0.93f, 1f);
        public static readonly Color SplitShadows    = new Color(0.00f, 0.30f, 0.38f);
        public static readonly Color SplitHighlights = new Color(0.55f, 0.42f, 0.32f);
        public const float SplitBalance = 0f;

        // ===== POST-FX ENVELOPE BOUNDS =====
        public const float ContrastMin = 12f, ContrastMax = 24f;
        public const float SaturationMin = 8f, SaturationMax = 20f;
        public const float VignetteMin = 0.12f, VignetteMax = 0.30f;
    }
}

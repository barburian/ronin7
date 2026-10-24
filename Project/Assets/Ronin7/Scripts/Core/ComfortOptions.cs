using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P6-02: pure math behind the haptic / reduce-flashes / slow-mo / vignette options.
    /// Every default input (1, false, 0.25, 1) returns today's behaviour unchanged.</summary>
    public static class ComfortOptions
    {
        public const float SlowMoFloorMin = 0.25f;
        public const float SlowMoFloorMax = 0.5f;

        /// <summary>Highest HDR channel an enemy rim may reach with Reduce Flashes on. Matches the elite
        /// rim (EliteAffixes.RimIntensity, BUG-46), so telegraphs and elites stay readable but never bloom.</summary>
        public const float ReducedRimMax = 1.2f;

        public static float HapticAmplitude(float amplitude, float intensity) => Mathf.Clamp01(amplitude * Mathf.Clamp01(intensity));

        /// <summary>False when the scaled pulse is silent, so the caller skips the device call.</summary>
        public static bool HapticAudible(float amplitude) => amplitude > 0f;

        public static float ClampSlowMoFloor(float f) => Mathf.Clamp(f, SlowMoFloorMin, SlowMoFloorMax);

        /// <summary>An effect's own slow-mo scale, lifted to the player's floor (0.25 = as designed, 0.5 = gentlest).</summary>
        public static float SlowMoScale(float baseScale, float floor) => Mathf.Max(baseScale, ClampSlowMoFloor(floor));

        public static float FlashAmount(float amount, bool reduceFlashes) => reduceFlashes ? 0f : amount;

        /// <summary>Steady rim: scales the colour down (hue kept) so no channel exceeds <see cref="ReducedRimMax"/>.</summary>
        public static Color Rim(Color rim, bool reduceFlashes)
        {
            if (!reduceFlashes) return rim;
            float m = Mathf.Max(rim.r, Mathf.Max(rim.g, rim.b));
            if (m <= ReducedRimMax) return rim;
            float k = ReducedRimMax / m;
            return new Color(rim.r * k, rim.g * k, rim.b * k, rim.a);
        }

        public static float VignetteIntensity(float intensity, float strength) => Mathf.Clamp01(intensity) * Mathf.Clamp01(strength);
    }
}

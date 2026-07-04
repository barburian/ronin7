namespace Ronin7.Core
{
    /// <summary>
    /// Quest-tier light-budget gate for per-frame ambient light animation (pulses, flicker, etc.).
    /// Decorative lights are frozen at a static intensity on <see cref="GraphicsQuality.Low"/> to
    /// cut the per-frame cost; gameplay-signal lights (they carry information, not ambience) always
    /// keep animating regardless of tier.
    /// </summary>
    public static class LightBudget
    {
        /// <summary>
        /// True when a per-frame light animation should keep running; false when it should freeze at
        /// a static intensity. Gameplay-signal lights always animate.
        /// </summary>
        public static bool ShouldAnimate(GraphicsQuality tier, bool isGameplaySignal) =>
            isGameplaySignal || tier == GraphicsQuality.High;
    }
}

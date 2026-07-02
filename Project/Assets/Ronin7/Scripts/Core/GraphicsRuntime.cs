namespace Ronin7.Core
{
    /// <summary>
    /// Process-wide snapshot of the resolved graphics tier, read by systems that must not take an
    /// assembly dependency on <c>Ronin7.Flow</c> (where <c>GraphicsDirector</c> lives). The
    /// director is the sole writer — it pushes these whenever settings change — and combat VFX is the
    /// main reader. Keeping this in Core (engine-free, referenced by everyone) avoids a
    /// Combat → Flow → Combat assembly cycle.
    ///
    /// Static, not a singleton, because the values are a simple read-mostly snapshot with no lifetime
    /// of their own; the defaults below are the safe "High" values so anything that reads before the
    /// director runs still behaves sensibly.
    /// </summary>
    public static class GraphicsRuntime
    {
        /// <summary>Current resolved tier.</summary>
        public static GraphicsQuality Quality = GraphicsQuality.High;

        /// <summary>Multiplier applied to combat particle burst counts (1.0 High, lower on Low).</summary>
        public static float ParticleScale = 1f;

        /// <summary>Whether per-blade point lights should be enabled (High only — Quest light budget is tight).</summary>
        public static bool BladeLightsEnabled = true;

        /// <summary>Whether organic hits spray red splatter (true) or fall back to neon sparks only (false).</summary>
        public static bool CombatBlood = true;
    }
}

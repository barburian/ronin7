namespace Ronin7.Core
{
    /// <summary>
    /// Graphics fidelity tier. Gates the heavy, platform-sensitive knobs (full-screen bloom strength,
    /// combat particle density, blade point-lights). Quest defaults to <see cref="Low"/>, PCVR to
    /// <see cref="High"/> — the per-platform default is resolved by the settings service (this Core
    /// type stays engine-free), and the player can override it in the settings panel.
    /// </summary>
    public enum GraphicsQuality { Low, High }

    /// <summary>
    /// Plain, serializable player-comfort/audio settings model. Lives in Core (no engine
    /// dependency) so the settings service, the worldspace UI, and any system that wants to
    /// react can share one type without an assembly dependency on each other — the
    /// <see cref="EventBus"/> (via <see cref="SettingsChanged"/>) is the only coupling.
    ///
    /// Mutable struct used as a small value bag; the service owns the single authoritative copy.
    /// </summary>
    public struct GameSettings
    {
        public bool SnapTurn;        // true = snap/step turn, false = continuous turn (on-foot AND flight yaw)
        public float SnapTurnDegrees;
        public bool ComfortVignette; // tunnelling vignette on/off (on-foot AND flight)

        public float SfxVolume;      // 0..1
        public float MusicVolume;    // 0..1
        public float AmbienceVolume; // 0..1

        public float EyeHeightOffset; // metres added to the rig's Camera Offset Y (eye-height calibration)

        public GraphicsQuality Quality; // Low (Quest) / High (PCVR) — gates bloom + particles + blade lights
        public bool CombatBlood;        // true = red splatter on organic hits; false = neon sparks only

        /// <summary>Comfortable first-time-player defaults (mirror the existing serialized defaults).</summary>
        public static GameSettings Default => new GameSettings
        {
            SnapTurn = false,
            SnapTurnDegrees = 45f,
            ComfortVignette = true,
            SfxVolume = 1f,
            MusicVolume = 0.45f,
            AmbienceVolume = 0.5f,
            EyeHeightOffset = 0f,
            Quality = GraphicsQuality.High, // platform default resolved in SettingsService.Load()
            CombatBlood = true,
        };
    }

    /// <summary>
    /// Published when the player recalibrates their eye height. Carries the new Camera Offset Y
    /// so the settings service can persist it and re-apply it on every scene load, without the
    /// rig-side calibrator needing a reference to the service (avoids a Player→Audio assembly cycle).
    /// </summary>
    public readonly struct EyeHeightCalibrated
    {
        public readonly float OffsetY;
        public EyeHeightCalibrated(float offsetY) { OffsetY = offsetY; }
    }

    /// <summary>
    /// Published whenever settings change so interested systems can re-read. Carries the new
    /// snapshot so subscribers need no reference to the service.
    /// </summary>
    public readonly struct SettingsChanged
    {
        public readonly GameSettings Settings;
        public SettingsChanged(GameSettings settings) { Settings = settings; }
    }
}

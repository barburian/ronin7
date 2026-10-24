namespace Ronin7.Core
{
    /// <summary>
    /// Graphics fidelity tier. Gates the heavy, platform-sensitive knobs (full-screen bloom strength,
    /// combat particle density, blade point-lights). Quest defaults to <see cref="Low"/>, PCVR to
    /// <see cref="High"/> — the per-platform default is resolved by the settings service (this Core
    /// type stays engine-free), and the player can override it in the settings panel.
    /// </summary>
    public enum GraphicsQuality { Low, High }

    /// <summary>On-foot move style (D-10: Smooth is the default, Teleport is optional). Append-only.</summary>
    public enum LocomotionMode { Smooth, Teleport }

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

        // P2-09 comfort options (appended; zero values ARE the defaults, so older saves load unchanged).
        public bool Seated;              // raises the viewpoint by SeatedEyeOffset, relaxes the capsule posture guard
        public bool LeftHanded;          // off-hand (HUD, haptics) = right; UI ray + katana holster mirrored
        public LocomotionMode Locomotion; // Smooth (default) or Teleport on the move stick

        // P6-02 comfort options. Defaults (see Default) reproduce pre-P6 behaviour.
        public float HapticIntensity;  // 0..1 multiplier on every Haptics.Pulse
        public bool ReduceFlashes;     // no vignette flash / hit flash / bloom spike; steady capped enemy rims
        public float SlowMoFloor;      // 0.25..0.5: slowest allowed deflect / Zanshin / Overdrive time scale
        public float VignetteStrength; // 0..1 multiplier on the comfort vignette

        // P6-03 accessibility. Zero (unset) reads as the default via AccessibilityOptions.Clamp*.
        public float UiScale;            // 1..1.5 multiplier on the world-space panels
        public float DamageTaken;        // 0.5..1 multiplier on damage the player takes (assist)
        public float EnemyCooldownScale; // 1..1.5 multiplier on enemy attack cooldowns (assist)
        public bool Subtitles;           // VO subtitles band (default on)

        /// <summary>Seated-play eye lift (metres) added on top of the calibrated <see cref="EyeHeightOffset"/>.</summary>
        public const float SeatedEyeOffset = 0.45f;

        /// <summary>The Y actually written to the rig's eye-height root: calibration + seated lift.</summary>
        public float EffectiveEyeOffset => EyeHeightOffset + (Seated ? SeatedEyeOffset : 0f);

        /// <summary>Last-applied settings snapshot, readable from any assembly (Player/Flow/World can't
        /// reference the Audio-side SettingsService). Written only by SettingsService.Apply.
        /// P2-04's exit gates read <c>GameSettings.Active.Seated</c> to fall back to the panel.</summary>
        public static GameSettings Active { get; set; } = Default;

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
            Seated = false,
            LeftHanded = false,
            Locomotion = LocomotionMode.Smooth,
            HapticIntensity = 1f,
            ReduceFlashes = false,
            SlowMoFloor = ComfortOptions.SlowMoFloorMin,
            VignetteStrength = 1f,
            UiScale = 1f,
            DamageTaken = 1f,
            EnemyCooldownScale = 1f,
            Subtitles = true,
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

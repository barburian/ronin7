using Ronin7.Core;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.Audio
{
    /// <summary>
    /// Persistent owner of the player's comfort/audio settings. Mirrors the
    /// <see cref="GameState"/>/<see cref="AudioDirector"/> singleton pattern: it loads from
    /// PlayerPrefs on Awake, applies values live, and RE-APPLIES whenever a scene loads — the rig
    /// (with its <see cref="ContinuousLocomotion"/>/<see cref="ShipController"/>) is rebuilt per
    /// scene, so the menu can't depend on those instances surviving a transition.
    ///
    /// Lives in the Audio assembly because that is the existing "integrator" module that already
    /// references Core + Ship; adding Player here is acyclic (nothing references Audio). The
    /// worldspace UI talks to this service directly; <see cref="SettingsChanged"/> is published on
    /// the EventBus for any other interested system.
    /// </summary>
    public class SettingsService : MonoBehaviour
    {
        public static SettingsService Instance { get; private set; }

        internal const string KeySnapTurn = "ss.snapTurn";
        internal const string KeySnapDeg = "ss.snapTurnDegrees";
        internal const string KeyVignette = "ss.comfortVignette";
        internal const string KeySfx = "ss.sfxVolume";
        internal const string KeyMusic = "ss.musicVolume";
        internal const string KeyAmbience = "ss.ambienceVolume";
        internal const string KeyEyeHeight = "ss.eyeHeightOffset";
        internal const string KeyQuality = "ss.graphicsQuality";
        internal const string KeyBlood = "ss.combatBlood";
        internal const string KeySeated = "ss.seated";
        internal const string KeyLeftHanded = "ss.leftHanded";
        internal const string KeyLocomotion = "ss.locomotion";
        internal const string KeyHaptic = "ss.hapticIntensity";
        internal const string KeyReduceFlashes = "ss.reduceFlashes";
        internal const string KeySlowMo = "ss.slowMoFloor";
        internal const string KeyVignetteStrength = "ss.vignetteStrength";
        internal const string KeyUiScale = "ss.uiScale";
        internal const string KeyDamageTaken = "ss.damageTaken";
        internal const string KeyEnemyCooldown = "ss.enemyCooldownScale";
        internal const string KeySubtitles = "ss.subtitles";
        internal const string KeyReplayTutorial = "ss.replayTutorial";

        private GameSettings settings;

        /// <summary>Current settings snapshot (read-only copy).</summary>
        public GameSettings Current => settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            settings = Load();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventBus.Subscribe<EyeHeightCalibrated>(OnEyeHeightCalibrated);
            EventBus.Subscribe<TutorialReplayShown>(OnTutorialReplayShown);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus.Unsubscribe<EyeHeightCalibrated>(OnEyeHeightCalibrated);
            EventBus.Unsubscribe<TutorialReplayShown>(OnTutorialReplayShown);
        }

        // P6-06: the replay is one-shot, the arena reports that the prompts were shown so the toggle clears.
        private void OnTutorialReplayShown(TutorialReplayShown e) => SetReplayTutorial(false);

        // The rig-side calibrator computes the offset and publishes it; we own persistence.
        // The calibrator measures the TOTAL offset (it strips the current one, seated lift included),
        // so store it net of the seated lift — otherwise Apply would add the 0.45 m twice.
        private void OnEyeHeightCalibrated(EyeHeightCalibrated e)
        {
            settings.EyeHeightOffset = e.OffsetY - (settings.Seated ? GameSettings.SeatedEyeOffset : 0f);
            XREyeHeightCalibrator.ApplyOffset(settings.EffectiveEyeOffset);
            Persist();
        }

        private void Start() => Apply(); // first apply once the boot scene's systems exist

        // Re-apply on every scene load: the rig and its locomotion/ship controllers are rebuilt
        // per scene, so freshly-spawned components must be told the current settings.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        // ---- Public setters used by the worldspace UI. Each persists + applies + announces. ----

        public void SetSnapTurn(bool snap)
        {
            settings.SnapTurn = snap;
            Persist(); Apply();
        }

        public void SetSnapTurnDegrees(float degrees)
        {
            settings.SnapTurnDegrees = degrees;
            Persist(); Apply();
        }

        public void SetComfortVignette(bool on)
        {
            settings.ComfortVignette = on;
            Persist(); Apply();
        }

        public void SetSfxVolume(float v)
        {
            settings.SfxVolume = Mathf.Clamp01(v);
            Persist(); Apply();
        }

        public void SetMusicVolume(float v)
        {
            settings.MusicVolume = Mathf.Clamp01(v);
            Persist(); Apply();
        }

        public void SetAmbienceVolume(float v)
        {
            settings.AmbienceVolume = Mathf.Clamp01(v);
            Persist(); Apply();
        }

        public void SetGraphicsQuality(GraphicsQuality q)
        {
            settings.Quality = q;
            Persist(); Apply();
        }

        public void SetCombatBlood(bool on)
        {
            settings.CombatBlood = on;
            Persist(); Apply();
        }

        public void SetSeated(bool on)
        {
            settings.Seated = on;
            Persist(); Apply();
        }

        public void SetLeftHanded(bool on)
        {
            settings.LeftHanded = on;
            Persist(); Apply();
        }

        public void SetLocomotion(LocomotionMode mode)
        {
            settings.Locomotion = mode;
            Persist(); Apply();
        }

        public void SetHapticIntensity(float v)
        {
            settings.HapticIntensity = Mathf.Clamp01(v);
            Persist(); Apply();
        }

        public void SetReduceFlashes(bool on)
        {
            settings.ReduceFlashes = on;
            Persist(); Apply();
        }

        public void SetSlowMoFloor(float v)
        {
            settings.SlowMoFloor = ComfortOptions.ClampSlowMoFloor(v);
            Persist(); Apply();
        }

        public void SetVignetteStrength(float v)
        {
            settings.VignetteStrength = Mathf.Clamp01(v);
            Persist(); Apply();
        }

        public void SetUiScale(float v)
        {
            settings.UiScale = AccessibilityOptions.ClampUiScale(v);
            Persist(); Apply();
        }

        public void SetDamageTaken(float v)
        {
            settings.DamageTaken = AccessibilityOptions.ClampDamageTaken(v);
            Persist(); Apply();
        }

        public void SetEnemyCooldownScale(float v)
        {
            settings.EnemyCooldownScale = AccessibilityOptions.ClampCooldownScale(v);
            Persist(); Apply();
        }

        public void SetSubtitles(bool on)
        {
            settings.Subtitles = on;
            Persist(); Apply();
        }

        public void SetReplayTutorial(bool on)
        {
            settings.ReplayTutorial = on;
            Persist(); Apply();
        }

        /// <summary>Push the current settings onto whatever live systems exist in the loaded scene(s).</summary>
        public void Apply()
        {
            // Turn type + comfort vignette apply to BOTH locomotion styles. Either may be absent
            // depending on the scene (on-foot vs seated flight), so both lookups are optional.
            var loco = ContinuousLocomotion.Instance;
            if (loco != null)
            {
                loco.SetTurnStyle(settings.SnapTurn, settings.SnapTurnDegrees);
                loco.SetComfortVignette(settings.ComfortVignette);
                loco.SetSeated(settings.Seated);
                loco.SetTeleport(settings.Locomotion == LocomotionMode.Teleport);
            }

            var ship = ShipController.Instance;
            if (ship != null)
            {
                ship.SetTurnStyle(settings.SnapTurn, settings.SnapTurnDegrees);
                ship.SetComfortVignette(settings.ComfortVignette);
            }

            // Volumes apply to the persistent audio director (may be null in test scenes).
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.SetSfxVolume(settings.SfxVolume);
                AudioDirector.Instance.SetMusicVolume(settings.MusicVolume);
                AudioDirector.Instance.SetAmbienceVolume(settings.AmbienceVolume);
            }

            // Eye-height offset (+ seated lift) re-applies to the per-scene rebuilt rig (no-op if no rig yet).
            XREyeHeightCalibrator.ApplyOffset(settings.EffectiveEyeOffset);

            // Handedness: HUD mount, UI ray, katana holster side (no-op if no rig yet).
            HandednessRig.Apply(settings.LeftHanded);

            GameSettings.Active = settings;
            EventBus.Publish(new SettingsChanged(settings));
        }

        internal static GameSettings Load()
        {
            var d = GameSettings.Default;
            // Platform default for the graphics tier: Quest=Low, PCVR=High. Resolved here (engine
            // access) rather than in the engine-free GameSettings struct.
            int qualityDefault = (int)(Application.isMobilePlatform ? GraphicsQuality.Low : GraphicsQuality.High);
            return new GameSettings
            {
                SnapTurn = PlayerPrefs.GetInt(KeySnapTurn, d.SnapTurn ? 1 : 0) != 0,
                SnapTurnDegrees = PlayerPrefs.GetFloat(KeySnapDeg, d.SnapTurnDegrees),
                ComfortVignette = PlayerPrefs.GetInt(KeyVignette, d.ComfortVignette ? 1 : 0) != 0,
                SfxVolume = PlayerPrefs.GetFloat(KeySfx, d.SfxVolume),
                MusicVolume = PlayerPrefs.GetFloat(KeyMusic, d.MusicVolume),
                AmbienceVolume = PlayerPrefs.GetFloat(KeyAmbience, d.AmbienceVolume),
                EyeHeightOffset = PlayerPrefs.GetFloat(KeyEyeHeight, d.EyeHeightOffset),
                Quality = (GraphicsQuality)PlayerPrefs.GetInt(KeyQuality, qualityDefault),
                CombatBlood = PlayerPrefs.GetInt(KeyBlood, d.CombatBlood ? 1 : 0) != 0,
                // P2-09: absent on pre-P2-09 installs → defaults (Standing, right-handed, Smooth).
                Seated = PlayerPrefs.GetInt(KeySeated, d.Seated ? 1 : 0) != 0,
                LeftHanded = PlayerPrefs.GetInt(KeyLeftHanded, d.LeftHanded ? 1 : 0) != 0,
                Locomotion = (LocomotionMode)PlayerPrefs.GetInt(KeyLocomotion, (int)d.Locomotion),
                // P6-02: absent on older installs -> pre-P6 behaviour; hand-edited values are clamped.
                HapticIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyHaptic, d.HapticIntensity)),
                ReduceFlashes = PlayerPrefs.GetInt(KeyReduceFlashes, d.ReduceFlashes ? 1 : 0) != 0,
                SlowMoFloor = ComfortOptions.ClampSlowMoFloor(PlayerPrefs.GetFloat(KeySlowMo, d.SlowMoFloor)),
                VignetteStrength = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVignetteStrength, d.VignetteStrength)),
                // P6-03: absent on older installs -> defaults; hand-edited values are clamped.
                UiScale = AccessibilityOptions.ClampUiScale(PlayerPrefs.GetFloat(KeyUiScale, d.UiScale)),
                DamageTaken = AccessibilityOptions.ClampDamageTaken(PlayerPrefs.GetFloat(KeyDamageTaken, d.DamageTaken)),
                EnemyCooldownScale = AccessibilityOptions.ClampCooldownScale(PlayerPrefs.GetFloat(KeyEnemyCooldown, d.EnemyCooldownScale)),
                Subtitles = PlayerPrefs.GetInt(KeySubtitles, d.Subtitles ? 1 : 0) != 0,
                ReplayTutorial = PlayerPrefs.GetInt(KeyReplayTutorial, 0) != 0, // P6-06
            };
        }

        private void Persist() => Persist(settings);

        internal static void Persist(GameSettings settings)
        {
            PlayerPrefs.SetInt(KeySnapTurn, settings.SnapTurn ? 1 : 0);
            PlayerPrefs.SetFloat(KeySnapDeg, settings.SnapTurnDegrees);
            PlayerPrefs.SetInt(KeyVignette, settings.ComfortVignette ? 1 : 0);
            PlayerPrefs.SetFloat(KeySfx, settings.SfxVolume);
            PlayerPrefs.SetFloat(KeyMusic, settings.MusicVolume);
            PlayerPrefs.SetFloat(KeyAmbience, settings.AmbienceVolume);
            PlayerPrefs.SetFloat(KeyEyeHeight, settings.EyeHeightOffset);
            PlayerPrefs.SetInt(KeyQuality, (int)settings.Quality);
            PlayerPrefs.SetInt(KeyBlood, settings.CombatBlood ? 1 : 0);
            PlayerPrefs.SetInt(KeySeated, settings.Seated ? 1 : 0);
            PlayerPrefs.SetInt(KeyLeftHanded, settings.LeftHanded ? 1 : 0);
            PlayerPrefs.SetInt(KeyLocomotion, (int)settings.Locomotion);
            PlayerPrefs.SetFloat(KeyHaptic, settings.HapticIntensity);
            PlayerPrefs.SetInt(KeyReduceFlashes, settings.ReduceFlashes ? 1 : 0);
            PlayerPrefs.SetFloat(KeySlowMo, settings.SlowMoFloor);
            PlayerPrefs.SetFloat(KeyVignetteStrength, settings.VignetteStrength);
            PlayerPrefs.SetFloat(KeyUiScale, settings.UiScale);
            PlayerPrefs.SetFloat(KeyDamageTaken, settings.DamageTaken);
            PlayerPrefs.SetFloat(KeyEnemyCooldown, settings.EnemyCooldownScale);
            PlayerPrefs.SetInt(KeySubtitles, settings.Subtitles ? 1 : 0);
            PlayerPrefs.SetInt(KeyReplayTutorial, settings.ReplayTutorial ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

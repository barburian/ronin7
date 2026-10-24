using Ronin7.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Audio
{
    /// <summary>
    /// Binds a worldspace settings Canvas's controls to the <see cref="SettingsService"/>. Reads
    /// the current values into the controls on enable, then forwards control changes to the
    /// service (which persists + applies them live). All control references are optional so a
    /// partially-built panel still runs; the Editor builder (Tools/Space Samurai/Build Settings
    /// Panel) wires them up in one click.
    ///
    /// Plain uGUI (Toggle/Slider/Text) — no TextMeshPro dependency, so the human needs no font/TMP
    /// import step. Listeners are added once in Awake (no per-frame work, no allocations).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Turn")]
        [SerializeField] private Toggle snapTurnToggle;
        [SerializeField] private Slider snapDegreesSlider; // 15..90
        [SerializeField] private Text snapDegreesLabel;

        [Header("Comfort")]
        [SerializeField] private Toggle vignetteToggle;

        [Header("Graphics")]
        [SerializeField] private Toggle highQualityToggle; // on = High (PCVR), off = Low (Quest)
        [SerializeField] private Toggle bloodToggle;       // on = red splatter on organic hits

        [Header("Comfort / Accessibility (P2-09)")]
        [SerializeField] private Toggle seatedToggle;     // on = seated play (+0.45 m eye lift)
        [SerializeField] private Toggle leftHandedToggle; // on = HUD on right controller, ray + holster mirrored
        [SerializeField] private Toggle teleportToggle;   // on = Teleport locomotion, off = Smooth (default)

        [Header("Haptics / Comfort options (P6-02)")]
        [SerializeField] private Slider hapticSlider;          // 0..1
        [SerializeField] private Text hapticLabel;
        [SerializeField] private Toggle reduceFlashesToggle;   // on = no flashes / bloom spikes, steady rims
        [SerializeField] private Slider slowMoSlider;          // 0.25..0.5 slowest allowed slow-mo scale
        [SerializeField] private Text slowMoLabel;
        [SerializeField] private Slider vignetteStrengthSlider; // 0..1
        [SerializeField] private Text vignetteStrengthLabel;

        [Header("Accessibility (P6-03)")]
        [SerializeField] private Slider uiScaleSlider;      // 1..1.5
        [SerializeField] private Text uiScaleLabel;
        [SerializeField] private Slider damageTakenSlider;  // 0.5..1 (assist)
        [SerializeField] private Text damageTakenLabel;
        [SerializeField] private Slider cooldownSlider;     // 1..1.5 enemy attack cooldown (assist)
        [SerializeField] private Text cooldownLabel;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle replayTutorialToggle; // P6-06: one-shot, next run shows the prompts

        [Header("Volume (0..1)")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider ambienceSlider;

        [Header("Flow")]
        [SerializeField] private Button returnToMenuButton;

        [Header("Save (wired by the builder)")]
        [SerializeField] private Button[] saveSlotButtons = new Button[3];
        [SerializeField] private Text saveStatusLabel;

        private bool initializing;

        private void Awake()
        {
            if (snapTurnToggle != null) snapTurnToggle.onValueChanged.AddListener(OnSnapTurnChanged);
            if (snapDegreesSlider != null) snapDegreesSlider.onValueChanged.AddListener(OnSnapDegreesChanged);
            if (vignetteToggle != null) vignetteToggle.onValueChanged.AddListener(OnVignetteChanged);
            if (highQualityToggle != null) highQualityToggle.onValueChanged.AddListener(OnHighQualityChanged);
            if (bloodToggle != null) bloodToggle.onValueChanged.AddListener(OnBloodChanged);
            if (seatedToggle != null) seatedToggle.onValueChanged.AddListener(OnSeatedChanged);
            if (leftHandedToggle != null) leftHandedToggle.onValueChanged.AddListener(OnLeftHandedChanged);
            if (teleportToggle != null) teleportToggle.onValueChanged.AddListener(OnTeleportChanged);
            if (hapticSlider != null) hapticSlider.onValueChanged.AddListener(OnHapticChanged);
            if (reduceFlashesToggle != null) reduceFlashesToggle.onValueChanged.AddListener(OnReduceFlashesChanged);
            if (slowMoSlider != null) slowMoSlider.onValueChanged.AddListener(OnSlowMoChanged);
            if (vignetteStrengthSlider != null) vignetteStrengthSlider.onValueChanged.AddListener(OnVignetteStrengthChanged);
            if (uiScaleSlider != null) uiScaleSlider.onValueChanged.AddListener(v => OnA11yChanged(() => SettingsService.Instance.SetUiScale(v)));
            if (damageTakenSlider != null) damageTakenSlider.onValueChanged.AddListener(v => OnA11yChanged(() => SettingsService.Instance.SetDamageTaken(v)));
            if (cooldownSlider != null) cooldownSlider.onValueChanged.AddListener(v => OnA11yChanged(() => SettingsService.Instance.SetEnemyCooldownScale(v)));
            if (subtitlesToggle != null) subtitlesToggle.onValueChanged.AddListener(v => OnA11yChanged(() => SettingsService.Instance.SetSubtitles(v)));
            if (replayTutorialToggle != null) replayTutorialToggle.onValueChanged.AddListener(v => OnA11yChanged(() => SettingsService.Instance.SetReplayTutorial(v)));
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (ambienceSlider != null) ambienceSlider.onValueChanged.AddListener(OnAmbienceChanged);
            if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(OnReturnToMenu);
        }

        private void OnEnable()
        {
            Refresh();
            RefreshSaveButtons();
            SetSaveStatus("");
            InvokeRepeating(nameof(RefreshSaveButtons), 0.5f, 0.5f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RefreshSaveButtons));
        }

        /// <summary>Pull current values from the service into the controls without re-triggering applies.</summary>
        private void Refresh()
        {
            if (SettingsService.Instance == null) return;
            var s = SettingsService.Instance.Current;

            initializing = true;
            if (snapTurnToggle != null) snapTurnToggle.isOn = s.SnapTurn;
            if (snapDegreesSlider != null) snapDegreesSlider.value = s.SnapTurnDegrees;
            if (vignetteToggle != null) vignetteToggle.isOn = s.ComfortVignette;
            if (highQualityToggle != null) highQualityToggle.isOn = s.Quality == GraphicsQuality.High;
            if (bloodToggle != null) bloodToggle.isOn = s.CombatBlood;
            if (seatedToggle != null) seatedToggle.isOn = s.Seated;
            if (leftHandedToggle != null) leftHandedToggle.isOn = s.LeftHanded;
            if (teleportToggle != null) teleportToggle.isOn = s.Locomotion == LocomotionMode.Teleport;
            if (hapticSlider != null) hapticSlider.value = s.HapticIntensity;
            if (reduceFlashesToggle != null) reduceFlashesToggle.isOn = s.ReduceFlashes;
            if (slowMoSlider != null) slowMoSlider.value = s.SlowMoFloor;
            if (vignetteStrengthSlider != null) vignetteStrengthSlider.value = s.VignetteStrength;
            if (uiScaleSlider != null) uiScaleSlider.value = AccessibilityOptions.ClampUiScale(s.UiScale);
            if (damageTakenSlider != null) damageTakenSlider.value = AccessibilityOptions.ClampDamageTaken(s.DamageTaken);
            if (cooldownSlider != null) cooldownSlider.value = AccessibilityOptions.ClampCooldownScale(s.EnemyCooldownScale);
            if (subtitlesToggle != null) subtitlesToggle.isOn = s.Subtitles;
            if (replayTutorialToggle != null) replayTutorialToggle.isOn = s.ReplayTutorial;
            if (sfxSlider != null) sfxSlider.value = s.SfxVolume;
            if (musicSlider != null) musicSlider.value = s.MusicVolume;
            if (ambienceSlider != null) ambienceSlider.value = s.AmbienceVolume;
            initializing = false;

            UpdateSnapLabel(s.SnapTurnDegrees);
            UpdateOptionLabels(s);
        }

        private void UpdateOptionLabels(GameSettings s)
        {
            if (hapticLabel != null) hapticLabel.text = $"Haptics: {Mathf.RoundToInt(s.HapticIntensity * 100f)}%";
            if (slowMoLabel != null) slowMoLabel.text = $"Slow-mo Speed: {Mathf.RoundToInt(s.SlowMoFloor * 100f)}%";
            if (vignetteStrengthLabel != null) vignetteStrengthLabel.text = $"Vignette: {Mathf.RoundToInt(s.VignetteStrength * 100f)}%";
            if (uiScaleLabel != null) uiScaleLabel.text = $"UI Scale: {AccessibilityOptions.ClampUiScale(s.UiScale):0.0}x";
            if (damageTakenLabel != null) damageTakenLabel.text = $"Damage Taken: {Mathf.RoundToInt(AccessibilityOptions.ClampDamageTaken(s.DamageTaken) * 100f)}%";
            if (cooldownLabel != null) cooldownLabel.text = $"Enemy Cooldown: x{AccessibilityOptions.ClampCooldownScale(s.EnemyCooldownScale):0.0}";
        }

        // The listeners are lambdas added once in Awake; the closure allocates per slider move, not per frame.
        private void OnA11yChanged(System.Action set)
        {
            if (initializing || SettingsService.Instance == null) return;
            set();
            UpdateOptionLabels(SettingsService.Instance.Current);
        }

        private void OnHapticChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetHapticIntensity(v);
            UpdateOptionLabels(SettingsService.Instance.Current);
            Ronin7.Player.Haptics.Pulse(UnityEngine.XR.XRNode.RightHand, 0.6f, 0.08f); // let the player feel the new level
        }

        private void OnReduceFlashesChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetReduceFlashes(v);
        }

        private void OnSlowMoChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetSlowMoFloor(v);
            UpdateOptionLabels(SettingsService.Instance.Current);
        }

        private void OnVignetteStrengthChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetVignetteStrength(v);
            UpdateOptionLabels(SettingsService.Instance.Current);
        }

        private void OnSnapTurnChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetSnapTurn(v);
        }

        private void OnSnapDegreesChanged(float v)
        {
            UpdateSnapLabel(v);
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetSnapTurnDegrees(v);
        }

        private void OnVignetteChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetComfortVignette(v);
        }

        private void OnHighQualityChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetGraphicsQuality(v ? GraphicsQuality.High : GraphicsQuality.Low);
        }

        private void OnBloodChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetCombatBlood(v);
        }

        private void OnSeatedChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetSeated(v);
        }

        private void OnLeftHandedChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetLeftHanded(v);
        }

        private void OnTeleportChanged(bool v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetLocomotion(v ? LocomotionMode.Teleport : LocomotionMode.Smooth);
        }

        private void OnSfxChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetSfxVolume(v);
        }

        private void OnMusicChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetMusicVolume(v);
        }

        private void OnAmbienceChanged(float v)
        {
            if (initializing || SettingsService.Instance == null) return;
            SettingsService.Instance.SetAmbienceVolume(v);
        }

        private void UpdateSnapLabel(float degrees)
        {
            if (snapDegreesLabel != null) snapDegreesLabel.text = $"Snap Angle: {Mathf.RoundToInt(degrees)}°";
        }

        private void OnReturnToMenu()
        {
            if (Ronin7.Flow.GameFlowManager.Instance != null)
                Ronin7.Flow.GameFlowManager.Instance.ReturnToMainMenu();
            else
                Debug.LogWarning("[SettingsPanel] Return-to-menu pressed but GameFlowManager is missing.");
        }

        /// <summary>UI bridge for the per-slot SAVE buttons (wired by the editor builder).</summary>
        public void OnSaveSlotClicked(int slot)
        {
            if (!CanSaveNow())
            {
                SetSaveStatus("Can't save in combat");
                return;
            }
            SaveSystem.Save(slot, CampaignState.ToSaveData());
            SaveSystem.MostRecentSlot = slot;
            SetSaveStatus($"Saved to slot {slot}");
        }

        /// <summary>Calm-moment gate: space with no live hostiles, or on foot with no aggroed enemies.</summary>
        private static bool CanSaveNow()
        {
            // A run is not campaign progress: CampaignState is blank there, so a slot save would
            // overwrite the player's campaign (the BUG-01 data loss through another door).
            if (RunState.InRun) return false;
            var mode = GameState.Instance != null ? GameState.Instance.Mode : GameMode.Boot;
            return SaveGate.CanSave(mode, AnyEnemyShipAlive(), CombatActivity.OnFootAggro);
        }

        private static bool AnyEnemyShipAlive()
        {
            var list = Ronin7.Ship.EnemyShip.Active;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsAlive) return true;
            }
            return false;
        }

        private void RefreshSaveButtons()
        {
            bool canSave = CanSaveNow();
            for (int i = 0; i < saveSlotButtons.Length; i++)
            {
                if (saveSlotButtons[i] != null) saveSlotButtons[i].interactable = canSave;
            }
        }

        private void SetSaveStatus(string msg)
        {
            if (saveStatusLabel != null) saveStatusLabel.text = msg;
        }
    }
}

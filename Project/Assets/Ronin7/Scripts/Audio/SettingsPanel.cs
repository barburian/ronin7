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
            if (sfxSlider != null) sfxSlider.value = s.SfxVolume;
            if (musicSlider != null) musicSlider.value = s.MusicVolume;
            if (ambienceSlider != null) ambienceSlider.value = s.AmbienceVolume;
            initializing = false;

            UpdateSnapLabel(s.SnapTurnDegrees);
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

using Ronin7.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Thin bridge between the main-menu canvas buttons and the persistent <see cref="GameFlowManager"/>.
    /// Buttons are wired to these no-arg methods via uGUI onClick events.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Eye height (metres) the Recalibrate button calibrates the player's viewpoint to.")]
        [SerializeField] private float targetEyeHeight = 1.6f;

        [Tooltip("Optional CONTINUE button — disabled when the most recent save slot is empty.")]
        [SerializeField] private Button continueButton;

        [Tooltip("ENTER THE FRACTURE button; relabelled RESUME RUN when a saved run exists.")]
        [SerializeField] private Button runButton;

        [Tooltip("ABANDON button; shown only when a saved run exists.")]
        [SerializeField] private Button abandonButton;

        [Tooltip("P2-01: shows the banked Echoes balance on the menu.")]
        [SerializeField] private Text echoesLabel;

        [Tooltip("P2-09: the boot scene's settings panel toggle (wired by the roguelike entry-point patcher).")]
        [SerializeField] private Ronin7.Player.SettingsMenuToggle settings;

        private bool abandonArmed;
        private MetaShopPanel shop;
        private bool settingsHidMenu;

        private void OnEnable()
        {
            HideCampaignEntriesIfUnavailable();
            RefreshRunButtons();
        }

        /// <summary>
        /// P0-10/D-02: the roguelike build leaves the campaign scenes out, so START NEW GAME and
        /// CONTINUE would lead nowhere. Hides every button wired to a campaign entry point — a one-time
        /// lookup on enable, not per frame.
        /// </summary>
        private void HideCampaignEntriesIfUnavailable()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null || flow.CampaignAvailable) return;
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (!IsCampaignEntry(button.onClick.GetPersistentMethodName(i))) continue;
                    button.gameObject.SetActive(false);
                    break;
                }
            }
        }

        /// <summary>Menu callbacks that enter the story campaign. Pure. P0-10 test seam.</summary>
        internal static bool IsCampaignEntry(string methodName) =>
            methodName == nameof(OnStartNewGameClicked) || methodName == nameof(OnStartCampaignClicked) ||
            methodName == nameof(OnContinueClicked) || methodName == nameof(OnLoadSlotClicked) ||
            methodName == nameof(OnGoToSpaceClicked);

        private void RefreshRunButtons()
        {
            abandonArmed = false;
            if (echoesLabel != null)
            {
                MetaProgression.EnsureLoaded();
                echoesLabel.text = MetaShopPanel.EchoesText(MetaProgression.Echoes);
            }
            SetAbandonLabel(RunEntryOptions.AbandonLabel);
            bool hasRun = RunEntryOptions.HasSavedRun();
            if (runButton != null)
            {
                var label = runButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = RunEntryOptions.PrimaryLabel(hasRun);
            }
            if (abandonButton != null) abandonButton.gameObject.SetActive(RunEntryOptions.ShowAbandon(hasRun));

            if (continueButton != null)
                continueButton.interactable = SaveSystem.SlotExists(SaveSystem.MostRecentSlot);
        }

        public void OnStartNewGameClicked()
        {
            // Galaxy / ship-select removed — a new game drops straight into Kessler's ship hub on-foot.
            GameFlowManager.Instance?.StartNewGame();
        }

        /// <summary>Boot "Start" → begin the campaign in Kessler's ship hub (on-foot).</summary>
        public void OnStartCampaignClicked()
        {
            GameFlowManager.Instance?.StartCampaign();
        }

        /// <summary>UI bridge for the per-slot LOAD GAME buttons (wired by the boot-scene builder).</summary>
        public void OnLoadSlotClicked(int slot)
        {
            GameFlowManager.Instance?.LoadGame(slot);
        }

        /// <summary>Menu "CONTINUE": resume from the most recently used save slot.</summary>
        public void OnContinueClicked()
        {
            GameFlowManager.Instance?.LoadGame(SaveSystem.MostRecentSlot);
        }

        /// <summary>
        /// Legacy in-ship "GO TO SPACE" prompt — superseded by <see cref="MissionLauncher"/>, which
        /// launches the next campaign mission from the hub. Kept only so any old button wiring still
        /// resolves; it now just restarts a fresh campaign at the hub.
        /// </summary>
        public void OnGoToSpaceClicked()
        {
            GameFlowManager.Instance?.StartNewGame();
        }

        public void OnExitGameClicked()
        {
            Debug.Log("[MainMenu] Exit clicked (no-op in Editor).");
            Application.Quit();
        }

        public void OnRecalibrateClicked()
        {
            Ronin7.Player.XRRecenterUtility.RecenterRig();
            Ronin7.Player.XREyeHeightCalibrator.Calibrate(targetEyeHeight);
        }

        /// <summary>Menu "START RUN": begin a new roguelike run (random seed).</summary>
        public void OnStartRunClicked()
        {
            RunDirector.Instance?.StartRun();
        }

        /// <summary>Menu "SHRINE": open the Echo shop (P2-01).</summary>
        public void OnShrineClicked()
        {
            MetaProgression.EnsureLoaded();
            if (shop != null) return;
            shop = MetaShopPanelFactory.Build(Camera.main);
            if (shop == null) return;
            // The shop spawns in front of the menu; hide the menu canvas (not this component) until it closes.
            var canvas = GetComponent<Canvas>();
            canvas.enabled = false;
            shop.onClosed = () => { if (this != null) canvas.enabled = true; };
        }

        /// <summary>Menu "SETTINGS" (P2-09): comfort options must be reachable before the first run.</summary>
        public void OnSettingsClicked()
        {
            if (settings != null) settings.Show();
        }

        // Hide the menu canvas while settings is open, however it was opened (button or controller
        // Menu) and closed (BACK or Menu) — same overlap rule as the shrine. One bool compare per frame.
        private void Update()
        {
            if (settings == null) return;
            bool open = settings.IsOpen;
            if (open == settingsHidMenu) return;
            settingsHidMenu = open;
            GetComponent<Canvas>().enabled = !open;
        }

        /// <summary>Menu "ABANDON": the first click asks for confirmation, the second discards the
        /// saved run (banking its Echoes). It does not start a new run (BUG-27).</summary>
        public void OnAbandonRunClicked()
        {
            if (!RunEntryOptions.AbandonConfirmed(abandonArmed))
            {
                abandonArmed = true;
                SetAbandonLabel(RunEntryOptions.ConfirmAbandonLabel);
                return;
            }
            RunDirector.Instance?.AbandonSavedRun();
            RefreshRunButtons();
        }

        private void SetAbandonLabel(string text)
        {
            if (abandonButton == null) return;
            var label = abandonButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text;
        }
    }
}

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

        private void OnEnable()
        {
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
    }
}

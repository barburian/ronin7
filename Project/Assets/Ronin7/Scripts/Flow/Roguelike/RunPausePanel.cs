using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Runtime hook for the in-run pause panel. Mirrors <see cref="RunSummaryPanel"/>: the hierarchy is
    /// built by <see cref="RunPausePanelFactory"/> and this component only surfaces its buttons as events.
    /// </summary>
    public class RunPausePanel : MonoBehaviour
    {
        // Assigned by the factory AFTER AddComponent — see WireButtons for why this cannot be Awake.
        public Button resumeButton;
        public Button settingsButton;
        public Button recalibrateButton;

        public event Action ResumeRequested;
        public event Action SettingsRequested;
        public event Action RecalibrateRequested;

        /// <summary>Wire the buttons. The factory MUST call this after assigning them (AddComponent runs
        /// Awake synchronously, before the fields are set — the defect that once made the boon panel
        /// unclickable). Idempotent.</summary>
        public void WireButtons()
        {
            Wire(resumeButton, () => ResumeRequested?.Invoke());
            Wire(settingsButton, () => SettingsRequested?.Invoke());
            Wire(recalibrateButton, () => RecalibrateRequested?.Invoke());
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}

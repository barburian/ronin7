using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Flow
{
    /// <summary>
    /// P0-03 (BUG-03, Meta VRC): pauses a run on focus loss (Quest menu), app pause (headset off) and
    /// the left controller Menu button. A pause freezes time through <see cref="TimeScaleArbiter"/>'s
    /// <see cref="TimeScaleChannel.Pause"/> channel, freezes the boon-offer fallback timer, pauses audio
    /// and shows <see cref="RunPausePanel"/>. Play resumes ONLY on an explicit RESUME press, never
    /// automatically when focus returns.
    ///
    /// Self-installs once per session (persistent, DontDestroyOnLoad) instead of living in a scene, so it
    /// exists whichever scene the game boots into and needs no scene patching. Outside a run it does
    /// nothing: the menu scenes keep the Menu button for their own settings toggle.
    /// </summary>
    public class RunPauseController : MonoBehaviour
    {
        public static RunPauseController Instance { get; private set; }

        /// <summary>Eye height RECALIBRATE HEIGHT calibrates to (matches MainMenuController's default).</summary>
        private const float TargetEyeHeight = 1.6f;

        public bool IsPaused { get; private set; }

        private InputAction menuAction;
        private RunPausePanel panel;
        private SettingsMenuToggle openSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(RunPauseController));
            DontDestroyOnLoad(go);
            go.AddComponent<RunPauseController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            menuAction = new InputAction("RunPause", InputActionType.Button, "<XRController>{LeftHand}/menuButton");
            menuAction.performed += OnMenuPressed;
            menuAction.Enable();
        }

        private void OnDisable()
        {
            if (menuAction != null)
            {
                menuAction.performed -= OnMenuPressed;
                menuAction.Disable();
                menuAction.Dispose();
                menuAction = null;
            }
            Resume();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Focus loss is skipped in the editor: clicking another editor window would otherwise pause
        // every desktop/MCP play session. Headset-off (OnApplicationPause) and the Menu button apply.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !Application.isEditor) Pause();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnMenuPressed(InputAction.CallbackContext _)
        {
            if (!IsPaused) Pause();
            else if (openSettings != null) CloseSettings(); // Menu = "back" out of SETTINGS
        }

        private void Update()
        {
            if (!IsPaused) return;
            // The run ended or was suspended underneath the pause: never leave the menu frozen.
            if (!RunState.InRun)
            {
                Resume();
                return;
            }
            // A scene load destroys the panel (it lives in the scene); rebuild it once a camera exists.
            if (panel == null && openSettings == null) ShowPanel();
        }

        /// <summary>Pause the run. No-op outside a run or when already paused.</summary>
        public void Pause()
        {
            if (IsPaused || !RunState.InRun) return;
            IsPaused = true;
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Pause, 0f);
            if (RunDirector.Instance != null) RunDirector.Instance.FreezeOfferTimer(true);
            AudioListener.pause = true;
            // Quest can kill a backgrounded app without any quit callback.
            PlayerPrefs.Save();
            ShowPanel();
        }

        /// <summary>Resume play. Called only by the RESUME button (or internally when the run is gone).</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            if (openSettings != null) openSettings.Hide();
            openSettings = null;
            DestroyPanel();
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.Pause);
            if (RunDirector.Instance != null) RunDirector.Instance.FreezeOfferTimer(false);
            AudioListener.pause = false;
        }

        private void ShowPanel()
        {
            if (panel != null) return;
            panel = RunPausePanelFactory.Build(Camera.main);
            if (panel == null) return; // no camera/EventSystem yet; Update retries
            panel.ResumeRequested += Resume;
            panel.SettingsRequested += OpenSettings;
            panel.RecalibrateRequested += Recalibrate;
        }

        private void DestroyPanel()
        {
            if (panel == null) return;
            panel.ResumeRequested -= Resume;
            panel.SettingsRequested -= OpenSettings;
            panel.RecalibrateRequested -= Recalibrate;
            Destroy(panel.gameObject);
            panel = null;
        }

        private void OpenSettings()
        {
            // One lookup per button press, not per frame. The settings panel is scene-owned.
            var toggle = FindAnyObjectByType<SettingsMenuToggle>(FindObjectsInactive.Include);
            if (toggle == null || !toggle.HasPanel)
            {
                Debug.LogWarning("[RunPauseController] No settings panel in this scene.");
                return;
            }
            openSettings = toggle;
            if (panel != null) panel.gameObject.SetActive(false);
            openSettings.Show();
        }

        private void CloseSettings()
        {
            if (openSettings != null) openSettings.Hide();
            openSettings = null;
            if (panel != null) panel.gameObject.SetActive(true);
        }

        private static void Recalibrate() => XREyeHeightCalibrator.Calibrate(TargetEyeHeight);
    }
}

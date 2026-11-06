using System.Collections;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;
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

        [Tooltip("NEW RUN button; with a saved run its first click asks ABANDON SAVED RUN?.")]
        [SerializeField] private Button runButton;

        [Tooltip("CONTINUE RUN button; shown only when a saved run exists.")]
        [SerializeField] private Button continueRunButton;

        [Tooltip("P2-01: shows the banked Echoes balance on the menu.")]
        [SerializeField] private Text echoesLabel;

        [Tooltip("P2-09: the boot scene's settings panel toggle (wired by the roguelike entry-point patcher).")]
        [SerializeField] private Ronin7.Player.SettingsMenuToggle settings;

        private bool newRunArmed;
        private float armedAt;
        private MetaShopPanel shop;
        private WeaponSelectPanel weaponSelect;
        private bool settingsHidMenu;

        private Canvas canvas;
        private bool placed;

        private void Start()
        {
            canvas = GetComponent<Canvas>();
            StartCoroutine(RecenterOnFirstHeadPose());
        }

        internal const float MenuDistance = 1.5f; // m in front of the head; tuned with the render check
        internal const float MenuDrop = 0.1f;     // m below eye level
        internal const int RequiredStableFrames = 5;
        internal const float RevealTimeout = 5f;  // s before the hidden menu shows at its baked pose
        private const float StableEpsilon = 0.02f; // m of head motion per frame still counted as steady

        /// <summary>P7-03: the menu stays hidden until the HMD reports a tracked, plausible-height, steady pose,
        /// then the rig is recentred and the menu is placed in front of the head. After 5 s without one (headset
        /// off the head, editor) the menu is revealed at its baked pose, never placed from a garbage pose, and
        /// the wait continues: the first good pose still recentres and places it.</summary>
        private IEnumerator RecenterOnFirstHeadPose()
        {
            if (canvas != null) canvas.enabled = false;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            int stable = 0;
            Vector3 prev = Vector3.zero;
            float waited = 0f;
            bool revealed = false;
            while (stable < RequiredStableFrames)
            {
                if (!revealed && waited >= RevealTimeout) { revealed = true; RevealCanvas(); }
                yield return null;
                waited += Time.unscaledDeltaTime;
                if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool tracked = head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool v) && v;
                var rig = Ronin7.Player.VRRig.Instance;
                if (rig == null || rig.Head == null) continue;
                Vector3 pos = rig.Head.localPosition;
                stable = IsHeadPoseValid(tracked, pos) ? NextStableFrames(stable, prev, pos) : 0;
                prev = pos;
            }
            RecenterAndPlace();
            placed = true;
            if (!revealed) RevealCanvas();
        }

        // Shrine/settings may have opened meanwhile; they restore the canvas themselves.
        private void RevealCanvas()
        {
            if (canvas != null) canvas.enabled = SettingsClosed && weaponSelect == null && shop == null;
        }

        /// <summary>Recentre the rig (first valid pose or system recenter), then put the canvas in front of the head.</summary>
        private void RecenterAndPlace()
        {
            Ronin7.Player.XRRecenterUtility.RecenterRig();
            PlaceMenu();
        }

        private void PlaceMenu()
        {
            var rig = Ronin7.Player.VRRig.Instance;
            if (rig == null || rig.Head == null) return; // keep the baked pose
            transform.position = PlaceInFront(rig.Head.position, rig.Head.forward, out var rot);
            transform.rotation = rot;
            Debug.Log($"[MainMenu] Placed menu: origin={TrackingOriginMode()}, head local y={rig.Head.localPosition.y:0.00} m, " +
                      $"eye offset={(rig.EyeHeightRoot != null ? rig.EyeHeightRoot.localPosition.y : 0f):0.00} m, " +
                      $"head world y={rig.Head.position.y:0.00} m.");
        }

        private static string TrackingOriginMode()
        {
            var subs = new System.Collections.Generic.List<XRInputSubsystem>(2);
            SubsystemManager.GetSubsystems(subs);
            return subs.Count > 0 ? subs[0].GetTrackingOriginMode().ToString() : "no XR subsystem";
        }

        // A Quest system recenter shifts the origin under the menu: re-place it, but only while this menu exists.
        private void HandleTrackingOriginUpdated(XRInputSubsystem _)
        {
            var rig = Ronin7.Player.VRRig.Instance;
            if (placed && isActiveAndEnabled && rig != null && rig.Head != null && IsHeadPoseValid(HeadTracked(), rig.Head.localPosition))
                RecenterAndPlace();
        }

        /// <summary>Head is tracked, the rig camera is off its zero pose, and its height above the tracking
        /// origin is plausible for a seated or standing player (rejects stale/garbage poses). Pure. P7-03 test seam.</summary>
        internal static bool IsHeadPoseValid(bool tracked, Vector3 localPos) =>
            tracked && localPos.sqrMagnitude > 1e-6f && localPos.y >= 0.4f && localPos.y <= 2.4f;

        /// <summary>Consecutive-frame counter: grows while the head moved under ~2 cm since last frame, else resets. Pure.</summary>
        internal static int NextStableFrames(int count, Vector3 prev, Vector3 cur) =>
            (cur - prev).sqrMagnitude <= StableEpsilon * StableEpsilon ? count + 1 : 0;

        /// <summary>Menu centre ~1.5 m ahead along the head's flat forward, just below eye level, panel facing away
        /// from the head. Looking steeply up/down falls back to +Z (the scene's authored forward). Pure. Test seam.</summary>
        internal static Vector3 PlaceInFront(Vector3 headPos, Vector3 headForward, out Quaternion rot)
        {
            Vector3 flat = Vector3.ProjectOnPlane(headForward, Vector3.up);
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            rot = Quaternion.LookRotation(flat, Vector3.up);
            return new Vector3(headPos.x, headPos.y - MenuDrop, headPos.z) + flat * MenuDistance;
        }

        private readonly System.Collections.Generic.List<XRInputSubsystem> subscribed = new System.Collections.Generic.List<XRInputSubsystem>(2);

        // trackingOriginUpdated is a per-subsystem event; subscribe on enable, always unsubscribe on disable/destroy.
        private void OnDisable()
        {
            foreach (var sub in subscribed) if (sub != null) sub.trackingOriginUpdated -= HandleTrackingOriginUpdated;
            subscribed.Clear();
        }

        private void OnEnable()
        {
            SubsystemManager.GetSubsystems(subscribed);
            foreach (var sub in subscribed) sub.trackingOriginUpdated += HandleTrackingOriginUpdated;
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
            newRunArmed = false;
            if (echoesLabel != null)
            {
                MetaProgression.EnsureLoaded();
                echoesLabel.text = MetaShopPanel.EchoesText(MetaProgression.Echoes);
            }
            SetRunLabel(RunEntryOptions.NewRunButtonLabel(false));
            bool hasRun = RunEntryOptions.HasSavedRun();
            if (continueRunButton != null)
            {
                var label = continueRunButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = RunEntryOptions.ContinueLabel;
                continueRunButton.gameObject.SetActive(RunEntryOptions.ShowContinue(hasRun));
                CloseContinueGap(!hasRun);
            }

            if (continueButton != null)
                continueButton.interactable = SaveSystem.SlotExists(SaveSystem.MostRecentSlot);
        }

        private const float ContinueSlotHeight = 82f;
        private bool gapClosed;

        /// <summary>No saved run: lift the buttons stacked below the hidden CONTINUE RUN slot so no hole is left.</summary>
        private void CloseContinueGap(bool close)
        {
            if (close == gapClosed) return;
            gapClosed = close;
            var parent = continueRunButton.transform.parent;
            float y = continueRunButton.GetComponent<RectTransform>().anchoredPosition.y;
            foreach (Transform child in parent)
            {
                var rt = child as RectTransform;
                if (rt == null || child == continueRunButton.transform || child.GetComponent<Button>() == null) continue;
                if (close ? rt.anchoredPosition.y < y : rt.anchoredPosition.y <= y) rt.anchoredPosition += new Vector2(0f, close ? ContinueSlotHeight : -ContinueSlotHeight);
            }
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
            var rig = Ronin7.Player.VRRig.Instance;
            if (rig != null && rig.Head != null && !IsHeadPoseValid(HeadTracked(), rig.Head.localPosition))
            {
                Debug.LogWarning("[MainMenu] Recalibrate ignored: head pose not tracked / implausible height.");
                return;
            }
            Ronin7.Player.XRRecenterUtility.RecenterRig();
            Ronin7.Player.XREyeHeightCalibrator.Calibrate(targetEyeHeight);
            PlaceMenu();
        }

        internal static bool HeadTracked()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool v) && v;
        }

        /// <summary>Menu "CONTINUE RUN": resume the saved run, no select screen.</summary>
        public void OnContinueRunClicked() => RunDirector.Instance?.StartRun();

        /// <summary>Menu "NEW RUN": always a new run through WEAPON SELECT (A15.13), whose START begins it
        /// (random seed). With a saved run the first click only warns ABANDON SAVED RUN?; the second
        /// (after <see cref="RunEntryOptions.ConfirmDelay"/>) opens the select screen; START banks and deletes the save, BACK keeps it.</summary>
        public void OnStartRunClicked()
        {
            if (weaponSelect != null || shop != null || (settings != null && settings.IsOpen)) return;
            switch (RunEntryOptions.NewRunClick(RunEntryOptions.HasSavedRun(), newRunArmed, Time.unscaledTime - armedAt))
            {
                case RunEntryOptions.NewRunStep.ArmWarning:
                    if (newRunArmed) return; // too-early second click: keep waiting
                    newRunArmed = true;
                    armedAt = Time.unscaledTime;
                    SetRunLabel(RunEntryOptions.NewRunButtonLabel(true));
                    return;
            }
            bool abandonOnStart = RunEntryOptions.HasSavedRun();
            MetaProgression.EnsureLoaded();
            weaponSelect = WeaponSelectPanelFactory.Build(Camera.main);
            if (weaponSelect == null) return;
            var canvas = GetComponent<Canvas>();
            canvas.enabled = false;
            var panel = weaponSelect;
            panel.onClosed = () => { if (this != null) { RefreshRunButtons(); canvas.enabled = SettingsClosed; } };
            // START: the run fades and loads next; keep the menu hidden instead of popping back at end of frame.
            // R14: the saved run is abandoned only here, before StartRun (which would otherwise resume it).
            panel.startRun = () =>
            {
                var rd = RunDirector.Instance;
                if (rd == null) return;
                panel.onClosed = null;
                if (abandonOnStart) rd.AbandonSavedRun();
                rd.StartRun();
            };
        }

        /// <summary>Menu "SHRINE": open the Echo shop (P2-01).</summary>
        public void OnShrineClicked()
        {
            MetaProgression.EnsureLoaded();
            if (shop != null || weaponSelect != null || (settings != null && settings.IsOpen)) return;
            shop = MetaShopPanelFactory.Build(Camera.main);
            if (shop == null) return;
            // The shop spawns in front of the menu; hide the menu canvas (not this component) until it closes.
            var canvas = GetComponent<Canvas>();
            canvas.enabled = false;
            shop.onClosed = () => { if (this != null) { RefreshRunButtons(); canvas.enabled = SettingsClosed; } };
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
            GetComponent<Canvas>().enabled = !open && weaponSelect == null && shop == null;
        }

        private bool SettingsClosed => settings == null || !settings.IsOpen;

        private void SetRunLabel(string text)
        {
            if (runButton == null) return;
            var label = runButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text;
        }
    }
}

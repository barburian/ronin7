using System.Collections;
using System.Runtime.CompilerServices;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

namespace Ronin7.Flow
{
    /// <summary>
    /// The missing subscriber that turns the isolated phase scenes into one playable loop.
    /// A persistent singleton (mirrors <see cref="GameState"/>) that owns scene transitions and
    /// the high-level mode flow:
    ///
    ///   Ship hub --(LandingRequested)--> OnFoot mission --(ZoneCompleted)--> Ship hub --> ...
    ///
    /// It listens on the <see cref="EventBus"/> (events live in Core, so this assembly never
    /// needs to reference World/Ship), and covers every scene swap with a <see cref="ScreenFader"/>
    /// fade-to-black — the standard VR-comfort treatment so the player never sees a hard cut.
    ///
    /// Because the rig is rebuilt per scene, the fader lives on each scene's head camera: we fade
    /// the *current* camera out before loading, then re-establish black on the *new* camera and
    /// fade in. Unity holds the last rendered (black) frame during the synchronous activation, so
    /// the view stays dark across the gap.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("Scenes (must be in the Build Profiles scene list)")]
        [SerializeField] private string onFootScene = "Phase4_Zone";
        [SerializeField] private string mainMenuScene = "Phase6_Boot";
        [Tooltip("Kessler's salvage ship — the persistent on-foot hub the player stages missions from. " +
                 "Never counts as a completed mission.")]
        [SerializeField] private string shipHubScene = "Galaxy1_Ch1_Hub";

        [Header("Boot")]
        [Tooltip("Load the hub automatically on start (used for quick-boot / in-editor testing).")]
        [SerializeField] private bool loadFlightOnStart = true;

        [Header("Transition")]
        [SerializeField] private float fadeDuration = 0.4f;
        [Tooltip("Max frames to wait for the new scene's head camera before fading back in.")]
        [SerializeField] private int maxCameraWaitFrames = 30;

        [Header("Game Over")]
        [SerializeField] private float gameOverPanelDuration = 4f;

        private bool transitioning;
        private bool gameOverInProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LandingRequested>(OnLandingRequested);
            EventBus.Subscribe<ZoneCompleted>(OnZoneCompleted);
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<PlayerShipDestroyed>(OnPlayerShipDestroyed);
        }

        // Reviewed for LoadSceneMode.Single leaks: the singleton is DontDestroyOnLoad (Awake) and
        // these unsubscribes balance every Subscribe (incl. a destroyed duplicate's), so no leak.
        private void OnDisable()
        {
            EventBus.Unsubscribe<LandingRequested>(OnLandingRequested);
            EventBus.Unsubscribe<ZoneCompleted>(OnZoneCompleted);
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<PlayerShipDestroyed>(OnPlayerShipDestroyed);
        }

        private void Start()
        {
            if (loadFlightOnStart)
            {
                // Quick-boot skips the menu (no New/Load choice was made), so arriving must not
                // autosave — it would clobber MostRecentSlot with a blank campaign.
                StartCoroutine(Transition(shipHubScene, GameMode.OnFoot, quickBoot: true));
            }
        }

        private void OnLandingRequested(LandingRequested evt)
        {
            if (transitioning) return;

            string dest = string.IsNullOrEmpty(evt.DestinationScene) ? onFootScene : evt.DestinationScene;
            // A mission is launched from the hub; that hub -> mission hop is the boundary that repoints
            // "which mission am I on". Interior scene hops within a mission (e.g. market -> hideout)
            // republish LandingRequested while already in the mission and must NOT repoint it — they
            // are not launched from the hub. Persistence happens on arrival: every scene reach
            // autosaves inside FadeLoadFade, after this state change lands.
            bool fromHub = SceneManager.GetActiveScene().name == shipHubScene;
            CampaignState.NoteLanding(dest, fromHub);
            StartCoroutine(Transition(dest, GameMode.OnFoot));
        }

        private void OnZoneCompleted(ZoneCompleted _)
        {
            if (transitioning) return;

            // A mission's terminal scene published ZoneCompleted: mark the mission (its entry scene,
            // held in LastPlanetScene since the hub launch) complete and return to the hub. The hub is
            // passed as the "never completes" guard so a stray ZoneCompleted in the hub is ignored.
            CampaignState.NoteZoneCompleted(shipHubScene);
            StartCoroutine(Transition(shipHubScene, GameMode.OnFoot));
        }

        private void OnEntityDied(EntityDied evt)
        {
            if (VRRig.Instance == null) return;
            if (evt.Entity != VRRig.Instance.gameObject) return; // only the on-foot player rig
            if (gameOverInProgress || transitioning) return;
            StartCoroutine(GameOverRoutine());
        }

        private void OnPlayerShipDestroyed(PlayerShipDestroyed _)
        {
            if (gameOverInProgress || transitioning) return;
            StartCoroutine(GameOverRoutine());
        }

        /// <summary>Fade out, swap scenes, set the new mode, fade back in. Re-entrancy guarded.</summary>
        private IEnumerator Transition(string scene, GameMode mode, bool quickBoot = false)
        {
            if (transitioning) yield break;
            transitioning = true;
            yield return FadeLoadFade(scene, mode, runUnloadAfter: true, quickBoot: quickBoot);
            transitioning = false;
        }

        /// <summary>Public entry point used by the in-game settings panel's "Return to Main Menu" button.</summary>
        public void ReturnToMainMenu()
        {
            if (transitioning) return;
            StartCoroutine(ReturnToMainMenuRoutine());
        }

        private IEnumerator ReturnToMainMenuRoutine()
        {
            transitioning = true;
            yield return FadeLoadFade(mainMenuScene, GameMode.Boot, runUnloadAfter: true);
            transitioning = false;
        }

        /// <summary>Menu "Start New Game": reset progress and drop into the ship hub on-foot.</summary>
        public void StartNewGame()
        {
            if (transitioning) return;
            Galaxy1Progress.Reset();
            CampaignState.Reset();
            StartCoroutine(Transition(shipHubScene, GameMode.OnFoot));
        }

        /// <summary>Campaign entry: reset progress and load Kessler's ship hub (on-foot).</summary>
        public void StartCampaign()
        {
            if (transitioning) return;
            Galaxy1Progress.Reset();
            CampaignState.Reset();
            StartCoroutine(Transition(shipHubScene, GameMode.OnFoot));
        }

        /// <summary>Main-menu "Load Slot N": restore campaign state and return to the ship hub on-foot.</summary>
        public void LoadGame(int slot)
        {
            if (transitioning) return;
            var data = SaveSystem.Load(slot);
            if (data == null)
            {
                Debug.LogWarning($"[Flow] Load requested for empty/unreadable slot {slot}.");
                return;
            }
            CampaignState.ApplyFrom(data);
            SaveSystem.MostRecentSlot = slot;
            StartCoroutine(Transition(shipHubScene, GameMode.OnFoot));
        }

        /// <summary>
        /// Gate for the post-load camera wait: stop waiting once the head camera exists OR the frame
        /// budget is spent. The budget arm only bounds the wait so a never-arriving camera can't spin
        /// the coroutine forever — it is not a reveal: with no camera there is nothing to render or
        /// fade, so the caller logs a diagnostic and proceeds. Pure, so it is unit-testable.
        /// </summary>
        internal static bool ShouldReveal(bool cameraPresent, int framesWaited, int maxFrames)
            => cameraPresent || framesWaited >= maxFrames;

        /// <summary>
        /// Arrival-autosave gate, checked once per completed scene load: reaching any player scene
        /// is a checkpoint. Excluded: Boot arrivals (main menu, via Return-to-Menu or game over —
        /// nothing new to persist there) and the quick-boot hub load (it runs before the player
        /// chose New/Load, so saving would clobber MostRecentSlot). Pure, so it is unit-testable.
        /// </summary>
        internal static bool ShouldAutosaveOnArrival(GameMode mode, bool quickBoot)
            => !quickBoot && mode != GameMode.Boot;

        /// <summary>Shared inner body of every scene transition: fade out → load → mode → autosave → camera → fade in.</summary>
        private IEnumerator FadeLoadFade(string scene, GameMode mode, bool runUnloadAfter, bool quickBoot = false)
        {
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogError("[Flow] Transition requested with no scene name.");
                yield break;
            }

            // 1. Fade the current view to black (skipped at boot when no camera exists yet).
            var fader = ScreenFader.Ensure();
            if (fader != null) yield return fader.FadeOut(fadeDuration);

            // 2. Swap scenes (Single). The last black frame is held during activation.
            var load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError($"[Flow] Could not load scene '{scene}'. Is it in the Build Profiles scene list?");
                if (fader != null) yield return fader.FadeIn(fadeDuration);
                yield break;
            }
            while (!load.isDone) yield return null;

            // 3. Memory cleanup while still under the black fade.
            if (runUnloadAfter)
            {
                var unload = Resources.UnloadUnusedAssets();
                while (unload != null && !unload.isDone) yield return null;
                System.GC.Collect();
            }

            // 4. Announce the new mode so per-scene systems react (ShipController also self-sets).
            if (GameState.Instance != null) GameState.Instance.SetMode(mode);

            // 5. Autosave: the player has reached a new scene — persist campaign progress to the
            //    active slot. Runs while still under the black fade so the save-file write can
            //    never hitch a visible frame (VR 90 FPS budget).
            if (ShouldAutosaveOnArrival(mode, quickBoot))
                SaveSystem.Autosave(CampaignState.ToSaveData());

            // 6. Wait (budgeted, ShouldReveal) for the new scene's head camera, then re-establish
            //    black before fading in. Normally the camera arrives — including one that appears
            //    mid-wait — and the fade-in below reveals it; the budget only bounds the wait so a
            //    never-arriving camera can't spin here forever.
            int waited = 0;
            while (!ShouldReveal(Camera.main != null, waited, maxCameraWaitFrames))
            {
                waited++;
                yield return null;
            }
            // With no head camera there is nothing to render or fade (ScreenFader.Ensure returns null
            // below, so the fade-in is skipped). That's a scene-authoring problem, not a soft-lock:
            // log a diagnostic and proceed.
            if (Camera.main == null)
                Debug.LogWarning($"[Flow] No head camera after {maxCameraWaitFrames} frames loading '{scene}'; nothing to render or fade — check the scene's XR rig/head camera. Skipping fade-in.");
            var newFader = ScreenFader.Ensure();
            if (newFader != null)
            {
                newFader.SetBlackInstant();
                yield return newFader.FadeIn(fadeDuration);
            }
        }

        private IEnumerator GameOverRoutine()
        {
            gameOverInProgress = true;

            var panel = GameOverPanelFactory.Build(Camera.main);
            bool dismissed = false;
            if (panel != null) panel.OnDismissed += () => dismissed = true;

            float t = 0f;
            while (t < gameOverPanelDuration && !dismissed)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (panel != null) Destroy(panel.gameObject);

            transitioning = true;
            yield return FadeLoadFade(mainMenuScene, GameMode.Boot, runUnloadAfter: true);
            transitioning = false;

            gameOverInProgress = false;
        }
    }
}

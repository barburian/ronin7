using System;
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
        private bool pendingGameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Meta progress (Echoes / BestDepth / RunsCompleted / RunsWon) is account-wide and lives in
            // its own meta.json. Load it once here, on the persistent singleton's Awake — this runs after
            // MetaProgression's SubsystemRegistration reset, so it repopulates rather than being wiped.
            // Without this call Load() had no callers at all and every run's progress died with the session.
            MetaProgression.Load();
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
            // A death during a roguelike run is handled by RunDirector (permadeath -> hub, echoes
            // banked), not this path (-> main menu game over). RunState lives in Core so this check
            // costs Flow no new coupling to Ronin7.Flow.Roguelike.
            //
            // A7.1: also guard on RunDirector.EndingRun, not just RunState.InRun. RunState.End() (which
            // flips InRun false) now runs only after the hub scene load completes, so InRun alone would
            // already prevent the original race; EndingRun is latched the instant RunDirector starts
            // ending the run (before it even publishes RunEnded) as a second, order-independent guard —
            // this must not depend on EventBus.Publish's subscription order deciding which handler for
            // this same EntityDied event runs first.
            if (RunState.InRun || (RunDirector.Instance != null && RunDirector.Instance.EndingRun)) return;
            TriggerGameOver();
        }

        private void OnPlayerShipDestroyed(PlayerShipDestroyed _)
        {
            TriggerGameOver();
        }

        /// <summary>
        /// Single funnel for both death sources (on-foot EntityDied, ship PlayerShipDestroyed).
        /// A death can land while `transitioning` is true (mid fade+load, e.g. a hazard/DoT tick
        /// during a hub&lt;-&gt;mission hop); previously that was dropped for good by the transitioning
        /// guard and the player arrived in the next scene "alive". Latch it instead and let the
        /// finishing transition fire it (see ConsumePendingGameOver).
        /// </summary>
        private void TriggerGameOver()
        {
            switch (ResolveGameOver(gameOverInProgress, transitioning))
            {
                case GameOverAction.Latch:
                    pendingGameOver = true;
                    break;
                case GameOverAction.FireNow:
                    StartCoroutine(GameOverRoutine());
                    break;
                // Ignore: a game over is already in flight, nothing to do.
            }
        }

        /// <summary>
        /// Decision for what a death event should do given the current game-over/transition state.
        /// Pure, so it is unit-testable.
        /// </summary>
        internal enum GameOverAction { Ignore, Latch, FireNow }

        internal static GameOverAction ResolveGameOver(bool gameOverInProgress, bool transitioning)
            => gameOverInProgress ? GameOverAction.Ignore : (transitioning ? GameOverAction.Latch : GameOverAction.FireNow);

        /// <summary>
        /// Read-and-clear step for the latch set by <see cref="TriggerGameOver"/>: called once a
        /// transition's fade+load fully completes, after `transitioning` has already flipped back to
        /// false. Returns true (and clears the latch) at most once per latched death, so callers can't
        /// double-fire the game-over path. Internal so it is unit-testable without spinning the
        /// coroutine.
        /// </summary>
        internal bool ConsumePendingGameOver()
        {
            if (!pendingGameOver) return false;
            pendingGameOver = false;
            return true;
        }

        /// <summary>
        /// Drops a pending latch without firing it. Used where a mid-fade death must not survive
        /// past the point it latched at: a deliberate return-to-menu supersedes it (the player is
        /// no longer in that session), and starting/loading a session must not inherit it either.
        /// Internal so it is unit-testable.
        /// </summary>
        internal void DiscardPendingGameOver() => pendingGameOver = false;

        /// <summary>Fade out, swap scenes, set the new mode, fade back in. Re-entrancy guarded.</summary>
        private IEnumerator Transition(string scene, GameMode mode, bool quickBoot = false)
        {
            if (transitioning) yield break;
            transitioning = true;
            yield return FadeLoadFade(scene, mode, runUnloadAfter: true, quickBoot: quickBoot);
            transitioning = false;

            // Ordering: consume the latch only after `transitioning` is already false, and only after
            // FadeLoadFade has fully returned — which means its arrival autosave (step 5 inside
            // FadeLoadFade) already ran. That autosave persists CampaignState (scene/mission progress)
            // only; it has no "alive/dead" field, so a death that happened mid-fade can't corrupt it.
            // Consuming here just means the game-over path starts right after the player is safely
            // parked in the arrival scene instead of racing the in-flight transition. Clearing the flag
            // inside ConsumePendingGameOver before starting GameOverRoutine rules out a double-fire.
            if (ConsumePendingGameOver()) StartCoroutine(GameOverRoutine());
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

            // A death that latched mid-fade during this deliberate menu exit belongs to the session
            // just left, not to whatever the player does next at the menu — discard, don't consume.
            DiscardPendingGameOver();
        }

        /// <summary>Menu "Start New Game": reset progress and drop into the ship hub on-foot.</summary>
        public void StartNewGame()
        {
            if (transitioning) return;
            // Defensive: a fresh session must never inherit a stale latch from whatever came before it.
            DiscardPendingGameOver();
            Galaxy1Progress.Reset();
            CampaignState.Reset();
            CampaignStats.Reset();
            DrillBestScores.Reset();
            StartCoroutine(Transition(shipHubScene, GameMode.OnFoot));
        }

        /// <summary>Campaign entry: reset progress and load Kessler's ship hub (on-foot).</summary>
        public void StartCampaign()
        {
            if (transitioning) return;
            DiscardPendingGameOver();
            Galaxy1Progress.Reset();
            CampaignState.Reset();
            CampaignStats.Reset();
            DrillBestScores.Reset();
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
            DiscardPendingGameOver();
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

        /// <summary>
        /// Public entry point for other persistent systems that need the same fade/load/autosave
        /// path this file uses for every scene swap — currently <see cref="RunDirector"/>, so the
        /// roguelike run's per-node scene reloads (and its hub return) don't duplicate it. Always
        /// runs the post-load unload/GC step, matching every in-file caller.
        ///
        /// <paramref name="onLoaded"/> (A7.9) fires once the scene has loaded and its mode is set, but
        /// BEFORE the camera wait / fade-in — i.e. still under the black fade. RunDirector uses this to
        /// apply boons to the freshly-loaded rig before the player can see or be hit at base stats,
        /// instead of after the fade-in completes (which used to show a live, attackable, base-stat
        /// player for the whole fade-in and then a visible instant full-heal).
        ///
        /// <paramref name="skipReveal"/> is checked right after <paramref name="onLoaded"/>; when it
        /// returns true the camera wait and fade-in are skipped entirely and the screen stays black.
        /// RunDirector uses this for a zero-enemy Forge/Treasure node: its RoomCleared fires
        /// synchronously from the arena's own Start() (i.e. before this method's load-wait even
        /// returns), latching the next scene load — fading fully in just to immediately fade back out
        /// for that queued load is a pointless flash the player would otherwise see six-ish times a run.
        /// </summary>
        public IEnumerator LoadSceneFaded(string scene, GameMode mode, Action onLoaded = null, Func<bool> skipReveal = null)
            => FadeLoadFade(scene, mode, runUnloadAfter: true, onLoaded: onLoaded, skipReveal: skipReveal);

        /// <summary>Shared inner body of every scene transition: fade out → load → mode → onLoaded → autosave → camera → fade in.</summary>
        private IEnumerator FadeLoadFade(string scene, GameMode mode, bool runUnloadAfter, bool quickBoot = false, Action onLoaded = null, Func<bool> skipReveal = null)
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

            // 4.5. Caller hook, still under the black fade (A7.9 — see LoadSceneFaded's doc comment).
            onLoaded?.Invoke();

            // 5. Autosave: the player has reached a new scene — persist campaign progress to the
            //    active slot. Runs while still under the black fade so the save-file write can
            //    never hitch a visible frame (VR 90 FPS budget).
            if (ShouldAutosaveOnArrival(mode, quickBoot))
                SaveSystem.Autosave(CampaignState.ToSaveData());

            // 5.5. Skip the reveal entirely when the caller already knows another load is queued
            // (A7.9 — see LoadSceneFaded's doc comment). The screen is already black from step 1; do
            // nothing further so it stays that way until the queued load's own fade-out (a no-op fade
            // from black) and load take over.
            if (skipReveal != null && skipReveal()) yield break;

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

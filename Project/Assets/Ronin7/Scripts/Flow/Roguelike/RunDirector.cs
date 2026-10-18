using System;
using System.Collections;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>
    /// Persistent singleton (mirrors <see cref="GameFlowManager"/>) that owns the roguelike run
    /// lifecycle: seeding a run, reacting to <see cref="RoomCleared"/> to offer boons / advance /
    /// win, and reacting to the player's death to end the run.
    ///
    /// DEATH-PATH SEAM: a death is already funnelled through <see cref="GameFlowManager.OnEntityDied"/>,
    /// which would otherwise show the "Game Over" panel and return to the main menu — wrong for a
    /// run, which must bank echoes and return to the hub instead, with no competing panel/transition.
    /// Rather than race <see cref="GameFlowManager"/>'s own <c>OnEntityDied</c> handler (EventBus
    /// multicast order isn't a contract either script should depend on), <c>GameFlowManager.OnEntityDied</c>
    /// gained two guards — <c>RunState.InRun</c> and this director's <see cref="EndingRun"/> latch — so
    /// it never starts its own game-over routine while a run is active or ending; this director's
    /// <see cref="OnEntityDied"/> is the sole handler for that case. See the comment at that guard in
    /// GameFlowManager.cs and <see cref="EndRun"/>'s own doc comment (A7.1).
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        public static RunDirector Instance { get; private set; }

        [Header("Scenes (must be in the Build Profiles scene list)")]
        [SerializeField] private string runArenaScene = "RunArena";
        [Tooltip("Scene loaded when a run ends. The main menu, NOT the story hub: a run is a " +
                 "self-contained loop (end -> summary -> ENTER THE FRACTURE -> next run), and " +
                 "Galaxy1_Ch1_Hub is the campaign's narrative hub with its own MissionDirector.")]
        [SerializeField] private string runEndScene = "Phase6_Boot";

        [Header("Boons")]
        [Tooltip("Source pool for starting boons, room-clear offers, and guaranteed Treasure rewards.")]
        [SerializeField] private BoonCatalog boonCatalog;
        /// <summary>P4-10: the Shrine reads Keepsake names from it.</summary>
        internal BoonCatalog Catalog => boonCatalog;

        [Header("Events (P2-04)")]
        [Tooltip("Pool an Event node draws from (seeded per node). Data/Roguelike/Events/*.asset.")]
        [SerializeField] private RunEventDefinition[] runEvents;

        [Header("Loadouts (P4-08)")]
        [Tooltip("Every WeaponDefinition the LOADOUT rack can pick, matched by WeaponDefinition.id. Data/*.asset.")]
        [SerializeField] private WeaponDefinition[] weapons;

        /// <summary>A7.2: how long the boon offer panel waits for player input before auto-picking
        /// choice 0 and advancing — the same safety net GameOverPanel gets from its own auto-dismiss
        /// timer, for a panel whose click is mandatory (not optional) to leave the node.</summary>
        private const float BoonOfferFallbackSeconds = 60f;

        /// <summary>Arbitrary node index outside the valid 0..TotalNodes-1 range, used only to seed a
        /// stable-but-distinct RunRng substream for starting-boon selection (RunRng.ForNode's mixing
        /// works fine on any int) — never collides with a real node's offer/spawn RNG.</summary>
        private const int StartingBonusRngNode = -1;

        // Re-entrancy guard for this director's own scene loads (mirrors GameFlowManager's
        // `transitioning` field). A zero-enemy Forge/Treasure node's RoomCleared fires synchronously
        // from RunArenaController.Start() — i.e. from inside the still-in-flight FadeLoadFade call
        // that loaded it, before that call even reaches its camera-wait/fade-in tail — so a second
        // StartSceneLoad can legitimately land while transitioning is still true. Rather than drop
        // it, latch it in pendingScene and fire it once the in-flight load finishes (mirrors
        // GameFlowManager's pendingGameOver latch/consume idiom).
        private bool transitioning;
        private string pendingScene;

        /// <summary>BUG-02: a zero-enemy Forge/Treasure node clears from RunArenaController.Start(),
        /// which runs before the onLoaded hook restores the carried HP onto the freshly built rig — so
        /// the reward would read the prefab's full HP. Such a clear is parked here and replayed by
        /// <see cref="OnArenaLoaded"/> once the rig holds the run's real HP.</summary>
        private RoomCleared? deferredClear;

        /// <summary>True from the start of an arena load until <see cref="OnArenaLoaded"/> has pushed
        /// the run's HP/boons onto the new rig. Only clears arriving inside this window are deferred.</summary>
        private bool awaitingRigRestore;

        /// <summary>A7.1: set the instant this director starts ending a run — before EndRun does
        /// anything else, including publishing <see cref="RunEnded"/> — and cleared only once
        /// <see cref="RunState.End"/> has actually run (in <see cref="SceneLoadRoutine"/>, after the hub
        /// load completes). See <see cref="EndRun"/> and the guard in GameFlowManager.OnEntityDied.</summary>
        internal bool EndingRun { get; private set; }

        /// <summary>A7.9: idempotence latch — a repeated RoomCleared for the node already processed
        /// (the arena scene has no reason to publish one twice, but nothing prevents it) must not
        /// double-award echoes or a second boon offer. Reset per run in StartRun.</summary>
        private int lastClearedNode = -1;

        /// <summary>CampaignStats.EnemiesDefeated as of StartRun, so the summary can subtract.</summary>
        private int enemiesDefeatedAtRunStart;

        /// <summary>P0-11 run log: when this session's play of the run began (resets on resume), and
        /// what killed the player, if anything.</summary>
        private float runStartRealtime;
        private string deathBy;

        // Boon-offer state for the node currently awaiting a choice.
        private RunRng offerRng;
        private RunNode offerNode;
        private List<BoonDefinition> offerChoices;
        private BoonOfferPanel offerPanel;
        private RunSummaryPanel summaryPanel;
        private float offerFallbackDeadline = -1f;
        /// <summary>P0-03: seconds left on the offer fallback while paused, or -1 when not frozen.</summary>
        private float frozenOfferSeconds = -1f;

        // This run's boon aggregation. One instance for the whole run (A1.1/A4.1) — Clear()+replay every
        // arena load rather than re-`new`-ing, since RunState.Boons/RevivesSpent are the durable ledger
        // this rebuilds from either way (A7.9).
        private readonly BoonInventory boonInventory = new BoonInventory();
        private readonly HashSet<string> warnedUnknownBoons = new HashSet<string>();

        // ---- P2-04 path choice / shop / event state ----
        /// <summary>Seconds without moving (head XZ within <see cref="PathStillRadius"/>) before the
        /// CHOOSE YOUR PATH panel joins the exit gates.</summary>
        internal const float PathPanelStillSeconds = 6f;
        private const float PathStillRadius = 0.5f;
        internal const int MendPrice = 40;
        private enum PanelMode { None, Path, Shop, Event, Forge, Treasure, Sector, Temper }
        private PanelMode panelMode;
        private BoonOfferPanel choicePanel;
        private int[] pathChoices;
        private float stillSeconds;
        private Vector3 stillAnchor;
        private List<BoonDefinition> shopStock;
        private bool[] shopSold;
        private bool mendSold;
        private RunEventDefinition activeEvent;
        private RunRng eventRng;

        // ---- P2-02 reveal cards / beats ----
        internal const float DeathBeatSeconds = 1.2f;
        internal const float WinBeatSeconds = 2f;
        internal const float SectorTitleSeconds = 2.5f;
        private BoonDefinition treasureBoon;
        private GameObject deathVolume;
        private VolumeProfile deathProfile;

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
            EventBus.Subscribe<RoomCleared>(OnRoomCleared);
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<HitResisted>(OnHitResisted);
            EventBus.Subscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Subscribe<PathChosen>(OnPathChosen);
            if (Instance == this) ArtSlots.ModalOpen = IsModalOpen; // P4-04: Arts stay quiet under a panel
        }

        /// <summary>P4-04 modal gate: a boon offer, a choice panel (path/forge/shop/treasure/checkpoint/event),
        /// the run summary or the pause menu is up.</summary>
        public bool IsModalOpen() =>
            offerPanel != null || choicePanel != null || summaryPanel != null
            || (RunPauseController.Instance != null && RunPauseController.Instance.IsPaused);

        // Balances OnEnable's subscriptions (incl. a destroyed duplicate's) — no leak across
        // LoadSceneMode.Single, same reasoning as GameFlowManager's OnDisable.
        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomCleared>(OnRoomCleared);
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<HitResisted>(OnHitResisted);
            EventBus.Unsubscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Unsubscribe<PathChosen>(OnPathChosen);
            if (Instance == this) ArtSlots.ModalOpen = null;
            DismissOfferPanel();
            DismissChoicePanel();
            DismissRunSummary();
            ClearDeathVolume();
        }

        /// <summary>Runs the offer's fallback timer (the panel lazy-follows the head itself — P1-13)
        /// and feeds the panel's last-10-seconds countdown.</summary>
        private void Update()
        {
            if (pathChoices != null && choicePanel == null) TickPathStillness();
            if (offerPanel == null) return;

            if (offerFallbackDeadline >= 0f)
                offerPanel.SetCountdown(RemainingOfferSeconds(offerFallbackDeadline, Time.unscaledTime));

            if (offerFallbackDeadline >= 0f && Time.unscaledTime >= offerFallbackDeadline)
            {
                Debug.LogWarning("[RunDirector] Boon offer timed out with no player input; auto-picking the first choice.");
                OnBoonChoiceSelected(0);
            }
        }

        /// <summary>A run is (re)starting with no run live, so a latch left stuck by an aborted run-end
        /// load must not block it. Safe: StartRun/TryResumeRun return early while a run is still live.</summary>
        internal void ClearEndingLatch() => EndingRun = false;

        /// <summary>Menu/hub entry (ENTER THE FRACTURE): resumes the autosaved run if a
        /// valid <see cref="RunSnapshot"/> exists (a crash or app kill mid-run), otherwise starts a new
        /// run with a random seed.</summary>
        public void StartRun()
        {
            if (!TryResumeRun()) StartRun(GenerateSeed());
        }

        /// <summary>Start a run with a known seed (daily run / replay).</summary>
        public void StartRun(uint seed)
        {
            if (RunState.InRun) return; // guard against a double-tap on the hub launcher
            ClearEndingLatch();
            RunState.Begin(seed);
            pathChoices = null;
            lastClearedNode = -1;
            deferredClear = null;
            warnedUnknownBoons.Clear();
            runStartRealtime = Time.realtimeSinceStartup;
            deathBy = null;
            // CampaignStats.EnemiesDefeated is a lifetime counter; snapshot it so the run summary can
            // report THIS run's kills as a delta rather than the campaign total.
            enemiesDefeatedAtRunStart = CampaignStats.EnemiesDefeated;
            ApplyStartingBonuses();
            RunTally.Reset();
            RunLog.Append(new RunLogEntry { utc = DateTime.UtcNow.ToString("o"), seed = seed, evt = "start", build = BuildTag() });
            EventBus.Publish(new RunStarted(seed));
            WriteRunSnapshot();
            StartSceneLoad(runArenaScene);
        }

        /// <summary>Rebuilds RunState from the saved snapshot and reloads its current node from the
        /// start. A stale/invalid snapshot is deleted and false is returned (caller starts fresh).</summary>
        private bool TryResumeRun()
        {
            if (RunState.InRun) return false;
            var snapshot = RunSnapshot.Read();
            if (snapshot == null) return false;
            if (!RunState.Restore(snapshot))
            {
                RunSnapshot.Delete();
                return false;
            }
            ClearEndingLatch();
            pathChoices = null;
            lastClearedNode = -1;
            deferredClear = null;
            warnedUnknownBoons.Clear();
            runStartRealtime = Time.realtimeSinceStartup;
            deathBy = null;
            enemiesDefeatedAtRunStart = CampaignStats.EnemiesDefeated - snapshot.runKills;
            RunTally.Reset(); // counts cover the resumed part only
            EventBus.Publish(new RunStarted(RunState.Seed));
            StartSceneLoad(runArenaScene);
            return true;
        }

        /// <summary>Mid-run autosave on entering a node (after HP/boons/echoes for the node just left
        /// are recorded), so a crash resumes here instead of losing the run.</summary>
        private void WriteRunSnapshot()
        {
            var snapshot = RunState.ToSnapshot();
            snapshot.runKills = CampaignStats.EnemiesDefeated - enemiesDefeatedAtRunStart;
            RunSnapshot.Write(snapshot);
        }

        /// <summary>Give up the current run early: same loss bookkeeping as permadeath (echoes
        /// banked, boons lost), just without a death event behind it.</summary>
        public void AbandonRun()
        {
            if (!RunState.InRun) return;
            deathBy = RunLog.AbandonedMarker;
            EndRun(won: false, beat: false);
        }

        /// <summary>
        /// P0-04 SAVE &amp; QUIT (and Return-to-Menu during a run): leave the run without ending it. The
        /// snapshot written on entering this node is kept, so the menu offers RESUME RUN and resuming
        /// restarts this node from its entry state (D-09). Kills/HP since node entry are not kept.
        /// </summary>
        public void SuspendRun()
        {
            if (!RunState.InRun || EndingRun) return;
            DismissOfferPanel();
            DismissChoicePanel();
            pathChoices = null;
            deferredClear = null;
            RunState.Reset();
            StartSceneLoad(runEndScene); // the run-end scene loads as GameMode.Boot: no autosave
        }

        /// <summary>Entrance ABANDON: discard the saved (not-live) run. Banks the snapshot's unbanked
        /// Echoes exactly once, same as <see cref="AbandonRun"/>/permadeath (<see cref="EndRun"/>), and
        /// only then deletes the snapshot so a crash in between cannot lose or double-bank them.
        /// P0-04/BUG-27: it no longer starts a new run; the player does that from the menu.</summary>
        public void AbandonSavedRun()
        {
            if (RunState.InRun) return;
            BankAndDeleteSavedRun();
        }

        /// <summary>Banks a saved run's LOSS Echo result for its depth (P4-11: never its Shards) and deletes the
        /// snapshot. No-op without one.</summary>
        internal static void BankAndDeleteSavedRun()
        {
            var snapshot = RunSnapshot.Read();
            if (snapshot == null) return;
            int echoes = SavedRunEchoes(snapshot);
            MetaProgression.AddEchoes(echoes);
            MetaProgression.NoteRunEnded(RunSnapshot.DepthOf(snapshot), won: false);
            MetaProgression.Save();
            // Skips EndRun, so log it here; counters belong to a prior session, sector is not in the snapshot (-1).
            RunLog.Append(new RunLogEntry
            {
                utc = DateTime.UtcNow.ToString("o"), seed = snapshot.seed, won = false,
                depth = RunSnapshot.DepthOf(snapshot), kills = snapshot.runKills, boons = snapshot.boons.Count,
                echoes = echoes, boonIds = snapshot.boons.ToArray(), deathBy = RunLog.AbandonedMarker,
                build = BuildTag(), evt = "end", outcome = RunLog.AbandonedMarker, sector = -1,
            });
            RunSnapshot.Delete();
        }

        /// <summary>P4-11: what abandoning a saved run banks: the loss result for its depth, scaled by its heat (EchoGain is not re-derived).</summary>
        internal static int SavedRunEchoes(RunSnapshot snapshot)
        {
            int heat = 0;
            foreach (int r in snapshot.heatRanks) heat += r;
            return ApplyHeatEchoes(RunScaling.EchoResult(RunSnapshot.DepthOf(snapshot), false), heat);
        }

        private static uint GenerateSeed() => (uint)UnityEngine.Random.Range(1, int.MaxValue);

        /// <summary>
        /// Seeds <see cref="RunState"/> from the player's <see cref="MetaProgression"/> upgrades.
        /// RunState has no dedicated flat-HP/flat-damage fields, so — per the design doc's reuse
        /// mandate ("Stat boons drive the existing PlayerCombatModifiers slots and Health") — the flat
        /// HP/damage upgrades are granted as extra stacks of the matching tier-1 stat boons
        /// (Ironskin/KeenEdge) rather than inventing a second stat channel; the boon-aggregation
        /// pipeline (Module 2/3) is the only thing downstream that turns RunState.Boons into numbers
        /// on the rig, so it must be the same channel for both meta-earned and in-run stat bonuses.
        /// </summary>
        private void ApplyStartingBonuses()
        {
            var rng = RunRng.ForNode(RunState.Seed, StartingBonusRngNode);

            int healthLevel = MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth);
            GrantBoonStacks(BoonId.Ironskin, healthLevel);

            int damageLevel = MetaProgression.UpgradeLevel(MetaUpgradeId.StartingDamage);
            GrantBoonStacks(BoonId.KeenEdge, damageLevel);

            GrantStartingBoons(MetaProgression.UpgradeLevel(MetaUpgradeId.StartingBoon), ref rng);

            int rerollLevel = MetaProgression.UpgradeLevel(MetaUpgradeId.RerollTokens);
            if (rerollLevel > 0) RunState.AddRerollToken(rerollLevel);

            ApplyMetaArts();
            ApplyKeepsake(boonCatalog);
        }

        /// <summary>P4-10: the Shrine's Art picks go into this run's slots ("" for an unbought slot).</summary>
        internal static void ApplyMetaArts()
        {
            ArtSlots.Equip(ArtSlot.Weapon, MetaProgression.EquippedArt(true));
            ArtSlots.Equip(ArtSlot.Offhand, MetaProgression.EquippedArt(false));
        }

        /// <summary>P4-10 Keepsake: the run starts holding the chosen past Epic. Like the other starting
        /// bonuses it is not a pick: no BoonChosen, and it is not re-recorded as a past Epic.</summary>
        internal static void ApplyKeepsake(BoonCatalog catalog)
        {
            string id = MetaProgression.SelectedKeepsake;
            var def = catalog != null && id.Length > 0 ? catalog.Find(id) : null;
            if (def == null) return;
            RunState.AddBoon(def.id);
            if (def.effect == BoonEffectKind.GrantAbility) RunState.GrantAbility(def.abilityId);
        }

        /// <summary>Grants up to <paramref name="level"/> stacks of a meta-upgrade boon, clamped to the
        /// catalog's authored maxStacks (A7.6: RunState.BoonCount is uncapped while BoonInventory.Add
        /// enforces maxStacks — granting past it desyncs the two, and since BoonOfferPicker reads the
        /// uncapped RunState.BoonCount, that silently drops the boon out of the offer pool for the rest
        /// of the run). See <see cref="StartingStacks"/>.</summary>
        private void GrantBoonStacks(string boonId, int level)
        {
            var def = boonCatalog != null ? boonCatalog.Find(boonId) : null;
            int cap = def != null ? Mathf.Max(1, def.maxStacks) : level;
            int stacks = StartingStacks(level, cap);
            for (int i = 0; i < stacks; i++) RunState.AddBoon(boonId);
        }

        /// <summary>Pure clamp: never grant more starting stacks than a boon's authored cap allows.
        /// A7.10 test seam (would have caught A7.6 immediately).</summary>
        internal static int StartingStacks(int upgradeLevel, int maxStacks) =>
            Mathf.Clamp(upgradeLevel, 0, Mathf.Max(0, maxStacks));

        /// <summary>A7.9: picks <paramref name="count"/> distinct Common boons via <see cref="RunRng"/>
        /// rather than always taking the first N in catalog array order (which made every run's starting
        /// boons identical).</summary>
        private void GrantStartingBoons(int count, ref RunRng rng)
        {
            if (boonCatalog == null || boonCatalog.boons == null)
            {
                Debug.LogError("[RunDirector] No BoonCatalog assigned; cannot grant starting boons.");
                return;
            }
            if (count <= 0) return;

            var pool = new List<BoonDefinition>();
            foreach (var boon in boonCatalog.boons)
                if (boon != null && boon.rarity == BoonRarity.Common && BoonOfferPicker.IsPackUnlocked(boon)) pool.Add(boon); // P4-05 pack locks

            int granted = 0;
            while (granted < count && pool.Count > 0)
            {
                int index = rng.NextInt(pool.Count);
                RunState.AddBoon(pool[index].id);
                pool.RemoveAt(index);
                granted++;
            }
        }

        private void OnRoomCleared(RoomCleared evt)
        {
            if (ShouldDeferClear(awaitingRigRestore, evt.NodeIndex == RunState.NodeIndex))
            {
                deferredClear = evt;
                return;
            }
            OnRoomClearedNow(evt);
        }

        /// <summary>BUG-02: defer a clear iff it belongs to the node being loaded and the rig has not
        /// had the run's HP restored yet. Pure. A7.10 test seam.</summary>
        internal static bool ShouldDeferClear(bool awaitingRigRestore, bool sameNode) =>
            awaitingRigRestore && sameNode;

        private void OnRoomClearedNow(RoomCleared evt)
        {
            if (!RunState.InRun || evt.NodeIndex != RunState.NodeIndex) return;
            if (evt.NodeIndex == lastClearedNode) return; // A7.9: idempotence — no double echoes/offer
            lastClearedNode = evt.NodeIndex;

            var node = RunState.CurrentNode;
            // P4-11 (D-11): rooms pay Shards (run currency). EchoGain + heat now scale only the run-end Echo result (EndRun).
            RunState.AddShards(RunScaling.ShardReward(node.Depth, node.Kind));

            bool isFinalNode = RunState.NextChoices.Count == 0;
            switch (ResolvePostClear(node.Kind, isFinalNode))
            {
                case PostClearAction.WinRun:
                    EndRun(won: true);
                    break;
                case PostClearAction.OfferBoon:
                    BeginBoonOffer(node);
                    break;
                case PostClearAction.AdvanceImmediately:
                    AdvanceNode();
                    break;
                case PostClearAction.OpenForge:
                    OpenForge();
                    break;
                case PostClearAction.OpenTreasure:
                    OpenTreasure(node);
                    break;
                case PostClearAction.OpenShop:
                    OpenShop(node);
                    break;
                case PostClearAction.OpenEvent:
                    OpenEvent(node);
                    break;
            }
        }

        // ---- P2-02 Forge / Treasure reveal cards (shown before the exit gates) ----

        private void OpenForge()
        {
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "FORGE", "Choose one.", 2);
            if (choicePanel == null)
            {
                Debug.LogWarning("[RunDirector] Forge panel could not be built (no camera/EventSystem); applying MEND and moving on.");
                if (!MendCursed) ApplyForgeMend();
                AdvanceNode();
                return;
            }
            panelMode = PanelMode.Forge;
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
            // BUG-30: show what each card does now — MEND heals 0 at full HP, so it says so and is disabled.
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            string mend = MendCursed ? ForgeMendCursed : health != null ? ForgeMendLabel(health.Current, health.Max) : ForgeHpFull;
            choicePanel.SetOption(0, "MEND", RoomKindText.Color(RoomKind.Forge), RunState.HeatRankOf(HeatRank.RustedForge) > 0 ? "Heal 30% of your missing health." : "Heal 60% of your missing health.", mend, mend != ForgeHpFull && mend != ForgeMendCursed);
            // P4-07: TEMPER opens a boon picker; disabled when nothing owned is eligible.
            bool canTemper = boonInventory.TemperCandidates().Count > 0;
            choicePanel.SetOption(1, "TEMPER", RoomKindText.Color(RoomKind.Forge), "Strengthen one owned boon by 50%.",
                canTemper ? "PICK A BOON" : ForgeTemperNone, canTemper);
        }

        internal const string ForgeTemperNone = "NO BOON";
        private List<BoonDefinition> temperOptions;

        /// <summary>P4-07: up to <paramref name="count"/> distinct random candidates. Pure.</summary>
        internal static List<BoonDefinition> PickTemperOptions(List<BoonDefinition> candidates, ref RunRng rng, int count)
        {
            var pool = new List<BoonDefinition>(candidates);
            var picks = new List<BoonDefinition>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = rng.NextInt(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }

        private void OpenTemperPicker()
        {
            var candidates = boonInventory.TemperCandidates();
            var rng = RunRng.ForNode(RunState.Seed, RunState.NodeIndex);
            var options = PickTemperOptions(candidates, ref rng, 3);
            if (options.Count == 0) return;
            DismissChoicePanel();
            temperOptions = options;
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "TEMPER", "Choose a boon to strengthen (+50%).", options.Count, "BACK");
            if (choicePanel == null) { temperOptions = null; OpenForge(); return; }
            panelMode = PanelMode.Temper;
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
            choicePanel.FooterSelected += OnChoicePanelFooter;
            for (int i = 0; i < options.Count; i++)
            {
                var b = options[i];
                choicePanel.SetOption(i, BoonOfferPanel.FormatChoiceTitle(b), BoonOfferPanel.RarityColor(b.rarity), b.description, "+50% POWER", true);
            }
        }

        private void ApplyTemper(int index)
        {
            if (temperOptions == null || index < 0 || index >= temperOptions.Count) return;
            string id = temperOptions[index].id;
            if (!boonInventory.CanTemper(id)) return;
            temperOptions = null;
            RunState.AddTemper(id);
            boonInventory.SetTemper(id, RunState.TemperLevel(id));
            var modifiers = VRRig.Instance != null ? VRRig.Instance.GetComponent<PlayerCombatModifiers>() : null;
            if (modifiers != null) PushBoonStats(modifiers, boonInventory); // max-HP changes land on the next node load
            LeaveRoom();
        }

        internal const string ForgeHpFull = "HP FULL";

        /// <summary>P4-05 Blood Price: MEND is disabled for the rest of the run.</summary>
        internal const string ForgeMendCursed = "BLOOD PRICE";

        private bool MendCursed => boonInventory.Bonus(BoonEffectKind.ForgeMend) < 0f;

        /// <summary>BUG-30: MEND card sub-label — the whole HP the HUD will gain ("+36 HP"), or
        /// "HP FULL" when MEND would heal nothing. Uses the HUD's ceil rounding. Pure.</summary>
        internal static string ForgeMendLabel(float current, float max)
        {
            int gain = Mathf.CeilToInt(ForgeResultHealth(current, max)) - Mathf.CeilToInt(current);
            return gain > 0 ? "+" + gain + " HP" : ForgeHpFull;
        }

        private void ChooseForge(int index)
        {
            if (index == 0) { if (MendCursed) return; ApplyForgeMend(); }
            else if (index == 1) { OpenTemperPicker(); return; }
            else return;
            LeaveRoom();
        }

        /// <summary>MEND: heal 60% of missing HP (against the durable stored HP, not a full-HP
        /// assumption — A4.2/A7.3).</summary>
        private void ApplyForgeMend()
        {
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health == null) return;
            health.SetCurrent(ForgeResultHealth(health.Current, health.Max));
            RunState.NotePlayerHealth(health.Current); // re-store the post-heal value
        }

        /// <summary>HP after a Forge: carried HP plus 60% of what is missing, clamped to max.
        /// Pure. BUG-02 test seam.</summary>
        internal static float ForgeResultHealth(float stored, float max) =>
            Mathf.Clamp(stored + RunState.HealAmountForForge(stored, max), 0f, max);

        /// <summary>
        /// Treasure: 1 guaranteed boon behind a single TAKE card. Reuses <see cref="BoonOfferPicker"/>
        /// with <see cref="RoomKind.Treasure"/> and count 1 — its RarityWeight already excludes Common
        /// for Treasure, so this is the single source of truth for what "Treasure-tier" means.
        /// </summary>
        private void OpenTreasure(RunNode node)
        {
            treasureBoon = null;
            if (boonCatalog == null || boonCatalog.boons == null)
            {
                Debug.LogError("[RunDirector] No BoonCatalog assigned; cannot grant the Treasure boon.");
                AdvanceNode();
                return;
            }
            var rng = RunRng.ForNode(RunState.Seed, node.Index);
            var picks = BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, RoomKind.Treasure, ref rng, count: 1, luck: Luck);
            if (picks.Count == 0)
            {
                AdvanceNode(); // pool exhausted; nothing left to grant this run
                return;
            }
            treasureBoon = picks[0];
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "TREASURE", "A gift from the Fracture.", 1);
            if (choicePanel == null)
            {
                Debug.LogWarning("[RunDirector] Treasure panel could not be built (no camera/EventSystem); granting the boon directly.");
                GrantBoon(treasureBoon);
                AdvanceNode();
                return;
            }
            panelMode = PanelMode.Treasure;
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
            choicePanel.SetOption(0, BoonOfferPanel.FormatChoiceTitle(treasureBoon), BoonOfferPanel.RarityColor(treasureBoon.rarity),
                treasureBoon.description, "TAKE", true);
        }

        private void TakeTreasure()
        {
            if (treasureBoon == null) return;
            var boon = treasureBoon;
            treasureBoon = null;
            if (choicePanel != null)
                EventBus.Publish(new TreasureOpened(choicePanel.transform.position, BoonOfferPanel.RarityColor(boon.rarity)));
            GrantBoon(boon);
            LeaveRoom();
        }

        /// <summary>
        /// Records a boon as held (RunState + BoonChosen) and, per A7.7, grants a GrantAbility-effect
        /// boon's ability for this run only — never <see cref="CampaignState.UnlockAbility"/>, which is
        /// permanent and would otherwise let a single run pick unlock a story ability in the player's
        /// campaign save forever. Shared by the two acquisition paths that grant one specific boon
        /// outright (an offer pick, a Treasure grant). ApplyStartingBonuses deliberately calls
        /// RunState.AddBoon directly instead: a meta-upgrade's starting boons aren't something the
        /// player "chose" this run, so they shouldn't republish BoonChosen (and Common-tier starting
        /// boons never carry a GrantAbility effect anyway).
        /// </summary>
        private void GrantBoon(BoonDefinition boon)
        {
            RunState.AddBoon(boon.id);
            // P4-01: stat slots take effect on the pick itself; max HP still applies from the next load.
            boonInventory.Add(boon);
            var modifiers = VRRig.Instance != null ? VRRig.Instance.GetComponent<PlayerCombatModifiers>() : null;
            if (modifiers != null) PushBoonStats(modifiers, boonInventory);
            // P4-05: a pick's own max-HP change and heal land now (Ironskin: +10 max AND heal 10), so a full-HP
            // player is not left 10 short; the next load rebuilds max from the inventory as usual.
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            float addMax = BoonInventory.Magnitude(boon, BoonEffectKind.MaxHealthAdd);
            float heal = BoonInventory.Magnitude(boon, BoonEffectKind.HealOnPick);
            if (health != null && health.IsAlive && (addMax != 0f || heal > 0f))
            {
                float current = health.Current;
                if (addMax != 0f) health.Configure(Mathf.Max(1f, health.Max + addMax));
                health.SetCurrent(Mathf.Max(1f, current + Mathf.Max(0f, heal)));
            }
            EventBus.Publish(new BoonChosen(boon.id));
            if (boon.effect == BoonEffectKind.GrantAbility) RunState.GrantAbility(boon.abilityId);
            if (boon.rarity == BoonRarity.Epic) MetaProgression.NoteEpicPicked(boon.id); // P4-10 Keepsake candidates
        }

        private void BeginBoonOffer(RunNode node)
        {
            DismissOfferPanel(); // A7.9: idempotence — never orphan a live canvas with a subscribed handler
            offerNode = node;
            offerRng = RunRng.ForNode(RunState.Seed, node.Index);
            offerChoices = PickBoonChoices(node.Kind);

            switch (ResolveOffer(offerChoices.Count, Camera.main != null, EventSystem.current != null))
            {
                case OfferResolution.AutoAdvanceNoChoices:
                    // Every boon in the pool is already at max stacks — nothing to offer; don't stall.
                    AfterBoonOffer();
                    return;
                case OfferResolution.AutoAdvanceNoEventSystem:
                    // A7.2/A7.8: without an EventSystem the panel's buttons can never be clicked — a
                    // headset-level soft-lock, not a cosmetic gap. Loud on purpose.
                    Debug.LogError("[RunDirector] No EventSystem in the arena scene; the boon offer panel would be unclickable. Auto-advancing without a choice.");
                    AdvanceNode();
                    return;
                case OfferResolution.AutoAdvanceNoCamera:
                    Debug.LogWarning("[RunDirector] No camera to anchor the boon offer panel; auto-advancing.");
                    AdvanceNode();
                    return;
            }

            offerPanel = BoonOfferPanelFactory.Build(Camera.main, offerChoices, RunState.RerollTokens > 0);
            if (offerPanel == null)
            {
                // Shouldn't happen given the ResolveOffer check above, but never stall on it either way.
                Debug.LogWarning("[RunDirector] Boon offer panel failed to build; auto-advancing.");
                AdvanceNode();
                return;
            }
            offerPanel.SetRerollTokens(RunState.RerollTokens);
            offerPanel.ChoiceSelected += OnBoonChoiceSelected;
            offerPanel.RerollRequested += OnRerollRequested;
            offerFallbackDeadline = Time.unscaledTime + BoonOfferFallbackSeconds;
        }

        /// <summary>What the offer should show, given the pure conditions that decide it. A7.2's
        /// soft-lock fix (no EventSystem / no camera / no choices all auto-advance) as a pure decision,
        /// so it is unit-testable without a scene. A7.10 test seam.</summary>
        internal enum OfferResolution { ShowPanel, AutoAdvanceNoChoices, AutoAdvanceNoEventSystem, AutoAdvanceNoCamera }

        internal static OfferResolution ResolveOffer(int choiceCount, bool hasCamera, bool hasEventSystem)
        {
            if (choiceCount <= 0) return OfferResolution.AutoAdvanceNoChoices;
            if (!hasEventSystem) return OfferResolution.AutoAdvanceNoEventSystem;
            if (!hasCamera) return OfferResolution.AutoAdvanceNoCamera;
            return OfferResolution.ShowPanel;
        }

        private List<BoonDefinition> PickBoonChoices(RoomKind kind)
        {
            if (boonCatalog == null || boonCatalog.boons == null)
            {
                Debug.LogError("[RunDirector] No BoonCatalog assigned; cannot offer boons.");
                return new List<BoonDefinition>();
            }
            return BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, kind, ref offerRng, 3, Luck);
        }

        /// <summary>Pure decision: can this index actually be honoured against the live offer? A7.10
        /// test seam, same idiom as <see cref="ResolveOffer"/> / <see cref="ShouldHealOnKill"/>. The
        /// caller must dismiss+advance either way — this only decides whether a boon is granted.</summary>
        internal static bool CanHonourChoice(IReadOnlyList<BoonDefinition> choices, int index) =>
            choices != null && index >= 0 && index < choices.Count;

        /// <summary>
        /// A7.2: this must ALWAYS dismiss the panel and advance, even when the index cannot be
        /// honoured. It used to `return` silently on a bad index, which was a permanent soft-lock:
        /// <see cref="offerFallbackDeadline"/> is cleared only by <see cref="DismissOfferPanel"/>, so
        /// the early return left the deadline armed and <see cref="Update"/> re-entered this method
        /// every frame forever — panel never removed, run never advanced, warning spamming the log.
        /// That is exactly the "stuck on the boon panel" failure. A skipped boon is the safe failure;
        /// a stuck run is not (same principle as A5.1's "clearing is the safe failure").
        /// </summary>
        private void OnBoonChoiceSelected(int index)
        {
            if (offerChoices != null)
            {
                var offeredIds = new List<string>();
                foreach (var b in offerChoices) offeredIds.Add(b.id);
                RunTally.NoteOffer(offeredIds, CanHonourChoice(offerChoices, index) ? offerChoices[index].id : "");
            }
            if (CanHonourChoice(offerChoices, index))
            {
                GrantBoon(offerChoices[index]);
            }
            else
            {
                Debug.LogError($"[RunDirector] Boon choice {index} is not valid for the current offer " +
                               $"({(offerChoices == null ? "null" : offerChoices.Count.ToString())} choices). " +
                               "Advancing without a boon rather than stalling the run.");
            }
            DismissOfferPanel();
            AfterBoonOffer();
        }

        /// <summary>P2-02: after a sector-0/1 Boss boon the sector card comes first; otherwise the gates.</summary>
        private void AfterBoonOffer()
        {
            if (ShouldShowSectorCard(offerNode.Kind, offerNode.Sector)) ShowSectorCard(offerNode.Sector + 1);
            else AdvanceNode();
        }

        /// <summary>Pure: the sector card follows the Boss of every sector but the last (that one wins the run).</summary>
        internal static bool ShouldShowSectorCard(RoomKind kind, int sector) =>
            kind == RoomKind.Boss && sector < RunMapGenerator.SectorCount - 1;

        /// <summary>Heals 20% of missing HP, shows the sector title for 2.5 s, then CONTINUE / SAVE &amp; QUIT.</summary>
        private void ShowSectorCard(int newSector)
        {
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health != null)
            {
                health.SetCurrent(Mathf.Clamp(health.Current + RunState.HealAmountForSector(health.Current, health.Max), 0f, health.Max));
                RunState.NotePlayerHealth(health.Current);
            }
            RunTitleCard.Show(Camera.main, SectorText.Title(newSector), SectorText.Color(newSector), SectorTitleSeconds);
            StartCoroutine(DelayThen(SectorTitleSeconds, ShowSectorPanel));
        }

        private void ShowSectorPanel()
        {
            if (!RunState.InRun || EndingRun) return; // died or suspended during the title
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "CHECKPOINT", "20% of your missing health restored.", 0, "CONTINUE", "SAVE & QUIT");
            if (choicePanel == null)
            {
                Debug.LogWarning("[RunDirector] Sector panel could not be built (no camera/EventSystem); continuing.");
                AdvanceNode();
                return;
            }
            panelMode = PanelMode.Sector;
            choicePanel.FooterSelected += OnChoicePanelFooter;
        }

        /// <summary>SAVE &amp; QUIT at the sector card: a Boss node's only out-edge is the next sector's
        /// first node, so take it exactly as CONTINUE would (Path/Depth stay valid) and snapshot the entry
        /// with the healed HP already noted by <see cref="ShowSectorCard"/>. False (nothing moved) otherwise.</summary>
        internal bool CheckpointAdvance()
        {
            var next = RunState.NextChoices;
            if (next.Count != 1 || !RunState.Advance(next[0])) return false;
            WriteRunSnapshot();
            return true;
        }

        private static IEnumerator DelayThen(float seconds, Action then)
        {
            yield return new WaitForSecondsRealtime(seconds);
            then();
        }

        /// <summary>
        /// A1.6: <see cref="BoonOfferPicker.Pick"/> can legitimately return ZERO items from a non-empty
        /// pool, and its doc comment requires every caller to auto-advance on that rather than block.
        /// <see cref="BeginBoonOffer"/> honours it via <see cref="ResolveOffer"/>; this path did not —
        /// a reroll that came back empty called SetChoices(0), which hides every choice button, leaving
        /// a panel with nothing clickable (and no reroll left once the tokens are spent). Auto-advance
        /// instead, same as the initial offer does.
        /// </summary>
        private void OnRerollRequested()
        {
            if (!RunState.TrySpendRerollToken()) return;
            offerChoices = PickBoonChoices(offerNode.Kind);
            if (offerChoices.Count == 0)
            {
                Debug.LogWarning("[RunDirector] Reroll left no offerable boons (A1.6); advancing without a choice rather than showing an unclickable panel.");
                DismissOfferPanel();
                AfterBoonOffer();
                return;
            }
            if (offerPanel == null) return;
            offerPanel.SetChoices(offerChoices);
            offerPanel.SetRerollTokens(RunState.RerollTokens);
        }

        /// <summary>
        /// Build the post-run summary on the menu we just loaded. Mirrors BeginBoonOffer's guards:
        /// no camera or no EventSystem means the panel would be unclickable, so skip it rather than
        /// park a dead canvas in front of the menu buttons. Never fatal — the summary is a report,
        /// and the menu behind it is fully usable without it.
        /// </summary>
        private void ShowRunSummary()
        {
            if (!RunSummary.HasResult) return;
            DismissRunSummary(); // idempotence: never orphan a subscribed canvas

            summaryPanel = RunSummaryPanelFactory.Build(Camera.main);
            if (summaryPanel == null)
            {
                Debug.LogWarning("[RunDirector] No camera/EventSystem on the run-end scene; skipping the run summary.");
                RunSummary.Clear();
                return;
            }
            summaryPanel.Dismissed += OnRunSummaryDismissed;
            // Consume the result now: a later Return-to-Menu must not resurrect this same summary.
            RunSummary.Clear();
        }

        private void OnRunSummaryDismissed() => DismissRunSummary();

        private void DismissRunSummary()
        {
            if (summaryPanel == null) return;
            summaryPanel.Dismissed -= OnRunSummaryDismissed;
            Destroy(summaryPanel.gameObject);
            summaryPanel = null;
        }

        /// <summary>
        /// P0-03: while the run is paused the boon offer must neither auto-pick (the 60 s fallback runs
        /// on unscaled time, so it keeps counting through a pause) nor be clickable behind the pause
        /// panel. Freezing stores the remaining time and hides the panel; thawing re-arms both.
        /// </summary>
        public void FreezeOfferTimer(bool freeze)
        {
            if (freeze)
            {
                if (offerFallbackDeadline >= 0f)
                {
                    frozenOfferSeconds = RemainingOfferSeconds(offerFallbackDeadline, Time.unscaledTime);
                    offerFallbackDeadline = -1f;
                }
            }
            else if (frozenOfferSeconds >= 0f)
            {
                offerFallbackDeadline = Time.unscaledTime + frozenOfferSeconds;
                frozenOfferSeconds = -1f;
            }
            if (offerPanel != null) offerPanel.gameObject.SetActive(!freeze);
            if (choicePanel != null) choicePanel.gameObject.SetActive(!freeze);
        }

        /// <summary>Seconds left before the offer auto-picks, never negative. Pure. P0-03 test seam.</summary>
        internal static float RemainingOfferSeconds(float deadline, float now) => Mathf.Max(0f, deadline - now);

        private void DismissOfferPanel()
        {
            offerFallbackDeadline = -1f;
            frozenOfferSeconds = -1f;
            if (offerPanel == null) return;
            offerPanel.ChoiceSelected -= OnBoonChoiceSelected;
            offerPanel.RerollRequested -= OnRerollRequested;
            Destroy(offerPanel.gameObject);
            offerPanel = null;
        }

        /// <summary>P2-04: the node's reward is resolved — let the player pick the next node. Raises the
        /// exit gates (via <see cref="PathChoiceOffered"/>, the arena builds them) and, when seated, the
        /// CHOOSE YOUR PATH panel straight away; otherwise the panel follows after
        /// <see cref="PathPanelStillSeconds"/> without moving.</summary>
        private void AdvanceNode()
        {
            var next = RunState.NextChoices;
            if (next.Count == 0)
            {
                // Safety net: the final Boss is normally caught by ResolvePostClear (WinRun) first.
                EndRun(won: true);
                return;
            }
            pathChoices = new int[next.Count];
            for (int i = 0; i < pathChoices.Length; i++) pathChoices[i] = next[i];
            stillSeconds = 0f;
            stillAnchor = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            EventBus.Publish(new PathChoiceOffered(pathChoices));
            if (ShouldShowPathPanel(GameSettings.Active.Seated, 0f)) ShowPathPanel();
        }

        /// <summary>Pure: the panel shows when seated, or once the player has stood still long enough.</summary>
        internal static bool ShouldShowPathPanel(bool seated, float stillSeconds) =>
            seated || stillSeconds >= PathPanelStillSeconds;

        private void TickPathStillness()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = cam.transform.position - stillAnchor;
            d.y = 0f;
            if (d.sqrMagnitude > PathStillRadius * PathStillRadius)
            {
                stillAnchor = cam.transform.position;
                stillSeconds = 0f;
                return;
            }
            stillSeconds += Time.deltaTime; // scaled: a paused run does not count as standing still
            if (ShouldShowPathPanel(false, stillSeconds)) ShowPathPanel();
        }

        private void ShowPathPanel()
        {
            DismissChoicePanel();
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "CHOOSE YOUR PATH",
                "Walk through an arch, or point and squeeze grip.", pathChoices.Length);
            if (choicePanel == null)
            {
                if (GameSettings.Active.Seated)
                {
                    // Seated with no clickable panel: never strand the run.
                    Debug.LogWarning("[RunDirector] Path panel could not be built while seated; taking the first path.");
                    AdvanceTo(pathChoices[0]);
                }
                else stillSeconds = float.NegativeInfinity; // gates still work; don't retry every frame
                return;
            }
            panelMode = PanelMode.Path;
            for (int i = 0; i < pathChoices.Length; i++)
            {
                RoomKind kind = RunState.Map[pathChoices[i]].Kind;
                choicePanel.SetOption(i, RoomKindText.Name(kind), RoomKindText.Color(kind), RoomKindText.Hint(kind), string.Empty, true);
            }
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
        }

        private void OnPathChosen(PathChosen evt)
        {
            if (pathChoices == null || Array.IndexOf(pathChoices, evt.NodeId) < 0) return;
            AdvanceTo(evt.NodeId);
        }

        private void AdvanceTo(int nodeId)
        {
            pathChoices = null;
            DismissChoicePanel();
            NoteCurrentPlayerHealth();
            if (!RunState.Advance(nodeId))
            {
                Debug.LogError($"[RunDirector] Node {nodeId} is not reachable from {RunState.NodeIndex}; ignoring the pick.");
                return;
            }
            WriteRunSnapshot();
            StartSceneLoad(runArenaScene);
        }

        private void OnChoicePanelSelected(int index)
        {
            switch (panelMode)
            {
                case PanelMode.Path:
                    if (pathChoices != null && index >= 0 && index < pathChoices.Length) AdvanceTo(pathChoices[index]);
                    break;
                case PanelMode.Shop:
                    BuyShopBoon(index);
                    break;
                case PanelMode.Event:
                    ChooseEventOption(index);
                    break;
                case PanelMode.Forge:
                    ChooseForge(index);
                    break;
                case PanelMode.Treasure:
                    TakeTreasure();
                    break;
                case PanelMode.Temper:
                    ApplyTemper(index);
                    break;
            }
        }

        private void OnChoicePanelFooter(int index)
        {
            if (panelMode == PanelMode.Shop)
            {
                if (index == 0) BuyMend();
                else LeaveRoom();
            }
            else if (panelMode == PanelMode.Event) LeaveRoom();
            else if (panelMode == PanelMode.Temper) { temperOptions = null; DismissChoicePanel(); OpenForge(); }
            else if (panelMode == PanelMode.Sector)
            {
                if (index == 0) LeaveRoom();
                else
                {
                    CheckpointAdvance(); // resume at the next sector's entry, not the boss
                    SuspendRun();
                }
            }
        }

        private void LeaveRoom()
        {
            DismissChoicePanel();
            AdvanceNode();
        }

        private void DismissChoicePanel()
        {
            panelMode = PanelMode.None;
            if (choicePanel == null) return;
            choicePanel.ChoiceSelected -= OnChoicePanelSelected;
            choicePanel.FooterSelected -= OnChoicePanelFooter;
            Destroy(choicePanel.gameObject);
            choicePanel = null;
        }

        // ---- P2-04 Shop: spend THIS run's Shards (P4-11; BUG-33: never Shrine Echoes) ----

        /// <summary>Shop boon price in Shards by rarity: Common 30, Rare 60, Epic 120.</summary>
        internal static int ShopPrice(BoonRarity rarity) => rarity >= BoonRarity.Epic ? 120 : rarity == BoonRarity.Rare ? 60 : 30;

        /// <summary>HP after MEND 25%: a quarter of max HP, clamped to max.</summary>
        internal static float MendResult(float current, float max) => Mathf.Min(max, current + 0.25f * max);

        private void OpenShop(RunNode node)
        {
            var rng = RunRng.ForNode(RunState.Seed, node.Index);
            shopStock = boonCatalog != null && boonCatalog.boons != null
                ? BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, RoomKind.Shop, ref rng, 3, Luck)
                : new List<BoonDefinition>();
            shopSold = new bool[shopStock.Count];
            mendSold = false;
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, "SHOP", null, shopStock.Count, "MEND", "LEAVE");
            if (choicePanel == null)
            {
                Debug.LogWarning("[RunDirector] Shop panel could not be built (no camera/EventSystem); skipping the shop.");
                AdvanceNode();
                return;
            }
            panelMode = PanelMode.Shop;
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
            choicePanel.FooterSelected += OnChoicePanelFooter;
            RefreshShop();
        }

        private void RefreshShop()
        {
            if (choicePanel == null) return;
            int shards = RunState.Shards;
            choicePanel.SetTitle("SHOP · " + RunHudPanel.ShardGlyph + " " + shards + " SHARDS", "Shards are this run's currency; they are lost when the run ends.");
            for (int i = 0; i < shopStock.Count; i++)
            {
                var b = shopStock[i];
                int price = ShopPrice(b.rarity);
                choicePanel.SetOption(i, BoonOfferPanel.FormatChoiceTitle(b), BoonOfferPanel.RarityColor(b.rarity), b.description,
                    shopSold[i] ? "SOLD" : RunHudPanel.ShardGlyph + " " + price, !shopSold[i] && shards >= price);
            }
            choicePanel.SetFooter(0, mendSold ? "MEND · SOLD" : "MEND 25% · " + RunHudPanel.ShardGlyph + " " + MendPrice, true, !mendSold && shards >= MendPrice);
        }

        private void BuyShopBoon(int index)
        {
            if (shopStock == null || index < 0 || index >= shopStock.Count || shopSold[index]) return;
            if (!RunState.TrySpendShards(ShopPrice(shopStock[index].rarity))) return;
            shopSold[index] = true;
            GrantBoon(shopStock[index]); // stat slots apply now; max HP from the next node load, like every pick
            RefreshShop();
        }

        private void BuyMend()
        {
            if (mendSold || !RunState.TrySpendShards(MendPrice)) return;
            mendSold = true;
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health != null)
            {
                health.SetCurrent(MendResult(health.Current, health.Max));
                RunState.NotePlayerHealth(health.Current);
            }
            RefreshShop();
        }

        // ---- P2-04 Event: a 2-option data event ----

        private void OpenEvent(RunNode node)
        {
            eventRng = RunRng.ForNode(RunState.Seed, node.Index);
            activeEvent = runEvents != null && runEvents.Length > 0 ? runEvents[eventRng.NextInt(runEvents.Length)] : null;
            if (activeEvent == null)
            {
                Debug.LogWarning("[RunDirector] No RunEventDefinitions assigned; skipping the event.");
                AdvanceNode();
                return;
            }
            choicePanel = BoonOfferPanelFactory.BuildCustom(Camera.main, activeEvent.title.ToUpperInvariant(), activeEvent.body, 2, "CONTINUE");
            if (choicePanel == null)
            {
                Debug.LogWarning("[RunDirector] Event panel could not be built (no camera/EventSystem); skipping the event.");
                AdvanceNode();
                return;
            }
            panelMode = PanelMode.Event;
            choicePanel.ChoiceSelected += OnChoicePanelSelected;
            choicePanel.FooterSelected += OnChoicePanelFooter;
            choicePanel.SetFooter(0, "CONTINUE", false);
            SetEventOption(0, activeEvent.optionA);
            SetEventOption(1, activeEvent.optionB);
        }

        private void SetEventOption(int i, RunEventOption o)
        {
            int cost = RunEventResolver.ShardCost(o);
            bool afford = RunEventResolver.CanAfford(o, RunState.Shards);
            string sub = cost > 0 ? (afford ? RunHudPanel.ShardGlyph + " " + cost : "NEED " + RunHudPanel.ShardGlyph + " " + cost) : string.Empty;
            choicePanel.SetOption(i, o.label, Color.white, o.hint, sub, afford);
        }

        private void ChooseEventOption(int index)
        {
            if (activeEvent == null || index < 0 || index > 1) return;
            var option = index == 0 ? activeEvent.optionA : activeEvent.optionB;
            if (!RunEventResolver.CanAfford(option, RunState.Shards)) return;
            var outcome = RunEventResolver.Resolve(option, ref eventRng);
            string result = ApplyEventOutcome(outcome);
            activeEvent = null;
            choicePanel.ShowOptions(0);
            choicePanel.SetTitle(option.label.ToUpperInvariant(), result);
            choicePanel.SetFooter(0, "CONTINUE", true);
        }

        /// <summary>Applies an event outcome to the run and the live rig; returns the line shown to the player.</summary>
        private string ApplyEventOutcome(RunEventOutcome o)
        {
            var parts = new List<string>();
            if (o.ShardsSpent > 0 && RunState.TrySpendShards(o.ShardsSpent)) parts.Add("-" + o.ShardsSpent + " " + RunHudPanel.ShardGlyph);
            if (o.Rerolls != 0) { RunState.AddRerollToken(o.Rerolls); parts.Add("+" + o.Rerolls + " reroll"); }
            if (o.MaxHealthDelta != 0f)
            {
                RunState.AddMaxHealthBonus(o.MaxHealthDelta);
                parts.Add((o.MaxHealthDelta > 0f ? "+" : "") + o.MaxHealthDelta + " max HP");
            }
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health != null && (o.MaxHealthDelta != 0f || o.HealFraction > 0f || o.Damage > 0f))
            {
                RunEventResolver.ApplyHealth(health.Current, health.Max, o, out float newMax, out float newCurrent);
                if (!Mathf.Approximately(newMax, health.Max)) health.Configure(newMax); // Configure refills; SetCurrent below restores
                health.SetCurrent(newCurrent);
                RunState.NotePlayerHealth(health.Current);
            }
            if (o.HealFraction > 0f) parts.Add("healed " + Mathf.RoundToInt(o.HealFraction * 100f) + "%");
            if (o.Damage > 0f) parts.Add("-" + o.Damage + " HP");
            foreach (var rarity in o.Boons)
            {
                var boon = PickEventBoon(rarity);
                if (boon == null) continue;
                GrantBoon(boon);
                parts.Add(BoonOfferPanel.TierWord(boon.rarity) + " · " + boon.displayName);
            }
            return parts.Count == 0 ? "Nothing happens." : string.Join("   ", parts);
        }

        private BoonDefinition PickEventBoon(BoonRarity rarity)
        {
            if (boonCatalog == null || boonCatalog.boons == null) return null;
            var boon = BoonOfferPicker.PickOfRarity(boonCatalog.boons, RunState.BoonCount, rarity, ref eventRng);
            if (boon != null) return boon;
            var picks = BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, RoomKind.Treasure, ref eventRng, 1, Luck);
            return picks.Count > 0 ? picks[0] : null;
        }

        /// <summary>A4.2/A7.3: capture the player's current HP immediately before leaving a node — while
        /// the rig still exists, before the fade/load starts — so the next node's fresh scene load can
        /// restore it instead of every node beginning at full HP (which guts attrition/permadeath).</summary>
        private void NoteCurrentPlayerHealth()
        {
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health != null) RunState.NotePlayerHealth(health.Current);
        }

        private void OnEntityDied(EntityDied evt)
        {
            if (!RunState.InRun) return;

            var playerObj = VRRig.Instance != null ? VRRig.Instance.gameObject : null;

            // A7.4: Bloodletter (HealOnKill) had no consumer — this handler early-returned on every
            // non-player death before, silently dropping the boon's whole effect. Any other entity's
            // death heals the player, checked before the player-death branch below.
            float healOnKill = boonInventory.HealOnKill;
            bool victimIsEnemy = evt.Entity != null && evt.Entity.GetComponent<Ronin7.Enemies.Enemy>() != null;
            if (ShouldHealOnKill(evt.Entity, playerObj, healOnKill, victimIsEnemy))
            {
                var health = playerObj.GetComponent<Health>();
                if (health != null) health.Heal(healOnKill);
            }

            if (playerObj != null && evt.Entity != playerObj && evt.Entity != null)
                RunTally.NoteKill(KillerName(evt.Entity));

            if (playerObj == null || evt.Entity != playerObj) return;
            deathBy = KillerName(evt.Killer);
            EndRun(won: false);
        }

        private static void OnHitResisted(HitResisted e) { if (RunState.InRun) RunTally.Resisted++; }
        private static void OnWeaknessExploited(WeaknessExploited e) { if (RunState.InRun) RunTally.Exploited++; }

        private static string BuildTag() => Application.version + (Debug.isDebugBuild ? "-dev" : "");

        private RunLogEntry BuildLogEntry(int depth, bool won, int echoes)
        {
            // One entry per stack, so the log shows how a build was actually stacked.
            var ids = new List<string>();
            foreach (string id in RunState.Boons)
                for (int i = 0; i < RunState.BoonCount(id); i++) ids.Add(id);
            return new RunLogEntry
            {
                utc = DateTime.UtcNow.ToString("o"),
                seed = RunState.Seed,
                won = won,
                depth = depth,
                kills = CampaignStats.EnemiesDefeated - enemiesDefeatedAtRunStart,
                boons = RunState.Boons.Count, // distinct boons, as the run summary counts them
                echoes = echoes, // P4-11: the banked Echo result, not Shards
                seconds = Time.realtimeSinceStartup - runStartRealtime,
                boonIds = ids.ToArray(),
                deathBy = won ? null : deathBy,
                build = BuildTag(),
                evt = "end",
                outcome = RunLog.Outcome(won, deathBy),
                sector = RunState.CurrentNode.Sector,
                elites = RunTally.Elites,
                resistedHits = RunTally.Resisted,
                weaknessHits = RunTally.Exploited,
                spawnCounts = RunTally.Spawns(),
                killCounts = RunTally.Kills(),
                affixCounts = RunTally.Affixes(),
                boonOffers = RunTally.Offers(),
            };
        }

#if RONIN7_DEV
        /// <summary>P0-11 dev tool: grant one random boon that is not yet at max stacks. It applies
        /// from the next node load, exactly like a normal pick.</summary>
        public void DevGrantRandomBoon()
        {
            if (!RunState.InRun || boonCatalog == null || boonCatalog.boons == null) return;
            var pool = new List<BoonDefinition>();
            foreach (var b in boonCatalog.boons)
                if (b != null && RunState.BoonCount(b.id) < Mathf.Max(1, b.maxStacks)) pool.Add(b);
            if (pool.Count == 0) return;
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            GrantBoon(pick);
            Debug.Log($"[DevRunTools] Granted boon '{pick.id}' (applies from the next node).");
        }

        /// <summary>P4-00e dev tool (G6 worst case): walk the map along the first successor to the next
        /// Boss node and load it. False when already on a Boss node (or no Boss is ahead).</summary>
        public bool DevJumpToBoss()
        {
            if (!RunState.InRun || EndingRun || RunState.CurrentNode.Kind == RoomKind.Boss) return false;
            NoteCurrentPlayerHealth();
            while (RunState.CurrentNode.Kind != RoomKind.Boss)
                if (RunState.NextChoices.Count == 0 || !RunState.Advance(RunState.NextChoices[0])) return false;
            DismissChoicePanel();
            pathChoices = null;
            WriteRunSnapshot();
            StartSceneLoad(runArenaScene);
            Debug.Log($"[DevRunTools] Jumped to boss node {RunState.NodeIndex}.");
            return true;
        }
#endif

        /// <summary>A killer's display name without Unity's "(Clone)" suffix; "unknown" if unattributed.</summary>
        internal static string KillerName(GameObject killer) =>
            killer == null ? "unknown" : killer.name.Replace("(Clone)", "").Trim();

        /// <summary>Pure decision: heal on kill iff the dead entity isn't the player, both references
        /// are real, the boon is actually held, and (P4-02) the victim is an Enemy — no healing from
        /// Mirror allies or props. A7.10 test seam.</summary>
        internal static bool ShouldHealOnKill(GameObject dead, GameObject playerRig, float healOnKill, bool victimIsEnemy) =>
            dead != null && playerRig != null && dead != playerRig && healOnKill > 0f && victimIsEnemy;

        /// <summary>P4-02 Luck held this run (whole points, capped by the picker).</summary>
        private float Luck => boonInventory.Bonus(BoonEffectKind.Luck);

        /// <summary>P4-02 EchoGain: scales an echo payout by 1 + bonus (cap x1.5), rounded to whole echoes.
        /// Pure so P4-11 can move it to the end-of-run payout unchanged.</summary>
        internal static int ApplyEchoGain(int echoes, float echoGainBonus) =>
            Mathf.RoundToInt(echoes * (1f + Mathf.Clamp(echoGainBonus, 0f, 0.5f)));

        /// <summary>P4-09: Echo multiplier for a run carrying <paramref name="heatRanks"/> total Fracture Pressure ranks.</summary>
        internal static float HeatEchoMultiplier(int heatRanks) => 1f + 0.1f * Mathf.Max(0, heatRanks);

        /// <summary>P4-09: scales an echo payout by <see cref="HeatEchoMultiplier"/>, rounded. Pure; applied to the run-end result (P4-11).</summary>
        internal static int ApplyHeatEchoes(int echoes, int heatRanks) => Mathf.RoundToInt(echoes * HeatEchoMultiplier(heatRanks));

        /// <summary>P4-11: the Echoes a finished run banks: <see cref="RunScaling.EchoResult"/> scaled by EchoGain, then heat.</summary>
        internal static int RunEchoResult(bool won, int depth, float echoGainBonus, int heatRanks) =>
            ApplyHeatEchoes(ApplyEchoGain(RunScaling.EchoResult(won ? RunMapGenerator.TotalNodes : depth, won), echoGainBonus), heatRanks);

        /// <summary>
        /// A7.1: ends the run's bookkeeping and starts the hub load, but does NOT call
        /// <see cref="RunState.End"/> here — that used to run synchronously inside this method, which
        /// (when called from <see cref="OnEntityDied"/>, itself invoked synchronously inside the
        /// EntityDied EventBus.Publish) flipped RunState.InRun to false before GameFlowManager's own
        /// EntityDied subscriber for the same event ran, if that subscriber happened to run second —
        /// racing its "if (RunState.InRun) return" guard into firing a competing main-menu game-over
        /// alongside this run's hub load. <see cref="EndingRun"/> is latched first, before anything else
        /// (including the RunEnded publish below), as an order-independent second guard; RunState.End()
        /// itself is deferred to <see cref="SceneLoadRoutine"/>, once the hub load has actually
        /// completed.
        /// </summary>
        internal void EndRun(bool won, bool beat = true)
        {
            if (EndingRun) return; // P2-02: a second death/clear during the beat must not double-bank
            EndingRun = true;
            deferredClear = null;
            int depth = RunState.Depth; // P2-04: rows cleared, never the node id
            // P4-11 (D-11): Echoes are paid once, here, from the result; Shards are dropped with the run.
            int echoes = RunEchoResult(won, depth, boonInventory.Bonus(BoonEffectKind.EchoGain), RunState.HeatTotal);
            MetaProgression.AddEchoes(echoes);
            MetaProgression.NoteRunEnded(won ? RunMapGenerator.TotalNodes : depth, won);

            // Snapshot the result BEFORE RunState.End() wipes it (deferred to SceneLoadRoutine, but
            // capture here so nothing else can clear it first), then persist the lifetime totals this
            // run just changed — Save() had no callers at all, so none of it survived the session.
            RunSummary.Capture(
                depth, won,
                CampaignStats.EnemiesDefeated - enemiesDefeatedAtRunStart,
                RunState.Boons.Count,
                echoes);
            MetaProgression.Save();
            RunLog.Append(BuildLogEntry(depth, won, echoes));
            // After the bank is saved (a crash in between must not lose the Echoes): the run is over,
            // so nothing is left to resume — win, permadeath and abandon all end here.
            RunSnapshot.Delete();

            EventBus.Publish(new RunEnded(depth, won));
            DismissOfferPanel();
            DismissChoicePanel();
            pathChoices = null;

            // P2-02 beat: the RunEnded cue above (runLost / runWon) plays now; the menu load waits.
            // Unscaled, so a paused or slowed run still gets the full beat. No camera shake.
            if (!beat)
            {
                StartSceneLoad(runEndScene);
                return;
            }
            float seconds = won ? WinBeatSeconds : DeathBeatSeconds;
            if (won) RunTitleCard.Show(Camera.main, "FRACTURE CLOSED", new Color(1f, 0.85f, 0.4f), seconds);
            else StartCoroutine(DeathFeedback(seconds));
            StartCoroutine(DelayThen(seconds, () => StartSceneLoad(runEndScene)));
        }

        /// <summary>Death beat visuals/haptics: a closing grey vignette, a global URP saturation
        /// ramp to -100 and a lub-dub heartbeat on both controllers. The volume is kept until the
        /// menu has loaded (<see cref="ClearDeathVolume"/>) so the colour does not pop back mid-fade.</summary>
        private IEnumerator DeathFeedback(float seconds)
        {
            var cam = Camera.main;
            var vignette = cam != null ? VignetteRig.Ensure(cam) : null;
            if (vignette != null) vignette.Flash(new Color(0.1f, 0.1f, 0.12f), 1f, seconds);

            ClearDeathVolume();
            deathVolume = new GameObject("Death Desaturate");
            deathVolume.transform.SetParent(transform, false);
            var volume = deathVolume.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.weight = 0f;
            deathProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var adjust = deathProfile.Add<ColorAdjustments>(true);
            adjust.saturation.Override(-100f);
            volume.sharedProfile = deathProfile;

            Haptics.Pulse(XRNode.LeftHand, 0.7f, 0.12f);
            Haptics.Pulse(XRNode.RightHand, 0.7f, 0.12f);
            float t = 0f;
            bool second = false;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                volume.weight = Mathf.Clamp01(t / seconds);
                if (!second && t >= 0.25f)
                {
                    second = true;
                    Haptics.Pulse(XRNode.LeftHand, 0.45f, 0.1f);
                    Haptics.Pulse(XRNode.RightHand, 0.45f, 0.1f);
                }
                yield return null;
            }
        }

        private void ClearDeathVolume()
        {
            if (deathVolume != null) Destroy(deathVolume);
            if (deathProfile != null) Destroy(deathProfile);
            deathVolume = null;
            deathProfile = null;
        }

        private void StartSceneLoad(string scene)
        {
            if (ShouldLatchSceneLoad(transitioning))
            {
                // A zero-enemy Forge/Treasure node's RoomCleared arrives synchronously from
                // RunArenaController.Start() — i.e. from inside the FadeLoadFade call that loaded
                // it, before that call's own camera-wait/fade-in tail runs — so this can legitimately
                // be reentrant. Latch it instead of dropping it; SceneLoadRoutine fires it once the
                // in-flight load finishes.
                pendingScene = scene;
                return;
            }
            if (GameFlowManager.Instance == null)
            {
                Debug.LogError($"[RunDirector] No GameFlowManager in the scene; cannot load '{scene}'.");
                return;
            }
            StartCoroutine(SceneLoadRoutine(scene));
        }

        /// <summary>Whether a scene-load request must be queued rather than started immediately — true
        /// exactly when another one of this director's loads is already in flight. Pure, mirrors
        /// GameFlowManager.ResolveGameOver's latch-vs-fire-now idiom. A7.10 test seam.</summary>
        internal static bool ShouldLatchSceneLoad(bool transitioning) => transitioning;

        /// <summary>Read-and-clear step for the pendingScene latch: returns the queued scene name (and
        /// clears the latch), or null if nothing was queued. Internal so it's unit-testable without
        /// spinning the coroutine. A7.10 test seam.</summary>
        internal string ConsumePendingSceneLoad()
        {
            string next = pendingScene;
            pendingScene = null;
            return next;
        }

        /// <summary>Finalize a run end only when the load that just completed is the run-end scene.
        /// Pure. BUG-25 test seam.</summary>
        internal static bool ShouldFinalizeRunEnd(bool endingRun, string loadedScene, string runEndScene) =>
            endingRun && loadedScene == runEndScene;

        private IEnumerator SceneLoadRoutine(string scene)
        {
            transitioning = true;
            // A7.9: boons apply as GameFlowManager's onLoaded hook, i.e. while the screen is still
            // black, instead of after this call fully returns (after the whole fade-in). skipReveal
            // lets a zero-enemy node's already-latched pendingScene skip the pointless fade-in/fade-out.
            bool arenaLoad = scene == runArenaScene;
            awaitingRigRestore = arenaLoad;
            Action onLoaded = arenaLoad ? (Action)OnArenaLoaded : null;
            // The run-end scene is the main menu, which must load as GameMode.Boot: ShouldAutosaveOnArrival
            // deliberately excludes Boot, and arriving there as OnFoot would fire an arrival autosave on
            // the menu and clobber MostRecentSlot.
            var mode = scene == runEndScene ? GameMode.Boot : GameMode.OnFoot;
            yield return GameFlowManager.Instance.LoadSceneFaded(scene, mode, onLoaded, () => pendingScene != null);
            transitioning = false;
            // Belt and braces: if the load aborted before onLoaded ran, never leave a clear parked.
            if (awaitingRigRestore) OnArenaLoaded();

            // A7.1: only now — after the hub load this EndRun triggered has actually completed — is it
            // safe to flip RunState.InRun off. BUG-25: a death during an ARENA load's fade-in also sets
            // EndingRun while that arena load is still in flight; finalizing then would show the summary
            // in the arena (destroyed by the latched menu load) and consume it. Only the run-end scene's
            // own load finalizes.
            bool justEndedRun = ShouldFinalizeRunEnd(EndingRun, scene, runEndScene);
            if (justEndedRun)
            {
                EndingRun = false;
                RunState.End();
                ClearDeathVolume();
            }

            // Report the finished run on the menu we just arrived at.
            if (justEndedRun) ShowRunSummary();

            string next = ConsumePendingSceneLoad();
            if (next != null) StartSceneLoad(next);
        }

        /// <summary>P4-01: writes the inventory's numbers onto the rig's modifier slots — the named slots
        /// plus one generic stat slot per <see cref="BoonEffectKind"/>. Runs on every load and every pick.</summary>
        internal static void PushBoonStats(PlayerCombatModifiers modifiers, BoonInventory inventory)
        {
            modifiers.BoonMultiplier = inventory.BladeDamageMultiplier;
            modifiers.BoonParryFlowBonus = inventory.ParryFlowBonus;
            modifiers.BoonComboBonus = inventory.ComboBonus;
            for (int k = 0; k < PlayerCombatModifiers.BoonKindCount; k++)
                modifiers.SetBoon((BoonEffectKind)k, inventory.Bonus((BoonEffectKind)k));
            modifiers.OwnedElements = ElementsFor(inventory.OwnedTags);
        }

        /// <summary>P4-05: owning a Bleed / Storm boon owns that S8 Imbue element.</summary>
        internal static ImbueElement ElementsFor(BoonTag tags) =>
            ((tags & BoonTag.Bleed) != 0 ? ImbueElement.Bleed : ImbueElement.None)
            | ((tags & BoonTag.Storm) != 0 ? ImbueElement.Storm : ImbueElement.None);

        /// <summary>P4-05 Hollow Oath: max HP after flat adds and the percentage curse, floor 1.</summary>
        internal static float BoonMaxHealth(float baseMax, float add, float percent) =>
            Mathf.Max(1f, (baseMax + add) * (1f + Mathf.Max(-0.9f, percent)));

        /// <summary>BUG-02: onLoaded hook for arena loads — restore the rig first, then replay a
        /// zero-enemy node's parked clear so its Forge/Treasure reward sees the carried HP.</summary>
        private void OnArenaLoaded()
        {
            awaitingRigRestore = false;
            ApplyBoonInventoryToRig();
            if (deferredClear is RoomCleared c)
            {
                deferredClear = null;
                OnRoomClearedNow(c);
            }
        }

        /// <summary>
        /// Rebuilds this run's <see cref="BoonInventory"/> from RunState.Boons (the durable ledger) and
        /// pushes the aggregated numbers onto the just-loaded arena rig's Health/PlayerCombatModifiers.
        /// Runs as GameFlowManager's onLoaded hook (A7.9) — still under the black fade, before the
        /// fade-in starts — so the player is never visible/attackable at base stats and never gets a
        /// visible instant full-heal once the fade-in completes.
        /// </summary>
        internal void ApplyBoonInventoryToRig()
        {
            if (boonCatalog == null || boonCatalog.boons == null)
            {
                Debug.LogError("[RunDirector] No BoonCatalog assigned; boons cannot be applied to the rig.");
                return;
            }

            boonInventory.Clear();
            foreach (string boonId in RunState.Boons)
            {
                var def = boonCatalog.Find(boonId);
                if (def == null)
                {
                    // BUG-25: a resumed snapshot can name a boon this build no longer ships. Skip it,
                    // but say so once per id rather than on every node load.
                    if (warnedUnknownBoons.Add(boonId))
                        Debug.LogWarning($"[RunDirector] Unknown boon '{boonId}' in this run; skipping it.");
                    continue;
                }
                int stacks = RunState.BoonCount(boonId);
                for (int i = 0; i < stacks; i++) boonInventory.Add(def);
                // P4-11: re-note Epics on every rebuild, so a pick that survived a crash in the snapshot still
                // reaches pastEpics (saved with meta at run end; no file IO here).
                if (def.rarity == BoonRarity.Epic) MetaProgression.NoteEpicPicked(def.id);
                boonInventory.SetTemper(boonId, RunState.TemperLevel(boonId));
            }
            boonInventory.SetRevivesSpent(RunState.RevivesSpent);

            if (VRRig.Instance == null)
            {
                Debug.LogWarning("[RunDirector] No VRRig.Instance in the loaded scene; boons were not applied.");
                return;
            }

            // A7.5: RoguelikeArenaBuilder doesn't add this (Module 5's rig-wiring gap) — without it
            // every damage/parry-flow/combo boon silently does nothing on the arena rig.
            ApplyLoadout(VRRig.Instance.transform, weapons, RunState.LoadoutId);

            var modifiers = VRRig.Instance.GetComponent<PlayerCombatModifiers>();
            if (modifiers == null) modifiers = VRRig.Instance.gameObject.AddComponent<PlayerCombatModifiers>();
            PushBoonStats(modifiers, boonInventory);

            var health = VRRig.Instance.GetComponent<Health>();
            if (health != null)
            {
                // A4.2/A7.3: Configure must come first — raising max HP mid-run must not silently heal,
                // and Configure resets Current to Max, so the stored-HP restore below has to follow it.
                // INVARIANT: health.Max is the prefab's base value on every call here only because VRRig
                // is a per-scene component with no DontDestroyOnLoad, so it's always rebuilt fresh from
                // prefab on every load — if a rig ever survived a load this would compound into
                // base + 15x add instead of a flat add.
                // P2-04: event max-HP changes (Blood Pact) ride on top of boons for the rest of the run.
                float maxAdd = boonInventory.MaxHealthAdd + RunState.MaxHealthBonus;
                float maxPct = boonInventory.Bonus(BoonEffectKind.MaxHealthPercent);
                if (maxAdd != 0f || maxPct != 0f) health.Configure(BoonMaxHealth(health.Max, maxAdd, maxPct));
                if (RunState.PlayerHealth >= 0f)
                    health.SetCurrent(RunState.CarryOverHealth(RunState.PlayerHealth, health.Max));

                // A6.1/A6.2: chain, never clobber and never null. UnbrokenWard (or any other death
                // interceptor) may already have registered itself in OnEnable, which runs before this
                // hook fires — capture it and compose so both mechanics fire in a defined order.
                // ConsumeRevive already returns false once spent, so no separate HasRevive guard is
                // needed in the composed delegate itself.
                var previous = health.DeathInterceptor;
                health.DeathInterceptor = ComposeDeathInterceptor(health, previous, boonInventory.ConsumeRevive);
            }
        }

        /// <summary>P4-08: equips the run's weapon on the freshly loaded rig. The main sword (the rig's
        /// <see cref="KatanaHolster"/>) takes the definition; Twin Wakizashi also activates the left blade on the
        /// <see cref="KatanaHolster.OffhandAnchorName"/> holster and blocks the Off-hand Art. An id with no
        /// matching definition leaves the scene's katana as built.</summary>
        internal static void ApplyLoadout(Transform rig, WeaponDefinition[] weapons, string loadoutId)
        {
            WeaponDefinition def = null;
            if (weapons != null)
                foreach (var w in weapons)
                    if (w != null && w.id == loadoutId) { def = w; break; }
            bool twin = def != null && loadoutId == WeaponId.TwinWakizashi;
            ArtSlots.OffhandBlocked = twin;

            var main = rig.GetComponent<KatanaHolster>();
            if (def != null && main != null) EquipBlade(main.Sword, def);

            var anchor = rig.Find(KatanaHolster.OffhandAnchorName);
            var off = anchor != null ? anchor.GetComponent<KatanaHolster>() : null;
            if (off == null || off.Sword == null) return;
            off.Sword.gameObject.SetActive(twin);
            if (twin) EquipBlade(off.Sword, def);
        }

        private static void EquipBlade(Grabbable sword, WeaponDefinition def)
        {
            if (sword == null) return;
            var s = sword.GetComponent<Sword>();
            if (s != null) s.Definition = def;
            foreach (var d in sword.GetComponentsInChildren<BladeDamager>(true)) d.SetDefinition(def);
        }

        /// <summary>P1-07: Unbroken is tried first so the free save is spent before Second Wind. Each
        /// branch sets its own revive fraction (Unbroken 0 = 1 HP, Second Wind 0.3) before returning,
        /// which Health reads right after a true result. Null <paramref name="unbroken"/> is skipped.</summary>
        internal static System.Func<bool> ComposeDeathInterceptor(Health health, System.Func<bool> unbroken, System.Func<bool> secondWind) =>
            () =>
            {
                if (unbroken != null && unbroken()) { health.ReviveFraction = 0f; return true; }
                if (secondWind()) { health.ReviveFraction = 0.3f; return true; }
                return false;
            };

        /// <summary>What a cleared node should do next. Pure, so it is unit-testable without a scene.</summary>
        internal enum PostClearAction { OfferBoon, AdvanceImmediately, WinRun, OpenShop, OpenEvent, OpenForge, OpenTreasure }

        /// <summary>
        /// Reward-table decision (see the design doc's per-node-kind reward table): Combat/Elite/Boss
        /// offer a 3-choice boon; Forge/Treasure advance immediately (their own reward is a fixed,
        /// non-chosen side effect applied by the caller, not this decision); the run is won only on
        /// the sector-2 Boss's final node.
        /// </summary>
        internal static PostClearAction ResolvePostClear(RoomKind kind, bool isFinalNode)
        {
            if (isFinalNode && kind == RoomKind.Boss) return PostClearAction.WinRun;

            return kind switch
            {
                RoomKind.Combat => PostClearAction.OfferBoon,
                RoomKind.Elite => PostClearAction.OfferBoon,
                RoomKind.Boss => PostClearAction.OfferBoon,
                RoomKind.Forge => PostClearAction.OpenForge,
                RoomKind.Treasure => PostClearAction.OpenTreasure,
                RoomKind.Shop => PostClearAction.OpenShop,
                RoomKind.Event => PostClearAction.OpenEvent,
                _ => PostClearAction.AdvanceImmediately,
            };
        }
    }
}

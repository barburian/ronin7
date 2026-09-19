using System;
using System.Collections;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.EventSystems;

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
        [SerializeField] private string hubScene = "Galaxy1_Ch1_Hub";

        [Header("Boons")]
        [Tooltip("Source pool for starting boons, room-clear offers, and guaranteed Treasure rewards.")]
        [SerializeField] private BoonCatalog boonCatalog;

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

        /// <summary>A7.1: set the instant this director starts ending a run — before EndRun does
        /// anything else, including publishing <see cref="RunEnded"/> — and cleared only once
        /// <see cref="RunState.End"/> has actually run (in <see cref="SceneLoadRoutine"/>, after the hub
        /// load completes). See <see cref="EndRun"/> and the guard in GameFlowManager.OnEntityDied.</summary>
        internal bool EndingRun { get; private set; }

        /// <summary>A7.9: idempotence latch — a repeated RoomCleared for the node already processed
        /// (the arena scene has no reason to publish one twice, but nothing prevents it) must not
        /// double-award echoes or a second boon offer. Reset per run in StartRun.</summary>
        private int lastClearedNode = -1;

        // Boon-offer state for the node currently awaiting a choice.
        private RunRng offerRng;
        private RunNode offerNode;
        private List<BoonDefinition> offerChoices;
        private BoonOfferPanel offerPanel;
        private float offerFallbackDeadline = -1f;

        // This run's boon aggregation. One instance for the whole run (A1.1/A4.1) — Clear()+replay every
        // arena load rather than re-`new`-ing, since RunState.Boons/RevivesSpent are the durable ledger
        // this rebuilds from either way (A7.9).
        private readonly BoonInventory boonInventory = new BoonInventory();

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
        }

        // Balances OnEnable's subscriptions (incl. a destroyed duplicate's) — no leak across
        // LoadSceneMode.Single, same reasoning as GameFlowManager's OnDisable.
        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomCleared>(OnRoomCleared);
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            DismissOfferPanel();
        }

        /// <summary>A7.2: re-anchors the live offer panel to the head every frame (position only — see
        /// BoonOfferPanel.Reposition) and runs the offer's fallback timer.</summary>
        private void Update()
        {
            if (offerPanel == null) return;

            if (Camera.main != null)
            {
                var camT = Camera.main.transform;
                offerPanel.Reposition(camT.position + camT.forward * 1.3f);
            }

            if (offerFallbackDeadline >= 0f && Time.unscaledTime >= offerFallbackDeadline)
            {
                Debug.LogWarning("[RunDirector] Boon offer timed out with no player input; auto-picking the first choice.");
                OnBoonChoiceSelected(0);
            }
        }

        /// <summary>Start a run with a random seed.</summary>
        public void StartRun() => StartRun(GenerateSeed());

        /// <summary>Start a run with a known seed (daily run / replay).</summary>
        public void StartRun(uint seed)
        {
            if (RunState.InRun) return; // guard against a double-tap on the hub launcher
            RunState.Begin(seed);
            lastClearedNode = -1;
            ApplyStartingBonuses();
            EventBus.Publish(new RunStarted(seed));
            StartSceneLoad(runArenaScene);
        }

        /// <summary>Give up the current run early: same loss bookkeeping as permadeath (echoes
        /// banked, boons lost), just without a death event behind it.</summary>
        public void AbandonRun()
        {
            if (!RunState.InRun) return;
            EndRun(won: false);
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
                if (boon != null && boon.rarity == BoonRarity.Common) pool.Add(boon);

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
            if (!RunState.InRun || evt.NodeIndex != RunState.NodeIndex) return;
            if (evt.NodeIndex == lastClearedNode) return; // A7.9: idempotence — no double echoes/offer
            lastClearedNode = evt.NodeIndex;

            var node = RunState.CurrentNode;
            RunState.AddEchoes(RunScaling.EchoReward(node.Index, node.Kind));

            bool isFinalNode = node.Index == RunMapGenerator.TotalNodes - 1;
            switch (ResolvePostClear(node.Kind, isFinalNode))
            {
                case PostClearAction.WinRun:
                    EndRun(won: true);
                    break;
                case PostClearAction.OfferBoon:
                    BeginBoonOffer(node);
                    break;
                case PostClearAction.AdvanceImmediately:
                    ApplyNonCombatReward(node);
                    AdvanceNode();
                    break;
            }
        }

        private void ApplyNonCombatReward(RunNode node)
        {
            switch (node.Kind)
            {
                case RoomKind.Forge:
                    ApplyForgeReward();
                    break;
                case RoomKind.Treasure:
                    GrantTreasureBoon(node);
                    break;
            }
        }

        /// <summary>Forge: heal 60% of missing HP (against the durable stored HP, not a full-HP
        /// assumption — A4.2/A7.3) + 1 reroll token, no boon choice.</summary>
        private void ApplyForgeReward()
        {
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            if (health != null)
            {
                health.Heal(RunState.HealAmountForForge(health.Current, health.Max));
                RunState.NotePlayerHealth(health.Current); // re-store the post-heal value
            }
            RunState.AddRerollToken(1);
        }

        /// <summary>
        /// Treasure: 1 guaranteed boon, no choice, no fight. Reuses <see cref="BoonOfferPicker"/>
        /// with <see cref="RoomKind.Treasure"/> and count 1 rather than hand-rolling a filter —
        /// BoonOfferPicker.RarityWeight already excludes Common for Treasure (its "guarantees at
        /// least Rare" weighting, Rare/Epic only), so this is the single source of truth for what
        /// "Treasure-tier" means, not a second guess at it.
        /// </summary>
        private void GrantTreasureBoon(RunNode node)
        {
            if (boonCatalog == null || boonCatalog.boons == null)
            {
                Debug.LogError("[RunDirector] No BoonCatalog assigned; cannot grant the Treasure boon.");
                return;
            }

            var rng = RunRng.ForNode(RunState.Seed, node.Index);
            var picks = BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, RoomKind.Treasure, ref rng, count: 1);
            if (picks.Count == 0) return; // pool exhausted; nothing left to grant this run

            GrantBoon(picks[0]);
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
        private static void GrantBoon(BoonDefinition boon)
        {
            RunState.AddBoon(boon.id);
            EventBus.Publish(new BoonChosen(boon.id));
            if (boon.effect == BoonEffectKind.GrantAbility) RunState.GrantAbility(boon.abilityId);
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
                    AdvanceNode();
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
            return BoonOfferPicker.Pick(boonCatalog.boons, RunState.BoonCount, kind, ref offerRng, 3);
        }

        private void OnBoonChoiceSelected(int index)
        {
            if (offerChoices == null || index < 0 || index >= offerChoices.Count) return;
            GrantBoon(offerChoices[index]);
            DismissOfferPanel();
            AdvanceNode();
        }

        private void OnRerollRequested()
        {
            if (!RunState.TrySpendRerollToken()) return;
            offerChoices = PickBoonChoices(offerNode.Kind);
            if (offerPanel == null) return;
            offerPanel.SetChoices(offerChoices);
            offerPanel.SetRerollInteractable(RunState.RerollTokens > 0);
        }

        private void DismissOfferPanel()
        {
            offerFallbackDeadline = -1f;
            if (offerPanel == null) return;
            offerPanel.ChoiceSelected -= OnBoonChoiceSelected;
            offerPanel.RerollRequested -= OnRerollRequested;
            Destroy(offerPanel.gameObject);
            offerPanel = null;
        }

        private void AdvanceNode()
        {
            NoteCurrentPlayerHealth();
            bool hasNext = RunState.Advance();
            if (!hasNext)
            {
                // Safety net: the Boss/final-node case is normally caught by ResolvePostClear before
                // Advance() is ever called (see WinRun above); this only fires if the map's length
                // ever disagreed with RunMapGenerator.TotalNodes.
                EndRun(won: true);
                return;
            }
            StartSceneLoad(runArenaScene);
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
            if (ShouldHealOnKill(evt.Entity, playerObj, healOnKill))
            {
                var health = playerObj.GetComponent<Health>();
                if (health != null) health.Heal(healOnKill);
            }

            if (playerObj == null || evt.Entity != playerObj) return;
            EndRun(won: false);
        }

        /// <summary>Pure decision: heal on kill iff the dead entity isn't the player, both references
        /// are real, and the boon is actually held. A7.10 test seam.</summary>
        internal static bool ShouldHealOnKill(GameObject dead, GameObject playerRig, float healOnKill) =>
            dead != null && playerRig != null && dead != playerRig && healOnKill > 0f;

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
        private void EndRun(bool won)
        {
            EndingRun = true;
            int depth = RunState.NodeIndex;
            MetaProgression.AddEchoes(RunState.EchoesEarned);
            MetaProgression.NoteRunEnded(depth, won);
            EventBus.Publish(new RunEnded(depth, won));
            DismissOfferPanel();
            StartSceneLoad(hubScene);
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

        private IEnumerator SceneLoadRoutine(string scene)
        {
            transitioning = true;
            // A7.9: boons apply as GameFlowManager's onLoaded hook, i.e. while the screen is still
            // black, instead of after this call fully returns (after the whole fade-in). skipReveal
            // lets a zero-enemy node's already-latched pendingScene skip the pointless fade-in/fade-out.
            Action onLoaded = scene == runArenaScene ? (Action)ApplyBoonInventoryToRig : null;
            yield return GameFlowManager.Instance.LoadSceneFaded(scene, GameMode.OnFoot, onLoaded, () => pendingScene != null);
            transitioning = false;

            // A7.1: only now — after the hub load this EndRun triggered has actually completed — is it
            // safe to flip RunState.InRun off.
            if (EndingRun)
            {
                EndingRun = false;
                RunState.End();
            }

            string next = ConsumePendingSceneLoad();
            if (next != null) StartSceneLoad(next);
        }

        /// <summary>
        /// Rebuilds this run's <see cref="BoonInventory"/> from RunState.Boons (the durable ledger) and
        /// pushes the aggregated numbers onto the just-loaded arena rig's Health/PlayerCombatModifiers.
        /// Runs as GameFlowManager's onLoaded hook (A7.9) — still under the black fade, before the
        /// fade-in starts — so the player is never visible/attackable at base stats and never gets a
        /// visible instant full-heal once the fade-in completes.
        /// </summary>
        private void ApplyBoonInventoryToRig()
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
                if (def == null) continue;
                int stacks = RunState.BoonCount(boonId);
                for (int i = 0; i < stacks; i++) boonInventory.Add(def);
            }
            boonInventory.SetRevivesSpent(RunState.RevivesSpent);

            if (VRRig.Instance == null)
            {
                Debug.LogWarning("[RunDirector] No VRRig.Instance in the loaded scene; boons were not applied.");
                return;
            }

            // A7.5: RoguelikeArenaBuilder doesn't add this (Module 5's rig-wiring gap) — without it
            // every damage/parry-flow/combo boon silently does nothing on the arena rig.
            var modifiers = VRRig.Instance.GetComponent<PlayerCombatModifiers>();
            if (modifiers == null) modifiers = VRRig.Instance.gameObject.AddComponent<PlayerCombatModifiers>();
            modifiers.BoonMultiplier = boonInventory.BladeDamageMultiplier;
            modifiers.BoonParryFlowBonus = boonInventory.ParryFlowBonus;
            modifiers.BoonComboBonus = boonInventory.ComboBonus;

            var health = VRRig.Instance.GetComponent<Health>();
            if (health != null)
            {
                // A4.2/A7.3: Configure must come first — raising max HP mid-run must not silently heal,
                // and Configure resets Current to Max, so the stored-HP restore below has to follow it.
                // INVARIANT: health.Max is the prefab's base value on every call here only because VRRig
                // is a per-scene component with no DontDestroyOnLoad, so it's always rebuilt fresh from
                // prefab on every load — if a rig ever survived a load this would compound into
                // base + 15x add instead of a flat add.
                if (boonInventory.MaxHealthAdd > 0f) health.Configure(health.Max + boonInventory.MaxHealthAdd);
                if (RunState.PlayerHealth >= 0f)
                    health.SetCurrent(RunState.CarryOverHealth(RunState.PlayerHealth, health.Max));

                // A6.1/A6.2: chain, never clobber and never null. UnbrokenWard (or any other death
                // interceptor) may already have registered itself in OnEnable, which runs before this
                // hook fires — capture it and compose so both mechanics fire in a defined order.
                // ConsumeRevive already returns false once spent, so no separate HasRevive guard is
                // needed in the composed delegate itself.
                var previous = health.DeathInterceptor;
                health.DeathInterceptor = () => boonInventory.ConsumeRevive() || (previous != null && previous());
                health.ReviveFraction = boonInventory.HasRevive ? 0.3f : 0f;
            }
        }

        /// <summary>What a cleared node should do next. Pure, so it is unit-testable without a scene.</summary>
        internal enum PostClearAction { OfferBoon, AdvanceImmediately, WinRun }

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
                RoomKind.Forge => PostClearAction.AdvanceImmediately,
                RoomKind.Treasure => PostClearAction.AdvanceImmediately,
                _ => PostClearAction.AdvanceImmediately,
            };
        }
    }
}

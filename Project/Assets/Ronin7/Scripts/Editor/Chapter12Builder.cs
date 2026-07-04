using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter 12 ("The Fracture") scene builder. Act III closer. One self-contained scene along +Z: the
    /// Cairn briefing (voice-only) -> the Dominion cryo-command vault descent (3 tiers, cold-certainty
    /// military architecture, cradle racks that repeat the player's own silhouette more densely with
    /// depth, a light rival/sentinel skirmish) -> the throne-tier command interface (Commander Vale wakes
    /// and pitches the "defective generation" army) -> the reveal + boss (the command-network readout
    /// names Ronin-7 the TEMPLATE; a Ronin-7 Edition, his own leashed clone, is the keeper; killing it
    /// grants Mirror, the FIFTH and final permanent ability) -> the refusal (Vale's throne declined, the
    /// last node cut loose, the Engine's build-record completed) -> the Ch13 hook.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch12</c>.
    ///
    /// LADDER C RUNG 2 (the identity reveal): delivered exactly once, in the Beat3 "reveal" dialogue set
    /// — Ronin-7 is the TEMPLATE, the seventh Ronin and first-viable iteration (1-6 culled), every
    /// edition on the network cloned FROM him. This inverts Ch11's "you're a copy" supposition. The birth
    /// name "Soren" is never used (reserved for Ch16) and no other numbered ladder rung advances this
    /// chapter — see Chapter12Lines' class summary and Chapter12LinesTests.NoLine_MentionsSoren.
    ///
    /// CREW-PRESENCE DECISION: mirrors Ch10/Ch11 — only Ronin-7 (the player), Vale, and the Ronin-7
    /// Edition get physical placement. Cassie-04/Sable/Mera Voss/Kessler/Morrigan/Coral Vex/Vess/Gryph
    /// speak only over comm/voice-only DialoguePlayers, matching every chapter's convention for crew who
    /// stay aboard the Cairn or hold comm from the vault mouth.
    ///
    /// VALE STAGING: a plain <see cref="StoryNpc"/>, no Health/combat — canon is explicit that this beat
    /// is "an argument, not a fight" and Vale "does not draw the command baton" and SURVIVES uncaptured
    /// as the Act IV foil. Built active from scene start (mirrors Ch10's Cassie-04-at-the-Archive
    /// staging: no separate "wake from the cryo-throne" animation system — greybox scope cut, consistent
    /// with that precedent). Resolves to the real, already-baked <c>Commander-Vale.prefab</c>.
    ///
    /// RONIN-7 EDITION / MIRROR BOSS DECISION: the sole boss, and the ONLY chapter-12 deliverable
    /// explicitly directed to reuse <see cref="MirrorPhantom"/> (the existing "sequence of phantom self
    /// copies" mechanic) rather than a single plain <see cref="Enemy"/> kill like Sever/Aldric — it
    /// stages the duel as two phases of the identical clone (mirrors the source script's "the duel
    /// escalating as a contest of the same toolkit in two hands"), both built from the real, already-baked
    /// <c>Ronin-7_Edition_Clone.prefab</c> (it IS his face) and sharing one <see cref="EnemyDefinition"/>
    /// (no invented stat escalation between phases — the horror is the perfect symmetry, not a numbers
    /// ramp). <c>finalChildScale</c> is forced to 1 so the second phase never visually shrinks: the whole
    /// point is that it is identical to Cipher, not a lesser copy. <see cref="MirrorPhantom.onSequenceCleared"/>
    /// is wired to <c>MissionDirector.AdvanceFromPrompt</c> via a null-object Prompt step — the exact
    /// idiom Ch10 already uses for <c>DuelYield.onAccepted</c> — because <c>BeginDefeatEnemies</c> would
    /// force-activate every listed Health immediately and defeat MirrorPhantom's own "one phantom active
    /// at a time" sequencing if both phases were listed in one DefeatEnemies step.
    ///
    /// CRYO-VAULT HAZARD: <see cref="CryoChillController"/> rides the rig (per the chapter brief's
    /// explicit reuse instruction) tuned mild — atmospheric cold, not a core survival mechanic, since the
    /// chapter's real pressure is Beat 2's argument and Beat 3's boss. One <see cref="HeatVent"/> sits at
    /// the throne-tier's node pulse (the source script: "the only warmth in the place is the slow amber
    /// pulse of the last scattered archive") — a one-line tie between the fiction and the mechanic, no
    /// controller wiring needed (HeatVent falls back to finding the rig's CryoChillController on whatever
    /// enters it).
    ///
    /// THE LAST NODE: unlike Cassie-04/the Dreaming Archive/Sever's Archive, this chapter's node is never
    /// given a personal name in the source dialogue (Sable only ever calls her "the last of mine") and is
    /// never fought — she is freed by dialogue + a campaign flag, not combat. Represented as a decorative
    /// amber <see cref="BuildHologram"/> node visual near the throne (mirrors Ch11's ArkshipCore
    /// hologram), not a StoryNpc.
    ///
    /// ALLY UNLOCK: none — per the story bible and audit, Ch12 stays ally-free (Vale is an antagonist/
    /// foil, the last node is freed not recruited). No HubBuilder room increment is added, mirroring
    /// Ch11's identical precedent (HubBuilder's CampaignDirector mission list already carries the CH12
    /// entry from prior work; only ally-recruiting chapters like Ch10 add a war-room increment).
    ///
    /// SENTINEL DUELIST + HIVE CASCADE (enemy-variety pass): the Tier-2 skirmish's three "cradle
    /// sentinels" are the chapter's own "repeats the player's own silhouette more densely with depth"
    /// motif — a hive of near-identical bodies. <see cref="HiveCascadeController"/> (previously wired
    /// nowhere in the project) makes that literal: it drives the trio through the EP24 "Fracture
    /// Protocol" Attacking/Frozen/Conflicted desync instead of lockstep, echoing this chapter's own title.
    /// One of the three is also upgraded to a tanky "Sentinel Duelist" elite (<c>Ch12SentinelDuelist</c>
    /// EnemyDefinition, PostureMeter + PatternedDuelist) — see <c>Ch12UpgradeToSentinelDuelist</c>.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch12ScenePath = SceneFolder + "/Ch12_TheFracture.unity";
        private const string Ch12VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch12TierHalfWidth = 6f;
        private const float Ch12ThroneHalfWidth = 10f;

        // Named-cast prefabs. Both resolve to real, already-baked prefabs (glob-confirmed on disk at
        // authoring time) — the edition reuses Ronin-7's own face, per canon ("it IS his face").
        private const string Ch12ValePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Commander-Vale.prefab";
        private const string Ch12EditionPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Ronin-7_Edition_Clone.prefab";
        private const string Ch12EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 12 — The Fracture", priority = 212)]
        public static void BuildChapter12TheFracture()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var skirmisherDef = Ch12EnsureSkirmisherDefinition();
            var editionDef = Ch12EnsureEditionDefinition();
            var sentinelDuelistDef = Ch12EnsureSentinelDuelistDefinition();

            // ---- Lighting: COLD CERTAINTY made into architecture — bone-grey daylight at the mouth,
            // cooling to a deep stasis-blue toward the throne-tier; the node's amber pulse is the only
            // warmth in the place. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.6f, 0.68f);
            light.intensity = 0.28f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.1f);
            RenderSettings.fogDensity = 0.016f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.6f, 0.65f, 0.75f), 1f, 10f);
            BuildAccentPointLight("Tier1Light", new Vector3(2f, 1.2f, 26f), new Color(0.55f, 0.6f, 0.72f), 1.2f, 12f);
            BuildAccentPointLight("Tier2Light", new Vector3(-2f, -0.2f, 42f), new Color(0.4f, 0.5f, 0.7f), 1.3f, 12f);
            BuildAccentPointLight("Tier3Light", new Vector3(1f, -1.6f, 58f), new Color(0.28f, 0.4f, 0.65f), 1.5f, 14f);
            BuildAccentPointLight("ThroneLight0", new Vector3(-4f, -2.4f, 74f), new Color(0.9f, 0.65f, 0.3f), 1.6f, 16f); // the node's amber pulse
            BuildAccentPointLight("ThroneLight1", new Vector3(4f, -2.4f, 74f), new Color(0.35f, 0.45f, 0.7f), 1.4f, 14f);

            // ---- World root: the cryo-command vault, a monolithic bilaterally-symmetrical descent. ----
            var worldGo = new GameObject("CryoVault");
            var world = worldGo.transform;

            BuildFloorCeiling(world, "SpawnGround", new Vector3(0f, 0f, 4f), new Vector3(16f, 0f, 12f),
                new Color(0.16f, 0.17f, 0.2f), new Color(0.05f, 0.05f, 0.06f));

            Vector3[] tiers =
            {
                new Vector3(2f, -1.5f, 26f),  // Tier 1 — upper vault, mixed anonymous cradles
                new Vector3(-2f, -3f, 42f),   // Tier 2 — mid vault, the repetition begins; skirmish here
                new Vector3(1f, -4.5f, 58f),  // Tier 3 — lower vault, the inventory in full
            };
            var upperColor = new Color(0.22f, 0.24f, 0.28f);
            var deepColor = new Color(0.1f, 0.14f, 0.24f);
            for (int i = 0; i < tiers.Length; i++)
            {
                float t = Mathf.InverseLerp(0, tiers.Length - 1, i);
                Color tierColor = Color.Lerp(upperColor, deepColor, t);
                Ch12BuildTier(world, $"Tier{i + 1}", tiers[i], tierColor);
                // Cradle racks repeat the player's own silhouette more densely with depth — the
                // architecture delivers "I am the template" in space before Vale ever says it.
                Ch12BuildCradleRow(world, $"Tier{i + 1}", tiers[i], 2 + i * 3);
            }
            Ch12BuildRamp(world, "Ramp0", new Vector3(0f, 0f, 10f), tiers[0], Ch12TierHalfWidth * 2f);
            for (int i = 0; i < tiers.Length - 1; i++)
                Ch12BuildRamp(world, $"Ramp{i + 1}", tiers[i], tiers[i + 1], Ch12TierHalfWidth * 2f);

            // ---- The throne-tier: an enclosed command interface, distinct from the open tier platforms
            // — Vale's cryo-throne, the edition's duel arena, and the last node's amber pulse. ----
            var throneCenter = new Vector3(0f, -6f, 74f);
            BuildFloorCeiling(world, "ThroneTier", throneCenter, new Vector3(Ch12ThroneHalfWidth * 2f, 0f, 22f),
                new Color(0.05f, 0.06f, 0.08f), new Color(0.02f, 0.03f, 0.04f));
            BuildWall(world, "Throne_WallW", throneCenter + new Vector3(-Ch12ThroneHalfWidth, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 22f));
            BuildWall(world, "Throne_WallE", throneCenter + new Vector3(Ch12ThroneHalfWidth, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 22f));
            BuildWall(world, "Throne_WallN", throneCenter + new Vector3(0f, RoomH / 2f, 11f), new Vector3(Ch12ThroneHalfWidth * 2f, RoomH, 0.2f));
            Ch12BuildRamp(world, "RampThrone", tiers[2], throneCenter + new Vector3(0f, 0f, -11f), Ch12TierHalfWidth * 2f);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, every shipped ability chain
            // (weakpoint-sight/Overdrive/Phase-step/Unbroken self-gate; Mirror is granted this chapter),
            // and the cryo-vault's cold hazard. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, -3f, 60f);
            bounds.radius = 130f;

            AttachPlayerAbilities(rig, refs);

            // Cryo-vault chill: mild atmospheric pressure (the chapter's real pressure is Beat 2's
            // argument and Beat 3's boss, not the cold) — playerHealth auto-wires via CryoChillController.Awake.
            var chill = rig.AddComponent<CryoChillController>();
            var chillSo = new SerializedObject(chill);
            chillSo.FindProperty("chillRiseRate").floatValue = 0.015f;
            chillSo.ApplyModifiedPropertiesWithoutUndo();

            // The katana rides from the start (deep into Act III — no rack-wake beat, matching Ch9-11's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch12EchoBladePrefab);

            // ---- Skirmish: rival-force vanguard + auto-roused cradle sentinels on the mid-vault tier.
            // Inactive until the DefeatEnemies step auto-activates them (mirrors Ch10's syndicate-guard
            // convention). Kept light and cold per the chapter brief — the real fights are Beat 2's
            // argument and Beat 3's boss. The LAST cradle sentinel is upgraded to the tanky "Sentinel
            // Duelist" elite variant, and a HiveCascadeController (the "one mind, many bodies" cradle-rack
            // motif — and this chapter's own EP24 "Fracture Protocol" — made literal; previously wired
            // nowhere in the project) drives the whole trio through Attacking/Frozen/Conflicted desync
            // instead of lockstep. Neither addition changes the DefeatEnemies step below (still the same
            // 3 Health objectives). ----
            Vector3[] skirmishPositions =
            {
                tiers[1] + new Vector3(-3f, 0f, 4f), tiers[1] + new Vector3(3f, 0f, 4f), tiers[1] + new Vector3(0f, 0f, -3f),
            };
            var skirmishHealths = new List<Object>();
            var skirmishSquad = new List<MeleeAttacker>();
            foreach (var pos in skirmishPositions)
            {
                var e = BuildEnemy(pos, playerHealth, skirmisherDef);
                e.gameObject.SetActive(false);
                skirmishHealths.Add(e.GetComponent<Health>());
                skirmishSquad.Add(e);
            }
            Ch12UpgradeToSentinelDuelist((Enemy)skirmishSquad[skirmishSquad.Count - 1], sentinelDuelistDef);
            Ch12BuildHiveCascade(skirmishSquad);

            // ---- Commander Vale: a plain StoryNpc, no Health/combat (see class summary) — built active
            // from scene start. ----
            Ch12PlaceStoryNpc(Ch12ValePrefab, throneCenter + new Vector3(-4f, 0f, 6f), "Vale");

            // ---- The last node: a decorative amber hologram, never fought (see class summary). ----
            Ch12PlaceLastNode(throneCenter + new Vector3(3f, 1.2f, 4f));

            // ---- The Ronin-7 Edition: the boss, staged as two MirrorPhantom phases of the same clone.
            // Both built inactive; MirrorPhantom.Begin() (fired from its own OnEnable, mirroring
            // AbilityGranter/ProximityDoor) activates phase 1 only when the Trigger step reveals it. ----
            var editionPhase1 = Ch12BuildNamedBoss(Ch12EditionPrefab, throneCenter + new Vector3(0f, 0f, -2f), "Ronin-7 Edition", editionDef, playerHealth);
            editionPhase1.gameObject.SetActive(false);
            var editionPhase2 = Ch12BuildNamedBoss(Ch12EditionPrefab, throneCenter + new Vector3(0f, 0f, -2f), "Ronin-7 Edition", editionDef, playerHealth);
            editionPhase2.gameObject.SetActive(false);

            var editionControllerGo = new GameObject("EditionMirrorPhantom");
            var editionPhantom = editionControllerGo.AddComponent<MirrorPhantom>();
            var editionSo = new SerializedObject(editionPhantom);
            SetObjectRefList(editionSo, "phantoms", new List<Object> { editionPhase1.GetComponent<Health>(), editionPhase2.GetComponent<Health>() });
            editionSo.FindProperty("finalChildScale").floatValue = 1f; // identical to Cipher, never a lesser copy
            editionSo.ApplyModifiedPropertiesWithoutUndo();
            editionControllerGo.SetActive(false); // Activated only by the Trigger step (fires Begin() via OnEnable).

            // ---- Mirror ability grant: activated alongside the edition's defeat. ----
            var mirrorGranterGo = new GameObject("MirrorGranter");
            var mirrorGranter = mirrorGranterGo.AddComponent<AbilityGranter>();
            var mgSo = new SerializedObject(mirrorGranter);
            mgSo.FindProperty("abilityId").stringValue = AbilityId.Mirror;
            mgSo.ApplyModifiedPropertiesWithoutUndo();
            mirrorGranterGo.SetActive(false);

            // ---- The throne-tier's warmth: the node's amber pulse sheds CryoChillController's chill
            // while the player stands near it (falls back to finding the rig's controller on whatever
            // enters — no wiring needed). ----
            var heatVentGo = new GameObject("NodeWarmth");
            heatVentGo.transform.position = throneCenter + new Vector3(3f, 1f, 4f);
            var heatVentCollider = heatVentGo.AddComponent<BoxCollider>();
            heatVentCollider.isTrigger = true;
            heatVentCollider.size = new Vector3(6f, 3f, 6f);
            heatVentGo.AddComponent<HeatVent>();

            // ---- Reach points. ----
            var midVaultReachGo = new GameObject("MidVaultReachPoint");
            midVaultReachGo.transform.position = tiers[1] + new Vector3(0f, 1f, 0f);
            var throneTierReachGo = new GameObject("ThroneTierReachPoint");
            throneTierReachGo.transform.position = throneCenter + new Vector3(0f, 1f, -6f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch12BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch12_beat0_briefing", talkRef);
            var dlgDescent = Ch12BuildDialogue("Dialogue_Beat1_Descent", new Vector3(0f, 1f, 14f), "ch12_beat1_descent", talkRef);
            var dlgRepetition = Ch12BuildDialogue("Dialogue_Beat1_Repetition", tiers[2] + new Vector3(0f, 1f, -4f), "ch12_beat1_repetition", talkRef);
            var dlgGreeting = Ch12BuildDialogue("Dialogue_Beat2_Greeting", throneCenter + new Vector3(0f, 1f, -4f), "ch12_beat2_greeting", talkRef);
            var dlgOffer = Ch12BuildDialogue("Dialogue_Beat2_Offer", throneCenter + new Vector3(0f, 1f, -2f), "ch12_beat2_offer", talkRef);
            var dlgThreat = Ch12BuildDialogue("Dialogue_Beat2_Threat", throneCenter + new Vector3(0f, 1f, 0f), "ch12_beat2_threat", talkRef);
            var dlgIntro3 = Ch12BuildDialogue("Dialogue_Beat3_Intro", throneCenter + new Vector3(0f, 1f, -2f), "ch12_beat3_intro", talkRef);
            var dlgReveal = Ch12BuildDialogue("Dialogue_Beat3_Reveal", throneCenter + new Vector3(0f, 1f, -1f), "ch12_beat3_reveal", talkRef);
            var dlgAftermath = Ch12BuildDialogue("Dialogue_Beat3_Aftermath", throneCenter + new Vector3(0f, 1f, 1f), "ch12_beat3_aftermath", talkRef);
            var dlgOffer4 = Ch12BuildDialogue("Dialogue_Beat4_Offer", throneCenter + new Vector3(0f, 1f, 3f), "ch12_beat4_offer", talkRef);
            var dlgRefusal = Ch12BuildDialogue("Dialogue_Beat4_Refusal", throneCenter + new Vector3(0f, 1f, 4f), "ch12_beat4_refusal", talkRef);
            var dlgCut = Ch12BuildDialogue("Dialogue_Beat4_Cut", throneCenter + new Vector3(2f, 1f, 4f), "ch12_beat4_cut", talkRef);
            var dlgHomecoming = Ch12BuildDialogue("Dialogue_Beat4_Homecoming", throneCenter + new Vector3(2f, 1f, 5f), "ch12_beat4_homecoming", talkRef);
            var dlgParting = Ch12BuildDialogue("Dialogue_Beat4_Parting", throneCenter + new Vector3(0f, 1f, 6f), "ch12_beat4_parting", talkRef);
            var dlgHook = Ch12BuildDialogue("Dialogue_Beat4_Hook", throneCenter + new Vector3(0f, 1f, 8f), "ch12_beat4_hook", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ONLY ch12_complete — no
            // recruit flag (Vale is an antagonist/foil; the last node is freed, not recruited). ----
            var completeCanvasGo = Ch12BuildCompleteCanvas(throneCenter + new Vector3(0f, 1.4f, 10f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = throneCenter + new Vector3(0f, 1f, 9f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch12_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 12 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 22;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Cryo-Vault Descent (race begins)", dlgDescent);
            AuthorReachStep(steps, n++, "ReachTrigger: The Mid-Vault (repetition begins)", midVaultReachGo.transform, 5f);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Rival Vanguard + Cradle Sentinels", skirmishHealths);
            AuthorDialogueStep(steps, n++, "Beat1: Counting Cradles (Echo's dawning realization)", dlgRepetition);
            AuthorReachStep(steps, n++, "ReachTrigger: The Throne-Tier (command interface)", throneTierReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat2: Waking Vale (the command-key)", dlgGreeting);
            AuthorDialogueStep(steps, n++, "Beat2: The Crown (Vale's pitch)", dlgOffer);
            AuthorDialogueStep(steps, n++, "Beat2: The Part You Haven't Priced (Vale's trump card)", dlgThreat);
            AuthorTriggerStep(steps, n++, "Trigger: The Edition Wakes (MirrorPhantom Begin)", editionControllerGo);
            AuthorDialogueStep(steps, n++, "Beat3: The Edition (conditioning-phrase confrontation)", dlgIntro3);
            AuthorDialogueStep(steps, n++, "Beat3: The Template (Ladder C rung 2)", dlgReveal);
            AuthorPromptStep(steps, n++, "Prompt: The Duel (Ronin-7 Edition, two-phase mirror)", null);
            AuthorDialogueStep(steps, n++, "Beat3: Grief, Not Fear (Mirror unlock)", dlgAftermath);
            AuthorTriggerStep(steps, n++, "Trigger: Mirror Granted (the edition's freed blade-shadow)", mirrorGranterGo);
            AuthorDialogueStep(steps, n++, "Beat4: The Final Offer (Vale's crown, no velvet)", dlgOffer4);
            AuthorDialogueStep(steps, n++, "Beat4: The Refusal (free, never command)", dlgRefusal);
            AuthorDialogueStep(steps, n++, "Beat4: Cut Her Loose (the build-record completes)", dlgCut);
            AuthorDialogueStep(steps, n++, "Beat4: Homecoming (Sable)", dlgHomecoming);
            AuthorDialogueStep(steps, n++, "Beat4: Vale's Parting (the foil survives)", dlgParting);
            AuthorDialogueStep(steps, n++, "Beat4: The Hook (Ch13, the maker)", dlgHook);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The boss sequence's clearance advances the mission out of the null-prompt duel step.
            UnityEventTools.AddPersistentListener(editionPhantom.onSequenceCleared,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch12ScenePath);
            EnsureScenesInBuild(Ch12ScenePath);

            Debug.Log($"[Space Samurai] Chapter 12 built at {Ch12ScenePath}. " +
                      "The cryo-command vault descent (3 tiers, density-escalating cradle racks, a light " +
                      "rival/sentinel skirmish) -> the throne-tier command interface (Vale wakes, pitches " +
                      "the army) -> the reveal + boss (Ronin-7 Edition, two-phase MirrorPhantom duel; " +
                      "Ladder C rung 2 delivered; grants Mirror, the fifth and final permanent ability) " +
                      "-> the refusal (the last node cut loose, build-record complete, Vale survives) -> " +
                      "the Ch13 hook. 22 mission steps. NO ally recruited. Vale/the edition resolve to " +
                      "real Named prefabs (Commander-Vale, Ronin-7_Edition_Clone).");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch10/Ch11's Ensure* convention). ----

        private static EnemyDefinition Ch12EnsureSkirmisherDefinition()
        {
            const string path = DataFolder + "/Ch12Skirmisher.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 60f;
            def.damage = 9f;
            def.moveSpeed = 1.6f;
            def.attackCooldown = 0.85f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch12EnsureEditionDefinition()
        {
            const string path = DataFolder + "/Ch12Edition.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            // The player's own school, mirrored back with zero hesitation — hard and fast, no invented
            // escalation between the two MirrorPhantom phases (see class summary).
            def.maxHealth = 320f;
            def.damage = 28f;
            def.moveSpeed = 1.6f;
            def.attackCooldown = 0.8f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch12EnsureSentinelDuelistDefinition()
        {
            const string path = DataFolder + "/Ch12SentinelDuelist.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            // Tanky elite among the cradle sentinels — sits between the skirmisher mook (60 HP/9 dmg)
            // and the chapter's actual boss (320 HP/28 dmg). postureMaxFraction is exercised here for the
            // first time in the project (see Ch12UpgradeToSentinelDuelist, which adds the PostureMeter
            // that makes it do anything).
            def.maxHealth = 150f;
            def.moveSpeed = 1.5f;
            def.attackRange = 1.8f;
            def.telegraphTime = 0.85f;
            def.activeTime = 0.8f;
            def.recoverTime = 0.65f;
            def.staggerTime = 1.3f;
            def.attackCooldown = 0.75f;
            def.damage = 16f;
            def.postureMaxFraction = 0.5f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        /// <summary>Upgrades one Tier-2 cradle-sentinel <see cref="Enemy"/> into the "Sentinel Duelist"
        /// elite variant: swaps its definition for the tanky <paramref name="sentinelDuelistDef"/>,
        /// renames it, and adds <see cref="PostureMeter"/> + <see cref="PatternedDuelist"/> (mirrors
        /// Ch6Builder's caradocEnemy — <c>PatternedDuelist</c> reads repeated-side hits). PostureMeter is
        /// required for <see cref="EnemyDefinition.postureMaxFraction"/> to have any effect (Enemy.Awake
        /// only calls <c>PostureMeter.Configure</c> when one is present on the same GameObject). Callable
        /// a second time without duplicating components (guarded by GetComponent checks) so a live-scene
        /// patch and a future rebuild agree.</summary>
        private static void Ch12UpgradeToSentinelDuelist(Enemy enemy, EnemyDefinition sentinelDuelistDef)
        {
            enemy.gameObject.name = "SentinelDuelist";
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", sentinelDuelistDef);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (enemy.GetComponent<PostureMeter>() == null) enemy.gameObject.AddComponent<PostureMeter>();
            if (enemy.GetComponent<PatternedDuelist>() == null) enemy.gameObject.AddComponent<PatternedDuelist>();
        }

        /// <summary>Wires a <see cref="HiveCascadeController"/> over the given squad (previously wired
        /// nowhere in the project — see class doc). Built active so its own OnEnable/Initialize can
        /// stagger member timers; harmless while every member is still inactive (SetActive(false) above),
        /// since only the DefeatEnemies step's auto-activation actually starts the fight.</summary>
        private static HiveCascadeController Ch12BuildHiveCascade(List<MeleeAttacker> members)
        {
            var go = new GameObject("CradleHiveCascade");
            var hive = go.AddComponent<HiveCascadeController>();
            var so = new SerializedObject(hive);
            SetObjectRefList(so, "members", members.ConvertAll(m => (Object)m));
            so.ApplyModifiedPropertiesWithoutUndo();
            return hive;
        }

        // ---- Dialogue: build via the shared helper, then wire ch12 voice clips ourselves. ----

        private static DialoguePlayer Ch12BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter12Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch12WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter12] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch12WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter12Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch12VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch12VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement. ----

        /// <summary>Mirrors Ch10PlaceStoryNpc: a decorative/story NPC with no combat component.</summary>
        private static GameObject Ch12PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0; the throne-tier sits at pos.y — re-add it
            // (mirrors Ch6/Ch10/Ch11's non-zero floor height convention).
            go.transform.position += Vector3.up * pos.y;

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>The last node: a decorative amber hologram (mirrors Ch11's ArkshipCore hologram) —
        /// never named, never fought, freed by dialogue + a campaign flag only (see class summary).</summary>
        private static void Ch12PlaceLastNode(Vector3 pos)
        {
            BuildHologram(null, pos);
        }

        /// <summary>
        /// A boss built from a Named-mesh prefab with no combat rig of its own (mirrors
        /// Ch10BuildNamedBoss/Ch11BuildNamedBoss): instantiates the mesh, synthesizes an
        /// ArmR/Sword/Blade/BladeTip hierarchy, then wires <see cref="Enemy"/> onto it. Returned inactive
        /// is the caller's job.
        /// </summary>
        private static Enemy Ch12BuildNamedBoss(string prefabPath, Vector3 pos, string displayName, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y; // re-add non-zero floor height (mirrors Ch10/Ch11)
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the approach (-Z)

            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1.1f, 0f);
            cc.height = 2.4f;
            cc.radius = 0.5f;

            go.AddComponent<Health>();
            var bodyRenderer = go.GetComponentInChildren<Renderer>();

            var armRGo = new GameObject("ArmR");
            armRGo.transform.SetParent(go.transform, false);
            armRGo.transform.localPosition = new Vector3(0.35f, 1.4f, 0f);
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(armRGo.transform, false);
            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(swordGo.transform, false);
            var bladeTipGo = new GameObject("BladeTip");
            bladeTipGo.transform.SetParent(bladeGo.transform, false);
            bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);

            var enemy = go.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "weapon", armRGo.transform);
            SetObjectRef(so, "bladeTip", bladeTipGo.transform);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            SetObjectRef(so, "target", playerHealth);
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        // ---- The cryo-vault: tier platforms + tilted ramp connectors (mirrors Ch10's Ninefold idiom). ----

        private static void Ch12BuildTier(Transform parent, string name, Vector3 center, Color floorColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.position = center + new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(Ch12TierHalfWidth * 2f, 0.4f, Ch12TierHalfWidth * 2f);
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            BuildProp(parent, name + "_PillarA", center + new Vector3(-3f, 0.9f, -1.5f), new Vector3(0.5f, 1.8f, 0.5f), floorColor * 0.7f);
            BuildProp(parent, name + "_PillarB", center + new Vector3(3f, 0.9f, 1.5f), new Vector3(0.5f, 1.8f, 0.5f), floorColor * 0.7f);
            BuildAccentPointLight(name + "_Glow", center + new Vector3(0f, 2.2f, 0f), floorColor, 1f, 9f);
        }

        private static void Ch12BuildRamp(Transform parent, string name, Vector3 from, Vector3 to, float width)
        {
            Vector3 mid = (from + to) * 0.5f;
            float rise = to.y - from.y;
            float run = to.z - from.z;
            float length = Mathf.Sqrt(rise * rise + run * run);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent, false);
            ramp.transform.position = mid;
            ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(width, 0.4f, length);
            TintShared(ramp.GetComponent<Renderer>(), new Color(0.26f, 0.28f, 0.32f));
        }

        /// <summary>A row of small blue-lit stasis-cradle props along a tier — the count grows with depth
        /// (2/5/8 across the three tiers), the architecture's own "count the cradles" reveal. Purely
        /// decorative (no collider needed beyond the shared BuildProp box).</summary>
        private static void Ch12BuildCradleRow(Transform parent, string name, Vector3 center, int count)
        {
            var cradleColor = new Color(0.3f, 0.55f, 0.85f);
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-Ch12TierHalfWidth + 1f, Ch12TierHalfWidth - 1f, count <= 1 ? 0.5f : i / (float)(count - 1));
                BuildProp(parent, name + "_Cradle", center + new Vector3(x, 0.9f, -2.5f), new Vector3(0.7f, 1.8f, 0.6f), cradleColor);
            }
        }

        /// <summary>A worldspace "CHAPTER 12 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch12BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 12 COMPLETE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 220f);
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "CHAPTER 12 COMPLETE";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 54;
            label.color = new Color(0.9f, 0.92f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            canvasGo.SetActive(false);
            return canvasGo;
        }
    }
}

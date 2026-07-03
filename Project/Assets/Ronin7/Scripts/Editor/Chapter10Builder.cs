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
    /// Chapter 10 ("The Ledger of Rust") scene builder. One self-contained scene along +Z: the Cairn
    /// briefing (voice-only) -> the Ninefold shafts (a six-tier switchback mine descent, rusted syndicate
    /// squatters aging into a signal-dead Program strongroom) -> the Archive at shaft seven (Cassie-04's
    /// living-archive node guarded by the leashed keeper Sever/Ninja-2 — a boss duel that frees the
    /// Phase-step ability and recruits Ally #7) -> the ship entrance (Vess's ambush -> a railed duel ->
    /// the mercy-mirror spare, Ally #8) -> the target list (Cassie's cross-index lights the Ch11/Ch12
    /// nodes). First node-run of Act III's second chapter, third permanent ability unlock.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch10</c>.
    ///
    /// CREW-PRESENCE DECISION: canon has Cipher descend with only Gryph on the ground ("Gryph is a
    /// present allied NPC on the descent... deep-shaft warrior"), with Kessler/Morrigan/Coral/Sable
    /// holding the comm from the Cairn — so Kessler/Morrigan/Coral Vex/Sable/Resh/Iris/Mira are
    /// voice-only here (DialoguePlayer speaker labels, no physical NPC), the same convention every prior
    /// chapter uses for crew who stay aboard. Only Ronin-7 (the player), Gryph, Cassie-04, Sever/Ninja-2,
    /// and Vess get physical placement.
    ///
    /// GRYPH PHYSICAL-PLACEMENT DECISION: Gryph is placed ONCE, near the descent entrance, as an
    /// <see cref="AllyCombatant"/> (mirrors Ch9's Gryph/Rook — fights alongside during the two mook
    /// encounters on the descent). He deliberately does NOT physically follow into the Archive, even
    /// though the source script stages him "at the cavern mouth": Sever's fight is written as a solo
    /// operative duel, and <c>AllyCombatant.RetargetNearestEnemy</c> has no max engagement range (the
    /// same gap Ch9 flagged for Gryph/Vane) — an ally standing near an active boss would auto-join a
    /// fight the source script frames as Cipher alone. All of Gryph's later lines (the strongroom guide
    /// barks, the ship-entrance approval, the target-list heading) play as decoupled DialoguePlayers at
    /// their beat's location — the same "dialogue panel decoupled from the physical NPC" convention every
    /// chapter already uses for crew comm and, in Ch7/Ch9, for a single-placement NPC's later lines.
    ///
    /// CASSIE-04 STAGING: decorative set-dressing at the Archive (no Health, no combat — canon: she is
    /// racked in cabling, not a combatant), built active from scene start, mirroring Ch9's Sable-at-the-
    /// Tide-Depths staging exactly (no separate "free her from the rack" animation system — greybox scope
    /// cut, consistent with that precedent).
    ///
    /// SEVER/NINJA-2 STAGING: the chapter's sole boss and hardest fight, a plain <see cref="Enemy"/> kill
    /// like Ch9's Vane/Wraith-6 (the source script is explicit: "Sever CANNOT be spared, talked down, or
    /// subdued... this is canon and load-bearing"), built INACTIVE and revealed by the DefeatEnemies
    /// step's own auto-activation (mirrors Ch4's "no separate Trigger step needed" DefeatEnemies
    /// convention) after the confrontation dialogue plays. Resolves to the real Named prefab
    /// <c>Sever_Ninja-2.prefab</c> (glob-confirmed on disk at authoring time).
    ///
    /// VESS / DUELYIELD DECISION: mechanically identical to Ch4's Kerrax — a <see cref="DuelYield"/> boss
    /// (yield at low health, accepted once the katana is sheathed) — reused rather than reinvented. The
    /// source script's three-phase "ambush / railed duel / scripted mercy" staging is honored at the
    /// NARRATIVE layer only: the dialogue frames Vess as the one extending mercy (she lowers the blade
    /// she already had at Cipher's throat), while the underlying mechanic is the same
    /// low-health-yield-then-accept FSM every other DuelYield boss in this saga uses. Building a bespoke
    /// "player cannot die, opponent cannot be executed, force-resolve at a clash-lock" rail (as the
    /// source's production note describes) would duplicate DuelYield's own job with a second FSM for one
    /// fight — simplest mechanization, matching the project's precedent of mechanizing a spare/mercy boss
    /// with the one component built for it. Resolves to the placeholder <c>Vess.prefab</c> (Humanoid
    /// archetype, already spec'd in <see cref="PlaceholderCharacterBuilder"/>).
    ///
    /// PHASE-STEP WIRING: killing Sever advances a <c>DefeatEnemies</c> step into a Trigger step that
    /// activates an <see cref="AbilityGranter"/> (phase_step) — canon: "Sever's freed blade-shadow floods
    /// into Echo... Phase-step." <c>AbilityGranter.OnEnable</c> unlocks it immediately; the right-
    /// secondaryButton Tap activation becomes live for the rest of the game from that point. The rig
    /// carries <see cref="WeakpointSight"/>, <see cref="OverdriveController"/>, AND
    /// <see cref="PhaseStepController"/> from scene start via <see cref="AttachPlayerAbilities"/> (all
    /// three self-gate, harmless before their respective unlocks).
    ///
    /// NINEFOLD DESCENT GEOMETRY: six switchback tiers (alternating x offsets, descending y) linked by
    /// tilted ramp colliders the existing <c>ContinuousLocomotion</c>/CharacterController already climbs
    /// — the same "no new locomotion mechanic" idiom Ch6's ascent (inverted here) and Ch4's Deepworks
    /// descent both use. Tiers 1-3 read as a rusted working mine (warm rust tint); tiers 4-6 transition
    /// into signal-dead Program strongroom (cooling to blue-grey), matching the source script's "the rust
    /// ends here" beat. Shaft seven (the Archive) is a proper enclosed cavern-room (floor/ceiling/walls),
    /// distinct from the open, wall-less tier platforms — the boss arena, not a traversal tier. A single
    /// ramp abstracts the return climb from the Archive back up to the ship entrance (the source script's
    /// "the crew starts the long climb back up the nine tiers" is compressed into one ReachTrigger +
    /// traversal segment rather than rebuilding six tiers a second time in reverse — mirrors Ch9's
    /// "climb out" reach point abstracting return travel).
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch10ScenePath = SceneFolder + "/Ch10_LedgerOfRust.unity";
        private const string Ch10VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch10TierHalfWidth = 5f;
        private const float Ch10ArchiveHalfWidth = 10f;
        private const float Ch10ShipEntranceHalfWidth = 8f;

        // Named-cast prefabs. Gryph resolves to a real Tripo image->3D mesh already baked to disk (same
        // Ch9GryphPrefab this chapter re-uses); Cassie-04 and Sever_Ninja-2 resolve to real Named
        // prefabs (glob-confirmed on disk at authoring time). Vess resolves to PlaceholderCharacterBuilder's
        // existing Humanoid-archetype spec (no real art yet).
        private const string Ch10CassiePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Cassie-04.prefab";
        private const string Ch10SeverPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Sever_Ninja-2.prefab";
        private const string Ch10VessPrefab = PlaceholderCharacterFolder + "/Vess.prefab";
        private const string Ch10EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 10 — The Ledger of Rust", priority = 210)]
        public static void BuildChapter10LedgerOfRust()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var guardDef = Ch10EnsureSyndicateGuardDefinition();
            var automatonDef = Ch10EnsureMineAutomatonDefinition();
            var severDef = Ch10EnsureSeverDefinition();
            var vessDef = Ch10EnsureVessDefinition();

            // ---- Lighting: rust-amber upper shafts cooling to Program-metal blue toward the strongroom,
            // a cold cyan data-light at the Archive, and open gutted-sky daylight at the ship entrance. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.65f, 0.5f);
            light.intensity = 0.4f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.06f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.05f, 0.05f);
            RenderSettings.fogDensity = 0.015f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 6f), new Color(0.9f, 0.7f, 0.4f), 1f, 10f);
            BuildAccentPointLight("Tier1Light", new Vector3(2f, 0.5f, 34f), new Color(0.85f, 0.6f, 0.35f), 1.2f, 12f);
            BuildAccentPointLight("Tier2Light", new Vector3(-2f, -1f, 48f), new Color(0.8f, 0.55f, 0.3f), 1.3f, 12f);
            BuildAccentPointLight("Tier3Light", new Vector3(1f, -2.5f, 62f), new Color(0.5f, 0.55f, 0.65f), 1.4f, 12f);
            BuildAccentPointLight("Tier4Light", new Vector3(-1f, -4f, 76f), new Color(0.35f, 0.5f, 0.7f), 1.5f, 12f);
            BuildAccentPointLight("Tier5Light", new Vector3(2f, -5.5f, 90f), new Color(0.3f, 0.5f, 0.75f), 1.6f, 12f);
            BuildAccentPointLight("ArchiveLight0", new Vector3(-4f, -6.4f, 104f), new Color(0.3f, 0.85f, 0.9f), 1.8f, 16f);
            BuildAccentPointLight("ArchiveLight1", new Vector3(4f, -6.4f, 112f), new Color(0.3f, 0.85f, 0.9f), 1.8f, 16f);
            BuildAccentPointLight("ShipEntranceLight0", new Vector3(-4f, 2.4f, 145f), new Color(0.9f, 0.85f, 0.7f), 1.4f, 16f);
            BuildAccentPointLight("ShipEntranceLight1", new Vector3(4f, 2.4f, 155f), new Color(0.9f, 0.85f, 0.7f), 1.4f, 16f);

            // ---- World root. ----
            var worldGo = new GameObject("Ninefold");
            var world = worldGo.transform;

            // ---- Spawn platform (mine entrance). ----
            BuildFloorCeiling(world, "SpawnGround", new Vector3(0f, 0f, 10f), new Vector3(Ch10TierHalfWidth * 2f, 0f, 20f),
                new Color(0.16f, 0.12f, 0.08f), new Color(0.06f, 0.05f, 0.05f));

            // ---- Six switchback tiers, alternating x, descending y — tiers 1-3 honest rusted mine,
            // tiers 4-6 cooling into Program-original strongroom. ----
            Vector3[] tiers =
            {
                new Vector3(2f, -1.5f, 34f),   // Tier 1
                new Vector3(-2f, -3f, 48f),    // Tier 2 — syndicate guard skirmish
                new Vector3(1f, -4.5f, 62f),   // Tier 3 — the rust ends here (sealed door)
                new Vector3(-1f, -6f, 76f),    // Tier 4
                new Vector3(2f, -7.5f, 90f),   // Tier 5 — mine automata
                new Vector3(0f, -9f, 104f),    // Tier 6 (shaft-mouth into the Archive at tier seven)
            };
            var rustColor = new Color(0.42f, 0.28f, 0.16f);
            var vaultColor = new Color(0.2f, 0.26f, 0.36f);
            for (int i = 0; i < tiers.Length; i++)
            {
                float t = Mathf.InverseLerp(0, tiers.Length - 1, i);
                Color tierColor = Color.Lerp(rustColor, vaultColor, t);
                Ch10BuildTier(world, $"Tier{i + 1}", tiers[i], tierColor);
            }
            Ch10BuildRamp(world, "Ramp0", new Vector3(0f, 0f, 18f), tiers[0], Ch10TierHalfWidth * 2f);
            for (int i = 0; i < tiers.Length - 1; i++)
                Ch10BuildRamp(world, $"Ramp{i + 1}", tiers[i], tiers[i + 1], Ch10TierHalfWidth * 2f);

            // The sealed, handle-less Program door at tier three — the "rust ends here" beat.
            BuildProp(world, "SealedProgramDoor", tiers[2] + new Vector3(0f, 1.8f, 5f),
                new Vector3(Ch10TierHalfWidth * 2f - 1f, 3.4f, 0.4f), new Color(0.22f, 0.26f, 0.34f));

            // ---- The Archive (shaft seven): an enclosed strongroom-cavern, distinct from the open tier
            // platforms — Sever's boss arena and Cassie's node. ----
            var archiveCenter = new Vector3(0f, -9f, 108f);
            BuildFloorCeiling(world, "Archive", archiveCenter, new Vector3(Ch10ArchiveHalfWidth * 2f, 0f, 24f),
                new Color(0.05f, 0.07f, 0.09f), new Color(0.03f, 0.04f, 0.05f));
            BuildWall(world, "Archive_WallW", archiveCenter + new Vector3(-Ch10ArchiveHalfWidth, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 24f));
            BuildWall(world, "Archive_WallE", archiveCenter + new Vector3(Ch10ArchiveHalfWidth, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 24f));
            BuildWall(world, "Archive_WallN", archiveCenter + new Vector3(0f, RoomH / 2f, 12f), new Vector3(Ch10ArchiveHalfWidth * 2f, RoomH, 0.2f));
            Ch10BuildArchiveRackRow(world, archiveCenter);

            // ---- The return climb: one long, gentle ramp abstracts the climb back up the nine tiers to
            // the ship entrance (mirrors Ch9's single "climb out" reach point rather than rebuilding
            // travel) — a wide run keeps the incline comfortable (~13 degrees, in line with Ch6's ascent
            // ramps) despite recovering the full 9m of tier descent in one segment. ----
            var shipEntranceCenter = new Vector3(0f, 0f, 170f);
            Ch10BuildRamp(world, "ReturnRamp", archiveCenter + new Vector3(0f, 0f, 12f), shipEntranceCenter + new Vector3(0f, 0f, -12f), Ch10ShipEntranceHalfWidth * 2f);

            // ---- The ship entrance: an open shaft-mouth under the gutted sky (exterior, no walls —
            // mirrors Ch6's Iron Yard "exterior under the mountain sky" pattern). ----
            var shipGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shipGround.name = "ShipEntrance_Ground";
            shipGround.transform.SetParent(world, true);
            shipGround.transform.position = shipEntranceCenter + new Vector3(0f, -0.1f, 0f);
            shipGround.transform.localScale = new Vector3(Ch10ShipEntranceHalfWidth * 2f, 0.2f, 24f);
            TintShared(shipGround.GetComponent<Renderer>(), new Color(0.4f, 0.36f, 0.3f));
            BuildProp(world, "SalvageRig", shipEntranceCenter + new Vector3(3f, 1.4f, 8f), new Vector3(3f, 2.8f, 4f), new Color(0.35f, 0.32f, 0.28f));

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, and every shipped ability chain
            // (weakpoint-sight from Ch7, Overdrive from Ch9, Phase-step granted this chapter — all
            // self-gate). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, -4f, 84f);
            bounds.radius = 150f;

            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act III — no rack-wake beat, matching Ch9's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch10EchoBladePrefab);
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- Gryph: placed once near the descent entrance, fights the two mook encounters
            // alongside the player, does not physically follow into the Archive (see class summary).
            // Reuses Ch9GryphPrefab (already-recruited ally, no new named-cast constant needed). ----
            Ch10PlaceAlly(Ch9GryphPrefab, new Vector3(2f, 0f, 18f), "Gryph");

            // ---- Syndicate guards (tier 2) and mine automata (tier 5): inactive until their
            // DefeatEnemies step auto-activates them (mirrors Ch4's snatch-team convention — no separate
            // Trigger step needed). ----
            Vector3[] guardPositions =
            {
                new Vector3(-4f, -3f, 44f), new Vector3(0f, -3f, 46f), new Vector3(3f, -3f, 50f),
            };
            var guardHealths = new List<Object>();
            foreach (var pos in guardPositions)
            {
                var e = BuildEnemy(pos, playerHealth, guardDef);
                e.gameObject.SetActive(false);
                guardHealths.Add(e.GetComponent<Health>());
            }

            Vector3[] automatonPositions = { new Vector3(-2f, -7.5f, 86f), new Vector3(3f, -7.5f, 92f) };
            var automatonHealths = new List<Object>();
            foreach (var pos in automatonPositions)
            {
                var e = BuildEnemy(pos, playerHealth, automatonDef);
                e.gameObject.SetActive(false);
                automatonHealths.Add(e.GetComponent<Health>());
            }

            // ---- Cassie-04: decorative set-dressing at the Archive, no Health/combat (racked in
            // cabling, not a combatant) — mirrors Ch9's Sable-at-the-Tide-Depths staging exactly. ----
            Ch10PlaceStoryNpc(Ch10CassiePrefab, archiveCenter + new Vector3(0f, 0f, 4f), "Cassie-04");

            // ---- Sever/Ninja-2: the boss, inactive until the DefeatEnemies step auto-activates it
            // (mirrors Ch8BuildWarden/Ch9BuildVane's reveal timing). A plain kill (no DuelYield — the
            // source script is explicit he cannot be spared). ----
            var sever = Ch10BuildNamedBoss(Ch10SeverPrefab, archiveCenter + new Vector3(0f, 0f, -4f), "Sever / Ninja-2", severDef, playerHealth);
            sever.gameObject.SetActive(false);

            // ---- Phase-step ability grant: activated alongside Sever's defeat. ----
            var phaseStepGranterGo = new GameObject("PhaseStepGranter");
            var phaseStepGranter = phaseStepGranterGo.AddComponent<AbilityGranter>();
            var psGranterSo = new SerializedObject(phaseStepGranter);
            psGranterSo.FindProperty("abilityId").stringValue = AbilityId.PhaseStep;
            psGranterSo.ApplyModifiedPropertiesWithoutUndo();
            phaseStepGranterGo.SetActive(false);

            // ---- Vess: the DuelYield boss at the ship entrance, inactive until the Trigger step
            // reveals her (mirrors Ch4BuildKerrax's confront-then-reveal timing). ----
            var vess = Ch10BuildNamedBoss(Ch10VessPrefab, shipEntranceCenter + new Vector3(0f, 0f, 4f), "Vess", vessDef, playerHealth);
            var vessGo = vess.gameObject;
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vessNpcSo = new SerializedObject(vessNpc);
            vessNpcSo.FindProperty("displayName").stringValue = "Vess";
            vessNpcSo.ApplyModifiedPropertiesWithoutUndo();

            var duelYield = vessGo.AddComponent<DuelYield>();
            var dySo = new SerializedObject(duelYield);
            SetObjectRef(dySo, "opponent", vess.GetComponent<Health>());
            dySo.FindProperty("yieldThreshold").floatValue = 0.2f; // yields at low health, not death
            SetObjectRefList(dySo, "disableOnYield", new List<Object> { vess });
            SetObjectRef(dySo, "sword", swordGrab);
            dySo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dySo.ApplyModifiedPropertiesWithoutUndo();
            vessGo.SetActive(false); // Activated only by the Trigger step.

            // ---- Reach points. ----
            var syndicateAreaReachGo = new GameObject("SyndicateAreaReachPoint");
            syndicateAreaReachGo.transform.position = tiers[1] + new Vector3(0f, 1f, 0f);
            var strongroomReachGo = new GameObject("StrongroomReachPoint");
            strongroomReachGo.transform.position = tiers[2] + new Vector3(0f, 1f, 0f);
            var automataAreaReachGo = new GameObject("AutomataAreaReachPoint");
            automataAreaReachGo.transform.position = tiers[4] + new Vector3(0f, 1f, 0f);
            var archiveReachGo = new GameObject("ArchiveReachPoint");
            archiveReachGo.transform.position = archiveCenter + new Vector3(0f, 1f, -8f);
            var shipEntranceReachGo = new GameObject("ShipEntranceReachPoint");
            shipEntranceReachGo.transform.position = shipEntranceCenter + new Vector3(0f, 1f, 0f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch10BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch10_beat0_briefing", talkRef);
            var dlgDescent = Ch10BuildDialogue("Dialogue_Beat1_Descent", new Vector3(0f, 1f, 20f), "ch10_beat1_descent", talkRef);
            var dlgStrongroom = Ch10BuildDialogue("Dialogue_Beat1_Strongroom", tiers[2] + new Vector3(0f, 1f, 0f), "ch10_beat1_strongroom", talkRef);
            var dlgKeeper = Ch10BuildDialogue("Dialogue_Beat2_Keeper", archiveCenter + new Vector3(0f, 1f, -6f), "ch10_beat2_keeper", talkRef);
            var dlgKill = Ch10BuildDialogue("Dialogue_Beat2_Kill", archiveCenter + new Vector3(0f, 1f, -4f), "ch10_beat2_kill", talkRef);
            var dlgPhaseStep = Ch10BuildDialogue("Dialogue_Beat2_PhaseStep", archiveCenter + new Vector3(0f, 1f, -2f), "ch10_beat2_phasestep", talkRef);
            var dlgRecruit = Ch10BuildDialogue("Dialogue_Beat2_Recruit", archiveCenter + new Vector3(0f, 1f, 2f), "ch10_beat2_recruit", talkRef);
            var dlgNinjaHistory = Ch10BuildDialogue("Dialogue_Beat3_NinjaHistory", archiveCenter + new Vector3(0f, 1f, 4f), "ch10_beat3_ninja_history", talkRef);
            var dlgReading = Ch10BuildDialogue("Dialogue_Beat3_Reading", archiveCenter + new Vector3(0f, 1f, 6f), "ch10_beat3_reading", talkRef);
            var dlgAmbush = Ch10BuildDialogue("Dialogue_Beat4_Ambush", shipEntranceCenter + new Vector3(0f, 1f, 2f), "ch10_beat4_ambush", talkRef);
            var dlgChoice = Ch10BuildDialogue("Dialogue_Beat4_Choice", shipEntranceCenter + new Vector3(0f, 1f, 4f), "ch10_beat4_choice", talkRef);
            var dlgTargetList = Ch10BuildDialogue("Dialogue_Beat5_TargetList", shipEntranceCenter + new Vector3(0f, 1f, 6f), "ch10_beat5_targetlist", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ch10_complete AND both
            // recruit flags together, mirroring how Ch9 combined its ally-recruit flags with completion. ----
            var completeCanvasGo = Ch10BuildCompleteCanvas(shipEntranceCenter + new Vector3(0f, 1.4f, 10f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = shipEntranceCenter + new Vector3(0f, 1f, 9f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 3;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch10_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "cassie_recruited";
            flagsProp.GetArrayElementAtIndex(2).stringValue = "vess_recruited";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 10 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 24;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Ninefold Shafts (descent begins)", dlgDescent);
            AuthorReachStep(steps, n++, "ReachTrigger: The Syndicate Guard Tier", syndicateAreaReachGo.transform, 5f);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Syndicate Guards", guardHealths);
            AuthorReachStep(steps, n++, "ReachTrigger: The Strongroom Threshold", strongroomReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Strongroom (rust ends, handle-less door)", dlgStrongroom);
            AuthorReachStep(steps, n++, "ReachTrigger: The Mine Automata Tier", automataAreaReachGo.transform, 5f);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Mine Automata", automatonHealths);
            AuthorReachStep(steps, n++, "ReachTrigger: The Archive (shaft seven)", archiveReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat2: Cassie-04 and Her Keeper (confrontation)", dlgKeeper);
            AuthorDefeatStep(steps, n++, "Beat2: The Duel (Sever/Ninja-2, boss)", new List<Object> { sever.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat2: Rest, Brother (aftermath)", dlgKill);
            AuthorTriggerStep(steps, n++, "Trigger: Phase-step Granted (Sever's freed blade-shadow)", phaseStepGranterGo);
            AuthorDialogueStep(steps, n++, "Beat2: Phase-step (Echo names the gift)", dlgPhaseStep);
            AuthorDialogueStep(steps, n++, "Beat2: Cassie Freed (Ally #7)", dlgRecruit);
            AuthorDialogueStep(steps, n++, "Beat3: The Ninja Program (history)", dlgNinjaHistory);
            AuthorDialogueStep(steps, n++, "Beat3: Reading the First Names", dlgReading);
            AuthorReachStep(steps, n++, "ReachTrigger: The Ship Entrance (climb out)", shipEntranceReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat4: Vess's Ambush", dlgAmbush);
            AuthorTriggerStep(steps, n++, "Trigger: Activate Vess (the boss)", vessGo);
            AuthorPromptStep(steps, n++, "Prompt: Vess Duel (yield + sheathe)", null);
            AuthorDialogueStep(steps, n++, "Beat4: The Mercy-Mirror (Ally #8)", dlgChoice);
            AuthorDialogueStep(steps, n++, "Beat5: The Target List (Ch11/Ch12 nodes lit)", dlgTargetList);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flags + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The duel's acceptance advances the mission out of the null-prompt duel step.
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
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
            EditorSceneManager.SaveScene(scene, Ch10ScenePath);
            EnsureScenesInBuild(Ch10ScenePath);

            Debug.Log($"[Space Samurai] Chapter 10 built at {Ch10ScenePath}. " +
                      "The Ninefold shafts (6-tier switchback descent, rust cooling into Program " +
                      "strongroom; syndicate guards then mine automata) -> the Archive at shaft seven " +
                      "(Sever/Ninja-2 boss duel grants Phase-step, Cassie-04 freed as Ally #7) -> the " +
                      "ship entrance (Vess's DuelYield ambush, mercy-mirror spare, Ally #8) -> the target " +
                      "list (Ch11 bone-canyon + Ch12 cryo-vault nodes lit). 24 mission steps. Cassie-04 " +
                      "and Sever_Ninja-2 resolve to real Named prefabs; Vess falls back to " +
                      "PlaceholderCharacterBuilder's existing Humanoid-archetype spec.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch9EnsureCoilRaiderDefinition). ----

        private static EnemyDefinition Ch10EnsureSyndicateGuardDefinition()
        {
            const string path = DataFolder + "/Ch10SyndicateGuard.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 50f;
            def.damage = 8f;
            def.moveSpeed = 1.5f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch10EnsureMineAutomatonDefinition()
        {
            const string path = DataFolder + "/Ch10MineAutomaton.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 130f;
            def.damage = 15f;
            def.moveSpeed = 1f;
            def.attackCooldown = 1.1f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch10EnsureSeverDefinition()
        {
            const string path = DataFolder + "/Ch10Sever.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 300f;
            def.damage = 26f;
            def.moveSpeed = 1.7f; // Ninja make: quicker/evasive than Vane's heavier press
            def.attackCooldown = 0.75f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch10EnsureVessDefinition()
        {
            const string path = DataFolder + "/Ch10Vess.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 240f;
            def.damage = 20f;
            def.moveSpeed = 1.5f;
            def.attackCooldown = 0.85f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch10 voice clips ourselves. ----

        private static DialoguePlayer Ch10BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter10Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch10WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter10] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch10WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter10Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch10VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch10VoiceFolder}/{clipName}.wav");
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

        /// <summary>Mirrors Chapter9's Ch9PlaceStoryNpc: a decorative/story NPC with no combat component.</summary>
        private static GameObject Ch10PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0; the Archive sits at pos.y — re-add it
            // (mirrors Ch6's citadel-floor re-add for non-zero floor heights).
            go.transform.position += Vector3.up * pos.y;

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>A friendly co-op combatant NPC (Gryph): no Health by design (AllyCombatant cannot be
        /// damaged), fights alongside the player once enemies are live. Mirrors Ch9PlaceAlly.</summary>
        private static GameObject Ch10PlaceAlly(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);

            var ally = go.AddComponent<AllyCombatant>();
            var so = new SerializedObject(ally);
            SetObjectRef(so, "bodyRenderer", go.GetComponentInChildren<Renderer>());
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>
        /// A boss built from a Named/placeholder mesh with no combat rig of its own (shared by both
        /// Sever/Ninja-2 and Vess): instantiates the mesh, synthesizes an ArmR/Sword/Blade/BladeTip
        /// hierarchy the way Ch6BuildMasterEnemy/Ch7BuildMindspaceBoss/Ch9BuildVane do, then wires
        /// <see cref="Enemy"/> onto it. Returned inactive is the caller's job.
        /// </summary>
        private static Enemy Ch10BuildNamedBoss(string prefabPath, Vector3 pos, string displayName, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0; bosses sit at pos.y (the Archive / ship
            // entrance floor heights) — re-add it (mirrors Ch6BuildMasterEnemy/Ch9BuildVane).
            go.transform.position += Vector3.up * pos.y;
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

        // ---- The Ninefold descent: tier platforms + tilted ramp connectors (mirrors Ch6's terrace/ramp
        // switchback idiom, inverted for a descent). ----

        /// <summary>A tier platform with rock props and a patrol light, centered on <paramref name="center"/>
        /// (world position — <paramref name="parent"/> sits at the world origin, so local and world
        /// positions coincide here). <paramref name="floorColor"/> carries the rust->Program-blue gradient.</summary>
        private static void Ch10BuildTier(Transform parent, string name, Vector3 center, Color floorColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.position = center + new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(Ch10TierHalfWidth * 2f, 0.4f, Ch10TierHalfWidth * 2f);
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            BuildProp(parent, name + "_Rock0", center + new Vector3(-2.2f, 0.5f, -1.5f), new Vector3(0.8f, 1f, 0.8f), floorColor * 0.7f);
            BuildProp(parent, name + "_Rock1", center + new Vector3(2f, 0.4f, 1.6f), new Vector3(0.7f, 0.8f, 0.7f), floorColor * 0.7f);
            BuildAccentPointLight(name + "_PatrolLight", center + new Vector3(0f, 2.2f, 0f), floorColor, 1.2f, 9f);
        }

        /// <summary>A tilted box collider bridging two tier centers — the CharacterController climbs it
        /// like any sloped floor (no new locomotion mechanic). Mirrors Ch6BuildRamp exactly.</summary>
        private static void Ch10BuildRamp(Transform parent, string name, Vector3 from, Vector3 to, float width)
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
            TintShared(ramp.GetComponent<Renderer>(), new Color(0.24f, 0.24f, 0.26f));
        }

        /// <summary>Tinted, data-lit rack props lining the Archive — the ledger of the erased made
        /// physical, mirrors Ch9's shadow-rack row / Ch7's reliquary racks.</summary>
        private static void Ch10BuildArchiveRackRow(Transform parent, Vector3 center)
        {
            var rackColor = new Color(0.15f, 0.55f, 0.6f);
            float[] zOffsets = { -8f, -4f, 4f, 8f };
            foreach (float z in zOffsets)
            {
                BuildProp(parent, "ArchiveRack", center + new Vector3(-8f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.2f), rackColor);
                BuildProp(parent, "ArchiveRack", center + new Vector3(8f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.2f), rackColor);
            }
        }

        /// <summary>A worldspace "CHAPTER 10 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch10BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 10 COMPLETE Canvas");
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
            label.text = "CHAPTER 10 COMPLETE";
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

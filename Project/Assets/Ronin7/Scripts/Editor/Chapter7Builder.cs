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
    /// Chapter 7 ("Forgotten Names") scene builder. One self-contained scene, a single vaulted
    /// data-reliquary hall threaded along +Z: the breach/outer stacks (a combat gauntlet against rival
    /// scavengers and archive-defense automata) -> the tended core (Coral Vex, the trust-test turn) ->
    /// deeper into the core (the Wraith-line forebear reveal, Ally #4) -> the deep archive (the kept-
    /// shadows reveal, the berserk oldest blade, a mindspace duel against its dead previous owner that
    /// grants permanent weakpoint-sight) -> the read bench (sabotage-was-dissent reveal, Ladder A rung
    /// 3, the Silent Garden hook into Ch8). Opens Act II's second chapter (EP13-14).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch7</c>.
    ///
    /// CREW-PRESENCE DECISION: canon has Cipher breach the hulk alone ("Cipher breaches the hull alone
    /// and works inward... Kessler, Iris, and Mera hold the comm from the ship"), so Kessler/Mera Voss/
    /// Morrigan are voice-only here (DialoguePlayer speaker labels, no physical NPC) — the same
    /// convention Ch5/Ch6 use for crew who stay aboard. Resh/Iris/Mira appear only in the Beat 0 Cairn
    /// briefing, also voice-only (that beat has no landing party at all). Only Ronin-7 (the player),
    /// Coral Vex, and the mindspace boss get physical placement.
    ///
    /// CORAL VEX PHYSICAL-PLACEMENT DECISION: Coral is placed ONCE, at the tended core (where she's
    /// first met), rather than walked/teleported to each subsequent beat location. Every later Coral
    /// line (the forebear reveal, the kept-shadows reveal, the read-bench sabotage reveal) plays as a
    /// disembodied <see cref="DialoguePlayer"/> positioned at that beat's spot — exactly the "dialogue
    /// panel decoupled from the physical NPC" convention every chapter already uses for crew comm lines
    /// and, in Ch6, for Master Kaelen's post-fight confession versus his fixed tower position. Simpler
    /// than authoring a walk cycle for a single scene, and consistent with the established pattern.
    ///
    /// TRUST-TEST SCOPE CUT: the source script's production note calls for a tracked spare-vs-kill
    /// choice across the outer-stacks gauntlet that swaps Coral's opening Beat 2 line between a warm and
    /// a cold variant. That tracking system (plus authoring the cold-path line) is out of scope for this
    /// pass — <see cref="Chapter7Lines"/> wires the warm/spare-path line unconditionally. Flagged for
    /// the reviewer as a deliberate simplification, not an oversight.
    ///
    /// MINI-BOSS / MINDSPACE DECISION: "gripping" the oldest blade is a ReachTrigger + Trigger step
    /// (walk up, then the mission activates the dive) rather than a bespoke VR grab interaction —
    /// mirrors the project's "no new locomotion/interaction mechanics" precedent (Ch4/Ch6). The
    /// mindspace itself reuses <see cref="MemoryDiveController"/> exactly as Ch3's Kethel-7 playback and
    /// Ch5's massacre dive do: a self-contained geometry island offset far from the main hall, entered
    /// via <see cref="MemoryDiveEntryTrigger"/>, exited via <see cref="MemoryDiveExitTrigger"/>. The
    /// Previous Owner is a plain <see cref="Enemy"/> (no spare condition — canon: "the victory itself is
    /// the mercy"), synthesized with an ArmR/Sword/Blade/BladeTip rig the way
    /// <c>Ch6BuildMasterEnemy</c> does for Named-mesh masters with no combat rig of their own. Because
    /// the whole mindspace root starts inactive, its child Enemy/Health never Awake()s (and can't be
    /// found/attacked) until the dive's Trigger step activates the root — no separate "activate the
    /// boss" trigger object is needed, the cascade from <c>MemoryDiveController.EnterDive</c>'s
    /// <c>diveRoot.SetActive(true)</c> covers it.
    ///
    /// BLADE-RESCUE SIDE-OBJECTIVES SCOPE CUT: the source script's deep-archive side content (freeing
    /// still-coherent kept blades, no duel required) is explicitly non-critical/collectible-style and
    /// seeds a later chapter's payoff, not this pass's deliverables. The deep archive is dressed with
    /// inert lit-rack props for atmosphere; no rescue-interaction system was built.
    ///
    /// ABILITY WIRING: the rig carries <see cref="Ronin7.Player.WeakpointSight"/> +
    /// <see cref="PlayerCombatModifiers"/> from scene start (both self-gate on
    /// <c>CampaignState.HasAbility</c>/default multiplier, so they're harmless before the ability
    /// unlocks). Defeating the mindspace boss advances a <c>DefeatEnemies</c> step into a Trigger step
    /// that activates an <see cref="Ronin7.World.Story.AbilityGranter"/> (weakpoint_sight) alongside the
    /// dive's exit trigger — canon: "defeating the [mindspace boss] frees a blade-shadow that grants
    /// weakpoint-sight." <c>AbilityGranter.OnEnable</c> unlocks it immediately; the toggle (left
    /// controller X) and the 2x damage multiplier become live for the rest of the game from that point.
    ///
    /// GANG WAR POCKET (enemy-variety pass): the outer-stacks gauntlet's own class doc already promises
    /// "rival scavengers and archive-defense automata" fighting each other, but every enemy built above
    /// (<c>scavengerDef</c>/<c>automatonDef</c>) is a plain <see cref="Enemy"/>, which only ever targets
    /// the player — the two-factions premise was narrated, never mechanized. <c>Ch7BuildGangWarPocket</c>
    /// adds a third wave (still one DefeatWaves step — no mission-step change) using
    /// <see cref="FactionCombatant"/>, previously wired nowhere in the project: a turncoat-scavenger cell
    /// and a rogue-drone cell that hunt each other as well as the player.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch7ScenePath = SceneFolder + "/Ch07_ForgottenNames.unity";
        private const string Ch7VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch7HallHalfWidth = 10f;
        private const float Ch7HallEndZ = 142f;

        // Named-cast prefabs (Tripo image->3D pipeline, grounded via FitNamedCharacter — same
        // convention Chapter3-6 use). Coral Vex resolves to a Named prefab if one has been generated
        // (glob found none on disk at authoring time — InstantiateNpc's capsule fallback covers that
        // gap until the art pass runs). The Previous Owner uses PlaceholderCharacterBuilder's existing
        // "The-Previous-Owner" Ethereal-archetype spec (ghost-blue glass orbs) — already defined there
        // in anticipation of this chapter, just not yet baked to disk.
        private const string Ch7CoralPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Coral-Vex.prefab";
        private const string Ch7PreviousOwnerPrefab = PlaceholderCharacterFolder + "/The-Previous-Owner.prefab";
        private const string Ch7EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 07 — Forgotten Names", priority = 207)]
        public static void BuildChapter7ForgottenNames()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var scavengerDef = Ch7EnsureScavengerDefinition();
            var automatonDef = Ch7EnsureAutomatonDefinition();
            var previousOwnerDef = Ch7EnsurePreviousOwnerDefinition();

            // ---- Lighting: a cold, low-lit archive-tomb. Frost-blue key, low ambient; warm accent
            // lights only where the salvager's patched power runs (growing brighter toward the core), so
            // the devotion (kept, lit, tended) reads as the horror the further in the player goes. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.68f, 0.82f);
            light.intensity = 0.35f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.13f);

            // Cold frost-blue archive haze.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.1f, 0.12f, 0.16f);
            RenderSettings.fogDensity = 0.022f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.55f, 0.7f, 0.9f), 1f, 10f);
            BuildAccentPointLight("OuterStacksLight", new Vector3(0f, 2.4f, 28f), new Color(0.55f, 0.7f, 0.9f), 1.2f, 14f);
            BuildAccentPointLight("TendedCoreLight0", new Vector3(-4f, 2.6f, 65f), new Color(1f, 0.88f, 0.6f), 1.6f, 16f);
            BuildAccentPointLight("TendedCoreLight1", new Vector3(4f, 2.6f, 68f), new Color(1f, 0.88f, 0.6f), 1.6f, 16f);
            BuildAccentPointLight("DeepArchiveLight0", new Vector3(-4f, 2.6f, 100f), new Color(1f, 0.9f, 0.65f), 2f, 18f);
            BuildAccentPointLight("DeepArchiveLight1", new Vector3(4f, 2.6f, 108f), new Color(1f, 0.9f, 0.65f), 2f, 18f);
            BuildAccentPointLight("OldestBladeLight", new Vector3(0f, 2.2f, 113f), new Color(0.9f, 0.25f, 0.2f), 1.8f, 10f);

            // ---- World root: one long vaulted hall (floor/ceiling/side walls), racks lining both
            // walls, growing brighter/warmer toward the core (the "reliquary of saints" gradient). ----
            var worldGo = new GameObject("DataReliquary");
            var world = worldGo.transform;

            BuildFloorCeiling(world, "ReliquaryHall", new Vector3(0f, 0f, 70f),
                new Vector3(Ch7HallHalfWidth * 2f, 0f, Ch7HallEndZ + 4f),
                new Color(0.06f, 0.07f, 0.09f), new Color(0.03f, 0.03f, 0.05f));
            BuildWall(world, "ReliquaryHall_WallW", new Vector3(-Ch7HallHalfWidth, RoomH / 2f, 70f), new Vector3(0.2f, RoomH, Ch7HallEndZ + 4f));
            BuildWall(world, "ReliquaryHall_WallE", new Vector3(Ch7HallHalfWidth, RoomH / 2f, 70f), new Vector3(0.2f, RoomH, Ch7HallEndZ + 4f));
            BuildWall(world, "ReliquaryHall_WallS", new Vector3(0f, RoomH / 2f, -2f), new Vector3(Ch7HallHalfWidth * 2f, RoomH, 0.2f));
            BuildWall(world, "ReliquaryHall_WallN", new Vector3(0f, RoomH / 2f, Ch7HallEndZ), new Vector3(Ch7HallHalfWidth * 2f, RoomH, 0.2f));

            Ch7BuildRackRow(world, 8f, 134f, 10f);

            // ---- The oldest blade + read bench: set-dressing anchors for Beat 4/5. ----
            var oldestBladeProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            oldestBladeProp.name = "OldestBlade";
            oldestBladeProp.transform.SetParent(world, true);
            oldestBladeProp.transform.position = new Vector3(0f, 1.2f, 113f);
            oldestBladeProp.transform.localScale = new Vector3(0.06f, 0.02f, 1.1f);
            TintShared(oldestBladeProp.GetComponent<Renderer>(), new Color(0.9f, 0.25f, 0.2f));

            BuildProp(world, "ReadBench", new Vector3(6f, 0.5f, 122f), new Vector3(1.6f, 1f, 1f), new Color(0.2f, 0.22f, 0.26f));
            BuildProp(world, "ReadBench_Rack", new Vector3(6f, 1.1f, 123.5f), new Vector3(1.2f, 1.6f, 0.3f), new Color(0.85f, 0.75f, 0.5f));

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, and the weakpoint-sight ability chain
            // (both self-gate — harmless before Ch7 grants the ability). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            // ZoneBounds clamps the rig's XZ every frame regardless of dive state (Ch3/Ch5 precedent:
            // "covers the hold/ash-world AND the dive island"), so this must reach the mindspace too —
            // otherwise the very first LateUpdate after EnterDive teleports the rig back out of it.
            bounds.center = new Vector3(0f, 3f, 128f);
            bounds.radius = 155f;

            // All shipped permanent abilities (weakpoint-sight from here on) + PlayerCombatModifiers,
            // via the shared helper so no later chapter can drop them. Each self-gates until unlocked.
            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act II — no rack-wake beat, matching Ch5/Ch6's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch7EchoBladePrefab);

            // ---- Coral Vex: placed once at the tended core (see the class summary's placement note). ----
            Ch7PlaceStoryNpc(Ch7CoralPrefab, new Vector3(0f, 0f, 65f), "Coral Vex");

            // ---- Outer stacks combat: scavengers (wave 0) + archive-defense automata (wave 1),
            // inactive until the wave spawner's own proximity trigger arms them. ----
            Vector3[] scavengerPos = { new Vector3(-3f, 0f, 18f), new Vector3(0f, 0f, 21f), new Vector3(3f, 0f, 24f) };
            var scavengers = Ch7BuildWaveEnemies(scavengerPos, playerHealth, scavengerDef);
            Vector3[] automatonPos = { new Vector3(-2f, 0f, 34f), new Vector3(2f, 0f, 38f) };
            var automata = Ch7BuildWaveEnemies(automatonPos, playerHealth, automatonDef);

            // ---- Gang war pocket (wave 2): the class summary's own claim that "the player can play the
            // two factions against each other" was only ever narrated — scavengers/automata above are
            // plain Enemy, which only ever targets the player. FactionCombatant (wired nowhere else in
            // the project) makes it literal: a turncoat-scavenger cell and a rogue-drone cell hunt each
            // OTHER as well as the player. Placed past the automata pocket, before the tended core. ----
            var gangWarHealths = Ch7BuildGangWarPocket();

            // ---- The mindspace: a small fractured dreamscape offset from the main hall (z=250, still
            // inside ZoneBounds' radius above), holding the mini-boss. Starts fully inactive; the whole
            // root cascades active on dive entry, so the boss's Enemy/Health never Awake()s (and can't
            // be targeted) until then. ----
            var mindspaceGo = new GameObject("Mindspace_PreviousOwner");
            var mindspace = mindspaceGo.transform;
            mindspace.position = new Vector3(0f, 0f, 250f);
            var mindspaceFlashback = mindspaceGo.AddComponent<MemoryFlashbackController>();

            BuildFloorCeiling(mindspace, "MindspaceFloor", Vector3.zero, new Vector3(14f, 0f, 14f),
                new Color(0.12f, 0.13f, 0.18f), new Color(0.07f, 0.07f, 0.1f));
            BuildWall(mindspace, "MindspaceWallW", new Vector3(-7f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(mindspace, "MindspaceWallE", new Vector3(7f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(mindspace, "MindspaceWallS", new Vector3(0f, RoomH / 2f, -7f), new Vector3(14f, RoomH, 0.2f));
            BuildWall(mindspace, "MindspaceWallN", new Vector3(0f, RoomH / 2f, 7f), new Vector3(14f, RoomH, 0.2f));
            BuildAccentPointLight("MindspaceLight", new Vector3(0f, 2.6f, 0f) + mindspace.position, new Color(0.5f, 0.6f, 0.9f), 1.4f, 12f);

            var diveEntryPointGo = new GameObject("MindspaceEntryPoint");
            diveEntryPointGo.transform.SetPositionAndRotation(mindspace.position + new Vector3(0f, 1f, -3f), Quaternion.identity);

            var previousOwner = Ch7BuildMindspaceBoss(mindspace, mindspace.position + new Vector3(0f, 0f, 3f), previousOwnerDef, playerHealth);

            mindspaceGo.SetActive(false);

            // ---- Dive exit point: back in the real world, in front of the (now calmed) oldest blade. ----
            var diveExitPointGo = new GameObject("MindspaceExitPoint");
            diveExitPointGo.transform.SetPositionAndRotation(new Vector3(0f, 1f, 112f), Quaternion.Euler(0f, 180f, 0f));

            var diveGo = new GameObject("MindspaceDive");
            var dive = diveGo.AddComponent<MemoryDiveController>();
            var diveSo = new SerializedObject(dive);
            SetObjectRef(diveSo, "diveRoot", mindspaceGo);
            SetObjectRef(diveSo, "diveEntryPoint", diveEntryPointGo.transform);
            SetObjectRef(diveSo, "diveExitPoint", diveExitPointGo.transform);
            SetObjectRef(diveSo, "rigRoot", rig.transform);
            SetObjectRef(diveSo, "flashback", mindspaceFlashback);
            diveSo.ApplyModifiedPropertiesWithoutUndo();

            var enterMindspaceGo = new GameObject("EnterMindspaceTrigger");
            var enterMindspaceTrigger = enterMindspaceGo.AddComponent<MemoryDiveEntryTrigger>();
            var enterSo = new SerializedObject(enterMindspaceTrigger);
            SetObjectRef(enterSo, "dive", dive);
            enterSo.ApplyModifiedPropertiesWithoutUndo();
            enterMindspaceGo.SetActive(false);

            var exitMindspaceGo = new GameObject("ExitMindspaceTrigger");
            var exitMindspaceTrigger = exitMindspaceGo.AddComponent<MemoryDiveExitTrigger>();
            var exitSo = new SerializedObject(exitMindspaceTrigger);
            SetObjectRef(exitSo, "dive", dive);
            exitSo.ApplyModifiedPropertiesWithoutUndo();
            exitMindspaceGo.SetActive(false);

            // ---- Weakpoint-sight ability grant: activated alongside the dive's exit trigger. ----
            var weakpointGranterGo = new GameObject("WeakpointSightGranter");
            var weakpointGranter = weakpointGranterGo.AddComponent<AbilityGranter>();
            var granterSo = new SerializedObject(weakpointGranter);
            granterSo.FindProperty("abilityId").stringValue = AbilityId.WeakpointSight;
            granterSo.ApplyModifiedPropertiesWithoutUndo();
            weakpointGranterGo.SetActive(false);

            // ---- Reach points. ----
            var tendedCoreReachGo = new GameObject("TendedCoreReachPoint");
            tendedCoreReachGo.transform.position = new Vector3(0f, 1f, 58f);
            var deepArchiveReachGo = new GameObject("DeepArchiveReachPoint");
            deepArchiveReachGo.transform.position = new Vector3(0f, 1f, 88f);
            var oldestBladeReachGo = new GameObject("OldestBladeReachPoint");
            oldestBladeReachGo.transform.position = new Vector3(0f, 1f, 111f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch7BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch7_beat0_briefing", talkRef);
            var dlgBreach = Ch7BuildDialogue("Dialogue_Beat1_Breach", new Vector3(0f, 1f, 8f), "ch7_beat1_breach", talkRef);
            var dlgGauntletBark = Ch7BuildDialogue("Dialogue_Beat1_GauntletBark", new Vector3(0f, 1f, 18f), "ch7_beat1_gauntlet_bark", talkRef);
            var dlgCoreAhead = Ch7BuildDialogue("Dialogue_Beat1_CoreAhead", new Vector3(0f, 1f, 50f), "ch7_beat1_core_ahead", talkRef);
            var dlgArchivist = Ch7BuildDialogue("Dialogue_Beat2_Archivist", new Vector3(0f, 1f, 62f), "ch7_beat2_archivist", talkRef);
            var dlgForebear = Ch7BuildDialogue("Dialogue_Beat3_Forebear", new Vector3(0f, 1f, 75f), "ch7_beat3_forebear", talkRef);
            var dlgKeptShadows = Ch7BuildDialogue("Dialogue_Beat4_KeptShadows", new Vector3(0f, 1f, 92f), "ch7_beat4_kept_shadows", talkRef);
            var dlgQuietIt = Ch7BuildDialogue("Dialogue_Beat4_QuietIt", new Vector3(0f, 1f, 111f), "ch7_beat4_quiet_it", talkRef);
            var dlgMindspaceIntro = Ch7BuildDialogue("Dialogue_Beat4_MindspaceIntro", diveEntryPointGo.transform.position, "ch7_beat4_mindspace_intro", talkRef);
            var dlgGift = Ch7BuildDialogue("Dialogue_Beat4_Gift", new Vector3(0f, 1f, 114f), "ch7_beat4_gift", talkRef);
            var dlgSabotage = Ch7BuildDialogue("Dialogue_Beat5_Sabotage", new Vector3(6f, 1f, 123f), "ch7_beat5_sabotage", talkRef);
            var dlgHookout = Ch7BuildDialogue("Dialogue_Beat5_Hookout", new Vector3(6f, 1f, 125f), "ch7_beat5_hookout", talkRef);

            // ---- Outer stacks wave spawner (wave0 = scavengers with the gauntlet bark, wave1 =
            // automata, wave2 = the gang-war pocket). Built active-idle (Ch4's HunterWave lesson: an
            // inactive spawner can't StartCoroutine) — Begin() is called by the DefeatWaves mission step
            // below, and its own proximity poll gates the actual spawn on the player reaching the trigger
            // point. Adding wave2 does not add a mission step — same single DefeatWaves step, one more
            // wave for it to clear. ----
            var outerStacksWaves = new List<List<Health>>
            {
                scavengers.ConvertAll(go => go.GetComponent<Health>()),
                automata.ConvertAll(go => go.GetComponent<Health>()),
                gangWarHealths,
            };
            var outerStacksSpawner = BuildWaveSpawner("OuterStacksWaveSpawner", new Vector3(0f, 0f, 22f), 12f,
                outerStacksWaves, new[] { dlgGauntletBark });

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ch7_complete AND
            // coral_vex_recruited together, mirroring how the episode finales combine an ally-recruit
            // flag with the completion flag at the same trigger. ----
            var completeCanvasGo = Ch7BuildCompleteCanvas(new Vector3(0f, 1.4f, 128f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 127f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 2;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch7_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "coral_vex_recruited";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 7 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 19;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Breach (Echo reads the reliquary)", dlgBreach);

            var wavesStep = steps.GetArrayElementAtIndex(n++);
            wavesStep.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            wavesStep.FindPropertyRelative("label").stringValue = "Beat1: The Outer Stacks Gauntlet (scavengers + automata)";
            wavesStep.FindPropertyRelative("waveSpawner").objectReferenceValue = outerStacksSpawner;

            AuthorDialogueStep(steps, n++, "Beat1: The Core Ahead (Echo hands it over)", dlgCoreAhead);
            AuthorReachStep(steps, n++, "ReachTrigger: The Tended Core", tendedCoreReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Archivist (meeting + turn)", dlgArchivist);
            AuthorDialogueStep(steps, n++, "Beat3: What Came Before Him (Ally #4)", dlgForebear);
            AuthorReachStep(steps, n++, "ReachTrigger: The Deep Archive", deepArchiveReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat4: The Kept Shadows (reveal)", dlgKeptShadows);
            AuthorReachStep(steps, n++, "ReachTrigger: The Oldest Blade", oldestBladeReachGo.transform, 4f);
            AuthorDialogueStep(steps, n++, "Beat4: Quiet It (the choice)", dlgQuietIt);
            AuthorTriggerStep(steps, n++, "Trigger: Grip the Blade (enter the mindspace)", enterMindspaceGo);
            AuthorDialogueStep(steps, n++, "Beat4: The Mindspace Duel Begins", dlgMindspaceIntro);
            AuthorDefeatStep(steps, n++, "Beat4: The Mindspace Duel (mini-boss)",
                new List<Object> { previousOwner.GetComponent<Health>() });
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Mindspace (weakpoint-sight granted)", exitMindspaceGo, weakpointGranterGo);
            AuthorDialogueStep(steps, n++, "Beat4: The Gift (weakpoint-sight, the vow)", dlgGift);
            AuthorDialogueStep(steps, n++, "Beat5: Sabotage Was Dissent (the read bench)", dlgSabotage);
            AuthorDialogueStep(steps, n++, "Beat5: The Silent Garden (hook out)", dlgHookout);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flags + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch7ScenePath);
            EnsureScenesInBuild(Ch7ScenePath);

            Debug.Log($"[Space Samurai] Chapter 7 built at {Ch7ScenePath}. " +
                      "Breach (spawn) -> outer stacks gauntlet (scavengers + automata) -> the tended core " +
                      "(Coral Vex, trust-test turn) -> the forebear reveal (Ally #4, Wraith line) -> the " +
                      "deep archive (kept-shadows reveal) -> the oldest blade -> a mindspace duel against " +
                      "its dead Previous Owner (grants permanent weakpoint-sight) -> the read bench " +
                      "(sabotage-was-dissent, Ladder A rung 3) -> the Silent Garden hook. 19 mission steps. " +
                      "Coral-Vex.prefab and The-Previous-Owner.prefab are not yet baked to disk — both " +
                      "fall back to placeholders (InstantiateNpc's capsule / PlaceholderCharacterBuilder's " +
                      "existing Ethereal spec, respectively) until the art pass runs.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch6EnsureHespaDefinition). ----

        private static EnemyDefinition Ch7EnsureScavengerDefinition()
        {
            const string path = DataFolder + "/Ch7Scavenger.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 50f;
            def.damage = 8f;
            def.moveSpeed = 1.6f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch7EnsureAutomatonDefinition()
        {
            const string path = DataFolder + "/Ch7Automaton.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 140f;
            def.damage = 16f;
            def.moveSpeed = 1.0f;
            def.attackCooldown = 1.0f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch7EnsurePreviousOwnerDefinition()
        {
            const string path = DataFolder + "/Ch7PreviousOwner.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 220f;
            def.damage = 18f;
            def.moveSpeed = 1.4f;
            def.attackCooldown = 0.8f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch7 voice clips ourselves. ----

        private static DialoguePlayer Ch7BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter7Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch7WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter7] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch7WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter7Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch7VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch7VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement (mirrors Chapter6's Ch6PlaceStoryNpc). ----

        private static GameObject Ch7PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0, which matches this chapter's single
            // floor-height hall — no re-add needed (unlike Ch6's elevated citadel floors).

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Builds N enemy instances at the given positions, each inactive until the wave
        /// spawner's own wave activates it (mirrors Ch6BuildCadre).</summary>
        private static List<GameObject> Ch7BuildWaveEnemies(Vector3[] positions, Health playerHealth, EnemyDefinition def)
        {
            var result = new List<GameObject>();
            foreach (var pos in positions)
            {
                var e = BuildEnemy(pos, playerHealth, def);
                e.gameObject.SetActive(false);
                result.Add(e.gameObject);
            }
            return result;
        }

        /// <summary>Builds the outer-stacks gang-war pocket: 3 turncoat-scavenger <see cref="FactionCombatant"/>s
        /// (faction 0) and 3 rogue-drone FactionCombatants (faction 1), positioned past the automata
        /// pocket and before the tended core. Also callable from a live-scene patch utility (see
        /// ChapterEnemyVarietyWirer) — takes no scene-specific refs beyond the fixed hall positions, so
        /// re-running it against an already-built scene reproduces the same 6 combatants.</summary>
        private static List<Health> Ch7BuildGangWarPocket()
        {
            Vector3[] turncoatPos = { new Vector3(-3f, 0f, 44f), new Vector3(-1f, 0f, 47f), new Vector3(-3f, 0f, 50f) };
            Vector3[] roguePos = { new Vector3(3f, 0f, 44f), new Vector3(1f, 0f, 47f), new Vector3(3f, 0f, 50f) };

            var healths = new List<Health>();
            for (int i = 0; i < turncoatPos.Length; i++)
                healths.Add(Ch7BuildFactionCombatant(turncoatPos[i], 0, $"TurncoatScavenger{i}"));
            for (int i = 0; i < roguePos.Length; i++)
                healths.Add(Ch7BuildFactionCombatant(roguePos[i], 1, $"RogueDrone{i}"));
            return healths;
        }

        /// <summary>Builds one self-contained <see cref="FactionCombatant"/> (own Health; unlike
        /// <see cref="Enemy"/> it is not a <see cref="MeleeAttacker"/>, so it carries no
        /// ArmR/Sword/Blade/BladeTip weapon rig — see the class doc). Built inactive; the caller's wave
        /// spawner activates it.</summary>
        private static Health Ch7BuildFactionCombatant(Vector3 position, int factionId, string name)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            root.transform.position = position;
            root.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var bodyRenderer = root.GetComponent<Renderer>();

            var health = root.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 45f;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var combatant = root.AddComponent<FactionCombatant>();
            combatant.SetFaction(factionId);
            var so = new SerializedObject(combatant);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return health;
        }

        /// <summary>
        /// The mindspace mini-boss, built from The Previous Owner's Named-mesh prefab (no combat rig of
        /// its own): instantiates the mesh, synthesizes an ArmR/Sword/Blade/BladeTip hierarchy the way
        /// <c>Ch6BuildMasterEnemy</c> does, then wires <see cref="Enemy"/> onto it.
        /// </summary>
        private static Enemy Ch7BuildMindspaceBoss(Transform parent, Vector3 pos, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(Ch7PreviousOwnerPrefab, pos, "The Previous Owner");
            go.transform.SetParent(parent, true);
            // FitNamedCharacter grounds the feet at world y=0, which already matches the mindspace
            // floor's world height (unlike Ch6's elevated citadel, no floor-height re-add is needed).
            FitNamedCharacter(go);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the entry point (-Z)

            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            go.AddComponent<Health>();
            var bodyRenderer = go.GetComponentInChildren<Renderer>();

            var armRGo = new GameObject("ArmR");
            armRGo.transform.SetParent(go.transform, false);
            armRGo.transform.localPosition = new Vector3(0.3f, 1.3f, 0f);
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(armRGo.transform, false);
            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(swordGo.transform, false);
            var bladeTipGo = new GameObject("BladeTip");
            bladeTipGo.transform.SetParent(bladeGo.transform, false);
            bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);

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

        /// <summary>Tinted rack props lining both walls of the reliquary hall, brightening/warming from
        /// <paramref name="zStart"/> (cold outer stacks) to <paramref name="zEnd"/> (the tended/kept
        /// core) — the script's "reliquary of saints" gradient, done with prop tint (cheap) rather than
        /// one real Light per rack (VR-perf: this hall would otherwise need dozens of point lights).</summary>
        private static void Ch7BuildRackRow(Transform parent, float zStart, float zEnd, float spacing)
        {
            var dimHilt = new Color(0.28f, 0.32f, 0.48f);
            var brightHilt = new Color(0.95f, 0.82f, 0.5f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                float t = Mathf.InverseLerp(zStart, zEnd, z);
                Color hilt = Color.Lerp(dimHilt, brightHilt, t);
                BuildProp(parent, "Rack", new Vector3(-9f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.4f), hilt);
                BuildProp(parent, "Rack", new Vector3(9f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.4f), hilt);
            }
        }

        /// <summary>A worldspace "CHAPTER 7 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch7BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 7 COMPLETE Canvas");
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
            label.text = "CHAPTER 7 COMPLETE";
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

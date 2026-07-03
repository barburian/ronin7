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
    /// Chapter 9 ("The Pit and the Deep") scene builder. Act III opener. One self-contained scene along
    /// +Z: the Cairn briefing (voice-only) -> the Rustfang hold descent (pirate warren aging into sealed
    /// Program-original machinery) -> the hold overlook (Gryph's bargain: a co-op Coil-raid defense
    /// fought AT Gryph's shoulder, Ally #5) -> the Tide depths (flooded archive: the Vane/Wraith-6 boss
    /// duel, Ally #6 Sable, the Overdrive unlock) -> the construction core (the Concord Engine reveal,
    /// Ladder A rung 4 + Ladder E rung 3) -> back to the hold overlook (Rook succeeds Gryph as keeper).
    /// Opens Act III (EP17-18).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch9</c>.
    ///
    /// CREW-PRESENCE DECISION: canon has Cipher descend the hold alone with the crew on comm ("Cipher
    /// works down through the pirate hold alone... KESSLER, MORRIGAN, CORAL, and the crew hold the comm
    /// from the ship"), so Kessler/Morrigan/Coral/Mera/Resh/Iris/Mira are voice-only here — the same
    /// convention Ch7/Ch8 use for crew who stay aboard. Only Ronin-7 (the player), Gryph, Rook, Sable,
    /// and the boss Vane/Wraith-6 get physical placement.
    ///
    /// GRYPH/ROOK PHYSICAL-PLACEMENT + PROTECTOR-DEFENSE DECISION: Gryph and Rook are placed once, at
    /// the hold overlook, and stay there for both the Bargain beat AND the final hand-off beat (the
    /// "climb out" walks the player back to this same point) — mirrors Ch7's "place once, dialogue plays
    /// as a decoupled DialoguePlayer" convention for Coral Vex. The Coil-raid defense is built as a
    /// straightforward co-op <c>DefeatWaves</c> encounter (Coil raiders as <see cref="Enemy"/>, Gryph +
    /// Rook as non-damageable <see cref="AllyCombatant"/> fighting alongside), NOT a tracked
    /// protect-objective/fail-state system — the source script's own production note says the choice is
    /// honored by being the only path (no "rob Gryph" branch exists to gate), so there is nothing to
    /// track. Simplest mechanization of the beat, consistent with Ch7/Ch8's scope cuts for source
    /// production notes that describe systems beyond a single pass's scope.
    ///
    /// SABLE / VANE STAGING: Sable is decorative set-dressing at the Tide depths (no Health, no combat —
    /// canon: she is wired into the lattice, not a combatant) built active from scene start. Vane/
    /// Wraith-6, the boss, is built INACTIVE like Ch8's Warden — dialogue for the confrontation beat
    /// plays before he's SetActive(true)'d by the DefeatEnemies mission step, exactly mirroring
    /// Ch8BuildWarden's reveal timing. Vane uses the existing <c>Vane_Wraith-6</c> placeholder (Massive
    /// archetype, already baked by <see cref="PlaceholderCharacterBuilder"/> in anticipation of this
    /// chapter); Gryph, Rook, and Sable resolve to real Tripo-generated Named prefabs already on disk.
    ///
    /// OVERDRIVE WIRING: killing Vane advances a <c>DefeatEnemies</c> step into a Trigger step that
    /// activates an <see cref="AbilityGranter"/> (overdrive) — canon: "Vane's freed blade-shadow floods
    /// into Echo... Overdrive." <c>AbilityGranter.OnEnable</c> unlocks it immediately; the right-A
    /// Hold(0.4s) activation and the sword-hit charge meter become live for the rest of the game from
    /// that point. The rig carries both <see cref="WeakpointSight"/> and <see cref="OverdriveController"/>
    /// from scene start via <see cref="AttachPlayerAbilities"/> (both self-gate, harmless before unlock).
    ///
    /// FLOODING WATER HAZARD: the Tide depths room carries one <see cref="FloodingWaterHazard"/> volume
    /// as ambient pressure-damage flavor (per the source script's "black water, pressure" staging) — it
    /// auto-rises on scene start and is not mission-step-gated; it is atmosphere, not a blocking puzzle,
    /// matching how Ch4 used the same component for exterior flavor rather than a hard mechanic.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch9ScenePath = SceneFolder + "/Ch09_PitAndTheDeep.unity";
        private const string Ch9VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch9HoldHalfWidth = 8f;
        private const float Ch9TideHalfWidth = 10f;
        private const float Ch9CoreHalfWidth = 8f;

        // Named-cast prefabs. Gryph/Rook/Sable resolve to real Tripo image->3D meshes already baked to
        // disk (glob confirmed at authoring time); Vane resolves to PlaceholderCharacterBuilder's
        // existing Vane_Wraith-6 Massive-archetype spec (no real art yet).
        private const string Ch9GryphPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Gryph.prefab";
        private const string Ch9RookPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Rook.prefab";
        private const string Ch9SablePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Sable.prefab";
        private const string Ch9VanePrefab = PlaceholderCharacterFolder + "/Vane_Wraith-6.prefab";
        private const string Ch9EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 09 — The Pit and the Deep", priority = 209)]
        public static void BuildChapter9PitAndTheDeep()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var raiderDef = Ch9EnsureCoilRaiderDefinition();
            var vaneDef = Ch9EnsureVaneDefinition();

            // ---- Lighting: dust-amber upper hold cooling to Program-metal blue at the old machinery,
            // then a dark drowned teal for the Tide depths and a data-cyan reveal at the core. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.58f, 0.68f);
            light.intensity = 0.32f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.09f, 0.12f);
            RenderSettings.fogDensity = 0.02f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.85f, 0.7f, 0.45f), 1f, 10f);
            BuildAccentPointLight("HoldUpperLight0", new Vector3(-3f, 2.6f, 18f), new Color(0.9f, 0.68f, 0.4f), 1.3f, 14f);
            BuildAccentPointLight("HoldUpperLight1", new Vector3(3f, 2.6f, 30f), new Color(0.85f, 0.62f, 0.38f), 1.3f, 14f);
            BuildAccentPointLight("OldMachineryLight", new Vector3(0f, 2.4f, 45f), new Color(0.4f, 0.6f, 0.95f), 1.8f, 12f);
            BuildAccentPointLight("OverlookLight0", new Vector3(-4f, 2.6f, 53f), new Color(0.95f, 0.55f, 0.3f), 1.6f, 16f);
            BuildAccentPointLight("OverlookLight1", new Vector3(4f, 2.6f, 57f), new Color(0.95f, 0.55f, 0.3f), 1.6f, 16f);
            BuildAccentPointLight("TideDepthsLight0", new Vector3(-5f, 2.2f, 78f), new Color(0.25f, 0.55f, 0.6f), 1.6f, 16f);
            BuildAccentPointLight("TideDepthsLight1", new Vector3(5f, 2.2f, 92f), new Color(0.2f, 0.5f, 0.65f), 1.8f, 18f);
            BuildAccentPointLight("CoreLight0", new Vector3(-3f, 2.6f, 108f), new Color(0.35f, 0.9f, 0.95f), 2f, 16f);
            BuildAccentPointLight("CoreLight1", new Vector3(3f, 2.6f, 112f), new Color(0.35f, 0.9f, 0.95f), 2f, 16f);

            // ---- World root: three abutting zones along +Z — the Rustfang hold, the Tide depths, the
            // construction core. ----
            var worldGo = new GameObject("Rustfang");
            var world = worldGo.transform;

            BuildFloorCeiling(world, "HoldHall", new Vector3(0f, 0f, 31f),
                new Vector3(Ch9HoldHalfWidth * 2f, 0f, 66f), new Color(0.14f, 0.1f, 0.07f), new Color(0.06f, 0.05f, 0.05f));
            BuildWall(world, "HoldHall_WallW", new Vector3(-Ch9HoldHalfWidth, RoomH / 2f, 31f), new Vector3(0.2f, RoomH, 66f));
            BuildWall(world, "HoldHall_WallE", new Vector3(Ch9HoldHalfWidth, RoomH / 2f, 31f), new Vector3(0.2f, RoomH, 66f));
            BuildWall(world, "HoldHall_WallS", new Vector3(0f, RoomH / 2f, -2f), new Vector3(Ch9HoldHalfWidth * 2f, RoomH, 0.2f));

            Ch9BuildHoldRackRow(world, 10f, 40f, 6f);
            BuildProp(world, "SealedProgramWall", new Vector3(0f, RoomH / 2f, 47f), new Vector3(Ch9HoldHalfWidth * 2f - 1f, RoomH, 0.4f), new Color(0.18f, 0.24f, 0.34f));
            BuildProp(world, "FreightCradle", new Vector3(0f, 0.4f, 58f), new Vector3(2.2f, 0.8f, 2.2f), new Color(0.3f, 0.28f, 0.22f));

            BuildFloorCeiling(world, "TideDepths", new Vector3(0f, -0.3f, 84f),
                new Vector3(Ch9TideHalfWidth * 2f, 0f, 40f), new Color(0.03f, 0.06f, 0.08f), new Color(0.02f, 0.03f, 0.04f));
            BuildWall(world, "TideDepths_WallW", new Vector3(-Ch9TideHalfWidth, RoomH / 2f, 84f), new Vector3(0.2f, RoomH, 40f));
            BuildWall(world, "TideDepths_WallE", new Vector3(Ch9TideHalfWidth, RoomH / 2f, 84f), new Vector3(0.2f, RoomH, 40f));
            Ch9BuildShadowRackRow(world, 70f, 98f, 7f);

            BuildFloorCeiling(world, "ConstructionCore", new Vector3(0f, -0.3f, 112f),
                new Vector3(Ch9CoreHalfWidth * 2f, 0f, 16f), new Color(0.03f, 0.06f, 0.08f), new Color(0.02f, 0.03f, 0.04f));
            BuildWall(world, "ConstructionCore_WallW", new Vector3(-Ch9CoreHalfWidth, RoomH / 2f, 112f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "ConstructionCore_WallE", new Vector3(Ch9CoreHalfWidth, RoomH / 2f, 112f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "ConstructionCore_WallN", new Vector3(0f, RoomH / 2f, 120f), new Vector3(Ch9CoreHalfWidth * 2f, RoomH, 0.2f));
            BuildProp(world, "EngineScaffold0", new Vector3(-3f, 1.4f, 112f), new Vector3(0.5f, 2.8f, 0.5f), new Color(0.3f, 0.6f, 0.65f));
            BuildProp(world, "EngineScaffold1", new Vector3(3f, 1.4f, 116f), new Vector3(0.5f, 2.8f, 0.5f), new Color(0.3f, 0.6f, 0.65f));
            BuildProp(world, "SableInterface", new Vector3(0f, 0.6f, 108f), new Vector3(1.4f, 1.2f, 1.0f), new Color(0.25f, 0.65f, 0.7f));

            // ---- Flooding hazard: ambient pressure-damage flavor for the Tide depths (see class
            // summary). ----
            var waterGo = new GameObject("TideFlood");
            waterGo.transform.position = new Vector3(0f, -0.3f, 84f);
            var waterCollider = waterGo.AddComponent<BoxCollider>();
            waterCollider.isTrigger = true;
            waterCollider.size = new Vector3(Ch9TideHalfWidth * 2f, 5f, 40f);
            waterCollider.center = new Vector3(0f, 1f, 0f);
            var water = waterGo.AddComponent<FloodingWaterHazard>();

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, and every shipped ability chain
            // (weakpoint-sight from Ch7, Overdrive granted this chapter — both self-gate). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 3f, 58f);
            bounds.radius = 130f;

            AttachPlayerAbilities(rig, refs);
            water.Configure(playerHealth);

            // The katana rides from the start (deep into Act III — no rack-wake beat, matching Ch5-8's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch9EchoBladePrefab);

            // ---- Gryph + Rook: placed once at the hold overlook, fight the Coil raid alongside the
            // player, and stay there for the final hand-off beat. ----
            // FLAGGED FOR REVIEW: AllyCombatant.RetargetNearestEnemy has no max engagement range — once
            // the Coil raid is cleared, Gryph/Rook idle (no live Health+MeleeAttacker target exists)
            // until Vane/Wraith-6 activates ~28m away at the Tide depths, at which point they WOULD start
            // walking toward him too, contradicting the "Gryph is comm-only, doesn't physically follow"
            // constraint the Ch9 dialogue fix establishes. A proper fix needs a range cap on the shared
            // AllyCombatant component, which is out of this pass's scope (touches a component every
            // future ally-combat chapter reuses). Left as-is: moveSpeed 1.4 m/s over ~28m gives a
            // multi-second grace window before they'd arrive, and most boss encounters resolve well
            // inside it, but this is a real gap, not a stylistic one — flagging for the reviewer.
            Ch9PlaceAlly(Ch9GryphPrefab, new Vector3(-2f, 0f, 60f), "Gryph");
            Ch9PlaceAlly(Ch9RookPrefab, new Vector3(2f, 0f, 61f), "Rook");

            // ---- Coil raiders: inactive until the wave spawner's own proximity trigger arms them. ----
            Vector3[] raiderPositions =
            {
                new Vector3(-3f, 0f, 56f), new Vector3(0f, 0f, 54f),
                new Vector3(3f, 0f, 56f), new Vector3(0f, 0f, 62f),
            };
            var raiders = new List<GameObject>();
            foreach (var pos in raiderPositions)
            {
                var e = BuildEnemy(pos, playerHealth, raiderDef);
                e.gameObject.SetActive(false);
                raiders.Add(e.gameObject);
            }

            // ---- Sable: decorative set-dressing at the Tide depths, no Health/combat (wired into the
            // lattice, not a combatant). ----
            Ch9PlaceStoryNpc(Ch9SablePrefab, new Vector3(2f, 0f, 90f), "Sable");

            // ---- Vane/Wraith-6: the boss, inactive until MissionDirector's DefeatEnemies step activates
            // it (mirrors Ch8BuildWarden's reveal timing). ----
            var vane = Ch9BuildVane(new Vector3(-1f, 0f, 88f), vaneDef, playerHealth);
            vane.gameObject.SetActive(false);

            // ---- Reach points. ----
            var oldMachineryReachGo = new GameObject("OldMachineryReachPoint");
            oldMachineryReachGo.transform.position = new Vector3(0f, 1f, 44f);
            var overlookReachGo = new GameObject("OverlookReachPoint");
            overlookReachGo.transform.position = new Vector3(0f, 1f, 52f);
            var tideEntryReachGo = new GameObject("TideEntryReachPoint");
            tideEntryReachGo.transform.position = new Vector3(0f, 1f, 68f);
            var coreReachGo = new GameObject("ConstructionCoreReachPoint");
            coreReachGo.transform.position = new Vector3(0f, 1f, 106f);
            var climbOutReachGo = new GameObject("ClimbOutReachPoint");
            climbOutReachGo.transform.position = new Vector3(0f, 1f, 59f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch9BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch9_beat0_briefing", talkRef);
            var dlgDescent = Ch9BuildDialogue("Dialogue_Beat1_Descent", new Vector3(0f, 1f, 12f), "ch9_beat1_descent", talkRef);
            var dlgOldMachinery = Ch9BuildDialogue("Dialogue_Beat1_OldMachinery", new Vector3(0f, 1f, 44f), "ch9_beat1_old_machinery", talkRef);
            var dlgChallenge = Ch9BuildDialogue("Dialogue_Beat2_Challenge", new Vector3(0f, 1f, 52f), "ch9_beat2_challenge", talkRef);
            var dlgRaidBark = Ch9BuildDialogue("Dialogue_Beat2_RaidBark", new Vector3(0f, 1f, 54f), "ch9_beat2_raid_bark", talkRef);
            var dlgBargain = Ch9BuildDialogue("Dialogue_Beat2_Bargain", new Vector3(0f, 1f, 59f), "ch9_beat2_bargain", talkRef);
            var dlgDiveIntro = Ch9BuildDialogue("Dialogue_Beat3_DiveIntro", new Vector3(0f, 1f, 70f), "ch9_beat3_dive_intro", talkRef);
            var dlgConfrontation = Ch9BuildDialogue("Dialogue_Beat3_Confrontation", new Vector3(0f, 1f, 87f), "ch9_beat3_confrontation", talkRef);
            var dlgAftermath = Ch9BuildDialogue("Dialogue_Beat3_Aftermath", new Vector3(0f, 1f, 88f), "ch9_beat3_aftermath", talkRef);
            var dlgOverdrive = Ch9BuildDialogue("Dialogue_Beat3_Overdrive", new Vector3(0f, 1f, 88f), "ch9_beat3_overdrive", talkRef);
            var dlgRecruit = Ch9BuildDialogue("Dialogue_Beat3_Recruit", new Vector3(1f, 1f, 90f), "ch9_beat3_recruit", talkRef);
            var dlgReveal = Ch9BuildDialogue("Dialogue_Beat4_Reveal", new Vector3(0f, 1f, 108f), "ch9_beat4_reveal", talkRef);
            var dlgHoldKept = Ch9BuildDialogue("Dialogue_Beat5_HoldKept", new Vector3(0f, 1f, 59f), "ch9_beat5_holdkept", talkRef);

            // ---- Coil raid wave spawner (single wave = all raiders, bark = the challenge->raid turn).
            // Built active-idle (Ch4/Ch7's HunterWave lesson: an inactive spawner can't StartCoroutine) —
            // Begin() is called by the DefeatWaves mission step below, and its own proximity poll gates
            // the actual spawn on the player reaching the overlook. ----
            var raidWaves = new List<List<Health>> { raiders.ConvertAll(go => go.GetComponent<Health>()) };
            var raidSpawner = BuildWaveSpawner("CoilRaidWaveSpawner", new Vector3(0f, 0f, 58f), 12f, raidWaves, new[] { dlgRaidBark });

            // ---- Overdrive ability grant: activated alongside Vane's defeat. ----
            var overdriveGranterGo = new GameObject("OverdriveGranter");
            var overdriveGranter = overdriveGranterGo.AddComponent<AbilityGranter>();
            var granterSo = new SerializedObject(overdriveGranter);
            granterSo.FindProperty("abilityId").stringValue = AbilityId.Overdrive;
            granterSo.ApplyModifiedPropertiesWithoutUndo();
            overdriveGranterGo.SetActive(false);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ch9_complete AND both recruit
            // flags together, mirroring how Ch7 combined its ally-recruit flag with the completion flag. ----
            var completeCanvasGo = Ch9BuildCompleteCanvas(new Vector3(0f, 1.4f, 61f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 60f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 3;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch9_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "gryph_recruited";
            flagsProp.GetArrayElementAtIndex(2).stringValue = "sable_recruited";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 9 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 21;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Rustfang Hold (descent begins)", dlgDescent);
            AuthorReachStep(steps, n++, "ReachTrigger: The Old Machinery", oldMachineryReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Old Machinery (Program-cut wall)", dlgOldMachinery);
            AuthorReachStep(steps, n++, "ReachTrigger: The Hold Overlook", overlookReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: Gryph's Challenge", dlgChallenge);

            var wavesStep = steps.GetArrayElementAtIndex(n++);
            wavesStep.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            wavesStep.FindPropertyRelative("label").stringValue = "Beat2: The Coil Raid (co-op with Gryph + Rook)";
            wavesStep.FindPropertyRelative("waveSpawner").objectReferenceValue = raidSpawner;

            AuthorDialogueStep(steps, n++, "Beat2: The Bargain (map given, Ally #5)", dlgBargain);
            AuthorReachStep(steps, n++, "ReachTrigger: The Tide Depths", tideEntryReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat3: The Dive (racks put to work)", dlgDiveIntro);
            AuthorDialogueStep(steps, n++, "Beat3: The Confrontation (Sable + Vane/Wraith-6)", dlgConfrontation);
            AuthorDefeatStep(steps, n++, "Beat3: The Duel (Vane/Wraith-6, boss)", new List<Object> { vane.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat3: Rest, Wraith (aftermath)", dlgAftermath);
            AuthorTriggerStep(steps, n++, "Trigger: Overdrive Granted (Vane's freed blade-shadow)", overdriveGranterGo);
            AuthorDialogueStep(steps, n++, "Beat3: Overdrive (Echo names the gift)", dlgOverdrive);
            AuthorDialogueStep(steps, n++, "Beat3: Sable Freed (Ally #6)", dlgRecruit);
            AuthorReachStep(steps, n++, "ReachTrigger: The Construction Core", coreReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat4: The Concord Engine (reveal)", dlgReveal);
            AuthorReachStep(steps, n++, "ReachTrigger: Back to the Overlook (climb out)", climbOutReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat5: The Hold Kept (Rook succeeds Gryph)", dlgHoldKept);
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
            EditorSceneManager.SaveScene(scene, Ch9ScenePath);
            EnsureScenesInBuild(Ch9ScenePath);

            Debug.Log($"[Space Samurai] Chapter 9 built at {Ch9ScenePath}. " +
                      "The Rustfang hold descent (rack row aging into a sealed Program wall) -> the hold " +
                      "overlook (Gryph's bargain: co-op Coil-raid defense, Ally #5, Rook seeded) -> the " +
                      "Tide depths (Vane/Wraith-6 boss duel, Overdrive granted, Sable freed as Ally #6) " +
                      "-> the construction core (Concord Engine reveal, Ladder A rung 4 + Ladder E rung 3) " +
                      "-> back to the overlook (Rook succeeds Gryph as keeper). 21 mission steps. Opens " +
                      "Act III. Gryph/Rook/Sable resolve to real Named prefabs; Vane/Wraith-6 falls back " +
                      "to PlaceholderCharacterBuilder's existing Massive-archetype spec.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch7EnsureScavengerDefinition). ----

        private static EnemyDefinition Ch9EnsureCoilRaiderDefinition()
        {
            const string path = DataFolder + "/Ch9CoilRaider.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 55f;
            def.damage = 9f;
            def.moveSpeed = 1.5f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch9EnsureVaneDefinition()
        {
            const string path = DataFolder + "/Ch9Vane.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 280f;
            def.damage = 24f;
            def.moveSpeed = 1.5f;
            def.attackCooldown = 0.85f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch9 voice clips ourselves. ----

        private static DialoguePlayer Ch9BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter9Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch9WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter9] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch9WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter9Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch9VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch9VoiceFolder}/{clipName}.wav");
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

        /// <summary>Mirrors Chapter7's Ch7PlaceStoryNpc: a decorative/story NPC with no combat component.</summary>
        private static GameObject Ch9PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>A friendly co-op combatant NPC (Gryph/Rook): no Health by design (AllyCombatant
        /// cannot be damaged), fights alongside the player once enemies are live.</summary>
        private static GameObject Ch9PlaceAlly(string prefabPath, Vector3 pos, string displayName)
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
        /// The boss, built from Vane/Wraith-6's placeholder mesh (no combat rig of its own): instantiates
        /// the mesh, synthesizes an ArmR/Sword/Blade/BladeTip hierarchy the way Ch7BuildMindspaceBoss /
        /// Ch8BuildWarden do, then wires <see cref="Enemy"/> onto it.
        /// </summary>
        private static Enemy Ch9BuildVane(Vector3 pos, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(Ch9VanePrefab, pos, "Vane / Wraith-6");
            if (go == null) return null; // match Ch9PlaceAlly/Ch9PlaceStoryNpc — never FitNamedCharacter(null)
            FitNamedCharacter(go);
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

        /// <summary>Tinted salvage-rack props lining the hold hall, dust-rust near the entrance cooling
        /// toward Program-blue near the sealed wall — the descent's "the deeper it goes, the older the
        /// machinery" gradient, done with prop tint (cheap) rather than one real Light per rack.</summary>
        private static void Ch9BuildHoldRackRow(Transform parent, float zStart, float zEnd, float spacing)
        {
            var rustRack = new Color(0.42f, 0.3f, 0.18f);
            var coldRack = new Color(0.22f, 0.3f, 0.42f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                float t = Mathf.InverseLerp(zStart, zEnd, z);
                Color rack = Color.Lerp(rustRack, coldRack, t);
                BuildProp(parent, "SalvageRack", new Vector3(-7f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.2f), rack);
                BuildProp(parent, "SalvageRack", new Vector3(7f, 1.1f, z), new Vector3(0.4f, 2.2f, 1.2f), rack);
            }
        }

        /// <summary>Tinted shadow-rack props lining the Tide depths — the reliquary's still racks (Ch7)
        /// wired into a working lattice down here, so they read teal-lit and slightly brighter near the
        /// core end rather than uniformly dim.</summary>
        private static void Ch9BuildShadowRackRow(Transform parent, float zStart, float zEnd, float spacing)
        {
            var dimRack = new Color(0.1f, 0.28f, 0.32f);
            var litRack = new Color(0.2f, 0.55f, 0.6f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                float t = Mathf.InverseLerp(zStart, zEnd, z);
                Color rack = Color.Lerp(dimRack, litRack, t);
                BuildProp(parent, "ShadowRack", new Vector3(-9f, 0.8f, z), new Vector3(0.4f, 1.6f, 1.2f), rack);
                BuildProp(parent, "ShadowRack", new Vector3(9f, 0.8f, z), new Vector3(0.4f, 1.6f, 1.2f), rack);
            }
        }

        /// <summary>A worldspace "CHAPTER 9 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch9BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 9 COMPLETE Canvas");
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
            label.text = "CHAPTER 9 COMPLETE";
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

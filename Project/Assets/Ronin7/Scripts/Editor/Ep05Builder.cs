using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// EP05 "The Broken Echo" scene builders. Builds the Rust Collective stations where Ronin-7
    /// encounters Ronin-9 (the defective operative from his cohort), learns about Kethel-7,
    /// escapes through pressure-lock corridors hunted by Dominion teams, fights silencers
    /// above the accretion disk, and meets Kessler to plan the final strike. Wires all MissionDirector
    /// steps, enemy waves, and NPC interactions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep05MarketTierScenePath = SceneFolder + "/Galaxy1_EP05_MarketTier.unity";
        private const string Galaxy1Ep05PressureLocksScenePath = SceneFolder + "/Galaxy1_EP05_PressureLocks.unity";
        private const string Galaxy1Ep05RotundaScenePath = SceneFolder + "/Galaxy1_EP05_Rotunda.unity";
        private const string Galaxy1Ep05CommandHubScenePath = SceneFolder + "/Galaxy1_EP05_CommandHub.unity";

        private static readonly string Galaxy1Ep05MarketTierSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05MarketTierScenePath);
        private static readonly string Galaxy1Ep05PressureLocksSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05PressureLocksScenePath);
        private static readonly string Galaxy1Ep05RotundaSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05RotundaScenePath);
        private static readonly string Galaxy1Ep05CommandHubSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05CommandHubScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP05 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep05" and loads lines from Ep05Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp05DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep05Lines.Get(setId), advanceRef, setId, clipPrefix: "ep05");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Market Tier", priority = 80)]
        public static void BuildEp05MarketTier()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Market tier interior: warm rust-amber palette, salvage station ambiance.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.75f, 0.65f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.2f, 0.15f);

            // Rust-vapor fog: amber exponential, low density.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.4f, 0.28f, 0.18f);
            RenderSettings.fogDensity = 0.025f;

            // Market accent lights (warm rust tones).
            BuildAccentPointLight("MarketLight1", new Vector3(-3f, 2.6f, 10f),
                new Color(1f, 0.7f, 0.4f), intensity: 1.5f, range: 12f);
            BuildAccentPointLight("MarketLight2", new Vector3(3f, 2.6f, 16f),
                new Color(1f, 0.75f, 0.5f), intensity: 1.4f, range: 12f);

            // ---- Market tier: market hall (z 0-10) -> cargo warrens corridor (z 10-20) ->
            // sealed refuge room (z 20-26).
            var interiorGo = new GameObject("MarketInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.22f, 0.18f, 0.15f);
            var ceilColor = new Color(0.12f, 0.1f, 0.08f);

            // Market hall: x[-5,5], z[0,10].
            BuildFloorCeiling(interior, "MarketHall", new Vector3(0f, 0f, 5f), new Vector3(10f, 0f, 10f), floorColor, ceilColor);
            BuildWall(interior, "MarketHall_WallW", new Vector3(-5f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MarketHall_WallE", new Vector3(5f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MarketHall_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(10f, 3f, 0.2f));

            // Market stall props (boxes/crates).
            var stallColor = new Color(0.35f, 0.3f, 0.25f);
            BuildProp(interior, "Stall1", new Vector3(-3f, 0.5f, 3f), new Vector3(1.5f, 1f, 1.5f), stallColor);
            BuildProp(interior, "Stall2", new Vector3(3f, 0.5f, 4f), new Vector3(1.5f, 1.2f, 1.5f), stallColor);
            BuildProp(interior, "Stall3", new Vector3(-2f, 0.4f, 7f), new Vector3(1f, 0.8f, 1f), stallColor);

            // Cargo warrens corridor: x[-4,4], z[10,20] with stacked container props.
            BuildFloorCeiling(interior, "WarrensCorridor", new Vector3(0f, 0f, 15f), new Vector3(8f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "WarrensCorridor_WallW", -4f, 10f, 20f, new float[0], 2.4f);
            BuildCorridorWall(interior, "WarrensCorridor_WallE", 4f, 10f, 20f, new float[0], 2.4f);

            // Container props stacked in warrens.
            var containerColor = new Color(0.45f, 0.35f, 0.25f);
            BuildProp(interior, "Container1", new Vector3(-2.5f, 0.8f, 12f), new Vector3(1.2f, 1.6f, 1.2f), containerColor);
            BuildProp(interior, "Container2", new Vector3(2.5f, 0.8f, 14f), new Vector3(1.2f, 1.6f, 1.2f), containerColor);
            BuildProp(interior, "Container3", new Vector3(-1f, 1.6f, 16f), new Vector3(1f, 1.4f, 1f), containerColor);

            // Sealed refuge room: x[-3,3], z[20,26].
            BuildFloorCeiling(interior, "RefugeRoom", new Vector3(0f, 0f, 23f), new Vector3(6f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "RefugeRoom_WallW", new Vector3(-3f, 1.5f, 23f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "RefugeRoom_WallE", new Vector3(3f, 1.5f, 23f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "RefugeRoom_WallBack", new Vector3(0f, 1.5f, 26f), new Vector3(6f, 3f, 0.2f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Ronin-9 near a market stall.
            var ronin9Pos = new Vector3(-2f, 1f, 4f);
            var ronin9Go = InstantiateNpc(ArtPrefabBuilder.Ronin9PrefabPath, ronin9Pos, "Ronin9");
            if (ronin9Go != null)
            {
                var ronin9Npc = ronin9Go.AddComponent<StoryNpc>();
                var r9So = new SerializedObject(ronin9Npc);
                r9So.FindProperty("displayName").stringValue = "Ronin-9";
                r9So.FindProperty("remote").boolValue = false;
                r9So.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Alarm lights (activated when enforcers emerge). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 14f), new Vector3(2f, 2.4f, 16f));

            // ---- Dialogue Players ----
            var marketRecognitionDialogue = BuildEp05DialoguePlayer("Dialogue_MarketRecognition", ronin9Pos, "market_recognition", talkRef);
            var warrensEnforcerBarksDialogue = BuildEp05DialoguePlayer("Dialogue_WarrensEnforcerBarks", new Vector3(0f, 1.5f, 15f), "warrens_enforcer_barks");
            var warrensAfterDialogue = BuildEp05DialoguePlayer("Dialogue_WarrensAfter", new Vector3(0f, 1f, 18f), "warrens_after");
            var kethelExplainedDialogue = BuildEp05DialoguePlayer("Dialogue_KethelExplained", new Vector3(0f, 1f, 23f), "kethel_explained", talkRef);

            // ---- Enemies: 3 Enforcers (rust-brown tint). ----
            var enforcerBrown = new Color(0.65f, 0.45f, 0.3f);
            var enforcerPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 12f),
                new Vector3(1.5f, 0f, 13f),
                new Vector3(0f, 0f, 15f)
            };
            var enforcerHealths = new List<Health>();
            foreach (var pos in enforcerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerBrown);
                enemy.gameObject.SetActive(false);
                enforcerHealths.Add(enemy.GetComponent<Health>());
            }

            var enforcerWaveSpawner = BuildWaveSpawner("EnforcerWaveSpawner", new Vector3(0f, 1f, 13f), 3f,
                new List<List<Health>> { enforcerHealths }, new[] { warrensEnforcerBarksDialogue });

            // Reach triggers.
            var warrensEntranceReachGo = new GameObject("WarrensEntranceReachPoint");
            warrensEntranceReachGo.transform.position = new Vector3(0f, 1f, 10f);
            var refugeRoomReachGo = new GameObject("RefugeRoomReachPoint");
            refugeRoomReachGo.transform.position = new Vector3(0f, 1f, 23f);

            // Transition box: "TO THE PRESSURE LOCKS".
            var pressureBoxGo = BuildTransitionBox("ToPressureLocksBox", new Vector3(0f, 1.2f, 24.5f), "TO THE PRESSURE LOCKS",
                out var pressureBtn, out var pressureTransition);
            var ptSo = new SerializedObject(pressureTransition);
            ptSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05PressureLocksSceneName;
            ptSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pressureBtn.onClick,
                new UnityEngine.Events.UnityAction(pressureTransition.LoadOnFootScene));
            pressureBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 8;

            // Step 0: Dialogue market_recognition (Ronin-9 recognition, talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Market Recognition";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = marketRecognitionDialogue;

            // Step 1: ReachTrigger — warrens entrance.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Warrens Entrance";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = warrensEntranceReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Enforcer Alarm";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 3: DefeatWaves — 3 Enforcers.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Cargo Enforcers";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerWaveSpawner;

            // Step 4: Dialogue warrens_after.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: After the Fight";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = warrensAfterDialogue;

            // Step 5: ReachTrigger — refuge room.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Refuge Room";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = refugeRoomReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 6: Dialogue kethel_explained (talk-gated).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Kethel-7 Explained";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = kethelExplainedDialogue;

            // Step 7: Prompt — transition to pressure locks.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s7.FindPropertyRelative("label").stringValue = "Prompt: To the Pressure Locks";
            s7.FindPropertyRelative("promptObject").objectReferenceValue = pressureBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05MarketTierScenePath);
            EnsureScenesInBuild(Galaxy1Ep05MarketTierScenePath);

            Debug.Log($"[Space Samurai] EP05 Market Tier scene built at {Galaxy1Ep05MarketTierScenePath}. " +
                      "Layout: market hall with vendor stalls → cargo warrens corridor (stacked containers, 3 Enforcers) → sealed refuge room. " +
                      "Warm rust-amber fog, emergency strobe accents. " +
                      "8 steps: market_recognition (Ronin-9 talk) → reach warrens → trigger alarms → defeat 3 Enforcers + barks → warrens_after → reach refuge → " +
                      "kethel_explained (talk) → transition to Pressure Locks.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Pressure Locks", priority = 81)]
        public static void BuildEp05PressureLocks()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Pressure locks interior: cold blue-white palette, industrial/technical mood.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.85f, 0.95f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.25f);

            // Frost-grey fog: blue-tinted.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.3f, 0.38f);
            RenderSettings.fogDensity = 0.02f;

            // Pressure lock accent lights (cool blue).
            BuildAccentPointLight("LockLight1", new Vector3(-2.5f, 2.6f, 12f),
                new Color(0.6f, 0.8f, 1f), intensity: 1.4f, range: 12f);
            BuildAccentPointLight("LockLight2", new Vector3(2.5f, 2.6f, 20f),
                new Color(0.65f, 0.85f, 1f), intensity: 1.3f, range: 12f);

            // ---- Pressure locks: entry corridor (z 0-8) -> lock maze corridor (z 8-22) ->
            // salvage bay (z 22-30).
            var interiorGo = new GameObject("PressureLockInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.28f);
            var ceilColor = new Color(0.1f, 0.12f, 0.16f);

            // Entry corridor: x[-3,3], z[0,8].
            BuildFloorCeiling(interior, "EntryLock", new Vector3(0f, 0f, 4f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildCorridorWall(interior, "EntryLock_WallW", -3f, 0f, 8f, new float[0], 2.4f);
            BuildCorridorWall(interior, "EntryLock_WallE", 3f, 0f, 8f, new float[0], 2.4f);
            BuildWall(interior, "EntryLock_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.2f));

            // Lock maze corridor: x[-4,4], z[8,22] with narrow passages and rotating-bulkhead props.
            BuildFloorCeiling(interior, "LockMaze", new Vector3(0f, 0f, 15f), new Vector3(8f, 0f, 14f), floorColor, ceilColor);
            BuildCorridorWall(interior, "LockMaze_WallW", -4f, 8f, 22f, new float[0], 2.4f);
            BuildCorridorWall(interior, "LockMaze_WallE", 4f, 8f, 22f, new float[0], 2.4f);

            // Rotating bulkhead props (grey-metal color).
            var bulkheadColor = new Color(0.4f, 0.42f, 0.45f);
            BuildProp(interior, "Bulkhead1", new Vector3(-1.5f, 0.8f, 11f), new Vector3(1f, 1.8f, 0.3f), bulkheadColor);
            BuildProp(interior, "Bulkhead2", new Vector3(1.5f, 0.8f, 16f), new Vector3(1f, 1.8f, 0.3f), bulkheadColor);
            BuildProp(interior, "Bulkhead3", new Vector3(-2f, 0.6f, 19f), new Vector3(0.8f, 1.6f, 0.3f), bulkheadColor);

            // Salvage bay: x[-5,5], z[22,30].
            BuildFloorCeiling(interior, "SalvageBay", new Vector3(0f, 0f, 26f), new Vector3(10f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "SalvageBay_WallW", new Vector3(-5f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "SalvageBay_WallE", new Vector3(5f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "SalvageBay_WallBack", new Vector3(0f, 1.5f, 30f), new Vector3(10f, 3f, 0.2f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Alarm lights. ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 14f), new Vector3(2f, 2.4f, 18f));

            // ---- Dialogue Players ----
            var lockAlertDialogue = BuildEp05DialoguePlayer("Dialogue_LockAlert", new Vector3(0f, 1.5f, 5f), "lock_alert");
            var lockHunterBarksDialogue = BuildEp05DialoguePlayer("Dialogue_LockHunterBarks", new Vector3(0f, 1.5f, 15f), "lock_hunter_barks");
            var purgeTruthDialogue = BuildEp05DialoguePlayer("Dialogue_PurgeTruth", new Vector3(0f, 1f, 15f), "purge_truth", talkRef);
            var defectiveGenerationDialogue = BuildEp05DialoguePlayer("Dialogue_DefectiveGeneration", new Vector3(0f, 1f, 26f), "defective_generation", talkRef);

            // ---- Folded in from the merged Observation Deck: Kessler reaches the salvage bay; the "Khall is
            // coming" wounded beat + Kessler's talk play here (the deck's Silencer fight is dropped). ----
            var kesslerPos = new Vector3(-1f, 1f, 28f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_SalvageBay");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var deckWoundedDialogue = BuildEp05DialoguePlayer("Dialogue_DeckWounded", new Vector3(0f, 1f, 27f), "deck_wounded");
            var medBayKesslerDialogue = BuildEp05DialoguePlayer("Dialogue_MedBayKessler", kesslerPos, "medbay_kessler", talkRef);

            // ---- Enemies: 2 Hunters (gunmetal tint, 1.5x health). ----
            var hunterGunmetal = new Color(0.55f, 0.57f, 0.6f);
            var hunterPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 13f),
                new Vector3(1.5f, 0f, 17f)
            };
            var hunterHealths = new List<Health>();
            foreach (var pos in hunterPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hunterGunmetal);
                var hunterHealth = enemy.GetComponent<Health>();
                if (hunterHealth != null)
                {
                    var hSo = new SerializedObject(hunterHealth);
                    hSo.FindProperty("maxHealth").floatValue = hSo.FindProperty("maxHealth").floatValue * 1.5f;
                    hSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                hunterHealths.Add(hunterHealth);
            }

            var hunterWaveSpawner = BuildWaveSpawner("HunterWaveSpawner", new Vector3(0f, 1f, 15f), 3f,
                new List<List<Health>> { hunterHealths }, new[] { lockHunterBarksDialogue });

            // Reach triggers.
            var salvageBayReachGo = new GameObject("SalvageBayReachPoint");
            salvageBayReachGo.transform.position = new Vector3(0f, 1f, 26f);

            // Transition box: "TO THE CENTRAL ROTUNDA" (Observation Deck merged in — its wounded + Kessler
            // beats now play in this salvage bay, so Pressure Locks chains straight to the Rotunda).
            var observationBoxGo = BuildTransitionBox("ToCentralRotundaBox", new Vector3(0f, 1.2f, 29.5f), "TO THE CENTRAL ROTUNDA",
                out var observationBtn, out var observationTransition);
            var otSo = new SerializedObject(observationTransition);
            otSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05RotundaSceneName;
            otSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(observationBtn.onClick,
                new UnityEngine.Events.UnityAction(observationTransition.LoadOnFootScene));
            observationBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 9;

            // Step 0: Dialogue lock_alert (auto-play).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Pressure Lock Alert";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lockAlertDialogue;

            // Step 1: Trigger — alarm lights.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Hunter Alarm";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 2: DefeatWaves — 2 Hunters.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Hunter Operatives";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = hunterWaveSpawner;

            // Step 3: Dialogue purge_truth (talk-gated).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The Purge Truth";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = purgeTruthDialogue;

            // Step 4: ReachTrigger — salvage bay.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Salvage Bay";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = salvageBayReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue defective_generation (talk-gated).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Defective Generation";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = defectiveGenerationDialogue;

            // Step 6: Dialogue deck_wounded (auto) — folded from the merged Observation Deck.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Wounded, Khall is Coming";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = deckWoundedDialogue;

            // Step 7: Dialogue medbay_kessler (talk-gated at Kessler) — folded from the merged Observation Deck.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Arrives";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = medBayKesslerDialogue;

            // Step 8: Prompt — transition to central rotunda.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s8.FindPropertyRelative("label").stringValue = "Prompt: To the Central Rotunda";
            s8.FindPropertyRelative("promptObject").objectReferenceValue = observationBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05PressureLocksScenePath);
            EnsureScenesInBuild(Galaxy1Ep05PressureLocksScenePath);

            Debug.Log($"[Space Samurai] EP05 Pressure Locks scene built at {Galaxy1Ep05PressureLocksScenePath}. " +
                      "Layout: entry corridor → lock maze (rotating bulkheads, 2 Hunters) → salvage bay. " +
                      "Cold blue-white fog, industrial accents. " +
                      "9 steps: lock_alert auto → trigger alarms → defeat 2 Hunters (1.5x health) + barks → purge_truth (talk) → " +
                      "reach salvage bay → defective_generation (talk) → deck_wounded → medbay_kessler (talk, both folded " +
                      "from the merged Observation Deck) → transition to Central Rotunda.");
        }
    }
}

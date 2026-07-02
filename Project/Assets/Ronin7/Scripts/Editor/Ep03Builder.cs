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
    /// EP03 "The Stowaway's Shadow" scene builders. Builds the Grimdock hauler scenes where Ronin-7
    /// finds Mira the stowaway, learns his killswitch already fired at Kethel-7 and failed, rescues the Block Seven children
    /// from the Crimson Lotus station, and settles them in the Fringe sanctuary. Wires all
    /// MissionDirector steps, enemy waves, and NPC interactions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep03HaulerScenePath = SceneFolder + "/Galaxy1_EP03_Hauler.unity";
        private const string Galaxy1Ep03EngineRoomScenePath = SceneFolder + "/Galaxy1_EP03_EngineRoom.unity";
        private const string Galaxy1Ep03LotusScenePath = SceneFolder + "/Galaxy1_EP03_LotusStation.unity";
        private const string Galaxy1Ep03SanctuaryScenePath = SceneFolder + "/Galaxy1_EP03_Sanctuary.unity";

        private static readonly string Galaxy1Ep03HaulerSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep03HaulerScenePath);
        private static readonly string Galaxy1Ep03EngineRoomSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep03EngineRoomScenePath);
        private static readonly string Galaxy1Ep03LotusSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep03LotusScenePath);
        private static readonly string Galaxy1Ep03SanctuarySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep03SanctuaryScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP03 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep03" and loads lines from Ep03Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp03DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep03Lines.Get(setId), advanceRef, setId, clipPrefix: "ep03");
        }

        /// <summary>Builds a small-scale child NPC from a feet-pivot character prefab with a StoryNpc display name.</summary>
        private static GameObject BuildEp03ChildNpc(string goName, string displayName, Vector3 position, float scale,
            string prefabPath = ChildPrefabPath)
        {
            var go = InstantiateNpc(prefabPath, AtFloor(position), goName);
            if (go == null) return null;
            go.transform.localScale = Vector3.one * scale;
            var npc = go.AddComponent<StoryNpc>();
            var so = new SerializedObject(npc);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("remote").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Builds an EnemyWaveSpawner with a trigger point, wave sting SFX, audio source, and
        /// the given waves (one enemy-health list + optional bark per wave).</summary>
        private static EnemyWaveSpawner BuildEp03WaveSpawner(string name, Vector3 triggerPos, float triggerRadius,
            List<List<Health>> waves, DialoguePlayer[] barks)
        {
            var spawnerGo = new GameObject(name);
            var spawner = spawnerGo.AddComponent<EnemyWaveSpawner>();
            var triggerGo = new GameObject(name + "_Trigger");
            triggerGo.transform.position = triggerPos;

            var so = new SerializedObject(spawner);
            SetObjectRef(so, "triggerPoint", triggerGo.transform);
            so.FindProperty("triggerRadius").floatValue = triggerRadius;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                so.FindProperty("waveSting").objectReferenceValue = waveSting;

            var wavesProp = so.FindProperty("waves");
            wavesProp.arraySize = waves.Count;
            for (int w = 0; w < waves.Count; w++)
            {
                var wave = wavesProp.GetArrayElementAtIndex(w);
                var enemiesProp = wave.FindPropertyRelative("enemies");
                enemiesProp.arraySize = waves[w].Count;
                for (int i = 0; i < waves[w].Count; i++)
                    enemiesProp.GetArrayElementAtIndex(i).objectReferenceValue = waves[w][i];
                if (barks != null && w < barks.Length && barks[w] != null)
                    wave.FindPropertyRelative("bark").objectReferenceValue = barks[w];
            }

            var audio = spawnerGo.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.playOnAwake = false;
            SetObjectRef(so, "audioSource", audio);
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        /// <summary>Builds a parented, initially-inactive pair of red alarm point lights (EP02 Core pattern).</summary>
        private static GameObject BuildEp03AlarmLights(Vector3 pos1, Vector3 pos2)
        {
            var alarmLightsGo = new GameObject("AlarmLights");
            foreach (var (pos, idx) in new[] { (pos1, 1), (pos2, 2) })
            {
                var lightGo = new GameObject($"AlarmLight{idx}");
                lightGo.transform.SetParent(alarmLightsGo.transform, false);
                lightGo.transform.position = pos;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.3f, 0.2f);
                light.intensity = 2.0f;
                light.range = 8f;
                light.shadows = LightShadows.None;
            }
            alarmLightsGo.SetActive(false); // Activated by Trigger step.
            return alarmLightsGo;
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP03 Hauler", priority = 72)]
        public static void BuildEp03Hauler()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Grimdock hauler ambiance: dim worn-metal interior, cold key with warm engine accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.8f, 0.88f);
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.14f);

            // Crew corridor accent: warm work light.
            BuildAccentPointLight("CorridorLight", new Vector3(0f, 2.6f, 4f),
                new Color(1f, 0.85f, 0.6f), intensity: 1.5f, range: 10f);
            // Cargo bay accent: cold industrial light.
            BuildAccentPointLight("CargoLight", new Vector3(0f, 2.6f, 16f),
                new Color(0.6f, 0.75f, 0.95f), intensity: 1.8f, range: 16f);
            // Containment cell accent: cool dim cryo glow.
            BuildAccentPointLight("ContainmentLight", new Vector3(0f, 2.6f, 28f),
                new Color(0.45f, 0.7f, 1f), intensity: 1.5f, range: 10f);
            // Bridge corridor accent.
            BuildAccentPointLight("BridgeLight", new Vector3(0f, 2.6f, 38f),
                new Color(0.7f, 0.8f, 1f), intensity: 1.4f, range: 10f);

            // ---- Hauler interior: crew corridor (z 0-8) -> cargo bay (z 8-24) ->
            // containment cell (z 24-32) -> bridge corridor (z 32-42).
            var interiorGo = new GameObject("HaulerInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.22f, 0.21f, 0.2f);
            var ceilColor = new Color(0.13f, 0.12f, 0.11f);

            // Crew corridor: x[-2.5,2.5], z[0,8].
            BuildFloorCeiling(interior, "CrewCorridor", new Vector3(0f, 0f, 4f), new Vector3(5f, 0f, 8f), floorColor, ceilColor);
            BuildCorridorWall(interior, "CrewCorridor_WallW", -2.5f, 0f, 8f, new float[0], 2.4f);
            BuildCorridorWall(interior, "CrewCorridor_WallE", 2.5f, 0f, 8f, new float[0], 2.4f);
            BuildWall(interior, "CrewCorridor_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(5f, 3f, 0.2f));

            // Cargo bay: x[-5,5], z[8,24], wide hold with cryo-pods.
            BuildFloorCeiling(interior, "CargoBay", new Vector3(0f, 0f, 16f), new Vector3(10f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "CargoBay_WallW", new Vector3(-5f, 1.5f, 16f), new Vector3(0.2f, 3f, 16f));
            BuildWall(interior, "CargoBay_WallE", new Vector3(5f, 1.5f, 16f), new Vector3(0.2f, 3f, 16f));
            BuildDoorwayWall(interior, "CargoBay_WallFront", new Vector3(0f, 1.5f, 8f), 10f, true, 2.4f);
            BuildDoorwayWall(interior, "CargoBay_WallBack", new Vector3(0f, 1.5f, 24f), 10f, true, 2.4f);

            // Containment cell: x[-3,3], z[24,32], where Mira's cryo-pod sits.
            BuildFloorCeiling(interior, "Containment", new Vector3(0f, 0f, 28f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Containment_WallW", new Vector3(-3f, 1.5f, 28f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Containment_WallE", new Vector3(3f, 1.5f, 28f), new Vector3(0.2f, 3f, 8f));
            BuildDoorwayWall(interior, "Containment_WallBack", new Vector3(0f, 1.5f, 32f), 6f, true, 2.4f);

            // Bridge corridor: x[-2.5,2.5], z[32,42], leads to the engine-room transition.
            BuildFloorCeiling(interior, "BridgeCorridor", new Vector3(0f, 0f, 37f), new Vector3(5f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "BridgeCorridor_WallW", -2.5f, 32f, 42f, new float[0], 2.4f);
            BuildCorridorWall(interior, "BridgeCorridor_WallE", 2.5f, 32f, 42f, new float[0], 2.4f);
            BuildWall(interior, "BridgeCorridor_WallBack", new Vector3(0f, 1.5f, 42f), new Vector3(5f, 3f, 0.2f));

            // Cryo-pod props in the cargo bay + Mira's pod in containment.
            var podColor = new Color(0.3f, 0.38f, 0.45f);
            BuildProp(interior, "CryoPod1", new Vector3(-3.5f, 0.7f, 11f), new Vector3(1f, 1.4f, 2.2f), podColor);
            BuildProp(interior, "CryoPod2", new Vector3(3.5f, 0.7f, 14f), new Vector3(1f, 1.4f, 2.2f), podColor);
            BuildProp(interior, "CryoPod3", new Vector3(-3.5f, 0.7f, 19f), new Vector3(1f, 1.4f, 2.2f), podColor);
            BuildProp(interior, "MiraPod", new Vector3(-1.5f, 0.7f, 29f), new Vector3(1f, 1.4f, 2.2f), new Color(0.35f, 0.5f, 0.6f));
            BuildProp(interior, "Crate1", new Vector3(2.5f, 0.8f, 21f), new Vector3(1.2f, 1.6f, 1f), new Color(0.35f, 0.33f, 0.3f));
            BuildProp(interior, "Crate2", new Vector3(-1f, 0.6f, 16f), new Vector3(1f, 1.2f, 1f), new Color(0.32f, 0.3f, 0.28f));

            // Sliding doors.
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var cargoDoor = BuildSlidingDoor(interior, "CargoDoor", new Vector3(0f, 0f, 8f), 2.4f, true, startLocked: false);
            WireDoorAudio(cargoDoor, doorSlideClip);
            var containmentDoor = BuildSlidingDoor(interior, "ContainmentDoor", new Vector3(0f, 0f, 24f), 2.4f, true, startLocked: true);
            WireDoorAudio(containmentDoor, doorSlideClip);

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
            // Kessler: crew corridor (z ~4).
            var kesslerPos = new Vector3(-1f, 1f, 4f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_Hauler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Iris: cargo bay (z ~12), near the cryo-pods.
            BuildEp03ChildNpc("Iris_Hauler", "Iris", new Vector3(-2.5f, 0.7f, 12f), 0.7f, IrisPrefabPath);

            // Mira: containment cell by her pod (scale 0.6), inactive until the containment Trigger step.
            var miraParent = new GameObject("MiraParent");
            miraParent.transform.SetParent(interior, false);
            var miraGo = BuildEp03ChildNpc("Mira_Hauler", "Mira", new Vector3(-0.5f, 0.6f, 29f), 0.6f);
            if (miraGo != null) miraGo.transform.SetParent(miraParent.transform, true);
            miraParent.SetActive(false);

            // ---- Alarm lights (corvette attack ambiance, activated late). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 35f), new Vector3(2f, 2.4f, 39f));

            // ---- Dialogue Players ----
            var haulerHumDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerHum", kesslerPos, "hauler_hum");
            var haulerFoundDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerFound", new Vector3(-2.5f, 1f, 12f), "hauler_found", talkRef);
            var haulerBoardingDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerBoarding", new Vector3(0f, 1.5f, 16f), "hauler_boarding");
            var haulerFightBarksDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerFightBarks", new Vector3(0f, 1.5f, 16f), "hauler_fight_barks");
            var haulerClearDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerClear", new Vector3(0f, 1.5f, 16f), "hauler_clear");
            var haulerPodDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerPod", new Vector3(-0.5f, 1f, 29f), "hauler_pod", talkRef);
            var haulerFuryDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerFury", new Vector3(0f, 1f, 34f), "hauler_fury", talkRef);
            var haulerCorvetteDialogue = BuildEp03DialoguePlayer("Dialogue_HaulerCorvette", new Vector3(0f, 1.5f, 37f), "hauler_corvette");

            // ---- Enemies: 3 dark-grey Dominion enforcers boarding the cargo bay. ----
            var dominionGrey = new Color(0.3f, 0.32f, 0.36f);
            var boardingPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 13f),
                new Vector3(0f, 0f, 17f),
                new Vector3(2f, 0f, 20f)
            };
            var boardingHealths = new List<Health>();
            foreach (var pos in boardingPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, dominionGrey);
                enemy.gameObject.SetActive(false);
                boardingHealths.Add(enemy.GetComponent<Health>());
            }

            var boardingWaveSpawner = BuildEp03WaveSpawner("BoardingWaveSpawner", new Vector3(0f, 1f, 11f), 3f,
                new List<List<Health>> { boardingHealths }, new[] { haulerFightBarksDialogue });

            // Reach triggers.
            var cargoReachGo = new GameObject("CargoReachPoint");
            cargoReachGo.transform.position = new Vector3(0f, 1f, 14f);
            var containmentReachGo = new GameObject("ContainmentReachPoint");
            containmentReachGo.transform.position = new Vector3(0f, 1f, 28f);

            // Transition box: "TO THE ENGINE ROOM".
            var engineBoxGo = BuildTransitionBox("ToEngineRoomBox", new Vector3(0f, 1.2f, 40f), "TO THE ENGINE ROOM",
                out var engineBtn, out var engineTransition);
            var etSo = new SerializedObject(engineTransition);
            etSo.FindProperty("onFootScene").stringValue = Galaxy1Ep03EngineRoomSceneName;
            etSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(engineBtn.onClick,
                new UnityEngine.Events.UnityAction(engineTransition.LoadOnFootScene));
            engineBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 14;

            // Step 0: Dialogue hauler_hum (the wrongness, auto-play).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Wrongness";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = haulerHumDialogue;

            // Step 1: Dialogue hauler_found (the stowaway found).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Stowaway Found";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = haulerFoundDialogue;

            // Step 2: Trigger — open cargo door.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Open Cargo Door";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = cargoDoor;

            // Step 3: ReachTrigger — cargo bay.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s3.FindPropertyRelative("label").stringValue = "ReachTrigger: Cargo Bay";
            s3.FindPropertyRelative("reachPoint").objectReferenceValue = cargoReachGo.transform;
            s3.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 4: Dialogue hauler_boarding (Dominion boarding comms).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Dominion Boarding";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = haulerBoardingDialogue;

            // Step 5: DefeatWaves — 3 Dominion enforcers in the cargo bay.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s5.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Dominion Enforcers";
            s5.FindPropertyRelative("waveSpawner").objectReferenceValue = boardingWaveSpawner;

            // Step 6: Dialogue hauler_clear (Kessler all-clear).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: All Clear";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = haulerClearDialogue;

            // Step 7: Trigger — open containment door + activate Mira.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s7.FindPropertyRelative("label").stringValue = "Trigger: Open Containment + Mira";
            var t7 = s7.FindPropertyRelative("triggerObjects");
            t7.arraySize = 2;
            t7.GetArrayElementAtIndex(0).objectReferenceValue = containmentDoor;
            t7.GetArrayElementAtIndex(1).objectReferenceValue = miraParent;

            // Step 8: ReachTrigger — containment cell.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s8.FindPropertyRelative("label").stringValue = "ReachTrigger: Containment Cell";
            s8.FindPropertyRelative("reachPoint").objectReferenceValue = containmentReachGo.transform;
            s8.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 9: Dialogue hauler_pod ("You have the same eyes").
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s9.FindPropertyRelative("label").stringValue = "Dialogue: The Cryo-Pod Opened";
            s9.FindPropertyRelative("dialogue").objectReferenceValue = haulerPodDialogue;

            // Step 10: Dialogue hauler_fury (Kessler's operational fury + Hollow Kings intercept).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s10.FindPropertyRelative("label").stringValue = "Dialogue: Kessler's Fury";
            s10.FindPropertyRelative("dialogue").objectReferenceValue = haulerFuryDialogue;

            // Step 11: Trigger — alarm lights (corvette attack ambiance).
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s11.FindPropertyRelative("label").stringValue = "Trigger: Corvette Alarm";
            var t11 = s11.FindPropertyRelative("triggerObjects");
            t11.arraySize = 1;
            t11.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 12: Dialogue hauler_corvette (corvette run + minefield over comms).
            var s12 = stepsProp.GetArrayElementAtIndex(12);
            s12.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s12.FindPropertyRelative("label").stringValue = "Dialogue: Corvette Run";
            s12.FindPropertyRelative("dialogue").objectReferenceValue = haulerCorvetteDialogue;

            // Step 13: Prompt — transition box to the engine room.
            var s13 = stepsProp.GetArrayElementAtIndex(13);
            s13.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s13.FindPropertyRelative("label").stringValue = "Prompt: To the Engine Room";
            s13.FindPropertyRelative("promptObject").objectReferenceValue = engineBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep03HaulerScenePath);
            EnsureScenesInBuild(Galaxy1Ep03HaulerScenePath);

            Debug.Log($"[Space Samurai] EP03 Hauler scene built at {Galaxy1Ep03HaulerScenePath}. " +
                      "Layout: crew corridor (Kessler) → cargo bay (cryo-pods, Iris, 3 Dominion enforcers) → containment cell (Mira's pod) → bridge corridor. " +
                      "14 steps: hauler_hum auto → hauler_found → open cargo door → reach bay → hauler_boarding → defeat 3 enforcers + barks → hauler_clear → " +
                      "open containment + activate Mira → reach cell → hauler_pod → hauler_fury → alarm lights → hauler_corvette comms → transition to Engine Room. " +
                      "Corvette/EVA chapter condensed to comms dialogue + alarm ambiance.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP03 Engine Room", priority = 73)]
        public static void BuildEp03EngineRoom()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Engine room ambiance: warm reactor glow against dark machinery.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.8f, 0.75f);
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.11f, 0.1f);

            // Reactor accent: warm orange glow.
            BuildAccentPointLight("ReactorLight", new Vector3(0f, 2.6f, 16f),
                new Color(1f, 0.6f, 0.3f), intensity: 2.2f, range: 16f);
            // Walkway accent.
            BuildAccentPointLight("WalkwayLight", new Vector3(0f, 2.6f, 5f),
                new Color(0.7f, 0.8f, 0.95f), intensity: 1.4f, range: 10f);
            // Workshop accent: cool terminal light.
            BuildAccentPointLight("WorkshopLight", new Vector3(0f, 2.6f, 30f),
                new Color(0.5f, 0.8f, 1f), intensity: 1.6f, range: 12f);

            // ---- Engine room: walkway (z 0-10) -> reactor chamber (z 10-26) -> workshop alcove (z 26-34).
            var interiorGo = new GameObject("EngineInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.19f, 0.18f);
            var ceilColor = new Color(0.12f, 0.11f, 0.1f);

            // Walkway: x[-2.5,2.5], z[0,10].
            BuildFloorCeiling(interior, "Walkway", new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Walkway_WallW", -2.5f, 0f, 10f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Walkway_WallE", 2.5f, 0f, 10f, new float[0], 2.4f);
            BuildWall(interior, "Walkway_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(5f, 3f, 0.2f));

            // Reactor chamber: x[-5,5], z[10,26], the duel arena.
            BuildFloorCeiling(interior, "ReactorChamber", new Vector3(0f, 0f, 18f), new Vector3(10f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "ReactorChamber_WallW", new Vector3(-5f, 1.5f, 18f), new Vector3(0.2f, 3f, 16f));
            BuildWall(interior, "ReactorChamber_WallE", new Vector3(5f, 1.5f, 18f), new Vector3(0.2f, 3f, 16f));
            BuildDoorwayWall(interior, "ReactorChamber_WallFront", new Vector3(0f, 1.5f, 10f), 10f, true, 2.4f);
            BuildDoorwayWall(interior, "ReactorChamber_WallBack", new Vector3(0f, 1.5f, 26f), 10f, true, 2.4f);

            // Workshop alcove: x[-3,3], z[26,34], terminal + transition.
            BuildFloorCeiling(interior, "Workshop", new Vector3(0f, 0f, 30f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Workshop_WallW", new Vector3(-3f, 1.5f, 30f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Workshop_WallE", new Vector3(3f, 1.5f, 30f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Workshop_WallBack", new Vector3(0f, 1.5f, 34f), new Vector3(6f, 3f, 0.2f));

            // Reactor column: tall warm-tinted box at the chamber's side (visual centerpiece).
            var reactorBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            reactorBox.name = "ReactorColumn";
            reactorBox.transform.SetParent(interior, false);
            reactorBox.transform.localPosition = new Vector3(-3.8f, 1.4f, 16f);
            reactorBox.transform.localScale = new Vector3(1.6f, 2.8f, 1.6f);
            TintShared(reactorBox.GetComponent<Renderer>(), new Color(0.9f, 0.45f, 0.2f));

            // Steam vents: pale semi-transparent boxes rising near the reactor (visual only).
            var steamColor = new Color(0.8f, 0.85f, 0.9f, 0.35f);
            BuildProp(interior, "Steam1", new Vector3(-2.5f, 1.5f, 14f), new Vector3(0.6f, 3f, 0.6f), steamColor);
            BuildProp(interior, "Steam2", new Vector3(2f, 1.5f, 19f), new Vector3(0.5f, 3f, 0.5f), steamColor);
            BuildProp(interior, "Steam3", new Vector3(-1f, 1.5f, 22f), new Vector3(0.5f, 3f, 0.5f), steamColor);

            // Terminal prop in the workshop.
            BuildProp(interior, "Terminal", new Vector3(0f, 0.7f, 32f), new Vector3(1.4f, 1.4f, 0.6f), new Color(0.15f, 0.2f, 0.25f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Mira: walkway (scale 0.6) — waits for Ronin's return.
            BuildEp03ChildNpc("Mira_Engine", "Mira", new Vector3(1f, 0.6f, 5f), 0.6f);

            // Kessler: reactor chamber, by the diagnosis spot.
            var kesslerPos = new Vector3(-1.5f, 1f, 14f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_Engine");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var engineReturnDialogue = BuildEp03DialoguePlayer("Dialogue_EngineReturn", new Vector3(1f, 1f, 5f), "engine_return");
            var engineScarDialogue = BuildEp03DialoguePlayer("Dialogue_EngineScar", kesslerPos, "engine_scar", talkRef);
            var engineClockDialogue = BuildEp03DialoguePlayer("Dialogue_EngineClock", kesslerPos, "engine_clock", talkRef);
            var hunterChallengeDialogue = BuildEp03DialoguePlayer("Dialogue_HunterChallenge", new Vector3(0f, 1.5f, 18f), "engine_hunter_challenge");
            var hunterAfterDialogue = BuildEp03DialoguePlayer("Dialogue_HunterAfter", new Vector3(0f, 1.5f, 18f), "engine_hunter_after");
            var engineBriefDialogue = BuildEp03DialoguePlayer("Dialogue_EngineBrief", new Vector3(0f, 1f, 32f), "engine_brief", talkRef);
            var lotusDecisionDialogue = BuildEp03DialoguePlayer("Dialogue_LotusDecision", new Vector3(0f, 1f, 32f), "engine_lotus_decision", talkRef);

            // ---- Enemy: the Dominion Hunter — a single elite duelist with boosted health. ----
            var hunterSilver = new Color(0.8f, 0.82f, 0.88f);
            var hunter = BuildDominionEnemy(new Vector3(0f, 0f, 19f), playerHealth, enemyDef);
            var hunterRenderer = hunter.GetComponent<Renderer>();
            if (hunterRenderer != null) TintShared(hunterRenderer, hunterSilver);
            var hunterHealth = hunter.GetComponent<Health>();
            if (hunterHealth != null)
            {
                // ~3.5x a normal enforcer: a duel, not a skirmish.
                var hhSo = new SerializedObject(hunterHealth);
                hhSo.FindProperty("maxHealth").floatValue = hhSo.FindProperty("maxHealth").floatValue * 3.5f;
                hhSo.ApplyModifiedPropertiesWithoutUndo();
            }
            hunter.gameObject.SetActive(false);

            var hunterWaveSpawner = BuildEp03WaveSpawner("HunterWaveSpawner", new Vector3(0f, 1f, 13f), 3f,
                new List<List<Health>> { new List<Health> { hunterHealth } }, new[] { hunterChallengeDialogue });

            // Reach triggers.
            var reactorReachGo = new GameObject("ReactorReachPoint");
            reactorReachGo.transform.position = new Vector3(0f, 1f, 14f);
            var workshopReachGo = new GameObject("WorkshopReachPoint");
            workshopReachGo.transform.position = new Vector3(0f, 1f, 30f);

            // Transition box: "DOCK AT THE LOTUS STATION".
            var lotusBoxGo = BuildTransitionBox("ToLotusBox", new Vector3(0f, 1.2f, 32.5f), "DOCK AT THE LOTUS STATION",
                out var lotusBtn, out var lotusTransition);
            var ltSo = new SerializedObject(lotusTransition);
            ltSo.FindProperty("onFootScene").stringValue = Galaxy1Ep03LotusSceneName;
            ltSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(lotusBtn.onClick,
                new UnityEngine.Events.UnityAction(lotusTransition.LoadOnFootScene));
            lotusBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 10;

            // Step 0: Dialogue engine_return (Mira waits; the indoctrination language).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Return";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = engineReturnDialogue;

            // Step 1: ReachTrigger — reactor chamber.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Reactor Chamber";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = reactorReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Dialogue engine_scar (Kessler reads the implant).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Scar Reading";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = engineScarDialogue;

            // Step 3: Dialogue engine_clock (the failed-execution reveal — EP03's core reveal).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The Clock Is Opened";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = engineClockDialogue;

            // Step 4: DefeatWaves — the Dominion Hunter duel.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s4.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Hunter Duel";
            s4.FindPropertyRelative("waveSpawner").objectReferenceValue = hunterWaveSpawner;

            // Step 5: Dialogue engine_hunter_after ("I'm sorry").
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: After the Duel";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = hunterAfterDialogue;

            // Step 6: ReachTrigger — workshop terminal.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s6.FindPropertyRelative("label").stringValue = "ReachTrigger: Workshop Terminal";
            s6.FindPropertyRelative("reachPoint").objectReferenceValue = workshopReachGo.transform;
            s6.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 7: Dialogue engine_brief (the data-chip: Mira was salvage).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: The Mission Brief";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = engineBriefDialogue;

            // Step 8: Dialogue engine_lotus_decision (the Lotus trap decision).
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s8.FindPropertyRelative("label").stringValue = "Dialogue: The Lotus Decision";
            s8.FindPropertyRelative("dialogue").objectReferenceValue = lotusDecisionDialogue;

            // Step 9: Prompt — transition box to the Lotus station.
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s9.FindPropertyRelative("label").stringValue = "Prompt: Dock at the Lotus Station";
            s9.FindPropertyRelative("promptObject").objectReferenceValue = lotusBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep03EngineRoomScenePath);
            EnsureScenesInBuild(Galaxy1Ep03EngineRoomScenePath);

            Debug.Log($"[Space Samurai] EP03 Engine Room scene built at {Galaxy1Ep03EngineRoomScenePath}. " +
                      "Layout: walkway (Mira) → reactor chamber (Kessler, steam, Hunter duel) → workshop (terminal, transition). " +
                      "10 steps: engine_return auto → reach reactor → engine_scar → engine_clock (failed-execution reveal) → Hunter duel (3.5x health, silver) + challenge bark →" +
                      "engine_hunter_after → reach workshop → engine_brief → engine_lotus_decision → transition to Lotus Station.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP03 Lotus Station", priority = 74)]
        public static void BuildEp03LotusStation()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Crimson Lotus station: toxic spore haze — green fog + sickly green/magenta accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.85f, 0.7f);
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.14f, 0.1f);

            // Spore haze: green exponential fog, low density so the far end of rooms reads hazy.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.4f, 0.25f);
            RenderSettings.fogDensity = 0.035f;

            // Sickly accents.
            BuildAccentPointLight("SporeLight1", new Vector3(0f, 2.6f, 12f),
                new Color(0.5f, 1f, 0.5f), intensity: 1.6f, range: 12f);
            BuildAccentPointLight("MedicalLight", new Vector3(0f, 2.6f, 27f),
                new Color(0.9f, 0.4f, 0.9f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("PodBayLight", new Vector3(0f, 2.6f, 42f),
                new Color(0.5f, 0.9f, 0.6f), intensity: 1.8f, range: 16f);
            BuildAccentPointLight("ExtractionLight", new Vector3(0f, 2.6f, 54f),
                new Color(0.5f, 0.8f, 1f), intensity: 1.5f, range: 10f);

            // ---- Lotus interior: airlock (z 0-6) -> spore corridor (z 6-20) -> medical chamber (z 20-34) ->
            // pod bay 7-C (z 34-50) -> extraction airlock (z 50-58).
            var interiorGo = new GameObject("LotusInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.18f, 0.22f, 0.18f);
            var ceilColor = new Color(0.1f, 0.13f, 0.1f);

            // Airlock: x[-4,4], z[0,6].
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 3f), new Vector3(8f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallE", new Vector3(4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 6f), 8f, true, 2.4f);

            // Spore corridor: x[-3,3], z[6,20].
            BuildFloorCeiling(interior, "SporeCorridor", new Vector3(0f, 0f, 13f), new Vector3(6f, 0f, 14f), floorColor, ceilColor);
            BuildCorridorWall(interior, "SporeCorridor_WallW", -3f, 6f, 20f, new float[0], 2.4f);
            BuildCorridorWall(interior, "SporeCorridor_WallE", 3f, 6f, 20f, new float[0], 2.4f);

            // Medical chamber: x[-5,5], z[20,34].
            BuildFloorCeiling(interior, "Medical", new Vector3(0f, 0f, 27f), new Vector3(10f, 0f, 14f), floorColor, ceilColor);
            BuildWall(interior, "Medical_WallW", new Vector3(-5f, 1.5f, 27f), new Vector3(0.2f, 3f, 14f));
            BuildWall(interior, "Medical_WallE", new Vector3(5f, 1.5f, 27f), new Vector3(0.2f, 3f, 14f));
            BuildDoorwayWall(interior, "Medical_WallBack", new Vector3(0f, 1.5f, 34f), 10f, true, 2.4f);

            // Pod bay 7-C: x[-6,6], z[34,50].
            BuildFloorCeiling(interior, "PodBay", new Vector3(0f, 0f, 42f), new Vector3(12f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "PodBay_WallW", new Vector3(-6f, 1.5f, 42f), new Vector3(0.2f, 3f, 16f));
            BuildWall(interior, "PodBay_WallE", new Vector3(6f, 1.5f, 42f), new Vector3(0.2f, 3f, 16f));
            BuildDoorwayWall(interior, "PodBay_WallBack", new Vector3(0f, 1.5f, 50f), 12f, true, 2.4f);

            // Extraction airlock: x[-3,3], z[50,58].
            BuildFloorCeiling(interior, "Extraction", new Vector3(0f, 0f, 54f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Extraction_WallW", new Vector3(-3f, 1.5f, 54f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Extraction_WallE", new Vector3(3f, 1.5f, 54f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Extraction_WallBack", new Vector3(0f, 1.5f, 58f), new Vector3(6f, 3f, 0.2f));

            // Pod-bay door (marked 7-C, locked until Mira finds the children).
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var podBayDoor = BuildSlidingDoor(interior, "PodBayDoor", new Vector3(0f, 0f, 34f), 2.4f, true, startLocked: true);
            WireDoorAudio(podBayDoor, doorSlideClip);

            // Spore-growth props: sickly green clutter.
            var sporeColor = new Color(0.4f, 0.65f, 0.35f, 0.6f);
            BuildProp(interior, "Spore1", new Vector3(-2f, 0.5f, 10f), new Vector3(1f, 1f, 1f), sporeColor);
            BuildProp(interior, "Spore2", new Vector3(2.2f, 0.4f, 16f), new Vector3(0.8f, 0.8f, 0.8f), sporeColor);
            BuildProp(interior, "Spore3", new Vector3(-3.5f, 0.5f, 24f), new Vector3(1.1f, 1f, 1.1f), sporeColor);
            BuildProp(interior, "MedTable", new Vector3(3f, 0.5f, 28f), new Vector3(2f, 1f, 1f), new Color(0.3f, 0.32f, 0.34f));

            // Rescue-pod capsule props lining the pod bay.
            var rescuePodColor = new Color(0.3f, 0.38f, 0.45f);
            BuildProp(interior, "RescuePod1", new Vector3(-4.5f, 0.7f, 38f), new Vector3(1f, 1.4f, 2f), rescuePodColor);
            BuildProp(interior, "RescuePod2", new Vector3(4.5f, 0.7f, 38f), new Vector3(1f, 1.4f, 2f), rescuePodColor);
            BuildProp(interior, "RescuePod3", new Vector3(-4.5f, 0.7f, 45f), new Vector3(1f, 1.4f, 2f), rescuePodColor);
            BuildProp(interior, "RescuePod4", new Vector3(4.5f, 0.7f, 45f), new Vector3(1f, 1.4f, 2f), rescuePodColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 70f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // The Block Seven children: 6 small capsules in the pod bay, inactive until rescued.
            var childParent = new GameObject("Children");
            childParent.transform.SetParent(interior, false);
            var childPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 39f),
                new Vector3(3f, 0f, 40f),
                new Vector3(-2f, 0f, 43f),
                new Vector3(2f, 0f, 44f),
                new Vector3(-1f, 0f, 46f),
                new Vector3(1f, 0f, 47f),
            };
            for (int i = 0; i < childPositions.Length; i++)
            {
                float scale = i % 2 == 0 ? 0.6f : 0.7f;
                var childGo = BuildEp03ChildNpc($"Child_{i}", "Block Seven Child",
                    new Vector3(childPositions[i].x, scale, childPositions[i].z), scale);
                if (childGo != null) childGo.transform.SetParent(childParent.transform, true);
            }
            childParent.SetActive(false); // Activated when the pod bay opens.

            // Senna (14, scale 0.8): waits at the extraction airlock threshold.
            BuildEp03ChildNpc("Senna_Lotus", "Senna", new Vector3(1f, 0.8f, 52f), 0.8f);

            // Mira has no body here — she guides over comms from the hauler (DialoguePlayers only).

            // ---- Alarm lights (strike-craft descent, activated after Khall's ultimatum). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-3f, 2.4f, 44f), new Vector3(3f, 2.4f, 50f));

            // ---- Dialogue Players ----
            var lotusGuidanceDialogue = BuildEp03DialoguePlayer("Dialogue_LotusGuidance", new Vector3(0f, 1.5f, 4f), "lotus_guidance");
            var lotusFightBarksDialogue = BuildEp03DialoguePlayer("Dialogue_LotusFightBarks", new Vector3(0f, 1.5f, 13f), "lotus_fight_barks");
            var lotusPodbayDialogue = BuildEp03DialoguePlayer("Dialogue_LotusPodbay", new Vector3(0f, 1.5f, 30f), "lotus_podbay");
            var lotusSennaDialogue = BuildEp03DialoguePlayer("Dialogue_LotusSenna", new Vector3(1f, 1f, 52f), "lotus_senna", talkRef);
            var lotusKhallDialogue = BuildEp03DialoguePlayer("Dialogue_LotusKhall", new Vector3(0f, 2.2f, 44f), "lotus_khall");
            var lotusReunionDialogue = BuildEp03DialoguePlayer("Dialogue_LotusReunion", new Vector3(0f, 1.5f, 54f), "lotus_reunion", talkRef);

            // ---- Enemies: Crimson Lotus enforcers (green tint). ----
            var lotusGreen = new Color(0.35f, 0.6f, 0.3f);

            // Wave 1: 4 enforcers in the spore corridor.
            var corridorPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 10f),
                new Vector3(1.5f, 0f, 13f),
                new Vector3(-1f, 0f, 16f),
                new Vector3(1f, 0f, 19f)
            };
            var corridorHealths = new List<Health>();
            foreach (var pos in corridorPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, lotusGreen);
                enemy.gameObject.SetActive(false);
                corridorHealths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 5 enforcers in the medical chamber.
            var medicalPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 23f),
                new Vector3(3f, 0f, 24f),
                new Vector3(-2f, 0f, 27f),
                new Vector3(2f, 0f, 30f),
                new Vector3(0f, 0f, 32f)
            };
            var medicalHealths = new List<Health>();
            foreach (var pos in medicalPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, lotusGreen);
                enemy.gameObject.SetActive(false);
                medicalHealths.Add(enemy.GetComponent<Health>());
            }

            var corridorWaveSpawner = BuildEp03WaveSpawner("CorridorWaveSpawner", new Vector3(0f, 1f, 8f), 3f,
                new List<List<Health>> { corridorHealths }, new[] { lotusFightBarksDialogue });
            var medicalWaveSpawner = BuildEp03WaveSpawner("MedicalWaveSpawner", new Vector3(0f, 1f, 22f), 3f,
                new List<List<Health>> { medicalHealths }, null);

            // Reach triggers.
            var medicalReachGo = new GameObject("MedicalReachPoint");
            medicalReachGo.transform.position = new Vector3(0f, 1f, 26f);
            var podBayReachGo = new GameObject("PodBayReachPoint");
            podBayReachGo.transform.position = new Vector3(0f, 1f, 42f);
            var extractionReachGo = new GameObject("ExtractionReachPoint");
            extractionReachGo.transform.position = new Vector3(0f, 1f, 54f);

            // Transition box: "JUMP TO THE FREE TERRITORIES".
            var jumpBoxGo = BuildTransitionBox("JumpToSanctuaryBox", new Vector3(0f, 1.2f, 56f), "JUMP TO THE FREE TERRITORIES",
                out var jumpBtn, out var jumpTransition);
            var jtSo = new SerializedObject(jumpTransition);
            jtSo.FindProperty("onFootScene").stringValue = Galaxy1Ep03SanctuarySceneName;
            jtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(jumpBtn.onClick,
                new UnityEngine.Events.UnityAction(jumpTransition.LoadOnFootScene));
            jumpBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 13;

            // Step 0: Dialogue lotus_guidance (Mira on comms).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Mira's Guidance";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lotusGuidanceDialogue;

            // Step 1: DefeatWaves — 4 corridor enforcers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Corridor Enforcers";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = corridorWaveSpawner;

            // Step 2: ReachTrigger — medical chamber.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Medical Chamber";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = medicalReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: DefeatWaves — 5 medical enforcers.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 5 Medical Enforcers";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = medicalWaveSpawner;

            // Step 4: Dialogue lotus_podbay (Mira: door marked 7-C).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Pod Bay 7-C";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = lotusPodbayDialogue;

            // Step 5: Trigger — open pod-bay door + activate children.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s5.FindPropertyRelative("label").stringValue = "Trigger: Open Pod Bay + Children";
            var t5 = s5.FindPropertyRelative("triggerObjects");
            t5.arraySize = 2;
            t5.GetArrayElementAtIndex(0).objectReferenceValue = podBayDoor;
            t5.GetArrayElementAtIndex(1).objectReferenceValue = childParent;

            // Step 6: ReachTrigger — pod bay.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s6.FindPropertyRelative("label").stringValue = "ReachTrigger: Pod Bay";
            s6.FindPropertyRelative("reachPoint").objectReferenceValue = podBayReachGo.transform;
            s6.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 7: Dialogue lotus_senna (Senna's question).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Senna's Question";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = lotusSennaDialogue;

            // Step 8: Dialogue lotus_khall (the ultimatum, speakers only).
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s8.FindPropertyRelative("label").stringValue = "Dialogue: Khall's Ultimatum";
            s8.FindPropertyRelative("dialogue").objectReferenceValue = lotusKhallDialogue;

            // Step 9: Trigger — alarm lights (strike-craft descend).
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s9.FindPropertyRelative("label").stringValue = "Trigger: Strike-Craft Alarm";
            var t9 = s9.FindPropertyRelative("triggerObjects");
            t9.arraySize = 1;
            t9.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 10: ReachTrigger — extraction airlock.
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s10.FindPropertyRelative("label").stringValue = "ReachTrigger: Extraction Airlock";
            s10.FindPropertyRelative("reachPoint").objectReferenceValue = extractionReachGo.transform;
            s10.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 11: Dialogue lotus_reunion (escape + "You came back for us").
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s11.FindPropertyRelative("label").stringValue = "Dialogue: Reunion";
            s11.FindPropertyRelative("dialogue").objectReferenceValue = lotusReunionDialogue;

            // Step 12: Prompt — transition box to the sanctuary.
            var s12 = stepsProp.GetArrayElementAtIndex(12);
            s12.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s12.FindPropertyRelative("label").stringValue = "Prompt: Jump to the Free Territories";
            s12.FindPropertyRelative("promptObject").objectReferenceValue = jumpBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep03LotusScenePath);
            EnsureScenesInBuild(Galaxy1Ep03LotusScenePath);

            Debug.Log($"[Space Samurai] EP03 Lotus Station scene built at {Galaxy1Ep03LotusScenePath}. " +
                      "Layout: airlock → spore corridor (4 enforcers) → medical chamber (5 enforcers) → pod bay 7-C (6 children) → extraction (Senna). " +
                      "Green fog + sickly accents for spore haze. Mira guides over comms (no body). " +
                      "13 steps: lotus_guidance → defeat corridor wave + barks → reach medical → defeat medical wave → lotus_podbay → open pod bay + children → " +
                      "reach pod bay → lotus_senna → lotus_khall (speakers) → strike-craft alarm → reach extraction → lotus_reunion → transition to Sanctuary.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP03 Sanctuary", priority = 75)]
        public static void BuildEp03Sanctuary()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fringe sanctuary: warm, lived-in light — the first safe place in the story.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.92f, 0.8f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.16f, 0.14f);

            // Warm garden + medical accents.
            BuildAccentPointLight("MedicalLight", new Vector3(0f, 2.4f, 5f),
                new Color(0.7f, 0.9f, 1f), intensity: 1.5f, range: 10f);
            BuildAccentPointLight("GardenLight1", new Vector3(-3f, 2.4f, 15f),
                new Color(1f, 0.85f, 0.6f), intensity: 1.8f, range: 12f);
            BuildAccentPointLight("GardenLight2", new Vector3(3f, 2.4f, 23f),
                new Color(1f, 0.8f, 0.55f), intensity: 1.7f, range: 12f);
            BuildAccentPointLight("PerimeterLight", new Vector3(0f, 2.4f, 36f),
                new Color(0.8f, 0.85f, 1f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("ObservatoryLight", new Vector3(0f, 2.4f, 48f),
                new Color(0.6f, 0.85f, 1f), intensity: 1.9f, range: 12f);

            // ---- Sanctuary: medical room (z 0-10) -> garden hall (z 10-28) ->
            // landing-platform perimeter (z 28-44) -> observatory nook (z 44-52).
            var interiorGo = new GameObject("SanctuaryInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.26f, 0.24f, 0.2f);
            var ceilColor = new Color(0.16f, 0.15f, 0.13f);

            // Medical room: x[-4,4], z[0,10].
            BuildFloorCeiling(interior, "MedicalRoom", new Vector3(0f, 0f, 5f), new Vector3(8f, 0f, 10f), floorColor, ceilColor);
            BuildWall(interior, "MedicalRoom_WallW", new Vector3(-4f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MedicalRoom_WallE", new Vector3(4f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MedicalRoom_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));
            BuildDoorwayWall(interior, "MedicalRoom_WallBack", new Vector3(0f, 1.5f, 10f), 8f, true, 2.4f);

            // Garden hall: x[-6,6], z[10,28], the heart of the sanctuary.
            BuildFloorCeiling(interior, "GardenHall", new Vector3(0f, 0f, 19f), new Vector3(12f, 0f, 18f),
                new Color(0.24f, 0.3f, 0.2f), ceilColor);
            BuildWall(interior, "GardenHall_WallW", new Vector3(-6f, 1.5f, 19f), new Vector3(0.2f, 3f, 18f));
            BuildWall(interior, "GardenHall_WallE", new Vector3(6f, 1.5f, 19f), new Vector3(0.2f, 3f, 18f));
            BuildDoorwayWall(interior, "GardenHall_WallBack", new Vector3(0f, 1.5f, 28f), 12f, true, 2.4f);

            // Landing-platform perimeter: x[-7,7], z[28,44], where the Rustfangs hit.
            BuildFloorCeiling(interior, "Perimeter", new Vector3(0f, 0f, 36f), new Vector3(14f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "Perimeter_WallW", new Vector3(-7f, 1.5f, 36f), new Vector3(0.2f, 3f, 16f));
            BuildWall(interior, "Perimeter_WallE", new Vector3(7f, 1.5f, 36f), new Vector3(0.2f, 3f, 16f));
            BuildDoorwayWall(interior, "Perimeter_WallBack", new Vector3(0f, 1.5f, 44f), 14f, true, 2.4f);

            // Observatory nook: x[-4,4], z[44,52], stars through the window.
            BuildFloorCeiling(interior, "Observatory", new Vector3(0f, 0f, 48f), new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Observatory_WallW", new Vector3(-4f, 1.5f, 48f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Observatory_WallE", new Vector3(4f, 1.5f, 48f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Observatory_WallBack", new Vector3(0f, 1.5f, 52f), new Vector3(8f, 3f, 0.2f));

            // Observatory window: large transparent "glass" quad on the back wall (visual only).
            var windowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowQuad.name = "Window";
            windowQuad.transform.SetParent(interior, false);
            windowQuad.transform.localPosition = new Vector3(0f, 1.5f, 52f);
            windowQuad.transform.localScale = new Vector3(6f, 2f, 1f);
            TintShared(windowQuad.GetComponent<Renderer>(), new Color(0.1f, 0.2f, 0.4f, 0.6f));
            var windowCollider = windowQuad.GetComponent<Collider>();
            if (windowCollider != null) Object.DestroyImmediate(windowCollider);

            // Garden planters + medical props.
            var planterGreen = new Color(0.3f, 0.55f, 0.25f);
            BuildProp(interior, "Planter1", new Vector3(-4f, 0.5f, 13f), new Vector3(2f, 1f, 1f), planterGreen);
            BuildProp(interior, "Planter2", new Vector3(4f, 0.5f, 17f), new Vector3(2f, 1f, 1f), planterGreen);
            BuildProp(interior, "Planter3", new Vector3(-3.5f, 0.5f, 22f), new Vector3(2f, 1f, 1f), planterGreen);
            BuildProp(interior, "Planter4", new Vector3(3.5f, 0.5f, 25f), new Vector3(2f, 1f, 1f), planterGreen);
            BuildProp(interior, "MedBed", new Vector3(-2f, 0.5f, 5f), new Vector3(1f, 1f, 2.2f), new Color(0.5f, 0.55f, 0.6f));
            BuildProp(interior, "Crate1", new Vector3(5f, 0.7f, 33f), new Vector3(1.2f, 1.4f, 1f), new Color(0.4f, 0.35f, 0.3f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Resistance Doctor: medical room, wandering.
            var doctorPos = new Vector3(1.5f, 1f, 5f);
            var doctorGo = InstantiateNpc(DrHerisPrefabPath, AtFloor(doctorPos), "ResistanceDoctor");
            if (doctorGo != null)
            {
                var doctorNpc = doctorGo.AddComponent<StoryNpc>();
                var doctorSo = new SerializedObject(doctorNpc);
                doctorSo.FindProperty("displayName").stringValue = "Resistance Doctor";
                doctorSo.FindProperty("remote").boolValue = false;
                doctorSo.ApplyModifiedPropertiesWithoutUndo();

                var wander = doctorGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = 2f;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler + Iris: observatory.
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, new Vector3(-2f, 1f, 47f), "Kessler_Sanctuary");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildEp03ChildNpc("Iris_Sanctuary", "Iris", new Vector3(2f, 0.7f, 48f), 0.7f, IrisPrefabPath);

            // Mira, Senna + 5 children wandering the garden — learning to be people.
            var gardenChildren = new (string go, string name, Vector3 pos, float scale)[]
            {
                ("Mira_Sanctuary", "Mira", new Vector3(-2f, 0.6f, 14f), 0.6f),
                ("Senna_Sanctuary", "Senna", new Vector3(2.5f, 0.8f, 16f), 0.8f),
                ("Child_0", "Block Seven Child", new Vector3(-3f, 0.65f, 19f), 0.65f),
                ("Child_1", "Block Seven Child", new Vector3(3f, 0.6f, 21f), 0.6f),
                ("Child_2", "Block Seven Child", new Vector3(-1.5f, 0.7f, 23f), 0.7f),
                ("Child_3", "Block Seven Child", new Vector3(1.5f, 0.65f, 25f), 0.65f),
                ("Child_4", "Block Seven Child", new Vector3(0f, 0.6f, 18f), 0.6f),
            };
            foreach (var (goName, displayName, pos, scale) in gardenChildren)
            {
                var childGo = BuildEp03ChildNpc(goName, displayName, pos, scale);
                if (childGo != null)
                {
                    var wander = childGo.AddComponent<StoryNpcWander>();
                    var wanderSo = new SerializedObject(wander);
                    wanderSo.FindProperty("wanderRadius").floatValue = 2.5f;
                    wanderSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ---- Dialogue Players ----
            var sanctuaryDoctorDialogue = BuildEp03DialoguePlayer("Dialogue_SanctuaryDoctor", doctorPos, "sanctuary_doctor", talkRef);
            var assaultBarksDialogue = BuildEp03DialoguePlayer("Dialogue_AssaultBarks", new Vector3(0f, 1.5f, 36f), "sanctuary_assault_barks");
            var sanctuaryQuietDialogue = BuildEp03DialoguePlayer("Dialogue_SanctuaryQuiet", new Vector3(0f, 1f, 19f), "sanctuary_quiet", talkRef);
            var sanctuaryFileDialogue = BuildEp03DialoguePlayer("Dialogue_SanctuaryFile", new Vector3(-2f, 1f, 14f), "sanctuary_file", talkRef);
            var observatoryDialogue = BuildEp03DialoguePlayer("Dialogue_Observatory", new Vector3(0f, 1f, 48f), "sanctuary_observatory", talkRef);

            // ---- Enemies: Rustfangs (rust-orange tint). ----
            var rustOrange = new Color(0.75f, 0.4f, 0.15f);

            // Assault wave 1: 4 raiders on the platform; wave 2: 5 raiders deeper in.
            var assaultWave1Positions = new Vector3[]
            {
                new Vector3(-3f, 0f, 31f),
                new Vector3(3f, 0f, 32f),
                new Vector3(-1.5f, 0f, 35f),
                new Vector3(1.5f, 0f, 36f)
            };
            var assaultWave2Positions = new Vector3[]
            {
                new Vector3(-4f, 0f, 34f),
                new Vector3(4f, 0f, 35f),
                new Vector3(-2f, 0f, 39f),
                new Vector3(2f, 0f, 40f),
                new Vector3(0f, 0f, 42f)
            };
            List<Health> BuildRustfangs(Vector3[] positions)
            {
                var healths = new List<Health>();
                foreach (var pos in positions)
                {
                    var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                    var renderer = enemy.GetComponent<Renderer>();
                    if (renderer != null) TintShared(renderer, rustOrange);
                    enemy.gameObject.SetActive(false);
                    healths.Add(enemy.GetComponent<Health>());
                }
                return healths;
            }
            var assaultWave1Healths = BuildRustfangs(assaultWave1Positions);
            var assaultWave2Healths = BuildRustfangs(assaultWave2Positions);

            var assaultWaveSpawner = BuildEp03WaveSpawner("AssaultWaveSpawner", new Vector3(0f, 1f, 30f), 3.5f,
                new List<List<Health>> { assaultWave1Healths, assaultWave2Healths },
                new DialoguePlayer[] { assaultBarksDialogue, null });

            // Reach triggers.
            var perimeterReachGo = new GameObject("PerimeterReachPoint");
            perimeterReachGo.transform.position = new Vector3(0f, 1f, 32f);
            var gardenReachGo = new GameObject("GardenReachPoint");
            gardenReachGo.transform.position = new Vector3(0f, 1f, 18f);
            var observatoryReachGo = new GameObject("ObservatoryReachPoint");
            observatoryReachGo.transform.position = new Vector3(0f, 1f, 48f);

            // Transition box: "LAUNCH TO SPACE" — wired to ReturnToSpace() so EP03 registers complete.
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 50f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 9;

            // Step 0: Dialogue sanctuary_doctor (the killswitch jammed).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Doctor";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = sanctuaryDoctorDialogue;

            // Step 1: ReachTrigger — landing-platform perimeter.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Perimeter";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = perimeterReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: DefeatWaves — Rustfangs assault (2 waves: 4 + 5).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Rustfangs Assault";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = assaultWaveSpawner;

            // Step 3: Dialogue sanctuary_quiet (Mira: "What will you do now?").
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Sanctuary Quiet";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = sanctuaryQuietDialogue;

            // Step 4: ReachTrigger — garden.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Garden";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = gardenReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue sanctuary_file (the encrypted Kethel-7 file).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Encrypted File";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = sanctuaryFileDialogue;

            // Step 6: ReachTrigger — observatory.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s6.FindPropertyRelative("label").stringValue = "ReachTrigger: Observatory";
            s6.FindPropertyRelative("reachPoint").objectReferenceValue = observatoryReachGo.transform;
            s6.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 7: Dialogue sanctuary_observatory (Iris's question — the EP04 hook).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Observatory Night";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = observatoryDialogue;

            // Step 8: Prompt — launch-to-space transition box (wired to ReturnToSpace).
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s8.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            s8.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep03SanctuaryScenePath);
            EnsureScenesInBuild(Galaxy1Ep03SanctuaryScenePath);

            Debug.Log($"[Space Samurai] EP03 Sanctuary scene built at {Galaxy1Ep03SanctuaryScenePath}. " +
                      "Layout: medical room (Doctor) → garden hall (Mira/Senna/5 children wandering, planters) → perimeter (Rustfangs assault) → observatory (Kessler/Iris, window). " +
                      "9 steps: sanctuary_doctor → reach perimeter → defeat Rustfangs assault (2 waves: 4+5) + barks → sanctuary_quiet → reach garden → " +
                      "sanctuary_file → reach observatory → sanctuary_observatory → LAUNCH TO SPACE (ReturnToSpace, marks EP03 complete).");
        }
    }
}

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
    /// EP02 station-interior scene builders. Builds the Velorum station scenes where Ronin-7 infiltrates
    /// to rescue Iris and 247 children from the Dominion pipeline. Wires all MissionDirector steps,
    /// enemy waves, and NPC interactions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP02 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep02" and loads lines from Ep02Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp02DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep02Lines.Get(setId), advanceRef, setId, clipPrefix: "ep02");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP02 Station Docking", priority = 68)]
        public static void BuildEp02Docking()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cool docking-collar ambiance: cyan accent lights + dark metal walls.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.82f, 0.95f);
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.18f);

            // Docking-collar accent: cool cyan light over the cargo area.
            BuildAccentPointLight("DockingLight", new Vector3(0f, 2.6f, 12f),
                new Color(0.5f, 0.8f, 1f), intensity: 1.8f, range: 14f);
            // Checkpoint widening accent: warning-warm light.
            BuildAccentPointLight("CheckpointLight", new Vector3(0f, 2.6f, 26f),
                new Color(1f, 0.75f, 0.5f), intensity: 1.6f, range: 12f);
            // Service corridor accent: dimmer, cooler.
            BuildAccentPointLight("ServiceLight", new Vector3(0f, 2.6f, 36f),
                new Color(0.6f, 0.78f, 1f), intensity: 1.4f, range: 12f);

            // ---- Docking interior: airlock room (z 0-6) -> docking collar (z 6-22) ->
            // checkpoint widening (z 22-30) -> service corridor (z 30-40).
            var interiorGo = new GameObject("DockingInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.26f);
            var ceilColor = new Color(0.12f, 0.13f, 0.16f);

            // Airlock room: x[-4,4], z[-2,6].
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 2f), new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-4f, 1.5f, 2f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Airlock_WallE", new Vector3(4f, 1.5f, 2f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, -2f), new Vector3(8f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 6f), 8f, true, 2.4f);

            // Docking-collar corridor: x[-2.5,2.5], z[6,22], narrower for "inside the collar" feel.
            BuildFloorCeiling(interior, "Collar", new Vector3(0f, 0f, 14f), new Vector3(5f, 0f, 16f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Collar_WallW", -2.5f, 6f, 22f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Collar_WallE", 2.5f, 6f, 22f, new float[0], 2.4f);

            // Checkpoint widening: x[-4,4], z[22,30] (opens up as security tightens).
            BuildFloorCeiling(interior, "Checkpoint", new Vector3(0f, 0f, 26f), new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Checkpoint_WallW", new Vector3(-4f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Checkpoint_WallE", new Vector3(4f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildDoorwayWall(interior, "Checkpoint_WallBack", new Vector3(0f, 1.5f, 30f), 8f, true, 2.4f);

            // Service corridor: x[-3,3], z[30,40], warmer dimmer lighting mood.
            BuildFloorCeiling(interior, "Service", new Vector3(0f, 0f, 35f), new Vector3(6f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Service_WallW", -3f, 30f, 40f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Service_WallE", 3f, 30f, 40f, new float[0], 2.4f);

            // Cargo-container props in the docking collar (visual clutter).
            BuildProp(interior, "Cargo1", new Vector3(-1.5f, 0.8f, 9f), new Vector3(1.2f, 1.6f, 1f), new Color(0.35f, 0.35f, 0.35f));
            BuildProp(interior, "Cargo2", new Vector3(1.5f, 0.8f, 16f), new Vector3(1f, 1.4f, 1.2f), new Color(0.32f, 0.32f, 0.32f));

            // Sliding doors.
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var airlockDoor = BuildSlidingDoor(interior, "AirlockDoor", new Vector3(0f, 0f, 6f), 2.4f, true, startLocked: false);
            WireDoorAudio(airlockDoor, doorSlideClip);
            var serviceDoor = BuildSlidingDoor(interior, "ServiceDoor", new Vector3(0f, 0f, 30f), 2.4f, true, startLocked: true);
            WireDoorAudio(serviceDoor, doorSlideClip);

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

            // ---- NPCs & Dialogue ----
            // Resh NPC: positioned at z~36 in the service corridor.
            var reshPos = new Vector3(0f, 1f, 36f);
            var reshGo = InstantiateNpc(ReshPrefabPath, AtFloor(reshPos), "Resh");
            if (reshGo != null)
            {
                var reshNpc = reshGo.AddComponent<StoryNpc>();
                var reshSo = new SerializedObject(reshNpc);
                reshSo.FindProperty("displayName").stringValue = "Captain Resh";
                reshSo.FindProperty("remote").boolValue = false;
                reshSo.ApplyModifiedPropertiesWithoutUndo();

                var wander = reshGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = 2f;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var dockApproachDialogue = BuildEp02DialoguePlayer("Dialogue_DockApproach", reshPos, "dock_approach");
            var dockKesslerDialogue = BuildEp02DialoguePlayer("Dialogue_DockKessler", reshPos, "dock_kessler", talkRef);
            var dockChallengeDialogue = BuildEp02DialoguePlayer("Dialogue_DockChallenge", new Vector3(0f, 1.5f, 25f), "dock_challenge", talkRef);
            var dockFightBarksDialogue = BuildEp02DialoguePlayer("Dialogue_DockFightBarks", new Vector3(0f, 1.5f, 25f), "dock_fight_barks");
            var dockAftermathDialogue = BuildEp02DialoguePlayer("Dialogue_DockAftermath", reshPos, "dock_aftermath", talkRef);
            var dockReshDialogue = BuildEp02DialoguePlayer("Dialogue_DockResh", reshPos, "dock_resh", talkRef);

            // All dialogue players: playOnStart=false (default). MissionDirector or EnemyWaveSpawner calls .Play().

            // ---- Enemies: 3 gold-tinted Saffron Veil enforcers at checkpoint (z ~23-27). ----
            var veilGold = new Color(0.85f, 0.7f, 0.3f);
            var checkpointEnemyPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 23f),
                new Vector3(0f, 0f, 25f),
                new Vector3(1.5f, 0f, 27f)
            };
            var checkpointEnemyHealths = new List<Health>();
            foreach (var pos in checkpointEnemyPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                // Tint the enemy gold for Saffron Veil.
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, veilGold);
                enemy.gameObject.SetActive(false);
                checkpointEnemyHealths.Add(enemy.GetComponent<Health>());
            }

            // Wave spawner for the checkpoint fight (single wave, 3 enforcers).
            var dockingWaveSpawnerGo = new GameObject("DockingWaveSpawner");
            var dockingWaveSpawner = dockingWaveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var dockingTriggerPointGo = new GameObject("TriggerPoint");
            dockingTriggerPointGo.transform.position = new Vector3(0f, 1f, 22f);

            var dockingWaveSo = new SerializedObject(dockingWaveSpawner);
            SetObjectRef(dockingWaveSo, "triggerPoint", dockingTriggerPointGo.transform);
            dockingWaveSo.FindProperty("triggerRadius").floatValue = 3f;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                dockingWaveSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            var dockingWavesProp = dockingWaveSo.FindProperty("waves");
            dockingWavesProp.arraySize = 1;
            var dockingWave0 = dockingWavesProp.GetArrayElementAtIndex(0);
            var dockingWave0Enemies = dockingWave0.FindPropertyRelative("enemies");
            dockingWave0Enemies.arraySize = checkpointEnemyHealths.Count;
            for (int i = 0; i < checkpointEnemyHealths.Count; i++)
                dockingWave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = checkpointEnemyHealths[i];
            dockingWave0.FindPropertyRelative("bark").objectReferenceValue = dockFightBarksDialogue;

            var dockingWaveAudio = dockingWaveSpawnerGo.AddComponent<AudioSource>();
            dockingWaveAudio.spatialBlend = 0f;
            dockingWaveAudio.playOnAwake = false;
            SetObjectRef(dockingWaveSo, "audioSource", dockingWaveAudio);

            dockingWaveSo.ApplyModifiedPropertiesWithoutUndo();

            // Reach triggers.
            var corridorReachGo = new GameObject("CorridorReachPoint");
            corridorReachGo.transform.position = new Vector3(0f, 1f, 14f);
            var serviceReachGo = new GameObject("ServiceReachPoint");
            serviceReachGo.transform.position = new Vector3(0f, 1f, 34f);

            // Transition box: "FOLLOW RESH — SERVICE TUNNELS".
            var transitionBoxGo = BuildTransitionBox("FollowReshBox", new Vector3(0f, 1.2f, 37f), "FOLLOW RESH — SERVICE TUNNELS",
                out var followBtn, out var followTransition);
            var stSo = new SerializedObject(followTransition);
            stSo.FindProperty("onFootScene").stringValue = Galaxy1Ep02PensSceneName;
            stSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(followBtn.onClick,
                new UnityEngine.Events.UnityAction(followTransition.LoadOnFootScene));
            transitionBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 11;

            // Step 0: Dialogue dock_approach (auto-play comms).
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("label").stringValue = "Dialogue: Docking Approach";
            step0.FindPropertyRelative("dialogue").objectReferenceValue = dockApproachDialogue;

            // Step 1: Dialogue dock_kessler (Kessler brief).
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step1.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Brief";
            step1.FindPropertyRelative("dialogue").objectReferenceValue = dockKesslerDialogue;

            // Step 2: Trigger — open airlock door.
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step2.FindPropertyRelative("label").stringValue = "Trigger: Open Airlock Door";
            var t2 = step2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = airlockDoor;

            // Step 3: ReachTrigger — corridor.
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step3.FindPropertyRelative("label").stringValue = "ReachTrigger: Docking Corridor";
            step3.FindPropertyRelative("reachPoint").objectReferenceValue = corridorReachGo.transform;
            step3.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 4: Dialogue dock_challenge (Veil challenge).
            var step4 = stepsProp.GetArrayElementAtIndex(4);
            step4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step4.FindPropertyRelative("label").stringValue = "Dialogue: Veil Challenge";
            step4.FindPropertyRelative("dialogue").objectReferenceValue = dockChallengeDialogue;

            // Step 5: DefeatWaves — checkpoint (1 wave, 3 enforcers).
            var step5 = stepsProp.GetArrayElementAtIndex(5);
            step5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            step5.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Saffron Veil Enforcers";
            step5.FindPropertyRelative("waveSpawner").objectReferenceValue = dockingWaveSpawner;

            // Step 6: Dialogue dock_aftermath.
            var step6 = stepsProp.GetArrayElementAtIndex(6);
            step6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step6.FindPropertyRelative("label").stringValue = "Dialogue: Aftermath";
            step6.FindPropertyRelative("dialogue").objectReferenceValue = dockAftermathDialogue;

            // Step 7: Trigger — open service door.
            var step7 = stepsProp.GetArrayElementAtIndex(7);
            step7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step7.FindPropertyRelative("label").stringValue = "Trigger: Open Service Door";
            var t7 = step7.FindPropertyRelative("triggerObjects");
            t7.arraySize = 1;
            t7.GetArrayElementAtIndex(0).objectReferenceValue = serviceDoor;

            // Step 8: ReachTrigger — service corridor.
            var step8 = stepsProp.GetArrayElementAtIndex(8);
            step8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step8.FindPropertyRelative("label").stringValue = "ReachTrigger: Service Corridor";
            step8.FindPropertyRelative("reachPoint").objectReferenceValue = serviceReachGo.transform;
            step8.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 9: Dialogue dock_resh (Resh encounter).
            var step9 = stepsProp.GetArrayElementAtIndex(9);
            step9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step9.FindPropertyRelative("label").stringValue = "Dialogue: Resh Encounter";
            step9.FindPropertyRelative("dialogue").objectReferenceValue = dockReshDialogue;

            // Step 10: Prompt — transition box.
            var step10 = stepsProp.GetArrayElementAtIndex(10);
            step10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step10.FindPropertyRelative("label").stringValue = "Prompt: Follow Resh to Pens";
            step10.FindPropertyRelative("promptObject").objectReferenceValue = transitionBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep02DockingScenePath);
            EnsureScenesInBuild(Galaxy1Ep02DockingScenePath);

            Debug.Log($"[Space Samurai] EP02 Docking scene built at {Galaxy1Ep02DockingScenePath}. " +
                      "Layout: airlock → docking collar (cargo) → checkpoint (3 Veil enforcers) → service corridor (Resh). " +
                      "11 steps: dock_approach auto-play → dock_kessler → open airlock → reach corridor → dock_challenge → defeat 3 enforcers + barks → " +
                      "dock_aftermath → open service door → reach service → dock_resh dialogue → transition box to Pens. " +
                      "Resh wanders service corridor. Enemy wave has SFX sting.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP02 Holding Pens", priority = 69)]
        public static void BuildEp02Pens()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cold security facility lighting: blue-ish, tension-filled.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.8f, 0.95f);
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.13f, 0.17f);

            // Cold accents for the pen facility.
            BuildAccentPointLight("LockLight", new Vector3(0f, 2.6f, 16f),
                new Color(0.5f, 0.8f, 1f), intensity: 1.6f, range: 12f);
            BuildAccentPointLight("PenLight", new Vector3(0f, 2.6f, 30f),
                new Color(0.45f, 0.75f, 1f), intensity: 1.5f, range: 14f);
            BuildAccentPointLight("GalleryLight", new Vector3(0f, 2.6f, 52f),
                new Color(0.4f, 0.7f, 0.95f), intensity: 1.8f, range: 16f);

            // ---- Pens geometry: service tunnel (z 0-12) -> lock station (z 12-22) ->
            // pen corridor (z 22-40) -> auction gallery (z 40-62).
            var interiorGo = new GameObject("PensInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.19f, 0.21f, 0.25f);
            var ceilColor = new Color(0.11f, 0.12f, 0.15f);

            // Service tunnel: x[-2.5,2.5], z[0,12] (descending, cold blue).
            BuildFloorCeiling(interior, "ServiceTunnel", new Vector3(0f, 0f, 6f), new Vector3(5f, 0f, 12f), floorColor, ceilColor);
            BuildCorridorWall(interior, "ServiceTunnel_WallW", -2.5f, 0f, 12f, new float[0], 2.4f);
            BuildCorridorWall(interior, "ServiceTunnel_WallE", 2.5f, 0f, 12f, new float[0], 2.4f);

            // Lock station: x[-3,3], z[12,22] (security checkpoint, 4 guard spawns).
            BuildFloorCeiling(interior, "LockStation", new Vector3(0f, 0f, 17f), new Vector3(6f, 0f, 10f), floorColor, ceilColor);
            BuildWall(interior, "LockStation_WallW", new Vector3(-3f, 1.5f, 17f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "LockStation_WallE", new Vector3(3f, 1.5f, 17f), new Vector3(0.2f, 3f, 10f));

            // Pen corridor: x[-4,4], z[22,40] (holds cells on both sides, 6-8 children inside, Iris at z~30).
            BuildFloorCeiling(interior, "Pen", new Vector3(0f, 0f, 31f), new Vector3(8f, 0f, 18f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Pen_WallW", -4f, 22f, 40f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Pen_WallE", 4f, 22f, 40f, new float[0], 2.4f);

            // Blast door separating pens from gallery (locked until after Resh plan dialogue).
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            BuildDoorwayWall(interior, "Pen_WallBack", new Vector3(0f, 1.5f, 40f), 8f, true, 2.4f);
            var blastDoor = BuildSlidingDoor(interior, "BlastDoor", new Vector3(0f, 0f, 40f), 2.4f, true, startLocked: true);
            WireDoorAudio(blastDoor, doorSlideClip);

            // Gallery: x[-6,6], z[40,62] (catwalk aesthetic, wide room for auction floor).
            BuildFloorCeiling(interior, "Gallery", new Vector3(0f, 0f, 51f), new Vector3(12f, 0f, 22f), floorColor, ceilColor);
            BuildWall(interior, "Gallery_WallW", new Vector3(-6f, 1.5f, 51f), new Vector3(0.2f, 3f, 22f));
            BuildWall(interior, "Gallery_WallE", new Vector3(6f, 1.5f, 51f), new Vector3(0.2f, 3f, 22f));
            BuildWall(interior, "Gallery_WallBack", new Vector3(0f, 1.5f, 62f), new Vector3(12f, 3f, 0.2f));

            // Glass-tinted cell boxes lining the pen corridor (visual only, no collision).
            var glassColor = new Color(0.3f, 0.5f, 0.6f, 0.4f);
            BuildProp(interior, "CellW1", new Vector3(-4.5f, 0.8f, 25f), new Vector3(0.4f, 1.2f, 2f), glassColor);
            BuildProp(interior, "CellE1", new Vector3(4.5f, 0.8f, 25f), new Vector3(0.4f, 1.2f, 2f), glassColor);
            BuildProp(interior, "CellW2", new Vector3(-4.5f, 0.8f, 33f), new Vector3(0.4f, 1.2f, 2f), glassColor);
            BuildProp(interior, "CellE2", new Vector3(4.5f, 0.8f, 33f), new Vector3(0.4f, 1.2f, 2f), glassColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 65f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Resh: positioned at lock station (z ~17).
            var reshLockPos = new Vector3(0f, 1f, 17f);
            var reshLockGo = InstantiateNpc(ReshPrefabPath, AtFloor(reshLockPos), "Resh_Lock");
            if (reshLockGo != null)
            {
                var reshNpc = reshLockGo.AddComponent<StoryNpc>();
                var reshSo = new SerializedObject(reshNpc);
                reshSo.FindProperty("displayName").stringValue = "Captain Resh";
                reshSo.FindProperty("remote").boolValue = false;
                reshSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Iris: small-scale child at a cell (z ~30), scale ~0.7.
            var irisPos = new Vector3(0f, 0.7f, 30f);
            var irisGo = InstantiateNpc(IrisPrefabPath, AtFloor(irisPos), "Iris");
            if (irisGo != null)
            {
                irisGo.transform.localScale = Vector3.one * 0.7f;
                var irisNpc = irisGo.AddComponent<StoryNpc>();
                var irisSo = new SerializedObject(irisNpc);
                irisSo.FindProperty("displayName").stringValue = "Iris";
                irisSo.FindProperty("remote").boolValue = false;
                irisSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Children: 6 child NPCs scattered in the pens (z 23-38, scale ~0.65), with StoryNpcWander paths.
            var childPositions = new Vector3[]
            {
                new Vector3(-2f, 0.65f, 24f),
                new Vector3(2f, 0.65f, 26f),
                new Vector3(-3f, 0.65f, 28f),
                new Vector3(1f, 0.65f, 32f),
                new Vector3(-1f, 0.65f, 35f),
                new Vector3(3f, 0.65f, 37f),
            };

            var childParent = new GameObject("Children");
            childParent.transform.SetParent(interior, false);
            var childGos = new List<GameObject>();

            foreach (int i in System.Linq.Enumerable.Range(0, childPositions.Length))
            {
                var childGo = InstantiateNpc(ChildPrefabPath, AtFloor(childPositions[i]), $"Child_{i}");
                childGo.transform.SetParent(childParent.transform, false);
                childGo.transform.localScale = Vector3.one * 0.65f;
                childGos.Add(childGo);
            }
            childParent.SetActive(false); // Activated by Trigger step after Iris dialogue

            // ---- Dialogue ----
            var pensChallengeDialogue = BuildEp02DialoguePlayer("Dialogue_PensChallenge", reshLockPos, "pens_challenge", talkRef);
            var pensIrisDialogue = BuildEp02DialoguePlayer("Dialogue_PensIris", irisPos, "pens_iris", talkRef);
            var pensAlarmDialogue = BuildEp02DialoguePlayer("Dialogue_PensAlarm", reshLockPos, "pens_alarm", talkRef);
            var pensReshPlanDialogue = BuildEp02DialoguePlayer("Dialogue_PensReshPlan", reshLockPos, "pens_resh_plan", talkRef);
            var pensKhallDialogue = BuildEp02DialoguePlayer("Dialogue_PensKhall", new Vector3(0f, 2.2f, 35f), "pens_khall");
            var pensGalleryBarksDialogue = BuildEp02DialoguePlayer("Dialogue_GalleryBarks", new Vector3(0f, 1.5f, 52f), "gallery_barks");

            // All barks: playOnStart=false, active (EnemyWaveSpawner calls .Play()).
            // pens_alarm will be activated by Trigger step as a dialogue, so playOnStart=false is correct.

            // ---- Enemies ----
            var veilGold = new Color(0.85f, 0.7f, 0.3f);
            var mawDarkRed = new Color(0.6f, 0.2f, 0.15f);

            // Wave 1: 4 gold Veil guards at lock station (z 12-22).
            var lockStationPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 14f),
                new Vector3(1.5f, 0f, 16f),
                new Vector3(-1f, 0f, 20f),
                new Vector3(1f, 0f, 22f)
            };
            var lockStationHealths = new List<Health>();
            foreach (var pos in lockStationPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, veilGold);
                enemy.gameObject.SetActive(false);
                lockStationHealths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 6 gold Veil guards in pen corridor (z 24-38).
            var penCorridorPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 24f),
                new Vector3(2f, 0f, 26f),
                new Vector3(-2.5f, 0f, 30f),
                new Vector3(2.5f, 0f, 32f),
                new Vector3(-1.5f, 0f, 36f),
                new Vector3(1.5f, 0f, 38f)
            };
            var penCorridorHealths = new List<Health>();
            foreach (var pos in penCorridorPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, veilGold);
                enemy.gameObject.SetActive(false);
                penCorridorHealths.Add(enemy.GetComponent<Health>());
            }

            // Wave 3: 6 Veil escorts + 2 dark-red Gilded Maw handlers in gallery (z 44-60).
            var galleryPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 44f),   // Veil
                new Vector3(3f, 0f, 46f),   // Veil
                new Vector3(-2f, 0f, 50f),  // Veil
                new Vector3(2f, 0f, 50f),   // Veil
                new Vector3(-1f, 0f, 54f),  // Veil
                new Vector3(1f, 0f, 54f),   // Veil
                new Vector3(-4f, 0f, 56f),  // Maw Handler
                new Vector3(4f, 0f, 56f)    // Maw Handler
            };
            var galleryHealths = new List<Health>();
            for (int i = 0; i < galleryPositions.Length; i++)
            {
                var enemy = BuildDominionEnemy(galleryPositions[i], playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // First 6 are gold Veil, last 2 are dark-red Maw.
                    TintShared(renderer, i < 6 ? veilGold : mawDarkRed);
                }
                enemy.gameObject.SetActive(false);
                galleryHealths.Add(enemy.GetComponent<Health>());
            }

            // Three wave spawners (one per wave).
            var lockWaveSpawnerGo = new GameObject("LockWaveSpawner");
            var lockWaveSpawner = lockWaveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var lockTriggerGo = new GameObject("LockTrigger");
            lockTriggerGo.transform.position = new Vector3(0f, 1f, 12f);

            var lockWaveSo = new SerializedObject(lockWaveSpawner);
            SetObjectRef(lockWaveSo, "triggerPoint", lockTriggerGo.transform);
            lockWaveSo.FindProperty("triggerRadius").floatValue = 3f;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                lockWaveSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            var lockWavesProp = lockWaveSo.FindProperty("waves");
            lockWavesProp.arraySize = 1;
            var lockWave0 = lockWavesProp.GetArrayElementAtIndex(0);
            var lockWave0Enemies = lockWave0.FindPropertyRelative("enemies");
            lockWave0Enemies.arraySize = lockStationHealths.Count;
            for (int i = 0; i < lockStationHealths.Count; i++)
                lockWave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = lockStationHealths[i];

            var lockFightBarksDialogue = BuildEp02DialoguePlayer("Dialogue_PensFightBarks", reshLockPos, "pens_fight_barks");
            lockWave0.FindPropertyRelative("bark").objectReferenceValue = lockFightBarksDialogue;

            var lockWaveAudio = lockWaveSpawnerGo.AddComponent<AudioSource>();
            lockWaveAudio.spatialBlend = 0f;
            lockWaveAudio.playOnAwake = false;
            SetObjectRef(lockWaveSo, "audioSource", lockWaveAudio);
            lockWaveSo.ApplyModifiedPropertiesWithoutUndo();

            // Pen corridor wave spawner.
            var penWaveSpawnerGo = new GameObject("PenWaveSpawner");
            var penWaveSpawner = penWaveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var penTriggerGo = new GameObject("PenTrigger");
            penTriggerGo.transform.position = new Vector3(0f, 1f, 28f);

            var penWaveSo = new SerializedObject(penWaveSpawner);
            SetObjectRef(penWaveSo, "triggerPoint", penTriggerGo.transform);
            penWaveSo.FindProperty("triggerRadius").floatValue = 3f;
            if (waveSting != null)
                penWaveSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            var penWavesProp = penWaveSo.FindProperty("waves");
            penWavesProp.arraySize = 1;
            var penWave0 = penWavesProp.GetArrayElementAtIndex(0);
            var penWave0Enemies = penWave0.FindPropertyRelative("enemies");
            penWave0Enemies.arraySize = penCorridorHealths.Count;
            for (int i = 0; i < penCorridorHealths.Count; i++)
                penWave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = penCorridorHealths[i];

            var penFightBarksDialogue = BuildEp02DialoguePlayer("Dialogue_PensFight2Barks", new Vector3(0f, 1.5f, 30f), "pens_fight2_barks");
            penWave0.FindPropertyRelative("bark").objectReferenceValue = penFightBarksDialogue;

            var penWaveAudio = penWaveSpawnerGo.AddComponent<AudioSource>();
            penWaveAudio.spatialBlend = 0f;
            penWaveAudio.playOnAwake = false;
            SetObjectRef(penWaveSo, "audioSource", penWaveAudio);
            penWaveSo.ApplyModifiedPropertiesWithoutUndo();

            // Gallery wave spawner.
            var galleryWaveSpawnerGo = new GameObject("GalleryWaveSpawner");
            var galleryWaveSpawner = galleryWaveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var galleryTriggerGo = new GameObject("GalleryTrigger");
            galleryTriggerGo.transform.position = new Vector3(0f, 1f, 44f);

            var galleryWaveSo = new SerializedObject(galleryWaveSpawner);
            SetObjectRef(galleryWaveSo, "triggerPoint", galleryTriggerGo.transform);
            galleryWaveSo.FindProperty("triggerRadius").floatValue = 4f;
            if (waveSting != null)
                galleryWaveSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            var galleryWavesProp = galleryWaveSo.FindProperty("waves");
            galleryWavesProp.arraySize = 1;
            var galleryWave0 = galleryWavesProp.GetArrayElementAtIndex(0);
            var galleryWave0Enemies = galleryWave0.FindPropertyRelative("enemies");
            galleryWave0Enemies.arraySize = galleryHealths.Count;
            for (int i = 0; i < galleryHealths.Count; i++)
                galleryWave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = galleryHealths[i];
            galleryWave0.FindPropertyRelative("bark").objectReferenceValue = pensGalleryBarksDialogue;

            var galleryWaveAudio = galleryWaveSpawnerGo.AddComponent<AudioSource>();
            galleryWaveAudio.spatialBlend = 0f;
            galleryWaveAudio.playOnAwake = false;
            SetObjectRef(galleryWaveSo, "audioSource", galleryWaveAudio);
            galleryWaveSo.ApplyModifiedPropertiesWithoutUndo();

            // Reach trigger for gallery.
            var galleryReachGo = new GameObject("GalleryReachPoint");
            galleryReachGo.transform.position = new Vector3(0f, 1f, 48f);

            // Transition box: "DESCEND TO THE CORE".
            var coreBoxGo = BuildTransitionBox("DescendToCoreBox", new Vector3(0f, 1.2f, 60f), "DESCEND TO THE CORE",
                out var coreBtn, out var coreTransition);
            var coreSo = new SerializedObject(coreTransition);
            coreSo.FindProperty("onFootScene").stringValue = Galaxy1Ep02CoreSceneName;
            coreSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(coreBtn.onClick,
                new UnityEngine.Events.UnityAction(coreTransition.LoadOnFootScene));
            coreBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 13;

            // Step 0: Dialogue pens_challenge.
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Pens Challenge";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = pensChallengeDialogue;

            // Step 1: DefeatWaves — lock station (1 wave, 4 guards).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Lock Station Guards";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = lockWaveSpawner;

            // Step 2: Dialogue pens_iris.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Iris Encounter";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = pensIrisDialogue;

            // Step 3: Trigger — activate children walkers (they walk back toward exit).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s3.FindPropertyRelative("label").stringValue = "Trigger: Children Evacuate";
            var t3 = s3.FindPropertyRelative("triggerObjects");
            t3.arraySize = 1;
            t3.GetArrayElementAtIndex(0).objectReferenceValue = childParent;

            // Step 4: Dialogue pens_alarm.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Alarm";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = pensAlarmDialogue;

            // Step 5: DefeatWaves — pen corridor (1 wave, 6 guards).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s5.FindPropertyRelative("label").stringValue = "DefeatWaves: 6 Pen Corridor Guards";
            s5.FindPropertyRelative("waveSpawner").objectReferenceValue = penWaveSpawner;

            // Step 6: Dialogue pens_khall (speakers-only, no body).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Khall (Speakers)";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = pensKhallDialogue;

            // Step 7: Dialogue pens_resh_plan.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Resh Plan";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = pensReshPlanDialogue;

            // Step 8: Trigger — unlock blast door to gallery.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s8.FindPropertyRelative("label").stringValue = "Trigger: Open Blast Door";
            var t8 = s8.FindPropertyRelative("triggerObjects");
            t8.arraySize = 1;
            t8.GetArrayElementAtIndex(0).objectReferenceValue = blastDoor;

            // Step 9: ReachTrigger — gallery.
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s9.FindPropertyRelative("label").stringValue = "ReachTrigger: Auction Gallery";
            s9.FindPropertyRelative("reachPoint").objectReferenceValue = galleryReachGo.transform;
            s9.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 10: DefeatWaves — gallery (1 wave, 6 Veil + 2 Maw).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s10.FindPropertyRelative("label").stringValue = "DefeatWaves: 6 Veil + 2 Maw (Gallery)";
            s10.FindPropertyRelative("waveSpawner").objectReferenceValue = galleryWaveSpawner;

            // Step 11: Dialogue gallery_khall.
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s11.FindPropertyRelative("label").stringValue = "Dialogue: Khall (Gallery)";
            var galleryKhallDialogue = BuildEp02DialoguePlayer("Dialogue_GalleryKhall", new Vector3(0f, 2.2f, 52f), "gallery_khall");
            s11.FindPropertyRelative("dialogue").objectReferenceValue = galleryKhallDialogue;

            // Step 12: Prompt — transition box to Core.
            var s12 = stepsProp.GetArrayElementAtIndex(12);
            s12.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s12.FindPropertyRelative("label").stringValue = "Prompt: Descend to Core";
            s12.FindPropertyRelative("promptObject").objectReferenceValue = coreBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep02PensScenePath);
            EnsureScenesInBuild(Galaxy1Ep02PensScenePath);

            Debug.Log($"[Space Samurai] EP02 Pens scene built at {Galaxy1Ep02PensScenePath}. " +
                      "Layout: service tunnel → lock station (4 guards) → pen corridor (6 guards, Iris, 6-8 children) → blast door → auction gallery (6 Veil + 2 Maw). " +
                      "13 steps: pens_challenge → defeat 4 guards + barks → pens_iris dialogue → activate children evacuation → pens_alarm → " +
                      "defeat 6 pen guards + barks → pens_khall (speakers) → pens_resh_plan → open blast door → reach gallery → defeat 8 gallery enemies + barks → " +
                      "gallery_khall dialogue → transition box to Core. Three EnemyWaveSpawners (one per fight), one for each with SFX sting. " +
                      "Resh at lock station. Iris (scale 0.7) + children (scale 0.65) in pens.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP02 Station Core", priority = 70)]
        public static void BuildEp02Core()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Core lighting: cool directional key, tense blue-red ambiance with alarm red accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.82f, 0.95f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.16f);

            // Core chamber accent: cool cyan light around mainframe.
            BuildAccentPointLight("MainframeLight", new Vector3(0f, 2.6f, 11f),
                new Color(0.5f, 0.8f, 1f), intensity: 2.0f, range: 16f);
            // Concourse accent: cool-neutral mid-space lighting.
            BuildAccentPointLight("ConcourseLight", new Vector3(0f, 2.6f, 33f),
                new Color(0.6f, 0.78f, 1f), intensity: 1.6f, range: 14f);
            // Airlock accent: final cool approach.
            BuildAccentPointLight("AirlockLight", new Vector3(0f, 2.6f, 48f),
                new Color(0.55f, 0.75f, 0.95f), intensity: 1.5f, range: 12f);

            // ---- Core interior: core chamber (z 0-22) -> concourse corridor (z 22-44) -> airlock (z 44-52). ----
            var interiorGo = new GameObject("CoreInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.26f);
            var ceilColor = new Color(0.12f, 0.13f, 0.16f);

            // Core chamber: x[-6,6], z[0,22], holds mainframe + Khall + escape pod.
            BuildFloorCeiling(interior, "CoreChamber", new Vector3(0f, 0f, 11f), new Vector3(12f, 0f, 22f), floorColor, ceilColor);
            BuildWall(interior, "CoreChamber_WallW", new Vector3(-6f, 1.5f, 11f), new Vector3(0.2f, 3f, 22f));
            BuildWall(interior, "CoreChamber_WallE", new Vector3(6f, 1.5f, 11f), new Vector3(0.2f, 3f, 22f));
            BuildWall(interior, "CoreChamber_WallFront", new Vector3(0f, 1.5f, -1f), new Vector3(12f, 3f, 0.2f));
            BuildDoorwayWall(interior, "CoreChamber_WallBack", new Vector3(0f, 1.5f, 23f), 12f, true, 2.4f);

            // Concourse corridor: x[-4,4], z[22,44], wide mid-section (can activate red alarm lights here).
            BuildFloorCeiling(interior, "Concourse", new Vector3(0f, 0f, 33f), new Vector3(8f, 0f, 22f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Concourse_WallW", -4f, 22f, 44f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Concourse_WallE", 4f, 22f, 44f, new float[0], 2.4f);

            // Airlock room: x[-3,3], z[44,52], final passage to escape.
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 48f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-3f, 1.5f, 48f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Airlock_WallE", new Vector3(3f, 1.5f, 48f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 52f), new Vector3(6f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, 44f), 6f, true, 2.4f);

            // Mainframe prop: tall dark box stack (visual centerpiece of core chamber).
            var mainframeBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainframeBox.name = "Mainframe";
            mainframeBox.transform.SetParent(interior, false);
            mainframeBox.transform.localPosition = new Vector3(0f, 0.8f, 12f); // moved deeper (was z=8) so Khall at z=8 stands clearly in front, not buried inside
            mainframeBox.transform.localScale = new Vector3(2f, 2.2f, 1.2f);
            TintShared(mainframeBox.GetComponent<Renderer>(), new Color(0.1f, 0.12f, 0.16f));

            // Escape pod prop: capsule near the chamber wall (z ~20).
            var podBox = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            podBox.name = "EscapePod";
            podBox.transform.SetParent(interior, false);
            podBox.transform.localPosition = new Vector3(-4.5f, 0.8f, 18f);
            podBox.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            TintShared(podBox.GetComponent<Renderer>(), new Color(0.3f, 0.25f, 0.2f));

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
            // Khall NPC: positioned at the mainframe (z ~8).
            var khallStandingGo = new GameObject("KhallStanding");
            khallStandingGo.transform.SetParent(interior, false);
            khallStandingGo.transform.localPosition = new Vector3(0f, 0f, 8f); // feet-pivot prefab stands on the floor

            var khallPos = new Vector3(0f, 0f, 0f); // local offset from parent
            var khallGo = InstantiateNpc(KhallPrefabPath, khallPos, "Khall");
            if (khallGo != null)
            {
                khallGo.transform.SetParent(khallStandingGo.transform, false);
                var khallNpc = khallGo.AddComponent<StoryNpc>();
                var khallSo = new SerializedObject(khallNpc);
                khallSo.FindProperty("displayName").stringValue = "Khall";
                khallSo.FindProperty("remote").boolValue = false;
                khallSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Resh is not physically present in Core — his evac coordination plays as comms via the
            // positional Dialogue_CoreEscape player below. (Physical Resh_Core NPC removed.)

            // ---- Evacuation Timer ----
            var timerGo = new GameObject("Evacuation Timer");
            timerGo.transform.SetParent(interior, false);
            timerGo.transform.localPosition = new Vector3(0f, 2.2f, 25f);
            var evacuationTimer = timerGo.AddComponent<EvacuationTimer>();
            var timerSo = new SerializedObject(evacuationTimer);
            timerSo.FindProperty("duration").floatValue = 90f;
            // Create a TextMesh at the position and wire it.
            var timerText = new GameObject("TextMesh");
            timerText.transform.SetParent(timerGo.transform, false);
            timerText.transform.localPosition = Vector3.zero;
            var textMesh = timerText.AddComponent<TextMesh>();
            textMesh.text = "PURGE IN 01:30";
            textMesh.fontSize = 40;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            SetObjectRef(timerSo, "textMesh", textMesh);
            timerSo.ApplyModifiedPropertiesWithoutUndo();
            timerGo.SetActive(false); // Activated by Trigger step.

            // ---- Alarm Lights (red accent) ----
            var alarmLightsGo = new GameObject("AlarmLights");

            var alarmLight1Go = new GameObject("AlarmLight1");
            alarmLight1Go.transform.SetParent(alarmLightsGo.transform, false);
            alarmLight1Go.transform.position = new Vector3(-3f, 2.4f, 30f);
            var alarmLight1 = alarmLight1Go.AddComponent<Light>();
            alarmLight1.type = LightType.Point;
            alarmLight1.color = new Color(1f, 0.3f, 0.2f);
            alarmLight1.intensity = 2.0f;
            alarmLight1.range = 8f;
            alarmLight1.shadows = LightShadows.None;

            var alarmLight2Go = new GameObject("AlarmLight2");
            alarmLight2Go.transform.SetParent(alarmLightsGo.transform, false);
            alarmLight2Go.transform.position = new Vector3(3f, 2.4f, 35f);
            var alarmLight2 = alarmLight2Go.AddComponent<Light>();
            alarmLight2.type = LightType.Point;
            alarmLight2.color = new Color(1f, 0.3f, 0.2f);
            alarmLight2.intensity = 2.0f;
            alarmLight2.range = 8f;
            alarmLight2.shadows = LightShadows.None;

            alarmLightsGo.SetActive(false); // Activated by Trigger step.

            // ---- Khall Escape Pod (appears when triggered) ----
            var khallEscapeGo = new GameObject("KhallEscape");
            khallEscapeGo.transform.SetParent(interior, false);
            khallEscapeGo.transform.localPosition = new Vector3(-5.5f, 1.2f, 18f);
            var escapePodVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            escapePodVisual.name = "PodLaunched";
            escapePodVisual.transform.SetParent(khallEscapeGo.transform, false);
            escapePodVisual.transform.localPosition = Vector3.zero;
            escapePodVisual.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            TintShared(escapePodVisual.GetComponent<Renderer>(), new Color(1f, 0.4f, 0.1f));
            khallEscapeGo.SetActive(false); // Activated by Trigger step.

            // ---- Dialogue Players ----
            var coreKhallDialogue = BuildEp02DialoguePlayer("Dialogue_CoreKhall", new Vector3(0f, 1.5f, 8f), "core_khall");
            var coreFightBarksDialogue = BuildEp02DialoguePlayer("Dialogue_CoreFightBarks", new Vector3(0f, 1.5f, 8f), "core_fight_barks");
            var coreEscapeDialogue = BuildEp02DialoguePlayer("Dialogue_CoreEscape", new Vector3(0f, 1.5f, 48f), "core_escape", talkRef);

            // ---- Enemies: 8 dark-red elite enforcers in the core chamber ----
            var eliteRed = new Color(0.7f, 0.2f, 0.15f);
            var coreEnemyPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 4f),
                new Vector3(3f, 0f, 4f),
                new Vector3(-2.5f, 0f, 8f),
                new Vector3(2.5f, 0f, 8f),
                new Vector3(-3.5f, 0f, 12f),
                new Vector3(3.5f, 0f, 12f),
                new Vector3(-2f, 0f, 16f),
                new Vector3(2f, 0f, 16f)
            };
            var coreEnemyHealths = new List<Health>();
            foreach (var pos in coreEnemyPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteRed);
                enemy.gameObject.SetActive(false);
                coreEnemyHealths.Add(enemy.GetComponent<Health>());
            }

            // Wave spawner for the core fight (single wave, 8 enforcers).
            var coreWaveSpawnerGo = new GameObject("CoreWaveSpawner");
            var coreWaveSpawner = coreWaveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var coreTriggerPointGo = new GameObject("TriggerPoint");
            coreTriggerPointGo.transform.position = new Vector3(0f, 1f, 8f);

            var coreWaveSo = new SerializedObject(coreWaveSpawner);
            SetObjectRef(coreWaveSo, "triggerPoint", coreTriggerPointGo.transform);
            coreWaveSo.FindProperty("triggerRadius").floatValue = 4f;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                coreWaveSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            var coreWavesProp = coreWaveSo.FindProperty("waves");
            coreWavesProp.arraySize = 1;
            var coreWave0 = coreWavesProp.GetArrayElementAtIndex(0);
            var coreWave0Enemies = coreWave0.FindPropertyRelative("enemies");
            coreWave0Enemies.arraySize = coreEnemyHealths.Count;
            for (int i = 0; i < coreEnemyHealths.Count; i++)
                coreWave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = coreEnemyHealths[i];
            coreWave0.FindPropertyRelative("bark").objectReferenceValue = coreFightBarksDialogue;

            var coreWaveAudio = coreWaveSpawnerGo.AddComponent<AudioSource>();
            coreWaveAudio.spatialBlend = 0f;
            coreWaveAudio.playOnAwake = false;
            SetObjectRef(coreWaveSo, "audioSource", coreWaveAudio);
            coreWaveSo.ApplyModifiedPropertiesWithoutUndo();

            // Reach triggers.
            var concourseReachGo = new GameObject("ConcourseReachPoint");
            concourseReachGo.transform.position = new Vector3(0f, 1f, 42f);

            // Transition box: "BOARD THE HAULER".
            var boardBoxGo = BuildTransitionBox("BoardHaulerBox", new Vector3(0f, 1.2f, 50f), "BOARD THE HAULER",
                out var boardBtn, out var boardTransition);
            var boardSo = new SerializedObject(boardTransition);
            boardSo.FindProperty("onFootScene").stringValue = Galaxy1Ep02SafeHouseSceneName;
            boardSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(boardBtn.onClick,
                new UnityEngine.Events.UnityAction(boardTransition.LoadOnFootScene));
            boardBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue core_khall (Khall's monologue at mainframe).
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("label").stringValue = "Dialogue: Khall's Ultimatum";
            step0.FindPropertyRelative("dialogue").objectReferenceValue = coreKhallDialogue;

            // Step 1: Trigger — activate AlarmLights, EvacuationTimer, and KhallEscape pod.
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step1.FindPropertyRelative("label").stringValue = "Trigger: Alarm Sequence";
            var t1 = step1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 3;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;
            t1.GetArrayElementAtIndex(1).objectReferenceValue = timerGo;
            t1.GetArrayElementAtIndex(2).objectReferenceValue = khallEscapeGo;

            // Step 2: DefeatWaves — 8 elite enforcers in core chamber.
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            step2.FindPropertyRelative("label").stringValue = "DefeatWaves: 8 Elite Enforcers";
            step2.FindPropertyRelative("waveSpawner").objectReferenceValue = coreWaveSpawner;

            // Step 3: ReachTrigger — end of concourse (z ~42).
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step3.FindPropertyRelative("label").stringValue = "ReachTrigger: Concourse End";
            step3.FindPropertyRelative("reachPoint").objectReferenceValue = concourseReachGo.transform;
            step3.FindPropertyRelative("reachRadius").floatValue = 3.5f;

            // Step 4: Dialogue core_escape (escape sequence with Kessler & Resh).
            var step4 = stepsProp.GetArrayElementAtIndex(4);
            step4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step4.FindPropertyRelative("label").stringValue = "Dialogue: Escape Sequence";
            step4.FindPropertyRelative("dialogue").objectReferenceValue = coreEscapeDialogue;

            // Step 5: Prompt — transition box to SafeHouse.
            var step5 = stepsProp.GetArrayElementAtIndex(5);
            step5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step5.FindPropertyRelative("label").stringValue = "Prompt: Board Hauler to SafeHouse";
            step5.FindPropertyRelative("promptObject").objectReferenceValue = boardBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep02CoreScenePath);
            EnsureScenesInBuild(Galaxy1Ep02CoreScenePath);

            Debug.Log($"[Space Samurai] EP02 Core scene built at {Galaxy1Ep02CoreScenePath}. " +
                      "Layout: core chamber (Khall at mainframe, escape pod) → concourse corridor (alarm lights, evacuation timer) → airlock (transition). " +
                      "6 steps: core_khall dialogue → trigger alarm + pod escape → defeat 8 elite enforcers + barks → reach concourse end → " +
                      "core_escape dialogue → board hauler transition to SafeHouse. " +
                      "EvacuationTimer displays 90s countdown. Khall NPC under 'KhallStanding' parent; 'KhallEscape' pod activates separately.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP02 Safe House", priority = 71)]
        public static void BuildEp02SafeHouse()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // SafeHouse lighting: cool, icy blue-white tones for the remote arctic refuge.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.9f, 1f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.22f);

            // Heater/warm accent lights for the main room.
            BuildAccentPointLight("HeaterLight1", new Vector3(-3f, 2.4f, 5f),
                new Color(1f, 0.75f, 0.45f), intensity: 1.8f, range: 10f);
            BuildAccentPointLight("HeaterLight2", new Vector3(3f, 2.4f, 12f),
                new Color(1f, 0.8f, 0.55f), intensity: 1.7f, range: 10f);
            // Training corridor cool accent.
            BuildAccentPointLight("CorridorLight", new Vector3(0f, 2.4f, 24f),
                new Color(0.5f, 0.75f, 0.95f), intensity: 1.6f, range: 12f);
            // Window observation area cool accent.
            BuildAccentPointLight("WindowLight", new Vector3(0f, 2.4f, 17f),
                new Color(0.6f, 0.85f, 1f), intensity: 1.9f, range: 14f);

            // ---- SafeHouse interior: main ice room (z 0-18) -> training corridor (z 18-30) -> observation window ----
            var interiorGo = new GameObject("SafeHouseInterior");
            var interior = interiorGo.transform;
            var iceFloorColor = new Color(0.25f, 0.35f, 0.42f);
            var iceCeilColor = new Color(0.18f, 0.24f, 0.3f);

            // Main ice room: x[-5,5], z[0,18], wide refuge space with star-map.
            BuildFloorCeiling(interior, "MainRoom", new Vector3(0f, 0f, 9f), new Vector3(10f, 0f, 18f), iceFloorColor, iceCeilColor);
            BuildWall(interior, "MainRoom_WallW", new Vector3(-5f, 1.5f, 9f), new Vector3(0.2f, 3f, 18f));
            BuildWall(interior, "MainRoom_WallE", new Vector3(5f, 1.5f, 9f), new Vector3(0.2f, 3f, 18f));
            BuildWall(interior, "MainRoom_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(10f, 3f, 0.2f));
            BuildDoorwayWall(interior, "MainRoom_WallBack", new Vector3(0f, 1.5f, 18f), 10f, true, 2.4f);

            // Training corridor: x[-2.5,2.5], z[18,30], narrow passage to back observation area.
            BuildFloorCeiling(interior, "TrainingCorridor", new Vector3(0f, 0f, 24f), new Vector3(5f, 0f, 12f), iceFloorColor, iceCeilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -2.5f, 18f, 30f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 2.5f, 18f, 30f, new float[0], 2.4f);

            // Observation nook: x[-4,4], z[30,36], back wall with large window.
            BuildFloorCeiling(interior, "ObservationNook", new Vector3(0f, 0f, 33f), new Vector3(8f, 0f, 6f), iceFloorColor, iceCeilColor);
            BuildWall(interior, "ObservationNook_WallW", new Vector3(-4f, 1.5f, 33f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "ObservationNook_WallE", new Vector3(4f, 1.5f, 33f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "ObservationNook_WallBack", new Vector3(0f, 1.5f, 36f), new Vector3(8f, 3f, 0.2f));
            BuildDoorwayWall(interior, "ObservationNook_WallFront", new Vector3(0f, 1.5f, 30f), 8f, true, 2.4f);

            // Observation window: large transparent "glass" quad on back wall.
            var windowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowQuad.name = "Window";
            windowQuad.transform.SetParent(interior, false);
            windowQuad.transform.localPosition = new Vector3(0f, 1.5f, 36f);
            windowQuad.transform.localScale = new Vector3(6f, 2f, 1f);
            windowQuad.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            TintShared(windowQuad.GetComponent<Renderer>(), new Color(0.1f, 0.3f, 0.5f, 0.6f));
            // Remove collider so it's visual-only.
            var windowCollider = windowQuad.GetComponent<Collider>();
            if (windowCollider != null) Object.DestroyImmediate(windowCollider);

            // Heater/crate props in main room (visual clutter, warm accents).
            BuildProp(interior, "Heater1", new Vector3(-3f, 0.8f, 4f), new Vector3(1.2f, 1.6f, 0.8f), new Color(1f, 0.6f, 0.3f));
            BuildProp(interior, "Crate1", new Vector3(3f, 0.8f, 12f), new Vector3(1f, 1.2f, 1f), new Color(0.4f, 0.35f, 0.3f));

            // Star-map hologram: reuse the hologram build pattern (cyan projection in main room).
            var starMapHologram = BuildHologram(interior, new Vector3(0f, 1f, 14f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();

            // Player rig + bounds (no sword in SafeHouse, it's a refuge/dialogue scene).
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 40f;

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Resh: positioned by the star-map in main room.
            var reshPos = new Vector3(-1f, 1f, 14f);
            var reshGo = InstantiateNpc(ReshPrefabPath, AtFloor(reshPos), "Resh_SafeHouse");
            if (reshGo != null)
            {
                var reshNpc = reshGo.AddComponent<StoryNpc>();
                var reshSo = new SerializedObject(reshNpc);
                reshSo.FindProperty("displayName").stringValue = "Captain Resh";
                reshSo.FindProperty("remote").boolValue = false;
                reshSo.ApplyModifiedPropertiesWithoutUndo();

                var wander = reshGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = 2f;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler: positioned near Resh (co-pilot scale).
            var kesslerPos = new Vector3(1f, 1f, 14f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_SafeHouse");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Iris: small-scale child standing near the window (scale 0.7).
            var irisPos = new Vector3(0f, 0.7f, 34f);
            var irisGo = InstantiateNpc(IrisPrefabPath, AtFloor(irisPos), "Iris_SafeHouse");
            if (irisGo != null)
            {
                irisGo.transform.localScale = Vector3.one * 0.7f;
                var irisNpc = irisGo.AddComponent<StoryNpc>();
                var irisSo = new SerializedObject(irisNpc);
                irisSo.FindProperty("displayName").stringValue = "Iris";
                irisSo.FindProperty("remote").boolValue = false;
                irisSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var safehouseReshDialogue = BuildEp02DialoguePlayer("Dialogue_SafehouseResh", reshPos, "safehouse_resh", talkRef);
            var safehouseDrillDialogue = BuildEp02DialoguePlayer("Dialogue_SafehouseDrill", new Vector3(0f, 1.5f, 24f), "safehouse_drill");
            var safehouseDecisionDialogue = BuildEp02DialoguePlayer("Dialogue_SafehouseDecision", new Vector3(0f, 1.5f, 34f), "safehouse_decision", talkRef);

            // ---- Reach Triggers ----
            var corridorReachGo = new GameObject("CorridorReachPoint");
            corridorReachGo.transform.position = new Vector3(0f, 1f, 24f);

            var windowReachGo = new GameObject("WindowReachPoint");
            windowReachGo.transform.position = new Vector3(0f, 1f, 34f);

            // Transition box: "LAUNCH TO SPACE" — wired to ReturnToSpace() to return to space combat.
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 35.5f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Resh walker ----
            // Inactive until the Trigger step just before safehouse_decision. On enable it disables
            // Resh's StoryNpcWander and walks her from the star-map to beside the launch prompt, so she
            // crosses the room toward the prompt while delivering the final lines (Iris stays by the
            // window). Mirrors the EP01 "Kessler leads to the command room" walker beat.
            var reshWalkerGo = new GameObject("Resh_Walker");
            var reshWaypointGo = new GameObject("Resh_WalkWaypoint");
            reshWaypointGo.transform.SetParent(reshWalkerGo.transform, false);
            reshWaypointGo.transform.position = new Vector3(1.2f, 0f, 33f); // beside LaunchToSpaceBox (z=35.5), clear of Iris (z=34)
            var reshWalker = reshWalkerGo.AddComponent<NpcWalker>();
            var reshWalkerSo = new SerializedObject(reshWalker);
            if (reshGo != null) SetObjectRef(reshWalkerSo, "target", reshGo.transform);
            var reshWpProp = reshWalkerSo.FindProperty("waypoints");
            reshWpProp.arraySize = 1;
            reshWpProp.GetArrayElementAtIndex(0).objectReferenceValue = reshWaypointGo.transform;
            reshWalkerSo.FindProperty("moveSpeed").floatValue = 2.0f;
            reshWalkerSo.FindProperty("faceTravel").boolValue = true;
            reshWalkerSo.ApplyModifiedPropertiesWithoutUndo();
            reshWalkerGo.SetActive(false); // activated by the Trigger step before safehouse_decision

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue safehouse_resh (data-chip handoff).
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("label").stringValue = "Dialogue: Resh Debriefing";
            step0.FindPropertyRelative("dialogue").objectReferenceValue = safehouseReshDialogue;

            // Step 1: ReachTrigger — training corridor.
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step1.FindPropertyRelative("label").stringValue = "ReachTrigger: Training Corridor";
            step1.FindPropertyRelative("reachPoint").objectReferenceValue = corridorReachGo.transform;
            step1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Dialogue safehouse_drill (Ronin solo line).
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step2.FindPropertyRelative("label").stringValue = "Dialogue: Ronin's Realization";
            step2.FindPropertyRelative("dialogue").objectReferenceValue = safehouseDrillDialogue;

            // Step 3: ReachTrigger — observation window.
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step3.FindPropertyRelative("label").stringValue = "ReachTrigger: Window Observation";
            step3.FindPropertyRelative("reachPoint").objectReferenceValue = windowReachGo.transform;
            step3.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 4: Trigger — start Resh walking toward the launch prompt as the decision begins.
            var step4 = stepsProp.GetArrayElementAtIndex(4);
            step4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step4.FindPropertyRelative("label").stringValue = "Trigger: Resh Walks to Launch Prompt";
            var t4 = step4.FindPropertyRelative("triggerObjects");
            t4.arraySize = 1;
            t4.GetArrayElementAtIndex(0).objectReferenceValue = reshWalkerGo;

            // Step 5: Dialogue safehouse_decision (Resh/Kessler/Iris group).
            var step5 = stepsProp.GetArrayElementAtIndex(5);
            step5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step5.FindPropertyRelative("label").stringValue = "Dialogue: Pact Decision";
            step5.FindPropertyRelative("dialogue").objectReferenceValue = safehouseDecisionDialogue;

            // Step 6: Prompt — launch-to-space transition box (wired to ReturnToSpace).
            var step6 = stepsProp.GetArrayElementAtIndex(6);
            step6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step6.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            step6.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep02SafeHouseScenePath);
            EnsureScenesInBuild(Galaxy1Ep02SafeHouseScenePath);

            Debug.Log($"[Space Samurai] EP02 SafeHouse scene built at {Galaxy1Ep02SafeHouseScenePath}. " +
                      "Layout: main ice room (star-map, heaters, Resh/Kessler/Iris) → training corridor → observation window nook. " +
                      "7 steps: safehouse_resh dialogue → reach training corridor → safehouse_drill (Ronin solo) → reach window → " +
                      "trigger Resh walks to launch prompt → safehouse_decision dialogue → launch to space prompt (returns to space combat). " +
                      "Resh wanders main room then walks to the prompt during the decision. Iris (scale 0.7) by window. Window quad is transparent visual-only. Wired to ReturnToSpace() for ZoneCompleted flow.");
        }
    }
}

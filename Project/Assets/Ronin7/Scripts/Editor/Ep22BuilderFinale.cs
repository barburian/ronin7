using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
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
    /// EP22 "The Void Inheritors" scene builders for the final four scenes aboard the derelict generation ship.
    /// - What Ronin-1 Left: vault data-hub with 3 "conditioned reflex" self-copies
    /// - Younger Chain: rotating habitat ring with Ronin-12 duel
    /// - Broadcast: ship bridge under assault with 2 waves of Dominion boarders
    /// - Stellar Dive: THE EP22 FINALE, collapsing core zero-G scene, 2 failing security units, sets ep22_complete flag
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp22DialoguePlayer helper is already declared in Ep22Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 What Ronin-1 Left", priority = 243)]
        public static void BuildEp22WhatRonin1Left()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // What Ronin-1 Left: vault data-hub with archival gold + cold.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.55f, 0.45f); // gold/warm key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.11f); // warm dark ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.17f, 0.14f);
            RenderSettings.fogDensity = 0.016f;

            // Two accent lights: gold and cyan.
            BuildAccentPointLight("DataHubLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.75f, 0.4f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("DataHubLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.5f, 0.8f, 1f), intensity: 0.70f, range: 9f);

            // ---- Data Hub floor and structure ----
            var dataGo = new GameObject("DataHub");
            var data = dataGo.transform;
            var dataMetal = new Color(0.50f, 0.47f, 0.44f);
            var dataDark = new Color(0.30f, 0.27f, 0.24f);

            // Main data floor (11 x 19).
            BuildFloorCeiling(data, "DataHubFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 19f), dataMetal, dataDark);

            // Data hub walls.
            BuildWall(data, "DataHubWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));
            BuildWall(data, "DataHubWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));

            // Comm-array white-blue props.
            var commColor = new Color(0.7f, 0.75f, 0.8f);
            BuildProp(data, "CommArray1", new Vector3(-2.5f, 1.5f, 8f), new Vector3(0.6f, 2.5f, 0.6f), commColor);
            BuildProp(data, "CommArray2", new Vector3(0f, 1.5f, 11f), new Vector3(0.6f, 2.5f, 0.6f), commColor);
            BuildProp(data, "CommArray3", new Vector3(2.5f, 1.5f, 14f), new Vector3(0.6f, 2.5f, 0.6f), commColor);

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

            // ---- Irene StoryNpc ----
            var ireneGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ireneGo.name = "Irene";
            Object.DestroyImmediate(ireneGo.GetComponent<Collider>());
            ireneGo.transform.position = new Vector3(0f, 0f, 3f);
            ireneGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ireneGo.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.50f)); // amber/warm tint
            var ireneNpc = ireneGo.AddComponent<StoryNpc>();
            var inSo = new SerializedObject(ireneNpc);
            inSo.FindProperty("displayName").stringValue = "Irene Sols";
            inSo.FindProperty("remote").boolValue = false;
            inSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 1 Wave of 3 "Conditioned Reflex" Self-Copies ----
            var selfTint = new Color(0.5f, 0.5f, 0.6f); // steel grey
            var selfPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(1.5f, 0f, 10f)
            };

            var selfWaveHealths = new List<Health>();
            foreach (var pos in selfPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, selfTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                selfWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var selfSpawner = BuildWaveSpawner("SelfSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { selfWaveHealths },
                new[] { BuildEp22DialoguePlayer("Dialogue_FailsafeBarks", new Vector3(0f, 1.5f, 8f), "failsafe_barks") });

            // ---- Dialogue Players ----
            var ronin1ArchiveDialogue = BuildEp22DialoguePlayer("Dialogue_Ronin1Archive", new Vector3(0f, 1.5f, 2f), "ronin1_archive");
            var raDialogueSo = new SerializedObject(ronin1ArchiveDialogue);
            raDialogueSo.FindProperty("playOnStart").boolValue = true;
            raDialogueSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE DOCKING COLLAR".
            var dockingBoxGo = BuildTransitionBox("ToDockingBox", new Vector3(0f, 1.2f, 19.5f), "TO THE DOCKING COLLAR",
                out var dockingBtn, out var dockingTransition);
            var dbSo = new SerializedObject(dockingTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22YoungerChainSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dockingBtn.onClick,
                new UnityEngine.Events.UnityAction(dockingTransition.LoadOnFootScene));
            dockingBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue ronin1_archive (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ronin-1 Archive";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ronin1ArchiveDialogue;

            // Step 1: DefeatWaves — 3 self-copies.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Conditioned Reflex (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = selfSpawner;

            // Step 2: Prompt — transition to Docking Collar.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To the Docking Collar";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = dockingBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22WhatRonin1LeftScenePath);
            EnsureScenesInBuild(Galaxy3Ep22WhatRonin1LeftScenePath, Galaxy3Ep22YoungerChainScenePath);

            Debug.Log($"[Space Samurai] EP22 What Ronin-1 Left scene built at {Galaxy3Ep22WhatRonin1LeftScenePath}. " +
                      "Data-hub vault with gold/cyan lighting, metal floor/walls, white-blue comm-array props. " +
                      "Irene Sols NPC (amber/warm tint, no Health). " +
                      "3 Conditioned Reflex (self-copies, steel grey tint, nonLethal). " +
                      "3 steps: ronin1_archive (auto) → defeat 3 self-copies (failsafe_barks bark) → to docking collar.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Younger Chain", priority = 244)]
        public static void BuildEp22YoungerChain()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Younger Chain: rotating habitat ring with steel + warm lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.6f, 0.62f); // warm steel key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.16f); // neutral steel ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.18f, 0.20f);
            RenderSettings.fogDensity = 0.015f;

            // Two accent lights: white and amber.
            BuildAccentPointLight("HabitatLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.9f, 0.9f, 0.95f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("HabitatLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.7f, 0.4f), intensity: 0.70f, range: 9f);

            // ---- Habitat Ring floor and structure ----
            var habitatGo = new GameObject("HabitatRing");
            var habitat = habitatGo.transform;
            var habitatMetal = new Color(0.48f, 0.48f, 0.50f);
            var habitatDark = new Color(0.28f, 0.28f, 0.30f);

            // Main habitat floor (12 x 20).
            BuildFloorCeiling(habitat, "HabitatFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), habitatMetal, habitatDark);

            // Habitat walls.
            BuildWall(habitat, "HabitatWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(habitat, "HabitatWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

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

            // ---- Irene StoryNpc ----
            var ireneGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ireneGo.name = "Irene";
            Object.DestroyImmediate(ireneGo.GetComponent<Collider>());
            ireneGo.transform.position = new Vector3(-1.5f, 0f, 3f);
            ireneGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ireneGo.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.50f)); // amber/warm tint
            var ireneNpc = ireneGo.AddComponent<StoryNpc>();
            var inSo = new SerializedObject(ireneNpc);
            inSo.FindProperty("displayName").stringValue = "Irene Sols";
            inSo.FindProperty("remote").boolValue = false;
            inSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Lira StoryNpc (small scale) ----
            var liraGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            liraGo.name = "Lira";
            Object.DestroyImmediate(liraGo.GetComponent<Collider>());
            liraGo.transform.position = new Vector3(1.5f, 0f, 3f);
            liraGo.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
            TintShared(liraGo.GetComponent<Renderer>(), new Color(0.7f, 0.65f, 0.6f)); // pale tan tint
            var liraNpc = liraGo.AddComponent<StoryNpc>();
            var lnSo = new SerializedObject(liraNpc);
            lnSo.FindProperty("displayName").stringValue = "Lira";
            lnSo.FindProperty("remote").boolValue = false;
            lnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 1 Wave of 1 Ronin-12 Duel ----
            var ronin12Tint = new Color(0.45f, 0.40f, 0.50f); // purple-grey duellist tint
            var ronin12Healths = new List<Health>();

            var ronin12 = BuildDominionEnemy(new Vector3(0f, 0f, 9f), playerHealth, enemyDef);
            var r12Renderer = ronin12.GetComponent<Renderer>();
            if (r12Renderer != null) TintShared(r12Renderer, ronin12Tint);
            var r12Melee = ronin12.GetComponent<MeleeAttacker>();
            if (r12Melee != null)
            {
                var r12So = new SerializedObject(r12Melee);
                r12So.FindProperty("nonLethalDisable").boolValue = true;
                r12So.ApplyModifiedPropertiesWithoutUndo();
            }
            ronin12.gameObject.SetActive(false);
            ronin12Healths.Add(ronin12.GetComponent<Health>());

            var ronin12Spawner = BuildWaveSpawner("Ronin12Spawner", new Vector3(0f, 0.5f, 9f), 2f,
                new List<List<Health>> { ronin12Healths },
                new[] { BuildEp22DialoguePlayer("Dialogue_Ronin12Barks", new Vector3(0f, 1.5f, 9f), "ronin12_barks") });

            // ---- Dialogue Players ----
            var youngerChainDialogue = BuildEp22DialoguePlayer("Dialogue_YoungerChain", new Vector3(0f, 1.5f, 2f), "younger_chain");
            var ycSo = new SerializedObject(youngerChainDialogue);
            ycSo.FindProperty("playOnStart").boolValue = true;
            ycSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE BRIDGE — BROADCAST".
            var broadcastBoxGo = BuildTransitionBox("ToBroadcastBox", new Vector3(0f, 1.2f, 20.5f), "TO THE BRIDGE — BROADCAST",
                out var broadcastBtn, out var broadcastTransition);
            var bbSo = new SerializedObject(broadcastTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22BroadcastSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(broadcastBtn.onClick,
                new UnityEngine.Events.UnityAction(broadcastTransition.LoadOnFootScene));
            broadcastBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue younger_chain (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Younger Chain";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = youngerChainDialogue;

            // Step 1: DefeatWaves — Ronin-12 duel.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Ronin-12 (1)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = ronin12Spawner;

            // Step 2: Prompt — transition to Broadcast.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To the Bridge — Broadcast";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = broadcastBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22YoungerChainScenePath);
            EnsureScenesInBuild(Galaxy3Ep22YoungerChainScenePath, Galaxy3Ep22BroadcastScenePath);

            Debug.Log($"[Space Samurai] EP22 Younger Chain scene built at {Galaxy3Ep22YoungerChainScenePath}. " +
                      "Habitat ring with warm steel lighting, metal floor/walls. " +
                      "Irene Sols NPC (amber/warm tint, no Health) + Lira NPC (small scale, pale tan tint, no Health). " +
                      "1 Ronin-12 Duel (purple-grey tint, nonLethal). " +
                      "3 steps: younger_chain (auto) → defeat Ronin-12 (ronin12_barks bark) → to bridge broadcast.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Broadcast", priority = 245)]
        public static void BuildEp22Broadcast()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Broadcast: ship bridge under assault (red-alert lighting).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.45f, 0.42f); // red-alert key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.11f, 0.10f); // red-tinted dark ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.22f, 0.13f, 0.12f);
            RenderSettings.fogDensity = 0.018f;

            // Two accent lights: red and amber.
            BuildAccentPointLight("BridgeLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.3f, 0.3f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("BridgeLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.7f, 0.4f), intensity: 0.70f, range: 9f);

            // ---- Bridge floor and structure ----
            var bridgeGo = new GameObject("Bridge");
            var bridge = bridgeGo.transform;
            var bridgeMetal = new Color(0.45f, 0.43f, 0.40f);
            var bridgeDark = new Color(0.25f, 0.23f, 0.20f);

            // Main bridge floor (12 x 21).
            BuildFloorCeiling(bridge, "BridgeFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), bridgeMetal, bridgeDark);

            // Bridge walls.
            BuildWall(bridge, "BridgeWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(bridge, "BridgeWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Bright cyan-white beacon at center.
            var beaconColor = new Color(0.8f, 0.9f, 1f);
            BuildProp(bridge, "Beacon", new Vector3(0f, 1.8f, 10f), new Vector3(0.5f, 0.5f, 0.5f), beaconColor);

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

            // ---- Irene StoryNpc ----
            var ireneGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ireneGo.name = "Irene";
            Object.DestroyImmediate(ireneGo.GetComponent<Collider>());
            ireneGo.transform.position = new Vector3(0f, 0f, 3f);
            ireneGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ireneGo.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.50f)); // amber/warm tint
            var ireneNpc = ireneGo.AddComponent<StoryNpc>();
            var inSo = new SerializedObject(ireneNpc);
            inSo.FindProperty("displayName").stringValue = "Irene Sols";
            inSo.FindProperty("remote").boolValue = false;
            inSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 2 Waves of Dominion Boarders (3 + 3) ----
            var boarderTint = new Color(0.40f, 0.42f, 0.46f); // standard dominion grey

            // Wave 1: 3 boarders at z8.
            var wave1Healths = new List<Health>();
            var wave1Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, boarderTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 3 boarders at z11.
            var wave2Healths = new List<Health>();
            var wave2Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 11f),
                new Vector3(0f, 0f, 11.5f),
                new Vector3(2f, 0f, 12f)
            };
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, boarderTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var boarderSpawner = BuildWaveSpawner("BoarderSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new[] { BuildEp22DialoguePlayer("Dialogue_BoardingBarks", new Vector3(0f, 1.5f, 8f), "boarding_barks") });

            // ---- Dialogue Players ----
            var broadcastDialogue = BuildEp22DialoguePlayer("Dialogue_Broadcast", new Vector3(0f, 1.5f, 2f), "broadcast");
            var bdSo = new SerializedObject(broadcastDialogue);
            bdSo.FindProperty("playOnStart").boolValue = true;
            bdSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE STELLAR CORE".
            var coreBoxGo = BuildTransitionBox("ToCoreBox", new Vector3(0f, 1.2f, 21.5f), "TO THE STELLAR CORE",
                out var coreBtn, out var coreTransition);
            var cbSo = new SerializedObject(coreTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22StellarDiveSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(coreBtn.onClick,
                new UnityEngine.Events.UnityAction(coreTransition.LoadOnFootScene));
            coreBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue broadcast (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Broadcast";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = broadcastDialogue;

            // Step 1: DefeatWaves — 2 waves of 3 boarders.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Boarders (2 waves, 3+3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = boarderSpawner;

            // Step 2: Prompt — transition to Stellar Core.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To the Stellar Core";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = coreBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22BroadcastScenePath);
            EnsureScenesInBuild(Galaxy3Ep22BroadcastScenePath, Galaxy3Ep22StellarDiveScenePath);

            Debug.Log($"[Space Samurai] EP22 Broadcast scene built at {Galaxy3Ep22BroadcastScenePath}. " +
                      "Bridge under assault with red-alert lighting, metal floor/walls, bright cyan beacon. " +
                      "Irene Sols NPC (amber/warm tint, no Health). " +
                      "6 Dominion Boarders (grey tint, nonLethal, 2 waves of 3). " +
                      "3 steps: broadcast (auto) → defeat 6 boarders in 2 waves (boarding_barks bark) → to stellar core.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Stellar Dive", priority = 246)]
        public static void BuildEp22StellarDive()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Stellar Dive: collapsing ship core + stellar fire (THE EP22 FINALE, zero-G).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.5f, 0.35f); // bright orange/fire key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.12f, 0.08f); // dark warm ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.18f, 0.10f);
            RenderSettings.fogDensity = 0.020f;

            // Two accent lights: bright orange and white.
            BuildAccentPointLight("CoreLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.55f, 0.25f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("CoreLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 1f, 0.9f), intensity: 0.75f, range: 9f);

            // ---- Collapsing core floor and structure ----
            var coreGo = new GameObject("CollapsingCore");
            var core = coreGo.transform;
            var coreMetal = new Color(0.50f, 0.45f, 0.42f);
            var coreDark = new Color(0.30f, 0.25f, 0.22f);

            // Main core floor (12 x 21).
            BuildFloorCeiling(core, "CoreFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), coreMetal, coreDark);

            // Core walls.
            BuildWall(core, "CoreWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(core, "CoreWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Fire/debris orange props.
            var fireColor = new Color(1f, 0.55f, 0.25f);
            BuildProp(core, "Fire1", new Vector3(-3f, 1.5f, 8f), new Vector3(1.2f, 1.8f, 0.8f), fireColor);
            BuildProp(core, "Fire2", new Vector3(0f, 1.5f, 12f), new Vector3(0.8f, 1.5f, 1.2f), fireColor);
            BuildProp(core, "Fire3", new Vector3(3f, 1.5f, 16f), new Vector3(1f, 1.8f, 0.8f), fireColor);

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

            // ---- Zero-G Grab Locomotion ----
            var zeroGLocomotion = rig.AddComponent<ZeroGGrabLocomotion>();
            var zeroGSo = new SerializedObject(zeroGLocomotion);
            var vrRig = rig.GetComponent<VRRig>();
            if (vrRig != null)
            {
                var leftHand = vrRig.LeftHand;
                var rightHand = vrRig.RightHand;
                SetObjectRef(zeroGSo, "leftHand", leftHand);
                SetObjectRef(zeroGSo, "rightHand", rightHand);
            }
            var gripRef = FindRef(refs, "Left Hand", "Select");
            if (gripRef != null) SetObjectRef(zeroGSo, "gripAction", gripRef);
            zeroGSo.FindProperty("grabLayerMask").intValue = LayerMask.GetMask("Default");
            zeroGSo.ApplyModifiedPropertiesWithoutUndo();

            // Zero-G Combat Volume box trigger.
            var zeroGVolume = new GameObject("ZeroGCombatVolume");
            zeroGVolume.transform.SetParent(coreGo.transform, false);
            zeroGVolume.transform.localPosition = new Vector3(0f, 1f, 10f);
            var zeroGCollider = zeroGVolume.AddComponent<BoxCollider>();
            zeroGCollider.size = new Vector3(12f, 3f, 21f);
            zeroGCollider.isTrigger = true;
            var zeroGComp = zeroGVolume.AddComponent<ZeroGCombatVolume>();
            var zeroGCSo = new SerializedObject(zeroGComp);
            zeroGCSo.FindProperty("driftDamping").floatValue = 0.95f;
            zeroGCSo.ApplyModifiedPropertiesWithoutUndo();

            // ZeroGHandle markers.
            var handlePositions = new Vector3[]
            {
                new Vector3(-3f, 1.5f, 8f), new Vector3(0f, 1.5f, 12f),
                new Vector3(3f, 1.5f, 16f),
                new Vector3(-5.8f, 1.5f, 6f), new Vector3(-5.8f, 1.5f, 14f),
                new Vector3(5.8f, 1.5f, 10f)
            };
            for (int i = 0; i < handlePositions.Length; i++)
            {
                var handleGo = new GameObject($"Handle{i}");
                handleGo.transform.SetParent(coreGo.transform, false);
                handleGo.transform.localPosition = handlePositions[i];
                var handleCollider = handleGo.AddComponent<SphereCollider>();
                handleCollider.radius = 0.3f;
                handleCollider.isTrigger = true;
                handleGo.AddComponent<ZeroGHandle>();
            }

            // ---- 1 Wave of 2 Failing Security Units ----
            var securityTint = new Color(0.7f, 0.5f, 0.4f); // damaged orange-brown
            var securityPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 10f)
            };

            var securityWaveHealths = new List<Health>();
            foreach (var pos in securityPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, securityTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                securityWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var securitySpawner = BuildWaveSpawner("SecuritySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { securityWaveHealths },
                new[] { BuildEp22DialoguePlayer("Dialogue_StellarDiveBarks", new Vector3(0f, 1.5f, 8f), "stellar_dive_barks") });

            // ---- Dialogue Players ----
            var voidInheritorsDialogue = BuildEp22DialoguePlayer("Dialogue_VoidInheritors", new Vector3(0f, 1.5f, 2f), "void_inheritors");
            var viSo = new SerializedObject(voidInheritorsDialogue);
            viSo.FindProperty("playOnStart").boolValue = true;
            viSo.ApplyModifiedPropertiesWithoutUndo();

            var closingDialogue = BuildEp22DialoguePlayer("Dialogue_Closing", new Vector3(0f, 1.5f, 16f), "closing");

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 21.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set flag: ep22_complete.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep22_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue void_inheritors (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Void Inheritors";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = voidInheritorsDialogue;

            // Step 1: DefeatWaves — 2 failing security units.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Failing Security (2)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = securitySpawner;

            // Step 2: Dialogue closing.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Closing";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = closingDialogue;

            // Step 3: Prompt — return to space with ep22_complete flag.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep22_complete)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22StellarDiveScenePath);
            EnsureScenesInBuild(Galaxy3Ep22StellarDiveScenePath);

            Debug.Log($"[Space Samurai] EP22 Stellar Dive scene built at {Galaxy3Ep22StellarDiveScenePath}. " +
                      "Zero-G collapsing core with bright orange/fire lighting, metal floor/walls, orange fire/debris props, " +
                      "ZeroGGrabLocomotion + ZeroGCombatVolume + 6 ZeroGHandle markers. " +
                      "2 Failing Security Units (damaged orange-brown tint, nonLethal). " +
                      "4 steps: void_inheritors (auto) → defeat 2 security (stellar_dive_barks bark) → closing dialogue → " +
                      "return to stars Prompt (sets ep22_complete via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 22 FINALE (sets ep22_complete).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP22 Scenes", priority = 247)]
        public static void BuildAllEp22Scenes()
        {
            BuildEp22CargoApproach();
            BuildEp22VaultOfGhosts();
            BuildEp22ChildrenBelow();
            BuildEp22WhatRonin1Left();
            BuildEp22YoungerChain();
            BuildEp22Broadcast();
            BuildEp22StellarDive();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP22 scenes built + inputs rewired.");
        }
    }
}

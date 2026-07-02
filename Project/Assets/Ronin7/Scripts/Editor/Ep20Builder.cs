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
    /// EP20 scene builders for the first three episodes. Builds three on-foot scenes:
    /// - Docking Bay: industrial cold blue-grey bazaar docking facility with 4 syndicate raiders
    /// - Chemical Sector: crimson narcotic-vapor corridor with 4 Crimson Lotus enforcers
    /// - Varek Shard Vault: cold scarred-moon warehouse with 5 Dominion hunter-drones
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP20 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep20" and loads lines from Ep20Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp20DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep20Lines.Get(setId), advanceRef, setId, clipPrefix: "ep20");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Docking Bay", priority = 200)]
        public static void BuildEp20DockingBay()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Docking Bay: industrial cold blue-grey with warning amber.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.65f, 0.7f); // cool industrial key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.18f); // cool grey ambient

            // Exponential fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.20f, 0.24f);
            RenderSettings.fogDensity = 0.018f;

            // Two accent lights: amber and blue.
            BuildAccentPointLight("DockingLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.6f, 0.3f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("DockingLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.5f, 0.65f, 0.9f), intensity: 0.70f, range: 9f);

            // ---- Docking bay floor and structure ----
            var bayGo = new GameObject("DockingBay");
            var bay = bayGo.transform;
            var metalGrey = new Color(0.45f, 0.48f, 0.50f);
            var darkerGrey = new Color(0.28f, 0.30f, 0.32f);

            // Main docking bay floor (14 x 18).
            BuildFloorCeiling(bay, "BayFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 18f), metalGrey, darkerGrey);

            // Docking bay walls.
            BuildWall(bay, "BayWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(bay, "BayWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // Cargo crate props.
            var crateColor = new Color(0.55f, 0.50f, 0.48f);
            BuildProp(bay, "Crate1", new Vector3(-2f, 0.8f, 5f), new Vector3(1.2f, 1.2f, 1.5f), crateColor);
            BuildProp(bay, "Crate2", new Vector3(2f, 0.8f, 6f), new Vector3(1.5f, 1f, 1.2f), crateColor);

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

            // ---- Vess StoryNpc ----
            var vessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessGo.name = "Vess";
            Object.DestroyImmediate(vessGo.GetComponent<Collider>());
            vessGo.transform.position = new Vector3(0f, 0f, 3f);
            vessGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(vessGo.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vnSo = new SerializedObject(vessNpc);
            vnSo.FindProperty("displayName").stringValue = "Vess";
            vnSo.FindProperty("remote").boolValue = false;
            vnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 4 Mixed Syndicate Raiders: 1 wave ----
            var raiderColors = new Color[]
            {
                new Color(0.60f, 0.45f, 0.38f), // rust
                new Color(0.50f, 0.50f, 0.48f), // steel
                new Color(0.58f, 0.42f, 0.35f), // rust
                new Color(0.52f, 0.48f, 0.45f)  // steel
            };

            var raiderWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 9f)
            };

            var raiderWaveHealths = new List<Health>();
            for (int i = 0; i < raiderWavePositions.Length; i++)
            {
                var enemy = BuildDominionEnemy(raiderWavePositions[i], playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, raiderColors[i]);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                raiderWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var raiderSpawner = BuildWaveSpawner("RaiderSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { raiderWaveHealths },
                new[] { BuildEp20DialoguePlayer("Dialogue_DockingBarks", new Vector3(0f, 1.5f, 8f), "docking_barks") });

            // ---- Dialogue Players ----
            var dockingChaosDialogue = BuildEp20DialoguePlayer("Dialogue_DockingChaos", new Vector3(0f, 1.5f, 2f), "docking_chaos");
            var dcSo = new SerializedObject(dockingChaosDialogue);
            dcSo.FindProperty("playOnStart").boolValue = true;
            dcSo.ApplyModifiedPropertiesWithoutUndo();

            var recognitionDialogue = BuildEp20DialoguePlayer("Dialogue_Recognition", new Vector3(0f, 1.5f, 14f), "recognition");

            // Transition box: "TO CHEMICAL SECTOR".
            var chemicalBoxGo = BuildTransitionBox("ToChemicalBox", new Vector3(0f, 1.2f, 18.5f), "TO CHEMICAL SECTOR",
                out var chemicalBtn, out var chemicalTransition);
            var cbSo = new SerializedObject(chemicalTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20ChemicalSectorSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(chemicalBtn.onClick,
                new UnityEngine.Events.UnityAction(chemicalTransition.LoadOnFootScene));
            chemicalBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue docking_chaos (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Docking Chaos";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = dockingChaosDialogue;

            // Step 1: DefeatWaves — 4 syndicate raiders.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Syndicate Raiders (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = raiderSpawner;

            // Step 2: Dialogue recognition.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Recognition";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = recognitionDialogue;

            // Step 3: Prompt — transition to Chemical Sector.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Move to Chemical Sector";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = chemicalBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20DockingBayScenePath);
            EnsureScenesInBuild(Galaxy3Ep20DockingBayScenePath, Galaxy3Ep20ChemicalSectorScenePath);

            Debug.Log($"[Space Samurai] EP20 Docking Bay scene built at {Galaxy3Ep20DockingBayScenePath}. " +
                      "Layout: industrial cold blue-grey docking bay with metal floor/walls, 2 cargo crate props, amber+blue accent lights. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "4 Syndicate Raiders (rust/steel tints, nonLethal). " +
                      "4 steps: docking_chaos (auto) → defeat 4 raiders (docking_barks bark) → recognition dialogue → transition to Chemical Sector.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Chemical Sector", priority = 201)]
        public static void BuildEp20ChemicalSector()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Chemical Sector: crimson narcotic-vapor corridor.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.35f, 0.4f); // crimson key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.10f, 0.12f); // dark warm ambient

            // Dense fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.14f, 0.18f);
            RenderSettings.fogDensity = 0.025f;

            // Two red accent lights.
            BuildAccentPointLight("ChemicalLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.40f, 0.50f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ChemicalLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.35f, 0.45f), intensity: 0.70f, range: 9f);

            // ---- Chemical sector floor and structure ----
            var chemGo = new GameObject("ChemicalSector");
            var chem = chemGo.transform;
            var crimsonMetal = new Color(0.50f, 0.25f, 0.28f);
            var darkerCrimson = new Color(0.30f, 0.15f, 0.18f);

            // Main corridor floor (10 x 20).
            BuildFloorCeiling(chem, "ChemicalFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), crimsonMetal, darkerCrimson);

            // Corridor walls.
            BuildWall(chem, "ChemicalWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(chem, "ChemicalWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Vapor tank prop.
            BuildProp(chem, "VaporTank", new Vector3(0f, 1.2f, 18f), new Vector3(1.5f, 1.8f, 0.8f), new Color(0.55f, 0.25f, 0.30f));

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

            // ---- Vess StoryNpc ----
            var vessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessGo.name = "Vess";
            Object.DestroyImmediate(vessGo.GetComponent<Collider>());
            vessGo.transform.position = new Vector3(0f, 0f, 3f);
            vessGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(vessGo.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vnSo = new SerializedObject(vessNpc);
            vnSo.FindProperty("displayName").stringValue = "Vess";
            vnSo.FindProperty("remote").boolValue = false;
            vnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 4 Crimson Lotus Enforcers: 1 wave ----
            var lotusColor = new Color(0.75f, 0.30f, 0.35f);
            var lotusWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f)
            };

            var lotusWaveHealths = new List<Health>();
            foreach (var pos in lotusWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, lotusColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                lotusWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var lotusSpawner = BuildWaveSpawner("LotusSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { lotusWaveHealths },
                new[] { BuildEp20DialoguePlayer("Dialogue_LotusBarks", new Vector3(0f, 1.5f, 8f), "lotus_barks") });

            // ---- Dialogue Players ----
            var graveyardDialogue = BuildEp20DialoguePlayer("Dialogue_Graveyard", new Vector3(0f, 1.5f, 14f), "graveyard");

            // Transition box: "TO VAREK SHARD VAULT".
            var varekBoxGo = BuildTransitionBox("ToVarekBox", new Vector3(0f, 1.2f, 20.5f), "TO VAREK SHARD VAULT",
                out var varekBtn, out var varekTransition);
            var vbSo = new SerializedObject(varekTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20VarekShardVaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(varekBtn.onClick,
                new UnityEngine.Events.UnityAction(varekTransition.LoadOnFootScene));
            varekBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var missionSo = new SerializedObject(missionDirector);
            var stepsProp = missionSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: DefeatWaves — 4 lotus enforcers.
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: Crimson Lotus Enforcers (4)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = lotusSpawner;

            // Step 1: Dialogue graveyard.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Graveyard";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = graveyardDialogue;

            // Step 2: Prompt — transition to Varek Shard Vault.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Move to Varek Shard Vault";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = varekBoxGo;

            missionSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20ChemicalSectorScenePath);
            EnsureScenesInBuild(Galaxy3Ep20ChemicalSectorScenePath, Galaxy3Ep20VarekShardVaultScenePath);

            Debug.Log($"[Space Samurai] EP20 Chemical Sector scene built at {Galaxy3Ep20ChemicalSectorScenePath}. " +
                      "Layout: crimson narcotic-vapor corridor with dense red fog, dark metal floor/walls, vapor tank prop. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "4 Crimson Lotus Enforcers (crimson tint, nonLethal). " +
                      "3 steps: defeat 4 lotus enforcers (lotus_barks bark) → graveyard dialogue → transition to Varek Shard Vault.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Varek Shard Vault", priority = 202)]
        public static void BuildEp20VarekShardVault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Varek Shard Vault: cold scarred-moon cold-storage warehouse.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.6f, 0.7f); // cool pale key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.15f, 0.18f); // cool dark ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.20f, 0.24f);
            RenderSettings.fogDensity = 0.015f;

            // Two accent lights: cyan and warm-orange.
            BuildAccentPointLight("VarekLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 1f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("VarekLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.65f, 0.40f), intensity: 0.70f, range: 9f);

            // ---- Vault floor and structure ----
            var vaultGo = new GameObject("VarekVault");
            var vault = vaultGo.transform;
            var coldMetal = new Color(0.42f, 0.45f, 0.48f);
            var darkerCold = new Color(0.25f, 0.28f, 0.32f);

            // Main vault floor (12 x 20).
            BuildFloorCeiling(vault, "VarekFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), coldMetal, darkerCold);

            // Vault walls.
            BuildWall(vault, "VarekWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(vault, "VarekWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Manifest rack props.
            BuildProp(vault, "ManifestRack1", new Vector3(-3f, 1.5f, 8f), new Vector3(1.5f, 2f, 0.8f), new Color(0.50f, 0.50f, 0.52f));
            BuildProp(vault, "ManifestRack2", new Vector3(3f, 1.5f, 12f), new Vector3(1.5f, 2f, 0.8f), new Color(0.48f, 0.48f, 0.50f));

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

            // ---- Vess StoryNpc ----
            var vessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessGo.name = "Vess";
            Object.DestroyImmediate(vessGo.GetComponent<Collider>());
            vessGo.transform.position = new Vector3(0f, 0f, 3f);
            vessGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(vessGo.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vnSo = new SerializedObject(vessNpc);
            vnSo.FindProperty("displayName").stringValue = "Vess";
            vnSo.FindProperty("remote").boolValue = false;
            vnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5 Dominion Hunter-Drones: 1 wave ----
            var droneColor = new Color(0.55f, 0.62f, 0.72f); // pale steel-blue tint
            var droneWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f),
                new Vector3(0f, 0f, 10f)
            };

            var droneWaveHealths = new List<Health>();
            foreach (var pos in droneWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, droneColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                droneWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { BuildEp20DialoguePlayer("Dialogue_DroneBarks", new Vector3(0f, 1.5f, 8f), "drone_barks") });

            // ---- Dialogue Players ----
            var vaultDescentDialogue = BuildEp20DialoguePlayer("Dialogue_VaultDescent", new Vector3(0f, 1.5f, 2f), "vault_descent");
            var vdSo = new SerializedObject(vaultDescentDialogue);
            vdSo.FindProperty("playOnStart").boolValue = true;
            vdSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO CENTRAL VAULT".
            var centralBoxGo = BuildTransitionBox("ToCentralBox", new Vector3(0f, 1.2f, 20.5f), "TO CENTRAL VAULT",
                out var centralBtn, out var centralTransition);
            var ctSo = new SerializedObject(centralTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20CentralVaultSceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(centralBtn.onClick,
                new UnityEngine.Events.UnityAction(centralTransition.LoadOnFootScene));
            centralBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var missionSo = new SerializedObject(missionDirector);
            var stepsProp = missionSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue vault_descent (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultDescentDialogue;

            // Step 1: DefeatWaves — 5 hunter-drones.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Hunter-Drones (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Prompt — transition to Central Vault.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Move to Central Vault";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = centralBoxGo;

            missionSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20VarekShardVaultScenePath);
            EnsureScenesInBuild(Galaxy3Ep20VarekShardVaultScenePath, Galaxy3Ep20CentralVaultScenePath);

            Debug.Log($"[Space Samurai] EP20 Varek Shard Vault scene built at {Galaxy3Ep20VarekShardVaultScenePath}. " +
                      "Layout: cold scarred-moon warehouse with pale cool lighting, dark metal floor/walls, manifest racks props. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "5 Dominion Hunter-Drones (pale steel-blue tint, nonLethal). " +
                      "3 steps: vault_descent (auto) → defeat 5 drones (drone_barks bark) → transition to Central Vault.");
        }
    }
}

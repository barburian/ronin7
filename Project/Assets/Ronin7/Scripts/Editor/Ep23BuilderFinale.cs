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
    /// EP23 "Thermopause" scene builders for the final four scenes at the cryo-outpost.
    /// - The Commander: cryo-chamber with 6 Defense Drones + Vale NPC + CryoChillController
    /// - Defective Generation: upper decks with 4 cryo-animates + Vale ally
    /// - The Wake: revival hall with 2 waves of 3 Dominion Boarders + Vale ally
    /// - Ghost Fleet: THE EP23 FINALE, escape corridor with 3 Dominion Pursuit Units, sets ep23_complete flag
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp23DialoguePlayer helper is already declared in Ep23Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 The Commander", priority = 253)]
        public static void BuildEp23TheCommander()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Commander: central cryo-chamber, supercooled blue-white light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.70f, 0.80f, 0.95f); // supercooled blue-white key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.28f); // cool ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.35f, 0.45f);
            RenderSettings.fogDensity = 0.019f;

            // Two accent lights: bright cyan and white.
            BuildAccentPointLight("ChamberLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.6f, 0.9f, 1f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ChamberLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.9f, 0.95f, 1f), intensity: 0.70f, range: 9f);

            // ---- Cryo-chamber floor and structure ----
            var chamberGo = new GameObject("CryoChamber");
            var chamber = chamberGo.transform;
            var chamberMetal = new Color(0.50f, 0.58f, 0.68f);
            var chamberDark = new Color(0.30f, 0.38f, 0.48f);

            // Main chamber floor (12 x 20).
            BuildFloorCeiling(chamber, "ChamberFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), chamberMetal, chamberDark);

            // Chamber walls.
            BuildWall(chamber, "ChamberWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(chamber, "ChamberWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Cryo-pod structure props (pale blue).
            var podColor = new Color(0.70f, 0.80f, 0.92f);
            BuildProp(chamber, "Pod1", new Vector3(-3f, 1.3f, 8f), new Vector3(1f, 1.9f, 0.8f), podColor);
            BuildProp(chamber, "Pod2", new Vector3(0f, 1.3f, 11f), new Vector3(1f, 1.9f, 0.8f), podColor);
            BuildProp(chamber, "Pod3", new Vector3(3f, 1.3f, 14f), new Vector3(1f, 1.9f, 0.8f), podColor);

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

            // ---- Cryo-Chill Controller ----
            var cryoChill = rig.AddComponent<Ronin7.World.CryoChillController>();
            // playerHealth auto-wires in CryoChillController.Awake(); no SerializedObject needed.

            // ---- 2 HeatVent pockets ----
            var heatVentPositions = new Vector3[]
            {
                new Vector3(-4f, 0.5f, 9f),
                new Vector3(4f, 0.5f, 13f)
            };
            for (int i = 0; i < heatVentPositions.Length; i++)
            {
                var ventGo = new GameObject($"HeatVent{i}");
                ventGo.transform.SetParent(chamberGo.transform, false);
                ventGo.transform.localPosition = heatVentPositions[i];
                var ventCollider = ventGo.AddComponent<BoxCollider>();
                ventCollider.size = new Vector3(2f, 3f, 2f);
                ventCollider.isTrigger = true;
                ventGo.AddComponent<Ronin7.World.HeatVent>();
            }

            // ---- Vale StoryNpc (frozen/waking, no AllyCombatant) ----
            var valeGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            valeGo.name = "Vale";
            Object.DestroyImmediate(valeGo.GetComponent<Collider>());
            valeGo.transform.position = new Vector3(0f, 0f, 3f);
            valeGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(valeGo.GetComponent<Renderer>(), new Color(0.5f, 0.55f, 0.65f)); // steel-blue tint
            var valeNpc = valeGo.AddComponent<StoryNpc>();
            var valeSo = new SerializedObject(valeNpc);
            valeSo.FindProperty("displayName").stringValue = "Vale";
            valeSo.FindProperty("remote").boolValue = false;
            valeSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 1 Wave of 6 Defense Drones ----
            var droneColor = new Color(0.65f, 0.72f, 0.80f); // white-blue tint
            var dronePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
                new Vector3(0f, 0f, 13f)
            };

            var droneWaveHealths = new List<Health>();
            foreach (var pos in dronePositions)
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

            var droneSpawner = BuildEp03WaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { BuildEp23DialoguePlayer("Dialogue_DefenseBarks", new Vector3(0f, 1.5f, 8f), "defense_barks") });

            // ---- Dialogue Players ----
            var commanderDialogue = BuildEp23DialoguePlayer("Dialogue_TheCommander", new Vector3(0f, 1.5f, 2f), "the_commander");
            var cdSo = new SerializedObject(commanderDialogue);
            cdSo.FindProperty("playOnStart").boolValue = true;
            cdSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE UPPER DECKS".
            var decksBoxGo = BuildTransitionBox("ToDecksBox", new Vector3(0f, 1.2f, 20.5f), "TO THE UPPER DECKS",
                out var decksBtn, out var decksTransition);
            var dbSo = new SerializedObject(decksTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23DefectiveGenerationSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(decksBtn.onClick,
                new UnityEngine.Events.UnityAction(decksTransition.LoadOnFootScene));
            decksBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue the_commander (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Commander";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = commanderDialogue;

            // Step 1: DefeatWaves — 6 defense drones.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Defense Drones (6)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Prompt — transition to Defective Generation.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To the Upper Decks";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = decksBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23TheCommanderScenePath);
            EnsureScenesInBuild(Galaxy3Ep23TheCommanderScenePath, Galaxy3Ep23DefectiveGenerationScenePath);

            Debug.Log($"[Space Samurai] EP23 The Commander scene built at {Galaxy3Ep23TheCommanderScenePath}. " +
                      "Central cryo-chamber with supercooled blue-white lighting, pale blue pod props. " +
                      "CryoChillController + 2 HeatVents. " +
                      "Vale NPC (steel-blue tint, no AllyCombatant, frozen/waking state). " +
                      "6 Defense Drones (white-blue tint, nonLethal). " +
                      "3 steps: the_commander (auto) → defeat 6 drones (defense_barks bark) → to upper decks.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 Defective Generation", priority = 254)]
        public static void BuildEp23DefectiveGeneration()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Defective Generation: upper decks, neutral steel + warm accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.60f, 0.62f, 0.65f); // neutral steel key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.18f); // neutral ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.22f, 0.24f);
            RenderSettings.fogDensity = 0.016f;

            // Two accent lights: white and warm amber.
            BuildAccentPointLight("DecksLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.9f, 0.92f, 0.95f), intensity: 0.70f, range: 10f);
            BuildAccentPointLight("DecksLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.75f, 0.5f), intensity: 0.65f, range: 9f);

            // ---- Upper decks floor and structure ----
            var decksGo = new GameObject("UpperDecks");
            var decks = decksGo.transform;
            var decksMetal = new Color(0.48f, 0.50f, 0.52f);
            var decksDark = new Color(0.28f, 0.30f, 0.32f);

            // Main decks floor (12 x 21).
            BuildFloorCeiling(decks, "DecksFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), decksMetal, decksDark);

            // Decks walls.
            BuildWall(decks, "DecksWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(decks, "DecksWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Warm accent equipment props.
            var accentColor = new Color(0.85f, 0.70f, 0.55f);
            BuildProp(decks, "Equipment1", new Vector3(-3f, 1.4f, 8f), new Vector3(0.8f, 1.6f, 0.8f), accentColor);
            BuildProp(decks, "Equipment2", new Vector3(3f, 1.4f, 12f), new Vector3(0.8f, 1.6f, 0.8f), accentColor);

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

            // ---- Vale StoryNpc + AllyCombatant ----
            var valeGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            valeGo.name = "Vale";
            Object.DestroyImmediate(valeGo.GetComponent<Collider>());
            valeGo.transform.position = new Vector3(0f, 0f, 3f);
            valeGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(valeGo.GetComponent<Renderer>(), new Color(0.5f, 0.55f, 0.65f)); // steel-blue tint
            var valeNpc = valeGo.AddComponent<StoryNpc>();
            var valeSo = new SerializedObject(valeNpc);
            valeSo.FindProperty("displayName").stringValue = "Vale";
            valeSo.FindProperty("remote").boolValue = false;
            valeSo.ApplyModifiedPropertiesWithoutUndo();
            valeGo.AddComponent<Ronin7.Enemies.AllyCombatant>();

            // ---- 1 Wave of 4 Cryo-Animates ----
            var cryoColor = new Color(0.50f, 0.70f, 0.85f); // icy-blue tint
            var cryoPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(2f, 0f, 10f),
                new Vector3(-1f, 0f, 11.5f)
            };

            var cryoWaveHealths = new List<Health>();
            foreach (var pos in cryoPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, cryoColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                cryoWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var cryoSpawner = BuildEp03WaveSpawner("CryoSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { cryoWaveHealths },
                new[] { BuildEp23DialoguePlayer("Dialogue_DefectiveBarks", new Vector3(0f, 1.5f, 8f), "defective_barks") });

            // ---- Dialogue Players ----
            var defectiveDialogue = BuildEp23DialoguePlayer("Dialogue_DefectiveGeneration", new Vector3(0f, 1.5f, 2f), "defective_generation");
            var dgSo = new SerializedObject(defectiveDialogue);
            dgSo.FindProperty("playOnStart").boolValue = true;
            dgSo.ApplyModifiedPropertiesWithoutUndo();

            var defectiveAftermathDialogue = BuildEp23DialoguePlayer("Dialogue_DefectiveAftermath", new Vector3(0f, 1.5f, 14f), "defective_aftermath");

            // Transition box: "TO THE VAULT".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 21.5f), "TO THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23TheWakeSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue defective_generation (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Defective Generation";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = defectiveDialogue;

            // Step 1: DefeatWaves — 4 cryo-animates.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Cryo-Animates (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cryoSpawner;

            // Step 2: Dialogue defective_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Defective Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = defectiveAftermathDialogue;

            // Step 3: Prompt — transition to The Wake.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Vault";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23DefectiveGenerationScenePath);
            EnsureScenesInBuild(Galaxy3Ep23DefectiveGenerationScenePath, Galaxy3Ep23TheWakeScenePath);

            Debug.Log($"[Space Samurai] EP23 Defective Generation scene built at {Galaxy3Ep23DefectiveGenerationScenePath}. " +
                      "Upper decks with neutral steel + warm accent lighting, steel floor/walls, warm equipment props. " +
                      "Vale NPC + AllyCombatant (steel-blue tint, fights beside player). " +
                      "4 Cryo-Animates (icy-blue tint, nonLethal). " +
                      "4 steps: defective_generation (auto) → defeat 4 (defective_barks bark) → defective_aftermath dialogue → to vault.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 The Wake", priority = 255)]
        public static void BuildEp23TheWake()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Wake: cryo-vault revival hall, cold blue with waking-amber accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.58f, 0.68f, 0.82f); // cold blue-white key light
            light.intensity = 0.47f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.20f, 0.26f); // cool ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.22f, 0.30f, 0.40f);
            RenderSettings.fogDensity = 0.017f;

            // Two accent lights: cyan and warm amber.
            BuildAccentPointLight("WakeLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.6f, 0.85f, 1f), intensity: 0.72f, range: 10f);
            BuildAccentPointLight("WakeLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.78f, 0.55f), intensity: 0.68f, range: 9f);

            // ---- Revival hall floor and structure ----
            var hallGo = new GameObject("RevivalHall");
            var hall = hallGo.transform;
            var hallMetal = new Color(0.50f, 0.56f, 0.64f);
            var hallDark = new Color(0.30f, 0.36f, 0.44f);

            // Main hall floor (12 x 21).
            BuildFloorCeiling(hall, "HallFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), hallMetal, hallDark);

            // Hall walls.
            BuildWall(hall, "HallWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(hall, "HallWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Stasis-pod amber/revival accents.
            var stasisColor = new Color(0.88f, 0.72f, 0.52f);
            BuildProp(hall, "Stasis1", new Vector3(-3f, 1.3f, 8f), new Vector3(0.9f, 1.8f, 0.8f), stasisColor);
            BuildProp(hall, "Stasis2", new Vector3(3f, 1.3f, 12f), new Vector3(0.9f, 1.8f, 0.8f), stasisColor);

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

            // ---- Vale StoryNpc + AllyCombatant ----
            var valeGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            valeGo.name = "Vale";
            Object.DestroyImmediate(valeGo.GetComponent<Collider>());
            valeGo.transform.position = new Vector3(0f, 0f, 3f);
            valeGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(valeGo.GetComponent<Renderer>(), new Color(0.5f, 0.55f, 0.65f)); // steel-blue tint
            var valeNpc = valeGo.AddComponent<StoryNpc>();
            var valeSo = new SerializedObject(valeNpc);
            valeSo.FindProperty("displayName").stringValue = "Vale";
            valeSo.FindProperty("remote").boolValue = false;
            valeSo.ApplyModifiedPropertiesWithoutUndo();
            valeGo.AddComponent<Ronin7.Enemies.AllyCombatant>();

            // ---- 2 Waves of 3 Dominion Boarders each (6 total) ----
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

            var boarderSpawner = BuildEp03WaveSpawner("BoarderSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new[] { BuildEp23DialoguePlayer("Dialogue_BoardingBarks", new Vector3(0f, 1.5f, 8f), "boarding_barks") });

            // ---- Dialogue Players ----
            var wakeDialogue = BuildEp23DialoguePlayer("Dialogue_TheWake", new Vector3(0f, 1.5f, 2f), "the_wake");
            var wdSo = new SerializedObject(wakeDialogue);
            wdSo.FindProperty("playOnStart").boolValue = true;
            wdSo.ApplyModifiedPropertiesWithoutUndo();

            var wakeAftermathDialogue = BuildEp23DialoguePlayer("Dialogue_WakeAftermath", new Vector3(0f, 1.5f, 14f), "wake_aftermath");

            // Transition box: "TO THE DOCKING BAY".
            var bayBoxGo = BuildTransitionBox("ToBayBox", new Vector3(0f, 1.2f, 21.5f), "TO THE DOCKING BAY",
                out var bayBtn, out var bayTransition);
            var bbSo = new SerializedObject(bayTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23GhostFleetSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(bayBtn.onClick,
                new UnityEngine.Events.UnityAction(bayTransition.LoadOnFootScene));
            bayBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue the_wake (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Wake";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = wakeDialogue;

            // Step 1: DefeatWaves — 2 waves of 3 boarders.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Boarders (2 waves, 3+3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = boarderSpawner;

            // Step 2: Dialogue wake_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Wake Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = wakeAftermathDialogue;

            // Step 3: Prompt — transition to Ghost Fleet.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Docking Bay";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = bayBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23TheWakeScenePath);
            EnsureScenesInBuild(Galaxy3Ep23TheWakeScenePath, Galaxy3Ep23GhostFleetScenePath);

            Debug.Log($"[Space Samurai] EP23 The Wake scene built at {Galaxy3Ep23TheWakeScenePath}. " +
                      "Cryo-vault revival hall with cold blue + waking-amber accents, stasis-pod props. " +
                      "Vale NPC + AllyCombatant (steel-blue tint, fights beside player). " +
                      "6 Dominion Boarders (grey tint, nonLethal, 2 waves of 3). " +
                      "4 steps: the_wake (auto) → defeat 6 boarders in 2 waves (boarding_barks bark) → wake_aftermath dialogue → to docking bay.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 Ghost Fleet", priority = 256)]
        public static void BuildEp23GhostFleet()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ghost Fleet: escape corridor / hauler hold, warm-amber + danger-red accents (THE EP23 FINALE).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.60f, 0.45f); // warm-amber key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.14f, 0.10f); // warm dark ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.28f, 0.18f, 0.12f);
            RenderSettings.fogDensity = 0.018f;

            // Two accent lights: amber and danger-red.
            BuildAccentPointLight("HaulerLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.75f, 0.4f), intensity: 0.72f, range: 10f);
            BuildAccentPointLight("HaulerLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.4f, 0.35f), intensity: 0.68f, range: 9f);

            // ---- Hauler hold floor and structure ----
            var haulerGo = new GameObject("HaulerHold");
            var hauler = haulerGo.transform;
            var haulerMetal = new Color(0.50f, 0.48f, 0.45f);
            var haulerDark = new Color(0.30f, 0.28f, 0.25f);

            // Main hauler floor (12 x 21).
            BuildFloorCeiling(hauler, "HaulerFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), haulerMetal, haulerDark);

            // Hauler walls.
            BuildWall(hauler, "HaulerWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(hauler, "HaulerWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Danger-red accent hazard props.
            var hazardColor = new Color(1f, 0.35f, 0.30f);
            BuildProp(hauler, "Hazard1", new Vector3(-3f, 1.5f, 8f), new Vector3(1f, 1.8f, 0.9f), hazardColor);
            BuildProp(hauler, "Hazard2", new Vector3(3f, 1.5f, 12f), new Vector3(1f, 1.8f, 0.9f), hazardColor);

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

            // ---- 1 Wave of 3 Dominion Pursuit Units ----
            var pursuitTint = new Color(0.45f, 0.35f, 0.32f); // grey-red tint
            var pursuitPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(1.5f, 0f, 10f)
            };

            var pursuitWaveHealths = new List<Health>();
            foreach (var pos in pursuitPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, pursuitTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                pursuitWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var pursuitSpawner = BuildEp03WaveSpawner("PursuitSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { pursuitWaveHealths },
                new[] { BuildEp23DialoguePlayer("Dialogue_GhostBarks", new Vector3(0f, 1.5f, 8f), "ghost_barks") });

            // ---- Dialogue Players ----
            var ghostFleetDialogue = BuildEp23DialoguePlayer("Dialogue_GhostFleet", new Vector3(0f, 1.5f, 2f), "ghost_fleet");
            var gfSo = new SerializedObject(ghostFleetDialogue);
            gfSo.FindProperty("playOnStart").boolValue = true;
            gfSo.ApplyModifiedPropertiesWithoutUndo();

            var closingDialogue = BuildEp23DialoguePlayer("Dialogue_Closing", new Vector3(0f, 1.5f, 14f), "closing");

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 21.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set flag: ep23_complete.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep23_complete";
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

            // Step 0: Dialogue ghost_fleet (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ghost Fleet";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ghostFleetDialogue;

            // Step 1: DefeatWaves — 3 pursuit units.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Pursuit Units (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = pursuitSpawner;

            // Step 2: Dialogue closing.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Closing";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = closingDialogue;

            // Step 3: Prompt — return to space with ep23_complete flag.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep23_complete)";
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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23GhostFleetScenePath);
            EnsureScenesInBuild(Galaxy3Ep23GhostFleetScenePath);

            Debug.Log($"[Space Samurai] EP23 Ghost Fleet scene built at {Galaxy3Ep23GhostFleetScenePath}. " +
                      "Escape corridor / hauler hold with warm-amber + danger-red accents, hazard props. " +
                      "3 Dominion Pursuit Units (grey-red tint, nonLethal). " +
                      "4 steps: ghost_fleet (auto) → defeat 3 pursuit (ghost_barks bark) → closing dialogue → " +
                      "return to stars Prompt (sets ep23_complete via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 23 FINALE (sets ep23_complete).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP23 Scenes", priority = 257)]
        public static void BuildAllEp23Scenes()
        {
            BuildEp23SignalApproach();
            BuildEp23IceBelow();
            BuildEp23CryptBelow();
            BuildEp23TheCommander();
            BuildEp23DefectiveGeneration();
            BuildEp23TheWake();
            BuildEp23GhostFleet();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP23 scenes built + inputs rewired.");
        }
    }
}

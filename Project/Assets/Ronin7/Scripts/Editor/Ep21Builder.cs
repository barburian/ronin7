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
    /// EP21 "The Crimson Sleep" scene builders for the first three episodes. Builds three on-foot scenes:
    /// - Narcosis Descent: purple/magenta pollen bay with 3 waves of Crimson Lotus enforcers (2 each)
    /// - Forgetting Descent: safe-house dreamstate with 3 self-copy DreamPhantom enemies, PollenHazeController
    /// - Garden of Echoes: greenhouse of glowing lotus blooms with 4 illusory Khall-swarm DreamPhantoms (dissolved by DreamReckoningTrigger)
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP21 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep21" and loads lines from Ep21Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp21DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep21Lines.Get(setId), advanceRef, setId, clipPrefix: "ep21");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Narcosis Descent", priority = 220)]
        public static void BuildEp21NarcosisDescent()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Narcosis Descent: purple/magenta pollen bay.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.4f, 0.75f); // purple/magenta key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.10f, 0.20f); // purple ambient

            // Dense fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.18f, 0.34f);
            RenderSettings.fogDensity = 0.022f;

            // Two accent lights: magenta and amber.
            BuildAccentPointLight("NarcosisLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.30f, 0.70f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("NarcosisLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.65f, 0.30f), intensity: 0.70f, range: 9f);

            // ---- Narcosis bay floor and structure ----
            var bayGo = new GameObject("NarcosisBay");
            var bay = bayGo.transform;
            var purpleMetal = new Color(0.50f, 0.32f, 0.42f);
            var darkerPurple = new Color(0.30f, 0.18f, 0.26f);

            // Main pollen bay floor (12 x 20).
            BuildFloorCeiling(bay, "NarcosisFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), purpleMetal, darkerPurple);

            // Pollen bay walls.
            BuildWall(bay, "NarcosisWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(bay, "NarcosisWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Scaffolding prop.
            BuildProp(bay, "Scaffolding", new Vector3(-3f, 1.2f, 8f), new Vector3(1.5f, 2f, 1f), new Color(0.45f, 0.28f, 0.38f));

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

            // ---- 3 Waves of Crimson Lotus Enforcers (2 + 2 + 2) ----
            var lotusColor = new Color(0.75f, 0.30f, 0.45f);

            // Wave 1: 2 enforcers.
            var wave1Healths = new List<Health>();
            var wave1Positions = new Vector3[] { new Vector3(-1.5f, 0f, 5f), new Vector3(1.5f, 0f, 5f) };
            foreach (var pos in wave1Positions)
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
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 2 enforcers.
            var wave2Healths = new List<Health>();
            var wave2Positions = new Vector3[] { new Vector3(-1.5f, 0f, 8f), new Vector3(1.5f, 0f, 8f) };
            foreach (var pos in wave2Positions)
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
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 3: 2 enforcers.
            var wave3Healths = new List<Health>();
            var wave3Positions = new Vector3[] { new Vector3(-1.5f, 0f, 11f), new Vector3(1.5f, 0f, 11f) };
            foreach (var pos in wave3Positions)
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
                wave3Healths.Add(enemy.GetComponent<Health>());
            }

            var lotusSpawner = BuildWaveSpawner("LotusSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths, wave3Healths },
                new[] { BuildEp21DialoguePlayer("Dialogue_DescentBarks", new Vector3(0f, 1.5f, 8f), "descent_barks") });

            // ---- Dialogue Players ----
            var facesHeturnsFromDialogue = BuildEp21DialoguePlayer("Dialogue_FacesHeTurnsFrom", new Vector3(0f, 1.5f, 2f), "faces_he_turns_from");
            var fhtfSo = new SerializedObject(facesHeturnsFromDialogue);
            fhtfSo.FindProperty("playOnStart").boolValue = true;
            fhtfSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE SAFE HOUSE".
            var safeHouseBoxGo = BuildTransitionBox("ToSafeHouseBox", new Vector3(0f, 1.2f, 20.5f), "TO THE SAFE HOUSE",
                out var safeHouseBtn, out var safeHouseTransition);
            var shbSo = new SerializedObject(safeHouseTransition);
            shbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21ForgettingDescentSceneName;
            shbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(safeHouseBtn.onClick,
                new UnityEngine.Events.UnityAction(safeHouseTransition.LoadOnFootScene));
            safeHouseBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue faces_he_turns_from (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Faces He Turns From";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = facesHeturnsFromDialogue;

            // Step 1: DefeatWaves — 3 waves of lotus enforcers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Crimson Lotus Enforcers (3 waves)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = lotusSpawner;

            // Step 2: Prompt — transition to Safe House.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Move to Safe House";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = safeHouseBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21NarcosisDescentScenePath);
            EnsureScenesInBuild(Galaxy3Ep21NarcosisDescentScenePath, Galaxy3Ep21ForgettingDescentScenePath);

            Debug.Log($"[Space Samurai] EP21 Narcosis Descent scene built at {Galaxy3Ep21NarcosisDescentScenePath}. " +
                      "Layout: purple/magenta pollen bay with dense purple fog, dark metal floor/walls, scaffolding prop, magenta+amber accent lights. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "3 waves of Crimson Lotus Enforcers (crimson-purple tint, nonLethal, 2+2+2). " +
                      "3 steps: faces_he_turns_from (auto) → defeat 3 waves (descent_barks bark) → transition to Safe House.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Forgetting Descent", priority = 221)]
        public static void BuildEp21ForgettingDescent()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Forgetting Descent: safe-house dreamstate.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.4f, 0.75f); // purple/magenta key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.10f, 0.20f); // purple ambient

            // Denser fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.18f, 0.34f);
            RenderSettings.fogDensity = 0.026f;

            // Two accent lights.
            BuildAccentPointLight("ForgettingLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.30f, 0.70f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ForgettingLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.65f, 0.30f), intensity: 0.70f, range: 9f);

            // ---- Safe house floor and structure ----
            var safeGo = new GameObject("SafeHouse");
            var safe = safeGo.transform;
            var purpleMetal = new Color(0.50f, 0.32f, 0.42f);
            var darkerPurple = new Color(0.30f, 0.18f, 0.26f);

            // Main floor (10 x 18).
            BuildFloorCeiling(safe, "SafeHouseFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 18f), purpleMetal, darkerPurple);

            // Walls.
            BuildWall(safe, "SafeHouseWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(safe, "SafeHouseWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

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

            // ---- Dreamscape ----
            var dreamscapeGo = new GameObject("Dreamscape");
            dreamscapeGo.AddComponent<PollenHazeController>();

            // ---- 3 Self-Copy DreamPhantom Enemies (1 wave) ----
            var cipherTint = new Color(0.5f, 0.5f, 0.6f); // steel

            var selfCopyPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };

            var selfCopyHealths = new List<Health>();
            foreach (var pos in selfCopyPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, cipherTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                var dp = enemy.gameObject.AddComponent<DreamPhantom>();
                // NOT illusory — they are killable copies
                selfCopyHealths.Add(enemy.GetComponent<Health>());
            }

            var selfCopySpawner = BuildWaveSpawner("SelfCopySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { selfCopyHealths },
                new[] { BuildEp21DialoguePlayer("Dialogue_SelfCopyBarks", new Vector3(0f, 1.5f, 8f), "self_copy_barks") });

            // ---- Dialogue Players ----
            var kadeConfessionDialogue = BuildEp21DialoguePlayer("Dialogue_KadeConfession", new Vector3(0f, 1.5f, 2f), "kade_confession");
            var kcSo = new SerializedObject(kadeConfessionDialogue);
            kcSo.FindProperty("playOnStart").boolValue = true;
            kcSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "DESCEND — THE GARDEN".
            var gardenBoxGo = BuildTransitionBox("ToGardenBox", new Vector3(0f, 1.2f, 18.5f), "DESCEND — THE GARDEN",
                out var gardenBtn, out var gardenTransition);
            var gbSo = new SerializedObject(gardenTransition);
            gbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21GardenOfEchoesSceneName;
            gbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gardenBtn.onClick,
                new UnityEngine.Events.UnityAction(gardenTransition.LoadOnFootScene));
            gardenBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue kade_confession (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Kade Confession";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = kadeConfessionDialogue;

            // Step 1: DefeatWaves — 3 self-copy phantoms.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Self-Copy DreamPhantoms (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = selfCopySpawner;

            // Step 2: Prompt — transition to Garden.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Descend to Garden";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = gardenBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21ForgettingDescentScenePath);
            EnsureScenesInBuild(Galaxy3Ep21ForgettingDescentScenePath, Galaxy3Ep21GardenOfEchoesScenePath);

            Debug.Log($"[Space Samurai] EP21 Forgetting Descent scene built at {Galaxy3Ep21ForgettingDescentScenePath}. " +
                      "Layout: safe-house dreamstate with denser purple fog, dark metal floor/walls, PollenHazeController on Dreamscape. " +
                      "3 Self-Copy DreamPhantom enemies (Cipher steel tint, nonLethal, killable, NOT illusory). " +
                      "3 steps: kade_confession (auto) → defeat 3 phantoms (self_copy_barks bark) → descend to garden.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Garden of Echoes", priority = 222)]
        public static void BuildEp21GardenOfEchoes()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Garden of Echoes: greenhouse of glowing lotus blooms.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.45f, 0.70f); // magenta-heavy key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.12f, 0.22f); // purple ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.20f, 0.36f);
            RenderSettings.fogDensity = 0.024f;

            // Two accent lights.
            BuildAccentPointLight("GardenLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.30f, 0.70f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("GardenLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.9f, 0.70f, 1f), intensity: 0.75f, range: 9f);

            // ---- Garden floor and structure ----
            var gardenGo = new GameObject("Garden");
            var garden = gardenGo.transform;
            var greenMetal = new Color(0.48f, 0.35f, 0.45f);
            var darkerGreen = new Color(0.28f, 0.20f, 0.28f);

            // Main floor (11 x 19).
            BuildFloorCeiling(garden, "GardenFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 19f), greenMetal, darkerGreen);

            // Walls.
            BuildWall(garden, "GardenWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));
            BuildWall(garden, "GardenWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));

            // Unlit magenta cube "bloom" props.
            var bloomColor = new Color(1f, 0.3f, 0.7f);
            BuildProp(garden, "Bloom1", new Vector3(-2f, 1.5f, 6f), new Vector3(0.8f, 1.2f, 0.8f), bloomColor);
            BuildProp(garden, "Bloom2", new Vector3(2f, 1.5f, 8f), new Vector3(0.8f, 1.2f, 0.8f), bloomColor);
            BuildProp(garden, "Bloom3", new Vector3(-1.5f, 1.5f, 12f), new Vector3(0.8f, 1.2f, 0.8f), bloomColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Dreamscape ----
            var dreamscapeGo = new GameObject("Dreamscape");
            dreamscapeGo.AddComponent<PollenHazeController>();

            // ---- 4 Illusory Khall-swarm DreamPhantom enemies ----
            var khallTint = new Color(0.4f, 0.35f, 0.5f); // dark cultured

            var khallPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 8f),
                new Vector3(-1f, 0f, 10f),
                new Vector3(1f, 0f, 10f)
            };

            var khallPhantoms = new List<DreamPhantom>();
            foreach (var pos in khallPositions)
            {
                var khallGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                khallGo.name = "Khall Echo";
                Object.DestroyImmediate(khallGo.GetComponent<Collider>());
                khallGo.transform.position = pos;
                khallGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
                TintShared(khallGo.GetComponent<Renderer>(), khallTint);
                khallGo.SetActive(true); // Illusory phantoms stay active

                var dp = khallGo.AddComponent<DreamPhantom>();
                dp.SetIllusory(true);
                khallPhantoms.Add(dp);
            }

            // ---- DreamReckoningTrigger ----
            var reckoningTriggerGo = new GameObject("ReckoningTrigger");
            var reckoningTrigger = reckoningTriggerGo.AddComponent<DreamReckoningTrigger>();
            var rtSo = new SerializedObject(reckoningTrigger);
            var phantomsProp = rtSo.FindProperty("phantoms");
            phantomsProp.arraySize = khallPhantoms.Count;
            for (int i = 0; i < khallPhantoms.Count; i++)
            {
                phantomsProp.GetArrayElementAtIndex(i).objectReferenceValue = khallPhantoms[i];
            }
            rtSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var khallEchoesDialogue = BuildEp21DialoguePlayer("Dialogue_KhallEchoes", new Vector3(0f, 1.5f, 2f), "khall_echoes");
            var keDialogueSo = new SerializedObject(khallEchoesDialogue);
            keDialogueSo.FindProperty("playOnStart").boolValue = true;
            keDialogueSo.ApplyModifiedPropertiesWithoutUndo();

            var reckoningDialogue = BuildEp21DialoguePlayer("Dialogue_Reckoning", new Vector3(0f, 1.5f, 12f), "reckoning");

            // Reckoning prompt box: "ACKNOWLEDGE — YOU ARE ME" with dual button listeners.
            var reckoningBoxGo = BuildTransitionBox("AcknowledgeBox", new Vector3(0f, 1.2f, 18.5f), "ACKNOWLEDGE — YOU ARE ME",
                out var reckoningBtn, out var reckoningTransition);
            var rbSo = new SerializedObject(reckoningTransition);
            rbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21TestimonySceneName;
            rbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(reckoningBtn.onClick,
                new UnityEngine.Events.UnityAction(reckoningTrigger.Acknowledge));
            UnityEventTools.AddPersistentListener(reckoningBtn.onClick,
                new UnityEngine.Events.UnityAction(reckoningTransition.LoadOnFootScene));
            reckoningBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue khall_echoes (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Echoes";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallEchoesDialogue;

            // Step 1: Dialogue reckoning.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Reckoning";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = reckoningDialogue;

            // Step 2: Prompt — reckoning box (dissolves phantoms and transitions).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Acknowledge (dissolve phantoms, move to Testimony)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = reckoningBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21GardenOfEchoesScenePath);
            EnsureScenesInBuild(Galaxy3Ep21GardenOfEchoesScenePath, Galaxy3Ep21TestimonyScenePath);

            Debug.Log($"[Space Samurai] EP21 Garden of Echoes scene built at {Galaxy3Ep21GardenOfEchoesScenePath}. " +
                      "Layout: greenhouse of glowing lotus blooms with magenta-heavy lighting, dark metal floor/walls, unlit magenta bloom props, PollenHazeController on Dreamscape. " +
                      "4 Illusory Khall-swarm DreamPhantoms (dark cultured tint, SetIllusory=true, NO Health). " +
                      "DreamReckoningTrigger with 4 phantoms in list. " +
                      "3 steps: khall_echoes (auto) → reckoning dialogue → acknowledge prompt (triggers reckoningTrigger.Acknowledge + transitions to Testimony).");
        }
    }
}

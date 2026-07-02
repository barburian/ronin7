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
    /// EP14 "The Echo Protocol" on-foot scene builders. Builds five core episodes:
    /// - DockingRing: cold blue vacuum corridor with automated security drones
    /// - Observation: dim utilitarian station interior with observation platform
    /// - ArchiveDefense: enclosed archive level with EchoHunter mechanic drones
    /// - Revelation: command platform with central glowing Verity core
    /// - Siege: collapsing corridor with red emergency light, final reach-to-collar escape
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP14 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep14" and loads lines from Ep14Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp14DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep14Lines.Get(setId), advanceRef, setId, clipPrefix: "ep14");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Docking Ring", priority = 142)]
        public static void BuildEp14DockingRing()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Docking Ring: cold blue vacuum corridor with dim directional light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.40f, 0.55f, 0.70f); // cold blue key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.25f, 0.32f); // dark blue ambient

            // Light blue exponential fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.40f, 0.50f);
            RenderSettings.fogDensity = 0.022f;

            // Two cold-blue accent point lights.
            BuildAccentPointLight("DockingLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.3f, 0.65f, 0.85f), intensity: 0.9f, range: 11f);
            BuildAccentPointLight("DockingLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.25f, 0.60f, 0.80f), intensity: 0.85f, range: 10f);

            // ---- Corridor floor and walls ----
            var corridorGo = new GameObject("Corridor");
            var corridor = corridorGo.transform;
            var darkMetal = new Color(0.25f, 0.28f, 0.32f);
            var darkerMetal = new Color(0.15f, 0.18f, 0.22f);

            // Main corridor floor.
            BuildFloorCeiling(corridor, "CorridorFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), darkMetal, darkerMetal);

            // Narrow corridor walls.
            BuildWall(corridor, "CorridorWall_W", new Vector3(-3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(corridor, "CorridorWall_E", new Vector3(3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

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

            // ---- Dialogue Players ----
            var transitIntroDialogue = BuildEp14DialoguePlayer("Dialogue_TransitIntro", new Vector3(0f, 1.5f, 2f), "transit_intro");
            var transitDlgSo = new SerializedObject(transitIntroDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var approachMercerDialogue = BuildEp14DialoguePlayer("Dialogue_ApproachMercer", new Vector3(0f, 1.5f, 4f), "approach_mercer");

            // ---- 4 Security Drone enemies: 1 wave ----
            var droneColor = new Color(0.45f, 0.48f, 0.52f); // metallic grey
            var droneWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(1.5f, 0f, 6f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f)
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

            var droneSpawner = BuildEp03WaveSpawner("SecuritySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { BuildEp14DialoguePlayer("Dialogue_SecurityBarks", new Vector3(0f, 1.5f, 8f), "security_barks") });

            // Transition box: "ENTER — THE INNER AIRLOCK".
            var airlockBoxGo = BuildTransitionBox("ToObservationBox", new Vector3(0f, 1.2f, 20.5f), "ENTER — THE INNER AIRLOCK",
                out var airlockBtn, out var airlockTransition);
            var abSo = new SerializedObject(airlockTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy2Ep14ObservationSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(airlockBtn.onClick,
                new UnityEngine.Events.UnityAction(airlockTransition.LoadOnFootScene));
            airlockBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue transit_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Transit Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = transitIntroDialogue;

            // Step 1: Dialogue approach_mercer.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Approach Mercer";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = approachMercerDialogue;

            // Step 2: DefeatWaves — 4 Security Drones.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Security Drones (4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 3: Prompt — transition to Observation.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Enter Inner Airlock";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = airlockBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14DockingRingScenePath);
            EnsureScenesInBuild(Galaxy2Ep14DockingRingScenePath, Galaxy2Ep14ObservationScenePath);

            Debug.Log($"[Space Samurai] EP14 Docking Ring scene built at {Galaxy2Ep14DockingRingScenePath}. " +
                      "Layout: cold blue vacuum corridor with dim blue lighting and narrow floor/walls. " +
                      "4 Security Drones (metallic grey, nonLethal). " +
                      "4 steps: transit_intro (auto) → approach_mercer dialogue → defeat 4 Security Drones (security_barks bark) → " +
                      "transition to Observation.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Observation", priority = 143)]
        public static void BuildEp14Observation()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Observation: dim utilitarian station interior with cool grey lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.58f, 0.62f); // cool grey key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.28f); // cool dim ambient

            // Cool grey utilitarian fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.35f, 0.40f);
            RenderSettings.fogDensity = 0.020f;

            // Two dim accent lights.
            BuildAccentPointLight("ObservationLight1", new Vector3(-2.5f, 2f, 8f),
                new Color(0.50f, 0.55f, 0.62f), intensity: 0.65f, range: 9f);
            BuildAccentPointLight("ObservationLight2", new Vector3(2.5f, 2.5f, 12f),
                new Color(0.48f, 0.52f, 0.58f), intensity: 0.60f, range: 8f);

            // ---- Observation platform ----
            var platformGo = new GameObject("ObservationPlatform");
            var platform = platformGo.transform;
            var platformMetal = new Color(0.40f, 0.42f, 0.46f);
            var platformDark = new Color(0.28f, 0.30f, 0.34f);

            // Main platform floor.
            BuildFloorCeiling(platform, "PlatformFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), platformMetal, platformDark);

            // Central raised observation prop.
            BuildProp(platform, "ObservationRaiser", new Vector3(0f, 0.8f, 10f), new Vector3(6f, 0.4f, 6f), platformMetal);

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

            // ---- Dialogue Players ----
            var verityIntroDialogue = BuildEp14DialoguePlayer("Dialogue_VerityIntro", new Vector3(0f, 1.5f, 2f), "verity_intro");
            var verityDlgSo = new SerializedObject(verityIntroDialogue);
            verityDlgSo.FindProperty("playOnStart").boolValue = true;
            verityDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var kethelBriefingDialogue = BuildEp14DialoguePlayer("Dialogue_KethelBriefing", new Vector3(0f, 1.5f, 8f), "kethel7_briefing");

            // Transition box: "PROCEED — DEEPER INTO THE ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 20.5f), "PROCEED — DEEPER INTO THE ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var arSo = new SerializedObject(archiveTransition);
            arSo.FindProperty("onFootScene").stringValue = Galaxy2Ep14ArchiveDefenseSceneName;
            arSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue verity_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Verity Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = verityIntroDialogue;

            // Step 1: Dialogue kethel7_briefing.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Kethel Briefing";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = kethelBriefingDialogue;

            // Step 2: Prompt — transition to Archive Defense.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Proceed Into Archive";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14ObservationScenePath);
            EnsureScenesInBuild(Galaxy2Ep14ObservationScenePath, Galaxy2Ep14ArchiveDefenseScenePath);

            Debug.Log($"[Space Samurai] EP14 Observation scene built at {Galaxy2Ep14ObservationScenePath}. " +
                      "Layout: dim utilitarian station interior with cool grey lighting and central raised observation platform. " +
                      "No enemies. " +
                      "3 steps: verity_intro (auto) → kethel7_briefing dialogue → transition to Archive Defense.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Archive Defense", priority = 144)]
        public static void BuildEp14ArchiveDefense()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive Defense: enclosed archive level with cold blue lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.35f, 0.50f, 0.65f); // cold blue key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.20f, 0.28f); // dark blue ambient

            // Cold blue archive fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.30f, 0.40f);
            RenderSettings.fogDensity = 0.025f;

            // Two cold-blue accent lights.
            BuildAccentPointLight("ArchiveLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.3f, 0.65f, 0.85f), intensity: 0.8f, range: 10f);
            BuildAccentPointLight("ArchiveLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.25f, 0.60f, 0.80f), intensity: 0.75f, range: 9f);

            // ---- Archive level with server racks ----
            var archiveGo = new GameObject("Archive");
            var archive = archiveGo.transform;
            var archiveMetal = new Color(0.35f, 0.37f, 0.42f);
            var archiveDark = new Color(0.22f, 0.24f, 0.28f);

            // Main archive floor.
            BuildFloorCeiling(archive, "ArchiveFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), archiveMetal, archiveDark);

            // Archive enclosure walls.
            BuildWall(archive, "ArchiveWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(archive, "ArchiveWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // ---- Cyan data-crystal glow cubes (unlit) ----
            var crystallColor = new Color(0.2f, 0.9f, 1f); // cyan
            var crystalCount = 8;
            for (int i = 0; i < crystalCount; i++)
            {
                float x = (i % 4 - 1.5f) * 3f;
                float y = 1.5f + (i / 4) * 1.2f;
                float z = 4f + (i % 2) * 4f;
                var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = $"DataCrystal_{i}";
                Object.DestroyImmediate(crystal.GetComponent<Collider>());
                crystal.transform.SetParent(archive, false);
                crystal.transform.position = new Vector3(x, y, z);
                crystal.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                crystal.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(crystallColor);
            }

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

            // ---- 6 EchoHunter drone enemies: 1 wave ----
            var hunterColor = new Color(0.25f, 0.30f, 0.40f); // dark blue-steel
            var hunterWavePositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 5f),
                new Vector3(2.5f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8f),
                new Vector3(-2f, 0f, 11f),
                new Vector3(2f, 0f, 11f)
            };

            var hunterWaveHealths = new List<Health>();
            foreach (var pos in hunterWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hunterColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }

                // Add EchoHunter component and wire flashRenderer.
                var echoSo = new SerializedObject(enemy.gameObject.AddComponent<EchoHunter>());
                SetObjectRef(echoSo, "flashRenderer", enemy.GetComponent<Renderer>());
                echoSo.ApplyModifiedPropertiesWithoutUndo();

                enemy.gameObject.SetActive(false);
                hunterWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var hunterSpawner = BuildEp03WaveSpawner("HunterSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { hunterWaveHealths },
                new[] { BuildEp14DialoguePlayer("Dialogue_HunterBarks", new Vector3(0f, 1.5f, 8f), "hunter_barks") });

            // Transition box: "ADVANCE — THE COMMAND PLATFORM".
            var revelationBoxGo = BuildTransitionBox("ToRevelationBox", new Vector3(0f, 1.2f, 18.5f), "ADVANCE — THE COMMAND PLATFORM",
                out var revelationBtn, out var revelationTransition);
            var rvSo = new SerializedObject(revelationTransition);
            rvSo.FindProperty("onFootScene").stringValue = Galaxy2Ep14RevelationSceneName;
            rvSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(revelationBtn.onClick,
                new UnityEngine.Events.UnityAction(revelationTransition.LoadOnFootScene));
            revelationBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: DefeatWaves — 6 EchoHunter drones.
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: EchoHunter Drones (6)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = hunterSpawner;

            // Step 1: Prompt — transition to Revelation.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Advance to Command Platform";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = revelationBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14ArchiveDefenseScenePath);
            EnsureScenesInBuild(Galaxy2Ep14ArchiveDefenseScenePath, Galaxy2Ep14RevelationScenePath);

            Debug.Log($"[Space Samurai] EP14 Archive Defense scene built at {Galaxy2Ep14ArchiveDefenseScenePath}. " +
                      "Layout: enclosed archive level with cold blue lighting, server-rack props, and cyan data-crystal glow cubes. " +
                      "6 EchoHunter drones (dark blue-steel, nonLethal, EchoHunter component with flashRenderer wired). " +
                      "2 steps: defeat 6 EchoHunter drones (hunter_barks bark) → transition to Revelation.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Revelation", priority = 145)]
        public static void BuildEp14Revelation()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Revelation: command platform with central glowing Verity core.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.55f, 0.62f); // cool grey-blue key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.28f); // dark cool ambient

            // Cool revelation fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.32f, 0.42f);
            RenderSettings.fogDensity = 0.020f;

            // Two dim accent lights.
            BuildAccentPointLight("RevelationLight1", new Vector3(-2f, 2f, 8f),
                new Color(0.45f, 0.55f, 0.68f), intensity: 0.7f, range: 10f);
            BuildAccentPointLight("RevelationLight2", new Vector3(2f, 2.5f, 12f),
                new Color(0.40f, 0.50f, 0.65f), intensity: 0.65f, range: 9f);

            // ---- Command platform ----
            var platformGo = new GameObject("CommandPlatform");
            var platform = platformGo.transform;
            var commandMetal = new Color(0.38f, 0.40f, 0.45f);
            var commandDark = new Color(0.25f, 0.27f, 0.32f);

            // Main command floor.
            BuildFloorCeiling(platform, "CommandFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), commandMetal, commandDark);

            // ---- Central Verity core: unlit cyan-white cube ----
            var verityCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            verityCore.name = "VerityCore";
            Object.DestroyImmediate(verityCore.GetComponent<Collider>());
            verityCore.transform.SetParent(platform, false);
            verityCore.transform.position = new Vector3(0f, 1.5f, 10f);
            verityCore.transform.localScale = new Vector3(1.2f, 1.8f, 1.2f);
            verityCore.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.8f, 1f, 1f)); // eerie cyan-white

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

            // ---- Dialogue Players ----
            var verityConfessionDialogue = BuildEp14DialoguePlayer("Dialogue_VerityConfession", new Vector3(0f, 1.5f, 4f), "verity_confession");
            var confessionDlgSo = new SerializedObject(verityConfessionDialogue);
            confessionDlgSo.FindProperty("playOnStart").boolValue = true;
            confessionDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var dominionInboundDialogue = BuildEp14DialoguePlayer("Dialogue_DominionInbound", new Vector3(0f, 1.5f, 8f), "dominion_inbound");
            var khallChannelDialogue = BuildEp14DialoguePlayer("Dialogue_KhallChannel", new Vector3(0f, 1.5f, 12f), "khall_channel");

            // Transition box: "TO THE DEFENSE GRID — HOLD THE STATION".
            var siegeBoxGo = BuildTransitionBox("ToSiegeBox", new Vector3(0f, 1.2f, 20.5f), "TO THE DEFENSE GRID — HOLD THE STATION",
                out var siegeBtn, out var siegeTransition);
            var sgSo = new SerializedObject(siegeTransition);
            sgSo.FindProperty("onFootScene").stringValue = Galaxy2Ep14SiegeSceneName;
            sgSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(siegeBtn.onClick,
                new UnityEngine.Events.UnityAction(siegeTransition.LoadOnFootScene));
            siegeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue verity_confession (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Verity Confession";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = verityConfessionDialogue;

            // Step 1: Dialogue dominion_inbound.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Dominion Inbound";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = dominionInboundDialogue;

            // Step 2: Dialogue khall_channel.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Khall Channel";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = khallChannelDialogue;

            // Step 3: Prompt — transition to Siege.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To Defense Grid";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = siegeBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14RevelationScenePath);
            EnsureScenesInBuild(Galaxy2Ep14RevelationScenePath, Galaxy2Ep14SiegeScenePath);

            Debug.Log($"[Space Samurai] EP14 Revelation scene built at {Galaxy2Ep14RevelationScenePath}. " +
                      "Layout: command platform / observation deck with central unlit cyan-white Verity core. " +
                      "No enemies. " +
                      "4 steps: verity_confession (auto) → dominion_inbound dialogue → khall_channel dialogue → transition to Siege.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Siege", priority = 146)]
        public static void BuildEp14Siege()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Siege: collapsing corridor with red emergency light and dark warm fog.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.45f, 0.35f); // warm-dim key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -15f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.15f, 0.12f); // dark warm ambient

            // Dark warm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.25f, 0.18f);
            RenderSettings.fogDensity = 0.028f;

            // Red emergency light accent.
            BuildAccentPointLight("SiegeEmergency", new Vector3(0f, 2.5f, 10f),
                new Color(1f, 0.2f, 0.2f), intensity: 1.3f, range: 16f);

            // ---- Collapsing corridor ----
            var siegeGo = new GameObject("SiegeCorridor");
            var siege = siegeGo.transform;
            var siegeDark = new Color(0.32f, 0.28f, 0.24f);
            var siegeBlack = new Color(0.18f, 0.15f, 0.12f);

            // Corridor floor.
            BuildFloorCeiling(siege, "SiegeFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), siegeDark, siegeBlack);

            // Corridor walls.
            BuildWall(siege, "SiegeWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(siege, "SiegeWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Scattered debris props.
            var debrisColor = new Color(0.45f, 0.40f, 0.35f);
            BuildProp(siege, "Debris1", new Vector3(-2f, 0.5f, 5f), new Vector3(0.6f, 0.3f, 0.7f), debrisColor);
            BuildProp(siege, "Debris2", new Vector3(2f, 0.5f, 7f), new Vector3(0.7f, 0.3f, 0.6f), debrisColor);
            BuildProp(siege, "Debris3", new Vector3(-1.5f, 0.5f, 12f), new Vector3(0.5f, 0.3f, 0.8f), debrisColor);
            BuildProp(siege, "Debris4", new Vector3(1.5f, 0.5f, 15f), new Vector3(0.6f, 0.3f, 0.5f), debrisColor);

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

            // ---- Dialogue Player ----
            var siegeBarksDialogue = BuildEp14DialoguePlayer("Dialogue_SiegeBarks", new Vector3(0f, 1.5f, 10f), "siege_barks");
            var barksDialogSo = new SerializedObject(siegeBarksDialogue);
            barksDialogSo.FindProperty("playOnStart").boolValue = true;
            barksDialogSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Docking Collar Reach Point (far end of corridor) ----
            var reachPointGo = new GameObject("DockingCollarReachPoint");
            reachPointGo.transform.position = new Vector3(0f, 0.5f, 16f);

            // Transition box: "BOARD — ESCAPE DEEP STATION MERCER".
            var escapeBoxGo = BuildTransitionBox("ToEscapeBox", new Vector3(0f, 1.2f, 18.5f), "BOARD — ESCAPE DEEP STATION MERCER",
                out var escapeBtn, out var escapeTransition);
            var ebSo = new SerializedObject(escapeTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy2Ep14EscapeSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(escapeBtn.onClick,
                new UnityEngine.Events.UnityAction(escapeTransition.LoadOnFootScene));
            escapeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue siege_barks (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Siege Barks";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = siegeBarksDialogue;

            // Step 1: ReachTrigger — docking collar reach point.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Docking Collar";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = reachPointGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Prompt — transition to Escape.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Board Escape";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = escapeBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14SiegeScenePath);
            EnsureScenesInBuild(Galaxy2Ep14SiegeScenePath, Galaxy2Ep14EscapeScenePath);

            Debug.Log($"[Space Samurai] EP14 Siege scene built at {Galaxy2Ep14SiegeScenePath}. " +
                      "Layout: collapsing corridor with red emergency light and dark warm fog, scattered debris props. " +
                      "No melee enemies (run-to-collar beat). " +
                      "3 steps: siege_barks (auto) → reach docking collar (reachRadius 3f) → transition to Escape.");
        }
    }
}

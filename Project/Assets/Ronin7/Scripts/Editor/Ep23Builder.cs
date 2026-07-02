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
    /// EP23 "Thermopause" scene builders for the first three scenes at a derelict cryo-outpost.
    /// - Signal Approach: docking corridor with 1 Corrupted Salvage Drone
    /// - Ice Below: ice storm surface with 2 Sentinel Platforms + CryoChillController + 3 HeatVents
    /// - Crypt Below: cryo-vault catwalks with 2 cryo-animates + CryoChillController + 2 HeatVents
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP23 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep23" and loads lines from Ep23Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp23DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep23Lines.Get(setId), advanceRef, setId, clipPrefix: "ep23");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 Signal Approach", priority = 250)]
        public static void BuildEp23SignalApproach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Signal Approach: cold steel-blue docking corridor, dim blue lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.50f, 0.60f); // cool steel-blue key light
            light.intensity = 0.40f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.18f); // cool blue ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.22f, 0.28f);
            RenderSettings.fogDensity = 0.018f;

            // Two accent lights: steel-blue and dim cyan.
            BuildAccentPointLight("DockLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.5f, 0.7f, 0.9f), intensity: 0.65f, range: 10f);
            BuildAccentPointLight("DockLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.4f, 0.6f, 0.8f), intensity: 0.60f, range: 9f);

            // ---- Docking corridor floor and structure ----
            var dockGo = new GameObject("DockingCorridor");
            var dock = dockGo.transform;
            var steelLight = new Color(0.42f, 0.45f, 0.50f);
            var steelDark = new Color(0.22f, 0.25f, 0.30f);

            // Main docking floor (10 x 20).
            BuildFloorCeiling(dock, "DockFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), steelLight, steelDark);

            // Docking walls.
            BuildWall(dock, "DockWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(dock, "DockWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Steel panel props.
            var steelPanel = new Color(0.50f, 0.52f, 0.55f);
            BuildProp(dock, "Panel1", new Vector3(-2f, 1.5f, 8f), new Vector3(0.8f, 2.2f, 0.6f), steelPanel);
            BuildProp(dock, "Panel2", new Vector3(2f, 1.5f, 12f), new Vector3(0.8f, 2.2f, 0.6f), steelPanel);
            BuildProp(dock, "Panel3", new Vector3(-2f, 1.5f, 16f), new Vector3(0.8f, 2.2f, 0.6f), steelPanel);

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

            // ---- 1 Wave of 1 Corrupted Salvage Drone ----
            var droneColor = new Color(0.35f, 0.40f, 0.30f); // sickly green-grey tint
            var dronePositions = new Vector3[]
            {
                new Vector3(0f, 0f, 8f)
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
                new[] { BuildEp23DialoguePlayer("Dialogue_DroneBarks", new Vector3(0f, 1.5f, 8f), "drone_barks") });

            // ---- Dialogue Players ----
            var signalDialogue = BuildEp23DialoguePlayer("Dialogue_Signal", new Vector3(0f, 1.5f, 2f), "signal");
            var sdSo = new SerializedObject(signalDialogue);
            sdSo.FindProperty("playOnStart").boolValue = true;
            sdSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "DESCEND TO THE ICE".
            var iceBoxGo = BuildTransitionBox("DescentBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND TO THE ICE",
                out var iceBtn, out var iceTransition);
            var ibSo = new SerializedObject(iceTransition);
            ibSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23IceBelowSceneName;
            ibSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(iceBtn.onClick,
                new UnityEngine.Events.UnityAction(iceTransition.LoadOnFootScene));
            iceBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue signal (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Signal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = signalDialogue;

            // Step 1: DefeatWaves — 1 corrupted drone.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Corrupted Salvage Drone (1)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Prompt — transition to Ice Below.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Descend to the Ice";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = iceBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23SignalApproachScenePath);
            EnsureScenesInBuild(Galaxy3Ep23SignalApproachScenePath, Galaxy3Ep23IceBelowScenePath);

            Debug.Log($"[Space Samurai] EP23 Signal Approach scene built at {Galaxy3Ep23SignalApproachScenePath}. " +
                      "Cold steel-blue docking corridor with dim blue lighting, metal floor/walls, steel panel props. " +
                      "1 Corrupted Salvage Drone (sickly green-grey tint, nonLethal). " +
                      "3 steps: signal (auto) → defeat 1 drone (drone_barks bark) → descend to ice.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 Ice Below", priority = 251)]
        public static void BuildEp23IceBelow()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ice Below: white-out ice storm surface, near-white fog (high density).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.88f, 0.95f); // pale blue key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.25f); // pale ambient

            // Fog (high density for whiteout effect).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.92f, 0.93f, 0.96f);
            RenderSettings.fogDensity = 0.030f;

            // Two accent lights: pale cyan and white.
            BuildAccentPointLight("IceLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.8f, 0.9f, 1f), intensity: 0.70f, range: 10f);
            BuildAccentPointLight("IceLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.9f, 0.92f, 0.95f), intensity: 0.65f, range: 9f);

            // ---- Ice surface floor and structure ----
            var iceGo = new GameObject("IceSurface");
            var ice = iceGo.transform;
            var iceLight = new Color(0.75f, 0.80f, 0.90f);
            var iceDark = new Color(0.45f, 0.50f, 0.60f);

            // Main ice floor (11 x 20).
            BuildFloorCeiling(ice, "IceFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 20f), iceLight, iceDark);

            // Ice walls.
            BuildWall(ice, "IceWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(ice, "IceWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Icy crystal props.
            var crystalColor = new Color(0.8f, 0.85f, 0.95f);
            BuildProp(ice, "Crystal1", new Vector3(-2.5f, 1.2f, 8f), new Vector3(0.8f, 1.6f, 0.6f), crystalColor);
            BuildProp(ice, "Crystal2", new Vector3(2.5f, 1.2f, 12f), new Vector3(0.8f, 1.6f, 0.6f), crystalColor);
            BuildProp(ice, "Crystal3", new Vector3(-2f, 1.2f, 16f), new Vector3(0.8f, 1.6f, 0.6f), crystalColor);

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

            // ---- 3 HeatVent pockets ----
            var heatVentPositions = new Vector3[]
            {
                new Vector3(-3.5f, 0.5f, 8f),
                new Vector3(3.5f, 0.5f, 10f),
                new Vector3(0f, 0.5f, 14f)
            };
            for (int i = 0; i < heatVentPositions.Length; i++)
            {
                var ventGo = new GameObject($"HeatVent{i}");
                ventGo.transform.SetParent(iceGo.transform, false);
                ventGo.transform.localPosition = heatVentPositions[i];
                var ventCollider = ventGo.AddComponent<BoxCollider>();
                ventCollider.size = new Vector3(2f, 3f, 2f);
                ventCollider.isTrigger = true;
                ventGo.AddComponent<Ronin7.World.HeatVent>();
            }

            // ---- 1 Wave of 2 Sentinel Platforms ----
            var sentinelColor = new Color(0.35f, 0.38f, 0.42f); // dark steel tint
            var sentinelPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 10f)
            };

            var sentinelWaveHealths = new List<Health>();
            foreach (var pos in sentinelPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, sentinelColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                sentinelWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var sentinelSpawner = BuildEp03WaveSpawner("SentinelSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { sentinelWaveHealths },
                new[] { BuildEp23DialoguePlayer("Dialogue_SentinelBarks", new Vector3(0f, 1.5f, 8f), "sentinel_barks") });

            // ---- Dialogue Players ----
            var iceBelowDialogue = BuildEp23DialoguePlayer("Dialogue_IceBelow", new Vector3(0f, 1.5f, 2f), "ice_below");
            var ibDialogueSo = new SerializedObject(iceBelowDialogue);
            ibDialogueSo.FindProperty("playOnStart").boolValue = true;
            ibDialogueSo.ApplyModifiedPropertiesWithoutUndo();

            var iceAftermathDialogue = BuildEp23DialoguePlayer("Dialogue_IceAftermath", new Vector3(0f, 1.5f, 14f), "ice_aftermath");

            // Transition box: "INTO THE STATION".
            var stationBoxGo = BuildTransitionBox("IntoStationBox", new Vector3(0f, 1.2f, 20.5f), "INTO THE STATION",
                out var stationBtn, out var stationTransition);
            var sbSo = new SerializedObject(stationTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23CryptBelowSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(stationBtn.onClick,
                new UnityEngine.Events.UnityAction(stationTransition.LoadOnFootScene));
            stationBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue ice_below (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ice Below";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = iceBelowDialogue;

            // Step 1: DefeatWaves — 2 sentinel platforms.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Sentinel Platforms (2)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = sentinelSpawner;

            // Step 2: Dialogue ice_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Ice Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = iceAftermathDialogue;

            // Step 3: Prompt — transition to Crypt Below.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Into the Station";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = stationBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23IceBelowScenePath);
            EnsureScenesInBuild(Galaxy3Ep23IceBelowScenePath, Galaxy3Ep23CryptBelowScenePath);

            Debug.Log($"[Space Samurai] EP23 Ice Below scene built at {Galaxy3Ep23IceBelowScenePath}. " +
                      "White-out ice storm surface with pale blue lighting, high-density near-white fog, icy crystal props. " +
                      "CryoChillController + 3 HeatVents. " +
                      "2 Sentinel Platforms (dark steel tint, nonLethal). " +
                      "4 steps: ice_below (auto) → defeat 2 sentinels (sentinel_barks bark) → ice_aftermath dialogue → into station.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP23 Crypt Below", priority = 252)]
        public static void BuildEp23CryptBelow()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Crypt Below: cryo-vault catwalks, cold cyan lighting + dark fog.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.70f, 0.85f); // cyan key light
            light.intensity = 0.42f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.18f, 0.22f); // cool dark ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.28f, 0.35f);
            RenderSettings.fogDensity = 0.022f;

            // Two accent lights: cyan and pale white.
            BuildAccentPointLight("CryptLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.5f, 0.85f, 1f), intensity: 0.70f, range: 10f);
            BuildAccentPointLight("CryptLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.75f, 0.80f, 0.85f), intensity: 0.65f, range: 9f);

            // ---- Cryo-vault floor and structure ----
            var cryptGo = new GameObject("CryptoVault");
            var crypt = cryptGo.transform;
            var vaultMetal = new Color(0.45f, 0.52f, 0.60f);
            var vaultDark = new Color(0.25f, 0.32f, 0.40f);

            // Main vault floor (11 x 21).
            BuildFloorCeiling(crypt, "VaultFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 21f), vaultMetal, vaultDark);

            // Vault walls.
            BuildWall(crypt, "VaultWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(crypt, "VaultWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Cryo-pod cyan props.
            var cryoColor = new Color(0.55f, 0.75f, 0.90f);
            BuildProp(crypt, "Cryo1", new Vector3(-2.5f, 1.3f, 8f), new Vector3(0.9f, 1.8f, 0.7f), cryoColor);
            BuildProp(crypt, "Cryo2", new Vector3(2.5f, 1.3f, 12f), new Vector3(0.9f, 1.8f, 0.7f), cryoColor);
            BuildProp(crypt, "Cryo3", new Vector3(-1.5f, 1.3f, 16f), new Vector3(0.9f, 1.8f, 0.7f), cryoColor);

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
                new Vector3(-3.5f, 0.5f, 9f),
                new Vector3(3.5f, 0.5f, 13f)
            };
            for (int i = 0; i < heatVentPositions.Length; i++)
            {
                var ventGo = new GameObject($"HeatVent{i}");
                ventGo.transform.SetParent(cryptGo.transform, false);
                ventGo.transform.localPosition = heatVentPositions[i];
                var ventCollider = ventGo.AddComponent<BoxCollider>();
                ventCollider.size = new Vector3(2f, 3f, 2f);
                ventCollider.isTrigger = true;
                ventGo.AddComponent<Ronin7.World.HeatVent>();
            }

            // ---- 1 Wave of 2 Cryo-Animates ----
            var cryoAnimateColor = new Color(0.50f, 0.70f, 0.85f); // icy-blue tint
            var cryoPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 10f)
            };

            var cryoWaveHealths = new List<Health>();
            foreach (var pos in cryoPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, cryoAnimateColor);
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
                new[] { BuildEp23DialoguePlayer("Dialogue_CryoBarks", new Vector3(0f, 1.5f, 8f), "cryo_barks") });

            // ---- Dialogue Players ----
            var cryptBelowDialogue = BuildEp23DialoguePlayer("Dialogue_CryptBelow", new Vector3(0f, 1.5f, 2f), "crypt_below");
            var cbDialogueSo = new SerializedObject(cryptBelowDialogue);
            cbDialogueSo.FindProperty("playOnStart").boolValue = true;
            cbDialogueSo.ApplyModifiedPropertiesWithoutUndo();

            var cryptAftermathDialogue = BuildEp23DialoguePlayer("Dialogue_CryptAftermath", new Vector3(0f, 1.5f, 15f), "crypt_aftermath");

            // Transition box: "TO THE CRYO-CHAMBER".
            var chamberBoxGo = BuildTransitionBox("ToChamberBox", new Vector3(0f, 1.2f, 21.5f), "TO THE CRYO-CHAMBER",
                out var chamberBtn, out var chamberTransition);
            var cbSo = new SerializedObject(chamberTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep23TheCommanderSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(chamberBtn.onClick,
                new UnityEngine.Events.UnityAction(chamberTransition.LoadOnFootScene));
            chamberBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue crypt_below (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Crypt Below";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = cryptBelowDialogue;

            // Step 1: DefeatWaves — 2 cryo-animates.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Cryo-Animates (2)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cryoSpawner;

            // Step 2: Dialogue crypt_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Crypt Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = cryptAftermathDialogue;

            // Step 3: Prompt — transition to The Commander.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Cryo-Chamber";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = chamberBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep23CryptBelowScenePath);
            EnsureScenesInBuild(Galaxy3Ep23CryptBelowScenePath, Galaxy3Ep23TheCommanderScenePath);

            Debug.Log($"[Space Samurai] EP23 Crypt Below scene built at {Galaxy3Ep23CryptBelowScenePath}. " +
                      "Cryo-vault catwalks with cold cyan lighting, dark fog, cyan cryo-pod props. " +
                      "CryoChillController + 2 HeatVents. " +
                      "2 Cryo-Animates (icy-blue tint, nonLethal). " +
                      "4 steps: crypt_below (auto) → defeat 2 cryo-animates (cryo_barks bark) → crypt_aftermath dialogue → to cryo-chamber.");
        }
    }
}

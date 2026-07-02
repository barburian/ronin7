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
    /// EP18 "The Drowning Deep" on-foot scene builders for the final three scenes:
    /// - CommandCore: research-floor command-core, gel-tank teal + thermal-vent amber, dense fog, 3 construct enemies
    /// - Dock: failing Vault emergency dock, red alarm light, Corsair ramp, Sable Dross ally + Takeshi NPC
    /// - Hyperspace: Corsair observation lounge, cool blue, pure denouement (NO enemies), THE GALAXY 3 FINALE, sets ep18_complete flag
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp18DialoguePlayer helper is already declared in Ep18Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Command Core", priority = 183)]
        public static void BuildEp18CommandCore()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // CommandCore: research-floor command-core. Gel-tank teal key light, thermal-vent amber accent, dark-blue ambient, dense fog.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.85f, 0.95f); // gel-tank teal key light
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.22f, 0.30f); // dark blue ambient

            // Dense fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.35f, 0.42f);
            RenderSettings.fogDensity = 0.028f;

            // Two accent point lights: teal and amber.
            BuildAccentPointLight("CommandCoreLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 0.95f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("CommandCoreLight2", new Vector3(3f, 2.5f, 14f),
                new Color(1f, 0.70f, 0.35f), intensity: 0.75f, range: 9f);

            // ---- Command core floor and walls ----
            var coreGo = new GameObject("CommandCore");
            var core = coreGo.transform;
            var coreMetal = new Color(0.30f, 0.35f, 0.40f);
            var coreDark = new Color(0.18f, 0.22f, 0.28f);

            // Main command floor.
            BuildFloorCeiling(core, "CommandFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), coreMetal, coreDark);

            // Command walls.
            BuildWall(core, "CommandWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(core, "CommandWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Gel-tank unlit cube props ----
            var gelTankColor = new Color(0.50f, 0.85f, 0.95f); // bright teal
            var gelTanks = 3;
            for (int i = 0; i < gelTanks; i++)
            {
                float x = (i - 1f) * 4f;
                float y = 1.5f;
                float z = 6f + i * 2f;
                var tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tank.name = $"GelTank_{i}";
                Object.DestroyImmediate(tank.GetComponent<Collider>());
                tank.transform.SetParent(core, false);
                tank.transform.position = new Vector3(x, y, z);
                tank.transform.localScale = new Vector3(0.6f, 0.8f, 0.5f);
                tank.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(gelTankColor);
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

            // ---- Sable Dross NPC (non-hostile, gives access code) ----
            var sableGo = new GameObject("SableDross_NPC");
            sableGo.transform.SetParent(core, false);
            sableGo.transform.position = new Vector3(2f, 0f, 6f);

            // Capsule body (deep-blue tint).
            var sableBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            sableBody.name = "Body";
            sableBody.transform.SetParent(sableGo.transform, false);
            TintShared(sableBody.GetComponent<Renderer>(), new Color(0.35f, 0.40f, 0.55f)); // deep-blue

            // Add StoryNpc (NO Health).
            var sableNpc = sableGo.AddComponent<StoryNpc>();
            var sableNpcSo = new SerializedObject(sableNpc);
            sableNpcSo.FindProperty("displayName").stringValue = "Sable Dross";
            sableNpcSo.FindProperty("remote").boolValue = false;
            sableNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var sableInterceptDialogue = BuildEp18DialoguePlayer("Dialogue_SableIntercept", new Vector3(2f, 1.5f, 6f), "sable_intercept");
            var sableInterceptSo = new SerializedObject(sableInterceptDialogue);
            sableInterceptSo.FindProperty("playOnStart").boolValue = true;
            sableInterceptSo.ApplyModifiedPropertiesWithoutUndo();

            var commodoreLogicDialogue = BuildEp18DialoguePlayer("Dialogue_CommodoreLogic", new Vector3(0f, 1.5f, 10f), "commodore_logic");

            // ---- 3 Construct enemies: 1 wave ----
            var constructColor = new Color(0.45f, 0.48f, 0.52f); // slate-grey tint
            var constructWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 10f),
                new Vector3(0f, 0f, 12f)
            };

            var constructWaveHealths = new List<Health>();
            foreach (var pos in constructWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, constructColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                constructWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var constructSpawner = BuildWaveSpawner("ConstructSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { constructWaveHealths },
                new[] { BuildEp18DialoguePlayer("Dialogue_ConstructBarks", new Vector3(0f, 1.5f, 10f), "construct_barks") });

            // Transition box: "ESCAPE — THE DOCK".
            var dockBoxGo = BuildTransitionBox("ToDockBox", new Vector3(0f, 1.2f, 20.5f), "ESCAPE — THE DOCK",
                out var dockBtn, out var dockTransition);
            var dbSo = new SerializedObject(dockTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep18DockSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dockBtn.onClick,
                new UnityEngine.Events.UnityAction(dockTransition.LoadOnFootScene));
            dockBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue sable_intercept (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Sable Intercept";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = sableInterceptDialogue;

            // Step 1: DefeatWaves — 3 Constructs.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Constructs (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = constructSpawner;

            // Step 2: Dialogue commodore_logic.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Commodore Logic";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = commodoreLogicDialogue;

            // Step 3: Prompt — transition to Dock.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Escape to the Dock";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = dockBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18CommandCoreScenePath);
            EnsureScenesInBuild(Galaxy3Ep18CommandCoreScenePath, Galaxy3Ep18DockScenePath);

            Debug.Log($"[Space Samurai] EP18 Command Core scene built at {Galaxy3Ep18CommandCoreScenePath}. " +
                      "Layout: research-floor command-core with gel-tank teal + thermal-vent amber lighting, dark metal floor/walls, unlit gel-tank cube props, dense fog. " +
                      "Sable Dross NPC (deep-blue capsule, NO Health, displayName). " +
                      "3 Construct enemies (slate-grey, nonLethal). " +
                      "4 steps: sable_intercept (auto) → defeat 3 Constructs (construct_barks bark) → commodore_logic dialogue → transition to Dock.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Dock", priority = 184)]
        public static void BuildEp18Dock()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Dock: failing Vault emergency dock, red alarm lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.35f, 0.30f); // red alarm key light
            light.intensity = 0.52f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.12f, 0.10f); // dark ambient

            // Red alarm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.15f, 0.12f);
            RenderSettings.fogDensity = 0.024f;

            // Two red alarm accent lights.
            BuildAccentPointLight("DockLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.40f, 0.30f), intensity: 0.90f, range: 10f);
            BuildAccentPointLight("DockLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.98f, 0.32f, 0.25f), intensity: 0.82f, range: 9f);

            // ---- Vault dock floor and walls ----
            var dockGo = new GameObject("VaultDock");
            var dock = dockGo.transform;
            var dockMetal = new Color(0.42f, 0.38f, 0.35f);
            var dockDark = new Color(0.28f, 0.22f, 0.18f);

            // Main dock floor.
            BuildFloorCeiling(dock, "DockFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), dockMetal, dockDark);

            // Dock walls.
            BuildWall(dock, "DockWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(dock, "DockWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Corsair ramp prop at the far end.
            BuildProp(dock, "CorsairRamp", new Vector3(0f, 0.8f, 18f), new Vector3(4f, 1.5f, 0.5f), new Color(0.50f, 0.45f, 0.40f));

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

            // ---- Sable Dross as ALLY (AllyCombatant, NO Health) ----
            var sableAllyGo = new GameObject("SableDross");
            sableAllyGo.transform.SetParent(dock, false);
            sableAllyGo.transform.position = new Vector3(-2f, 0f, 6f);

            // Primitive body (capsule, deep-blue tint).
            var sableAllyBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            sableAllyBody.name = "Body";
            sableAllyBody.transform.SetParent(sableAllyGo.transform, false);
            TintShared(sableAllyBody.GetComponent<Renderer>(), new Color(0.35f, 0.40f, 0.55f)); // deep-blue

            // Add AllyCombatant (NO Health).
            var allyCombatant = sableAllyGo.AddComponent<AllyCombatant>();
            var allySo = new SerializedObject(allyCombatant);
            allySo.ApplyModifiedPropertiesWithoutUndo();

            // Add StoryNpc.
            var sableAllyNpc = sableAllyGo.AddComponent<StoryNpc>();
            var sNpcSo = new SerializedObject(sableAllyNpc);
            sNpcSo.FindProperty("displayName").stringValue = "Sable Dross";
            sNpcSo.FindProperty("remote").boolValue = false;
            sNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Takeshi NPC (rescued, present at dock, NO Health) ----
            var takeshiGo = new GameObject("Takeshi_NPC");
            takeshiGo.transform.SetParent(dock, false);
            takeshiGo.transform.position = new Vector3(2f, 0f, 6f);

            // Capsule body (pale tint).
            var takeshiBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            takeshiBody.name = "Body";
            takeshiBody.transform.SetParent(takeshiGo.transform, false);
            TintShared(takeshiBody.GetComponent<Renderer>(), new Color(0.75f, 0.73f, 0.70f)); // pale tint

            // Add StoryNpc (NO Health).
            var takeshiNpc = takeshiGo.AddComponent<StoryNpc>();
            var tNpcSo = new SerializedObject(takeshiNpc);
            tNpcSo.FindProperty("displayName").stringValue = "Takeshi";
            tNpcSo.FindProperty("remote").boolValue = false;
            tNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Player ----
            var dockReturnDialogue = BuildEp18DialoguePlayer("Dialogue_DockReturn", new Vector3(0f, 1.5f, 4f), "dock_return");
            var dockReturnSo = new SerializedObject(dockReturnDialogue);
            dockReturnSo.FindProperty("playOnStart").boolValue = true;
            dockReturnSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "BOARD — THE CORSAIR" with CampaignFlagSetter.
            var corsairBoxGo = BuildTransitionBox("ToCorsairBox", new Vector3(0f, 1.2f, 20.5f), "BOARD — THE CORSAIR",
                out var corsairBtn, out var corsairTransition);
            corsairBoxGo.SetActive(false);

            // Set flags: sable_dross_recruited, takeshi_rescued.
            var dockFlagSetter = corsairBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var dockFsSo = new SerializedObject(dockFlagSetter);
            var dockFlagsProp = dockFsSo.FindProperty("flags");
            dockFlagsProp.arraySize = 2;
            dockFlagsProp.GetArrayElementAtIndex(0).stringValue = "sable_dross_recruited";
            dockFlagsProp.GetArrayElementAtIndex(1).stringValue = "takeshi_rescued";
            dockFsSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire corsairBtn.onClick → flagSetter.SetFlags AND → corsairTransition.LoadOnFootScene.
            UnityEventTools.AddPersistentListener(corsairBtn.onClick,
                new UnityEngine.Events.UnityAction(dockFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(corsairBtn.onClick,
                new UnityEngine.Events.UnityAction(corsairTransition.LoadOnFootScene));

            // Set transition scene name.
            var cbSo = new SerializedObject(corsairTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep18HyperspaceSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue dock_return (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Dock Return";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = dockReturnDialogue;

            // Step 1: Prompt — transition to Corsair (sets recruit flags).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Board the Corsair (sets sable_dross_recruited, takeshi_rescued)";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = corsairBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18DockScenePath);
            EnsureScenesInBuild(Galaxy3Ep18DockScenePath, Galaxy3Ep18HyperspaceScenePath);

            Debug.Log($"[Space Samurai] EP18 Dock scene built at {Galaxy3Ep18DockScenePath}. " +
                      "Layout: failing Vault emergency dock with red alarm lighting, dark metal floor/walls, corsair ramp prop. " +
                      "Sable Dross ally (AllyCombatant, NO Health, deep-blue capsule, StoryNpc displayName). " +
                      "Takeshi NPC (pale capsule, NO Health, StoryNpc displayName, rescued). " +
                      "2 steps: dock_return (auto) → transition to Corsair (sets sable_dross_recruited + takeshi_rescued via CampaignFlagSetter).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Hyperspace", priority = 185)]
        public static void BuildEp18Hyperspace()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Hyperspace: Corsair observation lounge, cool blue lighting, streaking-stars feel.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.70f, 0.85f, 1.0f); // cool blue key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.28f, 0.35f); // cool blue ambient

            // Light streaking fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.40f, 0.48f);
            RenderSettings.fogDensity = 0.018f;

            // Two cool blue accent lights.
            BuildAccentPointLight("HyperspaceLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.60f, 0.80f, 1f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("HyperspaceLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.55f, 0.75f, 0.95f), intensity: 0.75f, range: 9f);

            // ---- Hyperspace observation lounge floor and walls ----
            var hyperspaceGo = new GameObject("ObservationLounge");
            var hyperspace = hyperspaceGo.transform;
            var hyperspcMetal = new Color(0.32f, 0.38f, 0.45f);
            var hyperspcDark = new Color(0.18f, 0.25f, 0.32f);

            // Main observation floor.
            BuildFloorCeiling(hyperspace, "HyperspaceFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), hyperspcMetal, hyperspcDark);

            // Observation walls.
            BuildWall(hyperspace, "HyperspaceWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(hyperspace, "HyperspaceWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Console prop.
            BuildProp(hyperspace, "ConsolePanel", new Vector3(-3f, 1.5f, 8f), new Vector3(2f, 1.5f, 0.3f), new Color(0.45f, 0.50f, 0.55f));

            // Window prop.
            BuildProp(hyperspace, "ObservationWindow", new Vector3(3f, 2f, 20f), new Vector3(3f, 2f, 0.1f), new Color(0.70f, 0.85f, 1f));

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

            // ---- Dialogue Players (NO enemies — pure denouement) ----
            var hyperspaceRevelationDialogue = BuildEp18DialoguePlayer("Dialogue_HyperspaceRevelation", new Vector3(0f, 1.5f, 2f), "hyperspace_revelation");
            var hyperspaceRevSo = new SerializedObject(hyperspaceRevelationDialogue);
            hyperspaceRevSo.FindProperty("playOnStart").boolValue = true;
            hyperspaceRevSo.ApplyModifiedPropertiesWithoutUndo();

            var ledgerPromiseDialogue = BuildEp18DialoguePlayer("Dialogue_LedgerPromise", new Vector3(0f, 1.5f, 8f), "ledger_promise");
            var observationTruthDialogue = BuildEp18DialoguePlayer("Dialogue_ObservationTruth", new Vector3(0f, 1.5f, 14f), "observation_truth");

            // Finale transition box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 20.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set ep18_complete flag and return to space.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep18_complete";
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

            // Step 0: Dialogue hyperspace_revelation (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Hyperspace Revelation";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = hyperspaceRevelationDialogue;

            // Step 1: Dialogue ledger_promise.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Ledger Promise";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = ledgerPromiseDialogue;

            // Step 2: Dialogue observation_truth.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Observation Truth";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = observationTruthDialogue;

            // Step 3: Prompt — return to space with ep18_complete flag.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep18_complete)";
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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18HyperspaceScenePath);
            EnsureScenesInBuild(Galaxy3Ep18HyperspaceScenePath);

            Debug.Log($"[Space Samurai] EP18 Hyperspace scene built at {Galaxy3Ep18HyperspaceScenePath}. " +
                      "Layout: Corsair observation lounge in hyperspace with cool blue lighting, dark metal floor/walls, console and window props, streaking-stars fog. " +
                      "NO enemies (pure denouement, dialogue-only finale). " +
                      "4 steps: hyperspace_revelation (auto) → ledger_promise dialogue → observation_truth dialogue → " +
                      "return to hub Prompt (sets ep18_complete via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 18 FINALE (sets ep18_complete; Galaxy 3 continues through EP19+).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP18 Scenes", priority = 188)]
        public static void BuildAllEp18Scenes()
        {
            BuildEp18Station();
            BuildEp18VaultOuter();
            BuildEp18ExtractionChamber();
            BuildEp18CommandCore();
            BuildEp18Dock();
            BuildEp18Hyperspace();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP18 scenes rebuilt + inputs rewired.");
        }
    }
}

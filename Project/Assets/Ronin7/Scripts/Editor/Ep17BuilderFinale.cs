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
    /// EP17 "The Failsafe" on-foot scene builders for the final three scenes:
    /// - Vault: zero-g cryo server corridor, 1 Pale Choir assassin duel via DuelYield
    /// - Escape: ore-tunnel escape with Vesper ally, 5 Rustfang security enemies
    /// - SurfaceDuel: THE GALAXY 3 FINALE with Khall + 2 operatives, sets ep17_complete flag
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp17DialoguePlayer helper is already declared in Ep17Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Vault", priority = 173)]
        public static void BuildEp17Vault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault: zero-g cryo server corridor. Cold cyan-white directional light, dark-blue ambient, light fog.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.90f, 1.0f); // cyan-white key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.22f, 0.30f); // dark blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.30f, 0.40f);
            RenderSettings.fogDensity = 0.020f;

            // Two cyan accent point lights.
            BuildAccentPointLight("VaultLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 1f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("VaultLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.45f, 0.80f, 0.95f), intensity: 0.80f, range: 9f);

            // ---- Vault corridor floor and walls ----
            var vaultGo = new GameObject("VaultCorridor");
            var vault = vaultGo.transform;
            var darkMetal = new Color(0.25f, 0.28f, 0.32f);
            var darkerMetal = new Color(0.15f, 0.18f, 0.22f);

            // Main vault floor.
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 10f), new Vector3(8f, 0f, 20f), darkMetal, darkerMetal);

            // Vault walls.
            BuildWall(vault, "VaultWall_W", new Vector3(-3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(vault, "VaultWall_E", new Vector3(3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Data-core unlit props ----
            var dataCoreColor = new Color(0.50f, 0.85f, 1f); // bright cyan
            var dataCores = 4;
            for (int i = 0; i < dataCores; i++)
            {
                float x = (i % 2 - 0.5f) * 3f;
                float y = 1.5f;
                float z = 6f + (i / 2) * 5f;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = $"DataCore_{i}";
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.transform.SetParent(vault, false);
                core.transform.position = new Vector3(x, y, z);
                core.transform.localScale = new Vector3(0.5f, 0.6f, 0.4f);
                core.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(dataCoreColor);
            }

            // ---- RONIN-7 MIRROR record (bright unlit prop at back) ----
            var recordProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            recordProp.name = "RecordRonin7";
            Object.DestroyImmediate(recordProp.GetComponent<Collider>());
            recordProp.transform.SetParent(vault, false);
            recordProp.transform.position = new Vector3(0f, 2f, 18f);
            recordProp.transform.localScale = new Vector3(0.8f, 1f, 0.6f);
            recordProp.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.95f, 0.85f)); // bright warm-white

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
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- ONE duel enemy: Pale Choir Assassin ----
            var duelist = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var duelistRenderer = duelist.GetComponent<Renderer>();
            if (duelistRenderer != null) TintShared(duelistRenderer, new Color(0.85f, 0.85f, 0.90f)); // pale tint
            var duelistMelee = duelist.GetComponent<MeleeAttacker>();
            if (duelistMelee != null)
            {
                var maSo = new SerializedObject(duelistMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var duelistHealth = duelist.GetComponent<Health>();
            var duelistNpc = duelist.gameObject.AddComponent<StoryNpc>();
            var dNpcSo = new SerializedObject(duelistNpc);
            dNpcSo.FindProperty("displayName").stringValue = "Pale Choir Assassin";
            dNpcSo.FindProperty("remote").boolValue = false;
            dNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.3.
            var duelYield = duelist.gameObject.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", duelistHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.3f;
            if (duelistMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { duelistMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Duelist STAYS ACTIVE (not SetActive(false)).

            // ---- Dialogue Players ----
            var vaultBarksDialogue = BuildEp17DialoguePlayer("Dialogue_VaultBarks", new Vector3(0f, 1.5f, 10f), "arena_barks");
            var barksDlgSo = new SerializedObject(vaultBarksDialogue);
            barksDlgSo.FindProperty("playOnStart").boolValue = true;
            barksDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var recordFoundDialogue = BuildEp17DialoguePlayer("Dialogue_RecordFound", new Vector3(0f, 1.5f, 18f), "record_found");

            // Transition box: "ASCEND — THE RISING".
            var escapeBoxGo = BuildTransitionBox("ToEscapeBox", new Vector3(0f, 1.2f, 20.5f), "ASCEND — THE RISING",
                out var escapeBtn, out var escapeTransition);
            var ebSo = new SerializedObject(escapeTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy3Ep17EscapeSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(escapeBtn.onClick,
                new UnityEngine.Events.UnityAction(escapeTransition.LoadOnFootScene));
            escapeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue arena_barks (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault Barks";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultBarksDialogue;

            // Step 1: Prompt (null) — the duel (DuelYield.onAccepted advances it).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Duel Pale Choir Assassin (yield via DuelYield)";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 2: Dialogue record_found.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Record Found";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = recordFoundDialogue;

            // Step 3: Prompt — transition to Escape.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Ascend to Escape";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = escapeBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → missionDirector.AdvanceFromPrompt.
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17VaultScenePath);
            EnsureScenesInBuild(Galaxy3Ep17VaultScenePath, Galaxy3Ep17EscapeScenePath);

            Debug.Log($"[Space Samurai] EP17 Vault scene built at {Galaxy3Ep17VaultScenePath}. " +
                      "Layout: zero-g cryo server corridor with cyan-white lighting, dark metal floor/walls, unlit data-core and record props. " +
                      "1 Duelist enemy (Pale Choir Assassin, pale tint, nonLethal, DuelYield yield at 30%, ACTIVE from start). " +
                      "4 steps: arena_barks (auto) → duel Prompt (onAccepted → AdvanceFromPrompt) → record_found dialogue → transition to Escape.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Escape", priority = 174)]
        public static void BuildEp17Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Escape: ore-tunnel escape with red alarm lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.35f, 0.30f); // red alarm key light
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.15f, 0.12f); // dark ambient

            // Dark fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.18f, 0.15f);
            RenderSettings.fogDensity = 0.022f;

            // Two red alarm accent lights.
            BuildAccentPointLight("EscapeLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.40f, 0.30f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("EscapeLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.95f, 0.35f, 0.25f), intensity: 0.80f, range: 9f);

            // ---- Ore-tunnel floor and walls ----
            var tunnelGo = new GameObject("OreTunnel");
            var tunnel = tunnelGo.transform;
            var oreMetal = new Color(0.40f, 0.35f, 0.30f);
            var oreDark = new Color(0.25f, 0.20f, 0.18f);

            // Main tunnel floor.
            BuildFloorCeiling(tunnel, "TunnelFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), oreMetal, oreDark);

            // Tunnel walls.
            BuildWall(tunnel, "TunnelWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(tunnel, "TunnelWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Collapsing bulkhead prop ----
            var bulkheadColor = new Color(0.45f, 0.38f, 0.32f);
            BuildProp(tunnel, "BulkheadPanel", new Vector3(0f, 1.5f, 18f), new Vector3(3f, 2f, 0.3f), bulkheadColor);

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

            // ---- Vesper as ally (AllyCombatant, NO Health) ----
            var vesperGo = new GameObject("Vesper");
            vesperGo.transform.SetParent(tunnel, false);
            vesperGo.transform.position = new Vector3(-2f, 0f, 6f);

            // Primitive body (capsule).
            var vesperBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vesperBody.name = "Body";
            vesperBody.transform.SetParent(vesperGo.transform, false);
            TintShared(vesperBody.GetComponent<Renderer>(), new Color(0.60f, 0.55f, 0.50f)); // neutral grey-brown

            // Add AllyCombatant (NO Health).
            var allyCombatant = vesperGo.AddComponent<AllyCombatant>();
            var allySo = new SerializedObject(allyCombatant);
            allySo.ApplyModifiedPropertiesWithoutUndo();

            // Add StoryNpc.
            var vesperNpc = vesperGo.AddComponent<StoryNpc>();
            var vNpcSo = new SerializedObject(vesperNpc);
            vNpcSo.FindProperty("displayName").stringValue = "Vesper";
            vNpcSo.FindProperty("remote").boolValue = false;
            vNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Player ----
            var risingDialogue = BuildEp17DialoguePlayer("Dialogue_TheRising", new Vector3(0f, 1.5f, 4f), "the_rising");
            var risingDlgSo = new SerializedObject(risingDialogue);
            risingDlgSo.FindProperty("playOnStart").boolValue = true;
            risingDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5 Rustfang Security enemies: 1 wave ----
            var securityColor = new Color(0.55f, 0.40f, 0.35f); // rust-brown tint
            var securityWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8f),
                new Vector3(0f, 0f, 10f)
            };

            var securityWaveHealths = new List<Health>();
            foreach (var pos in securityWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, securityColor);
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

            var securitySpawner = BuildEp03WaveSpawner("RustfangSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { securityWaveHealths },
                new[] { BuildEp17DialoguePlayer("Dialogue_EscapeBarks", new Vector3(0f, 1.5f, 8f), "arena_barks") });

            // Transition box: "REACH — THE SURFACE".
            var surfaceBoxGo = BuildTransitionBox("ToSurfaceBox", new Vector3(0f, 1.2f, 20.5f), "REACH — THE SURFACE",
                out var surfaceBtn, out var surfaceTransition);
            var sbSo = new SerializedObject(surfaceTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep17SurfaceDuelSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(surfaceBtn.onClick,
                new UnityEngine.Events.UnityAction(surfaceTransition.LoadOnFootScene));
            surfaceBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue the_rising (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Rising";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = risingDialogue;

            // Step 1: DefeatWaves — 5 Rustfang Security.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Rustfang Security (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = securitySpawner;

            // Step 2: Prompt — transition to Surface Duel.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Reach the Surface";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = surfaceBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17EscapeScenePath);
            EnsureScenesInBuild(Galaxy3Ep17EscapeScenePath, Galaxy3Ep17SurfaceDuelScenePath);

            Debug.Log($"[Space Samurai] EP17 Escape scene built at {Galaxy3Ep17EscapeScenePath}. " +
                      "Layout: ore-tunnel with red alarm lighting, dark metal floor/walls, collapsing bulkhead prop. " +
                      "Vesper ally (AllyCombatant, NO Health, grey-brown capsule, StoryNpc displayName). " +
                      "5 Rustfang Security enemies (rust-brown, nonLethal). " +
                      "3 steps: the_rising (auto) → defeat 5 Rustfang Security (arena_barks bark) → transition to Surface Duel.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Surface Duel", priority = 175)]
        public static void BuildEp17SurfaceDuel()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Surface Duel: rust-red landing platform with dusk lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.60f, 0.40f); // warm rust directional
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.20f, 0.15f); // warm dark ambient

            // Light dust fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.50f, 0.38f, 0.28f);
            RenderSettings.fogDensity = 0.019f;

            // Two warm rust accent lights.
            BuildAccentPointLight("SurfaceLight1", new Vector3(-4f, 2.5f, 8f),
                new Color(1f, 0.65f, 0.40f), intensity: 0.95f, range: 12f);
            BuildAccentPointLight("SurfaceLight2", new Vector3(4f, 3f, 14f),
                new Color(0.95f, 0.60f, 0.35f), intensity: 0.90f, range: 11f);

            // ---- Platform floor and scenery ----
            var platformGo = new GameObject("LandingPlatform");
            var platform = platformGo.transform;
            var rustMetal = new Color(0.55f, 0.42f, 0.35f);
            var rustDark = new Color(0.40f, 0.30f, 0.22f);

            // Large platform floor.
            BuildFloorCeiling(platform, "PlatformFloor", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), rustMetal, rustDark);

            // Platform walls/edges.
            BuildWall(platform, "PlatformWall_W", new Vector3(-8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(platform, "PlatformWall_E", new Vector3(8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Corsair ramp prop at the far end.
            BuildProp(platform, "CorsairRamp", new Vector3(0f, 0.8f, 18f), new Vector3(4f, 1.5f, 0.5f), new Color(0.50f, 0.45f, 0.40f));

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
            var khallConfrontDialogue = BuildEp17DialoguePlayer("Dialogue_KhallConfront", new Vector3(0f, 1.5f, 2f), "khall_confront");
            var khallConfrontSo = new SerializedObject(khallConfrontDialogue);
            khallConfrontSo.FindProperty("playOnStart").boolValue = true;
            khallConfrontSo.ApplyModifiedPropertiesWithoutUndo();

            var khallPartingDialogue = BuildEp17DialoguePlayer("Dialogue_KhallParting", new Vector3(0f, 1.5f, 8f), "khall_parting");
            var bridgeReckoningDialogue = BuildEp17DialoguePlayer("Dialogue_BridgeReckoning", new Vector3(0f, 1.5f, 10f), "bridge_reckoning");
            var networkRevelationDialogue = BuildEp17DialoguePlayer("Dialogue_NetworkRevelation", new Vector3(0f, 1.5f, 12f), "network_revelation");
            var hyperspaceDialogue = BuildEp17DialoguePlayer("Dialogue_Hyperspace", new Vector3(0f, 1.5f, 14f), "hyperspace_drift");

            // ---- 3 enemies: Khall + 2 Ronin-remnant operatives: 1 wave ----
            var khallColor = new Color(0.30f, 0.30f, 0.40f); // distinct dark tint
            var operativeColor = new Color(0.50f, 0.50f, 0.55f); // lighter tint for operatives
            var enemyWaveHealths = new List<Health>();

            // Khall at center.
            var khall = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var khallRenderer = khall.GetComponent<Renderer>();
            if (khallRenderer != null) TintShared(khallRenderer, khallColor);
            var khallMelee = khall.GetComponent<MeleeAttacker>();
            if (khallMelee != null)
            {
                var khallMaSo = new SerializedObject(khallMelee);
                khallMaSo.FindProperty("nonLethalDisable").boolValue = true;
                khallMaSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var khallNpc = khall.gameObject.AddComponent<StoryNpc>();
            var khallNpcSo = new SerializedObject(khallNpc);
            khallNpcSo.FindProperty("displayName").stringValue = "Khall";
            khallNpcSo.FindProperty("remote").boolValue = false;
            khallNpcSo.ApplyModifiedPropertiesWithoutUndo();
            khall.gameObject.SetActive(false);
            enemyWaveHealths.Add(khall.GetComponent<Health>());

            // Two operatives flanking.
            var operativePositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 8f),
                new Vector3(2.5f, 0f, 8f)
            };

            foreach (var pos in operativePositions)
            {
                var operative = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var operRenderer = operative.GetComponent<Renderer>();
                if (operRenderer != null) TintShared(operRenderer, operativeColor);
                var operMelee = operative.GetComponent<MeleeAttacker>();
                if (operMelee != null)
                {
                    var operMaSo = new SerializedObject(operMelee);
                    operMaSo.FindProperty("nonLethalDisable").boolValue = true;
                    operMaSo.ApplyModifiedPropertiesWithoutUndo();
                }
                operative.gameObject.SetActive(false);
                enemyWaveHealths.Add(operative.GetComponent<Health>());
            }

            var waveSpawner = BuildEp03WaveSpawner("FinaleSpawner", new Vector3(0f, 0.5f, 10f), 3f,
                new List<List<Health>> { enemyWaveHealths },
                new[] { BuildEp17DialoguePlayer("Dialogue_SurfaceBarks", new Vector3(0f, 1.5f, 10f), "arena_barks") });

            // ---- Finale transition box with CampaignFlagSetter ----
            var returnBoxGo = BuildTransitionBox("ReturnHubBox", new Vector3(0f, 1.2f, 20.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set ep17_complete flag and return to hub.
            var flagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var fsSo = new SerializedObject(flagSetter);
            var flagsProp = fsSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ep17_complete";
            fsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue khall_confront (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Confront";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallConfrontDialogue;

            // Step 1: DefeatWaves — 3 (Khall + 2 operatives).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Khall + 2 Operatives (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = waveSpawner;

            // Step 2: Dialogue khall_parting.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Khall Parting";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = khallPartingDialogue;

            // Step 3: Dialogue bridge_reckoning.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Bridge Reckoning";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = bridgeReckoningDialogue;

            // Step 4: Dialogue network_revelation.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Network Revelation";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = networkRevelationDialogue;

            // Step 5: Dialogue hyperspace_drift.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Hyperspace Drift";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = hyperspaceDialogue;

            // Step 6: Prompt — return to hub with flags.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Return to Stars (sets ep17_complete)";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17SurfaceDuelScenePath);
            EnsureScenesInBuild(Galaxy3Ep17SurfaceDuelScenePath);

            Debug.Log($"[Space Samurai] EP17 Surface Duel scene built at {Galaxy3Ep17SurfaceDuelScenePath}. " +
                      "Layout: rust-red landing platform with warm dusk lighting, corsair ramp prop. " +
                      "3 enemies (Khall distinct dark tint + 2 Ronin-remnant operatives lighter tint, nonLethal). " +
                      "7 steps: khall_confront (auto) → defeat 3 (arena_barks bark) → khall_parting dialogue → " +
                      "bridge_reckoning dialogue → network_revelation dialogue → hyperspace_drift dialogue → " +
                      "return to hub Prompt (sets ep17_complete via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 17 FINALE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP17 Scenes", priority = 178)]
        public static void BuildAllEp17Scenes()
        {
            BuildEp17SalvageYard();
            BuildEp17Arena();
            BuildEp17LowerPit();
            BuildEp17Vault();
            BuildEp17Escape();
            BuildEp17SurfaceDuel();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP17 scenes rebuilt + inputs rewired.");
        }
    }
}

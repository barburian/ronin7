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
    /// EP19 "The Operative's Redemption" on-foot scene builders for the final four scenes:
    /// - VaultBelow: cold below-freezing data-vault with Tara NPC, HackTerminal, 5 vault operatives
    /// - AshAndVoid: red alarm fire-orange tower crown with Tara NPC, 4 colony guards
    /// - DarkCorridors: dim grey-blue derelict station with ONE duel enemy via DuelYield
    /// - Corsair: cool blue observation lounge, pure denouement (NO enemies), THE GALAXY 3 FINALE, sets ep19_complete + cassie04_recruited flags
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp19DialoguePlayer helper is already declared in Ep19Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Vault Below", priority = 193)]
        public static void BuildEp19VaultBelow()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault Below: cold below-freezing blue-white data-vault.
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
            BuildAccentPointLight("VaultBelowLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 1f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("VaultBelowLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.45f, 0.80f, 0.95f), intensity: 0.80f, range: 9f);

            // ---- Vault floor and walls ----
            var vaultGo = new GameObject("DataVault");
            var vault = vaultGo.transform;
            var darkMetal = new Color(0.25f, 0.28f, 0.32f);
            var darkerMetal = new Color(0.15f, 0.18f, 0.22f);

            // Main vault floor (12 x 22).
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 22f), darkMetal, darkerMetal);

            // Vault walls.
            BuildWall(vault, "VaultWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 22f));
            BuildWall(vault, "VaultWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 22f));

            // ---- Data-core unlit cube props ----
            var dataCoreColor = new Color(0.50f, 0.85f, 1f); // bright cyan
            var dataCores = 4;
            for (int i = 0; i < dataCores; i++)
            {
                float x = (i % 2 - 0.5f) * 4f;
                float y = 1.5f;
                float z = 6f + (i / 2) * 6f;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = $"DataCore_{i}";
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.transform.SetParent(vault, false);
                core.transform.position = new Vector3(x, y, z);
                core.transform.localScale = new Vector3(0.5f, 0.6f, 0.4f);
                core.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(dataCoreColor);
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

            // ---- Tara StoryNpc capsule ----
            var taraGo = new GameObject("Tara_NPC");
            taraGo.transform.SetParent(vault, false);
            taraGo.transform.position = new Vector3(2f, 0f, 8f);

            var taraBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            taraBody.name = "Body";
            taraBody.transform.SetParent(taraGo.transform, false);
            TintShared(taraBody.GetComponent<Renderer>(), new Color(0.50f, 0.50f, 0.55f)); // neutral tint

            var taraNpc = taraGo.AddComponent<StoryNpc>();
            var taraNpcSo = new SerializedObject(taraNpc);
            taraNpcSo.FindProperty("displayName").stringValue = "Tara";
            taraNpcSo.FindProperty("remote").boolValue = false;
            taraNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- HackTerminal (archive lock) ----
            var hackTerminalGo = new GameObject("HackTerminal");
            hackTerminalGo.transform.SetParent(vault, false);
            hackTerminalGo.transform.position = new Vector3(0f, 1f, 15f);
            var hackTerminal = hackTerminalGo.AddComponent<HackTerminal>();
            var htSo = new SerializedObject(hackTerminal);

            var hackRef = FindRef(refs, "Right Hand", "Hack");
            SetObjectRef(htSo, "hackAction", hackRef);

            // Vault door reveal dialogue.
            var vaultDoorDialogue = BuildEp19DialoguePlayer("Dialogue_VaultDoor", new Vector3(0f, 1.5f, 15f), "vault_door");
            SetObjectRef(htSo, "revealDialogue", vaultDoorDialogue);

            // AudioSource for HackTerminal.
            var hackAudio = hackTerminalGo.AddComponent<AudioSource>();
            hackAudio.spatialBlend = 1f;
            hackAudio.playOnAwake = false;
            SetObjectRef(htSo, "audioSource", hackAudio);

            htSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: purged_entry ----
            var purgedEntryDialogue = BuildEp19DialoguePlayer("Dialogue_PurgedEntry", new Vector3(0f, 1.5f, 4f), "purged_entry");

            // ---- 5 Vault operatives: 1 wave ----
            var operativeColor = new Color(0.50f, 0.50f, 0.55f); // neutral tint
            var operativeWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8f),
                new Vector3(0f, 0f, 10f)
            };

            var operativeWaveHealths = new List<Health>();
            foreach (var pos in operativeWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, operativeColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                operativeWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var operativeSpawner = BuildEp03WaveSpawner("VaultOperativeSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { operativeWaveHealths },
                new[] { BuildEp19DialoguePlayer("Dialogue_VaultBarks", new Vector3(0f, 1.5f, 8f), "vault_barks") });

            // Transition box: "ASCEND — THE TOWER CROWN".
            var ashAndVoidBoxGo = BuildTransitionBox("ToAshAndVoidBox", new Vector3(0f, 1.2f, 21.5f), "ASCEND — THE TOWER CROWN",
                out var ashAndVoidBtn, out var ashAndVoidTransition);
            var abSo = new SerializedObject(ashAndVoidTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19AshAndVoidSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(ashAndVoidBtn.onClick,
                new UnityEngine.Events.UnityAction(ashAndVoidTransition.LoadOnFootScene));
            ashAndVoidBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Hack vault_door terminal.
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Hack;
            s0.FindPropertyRelative("label").stringValue = "Hack: Archive Lock";
            s0.FindPropertyRelative("hackTerminal").objectReferenceValue = hackTerminal;

            // Step 1: DefeatWaves — 5 vault operatives.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Vault Operatives (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = operativeSpawner;

            // Step 2: Dialogue purged_entry.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Purged Entry";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = purgedEntryDialogue;

            // Step 3: Prompt — transition to Ash And Void.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Ascend to Tower Crown";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = ashAndVoidBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19VaultBelowScenePath);
            EnsureScenesInBuild(Galaxy3Ep19VaultBelowScenePath, Galaxy3Ep19AshAndVoidScenePath);

            Debug.Log($"[Space Samurai] EP19 Vault Below scene built at {Galaxy3Ep19VaultBelowScenePath}. " +
                      "Layout: cold below-freezing data-vault with cyan-white lighting, dark metal floor/walls, unlit data-core cube props, light fog. " +
                      "Tara NPC (neutral capsule, NO Health, StoryNpc displayName). " +
                      "HackTerminal (archive lock, reveals vault_door dialogue, armed at runtime). " +
                      "5 Vault Operative enemies (neutral tint, nonLethal). " +
                      "4 steps: hack archive lock → defeat 5 vault operatives (vault_barks bark) → purged_entry dialogue → transition to Ash And Void.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Ash And Void", priority = 194)]
        public static void BuildEp19AshAndVoid()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ash And Void: red alarm + fire-orange tower crown.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.40f, 0.30f); // red alarm + fire-orange key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.14f, 0.10f); // dark warm ambient

            // Alarm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.18f, 0.12f);
            RenderSettings.fogDensity = 0.020f;

            // Two red/orange accent point lights.
            BuildAccentPointLight("AshAndVoidLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.50f, 0.30f), intensity: 0.90f, range: 10f);
            BuildAccentPointLight("AshAndVoidLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.95f, 0.45f, 0.25f), intensity: 0.85f, range: 9f);

            // ---- Tower crown platform floor and walls ----
            var platformGo = new GameObject("TowerCrown");
            var platform = platformGo.transform;
            var platformMetal = new Color(0.40f, 0.35f, 0.32f);
            var platformDark = new Color(0.25f, 0.20f, 0.18f);

            // Main platform floor (14 x 18).
            BuildFloorCeiling(platform, "CrownFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 18f), platformMetal, platformDark);

            // Platform walls.
            BuildWall(platform, "CrownWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(platform, "CrownWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // ArchiveCore prop.
            BuildProp(platform, "ArchiveCore", new Vector3(-4f, 1.5f, 12f), new Vector3(1.5f, 2f, 1.5f), new Color(0.60f, 0.50f, 0.40f));

            // Detonator prop.
            BuildProp(platform, "Detonator", new Vector3(4f, 1.5f, 12f), new Vector3(1f, 1.5f, 1f), new Color(0.70f, 0.35f, 0.25f));

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

            // ---- Tara StoryNpc capsule ----
            var taraGo = new GameObject("Tara_NPC");
            taraGo.transform.SetParent(platform, false);
            taraGo.transform.position = new Vector3(-2f, 0f, 6f);

            var taraBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            taraBody.name = "Body";
            taraBody.transform.SetParent(taraGo.transform, false);
            TintShared(taraBody.GetComponent<Renderer>(), new Color(0.50f, 0.50f, 0.55f)); // neutral tint

            var taraNpc = taraGo.AddComponent<StoryNpc>();
            var taraNpcSo = new SerializedObject(taraNpc);
            taraNpcSo.FindProperty("displayName").stringValue = "Tara";
            taraNpcSo.FindProperty("remote").boolValue = false;
            taraNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogues ----
            var hammerClauseDialogue = BuildEp19DialoguePlayer("Dialogue_HammerClause", new Vector3(0f, 1.5f, 4f), "hammer_clause");
            var hammerClauseSo = new SerializedObject(hammerClauseDialogue);
            hammerClauseSo.FindProperty("playOnStart").boolValue = true;
            hammerClauseSo.ApplyModifiedPropertiesWithoutUndo();

            var towerCrownDialogue = BuildEp19DialoguePlayer("Dialogue_TowerCrown", new Vector3(0f, 1.5f, 10f), "tower_crown");

            // ---- 4 Colony guards: 1 wave ----
            var guardColor = new Color(0.55f, 0.40f, 0.35f); // rust-brown tint
            var guardWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f)
            };

            var guardWaveHealths = new List<Health>();
            foreach (var pos in guardWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, guardColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                guardWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var guardSpawner = BuildEp03WaveSpawner("GuardSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { guardWaveHealths },
                new[] { BuildEp19DialoguePlayer("Dialogue_GuardBarks", new Vector3(0f, 1.5f, 8f), "guard_barks") });

            // Transition box: "TO THE SIGNAL — DERELICT STATION".
            var darkCorridorsBoxGo = BuildTransitionBox("ToDarkCorridorsBox", new Vector3(0f, 1.2f, 18.5f), "TO THE SIGNAL — DERELICT STATION",
                out var darkCorridorsBtn, out var darkCorridorsTransition);
            var dcSo = new SerializedObject(darkCorridorsTransition);
            dcSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19DarkCorridorsSceneName;
            dcSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(darkCorridorsBtn.onClick,
                new UnityEngine.Events.UnityAction(darkCorridorsTransition.LoadOnFootScene));
            darkCorridorsBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue hammer_clause (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Hammer Clause";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = hammerClauseDialogue;

            // Step 1: DefeatWaves — 4 colony guards.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Colony Guards (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = guardSpawner;

            // Step 2: Dialogue tower_crown.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Tower Crown";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = towerCrownDialogue;

            // Step 3: Prompt — transition to Dark Corridors.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Board to Derelict Station";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = darkCorridorsBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19AshAndVoidScenePath);
            EnsureScenesInBuild(Galaxy3Ep19AshAndVoidScenePath, Galaxy3Ep19DarkCorridorsScenePath);

            Debug.Log($"[Space Samurai] EP19 Ash And Void scene built at {Galaxy3Ep19AshAndVoidScenePath}. " +
                      "Layout: red alarm + fire-orange tower crown with warm orange/red lighting, dark metal floor/walls, archive core and detonator props. " +
                      "Tara NPC (neutral capsule, NO Health, StoryNpc displayName). " +
                      "4 Colony Guard enemies (rust-brown tint, nonLethal). " +
                      "4 steps: hammer_clause (auto) → defeat 4 colony guards (guard_barks bark) → tower_crown dialogue → transition to Derelict Station.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Dark Corridors", priority = 195)]
        public static void BuildEp19DarkCorridors()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Dark Corridors: dim grey-blue derelict station (duel arena).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.55f, 0.65f); // dim grey-blue key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.20f); // dim blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.24f, 0.30f);
            RenderSettings.fogDensity = 0.022f;

            // Two dim blue accent point lights.
            BuildAccentPointLight("DarkCorridorsLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.40f, 0.55f, 0.75f), intensity: 0.70f, range: 10f);
            BuildAccentPointLight("DarkCorridorsLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.35f, 0.50f, 0.70f), intensity: 0.65f, range: 9f);

            // ---- Corridor floor and walls ----
            var corridorGo = new GameObject("DarkCorridor");
            var corridor = corridorGo.transform;
            var stationMetal = new Color(0.30f, 0.32f, 0.35f);
            var stationDark = new Color(0.18f, 0.20f, 0.24f);

            // Main corridor floor (8 x 20).
            BuildFloorCeiling(corridor, "CorridorFloor", new Vector3(0f, 0f, 10f), new Vector3(8f, 0f, 20f), stationMetal, stationDark);

            // Corridor walls.
            BuildWall(corridor, "CorridorWall_W", new Vector3(-4f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(corridor, "CorridorWall_E", new Vector3(4f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

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

            // ---- ONE duel enemy: The Operative ----
            var duelist = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var duelistRenderer = duelist.GetComponent<Renderer>();
            if (duelistRenderer != null) TintShared(duelistRenderer, new Color(0.60f, 0.60f, 0.65f)); // pale grey-blue tint
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
            dNpcSo.FindProperty("displayName").stringValue = "Operative";
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
            var signalRemnantDialogue = BuildEp19DialoguePlayer("Dialogue_SignalRemnant", new Vector3(0f, 1.5f, 6f), "signal_remnant");
            var signalSo = new SerializedObject(signalRemnantDialogue);
            signalSo.FindProperty("playOnStart").boolValue = true;
            signalSo.ApplyModifiedPropertiesWithoutUndo();

            var debtPaidDialogue = BuildEp19DialoguePlayer("Dialogue_DebtPaid", new Vector3(0f, 1.5f, 14f), "debt_paid");

            // Transition box: "BOARD — THE CORSAIR".
            var corsairBoxGo = BuildTransitionBox("ToCorsairBox", new Vector3(0f, 1.2f, 20.5f), "BOARD — THE CORSAIR",
                out var corsairBtn, out var corsairTransition);
            var crSo = new SerializedObject(corsairTransition);
            crSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19CorsairSceneName;
            crSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(corsairBtn.onClick,
                new UnityEngine.Events.UnityAction(corsairTransition.LoadOnFootScene));
            corsairBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue signal_remnant (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Signal Remnant";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = signalRemnantDialogue;

            // Step 1: Prompt (null) — the duel (DuelYield.onAccepted advances it).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Duel The Operative (yield via DuelYield)";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 2: Dialogue debt_paid.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Debt Paid";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = debtPaidDialogue;

            // Step 3: Prompt — transition to Corsair.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Board the Corsair";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = corsairBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19DarkCorridorsScenePath);
            EnsureScenesInBuild(Galaxy3Ep19DarkCorridorsScenePath, Galaxy3Ep19CorsairScenePath);

            Debug.Log($"[Space Samurai] EP19 Dark Corridors scene built at {Galaxy3Ep19DarkCorridorsScenePath}. " +
                      "Layout: dim grey-blue derelict station with dim blue lighting, dark metal floor/walls, light fog. " +
                      "1 Duelist enemy (The Operative, pale grey-blue tint, nonLethal, DuelYield yield at 30%, ACTIVE from start). " +
                      "4 steps: signal_remnant (auto) → duel Prompt (onAccepted → AdvanceFromPrompt) → debt_paid dialogue → transition to Corsair.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Corsair", priority = 196)]
        public static void BuildEp19Corsair()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Corsair: cool blue observation lounge (the finale).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.70f, 0.85f, 1.0f); // cool blue key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.28f, 0.35f); // cool blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.40f, 0.48f);
            RenderSettings.fogDensity = 0.018f;

            // Two cool blue accent lights.
            BuildAccentPointLight("CorsairLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.60f, 0.80f, 1f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("CorsairLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.55f, 0.75f, 0.95f), intensity: 0.75f, range: 9f);

            // ---- Corsair observation lounge floor and walls ----
            var loungeGo = new GameObject("ObservationLounge");
            var lounge = loungeGo.transform;
            var corsairMetal = new Color(0.32f, 0.38f, 0.45f);
            var corsairDark = new Color(0.18f, 0.25f, 0.32f);

            // Main lounge floor (12 x 20).
            BuildFloorCeiling(lounge, "CorsairFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), corsairMetal, corsairDark);

            // Lounge walls.
            BuildWall(lounge, "CorsairWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(lounge, "CorsairWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Console panel prop.
            BuildProp(lounge, "ConsolePanel", new Vector3(-3f, 1.5f, 8f), new Vector3(2f, 1.5f, 0.3f), new Color(0.45f, 0.50f, 0.55f));

            // Observation window prop.
            BuildProp(lounge, "ObservationWindow", new Vector3(3f, 2f, 20f), new Vector3(3f, 2f, 0.1f), new Color(0.70f, 0.85f, 1f));

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

            // ---- Cassie-04 StoryNpc capsule ----
            var cassieGo = new GameObject("Cassie04_NPC");
            cassieGo.transform.SetParent(lounge, false);
            cassieGo.transform.position = new Vector3(-2f, 0f, 6f);

            var cassieBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cassieBody.name = "Body";
            cassieBody.transform.SetParent(cassieGo.transform, false);
            TintShared(cassieBody.GetComponent<Renderer>(), new Color(0.70f, 0.60f, 0.65f)); // cool mauve tint

            var cassieNpc = cassieGo.AddComponent<StoryNpc>();
            var cassieNpcSo = new SerializedObject(cassieNpc);
            cassieNpcSo.FindProperty("displayName").stringValue = "Cassie-04";
            cassieNpcSo.FindProperty("remote").boolValue = false;
            cassieNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Takeshi StoryNpc capsule ----
            var takeshiGo = new GameObject("Takeshi_NPC");
            takeshiGo.transform.SetParent(lounge, false);
            takeshiGo.transform.position = new Vector3(2f, 0f, 6f);

            var takeshiBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            takeshiBody.name = "Body";
            takeshiBody.transform.SetParent(takeshiGo.transform, false);
            TintShared(takeshiBody.GetComponent<Renderer>(), new Color(0.75f, 0.73f, 0.70f)); // pale tint

            var takeshiNpc = takeshiGo.AddComponent<StoryNpc>();
            var takeshiNpcSo = new SerializedObject(takeshiNpc);
            takeshiNpcSo.FindProperty("displayName").stringValue = "Takeshi";
            takeshiNpcSo.FindProperty("remote").boolValue = false;
            takeshiNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players (NO enemies — pure denouement) ----
            var operativesNameDialogue = BuildEp19DialoguePlayer("Dialogue_OperativesName", new Vector3(0f, 1.5f, 2f), "operatives_name");
            var operativesNameSo = new SerializedObject(operativesNameDialogue);
            operativesNameSo.FindProperty("playOnStart").boolValue = true;
            operativesNameSo.ApplyModifiedPropertiesWithoutUndo();

            var weightOfNamesDialogue = BuildEp19DialoguePlayer("Dialogue_WeightOfNames", new Vector3(0f, 1.5f, 8f), "weight_of_names");
            var chooseMyselfDialogue = BuildEp19DialoguePlayer("Dialogue_ChooseMyself", new Vector3(0f, 1.5f, 12f), "choose_myself");
            var finalChoiceDialogue = BuildEp19DialoguePlayer("Dialogue_FinalChoice", new Vector3(0f, 1.5f, 16f), "final_choice");

            // Finale transition box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 20.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set flags: ep19_complete, cassie04_recruited.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep19_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "cassie04_recruited";
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
            stepsProp.arraySize = 5;

            // Step 0: Dialogue operatives_name (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Operatives Name";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = operativesNameDialogue;

            // Step 1: Dialogue weight_of_names.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Weight of Names";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = weightOfNamesDialogue;

            // Step 2: Dialogue choose_myself.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Choose Myself";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = chooseMyselfDialogue;

            // Step 3: Dialogue final_choice.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Final Choice";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = finalChoiceDialogue;

            // Step 4: Prompt — return to space with ep19_complete + cassie04_recruited flags.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep19_complete + cassie04_recruited)";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19CorsairScenePath);
            EnsureScenesInBuild(Galaxy3Ep19CorsairScenePath);

            Debug.Log($"[Space Samurai] EP19 Corsair scene built at {Galaxy3Ep19CorsairScenePath}. " +
                      "Layout: Corsair observation lounge in hyperspace with cool blue lighting, dark metal floor/walls, console and window props, light fog. " +
                      "Cassie-04 NPC (cool mauve capsule, NO Health, StoryNpc displayName) and Takeshi NPC (pale capsule, NO Health, StoryNpc displayName), positioned as if sparring. " +
                      "NO enemies (pure denouement, dialogue-only finale). " +
                      "5 steps: operatives_name (auto) → weight_of_names dialogue → choose_myself dialogue → final_choice dialogue → " +
                      "return to hub Prompt (sets ep19_complete + cassie04_recruited via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 19 FINALE (sets ep19_complete + cassie04_recruited; Galaxy 3 complete).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP19 Scenes", priority = 197)]
        public static void BuildAllEp19Scenes()
        {
            BuildEp19CargoHold();
            BuildEp19AsteroidPursuit();
            BuildEp19MineShaft();
            BuildEp19VaultBelow();
            BuildEp19AshAndVoid();
            BuildEp19DarkCorridors();
            BuildEp19Corsair();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP19 scenes built + inputs rewired.");
        }
    }
}

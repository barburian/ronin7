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
    /// EP18 "The Drowning Deep" on-foot scene builders. Builds three core episodes:
    /// - Station: cold ice-cavern Tide-Baron station bar with bartender NPC and 3 enforcers
    /// - VaultOuter: Abyssal Vault outer corridors with bioluminescent green light, 2 NPCs, and FloodingWaterHazard
    /// - ExtractionChamber: extraction chamber half-flooded with 3 researchers and aggressive FloodingWaterHazard
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP18 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep18" and loads lines from Ep18Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp18DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep18Lines.Get(setId), advanceRef, setId, clipPrefix: "ep18");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Station", priority = 180)]
        public static void BuildEp18Station()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Station: cold ice-cavern Tide-Baron station bar.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.5f, 0.7f, 0.85f); // cyan/blue cold key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.35f, 0.45f); // dark blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.50f, 0.60f);
            RenderSettings.fogDensity = 0.014f;

            // Two cyan accent point lights.
            BuildAccentPointLight("StationLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.45f, 0.75f, 0.95f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("StationLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.40f, 0.70f, 0.90f), intensity: 0.70f, range: 9f);

            // ---- Station bar floor and structure ----
            var stationGo = new GameObject("Station");
            var station = stationGo.transform;
            var iceBlue = new Color(0.45f, 0.60f, 0.75f);
            var darkerIce = new Color(0.30f, 0.45f, 0.60f);

            // Main station bar floor.
            BuildFloorCeiling(station, "StationFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 18f), iceBlue, darkerIce);

            // Station bar walls.
            BuildWall(station, "StationWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(station, "StationWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // Bar props: counter cube, stools.
            var barColor = new Color(0.50f, 0.65f, 0.78f);
            BuildProp(station, "BarCounter", new Vector3(0f, 0.8f, 5f), new Vector3(3f, 1f, 1.5f), barColor);
            BuildProp(station, "Stool1", new Vector3(-1.5f, 0.5f, 4.5f), new Vector3(0.5f, 0.9f, 0.5f), new Color(0.55f, 0.60f, 0.70f));
            BuildProp(station, "Stool2", new Vector3(1.5f, 0.5f, 4.5f), new Vector3(0.5f, 0.9f, 0.5f), new Color(0.55f, 0.60f, 0.70f));

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

            // ---- Bartender NPC ----
            var bartenderGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bartenderGo.name = "Bartender";
            Object.DestroyImmediate(bartenderGo.GetComponent<Collider>());
            bartenderGo.transform.position = new Vector3(0f, 0f, 3f);
            bartenderGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(bartenderGo.GetComponent<Renderer>(), new Color(0.35f, 0.70f, 0.80f)); // teal tint
            var bartenderNpc = bartenderGo.AddComponent<StoryNpc>();
            var bnSo = new SerializedObject(bartenderNpc);
            bnSo.FindProperty("displayName").stringValue = "Bartender";
            bnSo.FindProperty("remote").boolValue = false;
            bnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var bartenderInfoDialogue = BuildEp18DialoguePlayer("Dialogue_BartenderInfo", new Vector3(0f, 1.5f, 2f), "bartender_info");
            var biSo = new SerializedObject(bartenderInfoDialogue);
            biSo.FindProperty("playOnStart").boolValue = true;
            biSo.ApplyModifiedPropertiesWithoutUndo();

            var corridorConfessionDialogue = BuildEp18DialoguePlayer("Dialogue_CorridorConfession", new Vector3(0f, 1.5f, 8f), "corridor_confession");

            // ---- 3 Tide Baron enforcers: 1 wave ----
            var enforcerColor = new Color(0.55f, 0.65f, 0.75f); // rust/teal tint
            var enforcerWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(0f, 0f, 6.5f),
                new Vector3(1.5f, 0f, 7f)
            };

            var enforcerWaveHealths = new List<Health>();
            foreach (var pos in enforcerWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                enforcerWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var stationBarksDialogue = BuildEp18DialoguePlayer("Dialogue_StationBarks", new Vector3(0f, 1.5f, 8f), "station_enforcer_barks");
            var enforcerSpawner = BuildWaveSpawner("EnforcerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { enforcerWaveHealths },
                new[] { stationBarksDialogue });

            // Transition box: "DESCEND — THE VAULT".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 18.5f), "DESCEND — THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep18VaultOuterSceneName;
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

            // Step 0: Dialogue bartender_info (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Bartender Info";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = bartenderInfoDialogue;

            // Step 1: DefeatWaves — 3 Tide Baron enforcers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Tide Baron Enforcers (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerSpawner;

            // Step 2: Dialogue corridor_confession.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Corridor Confession";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = corridorConfessionDialogue;

            // Step 3: Prompt — transition to Vault.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Vault";
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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18StationScenePath);
            EnsureScenesInBuild(Galaxy3Ep18StationScenePath, Galaxy3Ep18VaultOuterScenePath);

            Debug.Log($"[Space Samurai] EP18 Station scene built at {Galaxy3Ep18StationScenePath}. " +
                      "Layout: cold ice-cavern Tide-Baron station bar with cyan/blue cold lighting, bar counter + stools. " +
                      "Bartender NPC (teal tint, no Health). " +
                      "3 Tide Baron Enforcers (rust/teal tint, nonLethal). " +
                      "4 steps: bartender_info (auto) → defeat 3 Tide Baron Enforcers (station_enforcer_barks bark) → " +
                      "corridor_confession dialogue → transition to Vault Outer.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Vault Outer", priority = 181)]
        public static void BuildEp18VaultOuter()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // VaultOuter: Abyssal Vault outer corridors with bioluminescent green light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.30f, 0.70f, 0.50f); // sickly bioluminescent green
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.50f, 0.35f); // green ambient

            // Dense fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.55f, 0.40f);
            RenderSettings.fogDensity = 0.022f;

            // Two green accent lights.
            BuildAccentPointLight("VaultLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.40f, 0.80f, 0.60f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("VaultLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.35f, 0.75f, 0.55f), intensity: 0.70f, range: 9f);

            // ---- Vault outer floor and structure ----
            var vaultGo = new GameObject("VaultOuter");
            var vault = vaultGo.transform;
            var vaultGreen = new Color(0.35f, 0.60f, 0.48f);
            var darkerVault = new Color(0.20f, 0.40f, 0.32f);

            // Main vault floor.
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 24f), vaultGreen, darkerVault);

            // Vault walls.
            BuildWall(vault, "VaultWall_W", new Vector3(-6f, 1.5f, 12f), new Vector3(0.2f, 3f, 24f));
            BuildWall(vault, "VaultWall_E", new Vector3(6f, 1.5f, 12f), new Vector3(0.2f, 3f, 24f));

            // Records data-card prop (unlit cube) and reinforced quarantine glass prop.
            var cardColor = new Color(0.20f, 0.20f, 0.22f);
            BuildProp(vault, "DataCard", new Vector3(-2f, 0.6f, 6f), new Vector3(0.8f, 0.4f, 1.2f), cardColor);

            var glassGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glassGo.name = "QuarantineGlass";
            Object.DestroyImmediate(glassGo.GetComponent<Collider>());
            glassGo.transform.SetParent(vault, false);
            glassGo.transform.position = new Vector3(2f, 1f, 18f);
            glassGo.transform.localScale = new Vector3(2f, 2f, 0.3f);
            var glassMat = MakeUnlitMaterial(new Color(0.35f, 0.75f, 0.65f));
            var glassMat2 = new Material(glassMat);
            glassMat2.color = new Color(0.35f, 0.75f, 0.65f);
            glassGo.GetComponent<Renderer>().sharedMaterial = glassMat2;

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

            // ---- Takeshi NPC behind the glass ----
            var takeshiGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            takeshiGo.name = "Takeshi";
            Object.DestroyImmediate(takeshiGo.GetComponent<Collider>());
            takeshiGo.transform.position = new Vector3(2f, 0f, 16f);
            takeshiGo.transform.localScale = new Vector3(0.5f, 1.6f, 0.5f);
            TintShared(takeshiGo.GetComponent<Renderer>(), new Color(0.70f, 0.75f, 0.80f)); // pale/thin tint
            var takeshiNpc = takeshiGo.AddComponent<StoryNpc>();
            var tnSo = new SerializedObject(takeshiNpc);
            tnSo.FindProperty("displayName").stringValue = "Takeshi";
            tnSo.FindProperty("remote").boolValue = false;
            tnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Prisoner-Worker NPC near mid-corridor ----
            var prisonerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prisonerGo.name = "PrisonerWorker";
            Object.DestroyImmediate(prisonerGo.GetComponent<Collider>());
            prisonerGo.transform.position = new Vector3(-1.5f, 0f, 10f);
            prisonerGo.transform.localScale = new Vector3(0.6f, 1.7f, 0.6f);
            TintShared(prisonerGo.GetComponent<Renderer>(), new Color(0.45f, 0.55f, 0.60f)); // subdued green
            var prisonerNpc = prisonerGo.AddComponent<StoryNpc>();
            var pnSo = new SerializedObject(prisonerNpc);
            pnSo.FindProperty("displayName").stringValue = "Prisoner-Worker";
            pnSo.FindProperty("remote").boolValue = false;
            pnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Environmental Flood Hazard ----
            var floodGo = new GameObject("FloodWater");
            floodGo.transform.SetParent(vault, false);
            floodGo.transform.position = new Vector3(0f, -2f, 12f);
            var floodCollider = floodGo.AddComponent<BoxCollider>();
            floodCollider.isTrigger = true;
            floodGo.transform.localScale = new Vector3(8f, 4f, 20f);

            var floodVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floodVis.name = "FloodVisuals";
            Object.DestroyImmediate(floodVis.GetComponent<Collider>());
            floodVis.transform.SetParent(floodGo.transform, false);
            floodVis.transform.position = Vector3.zero;
            floodVis.transform.localScale = Vector3.one;
            floodVis.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.25f, 0.65f, 0.80f));

            var floodHaz = floodGo.AddComponent<Ronin7.World.FloodingWaterHazard>();
            floodHaz.Configure(playerHealth);
            var fhSo = new SerializedObject(floodHaz);
            fhSo.FindProperty("damagePerTick").floatValue = 5f;
            fhSo.FindProperty("damageInterval").floatValue = 1.5f;
            fhSo.FindProperty("riseDuration").floatValue = 18f;
            fhSo.FindProperty("startY").floatValue = -3f;
            fhSo.FindProperty("endY").floatValue = 1.2f;
            fhSo.FindProperty("autoStart").boolValue = true;
            fhSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var prisonerWorkerDialogue = BuildEp18DialoguePlayer("Dialogue_PrisonerWorker", new Vector3(0f, 1.5f, 2f), "prisoner_worker");
            var pwSo = new SerializedObject(prisonerWorkerDialogue);
            pwSo.FindProperty("playOnStart").boolValue = true;
            pwSo.ApplyModifiedPropertiesWithoutUndo();

            var quarantineRecognitionDialogue = BuildEp18DialoguePlayer("Dialogue_QuarantineRecognition", new Vector3(0f, 1.5f, 14f), "quarantine_recognition");

            // Transition box: "BREACH — THE EXTRACTION CHAMBER".
            var extractionBoxGo = BuildTransitionBox("ToExtractionBox", new Vector3(0f, 1.2f, 24.5f), "BREACH — THE EXTRACTION CHAMBER",
                out var extractionBtn, out var extractionTransition);
            var ebSo = new SerializedObject(extractionTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy3Ep18ExtractionSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(extractionBtn.onClick,
                new UnityEngine.Events.UnityAction(extractionTransition.LoadOnFootScene));
            extractionBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue prisoner_worker (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Prisoner Worker";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = prisonerWorkerDialogue;

            // Step 1: Dialogue quarantine_recognition.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Quarantine Recognition";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = quarantineRecognitionDialogue;

            // Step 2: Prompt — transition to Extraction Chamber.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Breach to Extraction Chamber";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = extractionBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18VaultOuterScenePath);
            EnsureScenesInBuild(Galaxy3Ep18VaultOuterScenePath, Galaxy3Ep18ExtractionScenePath);

            Debug.Log($"[Space Samurai] EP18 Vault Outer scene built at {Galaxy3Ep18VaultOuterScenePath}. " +
                      "Layout: Abyssal Vault outer corridors with sickly green bioluminescent light, dense fog, " +
                      "data-card prop, quarantine glass. Takeshi (pale, no Health) and Prisoner-Worker NPCs. " +
                      "FloodingWaterHazard (damagePerTick 5, damageInterval 1.5, riseDuration 18, startY -3, endY 1.2). " +
                      "3 steps: prisoner_worker (auto) → quarantine_recognition dialogue → transition to Extraction Chamber.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP18 Extraction Chamber", priority = 182)]
        public static void BuildEp18ExtractionChamber()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ExtractionChamber: extraction chamber half-flooded with cold teal + emergency amber accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.70f, 0.80f); // cold teal key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.50f, 0.60f); // teal ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.38f, 0.58f, 0.68f);
            RenderSettings.fogDensity = 0.016f;

            // Two teal + amber accent lights.
            BuildAccentPointLight("ExtractionLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.45f, 0.80f, 0.90f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ExtractionLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.65f, 0.35f), intensity: 0.70f, range: 9f); // amber accent

            // ---- Extraction chamber floor and structure ----
            var chamberGo = new GameObject("ExtractionChamber");
            var chamber = chamberGo.transform;
            var chamberTeal = new Color(0.40f, 0.65f, 0.75f);
            var darkerChamber = new Color(0.25f, 0.45f, 0.55f);

            // Main chamber floor.
            BuildFloorCeiling(chamber, "ChamberFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 20f), chamberTeal, darkerChamber);

            // Chamber walls.
            BuildWall(chamber, "ChamberWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(chamber, "ChamberWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Extraction rig prop cube cluster.
            var rigColor = new Color(0.45f, 0.55f, 0.65f);
            BuildProp(chamber, "ExtractionRig_Main", new Vector3(0f, 1.2f, 18f), new Vector3(2f, 2f, 1f), rigColor);
            BuildProp(chamber, "ExtractionRig_Arm1", new Vector3(-1.5f, 1.5f, 16f), new Vector3(0.5f, 1.5f, 0.5f), rigColor);
            BuildProp(chamber, "ExtractionRig_Arm2", new Vector3(1.5f, 1.5f, 16f), new Vector3(0.5f, 1.5f, 0.5f), rigColor);

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

            // ---- Environmental Flood Hazard (aggressive) ----
            var floodGo = new GameObject("FloodWater");
            floodGo.transform.SetParent(chamber, false);
            floodGo.transform.position = new Vector3(0f, -1.5f, 10f);
            var floodCollider = floodGo.AddComponent<BoxCollider>();
            floodCollider.isTrigger = true;
            floodGo.transform.localScale = new Vector3(8f, 4f, 20f);

            var floodVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floodVis.name = "FloodVisuals";
            Object.DestroyImmediate(floodVis.GetComponent<Collider>());
            floodVis.transform.SetParent(floodGo.transform, false);
            floodVis.transform.position = Vector3.zero;
            floodVis.transform.localScale = Vector3.one;
            floodVis.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.20f, 0.60f, 0.78f));

            var floodHaz = floodGo.AddComponent<Ronin7.World.FloodingWaterHazard>();
            floodHaz.Configure(playerHealth);
            var fhSo = new SerializedObject(floodHaz);
            fhSo.FindProperty("damagePerTick").floatValue = 6f;
            fhSo.FindProperty("damageInterval").floatValue = 1.5f;
            fhSo.FindProperty("riseDuration").floatValue = 14f;
            fhSo.FindProperty("startY").floatValue = -2.5f;
            fhSo.FindProperty("endY").floatValue = 1.5f;
            fhSo.FindProperty("autoStart").boolValue = true;
            fhSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var failsafeRevealDialogue = BuildEp18DialoguePlayer("Dialogue_FailsafeReveal", new Vector3(0f, 1.5f, 2f), "failsafe_reveal");
            var frSo = new SerializedObject(failsafeRevealDialogue);
            frSo.FindProperty("playOnStart").boolValue = true;
            frSo.ApplyModifiedPropertiesWithoutUndo();

            var vaultInventoryDialogue = BuildEp18DialoguePlayer("Dialogue_VaultInventory", new Vector3(0f, 1.5f, 14f), "vault_inventory");

            // ---- 3 Tide Baron researchers: 1 wave ----
            var researcherColor = new Color(0.45f, 0.70f, 0.65f); // green-tinted augmented-frame look
            var researcherWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };

            var researcherWaveHealths = new List<Health>();
            foreach (var pos in researcherWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, researcherColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                researcherWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var extractionBarksDialogue = BuildEp18DialoguePlayer("Dialogue_ExtractionBarks", new Vector3(0f, 1.5f, 8f), "extraction_barks");
            var researcherSpawner = BuildWaveSpawner("ResearcherSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { researcherWaveHealths },
                new[] { extractionBarksDialogue });

            // Transition box: "DESCEND — THE COMMAND CORE".
            var commandCoreBoxGo = BuildTransitionBox("ToCommandCoreBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND — THE COMMAND CORE",
                out var commandCoreBtn, out var commandCoreTransition);
            var ccbSo = new SerializedObject(commandCoreTransition);
            ccbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep18CommandCoreSceneName;
            ccbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(commandCoreBtn.onClick,
                new UnityEngine.Events.UnityAction(commandCoreTransition.LoadOnFootScene));
            commandCoreBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue failsafe_reveal (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Failsafe Reveal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = failsafeRevealDialogue;

            // Step 1: DefeatWaves — 3 Tide Baron researchers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Tide Baron Researchers (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = researcherSpawner;

            // Step 2: Dialogue vault_inventory.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Vault Inventory";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = vaultInventoryDialogue;

            // Step 3: Prompt — transition to Command Core.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Command Core";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = commandCoreBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep18ExtractionScenePath);
            EnsureScenesInBuild(Galaxy3Ep18ExtractionScenePath, Galaxy3Ep18CommandCoreScenePath);

            Debug.Log($"[Space Samurai] EP18 Extraction Chamber scene built at {Galaxy3Ep18ExtractionScenePath}. " +
                      "Layout: extraction chamber half-flooded with cold teal + emergency amber accents, extraction rig prop cluster. " +
                      "3 Tide Baron Researchers (green-tinted augmented, nonLethal). " +
                      "Aggressive FloodingWaterHazard (damagePerTick 6, damageInterval 1.5, riseDuration 14, startY -2.5, endY 1.5). " +
                      "4 steps: failsafe_reveal (auto) → defeat 3 Tide Baron Researchers (extraction_barks bark) → " +
                      "vault_inventory dialogue → transition to Command Core.");
        }
    }
}

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
    /// EP21 "The Crimson Sleep" scene builders for the final four episodes:
    /// - Testimony: orphanage dreamscape with one illusory shadow (internal reckoning, no combat)
    /// - Expulsion: greenhouse collapsing back to reality with Kade NPC and 4 Crimson Lotus guards
    /// - ShadowInCode: ship cargo hold with Vess NPC and 3 Dominion Elite Operatives
    /// - Awakening: lotus facility core (THE EP21 FINALE), Ronin-8 NPC, 5 Lotus enforcers, sets ep21_complete + ronin8_freed flags
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp21DialoguePlayer helper is already declared in Ep21Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Testimony", priority = 223)]
        public static void BuildEp21Testimony()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Testimony: orphanage dreamscape (soft warm light, gentle fog).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.60f, 0.55f); // soft warm key light
            light.intensity = 0.44f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.15f); // warm dark ambient

            // Gentle fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.25f);
            RenderSettings.fogDensity = 0.020f;

            // Two warm accent lights.
            BuildAccentPointLight("TestimonyLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.75f, 0.55f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("TestimonyLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.70f, 0.50f), intensity: 0.70f, range: 9f);

            // ---- Orphanage floor and structure ----
            var orphanageGo = new GameObject("Orphanage");
            var orphanage = orphanageGo.transform;
            var warmMetal = new Color(0.52f, 0.45f, 0.40f);
            var darkerWarm = new Color(0.32f, 0.27f, 0.24f);

            // Main floor (10 x 19).
            BuildFloorCeiling(orphanage, "OrphanageFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 19f), warmMetal, darkerWarm);

            // Walls.
            BuildWall(orphanage, "OrphanageWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));
            BuildWall(orphanage, "OrphanageWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 19f));

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

            // ---- 1 Illusory Obedient Shadow DreamPhantom ----
            var shadowGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shadowGo.name = "Obedient Shadow";
            Object.DestroyImmediate(shadowGo.GetComponent<Collider>());
            shadowGo.transform.position = new Vector3(0f, 0f, 10f);
            shadowGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(shadowGo.GetComponent<Renderer>(), new Color(0.5f, 0.5f, 0.6f)); // Cipher steel tint
            shadowGo.SetActive(true);

            var shadowDp = shadowGo.AddComponent<DreamPhantom>();
            shadowDp.SetIllusory(true);

            // ---- DreamReckoningTrigger ----
            var shadowReckoningGo = new GameObject("ShadowReckoningTrigger");
            var shadowReckoning = shadowReckoningGo.AddComponent<DreamReckoningTrigger>();
            var srSo = new SerializedObject(shadowReckoning);
            var shadowPhantomsProp = srSo.FindProperty("phantoms");
            shadowPhantomsProp.arraySize = 1;
            shadowPhantomsProp.GetArrayElementAtIndex(0).objectReferenceValue = shadowDp;
            srSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var testimonyDialogue = BuildEp21DialoguePlayer("Dialogue_Testimony", new Vector3(0f, 1.5f, 2f), "testimony");
            var tSo = new SerializedObject(testimonyDialogue);
            tSo.FindProperty("playOnStart").boolValue = true;
            tSo.ApplyModifiedPropertiesWithoutUndo();

            var labyrinthDialogue = BuildEp21DialoguePlayer("Dialogue_Labyrinth", new Vector3(0f, 1.5f, 10f), "labyrinth");

            // Mirror reckoning prompt: "FACE THE MIRROR".
            var mirrorBoxGo = BuildTransitionBox("FaceMirrorBox", new Vector3(0f, 1.2f, 18.5f), "FACE THE MIRROR",
                out var mirrorBtn, out var mirrorTransition);
            var mbSo = new SerializedObject(mirrorTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21ExpulsionSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(mirrorBtn.onClick,
                new UnityEngine.Events.UnityAction(shadowReckoning.Acknowledge));
            UnityEventTools.AddPersistentListener(mirrorBtn.onClick,
                new UnityEngine.Events.UnityAction(mirrorTransition.LoadOnFootScene));
            mirrorBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue testimony (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Testimony";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = testimonyDialogue;

            // Step 1: Dialogue labyrinth.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Labyrinth";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = labyrinthDialogue;

            // Step 2: Prompt — mirror box.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Face the Mirror (dissolve shadow, move to Expulsion)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = mirrorBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21TestimonyScenePath);
            EnsureScenesInBuild(Galaxy3Ep21TestimonyScenePath, Galaxy3Ep21ExpulsionScenePath);

            Debug.Log($"[Space Samurai] EP21 Testimony scene built at {Galaxy3Ep21TestimonyScenePath}. " +
                      "Layout: orphanage dreamscape with soft warm lighting, gentle fog, dark metal floor/walls, PollenHazeController on Dreamscape. " +
                      "1 Illusory Obedient Shadow DreamPhantom (Cipher steel tint, SetIllusory=true, NO Health). " +
                      "DreamReckoningTrigger with single phantom. " +
                      "NO combat (internal reckoning). " +
                      "3 steps: testimony (auto) → labyrinth dialogue → face mirror prompt (triggers shadow acknowledge + transitions to Expulsion).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Expulsion", priority = 224)]
        public static void BuildEp21Expulsion()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Expulsion: greenhouse collapsing back to reality.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.42f, 0.72f); // purple key light
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.11f, 0.19f); // purple ambient

            // Thinning fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.28f, 0.16f, 0.30f);
            RenderSettings.fogDensity = 0.018f;

            // Two accent lights.
            BuildAccentPointLight("ExpulsionLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.35f, 0.65f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ExpulsionLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.9f, 0.60f, 0.85f), intensity: 0.70f, range: 9f);

            // ---- Greenhouse floor and structure ----
            var greenhouseGo = new GameObject("Greenhouse");
            var greenhouse = greenhouseGo.transform;
            var purpleMetal = new Color(0.50f, 0.32f, 0.42f);
            var darkerPurple = new Color(0.30f, 0.18f, 0.26f);

            // Main floor (11 x 20).
            BuildFloorCeiling(greenhouse, "GreenhouseFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 20f), purpleMetal, darkerPurple);

            // Walls.
            BuildWall(greenhouse, "GreenhouseWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(greenhouse, "GreenhouseWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

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

            // ---- Kade StoryNpc ----
            var kadeGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            kadeGo.name = "Kade";
            Object.DestroyImmediate(kadeGo.GetComponent<Collider>());
            kadeGo.transform.position = new Vector3(0f, 0f, 3f);
            kadeGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(kadeGo.GetComponent<Renderer>(), new Color(0.5f, 0.45f, 0.5f)); // gaunt pale tint
            var kadeNpc = kadeGo.AddComponent<StoryNpc>();
            var knSo = new SerializedObject(kadeNpc);
            knSo.FindProperty("displayName").stringValue = "Kade";
            knSo.FindProperty("remote").boolValue = false;
            knSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 4 Crimson Lotus Guards: 1 wave ----
            var lotusColor = new Color(0.75f, 0.30f, 0.45f);
            var lotusWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 8f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 11f)
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

            var lotusSpawner = BuildEp03WaveSpawner("LotusSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { lotusWaveHealths },
                new[] { BuildEp21DialoguePlayer("Dialogue_ExpulsionBarks", new Vector3(0f, 1.5f, 8f), "expulsion_barks") });

            // ---- Dialogue Players ----
            var expulsionDialogue = BuildEp21DialoguePlayer("Dialogue_Expulsion", new Vector3(0f, 1.5f, 2f), "expulsion");
            var eSo = new SerializedObject(expulsionDialogue);
            eSo.FindProperty("playOnStart").boolValue = true;
            eSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "EXTRACT — TO THE SHIP".
            var shipBoxGo = BuildTransitionBox("ToShipBox", new Vector3(0f, 1.2f, 20.5f), "EXTRACT — TO THE SHIP",
                out var shipBtn, out var shipTransition);
            var sbSo = new SerializedObject(shipTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21ShadowInCodeSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(shipBtn.onClick,
                new UnityEngine.Events.UnityAction(shipTransition.LoadOnFootScene));
            shipBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue expulsion (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Expulsion";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = expulsionDialogue;

            // Step 1: DefeatWaves — 4 lotus guards.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Crimson Lotus Guards (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = lotusSpawner;

            // Step 2: Prompt — transition to Ship.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Extract to Ship";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = shipBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21ExpulsionScenePath);
            EnsureScenesInBuild(Galaxy3Ep21ExpulsionScenePath, Galaxy3Ep21ShadowInCodeScenePath);

            Debug.Log($"[Space Samurai] EP21 Expulsion scene built at {Galaxy3Ep21ExpulsionScenePath}. " +
                      "Layout: greenhouse collapsing back to reality with thinning purple fog, dark metal floor/walls. " +
                      "Kade NPC (gaunt pale tint, no Health). " +
                      "4 Crimson Lotus Guards (crimson-purple tint, nonLethal). " +
                      "3 steps: expulsion (auto) → defeat 4 guards (expulsion_barks bark) → extract to ship.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Shadow in Code", priority = 225)]
        public static void BuildEp21ShadowInCode()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Shadow in Code: ship cargo hold.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.65f, 0.70f); // cool steel key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.18f); // cool steel ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.22f, 0.26f);
            RenderSettings.fogDensity = 0.015f;

            // Two accent lights: cyan and amber.
            BuildAccentPointLight("ShadowLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.55f, 0.85f, 1f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ShadowLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.65f, 0.35f), intensity: 0.70f, range: 9f);

            // ---- Cargo hold floor and structure ----
            var holdGo = new GameObject("CargoHold");
            var hold = holdGo.transform;
            var steelMetal = new Color(0.45f, 0.48f, 0.50f);
            var darkerSteel = new Color(0.28f, 0.30f, 0.32f);

            // Main hold floor (12 x 20).
            BuildFloorCeiling(hold, "HoldFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), steelMetal, darkerSteel);

            // Hold walls.
            BuildWall(hold, "HoldWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(hold, "HoldWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Data-core cyan cube props.
            var dataCoreColor = new Color(0.50f, 0.85f, 1f);
            BuildProp(hold, "DataCore1", new Vector3(-2.5f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), dataCoreColor);
            BuildProp(hold, "DataCore2", new Vector3(2.5f, 1.2f, 12f), new Vector3(0.8f, 1.5f, 0.8f), dataCoreColor);

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

            // ---- 3 Dominion Elite Operatives: 1 wave ----
            var eliteColor = new Color(0.5f, 0.55f, 0.62f); // steel tint
            var eliteWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };

            var eliteWaveHealths = new List<Health>();
            foreach (var pos in eliteWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                eliteWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var eliteSpawner = BuildEp03WaveSpawner("EliteSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { eliteWaveHealths },
                new[] { BuildEp21DialoguePlayer("Dialogue_EliteBarks", new Vector3(0f, 1.5f, 8f), "elite_barks") });

            // ---- Dialogue Players ----
            var shadowLedgerDialogue = BuildEp21DialoguePlayer("Dialogue_ShadowLedger", new Vector3(0f, 1.5f, 2f), "shadow_ledger");
            var slSo = new SerializedObject(shadowLedgerDialogue);
            slSo.FindProperty("playOnStart").boolValue = true;
            slSo.ApplyModifiedPropertiesWithoutUndo();

            var leashBreakDialogue = BuildEp21DialoguePlayer("Dialogue_LeashBreak", new Vector3(0f, 1.5f, 14f), "leash_break");

            // Transition box: "RETURN TO NARCOSIS".
            var narcosisBoxGo = BuildTransitionBox("ToNarcosisBox", new Vector3(0f, 1.2f, 20.5f), "RETURN TO NARCOSIS",
                out var narcosisBtn, out var narcosisTransition);
            var nbSo = new SerializedObject(narcosisTransition);
            nbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep21AwakeningSceneName;
            nbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(narcosisBtn.onClick,
                new UnityEngine.Events.UnityAction(narcosisTransition.LoadOnFootScene));
            narcosisBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue shadow_ledger (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Shadow Ledger";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = shadowLedgerDialogue;

            // Step 1: DefeatWaves — 3 elite operatives.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Elite Operatives (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = eliteSpawner;

            // Step 2: Dialogue leash_break.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Leash Break";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = leashBreakDialogue;

            // Step 3: Prompt — transition to Awakening.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Return to Narcosis";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = narcosisBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21ShadowInCodeScenePath);
            EnsureScenesInBuild(Galaxy3Ep21ShadowInCodeScenePath, Galaxy3Ep21AwakeningScenePath);

            Debug.Log($"[Space Samurai] EP21 Shadow in Code scene built at {Galaxy3Ep21ShadowInCodeScenePath}. " +
                      "Layout: ship cargo hold with cool steel lighting, light fog, dark metal floor/walls, cyan data-core props. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "3 Dominion Elite Operatives (steel tint, nonLethal). " +
                      "4 steps: shadow_ledger (auto) → defeat 3 elites (elite_barks bark) → leash_break dialogue → return to Narcosis prompt.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP21 Awakening", priority = 226)]
        public static void BuildEp21Awakening()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Awakening: lotus facility core (golden/amber + cyan).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.70f, 0.55f); // golden/amber key light
            light.intensity = 0.52f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.16f, 0.12f); // warm dark ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.20f);
            RenderSettings.fogDensity = 0.016f;

            // Two accent lights: cyan and golden.
            BuildAccentPointLight("AwakeningLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 1f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("AwakeningLight2", new Vector3(3f, 2.5f, 12f),
                new Color(1f, 0.75f, 0.40f), intensity: 0.75f, range: 9f);

            // ---- Lotus facility core floor and structure ----
            var coreGo = new GameObject("LotusCore");
            var core = coreGo.transform;
            var coreMetal = new Color(0.55f, 0.50f, 0.45f);
            var darkerCore = new Color(0.35f, 0.30f, 0.25f);

            // Main core floor (12 x 21).
            BuildFloorCeiling(core, "CoreFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), coreMetal, darkerCore);

            // Core walls.
            BuildWall(core, "CoreWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(core, "CoreWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Dreamstate-tank props: unlit cyan/gold cubes in rows.
            var cyanTank = new Color(0.50f, 0.85f, 1f);
            var goldTank = new Color(1f, 0.75f, 0.40f);
            BuildProp(core, "TankRow1_Cyan", new Vector3(-3f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cyanTank);
            BuildProp(core, "TankRow1_Gold", new Vector3(0f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), goldTank);
            BuildProp(core, "TankRow1_Cyan2", new Vector3(3f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cyanTank);
            BuildProp(core, "TankRow2_Gold", new Vector3(-2f, 1.2f, 12f), new Vector3(0.8f, 1.5f, 0.8f), goldTank);
            BuildProp(core, "TankRow2_Cyan", new Vector3(2f, 1.2f, 12f), new Vector3(0.8f, 1.5f, 0.8f), cyanTank);

            // Freed-operative capsule props (set-dressing).
            var capsuleColor = new Color(0.60f, 0.70f, 0.65f);
            BuildProp(core, "Capsule1", new Vector3(-3.5f, 0.8f, 15f), new Vector3(0.6f, 1.8f, 0.6f), capsuleColor);
            BuildProp(core, "Capsule2", new Vector3(3.5f, 0.8f, 16f), new Vector3(0.6f, 1.8f, 0.6f), capsuleColor);

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

            // ---- Ronin-8 StoryNpc ----
            var ronin8Go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ronin8Go.name = "Ronin8";
            Object.DestroyImmediate(ronin8Go.GetComponent<Collider>());
            ronin8Go.transform.position = new Vector3(0f, 0f, 3f);
            ronin8Go.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ronin8Go.GetComponent<Renderer>(), new Color(0.70f, 0.68f, 0.65f)); // pale tint
            var ronin8Npc = ronin8Go.AddComponent<StoryNpc>();
            var r8So = new SerializedObject(ronin8Npc);
            r8So.FindProperty("displayName").stringValue = "Ronin-8";
            r8So.FindProperty("remote").boolValue = false;
            r8So.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5 Lotus Enforcers (facility gauntlet): 1 wave ----
            var lotusColor = new Color(0.75f, 0.30f, 0.45f);
            var lotusWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 8f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 11f),
                new Vector3(0f, 0f, 13f)
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

            var lotusSpawner = BuildEp03WaveSpawner("LotusSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { lotusWaveHealths },
                new[] { BuildEp21DialoguePlayer("Dialogue_AwakeningBarks", new Vector3(0f, 1.5f, 8f), "awakening_barks") });

            // ---- Dialogue Players ----
            var theQuestionDialogue = BuildEp21DialoguePlayer("Dialogue_TheQuestion", new Vector3(0f, 1.5f, 2f), "the_question");
            var tqSo = new SerializedObject(theQuestionDialogue);
            tqSo.FindProperty("playOnStart").boolValue = true;
            tqSo.ApplyModifiedPropertiesWithoutUndo();

            var khallRealDialogue = BuildEp21DialoguePlayer("Dialogue_KhallReal", new Vector3(0f, 1.5f, 12f), "khall_real");

            var ronin8WakeDialogue = BuildEp21DialoguePlayer("Dialogue_Ronin8Wake", new Vector3(0f, 1.5f, 18f), "ronin8_wake");

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 21.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set flags: ep21_complete, ronin8_freed.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep21_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "ronin8_freed";
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

            // Step 0: Dialogue the_question (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Question";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = theQuestionDialogue;

            // Step 1: DefeatWaves — 5 lotus enforcers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Lotus Enforcers (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = lotusSpawner;

            // Step 2: Dialogue khall_real.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Khall Real";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = khallRealDialogue;

            // Step 3: Dialogue ronin8_wake.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Ronin-8 Wake";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = ronin8WakeDialogue;

            // Step 4: Prompt — return to space with ep21_complete + ronin8_freed flags.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep21_complete + ronin8_freed)";
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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep21AwakeningScenePath);
            EnsureScenesInBuild(Galaxy3Ep21AwakeningScenePath);

            Debug.Log($"[Space Samurai] EP21 Awakening scene built at {Galaxy3Ep21AwakeningScenePath}. " +
                      "Layout: lotus facility core with golden/amber and cyan lighting, light fog, dark metal floor/walls, unlit cyan/gold tank props, capsule set-dressing, PollenHazeController on Dreamscape. " +
                      "Ronin-8 NPC (pale tint, no Health, StoryNpc displayName). " +
                      "5 Lotus Enforcers (crimson-purple tint, nonLethal, facility gauntlet). " +
                      "5 steps: the_question (auto) → defeat 5 enforcers (awakening_barks bark) → khall_real dialogue → ronin8_wake dialogue → " +
                      "return to hub Prompt (sets ep21_complete + ronin8_freed via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 21 FINALE (sets ep21_complete + ronin8_freed).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP21 Scenes", priority = 227)]
        public static void BuildAllEp21Scenes()
        {
            BuildEp21NarcosisDescent();
            BuildEp21ForgettingDescent();
            BuildEp21GardenOfEchoes();
            BuildEp21Testimony();
            BuildEp21Expulsion();
            BuildEp21ShadowInCode();
            BuildEp21Awakening();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP21 scenes built + inputs rewired.");
        }
    }
}

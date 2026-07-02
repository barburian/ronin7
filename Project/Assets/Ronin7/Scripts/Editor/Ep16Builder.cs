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
    /// EP16 "The Ashfall Monastery" on-foot scene builders. Builds six core episodes on Cinder Vale:
    /// - DockingTrench: cold ash/bone grey trench with one wave of Salvage Pirates
    /// - Monastery: warm meditation hall with Morrow's introduction (no combat)
    /// - BladeGarden: stone-column garden with non-lethal spar vs Sela (DuelYield)
    /// - CorvetteAssault: monastery corridor with red alarm lights, two waves of Dominion Soldiers
    /// - TheDuel: sealed stone chamber with Khall duel + SelfSeveranceTrigger mechanic
    /// - SilentGarden: meditation hall finale with story flags galaxy2_complete + ep16_complete, returns to hub
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP16 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep16" and loads lines from Ep16Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp16DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep16Lines.Get(setId), advanceRef, setId, clipPrefix: "ep16");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 Docking Trench", priority = 160)]
        public static void BuildEp16DockingTrench()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Docking Trench: cold ash/bone grey descent ravine.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.50f, 0.48f); // cold ash/bone grey key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.18f, 0.16f); // cold dark ambient

            // Cold grey fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.32f, 0.30f);
            RenderSettings.fogDensity = 0.021f;

            // Two cool grey accent point lights.
            BuildAccentPointLight("TrenchLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.70f, 0.68f, 0.65f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("TrenchLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.65f, 0.63f, 0.60f), intensity: 0.70f, range: 9f);

            // ---- Docking trench floor and walls ----
            var trenchGo = new GameObject("DockingTrench");
            var trench = trenchGo.transform;
            var ashMetal = new Color(0.40f, 0.38f, 0.35f);
            var ashDark = new Color(0.25f, 0.23f, 0.20f);

            // Main trench floor.
            BuildFloorCeiling(trench, "TrenchFloor", new Vector3(0f, 0f, 10f), new Vector3(8f, 0f, 20f), ashMetal, ashDark);

            // Trench walls (narrow descent).
            BuildWall(trench, "TrenchWall_W", new Vector3(-3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(trench, "TrenchWall_E", new Vector3(3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // A few ashfall debris props.
            var debrisColor = new Color(0.48f, 0.45f, 0.42f);
            BuildProp(trench, "Debris1", new Vector3(-1.5f, 0.6f, 6f), new Vector3(0.5f, 0.4f, 0.6f), debrisColor);
            BuildProp(trench, "Debris2", new Vector3(1.5f, 0.6f, 8f), new Vector3(0.4f, 0.5f, 0.5f), debrisColor);

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
            var transitIntroDialogue = BuildEp16DialoguePlayer("Dialogue_TransitIntro", new Vector3(0f, 1.5f, 2f), "transit_intro");
            var transitDlgSo = new SerializedObject(transitIntroDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var vessIntroDialogue = BuildEp16DialoguePlayer("Dialogue_SelaIntro", new Vector3(0f, 1.5f, 6f), "sela_intro");
            var descentDialogue = BuildEp16DialoguePlayer("Dialogue_Descent", new Vector3(0f, 1.5f, 12f), "descent");

            // ---- 4 Salvage Pirate enemies: 1 wave ----
            var salvageColor = new Color(0.65f, 0.45f, 0.35f); // rough rusty tint
            var salvageWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(1.5f, 0f, 6f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f)
            };

            var salvageWaveHealths = new List<Health>();
            foreach (var pos in salvageWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, salvageColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                salvageWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var salvageSpawner = BuildWaveSpawner("SalvageSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { salvageWaveHealths },
                new[] { BuildEp16DialoguePlayer("Dialogue_SalvageBarks", new Vector3(0f, 1.5f, 8f), "salvage_barks") });

            // Transition box: "DESCEND — INTO ASHFALL".
            var monasteryBoxGo = BuildTransitionBox("ToMonasteryBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND — INTO ASHFALL",
                out var monasteryBtn, out var monasteryTransition);
            var mbSo = new SerializedObject(monasteryTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep16MonasterySceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(monasteryBtn.onClick,
                new UnityEngine.Events.UnityAction(monasteryTransition.LoadOnFootScene));
            monasteryBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue transit_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Transit Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = transitIntroDialogue;

            // Step 1: DefeatWaves — 4 Salvage Pirates.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Salvage Pirates (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = salvageSpawner;

            // Step 2: Dialogue sela_intro.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Sela Intro";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = vessIntroDialogue;

            // Step 3: Dialogue descent.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Descent";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = descentDialogue;

            // Step 4: Prompt — transition to Monastery.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Descend to Ashfall";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = monasteryBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16DockingTrenchScenePath);
            EnsureScenesInBuild(Galaxy2Ep16DockingTrenchScenePath, Galaxy2Ep16MonasteryScenePath);

            Debug.Log($"[Space Samurai] EP16 Docking Trench scene built at {Galaxy2Ep16DockingTrenchScenePath}. " +
                      "Layout: cold ash/bone grey descent trench with dark metal floor/walls, debris props. " +
                      "4 Salvage Pirates (rusty tint, nonLethal, SetActive false). " +
                      "5 steps: transit_intro (auto) → defeat 4 Salvage Pirates (salvage_barks bark) → " +
                      "sela_intro dialogue → descent dialogue → transition to Monastery.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 Monastery", priority = 161)]
        public static void BuildEp16Monastery()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Monastery: warm carved stone meditation hall (amber/sand palette).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.65f, 0.50f); // warm amber/sand key light
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.24f, 0.18f); // warm dark ambient

            // Warm amber fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.48f, 0.40f, 0.30f);
            RenderSettings.fogDensity = 0.020f;

            // Two warm amber accent lights.
            BuildAccentPointLight("MonasteryLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.95f, 0.75f, 0.50f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("MonasteryLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.90f, 0.70f, 0.45f), intensity: 0.80f, range: 9f);

            // ---- Monastery meditation hall ----
            var monasteryGo = new GameObject("Monastery");
            var monastery = monasteryGo.transform;
            var stoneMid = new Color(0.55f, 0.50f, 0.45f);
            var stoneDark = new Color(0.35f, 0.32f, 0.28f);

            // Main meditation hall floor.
            BuildFloorCeiling(monastery, "HallFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 16f), stoneMid, stoneDark);

            // Hall walls (warm carved stone).
            BuildWall(monastery, "HallWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));
            BuildWall(monastery, "HallWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));

            // Stone pillar props.
            var pillarColor = new Color(0.50f, 0.45f, 0.40f);
            BuildProp(monastery, "Pillar1", new Vector3(-2f, 1f, 6f), new Vector3(0.4f, 2f, 0.4f), pillarColor);
            BuildProp(monastery, "Pillar2", new Vector3(2f, 1f, 8f), new Vector3(0.4f, 2f, 0.4f), pillarColor);

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

            // ---- Dialogue Player ----
            var morrowIntroDialogue = BuildEp16DialoguePlayer("Dialogue_MorrowIntro", new Vector3(0f, 1.5f, 10f), "morrow_intro");
            var morrowDlgSo = new SerializedObject(morrowIntroDialogue);
            morrowDlgSo.FindProperty("playOnStart").boolValue = true;
            morrowDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "ENTER — THE BLADE GARDEN".
            var gardenBoxGo = BuildTransitionBox("ToGardenBox", new Vector3(0f, 1.2f, 16.5f), "ENTER — THE BLADE GARDEN",
                out var gardenBtn, out var gardenTransition);
            var gbSo = new SerializedObject(gardenTransition);
            gbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep16BladeGardenSceneName;
            gbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gardenBtn.onClick,
                new UnityEngine.Events.UnityAction(gardenTransition.LoadOnFootScene));
            gardenBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue morrow_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Morrow Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = morrowIntroDialogue;

            // Step 1: Prompt — transition to Blade Garden.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Enter Blade Garden";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = gardenBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16MonasteryScenePath);
            EnsureScenesInBuild(Galaxy2Ep16MonasteryScenePath, Galaxy2Ep16BladeGardenScenePath);

            Debug.Log($"[Space Samurai] EP16 Monastery scene built at {Galaxy2Ep16MonasteryScenePath}. " +
                      "Layout: warm meditation hall with carved stone floor/walls, stone pillar props, amber lighting. " +
                      "No combat. " +
                      "2 steps: morrow_intro (auto) → transition to Blade Garden.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 Blade Garden", priority = 162)]
        public static void BuildEp16BladeGarden()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Blade Garden: warm stone-column garden.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.68f, 0.55f); // warm sand/amber key light
            light.intensity = 0.52f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.20f); // warm dark ambient

            // Warm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.50f, 0.42f, 0.32f);
            RenderSettings.fogDensity = 0.022f;

            // Two warm accent lights.
            BuildAccentPointLight("GardenLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.72f, 0.50f), intensity: 0.90f, range: 10f);
            BuildAccentPointLight("GardenLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.67f, 0.45f), intensity: 0.85f, range: 9f);

            // ---- Blade Garden stone columns ----
            var gardenGo = new GameObject("BladeGarden");
            var garden = gardenGo.transform;
            var gardenStone = new Color(0.58f, 0.52f, 0.45f);
            var gardenDark = new Color(0.38f, 0.34f, 0.28f);

            // Main garden floor.
            BuildFloorCeiling(garden, "GardenFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), gardenStone, gardenDark);

            // Stone column props.
            var columnColor = new Color(0.52f, 0.48f, 0.42f);
            BuildProp(garden, "Column1", new Vector3(-2.5f, 1f, 6f), new Vector3(0.3f, 2.2f, 0.3f), columnColor);
            BuildProp(garden, "Column2", new Vector3(2.5f, 1f, 8f), new Vector3(0.3f, 2.2f, 0.3f), columnColor);
            BuildProp(garden, "Column3", new Vector3(-1.5f, 1f, 12f), new Vector3(0.3f, 2.2f, 0.3f), columnColor);
            BuildProp(garden, "Column4", new Vector3(1.5f, 1f, 14f), new Vector3(0.3f, 2.2f, 0.3f), columnColor);

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

            // ---- ONE duel enemy: Sela (sparring partner) ----
            var duelist = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var duelistRenderer = duelist.GetComponent<Renderer>();
            if (duelistRenderer != null) TintShared(duelistRenderer, new Color(0.55f, 0.50f, 0.48f)); // stone/neutral tint
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
            dNpcSo.FindProperty("displayName").stringValue = "Sela";
            dNpcSo.FindProperty("remote").boolValue = false;
            dNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.5 (sparring, not combat).
            var duelYield = duelist.gameObject.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", duelistHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.5f;
            if (duelistMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { duelistMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Duelist STAYS ACTIVE (not SetActive(false)).

            // ---- Dialogue Player ----
            var vessPracticeDialogue = BuildEp16DialoguePlayer("Dialogue_SelaPractice", new Vector3(0f, 1.5f, 10f), "sela_practice");
            var practiceDlgSo = new SerializedObject(vessPracticeDialogue);
            practiceDlgSo.FindProperty("playOnStart").boolValue = true;
            practiceDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "PROCEED — THE ASSAULT".
            var assaultBoxGo = BuildTransitionBox("ToAssaultBox", new Vector3(0f, 1.2f, 18.5f), "PROCEED — THE ASSAULT",
                out var assaultBtn, out var assaultTransition);
            var abSo = new SerializedObject(assaultTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy2Ep16CorvetteAssaultSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(assaultBtn.onClick,
                new UnityEngine.Events.UnityAction(assaultTransition.LoadOnFootScene));
            assaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue sela_practice (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Sela Practice";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vessPracticeDialogue;

            // Step 1: Prompt (null) — the spar (DuelYield.onAccepted advances it).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Spar with Sela (yield via DuelYield)";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 2: Prompt — transition to Corvette Assault.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Proceed to Assault";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = assaultBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16BladeGardenScenePath);
            EnsureScenesInBuild(Galaxy2Ep16BladeGardenScenePath, Galaxy2Ep16CorvetteAssaultScenePath);

            Debug.Log($"[Space Samurai] EP16 Blade Garden scene built at {Galaxy2Ep16BladeGardenScenePath}. " +
                      "Layout: warm stone-column garden with carved stone floor/walls, stone column props. " +
                      "1 Duelist enemy (Sela, stone/neutral, nonLethal, DuelYield yield at 50%, ACTIVE from start). " +
                      "3 steps: sela_practice (auto) → spar Prompt (onAccepted → AdvanceFromPrompt) → transition to Corvette Assault.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 Corvette Assault", priority = 163)]
        public static void BuildEp16CorvetteAssault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Corvette Assault: monastery corridor with red alarm accent lights.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.50f, 0.45f); // warm copper key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.15f); // dark warm ambient

            // Warm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.40f, 0.32f, 0.28f);
            RenderSettings.fogDensity = 0.023f;

            // Two red alarm accent lights.
            BuildAccentPointLight("AlarmLight1", new Vector3(-2f, 2f, 8f),
                new Color(1f, 0.35f, 0.25f), intensity: 0.8f, range: 9f);
            BuildAccentPointLight("AlarmLight2", new Vector3(2f, 2.5f, 12f),
                new Color(0.95f, 0.30f, 0.20f), intensity: 0.75f, range: 8f);

            // ---- Monastery corridor ----
            var corridorGo = new GameObject("CorvetteAssaultCorridor");
            var corridor = corridorGo.transform;
            var metalMid = new Color(0.40f, 0.38f, 0.35f);
            var metalDark = new Color(0.25f, 0.22f, 0.20f);

            // Main corridor floor.
            BuildFloorCeiling(corridor, "CorridorFloor", new Vector3(0f, 0f, 10f), new Vector3(6f, 0f, 16f), metalMid, metalDark);

            // Corridor walls.
            BuildWall(corridor, "CorridorWall_W", new Vector3(-2f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));
            BuildWall(corridor, "CorridorWall_E", new Vector3(2f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));

            // A few alarm beacon props (red).
            var beaconColor = new Color(0.95f, 0.30f, 0.20f);
            BuildProp(corridor, "Beacon1", new Vector3(-1.2f, 1.8f, 6f), new Vector3(0.2f, 0.4f, 0.2f), beaconColor);
            BuildProp(corridor, "Beacon2", new Vector3(1.2f, 1.8f, 10f), new Vector3(0.2f, 0.4f, 0.2f), beaconColor);

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
            var pilgrimDialogue = BuildEp16DialoguePlayer("Dialogue_Pilgrim", new Vector3(0f, 1.5f, 2f), "pilgrim");
            var pilgrimDlgSo = new SerializedObject(pilgrimDialogue);
            pilgrimDlgSo.FindProperty("playOnStart").boolValue = true;
            pilgrimDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5 Dominion Soldier enemies: 1 wave ----
            var soldierColor = new Color(0.45f, 0.40f, 0.38f); // dark steel tint
            var soldierWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 5f),
                new Vector3(0f, 0f, 5.5f),
                new Vector3(1.5f, 0f, 5f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8f)
            };

            var soldierWaveHealths = new List<Health>();
            foreach (var pos in soldierWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, soldierColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                soldierWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var soldierSpawner = BuildWaveSpawner("DominionSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { soldierWaveHealths },
                new[] { BuildEp16DialoguePlayer("Dialogue_CorvetteBarks", new Vector3(0f, 1.5f, 8f), "corvette_barks") });

            // Transition box: "ADVANCE — THE OVERSEER".
            var duelBoxGo = BuildTransitionBox("ToDuelBox", new Vector3(0f, 1.2f, 16.5f), "ADVANCE — THE OVERSEER",
                out var duelBtn, out var duelTransition);
            var dbSo = new SerializedObject(duelTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep16TheDuelSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(duelBtn.onClick,
                new UnityEngine.Events.UnityAction(duelTransition.LoadOnFootScene));
            duelBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue pilgrim (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Pilgrim";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = pilgrimDialogue;

            // Step 1: DefeatWaves — 5 Dominion Soldiers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Soldiers (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = soldierSpawner;

            // Step 2: Prompt — transition to The Duel.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Advance to Duel";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = duelBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16CorvetteAssaultScenePath);
            EnsureScenesInBuild(Galaxy2Ep16CorvetteAssaultScenePath, Galaxy2Ep16TheDuelScenePath);

            Debug.Log($"[Space Samurai] EP16 Corvette Assault scene built at {Galaxy2Ep16CorvetteAssaultScenePath}. " +
                      "Layout: monastery corridor with red alarm lights, beacon props, dark metal floor/walls. " +
                      "5 Dominion Soldiers (dark steel, nonLethal, SetActive false). " +
                      "3 steps: pilgrim (auto) → defeat 5 Dominion Soldiers (corvette_barks bark) → " +
                      "transition to The Duel.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 The Duel", priority = 164)]
        public static void BuildEp16TheDuel()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Duel: sealed stone chamber with cool tones.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.60f, 0.55f, 0.52f); // cool stone key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.16f, 0.14f); // dark cool ambient

            // Cool grey fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.30f, 0.28f);
            RenderSettings.fogDensity = 0.024f;

            // Two cool grey accent lights.
            BuildAccentPointLight("ChamberLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.75f, 0.72f, 0.68f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("ChamberLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.70f, 0.67f, 0.63f), intensity: 0.70f, range: 9f);

            // ---- Sealed stone chamber ----
            var chamberGo = new GameObject("DuelChamber");
            var chamber = chamberGo.transform;
            var chamberStone = new Color(0.48f, 0.45f, 0.42f);
            var chamberDark = new Color(0.28f, 0.26f, 0.24f);

            // Main chamber floor.
            BuildFloorCeiling(chamber, "ChamberFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 16f), chamberStone, chamberDark);

            // Chamber walls (sealed, tall).
            BuildWall(chamber, "ChamberWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 4f, 16f));
            BuildWall(chamber, "ChamberWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 4f, 16f));

            // A stone altar prop.
            BuildProp(chamber, "AltarStone", new Vector3(0f, 1.2f, 14f), new Vector3(1.5f, 0.8f, 1.5f), new Color(0.42f, 0.40f, 0.38f));

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

            // ---- Dialogue Players ----
            var locatorRevealDialogue = BuildEp16DialoguePlayer("Dialogue_LocatorReveal", new Vector3(0f, 1.5f, 2f), "locator_reveal");
            var locatorDlgSo = new SerializedObject(locatorRevealDialogue);
            locatorDlgSo.FindProperty("playOnStart").boolValue = true;
            locatorDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var makersTruthDialogue = BuildEp16DialoguePlayer("Dialogue_MakersTruth", new Vector3(0f, 1.5f, 6f), "makers_truth");
            var overseerArrivalDialogue = BuildEp16DialoguePlayer("Dialogue_OverseerArrival", new Vector3(0f, 1.5f, 8f), "overseer_arrival");
            var theOfferDialogue = BuildEp16DialoguePlayer("Dialogue_TheOffer", new Vector3(0f, 1.5f, 10f), "the_offer");
            var theSeveranceDialogue = BuildEp16DialoguePlayer("Dialogue_TheSeverance", new Vector3(0f, 1.5f, 12f), "the_severance");

            // ---- ONE duel enemy: Khall (Overseer) ----
            var khall = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var khallRenderer = khall.GetComponent<Renderer>();
            if (khallRenderer != null) TintShared(khallRenderer, new Color(0.35f, 0.30f, 0.28f)); // dark tint
            var khallMelee = khall.GetComponent<MeleeAttacker>();
            if (khallMelee != null)
            {
                var maSo = new SerializedObject(khallMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var khallHealth = khall.GetComponent<Health>();
            var khallNpc = khall.gameObject.AddComponent<StoryNpc>();
            var kNpcSo = new SerializedObject(khallNpc);
            kNpcSo.FindProperty("displayName").stringValue = "Khall";
            kNpcSo.FindProperty("remote").boolValue = false;
            kNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.3.
            var khallDuelYield = khall.gameObject.AddComponent<DuelYield>();
            var khallDyeSo = new SerializedObject(khallDuelYield);
            SetObjectRef(khallDyeSo, "opponent", khallHealth);
            khallDyeSo.FindProperty("yieldThreshold").floatValue = 0.3f;
            if (khallMelee != null) SetObjectRefList(khallDyeSo, "disableOnYield", new List<Object> { khallMelee });
            SetObjectRef(khallDyeSo, "sword", swordGrab);
            khallDyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            khallDyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Khall STAYS ACTIVE (not SetActive(false)).

            // ---- Self-Severance mechanism ----
            var selfSeveranceGo = new GameObject("SelfSeverance");
            var severanceTrigger = selfSeveranceGo.AddComponent<Ronin7.Enemies.SelfSeveranceTrigger>();

            // Transition box: "SEVER THE LOCATOR".
            var severBoxGo = BuildTransitionBox("SeverBox", new Vector3(0f, 1.2f, 16.5f), "SEVER THE LOCATOR",
                out var severBtn, out var severTransition);
            severBoxGo.SetActive(false);

            // Wire severBtn.onClick → trigger.Sever().
            UnityEventTools.AddPersistentListener(severBtn.onClick,
                new UnityEngine.Events.UnityAction(severanceTrigger.Sever));

            // Transition box to Silent Garden (for after severance).
            var silentBoxGo = BuildTransitionBox("ToSilentBox", new Vector3(0f, 1.2f, 16.5f), "WITHDRAW — THE GARDEN",
                out var silentBtn, out var silentTransition);
            var sbSo = new SerializedObject(silentTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep16SilentGardenSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(silentBtn.onClick,
                new UnityEngine.Events.UnityAction(silentTransition.LoadOnFootScene));
            silentBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 8;

            // Step 0: Dialogue locator_reveal (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Locator Reveal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = locatorRevealDialogue;

            // Step 1: Dialogue makers_truth.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Makers Truth";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = makersTruthDialogue;

            // Step 2: Dialogue overseer_arrival.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Overseer Arrival";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = overseerArrivalDialogue;

            // Step 3: Prompt (null) — Khall duel (DuelYield.onAccepted advances it).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Duel Khall (yield via DuelYield)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 4: Dialogue the_offer.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: The Offer";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = theOfferDialogue;

            // Step 5: Prompt — severance box (trigger.onSevered advances it).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Sever the Locator";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = severBoxGo;

            // Step 6: Dialogue the_severance.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: The Severance";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = theSeveranceDialogue;

            // Step 7: Prompt — transition to Silent Garden.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s7.FindPropertyRelative("label").stringValue = "Prompt: Withdraw to Garden";
            s7.FindPropertyRelative("promptObject").objectReferenceValue = silentBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → missionDirector.AdvanceFromPrompt.
            UnityEventTools.AddPersistentListener(khallDuelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // Wire SelfSeveranceTrigger.onSevered → missionDirector.AdvanceFromPrompt.
            UnityEventTools.AddPersistentListener(severanceTrigger.onSevered,
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16TheDuelScenePath);
            EnsureScenesInBuild(Galaxy2Ep16TheDuelScenePath, Galaxy2Ep16SilentGardenScenePath);

            Debug.Log($"[Space Samurai] EP16 The Duel scene built at {Galaxy2Ep16TheDuelScenePath}. " +
                      "Layout: sealed stone chamber with cool grey lighting, altar prop. " +
                      "1 Duelist enemy (Khall, dark tint, nonLethal, DuelYield yield at 30%, ACTIVE from start). " +
                      "SelfSeveranceTrigger with SEVER button (wired to trigger.Sever, onSevered → AdvanceFromPrompt). " +
                      "8 steps: locator_reveal (auto) → makers_truth dialogue → overseer_arrival dialogue → " +
                      "duel Khall Prompt (onAccepted → AdvanceFromPrompt) → the_offer dialogue → " +
                      "severance Prompt (onSevered → AdvanceFromPrompt) → the_severance dialogue → transition to Silent Garden.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP16 Silent Garden", priority = 165)]
        public static void BuildEp16SilentGarden()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Silent Garden: warm meditation hall, final chapter.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.70f, 0.55f); // warm amber finale light
            light.intensity = 0.50f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.20f); // warm ambient

            // Soft warm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.45f, 0.35f);
            RenderSettings.fogDensity = 0.019f;

            // Two warm finale accent lights.
            BuildAccentPointLight("GardenFinalLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.80f, 0.55f), intensity: 0.85f, range: 11f);
            BuildAccentPointLight("GardenFinalLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.75f, 0.50f), intensity: 0.80f, range: 10f);

            // ---- Silent meditation garden ----
            var finalGardenGo = new GameObject("SilentGarden");
            var finalGarden = finalGardenGo.transform;
            var finalStone = new Color(0.60f, 0.55f, 0.48f);
            var finalDark = new Color(0.40f, 0.36f, 0.30f);

            // Main garden floor.
            BuildFloorCeiling(finalGarden, "FinalFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), finalStone, finalDark);

            // Garden walls (open sides for revelation).
            BuildWall(finalGarden, "FinalWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(finalGarden, "FinalWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // Sword Altar prop.
            BuildProp(finalGarden, "SwordAltar", new Vector3(0f, 1.5f, 14f), new Vector3(1.5f, 1.2f, 1.5f), new Color(0.55f, 0.50f, 0.45f));

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
            var gardenRemadeDialogue = BuildEp16DialoguePlayer("Dialogue_GardenRemade", new Vector3(0f, 1.5f, 2f), "garden_remade");
            var gardenRemadeSo = new SerializedObject(gardenRemadeDialogue);
            gardenRemadeSo.FindProperty("playOnStart").boolValue = true;
            gardenRemadeSo.ApplyModifiedPropertiesWithoutUndo();

            var departureDialogue = BuildEp16DialoguePlayer("Dialogue_Departure", new Vector3(0f, 1.5f, 6f), "departure");
            var cageRevealedDialogue = BuildEp16DialoguePlayer("Dialogue_CageRevealed", new Vector3(0f, 1.5f, 8f), "cage_revealed");
            var swordLaidDownDialogue = BuildEp16DialoguePlayer("Dialogue_SwordLaidDown", new Vector3(0f, 1.5f, 10f), "sword_laid_down");
            var silentGardenDialogue = BuildEp16DialoguePlayer("Dialogue_SilentGarden", new Vector3(0f, 1.5f, 12f), "silent_garden");

            // Transition box: return to hub with flags set.
            var returnHubBoxGo = BuildTransitionBox("ReturnHubBox", new Vector3(0f, 1.2f, 18.5f), "RETURN — TO THE STARS",
                out var returnHubBtn, out var returnHubTransition);
            returnHubBoxGo.SetActive(false);

            // Galaxy 2 finale: set ep16_complete + galaxy2_complete, then return to hub.
            // Use a serializable CampaignFlagSetter wired via a PERSISTENT listener so the flags
            // actually set at runtime (a build-time runtime AddListener is not serialized into the scene).
            var flagSetter = returnHubBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var fsSo = new SerializedObject(flagSetter);
            var flagsProp = fsSo.FindProperty("flags");
            flagsProp.arraySize = 2;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "galaxy2_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "ep16_complete";
            fsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnHubBtn.onClick,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnHubBtn.onClick,
                new UnityEngine.Events.UnityAction(returnHubTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue garden_remade (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Garden Remade";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = gardenRemadeDialogue;

            // Step 1: Dialogue departure.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Departure";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = departureDialogue;

            // Step 2: Dialogue cage_revealed.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Cage Revealed";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = cageRevealedDialogue;

            // Step 3: Dialogue sword_laid_down.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Sword Laid Down";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = swordLaidDownDialogue;

            // Step 4: Dialogue silent_garden.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Silent Garden";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = silentGardenDialogue;

            // Step 5: Prompt — return to hub.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Return to Stars (sets ep16_complete + galaxy2_complete)";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = returnHubBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep16SilentGardenScenePath);
            EnsureScenesInBuild(Galaxy2Ep16SilentGardenScenePath);

            Debug.Log($"[Space Samurai] EP16 Silent Garden scene built at {Galaxy2Ep16SilentGardenScenePath}. " +
                      "Layout: warm meditation garden with sword altar, soft amber finale lighting. " +
                      "No combat. Galaxy 2 finale. " +
                      "6 steps: garden_remade (auto) → departure dialogue → cage_revealed dialogue → " +
                      "sword_laid_down dialogue → silent_garden dialogue → return to hub Prompt " +
                      "(sets ep16_complete + galaxy2_complete via CampaignState.SetFlag, then ReturnToSpace).");
        }
    }
}

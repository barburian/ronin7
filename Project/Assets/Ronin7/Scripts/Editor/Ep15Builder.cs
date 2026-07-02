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
    /// EP15 "The Frequency" on-foot scene builders. Builds five core episodes:
    /// - RelayShafts: cold amber relay station with automated security drones
    /// - CorvetteBoarding: cramped Gilded Maw corvette interior with duel via DuelYield
    /// - SulfurThrone: warm industrial Emberhand station with enforcer wave
    /// - SunkenArchives: deep dark blue drowned ruins with Pale Choir assassins
    /// - DesertReckoning: warm desert with three-way FactionCombatant battle (Dominion vs Syndicate)
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP15 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep15" and loads lines from Ep15Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp15DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep15Lines.Get(setId), advanceRef, setId, clipPrefix: "ep15");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Relay Shafts", priority = 152)]
        public static void BuildEp15RelayShafts()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Relay Shafts: cold amber zero-g relay with dark metal and unlit cyan logic cores.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.50f, 0.35f); // amber key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.20f, 0.15f); // dark amber ambient

            // Cold amber exponential fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.20f);
            RenderSettings.fogDensity = 0.020f;

            // Two amber accent point lights.
            BuildAccentPointLight("RelayLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.6f, 0.3f), intensity: 0.9f, range: 11f);
            BuildAccentPointLight("RelayLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.95f, 0.55f, 0.25f), intensity: 0.85f, range: 10f);

            // ---- Relay shaft floor and walls ----
            var relayGo = new GameObject("RelayShaft");
            var relay = relayGo.transform;
            var darkMetal = new Color(0.25f, 0.28f, 0.32f);
            var darkerMetal = new Color(0.15f, 0.18f, 0.22f);

            // Main relay floor.
            BuildFloorCeiling(relay, "RelayFloor", new Vector3(0f, 0f, 10f), new Vector3(8f, 0f, 20f), darkMetal, darkerMetal);

            // Narrow relay walls.
            BuildWall(relay, "RelayWall_W", new Vector3(-3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(relay, "RelayWall_E", new Vector3(3f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Unlit cyan logic core cubes ----
            var logicCoreColor = new Color(0.2f, 0.9f, 1f); // cyan
            var logicCores = 4;
            for (int i = 0; i < logicCores; i++)
            {
                float x = (i % 2 - 0.5f) * 4f;
                float y = 1.5f;
                float z = 6f + (i / 2) * 5f;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = $"LogicCore_{i}";
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.transform.SetParent(relay, false);
                core.transform.position = new Vector3(x, y, z);
                core.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                core.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(logicCoreColor);
            }

            // ---- SignalCore unlit prop ----
            var signalCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            signalCore.name = "SignalCore";
            Object.DestroyImmediate(signalCore.GetComponent<Collider>());
            signalCore.transform.SetParent(relay, false);
            signalCore.transform.position = new Vector3(0f, 2f, 16f);
            signalCore.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            signalCore.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.8f, 1f, 1f)); // bright cyan

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
            var transitIntroDialogue = BuildEp15DialoguePlayer("Dialogue_TransitIntro", new Vector3(0f, 1.5f, 2f), "transit_intro");
            var transitDlgSo = new SerializedObject(transitIntroDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var relayApproachDialogue = BuildEp15DialoguePlayer("Dialogue_RelayApproach", new Vector3(0f, 1.5f, 4f), "relay_approach");
            var signalConfessionDialogue = BuildEp15DialoguePlayer("Dialogue_SignalConfession", new Vector3(0f, 1.5f, 10f), "signal_confession");
            var ledgerDialogue = BuildEp15DialoguePlayer("Dialogue_Ledger", new Vector3(0f, 1.5f, 12f), "ledger");

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

            var droneSpawner = BuildWaveSpawner("RelaySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { BuildEp15DialoguePlayer("Dialogue_RelayBarks", new Vector3(0f, 1.5f, 8f), "relay_barks") });

            // Transition box: "ENTER — THE CORVETTE".
            var corvetteBoxGo = BuildTransitionBox("ToCorvetteBox", new Vector3(0f, 1.2f, 20.5f), "ENTER — THE CORVETTE",
                out var corvetteBtn, out var corvetteTransition);
            var cbSo = new SerializedObject(corvetteTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep15CorvetteBoardingSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(corvetteBtn.onClick,
                new UnityEngine.Events.UnityAction(corvetteTransition.LoadOnFootScene));
            corvetteBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue transit_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Transit Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = transitIntroDialogue;

            // Step 1: Dialogue relay_approach.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Relay Approach";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = relayApproachDialogue;

            // Step 2: DefeatWaves — 4 Security Drones.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Security Drones (4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 3: Dialogue signal_confession.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Signal Confession";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = signalConfessionDialogue;

            // Step 4: Dialogue ledger.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Ledger";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = ledgerDialogue;

            // Step 5: Prompt — transition to Corvette.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Enter Corvette";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = corvetteBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15RelayShaftsScenePath);
            EnsureScenesInBuild(Galaxy2Ep15RelayShaftsScenePath, Galaxy2Ep15CorvetteBoardingScenePath);

            Debug.Log($"[Space Samurai] EP15 Relay Shafts scene built at {Galaxy2Ep15RelayShaftsScenePath}. " +
                      "Layout: cold amber zero-g relay with dark metal floor/walls, unlit cyan logic cores and signal core. " +
                      "4 Security Drones (metallic grey, nonLethal). " +
                      "6 steps: transit_intro (auto) → relay_approach dialogue → defeat 4 Security Drones (relay_barks bark) → " +
                      "signal_confession dialogue → ledger dialogue → transition to Corvette Boarding.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Corvette Boarding", priority = 153)]
        public static void BuildEp15CorvetteBoarding()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Corvette Boarding: Gilded Maw cramped interior with dark metal and red accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.45f, 0.40f); // warm-dim key light
            light.intensity = 0.48f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.15f, 0.12f); // dark warm ambient

            // Dark metal fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.fogDensity = 0.024f;

            // Two red/orange accent lights (sparking conduits feel).
            BuildAccentPointLight("CorvetteLight1", new Vector3(-2f, 2f, 8f),
                new Color(1f, 0.4f, 0.2f), intensity: 0.8f, range: 9f);
            BuildAccentPointLight("CorvetteLight2", new Vector3(2f, 2.5f, 12f),
                new Color(0.95f, 0.35f, 0.15f), intensity: 0.75f, range: 8f);

            // ---- Corvette interior ----
            var corvetteGo = new GameObject("CorvetteInterior");
            var corvette = corvetteGo.transform;
            var metalMid = new Color(0.35f, 0.37f, 0.40f);
            var metalDark = new Color(0.22f, 0.24f, 0.28f);

            // Main corridor floor.
            BuildFloorCeiling(corvette, "CorvetteFloor", new Vector3(0f, 0f, 10f), new Vector3(6f, 0f, 16f), metalMid, metalDark);

            // Corridor walls (cramped).
            BuildWall(corvette, "CorvetteWall_W", new Vector3(-2f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));
            BuildWall(corvette, "CorvetteWall_E", new Vector3(2f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));

            // A few sparking conduit prop cubes.
            var sparkColor = new Color(0.50f, 0.45f, 0.40f);
            BuildProp(corvette, "Conduit1", new Vector3(-1.5f, 1.5f, 6f), new Vector3(0.3f, 0.6f, 0.2f), sparkColor);
            BuildProp(corvette, "Conduit2", new Vector3(1.5f, 1.5f, 10f), new Vector3(0.2f, 0.6f, 0.3f), sparkColor);

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

            // ---- ONE duel enemy: Gilded Maw Operative ----
            var duelist = BuildDominionEnemy(new Vector3(0f, 0f, 10f), playerHealth, enemyDef);
            var dualistRenderer = duelist.GetComponent<Renderer>();
            if (dualistRenderer != null) TintShared(dualistRenderer, new Color(0.50f, 0.45f, 0.55f)); // steel/violet tint
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
            dNpcSo.FindProperty("displayName").stringValue = "Gilded Maw Operative";
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

            // ---- Dialogue Player ----
            var mawBarksDialogue = BuildEp15DialoguePlayer("Dialogue_MawBarks", new Vector3(0f, 1.5f, 10f), "maw_barks");
            var barksDialogSo = new SerializedObject(mawBarksDialogue);
            barksDialogSo.FindProperty("playOnStart").boolValue = true;
            barksDialogSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "PROCEED — THE SULFUR STATION".
            var sulfurBoxGo = BuildTransitionBox("ToSulfurBox", new Vector3(0f, 1.2f, 16.5f), "PROCEED — THE SULFUR STATION",
                out var sulfurBtn, out var sulfurTransition);
            var sbSo = new SerializedObject(sulfurTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep15SulfurThroneSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(sulfurBtn.onClick,
                new UnityEngine.Events.UnityAction(sulfurTransition.LoadOnFootScene));
            sulfurBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue maw_barks (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Maw Barks";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = mawBarksDialogue;

            // Step 1: Prompt (null) — the duel (DuelYield.onAccepted advances it).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Duel Gilded Maw Operative (yield via DuelYield)";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 2: Prompt — transition to Sulfur Throne.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Proceed to Sulfur Station";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = sulfurBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15CorvetteBoardingScenePath);
            EnsureScenesInBuild(Galaxy2Ep15CorvetteBoardingScenePath, Galaxy2Ep15SulfurThroneScenePath);

            Debug.Log($"[Space Samurai] EP15 Corvette Boarding scene built at {Galaxy2Ep15CorvetteBoardingScenePath}. " +
                      "Layout: Gilded Maw cramped interior with dark metal and red/orange accent lights, conduit props. " +
                      "1 Duelist enemy (Gilded Maw Operative, steel/violet, nonLethal, DuelYield yield at 30%, ACTIVE from start). " +
                      "3 steps: maw_barks (auto) → duel Prompt (onAccepted → AdvanceFromPrompt) → transition to Sulfur Throne.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Sulfur Throne", priority = 154)]
        public static void BuildEp15SulfurThrone()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sulfur Throne: warm industrial catwalk-ish station.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.55f, 0.35f); // warm amber key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.22f, 0.15f); // warm dark ambient

            // Warm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.45f, 0.35f, 0.25f);
            RenderSettings.fogDensity = 0.023f;

            // Two warm amber accent lights.
            BuildAccentPointLight("SulfurLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.65f, 0.35f), intensity: 0.95f, range: 10f);
            BuildAccentPointLight("SulfurLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.6f, 0.30f), intensity: 0.90f, range: 9f);

            // ---- Sulfur Throne catwalk platform ----
            var sulfurGo = new GameObject("SulfurThrone");
            var sulfur = sulfurGo.transform;
            var warmMetal = new Color(0.45f, 0.40f, 0.35f);
            var warmDark = new Color(0.32f, 0.28f, 0.24f);

            // Main catwalk floor.
            BuildFloorCeiling(sulfur, "SulfurFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 18f), warmMetal, warmDark);

            // Catwalk walls.
            BuildWall(sulfur, "SulfurWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(sulfur, "SulfurWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // A few prop crates.
            var crateColor = new Color(0.50f, 0.45f, 0.40f);
            BuildProp(sulfur, "Crate1", new Vector3(-3f, 0.6f, 6f), new Vector3(1f, 0.8f, 1f), crateColor);
            BuildProp(sulfur, "Crate2", new Vector3(3f, 0.6f, 8f), new Vector3(1f, 0.8f, 1f), crateColor);
            BuildProp(sulfur, "Crate3", new Vector3(-2f, 0.6f, 14f), new Vector3(1f, 0.8f, 1f), crateColor);

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
            var mirsIntroDialogue = BuildEp15DialoguePlayer("Dialogue_MirsIntro", new Vector3(0f, 1.5f, 2f), "mirs_intro");
            var mirsDlgSo = new SerializedObject(mirsIntroDialogue);
            mirsDlgSo.FindProperty("playOnStart").boolValue = true;
            mirsDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var sulfurConfessionDialogue = BuildEp15DialoguePlayer("Dialogue_SulfurConfession", new Vector3(0f, 1.5f, 12f), "sulfur_confession");

            // ---- 5 Emberhand Enforcer enemies: 1 wave ----
            var enforcerColor = new Color(0.65f, 0.55f, 0.45f); // warm bronze tint
            var enforcerWavePositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 5f),
                new Vector3(0f, 0f, 5.5f),
                new Vector3(2.5f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8f)
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

            var enforcerSpawner = BuildWaveSpawner("EmberhandSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { enforcerWaveHealths },
                new[] { BuildEp15DialoguePlayer("Dialogue_EmberhandBarks", new Vector3(0f, 1.5f, 8f), "emberhand_barks") });

            // Transition box: "DESCEND — THE WATER MOON".
            var waterBoxGo = BuildTransitionBox("ToWaterBox", new Vector3(0f, 1.2f, 18.5f), "DESCEND — THE WATER MOON",
                out var waterBtn, out var waterTransition);
            var wbSo = new SerializedObject(waterTransition);
            wbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep15SunkenArchivesSceneName;
            wbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(waterBtn.onClick,
                new UnityEngine.Events.UnityAction(waterTransition.LoadOnFootScene));
            waterBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue mirs_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Mirs Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = mirsIntroDialogue;

            // Step 1: DefeatWaves — 5 Emberhand Enforcers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Emberhand Enforcers (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerSpawner;

            // Step 2: Dialogue sulfur_confession.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Sulfur Confession";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = sulfurConfessionDialogue;

            // Step 3: Prompt — transition to Sunken Archives.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Water Moon";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = waterBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15SulfurThroneScenePath);
            EnsureScenesInBuild(Galaxy2Ep15SulfurThroneScenePath, Galaxy2Ep15SunkenArchivesScenePath);

            Debug.Log($"[Space Samurai] EP15 Sulfur Throne scene built at {Galaxy2Ep15SulfurThroneScenePath}. " +
                      "Layout: warm industrial catwalk platform with warm lighting and crate props. " +
                      "5 Emberhand Enforcers (warm bronze, nonLethal). " +
                      "4 steps: mirs_intro (auto) → defeat 5 Emberhand Enforcers (emberhand_barks bark) → " +
                      "sulfur_confession dialogue → transition to Sunken Archives.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Sunken Archives", priority = 155)]
        public static void BuildEp15SunkenArchives()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sunken Archives: dark blue, dense fog, drowned ruins.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.35f, 0.48f, 0.60f); // cool blue key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.20f, 0.28f); // deep blue ambient

            // DENSE dark blue fog (~0.04 density).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.15f, 0.25f, 0.35f);
            RenderSettings.fogDensity = 0.040f;

            // Two dim blue accent lights.
            BuildAccentPointLight("SunkenLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.35f, 0.55f, 0.75f), intensity: 0.7f, range: 9f);
            BuildAccentPointLight("SunkenLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.30f, 0.50f, 0.70f), intensity: 0.65f, range: 8f);

            // ---- Sunken Archives ruins ----
            var sunkenGo = new GameObject("SunkenArchives");
            var sunken = sunkenGo.transform;
            var deepBlueMetal = new Color(0.25f, 0.32f, 0.40f);
            var deepBlueDark = new Color(0.15f, 0.20f, 0.28f);

            // Main sunken floor.
            BuildFloorCeiling(sunken, "SunkenFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), deepBlueMetal, deepBlueDark);

            // Ruin walls.
            BuildWall(sunken, "SunkenWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(sunken, "SunkenWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Ruin pillar props.
            var pillarColor = new Color(0.32f, 0.38f, 0.45f);
            BuildProp(sunken, "RuinPillar1", new Vector3(-2.5f, 1f, 6f), new Vector3(0.4f, 2f, 0.4f), pillarColor);
            BuildProp(sunken, "RuinPillar2", new Vector3(2.5f, 1f, 8f), new Vector3(0.4f, 2f, 0.4f), pillarColor);
            BuildProp(sunken, "RuinPillar3", new Vector3(-1.5f, 1f, 14f), new Vector3(0.4f, 2f, 0.4f), pillarColor);

            // ---- ArchiveRelay unlit core ----
            var archiveRelay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archiveRelay.name = "ArchiveRelay";
            Object.DestroyImmediate(archiveRelay.GetComponent<Collider>());
            archiveRelay.transform.SetParent(sunken, false);
            archiveRelay.transform.position = new Vector3(0f, 1.5f, 16f);
            archiveRelay.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
            archiveRelay.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.4f, 0.8f, 1f)); // dim cyan

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
            var waterApproachDialogue = BuildEp15DialoguePlayer("Dialogue_WaterApproach", new Vector3(0f, 1.5f, 2f), "water_approach");
            var waterDlgSo = new SerializedObject(waterApproachDialogue);
            waterDlgSo.FindProperty("playOnStart").boolValue = true;
            waterDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var khallConfessionDialogue = BuildEp15DialoguePlayer("Dialogue_KhallConfession", new Vector3(0f, 1.5f, 8f), "khall_confession");
            var sunkenUnderstandingDialogue = BuildEp15DialoguePlayer("Dialogue_SunkenUnderstanding", new Vector3(0f, 1.5f, 14f), "sunken_understanding");

            // ---- 5 Pale Choir Assassin enemies: 1 wave ----
            var assassinColor = new Color(0.80f, 0.82f, 0.85f); // pale white/ghostly tint
            var assassinWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5.5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(0f, 0f, 10f)
            };

            var assassinWaveHealths = new List<Health>();
            foreach (var pos in assassinWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, assassinColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                assassinWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var assassinSpawner = BuildWaveSpawner("PaleChoirSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { assassinWaveHealths },
                new[] { BuildEp15DialoguePlayer("Dialogue_PaleChoirBarks", new Vector3(0f, 1.5f, 8f), "pale_choir_barks") });

            // Transition box: "ASCEND — THE DESERT WORLD".
            var desertBoxGo = BuildTransitionBox("ToDesertBox", new Vector3(0f, 1.2f, 20.5f), "ASCEND — THE DESERT WORLD",
                out var desertBtn, out var desertTransition);
            var dbSo = new SerializedObject(desertTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep15DesertReckoningSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(desertBtn.onClick,
                new UnityEngine.Events.UnityAction(desertTransition.LoadOnFootScene));
            desertBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue water_approach (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Water Approach";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = waterApproachDialogue;

            // Step 1: Dialogue khall_confession.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Khall Confession";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = khallConfessionDialogue;

            // Step 2: DefeatWaves — 5 Pale Choir Assassins.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Pale Choir Assassins (5)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = assassinSpawner;

            // Step 3: Dialogue sunken_understanding.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Sunken Understanding";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = sunkenUnderstandingDialogue;

            // Step 4: Prompt — transition to Desert Reckoning.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Ascend to Desert World";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = desertBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15SunkenArchivesScenePath);
            EnsureScenesInBuild(Galaxy2Ep15SunkenArchivesScenePath, Galaxy2Ep15DesertReckoningScenePath);

            Debug.Log($"[Space Samurai] EP15 Sunken Archives scene built at {Galaxy2Ep15SunkenArchivesScenePath}. " +
                      "Layout: dark blue drowned ruins with dense fog (fogDensity ~0.04), ruin pillars, unlit ArchiveRelay core. " +
                      "5 Pale Choir Assassins (pale white/ghostly, nonLethal). " +
                      "5 steps: water_approach (auto) → khall_confession dialogue → defeat 5 Pale Choir Assassins (pale_choir_barks bark) → " +
                      "sunken_understanding dialogue → transition to Desert Reckoning.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Desert Reckoning", priority = 156)]
        public static void BuildEp15DesertReckoning()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Desert Reckoning: warm desert with tan/orange directional light and light dust fog.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.65f, 0.40f); // warm tan/orange key light
            light.intensity = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.28f, 0.18f); // warm sand ambient

            // Light dust fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.60f, 0.50f, 0.35f);
            RenderSettings.fogDensity = 0.018f;

            // One warm accent light (desert sun).
            BuildAccentPointLight("DesertSun", new Vector3(0f, 3f, 5f),
                new Color(1f, 0.75f, 0.45f), intensity: 1.0f, range: 20f);

            // ---- Desert arena ----
            var desertGo = new GameObject("DesertArena");
            var desert = desertGo.transform;
            var sandyColor = new Color(0.65f, 0.55f, 0.40f);
            var sandDark = new Color(0.45f, 0.38f, 0.28f);

            // Main desert floor.
            BuildFloorCeiling(desert, "DesertFloor", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), sandyColor, sandDark);

            // Central faceless Monument prop.
            BuildProp(desert, "Monument", new Vector3(0f, 1.5f, 10f), new Vector3(1.2f, 2.5f, 1.2f), new Color(0.55f, 0.50f, 0.45f));

            // A couple dune props.
            BuildProp(desert, "Dune1", new Vector3(-5f, 0.5f, 6f), new Vector3(2f, 1f, 2f), new Color(0.62f, 0.52f, 0.38f));
            BuildProp(desert, "Dune2", new Vector3(5f, 0.5f, 14f), new Vector3(2f, 1f, 2f), new Color(0.62f, 0.52f, 0.38f));

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
            var desertKhallDialogue = BuildEp15DialoguePlayer("Dialogue_DesertKhall", new Vector3(0f, 1.5f, 2f), "desert_khall");
            var khallDlgSo = new SerializedObject(desertKhallDialogue);
            khallDlgSo.FindProperty("playOnStart").boolValue = true;
            khallDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var desertAftermathDialogue = BuildEp15DialoguePlayer("Dialogue_DesertAftermath", new Vector3(0f, 1.5f, 12f), "desert_aftermath");
            var relayReprogramDialogue = BuildEp15DialoguePlayer("Dialogue_RelayReprogram", new Vector3(0f, 1.5f, 14f), "relay_reprogram");

            // ---- THREE-WAY BATTLE: 6 FactionCombatant units (3 Dominion faction 0, 3 Syndicate faction 1) ----
            var factionWaveHealths = new List<Health>();

            // 3 Dominion units (faction 0) — dark grey-blue tint.
            var dominionColor = new Color(0.32f, 0.38f, 0.48f);
            var dominionPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 6f),
                new Vector3(0f, 0f, 6.5f),
                new Vector3(3f, 0f, 6f)
            };

            foreach (var pos in dominionPositions)
            {
                var unitGo = new GameObject("DominionUnit");
                unitGo.transform.SetParent(desert, false);
                unitGo.transform.position = pos;

                // Primitive body (capsule). Keep its collider so the player's blade can hit it.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(unitGo.transform, false);
                TintShared(body.GetComponent<Renderer>(), dominionColor);

                // Health component.
                var health = unitGo.AddComponent<Health>();
                health.Configure(60f);

                // FactionCombatant component.
                var combatant = unitGo.AddComponent<FactionCombatant>();
                combatant.SetFaction(0);

                unitGo.SetActive(false);
                factionWaveHealths.Add(health);
            }

            // 3 Syndicate units (faction 1) — rust-red tint.
            var syndicateColor = new Color(0.65f, 0.35f, 0.25f);
            var syndicatePositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 12f),
                new Vector3(0f, 0f, 12.5f),
                new Vector3(2.5f, 0f, 12f)
            };

            foreach (var pos in syndicatePositions)
            {
                var unitGo = new GameObject("SyndicateUnit");
                unitGo.transform.SetParent(desert, false);
                unitGo.transform.position = pos;

                // Primitive body (capsule). Keep its collider so the player's blade can hit it.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(unitGo.transform, false);
                TintShared(body.GetComponent<Renderer>(), syndicateColor);

                // Health component.
                var health = unitGo.AddComponent<Health>();
                health.Configure(60f);

                // FactionCombatant component.
                var combatant = unitGo.AddComponent<FactionCombatant>();
                combatant.SetFaction(1);

                unitGo.SetActive(false);
                factionWaveHealths.Add(health);
            }

            // Build wave spawner with all 6 units.
            var factionSpawner = BuildWaveSpawner("DesertSpawner", new Vector3(0f, 0.5f, 10f), 3f,
                new List<List<Health>> { factionWaveHealths },
                new[] { BuildEp15DialoguePlayer("Dialogue_DesertBarks", new Vector3(0f, 1.5f, 10f), "desert_barks") });

            // Transition box: "LAUNCH — THE DREADNOUGHT".
            var dreadnoughtBoxGo = BuildTransitionBox("ToDreadnoughtBox", new Vector3(0f, 1.2f, 20.5f), "LAUNCH — THE DREADNOUGHT",
                out var dreadnoughtBtn, out var dreadnoughtTransition);
            var drSo = new SerializedObject(dreadnoughtTransition);
            drSo.FindProperty("onFootScene").stringValue = Galaxy2Ep15DreadnoughtSceneName;
            drSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dreadnoughtBtn.onClick,
                new UnityEngine.Events.UnityAction(dreadnoughtTransition.LoadOnFootScene));
            dreadnoughtBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue desert_khall (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Desert Khall";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = desertKhallDialogue;

            // Step 1: DefeatWaves — 6 FactionCombatant units.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Three-Way Battle (6 FactionCombatants)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = factionSpawner;

            // Step 2: Dialogue desert_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Desert Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = desertAftermathDialogue;

            // Step 3: Dialogue relay_reprogram.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Relay Reprogram";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = relayReprogramDialogue;

            // Step 4: Prompt — transition to Dreadnought.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Launch to Dreadnought";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = dreadnoughtBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15DesertReckoningScenePath);
            EnsureScenesInBuild(Galaxy2Ep15DesertReckoningScenePath);

            Debug.Log($"[Space Samurai] EP15 Desert Reckoning scene built at {Galaxy2Ep15DesertReckoningScenePath}. " +
                      "Layout: warm desert arena with sand-colored floor, Monument prop, dune props, light dust fog. " +
                      "6 FactionCombatant units (3 Dominion faction 0 dark grey-blue + 3 Syndicate faction 1 rust-red), each with Health + FactionCombatant. " +
                      "5 steps: desert_khall (auto) → defeat 6 FactionCombatant units (desert_barks bark, three-way battle) → " +
                      "desert_aftermath dialogue → relay_reprogram dialogue → transition to Dreadnought.");
        }
    }
}

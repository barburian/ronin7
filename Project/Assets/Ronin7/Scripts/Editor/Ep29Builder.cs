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
    /// EP29 "The Unbroken Bond" (Galaxy 4) scene builders for the first four on-foot scenes
    /// on Ketos Prime. The cold, dead neural-research station contains evidence of Cipher's
    /// sister and Samurai-4's neural conditioning. The player descends through docking bay,
    /// zero-G cryopod chamber, neural archive, and scan console, where a LeashBreakController
    /// mechanic forces Samurai-4 to read her own failsafe file and breaks her conditioning.
    /// - Sister Signal: cold station docking-bay entry with Dominion Hunter-Drones.
    /// - Zero Chamber: weightless cryopod chamber with Samurai-4 duel (ZeroGFloatController).
    /// - Neurovault: deepest neural-archive vault with Dominion acolytes.
    /// - Scan Console: neural-scan control room where LeashBreakController resolves Samurai-4's leash break.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 6 scenes declared here; scenes 5-6 built in Ep29BuilderFinale.cs.
        private const string Galaxy4Ep29SisterSignalScenePath     = SceneFolder + "/Galaxy4_EP29_SisterSignal.unity";
        private const string Galaxy4Ep29ZeroChamberScenePath      = SceneFolder + "/Galaxy4_EP29_ZeroChamber.unity";
        private const string Galaxy4Ep29NeurovaultScenePath       = SceneFolder + "/Galaxy4_EP29_Neurovault.unity";
        private const string Galaxy4Ep29ScanConsoleScenePath      = SceneFolder + "/Galaxy4_EP29_ScanConsole.unity";
        private const string Galaxy4Ep29ReactorCatwalkScenePath   = SceneFolder + "/Galaxy4_EP29_ReactorCatwalk.unity";
        private const string Galaxy4Ep29IntermissionScenePath     = SceneFolder + "/Galaxy4_EP29_Intermission.unity";

        private static readonly string Galaxy4Ep29SisterSignalSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29SisterSignalScenePath);
        private static readonly string Galaxy4Ep29ZeroChamberSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29ZeroChamberScenePath);
        private static readonly string Galaxy4Ep29NeurovaultSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29NeurovaultScenePath);
        private static readonly string Galaxy4Ep29ScanConsoleSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29ScanConsoleScenePath);
        private static readonly string Galaxy4Ep29ReactorCatwalkSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29ReactorCatwalkScenePath);
        private static readonly string Galaxy4Ep29IntermissionSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep29IntermissionScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP29 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep29" and loads lines from Ep29Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp29DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep29Lines.Get(setId), advanceRef, setId, clipPrefix: "ep29");
        }

        /// <summary>Standard EP29 on-foot scene scaffold shared by scenes 1-4: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp29OnFootShell(Object[] refs, WeaponDefinition weapon,
            Color keyLight, Color ambient, Color fogColor, float fogDensity,
            string structureName, Color accent1, Color accent2,
            Color floorLight, Color floorDark, Color propTint, out GameObject structureGo)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = keyLight;
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            BuildAccentPointLight(structureName + "Light1", new Vector3(-3f, 2f, 8f), accent1, intensity: 0.70f, range: 10f);
            BuildAccentPointLight(structureName + "Light2", new Vector3(3f, 2.5f, 12f), accent2, intensity: 0.65f, range: 9f);

            structureGo = new GameObject(structureName);
            var s = structureGo.transform;
            BuildFloorCeiling(s, structureName + "Floor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), floorLight, floorDark);
            BuildWall(s, structureName + "Wall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(s, structureName + "Wall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Standardized tower/panel props.
            BuildProp(s, "Tower1", new Vector3(-3f, 1.5f, 8f), new Vector3(0.9f, 2.4f, 0.9f), propTint);
            BuildProp(s, "Tower2", new Vector3(3f, 1.5f, 12f), new Vector3(0.9f, 2.4f, 0.9f), propTint);
            BuildProp(s, "Tower3", new Vector3(-2f, 1.5f, 16f), new Vector3(0.9f, 2.4f, 0.9f), propTint);

            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            return playerHealth;
        }

        /// <summary>Adds a non-remote StoryNpc capsule. Returns the GameObject so callers can optionally
        /// add an AllyCombatant or other components.</summary>
        private static GameObject BuildEp29Npc(string displayName, Vector3 position, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = displayName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(go.GetComponent<Renderer>(), tint);
            var npc = go.AddComponent<StoryNpc>();
            var so = new SerializedObject(npc);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("remote").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void FinishEp29Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
        {
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, scenePath);
            if (string.IsNullOrEmpty(nextScenePath)) EnsureScenesInBuild(scenePath);
            else EnsureScenesInBuild(scenePath, nextScenePath);
        }

        // Ketos Prime tint: cold steel grey (neutral, cold research station)
        private static readonly Color Ep29ColdSteel = new Color(0.45f, 0.46f, 0.50f);
        // Ketos Prime accent: deep blue (neural/void theme)
        private static readonly Color Ep29DeepBlue = new Color(0.35f, 0.42f, 0.55f);
        // Ketos Prime accent: pale cold grey
        private static readonly Color Ep29PaleCold = new Color(0.58f, 0.60f, 0.65f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Sister Signal", priority = 302)]
        public static void BuildEp29SisterSignal()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sister Signal: cold steel-grey station docking-bay entry hall.
            var playerHealth = BuildEp29OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.64f, 0.70f),     // cool pale steel
                ambient: new Color(0.12f, 0.12f, 0.15f),      // dark cool
                fogColor: new Color(0.26f, 0.28f, 0.32f), fogDensity: 0.012f,
                structureName: "SisterSignalBay",
                accent1: new Color(0.35f, 0.42f, 0.55f),      // deep blue neural
                accent2: new Color(0.58f, 0.60f, 0.65f),      // pale cold grey
                floorLight: new Color(0.50f, 0.52f, 0.56f), floorDark: new Color(0.28f, 0.30f, 0.34f),
                propTint: new Color(0.44f, 0.46f, 0.50f), out _);

            // ---- Dominion Hunter-Drones (lethal, dark steel tint) in 1 wave ----
            var dronePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(0f, 0f, 10f),
            };
            var droneHealths = new List<Health>();
            foreach (var pos in dronePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep29ColdSteel);
                enemy.gameObject.SetActive(false);
                droneHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildEp03WaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneHealths },
                new[] { BuildEp29DialoguePlayer("Dialogue_SisterSignal", new Vector3(0f, 1.5f, 8f), "sister_signal") });

            // ---- Transition box: "INTO THE STATION" ----
            var stationBoxGo = BuildTransitionBox("ToStationBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE STATION",
                out var stationBtn, out var stationTransition);
            var sbSo = new SerializedObject(stationTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep29ZeroChamberSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(stationBtn.onClick,
                new UnityEngine.Events.UnityAction(stationTransition.LoadOnFootScene));
            stationBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Hunter-Drones (3, sister_signal)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Sister Signal (plays on spawn)";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = null; // Dialogue plays on spawn, not via step

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Into the Station";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = stationBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp29Scene(scene, Galaxy4Ep29SisterSignalScenePath, Galaxy4Ep29ZeroChamberScenePath);

            Debug.Log($"[Space Samurai] EP29 Sister Signal scene built at {Galaxy4Ep29SisterSignalScenePath}. " +
                      "Cold steel-grey docking-bay entry (cool pale steel, deep blue + pale cold accents). " +
                      "3 Dominion Hunter-Drones (dark steel, lethal, sister_signal spawn bark). " +
                      "3 steps: defeat 3 drones → dialogue step → INTO THE STATION.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Zero Chamber", priority = 303)]
        public static void BuildEp29ZeroChamber()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Zero Chamber: central zero-G cryopod chamber, cold void-blue palette.
            var playerHealth = BuildEp29OnFootShell(refs, weapon,
                keyLight: new Color(0.58f, 0.60f, 0.66f),     // pale cool
                ambient: new Color(0.10f, 0.10f, 0.13f),      // dark void
                fogColor: new Color(0.24f, 0.26f, 0.32f), fogDensity: 0.014f,
                structureName: "ZeroChamber",
                accent1: new Color(0.32f, 0.40f, 0.55f),      // void blue
                accent2: new Color(0.50f, 0.55f, 0.65f),      // pale blue accent
                floorLight: new Color(0.48f, 0.50f, 0.54f), floorDark: new Color(0.26f, 0.28f, 0.32f),
                propTint: new Color(0.42f, 0.44f, 0.48f), out _);

            // ---- ZeroGFloatController for weightless drifting ----
            var floatGo = new GameObject("ZeroGFloat");
            var floatController = floatGo.AddComponent<ZeroGFloatController>();

            // ---- Samurai-4 DUEL opponent (non-lethal, steel-blue tint) ----
            var samurai4Tint = new Color(0.40f, 0.42f, 0.50f);
            var samurai4 = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var samurai4Go = samurai4.gameObject;
            samurai4Go.name = "Samurai-4";
            var samurai4Renderer = samurai4.GetComponent<Renderer>();
            if (samurai4Renderer != null) TintShared(samurai4Renderer, samurai4Tint);
            var samurai4Health = samurai4.GetComponent<Health>();
            var samurai4Melee = samurai4.GetComponent<MeleeAttacker>();
            if (samurai4Melee != null)
            {
                var maSo = new SerializedObject(samurai4Melee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Add Samurai-4 to ZeroGFloatController's members list
            var floatSo = new SerializedObject(floatController);
            SetObjectRefList(floatSo, "members", new List<Object> { samurai4Melee });
            floatSo.ApplyModifiedPropertiesWithoutUndo();

            // Re-find the player sword for DuelYield wiring.
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // DuelYield: yield at 0.25. Wired as null-prompt step advanced by onAccepted.
            var duelYield = samurai4Go.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", samurai4Health);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.25f;
            if (samurai4Melee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { samurai4Melee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // duel_yield bark plays when Samurai-4 yields.
            var duelYieldDialogue = BuildEp29DialoguePlayer("Dialogue_DuelYield", new Vector3(0f, 1.5f, 12f), "duel_yield");
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(duelYieldDialogue.Play));

            samurai4Go.SetActive(false);

            // ---- Dialogue Players ----
            var duelIntroDialogue = BuildEp29DialoguePlayer("Dialogue_DuelIntro", new Vector3(0f, 1.5f, 2f), "duel_intro");
            var diSo = new SerializedObject(duelIntroDialogue);
            diSo.FindProperty("playOnStart").boolValue = true;
            diSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "DESCEND — THE NEUROVAULT".
            var neurovaultBoxGo = BuildTransitionBox("ToNeurovaultBox", new Vector3(0f, 1.2f, 21.5f), "DESCEND — THE NEUROVAULT",
                out var neurovaultBtn, out var neurovaultTransition);
            var nbSo = new SerializedObject(neurovaultTransition);
            nbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep29NeurovaultSceneName;
            nbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(neurovaultBtn.onClick,
                new UnityEngine.Events.UnityAction(neurovaultTransition.LoadOnFootScene));
            neurovaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Duel Intro (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = duelIntroDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Activate Samurai-4";
            var activateProp1 = s1.FindPropertyRelative("triggerObjects");
            activateProp1.arraySize = 1;
            activateProp1.GetArrayElementAtIndex(0).objectReferenceValue = samurai4Go;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Duel Samurai-4 (DuelYield onAccepted advances)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = null;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend — The Neurovault";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = neurovaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → AdvanceFromPrompt (advance the null-prompt duel step).
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            FinishEp29Scene(scene, Galaxy4Ep29ZeroChamberScenePath, Galaxy4Ep29NeurovaultScenePath);

            Debug.Log($"[Space Samurai] EP29 Zero Chamber scene built at {Galaxy4Ep29ZeroChamberScenePath}. " +
                      "Weightless cryopod chamber (void blue + pale blue accents). " +
                      "Samurai-4 opponent (steel-blue, nonLethal, DuelYield at 0.25) with ZeroGFloatController bob/sway. " +
                      "4 steps: duel_intro (auto) → Trigger (activate Samurai-4) → Prompt(null, duel) → Prompt(descent).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Neurovault", priority = 304)]
        public static void BuildEp29Neurovault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Neurovault: deepest neural-archive vault, darkest cold palette.
            var playerHealth = BuildEp29OnFootShell(refs, weapon,
                keyLight: new Color(0.55f, 0.57f, 0.62f),     // pale cool dark
                ambient: new Color(0.08f, 0.08f, 0.10f),      // darkest void
                fogColor: new Color(0.22f, 0.24f, 0.28f), fogDensity: 0.016f,
                structureName: "NeurovaultVault",
                accent1: new Color(0.30f, 0.38f, 0.52f),      // darkest void blue
                accent2: new Color(0.48f, 0.52f, 0.60f),      // pale cold blue
                floorLight: new Color(0.46f, 0.48f, 0.52f), floorDark: new Color(0.24f, 0.26f, 0.30f),
                propTint: new Color(0.40f, 0.42f, 0.46f), out _);

            // ---- Dominion acolytes (lethal, dark tint) in 1 wave ----
            var acolytePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1f, 0f, 10.5f),
                new Vector3(1f, 0f, 11f),
            };
            var acolyteHealths = new List<Health>();
            foreach (var pos in acolytePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep29ColdSteel);
                enemy.gameObject.SetActive(false);
                acolyteHealths.Add(enemy.GetComponent<Health>());
            }

            var acolyteSpawner = BuildEp03WaveSpawner("AcolyteSpawner", new Vector3(0f, 0.5f, 9f), 2f,
                new List<List<Health>> { acolyteHealths },
                new DialoguePlayer[0]); // No spawn dialogue for archive guardians

            // ---- Dialogue Players ----
            var archiveDescentDialogue = BuildEp29DialoguePlayer("Dialogue_ArchiveDescent", new Vector3(0f, 1.5f, 2f), "archive_descent");
            var adSo = new SerializedObject(archiveDescentDialogue);
            adSo.FindProperty("playOnStart").boolValue = true;
            adSo.ApplyModifiedPropertiesWithoutUndo();

            var neuralScarDialogue = BuildEp29DialoguePlayer("Dialogue_NeuralScar", new Vector3(0f, 1.5f, 14f), "neural_scar");

            // Transition box: "THE SCAN CONSOLE".
            var consoleBoxGo = BuildTransitionBox("ToConsoleBox", new Vector3(0f, 1.2f, 21.5f), "THE SCAN CONSOLE",
                out var consoleBtn, out var consoleTransition);
            var cbSo = new SerializedObject(consoleTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep29ScanConsoleSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(consoleBtn.onClick,
                new UnityEngine.Events.UnityAction(consoleTransition.LoadOnFootScene));
            consoleBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archive Descent (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveDescentDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Acolytes (5, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = acolyteSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Neural Scar";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = neuralScarDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: The Scan Console";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = consoleBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp29Scene(scene, Galaxy4Ep29NeurovaultScenePath, Galaxy4Ep29ScanConsoleScenePath);

            Debug.Log($"[Space Samurai] EP29 Neurovault scene built at {Galaxy4Ep29NeurovaultScenePath}. " +
                      "Deepest neural-archive vault (darkest void blue + pale cold blue accents). " +
                      "5 Dominion acolytes (dark steel, lethal). " +
                      "4 steps: archive_descent (auto) → defeat 5 acolytes → neural_scar → THE SCAN CONSOLE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Scan Console", priority = 305)]
        public static void BuildEp29ScanConsole()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Scan Console: neural-scan control room, cold palette with bright console accent.
            var playerHealth = BuildEp29OnFootShell(refs, weapon,
                keyLight: new Color(0.60f, 0.62f, 0.68f),     // pale cool
                ambient: new Color(0.11f, 0.11f, 0.14f),      // cool dim
                fogColor: new Color(0.25f, 0.27f, 0.31f), fogDensity: 0.013f,
                structureName: "ScanControlRoom",
                accent1: new Color(0.34f, 0.41f, 0.53f),      // cool blue accent
                accent2: new Color(0.70f, 0.60f, 0.40f),      // bright warm console accent
                floorLight: new Color(0.49f, 0.51f, 0.55f), floorDark: new Color(0.27f, 0.29f, 0.33f),
                propTint: new Color(0.43f, 0.45f, 0.49f), out _);

            // ---- Samurai-4 DUEL opponent again (non-lethal, steel tint, high health for mechanic) ----
            var samurai4Tint = new Color(0.40f, 0.42f, 0.50f);
            var samurai4 = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var samurai4Go = samurai4.gameObject;
            samurai4Go.name = "Samurai-4";
            var samurai4Renderer = samurai4.GetComponent<Renderer>();
            if (samurai4Renderer != null) TintShared(samurai4Renderer, samurai4Tint);
            var samurai4Health = samurai4.GetComponent<Health>();
            var samurai4Melee = samurai4.GetComponent<MeleeAttacker>();
            if (samurai4Melee != null)
            {
                var maSo = new SerializedObject(samurai4Melee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Add LeashBreakController to Samurai-4 (only ticks when she is active).
            var leashBreak = samurai4Go.AddComponent<LeashBreakController>();
            var lbSo = new SerializedObject(leashBreak);
            lbSo.FindProperty("startingConviction").floatValue = 1f;
            lbSo.FindProperty("breakThreshold").floatValue = 0.25f;
            lbSo.FindProperty("convictionDrainPerSecond").floatValue = 0.08f;
            lbSo.FindProperty("evidenceDrainAmount").floatValue = 0.4f;
            lbSo.FindProperty("autoAdvance").boolValue = true;
            lbSo.ApplyModifiedPropertiesWithoutUndo();

            samurai4Go.SetActive(false);

            // ---- Dialogue Players ----
            var implantEvidenceDialogue = BuildEp29DialoguePlayer("Dialogue_ImplantEvidence", new Vector3(0f, 1.5f, 2f), "implant_evidence");
            var ieSo = new SerializedObject(implantEvidenceDialogue);
            ieSo.FindProperty("playOnStart").boolValue = true;
            ieSo.ApplyModifiedPropertiesWithoutUndo();

            var leashBreakDialogue = BuildEp29DialoguePlayer("Dialogue_LeashBreak", new Vector3(0f, 1.5f, 12f), "leash_break");

            // Transition box: "ESCAPE — THE REACTOR".
            var reactorBoxGo = BuildTransitionBox("ToReactorBox", new Vector3(0f, 1.2f, 21.5f), "ESCAPE — THE REACTOR",
                out var reactorBtn, out var reactorTransition);
            var rbSo = new SerializedObject(reactorTransition);
            rbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep29ReactorCatwalkSceneName;
            rbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(reactorBtn.onClick,
                new UnityEngine.Events.UnityAction(reactorTransition.LoadOnFootScene));
            reactorBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Implant Evidence (playOnStart, Cipher presents failsafe)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = implantEvidenceDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Activate Samurai-4 (starts LeashBreakController drain)";
            var activateProp1 = s1.FindPropertyRelative("triggerObjects");
            activateProp1.arraySize = 1;
            activateProp1.GetArrayElementAtIndex(0).objectReferenceValue = samurai4Go;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: LeashBreak Samurai-4 (onLeashBreak advances)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = null;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Escape — The Reactor";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = reactorBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire LeashBreakController.onLeashBreak → (a) leashBreakDialogue.Play and (b) AdvanceFromPrompt.
            UnityEventTools.AddPersistentListener(leashBreak.onLeashBreak,
                new UnityEngine.Events.UnityAction(leashBreakDialogue.Play));
            UnityEventTools.AddPersistentListener(leashBreak.onLeashBreak,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            FinishEp29Scene(scene, Galaxy4Ep29ScanConsoleScenePath, Galaxy4Ep29ReactorCatwalkScenePath);

            Debug.Log($"[Space Samurai] EP29 Scan Console scene built at {Galaxy4Ep29ScanConsoleScenePath}. " +
                      "Neural-scan control room (cool + warm console accent). " +
                      "Samurai-4 opponent (steel tint, nonLethal) with LeashBreakController (conviction drain: 0.08/s passive, 0.4 per evidence read). " +
                      "4 steps: implant_evidence (auto, failsafe discovery) → Trigger (activate Samurai-4) → Prompt(null, leash break) → Prompt(escape).");
        }
    }
}

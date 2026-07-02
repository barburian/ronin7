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
    /// EP31 "The Eternal Cycle" (Galaxy 4) scene builders for the five on-foot scenes
    /// at Chronus Prime, an isolated world locked in temporal recursion. Soren crashes into
    /// a looping isolation sphere where Dr. Lyssa Chen and echoes of Dr. Heris reveal that
    /// his chip's deepest layer is a time-lock: a contingency weapon designed to trap his
    /// identity in recursive erasure the moment he became whole. The episode follows Soren
    /// through the repeating pattern of the sphere's architecture toward the temporal nexus
    /// where the loop originates. Dr. Marcus Renn (the sphere's architect, now trapped in
    /// the rogue AI) meets him at the loop's origin, and Soren breaks the time-lock, ending
    /// the cycle and emerging whole.
    /// - Armed Layer: Kessler's descent + CorruptedEcho guardian.
    /// - Repeating Pattern: Dr. Lyssa Chen intro + PatrolDrone wave.
    /// - Architect's Echo: Heris's holographic revelation of the time-lock protocol.
    /// - Fractured Crew: Lyssa's warning + CorruptedScientist wave.
    /// - Temporal Nexus: Lyssa Vale reveal + two-wave climax (QuantumPhantom + EchoSwarm).
    /// - One Day: SPACE finale with Dr. Marcus Renn reveal and time-lock destruction (in Ep31BuilderFinale.cs).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 6 scenes declared here; scene 6 built in Ep31BuilderFinale.cs.
        private const string Galaxy4Ep31ArmedLayerScenePath        = SceneFolder + "/Galaxy4_EP31_ArmedLayer.unity";
        private const string Galaxy4Ep31RepeatingPatternScenePath  = SceneFolder + "/Galaxy4_EP31_RepeatingPattern.unity";
        private const string Galaxy4Ep31ArchitectsEchoScenePath    = SceneFolder + "/Galaxy4_EP31_ArchitectsEcho.unity";
        private const string Galaxy4Ep31FracturedCrewScenePath     = SceneFolder + "/Galaxy4_EP31_FracturedCrew.unity";
        private const string Galaxy4Ep31TemporalNexusScenePath     = SceneFolder + "/Galaxy4_EP31_TemporalNexus.unity";
        private const string Galaxy4Ep31OneDayScenePath            = SceneFolder + "/Galaxy4_EP31_OneDay.unity";

        private static readonly string Galaxy4Ep31ArmedLayerSceneName        = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31ArmedLayerScenePath);
        private static readonly string Galaxy4Ep31RepeatingPatternSceneName  = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31RepeatingPatternScenePath);
        private static readonly string Galaxy4Ep31ArchitectsEchoSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31ArchitectsEchoScenePath);
        private static readonly string Galaxy4Ep31FracturedCrewSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31FracturedCrewScenePath);
        private static readonly string Galaxy4Ep31TemporalNexusSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31TemporalNexusScenePath);
        private static readonly string Galaxy4Ep31OneDaySceneName            = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep31OneDayScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP31 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep31" and loads lines from Ep31Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp31DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep31Lines.Get(setId), advanceRef, setId, clipPrefix: "ep31");
        }

        /// <summary>Standard EP31 on-foot scene scaffold: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp31OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        private static GameObject BuildEp31Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp31Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Chronus Prime tint palette: warm desert-sand, temporal-cyan accents
        private static readonly Color Ep31Sand        = new Color(0.70f, 0.58f, 0.38f);
        private static readonly Color Ep31RustOchre   = new Color(0.55f, 0.38f, 0.24f);
        private static readonly Color Ep31TemporalCyan = new Color(0.30f, 0.62f, 0.68f);

        /// <summary>Builds a TimeLoopController with two flickering phase-platform groups (temporal phase visualization).</summary>
        private static Ronin7.World.TimeLoopController BuildEp31TimeLoop(string name, Vector3 center, Color tintA, Color tintB)
        {
            var root = new GameObject(name).transform;
            root.position = center;
            var phaseA = new System.Collections.Generic.List<GameObject>();
            var phaseB = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
                a.name = name + "_A" + i;
                a.transform.SetParent(root, false);
                a.transform.localPosition = new Vector3(-2.5f + i * 0.6f, 0.05f, 4f + i * 2f);
                a.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
                var ac = a.GetComponent<Collider>(); if (ac != null) ac.enabled = false;
                TintShared(a.GetComponent<Renderer>(), tintA);
                phaseA.Add(a);

                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = name + "_B" + i;
                b.transform.SetParent(root, false);
                b.transform.localPosition = new Vector3(2.5f - i * 0.6f, 0.05f, 5f + i * 2f);
                b.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
                var bc = b.GetComponent<Collider>(); if (bc != null) bc.enabled = false;
                TintShared(b.GetComponent<Renderer>(), tintB);
                phaseB.Add(b);
            }
            var tl = root.gameObject.AddComponent<Ronin7.World.TimeLoopController>();
            var so = new SerializedObject(tl);
            so.FindProperty("phaseInterval").floatValue = 2f;
            var groups = so.FindProperty("phaseGroups");
            groups.arraySize = 2;
            var m0 = groups.GetArrayElementAtIndex(0).FindPropertyRelative("members");
            m0.arraySize = phaseA.Count;
            for (int i = 0; i < phaseA.Count; i++) m0.GetArrayElementAtIndex(i).objectReferenceValue = phaseA[i];
            var m1 = groups.GetArrayElementAtIndex(1).FindPropertyRelative("members");
            m1.arraySize = phaseB.Count;
            for (int i = 0; i < phaseB.Count; i++) m1.GetArrayElementAtIndex(i).objectReferenceValue = phaseB[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return tl;
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 Armed Layer", priority = 316)]
        public static void BuildEp31ArmedLayer()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Armed Layer: warm sand-palette shuttle bay where Soren lands and encounters the CorruptedEcho.
            var playerHealth = BuildEp31OnFootShell(refs, weapon,
                keyLight: new Color(0.75f, 0.70f, 0.62f),     // warm pale sand
                ambient: new Color(0.15f, 0.12f, 0.10f),      // warm dim
                fogColor: new Color(0.35f, 0.30f, 0.25f), fogDensity: 0.010f,
                structureName: "ShuttleBay",
                accent1: new Color(0.65f, 0.55f, 0.40f),      // rust ochre
                accent2: new Color(0.70f, 0.60f, 0.45f),      // warm sand
                floorLight: new Color(0.72f, 0.65f, 0.52f), floorDark: new Color(0.40f, 0.35f, 0.28f),
                propTint: new Color(0.60f, 0.50f, 0.38f), out _);

            BuildEp31TimeLoop("TimeLoop", new Vector3(0f, 0f, 10f), Ep31TemporalCyan, Ep31RustOchre);

            // ---- Kessler descent dialogue (playOnStart) ----
            var kesslerDescentDialogue = BuildEp31DialoguePlayer("Dialogue_KesslerDescent", new Vector3(0f, 1.5f, 2f), "kessler_descent");
            var kdSo = new SerializedObject(kesslerDescentDialogue);
            kdSo.FindProperty("playOnStart").boolValue = true;
            kdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Echo welcome dialogue ----
            var echoWelcomeDialogue = BuildEp31DialoguePlayer("Dialogue_EchoWelcome", new Vector3(0f, 1.5f, 12f), "echo_welcome");

            // ---- CorruptedEcho boss (lethal, bluish tint) ----
            var echo = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var echoRenderer = echo.GetComponent<Renderer>();
            if (echoRenderer != null) TintShared(echoRenderer, new Color(0.30f, 0.40f, 0.55f)); // bluish steel
            echo.gameObject.SetActive(false);
            var echoHealth = echo.GetComponent<Health>();

            var echoSpawner = BuildWaveSpawner("EchoSpawner", new Vector3(0f, 0.5f, 12f), 1f,
                new List<List<Health>> { new List<Health> { echoHealth } },
                new DialoguePlayer[0]);

            // ---- Echo dying dialogue ----
            var echoDyingDialogue = BuildEp31DialoguePlayer("Dialogue_EchoDying", new Vector3(0f, 1.5f, 12f), "echo_dying");

            // ---- Transition box: "ENTER THE OBSERVATORY" ----
            var observatoryBoxGo = BuildTransitionBox("ToObservatoryBox", new Vector3(0f, 1.2f, 21.5f), "ENTER THE OBSERVATORY",
                out var observatoryBtn, out var observatoryTransition);
            var obSo = new SerializedObject(observatoryTransition);
            obSo.FindProperty("onFootScene").stringValue = Galaxy4Ep31RepeatingPatternSceneName;
            obSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(observatoryBtn.onClick,
                new UnityEngine.Events.UnityAction(observatoryTransition.LoadOnFootScene));
            observatoryBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Descent (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = kesslerDescentDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Echo Welcome";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = echoWelcomeDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: CorruptedEcho (1, lethal)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = echoSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Echo Dying";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = echoDyingDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Enter The Observatory";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = observatoryBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp31Scene(scene, Galaxy4Ep31ArmedLayerScenePath, Galaxy4Ep31RepeatingPatternScenePath);

            Debug.Log($"[Space Samurai] EP31 Armed Layer scene built at {Galaxy4Ep31ArmedLayerScenePath}. " +
                      "Warm sand-palette shuttle bay (warm pale sand, rust ochre + warm sand accents, temporal flicker platforms). " +
                      "CorruptedEcho boss (bluish steel tint, lethal, single enemy). " +
                      "5 steps: kessler_descent (auto) → echo_welcome → defeat CorruptedEcho → echo_dying → ENTER THE OBSERVATORY.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 Repeating Pattern", priority = 317)]
        public static void BuildEp31RepeatingPattern()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Repeating Pattern: observatory with warm-to-cool transition, Dr. Lyssa Chen present.
            var playerHealth = BuildEp31OnFootShell(refs, weapon,
                keyLight: new Color(0.72f, 0.68f, 0.60f),     // warm pale sand transitioning cool
                ambient: new Color(0.14f, 0.13f, 0.12f),      // warm to cool dim
                fogColor: new Color(0.33f, 0.30f, 0.28f), fogDensity: 0.010f,
                structureName: "Observatory",
                accent1: new Color(0.60f, 0.50f, 0.38f),      // rust ochre
                accent2: new Color(0.68f, 0.58f, 0.42f),      // warm sand cooler
                floorLight: new Color(0.70f, 0.63f, 0.50f), floorDark: new Color(0.38f, 0.33f, 0.26f),
                propTint: new Color(0.58f, 0.48f, 0.36f), out _);

            BuildEp31TimeLoop("TimeLoop", new Vector3(0f, 0f, 10f), Ep31TemporalCyan, Ep31RustOchre);

            // ---- Lyssa intro dialogue (playOnStart) ----
            var lyssaIntroDialogue = BuildEp31DialoguePlayer("Dialogue_LyssaIntro", new Vector3(0f, 1.5f, 2f), "lyssa_intro");
            var liSo = new SerializedObject(lyssaIntroDialogue);
            liSo.FindProperty("playOnStart").boolValue = true;
            liSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Core presence dialogue ----
            var corePresenceDialogue = BuildEp31DialoguePlayer("Dialogue_CorePresence", new Vector3(0f, 1.5f, 12f), "core_presence");

            // ---- Dr. Lyssa Chen NPC ----
            BuildEp31Npc("Dr. Lyssa Chen", new Vector3(-2f, 0f, 3f), new Color(0.6f, 0.6f, 0.62f));

            // ---- PatrolDrone enemies (dark Dominion tint, 3 in 1 wave) ----
            var dronePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 9f),
                new Vector3(0f, 0f, 11f),
            };
            var droneHealths = new List<Health>();
            foreach (var pos in dronePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.32f, 0.40f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                droneHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 9f), 2f,
                new List<List<Health>> { droneHealths },
                new DialoguePlayer[0]);

            // ---- Transition box: "DESCEND TO THE CORE" ----
            var coreBoxGo = BuildTransitionBox("ToCoreBox", new Vector3(0f, 1.2f, 21.5f), "DESCEND TO THE CORE",
                out var coreBtn, out var coreTransition);
            var cbSo = new SerializedObject(coreTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep31ArchitectsEchoSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(coreBtn.onClick,
                new UnityEngine.Events.UnityAction(coreTransition.LoadOnFootScene));
            coreBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Lyssa Intro (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lyssaIntroDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: PatrolDrones (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Core Presence";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = corePresenceDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend To The Core";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = coreBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp31Scene(scene, Galaxy4Ep31RepeatingPatternScenePath, Galaxy4Ep31ArchitectsEchoScenePath);

            Debug.Log($"[Space Samurai] EP31 Repeating Pattern scene built at {Galaxy4Ep31RepeatingPatternScenePath}. " +
                      "Observatory (warm-to-cool palette, rust ochre + warm sand accents, temporal flicker platforms). " +
                      "Dr. Lyssa Chen NPC (grey tint). 3 PatrolDrones (dark Dominion tint). " +
                      "4 steps: lyssa_intro (auto, cycle count + isolation sphere degradation) → defeat 3 drones → core_presence (Heris echo reveal) → DESCEND TO THE CORE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 Architects Echo", priority = 318)]
        public static void BuildEp31ArchitectsEcho()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Architect's Echo: cool/temporal palette with cyan accents, observatory tower.
            var playerHealth = BuildEp31OnFootShell(refs, weapon,
                keyLight: new Color(0.65f, 0.68f, 0.72f),     // cool pale steel
                ambient: new Color(0.12f, 0.13f, 0.16f),      // cool dim
                fogColor: new Color(0.28f, 0.32f, 0.36f), fogDensity: 0.011f,
                structureName: "ObservatoryTower",
                accent1: new Color(0.30f, 0.62f, 0.68f),      // temporal cyan
                accent2: new Color(0.50f, 0.62f, 0.68f),      // cyan-leaning steel
                floorLight: new Color(0.60f, 0.63f, 0.68f), floorDark: new Color(0.30f, 0.33f, 0.38f),
                propTint: new Color(0.48f, 0.52f, 0.58f), out _);

            BuildEp31TimeLoop("TimeLoop", new Vector3(0f, 0f, 10f), Ep31TemporalCyan, Ep31RustOchre);

            // ---- Heris echo dialogue (playOnStart) ----
            var herisEchoDialogue = BuildEp31DialoguePlayer("Dialogue_HerisEcho", new Vector3(0f, 1.5f, 2f), "heris_echo");
            var heSo = new SerializedObject(herisEchoDialogue);
            heSo.FindProperty("playOnStart").boolValue = true;
            heSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Heris comms dialogue ----
            var herisCommsDialogue = BuildEp31DialoguePlayer("Dialogue_HerisComms", new Vector3(0f, 1.5f, 12f), "heris_comms");

            // ---- SelfEcho enemies (bluish tint like scene 1, 3 in 1 wave) ----
            var selfEchoPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 9f),
                new Vector3(0f, 0f, 11f),
            };
            var selfEchoHealths = new List<Health>();
            foreach (var pos in selfEchoPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.40f, 0.55f)); // bluish steel
                enemy.gameObject.SetActive(false);
                selfEchoHealths.Add(enemy.GetComponent<Health>());
            }

            var selfEchoSpawner = BuildWaveSpawner("SelfEchoSpawner", new Vector3(0f, 0.5f, 9f), 2f,
                new List<List<Health>> { selfEchoHealths },
                new DialoguePlayer[0]);

            // ---- Transition box: "CONTINUE" ----
            var fracturedBoxGo = BuildTransitionBox("ToFracturedBox", new Vector3(0f, 1.2f, 21.5f), "CONTINUE",
                out var fracturedBtn, out var fracturedTransition);
            var fbSo = new SerializedObject(fracturedTransition);
            fbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep31FracturedCrewSceneName;
            fbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(fracturedBtn.onClick,
                new UnityEngine.Events.UnityAction(fracturedTransition.LoadOnFootScene));
            fracturedBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Heris Echo (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = herisEchoDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: SelfEchoes (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = selfEchoSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Heris Comms";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = herisCommsDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Continue";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = fracturedBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp31Scene(scene, Galaxy4Ep31ArchitectsEchoScenePath, Galaxy4Ep31FracturedCrewScenePath);

            Debug.Log($"[Space Samurai] EP31 Architects Echo scene built at {Galaxy4Ep31ArchitectsEchoScenePath}. " +
                      "Observatory tower (cool palette, temporal cyan + cyan-leaning steel accents, temporal flicker platforms). " +
                      "3 SelfEcho enemies (bluish steel tint). " +
                      "4 steps: heris_echo (auto, time-lock protocol revelation) → defeat 3 echoes → heris_comms (protocol mechanics) → CONTINUE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 Fractured Crew", priority = 319)]
        public static void BuildEp31FracturedCrew()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fractured Crew: cool palette, fractured corridor.
            var playerHealth = BuildEp31OnFootShell(refs, weapon,
                keyLight: new Color(0.63f, 0.66f, 0.70f),     // cool pale steel
                ambient: new Color(0.11f, 0.12f, 0.15f),      // cool dim
                fogColor: new Color(0.27f, 0.30f, 0.34f), fogDensity: 0.011f,
                structureName: "FracturedCorridor",
                accent1: new Color(0.32f, 0.50f, 0.65f),      // cooler temporal
                accent2: new Color(0.48f, 0.58f, 0.68f),      // steel blue
                floorLight: new Color(0.58f, 0.61f, 0.66f), floorDark: new Color(0.28f, 0.31f, 0.36f),
                propTint: new Color(0.46f, 0.50f, 0.56f), out _);

            BuildEp31TimeLoop("TimeLoop", new Vector3(0f, 0f, 10f), Ep31TemporalCyan, Ep31RustOchre);

            // ---- Lyssa threat dialogue (playOnStart) ----
            var lyssaThreatDialogue = BuildEp31DialoguePlayer("Dialogue_LyssaThreat", new Vector3(0f, 1.5f, 2f), "lyssa_threat");
            var ltSo = new SerializedObject(lyssaThreatDialogue);
            ltSo.FindProperty("playOnStart").boolValue = true;
            ltSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Heris orphanage dialogue ----
            var herisOrphanageDialogue = BuildEp31DialoguePlayer("Dialogue_HerisOrphanage", new Vector3(0f, 1.5f, 12f), "heris_orphanage");

            // ---- CorruptedScientist enemies (dark Dominion tint, 3 in 1 wave) ----
            var scientistPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 10f),
                new Vector3(2f, 0f, 8.5f),
            };
            var scientistHealths = new List<Health>();
            foreach (var pos in scientistPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.32f, 0.40f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                scientistHealths.Add(enemy.GetComponent<Health>());
            }

            var scientistSpawner = BuildWaveSpawner("ScientistSpawner", new Vector3(0f, 0.5f, 9f), 2f,
                new List<List<Health>> { scientistHealths },
                new DialoguePlayer[0]);

            // ---- Transition box: "ENTER THE NEXUS" ----
            var nexusBoxGo = BuildTransitionBox("ToNexusBox", new Vector3(0f, 1.2f, 21.5f), "ENTER THE NEXUS",
                out var nexusBtn, out var nexusTransition);
            var nbSo = new SerializedObject(nexusTransition);
            nbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep31TemporalNexusSceneName;
            nbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(nexusBtn.onClick,
                new UnityEngine.Events.UnityAction(nexusTransition.LoadOnFootScene));
            nexusBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Lyssa Threat (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lyssaThreatDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: CorruptedScientists (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = scientistSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Heris Orphanage";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = herisOrphanageDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Enter The Nexus";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = nexusBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp31Scene(scene, Galaxy4Ep31FracturedCrewScenePath, Galaxy4Ep31TemporalNexusScenePath);

            Debug.Log($"[Space Samurai] EP31 Fractured Crew scene built at {Galaxy4Ep31FracturedCrewScenePath}. " +
                      "Fractured corridor (cool palette, cooler temporal + steel blue accents, temporal flicker platforms). " +
                      "3 CorruptedScientist enemies (dark Dominion tint). " +
                      "4 steps: lyssa_threat (auto, rogue AI escalation) → defeat 3 scientists → heris_orphanage (Soren's refusal memory) → ENTER THE NEXUS.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 Temporal Nexus", priority = 320)]
        public static void BuildEp31TemporalNexus()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Temporal Nexus: cold inverted-light palette with temporal cyan accents, the loop's origin.
            var playerHealth = BuildEp31OnFootShell(refs, weapon,
                keyLight: new Color(0.58f, 0.62f, 0.68f),     // cool pale cyan-leaning
                ambient: new Color(0.08f, 0.10f, 0.13f),      // very dark cool
                fogColor: new Color(0.24f, 0.28f, 0.32f), fogDensity: 0.012f,
                structureName: "TemporalNexus",
                accent1: new Color(0.30f, 0.62f, 0.68f),      // bright temporal cyan
                accent2: new Color(0.40f, 0.60f, 0.70f),      // cyan-blue
                floorLight: new Color(0.54f, 0.58f, 0.64f), floorDark: new Color(0.24f, 0.27f, 0.32f),
                propTint: new Color(0.42f, 0.48f, 0.54f), out _);

            BuildEp31TimeLoop("TimeLoop", new Vector3(0f, 0f, 10f), Ep31TemporalCyan, Ep31RustOchre);

            // ---- Lyssa Vale dialogue (playOnStart) ----
            var lyssaValeDialogue = BuildEp31DialoguePlayer("Dialogue_LyssaVale", new Vector3(0f, 1.5f, 2f), "lyssa_vale");
            var lvSo = new SerializedObject(lyssaValeDialogue);
            lvSo.FindProperty("playOnStart").boolValue = true;
            lvSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Vale core dialogue ----
            var valeCoreDialogue = BuildEp31DialoguePlayer("Dialogue_ValeCore", new Vector3(0f, 1.5f, 12f), "vale_core");

            // ---- Nexus resolve dialogue ----
            var nexusResolveDialogue = BuildEp31DialoguePlayer("Dialogue_NexusResolve", new Vector3(0f, 1.5f, 15f), "nexus_resolve");

            // ---- Wave 1: QuantumPhantom enemies (bluish tint, 3) ----
            var phantomPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 9f),
                new Vector3(0f, 0f, 7f),
            };
            var phantomHealths = new List<Health>();
            foreach (var pos in phantomPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.40f, 0.55f)); // bluish steel
                enemy.gameObject.SetActive(false);
                phantomHealths.Add(enemy.GetComponent<Health>());
            }

            // ---- Wave 2: EchoSwarm enemies (bluish tint, 4) ----
            var swarmPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 10f),
                new Vector3(-0.5f, 0f, 13f),
                new Vector3(0.5f, 0f, 12f),
            };
            var swarmHealths = new List<Health>();
            foreach (var pos in swarmPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.40f, 0.55f)); // bluish steel
                enemy.gameObject.SetActive(false);
                swarmHealths.Add(enemy.GetComponent<Health>());
            }

            var nexusWaveSpawner = BuildWaveSpawner("NexusWaveSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { phantomHealths, swarmHealths },
                new DialoguePlayer[0]);

            // ---- Transition box: "IGNITE — TO THE POD" ----
            var podBoxGo = BuildTransitionBox("ToPodBox", new Vector3(0f, 1.2f, 21.5f), "IGNITE — TO THE POD",
                out var podBtn, out var podTransition);
            var pbSo = new SerializedObject(podTransition);
            pbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep31OneDaySceneName;
            pbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(podBtn.onClick,
                new UnityEngine.Events.UnityAction(podTransition.LoadOnFootScene));
            podBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Lyssa Vale (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lyssaValeDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Vale Core";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = valeCoreDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: QuantumPhantom Wave 1 (3) + EchoSwarm Wave 2 (4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = nexusWaveSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Nexus Resolve";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = nexusResolveDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Ignite — To The Pod";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = podBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp31Scene(scene, Galaxy4Ep31TemporalNexusScenePath, Galaxy4Ep31OneDayScenePath);

            Debug.Log($"[Space Samurai] EP31 Temporal Nexus scene built at {Galaxy4Ep31TemporalNexusScenePath}. " +
                      "Temporal nexus origin (cold inverted-light palette, bright temporal cyan + cyan-blue accents, temporal flicker platforms). " +
                      "Wave 1: 3 QuantumPhantom enemies (bluish steel). Wave 2: 4 EchoSwarm enemies (bluish steel). " +
                      "5 steps: lyssa_vale (auto, Dr. Marcus Renn reveal) → vale_core (Renn dialogue, identity vs erasure) → defeat 2 waves → nexus_resolve (time-lock breaking + Soren wholeness) → IGNITE — TO THE POD.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP31 Scenes", priority = 321)]
        public static void BuildAllEp31Scenes()
        {
            BuildEp31ArmedLayer();
            BuildEp31RepeatingPattern();
            BuildEp31ArchitectsEcho();
            BuildEp31FracturedCrew();
            BuildEp31TemporalNexus();
            BuildEp31OneDay();   // defined in Ep31BuilderFinale.cs
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP31 scenes built + inputs rewired. GALAXY 4 EP31 COMPLETE.");
        }
    }
}

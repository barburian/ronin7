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
    /// EP28 "The Harvest Moon Festival" (Galaxy 4) scene builders for the first four on-foot scenes
    /// on Aethon-9. The agrarian planet's Harvest Moon Festival conceals a Dominion child-extraction
    /// facility identical to the Kethel-7 massacre that broke Cipher. Festival administrator Maya Selene
    /// guides him from the festival landing pad down through the sealed facility, where the manifests
    /// reveal the harvested children and Cipher reads his own CIPHER-7 file.
    /// - Luminous Descent: festival-palette garden terraces with Dominion scan agents.
    /// - Garden Below: brighter bioluminescent sanctuary with surveillance drones + DreamPhantom.
    /// - Extraction Team: cold facility descent with Maya ally + 8 Dominion soldiers in 2 waves.
    /// - Underground Archive: dark archive vault with 6 containment drones.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 7 scenes declared here; scenes 5-7 built in Ep28BuilderFinale.cs.
        private const string Galaxy4Ep28LuminousDescentScenePath      = SceneFolder + "/Galaxy4_EP28_LuminousDescent.unity";
        private const string Galaxy4Ep28GardenBelowScenePath          = SceneFolder + "/Galaxy4_EP28_GardenBelow.unity";
        private const string Galaxy4Ep28ExtractionTeamScenePath       = SceneFolder + "/Galaxy4_EP28_ExtractionTeam.unity";
        private const string Galaxy4Ep28UndergroundArchiveScenePath   = SceneFolder + "/Galaxy4_EP28_UndergroundArchive.unity";
        private const string Galaxy4Ep28HarvestCeremonyScenePath      = SceneFolder + "/Galaxy4_EP28_HarvestCeremony.unity";
        private const string Galaxy4Ep28BreakingPointScenePath        = SceneFolder + "/Galaxy4_EP28_BreakingPoint.unity";
        private const string Galaxy4Ep28MoonRisesScenePath            = SceneFolder + "/Galaxy4_EP28_MoonRises.unity";

        private static readonly string Galaxy4Ep28LuminousDescentSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28LuminousDescentScenePath);
        private static readonly string Galaxy4Ep28GardenBelowSceneName          = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28GardenBelowScenePath);
        private static readonly string Galaxy4Ep28ExtractionTeamSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28ExtractionTeamScenePath);
        private static readonly string Galaxy4Ep28UndergroundArchiveSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28UndergroundArchiveScenePath);
        private static readonly string Galaxy4Ep28HarvestCeremonySceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28HarvestCeremonyScenePath);
        private static readonly string Galaxy4Ep28BreakingPointSceneName        = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28BreakingPointScenePath);
        private static readonly string Galaxy4Ep28MoonRisesSceneName            = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep28MoonRisesScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP28 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep28" and loads lines from Ep28Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp28DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep28Lines.Get(setId), advanceRef, setId, clipPrefix: "ep28");
        }

        /// <summary>Standard EP28 on-foot scene scaffold shared by scenes 1-6: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp28OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        /// add an AllyCombatant.</summary>
        private static GameObject BuildEp28Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp28Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Festival tint: warm jade/amber for bioluminescent garden
        private static readonly Color Ep28FestivalJade = new Color(0.45f, 0.85f, 0.60f);
        // Festival tint: warm amber accent
        private static readonly Color Ep28FestivalAmber = new Color(0.95f, 0.75f, 0.35f);
        // Dark Dominion tint for scan agents
        private static readonly Color Ep28DarkDominion = new Color(0.30f, 0.32f, 0.40f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Luminous Descent", priority = 294)]
        public static void BuildEp28LuminousDescent()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Luminous Descent: warm bioluminescent festival palette, bright ambient.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.80f, 0.78f, 0.72f),     // warm pale
                ambient: new Color(0.20f, 0.18f, 0.14f),      // brighter visible
                fogColor: new Color(0.28f, 0.26f, 0.20f), fogDensity: 0.010f,
                structureName: "LuminousTerraces",
                accent1: new Color(0.45f, 0.85f, 0.60f),      // jade bioluminescent
                accent2: new Color(0.95f, 0.75f, 0.35f),      // amber warm
                floorLight: new Color(0.50f, 0.48f, 0.44f), floorDark: new Color(0.28f, 0.26f, 0.22f),
                propTint: new Color(0.44f, 0.42f, 0.38f), out _);

            // ---- Dominion scan agents (lethal, dark tint) in 1 wave ----
            var scanAgentPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(0f, 0f, 10f),
            };
            var scanAgentHealths = new List<Health>();
            foreach (var pos in scanAgentPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep28DarkDominion);
                enemy.gameObject.SetActive(false);
                scanAgentHealths.Add(enemy.GetComponent<Health>());
            }

            var scanAgentSpawner = BuildWaveSpawner("ScanAgentSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { scanAgentHealths },
                new[] { BuildEp28DialoguePlayer("Dialogue_ScanAgents", new Vector3(0f, 1.5f, 8f), "scan_agents") });

            // ---- Maya Selene NPC (warm practical tint) ----
            var mayaTint = new Color(0.70f, 0.65f, 0.58f); // warm practical
            var mayaGo = BuildEp28Npc("Maya Selene", new Vector3(0f, 0f, 14f), mayaTint);

            // ---- Dialogue Players ----
            var mayaIntroDialogue = BuildEp28DialoguePlayer("Dialogue_MayaIntro", new Vector3(0f, 1.5f, 2f), "maya_intro");

            // Transition box: "INTO THE GARDEN".
            var gardenBoxGo = BuildTransitionBox("ToGardenBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE GARDEN",
                out var gardenBtn, out var gardenTransition);
            var gbSo = new SerializedObject(gardenTransition);
            gbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28GardenBelowSceneName;
            gbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gardenBtn.onClick,
                new UnityEngine.Events.UnityAction(gardenTransition.LoadOnFootScene));
            gardenBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Scan Agents (3, scan_agents)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = scanAgentSpawner;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Maya Intro";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = mayaIntroDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Into the Garden";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = gardenBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28LuminousDescentScenePath, Galaxy4Ep28GardenBelowScenePath);

            Debug.Log($"[Space Samurai] EP28 Luminous Descent scene built at {Galaxy4Ep28LuminousDescentScenePath}. " +
                      "Festival-palette bioluminescent garden (warm jade/amber, bright ambient). " +
                      "3 Dominion scan agents (dark Dominion, lethal, scan_agents spawn bark). Maya Selene NPC (warm practical). " +
                      "3 steps: defeat 3 scan agents → maya_intro dialogue → INTO THE GARDEN.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Garden Below", priority = 295)]
        public static void BuildEp28GardenBelow()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Garden Below: brighter bioluminescent sanctuary with terraces.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.82f, 0.80f, 0.76f),     // warm bright
                ambient: new Color(0.22f, 0.20f, 0.16f),      // visible
                fogColor: new Color(0.30f, 0.28f, 0.24f), fogDensity: 0.008f,
                structureName: "GardenBelowTerraces",
                accent1: new Color(0.45f, 0.85f, 0.60f),      // jade bioluminescent
                accent2: new Color(0.60f, 0.90f, 0.75f),      // indigo-jade accent
                floorLight: new Color(0.52f, 0.50f, 0.46f), floorDark: new Color(0.30f, 0.28f, 0.24f),
                propTint: new Color(0.46f, 0.44f, 0.40f), out _);

            // ---- Surveillance drones with DreamPhantom (memory-flash killable) ----
            var dronePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(-0.5f, 0f, 10f),
                new Vector3(0.5f, 0f, 10.5f),
            };
            var droneHealths = new List<Health>();
            foreach (var pos in dronePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep28DarkDominion);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                // Add DreamPhantom component (killable memory-flash)
                enemy.gameObject.AddComponent<DreamPhantom>();
                droneHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneHealths },
                new DialoguePlayer[0]); // No spawn dialogue for phantoms

            // ---- Dialogue Players ----
            var gardenWalkDialogue = BuildEp28DialoguePlayer("Dialogue_GardenWalk", new Vector3(0f, 1.5f, 2f), "garden_walk");
            var gwSo = new SerializedObject(gardenWalkDialogue);
            gwSo.FindProperty("playOnStart").boolValue = true;
            gwSo.ApplyModifiedPropertiesWithoutUndo();

            var manifestDialogue = BuildEp28DialoguePlayer("Dialogue_Manifest", new Vector3(0f, 1.5f, 12f), "manifest_room");

            var kesslerConfessionDialogue = BuildEp28DialoguePlayer("Dialogue_KesslerConfession", new Vector3(0f, 1.5f, 14f), "kessler_confession");

            // Transition box: "DEEPER — THE FACILITY".
            var facilityBoxGo = BuildTransitionBox("ToFacilityBox", new Vector3(0f, 1.2f, 21.5f), "DEEPER — THE FACILITY",
                out var facilityBtn, out var facilityTransition);
            var fbSo = new SerializedObject(facilityTransition);
            fbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28ExtractionTeamSceneName;
            fbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(facilityBtn.onClick,
                new UnityEngine.Events.UnityAction(facilityTransition.LoadOnFootScene));
            facilityBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Garden Walk";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = gardenWalkDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Surveillance Drones (4, DreamPhantom, nonLethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Manifest + Kessler Confession";
            var manifestDialogueProp = s2.FindPropertyRelative("dialogue");
            manifestDialogueProp.objectReferenceValue = manifestDialogue;
            // Add kessler_confession as a follow-up step
            stepsProp.arraySize = 5;
            var s2b = stepsProp.GetArrayElementAtIndex(3);
            s2b.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2b.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Confession";
            s2b.FindPropertyRelative("dialogue").objectReferenceValue = kesslerConfessionDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(4);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Deeper — The Facility";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = facilityBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28GardenBelowScenePath, Galaxy4Ep28ExtractionTeamScenePath);

            Debug.Log($"[Space Samurai] EP28 Garden Below scene built at {Galaxy4Ep28GardenBelowScenePath}. " +
                      "Bioluminescent sanctuary with brighter palette (jade + indigo accents). " +
                      "4 surveillance drones (dark Dominion, DreamPhantom, nonLethal, killable memory-flash). " +
                      "5 steps: garden_walk (auto) → defeat 4 drones → manifest_room dialogue → kessler_confession → DEEPER — THE FACILITY.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Extraction Team", priority = 296)]
        public static void BuildEp28ExtractionTeam()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Extraction Team: colder facility palette, descent into security zone.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.70f, 0.68f, 0.72f),     // cool facility
                ambient: new Color(0.16f, 0.14f, 0.18f),      // cool dim
                fogColor: new Color(0.28f, 0.26f, 0.32f), fogDensity: 0.012f,
                structureName: "FacilityDescent",
                accent1: new Color(0.50f, 0.70f, 0.75f),      // cool teal
                accent2: new Color(0.65f, 0.70f, 0.80f),      // cool indigo
                floorLight: new Color(0.48f, 0.46f, 0.50f), floorDark: new Color(0.26f, 0.24f, 0.28f),
                propTint: new Color(0.42f, 0.40f, 0.44f), out _);

            // ---- Maya Selene NPC with AllyCombatant ----
            var mayaGo = BuildEp28Npc("Maya Selene", new Vector3(-1.5f, 0f, 4f), new Color(0.70f, 0.65f, 0.58f));
            if (mayaGo != null)
            {
                var allyCombatant = mayaGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dominion soldiers (lethal, dark tint) in 2 waves of 4 ----
            var waveAPositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 9f),
                new Vector3(0.5f, 0f, 9.5f),
                new Vector3(-1f, 0f, 11f),
                new Vector3(1f, 0f, 11.5f),
            };
            var waveA = new List<Health>();
            foreach (var pos in waveAPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep28DarkDominion);
                enemy.gameObject.SetActive(false);
                waveA.Add(enemy.GetComponent<Health>());
            }

            var waveBPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 13f),
                new Vector3(1.5f, 0f, 13.5f),
                new Vector3(-0.5f, 0f, 14.5f),
                new Vector3(0.5f, 0f, 15f),
            };
            var waveB = new List<Health>();
            foreach (var pos in waveBPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep28DarkDominion);
                enemy.gameObject.SetActive(false);
                waveB.Add(enemy.GetComponent<Health>());
            }

            var soldierSpawner = BuildWaveSpawner("SoldierSpawner", new Vector3(0f, 0.5f, 11f), 2f,
                new List<List<Health>> { waveA, waveB },
                new[] { BuildEp28DialoguePlayer("Dialogue_ExtractionCombat", new Vector3(0f, 1.5f, 11f), "extraction_combat") });

            // ---- Dialogue Players ----
            var kethelConfessionDialogue = BuildEp28DialoguePlayer("Dialogue_KethelConfession", new Vector3(0f, 1.5f, 16f), "kethel_confession");

            // Transition box: "THE ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 21.5f), "THE ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var abSo = new SerializedObject(archiveTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28UndergroundArchiveSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Soldiers (8, extraction_combat)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = soldierSpawner;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Kethel Confession";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = kethelConfessionDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: The Archive";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28ExtractionTeamScenePath, Galaxy4Ep28UndergroundArchiveScenePath);

            Debug.Log($"[Space Samurai] EP28 Extraction Team scene built at {Galaxy4Ep28ExtractionTeamScenePath}. " +
                      "Cold facility palette (cool teal/indigo accents). " +
                      "Maya Selene NPC (warm practical, AllyCombatant ally). 8 Dominion soldiers (dark Dominion, lethal) in 2 waves of 4. " +
                      "3 steps: defeat 8 soldiers (extraction_combat) → kethel_confession dialogue → THE ARCHIVE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Underground Archive", priority = 297)]
        public static void BuildEp28UndergroundArchive()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Underground Archive: dark vault palette, coldest descent yet.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.65f, 0.63f, 0.68f),     // pale cool
                ambient: new Color(0.12f, 0.10f, 0.14f),      // dark cool
                fogColor: new Color(0.24f, 0.22f, 0.28f), fogDensity: 0.016f,
                structureName: "ArchiveVault",
                accent1: new Color(0.55f, 0.70f, 0.80f),      // cool teal/blue
                accent2: new Color(0.70f, 0.72f, 0.85f),      // pale cool accent
                floorLight: new Color(0.50f, 0.48f, 0.52f), floorDark: new Color(0.26f, 0.24f, 0.28f),
                propTint: new Color(0.44f, 0.42f, 0.46f), out _);

            // ---- Containment drones (lethal, dark tint) ----
            var dronePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(0.5f, 0f, 11.5f),
                new Vector3(1.5f, 0f, 12f),
            };
            var droneHealths = new List<Health>();
            foreach (var pos in dronePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep28DarkDominion);
                enemy.gameObject.SetActive(false);
                droneHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("ContainmentSpawner", new Vector3(0f, 0.5f, 9.5f), 2f,
                new List<List<Health>> { droneHealths },
                new DialoguePlayer[0]); // No spawn dialogue

            // ---- Dialogue Players ----
            var archiveFileDialogue = BuildEp28DialoguePlayer("Dialogue_ArchiveFile", new Vector3(0f, 1.5f, 2f), "archive_file");
            var afSo = new SerializedObject(archiveFileDialogue);
            afSo.FindProperty("playOnStart").boolValue = true;
            afSo.ApplyModifiedPropertiesWithoutUndo();

            var failsafeExplainDialogue = BuildEp28DialoguePlayer("Dialogue_FailsafeExplain", new Vector3(0f, 1.5f, 14f), "failsafe_explain");

            // Transition box: "THE CEREMONY".
            var ceremonyBoxGo = BuildTransitionBox("ToCeremonyBox", new Vector3(0f, 1.2f, 21.5f), "THE CEREMONY",
                out var ceremonyBtn, out var ceremonyTransition);
            var cbSo = new SerializedObject(ceremonyTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28HarvestCeremonySceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(ceremonyBtn.onClick,
                new UnityEngine.Events.UnityAction(ceremonyTransition.LoadOnFootScene));
            ceremonyBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archive File";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveFileDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Containment Drones (6, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Failsafe Explain";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = failsafeExplainDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: The Ceremony";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = ceremonyBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28UndergroundArchiveScenePath, Galaxy4Ep28HarvestCeremonyScenePath);

            Debug.Log($"[Space Samurai] EP28 Underground Archive scene built at {Galaxy4Ep28UndergroundArchiveScenePath}. " +
                      "Dark archive vault (cool teal/blue + pale cool accents). " +
                      "6 containment drones (dark Dominion, lethal). " +
                      "4 steps: archive_file (auto, failsafe discovery) → defeat 6 drones → failsafe_explain dialogue → THE CEREMONY.");
        }
    }
}

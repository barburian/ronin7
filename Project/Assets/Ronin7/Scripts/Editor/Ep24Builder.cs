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
    /// EP24 "The Fracture Protocol" (Galaxy 3 FINALE) scene builders for the first three on-foot scenes
    /// on Meridian-7, a failing hive-consensus clone colony where every enemy wears Cipher's own face.
    /// - How Many: docking bay, clone port-authority breach (HiveCascadeController)
    /// - Retirement Colony: cracking streets to the beacon tower, Seven-Prime intro, Cache appears
    /// - Thirty Years: subterranean archive vault, trained clones (HiveCascade + EchoHunter), SERIES 1 // CIPHER reveal
    ///
    /// Clone combat uses the EP24 signature mechanic <see cref="HiveCascadeController"/>: the clones drift between
    /// attacking, freezing, and turning inward as their hive-consensus collapses. Lives in the same
    /// <see cref="XRRigBuilder"/> partial class so it can call all private static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP24 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep24" and loads lines from Ep24Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp24DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep24Lines.Get(setId), advanceRef, setId, clipPrefix: "ep24");
        }

        /// <summary>Builds a wave of identical clone enemies (Cipher's face) and attaches a HiveCascadeController
        /// that drives them through consensus collapse. Optionally adds an EchoHunter to each (trained-on-player).
        /// Returns the wave's Health list for the wave spawner.</summary>
        private static List<Health> BuildEp24CloneWave(Vector3[] positions, Health playerHealth,
            EnemyDefinition enemyDef, Color cloneTint, bool trained)
        {
            var waveHealths = new List<Health>();
            var meleeAttackers = new List<MeleeAttacker>();
            foreach (var pos in positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, cloneTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                    meleeAttackers.Add(meleeAttacker);
                }
                // Trained vault clones counter Cipher's own instincts (EP14 EchoHunter: repetition is punished).
                if (trained) enemy.gameObject.AddComponent<EchoHunter>();
                enemy.gameObject.SetActive(false);
                waveHealths.Add(enemy.GetComponent<Health>());
            }

            // Hive-consensus collapse: clones fracture between attacking, freezing, and turning inward.
            var hiveGo = new GameObject("HiveCascade");
            var hive = hiveGo.AddComponent<HiveCascadeController>();
            var hiveSo = new SerializedObject(hive);
            var membersProp = hiveSo.FindProperty("members");
            membersProp.arraySize = meleeAttackers.Count;
            for (int i = 0; i < meleeAttackers.Count; i++)
                membersProp.GetArrayElementAtIndex(i).objectReferenceValue = meleeAttackers[i];
            hiveSo.ApplyModifiedPropertiesWithoutUndo();

            return waveHealths;
        }

        /// <summary>Standard EP24 on-foot scene scaffold shared by scenes 1-4: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp24OnFootShell(Object[] refs, WeaponDefinition weapon,
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

            // Identical hive-uniform tower/panel props.
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

        /// <summary>Adds a non-remote StoryNpc capsule (used for Seven-Prime / Cache). Returns the GameObject so
        /// callers can optionally add an AllyCombatant.</summary>
        private static GameObject BuildEp24Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp24Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // The clone tint is close to the player's own grey palette — every enemy is Cipher's face.
        private static readonly Color Ep24CloneTint = new Color(0.52f, 0.54f, 0.58f);

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 How Many", priority = 260)]
        public static void BuildEp24HowMany()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // How Many: cold precise hive docking bay, steel-white light fracturing into magenta.
            var playerHealth = BuildEp24OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.66f, 0.72f),
                ambient: new Color(0.14f, 0.15f, 0.18f),
                fogColor: new Color(0.20f, 0.22f, 0.27f), fogDensity: 0.016f,
                structureName: "DockingBay",
                accent1: new Color(0.55f, 0.80f, 0.95f),   // ordered cyan
                accent2: new Color(0.85f, 0.45f, 0.80f),   // glitch magenta (synchrony breaking)
                floorLight: new Color(0.48f, 0.50f, 0.54f), floorDark: new Color(0.26f, 0.28f, 0.32f),
                propTint: new Color(0.50f, 0.52f, 0.56f), out _);

            // ---- Clone port authority (HiveCascade) ----
            var clonePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(0f, 0f, 10f),
                new Vector3(-1f, 0f, 12f),
            };
            var cloneHealths = BuildEp24CloneWave(clonePositions, playerHealth, enemyDef, Ep24CloneTint, trained: false);

            var cloneSpawner = BuildWaveSpawner("CloneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { cloneHealths },
                new[] { BuildEp24DialoguePlayer("Dialogue_DockingBarks", new Vector3(0f, 1.5f, 8f), "docking_barks") });

            // ---- Dialogue Players ----
            var howManyDialogue = BuildEp24DialoguePlayer("Dialogue_HowMany", new Vector3(0f, 1.5f, 2f), "how_many");
            var hmSo = new SerializedObject(howManyDialogue);
            hmSo.FindProperty("playOnStart").boolValue = true;
            hmSo.ApplyModifiedPropertiesWithoutUndo();

            var dyingCloneDialogue = BuildEp24DialoguePlayer("Dialogue_DyingClone", new Vector3(0f, 1.5f, 13f), "dying_clone");

            // Transition box: "INTO THE STREETS".
            var streetsBoxGo = BuildTransitionBox("IntoStreetsBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE STREETS",
                out var streetsBtn, out var streetsTransition);
            var stSo = new SerializedObject(streetsTransition);
            stSo.FindProperty("onFootScene").stringValue = Galaxy3Ep24RetirementColonySceneName;
            stSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(streetsBtn.onClick,
                new UnityEngine.Events.UnityAction(streetsTransition.LoadOnFootScene));
            streetsBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: How Many";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = howManyDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Clone Port Authority (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cloneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Dying Clone";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = dyingCloneDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Into the Streets";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = streetsBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp24Scene(scene, Galaxy3Ep24HowManyScenePath, Galaxy3Ep24RetirementColonyScenePath);

            Debug.Log($"[Space Samurai] EP24 How Many scene built at {Galaxy3Ep24HowManyScenePath}. " +
                      "Hive docking bay, steel-white light fracturing to magenta, identical tower props. " +
                      "4 clone port-authority (Cipher's grey, nonLethal) under HiveCascadeController. " +
                      "4 steps: how_many (auto) → defeat 4 clones (docking_barks) → dying_clone dialogue → into the streets.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 Retirement Colony", priority = 261)]
        public static void BuildEp24RetirementColony()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Retirement Colony: cracking administrative streets, neutral steel with collapse-red accents.
            var playerHealth = BuildEp24OnFootShell(refs, weapon,
                keyLight: new Color(0.58f, 0.58f, 0.60f),
                ambient: new Color(0.13f, 0.13f, 0.14f),
                fogColor: new Color(0.22f, 0.20f, 0.20f), fogDensity: 0.017f,
                structureName: "AdminStreets",
                accent1: new Color(0.85f, 0.88f, 0.92f),   // pale hive-white
                accent2: new Color(1f, 0.42f, 0.34f),      // collapse red
                floorLight: new Color(0.46f, 0.47f, 0.49f), floorDark: new Color(0.26f, 0.27f, 0.29f),
                propTint: new Color(0.48f, 0.49f, 0.52f), out _);

            // ---- Seven-Prime (terrified, no ally yet) ----
            BuildEp24Npc("Seven-Prime", new Vector3(-1.2f, 0f, 3f), new Color(0.55f, 0.5f, 0.62f));

            // ---- Clones in chaos (HiveCascade) ----
            var clonePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
            };
            var cloneHealths = BuildEp24CloneWave(clonePositions, playerHealth, enemyDef, Ep24CloneTint, trained: false);

            var cloneSpawner = BuildWaveSpawner("CloneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { cloneHealths },
                new[] { BuildEp24DialoguePlayer("Dialogue_ColonyBarks", new Vector3(0f, 1.5f, 8f), "colony_barks") });

            // ---- Cache (appears at the tower control room) ----
            BuildEp24Npc("Cache", new Vector3(1.2f, 0f, 18f), new Color(0.5f, 0.55f, 0.5f));

            // ---- Dialogue Players ----
            var colonyDialogue = BuildEp24DialoguePlayer("Dialogue_RetirementColony", new Vector3(0f, 1.5f, 2f), "retirement_colony");
            var rcSo = new SerializedObject(colonyDialogue);
            rcSo.FindProperty("playOnStart").boolValue = true;
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            var cacheDialogue = BuildEp24DialoguePlayer("Dialogue_CacheIntro", new Vector3(0f, 1.5f, 16f), "cache_intro");

            // Transition box: "TO THE ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 21.5f), "TO THE ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var abSo = new SerializedObject(archiveTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy3Ep24ThirtyYearsSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Retirement Colony";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = colonyDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Fracturing Clones (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cloneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Cache Intro";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = cacheDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Archive";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp24Scene(scene, Galaxy3Ep24RetirementColonyScenePath, Galaxy3Ep24ThirtyYearsScenePath);

            Debug.Log($"[Space Samurai] EP24 Retirement Colony scene built at {Galaxy3Ep24RetirementColonyScenePath}. " +
                      "Cracking admin streets, collapse-red accents. Seven-Prime (terrified, no ally) + Cache at the tower. " +
                      "5 fracturing clones under HiveCascadeController. " +
                      "4 steps: retirement_colony (auto) → defeat 5 clones (colony_barks) → cache_intro dialogue → to the archive.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 Thirty Years", priority = 262)]
        public static void BuildEp24ThirtyYears()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Thirty Years: subterranean archive vault, disorienting cyan/violet holographic light.
            var playerHealth = BuildEp24OnFootShell(refs, weapon,
                keyLight: new Color(0.45f, 0.55f, 0.78f),
                ambient: new Color(0.12f, 0.13f, 0.20f),
                fogColor: new Color(0.16f, 0.18f, 0.30f), fogDensity: 0.020f,
                structureName: "ArchiveVault",
                accent1: new Color(0.45f, 0.85f, 1f),      // holographic cyan
                accent2: new Color(0.65f, 0.45f, 0.95f),   // record-bank violet
                floorLight: new Color(0.40f, 0.45f, 0.58f), floorDark: new Color(0.22f, 0.26f, 0.36f),
                propTint: new Color(0.45f, 0.52f, 0.70f), out _);

            // ---- Cache (delivering the SERIES 1 // CIPHER reveal) ----
            BuildEp24Npc("Cache", new Vector3(-1.2f, 0f, 3f), new Color(0.5f, 0.55f, 0.5f));

            // ---- Trained clones: HiveCascade + EchoHunter (every counter mirrors his instincts) ----
            var clonePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(2f, 0f, 10f),
                new Vector3(-1.5f, 0f, 11.5f),
                new Vector3(1.5f, 0f, 12.5f),
            };
            var cloneHealths = BuildEp24CloneWave(clonePositions, playerHealth, enemyDef, Ep24CloneTint, trained: true);

            var cloneSpawner = BuildWaveSpawner("CloneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { cloneHealths },
                new[] { BuildEp24DialoguePlayer("Dialogue_VaultBarks", new Vector3(0f, 1.5f, 8f), "vault_barks") });

            // ---- Dialogue Players ----
            var thirtyDialogue = BuildEp24DialoguePlayer("Dialogue_ThirtyYears", new Vector3(0f, 1.5f, 2f), "thirty_years");
            var tySo = new SerializedObject(thirtyDialogue);
            tySo.FindProperty("playOnStart").boolValue = true;
            tySo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "DEEPER — KHALL CALLS".
            var deeperBoxGo = BuildTransitionBox("DeeperBox", new Vector3(0f, 1.2f, 21.5f), "DEEPER — KHALL CALLS",
                out var deeperBtn, out var deeperTransition);
            var dbSo = new SerializedObject(deeperTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep24KhallsMercySceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(deeperBtn.onClick,
                new UnityEngine.Events.UnityAction(deeperTransition.LoadOnFootScene));
            deeperBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Thirty Years";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = thirtyDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Trained Clones (5, EchoHunter)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cloneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Deeper — Khall Calls";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = deeperBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp24Scene(scene, Galaxy3Ep24ThirtyYearsScenePath, Galaxy3Ep24KhallsMercyScenePath);

            Debug.Log($"[Space Samurai] EP24 Thirty Years scene built at {Galaxy3Ep24ThirtyYearsScenePath}. " +
                      "Archive vault, cyan/violet holographic light. Cache delivers SERIES 1 // CIPHER reveal. " +
                      "5 trained clones under HiveCascadeController + EchoHunter (repetition punished). " +
                      "3 steps: thirty_years (auto) → defeat 5 trained clones (vault_barks) → deeper, Khall calls.");
        }
    }
}

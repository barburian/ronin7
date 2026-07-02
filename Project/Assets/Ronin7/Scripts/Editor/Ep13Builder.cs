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
    /// EP13 "The Carnival of Forgotten Names" on-foot scene builders. Builds five core episodes:
    /// - Carousel: warm amber/red carnival hall with spinning carousel hazard and Memory Grinders
    /// - MirrorMaze: near-black holographic maze with MirrorPhantom sequence mechanic
    /// - CenterTent: dark vial-forest cathedral with Coral Vex and Extraction Guards
    /// - Vault: grey Verath bedrock archive with Coral Vex ally and Dominion Sentries
    /// - CoreFight: dark detonating core with Ronin-10 boss (DuelYield) and Coral Vex ally
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP13 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep13" and loads lines from Ep13Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp13DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep13Lines.Get(setId), advanceRef, setId, clipPrefix: "ep13");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Carousel", priority = 136)]
        public static void BuildEp13Carousel()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Carousel: warm amber/red carnival hall with spinning carousel of "painted horses".
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.55f, 0.35f); // warm amber key light
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.25f, 0.15f); // warm dim ambient

            // Warm amber-red carnival fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.50f, 0.30f, 0.15f);
            RenderSettings.fogDensity = 0.018f;

            // Two warm amber accent point lights.
            BuildAccentPointLight("CarouselLight1", new Vector3(-5f, 2.5f, 8f),
                new Color(1f, 0.6f, 0.3f), intensity: 1.1f, range: 12f);
            BuildAccentPointLight("CarouselLight2", new Vector3(5f, 2f, 14f),
                new Color(0.95f, 0.55f, 0.25f), intensity: 1.0f, range: 11f);

            // ---- Carnival floor and carnival set dressing ----
            var carouselGo = new GameObject("Carnival");
            var carousel = carouselGo.transform;
            var amberWood = new Color(0.55f, 0.40f, 0.25f);
            var darkWood = new Color(0.35f, 0.25f, 0.15f);

            // Main carnival floor.
            BuildFloorCeiling(carousel, "CarouselFloor", new Vector3(0f, 0f, 10f), new Vector3(20f, 0f, 20f), amberWood, darkWood);

            // ---- Carousel Hazard with OrbitPivot and decorative horses ----
            var carouselHazardGo = new GameObject("Carousel");
            carouselHazardGo.transform.SetParent(carousel, false);
            carouselHazardGo.transform.position = new Vector3(0f, 0f, 10f);

            var orbitPivot = new GameObject("OrbitPivot");
            orbitPivot.transform.SetParent(carouselHazardGo.transform, false);
            orbitPivot.transform.localPosition = Vector3.zero;

            // Build ~6 decorative painted horse props around the orbit pivot.
            var horseColor = new Color(0.70f, 0.50f, 0.40f); // painted wood tint
            var horseCount = 6;
            for (int i = 0; i < horseCount; i++)
            {
                float angle = (i / (float)horseCount) * 360f;
                float x = Mathf.Cos(angle * Mathf.Deg2Rad) * 3f;
                float z = Mathf.Sin(angle * Mathf.Deg2Rad) * 3f;
                BuildProp(orbitPivot.transform, $"Horse_{i}", new Vector3(x, 0.5f, z), new Vector3(0.4f, 0.8f, 0.5f), horseColor);
            }

            // Add CarouselHazard component and wire orbitPivot.
            var hazardComponent = carouselHazardGo.AddComponent<CarouselHazard>();
            var hazardSo = new SerializedObject(hazardComponent);
            SetObjectRef(hazardSo, "orbitPivot", orbitPivot.transform);
            hazardSo.ApplyModifiedPropertiesWithoutUndo();

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds + standard locomotion.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Dialogue Players ----
            var transitIntroDialogue = BuildEp13DialoguePlayer("Dialogue_TransitIntro", new Vector3(0f, 1.5f, 2f), "transit_intro");
            var transitDlgSo = new SerializedObject(transitIntroDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var lyrisGateDialogue = BuildEp13DialoguePlayer("Dialogue_LyrisGate", new Vector3(0f, 1.5f, 4f), "lyris_gate");
            var memoryStolenDialogue = BuildEp13DialoguePlayer("Dialogue_MemoryStolen", new Vector3(0f, 1.5f, 12f), "memory_stolen");

            // ---- 6 Memory Grinder enemies: 1 wave ----
            var grinderColor = new Color(0.85f, 0.85f, 0.88f); // porcelain-white
            var grinderWavePositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 6f),
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(0f, 0f, 6f),
                new Vector3(1.5f, 0f, 6f),
                new Vector3(3f, 0f, 6f),
                new Vector3(0f, 0f, 7.5f)
            };

            var grinderWaveHealths = new List<Health>();
            foreach (var pos in grinderWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, grinderColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                grinderWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var grinderSpawner = BuildEp03WaveSpawner("GrinderSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { grinderWaveHealths },
                new[] { BuildEp13DialoguePlayer("Dialogue_CarouselBarks", new Vector3(0f, 1.5f, 8f), "carousel_barks") });

            // Transition box: "ENTER — THE MIRROR MAZE".
            var mazBoxGo = BuildTransitionBox("ToMazeBox", new Vector3(0f, 1.2f, 20.5f), "ENTER — THE MIRROR MAZE",
                out var mazeBtn, out var mazeTransition);
            var mbSo = new SerializedObject(mazeTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep13MirrorMazeSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(mazeBtn.onClick,
                new UnityEngine.Events.UnityAction(mazeTransition.LoadOnFootScene));
            mazBoxGo.SetActive(false);

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

            // Step 1: Dialogue lyris_gate.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Lyris Gate";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = lyrisGateDialogue;

            // Step 2: DefeatWaves — 6 Memory Grinders.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Memory Grinders (6)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = grinderSpawner;

            // Step 3: Dialogue memory_stolen.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Memory Stolen";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = memoryStolenDialogue;

            // Step 4: Prompt — transition to Mirror Maze.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Enter Mirror Maze";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = mazBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13CarouselScenePath);
            EnsureScenesInBuild(Galaxy2Ep13CarouselScenePath, Galaxy2Ep13MirrorMazeScenePath);

            Debug.Log($"[Space Samurai] EP13 Carousel scene built at {Galaxy2Ep13CarouselScenePath}. " +
                      "Layout: warm amber/red carnival hall with spinning carousel (CarouselHazard, orbitPivot + 6 horse decor). " +
                      "6 Memory Grinder enemies (porcelain-white, nonLethal). " +
                      "5 steps: transit_intro (auto) → lyris_gate dialogue → defeat 6 Memory Grinders (carousel_barks bark) → " +
                      "memory_stolen dialogue → transition to Mirror Maze.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Mirror Maze", priority = 137)]
        public static void BuildEp13MirrorMaze()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Mirror Maze: near-black holographic maze with cold blue accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.35f, 0.50f, 0.65f); // cold blue key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.22f); // near-black ambient

            // Deep near-black maze fog with blue tint.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.08f, 0.12f, 0.18f);
            RenderSettings.fogDensity = 0.035f;

            // Two cold blue accent lights.
            BuildAccentPointLight("MazeLight1", new Vector3(-4f, 2f, 6f),
                new Color(0.3f, 0.6f, 0.85f), intensity: 0.8f, range: 10f);
            BuildAccentPointLight("MazeLight2", new Vector3(4f, 2.5f, 12f),
                new Color(0.25f, 0.55f, 0.80f), intensity: 0.75f, range: 9f);

            // ---- Maze floor and walls ----
            var mazeGo = new GameObject("Maze");
            var maze = mazeGo.transform;
            var darkMirror = new Color(0.15f, 0.15f, 0.18f);
            var nearBlack = new Color(0.08f, 0.08f, 0.10f);

            // Main maze floor.
            BuildFloorCeiling(maze, "MazeFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), darkMirror, nearBlack);

            // Maze walls (holographic glass effect via tint).
            BuildWall(maze, "MazeWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(maze, "MazeWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

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
            var yaskWarningDialogue = BuildEp13DialoguePlayer("Dialogue_YaskWarning", new Vector3(0f, 1.5f, 2f), "yask_warning");
            var yaskDlgSo = new SerializedObject(yaskWarningDialogue);
            yaskDlgSo.FindProperty("playOnStart").boolValue = true;
            yaskDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var mirrorMazeDialogue = BuildEp13DialoguePlayer("Dialogue_MirrorMaze", new Vector3(0f, 1.5f, 8f), "mirror_maze");
            var mirrorAfterDialogue = BuildEp13DialoguePlayer("Dialogue_MirrorAfter", new Vector3(0f, 1.5f, 14f), "mirror_after");

            // ---- MirrorPhantom: 7 phantom enemies in a sequence ----
            var phantomsContainer = new GameObject("Phantoms");
            phantomsContainer.transform.SetParent(maze, false);
            phantomsContainer.transform.position = new Vector3(0f, 0f, 10f);

            var phantomColor = new Color(0.55f, 0.62f, 0.78f); // ghost-blue
            var phantomHealths = new List<Health>();
            for (int i = 0; i < 7; i++)
            {
                float x = (i % 3 - 1) * 2f;
                float z = (i / 3) * 3f;
                var enemy = BuildDominionEnemy(new Vector3(x, 0f, z), playerHealth, enemyDef);
                enemy.transform.SetParent(phantomsContainer.transform, false);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, phantomColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                phantomHealths.Add(enemy.GetComponent<Health>());
            }

            // MirrorPhantom holder.
            var mirrorPhantomGo = new GameObject("MirrorPhantom");
            mirrorPhantomGo.transform.SetParent(maze, false);
            mirrorPhantomGo.transform.position = Vector3.zero;
            var mirrorPhantom = mirrorPhantomGo.AddComponent<MirrorPhantom>();
            var mpSo = new SerializedObject(mirrorPhantom);
            SetObjectRefList(mpSo, "phantoms", new List<Object>(phantomHealths));
            mpSo.ApplyModifiedPropertiesWithoutUndo();
            mirrorPhantomGo.SetActive(false);

            // Transition box: "APPROACH — THE CENTER TENT".
            var tentBoxGo = BuildTransitionBox("ToTentBox", new Vector3(0f, 1.2f, 20.5f), "APPROACH — THE CENTER TENT",
                out var tentBtn, out var tentTransition);
            var tbSo = new SerializedObject(tentTransition);
            tbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep13CenterTentSceneName;
            tbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(tentBtn.onClick,
                new UnityEngine.Events.UnityAction(tentTransition.LoadOnFootScene));
            tentBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue yask_warning (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Yask Warning";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = yaskWarningDialogue;

            // Step 1: Trigger — activate MirrorPhantom sequence.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Activate MirrorPhantom Sequence";
            var activateProp1 = s1.FindPropertyRelative("triggerObjects");
            activateProp1.arraySize = 1;
            activateProp1.GetArrayElementAtIndex(0).objectReferenceValue = mirrorPhantomGo;

            // Step 2: Prompt (null) — the sequence runs; onSequenceCleared → AdvanceFromPrompt.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: MirrorPhantom Sequence (cleared via onSequenceCleared)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 3: Dialogue mirror_maze (Cipher's quiet line after the child-phantom falls).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Mirror Maze (Cipher)";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = mirrorMazeDialogue;

            // Step 4: Dialogue mirror_after.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Mirror After";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = mirrorAfterDialogue;

            // Step 5: Prompt — transition to Center Tent.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Approach Center Tent";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = tentBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire MirrorPhantom.onSequenceCleared → missionDirector.AdvanceFromPrompt (step 2).
            UnityEventTools.AddPersistentListener(mirrorPhantom.onSequenceCleared,
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13MirrorMazeScenePath);
            EnsureScenesInBuild(Galaxy2Ep13MirrorMazeScenePath, Galaxy2Ep13CenterTentScenePath);

            Debug.Log($"[Space Samurai] EP13 Mirror Maze scene built at {Galaxy2Ep13MirrorMazeScenePath}. " +
                      "Layout: near-black holographic maze with cold blue accents. Maze walls. " +
                      "7 Phantom enemies (ghost-blue, nonLethal) in MirrorPhantom sequence. " +
                      "6 steps: yask_warning (auto) → trigger MirrorPhantom sequence → prompt (null, cleared via onSequenceCleared) → " +
                      "mirror_maze dialogue → mirror_after dialogue → transition to Center Tent.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Center Tent", priority = 138)]
        public static void BuildEp13CenterTent()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Center Tent: dark vial-forest cathedral with many unlit cyan/violet memory vial glow cubes.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.40f, 0.45f, 0.50f); // cool-dim key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.22f); // dark ambient

            // Dark vial-forest fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.28f, 0.32f);
            RenderSettings.fogDensity = 0.028f;

            // Two dim accent lights.
            BuildAccentPointLight("TentLight1", new Vector3(-3f, 2f, 6f),
                new Color(0.5f, 0.6f, 0.7f), intensity: 0.7f, range: 9f);
            BuildAccentPointLight("TentLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.45f, 0.55f, 0.65f), intensity: 0.65f, range: 8f);

            // ---- Center Tent cathedral ----
            var tentGo = new GameObject("CenterTent");
            var tent = tentGo.transform;
            var stoneGrey = new Color(0.38f, 0.38f, 0.40f);
            var darkStone = new Color(0.25f, 0.25f, 0.28f);

            // Main tent floor.
            BuildFloorCeiling(tent, "TentFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 20f), stoneGrey, darkStone);

            // Tent walls (cathedral-like enclosure).
            BuildWall(tent, "TentWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(tent, "TentWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Hanging memory vial glow cubes (cyan/violet) ----
            var vialCount = 12;
            for (int i = 0; i < vialCount; i++)
            {
                float x = (i % 4 - 1.5f) * 3.5f;
                float y = 2.5f + (i / 4) * 1f;
                float z = 6f + (i % 2) * 2f;
                var vialColor = (i % 2 == 0) ? new Color(0.2f, 0.8f, 1f) : new Color(0.6f, 0.3f, 0.9f); // cyan or violet
                var vial = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vial.name = $"MemoryVial_{i}";
                Object.DestroyImmediate(vial.GetComponent<Collider>());
                vial.transform.SetParent(tent, false);
                vial.transform.position = new Vector3(x, y, z);
                vial.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                vial.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(vialColor);
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

            // ---- Coral Vex NPC (non-combat, scenery) ----
            var coralVexPos = new Vector3(0f, 0f, 10f);
            var coralVexGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, coralVexPos, "CoralVex");
            if (coralVexGo != null)
            {
                var storyNpc = coralVexGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Coral Vex";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var ringmasterRevealDialogue = BuildEp13DialoguePlayer("Dialogue_RingmasterReveal", new Vector3(0f, 1.5f, 6f), "ringmaster_reveal");
            var ringDlgSo = new SerializedObject(ringmasterRevealDialogue);
            ringDlgSo.FindProperty("playOnStart").boolValue = true;
            ringDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var coralSurvivalDialogue = BuildEp13DialoguePlayer("Dialogue_CoralSurvival", new Vector3(0f, 1.5f, 8f), "coral_survival");
            var bladeAtThroatDialogue = BuildEp13DialoguePlayer("Dialogue_BladeAtThroat", new Vector3(0f, 1.5f, 12f), "blade_at_throat");

            // ---- 6 Extraction Guard enemies: 1 wave ----
            var guardColor = new Color(0.40f, 0.42f, 0.48f); // Vellum steel
            var guardWavePositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 5f),
                new Vector3(-0.5f, 0f, 5f),
                new Vector3(1.5f, 0f, 5f),
                new Vector3(-2f, 0f, 7f),
                new Vector3(0f, 0f, 7f),
                new Vector3(2f, 0f, 7f)
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
                new[] { BuildEp13DialoguePlayer("Dialogue_ExtractionBarks", new Vector3(0f, 1.5f, 8f), "extraction_barks") });

            // Transition box: "DESCEND — THE VAULT BELOW".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND — THE VAULT BELOW",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep13VaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue ringmaster_reveal (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ringmaster Reveal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ringmasterRevealDialogue;

            // Step 1: Dialogue coral_survival.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Coral Survival";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = coralSurvivalDialogue;

            // Step 2: DefeatWaves — 6 Extraction Guards.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Extraction Guards (6)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = guardSpawner;

            // Step 3: Dialogue blade_at_throat.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Blade At Throat";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = bladeAtThroatDialogue;

            // Step 4: Prompt — transition to Vault.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Descend to Vault";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13CenterTentScenePath);
            EnsureScenesInBuild(Galaxy2Ep13CenterTentScenePath, Galaxy2Ep13VaultScenePath);

            Debug.Log($"[Space Samurai] EP13 Center Tent scene built at {Galaxy2Ep13CenterTentScenePath}. " +
                      "Layout: dark vial-forest cathedral with hanging cyan/violet memory vial glow cubes. " +
                      "Coral Vex NPC (non-combat, StoryNpc). 6 Extraction Guards (steel, nonLethal). " +
                      "5 steps: ringmaster_reveal (auto) → coral_survival dialogue → defeat 6 Extraction Guards (extraction_barks bark) → " +
                      "blade_at_throat dialogue → transition to Vault.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Vault", priority = 139)]
        public static void BuildEp13Vault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault: grey Verath bedrock archive, dim, with red/amber status-light glow cubes.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.55f, 0.45f); // warm-dim key light
            light.intensity = 0.60f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.15f, 0.12f); // warm-dim ambient

            // Vault interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.30f, 0.25f);
            RenderSettings.fogDensity = 0.026f;

            // Two warm accent lights.
            BuildAccentPointLight("VaultLight1", new Vector3(-3f, 2f, 6f),
                new Color(0.8f, 0.65f, 0.4f), intensity: 0.8f, range: 9f);
            BuildAccentPointLight("VaultLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.75f, 0.6f, 0.35f), intensity: 0.75f, range: 8f);

            // ---- Vault archive ----
            var vaultGo = new GameObject("Vault");
            var vault = vaultGo.transform;
            var bedrock = new Color(0.42f, 0.40f, 0.36f);
            var darkBedrock = new Color(0.28f, 0.26f, 0.22f);

            // Main vault corridor floor.
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 8f), new Vector3(10f, 0f, 16f), bedrock, darkBedrock);

            // Corridor walls.
            BuildWall(vault, "VaultWall_W", new Vector3(-5f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(vault, "VaultWall_E", new Vector3(5f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));

            // Vault cells.
            var cellColor = new Color(0.30f, 0.32f, 0.35f);
            BuildProp(vault, "VaultCell1", new Vector3(-2f, 1.2f, 4f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "VaultCell2", new Vector3(2f, 1.2f, 4f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "VaultCell3", new Vector3(-2f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "VaultCell4", new Vector3(2f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);

            // ---- Status-light glow cubes (red/amber) ----
            var statusColor = new Color(1f, 0.5f, 0.2f);
            var statusLight1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            statusLight1.name = "StatusLight1";
            Object.DestroyImmediate(statusLight1.GetComponent<Collider>());
            statusLight1.transform.SetParent(vault, false);
            statusLight1.transform.position = new Vector3(-2f, 1.5f, 12f);
            statusLight1.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            statusLight1.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(statusColor);

            var statusLight2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            statusLight2.name = "StatusLight2";
            Object.DestroyImmediate(statusLight2.GetComponent<Collider>());
            statusLight2.transform.SetParent(vault, false);
            statusLight2.transform.position = new Vector3(2f, 1.5f, 14f);
            statusLight2.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            statusLight2.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(statusColor);

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

            // ---- Coral Vex as ALLY ----
            var coralAllyPos = new Vector3(-1f, 0f, 5f);
            var coralAllyGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, coralAllyPos, "CoralVexAlly");
            if (coralAllyGo != null)
            {
                var allyCombatant = coralAllyGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = coralAllyGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Coral Vex";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var vaultDescentDialogue = BuildEp13DialoguePlayer("Dialogue_VaultDescent", new Vector3(0f, 1.5f, 4f), "vault_descent");
            var descentDlgSo = new SerializedObject(vaultDescentDialogue);
            descentDlgSo.FindProperty("playOnStart").boolValue = true;
            descentDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var vaultTerminalDialogue = BuildEp13DialoguePlayer("Dialogue_VaultTerminal", new Vector3(0f, 1.5f, 14f), "vault_terminal");
            var khallsGambitDialogue = BuildEp13DialoguePlayer("Dialogue_KhallsGambit", new Vector3(0f, 1.5f, 16f), "khalls_gambit");

            // ---- 4 Dominion Sentry enemies: 1 wave ----
            var sentryColor = new Color(0.18f, 0.18f, 0.20f); // dark
            var sentryWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 5f),
                new Vector3(1.5f, 0f, 5.5f),
                new Vector3(-1f, 0f, 7f),
                new Vector3(1f, 0f, 7.5f)
            };

            var sentryWaveHealths = new List<Health>();
            foreach (var pos in sentryWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, sentryColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                sentryWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var sentrySpawner = BuildEp03WaveSpawner("SentrySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { sentryWaveHealths },
                new[] { BuildEp13DialoguePlayer("Dialogue_VerathBarks", new Vector3(0f, 1.5f, 8f), "verath_barks") });

            // Vault terminal reach point.
            var terminalReachGo = new GameObject("VaultTerminalReachPoint");
            terminalReachGo.transform.position = new Vector3(0f, 0.5f, 14f);

            // Transition box: "TO THE ACTIVATION CORE".
            var coreBoxGo = BuildTransitionBox("ToCoreBox", new Vector3(0f, 1.2f, 18.5f), "TO THE ACTIVATION CORE",
                out var coreBtn, out var coreTransition);
            var cbSo = new SerializedObject(coreTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep13CoreFightSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(coreBtn.onClick,
                new UnityEngine.Events.UnityAction(coreTransition.LoadOnFootScene));
            coreBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue vault_descent (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultDescentDialogue;

            // Step 1: DefeatWaves — 4 Dominion Sentries.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Sentries (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = sentrySpawner;

            // Step 2: ReachTrigger — vault terminal reach point.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Vault Terminal";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = terminalReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: Dialogue vault_terminal.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Vault Terminal";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = vaultTerminalDialogue;

            // Step 4: Dialogue khalls_gambit.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Khalls Gambit";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = khallsGambitDialogue;

            // Step 5: Prompt — transition to Core Fight.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: To Activation Core";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = coreBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13VaultScenePath);
            EnsureScenesInBuild(Galaxy2Ep13VaultScenePath, Galaxy2Ep13CoreFightScenePath);

            Debug.Log($"[Space Samurai] EP13 Vault scene built at {Galaxy2Ep13VaultScenePath}. " +
                      "Layout: grey Verath bedrock archive with vault cells and red/amber status-light glow cubes. " +
                      "Coral Vex ally (AllyCombatant). 4 Dominion Sentries (dark, nonLethal). " +
                      "6 steps: vault_descent (auto) → defeat 4 Dominion Sentries (verath_barks bark) → " +
                      "reach vault terminal → vault_terminal dialogue → khalls_gambit dialogue → transition to Core Fight.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Core Fight", priority = 140)]
        public static void BuildEp13CoreFight()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Core Fight: dark detonating core with red emergency light and broken-glass debris props.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.40f, 0.35f); // warm-dim key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -15f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.10f); // dark ambient

            // Core interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.20f, 0.15f);
            RenderSettings.fogDensity = 0.030f;

            // Red emergency light accent.
            BuildAccentPointLight("CoreEmergency", new Vector3(0f, 2.5f, 10f),
                new Color(1f, 0.2f, 0.2f), intensity: 1.2f, range: 15f);

            // ---- Core chamber ----
            var coreGo = new GameObject("CoreChamber");
            var core = coreGo.transform;
            var coreDark = new Color(0.28f, 0.25f, 0.22f);
            var coreBlack = new Color(0.15f, 0.12f, 0.10f);

            // Core floor.
            BuildFloorCeiling(core, "CoreFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 16f), coreDark, coreBlack);

            // Core walls.
            BuildWall(core, "CoreWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));
            BuildWall(core, "CoreWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 16f));

            // Broken-glass debris props.
            var debrisColor = new Color(0.40f, 0.42f, 0.45f);
            BuildProp(core, "Debris1", new Vector3(-3f, 0.5f, 6f), new Vector3(0.6f, 0.3f, 0.8f), debrisColor);
            BuildProp(core, "Debris2", new Vector3(3f, 0.5f, 8f), new Vector3(0.7f, 0.3f, 0.5f), debrisColor);
            BuildProp(core, "Debris3", new Vector3(-2f, 0.5f, 12f), new Vector3(0.5f, 0.3f, 0.6f), debrisColor);
            BuildProp(core, "Debris4", new Vector3(2.5f, 0.5f, 14f), new Vector3(0.6f, 0.3f, 0.7f), debrisColor);

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

            // ---- Ronin-10 BOSS (DuelYield) ----
            var ronin10Pos = new Vector3(0f, 0f, 10f);
            var ronin10 = BuildDominionEnemy(ronin10Pos, playerHealth, enemyDef);
            ronin10.gameObject.transform.localScale *= 1.05f;
            var ronin10Renderer = ronin10.GetComponent<Renderer>();
            if (ronin10Renderer != null) TintShared(ronin10Renderer, new Color(0.30f, 0.32f, 0.36f)); // hollow grey
            var ronin10Health = ronin10.GetComponent<Health>();

            // Ronin-10's strikes are non-lethal.
            var ronin10Melee = ronin10.GetComponent<MeleeAttacker>();
            if (ronin10Melee != null)
            {
                var maSo = new SerializedObject(ronin10Melee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var ronin10Npc = ronin10.gameObject.AddComponent<StoryNpc>();
            var rNpcSo = new SerializedObject(ronin10Npc);
            rNpcSo.FindProperty("displayName").stringValue = "Ronin-10";
            rNpcSo.FindProperty("remote").boolValue = false;
            rNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.3.
            var duelYield = ronin10.gameObject.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", ronin10Health);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.3f;
            if (ronin10Melee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { ronin10Melee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Ronin-10 STAYS ACTIVE (not SetActive(false)).

            // ---- Coral Vex as ALLY ----
            var coralCorePos = new Vector3(-1f, 0f, 5f);
            var coralCoreGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, coralCorePos, "CoralVexAlly");
            if (coralCoreGo != null)
            {
                var allyCombatant = coralCoreGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = coralCoreGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Coral Vex";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Player ----
            var ronin10Dialogue = BuildEp13DialoguePlayer("Dialogue_Ronin10", new Vector3(0f, 1.5f, 10f), "ronin10");

            // Wire DuelYield.onYielded → ronin10Dialogue.Play.
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(ronin10Dialogue.Play));

            // Transition box: "BOARD — ESCAPE THE CARNIVAL".
            var escapeBoxGo = BuildTransitionBox("ToEscapeBox", new Vector3(0f, 1.2f, 16.5f), "BOARD — ESCAPE THE CARNIVAL",
                out var escapeBtn, out var escapeTransition);
            var ebSo = new SerializedObject(escapeTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy2Ep13EscapeSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(escapeBtn.onClick,
                new UnityEngine.Events.UnityAction(escapeTransition.LoadOnFootScene));
            escapeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Prompt (null) — the duel (DuelYield.onAccepted advances it).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s0.FindPropertyRelative("label").stringValue = "Prompt: Duel Ronin-10 (yield + sheathe)";
            s0.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 1: Prompt — transition to Escape.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Board Escape";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = escapeBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13CoreFightScenePath);
            EnsureScenesInBuild(Galaxy2Ep13CoreFightScenePath, Galaxy2Ep13EscapeScenePath);

            Debug.Log($"[Space Samurai] EP13 Core Fight scene built at {Galaxy2Ep13CoreFightScenePath}. " +
                      "Layout: dark detonating core with red emergency light and broken-glass debris props. " +
                      "Ronin-10 boss (DuelYield, yield at 30%, ronin10 on yield, nonLethal, ACTIVE from start). " +
                      "Coral Vex ally (AllyCombatant). " +
                      "2 steps: duel Prompt (onAccepted → AdvanceFromPrompt) → transition to Escape.");
        }
    }
}

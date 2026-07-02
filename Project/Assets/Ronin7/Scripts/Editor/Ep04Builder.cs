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
    /// EP04 "The Sword Remembers" scene builders. Builds the jungle-moon vigil where Ronin-7
    /// confronts the Rustfangs, discovers the ancient archive ring and its Enforcer guardian,
    /// witnesses Khall's final revelation via the archive heart's video log, retrieves the ledger
    /// proving the blade's origin, and escapes for the Veiled Reaches. Wires all MissionDirector
    /// steps, enemy waves, and NPC interactions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep04JungleMoonScenePath = SceneFolder + "/Galaxy1_EP04_JungleMoon.unity";
        private const string Galaxy1Ep04ArchiveRingScenePath = SceneFolder + "/Galaxy1_EP04_ArchiveRing.unity";
        private const string Galaxy1Ep04LedgerScenePath = SceneFolder + "/Galaxy1_EP04_Ledger.unity";

        private static readonly string Galaxy1Ep04JungleMoonSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04JungleMoonScenePath);
        private static readonly string Galaxy1Ep04ArchiveRingSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04ArchiveRingScenePath);
        private static readonly string Galaxy1Ep04LedgerSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04LedgerScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP04 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep04" and loads lines from Ep04Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp04DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep04Lines.Get(setId), advanceRef, setId, clipPrefix: "ep04");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Jungle Moon", priority = 76)]
        public static void BuildEp04JungleMoon()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Jungle moon exterior: warm-green daylight, lush foliage, decaying ruins.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 0.7f);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.25f, 0.15f);

            // Jungle canopy fog: green exponential, low density.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.2f, 0.35f, 0.2f);
            RenderSettings.fogDensity = 0.03f;

            // Clearing accent lights.
            BuildAccentPointLight("ClearingLight1", new Vector3(-3f, 2.4f, 14f),
                new Color(0.8f, 1f, 0.6f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("ClearingLight2", new Vector3(3f, 2.4f, 14f),
                new Color(0.8f, 1f, 0.6f), intensity: 1.6f, range: 14f);

            // ---- Jungle moon exterior: ramp vigil area (z ~0-4) -> wreckage clearing (z ~8-16) ->
            // back to ship bridge nook (z ~2-8, side). Large ground plane (jungle-green, no ceiling).
            var exteriorGo = new GameObject("JungleExterior");
            var exterior = exteriorGo.transform;

            // Ground plane: large flat landscape (60x60, flattened cube).
            var groundPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundPlane.name = "GroundPlane";
            groundPlane.transform.SetParent(exterior, false);
            groundPlane.transform.localPosition = new Vector3(0f, -0.5f, 30f);
            groundPlane.transform.localScale = new Vector3(60f, 1f, 60f);
            TintShared(groundPlane.GetComponent<Renderer>(), new Color(0.15f, 0.28f, 0.12f));
            var groundCollider = groundPlane.GetComponent<Collider>();
            if (groundCollider != null) groundCollider.isTrigger = false;

            // Trees and jungle props scattered via BuildJungleProps.
            BuildJungleProps(exterior, 25f);

            // Corsair hint: grey BuildProp boxes forming a grounded ship hull + ramp.
            // Kept behind the player spawn (origin) so the rig never starts inside the hull collider.
            var shipBodyColor = new Color(0.5f, 0.5f, 0.52f);
            BuildProp(exterior, "ShipHull_Main", new Vector3(0f, 1.5f, -8f), new Vector3(6f, 3f, 10f), shipBodyColor);
            BuildProp(exterior, "ShipHull_Bridge", new Vector3(0f, 2.5f, -14f), new Vector3(4f, 2f, 4f), shipBodyColor);
            BuildProp(exterior, "RampBox", new Vector3(0f, 0.2f, -2.5f), new Vector3(3f, 0.4f, 3f), new Color(0.6f, 0.55f, 0.5f));

            // Landing pad prop: rust-metal color.
            var padColor = new Color(0.65f, 0.45f, 0.35f);
            BuildProp(exterior, "LandingPad", new Vector3(-8f, 0f, 20f), new Vector3(5f, 0.3f, 5f), padColor);

            // Wreckage props scattered in the clearing.
            BuildProp(exterior, "Wreckage1", new Vector3(5f, 0.5f, 12f), new Vector3(2f, 1.5f, 3f), padColor);
            BuildProp(exterior, "Wreckage2", new Vector3(-6f, 0.4f, 15f), new Vector3(3f, 1f, 2f), padColor);
            BuildProp(exterior, "Wreckage3", new Vector3(4f, 0.3f, 20f), new Vector3(1.5f, 0.8f, 2.5f), padColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Kessler near the ramp, beside the player spawn.
            var kesslerPos = new Vector3(1.5f, 1f, -1f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_JungleMoon");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Alarm lights (attack ambiance, activated when scouts emerge). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 13f), new Vector3(2f, 2.4f, 15f));

            // ---- Dialogue Players ----
            var jungleVigilDialogue = BuildEp04DialoguePlayer("Dialogue_JungleVigil", kesslerPos, "jungle_vigil", talkRef);
            var jungleFightBarksDialogue = BuildEp04DialoguePlayer("Dialogue_JungleFightBarks", new Vector3(0f, 1.5f, 14f), "jungle_fight_barks");
            var jungleConfessionDialogue = BuildEp04DialoguePlayer("Dialogue_JungleConfession", new Vector3(0f, 1f, -1f), "jungle_confession", talkRef);

            // ---- Enemies: 4 Rustfang scouts (rust-orange tint). ----
            var rustOrange = new Color(0.75f, 0.4f, 0.15f);
            var scoutPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 11f),
                new Vector3(2f, 0f, 12f),
                new Vector3(-1f, 0f, 14f),
                new Vector3(1f, 0f, 16f)
            };
            var scoutHealths = new List<Health>();
            foreach (var pos in scoutPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, rustOrange);
                enemy.gameObject.SetActive(false);
                scoutHealths.Add(enemy.GetComponent<Health>());
            }

            var scoutWaveSpawner = BuildWaveSpawner("ScoutWaveSpawner", new Vector3(0f, 1f, 11f), 3f,
                new List<List<Health>> { scoutHealths }, new[] { jungleFightBarksDialogue });

            // Reach triggers.
            var clearingReachGo = new GameObject("ClearingReachPoint");
            clearingReachGo.transform.position = new Vector3(0f, 1f, 14f);
            var nookReachGo = new GameObject("NookReachPoint");
            nookReachGo.transform.position = new Vector3(0f, 1f, -1f);

            // Transition box: "LAUNCH FOR THE VEILED REACHES" wired to ReturnToSpace.
            var launchBoxGo = BuildTransitionBox("LaunchToVeiledReachesBox", new Vector3(0f, 1.2f, 1.5f), "LAUNCH FOR THE VEILED REACHES",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue jungle_vigil (Kessler's ramp vigil).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ramp Vigil";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = jungleVigilDialogue;

            // Step 1: ReachTrigger — wreckage clearing.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Clearing";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = clearingReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights (scouts emerge).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Scout Alarm";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 3: DefeatWaves — 4 Rustfang scouts.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Rustfang Scouts";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = scoutWaveSpawner;

            // Step 4: ReachTrigger — ship bridge nook.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Bridge Nook";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = nookReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue jungle_confession (Kessler's confession).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: The Confession";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = jungleConfessionDialogue;

            // Step 6: Prompt — launch to the Veiled Reaches.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Launch to Veiled Reaches";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04JungleMoonScenePath);
            EnsureScenesInBuild(Galaxy1Ep04JungleMoonScenePath);

            Debug.Log($"[Space Samurai] EP04 Jungle Moon scene built at {Galaxy1Ep04JungleMoonScenePath}. " +
                      "Layout: exterior ground plane with jungle props, ramp vigil area (Kessler) → wreckage clearing (4 Rustfang scouts) → ship bridge nook. " +
                      "Green exponential fog + warm-green daylight. " +
                      "7 steps: jungle_vigil → reach clearing → trigger alarms → defeat 4 scouts + barks → reach nook → jungle_confession → launch to Veiled Reaches.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Archive Ring", priority = 77)]
        public static void BuildEp04ArchiveRing()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive ring interior: amber solar-flare, cold-grey mood, ancient tech.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.75f, 0.65f);
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.1f);

            // Amber solar-flare fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.18f);
            RenderSettings.fogDensity = 0.025f;

            // Amber accent lights.
            BuildAccentPointLight("RingLight1", new Vector3(-3f, 2.6f, 12f),
                new Color(1f, 0.75f, 0.4f), intensity: 1.7f, range: 14f);
            BuildAccentPointLight("RingLight2", new Vector3(3f, 2.6f, 20f),
                new Color(1f, 0.7f, 0.35f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("NaveLight", new Vector3(0f, 2.6f, 28f),
                new Color(0.9f, 0.75f, 0.5f), intensity: 1.8f, range: 16f);

            // ---- Archive ring: airlock (z 0-6) -> outer corridor (z 6-18) -> central nave (z 18-38) ->
            // heart-gate alcove (z 38-44).
            var interiorGo = new GameObject("ArchiveInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.2f, 0.22f);
            var ceilColor = new Color(0.12f, 0.12f, 0.14f);

            // Airlock: x[-3,3], z[0,6].
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 3f), new Vector3(6f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-3f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallE", new Vector3(3f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 6f), 6f, true, 2.4f);

            // Outer corridor: x[-3.5,3.5], z[6,18].
            BuildFloorCeiling(interior, "Corridor", new Vector3(0f, 0f, 12f), new Vector3(7f, 0f, 12f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -3.5f, 6f, 18f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 3.5f, 6f, 18f, new float[0], 2.4f);

            // Central nave: x[-6,6], z[18,38], with raised catwalk floor strips (y~0.4).
            BuildFloorCeiling(interior, "NaveFloor", new Vector3(0f, 0f, 28f), new Vector3(12f, 0f, 20f),
                new Color(0.15f, 0.15f, 0.17f), ceilColor);
            BuildWall(interior, "Nave_WallW", new Vector3(-6f, 1.5f, 28f), new Vector3(0.2f, 3f, 20f));
            BuildWall(interior, "Nave_WallE", new Vector3(6f, 1.5f, 28f), new Vector3(0.2f, 3f, 20f));

            // Catwalk strips (raised platforms where Sentries stand).
            var catwalkColor = new Color(0.25f, 0.25f, 0.27f);
            BuildProp(interior, "CatwalkW", new Vector3(-4f, 0.4f, 22f), new Vector3(1.5f, 0.2f, 8f), catwalkColor);
            BuildProp(interior, "CatwalkCenter", new Vector3(0f, 0.4f, 28f), new Vector3(2f, 0.2f, 8f), catwalkColor);
            BuildProp(interior, "CatwalkE", new Vector3(4f, 0.4f, 32f), new Vector3(1.5f, 0.2f, 8f), catwalkColor);

            // Heart-gate alcove: x[-4,4], z[38,44].
            BuildFloorCeiling(interior, "HeartAlcove", new Vector3(0f, 0f, 41f), new Vector3(8f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "HeartAlcove_WallW", new Vector3(-4f, 1.5f, 41f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "HeartAlcove_WallE", new Vector3(4f, 1.5f, 41f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "HeartAlcove_WallBack", new Vector3(0f, 1.5f, 44f), new Vector3(8f, 3f, 0.2f));

            // Sliding door "RingDoor" at z=6.
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var ringDoor = BuildSlidingDoor(interior, "RingDoor", new Vector3(0f, 0f, 6f), 2.4f, true, startLocked: false);
            WireDoorAudio(ringDoor, doorSlideClip);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Dialogue Players ----
            var archiveGhostDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveGhost", new Vector3(0f, 1.5f, 2f), "archive_ghost");
            var archiveSentryBarksDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveSentryBarks", new Vector3(0f, 1.5f, 24f), "archive_sentry_barks");
            var archiveWelcomeDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveWelcome", new Vector3(0f, 1.5f, 41f), "archive_welcome", talkRef);

            // ---- Heart-of-the-archive dialogue (folded in from the merged Archive Heart scene): Khall's video
            // log + the blade-truth reveal play in the nave; the encrypted-files/alarm/escape beats in the alcove. ----
            var heartKhallLogDialogue = BuildEp04DialoguePlayer("Dialogue_KhallLog", new Vector3(0f, 1.5f, 30f), "heart_khall_log");
            var heartBladeTruthDialogue = BuildEp04DialoguePlayer("Dialogue_BladeTruth", new Vector3(0f, 1.5f, 30f), "heart_blade_truth", talkRef);
            var heartEnforcerChallengeDialogue = BuildEp04DialoguePlayer("Dialogue_EnforcerChallenge", new Vector3(0f, 1.5f, 30f), "heart_enforcer_challenge");
            var heartEnforcerAfterDialogue = BuildEp04DialoguePlayer("Dialogue_EnforcerAfter", new Vector3(0f, 1.5f, 32f), "heart_enforcer_after");
            var heartFilesDialogue = BuildEp04DialoguePlayer("Dialogue_Files", new Vector3(0f, 1f, 41f), "heart_files", talkRef);
            var heartAlarmDialogue = BuildEp04DialoguePlayer("Dialogue_HeartAlarm", new Vector3(0f, 1.5f, 41f), "heart_alarm");
            var heartEscapeDialogue = BuildEp04DialoguePlayer("Dialogue_EscapeRun", new Vector3(0f, 1.5f, 41f), "heart_escape");

            // ---- Enemies: 2 Recon Sentries (pale-grey tint) — trimmed from 3 in the consolidation pass so the
            // merged scene's nave skirmish stays light ahead of the Enforcer duel. ----
            var sentryGrey = new Color(0.72f, 0.75f, 0.8f);
            var sentryPositions = new Vector3[]
            {
                new Vector3(-4f, 0.6f, 22f),
                new Vector3(4f, 0.6f, 30f)
            };
            var sentryHealths = new List<Health>();
            foreach (var pos in sentryPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, sentryGrey);
                enemy.gameObject.SetActive(false);
                sentryHealths.Add(enemy.GetComponent<Health>());
            }

            var sentryWaveSpawner = BuildWaveSpawner("SentryWaveSpawner", new Vector3(0f, 1f, 20f), 3f,
                new List<List<Health>> { sentryHealths }, new[] { archiveSentryBarksDialogue });

            // ---- Enemy: Dominion Enforcer duel (silver, 3.5x health) — folded in from the merged Archive Heart;
            // the episode's climactic guardian fight, staged in the nave. ----
            var enforcerSilver = new Color(0.8f, 0.82f, 0.88f);
            var enforcer = BuildDominionEnemy(new Vector3(0f, 0f, 30f), playerHealth, enemyDef);
            var enforcerRenderer = enforcer.GetComponent<Renderer>();
            if (enforcerRenderer != null) TintShared(enforcerRenderer, enforcerSilver);
            var enforcerHealth = enforcer.GetComponent<Health>();
            if (enforcerHealth != null)
            {
                var ehSo = new SerializedObject(enforcerHealth);
                ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 3.5f;
                ehSo.ApplyModifiedPropertiesWithoutUndo();
            }
            enforcer.gameObject.SetActive(false);

            var enforcerWaveSpawner = BuildWaveSpawner("EnforcerWaveSpawner", new Vector3(0f, 1f, 28f), 3f,
                new List<List<Health>> { new List<Health> { enforcerHealth } }, new[] { heartEnforcerChallengeDialogue });

            // ---- Heart props (folded in): Khall's video screen on the nave back wall, the records terminal in
            // the alcove, and red alarm lights for the escape beat. ----
            var videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoQuad.name = "VideoScreen";
            videoQuad.transform.SetParent(interior, false);
            videoQuad.transform.localPosition = new Vector3(0f, 1.8f, 37.8f);
            videoQuad.transform.localScale = new Vector3(5f, 3f, 1f);
            TintShared(videoQuad.GetComponent<Renderer>(), new Color(0.2f, 0.15f, 0.1f, 0.7f));
            var videoCollider = videoQuad.GetComponent<Collider>();
            if (videoCollider != null) Object.DestroyImmediate(videoCollider);

            BuildProp(interior, "RecordsTerminal", new Vector3(0f, 0.7f, 43f), new Vector3(1.4f, 1.4f, 0.6f), new Color(0.1f, 0.12f, 0.15f));

            var heartAlarmLightsGo = BuildEp03AlarmLights(new Vector3(-3f, 2.4f, 40f), new Vector3(3f, 2.4f, 42f));

            // Reach triggers.
            var naveReachGo = new GameObject("NaveReachPoint");
            naveReachGo.transform.position = new Vector3(0f, 1f, 24f);
            var terminalReachGo = new GameObject("TerminalReachPoint");
            terminalReachGo.transform.position = new Vector3(0f, 1f, 41f);

            // Transition box: "ESCAPE TO THE CORSAIR" (Archive Heart merged into this scene — the reveal +
            // Enforcer duel play here now, so the ring chains straight to the Ledger finale scene).
            var heartBoxGo = BuildTransitionBox("EscapeToCorsairBox", new Vector3(0f, 1.2f, 43.2f), "ESCAPE TO THE CORSAIR",
                out var heartBtn, out var heartTransition);
            var htSo = new SerializedObject(heartTransition);
            htSo.FindProperty("onFootScene").stringValue = Galaxy1Ep04LedgerSceneName;
            htSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(heartBtn.onClick,
                new UnityEngine.Events.UnityAction(heartTransition.LoadOnFootScene));
            heartBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 15;

            // Step 0: Dialogue archive_ghost (the ghost signal, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ghost Signal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveGhostDialogue;

            // Step 1: Trigger — open RingDoor.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Open Ring Door";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = ringDoor;

            // Step 2: ReachTrigger — central nave.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Central Nave";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = naveReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: DefeatWaves — 2 Recon Sentries (trimmed skirmish).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Recon Sentries";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = sentryWaveSpawner;

            // Step 4: Dialogue heart_khall_log (Khall's video log, auto) — folded from Archive Heart.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Khall's Video Log";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = heartKhallLogDialogue;

            // Step 5: Dialogue heart_blade_truth (the blade's truth — THE reveal, talk-gated).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: The Blade's Truth";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = heartBladeTruthDialogue;

            // Step 6: DefeatWaves — Enforcer duel (silver, 3.5x health), the climax.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s6.FindPropertyRelative("label").stringValue = "DefeatWaves: Enforcer Duel";
            s6.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerWaveSpawner;

            // Step 7: Dialogue heart_enforcer_after (after the duel, auto).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: After the Duel";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = heartEnforcerAfterDialogue;

            // Step 8: ReachTrigger — records terminal in the heart alcove.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s8.FindPropertyRelative("label").stringValue = "ReachTrigger: Records Terminal";
            s8.FindPropertyRelative("reachPoint").objectReferenceValue = terminalReachGo.transform;
            s8.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 9: Dialogue archive_welcome (entry to the inner records granted, talk-gated).
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s9.FindPropertyRelative("label").stringValue = "Dialogue: Welcome Home Elegy";
            s9.FindPropertyRelative("dialogue").objectReferenceValue = archiveWelcomeDialogue;

            // Step 10: Dialogue heart_files (the encrypted files, talk-gated).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s10.FindPropertyRelative("label").stringValue = "Dialogue: The Encrypted Files";
            s10.FindPropertyRelative("dialogue").objectReferenceValue = heartFilesDialogue;

            // Step 11: Trigger — heart alarm lights.
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s11.FindPropertyRelative("label").stringValue = "Trigger: Heart Alarm";
            var t11 = s11.FindPropertyRelative("triggerObjects");
            t11.arraySize = 1;
            t11.GetArrayElementAtIndex(0).objectReferenceValue = heartAlarmLightsGo;

            // Step 12: Dialogue heart_alarm (alarms wail, auto).
            var s12 = stepsProp.GetArrayElementAtIndex(12);
            s12.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s12.FindPropertyRelative("label").stringValue = "Dialogue: Heart Alarms";
            s12.FindPropertyRelative("dialogue").objectReferenceValue = heartAlarmDialogue;

            // Step 13: Dialogue heart_escape (dogfight over comms, auto).
            var s13 = stepsProp.GetArrayElementAtIndex(13);
            s13.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s13.FindPropertyRelative("label").stringValue = "Dialogue: Escape Run";
            s13.FindPropertyRelative("dialogue").objectReferenceValue = heartEscapeDialogue;

            // Step 14: Prompt — escape to the Corsair (chains to the EP04 Ledger finale).
            var s14 = stepsProp.GetArrayElementAtIndex(14);
            s14.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s14.FindPropertyRelative("label").stringValue = "Prompt: Escape to the Corsair";
            s14.FindPropertyRelative("promptObject").objectReferenceValue = heartBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04ArchiveRingScenePath);
            EnsureScenesInBuild(Galaxy1Ep04ArchiveRingScenePath);

            Debug.Log($"[Space Samurai] EP04 Archive Ring scene built at {Galaxy1Ep04ArchiveRingScenePath} " +
                      "(Archive Heart merged in). Layout: airlock → outer corridor → central nave (catwalk strips, " +
                      "2 Sentries + the Enforcer duel, Khall video screen) → heart-gate alcove (records terminal). " +
                      "Amber solar-flare fog + cold-grey accents. " +
                      "15 steps: archive_ghost → open RingDoor → reach nave → defeat 2 Sentries → heart_khall_log → " +
                      "heart_blade_truth → Enforcer duel → heart_enforcer_after → reach terminal → archive_welcome → " +
                      "heart_files → heart alarms → heart_alarm → heart_escape → ESCAPE TO THE CORSAIR (chains to Ledger).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Ledger", priority = 79)]
        public static void BuildEp04Ledger()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Corsair salvage bay: warm interior light, intimate space.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.9f, 0.85f, 0.75f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.1f);

            // Interior light accents.
            BuildAccentPointLight("TableLight", new Vector3(0f, 2.4f, 7f),
                new Color(1f, 0.9f, 0.7f), intensity: 1.6f, range: 12f);

            // ---- Corsair salvage bay: one small room z 0-14, x[-4,4].
            var interiorGo = new GameObject("SalvageBay");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.22f, 0.2f, 0.18f);
            var ceilColor = new Color(0.12f, 0.11f, 0.1f);

            // Bay: x[-4,4], z[0,14].
            BuildFloorCeiling(interior, "Bay", new Vector3(0f, 0f, 7f), new Vector3(8f, 0f, 14f), floorColor, ceilColor);
            BuildWall(interior, "Bay_WallW", new Vector3(-4f, 1.5f, 7f), new Vector3(0.2f, 3f, 14f));
            BuildWall(interior, "Bay_WallE", new Vector3(4f, 1.5f, 7f), new Vector3(0.2f, 3f, 14f));
            BuildWall(interior, "Bay_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));
            BuildWall(interior, "Bay_WallBack", new Vector3(0f, 1.5f, 14f), new Vector3(8f, 3f, 0.2f));

            // Chart table prop in the center (z~7).
            BuildProp(interior, "ChartTable", new Vector3(0f, 0.7f, 7f), new Vector3(2.5f, 0.8f, 2f), new Color(0.3f, 0.28f, 0.25f));

            // Hyperspace window quads on side walls (blue-tinted, semi-transparent, no colliders).
            var windowColor = new Color(0.25f, 0.45f, 0.9f, 0.6f);
            var windowW = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowW.name = "WindowW";
            windowW.transform.SetParent(interior, false);
            windowW.transform.localPosition = new Vector3(-4f, 1.5f, 7f);
            windowW.transform.localScale = new Vector3(0.2f, 2f, 3f);
            TintShared(windowW.GetComponent<Renderer>(), windowColor);
            var wcW = windowW.GetComponent<Collider>();
            if (wcW != null) Object.DestroyImmediate(wcW);

            var windowE = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowE.name = "WindowE";
            windowE.transform.SetParent(interior, false);
            windowE.transform.localPosition = new Vector3(4f, 1.5f, 7f);
            windowE.transform.localScale = new Vector3(0.2f, 2f, 3f);
            TintShared(windowE.GetComponent<Renderer>(), windowColor);
            var wcE = windowE.GetComponent<Collider>();
            if (wcE != null) Object.DestroyImmediate(wcE);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 30f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Kessler by the chart table.
            var kesslerPos = new Vector3(-1f, 1f, 7f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_Ledger");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var ledgerCountDialogue = BuildEp04DialoguePlayer("Dialogue_LedgerCount", kesslerPos, "ledger_count");
            var ledgerRevelationDialogue = BuildEp04DialoguePlayer("Dialogue_LedgerRevelation", kesslerPos, "ledger_revelation", talkRef);

            // Reach trigger.
            var tableReachGo = new GameObject("TableReachPoint");
            tableReachGo.transform.position = new Vector3(0f, 1f, 7f);

            // Transition box: "LAUNCH TO SPACE" wired to ReturnToSpace.
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 12f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue ledger_count (the ledger count, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ledger";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ledgerCountDialogue;

            // Step 1: ReachTrigger — chart table.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Chart Table";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = tableReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 2: Dialogue ledger_revelation (the final revelation).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Final Revelation";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = ledgerRevelationDialogue;

            // Step 3: Prompt — launch to space.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04LedgerScenePath);
            EnsureScenesInBuild(Galaxy1Ep04LedgerScenePath);

            Debug.Log($"[Space Samurai] EP04 Ledger scene built at {Galaxy1Ep04LedgerScenePath}. " +
                      "Layout: Corsair salvage bay interior (chart table center, Kessler, hyperspace window quads on sides). " +
                      "4 steps: ledger_count auto → reach chart table → ledger_revelation → LAUNCH TO SPACE (ReturnToSpace, marks EP04 complete).");
        }
    }
}

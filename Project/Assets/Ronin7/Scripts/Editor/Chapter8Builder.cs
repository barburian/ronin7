using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Editor.Art;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter 8 ("The Silent Garden") scene builder. One self-contained scene, an open fog-drowned
    /// burial plain along +Z: the Cairn briefing (voice-only) -> the gate of fog (crew held, Ronin
    /// walks in alone with Echo) -> the grave-paths (a single riddle-trial gate; a wrong answer wakes
    /// spectral guardians, the true answer opens the way) -> the deep garden (the Warden boss, the
    /// showcase fight for Ch7's weakpoint-sight) -> the still center (the Mourners manifest, name him
    /// "the one who defies", and grant the vision) -> a comfort-safe memory dive holding the two
    /// visions (the faceless killswitch-maker; Khall's grief and doubt, Ladder D rung 2) -> the leaving
    /// (the Mourners' farewell + descent hook) -> the gate again (crew reunion, the report). Closes Act
    /// II (EP15-16). Ships NO new player ability.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch8</c>.
    ///
    /// CREW-PRESENCE DECISION: canon has the crew held at the gate and Ronin cross alone with only Echo
    /// ("the Garden admits only the one it means to test... an unseen will holds the crew at the gate").
    /// Unlike Ch4/Ch5's landing parties, NOTHING of the crew (Kessler/Coral/Mera/Morrigan/Iris/Resh/
    /// Mira) gets physical placement anywhere in this scene, extending Ch6's "no body in the scene"
    /// convention chapter-wide (Ch6 did this for a solo climb; Ch8 does it for a solo trial). Every
    /// crew line, at the briefing and at the reunion, is a voice-only DialoguePlayer.
    ///
    /// THE RIDDLE TRIAL / PUZZLE SCOPE CUT: the source script frames three seed puzzles (Grave of True
    /// Names, The Honest Order, The Fog's Question) as production-note placeholders explicitly
    /// "designed in a later pass," not fully mechanized content, and its own "disturbance loop"
    /// (guardians respawning on every retry, the puzzle visibly resetting) is likewise a seed design.
    /// This pass builds ONE riddle as the interactive <see cref="RiddleTrial"/> gate: Puzzle C ("The
    /// Fog's Question"), the only one of the three with a fully authored answer and the one the source
    /// script's own voice notes call "the thematic spine of the whole trial." Puzzles A/B's set-dressing
    /// and the phantom-mirror mechanic are out of scope, mirroring Chapter7's blade-rescue side-
    /// objective scope cut. See <see cref="RiddleTrial"/>'s own doc comment for the resulting
    /// "guardians spawn once, not every retry" simplification.
    ///
    /// VISION-DIVE DECISION: the reveal's two visions (the faceless hand; Khall's grief and doubt) are
    /// walkable memory-space geometry inside one <see cref="MemoryDiveController"/> island, mirroring
    /// Ch3's Kethel-7 playback and Ch7's mindspace duel — comfort-safe teleport in/out, no forced camera
    /// motion, per the VR constraints. Vision A (the sterile room) deliberately stages NO figure for the
    /// woman herself: the vision never finds her face, so nothing but the room and the table is built.
    /// Vision B (Khall's handler's bay) reuses Ch3's ghost-cast staging (<c>MakeGhostMaterial</c>) for
    /// Khall and a silhouette for footage-Ronin, the same "recording, not reality" read.
    ///
    /// ABILITY WIRING: Ch8 grants no new ability (per design: "Ch08 ships NO new player ability"). The
    /// rig still carries every previously-shipped ability via <see cref="AttachPlayerAbilities"/> (so
    /// weakpoint-sight, earned in Ch7, is present and self-gates on <c>CampaignState.HasAbility</c> — it
    /// is the showcase mechanic for the Warden fight below). No <see cref="AbilityGranter"/> is built.
    ///
    /// PLACEHOLDERS: the Warden (Massive archetype) and the Mourners (Hooded archetype) both resolve
    /// via <see cref="PlaceholderCharacterBuilder"/>'s existing specs at
    /// <c>Art/Generated/Characters3D/Named/The-Warden.prefab</c> / <c>The-Mourners.prefab</c> — no new
    /// meshes are authored here. Khall reuses his existing Named prefab (first used by Ch3's playback).
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch8ScenePath = SceneFolder + "/Ch08_SilentGarden.unity";
        private const string Ch8VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        private const string Ch8WardenPrefab = PlaceholderCharacterFolder + "/The-Warden.prefab";
        private const string Ch8MournersPrefab = PlaceholderCharacterFolder + "/The-Mourners.prefab";
        private const string Ch8KhallPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab";
        private const string Ch8EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 08 — The Silent Garden", priority = 208)]
        public static void BuildChapter8SilentGarden()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var buriedDef = Ch8EnsureBuriedDefinition();
            var wardenDef = Ch8EnsureWardenDefinition();

            // ---- Lighting: sourceless flat grey, no sky, no horizon. Fog is the Garden's hand (per
            // the source script's voice notes) so it is thick enough to read as an active presence. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.62f, 0.66f);
            light.intensity = 0.3f;
            lightGo.transform.rotation = Quaternion.Euler(60f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.23f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.52f, 0.54f, 0.57f);
            RenderSettings.fogDensity = 0.032f;

            BuildAccentPointLight("GateLight", new Vector3(0f, 2.2f, 10f), new Color(0.6f, 0.64f, 0.7f), 1f, 12f);
            BuildAccentPointLight("TrialLight", new Vector3(0f, 2.2f, 35f), new Color(0.6f, 0.64f, 0.7f), 1f, 14f);
            BuildAccentPointLight("BarrowLight0", new Vector3(-4f, 2.6f, 66f), new Color(0.7f, 0.68f, 0.62f), 1.4f, 16f);
            BuildAccentPointLight("BarrowLight1", new Vector3(4f, 2.6f, 70f), new Color(0.7f, 0.68f, 0.62f), 1.4f, 16f);

            // ---- World root: an open fog plain (no walls, no ceiling) running from the gate to the
            // barrow, headstones scattered along both sides. ----
            var worldGo = new GameObject("SilentGarden");
            var world = worldGo.transform;

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundGo.name = "GardenGround";
            groundGo.transform.SetParent(world, false);
            groundGo.transform.localPosition = new Vector3(0f, -0.5f, 45f);
            groundGo.transform.localScale = new Vector3(30f, 1f, 100f);
            TintShared(groundGo.GetComponent<Renderer>(), new Color(0.16f, 0.18f, 0.17f));

            Ch8ScatterHeadstones(world, 14f, 62f, 4f);

            // The fog gate is a visual marker only — per the source script it parts for the player
            // without resistance ("the pressure that held the crew parts for him without sound"), so
            // unlike BuildProp's default (a solid, collidable box) its collider is stripped here.
            var fogGateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fogGateGo.name = "FogGateWall";
            fogGateGo.transform.SetParent(world, false);
            fogGateGo.transform.localPosition = new Vector3(0f, 1.8f, 10f);
            fogGateGo.transform.localScale = new Vector3(14f, 3.6f, 0.6f);
            TintShared(fogGateGo.GetComponent<Renderer>(), new Color(0.58f, 0.6f, 0.63f));
            Object.DestroyImmediate(fogGateGo.GetComponent<Collider>());

            var barrowGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            barrowGo.name = "Barrow";
            barrowGo.transform.SetParent(world, false);
            barrowGo.transform.localPosition = new Vector3(0f, -1f, 68f);
            barrowGo.transform.localScale = new Vector3(8f, 3f, 8f);
            TintShared(barrowGo.GetComponent<Renderer>(), new Color(0.2f, 0.24f, 0.2f));

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, every shipped ability (weakpoint-sight
            // self-gates on CampaignState.HasAbility — Ch8 grants nothing new, see the class summary). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            // Covers the garden plain (z 0-90) AND the vision-dive island offset to z=250 (Ch7 precedent:
            // ZoneBounds clamps XZ every frame regardless of dive state, so it must reach the dive too).
            bounds.center = new Vector3(0f, 3f, 125f);
            bounds.radius = 180f;
            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act II — no rack-wake beat, matching Ch5-7's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch8EchoBladePrefab);

            // ---- The riddle trial: two answer pads on the grave-paths. Index 1 ("None") is the
            // Garden's true answer; index 0 ("Obey") is the trained reflex the Garden is listening for. ----
            var padObeyPoint = Ch8BuildAnswerPad(world, "AnswerPad_Obey", new Vector3(-2.5f, 0f, 35f), "OBEY", new Color(0.4f, 0.38f, 0.36f));
            var padNonePoint = Ch8BuildAnswerPad(world, "AnswerPad_None", new Vector3(2.5f, 0f, 35f), "NONE", new Color(0.4f, 0.38f, 0.36f));

            // Built INACTIVE and wired as the riddle Prompt step's promptObject (below): MissionDirector
            // .BeginPrompt SetActive(true)s it only when that step begins, and AdvanceFromPrompt
            // SetActive(false)s it on the correct answer. This gates the trial to its own beat — without
            // it, a player wandering to the correct pad during the briefing would latch the trial passed
            // (RiddleTrialLogic.Passed) while AdvanceFromPrompt no-ops off-step, soft-locking the Prompt.
            var trialGo = new GameObject("GraveRiddleTrial");
            var riddleTrial = trialGo.AddComponent<RiddleTrial>();
            var trialSo = new SerializedObject(riddleTrial);
            var answerPointsProp = trialSo.FindProperty("answerPoints");
            answerPointsProp.arraySize = 2;
            answerPointsProp.GetArrayElementAtIndex(0).objectReferenceValue = padObeyPoint;
            answerPointsProp.GetArrayElementAtIndex(1).objectReferenceValue = padNonePoint;
            trialSo.FindProperty("correctAnswerIndex").intValue = 1;
            trialSo.FindProperty("answerRadius").floatValue = 1.75f;
            trialSo.ApplyModifiedPropertiesWithoutUndo();
            trialGo.SetActive(false);

            // ---- The buried: 3 spectral guardians, inactive until the wrong-answer wave spawner Begins. ----
            Vector3[] buriedPositions = { new Vector3(-2f, 0f, 40f), new Vector3(0f, 0f, 42f), new Vector3(2f, 0f, 40f) };
            var buriedEnemies = new List<GameObject>();
            foreach (var pos in buriedPositions)
            {
                var e = BuildEnemy(pos, playerHealth, buriedDef);
                e.gameObject.SetActive(false);
                buriedEnemies.Add(e.gameObject);
            }

            // ---- The Warden: the boss, inactive until MissionDirector's DefeatEnemies step activates it. ----
            var wardenEnemy = Ch8BuildWarden(new Vector3(0f, 0f, 66f), wardenDef, playerHealth);
            wardenEnemy.gameObject.SetActive(false);

            // ---- The Mourners: manifest only at the reveal, inactive until that beat's Trigger step. ----
            var mournersRingGo = Ch8BuildMournersRing(world, new Vector3(0f, 0f, 68f), 5f, 4);

            // ---- The vision dive: a small memory-space island offset far from the main plain (still
            // inside ZoneBounds' radius above), holding both visions. Starts fully inactive. ----
            var visionDiveGo = new GameObject("VisionDive");
            var visionDive = visionDiveGo.transform;
            visionDive.position = new Vector3(0f, 0f, 250f);
            var visionFlashback = visionDiveGo.AddComponent<MemoryFlashbackController>();

            Ch8BuildVisionRooms(visionDive, out var visionEntryPointGo);
            visionDiveGo.SetActive(false);

            // x=6 clears the barrow mound's collider footprint (the sphere is centered at x=0 with a
            // ~4m radius) so the rig doesn't rematerialize embedded in it.
            var visionExitPointGo = new GameObject("VisionExitPoint");
            visionExitPointGo.transform.SetPositionAndRotation(new Vector3(6f, 1f, 68f), Quaternion.Euler(0f, 180f, 0f));

            var visionDiveControllerGo = new GameObject("VisionDiveController");
            var visionDiveController = visionDiveControllerGo.AddComponent<MemoryDiveController>();
            var vdSo = new SerializedObject(visionDiveController);
            SetObjectRef(vdSo, "diveRoot", visionDiveGo);
            SetObjectRef(vdSo, "diveEntryPoint", visionEntryPointGo.transform);
            SetObjectRef(vdSo, "diveExitPoint", visionExitPointGo.transform);
            SetObjectRef(vdSo, "rigRoot", rig.transform);
            SetObjectRef(vdSo, "flashback", visionFlashback);
            vdSo.ApplyModifiedPropertiesWithoutUndo();

            var enterVisionGo = new GameObject("EnterVisionTrigger");
            var enterVisionTrigger = enterVisionGo.AddComponent<MemoryDiveEntryTrigger>();
            var enterVisionSo = new SerializedObject(enterVisionTrigger);
            SetObjectRef(enterVisionSo, "dive", visionDiveController);
            enterVisionSo.ApplyModifiedPropertiesWithoutUndo();
            enterVisionGo.SetActive(false);

            var exitVisionGo = new GameObject("ExitVisionTrigger");
            var exitVisionTrigger = exitVisionGo.AddComponent<MemoryDiveExitTrigger>();
            var exitVisionSo = new SerializedObject(exitVisionTrigger);
            SetObjectRef(exitVisionSo, "dive", visionDiveController);
            exitVisionSo.ApplyModifiedPropertiesWithoutUndo();
            exitVisionGo.SetActive(false);

            // ---- Reach points. ----
            var gravePathReachGo = new GameObject("GravePathReachPoint");
            gravePathReachGo.transform.position = new Vector3(0f, 1f, 25f);
            var deepGardenReachGo = new GameObject("DeepGardenReachPoint");
            deepGardenReachGo.transform.position = new Vector3(0f, 1f, 60f);
            var gateReturnReachGo = new GameObject("GateReturnReachPoint");
            gateReturnReachGo.transform.position = new Vector3(0f, 1f, 12f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch8BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch8_beat0_briefing", talkRef);
            var dlgGate = Ch8BuildDialogue("Dialogue_Beat1_Gate", new Vector3(0f, 1f, 9f), "ch8_beat1_gate", talkRef);
            var dlgAlone = Ch8BuildDialogue("Dialogue_Beat1_Alone", new Vector3(0f, 1f, 15f), "ch8_beat1_alone", talkRef);
            var dlgRiddlePose = Ch8BuildDialogue("Dialogue_Beat2_RiddlePose", new Vector3(0f, 1f, 30f), "ch8_beat2_riddle_pose", talkRef);
            var dlgWrongBark = Ch8BuildDialogue("Dialogue_Beat2_WrongBark", new Vector3(0f, 1f, 36f), "ch8_beat2_wrong_bark", talkRef);
            var dlgRiddleAnswer = Ch8BuildDialogue("Dialogue_Beat2_RiddleAnswer", new Vector3(0f, 1f, 36f), "ch8_beat2_riddle_answer", talkRef);
            var dlgWardenIntro = Ch8BuildDialogue("Dialogue_Beat3_WardenIntro", new Vector3(0f, 1f, 60f), "ch8_beat3_warden_intro", talkRef);
            var dlgWardenDefeat = Ch8BuildDialogue("Dialogue_Beat3_WardenDefeat", new Vector3(0f, 1f, 66f), "ch8_beat3_warden_defeat", talkRef);
            var dlgNaming = Ch8BuildDialogue("Dialogue_Beat4_Naming", new Vector3(0f, 1f, 68f), "ch8_beat4_naming", talkRef);
            var dlgVisionA = Ch8BuildDialogue("Dialogue_Beat4_VisionA", visionDive.position + new Vector3(0f, 1f, 5f), "ch8_beat4_vision_a", talkRef);
            var dlgVisionB = Ch8BuildDialogue("Dialogue_Beat4_VisionB", visionDive.position + new Vector3(0f, 1f, 22f), "ch8_beat4_vision_b", talkRef);
            var dlgAftermath = Ch8BuildDialogue("Dialogue_Beat4_Aftermath", new Vector3(0f, 1f, 69f), "ch8_beat4_aftermath", talkRef);
            var dlgLeaving = Ch8BuildDialogue("Dialogue_Beat5_Leaving", new Vector3(0f, 1f, 70f), "ch8_beat5_leaving", talkRef);
            var dlgReunion = Ch8BuildDialogue("Dialogue_Beat5_Reunion", new Vector3(0f, 1f, 11f), "ch8_beat5_reunion", talkRef);

            // ---- The wrong-answer wave spawner: built active-idle (Ch4's HunterWave lesson: an
            // inactive spawner can't StartCoroutine) — Begin() only runs once, wired below to
            // RiddleTrial.onWrongAnswer, mirroring Ch6's ActivationRelay-style "one activation, one
            // Begin()" wiring. triggerRadius is generous because the player is already standing at the
            // wrong pad the instant onWrongAnswer fires. ----
            var buriedWaves = new List<List<Health>> { buriedEnemies.ConvertAll(go => go.GetComponent<Health>()) };
            var buriedSpawner = BuildWaveSpawner("BuriedWaveSpawner", new Vector3(0f, 0f, 38f), 10f, buriedWaves, new[] { dlgWrongBark });

            // ---- Chapter-complete canvas (worldspace) + outro driver. NO AbilityGranter (Ch8 grants
            // no new ability) and NO ally-recruit flag (no ally is recruited this chapter). ----
            var completeCanvasGo = Ch8BuildCompleteCanvas(new Vector3(0f, 1.4f, 13f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 13f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch8_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 8 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 22;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Gate of Fog (the crew held)", dlgGate);
            AuthorDialogueStep(steps, n++, "Beat1: Alone (Echo, then the Mourners' rules)", dlgAlone);
            AuthorReachStep(steps, n++, "ReachTrigger: The Grave-Paths", gravePathReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Fog's Question (the riddle pose)", dlgRiddlePose);
            AuthorPromptStep(steps, n++, "Prompt: Answer the Riddle (RiddleTrial gate)", trialGo);
            AuthorDialogueStep(steps, n++, "Beat2: True (the answer, the trial ends)", dlgRiddleAnswer);
            AuthorReachStep(steps, n++, "ReachTrigger: The Deep Garden", deepGardenReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat3: The Warden (boss intro)", dlgWardenIntro);
            AuthorDefeatStep(steps, n++, "Beat3: The Warden (boss)", new List<Object> { wardenEnemy.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat3: Down It Goes (boss defeated)", dlgWardenDefeat);
            AuthorTriggerStep(steps, n++, "Trigger: The Mourners Manifest", mournersRingGo);
            AuthorDialogueStep(steps, n++, "Beat4: The One Who Defies (the naming)", dlgNaming);
            AuthorTriggerStep(steps, n++, "Trigger: Enter the Vision", enterVisionGo);
            AuthorDialogueStep(steps, n++, "Beat4: Vision A (the faceless hand)", dlgVisionA);
            AuthorDialogueStep(steps, n++, "Beat4: Vision B (Khall, the moment of the switch)", dlgVisionB);
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Vision", exitVisionGo);
            AuthorDialogueStep(steps, n++, "Beat4: The Aftermath (I couldn't see her)", dlgAftermath);
            AuthorDialogueStep(steps, n++, "Beat5: The Leaving (the Mourners' farewell)", dlgLeaving);
            AuthorReachStep(steps, n++, "ReachTrigger: The Gate (the way back)", gateReturnReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat5: The Reunion (the crew, the report)", dlgReunion);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The riddle trial drives both the guardian punishment and the mission's own advance.
            UnityEventTools.AddPersistentListener(riddleTrial.onWrongAnswer, new UnityEngine.Events.UnityAction(buriedSpawner.Begin));
            UnityEventTools.AddPersistentListener(riddleTrial.onRightAnswer, new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, garden/barrow ambience beds, console/mood lights. ----
            BuildAmbienceLayer("SilentGardenWindAmbience", new Vector3(0f, 2.2f, 10f), 6f, 26f, 0.4f);
            BuildAmbienceLayer("BarrowDreadAmbience", new Vector3(-4f, 2.6f, 66f), 5f, 18f, 0.4f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();
            AddConsoleFlicker("TrialLight", seed: 88f);
            AddAmbientPulse("BarrowLight0", periodSeconds: 6.6f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch8ScenePath);
            EnsureScenesInBuild(Ch8ScenePath);

            Debug.Log($"[Space Samurai] Chapter 8 built at {Ch8ScenePath}. " +
                      "The Cairn briefing (voice-only) -> the gate of fog (crew held, Ronin alone with " +
                      "Echo) -> the grave-paths (RiddleTrial: wrong wakes the buried, true opens the way) " +
                      "-> the deep garden (the Warden boss, the weakpoint-sight showcase fight) -> the " +
                      "still center (the Mourners name him 'the one who defies') -> the vision dive (the " +
                      "faceless hand; Khall's grief and doubt, Ladder D rung 2) -> the leaving -> the gate " +
                      "(crew reunion). 22 mission steps. No new player ability. The-Warden.prefab and " +
                      "The-Mourners.prefab resolve via PlaceholderCharacterBuilder's existing specs.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch7EnsureScavengerDefinition). ----

        private static EnemyDefinition Ch8EnsureBuriedDefinition()
        {
            const string path = DataFolder + "/Ch8Buried.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 45f;
            def.damage = 9f;
            def.moveSpeed = 1.3f;
            def.attackCooldown = 0.85f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch8EnsureWardenDefinition()
        {
            const string path = DataFolder + "/Ch8Warden.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 340f;
            def.damage = 26f;
            def.moveSpeed = 0.9f;
            def.attackCooldown = 1.1f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch8 voice clips ourselves. ----

        private static DialoguePlayer Ch8BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter8Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch8WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter8] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch8WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter8Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch8VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch8VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Grave-paths set-dressing + the riddle trial's answer pads. ----

        /// <summary>Headstone props scattered along both sides of the path between <paramref name="zStart"/>
        /// and <paramref name="zEnd"/>, deterministically jittered (no UnityEngine.Random, for
        /// reproducible builds) rather than laid out in a perfectly even row.</summary>
        private static void Ch8ScatterHeadstones(Transform parent, float zStart, float zEnd, float spacing)
        {
            var stoneColor = new Color(0.35f, 0.36f, 0.38f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                float t = (z - zStart) / spacing;
                float jitterA = Mathf.Sin(t * 2.3f) * 2.5f;
                float jitterB = Mathf.Cos(t * 1.7f) * 2.5f;
                BuildProp(parent, "Headstone", new Vector3(-4f - jitterA, 0.4f, z), new Vector3(0.5f, 0.8f, 0.2f), stoneColor);
                BuildProp(parent, "Headstone", new Vector3(4f + jitterB, 0.4f, z), new Vector3(0.5f, 0.8f, 0.2f), stoneColor);
            }
        }

        /// <summary>A labeled headstone pad for the riddle trial: a visible stone + floating label, plus
        /// a plain anchor Transform (returned) at head height for <see cref="RiddleTrial"/>'s
        /// answerPoints to poll against.</summary>
        private static Transform Ch8BuildAnswerPad(Transform parent, string name, Vector3 pos, string label, Color color)
        {
            BuildProp(parent, name + "_Stone", pos + new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 1f, 0.25f), color);

            var labelGo = new GameObject(name + "_Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = pos + new Vector3(0f, 1.3f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.02f;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.color = new Color(0.85f, 0.87f, 0.9f);

            var pointGo = new GameObject(name + "_AnswerPoint");
            pointGo.transform.SetParent(parent, false);
            pointGo.transform.localPosition = pos + new Vector3(0f, 1f, 0f);
            return pointGo.transform;
        }

        // ---- The Warden: a master boss built from a Named-mesh prefab (no combat rig of its own),
        // mirrors Ch6BuildMasterEnemy/Ch7BuildMindspaceBoss. ----

        private static Enemy Ch8BuildWarden(Vector3 pos, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(Ch8WardenPrefab, pos, "The Warden");
            FitNamedCharacter(go);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the approach (-Z)

            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1.2f, 0f);
            cc.height = 2.6f;
            cc.radius = 0.55f;

            go.AddComponent<Health>();
            var bodyRenderer = go.GetComponentInChildren<Renderer>();

            var armRGo = new GameObject("ArmR");
            armRGo.transform.SetParent(go.transform, false);
            armRGo.transform.localPosition = new Vector3(0.4f, 1.6f, 0f);
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(armRGo.transform, false);
            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(swordGo.transform, false);
            var bladeTipGo = new GameObject("BladeTip");
            bladeTipGo.transform.SetParent(bladeGo.transform, false);
            bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.6f);

            var enemy = go.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "weapon", armRGo.transform);
            SetObjectRef(so, "bladeTip", bladeTipGo.transform);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            SetObjectRef(so, "target", playerHealth);
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        // ---- The Mourners: a decorative ring around the barrow, inactive until the reveal's Trigger step. ----

        private static GameObject Ch8BuildMournersRing(Transform parent, Vector3 center, float radius, int count)
        {
            var ringGo = new GameObject("MournersRing");
            ringGo.transform.SetParent(parent, true);
            ringGo.transform.position = center;

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                var go = InstantiateNpc(Ch8MournersPrefab, pos, $"Mourner_{i}");
                if (go == null) continue;
                FitNamedCharacter(go);
                go.transform.SetParent(ringGo.transform, true);
                go.transform.LookAt(new Vector3(center.x, go.transform.position.y, center.z));
            }
            ringGo.SetActive(false);
            return ringGo;
        }

        // ---- The vision dive: two rooms under one MemoryDiveController island (see the class summary's
        // VISION-DIVE DECISION). ----

        private static void Ch8BuildVisionRooms(Transform diveRoot, out GameObject entryPointGo)
        {
            var whiteFloor = new Color(0.75f, 0.76f, 0.78f);
            var whiteCeil = new Color(0.85f, 0.86f, 0.88f);
            var greyFloor = new Color(0.22f, 0.23f, 0.26f);
            var greyCeil = new Color(0.13f, 0.14f, 0.16f);
            var greyProp = new Color(0.3f, 0.31f, 0.34f);
            var ghostMat = MemoryFlashbackController.MakeGhostMaterial();

            // Vision A: the sterile room (local z[0,10]). Deliberately no figure for the woman herself —
            // the vision never finds her face, so nothing but the room and the table is staged.
            BuildFloorCeiling(diveRoot, "VisionA_Room", new Vector3(0f, 0f, 5f), new Vector3(8f, 0f, 10f), whiteFloor, whiteCeil);
            BuildWall(diveRoot, "VisionA_WallW", new Vector3(-4f, RoomH / 2f, 5f), new Vector3(0.2f, RoomH, 10f));
            BuildWall(diveRoot, "VisionA_WallE", new Vector3(4f, RoomH / 2f, 5f), new Vector3(0.2f, RoomH, 10f));
            BuildWall(diveRoot, "VisionA_WallS", new Vector3(0f, RoomH / 2f, 0f), new Vector3(8f, RoomH, 0.2f));
            BuildProp(diveRoot, "VisionA_Table", new Vector3(0f, 0.4f, 7f), new Vector3(1.6f, 0.8f, 0.7f), new Color(0.9f, 0.9f, 0.92f));
            Ch8BuildVisionLight(diveRoot, "VisionA_Light", new Vector3(0f, 2.4f, 7f), new Color(0.95f, 0.96f, 1f), 1.8f);

            // Vision B: Khall's ship, the handler's bay (local z[15,30]) — mirrors Ch3's playback-bay
            // ghost-cast staging (MakeGhostMaterial) for the same "recording, not reality" read.
            BuildFloorCeiling(diveRoot, "VisionB_Bay", new Vector3(0f, 0f, 22f), new Vector3(8f, 0f, 14f), greyFloor, greyCeil);
            BuildWall(diveRoot, "VisionB_WallW", new Vector3(-4f, RoomH / 2f, 22f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(diveRoot, "VisionB_WallE", new Vector3(4f, RoomH / 2f, 22f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(diveRoot, "VisionB_WallN", new Vector3(0f, RoomH / 2f, 29f), new Vector3(8f, RoomH, 0.2f));
            BuildProp(diveRoot, "VisionB_Console", new Vector3(0f, 0.55f, 26.5f), new Vector3(1.4f, 1.1f, 0.6f), greyProp);
            Ch8BuildGhostFigure(diveRoot, "Ghost_FootageRonin", new Vector3(0f, 0f, 20f), 1.8f, ghostMat);

            var khallGhostGo = InstantiateNpc(Ch8KhallPrefab, diveRoot.position + new Vector3(0f, 0f, 25f), "Ghost_Khall");
            khallGhostGo.transform.SetParent(diveRoot, true);
            FitNamedCharacter(khallGhostGo);
            khallGhostGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the entry (-Z)
            foreach (var r in khallGhostGo.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = ghostMat;

            Ch8BuildVisionLight(diveRoot, "VisionB_Light", new Vector3(0f, 2.4f, 22f), new Color(0.55f, 0.6f, 0.7f), 1.2f);

            // Placed INSIDE VisionA's bounds (room spans local z[0,10]) — VisionA_WallS at z=0 is a
            // solid collider (BuildWall always keeps one), so an entry point outside it would spawn the
            // rig embedded in the wall instead of the room.
            entryPointGo = new GameObject("VisionEntryPoint");
            entryPointGo.transform.SetParent(diveRoot, false);
            entryPointGo.transform.SetPositionAndRotation(new Vector3(0f, 1f, 2f), Quaternion.identity);
        }

        /// <summary>A still ghost silhouette: a capsule (torso) + sphere (head) in the shared ghost
        /// material, colliders stripped — memory cast, never an obstacle, never a threat. Mirrors
        /// Chapter3's Ch3BuildGhostFigure.</summary>
        private static GameObject Ch8BuildGhostFigure(Transform parent, string name, Vector3 pos, float height, Material ghostMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            float torsoH = height * 0.42f; // capsule default height is 2 at scaleY=1
            body.transform.localPosition = new Vector3(0f, torsoH, 0f);
            body.transform.localScale = new Vector3(height * 0.22f, torsoH, height * 0.22f);
            body.GetComponent<Renderer>().sharedMaterial = ghostMat;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, height * 0.9f, 0f);
            head.transform.localScale = Vector3.one * height * 0.16f;
            head.GetComponent<Renderer>().sharedMaterial = ghostMat;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            return root;
        }

        /// <summary>A cheap shadowless point light parented under the dive root (BuildAccentPointLight
        /// creates standalone scene objects, which would light the vision even while it's inactive).</summary>
        private static void Ch8BuildVisionLight(Transform parent, string name, Vector3 localPos, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = 12f;
            l.shadows = LightShadows.None;
        }

        /// <summary>A worldspace "CHAPTER 8 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch8BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 8 COMPLETE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 220f);
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "CHAPTER 8 COMPLETE";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 54;
            label.color = new Color(0.9f, 0.92f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            canvasGo.SetActive(false);
            return canvasGo;
        }
    }
}

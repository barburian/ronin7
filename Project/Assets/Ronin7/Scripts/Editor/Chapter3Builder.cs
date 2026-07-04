using Ronin7.Core;
using Ronin7.Editor.Art;
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
    /// Chapter 3 ("The Sword Remembers") scene builder. One scene, two environments that could not be
    /// less alike: the Cairn's hold running quiet after Velorum (warm, domestic, the crew's council and
    /// the katana's wake), and — offset far up +Z — THE PLAYBACK: a desaturated recording of Ronin-7's
    /// erased last mission on Kethel-7, entered via <see cref="MemoryDiveController"/> and patrolled by
    /// <see cref="RedactionSentinel"/>s (stealth: being seen resets you to the dive entry). No boss, no
    /// combat — traversal + stealth + story. Ends on the naming beat (Shadow becomes Echo,
    /// "ch3_echo_named") and the chapter outro (ch3_complete + ZoneCompleted).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> /
    /// <c>Chapter2Builder</c> so it reuses their geometry/dialogue/mission-step helpers directly. All
    /// chapter-local helpers are prefixed <c>Ch3</c>.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch3ScenePath = SceneFolder + "/Ch03_SwordRemembers.unity";

        // Voice clips/SFX live alongside Ch1/Ch2's (each chapter wires its own copies of these
        // folder constants rather than sharing them).
        private const string Ch3VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        // Named-cast prefabs (Tripo image->3D pipeline, grounded via FitNamedCharacter — same
        // convention Chapter1/Chapter2 use).
        private const string Ch3KesslerPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab";
        private const string Ch3IrisPrefab    = "Assets/Ronin7/Art/Generated/Characters3D/Named/Iris.prefab";
        private const string Ch3ReshPrefab    = "Assets/Ronin7/Art/Generated/Characters3D/Named/Resh.prefab";
        private const string Ch3MiraPrefab    = "Assets/Ronin7/Art/Generated/Characters3D/Named/Mira.prefab";
        private const string Ch3KhallPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab";
        private const string Ch3EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 03 — The Sword Remembers", priority = 203)]
        public static void BuildChapter3SwordRemembers()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets,
            // so references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();

            // ---- Lighting: running-quiet twilight. Dim warm key + low warm ambient — the safe,
            // domestic hold the wake is about to make strange. The playback overrides this via
            // MemoryFlashbackController on EnterDive and MemoryDiveController restores it on exit. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.85f, 0.65f);
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.10f);

            // Quiet warm hold haze — calm, barely-there.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.17f, 0.14f, 0.11f);
            RenderSettings.fogDensity = 0.018f;

            BuildAccentPointLight("HoldLampBench", new Vector3(-3f, 1.6f, 2f), new Color(1f, 0.8f, 0.55f), 1.6f, 8f);   // Iris's single work lamp
            BuildAccentPointLight("HoldLampSeat", new Vector3(0f, 2.4f, 8f), new Color(1f, 0.75f, 0.5f), 1.2f, 9f);     // the playback seat corner
            BuildAccentPointLight("HoldLampRack", new Vector3(4f, 2.2f, 6f), new Color(0.85f, 0.8f, 0.7f), 1f, 7f);     // the weapon rack

            // ---- The Cairn hold: one compact warm room. x[-5,5], z[-4,10]. Solid walls, no doors —
            // nothing boards the ship this chapter; the threat is already in the room. ----
            var holdGo = new GameObject("CairnHold");
            var hold = holdGo.transform;
            var floorColor = new Color(0.2f, 0.17f, 0.14f);
            var ceilColor = new Color(0.1f, 0.09f, 0.08f);

            BuildFloorCeiling(hold, "Hold", new Vector3(0f, 0f, 3f), new Vector3(10f, 0f, 14f), floorColor, ceilColor);
            BuildWall(hold, "Hold_WallW", new Vector3(-5f, RoomH / 2f, 3f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(hold, "Hold_WallE", new Vector3(5f, RoomH / 2f, 3f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(hold, "Hold_WallFront", new Vector3(0f, RoomH / 2f, -4f), new Vector3(10f, RoomH, 0.2f));
            BuildWall(hold, "Hold_WallBack", new Vector3(0f, RoomH / 2f, 10f), new Vector3(10f, RoomH, 0.2f));
            BuildRoomDetails(hold, "Hold", new Vector3(0f, 0f, 3f), new Vector2(5f, 7f), new Color(0.38f, 0.3f, 0.24f));
            Ch3BuildHoldStory(hold);

            // ---- The Playback: Kethel-7's last mission as failing footage, offset far up +Z (z 40-80).
            // All of it under one inactive diveRoot; MemoryDiveController activates it on EnterDive. ----
            var diveRootGo = new GameObject("Playback_Kethel7");
            var diveRoot = diveRootGo.transform;
            var flashback = diveRootGo.AddComponent<MemoryFlashbackController>(); // fog/ambient grey-out on activation
            Ch3BuildPlayback(diveRoot, out var diveEntryPointGo);
            diveRootGo.SetActive(false);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig (head + hands), locomotion, bounds, EchoPresence. ----
            var rig = BuildRig(refs, addLocomotion: true);
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 38f); // covers the hold AND the playback island
            bounds.radius = 55f;
            var rigHead = rig.GetComponentInChildren<Camera>(true).transform;

            // The rack katana IS the player sword: Ronin's blade "Echo" resting on the bulkhead rack
            // (Beat 1 — "he crosses to the weapon rack and reaches for the katana"). Blade laid along
            // the rack's long axis (+Z, along the east wall) at rack height.
            BuildSword(new Vector3(4.3f, 1.2f, 6f), Quaternion.identity, weapon, Ch3EchoBladePrefab);

            // ---- The crew, gathered in the hold for the council. ----
            Ch3PlaceStoryNpc(Ch3KesslerPrefab, new Vector3(-1.6f, 0f, 4.5f), "Kessler", wanderRadius: 0.7f);
            Ch3PlaceStoryNpc(Ch3IrisPrefab, new Vector3(-3.2f, 0f, 2.6f), "Iris", wanderRadius: 0f);   // stays at her bench
            Ch3PlaceStoryNpc(Ch3ReshPrefab, new Vector3(2f, 0f, 3f), "Resh", wanderRadius: 0.8f);
            Ch3PlaceStoryNpc(Ch3MiraPrefab, new Vector3(1.2f, 0f, 0.5f), "Mira", wanderRadius: 1f);    // the stowaway, underfoot

            // ---- Memory dive controller + entry/exit trigger objects (Trigger steps SetActive them;
            // their OnEnable drives EnterDive/ExitDive — MissionDirector never learns about dives). ----
            var diveGo = new GameObject("MemoryDive");
            var dive = diveGo.AddComponent<MemoryDiveController>();
            // Exit anchor: the playback seat corner of the hold, facing back into the room (-Z-ish).
            var exitPointGo = new GameObject("DiveExitPoint");
            exitPointGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, 8f), Quaternion.Euler(0f, 180f, 0f));
            var diveSo = new SerializedObject(dive);
            SetObjectRef(diveSo, "diveRoot", diveRootGo);
            SetObjectRef(diveSo, "diveEntryPoint", diveEntryPointGo.transform);
            SetObjectRef(diveSo, "diveExitPoint", exitPointGo.transform);
            SetObjectRef(diveSo, "rigRoot", rig.transform);
            SetObjectRef(diveSo, "flashback", flashback);
            diveSo.ApplyModifiedPropertiesWithoutUndo();

            var enterDiveGo = new GameObject("EnterDiveTrigger");
            var enterTrigger = enterDiveGo.AddComponent<MemoryDiveEntryTrigger>();
            var enterSo = new SerializedObject(enterTrigger);
            SetObjectRef(enterSo, "dive", dive);
            enterSo.ApplyModifiedPropertiesWithoutUndo();
            enterDiveGo.SetActive(false);

            var exitDiveGo = new GameObject("ExitDiveTrigger");
            var exitTrigger = exitDiveGo.AddComponent<MemoryDiveExitTrigger>();
            var exitSo = new SerializedObject(exitTrigger);
            SetObjectRef(exitSo, "dive", dive);
            exitSo.ApplyModifiedPropertiesWithoutUndo();
            exitDiveGo.SetActive(false);

            // ---- Redaction sentinels: the conditioning made manifest, patrolling the footage. Being
            // seen resets the witness to the dive entry (no game-over — pushed out of the record). ----
            Ch3BuildRedactionSentinel(diveRoot, "Redaction_Corridor",
                new Vector3(-2.4f, 0f, 49f), new Vector3(2.4f, 0f, 49f), rigHead, dive);
            Ch3BuildRedactionSentinel(diveRoot, "Redaction_BayApproach",
                new Vector3(-3f, 0f, 66f), new Vector3(3f, 0f, 66f), rigHead, dive);

            // ---- Reach points. ----
            var rackReachGo = new GameObject("RackReachPoint");
            rackReachGo.transform.position = new Vector3(4f, 1f, 6f);
            var thresholdReachGo = new GameObject("ThresholdReachPoint");
            thresholdReachGo.transform.position = new Vector3(0f, 1f, 52.5f);
            var aftermathReachGo = new GameObject("AftermathReachPoint");
            aftermathReachGo.transform.position = new Vector3(0f, 1f, 61f);
            var bayReachGo = new GameObject("BayReachPoint");
            bayReachGo.transform.position = new Vector3(0f, 1f, 73f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch3BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 3f), "ch3_beat0_briefing", talkRef);
            var dlgBonding = Ch3BuildDialogue("Dialogue_Beat1_Bonding", new Vector3(3.5f, 1f, 6f), "ch3_beat1_bonding", talkRef);
            var dlgExplains = Ch3BuildDialogue("Dialogue_Beat1_ShadowExplains", new Vector3(2f, 1f, 7f), "ch3_beat1_shadow_explains", talkRef);
            var dlgThreshold = Ch3BuildDialogue("Dialogue_Beat2_Threshold", new Vector3(0f, 1f, 53f), "ch3_beat2_threshold", talkRef);
            var dlgAftermath = Ch3BuildDialogue("Dialogue_Beat2_Aftermath", new Vector3(0f, 1f, 62f), "ch3_beat2_aftermath", talkRef);
            var dlgExecution = Ch3BuildDialogue("Dialogue_Beat2_Execution", new Vector3(0f, 1f, 74f), "ch3_beat2_execution", talkRef);
            var dlgBurndown = Ch3BuildDialogue("Dialogue_Beat2_Burndown", new Vector3(0f, 1f, 75f), "ch3_beat2_burndown", talkRef);
            var dlgNaming = Ch3BuildDialogue("Dialogue_Beat3_Naming", new Vector3(0f, 1f, 8f), "ch3_beat3_naming", talkRef);
            var dlgDebrief = Ch3BuildDialogue("Dialogue_Beat3_Debrief", new Vector3(0f, 1f, 5f), "ch3_beat3_debrief", talkRef);

            // ---- Grip prompt (the wake moment), advanced by input via PromptInputAdvancer. ----
            var gripPromptGo = Ch3BuildPrompt(new Vector3(4f, 1.6f, 6f));

            // ---- Naming flag: a Trigger step activates this and its OnEnable sets "ch3_echo_named"
            // (setOnEnable opt-in — see CampaignFlagSetter). No ability this chapter; the naming beat
            // is a story flag, and EchoPresence's pools gate on ch3_complete. ----
            var namedFlagGo = new GameObject("EchoNamedFlag");
            var namedFlag = namedFlagGo.AddComponent<CampaignFlagSetter>();
            var namedFlagSo = new SerializedObject(namedFlag);
            var namedFlagsProp = namedFlagSo.FindProperty("flags");
            namedFlagsProp.arraySize = 1;
            namedFlagsProp.GetArrayElementAtIndex(0).stringValue = "ch3_echo_named";
            namedFlagSo.FindProperty("setOnEnable").boolValue = true;
            namedFlagSo.ApplyModifiedPropertiesWithoutUndo();
            namedFlagGo.SetActive(false);

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = Ch3BuildCompleteCanvas(new Vector3(0f, 1.4f, 9f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 8f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch3_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            // publishZoneCompleted defaults to true on ChapterOutro — scene-keyed completion, same as
            // every chapter finale.
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 3 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var advancer = gripPromptGo.GetComponent<PromptInputAdvancer>();
            var advancerSo = new SerializedObject(advancer);
            SetObjectRef(advancerSo, "mission", missionDirector);
            if (talkRef != null) SetObjectRef(advancerSo, "advanceAction", talkRef);
            advancerSo.ApplyModifiedPropertiesWithoutUndo();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 18;

            AuthorDialogueStep(steps, n++, "Beat0: The Council (a heading, not yet a wake)", dlgBriefing);
            AuthorReachStep(steps, n++, "ReachTrigger: The Weapon Rack", rackReachGo.transform, 2.5f);
            AuthorPromptStep(steps, n++, "Prompt: Grip the Katana (the wake)", gripPromptGo);
            AuthorDialogueStep(steps, n++, "Beat1: The Bonding (the voice only he hears)", dlgBonding);
            AuthorDialogueStep(steps, n++, "Beat1: What the Shadow Is (the offer)", dlgExplains);
            AuthorTriggerStep(steps, n++, "Trigger: Enter the Playback (dive in)", enterDiveGo);
            AuthorReachStep(steps, n++, "ReachTrigger: The Threshold (past the corridor redaction)", thresholdReachGo.transform, 3.5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Threshold (the refusal)", dlgThreshold);
            AuthorReachStep(steps, n++, "ReachTrigger: The Aftermath", aftermathReachGo.transform, 3.5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Aftermath (grief in empty space)", dlgAftermath);
            AuthorReachStep(steps, n++, "ReachTrigger: Khall's Bay (deepest point)", bayReachGo.transform, 3.5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Execution (Khall's 'I know')", dlgExecution);
            AuthorDialogueStep(steps, n++, "Beat2: The Burn-Down (the charge)", dlgBurndown);
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Playback (surface)", exitDiveGo);
            AuthorDialogueStep(steps, n++, "Beat3: The Naming (Shadow becomes Echo)", dlgNaming);
            AuthorTriggerStep(steps, n++, "Trigger: Set ch3_echo_named", namedFlagGo);
            AuthorDialogueStep(steps, n++, "Beat3: The Debrief (a target, not an answer)", dlgDebrief);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, hold + playback ambience beds, console/mood lights. ----
            BuildAmbienceLayer("HoldAmbience", new Vector3(0f, 1.5f, 3f), 3f, 10f, 0.4f);
            BuildAmbienceLayer("PlaybackDreadAmbience", new Vector3(0f, 1.5f, 62f), 3f, 12f, 0.4f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();
            AddConsoleFlicker("HoldLampBench", seed: 33f);
            AddAmbientPulse("HoldLampSeat", periodSeconds: 6f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch3ScenePath);
            EnsureScenesInBuild(Ch3ScenePath);

            Debug.Log($"[Space Samurai] Chapter 3 built at {Ch3ScenePath}. " +
                      "Two environments: Cairn hold (council, rack katana wake) + Kethel-7 playback " +
                      "(z 40-80, inactive until the dive): Threshold -> Aftermath -> Khall's Bay, with " +
                      "2 RedactionSentinels (spotted = reset to dive entry, no game-over). 18 mission " +
                      "steps, no combat. Naming beat sets ch3_echo_named; outro sets ch3_complete.");
        }

        // ---- Dialogue: build via the shared helper, then wire ch3 voice clips ourselves. ----

        private static DialoguePlayer Ch3BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter3Lines.Get(setId);
            // clipSetId left null so the shared loader does not spam missing-clip warnings for the wrong folder.
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch3WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter3] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch3WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter3Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch3VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch3VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement (mirrors Chapter2's StoryNpc + optional wander wiring). ----

        private static GameObject Ch3PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName, float wanderRadius)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            FitNamedCharacter(go);
            if (go == null) return null;

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            if (wanderRadius > 0f)
            {
                var wander = go.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = wanderRadius;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        // ---- The playback: Kethel-7 as failing footage (all under the inactive diveRoot). ----

        /// <summary>
        /// Threshold corridor (z 40-56) -> Aftermath room (z 56-68) -> Khall's Bay (z 68-80), all in
        /// scrubbed greys with ghost-tinted memory cast (MakeGhostMaterial). Respectful staging: the
        /// caretaker and children are still silhouettes at the threshold; the aftermath room is EMPTY —
        /// the massacre is carried by dialogue and vacancy, never shown.
        /// </summary>
        private static void Ch3BuildPlayback(Transform diveRoot, out GameObject diveEntryPointGo)
        {
            var greyFloor = new Color(0.24f, 0.25f, 0.27f);
            var greyCeil = new Color(0.15f, 0.16f, 0.17f);
            var greyProp = new Color(0.3f, 0.31f, 0.33f);
            var ghostMat = MemoryFlashbackController.MakeGhostMaterial();

            // Threshold corridor: x[-3,3], z[40,56]. Door gap at the far end (the half-shut door).
            BuildFloorCeiling(diveRoot, "Playback_Corridor", new Vector3(0f, 0f, 48f), new Vector3(6f, 0f, 16f), greyFloor, greyCeil);
            BuildWall(diveRoot, "Corridor_WallW", new Vector3(-3f, RoomH / 2f, 48f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(diveRoot, "Corridor_WallE", new Vector3(3f, RoomH / 2f, 48f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(diveRoot, "Corridor_WallFront", new Vector3(0f, RoomH / 2f, 40f), new Vector3(6f, RoomH, 0.2f));
            BuildDoorwayWall(diveRoot, "Corridor_WallBack", new Vector3(0f, RoomH / 2f, 56f), 6f, true, 1.6f);

            // "Frames that drop and repeat": misaligned static blocks along the corridor walls.
            BuildProp(diveRoot, "StaticBlock0", new Vector3(-2.6f, 1.6f, 44f), new Vector3(0.3f, 1.2f, 0.8f), greyProp);
            BuildProp(diveRoot, "StaticBlock1", new Vector3(2.6f, 0.8f, 47f), new Vector3(0.3f, 1.6f, 0.6f), greyProp * 0.8f);
            BuildProp(diveRoot, "StaticBlock2", new Vector3(-2.5f, 2.2f, 51f), new Vector3(0.4f, 0.9f, 0.9f), greyProp * 1.15f);

            // The threshold cast: the caretaker (hands open, no threat) before the half-shut door,
            // the small shapes of the children beyond it. Still silhouettes, ghost-tinted.
            Ch3BuildGhostFigure(diveRoot, "Ghost_Caretaker", new Vector3(0f, 0f, 54f), 1.75f, ghostMat);
            Ch3BuildGhostFigure(diveRoot, "Ghost_Child0", new Vector3(-0.7f, 0f, 57f), 1.1f, ghostMat);
            Ch3BuildGhostFigure(diveRoot, "Ghost_Child1", new Vector3(0.6f, 0f, 57.4f), 1.0f, ghostMat);
            // The one color the footage keeps: the red kill-order glyph, pulsed still over the scene.
            var glyph = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glyph.name = "KillOrderGlyph";
            glyph.transform.SetParent(diveRoot, false);
            glyph.transform.localPosition = new Vector3(0f, 2.1f, 53.4f);
            glyph.transform.localScale = new Vector3(0.35f, 0.35f, 0.05f);
            glyph.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.9f, 0.1f, 0.08f));
            Object.DestroyImmediate(glyph.GetComponent<Collider>());

            // Aftermath room: x[-5,5], z[56,68]. Deliberately EMPTY — smoke-grey, nothing staged.
            BuildFloorCeiling(diveRoot, "Playback_Aftermath", new Vector3(0f, 0f, 62f), new Vector3(10f, 0f, 12f), greyFloor * 0.85f, greyCeil);
            BuildWall(diveRoot, "Aftermath_WallW", new Vector3(-5f, RoomH / 2f, 62f), new Vector3(0.2f, RoomH, 12f));
            BuildWall(diveRoot, "Aftermath_WallE", new Vector3(5f, RoomH / 2f, 62f), new Vector3(0.2f, RoomH, 12f));
            BuildDoorwayWall(diveRoot, "Aftermath_WallBack", new Vector3(0f, RoomH / 2f, 68f), 10f, true, 2f);

            // Khall's Bay: x[-4,4], z[68,80]. The footage is steadier here — cleaner walls, a console
            // between two ghosts: the footage-Ronin in mission-dirt, and Khall, hand at the switch.
            BuildFloorCeiling(diveRoot, "Playback_Bay", new Vector3(0f, 0f, 74f), new Vector3(8f, 0f, 12f), greyFloor, greyCeil);
            BuildWall(diveRoot, "Bay_WallW", new Vector3(-4f, RoomH / 2f, 74f), new Vector3(0.2f, RoomH, 12f));
            BuildWall(diveRoot, "Bay_WallE", new Vector3(4f, RoomH / 2f, 74f), new Vector3(0.2f, RoomH, 12f));
            BuildWall(diveRoot, "Bay_WallBack", new Vector3(0f, RoomH / 2f, 80f), new Vector3(8f, RoomH, 0.2f));
            BuildProp(diveRoot, "Bay_KillswitchConsole", new Vector3(0f, 0.55f, 74.5f), new Vector3(1.4f, 1.1f, 0.6f), greyProp);

            Ch3BuildGhostFigure(diveRoot, "Ghost_FootageRonin", new Vector3(0f, 0f, 72.8f), 1.8f, ghostMat);
            // Khall: the named prefab as a hologram-ghost if baked, else a silhouette.
            var khallGhostGo = InstantiateNpc(Ch3KhallPrefab, new Vector3(0f, 0f, 76.2f), "Ghost_Khall");
            khallGhostGo.transform.SetParent(diveRoot, true);
            FitNamedCharacter(khallGhostGo);
            foreach (var r in khallGhostGo.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = ghostMat;

            // Cold, drained accent lights, parented under the dive so they only exist while the
            // playback does (the flashback fog does most of the mood work).
            Ch3BuildPlaybackLight(diveRoot, "PlaybackLight_Corridor", new Vector3(0f, 2.6f, 48f), new Color(0.55f, 0.6f, 0.7f), 1.2f);
            Ch3BuildPlaybackLight(diveRoot, "PlaybackLight_Aftermath", new Vector3(0f, 2.6f, 62f), new Color(0.5f, 0.52f, 0.58f), 0.9f);
            Ch3BuildPlaybackLight(diveRoot, "PlaybackLight_Bay", new Vector3(0f, 2.6f, 74f), new Color(0.6f, 0.65f, 0.75f), 1.3f);

            // Dive entry/reset anchor, just inside the corridor mouth, facing up the recording (+Z).
            diveEntryPointGo = new GameObject("DiveEntryPoint");
            diveEntryPointGo.transform.SetParent(diveRoot, false);
            diveEntryPointGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, 42f), Quaternion.identity);
        }

        /// <summary>A cheap shadowless point light parented under the dive root (BuildAccentPointLight
        /// creates standalone scene objects, which would light the playback even while it's inactive).</summary>
        private static void Ch3BuildPlaybackLight(Transform parent, string name, Vector3 pos, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = 12f;
            l.shadows = LightShadows.None; // keep it cheap on Quest
        }

        /// <summary>A still ghost silhouette: a capsule (torso) + sphere (head) in the shared ghost
        /// material, colliders stripped — memory cast, never an obstacle, never a threat.</summary>
        private static GameObject Ch3BuildGhostFigure(Transform parent, string name, Vector3 pos, float height, Material ghostMat)
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

        /// <summary>A redaction sentinel: a faceless censor-slab of scrubbed static that ping-pongs
        /// between two waypoints; seeing the rig head fires the dive's ResetToEntry.</summary>
        private static void Ch3BuildRedactionSentinel(Transform diveRoot, string name,
            Vector3 waypointA, Vector3 waypointB, Transform rigHead, MemoryDiveController dive)
        {
            var root = new GameObject(name);
            root.transform.SetParent(diveRoot, false);
            root.transform.localPosition = waypointA;

            // Censor-shape: a dark slab with a lighter static band — geometric, faceless, not a person.
            var slabMat = MakeUnlitMaterial(new Color(0.08f, 0.08f, 0.1f));
            var bandMat = MakeUnlitMaterial(new Color(0.5f, 0.52f, 0.58f));
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "CensorSlab";
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            slab.transform.localScale = new Vector3(0.8f, 2.2f, 0.25f);
            slab.GetComponent<Renderer>().sharedMaterial = slabMat;
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "StaticBand";
            band.transform.SetParent(root.transform, false);
            band.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            band.transform.localScale = new Vector3(0.85f, 0.25f, 0.28f);
            band.GetComponent<Renderer>().sharedMaterial = bandMat;
            Object.DestroyImmediate(band.GetComponent<Collider>());

            var wpA = new GameObject("WP_A");
            wpA.transform.SetParent(diveRoot, false);
            wpA.transform.localPosition = waypointA;
            var wpB = new GameObject("WP_B");
            wpB.transform.SetParent(diveRoot, false);
            wpB.transform.localPosition = waypointB;

            var sentinel = root.AddComponent<RedactionSentinel>();
            var so = new SerializedObject(sentinel);
            SetObjectRef(so, "waypointA", wpA.transform);
            SetObjectRef(so, "waypointB", wpB.transform);
            SetObjectRef(so, "target", rigHead);
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(sentinel.OnSpotted,
                new UnityEngine.Events.UnityAction(dive.ResetToEntry));
        }

        // ---- Small scene-dressing / prompt helpers specific to Chapter 3. ----

        /// <summary>Cairn hold story dressing: Iris's bench with the single lamp, the drive stacks Resh
        /// sorts, Kessler's three cups, the bulkhead weapon rack, and the playback seat.</summary>
        private static void Ch3BuildHoldStory(Transform parent)
        {
            var worn = new Color(0.34f, 0.29f, 0.24f);
            var steel = new Color(0.4f, 0.42f, 0.46f);
            var cupColor = new Color(0.45f, 0.43f, 0.4f);

            // Iris's bench (west side), half-stripped board on it.
            BuildProp(parent, "IrisBench", new Vector3(-3.6f, 0.45f, 2.2f), new Vector3(1.8f, 0.9f, 0.8f), worn);
            BuildProp(parent, "StrippedBoard", new Vector3(-3.5f, 0.95f, 2.2f), new Vector3(0.5f, 0.05f, 0.35f), steel);

            // Resh's drive stacks (the copied vault files, being sorted).
            BuildProp(parent, "DriveStack0", new Vector3(2.4f, 0.15f, 2.4f), new Vector3(0.5f, 0.3f, 0.35f), steel * 0.8f);
            BuildProp(parent, "DriveStack1", new Vector3(2.9f, 0.1f, 2.6f), new Vector3(0.4f, 0.2f, 0.3f), steel * 0.9f);
            BuildProp(parent, "DriveStack2", new Vector3(2.6f, 0.42f, 2.45f), new Vector3(0.35f, 0.18f, 0.28f), steel);

            // Kessler's battered pot and three cups — two out of old habit, then a third.
            BuildProp(parent, "GalleyShelf", new Vector3(-1.5f, 0.45f, 8.8f), new Vector3(1.4f, 0.9f, 0.5f), worn * 0.9f);
            for (int i = 0; i < 3; i++)
            {
                var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cup.name = $"Cup_{i}";
                cup.transform.SetParent(parent, false);
                cup.transform.localPosition = new Vector3(-1.8f + i * 0.3f, 0.96f, 8.8f);
                cup.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
                TintShared(cup.GetComponent<Renderer>(), cupColor);
            }

            // The bulkhead weapon rack (east wall) — the katana rests on these pegs.
            BuildProp(parent, "WeaponRack_Back", new Vector3(4.85f, 1.2f, 6f), new Vector3(0.1f, 0.7f, 1.4f), worn);
            BuildProp(parent, "WeaponRack_PegA", new Vector3(4.6f, 1.12f, 5.6f), new Vector3(0.4f, 0.05f, 0.05f), steel);
            BuildProp(parent, "WeaponRack_PegB", new Vector3(4.6f, 1.12f, 6.4f), new Vector3(0.4f, 0.05f, 0.05f), steel);

            // The playback seat (back corner): where he settles for the dive, and surfaces after it.
            BuildProp(parent, "PlaybackSeat", new Vector3(0f, 0.3f, 8.6f), new Vector3(0.9f, 0.6f, 0.9f), worn);
            BuildProp(parent, "PlaybackSeat_Back", new Vector3(0f, 0.85f, 9.05f), new Vector3(0.9f, 0.9f, 0.15f), worn * 0.85f);
        }

        /// <summary>The grip prompt at the rack, carrying a PromptInputAdvancer. Created inactive.</summary>
        private static GameObject Ch3BuildPrompt(Vector3 position)
        {
            var go = new GameObject("GripKatanaPrompt");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.012f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = "Grip the Katana  (Y)";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = new Color(0.95f, 0.85f, 0.55f);
            go.AddComponent<PromptInputAdvancer>();
            go.SetActive(false);
            return go;
        }

        /// <summary>A worldspace "CHAPTER 3 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch3BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 3 COMPLETE Canvas");
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
            label.text = "CHAPTER 3 COMPLETE";
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

using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
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
    /// Chapter 1 ("The Salvager's Debt") hub scene builder. Builds the four-room salvage-ship interior
    /// faithful to the canonical Ch01 script: Revival Bay -> Main Hold (wreck-field viewport) ->
    /// Airlock corridor (the boarding) -> Command Room (the ultimatum / cracked viewscreen).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// can call all the shared private static helpers directly (geometry, doors, NPCs, dialogue, combat rigs).
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch1HubScenePath = SceneFolder + "/Galaxy1_Ch1_Hub.unity";
        private static readonly string Ch1HubSceneName = System.IO.Path.GetFileNameWithoutExtension(Ch1HubScenePath);

        // ch1 voice clips were authored in Phase 1-2 as mp3s here (NOT in the Audio/Voice folder the
        // shared BuildDialoguePlayer loader searches), so this builder wires their clips itself.
        private const string Ch1VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        // SFX live under Art/Generated/Audio (NOT the Assets/Ronin7/Audio path some older calls used).
        private const string Ch1AudioFolder = "Assets/Ronin7/Art/Generated/Audio";

        // The cast uses the existing 3D character prefabs (feet-pivot models, placed at y=0).
        private const string Ch1KesslerPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab";
        private const string Ch1KhallPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab";
        // Ronin's blade is the "Echo" katana mesh (a ~1 m model standing along +Y).
        private const string Ch1EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Chapter 1 (Fresh)", priority = 64)]
        public static void BuildChapter1Hub()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets,
            // so references held across it go fake-null and serialize as {fileID: 0} on every enemy.
            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            // ---- Lighting: cool directional key + low ambient fill, then per-room mood accents. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.82f, 0.88f, 1f);
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.19f);

            // Faint cold haze inside the dead leviathan — subtle, matches the cool ambient tone.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.16f, 0.17f, 0.20f);
            RenderSettings.fogDensity = 0.018f;

            BuildAccentPointLight("MedbayLight", new Vector3(0f, 2.6f, 0f), new Color(1f, 0.82f, 0.6f), 2.2f, 11f);   // warm revival bay
            BuildAccentPointLight("HoldLight", new Vector3(0f, 2.8f, 10f), new Color(0.72f, 0.82f, 0.95f), 1.8f, 16f); // neutral hold
            BuildAccentPointLight("AirlockLight", new Vector3(0f, 2.6f, 21f), new Color(0.6f, 0.78f, 1f), 1.5f, 12f);  // cool corridor
            BuildAccentPointLight("CommandLight", new Vector3(0f, 2.6f, 34f), new Color(0.5f, 0.82f, 1f), 2.4f, 16f);  // cold command

            // ---- Interior geometry. Linear +Z run: Revival Bay -> Hold -> Airlock -> Command. ----
            // Command room at z[26,42], x[-9,9] so BuildCommandWindshield aligns as the
            // cracked viewscreen.
            var interiorGo = new GameObject("ShipInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.19f, 0.21f, 0.25f);
            var ceilColor = new Color(0.11f, 0.12f, 0.15f);

            // Revival Bay: cramped wake room. x[-4,4], z[-4,4]. Door gap in back wall (z=4).
            BuildFloorCeiling(interior, "Medbay", new Vector3(0f, 0f, 0f), new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Medbay_WallW", new Vector3(-4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildWall(interior, "Medbay_WallE", new Vector3(4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildWall(interior, "Medbay_WallFront", new Vector3(0f, RoomH / 2f, -4f), new Vector3(8f, RoomH, 0.2f));
            BuildDoorwayWall(interior, "Medbay_WallBack", new Vector3(0f, RoomH / 2f, 4f), 8f, true, 2.4f);
            BuildRoomDetails(interior, "Medbay", new Vector3(0f, 0f, 0f), new Vector2(4f, 4f), new Color(0.5f, 0.55f, 0.6f));
            BuildMedbayProps(interior);
            BuildRevivalBayStory(interior); // the open casket Ronin woke from, IV rack, cutting tools, wires

            // Main Hold: largest space. x[-6,6], z[4,16]. Door gaps front (z=4) + back (z=16).
            BuildFloorCeiling(interior, "Hold", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 12f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Hold_WallFront", new Vector3(0f, RoomH / 2f, 4f), 12f, true, 2.4f);
            BuildDoorwayWall(interior, "Hold_WallBack", new Vector3(0f, RoomH / 2f, 16f), 12f, true, 2.4f);
            BuildWall(interior, "Hold_WallE", new Vector3(6f, RoomH / 2f, 10f), new Vector3(0.2f, RoomH, 12f));
            // West wall has a wreck-field viewport (running light is revealed through it later).
            var runningLight = BuildHoldViewport(interior);
            BuildRoomDetails(interior, "Hold", new Vector3(2f, 0f, 10f), new Vector2(3.5f, 5.5f), new Color(0.3f, 0.32f, 0.36f));
            BuildHoldStory(interior); // the two dented cups, graded salvage piles, the thin dirty star

            // Airlock corridor: x[-2,2], z[16,26]. Boarding point. Door gaps front (z=16) + back (z=26).
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 21f), new Vector3(4f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Airlock_WallW", -2f, 16f, 26f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Airlock_WallE", 2f, 16f, 26f, new float[0], 2.4f);
            BuildAirlockHatch(interior); // sealed outer dock hatch (set dressing) on the east wall
            BuildBoardingClamp(interior); // heavy clamp seated on the outer hatch — "they arrive by clamp and hatch"

            // Command Room: x[-9,9], z[26,42]. Cracked viewscreen on the back wall (z=42).
            BuildFloorCeiling(interior, "Command", new Vector3(0f, 0f, 34f), new Vector3(18f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "Command_WallW", new Vector3(-9f, RoomH / 2f, 34f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(interior, "Command_WallE", new Vector3(9f, RoomH / 2f, 34f), new Vector3(0.2f, RoomH, 16f));
            var galaxyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/Galaxy1View.mat");
            BuildCommandWindshield(interior, galaxyMat);       // the windshield (cracked-screen) frame + galaxy backdrop
            BuildViewscreenCracks(interior);                   // thin dark bars overlaid to read as cracks
            BuildDoorwayWall(interior, "Command_WallFront", new Vector3(0f, RoomH / 2f, 26f), 18f, true, 2.4f);
            BuildRoomDetails(interior, "Command", new Vector3(0f, 0f, 30f), new Vector2(8f, 3.5f), new Color(0.33f, 0.4f, 0.5f));
            BuildCaptainsChair(interior, new Vector3(0f, 0f, 30f));

            // ---- Sliding doors. Medbay door starts locked until the boarding fight is won. ----
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch1AudioFolder}/DoorSlide.wav");
            var medbayDoor = BuildSlidingDoor(interior, "MedbayDoor", new Vector3(0f, 0f, 4f), 2.4f, true, startLocked: true);
            WireDoorAudio(medbayDoor, doorSlideClip);
            var airlockDoor = BuildSlidingDoor(interior, "AirlockInnerDoor", new Vector3(0f, 0f, 16f), 2.4f, true, startLocked: false);
            WireDoorAudio(airlockDoor, doorSlideClip);
            var commandDoor = BuildSlidingDoor(interior, "CommandDoor", new Vector3(0f, 0f, 26f), 2.4f, true, startLocked: true);
            WireDoorAudio(commandDoor, doorSlideClip);

            // Red docking alarm light in the airlock — off until the boarding alarm fires. When the
            // mission Trigger activates it, two play-on-awake sources fire: the docking-alarm tone and a
            // heavy clamp/forced-seal clank ("a boarding clamp seating, then the grind of a forced seal").
            var alarmGo = new GameObject("DockingAlarmLight");
            alarmGo.transform.SetParent(interior, false);
            alarmGo.transform.localPosition = new Vector3(0f, 2.6f, 21f);
            var alarmLight = alarmGo.AddComponent<Light>();
            alarmLight.type = LightType.Point;
            alarmLight.color = new Color(1f, 0.2f, 0.15f);
            alarmLight.intensity = 3f;
            alarmLight.range = 12f;
            alarmLight.shadows = LightShadows.None;
            AddOneShotOnEnable(alarmGo, $"{Ch1AudioFolder}/WaveAlarm.wav", loop: true, volume: 0.6f);
            AddOneShotOnEnable(alarmGo, $"{Ch1AudioFolder}/Landing.wav", loop: false, volume: 1f); // clamp clank
            alarmGo.SetActive(false);

            // ---- Game root: GameState + CombatFeedbackController. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- The Cairn: the lit core is one warm island inside a cold dead leviathan. ----
            BuildCairnAtmosphere(interior, gameGo); // ambient loop, sealed dead-deck hatches, hull stencil, conduit sparks

            // ---- Player rig (head + hands, no body), locomotion, bounds, belt katana. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>(); // ambient shadow-AI callouts, additive, no wiring needed
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 19f);
            bounds.radius = 45f;
            // The katana — Ronin's blade "Echo" — rests flat on the far workbench in the revival bay
            // (Beat-1 "that came out of the box with you"). Laid along the bench's long axis (+X) at
            // bench-top height so it reads as resting, not standing at eye level.
            BuildSword(new Vector3(2.15f, 0.93f, -2.6f), Quaternion.Euler(0f, 90f, 0f), weapon, Ch1EchoBladePrefab);

            // ---- Kessler: tends the table in the revival bay during the wake (a small idle only);
            // his real movement is story-driven — he walks into the hold for Beat 2 and on to the
            // command room after the fight (the two NpcWalker legs below). ----
            var kesslerPos = new Vector3(-1.6f, 0f, -0.2f); // beside the exam table, where he kept vigil
            var kesslerGo = InstantiateNpc(Ch1KesslerPrefab, kesslerPos, "Kessler");
            FitNamedCharacter(kesslerGo);
            // FitNamedCharacter grounds Kessler's (centered-pivot) root above y=0 by his mesh's foot
            // offset. NpcWalker drags the full waypoint position (including Y), so his walk waypoints
            // must share this same root Y or he gets dragged down into the floor the instant a leg starts.
            float kesslerFloorY = kesslerGo != null ? kesslerGo.transform.position.y : 0f;
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerNpcSo = new SerializedObject(kesslerNpc);
                kesslerNpcSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerNpcSo.FindProperty("remote").boolValue = false;
                kesslerNpcSo.ApplyModifiedPropertiesWithoutUndo();

                // Subtle idle by the table; the scripted walks (disabled wander on activation) carry him
                // between rooms. Small radius so he never clips the table or the cramped bay walls.
                var wander = kesslerGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = 0.7f;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Khall hologram + holotable in the command room (hidden until revealed). ----
            var holotable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holotable.name = "Holotable";
            holotable.transform.SetParent(interior, false);
            holotable.transform.localPosition = new Vector3(0f, 0.5f, 33f);
            holotable.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            TintShared(holotable.GetComponent<Renderer>(), new Color(0.15f, 0.18f, 0.22f));

            var khallGo = InstantiateNpc(Ch1KhallPrefab, new Vector3(0f, 0f, 33f), "Khall");
            FitNamedCharacter(khallGo);
            if (khallGo != null)
            {
                var khallNpc = khallGo.AddComponent<StoryNpc>();
                var khallNpcSo = new SerializedObject(khallNpc);
                khallNpcSo.FindProperty("displayName").stringValue = "Khall";
                khallNpcSo.FindProperty("remote").boolValue = true;
                khallNpcSo.ApplyModifiedPropertiesWithoutUndo();

                var hologramAudio = khallGo.AddComponent<AudioSource>();
                var hologramClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch1AudioFolder}/HologramOn.wav");
                if (hologramClip != null)
                {
                    hologramAudio.clip = hologramClip;
                    hologramAudio.playOnAwake = true;
                    hologramAudio.spatialBlend = 1f;
                }
                khallGo.SetActive(false); // revealed by the command-room Trigger step
            }

            // Ship-drone "on a worn rail" in the command-room corner (carries the "Drone" lines).
            var commandDrone = BuildShipDrone(interior, "CommandDrone", new Vector3(-6f, 1.6f, 38f), new Color(0.3f, 0.9f, 1f));
            commandDrone.AddComponent<Ronin7.Ship.TurntableRotator>();
            BuildDroneRail(interior, new Vector3(-6f, 1.6f, 38f)); // the worn rail the drone rides on

            // ---- The Dominion boarding party (Beat 3), inactive until the DefeatEnemies step. ----
            // Per the script they "arrive by clamp and hatch": spawned inside the AIRLOCK corridor at the
            // outer hatch (z~20-23), so they read as boarding through the dock and then push in toward the
            // player — the fight flows out of the airlock through the corridor/hold (the kill-box). Their
            // Enemy AI chases the player once activated. 3 troopers + the Squad Leader (the comm voice now
            // given a body, per the script's "3 Dominion troopers + the Squad Leader").
            var trooperPositions = new Vector3[]
            {
                new Vector3(-1.0f, 0f, 20f),
                new Vector3(1.0f, 0f, 20f),
                new Vector3(0f, 0f, 22f),
                new Vector3(0f, 0f, 23.5f), // Squad Leader, in behind the line
            };
            var trooperHealths = new List<Object>();
            foreach (var trooperPos in trooperPositions)
            {
                var enemy = BuildDominionEnemy(trooperPos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                trooperHealths.Add(enemy.GetComponent<Health>());
            }

            // ---- Kessler's scripted walks (inactive until their mission Triggers). ----
            // Leg 1: revival bay -> main hold, so he joins the "Three Weeks Adrift" talk by the viewport.
            var kesslerToHoldGo = BuildNpcWalker(interior, "KesslerToHold", kesslerGo, KesslerToHoldWaypoints(kesslerFloorY));
            // Leg 2: hold -> command room, after the boarding fight.
            var walkerGo = BuildNpcWalker(interior, "KesslerWalker", kesslerGo, KesslerWalkerWaypoints(kesslerFloorY));

            // ---- Reach points. ----
            var holdReachGo = new GameObject("HoldReachPoint");
            holdReachGo.transform.position = new Vector3(0f, 1f, 10f);
            var commandReachGo = new GameObject("CommandReachPoint");
            commandReachGo.transform.position = new Vector3(0f, 1f, 30f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgWake = BuildChapter1Dialogue("Dialogue_Beat1_Wake", new Vector3(0f, 1f, 1f), "ch1_beat1_wake", talkRef);
            var dlgSettle = BuildChapter1Dialogue("Dialogue_Beat1_Settle", new Vector3(0f, 1f, 1f), "ch1_beat1_settle", talkRef);
            var dlgAdrift = BuildChapter1Dialogue("Dialogue_Beat2_Adrift", new Vector3(0f, 1f, 10f), "ch1_beat2_adrift", talkRef);
            var dlgBoardPre = BuildChapter1Dialogue("Dialogue_Beat3_BoardPre", new Vector3(0f, 1f, 13f), "ch1_beat3_board_pre", talkRef);
            var dlgBoardPost = BuildChapter1Dialogue("Dialogue_Beat3_BoardPost", new Vector3(0f, 1f, 11f), "ch1_beat3_board_post", talkRef);
            var dlgWalk = BuildChapter1Dialogue("Dialogue_Beat4_Walk", new Vector3(0f, 1f, 21f), "ch1_beat4_walk", talkRef);
            var dlgUltimatum = BuildChapter1Dialogue("Dialogue_Beat4_Ultimatum", new Vector3(0f, 1f, 32f), "ch1_beat4_ultimatum", talkRef);

            // ---- Release prompt (cinematic grapple resolve), advanced by input via PromptInputAdvancer. ----
            var releasePromptGo = BuildReleasePrompt(new Vector3(0f, 1.4f, 1f));

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = BuildCompleteCanvas(new Vector3(0f, 1.4f, 30f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 30f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch1_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            // Reuse CampaignFlagSetter: wire SetFlags into the outro's activation event (persistent so it
            // survives serialization), mirroring how chapter finales wire the flag onto a button onClick.
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 1 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            // The release prompt advances the mission via this director.
            var advancer = releasePromptGo.GetComponent<PromptInputAdvancer>();
            var advancerSo = new SerializedObject(advancer);
            SetObjectRef(advancerSo, "mission", missionDirector);
            if (talkRef != null) SetObjectRef(advancerSo, "advanceAction", talkRef);
            advancerSo.ApplyModifiedPropertiesWithoutUndo();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 17;

            AuthorDialogueStep(steps, n++, "Beat1: Wake", dlgWake);
            AuthorPromptStep(steps, n++, "Prompt: Release Grapple", releasePromptGo);
            AuthorDialogueStep(steps, n++, "Beat1: Settle", dlgSettle);
            AuthorTriggerStep(steps, n++, "Trigger: Open Medbay Door + Kessler Heads In", medbayDoor, kesslerToHoldGo);
            AuthorReachStep(steps, n++, "ReachTrigger: Main Hold", holdReachGo.transform, 4.5f);
            AuthorDialogueStep(steps, n++, "Beat2: Adrift", dlgAdrift);
            AuthorTriggerStep(steps, n++, "Trigger: Wreck-field Running Light", runningLight);
            AuthorTriggerStep(steps, n++, "Trigger: Boarding Alarm (red wash)", alarmGo);
            AuthorDialogueStep(steps, n++, "Beat3: Boarding (pre-fight VO)", dlgBoardPre);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Boarding Party (3 troopers + Squad Leader)", trooperHealths);
            AuthorDialogueStep(steps, n++, "Beat3: Boarding (aftermath)", dlgBoardPost);
            AuthorTriggerStep(steps, n++, "Trigger: Unlock Corridor + Kessler Walks", commandDoor, walkerGo);
            AuthorDialogueStep(steps, n++, "Beat4: Walk to Command", dlgWalk);
            AuthorReachStep(steps, n++, "ReachTrigger: Command Room", commandReachGo.transform, 4.5f);
            AuthorTriggerStep(steps, n++, "Trigger: Drone Message + Khall Hologram", khallGo);
            AuthorDialogueStep(steps, n++, "Beat4: Ultimatum", dlgUltimatum);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Hub mode: the persistent-hub half of this scene (console + gated future-chapter rooms).
            // The startLocked doors are handed over so hub mode can activate them without the mission. ----
            BuildHubMode(gameGo, missionGo, new[] { medbayDoor, commandDoor });

            // ---- XR UI infrastructure. ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, revival-bay ambience bed, console/mood light beats. ----
            var medbayAmbience = BuildAmbienceLayer("MedbayAmbience", new Vector3(3f, 1f, -3.3f), 2f, 6f, 0.45f);
            var medbayAmbienceSource = medbayAmbience.GetComponent<AudioSource>();
            medbayAmbienceSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Art/Generated/Audio/SFX/medbay_hum.wav");
            AddConsoleFlicker("CommandLight", seed: 11f);
            AddAmbientPulse("MedbayLight", periodSeconds: 7f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch1HubScenePath);
            EnsureScenesInBuild(Ch1HubScenePath);

            Debug.Log($"[Space Samurai] Chapter 1 hub built at {Ch1HubScenePath}. " +
                      "Four rooms: Revival Bay (wake + katana) -> Main Hold (wreck-field viewport) -> " +
                      "Airlock (the boarding, red alarm) -> Command Room (cracked viewscreen, Khall ultimatum). " +
                      "17 mission steps: wake, release prompt, settle, open door, reach hold, adrift, running light, " +
                      "boarding alarm, board pre-VO, 3-trooper fight, board aftermath, unlock + Kessler walks, walk, " +
                      "reach command, Khall reveal, ultimatum, chapter outro (sets ch1_complete + fade + canvas). " +
                      "Medbay door starts locked until the fight is won.");
        }

        // ---- Dialogue: build via the shared helper, then wire ch1 voice clips ourselves. ----

        /// <summary>
        /// Builds a dialogue player from canonical Chapter 1 lines and wires its voice clips from
        /// <see cref="Ch1VoiceFolder"/>. The shared <c>BuildDialoguePlayer</c> loader searches a
        /// different folder (Audio/Voice) and a different prefix, so clips are wired here instead.
        /// </summary>
        private static DialoguePlayer BuildChapter1Dialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter1Lines.Get(setId);
            // clipSetId left null so the shared loader does not spam missing-clip warnings for the wrong folder.
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = WireChapter1Clips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter1] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int WireChapter1Clips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter1Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch1VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch1VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Small scene-dressing / prompt helpers specific to Chapter 1. ----

        /// <summary>
        /// Clamps a generated character to ~1.8 m tall if it imported off-scale, then grounds it by its
        /// mesh bottom so the feet sit on the floor. These Tripo meshes have a CENTERED pivot, so simply
        /// placing the root at y=0 buries the lower half — we lift the root by the mesh's lowest point.
        /// </summary>
        private static void FitNamedCharacter(GameObject go)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            var b = EncapsulateRenderers(rends);
            float h = b.size.y;
            if (h > 0.01f && (h < 1.2f || h > 2.4f))
            {
                float k = 1.8f / h;
                go.transform.localScale = go.transform.localScale * k;
            }

            // Re-measure after scaling (a centered pivot's extents shift with scale) and ground the feet.
            b = EncapsulateRenderers(go.GetComponentsInChildren<Renderer>());
            go.transform.position += Vector3.up * (-b.min.y);
        }

        /// <summary>World-space AABB enclosing all of the given renderers.</summary>
        private static Bounds EncapsulateRenderers(Renderer[] rends)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>
        /// Leg 1 waypoints for Kessler's revival-bay -> main-hold walk. <paramref name="floorY"/> must be
        /// the Y his (centered-pivot) root sits at once grounded by <see cref="FitNamedCharacter"/> —
        /// <see cref="NpcWalker"/> drags the full waypoint position, so a mismatched Y buries or floats him.
        /// Internal (not private) so <c>Chapter1BuilderTests</c> can verify this invariant directly.
        /// </summary>
        internal static Vector3[] KesslerToHoldWaypoints(float floorY) => new[]
        {
            new Vector3(0f, floorY, 2f),
            new Vector3(0f, floorY, 6f),
            new Vector3(-2f, floorY, 9.5f), // by the wreck-field viewport (west wall)
        };

        /// <summary>Leg 2 waypoints for Kessler's hold -> command-room walk. See <see cref="KesslerToHoldWaypoints"/>.</summary>
        internal static Vector3[] KesslerWalkerWaypoints(float floorY) => new[]
        {
            new Vector3(0f, floorY, 10f),
            new Vector3(0f, floorY, 21f),
            new Vector3(0f, floorY, 28f),
            new Vector3(2f, floorY, 31f),
        };

        /// <summary>
        /// Builds an inactive <see cref="NpcWalker"/> that walks <paramref name="target"/> through the
        /// given waypoints once when a mission Trigger activates it. Used for Kessler's story-driven
        /// relocations (revival bay -> hold -> command room).
        /// </summary>
        private static GameObject BuildNpcWalker(Transform parent, string name, GameObject target, Vector3[] waypoints)
        {
            var walkerGo = new GameObject(name);
            walkerGo.transform.SetParent(parent, false);

            var waypointTransforms = new Transform[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = new GameObject($"WP{i}");
                wp.transform.SetParent(walkerGo.transform, false);
                wp.transform.localPosition = waypoints[i];
                waypointTransforms[i] = wp.transform;
            }

            var walker = walkerGo.AddComponent<NpcWalker>();
            var walkerSo = new SerializedObject(walker);
            if (target != null) SetObjectRef(walkerSo, "target", target.transform);
            var wpProp = walkerSo.FindProperty("waypoints");
            wpProp.arraySize = waypointTransforms.Length;
            for (int i = 0; i < waypointTransforms.Length; i++)
                wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypointTransforms[i];
            walkerSo.ApplyModifiedPropertiesWithoutUndo();
            walkerGo.SetActive(false);
            return walkerGo;
        }

        // ---- Part B: story-faithful Ch1 set dressing (cheap primitives, reusing BuildProp/TintShared). ----

        /// <summary>Adds a child 3D AudioSource that plays the clip when its GameObject becomes active —
        /// used to fire SFX off a mission Trigger that SetActive(true)s the parent (e.g. the docking alarm).</summary>
        private static void AddOneShotOnEnable(GameObject target, string clipPath, bool loop, float volume)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null) return;
            var sfxGo = new GameObject("SFX_" + clip.name);
            sfxGo.transform.SetParent(target.transform, false);
            var src = sfxGo.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = volume;
            src.spatialBlend = 1f;
            src.playOnAwake = true;
        }

        /// <summary>Revival Bay (Beat 1): the opened Dominion casket Ronin woke from, an IV rack, a
        /// salvage cutter's torch + pry bar on the workbench, and a stray feed cable off the table.</summary>
        private static void BuildRevivalBayStory(Transform parent)
        {
            var steel = new Color(0.4f, 0.43f, 0.47f);
            var darkSteel = new Color(0.22f, 0.24f, 0.28f);
            var seal = new Color(0.7f, 0.5f, 0.16f);  // Dominion lock/seal brass
            var screen = new Color(0.2f, 0.85f, 1f);

            // The opened casket on the floor beside the table — the box he was thrown away in.
            var casket = new GameObject("DominionCasket");
            casket.transform.SetParent(parent, false);
            casket.transform.localPosition = new Vector3(-1.4f, 0f, 1.0f);
            BuildProp(casket.transform, "Casket_Shell", new Vector3(0f, 0.3f, 0f), new Vector3(0.85f, 0.55f, 2.1f), darkSteel);
            BuildProp(casket.transform, "Casket_Interior", new Vector3(0f, 0.42f, 0f), new Vector3(0.65f, 0.4f, 1.9f), new Color(0.1f, 0.11f, 0.13f));
            // Lid, hinged open and tilted back against the floor.
            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Casket_Lid";
            lid.transform.SetParent(casket.transform, false);
            lid.transform.localPosition = new Vector3(-0.7f, 0.35f, 0f);
            lid.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            lid.transform.localScale = new Vector3(0.85f, 0.1f, 2.1f);
            TintShared(lid.GetComponent<Renderer>(), darkSteel);
            BuildProp(casket.transform, "Casket_Seal", new Vector3(0.44f, 0.55f, 0f), new Vector3(0.02f, 0.12f, 0.5f), seal);

            // IV rack beside the table — a pole, a foot, and a fluid bag near the top.
            var iv = new GameObject("IVRack");
            iv.transform.SetParent(parent, false);
            iv.transform.localPosition = new Vector3(1.1f, 0f, -0.7f);
            BuildProp(iv.transform, "IV_Pole", new Vector3(0f, 0.8f, 0f), new Vector3(0.04f, 1.6f, 0.04f), steel);
            BuildProp(iv.transform, "IV_Foot", new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.06f, 0.4f), darkSteel);
            BuildProp(iv.transform, "IV_Bag", new Vector3(0.12f, 1.4f, 0f), new Vector3(0.12f, 0.22f, 0.05f), new Color(0.6f, 0.75f, 0.55f));

            // Salvage-cutter tools on the far workbench (bench top ~y0.9 at x2.6,z-2.6).
            BuildProp(parent, "CuttingTorch", new Vector3(2.1f, 0.98f, -2.5f), new Vector3(0.08f, 0.08f, 0.35f), new Color(0.5f, 0.3f, 0.12f));
            BuildProp(parent, "PryBar", new Vector3(2.9f, 0.96f, -2.55f), new Vector3(0.04f, 0.04f, 0.5f), steel);

            // A stray monitor feed cable trailing off the exam table.
            BuildProp(parent, "TableCable", new Vector3(0.5f, 0.45f, 0.4f), new Vector3(0.025f, 0.5f, 0.025f), darkSteel);
            // A live wall monitor on the casket side reads as the revival rig still running.
            var mon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mon.name = "RevivalMonitor";
            mon.transform.SetParent(parent, false);
            mon.transform.localPosition = new Vector3(-3.85f, 1.6f, 0.8f);
            mon.transform.localScale = new Vector3(0.08f, 0.5f, 0.7f);
            TintShared(mon.GetComponent<Renderer>(), screen);
            Object.DestroyImmediate(mon.GetComponent<Collider>());
        }

        /// <summary>Main Hold (Beat 2): the two dented cups Kessler fills out of habit, graded salvage
        /// piles (copper coils + stripped hull plate), and the single thin dirty star past the viewport.</summary>
        private static void BuildHoldStory(Transform parent)
        {
            var worn = new Color(0.32f, 0.3f, 0.28f);
            var copper = new Color(0.55f, 0.33f, 0.16f);
            var plate = new Color(0.34f, 0.36f, 0.4f);
            var cupColor = new Color(0.45f, 0.43f, 0.4f);

            // A worn workbench on the +x side of the hold, with two dented cups on it.
            BuildProp(parent, "HoldWorkbench", new Vector3(3.6f, 0.45f, 7.5f), new Vector3(1.4f, 0.9f, 0.7f), worn);
            var cupA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cupA.name = "Cup_A";
            cupA.transform.SetParent(parent, false);
            cupA.transform.localPosition = new Vector3(3.4f, 0.98f, 7.4f);
            cupA.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
            TintShared(cupA.GetComponent<Renderer>(), cupColor);
            var cupB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cupB.name = "Cup_B";
            cupB.transform.SetParent(parent, false);
            cupB.transform.localPosition = new Vector3(3.7f, 0.98f, 7.6f);
            cupB.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
            TintShared(cupB.GetComponent<Renderer>(), cupColor);

            // Graded salvage piles (kept clear of the doorways at z=4/16 and the west viewport).
            BuildProp(parent, "Salvage_HullPlate0", new Vector3(4.6f, 0.15f, 12.5f), new Vector3(1.2f, 0.08f, 1.6f), plate);
            BuildProp(parent, "Salvage_HullPlate1", new Vector3(4.6f, 0.28f, 12.4f), new Vector3(1.1f, 0.08f, 1.5f), plate * 0.9f);
            BuildProp(parent, "Salvage_HullPlate2", new Vector3(4.55f, 0.4f, 12.6f), new Vector3(1.0f, 0.08f, 1.4f), plate * 1.1f);
            var coil0 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coil0.name = "Salvage_CopperCoil0";
            coil0.transform.SetParent(parent, false);
            coil0.transform.localPosition = new Vector3(3.4f, 0.25f, 13.4f);
            coil0.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coil0.transform.localScale = new Vector3(0.5f, 0.25f, 0.5f);
            TintShared(coil0.GetComponent<Renderer>(), copper);
            var coil1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coil1.name = "Salvage_CopperCoil1";
            coil1.transform.SetParent(parent, false);
            coil1.transform.localPosition = new Vector3(3.9f, 0.25f, 13.5f);
            coil1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coil1.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
            TintShared(coil1.GetComponent<Renderer>(), copper * 0.9f);

            // The single thin, dirty star past the wreck-field viewport (viewport backdrop sits at x=-8).
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "DirtyStar";
            star.transform.SetParent(parent, false);
            star.transform.localPosition = new Vector3(-9.5f, 2.6f, 6.5f);
            star.transform.localScale = Vector3.one * 0.5f;
            TintShared(star.GetComponent<Renderer>(), new Color(1f, 0.78f, 0.45f));
            Object.DestroyImmediate(star.GetComponent<Collider>());
            var starLight = star.AddComponent<Light>();
            starLight.type = LightType.Point;
            starLight.color = new Color(1f, 0.8f, 0.5f);
            starLight.intensity = 1.1f;
            starLight.range = 14f;
            starLight.shadows = LightShadows.None;
        }

        /// <summary>The Cairn: the lit core is one warm island in a cold dead leviathan. A low ambient bed,
        /// sealed unpowered dead-deck hatches that imply more ship, a weathered hull stencil, and a couple
        /// of shorted conduits throwing sparks ("power is thin: flickers, shorted lines").</summary>
        private static void BuildCairnAtmosphere(Transform interior, GameObject gameGo)
        {
            // Ambient bed (2D) — coolant hiss / the slow tick of a machine left running too long.
            var ambClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch1AudioFolder}/OnFootAmbience.wav");
            if (ambClip != null)
            {
                var amb = gameGo.AddComponent<AudioSource>();
                amb.clip = ambClip;
                amb.loop = true;
                amb.playOnAwake = true;
                amb.spatialBlend = 0f;
                amb.volume = 0.35f;
            }

            // Sealed, unpowered dead-deck hatches: dark frames with a dead status nub — beyond them is hull
            // the player never enters this chapter. One behind the revival bay, one off the command room.
            BuildDeadDeckHatch(interior, new Vector3(0f, 1.3f, -3.88f), Quaternion.identity);          // revival bay front
            BuildDeadDeckHatch(interior, new Vector3(8.78f, 1.3f, 36f), Quaternion.Euler(0f, 90f, 0f)); // command room side

            // Weathered hull stencil — canon: the name is on the hull (in dialogue it's just "the rig").
            var stencil = new GameObject("HullStencil_TheCairn");
            stencil.transform.SetParent(interior, false);
            stencil.transform.localPosition = new Vector3(-1.88f, 1.9f, 24f);
            stencil.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // on the airlock west wall, facing +x
            stencil.transform.localScale = Vector3.one * 0.01f;
            var tm = stencil.AddComponent<TextMesh>();
            tm.text = "THE CAIRN";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.color = new Color(0.4f, 0.42f, 0.46f);

            // Shorted conduits throwing sparks on a couple of bulkheads.
            BuildConduitSpark(interior, new Vector3(3.6f, 2.4f, -3.7f)); // revival bay
            BuildConduitSpark(interior, new Vector3(-1.7f, 2.3f, 18f));  // airlock corridor
        }

        /// <summary>A dark, sealed, unpowered hatch frame with a single dead status light — set dressing
        /// that implies the cold leviathan beyond the lit core.</summary>
        private static void BuildDeadDeckHatch(Transform parent, Vector3 pos, Quaternion rot)
        {
            var hatch = new GameObject("DeadDeckHatch");
            hatch.transform.SetParent(parent, false);
            hatch.transform.localPosition = pos;
            hatch.transform.localRotation = rot;
            BuildProp(hatch.transform, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(1.6f, 2.4f, 0.06f), new Color(0.16f, 0.17f, 0.2f));
            BuildProp(hatch.transform, "Door", new Vector3(0f, 0f, 0.05f), new Vector3(1.3f, 2.1f, 0.04f), new Color(0.08f, 0.09f, 0.11f));
            BuildProp(hatch.transform, "DeadStatus", new Vector3(0.5f, 0.7f, 0.08f), new Vector3(0.06f, 0.06f, 0.02f), new Color(0.15f, 0.05f, 0.05f));
        }

        /// <summary>Instantiates the SwordSpark VFX (if present) as a shorted-conduit spark, plus a small
        /// faint emissive nub + light so the "stressed conduit" reads even without the particle.</summary>
        private static void BuildConduitSpark(Transform parent, Vector3 pos)
        {
            var nub = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nub.name = "ShortedConduit";
            nub.transform.SetParent(parent, false);
            nub.transform.localPosition = pos;
            nub.transform.localScale = new Vector3(0.12f, 0.12f, 0.08f);
            TintShared(nub.GetComponent<Renderer>(), new Color(0.9f, 0.85f, 0.6f));
            Object.DestroyImmediate(nub.GetComponent<Collider>());
            var l = nub.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.8f, 0.85f, 1f);
            l.intensity = 0.8f;
            l.range = 3f;
            l.shadows = LightShadows.None;

            var sparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ronin7/Resources/Vfx/SwordSpark.prefab");
            if (sparkPrefab != null)
            {
                var spark = (GameObject)PrefabUtility.InstantiatePrefab(sparkPrefab, parent);
                spark.name = "ConduitSparkVfx";
                spark.transform.localPosition = pos;
            }
        }

        /// <summary>A heavy boarding clamp seated on the airlock's outer dock hatch (the boarders' entry).</summary>
        private static void BuildBoardingClamp(Transform parent)
        {
            var clampColor = new Color(0.2f, 0.21f, 0.24f);
            var clamp = new GameObject("BoardingClamp");
            clamp.transform.SetParent(parent, false);
            clamp.transform.localPosition = new Vector3(2.05f, 1.4f, 21f); // on the east-wall outer hatch
            BuildProp(clamp.transform, "Clamp_Ring", new Vector3(0f, 0f, 0f), new Vector3(0.14f, 2.6f, 0.2f), clampColor);
            BuildProp(clamp.transform, "Clamp_ArmTop", new Vector3(-0.12f, 1.0f, 0.7f), new Vector3(0.3f, 0.18f, 0.18f), clampColor);
            BuildProp(clamp.transform, "Clamp_ArmBot", new Vector3(-0.12f, -1.0f, 0.7f), new Vector3(0.3f, 0.18f, 0.18f), clampColor);
            BuildProp(clamp.transform, "Clamp_ArmL", new Vector3(-0.12f, 0f, 1.0f), new Vector3(0.3f, 0.18f, 0.18f), clampColor);
        }

        /// <summary>The worn rail the command-room ship-drone rides on (and a wall bracket).</summary>
        private static void BuildDroneRail(Transform parent, Vector3 dronePos)
        {
            var rail = new Color(0.3f, 0.32f, 0.36f);
            BuildProp(parent, "DroneRail", new Vector3(dronePos.x, dronePos.y + 0.18f, dronePos.z), new Vector3(0.06f, 0.06f, 4f), rail);
            BuildProp(parent, "DroneRail_BracketA", new Vector3(dronePos.x, dronePos.y + 0.18f, dronePos.z - 2f), new Vector3(0.12f, 0.5f, 0.12f), rail * 0.8f);
            BuildProp(parent, "DroneRail_BracketB", new Vector3(dronePos.x, dronePos.y + 0.18f, dronePos.z + 2f), new Vector3(0.12f, 0.5f, 0.12f), rail * 0.8f);
        }

        /// <summary>Revival-bay set dressing: a steel exam table, two wall monitors, and a workbench.</summary>
        private static void BuildMedbayProps(Transform parent)
        {
            var steel = new Color(0.55f, 0.58f, 0.62f);
            var screen = new Color(0.2f, 0.85f, 1f);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "ExamTable";
            table.transform.SetParent(parent, false);
            table.transform.localPosition = new Vector3(0f, 0.5f, -0.2f);
            table.transform.localScale = new Vector3(0.9f, 0.12f, 2.0f);
            TintShared(table.GetComponent<Renderer>(), steel);
            var legs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legs.name = "ExamTableBase";
            legs.transform.SetParent(parent, false);
            legs.transform.localPosition = new Vector3(0f, 0.25f, -0.2f);
            legs.transform.localScale = new Vector3(0.4f, 0.5f, 0.8f);
            TintShared(legs.GetComponent<Renderer>(), steel * 0.7f);

            // Two wall monitors on the east wall.
            for (int i = 0; i < 2; i++)
            {
                var mon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mon.name = "Monitor" + i;
                mon.transform.SetParent(parent, false);
                mon.transform.localPosition = new Vector3(3.85f, 1.7f, -1f + i * 1.6f);
                mon.transform.localScale = new Vector3(0.08f, 0.5f, 0.7f);
                TintShared(mon.GetComponent<Renderer>(), screen);
                Object.DestroyImmediate(mon.GetComponent<Collider>());
            }

            // Far workbench (holds the katana).
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Workbench";
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = new Vector3(2.6f, 0.45f, -2.6f);
            bench.transform.localScale = new Vector3(1.6f, 0.9f, 0.7f);
            TintShared(bench.GetComponent<Renderer>(), steel * 0.6f);
        }

        /// <summary>
        /// A wreck-field viewport in the Main Hold's west wall: solid sill/header/pillars frame a clear
        /// opening, with a dark space backdrop and wreck silhouettes set outside the hull. Returns the
        /// "running light" object (inactive) that a mission Trigger reveals — a dim light that orbits past
        /// the window so the player sees something drift by outside.
        /// </summary>
        private static GameObject BuildHoldViewport(Transform parent)
        {
            // West wall runs along z at x=-6; opening is z[7,13], y[0.8,2.8].
            BuildWall(parent, "Hold_WallW_Sill", new Vector3(-6f, 0.4f, 10f), new Vector3(0.2f, 0.8f, 12f));
            BuildWall(parent, "Hold_WallW_Header", new Vector3(-6f, (2.8f + RoomH) / 2f, 10f), new Vector3(0.2f, RoomH - 2.8f, 12f));
            BuildWall(parent, "Hold_WallW_PillarA", new Vector3(-6f, 1.8f, 5.5f), new Vector3(0.2f, 2.0f, 3f));
            BuildWall(parent, "Hold_WallW_PillarB", new Vector3(-6f, 1.8f, 14.5f), new Vector3(0.2f, 2.0f, 3f));

            // Deep-space backdrop quad outside (x=-8), oversized so its edges hide behind the frame.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "WreckFieldBackdrop";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = new Vector3(-8f, 1.8f, 10f);
            backdrop.transform.localScale = new Vector3(11f, 5f, 1f);
            backdrop.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // face +x toward the player
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            TintShared(backdrop.GetComponent<Renderer>(), new Color(0.05f, 0.06f, 0.12f));

            // A few wreck silhouettes drifting between the backdrop and the window.
            var wreck = new Color(0.12f, 0.13f, 0.16f);
            BuildProp(parent, "Wreck0", new Vector3(-7.2f, 1.4f, 8.4f), new Vector3(0.6f, 0.5f, 1.8f), wreck);
            BuildProp(parent, "Wreck1", new Vector3(-7.4f, 2.3f, 11.6f), new Vector3(0.5f, 0.4f, 1.2f), wreck);
            BuildProp(parent, "Wreck2", new Vector3(-7.0f, 1.0f, 12.4f), new Vector3(0.8f, 0.4f, 0.4f), wreck);

            // Running light: a dim emissive mote that orbits past the viewport (revealed by a Trigger step).
            var hub = new GameObject("RunningLightHub");
            hub.transform.SetParent(parent, false);
            hub.transform.localPosition = new Vector3(-9f, 1.8f, 10f);

            var runningLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            runningLight.name = "RunningLight";
            runningLight.transform.SetParent(parent, false);
            runningLight.transform.localPosition = new Vector3(-7f, 1.8f, 10f);
            runningLight.transform.localScale = Vector3.one * 0.25f;
            TintShared(runningLight.GetComponent<Renderer>(), new Color(1f, 0.6f, 0.4f));
            Object.DestroyImmediate(runningLight.GetComponent<Collider>());
            var rlLight = runningLight.AddComponent<Light>();
            rlLight.type = LightType.Point;
            rlLight.color = new Color(1f, 0.7f, 0.5f);
            rlLight.intensity = 1.2f;
            rlLight.range = 6f;
            rlLight.shadows = LightShadows.None;
            var orbit = runningLight.AddComponent<Ronin7.Ship.PlanetOrbit>();
            var orbitSo = new SerializedObject(orbit);
            SetObjectRef(orbitSo, "center", hub.transform);
            orbitSo.FindProperty("radius").floatValue = 2.2f;
            orbitSo.FindProperty("angularSpeedDeg").floatValue = 18f;
            orbitSo.FindProperty("yOffset").floatValue = 0f;
            orbitSo.FindProperty("startAngleDeg").floatValue = 0f;
            orbitSo.ApplyModifiedPropertiesWithoutUndo();
            runningLight.SetActive(false);

            return runningLight;
        }

        /// <summary>A sealed outer dock hatch on the airlock's east wall — the boarders' entry point.</summary>
        private static void BuildAirlockHatch(Transform parent)
        {
            var hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hatch.name = "OuterDockHatch";
            hatch.transform.SetParent(parent, false);
            hatch.transform.localPosition = new Vector3(1.95f, 1.4f, 21f);
            hatch.transform.localScale = new Vector3(0.12f, 2.2f, 2.6f);
            TintShared(hatch.GetComponent<Renderer>(), new Color(0.28f, 0.3f, 0.34f));
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "OuterDockHatchSeal";
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(1.9f, 1.4f, 21f);
            ring.transform.localScale = new Vector3(0.1f, 2.4f, 2.9f);
            TintShared(ring.GetComponent<Renderer>(), new Color(0.45f, 0.32f, 0.16f));
            Object.DestroyImmediate(ring.GetComponent<Collider>());
        }

        /// <summary>Thin dark bars overlaid on the command windshield to read as a cracked viewscreen.</summary>
        private static void BuildViewscreenCracks(Transform parent)
        {
            var crackColor = new Color(0.03f, 0.03f, 0.05f);
            var cracks = new (Vector3 pos, Vector3 scale, Vector3 rot)[]
            {
                (new Vector3(-1.5f, 1.9f, 41.8f), new Vector3(0.04f, 2.6f, 0.04f), new Vector3(0f, 0f, 18f)),
                (new Vector3(0.6f, 2.2f, 41.8f), new Vector3(0.04f, 2.2f, 0.04f), new Vector3(0f, 0f, -28f)),
                (new Vector3(-0.4f, 1.4f, 41.8f), new Vector3(0.03f, 1.6f, 0.03f), new Vector3(0f, 0f, 70f)),
            };
            foreach (var (pos, scale, rot) in cracks)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "ViewscreenCrack";
                c.transform.SetParent(parent, false);
                c.transform.localPosition = pos;
                c.transform.localScale = scale;
                c.transform.localRotation = Quaternion.Euler(rot);
                TintShared(c.GetComponent<Renderer>(), crackColor);
                Object.DestroyImmediate(c.GetComponent<Collider>());
            }
        }

        /// <summary>The salvaged captain's chair the command room is built around.</summary>
        private static void BuildCaptainsChair(Transform parent, Vector3 pos)
        {
            var worn = new Color(0.3f, 0.28f, 0.26f);
            var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "CaptainsChair_Seat";
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = pos + new Vector3(0f, 0.5f, 0f);
            seat.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);
            TintShared(seat.GetComponent<Renderer>(), worn);
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "CaptainsChair_Back";
            back.transform.SetParent(parent, false);
            back.transform.localPosition = pos + new Vector3(0f, 1.1f, -0.4f);
            back.transform.localScale = new Vector3(0.9f, 1.2f, 0.18f);
            TintShared(back.GetComponent<Renderer>(), worn);
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "CaptainsChair_Pedestal";
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localPosition = pos + new Vector3(0f, 0.2f, 0f);
            pedestal.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);
            TintShared(pedestal.GetComponent<Renderer>(), worn * 0.7f);
        }

        /// <summary>A worldspace "Release" prompt carrying a PromptInputAdvancer. Created inactive.</summary>
        private static GameObject BuildReleasePrompt(Vector3 position)
        {
            var go = new GameObject("ReleasePrompt");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.012f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = "Release  (Y)";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = new Color(0.95f, 0.85f, 0.55f);
            go.AddComponent<PromptInputAdvancer>();
            go.SetActive(false);
            return go;
        }

        /// <summary>A worldspace "CHAPTER 1 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 1 COMPLETE Canvas");
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
            label.text = "CHAPTER 1 COMPLETE";
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

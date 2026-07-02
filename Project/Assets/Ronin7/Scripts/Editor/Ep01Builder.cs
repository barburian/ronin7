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
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// EP01 salvage-ship interior scene builder. Builds the mother-ship scene where Ronin-7 wakes,
    /// meets Kessler, fights Empire troopers, and learns of the mission to rescue Iris.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ep01ShipScenePath = SceneFolder + "/Galaxy1_EP01_Ship.unity";
        private static readonly string Ep01ShipSceneName = System.IO.Path.GetFileNameWithoutExtension(Ep01ShipScenePath);
        private const string Galaxy1Ep01PlanetScenePath = SceneFolder + "/Galaxy1_EP01_Planet.unity";
        private static readonly string Galaxy1Ep01PlanetSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep01PlanetScenePath);
        private const string Galaxy1Ep01HideoutScenePath = SceneFolder + "/Galaxy1_EP01_Hideout.unity";
        private static readonly string Galaxy1Ep01HideoutSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep01HideoutScenePath);

        /// <summary>
        /// Rebuilds every EP01–EP05 interior/zone scene, then re-runs the input rewire pass over all
        /// scenes (builders in batchmode leave InputActionReferences null — see RewireAllScenes).
        /// Also a batchmode entry point (-executeMethod).
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build All Episode Scenes (EP01-07)", priority = 58)]
        public static void BuildAllEpisodeScenes()
        {
            BuildEp01ShipInterior();
            BuildEp01PlanetZone();
            BuildEp01Hideout();
            BuildEp02Docking();
            BuildEp02Pens();
            BuildEp02Core();
            BuildEp02SafeHouse();
            BuildEp03Hauler();
            BuildEp03EngineRoom();
            BuildEp03LotusStation();
            BuildEp03Sanctuary();
            BuildEp04JungleMoon();
            BuildEp04ArchiveRing();
            BuildEp04Ledger();
            BuildEp05MarketTier();
            BuildEp05PressureLocks();
            BuildEp05Rotunda();
            BuildEp05CommandHub();
            BuildEp06MedicalCompound();
            BuildEp06GantryHub();
            BuildEp06ArchiveVault();
            BuildEp06Approach();
            BuildEp06Escape();
            BuildEp07Approach();
            BuildEp07MarketHub();
            BuildEp07Archive();
            BuildEp07Escape();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All episode scenes rebuilt + inputs rewired.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP01 Ship Interior", priority = 65)]
        public static void BuildEp01ShipInterior()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: a cool directional key + a low ambient fill, then two warm/cold accent
            // point lights to give the interior depth and a distinct medbay-vs-bridge mood (flat
            // single-directional lighting was a big part of why EP01 read as basic).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 1f); // cool key
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Low ambient fill so shadowed sides don't crush to black on Quest.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.2f);

            // Warm medbay accent (front of the ship, around the wake/origin area).
            BuildAccentPointLight("MedbayLight", new Vector3(0f, 2.6f, 1f),
                new Color(1f, 0.86f, 0.66f), intensity: 2.2f, range: 13f);
            // Cool corridor accents.
            BuildAccentPointLight("CorridorLight1", new Vector3(0f, 2.6f, 12f),
                new Color(0.6f, 0.78f, 1f), intensity: 1.6f, range: 12f);
            BuildAccentPointLight("CorridorLight2", new Vector3(0f, 2.6f, 22f),
                new Color(0.6f, 0.78f, 1f), intensity: 1.6f, range: 12f);
            // Cold cyan command-room accent (where the Khall hologram appears, ~z35).
            BuildAccentPointLight("CommandLight", new Vector3(0f, 2.6f, 33f),
                new Color(0.5f, 0.82f, 1f), intensity: 2.4f, range: 16f);

            // ---- Ship interior geometry: medbay room -> corridor -> 6 side rooms + command room. ----
            // The player wakes in the enclosed medbay (z<6), fights, then a sliding door opens into a
            // corridor lined with rooms; the command room sits at the far end (z>26).
            var interiorGo = new GameObject("ShipInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.26f);
            var ceilColor = new Color(0.12f, 0.13f, 0.16f);

            // Medbay room: x[-5,5], z[-4,6], with a door gap in the back wall (z=6).
            BuildFloorCeiling(interior, "Medbay", new Vector3(0f, 0f, -0.5f), new Vector3(13f, 0f, 13f), floorColor, ceilColor);
            BuildWall(interior, "Medbay_WallW", new Vector3(-6.5f, RoomH/2f, -0.5f), new Vector3(0.2f, RoomH, 13f));
            BuildWall(interior, "Medbay_WallE", new Vector3(6.5f, RoomH/2f, -0.5f), new Vector3(0.2f, RoomH, 13f));
            BuildWall(interior, "Medbay_WallFront", new Vector3(0f, RoomH/2f, -7f), new Vector3(13f, RoomH, 0.2f));
            BuildDoorwayWall(interior, "Medbay_WallBack", new Vector3(0f, RoomH/2f, 6f), 13f, true, 2.4f);
            // Set-dressing biased to the front/side corners (z<0, x≈±5.5) to stay clear of the wake area,
            // Kessler, troopers, the sword and the z=6 door.
            BuildRoomDetails(interior, "Medbay", new Vector3(0f, 0f, -0.5f), new Vector2(6.5f, 6.5f), new Color(0.5f, 0.6f, 0.62f));

            // Corridor: x[-2,2], z[6,26], three door gaps per side at z=10,16,22.
            float[] sideDoorZ = { 10f, 16f, 22f };
            BuildFloorCeiling(interior, "Corridor", new Vector3(0f, 0f, 16f), new Vector3(4f, 0f, 20f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -2f, 6f, 26f, sideDoorZ, 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 2f, 6f, 26f, sideDoorZ, 2.4f);

            // Command room (the 7th room) at the far end: x[-7,7], z[26,38], door gap in front wall (z=26).
            BuildFloorCeiling(interior, "Command", new Vector3(0f, 0f, 34f), new Vector3(18f, 0f, 16f), floorColor, ceilColor);
            BuildWall(interior, "Command_WallW", new Vector3(-9f, RoomH/2f, 34f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(interior, "Command_WallE", new Vector3(9f, RoomH/2f, 34f), new Vector3(0.2f, RoomH, 16f));
            // The back wall the player faces is a bridge windshield onto Galaxy 1 instead of solid steel.
            var galaxyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/Galaxy1View.mat");
            BuildCommandWindshield(interior, galaxyMat);
            BuildDoorwayWall(interior, "Command_WallFront", new Vector3(0f, RoomH/2f, 26f), 18f, true, 2.4f);
            // Set-dressing biased to the side walls / front corners (x≈±8, z<40) to stay clear of the
            // Holotable, Khall, the GO TO SPACE canvas and the z=42 windshield wall.
            BuildRoomDetails(interior, "Command", new Vector3(0f, 0f, 34f), new Vector2(9f, 8f), new Color(0.35f, 0.4f, 0.5f));

            // Six lightly-themed side rooms off the corridor (3 west, 3 east).
            BuildRoomShell(interior, "Armory",       2f, true,  10f, 2.9f, 9f, floorColor, ceilColor, "ARMORY",        new Color(0.3f, 0.22f, 0.22f));
            BuildRoomShell(interior, "CrewQuarters", 2f, true,  16f, 2.9f, 9f, floorColor, ceilColor, "CREW QUARTERS", new Color(0.22f, 0.24f, 0.3f));
            BuildRoomShell(interior, "EngineBay",    2f, true,  22f, 2.9f, 9f, floorColor, ceilColor, "ENGINE BAY",    new Color(0.3f, 0.26f, 0.18f));
            BuildRoomShell(interior, "Storage",      2f, false, 10f, 2.9f, 9f, floorColor, ceilColor, "STORAGE",       new Color(0.24f, 0.26f, 0.24f));
            BuildRoomShell(interior, "MedStorage",   2f, false, 16f, 2.9f, 9f, floorColor, ceilColor, "MED-STORAGE",   new Color(0.22f, 0.3f, 0.3f));
            BuildRoomShell(interior, "Comms",        2f, false, 22f, 2.9f, 9f, floorColor, ceilColor, "COMMS",         new Color(0.2f, 0.28f, 0.34f));

            // Sliding doors. The medbay door starts locked (controller inactive) until the fight is won;
            // every other door is openable from the start so the player can explore.
            // Each door controller gets 3D spatial audio for door slides.
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var medbayDoor = BuildSlidingDoor(interior, "MedbayDoor", new Vector3(0f, 0f, 6f), 2.4f, true, startLocked: true);
            WireDoorAudio(medbayDoor, doorSlideClip);
            var cmdDoor = BuildSlidingDoor(interior, "CommandDoor", new Vector3(0f, 0f, 26f), 2.4f, true, startLocked: false);
            WireDoorAudio(cmdDoor, doorSlideClip);
            foreach (float dz in sideDoorZ)
            {
                var doorW = BuildSlidingDoor(interior, $"DoorW_z{dz}", new Vector3(-2f, 0f, dz), 2.4f, false, startLocked: false);
                WireDoorAudio(doorW, doorSlideClip);
                var doorE = BuildSlidingDoor(interior, $"DoorE_z{dz}", new Vector3(2f, 0f, dz), 2.4f, false, startLocked: false);
                WireDoorAudio(doorE, doorSlideClip);
            }

            // Game root: GameState + CombatFeedbackController (mirrors PopulateZoneGameplay).
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Build the player rig with locomotion. Bounds radius covers the full medbay->command run.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;

            // Belt katana: BuildSword wires the KatanaHolster on the rig.
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // NPCs: Kessler in medbay; Khall hologram in the command room (hidden until revealed).
            var kesslerPos = new Vector3(-2f, 1f, 2f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerNpcSo = new SerializedObject(kesslerNpc);
                kesslerNpcSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerNpcSo.FindProperty("remote").boolValue = false;
                kesslerNpcSo.ApplyModifiedPropertiesWithoutUndo();

                // Add wandering behavior (medbay-local wandering with default speeds).
                var wander = kesslerGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = 2f;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Holotable in the command room centre; Khall projects above it.
            var holotable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holotable.name = "Holotable";
            holotable.transform.SetParent(interior, false);
            holotable.transform.localPosition = new Vector3(0f, 0.5f, 35f);
            holotable.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            TintShared(holotable.GetComponent<Renderer>(), new Color(0.15f, 0.18f, 0.22f));

            var khallPos = new Vector3(0f, 1.4f, 35f);
            var khallGo = InstantiateNpc(ArtPrefabBuilder.KhallHologramPrefabPath, khallPos, "Khall");
            if (khallGo != null)
            {
                var khallNpc = khallGo.AddComponent<StoryNpc>();
                var khallNpcSo = new SerializedObject(khallNpc);
                khallNpcSo.FindProperty("displayName").stringValue = "Khall";
                khallNpcSo.FindProperty("remote").boolValue = true;
                khallNpcSo.ApplyModifiedPropertiesWithoutUndo();

                // Add audio for the hologram activation.
                var hologramAudio = khallGo.AddComponent<AudioSource>();
                var hologramClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/HologramOn.wav");
                if (hologramClip != null)
                {
                    hologramAudio.clip = hologramClip;
                    hologramAudio.playOnAwake = true;
                    hologramAudio.spatialBlend = 1f; // 3D spatial
                }

                khallGo.SetActive(false); // revealed by the Trigger step when the player reaches the command room
            }

            // Three Empire troopers in the medbay (initially inactive, activated by MissionDirector).
            var trooperPositions = new Vector3[]
            {
                new Vector3(1f, 0f, 3f),
                new Vector3(-1f, 0f, 3f),
                new Vector3(0f, 0f, 3.5f)
            };
            var trooperHealths = new List<Object>();
            foreach (var trooperPos in trooperPositions)
            {
                // Human armored soldiers (the DominionTrooper prefab), not the samurai EnemyFoot —
                // EP01's medbay foes are human Empire troops. Inactive until the DefeatEnemies step.
                var enemy = BuildDominionEnemy(trooperPos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                trooperHealths.Add(enemy.GetComponent<Health>());
            }

            // Kessler's walk: an inactive walker that, when activated, leads Kessler from the medbay
            // down the corridor to the command room. Waypoints are child transforms.
            var walkerGo = new GameObject("KesslerWalker");
            walkerGo.transform.SetParent(interior, false);
            var waypointPositions = new Vector3[]
            {
                new Vector3(0f, 1f, 5.5f),
                new Vector3(0f, 1f, 16f),
                new Vector3(0f, 1f, 26f),
                new Vector3(2f, 1f, 33f)
            };
            var waypointTransforms = new Transform[waypointPositions.Length];
            for (int i = 0; i < waypointPositions.Length; i++)
            {
                var wp = new GameObject($"WP{i}");
                wp.transform.SetParent(walkerGo.transform, false);
                wp.transform.localPosition = waypointPositions[i];
                waypointTransforms[i] = wp.transform;
            }
            var walker = walkerGo.AddComponent<NpcWalker>();
            var walkerSo = new SerializedObject(walker);
            if (kesslerGo != null) SetObjectRef(walkerSo, "target", kesslerGo.transform);
            var wpProp = walkerSo.FindProperty("waypoints");
            wpProp.arraySize = waypointTransforms.Length;
            for (int i = 0; i < waypointTransforms.Length; i++)
                wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypointTransforms[i];
            walkerSo.ApplyModifiedPropertiesWithoutUndo();
            walkerGo.SetActive(false); // activated by the Trigger step after the fight

            // Command-room reach trigger point (no collider needed, just a position reference).
            var commandReachGo = new GameObject("CommandReachPoint");
            commandReachGo.transform.position = new Vector3(0f, 1f, 33f);

            // GO TO SPACE button canvas (worldspace, in the command room, facing the entering player).
            var canvasGo = new GameObject("GO TO SPACE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var canvasRt = canvas.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(600f, 200f);
            canvasRt.localScale = Vector3.one * 0.001f;
            canvasRt.position = new Vector3(0f, 1.2f, 36.5f);
            canvasRt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);

            var mainMenuCtrl = canvasGo.AddComponent<MainMenuController>();
            var goToSpaceBtn = MenuMakeButton(canvasRt, "GO TO SPACE", Vector2.zero);
            UnityEventTools.AddPersistentListener(goToSpaceBtn.onClick,
                new UnityEngine.Events.UnityAction(mainMenuCtrl.OnGoToSpaceClicked));
            canvasGo.SetActive(false); // MissionDirector shows it at the Prompt step.

            // Create dialogue players for each mission beat. Y (Left Hand/Talk) advances each line.
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var beat2Dialogue = BuildDialoguePlayer("Dialogue_Beat2", kesslerPos, "ship_beat2", talkRef);
            var beat3BarksDialogue = BuildDialoguePlayer("Dialogue_ShipBarks", kesslerPos, "ship_beat3_barks");
            var beat4Dialogue = BuildDialoguePlayer("Dialogue_Beat4", kesslerPos, "ship_beat4", talkRef);
            var beat7Dialogue = BuildDialoguePlayer("Dialogue_Beat7", kesslerPos, "ship_beat7", talkRef);
            var beat56Dialogue = BuildDialoguePlayer("Dialogue_Beat56", new Vector3(0f, 1f, 34f), "ship_beat56", talkRef);

            // Set beat3_barks to play automatically when activated, with no advance action.
            var beat3So = new SerializedObject(beat3BarksDialogue);
            beat3So.FindProperty("playOnStart").boolValue = true;
            beat3So.ApplyModifiedPropertiesWithoutUndo();
            beat3BarksDialogue.gameObject.SetActive(false); // Trigger step activates it.

            // Mission Director: build the steps and wire them.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 10;

            // Step 0: Dialogue Beat2 (medbay wake).
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("label").stringValue = "Beat2: Medbay Wake";
            step0.FindPropertyRelative("dialogue").objectReferenceValue = beat2Dialogue;

            // Step 1: Trigger — activate beat3_barks dialogue (plays automatically during the fight).
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step1.FindPropertyRelative("label").stringValue = "Trigger: Combat Barks Begin";
            var t1 = step1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = beat3BarksDialogue.gameObject;

            // Step 2: DefeatEnemies (the 3 troopers).
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatEnemies;
            step2.FindPropertyRelative("label").stringValue = "DefeatEnemies: 3 Troopers";
            var en2 = step2.FindPropertyRelative("enemies");
            en2.arraySize = trooperHealths.Count;
            for (int i = 0; i < trooperHealths.Count; i++)
                en2.GetArrayElementAtIndex(i).objectReferenceValue = trooperHealths[i];

            // Step 3: Dialogue Beat4 (Kessler's confession).
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step3.FindPropertyRelative("label").stringValue = "Beat4: Kessler's Confession";
            step3.FindPropertyRelative("dialogue").objectReferenceValue = beat4Dialogue;

            // Step 4: Dialogue Beat7 (beacon discovery / the retrieval tag).
            var step4 = stepsProp.GetArrayElementAtIndex(4);
            step4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step4.FindPropertyRelative("label").stringValue = "Beat7: Beacon Discovery";
            step4.FindPropertyRelative("dialogue").objectReferenceValue = beat7Dialogue;

            // Step 5: Trigger — unlock the medbay door + start Kessler walking to the command room.
            var step5 = stepsProp.GetArrayElementAtIndex(5);
            step5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step5.FindPropertyRelative("label").stringValue = "Trigger: Open Medbay Door + Kessler Walks";
            var t5 = step5.FindPropertyRelative("triggerObjects");
            t5.arraySize = 2;
            t5.GetArrayElementAtIndex(0).objectReferenceValue = medbayDoor;
            t5.GetArrayElementAtIndex(1).objectReferenceValue = walkerGo;

            // Step 6: ReachTrigger (command room — player follows Kessler).
            var step6 = stepsProp.GetArrayElementAtIndex(6);
            step6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            step6.FindPropertyRelative("label").stringValue = "ReachTrigger: Command Room";
            step6.FindPropertyRelative("reachPoint").objectReferenceValue = commandReachGo.transform;
            step6.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 7: Trigger — reveal the Khall hologram on the holotable.
            var step7 = stepsProp.GetArrayElementAtIndex(7);
            step7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step7.FindPropertyRelative("label").stringValue = "Trigger: Khall Hologram Appears";
            var t7 = step7.FindPropertyRelative("triggerObjects");
            t7.arraySize = 1;
            t7.GetArrayElementAtIndex(0).objectReferenceValue = khallGo;

            // Step 8: Dialogue Beat5+6 (recognition flag triggers Khall — Dominion-side comms).
            var step8 = stepsProp.GetArrayElementAtIndex(8);
            step8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step8.FindPropertyRelative("label").stringValue = "Beat5+6: Recognition Flag / Khall";
            step8.FindPropertyRelative("dialogue").objectReferenceValue = beat56Dialogue;

            // Step 9: Prompt (GO TO SPACE canvas button).
            var step9 = stepsProp.GetArrayElementAtIndex(9);
            step9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step9.FindPropertyRelative("label").stringValue = "Prompt: GO TO SPACE";
            step9.FindPropertyRelative("promptObject").objectReferenceValue = canvasGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Maintenance drones + holo display: small cosmetic movers so the ship reads as "alive".
            // Medbay drone bobs/spins in place up high, clear of the NPCs & troopers.
            var medbayDrone = BuildShipDrone(interior, "MedbayDrone", new Vector3(3f, 2.2f, 1f), new Color(0.3f, 0.9f, 1f));
            medbayDrone.AddComponent<Ronin7.World.Story.FloatingArrow>();

            // Corridor drone orbits a hub node at the corridor centre (PlanetOrbit owns its position).
            var corridorHub = new GameObject("CorridorDroneHub");
            corridorHub.transform.SetParent(interior, false);
            corridorHub.transform.localPosition = new Vector3(0f, 2.3f, 16f);
            var corridorDrone = BuildShipDrone(interior, "CorridorDrone", new Vector3(1.3f, 2.3f, 16f), new Color(1f, 0.7f, 0.2f));
            var corridorOrbit = corridorDrone.AddComponent<Ronin7.Ship.PlanetOrbit>();
            var corridorOrbitSo = new SerializedObject(corridorOrbit);
            SetObjectRef(corridorOrbitSo, "center", corridorHub.transform);
            corridorOrbitSo.FindProperty("radius").floatValue = 1.3f;
            corridorOrbitSo.FindProperty("angularSpeedDeg").floatValue = 25f;
            corridorOrbitSo.FindProperty("yOffset").floatValue = 0f;
            corridorOrbitSo.FindProperty("startAngleDeg").floatValue = 0f;
            corridorOrbitSo.ApplyModifiedPropertiesWithoutUndo();

            // Command drone circles above the holotable (PlanetOrbit owns its position).
            var commandDrone = BuildShipDrone(interior, "CommandDrone", new Vector3(0f, 1.6f, 36.5f), new Color(0.3f, 0.9f, 1f));
            var commandOrbit = commandDrone.AddComponent<Ronin7.Ship.PlanetOrbit>();
            var commandOrbitSo = new SerializedObject(commandOrbit);
            SetObjectRef(commandOrbitSo, "center", holotable.transform);
            commandOrbitSo.FindProperty("radius").floatValue = 1.5f;
            commandOrbitSo.FindProperty("angularSpeedDeg").floatValue = 30f;
            commandOrbitSo.FindProperty("yOffset").floatValue = 1.1f;
            commandOrbitSo.FindProperty("startAngleDeg").floatValue = 90f;
            commandOrbitSo.ApplyModifiedPropertiesWithoutUndo();

            // Slowly rotating holographic element just above the holotable top.
            var holoDisplay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holoDisplay.name = "HolotableDisplay";
            holoDisplay.transform.SetParent(interior, false);
            holoDisplay.transform.localPosition = new Vector3(0f, 1.0f, 35f);
            holoDisplay.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
            TintShared(holoDisplay.GetComponent<Renderer>(), new Color(0.2f, 0.9f, 1f));
            Object.DestroyImmediate(holoDisplay.GetComponent<Collider>());
            holoDisplay.AddComponent<Ronin7.Ship.TurntableRotator>();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ep01ShipScenePath);
            EnsureScenesInBuild(Ep01ShipScenePath);

            Debug.Log($"[Space Samurai] EP01 Ship Interior scene built at {Ep01ShipScenePath}. " +
                      "Real-ship layout: enclosed medbay -> sliding door -> corridor with 7 rooms (6 themed side rooms + command room at the end). " +
                      "10 mission steps (Beat2 wake, combat barks, 3-trooper fight, Beat4 confession, Beat7 retrieval tag, open door + Kessler walks, " +
                      "reach command room, Khall appears, Beat56 recognition-flag dialogue, GO TO SPACE prompt). " +
                      "All doors have SFX. Kessler wanders the medbay. Khall plays hologram activation SFX. Medbay door starts locked until the fight is won.");
        }

        /// <summary>
        /// Kessler's mother ship as a static prop in the flight scene — the capital hull the player
        /// just launched from. Built from primitives under the moving <paramref name="universe"/> so it
        /// stays fixed in the world while the player flies past it. Purely visual: colliders stripped so
        /// it never interferes with bolts or the asteroid hazard.
        /// </summary>
        private static GameObject BuildKesslerMothership(Transform universe, Vector3 localPos, float scale)
        {
            var root = new GameObject("Kessler Mothership");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = localPos;
            root.transform.localScale = Vector3.one * scale;

            var worn  = new Color(0.5f, 0.45f, 0.4f);   // weathered hull (matches the Kessler NPC palette)
            var steel = new Color(0.55f, 0.58f, 0.62f);
            var brass = new Color(0.7f, 0.55f, 0.25f);
            var glow  = new Color(0.3f, 0.7f, 1f);      // lit windows / engine exhaust

            void Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
            }

            // Long main hull + lower keel.
            Part("Hull", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(12f, 6f, 34f), worn);
            Part("Keel", PrimitiveType.Cube, new Vector3(0f, -4f, -2f), new Vector3(8f, 3f, 26f), steel);
            // Brass mid-band for read.
            Part("Band", PrimitiveType.Cube, new Vector3(0f, 0f, 2f), new Vector3(12.4f, 1.4f, 4f), brass);
            // Bridge tower + lit canopy, forward-top.
            Part("Bridge", PrimitiveType.Cube, new Vector3(0f, 5f, 11f), new Vector3(6f, 5f, 7f), steel);
            Part("BridgeGlass", PrimitiveType.Cube, new Vector3(0f, 6f, 14.3f), new Vector3(5f, 2f, 0.4f), glow);
            // Side pods.
            Part("PodL", PrimitiveType.Cube, new Vector3(-8f, 0f, -4f), new Vector3(3f, 4f, 16f), steel);
            Part("PodR", PrimitiveType.Cube, new Vector3(8f, 0f, -4f), new Vector3(3f, 4f, 16f), steel);
            // Three rear engine blocks + exhaust glow.
            Part("EngineL", PrimitiveType.Cube, new Vector3(-4.5f, 0f, -18f), new Vector3(3f, 3f, 4f), steel);
            Part("EngineC", PrimitiveType.Cube, new Vector3(0f, 0f, -18f), new Vector3(3f, 3f, 4f), steel);
            Part("EngineR", PrimitiveType.Cube, new Vector3(4.5f, 0f, -18f), new Vector3(3f, 3f, 4f), steel);
            Part("ExhaustL", PrimitiveType.Cube, new Vector3(-4.5f, 0f, -20.2f), new Vector3(2.2f, 2.2f, 0.4f), glow);
            Part("ExhaustC", PrimitiveType.Cube, new Vector3(0f, 0f, -20.2f), new Vector3(2.2f, 2.2f, 0.4f), glow);
            Part("ExhaustR", PrimitiveType.Cube, new Vector3(4.5f, 0f, -20.2f), new Vector3(2.2f, 2.2f, 0.4f), glow);
            // Forward comms dish.
            Part("CommsDish", PrimitiveType.Sphere, new Vector3(0f, 3.5f, 17.5f), new Vector3(2.5f, 2.5f, 1f), brass);

            return root;
        }

        /// <summary>Replaces the command room's back wall (z=42) with a bridge windshield: a structural
        /// frame around a large transparent canopy, backed by a big emissive galaxy backdrop quad set
        /// further out so the player reads it as the view of Galaxy 1 outside the ship.</summary>
        private static void BuildCommandWindshield(Transform parent, Material galaxyMat)
        {
            var rootGo = new GameObject("Command_Windshield");
            var root = rootGo.transform;
            root.SetParent(parent, false);

            // Structural frame (keeps colliders so the player can't walk out): side pillars, top header,
            // bottom sill. This frames a window opening of roughly x[-8,8], y[0.6, RoomH-0.5].
            BuildWall(root, "Windshield_Frame_PillarW", new Vector3(-8f, RoomH/2f, 42f), new Vector3(0.4f, RoomH, 0.3f));
            BuildWall(root, "Windshield_Frame_PillarE", new Vector3(8f, RoomH/2f, 42f), new Vector3(0.4f, RoomH, 0.3f));
            BuildWall(root, "Windshield_Frame_Header", new Vector3(0f, RoomH - 0.25f, 42f), new Vector3(18f, 0.5f, 0.3f));
            BuildWall(root, "Windshield_Frame_Sill", new Vector3(0f, 0.3f, 42f), new Vector3(18f, 0.6f, 0.3f));
            // Vertical mullion dividers across the opening for a canopy look.
            BuildWall(root, "Windshield_Frame_MullionL", new Vector3(-2.7f, RoomH/2f, 42f), new Vector3(0.15f, RoomH, 0.25f));
            BuildWall(root, "Windshield_Frame_MullionR", new Vector3(2.7f, RoomH/2f, 42f), new Vector3(0.15f, RoomH, 0.25f));

            // Invisible barrier sealing the FULL opening (x[-9,9]) so the player can't walk out into
            // z>42 (the floor ends at z=42). We keep its collider but strip the renderer — a visible
            // opaque pane would hide the galaxy, and we want a clear "open canopy" view through to it.
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Windshield_Glass";
            glass.transform.SetParent(root, false);
            glass.transform.localPosition = new Vector3(0f, RoomH/2f, 42f);
            glass.transform.localScale = new Vector3(18f, RoomH, 0.05f);
            Object.DestroyImmediate(glass.GetComponent<MeshRenderer>());

            // Galaxy backdrop: a large quad set further out (z=45), sized larger than the opening so its
            // edges hide behind the frame and it reads as distant. Purely visual (no collider).
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "GalaxyBackdrop";
            backdrop.transform.SetParent(root, false);
            backdrop.transform.localPosition = new Vector3(0f, RoomH/2f, 45f);
            backdrop.transform.localScale = new Vector3(22f, 6f, 0.1f);
            // A Unity Quad's front face normal points -Z; the player stands on the -Z side looking +Z,
            // so identity rotation already faces the lit side at the player. (No 180° flip — that would
            // turn the back-culled face toward the player and show nothing.)
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            var backdropRenderer = backdrop.GetComponent<Renderer>();
            if (galaxyMat != null)
                backdropRenderer.sharedMaterial = galaxyMat;
            else
                TintShared(backdropRenderer, new Color(0.12f, 0.10f, 0.28f)); // deep-space fallback tint
        }

        /// <summary>
        /// A side room off the corridor: 3 outer walls + floor/ceiling + a back-wall label and a couple
        /// of props. The corridor-facing wall (with its door gap) is built by <see cref="BuildCorridorWall"/>.
        /// </summary>
        private static void BuildRoomShell(Transform parent, string name, float corridorX, bool west, float doorZ,
            float halfW, float depth, Color floorColor, Color ceilColor, string label, Color accent)
        {
            float sign = west ? -1f : 1f;
            float backX = sign * (corridorX + depth);
            float centerX = sign * (corridorX + depth / 2f);

            BuildFloorCeiling(parent, name, new Vector3(centerX, 0f, doorZ), new Vector3(depth, 0f, halfW * 2f), floorColor, ceilColor);
            BuildWall(parent, name + "_Back", new Vector3(backX, RoomH/2f, doorZ), new Vector3(0.2f, RoomH, halfW * 2f));
            BuildWall(parent, name + "_SideA", new Vector3(centerX, RoomH/2f, doorZ - halfW), new Vector3(depth, RoomH, 0.2f));
            BuildWall(parent, name + "_SideB", new Vector3(centerX, RoomH/2f, doorZ + halfW), new Vector3(depth, RoomH, 0.2f));

            // Label on the back wall, facing the room interior / corridor.
            var labelGo = new GameObject(name + "_Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(backX - sign * 0.15f, 2.3f, doorZ);
            labelGo.transform.localRotation = Quaternion.Euler(0f, west ? -90f : 90f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.05f;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.color = new Color(0.8f, 0.9f, 1f);

            // A couple of generic props tinted with the room accent.
            BuildProp(parent, name + "_Prop1", new Vector3(backX - sign * 0.6f, 0.5f, doorZ - halfW * 0.5f), new Vector3(0.8f, 1f, 0.8f), accent);
            BuildProp(parent, name + "_Prop2", new Vector3(backX - sign * 0.6f, 0.35f, doorZ + halfW * 0.5f), new Vector3(0.8f, 0.7f, 0.8f), accent);

            // Set-dressing to make the room feel lived-in. Floor center/half-extents match BuildFloorCeiling above.
            BuildRoomDetails(parent, name, new Vector3(centerX, 0f, doorZ), new Vector2(depth / 2f, halfW), accent);
        }

        /// <summary>Builds a small emissive "maintenance drone" primitive (no collider — purely cosmetic)
        /// parented under <paramref name="parent"/>. The caller attaches a mover (FloatingArrow / PlanetOrbit).
        /// Returns the drone GameObject.</summary>
        private static GameObject BuildShipDrone(Transform parent, string name, Vector3 localPos, Color glow)
        {
            var drone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drone.name = name;
            drone.transform.SetParent(parent, false);
            drone.transform.localPosition = localPos;
            drone.transform.localScale = Vector3.one * 0.25f;
            TintShared(drone.GetComponent<Renderer>(), glow);
            Object.DestroyImmediate(drone.GetComponent<Collider>());

            // Tiny antenna nub for silhouette (also cosmetic, no collider).
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cube);
            antenna.name = "Antenna";
            antenna.transform.SetParent(drone.transform, false);
            antenna.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            antenna.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
            TintShared(antenna.GetComponent<Renderer>(), glow);
            Object.DestroyImmediate(antenna.GetComponent<Collider>());

            return drone;
        }

        // Named-cast prefabs baked from Data/CharacterSpecs by "Build Characters from Specs Folder".
        // These are FEET-pivot (parts authored from y≈0 up), so place them at floor height (y=0) —
        // unlike ArtPrefabBuilder.KesslerPrefabPath, whose root is a body capsule placed at y=1.
        private const string GeneratedCharFolder = "Assets/Ronin7/Prefabs/Art/Generated";
        private const string ReshPrefabPath    = GeneratedCharFolder + "/Resh.prefab";
        private const string IrisPrefabPath    = GeneratedCharFolder + "/Iris.prefab";
        private const string KhallPrefabPath   = GeneratedCharFolder + "/Khall.prefab";
        private const string DrHerisPrefabPath = GeneratedCharFolder + "/DrHeris.prefab";
        private const string ChildPrefabPath   = GeneratedCharFolder + "/Cassie04.prefab";

        /// <summary>Drops a capsule-frame position (pivot at y≈1) to the floor for feet-pivot prefabs.</summary>
        private static Vector3 AtFloor(Vector3 p) => new Vector3(p.x, 0f, p.z);

        /// <summary>
        /// Scatters decorative character prefabs (baked by "Build Characters from Specs Folder" into
        /// Prefabs/Art/Generated/) at the given <paramref name="positions"/> for crowd flavour. Purely
        /// visual — no StoryNpc, dialogue, arrow, or combat. Which prefabs count as "decorative" is read
        /// from the Data/CharacterSpecs/Decorative specs; the named story cast is skipped so the crowd
        /// never duplicates them. Degrades gracefully (logs) if fewer prefabs are baked than positions
        /// requested. Returns the count actually placed.
        /// </summary>
        private static int PlaceDecorativeCrowd(Vector3[] positions)
        {
            const string GeneratedFolder = "Assets/Ronin7/Prefabs/Art/Generated";
            const string DecorativeSpecs = "Assets/Ronin7/Data/CharacterSpecs/Decorative";
            var skip = new HashSet<string> { "Kessler", "Khall", "Iris" }; // named cast placed by story scenes

            string osFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), DecorativeSpecs);
            if (!System.IO.Directory.Exists(osFolder))
            {
                Debug.LogWarning($"[Space Samurai] No decorative specs at {DecorativeSpecs}; skipping crowd.");
                return 0;
            }

            int placed = 0;
            foreach (string file in System.IO.Directory.GetFiles(osFolder, "*.json", System.IO.SearchOption.TopDirectoryOnly))
            {
                if (placed >= positions.Length) break;

                ArtPrefabBuilder.CharacterSpec spec;
                try { spec = JsonUtility.FromJson<ArtPrefabBuilder.CharacterSpec>(System.IO.File.ReadAllText(file)); }
                catch { continue; }
                if (spec == null || string.IsNullOrEmpty(spec.name) || skip.Contains(spec.name)) continue;

                // The baker names the prefab from the sanitized spec name, not the JSON filename.
                string prefabPath = $"{GeneratedFolder}/{SanitizeAssetName(spec.name)}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) continue; // not baked yet

                var p = positions[placed];
                InstantiateNpc(prefabPath, new Vector3(p.x, 0f, p.z), $"Decorative_{spec.name}");
                placed++;
            }

            if (placed < positions.Length)
                Debug.LogWarning($"[Space Samurai] Placed {placed}/{positions.Length} decorative NPCs " +
                                 "— run 'Tools/Space Samurai/Art/Build Characters from Specs Folder' first to bake them all.");
            return placed;
        }

        // Mirrors the private Sanitize in ArtPrefabBuilder.CharacterGen so derived prefab paths match
        // the baker's output names exactly (letters/digits kept, everything else collapsed to '_').
        private static string SanitizeAssetName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "GeneratedCharacter";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            string r = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(r) ? "GeneratedCharacter" : r;
        }

        // Dialogue line data now comes from Ep01Lines.cs (centralized, canonical source).

        // Hideout and Galaxy 1 dialogue data now comes from Ep01Lines.cs (centralized, canonical source).

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP01 Planet Zone", priority = 66)]
        public static void BuildEp01PlanetZone()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var zoneDef = EnsureZoneDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Water-theme visuals: a settlement market on the water-mining world.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.3f);
            RenderSettings.skybox = EnsureThemedSkybox("EP01Water", new Color(0.2f, 0.4f, 0.7f));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(zoneDef.radius * 0.5f, 1f, zoneDef.radius * 0.5f);
            TintShared(floor.GetComponent<Renderer>(), new Color(0.3f, 0.34f, 0.4f));

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.92f, 1f);
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var propRoot = new GameObject("Theme Props").transform;
            BuildWaterProps(propRoot, zoneDef.radius);
            BuildMarketStalls(propRoot);

            // Game root: GameState + CombatFeedbackController.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Build the player rig with locomotion.
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = zoneDef.radius;

            // Sword and boundary.
            BuildSword(new Vector3(-0.4f, 1.0f, 0.5f), weapon);
            BuildBoundary(zoneDef.radius);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // Three market NPCs (Kessler + two locals). The player talks to each with Y to learn
            // where the Dominion runs its operation. Each gets a dialogue, a StoryNpc, and a floating
            // arrow that disappears once talked to.
            var npcSpecs = new (string name, string prefab, Vector3 pos, string setId, float wanderRadius)[]
            {
                ("Kessler",       ArtPrefabBuilder.KesslerPrefabPath,         new Vector3(-3f, 1f, 4f),  "market_kessler",     1.5f),
                ("Dock Hand",     GeneratedCharFolder + "/Tidecaller.prefab", new Vector3(3f, 0f, 4.5f), "market_dockhand",    2.5f),
                ("Market Trader", GeneratedCharFolder + "/Aureling.prefab",   new Vector3(0f, 0f, 7.5f), "market_trader",      2.5f),
            };

            var marketNpcs = new List<StoryNpc>();
            foreach (var spec in npcSpecs)
            {
                var npcGo = InstantiateNpc(spec.prefab, spec.pos, spec.name);
                if (npcGo.name.EndsWith("_Placeholder"))
                    npcGo.transform.position = new Vector3(spec.pos.x, 1f, spec.pos.z); // lift capsule placeholders onto the floor

                var dialogue = BuildDialoguePlayer($"Dialogue_{spec.name.Replace(" ", "")}", spec.pos + new Vector3(0f, 0.5f, 0f), spec.setId, talkRef);
                var arrow = BuildNpcArrow(npcGo.transform);

                var npc = npcGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(npc);
                npcSo.FindProperty("displayName").stringValue = spec.name;
                npcSo.FindProperty("remote").boolValue = false;
                SetObjectRef(npcSo, "dialogue", dialogue);
                SetObjectRef(npcSo, "talkArrow", arrow);
                npcSo.ApplyModifiedPropertiesWithoutUndo();
                marketNpcs.Add(npc);

                // Add StoryNpcWander for ambient wandering.
                var wander = npcGo.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = spec.wanderRadius;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Decorative crowd: scatter baked character prefabs around the market for life. Purely
            // visual — no dialogue/combat — kept clear of the player spawn (origin), the 3 story NPCs
            // (z≈4–7.5), and the ENTER HIDEOUT box (z=10).
            var decorativePositions = new Vector3[]
            {
                new Vector3(-7f, 0f, 2f),  new Vector3(7f, 0f, 2f),
                new Vector3(-9f, 0f, 6f),  new Vector3(9f, 0f, 6f),
                new Vector3(-6f, 0f, 11f), new Vector3(6f, 0f, 11f),
                new Vector3(-3f, 0f, 13f), new Vector3(3f, 0f, 13f),
                new Vector3(-8f, 0f, -3f), new Vector3(8f, 0f, -3f),
            };
            int decorativeCount = PlaceDecorativeCrowd(decorativePositions);

            // "Press Y to talk" pop-up + the talk interactor on the rig.
            var talkPrompt = BuildWorldPromptText("TalkPrompt", new Vector3(0f, 2f, 4f), new Color(0.9f, 0.95f, 1f));
            var talkInteractor = rig.AddComponent<TalkInteractor>();
            var tiSo = new SerializedObject(talkInteractor);
            SetObjectRef(tiSo, "talkAction", talkRef);
            SetObjectRef(tiSo, "promptText", talkPrompt);
            tiSo.ApplyModifiedPropertiesWithoutUndo();

            // ENTER HIDEOUT box (worldspace, revealed after all three dialogues). Pressing it loads the
            // hideout scene via StoryTransition -> LandingRequested.
            var boxGo = BuildTransitionBox("ENTER HIDEOUT Box", new Vector3(0f, 1.2f, 10f), "ENTER THE HIDEOUT",
                out var enterBtn, out var enterTransition);
            var stSo = new SerializedObject(enterTransition);
            stSo.FindProperty("onFootScene").stringValue = Galaxy1Ep01HideoutSceneName;
            stSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(enterBtn.onClick,
                new UnityEngine.Events.UnityAction(enterTransition.LoadOnFootScene));
            boxGo.SetActive(false);

            // Mission Director: talk to all three NPCs, then reveal the hideout box.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.TalkToNpcs;
            step0.FindPropertyRelative("label").stringValue = "TalkToNpcs: Market (Kessler + 2 locals)";
            var npcsProp = step0.FindPropertyRelative("npcs");
            npcsProp.arraySize = marketNpcs.Count;
            for (int i = 0; i < marketNpcs.Count; i++)
                npcsProp.GetArrayElementAtIndex(i).objectReferenceValue = marketNpcs[i];

            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            step1.FindPropertyRelative("label").stringValue = "Trigger: Reveal ENTER HIDEOUT box";
            var t1 = step1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = boxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure (the box button is ray-clickable).
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep01PlanetScenePath);
            EnsureScenesInBuild(Galaxy1Ep01PlanetScenePath);

            Debug.Log($"[Space Samurai] EP01 Market scene built at {Galaxy1Ep01PlanetScenePath}. " +
                      "Talk to 3 NPCs (Kessler + 2 locals) with Y; each has a floating arrow + 'Press Y to talk' pop-up. " +
                      $"+{decorativeCount} decorative NPCs around the market. " +
                      "After all 3, the ENTER HIDEOUT box appears and loads " + Galaxy1Ep01HideoutSceneName + ".");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP01 Hideout", priority = 67)]
        public static void BuildEp01Hideout()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Dim Dominion-outpost interior.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.78f, 0.95f);
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -20f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.15f, 0.18f);
            BuildAccentPointLight("EntryLight", new Vector3(0f, 2.6f, 2f), new Color(1f, 0.7f, 0.5f), 1.8f, 12f);
            BuildAccentPointLight("CorridorLight", new Vector3(0f, 2.6f, 24f), new Color(0.6f, 0.78f, 1f), 1.4f, 14f);
            BuildAccentPointLight("CommandLight", new Vector3(0f, 2.6f, 48f), new Color(0.4f, 0.85f, 1f), 2.4f, 16f);

            // ---- Hideout geometry: entry hall (z<8) -> long corridor (z[8,40]) -> command room (z[40,52]). ----
            var interiorGo = new GameObject("Hideout");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.18f, 0.19f, 0.22f);
            var ceilColor = new Color(0.1f, 0.11f, 0.13f);

            BuildFloorCeiling(interior, "Entry", new Vector3(0f, 0f, 2f), new Vector3(10f, 0f, 12f), floorColor, ceilColor);
            BuildWall(interior, "Entry_WallW", new Vector3(-5f, 1.5f, 2f), new Vector3(0.2f, 3f, 12f));
            BuildWall(interior, "Entry_WallE", new Vector3(5f, 1.5f, 2f), new Vector3(0.2f, 3f, 12f));
            BuildWall(interior, "Entry_WallFront", new Vector3(0f, 1.5f, -4f), new Vector3(10f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Entry_WallBack", new Vector3(0f, 1.5f, 8f), 10f, true, 2.4f);

            BuildFloorCeiling(interior, "Corridor", new Vector3(0f, 0f, 24f), new Vector3(4f, 0f, 32f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -2f, 8f, 40f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 2f, 8f, 40f, new float[0], 2.4f);

            BuildFloorCeiling(interior, "Command", new Vector3(0f, 0f, 46f), new Vector3(14f, 0f, 12f), floorColor, ceilColor);
            BuildWall(interior, "Command_WallW", new Vector3(-7f, 1.5f, 46f), new Vector3(0.2f, 3f, 12f));
            BuildWall(interior, "Command_WallE", new Vector3(7f, 1.5f, 46f), new Vector3(0.2f, 3f, 12f));
            BuildWall(interior, "Command_WallBack", new Vector3(0f, 1.5f, 52f), new Vector3(14f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Command_WallFront", new Vector3(0f, 1.5f, 40f), 14f, true, 2.4f);

            BuildSlidingDoor(interior, "EntryDoor", new Vector3(0f, 0f, 8f), 2.4f, true, startLocked: false);
            BuildSlidingDoor(interior, "CommandDoor", new Vector3(0f, 0f, 40f), 2.4f, true, startLocked: false);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds covering the full run.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(-0.4f, 1.0f, 0.5f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var hackRef = FindRef(refs, "Right Hand", "Hack");

            // Nine Dominion troopers in the corridor, divided into 3 waves. All start inactive and are
            // activated as the player advances through the EnemyWaveSpawner.
            // Wave 1: 2 troopers around z 18–24
            var wave1Positions = new Vector3[]
            {
                new Vector3(-1f, 0f, 19f),
                new Vector3(1f, 0f, 22f),
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 3 troopers around z 26–32
            var wave2Positions = new Vector3[]
            {
                new Vector3(-1f, 0f, 26f),
                new Vector3(0f, 0f, 29f),
                new Vector3(1f, 0f, 32f),
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 3: 4 troopers around z 34–38
            // x kept within ±1.5: the corridor walls sit at x=±2 (inner face ~±1.9), so ±2 would bury
            // the trooper bodies in the wall.
            var wave3Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 34f),
                new Vector3(-0.5f, 0f, 36f),
                new Vector3(0.5f, 0f, 36f),
                new Vector3(1.5f, 0f, 38f),
            };
            var wave3Healths = new List<Health>();
            foreach (var pos in wave3Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                wave3Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave bark dialogues. They stay ACTIVE with playOnStart=false: EnemyWaveSpawner.StartWave
            // calls bark.Play() directly when each wave activates. (Play() can't start a coroutine on an
            // inactive GameObject, and an active playOnStart bark would fire all three at scene start —
            // so neither inactive nor playOnStart is correct here. The panel is hidden in Awake, so an
            // idle active bark shows nothing until its wave drives Play().)
            var wave1Bark = BuildDialoguePlayer("Dialogue_Wave1Bark", new Vector3(0f, 1.5f, 20f), "hideout_wave1_bark");
            var wave2Bark = BuildDialoguePlayer("Dialogue_Wave2Bark", new Vector3(0f, 1.5f, 28f), "hideout_wave2_bark");
            var wave3Bark = BuildDialoguePlayer("Dialogue_Wave3Bark", new Vector3(0f, 1.5f, 36f), "hideout_wave3_bark");

            // Wave spawner: triggers when player enters the corridor (z=14), activates each wave in sequence.
            var waveSpawnerGo = new GameObject("WaveSpawner");
            var waveSpawner = waveSpawnerGo.AddComponent<EnemyWaveSpawner>();
            var triggerPointGo = new GameObject("TriggerPoint");
            triggerPointGo.transform.position = new Vector3(0f, 1f, 14f);

            var waveSpawnerSo = new SerializedObject(waveSpawner);
            SetObjectRef(waveSpawnerSo, "triggerPoint", triggerPointGo.transform);
            waveSpawnerSo.FindProperty("triggerRadius").floatValue = 3f;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                waveSpawnerSo.FindProperty("waveSting").objectReferenceValue = waveSting;

            // Wire the waves array via serialized properties.
            var wavesProp = waveSpawnerSo.FindProperty("waves");
            wavesProp.arraySize = 3;

            // Wave 0
            var wave0 = wavesProp.GetArrayElementAtIndex(0);
            var wave0Enemies = wave0.FindPropertyRelative("enemies");
            wave0Enemies.arraySize = wave1Healths.Count;
            for (int i = 0; i < wave1Healths.Count; i++)
                wave0Enemies.GetArrayElementAtIndex(i).objectReferenceValue = wave1Healths[i];
            wave0.FindPropertyRelative("bark").objectReferenceValue = wave1Bark;

            // Wave 1
            var wave1 = wavesProp.GetArrayElementAtIndex(1);
            var wave1Enemies = wave1.FindPropertyRelative("enemies");
            wave1Enemies.arraySize = wave2Healths.Count;
            for (int i = 0; i < wave2Healths.Count; i++)
                wave1Enemies.GetArrayElementAtIndex(i).objectReferenceValue = wave2Healths[i];
            wave1.FindPropertyRelative("bark").objectReferenceValue = wave2Bark;

            // Wave 2
            var wave2 = wavesProp.GetArrayElementAtIndex(2);
            var wave2Enemies = wave2.FindPropertyRelative("enemies");
            wave2Enemies.arraySize = wave3Healths.Count;
            for (int i = 0; i < wave3Healths.Count; i++)
                wave2Enemies.GetArrayElementAtIndex(i).objectReferenceValue = wave3Healths[i];
            wave2.FindPropertyRelative("bark").objectReferenceValue = wave3Bark;

            // Add AudioSource to the spawner for wave stings.
            var waveAudio = waveSpawnerGo.AddComponent<AudioSource>();
            waveAudio.spatialBlend = 0f; // 2D/near-player
            waveAudio.playOnAwake = false;
            SetObjectRef(waveSpawnerSo, "audioSource", waveAudio);

            waveSpawnerSo.ApplyModifiedPropertiesWithoutUndo();

            // Command terminal + hologram in the command room.
            var terminalPos = new Vector3(0f, 0.6f, 50f);
            var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = "Command Terminal";
            terminal.transform.SetParent(interior, false);
            terminal.transform.localPosition = terminalPos;
            terminal.transform.localScale = new Vector3(1.4f, 1.2f, 0.5f);
            TintShared(terminal.GetComponent<Renderer>(), new Color(0.12f, 0.16f, 0.22f));

            var hologramRoot = BuildHologram(interior, terminalPos + new Vector3(0f, 1.4f, -0.6f));
            hologramRoot.SetActive(false);

            var hackPrompt = BuildWorldPromptText("HackPrompt", terminalPos + new Vector3(0f, 1.4f, -0.9f), new Color(0.5f, 0.95f, 1f));
            var revealDialogue = BuildDialoguePlayer("Dialogue_HackReveal", terminalPos + new Vector3(0f, 1.6f, -1f), "hideout_hack", talkRef);

            var hackTerminalGo = new GameObject("HackTerminal");
            hackTerminalGo.transform.SetParent(interior, false);
            hackTerminalGo.transform.localPosition = terminalPos;
            var hackTerminal = hackTerminalGo.AddComponent<HackTerminal>();
            var htSo = new SerializedObject(hackTerminal);
            SetObjectRef(htSo, "hackAction", hackRef);
            SetObjectRef(htSo, "hologramRoot", hologramRoot);
            SetObjectRef(htSo, "promptText", hackPrompt);
            SetObjectRef(htSo, "revealDialogue", revealDialogue);

            // Wire SFX to HackTerminal.
            var hackLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/HackLoop.wav");
            var hackSuccessClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/HackSuccess.wav");
            if (hackLoopClip != null)
                htSo.FindProperty("hackLoopClip").objectReferenceValue = hackLoopClip;
            if (hackSuccessClip != null)
                htSo.FindProperty("hackSuccessClip").objectReferenceValue = hackSuccessClip;
            var hackAudio = hackTerminalGo.AddComponent<AudioSource>();
            hackAudio.spatialBlend = 1f; // 3D spatial
            hackAudio.playOnAwake = false;
            SetObjectRef(htSo, "audioSource", hackAudio);

            // Wire SFX to the hologram (plays when revealed).
            var hologramAudio = hologramRoot.AddComponent<AudioSource>();
            var hologramClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/HologramOn.wav");
            if (hologramClip != null)
            {
                hologramAudio.clip = hologramClip;
                hologramAudio.playOnAwake = true;
                hologramAudio.spatialBlend = 1f; // 3D spatial
            }

            htSo.ApplyModifiedPropertiesWithoutUndo();

            // Intro comms dialogue (Kessler on the radio — he's with the ship, not in the hideout).
            var introDialogue = BuildDialoguePlayer("Dialogue_HideoutIntro", new Vector3(0f, 1.5f, 2f), "hideout_intro", talkRef);

            // Command-room reach point.
            var commandReachGo = new GameObject("CommandReachPoint");
            commandReachGo.transform.position = new Vector3(0f, 1f, 46f);

            // LAUNCH TO SPACE box (revealed after the hack). Pressing it returns to space combat.
            var boxGo = BuildTransitionBox("LAUNCH TO SPACE Box", new Vector3(0f, 1.2f, 47f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            boxGo.SetActive(false);

            // Mission Director: intro -> defeat 3 waves -> reach command room -> hack -> escape dialogue -> reveal launch box.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Hideout Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = introDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Waves (9 Troopers)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = waveSpawner;
            // The spawner is armed at runtime by MissionDirector.BeginDefeatWaves (which calls Begin());
            // it must NOT be started here at edit time.

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Command Room";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = commandReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 4f;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Hack;
            s3.FindPropertyRelative("label").stringValue = "Hack: Command Terminal";
            s3.FindPropertyRelative("hackTerminal").objectReferenceValue = hackTerminal;
            // The terminal is armed at runtime by MissionDirector.BeginHack (which calls Activate());
            // it must NOT be armed here at edit time.

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s4.FindPropertyRelative("label").stringValue = "Trigger: Reveal LAUNCH TO SPACE box";
            var t4 = s4.FindPropertyRelative("triggerObjects");
            t4.arraySize = 1;
            t4.GetArrayElementAtIndex(0).objectReferenceValue = boxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep01HideoutScenePath);
            EnsureScenesInBuild(Galaxy1Ep01HideoutScenePath);

            Debug.Log($"[Space Samurai] EP01 Hideout scene built at {Galaxy1Ep01HideoutScenePath}. " +
                      "Intro comms → 9 Dominion troopers in 3 waves (corridor) with combat barks → reach command room → hack terminal (reveals Iris → Velorum) with SFX → " +
                      "LAUNCH TO SPACE returns to space combat. All SFX wired (wave alarm, hack loop/success, hologram).");
        }

        /// <summary>A row of simple market stalls (canopies on posts) around the spawn plaza.</summary>
        private static void BuildMarketStalls(Transform parent)
        {
            var stallPositions = new (Vector3 pos, Color color)[]
            {
                (new Vector3(-5f, 0f, 3f),  new Color(0.5f, 0.3f, 0.25f)),
                (new Vector3(5f, 0f, 3f),   new Color(0.3f, 0.4f, 0.35f)),
                (new Vector3(-5f, 0f, 8f),  new Color(0.35f, 0.3f, 0.45f)),
                (new Vector3(5f, 0f, 8f),   new Color(0.45f, 0.4f, 0.25f)),
            };
            foreach (var (pos, color) in stallPositions)
            {
                var stall = new GameObject("Stall").transform;
                stall.SetParent(parent, false);
                stall.localPosition = pos;

                var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counter.name = "Counter";
                counter.transform.SetParent(stall, false);
                counter.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                counter.transform.localScale = new Vector3(2.4f, 1f, 1f);
                TintShared(counter.GetComponent<Renderer>(), color * 0.7f);

                var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canopy.name = "Canopy";
                canopy.transform.SetParent(stall, false);
                canopy.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                canopy.transform.localScale = new Vector3(2.8f, 0.15f, 1.6f);
                TintShared(canopy.GetComponent<Renderer>(), color);
            }
        }

        /// <summary>Build the bobbing/spinning marker placed above an un-talked NPC. Parented to the NPC.</summary>
        private static GameObject BuildNpcArrow(Transform npc)
        {
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "TalkArrow";
            arrow.transform.SetParent(npc, false);
            arrow.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            arrow.transform.localRotation = Quaternion.Euler(45f, 45f, 0f); // diamond silhouette
            arrow.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            TintShared(arrow.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.2f));
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.AddComponent<FloatingArrow>();
            return arrow;
        }

        /// <summary>A world-space TextMesh prompt (e.g. "Press Y to talk"), created hidden.</summary>
        private static TextMesh BuildWorldPromptText(string name, Vector3 position, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.012f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = "";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = color;
            go.SetActive(false);
            return tm;
        }

    }
}

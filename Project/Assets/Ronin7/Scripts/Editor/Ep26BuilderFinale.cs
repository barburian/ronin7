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
    /// EP26 "The Requiem Protocol" (Galaxy 4 conclusion) finale builders for the last three scenes.
    /// - Archive Corridor: marble columns + burning candles; Sallow reveals her defection; warrior-priests (nonLethal).
    /// - Drone Escape: SPACE dogfight (GuardEncounter hunter-drone carrier escape); Sallow becomes ally #10; gate finale on cleared.
    /// - Cargo Bay: intimate hunter duel (copy Nave's Varrik pattern); Sallow joins; sets ep26_complete + sallow_recruited flags.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and constants
    /// (Galaxy4Ep26*ScenePath/Name, BuildEp26DialoguePlayer, BuildEp26OnFootShell, BuildEp26Npc, FinishEp26Scene)
    /// are declared in Ep26Builder.cs. DO NOT redefine them.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Archive Corridor", priority = 283)]
        public static void BuildEp26ArchiveCorridor()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive Corridor: marble columns + burning candles (cool marble white + candle gold).
            var playerHealth = BuildEp26OnFootShell(refs, weapon,
                keyLight: new Color(0.75f, 0.72f, 0.70f),     // cool marble white
                ambient: new Color(0.14f, 0.13f, 0.12f),      // dim cool
                fogColor: new Color(0.22f, 0.20f, 0.18f), fogDensity: 0.018f,
                structureName: "ArchiveCorridor",
                accent1: new Color(1f, 0.85f, 0.50f),         // candle gold
                accent2: new Color(0.90f, 0.88f, 0.85f),      // pale marble
                floorLight: new Color(0.52f, 0.50f, 0.48f), floorDark: new Color(0.26f, 0.25f, 0.24f),
                propTint: new Color(0.48f, 0.46f, 0.44f), out _);

            // ---- Sallow NPC (pale luminous tint) ----
            BuildEp26Npc("Sallow", new Vector3(-1.2f, 0f, 3f), new Color(0.8f, 0.82f, 0.85f));

            // ---- Warrior-priests (3-4 enemies, lethal, dark robed) ----
            var priestTint = new Color(0.32f, 0.30f, 0.38f);
            var priestPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1f, 0f, 11f),
            };
            var priestHealths = new List<Health>();
            foreach (var pos in priestPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, priestTint);
                enemy.gameObject.SetActive(false);
                priestHealths.Add(enemy.GetComponent<Health>());
            }

            var priestSpawner = BuildWaveSpawner("PriestSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { priestHealths },
                new[] { BuildEp26DialoguePlayer("Dialogue_ArchiveBarks", new Vector3(0f, 1.5f, 8f), "archive_barks") });

            // ---- Dialogue Players ----
            var archivistDialogue = BuildEp26DialoguePlayer("Dialogue_Archivist", new Vector3(0f, 1.5f, 2f), "archivist");
            var arcSo = new SerializedObject(archivistDialogue);
            arcSo.FindProperty("playOnStart").boolValue = true;
            arcSo.ApplyModifiedPropertiesWithoutUndo();

            var archiveDriveDialogue = BuildEp26DialoguePlayer("Dialogue_ArchiveDrive", new Vector3(0f, 1.5f, 14f), "archive_drive");

            // Transition box: "ESCAPE — TO THE BAY".
            var bayBoxGo = BuildTransitionBox("ToBayBox", new Vector3(0f, 1.2f, 21.5f), "ESCAPE — TO THE BAY",
                out var bayBtn, out var bayTransition);
            var bbSo = new SerializedObject(bayTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep26DroneEscapeSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(bayBtn.onClick,
                new UnityEngine.Events.UnityAction(bayTransition.LoadOnFootScene));
            bayBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archivist";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archivistDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Warrior-Priests (4, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = priestSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Archive Drive";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = archiveDriveDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Escape to the Bay";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = bayBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp26Scene(scene, Galaxy4Ep26ArchiveCorridorScenePath, Galaxy4Ep26DroneEscapeScenePath);

            Debug.Log($"[Space Samurai] EP26 Archive Corridor scene built at {Galaxy4Ep26ArchiveCorridorScenePath}. " +
                      "Marble columns + burning candles (cool marble white + candle gold). " +
                      "Sallow (pale luminous, nonLethal). 4 warrior-priests (dark robed, lethal). " +
                      "4 steps: archivist (auto, Sallow defects) → defeat 4 priests (archive_barks) → archive_drive (data handoff) → escape to the bay.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Drone Escape", priority = 284)]
        public static void BuildEp26DroneEscape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void near the Absolution (dark obsidian mass) with debris.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // The Absolution below, dark obsidian mass.
            var absolutionGo = AddUnlitVisual(universe, "The Absolution", new Vector3(-1200f, -700f, -2600f),
                Vector3.one * 900f, PrimitiveType.Sphere, new Color(0.15f, 0.14f, 0.18f));
            var absolutionCollider = absolutionGo.GetComponent<Collider>();
            if (absolutionCollider != null) absolutionCollider.isTrigger = true;

            // A drifting debris cluster for parallax.
            var debris = AddUnlitVisual(universe, "Orbital Debris", new Vector3(2200f, 300f, -2700f),
                new Vector3(260f, 130f, 340f), PrimitiveType.Cube, new Color(0.40f, 0.42f, 0.46f));
            var debrisCollider = debris.GetComponent<Collider>();
            if (debrisCollider != null) debrisCollider.isTrigger = true;

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool.
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            // the_tenth_voice: escape + Mortis comms, plays on start.
            var tenthVoiceDialogue = BuildEp26DialoguePlayer("Dialogue_TheTenthVoice", new Vector3(0f, 1.62f, 0.8f), "the_tenth_voice");
            var tvGo = tenthVoiceDialogue.gameObject;
            tvGo.transform.SetParent(cockpit, false);
            tvGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            tvGo.transform.localRotation = Quaternion.identity;
            var tvSo = new SerializedObject(tenthVoiceDialogue);
            tvSo.FindProperty("playOnStart").boolValue = true;
            tvSo.ApplyModifiedPropertiesWithoutUndo();

            // drone_barks: GuardEncounter spawn dialogue.
            var droneBarksDialogue = BuildEp26DialoguePlayer("Dialogue_DroneBarks", new Vector3(0f, 1.62f, 0.8f), "drone_barks");
            var dbGo = droneBarksDialogue.gameObject;
            dbGo.transform.SetParent(cockpit, false);
            dbGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            dbGo.transform.localRotation = Quaternion.identity;

            // worlds_not_people: revealed (and auto-played) when the drones are cleared.
            var worldsDialogue = BuildEp26DialoguePlayer("Dialogue_WorldsNotPeople", new Vector3(0f, 1.62f, 0.8f), "worlds_not_people");
            var worldsGo = worldsDialogue.gameObject;
            worldsGo.transform.SetParent(cockpit, false);
            worldsGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            worldsGo.transform.localRotation = Quaternion.identity;
            var worldsSo = new SerializedObject(worldsDialogue);
            worldsSo.FindProperty("playOnStart").boolValue = true; // fires on Start when activated
            worldsSo.ApplyModifiedPropertiesWithoutUndo();
            worldsGo.SetActive(false);

            // ---- Enemy Encounter: Hunter-drone carrier (4 ships) ----
            var droneEncounterGo = new GameObject("DroneEncounter");
            var droneEncounter = droneEncounterGo.AddComponent<GuardEncounter>();
            var deSo = new SerializedObject(droneEncounter);
            SetObjectRef(deSo, "player", shipCtrl);
            SetObjectRef(deSo, "universe", universe);
            SetObjectRef(deSo, "pool", pool);
            SetObjectRef(deSo, "definition", enemyShipDef);
            deSo.FindProperty("shipCount").intValue = 4;
            deSo.FindProperty("spawnRadius").floatValue = 280f;
            deSo.FindProperty("initialDelay").floatValue = 6f;
            deSo.FindProperty("requiredCompletedScene").stringValue = "";
            deSo.FindProperty("clearedFlag").stringValue = "ep26_drones_cleared";
            SetObjectRef(deSo, "spawnDialogue", droneBarksDialogue);
            deSo.ApplyModifiedPropertiesWithoutUndo();

            // Mid-episode transition box: "BOARD THE CORSAIR" (NOT a return box).
            var boardBoxGo = BuildTransitionBox("BoardCorsairBox", new Vector3(0f, 1.2f, 0.8f), "BOARD THE CORSAIR",
                out var boardBtn, out var boardTransition);
            var boardSo = new SerializedObject(boardTransition);
            boardSo.FindProperty("onFootScene").stringValue = Galaxy4Ep26CargoBaySceneName;
            boardSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(boardBtn.onClick,
                new UnityEngine.Events.UnityAction(boardTransition.LoadOnFootScene));
            boardBoxGo.SetActive(false);

            // Gate the finale on the drone fight: EncounterClearedActivator reveals worlds_not_people + the board box.
            var droneGateGo = new GameObject("DroneClearedGate");
            var droneGate = droneGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(droneGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = worldsGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = boardBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep26_drones_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (register next scene too).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep26DroneEscapeScenePath);
            EnsureScenesInBuild(Galaxy4Ep26DroneEscapeScenePath, Galaxy4Ep26CargoBayScenePath);

            Debug.Log($"[Space Samurai] EP26 Drone Escape scene built at {Galaxy4Ep26DroneEscapeScenePath}. " +
                      "SPACE escape near the Absolution (dark obsidian mass + orbital debris). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: the_tenth_voice (auto, escape + Mortis comms) → 4 hunter-drones (GuardEncounter, 6s) → on cleared, " +
                      "EncounterClearedActivator reveals worlds_not_people dialogue + BOARD THE CORSAIR transition. " +
                      "NOTE: this is a MID-EPISODE interlude, not the hub return.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Cargo Bay", priority = 285)]
        public static void BuildEp26CargoBay()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cargo Bay: dim industrial (cool steel + warm worklight).
            var playerHealth = BuildEp26OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.60f, 0.58f),     // dim industrial
                ambient: new Color(0.11f, 0.11f, 0.10f),      // very dim
                fogColor: new Color(0.20f, 0.19f, 0.18f), fogDensity: 0.020f,
                structureName: "CargoHold",
                accent1: new Color(0.85f, 0.88f, 0.90f),      // cool steel
                accent2: new Color(1f, 0.75f, 0.45f),         // warm worklight
                floorLight: new Color(0.50f, 0.48f, 0.46f), floorDark: new Color(0.25f, 0.24f, 0.23f),
                propTint: new Color(0.46f, 0.44f, 0.42f), out _);

            // ---- Sallow NPC (ally #10, pale luminous) ----
            BuildEp26Npc("Sallow", new Vector3(-1.2f, 0f, 3f), new Color(0.8f, 0.82f, 0.85f));

            // ---- The Hunter (Mortis's duel opponent, cold neural-spike steel) ----
            var hunterTint = new Color(0.4f, 0.42f, 0.5f);
            var hunter = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var hunterGo = hunter.gameObject;
            hunterGo.name = "Mortis's Hunter";
            var hunterRenderer = hunter.GetComponent<Renderer>();
            if (hunterRenderer != null) TintShared(hunterRenderer, hunterTint);
            var hunterHealth = hunter.GetComponent<Health>();

            // Hunter's strikes are non-lethal — this is a duel of wills, not an execution.
            var hunterMelee = hunter.GetComponent<MeleeAttacker>();
            if (hunterMelee != null)
            {
                var maSo = new SerializedObject(hunterMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var hunterNpc = hunterGo.AddComponent<StoryNpc>();
            var hNpcSo = new SerializedObject(hunterNpc);
            hNpcSo.FindProperty("displayName").stringValue = "Hunter";
            hNpcSo.FindProperty("remote").boolValue = false;
            hNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // Re-find the player sword ("Sword") for the DuelYield wire.
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // DuelYield: yield at 0.25. The duel ends in a YIELD (not a death), so it is wired as a
            // null-prompt mission step that DuelYield.onAccepted advances — never a DefeatWaves step.
            var duelYield = hunterGo.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", hunterHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.25f;
            if (hunterMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { hunterMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // hunter_duel bark ("Clean. One strike.") plays when Hunter yields.
            var hunterDuelDialogue = BuildEp26DialoguePlayer("Dialogue_HunterDuel", new Vector3(0f, 1.5f, 12f), "hunter_duel");
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(hunterDuelDialogue.Play));

            hunterGo.SetActive(false);

            // ---- ConfessorLink component (EP26 duel-of-wills mechanic) ----
            var linkGo = new GameObject("ConfessorLink");
            var link = linkGo.AddComponent<ConfessorLink>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("linkActive").boolValue = true;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var hunterArrivalDialogue = BuildEp26DialoguePlayer("Dialogue_HunterArrival", new Vector3(0f, 1.5f, 2f), "hunter_arrival");
            var haySo = new SerializedObject(hunterArrivalDialogue);
            haySo.FindProperty("playOnStart").boolValue = true;
            haySo.ApplyModifiedPropertiesWithoutUndo();

            var reckoningVowDialogue = BuildEp26DialoguePlayer("Dialogue_ReckoningVow", new Vector3(0f, 1.5f, 14f), "reckoning_vow");

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (ep26_complete + sallow_recruited).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 21.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep26_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "sallow_recruited";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Hunter Arrival";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = hunterArrivalDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Activate Hunter";
            var activateProp1 = s1.FindPropertyRelative("triggerObjects");
            activateProp1.arraySize = 1;
            activateProp1.GetArrayElementAtIndex(0).objectReferenceValue = hunterGo;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Hunter Duel (yield + sheathe, refuse to consume)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = null;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Reckoning Vow";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = reckoningVowDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → AdvanceFromPrompt (advance the null-prompt duel step) and
            // → ConfessorLink.Resolve (the mechanic computes the refusal logic).
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(link.Resolve));

            FinishEp26Scene(scene, Galaxy4Ep26CargoBayScenePath, null);

            Debug.Log($"[Space Samurai] EP26 Cargo Bay scene built at {Galaxy4Ep26CargoBayScenePath}. " +
                      "Corsair cargo hold, dim industrial (cool steel + warm worklight). " +
                      "Sallow (pale luminous, ally #10). Hunter (cold neural-spike steel, duel opponent, DuelYield yield 25%, hunter_duel on yield). " +
                      "ConfessorLink active; onAccepted → AdvanceFromPrompt + ConfessorLink.Resolve. " +
                      "5 steps: hunter_arrival (auto, Sallow becomes ally #10) → activate Hunter (trigger) → " +
                      "duel Prompt (onAccepted → AdvanceFromPrompt + Resolve) → reckoning_vow → " +
                      "RETURN — TO THE STARS (CampaignFlagSetter sets ep26_complete + sallow_recruited, then ReturnToSpace). " +
                      "GALAXY 4 EP26 FINALE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP26 Scenes", priority = 286)]
        public static void BuildAllEp26Scenes()
        {
            BuildEp26AshenDeep();
            BuildEp26DockingBay();
            BuildEp26Nave();
            BuildEp26ArchiveCorridor();
            BuildEp26DroneEscape();
            BuildEp26CargoBay();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP26 scenes built + inputs rewired. GALAXY 4 EP26 COMPLETE.");
        }
    }
}

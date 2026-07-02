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
    /// EP13 "The Carnival of Forgotten Names" space-finale builder: the burning escape.
    /// Mirrors EP12 Pursuit pattern: cockpit space scene with GuardEncounter (3 ships),
    /// denouement MissionDirector (4 dialogue steps + 1 prompt), and EncounterClearedActivator gate.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: Scene paths and names (Galaxy2Ep13EscapeScenePath, Galaxy2Ep13EscapeSceneName, etc.)
        // are defined in Galaxy2Builder.cs. We reference them but do not redeclare them.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP13 Escape", priority = 141)]
        public static void BuildEp13Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Carnival debris field: scattered unlit grey carnival wreckage chunks.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.7f, 0.5f);
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

            // Universe root.
            var universe = new GameObject("Universe").transform;

            // Scatter ~6 carnival debris chunks under the universe (grey, rounded carnival structures).
            var debrisPositions = new Vector3[]
            {
                new Vector3(-400f, -200f, -1500f),
                new Vector3(300f, 150f, -1700f),
                new Vector3(-150f, 250f, -1900f),
                new Vector3(550f, 0f, -1400f),
                new Vector3(50f, 300f, -2000f),
                new Vector3(-600f, 150f, -1600f)
            };

            foreach (var pos in debrisPositions)
            {
                AddUnlitVisual(universe, $"CarnivalDebris{Random.Range(1, 99)}", pos,
                    new Vector3(120f + Random.Range(-30f, 30f), 100f + Random.Range(-20f, 20f), 110f + Random.Range(-25f, 30f)),
                    PrimitiveType.Cube, new Color(0.30f, 0.28f, 0.30f));
            }

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
            var burningEscapeDialogue = BuildDialoguePlayer("Dialogue_BurningEscape", new Vector3(0f, 1.62f, 0.8f),
                Ep13Lines.Get("burning_escape"), null, "burning_escape", clipPrefix: "ep13");
            var burningGo = burningEscapeDialogue.gameObject;
            burningGo.transform.SetParent(cockpit, false);
            burningGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            burningGo.transform.localRotation = Quaternion.identity;

            // Denouement dialogues (NOT playOnStart): drifting_coral, archive_reviewed, final_drone, the_question.
            var driftingCoralDialogue = BuildDialoguePlayer("Dialogue_DriftingCoral", new Vector3(0f, 1.62f, 0.8f),
                Ep13Lines.Get("drifting_coral"), null, "drifting_coral", clipPrefix: "ep13");
            var driftingGo = driftingCoralDialogue.gameObject;
            driftingGo.transform.SetParent(cockpit, false);
            driftingGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            driftingGo.transform.localRotation = Quaternion.identity;

            var archiveReviewedDialogue = BuildDialoguePlayer("Dialogue_ArchiveReviewed", new Vector3(0f, 1.62f, 0.8f),
                Ep13Lines.Get("archive_reviewed"), null, "archive_reviewed", clipPrefix: "ep13");
            var archiveGo = archiveReviewedDialogue.gameObject;
            archiveGo.transform.SetParent(cockpit, false);
            archiveGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            archiveGo.transform.localRotation = Quaternion.identity;

            var finalDroneDialogue = BuildDialoguePlayer("Dialogue_FinalDrone", new Vector3(0f, 1.62f, 0.8f),
                Ep13Lines.Get("final_drone"), null, "final_drone", clipPrefix: "ep13");
            var droneGo = finalDroneDialogue.gameObject;
            droneGo.transform.SetParent(cockpit, false);
            droneGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            droneGo.transform.localRotation = Quaternion.identity;

            var theQuestionDialogue = BuildDialoguePlayer("Dialogue_TheQuestion", new Vector3(0f, 1.62f, 0.8f),
                Ep13Lines.Get("the_question"), null, "the_question", clipPrefix: "ep13");
            var questionGo = theQuestionDialogue.gameObject;
            questionGo.transform.SetParent(cockpit, false);
            questionGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            questionGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Carnival Pursuit (3 ships) ----
            var carnivalPursuitGo = new GameObject("CarnivalPursuit");
            var carnivalPursuit = carnivalPursuitGo.AddComponent<GuardEncounter>();
            var carnivalPursuitSo = new SerializedObject(carnivalPursuit);
            SetObjectRef(carnivalPursuitSo, "player", shipCtrl);
            SetObjectRef(carnivalPursuitSo, "universe", universe);
            SetObjectRef(carnivalPursuitSo, "pool", pool);
            SetObjectRef(carnivalPursuitSo, "definition", enemyShipDef);
            carnivalPursuitSo.FindProperty("shipCount").intValue = 3;
            carnivalPursuitSo.FindProperty("spawnRadius").floatValue = 320f;
            carnivalPursuitSo.FindProperty("initialDelay").floatValue = 5f;
            carnivalPursuitSo.FindProperty("requiredCompletedScene").stringValue = "";
            carnivalPursuitSo.FindProperty("clearedFlag").stringValue = "ep13_escape_cleared";
            SetObjectRef(carnivalPursuitSo, "spawnDialogue", burningEscapeDialogue);
            carnivalPursuitSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // ---- Denouement MissionDirector ----
            var denouementGo = new GameObject("Denouement");
            denouementGo.SetActive(false);
            var denouementDirector = denouementGo.AddComponent<MissionDirector>();
            var denouementSo = new SerializedObject(denouementDirector);

            // Build 5 mission steps: 4 dialogues + 1 prompt.
            var stepsProp = denouementSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue (drifting_coral)
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("dialogue").objectReferenceValue = driftingCoralDialogue;

            // Step 1: Dialogue (archive_reviewed)
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step1.FindPropertyRelative("dialogue").objectReferenceValue = archiveReviewedDialogue;

            // Step 2: Dialogue (final_drone)
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step2.FindPropertyRelative("dialogue").objectReferenceValue = finalDroneDialogue;

            // Step 3: Dialogue (the_question)
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step3.FindPropertyRelative("dialogue").objectReferenceValue = theQuestionDialogue;

            // Step 4: Prompt (return map box)
            var step4 = stepsProp.GetArrayElementAtIndex(4);
            step4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step4.FindPropertyRelative("promptObject").objectReferenceValue = returnMapBoxGo;

            denouementSo.ApplyModifiedPropertiesWithoutUndo();

            // Gate the ending on the fight: EncounterClearedActivator.
            var escapeClearedGateGo = new GameObject("EscapeClearedGate");
            var escapeClearedGate = escapeClearedGateGo.AddComponent<EncounterClearedActivator>();
            var escapeClearedGateSo = new SerializedObject(escapeClearedGate);
            var activateProp = escapeClearedGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = denouementGo;
            escapeClearedGateSo.FindProperty("clearedFlag").stringValue = "ep13_escape_cleared";
            var extraFlagProp = escapeClearedGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep13_complete";
            }
            escapeClearedGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep13EscapeScenePath);
            EnsureScenesInBuild(Galaxy2Ep13EscapeScenePath);

            Debug.Log($"[Space Samurai] EP13 Escape scene built at {Galaxy2Ep13EscapeScenePath}. " +
                      "Carnival debris field with scattered unlit debris chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: burning_escape spawns (GuardEncounter, 3 ships, 5s delay) → " +
                      "on cleared, EncounterClearedActivator activates Denouement MissionDirector " +
                      "(plays drifting_coral → archive_reviewed → final_drone → the_question → " +
                      "reveals JUMP — RETURN TO GALAXY MAP) + sets extraFlag 'ep13_complete'.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP13 Scenes", priority = 135)]
        public static void BuildAllEp13Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP13 scenes in order: Carousel, Mirror Maze, Center Tent, Vault, Core Fight, Escape, Galaxy 2...");
            BuildEp13Carousel();
            BuildEp13MirrorMaze();
            BuildEp13CenterTent();
            BuildEp13Vault();
            BuildEp13CoreFight();
            BuildEp13Escape();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP13 scenes built successfully! Galaxy 2 hub rebuilt to register EP13 completions + Carnival landable.");
        }
    }
}

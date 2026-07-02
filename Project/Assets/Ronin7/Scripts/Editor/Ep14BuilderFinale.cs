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
    /// EP14 "The Echo Protocol" space-finale builder: the escape from the imploding Deep Station Mercer.
    /// Mirrors EP13 Carnival pattern: cockpit space scene with GuardEncounter (3 ships),
    /// denouement MissionDirector (3 dialogue steps + 1 prompt), and EncounterClearedActivator gate.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: Scene paths and names (Galaxy2Ep14EscapeScenePath, Galaxy2Ep14EscapeSceneName, etc.)
        // are defined in Galaxy2Builder.cs. We reference them but do not redeclare them.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP14 Escape", priority = 147)]
        public static void BuildEp14Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Veil Nebula debris field: scattered unlit cold blue-grey Mercer station wreckage chunks.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(0.6f, 0.7f, 0.85f);
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

            // Scatter ~6 Mercer debris chunks under the universe (cold blue-grey, Veil Nebula tint).
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
                AddUnlitVisual(universe, $"MercerDebris{Random.Range(1, 99)}", pos,
                    new Vector3(120f + Random.Range(-30f, 30f), 100f + Random.Range(-20f, 20f), 110f + Random.Range(-25f, 30f)),
                    PrimitiveType.Cube, new Color(0.22f, 0.26f, 0.32f));
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
            var escapePursuitDialogue = BuildDialoguePlayer("Dialogue_EscapePursuit", new Vector3(0f, 1.62f, 0.8f),
                Ep14Lines.Get("escape_pursuit"), null, "escape_pursuit", clipPrefix: "ep14");
            var escapePursuitGo = escapePursuitDialogue.gameObject;
            escapePursuitGo.transform.SetParent(cockpit, false);
            escapePursuitGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            escapePursuitGo.transform.localRotation = Quaternion.identity;

            // Denouement dialogues (NOT playOnStart): implosion, confession_broadcast, return_to_questions.
            var implosionDialogue = BuildDialoguePlayer("Dialogue_Implosion", new Vector3(0f, 1.62f, 0.8f),
                Ep14Lines.Get("implosion"), null, "implosion", clipPrefix: "ep14");
            var implosionGo = implosionDialogue.gameObject;
            implosionGo.transform.SetParent(cockpit, false);
            implosionGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            implosionGo.transform.localRotation = Quaternion.identity;

            var confessionBroadcastDialogue = BuildDialoguePlayer("Dialogue_ConfessionBroadcast", new Vector3(0f, 1.62f, 0.8f),
                Ep14Lines.Get("confession_broadcast"), null, "confession_broadcast", clipPrefix: "ep14");
            var confessionGo = confessionBroadcastDialogue.gameObject;
            confessionGo.transform.SetParent(cockpit, false);
            confessionGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            confessionGo.transform.localRotation = Quaternion.identity;

            var returnToQuestionsDialogue = BuildDialoguePlayer("Dialogue_ReturnToQuestions", new Vector3(0f, 1.62f, 0.8f),
                Ep14Lines.Get("return_to_questions"), null, "return_to_questions", clipPrefix: "ep14");
            var questionsGo = returnToQuestionsDialogue.gameObject;
            questionsGo.transform.SetParent(cockpit, false);
            questionsGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            questionsGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Mercer Pursuit (3 ships) ----
            var mercerPursuitGo = new GameObject("MercerPursuit");
            var mercerPursuit = mercerPursuitGo.AddComponent<GuardEncounter>();
            var mercerPursuitSo = new SerializedObject(mercerPursuit);
            SetObjectRef(mercerPursuitSo, "player", shipCtrl);
            SetObjectRef(mercerPursuitSo, "universe", universe);
            SetObjectRef(mercerPursuitSo, "pool", pool);
            SetObjectRef(mercerPursuitSo, "definition", enemyShipDef);
            mercerPursuitSo.FindProperty("shipCount").intValue = 3;
            mercerPursuitSo.FindProperty("spawnRadius").floatValue = 320f;
            mercerPursuitSo.FindProperty("initialDelay").floatValue = 5f;
            mercerPursuitSo.FindProperty("requiredCompletedScene").stringValue = "";
            mercerPursuitSo.FindProperty("clearedFlag").stringValue = "ep14_escape_cleared";
            SetObjectRef(mercerPursuitSo, "spawnDialogue", escapePursuitDialogue);
            mercerPursuitSo.ApplyModifiedPropertiesWithoutUndo();

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

            // Build 4 mission steps: 3 dialogues + 1 prompt.
            var stepsProp = denouementSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue (implosion)
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("dialogue").objectReferenceValue = implosionDialogue;

            // Step 1: Dialogue (confession_broadcast)
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step1.FindPropertyRelative("dialogue").objectReferenceValue = confessionBroadcastDialogue;

            // Step 2: Dialogue (return_to_questions)
            var step2 = stepsProp.GetArrayElementAtIndex(2);
            step2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step2.FindPropertyRelative("dialogue").objectReferenceValue = returnToQuestionsDialogue;

            // Step 3: Prompt (return map box)
            var step3 = stepsProp.GetArrayElementAtIndex(3);
            step3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step3.FindPropertyRelative("promptObject").objectReferenceValue = returnMapBoxGo;

            denouementSo.ApplyModifiedPropertiesWithoutUndo();

            // Gate the ending on the fight: EncounterClearedActivator.
            var escapeClearedGateGo = new GameObject("EscapeClearedGate");
            var escapeClearedGate = escapeClearedGateGo.AddComponent<EncounterClearedActivator>();
            var escapeClearedGateSo = new SerializedObject(escapeClearedGate);
            var activateProp = escapeClearedGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = denouementGo;
            escapeClearedGateSo.FindProperty("clearedFlag").stringValue = "ep14_escape_cleared";
            var extraFlagProp = escapeClearedGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep14_complete";
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep14EscapeScenePath);
            EnsureScenesInBuild(Galaxy2Ep14EscapeScenePath);

            Debug.Log($"[Space Samurai] EP14 Escape scene built at {Galaxy2Ep14EscapeScenePath}. " +
                      "Veil Nebula debris field with scattered unlit cold blue-grey Mercer debris chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: escape_pursuit spawns (GuardEncounter, 3 ships, 5s delay) → " +
                      "on cleared, EncounterClearedActivator activates Denouement MissionDirector " +
                      "(plays implosion → confession_broadcast → return_to_questions → " +
                      "reveals JUMP — RETURN TO GALAXY MAP) + sets extraFlag 'ep14_complete'.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP14 Scenes", priority = 148)]
        public static void BuildAllEp14Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP14 scenes in order: Docking Ring, Observation, Archive Defense, Revelation, Siege, Escape, Galaxy 2...");
            BuildEp14DockingRing();
            BuildEp14Observation();
            BuildEp14ArchiveDefense();
            BuildEp14Revelation();
            BuildEp14Siege();
            BuildEp14Escape();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP14 scenes built successfully! Galaxy 2 hub rebuilt to register EP14 completions + Deep Station Mercer landable.");
        }
    }
}

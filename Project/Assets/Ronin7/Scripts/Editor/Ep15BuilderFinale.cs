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
    /// EP15 "The Dreadnought" space-finale builder: the desperate dogfight to scatter the Dominion's truth-suppressing relay.
    /// Mirrors EP14 pattern: cockpit space scene with GuardEncounter (3 ships),
    /// denouement MissionDirector (1 dialogue step + 1 prompt), and EncounterClearedActivator gate.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: Scene paths and names (Galaxy2Ep15DreadnoughtScenePath, Galaxy2Ep15DreadnoughtSceneName, etc.)
        // are defined in Galaxy2Builder.cs. We reference them but do not redeclare them.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP15 Dreadnought", priority = 157)]
        public static void BuildEp15Dreadnought()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Veil Expanse debris field: scattered unlit cold blue-grey Relay-9 wreckage chunks.
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

            // Scatter ~6 Relay-9 debris chunks under the universe (cold blue-grey, Veil Expanse tint).
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
                AddUnlitVisual(universe, $"Relay9Debris{Random.Range(1, 99)}", pos,
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
            var dreadnoughtBarkDialogue = BuildDialoguePlayer("Dialogue_DreadnoughtBark", new Vector3(0f, 1.62f, 0.8f),
                Ep15Lines.Get("dreadnought_barks"), null, "dreadnought_barks", clipPrefix: "ep15");
            var dreadnoughtBarkGo = dreadnoughtBarkDialogue.gameObject;
            dreadnoughtBarkGo.transform.SetParent(cockpit, false);
            dreadnoughtBarkGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            dreadnoughtBarkGo.transform.localRotation = Quaternion.identity;

            // Denouement dialogue (NOT playOnStart): cockpit_final.
            var cockpitFinalDialogue = BuildDialoguePlayer("Dialogue_CockpitFinal", new Vector3(0f, 1.62f, 0.8f),
                Ep15Lines.Get("cockpit_final"), null, "cockpit_final", clipPrefix: "ep15");
            var cockpitFinalGo = cockpitFinalDialogue.gameObject;
            cockpitFinalGo.transform.SetParent(cockpit, false);
            cockpitFinalGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            cockpitFinalGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Dreadnought Defense (3 ships) ----
            var dreadnoughtGo = new GameObject("Dreadnought");
            var dreadnought = dreadnoughtGo.AddComponent<GuardEncounter>();
            var dreadnoughtSo = new SerializedObject(dreadnought);
            SetObjectRef(dreadnoughtSo, "player", shipCtrl);
            SetObjectRef(dreadnoughtSo, "universe", universe);
            SetObjectRef(dreadnoughtSo, "pool", pool);
            SetObjectRef(dreadnoughtSo, "definition", enemyShipDef);
            dreadnoughtSo.FindProperty("shipCount").intValue = 3;
            dreadnoughtSo.FindProperty("spawnRadius").floatValue = 320f;
            dreadnoughtSo.FindProperty("initialDelay").floatValue = 5f;
            dreadnoughtSo.FindProperty("requiredCompletedScene").stringValue = "";
            dreadnoughtSo.FindProperty("clearedFlag").stringValue = "ep15_escape_cleared";
            SetObjectRef(dreadnoughtSo, "spawnDialogue", dreadnoughtBarkDialogue);
            dreadnoughtSo.ApplyModifiedPropertiesWithoutUndo();

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

            // Build 2 mission steps: 1 dialogue + 1 prompt.
            var stepsProp = denouementSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue (cockpit_final)
            var step0 = stepsProp.GetArrayElementAtIndex(0);
            step0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            step0.FindPropertyRelative("dialogue").objectReferenceValue = cockpitFinalDialogue;

            // Step 1: Prompt (return map box)
            var step1 = stepsProp.GetArrayElementAtIndex(1);
            step1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            step1.FindPropertyRelative("promptObject").objectReferenceValue = returnMapBoxGo;

            denouementSo.ApplyModifiedPropertiesWithoutUndo();

            // Gate the ending on the fight: EncounterClearedActivator.
            var dreadnoughtClearedGateGo = new GameObject("DreadnoughtClearedGate");
            var dreadnoughtClearedGate = dreadnoughtClearedGateGo.AddComponent<EncounterClearedActivator>();
            var dreadnoughtClearedGateSo = new SerializedObject(dreadnoughtClearedGate);
            var activateProp = dreadnoughtClearedGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = denouementGo;
            dreadnoughtClearedGateSo.FindProperty("clearedFlag").stringValue = "ep15_escape_cleared";
            var extraFlagProp = dreadnoughtClearedGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep15_complete";
            }
            dreadnoughtClearedGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep15DreadnoughtScenePath);
            EnsureScenesInBuild(Galaxy2Ep15DreadnoughtScenePath);

            Debug.Log($"[Space Samurai] EP15 Dreadnought scene built at {Galaxy2Ep15DreadnoughtScenePath}. " +
                      "Veil Expanse debris field with scattered unlit cold blue-grey Relay-9 debris chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: dreadnought_barks spawns (GuardEncounter, 3 ships, 5s delay) → " +
                      "on cleared, EncounterClearedActivator activates Denouement MissionDirector " +
                      "(plays cockpit_final → " +
                      "reveals JUMP — RETURN TO GALAXY MAP) + sets extraFlag 'ep15_complete'.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP15 Scenes", priority = 158)]
        public static void BuildAllEp15Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP15 scenes in order: Relay Shafts, Corvette Boarding, Sulfur Throne, Sunken Archives, Desert Reckoning, Dreadnought, Galaxy 2...");
            BuildEp15RelayShafts();
            BuildEp15CorvetteBoarding();
            BuildEp15SulfurThrone();
            BuildEp15SunkenArchives();
            BuildEp15DesertReckoning();
            BuildEp15Dreadnought();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP15 scenes built successfully! Galaxy 2 hub rebuilt to register EP15 completions + The Dreadnought final encounter.");
        }
    }
}

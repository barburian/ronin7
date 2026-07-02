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
    /// EP11 "The Iron Dojo" space scene builders: Ascent (above Verdis Prime) and Exit Point (debris-heavy transit).
    /// Mirrors EP10 Extraction pattern closely (cockpit, hull, ShipController, ProjectilePool, ShipWeaponController,
    /// BuildStarfield, universe root, GameState startMode=SpaceFlight, farClip 6000).
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: BuildEp11DialoguePlayer lives in Ep11Builder.cs (same partial class) — shared here.
        // NOTE: Scene paths and names (Galaxy2Ep11AscentScenePath, etc.) are defined in Galaxy2Builder.cs.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP11 Ascent", priority = 126)]
        public static void BuildEp11Ascent()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space above Verdis Prime: green-blue jungle-moon sphere below via AddUnlitVisual.
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

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Verdis Prime: green-blue jungle-moon sphere below.
            var verdisPrimeGo = AddUnlitVisual(universe, "Verdis Prime", new Vector3(-1000f, -500f, -1800f),
                new Vector3(400f, 400f, 400f), PrimitiveType.Sphere, new Color(0.30f, 0.55f, 0.42f));
            var verdisPrimeCollider = verdisPrimeGo.GetComponent<Collider>();
            if (verdisPrimeCollider != null) verdisPrimeCollider.isTrigger = true;

            // Sparse lights on Verdis Prime (unlit glow spots).
            AddUnlitVisual(universe, "VerdisPrimeLight1", new Vector3(-950f, -450f, -1700f),
                new Vector3(25f, 25f, 25f), PrimitiveType.Cube, new Color(0.4f, 0.8f, 0.6f));
            AddUnlitVisual(universe, "VerdisPrimeLight2", new Vector3(-1100f, -600f, -1900f),
                new Vector3(20f, 20f, 20f), PrimitiveType.Cube, new Color(0.3f, 0.7f, 0.5f));

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
            var corvetteDialogue = BuildDialoguePlayer("Dialogue_AscentCorvetteBarks", new Vector3(0f, 1.62f, 0.8f),
                Ep11Lines.Get("corvette_barks"), null, "corvette_barks", clipPrefix: "ep11");
            var corvetteGo = corvetteDialogue.gameObject;
            corvetteGo.transform.SetParent(cockpit, false);
            corvetteGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            corvetteGo.transform.localRotation = Quaternion.identity;

            // The file dialogue (revealed when the fight clears).
            var fileDialogue = BuildDialoguePlayer("Dialogue_AscentTheFile", new Vector3(0f, 1.62f, 0.8f),
                Ep11Lines.Get("the_file"), null, "the_file", clipPrefix: "ep11");
            var fileGo = fileDialogue.gameObject;
            fileGo.transform.SetParent(cockpit, false);
            fileGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            fileGo.transform.localRotation = Quaternion.identity;
            var fileSo = new SerializedObject(fileDialogue);
            fileSo.FindProperty("playOnStart").boolValue = true;
            fileSo.ApplyModifiedPropertiesWithoutUndo();
            fileGo.SetActive(false); // Initially inactive; EncounterClearedActivator will activate it.

            // ---- Enemy Encounter: Corvette Patrol (pursuit-mode) ----
            var ascentEncGo = new GameObject("AscentPatrol");
            var ascentEnc = ascentEncGo.AddComponent<GuardEncounter>();
            var ascentEncSo = new SerializedObject(ascentEnc);
            SetObjectRef(ascentEncSo, "player", shipCtrl);
            SetObjectRef(ascentEncSo, "universe", universe);
            SetObjectRef(ascentEncSo, "pool", pool);
            SetObjectRef(ascentEncSo, "definition", enemyShipDef);
            ascentEncSo.FindProperty("shipCount").intValue = 3;
            ascentEncSo.FindProperty("spawnRadius").floatValue = 260f;
            ascentEncSo.FindProperty("initialDelay").floatValue = 5f;
            ascentEncSo.FindProperty("requiredCompletedScene").stringValue = "";
            ascentEncSo.FindProperty("clearedFlag").stringValue = "ep11_ascent_cleared";
            SetObjectRef(ascentEncSo, "spawnDialogue", corvetteDialogue);
            ascentEncSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — THE EXIT POINT".
            var exitBoxGo = BuildTransitionBox("ToExitPointBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — THE EXIT POINT",
                out var exitBtn, out var exitTransition);
            var etSo = new SerializedObject(exitTransition);
            etSo.FindProperty("onFootScene").stringValue = Galaxy2Ep11ExitPointSceneName;
            etSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(exitBtn.onClick,
                new UnityEngine.Events.UnityAction(exitTransition.LoadOnFootScene));
            exitBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var ascentGateGo = new GameObject("AscentClearedGate");
            var ascentGate = ascentGateGo.AddComponent<EncounterClearedActivator>();
            var ascentGateSo = new SerializedObject(ascentGate);
            var activateProp = ascentGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = fileGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = exitBoxGo;
            ascentGateSo.FindProperty("clearedFlag").stringValue = "ep11_ascent_cleared";
            ascentGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep11AscentScenePath);
            EnsureScenesInBuild(Galaxy2Ep11AscentScenePath);

            Debug.Log($"[Space Samurai] EP11 Ascent scene built at {Galaxy2Ep11AscentScenePath}. " +
                      "Space above Verdis Prime (green-blue jungle-moon sphere below/behind with sparse lights) + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: corvette_barks spawn (GuardEncounter, 3 ships, 5s delay, pursuit-mode) → " +
                      "on cleared, EncounterClearedActivator plays the_file + " +
                      "reveals JUMP — THE EXIT POINT (transition to Exit Point space).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP11 Exit Point", priority = 127)]
        public static void BuildEp11ExitPoint()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Debris-heavy transit lane: scattered unlit debris chunks under universe.
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

            // Scatter debris chunks under the universe.
            var debrisPositions = new Vector3[]
            {
                new Vector3(-500f, -300f, -1500f),
                new Vector3(400f, 200f, -1200f),
                new Vector3(-300f, 400f, -1800f),
                new Vector3(600f, -100f, -1400f),
                new Vector3(200f, 300f, -2000f)
            };

            foreach (var pos in debrisPositions)
            {
                AddUnlitVisual(universe, $"Debris{Random.Range(1, 99)}", pos,
                    new Vector3(60f + Random.Range(-20f, 20f), 50f + Random.Range(-15f, 15f), 70f + Random.Range(-20f, 20f)),
                    PrimitiveType.Cube, new Color(0.3f, 0.28f, 0.25f));
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
            var huntersDialogue = BuildDialoguePlayer("Dialogue_ExitHuntersBarks", new Vector3(0f, 1.62f, 0.8f),
                Ep11Lines.Get("exit_hunters_barks"), null, "exit_hunters_barks", clipPrefix: "ep11");
            var huntersGo = huntersDialogue.gameObject;
            huntersGo.transform.SetParent(cockpit, false);
            huntersGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            huntersGo.transform.localRotation = Quaternion.identity;

            // Merchant trail dialogue (revealed when the fight clears).
            var merchantDialogue = BuildDialoguePlayer("Dialogue_MerchantTrail", new Vector3(0f, 1.62f, 0.8f),
                Ep11Lines.Get("merchant_trail"), null, "merchant_trail", clipPrefix: "ep11");
            var merchantGo = merchantDialogue.gameObject;
            merchantGo.transform.SetParent(cockpit, false);
            merchantGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            merchantGo.transform.localRotation = Quaternion.identity;
            var merchantSo = new SerializedObject(merchantDialogue);
            merchantSo.FindProperty("playOnStart").boolValue = true;
            merchantSo.ApplyModifiedPropertiesWithoutUndo();
            merchantGo.SetActive(false); // Initially inactive; EncounterClearedActivator will activate it.

            // ---- Enemy Encounter: Bounty Rig (pursuit-mode, DISABLES instead of destroys) ----
            var exitEncGo = new GameObject("BountyRig");
            var exitEnc = exitEncGo.AddComponent<GuardEncounter>();
            var exitEncSo = new SerializedObject(exitEnc);
            SetObjectRef(exitEncSo, "player", shipCtrl);
            SetObjectRef(exitEncSo, "universe", universe);
            SetObjectRef(exitEncSo, "pool", pool);
            SetObjectRef(exitEncSo, "definition", enemyShipDef);
            exitEncSo.FindProperty("shipCount").intValue = 1;
            exitEncSo.FindProperty("spawnRadius").floatValue = 260f;
            exitEncSo.FindProperty("initialDelay").floatValue = 4f;
            exitEncSo.FindProperty("requiredCompletedScene").stringValue = "";
            exitEncSo.FindProperty("clearedFlag").stringValue = "ep11_exit_cleared";
            SetObjectRef(exitEncSo, "spawnDialogue", huntersDialogue);
            // NEW: Set disableInsteadOfDestroy + disableHealthFraction for the bounty rig
            exitEncSo.FindProperty("disableInsteadOfDestroy").boolValue = true;
            exitEncSo.FindProperty("disableHealthFraction").floatValue = 0.2f;
            exitEncSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var exitGateGo = new GameObject("ExitClearedGate");
            var exitGate = exitGateGo.AddComponent<EncounterClearedActivator>();
            var exitGateSo = new SerializedObject(exitGate);
            var activateProp = exitGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = merchantGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnMapBoxGo;
            exitGateSo.FindProperty("clearedFlag").stringValue = "ep11_exit_cleared";
            var extraFlagProp = exitGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep11_complete";
            }
            exitGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep11ExitPointScenePath);
            EnsureScenesInBuild(Galaxy2Ep11ExitPointScenePath);

            Debug.Log($"[Space Samurai] EP11 Exit Point scene built at {Galaxy2Ep11ExitPointScenePath}. " +
                      "Debris-heavy transit lane with scattered unlit debris chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: exit_hunters_barks spawn (GuardEncounter, 1 rig, 4s delay, pursuit-mode, disableInsteadOfDestroy) → " +
                      "on cleared, EncounterClearedActivator plays merchant_trail + sets extraFlag 'ep11_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace).");
        }
    }
}

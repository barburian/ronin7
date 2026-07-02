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
    /// EP12 "The Memory Merchant" space scene builders: Graveyard (core reveal + gunboat fight) and Pursuit (cruiser finale).
    /// Mirrors EP11 pattern closely (cockpit, hull, ShipController, ProjectilePool, ShipWeaponController,
    /// BuildStarfield, universe root, GameState startMode=SpaceFlight, farClip 6000).
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: Scene paths and names (Galaxy2Ep12GraveyardScenePath, Galaxy2Ep12PursuitScenePath, etc.) are defined in Galaxy2Builder.cs.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 Graveyard", priority = 133)]
        public static void BuildEp12Graveyard()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ore-hauler graveyard: scattered debris chunks (larger unlit grey-brown debris).
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

            // Scatter ~5 ore-hauler debris chunks under the universe (grey-brown, larger 80-120 units).
            var debrisPositions = new Vector3[]
            {
                new Vector3(-400f, -250f, -1400f),
                new Vector3(350f, 150f, -1600f),
                new Vector3(-200f, 300f, -1800f),
                new Vector3(500f, -50f, -1300f),
                new Vector3(100f, 200f, -1900f)
            };

            foreach (var pos in debrisPositions)
            {
                AddUnlitVisual(universe, $"OreHauler{Random.Range(1, 99)}", pos,
                    new Vector3(80f + Random.Range(-20f, 20f), 90f + Random.Range(-15f, 20f), 100f + Random.Range(-20f, 25f)),
                    PrimitiveType.Cube, new Color(0.32f, 0.30f, 0.27f));
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
            var coreRevealDialogue = BuildDialoguePlayer("Dialogue_GraveyardCoreReveal", new Vector3(0f, 1.62f, 0.8f),
                Ep12Lines.Get("the_outside_hand"), null, "the_outside_hand", clipPrefix: "ep12");
            var coreRevealGo = coreRevealDialogue.gameObject;
            coreRevealGo.transform.SetParent(cockpit, false);
            coreRevealGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            coreRevealGo.transform.localRotation = Quaternion.identity;
            var coreRevealSo = new SerializedObject(coreRevealDialogue);
            coreRevealSo.FindProperty("playOnStart").boolValue = true;
            coreRevealSo.ApplyModifiedPropertiesWithoutUndo();

            var gunboatBarksDialogue = BuildDialoguePlayer("Dialogue_GunboatBarks", new Vector3(0f, 1.62f, 0.8f),
                Ep12Lines.Get("gunboat_barks"), null, "gunboat_barks", clipPrefix: "ep12");
            var gunboatGo = gunboatBarksDialogue.gameObject;
            gunboatGo.transform.SetParent(cockpit, false);
            gunboatGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            gunboatGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Gunboat Patrol ----
            var graveyardEncGo = new GameObject("GraveyardPatrol");
            var graveyardEnc = graveyardEncGo.AddComponent<GuardEncounter>();
            var graveyardEncSo = new SerializedObject(graveyardEnc);
            SetObjectRef(graveyardEncSo, "player", shipCtrl);
            SetObjectRef(graveyardEncSo, "universe", universe);
            SetObjectRef(graveyardEncSo, "pool", pool);
            SetObjectRef(graveyardEncSo, "definition", enemyShipDef);
            graveyardEncSo.FindProperty("shipCount").intValue = 2;
            graveyardEncSo.FindProperty("spawnRadius").floatValue = 260f;
            graveyardEncSo.FindProperty("initialDelay").floatValue = 5f;
            graveyardEncSo.FindProperty("requiredCompletedScene").stringValue = "";
            graveyardEncSo.FindProperty("clearedFlag").stringValue = "ep12_graveyard_cleared";
            SetObjectRef(graveyardEncSo, "spawnDialogue", gunboatBarksDialogue);
            graveyardEncSo.FindProperty("disableInsteadOfDestroy").boolValue = true;
            graveyardEncSo.FindProperty("disableHealthFraction").floatValue = 0.2f;
            graveyardEncSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "BOARD — THE CUTTER".
            var cutterBoxGo = BuildTransitionBox("ToCutterBox", new Vector3(0f, 1.2f, 0.8f), "BOARD — THE CUTTER",
                out var cutterBtn, out var cutterTransition);
            var ctSo = new SerializedObject(cutterTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy2Ep12CutterSceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(cutterBtn.onClick,
                new UnityEngine.Events.UnityAction(cutterTransition.LoadOnFootScene));
            cutterBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var graveyardGateGo = new GameObject("GraveyardClearedGate");
            var graveyardGate = graveyardGateGo.AddComponent<EncounterClearedActivator>();
            var graveyardGateSo = new SerializedObject(graveyardGate);
            var activateProp = graveyardGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = cutterBoxGo;
            graveyardGateSo.FindProperty("clearedFlag").stringValue = "ep12_graveyard_cleared";
            graveyardGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12GraveyardScenePath);
            EnsureScenesInBuild(Galaxy2Ep12GraveyardScenePath);

            Debug.Log($"[Space Samurai] EP12 Graveyard scene built at {Galaxy2Ep12GraveyardScenePath}. " +
                      "Ore-hauler graveyard debris field with scattered unlit debris chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: the_outside_hand plays on start (core reveal) → " +
                      "gunboat_barks spawn (GuardEncounter, 2 ships, 5s delay, disableInsteadOfDestroy) → " +
                      "on cleared, EncounterClearedActivator reveals BOARD — THE CUTTER (transition to Cutter on-foot).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 Pursuit", priority = 134)]
        public static void BuildEp12Pursuit()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Construction-frame debris field: scattered unlit lattice chunks (grey).
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

            // Scatter ~6 construction-frame lattice chunks under the universe (long thin grey boxes).
            var debrisPositions = new Vector3[]
            {
                new Vector3(-450f, -200f, -1500f),
                new Vector3(300f, 100f, -1700f),
                new Vector3(-150f, 250f, -1900f),
                new Vector3(550f, 0f, -1400f),
                new Vector3(50f, 300f, -2000f),
                new Vector3(-600f, 150f, -1600f)
            };

            foreach (var pos in debrisPositions)
            {
                AddUnlitVisual(universe, $"FrameLattice{Random.Range(1, 99)}", pos,
                    new Vector3(150f + Random.Range(-30f, 30f), 20f + Random.Range(-5f, 10f), 40f + Random.Range(-10f, 15f)),
                    PrimitiveType.Cube, new Color(0.28f, 0.28f, 0.30f));
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
            var defectorDialogue = BuildDialoguePlayer("Dialogue_TheDefector", new Vector3(0f, 1.62f, 0.8f),
                Ep12Lines.Get("the_defector"), null, "the_defector", clipPrefix: "ep12");
            var defectorGo = defectorDialogue.gameObject;
            defectorGo.transform.SetParent(cockpit, false);
            defectorGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            defectorGo.transform.localRotation = Quaternion.identity;
            var defectorSo = new SerializedObject(defectorDialogue);
            defectorSo.FindProperty("playOnStart").boolValue = true;
            defectorSo.ApplyModifiedPropertiesWithoutUndo();

            var cruiserBarksDialogue = BuildDialoguePlayer("Dialogue_CruiserBarks", new Vector3(0f, 1.62f, 0.8f),
                Ep12Lines.Get("cruiser_barks"), null, "cruiser_barks", clipPrefix: "ep12");
            var cruiserGo = cruiserBarksDialogue.gameObject;
            cruiserGo.transform.SetParent(cockpit, false);
            cruiserGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            cruiserGo.transform.localRotation = Quaternion.identity;

            var viewportDialogue = BuildDialoguePlayer("Dialogue_TheViewport", new Vector3(0f, 1.62f, 0.8f),
                Ep12Lines.Get("the_viewport"), null, "the_viewport", clipPrefix: "ep12");
            var viewportGo = viewportDialogue.gameObject;
            viewportGo.transform.SetParent(cockpit, false);
            viewportGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            viewportGo.transform.localRotation = Quaternion.identity;
            var viewportSo = new SerializedObject(viewportDialogue);
            viewportSo.FindProperty("playOnStart").boolValue = true;
            viewportSo.ApplyModifiedPropertiesWithoutUndo();
            viewportGo.SetActive(false); // Initially inactive; EncounterClearedActivator will activate it.

            // ---- Enemy Encounter: Hollow Kings Cruiser (pursuit-mode) ----
            var pursuitEncGo = new GameObject("HollowKingsCruiser");
            var pursuitEnc = pursuitEncGo.AddComponent<GuardEncounter>();
            var pursuitEncSo = new SerializedObject(pursuitEnc);
            SetObjectRef(pursuitEncSo, "player", shipCtrl);
            SetObjectRef(pursuitEncSo, "universe", universe);
            SetObjectRef(pursuitEncSo, "pool", pool);
            SetObjectRef(pursuitEncSo, "definition", enemyShipDef);
            pursuitEncSo.FindProperty("shipCount").intValue = 1;
            pursuitEncSo.FindProperty("spawnRadius").floatValue = 300f;
            pursuitEncSo.FindProperty("initialDelay").floatValue = 5f;
            pursuitEncSo.FindProperty("requiredCompletedScene").stringValue = "";
            pursuitEncSo.FindProperty("clearedFlag").stringValue = "ep12_pursuit_cleared";
            SetObjectRef(pursuitEncSo, "spawnDialogue", cruiserBarksDialogue);
            pursuitEncSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var pursuitGateGo = new GameObject("PursuitClearedGate");
            var pursuitGate = pursuitGateGo.AddComponent<EncounterClearedActivator>();
            var pursuitGateSo = new SerializedObject(pursuitGate);
            var activateProp = pursuitGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = viewportGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnMapBoxGo;
            pursuitGateSo.FindProperty("clearedFlag").stringValue = "ep12_pursuit_cleared";
            var extraFlagProp = pursuitGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep12_complete";
            }
            pursuitGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12PursuitScenePath);
            EnsureScenesInBuild(Galaxy2Ep12PursuitScenePath);

            Debug.Log($"[Space Samurai] EP12 Pursuit scene built at {Galaxy2Ep12PursuitScenePath}. " +
                      "Construction-frame debris field with scattered unlit lattice chunks + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: the_defector plays on start (Morrigan joins) → " +
                      "cruiser_barks spawn (GuardEncounter, 1 cruiser, 5s delay, pursuit-mode) → " +
                      "on cleared, EncounterClearedActivator plays the_viewport + sets extraFlag 'ep12_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP12 Scenes", priority = 128)]
        public static void BuildAllEp12Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP12 scenes in order: Shard Market, Vault, Graveyard, Cutter, The Shard, Pursuit, Galaxy 2...");
            BuildEp12ShardMarket();
            BuildEp12Vault();
            BuildEp12Graveyard();
            BuildEp12Cutter();
            BuildEp12TheShard();
            BuildEp12Pursuit();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP12 scenes built successfully! Galaxy 2 hub rebuilt to register EP12 completions.");
        }
    }
}

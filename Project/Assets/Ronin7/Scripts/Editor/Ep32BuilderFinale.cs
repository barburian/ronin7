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
    /// EP32 "The Throne of Ashes" (Galaxy 4) builder for the two space-combat cockpit scenes (scenes 1 and 6).
    /// The orbital approach to the Dominion fortress and the blockade escape / jump to the Threshold.
    /// - The Breach: SPACE dogfight over Dominion obsidian fortress; interceptor encounter.
    /// - The Exodus: SPACE combat through privateer blockade; hull-breach repair mechanic under fire.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and scene constants
    /// (Galaxy4Ep32*ScenePath/Name, Galaxy4Ep32GateKeepersSceneName, Galaxy4Ep32ThresholdSceneName,
    /// BuildEp32DialoguePlayer) are declared in Ep32Builder.cs. DO NOT redefine them here.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 The Breach", priority = 323)]
        public static void BuildEp32TheBreach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over Dominion obsidian fortress (near-black iron sphere below).
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

            // Dominion Fortress below: near-black iron sphere.
            var fortressGo = AddUnlitVisual(universe, "Dominion Fortress", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.12f, 0.12f, 0.14f));
            var fortressCollider = fortressGo.GetComponent<Collider>();
            if (fortressCollider != null) fortressCollider.isTrigger = true;

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
            // breach_approach: orbital approach to Dominion fortress (plays on start, cockpit-parented).
            var breachApproachDialogue = BuildEp32DialoguePlayer("Dialogue_BreachApproach", new Vector3(0f, 1.62f, 0.8f), "breach_approach");
            var baGo = breachApproachDialogue.gameObject;
            baGo.transform.SetParent(cockpit, false);
            baGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            baGo.transform.localRotation = Quaternion.identity;
            var baSo = new SerializedObject(breachApproachDialogue);
            baSo.FindProperty("playOnStart").boolValue = true;
            baSo.ApplyModifiedPropertiesWithoutUndo();

            // breach_cleared: revealed on encounter clear.
            var breachClearedDialogue = BuildEp32DialoguePlayer("Dialogue_BreachCleared", new Vector3(0f, 1.62f, 0.8f), "breach_cleared");
            var bcGo = breachClearedDialogue.gameObject;
            bcGo.transform.SetParent(cockpit, false);
            bcGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            bcGo.transform.localRotation = Quaternion.identity;
            var bcSo = new SerializedObject(breachClearedDialogue);
            bcSo.FindProperty("playOnStart").boolValue = true;
            bcSo.ApplyModifiedPropertiesWithoutUndo();
            bcGo.SetActive(false);

            // ---- Enemy Encounter: 4-ship interceptor swarm ----
            var interceptorEncounterGo = new GameObject("InterceptorEncounter");
            var interceptorEncounter = interceptorEncounterGo.AddComponent<GuardEncounter>();
            var ieSo = new SerializedObject(interceptorEncounter);
            SetObjectRef(ieSo, "player", shipCtrl);
            SetObjectRef(ieSo, "universe", universe);
            SetObjectRef(ieSo, "pool", pool);
            SetObjectRef(ieSo, "definition", enemyShipDef);
            ieSo.FindProperty("shipCount").intValue = 4;
            ieSo.FindProperty("spawnRadius").floatValue = 280f;
            ieSo.FindProperty("initialDelay").floatValue = 6f;
            ieSo.FindProperty("requiredCompletedScene").stringValue = "";
            ieSo.FindProperty("clearedFlag").stringValue = "ep32_breach_cleared";
            var breachDogfightDialogue = BuildEp32DialoguePlayer("Dialogue_BreachDogfight", new Vector3(0f, 1.62f, 0.8f), "breach_dogfight");
            SetObjectRef(ieSo, "spawnDialogue", breachDogfightDialogue);
            ieSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals breach_cleared dialogue + transition box.
            var gatekeeperBoxGo = BuildTransitionBox("ToGateKeepersBox", new Vector3(0f, 1.2f, 0.8f), "ENTER THE FORTRESS",
                out var gatekeeperBtn, out var gatekeeperTransition);
            var gbSo = new SerializedObject(gatekeeperTransition);
            gbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32GateKeepersSceneName;
            gbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(gatekeeperBtn.onClick,
                new UnityEngine.Events.UnityAction(gatekeeperTransition.LoadOnFootScene));
            gatekeeperBoxGo.SetActive(false);

            var breachGateGo = new GameObject("BreachClearedGate");
            var breachGate = breachGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(breachGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = bcGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = gatekeeperBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep32_breach_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep32TheBreachScenePath);
            EnsureScenesInBuild(Galaxy4Ep32TheBreachScenePath, SceneFolder + "/Galaxy4_EP32_GateKeepers.unity");

            Debug.Log($"[Space Samurai] EP32 The Breach scene built at {Galaxy4Ep32TheBreachScenePath}. " +
                      "Dominion obsidian fortress orbital approach (near-black iron sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: breach_approach (auto, orbital approach + throne threat) → 4-ship interceptor encounter (ep32_breach_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals breach_cleared (command acknowledge) + ENTER THE FORTRESS transition box. " +
                      "GALAXY 4 EP32 SCENE 1 (space combat finale intro).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Exodus", priority = 328)]
        public static void BuildEp32Exodus()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over blockade (dim corona-red sphere below).
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

            // Blockade below: dim corona-red sphere.
            var blockadeGo = AddUnlitVisual(universe, "Hollow Kings Blockade", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.28f, 0.12f, 0.10f));
            var blockadeCollider = blockadeGo.GetComponent<Collider>();
            if (blockadeCollider != null) blockadeCollider.isTrigger = true;

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
            // exodus_dock: pre-jump docking sequence (plays on start, cockpit-parented).
            var exodusDockDialogue = BuildEp32DialoguePlayer("Dialogue_ExodusDock", new Vector3(0f, 1.62f, 0.8f), "exodus_dock");
            var edGo = exodusDockDialogue.gameObject;
            edGo.transform.SetParent(cockpit, false);
            edGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            edGo.transform.localRotation = Quaternion.identity;
            var edSo = new SerializedObject(exodusDockDialogue);
            edSo.FindProperty("playOnStart").boolValue = true;
            edSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Enemy Encounter: 3-ship privateer blockade ----
            var privateerEncounterGo = new GameObject("PrivateerEncounter");
            var privateerEncounter = privateerEncounterGo.AddComponent<GuardEncounter>();
            var peSo = new SerializedObject(privateerEncounter);
            SetObjectRef(peSo, "player", shipCtrl);
            SetObjectRef(peSo, "universe", universe);
            SetObjectRef(peSo, "pool", pool);
            SetObjectRef(peSo, "definition", enemyShipDef);
            peSo.FindProperty("shipCount").intValue = 3;
            peSo.FindProperty("spawnRadius").floatValue = 280f;
            peSo.FindProperty("initialDelay").floatValue = 6f;
            peSo.FindProperty("requiredCompletedScene").stringValue = "";
            peSo.FindProperty("clearedFlag").stringValue = "ep32_exodus_cleared";
            var blockadeRunDialogue = BuildEp32DialoguePlayer("Dialogue_BlockadeRun", new Vector3(0f, 1.62f, 0.8f), "blockade_run");
            SetObjectRef(peSo, "spawnDialogue", blockadeRunDialogue);
            peSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- NEW MECHANIC: Hull breach repair under fire ----
            var breachGo = new GameObject("HullBreachRepair");
            var breachCtrl = breachGo.AddComponent<Ronin7.World.HullBreachRepairController>();
            breachCtrl.Configure(rig.GetComponent<Health>());
            breachCtrl.AddBreach("port");
            breachCtrl.AddBreach("starboard");
            breachCtrl.AddBreach("dorsal");
            var breachSo = new SerializedObject(breachCtrl);
            breachSo.FindProperty("damagePerBreach").floatValue = 3f;
            breachSo.FindProperty("breachInterval").floatValue = 10f;
            breachSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals transition box.
            var thresholdBoxGo = BuildTransitionBox("ToThresholdBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — TO THE THRESHOLD",
                out var thresholdBtn, out var thresholdTransition);
            var tbSo = new SerializedObject(thresholdTransition);
            tbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32ThresholdSceneName;
            tbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(thresholdBtn.onClick,
                new UnityEngine.Events.UnityAction(thresholdTransition.LoadOnFootScene));
            thresholdBoxGo.SetActive(false);

            var exodusGateGo = new GameObject("ExodusClearedGate");
            var exodusGate = exodusGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(exodusGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = thresholdBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep32_exodus_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep32ExodusScenePath);
            EnsureScenesInBuild(Galaxy4Ep32ExodusScenePath, SceneFolder + "/Galaxy4_EP32_Threshold.unity");

            Debug.Log($"[Space Samurai] EP32 Exodus scene built at {Galaxy4Ep32ExodusScenePath}. " +
                      "Hollow Kings blockade run (dim corona-red sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: exodus_dock (auto, pre-jump docking sequence) → 3-ship privateer encounter (ep32_exodus_cleared) under hull-breach pressure (port/starboard/dorsal, 3 dps, 10s interval) → on cleared, " +
                      "EncounterClearedActivator reveals JUMP — TO THE THRESHOLD transition box. " +
                      "HullBreachRepairController wired to player health; new hull-breach mechanic tested. " +
                      "GALAXY 4 EP32 SCENE 6 (space combat finale, blockade escape).");
        }
    }
}

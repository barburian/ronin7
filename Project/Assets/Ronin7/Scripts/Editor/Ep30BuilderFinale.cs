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
    /// EP30 "The Vault Within" (Galaxy 4) builder for the four space-combat cockpit scenes (scenes 1, 2, 5, 6).
    /// The black-market relay station approach, the Iron Sepulcher thermal-corridor approach, the Hollow Kings
    /// debris-field evasion, and the finale denouement (no combat) as Soren reclaims his name from the vault.
    /// - Price of Memory: SPACE dogfight over black-market relay station; corvette encounter.
    /// - Sepulcher Approach: SPACE dogfight over Iron Sepulcher; strike cruiser encounter; Khall welcome dialogue on clear.
    /// - Hollow Kings: SPACE evasion through mineral debris field; Hollow Kings corvette encounter.
    /// - Name Remains: SPACE denouement (no combat); time-lock reveal and Soren reclaiming his name.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and scene constants
    /// (Galaxy4Ep30*ScenePath/Name, BuildEp30DialoguePlayer, BuildEp30OnFootShell, BuildEp30Npc,
    /// FinishEp30Scene) are declared in Ep30Builder.cs. DO NOT redefine them here.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 Price Of Memory", priority = 309)]
        public static void BuildEp30PriceOfMemory()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over black-market relay station (scarred dark grey sphere below).
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

            // Relay Station below: dark scarred sphere.
            var relayStationGo = AddUnlitVisual(universe, "Relay Station", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.28f, 0.28f, 0.30f));
            var relayStationCollider = relayStationGo.GetComponent<Collider>();
            if (relayStationCollider != null) relayStationCollider.isTrigger = true;

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
            // mera_intro: Mera returns with vault codes (plays on start, cockpit-parented).
            var meraIntroDialogue = BuildEp30DialoguePlayer("Dialogue_MeraIntro", new Vector3(0f, 1.62f, 0.8f), "mera_intro");
            var miGo = meraIntroDialogue.gameObject;
            miGo.transform.SetParent(cockpit, false);
            miGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            miGo.transform.localRotation = Quaternion.identity;
            var miSo = new SerializedObject(meraIntroDialogue);
            miSo.FindProperty("playOnStart").boolValue = true;
            miSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Enemy Encounter: 3-ship corvette swarm ----
            var corvettEncounterGo = new GameObject("CorvetteEncounter");
            var corvettEncounter = corvettEncounterGo.AddComponent<GuardEncounter>();
            var ceSo = new SerializedObject(corvettEncounter);
            SetObjectRef(ceSo, "player", shipCtrl);
            SetObjectRef(ceSo, "universe", universe);
            SetObjectRef(ceSo, "pool", pool);
            SetObjectRef(ceSo, "definition", enemyShipDef);
            ceSo.FindProperty("shipCount").intValue = 3;
            ceSo.FindProperty("spawnRadius").floatValue = 280f;
            ceSo.FindProperty("initialDelay").floatValue = 6f;
            ceSo.FindProperty("requiredCompletedScene").stringValue = "";
            ceSo.FindProperty("clearedFlag").stringValue = "ep30_corvettes_cleared";
            var corvettDogfightDialogue = BuildEp30DialoguePlayer("Dialogue_CorvetteDogfight", new Vector3(0f, 1.62f, 0.8f), "corvette_dogfight");
            SetObjectRef(ceSo, "spawnDialogue", corvettDogfightDialogue);
            ceSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals transition box.
            var approachBoxGo = BuildTransitionBox("ToSepulcherBox", new Vector3(0f, 1.2f, 0.8f), "APPROACH THE SEPULCHER",
                out var approachBtn, out var approachTransition);
            var abSo = new SerializedObject(approachTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy4Ep30SepulcherApproachSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(approachBtn.onClick,
                new UnityEngine.Events.UnityAction(approachTransition.LoadOnFootScene));
            approachBoxGo.SetActive(false);

            var corvettGateGo = new GameObject("CorvetteClearedGate");
            var corvettGate = corvettGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(corvettGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = approachBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep30_corvettes_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy4Ep30PriceOfMemoryScenePath);
            EnsureScenesInBuild(Galaxy4Ep30PriceOfMemoryScenePath, Galaxy4Ep30SepulcherApproachScenePath);

            Debug.Log($"[Space Samurai] EP30 Price Of Memory scene built at {Galaxy4Ep30PriceOfMemoryScenePath}. " +
                      "Black-market relay station (dark scarred sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: mera_intro (auto, Mera returns with codes) → 3-ship corvette encounter (ep30_corvettes_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals APPROACH THE SEPULCHER transition box. " +
                      "GALAXY 4 EP30 SCENE 1 (space combat intro).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 Sepulcher Approach", priority = 310)]
        public static void BuildEp30SepulcherApproach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over Iron Sepulcher (dark metallic sphere below).
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

            // Iron Sepulcher below: dark metallic station sphere.
            var sepulcherGo = AddUnlitVisual(universe, "Iron Sepulcher", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.32f, 0.33f, 0.36f));
            var sepulcherCollider = sepulcherGo.GetComponent<Collider>();
            if (sepulcherCollider != null) sepulcherCollider.isTrigger = true;

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
            // sepulcher_briefing: thermal-corridor approach briefing (plays on start).
            var briefingDialogue = BuildEp30DialoguePlayer("Dialogue_SepulcherBriefing", new Vector3(0f, 1.62f, 0.8f), "sepulcher_briefing");
            var brGo = briefingDialogue.gameObject;
            brGo.transform.SetParent(cockpit, false);
            brGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            brGo.transform.localRotation = Quaternion.identity;
            var brSo = new SerializedObject(briefingDialogue);
            brSo.FindProperty("playOnStart").boolValue = true;
            brSo.ApplyModifiedPropertiesWithoutUndo();

            // khall_welcome: revealed on encounter clear.
            var khallWelcomeDialogue = BuildEp30DialoguePlayer("Dialogue_KhallWelcome", new Vector3(0f, 1.62f, 0.8f), "khall_welcome");
            var kwGo = khallWelcomeDialogue.gameObject;
            kwGo.transform.SetParent(cockpit, false);
            kwGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            kwGo.transform.localRotation = Quaternion.identity;
            var kwSo = new SerializedObject(khallWelcomeDialogue);
            kwSo.FindProperty("playOnStart").boolValue = true;
            kwSo.ApplyModifiedPropertiesWithoutUndo();
            kwGo.SetActive(false);

            // ---- Enemy Encounter: 2-ship strike cruiser pair ----
            var strikeEncounterGo = new GameObject("StrikeEncounter");
            var strikeEncounter = strikeEncounterGo.AddComponent<GuardEncounter>();
            var seSo = new SerializedObject(strikeEncounter);
            SetObjectRef(seSo, "player", shipCtrl);
            SetObjectRef(seSo, "universe", universe);
            SetObjectRef(seSo, "pool", pool);
            SetObjectRef(seSo, "definition", enemyShipDef);
            seSo.FindProperty("shipCount").intValue = 2;
            seSo.FindProperty("spawnRadius").floatValue = 280f;
            seSo.FindProperty("initialDelay").floatValue = 6f;
            seSo.FindProperty("requiredCompletedScene").stringValue = "";
            seSo.FindProperty("clearedFlag").stringValue = "ep30_approach_cleared";
            SetObjectRef(seSo, "spawnDialogue", null); // Briefing already played
            seSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals khall_welcome + transition box.
            var breachBoxGo = BuildTransitionBox("ToBreachBox", new Vector3(0f, 1.2f, 0.8f), "BREACH THE HULL",
                out var breachBtn, out var breachTransition);
            var bbSo = new SerializedObject(breachTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep30IntoTheIronSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(breachBtn.onClick,
                new UnityEngine.Events.UnityAction(breachTransition.LoadOnFootScene));
            breachBoxGo.SetActive(false);

            var strikeGateGo = new GameObject("StrikeClearedGate");
            var strikeGate = strikeGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(strikeGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = kwGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = breachBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep30_approach_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy4Ep30SepulcherApproachScenePath);
            EnsureScenesInBuild(Galaxy4Ep30SepulcherApproachScenePath, Galaxy4Ep30IntoTheIronScenePath);

            Debug.Log($"[Space Samurai] EP30 Sepulcher Approach scene built at {Galaxy4Ep30SepulcherApproachScenePath}. " +
                      "Iron Sepulcher thermal-corridor approach (dark metallic sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: sepulcher_briefing (auto, thermal-lock window countdown) → 2-ship strike cruiser encounter (ep30_approach_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals khall_welcome (hologram) + BREACH THE HULL transition box. " +
                      "GALAXY 4 EP30 SCENE 2 (space combat + Khall intel).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 Hollow Kings", priority = 313)]
        public static void BuildEp30HollowKings()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over mineral debris field (dim asteroid-like sphere below).
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

            // Debris Field below: dim asteroid-like sphere.
            var debrisGo = AddUnlitVisual(universe, "Debris Field", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.26f, 0.25f, 0.24f));
            var debrisCollider = debrisGo.GetComponent<Collider>();
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
            // hollow_kings_offer: broker offer + confrontation (plays on start).
            var offerDialogue = BuildEp30DialoguePlayer("Dialogue_HollowKingsOffer", new Vector3(0f, 1.62f, 0.8f), "hollow_kings_offer");
            var ofGo = offerDialogue.gameObject;
            ofGo.transform.SetParent(cockpit, false);
            ofGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ofGo.transform.localRotation = Quaternion.identity;
            var ofSo = new SerializedObject(offerDialogue);
            ofSo.FindProperty("playOnStart").boolValue = true;
            ofSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Enemy Encounter: 2-ship Hollow Kings corvettes ----
            var hkEncounterGo = new GameObject("HollowKingsEncounter");
            var hkEncounter = hkEncounterGo.AddComponent<GuardEncounter>();
            var hkSo = new SerializedObject(hkEncounter);
            SetObjectRef(hkSo, "player", shipCtrl);
            SetObjectRef(hkSo, "universe", universe);
            SetObjectRef(hkSo, "pool", pool);
            SetObjectRef(hkSo, "definition", enemyShipDef);
            hkSo.FindProperty("shipCount").intValue = 2;
            hkSo.FindProperty("spawnRadius").floatValue = 280f;
            hkSo.FindProperty("initialDelay").floatValue = 6f;
            hkSo.FindProperty("requiredCompletedScene").stringValue = "";
            hkSo.FindProperty("clearedFlag").stringValue = "ep30_hollowkings_cleared";
            var debrisEvadeDialogue = BuildEp30DialoguePlayer("Dialogue_DebrisEvade", new Vector3(0f, 1.62f, 0.8f), "debris_evade");
            SetObjectRef(hkSo, "spawnDialogue", debrisEvadeDialogue);
            hkSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals transition box.
            var jumpBoxGo = BuildTransitionBox("ToJumpBox", new Vector3(0f, 1.2f, 0.8f), "JUMP POINT",
                out var jumpBtn, out var jumpTransition);
            var jbSo = new SerializedObject(jumpTransition);
            jbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep30NameRemainsSceneName;
            jbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(jumpBtn.onClick,
                new UnityEngine.Events.UnityAction(jumpTransition.LoadOnFootScene));
            jumpBoxGo.SetActive(false);

            var hkGateGo = new GameObject("HollowKingsClearedGate");
            var hkGate = hkGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(hkGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = jumpBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep30_hollowkings_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy4Ep30HollowKingsScenePath);
            EnsureScenesInBuild(Galaxy4Ep30HollowKingsScenePath, Galaxy4Ep30NameRemainsScenePath);

            Debug.Log($"[Space Samurai] EP30 Hollow Kings scene built at {Galaxy4Ep30HollowKingsScenePath}. " +
                      "Debris field evasion vector (dim asteroid-like sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: hollow_kings_offer (auto, broker offer + Soren refusal + Mera confrontation) → 2-ship Hollow Kings corvette encounter (ep30_hollowkings_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals JUMP POINT transition box. " +
                      "GALAXY 4 EP30 SCENE 5 (space combat evasion).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 Name Remains", priority = 314)]
        public static void BuildEp30NameRemains()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: jump-gate / folded-space aesthetic (deep blue gate sphere below).
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

            // Jump Gate below: deep blue folded-space aesthetic.
            var gateGo = AddUnlitVisual(universe, "Jump Gate", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.20f, 0.35f, 0.55f));
            var gateCollider = gateGo.GetComponent<Collider>();
            if (gateCollider != null) gateCollider.isTrigger = true;

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
            // name_remains: Kessler diagnostic + time-lock reveal + Soren reclaims his name (plays on start).
            var nameRemainsDialogue = BuildEp30DialoguePlayer("Dialogue_NameRemains", new Vector3(0f, 1.62f, 0.8f), "name_remains");
            var nrGo = nameRemainsDialogue.gameObject;
            nrGo.transform.SetParent(cockpit, false);
            nrGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            nrGo.transform.localRotation = Quaternion.identity;
            var nrSo = new SerializedObject(nameRemainsDialogue);
            nrSo.FindProperty("playOnStart").boolValue = true;
            nrSo.ApplyModifiedPropertiesWithoutUndo();

            // NO GuardEncounter, NO combat.

            // Finale return box: "RETURN — TO THE STARS" (ACTIVE from start).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(true); // Active from start, no combat gate.

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep30_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of EP30: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep30NameRemainsScenePath);
            EnsureScenesInBuild(Galaxy4Ep30NameRemainsScenePath);

            Debug.Log($"[Space Samurai] EP30 Name Remains scene built at {Galaxy4Ep30NameRemainsScenePath}. " +
                      "SPACE denouement (no combat). Jump gate / folded-space aesthetic (deep blue sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: name_remains (auto, Kessler diagnostic on killswitch deletion, time-lock reveal, Soren reclaims his name). " +
                      "No encounter. RETURN — TO THE STARS button (ACTIVE from start) wired to CampaignFlagSetter (ep30_complete ONLY, no ally recruit) + ReturnToSpace. " +
                      "GALAXY 4 EP30 FINALE (last scene, space denouement).");
        }
    }
}

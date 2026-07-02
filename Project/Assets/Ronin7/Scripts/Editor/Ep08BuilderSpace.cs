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
    /// EP08 "The Architect of Mercy" space scene builders. Builds the Orbit Break and Nebula Edge phases
    /// of the escape from Vel Keth system where Cipher battles a Dominion frigate bracket and a final
    /// interceptor pursuit before jumping out of the galaxy. Wires cockpit dialogue, GuardEncounter with
    /// enemy fleet formations, scene transitions, and completion flags.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep08OrbitBreakScenePath = SceneFolder + "/Galaxy1_EP08_OrbitBreak.unity";
        private const string Galaxy1Ep08NebulaEdgeScenePath = SceneFolder + "/Galaxy1_EP08_NebulaEdge.unity";

        private static readonly string Galaxy1Ep08OrbitBreakSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08OrbitBreakScenePath);
        private static readonly string Galaxy1Ep08NebulaEdgeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08NebulaEdgeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Orbit Break", priority = 103)]
        public static void BuildEp08OrbitBreak()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
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

            // Vel Keth as big red sphere below/behind the corsair.
            var velKethGo = AddUnlitVisual(universe, "Vel Keth", new Vector3(-1500f, -800f, -2500f),
                Vector3.one * 700f, PrimitiveType.Sphere, new Color(0.75f, 0.40f, 0.35f)); // red planet
            var velKethCollider = velKethGo.GetComponent<Collider>();
            if (velKethCollider != null) velKethCollider.isTrigger = true;

            // 2 large frigate hull visuals (unlit block clusters).
            var frigateColor = new Color(0.45f, 0.42f, 0.40f);
            var frigate1 = AddUnlitVisual(universe, "Frigate Hulk 1", new Vector3(-2000f, 200f, -3000f),
                new Vector3(300f, 150f, 400f), PrimitiveType.Cube, frigateColor);
            var f1Collider = frigate1.GetComponent<Collider>();
            if (f1Collider != null) f1Collider.isTrigger = true;

            var frigate2 = AddUnlitVisual(universe, "Frigate Hulk 2", new Vector3(2500f, -300f, -2800f),
                new Vector3(280f, 140f, 380f), PrimitiveType.Cube, frigateColor);
            var f2Collider = frigate2.GetComponent<Collider>();
            if (f2Collider != null) f2Collider.isTrigger = true;

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
            var frigateBracketDialogue = BuildEp08DialoguePlayer("Dialogue_FrigateBracket", new Vector3(0f, 1.62f, 0.8f),
                "frigate_bracket");
            var frigateBracketGo = frigateBracketDialogue.gameObject;
            frigateBracketGo.transform.SetParent(cockpit, false);
            frigateBracketGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            frigateBracketGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: 3-ship frigate bracket ----
            var frigateEncounterGo = new GameObject("FrigateBracketEncounter");
            var frigateEncounter = frigateEncounterGo.AddComponent<GuardEncounter>();
            var frigateEncounterSo = new SerializedObject(frigateEncounter);
            SetObjectRef(frigateEncounterSo, "player", shipCtrl);
            SetObjectRef(frigateEncounterSo, "universe", universe);
            SetObjectRef(frigateEncounterSo, "pool", pool);
            SetObjectRef(frigateEncounterSo, "definition", enemyShipDef);
            frigateEncounterSo.FindProperty("shipCount").intValue = 3;
            frigateEncounterSo.FindProperty("spawnRadius").floatValue = 280f;
            frigateEncounterSo.FindProperty("initialDelay").floatValue = 5f;
            frigateEncounterSo.FindProperty("requiredCompletedScene").stringValue = "";
            frigateEncounterSo.FindProperty("clearedFlag").stringValue = "ep08_bracket_cleared";
            SetObjectRef(frigateEncounterSo, "spawnDialogue", frigateBracketDialogue);
            frigateEncounterSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "PUNCH THROUGH".
            var nebulaBoxGo = BuildTransitionBox("ToNebulaBox", new Vector3(0f, 1.2f, 0.8f), "PUNCH THROUGH",
                out var nebulaBtn, out var nebulaTransition);
            var ntSo = new SerializedObject(nebulaTransition);
            ntSo.FindProperty("onFootScene").stringValue = Galaxy1Ep08NebulaEdgeSceneName;
            ntSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(nebulaBtn.onClick,
                new UnityEngine.Events.UnityAction(nebulaTransition.LoadOnFootScene));
            nebulaBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var frigateGateGo = new GameObject("FrigateClearedGate");
            var frigateGate = frigateGateGo.AddComponent<EncounterClearedActivator>();
            var frigateSo = new SerializedObject(frigateGate);
            var activateProp = frigateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = nebulaBoxGo;
            frigateSo.FindProperty("clearedFlag").stringValue = "ep08_bracket_cleared";
            frigateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08OrbitBreakScenePath);
            EnsureScenesInBuild(Galaxy1Ep08OrbitBreakScenePath);

            Debug.Log($"[Space Samurai] EP08 Orbit Break scene built at {Galaxy1Ep08OrbitBreakScenePath}. " +
                      "Space over Vel Keth system (red planet below/behind, 2 frigate hulk visuals). " +
                      "Cockpit with canopy + HUD. " +
                      "Flow: frigate_bracket spawn (GuardEncounter, 3 ships, 5s delay) → on cleared, " +
                      "EncounterClearedActivator reveals PUNCH THROUGH (LoadOnFootScene, chains to EP08 Nebula Edge).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Nebula Edge", priority = 104)]
        public static void BuildEp08NebulaEdge()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Nebula fringe: tinted fog + colored accent visuals.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.25f, 0.45f); // nebula purple-blue tint
            RenderSettings.fogDensity = 0.015f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.05f, 0.12f); // dim purple tones
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(0.6f, 0.5f, 0.8f); // purple-tinted key
            lightGo.transform.rotation = Quaternion.Euler(30f, 50f, 0f);

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

            // Nebula fringe accent visuals (tinted geometric shapes).
            var nebulaTint1 = new Color(0.45f, 0.25f, 0.65f, 0.4f);
            var nebulaTint2 = new Color(0.55f, 0.35f, 0.75f, 0.35f);
            var nebulaVis1 = AddUnlitVisual(universe, "NebulaAccent1", new Vector3(1500f, 300f, 1200f),
                new Vector3(500f, 300f, 600f), PrimitiveType.Cube, nebulaTint1);
            var nv1Col = nebulaVis1.GetComponent<Collider>();
            if (nv1Col != null) nv1Col.isTrigger = true;

            var nebulaVis2 = AddUnlitVisual(universe, "NebulaAccent2", new Vector3(-1800f, -400f, 1400f),
                new Vector3(400f, 250f, 500f), PrimitiveType.Cube, nebulaTint2);
            var nv2Col = nebulaVis2.GetComponent<Collider>();
            if (nv2Col != null) nv2Col.isTrigger = true;

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
            // Covenant epilogue (auto) at scene start.
            var covenantEpilogueDialogue = BuildEp08DialoguePlayer("Dialogue_CovenantEpilogue", new Vector3(0f, 1.62f, 0.8f),
                "covenant_epilogue");
            var covenantGo = covenantEpilogueDialogue.gameObject;
            covenantGo.transform.SetParent(cockpit, false);
            covenantGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            covenantGo.transform.localRotation = Quaternion.identity;
            var covenantSo = new SerializedObject(covenantEpilogueDialogue);
            covenantSo.FindProperty("playOnStart").boolValue = true;
            covenantSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Enemy Encounter: 1-ship final interceptor ----
            var interceptorGo = new GameObject("FinalInterceptor");
            var interceptor = interceptorGo.AddComponent<GuardEncounter>();
            var interceptorSo = new SerializedObject(interceptor);
            SetObjectRef(interceptorSo, "player", shipCtrl);
            SetObjectRef(interceptorSo, "universe", universe);
            SetObjectRef(interceptorSo, "pool", pool);
            SetObjectRef(interceptorSo, "definition", enemyShipDef);
            interceptorSo.FindProperty("shipCount").intValue = 1;
            interceptorSo.FindProperty("spawnRadius").floatValue = 250f;
            interceptorSo.FindProperty("initialDelay").floatValue = 30f; // 30s delay for epilogue
            interceptorSo.FindProperty("requiredCompletedScene").stringValue = "";
            interceptorSo.FindProperty("clearedFlag").stringValue = "ep08_interceptor_cleared";

            // Build spawn dialogue for this encounter.
            var finalPursuitDialogue = BuildEp08DialoguePlayer("Dialogue_FinalPursuit", new Vector3(0f, 1.62f, 0.8f),
                "final_pursuit");
            var finalPursuitGo = finalPursuitDialogue.gameObject;
            finalPursuitGo.transform.SetParent(cockpit, false);
            finalPursuitGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            finalPursuitGo.transform.localRotation = Quaternion.identity;
            var finalPursuitSo = new SerializedObject(finalPursuitDialogue);
            finalPursuitSo.FindProperty("playOnStart").boolValue = false;
            finalPursuitSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(interceptorSo, "spawnDialogue", finalPursuitDialogue);
            interceptorSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator with extraFlag "galaxy1_complete".
            var interceptorGateGo = new GameObject("InterceptorClearedGate");
            var interceptorGate = interceptorGateGo.AddComponent<EncounterClearedActivator>();
            var interceptorGateSo = new SerializedObject(interceptorGate);
            var activateProp = interceptorGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = returnMapBoxGo;
            interceptorGateSo.FindProperty("clearedFlag").stringValue = "ep08_interceptor_cleared";
            // IMPORTANT: set extraFlag = "galaxy1_complete" if the property exists.
            var extraFlagProp = interceptorGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "galaxy1_complete";
            }
            interceptorGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08NebulaEdgeScenePath);
            EnsureScenesInBuild(Galaxy1Ep08NebulaEdgeScenePath);

            Debug.Log($"[Space Samurai] EP08 Nebula Edge scene built at {Galaxy1Ep08NebulaEdgeScenePath}. " +
                      "Nebula fringe (purple-blue tinted fog, accent visuals). Cockpit with canopy + HUD. " +
                      "Flow: covenant_epilogue (auto on start) → final_pursuit (GuardEncounter, 1 ship, 30s delay) → " +
                      "on cleared, EncounterClearedActivator plays nothing + sets extraFlag 'galaxy1_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace, marks galaxy1 complete).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build ALL EP08 Scenes", priority = 105)]
        public static void BuildAllEp08Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP08 scenes in order: Cargo Hold, Apex Vault, Orbit Break, Nebula Edge...");
            BuildEp08CargoHold();
            BuildEp08ApexVault();
            BuildEp08OrbitBreak();
            BuildEp08NebulaEdge();
            Debug.Log("[Space Samurai] All EP08 scenes built successfully!");
        }
    }
}

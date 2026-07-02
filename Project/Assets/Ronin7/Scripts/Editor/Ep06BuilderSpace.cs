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
    /// EP06 "Signal in the Dark" space scene builders. Builds the Approach and Escape phases
    /// of the Frosthold mission where Ronin-7 descends into the ice colony to uncover Khall's
    /// conspiracy and retrieve the failsafe. The Approach scene handles the opening transmission
    /// and Dominion pursuit; the Escape scene handles the finale and return to the galaxy map.
    /// Wires cockpit dialogue, GuardEncounter with spawnDialogue, and scene transitions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep06ApproachScenePath = SceneFolder + "/Galaxy1_EP06_Approach.unity";
        private const string Galaxy1Ep06EscapeScenePath = SceneFolder + "/Galaxy1_EP06_Escape.unity";

        private static readonly string Galaxy1Ep06ApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06ApproachScenePath);
        private static readonly string Galaxy1Ep06EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06EscapeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Approach", priority = 87)]
        public static void BuildEp06Approach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space is a black void: kill fog, drop ambient to a faint cool fill.
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

            // Seated flight rig: stationary at the origin, NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            // Far clip to cover Frosthold planet (icy white-blue sphere at a distance).
            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            // Player ship Health for damage relay.
            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            // Hull collider at origin for damage.
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

            // Starfield dome (shared helper).
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Frosthold planet: icy white-blue sphere distant from the approach.
            var planetGo = AddUnlitVisual(universe, "Frosthold", new Vector3(2000f, -500f, 3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.7f, 0.85f, 1f));
            var planetCollider = planetGo.GetComponent<Collider>();
            if (planetCollider != null) planetCollider.isTrigger = true;

            // Flight controller, wired identically to the Galaxy1 cockpit (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool (player + enemies draw from this one bounded pool).
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns mounted on the cockpit (Galaxy1 pattern: twin muzzles flank the canopy).
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
            // Opening cockpit dialogue: approach_transmission (Kessler's intel on Frosthold).
            var approachDialogue = BuildEp06DialoguePlayer("Dialogue_ApproachTransmission", new Vector3(0f, 1.62f, 0.8f),
                "approach_transmission");
            var approachDpGo = approachDialogue.gameObject;
            approachDpGo.transform.SetParent(cockpit, false);
            approachDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            approachDpGo.transform.localRotation = Quaternion.identity;

            // ---- Kessler co-pilot (if present from prior scenes) ----
            // Kessler is already on the ship from EP05; no need to instantiate again.
            // The cockpit dialogue will be driven by the approach_transmission set.

            // ---- Enemy Encounter: Dominion Pursuit (3 ships) ----
            // Copy EP04/05 pursuit pattern: guardTarget = null, spawn around player.
            var pursuitGo = new GameObject("Dominion Pursuit");
            var pursuit = pursuitGo.AddComponent<GuardEncounter>();
            var pursuitSo = new SerializedObject(pursuit);
            SetObjectRef(pursuitSo, "player", shipCtrl);
            SetObjectRef(pursuitSo, "universe", universe);
            SetObjectRef(pursuitSo, "pool", pool);
            SetObjectRef(pursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            pursuitSo.FindProperty("shipCount").intValue = 3;
            pursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            pursuitSo.FindProperty("initialDelay").floatValue = 6f;
            // Scene is only reachable via the EP05 chain, so the pursuit is always active.
            pursuitSo.FindProperty("requiredCompletedScene").stringValue = "";
            pursuitSo.FindProperty("clearedFlag").stringValue = "ep06_pursuit_cleared";

            // Build spawn dialogue for this encounter
            var orbitalUltimatum = BuildEp06DialoguePlayer("Dialogue_OrbitalUltimatum", new Vector3(0f, 1.62f, 0.8f),
                "orbital_ultimatum");
            var orbitalUltimGo = orbitalUltimatum.gameObject;
            orbitalUltimGo.transform.SetParent(cockpit, false);
            orbitalUltimGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            orbitalUltimGo.transform.localRotation = Quaternion.identity;
            var orbitalDpSo = new SerializedObject(orbitalUltimatum);
            orbitalDpSo.FindProperty("playOnStart").boolValue = false;
            orbitalDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(pursuitSo, "spawnDialogue", orbitalUltimatum);
            pursuitSo.ApplyModifiedPropertiesWithoutUndo();

            // Landing prompt for transition to on-foot medical compound.
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 1;
                // Single landable: Frosthold Medical Compound (the on-foot scene).
                var frostHoldEntry = landables.GetArrayElementAtIndex(0);
                frostHoldEntry.FindPropertyRelative("target").objectReferenceValue = planetGo.transform;
                frostHoldEntry.FindPropertyRelative("approachRadius").floatValue = 140f;
                frostHoldEntry.FindPropertyRelative("destinationScene").stringValue = "Galaxy1_EP06_MedicalCompound";
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06ApproachScenePath);
            EnsureScenesInBuild(Galaxy1Ep06ApproachScenePath);

            Debug.Log($"[Space Samurai] EP06 Approach scene built at {Galaxy1Ep06ApproachScenePath}. " +
                      "Space over Frosthold (icy white-blue planet). " +
                      "Cockpit with canopy + HUD, Kessler co-pilot, 3-ship Dominion pursuit with orbital_ultimatum comms. " +
                      "Landing to Galaxy1_EP06_MedicalCompound. Reached via EP05 CommandHub launch.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Escape", priority = 88)]
        public static void BuildEp06Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: same dark void as Approach.
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
            // No-combat escape scene, so no gun crosshair.
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Frosthold behind the escape.
            var planetGo = AddUnlitVisual(universe, "Frosthold", new Vector3(-2000f, -500f, -3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.7f, 0.85f, 1f));
            var planetCollider = planetGo.GetComponent<Collider>();
            if (planetCollider != null) planetCollider.isTrigger = true;

            // Flight controller (no combat in this scene, so no guns/pool needed).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Player ----
            // Escape jammer dialogue: the failsafe is activated and returns player to galaxy map.
            var escapeDialogue = BuildEp06DialoguePlayer("Dialogue_EscapeJammer", new Vector3(0f, 1.62f, 0.8f),
                "escape_jammer");
            var escapeDpGo = escapeDialogue.gameObject;
            escapeDpGo.transform.SetParent(cockpit, false);
            escapeDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            escapeDpGo.transform.localRotation = Quaternion.identity;

            // Transition box: "CONTINUE TO BLACKVEIL YARDS" wired to LoadOnFootScene (chains to EP07 Approach).
            var returnBoxGo = BuildTransitionBox("ReturnToSpaceBox", new Vector3(0f, 1.2f, 0.8f),
                "CONTINUE TO BLACKVEIL YARDS", out var returnBtn, out var returnTransition);
            var rtSo = new SerializedObject(returnTransition);
            rtSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP07_Approach";
            rtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.LoadOnFootScene));
            returnBoxGo.SetActive(false);

            // Mission Director for the escape sequence.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue escape_jammer (jammer activation, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Escape Jammer";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = escapeDialogue;

            // Step 1: Prompt — return to galaxy map (ReturnToSpace, marks EP06 complete).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Return to Galaxy Map";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06EscapeScenePath);
            EnsureScenesInBuild(Galaxy1Ep06EscapeScenePath);

            Debug.Log($"[Space Samurai] EP06 Escape scene built at {Galaxy1Ep06EscapeScenePath}. " +
                      "Space leaving Frosthold (planet receding). Cockpit with canopy + HUD. " +
                      "2 steps: escape_jammer auto → CONTINUE TO BLACKVEIL YARDS (LoadOnFootScene to Galaxy1_EP07_Approach, chains to EP07).");
        }
    }
}

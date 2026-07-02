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
    /// EP07 "The Technician's Debt" space scene builders. Builds the Approach and Escape phases
    /// of the Blackveil Shipbreaker Yards mission where Cipher retrieves the manifest of sleeping
    /// operatives from Tessa Rin and flees the yards with the Corsair. The Approach scene handles
    /// the arrival dialogue over the industrial graveyard; the Escape scene handles the pursuit by
    /// a Dominion interceptor and the final dialogue before returning to space. Wires cockpit
    /// dialogue, GuardEncounter with interceptor pursuit, and scene transitions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep07ApproachScenePath = SceneFolder + "/Galaxy1_EP07_Approach.unity";
        private const string Galaxy1Ep07EscapeScenePath = SceneFolder + "/Galaxy1_EP07_Escape.unity";

        private static readonly string Galaxy1Ep07ApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07ApproachScenePath);
        private static readonly string Galaxy1Ep07EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07EscapeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Approach", priority = 97)]
        public static void BuildEp07Approach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

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

            // Far clip to cover Blackveil moon and distant sun.
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

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Blackveil moon: dead grey-brown industrial body with scattered debris.
            var blackveilGo = AddUnlitVisual(universe, "Blackveil", new Vector3(2000f, -500f, 3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.35f, 0.3f, 0.25f));
            var blackveilCollider = blackveilGo.GetComponent<Collider>();
            if (blackveilCollider != null) blackveilCollider.isTrigger = true;

            // Debris field: scattered grey hull-plate chunks around Blackveil.
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                float distance = 1200f + i * 200f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * distance, -200f + i * 100f, Mathf.Sin(angle) * distance);
                var debrisGo = AddUnlitVisual(universe, $"Hull Plate {i}", pos,
                    Vector3.one * (150f + i * 50f), PrimitiveType.Cube, new Color(0.4f, 0.38f, 0.35f));
                var debrisCollider = debrisGo.GetComponent<Collider>();
                if (debrisCollider != null) debrisCollider.isTrigger = true;
            }

            // Flight controller, wired identically to the Galaxy1 cockpit (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            // Opening cockpit dialogue: approach_viewport (Cipher's realization over Blackveil).
            var approachDialogue = BuildEp07DialoguePlayer("Dialogue_ApproachViewport", new Vector3(0f, 1.62f, 0.8f),
                "approach_viewport");
            var approachDpGo = approachDialogue.gameObject;
            approachDpGo.transform.SetParent(cockpit, false);
            approachDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            approachDpGo.transform.localRotation = Quaternion.identity;

            // Landing prompt for transition to on-foot market hub.
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
                // Single landable: Blackveil Market Hub (the on-foot scene).
                var blackveilEntry = landables.GetArrayElementAtIndex(0);
                blackveilEntry.FindPropertyRelative("target").objectReferenceValue = blackveilGo.transform;
                blackveilEntry.FindPropertyRelative("approachRadius").floatValue = 140f;
                blackveilEntry.FindPropertyRelative("destinationScene").stringValue = "Galaxy1_EP07_MarketHub";
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Mission Director for the approach sequence.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue approach_viewport (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Approach Viewport";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = approachDialogue;

            // Step 1: Prompt — land on Blackveil (LoadOnFootScene, chains to EP07 MarketHub).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Land on Blackveil";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = prompt.gameObject;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07ApproachScenePath);
            EnsureScenesInBuild(Galaxy1Ep07ApproachScenePath);

            Debug.Log($"[Space Samurai] EP07 Approach scene built at {Galaxy1Ep07ApproachScenePath}. " +
                      "Space over Blackveil (dead industrial moon, grey-brown, debris field). " +
                      "Cockpit with canopy + HUD. Opening dialogue approach_viewport (Cipher's realization). " +
                      "Landing to Galaxy1_EP07_MarketHub. Reached via EP06 Escape chaining.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Escape", priority = 98)]
        public static void BuildEp07Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

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
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Blackveil receding behind the escape.
            var blackveilGo = AddUnlitVisual(universe, "Blackveil", new Vector3(-2000f, -500f, -3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.35f, 0.3f, 0.25f));
            var blackveilCollider = blackveilGo.GetComponent<Collider>();
            if (blackveilCollider != null) blackveilCollider.isTrigger = true;

            // Debris field: scattered grey hull-plate chunks around Blackveil (receding).
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                float distance = 1200f + i * 200f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * distance * -1f, -200f + i * 100f, Mathf.Sin(angle) * distance * -1f);
                var debrisGo = AddUnlitVisual(universe, $"Hull Plate {i}", pos,
                    Vector3.one * (150f + i * 50f), PrimitiveType.Cube, new Color(0.4f, 0.38f, 0.35f));
                var debrisCollider = debrisGo.GetComponent<Collider>();
                if (debrisCollider != null) debrisCollider.isTrigger = true;
            }

            // Flight controller.
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

            // Player guns mounted on the cockpit.
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
            // Manifest transfer dialogue (auto): aboard Corsair, transferring the operatives' manifest.
            var manifestDialogue = BuildEp07DialoguePlayer("Dialogue_ManifestTransfer", new Vector3(0f, 1.62f, 0.8f),
                "manifest_transfer");
            var manifestDpGo = manifestDialogue.gameObject;
            manifestDpGo.transform.SetParent(cockpit, false);
            manifestDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            manifestDpGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Dominion Interceptor Pursuit ----
            // Single hunter-class interceptor (pursuit mode: guardTarget = null, spawn around player).
            var interceptorGo = new GameObject("Dominion Interceptor");
            var interceptor = interceptorGo.AddComponent<GuardEncounter>();
            var interceptorSo = new SerializedObject(interceptor);
            SetObjectRef(interceptorSo, "player", shipCtrl);
            SetObjectRef(interceptorSo, "universe", universe);
            SetObjectRef(interceptorSo, "pool", pool);
            SetObjectRef(interceptorSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            interceptorSo.FindProperty("shipCount").intValue = 1;
            interceptorSo.FindProperty("spawnRadius").floatValue = 260f;
            // Generous delay so the manifest-transfer dialogue can finish before the fight starts.
            interceptorSo.FindProperty("initialDelay").floatValue = 20f;
            // Gated by EP07 completion (never suppresses or requires prior scene).
            interceptorSo.FindProperty("requiredCompletedScene").stringValue = "";
            interceptorSo.FindProperty("clearedFlag").stringValue = "ep07_interceptor_cleared";

            // Build spawn dialogue for this encounter.
            var interceptorPursuitDialogue = BuildEp07DialoguePlayer("Dialogue_InterceptorPursuit", new Vector3(0f, 1.62f, 0.8f),
                "interceptor_pursuit");
            var interceptorPursuitGo = interceptorPursuitDialogue.gameObject;
            interceptorPursuitGo.transform.SetParent(cockpit, false);
            interceptorPursuitGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            interceptorPursuitGo.transform.localRotation = Quaternion.identity;
            var interceptorDpSo = new SerializedObject(interceptorPursuitDialogue);
            interceptorDpSo.FindProperty("playOnStart").boolValue = false;
            interceptorDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(interceptorSo, "spawnDialogue", interceptorPursuitDialogue);
            interceptorSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-encounter dialogue: compassion anomalies (Tessa's revelation about prior hesitations).
            // Played by the EncounterClearedActivator once the interceptor is destroyed, not on start.
            var compassionDialogue = BuildEp07DialoguePlayer("Dialogue_CompassionAnomalies", new Vector3(0f, 1.62f, 0.8f),
                "compassion_anomalies");
            var compassionDpGo = compassionDialogue.gameObject;
            compassionDpGo.transform.SetParent(cockpit, false);
            compassionDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            compassionDpGo.transform.localRotation = Quaternion.identity;
            var compassionDpSo = new SerializedObject(compassionDialogue);
            compassionDpSo.FindProperty("playOnStart").boolValue = false;
            compassionDpSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "CONTINUE — EPISODE 8" wired to LoadOnFootScene (chains to EP08 CargoHold).
            // Hidden until the interceptor encounter is cleared.
            var returnBoxGo = BuildTransitionBox("ContinueToEp08Box", new Vector3(0f, 1.2f, 0.8f),
                "CONTINUE — EPISODE 8", out var returnBtn, out var returnTransition);
            var rtSo = new SerializedObject(returnTransition);
            rtSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP08_CargoHold";
            rtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.LoadOnFootScene));
            returnBoxGo.SetActive(false);

            // Gate the ending on the fight: when the GuardEncounter publishes SpaceEncounterCleared,
            // play the compassion_anomalies dialogue and reveal the return prompt. No MissionDirector
            // here — its completion would publish ZoneCompleted and end the scene early.
            var clearedGateGo = new GameObject("EncounterClearedGate");
            var clearedGate = clearedGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(clearedGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("playOnCleared").objectReferenceValue = compassionDialogue;
            gateSo.FindProperty("clearedFlag").stringValue = "ep07_interceptor_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07EscapeScenePath);
            EnsureScenesInBuild(Galaxy1Ep07EscapeScenePath);

            Debug.Log($"[Space Samurai] EP07 Escape scene built at {Galaxy1Ep07EscapeScenePath}. " +
                      "Space leaving Blackveil (industrial moon receding, debris field). Cockpit with canopy + HUD. " +
                      "Flow: manifest_transfer auto → interceptor pursuit (GuardEncounter, 20s delay) → on cleared, " +
                      "EncounterClearedActivator plays compassion_anomalies + reveals CONTINUE TO APEX VAULT " +
                      "(LoadOnFootScene to Galaxy1_EP08_CargoHold, chains to EP08).");
        }
    }
}

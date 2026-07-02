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
    /// EP33 "The Tenfold Pact" (Galaxy 4) builder for the two space-combat cockpit scenes (scenes 3 and 7).
    /// The assault on the Obsidian Synod fortress and the convoy escape through privateer blockade.
    /// - The Assault: SPACE dogfight breaching the Synod's shield lattice; 5-ship interceptor encounter.
    /// - The Exodus: SPACE combat defending the evacuation convoy; 4-ship privateer blockade.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and scene constants
    /// (Galaxy4Ep33*ScenePath/Name, BuildEp33DialoguePlayer) are declared in Ep33Builder.cs.
    /// DO NOT redefine them here.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Assault", priority = 335)]
        public static void BuildEp33TheAssault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over Obsidian Synod fortress (near-black iron sphere below).
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

            // Obsidian Synod fortress below: near-black iron sphere.
            var fortressGo = AddUnlitVisual(universe, "Obsidian Synod", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.10f, 0.10f, 0.12f));
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
            // assault_approach: orbital approach to Obsidian Synod (plays on start, cockpit-parented).
            var assaultApproachDialogue = BuildEp33DialoguePlayer("Dialogue_AssaultApproach", new Vector3(0f, 1.62f, 0.8f), "assault_approach");
            var aaGo = assaultApproachDialogue.gameObject;
            aaGo.transform.SetParent(cockpit, false);
            aaGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            aaGo.transform.localRotation = Quaternion.identity;
            var aaSo = new SerializedObject(assaultApproachDialogue);
            aaSo.FindProperty("playOnStart").boolValue = true;
            aaSo.ApplyModifiedPropertiesWithoutUndo();

            // assault_cleared: revealed on encounter clear.
            var assaultClearedDialogue = BuildEp33DialoguePlayer("Dialogue_AssaultCleared", new Vector3(0f, 1.62f, 0.8f), "assault_cleared");
            var acGo = assaultClearedDialogue.gameObject;
            acGo.transform.SetParent(cockpit, false);
            acGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            acGo.transform.localRotation = Quaternion.identity;
            var acSo = new SerializedObject(assaultClearedDialogue);
            acSo.FindProperty("playOnStart").boolValue = true;
            acSo.ApplyModifiedPropertiesWithoutUndo();
            acGo.SetActive(false);

            // ---- Enemy Encounter: 5-ship assault swarm ----
            var assaultEncounterGo = new GameObject("AssaultEncounter");
            var assaultEncounter = assaultEncounterGo.AddComponent<GuardEncounter>();
            var aeSo = new SerializedObject(assaultEncounter);
            SetObjectRef(aeSo, "player", shipCtrl);
            SetObjectRef(aeSo, "universe", universe);
            SetObjectRef(aeSo, "pool", pool);
            SetObjectRef(aeSo, "definition", enemyShipDef);
            aeSo.FindProperty("shipCount").intValue = 5;
            aeSo.FindProperty("spawnRadius").floatValue = 280f;
            aeSo.FindProperty("initialDelay").floatValue = 6f;
            aeSo.FindProperty("requiredCompletedScene").stringValue = "";
            aeSo.FindProperty("clearedFlag").stringValue = "ep33_assault_cleared";
            var assaultDogfightDialogue = BuildEp33DialoguePlayer("Dialogue_AssaultDogfight", new Vector3(0f, 1.62f, 0.8f), "assault_dogfight");
            SetObjectRef(aeSo, "spawnDialogue", assaultDogfightDialogue);
            aeSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals assault_cleared dialogue + transition box.
            var crecheBoxGo = BuildTransitionBox("ToCrecheBox", new Vector3(0f, 1.2f, 0.8f), "BREACH THE VOID-DOCK",
                out var crecheBtn, out var crecheTransition);
            var cbSo = new SerializedObject(crecheTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33CrecheHallsSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(crecheBtn.onClick,
                new UnityEngine.Events.UnityAction(crecheTransition.LoadOnFootScene));
            crecheBoxGo.SetActive(false);

            var assaultGateGo = new GameObject("AssaultClearedGate");
            var assaultGate = assaultGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(assaultGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = acGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = crecheBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep33_assault_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy4Ep33TheAssaultScenePath);
            EnsureScenesInBuild(Galaxy4Ep33TheAssaultScenePath, SceneFolder + "/Galaxy4_EP33_CrecheHalls.unity");

            Debug.Log($"[Space Samurai] EP33 The Assault scene built at {Galaxy4Ep33TheAssaultScenePath}. " +
                      "Obsidian Synod fortress assault (near-black iron sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: assault_approach (auto, orbital approach + shield breach) → 5-ship interceptor encounter (ep33_assault_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals assault_cleared (command acknowledge) + BREACH THE VOID-DOCK transition box. " +
                      "GALAXY 4 EP33 SCENE 3 (space combat finale assault).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Exodus", priority = 340)]
        public static void BuildEp33TheExodus()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over dying star (dim red-giant sphere below).
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

            // Dying star below: dim red-giant sphere.
            var starGo = AddUnlitVisual(universe, "Dying Star", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.30f, 0.10f, 0.08f));
            var starCollider = starGo.GetComponent<Collider>();
            if (starCollider != null) starCollider.isTrigger = true;

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
            // exodus_escape: evacuation escape sequence (plays on start, cockpit-parented).
            var exodusEscapeDialogue = BuildEp33DialoguePlayer("Dialogue_ExodusEscape", new Vector3(0f, 1.62f, 0.8f), "exodus_escape");
            var eeGo = exodusEscapeDialogue.gameObject;
            eeGo.transform.SetParent(cockpit, false);
            eeGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            eeGo.transform.localRotation = Quaternion.identity;
            var eeSo = new SerializedObject(exodusEscapeDialogue);
            eeSo.FindProperty("playOnStart").boolValue = true;
            eeSo.ApplyModifiedPropertiesWithoutUndo();

            // convoy_after: revealed on encounter clear.
            var convoyAfterDialogue = BuildEp33DialoguePlayer("Dialogue_ConvoyAfter", new Vector3(0f, 1.62f, 0.8f), "convoy_after");
            var caGo = convoyAfterDialogue.gameObject;
            caGo.transform.SetParent(cockpit, false);
            caGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            caGo.transform.localRotation = Quaternion.identity;
            var caSo = new SerializedObject(convoyAfterDialogue);
            caSo.FindProperty("playOnStart").boolValue = true;
            caSo.ApplyModifiedPropertiesWithoutUndo();
            caGo.SetActive(false);

            // ---- Enemy Encounter: 4-ship privateer blockade ----
            var convoyEncounterGo = new GameObject("ConvoyEncounter");
            var convoyEncounter = convoyEncounterGo.AddComponent<GuardEncounter>();
            var ceSo = new SerializedObject(convoyEncounter);
            SetObjectRef(ceSo, "player", shipCtrl);
            SetObjectRef(ceSo, "universe", universe);
            SetObjectRef(ceSo, "pool", pool);
            SetObjectRef(ceSo, "definition", enemyShipDef);
            ceSo.FindProperty("shipCount").intValue = 4;
            ceSo.FindProperty("spawnRadius").floatValue = 280f;
            ceSo.FindProperty("initialDelay").floatValue = 6f;
            ceSo.FindProperty("requiredCompletedScene").stringValue = "";
            ceSo.FindProperty("clearedFlag").stringValue = "ep33_exodus_cleared";
            var convoyDefenseDialogue = BuildEp33DialoguePlayer("Dialogue_ConvoyDefense", new Vector3(0f, 1.62f, 0.8f), "convoy_defense");
            SetObjectRef(ceSo, "spawnDialogue", convoyDefenseDialogue);
            ceSo.ApplyModifiedPropertiesWithoutUndo();

            // On clear → EncounterClearedActivator reveals convoy_after dialogue + transition box.
            var pactBoxGo = BuildTransitionBox("ToPactBox", new Vector3(0f, 1.2f, 0.8f), "TO THE LANTERN",
                out var pactBtn, out var pactTransition);
            var pbSo = new SerializedObject(pactTransition);
            pbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheTenfoldPactSceneName;
            pbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pactBtn.onClick,
                new UnityEngine.Events.UnityAction(pactTransition.LoadOnFootScene));
            pactBoxGo.SetActive(false);

            var exodusGateGo = new GameObject("ExodusClearedGate");
            var exodusGate = exodusGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(exodusGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = caGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = pactBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep33_exodus_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy4Ep33TheExodusScenePath);
            EnsureScenesInBuild(Galaxy4Ep33TheExodusScenePath, SceneFolder + "/Galaxy4_EP33_TheTenfoldPact.unity");

            Debug.Log($"[Space Samurai] EP33 The Exodus scene built at {Galaxy4Ep33TheExodusScenePath}. " +
                      "Evacuation convoy escape (dim red-giant dying star below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: exodus_escape (auto, evacuation sequence) → 4-ship privateer encounter (ep33_exodus_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals convoy_after (resistance acknowledgment) + TO THE LANTERN transition box. " +
                      "GALAXY 4 EP33 SCENE 7 (space combat finale, convoy defense).");
        }
    }
}

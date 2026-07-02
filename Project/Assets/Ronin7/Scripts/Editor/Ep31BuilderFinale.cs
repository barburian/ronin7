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
    /// EP31 "One Day" (Galaxy 4) builder for the space-flight denouement finale.
    /// No combat. Sunrise/desert-dawn aesthetic on Chronus Prime as Soren reflects.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and scene constants
    /// (Galaxy4Ep31OneDayScenePath, Galaxy4Ep31OneDaySceneName, BuildEp31DialoguePlayer)
    /// are declared in Ep31Builder.cs. DO NOT redefine them here.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP31 One Day", priority = 322)]
        public static void BuildEp31OneDay()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: sunrise/desert-dawn aesthetic (warm ochre sphere below).
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

            // Chronus Prime Dawn below: warm dawn ochre aesthetic.
            var dawnGo = AddUnlitVisual(universe, "Chronus Prime Dawn", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.62f, 0.45f, 0.30f));
            var dawnCollider = dawnGo.GetComponent<Collider>();
            if (dawnCollider != null) dawnCollider.isTrigger = true;

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
            // one_day: Soren's final reflection (plays on start, cockpit-parented).
            var oneDayDialogue = BuildEp31DialoguePlayer("Dialogue_OneDay", new Vector3(0f, 1.62f, 0.8f), "one_day");
            var odGo = oneDayDialogue.gameObject;
            odGo.transform.SetParent(cockpit, false);
            odGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            odGo.transform.localRotation = Quaternion.identity;
            var odSo = new SerializedObject(oneDayDialogue);
            odSo.FindProperty("playOnStart").boolValue = true;
            odSo.ApplyModifiedPropertiesWithoutUndo();

            // NO GuardEncounter, NO combat.

            // Finale return box: "RETURN — TO THE STARS" (ACTIVE from start).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(true); // Active from start, no combat gate.

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep31_complete";
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

            // Save the scene (LAST scene of EP31: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep31OneDayScenePath);
            EnsureScenesInBuild(Galaxy4Ep31OneDayScenePath);

            Debug.Log($"[Space Samurai] EP31 One Day scene built at {Galaxy4Ep31OneDayScenePath}. " +
                      "SPACE denouement (no combat). Chronus Prime Dawn sunrise aesthetic (warm ochre sphere below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: one_day (auto, Soren's final reflection). " +
                      "No encounter. RETURN — TO THE STARS button (ACTIVE from start) wired to CampaignFlagSetter (ep31_complete) + ReturnToSpace. " +
                      "GALAXY 4 EP31 FINALE (last scene, space denouement).");
        }
    }
}

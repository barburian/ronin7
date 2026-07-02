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
    /// EP24 "The Fracture Protocol" (Galaxy 3 FINALE) builders for the final three scenes.
    /// - Khall's Mercy: crumbling streets, the cascade peak (HiveCascadeController), Khall's private channel + EMP plan
    /// - Shattered Protocol: override chamber, Seven-Prime ALLY, Khall's flawless Dominion strike unit (NO cascade — the contrast), EMP fired
    /// - Light, Separate: SPACE dogfight (GuardEncounter intercept) in orbit; sets galaxy3_complete + ep24_complete
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers (BuildEp24DialoguePlayer,
    /// BuildEp24OnFootShell, BuildEp24Npc, BuildEp24CloneWave, FinishEp24Scene, Ep24CloneTint) are declared in Ep24Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 Khalls Mercy", priority = 263)]
        public static void BuildEp24KhallsMercy()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Khall's Mercy: crumbling streets at cascade peak, danger amber + collapse red.
            var playerHealth = BuildEp24OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.50f, 0.42f),
                ambient: new Color(0.16f, 0.12f, 0.10f),
                fogColor: new Color(0.26f, 0.18f, 0.14f), fogDensity: 0.019f,
                structureName: "CrumblingStreets",
                accent1: new Color(1f, 0.72f, 0.42f),      // failing-grid amber
                accent2: new Color(1f, 0.40f, 0.34f),      // collapse red
                floorLight: new Color(0.48f, 0.44f, 0.42f), floorDark: new Color(0.28f, 0.25f, 0.23f),
                propTint: new Color(0.50f, 0.47f, 0.45f), out _);

            // ---- Seven-Prime (defiant at the beacon; no ally here) ----
            BuildEp24Npc("Seven-Prime", new Vector3(-1.2f, 0f, 3f), new Color(0.55f, 0.5f, 0.62f));

            // ---- Cascade peak: grief given bodies (HiveCascade) ----
            var clonePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
                new Vector3(0f, 0f, 13f),
            };
            var cloneHealths = BuildEp24CloneWave(clonePositions, playerHealth, enemyDef, Ep24CloneTint, trained: false);

            var cloneSpawner = BuildWaveSpawner("CloneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { cloneHealths },
                new[] { BuildEp24DialoguePlayer("Dialogue_BroadcastBarks", new Vector3(0f, 1.5f, 8f), "broadcast_barks") });

            // ---- Dialogue Players ----
            var mercyDialogue = BuildEp24DialoguePlayer("Dialogue_KhallsMercy", new Vector3(0f, 1.5f, 2f), "khalls_mercy");
            var kmSo = new SerializedObject(mercyDialogue);
            kmSo.FindProperty("playOnStart").boolValue = true;
            kmSo.ApplyModifiedPropertiesWithoutUndo();

            var empPlanDialogue = BuildEp24DialoguePlayer("Dialogue_EmpPlan", new Vector3(0f, 1.5f, 14f), "emp_plan");

            // Transition box: "TO THE OVERRIDE CHAMBER".
            var overrideBoxGo = BuildTransitionBox("ToOverrideBox", new Vector3(0f, 1.2f, 21.5f), "TO THE OVERRIDE CHAMBER",
                out var overrideBtn, out var overrideTransition);
            var obSo = new SerializedObject(overrideTransition);
            obSo.FindProperty("onFootScene").stringValue = Galaxy3Ep24ShatteredProtocolSceneName;
            obSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(overrideBtn.onClick,
                new UnityEngine.Events.UnityAction(overrideTransition.LoadOnFootScene));
            overrideBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall's Mercy";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = mercyDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Cascade Peak Clones (6)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cloneSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: EMP Plan";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = empPlanDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Override Chamber";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = overrideBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp24Scene(scene, Galaxy3Ep24KhallsMercyScenePath, Galaxy3Ep24ShatteredProtocolScenePath);

            Debug.Log($"[Space Samurai] EP24 Khall's Mercy scene built at {Galaxy3Ep24KhallsMercyScenePath}. " +
                      "Crumbling streets at cascade peak, amber/red collapse. Seven-Prime (defiant, no ally). " +
                      "6 clones under HiveCascadeController. " +
                      "4 steps: khalls_mercy (auto, Khall neural channel) → defeat 6 (broadcast_barks) → emp_plan dialogue → to the override chamber.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 Shattered Protocol", priority = 264)]
        public static void BuildEp24ShatteredProtocol()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Shattered Protocol: planetary override chamber, cold control-room blue + activation cyan.
            var playerHealth = BuildEp24OnFootShell(refs, weapon,
                keyLight: new Color(0.55f, 0.62f, 0.72f),
                ambient: new Color(0.13f, 0.15f, 0.18f),
                fogColor: new Color(0.18f, 0.22f, 0.28f), fogDensity: 0.016f,
                structureName: "OverrideChamber",
                accent1: new Color(0.5f, 0.85f, 1f),       // activation-panel cyan
                accent2: new Color(0.7f, 0.78f, 0.88f),    // cold control-room white
                floorLight: new Color(0.46f, 0.50f, 0.56f), floorDark: new Color(0.26f, 0.30f, 0.36f),
                propTint: new Color(0.48f, 0.54f, 0.62f), out _);

            // ---- Seven-Prime ALLY (fights beside the player) ----
            var sevenGo = BuildEp24Npc("Seven-Prime", new Vector3(-1.2f, 0f, 3f), new Color(0.55f, 0.5f, 0.62f));
            sevenGo.AddComponent<Ronin7.Enemies.AllyCombatant>();

            // ---- Khall's elite Dominion strike unit: flawless protocol, NO HiveCascade (the contrast) ----
            var operativeTint = new Color(0.38f, 0.40f, 0.44f); // standard Dominion grey
            var operativePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
            };
            var operativeHealths = new List<Health>();
            foreach (var pos in operativePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, operativeTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                operativeHealths.Add(enemy.GetComponent<Health>());
            }

            var strikeSpawner = BuildWaveSpawner("StrikeSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { operativeHealths },
                new[] { BuildEp24DialoguePlayer("Dialogue_StrikeBarks", new Vector3(0f, 1.5f, 8f), "strike_barks") });

            // ---- Dialogue Players ----
            var protocolDialogue = BuildEp24DialoguePlayer("Dialogue_ShatteredProtocol", new Vector3(0f, 1.5f, 2f), "shattered_protocol");
            var spSo = new SerializedObject(protocolDialogue);
            spSo.FindProperty("playOnStart").boolValue = true;
            spSo.ApplyModifiedPropertiesWithoutUndo();

            var empAftermathDialogue = BuildEp24DialoguePlayer("Dialogue_EmpAftermath", new Vector3(0f, 1.5f, 14f), "emp_aftermath");

            // Transition box: "TO THE SHIP" (chains to the space coda).
            var shipBoxGo = BuildTransitionBox("ToShipBox", new Vector3(0f, 1.2f, 21.5f), "TO THE SHIP",
                out var shipBtn, out var shipTransition);
            var sbSo = new SerializedObject(shipTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep24LightSeparateSceneName;
            sbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(shipBtn.onClick,
                new UnityEngine.Events.UnityAction(shipTransition.LoadOnFootScene));
            shipBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Shattered Protocol";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = protocolDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Strike Unit (5, no cascade)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = strikeSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: EMP Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = empAftermathDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Ship";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = shipBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp24Scene(scene, Galaxy3Ep24ShatteredProtocolScenePath, Galaxy3Ep24LightSeparateScenePath);

            Debug.Log($"[Space Samurai] EP24 Shattered Protocol scene built at {Galaxy3Ep24ShatteredProtocolScenePath}. " +
                      "Override chamber, control-room blue + activation cyan. Seven-Prime ALLY (fights beside player). " +
                      "5 Dominion elite operatives (grey, nonLethal, NO HiveCascade — the contrast). " +
                      "4 steps: shattered_protocol (auto, Cipher's choice) → defeat 5 elite (strike_barks) → emp_aftermath dialogue → to the ship.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP24 Light Separate", priority = 265)]
        public static void BuildEp24LightSeparate()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void over Meridian-7 lighting up with fifty thousand separate sparks.
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

            // Meridian-7 below, its night side sparking with fifty thousand individual lights.
            var planetGo = AddUnlitVisual(universe, "Meridian-7", new Vector3(-1200f, -700f, -2600f),
                Vector3.one * 800f, PrimitiveType.Sphere, new Color(0.45f, 0.5f, 0.55f));
            var planetCollider = planetGo.GetComponent<Collider>();
            if (planetCollider != null) planetCollider.isTrigger = true;

            // A drifting debris cluster for parallax.
            var debris = AddUnlitVisual(universe, "Orbital Debris", new Vector3(2200f, 300f, -2700f),
                new Vector3(260f, 130f, 340f), PrimitiveType.Cube, new Color(0.40f, 0.42f, 0.46f));
            var debrisCollider = debris.GetComponent<Collider>();
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
            // light_separate: orbit reflection, plays on start.
            var lightSeparateDialogue = BuildEp24DialoguePlayer("Dialogue_LightSeparate", new Vector3(0f, 1.62f, 0.8f), "light_separate");
            var lsGo = lightSeparateDialogue.gameObject;
            lsGo.transform.SetParent(cockpit, false);
            lsGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            lsGo.transform.localRotation = Quaternion.identity;
            var lsSo = new SerializedObject(lightSeparateDialogue);
            lsSo.FindProperty("playOnStart").boolValue = true;
            lsSo.ApplyModifiedPropertiesWithoutUndo();

            // intercept_barks: GuardEncounter spawn dialogue.
            var interceptBarksDialogue = BuildEp24DialoguePlayer("Dialogue_InterceptBarks", new Vector3(0f, 1.62f, 0.8f), "intercept_barks");
            var ibGo = interceptBarksDialogue.gameObject;
            ibGo.transform.SetParent(cockpit, false);
            ibGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ibGo.transform.localRotation = Quaternion.identity;

            // closing: the leash hook, revealed (and auto-played) when the intercept is cleared.
            var closingDialogue = BuildEp24DialoguePlayer("Dialogue_Closing", new Vector3(0f, 1.62f, 0.8f), "closing");
            var closingGo = closingDialogue.gameObject;
            closingGo.transform.SetParent(cockpit, false);
            closingGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            closingGo.transform.localRotation = Quaternion.identity;
            var closingSo = new SerializedObject(closingDialogue);
            closingSo.FindProperty("playOnStart").boolValue = true; // fires on Start when activated (GO starts inactive)
            closingSo.ApplyModifiedPropertiesWithoutUndo();
            closingGo.SetActive(false);

            // ---- Enemy Encounter: Dominion intercept vessel (3 ships) ----
            var interceptEncounterGo = new GameObject("InterceptEncounter");
            var interceptEncounter = interceptEncounterGo.AddComponent<GuardEncounter>();
            var ieSo = new SerializedObject(interceptEncounter);
            SetObjectRef(ieSo, "player", shipCtrl);
            SetObjectRef(ieSo, "universe", universe);
            SetObjectRef(ieSo, "pool", pool);
            SetObjectRef(ieSo, "definition", enemyShipDef);
            ieSo.FindProperty("shipCount").intValue = 3;
            ieSo.FindProperty("spawnRadius").floatValue = 280f;
            ieSo.FindProperty("initialDelay").floatValue = 6f;
            ieSo.FindProperty("requiredCompletedScene").stringValue = "";
            ieSo.FindProperty("clearedFlag").stringValue = "ep24_intercept_cleared";
            SetObjectRef(ieSo, "spawnDialogue", interceptBarksDialogue);
            ieSo.ApplyModifiedPropertiesWithoutUndo();

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (galaxy3_complete + ep24_complete).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "galaxy3_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "ep24_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Gate the ending on the intercept fight: EncounterClearedActivator reveals closing + the return box.
            var clearedGateGo = new GameObject("InterceptClearedGate");
            var clearedGate = clearedGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(clearedGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = closingGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep24_intercept_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (FINALE — returns to the Galaxy 3 hub).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep24LightSeparateScenePath);
            EnsureScenesInBuild(Galaxy3Ep24LightSeparateScenePath);

            Debug.Log($"[Space Samurai] EP24 Light Separate scene built at {Galaxy3Ep24LightSeparateScenePath}. " +
                      "SPACE coda over Meridian-7. Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: light_separate (auto, orbit reflection) → intercept vessel (GuardEncounter, 3 ships, 6s) → on cleared, " +
                      "EncounterClearedActivator reveals closing dialogue + RETURN — TO THE STARS (CampaignFlagSetter sets " +
                      "galaxy3_complete + ep24_complete, then ReturnToSpace). GALAXY 3 FINALE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP24 Scenes", priority = 266)]
        public static void BuildAllEp24Scenes()
        {
            BuildEp24HowMany();
            BuildEp24RetirementColony();
            BuildEp24ThirtyYears();
            BuildEp24KhallsMercy();
            BuildEp24ShatteredProtocol();
            BuildEp24LightSeparate();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP24 scenes built + inputs rewired. GALAXY 3 FINALE complete.");
        }
    }
}

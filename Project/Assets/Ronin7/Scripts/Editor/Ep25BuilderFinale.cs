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
    /// EP25 "The Sterile Reckoning" (Galaxy 4 launch) builders for the final three scenes.
    /// - Vault: cryo vault with SurgicalDefenseArray blade hazards; the frozen child + sourcing records
    /// - Khall Wire: collapsing medical corridor under Dominion siege; Harrow's betrayal triggers self-destruct
    /// - Purpose Untold: SPACE dogfight (GuardEncounter intercept) in orbit over Sable Drift; sets ep25_complete + heris_recruited
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers (BuildEp25DialoguePlayer,
    /// BuildEp25OnFootShell, BuildEp25Npc, FinishEp25Scene) are declared in Ep25Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Vault", priority = 273)]
        public static void BuildEp25Vault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault: cryo vault, deep cold blue, mist.
            var playerHealth = BuildEp25OnFootShell(refs, weapon,
                keyLight: new Color(0.45f, 0.55f, 0.78f),
                ambient: new Color(0.10f, 0.12f, 0.20f),
                fogColor: new Color(0.14f, 0.16f, 0.28f), fogDensity: 0.022f,
                structureName: "CryoVault",
                accent1: new Color(0.45f, 0.85f, 1f),      // cryo cyan
                accent2: new Color(0.55f, 0.65f, 0.95f),   // frost blue
                floorLight: new Color(0.38f, 0.44f, 0.58f), floorDark: new Color(0.20f, 0.26f, 0.38f),
                propTint: new Color(0.42f, 0.50f, 0.68f), out _);

            // ---- Dr. Heris ----
            BuildEp25Npc("Dr. Heris", new Vector3(-1.2f, 0f, 3f), new Color(0.7f, 0.6f, 0.8f));

            // ---- Surgical Defense Arrays (blade hazards lining the corridor) ----
            var bladeTint = new Color(0.85f, 0.9f, 0.95f);
            var bladePositions = new Vector3[] { new Vector3(-1.5f, 0.5f, 7f), new Vector3(1.5f, 0.5f, 10f),
                                                   new Vector3(-1.5f, 0.5f, 13f), new Vector3(1.5f, 0.5f, 16f) };
            for (int i = 0; i < bladePositions.Length; i++)
            {
                var bladeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bladeGo.name = "BladeArray_" + i;
                bladeGo.transform.position = bladePositions[i];
                bladeGo.transform.localScale = new Vector3(2.4f, 0.2f, 0.2f);
                TintShared(bladeGo.GetComponent<Renderer>(), bladeTint);
                var bladeCollider = bladeGo.GetComponent<BoxCollider>();
                if (bladeCollider != null) bladeCollider.isTrigger = true;
                bladeGo.AddComponent<SurgicalDefenseArray>();
            }

            // ---- Dialogue Players ----
            var vaultDialogue = BuildEp25DialoguePlayer("Dialogue_Vault", new Vector3(0f, 1.5f, 2f), "vault");
            var vSo = new SerializedObject(vaultDialogue);
            vSo.FindProperty("playOnStart").boolValue = true;
            vSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "OPEN THE POD — TO THE CORRIDOR".
            var corridorBoxGo = BuildTransitionBox("ToCorridorBox", new Vector3(0f, 1.2f, 21.5f), "OPEN THE POD — TO THE CORRIDOR",
                out var corridorBtn, out var corridorTransition);
            var cbSo = new SerializedObject(corridorTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep25KhallWireSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(corridorBtn.onClick,
                new UnityEngine.Events.UnityAction(corridorTransition.LoadOnFootScene));
            corridorBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Open the Pod — To the Corridor";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = corridorBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp25Scene(scene, Galaxy4Ep25VaultScenePath, Galaxy4Ep25KhallWireScenePath);

            Debug.Log($"[Space Samurai] EP25 Vault scene built at {Galaxy4Ep25VaultScenePath}. " +
                      "Cryo vault, deep cold blue mist. Dr. Heris. " +
                      "4 SurgicalDefenseArray blade hazards lining the corridor (timing challenge). " +
                      "2 steps: vault (auto, frozen child + sourcing records) → to the corridor.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Khall Wire", priority = 274)]
        public static void BuildEp25KhallWire()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Khall Wire: collapsing medical corridor, emergency red + failing white.
            var playerHealth = BuildEp25OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.50f, 0.45f),
                ambient: new Color(0.16f, 0.12f, 0.11f),
                fogColor: new Color(0.26f, 0.18f, 0.16f), fogDensity: 0.019f,
                structureName: "MedCorridor",
                accent1: new Color(1f, 0.72f, 0.42f),      // failing amber
                accent2: new Color(1f, 0.40f, 0.34f),      // emergency red
                floorLight: new Color(0.48f, 0.44f, 0.42f), floorDark: new Color(0.28f, 0.25f, 0.23f),
                propTint: new Color(0.50f, 0.47f, 0.45f), out _);

            // ---- Gilded Maw enforcers (lethal, Harrow's last stand) ----
            var mercTint = new Color(0.80f, 0.68f, 0.35f);
            var enforcerPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
            };
            var enforcerHealths = new List<Health>();
            foreach (var pos in enforcerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, mercTint);
                enemy.gameObject.SetActive(false);
                enforcerHealths.Add(enemy.GetComponent<Health>());
            }

            var enforcerSpawner = BuildEp03WaveSpawner("EnforcerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { enforcerHealths },
                new[] { BuildEp25DialoguePlayer("Dialogue_CorridorBarks", new Vector3(0f, 1.5f, 8f), "corridor_barks") });

            // ---- Dialogue Players ----
            var khallWireDialogue = BuildEp25DialoguePlayer("Dialogue_KhallWire", new Vector3(0f, 1.5f, 2f), "khall_wire");
            var kwSo = new SerializedObject(khallWireDialogue);
            kwSo.FindProperty("playOnStart").boolValue = true;
            kwSo.ApplyModifiedPropertiesWithoutUndo();

            var selfDestructDialogue = BuildEp25DialoguePlayer("Dialogue_SelfDestruct", new Vector3(0f, 1.5f, 14f), "self_destruct");

            // Transition box: "TO THE SHIP".
            var shipBoxGo = BuildTransitionBox("ToShipBox", new Vector3(0f, 1.2f, 21.5f), "TO THE SHIP",
                out var shipBtn, out var shipTransition);
            var sbSo = new SerializedObject(shipTransition);
            sbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep25PurposeUntoldSceneName;
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
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Wire";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallWireDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Gilded Maw Enforcers (5, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Self Destruct";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = selfDestructDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Ship";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = shipBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp25Scene(scene, Galaxy4Ep25KhallWireScenePath, Galaxy4Ep25PurposeUntoldScenePath);

            Debug.Log($"[Space Samurai] EP25 Khall Wire scene built at {Galaxy4Ep25KhallWireScenePath}. " +
                      "Collapsing medical corridor, failing amber + emergency red. " +
                      "5 Gilded Maw enforcers (gold-chrome, lethal, NO EchoHunter). " +
                      "4 steps: khall_wire (auto, Khall neural countdown) → defeat 5 enforcers (corridor_barks) → self_destruct (Harrow betrayal) → to the ship.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Purpose Untold", priority = 275)]
        public static void BuildEp25PurposeUntold()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void over Sable Drift lighting up with orbital debris.
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

            // Sable Drift below, barren and grey with orbital debris.
            var planetGo = AddUnlitVisual(universe, "Sable Drift", new Vector3(-1200f, -700f, -2600f),
                Vector3.one * 800f, PrimitiveType.Sphere, new Color(0.30f, 0.30f, 0.34f));
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
            // purpose_untold: auto-orbit reflection, plays on start.
            var purposeDialogue = BuildEp25DialoguePlayer("Dialogue_PurposeUntold", new Vector3(0f, 1.62f, 0.8f), "purpose_untold");
            var puGo = purposeDialogue.gameObject;
            puGo.transform.SetParent(cockpit, false);
            puGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            puGo.transform.localRotation = Quaternion.identity;
            var puSo = new SerializedObject(purposeDialogue);
            puSo.FindProperty("playOnStart").boolValue = true;
            puSo.ApplyModifiedPropertiesWithoutUndo();

            // intercept_barks: GuardEncounter spawn dialogue.
            var interceptBarksDialogue = BuildEp25DialoguePlayer("Dialogue_InterceptBarks", new Vector3(0f, 1.62f, 0.8f), "intercept_barks");
            var ibGo = interceptBarksDialogue.gameObject;
            ibGo.transform.SetParent(cockpit, false);
            ibGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ibGo.transform.localRotation = Quaternion.identity;

            // the_heading: the final path forward, revealed (and auto-played) when the intercept is cleared.
            var headingDialogue = BuildEp25DialoguePlayer("Dialogue_TheHeading", new Vector3(0f, 1.62f, 0.8f), "the_heading");
            var headingGo = headingDialogue.gameObject;
            headingGo.transform.SetParent(cockpit, false);
            headingGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            headingGo.transform.localRotation = Quaternion.identity;
            var headingSo = new SerializedObject(headingDialogue);
            headingSo.FindProperty("playOnStart").boolValue = true; // fires on Start when activated (GO starts inactive)
            headingSo.ApplyModifiedPropertiesWithoutUndo();
            headingGo.SetActive(false);

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
            ieSo.FindProperty("clearedFlag").stringValue = "ep25_intercept_cleared";
            SetObjectRef(ieSo, "spawnDialogue", interceptBarksDialogue);
            ieSo.ApplyModifiedPropertiesWithoutUndo();

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (ep25_complete + heris_recruited).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep25_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "heris_recruited";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Gate the ending on the intercept fight: EncounterClearedActivator reveals the_heading + the return box.
            var clearedGateGo = new GameObject("InterceptClearedGate");
            var clearedGate = clearedGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(clearedGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = headingGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep25_intercept_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (FINALE — returns to the Galaxy 4 hub).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep25PurposeUntoldScenePath);
            EnsureScenesInBuild(Galaxy4Ep25PurposeUntoldScenePath);

            Debug.Log($"[Space Samurai] EP25 Purpose Untold scene built at {Galaxy4Ep25PurposeUntoldScenePath}. " +
                      "SPACE coda over Sable Drift. Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: purpose_untold (auto, escape + hidden design reveal) → intercept vessel (GuardEncounter, 3 ships, 6s) → on cleared, " +
                      "EncounterClearedActivator reveals the_heading dialogue + RETURN — TO THE STARS (CampaignFlagSetter sets " +
                      "ep25_complete + heris_recruited, then ReturnToSpace). GALAXY 4 EP25 FINALE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP25 Scenes", priority = 276)]
        public static void BuildAllEp25Scenes()
        {
            BuildEp25CargoShelf();
            BuildEp25SurgicalLab();
            BuildEp25RealRecord();
            BuildEp25Vault();
            BuildEp25KhallWire();
            BuildEp25PurposeUntold();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP25 scenes built + inputs rewired. GALAXY 4 EP25 COMPLETE.");
        }
    }
}

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
    /// EP19 "The Rust Meridian" scene builders. Builds three core episodes on a tidally-locked ore-mining debt-colony:
    /// - Cargo Hold: warm dim amber/rust freighter hold with Tara NPC
    /// - Asteroid Pursuit: space-flight scene with 4-ship enemy encounter over rust-orange dwarf star
    /// - Mine Shaft: bioluminescent green-amber ore tunnel with 3 thermal drone enemies
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP19 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep19" and loads lines from Ep19Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp19DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep19Lines.Get(setId), advanceRef, setId, clipPrefix: "ep19");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Cargo Hold", priority = 190)]
        public static void BuildEp19CargoHold()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cargo Hold: warm dim amber/rust freighter hold.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.65f, 0.45f); // warm amber/rust key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.14f); // dim rust ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.22f, 0.16f);
            RenderSettings.fogDensity = 0.015f;

            // Two amber accent point lights.
            BuildAccentPointLight("CargoLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.95f, 0.65f, 0.35f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("CargoLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.90f, 0.60f, 0.30f), intensity: 0.70f, range: 9f);

            // ---- Cargo hold floor and structure ----
            var cargoGo = new GameObject("CargoHold");
            var cargo = cargoGo.transform;
            var rustColor = new Color(0.70f, 0.55f, 0.40f);
            var darkerRust = new Color(0.50f, 0.38f, 0.25f);

            // Main cargo floor.
            BuildFloorCeiling(cargo, "CargoFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 18f), rustColor, darkerRust);

            // Cargo hold walls.
            BuildWall(cargo, "CargoWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(cargo, "CargoWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // Cargo props: crate cluster.
            var crateColor = new Color(0.65f, 0.50f, 0.35f);
            BuildProp(cargo, "Crate1", new Vector3(-2f, 0.8f, 5f), new Vector3(1.2f, 1.2f, 1.5f), crateColor);
            BuildProp(cargo, "Crate2", new Vector3(2f, 0.8f, 6f), new Vector3(1.5f, 1f, 1.2f), crateColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Tara NPC ----
            var taraGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            taraGo.name = "Tara";
            Object.DestroyImmediate(taraGo.GetComponent<Collider>());
            taraGo.transform.position = new Vector3(0f, 0f, 3f);
            taraGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(taraGo.GetComponent<Renderer>(), new Color(0.55f, 0.5f, 0.6f)); // mauve tint
            var taraNpc = taraGo.AddComponent<StoryNpc>();
            var tnSo = new SerializedObject(taraNpc);
            tnSo.FindProperty("displayName").stringValue = "Tara";
            tnSo.FindProperty("remote").boolValue = false;
            tnSo.ApplyModifiedPropertiesWithoutUndo();

            // Optional set-dressing debtor capsules.
            var debtorColor = new Color(0.50f, 0.45f, 0.55f);
            for (int i = 0; i < 2; i++)
            {
                var debtorGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                debtorGo.name = $"Debtor{i}";
                Object.DestroyImmediate(debtorGo.GetComponent<Collider>());
                debtorGo.transform.position = new Vector3(-2f + i * 4f, 0f, 8f + i);
                debtorGo.transform.localScale = new Vector3(0.5f, 1.7f, 0.5f);
                TintShared(debtorGo.GetComponent<Renderer>(), debtorColor);
            }

            // ---- Dialogue Players ----
            var ghostNameDialogue = BuildEp19DialoguePlayer("Dialogue_GhostName", new Vector3(0f, 1.5f, 2f), "ghost_name");
            var gnSo = new SerializedObject(ghostNameDialogue);
            gnSo.FindProperty("playOnStart").boolValue = true;
            gnSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE TURRET — PURSUIT".
            var pursuitBoxGo = BuildTransitionBox("ToPursuitBox", new Vector3(0f, 1.2f, 18.5f), "TO THE TURRET — PURSUIT",
                out var pursuitBtn, out var pursuitTransition);
            var pbSo = new SerializedObject(pursuitTransition);
            pbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19AsteroidPursuitSceneName;
            pbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pursuitBtn.onClick,
                new UnityEngine.Events.UnityAction(pursuitTransition.LoadOnFootScene));
            pursuitBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue ghost_name (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ghost Name";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ghostNameDialogue;

            // Step 1: Prompt — transition to Pursuit.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Ascend to Turret";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = pursuitBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19CargoHoldScenePath);
            EnsureScenesInBuild(Galaxy3Ep19CargoHoldScenePath, Galaxy3Ep19AsteroidPursuitScenePath);

            Debug.Log($"[Space Samurai] EP19 Cargo Hold scene built at {Galaxy3Ep19CargoHoldScenePath}. " +
                      "Layout: warm dim amber/rust freighter cargo hold with 2 crate props. " +
                      "Tara NPC (mauve tint, no Health). " +
                      "2 steps: ghost_name (auto) → transition to Asteroid Pursuit.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Asteroid Pursuit", priority = 191)]
        public static void BuildEp19AsteroidPursuit()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void with rust-orange dwarf star.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.025f, 0.02f);
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

            // Rust-orange dwarf star below/behind the corsair.
            var dwarvGo = AddUnlitVisual(universe, "Dwarf Star", new Vector3(-1500f, -800f, -2500f),
                Vector3.one * 700f, PrimitiveType.Sphere, new Color(0.85f, 0.45f, 0.25f)); // rust-orange
            var dwarfCollider = dwarvGo.GetComponent<Collider>();
            if (dwarfCollider != null) dwarfCollider.isTrigger = true;

            // 2-3 grey asteroid/ore cube clusters as isTrigger.
            var asteroidColor = new Color(0.45f, 0.42f, 0.40f);
            var asteroid1 = AddUnlitVisual(universe, "Asteroid Cluster 1", new Vector3(-2000f, 200f, -3000f),
                new Vector3(300f, 150f, 400f), PrimitiveType.Cube, asteroidColor);
            var a1Collider = asteroid1.GetComponent<Collider>();
            if (a1Collider != null) a1Collider.isTrigger = true;

            var asteroid2 = AddUnlitVisual(universe, "Asteroid Cluster 2", new Vector3(2500f, -300f, -2800f),
                new Vector3(280f, 140f, 380f), PrimitiveType.Cube, asteroidColor);
            var a2Collider = asteroid2.GetComponent<Collider>();
            if (a2Collider != null) a2Collider.isTrigger = true;

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
            var pursuitBarksDialogue = BuildEp19DialoguePlayer("Dialogue_PursuitBarks", new Vector3(0f, 1.62f, 0.8f),
                "pursuit_barks");
            var pursuitBarksGo = pursuitBarksDialogue.gameObject;
            pursuitBarksGo.transform.SetParent(cockpit, false);
            pursuitBarksGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            pursuitBarksGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: 4-ship pursuit (1 cruiser + 3 fighters) ----
            var pursuitEncounterGo = new GameObject("PursuitEncounter");
            var pursuitEncounter = pursuitEncounterGo.AddComponent<GuardEncounter>();
            var pursuitEncounterSo = new SerializedObject(pursuitEncounter);
            SetObjectRef(pursuitEncounterSo, "player", shipCtrl);
            SetObjectRef(pursuitEncounterSo, "universe", universe);
            SetObjectRef(pursuitEncounterSo, "pool", pool);
            SetObjectRef(pursuitEncounterSo, "definition", enemyShipDef);
            pursuitEncounterSo.FindProperty("shipCount").intValue = 4;
            pursuitEncounterSo.FindProperty("spawnRadius").floatValue = 280f;
            pursuitEncounterSo.FindProperty("initialDelay").floatValue = 5f;
            pursuitEncounterSo.FindProperty("requiredCompletedScene").stringValue = "";
            pursuitEncounterSo.FindProperty("clearedFlag").stringValue = "ep19_pursuit_cleared";
            SetObjectRef(pursuitEncounterSo, "spawnDialogue", pursuitBarksDialogue);
            pursuitEncounterSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "PUNCH THROUGH — THE MINE".
            var mineBoxGo = BuildTransitionBox("ToMineBox", new Vector3(0f, 1.2f, 0.8f), "PUNCH THROUGH — THE MINE",
                out var mineBtn, out var mineTransition);
            var mbSo = new SerializedObject(mineTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19MineShaftSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(mineBtn.onClick,
                new UnityEngine.Events.UnityAction(mineTransition.LoadOnFootScene));
            mineBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var pursuitGateGo = new GameObject("PursuitClearedGate");
            var pursuitGate = pursuitGateGo.AddComponent<EncounterClearedActivator>();
            var pursuitGateSo = new SerializedObject(pursuitGate);
            var activateProp = pursuitGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = mineBoxGo;
            pursuitGateSo.FindProperty("clearedFlag").stringValue = "ep19_pursuit_cleared";
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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19AsteroidPursuitScenePath);
            EnsureScenesInBuild(Galaxy3Ep19AsteroidPursuitScenePath, Galaxy3Ep19MineShaftScenePath);

            Debug.Log($"[Space Samurai] EP19 Asteroid Pursuit scene built at {Galaxy3Ep19AsteroidPursuitScenePath}. " +
                      "Space over rust-orange dwarf star (2 asteroid clusters). " +
                      "Cockpit with canopy + HUD. " +
                      "Flow: pursuit_barks spawn (GuardEncounter, 4 ships, 5s delay) → on cleared, " +
                      "EncounterClearedActivator reveals PUNCH THROUGH — THE MINE (LoadOnFootScene, chains to EP19 Mine Shaft).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP19 Mine Shaft", priority = 192)]
        public static void BuildEp19MineShaft()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Mine Shaft: sickly bioluminescent green-amber ore tunnel.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.6f, 0.4f); // green-amber key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.14f); // green ambient

            // Dense fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.40f, 0.22f);
            RenderSettings.fogDensity = 0.022f;

            // Two green-amber accent lights.
            BuildAccentPointLight("MineLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 0.55f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("MineLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.65f, 0.80f, 0.45f), intensity: 0.70f, range: 9f);

            // ---- Mine shaft floor and structure ----
            var mineGo = new GameObject("MineShaft");
            var mine = mineGo.transform;
            var oreGreen = new Color(0.40f, 0.55f, 0.45f);
            var darkerOre = new Color(0.25f, 0.38f, 0.30f);

            // Main mine floor.
            BuildFloorCeiling(mine, "MineFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), oreGreen, darkerOre);

            // Mine walls.
            BuildWall(mine, "MineWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(mine, "MineWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Ore equipment props.
            var equipColor = new Color(0.50f, 0.58f, 0.48f);
            BuildProp(mine, "OreProcessor", new Vector3(0f, 1.2f, 18f), new Vector3(1.5f, 1.8f, 0.8f), equipColor);
            BuildProp(mine, "OreChute", new Vector3(-2f, 0.8f, 6f), new Vector3(1f, 1.5f, 0.6f), equipColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Tara NPC ----
            var taraGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            taraGo.name = "Tara";
            Object.DestroyImmediate(taraGo.GetComponent<Collider>());
            taraGo.transform.position = new Vector3(0f, 0f, 3f);
            taraGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(taraGo.GetComponent<Renderer>(), new Color(0.55f, 0.5f, 0.6f)); // mauve tint
            var taraNpc = taraGo.AddComponent<StoryNpc>();
            var tnSo = new SerializedObject(taraNpc);
            tnSo.FindProperty("displayName").stringValue = "Tara";
            tnSo.FindProperty("remote").boolValue = false;
            tnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 3 Thermal Drones: 1 wave ----
            var droneColor = new Color(0.4f, 0.7f, 0.5f);
            var droneWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };

            var droneWaveHealths = new List<Health>();
            foreach (var pos in droneWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, droneColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                droneWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var droneBarksDialogue = BuildEp19DialoguePlayer("Dialogue_DroneBarks", new Vector3(0f, 1.5f, 8f), "drone_barks");
            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { droneBarksDialogue });

            // ---- Dialogue Players ----
            var mineDescentDialogue = BuildEp19DialoguePlayer("Dialogue_MineDescentent", new Vector3(0f, 1.5f, 2f), "mine_descent");
            var mdSo = new SerializedObject(mineDescentDialogue);
            mdSo.FindProperty("playOnStart").boolValue = true;
            mdSo.ApplyModifiedPropertiesWithoutUndo();

            var ghostMemoriesDialogue = BuildEp19DialoguePlayer("Dialogue_GhostMemories", new Vector3(0f, 1.5f, 14f), "ghost_memories");

            // Transition box: "DESCEND — THE VAULT".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND — THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep19VaultBelowSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var missionSo = new SerializedObject(missionDirector);
            var stepsProp = missionSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue mine_descent (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Mine Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = mineDescentDialogue;

            // Step 1: DefeatWaves — 3 thermal drones.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Thermal Drones (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Dialogue ghost_memories.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Ghost Memories";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = ghostMemoriesDialogue;

            // Step 3: Prompt — transition to Vault.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Vault";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

            missionSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep19MineShaftScenePath);
            EnsureScenesInBuild(Galaxy3Ep19MineShaftScenePath, Galaxy3Ep19VaultBelowScenePath);

            Debug.Log($"[Space Samurai] EP19 Mine Shaft scene built at {Galaxy3Ep19MineShaftScenePath}. " +
                      "Layout: sickly bioluminescent green-amber ore tunnel with ore processor + chute props. " +
                      "Tara NPC (mauve tint, no Health). " +
                      "3 Thermal Drones (green-tinted, nonLethal). " +
                      "4 steps: mine_descent (auto) → defeat 3 Thermal Drones (drone_barks bark) → " +
                      "ghost_memories dialogue → transition to Vault Below.");
        }
    }
}

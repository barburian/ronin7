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
    /// EP20 "Vendor of Ghosts" scene builders for the final four scenes:
    /// - CentralVault: bazaar central vault corridor with Vess NPC, 5 mixed syndicate enemies
    /// - DominionInterdiction: space-flight scene with 5-ship GuardEncounter over Dominion command ship
    /// - RecordsRoom: intimate vault chamber, holographic blue data-streams with Vess NPC, 3 Dominion elites
    /// - Hyperspace: cool blue observation lounge, pure denouement (NO enemies), THE EP20 FINALE, sets ep20_complete + vess_recruited flags
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly. BuildEp20DialoguePlayer helper is already declared in Ep20Builder.cs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Central Vault", priority = 203)]
        public static void BuildEp20CentralVault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Central Vault: bazaar central vault corridor, chaotic warm amber/alarm-red.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.5f, 0.4f); // warm amber/alarm-red key light
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.14f, 0.12f); // warm dark ambient

            // Alarm fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.20f, 0.16f);
            RenderSettings.fogDensity = 0.02f;

            // Two amber/red accent lights.
            BuildAccentPointLight("CentralLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.60f, 0.35f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("CentralLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.95f, 0.50f, 0.30f), intensity: 0.70f, range: 9f);

            // ---- Central vault floor and structure ----
            var vaultGo = new GameObject("CentralVault");
            var vault = vaultGo.transform;
            var bazaarMetal = new Color(0.50f, 0.42f, 0.38f);
            var darkerBazaar = new Color(0.30f, 0.25f, 0.22f);

            // Main vault floor (12 x 22).
            BuildFloorCeiling(vault, "CentralFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 22f), bazaarMetal, darkerBazaar);

            // Vault walls.
            BuildWall(vault, "CentralWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 22f));
            BuildWall(vault, "CentralWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 22f));

            // Data-core prop (torso-size).
            BuildProp(vault, "DataCore", new Vector3(0f, 1.2f, 15f), new Vector3(1f, 1.5f, 0.8f), new Color(0.55f, 0.48f, 0.42f));

            // Failing seal prop.
            BuildProp(vault, "FailingSeal", new Vector3(-3f, 2f, 18f), new Vector3(0.8f, 1.2f, 0.6f), new Color(0.60f, 0.40f, 0.35f));

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

            // ---- Vess StoryNpc ----
            var vessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessGo.name = "Vess";
            Object.DestroyImmediate(vessGo.GetComponent<Collider>());
            vessGo.transform.position = new Vector3(0f, 0f, 3f);
            vessGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(vessGo.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vnSo = new SerializedObject(vessNpc);
            vnSo.FindProperty("displayName").stringValue = "Vess";
            vnSo.FindProperty("remote").boolValue = false;
            vnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5 Mixed Syndicate Enemies (Tide Baron / Rustfang / Hollow): 1 wave ----
            var syndicateColors = new Color[]
            {
                new Color(0.65f, 0.35f, 0.40f), // crimson
                new Color(0.60f, 0.48f, 0.35f), // rust
                new Color(0.55f, 0.55f, 0.52f), // steel
                new Color(0.58f, 0.38f, 0.32f), // rust-brown
                new Color(0.50f, 0.42f, 0.38f)  // dark metal
            };

            var syndicateWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(2f, 0f, 5f),
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8f),
                new Vector3(0f, 0f, 10f)
            };

            var syndicateWaveHealths = new List<Health>();
            for (int i = 0; i < syndicateWavePositions.Length; i++)
            {
                var enemy = BuildDominionEnemy(syndicateWavePositions[i], playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, syndicateColors[i]);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                syndicateWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var syndicateSpawner = BuildEp03WaveSpawner("SyndicateSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { syndicateWaveHealths },
                new[] { BuildEp20DialoguePlayer("Dialogue_SyndicateBarks", new Vector3(0f, 1.5f, 8f), "syndicate_barks") });

            // ---- Dialogue Players ----
            var numberedWeaponsDialogue = BuildEp20DialoguePlayer("Dialogue_NumberedWeapons", new Vector3(0f, 1.5f, 2f), "numbered_weapons");
            var nwSo = new SerializedObject(numberedWeaponsDialogue);
            nwSo.FindProperty("playOnStart").boolValue = true;
            nwSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO DOMINION INTERDICTION".
            var interdictionBoxGo = BuildTransitionBox("ToInterdictionBox", new Vector3(0f, 1.2f, 21.5f), "TO DOMINION INTERDICTION",
                out var interdictionBtn, out var interdictionTransition);
            var ibSo = new SerializedObject(interdictionTransition);
            ibSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20DominionInterdictionSceneName;
            ibSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(interdictionBtn.onClick,
                new UnityEngine.Events.UnityAction(interdictionTransition.LoadOnFootScene));
            interdictionBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue numbered_weapons (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Numbered Weapons";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = numberedWeaponsDialogue;

            // Step 1: DefeatWaves — 5 mixed syndicate.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Mixed Syndicate (5)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = syndicateSpawner;

            // Step 2: Prompt — transition to Dominion Interdiction.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To Dominion Interdiction";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = interdictionBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20CentralVaultScenePath);
            EnsureScenesInBuild(Galaxy3Ep20CentralVaultScenePath, Galaxy3Ep20DominionInterdictionScenePath);

            Debug.Log($"[Space Samurai] EP20 Central Vault scene built at {Galaxy3Ep20CentralVaultScenePath}. " +
                      "Layout: bazaar central vault corridor with warm amber/alarm-red lighting, dark metal floor/walls, data-core and failing-seal props. " +
                      "Vess NPC (warm grey-orange tint, no Health). " +
                      "5 Mixed Syndicate enemies (varied tints, nonLethal). " +
                      "3 steps: numbered_weapons (auto) → defeat 5 syndicate (syndicate_barks bark) → transition to Dominion Interdiction.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Dominion Interdiction", priority = 204)]
        public static void BuildEp20DominionInterdiction()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void with Dominion command ship.
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

            // Large dark-steel Dominion command ship (sphere).
            var commandShipGo = AddUnlitVisual(universe, "Dominion Command Ship", new Vector3(-1500f, -800f, -2500f),
                Vector3.one * 700f, PrimitiveType.Sphere, new Color(0.35f, 0.38f, 0.42f)); // dark steel
            var commandShipCollider = commandShipGo.GetComponent<Collider>();
            if (commandShipCollider != null) commandShipCollider.isTrigger = true;

            // 2 Bazaar-wreckage asteroid clusters (isTrigger).
            var wreckageColor = new Color(0.45f, 0.42f, 0.40f);
            var wreckage1 = AddUnlitVisual(universe, "Wreckage Cluster 1", new Vector3(-2000f, 200f, -3000f),
                new Vector3(300f, 150f, 400f), PrimitiveType.Cube, wreckageColor);
            var w1Collider = wreckage1.GetComponent<Collider>();
            if (w1Collider != null) w1Collider.isTrigger = true;

            var wreckage2 = AddUnlitVisual(universe, "Wreckage Cluster 2", new Vector3(2500f, -300f, -2800f),
                new Vector3(280f, 140f, 380f), PrimitiveType.Cube, wreckageColor);
            var w2Collider = wreckage2.GetComponent<Collider>();
            if (w2Collider != null) w2Collider.isTrigger = true;

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
            var interdictionBarksDialogue = BuildEp20DialoguePlayer("Dialogue_InterdictionBarks", new Vector3(0f, 1.62f, 0.8f),
                "interdiction");
            var interdictionBarksGo = interdictionBarksDialogue.gameObject;
            interdictionBarksGo.transform.SetParent(cockpit, false);
            interdictionBarksGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            interdictionBarksGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: 5-ship interdiction force ----
            var interdictionEncounterGo = new GameObject("InterdictionEncounter");
            var interdictionEncounter = interdictionEncounterGo.AddComponent<GuardEncounter>();
            var interdictionEncounterSo = new SerializedObject(interdictionEncounter);
            SetObjectRef(interdictionEncounterSo, "player", shipCtrl);
            SetObjectRef(interdictionEncounterSo, "universe", universe);
            SetObjectRef(interdictionEncounterSo, "pool", pool);
            SetObjectRef(interdictionEncounterSo, "definition", enemyShipDef);
            interdictionEncounterSo.FindProperty("shipCount").intValue = 5;
            interdictionEncounterSo.FindProperty("spawnRadius").floatValue = 280f;
            interdictionEncounterSo.FindProperty("initialDelay").floatValue = 5f;
            interdictionEncounterSo.FindProperty("requiredCompletedScene").stringValue = "";
            interdictionEncounterSo.FindProperty("clearedFlag").stringValue = "ep20_interdiction_cleared";
            SetObjectRef(interdictionEncounterSo, "spawnDialogue", interdictionBarksDialogue);
            interdictionEncounterSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP CLEAR — HYPERSPACE".
            var hyperspaceBoxGo = BuildTransitionBox("ToHyperspaceBox", new Vector3(0f, 1.2f, 0.8f), "JUMP CLEAR — HYPERSPACE",
                out var hyperspaceBtn, out var hyperspaceTransition);
            var hbSo = new SerializedObject(hyperspaceTransition);
            hbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20HyperspaceSceneName;
            hbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(hyperspaceBtn.onClick,
                new UnityEngine.Events.UnityAction(hyperspaceTransition.LoadOnFootScene));
            hyperspaceBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var interdictionGateGo = new GameObject("InterdictionClearedGate");
            var interdictionGate = interdictionGateGo.AddComponent<EncounterClearedActivator>();
            var interdictionGateSo = new SerializedObject(interdictionGate);
            var activateProp = interdictionGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = hyperspaceBoxGo;
            interdictionGateSo.FindProperty("clearedFlag").stringValue = "ep20_interdiction_cleared";
            interdictionGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20DominionInterdictionScenePath);
            EnsureScenesInBuild(Galaxy3Ep20DominionInterdictionScenePath, Galaxy3Ep20HyperspaceScenePath);

            Debug.Log($"[Space Samurai] EP20 Dominion Interdiction scene built at {Galaxy3Ep20DominionInterdictionScenePath}. " +
                      "Space over large Dominion command ship (2 bazaar-wreckage asteroid clusters). " +
                      "Cockpit with canopy + HUD. " +
                      "Flow: interdiction_barks spawn (GuardEncounter, 5 ships, 5s delay) → on cleared, " +
                      "EncounterClearedActivator reveals JUMP CLEAR — HYPERSPACE (LoadOnFootScene, chains to EP20 Hyperspace Finale).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Records Room", priority = 205)]
        public static void BuildEp20RecordsRoom()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Records Room: intimate vault chamber, holographic blue data-streams.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.75f, 1.0f); // cyan-blue key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.18f, 0.26f); // cool dark blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.24f, 0.34f);
            RenderSettings.fogDensity = 0.018f;

            // Two cyan accent lights.
            BuildAccentPointLight("RecordsLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.50f, 0.85f, 1f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("RecordsLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.45f, 0.80f, 0.95f), intensity: 0.75f, range: 9f);

            // ---- Records room floor and walls ----
            var roomGo = new GameObject("RecordsRoom");
            var room = roomGo.transform;
            var darkMetal = new Color(0.25f, 0.28f, 0.32f);
            var darkerMetal = new Color(0.15f, 0.18f, 0.22f);

            // Main room floor (12 x 20).
            BuildFloorCeiling(room, "RecordsFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), darkMetal, darkerMetal);

            // Room walls.
            BuildWall(room, "RecordsWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(room, "RecordsWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- Unlit cyan data-core cube props ----
            var dataCoreColor = new Color(0.50f, 0.85f, 1f); // bright cyan
            var dataCores = 3;
            for (int i = 0; i < dataCores; i++)
            {
                float x = (i - 1) * 3f;
                float y = 1.5f;
                float z = 8f + i * 4f;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = $"DataCore_{i}";
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.transform.SetParent(room, false);
                core.transform.position = new Vector3(x, y, z);
                core.transform.localScale = new Vector3(0.5f, 0.6f, 0.4f);
                core.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(dataCoreColor);
            }

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

            // ---- Vess StoryNpc (ally, set-dressing) ----
            var vessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessGo.name = "Vess";
            Object.DestroyImmediate(vessGo.GetComponent<Collider>());
            vessGo.transform.position = new Vector3(0f, 0f, 3f);
            vessGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(vessGo.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint
            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vnSo = new SerializedObject(vessNpc);
            vnSo.FindProperty("displayName").stringValue = "Vess";
            vnSo.FindProperty("remote").boolValue = false;
            vnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 3 Dominion Elite enemies: 1 wave ----
            var eliteColor = new Color(0.5f, 0.55f, 0.62f); // steel tint
            var eliteWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(1.5f, 0f, 9f)
            };

            var eliteWaveHealths = new List<Health>();
            foreach (var pos in eliteWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                eliteWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var eliteSpawner = BuildEp03WaveSpawner("EliteSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { eliteWaveHealths },
                new[] { BuildEp20DialoguePlayer("Dialogue_EliteBarks", new Vector3(0f, 1.5f, 8f), "elite_barks") });

            // ---- Dialogue Players ----
            var merchantsChoiceDialogue = BuildEp20DialoguePlayer("Dialogue_MerchantsChoice", new Vector3(0f, 1.5f, 2f), "merchants_choice");
            var mcSo = new SerializedObject(merchantsChoiceDialogue);
            mcSo.FindProperty("playOnStart").boolValue = true;
            mcSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO HYPERSPACE".
            var hyperspaceBoxGo = BuildTransitionBox("ToHyperspaceBox2", new Vector3(0f, 1.2f, 20.5f), "TO HYPERSPACE",
                out var hyperspaceBtn, out var hyperspaceTransition);
            var hbSo = new SerializedObject(hyperspaceTransition);
            hbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep20HyperspaceSceneName;
            hbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(hyperspaceBtn.onClick,
                new UnityEngine.Events.UnityAction(hyperspaceTransition.LoadOnFootScene));
            hyperspaceBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue merchants_choice (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Merchants Choice";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = merchantsChoiceDialogue;

            // Step 1: DefeatWaves — 3 dominion elites.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Elites (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = eliteSpawner;

            // Step 2: Prompt — transition to Hyperspace.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To Hyperspace";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = hyperspaceBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20RecordsRoomScenePath);
            EnsureScenesInBuild(Galaxy3Ep20RecordsRoomScenePath, Galaxy3Ep20HyperspaceScenePath);

            Debug.Log($"[Space Samurai] EP20 Records Room scene built at {Galaxy3Ep20RecordsRoomScenePath}. " +
                      "Layout: intimate vault chamber with holographic blue data-streams, dark metal floor/walls, unlit cyan data-core cubes. " +
                      "Vess NPC (warm grey-orange tint, no Health, set-dressing). " +
                      "3 Dominion Elite enemies (steel tint, nonLethal). " +
                      "3 steps: merchants_choice (auto) → defeat 3 elites (elite_barks bark) → transition to Hyperspace.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP20 Hyperspace", priority = 206)]
        public static void BuildEp20Hyperspace()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Hyperspace: cool blue observation lounge (the finale).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.85f, 1.0f); // cool blue key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.28f, 0.35f); // cool blue ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.40f, 0.48f);
            RenderSettings.fogDensity = 0.018f;

            // Two cool blue accent lights.
            BuildAccentPointLight("HyperspaceLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.60f, 0.80f, 1f), intensity: 0.80f, range: 10f);
            BuildAccentPointLight("HyperspaceLight2", new Vector3(3f, 2.5f, 14f),
                new Color(0.55f, 0.75f, 0.95f), intensity: 0.75f, range: 9f);

            // ---- Observation lounge floor and walls ----
            var loungeGo = new GameObject("ObservationLounge");
            var lounge = loungeGo.transform;
            var hyperspaceMetal = new Color(0.32f, 0.38f, 0.45f);
            var hyperspaceDark = new Color(0.18f, 0.25f, 0.32f);

            // Main lounge floor (12 x 20).
            BuildFloorCeiling(lounge, "HyperspaceFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), hyperspaceMetal, hyperspaceDark);

            // Lounge walls.
            BuildWall(lounge, "HyperspaceWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(lounge, "HyperspaceWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Console panel prop.
            BuildProp(lounge, "ConsolePanel", new Vector3(-3f, 1.5f, 8f), new Vector3(2f, 1.5f, 0.3f), new Color(0.45f, 0.50f, 0.55f));

            // Observation window prop.
            BuildProp(lounge, "ObservationWindow", new Vector3(3f, 2f, 20f), new Vector3(3f, 2f, 0.1f), new Color(0.70f, 0.85f, 1f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Vess StoryNpc capsule ----
            var vessGo = new GameObject("Vess_NPC");
            vessGo.transform.SetParent(lounge, false);
            vessGo.transform.position = new Vector3(-2f, 0f, 6f);

            var vessBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vessBody.name = "Body";
            vessBody.transform.SetParent(vessGo.transform, false);
            TintShared(vessBody.GetComponent<Renderer>(), new Color(0.55f, 0.45f, 0.42f)); // warm grey-orange tint

            var vessNpc = vessGo.AddComponent<StoryNpc>();
            var vessNpcSo = new SerializedObject(vessNpc);
            vessNpcSo.FindProperty("displayName").stringValue = "Vess";
            vessNpcSo.FindProperty("remote").boolValue = false;
            vessNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players (NO enemies — pure denouement) ----
            var namesAreArchiveDialogue = BuildEp20DialoguePlayer("Dialogue_NamesAreArchive", new Vector3(0f, 1.5f, 2f), "names_are_archive");
            var naadiSo = new SerializedObject(namesAreArchiveDialogue);
            naadiSo.FindProperty("playOnStart").boolValue = true;
            naadiSo.ApplyModifiedPropertiesWithoutUndo();

            var kessMessageDialogue = BuildEp20DialoguePlayer("Dialogue_KessMessage", new Vector3(0f, 1.5f, 8f), "kess_message");

            // Finale transition box: "RETURN — TO THE STARS" with CampaignFlagSetter.
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 20.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            // Set flags: ep20_complete, vess_recruited.
            var finaleFlagSetter = returnBoxGo.AddComponent<Ronin7.World.Story.CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 2;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep20_complete";
            finaleFlagsProp.GetArrayElementAtIndex(1).stringValue = "vess_recruited";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue names_are_archive (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Names Are Archive";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = namesAreArchiveDialogue;

            // Step 1: Dialogue kess_message.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Kess Message";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = kessMessageDialogue;

            // Step 2: Prompt — return to space with ep20_complete + vess_recruited flags.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Return to the Stars (sets ep20_complete + vess_recruited)";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep20HyperspaceScenePath);
            EnsureScenesInBuild(Galaxy3Ep20HyperspaceScenePath);

            Debug.Log($"[Space Samurai] EP20 Hyperspace scene built at {Galaxy3Ep20HyperspaceScenePath}. " +
                      "Layout: cool blue observation lounge in hyperspace with cool blue lighting, dark metal floor/walls, console and window props. " +
                      "Vess NPC (warm grey-orange capsule, NO Health, StoryNpc displayName). " +
                      "NO enemies (pure denouement, dialogue-only finale). " +
                      "3 steps: names_are_archive (auto) → kess_message dialogue → " +
                      "return to hub Prompt (sets ep20_complete + vess_recruited via CampaignFlagSetter, then ReturnToSpace). " +
                      "EPISODE 20 FINALE (sets ep20_complete + vess_recruited).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build All EP20 Scenes", priority = 207)]
        public static void BuildAllEp20Scenes()
        {
            BuildEp20DockingBay();
            BuildEp20ChemicalSector();
            BuildEp20VarekShardVault();
            BuildEp20CentralVault();
            BuildEp20DominionInterdiction();
            BuildEp20RecordsRoom();
            BuildEp20Hyperspace();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP20 scenes built + inputs rewired.");
        }
    }
}

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
    /// EP10 "The Broken Echo" finale scene builders: Rescue (underground tunnels), Landing Zone (surface),
    /// and Extraction (space). Builds the story flow from the underground sanctuary rescue through surface
    /// evacuation to the final space picket encounter.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: BuildEp10DialoguePlayer lives in Ep10Builder.cs (same partial class) — shared here.
        // NOTE: Scene paths and names (Galaxy2Ep10RescueScenePath, etc.) are defined in Galaxy2Builder.cs.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Rescue", priority = 119)]
        public static void BuildEp10Rescue()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rescue: dark branching stone tunnels with sparse emergency lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.6f, 0.45f); // amber key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.09f, 0.07f); // very dark amber tones

            // Underground atmosphere: heavy fog with amber tint.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.30f, 0.25f, 0.18f);
            RenderSettings.fogDensity = 0.040f;

            // Sparse emergency lighting accents.
            BuildAccentPointLight("EmergencyLight1", new Vector3(-3f, 1.5f, 5f),
                new Color(1f, 0.6f, 0.2f), intensity: 0.7f, range: 8f);
            BuildAccentPointLight("EmergencyLight2", new Vector3(3f, 1.5f, 12f),
                new Color(1f, 0.5f, 0.15f), intensity: 0.6f, range: 7f);

            // ---- Underground tunnel system: main tunnel with sealed chamber branch ----
            var tunnelGo = new GameObject("UndergroundTunnels");
            var tunnel = tunnelGo.transform;

            var stoneGrey = new Color(0.35f, 0.33f, 0.30f);
            var darkStone = new Color(0.20f, 0.18f, 0.16f);

            // Main tunnel (x[-5,5], z[0,20]).
            BuildFloorCeiling(tunnel, "MainTunnelFloor", new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 20f), stoneGrey, darkStone);
            BuildWall(tunnel, "MainTunnelWall_W", new Vector3(-5f, 1.5f, 10f), new Vector3(0.3f, 3f, 20f));
            BuildWall(tunnel, "MainTunnelWall_E", new Vector3(5f, 1.5f, 10f), new Vector3(0.3f, 3f, 20f));

            // Branch to sealed chamber (x[5,10], z[10,18]).
            BuildFloorCeiling(tunnel, "ChamberBranchFloor", new Vector3(7.5f, 0f, 14f), new Vector3(5f, 0f, 8f), stoneGrey, darkStone);
            BuildWall(tunnel, "ChamberBranchWall_S", new Vector3(7.5f, 1.5f, 10f), new Vector3(5f, 3f, 0.3f));
            BuildWall(tunnel, "ChamberBranchWall_N", new Vector3(7.5f, 1.5f, 18f), new Vector3(5f, 3f, 0.3f));
            BuildWall(tunnel, "ChamberBranchWall_E", new Vector3(10.2f, 1.5f, 14f), new Vector3(0.3f, 3f, 8f));

            // Upper hatch exit area (x[-5,0], z[0,5]).
            BuildFloorCeiling(tunnel, "HatchFloor", new Vector3(-2.5f, 0f, 2.5f), new Vector3(5f, 0f, 5f), stoneGrey, darkStone);

            // Unlit amber path markers along main tunnel (sparse emergency strips).
            AddUnlitVisual(tunnel, "PathMarker1", new Vector3(0f, 0.1f, 5f),
                new Vector3(8f, 0.05f, 0.5f), PrimitiveType.Cube, new Color(1f, 0.5f, 0.1f));
            AddUnlitVisual(tunnel, "PathMarker2", new Vector3(0f, 0.1f, 15f),
                new Vector3(8f, 0.05f, 0.5f), PrimitiveType.Cube, new Color(1f, 0.4f, 0.08f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- ALLY NPC: Sister Meredith (StoryNpc, NO Health) ----
            var meredithPos = new Vector3(-2f, 0f, 1f);
            var meredithGo = InstantiateNpc(GeneratedCharFolder + "/SisterMeredith.prefab", meredithPos, "Meredith");
            if (meredithGo != null)
            {
                var allyCombatant = meredithGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = meredithGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(storyNpc);
                mSo.FindProperty("displayName").stringValue = "Meredith";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 3 Teenager NPCs in sealed chamber (StoryNpc, NO Health, NpcWalker disabled at start) ----
            // Using AurelingNpc07, AurelingNpc08, AurelingNpc09 as plausible teen characters.
            var teen1Pos = new Vector3(6f, 0f, 13f);
            var teen1Go = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc07.prefab", teen1Pos, "Teen1");
            var teen1Walker = new GameObject("Teen1Walker");
            teen1Walker.transform.position = teen1Pos;
            if (teen1Go != null)
            {
                var storyNpc = teen1Go.AddComponent<StoryNpc>();
                var t1So = new SerializedObject(storyNpc);
                t1So.FindProperty("displayName").stringValue = "Kael";
                t1So.FindProperty("remote").boolValue = false;
                t1So.ApplyModifiedPropertiesWithoutUndo();

                var walker = teen1Walker.AddComponent<NpcWalker>();
                var wSo = new SerializedObject(walker);
                SetObjectRef(wSo, "target", teen1Go.transform);
                var wpArray = new Transform[]
                {
                    CreateWaypoint(teen1Walker.transform, "wp_chamber", new Vector3(0f, 0f, 0f)),
                    CreateWaypoint(teen1Walker.transform, "wp_main", new Vector3(-6f, 0f, -4f)),
                    CreateWaypoint(teen1Walker.transform, "wp_hatch", new Vector3(-2.5f, 0f, 2.5f))
                };
                SetObjectRefList(wSo, "waypoints", new List<Object>(wpArray));
                wSo.FindProperty("moveSpeed").floatValue = 1.4f;
                wSo.FindProperty("faceTravel").boolValue = true;
                wSo.ApplyModifiedPropertiesWithoutUndo();
                teen1Walker.SetActive(false);
            }

            var teen2Pos = new Vector3(8f, 0f, 15f);
            var teen2Go = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc08.prefab", teen2Pos, "Teen2");
            var teen2Walker = new GameObject("Teen2Walker");
            teen2Walker.transform.position = teen2Pos;
            if (teen2Go != null)
            {
                var storyNpc = teen2Go.AddComponent<StoryNpc>();
                var t2So = new SerializedObject(storyNpc);
                t2So.FindProperty("displayName").stringValue = "Lyris";
                t2So.FindProperty("remote").boolValue = false;
                t2So.ApplyModifiedPropertiesWithoutUndo();

                var walker = teen2Walker.AddComponent<NpcWalker>();
                var wSo = new SerializedObject(walker);
                SetObjectRef(wSo, "target", teen2Go.transform);
                var wpArray = new Transform[]
                {
                    CreateWaypoint(teen2Walker.transform, "wp_chamber", new Vector3(0f, 0f, 0f)),
                    CreateWaypoint(teen2Walker.transform, "wp_main", new Vector3(-4f, 0f, -6f)),
                    CreateWaypoint(teen2Walker.transform, "wp_hatch", new Vector3(-2.5f, 0f, 2.5f))
                };
                SetObjectRefList(wSo, "waypoints", new List<Object>(wpArray));
                wSo.FindProperty("moveSpeed").floatValue = 1.5f;
                wSo.FindProperty("faceTravel").boolValue = true;
                wSo.ApplyModifiedPropertiesWithoutUndo();
                teen2Walker.SetActive(false);
            }

            var teen3Pos = new Vector3(9f, 0f, 13.5f);
            var teen3Go = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc09.prefab", teen3Pos, "Teen3");
            var teen3Walker = new GameObject("Teen3Walker");
            teen3Walker.transform.position = teen3Pos;
            if (teen3Go != null)
            {
                var storyNpc = teen3Go.AddComponent<StoryNpc>();
                var t3So = new SerializedObject(storyNpc);
                t3So.FindProperty("displayName").stringValue = "Venn";
                t3So.FindProperty("remote").boolValue = false;
                t3So.ApplyModifiedPropertiesWithoutUndo();

                var walker = teen3Walker.AddComponent<NpcWalker>();
                var wSo = new SerializedObject(walker);
                SetObjectRef(wSo, "target", teen3Go.transform);
                var wpArray = new Transform[]
                {
                    CreateWaypoint(teen3Walker.transform, "wp_chamber", new Vector3(0f, 0f, 0f)),
                    CreateWaypoint(teen3Walker.transform, "wp_main", new Vector3(-5f, 0f, -5f)),
                    CreateWaypoint(teen3Walker.transform, "wp_hatch", new Vector3(-2.5f, 0f, 2.5f))
                };
                SetObjectRefList(wSo, "waypoints", new List<Object>(wpArray));
                wSo.FindProperty("moveSpeed").floatValue = 1.3f;
                wSo.FindProperty("faceTravel").boolValue = true;
                wSo.ApplyModifiedPropertiesWithoutUndo();
                teen3Walker.SetActive(false);
            }

            // ---- Sealed chamber door (ProximityDoor, proximity-opens as the player reaches it) ----
            var doorGo = new GameObject("ChamberDoor");
            doorGo.transform.position = new Vector3(7.5f, 0.5f, 10.15f);

            var doorLeftPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorLeftPanel.name = "DoorLeftPanel";
            doorLeftPanel.transform.SetParent(doorGo.transform, false);
            doorLeftPanel.transform.localPosition = new Vector3(-0.5f, 1.5f, 0f);
            doorLeftPanel.transform.localScale = new Vector3(0.4f, 2.5f, 0.15f);
            TintShared(doorLeftPanel.GetComponent<Renderer>(), new Color(0.25f, 0.23f, 0.20f));

            var doorRightPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorRightPanel.name = "DoorRightPanel";
            doorRightPanel.transform.SetParent(doorGo.transform, false);
            doorRightPanel.transform.localPosition = new Vector3(0.5f, 1.5f, 0f);
            doorRightPanel.transform.localScale = new Vector3(0.4f, 2.5f, 0.15f);
            TintShared(doorRightPanel.GetComponent<Renderer>(), new Color(0.25f, 0.23f, 0.20f));

            var proximityDoor = doorGo.AddComponent<ProximityDoor>();
            var pdSo = new SerializedObject(proximityDoor);
            SetObjectRef(pdSo, "leftPanel", doorLeftPanel.transform);
            SetObjectRef(pdSo, "rightPanel", doorRightPanel.transform);
            pdSo.FindProperty("openOffset").vector3Value = new Vector3(0.6f, 0f, 0f);
            pdSo.FindProperty("triggerRadius").floatValue = 3f;
            pdSo.FindProperty("slideSpeed").floatValue = 4f;
            pdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var photographDialogue = BuildEp10DialoguePlayer("Dialogue_ThePhotograph", new Vector3(0f, 1.5f, 5f), "the_photograph");
            var photographDlgSo = new SerializedObject(photographDialogue);
            photographDlgSo.FindProperty("playOnStart").boolValue = true;
            photographDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var rescueRunDialogue = BuildEp10DialoguePlayer("Dialogue_RescueRun", new Vector3(0f, 1.5f, 10f), "rescue_run");

            var teenDoorDialogue = BuildEp10DialoguePlayer("Dialogue_TeenDoor", new Vector3(7.5f, 1.5f, 14f), "teen_door");

            // ---- Enemies: 5 troopers in two waves (2 + 3) ----
            var trooperColor = new Color(0.50f, 0.48f, 0.45f);

            // Wave 1: 2 troopers.
            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 6f),
                new Vector3(2f, 0f, 7f)
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, trooperColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 0.5f, 6.5f), 2.5f,
                new List<List<Health>> { wave1Healths }, new[] { rescueRunDialogue });

            // Wave 2: 3 troopers.
            var wave2Positions = new Vector3[]
            {
                new Vector3(-3f, 0f, 12f),
                new Vector3(0f, 0f, 13f),
                new Vector3(3f, 0f, 12f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, trooperColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 0.5f, 12.5f), 2.5f,
                new List<List<Health>> { wave2Healths }, new[] { BuildEp10DialoguePlayer("Dialogue_RescueRun2", new Vector3(0f, 1.5f, 12f), "rescue_run") });

            // Reach point at sealed chamber door.
            var doorReachGo = new GameObject("DoorReachPoint");
            doorReachGo.transform.position = new Vector3(7.5f, 1f, 10.15f);

            // Reach point at upper hatch.
            var hatchReachGo = new GameObject("HatchReachPoint");
            hatchReachGo.transform.position = new Vector3(-2.5f, 0.5f, 2.5f);

            // Transition box: "TOPSIDE — THE LANDING ZONE".
            var lzBoxGo = BuildTransitionBox("ToLandingZoneBox", new Vector3(-2.5f, 1.2f, 3.5f), "TOPSIDE — THE LANDING ZONE",
                out var lzBtn, out var lzTransition);
            var lzSo = new SerializedObject(lzTransition);
            lzSo.FindProperty("onFootScene").stringValue = Galaxy2Ep10LandingZoneSceneName;
            lzSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(lzBtn.onClick,
                new UnityEngine.Events.UnityAction(lzTransition.LoadOnFootScene));
            lzBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue the_photograph (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Photograph";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = photographDialogue;

            // Step 1: DefeatWaves — Wave 1 (2 troopers).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1 (2 Troopers)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 2: ReachTrigger — sealed chamber door (activates ProximityDoor).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Sealed Door (open ProximityDoor)";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = doorReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: Dialogue teen_door.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Teen Door";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = teenDoorDialogue;

            // Step 4: Trigger step — activate teen walkers.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s4.FindPropertyRelative("label").stringValue = "Trigger: Activate Teen Walkers";
            var activateProp4 = s4.FindPropertyRelative("triggerObjects");
            activateProp4.arraySize = 3;
            activateProp4.GetArrayElementAtIndex(0).objectReferenceValue = teen1Walker;
            activateProp4.GetArrayElementAtIndex(1).objectReferenceValue = teen2Walker;
            activateProp4.GetArrayElementAtIndex(2).objectReferenceValue = teen3Walker;

            // Step 5: ReachTrigger — upper hatch (player-driven).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Upper Hatch";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = hatchReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 6: Prompt — transition to Landing Zone.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: To Landing Zone";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = lzBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10RescueScenePath);
            EnsureScenesInBuild(Galaxy2Ep10RescueScenePath);

            Debug.Log($"[Space Samurai] EP10 Rescue scene built at {Galaxy2Ep10RescueScenePath}. " +
                      "Layout: dark branching stone tunnels with sealed chamber. Sparse amber emergency lighting. " +
                      "Meredith ally NPC (AllyCombatant, NO Health). 3 teen NPCs (Kael, Lyris, Venn) in sealed chamber with disabled NpcWalkers. " +
                      "7 steps: the_photograph (auto) → defeat 2 troopers → reach sealed door (ProximityDoor opens) → teen_door dialogue → " +
                      "trigger teen walkers → reach upper hatch → transition to Landing Zone.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Landing Zone", priority = 120)]
        public static void BuildEp10LandingZone()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Landing Zone: dusk surface with golden-amber lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.75f, 0.5f); // golden-amber key light
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -45f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.30f, 0.22f); // warm dusk tones

            // Clear air, no fog.
            RenderSettings.fog = false;

            // Warm accent lights.
            BuildAccentPointLight("DuskLight1", new Vector3(-8f, 2f, 5f),
                new Color(1f, 0.7f, 0.4f), intensity: 1.3f, range: 15f);
            BuildAccentPointLight("DuskLight2", new Vector3(8f, 2f, 10f),
                new Color(1f, 0.65f, 0.35f), intensity: 1.2f, range: 14f);

            // ---- Landing Zone surface: flat ground with rubble and Kessler's grounded ship ----
            var lzGo = new GameObject("LandingZone");
            var lz = lzGo.transform;

            var dustColor = new Color(0.65f, 0.60f, 0.50f);
            var rockColor = new Color(0.50f, 0.45f, 0.38f);

            // Open LZ floor.
            BuildFloorCeiling(lz, "LZFloor", new Vector3(0f, 0f, 5f), new Vector3(30f, 0f, 20f), dustColor, dustColor);

            // Rubble perimeter (scattered cubes).
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 7f;
                float z = (i % 2) * 15f;
                BuildProp(lz, $"Rubble{i}", new Vector3(x, 0.3f, z), new Vector3(2f + i * 0.3f, 0.6f, 2.5f), rockColor);
            }

            // Kessler's grounded ship: elongated hull cluster with ramp.
            var shipRoot = new GameObject("KesslersShip");
            shipRoot.transform.SetParent(lz, false);
            shipRoot.transform.position = new Vector3(-10f, 0f, 5f);

            var hullColor = new Color(0.30f, 0.28f, 0.25f);
            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "Hull";
            Object.DestroyImmediate(hull.GetComponent<Collider>());
            hull.transform.SetParent(shipRoot.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1f, 0f);
            hull.transform.localScale = new Vector3(6f, 3f, 10f);
            TintShared(hull.GetComponent<Renderer>(), hullColor);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            Object.DestroyImmediate(ramp.GetComponent<Collider>());
            ramp.transform.SetParent(shipRoot.transform, false);
            ramp.transform.localPosition = new Vector3(0f, 0.3f, 5.5f);
            ramp.transform.localScale = new Vector3(6f, 0.6f, 3f);
            TintShared(ramp.GetComponent<Renderer>(), hullColor);

            // ---- Gunship prop: dark hull with red emissive strips, on elevated walker track ----
            var gunshipRoot = new GameObject("Gunship");
            gunshipRoot.transform.SetParent(lz, false);
            gunshipRoot.transform.position = new Vector3(12f, 5f, 8f);

            var gunshipHull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gunshipHull.name = "GunshipHull";
            Object.DestroyImmediate(gunshipHull.GetComponent<Collider>());
            gunshipHull.transform.SetParent(gunshipRoot.transform, false);
            gunshipHull.transform.localPosition = Vector3.zero;
            gunshipHull.transform.localScale = new Vector3(8f, 2f, 4f);
            TintShared(gunshipHull.GetComponent<Renderer>(), new Color(0.15f, 0.14f, 0.13f));

            AddUnlitVisual(gunshipRoot.transform, "GunshipRedStrip", new Vector3(0f, 0.8f, 0f),
                new Vector3(8f, 0.1f, 3.5f), PrimitiveType.Cube, new Color(1f, 0.1f, 0.05f));

            var gunshipWalker = gunshipRoot.AddComponent<NpcWalker>();
            var gwSo = new SerializedObject(gunshipWalker);
            SetObjectRef(gwSo, "target", gunshipRoot.transform);
            // Waypoints must live under the STATIC lz root — NpcWalker reads wp.position live,
            // so waypoints parented to the moving gunship would never be reached.
            var gwpArray = new Transform[]
            {
                CreateWaypoint(lz, "gunship_wp_start", new Vector3(12f, 5f, 8f)),
                CreateWaypoint(lz, "gunship_wp_mid1", new Vector3(5f, 7f, 12f)),
                CreateWaypoint(lz, "gunship_wp_mid2", new Vector3(-5f, 6f, 8f)),
                CreateWaypoint(lz, "gunship_wp_mid3", new Vector3(0f, 8f, 2f)),
                CreateWaypoint(lz, "gunship_wp_sweep", new Vector3(10f, 7f, 5f)),
                CreateWaypoint(lz, "gunship_wp_final", new Vector3(12f, 5f, 8f))
            };
            SetObjectRefList(gwSo, "waypoints", new List<Object>(gwpArray));
            gwSo.FindProperty("moveSpeed").floatValue = 25f;
            gwSo.FindProperty("faceTravel").boolValue = false;
            gwSo.ApplyModifiedPropertiesWithoutUndo();
            gunshipRoot.SetActive(false);

            // ---- Evacuation pod prop on eastern rail ----
            var evacPodGo = AddUnlitVisual(lz, "EvacPod", new Vector3(15f, 0.5f, 0f),
                new Vector3(2f, 2.5f, 3f), PrimitiveType.Cube, new Color(0.8f, 0.7f, 0.5f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Meredith + 3 teens near ship ramp (decorative setup) ----
            var meredithLZPos = new Vector3(-10f, 0f, 8f);
            var meredithLZGo = InstantiateNpc(GeneratedCharFolder + "/SisterMeredith.prefab", meredithLZPos, "MeredithLZ");
            // Walker lives on a separate STATIC GameObject so Meredith stays visible during the
            // opening dialogue and the waypoints don't move with her (NpcWalker reads wp.position live).
            var meredithWalkerGo = new GameObject("MeredithLZWalker");
            if (meredithLZGo != null)
            {
                var storyNpc = meredithLZGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(storyNpc);
                mSo.FindProperty("displayName").stringValue = "Meredith";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();

                var walker = meredithWalkerGo.AddComponent<NpcWalker>();
                var wSo = new SerializedObject(walker);
                SetObjectRef(wSo, "target", meredithLZGo.transform);
                var wpArray = new Transform[]
                {
                    CreateWaypoint(meredithWalkerGo.transform, "wp_lz", new Vector3(-10f, 0f, 8f)),
                    CreateWaypoint(meredithWalkerGo.transform, "wp_ramp", new Vector3(-10f, 0f, 4f))
                };
                SetObjectRefList(wSo, "waypoints", new List<Object>(wpArray));
                wSo.FindProperty("moveSpeed").floatValue = 1.6f;
                wSo.FindProperty("faceTravel").boolValue = true;
                wSo.ApplyModifiedPropertiesWithoutUndo();
                meredithWalkerGo.SetActive(false);
            }

            // 3 teen decoratives near ramp (StoryNpc only, no walkers).
            var teen1LZPos = new Vector3(-9f, 0f, 3f);
            var teen1LZGo = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc07.prefab", teen1LZPos, "Teen1LZ");
            if (teen1LZGo != null)
            {
                var storyNpc = teen1LZGo.AddComponent<StoryNpc>();
                var t1So = new SerializedObject(storyNpc);
                t1So.FindProperty("displayName").stringValue = "Kael";
                t1So.FindProperty("remote").boolValue = false;
                t1So.ApplyModifiedPropertiesWithoutUndo();
            }

            var teen2LZPos = new Vector3(-11f, 0f, 3.5f);
            var teen2LZGo = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc08.prefab", teen2LZPos, "Teen2LZ");
            if (teen2LZGo != null)
            {
                var storyNpc = teen2LZGo.AddComponent<StoryNpc>();
                var t2So = new SerializedObject(storyNpc);
                t2So.FindProperty("displayName").stringValue = "Lyris";
                t2So.FindProperty("remote").boolValue = false;
                t2So.ApplyModifiedPropertiesWithoutUndo();
            }

            var teen3LZPos = new Vector3(-10f, 0f, 2f);
            var teen3LZGo = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc09.prefab", teen3LZPos, "Teen3LZ");
            if (teen3LZGo != null)
            {
                var storyNpc = teen3LZGo.AddComponent<StoryNpc>();
                var t3So = new SerializedObject(storyNpc);
                t3So.FindProperty("displayName").stringValue = "Venn";
                t3So.FindProperty("remote").boolValue = false;
                t3So.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var architectureDialogue = BuildEp10DialoguePlayer("Dialogue_TheArchitecture", new Vector3(0f, 1.5f, 5f), "the_architecture");
            var architectureDlgSo = new SerializedObject(architectureDialogue);
            architectureDlgSo.FindProperty("playOnStart").boolValue = true;
            architectureDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var lzBarksDialogue = BuildEp10DialoguePlayer("Dialogue_LZBarks", new Vector3(0f, 1.5f, 8f), "lz_barks");

            var lzGoDialogue = BuildEp10DialoguePlayer("Dialogue_LandingZoneGo", new Vector3(0f, 1.5f, 12f), "landing_zone_go");

            // ---- Enemies: Wave 1 (6 heavies in 2 waves of 3) + Wave 2 (4 in 1) ----
            var heavyColor = new Color(0.55f, 0.50f, 0.45f);

            // Wave 1a: 3 heavies.
            var wave1aPositions = new Vector3[]
            {
                new Vector3(-5f, 0f, 10f),
                new Vector3(0f, 0f, 11f),
                new Vector3(5f, 0f, 10f)
            };
            var wave1aHealths = new List<Health>();
            foreach (var pos in wave1aPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, heavyColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave1aHealths.Add(enemy.GetComponent<Health>());
            }

            var wave1aSpawner = BuildWaveSpawner("Wave1aSpawner", new Vector3(0f, 0.5f, 10.5f), 2.5f,
                new List<List<Health>> { wave1aHealths }, new[] { lzBarksDialogue });

            // Wave 1b: 3 more heavies.
            var wave1bPositions = new Vector3[]
            {
                new Vector3(-4f, 0f, 13f),
                new Vector3(1f, 0f, 14f),
                new Vector3(4f, 0f, 13f)
            };
            var wave1bHealths = new List<Health>();
            foreach (var pos in wave1bPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, heavyColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave1bHealths.Add(enemy.GetComponent<Health>());
            }

            var wave1bSpawner = BuildWaveSpawner("Wave1bSpawner", new Vector3(0f, 0.5f, 13.5f), 2.5f,
                new List<List<Health>> { wave1bHealths }, new[] { BuildEp10DialoguePlayer("Dialogue_LZBarks2", new Vector3(0f, 1.5f, 13f), "lz_barks") });

            // Wave 2: 4 enemies.
            var wave2Positions = new Vector3[]
            {
                new Vector3(-3f, 0f, 15f),
                new Vector3(1f, 0f, 15.5f),
                new Vector3(3f, 0f, 16f),
                new Vector3(-1f, 0f, 16.5f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, heavyColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 0.5f, 15.5f), 2.5f,
                new List<List<Health>> { wave2Healths }, new[] { BuildEp10DialoguePlayer("Dialogue_Wave2Barks", new Vector3(0f, 1.5f, 15f), "lz_barks") });

            // Reach point at evacuation pod.
            var evacReachGo = new GameObject("EvacPodReachPoint");
            evacReachGo.transform.position = new Vector3(15f, 0.5f, 0f);

            // Transition box: "LAUNCH — EVAC POD".
            var extractionBoxGo = BuildTransitionBox("ToExtractionBox", new Vector3(15f, 1.2f, 1f), "LAUNCH — EVAC POD",
                out var extractionBtn, out var extractionTransition);
            var etSo = new SerializedObject(extractionTransition);
            etSo.FindProperty("onFootScene").stringValue = Galaxy2Ep10ExtractionSceneName;
            etSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(extractionBtn.onClick,
                new UnityEngine.Events.UnityAction(extractionTransition.LoadOnFootScene));
            extractionBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue the_architecture (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Architecture";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = architectureDialogue;

            // Step 1: Trigger — activate gunship and Meredith walkers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Activate Gunship and Meredith Walkers";
            var activateProp1 = s1.FindPropertyRelative("triggerObjects");
            activateProp1.arraySize = 2;
            activateProp1.GetArrayElementAtIndex(0).objectReferenceValue = gunshipRoot;
            activateProp1.GetArrayElementAtIndex(1).objectReferenceValue = meredithWalkerGo;

            // Step 2: DefeatWaves — Wave 1a (3 heavies).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1a (3 Heavies)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1aSpawner;

            // Step 3: DefeatWaves — Wave 2 (4 enemies).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 2 (4 Enemies)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 4: Dialogue landing_zone_go.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Landing Zone Go";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = lzGoDialogue;

            // Step 5: ReachTrigger — evac pod.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Evac Pod";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = evacReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 6: Prompt — transition to Extraction.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: To Extraction";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = extractionBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10LandingZoneScenePath);
            EnsureScenesInBuild(Galaxy2Ep10LandingZoneScenePath);

            Debug.Log($"[Space Samurai] EP10 Landing Zone scene built at {Galaxy2Ep10LandingZoneScenePath}. " +
                      "Layout: open dusk surface with Kessler's grounded ship (hull + ramp), gunship prop on elevated walker track, evac pod (15u east). " +
                      "Meredith + 3 teens (decorative near ramp). " +
                      "7 steps: the_architecture (auto) → trigger gunship + Meredith walkers → defeat 6 heavies (2 waves of 3) → " +
                      "defeat 4 enemies → landing_zone_go dialogue → reach evac pod → transition to Extraction (space).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Extraction", priority = 121)]
        public static void BuildEp10Extraction()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void with amber Velloch's Reach sphere.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.7f, 0.5f);
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

            // Velloch's Reach: large amber sphere below/behind + sparse lights (unlit).
            var vellochGo = AddUnlitVisual(universe, "Velloch's Reach", new Vector3(-1200f, -600f, -2200f),
                new Vector3(500f, 500f, 500f), PrimitiveType.Sphere, new Color(0.85f, 0.60f, 0.35f));
            var vellochCollider = vellochGo.GetComponent<Collider>();
            if (vellochCollider != null) vellochCollider.isTrigger = true;

            // Sparse lights on Velloch's Reach (unlit glow spots).
            AddUnlitVisual(universe, "VellochLight1", new Vector3(-1100f, -550f, -2100f),
                new Vector3(30f, 30f, 30f), PrimitiveType.Cube, new Color(1f, 0.45f, 0.2f));
            AddUnlitVisual(universe, "VellochLight2", new Vector3(-1300f, -700f, -2300f),
                new Vector3(25f, 25f, 25f), PrimitiveType.Cube, new Color(0.9f, 0.35f, 0.1f));

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
            var picketDialogue = BuildEp10DialoguePlayer("Dialogue_ExtractionPicket", new Vector3(0f, 1.62f, 0.8f),
                "extraction_picket");
            var picketGo = picketDialogue.gameObject;
            picketGo.transform.SetParent(cockpit, false);
            picketGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            picketGo.transform.localRotation = Quaternion.identity;

            // Ascent epilogue (revealed when the fight clears).
            var ascentEpilogueDialogue = BuildEp10DialoguePlayer("Dialogue_AscentEpilogue", new Vector3(0f, 1.62f, 0.8f),
                "ascent_epilogue");
            var ascentGo = ascentEpilogueDialogue.gameObject;
            ascentGo.transform.SetParent(cockpit, false);
            ascentGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ascentGo.transform.localRotation = Quaternion.identity;
            var ascentSo = new SerializedObject(ascentEpilogueDialogue);
            ascentSo.FindProperty("playOnStart").boolValue = true;
            ascentSo.ApplyModifiedPropertiesWithoutUndo();
            ascentGo.SetActive(false); // Initially inactive; EncounterClearedActivator will activate it.

            // ---- Enemy Encounter: 3-ship strike detachment (pursuit-mode) ----
            var picketGo2 = new GameObject("StrikeDetachment");
            var picket = picketGo2.AddComponent<GuardEncounter>();
            var picketSo = new SerializedObject(picket);
            SetObjectRef(picketSo, "player", shipCtrl);
            SetObjectRef(picketSo, "universe", universe);
            SetObjectRef(picketSo, "pool", pool);
            SetObjectRef(picketSo, "definition", enemyShipDef);
            picketSo.FindProperty("shipCount").intValue = 3;
            picketSo.FindProperty("spawnRadius").floatValue = 260f;
            picketSo.FindProperty("initialDelay").floatValue = 5f;
            picketSo.FindProperty("requiredCompletedScene").stringValue = "";
            picketSo.FindProperty("clearedFlag").stringValue = "ep10_picket_cleared";
            SetObjectRef(picketSo, "spawnDialogue", picketDialogue);
            picketSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var picketGateGo = new GameObject("PicketClearedGate");
            var picketGate = picketGateGo.AddComponent<EncounterClearedActivator>();
            var picketGateSo = new SerializedObject(picketGate);
            var activateProp = picketGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = ascentGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnMapBoxGo;
            picketGateSo.FindProperty("clearedFlag").stringValue = "ep10_picket_cleared";
            var extraFlagProp = picketGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep10_complete";
            }
            picketGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10ExtractionScenePath);
            EnsureScenesInBuild(Galaxy2Ep10ExtractionScenePath);

            Debug.Log($"[Space Samurai] EP10 Extraction scene built at {Galaxy2Ep10ExtractionScenePath}. " +
                      "Space over Velloch's Reach (large amber sphere below/behind with sparse lights) + stars. " +
                      "Cockpit with canopy + HUD + ship guns + enemy warning. " +
                      "Flow: extraction_picket spawn (GuardEncounter, 3 ships, 5s delay, pursuit-mode) → " +
                      "on cleared, EncounterClearedActivator plays ascent_epilogue + sets extraFlag 'ep10_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP10 Scenes", priority = 115)]
        public static void BuildAllEp10Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP10 scenes in order: Velloch Surface, Sanctuary, Ruins Duel, Rescue, Landing Zone, Extraction, Galaxy 2...");
            BuildEp10VellochSurface();
            BuildEp10Sanctuary();
            BuildEp10RuinsDuel();
            BuildEp10Rescue();
            BuildEp10LandingZone();
            BuildEp10Extraction();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP10 scenes built successfully! Galaxy 2 hub rebuilt to register EP10 completions.");
        }

        /// <summary>Helper to create a waypoint transform for NpcWalker.</summary>
        private static Transform CreateWaypoint(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }
    }
}

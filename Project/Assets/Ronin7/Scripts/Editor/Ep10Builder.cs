using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
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
    /// EP10 "The Rescue" on-foot scene builders. Builds three core episodes:
    /// - Velloch Surface: dust-scoured ruins with proximity drones and shock mines
    /// - Sanctuary: underground stone sanctuary where Sister Meredith reveals the truth
    /// - Ruins Duel: open surface where the Enforcer challenges Cipher
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP10 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep10" and loads lines from Ep10Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp10DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep10Lines.Get(setId), advanceRef, setId, clipPrefix: "ep10");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Velloch Surface", priority = 116)]
        public static void BuildEp10VellochSurface()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Velloch Surface: dust-scoured ruins — amber key light, orange-brown exponential fog (heavier than surface scenes),
            // pale sky, scattered collapsed-tower/rubble props.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.90f, 0.70f, 0.45f); // amber key light
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.20f, 0.15f); // warm dim tones

            // Heavier dust fog than EP09 surface scenes.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.42f, 0.28f);
            RenderSettings.fogDensity = 0.035f;

            // Amber dust-glow lights.
            BuildAccentPointLight("VellochLight1", new Vector3(-5f, 2f, 10f),
                new Color(1f, 0.65f, 0.35f), intensity: 1.3f, range: 12f);
            BuildAccentPointLight("VellochLight2", new Vector3(4f, 2.5f, 20f),
                new Color(1f, 0.60f, 0.30f), intensity: 1.2f, range: 11f);

            // ---- Velloch Surface: ground plane + scattered rubble ----
            var surfaceGo = new GameObject("VellochSurface");
            var surface = surfaceGo.transform;
            var paleGround = new Color(0.50f, 0.45f, 0.40f);
            var darkRocks = new Color(0.30f, 0.26f, 0.22f);

            // Main floor (x[-8,8], z[0,30]).
            BuildFloorCeiling(surface, "MainFloor", new Vector3(0f, 0f, 15f), new Vector3(16f, 0f, 30f), paleGround, darkRocks);

            // Scattered rubble props (collapsed tower sections, rocks).
            BuildProp(surface, "RubbleTower1", new Vector3(-4f, 1f, 5f), new Vector3(1.2f, 2.5f, 1.2f), darkRocks);
            BuildProp(surface, "RubbleTower2", new Vector3(5f, 0.8f, 8f), new Vector3(1.4f, 2.2f, 1.4f), darkRocks);
            BuildProp(surface, "RubbleRock1", new Vector3(-3f, 0.3f, 18f), new Vector3(0.8f, 0.5f, 1f), darkRocks);
            BuildProp(surface, "RubbleRock2", new Vector3(3f, 0.3f, 22f), new Vector3(0.9f, 0.6f, 0.8f), darkRocks);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds + standard locomotion.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- 6 ShockMines scattered on approach lanes ----
            var shockMinePositions = new Vector3[]
            {
                new Vector3(-4f, 0.1f, 5f),
                new Vector3(2f, 0.1f, 7f),
                new Vector3(-3f, 0.1f, 12f),
                new Vector3(4f, 0.1f, 14f),
                new Vector3(-2f, 0.1f, 20f),
                new Vector3(3f, 0.1f, 24f)
            };
            foreach (var minePos in shockMinePositions)
            {
                var mineGo = new GameObject("ShockMine");
                mineGo.transform.SetParent(surface, false);
                mineGo.transform.position = minePos;

                // Small flattened cylinder visual (barely above ground).
                var mineVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mineVisual.name = "MineVisual";
                Object.DestroyImmediate(mineVisual.GetComponent<Collider>());
                mineVisual.transform.SetParent(mineGo.transform, false);
                mineVisual.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
                TintShared(mineVisual.GetComponent<Renderer>(), new Color(0.35f, 0.30f, 0.28f));

                // Trigger collider (SphereCollider isTrigger).
                var triggerCol = mineGo.AddComponent<SphereCollider>();
                triggerCol.radius = 1.2f;
                triggerCol.isTrigger = true;

                // ShockMine component (damage 15, armDelay 1).
                var shockMine = mineGo.AddComponent<ShockMine>();
                var shockSo = new SerializedObject(shockMine);
                shockSo.FindProperty("damage").floatValue = 15f;
                shockSo.FindProperty("armDelay").floatValue = 1f;

                // Small emissive sphere child as flashObject.
                var flashGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flashGo.name = "FlashEffect";
                Object.DestroyImmediate(flashGo.GetComponent<Collider>());
                flashGo.transform.SetParent(mineGo.transform, false);
                flashGo.transform.localPosition = Vector3.zero;
                flashGo.transform.localScale = Vector3.one * 0.3f;
                flashGo.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.8f, 0.4f));
                flashGo.SetActive(false);

                SetObjectRef(shockSo, "flashObject", flashGo);
                shockSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 9 Proximity Drones: 3 waves of 3 (scaled 0.5, cyan-gray tint, nonLethalDisable) ----
            var droneColor = new Color(0.45f, 0.50f, 0.55f); // cyan-gray

            var dronePositions = new Vector3[]
            {
                // Wave 1 (3 drones)
                new Vector3(-3f, 0f, 6f),
                new Vector3(0f, 0f, 7f),
                new Vector3(3f, 0f, 6.5f),
                // Wave 2 (3 drones)
                new Vector3(-2.5f, 0f, 14f),
                new Vector3(1f, 0f, 15f),
                new Vector3(3f, 0f, 14.5f),
                // Wave 3 (3 drones)
                new Vector3(-2f, 0f, 22f),
                new Vector3(2f, 0f, 23f),
                new Vector3(0f, 0f, 24f)
            };
            var droneWave1Healths = new List<Health>();
            var droneWave2Healths = new List<Health>();
            var droneWave3Healths = new List<Health>();

            for (int i = 0; i < dronePositions.Length; i++)
            {
                var enemy = BuildDominionEnemy(dronePositions[i], playerHealth, enemyDef);
                enemy.gameObject.transform.localScale *= 0.5f;
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

                if (i < 3)
                    droneWave1Healths.Add(enemy.GetComponent<Health>());
                else if (i < 6)
                    droneWave2Healths.Add(enemy.GetComponent<Health>());
                else
                    droneWave3Healths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 14f), 2f,
                new List<List<Health>> { droneWave1Healths, droneWave2Healths, droneWave3Healths },
                new[] { BuildEp10DialoguePlayer("Dialogue_GauntletBarks", new Vector3(0f, 1.5f, 14f), "gauntlet_barks") });

            // Hatch trigger at far end.
            var hatchGo = new GameObject("HatchReachPoint");
            hatchGo.transform.position = new Vector3(0f, 1f, 30f);

            // Transition box: "DESCEND — THE SANCTUARY".
            var sanctuaryBoxGo = BuildTransitionBox("ToSanctuaryBox", new Vector3(0f, 1.2f, 30.5f), "DESCEND — THE SANCTUARY",
                out var sanctuaryBtn, out var sanctuaryTransition);
            var stSo = new SerializedObject(sanctuaryTransition);
            stSo.FindProperty("onFootScene").stringValue = Galaxy2Ep10SanctuarySceneName;
            stSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(sanctuaryBtn.onClick,
                new UnityEngine.Events.UnityAction(sanctuaryTransition.LoadOnFootScene));
            sanctuaryBoxGo.SetActive(false);

            // ---- Dialogue Players ----
            var descentDialogue = BuildEp10DialoguePlayer("Dialogue_DescentContact", new Vector3(0f, 1.5f, 2f), "descent_contact");
            var descentDlgSo = new SerializedObject(descentDialogue);
            descentDlgSo.FindProperty("playOnStart").boolValue = true;
            descentDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue descent_contact (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Descent Contact";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = descentDialogue;

            // Step 1: DefeatWaves — 9 proximity drones (gauntlet_barks spawn dialogue).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Proximity Drones (9)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: ReachTrigger — hatch.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Hatch";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = hatchGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 3: Prompt — transition to Sanctuary.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Sanctuary";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = sanctuaryBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10VellochSurfaceScenePath);
            EnsureScenesInBuild(Galaxy2Ep10VellochSurfaceScenePath, Galaxy2Ep10SanctuaryScenePath);

            Debug.Log($"[Space Samurai] EP10 Velloch Surface scene built at {Galaxy2Ep10VellochSurfaceScenePath}. " +
                      "Layout: dust-scoured surface ruins with amber lighting, heavy exponential fog. " +
                      "6 ShockMines (15 damage, 1s arm delay, emissive flash) on approach lanes. " +
                      "9 proximity drones (3 waves of 3, scaled 0.5, cyan-gray, nonLethal). " +
                      "4 steps: descent_contact (auto) → defeat 9 drones (gauntlet_barks) → reach hatch → " +
                      "transition to Sanctuary.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Sanctuary", priority = 117)]
        public static void BuildEp10Sanctuary()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sanctuary: enclosed stone corridors + main chamber — warm amber + pale-blue emergency strip lights,
            // carved-name walls, children's drawings, cold-sleep lockers, records table, data-core prop.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.70f, 0.55f); // warm amber key
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.14f); // warm dim tones

            // Stone interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.40f, 0.35f, 0.30f);
            RenderSettings.fogDensity = 0.020f;

            // Pale-blue emergency strip lights.
            BuildAccentPointLight("StripLight1", new Vector3(-4f, 2.8f, 8f),
                new Color(0.6f, 0.8f, 0.95f), intensity: 1f, range: 9f);
            BuildAccentPointLight("StripLight2", new Vector3(4f, 2.8f, 14f),
                new Color(0.6f, 0.8f, 0.95f), intensity: 1f, range: 9f);

            // ---- Sanctuary interior: stone corridors + main chamber ----
            var sanctuaryGo = new GameObject("SanctuaryInterior");
            var sanctuary = sanctuaryGo.transform;
            var stoneGray = new Color(0.45f, 0.42f, 0.40f);
            var darkStone = new Color(0.28f, 0.26f, 0.24f);

            // Entrance corridor (x[-3,3], z[0,5]).
            BuildFloorCeiling(sanctuary, "EntranceFloor", new Vector3(0f, 0f, 2.5f), new Vector3(6f, 0f, 5f), stoneGray, darkStone);
            BuildWall(sanctuary, "EntranceWall_W", new Vector3(-3f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 5f));
            BuildWall(sanctuary, "EntranceWall_E", new Vector3(3f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 5f));

            // Main chamber (x[-5,5], z[5,16]).
            BuildFloorCeiling(sanctuary, "ChamberFloor", new Vector3(0f, 0f, 10.5f), new Vector3(10f, 0f, 11f), stoneGray, darkStone);
            BuildWall(sanctuary, "ChamberWall_W", new Vector3(-5f, 1.5f, 10.5f), new Vector3(0.2f, 3f, 11f));
            BuildWall(sanctuary, "ChamberWall_E", new Vector3(5f, 1.5f, 10.5f), new Vector3(0.2f, 3f, 11f));

            // Records room rear (x[-4,4], z[16,22]).
            BuildFloorCeiling(sanctuary, "RecordsFloor", new Vector3(0f, 0f, 19f), new Vector3(8f, 0f, 6f), stoneGray, darkStone);
            BuildWall(sanctuary, "RecordsWall_W", new Vector3(-4f, 1.5f, 19f), new Vector3(0.2f, 3f, 6f));
            BuildWall(sanctuary, "RecordsWall_E", new Vector3(4f, 1.5f, 19f), new Vector3(0.2f, 3f, 6f));
            BuildWall(sanctuary, "RecordsWall_Back", new Vector3(0f, 1.5f, 22f), new Vector3(8f, 3f, 0.2f));

            // Carved-name wall prop (decorative quads).
            BuildProp(sanctuary, "CarvedNames", new Vector3(0f, 1.8f, 15f), new Vector3(6f, 1.5f, 0.1f), new Color(0.35f, 0.32f, 0.30f));

            // Children's-drawing tinted props.
            BuildProp(sanctuary, "DrawingQuad1", new Vector3(-3.5f, 1f, 8f), new Vector3(0.8f, 0.8f, 0.05f), new Color(0.85f, 0.70f, 0.50f));
            BuildProp(sanctuary, "DrawingQuad2", new Vector3(3.5f, 1f, 12f), new Vector3(0.8f, 0.8f, 0.05f), new Color(0.75f, 0.65f, 0.45f));

            // Cold-sleep locker boxes (stacked, decorative).
            BuildProp(sanctuary, "LockerStack", new Vector3(-4f, 1f, 10f), new Vector3(0.6f, 1.5f, 0.6f), new Color(0.30f, 0.35f, 0.40f));

            // Records table.
            BuildProp(sanctuary, "RecordsTable", new Vector3(0f, 0.7f, 18f), new Vector3(2f, 0.3f, 1.5f), new Color(0.35f, 0.30f, 0.28f));

            // Data-core prop: glowing cylinder in records room.
            var coreProp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coreProp.name = "DataCore";
            Object.DestroyImmediate(coreProp.GetComponent<Collider>());
            coreProp.transform.SetParent(sanctuary, false);
            coreProp.transform.position = new Vector3(0f, 1.5f, 20f);
            coreProp.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
            coreProp.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.4f, 0.8f, 1f));

            // Chokepoint corridor section (narrow, x[-2,2], z[5.5,7]).
            BuildWall(sanctuary, "ChokepointWall_W", new Vector3(-2f, 1.5f, 6.25f), new Vector3(0.2f, 3f, 1.5f));
            BuildWall(sanctuary, "ChokepointWall_E", new Vector3(2f, 1.5f, 6.25f), new Vector3(0.2f, 3f, 1.5f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 40f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- NPCs ----
            // Sister Meredith: AllyCombatant, NO Health.
            var meredithPos = new Vector3(-1f, 0f, 10f);
            var meredithGo = InstantiateNpc(GeneratedCharFolder + "/SisterMeredith.prefab", meredithPos, "SisterMeredith");
            if (meredithGo != null)
            {
                var allyCombatant = meredithGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = meredithGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(storyNpc);
                mSo.FindProperty("displayName").stringValue = "Sister Meredith";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler: StoryNpc.
            var kesslerPos = new Vector3(2f, 0f, 9f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Enemies: Two wave spawners ----
            var trooperColor = new Color(0.55f, 0.50f, 0.48f); // grey trooper

            // Spawner 1: 6 troopers (2 waves of 3).
            var spawner1Wave1Positions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2.5f, 0f, 8f)
            };
            var spawner1Wave2Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 11f),
                new Vector3(1f, 0f, 11.5f),
                new Vector3(2f, 0f, 11f)
            };
            var spawner1Wave1Healths = new List<Health>();
            var spawner1Wave2Healths = new List<Health>();

            foreach (var pos in spawner1Wave1Positions)
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
                spawner1Wave1Healths.Add(enemy.GetComponent<Health>());
            }

            foreach (var pos in spawner1Wave2Positions)
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
                spawner1Wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var spawner1 = BuildWaveSpawner("Spawner1", new Vector3(0f, 0.5f, 9.5f), 2f,
                new List<List<Health>> { spawner1Wave1Healths, spawner1Wave2Healths },
                new[] { BuildEp10DialoguePlayer("Dialogue_FirstBreach", new Vector3(0f, 1.5f, 9f), "first_breach") });

            // Spawner 2: 6 troopers (3 waves of 2) at chokepoint.
            var spawner2Wave1Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6.5f),
                new Vector3(1.5f, 0f, 6.5f)
            };
            var spawner2Wave2Positions = new Vector3[]
            {
                new Vector3(-1f, 0f, 7f),
                new Vector3(1f, 0f, 7f)
            };
            var spawner2Wave3Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 7.5f),
                new Vector3(1.5f, 0f, 7.5f)
            };
            var spawner2Wave1Healths = new List<Health>();
            var spawner2Wave2Healths = new List<Health>();
            var spawner2Wave3Healths = new List<Health>();

            foreach (var pos in spawner2Wave1Positions)
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
                spawner2Wave1Healths.Add(enemy.GetComponent<Health>());
            }

            foreach (var pos in spawner2Wave2Positions)
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
                spawner2Wave2Healths.Add(enemy.GetComponent<Health>());
            }

            foreach (var pos in spawner2Wave3Positions)
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
                spawner2Wave3Healths.Add(enemy.GetComponent<Health>());
            }

            var spawner2 = BuildWaveSpawner("Spawner2", new Vector3(0f, 0.5f, 6.75f), 1.5f,
                new List<List<Health>> { spawner2Wave1Healths, spawner2Wave2Healths, spawner2Wave3Healths },
                new[] { BuildEp10DialoguePlayer("Dialogue_ChokepointBarks", new Vector3(0f, 1.5f, 6.5f), "chokepoint_barks") });

            // ---- Dialogue Players ----
            var thresholdDialogue = BuildEp10DialoguePlayer("Dialogue_SanctuaryThreshold", new Vector3(0f, 1.5f, 3f), "sanctuary_threshold");
            var thresholdDlgSo = new SerializedObject(thresholdDialogue);
            thresholdDlgSo.FindProperty("playOnStart").boolValue = true;
            thresholdDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var kaelVorDialogue = BuildEp10DialoguePlayer("Dialogue_KaelVor", new Vector3(0f, 1.5f, 10f), "kael_vor");
            var theFileDialogue = BuildEp10DialoguePlayer("Dialogue_TheFile", new Vector3(0f, 1.5f, 14f), "the_file");
            var directiveSevenDialogue = BuildEp10DialoguePlayer("Dialogue_DirectiveSeven", new Vector3(0f, 1.5f, 20f), "directive_seven");

            // Transition box: "TOPSIDE — CUT THEM OFF".
            var ruinsDuelBoxGo = BuildTransitionBox("ToRuinsDuelBox", new Vector3(0f, 1.2f, 21f), "TOPSIDE — CUT THEM OFF",
                out var ruinsDuelBtn, out var ruinsDuelTransition);
            var rdtSo = new SerializedObject(ruinsDuelTransition);
            rdtSo.FindProperty("onFootScene").stringValue = Galaxy2Ep10RuinsDuelSceneName;
            rdtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(ruinsDuelBtn.onClick,
                new UnityEngine.Events.UnityAction(ruinsDuelTransition.LoadOnFootScene));
            ruinsDuelBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue sanctuary_threshold (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Sanctuary Threshold";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = thresholdDialogue;

            // Step 1: Dialogue kael_vor.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Kael Vor";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = kaelVorDialogue;

            // Step 2: DefeatWaves spawner 1 (first_breach).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Spawner 1 (6 Troopers)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = spawner1;

            // Step 3: Dialogue the_file.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The File";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = theFileDialogue;

            // Step 4: DefeatWaves spawner 2 (chokepoint_barks).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s4.FindPropertyRelative("label").stringValue = "DefeatWaves: Spawner 2 (6 Troopers, Chokepoint)";
            s4.FindPropertyRelative("waveSpawner").objectReferenceValue = spawner2;

            // Step 5: Dialogue directive_seven (at data-core).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Directive Seven";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = directiveSevenDialogue;

            // Step 6: Prompt — topside to Ruins Duel.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Topside to Ruins Duel";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = ruinsDuelBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10SanctuaryScenePath);
            EnsureScenesInBuild(Galaxy2Ep10SanctuaryScenePath, Galaxy2Ep10RuinsDuelScenePath);

            Debug.Log($"[Space Samurai] EP10 Sanctuary scene built at {Galaxy2Ep10SanctuaryScenePath}. " +
                      "Layout: underground stone sanctuary corridors + main chamber (carved names, children drawings, cold-sleep lockers, records table, data-core). " +
                      "Warm amber + pale-blue emergency lights. Sister Meredith ally (AllyCombatant, NO Health). Kessler StoryNpc. " +
                      "7 steps: sanctuary_threshold (auto) → kael_vor dialogue → defeat wave 1 (6 troopers, first_breach) → " +
                      "the_file dialogue → defeat wave 2 (6 troopers, chokepoint_barks) → directive_seven dialogue → " +
                      "transition to Ruins Duel.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP10 Ruins Duel", priority = 118)]
        public static void BuildEp10RuinsDuel()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var enforcerDef = EnsureEp10EnforcerDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ruins Duel: open surface ruins, twilight sky — calm, tense atmosphere.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.70f, 0.65f, 0.75f); // cool twilight
            light.intensity = 0.70f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -45f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.18f, 0.22f); // cool dim

            // Light twilight fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.33f, 0.40f);
            RenderSettings.fogDensity = 0.018f;

            // Subtle twilight accents.
            BuildAccentPointLight("TwilightLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.55f, 0.65f, 0.85f), intensity: 0.9f, range: 10f);
            BuildAccentPointLight("TwilightLight2", new Vector3(3f, 2f, 12f),
                new Color(0.50f, 0.60f, 0.80f), intensity: 0.8f, range: 9f);

            // ---- Ruins Duel: open ground + duel circle ----
            var ruinsGo = new GameObject("RuinsDuelArea");
            var ruins = ruinsGo.transform;
            var dustyGround = new Color(0.48f, 0.44f, 0.42f);
            var darkRuin = new Color(0.32f, 0.28f, 0.26f);

            // Main floor (x[-8,8], z[0,16]).
            BuildFloorCeiling(ruins, "DuelFloor", new Vector3(0f, 0f, 8f), new Vector3(16f, 0f, 16f), dustyGround, darkRuin);

            // Scattered ruin props (towers, collapsed sections).
            BuildProp(ruins, "RuinTower1", new Vector3(-5f, 1f, 3f), new Vector3(1f, 2f, 1f), darkRuin);
            BuildProp(ruins, "RuinTower2", new Vector3(5f, 0.9f, 12f), new Vector3(1.2f, 2.3f, 1.2f), darkRuin);
            BuildProp(ruins, "RuinDebris1", new Vector3(-3f, 0.3f, 10f), new Vector3(0.7f, 0.5f, 0.8f), darkRuin);

            // Duel circle (slight visual marking).
            BuildProp(ruins, "DuelCircle", new Vector3(0f, 0.05f, 8f), new Vector3(6f, 0.1f, 6f), new Color(0.35f, 0.30f, 0.28f));

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

            // ---- Dialogue Players ----
            var flankDialogue = BuildEp10DialoguePlayer("Dialogue_FlankWarning", new Vector3(0f, 1.5f, 4f), "flank_warning");
            var flankDlgSo = new SerializedObject(flankDialogue);
            flankDlgSo.FindProperty("playOnStart").boolValue = true;
            flankDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var patternBrokenDialogue = BuildEp10DialoguePlayer("Dialogue_PatternBroken", new Vector3(0f, 1.5f, 9f), "pattern_broken");

            // ---- 4 Soldiers: 2 waves of 2 ----
            var soldierColor = new Color(0.55f, 0.50f, 0.48f); // trooper grey

            var soldierWave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 6f),
                new Vector3(2f, 0f, 6.5f)
            };
            var soldierWave2Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 10f),
                new Vector3(1.5f, 0f, 10.5f)
            };
            var soldierWave1Healths = new List<Health>();
            var soldierWave2Healths = new List<Health>();

            foreach (var pos in soldierWave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, soldierColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                soldierWave1Healths.Add(enemy.GetComponent<Health>());
            }

            foreach (var pos in soldierWave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, soldierColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                soldierWave2Healths.Add(enemy.GetComponent<Health>());
            }

            var soldierSpawner = BuildWaveSpawner("SoldierSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { soldierWave1Healths, soldierWave2Healths },
                new[] { BuildEp10DialoguePlayer("Dialogue_SoldierBarks", new Vector3(0f, 1.5f, 8f), "") });

            // ---- THE ENFORCER: Elite pre-placed duel opponent ----
            var enforcerPos = new Vector3(0f, 0f, 12f);
            var enforcer = BuildDominionEnemy(enforcerPos, playerHealth, enforcerDef);
            enforcer.gameObject.transform.localScale *= 1.15f; // Slightly larger.

            // Dark tint with red accents.
            var enforcerRenderer = enforcer.GetComponent<Renderer>();
            if (enforcerRenderer != null) TintShared(enforcerRenderer, new Color(0.20f, 0.18f, 0.16f));

            // PatternedDuelist component (defaults fine).
            var patternedDuelist = enforcer.gameObject.AddComponent<PatternedDuelist>();
            var pdSo = new SerializedObject(patternedDuelist);
            pdSo.ApplyModifiedPropertiesWithoutUndo();

            var enforcerMeleeAttacker = enforcer.GetComponent<MeleeAttacker>();
            if (enforcerMeleeAttacker != null)
            {
                var maSo = new SerializedObject(enforcerMeleeAttacker);
                maSo.FindProperty("nonLethalDisable").boolValue = false;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Enforcer starts ACTIVE.
            enforcer.gameObject.SetActive(true);

            var enforcerHealth = enforcer.GetComponent<Health>();

            // Transition box: "BELOW — THE SEALED CHAMBER".
            var rescueBoxGo = BuildTransitionBox("ToRescueBox", new Vector3(0f, 1.2f, 15f), "BELOW — THE SEALED CHAMBER",
                out var rescueBtn, out var rescueTransition);
            var rboxSo = new SerializedObject(rescueTransition);
            rboxSo.FindProperty("onFootScene").stringValue = Galaxy2Ep10RescueSceneName;
            rboxSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(rescueBtn.onClick,
                new UnityEngine.Events.UnityAction(rescueTransition.LoadOnFootScene));
            rescueBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue flank_warning (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Flank Warning";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = flankDialogue;

            // Step 1: DefeatWaves — 4 soldiers (2 waves of 2).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Soldiers (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = soldierSpawner;

            // Step 2: DefeatEnemies — The Enforcer.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatEnemies;
            s2.FindPropertyRelative("label").stringValue = "DefeatEnemies: The Enforcer";
            var enemiesProp = s2.FindPropertyRelative("enemies");
            enemiesProp.arraySize = 1;
            enemiesProp.GetArrayElementAtIndex(0).objectReferenceValue = enforcerHealth;

            // Step 3: Dialogue pattern_broken.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Pattern Broken";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = patternBrokenDialogue;

            // Step 4: Prompt — transition to Rescue.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Below to Sealed Chamber";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = rescueBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep10RuinsDuelScenePath);
            EnsureScenesInBuild(Galaxy2Ep10RuinsDuelScenePath, Galaxy2Ep10RescueScenePath);

            Debug.Log($"[Space Samurai] EP10 Ruins Duel scene built at {Galaxy2Ep10RuinsDuelScenePath}. " +
                      "Layout: open surface ruins with twilight sky, cool lighting, duel circle. " +
                      "4 soldiers (2 waves of 2, nonLethal). The Enforcer elite (160 HP, PatternedDuelist, 1.15 scale, dark tint). " +
                      "5 steps: flank_warning (auto) → defeat 4 soldiers → defeat Enforcer → pattern_broken dialogue → " +
                      "transition to Sealed Chamber.");
        }

        /// <summary>Elite EnemyDefinition for EP10 The Enforcer duel (~160 HP).</summary>
        private static EnemyDefinition EnsureEp10EnforcerDefinition()
        {
            const string path = DataFolder + "/Ep10Enforcer.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 160f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }
    }
}

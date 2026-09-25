using System.Collections.Generic;
using Ronin7.Audio;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
// Disambiguate from UnityEditor.SettingsService (the Editor namespace defines a type of the same name).
using SettingsService = Ronin7.Audio.SettingsService;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// One-click construction of the player XR rig and a Phase 0 smoke-test scene so the
    /// human's only Editor chore is enabling OpenXR in Project Settings and pressing Play.
    /// Everything here is data-wired against Assets/Ronin7/Settings/Ronin7Input.inputactions.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string InputAssetPath = "Assets/Ronin7/Settings/Ronin7Input.inputactions";
        private const string PrefabFolder = "Assets/Ronin7/Prefabs";
        private const string PrefabPath = PrefabFolder + "/XR Rig.prefab";
        private const string SceneFolder = "Assets/Ronin7/Scenes";
        private const string ZoneScenePath = SceneFolder + "/Phase4_Zone.unity";
        private const string SpaceCombatScenePath = SceneFolder + "/Phase7_SpaceCombat.unity";
        private const string BootScenePath = SceneFolder + "/Phase6_Boot.unity";
        private const string OnFootSceneName = "Phase4_Zone";
        // Derived from the path's filename so the name used at runtime (LoadSceneAsync) can never
        // drift from the asset the builder actually writes.
        private static readonly string SpaceCombatSceneName =
            System.IO.Path.GetFileNameWithoutExtension(SpaceCombatScenePath);
        private const string DataFolder = "Assets/Ronin7/Data";
        private const string WeaponPath = DataFolder + "/Katana.asset";
        private const string EnemyDefPath = DataFolder + "/Bandit.asset";
        private const string ZoneDefPath = DataFolder + "/Zone_Alpha.asset";
        private const string ShipWeaponPath = DataFolder + "/ShipCannon.asset";
        private const string EnemyShipDefPath = DataFolder + "/Interceptor.asset";
        private const string MaterialFolder = "Assets/Ronin7/Art/Materials";
        private const string SpaceSkyboxPath = MaterialFolder + "/SpaceBlackSkybox.mat";
        private const string NeonSkyboxPath = MaterialFolder + "/SpaceNeonNebulaSkybox.mat";

        // Art prefab paths — Agents 1, 3, 4, 5, 6 own the actual prefabs at these paths.
        // TODO(Agent 1): pick the exact planet variant (A/B/C) used per scene.
        private const string PlanetPrefabPath = "Assets/Ronin7/Prefabs/Art/Planet_VariantA.prefab";
        private const string EnemyShipPrefabPath = "Assets/Ronin7/Prefabs/Art/EnemyShip.prefab";
        private const string EnemyFootPrefabPath = "Assets/Ronin7/Prefabs/Art/EnemyFoot.prefab";
        private const string CockpitPrefabPath = "Assets/Ronin7/Prefabs/Art/Cockpit.prefab";
        private const string HandLeftPrefabPath = "Assets/Ronin7/Prefabs/Art/Hand_L.prefab";
        private const string HandRightPrefabPath = "Assets/Ronin7/Prefabs/Art/Hand_R.prefab";
        private const string SwordPrefabPath = "Assets/Ronin7/Prefabs/Art/Sword_Katana.prefab";
        private const string AsteroidPrefabPath = "Assets/Ronin7/Prefabs/Art/Asteroid.prefab";
        private const string WormholePrefabPath = "Assets/Ronin7/Prefabs/Art/Wormhole.prefab";

        [MenuItem("Tools/Space Samurai/Build XR Rig Prefab", priority = 0)]
        public static void BuildRigPrefab()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            GameObject root = BuildRig(refs);

            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[Space Samurai] XR Rig prefab built at {PrefabPath}");
        }

        [MenuItem("Tools/Space Samurai/Build Phase 4 Zone Scene", priority = 4)]
        public static void BuildZoneScene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var zoneDef = EnsureZoneDefinition();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(zoneDef.radius * 0.5f, 1f, zoneDef.radius * 0.5f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            PopulateZoneGameplay(refs, weapon, enemyDef, zoneDef);

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, ZoneScenePath);
            Debug.Log($"[Space Samurai] Phase 4 zone built at {ZoneScenePath}. " +
                      "Grab the sword, defeat the enemies, collect the relics, then stand on the (green) pad.");
        }

        /// <summary>
        /// Builds the shared on-foot zone gameplay into the active scene: Game state, the player rig
        /// with ZoneBounds, the sword, the boundary, the enemy + relic rings, the extraction pad, and
        /// the wired ZoneController. The plain Phase 4 zone and every themed Galaxy 1 zone differ only
        /// in visuals (floor/light/sky/props) and save path — they share this identical recipe.
        /// </summary>
        private static void PopulateZoneGameplay(Object[] refs, WeaponDefinition weapon,
            EnemyDefinition enemyDef, ZoneDefinition zoneDef)
        {
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            var rig = BuildRig(refs);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = zoneDef.radius;

            BuildSword(new Vector3(-0.4f, 1.0f, 0.5f), weapon);
            BuildBoundary(zoneDef.radius);

            var enemyObjs = new List<Object>();
            for (int i = 0; i < zoneDef.enemyCount; i++)
            {
                float a = (i / Mathf.Max(1f, zoneDef.enemyCount)) * Mathf.PI * 2f;
                float r = zoneDef.radius * 0.6f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                enemyObjs.Add(BuildEnemy(pos, playerHealth, enemyDef));
            }

            var relicObjs = new List<Object>();
            for (int i = 0; i < zoneDef.relicCount; i++)
            {
                float a = (i / Mathf.Max(1f, zoneDef.relicCount)) * Mathf.PI * 2f + 0.6f;
                float r = zoneDef.radius * 0.4f;
                var pos = new Vector3(Mathf.Cos(a) * r, 1.0f, Mathf.Sin(a) * r);
                relicObjs.Add(BuildPickup(pos));
            }

            var extraction = BuildExtraction(Vector3.zero);

            var zoneGo = new GameObject("Zone");
            var zc = zoneGo.AddComponent<ZoneController>();
            var zcSo = new SerializedObject(zc);
            SetObjectRef(zcSo, "definition", zoneDef);
            SetObjectRef(zcSo, "extraction", extraction);
            SetObjectRefList(zcSo, "enemies", enemyObjs);
            SetObjectRefList(zcSo, "relics", relicObjs);
            zcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Space Samurai/Build Phase 7 Space Combat Scene", priority = 7)]
        public static void BuildSpaceCombatScene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space is a black void: kill fog, drop ambient to a faint cool fill so unlit faces
            // aren't pure black, and swap the default gradient sky for a solid-black skybox.
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

            // Seated flight rig: stationary at the origin, NO locomotion (no walking/gravity), exactly
            // like the Phase 5 flight scene. The universe moves around it; the rig never moves.
            var rig = BuildRig(refs, addLocomotion: false);

            // Player ship Health so enemy bolts can hurt the samurai's craft, and a HUD/event bridge
            // can react to PlayerShipDamaged. The rig already has a Health (added in BuildRig); ensure it.
            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            // A hull trigger collider at the origin so enemy bolts (which converge on the cockpit at
            // the origin in the moving-universe frame) actually strike SOMETHING and damage the ship
            // Health. The seated flight rig has no CharacterController, so without this the player
            // would be invulnerable. It's a child of the rig root, so Projectile.TryHit's
            // GetComponentInParent<IDamageable>() resolves to the rig's Health.
            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false; // solid so the bolt's SphereCast (which ignores triggers) hits it

            var cockpit = new GameObject("Cockpit").transform;
            BuildCockpit(cockpit);

            // Long-press right-A re-aligns the cockpit with the current head pose so the ship
            // front follows where the player is looking when the seated rig drifts.
            var vrRig = rig.GetComponent<VRRig>();
            var recenter = cockpit.gameObject.AddComponent<CockpitRecenter>();
            var rcSo = new SerializedObject(recenter);
            SetObjectRef(rcSo, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
            SetObjectRef(rcSo, "head", vrRig != null ? vrRig.Head : null);
            SetObjectRef(rcSo, "cockpit", cockpit);
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            // The moving world. A couple of planets for parallax/orientation so dogfighting reads.
            // The first planet is the landable one (used by the LandingApproach below).
            var universe = new GameObject("Universe").transform;
            // A visible, radiant sun far out in the universe frame, with the "Sun" directional light
            // reoriented to shine from it toward the origin (so lit faces face the rendered disc).
            BuildSun(universe, light);
            var landingPlanet = BuildPlanet(universe, new Vector3(220f, 30f, 600f), 80f, new Color(0.5f, 0.55f, 0.7f));
            BuildPlanet(universe, new Vector3(-360f, -50f, 800f), 130f, new Color(0.7f, 0.45f, 0.3f));
            BuildPlanet(universe, new Vector3(900f, 120f, -200f), 90f, new Color(0.55f, 0.6f, 0.45f));
            BuildPlanet(universe, new Vector3(-700f, 80f, -500f), 70f, new Color(0.6f, 0.5f, 0.4f));
            BuildPlanet(universe, new Vector3(450f, -180f, -900f), 110f, new Color(0.45f, 0.5f, 0.65f));
            BuildPlanet(universe, new Vector3(-1100f, 160f, 350f), 140f, new Color(0.65f, 0.4f, 0.35f));
            BuildPlanet(universe, new Vector3(50f, 200f, -1200f), 95f, new Color(0.5f, 0.6f, 0.55f));
            BuildPlanet(universe, new Vector3(-250f, -160f, 1100f), 60f, new Color(0.7f, 0.55f, 0.4f));
            BuildPlanet(universe, new Vector3(1300f, -40f, 400f), 120f, new Color(0.4f, 0.55f, 0.6f));
            BuildPlanet(universe, new Vector3(-900f, -120f, -800f), 80f, new Color(0.55f, 0.45f, 0.55f));
            BuildPlanet(universe, new Vector3(600f, 180f, 1000f), 100f, new Color(0.5f, 0.65f, 0.5f));
            BuildPlanet(universe, new Vector3(-50f, -200f, 500f), 45f, new Color(0.6f, 0.45f, 0.3f));
            BuildAsteroidField(universe, 39);

            // Phase 2 hazard manager: ONE scene-wide pass that makes the belts deal collision damage
            // to BOTH the player hull (at the origin) and enemy ships, with brief per-victim cooldowns.
            var hazardGo = new GameObject("Asteroid Hazard");
            var hazard = hazardGo.AddComponent<AsteroidHazard>();
            hazard.Configure(rig.GetComponent<Health>(), hullCol.radius);

            // Flight controller, wired identically to the Phase 5 scene (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var shipSo = new SerializedObject(shipCtrl);
            SetObjectRef(shipSo, "universe", universe);
            SetObjectRef(shipSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(shipSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            shipSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool (player + all enemies draw from this one bounded pool).
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns mounted on the cockpit. Twin muzzles flank the canopy and point forward (+Z),
            // i.e. into real/camera space — bolts fly out and hit the rendered enemy ships directly.
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
            SetObjectRef(gunsSo, "ownerRoot", rig); // player ship root → bolts skip our own hull
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCockpitCrosshair(cockpit, guns);

            // Encounter spawner: warps in waves of fighters around the player's virtual position.
            var encGo = new GameObject("Space Encounter Manager");
            var enc = encGo.AddComponent<SpaceEncounterManager>();
            var encSo = new SerializedObject(enc);
            SetObjectRef(encSo, "player", shipCtrl);
            SetObjectRef(encSo, "universe", universe);
            SetObjectRef(encSo, "pool", pool);
            SetObjectRef(encSo, "definition", enemyShipDef);
            // Art-pass: hand the spawner the EnemyShip art prefab so runtime waves use the
            // painted fighter instead of the inline grey-box. Falls back to grey-box silently
            // if the prefab is missing (e.g., before Build All Art Prefabs has been run).
            var enemyShipPrefab = AssetDatabase.LoadAssetAtPath<EnemyShip>(EnemyShipPrefabPath);
            if (enemyShipPrefab != null) SetObjectRef(encSo, "enemyPrefab", enemyShipPrefab);
            encSo.ApplyModifiedPropertiesWithoutUndo();

            // A few pre-placed grey-box enemy ships under the universe so the scene shows combatants
            // immediately (the spawner adds more waves over time). These sit out at engagement range.
            BuildEnemyShip(universe, new Vector3(60f, 10f, 180f), enemyShipDef, pool, shipCtrl);
            BuildEnemyShip(universe, new Vector3(-90f, -15f, 200f), enemyShipDef, pool, shipCtrl);
            BuildEnemyShip(universe, new Vector3(20f, 40f, 240f), enemyShipDef, pool, shipCtrl);

            // Landing trigger: once the player clears the hostiles and flies slowly into the landable
            // planet, hold grip to drop into the on-foot zone. LandingApproach gates on HostilesPresent()
            // so it stays silent during combat ("Clear hostiles to land" prompt).
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            var landables = lso.FindProperty("landables");
            if (landables != null && landingPlanet != null)
            {
                landables.arraySize = 1;
                var entry = landables.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("target").objectReferenceValue = landingPlanet.transform;
                entry.FindPropertyRelative("approachRadius").floatValue = 120f;
                entry.FindPropertyRelative("destinationScene").stringValue = OnFootSceneName;
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Phase 3 wormholes: paired portals that instantly relocate the virtual ship across the
            // universe (no scene load) with a comfort fade. Two pairs link the near combat area to far
            // regions out among the planets so the jumps are dramatic.
            BuildWormholePair(universe, shipCtrl, new Vector3(0f, 0f, 120f), new Vector3(-1100f, 160f, 350f));
            BuildWormholePair(universe, shipCtrl, new Vector3(200f, 60f, -150f), new Vector3(600f, 180f, 1000f));

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, SpaceCombatScenePath);
            // Register the on-foot zone too so LandingRequested → Phase4_Zone resolves at runtime.
            EnsureScenesInBuild(SpaceCombatScenePath, ZoneScenePath);
            Debug.Log($"[Space Samurai] Phase 7 SPACE COMBAT scene built at {SpaceCombatScenePath}. " +
                      "Left stick = throttle/roll, right stick = pitch/yaw, RIGHT trigger (Activate) = fire, " +
                      "RIGHT grip (Select) over the lead planet = land after clearing hostiles. " +
                      "NOTE: rebuilding re-nulls InputActionReferences (throttle/steer/fire/land) — verify them " +
                      "on Flight Controller, Ship Guns, and Landing Approach in the Inspector before Play.");
        }

        /// <summary>
        /// Adds a <see cref="LandingApproach"/> to the CURRENTLY OPEN Phase 7 scene without rebuilding it
        /// (which would re-null every input ref). Finds the existing ShipController, Universe, Cockpit, and
        /// the first child of Universe whose name starts with "Planet" to use as the landable. Run with
        /// Phase7_SpaceCombat open.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Add Landing Approach To Open Scene", priority = 9)]
        public static void AddLandingApproachToOpenScene()
        {
            if (Object.FindAnyObjectByType<LandingApproach>() != null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "The open scene already has a LandingApproach. Remove it first if you want to rewire.", "OK");
                return;
            }

            var shipCtrl = Object.FindAnyObjectByType<ShipController>();
            if (shipCtrl == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No ShipController in the open scene. Open Phase7_SpaceCombat first.", "OK");
                return;
            }
            var universeGo = GameObject.Find("Universe");
            if (universeGo == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No GameObject named 'Universe' in the open scene.", "OK");
                return;
            }
            var cockpitGo = GameObject.Find("Cockpit");
            if (cockpitGo == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No GameObject named 'Cockpit' in the open scene.", "OK");
                return;
            }

            // Pick the first child of Universe whose name starts with "Planet" as the landable.
            Transform landable = null;
            foreach (Transform child in universeGo.transform)
            {
                if (child.name.StartsWith("Planet", System.StringComparison.OrdinalIgnoreCase))
                {
                    landable = child;
                    break;
                }
            }
            if (landable == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No child of 'Universe' named 'Planet…' found. Rebuild the Phase 7 scene or add a planet manually.", "OK");
                return;
            }

            if (!TryLoadInputRefs(out var refs)) return;

            var prompt = BuildLandingPrompt(cockpitGo.transform);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universeGo.transform);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 1;
                var entry = landables.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("target").objectReferenceValue = landable;
                entry.FindPropertyRelative("approachRadius").floatValue = 120f;
                entry.FindPropertyRelative("destinationScene").stringValue = OnFootSceneName;
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(landingGo.scene);
            Debug.Log($"[Space Samurai] Added LandingApproach wired to '{landable.name}' (approachRadius 120, " +
                      $"destination '{OnFootSceneName}'). Save the scene to keep this. After clearing hostiles, " +
                      "fly slowly into the planet and HOLD right grip to land.");
        }

        /// <summary>
        /// Registers every direct child of <c>Universe</c> whose name starts with "Planet" as a
        /// landable on the open scene's <see cref="LandingApproach"/>. Idempotent — already-registered
        /// planets are skipped, so re-running never creates duplicates. Run with Phase7_SpaceCombat open.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Register All Planets As Landable", priority = 10)]
        public static void RegisterAllPlanetsAsLandable()
        {
            var landing = Object.FindAnyObjectByType<LandingApproach>();
            if (landing == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No LandingApproach in the open scene. Open Phase7_SpaceCombat first.", "OK");
                return;
            }
            var universeGo = GameObject.Find("Universe");
            if (universeGo == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No GameObject named 'Universe' in the open scene.", "OK");
                return;
            }

            var lso = new SerializedObject(landing);
            var landables = lso.FindProperty("landables");
            int added = 0;
            int skipped = 0;
            foreach (Transform child in universeGo.transform)
            {
                if (!child.name.StartsWith("Planet", System.StringComparison.OrdinalIgnoreCase)) continue;

                bool already = false;
                for (int i = 0; i < landables.arraySize; i++)
                {
                    var existing = landables.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("target").objectReferenceValue as Transform;
                    if (existing == child) { already = true; break; }
                }
                if (already) { skipped++; continue; }

                landables.arraySize++;
                var entry = landables.GetArrayElementAtIndex(landables.arraySize - 1);
                entry.FindPropertyRelative("target").objectReferenceValue = child;
                entry.FindPropertyRelative("approachRadius").floatValue = Mathf.Max(child.localScale.x * 0.75f, 80f);
                entry.FindPropertyRelative("destinationScene").stringValue = OnFootSceneName;
                added++;
            }
            lso.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(landing.gameObject.scene);
            Debug.Log($"[Space Samurai] Registered {added} planet(s) as landable ({skipped} already registered). Save the scene to keep this.");
        }

        /// <summary>
        /// Adds a <see cref="CockpitRecenter"/> to the open scene's Cockpit, wired to the right
        /// secondary button (held). After running, the player can hold right-B to align the
        /// cockpit with their current head pose. Run with Phase7_SpaceCombat open.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Add Cockpit Recenter To Open Scene", priority = 11)]
        public static void AddCockpitRecenterToOpenScene()
        {
            var cockpitGo = GameObject.Find("Cockpit");
            if (cockpitGo == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No object named 'Cockpit' found in the open scene. Open Phase7_SpaceCombat first.", "OK");
                return;
            }
            if (cockpitGo.GetComponent<CockpitRecenter>() != null)
            {
                Debug.Log("[Space Samurai] Cockpit already has a CockpitRecenter — nothing to do.");
                return;
            }
            var vrRig = Object.FindAnyObjectByType<VRRig>();
            if (vrRig == null || vrRig.Head == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No VRRig with a Head transform found. Open a scene built by the rig builder.", "OK");
                return;
            }
            if (!TryLoadInputRefsNoReimport(out var refs)) return;

            var recenter = cockpitGo.AddComponent<CockpitRecenter>();
            var rcSo = new SerializedObject(recenter);
            SetObjectRef(rcSo, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
            SetObjectRef(rcSo, "head", vrRig.Head);
            SetObjectRef(rcSo, "cockpit", cockpitGo.transform);
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(cockpitGo.scene);
            Debug.Log("[Space Samurai] Added CockpitRecenter to the cockpit (hold right-A for 0.6s to re-align). " +
                      "Save the scene to keep this.");
        }

        /// <summary>
        /// Re-attaches the player guns to the Cockpit in the CURRENTLY OPEN scene, for when the
        /// cockpit has been swapped to a prefab instance and its Ship Guns removed. Finds the cockpit,
        /// projectile pool, and rig in the live scene and wires a ShipWeaponController exactly like
        /// <see cref="BuildSpaceCombatScene"/> does. Run with Phase7_SpaceCombat open.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Add Ship Guns To Cockpit", priority = 8)]
        public static void AddShipGunsToCockpit()
        {
            var cockpit = GameObject.Find("Cockpit");
            if (cockpit == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No object named 'Cockpit' found in the open scene. Open Phase7_SpaceCombat first.", "OK");
                return;
            }
            var existingGuns = cockpit.GetComponentInChildren<ShipWeaponController>();
            if (existingGuns != null)
            {
                // Guns are already wired. Still (re)build the crosshair so this menu can be used to
                // retro-fit the crosshair onto scenes that pre-date the smoother-fighting pass.
                BuildCockpitCrosshair(cockpit.transform, existingGuns);
                EditorSceneManager.MarkSceneDirty(cockpit.scene);
                Debug.Log("[Space Samurai] Cockpit already had Ship Guns — (re)built the Crosshair under it. Save the scene to keep this.");
                return;
            }
            if (!TryLoadInputRefs(out var refs)) return;

            var pool = Object.FindAnyObjectByType<ProjectilePool>();
            if (pool == null)
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "No ProjectilePool found in the open scene. Build the Phase 7 scene first.", "OK");
                return;
            }
            var rig = Object.FindAnyObjectByType<XROrigin>();

            var shipWeapon = EnsureShipWeaponDefinition();

            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit.transform, false);
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
            SetObjectRef(gunsSo, "ownerRoot", rig != null ? rig.gameObject : null);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCockpitCrosshair(cockpit.transform, guns);

            EditorSceneManager.MarkSceneDirty(cockpit.scene);
            Debug.Log("[Space Samurai] Added Ship Guns (twin muzzles, RIGHT trigger = fire) under the Cockpit. " +
                      (rig == null ? "WARNING: no XR Origin found, so 'ownerRoot' is unset — player bolts may hit their own hull. " : "") +
                      "Save the scene to keep this.");
        }

        /// <summary>
        /// Build the cockpit-locked reticle + a hidden lock marker, wire them to the given
        /// <see cref="ShipWeaponController"/>. Idempotent — replaces any existing "Crosshair" child
        /// so re-running the menu doesn't stack reticles.
        /// </summary>
        private static void BuildCockpitCrosshair(Transform cockpit, ShipWeaponController guns)
        {
            // Tear down a previous build so this is safe to re-run.
            var existing = cockpit.Find("Crosshair");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // Sits at the muzzle midpoint, pushed 8m forward in cockpit-local space so it reads as
            // a gunsight floating ahead of the cockpit. Far enough that the slight parallax between
            // the two parallel muzzles is invisible to the player.
            var crosshairRoot = new GameObject("Crosshair");
            crosshairRoot.transform.SetParent(cockpit, false);
            crosshairRoot.transform.localPosition = new Vector3(0f, 1.0f, 8.8f);

            var reticleGo = new GameObject("Reticle");
            reticleGo.transform.SetParent(crosshairRoot.transform, false);

            // Self-luminous holographic reticle: an unlit material reads uniformly bright in dark space
            // and (with the HDR colour ShipCrosshair drives) blooms into a glowing gunsight. ShipCrosshair
            // overrides the per-renderer colour each frame via a MaterialPropertyBlock, so all parts can
            // share one material.
            var holoMat = MakeUnlitMaterial(new Color(0.15f, 0.85f, 1f) * 2.5f);

            void AddReticlePart(string name, PrimitiveType prim, Vector3 localPos, Vector3 localScale)
            {
                var part = GameObject.CreatePrimitive(prim);
                part.name = name;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = holoMat;
                part.transform.SetParent(reticleGo.transform, false);
                part.transform.localPosition = localPos;
                part.transform.localScale = localScale;
            }

            AddReticlePart("ArmUp", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.04f, 0.5f, 0.04f));
            AddReticlePart("ArmDown", PrimitiveType.Cube, new Vector3(0f, -0.45f, 0f), new Vector3(0.04f, 0.5f, 0.04f));
            AddReticlePart("ArmLeft", PrimitiveType.Cube, new Vector3(-0.45f, 0f, 0f), new Vector3(0.5f, 0.04f, 0.04f));
            AddReticlePart("ArmRight", PrimitiveType.Cube, new Vector3(0.45f, 0f, 0f), new Vector3(0.5f, 0.04f, 0.04f));
            AddReticlePart("Center", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f));

            var lockGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lockGo.name = "LockMarker";
            Object.DestroyImmediate(lockGo.GetComponent<Collider>());
            // World-positioned each frame by ShipCrosshair — parenting under crosshair is fine
            // because we set transform.position (Unity converts to local automatically).
            lockGo.transform.SetParent(crosshairRoot.transform, false);
            lockGo.GetComponent<Renderer>().sharedMaterial = holoMat;
            lockGo.transform.localScale = Vector3.one * 1.2f;
            lockGo.SetActive(false);

            var crosshair = crosshairRoot.AddComponent<ShipCrosshair>();
            var cSo = new SerializedObject(crosshair);
            SetObjectRef(cSo, "weapon", guns);
            SetObjectRef(cSo, "reticleRoot", reticleGo);
            SetObjectRef(cSo, "lockMarker", lockGo.GetComponent<Renderer>());
            cSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A grey-box enemy fighter parented under the universe and fully wired. Mirrors BuildEnemy().</summary>
        private static EnemyShip BuildEnemyShip(Transform universe, Vector3 universeLocalPos,
            EnemyShipDefinition def, ProjectilePool pool, ShipController player)
        {
            // Art-pass: prefer the EnemyShip art prefab (which already carries the EnemyShip
            // component, body collider, and muzzle). Falls back to the original grey-box
            // construction so this builder works even before Build All Art Prefabs has run.
            var shipPrefab = AssetDatabase.LoadAssetAtPath<EnemyShip>(EnemyShipPrefabPath);
            if (shipPrefab != null)
            {
                var instance = (EnemyShip)PrefabUtility.InstantiatePrefab(shipPrefab, universe);
                instance.transform.localPosition = universeLocalPos;
                Vector3 look = universe.TransformPoint(Vector3.zero) - instance.transform.position;
                if (look.sqrMagnitude > 0.01f) instance.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                var so2 = new SerializedObject(instance);
                SetObjectRef(so2, "definition", def);
                SetObjectRef(so2, "universe", universe);
                SetObjectRef(so2, "pool", pool);
                SetObjectRef(so2, "player", player);
                so2.ApplyModifiedPropertiesWithoutUndo();
                return instance;
            }

            var root = new GameObject("Enemy Ship");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = universeLocalPos;
            // Face roughly back toward the player's virtual origin so it reads as inbound.
            Vector3 look2 = universe.TransformPoint(Vector3.zero) - root.transform.position;
            if (look2.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(look2, Vector3.up);

            root.AddComponent<Health>();

            // Body: a wide box with a collider the player's bolts can strike.
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.transform.localScale = new Vector3(6f, 2.5f, 9f);
            var bodyRenderer = bodyGo.GetComponent<Renderer>();
            TintShared(bodyRenderer, new Color(0.7f, 0.72f, 0.78f));

            // Nose (no collider) so facing is legible.
            AddVisual(root.transform, "Nose", new Vector3(0f, 0f, 6f),
                new Vector3(2f, 1.5f, 4f), PrimitiveType.Cube, removeCollider: true);

            // Muzzle at the nose tip.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 8f);

            // Generous catch radius so glancing player bolts still register; mirrors the art prefab.
            var hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(root.transform, false);
            var hitSphere = hitbox.AddComponent<SphereCollider>();
            hitSphere.radius = 7f;
            hitSphere.isTrigger = false;

            var ship = root.AddComponent<EnemyShip>();
            var so = new SerializedObject(ship);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "universe", universe);
            SetObjectRef(so, "pool", pool);
            SetObjectRef(so, "player", player);
            SetObjectRef(so, "muzzle", muzzle.transform);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ship;
        }

        private static ShipWeaponDefinition EnsureShipWeaponDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipWeaponDefinition>(ShipWeaponPath);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<ShipWeaponDefinition>();
            AssetDatabase.CreateAsset(def, ShipWeaponPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyShipDefinition EnsureEnemyShipDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyShipDefinition>(EnemyShipDefPath);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            AssetDatabase.CreateAsset(def, EnemyShipDefPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        [MenuItem("Tools/Space Samurai/Build Phase 6 Boot Scene", priority = 6)]
        public static void BuildBootScene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Persistent managers on a "Game" root. GameFlowManager + GameState survive scene
            //    loads (DontDestroyOnLoad) so the menu is the only place we hard-configure scene names.
            var gameGo = new GameObject("Game");

            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.Boot;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            var flow = gameGo.AddComponent<GameFlowManager>();
            var flowSo = new SerializedObject(flow);
            // The menu does NOT auto-load anything — buttons drive the transitions instead.
            var loadOnStartProp = flowSo.FindProperty("loadFlightOnStart");
            if (loadOnStartProp != null) loadOnStartProp.boolValue = false;
            var bootProp = flowSo.FindProperty("bootScene");
            if (bootProp != null) bootProp.stringValue = "";
            var onFootProp = flowSo.FindProperty("onFootScene");
            if (onFootProp != null) onFootProp.stringValue = OnFootSceneName;
            var mainMenuProp = flowSo.FindProperty("mainMenuScene");
            if (mainMenuProp != null) mainMenuProp.stringValue = System.IO.Path.GetFileNameWithoutExtension(BootScenePath);
            // Persistent session hub: Start launches into the Chapter 1 hub (Phase 4 wiring). The
            // GameFlowManager is DontDestroyOnLoad, so this one value governs StartCampaign/StartNewGame
            // for the whole session.
            var shipHubProp = flowSo.FindProperty("shipHubScene");
            if (shipHubProp != null) shipHubProp.stringValue = Ch1HubSceneName;
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            // Persistent audio. Auto-wire any clips sitting in Assets/Ronin7/Audio by name so a
            // fresh build comes with sound already assigned (rebuilds are what null the refs); fields
            // with no matching file just stay silent. Re-runnable via Tools/Space Samurai/Audio.
            var audio = gameGo.AddComponent<AudioDirector>();
            var (audioAssigned, _) = AudioClipWiring.Wire(audio);
            Debug.Log($"[Space Samurai] Auto-wired {audioAssigned} audio clip(s) onto the AudioDirector.");

            // Persistent settings owner (loads PlayerPrefs, re-applies per scene) and the Quest
            // perf/quality scaffolding. Both no-op gracefully before assets/devices are present.
            gameGo.AddComponent<SettingsService>();
            gameGo.AddComponent<QualityBootstrap>();
            gameGo.AddComponent<GraphicsDirector>(); // resolves the graphics tier (bloom + particle/light gating)
            gameGo.AddComponent<Ronin7.Audio.Vfx.CombatVfxController>(); // pooled combat particle VFX (event-driven)

            // 2. Lighting — the menu is the first thing the player sees, so it needs to be lit.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 3. Seated rig — no locomotion (the menu is a single fixed pose).
            BuildRig(refs, addLocomotion: false);

            // 4. XR UI plumbing — interaction manager + event system with XRUIInputModule.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();

            // 5. Worldspace menu canvas with three buttons.
            var canvasGo = new GameObject("Main Menu Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>(); // makes the canvas hittable by XR rays

            var canvasRt = canvas.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(600f, 700f);
            canvasRt.localScale = Vector3.one * 0.001f; // 1px = 1mm → 0.6m wide
            canvasRt.position = new Vector3(0f, 1.2f, 1.0f);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);

            // Title — anchored near the top of the canvas.
            var title = MenuNewText(canvasRt, "RONIN 7", new Vector2(0f, 270f),
                new Vector2(560f, 70f), 44, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            // Four stacked buttons. ~110 px vertical spacing keeps the action methods on the
            // controller (no fragile per-button wiring beyond the onClick listener).
            var controller = canvasGo.AddComponent<MainMenuController>();
            var startBtn = MenuMakeButton(canvasRt, "START NEW GAME", new Vector2(0f, 130f));
            var continueBtn = MenuMakeButton(canvasRt, "CONTINUE", new Vector2(0f, 20f));
            var recalBtn = MenuMakeButton(canvasRt, "RECALIBRATE HEIGHT", new Vector2(0f, -90f));
            var exitBtn = MenuMakeButton(canvasRt, "EXIT GAME", new Vector2(0f, -200f));

            // Persistent listeners — survive serialization so the buttons actually work at runtime.
            // Start overwrites the current save (arrival autosave writes the most-recent slot);
            // Continue resumes from that same slot.
            UnityEventTools.AddPersistentListener(startBtn.onClick,
                new UnityEngine.Events.UnityAction(controller.OnStartCampaignClicked));
            UnityEventTools.AddPersistentListener(continueBtn.onClick,
                new UnityEngine.Events.UnityAction(controller.OnContinueClicked));
            UnityEventTools.AddPersistentListener(recalBtn.onClick,
                new UnityEngine.Events.UnityAction(controller.OnRecalibrateClicked));
            UnityEventTools.AddPersistentListener(exitBtn.onClick,
                new UnityEngine.Events.UnityAction(controller.OnExitGameClicked));

            // The controller greys CONTINUE out when the most recent slot is empty.
            var controllerSo = new SerializedObject(controller);
            var continueProp = controllerSo.FindProperty("continueButton");
            if (continueProp != null) continueProp.objectReferenceValue = continueBtn;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. Right-hand ray interactor on the rig we just built (UI Press = Right Hand/Select).
            WireRightHandRayInteractorMenu();

            // 6.5. Sci-fi dressing around the boot zone (skybox, glowing RONIN 7 backdrop, props).
            BuildBootDressing();

            // 7. Save the scene and register every scene the flow can swap into.
            EnsureFolder(SceneFolder);
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, BootScenePath);
            EnsureScenesInBuild(BootScenePath, ZoneScenePath, SpaceCombatScenePath);

            Debug.Log($"[Space Samurai] Phase 6 main menu built at {BootScenePath} (rig + worldspace " +
                      $"canvas with Start/Continue/Recalibrate/Exit buttons + sci-fi boot-zone dressing). " +
                      $"Build the other phase scenes too " +
                      $"if you haven't ({ZoneScenePath}, {SpaceCombatScenePath}). " +
                      "VERIFY in the Inspector: the Right Hand's XR Ray Interactor → UI Press Input → " +
                      "Input Action Reference Performed is set to 'Right Hand/Select' (rebuilds can re-null it). " +
                      "TODO: AudioDirector has no menu-music API yet — wire menu ambience/music when " +
                      "that API is added (out of scope for this builder).");
        }

        // ---- Main-menu helpers. Duplicated from SettingsPanelBuilder per the karpathy guideline:
        // those helpers are `private static` over there and not worth a new shared file for a single
        // sibling caller. Prefixed `Menu*` to keep intent and namespace local. ----

        private static void EnsureXRUIEventSystemMenu()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
            if (es.GetComponent<XRUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                es.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        private static Button MenuMakeButton(RectTransform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(label, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            MenuAnchor(rt, anchoredPos, new Vector2(480f, 80f));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.25f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            // Label stretched to the button's full rect so the text never drifts when the parent resizes.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.font = MenuLegacyFont();
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Text MenuNewText(RectTransform parent, string text, Vector2 pos, Vector2 size,
            int fontSize, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            MenuAnchor(rt, pos, size);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = MenuLegacyFont();
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = anchor;
            return t;
        }

        private static void MenuAnchor(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static Font MenuLegacyFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return font;
        }

        // ---- Boot-zone sci-fi dressing. Primitives + scene-embedded materials only (no new assets);
        // HDR emissive colors ride the global techno-noir bloom grade (NeonPostFx, threshold 0.9). ----

        private static void BuildBootDressing()
        {
            // The menu floats in open space: nebula skybox + dark cool ambient.
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(NeonSkyboxPath)
                         ?? AssetDatabase.LoadAssetAtPath<Material>(SpaceSkyboxPath);
            if (skybox != null) RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.16f);

            var root = new GameObject("Boot Zone Dressing");
            var cyan = new Color(0.35f, 0.9f, 1f);

            // Big glowing "RONIN 7" backdrop behind and above the menu canvas. TMP 3D text: 1 font
            // point ≈ 0.1 m, so size 22 → ~2.2 m tall letters at 10 m. HDR face color drives bloom.
            var titleGo = new GameObject("Ronin7 Backdrop Title");
            titleGo.transform.SetParent(root.transform, false);
            titleGo.transform.position = new Vector3(0f, 3.4f, 10f);
            var tmp = titleGo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "RONIN 7";
            tmp.fontSize = 22;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.characterSpacing = 8f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(40f, 8f); // wide enough that nothing wraps
            tmp.color = Color.white;
            var titleMat = new Material(tmp.fontSharedMaterial);
            // 1.6× keeps a visibly cyan core while still clearing the 0.9 bloom threshold for glow.
            titleMat.SetColor("_FaceColor", cyan * 1.6f);
            tmp.fontSharedMaterial = titleMat;

            // Starfield: a static shell of soft dots well outside the boot zone.
            var starsGo = new GameObject("Starfield");
            starsGo.transform.SetParent(root.transform, false);
            var stars = starsGo.AddComponent<ParticleSystem>();
            var main = stars.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 1000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.7f, 0.85f, 1f));
            main.maxParticles = 800;
            var emission = stars.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 800) });
            var shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 60f;
            shape.radiusThickness = 0.15f; // shell, not volume — keeps dots off the boot zone
            var starsRenderer = starsGo.GetComponent<ParticleSystemRenderer>();
            var starMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var starTex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (starTex != null) starMat.SetTexture("_BaseMap", starTex);
            starsRenderer.material = starMat;

            // Distant planet on the horizon — the directional light gives it a crescent.
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Distant Planet";
            planet.transform.SetParent(root.transform, false);
            planet.transform.position = new Vector3(26f, 11f, 55f); // clear of the backdrop title
            planet.transform.localScale = Vector3.one * 18f;
            planet.GetComponent<Renderer>().sharedMaterial =
                MenuDressingMat(new Color(0.16f, 0.22f, 0.34f), Color.black);
            Object.DestroyImmediate(planet.GetComponent<Collider>());

            // Boot deck: dark landing disc under the player with a neon rim.
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            deck.name = "Boot Deck";
            deck.transform.SetParent(root.transform, false);
            deck.transform.position = new Vector3(0f, -0.06f, 0f);
            deck.transform.localScale = new Vector3(7f, 0.05f, 7f);
            deck.GetComponent<Renderer>().sharedMaterial =
                MenuDressingMat(new Color(0.10f, 0.11f, 0.14f), Color.black);
            Object.DestroyImmediate(deck.GetComponent<Collider>());

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Deck Rim";
            rim.transform.SetParent(root.transform, false);
            rim.transform.position = new Vector3(0f, -0.09f, 0f);
            rim.transform.localScale = new Vector3(7.4f, 0.02f, 7.4f);
            rim.GetComponent<Renderer>().sharedMaterial =
                MenuDressingMat(Color.black, cyan * 2.5f);
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            // Four pillars flanking the boot zone, each with an emissive strip on the inner face.
            var pillarMat = MenuDressingMat(new Color(0.13f, 0.14f, 0.18f), Color.black);
            var stripMat = MenuDressingMat(Color.black, cyan * 2.2f);
            Vector3[] pillarPos =
            {
                new Vector3(-3f, 1.2f, 3f), new Vector3(3f, 1.2f, 3f),
                new Vector3(-3f, 1.2f, -2f), new Vector3(3f, 1.2f, -2f),
            };
            foreach (var pos in pillarPos)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.position = pos;
                pillar.transform.localScale = new Vector3(0.35f, 2.4f, 0.35f);
                pillar.GetComponent<Renderer>().sharedMaterial = pillarMat;
                Object.DestroyImmediate(pillar.GetComponent<Collider>());

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Pillar Strip";
                strip.transform.SetParent(root.transform, false);
                // Strip sits just proud of the pillar face that looks at the player.
                var inward = new Vector3(-Mathf.Sign(pos.x), 0f, 0f);
                strip.transform.position = pos + inward * 0.19f;
                strip.transform.localScale = new Vector3(0.06f, 2.0f, 0.08f);
                strip.GetComponent<Renderer>().sharedMaterial = stripMat;
                Object.DestroyImmediate(strip.GetComponent<Collider>());
            }

            // Slow-rotating holographic ring of data motes around the boot zone (reuses the
            // ship-select turntable spinner).
            var holoRing = new GameObject("Holo Ring");
            holoRing.transform.SetParent(root.transform, false);
            holoRing.transform.position = new Vector3(0f, 0.05f, 0f);
            holoRing.AddComponent<TurntableRotator>();
            var moteMat = MenuDressingMat(Color.black, cyan * 2f);
            const int motes = 12;
            for (int i = 0; i < motes; i++)
            {
                float angle = i * Mathf.PI * 2f / motes;
                var mote = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mote.name = "Mote";
                mote.transform.SetParent(holoRing.transform, false);
                mote.transform.localPosition = new Vector3(Mathf.Sin(angle) * 2.6f,
                    i % 2 == 0 ? 0.05f : 0.4f, Mathf.Cos(angle) * 2.6f);
                mote.transform.localScale = Vector3.one * 0.06f;
                mote.GetComponent<Renderer>().sharedMaterial = moteMat;
                Object.DestroyImmediate(mote.GetComponent<Collider>());
            }
        }

        /// <summary>URP Lit material (scene-embedded, not an asset). Pass a non-black emission for glow.</summary>
        private static Material MenuDressingMat(Color baseColor, Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", baseColor);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        /// <summary>Find the rig's right hand and add an XRRayInteractor wired to fire UI on
        /// Right Hand/Activate (the trigger). NOT Select: that action is the grip, which Grabber
        /// already holds down for the whole time the katana is in hand — leaving no press edge for
        /// the UI, so world-space buttons (the roguelike boon offer, settings panels) can only be
        /// clicked by letting go of the sword.</summary>
        private static void WireRightHandRayInteractorMenu()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogWarning("[Space Samurai] Main menu: no XR Origin found; skipped UI ray interactor.");
                return;
            }
            var hand = MenuFindChildByName(origin.transform, "Right Hand Controller");
            if (hand == null)
            {
                Debug.LogWarning("[Space Samurai] Main menu: 'Right Hand Controller' not found under XR Origin; " +
                                 "skipped UI ray interactor.");
                return;
            }
            if (hand.GetComponent<XRRayInteractor>() != null) return;

            var interactor = hand.gameObject.AddComponent<XRRayInteractor>();
            // MUST be "Select" (grip), not "Activate" (trigger). Nothing enables 'Right Hand/Activate'
            // at runtime — OverdriveController/MirrorSummonController are the only components that
            // enable it and both self-gate on AbilityAccess.Has — so a UI Press bound to Activate is a
            // permanently DISABLED action: the ray hovers the panel, the controller reports the button
            // as physically pressed, and the action never fires. That is the "I pressed every button
            // and nothing happened" boon-panel failure. 'Right Hand/Select' is enabled by Grabber and
            // is what the working Phase6_Boot menu uses (and what this builder's own end-of-build
            // verification note already told us to check for).
            var pressRef = MenuLoadActionRef("Right Hand", "Select");
            var so = new SerializedObject(interactor);
            if (pressRef != null)
            {
                var prop = so.FindProperty("m_UIPressInput.m_InputActionReferencePerformed");
                if (prop != null) prop.objectReferenceValue = pressRef;
            }
            // Force XRI to read XRInputButtonReader fields (UIPressInput) rather than the legacy XR
            // Controller component. In Auto mode without a controller, some XRI 3.x builds fall back
            // to default-input which ignores our UIPressInput → buttons hover but never click.
            var compatProp = so.FindProperty("m_InputCompatibilityMode");
            if (compatProp != null) compatProp.intValue = 2; // NewerOnly
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureRayLineVisual(hand.gameObject);
        }

        /// <summary>
        /// Adds a LineRenderer + XRInteractorLineVisual to the given XR Ray Interactor's GameObject so
        /// the player can SEE where they're pointing. Without this the ray is invisible and aiming at a
        /// worldspace UI button is guesswork. Idempotent.
        /// </summary>
        private static void EnsureRayLineVisual(GameObject handGo)
        {
            var line = handGo.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = handGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = 0.005f; // ~5mm thick laser
                line.positionCount = 2;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = new Color(0.4f, 0.8f, 1f, 0.9f);
                line.endColor = new Color(0.4f, 0.8f, 1f, 0.2f);
            }
            if (handGo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>() == null)
            {
                var visual = handGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
                visual.lineLength = 5f;
                visual.lineWidth = 0.005f;
            }
        }

        private static Transform MenuFindChildByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = MenuFindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static InputActionReference MenuLoadActionRef(string map, string action)
        {
            var assetRefs = AssetDatabase.LoadAllAssetRepresentationsAtPath(InputAssetPath);
            if (assetRefs == null) return null;
            foreach (var o in assetRefs)
            {
                if (o is InputActionReference r && r.action != null && r.action.actionMap != null
                    && r.action.actionMap.name == map && r.action.name == action)
                    return r;
            }
            Debug.LogWarning($"[Space Samurai] Main menu: input action {map}/{action} not found for the UI ray interactor.");
            return null;
        }

        private static void BuildCockpit(Transform parent)
        {
            // Try Agent 5's prefab first; on miss, build the greybox cubes under a wrapper that
            // sits at identity under `parent` so visual positions are unchanged.
            System.Func<GameObject> greybox = () =>
            {
                var wrapper = new GameObject("CockpitGreybox");
                var metal = new Color(0.2f, 0.22f, 0.26f);
                AddVisualTinted(wrapper.transform, "CockpitFloor", new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.1f, 1.6f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "Dashboard", new Vector3(0f, 0.55f, 0.85f), new Vector3(1.0f, 0.05f, 0.35f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "RailL", new Vector3(-0.75f, 1.0f, 0.35f), new Vector3(0.08f, 0.08f, 1.0f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "RailR", new Vector3(0.75f, 1.0f, 0.35f), new Vector3(0.08f, 0.08f, 1.0f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "CanopyBar", new Vector3(0f, 1.7f, 0.6f), new Vector3(1.3f, 0.08f, 0.08f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "StrutL", new Vector3(-0.65f, 1.35f, 0.85f), new Vector3(0.06f, 0.95f, 0.06f), PrimitiveType.Cube, metal);
                AddVisualTinted(wrapper.transform, "StrutR", new Vector3(0.65f, 1.35f, 0.85f), new Vector3(0.06f, 0.95f, 0.06f), PrimitiveType.Cube, metal);
                return wrapper;
            };
            ArtPrefabRegistry.TryInstantiateOrFallback(CockpitPrefabPath, greybox, parent);
        }

        /// <summary>
        /// Shared player-ship visual for open (non-hub) space scenes: the sleek <see cref="BuildCockpit"/>
        /// prefab plus a <see cref="Ronin7.Ship.ShipHullSelector"/> that wraps the signature exterior
        /// hull at runtime (same index-aligned wiring the galaxy hubs use). Galaxy hubs add the enclosed
        /// cabin separately; open-space dogfights stay open. Call this where the inline cockpit used to be,
        /// then call <see cref="BuildCockpitCrosshair"/> after the guns are built.
        /// </summary>
        private static void BuildPlayerShipVisual(Transform cockpit)
        {
            BuildCockpit(cockpit);

            var hullSelector = cockpit.gameObject.AddComponent<Ronin7.Ship.ShipHullSelector>();
            var hullPaths = ArtPrefabBuilder.ShipHullPrefabPaths;
            var hullObjs = new GameObject[hullPaths.Length];
            for (int i = 0; i < hullPaths.Length; i++)
                hullObjs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(hullPaths[i]);

            var hsSo = new SerializedObject(hullSelector);
            var arr = hsSo.FindProperty("hullPrefabs");
            arr.arraySize = hullObjs.Length;
            for (int i = 0; i < hullObjs.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = hullObjs[i];
            hsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static readonly string[] PlanetBiomes = { "Rocky", "Ice", "Lush", "GasGiant" };

        /// <summary>
        /// A solid-black skybox material, created idempotently at <see cref="SpaceSkyboxPath"/>.
        /// Uses Unity's built-in "Skybox/Procedural" with the sun disk removed and every tint
        /// driven to black (exposure 0), so the procedural sky renders flat black on all sides —
        /// the simplest robust way to get a true void without authoring a cubemap.
        /// </summary>
        private static Material EnsureBlackSkybox()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SpaceSkyboxPath);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);
            var mat = new Material(Shader.Find("Skybox/Procedural"));
            mat.SetFloat("_SunDisk", 0f);                 // 0 = None: no sun on the skybox itself
            mat.SetColor("_SkyTint", Color.black);
            mat.SetColor("_GroundColor", Color.black);
            mat.SetFloat("_AtmosphereThickness", 0f);
            mat.SetFloat("_Exposure", 0f);                // drives the whole sky to black
            AssetDatabase.CreateAsset(mat, SpaceSkyboxPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// A dark techno-noir "neon nebula" skybox material, created idempotently at
        /// <see cref="NeonSkyboxPath"/>. Mirrors <see cref="EnsureBlackSkybox"/>: built from Unity's
        /// "Skybox/Procedural" (no cubemap authoring), but instead of driving everything to black we
        /// keep a deep blue-violet zenith with a faint magenta/teal horizon glow (a touch of atmosphere
        /// thickness + a low non-zero exposure) so SPACE scenes read as a moody void with just enough
        /// colour for the neon emissives + bloom to pop against. Sun disk stays off — the sun is the
        /// separate self-luminous sphere from <see cref="BuildSun"/>.
        /// </summary>
        private static Material EnsureNeonNebulaSkybox()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(NeonSkyboxPath);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);
            var mat = new Material(Shader.Find("Skybox/Procedural"));
            mat.SetFloat("_SunDisk", 0f);                              // 0 = None: no sun on the sky itself
            mat.SetColor("_SkyTint", new Color(0.05f, 0.06f, 0.16f));  // deep blue-violet zenith
            mat.SetColor("_GroundColor", new Color(0.12f, 0.03f, 0.10f)); // faint magenta lower-hemisphere glow
            mat.SetFloat("_AtmosphereThickness", 0.35f);              // thin haze → a soft horizon band
            mat.SetFloat("_Exposure", 0.35f);                         // low but non-zero: faint nebula light, not black
            AssetDatabase.CreateAsset(mat, NeonSkyboxPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// Places a big, self-luminous "Sun Visual" sphere far out under the universe frame and
        /// reorients the existing directional <paramref name="sunLight"/> to shine from it toward
        /// the origin. The sphere uses URP's Unlit shader so it reads as a uniform glowing disc
        /// (the toon shader would cel-shade its far side into shadow). Collider stripped — never a hazard.
        /// </summary>
        private static void BuildSun(Transform universe, Light sunLight)
        {
            // Direction the light currently travels (its forward). The sun sits FAR back along the
            // opposite direction so its rays come "from the sun" through the origin.
            Vector3 fromSunToOrigin = sunLight.transform.forward;
            Vector3 sunPos = -fromSunToOrigin.normalized * 4000f;

            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun Visual";
            Object.DestroyImmediate(sun.GetComponent<Collider>());
            sun.transform.SetParent(universe, false);
            sun.transform.localPosition = sunPos;
            sun.transform.localScale = Vector3.one * 800f;

            // Self-luminous unlit material: a bright warm white that ignores scene lighting, so the
            // whole disc glows uniformly regardless of which way it faces.
            var renderer = sun.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var sunColor = new Color(1f, 0.96f, 0.85f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sunColor);
            if (mat.HasProperty("_Color")) mat.color = sunColor;
            renderer.sharedMaterial = mat;

            // Re-aim the key light so the lit side of ships/asteroids faces the visible sun, and bump
            // intensity for a clear key. RenderSettings.sun pins URP's "main" sun to this light.
            sunLight.transform.rotation = Quaternion.LookRotation(-sunPos.normalized);
            sunLight.intensity = 1.3f;
            RenderSettings.sun = sunLight;
        }

        private static GameObject BuildPlanet(Transform parent, Vector3 position, float radius, Color color)
        {
            System.Func<GameObject> greybox = () =>
                AddVisualTinted(null, "Planet", Vector3.zero, Vector3.one * (radius * 2f), PrimitiveType.Sphere, color);
            var go = ArtPrefabRegistry.TryInstantiateOrFallback(PlanetPrefabPath, greybox, parent);
            if (go == null) return null;
            // Original BuildPlanet placed the planet at `position` in the parent's local space.
            go.transform.localPosition = position;

            // Per-planet biome variant: deterministic by position so the same planet always looks
            // the same across rebuilds. Falls back silently to the prefab's base material when the
            // biome variants don't exist yet (run Tools/Space Samurai/Art/Regenerate Planets).
            int biomeIndex = (int)((uint)position.GetHashCode() % (uint)PlanetBiomes.Length);
            var biomeMat = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/Ronin7/Art/Materials/SamuraiToon_Planet_{PlanetBiomes[biomeIndex]}.mat");
            var renderer = go.GetComponentInChildren<Renderer>();
            if (biomeMat != null && renderer != null) renderer.sharedMaterial = biomeMat;
            // Apply per-planet color tint so it takes effect on the prefab path as well as greybox.
            if (renderer != null) TintShared(renderer, color);
            return go;
        }

        /// <summary>
        /// Builds a pair of linked <see cref="Wormhole"/>s under <paramref name="universe"/> at
        /// <paramref name="aPos"/>/<paramref name="bPos"/> (universe-local). Each is wired as the
        /// other's exit and shares the universe + ship refs. Greybox visual: a flattened sphere as a
        /// portal-disc placeholder (art is Phase 4 — swap for a torus/shader-ring prefab there).
        /// </summary>
        private static void BuildWormholePair(Transform universe, ShipController ship, Vector3 aPos, Vector3 bPos)
        {
            var a = BuildWormhole(universe, "Wormhole A", aPos, new Color(0.4f, 0.7f, 1f));
            var b = BuildWormhole(universe, "Wormhole B", bPos, new Color(1f, 0.6f, 0.4f));

            WireWormhole(a, b, universe, ship);
            WireWormhole(b, a, universe, ship);
        }

        /// <summary>One greybox wormhole GameObject with its <see cref="Wormhole"/> component (unwired).</summary>
        private static Wormhole BuildWormhole(Transform universe, string name, Vector3 position, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(universe, false);
            go.transform.localPosition = position;
            // Art portal ring (Phase 4) instantiates as a CHILD of the root; greybox portal disc — a
            // flattened sphere — is the fallback. The Wormhole component stays on the root (added
            // below). Either way the visual is tinted to the per-end color (blue / orange).
            System.Func<GameObject> greybox = () =>
                AddVisualTinted(null, "Portal (greybox)", Vector3.zero,
                    new Vector3(40f, 40f, 4f), PrimitiveType.Sphere, color);
            var visual = ArtPrefabRegistry.TryInstantiateOrFallback(WormholePrefabPath, greybox, go.transform);
            if (visual != null)
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    TintShared(r, color);
            return go.AddComponent<Wormhole>();
        }

        /// <summary>Serialized wiring for one wormhole: its exit + the shared universe/ship refs.</summary>
        private static void WireWormhole(Wormhole hole, Wormhole exit, Transform universe, ShipController ship)
        {
            var so = new SerializedObject(hole);
            SetObjectRef(so, "exit", exit);
            SetObjectRef(so, "universe", universe);
            SetObjectRef(so, "ship", ship);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A small worldspace TextMesh on the dashboard for the landing prompt (starts hidden).</summary>
        private static TextMesh BuildLandingPrompt(Transform cockpit)
        {
            var go = new GameObject("Landing Prompt");
            go.transform.SetParent(cockpit, false);
            go.transform.localPosition = new Vector3(0f, 1.05f, 0.74f);
            go.transform.localScale = Vector3.one * 0.012f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = "";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = new Color(0.7f, 0.9f, 1f);
            go.SetActive(false); // LandingApproach toggles it on when in range
            return tm;
        }

        /// <summary>
        /// Builds a few belt clusters of REAL, destructible, contact-damaging asteroids (Phase 2),
        /// replacing the old purely-decorative sphere field. <paramref name="count"/> is the total
        /// asteroid budget shared across the belts (kept conservative for Quest). Each asteroid is a
        /// solid sphere with <see cref="Health"/> + <see cref="Asteroid"/> on the Hittable layer, so
        /// player AND enemy bolts cut it down via the existing <see cref="Projectile"/> path.
        /// </summary>
        private static void BuildAsteroidField(Transform parent, int count)
        {
            var rng = new System.Random(20260520);

            // 3 belts as rough bands at distinct depths the player flies into (+Z). Each is a center
            // with a scatter box; the budget is split across them. Total ≈ count asteroids.
            BuildAsteroidBelt(parent, rng, new Vector3(0f, 0f, 90f), new Vector3(70f, 30f, 30f), count / 3);
            BuildAsteroidBelt(parent, rng, new Vector3(-15f, 8f, 210f), new Vector3(60f, 26f, 28f), count / 3);
            BuildAsteroidBelt(parent, rng, new Vector3(20f, -6f, 330f), new Vector3(64f, 28f, 30f), count - 2 * (count / 3));
        }

        /// <summary>Scatter <paramref name="n"/> asteroids in a band centred at <paramref name="center"/>
        /// with half-extents <paramref name="extents"/> (all in the parent/universe-local frame).</summary>
        private static void BuildAsteroidBelt(Transform parent, System.Random rng, Vector3 center, Vector3 extents, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 offset = new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * 2f * extents.x,
                    ((float)rng.NextDouble() - 0.5f) * 2f * extents.y,
                    ((float)rng.NextDouble() - 0.5f) * 2f * extents.z);
                float s = (float)rng.NextDouble() * 1.6f + 0.6f;
                BuildAsteroid(parent, center + offset, s);
            }
        }

        /// <summary>One destructible, contact-hazard asteroid: greybox sphere + solid collider + Health + Asteroid.</summary>
        private static GameObject BuildAsteroid(Transform parent, Vector3 localPos, float scale)
        {
            var rock = new Color(0.4f, 0.38f, 0.34f);
            // Greybox: a single sphere WITH its collider kept (unlike AddVisualTinted, which strips
            // it). Mirrors BuildPlanet — the art prefab (a craggy primitive cluster with a root
            // solid SphereCollider) slots in when present, this sphere is the fallback.
            System.Func<GameObject> greybox = () =>
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Asteroid";
                TintShared(g.GetComponent<Renderer>(), rock);
                return g;
            };
            var go = ArtPrefabRegistry.TryInstantiateOrFallback(AsteroidPrefabPath, greybox, parent);
            if (go == null) return null;
            go.name = "Asteroid";
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;

            // Put it on Hittable so the Projectile sweep targets it (gunfire-destructible for free).
            // Layers.Hittable is -1 when the layer isn't defined in Tags & Layers; in that case leave
            // the object on Default — the HittableMask falls back to ~0, so bolts still hit it.
            if (Layers.Hittable >= 0) go.layer = Layers.Hittable;

            // Ensure ONE solid root SphereCollider — the art prefab root already carries one (sized
            // to enclose the cluster) and the greybox sphere brings its own; only add if neither did.
            // Asteroid.WorldRadius derives from this collider, so it must live on the root.
            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = false;

            // The art prefab already ships its rock-grey SamuraiToon_Asteroid material across all
            // children, so no extra tint is applied on the prefab path (greybox is tinted above).
            var health = go.AddComponent<Health>();
            health.Configure(30f);
            go.AddComponent<Asteroid>();
            return go;
        }

        private static GameObject AddVisualTinted(Transform parent, string name, Vector3 pos,
            Vector3 scale, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) { go.transform.SetParent(parent, false); go.transform.localPosition = pos; }
            else go.transform.position = pos;
            go.transform.localScale = scale;
            TintShared(go.GetComponent<Renderer>(), color);
            return go;
        }

        private static EnemyDefinition EnsureEnemyDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefPath);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(def, EnemyDefPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        /// <summary>Builds a melee enemy (capsule body + overhead-chop weapon) and returns it.</summary>
        private static Enemy BuildEnemy(Vector3 position, Health playerHealth, EnemyDefinition def)
        {
            var root = new GameObject("Enemy");
            root.transform.position = position;
            root.AddComponent<Health>();

            // Try Agent 3's prefab body first; on miss, fall back to the greybox capsule.
            System.Func<GameObject> bodyGreybox = () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Body";
                go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                return go;
            };
            var bodyGo = ArtPrefabRegistry.TryInstantiateOrFallback(EnemyFootPrefabPath, bodyGreybox, root.transform);
            var bodyRenderer = bodyGo.GetComponent<Renderer>();

            var armR = bodyGo.transform.Find("ArmR");
            var bladeTip = bodyGo.transform.Find("ArmR/Sword/Blade/BladeTip");
            if (armR == null || bladeTip == null)
            {
                var armRGo = new GameObject("ArmR");
                armRGo.transform.SetParent(bodyGo.transform, false);
                armRGo.transform.localPosition = new Vector3(0.28f, 0.95f, 0f);
                var swordGo = new GameObject("Sword");
                swordGo.transform.SetParent(armRGo.transform, false);
                var bladeGo = new GameObject("Blade");
                bladeGo.transform.SetParent(swordGo.transform, false);
                var bladeTipGo = new GameObject("BladeTip");
                bladeTipGo.transform.SetParent(bladeGo.transform, false);
                bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                armR = armRGo.transform;
                bladeTip = bladeTipGo.transform;
            }

            var enemy = root.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "weapon", armR);
            SetObjectRef(so, "bladeTip", bladeTip);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            SetObjectRef(so, "target", playerHealth);
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private static ZoneDefinition EnsureZoneDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(ZoneDefPath);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<ZoneDefinition>();
            AssetDatabase.CreateAsset(def, ZoneDefPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static Pickup BuildPickup(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Relic";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.12f;
            TintShared(go.GetComponent<Renderer>(), new Color(0.95f, 0.82f, 0.2f));

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; // floats until grabbed
            go.AddComponent<Grabbable>();
            return go.AddComponent<Pickup>();
        }

        private static ExtractionZone BuildExtraction(Vector3 position)
        {
            var root = new GameObject("Extraction Pad");
            root.transform.position = position;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(1.6f, 2f, 1.6f);

            var ez = root.AddComponent<ExtractionZone>();
            var so = new SerializedObject(ez);
            SetObjectRef(so, "pad", disc.GetComponent<Renderer>());
            so.ApplyModifiedPropertiesWithoutUndo();
            return ez;
        }

        private static void BuildBoundary(float radius)
        {
            var parent = new GameObject("Boundary").transform;
            const int posts = 24;
            for (int i = 0; i < posts; i++)
            {
                float a = (i / (float)posts) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * radius, 1f, Mathf.Sin(a) * radius);
                AddVisual(parent, "Post", pos, new Vector3(0.1f, 2f, 0.1f),
                    PrimitiveType.Cube, removeCollider: true);
            }
        }

        /// <summary>
        /// Edit-mode-safe tint: reuses one material per (base material, colour) pair via a static
        /// cache instead of cloning a brand-new material per renderer. Cloning per-renderer used to
        /// embed 50-164 unique materials in a single chapter scene and defeat the SRP batcher on
        /// Quest; identical shared materials batch, so keying the cache on the exact colour keeps
        /// the perf win while still giving every distinct tint its own material. Safe: none of the
        /// callers (BuildWall/BuildFloorCeiling/BuildProp/BuildSlidingDoor/etc.) mutate a renderer's
        /// sharedMaterial at runtime — audited, no hits outside this builder file.
        /// </summary>
        private static readonly Dictionary<(Material baseMat, Color32 color), Material> TintCache =
            new Dictionary<(Material, Color32), Material>();

        private static void TintShared(Renderer r, Color color)
        {
            if (r == null || r.sharedMaterial == null) return;
            var baseMat = r.sharedMaterial;
            var key = (baseMat, (Color32)color);
            if (!TintCache.TryGetValue(key, out var mat) || mat == null)
            {
                mat = new Material(baseMat) { name = baseMat.name + "_Tint" };
                // SamuraiToon exposes _BaseColor; legacy/Standard materials use _Color. Set whichever exists.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.color = color;
                TintCache[key] = mat;
            }
            r.sharedMaterial = mat;
        }

        private static void SetObjectRefList(SerializedObject so, string property, List<Object> values)
        {
            var prop = so.FindProperty(property);
            if (prop == null) return;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static WeaponDefinition EnsureWeaponDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponPath);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(def, WeaponPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        /// <summary>A grabbable katana built from primitives, blade pointing along +Z.</summary>
        private static void BuildSword(Vector3 position, WeaponDefinition weapon)
            => BuildSword(position, Quaternion.Euler(-90f, 0f, 0f), weapon, null);

        /// <summary>
        /// Builds the grabbable katana. The gameplay hitbox (a <c>Blade</c> child carrying the trigger
        /// BoxCollider + <see cref="BladeDamager"/>) always comes from the greybox factory so combat is
        /// identical regardless of the visual. When <paramref name="visualPrefabPath"/> points at an
        /// existing prefab (e.g. <c>Echo</c>, Ronin's named blade), that mesh becomes the visual: the
        /// greybox renderers are disabled (their colliders/damager kept) and the prefab is fitted along
        /// the grip's local +Z. When it is null/missing, the original behaviour is preserved exactly
        /// (Sword_Katana.prefab if present, else the greybox visual).
        /// </summary>
        private static void BuildSword(Vector3 position, Quaternion rotation, WeaponDefinition weapon, string visualPrefabPath)
        {
            var root = new GameObject("Sword");
            root.transform.SetPositionAndRotation(position, rotation);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.isKinematic = true; // floats on the rack; becomes dynamic once thrown

            // Grip point in the middle of the handle.
            var attach = new GameObject("AttachPoint");
            attach.transform.SetParent(root.transform, false);
            attach.transform.localPosition = new Vector3(0f, 0f, 0.06f);

            // Full sword visual: handle, guard, and blade all live under a single wrapper so the
            // art-pass prefab can replace the entire grip/blade stack as one unit. Without this
            // the greybox Handle/Guard cubes would render alongside the art prefab's tsuka/tsuba.
            // Gameplay-critical: the Blade child carries the trigger BoxCollider + BladeDamager.
            System.Func<GameObject> swordGreybox = () =>
            {
                var wrapper = new GameObject("SwordVisual");

                AddVisual(wrapper.transform, "Handle", new Vector3(0f, 0f, 0.06f),
                    new Vector3(0.035f, 0.035f, 0.12f), PrimitiveType.Cube, removeCollider: true);
                AddVisual(wrapper.transform, "Guard", new Vector3(0f, 0f, 0.13f),
                    new Vector3(0.16f, 0.03f, 0.03f), PrimitiveType.Cube, removeCollider: true);

                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Blade";
                b.transform.SetParent(wrapper.transform, false);
                b.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                b.transform.localScale = new Vector3(0.04f, 0.012f, 0.62f);
                b.GetComponent<BoxCollider>().isTrigger = true;
                b.AddComponent<BladeDamager>();
                return wrapper;
            };

            var namedVisual = string.IsNullOrEmpty(visualPrefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            if (namedVisual != null)
            {
                // Hitbox from the greybox (renderers off, collider + damager kept); mesh from the prefab.
                var greybox = swordGreybox();
                greybox.transform.SetParent(root.transform, false);
                foreach (var r in greybox.GetComponentsInChildren<Renderer>()) r.enabled = false;

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(namedVisual, root.transform);
                visual.name = "BladeVisual";
                FitBladeVisual(visual.transform);
            }
            else
            {
                ArtPrefabRegistry.TryInstantiateOrFallback(SwordPrefabPath, swordGreybox, root.transform);
            }

            var grab = root.AddComponent<Grabbable>();
            var grabSo = new SerializedObject(grab);
            SetObjectRef(grabSo, "attachPoint", attach.transform);
            grabSo.ApplyModifiedPropertiesWithoutUndo();

            var sword = root.AddComponent<Sword>();
            sword.Definition = weapon;

            EnsureKatanaHolster(grab);
        }

        /// <summary>
        /// Orients a named blade mesh (e.g. Echo — a ~1 m katana modelled standing along +Y, centred on
        /// origin) into the sword grip convention: blade along the root's local +Z with the grip near the
        /// origin, so it lines up with the greybox Blade hitbox (z≈0.14–0.76). Rotating +90° about X maps
        /// the mesh's +Y up-axis onto +Z; shifting +0.5 m puts the grip end at the origin.
        /// </summary>
        private static void FitBladeVisual(Transform visual)
        {
            visual.localScale = Vector3.one;                 // Echo already imports ~1 m tall
            visual.localRotation = Quaternion.Euler(90f, 0f, 0f); // blade +Y -> grip +Z
            visual.localPosition = new Vector3(0f, 0f, 0.5f);     // grip end at origin, blade out to +Z
        }

        /// <summary>
        /// Wires a KatanaHolster on the scene's VRRig so the sword always returns to the player's
        /// hip when released. Safe to call repeatedly (rebuilds, rewires): reuses an existing
        /// holster/anchor and only fills in the references.
        /// </summary>
        private static void EnsureKatanaHolster(Grabbable swordGrabbable)
        {
            if (swordGrabbable == null) return;
            var vrRig = Object.FindAnyObjectByType<VRRig>(FindObjectsInactive.Include);
            if (vrRig == null) return;

            var hipAnchor = vrRig.transform.Find("HipAnchor");
            if (hipAnchor == null)
            {
                var anchorGo = new GameObject("HipAnchor");
                anchorGo.transform.SetParent(vrRig.transform, false);
                anchorGo.transform.localPosition = new Vector3(0.15f, 0.9f, 0.05f);
                hipAnchor = anchorGo.transform;
            }

            var holster = vrRig.GetComponent<KatanaHolster>();
            if (holster == null) holster = vrRig.gameObject.AddComponent<KatanaHolster>();

            var so = new SerializedObject(holster);
            SetObjectRef(so, "sword", swordGrabbable);
            SetObjectRef(so, "hipAnchor", hipAnchor);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(holster);
        }

        private static void AddVisual(Transform parent, string name, Vector3 localPos,
            Vector3 localScale, PrimitiveType type, bool removeCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (removeCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
        }

        /// <summary>Builds the XR Origin hierarchy in the active scene and returns the root.</summary>
        private static GameObject BuildRig(Object[] inputRefs, bool addLocomotion = true)
        {
            // Root / XR Origin
            var rootGo = new GameObject("XR Origin (Space Samurai)");
            var origin = rootGo.AddComponent<XROrigin>();

            // On-foot scenes get a CharacterController + locomotion. Seated flight omits both
            // entirely (no walking, no gravity/fall) so the rig is truly stationary.
            if (addLocomotion)
            {
                var characterController = rootGo.AddComponent<CharacterController>();
                characterController.radius = 0.25f;
                characterController.height = 1.6f;
                characterController.center = new Vector3(0f, 0.8f, 0f);
                characterController.skinWidth = 0.01f;

                // On-foot scenes share a layout convention: a thin front wall + floor lip at z=0 with
                // the room extending into +z. Spawning the rig at world origin straddles that wall, so
                // CharacterController depenetration shoves the player backward off the floor into the
                // void (the spawn-through-floor bug). Nudge on-foot rigs a short distance inside the
                // room so they spawn over solid floor and ZoneBounds captures a valid safe position.
                // Seated/space rigs pass addLocomotion=false and keep the origin.
                const float OnFootSpawnZ = 2f; // clear the z=0 front wall / floor lip
                rootGo.transform.position = new Vector3(0f, 0f, OnFootSpawnZ);
            }

            // Camera offset — OWNED BY XROrigin. In Floor tracking mode XROrigin forces this
            // object's local Y to 0 on every tracking-origin update (a recenter triggers one), so
            // it must NOT be used as the eye-height lever.
            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(rootGo.transform, false);

            // Eye Height — a transform WE own, between the XROrigin offset and the tracked head.
            // The camera and hands hang off it, so shifting its local Y raises/lowers the whole
            // viewpoint for eye-height calibration without XROrigin clobbering it.
            var eyeHeightGo = new GameObject("Eye Height");
            eyeHeightGo.transform.SetParent(offsetGo.transform, false);

            // Main camera
            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(eyeHeightGo.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            // Space scene places the sun visual at 4000 units and planets/wormholes out to ~1360.
            // The default 1000 far clip clipped all of them away (only near enemies/asteroids and the
            // cockpit rendered → "empty black space"). 6000 covers the sun with margin; reversed-Z
            // depth on Quest keeps 0.01–6000 precise enough to avoid z-fighting.
            cam.farClipPlane = 6000f;
            camGo.AddComponent<AudioListener>();
            AddPoseDriver(camGo, inputRefs, "Head");

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // Hands
            var leftHand = CreateHand(eyeHeightGo.transform, "Left Hand Controller", inputRefs, "Left Hand", true);
            var rightHand = CreateHand(eyeHeightGo.transform, "Right Hand Controller", inputRefs, "Right Hand", false);

            // Locomotion (our own, Input-System-driven)
            if (addLocomotion)
            {
                var loco = rootGo.AddComponent<ContinuousLocomotion>();
                var locoSo = new SerializedObject(loco);
                SetObjectRef(locoSo, "cameraTransform", camGo.transform);
                SetObjectRef(locoSo, "moveAction", FindRef(inputRefs, "Left Hand", "Move"));
                SetObjectRef(locoSo, "turnAction", FindRef(inputRefs, "Right Hand", "Turn"));
                SetObjectRef(locoSo, "dashAction", FindRef(inputRefs, "Right Hand", "Dash"));
                SetObjectRef(locoSo, "runAction", FindRef(inputRefs, "Right Hand", "Run"));
                SetObjectRef(locoSo, "crouchAction", FindRef(inputRefs, "Left Hand", "Crouch"));
                SetObjectRef(locoSo, "recenterAction", FindRef(inputRefs, "Right Hand", "Recenter"));
                SetObjectRef(locoSo, "jumpAction", FindRef(inputRefs, "Left Hand", "Jump"));
                locoSo.ApplyModifiedPropertiesWithoutUndo();

                // Parkour: grip-based wall climbing (hands/grabbers self-resolve from VRRig).
                var climb = rootGo.AddComponent<WallClimbLocomotion>();
                var climbSo = new SerializedObject(climb);
                SetObjectRef(climbSo, "leftGripAction", FindRef(inputRefs, "Left Hand", "Select"));
                SetObjectRef(climbSo, "rightGripAction", FindRef(inputRefs, "Right Hand", "Select"));
                climbSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // VRRig references
            var rig = rootGo.AddComponent<VRRig>();
            rig.Origin = rootGo.transform;
            rig.EyeHeightRoot = eyeHeightGo.transform;
            rig.Head = camGo.transform;
            rig.LeftHand = leftHand.transform;
            rig.RightHand = rightHand.transform;

            // Player health so enemies can damage the samurai later.
            rootGo.AddComponent<Health>();

            return rootGo;
        }

        private static GameObject CreateHand(Transform parent, string name, Object[] refs, string map, bool isLeft)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(parent, false);
            AddPoseDriver(hand, refs, map);

            // Velocity tracking (throwing + sword swing speed) and grab interaction.
            var vel = hand.AddComponent<HandVelocityTracker>();
            var grabber = hand.AddComponent<Grabber>();
            var grabberSo = new SerializedObject(grabber);
            SetObjectRef(grabberSo, "gripAction", FindRef(refs, map, "Select"));
            SetObjectRef(grabberSo, "velocity", vel);
            var leftProp = grabberSo.FindProperty("leftHand");
            if (leftProp != null) leftProp.boolValue = isLeft;
            grabberSo.ApplyModifiedPropertiesWithoutUndo();

            // Visible placeholder for the controller until Agent 4's hand prefab lands. A small
            // sphere (palm) + a short "pointer" cube reads as a hand far better than a thin cube.
            System.Func<GameObject> handVisualGreybox = () =>
            {
                var wrapper = new GameObject("Visual");
                var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                vis.name = "Palm";
                Object.DestroyImmediate(vis.GetComponent<Collider>());
                vis.transform.SetParent(wrapper.transform, false);
                vis.transform.localScale = Vector3.one * 0.05f;

                var fwd = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fwd.name = "Forward";
                Object.DestroyImmediate(fwd.GetComponent<Collider>());
                fwd.transform.SetParent(wrapper.transform, false);
                fwd.transform.localScale = new Vector3(0.012f, 0.012f, 0.08f);
                fwd.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                return wrapper;
            };
            string handPath = isLeft ? HandLeftPrefabPath : HandRightPrefabPath;
            ArtPrefabRegistry.TryInstantiateOrFallback(handPath, handVisualGreybox, hand.transform);
            return hand;
        }

        private static void AddPoseDriver(GameObject go, Object[] refs, string map)
        {
            var follower = go.AddComponent<PoseFollower>();
            var so = new SerializedObject(follower);
            SetObjectRef(so, "positionAction", FindRef(refs, map, "Position"));
            SetObjectRef(so, "rotationAction", FindRef(refs, map, "Rotation"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Loads the input-action sub-assets, force-importing first and verifying they are fully
        /// resolved. Without this, running a build right after a reimport/domain-reload can return
        /// InputActionReferences whose <c>.action</c> is still null; FindRef then matches nothing and
        /// every binding silently serializes as null (this is what broke Phase5_Flight head tracking).
        /// </summary>
        private static bool TryLoadInputRefs(out Object[] refs)
        {
            AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceUpdate);
            refs = AssetDatabase.LoadAllAssetRepresentationsAtPath(InputAssetPath);

            // Require EVERY reference to resolve, not just one: a half-finished import (typical
            // right after a large reimport/domain reload) resolves the early sub-assets but not
            // the later ones, which slipped past the old any-one-resolved check and silently
            // serialized null bindings (e.g. Galaxy1's CockpitRecenter losing Right Hand/Recenter).
            if (!AllInputRefsResolved(refs, out string detail))
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "Input action references at:\n" + InputAssetPath +
                    "\n\nare missing or not finished importing (" + detail + "). Open the " +
                    ".inputactions asset once (or wait for the import to complete), then run " +
                    "this menu item again.", "OK");
                refs = null;
                return false;
            }
            return true;
        }

        private static bool AllInputRefsResolved(Object[] refs, out string detail)
        {
            int total = 0, unresolved = 0;
            if (refs != null)
            {
                foreach (var o in refs)
                {
                    if (o is InputActionReference r)
                    {
                        total++;
                        if (r.action == null || r.action.actionMap == null) unresolved++;
                    }
                }
            }
            detail = total == 0 ? "no references found" : $"{unresolved} of {total} unresolved";
            return total > 0 && unresolved == 0;
        }

        private static InputActionReference FindRef(Object[] refs, string map, string action)
        {
            foreach (var o in refs)
            {
                if (o is InputActionReference r && r.action != null && r.action.actionMap != null
                    && r.action.actionMap.name == map && r.action.name == action)
                    return r;
            }
            // Escalated from a warning: a missing binding produces a scene that looks built but is
            // silently broken, so make it loud enough to notice before the scene is saved.
            Debug.LogError($"[Space Samurai] Input action not found: {map}/{action}. " +
                           "Generated scene/prefab will have an unwired binding.");
            return null;
        }

        private static void SetObjectRef(SerializedObject so, string property, Object value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = value;
        }

        /// <summary>
        /// Like <see cref="TryLoadInputRefs"/> but WITHOUT the ForceUpdate reimport. The reimport is what
        /// re-nulls every InputActionReference binding in the open scene (they reserialize as fileID: 0).
        /// Loading the already-imported sub-asset representations preserves their stable IDs, so assigning
        /// them and saving normally (Ctrl+S) keeps the bindings — exactly like hand-wiring in the Inspector.
        /// </summary>
        private static bool TryLoadInputRefsNoReimport(out Object[] refs)
        {
            refs = AssetDatabase.LoadAllAssetRepresentationsAtPath(InputAssetPath);

            // Same all-resolved requirement as TryLoadInputRefs: a partially-imported state must
            // abort instead of wiring some components and silently nulling the rest.
            if (!AllInputRefsResolved(refs, out string detail))
            {
                EditorUtility.DisplayDialog("Space Samurai",
                    "Input action references at:\n" + InputAssetPath +
                    "\n\nare missing or not finished importing (" + detail + "). Open the " +
                    ".inputactions asset once (or wait for the import to complete), then run " +
                    "this menu item again.", "OK");
                refs = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Infers which action map a PoseFollower belongs to from its GameObject: the camera is the head,
        /// otherwise the hand is read from "left"/"right" in the object's own name or any ancestor's name.
        /// Returns null (and logs) when it cannot decide, so FindRef isn't called with a bad map.
        /// </summary>
        private static string InferPoseMap(GameObject go)
        {
            if (go.GetComponent<Camera>() != null || go.CompareTag("MainCamera")) return "Head";

            for (var t = go.transform; t != null; t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("left")) return "Left Hand";
                if (n.Contains("right")) return "Right Hand";
            }

            Debug.LogError($"[Space Samurai] Could not infer pose map for '{go.name}' " +
                           "(not a camera, no left/right in its name or ancestors). Skipping its PoseFollower.");
            return null;
        }

        /// <summary>
        /// Re-applies every input binding to the components in the CURRENTLY-OPEN scene, recovering from the
        /// fileID: 0 bug where rebuilding a scene re-nulls all InputActionReferences. Deliberately does NOT
        /// reimport the asset and does NOT save the scene — it assigns the stable sub-assets, marks the scene
        /// dirty, and leaves the save to the user (Ctrl+S), mirroring a manual Inspector edit.
        /// </summary>
        /// <summary>
        /// Parkour retrofit: runs <see cref="RewireOpenScene"/> (which now also adds/wires
        /// <see cref="WallClimbLocomotion"/> and the Jump action) across every scene in the build
        /// list and saves each — the additive-patch route for shipping the parkour kit into scenes
        /// built before it existed. Safe to re-run: rewiring is idempotent.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Retrofit Parkour (all build scenes)", priority = 21)]
        public static void RetrofitParkourAllScenes()
        {
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            int patched = 0;
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    entry.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                if (Object.FindAnyObjectByType<ContinuousLocomotion>(FindObjectsInactive.Include) == null)
                    continue; // no on-foot rig here (pure menu/space scenes)
                RewireOpenScene();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                patched++;
            }
            if (setup != null && setup.Length > 0)
                UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
            Debug.Log($"[XRRigBuilder] Parkour retrofit: {patched} scene(s) rewired+saved.");
        }

        [MenuItem("Tools/Space Samurai/Rewire Input Actions (open scene)", priority = 20)]
        public static void RewireOpenScene()
        {
            if (!TryLoadInputRefsNoReimport(out var refs)) return;

            int components = 0;
            var objects = new HashSet<GameObject>();

            foreach (var follower in Object.FindObjectsByType<PoseFollower>(FindObjectsInactive.Include))
            {
                string map = InferPoseMap(follower.gameObject);
                if (map == null) continue;
                var so = new SerializedObject(follower);
                SetObjectRef(so, "positionAction", FindRef(refs, map, "Position"));
                SetObjectRef(so, "rotationAction", FindRef(refs, map, "Rotation"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(follower);
                components++;
                objects.Add(follower.gameObject);
            }

            foreach (var grabber in Object.FindObjectsByType<Grabber>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(grabber);
                string map = so.FindProperty("leftHand").boolValue ? "Left Hand" : "Right Hand";
                SetObjectRef(so, "gripAction", FindRef(refs, map, "Select"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(grabber);
                components++;
                objects.Add(grabber.gameObject);
            }

            foreach (var loco in Object.FindObjectsByType<ContinuousLocomotion>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(loco);
                SetObjectRef(so, "moveAction", FindRef(refs, "Left Hand", "Move"));
                SetObjectRef(so, "turnAction", FindRef(refs, "Right Hand", "Turn"));
                SetObjectRef(so, "dashAction", FindRef(refs, "Right Hand", "Dash"));
                SetObjectRef(so, "runAction", FindRef(refs, "Right Hand", "Run"));
                SetObjectRef(so, "crouchAction", FindRef(refs, "Left Hand", "Crouch"));
                SetObjectRef(so, "jumpAction", FindRef(refs, "Left Hand", "Jump"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loco);
                components++;
                objects.Add(loco.gameObject);

                // Parkour retrofit: rigs built before wall climbing existed gain it here (the
                // rewire pass is the standing additive-patch mechanism for rig components).
                var climb = loco.GetComponent<WallClimbLocomotion>();
                if (climb == null) climb = loco.gameObject.AddComponent<WallClimbLocomotion>();
                var climbSo = new SerializedObject(climb);
                SetObjectRef(climbSo, "leftGripAction", FindRef(refs, "Left Hand", "Select"));
                SetObjectRef(climbSo, "rightGripAction", FindRef(refs, "Right Hand", "Select"));
                climbSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(climb);
                components++;
            }

            foreach (var ship in Object.FindObjectsByType<ShipController>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(ship);
                SetObjectRef(so, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
                SetObjectRef(so, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ship);
                components++;
                objects.Add(ship.gameObject);
            }

            foreach (var weapon in Object.FindObjectsByType<ShipWeaponController>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(weapon);
                SetObjectRef(so, "fireAction", FindRef(refs, "Right Hand", "Activate"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
                components++;
                objects.Add(weapon.gameObject);
            }

            foreach (var landing in Object.FindObjectsByType<LandingApproach>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(landing);
                SetObjectRef(so, "landAction", FindRef(refs, "Right Hand", "Select"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(landing);
                components++;
                objects.Add(landing.gameObject);
            }

            foreach (var recenter in Object.FindObjectsByType<CockpitRecenter>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(recenter);
                SetObjectRef(so, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recenter);
                components++;
                objects.Add(recenter.gameObject);
            }

            // Main-menu UI ray: the XRRayInteractor's UI Press action drives Button.onClick. If this
            // was null at build time (asset-import race), buttons feel "frozen" — no hover highlight
            // press, no clicks. Rewire it here, and ensure a visible LineRenderer so the player can
            // see where they're aiming (without the visual, hitting a button is guesswork).
            foreach (var interactor in Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(interactor);
                var prop = so.FindProperty("m_UIPressInput.m_InputActionReferencePerformed");
                if (prop != null)
                {
                    // Select (grip), not Activate (trigger) — 'Right Hand/Activate' is never enabled
                    // at runtime, so binding UI Press to it makes every press a no-op. See the long
                    // note in WireRightHandRayInteractorMenu.
                    prop.objectReferenceValue = FindRef(refs, "Right Hand", "Select");
                }
                // Force NewerOnly so XRI reads UIPressInput instead of the legacy XR Controller path.
                var compatProp = so.FindProperty("m_InputCompatibilityMode");
                if (compatProp != null) compatProp.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(interactor);
                components++;
                objects.Add(interactor.gameObject);
                EnsureRayLineVisual(interactor.gameObject);
            }

            // Dialogue panels are built inactive (hidden until triggered), so the search must include
            // inactive objects or every advanceAction re-nulls silently on rebuild.
            foreach (var dialogue in Object.FindObjectsByType<DialoguePlayer>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(dialogue);
                SetObjectRef(so, "advanceAction", FindRef(refs, "Left Hand", "Talk"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dialogue);
                components++;
                objects.Add(dialogue.gameObject);
            }

            foreach (var toggle in Object.FindObjectsByType<SettingsMenuToggle>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(toggle);
                SetObjectRef(so, "toggleAction", FindRef(refs, "Left Hand", "Menu"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(toggle);
                components++;
                objects.Add(toggle.gameObject);
            }

            // Weakpoint-sight (Ch7+ ability): its Hold-toggle InputActionReference is nulled by the same
            // asset-import race, so re-wire it here (WeakpointSight self-disables when locked, so this is
            // a no-op wire on scenes where the ability isn't unlocked yet — harmless).
            foreach (var weakpoint in Object.FindObjectsByType<WeakpointSight>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(weakpoint);
                SetObjectRef(so, "toggleAction", FindRef(refs, "Left Hand", "Toggle Weakpoint Sight"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weakpoint);
                components++;
                objects.Add(weakpoint.gameObject);
            }

            // Overdrive (Ch9+ ability): same asset-import-race null-out as Toggle Weakpoint Sight above
            // (OverdriveController self-disables when locked, so this is a harmless no-op wire on scenes
            // where the ability isn't unlocked yet).
            foreach (var overdrive in Object.FindObjectsByType<OverdriveController>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(overdrive);
                SetObjectRef(so, "activateAction", FindRef(refs, "Right Hand", "Activate Overdrive"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(overdrive);
                components++;
                objects.Add(overdrive.gameObject);
            }

            // Phase-step (Ch10+ ability): same asset-import-race null-out as Toggle Weakpoint
            // Sight/Overdrive above (PhaseStepController self-disables when locked, so this is a
            // harmless no-op wire on scenes where the ability isn't unlocked yet — the Ch7 bug).
            foreach (var phaseStep in Object.FindObjectsByType<PhaseStepController>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(phaseStep);
                SetObjectRef(so, "activateAction", FindRef(refs, "Right Hand", "Phase Step"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(phaseStep);
                components++;
                objects.Add(phaseStep.gameObject);
            }

            // Mirror (Ch12+ ability): same asset-import-race null-out as Toggle Weakpoint
            // Sight/Overdrive/Phase Step above (MirrorSummonController self-disables when locked, so
            // this is a harmless no-op wire on scenes where the ability isn't unlocked yet — the Ch7
            // bug).
            foreach (var mirror in Object.FindObjectsByType<MirrorSummonController>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(mirror);
                SetObjectRef(so, "activateAction", FindRef(refs, "Right Hand", "Summon Mirror"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mirror);
                components++;
                objects.Add(mirror.gameObject);
            }

            // Prompt advancers are built inactive like dialogue panels and share the Talk action.
            foreach (var prompt in Object.FindObjectsByType<PromptInputAdvancer>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(prompt);
                SetObjectRef(so, "advanceAction", FindRef(refs, "Left Hand", "Talk"));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prompt);
                components++;
                objects.Add(prompt.gameObject);
            }

            // Retrofit: older built scenes have a sword but no KatanaHolster, so a released sword
            // stayed wherever it was dropped. Wire the holster here so the sword always returns
            // to the player's hip without a full scene rebuild.
            var sceneSword = Object.FindAnyObjectByType<Sword>(FindObjectsInactive.Include);
            if (sceneSword != null)
            {
                EnsureKatanaHolster(sceneSword.GetComponent<Grabbable>());
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Space Samurai] Rewired {components} components across {objects.Count} objects — save the scene (Ctrl+S).");
        }

        /// <summary>
        /// Opens every scene under Assets/Ronin7/Scenes, runs <see cref="RewireOpenScene"/>, and
        /// saves — so a newly-added input action (e.g. Crouch) lands in already-built scenes without a
        /// full rebuild. Also a batchmode entry point (-executeMethod).
        /// </summary>
        [MenuItem("Tools/Space Samurai/Rewire Input Actions (all scenes)", priority = 21)]
        public static void RewireAllScenes()
        {
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets/Ronin7/Scenes" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RewireOpenScene();
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"[Space Samurai] Rewired + saved {path}");
            }
        }

        // --- Phase 5: per-scene techno-noir restyle -------------------------------------------------
        //
        // The scene-level half of the Sairento look. Phases 0-4 made the katana glow, the particles
        // storm, and the materials emissive; this pass darkens the *room* so those neon accents read.
        // It mirrors RewireAllScenes' proven "open every SceneAsset → mutate → save" mechanism, but
        // touches ONLY lighting RenderSettings + the skybox + two no-shadow neon accent point lights —
        // never gameplay objects, colliders, input wiring, or the XR rig. Fully idempotent: ambient/fog
        // are absolute sets (re-running is a no-op), and the accent lights are guarded by a marker root
        // so they never stack. Global VFX singletons (GraphicsDirector / CombatVfxController) are
        // DontDestroyOnLoad from the boot builders, so this pass deliberately does NOT add them per-scene.

        private const string NeonLightingRoot = "[Neon Lighting]";

        /// <summary>
        /// Scenes that play out in open space (hub star-maps, the space-combat scene, the dockable
        /// Corsair, and void dogfights) get the neon-nebula skybox; everything else is an
        /// interior / planet-surface scene that leans on the dark ambient + fog instead (its walls cover
        /// the sky anyway, so a nebula there would be wasted and occasionally peek oddly through windows).
        /// Heuristic is purely scene-NAME based and documented so it's reproducible: the four hub maps and
        /// Phase7_SpaceCombat by exact name, anything ending in "_Corsair", or any scene whose name
        /// contains one of the void keywords below (every Galaxy*_EP* space-dogfight scene is named with
        /// one of these). The human re-tunes the final mood in-headset.
        /// </summary>
        private static readonly string[] SpaceSceneKeywords =
        {
            "Pursuit", "Approach", "OrbitBreak", "NebulaEdge", "Hyperspace", "Boarding",
            "CorvetteAssault", "DockingTrench", "Interdiction", "GhostFleet", "BoneFleet",
            "DroneEscape", "StellarDive", "Exodus",
        };

        private static bool IsSpaceScene(string sceneName)
        {
            if (sceneName == "Galaxy1" || sceneName == "Galaxy2" || sceneName == "Galaxy3" ||
                sceneName == "Galaxy4" || sceneName == "Phase7_SpaceCombat")
                return true;
            if (sceneName.EndsWith("_Corsair")) return true;
            foreach (var kw in SpaceSceneKeywords)
                if (sceneName.Contains(kw)) return true;
            return false;
        }

        /// <summary>
        /// Opens every scene under Assets/Ronin7/Scenes, applies the techno-noir lighting restyle
        /// via <see cref="ApplyNeonStyleToOpenScene"/>, and saves. Mirrors <see cref="RewireAllScenes"/>;
        /// also a batchmode entry point (-executeMethod, FQ: Ronin7.EditorTools.XRRigBuilder.RestyleScenesNeon).
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Restyle Scenes — Neon", priority = 22)]
        public static void RestyleScenesNeon()
        {
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets/Ronin7/Scenes" });
            int visited = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                ApplyNeonStyleToOpenScene(sceneName);
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
                visited++;
                Debug.Log($"[Space Samurai] Restyled (neon) + saved {path} (space={IsSpaceScene(sceneName)})");
            }
            Debug.Log($"[Space Samurai] Neon restyle pass complete — {visited} scenes visited/saved.");
        }

        /// <summary>
        /// The per-scene mutation. Low-key cool-dark ambient (Flat, blue-grey) + a subtle dark/cool
        /// exponential fog so geometry still reads but the frame stays moody; the neon-nebula skybox on
        /// space scenes (interiors keep whatever skybox they had); and up to two no-shadow neon accent
        /// point lights. Kept deliberately not-too-dark — enough fill that geometry reads, the emissives
        /// + bloom do the heavy lifting. All values are absolute sets, so re-running is a no-op.
        /// </summary>
        private static void ApplyNeonStyleToOpenScene(string sceneName)
        {
            // Low-key cool-dark ambient — dark blue-grey so unlit faces sink into shadow but don't go
            // fully black (geometry must still read); the neon emissives pop against it.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.10f, 0.14f);

            // Subtle dark/cool exponential fog — gives depth + a noir haze without washing the scene out.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.10f);
            RenderSettings.fogDensity = 0.012f;

            // Space scenes get the neon void; interiors keep their existing (usually black) sky.
            if (IsSpaceScene(sceneName))
                RenderSettings.skybox = EnsureNeonNebulaSkybox();

            EnsureNeonAccentLights();
        }

        /// <summary>
        /// Adds two neon accent point lights (cyan + magenta) under a marker root near the player spawn,
        /// once. Idempotent: if the marker root already exists in the scene this is a no-op, so the pass
        /// can be re-run without ever stacking duplicate lights. Both are no-shadow Point lights within
        /// the URP mobile additional-light budget — cheap neon fill that makes emissive surfaces glow.
        /// </summary>
        private static void EnsureNeonAccentLights()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == NeonLightingRoot) return; // already present — never duplicate

            var holder = new GameObject(NeonLightingRoot); // new GameObjects land in the active scene
            AddNeonAccentLight(holder.transform, "NeonAccent_Cyan", new Vector3(4f, 3.5f, 2f), new Color(0.15f, 0.7f, 1f));
            AddNeonAccentLight(holder.transform, "NeonAccent_Magenta", new Vector3(-4f, 3.5f, -2f), new Color(1f, 0.2f, 0.7f));
        }

        private static void AddNeonAccentLight(Transform parent, string name, Vector3 localPos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 2.2f;
            light.range = 14f;
            light.shadows = LightShadows.None; // Quest budget: never add extra shadow draws
        }

        /// <summary>
        /// Idempotently registers scenes in EditorBuildSettings so runtime LoadSceneAsync(name) works
        /// without the human hand-editing Build Profiles. Preserves existing entries and their enabled
        /// flags; appends only paths not already present (enabled).
        /// </summary>
        private static void EnsureScenesInBuild(params string[] scenePaths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;
            foreach (var path in scenePaths)
            {
                bool present = false;
                foreach (var s in scenes)
                {
                    if (s.path == path) { present = true; break; }
                }
                if (!present)
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    changed = true;
                }
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

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
    /// EP09 "The Debt of Ashes" finale scene builders: Cinders Refinery (on-foot) and Extraction (space).
    /// Builds the moon Cinders refinery where Cipher confronts Khall over comms and defeats Dominion Scouts.
    /// Then the space escape with Vera Dusk as ally combatant, guarding the player's six while Kessler pilots.
    /// Both scenes wire dialogue, enemy spawners, NPC allies, and transitions per the canonical EP09 story.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // NOTE: BuildEp09DialoguePlayer lives in Ep09Builder.cs (same partial class) — shared here.

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP09 Cinders Refinery", priority = 113)]
        public static void BuildEp09CindersRefinery()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cinders refinery yard: dark bedrock floor, refinery towers with ember crater glows,
            // fire glows everywhere (unlit), heavy orange key light, dark ash sky ambient.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.6f, 0.3f); // orange key light for fire/lava
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.12f, 0.08f); // dark ash tones

            // Ash sky with volcanic haze.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.45f, 0.30f, 0.15f);
            RenderSettings.fogDensity = 0.025f;

            // Accent lights: ember glows from refinery.
            BuildAccentPointLight("EmbersLight1", new Vector3(-5f, 3f, 8f),
                new Color(1f, 0.5f, 0.1f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("EmbersLight2", new Vector3(4f, 2.5f, 15f),
                new Color(1f, 0.4f, 0.05f), intensity: 1.4f, range: 12f);

            // ---- Cinders refinery yard: bedrock floor + refinery towers + fire props ----
            var refineryGo = new GameObject("CindersRefinery");
            var refinery = refineryGo.transform;
            var darkBedrock = new Color(0.25f, 0.22f, 0.20f);
            var ashColor = new Color(0.35f, 0.30f, 0.28f);

            // Main refinery floor and ceiling.
            BuildFloorCeiling(refinery, "RefineryFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 20f), darkBedrock, ashColor);
            BuildWall(refinery, "RefineryWall_W", new Vector3(-7f, 2f, 10f), new Vector3(0.3f, 4f, 20f));
            BuildWall(refinery, "RefineryWall_E", new Vector3(7f, 2f, 10f), new Vector3(0.3f, 4f, 20f));
            BuildWall(refinery, "RefineryWall_Back", new Vector3(0f, 2f, 20f), new Vector3(14f, 4f, 0.3f));

            // Refinery towers (tall stacked cubes with ember crater glows).
            var towerColor = new Color(0.4f, 0.35f, 0.30f);
            BuildProp(refinery, "Tower1", new Vector3(-4f, 1.2f, 4f), new Vector3(1.5f, 3.5f, 1.5f), towerColor);
            BuildProp(refinery, "Tower2", new Vector3(3f, 1.2f, 6f), new Vector3(1.4f, 3.8f, 1.4f), towerColor);
            BuildProp(refinery, "Tower3", new Vector3(-2f, 1.2f, 14f), new Vector3(1.6f, 3.2f, 1.6f), towerColor);

            // Ember crater glows (unlit) at tower bases.
            var emberGlow = new Color(1f, 0.5f, 0.15f);
            AddUnlitVisual(refinery, "CraterGlow1", new Vector3(-4f, 0.2f, 4f),
                new Vector3(2f, 0.3f, 2f), PrimitiveType.Cube, emberGlow);
            AddUnlitVisual(refinery, "CraterGlow2", new Vector3(3f, 0.2f, 6f),
                new Vector3(1.8f, 0.3f, 1.8f), PrimitiveType.Cube, emberGlow);
            AddUnlitVisual(refinery, "CraterGlow3", new Vector3(-2f, 0.2f, 14f),
                new Vector3(2.2f, 0.3f, 2.2f), PrimitiveType.Cube, emberGlow);

            // Fire glow props (unlit).
            AddUnlitVisual(refinery, "FireGlow1", new Vector3(5f, 2f, 8f),
                new Vector3(0.8f, 1.2f, 0.8f), PrimitiveType.Cube, new Color(1f, 0.6f, 0.2f));
            AddUnlitVisual(refinery, "FireGlow2", new Vector3(-5f, 1.8f, 12f),
                new Vector3(0.7f, 1f, 0.7f), PrimitiveType.Cube, new Color(1f, 0.5f, 0.1f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 25f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- ALLY NPC: Vera Dusk ----
            // Vera Dusk is an ally who fights alongside the player. She has AllyCombatant, NO Health.
            var veraPos = new Vector3(-2f, 0f, 2f);
            var veraGo = InstantiateNpc(GeneratedCharFolder + "/VeraDusk.prefab", veraPos, "Vera");
            if (veraGo != null)
            {
                // Add AllyCombatant (defaults: moveSpeed=1.4, attackRange=1.6, damagePerHit=8, attackInterval=1.4, retargetInterval=1).
                var allyCombatant = veraGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                // Use defaults; no customization needed.
                allySo.ApplyModifiedPropertiesWithoutUndo();

                // CRITICAL: Do NOT add Health component to ally NPCs per AllyCombatant design.
                var storyNpc = veraGo.AddComponent<StoryNpc>();
                var vSo = new SerializedObject(storyNpc);
                vSo.FindProperty("displayName").stringValue = "Vera";
                vSo.FindProperty("remote").boolValue = false;
                vSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- KHALL HOLOGRAM (visual only, no combat) ----
            // Tall glass-material figure box + unlit red-violet glow ring at the far command platform.
            var khallPos = new Vector3(0f, 1.5f, 19f);
            var khallBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            khallBox.name = "KhallHologram";
            khallBox.transform.position = khallPos;
            khallBox.transform.localScale = new Vector3(0.6f, 2f, 0.4f);
            TintShared(khallBox.GetComponent<Renderer>(), new Color(0.1f, 0.2f, 0.3f, 0.6f));
            var khallCollider = khallBox.GetComponent<Collider>();
            if (khallCollider != null) khallCollider.isTrigger = true;

            // Red-violet glow ring (unlit).
            AddUnlitVisual(khallBox.transform.parent ?? refineryGo.transform, "KhallGlowRing", khallPos,
                new Vector3(1.2f, 0.2f, 1.2f), PrimitiveType.Cube, new Color(0.9f, 0.1f, 0.7f));

            // ---- Dialogue Players ----
            var cindersBreachDialogue = BuildEp09DialoguePlayer("Dialogue_CindersBreach", new Vector3(0f, 1.5f, 5f), "cinders_breach");
            var khallReckoningDialogue = BuildEp09DialoguePlayer("Dialogue_KhallReckoning", khallPos, "khall_reckoning");
            var scoutBarkDialogue = BuildEp09DialoguePlayer("Dialogue_ScoutBark", new Vector3(0f, 1.5f, 12f), "cinders_scouts");
            var cindersEscapeDialogue = BuildEp09DialoguePlayer("Dialogue_CindersEscape", new Vector3(0f, 1.5f, 17f), "cinders_escape");

            // ---- Enemies: Two waves of Dominion Scouts (nonLethalDisable=true) ----
            var scoutColor = new Color(0.55f, 0.55f, 0.55f); // grey

            // Wave 1: 3 scouts.
            var wave1Positions = new Vector3[]
            {
                new Vector3(-3f, 0f, 12f),
                new Vector3(0f, 0f, 13f),
                new Vector3(3f, 0f, 12.5f)
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, scoutColor);
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

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1f, 13f), 2.5f,
                new List<List<Health>> { wave1Healths }, new[] { scoutBarkDialogue });

            // Wave 2: 4 scouts.
            var wave2Positions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 15f),
                new Vector3(1f, 0f, 15.5f),
                new Vector3(3f, 0f, 16f),
                new Vector3(-1f, 0f, 16.5f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, scoutColor);
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

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 16f), 2.5f,
                new List<List<Health>> { wave2Healths }, new[] { scoutBarkDialogue });

            // Reach trigger at command platform.
            var commandReachGo = new GameObject("CommandReachPoint");
            commandReachGo.transform.position = new Vector3(0f, 1f, 19f);

            // Transition box: "RUN FOR THE PAD — LIFT OFF".
            var extractionBoxGo = BuildTransitionBox("ToExtractionBox", new Vector3(0f, 1.2f, 19.5f), "RUN FOR THE PAD — LIFT OFF",
                out var extractionBtn, out var extractionTransition);
            var etSo = new SerializedObject(extractionTransition);
            etSo.FindProperty("onFootScene").stringValue = Galaxy2Ep09ExtractionSceneName;
            etSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(extractionBtn.onClick,
                new UnityEngine.Events.UnityAction(extractionTransition.LoadOnFootScene));
            extractionBoxGo.SetActive(false);

            // ---- Mission Director ----
            // Flow: cinders_breach (auto) → reach command platform → khall_reckoning (comms) →
            // defeat wave 1 → defeat wave 2 → cinders_escape → transition to Extraction.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue cinders_breach (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Cinders Breach";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = cindersBreachDialogue;

            // Step 1: ReachTrigger — command platform.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Command Platform";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = commandReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 2: Dialogue khall_reckoning (Khall over comms).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Khall Reckoning";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = khallReckoningDialogue;

            // Step 3: DefeatWaves — Wave 1 (3 scouts).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1 (3 Scouts)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 4: DefeatWaves — Wave 2 (4 scouts).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s4.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 2 (4 Scouts)";
            s4.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 5: Dialogue cinders_escape (through fire).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Cinders Escape";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = cindersEscapeDialogue;

            // Step 6: Prompt — run for the extraction pad.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Extraction";
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep09CindersRefineryScenePath);
            EnsureScenesInBuild(Galaxy2Ep09CindersRefineryScenePath);

            Debug.Log($"[Space Samurai] EP09 Cinders Refinery scene built at {Galaxy2Ep09CindersRefineryScenePath}. " +
                      "Layout: dark bedrock refinery yard with towers + ember crater glows, heavy orange key light, ash fog. " +
                      "Vera Dusk ally NPC (AllyCombatant, NO Health). Khall hologram at command platform. " +
                      "7 steps: cinders_breach (auto) → reach command platform → khall_reckoning (comms) → " +
                      "defeat wave 1 (3 Scouts) + barks → defeat wave 2 (4 Scouts) + barks → " +
                      "cinders_escape → transition to Extraction (space).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP09 Extraction", priority = 114)]
        public static void BuildEp09Extraction()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void with ash-gray Cinders moon.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
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

            // Cinders moon: large ash-gray sphere below/behind + ember city lights (unlit).
            var cindersGo = AddUnlitVisual(universe, "Cinders Moon", new Vector3(-1200f, -600f, -2200f),
                new Vector3(600f, 600f, 600f), PrimitiveType.Sphere, new Color(0.45f, 0.40f, 0.38f));
            var cindersCollider = cindersGo.GetComponent<Collider>();
            if (cindersCollider != null) cindersCollider.isTrigger = true;

            // Ember city lights on Cinders (unlit glow spots).
            AddUnlitVisual(universe, "CindersLight1", new Vector3(-1100f, -550f, -2100f),
                new Vector3(40f, 40f, 40f), PrimitiveType.Cube, new Color(1f, 0.5f, 0.2f));
            AddUnlitVisual(universe, "CindersLight2", new Vector3(-1300f, -700f, -2300f),
                new Vector3(35f, 35f, 35f), PrimitiveType.Cube, new Color(0.9f, 0.4f, 0.1f));

            // Dead world distant (ash-gray sphere far away).
            var deadWorldGo = AddUnlitVisual(universe, "Dead World", new Vector3(2000f, 300f, 1800f),
                new Vector3(400f, 400f, 400f), PrimitiveType.Sphere, new Color(0.35f, 0.32f, 0.30f));
            var dwCollider = deadWorldGo.GetComponent<Collider>();
            if (dwCollider != null) dwCollider.isTrigger = true;

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
            // Extraction picket spawn dialogue.
            var extractionPicketDialogue = BuildEp09DialoguePlayer("Dialogue_ExtractionPicket", new Vector3(0f, 1.62f, 0.8f),
                "extraction_picket");
            var extractionPicketGo = extractionPicketDialogue.gameObject;
            extractionPicketGo.transform.SetParent(cockpit, false);
            extractionPicketGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            extractionPicketGo.transform.localRotation = Quaternion.identity;

            // Ascent epilogue (revealed when the fight clears).
            var ascentEpilogueDialogue = BuildEp09DialoguePlayer("Dialogue_AscentEpilogue", new Vector3(0f, 1.62f, 0.8f),
                "ascent_epilogue");
            var ascentGo = ascentEpilogueDialogue.gameObject;
            ascentGo.transform.SetParent(cockpit, false);
            ascentGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ascentGo.transform.localRotation = Quaternion.identity;
            var ascentSo = new SerializedObject(ascentEpilogueDialogue);
            ascentSo.FindProperty("playOnStart").boolValue = true;
            ascentSo.ApplyModifiedPropertiesWithoutUndo();
            ascentGo.SetActive(false); // Initially inactive; EncounterClearedActivator will activate it to play the epilogue.

            // ---- Enemy Encounter: 3-ship Dominion picket (pursuit-mode) ----
            var picketGo = new GameObject("DominionPicket");
            var picket = picketGo.AddComponent<GuardEncounter>();
            var picketSo = new SerializedObject(picket);
            SetObjectRef(picketSo, "player", shipCtrl);
            SetObjectRef(picketSo, "universe", universe);
            SetObjectRef(picketSo, "pool", pool);
            SetObjectRef(picketSo, "definition", enemyShipDef);
            picketSo.FindProperty("shipCount").intValue = 3;
            picketSo.FindProperty("spawnRadius").floatValue = 260f;
            picketSo.FindProperty("initialDelay").floatValue = 5f;
            picketSo.FindProperty("requiredCompletedScene").stringValue = "";
            picketSo.FindProperty("clearedFlag").stringValue = "ep09_picket_cleared";
            SetObjectRef(picketSo, "spawnDialogue", extractionPicketDialogue);
            picketSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            // When the picket is cleared, activate the epilogue dialogue (initially inactive) and set extraFlag "ep09_complete".
            var picketGateGo = new GameObject("PicketClearedGate");
            var picketGate = picketGateGo.AddComponent<EncounterClearedActivator>();
            var picketGateSo = new SerializedObject(picketGate);
            var activateProp = picketGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = ascentGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnMapBoxGo;
            picketGateSo.FindProperty("clearedFlag").stringValue = "ep09_picket_cleared";
            var extraFlagProp = picketGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "ep09_complete";
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep09ExtractionScenePath);
            EnsureScenesInBuild(Galaxy2Ep09ExtractionScenePath);

            Debug.Log($"[Space Samurai] EP09 Extraction scene built at {Galaxy2Ep09ExtractionScenePath}. " +
                      "Space over Cinders moon (ash-gray sphere below/behind with ember city lights) + dead world distant. " +
                      "Cockpit with canopy + HUD + ship guns. " +
                      "Flow: extraction_picket spawn (GuardEncounter, 3 ships, 5s delay) → " +
                      "on cleared, EncounterClearedActivator plays ascent_epilogue + sets extraFlag 'ep09_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP09 Scenes", priority = 109)]
        public static void BuildAllEp09Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP09 scenes in order: Char Spire, Spire Duel, Kethel-7 Memory, Cinders Refinery, Extraction, Galaxy 2, Galaxy 1...");
            BuildEp09CharSpire();
            BuildEp09SpireDuel();
            BuildEp09Kethel7Memory();
            BuildEp09CindersRefinery();
            BuildEp09Extraction();
            BuildGalaxy2Scene();
            BuildGalaxy1Scene();
            Debug.Log("[Space Samurai] All EP09 scenes built successfully! Galaxy 1 and Galaxy 2 hubs rebuilt to register EP09 completions.");
        }
    }
}

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
    /// EP27 "The Hollow Choir" (Galaxy 4 finale) builder for the last three scenes.
    /// - Weight of a God: contemplative memory-space on-foot scene (Cipher touches the thought-fossil,
    ///   recalls Khall's Genesis Cannon order); uses MemoryFlashbackController for dreamlike atmosphere.
    /// - Handler Arrives: escort/combat scene (Meren joins as temporary ally; Dominion assault troops;
    ///   Khall non-combat confrontation); MissionDirector gates finale on DefeatWaves clear.
    /// - Bone Fleet: SPACE dogfight finale near the Leviathan-9 corpse (4-ship GuardEncounter);
    ///   EncounterClearedActivator reveals relay_aftermath dialogue + return box; sets ep27_complete.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and constants
    /// (Galaxy4Ep27*ScenePath/Name, BuildEp27DialoguePlayer, BuildEp27OnFootShell, BuildEp27Npc, FinishEp27Scene)
    /// are declared in Ep27Builder.cs. DO NOT redefine them.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Weight Of A God", priority = 290)]
        public static void BuildEp27WeightOfAGod()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Weight of a God: contemplative memory-space with pale bone/teal fog (MemoryFlashbackController).
            var playerHealth = BuildEp27OnFootShell(refs, weapon,
                keyLight: new Color(0.65f, 0.68f, 0.70f),     // cool pale bone
                ambient: new Color(0.18f, 0.20f, 0.22f),      // soft cool
                fogColor: new Color(0.28f, 0.32f, 0.35f), fogDensity: 0.035f,
                structureName: "MemoryChamber",
                accent1: new Color(0.50f, 0.70f, 0.75f),      // teal ghost light
                accent2: new Color(0.85f, 0.88f, 0.90f),      // pale bone
                floorLight: new Color(0.55f, 0.58f, 0.60f), floorDark: new Color(0.27f, 0.30f, 0.32f),
                propTint: new Color(0.50f, 0.53f, 0.55f), out _);

            // ---- MemoryFlashbackController: apply memory-space atmosphere ----
            var memoryGo = new GameObject("MemoryFlashback");
            var memory = memoryGo.AddComponent<MemoryFlashbackController>();
            var memorySo = new SerializedObject(memory);
            memorySo.FindProperty("fogColor").colorValue = new Color(0.28f, 0.32f, 0.35f);
            memorySo.FindProperty("fogDensity").floatValue = 0.035f;
            memorySo.FindProperty("ambientColor").colorValue = new Color(0.18f, 0.20f, 0.22f);
            memorySo.ApplyModifiedPropertiesWithoutUndo();

            // ---- ThoughtFossil visual prop: glowing bioluminescent chamber center ----
            var fossilGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fossilGo.name = "ThoughtFossil";
            fossilGo.transform.position = new Vector3(0f, 1.2f, 10f);
            fossilGo.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            TintShared(fossilGo.GetComponent<Renderer>(), new Color(0.6f, 0.8f, 0.9f));
            var fossilCollider = fossilGo.GetComponent<Collider>();
            if (fossilCollider != null) fossilCollider.isTrigger = true;

            // Emit a subtle bioluminescent glow.
            var fossilGlowGo = new GameObject("FossilGlow");
            fossilGlowGo.transform.SetParent(fossilGo.transform, false);
            var glowLight = fossilGlowGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.5f, 0.75f, 0.9f);
            glowLight.intensity = 1.5f;
            glowLight.range = 8f;

            // ---- Dialogue Players ----
            var khallMemoryDialogue = BuildEp27DialoguePlayer("Dialogue_KhallMemory", new Vector3(0f, 1.5f, 5f), "khall_memory");
            var khallMemSo = new SerializedObject(khallMemoryDialogue);
            khallMemSo.FindProperty("playOnStart").boolValue = true;
            khallMemSo.ApplyModifiedPropertiesWithoutUndo();

            var weightWitnessDialogue = BuildEp27DialoguePlayer("Dialogue_WeightWitness", new Vector3(0f, 1.5f, 10f), "weight_witness");

            // Transition box: "RISE — THE HANDLER COMES".
            var riseBoxGo = BuildTransitionBox("ToHandlerBox", new Vector3(0f, 1.2f, 21.5f), "RISE — THE HANDLER COMES",
                out var riseBtn, out var riseTransition);
            var rbSo = new SerializedObject(riseTransition);
            rbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep27HandlerArrivesSceneName;
            rbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(riseBtn.onClick,
                new UnityEngine.Events.UnityAction(riseTransition.LoadOnFootScene));
            riseBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Memory";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallMemoryDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Weight Witness";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = weightWitnessDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Rise to Handler";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = riseBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp27Scene(scene, Galaxy4Ep27WeightOfAGodScenePath, Galaxy4Ep27HandlerArrivesScenePath);

            Debug.Log($"[Space Samurai] EP27 Weight Of A God scene built at {Galaxy4Ep27WeightOfAGodScenePath}. " +
                      "Contemplative memory-space (pale bone/teal fog + MemoryFlashbackController). " +
                      "ThoughtFossil glowing prop at chamber center. " +
                      "3 steps: khall_memory (auto, Genesis Cannon order recall) → weight_witness (Sallow recognition) → " +
                      "RISE — THE HANDLER COMES transition.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Handler Arrives", priority = 291)]
        public static void BuildEp27HandlerArrives()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Handler Arrives: organ-chamber palette (dark organ-stone with bioluminescent accents).
            var playerHealth = BuildEp27OnFootShell(refs, weapon,
                keyLight: new Color(0.58f, 0.55f, 0.60f),     // cool organ-stone
                ambient: new Color(0.12f, 0.10f, 0.15f),      // dark cool
                fogColor: new Color(0.20f, 0.18f, 0.25f), fogDensity: 0.022f,
                structureName: "OrganChamber",
                accent1: new Color(0.60f, 0.75f, 0.70f),      // bioluminescent teal
                accent2: new Color(0.85f, 0.82f, 0.88f),      // pale organ
                floorLight: new Color(0.52f, 0.50f, 0.55f), floorDark: new Color(0.26f, 0.24f, 0.29f),
                propTint: new Color(0.48f, 0.45f, 0.50f), out _);

            // ---- Meren NPC (temporary ally, warm earth tint) ----
            var merenGo = BuildEp27Npc("Meren", new Vector3(-1.5f, 0f, 4f), new Color(0.70f, 0.65f, 0.58f));

            // Add AllyCombatant to Meren (wired from EP09BuilderCinders precedent: add component, serialize, apply).
            if (merenGo != null)
            {
                var allyCombatant = merenGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dominion assault troops (4 lethal, dark Dominion tint, 1-2 waves) ----
            var assaultTint = new Color(0.32f, 0.30f, 0.38f);
            var assaultPositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 12f),
                new Vector3(0.5f, 0f, 12.5f),
                new Vector3(2f, 0f, 13f),
                new Vector3(-1f, 0f, 13.5f),
            };
            var assaultHealths = new List<Health>();
            foreach (var pos in assaultPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, assaultTint);
                enemy.gameObject.SetActive(false);
                assaultHealths.Add(enemy.GetComponent<Health>());
            }

            var assaultSpawner = BuildEp03WaveSpawner("AssaultSpawner", new Vector3(0f, 0.5f, 12.5f), 2f,
                new List<List<Health>> { assaultHealths },
                new[] { BuildEp27DialoguePlayer("Dialogue_HandlerBarks", new Vector3(0f, 1.5f, 12.5f), "handler_barks") });

            // ---- Khall NPC (non-combat, near far-end shuttle; distinctive cool steel tint) ----
            var khallGo = BuildEp27Npc("Khall", new Vector3(0f, 0f, 20f), new Color(0.45f, 0.48f, 0.55f));
            khallGo.gameObject.SetActive(false);

            // ---- Dialogue Players ----
            var kesslerDescentDialogue = BuildEp27DialoguePlayer("Dialogue_KesslerDescent", new Vector3(0f, 1.5f, 2f), "kessler_descent");
            var kdSo = new SerializedObject(kesslerDescentDialogue);
            kdSo.FindProperty("playOnStart").boolValue = true;
            kdSo.ApplyModifiedPropertiesWithoutUndo();

            var handoverDialogue = BuildEp27DialoguePlayer("Dialogue_Handover", new Vector3(0f, 1.5f, 8f), "handover");

            var khallConfrontDialogue = BuildEp27DialoguePlayer("Dialogue_KhallConfront", new Vector3(0f, 1.5f, 20f), "khall_confront");

            // Transition box: "LAUNCH — STOP THE CANNON".
            var launchBoxGo = BuildTransitionBox("ToBoneFleetBox", new Vector3(0f, 1.2f, 21.5f), "LAUNCH — STOP THE CANNON",
                out var launchBtn, out var launchTransition);
            var lbSo = new SerializedObject(launchTransition);
            lbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep27BoneFleetSceneName;
            lbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.LoadOnFootScene));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = kesslerDescentDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Handover";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = handoverDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Assault (4, lethal)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = assaultSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s3.FindPropertyRelative("label").stringValue = "Trigger: Activate Khall";
            var activateProp = s3.FindPropertyRelative("triggerObjects");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = khallGo;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Khall Confront";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = khallConfrontDialogue;

            // Add final prompt step for transition.
            stepsProp.arraySize = 6;
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Launch to Stop Cannon";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp27Scene(scene, Galaxy4Ep27HandlerArrivesScenePath, Galaxy4Ep27BoneFleetScenePath);

            Debug.Log($"[Space Samurai] EP27 Handler Arrives scene built at {Galaxy4Ep27HandlerArrivesScenePath}. " +
                      "Organ-chamber (cool organ-stone + bioluminescent teal). " +
                      "Meren (warm earth, AllyCombatant temporary ally). 4 Dominion assault troops (dark Dominion, lethal). " +
                      "Khall (cool steel, non-combat, far end). " +
                      "6 steps: kessler_descent (auto) → handover → defeat assault (handler_barks) → activate Khall (trigger) → " +
                      "khall_confront → LAUNCH — STOP THE CANNON transition.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Bone Fleet", priority = 292)]
        public static void BuildEp27BoneFleet()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void near the Leviathan-9 corpse (dark calcified mass) with petrified-rib debris.
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

            // Leviathan-9 corpse: dark calcified mass below.
            var leviathanGo = AddUnlitVisual(universe, "Leviathan-9", new Vector3(-1200f, -700f, -2600f),
                Vector3.one * 900f, PrimitiveType.Sphere, new Color(0.12f, 0.11f, 0.15f));
            var leviathanCollider = leviathanGo.GetComponent<Collider>();
            if (leviathanCollider != null) leviathanCollider.isTrigger = true;

            // Petrified-rib debris cluster (scattered calcified bone geometry).
            var debrisGo = AddUnlitVisual(universe, "Petrified Debris", new Vector3(2200f, 300f, -2700f),
                new Vector3(260f, 130f, 340f), PrimitiveType.Cube, new Color(0.35f, 0.33f, 0.37f));
            var debrisCollider = debrisGo.GetComponent<Collider>();
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
            // bone_fleet_barks: Kessler "three minutes" timer, plays on start.
            var boneBarksDialogue = BuildEp27DialoguePlayer("Dialogue_BoneFleetBarks", new Vector3(0f, 1.62f, 0.8f), "bone_fleet_barks");
            var bbGo = boneBarksDialogue.gameObject;
            bbGo.transform.SetParent(cockpit, false);
            bbGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            bbGo.transform.localRotation = Quaternion.identity;
            var bbSo = new SerializedObject(boneBarksDialogue);
            bbSo.FindProperty("playOnStart").boolValue = true;
            bbSo.ApplyModifiedPropertiesWithoutUndo();

            // relay_aftermath: revealed when the encounter is cleared (dialogues + heading).
            var relayDialogue = BuildEp27DialoguePlayer("Dialogue_RelayAftermath", new Vector3(0f, 1.62f, 0.8f), "relay_aftermath");
            var relayGo = relayDialogue.gameObject;
            relayGo.transform.SetParent(cockpit, false);
            relayGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            relayGo.transform.localRotation = Quaternion.identity;
            var relaySo = new SerializedObject(relayDialogue);
            relaySo.FindProperty("playOnStart").boolValue = true;
            relaySo.ApplyModifiedPropertiesWithoutUndo();
            relayGo.SetActive(false);

            // ---- Enemy Encounter: 4-ship bone fleet guard ----
            var boneEncounterGo = new GameObject("BoneFleetEncounter");
            var boneEncounter = boneEncounterGo.AddComponent<GuardEncounter>();
            var beSo = new SerializedObject(boneEncounter);
            SetObjectRef(beSo, "player", shipCtrl);
            SetObjectRef(beSo, "universe", universe);
            SetObjectRef(beSo, "pool", pool);
            SetObjectRef(beSo, "definition", enemyShipDef);
            beSo.FindProperty("shipCount").intValue = 4;
            beSo.FindProperty("spawnRadius").floatValue = 280f;
            beSo.FindProperty("initialDelay").floatValue = 6f;
            beSo.FindProperty("requiredCompletedScene").stringValue = "";
            beSo.FindProperty("clearedFlag").stringValue = "ep27_relay_disabled";
            SetObjectRef(beSo, "spawnDialogue", null); // no spawn dialogue for bone fleet
            beSo.ApplyModifiedPropertiesWithoutUndo();

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (ep27_complete only).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep27_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Gate the finale on the bone fleet fight: EncounterClearedActivator reveals relay_aftermath + return box.
            var boneGateGo = new GameObject("BoneClearedGate");
            var boneGate = boneGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(boneGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = relayGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep27_relay_disabled";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of Galaxy 4 EP27: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep27BoneFleetScenePath);
            EnsureScenesInBuild(Galaxy4Ep27BoneFleetScenePath);

            Debug.Log($"[Space Samurai] EP27 Bone Fleet scene built at {Galaxy4Ep27BoneFleetScenePath}. " +
                      "SPACE finale near Leviathan-9 corpse (dark calcified mass + petrified-rib debris). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: bone_fleet_barks (auto, Kessler 3-minute timer) → 4-ship GuardEncounter (ep27_relay_disabled) → on cleared, " +
                      "EncounterClearedActivator reveals relay_aftermath dialogue + RETURN — TO THE STARS. " +
                      "Return box wired to CampaignFlagSetter (ep27_complete ONLY, no ally recruit) + ReturnToSpace. " +
                      "GALAXY 4 EP27 FINALE (last scene).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP27 Scenes", priority = 293)]
        public static void BuildAllEp27Scenes()
        {
            BuildEp27BoneGates();
            BuildEp27WetChambers();
            BuildEp27MarrowArchive();
            BuildEp27WeightOfAGod();
            BuildEp27HandlerArrives();
            BuildEp27BoneFleet();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP27 scenes built + inputs rewired. GALAXY 4 EP27 COMPLETE.");
        }
    }
}

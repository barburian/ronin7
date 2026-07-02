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
    /// EP08 "The Architect of Mercy" scene builders. Builds the Vel Keth descent mission where Cipher
    /// encounters Mera Voss, discovers proof of pre-wipe humanity, and retrieves the neural fragment
    /// from Apex Station. Wires all MissionDirector steps, zero-gravity mechanics, enemy waves,
    /// and NPC interactions across Cargo Hold and Canyon Narrows scenes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep08CargoHoldScenePath = SceneFolder + "/Galaxy1_EP08_CargoHold.unity";

        private static readonly string Galaxy1Ep08CargoHoldSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08CargoHoldScenePath);

        private const string MeraVossPrefabPath = GeneratedCharFolder + "/MeraVoss.prefab";

        /// <summary>Shorthand for building a DialoguePlayer with EP08 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep08" and loads lines from Ep08Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp08DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep08Lines.Get(setId), advanceRef, setId, clipPrefix: "ep08");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Cargo Hold", priority = 99)]
        public static void BuildEp08CargoHold()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cargo hold interior: metal floor/ceiling/walls, stacked crate props, cool blue-ish lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.75f, 0.85f); // cool blue key
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.22f); // cool dim tones

            // Cool blue cargo hold atmosphere.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.40f, 0.48f);
            RenderSettings.fogDensity = 0.02f;

            // Cool accent lights for cargo hold.
            BuildAccentPointLight("CargoLight1", new Vector3(-3.5f, 2.5f, 8f),
                new Color(0.5f, 0.7f, 0.9f), intensity: 1.3f, range: 12f);
            BuildAccentPointLight("CargoLight2", new Vector3(3.5f, 2.5f, 16f),
                new Color(0.45f, 0.65f, 0.85f), intensity: 1.2f, range: 11f);

            // ---- Cargo hold: metal interior (x[-6,6], z[0,20]) with stacked crates ----
            var cargoGo = new GameObject("CargoHoldInterior");
            var cargo = cargoGo.transform;
            var metalFloorColor = new Color(0.28f, 0.30f, 0.32f);
            var metalCeilColor = new Color(0.15f, 0.17f, 0.19f);

            // Main cargo floor and ceiling.
            BuildFloorCeiling(cargo, "CargoFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), metalFloorColor, metalCeilColor);
            BuildWall(cargo, "CargoWall_W", new Vector3(-6f, 1.6f, 10f), new Vector3(0.2f, 3.2f, 20f));
            BuildWall(cargo, "CargoWall_E", new Vector3(6f, 1.6f, 10f), new Vector3(0.2f, 3.2f, 20f));
            BuildWall(cargo, "CargoWall_Back", new Vector3(0f, 1.6f, 20f), new Vector3(12f, 3.2f, 0.2f));

            // Stacked crate props (varied heights for visual interest).
            var crateColor = new Color(0.42f, 0.38f, 0.35f);
            var cratePositions = new Vector3[]
            {
                new Vector3(-4f, 0.5f, 3f), new Vector3(-2f, 0.5f, 5f), new Vector3(0f, 1.2f, 2f),
                new Vector3(3f, 0.5f, 6f), new Vector3(5f, 1.0f, 4f), new Vector3(-3f, 0.8f, 12f),
                new Vector3(2f, 0.6f, 14f), new Vector3(4f, 1.2f, 10f), new Vector3(-5f, 0.7f, 18f),
                new Vector3(3.5f, 0.9f, 16f)
            };
            for (int i = 0; i < cratePositions.Length; i++)
            {
                BuildProp(cargo, $"Crate{i}", cratePositions[i], new Vector3(1.2f, 1.2f, 1.2f), crateColor);
            }

            // Wall-mounted rails for zero-G hold points (2 rails along walls).
            BuildProp(cargo, "RailWest1", new Vector3(-5.8f, 1.5f, 5f), new Vector3(0.15f, 0.15f, 4f), metalFloorColor);
            BuildProp(cargo, "RailWest2", new Vector3(-5.8f, 1.5f, 15f), new Vector3(0.15f, 0.15f, 4f), metalFloorColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds + zero-G locomotion.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // Zero-G Grab Locomotion: wire hand transforms and grip action.
            var zeroGLocomotion = rig.AddComponent<ZeroGGrabLocomotion>();
            var zeroGSo = new SerializedObject(zeroGLocomotion);
            var vrRig = rig.GetComponent<VRRig>();
            if (vrRig != null)
            {
                var leftHand = vrRig.LeftHand;
                var rightHand = vrRig.RightHand;
                SetObjectRef(zeroGSo, "leftHand", leftHand);
                SetObjectRef(zeroGSo, "rightHand", rightHand);
            }
            // Left-hand grip pulls; the right hand keeps the sword (Right Hand Select).
            var gripRef = FindRef(refs, "Left Hand", "Select");
            if (gripRef != null) SetObjectRef(zeroGSo, "gripAction", gripRef);
            zeroGSo.FindProperty("grabLayerMask").intValue = LayerMask.GetMask("Default");
            zeroGSo.ApplyModifiedPropertiesWithoutUndo();

            // Zero-G Combat Volume box trigger covering the cargo hold.
            var zeroGVolume = new GameObject("ZeroGCombatVolume");
            zeroGVolume.transform.SetParent(cargoGo.transform, false);
            zeroGVolume.transform.localPosition = new Vector3(0f, 1f, 10f);
            var zeroGCollider = zeroGVolume.AddComponent<BoxCollider>();
            zeroGCollider.size = new Vector3(12f, 3f, 20f);
            zeroGCollider.isTrigger = true;
            var zeroGComp = zeroGVolume.AddComponent<ZeroGCombatVolume>();
            var zeroGCSo = new SerializedObject(zeroGComp);
            zeroGCSo.FindProperty("driftDamping").floatValue = 0.95f;
            zeroGCSo.ApplyModifiedPropertiesWithoutUndo();

            // ZeroGHandle markers on crates and rails (8-10 total).
            var handlePositions = new Vector3[]
            {
                cratePositions[0], cratePositions[2], cratePositions[4],
                cratePositions[6], cratePositions[8],
                new Vector3(-5.8f, 1.5f, 5f), new Vector3(-5.8f, 1.5f, 15f),
                cratePositions[1], cratePositions[3]
            };
            for (int i = 0; i < handlePositions.Length; i++)
            {
                var handleGo = new GameObject($"Handle{i}");
                handleGo.transform.SetParent(cargoGo.transform, false);
                handleGo.transform.localPosition = handlePositions[i];
                var handleCollider = handleGo.AddComponent<SphereCollider>();
                handleCollider.radius = 0.3f;
                handleCollider.isTrigger = true;
                handleGo.AddComponent<ZeroGHandle>();
            }

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Mera Voss at the airlock entry.
            var meraPos = new Vector3(0f, 1f, 2f);
            var meraGo = InstantiateNpc(MeraVossPrefabPath, meraPos, "Mera");
            if (meraGo != null)
            {
                var meraNpc = meraGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(meraNpc);
                mSo.FindProperty("displayName").stringValue = "Mera";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var introspectionDialogue = BuildEp08DialoguePlayer("Dialogue_IntrospectionHold", new Vector3(0f, 1.5f, 5f), "introspection_hold");
            var droneBreachDialogue = BuildEp08DialoguePlayer("Dialogue_DroneBreach", new Vector3(0f, 1.5f, 8f), "drone_breach");
            var meraParleyDialogue = BuildEp08DialoguePlayer("Dialogue_MeraParley", meraPos, "mera_parley", talkRef);
            var enforcerAmbushDialogue = BuildEp08DialoguePlayer("Dialogue_EnforcerAmbush", new Vector3(0f, 1.5f, 12f), "enforcer_ambush");
            var descentTermsDialogue = BuildEp08DialoguePlayer("Dialogue_DescentTerms", meraPos, "descent_terms", talkRef);

            // ---- Enemies: Wave 1 = 2 Lotus Drones (crimson-grey), Wave 2 = 3 Lotus Enforcers ----
            var droneColor = new Color(0.65f, 0.48f, 0.50f); // crimson-grey
            var enforcerColor = new Color(0.70f, 0.55f, 0.35f); // crimson-gold

            // Wave 1: 2 Lotus Drones at varied heights.
            var wave1Positions = new Vector3[]
            {
                new Vector3(-2.5f, 1.5f, 12f),
                new Vector3(2.5f, 2.0f, 14f)
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
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
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1.5f, 13f), 3f,
                new List<List<Health>> { wave1Healths }, new[] { droneBreachDialogue });

            // Wave 2: 3 Lotus Enforcers.
            var wave2Positions = new Vector3[]
            {
                new Vector3(-2f, 0.5f, 16f),
                new Vector3(0f, 0.5f, 17.5f),
                new Vector3(2f, 0.5f, 16f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerColor);
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

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 17f), 3f,
                new List<List<Health>> { wave2Healths }, new[] { enforcerAmbushDialogue });

            // Transition box: "ENTER APEX STATION" (Canyon Narrows cut — CargoHold now chains straight to Apex Vault).
            var canyonBoxGo = BuildTransitionBox("ToApexBox", new Vector3(0f, 1.2f, 19.5f), "ENTER APEX STATION",
                out var canyonBtn, out var canyonTransition);
            var ctSo = new SerializedObject(canyonTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy1Ep08ApexVaultSceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(canyonBtn.onClick,
                new UnityEngine.Events.UnityAction(canyonTransition.LoadOnFootScene));
            canyonBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue introspection_hold (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Introspection Hold";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = introspectionDialogue;

            // Step 1: DefeatWaves — Wave 1 (2 Lotus Drones).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1 (2 Lotus Drones)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 2: Dialogue mera_parley (talk-gated at Mera).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Mera Parley";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = meraParleyDialogue;

            // Step 3: DefeatWaves — Wave 2 (3 Lotus Enforcers).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 2 (3 Lotus Enforcers)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 4: Dialogue descent_terms (talk-gated at Mera).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Descent Terms";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = descentTermsDialogue;

            // Step 5: Prompt — enter apex station.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Enter Apex Station";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = canyonBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08CargoHoldScenePath);
            EnsureScenesInBuild(Galaxy1Ep08CargoHoldScenePath);

            Debug.Log($"[Space Samurai] EP08 Cargo Hold scene built at {Galaxy1Ep08CargoHoldScenePath}. " +
                      "Layout: metal cargo hold interior with stacked crates, wall rails, blue-tinted lighting. " +
                      "Zero-G combat volume with grab-point handles. Mera Voss NPC at airlock entry. " +
                      "6 steps: introspection_hold (auto) → defeat wave 1 (2 Drones) + barks → " +
                      "mera_parley (talk) at Mera → defeat wave 2 (3 Enforcers) + barks → " +
                      "descent_terms (talk) at Mera → transition to Apex Station.");
        }
    }
}

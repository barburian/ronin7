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
    /// EP22 "The Void Inheritors" scene builders for the first three scenes aboard the derelict generation ship.
    /// - Cargo Approach: zero-G cargo hold with 4 Security Mechs (1 wave)
    /// - Vault of Ghosts: ancient vault with 6 Holo Sentries (1 wave) + Irene NPC
    /// - Children Below: flooded maintenance with 4 Repair Drones (1 wave) + Irene + Lira NPCs
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP22 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep22" and loads lines from Ep22Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp22DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep22Lines.Get(setId), advanceRef, setId, clipPrefix: "ep22");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Cargo Approach", priority = 240)]
        public static void BuildEp22CargoApproach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cargo Approach: derelict cargo hold in crimson nebula light (zero-G scene).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.35f, 0.35f); // crimson/dark red key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.10f, 0.10f); // dark red ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.12f, 0.12f);
            RenderSettings.fogDensity = 0.020f;

            // Two accent lights: crimson and steel-blue.
            BuildAccentPointLight("CargoLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.30f, 0.30f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("CargoLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.5f, 0.6f, 0.8f), intensity: 0.70f, range: 9f);

            // ---- Cargo hold floor and structure ----
            var cargoGo = new GameObject("CargoHold");
            var cargo = cargoGo.transform;
            var metalLight = new Color(0.40f, 0.38f, 0.40f);
            var metalDark = new Color(0.24f, 0.22f, 0.24f);

            // Main cargo floor (12 x 20).
            BuildFloorCeiling(cargo, "CargoFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), metalLight, metalDark);

            // Cargo walls.
            BuildWall(cargo, "CargoWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(cargo, "CargoWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Crate props (steel color).
            var steelCrate = new Color(0.45f, 0.45f, 0.45f);
            BuildProp(cargo, "Crate1", new Vector3(-3f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), steelCrate);
            BuildProp(cargo, "Crate2", new Vector3(3f, 0.6f, 9f), new Vector3(1.2f, 1.2f, 1.2f), steelCrate);
            BuildProp(cargo, "Crate3", new Vector3(-3f, 0.6f, 13f), new Vector3(1.2f, 1.2f, 1.2f), steelCrate);
            BuildProp(cargo, "Crate4", new Vector3(3f, 0.6f, 16f), new Vector3(1.2f, 1.2f, 1.2f), steelCrate);

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

            // ---- Zero-G Grab Locomotion ----
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
            var gripRef = FindRef(refs, "Left Hand", "Select");
            if (gripRef != null) SetObjectRef(zeroGSo, "gripAction", gripRef);
            zeroGSo.FindProperty("grabLayerMask").intValue = LayerMask.GetMask("Default");
            zeroGSo.ApplyModifiedPropertiesWithoutUndo();

            // Zero-G Combat Volume box trigger.
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

            // ZeroGHandle markers on crates and rail points.
            var handlePositions = new Vector3[]
            {
                new Vector3(-3f, 0.6f, 6f), new Vector3(3f, 0.6f, 9f),
                new Vector3(-3f, 0.6f, 13f), new Vector3(3f, 0.6f, 16f),
                new Vector3(-5.8f, 1.5f, 5f), new Vector3(-5.8f, 1.5f, 15f),
                new Vector3(5.8f, 1.5f, 8f), new Vector3(5.8f, 1.5f, 14f)
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

            // ---- 1 Wave of 4 Security Mechs ----
            var mechColor = new Color(0.45f, 0.47f, 0.50f); // steel-grey tint
            var mechPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(1.5f, 0f, 6f),
                new Vector3(-1.5f, 0f, 10f),
                new Vector3(1.5f, 0f, 10f)
            };

            var mechWaveHealths = new List<Health>();
            foreach (var pos in mechPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, mechColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                mechWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var mechSpawner = BuildEp03WaveSpawner("MechSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { mechWaveHealths },
                new[] { BuildEp22DialoguePlayer("Dialogue_CargoBarks", new Vector3(0f, 1.5f, 8f), "cargo_barks") });

            // ---- Dialogue Players ----
            var approachDialogue = BuildEp22DialoguePlayer("Dialogue_Approach", new Vector3(0f, 1.5f, 2f), "approach");
            var apSo = new SerializedObject(approachDialogue);
            apSo.FindProperty("playOnStart").boolValue = true;
            apSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "ENTER THE BRIDGE".
            var bridgeBoxGo = BuildTransitionBox("EnterBridgeBox", new Vector3(0f, 1.2f, 20.5f), "ENTER THE BRIDGE",
                out var bridgeBtn, out var bridgeTransition);
            var bbSo = new SerializedObject(bridgeTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22VaultOfGhostsSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(bridgeBtn.onClick,
                new UnityEngine.Events.UnityAction(bridgeTransition.LoadOnFootScene));
            bridgeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue approach (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Approach";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = approachDialogue;

            // Step 1: DefeatWaves — 4 security mechs.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Security Mechs (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = mechSpawner;

            // Step 2: Prompt — transition to Bridge.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Enter the Bridge";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = bridgeBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22CargoApproachScenePath);
            EnsureScenesInBuild(Galaxy3Ep22CargoApproachScenePath, Galaxy3Ep22VaultOfGhostsScenePath);

            Debug.Log($"[Space Samurai] EP22 Cargo Approach scene built at {Galaxy3Ep22CargoApproachScenePath}. " +
                      "Zero-G cargo hold with crimson nebula lighting, metal floor/walls, 4 crates, ZeroGGrabLocomotion + ZeroGCombatVolume + 8 ZeroGHandle markers. " +
                      "4 Security Mechs (steel-grey tint, nonLethal). " +
                      "3 steps: approach (auto) → defeat 4 mechs (cargo_barks bark) → enter bridge.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Vault of Ghosts", priority = 241)]
        public static void BuildEp22VaultOfGhosts()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault of Ghosts: ancient vault with amber/steel lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.55f, 0.5f); // amber/gold key light
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.12f); // warm dark ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.22f, 0.18f, 0.16f);
            RenderSettings.fogDensity = 0.016f;

            // Two accent lights: amber and cyan.
            BuildAccentPointLight("VaultLight1", new Vector3(-3f, 2f, 8f),
                new Color(1f, 0.7f, 0.4f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("VaultLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.5f, 0.8f, 1f), intensity: 0.70f, range: 9f);

            // ---- Vault floor and structure ----
            var vaultGo = new GameObject("Vault");
            var vault = vaultGo.transform;
            var vaultMetal = new Color(0.48f, 0.45f, 0.42f);
            var vaultDark = new Color(0.28f, 0.25f, 0.22f);

            // Main vault floor (12 x 21).
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), vaultMetal, vaultDark);

            // Vault walls.
            BuildWall(vault, "VaultWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(vault, "VaultWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Data-terminal cyan props.
            var terminalColor = new Color(0.5f, 0.8f, 1f);
            BuildProp(vault, "Terminal1", new Vector3(-3f, 1.5f, 8f), new Vector3(0.6f, 2f, 0.6f), terminalColor);
            BuildProp(vault, "Terminal2", new Vector3(3f, 1.5f, 12f), new Vector3(0.6f, 2f, 0.6f), terminalColor);

            // Cryo-chamber pale props.
            var cryoColor = new Color(0.7f, 0.7f, 0.72f);
            BuildProp(vault, "Cryo1", new Vector3(-2.5f, 0.8f, 15f), new Vector3(1f, 1.8f, 0.8f), cryoColor);
            BuildProp(vault, "Cryo2", new Vector3(2.5f, 0.8f, 16f), new Vector3(1f, 1.8f, 0.8f), cryoColor);

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

            // ---- Irene StoryNpc ----
            var ireneGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ireneGo.name = "Irene";
            Object.DestroyImmediate(ireneGo.GetComponent<Collider>());
            ireneGo.transform.position = new Vector3(0f, 0f, 3f);
            ireneGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ireneGo.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.50f)); // amber/warm tint
            var ireneNpc = ireneGo.AddComponent<StoryNpc>();
            var inSo = new SerializedObject(ireneNpc);
            inSo.FindProperty("displayName").stringValue = "Irene Sols";
            inSo.FindProperty("remote").boolValue = false;
            inSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 1 Wave of 6 Holo Sentries ----
            var sentryColor = new Color(0.5f, 0.7f, 0.9f); // cyan-blue tint
            var sentryPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
                new Vector3(0f, 0f, 13f)
            };

            var sentryWaveHealths = new List<Health>();
            foreach (var pos in sentryPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, sentryColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                sentryWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var sentrySpawner = BuildEp03WaveSpawner("SentrySpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { sentryWaveHealths },
                new[] { BuildEp22DialoguePlayer("Dialogue_VaultBarks", new Vector3(0f, 1.5f, 8f), "vault_barks") });

            // ---- Dialogue Players ----
            var recognitionDialogue = BuildEp22DialoguePlayer("Dialogue_Recognition", new Vector3(0f, 1.5f, 2f), "recognition");
            var rgSo = new SerializedObject(recognitionDialogue);
            rgSo.FindProperty("playOnStart").boolValue = true;
            rgSo.ApplyModifiedPropertiesWithoutUndo();

            var vaultAftermathDialogue = BuildEp22DialoguePlayer("Dialogue_VaultAftermath", new Vector3(0f, 1.5f, 14f), "vault_aftermath");

            // Transition box: "DOWN TO THE COLONY".
            var colonyBoxGo = BuildTransitionBox("ToColonyBox", new Vector3(0f, 1.2f, 21.5f), "DOWN TO THE COLONY",
                out var colonyBtn, out var colonyTransition);
            var cbSo = new SerializedObject(colonyTransition);
            cbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22ChildrenBelowSceneName;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(colonyBtn.onClick,
                new UnityEngine.Events.UnityAction(colonyTransition.LoadOnFootScene));
            colonyBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue recognition (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Recognition";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = recognitionDialogue;

            // Step 1: DefeatWaves — 6 sentries.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Holo Sentries (6)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = sentrySpawner;

            // Step 2: Dialogue vault_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Vault Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = vaultAftermathDialogue;

            // Step 3: Prompt — transition to Colony.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Down to the Colony";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = colonyBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22VaultOfGhostsScenePath);
            EnsureScenesInBuild(Galaxy3Ep22VaultOfGhostsScenePath, Galaxy3Ep22ChildrenBelowScenePath);

            Debug.Log($"[Space Samurai] EP22 Vault of Ghosts scene built at {Galaxy3Ep22VaultOfGhostsScenePath}. " +
                      "Ancient vault with amber/steel lighting, metal floor/walls, cyan terminals, pale cryo-chambers. " +
                      "Irene Sols NPC (amber/warm tint, no Health). " +
                      "6 Holo Sentries (cyan-blue tint, nonLethal). " +
                      "4 steps: recognition (auto) → defeat 6 sentries (vault_barks bark) → vault_aftermath dialogue → down to colony.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP22 Children Below", priority = 242)]
        public static void BuildEp22ChildrenBelow()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Children Below: flooded maintenance with dark teal lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.4f, 0.5f, 0.55f); // dark teal key light
            light.intensity = 0.42f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.14f); // teal ambient

            // Fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.14f, 0.20f, 0.22f);
            RenderSettings.fogDensity = 0.026f;

            // Two accent lights: teal and dim white.
            BuildAccentPointLight("FloodLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.4f, 0.8f, 0.8f), intensity: 0.75f, range: 10f);
            BuildAccentPointLight("FloodLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.6f, 0.6f, 0.65f), intensity: 0.70f, range: 9f);

            // ---- Farm stack floor and structure ----
            var farmGo = new GameObject("FarmStack");
            var farm = farmGo.transform;
            var farmMetal = new Color(0.42f, 0.45f, 0.48f);
            var farmDark = new Color(0.22f, 0.25f, 0.28f);

            // Main farm floor (11 x 20).
            BuildFloorCeiling(farm, "FarmFloor", new Vector3(0f, 0f, 10f), new Vector3(11f, 0f, 20f), farmMetal, farmDark);

            // Farm walls.
            BuildWall(farm, "FarmWall_W", new Vector3(-5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(farm, "FarmWall_E", new Vector3(5.5f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Hydroponic green props.
            var hydroColor = new Color(0.4f, 0.6f, 0.4f);
            BuildProp(farm, "Hydro1", new Vector3(-2.5f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), hydroColor);
            BuildProp(farm, "Hydro2", new Vector3(0f, 1.2f, 11f), new Vector3(0.8f, 1.5f, 0.8f), hydroColor);
            BuildProp(farm, "Hydro3", new Vector3(2.5f, 1.2f, 14f), new Vector3(0.8f, 1.5f, 0.8f), hydroColor);

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

            // ---- Irene StoryNpc ----
            var ireneGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ireneGo.name = "Irene";
            Object.DestroyImmediate(ireneGo.GetComponent<Collider>());
            ireneGo.transform.position = new Vector3(-1.5f, 0f, 3f);
            ireneGo.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(ireneGo.GetComponent<Renderer>(), new Color(0.6f, 0.55f, 0.50f)); // amber/warm tint
            var ireneNpc = ireneGo.AddComponent<StoryNpc>();
            var inSo = new SerializedObject(ireneNpc);
            inSo.FindProperty("displayName").stringValue = "Irene Sols";
            inSo.FindProperty("remote").boolValue = false;
            inSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Lira StoryNpc (small scale) ----
            var liraGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            liraGo.name = "Lira";
            Object.DestroyImmediate(liraGo.GetComponent<Collider>());
            liraGo.transform.position = new Vector3(1.5f, 0f, 3f);
            liraGo.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
            TintShared(liraGo.GetComponent<Renderer>(), new Color(0.7f, 0.65f, 0.6f)); // pale tan tint
            var liraNpc = liraGo.AddComponent<StoryNpc>();
            var lnSo = new SerializedObject(liraNpc);
            lnSo.FindProperty("displayName").stringValue = "Lira";
            lnSo.FindProperty("remote").boolValue = false;
            lnSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 1 Wave of 4 Repair Drones ----
            var droneColor = new Color(0.6f, 0.45f, 0.35f); // copper/brown tint
            var dronePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 12f),
                new Vector3(1.5f, 0f, 13f)
            };

            var droneWaveHealths = new List<Health>();
            foreach (var pos in dronePositions)
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

            var droneSpawner = BuildEp03WaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { droneWaveHealths },
                new[] { BuildEp22DialoguePlayer("Dialogue_FloodBarks", new Vector3(0f, 1.5f, 8f), "flood_barks") });

            // ---- Dialogue Players ----
            var childrenBelowDialogue = BuildEp22DialoguePlayer("Dialogue_ChildrenBelow", new Vector3(0f, 1.5f, 2f), "children_below");
            var cbSo = new SerializedObject(childrenBelowDialogue);
            cbSo.FindProperty("playOnStart").boolValue = true;
            cbSo.ApplyModifiedPropertiesWithoutUndo();

            var childrenAftermathDialogue = BuildEp22DialoguePlayer("Dialogue_ChildrenAftermath", new Vector3(0f, 1.5f, 14f), "children_aftermath");

            // Transition box: "TO THE DATA-HUB".
            var dataHubBoxGo = BuildTransitionBox("ToDataHubBox", new Vector3(0f, 1.2f, 20.5f), "TO THE DATA-HUB",
                out var dataHubBtn, out var dataHubTransition);
            var dhbSo = new SerializedObject(dataHubTransition);
            dhbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep22WhatRonin1LeftSceneName;
            dhbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dataHubBtn.onClick,
                new UnityEngine.Events.UnityAction(dataHubTransition.LoadOnFootScene));
            dataHubBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue children_below (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Children Below";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = childrenBelowDialogue;

            // Step 1: DefeatWaves — 4 drones.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Repair Drones (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Dialogue children_aftermath.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Children Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = childrenAftermathDialogue;

            // Step 3: Prompt — transition to Data-Hub.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Data-Hub";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = dataHubBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep22ChildrenBelowScenePath);
            EnsureScenesInBuild(Galaxy3Ep22ChildrenBelowScenePath, Galaxy3Ep22WhatRonin1LeftScenePath);

            Debug.Log($"[Space Samurai] EP22 Children Below scene built at {Galaxy3Ep22ChildrenBelowScenePath}. " +
                      "Flooded maintenance with dark teal lighting, metal floor/walls, green hydroponic props. " +
                      "Irene Sols NPC (amber/warm tint, no Health) + Lira NPC (small scale, pale tan tint, no Health). " +
                      "4 Repair Drones (copper/brown tint, nonLethal). " +
                      "4 steps: children_below (auto) → defeat 4 drones (flood_barks bark) → children_aftermath dialogue → to data-hub.");
        }
    }
}

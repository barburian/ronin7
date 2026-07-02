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
    /// EP12 "The Memory Merchant" on-foot scene builders. Builds four core episodes:
    /// - Shard Market: zero-gravity bazaar commons with Morrigan's recognition duel
    /// - Vault: excavated mining-vault passage with memory-cell racks, Morrigan as ally
    /// - Cutter: tight cutter interior corridor with Shardborn enemies (HesitantAttacker mechanic)
    /// - The Shard: memory-space orphanage hallucination with MemoryEchoVignette and phantom echoes
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP12 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep12" and loads lines from Ep12Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp12DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep12Lines.Get(setId), advanceRef, setId, clipPrefix: "ep12");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 Shard Market", priority = 129)]
        public static void BuildEp12ShardMarket()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Shard Market: zero-gravity memory-bazaar commons (visual flavor only; normal locomotion).
            // Cool blue-lit interior with floating-stall set dressing.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.55f, 0.70f); // cool blue key light
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.24f, 0.30f); // cool dim ambient

            // Light blue-grey fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.22f, 0.30f);
            RenderSettings.fogDensity = 0.020f;

            // Two blue accent point lights.
            BuildAccentPointLight("CommonsLight1", new Vector3(-5f, 2.5f, 8f),
                new Color(0.4f, 0.6f, 0.8f), intensity: 1.0f, range: 11f);
            BuildAccentPointLight("CommonsLight2", new Vector3(5f, 2f, 14f),
                new Color(0.3f, 0.55f, 0.75f), intensity: 0.95f, range: 10f);

            // ---- Shard Market Commons: floating-stall set dressing ----
            var commonsGo = new GameObject("ShardCommons");
            var commons = commonsGo.transform;
            var lightGrey = new Color(0.40f, 0.40f, 0.42f);
            var darkGrey = new Color(0.25f, 0.25f, 0.28f);

            // Main commons floor.
            BuildFloorCeiling(commons, "CommonsFloor", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), lightGrey, darkGrey);

            // Cartridge rack boxes (tall thin boxes ~0.6x2x1.2) tinted dark blue-grey.
            var cartridgeRackColor = new Color(0.28f, 0.32f, 0.38f);
            BuildProp(commons, "CartridgeRack1", new Vector3(-4f, 1f, 5f), new Vector3(0.6f, 2f, 1.2f), cartridgeRackColor);
            BuildProp(commons, "CartridgeRack2", new Vector3(4f, 1f, 7f), new Vector3(0.6f, 2f, 1.2f), cartridgeRackColor);
            BuildProp(commons, "CartridgeRack3", new Vector3(-3f, 1f, 12f), new Vector3(0.6f, 2f, 1.2f), cartridgeRackColor);
            BuildProp(commons, "CartridgeRack4", new Vector3(3f, 1f, 14f), new Vector3(0.6f, 2f, 1.2f), cartridgeRackColor);

            // Small unlit cyan cube "neural cartridge" glow props.
            var neuralCartProp1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neuralCartProp1.name = "NeuralCartridge1";
            Object.DestroyImmediate(neuralCartProp1.GetComponent<Collider>());
            neuralCartProp1.transform.SetParent(commons, false);
            neuralCartProp1.transform.position = new Vector3(-2f, 0.8f, 8f);
            neuralCartProp1.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            neuralCartProp1.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.3f, 0.8f, 1f));

            var neuralCartProp2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neuralCartProp2.name = "NeuralCartridge2";
            Object.DestroyImmediate(neuralCartProp2.GetComponent<Collider>());
            neuralCartProp2.transform.SetParent(commons, false);
            neuralCartProp2.transform.position = new Vector3(2f, 0.8f, 13f);
            neuralCartProp2.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            neuralCartProp2.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.3f, 0.8f, 1f));

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
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- Dialogue Players ----
            var transitIntroDialogue = BuildEp12DialoguePlayer("Dialogue_TransitIntro", new Vector3(0f, 1.5f, 2f), "transit_intro");
            var transitDlgSo = new SerializedObject(transitIntroDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var morriganInterceptDialogue = BuildEp12DialoguePlayer("Dialogue_MorriganIntercept", new Vector3(0f, 1.5f, 8f), "morrigan_intercept");
            var recognitionDialogue = BuildEp12DialoguePlayer("Dialogue_RecognitionDuel", new Vector3(0f, 1.5f, 12f), "recognition_duel");

            // ---- Morrigan: duelist opponent (recognition duel), starts INACTIVE ----
            // Build as a proper damageable enemy so the player can spar; DuelYield resolves the duel.
            var morriganPos = new Vector3(0f, 0f, 10f);
            var morrigan = BuildDominionEnemy(morriganPos, playerHealth, enemyDef);
            morrigan.gameObject.transform.localScale *= 1.05f;
            var morriganRenderer = morrigan.GetComponent<Renderer>();
            if (morriganRenderer != null) TintShared(morriganRenderer, new Color(0.40f, 0.45f, 0.50f)); // cool teal-grey
            var morriganGo = morrigan.gameObject;
            var morriganHealth = morrigan.GetComponent<Health>();

            // Morrigan's strikes are non-lethal.
            var morriganMelee = morrigan.GetComponent<MeleeAttacker>();
            if (morriganMelee != null)
            {
                var maSo = new SerializedObject(morriganMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var morriganNpc = morriganGo.AddComponent<StoryNpc>();
            var mNpcSo = new SerializedObject(morriganNpc);
            mNpcSo.FindProperty("displayName").stringValue = "Morrigan";
            mNpcSo.FindProperty("remote").boolValue = false;
            mNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.4.
            var duelYield = morriganGo.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", morriganHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.4f;
            if (morriganMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { morriganMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onYielded → recognition_duel dialogue.
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(recognitionDialogue.Play));

            morriganGo.SetActive(false);

            // Transition box: "DESCEND — THE LOWER VAULTS".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 20.5f), "DESCEND — THE LOWER VAULTS",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep12VaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- 3 Hollow Kings Enforcers: 1 wave of 3 ----
            var enforcerColor = new Color(0.30f, 0.38f, 0.42f);
            var enforcerWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 6f),
                new Vector3(0f, 0f, 6.5f),
                new Vector3(2f, 0f, 6f)
            };

            var enforcerWaveHealths = new List<Health>();
            foreach (var pos in enforcerWavePositions)
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
                enforcerWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var enforcerSpawner = BuildWaveSpawner("EnforcerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { enforcerWaveHealths },
                new DialoguePlayer[0]); // No canonical enforcer barks.

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue transit_intro (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Transit Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = transitIntroDialogue;

            // Step 1: Dialogue morrigan_intercept.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Morrigan Intercept";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = morriganInterceptDialogue;

            // Step 2: Trigger — activate Morrigan duelist.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Activate Morrigan Duel";
            var activateProp2 = s2.FindPropertyRelative("triggerObjects");
            activateProp2.arraySize = 1;
            activateProp2.GetArrayElementAtIndex(0).objectReferenceValue = morriganGo;

            // Step 3: Prompt — the duel (DuelYield.onAccepted advances it).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Recognition Duel (yield + sheathe)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 4: DefeatWaves — 3 Hollow Kings enforcers.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s4.FindPropertyRelative("label").stringValue = "DefeatWaves: Hollow Kings Enforcers (3)";
            s4.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerSpawner;

            // Step 5: Prompt — transition to Vault.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Descend to Vault";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → missionDirector.AdvanceFromPrompt.
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12ShardMarketScenePath);
            EnsureScenesInBuild(Galaxy2Ep12ShardMarketScenePath, Galaxy2Ep12VaultScenePath);

            Debug.Log($"[Space Samurai] EP12 Shard Market scene built at {Galaxy2Ep12ShardMarketScenePath}. " +
                      "Layout: zero-gravity memory-bazaar commons (visual flavor only; normal locomotion). Cool blue-lit interior with floating-stall set dressing. " +
                      "Morrigan elite (DuelYield, yield at 40%, recognition_duel on yield, nonLethal). " +
                      "6 steps: transit_intro (auto) → morrigan_intercept dialogue → activate Morrigan duel (trigger) → " +
                      "duel Prompt (onAccepted → AdvanceFromPrompt) → defeat 3 Hollow Kings enforcers → transition to Vault.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 Vault", priority = 130)]
        public static void BuildEp12Vault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Vault: excavated mining-vault passage with memory-cell racks. Dim, claustrophobic.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.70f, 0.60f, 0.50f); // warm-dim key light
            light.intensity = 0.65f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.14f); // warm-dim ambient

            // Vault interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.40f, 0.35f, 0.28f);
            RenderSettings.fogDensity = 0.025f;

            // Two dim accent lights.
            BuildAccentPointLight("VaultLight1", new Vector3(-3f, 2f, 6f),
                new Color(0.8f, 0.65f, 0.4f), intensity: 0.8f, range: 9f);
            BuildAccentPointLight("VaultLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.75f, 0.6f, 0.35f), intensity: 0.75f, range: 8f);

            // ---- Vault Passage ----
            var vaultGo = new GameObject("VaultPassage");
            var vault = vaultGo.transform;
            var stoneGrey = new Color(0.42f, 0.40f, 0.36f);
            var darkStone = new Color(0.28f, 0.26f, 0.22f);

            // Main vault corridor floor.
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 8f), new Vector3(10f, 0f, 16f), stoneGrey, darkStone);

            // Corridor walls (corridor ~3.5 wide in the vault).
            BuildWall(vault, "VaultWall_W", new Vector3(-5f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(vault, "VaultWall_E", new Vector3(5f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));

            // Memory-cell boxes in double rows.
            var cellColor = new Color(0.30f, 0.32f, 0.35f);
            BuildProp(vault, "MemoryCell1", new Vector3(-2f, 1.2f, 4f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "MemoryCell2", new Vector3(2f, 1.2f, 4f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "MemoryCell3", new Vector3(-2f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "MemoryCell4", new Vector3(2f, 1.2f, 8f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "MemoryCell5", new Vector3(-2f, 1.2f, 12f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);
            BuildProp(vault, "MemoryCell6", new Vector3(2f, 1.2f, 12f), new Vector3(0.8f, 1.5f, 0.8f), cellColor);

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

            // ---- Morrigan as ALLY NPC ----
            var morriganAllyPos = new Vector3(-1f, 0f, 5f);
            var morriganGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, morriganAllyPos, "MorriganAlly");
            if (morriganGo != null)
            {
                var allyCombatant = morriganGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = morriganGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Morrigan";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var confessionDialogue = BuildEp12DialoguePlayer("Dialogue_TheConfession", new Vector3(0f, 1.5f, 4f), "the_confession");
            var confessionDlgSo = new SerializedObject(confessionDialogue);
            confessionDlgSo.FindProperty("playOnStart").boolValue = true;
            confessionDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 3 Dominion Purifiers: 1 wave of 3 ----
            var purifierColor = new Color(0.18f, 0.18f, 0.20f);
            var purifierWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 5f),
                new Vector3(0f, 0f, 5.5f),
                new Vector3(2f, 0f, 5f)
            };

            var purifierWaveHealths = new List<Health>();
            foreach (var pos in purifierWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, purifierColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                purifierWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var purifierSpawner = BuildWaveSpawner("PurifierSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { purifierWaveHealths },
                new[] { BuildEp12DialoguePlayer("Dialogue_PurifierBarks", new Vector3(0f, 1.5f, 8f), "purifier_barks") });

            // Emergency-lock reach point.
            var lockReachGo = new GameObject("EmergencyLockReachPoint");
            lockReachGo.transform.position = new Vector3(0f, 0.5f, 16f);

            // Transition box: "BREACH — THE EMERGENCY LOCK".
            var graveyardBoxGo = BuildTransitionBox("ToGraveyardBox", new Vector3(0f, 1.2f, 18.5f), "BREACH — THE EMERGENCY LOCK",
                out var graveyardBtn, out var graveyardTransition);
            var gbSo = new SerializedObject(graveyardTransition);
            gbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep12GraveyardSceneName;
            gbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(graveyardBtn.onClick,
                new UnityEngine.Events.UnityAction(graveyardTransition.LoadOnFootScene));
            graveyardBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue the_confession (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Confession";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = confessionDialogue;

            // Step 1: DefeatWaves — 3 Dominion Purifiers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Purifiers (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = purifierSpawner;

            // Step 2: ReachTrigger — emergency lock reach point.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Emergency Lock";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = lockReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: Prompt — transition to Graveyard.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Breach to Graveyard";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = graveyardBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12VaultScenePath);
            EnsureScenesInBuild(Galaxy2Ep12VaultScenePath, Galaxy2Ep12GraveyardScenePath);

            Debug.Log($"[Space Samurai] EP12 Vault scene built at {Galaxy2Ep12VaultScenePath}. " +
                      "Layout: excavated mining-vault passage with memory-cell racks (double rows). Dim, claustrophobic. " +
                      "Warm-dim + dim accent lights. Morrigan ally (AllyCombatant, NO Health). " +
                      "4 steps: the_confession (auto) → defeat 3 Dominion Purifiers (purifier_barks) → " +
                      "reach emergency lock → transition to Graveyard.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 Cutter", priority = 131)]
        public static void BuildEp12Cutter()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cutter: tight cutter interior corridor. Cold dim.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.60f, 0.70f); // cold dim key light
            light.intensity = 0.60f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -15f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.24f); // cold dim ambient

            // Cold interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.38f, 0.44f);
            RenderSettings.fogDensity = 0.030f;

            // Two cold accent lights.
            BuildAccentPointLight("CutterLight1", new Vector3(-2f, 2f, 4f),
                new Color(0.5f, 0.7f, 0.85f), intensity: 0.9f, range: 8f);
            BuildAccentPointLight("CutterLight2", new Vector3(2f, 2.5f, 10f),
                new Color(0.45f, 0.65f, 0.8f), intensity: 0.85f, range: 8f);

            // ---- Cutter Corridor ----
            var cutterGo = new GameObject("CutterCorridor");
            var cutter = cutterGo.transform;
            var metalGrey = new Color(0.38f, 0.40f, 0.42f);
            var darkMetal = new Color(0.24f, 0.26f, 0.28f);

            // Narrow corridor floor (~3 wide).
            BuildFloorCeiling(cutter, "CorridorFloor", new Vector3(0f, 0f, 8f), new Vector3(6f, 0f, 16f), metalGrey, darkMetal);

            // Two close walls.
            BuildWall(cutter, "CorridorWall_W", new Vector3(-3f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(cutter, "CorridorWall_E", new Vector3(3f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));

            // Conduit/box props.
            var conduitColor = new Color(0.28f, 0.30f, 0.32f);
            BuildProp(cutter, "Conduit1", new Vector3(-2.5f, 1f, 4f), new Vector3(0.5f, 1f, 0.5f), conduitColor);
            BuildProp(cutter, "Conduit2", new Vector3(2.5f, 1f, 6f), new Vector3(0.5f, 1f, 0.5f), conduitColor);
            BuildProp(cutter, "BoxProp1", new Vector3(-2f, 1.5f, 10f), new Vector3(0.8f, 1.2f, 0.8f), conduitColor);

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

            // ---- Morrigan present but NON-INTERVENING ----
            var morriganWatchPos = new Vector3(-1f, 0f, 3f);
            var morriganGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, morriganWatchPos, "MorriganWatching");
            if (morriganGo != null)
            {
                var storyNpc = morriganGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Morrigan";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var offerDialogue = BuildEp12DialoguePlayer("Dialogue_HollowKingsOffer", new Vector3(0f, 1.5f, 4f), "hollow_kings_offer");
            var offerDlgSo = new SerializedObject(offerDialogue);
            offerDlgSo.FindProperty("playOnStart").boolValue = true;
            offerDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var barkDialogue = BuildEp12DialoguePlayer("Dialogue_ShardbornAftermath", new Vector3(0f, 1.5f, 8f), "shardborn_barks");

            // ---- 4 Shardborn with HesitantAttacker mechanic ----
            var shardbornColor = new Color(0.42f, 0.38f, 0.50f); // pooled-augment purple-grey
            var shardbornWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 5f),
                new Vector3(1.5f, 0f, 5.5f),
                new Vector3(-1f, 0f, 7f),
                new Vector3(1f, 0f, 7.5f)
            };

            var shardbornWaveHealths = new List<Health>();
            foreach (var pos in shardbornWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, shardbornColor);

                // Set MeleeAttacker nonLethalDisable.
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }

                // Add HesitantAttacker component with flashRenderer wiring.
                var hesit = enemy.gameObject.AddComponent<HesitantAttacker>();
                var hesitSo = new SerializedObject(hesit);
                SetObjectRef(hesitSo, "flashRenderer", renderer);
                hesitSo.ApplyModifiedPropertiesWithoutUndo();

                enemy.gameObject.SetActive(false);
                shardbornWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var shardbornSpawner = BuildWaveSpawner("ShardbornSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { shardbornWaveHealths },
                new DialoguePlayer[0]);

            // Transition box: "SEAT THE SHARD".
            var theShardBoxGo = BuildTransitionBox("ToTheShardBox", new Vector3(0f, 1.2f, 16.5f), "SEAT THE SHARD",
                out var theShardBtn, out var theShardTransition);
            var tseSo = new SerializedObject(theShardTransition);
            tseSo.FindProperty("onFootScene").stringValue = Galaxy2Ep12TheShardSceneName;
            tseSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(theShardBtn.onClick,
                new UnityEngine.Events.UnityAction(theShardTransition.LoadOnFootScene));
            theShardBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue hollow_kings_offer (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Hollow Kings Offer";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = offerDialogue;

            // Step 1: DefeatWaves — 4 Shardborn.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Shardborn (4, HesitantAttacker)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = shardbornSpawner;

            // Step 2: Dialogue shardborn_barks.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Shardborn Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = barkDialogue;

            // Step 3: Prompt — transition to The Shard.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Seat The Shard";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = theShardBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12CutterScenePath);
            EnsureScenesInBuild(Galaxy2Ep12CutterScenePath, Galaxy2Ep12TheShardScenePath);

            Debug.Log($"[Space Samurai] EP12 Cutter scene built at {Galaxy2Ep12CutterScenePath}. " +
                      "Layout: tight cutter corridor with conduit/box props. Cold dim. " +
                      "Morrigan non-intervening (StoryNpc, NO AllyCombatant). " +
                      "4 steps: hollow_kings_offer (auto) → defeat 4 Shardborn (HesitantAttacker mechanic) → " +
                      "shardborn_barks dialogue → transition to The Shard.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP12 The Shard", priority = 132)]
        public static void BuildEp12TheShard()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Shard: memory-space Kethel-7 orphanage hallucination.
            // MemoryFlashbackController applies its own fog/ambient treatment.
            var memoryFlashbackGo = new GameObject("MemoryFlashback");
            var memoryFlashback = memoryFlashbackGo.AddComponent<MemoryFlashbackController>();
            var mfSo = new SerializedObject(memoryFlashback);
            mfSo.FindProperty("fogColor").colorValue = new Color(0.30f, 0.34f, 0.42f); // cold blue memory-space
            mfSo.ApplyModifiedPropertiesWithoutUndo();
            // Awake() will apply the treatment via RenderSettings.

            // Two dim accent lights.
            BuildAccentPointLight("ShardLight1", new Vector3(-3f, 2f, 5f),
                new Color(0.4f, 0.5f, 0.6f), intensity: 0.7f, range: 10f);
            BuildAccentPointLight("ShardLight2", new Vector3(3f, 2.5f, 11f),
                new Color(0.35f, 0.45f, 0.55f), intensity: 0.65f, range: 9f);

            // ---- Orphanage Echo: bunk dormitory ----
            var orphanageGo = new GameObject("OrphanageEcho");
            var orphanage = orphanageGo.transform;
            var dustyGrey = new Color(0.35f, 0.35f, 0.37f);
            var darkDusty = new Color(0.22f, 0.22f, 0.24f);

            // Dim floor.
            BuildFloorCeiling(orphanage, "OrphanageFloor", new Vector3(0f, 0f, 8f), new Vector3(12f, 0f, 16f), dustyGrey, darkDusty);

            // Bunk boxes in rows to suggest dormitory.
            var bunkColor = new Color(0.30f, 0.30f, 0.32f);
            BuildProp(orphanage, "Bunk1", new Vector3(-3f, 0.8f, 4f), new Vector3(1.5f, 0.6f, 1f), bunkColor);
            BuildProp(orphanage, "Bunk2", new Vector3(0f, 0.8f, 4f), new Vector3(1.5f, 0.6f, 1f), bunkColor);
            BuildProp(orphanage, "Bunk3", new Vector3(3f, 0.8f, 4f), new Vector3(1.5f, 0.6f, 1f), bunkColor);
            BuildProp(orphanage, "Bunk4", new Vector3(-3f, 0.8f, 8f), new Vector3(1.5f, 0.6f, 1f), bunkColor);
            BuildProp(orphanage, "Bunk5", new Vector3(0f, 0.8f, 8f), new Vector3(1.5f, 0.6f, 1f), bunkColor);
            BuildProp(orphanage, "Bunk6", new Vector3(3f, 0.8f, 8f), new Vector3(1.5f, 0.6f, 1f), bunkColor);

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

            // ---- Ghost echo vignette: echo NPCs + memory replay ----
            var echoRootGo = new GameObject("EchoRoot");

            // Ghost echo NPCs (Kessler models with ghost material).
            var ghostEcho1Go = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, new Vector3(-2f, 0f, 3f), "GhostEcho1");
            if (ghostEcho1Go != null)
            {
                ghostEcho1Go.transform.SetParent(echoRootGo.transform, false);
                var ghostRenderer1 = ghostEcho1Go.GetComponent<Renderer>();
                if (ghostRenderer1 != null)
                    ghostRenderer1.sharedMaterial = MemoryFlashbackController.MakeGhostMaterial();
            }

            var ghostEcho2Go = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, new Vector3(2f, 0f, 3f), "GhostEcho2");
            if (ghostEcho2Go != null)
            {
                ghostEcho2Go.transform.SetParent(echoRootGo.transform, false);
                var ghostRenderer2 = ghostEcho2Go.GetComponent<Renderer>();
                if (ghostRenderer2 != null)
                    ghostRenderer2.sharedMaterial = MemoryFlashbackController.MakeGhostMaterial();
            }

            // Echo move-from/to points.
            var echoMoveFromGo = new GameObject("EchoMoveFrom");
            echoMoveFromGo.transform.position = new Vector3(-2f, 0f, 2f);

            var echoMoveToGo = new GameObject("EchoMoveTo");
            echoMoveToGo.transform.position = new Vector3(-2f, 0f, 8f);

            // Memory echo vignette trigger.
            var memoryEchoGo = new GameObject("MemoryEcho");
            memoryEchoGo.transform.position = new Vector3(0f, 1f, 3f);
            var memoryEchoCollider = memoryEchoGo.AddComponent<BoxCollider>();
            memoryEchoCollider.isTrigger = true;
            memoryEchoCollider.size = new Vector3(8f, 2f, 4f);

            var memoryEchoVignette = memoryEchoGo.AddComponent<MemoryEchoVignette>();
            var mevSo = new SerializedObject(memoryEchoVignette);
            SetObjectRef(mevSo, "echoRoot", echoRootGo);
            SetObjectRef(mevSo, "moveFrom", echoMoveFromGo.transform);
            SetObjectRef(mevSo, "moveTo", echoMoveToGo.transform);
            mevSo.FindProperty("duration").floatValue = 6f;
            mevSo.FindProperty("oneShot").boolValue = true;
            mevSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Phantom-self echo enemies: 1 wave of 3 ----
            var echoEnemyColor = new Color(0.55f, 0.62f, 0.75f); // ghost tint
            var echoWavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 10f),
                new Vector3(0f, 0f, 10.5f),
                new Vector3(2f, 0f, 10f)
            };

            var echoWaveHealths = new List<Health>();
            foreach (var pos in echoWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, echoEnemyColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                echoWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var echoSpawner = BuildWaveSpawner("EchoSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { echoWaveHealths },
                new DialoguePlayer[0]);

            // ---- Dialogue Players ----
            var shardDialogue = BuildEp12DialoguePlayer("Dialogue_TheShard", new Vector3(0f, 1.5f, 12f), "the_shard");

            // Transition box: "TO THE BRIDGE".
            var pursuitBoxGo = BuildTransitionBox("ToPursuitBox", new Vector3(0f, 1.2f, 16.5f), "TO THE BRIDGE",
                out var pursuitBtn, out var pursuitTransition);
            var pbSo = new SerializedObject(pursuitTransition);
            pbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep12PursuitSceneName;
            pbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pursuitBtn.onClick,
                new UnityEngine.Events.UnityAction(pursuitTransition.LoadOnFootScene));
            pursuitBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Trigger — activate memory echo vignette (the MemoryEchoVignette plays on its own OnTriggerEnter).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s0.FindPropertyRelative("label").stringValue = "Trigger: Activate Memory Echo";
            var activateProp0 = s0.FindPropertyRelative("triggerObjects");
            activateProp0.arraySize = 1;
            activateProp0.GetArrayElementAtIndex(0).objectReferenceValue = memoryEchoGo;

            // Step 1: DefeatWaves — 3 phantom-self echoes.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Phantom-Self Echoes (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = echoSpawner;

            // Step 2: Dialogue the_shard.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Shard";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = shardDialogue;

            // Step 3: Prompt — transition to Pursuit.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To The Bridge (Pursuit)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = pursuitBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep12TheShardScenePath);
            EnsureScenesInBuild(Galaxy2Ep12TheShardScenePath);

            Debug.Log($"[Space Samurai] EP12 The Shard scene built at {Galaxy2Ep12TheShardScenePath}. " +
                      "Layout: memory-space Kethel-7 orphanage hallucination with bunk dormitory set dressing. " +
                      "MemoryFlashbackController applies cold-blue memory-space fog/ambient. " +
                      "2 ghost echo NPCs (ghost-material) with MemoryEchoVignette (moveFrom/moveTo, 6s duration, oneShot). " +
                      "4 steps: activate memory echo (trigger) → defeat 3 phantom-self echoes → " +
                      "the_shard dialogue → transition to Pursuit (Bridge).");
        }
    }
}

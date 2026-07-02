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
    /// EP32 "The Throne of Ashes" (Galaxy 4 finale) scene builders for the four on-foot combat scenes
    /// and the cockpit denouement finale. Soren storms the Dominion fortress, confronts Khall, learns
    /// the Kethel-7 kill-order was forged by the Hollow Kings—and that the true enemy is First Overseer
    /// Maelgorn and the Obsidian Synod. Khall repents and stays behind to dismantle the fortress. Soren
    /// calls in the ten allies and assembles a war council to face the Synod.
    /// - Gate-Keepers: Commander Vale retrieves Soren at the hangar.
    /// - Nurture Vault: Vale reveals Khall's confession; drones defend the archives.
    /// - Pale Choir: Revenant Threne threatens the sublevel creches; Choir operatives attack.
    /// - Descending King: Soren confronts Khall in the throne chamber; Maelgorn and the Synod revealed.
    /// - Threshold: SPACE cockpit denouement with war council call-in (in Ep32BuilderFinale.cs).
    ///
    /// The two SPACE scenes (The Breach, The Exodus) are built in Ep32BuilderFinale.cs but share
    /// the scene path constants and helper methods declared here.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 7 scenes: 4 on-foot here + 1 cockpit here + 2 space in Ep32BuilderFinale.cs.
        private const string Galaxy4Ep32TheBreachScenePath      = SceneFolder + "/Galaxy4_EP32_TheBreach.unity";
        private const string Galaxy4Ep32GateKeepersScenePath    = SceneFolder + "/Galaxy4_EP32_GateKeepers.unity";
        private const string Galaxy4Ep32NurtureVaultScenePath   = SceneFolder + "/Galaxy4_EP32_NurtureVault.unity";
        private const string Galaxy4Ep32PaleChoirScenePath      = SceneFolder + "/Galaxy4_EP32_PaleChoir.unity";
        private const string Galaxy4Ep32DescendingKingScenePath = SceneFolder + "/Galaxy4_EP32_DescendingKing.unity";
        private const string Galaxy4Ep32ExodusScenePath         = SceneFolder + "/Galaxy4_EP32_Exodus.unity";
        private const string Galaxy4Ep32ThresholdScenePath      = SceneFolder + "/Galaxy4_EP32_Threshold.unity";

        private static readonly string Galaxy4Ep32TheBreachSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32TheBreachScenePath);
        private static readonly string Galaxy4Ep32GateKeepersSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32GateKeepersScenePath);
        private static readonly string Galaxy4Ep32NurtureVaultSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32NurtureVaultScenePath);
        private static readonly string Galaxy4Ep32PaleChoirSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32PaleChoirScenePath);
        private static readonly string Galaxy4Ep32DescendingKingSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32DescendingKingScenePath);
        private static readonly string Galaxy4Ep32ExodusSceneName         = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32ExodusScenePath);
        private static readonly string Galaxy4Ep32ThresholdSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep32ThresholdScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP32 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep32" and loads lines from Ep32Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp32DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep32Lines.Get(setId), advanceRef, setId, clipPrefix: "ep32");
        }

        /// <summary>Standard EP32 on-foot scene scaffold: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp32OnFootShell(Object[] refs, WeaponDefinition weapon,
            Color keyLight, Color ambient, Color fogColor, float fogDensity,
            string structureName, Color accent1, Color accent2,
            Color floorLight, Color floorDark, Color propTint, out GameObject structureGo)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = keyLight;
            light.intensity = 0.46f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            BuildAccentPointLight(structureName + "Light1", new Vector3(-3f, 2f, 8f), accent1, intensity: 0.70f, range: 10f);
            BuildAccentPointLight(structureName + "Light2", new Vector3(3f, 2.5f, 12f), accent2, intensity: 0.65f, range: 9f);

            structureGo = new GameObject(structureName);
            var s = structureGo.transform;
            BuildFloorCeiling(s, structureName + "Floor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 21f), floorLight, floorDark);
            BuildWall(s, structureName + "Wall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));
            BuildWall(s, structureName + "Wall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 21f));

            // Standardized tower/panel props.
            BuildProp(s, "Tower1", new Vector3(-3f, 1.5f, 8f), new Vector3(0.9f, 2.4f, 0.9f), propTint);
            BuildProp(s, "Tower2", new Vector3(3f, 1.5f, 12f), new Vector3(0.9f, 2.4f, 0.9f), propTint);
            BuildProp(s, "Tower3", new Vector3(-2f, 1.5f, 16f), new Vector3(0.9f, 2.4f, 0.9f), propTint);

            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            return playerHealth;
        }

        /// <summary>Adds a non-remote StoryNpc capsule. Returns the GameObject so callers can optionally
        /// add an AllyCombatant or other components.</summary>
        private static GameObject BuildEp32Npc(string displayName, Vector3 position, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = displayName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            TintShared(go.GetComponent<Renderer>(), tint);
            var npc = go.AddComponent<StoryNpc>();
            var so = new SerializedObject(npc);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("remote").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void FinishEp32Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
        {
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, scenePath);
            if (string.IsNullOrEmpty(nextScenePath)) EnsureScenesInBuild(scenePath);
            else EnsureScenesInBuild(scenePath, nextScenePath);
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Gate-Keepers", priority = 324)]
        public static void BuildEp32GateKeepers()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Gate-Keepers: dark Dominion fortress hangar, black-iron palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.40f, 0.42f, 0.45f),       // cold dark steel
                ambient: new Color(0.05f, 0.05f, 0.06f),        // near-black
                fogColor: new Color(0.08f, 0.08f, 0.09f), fogDensity: 0.011f,
                structureName: "Hangar",
                accent1: new Color(0.25f, 0.28f, 0.32f),        // dark slate
                accent2: new Color(0.30f, 0.32f, 0.36f),        // dark charcoal
                floorLight: new Color(0.35f, 0.37f, 0.41f), floorDark: new Color(0.10f, 0.10f, 0.12f),
                propTint: new Color(0.22f, 0.24f, 0.28f), out _);

            // ---- Commander Vale presence ----
            BuildEp32Npc("Commander Vale", new Vector3(-2f, 0f, 3f), new Color(0.50f, 0.50f, 0.52f));

            // ---- Vale PA dialogue (playOnStart) ----
            var valePaDialogue = BuildEp32DialoguePlayer("Dialogue_ValePa", new Vector3(0f, 1.5f, 2f), "vale_pa");
            var vpSo = new SerializedObject(valePaDialogue);
            vpSo.FindProperty("playOnStart").boolValue = true;
            vpSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Hangar engage dialogue ----
            var hangarEngageDialogue = BuildEp32DialoguePlayer("Dialogue_HangarEngage", new Vector3(0f, 1.5f, 12f), "hangar_engage");

            // ---- Dominion soldier enemies (dark Dominion tint, 2 waves of 4 = 8 total) ----
            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 8.5f),
                new Vector3(-1f, 0f, 10f),
                new Vector3(1f, 0f, 9.5f),
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 11.5f),
                new Vector3(0f, 0f, 12.5f),
                new Vector3(-0.5f, 0f, 13f),
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var soldierSpawner = BuildWaveSpawner("SoldierSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new DialoguePlayer[0]);

            // ---- Vale aftermath dialogue ----
            var valeAftermathDialogue = BuildEp32DialoguePlayer("Dialogue_ValeAftermath", new Vector3(0f, 1.5f, 15f), "vale_aftermath");

            // ---- Transition box: "ENTER THE VAULT" ----
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 21.5f), "ENTER THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32NurtureVaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vale PA (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = valePaDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Hangar Engage";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = hangarEngageDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Soldiers (Wave 1: 4 + Wave 2: 4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = soldierSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Vale Aftermath";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = valeAftermathDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Enter The Vault";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep32GateKeepersScenePath, Galaxy4Ep32NurtureVaultScenePath);

            Debug.Log($"[Space Samurai] EP32 Gate-Keepers scene built at {Galaxy4Ep32GateKeepersScenePath}. " +
                      "Dark Dominion fortress hangar (cold dark steel, dark slate + dark charcoal accents, near-black fog). " +
                      "Commander Vale NPC (grey tint). 8 Dominion Soldier enemies (dark Dominion tint, 2 waves of 4). " +
                      "5 steps: vale_pa (auto, Vale's retrieval) → hangar_engage → defeat 2 soldier waves → vale_aftermath (vault reveal) → ENTER THE VAULT.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Nurture Vault", priority = 325)]
        public static void BuildEp32NurtureVault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Nurture Vault: sterile black-iron vault, cold obsidian palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.38f, 0.40f, 0.43f),       // cold dark steel
                ambient: new Color(0.04f, 0.04f, 0.05f),        // very dark
                fogColor: new Color(0.07f, 0.07f, 0.08f), fogDensity: 0.011f,
                structureName: "NurtureVault",
                accent1: new Color(0.22f, 0.25f, 0.30f),        // obsidian dark
                accent2: new Color(0.28f, 0.31f, 0.36f),        // iron slate
                floorLight: new Color(0.32f, 0.35f, 0.39f), floorDark: new Color(0.08f, 0.08f, 0.10f),
                propTint: new Color(0.20f, 0.22f, 0.26f), out _);

            // ---- Commander Vale presence ----
            BuildEp32Npc("Commander Vale", new Vector3(-2f, 0f, 3f), new Color(0.50f, 0.50f, 0.52f));

            // ---- Vault history dialogue (playOnStart) ----
            var vaultHistoryDialogue = BuildEp32DialoguePlayer("Dialogue_VaultHistory", new Vector3(0f, 1.5f, 2f), "vault_history");
            var vhSo = new SerializedObject(vaultHistoryDialogue);
            vhSo.FindProperty("playOnStart").boolValue = true;
            vhSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Vault combat dialogue ----
            var vaultCombatDialogue = BuildEp32DialoguePlayer("Dialogue_VaultCombat", new Vector3(0f, 1.5f, 12f), "vault_combat");

            // ---- Ceiling pulse-drone enemies (dark Dominion tint, 3 in 1 wave) ----
            var dronePositions = new Vector3[]
            {
                new Vector3(-1.5f, 1.8f, 9f),
                new Vector3(1.5f, 1.8f, 10f),
                new Vector3(0f, 1.8f, 11.5f),
            };
            var droneHealths = new List<Health>();
            foreach (var pos in dronePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                droneHealths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildWaveSpawner("DroneSpawner", new Vector3(0f, 1.5f, 10f), 2f,
                new List<List<Health>> { droneHealths },
                new DialoguePlayer[0]);

            // ---- Khall recording dialogue ----
            var khallRecordingDialogue = BuildEp32DialoguePlayer("Dialogue_KhallRecording", new Vector3(0f, 1.5f, 15f), "khall_recording");

            // ---- Transition box: "TO THE MANUFACTURING DECK" ----
            var mfgBoxGo = BuildTransitionBox("ToMfgBox", new Vector3(0f, 1.2f, 21.5f), "TO THE MANUFACTURING DECK",
                out var mfgBtn, out var mfgTransition);
            var mbSo = new SerializedObject(mfgTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32PaleChoirSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(mfgBtn.onClick,
                new UnityEngine.Events.UnityAction(mfgTransition.LoadOnFootScene));
            mfgBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault History (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultHistoryDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Vault Combat";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = vaultCombatDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Ceiling Pulse-Drones (3)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Khall Recording";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = khallRecordingDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: To The Manufacturing Deck";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = mfgBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep32NurtureVaultScenePath, Galaxy4Ep32PaleChoirScenePath);

            Debug.Log($"[Space Samurai] EP32 Nurture Vault scene built at {Galaxy4Ep32NurtureVaultScenePath}. " +
                      "Sterile black-iron vault (cold dark steel, obsidian dark + iron slate accents, very dark fog). " +
                      "Commander Vale NPC (grey tint). 3 Ceiling Pulse-Drone enemies (dark Dominion tint). " +
                      "5 steps: vault_history (auto, operative children reveal) → vault_combat → defeat 3 drones → khall_recording (Khall's confession) → TO THE MANUFACTURING DECK.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Pale Choir", priority = 326)]
        public static void BuildEp32PaleChoir()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Pale Choir: volcanic dark manufacturing deck, warmer charcoal tones.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.42f, 0.38f, 0.34f),       // warm dark charcoal
                ambient: new Color(0.06f, 0.05f, 0.04f),        // dark warm
                fogColor: new Color(0.10f, 0.08f, 0.06f), fogDensity: 0.011f,
                structureName: "ManufacturingDeck",
                accent1: new Color(0.35f, 0.28f, 0.22f),        // volcanic brown
                accent2: new Color(0.32f, 0.26f, 0.20f),        // burnt charcoal
                floorLight: new Color(0.38f, 0.33f, 0.28f), floorDark: new Color(0.12f, 0.10f, 0.08f),
                propTint: new Color(0.28f, 0.24f, 0.20f), out _);

            // ---- Threne offer dialogue (playOnStart) ----
            var threneOfferDialogue = BuildEp32DialoguePlayer("Dialogue_ThreneOffer", new Vector3(0f, 1.5f, 2f), "threne_offer");
            var toSo = new SerializedObject(threneOfferDialogue);
            toSo.FindProperty("playOnStart").boolValue = true;
            toSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Choir engage dialogue ----
            var choirEngageDialogue = BuildEp32DialoguePlayer("Dialogue_ChoirEngage", new Vector3(0f, 1.5f, 12f), "choir_engage");

            // ---- Choir operative enemies (pale bone tint, 2 waves of 3 = 6 total) ----
            var operativeColor = new Color(0.80f, 0.80f, 0.75f); // pale bone

            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(2f, 0f, 8.5f),
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, operativeColor);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 11.5f),
                new Vector3(0f, 0f, 12.5f),
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, operativeColor);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var choirSpawner = BuildWaveSpawner("ChoirSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new DialoguePlayer[0]);

            // ---- Choir aftermath dialogue ----
            var choirAftermathDialogue = BuildEp32DialoguePlayer("Dialogue_ChoirAftermath", new Vector3(0f, 1.5f, 15f), "choir_aftermath");

            // ---- Transition box: "TO THE THRONE" ----
            var throneBoxGo = BuildTransitionBox("ToThroneBox", new Vector3(0f, 1.2f, 21.5f), "TO THE THRONE",
                out var throneBtn, out var throneTransition);
            var tbSo = new SerializedObject(throneTransition);
            tbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32DescendingKingSceneName;
            tbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(throneBtn.onClick,
                new UnityEngine.Events.UnityAction(throneTransition.LoadOnFootScene));
            throneBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Threne Offer (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = threneOfferDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Choir Operatives (Wave 1: 3 + Wave 2: 3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = choirSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Choir Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = choirAftermathDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To The Throne";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = throneBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep32PaleChoirScenePath, Galaxy4Ep32DescendingKingScenePath);

            Debug.Log($"[Space Samurai] EP32 Pale Choir scene built at {Galaxy4Ep32PaleChoirScenePath}. " +
                      "Volcanic manufacturing deck (warm dark charcoal, volcanic brown + burnt charcoal accents, warm dark fog). " +
                      "6 Choir Operative enemies (pale bone tint, 2 waves of 3). " +
                      "4 steps: threne_offer (auto, creche hostage threat) → defeat 2 operative waves → choir_aftermath (bluff resolution) → TO THE THRONE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Descending King", priority = 327)]
        public static void BuildEp32DescendingKing()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Descending King: cathedral black iron throne chamber, deep obsidian palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.35f, 0.37f, 0.40f),       // cold dark steel
                ambient: new Color(0.03f, 0.03f, 0.04f),        // very deep dark
                fogColor: new Color(0.06f, 0.06f, 0.07f), fogDensity: 0.012f,
                structureName: "ThroneChamber",
                accent1: new Color(0.20f, 0.22f, 0.26f),        // deep obsidian
                accent2: new Color(0.25f, 0.27f, 0.32f),        // dark iron
                floorLight: new Color(0.30f, 0.32f, 0.37f), floorDark: new Color(0.07f, 0.07f, 0.09f),
                propTint: new Color(0.18f, 0.20f, 0.24f), out _);

            // ---- Throne confront dialogue (playOnStart) ----
            var throneConfrontDialogue = BuildEp32DialoguePlayer("Dialogue_ThroneConfront", new Vector3(0f, 1.5f, 2f), "throne_confront");
            var tcSo = new SerializedObject(throneConfrontDialogue);
            tcSo.FindProperty("playOnStart").boolValue = true;
            tcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Throne engage dialogue ----
            var throneEngageDialogue = BuildEp32DialoguePlayer("Dialogue_ThroneEngage", new Vector3(0f, 1.5f, 12f), "throne_engage");

            // ---- Rogue officer enemies (dark Dominion tint, 3 in 1 wave) ----
            var officerPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 9f),
                new Vector3(0f, 0f, 10.5f),
                new Vector3(2f, 0f, 9.5f),
            };
            var officerHealths = new List<Health>();
            foreach (var pos in officerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                officerHealths.Add(enemy.GetComponent<Health>());
            }

            var officerSpawner = BuildWaveSpawner("OfficerSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { officerHealths },
                new DialoguePlayer[0]);

            // ---- Maelgorn reveal dialogue ----
            var maelgornRevealDialogue = BuildEp32DialoguePlayer("Dialogue_MaelgornReveal", new Vector3(0f, 1.5f, 15f), "maelgorn_reveal");

            // ---- Transition box: "TO THE DOCKS" ----
            var docksBoxGo = BuildTransitionBox("ToDocksBox", new Vector3(0f, 1.2f, 21.5f), "TO THE DOCKS",
                out var docksBtn, out var docksTransition);
            var dbSo = new SerializedObject(docksTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep32ExodusSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(docksBtn.onClick,
                new UnityEngine.Events.UnityAction(docksTransition.LoadOnFootScene));
            docksBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Throne Confront (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = throneConfrontDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Rogue Officers (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = officerSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Maelgorn Reveal";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = maelgornRevealDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To The Docks";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = docksBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep32DescendingKingScenePath, Galaxy4Ep32ExodusScenePath);

            Debug.Log($"[Space Samurai] EP32 Descending King scene built at {Galaxy4Ep32DescendingKingScenePath}. " +
                      "Cathedral throne chamber (deep obsidian palette, deep obsidian + dark iron accents, very deep dark fog). " +
                      "3 Rogue Officer enemies (dark Dominion tint). " +
                      "4 steps: throne_confront (auto, Khall's confession, Kethel-7 forgery) → defeat 3 officers → maelgorn_reveal (Obsidian Synod, failsafe network) → TO THE DOCKS.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP32 Threshold", priority = 330)]
        public static void BuildEp32Threshold()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // SPACE styling: dying red giant below.
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

            // Dying Red Giant below: deep red dim aesthetic.
            var dyingStarGo = AddUnlitVisual(universe, "Dying Red Giant", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.30f, 0.10f, 0.08f));
            var starCollider = dyingStarGo.GetComponent<Collider>();
            if (starCollider != null) starCollider.isTrigger = true;

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
            // threshold_call: Soren's war council assembly (plays on start, cockpit-parented).
            var thresholdCallDialogue = BuildEp32DialoguePlayer("Dialogue_ThresholdCall", new Vector3(0f, 1.62f, 0.8f), "threshold_call");
            var tcGo = thresholdCallDialogue.gameObject;
            tcGo.transform.SetParent(cockpit, false);
            tcGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            tcGo.transform.localRotation = Quaternion.identity;
            var tcSo = new SerializedObject(thresholdCallDialogue);
            tcSo.FindProperty("playOnStart").boolValue = true;
            tcSo.ApplyModifiedPropertiesWithoutUndo();

            // NO GuardEncounter, NO combat.

            // Finale return box: "RETURN — TO THE STARS" (ACTIVE from start).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(true); // Active from start, no combat gate.

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep32_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of EP32: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep32ThresholdScenePath);
            EnsureScenesInBuild(Galaxy4Ep32ThresholdScenePath);

            Debug.Log($"[Space Samurai] EP32 Threshold scene built at {Galaxy4Ep32ThresholdScenePath}. " +
                      "SPACE denouement (no combat). Dying Red Giant (deep red dim aesthetic) below. Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: threshold_call (auto, Soren's war council assembly, Obsidian Synod facing). " +
                      "No encounter. RETURN — TO THE STARS button (ACTIVE from start) wired to CampaignFlagSetter (ep32_complete) + ReturnToSpace. " +
                      "GALAXY 4 EP32 FINALE (last scene, space denouement).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP32 Scenes", priority = 331)]
        public static void BuildAllEp32Scenes()
        {
            BuildEp32TheBreach();      // defined in Ep32BuilderFinale.cs
            BuildEp32GateKeepers();
            BuildEp32NurtureVault();
            BuildEp32PaleChoir();
            BuildEp32DescendingKing();
            BuildEp32Exodus();         // defined in Ep32BuilderFinale.cs
            BuildEp32Threshold();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP32 scenes built + inputs rewired. GALAXY 4 EP32 COMPLETE.");
        }
    }
}

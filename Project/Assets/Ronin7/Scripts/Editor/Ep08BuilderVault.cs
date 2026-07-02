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
    /// EP08 "The Architect of Mercy" vault & collapse scene builders. Builds the Apex Station vault
    /// where Cipher encounters the REAPER-9 elite unit and faces the truth about pre-wipe consciousness,
    /// then escapes through a collapsing chamber. Wires all MissionDirector steps, elite enemy encounter,
    /// and the collapse sequence controller.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as Ep08Builder, so it calls
    /// the private static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep08ApexVaultScenePath = SceneFolder + "/Galaxy1_EP08_ApexVault.unity";

        private static readonly string Galaxy1Ep08ApexVaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08ApexVaultScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Apex Vault", priority = 101)]
        public static void BuildEp08ApexVault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Apex Vault: cold wide chamber with tall server-column props, vault console, emissive accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.8f, 0.9f); // cool white-blue key
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.25f); // cool dim tones

            // Cold vault atmosphere.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.40f, 0.48f);
            RenderSettings.fogDensity = 0.023f;

            // Cool accent lights on server columns.
            BuildAccentPointLight("VaultLight1", new Vector3(-5f, 3f, 8f),
                new Color(0.4f, 0.65f, 0.9f), intensity: 1.2f, range: 12f);
            BuildAccentPointLight("VaultLight2", new Vector3(5f, 3f, 12f),
                new Color(0.45f, 0.70f, 0.95f), intensity: 1.1f, range: 11f);

            // ---- Apex Vault: wide chamber (x[-8,8], z[0,16]) with tall server columns ----
            var vaultGo = new GameObject("ApexVaultInterior");
            var vault = vaultGo.transform;
            var vaultFloor = new Color(0.25f, 0.27f, 0.30f); // dark metal
            var vaultCeiling = new Color(0.12f, 0.14f, 0.16f);

            // Wide chamber floor and ceiling.
            BuildFloorCeiling(vault, "VaultFloor", new Vector3(0f, 0f, 8f), new Vector3(16f, 0f, 16f), vaultFloor, vaultCeiling);
            BuildWall(vault, "VaultWall_W", new Vector3(-8f, 2f, 8f), new Vector3(0.3f, 4f, 16f));
            BuildWall(vault, "VaultWall_E", new Vector3(8f, 2f, 8f), new Vector3(0.3f, 4f, 16f));
            BuildWall(vault, "VaultWall_Back", new Vector3(0f, 2f, 16f), new Vector3(16f, 4f, 0.3f));

            // Tall server columns (emissive accents).
            var columnColor = new Color(0.30f, 0.32f, 0.35f);
            var columnPositions = new Vector3[]
            {
                new Vector3(-6f, 0.8f, 4f), new Vector3(-2f, 0.8f, 6f),
                new Vector3(2f, 0.8f, 5f), new Vector3(6f, 0.8f, 7f),
                new Vector3(-4f, 0.8f, 12f), new Vector3(4f, 0.8f, 11f)
            };
            for (int i = 0; i < columnPositions.Length; i++)
            {
                BuildProp(vault, $"ServerColumn{i}", columnPositions[i], new Vector3(0.6f, 2.2f, 0.6f), columnColor);
            }

            // Emissive accent lights on columns (subtle glow).
            for (int i = 0; i < 3; i++)
            {
                var accentPos = columnPositions[i] + new Vector3(0f, 1.8f, 0f);
                var accentGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                accentGo.name = $"ColumnAccent{i}";
                accentGo.transform.SetParent(vault, false);
                accentGo.transform.localPosition = accentPos;
                accentGo.transform.localScale = Vector3.one * 0.3f;
                TintShared(accentGo.GetComponent<Renderer>(), new Color(0.3f, 0.7f, 0.95f, 0.6f));
                var accentCollider = accentGo.GetComponent<Collider>();
                if (accentCollider != null) Object.DestroyImmediate(accentCollider);
            }

            // Vault console prop at center back.
            var consolePos = new Vector3(0f, 0.8f, 14f);
            BuildProp(vault, "VaultConsole", consolePos, new Vector3(1.5f, 1.2f, 0.8f), vaultFloor);

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

            // ---- NPCs ----
            // Mera Voss at the vault console.
            var meraConsolePos = new Vector3(0f, 1f, 14f);
            var meraGo = InstantiateNpc(MeraVossPrefabPath, meraConsolePos, "Mera");
            if (meraGo != null)
            {
                var meraNpc = meraGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(meraNpc);
                mSo.FindProperty("displayName").stringValue = "Mera";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var vaultCoreDialogue = BuildEp08DialoguePlayer("Dialogue_VaultCore", meraConsolePos, "vault_core", talkRef);
            var reaperDuelDialogue = BuildEp08DialoguePlayer("Dialogue_ReaperDuel", new Vector3(0f, 1.5f, 8f), "reaper_duel");

            // ---- Enemy: REAPER-9 Elite (single high-HP unit, dark tint) ----
            // Create an elite enemy definition with higher HP than standard.
            // Enemy.Awake re-configures Health from its definition, so the elite HP must
            // live on a dedicated EnemyDefinition asset rather than an editor-time Health edit.
            var reaperEnemyDef = EnsureEp08ReaperDefinition();

            var reaperPos = new Vector3(0f, 0f, 6f);
            var reaper = BuildDominionEnemy(reaperPos, playerHealth, reaperEnemyDef);
            var reaperRenderer = reaper.GetComponent<Renderer>();
            if (reaperRenderer != null)
            {
                TintShared(reaperRenderer, new Color(0.25f, 0.25f, 0.28f)); // dark elite tint
            }
            var reaperMeleeAttacker = reaper.GetComponent<MeleeAttacker>();
            if (reaperMeleeAttacker != null)
            {
                var rmaSo = new SerializedObject(reaperMeleeAttacker);
                rmaSo.FindProperty("nonLethalDisable").boolValue = true;
                rmaSo.ApplyModifiedPropertiesWithoutUndo();
            }
            reaper.gameObject.SetActive(false);

            // Build wave spawner for the single REAPER-9.
            var reaperSpawner = BuildWaveSpawner("ReaperSpawner", new Vector3(0f, 1f, 6f), 1f,
                new List<List<Health>> { new List<Health> { reaper.GetComponent<Health>() } }, new[] { reaperDuelDialogue });

            // Reach trigger at vault back.
            var escapeReachGo = new GameObject("VaultEscapeReachPoint");
            escapeReachGo.transform.position = new Vector3(0f, 1f, 15f);

            // Transition box: "BOARD THE CORSAIR" (Collapse cut — ApexVault now chains straight to Orbit Break).
            var collapseBoxGo = BuildTransitionBox("ToOrbitBox", new Vector3(0f, 1.2f, 15.5f), "BOARD THE CORSAIR",
                out var collapseBtn, out var collapseTransition);
            var ctSo = new SerializedObject(collapseTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy1Ep08OrbitBreakSceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(collapseBtn.onClick,
                new UnityEngine.Events.UnityAction(collapseTransition.LoadOnFootScene));
            collapseBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            // Step 0: Dialogue vault_core (talk-gated at Mera).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Vault Core";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = vaultCoreDialogue;

            // Step 1: DefeatWaves — REAPER-9 Elite.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: REAPER-9 Elite";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = reaperSpawner;

            // Step 2: Prompt — board the corsair.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Board the Corsair";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = collapseBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08ApexVaultScenePath);
            EnsureScenesInBuild(Galaxy1Ep08ApexVaultScenePath);

            Debug.Log($"[Space Samurai] EP08 Apex Vault scene built at {Galaxy1Ep08ApexVaultScenePath}. " +
                      "Layout: wide cold vault chamber with tall server columns (emissive accents), vault console prop. " +
                      "Mera Voss NPC at console. Dark elite tint. " +
                      "3 steps: vault_core (talk) at Mera → defeat REAPER-9 elite + barks → " +
                      "transition to Orbit Break.");
        }

        /// <summary>Elite EnemyDefinition for the EP08 REAPER-9 duel (~1.8x standard HP).</summary>
        private static EnemyDefinition EnsureEp08ReaperDefinition()
        {
            const string path = DataFolder + "/Ep08Reaper.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 110f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }
    }
}

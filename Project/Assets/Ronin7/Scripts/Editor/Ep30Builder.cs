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
    /// EP30 "The Vault Within" (Galaxy 4) scene builders for the first four on-foot scenes
    /// at the Iron Sepulcher, a Dominion black-archive station. Cipher and Mera Voss breach
    /// the vault to retrieve Cipher's sealed file (SUBJECT 7). The player descends through
    /// an infiltration approach, encounters the Warden guardian, reaches the vault chamber,
    /// reads the file that reveals his true name (Soren), and escapes with Mera. The episode
    /// closes on the discovery that a deeper time-lock remains armed inside his chip.
    /// - Price of Memory: Mera Voss intro, docking corridor (in Ep30BuilderFinale.cs).
    /// - Sepulcher Approach: briefing + space dogfight (in Ep30BuilderFinale.cs).
    /// - Into The Iron: infiltration of the frozen administrative spine with the Warden.
    /// - File Speaks: the vault chamber with docking-ring soldiers and the name reclaim.
    /// - Hollow Kings: post-heist dialogue offering a new contract (in Ep30BuilderFinale.cs).
    /// - Name Remains: SPACE finale with debris evasion and the time-lock discovery (in Ep30BuilderFinale.cs).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 6 scenes declared here; scenes 1, 2, 5, 6 built in Ep30BuilderFinale.cs.
        private const string Galaxy4Ep30PriceOfMemoryScenePath     = SceneFolder + "/Galaxy4_EP30_PriceOfMemory.unity";
        private const string Galaxy4Ep30SepulcherApproachScenePath = SceneFolder + "/Galaxy4_EP30_SepulcherApproach.unity";
        private const string Galaxy4Ep30IntoTheIronScenePath       = SceneFolder + "/Galaxy4_EP30_IntoTheIron.unity";
        private const string Galaxy4Ep30FileSpeaksScenePath        = SceneFolder + "/Galaxy4_EP30_FileSpeaks.unity";
        private const string Galaxy4Ep30HollowKingsScenePath       = SceneFolder + "/Galaxy4_EP30_HollowKings.unity";
        private const string Galaxy4Ep30NameRemainsScenePath       = SceneFolder + "/Galaxy4_EP30_NameRemains.unity";

        private static readonly string Galaxy4Ep30PriceOfMemorySceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30PriceOfMemoryScenePath);
        private static readonly string Galaxy4Ep30SepulcherApproachSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30SepulcherApproachScenePath);
        private static readonly string Galaxy4Ep30IntoTheIronSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30IntoTheIronScenePath);
        private static readonly string Galaxy4Ep30FileSpeaksSceneName        = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30FileSpeaksScenePath);
        private static readonly string Galaxy4Ep30HollowKingsSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30HollowKingsScenePath);
        private static readonly string Galaxy4Ep30NameRemainsSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep30NameRemainsScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP30 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep30" and loads lines from Ep30Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp30DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep30Lines.Get(setId), advanceRef, setId, clipPrefix: "ep30");
        }

        /// <summary>Standard EP30 on-foot scene scaffold shared by scenes 3-4: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp30OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        private static GameObject BuildEp30Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp30Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Iron Sepulcher tint: cold steel grey (frozen administrative archive, like EP29's Neurovault)
        private static readonly Color Ep30ColdSteel = new Color(0.45f, 0.46f, 0.50f);
        // Iron Sepulcher accent: deep frost blue
        private static readonly Color Ep30FrostBlue = new Color(0.35f, 0.42f, 0.55f);
        // Iron Sepulcher accent: pale cold grey
        private static readonly Color Ep30PaleCold = new Color(0.58f, 0.60f, 0.65f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 Into The Iron", priority = 311)]
        public static void BuildEp30IntoTheIron()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Into The Iron: cold steel-grey infiltration of the frozen administrative spine.
            var playerHealth = BuildEp30OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.64f, 0.70f),     // cool pale steel
                ambient: new Color(0.12f, 0.12f, 0.15f),      // dark cool
                fogColor: new Color(0.26f, 0.28f, 0.32f), fogDensity: 0.012f,
                structureName: "IronSpine",
                accent1: new Color(0.35f, 0.42f, 0.55f),      // deep frost blue
                accent2: new Color(0.58f, 0.60f, 0.65f),      // pale cold grey
                floorLight: new Color(0.50f, 0.52f, 0.56f), floorDark: new Color(0.28f, 0.30f, 0.34f),
                propTint: new Color(0.44f, 0.46f, 0.50f), out _);

            // ---- Khall Hologram intro dialogue (playOnStart) ----
            var khallHologramDialogue = BuildEp30DialoguePlayer("Dialogue_KhallHologram", new Vector3(0f, 1.5f, 2f), "khall_hologram");
            var khSo = new SerializedObject(khallHologramDialogue);
            khSo.FindProperty("playOnStart").boolValue = true;
            khSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- The Warden (lethal, single tough enemy, dark steel tint) ----
            var warden = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var wardenRenderer = warden.GetComponent<Renderer>();
            if (wardenRenderer != null) TintShared(wardenRenderer, new Color(0.30f, 0.32f, 0.40f)); // very dark steel
            warden.gameObject.SetActive(false);
            var wardenHealth = warden.GetComponent<Health>();

            var wardenSpawner = BuildEp03WaveSpawner("WardenSpawner", new Vector3(0f, 0.5f, 12f), 1f,
                new List<List<Health>> { new List<Health> { wardenHealth } },
                new DialoguePlayer[0]); // No spawn dialogue for the Warden

            // ---- Warden clear dialogue (plays after defeat) ----
            var wardenClearDialogue = BuildEp30DialoguePlayer("Dialogue_WardenClear", new Vector3(0f, 1.5f, 12f), "warden_clear");

            // ---- Transition box: "ENTER THE VAULT" ----
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 21.5f), "ENTER THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep30FileSpeaksSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Hologram (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallHologramDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: The Warden (1, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = wardenSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Warden Clear";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = wardenClearDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Enter The Vault";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp30Scene(scene, Galaxy4Ep30IntoTheIronScenePath, Galaxy4Ep30FileSpeaksScenePath);

            Debug.Log($"[Space Samurai] EP30 Into The Iron scene built at {Galaxy4Ep30IntoTheIronScenePath}. " +
                      "Cold steel-grey infiltration of the frozen administrative spine (cool pale steel, deep frost blue + pale cold accents). " +
                      "The Warden opponent (very dark steel, lethal, single enemy). " +
                      "4 steps: khall_hologram (auto, Khall's archive welcome) → defeat Warden → warden_clear → ENTER THE VAULT.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP30 File Speaks", priority = 312)]
        public static void BuildEp30FileSpeaks()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // File Speaks: vault chamber, slightly warmer accent than scene 3 to feel like an opened vault.
            var playerHealth = BuildEp30OnFootShell(refs, weapon,
                keyLight: new Color(0.64f, 0.66f, 0.71f),     // cool warm pale
                ambient: new Color(0.12f, 0.13f, 0.16f),      // cool dim
                fogColor: new Color(0.27f, 0.29f, 0.33f), fogDensity: 0.011f,
                structureName: "VaultChamber",
                accent1: new Color(0.38f, 0.44f, 0.56f),      // warmer frost blue
                accent2: new Color(0.60f, 0.62f, 0.67f),      // pale warm grey
                floorLight: new Color(0.52f, 0.54f, 0.58f), floorDark: new Color(0.29f, 0.31f, 0.35f),
                propTint: new Color(0.46f, 0.48f, 0.52f), out _);

            // ---- File Speaks dialogue (playOnStart) — SUBJECT 7 file + younger-Soren recording + name reclaim ----
            var fileSpeaksDialogue = BuildEp30DialoguePlayer("Dialogue_FileSpeaks", new Vector3(0f, 1.5f, 2f), "file_speaks");
            var fsSo = new SerializedObject(fileSpeaksDialogue);
            fsSo.FindProperty("playOnStart").boolValue = true;
            fsSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Docking-ring soldiers (lethal, dark Dominion tint) in 1 wave ----
            var soldierPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(-0.5f, 0f, 10.5f),
                new Vector3(0.5f, 0f, 11f),
            };
            var soldierHealths = new List<Health>();
            foreach (var pos in soldierPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.32f, 0.40f)); // dark Dominion
                enemy.gameObject.SetActive(false);
                soldierHealths.Add(enemy.GetComponent<Health>());
            }

            var soldierSpawner = BuildEp03WaveSpawner("SoldierSpawner", new Vector3(0f, 0.5f, 11f), 2f,
                new List<List<Health>> { soldierHealths },
                new[] { BuildEp30DialoguePlayer("Dialogue_DockingEscape", new Vector3(0f, 1.5f, 11f), "docking_escape") });

            // ---- Transition box: "ESCAPE — TO THE STARS" ----
            var escapeBoxGo = BuildTransitionBox("ToHollowKingsBox", new Vector3(0f, 1.2f, 21.5f), "ESCAPE — TO THE STARS",
                out var escapeBtn, out var escapeTransition);
            var ebSo = new SerializedObject(escapeTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy4Ep30HollowKingsSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(escapeBtn.onClick,
                new UnityEngine.Events.UnityAction(escapeTransition.LoadOnFootScene));
            escapeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: File Speaks (playOnStart, SUBJECT 7 + name reclaim)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = fileSpeaksDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Docking-Ring Soldiers (4, docking_escape)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = soldierSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Escape — To The Stars";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = escapeBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp30Scene(scene, Galaxy4Ep30FileSpeaksScenePath, Galaxy4Ep30HollowKingsScenePath);

            Debug.Log($"[Space Samurai] EP30 File Speaks scene built at {Galaxy4Ep30FileSpeaksScenePath}. " +
                      "Vault chamber (warmer cool palette, warmer frost blue + pale warm grey accents). " +
                      "4 docking-ring Dominion soldiers (dark Dominion tint, lethal). " +
                      "3 steps: file_speaks (auto, SUBJECT 7 archive + younger Soren recording + name reclaim) → defeat 4 soldiers (docking_escape) → ESCAPE — TO THE STARS.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP30 Scenes", priority = 315)]
        public static void BuildAllEp30Scenes()
        {
            BuildEp30PriceOfMemory();     // in Ep30BuilderFinale.cs
            BuildEp30SepulcherApproach(); // in Ep30BuilderFinale.cs
            BuildEp30IntoTheIron();
            BuildEp30FileSpeaks();
            BuildEp30HollowKings();       // in Ep30BuilderFinale.cs
            BuildEp30NameRemains();       // in Ep30BuilderFinale.cs
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP30 scenes built + inputs rewired. GALAXY 4 EP30 COMPLETE.");
        }
    }
}

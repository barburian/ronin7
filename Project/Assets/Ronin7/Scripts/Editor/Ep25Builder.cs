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
    /// EP25 "The Sterile Reckoning" (Galaxy 4 launch) scene builders for the first three on-foot scenes
    /// on the Crimson Thread medical station in the Sable Drift, where Dr. Heris (Cipher's builder from Kethel-7)
    /// waits in hiding. Cipher must infiltrate the Gilded Maw facility to find her and interrupt his failsafe.
    /// - Cargo Shelf: docking bay with Gilded Maw mercs in zero-G deployment (ZeroGFloatController)
    /// - Surgical Lab: medical theater; Dr. Heris reveals herself; cyborg enforcers
    /// - Real Record: sealed archive; the Kethel-7 massacre revealed; nonlethal Dominion commandos
    ///
    /// Zero-G combat uses the EP25 signature mechanic <see cref="ZeroGFloatController"/>: mercs float
    /// in a gentle bob+sway pattern, desynchronized, creating an eerie weightless ambiance.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names).
        private const string Galaxy4Ep25CargoShelfScenePath    = SceneFolder + "/Galaxy4_EP25_CargoShelf.unity";
        private const string Galaxy4Ep25SurgicalLabScenePath   = SceneFolder + "/Galaxy4_EP25_SurgicalLab.unity";
        private const string Galaxy4Ep25RealRecordScenePath    = SceneFolder + "/Galaxy4_EP25_RealRecord.unity";
        private const string Galaxy4Ep25VaultScenePath         = SceneFolder + "/Galaxy4_EP25_Vault.unity";
        private const string Galaxy4Ep25KhallWireScenePath     = SceneFolder + "/Galaxy4_EP25_KhallWire.unity";
        private const string Galaxy4Ep25PurposeUntoldScenePath = SceneFolder + "/Galaxy4_EP25_PurposeUntold.unity";

        private static readonly string Galaxy4Ep25CargoShelfSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25CargoShelfScenePath);
        private static readonly string Galaxy4Ep25SurgicalLabSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25SurgicalLabScenePath);
        private static readonly string Galaxy4Ep25RealRecordSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25RealRecordScenePath);
        private static readonly string Galaxy4Ep25VaultSceneName         = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25VaultScenePath);
        private static readonly string Galaxy4Ep25KhallWireSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25KhallWireScenePath);
        private static readonly string Galaxy4Ep25PurposeUntoldSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep25PurposeUntoldScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP25 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep25" and loads lines from Ep25Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp25DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep25Lines.Get(setId), advanceRef, setId, clipPrefix: "ep25");
        }

        /// <summary>Standard EP25 on-foot scene scaffold shared by scenes 1-5: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp25OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        /// add an AllyCombatant.</summary>
        private static GameObject BuildEp25Npc(string displayName, Vector3 position, Color tint)
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

        /// <summary>Builds a wave of zero-G floating mercs and attaches a ZeroGFloatController that drives
        /// gentle bob+sway motion with desynchronized phase offsets. Returns the wave's Health list.</summary>
        private static List<Health> BuildEp25ZeroGWave(Vector3[] positions, Health playerHealth,
            EnemyDefinition enemyDef, Color tint)
        {
            var waveHealths = new List<Health>();
            var meleeAttackers = new List<MeleeAttacker>();
            foreach (var pos in positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, tint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                    meleeAttackers.Add(meleeAttacker);
                enemy.gameObject.SetActive(false);
                waveHealths.Add(enemy.GetComponent<Health>());
            }

            // Zero-G float: gentle bob+sway with desynchronized phase, creating eerie weightless ambiance.
            var zgGo = new GameObject("ZeroGFloat");
            var zg = zgGo.AddComponent<ZeroGFloatController>();
            var zgSo = new SerializedObject(zg);
            var membersProp = zgSo.FindProperty("members");
            membersProp.arraySize = meleeAttackers.Count;
            for (int i = 0; i < meleeAttackers.Count; i++)
                membersProp.GetArrayElementAtIndex(i).objectReferenceValue = meleeAttackers[i];
            zgSo.ApplyModifiedPropertiesWithoutUndo();

            return waveHealths;
        }

        private static void FinishEp25Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Merc tint: gold-chrome
        private static readonly Color Ep25MercTint = new Color(0.80f, 0.68f, 0.35f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Cargo Shelf", priority = 270)]
        public static void BuildEp25CargoShelf()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Cargo Shelf: sterile chrome cargo bay, clinical cyan + emergency red.
            var playerHealth = BuildEp25OnFootShell(refs, weapon,
                keyLight: new Color(0.62f, 0.66f, 0.72f),
                ambient: new Color(0.14f, 0.15f, 0.18f),
                fogColor: new Color(0.20f, 0.22f, 0.27f), fogDensity: 0.015f,
                structureName: "CargoShelf",
                accent1: new Color(0.55f, 0.80f, 0.95f),   // clinical cyan
                accent2: new Color(1f, 0.42f, 0.34f),      // emergency red
                floorLight: new Color(0.50f, 0.52f, 0.56f), floorDark: new Color(0.28f, 0.30f, 0.34f),
                propTint: new Color(0.52f, 0.54f, 0.58f), out _);

            // ---- Gilded Maw mercs in zero-G deployment ----
            var waveAPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(0f, 0f, 9.5f),
            };
            var waveA = BuildEp25ZeroGWave(waveAPositions, playerHealth, enemyDef, Ep25MercTint);

            var waveBPositions = new Vector3[]
            {
                new Vector3(-1f, 0f, 11f),
                new Vector3(1f, 0f, 11.5f),
                new Vector3(0f, 0f, 12.5f),
            };
            var waveB = BuildEp25ZeroGWave(waveBPositions, playerHealth, enemyDef, Ep25MercTint);

            var waveCPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 14f),
                new Vector3(1.5f, 0f, 14.5f),
            };
            var waveC = BuildEp25ZeroGWave(waveCPositions, playerHealth, enemyDef, Ep25MercTint);

            var mercSpawner = BuildWaveSpawner("MercSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { waveA, waveB, waveC },
                new[] { BuildEp25DialoguePlayer("Dialogue_CargoBarks", new Vector3(0f, 1.5f, 8f), "cargo_barks") });

            // ---- Dialogue Players ----
            var leadIntroDialogue = BuildEp25DialoguePlayer("Dialogue_LeadIntro", new Vector3(0f, 1.5f, 2f), "lead_intro");
            var liSo = new SerializedObject(leadIntroDialogue);
            liSo.FindProperty("playOnStart").boolValue = true;
            liSo.ApplyModifiedPropertiesWithoutUndo();

            var harrowGreetingDialogue = BuildEp25DialoguePlayer("Dialogue_HarrowGreeting", new Vector3(0f, 1.5f, 5f), "harrow_greeting");

            // Transition box: "INTO THE MEDICAL CORE".
            var medicalBoxGo = BuildTransitionBox("IntoMedicalBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE MEDICAL CORE",
                out var medicalBtn, out var medicalTransition);
            var mbSo = new SerializedObject(medicalTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep25SurgicalLabSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(medicalBtn.onClick,
                new UnityEngine.Events.UnityAction(medicalTransition.LoadOnFootScene));
            medicalBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Lead Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = leadIntroDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Harrow Greeting";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = harrowGreetingDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Gilded Maw Mercs (8, ZeroG)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = mercSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Into the Medical Core";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = medicalBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp25Scene(scene, Galaxy4Ep25CargoShelfScenePath, Galaxy4Ep25SurgicalLabScenePath);

            Debug.Log($"[Space Samurai] EP25 Cargo Shelf scene built at {Galaxy4Ep25CargoShelfScenePath}. " +
                      "Sterile chrome cargo bay, clinical cyan + emergency red. " +
                      "8 Gilded Maw mercs (gold-chrome, ZeroGFloatController) in 3 waves. " +
                      "4 steps: lead_intro (auto) → harrow_greeting → defeat 8 mercs (cargo_barks) → into the medical core.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Surgical Lab", priority = 271)]
        public static void BuildEp25SurgicalLab()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Surgical Lab: surgical theater, antiseptic white + violet records.
            var playerHealth = BuildEp25OnFootShell(refs, weapon,
                keyLight: new Color(0.68f, 0.70f, 0.74f),
                ambient: new Color(0.15f, 0.15f, 0.17f),
                fogColor: new Color(0.22f, 0.22f, 0.26f), fogDensity: 0.016f,
                structureName: "SurgicalLab",
                accent1: new Color(0.85f, 0.90f, 0.95f),   // surgical white
                accent2: new Color(0.65f, 0.45f, 0.95f),   // record violet
                floorLight: new Color(0.52f, 0.53f, 0.56f), floorDark: new Color(0.30f, 0.31f, 0.34f),
                propTint: new Color(0.55f, 0.56f, 0.60f), out _);

            // ---- Dr. Heris (architect, non-combatant) ----
            BuildEp25Npc("Dr. Heris", new Vector3(-1.2f, 0f, 16f), new Color(0.7f, 0.6f, 0.8f));

            // ---- Cyborg enforcers (trained on Cipher's specs) ----
            var enforcerTint = new Color(0.45f, 0.47f, 0.52f);
            var enforcerPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(2f, 0f, 10f),
            };
            var enforcerHealths = new List<Health>();
            foreach (var pos in enforcerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerTint);
                // Enforcers are trained on the player's own combat patterns.
                enemy.gameObject.AddComponent<EchoHunter>();
                enemy.gameObject.SetActive(false);
                enforcerHealths.Add(enemy.GetComponent<Health>());
            }

            var enforcerSpawner = BuildWaveSpawner("EnforcerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { enforcerHealths },
                new DialoguePlayer[0]);

            // ---- Dialogue Players ----
            var architectDialogue = BuildEp25DialoguePlayer("Dialogue_Architect", new Vector3(0f, 1.5f, 2f), "architect");
            var archSo = new SerializedObject(architectDialogue);
            archSo.FindProperty("playOnStart").boolValue = true;
            archSo.ApplyModifiedPropertiesWithoutUndo();

            var enforcerAftermathDialogue = BuildEp25DialoguePlayer("Dialogue_EnforcerAftermath", new Vector3(0f, 1.5f, 16f), "enforcer_aftermath");

            // Transition box: "TO THE SEALED LAB".
            var labBoxGo = BuildTransitionBox("ToSealedLabBox", new Vector3(0f, 1.2f, 21.5f), "TO THE SEALED LAB",
                out var labBtn, out var labTransition);
            var lbSo = new SerializedObject(labTransition);
            lbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep25RealRecordSceneName;
            lbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(labBtn.onClick,
                new UnityEngine.Events.UnityAction(labTransition.LoadOnFootScene));
            labBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Architect";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = architectDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Cyborg Enforcers (3, EchoHunter)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Enforcer Aftermath";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = enforcerAftermathDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Sealed Lab";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = labBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp25Scene(scene, Galaxy4Ep25SurgicalLabScenePath, Galaxy4Ep25RealRecordScenePath);

            Debug.Log($"[Space Samurai] EP25 Surgical Lab scene built at {Galaxy4Ep25SurgicalLabScenePath}. " +
                      "Surgical theater, antiseptic white + record violet. Dr. Heris (architect, non-combatant). " +
                      "3 cyborg enforcers (chrome-grey, EchoHunter). " +
                      "4 steps: architect (auto, Dr. Heris reveal) → defeat 3 enforcers (commando_barks) → enforcer_aftermath dialogue → to the sealed lab.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP25 Real Record", priority = 272)]
        public static void BuildEp25RealRecord()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Real Record: sealed archive lab, cold blue record-glow.
            var playerHealth = BuildEp25OnFootShell(refs, weapon,
                keyLight: new Color(0.55f, 0.60f, 0.72f),
                ambient: new Color(0.13f, 0.14f, 0.18f),
                fogColor: new Color(0.18f, 0.20f, 0.28f), fogDensity: 0.017f,
                structureName: "RecordLab",
                accent1: new Color(0.5f, 0.85f, 1f),       // data cyan
                accent2: new Color(0.7f, 0.78f, 0.88f),    // cold white
                floorLight: new Color(0.46f, 0.50f, 0.56f), floorDark: new Color(0.26f, 0.30f, 0.36f),
                propTint: new Color(0.48f, 0.54f, 0.62f), out _);

            // ---- Dr. Heris ----
            BuildEp25Npc("Dr. Heris", new Vector3(-1.2f, 0f, 3f), new Color(0.7f, 0.6f, 0.8f));

            // ---- Dominion commandos (nonlethal incapacitation) ----
            var commandoTint = new Color(0.38f, 0.40f, 0.44f);
            var commandoPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2f, 0f, 9f),
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(1.5f, 0f, 12f),
            };
            var commandoHealths = new List<Health>();
            foreach (var pos in commandoPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, commandoTint);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                commandoHealths.Add(enemy.GetComponent<Health>());
            }

            var commandoSpawner = BuildWaveSpawner("CommandoSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { commandoHealths },
                new[] { BuildEp25DialoguePlayer("Dialogue_CommandoBarks", new Vector3(0f, 1.5f, 8f), "commando_barks") });

            // ---- Dialogue Players ----
            var realRecordDialogue = BuildEp25DialoguePlayer("Dialogue_RealRecord", new Vector3(0f, 1.5f, 2f), "real_record");
            var rrSo = new SerializedObject(realRecordDialogue);
            rrSo.FindProperty("playOnStart").boolValue = true;
            rrSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "TO THE VAULT".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 21.5f), "TO THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep25VaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Real Record";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = realRecordDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Commandos (5, nonLethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = commandoSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: To the Vault";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp25Scene(scene, Galaxy4Ep25RealRecordScenePath, Galaxy4Ep25VaultScenePath);

            Debug.Log($"[Space Samurai] EP25 Real Record scene built at {Galaxy4Ep25RealRecordScenePath}. " +
                      "Sealed archive lab, cold blue record-glow. Dr. Heris witnesses. " +
                      "5 Dominion commandos (grey, nonLethal incapacitation, NO EchoHunter). " +
                      "3 steps: real_record (auto, Kethel-7 massacre revealed) → defeat 5 commandos (commando_barks) → to the vault.");
        }
    }
}

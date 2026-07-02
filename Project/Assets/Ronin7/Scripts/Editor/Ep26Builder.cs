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
    /// EP26 "The Requiem Protocol" (Galaxy 4 conclusion) scene builders for the first three on-foot scenes
    /// on the Absolution, the Pale Choir's cathedral-shrine where Cipher discovers his original purpose:
    /// a confessor trained to carry the final truths of the dead via his katana's neural-link.
    /// - Ashen Deep: volcanic-moon waystation; Pale Choir operatives (pale ceremonial tint, nonLethal).
    /// - Docking Bay: cathedral-ship docking bay, obsidian + amber candlelight; robed assassins (lethal, with EchoHunter).
    /// - Nave: candlelit stone dome (Nave of Confessions); Varrik duel with DuelYield + ConfessorLink reveals the confessor function.
    ///
    /// The EP26 signature mechanic is <see cref="ConfessorLink"/>: the duel-of-wills tracker that measures
    /// "hunger" (the neural-link pressure to consume) vs. "restraint" (holding back from the kill).
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names).
        private const string Galaxy4Ep26AshenDeepScenePath      = SceneFolder + "/Galaxy4_EP26_AshenDeep.unity";
        private const string Galaxy4Ep26DockingBayScenePath     = SceneFolder + "/Galaxy4_EP26_DockingBay.unity";
        private const string Galaxy4Ep26NaveScenePath           = SceneFolder + "/Galaxy4_EP26_Nave.unity";
        private const string Galaxy4Ep26ArchiveCorridorScenePath = SceneFolder + "/Galaxy4_EP26_ArchiveCorridor.unity";
        private const string Galaxy4Ep26DroneEscapeScenePath    = SceneFolder + "/Galaxy4_EP26_DroneEscape.unity";
        private const string Galaxy4Ep26CargoBayScenePath       = SceneFolder + "/Galaxy4_EP26_CargoBay.unity";

        private static readonly string Galaxy4Ep26AshenDeepSceneName       = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26AshenDeepScenePath);
        private static readonly string Galaxy4Ep26DockingBaySceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26DockingBayScenePath);
        private static readonly string Galaxy4Ep26NaveSceneName            = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26NaveScenePath);
        private static readonly string Galaxy4Ep26ArchiveCorridorSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26ArchiveCorridorScenePath);
        private static readonly string Galaxy4Ep26DroneEscapeSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26DroneEscapeScenePath);
        private static readonly string Galaxy4Ep26CargoBaySceneName        = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep26CargoBayScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP26 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep26" and loads lines from Ep26Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp26DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep26Lines.Get(setId), advanceRef, setId, clipPrefix: "ep26");
        }

        /// <summary>Standard EP26 on-foot scene scaffold shared by scenes 1-5: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp26OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        private static GameObject BuildEp26Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp26Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Pale Choir operative tint: pale ceremonial
        private static readonly Color Ep26PaleChoir = new Color(0.85f, 0.82f, 0.78f);
        // Robed assassin tint: dark robed
        private static readonly Color Ep26RobeAssassin = new Color(0.3f, 0.28f, 0.35f);
        // Varrik tint: Rustfang rust
        private static readonly Color Ep26VarrikTint = new Color(0.6f, 0.4f, 0.3f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Ashen Deep", priority = 280)]
        public static void BuildEp26AshenDeep()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ashen Deep: sulfurous/ash with neon + vent-glow.
            var playerHealth = BuildEp26OnFootShell(refs, weapon,
                keyLight: new Color(0.65f, 0.58f, 0.52f),     // warm grey
                ambient: new Color(0.12f, 0.10f, 0.09f),      // dark warm
                fogColor: new Color(0.22f, 0.16f, 0.13f), fogDensity: 0.018f,
                structureName: "AshenStation",
                accent1: new Color(1f, 0.45f, 0.18f),         // lava orange
                accent2: new Color(0.4f, 0.85f, 0.95f),       // neon cyan
                floorLight: new Color(0.45f, 0.40f, 0.36f), floorDark: new Color(0.25f, 0.22f, 0.20f),
                propTint: new Color(0.40f, 0.38f, 0.36f), out _);

            // ---- Pale Choir operatives (nonLethal submission strikes) ----
            var waveAPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
            };
            var waveA = new List<Health>();
            foreach (var pos in waveAPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep26PaleChoir);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                waveA.Add(enemy.GetComponent<Health>());
            }

            var waveBPositions = new Vector3[]
            {
                new Vector3(-1f, 0f, 11f),
                new Vector3(1f, 0f, 11.5f),
                new Vector3(0f, 0f, 12.5f),
            };
            var waveB = new List<Health>();
            foreach (var pos in waveBPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep26PaleChoir);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                waveB.Add(enemy.GetComponent<Health>());
            }

            var operativeSpawner = BuildEp03WaveSpawner("OperativeSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { waveA, waveB },
                new[] { BuildEp26DialoguePlayer("Dialogue_AshenBarks", new Vector3(0f, 1.5f, 8f), "ashen_barks") });

            // ---- Dialogue Players ----
            var contractIntroDialogue = BuildEp26DialoguePlayer("Dialogue_ContractIntro", new Vector3(0f, 1.5f, 2f), "contract_intro");
            var ciSo = new SerializedObject(contractIntroDialogue);
            ciSo.FindProperty("playOnStart").boolValue = true;
            ciSo.ApplyModifiedPropertiesWithoutUndo();

            var mortisSummonsDialogue = BuildEp26DialoguePlayer("Dialogue_MortisSummons", new Vector3(0f, 1.5f, 5f), "mortis_summons");

            // Transition box: "TO THE ABSOLUTION".
            var dockingBoxGo = BuildTransitionBox("ToDockingBox", new Vector3(0f, 1.2f, 21.5f), "TO THE ABSOLUTION",
                out var dockingBtn, out var dockingTransition);
            var dbSo = new SerializedObject(dockingTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep26DockingBaySceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dockingBtn.onClick,
                new UnityEngine.Events.UnityAction(dockingTransition.LoadOnFootScene));
            dockingBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Contract Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = contractIntroDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Pale Choir Operatives (5, nonLethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = operativeSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Mortis Summons";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = mortisSummonsDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: To the Absolution";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = dockingBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp26Scene(scene, Galaxy4Ep26AshenDeepScenePath, Galaxy4Ep26DockingBayScenePath);

            Debug.Log($"[Space Samurai] EP26 Ashen Deep scene built at {Galaxy4Ep26AshenDeepScenePath}. " +
                      "Volcanic-moon waystation, sulfurous ash with neon + vent-glow. " +
                      "5 Pale Choir operatives (pale ceremonial, nonLethal submission strikes) in 2 waves. " +
                      "4 steps: contract_intro (auto, Varrik briefing) → defeat 5 operatives (ashen_barks) → mortis_summons (voice recording) → to the absolution.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Docking Bay", priority = 281)]
        public static void BuildEp26DockingBay()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Docking Bay: obsidian + amber candlelight.
            var playerHealth = BuildEp26OnFootShell(refs, weapon,
                keyLight: new Color(0.55f, 0.45f, 0.42f),     // low warm
                ambient: new Color(0.08f, 0.07f, 0.08f),      // very dark
                fogColor: new Color(0.18f, 0.15f, 0.14f), fogDensity: 0.019f,
                structureName: "DockingBay",
                accent1: new Color(1f, 0.72f, 0.32f),         // candle amber
                accent2: new Color(0.45f, 0.35f, 0.6f),       // obsidian violet
                floorLight: new Color(0.42f, 0.38f, 0.36f), floorDark: new Color(0.22f, 0.20f, 0.18f),
                propTint: new Color(0.38f, 0.35f, 0.38f), out _);

            // ---- Robed assassins (lethal, with EchoHunter "calibration") ----
            var assassinPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(0f, 0f, 9f),
                new Vector3(2f, 0f, 10f),
            };
            var assassinHealths = new List<Health>();
            foreach (var pos in assassinPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep26RobeAssassin);
                // 1-2 assassins get EchoHunter to "test/calibrate" the player.
                if (assassinHealths.Count < 2)
                {
                    enemy.gameObject.AddComponent<EchoHunter>();
                }
                enemy.gameObject.SetActive(false);
                assassinHealths.Add(enemy.GetComponent<Health>());
            }

            var assassinSpawner = BuildEp03WaveSpawner("AssassinSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { assassinHealths },
                new[] { BuildEp26DialoguePlayer("Dialogue_DockingBarks", new Vector3(0f, 1.5f, 8f), "docking_barks") });

            // ---- Dialogue Players ----
            var cathedralShadowDialogue = BuildEp26DialoguePlayer("Dialogue_CathedralShadow", new Vector3(0f, 1.5f, 2f), "cathedral_shadow");
            var csSo = new SerializedObject(cathedralShadowDialogue);
            csSo.FindProperty("playOnStart").boolValue = true;
            csSo.ApplyModifiedPropertiesWithoutUndo();

            var mortisGreetingDialogue = BuildEp26DialoguePlayer("Dialogue_MortisGreeting", new Vector3(0f, 1.5f, 14f), "mortis_greeting");

            // Transition box: "INTO THE NAVE".
            var naveBoxGo = BuildTransitionBox("IntoNaveBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE NAVE",
                out var naveBtn, out var naveTransition);
            var nbSo = new SerializedObject(naveTransition);
            nbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep26NaveSceneName;
            nbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(naveBtn.onClick,
                new UnityEngine.Events.UnityAction(naveTransition.LoadOnFootScene));
            naveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Cathedral Shadow";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = cathedralShadowDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Robed Assassins (3, lethal + EchoHunter x2)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = assassinSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Mortis Greeting";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = mortisGreetingDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Into the Nave";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = naveBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp26Scene(scene, Galaxy4Ep26DockingBayScenePath, Galaxy4Ep26NaveScenePath);

            Debug.Log($"[Space Samurai] EP26 Docking Bay scene built at {Galaxy4Ep26DockingBayScenePath}. " +
                      "Cathedral-ship docking bay, obsidian + amber candlelight. " +
                      "3 robed assassins (dark robed, lethal, 2x EchoHunter for calibration). " +
                      "4 steps: cathedral_shadow (auto, Kessler's unease) → defeat 3 assassins (docking_barks) → mortis_greeting (High Deacon emerges) → into the nave.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP26 Nave", priority = 282)]
        public static void BuildEp26Nave()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Nave: candlelit stone dome, warm candle-gold + deep shadow.
            var playerHealth = BuildEp26OnFootShell(refs, weapon,
                keyLight: new Color(0.70f, 0.62f, 0.52f),     // warm dim
                ambient: new Color(0.10f, 0.09f, 0.08f),      // dark warm
                fogColor: new Color(0.20f, 0.17f, 0.14f), fogDensity: 0.020f,
                structureName: "Nave",
                accent1: new Color(1f, 0.78f, 0.4f),          // gold candle
                accent2: new Color(0.6f, 0.5f, 0.75f),        // pale violet shrine
                floorLight: new Color(0.48f, 0.44f, 0.40f), floorDark: new Color(0.24f, 0.21f, 0.18f),
                propTint: new Color(0.42f, 0.38f, 0.36f), out _);

            // ---- Varrik (Rustfang captain, single duel opponent), starts INACTIVE ----
            var varrik = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var varrikGo = varrik.gameObject;
            varrikGo.name = "Varrik";
            var varrikRenderer = varrik.GetComponent<Renderer>();
            if (varrikRenderer != null) TintShared(varrikRenderer, Ep26VarrikTint);
            var varrikHealth = varrik.GetComponent<Health>();

            // Varrik's strikes are non-lethal — this is a duel of wills, not an execution.
            var varrikMelee = varrik.GetComponent<MeleeAttacker>();
            if (varrikMelee != null)
            {
                var maSo = new SerializedObject(varrikMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var varrikNpc = varrikGo.AddComponent<StoryNpc>();
            var vNpcSo = new SerializedObject(varrikNpc);
            vNpcSo.FindProperty("displayName").stringValue = "Varrik";
            vNpcSo.FindProperty("remote").boolValue = false;
            vNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // The shell already built the player sword ("Sword"); re-find its Grabbable for the DuelYield wire.
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // DuelYield: yield at 0.25. The duel ends in a YIELD (not a death), so it is wired as a
            // null-prompt mission step that DuelYield.onAccepted advances — never a DefeatWaves step.
            var duelYield = varrikGo.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", varrikHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.25f;
            if (varrikMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { varrikMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // varrik_duel bark ("You're not a confessor! You eat souls!") plays when Varrik yields.
            var varrikDuelDialogue = BuildEp26DialoguePlayer("Dialogue_VarrikDuel", new Vector3(0f, 1.5f, 12f), "varrik_duel");
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(varrikDuelDialogue.Play));

            varrikGo.SetActive(false);

            // ---- ConfessorLink component (EP26 duel-of-wills mechanic) ----
            var linkGo = new GameObject("ConfessorLink");
            var link = linkGo.AddComponent<ConfessorLink>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("linkActive").boolValue = true;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            var naveRevealDialogue = BuildEp26DialoguePlayer("Dialogue_NaveReveal", new Vector3(0f, 1.5f, 2f), "nave_reveal");
            var nrSo = new SerializedObject(naveRevealDialogue);
            nrSo.FindProperty("playOnStart").boolValue = true;
            nrSo.ApplyModifiedPropertiesWithoutUndo();

            var katanaVoDialogue = BuildEp26DialoguePlayer("Dialogue_KatanaVo", new Vector3(0f, 1.5f, 4f), "katana_vo");

            var varrikTestDialogue = BuildEp26DialoguePlayer("Dialogue_VarrikTest", new Vector3(0f, 1.5f, 6f), "varrik_test");

            var refuseDialogue = BuildEp26DialoguePlayer("Dialogue_Refuse", new Vector3(0f, 1.5f, 16f), "refuse");

            // Transition box: "TO THE ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 21.5f), "TO THE ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var abSo = new SerializedObject(archiveTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy4Ep26ArchiveCorridorSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Nave Reveal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = naveRevealDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Katana VO";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = katanaVoDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Varrik Test";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = varrikTestDialogue;

            // Trigger: activate Varrik for the duel of wills.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s3.FindPropertyRelative("label").stringValue = "Trigger: Activate Varrik Duel";
            var activateProp3 = s3.FindPropertyRelative("triggerObjects");
            activateProp3.arraySize = 1;
            activateProp3.GetArrayElementAtIndex(0).objectReferenceValue = varrikGo;

            // Prompt (null): the duel — DuelYield.onAccepted advances it once the player yields + sheathes.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Varrik Duel (yield + sheathe, refuse to consume)";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = null;

            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Refuse";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = refuseDialogue;

            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: To the Archive";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → AdvanceFromPrompt (advance the null-prompt duel step) and
            // → ConfessorLink.Resolve (the mechanic computes Refused when the player chooses not to consume).
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(link.Resolve));

            FinishEp26Scene(scene, Galaxy4Ep26NaveScenePath, Galaxy4Ep26ArchiveCorridorScenePath);

            Debug.Log($"[Space Samurai] EP26 Nave scene built at {Galaxy4Ep26NaveScenePath}. " +
                      "Candlelit stone dome (Nave of Confessions), warm candle-gold + deep shadow. " +
                      "Varrik (Rustfang rust tint) with DuelYield: yield at 25%, varrik_duel bark on yield, sheathe sword to accept (auto-accept 30s). " +
                      "ConfessorLink active; onAccepted → AdvanceFromPrompt + ConfessorLink.Resolve. " +
                      "7 steps: nave_reveal (auto) → katana_vo → varrik_test → activate Varrik (trigger) → " +
                      "duel Prompt (onAccepted → AdvanceFromPrompt) → refuse dialogue → to the archive.");
        }
    }
}

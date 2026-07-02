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
    /// EP11 "The Iron Dojo" on-foot scene builders. Builds three core episodes:
    /// - Courtyard: monastery courtyard with Master Kaelen's recognition duel
    /// - Dojo: stone dojo interior where Kaelen reveals the truth of the program
    /// - Archive Collapse: collapsing escape corridor ending in archive chamber with data cores
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP11 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep11" and loads lines from Ep11Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp11DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep11Lines.Get(setId), advanceRef, setId, clipPrefix: "ep11");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP11 Courtyard", priority = 123)]
        public static void BuildEp11Courtyard()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Courtyard: bioluminescent jungle monastery with green-blue ambient, light mist fog,
            // pale-green accent lights, cracked-stone floor, weapon-rack/pillar props, entrance arch.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.50f, 0.70f, 0.65f); // green-blue key light
            light.intensity = 0.80f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.45f, 0.42f); // green-blue ambient

            // Light mist fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.40f, 0.50f, 0.48f);
            RenderSettings.fogDensity = 0.015f;

            // Pale-green accent point lights.
            BuildAccentPointLight("CourtyardLight1", new Vector3(-6f, 2.5f, 8f),
                new Color(0.6f, 1f, 0.7f), intensity: 1.2f, range: 13f);
            BuildAccentPointLight("CourtyardLight2", new Vector3(6f, 2f, 12f),
                new Color(0.5f, 0.95f, 0.65f), intensity: 1.1f, range: 12f);

            // ---- Monastery Courtyard: cracked stone floor + props ----
            var courtyardGo = new GameObject("Courtyard");
            var courtyard = courtyardGo.transform;
            var stoneFloor = new Color(0.55f, 0.52f, 0.48f);
            var darkStone = new Color(0.38f, 0.35f, 0.30f);

            // Main courtyard floor (x[-8,8], z[0,20]).
            BuildFloorCeiling(courtyard, "CourtyardFloor", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), stoneFloor, darkStone);

            // Cracked pillars and weapon racks as props.
            BuildProp(courtyard, "Pillar1", new Vector3(-5f, 1f, 5f), new Vector3(1f, 3f, 1f), darkStone);
            BuildProp(courtyard, "Pillar2", new Vector3(5f, 1f, 7f), new Vector3(0.9f, 3.2f, 0.9f), darkStone);
            BuildProp(courtyard, "WeaponRack1", new Vector3(-3f, 0.5f, 12f), new Vector3(0.6f, 2f, 1.5f), darkStone);
            BuildProp(courtyard, "WeaponRack2", new Vector3(4f, 0.5f, 15f), new Vector3(0.6f, 2f, 1.5f), darkStone);

            // Entrance arch prop.
            BuildProp(courtyard, "EntranceArch", new Vector3(0f, 2.5f, 20.5f), new Vector3(5f, 4f, 0.3f), darkStone);

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
            var transitDialogue = BuildEp11DialoguePlayer("Dialogue_TransitQuestion", new Vector3(0f, 1.5f, 2f), "transit_question");
            var transitDlgSo = new SerializedObject(transitDialogue);
            transitDlgSo.FindProperty("playOnStart").boolValue = true;
            transitDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var monasteryDialogue = BuildEp11DialoguePlayer("Dialogue_MonasteryArrival", new Vector3(0f, 1.5f, 8f), "monastery_arrival");
            var recognitionDialogue = BuildEp11DialoguePlayer("Dialogue_RecognitionDuel", new Vector3(0f, 1.5f, 12f), "recognition_duel");

            // ---- Master Kaelen: duelist opponent (recognition duel), starts INACTIVE ----
            // Build as a proper damageable enemy (Health + concrete MeleeAttacker + brain) so the
            // player can spar him; DuelYield resolves the duel once his health crosses the threshold.
            // (MeleeAttacker is abstract — only BuildDominionEnemy adds the concrete attacker.)
            var kaelenPos = new Vector3(0f, 0f, 10f);
            var kaelen = BuildDominionEnemy(kaelenPos, playerHealth, enemyDef);
            kaelen.gameObject.transform.localScale *= 1.1f;
            var kaelenRenderer = kaelen.GetComponent<Renderer>();
            if (kaelenRenderer != null) TintShared(kaelenRenderer, new Color(0.45f, 0.42f, 0.38f)); // stone-robe grey
            var kaelenGo = kaelen.gameObject;
            var kaelenHealth = kaelen.GetComponent<Health>();

            // Master Kaelen's strikes test, never kill, the player.
            var kaelenMelee = kaelen.GetComponent<MeleeAttacker>();
            if (kaelenMelee != null)
            {
                var maSo = new SerializedObject(kaelenMelee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var kaelenNpc = kaelenGo.AddComponent<StoryNpc>();
            var kNpcSo = new SerializedObject(kaelenNpc);
            kNpcSo.FindProperty("displayName").stringValue = "Master Kaelen";
            kNpcSo.FindProperty("remote").boolValue = false;
            kNpcSo.ApplyModifiedPropertiesWithoutUndo();

            // DuelYield: yield at 0.4, onYielded → recognition_duel dialogue, onAccepted → AdvanceFromPrompt.
            var duelYield = kaelenGo.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", kaelenHealth);
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.4f;
            if (kaelenMelee != null) SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { kaelenMelee });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onYielded → recognition_duel dialogue.
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(recognitionDialogue.Play));

            kaelenGo.SetActive(false); // Activate only on the duel trigger step.

            // Transition box: "ENTER — THE DOJO".
            var dojoBoxGo = BuildTransitionBox("ToDojoBox", new Vector3(0f, 1.2f, 20.5f), "ENTER — THE DOJO",
                out var dojoBtn, out var dojoTransition);
            var dbSo = new SerializedObject(dojoTransition);
            dbSo.FindProperty("onFootScene").stringValue = Galaxy2Ep11DojoSceneName;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(dojoBtn.onClick,
                new UnityEngine.Events.UnityAction(dojoTransition.LoadOnFootScene));
            dojoBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue transit_question (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Transit Question";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = transitDialogue;

            // Step 1: Dialogue monastery_arrival.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Monastery Arrival";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = monasteryDialogue;

            // Step 2: Trigger — activate Kaelen duelist.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Activate Kaelen Duel";
            var activateProp2 = s2.FindPropertyRelative("triggerObjects");
            activateProp2.arraySize = 1;
            activateProp2.GetArrayElementAtIndex(0).objectReferenceValue = kaelenGo;

            // Step 3: Prompt — the duel (null promptObject; DuelYield.onAccepted advances it).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Recognition Duel (yield + sheathe)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 4: Prompt — transition to Dojo.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Enter the Dojo";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = dojoBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onAccepted → missionDirector.AdvanceFromPrompt (advances the null-prompt duel step).
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep11CourtyardScenePath);
            EnsureScenesInBuild(Galaxy2Ep11CourtyardScenePath, Galaxy2Ep11DojoScenePath);

            Debug.Log($"[Space Samurai] EP11 Courtyard scene built at {Galaxy2Ep11CourtyardScenePath}. " +
                      "Layout: bioluminescent jungle monastery courtyard with green-blue ambient, light mist fog, pale-green accent lights, cracked-stone floor + pillars/racks/arch props. " +
                      "Master Kaelen elite (DuelYield, yield at 40%, recognition_duel on yield, nonLethal). " +
                      "5 steps: transit_question (auto) → monastery_arrival dialogue → activate Kaelen duel (trigger) → " +
                      "duel Prompt (onAccepted → AdvanceFromPrompt) → transition to Dojo.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP11 Dojo", priority = 124)]
        public static void BuildEp11Dojo()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Dojo: stone interior with smoothed walls, empty weapon racks, meditation cushions,
            // load-bearing pillars as cover. Warm dim + emergency strip accent lights.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.80f, 0.65f, 0.50f); // warm dim key light
            light.intensity = 0.70f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.22f, 0.16f); // warm dim tones

            // Dojo interior fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.45f, 0.38f, 0.30f);
            RenderSettings.fogDensity = 0.020f;

            // Emergency strip accent lights.
            BuildAccentPointLight("DojoLight1", new Vector3(-4f, 2.5f, 6f),
                new Color(1f, 0.7f, 0.3f), intensity: 0.9f, range: 10f);
            BuildAccentPointLight("DojoLight2", new Vector3(4f, 2.5f, 14f),
                new Color(0.95f, 0.65f, 0.25f), intensity: 0.85f, range: 9f);

            // ---- Dojo Interior ----
            var dojoGo = new GameObject("DojoInterior");
            var dojo = dojoGo.transform;
            var smoothStone = new Color(0.50f, 0.48f, 0.45f);
            var darkStone = new Color(0.32f, 0.30f, 0.27f);

            // Main dojo floor and walls.
            BuildFloorCeiling(dojo, "DojoFloor", new Vector3(0f, 0f, 8f), new Vector3(12f, 0f, 16f), smoothStone, darkStone);
            BuildWall(dojo, "DojoWall_W", new Vector3(-6f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(dojo, "DojoWall_E", new Vector3(6f, 1.5f, 8f), new Vector3(0.2f, 3f, 16f));

            // Load-bearing pillars as cover.
            BuildProp(dojo, "Pillar1", new Vector3(-3f, 1f, 4f), new Vector3(1.2f, 3.5f, 1.2f), darkStone);
            BuildProp(dojo, "Pillar2", new Vector3(3f, 1f, 6f), new Vector3(1.2f, 3.5f, 1.2f), darkStone);
            BuildProp(dojo, "Pillar3", new Vector3(-2f, 1f, 12f), new Vector3(1f, 3.2f, 1f), darkStone);
            BuildProp(dojo, "Pillar4", new Vector3(2f, 1f, 14f), new Vector3(1f, 3.2f, 1f), darkStone);

            // Empty weapon racks and meditation cushions.
            BuildProp(dojo, "WeaponRack1", new Vector3(-5f, 0.5f, 3f), new Vector3(0.6f, 2f, 1.5f), darkStone);
            BuildProp(dojo, "WeaponRack2", new Vector3(5f, 0.5f, 5f), new Vector3(0.6f, 2f, 1.5f), darkStone);
            BuildProp(dojo, "Cushion1", new Vector3(-2f, 0.3f, 10f), new Vector3(0.8f, 0.4f, 0.8f), new Color(0.55f, 0.45f, 0.38f));
            BuildProp(dojo, "Cushion2", new Vector3(2f, 0.3f, 12f), new Vector3(0.8f, 0.4f, 0.8f), new Color(0.55f, 0.45f, 0.38f));

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

            // ---- Master Kaelen as ALLY NPC ----
            var kaelenPos = new Vector3(-2f, 0f, 6f);
            var kaelenGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kaelenPos, "MasterKaelenAlly");
            if (kaelenGo != null)
            {
                var allyCombatant = kaelenGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = kaelenGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Master Kaelen";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 4 Hunter-Droids: 2 waves of 2 ----
            var droneColor = new Color(0.45f, 0.50f, 0.58f); // cyan-gray

            var wave1Positions = new Vector3[]
            {
                new Vector3(-3f, 0f, 5f),
                new Vector3(3f, 0f, 5.5f)
            };
            var wave2Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 10f),
                new Vector3(2f, 0f, 10.5f)
            };

            var wave1Healths = new List<Health>();
            var wave2Healths = new List<Health>();

            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.transform.localScale *= 0.5f;
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

            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.transform.localScale *= 0.5f;
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
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var droneSpawner = BuildEp03WaveSpawner("DroneSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new[] { BuildEp11DialoguePlayer("Dialogue_HunterDroidBarks", new Vector3(0f, 1.5f, 8f), "hunterdroid_barks") });

            // ---- Dialogue Players ----
            var confessionDialogue = BuildEp11DialoguePlayer("Dialogue_TheConfession", new Vector3(0f, 1.5f, 4f), "the_confession");
            var confessionDlgSo = new SerializedObject(confessionDialogue);
            confessionDlgSo.FindProperty("playOnStart").boolValue = true;
            confessionDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var flawDialogue = BuildEp11DialoguePlayer("Dialogue_TheFlaw", new Vector3(0f, 1.5f, 10f), "the_flaw");

            // Transition box: "DOWN — THE ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 16.5f), "DOWN — THE ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var arSo = new SerializedObject(archiveTransition);
            arSo.FindProperty("onFootScene").stringValue = Galaxy2Ep11ArchiveCollapseSceneName;
            arSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

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

            // Step 1: DefeatWaves — 4 droids (2 waves of 2).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Hunter-Droids (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = droneSpawner;

            // Step 2: Dialogue the_flaw.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Flaw";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = flawDialogue;

            // Step 3: Prompt — transition to Archive.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Down to Archive";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep11DojoScenePath);
            EnsureScenesInBuild(Galaxy2Ep11DojoScenePath, Galaxy2Ep11ArchiveCollapseScenePath);

            Debug.Log($"[Space Samurai] EP11 Dojo scene built at {Galaxy2Ep11DojoScenePath}. " +
                      "Layout: stone dojo interior (smoothed walls, empty weapon racks, meditation cushions, pillars as cover). " +
                      "Warm dim + emergency strip accent lights. Master Kaelen ally (AllyCombatant, NO Health). " +
                      "4 steps: the_confession (auto) → defeat 4 hunter-droids (2 waves of 2, hunterdroid_barks) → " +
                      "the_flaw dialogue → transition to Archive Collapse.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP11 Archive Collapse", priority = 125)]
        public static void BuildEp11ArchiveCollapse()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive Collapse: escape corridor → archive chamber. Dust/heavy fog, red-amber emergency lights.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.95f, 0.50f, 0.30f); // red-amber key light
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.15f, 0.10f); // red-amber ambient

            // Heavy collapse dust/fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.35f, 0.20f);
            RenderSettings.fogDensity = 0.035f;

            // Red-amber emergency lights.
            BuildAccentPointLight("CollapsedLight1", new Vector3(-4f, 2f, 4f),
                new Color(1f, 0.4f, 0.15f), intensity: 1.1f, range: 11f);
            BuildAccentPointLight("CollapsedLight2", new Vector3(4f, 2.5f, 12f),
                new Color(0.95f, 0.35f, 0.1f), intensity: 1.0f, range: 10f);

            // ---- Archive System: corridor + chamber ----
            var archiveGo = new GameObject("ArchiveComplex");
            var archive = archiveGo.transform;
            var stoneGrey = new Color(0.38f, 0.36f, 0.33f);
            var darkStone = new Color(0.22f, 0.20f, 0.18f);

            // Escape corridor (x[-4,4], z[0,8]).
            BuildFloorCeiling(archive, "CorridorFloor", new Vector3(0f, 0f, 4f), new Vector3(8f, 0f, 8f), stoneGrey, darkStone);
            BuildWall(archive, "CorridorWall_W", new Vector3(-4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(archive, "CorridorWall_E", new Vector3(4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));

            // Archive chamber (x[-5,5], z[8,18]).
            BuildFloorCeiling(archive, "ChamberFloor", new Vector3(0f, 0f, 13f), new Vector3(10f, 0f, 10f), stoneGrey, darkStone);
            BuildWall(archive, "ChamberWall_W", new Vector3(-5f, 1.5f, 13f), new Vector3(0.2f, 3f, 10f));
            BuildWall(archive, "ChamberWall_E", new Vector3(5f, 1.5f, 13f), new Vector3(0.2f, 3f, 10f));
            BuildWall(archive, "ChamberWall_Back", new Vector3(0f, 1.5f, 18f), new Vector3(10f, 3f, 0.2f));

            // Corridor start/end for CollapseSequenceController.
            var corridorStart = new GameObject("CorridorStart");
            corridorStart.transform.SetParent(archive, false);
            corridorStart.transform.position = new Vector3(0f, 0.5f, 0f);

            var corridorEnd = new GameObject("CorridorEnd");
            corridorEnd.transform.SetParent(archive, false);
            corridorEnd.transform.position = new Vector3(0f, 0.5f, 8f);

            // Debris cubes (ceiling sections) for collapse.
            var debrisParent = new GameObject("DebrisParent");
            debrisParent.transform.SetParent(archive, false);

            var debris1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris1.name = "Debris1";
            Object.DestroyImmediate(debris1.GetComponent<Collider>());
            debris1.transform.SetParent(debrisParent.transform, false);
            debris1.transform.position = new Vector3(-2f, 3.5f, 2f);
            debris1.transform.localScale = new Vector3(2f, 1.5f, 2f);
            TintShared(debris1.GetComponent<Renderer>(), darkStone);

            var debris2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris2.name = "Debris2";
            Object.DestroyImmediate(debris2.GetComponent<Collider>());
            debris2.transform.SetParent(debrisParent.transform, false);
            debris2.transform.position = new Vector3(2f, 3.2f, 5f);
            debris2.transform.localScale = new Vector3(1.8f, 1.2f, 1.8f);
            TintShared(debris2.GetComponent<Renderer>(), darkStone);

            var debris3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris3.name = "Debris3";
            Object.DestroyImmediate(debris3.GetComponent<Collider>());
            debris3.transform.SetParent(debrisParent.transform, false);
            debris3.transform.position = new Vector3(-3f, 3.8f, 6.5f);
            debris3.transform.localScale = new Vector3(2.2f, 1.3f, 2f);
            TintShared(debris3.GetComponent<Renderer>(), darkStone);

            // CollapseSequenceController.
            var collapseGo = new GameObject("CollapseSequence");
            collapseGo.transform.SetParent(archive, false);
            var collapse = collapseGo.AddComponent<CollapseSequenceController>();
            var collapseSo = new SerializedObject(collapse);
            SetObjectRef(collapseSo, "corridorStart", corridorStart.transform);
            SetObjectRef(collapseSo, "corridorEnd", corridorEnd.transform);
            SetObjectRef(collapseSo, "playerTransform", null); // Will be auto-wired by RewireOpenScene
            collapseSo.FindProperty("dropDuration").floatValue = 0.6f;
            collapseSo.FindProperty("dropHeight").floatValue = 2f;

            // Setup segments array (3 debris pieces).
            var segmentsProp = collapseSo.FindProperty("segments");
            segmentsProp.arraySize = 3;

            var seg0 = segmentsProp.GetArrayElementAtIndex(0);
            seg0.FindPropertyRelative("triggerProgress").floatValue = 0.2f;
            var debris0Arr = seg0.FindPropertyRelative("debris");
            debris0Arr.arraySize = 1;
            debris0Arr.GetArrayElementAtIndex(0).objectReferenceValue = debris1.transform;

            var seg1 = segmentsProp.GetArrayElementAtIndex(1);
            seg1.FindPropertyRelative("triggerProgress").floatValue = 0.5f;
            var debris1Arr = seg1.FindPropertyRelative("debris");
            debris1Arr.arraySize = 1;
            debris1Arr.GetArrayElementAtIndex(0).objectReferenceValue = debris2.transform;

            var seg2 = segmentsProp.GetArrayElementAtIndex(2);
            seg2.FindPropertyRelative("triggerProgress").floatValue = 0.8f;
            var debris2Arr = seg2.FindPropertyRelative("debris");
            debris2Arr.arraySize = 1;
            debris2Arr.GetArrayElementAtIndex(0).objectReferenceValue = debris3.transform;

            collapseSo.ApplyModifiedPropertiesWithoutUndo();

            // Data-core prop: glowing cylinder in archive chamber.
            var coreProp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coreProp.name = "DataCore";
            Object.DestroyImmediate(coreProp.GetComponent<Collider>());
            coreProp.transform.SetParent(archive, false);
            coreProp.transform.position = new Vector3(0f, 1.5f, 16f);
            coreProp.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
            coreProp.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.4f, 0.8f, 1f));

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

            // Wire CollapseSequenceController's playerTransform.
            var collapseSoRewire = new SerializedObject(collapse);
            SetObjectRef(collapseSoRewire, "playerTransform", rig.transform);
            collapseSoRewire.ApplyModifiedPropertiesWithoutUndo();

            // ---- Master Kaelen as ALLY ----
            var kaelenPos = new Vector3(-1f, 0f, 12f);
            var kaelenGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kaelenPos, "MasterKaelenArchive");
            if (kaelenGo != null)
            {
                var allyCombatant = kaelenGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();

                var storyNpc = kaelenGo.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(storyNpc);
                npcSo.FindProperty("displayName").stringValue = "Master Kaelen";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 3 Shock Troops: 1 wave of 3 ----
            var trooperColor = new Color(0.55f, 0.48f, 0.45f);

            var wavePositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 10f),
                new Vector3(0f, 0f, 11f),
                new Vector3(2f, 0f, 10.5f)
            };

            var waveHealths = new List<Health>();

            foreach (var pos in wavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, trooperColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                waveHealths.Add(enemy.GetComponent<Health>());
            }

            var troopSpawner = BuildEp03WaveSpawner("TroopSpawner", new Vector3(0f, 0.5f, 10.5f), 2f,
                new List<List<Health>> { waveHealths },
                new[] { BuildEp11DialoguePlayer("Dialogue_CollapseBarks", new Vector3(0f, 1.5f, 10f), "collapse_barks") });

            // Reach point at archive chamber center.
            var chamberReachGo = new GameObject("ArchiveChamberReachPoint");
            chamberReachGo.transform.position = new Vector3(0f, 0.5f, 13f);

            // Reach point at data-core.
            var coreReachGo = new GameObject("DataCoreReachPoint");
            coreReachGo.transform.position = new Vector3(0f, 1f, 16f);

            // ---- Dialogue Players ----
            var archiveDialogue = BuildEp11DialoguePlayer("Dialogue_TheArchive", new Vector3(0f, 1.5f, 16f), "the_archive");

            // Transition box: "EXIT — THE HAULER".
            var haulerBoxGo = BuildTransitionBox("ToAscentBox", new Vector3(0f, 1.2f, 18.5f), "EXIT — THE HAULER",
                out var haulerBtn, out var haulerTransition);
            var hSo = new SerializedObject(haulerTransition);
            hSo.FindProperty("onFootScene").stringValue = Galaxy2Ep11AscentSceneName;
            hSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(haulerBtn.onClick,
                new UnityEngine.Events.UnityAction(haulerTransition.LoadOnFootScene));
            haulerBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: ReachTrigger — archive chamber center (player runs the collapsing corridor).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s0.FindPropertyRelative("label").stringValue = "ReachTrigger: Archive Chamber";
            s0.FindPropertyRelative("reachPoint").objectReferenceValue = chamberReachGo.transform;
            s0.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 1: DefeatWaves — 3 shock troops (collapse_barks plays as they breach).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Shock Troops (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = troopSpawner;

            // Step 2: ReachTrigger — data core.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Data Core";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = coreReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 3: Dialogue the_archive.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The Archive";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = archiveDialogue;

            // Step 4: Prompt — exit to Ascent.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Exit to Hauler (Ascent)";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = haulerBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep11ArchiveCollapseScenePath);
            EnsureScenesInBuild(Galaxy2Ep11ArchiveCollapseScenePath);

            Debug.Log($"[Space Samurai] EP11 Archive Collapse scene built at {Galaxy2Ep11ArchiveCollapseScenePath}. " +
                      "Layout: escaping collapse corridor → archive chamber (4 data-core props, 1 glowing). " +
                      "Dust/heavy fog, red-amber emergency lights. Master Kaelen ally (AllyCombatant, NO Health). " +
                      "CollapseSequenceController (3 segments dropping debris behind player, corridorStart/End, player wired). " +
                      "5 steps: reach archive chamber → defeat 3 shock troops (collapse_barks on breach) → " +
                      "reach data core → the_archive dialogue → transition to Hauler (Ascent space).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP11 Scenes", priority = 122)]
        public static void BuildAllEp11Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP11 scenes in order: Courtyard, Dojo, Archive Collapse, Ascent, Exit Point, Galaxy 2...");
            BuildEp11Courtyard();
            BuildEp11Dojo();
            BuildEp11ArchiveCollapse();
            BuildEp11Ascent();
            BuildEp11ExitPoint();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP11 scenes built successfully! Galaxy 2 hub rebuilt to register EP11 completions.");
        }
    }
}

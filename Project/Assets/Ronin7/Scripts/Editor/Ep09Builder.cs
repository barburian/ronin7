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
    /// EP09 "The Spared Lieutenant" on-foot scene builders. Builds three core episodes:
    /// - Char Spire: derelict station where Vera Dusk is first encountered
    /// - Spire Duel: suspended catwalks where Cipher duels Vera (with DuelYield mechanics)
    /// - Kethel-7 Memory: memory-space descent with ghost echo vignettes
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP09 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep09" and loads lines from Ep09Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp09DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep09Lines.Get(setId), advanceRef, setId, clipPrefix: "ep09");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP09 Char Spire", priority = 110)]
        public static void BuildEp09CharSpire()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Char Spire: derelict mining station interior — ash/rust palette, jury-rigged accent lights,
            // low amber emergency lighting.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.65f, 0.55f); // warm amber key
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.16f, 0.12f); // warm dim tones

            // Ash/rust atmosphere with haze.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.48f, 0.38f, 0.30f);
            RenderSettings.fogDensity = 0.025f;

            // Warm jury-rigged accent lights (amber emergency theme).
            BuildAccentPointLight("SpireLight1", new Vector3(-3f, 2.5f, 6f),
                new Color(1f, 0.65f, 0.35f), intensity: 1.2f, range: 11f);
            BuildAccentPointLight("SpireLight2", new Vector3(4f, 2.5f, 14f),
                new Color(1f, 0.6f, 0.3f), intensity: 1.1f, range: 10f);

            // ---- Char Spire interior: docking bay → mag-locked corridor → central tavern ----
            var spireGo = new GameObject("CharSpireInterior");
            var spire = spireGo.transform;
            var ashRust = new Color(0.38f, 0.32f, 0.26f);
            var darkRust = new Color(0.25f, 0.20f, 0.15f);

            // Docking bay floor and ceiling (x[-5,5], z[0,8]).
            BuildFloorCeiling(spire, "DockingFloor", new Vector3(0f, 0f, 4f), new Vector3(10f, 0f, 8f), ashRust, darkRust);
            BuildWall(spire, "DockingWall_W", new Vector3(-5f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(spire, "DockingWall_E", new Vector3(5f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(spire, "DockingWall_Back", new Vector3(0f, 1.5f, 8f), new Vector3(10f, 3f, 0.2f));

            // Mag-locked corridor (x[-4,4], z[8,16]) — narrower, metal grating aesthetic.
            BuildFloorCeiling(spire, "CorridorFloor", new Vector3(0f, 0f, 12f), new Vector3(8f, 0f, 8f), ashRust, darkRust);
            BuildWall(spire, "CorridorWall_W", new Vector3(-4f, 1.5f, 12f), new Vector3(0.2f, 3f, 8f));
            BuildWall(spire, "CorridorWall_E", new Vector3(4f, 1.5f, 12f), new Vector3(0.2f, 3f, 8f));

            // Central tavern (x[-6,6], z[16,24]) — open floor plan.
            BuildFloorCeiling(spire, "TavernFloor", new Vector3(0f, 0f, 20f), new Vector3(12f, 0f, 8f), ashRust, darkRust);
            BuildWall(spire, "TavernWall_W", new Vector3(-6f, 1.5f, 20f), new Vector3(0.2f, 3f, 8f));
            BuildWall(spire, "TavernWall_E", new Vector3(6f, 1.5f, 20f), new Vector3(0.2f, 3f, 8f));
            BuildWall(spire, "TavernWall_Back", new Vector3(0f, 1.5f, 24f), new Vector3(12f, 3f, 0.2f));

            // Tavern counter (glass + frame, no collider — decorative).
            var counterColor = new Color(0.35f, 0.30f, 0.25f);
            BuildProp(spire, "BarCounter", new Vector3(0f, 0.8f, 22f), new Vector3(8f, 1.6f, 1.2f), counterColor);

            // Rotating bounty hologram prop in docking bay: tall thin glass-material box + unlit amber frame.
            var holoPos = new Vector3(-2f, 2.5f, 2f);
            var holoProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            holoProp.name = "BountyHologram";
            Object.DestroyImmediate(holoProp.GetComponent<Collider>());
            holoProp.transform.SetParent(spire, false);
            holoProp.transform.localPosition = holoPos;
            holoProp.transform.localScale = new Vector3(0.6f, 2.2f, 0.3f);
            holoProp.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(new Color(1f, 0.65f, 0.35f), 0.15f);
            // Add a thin amber frame around it.
            for (int i = 0; i < 4; i++)
            {
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = $"HoloFrame{i}";
                Object.DestroyImmediate(frame.GetComponent<Collider>());
                frame.transform.SetParent(spire, false);
                frame.transform.localPosition = holoPos + new Vector3((i % 2 - 0.5f) * 0.65f, (i / 2 - 0.5f) * 2.5f, 0f);
                frame.transform.localScale = new Vector3(0.1f, (i < 2 ? 2.4f : 0.7f), 0.05f);
                TintShared(frame.GetComponent<Renderer>(), new Color(1f, 0.6f, 0.2f));
                frame.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.6f, 0.2f));
            }

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds + standard locomotion.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 40f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Kessler + Mera (StoryNpc, near player spawn in docking bay).
            var kesslerPos = new Vector3(-2f, 0f, 1f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var meraPos = new Vector3(2f, 0f, 2f);
            var meraGo = InstantiateNpc(MeraVossPrefabPath, meraPos, "Mera");
            if (meraGo != null)
            {
                var meraNpc = meraGo.AddComponent<StoryNpc>();
                var mSo = new SerializedObject(meraNpc);
                mSo.FindProperty("displayName").stringValue = "Mera";
                mSo.FindProperty("remote").boolValue = false;
                mSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Barkeep (decorative, behind tavern counter).
            var barkeepPos = new Vector3(0f, 0f, 22f);
            var barkeepGo = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc01.prefab", barkeepPos, "Barkeep");
            if (barkeepGo != null)
            {
                var barkeepNpc = barkeepGo.AddComponent<StoryNpc>();
                var bSo = new SerializedObject(barkeepNpc);
                bSo.FindProperty("displayName").stringValue = "Barkeep";
                bSo.FindProperty("remote").boolValue = false;
                bSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2 patron decoratives with StoryNpcWander.
            var patron1Pos = new Vector3(-3f, 0f, 20f);
            var patron1Go = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc02.prefab", patron1Pos, "Patron1");
            if (patron1Go != null)
            {
                var wanderer = patron1Go.AddComponent<StoryNpcWander>();
                var w1So = new SerializedObject(wanderer);
                w1So.FindProperty("moveSpeed").floatValue = 1.0f;
                w1So.FindProperty("wanderRadius").floatValue = 2f;
                w1So.ApplyModifiedPropertiesWithoutUndo();
            }

            var patron2Pos = new Vector3(3f, 0f, 19f);
            var patron2Go = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc03.prefab", patron2Pos, "Patron2");
            if (patron2Go != null)
            {
                var wanderer = patron2Go.AddComponent<StoryNpcWander>();
                var w2So = new SerializedObject(wanderer);
                w2So.FindProperty("moveSpeed").floatValue = 1.2f;
                w2So.FindProperty("wanderRadius").floatValue = 1.8f;
                w2So.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var dockingDialogue = BuildEp09DialoguePlayer("Dialogue_SpireDocking", new Vector3(-2f, 1.5f, 2f), "spire_docking");
            var dockingDlgSo = new SerializedObject(dockingDialogue);
            dockingDlgSo.FindProperty("playOnStart").boolValue = true;
            dockingDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var bountyDialogue = BuildEp09DialoguePlayer("Dialogue_BountyRead", new Vector3(0f, 1.5f, 6f), "bounty_read");

            var tavernDialogue = BuildEp09DialoguePlayer("Dialogue_TavernMurmur", new Vector3(0f, 1.5f, 20f), "tavern_murmur");

            var gambitDialogue = BuildEp09DialoguePlayer("Dialogue_HuntersGambit", new Vector3(0f, 1.5f, 23f), "hunters_gambit");

            // ---- Enemies: 3 Syndicate Runners in corridor ----
            var runnerColor = new Color(0.55f, 0.45f, 0.40f); // syndicate tint

            var runnerPositions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 11f),
                new Vector3(0f, 0f, 13f),
                new Vector3(2.5f, 0f, 12f)
            };
            var runnerHealths = new List<Health>();
            foreach (var pos in runnerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, runnerColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                runnerHealths.Add(enemy.GetComponent<Health>());
            }

            var runnerSpawner = BuildEp03WaveSpawner("RunnerSpawner", new Vector3(0f, 0.5f, 12f), 2.5f,
                new List<List<Health>> { runnerHealths }, new[] { BuildEp09DialoguePlayer("Dialogue_CorridorRunners", new Vector3(0f, 1.5f, 12f), "corridor_runners") });

            // Tavern entrance reach trigger.
            var tavernReachGo = new GameObject("TavernReachPoint");
            tavernReachGo.transform.position = new Vector3(0f, 1f, 16f);

            // Vera Dusk seated at tavern rear (masked, dark armor tint).
            var veraPos = new Vector3(0f, 0f, 23.5f);
            var veraGo = InstantiateNpc(GeneratedCharFolder + "/VeraDusk.prefab", veraPos, "Vera");
            if (veraGo != null)
            {
                var veraNpc = veraGo.AddComponent<StoryNpc>();
                var vSo = new SerializedObject(veraNpc);
                vSo.FindProperty("displayName").stringValue = "Vera Dusk";
                vSo.FindProperty("remote").boolValue = false;
                vSo.ApplyModifiedPropertiesWithoutUndo();

                // Apply dark armor tint.
                var renderer = veraGo.GetComponentInChildren<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.25f, 0.22f));
            }

            // Transition box: "FOLLOW HER — THE CATWALKS".
            var duelBoxGo = BuildTransitionBox("ToCatwalkBox", new Vector3(0f, 1.2f, 23.8f), "FOLLOW HER — THE CATWALKS",
                out var duelBtn, out var duelTransition);
            var dtSo = new SerializedObject(duelTransition);
            dtSo.FindProperty("onFootScene").stringValue = Galaxy2Ep09SpireDuelSceneName;
            dtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(duelBtn.onClick,
                new UnityEngine.Events.UnityAction(duelTransition.LoadOnFootScene));
            duelBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue spire_docking (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Spire Docking";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = dockingDialogue;

            // Step 1: DefeatWaves — 3 Syndicate Runners.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Corridor Runners (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = runnerSpawner;

            // Step 2: Dialogue bounty_read.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Bounty Read";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = bountyDialogue;

            // Step 3: ReachTrigger — tavern entrance.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s3.FindPropertyRelative("label").stringValue = "ReachTrigger: Tavern Entrance";
            s3.FindPropertyRelative("reachPoint").objectReferenceValue = tavernReachGo.transform;
            s3.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 4: Dialogue tavern_murmur.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Tavern Murmur";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = tavernDialogue;

            // Step 5: Dialogue hunters_gambit (at Vera's table) then transition.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Follow to Catwalks";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = duelBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep09CharSpireScenePath);
            EnsureScenesInBuild(Galaxy2Ep09CharSpireScenePath, Galaxy2Ep09SpireDuelScenePath);

            Debug.Log($"[Space Samurai] EP09 Char Spire scene built at {Galaxy2Ep09CharSpireScenePath}. " +
                      "Layout: derelict mining station interior (docking bay → mag-locked corridor → tavern). " +
                      "Ash/rust palette, jury-rigged amber lighting. Rotating bounty hologram in docking bay. " +
                      "NPCs: Kessler + Mera (docking), Barkeep (tavern), 2 patrons with wandering. Vera Dusk seated at tavern rear. " +
                      "6 steps: spire_docking (auto) → defeat 3 corridor runners + barks → bounty_read → " +
                      "reach tavern → tavern_murmur → transition to Spire Duel.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP09 Spire Duel", priority = 111)]
        public static void BuildEp09SpireDuel()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Spire Duel: suspended catwalks over vacuum-adjacent shuttle bay — cold blue-white key light + ember accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.8f, 0.95f); // cold blue-white key
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f); // cold dim tones

            // Cold vacuum-adjacent atmosphere.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.40f, 0.50f);
            RenderSettings.fogDensity = 0.020f;

            // Cold accent lights + ember glows.
            BuildAccentPointLight("CatwalkLight1", new Vector3(-3f, 3f, 8f),
                new Color(0.5f, 0.7f, 0.95f), intensity: 1.2f, range: 12f);
            BuildAccentPointLight("CatwalkLight2", new Vector3(3f, 3f, 14f),
                new Color(0.55f, 0.65f, 0.90f), intensity: 1.1f, range: 11f);
            BuildAccentPointLight("EmberAccent", new Vector3(0f, 4f, 11f),
                new Color(1f, 0.4f, 0.2f), intensity: 0.8f, range: 8f);

            // ---- Catwalks over shuttle bay ----
            var cateGo = new GameObject("CatwalkArea");
            var cate = cateGo.transform;

            var metalGrey = new Color(0.30f, 0.32f, 0.35f);
            var darkBreach = new Color(0.22f, 0.18f, 0.16f);

            // Elevated catwalk platforms (x[-6,6], z[0,16]) with thin railings.
            BuildFloorCeiling(cate, "CatwalkFloor", new Vector3(0f, 3f, 8f), new Vector3(12f, 0f, 16f), metalGrey, metalGrey);

            // Railing props along sides (thin safety rails).
            BuildProp(cate, "RailingWest", new Vector3(-6.1f, 3.2f, 8f), new Vector3(0.15f, 0.4f, 16f), metalGrey);
            BuildProp(cate, "RailingEast", new Vector3(6.1f, 3.2f, 8f), new Vector3(0.15f, 0.4f, 16f), metalGrey);

            // Breach-scarred walls (hint of damage).
            BuildWall(cate, "BreachWall_W", new Vector3(-6.2f, 2f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(cate, "BreachWall_E", new Vector3(6.2f, 2f, 8f), new Vector3(0.2f, 3f, 16f));
            BuildWall(cate, "BreachWall_Back", new Vector3(0f, 2f, 16f), new Vector3(12f, 3f, 0.2f));

            // Deep dark floor plane far below (visual only, no collider).
            var dropFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dropFloor.name = "VoidFloor";
            Object.DestroyImmediate(dropFloor.GetComponent<Collider>());
            dropFloor.transform.SetParent(cate, false);
            dropFloor.transform.localPosition = new Vector3(0f, -15f, 8f);
            dropFloor.transform.localScale = new Vector3(20f, 1f, 20f);
            TintShared(dropFloor.GetComponent<Renderer>(), new Color(0.05f, 0.05f, 0.08f));

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
            // BuildSword wires the KatanaHolster (sword + hipAnchor) on the rig already; it returns
            // void, so re-find the sword root it created (named "Sword") for the DuelYield wiring.
            BuildSword(new Vector3(0f, 3f, 2f), weapon);
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- Dialogue Players ----
            // catwalk_duel auto-plays as the scene opens — the duel "opens without ceremony", so it
            // is NOT a blocking mission step (the duel Prompt step below is step 0 from the start).
            var duelDialogue = BuildEp09DialoguePlayer("Dialogue_CatwalkDuel", new Vector3(0f, 3.5f, 8f), "catwalk_duel");
            var duelDlgSo = new SerializedObject(duelDialogue);
            duelDlgSo.FindProperty("playOnStart").boolValue = true;
            duelDlgSo.ApplyModifiedPropertiesWithoutUndo();

            var maskDialogue = BuildEp09DialoguePlayer("Dialogue_MaskRemoved", new Vector3(0f, 3.5f, 10f), "mask_removed");

            var fractureDialogue = BuildEp09DialoguePlayer("Dialogue_TheFracture", new Vector3(0f, 3.5f, 11f), "the_fracture");

            var evidenceDialogue = BuildEp09DialoguePlayer("Dialogue_TheEvidence", new Vector3(0f, 3.5f, 12f), "the_evidence");

            // ---- VERA DUSK: Elite duel opponent ----
            // Create dedicated elite enemy definition for Vera.
            var veraEnemyDef = EnsureEp09VeraDefinition();

            var veraPos = new Vector3(0f, 3f, 5f);
            var vera = BuildDominionEnemy(veraPos, playerHealth, veraEnemyDef);
            var veraRenderer = vera.GetComponent<Renderer>();
            if (veraRenderer != null)
            {
                TintShared(veraRenderer, new Color(0.28f, 0.24f, 0.20f)); // dark elite tint
            }
            var veraMeleeAttacker = vera.GetComponent<MeleeAttacker>();
            if (veraMeleeAttacker != null)
            {
                var vmaSo = new SerializedObject(veraMeleeAttacker);
                vmaSo.FindProperty("nonLethalDisable").boolValue = true;
                vmaSo.ApplyModifiedPropertiesWithoutUndo();
            }
            // Vera is ACTIVE from scene start — the duel opens immediately. She must never go
            // through a wave spawner: the duel ends in a YIELD (DuelYield), not a death, so a
            // DefeatWaves step would soft-lock waiting for a kill that cannot happen.

            // Wire DuelYield component on Vera. (BuildDominionEnemy returns the Enemy component —
            // AddComponent lives on the GameObject.)
            var duelYield = vera.gameObject.AddComponent<DuelYield>();
            var dyeSo = new SerializedObject(duelYield);
            SetObjectRef(dyeSo, "opponent", vera.GetComponent<Health>());
            dyeSo.FindProperty("yieldThreshold").floatValue = 0.3f;
            SetObjectRefList(dyeSo, "disableOnYield", new List<Object> { veraMeleeAttacker });
            SetObjectRef(dyeSo, "sword", swordGrab);
            dyeSo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dyeSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire DuelYield.onYielded → maskDialogue.Play (show testimony).
            UnityEventTools.AddPersistentListener(duelYield.onYielded,
                new UnityEngine.Events.UnityAction(maskDialogue.Play));

            // ---- DOMINION BREACH WAVE: 4 Dominion Scouts after the duel ----
            var scoutColor = new Color(0.55f, 0.55f, 0.55f); // grey

            var scoutPositions = new Vector3[]
            {
                new Vector3(-2.5f, 3f, 12f),
                new Vector3(2.5f, 3f, 12.5f),
                new Vector3(-1.5f, 3f, 14f),
                new Vector3(1.5f, 3f, 14f)
            };
            var scoutHealths = new List<Health>();
            foreach (var pos in scoutPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, scoutColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                scoutHealths.Add(enemy.GetComponent<Health>());
            }

            var scoutSpawner = BuildEp03WaveSpawner("ScoutSpawner", new Vector3(0f, 3.5f, 13f), 2f,
                new List<List<Health>> { scoutHealths }, new[] { BuildEp09DialoguePlayer("Dialogue_DominionBreach", new Vector3(0f, 3.5f, 13f), "dominion_breach") });

            // Transition box: "INTO THE MEMORY".
            var memoryBoxGo = BuildTransitionBox("ToMemoryBox", new Vector3(0f, 3.2f, 15.5f), "INTO THE MEMORY",
                out var memoryBtn, out var memoryTransition);
            var mtSo = new SerializedObject(memoryTransition);
            mtSo.FindProperty("onFootScene").stringValue = Galaxy2Ep09Kethel7MemorySceneName;
            mtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(memoryBtn.onClick,
                new UnityEngine.Events.UnityAction(memoryTransition.LoadOnFootScene));
            memoryBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Prompt — THE DUEL. No prompt object: this step simply waits. Vera fights
            // from scene start (catwalk_duel barks auto-play); when she yields and the player
            // sheathes the sword, DuelYield.onAccepted calls AdvanceFromPrompt (wired below).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s0.FindPropertyRelative("label").stringValue = "Prompt: Vera Duel (yield + sheathe)";
            s0.FindPropertyRelative("promptObject").objectReferenceValue = null;

            // Step 1: Dialogue the_fracture (Vera's testimony after the yield is accepted).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: The Fracture";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = fractureDialogue;

            // Step 2: DefeatWaves — Dominion Scouts (4 breaching).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Breach (4 Scouts)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = scoutSpawner;

            // Step 3: Dialogue the_evidence.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The Evidence";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = evidenceDialogue;

            // Step 4: Prompt — enter memory.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Into the Memory";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = memoryBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The duel's acceptance advances the mission out of step 0.
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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep09SpireDuelScenePath);
            EnsureScenesInBuild(Galaxy2Ep09SpireDuelScenePath, Galaxy2Ep09Kethel7MemoryScenePath);

            Debug.Log($"[Space Samurai] EP09 Spire Duel scene built at {Galaxy2Ep09SpireDuelScenePath}. " +
                      "Layout: suspended catwalks over dark void shuttle bay. Cold blue-white lighting + ember accents. " +
                      "Vera Dusk elite opponent (120 HP) with DuelYield: yield at 30% plays mask_removed; " +
                      "sheathing the sword accepts (auto-accept 30s safety) and advances the mission. " +
                      "5 steps: duel Prompt (onAccepted → AdvanceFromPrompt) → the_fracture dialogue → " +
                      "defeat 4 Dominion Scouts (breach barks) → the_evidence dialogue → " +
                      "transition to Kethel-7 Memory.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 2/Build EP09 Kethel-7 Memory", priority = 112)]
        public static void BuildEp09Kethel7Memory()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Kethel-7 Memory: desaturated echo of orphanage night. Gray-blue tint, heartbeat ambience.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.60f, 0.70f); // cool desaturated key
            light.intensity = 0.65f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.26f, 0.30f); // dim cool tones

            // Memory-space fog treatment via MemoryFlashbackController (applied via component).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.35f, 0.37f, 0.42f);
            RenderSettings.fogDensity = 0.045f;

            // Subtle ember glows (child memory fragments, distant fire).
            BuildAccentPointLight("MemoryLight1", new Vector3(-2f, 2f, 5f),
                new Color(1f, 0.5f, 0.3f), intensity: 0.6f, range: 6f);
            BuildAccentPointLight("MemoryLight2", new Vector3(2f, 2f, 10f),
                new Color(1f, 0.45f, 0.25f), intensity: 0.5f, range: 5f);

            // ---- Kethel-7 Memory: simple corridor of rooms ----
            var memoryGo = new GameObject("Kethel7Memory");
            var memory = memoryGo.transform;

            var orphanGrey = new Color(0.42f, 0.44f, 0.48f);
            var darkGrey = new Color(0.28f, 0.30f, 0.34f);

            // Dorm room (x[-4,0], z[0,5]).
            BuildFloorCeiling(memory, "DormFloor", new Vector3(-2f, 0f, 2.5f), new Vector3(4f, 0f, 5f), orphanGrey, darkGrey);
            BuildWall(memory, "DormWall_W", new Vector3(-4f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 5f));
            BuildWall(memory, "DormWall_E", new Vector3(0f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 5f));
            BuildWall(memory, "DormWall_Back", new Vector3(-2f, 1.5f, 5f), new Vector3(4f, 3f, 0.2f));

            // Small bed prop in dorm.
            BuildProp(memory, "DormBed", new Vector3(-2f, 0.4f, 3f), new Vector3(1.5f, 0.8f, 2f), new Color(0.35f, 0.32f, 0.30f));

            // Hall with burning doorframe (x[0,4], z[5,12]).
            BuildFloorCeiling(memory, "HallFloor", new Vector3(2f, 0f, 8.5f), new Vector3(4f, 0f, 7f), orphanGrey, darkGrey);
            BuildWall(memory, "HallWall_W", new Vector3(0f, 1.5f, 8.5f), new Vector3(0.2f, 3f, 7f));
            BuildWall(memory, "HallWall_E", new Vector3(4f, 1.5f, 8.5f), new Vector3(0.2f, 3f, 7f));

            // Burning doorframe: dark geometry + unlit ember glows (no collider, decorative).
            var doorPos = new Vector3(2f, 1.5f, 12f);
            BuildProp(memory, "DoorframeLeft", new Vector3(doorPos.x - 0.8f, doorPos.y, doorPos.z), new Vector3(0.2f, 2.2f, 0.2f), new Color(0.20f, 0.15f, 0.12f));
            BuildProp(memory, "DoorframeRight", new Vector3(doorPos.x + 0.8f, doorPos.y, doorPos.z), new Vector3(0.2f, 2.2f, 0.2f), new Color(0.20f, 0.15f, 0.12f));
            var doorEmber = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorEmber.name = "FireGlow";
            Object.DestroyImmediate(doorEmber.GetComponent<Collider>());
            doorEmber.transform.SetParent(memory, false);
            doorEmber.transform.localPosition = doorPos;
            doorEmber.transform.localScale = new Vector3(1.6f, 2f, 0.3f);
            TintShared(doorEmber.GetComponent<Renderer>(), new Color(0.25f, 0.15f, 0.08f));
            doorEmber.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.35f, 0.15f));

            // End room (x[0,4], z[12,18]) — space beyond the doorframe.
            BuildFloorCeiling(memory, "EndFloor", new Vector3(2f, 0f, 15f), new Vector3(4f, 0f, 6f), orphanGrey, darkGrey);
            BuildWall(memory, "EndWall_W", new Vector3(0f, 1.5f, 15f), new Vector3(0.2f, 3f, 6f));
            BuildWall(memory, "EndWall_E", new Vector3(4f, 1.5f, 15f), new Vector3(0.2f, 3f, 6f));
            BuildWall(memory, "EndWall_Back", new Vector3(2f, 1.5f, 18f), new Vector3(4f, 3f, 0.2f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds (no locomotion override, but still add katana).
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 40f;
            BuildSword(new Vector3(0f, 0f, 0f), weapon);

            // ---- Memory Treatment ----
            var memoryTreatmentGo = new GameObject("Memory Treatment");
            var memoryCtrl = memoryTreatmentGo.AddComponent<MemoryFlashbackController>();
            // Defaults are fine; audio sourcing can fill heartbeatLoop later.

            // ---- Dialogue Players ----
            var descentDialogue = BuildEp09DialoguePlayer("Dialogue_MemoryDescent", new Vector3(-2f, 1.5f, 2.5f), "memory_descent");
            var descentDlgSo = new SerializedObject(descentDialogue);
            descentDlgSo.FindProperty("playOnStart").boolValue = true;
            descentDlgSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Three MemoryEchoVignette triggers with ghost echoes ----

            // Vignette 1: Two child-sized echoes running to wall gap near dorm entry.
            var vig1Go = new GameObject("Vignette1_ChildEchoes");
            vig1Go.transform.SetParent(memoryGo.transform, false);
            vig1Go.transform.position = new Vector3(-2f, 0f, 1.5f);
            var vig1Collider = vig1Go.AddComponent<BoxCollider>();
            vig1Collider.size = new Vector3(2f, 3f, 2f);
            vig1Collider.isTrigger = true;

            var vig1Root = new GameObject("EchoRoot");
            vig1Root.transform.SetParent(vig1Go.transform, false);
            vig1Root.transform.localPosition = Vector3.zero;

            // Two child echoes: scale prefabs to ~0.6.
            var echo1 = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc04.prefab", new Vector3(-1f, 0f, 0f), "ChildEcho1");
            if (echo1 != null)
            {
                echo1.transform.SetParent(vig1Root.transform, false);
                echo1.transform.localScale *= 0.6f;
                var meshRenderers = echo1.GetComponentsInChildren<Renderer>();
                foreach (var r in meshRenderers)
                    r.material = MemoryFlashbackController.MakeGhostMaterial();
            }

            var echo2 = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc05.prefab", new Vector3(0.5f, 0f, 0.2f), "ChildEcho2");
            if (echo2 != null)
            {
                echo2.transform.SetParent(vig1Root.transform, false);
                echo2.transform.localScale *= 0.6f;
                var meshRenderers = echo2.GetComponentsInChildren<Renderer>();
                foreach (var r in meshRenderers)
                    r.material = MemoryFlashbackController.MakeGhostMaterial();
            }

            var moveFromVig1 = new GameObject("MoveFrom");
            moveFromVig1.transform.SetParent(vig1Go.transform, false);
            moveFromVig1.transform.localPosition = new Vector3(-0.2f, 0f, 0.1f);

            var moveToVig1 = new GameObject("MoveTo");
            moveToVig1.transform.SetParent(vig1Go.transform, false);
            moveToVig1.transform.localPosition = new Vector3(1.5f, 0f, -0.5f);

            var vig1 = vig1Go.AddComponent<MemoryEchoVignette>();
            var vig1So = new SerializedObject(vig1);
            SetObjectRef(vig1So, "echoRoot", vig1Root);
            SetObjectRef(vig1So, "moveFrom", moveFromVig1.transform);
            SetObjectRef(vig1So, "moveTo", moveToVig1.transform);
            vig1So.FindProperty("duration").floatValue = 4f;
            SetObjectRef(vig1So, "dialogue", BuildEp09DialoguePlayer("Dialogue_MemoryEchoes", new Vector3(-2f, 1.5f, 1f), "memory_echoes"));
            vig1So.ApplyModifiedPropertiesWithoutUndo();

            // Vignette 2: Lone soldier echo frozen before burning doorframe (static, no movement).
            var vig2Go = new GameObject("Vignette2_SoldierEcho");
            vig2Go.transform.SetParent(memoryGo.transform, false);
            vig2Go.transform.position = new Vector3(2f, 0f, 11f);
            var vig2Collider = vig2Go.AddComponent<BoxCollider>();
            vig2Collider.size = new Vector3(2f, 3f, 2f);
            vig2Collider.isTrigger = true;

            var vig2Root = new GameObject("EchoRoot");
            vig2Root.transform.SetParent(vig2Go.transform, false);
            vig2Root.transform.localPosition = Vector3.zero;

            // Lone soldier: static frozen pose.
            var echoSoldier = InstantiateNpc(GeneratedCharFolder + "/AurelingCombat.prefab", Vector3.zero, "SoldierEcho");
            if (echoSoldier != null)
            {
                echoSoldier.transform.SetParent(vig2Root.transform, false);
                var meshRenderers = echoSoldier.GetComponentsInChildren<Renderer>();
                foreach (var r in meshRenderers)
                    r.material = MemoryFlashbackController.MakeGhostMaterial();
            }

            var vig2 = vig2Go.AddComponent<MemoryEchoVignette>();
            var vig2So = new SerializedObject(vig2);
            SetObjectRef(vig2So, "echoRoot", vig2Root);
            vig2So.FindProperty("duration").floatValue = 5f;
            SetObjectRef(vig2So, "dialogue", BuildEp09DialoguePlayer("Dialogue_MemoryRefusal", new Vector3(2f, 1.5f, 11f), "memory_refusal"));
            vig2So.ApplyModifiedPropertiesWithoutUndo();

            // Vignette 3: Kneeling echo in end room.
            var vig3Go = new GameObject("Vignette3_KneelingEcho");
            vig3Go.transform.SetParent(memoryGo.transform, false);
            vig3Go.transform.position = new Vector3(2f, 0f, 16f);
            var vig3Collider = vig3Go.AddComponent<BoxCollider>();
            vig3Collider.size = new Vector3(2f, 3f, 2f);
            vig3Collider.isTrigger = true;

            var vig3Root = new GameObject("EchoRoot");
            vig3Root.transform.SetParent(vig3Go.transform, false);
            vig3Root.transform.localPosition = Vector3.zero;

            // Kneeling echo.
            var echoKneel = InstantiateNpc(GeneratedCharFolder + "/AurelingNpc06.prefab", Vector3.zero, "KneelingEcho");
            if (echoKneel != null)
            {
                echoKneel.transform.SetParent(vig3Root.transform, false);
                var meshRenderers = echoKneel.GetComponentsInChildren<Renderer>();
                foreach (var r in meshRenderers)
                    r.material = MemoryFlashbackController.MakeGhostMaterial();
            }

            var vig3 = vig3Go.AddComponent<MemoryEchoVignette>();
            var vig3So = new SerializedObject(vig3);
            SetObjectRef(vig3So, "echoRoot", vig3Root);
            vig3So.FindProperty("duration").floatValue = 4.5f;
            SetObjectRef(vig3So, "dialogue", BuildEp09DialoguePlayer("Dialogue_KiraName", new Vector3(2f, 1.5f, 16f), "kira_name"));
            vig3So.ApplyModifiedPropertiesWithoutUndo();

            // Reach triggers through vignette rooms.
            var vig1ReachGo = new GameObject("Vig1ReachPoint");
            vig1ReachGo.transform.position = new Vector3(-2f, 0.5f, 2.5f);

            var vig2ReachGo = new GameObject("Vig2ReachPoint");
            vig2ReachGo.transform.position = new Vector3(2f, 0.5f, 8.5f);

            var vig3ReachGo = new GameObject("Vig3ReachPoint");
            vig3ReachGo.transform.position = new Vector3(2f, 0.5f, 15f);

            // Transition box: "WAKE — MAKE FOR CINDERS".
            var cindersBoxGo = BuildTransitionBox("ToCindersBox", new Vector3(2f, 1.2f, 17.5f), "WAKE — MAKE FOR CINDERS",
                out var cindersBtn, out var cindersTransition);
            var ctSo = new SerializedObject(cindersTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy2Ep09CindersRefinerySceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(cindersBtn.onClick,
                new UnityEngine.Events.UnityAction(cindersTransition.LoadOnFootScene));
            cindersBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue memory_descent (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Memory Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = descentDialogue;

            // Step 1: ReachTrigger — vignette 1 (child echoes).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Vignette 1 (Child Echoes)";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = vig1ReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 2f;

            // Step 2: ReachTrigger — vignette 2 (soldier/doorframe).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Vignette 2 (Soldier Echo)";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = vig2ReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 2f;

            // Step 3: ReachTrigger — vignette 3 (kneeling/kira name).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s3.FindPropertyRelative("label").stringValue = "ReachTrigger: Vignette 3 (Kneeling Echo / Kira Name)";
            s3.FindPropertyRelative("reachPoint").objectReferenceValue = vig3ReachGo.transform;
            s3.FindPropertyRelative("reachRadius").floatValue = 2f;

            // Step 4: Prompt — wake and escape to Cinders.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Wake — Make for Cinders";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = cindersBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy2Ep09Kethel7MemoryScenePath);
            EnsureScenesInBuild(Galaxy2Ep09Kethel7MemoryScenePath, Galaxy2Ep09CindersRefineryScenePath);

            Debug.Log($"[Space Samurai] EP09 Kethel-7 Memory scene built at {Galaxy2Ep09Kethel7MemoryScenePath}. " +
                      "Layout: memory-space corridor of rooms (dorm + hall with burning doorframe + end room). " +
                      "Desaturated gray-blue palette with ember glows, ExponentialSquared fog. MemoryFlashbackController applied. " +
                      "3 MemoryEchoVignette triggers with ghost echoes: dorm (2 children running), hall (soldier frozen), end (kneeling). " +
                      "5 steps: memory_descent (auto) → reach vig1 → reach vig2 → reach vig3 → " +
                      "transition to Cinders Refinery.");
        }

        /// <summary>Elite EnemyDefinition for EP09 Vera Dusk duel (~120 HP).</summary>
        private static EnemyDefinition EnsureEp09VeraDefinition()
        {
            const string path = DataFolder + "/Ep09Vera.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 120f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }
    }
}

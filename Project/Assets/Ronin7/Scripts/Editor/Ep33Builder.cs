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
    /// EP33 "The Tenfold Pact" (Galaxy 4 SERIES FINALE) scene builders for six of the eight on-foot scenes.
    /// Soren gathers the ten syndicate heads at neutral ground, reveals the Obsidian Synod as the Dominion's
    /// true architect (First Overseer Maelgorn), and leads a coordinated assault on three fronts: the data-spine,
    /// the creche halls (rescue), and the Beacon (shutdown). Samurai-4 yields her leash in the Beacon chamber.
    /// Khall sacrifices himself destroying the network from within. Maelgorn and the Synod fall to the united ten.
    /// At the Lantern table where it all began, the ten swear the Tenfold Pact: to spend their remaining lives
    /// keeping instead of taking.
    ///
    /// This file builds 6 of 8 scenes (on-foot):
    /// 1. The Gathering — ten allies + Dominion strike-team (2 waves)
    /// 2. The Map — war-room briefing (no combat)
    /// 3. (SPACE) The Assault — defined in Ep33BuilderFinale.cs
    /// 4. The Creche Halls — Dr. Heris + Resh rescue sequence (2 waves)
    /// 5. The Lit Beacon — Samurai-4 leash-break duel (LeashBreakController)
    /// 6. The Throne — the ten vs. Maelgorn + Wardens (2 waves)
    /// 7. (SPACE) The Exodus — defined in Ep33BuilderFinale.cs
    /// 8. The Tenfold Pact — series finale cutscene hook + return to stars
    ///
    /// The two SPACE scenes are built in Ep33BuilderFinale.cs but share the scene path constants and
    /// helper methods declared here.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 8 scenes: 6 on-foot here + 2 space in Ep33BuilderFinale.cs.
        private const string Galaxy4Ep33TheGatheringScenePath   = SceneFolder + "/Galaxy4_EP33_TheGathering.unity";
        private const string Galaxy4Ep33TheMapScenePath         = SceneFolder + "/Galaxy4_EP33_TheMap.unity";
        private const string Galaxy4Ep33TheAssaultScenePath     = SceneFolder + "/Galaxy4_EP33_TheAssault.unity";
        private const string Galaxy4Ep33CrecheHallsScenePath    = SceneFolder + "/Galaxy4_EP33_CrecheHalls.unity";
        private const string Galaxy4Ep33TheLitBeaconScenePath   = SceneFolder + "/Galaxy4_EP33_TheLitBeacon.unity";
        private const string Galaxy4Ep33TheThroneScenePath      = SceneFolder + "/Galaxy4_EP33_TheThrone.unity";
        private const string Galaxy4Ep33TheExodusScenePath      = SceneFolder + "/Galaxy4_EP33_TheExodus.unity";
        private const string Galaxy4Ep33TheTenfoldPactScenePath = SceneFolder + "/Galaxy4_EP33_TheTenfoldPact.unity";

        private static readonly string Galaxy4Ep33TheGatheringSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheGatheringScenePath);
        private static readonly string Galaxy4Ep33TheMapSceneName         = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheMapScenePath);
        private static readonly string Galaxy4Ep33TheAssaultSceneName     = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheAssaultScenePath);
        private static readonly string Galaxy4Ep33CrecheHallsSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33CrecheHallsScenePath);
        private static readonly string Galaxy4Ep33TheLitBeaconSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheLitBeaconScenePath);
        private static readonly string Galaxy4Ep33TheThroneSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheThroneScenePath);
        private static readonly string Galaxy4Ep33TheExodusSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheExodusScenePath);
        private static readonly string Galaxy4Ep33TheTenfoldPactSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep33TheTenfoldPactScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP33 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep33" and loads lines from Ep33Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp33DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep33Lines.Get(setId), advanceRef, setId, clipPrefix: "ep33");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Gathering", priority = 333)]
        public static void BuildEp33TheGathering()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Gathering: warm salvage-hulk palette, the Lantern table chamber.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.45f, 0.42f, 0.38f),       // warm grey
                ambient: new Color(0.06f, 0.055f, 0.05f),       // warm dim
                fogColor: new Color(0.10f, 0.09f, 0.07f), fogDensity: 0.011f,
                structureName: "Lantern",
                accent1: new Color(0.52f, 0.45f, 0.32f),        // amber
                accent2: new Color(0.48f, 0.42f, 0.30f),        // warm accent
                floorLight: new Color(0.42f, 0.38f, 0.34f), floorDark: new Color(0.14f, 0.12f, 0.10f),
                propTint: new Color(0.38f, 0.34f, 0.30f), out _);

            // ---- THE TEN ALLIES (positioned in semicircle behind player, z ~2..5) ----
            var allyPositions = new (string name, Vector3 pos)[]
            {
                ("Mera Voss", new Vector3(-3f, 0f, 2.5f)),
                ("Morrigan", new Vector3(-2f, 0f, 3f)),
                ("Captain Resh", new Vector3(-1f, 0f, 3.5f)),
                ("Dr. Heris", new Vector3(0f, 0f, 4f)),
                ("Sallow", new Vector3(1f, 0f, 3.5f)),
                ("Gryph", new Vector3(2f, 0f, 3f)),
                ("Sable Dross", new Vector3(3f, 0f, 2.5f)),
                ("Cassie-04", new Vector3(-2.5f, 0f, 2f)),
                ("Vess", new Vector3(0f, 0f, 2f)),
                ("Coral Vex", new Vector3(2.5f, 0f, 2f)),
            };

            foreach (var (name, pos) in allyPositions)
            {
                var allyGo = BuildEp32Npc(name, pos, new Color(0.50f, 0.48f, 0.46f)); // muted tint
                // The ten fight as one against the strike-team (canon beat 2): AllyCombatant, NO Health.
                var allyCombatant = allyGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue: gathering table (playOnStart) ----
            var gatheringTableDialogue = BuildEp33DialoguePlayer("Dialogue_GatheringTable", new Vector3(0f, 1.5f, 2f), "gathering_table");
            var gtSo = new SerializedObject(gatheringTableDialogue);
            gtSo.FindProperty("playOnStart").boolValue = true;
            gtSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: gathering fight ----
            var gatheringFightDialogue = BuildEp33DialoguePlayer("Dialogue_GatheringFight", new Vector3(0f, 1.5f, 12f), "gathering_fight");

            // ---- Dominion strike-team enemies (dark Dominion tint, 2 waves of 4 = 8 total) ----
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

            var strikeTeamSpawner = BuildWaveSpawner("StrikeTeamSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new DialoguePlayer[0]);

            // ---- Dialogue: gathering pact ----
            var gatheringPactDialogue = BuildEp33DialoguePlayer("Dialogue_GatheringPact", new Vector3(0f, 1.5f, 15f), "gathering_pact");

            // ---- Transition box: "TO THE WAR ROOM" ----
            var warRoomBoxGo = BuildTransitionBox("ToWarRoomBox", new Vector3(0f, 1.2f, 21.5f), "TO THE WAR ROOM",
                out var warRoomBtn, out var warRoomTransition);
            var wrSo = new SerializedObject(warRoomTransition);
            wrSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheMapSceneName;
            wrSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(warRoomBtn.onClick,
                new UnityEngine.Events.UnityAction(warRoomTransition.LoadOnFootScene));
            warRoomBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Gathering Table (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = gatheringTableDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Gathering Fight";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = gatheringFightDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Strike-Team (Wave 1: 4 + Wave 2: 4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = strikeTeamSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Gathering Pact";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = gatheringPactDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: To The War Room";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = warRoomBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep33TheGatheringScenePath, Galaxy4Ep33TheMapScenePath);

            Debug.Log($"[Space Samurai] EP33 The Gathering scene built at {Galaxy4Ep33TheGatheringScenePath}. " +
                      "Warm salvage-hulk Lantern chamber. Ten allies present (Mera Voss, Morrigan, Captain Resh, Dr. Heris, Sallow, Gryph, Sable Dross, Cassie-04, Vess, Coral Vex). " +
                      "8 Dominion Strike-Team enemies (dark Dominion tint, 2 waves of 4). " +
                      "5 steps: gathering_table (auto, Soren reveals the Synod) → gathering_fight → defeat 2 waves → gathering_pact (Pact sworn) → TO THE WAR ROOM.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Map", priority = 334)]
        public static void BuildEp33TheMap()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Map: cool steel war-room palette.
            BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.42f, 0.45f, 0.48f),       // cool steel
                ambient: new Color(0.08f, 0.08f, 0.10f),        // cool dim
                fogColor: new Color(0.12f, 0.13f, 0.15f), fogDensity: 0.010f,
                structureName: "WarRoom",
                accent1: new Color(0.30f, 0.36f, 0.42f),        // cool blue accent
                accent2: new Color(0.35f, 0.40f, 0.46f),        // cool slate
                floorLight: new Color(0.40f, 0.43f, 0.47f), floorDark: new Color(0.12f, 0.14f, 0.17f),
                propTint: new Color(0.32f, 0.36f, 0.41f), out _);

            // ---- Presence NPCs ----
            BuildEp32Npc("Coral Vex", new Vector3(-2f, 0f, 3f), new Color(0.50f, 0.48f, 0.46f));
            BuildEp32Npc("Morrigan", new Vector3(2f, 0f, 3f), new Color(0.50f, 0.48f, 0.46f));

            // ---- Dialogue: map briefing (playOnStart) ----
            var mapBriefingDialogue = BuildEp33DialoguePlayer("Dialogue_MapBriefing", new Vector3(0f, 1.5f, 2f), "map_briefing");
            var mbSo = new SerializedObject(mapBriefingDialogue);
            mbSo.FindProperty("playOnStart").boolValue = true;
            mbSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Transition box: "LAUNCH — ASSAULT THE SYNOD" ----
            var assaultBoxGo = BuildTransitionBox("ToAssaultBox", new Vector3(0f, 1.2f, 21.5f), "LAUNCH — ASSAULT THE SYNOD",
                out var assaultBtn, out var assaultTransition);
            var aSo = new SerializedObject(assaultTransition);
            aSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheAssaultSceneName;
            aSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(assaultBtn.onClick,
                new UnityEngine.Events.UnityAction(assaultTransition.LoadOnFootScene));
            assaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Map Briefing (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = mapBriefingDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Launch — Assault The Synod";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = assaultBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep33TheMapScenePath, Galaxy4Ep33TheAssaultScenePath);

            Debug.Log($"[Space Samurai] EP33 The Map scene built at {Galaxy4Ep33TheMapScenePath}. " +
                      "Cool steel war-room (cool steel + cool blue + cool slate). Coral Vex + Morrigan presence. " +
                      "2 steps: map_briefing (auto, three-front assault plan) → LAUNCH — ASSAULT THE SYNOD.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 Creche Halls", priority = 336)]
        public static void BuildEp33CrecheHalls()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Creche Halls: black-iron fortress corridor, cold obsidian palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.38f, 0.40f, 0.43f),       // cold dark steel
                ambient: new Color(0.04f, 0.04f, 0.05f),        // very dark
                fogColor: new Color(0.07f, 0.07f, 0.08f), fogDensity: 0.011f,
                structureName: "CrecheHalls",
                accent1: new Color(0.22f, 0.25f, 0.30f),        // obsidian dark
                accent2: new Color(0.28f, 0.31f, 0.36f),        // iron slate
                floorLight: new Color(0.32f, 0.35f, 0.39f), floorDark: new Color(0.08f, 0.08f, 0.10f),
                propTint: new Color(0.20f, 0.22f, 0.26f), out _);

            // ---- Ally NPCs with AllyCombatant ----
            var crecheReshGo = BuildEp32Npc("Captain Resh", new Vector3(-2f, 0f, 3f), new Color(0.50f, 0.48f, 0.46f));
            var reshAlly = crecheReshGo.AddComponent<AllyCombatant>();
            var reshSo = new SerializedObject(reshAlly);
            reshSo.ApplyModifiedPropertiesWithoutUndo();

            var crecheHerisGo = BuildEp32Npc("Dr. Heris", new Vector3(2f, 0f, 3f), new Color(0.50f, 0.48f, 0.46f));
            var herisAlly = crecheHerisGo.AddComponent<AllyCombatant>();
            var herisSo = new SerializedObject(herisAlly);
            herisSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: creche divide (playOnStart) ----
            var crecheDivideDialogue = BuildEp33DialoguePlayer("Dialogue_CrecheDivide", new Vector3(0f, 1.5f, 2f), "creche_divide");
            var cdSo = new SerializedObject(crecheDivideDialogue);
            cdSo.FindProperty("playOnStart").boolValue = true;
            cdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: creche combat ----
            var crecheCombatDialogue = BuildEp33DialoguePlayer("Dialogue_CrecheCombat", new Vector3(0f, 1.5f, 12f), "creche_combat");

            // ---- Obsidian trooper enemies (dark obsidian tint, 2 waves of 4 = 8 total) ----
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
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark obsidian
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
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark obsidian
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var obsidianSpawner = BuildWaveSpawner("ObsidianSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { wave1Healths, wave2Healths },
                new DialoguePlayer[0]);

            // ---- Dialogue: creche rescue ----
            var crecheRescueDialogue = BuildEp33DialoguePlayer("Dialogue_CrecheRescue", new Vector3(0f, 1.5f, 15f), "creche_rescue");

            // ---- Transition box: "TO THE BEACON" ----
            var beaconBoxGo = BuildTransitionBox("ToBeaconBox", new Vector3(0f, 1.2f, 21.5f), "TO THE BEACON",
                out var beaconBtn, out var beaconTransition);
            var bbSo = new SerializedObject(beaconTransition);
            bbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheLitBeaconSceneName;
            bbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(beaconBtn.onClick,
                new UnityEngine.Events.UnityAction(beaconTransition.LoadOnFootScene));
            beaconBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Creche Divide (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = crecheDivideDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Creche Combat";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = crecheCombatDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Obsidian Troopers (Wave 1: 4 + Wave 2: 4)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = obsidianSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Creche Rescue";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = crecheRescueDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: To The Beacon";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = beaconBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep33CrecheHallsScenePath, Galaxy4Ep33TheLitBeaconScenePath);

            Debug.Log($"[Space Samurai] EP33 Creche Halls scene built at {Galaxy4Ep33CrecheHallsScenePath}. " +
                      "Black-iron fortress corridor (cold obsidian palette). Captain Resh + Dr. Heris (AllyCombatant). " +
                      "8 Obsidian Trooper enemies (dark obsidian tint, 2 waves of 4). " +
                      "5 steps: creche_divide (auto, task assignment) → creche_combat → defeat 2 waves → creche_rescue (children freed) → TO THE BEACON.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Lit Beacon", priority = 337)]
        public static void BuildEp33TheLitBeacon()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Lit Beacon: Beacon chamber, cold blue palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.42f, 0.45f, 0.50f),       // cold blue-steel
                ambient: new Color(0.08f, 0.09f, 0.12f),        // cool dim
                fogColor: new Color(0.12f, 0.14f, 0.18f), fogDensity: 0.010f,
                structureName: "BeaconChamber",
                accent1: new Color(0.34f, 0.41f, 0.53f),        // cool blue accent
                accent2: new Color(0.30f, 0.36f, 0.45f),        // cool slate
                floorLight: new Color(0.40f, 0.43f, 0.49f), floorDark: new Color(0.12f, 0.14f, 0.18f),
                propTint: new Color(0.35f, 0.40f, 0.48f), out _);

            // ---- Samurai-4 duel opponent (steel tint, nonLethal, LeashBreakController) ----
            var samurai4Tint = new Color(0.40f, 0.42f, 0.50f);
            var samurai4 = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var samurai4Go = samurai4.gameObject;
            samurai4Go.name = "Samurai-4";
            var samurai4Renderer = samurai4.GetComponent<Renderer>();
            if (samurai4Renderer != null) TintShared(samurai4Renderer, samurai4Tint);
            var samurai4Melee = samurai4.GetComponent<MeleeAttacker>();
            if (samurai4Melee != null)
            {
                var maSo = new SerializedObject(samurai4Melee);
                maSo.FindProperty("nonLethalDisable").boolValue = true;
                maSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Add LeashBreakController to Samurai-4.
            var leashBreak = samurai4Go.AddComponent<LeashBreakController>();
            var lbSo = new SerializedObject(leashBreak);
            lbSo.FindProperty("startingConviction").floatValue = 1f;
            lbSo.FindProperty("breakThreshold").floatValue = 0.25f;
            lbSo.FindProperty("convictionDrainPerSecond").floatValue = 0.08f;
            lbSo.FindProperty("evidenceDrainAmount").floatValue = 0.4f;
            lbSo.FindProperty("autoAdvance").boolValue = true;
            lbSo.ApplyModifiedPropertiesWithoutUndo();

            samurai4Go.SetActive(false);

            // ---- Dialogue: beacon lit (playOnStart) ----
            var beaconLitDialogue = BuildEp33DialoguePlayer("Dialogue_BeaconLit", new Vector3(0f, 1.5f, 2f), "beacon_lit");
            var blSo = new SerializedObject(beaconLitDialogue);
            blSo.FindProperty("playOnStart").boolValue = true;
            blSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: beacon duel ----
            var beaconDuelDialogue = BuildEp33DialoguePlayer("Dialogue_BeaconDuel", new Vector3(0f, 1.5f, 12f), "beacon_duel");

            // ---- Dialogue: beacon freed ----
            var beaconFreedDialogue = BuildEp33DialoguePlayer("Dialogue_BeaconFreed", new Vector3(0f, 1.5f, 15f), "beacon_freed");

            // ---- Dialogue: khall death ----
            var khallDeathDialogue = BuildEp33DialoguePlayer("Dialogue_KhallDeath", new Vector3(0f, 1.5f, 18f), "khall_death");

            // ---- Transition box: "TO THE THRONE" ----
            var throneBoxGo = BuildTransitionBox("ToThroneBox", new Vector3(0f, 1.2f, 21.5f), "TO THE THRONE",
                out var throneBtn, out var throneTransition);
            var tbSo = new SerializedObject(throneTransition);
            tbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheThroneSceneName;
            tbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(throneBtn.onClick,
                new UnityEngine.Events.UnityAction(throneTransition.LoadOnFootScene));
            throneBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Beacon Lit (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = beaconLitDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Beacon Duel";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = beaconDuelDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Activate Samurai-4 (starts LeashBreakController drain)";
            var triggerProp = s2.FindPropertyRelative("triggerObjects");
            triggerProp.arraySize = 1;
            triggerProp.GetArrayElementAtIndex(0).objectReferenceValue = samurai4Go;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: LeashBreak Samurai-4 (onLeashBreak advances)";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = null;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Beacon Freed";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = beaconFreedDialogue;

            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Khall Death";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = khallDeathDialogue;

            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: To The Throne";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = throneBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire LeashBreakController.onLeashBreak → AdvanceFromPrompt (s3 null Prompt).
            UnityEventTools.AddPersistentListener(leashBreak.onLeashBreak,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            FinishEp32Scene(scene, Galaxy4Ep33TheLitBeaconScenePath, Galaxy4Ep33TheThroneScenePath);

            Debug.Log($"[Space Samurai] EP33 The Lit Beacon scene built at {Galaxy4Ep33TheLitBeaconScenePath}. " +
                      "Beacon chamber (cold blue-steel palette). " +
                      "Samurai-4 opponent (steel tint, nonLethal) with LeashBreakController (conviction drain: 0.08/s passive, 0.4 per evidence read). " +
                      "7 steps: beacon_lit (auto, Maelgorn's threat) → beacon_duel → Trigger(Samurai-4) → Prompt(null, leash break) → beacon_freed → khall_death (sacrifice) → TO THE THRONE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Throne", priority = 338)]
        public static void BuildEp33TheThrone()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Throne: cathedral throne hall, deep obsidian palette.
            var playerHealth = BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.35f, 0.37f, 0.40f),       // cold dark steel
                ambient: new Color(0.03f, 0.03f, 0.04f),        // very deep dark
                fogColor: new Color(0.06f, 0.06f, 0.07f), fogDensity: 0.012f,
                structureName: "ThroneHall",
                accent1: new Color(0.20f, 0.22f, 0.26f),        // deep obsidian
                accent2: new Color(0.25f, 0.27f, 0.32f),        // dark iron
                floorLight: new Color(0.30f, 0.32f, 0.37f), floorDark: new Color(0.07f, 0.07f, 0.09f),
                propTint: new Color(0.18f, 0.20f, 0.24f), out _);

            // ---- THE TEN ALLIES (AllyCombatant) ----
            var allyPositions = new (string name, Vector3 pos)[]
            {
                ("Mera Voss", new Vector3(-3f, 0f, 1.5f)),
                ("Morrigan", new Vector3(-2f, 0f, 2f)),
                ("Captain Resh", new Vector3(-1f, 0f, 2.5f)),
                ("Dr. Heris", new Vector3(0f, 0f, 3f)),
                ("Sallow", new Vector3(1f, 0f, 2.5f)),
                ("Gryph", new Vector3(2f, 0f, 2f)),
                ("Sable Dross", new Vector3(3f, 0f, 1.5f)),
                ("Cassie-04", new Vector3(-2.5f, 0f, 1f)),
                ("Vess", new Vector3(0f, 0f, 1f)),
                ("Coral Vex", new Vector3(2.5f, 0f, 1f)),
            };

            foreach (var (name, pos) in allyPositions)
            {
                var allyGo = BuildEp32Npc(name, pos, new Color(0.50f, 0.48f, 0.46f));
                var allyCombatant = allyGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue: throne confront (playOnStart) ----
            var throneConfrontDialogue = BuildEp33DialoguePlayer("Dialogue_ThroneConfront", new Vector3(0f, 1.5f, 2f), "throne_confront");
            var tcSo = new SerializedObject(throneConfrontDialogue);
            tcSo.FindProperty("playOnStart").boolValue = true;
            tcSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: throne fight ----
            var throneFightDialogue = BuildEp33DialoguePlayer("Dialogue_ThroneFight", new Vector3(0f, 1.5f, 12f), "throne_fight");

            // ---- Warden enemies (dark obsidian tint, Wave 1: 4) ----
            var wardenPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 8f),
                new Vector3(2f, 0f, 8.5f),
                new Vector3(-1f, 0f, 10f),
                new Vector3(1f, 0f, 9.5f),
            };
            var wardenHealths = new List<Health>();
            foreach (var pos in wardenPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.28f, 0.30f, 0.38f)); // dark obsidian
                enemy.gameObject.SetActive(false);
                wardenHealths.Add(enemy.GetComponent<Health>());
            }

            // ---- Maelgorn boss (Wave 2: 1) ----
            var maelgornTint = new Color(0.15f, 0.12f, 0.18f);
            var maelgorn = BuildDominionEnemy(new Vector3(0f, 0f, 13f), playerHealth, enemyDef);
            maelgorn.gameObject.name = "Maelgorn";
            var maelgornRenderer = maelgorn.GetComponent<Renderer>();
            if (maelgornRenderer != null) TintShared(maelgornRenderer, maelgornTint);

            // Boost Maelgorn's health.
            var maelgornHealth = maelgorn.GetComponent<Health>();
            var mhSo = new SerializedObject(maelgornHealth);
            mhSo.FindProperty("maxHealth").floatValue = 240f;
            mhSo.ApplyModifiedPropertiesWithoutUndo();

            maelgorn.gameObject.SetActive(false);
            var maelgornHealthComponent = maelgorn.GetComponent<Health>();

            var maelgornList = new List<Health> { maelgornHealthComponent };

            var synodSpawner = BuildWaveSpawner("WardenSpawner", new Vector3(0f, 0.5f, 10f), 2f,
                new List<List<Health>> { wardenHealths, maelgornList },
                new DialoguePlayer[0]);

            // ---- Dialogue: throne fall ----
            var throneFallDialogue = BuildEp33DialoguePlayer("Dialogue_ThroneFall", new Vector3(0f, 1.5f, 15f), "throne_fall");

            // ---- Transition box: "TO THE DOCKS — ESCAPE" ----
            var exodusBoxGo = BuildTransitionBox("ToExodusBox", new Vector3(0f, 1.2f, 21.5f), "TO THE DOCKS — ESCAPE",
                out var exodusBtn, out var exodusTransition);
            var ebSo = new SerializedObject(exodusTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy4Ep33TheExodusSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(exodusBtn.onClick,
                new UnityEngine.Events.UnityAction(exodusTransition.LoadOnFootScene));
            exodusBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Throne Confront (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = throneConfrontDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Throne Fight";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = throneFightDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Wardens + Maelgorn (Wave 1: 4 Wardens + Wave 2: Maelgorn boss)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = synodSpawner;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Throne Fall";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = throneFallDialogue;

            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: To The Docks — Escape";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = exodusBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp32Scene(scene, Galaxy4Ep33TheThroneScenePath, Galaxy4Ep33TheExodusScenePath);

            Debug.Log($"[Space Samurai] EP33 The Throne scene built at {Galaxy4Ep33TheThroneScenePath}. " +
                      "Cathedral throne hall (deep obsidian palette). Ten allies (AllyCombatant). " +
                      "4 Warden enemies + Maelgorn boss (health: 240, dark obsidian tint). " +
                      "5 steps: throne_confront (auto, Maelgorn's defiance) → throne_fight → defeat wardens + Maelgorn → throne_fall (Synod toppled) → TO THE DOCKS — ESCAPE.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP33 The Tenfold Pact", priority = 340)]
        public static void BuildEp33TheTenfoldPact()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The Tenfold Pact: the Lantern table again, warm calm palette (epilogue tone).
            BuildEp32OnFootShell(refs, weapon,
                keyLight: new Color(0.45f, 0.42f, 0.38f),       // warm grey
                ambient: new Color(0.06f, 0.055f, 0.05f),       // warm dim
                fogColor: new Color(0.10f, 0.09f, 0.07f), fogDensity: 0.011f,
                structureName: "LanternEpilogue",
                accent1: new Color(0.52f, 0.45f, 0.32f),        // amber
                accent2: new Color(0.48f, 0.42f, 0.30f),        // warm accent
                floorLight: new Color(0.42f, 0.38f, 0.34f), floorDark: new Color(0.14f, 0.12f, 0.10f),
                propTint: new Color(0.38f, 0.34f, 0.30f), out _);

            // ---- THE TEN ALLIES (presence, no combat) ----
            var allyNames = new string[]
            {
                "Mera Voss", "Morrigan", "Captain Resh", "Dr. Heris", "Sallow",
                "Gryph", "Sable Dross", "Cassie-04", "Vess", "Coral Vex"
            };
            var pos = new Vector3(-3f, 0f, 2.5f);
            foreach (var allyName in allyNames)
            {
                BuildEp32Npc(allyName, pos, new Color(0.50f, 0.48f, 0.46f));
                pos.x += 0.6f; // Slight spread along x
            }

            // ---- Samurai-4 (optional epilogue presence) ----
            BuildEp32Npc("Samurai-4", new Vector3(0f, 0f, -1f), new Color(0.40f, 0.42f, 0.50f));

            // ---- Dialogue: pact vows (playOnStart) ----
            var pactVowsDialogue = BuildEp33DialoguePlayer("Dialogue_PactVows", new Vector3(0f, 1.5f, 2f), "pact_vows");
            var pvSo = new SerializedObject(pactVowsDialogue);
            pvSo.FindProperty("playOnStart").boolValue = true;
            pvSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue: epilogue ----
            var epilogueDialogue = BuildEp33DialoguePlayer("Dialogue_Epilogue", new Vector3(0f, 1.5f, 10f), "epilogue");

            // ---- CUTSCENE PLACEHOLDER ----
            var cutsceneAnchor = new GameObject("[ENDING CUTSCENE — TODO]");
            cutsceneAnchor.transform.position = new Vector3(0f, 1.5f, 3f);
            // CUTSCENE PLACEHOLDER: drop an ending Timeline/PlayableDirector on this anchor and trigger it from the MissionDirector "Ending cutscene" prompt step below.

            // ---- Finale return box (ACTIVE from start, no combat gate) ----
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 21.5f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false); // SetActive(false) until dialogue concludes

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var fsSo = new SerializedObject(finaleFlagSetter);
            var flagsProp = fsSo.FindProperty("flags");
            flagsProp.arraySize = 3;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ep33_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "galaxy4_complete";
            flagsProp.GetArrayElementAtIndex(2).stringValue = "series_complete";
            fsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Pact Vows (playOnStart)";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = pactVowsDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Epilogue";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = epilogueDialogue;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "TODO: Ending cutscene — insert Timeline here";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of EP33: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep33TheTenfoldPactScenePath);
            EnsureScenesInBuild(Galaxy4Ep33TheTenfoldPactScenePath);

            Debug.Log($"[Space Samurai] EP33 The Tenfold Pact scene built at {Galaxy4Ep33TheTenfoldPactScenePath}. " +
                      "Warm Lantern epilogue chamber. Ten allies present + Samurai-4. " +
                      "Cutscene anchor placeholder at (0, 1.5, 3). " +
                      "3 steps: pact_vows (auto, ten take their oaths) → epilogue (Soren + Samurai-4 final moment) → Prompt(return box, RETURN — TO THE STARS). " +
                      "Return box wired to CampaignFlagSetter (ep33_complete + galaxy4_complete + series_complete) + ReturnToSpace. " +
                      "GALAXY 4 EP33 SERIES FINALE COMPLETE (last scene).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP33 Scenes", priority = 345)]
        public static void BuildAllEp33Scenes()
        {
            BuildEp33TheGathering();
            BuildEp33TheMap();
            BuildEp33TheAssault();      // defined in Ep33BuilderFinale.cs
            BuildEp33CrecheHalls();
            BuildEp33TheLitBeacon();
            BuildEp33TheThrone();
            BuildEp33TheExodus();       // defined in Ep33BuilderFinale.cs
            BuildEp33TheTenfoldPact();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP33 scenes built + inputs rewired. GALAXY 4 EP33 — SERIES FINALE COMPLETE.");
        }
    }
}

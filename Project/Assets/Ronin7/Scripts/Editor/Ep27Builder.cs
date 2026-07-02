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
    /// EP27 "The Hollow Choir" (Galaxy 4 finale) scene builders for the first three on-foot scenes
    /// inside Leviathan-9, the petrified remains of a thought-oracle (living knowledge-keeper) that
    /// the Dominion murdered forty years ago with a Genesis Cannon. The Pale Choir has spent four
    /// decades preserving its residual neural tissue in the Archive. Cipher discovers his original
    /// purpose: he fired that cannon on Overseer Khall's order, and has been circling this wound
    /// since his memory wipe.
    /// - Bone Gates: cathedral-entrance sentries; Pale Choir cultists (pale ceremonial tint, nonLethal).
    /// - Wet Chambers: settlement sanctuary with Meren NPC; memory-flash caretakers (DreamPhantom, nonLethal).
    /// - Marrow Archive: thought-oracle sanctuary with Sallow NPC; Dominion acolytes (dark tint, lethal).
    ///
    /// The EP27 signature moment is the thought-fossil touch: the oracle reveals Cipher's original trigger-pull,
    /// and the deeper architecture — the ten syndicates of the fringe are a cage, each bar holding the others.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths (constants + derived readonly names). All 6 scenes declared here; scenes 4-6 built in Ep27BuilderFinale.cs.
        private const string Galaxy4Ep27BoneGatesScenePath      = SceneFolder + "/Galaxy4_EP27_BoneGates.unity";
        private const string Galaxy4Ep27WetChambersScenePath    = SceneFolder + "/Galaxy4_EP27_WetChambers.unity";
        private const string Galaxy4Ep27MarrowArchiveScenePath  = SceneFolder + "/Galaxy4_EP27_MarrowArchive.unity";
        private const string Galaxy4Ep27WeightOfAGodScenePath   = SceneFolder + "/Galaxy4_EP27_WeightOfAGod.unity";
        private const string Galaxy4Ep27HandlerArrivesScenePath = SceneFolder + "/Galaxy4_EP27_HandlerArrives.unity";
        private const string Galaxy4Ep27BoneFleetScenePath      = SceneFolder + "/Galaxy4_EP27_BoneFleet.unity";

        private static readonly string Galaxy4Ep27BoneGatesSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27BoneGatesScenePath);
        private static readonly string Galaxy4Ep27WetChambersSceneName    = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27WetChambersScenePath);
        private static readonly string Galaxy4Ep27MarrowArchiveSceneName  = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27MarrowArchiveScenePath);
        private static readonly string Galaxy4Ep27WeightOfAGodSceneName   = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27WeightOfAGodScenePath);
        private static readonly string Galaxy4Ep27HandlerArrivesSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27HandlerArrivesScenePath);
        private static readonly string Galaxy4Ep27BoneFleetSceneName      = System.IO.Path.GetFileNameWithoutExtension(Galaxy4Ep27BoneFleetScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP27 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep27" and loads lines from Ep27Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp27DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep27Lines.Get(setId), advanceRef, setId, clipPrefix: "ep27");
        }

        /// <summary>Standard EP27 on-foot scene scaffold shared by scenes 1-5: directional + 2 accent lights,
        /// fog, floor/walls/props, game root, player rig + sword + bounds, XR UI. Returns the player Health.</summary>
        private static Health BuildEp27OnFootShell(Object[] refs, WeaponDefinition weapon,
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
        private static GameObject BuildEp27Npc(string displayName, Vector3 position, Color tint)
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

        private static void FinishEp27Scene(UnityEngine.SceneManagement.Scene scene, string scenePath, string nextScenePath)
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

        // Pale Choir cultist tint: pale bone-white
        private static readonly Color Ep27PaleChoir = new Color(0.86f, 0.84f, 0.78f);
        // Fungal-bioluminescent tint: teal/green accent
        private static readonly Color Ep27FungalAccent = new Color(0.45f, 0.85f, 0.7f);
        // Dark Dominion acolyte tint
        private static readonly Color Ep27DarkDominion = new Color(0.3f, 0.32f, 0.4f);

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Bone Gates", priority = 287)]
        public static void BuildEp27BoneGates()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Bone Gates: pale cathedral-entrance with bone/fungal palette.
            var playerHealth = BuildEp27OnFootShell(refs, weapon,
                keyLight: new Color(0.72f, 0.70f, 0.68f),     // warm pale
                ambient: new Color(0.14f, 0.13f, 0.12f),      // soft dim
                fogColor: new Color(0.24f, 0.22f, 0.20f), fogDensity: 0.015f,
                structureName: "BoneGates",
                accent1: new Color(0.86f, 0.84f, 0.78f),      // pale bone-white
                accent2: new Color(0.45f, 0.85f, 0.7f),       // fungal teal/green
                floorLight: new Color(0.48f, 0.46f, 0.44f), floorDark: new Color(0.26f, 0.24f, 0.22f),
                propTint: new Color(0.42f, 0.40f, 0.38f), out _);

            // ---- Pale Choir cultists (nonLethal submission strikes) ----
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
                if (renderer != null) TintShared(renderer, Ep27PaleChoir);
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
                if (renderer != null) TintShared(renderer, Ep27PaleChoir);
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

            var cultistSpawner = BuildWaveSpawner("CultistSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { waveA, waveB },
                new[] { BuildEp27DialoguePlayer("Dialogue_GateChallenge", new Vector3(0f, 1.5f, 8f), "gate_challenge") });

            // ---- Dialogue Players ----
            var drekBriefingDialogue = BuildEp27DialoguePlayer("Dialogue_DrekBriefing", new Vector3(0f, 1.5f, 2f), "drek_briefing");
            var dbSo = new SerializedObject(drekBriefingDialogue);
            dbSo.FindProperty("playOnStart").boolValue = true;
            dbSo.ApplyModifiedPropertiesWithoutUndo();

            var dyingCultistDialogue = BuildEp27DialoguePlayer("Dialogue_DyingCultist", new Vector3(0f, 1.5f, 5f), "dying_cultist");

            // Transition box: "INTO THE BONE GATES".
            var wetChambersBoxGo = BuildTransitionBox("ToWetChambersBox", new Vector3(0f, 1.2f, 21.5f), "INTO THE BONE GATES",
                out var wetChambersBtn, out var wetChambersTransition);
            var wcSo = new SerializedObject(wetChambersTransition);
            wcSo.FindProperty("onFootScene").stringValue = Galaxy4Ep27WetChambersSceneName;
            wcSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(wetChambersBtn.onClick,
                new UnityEngine.Events.UnityAction(wetChambersTransition.LoadOnFootScene));
            wetChambersBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Drek Briefing";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = drekBriefingDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Pale Choir Cultists (5, nonLethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = cultistSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Dying Cultist";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = dyingCultistDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Into the Bone Gates";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = wetChambersBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp27Scene(scene, Galaxy4Ep27BoneGatesScenePath, Galaxy4Ep27WetChambersScenePath);

            Debug.Log($"[Space Samurai] EP27 Bone Gates scene built at {Galaxy4Ep27BoneGatesScenePath}. " +
                      "Cathedral-entrance pale bone-white tones with fungal accents. " +
                      "5 Pale Choir cultists (pale ceremonial, nonLethal submission strikes) in 2 waves. " +
                      "4 steps: drek_briefing (auto, Dominion briefing) → defeat 5 cultists (gate_challenge) → dying_cultist (interrogation) → into the bone gates.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Wet Chambers", priority = 288)]
        public static void BuildEp27WetChambers()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Wet Chambers: brighter fungal/bioluminescent palette, settlement sanctuary.
            var playerHealth = BuildEp27OnFootShell(refs, weapon,
                keyLight: new Color(0.75f, 0.72f, 0.70f),     // warm bright
                ambient: new Color(0.18f, 0.16f, 0.14f),      // visible
                fogColor: new Color(0.26f, 0.24f, 0.22f), fogDensity: 0.012f,
                structureName: "WetChambers",
                accent1: new Color(0.45f, 0.85f, 0.7f),       // fungal teal/green
                accent2: new Color(0.55f, 0.78f, 0.88f),      // bioluminescent blue
                floorLight: new Color(0.50f, 0.48f, 0.46f), floorDark: new Color(0.28f, 0.26f, 0.24f),
                propTint: new Color(0.44f, 0.42f, 0.40f), out _);

            // ---- Meren NPC ----
            var merenLayeredTint = new Color(0.52f, 0.50f, 0.48f); // practical, layered
            var merenGo = BuildEp27Npc("Meren", new Vector3(0f, 0f, 14f), merenLayeredTint);

            // ---- Memory-flash combat: Pale Choir caretakers with DreamPhantom ----
            var caretakerPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(-0.5f, 0f, 10f),
                new Vector3(0.5f, 0f, 10.5f),
            };
            var caretakerHealths = new List<Health>();
            foreach (var pos in caretakerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep27PaleChoir);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                // Add DreamPhantom component (mirrored from Ep21Builder: memory-flash enemies are killable, NOT illusory)
                enemy.gameObject.AddComponent<DreamPhantom>();
                caretakerHealths.Add(enemy.GetComponent<Health>());
            }

            var caretakerSpawner = BuildWaveSpawner("CaretakerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { caretakerHealths },
                new DialoguePlayer[0]); // No spawn dialogue for phantoms

            // ---- Dialogue Players ----
            var merenIntroDialogue = BuildEp27DialoguePlayer("Dialogue_MerenIntro", new Vector3(0f, 1.5f, 2f), "meren_intro");
            var miSo = new SerializedObject(merenIntroDialogue);
            miSo.FindProperty("playOnStart").boolValue = true;
            miSo.ApplyModifiedPropertiesWithoutUndo();

            var bloodiedElderDialogue = BuildEp27DialoguePlayer("Dialogue_BloodiedElder", new Vector3(0f, 1.5f, 12f), "bloodied_elder");

            // Transition box: "DEEPER — THE MARROW ARCHIVE".
            var archiveBoxGo = BuildTransitionBox("ToMarrowArchiveBox", new Vector3(0f, 1.2f, 21.5f), "DEEPER — THE MARROW ARCHIVE",
                out var archiveBtn, out var archiveTransition);
            var abSo = new SerializedObject(archiveTransition);
            abSo.FindProperty("onFootScene").stringValue = Galaxy4Ep27MarrowArchiveSceneName;
            abSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archiveBtn.onClick,
                new UnityEngine.Events.UnityAction(archiveTransition.LoadOnFootScene));
            archiveBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Meren Intro";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = merenIntroDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Phantom Caretakers (4, DreamPhantom, nonLethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = caretakerSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Bloodied Elder";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = bloodiedElderDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Deeper — The Marrow Archive";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = archiveBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp27Scene(scene, Galaxy4Ep27WetChambersScenePath, Galaxy4Ep27MarrowArchiveScenePath);

            Debug.Log($"[Space Samurai] EP27 Wet Chambers scene built at {Galaxy4Ep27WetChambersScenePath}. " +
                      "Settlement sanctuary with fungal/bioluminescent palette, bright greens and blues. " +
                      "Meren NPC (practical layered tint) + 4 Pale Choir caretaker phantoms (pale bone-white, DreamPhantom, nonLethal, killable memory-flash). " +
                      "4 steps: meren_intro (auto, settlement protection) → defeat 4 phantom caretakers → bloodied_elder (oracle recognition) → deeper into marrow archive.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP27 Marrow Archive", priority = 289)]
        public static void BuildEp27MarrowArchive()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Marrow Archive: spinal-column sanctuary palette, bioluminescent pulse.
            var playerHealth = BuildEp27OnFootShell(refs, weapon,
                keyLight: new Color(0.78f, 0.75f, 0.72f),     // pale luminous
                ambient: new Color(0.16f, 0.15f, 0.13f),      // soft glow
                fogColor: new Color(0.28f, 0.26f, 0.24f), fogDensity: 0.014f,
                structureName: "MarrowArchive",
                accent1: new Color(0.55f, 0.78f, 0.88f),      // bioluminescent blue
                accent2: new Color(0.45f, 0.85f, 0.7f),       // fungal teal/green
                floorLight: new Color(0.52f, 0.50f, 0.48f), floorDark: new Color(0.30f, 0.28f, 0.26f),
                propTint: new Color(0.46f, 0.44f, 0.42f), out _);

            // ---- Sallow NPC (pale luminous tint) ----
            var sallowPaleLuminousTint = new Color(0.8f, 0.82f, 0.85f); // pale luminous
            var sallowGo = BuildEp27Npc("Sallow", new Vector3(0f, 0f, 14f), sallowPaleLuminousTint);

            // ---- Acolyte enemies (lethal, dark Dominion tint) ----
            var acolytePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 8f),
                new Vector3(1.5f, 0f, 8.5f),
                new Vector3(-0.5f, 0f, 10.5f),
                new Vector3(0.5f, 0f, 11f),
            };
            var acolyteHealths = new List<Health>();
            foreach (var pos in acolytePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, Ep27DarkDominion);
                // Acolytes are lethal (no nonLethalDisable flag)
                enemy.gameObject.SetActive(false);
                acolyteHealths.Add(enemy.GetComponent<Health>());
            }

            var acolyteSpawner = BuildWaveSpawner("AcolyteSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { acolyteHealths },
                new DialoguePlayer[0]); // No spawn dialogue for acolytes

            // ---- Dialogue Players ----
            var sallowArchiveDialogue = BuildEp27DialoguePlayer("Dialogue_SallowArchive", new Vector3(0f, 1.5f, 2f), "sallow_archive");
            var saDialogueSo = new SerializedObject(sallowArchiveDialogue);
            saDialogueSo.FindProperty("playOnStart").boolValue = true;
            saDialogueSo.ApplyModifiedPropertiesWithoutUndo();

            var sallowInviteDialogue = BuildEp27DialoguePlayer("Dialogue_SallowInvite", new Vector3(0f, 1.5f, 12f), "sallow_invite");

            // Transition box: "TOUCH THE THOUGHT-FOSSIL".
            var weightBoxGo = BuildTransitionBox("ToWeightBox", new Vector3(0f, 1.2f, 21.5f), "TOUCH THE THOUGHT-FOSSIL",
                out var weightBtn, out var weightTransition);
            var wbSo = new SerializedObject(weightTransition);
            wbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep27WeightOfAGodSceneName;
            wbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(weightBtn.onClick,
                new UnityEngine.Events.UnityAction(weightTransition.LoadOnFootScene));
            weightBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Sallow Archive";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = sallowArchiveDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Acolytes (4, lethal)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = acolyteSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Sallow Invite";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = sallowInviteDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Touch the Thought-Fossil";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = weightBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp27Scene(scene, Galaxy4Ep27MarrowArchiveScenePath, Galaxy4Ep27WeightOfAGodScenePath);

            Debug.Log($"[Space Samurai] EP27 Marrow Archive scene built at {Galaxy4Ep27MarrowArchiveScenePath}. " +
                      "Thought-oracle sanctuary with pale luminous + bioluminescent pulse palette. " +
                      "Sallow NPC (pale luminous tint) + 4 Dominion acolytes (dark Dominion, lethal). " +
                      "4 steps: sallow_archive (auto, thought-oracle revelation) → defeat 4 acolytes → sallow_invite (fossil touch) → touch the thought-fossil.");
        }
    }
}

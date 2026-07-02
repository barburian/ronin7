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
    /// EP06 "Signal in the Dark" scene builders. Builds the frozen Frosthold colony where Ronin-7
    /// discovers his true identity as Cipher and confronts Khall's machinations. Wires all MissionDirector
    /// steps, enemy waves, and NPC interactions across Medical Compound, Landing Yard, and Gantry Hub scenes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep06MedicalCompoundScenePath = SceneFolder + "/Galaxy1_EP06_MedicalCompound.unity";
        private const string Galaxy1Ep06GantryHubScenePath = SceneFolder + "/Galaxy1_EP06_GantryHub.unity";

        private static readonly string Galaxy1Ep06MedicalCompoundSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06MedicalCompoundScenePath);
        private static readonly string Galaxy1Ep06GantryHubSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06GantryHubScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP06 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep06" and loads lines from Ep06Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp06DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep06Lines.Get(setId), advanceRef, setId, clipPrefix: "ep06");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Medical Compound", priority = 90)]
        public static void BuildEp06MedicalCompound()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Medical compound: cold blue-white palette, dense icy fog, industrial medical equipment.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.85f, 0.95f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.25f);

            // Frost-grey fog: dense icy atmosphere.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.3f, 0.35f, 0.42f);
            RenderSettings.fogDensity = 0.04f;

            // Medical compound accent lights (cool blue).
            BuildAccentPointLight("MedicalLight1", new Vector3(-3f, 2.6f, 8f),
                new Color(0.65f, 0.85f, 1f), intensity: 1.5f, range: 12f);
            BuildAccentPointLight("MedicalLight2", new Vector3(3f, 2.6f, 16f),
                new Color(0.6f, 0.8f, 1f), intensity: 1.4f, range: 12f);

            // ---- Medical compound: entry corridor (z 0-8) -> medical hall (z 8-22).
            var interiorGo = new GameObject("MedicalCompoundInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.28f);
            var ceilColor = new Color(0.1f, 0.12f, 0.16f);

            // Entry corridor: x[-3,3], z[0,8].
            BuildFloorCeiling(interior, "EntryCorridorMed", new Vector3(0f, 0f, 4f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildCorridorWall(interior, "EntryCorridorMed_WallW", -3f, 0f, 8f, new float[0], 2.4f);
            BuildCorridorWall(interior, "EntryCorridorMed_WallE", 3f, 0f, 8f, new float[0], 2.4f);
            BuildWall(interior, "EntryCorridorMed_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.2f));

            // Medical hall: x[-4,4], z[8,22] with medical console at far end.
            BuildFloorCeiling(interior, "MedicalHall", new Vector3(0f, 0f, 15f), new Vector3(8f, 0f, 14f), floorColor, ceilColor);
            BuildCorridorWall(interior, "MedicalHall_WallW", -4f, 8f, 22f, new float[0], 2.4f);
            BuildCorridorWall(interior, "MedicalHall_WallE", 4f, 8f, 22f, new float[0], 2.4f);

            // Medical console prop at far end.
            var consoleColor = new Color(0.25f, 0.28f, 0.32f);
            BuildProp(interior, "MedicalConsole", new Vector3(0f, 0.8f, 20f), new Vector3(1.5f, 1.4f, 0.6f), consoleColor);

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

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Dialogue Players ----
            var medicalBarksDialogue = BuildEp06DialoguePlayer("Dialogue_MedicalBarks", new Vector3(0f, 1.5f, 15f), "medical_barks");
            var khallHologramDialogue = BuildEp06DialoguePlayer("Dialogue_KhallHologram", new Vector3(0f, 1.5f, 20f), "khall_hologram", talkRef);

            // ---- Enemies: 6 Hollow Kings operatives (pale blue tint, 2 waves of 3) ----
            var hollowKingsBlue = new Color(0.7f, 0.8f, 0.9f);
            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 10f),
                new Vector3(2f, 0f, 12f),
                new Vector3(0f, 0f, 14f)
            };
            var wave2Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 16f),
                new Vector3(1.5f, 0f, 18f),
                new Vector3(0f, 0f, 20f)
            };

            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hollowKingsBlue);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1f, 12f), 3f,
                new List<List<Health>> { wave1Healths }, new[] { medicalBarksDialogue });

            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hollowKingsBlue);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 18f), 3f,
                new List<List<Health>> { wave2Healths }, new[] { medicalBarksDialogue });

            // Reach trigger near medical console.
            var consoleReachGo = new GameObject("ConsoleReachPoint");
            consoleReachGo.transform.position = new Vector3(0f, 1f, 20f);

            // Transition box: "TO THE CENTRAL HUB" (Landing Yard merged away — MedicalCompound is the colony
            // assault and now chains straight to the Gantry Hub).
            var yardBoxGo = BuildTransitionBox("ToCentralHubBox", new Vector3(0f, 1.2f, 21.5f), "TO THE CENTRAL HUB",
                out var yardBtn, out var yardTransition);
            var ytSo = new SerializedObject(yardTransition);
            ytSo.FindProperty("onFootScene").stringValue = Galaxy1Ep06GantryHubSceneName;
            ytSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(yardBtn.onClick,
                new UnityEngine.Events.UnityAction(yardTransition.LoadOnFootScene));
            yardBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: DefeatWaves — Wave 1 (3 Hollow Kings).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s0.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1 (3 Hollow Kings)";
            s0.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 1: DefeatWaves — Wave 2 (3 Hollow Kings).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 2 (3 Hollow Kings)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 2: ReachTrigger — medical console.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Medical Console";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = consoleReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: Dialogue khall_hologram (talk-gated at console).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Khall Hologram";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = khallHologramDialogue;

            // Step 4: Prompt — transition to central hub.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: To the Central Hub";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = yardBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06MedicalCompoundScenePath);
            EnsureScenesInBuild(Galaxy1Ep06MedicalCompoundScenePath);

            Debug.Log($"[Space Samurai] EP06 Medical Compound scene built at {Galaxy1Ep06MedicalCompoundScenePath}. " +
                      "Layout: entry corridor → medical hall with console at far end. " +
                      "Frozen colony, dense icy fog, pale blue-white lighting. " +
                      "5 steps: defeat wave 1 (3 Hollow Kings) + barks → defeat wave 2 (3 Hollow Kings) + barks → " +
                      "reach console → khall_hologram (talk) → transition to Central Hub.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Gantry Hub", priority = 92)]
        public static void BuildEp06GantryHub()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Gantry hub: narrow gantry over glowing reactor pit, dark orange-red emissive glow.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.5f, 0.4f);
            light.intensity = 0.7f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.08f, 0.06f);

            // Dark void with orange reactor glow.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.18f, 0.1f);
            RenderSettings.fogDensity = 0.018f;

            // Reactor pit emissive glow (orange-red point light below).
            BuildAccentPointLight("ReactorPit", new Vector3(0f, -3f, 7f),
                new Color(1f, 0.4f, 0.15f), intensity: 2.5f, range: 16f);
            BuildAccentPointLight("GantryLight1", new Vector3(-4f, 2.4f, 4f),
                new Color(1f, 0.45f, 0.2f), intensity: 1.6f, range: 12f);
            BuildAccentPointLight("GantryLight2", new Vector3(4f, 2.4f, 10f),
                new Color(0.95f, 0.4f, 0.18f), intensity: 1.5f, range: 11f);

            // ---- Gantry hub: narrow gantry bridge (z 0-14) over void.
            var gantryGo = new GameObject("GantryHubInterior");
            var gantry = gantryGo.transform;
            var gantryFloor = new Color(0.2f, 0.18f, 0.16f);
            var voidColor = new Color(0.08f, 0.06f, 0.04f);

            // Narrow gantry floor: x[-1.5,1.5], z[0,14], thin strip over reactor pit.
            BuildFloorCeiling(gantry, "GantryBridge", new Vector3(0f, 0f, 7f), new Vector3(3f, 0f, 14f), gantryFloor, voidColor);

            // Gantry side rails (low walls).
            BuildWall(gantry, "Gantry_RailW", new Vector3(-1.5f, 1f, 7f), new Vector3(0.15f, 1.2f, 14f));
            BuildWall(gantry, "Gantry_RailE", new Vector3(1.5f, 1f, 7f), new Vector3(0.15f, 1.2f, 14f));

            // Void blackness beneath (emissive quad, no collider, represents the reactor pit).
            var pitQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pitQuad.name = "ReactorVoid";
            pitQuad.transform.SetParent(gantry, false);
            pitQuad.transform.localPosition = new Vector3(0f, -0.5f, 7f);
            pitQuad.transform.localScale = new Vector3(4f, 1f, 16f);
            pitQuad.transform.Rotate(90f, 0f, 0f);
            TintShared(pitQuad.GetComponent<Renderer>(), new Color(0.2f, 0.08f, 0.03f, 0.9f));
            var pqCollider = pitQuad.GetComponent<Collider>();
            if (pqCollider != null) Object.DestroyImmediate(pqCollider);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 30f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Khall stands at far end of gantry.
            var khallPos = new Vector3(0f, 1f, 14f);
            var khallGo = InstantiateNpc(KhallPrefabPath, khallPos, "Khall");
            if (khallGo != null)
            {
                var khallNpc = khallGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(khallNpc);
                kSo.FindProperty("displayName").stringValue = "Khall";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var gantryMonologueDialogue = BuildEp06DialoguePlayer("Dialogue_GantryMonologue", khallPos, "gantry_monologue", talkRef);
            var gantryEliteBarksDialogue = BuildEp06DialoguePlayer("Dialogue_GantryEliteBarks", new Vector3(0f, 1.5f, 7f), "gantry_elite_barks");

            // ---- Enemies: 2 Dominion Elite (dark tint, 2x health, 1 wave of 2) ----
            var eliteDark = new Color(0.35f, 0.35f, 0.38f);
            var elitePositions = new Vector3[]
            {
                new Vector3(-1f, 0f, 5f),
                new Vector3(1f, 0f, 9f)
            };

            var eliteHealths = new List<Health>();
            foreach (var pos in elitePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteDark);
                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    var ehSo = new SerializedObject(enemyHealth);
                    ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 2f;
                    ehSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                eliteHealths.Add(enemyHealth);
            }

            var eliteWaveSpawner = BuildWaveSpawner("EliteWaveSpawner", new Vector3(0f, 1f, 7f), 3f,
                new List<List<Health>> { eliteHealths }, new[] { gantryEliteBarksDialogue });

            // Reach trigger near Khall.
            var khallReachGo = new GameObject("KhallReachPoint");
            khallReachGo.transform.position = new Vector3(0f, 1f, 14f);

            // Transition box: "DESCEND TO THE ARCHIVES".
            var archivesBoxGo = BuildTransitionBox("ToArchivesBox", new Vector3(0f, 1.2f, 13f), "DESCEND TO THE ARCHIVES",
                out var archivesBtn, out var archivesTransition);
            var atSo = new SerializedObject(archivesTransition);
            atSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP06_ArchiveVault";
            atSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(archivesBtn.onClick,
                new UnityEngine.Events.UnityAction(archivesTransition.LoadOnFootScene));
            archivesBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue gantry_monologue (talk-gated at Khall).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Gantry Monologue";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = gantryMonologueDialogue;

            // Step 1: DefeatWaves — 2 Dominion Elite (2x health).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Dominion Elite (2x health)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = eliteWaveSpawner;

            // Step 2: ReachTrigger — near Khall.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Near Khall";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = khallReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 3: Prompt — transition to archives.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to the Archives";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = archivesBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06GantryHubScenePath);
            EnsureScenesInBuild(Galaxy1Ep06GantryHubScenePath);

            Debug.Log($"[Space Samurai] EP06 Gantry Hub scene built at {Galaxy1Ep06GantryHubScenePath}. " +
                      "Layout: narrow gantry bridge (~3m wide, ~14m long) over glowing reactor pit (orange-red emissive core). " +
                      "Dark void surroundings, intense orange accretion glow, Khall NPC at far end. " +
                      "4 steps: gantry_monologue (talk) at Khall → defeat 2 Elite (2x health) + barks → " +
                      "reach Khall → transition to Archive Vault.");
        }
    }
}

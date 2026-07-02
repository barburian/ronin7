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
    /// EP07 "The Technician's Debt" scene builders. Builds the Blackveil Shipbreaker Yards where Cipher
    /// encounters Tessa Rin and discovers the truth about the Ronin operatives. Wires all MissionDirector
    /// steps, enemy waves, and NPC interactions across Market Hub and Hauler scenes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all the private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep07MarketHubScenePath = SceneFolder + "/Galaxy1_EP07_MarketHub.unity";

        private static readonly string Galaxy1Ep07MarketHubSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07MarketHubScenePath);

        private const string TessaRinPrefabPath = GeneratedCharFolder + "/TessaRin.prefab";

        /// <summary>Shorthand for building a DialoguePlayer with EP07 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep07" and loads lines from Ep07Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp07DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep07Lines.Get(setId), advanceRef, setId, clipPrefix: "ep07");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Market Hub Scene", priority = 93)]
        public static void BuildEp07MarketHub()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Market hub: rusted catwalks/gantries, salvage stalls, flickering warm arc-light feel.
            // Dim ambient + warm point lights for industrial salvage ambiance.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.65f, 0.55f); // warm key
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.10f, 0.08f); // dim rust tones

            // Warm arc-light atmosphere: dim yellow-orange glow.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.4f, 0.3f, 0.2f);
            RenderSettings.fogDensity = 0.025f;

            // Market hub accent lights: warm arc-light feel.
            BuildAccentPointLight("MarketLight1", new Vector3(-3f, 2.4f, 6f),
                new Color(1f, 0.75f, 0.3f), intensity: 1.4f, range: 12f);
            BuildAccentPointLight("MarketLight2", new Vector3(3f, 2.4f, 14f),
                new Color(0.95f, 0.7f, 0.25f), intensity: 1.3f, range: 11f);

            // ---- Market Hub: salvage area (z 0-12) -> encounter zone (z 12-24).
            var marketGo = new GameObject("MarketHubInterior");
            var market = marketGo.transform;
            var floorColor = new Color(0.32f, 0.28f, 0.25f); // rust grey
            var ceilColor = new Color(0.18f, 0.16f, 0.14f);

            // Salvage area: x[-5,5], z[0,12] with catwalks and stall props.
            BuildFloorCeiling(market, "SalvageArea", new Vector3(0f, 0f, 6f), new Vector3(10f, 0f, 12f), floorColor, ceilColor);
            BuildCorridorWall(market, "SalvageArea_WallW", -5f, 0f, 12f, new float[0], 2.4f);
            BuildCorridorWall(market, "SalvageArea_WallE", 5f, 0f, 12f, new float[0], 2.4f);

            // Salvage stall props (prop boxes scattered).
            var stallColor = new Color(0.48f, 0.42f, 0.38f);
            BuildProp(market, "Stall1", new Vector3(-3f, 0.5f, 2f), new Vector3(1.5f, 1.2f, 1.2f), stallColor);
            BuildProp(market, "Stall2", new Vector3(3f, 0.5f, 4f), new Vector3(1.5f, 1.2f, 1.2f), stallColor);
            BuildProp(market, "Stall3", new Vector3(0f, 0.5f, 8f), new Vector3(1.2f, 1.2f, 1.2f), stallColor);

            // Encounter zone: x[-4,4], z[12,24].
            BuildFloorCeiling(market, "EncounterZone", new Vector3(0f, 0f, 18f), new Vector3(8f, 0f, 12f), floorColor, ceilColor);
            BuildWall(market, "EncounterZone_WallW", new Vector3(-4f, 1.5f, 18f), new Vector3(0.2f, 3f, 12f));
            BuildWall(market, "EncounterZone_WallE", new Vector3(4f, 1.5f, 18f), new Vector3(0.2f, 3f, 12f));
            BuildWall(market, "EncounterZone_WallBack", new Vector3(0f, 1.5f, 24f), new Vector3(8f, 3f, 0.2f));

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

            // ---- NPCs ----
            // Tessa at the market center.
            var tessaPos = new Vector3(0f, 1f, 8f);
            var tessaGo = InstantiateNpc(TessaRinPrefabPath, tessaPos, "Tessa");
            if (tessaGo != null)
            {
                var tessaNpc = tessaGo.AddComponent<StoryNpc>();
                var tSo = new SerializedObject(tessaNpc);
                tSo.FindProperty("displayName").stringValue = "Tessa";
                tSo.FindProperty("remote").boolValue = false;
                tSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var marketTessaDialogue = BuildEp07DialoguePlayer("Dialogue_MarketTessa", tessaPos, "market_tessa", talkRef);
            // hauler_tessa folded in from the merged Hauler scene — Tessa's full self-reveal now plays here at the market.
            var haulerTessaDialogue = BuildEp07DialoguePlayer("Dialogue_HaulerTessa", tessaPos, "hauler_tessa", talkRef);
            var enforcerBarksDialogue = BuildEp07DialoguePlayer("Dialogue_EnforcerBarks", new Vector3(0f, 1.5f, 16f), "enforcer_barks");
            var corridorChoiceDialogue = BuildEp07DialoguePlayer("Dialogue_CorridorChoice", new Vector3(0f, 1.5f, 20f), "corridor_choice");

            // ---- Enemies: 6 Yard Enforcers (rough grey, 2 waves of 3) ----
            var enforcerGrey = new Color(0.58f, 0.56f, 0.54f);

            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 14f),
                new Vector3(2f, 0f, 16f),
                new Vector3(0f, 0f, 18f)
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerGrey);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1f, 16f), 3f,
                new List<List<Health>> { wave1Healths }, new[] { enforcerBarksDialogue });

            var wave2Positions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 19f),
                new Vector3(1.5f, 0f, 21f),
                new Vector3(0f, 0f, 23f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerGrey);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 21f), 3f,
                new List<List<Health>> { wave2Healths }, new[] { enforcerBarksDialogue });

            // Reach trigger near corridor entry.
            var corridorReachGo = new GameObject("CorridorReachPoint");
            corridorReachGo.transform.position = new Vector3(0f, 1f, 23.5f);

            // Transition box: "ENTER THE VAULT" (Hauler merged into MarketHub — Tessa's full exchange happens
            // here now, so the market chains straight to the Archive vault).
            var haulerBoxGo = BuildTransitionBox("ToArchiveBox", new Vector3(0f, 1.2f, 23.2f), "ENTER THE VAULT",
                out var haulerBtn, out var haulerTransition);
            var htSo = new SerializedObject(haulerTransition);
            htSo.FindProperty("onFootScene").stringValue = Galaxy1Ep07ArchiveSceneName;
            htSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(haulerBtn.onClick,
                new UnityEngine.Events.UnityAction(haulerTransition.LoadOnFootScene));
            haulerBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 6;

            // Step 0: Dialogue market_tessa (talk-gated at Tessa).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Market Tessa";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = marketTessaDialogue;

            // Step 1: Dialogue hauler_tessa (talk-gated at Tessa) — folded in from the merged Hauler scene.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s1.FindPropertyRelative("label").stringValue = "Dialogue: Hauler Tessa";
            s1.FindPropertyRelative("dialogue").objectReferenceValue = haulerTessaDialogue;

            // Step 2: DefeatWaves — Wave 1 (3 Yard Enforcers).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 1 (3 Yard Enforcers)";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 3: DefeatWaves — Wave 2 (3 Yard Enforcers).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: Wave 2 (3 Yard Enforcers)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 4: Dialogue corridor_choice (post-fight).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Corridor Choice";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = corridorChoiceDialogue;

            // Step 5: Prompt — transition to the vault.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s5.FindPropertyRelative("label").stringValue = "Prompt: Enter the Vault";
            s5.FindPropertyRelative("promptObject").objectReferenceValue = haulerBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07MarketHubScenePath);
            EnsureScenesInBuild(Galaxy1Ep07MarketHubScenePath);

            Debug.Log($"[Space Samurai] EP07 Market Hub scene built at {Galaxy1Ep07MarketHubScenePath}. " +
                      "Layout: salvage area with stalls → encounter zone. " +
                      "Rusted catwalks, warm arc-light glow, dim ambient. " +
                      "6 steps: market_tessa (talk) → hauler_tessa (talk, folded from merged Hauler) → " +
                      "defeat wave 1 (3 Enforcers) + barks → defeat wave 2 (3 Enforcers) + barks → " +
                      "corridor_choice (dialogue) → transition to the Archive vault.");
        }
    }
}

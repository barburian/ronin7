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
    /// EP06 "Signal in the Dark" vault & collapse scene builders. Builds the frozen archive vault
    /// where Ronin-7 discovers the failsafe core and escapes the structural collapse. Wires all
    /// MissionDirector steps, enemy waves, and NPC interactions across ArchiveVault and Collapse scenes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as Ep06Builder, so it calls
    /// the private static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep06ArchiveVaultScenePath = SceneFolder + "/Galaxy1_EP06_ArchiveVault.unity";

        private static readonly string Galaxy1Ep06ArchiveVaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06ArchiveVaultScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Archive Vault", priority = 85)]
        public static void BuildEp06ArchiveVault()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive vault interior: cold, frozen, pale-blue palette with dark vault accents.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.8f, 0.95f);
            light.intensity = 0.8f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.15f, 0.22f);

            // Frozen pale-blue fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.3f, 0.4f, 0.5f);
            RenderSettings.fogDensity = 0.022f;

            // Frozen accent lights (cool cyan).
            BuildAccentPointLight("FrozenLight1", new Vector3(-3f, 2.6f, 12f),
                new Color(0.5f, 0.8f, 1f), intensity: 1.4f, range: 12f);
            BuildAccentPointLight("FrozenLight2", new Vector3(3f, 2.6f, 20f),
                new Color(0.6f, 0.85f, 1f), intensity: 1.3f, range: 12f);

            // ---- Archive vault: sealed records chamber (z 0-8) -> archive north wing (z 8-16) ->
            // archive east wing (z 16-24) -> final chamber (z 24-32).
            var vaultGo = new GameObject("ArchiveVaultInterior");
            var vault = vaultGo.transform;
            var darkVaultColor = new Color(0.12f, 0.14f, 0.18f);
            var frozenWallColor = new Color(0.18f, 0.22f, 0.3f);

            // Sealed records chamber: x[-4,4], z[0,8], dark scarred vault with pried-open props.
            BuildFloorCeiling(vault, "SealedRecords", new Vector3(0f, 0f, 4f), new Vector3(8f, 0f, 8f), darkVaultColor, new Color(0.08f, 0.1f, 0.14f));
            BuildWall(vault, "Records_WallW", new Vector3(-4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Records_WallE", new Vector3(4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Records_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));

            // Scarred/pried vault door props.
            BuildProp(vault, "VaultDoor1", new Vector3(-2f, 1.2f, 3f), new Vector3(1.5f, 2.5f, 0.4f), new Color(0.25f, 0.25f, 0.28f));
            BuildProp(vault, "VaultDoor2", new Vector3(2f, 1.2f, 5f), new Vector3(1.5f, 2.5f, 0.4f), new Color(0.25f, 0.25f, 0.28f));
            BuildProp(vault, "ScrapMetal1", new Vector3(-1f, 0.3f, 6f), new Vector3(0.8f, 0.2f, 0.6f), new Color(0.35f, 0.3f, 0.25f));

            // Archive North Wing: x[-5,5], z[8,16], frozen corridor with ice props.
            BuildFloorCeiling(vault, "ArchiveNorth", new Vector3(0f, 0f, 12f), new Vector3(10f, 0f, 8f), frozenWallColor, new Color(0.1f, 0.12f, 0.18f));
            BuildCorridorWall(vault, "ArchiveNorth_WallW", -5f, 8f, 16f, new float[0], 2.4f);
            BuildCorridorWall(vault, "ArchiveNorth_WallE", 5f, 8f, 16f, new float[0], 2.4f);

            // Ice props in north wing.
            var iceColor = new Color(0.4f, 0.5f, 0.65f);
            BuildProp(vault, "IceBlock1", new Vector3(-2.5f, 0.6f, 10f), new Vector3(1.2f, 1.2f, 1.2f), iceColor);
            BuildProp(vault, "IceBlock2", new Vector3(2.5f, 0.6f, 13f), new Vector3(1.2f, 1.2f, 1.2f), iceColor);
            BuildProp(vault, "IceBlock3", new Vector3(0f, 0.5f, 15f), new Vector3(1f, 0.8f, 1f), iceColor);

            // Archive East Wing: x[-4,4], z[16,24], colder and darker.
            BuildFloorCeiling(vault, "ArchiveEast", new Vector3(0f, 0f, 20f), new Vector3(8f, 0f, 8f), new Color(0.14f, 0.16f, 0.22f), new Color(0.07f, 0.08f, 0.12f));
            BuildCorridorWall(vault, "ArchiveEast_WallW", -4f, 16f, 24f, new float[0], 2.4f);
            BuildCorridorWall(vault, "ArchiveEast_WallE", 4f, 16f, 24f, new float[0], 2.4f);

            // Final Chamber: x[-6,6], z[24,32], largest coldest zone with data core pedestal.
            BuildFloorCeiling(vault, "FinalChamber", new Vector3(0f, 0f, 28f), new Vector3(12f, 0f, 8f), darkVaultColor, new Color(0.06f, 0.08f, 0.12f));
            BuildWall(vault, "Final_WallW", new Vector3(-6f, 1.5f, 28f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Final_WallE", new Vector3(6f, 1.5f, 28f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Final_WallBack", new Vector3(0f, 1.5f, 32f), new Vector3(12f, 3f, 0.2f));

            // Data core pedestal: emissive glowing cube on pedestal.
            BuildProp(vault, "Pedestal", new Vector3(0f, 0.8f, 28f), new Vector3(1.2f, 0.5f, 1.2f), new Color(0.2f, 0.2f, 0.25f));
            var coreQuad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coreQuad.name = "DataCore";
            coreQuad.transform.SetParent(vault, false);
            coreQuad.transform.localPosition = new Vector3(0f, 1.8f, 28f);
            coreQuad.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            TintShared(coreQuad.GetComponent<Renderer>(), new Color(0f, 1f, 0.8f, 0.9f));
            var coreCollider = coreQuad.GetComponent<Collider>();
            if (coreCollider != null) Object.DestroyImmediate(coreCollider);

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
            // Khall in sealed records chamber.
            var khallPos = new Vector3(0f, 1f, 4f);
            var khallGo = InstantiateNpc(ArtPrefabBuilder.KhallHologramPrefabPath, khallPos, "Khall");
            if (khallGo != null)
            {
                var khallNpc = khallGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(khallNpc);
                kSo.FindProperty("displayName").stringValue = "Khall";
                kSo.FindProperty("remote").boolValue = true;  // Holographic
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Alarm lights (zone 2+3 transition). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2.5f, 2.4f, 14f), new Vector3(2.5f, 2.4f, 16f));

            // ---- Dialogue Players ----
            var archiveDealDialogue = BuildEp06DialoguePlayer("Dialogue_ArchiveDeal", khallPos, "archive_deal", talkRef);
            var archiveBarksDialogue = BuildEp06DialoguePlayer("Dialogue_ArchiveBarks", new Vector3(0f, 1.5f, 18f), "archive_barks");
            var failsafeActivationDialogue = BuildEp06DialoguePlayer("Dialogue_FailsafeActivation", khallPos, "failsafe_activation", talkRef);

            // ---- Enemies: Zone 2 (3 Hollow Kings) ----
            var hollowKingColor = new Color(0.5f, 0.55f, 0.6f);
            var zone2Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 10f),
                new Vector3(0f, 0f, 12f),
                new Vector3(2f, 0f, 14f)
            };
            var zone2Healths = new List<Health>();
            foreach (var pos in zone2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hollowKingColor);
                enemy.gameObject.SetActive(false);
                zone2Healths.Add(enemy.GetComponent<Health>());
            }

            // ---- Enemies: Zone 3 (4 Hollow Kings) ----
            var zone3Positions = new Vector3[]
            {
                new Vector3(-2.5f, 0f, 17f),
                new Vector3(-0.5f, 0f, 19f),
                new Vector3(0.5f, 0f, 20f),
                new Vector3(2.5f, 0f, 22f)
            };
            var zone3Healths = new List<Health>();
            foreach (var pos in zone3Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hollowKingColor);
                enemy.gameObject.SetActive(false);
                zone3Healths.Add(enemy.GetComponent<Health>());
            }

            // ---- Enemies: Zone 4 (3 Hollow Kings, 1 with 2x health for elite whip operator) ----
            var zone4Positions = new Vector3[]
            {
                new Vector3(-2f, 0f, 26f),
                new Vector3(0f, 0f, 28f),
                new Vector3(2f, 0f, 30f)
            };
            var zone4Healths = new List<Health>();
            for (int i = 0; i < zone4Positions.Length; i++)
            {
                var pos = zone4Positions[i];
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hollowKingColor);
                var enemyHealth = enemy.GetComponent<Health>();

                // Second enemy in zone 4 is the elite whip operator (2x health).
                if (i == 1 && enemyHealth != null)
                {
                    var ehSo = new SerializedObject(enemyHealth);
                    ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 2f;
                    ehSo.ApplyModifiedPropertiesWithoutUndo();
                }

                enemy.gameObject.SetActive(false);
                zone4Healths.Add(enemyHealth);
            }

            // Reach triggers.
            var zone2ReachGo = new GameObject("Zone2ReachPoint");
            zone2ReachGo.transform.position = new Vector3(0f, 1f, 8f);

            var coreReachGo = new GameObject("DataCoreReachPoint");
            coreReachGo.transform.position = new Vector3(0f, 1f, 28f);

            var khallReturnReachGo = new GameObject("KhallReturnReachPoint");
            khallReturnReachGo.transform.position = new Vector3(0f, 1f, 4f);

            // Transition box: "RETURN TO THE CORSAIR" (Collapse cut — ArchiveVault now chains straight to the EP06 Escape space scene).
            var collapseBoxGo = BuildTransitionBox("ToCorsairBox", new Vector3(0f, 1.2f, 30.5f), "RETURN TO THE CORSAIR",
                out var collapseBtn, out var collapseTransition);
            var ctSo = new SerializedObject(collapseTransition);
            ctSo.FindProperty("onFootScene").stringValue = Galaxy1Ep06EscapeSceneName;
            ctSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(collapseBtn.onClick,
                new UnityEngine.Events.UnityAction(collapseTransition.LoadOnFootScene));
            collapseBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue archive_deal (talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archive Deal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveDealDialogue;

            // Step 1: ReachTrigger — zone 2 entrance.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Archive North Wing";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = zone2ReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: DefeatWaves — 3 waves (3/4/3 Hollow Kings).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: Zones 2+3+4 Hollow Kings (3/4/3)";
            // Note: Wave spawner expects all waves in a single spawner. We'll combine them.
            var allWaveHealths = new List<List<Health>> { zone2Healths, zone3Healths, zone4Healths };
            var combinedSpawner = BuildEp03WaveSpawner("AllWavesSpawner", new Vector3(0f, 1f, 20f), 3f,
                allWaveHealths, new[] { archiveBarksDialogue });
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = combinedSpawner;

            // Step 3: ReachTrigger — data core.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s3.FindPropertyRelative("label").stringValue = "ReachTrigger: Data Core";
            s3.FindPropertyRelative("reachPoint").objectReferenceValue = coreReachGo.transform;
            s3.FindPropertyRelative("reachRadius").floatValue = 2f;

            // Step 4: ReachTrigger — return to Khall.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Return to Khall";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = khallReturnReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue failsafe_activation (talk-gated).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Failsafe Activation";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = failsafeActivationDialogue;

            // Step 6: Prompt — return to the corsair.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Return to the Corsair";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = collapseBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06ArchiveVaultScenePath);
            EnsureScenesInBuild(Galaxy1Ep06ArchiveVaultScenePath);

            Debug.Log($"[Space Samurai] EP06 Archive Vault scene built at {Galaxy1Ep06ArchiveVaultScenePath}. " +
                      "Layout: sealed records chamber (dark vault, scarred props, Khall) → archive north wing (frozen, ice props, 3 Hollow Kings) → " +
                      "archive east wing (colder/darker, 4 Hollow Kings) → final chamber (coldest, 3 Hollow Kings + 1 elite whip, glowing data core). " +
                      "Pale-blue frozen fog. " +
                      "7 steps: archive_deal (talk) → reach zone 2 → defeat 3 waves (3/4/3 Hollow Kings) + barks → reach data core → " +
                      "return to Khall → failsafe_activation (talk) → RETURN TO THE CORSAIR (transition to Galaxy1_EP06_Escape).");
        }
    }
}

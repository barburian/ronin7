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
    /// EP07 "The Technician's Debt" vault & cargo shaft scene builders. Builds the Dominion storage
    /// vault where Cipher discovers the Kethel-7 archive and fights the Guard assault, then escapes
    /// through the cargo shaft. Wires all MissionDirector steps, enemy waves, and NPC interactions.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as Ep07Builder, so it calls
    /// the private static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Galaxy1Ep07ArchiveScenePath = SceneFolder + "/Galaxy1_EP07_Archive.unity";

        private static readonly string Galaxy1Ep07ArchiveSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07ArchiveScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Archive Scene", priority = 95)]
        public static void BuildEp07Archive()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive vault: industrial, dark metallic Dominion palette with coolant stains.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.72f, 0.75f);
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.16f);

            // Industrial grey fog with slight tint.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.27f, 0.3f);
            RenderSettings.fogDensity = 0.025f;

            // Industrial accent lights (dim grey-white).
            BuildAccentPointLight("VaultLight1", new Vector3(-4f, 2.6f, 12f),
                new Color(0.65f, 0.67f, 0.7f), intensity: 1.2f, range: 11f);
            BuildAccentPointLight("VaultLight2", new Vector3(4f, 2.6f, 20f),
                new Color(0.6f, 0.62f, 0.65f), intensity: 1.1f, range: 10f);

            // ---- Storage vault: entry approach (z 0-8) -> catwalk grid over reservoir (z 8-24).
            var vaultGo = new GameObject("StorageVaultInterior");
            var vault = vaultGo.transform;
            var metalFloorColor = new Color(0.22f, 0.24f, 0.26f);
            var metalCeilColor = new Color(0.14f, 0.16f, 0.18f);

            // Entry approach: x[-4,4], z[0,8], sealed corridor.
            BuildFloorCeiling(vault, "VaultEntrance", new Vector3(0f, 0f, 4f), new Vector3(8f, 0f, 8f), metalFloorColor, metalCeilColor);
            BuildWall(vault, "Entrance_WallW", new Vector3(-4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Entrance_WallE", new Vector3(4f, 1.5f, 4f), new Vector3(0.2f, 3f, 8f));
            BuildWall(vault, "Entrance_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));

            // Catwalk grid over dead coolant reservoir: x[-5,5], z[8,24], elevated platforms.
            var catwalkColor = new Color(0.3f, 0.32f, 0.35f);
            var reservoirColor = new Color(0.08f, 0.1f, 0.12f);

            // Reservoir floor (far below, dark reflective).
            BuildFloorCeiling(vault, "DeadReservoir", new Vector3(0f, -1.5f, 16f), new Vector3(10f, 0f, 16f), reservoirColor, reservoirColor);

            // Catwalk platforms (elevated, grid-like).
            BuildFloorCeiling(vault, "CatwalkNorth", new Vector3(0f, 0.3f, 12f), new Vector3(10f, 0f, 8f), catwalkColor, metalCeilColor);
            BuildFloorCeiling(vault, "CatwalkCentral", new Vector3(0f, 0.3f, 16f), new Vector3(8f, 0f, 8f), catwalkColor, metalCeilColor);
            BuildFloorCeiling(vault, "CatwalkSouth", new Vector3(0f, 0.3f, 20f), new Vector3(10f, 0f, 8f), catwalkColor, metalCeilColor);

            // Holo-display pedestal in central catwalk chamber.
            var pedestalColor = new Color(0.25f, 0.27f, 0.3f);
            BuildProp(vault, "HoloPedestal", new Vector3(0f, 0.8f, 16f), new Vector3(1.2f, 0.6f, 1.2f), pedestalColor);

            // Emissive holographic display (no collider).
            var holoColor = new Color(0.2f, 0.9f, 0.95f, 0.8f);
            var holoDisplay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            holoDisplay.name = "HoloDisplay";
            holoDisplay.transform.SetParent(vault, false);
            holoDisplay.transform.localPosition = new Vector3(0f, 1.8f, 16f);
            holoDisplay.transform.localScale = new Vector3(0.5f, 0.5f, 0.08f);
            TintShared(holoDisplay.GetComponent<Renderer>(), holoColor);
            var holoCollider = holoDisplay.GetComponent<Collider>();
            if (holoCollider != null) Object.DestroyImmediate(holoCollider);

            // Sparking conduit props scattered on catwalks.
            var conduitColor = new Color(0.4f, 0.42f, 0.45f);
            BuildProp(vault, "ConduitProp1", new Vector3(-2f, 0.8f, 10f), new Vector3(0.3f, 1.5f, 0.3f), conduitColor);
            BuildProp(vault, "ConduitProp2", new Vector3(3f, 0.8f, 18f), new Vector3(0.3f, 1.5f, 0.3f), conduitColor);
            BuildProp(vault, "ConduitProp3", new Vector3(-3f, 0.8f, 22f), new Vector3(0.3f, 1.2f, 0.3f), conduitColor);

            // Sparking electrical-arc effects (emissive, no collider).
            var arcColor = new Color(0.9f, 0.5f, 0.2f, 0.6f);
            var arc1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            arc1.name = "ConduitSpark1";
            arc1.transform.SetParent(vault, false);
            arc1.transform.localPosition = new Vector3(-2.2f, 2f, 10f);
            arc1.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            TintShared(arc1.GetComponent<Renderer>(), arcColor);
            var a1col = arc1.GetComponent<Collider>();
            if (a1col != null) Object.DestroyImmediate(a1col);

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
            // Tessa at holo-display.
            var tessaPos = new Vector3(0f, 1f, 16f);
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
            var kethilFileDialogue = BuildEp07DialoguePlayer("Dialogue_KethelFile", tessaPos, "kethel_file", talkRef);
            var guardAssaultBarksDialogue = BuildEp07DialoguePlayer("Dialogue_GuardAssaultBarks", new Vector3(0f, 1.5f, 16f), "guard_assault");
            var architectureDyingDialogue = BuildEp07DialoguePlayer("Dialogue_ArchitectureDying", tessaPos, "architecture_dying", talkRef);

            // ---- Enemies: 3 waves of Dominion Guard (3/4/3) on catwalks ----
            var guardColor = new Color(0.35f, 0.37f, 0.4f);

            // Wave 1: 3 Guards on north catwalk.
            var wave1Positions = new Vector3[]
            {
                new Vector3(-2f, 0.5f, 10f),
                new Vector3(0f, 0.5f, 11f),
                new Vector3(2f, 0.5f, 12f)
            };
            var wave1Healths = new List<Health>();
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, guardColor);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 2: 4 Guards on central catwalk.
            var wave2Positions = new Vector3[]
            {
                new Vector3(-2.5f, 0.5f, 14f),
                new Vector3(-0.5f, 0.5f, 15f),
                new Vector3(0.5f, 0.5f, 17f),
                new Vector3(2.5f, 0.5f, 18f)
            };
            var wave2Healths = new List<Health>();
            foreach (var pos in wave2Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, guardColor);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            // Wave 3: 3 Guards on south catwalk.
            var wave3Positions = new Vector3[]
            {
                new Vector3(-2f, 0.5f, 20f),
                new Vector3(0f, 0.5f, 21f),
                new Vector3(2f, 0.5f, 22f)
            };
            var wave3Healths = new List<Health>();
            foreach (var pos in wave3Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, guardColor);
                enemy.gameObject.SetActive(false);
                wave3Healths.Add(enemy.GetComponent<Health>());
            }

            // Reach triggers.
            var holoReachGo = new GameObject("HoloDisplayReachPoint");
            holoReachGo.transform.position = new Vector3(0f, 1f, 16f);

            var shaftReachGo = new GameObject("CargoShaftReachPoint");
            shaftReachGo.transform.position = new Vector3(0f, 1f, 24f);

            // Transition box: "RETURN TO THE CORSAIR" (Cargo Shaft cut — Archive now chains straight to the EP07 Escape space scene).
            var shaftBoxGo = BuildTransitionBox("ToCorsairBox", new Vector3(0f, 1.2f, 23.5f), "RETURN TO THE CORSAIR",
                out var shaftBtn, out var shaftTransition);
            var stSo = new SerializedObject(shaftTransition);
            stSo.FindProperty("onFootScene").stringValue = Galaxy1Ep07EscapeSceneName;
            stSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(shaftBtn.onClick,
                new UnityEngine.Events.UnityAction(shaftTransition.LoadOnFootScene));
            shaftBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue kethel_file (talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Kethel File";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = kethilFileDialogue;

            // Step 1: DefeatWaves — 3 waves (3/4/3 Dominion Guard).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Guard Assault (3/4/3)";
            var allWaveHealths = new List<List<Health>> { wave1Healths, wave2Healths, wave3Healths };
            var combinedSpawner = BuildEp03WaveSpawner("GuardAssaultSpawner", new Vector3(0f, 1f, 16f), 3.5f,
                allWaveHealths, new[] { guardAssaultBarksDialogue });
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = combinedSpawner;

            // Step 2: ReachTrigger — cargo shaft.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Cargo Shaft";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = shaftReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 3: Dialogue architecture_dying (talk-gated).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Architecture of Dying";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = architectureDyingDialogue;

            // Step 4: Prompt — return to the corsair.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Return to the Corsair";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = shaftBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07ArchiveScenePath);
            EnsureScenesInBuild(Galaxy1Ep07ArchiveScenePath);

            Debug.Log($"[Space Samurai] EP07 Archive scene built at {Galaxy1Ep07ArchiveScenePath}. " +
                      "Layout: vault entrance → catwalk grid over dead coolant reservoir with holo-display pedestal, Tessa, sparking conduits. " +
                      "Industrial metallic grey palette, dark fog. " +
                      "5 steps: kethel_file (talk) → defeat 3 Guard waves (3/4/3) + barks → reach cargo shaft → " +
                      "architecture_dying (talk) → RETURN TO THE CORSAIR (transition to Galaxy1_EP07_Escape).");
        }
    }
}

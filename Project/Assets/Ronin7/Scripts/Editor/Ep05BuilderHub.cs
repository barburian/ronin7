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
    /// EP05 "The Broken Echo" scene builders. Builds the Rust Collective salvage station finale
    /// where Ronin-7 confronts Khall and the defective generation assembles. Wires all MissionDirector
    /// steps, enemy waves, and NPC interactions across Rotunda and Command Hub scenes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as Ep05Builder, so it calls
    /// the private static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Rotunda", priority = 83)]
        public static void BuildEp05Rotunda()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rust Collective rotunda: cool grey + amber accents, industrial crane and reactor-cooling props,
            // echoing steel palette, neutron-star accretion glow accent.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.75f, 0.78f);
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.17f);

            // Cool grey exponential fog with hint of rust.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.18f, 0.2f);
            RenderSettings.fogDensity = 0.02f;

            // Amber accretion-glow accent lights.
            BuildAccentPointLight("AmberLight1", new Vector3(-4f, 3f, 12f),
                new Color(1f, 0.65f, 0.3f), intensity: 1.5f, range: 16f);
            BuildAccentPointLight("AmberLight2", new Vector3(4f, 3f, 16f),
                new Color(0.95f, 0.6f, 0.25f), intensity: 1.4f, range: 15f);

            // ---- Rotunda: central circular-ish chamber (z 0-20) with crane/reactor props ->
            // bridge room (z 20-28) with console props and viewport.
            var rotundaGo = new GameObject("RotundaInterior");
            var rotunda = rotundaGo.transform;
            var steelColor = new Color(0.25f, 0.25f, 0.27f);
            var darkSteelColor = new Color(0.15f, 0.15f, 0.17f);

            // Central chamber: x[-8,8], z[0,20], large open space.
            BuildFloorCeiling(rotunda, "CentralChamber", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), steelColor, darkSteelColor);
            BuildWall(rotunda, "Chamber_WallW", new Vector3(-8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(rotunda, "Chamber_WallE", new Vector3(8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Crane prop (overhead structure, approximated).
            BuildProp(rotunda, "CraneArm", new Vector3(0f, 2.5f, 8f), new Vector3(10f, 0.4f, 1.5f), steelColor);
            BuildProp(rotunda, "CraneBase", new Vector3(-6f, 0.5f, 8f), new Vector3(1f, 2f, 1f), steelColor);

            // Reactor cooling pipes (exposed-girder flavor).
            BuildProp(rotunda, "CoolingPipe1", new Vector3(3f, 1.2f, 15f), new Vector3(0.3f, 0.3f, 6f), darkSteelColor);
            BuildProp(rotunda, "CoolingPipe2", new Vector3(-3f, 1.2f, 12f), new Vector3(0.3f, 0.3f, 4f), darkSteelColor);

            // Bridge room: x[-6,6], z[20,28].
            BuildFloorCeiling(rotunda, "BridgeRoom", new Vector3(0f, 0f, 24f), new Vector3(12f, 0f, 8f), steelColor, darkSteelColor);
            BuildWall(rotunda, "Bridge_WallW", new Vector3(-6f, 1.5f, 24f), new Vector3(0.2f, 3f, 8f));
            BuildWall(rotunda, "Bridge_WallE", new Vector3(6f, 1.5f, 24f), new Vector3(0.2f, 3f, 8f));
            BuildWall(rotunda, "Bridge_WallBack", new Vector3(0f, 1.5f, 28f), new Vector3(12f, 3f, 0.2f));

            // Console props in bridge room.
            BuildProp(rotunda, "Console1", new Vector3(-2f, 0.8f, 24f), new Vector3(1.2f, 1.2f, 0.5f), new Color(0.1f, 0.12f, 0.15f));
            BuildProp(rotunda, "Console2", new Vector3(2f, 0.8f, 24f), new Vector3(1.2f, 1.2f, 0.5f), new Color(0.1f, 0.12f, 0.15f));

            // Viewport emissive quad (no collider).
            var viewportQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            viewportQuad.name = "Viewport";
            viewportQuad.transform.SetParent(rotunda, false);
            viewportQuad.transform.localPosition = new Vector3(0f, 1.5f, 20f);
            viewportQuad.transform.localScale = new Vector3(6f, 3f, 1f);
            TintShared(viewportQuad.GetComponent<Renderer>(), new Color(1f, 0.4f, 0.15f, 0.7f));
            var vpCollider = viewportQuad.GetComponent<Collider>();
            if (vpCollider != null) Object.DestroyImmediate(vpCollider);

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

            // ---- Alarm lights (two sets). ----
            var alarmLights1Go = BuildEp03AlarmLights(new Vector3(-4f, 2.8f, 10f), new Vector3(4f, 2.8f, 12f));
            var alarmLights2Go = BuildEp03AlarmLights(new Vector3(-3f, 2.8f, 24f), new Vector3(3f, 2.8f, 26f));

            // ---- Dialogue Players ----
            var rotundaAlertDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaAlert", new Vector3(0f, 1.5f, 5f), "rotunda_alert");
            var rotundaStrikeBarksDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaStrikeBarks", new Vector3(0f, 1.5f, 12f), "rotunda_strike_barks");
            var rotundaChoiceDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaChoice", new Vector3(0f, 1.5f, 10f), "rotunda_choice", talkRef);
            var bridgeBoardersBarksDialogue = BuildEp05DialoguePlayer("Dialogue_BridgeBoardersBarks", new Vector3(0f, 1.5f, 24f), "bridge_boarders_barks");
            var bridgeOthersDialogue = BuildEp05DialoguePlayer("Dialogue_BridgeOthers", new Vector3(0f, 1.5f, 24f), "bridge_others", talkRef);

            // ---- Enemies: Rotunda Wave 1 (5 soldiers) ----
            // 4 standard soldiers (grey) + 1 Dominion Officer (darker tint, 2x health).
            var soldierGrey = new Color(0.65f, 0.65f, 0.68f);
            var officerDark = new Color(0.35f, 0.35f, 0.38f);

            var soldierPositions = new Vector3[]
            {
                new Vector3(-4f, 0f, 8f),
                new Vector3(4f, 0f, 10f),
                new Vector3(-2f, 0f, 14f),
                new Vector3(2f, 0f, 16f)
            };

            var wave1Healths = new List<Health>();
            foreach (var pos in soldierPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, soldierGrey);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Officer with 2x health.
            var officer = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var officerRenderer = officer.GetComponent<Renderer>();
            if (officerRenderer != null) TintShared(officerRenderer, officerDark);
            var officerHealth = officer.GetComponent<Health>();
            if (officerHealth != null)
            {
                var ohSo = new SerializedObject(officerHealth);
                ohSo.FindProperty("maxHealth").floatValue = ohSo.FindProperty("maxHealth").floatValue * 2f;
                ohSo.ApplyModifiedPropertiesWithoutUndo();
            }
            officer.gameObject.SetActive(false);
            wave1Healths.Add(officerHealth);

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1f, 10f), 3f,
                new List<List<Health>> { wave1Healths }, new[] { rotundaStrikeBarksDialogue });

            // ---- Enemies: Rotunda Wave 2 (2 boarders) ----
            var boarderGrey = new Color(0.6f, 0.6f, 0.63f);
            var boarderPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 24f),
                new Vector3(3f, 0f, 26f)
            };

            var wave2Healths = new List<Health>();
            foreach (var pos in boarderPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, boarderGrey);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 24f), 3f,
                new List<List<Health>> { wave2Healths }, new[] { bridgeBoardersBarksDialogue });

            // Reach triggers.
            var bridgeDoorwayReachGo = new GameObject("BridgeDoorwayReachPoint");
            bridgeDoorwayReachGo.transform.position = new Vector3(0f, 1f, 20f);

            // Transition box: "TO THE COMMAND HUB" wired to LoadOnFootScene (copy Ep04 ArchiveRing pattern).
            var hubBoxGo = BuildTransitionBox("ToCommandHubBox", new Vector3(0f, 1.2f, 26f), "TO THE COMMAND HUB",
                out var hubBtn, out var hubTransition);
            var htSo = new SerializedObject(hubTransition);
            htSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05CommandHubSceneName;
            htSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(hubBtn.onClick,
                new UnityEngine.Events.UnityAction(hubTransition.LoadOnFootScene));
            hubBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 9;

            // Step 0: Dialogue rotunda_alert (Khall over comms, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Rotunda Alert";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = rotundaAlertDialogue;

            // Step 1: Trigger — alarm lights (first pair).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Rotunda Alarms";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = alarmLights1Go;

            // Step 2: DefeatWaves — 5 soldiers (4 + 1 officer).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Soldiers + 1 Officer";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 3: Dialogue rotunda_choice (talk-gated).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Rotunda Choice";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = rotundaChoiceDialogue;

            // Step 4: ReachTrigger — bridge doorway.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Bridge Doorway";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = bridgeDoorwayReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Trigger — alarm lights (second pair, at bridge).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s5.FindPropertyRelative("label").stringValue = "Trigger: Bridge Alarms";
            var t5 = s5.FindPropertyRelative("triggerObjects");
            t5.arraySize = 1;
            t5.GetArrayElementAtIndex(0).objectReferenceValue = alarmLights2Go;

            // Step 6: DefeatWaves — 2 boarders.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s6.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Boarders";
            s6.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 7: Dialogue bridge_others (talk-gated).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Bridge Others";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = bridgeOthersDialogue;

            // Step 8: Prompt — transition to Command Hub.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s8.FindPropertyRelative("label").stringValue = "Prompt: To the Command Hub";
            s8.FindPropertyRelative("promptObject").objectReferenceValue = hubBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05RotundaScenePath);
            EnsureScenesInBuild(Galaxy1Ep05RotundaScenePath);

            Debug.Log($"[Space Samurai] EP05 Rotunda scene built at {Galaxy1Ep05RotundaScenePath}. " +
                      "Layout: central chamber (crane, reactor-cooling props, cool grey + amber accretion glow) → bridge room (consoles, viewport). " +
                      "9 steps: rotunda_alert auto → trigger alarms → defeat 5 soldiers (4+Officer 2x) + barks → rotunda_choice → reach bridge doorway → " +
                      "trigger bridge alarms → defeat 2 boarders + barks → bridge_others → transition to Command Hub.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Command Hub", priority = 84)]
        public static void BuildEp05CommandHub()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Command hub: dark, intense orange accretion-glare, life-support core pulsing,
            // holo-panel emissive props, catwalk over void.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.7f, 0.73f);
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.14f);

            // Dark void fog with orange-tinted accretion glow.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.15f, 0.08f);
            RenderSettings.fogDensity = 0.015f;

            // Intense orange accretion-glare accent lights (outer hull zone).
            BuildAccentPointLight("AccretionGlare1", new Vector3(-5f, 2.6f, 12f),
                new Color(1f, 0.5f, 0.2f), intensity: 1.8f, range: 14f);
            BuildAccentPointLight("AccretionGlare2", new Vector3(5f, 2.6f, 18f),
                new Color(0.95f, 0.45f, 0.15f), intensity: 1.7f, range: 13f);

            // ---- Command hub: archive alcove (z 0-6) -> outer-hull walkway (z 6-20) ->
            // hub arena (z 20-36).
            var hubGo = new GameObject("CommandHubInterior");
            var hub = hubGo.transform;
            var darkSteel = new Color(0.18f, 0.18f, 0.2f);
            var voidBlack = new Color(0.08f, 0.08f, 0.1f);

            // Archive alcove: x[-4,4], z[0,6].
            BuildFloorCeiling(hub, "ArchiveAlcove", new Vector3(0f, 0f, 3f), new Vector3(8f, 0f, 6f), darkSteel, voidBlack);
            BuildWall(hub, "Alcove_WallW", new Vector3(-4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(hub, "Alcove_WallE", new Vector3(4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(hub, "Alcove_WallBack", new Vector3(0f, 1.5f, 6f), new Vector3(8f, 3f, 0.2f));

            // Terminal prop in archive alcove.
            BuildProp(hub, "ArchiveTerminal", new Vector3(0f, 0.7f, 3f), new Vector3(1.4f, 1.4f, 0.6f), new Color(0.1f, 0.12f, 0.15f));

            // Emissive screen quad (no collider) on back wall of alcove.
            var screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = "ArchiveScreen";
            screenQuad.transform.SetParent(hub, false);
            screenQuad.transform.localPosition = new Vector3(0f, 1.5f, 6f);
            screenQuad.transform.localScale = new Vector3(4f, 2.5f, 1f);
            TintShared(screenQuad.GetComponent<Renderer>(), new Color(0.15f, 0.25f, 0.4f, 0.7f));
            var sqCollider = screenQuad.GetComponent<Collider>();
            if (sqCollider != null) Object.DestroyImmediate(sqCollider);

            // Outer-hull walkway: x[-6,6], z[6,20], dark narrow catwalk strips over void.
            BuildFloorCeiling(hub, "OuterWalkway", new Vector3(0f, 0f, 13f), new Vector3(12f, 0f, 14f),
                new Color(0.15f, 0.15f, 0.17f), voidBlack);
            BuildWall(hub, "Hull_WallW", new Vector3(-6f, 1.5f, 13f), new Vector3(0.2f, 3f, 14f));
            BuildWall(hub, "Hull_WallE", new Vector3(6f, 1.5f, 13f), new Vector3(0.2f, 3f, 14f));

            // Catwalk strips (raised platforms, dark).
            var catwalkDark = new Color(0.22f, 0.22f, 0.24f);
            BuildProp(hub, "CatwalkLeft", new Vector3(-4f, 0.4f, 10f), new Vector3(1.5f, 0.2f, 6f), catwalkDark);
            BuildProp(hub, "CatwalkCenter", new Vector3(0f, 0.4f, 15f), new Vector3(2f, 0.2f, 6f), catwalkDark);

            // Hub arena: x[-7,7], z[20,36], command center with holo-panels and life-support core.
            BuildFloorCeiling(hub, "HubArena", new Vector3(0f, 0f, 28f), new Vector3(14f, 0f, 16f), darkSteel, voidBlack);
            BuildWall(hub, "Arena_WallW", new Vector3(-7f, 1.5f, 28f), new Vector3(0.2f, 3f, 16f));
            BuildWall(hub, "Arena_WallE", new Vector3(7f, 1.5f, 28f), new Vector3(0.2f, 3f, 16f));

            // Holo-panel emissive props (floating panels, no colliders).
            var panelColor = new Color(0.3f, 0.5f, 0.8f, 0.8f);
            var panel1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel1.name = "HoloPanel1";
            panel1.transform.SetParent(hub, false);
            panel1.transform.localPosition = new Vector3(-3f, 1.2f, 24f);
            panel1.transform.localScale = new Vector3(2f, 1.5f, 0.2f);
            TintShared(panel1.GetComponent<Renderer>(), panelColor);
            var p1col = panel1.GetComponent<Collider>();
            if (p1col != null) Object.DestroyImmediate(p1col);

            var panel2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel2.name = "HoloPanel2";
            panel2.transform.SetParent(hub, false);
            panel2.transform.localPosition = new Vector3(3f, 1.2f, 30f);
            panel2.transform.localScale = new Vector3(2f, 1.5f, 0.2f);
            TintShared(panel2.GetComponent<Renderer>(), panelColor);
            var p2col = panel2.GetComponent<Collider>();
            if (p2col != null) Object.DestroyImmediate(p2col);

            // Life-support core prop (pulsing emissive center).
            BuildProp(hub, "LifeSupportCore", new Vector3(0f, 1f, 28f), new Vector3(1.5f, 2f, 1.5f),
                new Color(0.2f, 0.4f, 0.6f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Ronin-9 near the archive alcove.
            var ronin9Pos = new Vector3(-1.5f, 1f, 3f);
            var ronin9Go = InstantiateNpc(ArtPrefabBuilder.Ronin9PrefabPath, ronin9Pos, "Ronin9");
            if (ronin9Go != null)
            {
                var ronin9Npc = ronin9Go.AddComponent<StoryNpc>();
                var r9So = new SerializedObject(ronin9Npc);
                r9So.FindProperty("displayName").stringValue = "Ronin-9";
                r9So.FindProperty("remote").boolValue = false;
                r9So.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler near archive alcove (other side).
            var kesslerPos = new Vector3(1.5f, 1f, 3f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Khall hologram near hub center (inactive, visual anchor).
            var khallPos = new Vector3(0f, 1f, 28f);
            var khallHoloGo = InstantiateNpc(ArtPrefabBuilder.KhallHologramPrefabPath, khallPos, "KhallHologram");
            if (khallHoloGo != null)
            {
                khallHoloGo.SetActive(false);
            }

            // ---- Alarm lights (during outer hull phase). ----
            var hullAlarmLightsGo = BuildEp03AlarmLights(new Vector3(-4f, 2.8f, 10f), new Vector3(4f, 2.8f, 16f));

            // ---- Dialogue Players ----
            var archiveReadingDialogue = BuildEp05DialoguePlayer("Dialogue_ArchiveReading", ronin9Pos, "archive_reading", talkRef);
            var hullEliteBarksDialogue = BuildEp05DialoguePlayer("Dialogue_HullEliteBarks", new Vector3(0f, 1.5f, 13f), "hull_elite_barks");
            var hullReinforcementsDialogue = BuildEp05DialoguePlayer("Dialogue_HullReinforcements", new Vector3(0f, 1.5f, 13f), "hull_reinforcements", talkRef);
            var khallDuelOpenDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelOpen", new Vector3(0f, 1.5f, 28f), "khall_duel_open");
            var khallDuelBarksDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelBarks", new Vector3(0f, 1.5f, 28f), "khall_duel_barks");
            var khallDuelAfterDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelAfter", new Vector3(0f, 1.5f, 28f), "khall_duel_after", talkRef);
            var spareChoiceDialogue = BuildEp05DialoguePlayer("Dialogue_SpareChoice", new Vector3(0f, 1.5f, 28f), "spare_choice", talkRef);
            var escapeThreatDialogue = BuildEp05DialoguePlayer("Dialogue_EscapeThreat", new Vector3(0f, 1.5f, 28f), "escape_threat");

            // ---- Enemies: Outer Hull Wave (3 Elite Operatives, jet-black, 2x health each) ----
            var eliteBlack = new Color(0.15f, 0.15f, 0.18f);
            var elitePositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 8f),
                new Vector3(0f, 0f, 12f),
                new Vector3(3f, 0f, 16f)
            };

            var hullWaveHealths = new List<Health>();
            foreach (var pos in elitePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteBlack);
                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    var ehSo = new SerializedObject(enemyHealth);
                    ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 2f;
                    ehSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                hullWaveHealths.Add(enemyHealth);
            }

            var hullWaveSpawner = BuildWaveSpawner("HullWaveSpawner", new Vector3(0f, 1f, 12f), 3f,
                new List<List<Health>> { hullWaveHealths }, new[] { hullEliteBarksDialogue });

            // ---- Enemy: Khall Duel (dark-crimson/black tint, 5x health, single wave) ----
            var khallCrimson = new Color(0.4f, 0.15f, 0.15f);
            var khall = BuildDominionEnemy(new Vector3(0f, 0f, 28f), playerHealth, enemyDef);
            var khallRenderer = khall.GetComponent<Renderer>();
            if (khallRenderer != null) TintShared(khallRenderer, khallCrimson);
            var khallHealth = khall.GetComponent<Health>();
            if (khallHealth != null)
            {
                var khSo = new SerializedObject(khallHealth);
                khSo.FindProperty("maxHealth").floatValue = khSo.FindProperty("maxHealth").floatValue * 5f;
                khSo.ApplyModifiedPropertiesWithoutUndo();
            }
            khall.gameObject.SetActive(false);

            var khallWaveSpawner = BuildWaveSpawner("KhallWaveSpawner", new Vector3(0f, 1f, 28f), 3f,
                new List<List<Health>> { new List<Health> { khallHealth } }, new[] { khallDuelBarksDialogue });

            // Reach triggers.
            var outerhullReachGo = new GameObject("OuterHullReachPoint");
            outerhullReachGo.transform.position = new Vector3(0f, 1f, 13f);

            var hubReachGo = new GameObject("HubReachPoint");
            hubReachGo.transform.position = new Vector3(0f, 1f, 28f);

            // Transition box: "LAUNCH TO SPACE" wired to LoadOnFootScene (transition to EP06 Approach).
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 34f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            var ltSo = new SerializedObject(launchTransition);
            ltSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP06_Approach";
            ltSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.LoadOnFootScene));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 12;

            // Step 0: Dialogue archive_reading (talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archive Reading";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveReadingDialogue;

            // Step 1: ReachTrigger — outer hull walkway.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Outer Hull Walkway";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = outerhullReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Hull Alarms";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = hullAlarmLightsGo;

            // Step 3: DefeatWaves — 3 Elite Operatives.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Elite Operatives (2x health)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = hullWaveSpawner;

            // Step 4: Dialogue hull_reinforcements (talk-gated).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Hull Reinforcements";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = hullReinforcementsDialogue;

            // Step 5: ReachTrigger — command hub center.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Command Hub";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = hubReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 6: Dialogue khall_duel_open (auto).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Khall Duel Open";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = khallDuelOpenDialogue;

            // Step 7: DefeatWaves — Khall duel (5x health, single wave).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s7.FindPropertyRelative("label").stringValue = "DefeatWaves: Khall Duel (5x health)";
            s7.FindPropertyRelative("waveSpawner").objectReferenceValue = khallWaveSpawner;

            // Step 8: Dialogue khall_duel_after (talk-gated).
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s8.FindPropertyRelative("label").stringValue = "Dialogue: After the Duel";
            s8.FindPropertyRelative("dialogue").objectReferenceValue = khallDuelAfterDialogue;

            // Step 9: Dialogue spare_choice (talk-gated).
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s9.FindPropertyRelative("label").stringValue = "Dialogue: Spare Choice";
            s9.FindPropertyRelative("dialogue").objectReferenceValue = spareChoiceDialogue;

            // Step 10: Dialogue escape_threat (auto).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s10.FindPropertyRelative("label").stringValue = "Dialogue: Escape Threat";
            s10.FindPropertyRelative("dialogue").objectReferenceValue = escapeThreatDialogue;

            // Step 11: Prompt — launch to space (LoadOnFootScene, chains into EP06 Approach).
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s11.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            s11.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05CommandHubScenePath);
            EnsureScenesInBuild(Galaxy1Ep05CommandHubScenePath);

            Debug.Log($"[Space Samurai] EP05 Command Hub scene built at {Galaxy1Ep05CommandHubScenePath}. " +
                      "Layout: archive alcove (terminal, emissive screen, Ronin-9 + Kessler staged) → outer-hull walkway (dark catwalk, intense orange accretion-glare) → " +
                      "hub arena (holo-panels, life-support core, Khall hologram anchor). " +
                      "12 steps: archive_reading → reach outer hull → trigger alarms → defeat 3 Elite (2x) + barks → hull_reinforcements → reach hub → " +
                      "khall_duel_open auto → defeat Khall duel (5x, crimson-black) + barks → khall_duel_after → spare_choice → escape_threat → " +
                      "LAUNCH TO SPACE (LoadOnFootScene to Galaxy1_EP06_Approach, chains to EP06).");
        }
    }
}

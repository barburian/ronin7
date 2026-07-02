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
    /// EP17 "The Frequency" on-foot scene builders. Builds three core episodes:
    /// - SalvageYard: outdoor rust-red ore-world salvage yard with Rustfang breakers
    /// - Arena: underground gladiator pit with GravityRigController mechanic and 4-bout sequence
    /// - LowerPit: dark cold clone-vat horror with shock-staff guards and Vesper encounter
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        /// <summary>Shorthand for building a DialoguePlayer with EP17 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep17" and loads lines from Ep17Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp17DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep17Lines.Get(setId), advanceRef, setId, clipPrefix: "ep17");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Salvage Yard", priority = 170)]
        public static void BuildEp17SalvageYard()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Salvage Yard: outdoor rust-red ore-world salvage yard.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.55f, 0.35f); // warm rust key light
            light.intensity = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.35f, 0.25f); // warm sand ambient

            // Light dust fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.5f, 0.4f, 0.3f);
            RenderSettings.fogDensity = 0.016f;

            // Two rust accent point lights.
            BuildAccentPointLight("SalvageLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.95f, 0.5f, 0.25f), intensity: 0.85f, range: 10f);
            BuildAccentPointLight("SalvageLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.90f, 0.45f, 0.20f), intensity: 0.80f, range: 9f);

            // ---- Salvage yard floor and structure ----
            var salvageGo = new GameObject("SalvageYard");
            var salvage = salvageGo.transform;
            var rustBrown = new Color(0.55f, 0.38f, 0.28f);
            var darkerRust = new Color(0.40f, 0.28f, 0.20f);

            // Main salvage floor.
            BuildFloorCeiling(salvage, "SalvageFloor", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), rustBrown, darkerRust);

            // Salvage yard walls.
            BuildWall(salvage, "SalvageWall_W", new Vector3(-8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(salvage, "SalvageWall_E", new Vector3(8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Scrap props: crates, barrels, crane pieces.
            var scrapColor = new Color(0.50f, 0.40f, 0.32f);
            BuildProp(salvage, "Crate1", new Vector3(-4f, 0.6f, 5f), new Vector3(1.2f, 1f, 1.2f), scrapColor);
            BuildProp(salvage, "Crate2", new Vector3(4f, 0.6f, 8f), new Vector3(1.2f, 1f, 1.2f), scrapColor);
            BuildProp(salvage, "Barrel1", new Vector3(-2f, 0.5f, 12f), new Vector3(0.6f, 1.2f, 0.6f), scrapColor);
            BuildProp(salvage, "Crane", new Vector3(0f, 1.8f, 6f), new Vector3(0.4f, 2.5f, 0.4f), new Color(0.45f, 0.35f, 0.28f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Dialogue Players ----
            var bloodDebtDialogue = BuildEp17DialoguePlayer("Dialogue_BloodDebt", new Vector3(0f, 1.5f, 2f), "blood_debt");
            var bdSo = new SerializedObject(bloodDebtDialogue);
            bdSo.FindProperty("playOnStart").boolValue = true;
            bdSo.ApplyModifiedPropertiesWithoutUndo();

            var pitlordStudyDialogue = BuildEp17DialoguePlayer("Dialogue_PitlordStudy", new Vector3(0f, 1.5f, 10f), "pitlord_study");

            // ---- 3 Rustfang Breaker enemies: 1 wave ----
            var breackerColor = new Color(0.6f, 0.4f, 0.3f); // rusty tint
            var breakerWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 6f),
                new Vector3(0f, 0f, 6.5f),
                new Vector3(1.5f, 0f, 6f)
            };

            var breakerWaveHealths = new List<Health>();
            foreach (var pos in breakerWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, breackerColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                breakerWaveHealths.Add(enemy.GetComponent<Health>());
            }

            var intakeBarksDialogue = BuildEp17DialoguePlayer("Dialogue_IntakeBarks", new Vector3(0f, 1.5f, 8f), "intake_barks");
            var breakerSpawner = BuildEp03WaveSpawner("BreackerSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { breakerWaveHealths },
                new[] { intakeBarksDialogue });

            // Transition box: "ENTER — THE PITS".
            var pitsBoxGo = BuildTransitionBox("ToPitsBox", new Vector3(0f, 1.2f, 20.5f), "ENTER — THE PITS",
                out var pitsBtn, out var pitsTransition);
            var pbSo = new SerializedObject(pitsTransition);
            pbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep17ArenaSceneName;
            pbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pitsBtn.onClick,
                new UnityEngine.Events.UnityAction(pitsTransition.LoadOnFootScene));
            pitsBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue blood_debt (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Blood Debt";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = bloodDebtDialogue;

            // Step 1: DefeatWaves — 3 Rustfang Breakers.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Rustfang Breakers (3)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = breakerSpawner;

            // Step 2: Dialogue pitlord_study.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Pitlord Study";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = pitlordStudyDialogue;

            // Step 3: Prompt — transition to Pits.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Enter The Pits";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = pitsBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17SalvageYardScenePath);
            EnsureScenesInBuild(Galaxy3Ep17SalvageYardScenePath, Galaxy3Ep17ArenaScenePath);

            Debug.Log($"[Space Samurai] EP17 Salvage Yard scene built at {Galaxy3Ep17SalvageYardScenePath}. " +
                      "Layout: outdoor rust-red ore-world salvage yard with warm lighting, scrap/crate props. " +
                      "3 Rustfang Breakers (rusty tint, nonLethal). " +
                      "4 steps: blood_debt (auto) → defeat 3 Rustfang Breakers (intake_barks bark) → " +
                      "pitlord_study dialogue → transition to Pits Arena.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Arena", priority = 171)]
        public static void BuildEp17Arena()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Arena: underground gladiator pit with gravity-rig mechanic.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.40f, 0.48f, 0.55f); // cool grey key light
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.25f); // dark ambient

            // Light fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.35f, 0.38f);
            RenderSettings.fogDensity = 0.019f;

            // Two cool grey accent lights.
            BuildAccentPointLight("ArenaLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.55f, 0.60f, 0.70f), intensity: 0.8f, range: 10f);
            BuildAccentPointLight("ArenaLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.50f, 0.55f, 0.65f), intensity: 0.75f, range: 9f);

            // ---- Arena floor, walls, and spectator tiers ----
            var arenaGo = new GameObject("Arena");
            var arena = arenaGo.transform;
            var arenaFloor = new Color(0.35f, 0.38f, 0.42f);
            var arenaDark = new Color(0.22f, 0.25f, 0.30f);

            // Main arena floor (circular-ish).
            BuildFloorCeiling(arena, "ArenaFloor", new Vector3(0f, 0f, 10f), new Vector3(14f, 0f, 18f), arenaFloor, arenaDark);

            // Arena walls.
            BuildWall(arena, "ArenaWall_W", new Vector3(-7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));
            BuildWall(arena, "ArenaWall_E", new Vector3(7f, 1.5f, 10f), new Vector3(0.2f, 3f, 18f));

            // Spectator tier props.
            var tierColor = new Color(0.40f, 0.42f, 0.48f);
            BuildProp(arena, "SpectatorTier1", new Vector3(-4f, 1.5f, 4f), new Vector3(2.5f, 0.6f, 1.5f), tierColor);
            BuildProp(arena, "SpectatorTier2", new Vector3(4f, 1.5f, 6f), new Vector3(2.5f, 0.6f, 1.5f), tierColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- GravityRigController with scrap debris ----
            var gravityRigGo = new GameObject("GravityRig");
            gravityRigGo.transform.SetParent(arena, false);
            var gravityRig = gravityRigGo.AddComponent<GravityRigController>();
            var gravSo = new SerializedObject(gravityRig);
            gravSo.FindProperty("roundDuration").floatValue = 7f;
            gravSo.ApplyModifiedPropertiesWithoutUndo();

            // Build 6 small scrap debris cubes scattered around the arena.
            var debrisColor = new Color(0.50f, 0.50f, 0.52f); // grey
            var debrisPositions = new Vector3[]
            {
                new Vector3(-2.5f, 1.5f, 5f),
                new Vector3(2.5f, 1.5f, 5.5f),
                new Vector3(-1.5f, 1.5f, 8f),
                new Vector3(1.5f, 1.5f, 8.5f),
                new Vector3(-0.5f, 1.5f, 11f),
                new Vector3(0.5f, 1.5f, 11.5f)
            };

            foreach (var pos in debrisPositions)
            {
                var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.name = "ScrapDebris";
                debris.transform.SetParent(arena, false);
                debris.transform.position = pos;
                debris.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                TintShared(debris.GetComponent<Renderer>(), debrisColor);

                // Primitives have no Rigidbody — add one so the gravity rig can drive it.
                var rb = debris.AddComponent<Rigidbody>();
                gravityRig.RegisterBody(rb); // RegisterBody sets useGravity = false; the rig applies its own force.
            }

            // ---- Dialogue Players ----
            var holdingPensDialogue = BuildEp17DialoguePlayer("Dialogue_HoldingPens", new Vector3(0f, 1.5f, 2f), "holding_pens");
            var hpSo = new SerializedObject(holdingPensDialogue);
            hpSo.FindProperty("playOnStart").boolValue = true;
            hpSo.ApplyModifiedPropertiesWithoutUndo();

            var theOfferDialogue = BuildEp17DialoguePlayer("Dialogue_TheOffer", new Vector3(0f, 1.5f, 12f), "the_offer");

            // ---- 4 waves of enemies (4 bouts) ----
            var arenaBarksDialogue = BuildEp17DialoguePlayer("Dialogue_ArenaBarks", new Vector3(0f, 1.5f, 8f), "arena_barks");
            var waves = new List<List<Health>>();

            // Wave 0: 1 Gilded Maw cyborg (tint Color(0.7,0.6,0.4)).
            var wave0Healths = new List<Health>();
            {
                var enemy = BuildDominionEnemy(new Vector3(0f, 0f, 6f), playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.7f, 0.6f, 0.4f));
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave0Healths.Add(enemy.GetComponent<Health>());
            }
            waves.Add(wave0Healths);

            // Wave 1: 2 Rustfang soldiers (tint Color(0.6,0.4,0.3)).
            var wave1Healths = new List<Health>();
            var wave1Positions = new Vector3[]
            {
                new Vector3(-1f, 0f, 6.5f),
                new Vector3(1f, 0f, 6.5f)
            };
            foreach (var pos in wave1Positions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.6f, 0.4f, 0.3f));
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
            waves.Add(wave1Healths);

            // Wave 2: 1 veteran pit-fighter (tint Color(0.5,0.45,0.5)).
            var wave2Healths = new List<Health>();
            {
                var enemy = BuildDominionEnemy(new Vector3(0f, 0f, 7f), playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.5f, 0.45f, 0.5f));
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
            waves.Add(wave2Healths);

            // Wave 3: 1 veteran pit-fighter (tint Color(0.5,0.45,0.5)).
            var wave3Healths = new List<Health>();
            {
                var enemy = BuildDominionEnemy(new Vector3(0.5f, 0f, 7.5f), playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.5f, 0.45f, 0.5f));
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                wave3Healths.Add(enemy.GetComponent<Health>());
            }
            waves.Add(wave3Healths);

            // Build wave spawner with all 4 waves (barks array reuses same DialoguePlayer for all 4).
            var arenaSpawner = BuildEp03WaveSpawner("ArenaSpawner", new Vector3(0f, 0.5f, 8f), 2f, waves,
                new[] { arenaBarksDialogue, arenaBarksDialogue, arenaBarksDialogue, arenaBarksDialogue });

            // Transition box: "DESCEND — THE LOWER PIT".
            var lowerPitBoxGo = BuildTransitionBox("ToLowerPitBox", new Vector3(0f, 1.2f, 18.5f), "DESCEND — THE LOWER PIT",
                out var lowerPitBtn, out var lowerPitTransition);
            var lpbSo = new SerializedObject(lowerPitTransition);
            lpbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep17LowerPitSceneName;
            lpbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(lowerPitBtn.onClick,
                new UnityEngine.Events.UnityAction(lowerPitTransition.LoadOnFootScene));
            lowerPitBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue holding_pens (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Holding Pens";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = holdingPensDialogue;

            // Step 1: DefeatWaves — 4-wave bout sequence.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: 4-Bout Arena Sequence";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = arenaSpawner;

            // Step 2: Dialogue the_offer.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Offer";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = theOfferDialogue;

            // Step 3: Prompt — transition to Lower Pit.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend to Lower Pit";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = lowerPitBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17ArenaScenePath);
            EnsureScenesInBuild(Galaxy3Ep17ArenaScenePath, Galaxy3Ep17LowerPitScenePath);

            Debug.Log($"[Space Samurai] EP17 Arena scene built at {Galaxy3Ep17ArenaScenePath}. " +
                      "Layout: underground gladiator pit with cool grey lighting, spectator tiers, 6 scrap debris with GravityRigController (roundDuration 7s). " +
                      "4 bout waves: (1 cyborg) → (2 soldiers) → (1 veteran) → (1 veteran), all nonLethal. " +
                      "4 steps: holding_pens (auto) → defeat 4 waves (arena_barks bark, 4x) → " +
                      "the_offer dialogue → transition to Lower Pit.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 3/Build EP17 Lower Pit", priority = 172)]
        public static void BuildEp17LowerPit()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lower Pit: dark cold clone-vat horror.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.3f, 0.45f, 0.45f); // dim teal key light
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.25f, 0.25f); // deep teal ambient

            // Dense teal fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.12f, 0.22f, 0.22f);
            RenderSettings.fogDensity = 0.030f;

            // Two dim teal accent lights.
            BuildAccentPointLight("VatLight1", new Vector3(-3f, 2f, 8f),
                new Color(0.25f, 0.50f, 0.55f), intensity: 0.7f, range: 9f);
            BuildAccentPointLight("VatLight2", new Vector3(3f, 2.5f, 12f),
                new Color(0.20f, 0.45f, 0.50f), intensity: 0.65f, range: 8f);

            // ---- Lower pit floor and structure ----
            var pitGo = new GameObject("LowerPit");
            var pit = pitGo.transform;
            var darkFloor = new Color(0.20f, 0.28f, 0.32f);
            var darkerPit = new Color(0.12f, 0.18f, 0.22f);

            // Main lower pit floor.
            BuildFloorCeiling(pit, "LowerPitFloor", new Vector3(0f, 0f, 10f), new Vector3(12f, 0f, 20f), darkFloor, darkerPit);

            // Pit walls.
            BuildWall(pit, "PitWall_W", new Vector3(-6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(pit, "PitWall_E", new Vector3(6f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // ---- 4 clone vat unlit props ----
            var vatColor = new Color(0.3f, 0.9f, 0.8f); // bright teal
            var vatPositions = new Vector3[]
            {
                new Vector3(-3f, 1f, 4f),
                new Vector3(3f, 1f, 6f),
                new Vector3(-2f, 1f, 12f),
                new Vector3(2f, 1f, 14f)
            };

            foreach (var pos in vatPositions)
            {
                var vat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vat.name = "CloneVat";
                Object.DestroyImmediate(vat.GetComponent<Collider>());
                vat.transform.SetParent(pit, false);
                vat.transform.position = pos;
                vat.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                vat.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(vatColor);
            }

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 45f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            // ---- Dialogue Players ----
            var descentDialogue = BuildEp17DialoguePlayer("Dialogue_Descent", new Vector3(0f, 1.5f, 2f), "descent");
            var dSo = new SerializedObject(descentDialogue);
            dSo.FindProperty("playOnStart").boolValue = true;
            dSo.ApplyModifiedPropertiesWithoutUndo();

            var vesperRevealDialogue = BuildEp17DialoguePlayer("Dialogue_VesperReveal", new Vector3(0f, 1.5f, 8f), "vesper_reveal");
            var vaultLocationDialogue = BuildEp17DialoguePlayer("Dialogue_VaultLocation", new Vector3(0f, 1.5f, 14f), "vault_location");

            // ---- 4 shock-staff guard enemies: 1 wave ----
            var guardColor = new Color(0.4f, 0.45f, 0.5f); // teal guard tint
            var guardWavePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 5f),
                new Vector3(1.5f, 0f, 5.5f),
                new Vector3(-1f, 0f, 8f),
                new Vector3(1f, 0f, 8.5f)
            };

            var guardWaveHealths = new List<Health>();
            foreach (var pos in guardWavePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, guardColor);
                var meleeAttacker = enemy.GetComponent<MeleeAttacker>();
                if (meleeAttacker != null)
                {
                    var maSo = new SerializedObject(meleeAttacker);
                    maSo.FindProperty("nonLethalDisable").boolValue = true;
                    maSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                guardWaveHealths.Add(enemy.GetComponent<Health>());
            }

            // Reuse arena_barks dialogue set for lower pit barks (no dedicated set exists).
            var lowerPitBarksDialogue = BuildEp17DialoguePlayer("Dialogue_LowerPitBarks", new Vector3(0f, 1.5f, 8f), "arena_barks");
            var guardSpawner = BuildEp03WaveSpawner("GuardSpawner", new Vector3(0f, 0.5f, 8f), 2f,
                new List<List<Health>> { guardWaveHealths },
                new[] { lowerPitBarksDialogue });

            // Transition box: "CLIMB — TO THE VAULT".
            var vaultBoxGo = BuildTransitionBox("ToVaultBox", new Vector3(0f, 1.2f, 20.5f), "CLIMB — TO THE VAULT",
                out var vaultBtn, out var vaultTransition);
            var vbSo = new SerializedObject(vaultTransition);
            vbSo.FindProperty("onFootScene").stringValue = Galaxy3Ep17VaultSceneName;
            vbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(vaultBtn.onClick,
                new UnityEngine.Events.UnityAction(vaultTransition.LoadOnFootScene));
            vaultBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 5;

            // Step 0: Dialogue descent (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Descent";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = descentDialogue;

            // Step 1: DefeatWaves — 4 shock-staff guards.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Shock-Staff Guards (4)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = guardSpawner;

            // Step 2: Dialogue vesper_reveal.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Vesper Reveal";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = vesperRevealDialogue;

            // Step 3: Dialogue vault_location.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Vault Location";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = vaultLocationDialogue;

            // Step 4: Prompt — transition to Vault.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s4.FindPropertyRelative("label").stringValue = "Prompt: Climb to Vault";
            s4.FindPropertyRelative("promptObject").objectReferenceValue = vaultBoxGo;

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
            EditorSceneManager.SaveScene(scene, Galaxy3Ep17LowerPitScenePath);
            EnsureScenesInBuild(Galaxy3Ep17LowerPitScenePath, Galaxy3Ep17VaultScenePath);

            Debug.Log($"[Space Samurai] EP17 Lower Pit scene built at {Galaxy3Ep17LowerPitScenePath}. " +
                      "Layout: dark cold clone-vat horror with dim teal lighting, dense fog (fogDensity ~0.03), 4 unlit bright-teal clone vats. " +
                      "4 Shock-Staff Guards (teal tint, nonLethal). " +
                      "5 steps: descent (auto) → defeat 4 Shock-Staff Guards (arena_barks bark) → " +
                      "vesper_reveal dialogue → vault_location dialogue → transition to Vault.");
        }
    }
}

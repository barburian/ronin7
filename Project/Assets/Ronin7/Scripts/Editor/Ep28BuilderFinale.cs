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
    /// EP28 "The Harvest Moon Festival" (Galaxy 4) builder for the last three scenes. Aethon-9's festival
    /// conceals a Dominion child-extraction facility; Cipher and Maya burn it down as the failsafe purge spikes.
    /// - Harvest Ceremony: pre-launch bay with Maya ally, Dominion commander tease, augmented operatives,
    ///   and the failsafe erosion pulse mechanism triggered via FailsafeErosionPulse + vignette.
    /// - Breaking Point: mainframe core with Khall comms reveal, lone terminator final stand,
    ///   peak failsafe erosion (vignette at max intensity).
    /// - Moon Rises: SPACE dogfight finale over Aethon-9 (frigate escape encounter);
    ///   EncounterClearedActivator reveals sister_iron dialogue + return box; sets ep28_complete.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and constants
    /// (Galaxy4Ep28*ScenePath/Name, BuildEp28DialoguePlayer, BuildEp28OnFootShell, BuildEp28Npc, FinishEp28Scene)
    /// are declared in Ep28Builder.cs. DO NOT redefine them.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Harvest Ceremony", priority = 298)]
        public static void BuildEp28HarvestCeremony()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Harvest Ceremony: pre-launch bay palette, cold descent, transition to failsafe.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.68f, 0.66f, 0.70f),     // cool pale
                ambient: new Color(0.14f, 0.12f, 0.16f),      // cool dim
                fogColor: new Color(0.26f, 0.24f, 0.30f), fogDensity: 0.014f,
                structureName: "LaunchBay",
                accent1: new Color(0.55f, 0.70f, 0.80f),      // cool teal
                accent2: new Color(0.75f, 0.78f, 0.90f),      // pale cool
                floorLight: new Color(0.50f, 0.48f, 0.52f), floorDark: new Color(0.28f, 0.26f, 0.30f),
                propTint: new Color(0.46f, 0.44f, 0.48f), out _);

            var rig = Object.FindAnyObjectByType<VRRig>();

            // ---- Maya Selene NPC with AllyCombatant ----
            var mayaGo = BuildEp28Npc("Maya Selene", new Vector3(-1.5f, 0f, 4f), new Color(0.70f, 0.65f, 0.58f));
            if (mayaGo != null)
            {
                var allyCombatant = mayaGo.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Failsafe Erosion Pulse: vignette overlay + pulse mechanism ----
            // Create a dark semi-transparent quad parented to the camera for vignette effect.
            var vignetteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vignetteQuad.name = "FailsafeVignette";
            Object.DestroyImmediate(vignetteQuad.GetComponent<Collider>());
            var vignetteRenderer = vignetteQuad.GetComponent<Renderer>();

            // Use Sprites/Default: an always-present URP-compatible alpha-blended shader (same choice
            // as the ray-line material in XRRigBuilder). Respects material.color.a, ZWrite off, Transparent queue.
            var vignetteMaterial = new Material(Shader.Find("Sprites/Default"));
            vignetteMaterial.color = new Color(0f, 0f, 0f, 0f); // Start transparent
            vignetteRenderer.sharedMaterial = vignetteMaterial;

            // Parent to camera and position close.
            if (rig != null && rig.Head != null && rig.Head.GetComponent<Camera>() != null)
            {
                var camera = rig.Head;
                vignetteQuad.transform.SetParent(camera, false);
                vignetteQuad.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                vignetteQuad.transform.localScale = new Vector3(1f, 1f, 0.1f);
            }

            // Add FailsafeErosionPulse to the rig.
            var failsafePulse = rig != null ? rig.gameObject.AddComponent<FailsafeErosionPulse>() : null;
            if (failsafePulse != null)
            {
                var fpSo = new SerializedObject(failsafePulse);
                fpSo.FindProperty("triggerOnStart").boolValue = true;
                fpSo.FindProperty("triggerDelay").floatValue = 4f;
                SetObjectRef(fpSo, "vignetteRenderer", vignetteRenderer);
                SetObjectRef(fpSo, "playerHealth", playerHealth);
                fpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dominion operatives (lethal, augmented) ----
            var operativePositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 11f),
                new Vector3(0.5f, 0f, 11.5f),
                new Vector3(-0.5f, 0f, 13f),
                new Vector3(1.5f, 0f, 13.5f),
            };
            var operativeHealths = new List<Health>();
            foreach (var pos in operativePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.35f, 0.30f, 0.40f)); // dark augmented tint
                enemy.gameObject.SetActive(false);
                operativeHealths.Add(enemy.GetComponent<Health>());
            }

            var operativeSpawner = BuildEp03WaveSpawner("OperativeSpawner", new Vector3(0f, 0.5f, 12f), 2f,
                new List<List<Health>> { operativeHealths },
                new[] { BuildEp28DialoguePlayer("Dialogue_CommanderRecognition", new Vector3(0f, 1.5f, 12f), "commander_recognition") });

            // ---- Dialogue Players ----
            var ceremonyPlanDialogue = BuildEp28DialoguePlayer("Dialogue_CeremonyPlan", new Vector3(0f, 1.5f, 2f), "ceremony_plan");
            var cpSo = new SerializedObject(ceremonyPlanDialogue);
            cpSo.FindProperty("playOnStart").boolValue = true;
            cpSo.ApplyModifiedPropertiesWithoutUndo();

            var mayaFarewellDialogue = BuildEp28DialoguePlayer("Dialogue_MayaFarewell", new Vector3(0f, 1.5f, 15f), "maya_farewell");

            // Transition box: "DESCEND — THE MAINFRAME".
            var mainframeBoxGo = BuildTransitionBox("ToMainframeBox", new Vector3(0f, 1.2f, 21.5f), "DESCEND — THE MAINFRAME",
                out var mainframeBtn, out var mainframeTransition);
            var mbSo = new SerializedObject(mainframeTransition);
            mbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28BreakingPointSceneName;
            mbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(mainframeBtn.onClick,
                new UnityEngine.Events.UnityAction(mainframeTransition.LoadOnFootScene));
            mainframeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Ceremony Plan";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ceremonyPlanDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Augmented Operatives (4, commander_recognition)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = operativeSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: Maya Farewell";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = mayaFarewellDialogue;

            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Descend — The Mainframe";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = mainframeBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28HarvestCeremonyScenePath, Galaxy4Ep28BreakingPointScenePath);

            Debug.Log($"[Space Samurai] EP28 Harvest Ceremony scene built at {Galaxy4Ep28HarvestCeremonyScenePath}. " +
                      "Pre-launch bay (cool pale palette). " +
                      "Maya Selene NPC (warm practical, AllyCombatant ally). 4 augmented Dominion operatives (lethal, dark augmented tint). " +
                      "FailsafeErosionPulse (triggerOnStart=true, delay=4s) + vignette quad overlay on camera. " +
                      "4 steps: ceremony_plan (auto) → defeat 4 operatives (commander_recognition) → maya_farewell → DESCEND — THE MAINFRAME.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Breaking Point", priority = 299)]
        public static void BuildEp28BreakingPoint()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Breaking Point: mainframe core, darkest palette, peak failsafe cascade.
            var playerHealth = BuildEp28OnFootShell(refs, weapon,
                keyLight: new Color(0.60f, 0.58f, 0.64f),     // cool darkest
                ambient: new Color(0.10f, 0.08f, 0.12f),      // deep cool
                fogColor: new Color(0.22f, 0.20f, 0.26f), fogDensity: 0.020f,
                structureName: "MainframeCore",
                accent1: new Color(0.60f, 0.75f, 0.85f),      // bright cool teal
                accent2: new Color(0.80f, 0.82f, 0.95f),      // bright cool pale
                floorLight: new Color(0.50f, 0.48f, 0.52f), floorDark: new Color(0.24f, 0.22f, 0.26f),
                propTint: new Color(0.44f, 0.42f, 0.46f), out _);

            var rig = Object.FindAnyObjectByType<VRRig>();

            // ---- Failsafe Erosion Pulse: peak intensity vignette ----
            // Create vignette quad parented to camera at peak opacity.
            var vignetteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vignetteQuad.name = "FailsafeVignettePeak";
            Object.DestroyImmediate(vignetteQuad.GetComponent<Collider>());
            var vignetteRenderer = vignetteQuad.GetComponent<Renderer>();

            // Use Sprites/Default: an always-present URP-compatible alpha-blended shader (same choice
            // as the ray-line material in XRRigBuilder). Respects material.color.a, ZWrite off, Transparent queue.
            var vignetteMaterial = new Material(Shader.Find("Sprites/Default"));
            vignetteMaterial.color = new Color(0f, 0f, 0f, 0f); // Start transparent, will pulse
            vignetteRenderer.sharedMaterial = vignetteMaterial;

            // Parent to camera and position close.
            if (rig != null && rig.Head != null && rig.Head.GetComponent<Camera>() != null)
            {
                var camera = rig.Head;
                vignetteQuad.transform.SetParent(camera, false);
                vignetteQuad.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                vignetteQuad.transform.localScale = new Vector3(1f, 1f, 0.1f);
            }

            // Add FailsafeErosionPulse to the rig (peak pulse).
            var failsafePulse = rig != null ? rig.gameObject.AddComponent<FailsafeErosionPulse>() : null;
            if (failsafePulse != null)
            {
                var fpSo = new SerializedObject(failsafePulse);
                fpSo.FindProperty("triggerOnStart").boolValue = true;
                fpSo.FindProperty("triggerDelay").floatValue = 2f; // Peak arrives sooner
                SetObjectRef(fpSo, "vignetteRenderer", vignetteRenderer);
                SetObjectRef(fpSo, "playerHealth", playerHealth);
                fpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dominion Terminator (lethal, enhanced) ----
            var terminator = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var terminatorRenderer = terminator.GetComponent<Renderer>();
            if (terminatorRenderer != null) TintShared(terminatorRenderer, new Color(0.25f, 0.25f, 0.35f)); // very dark enhanced
            terminator.gameObject.SetActive(false);
            var terminatorHealth = terminator.GetComponent<Health>();

            var terminatorSpawner = BuildEp03WaveSpawner("TerminatorSpawner", new Vector3(0f, 0.5f, 12f), 1f,
                new List<List<Health>> { new List<Health> { terminatorHealth } },
                new[] { BuildEp28DialoguePlayer("Dialogue_BreakingBarks", new Vector3(0f, 1.5f, 12f), "breaking_barks") });

            // ---- Dialogue Players ----
            var khallConfrontDialogue = BuildEp28DialoguePlayer("Dialogue_KhallConfront", new Vector3(0f, 1.5f, 2f), "khall_confront");
            var kcSo = new SerializedObject(khallConfrontDialogue);
            kcSo.FindProperty("playOnStart").boolValue = true;
            kcSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "ESCAPE — TO THE STARS".
            var escapeBoxGo = BuildTransitionBox("ToEscapeBox", new Vector3(0f, 1.2f, 21.5f), "ESCAPE — TO THE STARS",
                out var escapeBtn, out var escapeTransition);
            var ebSo = new SerializedObject(escapeTransition);
            ebSo.FindProperty("onFootScene").stringValue = Galaxy4Ep28MoonRisesSceneName;
            ebSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(escapeBtn.onClick,
                new UnityEngine.Events.UnityAction(escapeTransition.LoadOnFootScene));
            escapeBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Khall Confront";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = khallConfrontDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Terminator (1, breaking_barks)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = terminatorSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Escape — To The Stars";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = escapeBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp28Scene(scene, Galaxy4Ep28BreakingPointScenePath, Galaxy4Ep28MoonRisesScenePath);

            Debug.Log($"[Space Samurai] EP28 Breaking Point scene built at {Galaxy4Ep28BreakingPointScenePath}. " +
                      "Mainframe core (darkest palette, cool teal + pale accents). " +
                      "Dominion Terminator (lethal, very dark enhanced tint). " +
                      "FailsafeErosionPulse (triggerOnStart=true, delay=2s, peak intensity) + vignette quad overlay. " +
                      "3 steps: khall_confront (auto, Khall comms) → defeat Terminator (breaking_barks) → ESCAPE — TO THE STARS.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP28 Moon Rises", priority = 300)]
        public static void BuildEp28MoonRises()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over Aethon-9 (green agricultural moon below).
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Aethon-9 below: agrarian green moon.
            var aethon9Go = AddUnlitVisual(universe, "Aethon-9", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.35f, 0.55f, 0.30f));
            var aethon9Collider = aethon9Go.GetComponent<Collider>();
            if (aethon9Collider != null) aethon9Collider.isTrigger = true;

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool.
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            // medbay_aftermath: Maya in medbay post-mission, plays on start.
            var medbayDialogue = BuildEp28DialoguePlayer("Dialogue_MedbayAftermath", new Vector3(0f, 1.62f, 0.8f), "medbay_aftermath");
            var mbGo = medbayDialogue.gameObject;
            mbGo.transform.SetParent(cockpit, false);
            mbGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            mbGo.transform.localRotation = Quaternion.identity;
            var mbSo = new SerializedObject(medbayDialogue);
            mbSo.FindProperty("playOnStart").boolValue = true;
            mbSo.ApplyModifiedPropertiesWithoutUndo();

            // sister_iron: revealed when the frigate encounter is cleared.
            var sisterIronDialogue = BuildEp28DialoguePlayer("Dialogue_SisterIron", new Vector3(0f, 1.62f, 0.8f), "sister_iron");
            var sisterGo = sisterIronDialogue.gameObject;
            sisterGo.transform.SetParent(cockpit, false);
            sisterGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            sisterGo.transform.localRotation = Quaternion.identity;
            var sisterSo = new SerializedObject(sisterIronDialogue);
            sisterSo.FindProperty("playOnStart").boolValue = true;
            sisterSo.ApplyModifiedPropertiesWithoutUndo();
            sisterGo.SetActive(false);

            // ---- Enemy Encounter: frigate escape (3-4 ships) ----
            var frigatEncounterGo = new GameObject("FrigateEncounter");
            var frigateEncounter = frigatEncounterGo.AddComponent<GuardEncounter>();
            var feSo = new SerializedObject(frigateEncounter);
            SetObjectRef(feSo, "player", shipCtrl);
            SetObjectRef(feSo, "universe", universe);
            SetObjectRef(feSo, "pool", pool);
            SetObjectRef(feSo, "definition", enemyShipDef);
            feSo.FindProperty("shipCount").intValue = 3;
            feSo.FindProperty("spawnRadius").floatValue = 280f;
            feSo.FindProperty("initialDelay").floatValue = 6f;
            feSo.FindProperty("requiredCompletedScene").stringValue = "";
            feSo.FindProperty("clearedFlag").stringValue = "ep28_frigate_evaded";
            SetObjectRef(feSo, "spawnDialogue", null); // no spawn dialogue
            feSo.ApplyModifiedPropertiesWithoutUndo();

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (ep28_complete only).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep28_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Gate the finale on the frigate fight: EncounterClearedActivator reveals sister_iron + return box.
            var frigateGateGo = new GameObject("FrigateClearedGate");
            var frigateGate = frigateGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(frigateGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = sisterGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep28_frigate_evaded";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of EP28: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep28MoonRisesScenePath);
            EnsureScenesInBuild(Galaxy4Ep28MoonRisesScenePath);

            Debug.Log($"[Space Samurai] EP28 Moon Rises scene built at {Galaxy4Ep28MoonRisesScenePath}. " +
                      "SPACE finale over Aethon-9 (agrarian green moon below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: medbay_aftermath (auto, Maya medbay framing) → 3-ship frigate encounter (ep28_frigate_evaded) → on cleared, " +
                      "EncounterClearedActivator reveals sister_iron dialogue (SISTER-IRON sting) + RETURN — TO THE STARS. " +
                      "Return box wired to CampaignFlagSetter (ep28_complete ONLY, no ally recruit) + ReturnToSpace. " +
                      "GALAXY 4 EP28 FINALE (last scene).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP28 Scenes", priority = 301)]
        public static void BuildAllEp28Scenes()
        {
            BuildEp28LuminousDescent();
            BuildEp28GardenBelow();
            BuildEp28ExtractionTeam();
            BuildEp28UndergroundArchive();
            BuildEp28HarvestCeremony();
            BuildEp28BreakingPoint();
            BuildEp28MoonRises();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP28 scenes built + inputs rewired. GALAXY 4 EP28 COMPLETE.");
        }
    }
}

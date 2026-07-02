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
    /// EP29 "The Unbroken Bond" (Galaxy 4) builder for the last two scenes. The reactor-core catwalk
    /// under attack sees Samurai-4 transform into an ally as she sacrifices herself to contain the meltdown,
    /// freeing Cipher to escape. The intermission SPACE finale over Ketos Prime (a dead moon) plays her
    /// recorded message as the player evades the final Dominion fighter pair.
    /// - Reactor Catwalk: hot reactor-core palette with Samurai-4 ally, Dominion boarders (lethal).
    /// - Intermission: SPACE dogfight finale over Ketos Prime; EncounterClearedActivator reveals
    ///   Samurai-4's recorded message + return box; sets ep29_complete.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class. Shared helpers and constants
    /// (Galaxy4Ep29*ScenePath/Name, BuildEp29DialoguePlayer, BuildEp29OnFootShell, BuildEp29Npc, FinishEp29Scene)
    /// are declared in Ep29Builder.cs. DO NOT redefine them.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Reactor Catwalk", priority = 306)]
        public static void BuildEp29ReactorCatwalk()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Reactor Catwalk: hot reactor-core palette, orange/amber warm accents over dark hull.
            var playerHealth = BuildEp29OnFootShell(refs, weapon,
                keyLight: new Color(0.75f, 0.65f, 0.55f),     // warm amber
                ambient: new Color(0.18f, 0.14f, 0.10f),      // warm dim
                fogColor: new Color(0.32f, 0.26f, 0.20f), fogDensity: 0.015f,
                structureName: "ReactorCatwalk",
                accent1: new Color(1.00f, 0.60f, 0.20f),      // hot orange reactor glow
                accent2: new Color(0.95f, 0.75f, 0.40f),      // warm amber accent
                floorLight: new Color(0.52f, 0.48f, 0.42f), floorDark: new Color(0.26f, 0.22f, 0.16f),
                propTint: new Color(0.48f, 0.42f, 0.36f), out _);

            // ---- Samurai-4 NPC with AllyCombatant ----
            var steelTint = new Color(0.58f, 0.60f, 0.65f); // Cool steel/grey tint
            var samurai4Go = BuildEp29Npc("Samurai-4", new Vector3(-1.5f, 0f, 4f), steelTint);
            if (samurai4Go != null)
            {
                var allyCombatant = samurai4Go.AddComponent<AllyCombatant>();
                var allySo = new SerializedObject(allyCombatant);
                allySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dominion boarders (lethal) in 2 waves of 3 ----
            var waveAPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 9f),
                new Vector3(0.5f, 0f, 9.5f),
                new Vector3(-0.5f, 0f, 11f),
            };
            var waveA = new List<Health>();
            foreach (var pos in waveAPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.32f, 0.40f)); // dark Dominion tint
                enemy.gameObject.SetActive(false);
                waveA.Add(enemy.GetComponent<Health>());
            }

            var waveBPositions = new Vector3[]
            {
                new Vector3(-1f, 0f, 12f),
                new Vector3(1f, 0f, 12.5f),
                new Vector3(0f, 0f, 13.5f),
            };
            var waveB = new List<Health>();
            foreach (var pos in waveBPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, new Color(0.30f, 0.32f, 0.40f)); // dark Dominion tint
                enemy.gameObject.SetActive(false);
                waveB.Add(enemy.GetComponent<Health>());
            }

            var boarderSpawner = BuildEp03WaveSpawner("BoarderSpawner", new Vector3(0f, 0.5f, 11f), 2f,
                new List<List<Health>> { waveA, waveB },
                new DialoguePlayer[0]); // no spawn bark; wrist_sacrifice plays on start

            // ---- Dialogue Players ----
            var wristSacrificeDialogue = BuildEp29DialoguePlayer("Dialogue_WristSacrifice", new Vector3(0f, 1.5f, 2f), "wrist_sacrifice");
            var wsSo = new SerializedObject(wristSacrificeDialogue);
            wsSo.FindProperty("playOnStart").boolValue = true;
            wsSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "LAUNCH — TO THE STARS".
            var launchBoxGo = BuildTransitionBox("ToIntermissionBox", new Vector3(0f, 1.2f, 21.5f), "LAUNCH — TO THE STARS",
                out var launchBtn, out var launchTransition);
            var lbSo = new SerializedObject(launchTransition);
            lbSo.FindProperty("onFootScene").stringValue = Galaxy4Ep29IntermissionSceneName;
            lbSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.LoadOnFootScene));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 3;

            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Wrist Sacrifice";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = wristSacrificeDialogue;

            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s1.FindPropertyRelative("label").stringValue = "DefeatWaves: Dominion Boarders (6, 2 waves)";
            s1.FindPropertyRelative("waveSpawner").objectReferenceValue = boarderSpawner;

            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s2.FindPropertyRelative("label").stringValue = "Prompt: Launch — To The Stars";
            s2.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            FinishEp29Scene(scene, Galaxy4Ep29ReactorCatwalkScenePath, Galaxy4Ep29IntermissionScenePath);

            Debug.Log($"[Space Samurai] EP29 Reactor Catwalk scene built at {Galaxy4Ep29ReactorCatwalkScenePath}. " +
                      "Hot reactor-core palette (warm amber/orange glow). " +
                      "Samurai-4 NPC (steel tint, AllyCombatant ally). 6 Dominion boarders (dark Dominion, lethal) in 2 waves of 3. " +
                      "3 steps: wrist_sacrifice (auto, Kessler + Cipher) → defeat 6 boarders (boarder_attack) → LAUNCH — TO THE STARS.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build EP29 Intermission", priority = 307)]
        public static void BuildEp29Intermission()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: dark void over Ketos Prime (dead moon below).
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

            // Ketos Prime below: dead battered moon, dark grey sphere.
            var ketosPrimeGo = AddUnlitVisual(universe, "Ketos Prime", new Vector3(0f, -800f, 0f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.30f, 0.30f, 0.34f));
            var ketosPrimeCollider = ketosPrimeGo.GetComponent<Collider>();
            if (ketosPrimeCollider != null) ketosPrimeCollider.isTrigger = true;

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
            // escape_reflection: Cipher's reflection on sacrifice (auto on start).
            var escapeReflectionDialogue = BuildEp29DialoguePlayer("Dialogue_EscapeReflection", new Vector3(0f, 1.62f, 0.8f), "escape_reflection");
            var erGo = escapeReflectionDialogue.gameObject;
            erGo.transform.SetParent(cockpit, false);
            erGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            erGo.transform.localRotation = Quaternion.identity;
            var erSo = new SerializedObject(escapeReflectionDialogue);
            erSo.FindProperty("playOnStart").boolValue = true;
            erSo.ApplyModifiedPropertiesWithoutUndo();

            // intermission_packet: Samurai-4's recorded message (revealed when encounter clears).
            var intermissionPacketDialogue = BuildEp29DialoguePlayer("Dialogue_IntermissionPacket", new Vector3(0f, 1.62f, 0.8f), "intermission_packet");
            var ipGo = intermissionPacketDialogue.gameObject;
            ipGo.transform.SetParent(cockpit, false);
            ipGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            ipGo.transform.localRotation = Quaternion.identity;
            var ipSo = new SerializedObject(intermissionPacketDialogue);
            ipSo.FindProperty("playOnStart").boolValue = true;
            ipSo.ApplyModifiedPropertiesWithoutUndo();
            ipGo.SetActive(false);

            // ---- Enemy Encounter: 2-ship finale (Dominion fighters) ----
            var fighterEncounterGo = new GameObject("FighterEncounter");
            var fighterEncounter = fighterEncounterGo.AddComponent<GuardEncounter>();
            var feSo = new SerializedObject(fighterEncounter);
            SetObjectRef(feSo, "player", shipCtrl);
            SetObjectRef(feSo, "universe", universe);
            SetObjectRef(feSo, "pool", pool);
            SetObjectRef(feSo, "definition", enemyShipDef);
            feSo.FindProperty("shipCount").intValue = 2;
            feSo.FindProperty("spawnRadius").floatValue = 280f;
            feSo.FindProperty("initialDelay").floatValue = 6f;
            feSo.FindProperty("requiredCompletedScene").stringValue = "";
            feSo.FindProperty("clearedFlag").stringValue = "ep29_fighters_cleared";
            SetObjectRef(feSo, "spawnDialogue", null); // no spawn dialogue
            feSo.ApplyModifiedPropertiesWithoutUndo();

            // Finale return box: "RETURN — TO THE STARS" with CampaignFlagSetter (ep29_complete only, no recruit).
            var returnBoxGo = BuildTransitionBox("ReturnStarsBox", new Vector3(0f, 1.2f, 0.8f), "RETURN — TO THE STARS",
                out var returnBtn, out var returnTransition);
            returnBoxGo.SetActive(false);

            var finaleFlagSetter = returnBoxGo.AddComponent<CampaignFlagSetter>();
            var finaleFsSo = new SerializedObject(finaleFlagSetter);
            var finaleFlagsProp = finaleFsSo.FindProperty("flags");
            finaleFlagsProp.arraySize = 1;
            finaleFlagsProp.GetArrayElementAtIndex(0).stringValue = "ep29_complete";
            finaleFsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(finaleFlagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.ReturnToSpace));

            // Gate the finale on the fighter encounter: EncounterClearedActivator reveals intermission_packet + return box.
            var fighterGateGo = new GameObject("FighterClearedGate");
            var fighterGate = fighterGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(fighterGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 2;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = ipGo;
            activateProp.GetArrayElementAtIndex(1).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("clearedFlag").stringValue = "ep29_fighters_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene (LAST scene of EP29: no next scene registered).
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4Ep29IntermissionScenePath);
            EnsureScenesInBuild(Galaxy4Ep29IntermissionScenePath);

            Debug.Log($"[Space Samurai] EP29 Intermission scene built at {Galaxy4Ep29IntermissionScenePath}. " +
                      "SPACE finale over Ketos Prime (battered dead moon below). Cockpit (canopy + HUD + EnemyWarning). " +
                      "Flow: escape_reflection (auto, Cipher's reflection on sacrifice) → 2-ship fighter encounter (ep29_fighters_cleared) → on cleared, " +
                      "EncounterClearedActivator reveals intermission_packet (Samurai-4's recorded message) + RETURN — TO THE STARS. " +
                      "Return box wired to CampaignFlagSetter (ep29_complete ONLY, no ally recruit) + ReturnToSpace. " +
                      "GALAXY 4 EP29 FINALE (last scene).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 4/Build All EP29 Scenes", priority = 308)]
        public static void BuildAllEp29Scenes()
        {
            BuildEp29SisterSignal();
            BuildEp29ZeroChamber();
            BuildEp29Neurovault();
            BuildEp29ScanConsole();
            BuildEp29ReactorCatwalk();
            BuildEp29Intermission();
            RewireAllScenes();
            Debug.Log("[Space Samurai] All EP29 scenes built + inputs rewired. GALAXY 4 EP29 COMPLETE.");
        }
    }
}

using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.Ship;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Galaxy 4 solar-system builder (EP25 sessions). Builds the sterile/cool-palette hub for the
    /// "Reckoning" arc with 8 canon worlds: Sable Drift (landable), Pale Reliquary, The Hollow Choir,
    /// Aethon-9, Severance Reach, The Vault, Mirelle, and Throne of Ashes.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths for Galaxy 4 and its connected episode scenes (EP25).
        private const string Galaxy4ScenePath = SceneFolder + "/Galaxy4.unity";
        private static readonly string Galaxy4SceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy4ScenePath);

        /// <summary>
        /// A swirling wormhole the player flies through to reach the Galaxy 4 hub from Galaxy 3.
        /// Restyled from the Galaxy 3 wormhole: concentric glowing event-horizon discs in a crimson/gold
        /// "Reckoning" palette, an outer accretion ring, and a bright beacon. Activation is gated by EP24
        /// completion; this method only builds the visual + beacon. Returns the root GameObject.
        /// </summary>
        private static GameObject BuildGalaxy4Wormhole(Transform universe, Vector3 localPos)
        {
            var root = new GameObject("Galaxy 4 Wormhole");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = localPos;

            var crimson = new Color(0.85f, 0.25f, 0.30f);
            var gold = new Color(0.95f, 0.7f, 0.3f);

            // Outer accretion ring: 16 tangent cube segments standing vertically around a circle.
            int segments = 16;
            float ringRadius = 14f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f);
                var seg = AddVisualTinted(root.transform, $"RingSegment{i}", pos,
                    new Vector3(5f, 1.2f, 1.2f), PrimitiveType.Cube, crimson);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }

            // Concentric event-horizon discs, flattened on Z, each with increasing transparency toward the bright core.
            // Outer disc: crimson, low alpha.
            var outerDisc = AddUnlitVisual(root.transform, "EventHorizon_Outer", Vector3.zero,
                new Vector3(22f, 22f, 0.2f), PrimitiveType.Sphere, crimson);
            outerDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(crimson, 0.12f);

            // Middle disc: gold, medium alpha.
            var midDisc = AddUnlitVisual(root.transform, "EventHorizon_Middle", Vector3.zero,
                new Vector3(15f, 15f, 0.2f), PrimitiveType.Sphere, gold);
            midDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(gold, 0.2f);

            // Inner disc: bright warm gold, higher alpha to read as a glowing throat.
            var innerDisc = AddUnlitVisual(root.transform, "EventHorizon_Inner", Vector3.zero,
                new Vector3(8f, 8f, 0.2f), PrimitiveType.Sphere, new Color(1f, 0.85f, 0.6f));
            innerDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(new Color(1f, 0.85f, 0.6f), 0.35f);

            // Bright gold beacon so it reads from across the system.
            var beaconGo = new GameObject("WormholeBeacon");
            beaconGo.transform.SetParent(root.transform, false);
            var beacon = beaconGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = gold;
            beacon.intensity = 3f;
            beacon.range = 80f;
            beacon.shadows = LightShadows.None;

            return root;
        }

        /// <summary>
        /// Encloses the Galaxy 4 cockpit in a solid cabin with a front windshield and adds briefings
        /// wired to a BriefingSelector. Trimmed single-episode cabin with physical shell from Galaxy3
        /// and two briefings (pre-mission + post-mission EP25).
        /// </summary>
        private static void BuildGalaxy4Cabin(Transform cockpit)
        {
            var hull = new Color(0.16f, 0.17f, 0.20f);
            var floorC = new Color(0.13f, 0.14f, 0.17f);

            // Cabin shell: floor, ceiling, back wall, side walls.
            AddVisualTinted(cockpit, "CabinFloor", new Vector3(0f, -0.05f, -0.4f), new Vector3(3.2f, 0.1f, 3.4f), PrimitiveType.Cube, floorC);
            AddVisualTinted(cockpit, "CabinCeiling", new Vector3(0f, 2.45f, -0.4f), new Vector3(3.2f, 0.1f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinBack", new Vector3(0f, 1.2f, -2.05f), new Vector3(3.2f, 2.6f, 0.1f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallL", new Vector3(-1.55f, 1.2f, -0.4f), new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallR", new Vector3(1.55f, 1.2f, -0.4f), new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);

            // Sleek wraparound glass-bubble canopy (slim dark frame + cyan neon edge-light).
            BuildGlassCanopyFrame(cockpit, hull, 1.55f);

            // Cabin light.
            BuildAccentPointLight("CabinLight", new Vector3(0f, 2.2f, -0.3f),
                new Color(1f, 0.88f, 0.7f), intensity: 2.0f, range: 6f);

            // Enemy warning system.
            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Pre-mission briefing (EP25 space briefing from Ep25Lines).
            var preMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep25Briefing", Vector3.zero,
                Ep25Lines.Get("lead_intro"), null, "lead_intro", "ep25");
            var briefingGo = preMissionDialogue.gameObject;
            briefingGo.transform.SetParent(cockpit, false);
            briefingGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            briefingGo.transform.localRotation = Quaternion.identity;

            var textT = briefingGo.transform.Find("Text");
            if (textT != null)
            {
                textT.localPosition = new Vector3(0f, 0f, 0.02f);
                textT.localScale = Vector3.one * 0.0045f;
            }
            var panelT = briefingGo.transform.Find("Panel");
            if (panelT != null)
            {
                panelT.localPosition = Vector3.zero;
                var bg = panelT.Find("Background");
                if (bg != null) bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var dpSo = new SerializedObject(preMissionDialogue);
            dpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            dpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (EP25 space outro from Ep25Lines).
            var postMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep25Post", Vector3.zero,
                Ep25Lines.Get("space_ep25_post"), null, "space_ep25_post", "ep25");
            var postMissionGo = postMissionDialogue.gameObject;
            postMissionGo.transform.SetParent(cockpit, false);
            postMissionGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMissionGo.transform.localRotation = Quaternion.identity;

            var postTextT = postMissionGo.transform.Find("Text");
            if (postTextT != null)
            {
                postTextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postTextT.localScale = Vector3.one * 0.0045f;
            }
            var postPanelT = postMissionGo.transform.Find("Panel");
            if (postPanelT != null)
            {
                postPanelT.localPosition = Vector3.zero;
                var postBg = postPanelT.Find("Background");
                if (postBg != null) postBg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postDpSo = new SerializedObject(postMissionDialogue);
            postDpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postDpSo.ApplyModifiedPropertiesWithoutUndo();

            // NOTE: EP26 has no pre-mission cabin briefing — BriefingSelector exposes a single preMission
            // slot (used by EP25's lead_intro). The EP26 briefing (contract_intro) plays as the auto
            // dialogue at the start of the Ashen Deep scene instead. Post-mission EP26 recap is below.

            // Post-mission briefing (EP26 space outro from Ep26Lines).
            var postMission2Dialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep26Post", Vector3.zero,
                Ep26Lines.Get("space_ep26_post"), null, "space_ep26_post", "ep26");
            var postMission2Go = postMission2Dialogue.gameObject;
            postMission2Go.transform.SetParent(cockpit, false);
            postMission2Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission2Go.transform.localRotation = Quaternion.identity;

            var postM2TextT = postMission2Go.transform.Find("Text");
            if (postM2TextT != null)
            {
                postM2TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postM2TextT.localScale = Vector3.one * 0.0045f;
            }
            var postM2PanelT = postMission2Go.transform.Find("Panel");
            if (postM2PanelT != null)
            {
                postM2PanelT.localPosition = Vector3.zero;
                var postM2Bg = postM2PanelT.Find("Background");
                if (postM2Bg != null) postM2Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postM2DpSo = new SerializedObject(postMission2Dialogue);
            postM2DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postM2DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (EP27 space outro from Ep27Lines).
            var postMission3Dialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep27Post", Vector3.zero,
                Ep27Lines.Get("space_ep27_post"), null, "space_ep27_post", "ep27");
            var postMission3Go = postMission3Dialogue.gameObject;
            postMission3Go.transform.SetParent(cockpit, false);
            postMission3Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission3Go.transform.localRotation = Quaternion.identity;

            var postM3TextT = postMission3Go.transform.Find("Text");
            if (postM3TextT != null)
            {
                postM3TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postM3TextT.localScale = Vector3.one * 0.0045f;
            }
            var postM3PanelT = postMission3Go.transform.Find("Panel");
            if (postM3PanelT != null)
            {
                postM3PanelT.localPosition = Vector3.zero;
                var postM3Bg = postM3PanelT.Find("Background");
                if (postM3Bg != null) postM3Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postM3DpSo = new SerializedObject(postMission3Dialogue);
            postM3DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postM3DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (EP28 space outro from Ep28Lines).
            var postMission4Dialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep28Post", Vector3.zero,
                Ep28Lines.Get("space_ep28_post"), null, "space_ep28_post", "ep28");
            var postMission4Go = postMission4Dialogue.gameObject;
            postMission4Go.transform.SetParent(cockpit, false);
            postMission4Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission4Go.transform.localRotation = Quaternion.identity;

            var postM4TextT = postMission4Go.transform.Find("Text");
            if (postM4TextT != null)
            {
                postM4TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postM4TextT.localScale = Vector3.one * 0.0045f;
            }
            var postM4PanelT = postMission4Go.transform.Find("Panel");
            if (postM4PanelT != null)
            {
                postM4PanelT.localPosition = Vector3.zero;
                var postM4Bg = postM4PanelT.Find("Background");
                if (postM4Bg != null) postM4Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postM4DpSo = new SerializedObject(postMission4Dialogue);
            postM4DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postM4DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (EP30 space outro from Ep30Lines).
            var postMission5Dialogue = BuildDialoguePlayer("Dialogue_Galaxy4Ep30Post", Vector3.zero,
                Ep30Lines.Get("space_ep30_post"), null, "space_ep30_post", "ep30");
            var postMission5Go = postMission5Dialogue.gameObject;
            postMission5Go.transform.SetParent(cockpit, false);
            postMission5Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission5Go.transform.localRotation = Quaternion.identity;

            var postM5TextT = postMission5Go.transform.Find("Text");
            if (postM5TextT != null)
            {
                postM5TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postM5TextT.localScale = Vector3.one * 0.0045f;
            }
            var postM5PanelT = postMission5Go.transform.Find("Panel");
            if (postM5PanelT != null)
            {
                postM5PanelT.localPosition = Vector3.zero;
                var postM5Bg = postM5PanelT.Find("Background");
                if (postM5Bg != null) postM5Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postM5DpSo = new SerializedObject(postMission5Dialogue);
            postM5DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postM5DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire the BriefingSelector: preMission + postMission + planet scenes, with EP26/EP27/EP28/EP30 extensions.
            var selectorGo = new GameObject("BriefingSelector");
            selectorGo.transform.SetParent(cockpit, false);
            var selector = selectorGo.AddComponent<BriefingSelector>();
            var selSo = new SerializedObject(selector);
            SetObjectRef(selSo, "preMission", preMissionDialogue);
            SetObjectRef(selSo, "postMission", postMissionDialogue);
            selSo.FindProperty("planetScene").stringValue = Galaxy4Ep25CargoShelfSceneName;
            SetObjectRef(selSo, "postMission2", postMission2Dialogue);
            selSo.FindProperty("planetScene2").stringValue = Galaxy4Ep26AshenDeepSceneName;
            SetObjectRef(selSo, "postMission3", postMission3Dialogue);
            selSo.FindProperty("planetScene3").stringValue = Galaxy4Ep27BoneGatesSceneName;
            SetObjectRef(selSo, "postMission4", postMission4Dialogue);
            selSo.FindProperty("planetScene4").stringValue = Galaxy4Ep28LuminousDescentSceneName;
            SetObjectRef(selSo, "postMission5", postMission5Dialogue);
            selSo.FindProperty("planetScene5").stringValue = Galaxy4Ep30PriceOfMemorySceneName;
            selSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds the Galaxy 4 SPACE flight scene: the sterile/cool-palette hub for EP25 ("Reckoning"),
        /// orbiting 8 canon worlds with Sable Drift (hidden in asteroid field) as the landable EP25
        /// destination. The Corsair is dockable as a fallback. This is the final galaxy hub.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 4/Build Galaxy 4 Space Scene", priority = 80)]
        public static void BuildGalaxy4Scene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sterile/cool-tinted space with cold ambient lighting.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.028f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig, no locomotion.
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
            BuildCockpit(cockpit);

            var recenter = cockpit.gameObject.AddComponent<CockpitRecenter>();
            var rcSo = new SerializedObject(recenter);
            SetObjectRef(rcSo, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
            SetObjectRef(rcSo, "head", vrRig != null ? vrRig.Head : null);
            SetObjectRef(rcSo, "cockpit", cockpit);
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            // Ship hull selector (same as Galaxy1/2/3).
            var hullSelector = cockpit.gameObject.AddComponent<Ronin7.Ship.ShipHullSelector>();
            var hullPaths = ArtPrefabBuilder.ShipHullPrefabPaths;
            var hullObjs = new GameObject[hullPaths.Length];
            int hullsFound = 0;
            for (int i = 0; i < hullPaths.Length; i++)
            {
                hullObjs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(hullPaths[i]);
                if (hullObjs[i] != null) hullsFound++;
            }

            var hsSo = new SerializedObject(hullSelector);
            var arr = hsSo.FindProperty("hullPrefabs");
            arr.arraySize = hullObjs.Length;
            for (int i = 0; i < hullObjs.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = hullObjs[i];
            hsSo.ApplyModifiedPropertiesWithoutUndo();

            if (hullsFound == hullPaths.Length)
                Debug.Log($"[Space Samurai] Wired {hullsFound}/{hullPaths.Length} ship hull prefabs onto the cockpit.");

            // Build the Galaxy 4 cabin with EP25 briefings.
            BuildGalaxy4Cabin(cockpit);

            // Universe: ember sun + 8 canon worlds.
            var universe = new GameObject("Universe").transform;
            Vector3 sunCenter = new Vector3(0f, 0f, 1500f);
            var sunVisual = BuildEmberSun(universe, light, sunCenter, 400f);

            var aimer = lightGo.AddComponent<SunLightAimer>();
            var aimerSo = new SerializedObject(aimer);
            SetObjectRef(aimerSo, "sunLight", light);
            SetObjectRef(aimerSo, "sunVisual", sunVisual.transform);
            aimerSo.ApplyModifiedPropertiesWithoutUndo();

            // World 1: Sable Drift — the landable anchor planet for EP25, dark rocky in asteroid field at 850u orbit.
            Vector3 sableDriftPos = OrbitPos(sunCenter, 850f, 0f, 0f);
            var sableDrift = BuildCanonPlanet(universe, "Sable Drift",
                sableDriftPos, 110f, new Color(0.30f, 0.30f, 0.34f), "Rocky");
            AddSurfacePatches(sableDrift, 5, new Color(0.20f, 0.20f, 0.24f), 64000001);
            AddPlanetOrbit(sableDrift, sunVisual.transform, 850f, 0.22f, 0f, 0f);
            // Sable Drift Asteroid Ring.
            BuildAsteroidRing(universe, sableDriftPos, 170f, 38f, 28f, 48, scaleMul: 6f);

            // World 2: Pale Reliquary — bone-white rocky at 1150u orbit.
            Vector3 paleReliquaryPos = OrbitPos(sunCenter, 1150f, 40f, 0f);
            var paleReliquary = BuildCanonPlanet(universe, "Pale Reliquary",
                paleReliquaryPos, 120f, new Color(0.82f, 0.80f, 0.74f), "Rocky");
            AddCityLights(paleReliquary, 6, new Color(0.9f, 0.85f, 0.7f), 64000002);
            AddPlanetOrbit(paleReliquary, sunVisual.transform, 1150f, 0.15f, 40f, 0f);

            // World 3: The Hollow Choir — abyssal blue-black ice at 1400u orbit.
            Vector3 hollowChoirPos = OrbitPos(sunCenter, 1400f, 120f, 0f);
            var hollowChoir = BuildCanonPlanet(universe, "The Hollow Choir",
                hollowChoirPos, 120f, new Color(0.12f, 0.20f, 0.30f), "Ice");
            AddCloudShell(hollowChoir, new Color(0.3f, 0.5f, 0.7f), 0.3f);
            AddPlanetOrbit(hollowChoir, sunVisual.transform, 1400f, 0.12f, 120f, 0f);

            // World 4: Aethon-9 — agrarian green rocky at 1650u orbit.
            Vector3 aethon9Pos = OrbitPos(sunCenter, 1650f, 200f, 0f);
            var aethon9 = BuildCanonPlanet(universe, "Aethon-9",
                aethon9Pos, 120f, new Color(0.35f, 0.55f, 0.30f), "Rocky");
            AddCityLights(aethon9, 12, new Color(0.6f, 1f, 0.5f), 64000003);
            AddPlanetOrbit(aethon9, sunVisual.transform, 1650f, 0.10f, 200f, 0f);

            // World 5: Ketos Prime — battered dead-moon station, dark grey rocky at 1900u orbit (EP29).
            Vector3 ketosPrimePos = OrbitPos(sunCenter, 1900f, 280f, 0f);
            var ketosPrime = BuildCanonPlanet(universe, "Ketos Prime",
                ketosPrimePos, 110f, new Color(0.30f, 0.30f, 0.34f), "Rocky");
            AddSurfacePatches(ketosPrime, 5, new Color(0.20f, 0.20f, 0.24f), 64000004);
            AddPlanetOrbit(ketosPrime, sunVisual.transform, 1900f, 0.08f, 280f, 0f);

            // World 5: Severance Reach — grey rocky at 1850u orbit.
            Vector3 severanceReachPos = OrbitPos(sunCenter, 1850f, 260f, 0f);
            var severanceReach = BuildCanonPlanet(universe, "Severance Reach",
                severanceReachPos, 100f, new Color(0.45f, 0.42f, 0.48f), "Rocky");
            AddSurfacePatches(severanceReach, 4, new Color(0.30f, 0.28f, 0.33f), 64000004);
            AddPlanetOrbit(severanceReach, sunVisual.transform, 1850f, 0.08f, 260f, 0f);

            // World 6: The Vault — dark obsidian rocky at 2100u orbit.
            Vector3 theVaultPos = OrbitPos(sunCenter, 2100f, 330f, 0f);
            var theVault = BuildCanonPlanet(universe, "The Vault",
                theVaultPos, 90f, new Color(0.18f, 0.18f, 0.22f), "Rocky");
            AddCityLights(theVault, 5, new Color(0.5f, 0.6f, 1f), 64000005);
            AddPlanetOrbit(theVault, sunVisual.transform, 2100f, 0.06f, 330f, 0f);

            // World 7: Mirelle — desert ochre rocky at 2350u orbit.
            Vector3 mirellePos = OrbitPos(sunCenter, 2350f, 380f, 0f);
            var mirelle = BuildCanonPlanet(universe, "Mirelle",
                mirellePos, 110f, new Color(0.78f, 0.62f, 0.40f), "Rocky");
            AddPolarCaps(mirelle, new Color(0.9f, 0.85f, 0.7f));
            AddPlanetOrbit(mirelle, sunVisual.transform, 2350f, 0.05f, 380f, 0f);

            // World 8: Throne of Ashes — ash-crimson rocky at 2600u orbit.
            Vector3 throneOfAshesPos = OrbitPos(sunCenter, 2600f, 440f, 0f);
            var throneOfAshes = BuildCanonPlanet(universe, "Throne of Ashes",
                throneOfAshesPos, 130f, new Color(0.30f, 0.10f, 0.12f), "Rocky");
            AddCityLights(throneOfAshes, 16, new Color(1f, 0.4f, 0.3f), 64000006);
            AddPlanetOrbit(throneOfAshes, sunVisual.transform, 2600f, 0.04f, 440f, 0f);

            // World 9: Ashen Deep — volcanic moon for EP26, dark red-grey rocky at 1100u orbit.
            Vector3 ashenDeepPos = OrbitPos(sunCenter, 1100f, 280f, 0f);
            var ashenDeep = BuildCanonPlanet(universe, "Ashen Deep",
                ashenDeepPos, 110f, new Color(0.35f, 0.20f, 0.16f), "Rocky");
            AddSurfacePatches(ashenDeep, 6, new Color(0.25f, 0.15f, 0.12f), 64000007);
            AddPlanetOrbit(ashenDeep, sunVisual.transform, 1100f, 0.18f, 280f, 0f);

            // World 10: Chronus Prime — desert world in a time loop, landable EP31, ochre rocky at 1300u orbit.
            Vector3 chronusPrimePos = OrbitPos(sunCenter, 1300f, 160f, 0f);
            var chronusPrime = BuildCanonPlanet(universe, "Chronus Prime",
                chronusPrimePos, 110f, new Color(0.78f, 0.62f, 0.40f), "Rocky");
            AddPolarCaps(chronusPrime, new Color(0.9f, 0.85f, 0.7f));
            AddPlanetOrbit(chronusPrime, sunVisual.transform, 1300f, 0.14f, 160f, 0f);

            // World 11: The Obsidian Synod — Dominion seat of power, landable EP33 (series finale),
            // near-black iron fortress-world over a dying star at 2850u orbit.
            Vector3 obsidianSynodPos = OrbitPos(sunCenter, 2850f, 500f, 0f);
            var obsidianSynod = BuildCanonPlanet(universe, "The Obsidian Synod",
                obsidianSynodPos, 140f, new Color(0.10f, 0.10f, 0.13f), "Rocky");
            AddCityLights(obsidianSynod, 20, new Color(0.6f, 0.5f, 1f), 64000008);
            AddPlanetOrbit(obsidianSynod, sunVisual.transform, 2850f, 0.03f, 500f, 0f);

            // Starfield dome.
            BuildStarfield(null, 5000f, 2200);

            // The Corsair: parked at (250, 20, 350), dockable as fallback.
            var corsair = BuildCorsairExterior(universe, new Vector3(250f, 20f, 350f), 6f);

            // Asteroid hazard.
            var hazardGo = new GameObject("Asteroid Hazard");
            var hazard = hazardGo.AddComponent<AsteroidHazard>();
            hazard.Configure(rig.GetComponent<Health>(), hullCol.radius);

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var shipSo = new SerializedObject(shipCtrl);
            SetObjectRef(shipSo, "universe", universe);
            SetObjectRef(shipSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(shipSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            shipSo.FindProperty("maxSpeed").floatValue = 90f;
            shipSo.FindProperty("acceleration").floatValue = 20f;
            shipSo.ApplyModifiedPropertiesWithoutUndo();

            // Projectile pool.
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

            BuildCockpitCrosshair(cockpit, guns);

            // Windshield waypoint arrow.
            var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowGo.name = "Planet Waypoint Arrow";
            Object.DestroyImmediate(arrowGo.GetComponent<Collider>());
            arrowGo.transform.SetParent(cockpit, false);
            arrowGo.transform.localPosition = new Vector3(0f, 1.35f, 1.2f);
            arrowGo.transform.localScale = new Vector3(0.06f, 0.06f, 0.25f);
            TintShared(arrowGo.GetComponent<Renderer>(), new Color(0.3f, 0.8f, 1f));
            var marker = arrowGo.AddComponent<Ronin7.Ship.PlanetTargetMarker>();
            var mSo = new SerializedObject(marker);
            SetObjectRef(mSo, "arrow", arrowGo.transform);
            SetObjectRef(mSo, "target", sableDrift != null ? sableDrift.transform : null);
            SetObjectRef(mSo, "universe", universe);
            SetObjectRef(mSo, "arrowRenderer", arrowGo.GetComponent<Renderer>());
            mSo.FindProperty("fadeStartRange").floatValue = 3000f;
            mSo.FindProperty("arriveRange").floatValue = 150f;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var arrowCtrl = arrowGo.AddComponent<Ronin7.Ship.ObjectiveArrowController>();
            var acSo = new SerializedObject(arrowCtrl);
            SetObjectRef(acSo, "marker", marker);
            SetObjectRef(acSo, "defaultObjective", sableDrift != null ? sableDrift.transform : null);
            acSo.ApplyModifiedPropertiesWithoutUndo();

            // Campaign Objective Selector: 4 entries (EP25 + EP26 + EP27 + EP28).
            var selectorGo = new GameObject("Campaign Objective Selector");
            selectorGo.transform.SetParent(arrowGo.transform, false);
            var selector = selectorGo.AddComponent<CampaignObjectiveSelector>();
            var selSo = new SerializedObject(selector);
            var entriesProp = selSo.FindProperty("entries");
            entriesProp.arraySize = 9;
            // Entry 0: Sable Drift (always available at start, EP25)
            var entry0 = entriesProp.GetArrayElementAtIndex(0);
            entry0.FindPropertyRelative("requiredCompletedScene").stringValue = "";
            entry0.FindPropertyRelative("objective").objectReferenceValue = sableDrift != null ? sableDrift.transform : null;
            // Entry 1: Ashen Deep (gated on EP25 finale, EP26)
            var entry1 = entriesProp.GetArrayElementAtIndex(1);
            entry1.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep25PurposeUntoldSceneName;
            entry1.FindPropertyRelative("objective").objectReferenceValue = ashenDeep != null ? ashenDeep.transform : null;
            // Entry 2: The Hollow Choir (gated on EP26 finale, EP27)
            var entry2 = entriesProp.GetArrayElementAtIndex(2);
            entry2.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep26CargoBaySceneName;
            entry2.FindPropertyRelative("objective").objectReferenceValue = hollowChoir != null ? hollowChoir.transform : null;
            // Entry 3: Aethon-9 (gated on EP27 finale, EP28)
            var entry3 = entriesProp.GetArrayElementAtIndex(3);
            entry3.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep27BoneFleetSceneName;
            entry3.FindPropertyRelative("objective").objectReferenceValue = aethon9 != null ? aethon9.transform : null;
            // Entry 4: Ketos Prime (gated on EP28 finale, EP29)
            var entry4 = entriesProp.GetArrayElementAtIndex(4);
            entry4.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep28MoonRisesSceneName;
            entry4.FindPropertyRelative("objective").objectReferenceValue = ketosPrime != null ? ketosPrime.transform : null;
            // Entry 5: The Vault / Iron Sepulcher (gated on EP29 finale, EP30)
            var entry5 = entriesProp.GetArrayElementAtIndex(5);
            entry5.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep29IntermissionSceneName;
            entry5.FindPropertyRelative("objective").objectReferenceValue = theVault != null ? theVault.transform : null;
            // Entry 6: Chronus Prime (gated on EP30 finale, EP31)
            var entry6 = entriesProp.GetArrayElementAtIndex(6);
            entry6.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep30NameRemainsSceneName;
            entry6.FindPropertyRelative("objective").objectReferenceValue = chronusPrime != null ? chronusPrime.transform : null;
            // Entry 7: Throne of Ashes (gated on EP31 finale, EP32 — Galaxy 4 closer)
            var entry7 = entriesProp.GetArrayElementAtIndex(7);
            entry7.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep31OneDaySceneName;
            entry7.FindPropertyRelative("objective").objectReferenceValue = throneOfAshes != null ? throneOfAshes.transform : null;
            // Entry 8: The Obsidian Synod (gated on EP32 finale, EP33 — SERIES FINALE)
            var entry8 = entriesProp.GetArrayElementAtIndex(8);
            entry8.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy4Ep32ThresholdSceneName;
            entry8.FindPropertyRelative("objective").objectReferenceValue = obsidianSynod != null ? obsidianSynod.transform : null;
            SetObjectRef(selSo, "arrow", arrowCtrl);
            selSo.ApplyModifiedPropertiesWithoutUndo();

            // Space Encounter Manager (same settings as Galaxy3).
            var encounterGo = new GameObject("Space Encounters");
            var encounter = encounterGo.AddComponent<SpaceEncounterManager>();
            var encSo = new SerializedObject(encounter);
            SetObjectRef(encSo, "player", shipCtrl);
            SetObjectRef(encSo, "universe", universe);
            SetObjectRef(encSo, "pool", pool);
            SetObjectRef(encSo, "definition", enemyShipDef);
            encSo.FindProperty("requireFirstPlanetDeparture").boolValue = true;
            encSo.FindProperty("waveCount").intValue = 1;
            encSo.FindProperty("enemiesPerWave").intValue = 2;
            encSo.FindProperty("enemiesAddedPerWave").intValue = 0;
            encSo.FindProperty("enemiesAddedPerGalaxy").intValue = 2;
            encSo.FindProperty("interWaveDelay").floatValue = 6f;
            encSo.FindProperty("initialDelay").floatValue = 5f;
            encSo.FindProperty("travelBetweenWaves").floatValue = 400f;
            encSo.FindProperty("spawnDistance").floatValue = 260f;
            encSo.ApplyModifiedPropertiesWithoutUndo();

            // NO GuardEncounter for Galaxy 4 (kept simple for EP25).

            // Landing approach: 4 landable targets (Sable Drift primary EP25, Ashen Deep gated EP26, The Hollow Choir gated EP27, Aethon-9 gated EP28, Corsair fallback).
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            lso.FindProperty("firstPlanetScene").stringValue = Galaxy4Ep25CargoShelfSceneName;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 10;
                // Sable Drift: primary EP25 landing (no requirement).
                SetLandable(landables, 0, sableDrift, 90f, Galaxy4Ep25CargoShelfSceneName);
                // Ashen Deep: EP26 landing, gated on EP25 finale completion.
                SetLandable(landables, 1, ashenDeep, 90f, Galaxy4Ep26AshenDeepSceneName, Galaxy4Ep25PurposeUntoldSceneName);
                // The Hollow Choir: EP27 landing, gated on EP26 finale completion.
                SetLandable(landables, 2, hollowChoir, 90f, Galaxy4Ep27BoneGatesSceneName, Galaxy4Ep26CargoBaySceneName);
                // Aethon-9: EP28 landing, gated on EP27 finale completion.
                SetLandable(landables, 3, aethon9, 90f, Galaxy4Ep28LuminousDescentSceneName, Galaxy4Ep27BoneFleetSceneName);
                // Ketos Prime: EP29 landing, gated on EP28 finale completion.
                SetLandable(landables, 4, ketosPrime, 90f, Galaxy4Ep29SisterSignalSceneName, Galaxy4Ep28MoonRisesSceneName);
                // The Vault / Iron Sepulcher: EP30 entry (space scene 1), gated on EP29 finale completion.
                SetLandable(landables, 5, theVault, 90f, Galaxy4Ep30PriceOfMemorySceneName, Galaxy4Ep29IntermissionSceneName);
                // Chronus Prime: EP31 entry (on-foot scene 1), gated on EP30 finale completion.
                SetLandable(landables, 6, chronusPrime, 90f, Galaxy4Ep31ArmedLayerSceneName, Galaxy4Ep30NameRemainsSceneName);
                // Throne of Ashes: EP32 entry (space scene 1, The Breach), gated on EP31 finale completion.
                SetLandable(landables, 7, throneOfAshes, 90f, Galaxy4Ep32TheBreachSceneName, Galaxy4Ep31OneDaySceneName);
                // The Obsidian Synod: EP33 entry (on-foot scene 1, The Gathering), gated on EP32 finale completion (SERIES FINALE).
                SetLandable(landables, 8, obsidianSynod, 90f, Galaxy4Ep33TheGatheringSceneName, Galaxy4Ep32ThresholdSceneName);
                // The Corsair: fallback docking (reuse Galaxy1 interior).
                SetLandable(landables, 9, corsair, 220f, Galaxy1CorsairSceneName);
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Ship Spawn Placer.
            var shipSpawnerGo = new GameObject("Ship Spawn Placer");
            var shipSpawner = shipSpawnerGo.AddComponent<Ronin7.Ship.ShipSpawnPlacer>();
            var spSo = new SerializedObject(shipSpawner);
            SetObjectRef(spSo, "ship", shipCtrl);
            SetObjectRef(spSo, "universe", universe);
            SetObjectRef(spSo, "landing", landing);
            spSo.ApplyModifiedPropertiesWithoutUndo();

            // Completed Planet Outlines.
            var outlineGo = new GameObject("Completed Planet Outlines");
            var outlines = outlineGo.AddComponent<Ronin7.Ship.CompletedPlanetOutlines>();
            var outSo = new SerializedObject(outlines);
            SetObjectRef(outSo, "landing", landing);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/SamuraiOutline.mat");
            if (outlineMat != null)
                SetObjectRef(outSo, "outlineMaterial", outlineMat);
            outSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy4ScenePath);
            EnsureScenesInBuild(Galaxy4ScenePath, Galaxy4Ep25CargoShelfScenePath, Galaxy4Ep25SurgicalLabScenePath,
                Galaxy4Ep25RealRecordScenePath, Galaxy4Ep25VaultScenePath, Galaxy4Ep25KhallWireScenePath,
                Galaxy4Ep25PurposeUntoldScenePath, Galaxy4Ep26AshenDeepScenePath, Galaxy4Ep26DockingBayScenePath,
                Galaxy4Ep26NaveScenePath, Galaxy4Ep26ArchiveCorridorScenePath, Galaxy4Ep26DroneEscapeScenePath,
                Galaxy4Ep26CargoBayScenePath, Galaxy4Ep27BoneGatesScenePath, Galaxy4Ep27WetChambersScenePath,
                Galaxy4Ep27MarrowArchiveScenePath, Galaxy4Ep27WeightOfAGodScenePath, Galaxy4Ep27HandlerArrivesScenePath,
                Galaxy4Ep27BoneFleetScenePath, Galaxy4Ep28LuminousDescentScenePath, Galaxy4Ep28GardenBelowScenePath,
                Galaxy4Ep28ExtractionTeamScenePath, Galaxy4Ep28UndergroundArchiveScenePath, Galaxy4Ep28HarvestCeremonyScenePath,
                Galaxy4Ep28BreakingPointScenePath, Galaxy4Ep28MoonRisesScenePath);

            Debug.Log($"[Space Samurai] Galaxy 4 SPACE scene built at {Galaxy4ScenePath}. " +
                      "Sterile/cool-palette hub for the Reckoning arc with 9 canon worlds: Sable Drift (asteroid field, landable EP25), " +
                      "Pale Reliquary (bone-white), The Hollow Choir (abyssal blue-black), Aethon-9 (agrarian green), " +
                      "Severance Reach (grey), The Vault (dark obsidian), Mirelle (desert ochre), Throne of Ashes (ash-crimson), " +
                      "Ashen Deep (volcanic, landable EP26 gated on EP25 finale), The Hollow Choir (landable EP27 gated on EP26 finale), Aethon-9 (landable EP28 gated on EP27 finale). " +
                      "Single ambient wave (no GuardEncounter). Landing: Sable Drift (EP25, no gate), Ashen Deep (EP26, gated on EP25_PurposeUntold), The Hollow Choir (EP27, gated on EP26_CargoBay), Aethon-9 (EP28, gated on EP27_BoneFleet), Corsair fallback. " +
                      "Briefings: pre-mission (lead_intro), post-mission EP25 (space_ep25_post), post-mission2 EP26 (space_ep26_post), post-mission3 EP27 (space_ep27_post), post-mission4 EP28 (space_ep28_post). " +
                      "Objectives: Sable Drift (EP25), Ashen Deep (EP26, gated on EP25_PurposeUntold), The Hollow Choir (EP27, gated on EP26_CargoBay), Aethon-9 (EP28, gated on EP27_BoneFleet).");
        }
    }
}

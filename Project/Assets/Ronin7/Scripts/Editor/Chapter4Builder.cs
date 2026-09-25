using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Editor.Art;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter 4 ("The Overseer's Hunt") scene builder. One scene, a single Drovis free-port run on a
    /// +Z line: The Throat (dock arrival, ambient manhunt/heat introduced) -> The Sink (Tessa Rin, the
    /// sabotage reveal, a snatch-team skirmish) -> The Mast (Khall names himself and "Cipher") ->
    /// the Deepworks (a switchback cave descent, Echo route-calling, water hazard) -> Kerrax's Hold
    /// (boss duel ending in a SPARE via <see cref="DuelYield"/>, Mera Voss defects). Closes Act I.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch4</c>.
    ///
    /// DESCENT DECISION: <see cref="ZeroGGrabLocomotion"/> is a zero-gravity mechanic (grip a
    /// ZeroGHandle, pull the rig) gated by <c>ContinuousLocomotion.SetZeroG</c> — it belongs to weightless
    /// interiors/space, not a gravity-bound flooded cave, so it is not reused here and no new climbing
    /// system is built either. The Deepworks is instead a single open cave floor (no walls, mirroring the
    /// jungle-exterior ground-plane pattern) threaded between rock-pillar obstacles that force a
    /// switchback path, walkable by the existing <c>ContinuousLocomotion</c>. The shared room helpers
    /// (<c>BuildFloorCeiling</c>/<c>BuildWall</c>) hard-code a single flat floor Y per parent, so a literal
    /// vertical drop was not attempted blind; the "descent" reads through darkening lighting, a narrowing
    /// cave palette, and a <see cref="FloodingWaterHazard"/> pool (shallow chip damage, never a drowning
    /// fail state) near the bottom.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch4ScenePath = SceneFolder + "/Ch04_OverseersHunt.unity";
        private const string Ch4VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        // Named-cast prefabs (Tripo image->3D pipeline, grounded via FitNamedCharacter).
        private const string Ch4KesslerPrefab  = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab";
        private const string Ch4IrisPrefab     = "Assets/Ronin7/Art/Generated/Characters3D/Named/Iris.prefab";
        private const string Ch4ReshPrefab     = "Assets/Ronin7/Art/Generated/Characters3D/Named/Resh.prefab";
        private const string Ch4TessaRinPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Tessa-Rin.prefab";
        private const string Ch4MeraVossPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Mera-Voss.prefab";
        private const string Ch4EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 04 — The Overseer's Hunt", priority = 204)]
        public static void BuildChapter4OverseersHunt()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets,
            // so references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var kerraxDef = Ch4EnsureKerraxDefinition();

            // ---- Lighting: sodium-glare dock key, low ambient, per-segment neon/cold accents. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.75f, 0.5f);
            light.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.09f, 0.09f);

            // Near-black fog: covers the dock haze but reads heaviest in the unwalled Deepworks cave
            // descent, where there's no ceiling geometry to otherwise imply enclosure.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.045f, 0.045f);
            RenderSettings.fogDensity = 0.04f;

            BuildAccentPointLight("ThroatLight0", new Vector3(-4f, 2.6f, 4f), new Color(0.7f, 0.5f, 0.9f), 1.6f, 12f);
            BuildAccentPointLight("ThroatLight1", new Vector3(4f, 2.6f, 10f), new Color(1f, 0.7f, 0.3f), 1.8f, 14f);
            BuildAccentPointLight("SinkLight0", new Vector3(-5f, 2.4f, 24f), new Color(1f, 0.3f, 0.6f), 2f, 14f);
            BuildAccentPointLight("SinkLight1", new Vector3(5f, 2.4f, 34f), new Color(0.2f, 0.85f, 0.9f), 2f, 14f);
            BuildAccentPointLight("MastLight0", new Vector3(0f, 3f, 54f), new Color(0.5f, 0.65f, 1f), 2.2f, 16f);
            BuildAccentPointLight("KerraxLight0", new Vector3(-4f, 2.6f, 118f), new Color(0.3f, 0.55f, 1f), 1.8f, 14f);
            BuildAccentPointLight("KerraxLight1", new Vector3(4f, 2.6f, 126f), new Color(0.3f, 0.55f, 1f), 1.8f, 14f);

            // ---- Drovis interior geometry. Linear +Z run: Throat -> Sink -> Mast -> Deepworks (open
            // cave) -> Kerrax's Hold. ----
            var interiorGo = new GameObject("Drovis");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.17f, 0.15f, 0.14f);
            var ceilColor = new Color(0.08f, 0.07f, 0.07f);

            // The Throat: x[-7,7], z[-4,16]. Spawn. Solid front wall (the shuttle already lifted off).
            BuildFloorCeiling(interior, "Throat", new Vector3(0f, 0f, 6f), new Vector3(14f, 0f, 20f), floorColor, ceilColor);
            BuildWall(interior, "Throat_WallW", new Vector3(-7f, RoomH / 2f, 6f), new Vector3(0.2f, RoomH, 20f));
            BuildWall(interior, "Throat_WallE", new Vector3(7f, RoomH / 2f, 6f), new Vector3(0.2f, RoomH, 20f));
            BuildWall(interior, "Throat_WallFront", new Vector3(0f, RoomH / 2f, -4f), new Vector3(14f, RoomH, 0.2f));
            BuildDoorwayWall(interior, "Throat_WallBack", new Vector3(0f, RoomH / 2f, 16f), 14f, true, 3f);
            BuildRoomDetails(interior, "Throat", new Vector3(0f, 0f, 6f), new Vector2(7f, 10f), new Color(0.4f, 0.35f, 0.5f));
            PlaceDecorativeCrowd(new[]
            {
                new Vector3(-4f, 0f, 3f), new Vector3(4f, 0f, 5f), new Vector3(-3f, 0f, 11f), new Vector3(3f, 0f, 12f),
            });

            // The Sink: x[-9,9], z[16,42]. Bazaar stalls, Tessa Rin, the snatch-team.
            BuildFloorCeiling(interior, "Sink", new Vector3(0f, 0f, 29f), new Vector3(18f, 0f, 26f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Sink_WallFront", new Vector3(0f, RoomH / 2f, 16f), 18f, true, 3f);
            BuildDoorwayWall(interior, "Sink_WallBack", new Vector3(0f, RoomH / 2f, 42f), 18f, true, 3f);
            BuildWall(interior, "Sink_WallW", new Vector3(-9f, RoomH / 2f, 29f), new Vector3(0.2f, RoomH, 26f));
            BuildWall(interior, "Sink_WallE", new Vector3(9f, RoomH / 2f, 29f), new Vector3(0.2f, RoomH, 26f));
            BuildRoomDetails(interior, "Sink", new Vector3(0f, 0f, 29f), new Vector2(9f, 13f), new Color(0.5f, 0.2f, 0.4f));
            Ch4BuildSinkStalls(interior);
            PlaceDecorativeCrowd(new[]
            {
                new Vector3(-5f, 0f, 20f), new Vector3(5f, 0f, 22f), new Vector3(-6f, 0f, 36f), new Vector3(6f, 0f, 38f),
            });

            // The Mast: x[-5,5], z[42,60]. Relay chamber; the back wall opens onto the Deepworks descent
            // (the crew rigs a line down from here), so it is a doorway, not a dead end.
            BuildFloorCeiling(interior, "Mast", new Vector3(0f, 0f, 51f), new Vector3(10f, 0f, 18f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Mast_WallFront", new Vector3(0f, RoomH / 2f, 42f), 10f, true, 3f);
            BuildDoorwayWall(interior, "Mast_WallBack", new Vector3(0f, RoomH / 2f, 60f), 10f, true, 3f);
            BuildWall(interior, "Mast_WallW", new Vector3(-5f, RoomH / 2f, 51f), new Vector3(0.2f, RoomH, 18f));
            BuildWall(interior, "Mast_WallE", new Vector3(5f, RoomH / 2f, 51f), new Vector3(0.2f, RoomH, 18f));
            BuildRoomDetails(interior, "Mast", new Vector3(0f, 0f, 51f), new Vector2(5f, 9f), new Color(0.3f, 0.4f, 0.6f));
            BuildHologram(interior, new Vector3(0f, 0f, 58f)); // the relay core Khall's voice answers through
            BuildShipDrone(interior, "RelayDrone", new Vector3(1.5f, 2.4f, 55f), new Color(0.5f, 0.7f, 1f));

            // The Deepworks: one open cave floor (no walls, an exterior ground-plane
            // pattern) threaded with rock pillars for a switchback path. z[60,112].
            var deepworksGo = new GameObject("Deepworks");
            var deepworks = deepworksGo.transform;
            var caveFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caveFloor.name = "Deepworks_Floor";
            caveFloor.transform.SetParent(deepworks, false);
            caveFloor.transform.localPosition = new Vector3(0f, -0.1f, 86f);
            caveFloor.transform.localScale = new Vector3(16f, 0.2f, 52f);
            TintShared(caveFloor.GetComponent<Renderer>(), new Color(0.1f, 0.09f, 0.09f));
            Ch4BuildDeepworksProps(deepworks);
            BuildAccentPointLight("DeepworksLight0", new Vector3(0f, 2.2f, 68f), new Color(0.3f, 0.5f, 0.55f), 1f, 12f);
            BuildAccentPointLight("DeepworksLight1", new Vector3(0f, 2.2f, 88f), new Color(0.25f, 0.45f, 0.5f), 0.8f, 12f);
            BuildAccentPointLight("DeepworksLight2", new Vector3(0f, 2.2f, 104f), new Color(0.2f, 0.4f, 0.5f), 0.7f, 10f);

            // Kerrax's Hold: x[-7,7], z[112,132]. Cold Dominion-blue trophy keep, dead end.
            BuildFloorCeiling(interior, "KerraxHold", new Vector3(0f, 0f, 122f), new Vector3(14f, 0f, 20f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "KerraxHold_WallFront", new Vector3(0f, RoomH / 2f, 112f), 14f, true, 4f);
            BuildWall(interior, "KerraxHold_WallBack", new Vector3(0f, RoomH / 2f, 132f), new Vector3(14f, RoomH, 0.2f));
            BuildWall(interior, "KerraxHold_WallW", new Vector3(-7f, RoomH / 2f, 122f), new Vector3(0.2f, RoomH, 20f));
            BuildWall(interior, "KerraxHold_WallE", new Vector3(7f, RoomH / 2f, 122f), new Vector3(0.2f, RoomH, 20f));
            BuildRoomDetails(interior, "KerraxHold", new Vector3(0f, 0f, 122f), new Vector2(7f, 10f), new Color(0.2f, 0.35f, 0.55f));
            Ch4BuildKerraxHoldDetails(interior);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig (head + hands), locomotion, bounds, EchoPresence. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 64f); // covers the whole Throat -> Kerrax's Hold run
            bounds.radius = 90f;
            var rigHead = rig.GetComponentInChildren<Camera>(true).transform;
            var vrRig = rig.GetComponent<VRRig>();

            BuildSword(new Vector3(2f, 1f, 0f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch4EchoBladePrefab);
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- Heat meter: wrist-anchored (left hand), thresholds wire to two hunter-wave ambushes.
            // onThreshold is field-initialized to 2 UnityEvents (matching HeatLogic's default 2
            // thresholds), so it's already live right after AddComponent — no SerializedObject resize
            // needed, same as how DuelYield.onYielded/onAccepted are wired elsewhere in this codebase. ----
            var heatGo = new GameObject("HeatMeter");
            var heatMeter = heatGo.AddComponent<HeatMeter>();
            var heatSo = new SerializedObject(heatMeter);
            SetObjectRef(heatSo, "anchor", vrRig.LeftHand);
            heatSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Scan-drones: 3 in the Throat, 2 more (Coil lookouts) in the Deepworks. ----
            Ch4BuildScanDrone(interior, "ScanDrone_Throat0", new Vector3(-3f, 2.4f, 4f), rigHead, heatMeter, 6f, 0.2f);
            Ch4BuildScanDrone(interior, "ScanDrone_Throat1", new Vector3(3f, 2.6f, 10f), rigHead, heatMeter, 6f, 0.2f);
            Ch4BuildScanDrone(interior, "ScanDrone_Throat2", new Vector3(0f, 2.5f, 14f), rigHead, heatMeter, 5f, 0.2f);
            Ch4BuildScanDrone(deepworks, "ScanDrone_Deepworks0", new Vector3(-2f, 2f, 72f), rigHead, heatMeter, 6f, 0.25f);
            Ch4BuildScanDrone(deepworks, "ScanDrone_Deepworks1", new Vector3(2f, 2f, 96f), rigHead, heatMeter, 6f, 0.25f);

            // ---- Hunter-wave ambushes: built as EnemyWaveSpawners kept active but idle (Begin() is
            // never called until a heat threshold fires it — see the wiring below). A huge trigger
            // radius makes the spawner's own proximity poll pass immediately once armed, so the
            // ambush reads as heat-triggered rather than position-triggered. ----
            var waveAEnemies = new List<Health>();
            foreach (var pos in new[] { new Vector3(-4f, 0f, 12f), new Vector3(4f, 0f, 13f) })
            {
                var e = BuildEnemy(pos, playerHealth, enemyDef);
                e.gameObject.SetActive(false);
                waveAEnemies.Add(e.GetComponent<Health>());
            }
            var waveASpawner = BuildWaveSpawner("HunterWaveA", new Vector3(0f, 1f, 10f), 5000f,
                new List<List<Health>> { waveAEnemies }, null);

            var waveBEnemies = new List<Health>();
            foreach (var pos in new[] { new Vector3(-3f, 0f, 66f), new Vector3(3f, 0f, 67f), new Vector3(0f, 0f, 70f) })
            {
                var e = BuildEnemy(pos, playerHealth, enemyDef);
                e.gameObject.SetActive(false);
                waveBEnemies.Add(e.GetComponent<Health>());
            }
            var waveBSpawner = BuildWaveSpawner("HunterWaveB", new Vector3(0f, 1f, 68f), 5000f,
                new List<List<Health>> { waveBEnemies }, null);

            UnityEventTools.AddPersistentListener(heatMeter.OnThresholdEvent(0), new UnityEngine.Events.UnityAction(waveASpawner.Begin));
            UnityEventTools.AddPersistentListener(heatMeter.OnThresholdEvent(1), new UnityEngine.Events.UnityAction(waveBSpawner.Begin));

            // ---- The crew, present through the manhunt. ----
            Ch4PlaceStoryNpc(Ch4KesslerPrefab, new Vector3(-1.5f, 0f, 3f), "Kessler", wanderRadius: 0.6f);
            Ch4PlaceStoryNpc(Ch4IrisPrefab, new Vector3(1.5f, 0f, 3f), "Iris", wanderRadius: 0.6f);
            Ch4PlaceStoryNpc(Ch4ReshPrefab, new Vector3(0f, 0f, 5f), "Resh", wanderRadius: 0.8f);

            // ---- Tessa Rin: the courier, in the Sink. A one-chapter character, not an ally. ----
            Ch4PlaceStoryNpc(Ch4TessaRinPrefab, new Vector3(3f, 0f, 32f), "Tessa Rin", wanderRadius: 0f);

            // ---- Sink snatch-team: inactive until the Trigger step, defeated to reach the sabotage reveal. ----
            var sinkEnemyHealths = new List<Object>();
            var snatchPositions = new[] { new Vector3(-3f, 0f, 30f), new Vector3(3f, 0f, 30f), new Vector3(0f, 0f, 34f) };
            foreach (var pos in snatchPositions)
            {
                var e = BuildEnemy(pos, playerHealth, enemyDef);
                e.gameObject.SetActive(false);
                sinkEnemyHealths.Add(e.GetComponent<Health>());
            }

            // ---- Kerrax: the boss, inactive until the Trigger step, yields via DuelYield (spare, not kill). ----
            var kerraxGo = BuildEnemy(new Vector3(0f, 0f, 122f), playerHealth, kerraxDef).gameObject;
            var kerraxEnemy = kerraxGo.GetComponent<Enemy>();
            var kerraxHealth = kerraxGo.GetComponent<Health>();
            var kerraxNpc = kerraxGo.AddComponent<StoryNpc>();
            var kerraxNpcSo = new SerializedObject(kerraxNpc);
            kerraxNpcSo.FindProperty("displayName").stringValue = "Kerrax";
            kerraxNpcSo.ApplyModifiedPropertiesWithoutUndo();

            var duelYield = kerraxGo.AddComponent<DuelYield>();
            var dySo = new SerializedObject(duelYield);
            SetObjectRef(dySo, "opponent", kerraxHealth);
            dySo.FindProperty("yieldThreshold").floatValue = 0.2f; // yields at low health, not death
            SetObjectRefList(dySo, "disableOnYield", new List<Object> { kerraxEnemy });
            SetObjectRef(dySo, "sword", swordGrab);
            dySo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dySo.ApplyModifiedPropertiesWithoutUndo();
            kerraxGo.SetActive(false); // Activated only by the Trigger step.

            // ---- Mera Voss: hidden watcher until she steps out post-mercy. Ally #2. ----
            var meraGo = Ch4PlaceStoryNpc(Ch4MeraVossPrefab, new Vector3(-5f, 0f, 118f), "Mera Voss", wanderRadius: 0f);
            if (meraGo != null) meraGo.SetActive(false);

            // ---- FloodingWaterHazard: shallow rising-water ambience near the bottom of the Deepworks
            // (chip damage while submerged, never a drowning fail state). ----
            var waterGo = new GameObject("Deepworks_RisingWater");
            waterGo.transform.position = new Vector3(0f, -0.3f, 106f);
            var waterCollider = waterGo.AddComponent<BoxCollider>();
            waterCollider.isTrigger = true;
            waterCollider.size = new Vector3(10f, 3f, 8f);
            var waterVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterVisual.name = "WaterVisual";
            waterVisual.transform.SetParent(waterGo.transform, false);
            waterVisual.transform.localScale = new Vector3(10f, 3f, 8f);
            Object.DestroyImmediate(waterVisual.GetComponent<Collider>());
            TintShared(waterVisual.GetComponent<Renderer>(), new Color(0.1f, 0.25f, 0.35f));
            var flood = waterGo.AddComponent<FloodingWaterHazard>();
            var floodSo = new SerializedObject(flood);
            floodSo.FindProperty("damagePerTick").floatValue = 3f;
            floodSo.FindProperty("damageInterval").floatValue = 1.5f;
            floodSo.FindProperty("riseDuration").floatValue = 20f;
            floodSo.FindProperty("startY").floatValue = -1.5f;
            floodSo.FindProperty("endY").floatValue = -0.2f; // shallow: never fully submerges the player
            floodSo.ApplyModifiedPropertiesWithoutUndo();
            flood.Configure(playerHealth);

            // ---- Reach points. ----
            var sinkReachGo = new GameObject("SinkReachPoint");
            sinkReachGo.transform.position = new Vector3(0f, 1f, 20f);
            var mastReachGo = new GameObject("MastReachPoint");
            mastReachGo.transform.position = new Vector3(0f, 1f, 46f);
            var deepworksEntranceReachGo = new GameObject("DeepworksEntranceReachPoint");
            deepworksEntranceReachGo.transform.position = new Vector3(0f, 1f, 63f);
            var deepworksMidReachGo = new GameObject("DeepworksMidReachPoint");
            deepworksMidReachGo.transform.position = new Vector3(0f, 1f, 86f);
            var deepworksDepthReachGo = new GameObject("DeepworksDepthReachPoint");
            deepworksDepthReachGo.transform.position = new Vector3(0f, 1f, 107f);
            var kerraxHoldReachGo = new GameObject("KerraxHoldReachPoint");
            kerraxHoldReachGo.transform.position = new Vector3(0f, 1f, 116f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch4BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 1f), "ch4_beat0_briefing", talkRef);
            var dlgThroat = Ch4BuildDialogue("Dialogue_Beat1_Throat", new Vector3(0f, 1f, 8f), "ch4_beat1_throat", talkRef);
            var dlgTessaMeet = Ch4BuildDialogue("Dialogue_Beat2_TessaMeet", new Vector3(3f, 1f, 31f), "ch4_beat2_tessa_meet", talkRef);
            var dlgSabotage = Ch4BuildDialogue("Dialogue_Beat2_Sabotage", new Vector3(0f, 1f, 36f), "ch4_beat2_sabotage", talkRef);
            var dlgKhall = Ch4BuildDialogue("Dialogue_Beat3_Khall", new Vector3(0f, 1f, 57f), "ch4_beat3_khall", talkRef);
            var dlgAftermath = Ch4BuildDialogue("Dialogue_Beat3_Aftermath", new Vector3(0f, 1f, 58f), "ch4_beat3_aftermath", talkRef);
            var dlgDescentIntro = Ch4BuildDialogue("Dialogue_Beat4_DescentIntro", new Vector3(0f, 1f, 62f), "ch4_beat4_descent_intro", talkRef);
            var dlgDescentCalls = Ch4BuildDialogue("Dialogue_Beat4_DescentCalls", new Vector3(0f, 1f, 86f), "ch4_beat4_descent_calls", talkRef);
            var dlgDescentEnd = Ch4BuildDialogue("Dialogue_Beat4_DescentEnd", new Vector3(0f, 1f, 108f), "ch4_beat4_descent_end", talkRef);
            var dlgKerraxConfront = Ch4BuildDialogue("Dialogue_Beat5_KerraxConfront", new Vector3(0f, 1f, 117f), "ch4_beat5_kerrax_confront", talkRef);
            var dlgMercy = Ch4BuildDialogue("Dialogue_Beat5_Mercy", new Vector3(0f, 1f, 121f), "ch4_beat5_mercy", talkRef);
            var dlgMeraRecruit = Ch4BuildDialogue("Dialogue_Beat5_MeraRecruit", new Vector3(0f, 1f, 122f), "ch4_beat5_mera_recruit", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = Ch4BuildCompleteCanvas(new Vector3(0f, 1.4f, 128f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 126f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch4_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 4 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 23;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Throat (grounding, the watcher)", dlgThroat);
            AuthorReachStep(steps, n++, "ReachTrigger: The Sink", sinkReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: Tessa Rin (the meet)", dlgTessaMeet);
            // DefeatEnemies activates its own enemies (MissionDirector.BeginDefeatEnemies SetActives
            // each Health's GameObject) — no separate Trigger step needed, matching Chapter2's Auction fight.
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Sink Snatch-Team", sinkEnemyHealths);
            AuthorDialogueStep(steps, n++, "Beat2: The Sabotage (the killswitch reveal)", dlgSabotage);
            AuthorReachStep(steps, n++, "ReachTrigger: The Mast", mastReachGo.transform, 4.5f);
            AuthorDialogueStep(steps, n++, "Beat3: Khall (the naming, Cipher)", dlgKhall);
            AuthorDialogueStep(steps, n++, "Beat3: Aftermath (the crew adopts Cipher)", dlgAftermath);
            AuthorReachStep(steps, n++, "ReachTrigger: Deepworks Entrance", deepworksEntranceReachGo.transform, 4f);
            AuthorDialogueStep(steps, n++, "Beat4: Descent Intro (Echo takes over)", dlgDescentIntro);
            AuthorReachStep(steps, n++, "ReachTrigger: Deepworks Midpoint", deepworksMidReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat4: Echo Route-Calls", dlgDescentCalls);
            AuthorReachStep(steps, n++, "ReachTrigger: Deepworks Depth (past the water)", deepworksDepthReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat4: The Last Drop", dlgDescentEnd);
            AuthorReachStep(steps, n++, "ReachTrigger: Kerrax's Hold", kerraxHoldReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat5: Kerrax Confronts", dlgKerraxConfront);
            AuthorTriggerStep(steps, n++, "Trigger: Activate Kerrax (the boss)", kerraxGo);
            AuthorPromptStep(steps, n++, "Prompt: Kerrax Duel (yield + sheathe)", null);
            AuthorTriggerStep(steps, n++, "Trigger: Mera Voss Steps Out", meraGo);
            AuthorDialogueStep(steps, n++, "Beat5: The Mercy", dlgMercy);
            AuthorDialogueStep(steps, n++, "Beat5: Mera Recruited (Ally #2, closes Act I)", dlgMeraRecruit);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The duel's acceptance advances the mission out of the null-prompt duel step.
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, cave/hold ambience beds, console/mood lights. ----
            BuildAmbienceLayer("SinkDreadAmbience", new Vector3(-5f, 2.4f, 29f), 4f, 14f, 0.4f);
            BuildAmbienceLayer("KerraxHoldAmbience", new Vector3(-4f, 2.6f, 122f), 4f, 14f, 0.4f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();
            AddConsoleFlicker("DeepworksLight0", seed: 44f);
            AddAmbientPulse("KerraxLight0", periodSeconds: 6f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch4ScenePath);
            EnsureScenesInBuild(Ch4ScenePath);

            Debug.Log($"[Space Samurai] Chapter 4 built at {Ch4ScenePath}. " +
                      "Five segments: The Throat (dock, 3 ScanDroneVolumes) -> The Sink (Tessa Rin, " +
                      "snatch-team, sabotage reveal) -> The Mast (Khall names himself and Cipher) -> " +
                      "the Deepworks (open-cave switchback, 2 more ScanDroneVolumes, water hazard) -> " +
                      "Kerrax's Hold (DuelYield boss, spares Kerrax, Mera Voss defects as Ally #2). " +
                      "HeatMeter on the rig (wrist-anchored, left hand) wires 2 thresholds to hunter-wave " +
                      "ambushes. 23 mission steps. Closes Act I.");
        }

        // ---- Data assets. ----

        /// <summary>Ensures a high-HP EnemyDefinition for Kerrax, mirroring Ch2EnsureBodyguardDefinition.</summary>
        private static EnemyDefinition Ch4EnsureKerraxDefinition()
        {
            const string path = DataFolder + "/Ch4Kerrax.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 260f;
            def.damage = 18f;
            def.moveSpeed = 1f;
            def.attackCooldown = 1f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch4 voice clips ourselves. ----

        private static DialoguePlayer Ch4BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter4Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch4WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter4] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch4WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter4Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch4VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch4VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement (mirrors Chapter3's StoryNpc + optional wander wiring). ----

        private static GameObject Ch4PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName, float wanderRadius)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            if (wanderRadius > 0f)
            {
                var wander = go.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = wanderRadius;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        // ---- Scan-drone builder: a BuildShipDrone visual carrying a ScanDroneVolume. ----

        private static void Ch4BuildScanDrone(Transform parent, string name, Vector3 pos, Transform rigHead,
            HeatMeter heat, float radius, float detectionPerSecond)
        {
            var drone = BuildShipDrone(parent, name, pos, new Color(0.75f, 0.8f, 0.9f));
            var volume = drone.AddComponent<ScanDroneVolume>();
            var so = new SerializedObject(volume);
            SetObjectRef(so, "target", rigHead);
            SetObjectRef(so, "heat", heat);
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("detectionPerSecond").floatValue = detectionPerSecond;
            SetObjectRef(so, "telegraphRenderer", drone.GetComponent<Renderer>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Small scene-dressing helpers specific to Chapter 4. ----

        /// <summary>The Sink: low, tight bazaar stalls (lower/tighter than the Throat, per canon).</summary>
        private static void Ch4BuildSinkStalls(Transform parent)
        {
            var stallColor = new Color(0.3f, 0.15f, 0.22f);
            var canopy = new Color(0.5f, 0.18f, 0.35f);
            Vector3[] stallPositions =
            {
                new Vector3(-6f, 0f, 22f), new Vector3(6f, 0f, 24f),
                new Vector3(-6f, 0f, 34f), new Vector3(6f, 0f, 36f),
            };
            foreach (var pos in stallPositions)
            {
                BuildProp(parent, "SinkStall_Counter", pos, new Vector3(1.4f, 0.8f, 0.7f), stallColor);
                BuildProp(parent, "SinkStall_Canopy", pos + new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 0.1f, 0.9f), canopy);
            }
            // Tessa Rin's fence-stall, screened at the back.
            BuildProp(parent, "TessaStall_Counter", new Vector3(3f, 0.5f, 33f), new Vector3(1.6f, 1f, 0.6f), stallColor);
            BuildProp(parent, "TessaStall_Screen", new Vector3(3f, 1.4f, 33.5f), new Vector3(1.8f, 0.9f, 0.05f), new Color(0.2f, 0.2f, 0.22f));
        }

        /// <summary>The Deepworks: rock pillars threaded across the cave floor, forcing a switchback path.</summary>
        private static void Ch4BuildDeepworksProps(Transform parent)
        {
            var rock = new Color(0.13f, 0.12f, 0.11f);
            Vector3[] pillarPositions =
            {
                new Vector3(-4f, 1.1f, 66f), new Vector3(3.5f, 1.3f, 74f),
                new Vector3(-4.5f, 1f, 82f), new Vector3(2.5f, 1.6f, 90f),
                new Vector3(-2f, 1.2f, 98f), new Vector3(3f, 1f, 104f),
            };
            foreach (var pos in pillarPositions)
                BuildProp(parent, "CavePillar", pos, new Vector3(1.2f, pos.y * 2f, 1.2f), rock);
        }

        /// <summary>Kerrax's Hold: plundered plate + trophy-cargo crates, cold stolen-Dominion consoles.</summary>
        private static void Ch4BuildKerraxHoldDetails(Transform parent)
        {
            var plate = new Color(0.22f, 0.24f, 0.28f);
            var trophy = new Color(0.35f, 0.3f, 0.2f);
            var console = new Color(0.3f, 0.7f, 1f);
            BuildProp(parent, "PlunderedPlate0", new Vector3(-5f, 0.6f, 116f), new Vector3(1.6f, 1.2f, 0.6f), plate);
            BuildProp(parent, "PlunderedPlate1", new Vector3(5f, 0.6f, 116f), new Vector3(1.6f, 1.2f, 0.6f), plate);
            BuildProp(parent, "TrophyCrate0", new Vector3(-4f, 0.4f, 126f), new Vector3(0.9f, 0.8f, 0.9f), trophy);
            BuildProp(parent, "TrophyCrate1", new Vector3(4f, 0.4f, 128f), new Vector3(0.9f, 0.8f, 0.9f), trophy);
            BuildProp(parent, "RelayConsole", new Vector3(0f, 0.6f, 125f), new Vector3(1.4f, 1.2f, 0.6f), console);
        }

        /// <summary>A worldspace "CHAPTER 4 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch4BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 4 COMPLETE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 220f);
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "CHAPTER 4 COMPLETE";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 54;
            label.color = new Color(0.9f, 0.92f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            canvasGo.SetActive(false);
            return canvasGo;
        }
    }
}

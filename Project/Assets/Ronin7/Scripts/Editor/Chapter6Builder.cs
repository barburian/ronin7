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
    /// Chapter 6 ("The Iron Dojo") scene builder. One scene: a mountain citadel climbed from a forest
    /// slope (spawn) up a switchback ascent to Morrigan's window, then an any-order kill-list across
    /// three tower branches off a central Iron Yard (Cradle/Matron Hespa, Proving/Drillmaster Caradoc,
    /// Vesting/Master Kaelen), Kaelen's dying confession (Ladder B rung 4), and an evacuation that closes
    /// with Morrigan joining as Ally #3. Opens the mountain segment of Act II.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch6</c>.
    ///
    /// CREW-PRESENCE DECISION: canon frames this chapter as a solo climb ("only one body fits the
    /// route... Cipher goes up alone on foot") with the crew holding comm from the Cairn for the entire
    /// chapter, unlike Ch4/Ch5's landing parties. So Kessler/Resh/Iris/Mera Voss/Mira are voice-only here
    /// (DialoguePlayer speaker labels, no physical NPC) — the same "no body in the scene" convention Ch5
    /// used for Iris/Mera Voss, extended chapter-wide since canon never puts the rest of the crew on the
    /// ground. Only Ronin-7 (the player), Morrigan, and the three masters get physical placement.
    ///
    /// ANY-ORDER GATE WIRING: the three towers are NOT mission steps — the kill-list Trigger step
    /// activates a single "TowerArm" GameObject carrying <see cref="ActivationRelay"/>, whose
    /// persistent listeners call each tower's <see cref="EnemyWaveSpawner"/>.Begin(); each spawner then
    /// arms its own proximity poll (tight ~9m radius on that tower's entrance corridor), so a tower's
    /// cadre+master only activate once the player actually walks toward THAT tower — free-roam in any
    /// order after that. Each master's <see cref="Health"/> feeds a <see cref="MultiObjectiveGate"/>
    /// (built once, always enabled, so its Died subscriptions are live before any tower activates); its
    /// <c>onAllComplete</c> is wired to <c>MissionDirector.AdvanceFromPrompt</c> exactly like
    /// <c>DuelYield.onAccepted</c> in Chapter 4 — the mission sits on a null-promptObject Prompt step
    /// until the gate fires, regardless of which order the masters fall in.
    ///
    /// MASTER-AI DECISION: all three masters are kills (canon: "no spare condition for the bosses"), so
    /// none use <see cref="DuelYield"/> (that FSM is for a yield-then-spare boss, which none of these
    /// are). Hespa and Kaelen are plain <see cref="Enemy"/> with custom <see cref="EnemyDefinition"/>
    /// stats. Caradoc additionally carries <see cref="PatternedDuelist"/> (unmodified, default params) —
    /// its "predicts repeated-side hits and refunds them" mechanic is a natural fit for a drillmaster who
    /// reads a fighter's patterns, and it layers onto <see cref="Enemy"/> without needing a bespoke duel
    /// FSM. The Named-mesh masters (Matron-Hespa/Drillmaster-Caradoc/Master-Kaelen prefabs) have no
    /// ArmR/Sword/BladeTip combat rig, so <see cref="Ch6BuildMasterEnemy"/> synthesizes one exactly the
    /// way <c>BuildDominionEnemy</c>'s fallback block does for troopers without a rigged prefab.
    ///
    /// EVACUATION-TIMER DECISION: <see cref="EvacuationTimer"/> is reused purely as an ambient "bells
    /// ringing wrong" countdown prop activated alongside the trainee descent — it never gates a fail
    /// state. Trainees are unkillable by design (no <see cref="Health"/> component at all, so
    /// <c>BladeDamager</c> has nothing to apply damage to), so there is no escort-objective/
    /// <see cref="ProtectNpcObjective"/> failure path to wire; the timer purely dresses the scene.
    ///
    /// GEOMETRY: the ascent is 5 switchback terraces (alternating x offsets, rising y) linked by tilted
    /// ramp colliders the existing <c>ContinuousLocomotion</c>/CharacterController already climbs, per
    /// Ch4's "no new locomotion" precedent — mirrors the Deepworks descent, inverted. Everything from
    /// Morrigan's spine onward sits on an "UpperCitadel" parent offset to y=12 so the shared room-shell
    /// helpers (which hard-code floor/ceiling Y relative to their parent) place geometry at the climb's
    /// summit without needing new helpers.
    ///
    /// BARK WIRING: <see cref="Chapter6Lines"/>'s 3 per-tower master-intro-bark sets
    /// (ch6_beat3_hespa_intro/caradoc_intro/kaelen_intro) are wired as wave-0 barks on each tower's
    /// <see cref="EnemyWaveSpawner"/> — each tower's own proximity-armed spawner is exactly the
    /// per-tower entry trigger the previous deferral was waiting on, so a bark can only fire once, for
    /// the tower the player actually approached.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch6ScenePath = SceneFolder + "/Ch06_IronDojo.unity";
        private const string Ch6VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch6UpperY = 12f; // world Y of Morrigan's spine / Iron Yard / the three towers

        // Named-cast prefabs (Tripo image->3D pipeline, grounded via FitNamedCharacter — same convention
        // Chapter3/4/5 use). All three masters and Morrigan have real baked meshes.
        private const string Ch6MorriganPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Morrigan.prefab";
        private const string Ch6HespaPrefab    = "Assets/Ronin7/Art/Generated/Characters3D/Named/Matron-Hespa.prefab";
        private const string Ch6CaradocPrefab  = "Assets/Ronin7/Art/Generated/Characters3D/Named/Drillmaster-Caradoc.prefab";
        private const string Ch6KaelenPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Master-Kaelen.prefab";
        private const string Ch6EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 06 — The Iron Dojo", priority = 206)]
        public static void BuildChapter6IronDojo()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var hespaDef = Ch6EnsureHespaDefinition();
            var caradocDef = Ch6EnsureCaradocDefinition();
            var kaelenDef = Ch6EnsureKaelenDefinition();

            // ---- Lighting: golden mountain-citadel daylight — the beauty IS the horror. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.92f, 0.78f);
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.26f, 0.22f);

            // Soft golden daylight haze over the citadel.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.5f, 0.4f);
            RenderSettings.fogDensity = 0.018f;

            BuildAccentPointLight("ForestLight0", new Vector3(-4f, 2.2f, 8f), new Color(1f, 0.9f, 0.6f), 1f, 12f);
            BuildAccentPointLight("SpineLight0", new Vector3(-3f, Ch6UpperY + 2.4f, 92f), new Color(0.6f, 0.75f, 1f), 1.2f, 12f);
            BuildAccentPointLight("SpineLight1", new Vector3(3f, Ch6UpperY + 2.4f, 98f), new Color(0.6f, 0.75f, 1f), 1.2f, 12f);
            BuildAccentPointLight("IronYardLight0", new Vector3(-8f, Ch6UpperY + 3f, 108f), new Color(1f, 0.9f, 0.65f), 1.4f, 16f);
            BuildAccentPointLight("IronYardLight1", new Vector3(8f, Ch6UpperY + 3f, 114f), new Color(1f, 0.9f, 0.65f), 1.4f, 16f);

            // ---- World root. ----
            var worldGo = new GameObject("IronDojo");
            var world = worldGo.transform;

            // ---- The forest slope (spawn): open ground, no walls, mirrors the Ch5 exterior pattern. ----
            var forestGo = new GameObject("ForestSlope");
            var forest = forestGo.transform;
            forest.SetParent(world, false);
            var forestGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forestGround.name = "ForestGround";
            forestGround.transform.SetParent(forest, false);
            forestGround.transform.localPosition = new Vector3(0f, -0.5f, 10f);
            forestGround.transform.localScale = new Vector3(20f, 1f, 28f); // reaches z=24, overlapping Terrace0's floor (z[18,26]) — no gap at the ascent's foot
            TintShared(forestGround.GetComponent<Renderer>(), new Color(0.14f, 0.24f, 0.1f));
            Vector3[] treePositions =
            {
                new Vector3(-6f, 1.2f, 2f), new Vector3(6f, 1.4f, 4f), new Vector3(-7f, 1.1f, 12f),
                new Vector3(7f, 1.3f, 10f), new Vector3(-4f, 1.2f, 15f), new Vector3(4f, 1.1f, 16f),
            };
            foreach (var pos in treePositions)
                BuildProp(forest, "Pine", pos, new Vector3(0.8f, pos.y * 2f, 0.8f), new Color(0.1f, 0.22f, 0.12f));

            // ---- Switchback ascent: 5 terraces (alternating x, rising y) linked by tilted ramps. ----
            var ascentGo = new GameObject("Ascent");
            var ascent = ascentGo.transform;
            ascent.SetParent(world, false);

            Vector3[] terraces =
            {
                new Vector3(0f, 0f, 22f),
                new Vector3(2f, 3f, 36f),
                new Vector3(-2f, 6f, 50f),
                new Vector3(1f, 9f, 64f),
                new Vector3(0f, Ch6UpperY, 80f), // WindowLedge — Morrigan's window
            };
            for (int i = 0; i < terraces.Length; i++)
                Ch6BuildTerrace(ascent, $"Terrace{i}", terraces[i]);
            for (int i = 0; i < terraces.Length - 1; i++)
                Ch6BuildRamp(ascent, $"Ramp{i}", terraces[i], terraces[i + 1], 6f);

            // ---- Upper citadel: Morrigan's spine, the Iron Yard, and the three tower branches, all
            // parented to y=Ch6UpperY so the shared room-shell helpers (which hard-code floor/ceiling Y
            // relative to their parent) place geometry at the climb's summit for free. ----
            var upperGo = new GameObject("UpperCitadel");
            var upper = upperGo.transform;
            upper.SetParent(world, false);
            upper.localPosition = new Vector3(0f, Ch6UpperY, 0f);

            var spineFloor = new Color(0.2f, 0.19f, 0.22f);
            var spineCeil = new Color(0.1f, 0.1f, 0.12f);
            BuildFloorCeiling(upper, "MorriganSpine", new Vector3(0f, 0f, 92f), new Vector3(12f, 0f, 16f), spineFloor, spineCeil);
            BuildWall(upper, "MorriganSpine_WallW", new Vector3(-6f, RoomH / 2f, 92f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(upper, "MorriganSpine_WallE", new Vector3(6f, RoomH / 2f, 92f), new Vector3(0.2f, RoomH, 16f));
            BuildDoorwayWall(upper, "MorriganSpine_WallN", new Vector3(0f, RoomH / 2f, 100f), 12f, true, 3f);
            BuildRoomDetails(upper, "MorriganSpine", new Vector3(0f, 0f, 92f), new Vector2(6f, 8f), new Color(0.35f, 0.4f, 0.55f));

            // Iron Yard: an open muster ground (no walls, no ceiling — exterior under the mountain sky).
            var yardGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yardGround.name = "IronYard_Ground";
            yardGround.transform.SetParent(upper, false);
            yardGround.transform.localPosition = new Vector3(0f, -0.1f, 111f);
            yardGround.transform.localScale = new Vector3(30f, 0.2f, 22f);
            TintShared(yardGround.GetComponent<Renderer>(), new Color(0.42f, 0.4f, 0.34f));
            var bellGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bellGo.name = "BellTower";
            bellGo.transform.SetParent(upper, false);
            bellGo.transform.localPosition = new Vector3(0f, 1.4f, 111f);
            bellGo.transform.localScale = new Vector3(0.6f, 1.4f, 0.6f);
            TintShared(bellGo.GetComponent<Renderer>(), new Color(0.55f, 0.5f, 0.3f));

            // Three tower corridors + arenas branching off the yard: Cradle (-X), Vesting (+X), Proving (+Z).
            var cradleColor = new Color(0.45f, 0.35f, 0.4f);
            var vestingColor = new Color(0.3f, 0.35f, 0.45f);
            var provingColor = new Color(0.4f, 0.3f, 0.25f);
            Ch6BuildGroundStrip(upper, "CradleCorridor", new Vector3(-26f, 0f, 111f), new Vector3(24f, 10f), cradleColor);
            Ch6BuildTowerArena(upper, "Cradle", new Vector3(-46f, 0f, 111f), 8f, "east", spineFloor, spineCeil, cradleColor);
            Ch6BuildGroundStrip(upper, "VestingCorridor", new Vector3(26f, 0f, 111f), new Vector3(24f, 10f), vestingColor);
            Ch6BuildTowerArena(upper, "Vesting", new Vector3(46f, 0f, 111f), 8f, "west", spineFloor, spineCeil, vestingColor);
            Ch6BuildGroundStrip(upper, "ProvingCorridor", new Vector3(0f, 0f, 131f), new Vector3(10f, 20f), provingColor);
            Ch6BuildTowerArena(upper, "Proving", new Vector3(0f, 0f, 150f), 8f, "south", spineFloor, spineCeil, provingColor);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig (head + hands), locomotion, bounds, EchoPresence. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 8f, 90f); // covers forest slope through the farthest tower arena
            bounds.radius = 100f;

            // The katana rides from the start (deep into Act II — no rack-wake beat, matching Ch5's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch6EchoBladePrefab);

            // ---- Morrigan: waiting in her spine from the start, fixed in place. ----
            Ch6PlaceStoryNpc(Ch6MorriganPrefab, new Vector3(0f, Ch6UpperY, 97f), "Morrigan", wanderRadius: 0f);

            // ---- The three towers: cadre + master, all inactive until their own tower's
            // EnemyWaveSpawner arms it (wired below, once dialogue's talkRef is available) — each tower
            // activates only when the player approaches ITS entrance corridor, not all three at once. ----

            Vector3[] cradleCadrePos = { new Vector3(-20f, Ch6UpperY, 108f), new Vector3(-30f, Ch6UpperY, 116f), new Vector3(-42f, Ch6UpperY, 111f) };
            var cradleCadre = Ch6BuildCadre(cradleCadrePos, playerHealth, enemyDef);
            var hespaEnemy = Ch6BuildMasterEnemy(Ch6HespaPrefab, new Vector3(-46f, Ch6UpperY, 111f), "Matron Hespa", hespaDef, playerHealth);
            hespaEnemy.gameObject.SetActive(false);
            Ch6BuildTrainee(new Vector3(-44f, Ch6UpperY, 105f), "Trainee_Cradle0", 0.5f);
            Ch6BuildTrainee(new Vector3(-48f, Ch6UpperY, 117f), "Trainee_Cradle1", 0.5f);

            Vector3[] provingCadrePos = { new Vector3(0f, Ch6UpperY, 126f), new Vector3(0f, Ch6UpperY, 134f), new Vector3(0f, Ch6UpperY, 146f) };
            var provingCadre = Ch6BuildCadre(provingCadrePos, playerHealth, enemyDef);
            var caradocEnemy = Ch6BuildMasterEnemy(Ch6CaradocPrefab, new Vector3(0f, Ch6UpperY, 150f), "Drillmaster Caradoc", caradocDef, playerHealth);
            caradocEnemy.gameObject.AddComponent<PatternedDuelist>(); // reads repeated-side hits; drillmaster flavor
            caradocEnemy.gameObject.SetActive(false);
            Ch6BuildTrainee(new Vector3(3f, Ch6UpperY, 144f), "Trainee_Proving0", 0.75f);

            Vector3[] vestingCadrePos = { new Vector3(20f, Ch6UpperY, 108f), new Vector3(30f, Ch6UpperY, 116f), new Vector3(42f, Ch6UpperY, 111f) };
            var vestingCadre = Ch6BuildCadre(vestingCadrePos, playerHealth, enemyDef);
            var kaelenEnemy = Ch6BuildMasterEnemy(Ch6KaelenPrefab, new Vector3(46f, Ch6UpperY, 111f), "Master Kaelen", kaelenDef, playerHealth);
            kaelenEnemy.gameObject.SetActive(false);
            Ch6BuildTrainee(new Vector3(44f, Ch6UpperY, 105f), "Trainee_Vesting0", 0.75f);

            // ---- MultiObjectiveGate: any-order 3-master kill-list. Always enabled (its Died
            // subscriptions must be live before the towers activate), invokes onAllComplete once all
            // three masters have died, regardless of order. ----
            var gateGo = new GameObject("KillListGate");
            var gate = gateGo.AddComponent<MultiObjectiveGate>();
            var gateSo = new SerializedObject(gate);
            SetObjectRefList(gateSo, "objectives", new List<Object>
            {
                hespaEnemy.GetComponent<Health>(),
                caradocEnemy.GetComponent<Health>(),
                kaelenEnemy.GetComponent<Health>(),
            });
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Evacuation: an ambient "bells ringing wrong" countdown + two trainee lines walking the
            // switchback ascent back down to the forest slope, both inactive until the evac Trigger step. ----
            var alarmGo = new GameObject("PurgeAlarm");
            alarmGo.transform.position = new Vector3(0f, Ch6UpperY + 3f, 111f);
            var alarmTextGo = new GameObject("Text");
            alarmTextGo.transform.SetParent(alarmGo.transform, false);
            var alarmTm = alarmTextGo.AddComponent<TextMesh>();
            alarmTm.anchor = TextAnchor.MiddleCenter;
            alarmTm.alignment = TextAlignment.Center;
            alarmTm.fontSize = 48;
            alarmTm.color = new Color(1f, 0.35f, 0.3f);
            var evac = alarmGo.AddComponent<EvacuationTimer>();
            var evacSo = new SerializedObject(evac);
            SetObjectRef(evacSo, "textMesh", alarmTm);
            evacSo.ApplyModifiedPropertiesWithoutUndo();
            alarmGo.SetActive(false);

            var descentWaypoints = new List<Vector3> { new Vector3(0f, Ch6UpperY + 1f, 96f) };
            for (int i = terraces.Length - 1; i >= 0; i--)
                descentWaypoints.Add(terraces[i] + new Vector3(0f, 1f, 0f));
            descentWaypoints.Add(new Vector3(0f, 0.5f, 8f));

            var trainee0 = Ch6BuildTrainee(new Vector3(2f, Ch6UpperY, 113f), "Trainee_Eldest", 0.9f);
            var trainee1 = Ch6BuildTrainee(new Vector3(-2f, Ch6UpperY, 113f), "Trainee_Young", 0.6f);
            var walker0 = BuildNpcWalker(world, "TraineeWalker0", trainee0, descentWaypoints.ToArray());
            var walker1 = BuildNpcWalker(world, "TraineeWalker1", trainee1, descentWaypoints.ToArray());

            // ---- Reach points. ----
            var ascentMidReachGo = new GameObject("AscentMidReachPoint");
            ascentMidReachGo.transform.position = terraces[2] + new Vector3(0f, 1f, 0f);
            var windowReachGo = new GameObject("WindowReachPoint");
            windowReachGo.transform.position = terraces[4] + new Vector3(0f, 1f, 0f);
            var morriganReturnReachGo = new GameObject("MorriganReturnReachPoint");
            morriganReturnReachGo.transform.position = new Vector3(0f, Ch6UpperY + 1f, 96f);
            var ironYardReachGo = new GameObject("IronYardReachPoint");
            ironYardReachGo.transform.position = new Vector3(0f, Ch6UpperY + 1f, 111f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch6BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch6_beat0_briefing", talkRef);
            var dlgClimb = Ch6BuildDialogue("Dialogue_Beat1_Climb", new Vector3(0f, 1f, 10f), "ch6_beat1_climb", talkRef);
            var dlgWindow = Ch6BuildDialogue("Dialogue_Beat1_Window", terraces[4] + new Vector3(0f, 1f, 0f), "ch6_beat1_window", talkRef);
            var dlgMorriganMeet = Ch6BuildDialogue("Dialogue_Beat2_MorriganMeet", new Vector3(0f, Ch6UpperY + 1f, 96f), "ch6_beat2_morrigan_meet", talkRef);
            var dlgKillList = Ch6BuildDialogue("Dialogue_Beat2_KillList", new Vector3(0f, Ch6UpperY + 1f, 97f), "ch6_beat2_killlist", talkRef);
            var dlgConfession = Ch6BuildDialogue("Dialogue_Beat4_Confession", new Vector3(46f, Ch6UpperY + 1f, 111f), "ch6_beat4_confession", talkRef);
            var dlgEvacuation = Ch6BuildDialogue("Dialogue_Beat5_Evacuation", new Vector3(0f, Ch6UpperY + 1f, 96f), "ch6_beat5_evacuation", talkRef);
            var dlgDescent = Ch6BuildDialogue("Dialogue_Beat5_Descent", new Vector3(0f, Ch6UpperY + 1f, 111f), "ch6_beat5_descent", talkRef);
            var dlgMorriganJoins = Ch6BuildDialogue("Dialogue_Beat5_MorriganJoins", new Vector3(0f, Ch6UpperY + 1f, 112f), "ch6_beat5_morrigan_joins", talkRef);
            var dlgOutroHook = Ch6BuildDialogue("Dialogue_Beat5_OutroHook", new Vector3(0f, Ch6UpperY + 1f, 113f), "ch6_beat5_outro", talkRef);

            // Per-tower master-intro barks (previously authored but unwired — see Chapter6Lines): each
            // plays as wave 0 (the cadre) of its tower's EnemyWaveSpawner below.
            var dlgHespaIntro = Ch6BuildDialogue("Dialogue_Beat3_HespaIntro", new Vector3(-38f, Ch6UpperY + 1f, 111f), "ch6_beat3_hespa_intro", talkRef);
            var dlgCaradocIntro = Ch6BuildDialogue("Dialogue_Beat3_CaradocIntro", new Vector3(0f, Ch6UpperY + 1f, 142f), "ch6_beat3_caradoc_intro", talkRef);
            var dlgKaelenIntro = Ch6BuildDialogue("Dialogue_Beat3_KaelenIntro", new Vector3(38f, Ch6UpperY + 1f, 111f), "ch6_beat3_kaelen_intro", talkRef);

            // ---- Per-tower EnemyWaveSpawners: wave0 = that tower's cadre, wave1 = its master. Built
            // active-idle (Ch4's HunterWave lesson: an inactive spawner can't StartCoroutine) — Begin()
            // is only called once TowerArm below relays the kill-list Trigger step into it, so a
            // wandering player can't pull a tower's fight before the beat that unlocks it. triggerRadius
            // 9 keeps each tower's proximity poll tight to its own entrance corridor so entering one
            // tower can't arm another. ----
            var cradleWaves = new List<List<Health>>
            {
                cradleCadre.ConvertAll(go => go.GetComponent<Health>()),
                new List<Health> { hespaEnemy.GetComponent<Health>() },
            };
            var cradleSpawner = BuildWaveSpawner("CradleWaveSpawner", new Vector3(-26f, Ch6UpperY, 111f), 9f,
                cradleWaves, new[] { dlgHespaIntro });

            var provingWaves = new List<List<Health>>
            {
                provingCadre.ConvertAll(go => go.GetComponent<Health>()),
                new List<Health> { caradocEnemy.GetComponent<Health>() },
            };
            var provingSpawner = BuildWaveSpawner("ProvingWaveSpawner", new Vector3(0f, Ch6UpperY, 131f), 9f,
                provingWaves, new[] { dlgCaradocIntro });

            var vestingWaves = new List<List<Health>>
            {
                vestingCadre.ConvertAll(go => go.GetComponent<Health>()),
                new List<Health> { kaelenEnemy.GetComponent<Health>() },
            };
            var vestingSpawner = BuildWaveSpawner("VestingWaveSpawner", new Vector3(26f, Ch6UpperY, 111f), 9f,
                vestingWaves, new[] { dlgKaelenIntro });

            // TowerArm: the kill-list Trigger step's sole target. MissionStepKind.Trigger only
            // SetActive(true)s objects, so ActivationRelay turns that single activation into 3 Begin()
            // calls (one per spawner) via persistent listeners — the HeatMeter.onThreshold precedent.
            var towerArmGo = new GameObject("TowerArm");
            var towerArmRelay = towerArmGo.AddComponent<ActivationRelay>();
            UnityEventTools.AddPersistentListener(towerArmRelay.OnEnabled, new UnityEngine.Events.UnityAction(cradleSpawner.Begin));
            UnityEventTools.AddPersistentListener(towerArmRelay.OnEnabled, new UnityEngine.Events.UnityAction(provingSpawner.Begin));
            UnityEventTools.AddPersistentListener(towerArmRelay.OnEnabled, new UnityEngine.Events.UnityAction(vestingSpawner.Begin));
            towerArmGo.SetActive(false);

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = Ch6BuildCompleteCanvas(new Vector3(0f, Ch6UpperY + 1.4f, 115f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, Ch6UpperY + 1f, 114f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch6_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 6 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 18;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Drop (Echo takes the climb)", dlgClimb);
            AuthorReachStep(steps, n++, "ReachTrigger: Ascent Midpoint", ascentMidReachGo.transform, 5f);
            AuthorReachStep(steps, n++, "ReachTrigger: Morrigan's Window", windowReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Window (last reach)", dlgWindow);
            AuthorDialogueStep(steps, n++, "Beat2: Morrigan (the meet)", dlgMorriganMeet);
            AuthorDialogueStep(steps, n++, "Beat2: The Kill-List (towers unlock, any order)", dlgKillList);
            AuthorTriggerStep(steps, n++, "Trigger: Activate the Three Towers", towerArmGo);
            AuthorPromptStep(steps, n++, "Prompt: Clear the Three Towers (any order)", null);
            AuthorDialogueStep(steps, n++, "Beat4: Kaelen's Confession (Ladder B, rung 4)", dlgConfession);
            AuthorReachStep(steps, n++, "ReachTrigger: Return to Morrigan", morriganReturnReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat5: The Evacuation (doors open)", dlgEvacuation);
            AuthorTriggerStep(steps, n++, "Trigger: Iron Yard Evacuation Begins", alarmGo, walker0, walker1);
            AuthorReachStep(steps, n++, "ReachTrigger: The Iron Yard", ironYardReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat5: The Farewell (you're not going with them)", dlgDescent);
            AuthorDialogueStep(steps, n++, "Beat5: Morrigan Joins (Ally #3, the two seeds)", dlgMorriganJoins);
            AuthorDialogueStep(steps, n++, "Beat5: The Outro Hook (the hunt widens)", dlgOutroHook);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The kill-list gate advances the mission out of the null-prompt tower-clearing step.
            UnityEventTools.AddPersistentListener(gate.onAllComplete,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, spine/forest ambience beds, console/mood lights. ----
            BuildAmbienceLayer("MorriganSpineAmbience", new Vector3(0f, 1.5f, 92f), 4f, 16f, 0.4f);
            BuildAmbienceLayer("ForestGardenAmbience", new Vector3(-4f, 2.2f, 8f), 6f, 24f, 0.4f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();
            AddConsoleFlicker("IronYardLight0", seed: 55f);
            AddAmbientPulse("SpineLight0", periodSeconds: 6.2f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Parkour summit route (cycle-5 retrofit, kept in the builder so rebuilds stay
            // correct): climbable terraces/rocks/bell tower + the hold ladder to the summit deck. ----
            AddDojoSummitRoute(world);

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch6ScenePath);
            EnsureScenesInBuild(Ch6ScenePath);

            Debug.Log($"[Space Samurai] Chapter 6 built at {Ch6ScenePath}. " +
                      "Forest slope (spawn) -> a 5-terrace switchback ascent -> Morrigan's window -> the " +
                      "kill-list (3 towers unlock in any order: Cradle/Hespa, Proving/Caradoc, " +
                      "Vesting/Kaelen, gated by MultiObjectiveGate) -> Kaelen's confession (Ladder B rung " +
                      "4) -> the evacuation (Morrigan joins as Ally #3, 2 seeds planted). 18 mission steps.");
        }

        // ---- Data assets: per-master EnemyDefinitions (mirrors Ch4EnsureKerraxDefinition). ----

        private static EnemyDefinition Ch6EnsureHespaDefinition()
        {
            const string path = DataFolder + "/Ch6Hespa.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 180f;
            def.damage = 12f;
            def.moveSpeed = 1.1f;
            def.attackCooldown = 1.1f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch6EnsureCaradocDefinition()
        {
            const string path = DataFolder + "/Ch6Caradoc.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 240f;
            def.damage = 20f;
            def.moveSpeed = 1.6f;
            def.attackCooldown = 0.7f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch6EnsureKaelenDefinition()
        {
            const string path = DataFolder + "/Ch6Kaelen.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 280f;
            def.damage = 22f;
            def.moveSpeed = 1.3f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch6 voice clips ourselves. ----

        private static DialoguePlayer Ch6BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter6Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch6WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter6] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch6WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter6Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch6VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch6VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement (mirrors Chapter3/4/5's StoryNpc + optional wander wiring). ----

        private static GameObject Ch6PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName, float wanderRadius)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0 (fine for Ch3/4/5's y=0 floors); this
            // chapter's citadel floors sit at pos.y, so re-add the floor height after fitting.
            go.transform.position += Vector3.up * pos.y;

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

        /// <summary>A decorative, unkillable-by-design trainee: a capsule primitive with NO Health
        /// component, so <c>BladeDamager</c> has nothing to apply damage to (the blade can never rise to
        /// them, structurally, not just by convention). <paramref name="pos"/> is the floor point; the
        /// centered capsule pivot is lifted by its half-height (= scale) so the feet sit on it.</summary>
        private static GameObject Ch6BuildTrainee(Vector3 pos, string name, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos + Vector3.up * scale;
            go.transform.localScale = new Vector3(scale, scale, scale);
            TintShared(go.GetComponent<Renderer>(), new Color(0.55f, 0.5f, 0.4f));
            return go;
        }

        /// <summary>Builds N cadre <see cref="Enemy"/> instances at the given world positions, each
        /// inactive until its tower's <see cref="EnemyWaveSpawner"/> starts wave 0.</summary>
        private static List<GameObject> Ch6BuildCadre(Vector3[] positions, Health playerHealth, EnemyDefinition def)
        {
            var result = new List<GameObject>();
            foreach (var pos in positions)
            {
                var e = BuildEnemy(pos, playerHealth, def);
                e.gameObject.SetActive(false);
                result.Add(e.gameObject);
            }
            return result;
        }

        /// <summary>
        /// A master boss built from a Named-mesh prefab (no combat rig of its own): instantiates the
        /// mesh, synthesizes an ArmR/Sword/Blade/BladeTip hierarchy the way <c>BuildDominionEnemy</c>'s
        /// fallback block does, then wires <see cref="Enemy"/> onto it. Returned inactive is the
        /// caller's job (mirrors <c>BuildEnemy</c>, which never SetActives its result either).
        /// </summary>
        private static Enemy Ch6BuildMasterEnemy(string prefabPath, Vector3 pos, string displayName, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            FitNamedCharacter(go);
            // FitNamedCharacter grounds the feet at world y=0; the tower floors sit at pos.y — re-add it
            // (Enemy.Awake captures groundY from the resulting transform, so chase stays on this floor).
            go.transform.position += Vector3.up * pos.y;

            // Body collider: the Named-mesh prefab carries none of its own (only BuildEnemy's
            // CreatePrimitive(Capsule) greybox body does), so without one BladeDamager.OnTriggerEnter's
            // GetComponentInParent<Health> never fires and the master is unkillable. Matches the cadre
            // greybox capsule convention closely enough (BuildEnemy's scaled Capsule primitive).
            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            go.AddComponent<Health>();
            var bodyRenderer = go.GetComponentInChildren<Renderer>();

            var armRGo = new GameObject("ArmR");
            armRGo.transform.SetParent(go.transform, false);
            armRGo.transform.localPosition = new Vector3(0.3f, 1.3f, 0f);
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(armRGo.transform, false);
            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(swordGo.transform, false);
            var bladeTipGo = new GameObject("BladeTip");
            bladeTipGo.transform.SetParent(bladeGo.transform, false);
            bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            var enemy = go.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "weapon", armRGo.transform);
            SetObjectRef(so, "bladeTip", bladeTipGo.transform);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            SetObjectRef(so, "target", playerHealth);
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        // ---- The switchback ascent: terrace platforms + tilted ramp connectors. ----

        /// <summary>An 8x8 terrace platform with a couple of rock props and a patrol light, centered on
        /// <paramref name="center"/> (world position — <paramref name="parent"/> sits at the world
        /// origin, so local and world positions coincide here).</summary>
        private static void Ch6BuildTerrace(Transform parent, string name, Vector3 center)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.position = center + new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.4f, 8f);
            TintShared(floor.GetComponent<Renderer>(), new Color(0.32f, 0.3f, 0.26f));

            BuildProp(parent, name + "_Rock0", center + new Vector3(-3.5f, 0.6f, -2f), new Vector3(1f, 1.2f, 1f), new Color(0.22f, 0.2f, 0.18f));
            BuildProp(parent, name + "_Rock1", center + new Vector3(3.2f, 0.5f, 2.5f), new Vector3(0.9f, 1f, 0.9f), new Color(0.22f, 0.2f, 0.18f));
            BuildAccentPointLight(name + "_PatrolLight", center + new Vector3(0f, 2.4f, 0f), new Color(1f, 0.85f, 0.5f), 1f, 10f);
        }

        /// <summary>A tilted box collider bridging two terrace centers — the CharacterController climbs
        /// it like any sloped floor (no new locomotion mechanic).</summary>
        private static void Ch6BuildRamp(Transform parent, string name, Vector3 from, Vector3 to, float width)
        {
            Vector3 mid = (from + to) * 0.5f;
            float rise = to.y - from.y;
            float run = to.z - from.z;
            float length = Mathf.Sqrt(rise * rise + run * run);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent, false);
            ramp.transform.position = mid;
            ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(width, 0.4f, length);
            TintShared(ramp.GetComponent<Renderer>(), new Color(0.28f, 0.26f, 0.22f));
        }

        // ---- The upper citadel: a plain ground strip for corridors, an enclosed arena per tower. ----

        /// <summary>A flat, wall-less ground strip (a tower approach corridor) — <paramref name="size"/>
        /// is (x,z). Local coordinates relative to the UpperCitadel parent.</summary>
        private static void Ch6BuildGroundStrip(Transform parent, string name, Vector3 localCenter, Vector2 size, Color color)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = localCenter + new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(size.x, 0.2f, size.y);
            TintShared(floor.GetComponent<Renderer>(), color);
        }

        /// <summary>
        /// An enclosed square tower-arena room (floor/ceiling/4 walls, one doorway) housing a master
        /// fight. <paramref name="doorSide"/> picks which wall gets the doorway back toward the Iron
        /// Yard / corridor ("east"/"west"/"south" — the fourth side is always solid, the tower's back).
        /// Local coordinates relative to the UpperCitadel parent.
        /// </summary>
        private static void Ch6BuildTowerArena(Transform parent, string name, Vector3 localCenter, float half,
            string doorSide, Color floorColor, Color ceilColor, Color accent)
        {
            float x0 = localCenter.x - half, x1 = localCenter.x + half;
            float z0 = localCenter.z - half, z1 = localCenter.z + half;
            BuildFloorCeiling(parent, name, localCenter, new Vector3(half * 2f, 0f, half * 2f), floorColor, ceilColor);

            if (doorSide == "west") BuildDoorwayWall(parent, name + "_WallW", new Vector3(x0, RoomH / 2f, localCenter.z), half * 2f, false, 4f);
            else BuildWall(parent, name + "_WallW", new Vector3(x0, RoomH / 2f, localCenter.z), new Vector3(0.2f, RoomH, half * 2f));

            if (doorSide == "east") BuildDoorwayWall(parent, name + "_WallE", new Vector3(x1, RoomH / 2f, localCenter.z), half * 2f, false, 4f);
            else BuildWall(parent, name + "_WallE", new Vector3(x1, RoomH / 2f, localCenter.z), new Vector3(0.2f, RoomH, half * 2f));

            if (doorSide == "south") BuildDoorwayWall(parent, name + "_WallS", new Vector3(localCenter.x, RoomH / 2f, z0), half * 2f, true, 4f);
            else BuildWall(parent, name + "_WallS", new Vector3(localCenter.x, RoomH / 2f, z0), new Vector3(half * 2f, RoomH, 0.2f));

            BuildWall(parent, name + "_WallN", new Vector3(localCenter.x, RoomH / 2f, z1), new Vector3(half * 2f, RoomH, 0.2f));
            BuildRoomDetails(parent, name, localCenter, new Vector2(half, half), accent);
        }

        /// <summary>A worldspace "CHAPTER 6 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch6BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 6 COMPLETE Canvas");
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
            label.text = "CHAPTER 6 COMPLETE";
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

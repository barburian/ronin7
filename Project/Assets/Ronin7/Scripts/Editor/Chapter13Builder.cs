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
    /// Chapter 13 ("The Sterile Reckoning") scene builder. Act IV opener. One self-contained scene along
    /// +Z: the Cairn briefing (voice-only sensing of the Concord Engine) -> a flat sterile-vault breach
    /// (outer corridor -> design-ward nurseries, light lab-security combat, Echo's dawning recognition
    /// that this is the room he and Ronin-7 were both made in) -> a mini-boss (a Redactor-class Program
    /// enforcer sent to silence the maker) -> the reckoning (Dr. Heris confesses she built Ronin-7, his
    /// killswitch, AND planted the saving flaw on purpose, then was conscripted to scale the same science
    /// into the Concord Engine and hid a seam in that too — Ladder A rung 5, closes the killswitch ladder)
    /// -> her defection (Ally #9, "work, not forgiveness") -> the annex reveal of Sallow, the
    /// absolution-body construct (Ally #10, THE TEN COMPLETE) -> the Ch16 hook.
    ///
    /// MINIMAL-NEW-CODE CHAPTER: no new ability, no new mechanic, no new runtime component. This is a
    /// story/dialogue chapter that completes the roster of ten. Every encounter uses the existing plain
    /// dialogue + reach + defeat-enemies step vocabulary (no DuelYield, no MemoryDiveController — there is
    /// no vision/duel beat here, unlike Ch7/Ch8/Ch11's mindspace dives).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch13</c>.
    ///
    /// LADDER A RUNG 5 (closes the killswitch ladder opened Ch2, sabotaged Ch4, named-insider Ch6, traced
    /// Ch7): delivered across ch13_beat2_opening/the_flaw/the_engine/the_ground — Dr. Heris built
    /// Ronin-7, built his killswitch, AND planted the saving flaw in it on purpose; she was then
    /// conscripted to scale the same suppression science into the Concord Engine and hid a deniable seam
    /// in that too. Delivered exactly once. The birth name "Soren" is never used (reserved for Ch16) and
    /// no other numbered ladder rung advances this chapter — see Chapter13Lines' class summary and
    /// Chapter13LinesTests.NoLine_MentionsSoren.
    ///
    /// AUDIT FIX #4 (the chapter's mandatory correctness fix, applied in Chapter13Lines, not here): the
    /// source script's Beat 2 Heris line omitted the iconic "I hope you will save us" whisper (the entire
    /// Ch08 vision payoff) from its actual Line: text and carried a duration mismatched to the words that
    /// remained. Fixed in ch13_beat2_the_vision; see Chapter13LinesTests.HerisLine_ContainsIHopeYouWillSaveUs.
    ///
    /// CREW-PRESENCE DECISION: mirrors Ch11/Ch12 — the full Cairn crew (Cassie-04, Sable, Kessler,
    /// Morrigan, Coral Vex, Mera Voss, Vess) speaks voice-only throughout (briefing + comm during the
    /// breach); only Ronin-7 (the player), the Redactor mini-boss, Dr. Heris, and Sallow get physical
    /// placement.
    ///
    /// REDACTOR MINI-BOSS: no Named prefab exists for a generic Program construct, so it uses the same
    /// generic <see cref="BuildEnemy"/> capsule pipeline as lab-security mooks (mirrors Ch9's Coil
    /// raiders / Ch12's skirmishers), just with a heavier stat block and the "Enforcer" speaker label for
    /// its erasure-cant barks. Per the source script it drops no blade-shadow (it is a construct, not a
    /// kept operative) — mechanized simply as one more <c>DefeatEnemies</c> step; no special "protect the
    /// door" tracking is added (out of scope for a minimal-code pass; the dialogue frames the stakes).
    ///
    /// HERIS / SALLOW STAGING: both resolve to real, already-baked Named prefabs (glob-confirmed on
    /// disk) and are placed via <see cref="Ch13PlaceStoryNpc"/> (no Health/combat, mirrors
    /// Ch9PlaceStoryNpc/Ch12PlaceStoryNpc) — built active from scene start, matching Ch12's Vale
    /// precedent for a non-combat story NPC that's present from the moment the room is reachable. Neither
    /// fights alongside the player this chapter, so neither gets <see cref="AllyCombatant"/> (that's
    /// reserved for allies who co-op fight, e.g. Ch9's Gryph/Rook) — they are recruited narratively via
    /// the outro's flags, matching Ch6/Ch7's Morrigan/Coral Vex precedent.
    ///
    /// ALLY UNLOCK: Dr. Heris (#9) and Sallow (#10) — the final two, completing the roster of ten.
    /// ChapterOutro sets ch13_complete + heris_recruited + sallow_recruited together (mirrors Ch9/Ch10's
    /// combined completion + recruit-flag convention). NO AbilityGranter — no new ability this chapter;
    /// <see cref="AttachPlayerAbilities"/> is still called so all five previously-earned abilities
    /// (weakpoint-sight/Overdrive/Phase-step/Unbroken/Mirror) persist onto the rig.
    ///
    /// HUB INCREMENT: <see cref="HubBuilder"/>'s SurgeryReactor dark-shell gate room (already wired to
    /// "ch13_complete" in <c>BuildHubMode</c>'s gate list) is filled by <c>Ch13FillSurgeryReactor</c> with
    /// idle Dr. Heris + Sallow — the last two allies aboard — mirroring the Ch6/Ch7/Ch9/Ch10 hub
    /// increments exactly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch13ScenePath = SceneFolder + "/Ch13_SterileReckoning.unity";
        private const string Ch13VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch13OuterHalfWidth = 6f;
        private const float Ch13CoreHalfWidth = 7f;
        private const float Ch13AnnexHalfWidth = 5f;

        // Named-cast prefabs. Heris/Sallow resolve to real, already-baked prefabs (glob-confirmed on disk
        // at authoring time) — no placeholders needed.
        private const string Ch13HerisPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Dr-Heris.prefab";
        private const string Ch13SallowPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Sallow.prefab";
        private const string Ch13EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 13 — The Sterile Reckoning", priority = 213)]
        public static void BuildChapter13SterileReckoning()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var labSecurityDef = Ch13EnsureLabSecurityDefinition();
            var redactorDef = Ch13EnsureRedactorDefinition();

            // ---- Lighting: THE STERILE — surgical white cooling to a clinical sterile-blue, the tonal
            // inverse of every place before it. No rot, no rust, no fog haze — the horror is cleanliness. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.92f, 0.94f, 0.98f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.85f, 0.88f, 0.94f);
            RenderSettings.fogDensity = 0.006f; // barely there — this place is clean, not hazy

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.95f, 0.96f, 1f), 1.2f, 10f);
            BuildAccentPointLight("CorridorLight0", new Vector3(-2f, 2.6f, 16f), new Color(0.9f, 0.93f, 1f), 1.3f, 14f);
            BuildAccentPointLight("CorridorLight1", new Vector3(2f, 2.6f, 26f), new Color(0.85f, 0.9f, 1f), 1.3f, 14f);
            BuildAccentPointLight("NurseryLight0", new Vector3(-3f, 2.4f, 40f), new Color(0.5f, 0.75f, 1f), 1.4f, 14f); // sterile blue seams
            BuildAccentPointLight("NurseryLight1", new Vector3(3f, 2.4f, 54f), new Color(0.45f, 0.7f, 1f), 1.4f, 14f);
            BuildAccentPointLight("ThresholdLight", new Vector3(0f, 2.4f, 62f), new Color(0.9f, 0.9f, 0.95f), 1.6f, 12f);
            BuildAccentPointLight("CoreLight0", new Vector3(-3f, 2.6f, 68f), new Color(1f, 1f, 1f), 1.8f, 16f); // brightest, most surgical
            BuildAccentPointLight("CoreLight1", new Vector3(3f, 2.6f, 76f), new Color(1f, 1f, 1f), 1.8f, 16f);
            BuildAccentPointLight("AnnexLight0", new Vector3(0f, 2.4f, 87f), new Color(0.55f, 0.75f, 1f), 1.5f, 14f); // Sallow's sterile blue

            // ---- World root: a flat, single-level sterile vault (no vertical descent — the chapter's
            // horror is clinical order, not depth). Four contiguous zones along +Z, each touching the
            // next's edge exactly (mirrors Ch9's HoldHall/TideDepths/ConstructionCore idiom) so there is
            // no floor gap for the player to fall through. ----
            var worldGo = new GameObject("SterileVault");
            var world = worldGo.transform;

            BuildFloorCeiling(world, "SpawnGround", new Vector3(0f, 0f, 4f), new Vector3(12f, 0f, 12f),
                new Color(0.82f, 0.85f, 0.9f), new Color(0.88f, 0.9f, 0.94f)); // z[-2,10]

            BuildFloorCeiling(world, "OuterCorridor", new Vector3(0f, 0f, 22f), new Vector3(Ch13OuterHalfWidth * 2f, 0f, 24f),
                new Color(0.8f, 0.83f, 0.88f), new Color(0.86f, 0.88f, 0.92f)); // z[10,34]
            BuildWall(world, "OuterCorridor_WallW", new Vector3(-Ch13OuterHalfWidth, RoomH / 2f, 22f), new Vector3(0.2f, RoomH, 24f));
            BuildWall(world, "OuterCorridor_WallE", new Vector3(Ch13OuterHalfWidth, RoomH / 2f, 22f), new Vector3(0.2f, RoomH, 24f));
            BuildWall(world, "OuterCorridor_WallS", new Vector3(0f, RoomH / 2f, -2f), new Vector3(12f, RoomH, 0.2f));
            Ch13BuildDesignWardRow(world, 12f, 32f, 6f);

            BuildFloorCeiling(world, "Nurseries", new Vector3(0f, 0f, 47f), new Vector3(Ch13OuterHalfWidth * 2f, 0f, 26f),
                new Color(0.78f, 0.83f, 0.92f), new Color(0.85f, 0.88f, 0.95f)); // z[34,60]
            BuildWall(world, "Nurseries_WallW", new Vector3(-Ch13OuterHalfWidth, RoomH / 2f, 47f), new Vector3(0.2f, RoomH, 26f));
            BuildWall(world, "Nurseries_WallE", new Vector3(Ch13OuterHalfWidth, RoomH / 2f, 47f), new Vector3(0.2f, RoomH, 26f));
            Ch13BuildSealedNurseryRow(world, 36f, 58f, 5f);

            BuildFloorCeiling(world, "LabCore", new Vector3(0f, 0f, 70f), new Vector3(Ch13CoreHalfWidth * 2f, 0f, 20f),
                new Color(0.92f, 0.94f, 0.98f), new Color(0.95f, 0.96f, 1f)); // z[60,80]
            BuildWall(world, "LabCore_WallW", new Vector3(-Ch13CoreHalfWidth, RoomH / 2f, 70f), new Vector3(0.2f, RoomH, 20f));
            BuildWall(world, "LabCore_WallE", new Vector3(Ch13CoreHalfWidth, RoomH / 2f, 70f), new Vector3(0.2f, RoomH, 20f));
            BuildProp(world, "SterileTable", new Vector3(0f, 0.5f, 68f), new Vector3(1.8f, 0.1f, 0.9f), new Color(0.95f, 0.96f, 1f));

            BuildFloorCeiling(world, "Annex", new Vector3(0f, 0f, 87f), new Vector3(Ch13AnnexHalfWidth * 2f, 0f, 14f),
                new Color(0.7f, 0.8f, 0.95f), new Color(0.78f, 0.85f, 0.97f)); // z[80,94]
            BuildWall(world, "Annex_WallW", new Vector3(-Ch13AnnexHalfWidth, RoomH / 2f, 87f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(world, "Annex_WallE", new Vector3(Ch13AnnexHalfWidth, RoomH / 2f, 87f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(world, "Annex_WallN", new Vector3(0f, RoomH / 2f, 94f), new Vector3(Ch13AnnexHalfWidth * 2f, RoomH, 0.2f));

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, every shipped ability chain (all five
            // self-gate; none are granted this chapter — minimal-new-code, no AbilityGranter). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 1f, 46f);
            bounds.radius = 105f;

            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act IV — no rack-wake beat, matching Ch9-12's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch13EchoBladePrefab);

            // ---- Lab security: inactive until the DefeatEnemies step auto-activates them (mirrors
            // Ch9/Ch12's mook convention). ----
            Vector3[] labSecurityPositions =
            {
                new Vector3(-3f, 0f, 22f), new Vector3(3f, 0f, 22f), new Vector3(0f, 0f, 27f),
            };
            var labSecurityHealths = new List<Object>();
            var labSecurityEnemies = new List<MeleeAttacker>();
            foreach (var pos in labSecurityPositions)
            {
                var e = BuildEnemy(pos, playerHealth, labSecurityDef);
                e.gameObject.SetActive(false);
                labSecurityHealths.Add(e.GetComponent<Health>());
                labSecurityEnemies.Add(e);
            }

            // ---- Hive cascade over the trio (enemy-variety retrofit, kept in the builder so
            // rebuilds stay correct): the suppression science that fractured Ch12's cradle sentinels
            // was scaled FROM these sterile levels — the same Attacking/Frozen/Conflicted desync on
            // the lab security sells that origin. Mirrors Ch12BuildHiveCascade. ----
            Ch13BuildHiveCascade(labSecurityEnemies);

            // ---- The Redactor: the mini-boss, generic-construct pipeline (no Named prefab exists for a
            // faceless Program enforcer — see class summary). Inactive until its own DefeatEnemies step. ----
            var redactor = BuildEnemy(new Vector3(0f, 0f, 62f), playerHealth, redactorDef);
            redactor.gameObject.name = "Redactor";
            redactor.gameObject.SetActive(false);

            // ---- Dr. Heris: real prefab, no combat, built active from scene start (mirrors Ch12's Vale
            // precedent for a non-combat story NPC present once the room is reachable). ----
            Ch13PlaceStoryNpc(Ch13HerisPrefab, new Vector3(0f, 0f, 72f), "Dr. Heris");

            // ---- Sallow: real prefab, no combat, built active from scene start (annex). ----
            Ch13PlaceStoryNpc(Ch13SallowPrefab, new Vector3(0f, 0f, 88f), "Sallow");

            // ---- Reach points. ----
            var wardsReachGo = new GameObject("DesignWardsReachPoint");
            wardsReachGo.transform.position = new Vector3(0f, 1f, 34f);
            var thresholdReachGo = new GameObject("LabThresholdReachPoint");
            thresholdReachGo.transform.position = new Vector3(0f, 1f, 60f);
            var annexReachGo = new GameObject("AnnexReachPoint");
            annexReachGo.transform.position = new Vector3(0f, 1f, 90f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch13BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch13_beat0_briefing", talkRef);
            var dlgBreach = Ch13BuildDialogue("Dialogue_Beat1_Breach", new Vector3(0f, 1f, 12f), "ch13_beat1_breach", talkRef);
            var dlgRecognition = Ch13BuildDialogue("Dialogue_Beat1_Recognition", new Vector3(0f, 1f, 44f), "ch13_beat1_recognition", talkRef);
            var dlgEnforcerIntro = Ch13BuildDialogue("Dialogue_Beat1B_EnforcerIntro", new Vector3(0f, 1f, 60f), "ch13_beat1b_enforcer_intro", talkRef);
            var dlgEnforcerDefeated = Ch13BuildDialogue("Dialogue_Beat1B_EnforcerDefeated", new Vector3(0f, 1f, 66f), "ch13_beat1b_enforcer_defeated", talkRef);
            var dlgOpening = Ch13BuildDialogue("Dialogue_Beat2_Opening", new Vector3(0f, 1f, 74f), "ch13_beat2_opening", talkRef);
            var dlgTheFlaw = Ch13BuildDialogue("Dialogue_Beat2_TheFlaw", new Vector3(0f, 1f, 75f), "ch13_beat2_the_flaw", talkRef);
            var dlgTheVision = Ch13BuildDialogue("Dialogue_Beat2_TheVision", new Vector3(0f, 1f, 76f), "ch13_beat2_the_vision", talkRef);
            var dlgTheEngine = Ch13BuildDialogue("Dialogue_Beat2_TheEngine", new Vector3(0f, 1f, 77f), "ch13_beat2_the_engine", talkRef);
            var dlgTheGround = Ch13BuildDialogue("Dialogue_Beat2_TheGround", new Vector3(0f, 1f, 78f), "ch13_beat2_the_ground", talkRef);
            var dlgDefection = Ch13BuildDialogue("Dialogue_Beat3_Defection", new Vector3(0f, 1f, 80f), "ch13_beat3_defection", talkRef);
            var dlgIntroduceSallow = Ch13BuildDialogue("Dialogue_Beat4_IntroduceSallow", new Vector3(0f, 1f, 88f), "ch13_beat4_introduce_sallow", talkRef);
            var dlgMechanic = Ch13BuildDialogue("Dialogue_Beat4_Mechanic", new Vector3(0f, 1f, 89f), "ch13_beat4_mechanic", talkRef);
            var dlgComplete = Ch13BuildDialogue("Dialogue_Beat4_Complete", new Vector3(0f, 1f, 90f), "ch13_beat4_complete", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ch13_complete AND both recruit
            // flags together, mirroring how Ch9/Ch10 combined their ally-recruit flags with completion. ----
            var completeCanvasGo = Ch13BuildCompleteCanvas(new Vector3(0f, 1.4f, 93f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 92f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 3;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch13_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "heris_recruited";
            flagsProp.GetArrayElementAtIndex(2).stringValue = "sallow_recruited";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 13 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 20;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the sensing, the briefing)", dlgBriefing);
            AuthorReachStep(steps, n++, "ReachTrigger: The Design Wards", wardsReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Sterile Vault (breach begins)", dlgBreach);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Lab Security", labSecurityHealths);
            AuthorDialogueStep(steps, n++, "Beat1: The Nurseries (Echo's dawning recognition)", dlgRecognition);
            AuthorReachStep(steps, n++, "ReachTrigger: The Lab Threshold", thresholdReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1B: The Redactor (mini-boss intro)", dlgEnforcerIntro);
            AuthorDefeatStep(steps, n++, "Beat1B: The Duel (Redactor, mini-boss)", new List<Object> { redactor.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat1B: No Shadow Came Off It (aftermath)", dlgEnforcerDefeated);
            AuthorDialogueStep(steps, n++, "Beat2: The Maker (Heris opens the reckoning)", dlgOpening);
            AuthorDialogueStep(steps, n++, "Beat2: The Flaw (she planted it on purpose)", dlgTheFlaw);
            AuthorDialogueStep(steps, n++, "Beat2: The Vision (Ch8 payoff, AUDIT FIX #4)", dlgTheVision);
            AuthorDialogueStep(steps, n++, "Beat2: The Engine (the Concord Engine reveal)", dlgTheEngine);
            AuthorDialogueStep(steps, n++, "Beat2: The Ground (Ladder A rung 5 closes)", dlgTheGround);
            AuthorDialogueStep(steps, n++, "Beat3: Heris Defects (Ally #9, work not forgiveness)", dlgDefection);
            AuthorReachStep(steps, n++, "ReachTrigger: The Annex", annexReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat4: Introduce Sallow", dlgIntroduceSallow);
            AuthorDialogueStep(steps, n++, "Beat4: The Absolution Mechanic (the reframe)", dlgMechanic);
            AuthorDialogueStep(steps, n++, "Beat4: The Ten Complete (Ally #10)", dlgComplete);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flags + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, lab-core ambience bed, console/mood lights. ----
            var labCoreAmbience = BuildAmbienceLayer("LabCoreAmbience", new Vector3(0f, 1f, 70f), 3f, 10f, 0.5f);
            labCoreAmbience.GetComponent<AudioSource>().clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Art/Generated/Audio/SFX/labcore_hum.wav");
            AddConsoleFlicker("CoreLight0", seed: 141f); // "brightest, most surgical"
            AddAmbientPulse("NurseryLight0", periodSeconds: 7.6f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch13ScenePath);
            EnsureScenesInBuild(Ch13ScenePath);

            Debug.Log($"[Space Samurai] Chapter 13 built at {Ch13ScenePath}. " +
                      "The sterile-vault breach (outer corridor -> design-ward nurseries, lab-security " +
                      "combat, Echo's dawning recognition) -> the Redactor mini-boss (sent to silence the " +
                      "maker) -> the reckoning (Dr. Heris: she built Ronin-7, his killswitch, and planted " +
                      "the saving flaw on purpose; conscripted architect of the Concord Engine; Ladder A " +
                      "rung 5 closes) -> her defection (Ally #9) -> the annex (Sallow, the absolution-body " +
                      "construct, Ally #10 — THE TEN COMPLETE) -> the Ch16 hook. 20 mission steps. NO new " +
                      "ability. Dr. Heris/Sallow resolve to real Named prefabs.");
        }

        /// <summary>Wires a <see cref="HiveCascadeController"/> over the lab-security trio (mirrors
        /// Ch12BuildHiveCascade — built active; harmless while members are inactive, the
        /// DefeatEnemies step's auto-activation starts the fight).</summary>
        internal static HiveCascadeController Ch13BuildHiveCascade(List<MeleeAttacker> members)
        {
            var go = new GameObject("SterileHiveCascade");
            var hive = go.AddComponent<HiveCascadeController>();
            var so = new SerializedObject(hive);
            SetObjectRefList(so, "members", members.ConvertAll(m => (Object)m));
            so.ApplyModifiedPropertiesWithoutUndo();
            return hive;
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch9/Ch12's Ensure* convention). ----

        private static EnemyDefinition Ch13EnsureLabSecurityDefinition()
        {
            const string path = DataFolder + "/Ch13LabSecurity.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 50f;
            def.damage = 8f;
            def.moveSpeed = 1.4f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch13EnsureRedactorDefinition()
        {
            const string path = DataFolder + "/Ch13Redactor.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            // Smaller in scale than an Act-closing keeper-boss but pointed and thematic (source script:
            // "the chapter's only true fight before the reckoning") — heavier than a mook, lighter than
            // a full boss like Ch9's Vane or Ch11's Aldric.
            def.maxHealth = 180f;
            def.damage = 16f;
            def.moveSpeed = 1.3f;
            def.attackCooldown = 1.0f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch13 voice clips ourselves. ----

        private static DialoguePlayer Ch13BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter13Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch13WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter13] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch13WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter13Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch13VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch13VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement. ----

        /// <summary>Mirrors Ch9PlaceStoryNpc/Ch12PlaceStoryNpc: a decorative/story NPC with no combat
        /// component (see class summary — neither Heris nor Sallow fights this chapter).</summary>
        private static GameObject Ch13PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---- The sterile vault: tinted set-dressing rows (mirrors Ch9's rack-row idiom). ----

        /// <summary>Design-ward props lining the outer corridor — clean tables and instrument trays laid
        /// out with professional care, per the source script's tiered clinical-horror staging.</summary>
        private static void Ch13BuildDesignWardRow(Transform parent, float zStart, float zEnd, float spacing)
        {
            var tableColor = new Color(0.88f, 0.9f, 0.94f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                BuildProp(parent, "DesignTable", new Vector3(-4.5f, 0.5f, z), new Vector3(1.4f, 0.1f, 0.7f), tableColor);
                BuildProp(parent, "InstrumentTray", new Vector3(4.5f, 0.4f, z), new Vector3(0.8f, 0.08f, 0.5f), tableColor);
            }
        }

        /// <summary>Sealed-nursery cradle props lining the mid vault — small cradles in filtered
        /// sterile-blue light, the obscenity of the place children were designed and switched.</summary>
        private static void Ch13BuildSealedNurseryRow(Transform parent, float zStart, float zEnd, float spacing)
        {
            var cradleColor = new Color(0.55f, 0.75f, 0.95f);
            for (float z = zStart; z <= zEnd; z += spacing)
            {
                BuildProp(parent, "SealedCradle", new Vector3(-5f, 0.6f, z), new Vector3(0.6f, 1.2f, 0.6f), cradleColor);
                BuildProp(parent, "SealedCradle", new Vector3(5f, 0.6f, z), new Vector3(0.6f, 1.2f, 0.6f), cradleColor);
            }
        }

        /// <summary>A worldspace "CHAPTER 13 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch13BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 13 COMPLETE Canvas");
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
            label.text = "CHAPTER 13 COMPLETE";
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

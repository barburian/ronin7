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
    /// Chapter 11 ("Ghosts and Origins") scene builder. Act III. One self-contained scene along +Z: the
    /// Cairn briefing (voice-only) -> the leviathan bone-canyon traverse (ribs/spine tiers, up to the
    /// arkship threshold) -> a <see cref="MemoryDiveController"/> dive into the node's narcosis
    /// dreamscape (ghosts of the fallen, the buried "younger self" seed, a duel against the eldest
    /// keeper Aldric/Knight-1 that grants Unbroken) -> the arkship origin core (the clone-lineage reveal,
    /// Echo alone with comm cut) -> the cradle (the dreaming archive herself put down, comm restored) ->
    /// back in the real canyon for the homecoming beat and the Ch12 hook-out. NO ally recruited (the
    /// node is freed, not recruited) and NO new input action (Unbroken is passive).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch11</c>.
    ///
    /// CREW-PRESENCE DECISION: canon has Ronin-7 alone the instant the dreamscape takes him ("Comm's
    /// gone... It's just us now"), and even during the canyon traverse the whole crew (Gryph, Coral Vex,
    /// Vess, Sable, Cassie-04) speaks only over comm from the ship/canyon mouth — unlike Ch9/Ch10, NO
    /// crew member gets physical placement at all this chapter. Every line plays as a plain
    /// <see cref="DialoguePlayer"/> with a comm-tagged speaker recorded under their plain name (matching
    /// Chapter11Lines' documented convention). Only Ronin-7 (the player) and the dream-cast (Aldric, the
    /// ghost-manifestations, Kira, the Younger Self, the Dreaming Archive) are physically placed.
    ///
    /// DREAMSCAPE / MEMORYDIVE DECISION: the whole narcosis sequence (ghosts, Aldric's duel, the arkship
    /// origin core, the cradle) lives under ONE <see cref="MemoryDiveController"/> island offset far from
    /// the real canyon (mirrors Ch7's mindspace-duel idiom and Ch8's "two rooms under one dive" island,
    /// extended here to three contiguous rooms: DreamscapeArena -> ArkshipCore -> Cradle). The dive is
    /// entered once (crossing the skull-dome threshold cuts comm) and exited once (after the second kill
    /// restores it) — matching the source script's "killing the keeper does NOT end the dream; only
    /// putting down the archive herself does." <see cref="MemoryFlashbackController"/> supplies the
    /// dream's fog/ambient treatment, reapplied on every <c>EnterDive</c> per its own contract.
    ///
    /// GHOST-MANIFESTATION / DREAMPHANTOM DECISION: two generic "the dead keep interrupting" enemies
    /// fight alongside Aldric in the SAME <c>DefeatEnemies</c> step (built via the shared generic
    /// <see cref="BuildEnemy"/> pipeline, then <c>AddComponent&lt;DreamPhantom&gt;()</c> — the exact
    /// idiom used elsewhere for memory-flash combat). Kira and the Younger Self are NON-combat:
    /// no Health/Enemy, purely a <see cref="StoryNpc"/> anchor tinted with
    /// <c>MemoryFlashbackController.MakeGhostMaterial()</c> for the dialogue beat to play against — the
    /// source script's "walk through, don't fight" rule for these two is honored narratively (the player
    /// is never blocked or attacked by them; there is nothing to fight).
    ///
    /// DREAMRECKONINGTRIGGER DECISION: <see cref="DreamReckoningTrigger"/>'s <c>Acknowledge()</c> is not
    /// self-firing (unlike <c>AbilityGranter</c>/<c>MemoryDiveEntryTrigger</c>, it has no <c>OnEnable</c>
    /// hook — and adding one would break its existing unit tests, which call <c>Acknowledge()</c>
    /// explicitly after construction). Rather than invent a new self-firing wrapper for one beat, its
    /// call is wired via <c>UnityEventTools.AddPersistentListener</c> onto <c>ChapterOutro.OnActivated</c>
    /// (the exact plumbing Ch7-10 already use to fire <c>CampaignFlagSetter.SetFlags</c> at the same
    /// moment): as the chapter closes, both ghost-manifestation phantoms still lingering are dissolved
    /// and "ch11_dream_ended" is set — a faithful, low-risk reuse of the component's real public API.
    ///
    /// ALDRIC / DREAMING ARCHIVE STAGING: Aldric resolves to the real <c>Aldric_Knight-1.prefab</c>
    /// (glob-confirmed on disk), built with the heaviest/slowest stat block in the saga (see
    /// <see cref="Ch11EnsureAldricDefinition"/>) — "the oldest, heaviest, most archaic school the player
    /// faces." He is a plain <see cref="Enemy"/> kill (source script: "cannot be freed, talked down,
    /// spared, or recruited... the ONLY resolution is to put him down"), inactive until the confrontation
    /// dialogue plays, mirroring Ch9/Ch10's "no separate activation Trigger" DefeatEnemies convention.
    /// The Dreaming Archive resolves to the real, already-baked Ethereal-archetype
    /// <c>The-Dreaming-Archive.prefab</c>; per the source's "NOT a boss fight... the only mercy left is
    /// the same door," her <see cref="EnemyDefinition"/> is a single symbolic hit (1 HP, 0 damage, never
    /// attacks) rather than a real fight.
    ///
    /// YOUNGER SELF CASTING: resolves to the existing <c>Ronin-7_Cipher_Soren.prefab</c> mesh (the
    /// correct visual asset for "Ronin-7 before the Program, no augments, no scars") — its filename is
    /// a dev-facing asset path only, never surfaced to the player; the in-scene <c>StoryNpc.displayName</c>
    /// stays "Younger Self" and no dialogue line names the character, per the source script's explicit
    /// "CRITICAL: never name Soren here."
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch11ScenePath = SceneFolder + "/Ch11_GhostsAndOrigins.unity";
        private const string Ch11VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const float Ch11TierHalfWidth = 6f;

        // Named-cast prefabs. Aldric/Kira/The-Dreaming-Archive resolve to real, already-baked prefabs
        // (glob-confirmed on disk at authoring time). Younger Self reuses the existing
        // Ronin-7_Cipher_Soren.prefab mesh (see the class summary's casting note) — its path is a
        // dev-facing asset reference only.
        private const string Ch11AldricPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Aldric_Knight-1.prefab";
        private const string Ch11KiraPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kira-Dusk.prefab";
        private const string Ch11YoungerSelfPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab";
        private const string Ch11DreamingArchivePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/The-Dreaming-Archive.prefab";
        private const string Ch11EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 11 — Ghosts and Origins", priority = 211)]
        public static void BuildChapter11GhostsAndOrigins()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var ghostDef = Ch11EnsureGhostManifestationDefinition();
            var aldricDef = Ch11EnsureAldricDefinition();
            var archiveDef = Ch11EnsureDreamingArchiveDefinition();

            // ---- Lighting: cold, bone-grey daylight in the upper canyon, cooling further toward the
            // arkship threshold; the dreamscape island gets its own treatment via MemoryFlashbackController. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.62f, 0.68f);
            light.intensity = 0.3f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.08f, 0.08f, 0.1f);
            RenderSettings.fogDensity = 0.018f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.7f, 0.72f, 0.8f), 1f, 10f);
            BuildAccentPointLight("Tier1Light", new Vector3(2f, 1.4f, 22f), new Color(0.65f, 0.68f, 0.76f), 1.2f, 12f);
            BuildAccentPointLight("Tier2Light", new Vector3(-2f, 0.4f, 40f), new Color(0.55f, 0.6f, 0.7f), 1.3f, 12f);
            BuildAccentPointLight("Tier3Light", new Vector3(1f, -0.6f, 58f), new Color(0.35f, 0.5f, 0.65f), 1.5f, 14f);
            BuildAccentPointLight("ThresholdLight", new Vector3(0f, -1.6f, 72f), new Color(0.3f, 0.42f, 0.55f), 1.6f, 12f);

            // ---- World root: the leviathan bone-canyon, a switchback rib/spine traverse. ----
            var worldGo = new GameObject("BoneCanyon");
            var world = worldGo.transform;

            BuildFloorCeiling(world, "SpawnGround", new Vector3(0f, 0f, 4f), new Vector3(16f, 0f, 12f),
                new Color(0.2f, 0.2f, 0.22f), new Color(0.08f, 0.08f, 0.09f));

            Vector3[] tiers =
            {
                new Vector3(2f, -1f, 22f),   // Tier 1 — upper rib traverse
                new Vector3(-2f, -2f, 40f),  // Tier 2 — deeper spine
                new Vector3(1f, -3f, 58f),   // Tier 3 — the arkship comes into view
            };
            var boneColor = new Color(0.5f, 0.48f, 0.44f);
            var vaultColor = new Color(0.24f, 0.3f, 0.4f);
            for (int i = 0; i < tiers.Length; i++)
            {
                float t = Mathf.InverseLerp(0, tiers.Length - 1, i);
                Color tierColor = Color.Lerp(boneColor, vaultColor, t);
                Ch11BuildTier(world, $"Tier{i + 1}", tiers[i], tierColor);
            }
            Ch11BuildRamp(world, "Ramp0", new Vector3(0f, 0f, 10f), tiers[0], Ch11TierHalfWidth * 2f);
            for (int i = 0; i < tiers.Length - 1; i++)
                Ch11BuildRamp(world, $"Ramp{i + 1}", tiers[i], tiers[i + 1], Ch11TierHalfWidth * 2f);

            var thresholdPos = new Vector3(0f, -4f, 72f);
            Ch11BuildTier(world, "Threshold", thresholdPos, new Color(0.18f, 0.24f, 0.32f));
            Ch11BuildRamp(world, "RampThreshold", tiers[2], thresholdPos, Ch11TierHalfWidth * 2f);

            // Impaled arkship hull, visible from Tier 3 on (the architecture reveal).
            BuildProp(world, "ArkshipHull", tiers[2] + new Vector3(-6f, 2f, 6f), new Vector3(3f, 4f, 10f), new Color(0.3f, 0.32f, 0.36f));

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, and every shipped ability chain
            // (weakpoint-sight/Overdrive/Phase-step self-gate; Unbroken is granted this chapter). Bounds
            // must cover both the real canyon (z 0-90) and the dreamscape island offset at z=300. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 150f);
            bounds.radius = 260f;

            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act III — no rack-wake beat, matching Ch9-10's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch11EchoBladePrefab);

            // ---- The dreamscape: three contiguous rooms (DreamscapeArena -> ArkshipCore -> Cradle)
            // under one MemoryDiveController island, offset far from the real canyon. Starts fully
            // inactive; the whole root cascades active on dive entry. ----
            var diveGo = new GameObject("Dreamscape");
            var dive = diveGo.transform;
            dive.position = new Vector3(0f, 0f, 300f);
            var diveFlashback = diveGo.AddComponent<MemoryFlashbackController>();

            // DreamscapeArena: local z spans -2..34 (ghosts + Aldric's duel).
            BuildFloorCeiling(dive, "DreamscapeArena", new Vector3(0f, 0f, 16f), new Vector3(20f, 0f, 36f),
                new Color(0.14f, 0.14f, 0.18f), new Color(0.06f, 0.06f, 0.08f));
            BuildWall(dive, "Arena_WallW", new Vector3(-10f, RoomH / 2f, 16f), new Vector3(0.2f, RoomH, 36f));
            BuildWall(dive, "Arena_WallE", new Vector3(10f, RoomH / 2f, 16f), new Vector3(0.2f, RoomH, 36f));
            BuildWall(dive, "Arena_WallS", new Vector3(0f, RoomH / 2f, -2f), new Vector3(20f, RoomH, 0.2f));
            BuildAccentPointLight("ArenaLight0", dive.position + new Vector3(-4f, 2.2f, 8f), new Color(0.5f, 0.5f, 0.6f), 1.1f, 14f);
            BuildAccentPointLight("ArenaLight1", dive.position + new Vector3(4f, 2.2f, 22f), new Color(0.55f, 0.45f, 0.55f), 1.2f, 14f);
            BuildAccentPointLight("AldricLight", dive.position + new Vector3(0f, 2.4f, 30f), new Color(0.7f, 0.2f, 0.18f), 1.4f, 12f);

            // ArkshipCore: local z spans 34..50 (the origin-record reveal).
            BuildFloorCeiling(dive, "ArkshipCore", new Vector3(0f, 0f, 42f), new Vector3(16f, 0f, 16f),
                new Color(0.04f, 0.06f, 0.08f), new Color(0.02f, 0.03f, 0.04f));
            BuildWall(dive, "Core_WallW", new Vector3(-8f, RoomH / 2f, 42f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(dive, "Core_WallE", new Vector3(8f, RoomH / 2f, 42f), new Vector3(0.2f, RoomH, 16f));
            BuildAccentPointLight("ArkshipCoreLight", dive.position + new Vector3(0f, 2.4f, 42f), new Color(0.35f, 0.9f, 0.95f), 1.8f, 14f);
            BuildHologram(dive, new Vector3(0f, 1.5f, 42f));

            // Cradle: local z spans 50..64 (the second kill).
            BuildFloorCeiling(dive, "Cradle", new Vector3(0f, 0f, 57f), new Vector3(14f, 0f, 14f),
                new Color(0.05f, 0.05f, 0.06f), new Color(0.02f, 0.02f, 0.03f));
            BuildWall(dive, "Cradle_WallW", new Vector3(-7f, RoomH / 2f, 57f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(dive, "Cradle_WallE", new Vector3(7f, RoomH / 2f, 57f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(dive, "Cradle_WallN", new Vector3(0f, RoomH / 2f, 64f), new Vector3(14f, RoomH, 0.2f));
            BuildAccentPointLight("CradleLight", dive.position + new Vector3(0f, 1.6f, 57f), new Color(0.4f, 0.28f, 0.15f), 1f, 10f);

            // Ghost anchors (non-combat): Kira and the Younger Self, tinted with the memory-echo ghost
            // material. Purely decorative — no Health/Enemy, nothing to fight or block the player.
            Ch11PlaceGhostNpc(Ch11KiraPrefab, dive.position + new Vector3(-4f, 0f, 6f), "Kira");
            Ch11PlaceGhostNpc(Ch11YoungerSelfPrefab, dive.position + new Vector3(4f, 0f, 14f), "Younger Self");

            // Ghost-manifestation combat texture ("the dead keep interrupting the duel") — generic
            // enemies via the shared pipeline, made phase-cycling via DreamPhantom.
            // Inactive until Aldric's DefeatEnemies step activates the whole encounter together.
            var ghostManifestations = new List<Health>();
            var ghostPhantoms = new List<DreamPhantom>();
            Vector3[] ghostPositions = { dive.position + new Vector3(-3f, 0f, 26f), dive.position + new Vector3(3f, 0f, 26f) };
            foreach (var pos in ghostPositions)
            {
                var e = BuildEnemy(pos, playerHealth, ghostDef);
                var phantom = e.gameObject.AddComponent<DreamPhantom>();
                e.gameObject.SetActive(false);
                ghostManifestations.Add(e.GetComponent<Health>());
                ghostPhantoms.Add(phantom);
            }

            // ---- Aldric / Knight-1: the boss, inactive until the confrontation dialogue plays and the
            // DefeatEnemies step's own auto-activation reveals him (mirrors Ch9/Ch10). Cannot be freed,
            // talked down, or recruited — a plain Enemy kill. ----
            var aldric = Ch11BuildNamedBoss(Ch11AldricPrefab, dive.position + new Vector3(0f, 0f, 30f), "Aldric / Knight-1", aldricDef, playerHealth);
            aldric.gameObject.transform.SetParent(dive, true);
            aldric.gameObject.SetActive(false);

            // ---- The Dreaming Archive: the second kill, NOT a boss fight — a single symbolic hit
            // (see Ch11EnsureDreamingArchiveDefinition). Inactive until its own DefeatEnemies step. ----
            var dreamingArchive = Ch11BuildNamedBoss(Ch11DreamingArchivePrefab, dive.position + new Vector3(0f, 0f, 57f), "The Dreaming Archive", archiveDef, playerHealth);
            dreamingArchive.gameObject.transform.SetParent(dive, true);
            dreamingArchive.gameObject.SetActive(false);

            diveGo.SetActive(false);

            // ---- Dive entry/exit points and controller (mirrors Ch7/Ch8 exactly). ----
            var diveEntryPointGo = new GameObject("DreamscapeEntryPoint");
            diveEntryPointGo.transform.SetPositionAndRotation(dive.position + new Vector3(0f, 1f, 0f), Quaternion.identity);

            var diveExitPointGo = new GameObject("DreamscapeExitPoint");
            diveExitPointGo.transform.SetPositionAndRotation(thresholdPos + new Vector3(0f, 1f, 4f), Quaternion.identity);

            var diveControllerGo = new GameObject("DreamscapeDive");
            var diveController = diveControllerGo.AddComponent<MemoryDiveController>();
            var diveSo = new SerializedObject(diveController);
            SetObjectRef(diveSo, "diveRoot", diveGo);
            SetObjectRef(diveSo, "diveEntryPoint", diveEntryPointGo.transform);
            SetObjectRef(diveSo, "diveExitPoint", diveExitPointGo.transform);
            SetObjectRef(diveSo, "rigRoot", rig.transform);
            SetObjectRef(diveSo, "flashback", diveFlashback);
            diveSo.ApplyModifiedPropertiesWithoutUndo();

            var enterDreamscapeGo = new GameObject("EnterDreamscapeTrigger");
            var enterDreamscapeTrigger = enterDreamscapeGo.AddComponent<MemoryDiveEntryTrigger>();
            var enterSo = new SerializedObject(enterDreamscapeTrigger);
            SetObjectRef(enterSo, "dive", diveController);
            enterSo.ApplyModifiedPropertiesWithoutUndo();
            enterDreamscapeGo.SetActive(false);

            var exitDreamscapeGo = new GameObject("ExitDreamscapeTrigger");
            var exitDreamscapeTrigger = exitDreamscapeGo.AddComponent<MemoryDiveExitTrigger>();
            var exitSo = new SerializedObject(exitDreamscapeTrigger);
            SetObjectRef(exitSo, "dive", diveController);
            exitSo.ApplyModifiedPropertiesWithoutUndo();
            exitDreamscapeGo.SetActive(false);

            // ---- Unbroken ability grant: activated alongside Aldric's defeat. ----
            var unbrokenGranterGo = new GameObject("UnbrokenGranter");
            var unbrokenGranter = unbrokenGranterGo.AddComponent<AbilityGranter>();
            var granterSo = new SerializedObject(unbrokenGranter);
            granterSo.FindProperty("abilityId").stringValue = AbilityId.Unbroken;
            granterSo.ApplyModifiedPropertiesWithoutUndo();
            unbrokenGranterGo.SetActive(false);

            // ---- Dream reckoning: dissolves any lingering ghost-manifestation phantoms and sets
            // "ch11_dream_ended" when the chapter outro fires (see the class summary's wiring decision). ----
            var reckoningGo = new GameObject("DreamReckoning");
            var reckoning = reckoningGo.AddComponent<DreamReckoningTrigger>();
            var reckoningSo = new SerializedObject(reckoning);
            SetObjectRefList(reckoningSo, "phantoms", new List<Object> { ghostPhantoms[0], ghostPhantoms[1] });
            reckoningSo.FindProperty("acknowledgedFlag").stringValue = "ch11_dream_ended";
            reckoningSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Reach points. ----
            var midCanyonReachGo = new GameObject("MidCanyonReachPoint");
            midCanyonReachGo.transform.position = tiers[2] + new Vector3(0f, 1f, 0f);
            var thresholdReachGo = new GameObject("ThresholdReachPoint");
            thresholdReachGo.transform.position = thresholdPos + new Vector3(0f, 1f, 0f);
            var arkshipCoreReachGo = new GameObject("ArkshipCoreReachPoint");
            arkshipCoreReachGo.transform.position = dive.position + new Vector3(0f, 1f, 40f);
            var cradleReachGo = new GameObject("CradleReachPoint");
            cradleReachGo.transform.position = dive.position + new Vector3(0f, 1f, 52f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch11BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch11_beat0_briefing", talkRef);
            var dlgDescent = Ch11BuildDialogue("Dialogue_Beat1_Descent", new Vector3(0f, 1f, 12f), "ch11_beat1_descent", talkRef);
            var dlgArkship = Ch11BuildDialogue("Dialogue_Beat1_Arkship", tiers[2] + new Vector3(0f, 1f, 2f), "ch11_beat1_arkship", talkRef);
            var dlgGhosts = Ch11BuildDialogue("Dialogue_Beat2_Ghosts", dive.position + new Vector3(0f, 1f, 4f), "ch11_beat2_ghosts", talkRef);
            var dlgYoungerSelf = Ch11BuildDialogue("Dialogue_Beat2_YoungerSelf", dive.position + new Vector3(3f, 1f, 15f), "ch11_beat2_youngerself", talkRef);
            var dlgKeeper = Ch11BuildDialogue("Dialogue_Beat2_Keeper", dive.position + new Vector3(0f, 1f, 27f), "ch11_beat2_keeper", talkRef);
            var dlgKill = Ch11BuildDialogue("Dialogue_Beat2_Kill", dive.position + new Vector3(0f, 1f, 30f), "ch11_beat2_kill", talkRef);
            var dlgUnbroken = Ch11BuildDialogue("Dialogue_Beat2_Unbroken", dive.position + new Vector3(0f, 1f, 32f), "ch11_beat2_unbroken", talkRef);
            var dlgIntro3 = Ch11BuildDialogue("Dialogue_Beat3_Intro", dive.position + new Vector3(0f, 1f, 35f), "ch11_beat3_intro", talkRef);
            var dlgReveal = Ch11BuildDialogue("Dialogue_Beat3_Reveal", dive.position + new Vector3(0f, 1f, 42f), "ch11_beat3_reveal", talkRef);
            var dlgEchoKin = Ch11BuildDialogue("Dialogue_Beat3_EchoKin", dive.position + new Vector3(0f, 1f, 45f), "ch11_beat3_echokin", talkRef);
            var dlgMercy = Ch11BuildDialogue("Dialogue_Beat4_Mercy", dive.position + new Vector3(0f, 1f, 55f), "ch11_beat4_mercy", talkRef);
            var dlgHomecoming = Ch11BuildDialogue("Dialogue_Beat4_Homecoming", thresholdPos + new Vector3(0f, 1f, 6f), "ch11_beat4_homecoming", talkRef);
            var dlgTargetList = Ch11BuildDialogue("Dialogue_Beat4_TargetList", thresholdPos + new Vector3(0f, 1f, 10f), "ch11_beat4_targetlist", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ONLY ch11_complete — no
            // recruit flag (the node is freed, not recruited; NO ally unlock this chapter). ----
            var completeCanvasGo = Ch11BuildCompleteCanvas(thresholdPos + new Vector3(0f, 1.4f, 14f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = thresholdPos + new Vector3(0f, 1f, 13f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch11_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(reckoning.Acknowledge));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 11 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 24;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorDialogueStep(steps, n++, "Beat1: The Bone-Canyon (descent begins)", dlgDescent);
            AuthorReachStep(steps, n++, "ReachTrigger: The Mid-Canyon (arkship comes into view)", midCanyonReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Arkship (Echo reads the old metal)", dlgArkship);
            AuthorReachStep(steps, n++, "ReachTrigger: The Threshold (skull-dome, comm about to cut)", thresholdReachGo.transform, 5f);
            AuthorTriggerStep(steps, n++, "Trigger: Enter the Dreamscape (comm cuts)", enterDreamscapeGo);
            AuthorDialogueStep(steps, n++, "Beat2: The Ghosts (Echo's rule, Kira's lure)", dlgGhosts);
            AuthorDialogueStep(steps, n++, "Beat2: The Younger Self (buried-self seed)", dlgYoungerSelf);
            AuthorDialogueStep(steps, n++, "Beat2: The Keeper (Aldric confrontation)", dlgKeeper);
            AuthorDefeatStep(steps, n++, "Beat2: The Duel (Aldric/Knight-1 + ghost-manifestations)",
                new List<Object> { aldric.GetComponent<Health>(), ghostManifestations[0], ghostManifestations[1] });
            AuthorDialogueStep(steps, n++, "Beat2: Rest, Old Man (aftermath)", dlgKill);
            AuthorTriggerStep(steps, n++, "Trigger: Unbroken Granted (Aldric's freed blade-shadow)", unbrokenGranterGo);
            AuthorDialogueStep(steps, n++, "Beat2: Unbroken (Echo names the gift)", dlgUnbroken);
            AuthorDialogueStep(steps, n++, "Beat3: The Dream Isn't Over (Echo redirects)", dlgIntro3);
            AuthorReachStep(steps, n++, "ReachTrigger: The Arkship Core", arkshipCoreReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat3: The Origin Record (clone-lineage reveal)", dlgReveal);
            AuthorDialogueStep(steps, n++, "Beat3: Echo's Own Kin (the shadow-make reveal)", dlgEchoKin);
            AuthorReachStep(steps, n++, "ReachTrigger: The Cradle", cradleReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat4: The Mercy (Ronin kneels)", dlgMercy);
            AuthorDefeatStep(steps, n++, "Beat4: The Second Kill (the Dreaming Archive)", new List<Object> { dreamingArchive.GetComponent<Health>() });
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Dreamscape (comm restored)", exitDreamscapeGo);
            AuthorDialogueStep(steps, n++, "Beat4: Homecoming (Sable, Coral Vex)", dlgHomecoming);
            AuthorDialogueStep(steps, n++, "Beat4: The Target List (Ch12 hooks)", dlgTargetList);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Immersion retrofit: room reverb, arkship/cradle ambience beds, console/mood lights. ----
            BuildAmbienceLayer("ArkshipCoreAmbience", new Vector3(0f, 2.4f, 42f), 5f, 16f, 0.4f);
            BuildAmbienceLayer("CradleDreadAmbience", new Vector3(0f, 1.6f, 57f), 4f, 14f, 0.4f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();
            AddConsoleFlicker("ArkshipCoreLight", seed: 121f);
            AddAmbientPulse("CradleLight", periodSeconds: 7.2f);
            ReverbZonePlacer.AutoTagInteriorVolumes();
            ReverbZonePlacer.PlaceReverbZonesForInteriorVolumes();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch11ScenePath);
            EnsureScenesInBuild(Ch11ScenePath);

            Debug.Log($"[Space Samurai] Chapter 11 built at {Ch11ScenePath}. " +
                      "The bone-canyon traverse (3 rib/spine tiers -> the arkship threshold) -> a single " +
                      "MemoryDiveController island (DreamscapeArena: ghosts + Aldric/Knight-1's duel " +
                      "grants Unbroken -> ArkshipCore: the clone-lineage reveal, comm cut, Echo alone -> " +
                      "Cradle: the Dreaming Archive put down, comm restored) -> homecoming + Ch12 hooks " +
                      "(cryo-vault, the face question). 24 mission steps. NO ally recruited, NO input " +
                      "action added (Unbroken is passive). Aldric/Kira/The-Dreaming-Archive resolve to " +
                      "real Named prefabs; Younger Self reuses the Ronin-7_Cipher_Soren.prefab mesh.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch10EnsureSeverDefinition). ----

        private static EnemyDefinition Ch11EnsureGhostManifestationDefinition()
        {
            const string path = DataFolder + "/Ch11GhostManifestation.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 35f;
            def.damage = 7f;
            def.moveSpeed = 1.3f;
            def.attackCooldown = 1.0f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch11EnsureAldricDefinition()
        {
            const string path = DataFolder + "/Ch11Aldric.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 360f;
            def.damage = 32f;         // heaviest hit in the saga so far
            def.moveSpeed = 0.9f;     // slow, committed, archaic
            def.attackCooldown = 1.5f; // monumental wind-ups
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch11EnsureDreamingArchiveDefinition()
        {
            const string path = DataFolder + "/Ch11DreamingArchive.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            // NOT a boss fight (source script: "the only mercy left is the same door") — a single
            // symbolic hit ends it; she never attacks back.
            def.maxHealth = 1f;
            def.damage = 0f;
            def.moveSpeed = 0f;
            def.attackCooldown = 999f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch11 voice clips ourselves. ----

        private static DialoguePlayer Ch11BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter11Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch11WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter11] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch11WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter11Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch11VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch11VoiceFolder}/{clipName}.wav");
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

        /// <summary>A non-combat "memory ghost" anchor (Kira, the Younger Self): instantiates the named
        /// mesh, tints every renderer with MemoryFlashbackController's translucent ghost material, and
        /// adds a StoryNpc label. No Health/Enemy — purely a dialogue-beat anchor the player walks past,
        /// never blocked or attacked.</summary>
        private static GameObject Ch11PlaceGhostNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y; // re-add non-zero floor height (mirrors Ch6/Ch10)

            var ghostMat = MemoryFlashbackController.MakeGhostMaterial();
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = ghostMat;

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>
        /// A boss built from a Named-mesh prefab with no combat rig of its own (shared by Aldric and the
        /// Dreaming Archive): instantiates the mesh, synthesizes an ArmR/Sword/Blade/BladeTip hierarchy
        /// the way Ch9BuildVane/Ch10BuildNamedBoss do, then wires <see cref="Enemy"/> onto it. Returned
        /// inactive is the caller's job.
        /// </summary>
        private static Enemy Ch11BuildNamedBoss(string prefabPath, Vector3 pos, string displayName, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y; // re-add non-zero floor height (mirrors Ch10)
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the approach (-Z)

            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1.1f, 0f);
            cc.height = 2.4f;
            cc.radius = 0.5f;

            go.AddComponent<Health>();
            var bodyRenderer = go.GetComponentInChildren<Renderer>();

            var armRGo = new GameObject("ArmR");
            armRGo.transform.SetParent(go.transform, false);
            armRGo.transform.localPosition = new Vector3(0.35f, 1.4f, 0f);
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(armRGo.transform, false);
            var bladeGo = new GameObject("Blade");
            bladeGo.transform.SetParent(swordGo.transform, false);
            var bladeTipGo = new GameObject("BladeTip");
            bladeTipGo.transform.SetParent(bladeGo.transform, false);
            bladeTipGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);

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

        // ---- The bone-canyon: tier platforms + tilted ramp connectors (mirrors Ch10's Ninefold idiom). ----

        private static void Ch11BuildTier(Transform parent, string name, Vector3 center, Color floorColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.position = center + new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(Ch11TierHalfWidth * 2f, 0.4f, Ch11TierHalfWidth * 2f);
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            BuildProp(parent, name + "_RibA", center + new Vector3(-2.4f, 0.6f, -1.6f), new Vector3(0.6f, 1.4f, 0.6f), floorColor * 0.7f);
            BuildProp(parent, name + "_RibB", center + new Vector3(2.2f, 0.5f, 1.5f), new Vector3(0.55f, 1.2f, 0.55f), floorColor * 0.7f);
            BuildAccentPointLight(name + "_Glow", center + new Vector3(0f, 2.2f, 0f), floorColor, 1f, 9f);
        }

        private static void Ch11BuildRamp(Transform parent, string name, Vector3 from, Vector3 to, float width)
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
            TintShared(ramp.GetComponent<Renderer>(), new Color(0.3f, 0.3f, 0.32f));
        }

        /// <summary>A worldspace "CHAPTER 11 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch11BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 11 COMPLETE Canvas");
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
            label.text = "CHAPTER 11 COMPLETE";
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

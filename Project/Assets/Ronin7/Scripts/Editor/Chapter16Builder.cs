using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
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
    /// Chapter 16 ("The Throne of Ashes") scene builder — THE SAGA FINALE, Act IV close, merging the
    /// former Ch15 (the vault within) and Ch16 (the throne of ashes) into one continuous `[CAVE]`
    /// descent: the Iron Sepulcher (a vault-descent holding Cipher's own sealed memory) whose floor
    /// opens, without a scene-cut, onto the Concord Engine's throne-core. ~2x the scope of a normal
    /// chapter — one continuous descent along +Z, tiers stepping down in Y via walkable ramps (never a
    /// forced camera motion), the architecture thinning from Program steel into memory-space the lower
    /// it goes, then scale-flipping into a monumental open cathedral for the throne-core.
    ///
    /// BEAT MAP (mirrors the dialogue script's own segmentation):
    /// 0 The Cairn briefing (full crew commits) -> 1 the descent (wardens, tiered) -> 2 Samurai-4's
    /// non-lethal duel (DuelYield) -> 3 she breaks her own leash (LeashBreakController) and joins as
    /// family -> 4 REVEAL: reclaiming SOREN at the memory-core (closes Ladder C, rung 3) -> 5 into the
    /// throne-core (no cut) -> 6 the time-loop trap (TimeLoopController; three Khall-image loop
    /// iterations broken by will, not force) -> 7 the forged order (Khall, real, repents and allies;
    /// closes Ladder D rung 3 + Ladder E's war-as-product) -> 8 the true enemy named (Maelgorn / the
    /// Obsidian Synod; Ladder E converge) -> 9 the climax (the refusal, the multi-phase Maelgorn boss,
    /// the Engine broken to FREE not kill the kept shadow-AIs, the throne left empty).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch16</c>.
    ///
    /// SAMURAI-4 / DUELYIELD (studied Ch4's Kerrax + Ch10's Vess): ONE <see cref="DuelYield"/> instance,
    /// yieldThreshold 0.22, disableOnYield -> her <see cref="Enemy"/> component, sword -> the player's
    /// Grabbable katana, onAccepted -> MissionDirector.AdvanceFromPrompt. The duel-becomes-a-conversation
    /// staging in the source script is compressed into one pre-fight dialogue exchange (ch16_beat2_duel)
    /// played before the Trigger+Prompt duel, exactly mirroring Ch4/Ch10's confront-then-duel structure
    /// (per Ch10Builder's own comment, a bespoke multi-tier FSM would just duplicate DuelYield's job).
    /// The duel ends ONLY on a yield -> accept (never a kill) -> she is recruited at the ChapterOutro.
    ///
    /// LEASH-BREAK (reuses <see cref="LeashBreakController"/>, distinct from the DuelYield mercy): after
    /// DuelYield.onAccepted the mission activates a small inactive child object carrying
    /// LeashBreakController (autoAdvance, convictionDrainPerSecond high enough to cross breakThreshold in
    /// a few seconds — the "long silence... her hand begins to shake" beat) whose onLeashBreak advances a
    /// second null Prompt step. This models two separate character beats: the physical duel resolving on
    /// mercy (DuelYield), then her own leash straining and snapping afterward (LeashBreakController).
    ///
    /// TIME-LOOP / COMFORT-SAFETY (reuses <see cref="TimeLoopController"/>): built with EXACTLY 2 phase
    /// groups (normal throne-core accents / folded-reality accents) and phaseInterval set effectively
    /// infinite so its own Update() auto-cycle never fires in a real play session — the fold is driven
    /// ONLY by two explicit AdvancePhase() calls (entering, exiting the loop), each wired from an
    /// <see cref="ActivationRelay"/> a mission Trigger step activates. TimeLoopController's own
    /// ApplyPhase() does nothing but GameObject.SetActive on the phaseGroups' members (confirmed by
    /// reading its source) — it never touches the camera and damageWhenOutOfPhase is left OFF here, so
    /// the loop is a pure lighting/set-dressing toggle with zero forced motion, satisfying the VR comfort
    /// constraint. The three loop ITERATIONS (per the "author several distinct iterations" note) are
    /// three separate ghost-tinted Khall-image DefeatEnemies encounters at the same spot, each a fresh
    /// GameObject the player must best a different way before Maelgorn concedes.
    ///
    /// MAELGORN / MULTI-PHASE BOSS: the source screenplay stages the throne-room confrontation as pure
    /// dialogue + a refusal, but the finale's GAME NARRATIVE DESIGN calls for a "final boss: Maelgorn /
    /// Obsidian Synod." Modeled as 3 sequential <see cref="Enemy"/>/<see cref="Health"/> instances built
    /// from the SAME Maelgorn Named prefab (mirrors Ch9BuildVane/Ch11BuildNamedBoss's "boss from a
    /// Named-mesh prefab" idiom, generalized here as <see cref="Ch16BuildNamedBoss"/>), positioned
    /// progressively closer to the seam as he descends from the throne to stop the cut, escalating stats
    /// per phase, each its own DefeatEnemies step (auto-activates, mirrors every other chapter's "no
    /// separate Trigger step needed" convention) with a short original phase-transition bark between
    /// phases. This is a LETHAL fight (no DuelYield) — Maelgorn is the true enemy, not a mercy target.
    ///
    /// Real Named prefabs exist for the whole finale cast (Samurai-4, Maelgorn, Khall,
    /// Ronin-7_Cipher_Soren, The-Hollow-Kings — glob-confirmed on disk at authoring time), so every
    /// placement below uses <see cref="InstantiateNpc"/> + <see cref="FitNamedCharacter"/>, never a
    /// placeholder capsule.
    ///
    /// CREW-PRESENCE DECISION (mirrors Ch13's convention): Cassie-04, Sable, Kessler, Morrigan, Coral
    /// Vex, Mera Voss, Vess, Gryph, Iris, Mira, and Resh all stay voice-only throughout (their lines are
    /// "(comm)"-tagged, or their one closing-beat cameo carries zero gameplay weight) — only Ronin-7 (the
    /// player), Samurai-4, the three Khall-image loop ghosts, the real Khall, Maelgorn (presence + 3
    /// combat phases), and the decorative Hollow Kings get physical placement.
    ///
    /// LADDER C, RUNG 3 (closes Ladder C): delivered exactly once, ch16_beat4_soren — the identity
    /// recovery reveal. This is the ONE chapter in the saga where "Soren" is spoken.
    /// LADDER D, RUNG 3 + LADDER E (war-as-product lands in the same beat, per the chapter's own
    /// continuity notes): delivered across ch16_beat7_forged_order (Khall's history, the forged Kethel-7
    /// order, the ten syndicates kept at war on purpose because the war IS the product) and
    /// ch16_beat8_true_enemy (Maelgorn / Obsidian Synod unmasked — Morrigan's Ch6 "outside hand" named).
    /// Both delivered exactly once. See Chapter16Lines' class summary for the full ladder accounting and
    /// AUDIT FIX #6 (Samurai-4 wrongly called a "newer make"; her second "Ronin-7" address fixed to
    /// "Cipher") — both are already applied there, this file only wires the transcribed sets.
    ///
    /// ALLY UNLOCKS: Samurai-4 (family, capstone bond beyond the numbered ten) + Khall (repents, allies).
    /// Both recruit flags are set together with ch16_complete + galaxy1_complete at the ChapterOutro
    /// (mirrors how Ch10/Ch13 combined ally-recruit flags with chapter completion) — the saga's very last
    /// flags.
    ///
    /// HUB INCREMENT: <see cref="HubBuilder"/>'s already-existing "FullyLit" ch16_complete-gated hub root
    /// (already wired in <c>BuildHubMode</c>'s gate list from a prior pass) is filled by
    /// <c>Ch16FillFullyLit</c> with idle Samurai-4 + Khall among the bright lights — the last increment,
    /// mirroring the Ch2/Ch6/Ch7/Ch9/Ch10/Ch13 increments exactly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch16ScenePath = SceneFolder + "/Ch16_ThroneOfAshes.unity";
        private const string Ch16VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        // Named-cast prefabs. All resolve to real, already-baked prefabs (glob-confirmed on disk at
        // authoring time) — no placeholders needed for the finale cast.
        private const string Ch16Samurai4Prefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Samurai-4.prefab";
        private const string Ch16MaelgornPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Maelgorn.prefab";
        private const string Ch16KhallPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab";
        private const string Ch16HollowKingsPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/The-Hollow-Kings.prefab";
        private const string Ch16SorenSelfPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab";
        private const string Ch16SallowPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Sallow.prefab";
        private const string Ch16EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        // Tier half-widths (corridor tiers) and the throne-core's much wider monumental half-width.
        private const float Ch16CorridorHalfWidth = 6f;
        private const float Ch16FloorHalfWidth = 7f;
        private const float Ch16ThroneCoreHalfWidth = 16f;

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 16 — The Throne of Ashes", priority = 216)]
        public static void BuildChapter16ThroneOfAshes()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets, so
            // references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();
            var wardenDef = Ch16EnsureWardenDefinition();
            var samurai4Def = Ch16EnsureSamurai4Definition();
            var khallImageDef = Ch16EnsureKhallImageDefinition();
            var maelgornP1Def = Ch16EnsureMaelgornPhaseDefinition(1, 220f, 16f);
            var maelgornP2Def = Ch16EnsureMaelgornPhaseDefinition(2, 260f, 19f);
            var maelgornP3Def = Ch16EnsureMaelgornPhaseDefinition(3, 300f, 22f);

            // ---- Lighting: cold Program steel up top, warming into memory-gold through the mid tiers,
            // deep violet/furnace-orange cathedral for the throne-core. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.8f, 0.9f);
            light.intensity = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.18f, 0.24f);
            RenderSettings.fogDensity = 0.012f;

            BuildAccentPointLight("SpawnLight", new Vector3(0f, 2.4f, 4f), new Color(0.82f, 0.86f, 0.95f), 1.2f, 10f);
            BuildAccentPointLight("UpperTierLight0", new Vector3(-3f, 2.2f, 26f), new Color(0.7f, 0.78f, 0.95f), 1.3f, 14f);
            BuildAccentPointLight("UpperTierLight1", new Vector3(3f, 2.2f, 32f), new Color(0.7f, 0.78f, 0.95f), 1.3f, 14f);
            BuildAccentPointLight("MidTierLight0", new Vector3(-3f, -5.8f, 50f), new Color(0.85f, 0.72f, 0.5f), 1.3f, 14f); // memory-gold seeping in
            BuildAccentPointLight("MidTierLight1", new Vector3(3f, -8.8f, 78f), new Color(0.88f, 0.75f, 0.5f), 1.4f, 14f);
            BuildAccentPointLight("LowerTierLight0", new Vector3(0f, -11.8f, 106f), new Color(0.92f, 0.8f, 0.55f), 1.6f, 16f); // sourceless memory-light
            BuildAccentPointLight("SepulcherFloorLight", new Vector3(0f, -17.8f, 154f), new Color(0.95f, 0.85f, 0.6f), 1.8f, 16f);
            BuildAccentPointLight("ThroneCoreLight0", new Vector3(-8f, -25f, 220f), new Color(0.35f, 0.28f, 0.55f), 2f, 26f); // lattice violet
            BuildAccentPointLight("ThroneCoreLight1", new Vector3(8f, -25f, 250f), new Color(0.35f, 0.28f, 0.55f), 2f, 26f);
            BuildAccentPointLight("ThroneLight", new Vector3(0f, -22f, 270f), new Color(0.95f, 0.5f, 0.2f), 2.4f, 22f); // Maelgorn's furnace-orange

            // ---- World root: one continuous descent along +Z, tiers stepping down in Y, ramps between
            // them (walkable, comfort-safe — no forced camera motion anywhere in this chapter). ----
            var worldGo = new GameObject("IronSepulcher");
            var world = worldGo.transform;

            var steelFloor = new Color(0.8f, 0.83f, 0.88f);
            var steelCeil = new Color(0.86f, 0.88f, 0.92f);
            var goldFloor = new Color(0.5f, 0.44f, 0.32f);
            var goldCeil = new Color(0.56f, 0.5f, 0.38f);

            // SpawnTier z[-2,14], y=0 — Program fortress-vault, clinical steel.
            Ch16BuildTier(world, "SpawnTier", new Vector3(0f, 0f, 6f), new Vector3(Ch16CorridorHalfWidth * 2f, 0f, 16f), steelFloor, steelCeil);
            BuildWall(world, "SpawnTier_WallW", new Vector3(-Ch16CorridorHalfWidth, RoomH / 2f, 6f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "SpawnTier_WallE", new Vector3(Ch16CorridorHalfWidth, RoomH / 2f, 6f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "SpawnTier_WallS", new Vector3(0f, RoomH / 2f, -2f), new Vector3(Ch16CorridorHalfWidth * 2f, RoomH, 0.2f));

            Ch16BuildRamp(world, "RampA", new Vector3(0f, 0f, 14f), new Vector3(0f, -3f, 22f), Ch16CorridorHalfWidth * 2f);

            // UpperTierB z[22,38], y=-3 — still steel, design-ward conditioning wards, wardens A.
            Ch16BuildTier(world, "UpperTierB", new Vector3(0f, -3f, 30f), new Vector3(Ch16CorridorHalfWidth * 2f, 0f, 16f), steelFloor, steelCeil);
            BuildWall(world, "UpperTierB_WallW", new Vector3(-Ch16CorridorHalfWidth, -3f + RoomH / 2f, 30f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "UpperTierB_WallE", new Vector3(Ch16CorridorHalfWidth, -3f + RoomH / 2f, 30f), new Vector3(0.2f, RoomH, 16f));
            BuildProp(world, "ConditioningWard0", new Vector3(-4.5f, -2.5f, 28f), new Vector3(1.2f, 1.6f, 0.4f), steelCeil);
            BuildProp(world, "ConditioningWard1", new Vector3(4.5f, -2.5f, 32f), new Vector3(1.2f, 1.6f, 0.4f), steelCeil);

            Ch16BuildRamp(world, "RampB", new Vector3(0f, -3f, 38f), new Vector3(0f, -6f, 46f), Ch16CorridorHalfWidth * 2f);

            // MidTierA z[46,62], y=-6 — thinning into memory-space, dialogue-only (no combat).
            Ch16BuildTier(world, "MidTierA", new Vector3(0f, -6f, 54f), new Vector3(Ch16CorridorHalfWidth * 2f, 0f, 16f), goldFloor, goldCeil);
            BuildWall(world, "MidTierA_WallW", new Vector3(-Ch16CorridorHalfWidth, -6f + RoomH / 2f, 54f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "MidTierA_WallE", new Vector3(Ch16CorridorHalfWidth, -6f + RoomH / 2f, 54f), new Vector3(0.2f, RoomH, 16f));
            BuildProp(world, "MemoryDoorway", new Vector3(0f, -5.4f, 58f), new Vector3(2.4f, 2.6f, 0.3f), goldCeil);

            Ch16BuildRamp(world, "RampC", new Vector3(0f, -6f, 62f), new Vector3(0f, -9f, 70f), Ch16CorridorHalfWidth * 2f);

            // MidTierB z[70,86], y=-9 — nursery-echo memory-space, wardens B.
            Ch16BuildTier(world, "MidTierB", new Vector3(0f, -9f, 78f), new Vector3(Ch16CorridorHalfWidth * 2f, 0f, 16f), goldFloor, goldCeil);
            BuildWall(world, "MidTierB_WallW", new Vector3(-Ch16CorridorHalfWidth, -9f + RoomH / 2f, 78f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(world, "MidTierB_WallE", new Vector3(Ch16CorridorHalfWidth, -9f + RoomH / 2f, 78f), new Vector3(0.2f, RoomH, 16f));
            BuildProp(world, "SealedCradleEcho0", new Vector3(-5f, -8.4f, 76f), new Vector3(0.6f, 1.2f, 0.6f), goldCeil);
            BuildProp(world, "SealedCradleEcho1", new Vector3(5f, -8.4f, 80f), new Vector3(0.6f, 1.2f, 0.6f), goldCeil);

            Ch16BuildRamp(world, "RampD", new Vector3(0f, -9f, 86f), new Vector3(0f, -12f, 94f), Ch16CorridorHalfWidth * 2f);

            // LowerTier z[94,126], y=-12 — near-pure memory-space, sourceless light. Samurai-4 stands
            // here; the duel and her leash-break both resolve on this tier (one location, per the class
            // summary's DuelYield note — no bespoke multi-tier FSM).
            Ch16BuildTier(world, "LowerTier", new Vector3(0f, -12f, 110f), new Vector3(Ch16CorridorHalfWidth * 2f, 0f, 32f), goldFloor * 1.15f, goldCeil * 1.15f);
            BuildWall(world, "LowerTier_WallW", new Vector3(-Ch16CorridorHalfWidth, -12f + RoomH / 2f, 110f), new Vector3(0.2f, RoomH, 32f));
            BuildWall(world, "LowerTier_WallE", new Vector3(Ch16CorridorHalfWidth, -12f + RoomH / 2f, 110f), new Vector3(0.2f, RoomH, 32f));

            Ch16BuildRamp(world, "RampE", new Vector3(0f, -12f, 126f), new Vector3(0f, -18f, 142f), Ch16CorridorHalfWidth * 2f);

            // SepulcherFloor z[142,166], y=-18 — the vault within: the stone is gone, an open platform
            // (no walls/ceiling), nearly pure memory-space, the cradle at its center.
            Ch16BuildFloorOnly(world, "SepulcherFloor", new Vector3(0f, -18f, 154f), new Vector3(Ch16FloorHalfWidth * 2f, 0f, 24f), goldFloor * 1.3f);
            BuildProp(world, "MemoryCradle", new Vector3(0f, -17.4f, 154f), new Vector3(1.4f, 1f, 1.4f), new Color(0.95f, 0.85f, 0.55f));

            // The floor of the memory-chamber opens onto the throne-core — one long ramp, no scene-cut.
            Ch16BuildRamp(world, "TheBigDescent", new Vector3(0f, -18f, 166f), new Vector3(0f, -30f, 198f), Ch16FloorHalfWidth * 2f);

            // ThroneCore z[198,290], y=-30 — the scale-flip: a monumental open cathedral, no walls, no
            // ceiling, the architecture having stopped being architecture entirely.
            Ch16BuildFloorOnly(world, "ThroneCore", new Vector3(0f, -30f, 244f), new Vector3(Ch16ThroneCoreHalfWidth * 2f, 0f, 92f), new Color(0.16f, 0.14f, 0.22f));
            BuildProp(world, "LatticeSpar0", new Vector3(-10f, -22f, 220f), new Vector3(0.8f, 16f, 0.8f), new Color(0.3f, 0.55f, 0.75f));
            BuildProp(world, "LatticeSpar1", new Vector3(10f, -22f, 236f), new Vector3(0.8f, 16f, 0.8f), new Color(0.3f, 0.55f, 0.75f));
            BuildProp(world, "SeamMarker", new Vector3(3f, -29.4f, 228f), new Vector3(0.3f, 1.2f, 0.3f), new Color(1f, 0.9f, 0.4f)); // Heris's flaw, bright
            BuildProp(world, "ForgedThrone", new Vector3(0f, -29f, 270f), new Vector3(2.2f, 2.4f, 2.2f), new Color(0.1f, 0.08f, 0.1f)); // obsidian, cools to ash at the climax

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig: locomotion, bounds, EchoPresence, every shipped ability chain (all five
            // are earned by the finale and should be usable — the whole point of AttachPlayerAbilities). ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, -15f, 150f);
            bounds.radius = 200f;

            AttachPlayerAbilities(rig, refs);

            // The katana rides from the start (deep into Act IV — no rack-wake beat, matching Ch9-13's
            // "cost, not initiation" precedent).
            BuildSword(new Vector3(2f, 1f, 4f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch16EchoBladePrefab);
            var swordGrab = GameObject.Find("Sword").GetComponent<Grabbable>();

            // ---- Wardens A (UpperTierB) and B (MidTierB): plain capsule mooks, inactive until their own
            // DefeatEnemies step auto-activates them (mirrors every earlier chapter's mook convention). ----
            var wardenAPositions = new[] { new Vector3(-3f, -3f, 27f), new Vector3(3f, -3f, 29f), new Vector3(0f, -3f, 33f) };
            var wardenAHealths = new List<Object>();
            foreach (var pos in wardenAPositions)
            {
                var e = BuildEnemy(pos, playerHealth, wardenDef);
                e.gameObject.SetActive(false);
                wardenAHealths.Add(e.GetComponent<Health>());
            }

            var wardenBPositions = new[] { new Vector3(-3f, -9f, 75f), new Vector3(3f, -9f, 77f), new Vector3(0f, -9f, 81f), new Vector3(-2f, -9f, 82f) };
            var wardenBHealths = new List<Object>();
            foreach (var pos in wardenBPositions)
            {
                var e = BuildEnemy(pos, playerHealth, wardenDef);
                e.gameObject.SetActive(false);
                wardenBHealths.Add(e.GetComponent<Health>());
            }

            // ---- Samurai-4: the boss of the descent. DuelYield ends the duel on a YIELD only (never a
            // kill); LeashBreakController then resolves the separate "leash straining and snapping"
            // beat afterward. Inactive until the Trigger step. ----
            var samurai4Enemy = Ch16BuildNamedBoss(Ch16Samurai4Prefab, new Vector3(0f, -12f, 106f), "Samurai-4", samurai4Def, playerHealth);
            var samurai4Go = samurai4Enemy.gameObject;
            var samurai4Npc = samurai4Go.AddComponent<StoryNpc>();
            var samurai4NpcSo = new SerializedObject(samurai4Npc);
            samurai4NpcSo.FindProperty("displayName").stringValue = "Samurai-4";
            samurai4NpcSo.ApplyModifiedPropertiesWithoutUndo();

            var duelYield = samurai4Go.AddComponent<DuelYield>();
            var dySo = new SerializedObject(duelYield);
            SetObjectRef(dySo, "opponent", samurai4Go.GetComponent<Health>());
            dySo.FindProperty("yieldThreshold").floatValue = 0.22f; // yields at low health, never death
            SetObjectRefList(dySo, "disableOnYield", new List<Object> { samurai4Enemy });
            SetObjectRef(dySo, "sword", swordGrab);
            dySo.FindProperty("autoAcceptSeconds").floatValue = 30f;
            dySo.ApplyModifiedPropertiesWithoutUndo();
            samurai4Go.SetActive(false); // Activated only by the Trigger step.

            // LeashBreakController lives on a small child object so it can be independently
            // Trigger-activated AFTER the duel accepts (Awake seeds conviction on first activation).
            var leashBreakGo = new GameObject("Samurai4_LeashBreak");
            leashBreakGo.transform.SetParent(samurai4Go.transform, false);
            var leashBreak = leashBreakGo.AddComponent<LeashBreakController>();
            var lbSo = new SerializedObject(leashBreak);
            lbSo.FindProperty("startingConviction").floatValue = 1f;
            lbSo.FindProperty("breakThreshold").floatValue = 0.25f;
            lbSo.FindProperty("convictionDrainPerSecond").floatValue = 0.5f; // breaks in ~1.5s — the held silence
            lbSo.FindProperty("evidenceDrainAmount").floatValue = 0.34f;
            lbSo.FindProperty("autoAdvance").boolValue = true;
            lbSo.ApplyModifiedPropertiesWithoutUndo();
            leashBreakGo.SetActive(false); // Activated only by its own Trigger step, after the duel accepts.

            // ---- The buried self, made visible: a ghost-tinted Ronin-7_Cipher_Soren figure at the
            // cradle for the name-reveal beat (mirrors Ch11PlaceGhostNpc's technique). Decorative only. ----
            Ch16PlaceGhostNpc(Ch16SorenSelfPrefab, new Vector3(0f, -18f, 156f), "Soren (Memory)");

            // ---- The Time-Loop Trap: two phase groups (normal / folded), driven ONLY by two explicit
            // AdvancePhase() calls via ActivationRelay — never an automatic per-frame cycle, and it only
            // ever SetActive()s GameObjects, never touches the camera (see class summary). ----
            var normalAccent0 = Ch16BuildAccentLight("LoopNormalAccent0", new Vector3(-4f, -28f, 228f), new Color(0.4f, 0.55f, 0.9f), 1.4f, 12f);
            var normalAccent1 = Ch16BuildAccentLight("LoopNormalAccent1", new Vector3(4f, -28f, 228f), new Color(0.4f, 0.55f, 0.9f), 1.4f, 12f);
            var foldAccent0 = Ch16BuildAccentLight("LoopFoldAccent0", new Vector3(-4f, -28f, 228f), new Color(0.9f, 0.2f, 0.35f), 1.8f, 14f);
            var foldAccent1 = Ch16BuildAccentLight("LoopFoldAccent1", new Vector3(4f, -28f, 228f), new Color(0.9f, 0.2f, 0.35f), 1.8f, 14f);
            foldAccent0.SetActive(false);
            foldAccent1.SetActive(false);

            var realityFoldGo = new GameObject("RealityFold");
            var timeLoop = realityFoldGo.AddComponent<TimeLoopController>();
            var tlSo = new SerializedObject(timeLoop);
            tlSo.FindProperty("phaseInterval").floatValue = 999999f; // manual-only: no automatic per-frame cycle
            tlSo.FindProperty("damageWhenOutOfPhase").boolValue = false; // pure visual toggle, never a DoT trap
            var groupsProp = tlSo.FindProperty("phaseGroups");
            groupsProp.arraySize = 2;
            var normalGroupMembers = groupsProp.GetArrayElementAtIndex(0).FindPropertyRelative("members");
            normalGroupMembers.arraySize = 2;
            normalGroupMembers.GetArrayElementAtIndex(0).objectReferenceValue = normalAccent0;
            normalGroupMembers.GetArrayElementAtIndex(1).objectReferenceValue = normalAccent1;
            var foldGroupMembers = groupsProp.GetArrayElementAtIndex(1).FindPropertyRelative("members");
            foldGroupMembers.arraySize = 2;
            foldGroupMembers.GetArrayElementAtIndex(0).objectReferenceValue = foldAccent0;
            foldGroupMembers.GetArrayElementAtIndex(1).objectReferenceValue = foldAccent1;
            tlSo.ApplyModifiedPropertiesWithoutUndo();
            // realityFoldGo stays ACTIVE from scene start so Start() runs and seeds phase 0 (normal).

            var enterFoldRelayGo = new GameObject("EnterFoldRelay");
            var enterFoldRelay = enterFoldRelayGo.AddComponent<ActivationRelay>();
            UnityEventTools.AddPersistentListener(enterFoldRelay.OnEnabled, new UnityEngine.Events.UnityAction(timeLoop.AdvancePhase));
            enterFoldRelayGo.SetActive(false);

            var exitFoldRelayGo = new GameObject("ExitFoldRelay");
            var exitFoldRelay = exitFoldRelayGo.AddComponent<ActivationRelay>();
            UnityEventTools.AddPersistentListener(exitFoldRelay.OnEnabled, new UnityEngine.Events.UnityAction(timeLoop.AdvancePhase));
            exitFoldRelayGo.SetActive(false);

            // ---- The three Khall-image loop iterations: ghost-tinted, fought and reset three distinct
            // times before the trap concedes (per the "author several distinct iterations" note). ----
            var ghostMat = MemoryFlashbackController.MakeGhostMaterial();
            var khallImage1 = Ch16BuildKhallImage(khallImageDef, playerHealth, ghostMat, 1);
            var khallImage2 = Ch16BuildKhallImage(khallImageDef, playerHealth, ghostMat, 2);
            var khallImage3 = Ch16BuildKhallImage(khallImageDef, playerHealth, ghostMat, 3);

            // ---- The real Khall: unarmed, non-combat, revealed once the loop breaks. ----
            var khallGo = Ch16PlaceStoryNpc(Ch16KhallPrefab, new Vector3(0f, -30f, 228f), "Khall");
            if (khallGo != null) khallGo.SetActive(false); // Activated by the exit-fold Trigger step.

            // ---- Maelgorn: a decorative presence on the throne for the confrontation dialogue, plus
            // three sequential lethal combat phases (built from the same Named prefab) as he descends to
            // stop the seam being cut. The Hollow Kings stand behind him, decorative only (per the
            // source script, they never fight — "ranged in hollow-crowned silence"). ----
            var maelgornPresenceGo = Ch16PlaceStoryNpc(Ch16MaelgornPrefab, new Vector3(0f, -30f, 270f), "Maelgorn");
            if (maelgornPresenceGo != null) maelgornPresenceGo.SetActive(false); // Activated by "Maelgorn Rises" Trigger.

            var hollowKingsGo = Ch16PlaceStoryNpc(Ch16HollowKingsPrefab, new Vector3(-3f, -30f, 274f), "The Hollow Kings");
            if (hollowKingsGo != null) hollowKingsGo.SetActive(false);

            var maelgornP1 = Ch16BuildNamedBoss(Ch16MaelgornPrefab, new Vector3(0f, -30f, 260f), "Maelgorn (Phase 1)", maelgornP1Def, playerHealth);
            maelgornP1.gameObject.SetActive(false);
            var maelgornP2 = Ch16BuildNamedBoss(Ch16MaelgornPrefab, new Vector3(0f, -30f, 248f), "Maelgorn (Phase 2)", maelgornP2Def, playerHealth);
            maelgornP2.gameObject.SetActive(false);
            var maelgornP3 = Ch16BuildNamedBoss(Ch16MaelgornPrefab, new Vector3(0f, -30f, 236f), "Maelgorn (Phase 3)", maelgornP3Def, playerHealth);
            maelgornP3.gameObject.SetActive(false);

            // ---- Sallow: present at the seam for the liberation (channels the freed shadows). ----
            var sallowGo = Ch16PlaceStoryNpc(Ch16SallowPrefab, new Vector3(-3f, -30f, 226f), "Sallow");
            if (sallowGo != null) sallowGo.SetActive(false);

            // ---- The Engine-break liberation VFX: a burst of bright shadow-lights around the seam,
            // inactive until the climax's Trigger step. ----
            var liberationVfxGo = new GameObject("EngineBreakLiberation");
            Vector3[] liberationOffsets = { new Vector3(-2f, -27f, 226f), new Vector3(2f, -27f, 230f), new Vector3(0f, -25f, 228f) };
            foreach (var offset in liberationOffsets)
            {
                var lg = new GameObject("FreedShadowLight");
                lg.transform.SetParent(liberationVfxGo.transform, false);
                lg.transform.position = offset;
                var l2 = lg.AddComponent<Light>();
                l2.type = LightType.Point;
                l2.color = new Color(0.65f, 0.85f, 1f);
                l2.intensity = 3f;
                l2.range = 18f;
                l2.shadows = LightShadows.None;
            }
            liberationVfxGo.SetActive(false);

            // ---- Reach points. ----
            var upperTierReachGo = new GameObject("UpperTierReachPoint");
            upperTierReachGo.transform.position = new Vector3(0f, 0f, 14f);
            var midTierBReachGo = new GameObject("MidTierBReachPoint");
            midTierBReachGo.transform.position = new Vector3(0f, -6f, 62f);
            var lowerTierReachGo = new GameObject("LowerTierReachPoint");
            lowerTierReachGo.transform.position = new Vector3(0f, -9f, 86f);
            var sepulcherFloorReachGo = new GameObject("SepulcherFloorReachPoint");
            sepulcherFloorReachGo.transform.position = new Vector3(0f, -12f, 130f);
            var throneCoreReachGo = new GameObject("ThroneCoreReachPoint");
            throneCoreReachGo.transform.position = new Vector3(0f, -18f, 168f);
            var seamApproachReachGo = new GameObject("SeamApproachReachPoint");
            seamApproachReachGo.transform.position = new Vector3(0f, -30f, 210f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch16BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 4f), "ch16_beat0_briefing", talkRef);
            var dlgDescentUpper = Ch16BuildDialogue("Dialogue_Beat1_DescentUpper", new Vector3(0f, -3f, 24f), "ch16_beat1_descent_upper", talkRef);
            var dlgDescentLower = Ch16BuildDialogue("Dialogue_Beat1_DescentLower", new Vector3(0f, -9f, 88f), "ch16_beat1_descent_lower", talkRef);
            var dlgDuel = Ch16BuildDialogue("Dialogue_Beat2_Duel", new Vector3(0f, -12f, 100f), "ch16_beat2_duel", talkRef);
            var dlgLeashBreak = Ch16BuildDialogue("Dialogue_Beat3_LeashBreak", new Vector3(0f, -12f, 108f), "ch16_beat3_leash_break", talkRef);
            var dlgSoren = Ch16BuildDialogue("Dialogue_Beat4_Soren", new Vector3(0f, -18f, 154f), "ch16_beat4_soren", talkRef);
            var dlgThroneCore = Ch16BuildDialogue("Dialogue_Beat5_ThroneCore", new Vector3(0f, -30f, 200f), "ch16_beat5_throne_core", talkRef);
            var dlgLoopTaunt = Ch16BuildDialogue("Dialogue_Beat6_LoopTaunt", new Vector3(0f, -30f, 212f), "ch16_beat6_loop_taunt", talkRef);
            var dlgLoopIter1 = Ch16BuildDialogue("Dialogue_Beat6_LoopIter1", new Vector3(0f, -30f, 228f), "ch16_beat6_loop_iter1", talkRef);
            var dlgLoopIter2 = Ch16BuildDialogue("Dialogue_Beat6_LoopIter2", new Vector3(0f, -30f, 228f), "ch16_beat6_loop_iter2", talkRef);
            var dlgLoopBreak = Ch16BuildDialogue("Dialogue_Beat6_LoopBreak", new Vector3(0f, -30f, 228f), "ch16_beat6_loop_break", talkRef);
            var dlgLoopConcede = Ch16BuildDialogue("Dialogue_Beat6_LoopConcede", new Vector3(0f, -30f, 228f), "ch16_beat6_loop_concede", talkRef);
            var dlgForgedOrder = Ch16BuildDialogue("Dialogue_Beat7_ForgedOrder", new Vector3(0f, -30f, 230f), "ch16_beat7_forged_order", talkRef);
            var dlgTrueEnemy = Ch16BuildDialogue("Dialogue_Beat8_TrueEnemy", new Vector3(0f, -30f, 234f), "ch16_beat8_true_enemy", talkRef);
            var dlgSeamChoice = Ch16BuildDialogue("Dialogue_Beat9_SeamChoice", new Vector3(0f, -30f, 226f), "ch16_beat9_seam_choice", talkRef);
            var dlgPhaseTaunt1 = Ch16BuildDialogue("Dialogue_Beat9_PhaseTaunt1", new Vector3(0f, -30f, 255f), "ch16_beat9_phase_taunt1", talkRef);
            var dlgPhaseTaunt2 = Ch16BuildDialogue("Dialogue_Beat9_PhaseTaunt2", new Vector3(0f, -30f, 244f), "ch16_beat9_phase_taunt2", talkRef);
            var dlgLiberation = Ch16BuildDialogue("Dialogue_Beat9_Liberation", new Vector3(0f, -30f, 228f), "ch16_beat9_liberation", talkRef);
            var dlgThroneTest = Ch16BuildDialogue("Dialogue_Beat9_ThroneTest", new Vector3(0f, -30f, 232f), "ch16_beat9_throne_test", talkRef);
            var dlgClosingCrew = Ch16BuildDialogue("Dialogue_Beat9_ClosingCrew", new Vector3(0f, -30f, 233f), "ch16_beat9_closing_crew", talkRef);
            var dlgHomecoming = Ch16BuildDialogue("Dialogue_Beat9_Homecoming", new Vector3(0f, -30f, 234f), "ch16_beat9_homecoming", talkRef);
            var dlgEchoFinal = Ch16BuildDialogue("Dialogue_Beat9_EchoFinal", new Vector3(0f, -30f, 235f), "ch16_beat9_echo_final", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. Sets ch16_complete +
            // galaxy1_complete + samurai4_recruited + khall_allied together — the saga's final flags. ----
            var completeCanvasGo = Ch16BuildCompleteCanvas(new Vector3(0f, -28f, 238f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, -30f, 236f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 4;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch16_complete";
            flagsProp.GetArrayElementAtIndex(1).stringValue = "galaxy1_complete";
            flagsProp.GetArrayElementAtIndex(2).stringValue = "samurai4_recruited";
            flagsProp.GetArrayElementAtIndex(3).stringValue = "khall_allied";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 16 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 47;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing, the full crew commits)", dlgBriefing);
            AuthorReachStep(steps, n++, "ReachTrigger: The Upper Tier", upperTierReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Descent (upper tiers peel)", dlgDescentUpper);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Sepulcher Wardens A", wardenAHealths);
            AuthorReachStep(steps, n++, "ReachTrigger: Mid Tier B", midTierBReachGo.transform, 5f);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Sepulcher Wardens B", wardenBHealths);
            AuthorReachStep(steps, n++, "ReachTrigger: The Lower Tier", lowerTierReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: Samurai-4 Named (Echo's warning)", dlgDescentLower);
            AuthorDialogueStep(steps, n++, "Beat2: Samurai-4 (the duel becomes a dialogue)", dlgDuel);
            AuthorTriggerStep(steps, n++, "Trigger: Activate Samurai-4", samurai4Go);
            AuthorPromptStep(steps, n++, "Prompt: Samurai-4 Duel (DuelYield, yield + sheathe)", null);
            AuthorTriggerStep(steps, n++, "Trigger: Activate Leash-Break", leashBreakGo);
            AuthorPromptStep(steps, n++, "Prompt: The Quiet (LeashBreakController onLeashBreak)", null);
            AuthorDialogueStep(steps, n++, "Beat3: Breaking Her Leash (Ally: family)", dlgLeashBreak);
            AuthorReachStep(steps, n++, "ReachTrigger: The Sepulcher Floor", sepulcherFloorReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat4: Reclaiming Soren (REVEAL, closes Ladder C)", dlgSoren);
            AuthorReachStep(steps, n++, "ReachTrigger: The Throne-Core (no cut)", throneCoreReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat5: Into the Throne-Core", dlgThroneCore);
            AuthorReachStep(steps, n++, "ReachTrigger: The Seam (triggers the fold)", seamApproachReachGo.transform, 6f);
            AuthorDialogueStep(steps, n++, "Beat6: The Time-Loop Trap (Maelgorn taunts)", dlgLoopTaunt);
            AuthorTriggerStep(steps, n++, "Trigger: Enter the Fold (loop iteration 1)", enterFoldRelayGo, khallImage1.gameObject);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Khall-Image, Loop Iteration 1", new List<Object> { khallImage1.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat6: Loop Iteration 1 (naming the wound)", dlgLoopIter1);
            AuthorTriggerStep(steps, n++, "Trigger: Loop Iteration 2", khallImage2.gameObject);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Khall-Image, Loop Iteration 2", new List<Object> { khallImage2.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat6: Loop Iteration 2 (the will-mechanic)", dlgLoopIter2);
            AuthorTriggerStep(steps, n++, "Trigger: Loop Iteration 3", khallImage3.gameObject);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Khall-Image, Loop Iteration 3", new List<Object> { khallImage3.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat6: The Trap Breaks (Soren's resolve)", dlgLoopBreak);
            AuthorDialogueStep(steps, n++, "Beat6: Maelgorn Concedes the Loop", dlgLoopConcede);
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Fold, Reveal Khall", exitFoldRelayGo, khallGo);
            AuthorDialogueStep(steps, n++, "Beat7: The Forged Order (Khall repents, allies; Ladder D3 + E)", dlgForgedOrder);
            AuthorTriggerStep(steps, n++, "Trigger: Maelgorn Rises", maelgornPresenceGo, hollowKingsGo, sallowGo);
            AuthorDialogueStep(steps, n++, "Beat8: The True Enemy Named (Ladder E converge)", dlgTrueEnemy);
            AuthorDialogueStep(steps, n++, "Beat9: The Seam Choice (the refusal)", dlgSeamChoice);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Maelgorn, Phase 1", new List<Object> { maelgornP1.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat9: Phase Transition 1", dlgPhaseTaunt1);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Maelgorn, Phase 2", new List<Object> { maelgornP2.GetComponent<Health>() });
            AuthorDialogueStep(steps, n++, "Beat9: Phase Transition 2", dlgPhaseTaunt2);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Maelgorn, Phase 3 (final)", new List<Object> { maelgornP3.GetComponent<Health>() });
            AuthorTriggerStep(steps, n++, "Trigger: The Engine Breaks (liberation, not destruction)", liberationVfxGo);
            AuthorDialogueStep(steps, n++, "Beat9: The Liberation (closes Ladder E)", dlgLiberation);
            AuthorDialogueStep(steps, n++, "Beat9: The Throne Test (final refusal)", dlgThroneTest);
            AuthorDialogueStep(steps, n++, "Beat9: The Closing Crew", dlgClosingCrew);
            AuthorDialogueStep(steps, n++, "Beat9: The Homecoming", dlgHomecoming);
            AuthorDialogueStep(steps, n++, "Beat9: Echo's Final Line", dlgEchoFinal);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flags + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // The duel's acceptance advances the mission out of its null-prompt duel step; the leash
            // break's onLeashBreak advances the mission out of ITS null-prompt step (a distinct FSM/beat
            // from the duel's own yield — see class summary).
            UnityEventTools.AddPersistentListener(duelYield.onAccepted,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));
            UnityEventTools.AddPersistentListener(leashBreak.onLeashBreak,
                new UnityEngine.Events.UnityAction(missionDirector.AdvanceFromPrompt));

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch16ScenePath);
            EnsureScenesInBuild(Ch16ScenePath);

            Debug.Log($"[Space Samurai] Chapter 16 (SAGA FINALE) built at {Ch16ScenePath}. " +
                      "The Iron Sepulcher descent (steel -> memory-space, wardens A/B) -> Samurai-4's " +
                      "non-lethal DuelYield duel -> LeashBreakController's leash-break (Ally: family) -> " +
                      "the vault floor (SOREN reclaimed, closes Ladder C) -> no-cut into the Concord " +
                      "Engine throne-core -> the TimeLoopController time-loop trap (3 Khall-image " +
                      "iterations) -> the forged order (Khall repents, allies; closes Ladder D rung 3 + " +
                      "Ladder E's war-as-product) -> Maelgorn / the Obsidian Synod named (Ladder E " +
                      "converge) -> the refusal + the 3-phase Maelgorn boss -> the Engine broken to FREE " +
                      "the kept shadow-AIs -> the throne left empty. 47 mission steps. ch16_complete + " +
                      "galaxy1_complete + samurai4_recruited + khall_allied set at the outro. Every " +
                      "finale cast member resolves to a real Named prefab.");
        }

        // ---- Data assets: per-encounter EnemyDefinitions (mirrors Ch9/Ch12/Ch13's Ensure* convention). ----

        private static EnemyDefinition Ch16EnsureWardenDefinition()
        {
            const string path = DataFolder + "/Ch16Warden.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 60f;
            def.damage = 10f;
            def.moveSpeed = 1.4f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch16EnsureSamurai4Definition()
        {
            const string path = DataFolder + "/Ch16Samurai4.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 260f; // boss-tier; high enough the DuelYield threshold never crosses to death in one hit
            def.damage = 14f;
            def.moveSpeed = 1.5f;
            def.attackCooldown = 0.8f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch16EnsureKhallImageDefinition()
        {
            const string path = DataFolder + "/Ch16KhallImage.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 90f; // quick, repeated fights — three distinct loop iterations, not attrition
            def.damage = 12f;
            def.moveSpeed = 1.4f;
            def.attackCooldown = 0.9f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static EnemyDefinition Ch16EnsureMaelgornPhaseDefinition(int phase, float maxHealth, float damage)
        {
            string path = DataFolder + $"/Ch16MaelgornPhase{phase}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = maxHealth;
            def.damage = damage;
            def.moveSpeed = 1.3f + phase * 0.02f;
            def.attackCooldown = 0.9f - phase * 0.05f; // escalating aggression per phase
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch16 voice clips ourselves. ----

        private static DialoguePlayer Ch16BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter16Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch16WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter16] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch16WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter16Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch16VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch16VoiceFolder}/{clipName}.wav");
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

        /// <summary>Mirrors Ch13PlaceStoryNpc: a decorative/story NPC with no combat component.</summary>
        private static GameObject Ch16PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y; // re-add non-zero floor height (mirrors Ch9/Ch11)

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Mirrors Ch11PlaceGhostNpc: a translucent memory-figure, no Health/Enemy, never
        /// blocked or attacked — purely a visual dialogue-beat anchor.</summary>
        private static GameObject Ch16PlaceGhostNpc(string prefabPath, Vector3 pos, string displayName)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y;

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
        /// A boss built from a Named-mesh prefab with no combat rig of its own (mirrors
        /// Ch9BuildVane/Ch11BuildNamedBoss): instantiates the mesh, synthesizes an ArmR/Sword/Blade/
        /// BladeTip hierarchy, then wires <see cref="Enemy"/> onto it. Returned inactive is the caller's
        /// job. Used for Samurai-4, the three Khall-image loop ghosts, and Maelgorn's three combat phases.
        /// </summary>
        private static Enemy Ch16BuildNamedBoss(string prefabPath, Vector3 pos, string displayName, EnemyDefinition def, Health playerHealth)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            FitNamedCharacter(go);
            go.transform.position += Vector3.up * pos.y; // re-add non-zero floor height (mirrors Ch9/Ch11)
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

        /// <summary>Builds one ghost-tinted Khall-image loop encounter at the fixed seam-approach spot,
        /// numbered for unique naming. Inactive until its own Trigger step.</summary>
        private static Enemy Ch16BuildKhallImage(EnemyDefinition def, Health playerHealth, Material ghostMat, int iteration)
        {
            var enemy = Ch16BuildNamedBoss(Ch16KhallPrefab, new Vector3(0f, -30f, 228f), $"Khall (Loop Image {iteration})", def, playerHealth);
            foreach (var renderer in enemy.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = ghostMat;
            enemy.gameObject.SetActive(false);
            return enemy;
        }

        /// <summary>Same shape as the shared BuildAccentPointLight, but returns the GameObject — needed
        /// here because the reality-fold's TimeLoopController phase groups must reference these lights.</summary>
        private static GameObject Ch16BuildAccentLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return go;
        }

        // ---- Geometry: Y-aware tier/floor/ramp builders (BuildFloorCeiling hardcodes y=0, which does
        // not fit a chapter that descends — these mirror Ch11BuildTier/Ch11BuildRamp's Y-aware idiom). ----

        /// <summary>An enclosed corridor tier: floor + ceiling at the given TOP-surface center (walkable
        /// surface = topCenter.y), mirrors BuildFloorCeiling's shape but Y-aware.</summary>
        private static void Ch16BuildTier(Transform parent, string name, Vector3 topCenter, Vector3 size, Color floorColor, Color ceilColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name + "_Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(topCenter.x, topCenter.y - 0.1f, topCenter.z);
            floor.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            var ceil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceil.name = name + "_Ceiling";
            ceil.transform.SetParent(parent, false);
            ceil.transform.localPosition = new Vector3(topCenter.x, topCenter.y + RoomH, topCenter.z);
            ceil.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            TintShared(ceil.GetComponent<Renderer>(), ceilColor);
            Object.DestroyImmediate(ceil.GetComponent<Collider>());
        }

        /// <summary>An open platform — floor only, no walls/ceiling (the memory-space floor and the
        /// monumental throne-core, where "the architecture stops being architecture").</summary>
        private static void Ch16BuildFloorOnly(Transform parent, string name, Vector3 topCenter, Vector3 size, Color floorColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name + "_Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(topCenter.x, topCenter.y - 0.1f, topCenter.z);
            floor.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            TintShared(floor.GetComponent<Renderer>(), floorColor);
        }

        /// <summary>A walkable, comfort-safe sloped ramp between two TOP-surface points (mirrors
        /// Ch11BuildRamp) — every tier transition in this chapter uses this, never a forced camera move.</summary>
        private static void Ch16BuildRamp(Transform parent, string name, Vector3 from, Vector3 to, float width)
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
            TintShared(ramp.GetComponent<Renderer>(), new Color(0.42f, 0.4f, 0.36f));
        }

        /// <summary>A worldspace "CHAPTER 16 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch16BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 16 COMPLETE Canvas");
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
            label.text = "CHAPTER 16 COMPLETE\nTHE SAGA ENDS";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 48;
            label.color = new Color(0.95f, 0.9f, 0.8f);
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

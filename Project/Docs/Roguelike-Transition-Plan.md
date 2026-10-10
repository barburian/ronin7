# Ronin7 — Full Roguelike Transition Plan

Status: **proposal, 2026-10-10.** Source: `Ronin7_VR_Roguelike_Development.pdf` (external research
report). This plan applies that report to this repo, and its starting point is what
`Roguelike-Design.md` already shipped (PR #1, merge `bbc0080`).

## 0. Where we actually are

The report treats ronin7 as "a utility repo with no game loop on top". **That's out of date.** A
seeded 15-node run mode already exists and is tested. Each item from the report is listed below
with its current state, so we don't rebuild work that's already done:

| Report recommendation | State in repo | Verdict |
|---|---|---|
| Deterministic seeded generation (Repeatability) | `RunRng`, `RunMapGenerator`, golden-vector tests (A2.5) | **Done.** Extend it to room layouts (Phase 3). |
| Data-driven content via ScriptableObjects | `BoonDefinition`/`BoonCatalog`, `EnemyDefinition` (39 assets), `ArenaRoomLibrary`, `EnemySpawnTable` | **Done.** Add more content, not more architecture. |
| Decoupled events instead of hard refs | `Core/EventBus` (type-keyed pub/sub), `RunEvents`, `CombatEvents` | **Done.** The decoupling goal is met, so a ScriptableObject event-channel rewrite isn't worth the churn. |
| Kill singletons | 10 `static Instance` managers (`GameFlowManager`, `RunDirector`, `AudioDirector`…) | **Leave as is.** They're tested, persist across scenes, and a rewrite is high-risk with no player-facing gain. Only fix a singleton when it causes a specific bug. |
| ECS | — | **No.** The concurrent-enemy cap is 4 (A2.2). ECS is meant for swarms, which we don't have. |
| Single Pass Instanced / Multiview | `OculusSettings`: Desktop=SPI, Android=Multiview; all 6 custom shaders use `UNITY_VERTEX_OUTPUT_STEREO` | **Done.** |
| Post-FX culling per tier | Quest drops HQ bloom + motion blur (CLAUDE.md) | **Done.** Keep SSR/SSAO/lens flare off (audit them in Phase 5). |
| Atomic save + corruption recovery | `SaveSystem.WriteAtomic` (tmp + `File.Replace`), migration | **Half done.** No rotating backups, and **runs are not persisted at all** (see Phase 1). |
| Swept / continuous blade collision | `BladeDamager` uses `OnTriggerEnter` only | **Gap.** Fast swings can tunnel through thin colliders (Phase 2). |
| Physical weapon with ghost hand (PID follow) | `PoseFollower` snaps the hand to the tracked pose in `onBeforeRender` | **Gap.** This is a feel experiment, gated behind a flag (Phase 2). |
| "No beheading rule" / fair permadeath | Windup tint telegraph, `ScreenFader`, no camera shake | **Partial.** There's no rule against attacks from outside the field of view and no per-hit damage cap (Phase 2). |
| Physicalized power (not just % stats) | 7 boon kinds, mostly multipliers | **Gap.** Phase 3 adds boons you can see and feel. |
| Diegetic inventory / holsters | `KatanaHolster`, `RunHudPanel` (floating panel) | **Partial.** Boons are passive, so no backpack is needed; the HUD moves to the wrist (Phase 4). |
| Flat procedural layouts on a grid | One runtime box room per biome (`ArenaGeometryBuilder`) | **Gap.** Phase 3. |

So "full transition" isn't about architecture. It means four things:
**(1) roguelike becomes the only product (campaign removed), (2) a run survives a crash, (3) combat is fair and physical,
(4) there is enough content to replay.**

## Decisions

1. **Story campaign: REMOVED COMPLETELY. Roguelike only.** (Decided 2026-10-10.) This supersedes
   the "never delete chapter scenes" reuse mandate in `Roguelike-Design.md` §2. That doc needs a
   one-line amendment saying so. Assets the run mode consumes (see the keep list in Phase 0) are
   *not* campaign and stay.
2. **Resume semantics. (Open; recommendation below.)**
   - *Recommended:* save at **node entry**. Quitting or crashing mid-node resumes at the start of that
     node, with the HP you had on entering it. This stops save-scumming and loses at most one room.
3. **Physical-follow sword (Phase 2.3). (Open.)** Ship it only if in-headset playtests prefer it to
   the current 1:1 tracking.
4. **Space flight / ship prologues. (Open; recommendation: remove with the campaign.)** The Ship
   assembly only exists to fly between chapters. A "space node" could come back later as a Phase 3
   node type, built on the code in git history.

---

## Phase 0 — Remove the story campaign (M)

Goal: the only game is the run loop. The campaign code is closely tied to the systems we keep, so
it has to come out **in this order**. Each step is one compiling, green-gate commit.

**Keep list.** These are not campaign, and the run mode depends on them:
- **Enemies:** `EnemyDefinition` assets (bosses/mobs), enemy art prefabs under
  `Art/Generated/Characters3D/`, and the materials/shaders they use
- **Player kit:** the five ability controllers (`WeakpointSight`, `Overdrive`, `PhaseStep`,
  `UnbrokenWard`, `MirrorSummon`), the parkour/climb kit, holster and haptics
- **Audio:** `AudioDirector`, VO clips (an optional narrative pool for Phase 4), ambience
- **Ship pieces locomotion needs:** `ComfortVignette`/`VignetteRig`. These live in `Scripts/Ship`
  but are needed by locomotion, so move them to `Ronin7.Player`.
- **Infrastructure:** `SaveSystem`, `GameSettings`/`SettingsService`, `TimeScaleArbiter`,
  `EventBus`, `LightBudget`, `RendererTint`, `ProjectilePool`
- **Story lines (optional):** `ChNNLines.cs` dialogue data. Keep it only if Phase 4 narrative
  fragments are wanted. Otherwise delete it with the builders.

**Step 0.1 — Cut the campaign's hooks into kept systems** (code only, nothing deleted yet)
- **Ability ownership.** `AbilityAccess.Has` drops `CampaignState.HasAbility` and becomes
  run-grants + meta unlocks. Delete `ParryFlowController`'s campaign read.
- **Enemy scaling.** `Enemy`/`EnemyDefinition` scale off `RunScaling` (sector/node) instead of
  `CampaignState.GalaxiesCompleted`.
- **Save path.** `GameFlowManager` stops calling `CampaignState.ApplyFrom/ToSaveData/NoteLanding/
  NoteZoneCompleted/Reset`, and `StartCampaign` is deleted.
  - Options persistence moves out of the slot save into `settings.json`; `SettingsPanel` currently
    saves via `CampaignState.ToSaveData`.
  - `DrillBestScores` moves to `meta.json` or is deleted. The dojo drill is campaign content, so the
    recommendation is delete.
- `MainMenuController`: Start Run is the main button, plus Settings and Quit. Delete
  `OnStartCampaignClicked`.

**Step 0.2 — Build a run hub.** `Galaxy1_Ch1_Hub` is the Ch1 mission *and* the hub, so it goes.
- Build `RunHub.unity` with a new `Editor/Roguelike/RunHubBuilder`. Reuse the
  `HubBuilder`/`XRRigBuilder` helpers, copying the generic ones *before* step 0.3 deletes
  `HubBuilder`.
- The hub holds:
  - a meta-upgrade terminal spending Echoes
  - Start Run / Resume Run
  - a summary of the last run, and the seed
- `RunDirector` returns to `RunHub` instead of `Galaxy1_Ch1_Hub`.
- `EditorBuildSettings` contains only: `Phase6_Boot` (or a renamed `Boot`) → `RunHub` → `RunArena`.

**Step 0.3 — Delete campaign content** (one commit per bullet, so each can be reverted alone)
- **Scenes:** `Galaxy1_Ch1_Hub`, all `ChNN_*` and `ChNN_Prologue`, `ParkourGrounds`, `1.unity`.
- **Editor:** `Chapter*Builder.cs`, `Chapter*VoiceManifest`, `ChapterVoiceWirer`,
  `ChapterEnemyVarietyWirer`, `ChapterRoomDetailsVarietyWirer`, `ChapterSharedBuilders` (after
  copying what `RunHubBuilder`/`ArenaGeometryBuilder` use), `HubBuilder`, `ShipPrologueBuilder`,
  `ParkourLevelBuilder`, `CrowdArtWirer`, `SceneContinuityFix`, `ContinuityAudit`.
- **Runtime:**
  - `Flow/CampaignDirector`, `MissionLauncher`, `CampaignStatsTracker`, `SaveSlotMenuView`
  - `Core/CampaignState`, `CampaignStats`, `Galaxy1Progress`, `GameMode`, `PrologueSceneNames`,
    `ShipSelection`, `ZoneEvents` (if it has no other consumers)
  - `World/Story/*`: keep `DialoguePlayer`/`DialogueLine` only if Phase 4 is wanted
  - story-only enemies/triggers: `SelfSeveranceTrigger`, `DreamReckoningTrigger`,
    `ConfessorLink`, `CollapseSequenceController`, `HullBreachRepairController`, …
  - `Player/ChapterOutro`, `Wormhole`, `DojoDrillController`
- **Ship (decision 4):** after moving the vignette, delete flight gameplay and `Ronin7.Ship.asmdef`.
  Fix the references in Flow/Audio/Player/Core first: `InputResolver`, `Health`, `AudioDirector`,
  `SettingsService` and `GameFlowManager` all mention Ship types.
- **Tests:** delete the tests for each deleted type in the same commit: the 16 `Chapter*LinesTests`,
  `Campaign*Tests`, `ChapterOutroTests`, `HubState…`, ship tests…
- **Data:** chapter-only `EnemyDefinition`s that no spawn table or boss list references; chapter
  `.prefab`s; voice clips if Phase 4 is dropped.

**Step 0.4 — Orphan sweep and docs**
- Run the existing orphan-material report. Use reversible cleanup only: move to an `_Orphaned/`
  folder or regenerate. Never auto-delete, per CLAUDE.md.
- Rewrite CLAUDE.md:
  - the assembly table loses Ship/Story
  - the scenes line becomes Boot/RunHub/RunArena
  - remove the Sun-nav and Ch1-hub pitfalls
  - give the real test baselines
- Retire the stale campaign docs (`CHAPTER-BUILD-LEDGER`, `Ch01-Scene-Construction`,
  `SunNavigation-Design`) to `Docs/Archive/` and update `INDEX.md`.
- **Migration:** existing player slot files hold campaign data. On first boot, `SaveSystem.Migrate`
  ignores the campaign fields; `meta.json` is untouched.

**Gate:** the EditMode count will **drop** by the number of deleted campaign tests. That drop is
expected. Record the new baseline in CLAUDE.md and require 0 failures, 0 skips. Before the first
deletion, compile the full assembly graph to prove nothing kept references a deleted type, by
running `-batchmode -quit` and checking the log.

## Phase 1 — Recoverability: crash-proof runs (M) — highest priority

Today `RunState` is a static class that lives only in memory. A headset disconnect, an OpenXR
runtime crash or a Quest thermal kill **deletes the run**. The report rightly names this as the
fastest way to lose player trust.

- **`Core/Roguelike/RunSaveData`** (JsonUtility, versioned) captures:
  - `seed`, `nodeIndex`, `boonIds[]`, `playerHealthAtNodeEntry`, `revivesSpent`, `rerollTokens`,
    `echoesPending`, `schemaVersion`
  - a CRC32 over the payload
- **`RunState.Snapshot()` / `RunState.Restore(RunSaveData)`.** These are pure functions with
  EditMode round-trip tests and golden-file tests.
- **Write points:** `RunDirector` writes `run.json` through `SaveSystem.WriteAtomic` on node entry
  (decision 2) and deletes it on `RunState.End()` (win or death).
  - Delete before publishing the run summary, so that a crash during the summary can't resurrect a
    dead run.
- **Rotating backups in `SaveSystem`.** This covers all three save files (slot, `meta.json`,
  `run.json`).
  - Before each write, shift `file → file.bak1 → file.bak2`.
  - On load, if the CRC or JSON is bad, fall back to bak1 and then bak2, and log a warning.
  - If all three are bad, the run is discarded. Meta progress is never discarded: it is rebuilt from
    defaults plus the best backup.
- **Boot flow:** if `run.json` is valid, the hub shows **Resume Run / Abandon Run**. Abandoning
  banks Echoes exactly as a death would.
- **Echo integrity:** `MetaProgression.Save()` runs at the same node-entry point, so Echoes earned
  before a crash are never lost.
- Tests:
  - corruption matrix: truncated file, wrong CRC, wrong schema version, missing file
  - resume restores identical `RunMapGenerator` output for the same seed
  - an abandoned run banks Echoes

## Phase 2 — Reliability & fairness in combat (M)

### 2.1 Swept blade hits (Reliability)
- `BladeDamager`: keep the trigger as a fast path, and add a **swept capsule cast** from the
  previous-frame blade segment to the current one.
  - Use `Physics.CapsuleCastNonAlloc` with a reusable buffer, so it allocates nothing.
  - Run it in `FixedUpdate`, interpolating the tracked pose.
- Deduplicate hits per swing by `IDamageable`, so trigger and sweep never double-hit.
- Put the sweep geometry in a pure static helper (like `SmoothSpeed`) so EditMode tests can cover
  the case that tunnels today: a 3 m/s+ swing through a 5 cm collider.

### 2.2 No-beheading rule (fair permadeath)
- **Off-FOV gate:** `MeleeAttacker` may not enter `Windup` while the enemy is more than ~60° from
  head-forward, unless a spatial audio warning cue has played at least 0.6 s earlier. Reuse the
  `EnemyProximityWarning` pattern from Ship.
- **Per-hit cap:** a single enemy hit deals at most X% of the player's max HP (start at 35%; tune
  it). Bosses use telegraphed multi-hit combos, never one-shots.
- **Death presentation audit:** fade to black in one frame through `ScreenFader`, with no camera
  takeover and no ragdoll cam. Then show the run summary.
- Tests: pure logic for the FOV/cue gate, and a cap test against `Health`.

### 2.3 Physical-follow sword — experiment, behind a flag
- Add a `PhysicalBlade` component. The blade follows the hand through a velocity-based PD on a
  kinematic→dynamic Rigidbody, and stops against blocking colliders (enemy blade or shield).
- Show a **ghost hand/blade** at the true tracked pose whenever drift exceeds about 5 cm.
- It's off by default, like sun-nav, until playtests confirm it.
- Perf: run one Rigidbody solve per hand per fixed step. Profile on Quest against the 11.11 ms
  budget.

### 2.4 Hit reactions
- The report suggests partial ragdolls on bones. The Tripo rigs plus the Quest budget make that
  expensive, so instead add a **procedural additive bone kick**: offset the hit bone along the swing
  vector and spring it back over about 150 ms.
- Damage scales with swing speed, so a light tap and a heavy slash look different.

## Phase 3 — Content depth & repeatability (L)

Without variety, a roguelike is just a short game. This phase is where the product gets made.

### 3.1 Procedural rooms on a grid
- Replace the single box room with **grid-snapped room templates**, authored as data in
  `ArenaRoomLibrary`. Each template is a footprint in cells plus obstacle and cover slots.
- Every layout must be flat: no ramps or stairs (VR comfort). Height changes use separate floors or
  teleport pads only.
- Generate with `RunRng` from the node seed.
- Add a **BFS navigability validator**: every spawn point and the exit must be reachable, with
  room-scale clearance around the player spawn. Rejected layouts are rerolled deterministically.
- Tests:
  - golden vectors for layouts
  - the validator rejects a deliberately blocked template
  - the room never exceeds the configured play area

### 3.2 Physicalized boons (new `BoonEffectKind`s)
Power should be something you can see and feel, not only a number. Candidates:
- **Blade length/scale** (+15% reach, visibly longer blade)
- **Heavy-strike threshold**: a lower swing-speed threshold before a swing counts as a heavy hit
- **Deflect-return**: parried projectiles fly back at the shooter
- **Parry window**: needs `PerfectParryWindow` turned from a `const` into a modifier slot, which
  A1.3 currently blocks
- Elemental on-hit statuses: burn, shock, using the existing `CombatVfxController`

Also add **duo boons**, which unlock when two specific boons are held together. Every new kind needs
a consumer before it ships (A1.3/A1.8 invariant tests).

### 3.3 Node and enemy variety
- **New nodes:**
  - **Shop** (spend run currency)
  - **Shrine/Event** (risk/reward choice; a story NPC delivers it, see Phase 4)
  - **Challenge** (e.g. a timed or parry-only room)
- **Elite affixes:** armored, hasted, splitting. These are data on `EnemyDefinition`.
- **Per-sector spawn tables** with a difficulty curve through `RunScaling`.
- **Bosses:** rotate the shipped boss definitions (Maelgorn phases, Edition, Redactor, Dreaming
  Archive…) per sector, chosen by seed.

### 3.4 Meta-progression depth
- More `MetaUpgradeId`s, such as unlocking new boons into the pool and new starting weapons from
  `WeaponDefinition`.
- An optional **Heat/difficulty** dial for replays.
- **Daily seed** (date → seed), shown in the hub.

## Phase 4 — Diegetic UI & narrative fold-in (M)

- Move `RunHudPanel` to a **wrist display** on the off hand: HP, boons, node, Echoes. It appears
  when you turn your wrist toward you. Make it large and easy to read, sized for arm's reach.
- The boon offer panel stays world-space, but should render as physical "shards" you grab (reuse
  `Grabbable`). Keep the A7.2 soft-lock escape path.
- **Story through runs (Hades-style):**
  - Kessler and Echo lines from the `ChNNLines` files play at the hub and at Shrine nodes, gated by
    run count and bosses defeated. This only applies if those files are kept in Phase 0's keep
    list; otherwise drop this bullet.
  - Each boss's chapter dialogue becomes its intro and death lines.
  - The chapter VO clips are already wired, so this is mostly data.

## Phase 5 — Performance & memory soak (S–M)

- **15-node soak test** on Quest:
  - watch memory growth across arena reloads (EventBus leaks, pools not released, materials made at
    runtime by `ArenaGeometryBuilder`)
  - the budget is flat memory after node 3
- `StaticBatchingUtility.Combine` on the generated room geometry while it's still under the fade.
  Gate decorative lights through `LightBudget.ShouldAnimate`.
- Profiler capture for the worst case: 4 enemies plus a boss phase plus swept casts, against
  11.11 ms.
- Re-audit the URP assets: SSR, SSAO and lens flare off on both tiers; MSAA 4x on Quest; check
  whether TAA plus sharpening suits PCVR.

---

## Order and gating

```
Phase 0 ──► Phase 1 ──► Phase 2.1/2.2 ──► Phase 3 ──► Phase 4 ──► Phase 5 (soak; also run after 3)
                         Phase 2.3/2.4 (headset experiments, parallel, flag-gated)
```

Each phase lands as its own PR and:
- keeps the **EditMode gate green**, with new pure-logic tests for every system above
- runs the windowed PlayMode suite (batch mode can't run PlayMode here, per the 2026-09-19 note)
- gets a `/code-review` pass before merge
- needs an in-headset `verify` for anything that changes how combat feels

## Deliberately not adopted from the report

- **ScriptableObject event-channel rewrite and singleton purge:** `EventBus` already gives the
  decoupling, and a rewrite would be churn with no gain.
- **ECS/DOTS:** we have at most 4 concurrent enemies, so there's no performance case.
- **Keeping a campaign mode alongside runs:** decided against on 2026-10-10. Roguelike only.
- **Physical backpack inventory:** boons are passive, and holsters and the wrist UI cover what's
  left.
- **`/doctor`-style agent diagnostics:** this is process advice, not code. The CLAUDE.md refresh in
  Phase 0 covers it.

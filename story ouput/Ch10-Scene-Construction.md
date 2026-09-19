# Chapter 10 — Scene Construction

*The architectural contract for `Ch10_LedgerOfRust.unity`: what Chapter 10 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 10 ("The Ledger of Rust") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from. It is the first node-run of Act III's second chapter — Cassie-04 (Ally #7), Vess (Ally #8), the Sever/Ninja-2 boss duel, and the Phase-step unlock all land here.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **P0 — as shipped, the chapter cannot be completed past Tier 2.** `ZoneBounds.fallResetY` (`Scripts/World/ZoneBounds.cs:18`) defaults to `-3f`, and `Chapter10Builder.cs` never overrides it. `ZoneBounds.LateUpdate`'s fall-catch branch fires **every frame** the rig's y is below `-3` and teleports it back to `lastSafePosition` — and the branch that updates `lastSafePosition` cannot run while the rig is below that threshold, so there is no recovery. The instant the player descends `Ramp2` toward Tier 3 (y=−4.5), they are yanked back to Tier 2, every frame, permanently. **The Strongroom, both remaining `DefeatEnemies` gates, the Archive, Sever, Cassie-04, the Phase-step unlock, Vess, and the ending are all unreachable in the current build.** Setting `bounds.fallResetY = -12f` (below the Archive floor at y=−9) is a prerequisite for the scene functioning at all — not a comfort tune, not an art-pass nice-to-have. Full technical writeup in §2 and §9; this bullet, not those, is the document's #1 finding, ranked above every art/registry gap below.
- **P0 (second, independent blocker) — `Archive_WallN` seals off `ReturnRamp`'s low end, even after the `ZoneBounds` fix above.** `Archive_WallN` (`Chapter10Builder.cs:195`) is built at `archiveCenter + (0, RoomH/2, 12)` = world **(0, -7.2, 120)**, size (20, 3.6, 0.2) — a solid, full-width slab spanning x∈[-10,10], y∈[-9,-5.4] at z≈120, with no doorway or side gap. `ReturnRamp` (`Chapter10Builder.cs:203`) is built starting from `archiveCenter + (0,0,12)` = **(0,-9,120)** — the exact same point, at the Archive floor's own y=−9 — running out to the ship entrance, width 16 (x∈[-8,8]), fully inside the wall's x-span. A player who clears the `ZoneBounds` fix, kills Sever, and hears the names, then turns north to climb out, walks straight into a solid wall instead of `ReturnRamp` — **Beats 4–5 (the climb-out, Vess, the target list, the ending) are unreachable behind this second, independent blocker.** No playtest has caught it yet, because `ZoneBounds` already stops the player on Tier 2. Fix: cut a ramp-width aperture in `Archive_WallN` at z=120, or start `ReturnRamp` a couple meters north of the wall (e.g. z≈122). Full geometry in §2 and §Beat 4b; **verify in-editor immediately, alongside the `ZoneBounds` fix** — see §9 and §8 item 9.
- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter10Builder.cs`, entry point `XRRigBuilder.BuildChapter10LedgerOfRust()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 10 — The Ledger of Rust**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch10_The_Ledger_of_Rust.md`, `Ch10_The_Ledger_of_Rust_Dialogue_Script.md`, `00_STORY_BIBLE.md`) and the dialogue data module `Chapter10Lines.cs`.
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The two syndicate/automata skirmishes, the Sever/Ninja-2 boss duel, and the Vess DuelYield encounter are this chapter's four combat beats, and every one of them carries impact feedback through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never the camera. This applies equally to Phase-step: the blink-through-matter effect is sold by the world briefly smearing dark/cold around the player, a systems-design visual, never a camera punch or shake.
- **Phase-step is an ability-blink, not a traversal-locomotion hole.** The "no teleport locomotion" rule below governs how the player *walks* the chapter; Phase-step is a granted combat/mobility ability that instantaneously relocates the player's rig through solid matter, and an uncushioned positional blink is exactly the discomfort class VR design guidance flags. **The dark/cold smear (§1.1 above, `Vfx.PhaseStepBlink`, Appendix B) must double as a comfort screen-fade/vignette pulse across the blink's translation** — full-opacity or near-full-opacity for the frame(s) the rig is actually relocated, clearing on arrival — not merely a stylistic color effect layered over a visible slide. This is a non-negotiable VR-comfort requirement on the effect, not a polish note, and it must be authored before `Vfx.PhaseStepBlink` ships (§Beat 2f, Appendix B).
- **Traversal in Ch10 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/movement. There is **no teleport locomotion and no NavMesh** — but unlike every prior chapter, Ch10's vertical descent *is* built with genuine Y variation: six switchback tier platforms descend from y=0 to y=-9, linked by **tilted ramp colliders** the existing `CharacterController` climbs like any sloped floor (the same idiom Ch6's ascent and Ch4's Deepworks descent use, inverted here). This is not a new locomotion mechanic — it is sloped-floor walking, already supported — but it is the first chapter since Ch4/Ch6 to commit real vertical geometry rather than flattening a "descent" onto one flat corridor the way Ch1 and Ch9 do. Do not introduce a bespoke climb/rappel/dive rig when patching this scene without a design sign-off.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | tier/room shells, ramps, props, VFX, decorative lights, and any inert/decorative character placement (Cassie-04, built active with no combat component — mirrors Ch9's Sable-at-the-Tide-Depths treatment) | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC/enemy spawns with behavior (Gryph, syndicate guards, mine automata, Sever, Vess), reach points, ability granters, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**There is no door idiom to split in this chapter**, the same finding Ch9's doc records — Ch10 has zero `BuildSlidingDoor` calls; every gate is a `ReachTrigger` mission step, or (for the Program strongroom) a sealed slab (`SealedProgramDoor`) that the level routes *around* rather than through. **This slab is not decorative — its default `BoxCollider` is a real physical blocker.** It sits astride `Ramp3` (Tier3→Tier4) and occupies ~85% of the ramp's 10 m width, leaving only a narrow western sliver passable; see §3 and §Beat 1c for the full geometry. Where Ch1's contract calls out "the one object that spans both is a door," Ch10 has **two** such split objects instead of one:

- **Sever/Ninja-2** — `BuildBeat2Art()` instantiates his inactive `GameObject`; `BuildBeat2Logic()` owns the `AuthorDefeatStep` wiring that calls `SetActive(true)` on him at runtime (via `MissionDirector.BeginDefeatEnemies`, not a separate `Trigger` step). A plain kill — no mercy branch (§4, Beat 2).
- **Vess** — `BuildBeat4Art()` instantiates her inactive `GameObject` and its `DuelYield` component; `BuildBeat4Logic()` owns the `Trigger` step that reveals her and the `Prompt` step that blocks the mission spine on `DuelYield.onAccepted` (§4, Beat 4). Mechanically identical to Ch4's Kerrax fight — a `DuelYield` boss reused rather than reinvented.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildEnemy`, `BuildWaveSpawner`, `Author*Step`, `AttachPlayerAbilities` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, including `AllyCombatant`, `DuelYield`, `AbilityGranter`, `EnemyWaveSpawner`, `ActivationRelay`, and `FactionCombatant` (all shared components, not Ch10-local).
- **Free to restructure:** the Ch10-local helpers, prefixed `Ch10` and called only from `BuildChapter10LedgerOfRust()` — `Ch10EnsureSyndicateGuardDefinition`, `Ch10EnsureMineAutomatonDefinition`, `Ch10EnsureSeverDefinition`, `Ch10EnsureVessDefinition`, `Ch10BuildDialogue`, `Ch10WireVoiceClips`, `Ch10PlaceStoryNpc`, `Ch10PlaceAlly`, `Ch10BuildNamedBoss`, `Ch10BuildTier`, `Ch10BuildRamp`, `Ch10BuildArchiveRackRow`, `Ch10BuildCompleteCanvas`, `Ch10BuildGangWarPocket`, `Ch10BuildBrawler`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 10 and are now silently rebuilding thirteen other chapters.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** The same two ScriptableObjects introduced by the Ch1 refactor carry everything Ch10's builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch10Environment.asset` | directional key, ambient mode + color, fog mode/color/density, per-tier floor tint (rust→Program-blue gradient), **sixteen** accent lights (the most of any chapter to date — six explicit + six per-tier `_PatrolLight`s that reuse the tier gradient color + two Archive + two ship-entrance), `ConsoleFlicker`/`AmbientPulse` behaviours |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document — **the same shared registry instance as every other chapter**, extended with Ch10's new keys (§Appendix B) |

Neither `ArtAssetRegistry` nor `Ch10Environment.asset` exists yet (repo search: `class ArtAssetRegistry` / `class ChapterEnvironmentProfile` match zero files, same finding as every prior chapter's doc).

Prefab **paths never appear in builder code.** The builder asks the registry for `Props.OreCart`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`.** Ch10 reuses the Characters3D/Named and Characters3D/Enemies folders that already exist (Gryph, Cassie-04, Sever_Ninja-2, Echo, Vess's placeholder, and the additively-wired enemy meshes) and needs new environment folders as siblings of them:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today
  Rooms/                                    ← exists; Ch10 adds NinefoldArchiveShell
  Props/                                    ← exists; Ch10 adds TierPlatformKit, DeadLiftCage, OreCart,
                                               SyndicateGuardPost, SealedProgramDoor, ArchiveRack,
                                               SalvageRig, KatanaWorkbench (voice-only Beat 0 dressing)
  VFX/                                      ← exists; Ch10 needs a Phase-step blink VFX that does not
                                               exist even as a concept yet (§Appendix B)
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — identical finding to every prior chapter's doc.**
>
> `BuildChapter10LedgerOfRust()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter10Builder.cs:116`). That call **discards the entire scene** and opens a fresh empty one — there is nothing left to search for. Making the safe zone real requires **replacing the wipe strategy**:
>
> 1. `EditorSceneManager.OpenScene(Ch10ScenePath)`, then `DestroyImmediate` each **generated root by name** (`Ninefold`, `Game`, `Mission`, the rig, the sixteen accent lights, dialogue players, reach points, the gang-war spawner, the two ability/duel components), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred**, reusing the same `EnemyArtWirer.cs` / `CrowdArtWirer.cs` idempotent-mutate-and-save pattern every other chapter's doc recommends.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero *environment* prefabs exist for Ch10.** No tier-platform kit, dead lift-cage, ore cart, syndicate guard-post, sealed Program door, archive rack, or salvage rig. Four named-character prefabs (Gryph — reused from Ch9, Cassie-04, Sever/Ninja-2, Echo) resolve to real art, one (Vess) resolves to a placeholder Humanoid archetype, and the enemy meshes resolve additively — see Appendix B for the full inventory.

A builder that instantiates from an empty registry produces **an empty mine** — the first run of the refactored builder would destroy Chapter 10.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_OreCart);
if (prefab == null) {
    Debug.LogWarning($"[Ch10] {ArtKey.Props_OreCart} unresolved — primitive fallback.");
    BuildOreCartPrimitive(staticArtRoot, pos, tint);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623`. The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No Ch10-specific greybox baseline has been recorded yet.** `Project/Docs/CHAPTER-BUILD-LEDGER.md` tracks Ch10's EditMode test contribution (+14 fixtures: `PhaseStepSolver` 7, `Chapter10Lines` 7, landing the running total at 496 as of the 2026-07-03 pass) but carries **no `UnityStats` draw-call/tris row for Ch10**, the same gap Ch9's doc flags. **Establish one at the first `script-execute` `UnityStats` read of the fresh build** and record it here before any prefab swap — do not invent a number.
- **This is a plausible perf risk case for a new reason:** Ch10 authors **sixteen** point lights (the most of any chapter so far, six more than Ch9's ten), a six-tier switchback descent with its own floor/rock-prop/light per tier, a full 20×24 enclosed Archive room, an 8-prop rack row, a 6-brawler gang-war pocket layered on top of the mission-spine encounters, and — at its single busiest moment — the Tier-4 gang war can be live (6 `FactionCombatant`s) at the same time the player is simply walking past on the way to the automata tier, meaning this "ambient" extra content adds simultaneous-actor load the mission spine itself never asks for. Re-measure at Tier 4 first when establishing the missing perf baseline. **On the sixteen-light count specifically:** the accent lights are spread across the full ~180 m z-run (spawn z=6 through the ship-entrance lights at z=155) and, given the switchback layout plus exponential fog (density 0.015, ~35% visibility by 70 m per §Beat 1c), are rarely co-visible — the number that actually threatens the 72 Hz floor is the **per-frame lit-light count in a single view**, not the raw total of sixteen. The Tier-4 measurement above is the right one to prioritize; this just heads off reading "sixteen lights" as "sixteen lights' worth of cost." (The two animated behaviours among them are already Quest-tier-gated — see Appendix A.1.)
- Set-dressing props today are cheap primitives tinted via the shared `TintShared` MaterialPropertyBlock batching convention — the tier floors and patrol lights explicitly lerp color per-instance along the rust→Program-blue gradient rather than authoring six unique materials, the same "cheap primitive, deliberate palette" economy every prior chapter's doc documents. **Prefabs replacing them must carry their own materials and will not batch this way.** Re-measure after every prefab lands.

## 2. Chapter spatial map

Chapter 10 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch10_LedgerOfRust.unity`, and unlike every prior chapter it is not a flat +Z corridor — it is a genuine **vertical switchback descent**: a mine-entrance platform, six alternating-X, descending-Y tier platforms linked by tilted ramps, an enclosed Archive room at the bottom (y=-9), one long return ramp climbing back to y=0, and an open-air ship-entrance platform at the far +Z end. There is no branching and the only backtrack is the scripted "climb out" ramp itself (a single new traversal segment, not a walk back through the six tiers a second time).

```
 -Z (spawn, y=0)                                                                                        +Z (dead end, y=0)
 SpawnGround ──Ramp0── Tier1 ══ Tier2 ══ Tier3 ══ Tier4 ══ Tier5 ══ Tier6 ── Archive ──ReturnRamp── ShipEntrance
 (0,0,10)              (2,-1.5,34)(-2,-3,48)(1,-4.5,62)(-1,-6,76)(2,-7.5,90)(0,-9,104) (0,-9,108)         (0,0,170)
 x[-5,5] z[0,20]        each a 10×10 open platform, alternating x, descending y — no walls, no ceiling      x[-8,8] z[158,182]
                        guards@Tier2  sealed door@Tier3  gang war@Tier4  automata@Tier5   ↓                exterior, no walls
                                                                                     20×24 enclosed room
                                                                                     x[-10,10] z[96,120]
                                                                                     Sever + Cassie-04
```

| Beat(s) | Location | Footprint / platform size | Floor center / Y | Notes |
|---|---|---|---|---|
| 0 (briefing) | Spawn Ground (mine entrance) | x[-5,5], z[0,20] | center (0,0,10), 10×20 | flat floor, y=0 — player spawns here |
| 1 (descent) | Tier 1 | 10×10 platform | (2,-1.5,34) | honest rusted mine, no combat |
| 1 (descent) | Tier 2 | 10×10 platform | (-2,-3,48) | 3 syndicate guards |
| 1 (descent) | Tier 3 | 10×10 platform | (1,-4.5,62) | `SealedProgramDoor` — "the rust ends here" |
| 1 (descent) | Tier 4 | 10×10 platform | (-1,-6,76) | 2 mine automata; **extra** Tier-4 gang-war pocket (unwired to mission spine) |
| 1 (descent) | Tier 5 | 10×10 platform | (2,-7.5,90) | 2 mine automata (fight resolves here) |
| 1→2 (descent) | Tier 6 | 10×10 platform, nested inside the Archive's southern footprint | (0,-9,104) | shaft-mouth into the Archive; Sever posted exactly at this threshold |
| 2, 3 (Archive) | The Archive (shaft seven) | x[-10,10], z[96,120] | center (0,-9,108), 20×24 | enclosed room (W/E/N walls + ceiling, open S toward Tier 6); Sever, Cassie-04, the reading of the names |
| 4 (climb out) | Return Ramp | — | (0,-9,120) → (0,0,158) | single long ramp, ~13° incline, abstracts the climb back up all six tiers |
| 4, 5 (ship entrance) | Ship Entrance | x[-8,8], z[158,182] | center (0,0,170), 16×24 | exterior, no walls/ceiling — Vess's ambush, the mercy-mirror, the target-list staging |

**Note — six built tiers, not canon's nine.** Canon frames this as a nine-tier descent (the chapter's own title, "The Ninefold"), with Morrigan's build-time briefing VO counting it out loud — "The Ninefold shafts. Nine tiers, and it gets older every level you drop" (`ch10_beat0_briefing`, §Beat 0e). The spatial map above compresses this to six switchback platforms plus the Archive at "shaft seven." See §3's candor-note cluster for the full reconciliation.

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m** — used only by the Archive, the one enclosed room in the chapter. The six tier platforms and the ship entrance are open-air/open-cave volumes with no ceiling at all, the same "exterior under the sky" idiom Ch6's Iron Yard and Ch4's Deepworks use.

**These footprints are load-bearing and survive the refactor unchanged.** A tier-platform or Archive-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience.

**No doors, no walls between adjacent tiers** — every tier-to-tier transition is an open ramp, not an aperture with a lock state. The only walls in the entire chapter are the Archive's three (W/E/N):

| Wall | Position | Size | Closes |
|---|---|---|---|
| `Archive_WallW` | (-10, -7.2, 108) | (0.2, 3.6, 24) | west side |
| `Archive_WallE` | (10, -7.2, 108) | (0.2, 3.6, 24) | east side |
| `Archive_WallN` | (0, -7.2, 120) | (20, 3.6, 0.2) | the Archive's dead end (north) — and, as-built, `ReturnRamp`'s own low end (see below) |

**Y is world-space, not the raw `RoomH/2` local offset.** The builder computes each wall's Y as `archiveCenter.y + RoomH/2` = `-9 + 1.8` = **-7.2**; a reader who took "1.8" at face value would picture the walls floating roughly 9 m above the floor near ship-entrance height, which is not what ships, and would never notice they seal the Archive-floor-level `ReturnRamp` below.

The Archive's south side (z=96) is fully open — no wall — where Tier 6 feeds into it. Gating is entirely mission-spine `ReachTrigger` steps (§4), same as Ch9.

**Ramps** — seven total, replacing Ch1's three doors and Ch9's five reach-gates-only join scheme with a genuine sloped-traversal chain. Each is a single tilted `BoxCollider` primitive (no separate mesh) the `CharacterController` climbs like any sloped floor:

| Ramp | Connects | Rise / Run | Approx. angle | Width |
|---|---|---|---|---|
| `Ramp0` | SpawnGround (0,0,18) → Tier1 (2,-1.5,34) | -1.5 m / 16 m | ≈5.4° | 10 m |
| `Ramp1` | Tier1 → Tier2 | -1.5 m / 14 m | ≈6.1° | 10 m |
| `Ramp2` | Tier2 → Tier3 | -1.5 m / 14 m | ≈6.1° | 10 m |
| `Ramp3` | Tier3 → Tier4 | -1.5 m / 14 m | ≈6.1° | 10 m |
| `Ramp4` | Tier4 → Tier5 | -1.5 m / 14 m | ≈6.1° | 10 m |
| `Ramp5` | Tier5 → Tier6 | -1.5 m / 14 m | ≈6.1° | 10 m |
| `ReturnRamp` | Archive (0,-9,120) → ShipEntrance (0,0,158) | +9 m / 38 m | ≈13.3° | 16 m |

Every descending ramp is a shallow, comfortable ≈5–6° incline; the single return ramp recovers the whole 9 m of descent in one longer, still-comfortable ≈13° run — the class-header comment explicitly calls this out as "in line with Ch6's ascent ramps." No ramp anywhere in the chapter approaches an angle that would read as a real climb/scramble mechanic.

**Second P0 progression blocker — `Archive_WallN` seals `ReturnRamp`'s low end.** `Archive_WallN`'s corrected world position (above) is (0,-7.2,120), size (20,3.6,0.2) — a solid slab spanning x∈[-10,10], y∈[-9,-5.4] at z≈120. `ReturnRamp`'s low end is built at `archiveCenter + (0,0,12)` = (0,-9,120) — the *exact same z-plane*, at the Archive floor's own y=-9, width 16 (x∈[-8,8]), fully inside the wall's x-span. There is no doorway or side gap: the player exiting the Archive toward the climb-out at floor level walks into `Archive_WallN` before ever reaching the ramp. As-built, this makes Beats 4–5 (the climb-out, Vess, the target list, the ending) unreachable **even after the `ZoneBounds` P0 (§1.1) is fixed** — a second, independent blocker no playtest has caught, because `ZoneBounds` already stops the player on Tier 2 today. Fix: give `Archive_WallN` a ramp-width aperture at z=120, or start `ReturnRamp` a couple meters north of the wall (z≈122). Co-equal in priority with §1.1's `fallResetY` finding — see also §Beat 4b, §9, and §8 item 9 for the required in-editor verification.

**Reach-trigger gates** — five, gating progression exactly like Ch9's five (no doors in either chapter):

| Reach point | Position | Radius | Gates | Fires mission step |
|---|---|---|---|---|
| `SyndicateAreaReachPoint` | (-2, -2, 48) | 5 m | the syndicate-guard `DefeatEnemies` step | step 2 |
| `StrongroomReachPoint` | (1, -3.5, 62) | 5 m | the Strongroom dialogue (the "rust ends here" reveal) | step 4 |
| `AutomataAreaReachPoint` | (2, -6.5, 90) | 5 m | the mine-automaton `DefeatEnemies` step | step 6 |
| `ArchiveReachPoint` | (0, -8, 100) | 6 m | the Cassie-04/Sever confrontation dialogue | step 8 |
| `ShipEntranceReachPoint` | (0, 1, 170) | 6 m | Vess's ambush dialogue (the climb-out payoff) | step 17 |

**Player rig:** `BuildRig(refs, addLocomotion: true)`, spawning at the shared on-foot default `(0, 0, 2)` (inside `SpawnGround`'s 10×20 floor) plus `EchoPresence`. `ZoneBounds` is set to **center (0, -4, 84), radius 150** — one large bounding sphere loosely enclosing the entire descent-plus-climb-out run, centered near the mid-depth of the six tiers.

**Open-edge fall behavior.** Every tier platform is a bare 10×10 floor slab with no raised lip or kerb (`Ch10BuildTier`, Appendix A.2) — nothing stops a player from walking off a tier's open edge instead of down its ramp, and the alternating-X switchback (Tier 1 x=+2, Tier 2 x=−2, …) leaves an exposed edge beside every ramp mouth. Canon's Beat 1 production note flags exactly this risk ("Tune fall-damage… in systems and level design"). The only safety net today is the `ZoneBounds` set up on the line above: its `LateUpdate` teleports the rig back to its last grounded position whenever `transform.position.y` drops below `fallResetY` — the same fall-catch idiom Ch4's Throat-to-Kerrax's-Hold run and Ch6's ascent both rely on (`ZoneBounds.cs`'s own doc comment names it a general fall failsafe, not a bespoke mechanic). **But `Chapter10Builder.cs:226-228` never sets `bounds.fallResetY`, so it keeps the component's class default of −3.** Every chapter that has shipped this component so far stays at or above y≈0 for its whole run (Ch4's Throat/Kerrax arena, Ch6's Iron Yard at `Ch6UpperY`=12), so that default has never mattered before. Ch10 is the first chapter whose intended floor legitimately drops below it — Tier 3 alone sits at y=−4.5, and the Archive floor is y=−9 — so an unmodified `fallResetY = -3` would fire the "fell off the level" reset the instant the rig's y crosses onto Ramp2/Tier 3 and everything below, not only on a genuine off-edge fall. This needs an explicit override (e.g. `bounds.fallResetY = -12f`, comfortably below the Archive floor) — and it is not merely a comfort tune. `ZoneBounds.LateUpdate`'s two branches are mutually exclusive: the reset branch fires, and the safe-position branch that would let recovery ever re-arm does not run, whenever the rig is below `fallResetY`. So the instant the player crosses onto `Ramp2` toward Tier 3, the rig is below −3 and gets teleported back to its last grounded spot on Tier 2 — every single frame thereafter, with no way to escape the loop, because escaping it requires first being above `fallResetY`. **As shipped, this is not a misconfigured comfort failsafe — it is a hard progression block: the entire chapter below Tier 2 (the Strongroom, both remaining `DefeatEnemies` gates, the Archive, Sever, Cassie-04, the Phase-step unlock, Vess, and the ending) is physically unreachable.** See §1.1's headline callout — this is the document's #1 finding, ranked above every art/registry gap, not a lesser issue running alongside the missing edge-kerb. Both gaps are open and should be closed together — see §Beat 1b/1f.

## 3. Global environment & backdrop

**The Ninefold Shafts** read as a working mine gone to rust aging into a Program strongroom, told entirely through the tier-floor tint gradient and Echo/Coral/Sable's reads rather than exposited flat — the same "environment tells the reveal" technique Ch9's Beat 1 uses for its rack-row gradient. `Color.Lerp(rustColor, vaultColor, t)` runs across all six tiers: rust `(0.42, 0.28, 0.16)` at Tier 1 fading through Tiers 2–4 toward Program-blue `(0.2, 0.26, 0.36)` by Tier 6, and the same lerp value drives both the tier floor tint **and** its patrol-light color, so the room ages under the player's feet and overhead simultaneously with no new prop type. `SealedProgramDoor` at Tier 3 (color `(0.22, 0.26, 0.34)`, already cold Program-blue) is the beat's hard visual break — planted one tier early, right where the gradient itself is still mid-transition, so the door reads as "the vault reaching up to meet you" rather than a color coincidence.

**The door's collider is not decorative — it is the de-facto realization of canon's "handle-less door you route around" (script line 256's vent-cut flaw, otherwise unbuilt).** `BuildProp` gives it Unity's default `BoxCollider` on top of its cube mesh, and the builder places it at `tiers[2] + (0, 1.8, 5)` = **(1, -2.7, 67)**, scale **(9, 3.4, 0.4)** — spanning x∈[-3.5, 5.5] at z=67. `Ramp3` (Tier3→Tier4, z 62→76, width 10 m centered x=0) spans x∈[-5, 5] at that same z. The door therefore blocks essentially the whole ramp except a **~1.5 m gap on the open west edge** (x∈[-5, -3.5]) — and the player has no Phase-step yet (granted only in Beat 2), so this squeeze is the sole physical route down at Tier 3. This is simultaneously a soft-block risk (a player may read the ramp as impassable and backtrack) and a fall/comfort risk (the forced sliver sits directly beside the exposed open edge §2 already flags). Must be verified in-headset; either widen the gap deliberately or add the canonical vent-cut side-route as directed geometry.

**Candor note — the upper mine's open-air platform idiom trades away the "cut-stone galleries" framing.** Canon opens the SETTING with "cut-stone galleries, dead lift-cages, ore-carts seized on their rails" — "galleries" implies cut walls and tunnel enclosure. §2 documents the six tier platforms as deliberately open 10×10 volumes with no walls and no ceiling, the same "exterior under the sky" idiom Ch6's Iron Yard and Ch4's Deepworks use — sufficient for traversal but never reconciled against "cut-stone galleries." This is likely an accepted greybox simplification, on the same footing as the ore-cart/lift-cage/guard-post reconciliation now in §Beat 1c, but it is the one SETTING-block noun with no corresponding note the way ore-carts/lift-cages/guard-posts now have: the upper tiers render as open switchback platforms rather than enclosed cut-stone galleries, trading "walls closing in as you descend a real shaft" claustrophobia for open verticality. Flag, don't fix.

**Candor note — the handle-less door is staged as a recurring descent motif in canon, built as a single instance.** The SETTING block says "pressure doors with no handle**s**" (plural, deep-tier), the Beat 1 production note names "the recurring beat of a sealed door that the player must route around," and Echo's own bark-pool line (§7 — "this door's got no handle, Cipher… doors like that are built so the only way through is permission") is written as a repeated beat down the shaft, not a one-off. The build realizes exactly one instance, `SealedProgramDoor` on Tier 3 (§Beat 1c, Appendix A.2, Appendix B) — the motif's *repetition*, one sealed door per deep tier reinforcing "paranoia made architecture, tier by tier," is collapsed to a single object. This sits beside the cut-stone-galleries note above as an accepted greybox simplification, but it had no note of its own the way galleries/ore-carts/lift-cages now do. Flag, don't fix.

**Candor note — canon insists on *nine* tiers, and the build's own briefing VO says the count out loud; this had no reconciling note until now.** The chapter's title is "The Ninefold," the SETTING block frames a nine-descending-tier mine, and Morrigan's build-time `ch10_beat0_briefing` line states it plainly and audibly before the player ever touches a ramp: "The Ninefold shafts. Nine tiers, and it gets older every level you drop" (§Beat 0e). §2's spatial map compresses this to **six** switchback tier platforms plus the Archive at "shaft seven," leaving canon's remaining tiers unbuilt. Unlike the cut-stone-galleries and plural-handle-less-doors notes above — both silent visual trades the player has no line-item to check against — this is an **on-ear contradiction**: the player descends six platforms while a voice they just heard explicitly counted nine. This is the one SETTING-derived simplification in this cluster the player can catch by counting, and it had no candor note of its own until this pass. Flag, don't fix — either retune the VO's count (or the "shaft seven" framing) to match six built tiers, or accept the compression as a named greybox trade against the chapter's own title. Cross-referenced from §2 and §Beat 1a.

**The Archive** is the chapter's coldest, most saturated interior — a dry strongroom-cavern rather than the Tide Depths' drowned hush, sold by the darkest floor/ceiling tint in the chapter (`(0.05, 0.07, 0.09)` / `(0.03, 0.04, 0.05)`) against two cyan `ArchiveLight0/1` accents at `(0.3, 0.85, 0.9)` — the same cold-data-glow register Ch9's `CoreLight0/1` uses for the Concord Engine reveal, reused here because the Archive *is* this chapter's living-archive-node room, the direct sibling of Ch9's Construction Core. The eight `ArchiveRack` props (teal `(0.15, 0.55, 0.6)`, four z-positions × two sides) are "the ledger of the erased made physical" — the visual equivalent of Ch9's shadow-rack row and Ch7's reliquary racks, but catalogued rather than merely stored: per the SETTING block, "filed, indexed, catalogued, a library of the dead strung into one living node," not the Tide's raw "shadows put to work."

**The Ship Entrance** is the chapter's one open-sky exterior — no walls, no ceiling, matching the "gutted sky" the dialogue script names three times over a "dead seam-world." The relief is real (the climb-out payoff), but the sky itself reads as ravaged, not pastoral — canon frames it as a wound over a dead world, not a postcard exterior, so the warm light should sell *escape from the mine* rather than an idyllic vista that would clash with "gutted." Warm, level `ShipEntranceLight0/1` accents `(0.9, 0.85, 0.7)` read as daylight after six tiers and an Archive of cold cyan and Program-blue, the same emotional beat structure Ch1's Beat 4 windshield uses (dark interior → bright exterior reveal), compressed here into a single lighting contrast rather than a windowed backdrop.

**Audio gap, mirrored from Ch9.** The six descent tiers — where the player spends the whole of Beat 1, arguably the chapter's longest single stretch of continuous traversal — have no `BuildAmbienceLayer` bed of their own; only `ArchiveAmbience` and `DeepTierDreadAmbience` exist (both anchored near Tiers 4–5/Archive, not the upper tiers). The SETTING block stages "dead lift-cages, ore-carts seized on their rails" and later "a dry, cold, humming order" at the Archive — the second half of that contrast (the Archive's hum) is built; the first half (the upper mine's own creak/settle texture) is not. See §7.

**Note — temperature/atmosphere-by-implication is carried only by color, not by any thermal/air read.** The SETTING contrasts the rusted-out working mine up top against the Archive's "dry, cold, humming order," staging the descent as "deeper = colder/signal-dead." As documented above, this chapter sells that entire contrast through the rust→Program-blue tint gradient — which §Beat 1c's own fog-legibility note warns may be washed out by fog before the eye reads it at range. A cheap, fog-independent reinforcement — a faint breath-fog/cold-particle emitter appearing at the Tier 4–6/Archive-threshold band where the palette turns Program-blue, absent on the warm upper tiers — would carry "it gets colder as it gets wronger" even where the color shift itself is fog-washed. Proposed sensory layer, flagged, not built.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter10Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch10Environment.asset`, same schema as every prior chapter's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint` (per tier, procedurally lerped) / Archive floor+ceiling tint | `Color` | every `BuildFloorCeiling`/tier-floor call — the six-tier gradient is a *curve*, not a fixed pair, unusual against every prior chapter's per-room fixed tint |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight`, **sixteen** entries — ten explicit accents plus six per-tier `_PatrolLight`s |
| `eventLights[]` | — | **unused this chapter**, same as Ch9 — no docking-alarm-style event light; leave the array empty rather than omitting the field |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("ArchiveLight0", seed: 110f)` and `AddAmbientPulse("Tier4Light", periodSeconds: 7f)` calls with data. Current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored.

## 4. Per-beat scene spec

The chapter plays as six beats matching the six `Beat0:`…`Beat5:` label prefixes actually authored on the `MissionDirector.steps` array (24 steps total, indices 0–23): Beat 0 (the Cairn briefing, voice-only), Beat 1 (the Ninefold descent — three encounter gates across six tiers), Beat 2 (Cassie-04 and Her Keeper — boss, Phase-step, Ally #7), Beat 3 (Reading the First Names), Beat 4 (Vess's Vengeance — DuelYield boss, Ally #8), Beat 5 (the Target List). Each beat is documented with the same a–f structure used for every prior chapter.

**Table conventions, everywhere below** — identical to Ch1's/Ch9's: art tables carry Position/Rotation, Registry Key, resolved path, and Status; art tables never carry `scale()`/`size()`/`PrimitiveType`; positions/rotations encode blocking and are kept; **Status `MISSING`** means the primitive fallback is active for that row.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Create **`BuildBeat0Art()`** for the spawn-area set dressing (there is almost none — this beat is voice-only, mirroring Ch9's Beat 0) and **`BuildBeat0Logic()`** for the single dialogue player and its mission step.

#### a. Narrative purpose & emotional target

Beat 0 opens Act III's second chapter on a war-room finally armed with a plan instead of a wound: Morrigan names the scattered-archive structure the Ch9 reveal opened ("the build-record was never one book… they split it"), and Sable — now off the rack, three days a person — is the one who names the target and its stakes in the same breath: "the first of three I can feel, and the only one I think we bring up breathing." That line is the chapter's whole emotional spine stated up front; everything from the syndicate tier to Vess's mercy either earns or complicates it. Echo's aside plants the chapter's second thread quietly — "somewhere in those names is the rest of us… maybe even you" — seeding Beat 3's sealed-file beat two scenes early. Gryph's one line ("deep-shaft work… that's me") is the entire justification for his physical presence on the descent; every other crew member (Morrigan, Coral, Sable, Kessler) stays voice-only per the class-header CREW-PRESENCE DECISION, continuing the convention Ch9 established for solo-descent chapters.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at the on-foot default (0, 0, 2), facing +Z into the mine entrance. The player does not travel in Beat 0 — this beat plays out entirely as a stationary voice-over.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4).
- No NPCs are physically placed for this beat's speakers — Morrigan, Sable, and Echo are all voice-only per the class-level CREW-PRESENCE DECISION; Ronin-7's closing line is the only one voiced by a physically-present character (the player).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` — the war-room briefing, 5 lines, ending on Ronin-7's "Morrigan, plot the Ninefold. Gryph, you're on the descent. Take us down." |

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Katana "Echo" | (2, 1, 4), Euler(-90, 0, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnLight` (accent) | (0, 2.4, 6) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |
| `KatanaWorkbench` (mine-mouth framing) | (2, 0, 5) — under/beside the katana | `Props.KatanaWorkbench` | `…/Art/Generated/Props/KatanaWorkbench.prefab` | **MISSING** |

**Note on the katana.** Per the class-header comment, "the katana rides from the start (deep into Act III — no rack-wake beat, matching Ch9's 'cost, not initiation' precedent)." It is a free-standing grabbable at world (2,1,4), 2 m right and roughly at chest height of the spawn point — grab-to-arm, no pickup prompt, same seeding convention as every chapter since the blade stopped being a discovery beat.

**⚠ Carried-forward flagged gap — the grab is ungated, same finding as Ch9.** Nothing in the mission spine requires the player to pick Echo up before descending; a player who walks straight past (2,1,4) toward the tiers can reach the syndicate-guard fight (step 3) unarmed, and would have to backtrack ~30 m to spawn to arm before the fight is winnable. Not fixed here — gating the grab is new mission-spine surface, out of scope for an art pass.

**Note reconciling `KatanaWorkbench`.** §1.3's folder tree names `KatanaWorkbench` as "voice-only Beat 0 dressing," but until this pass it was never placed or registered — the fallback location (§1.5's "the player stands alone at the Ninefold mine entrance") was a bare warm-lit 10×20 slab with nothing reading as a mine mouth or staging camp. The row above places it at the katana's feet as cheap physical grounding for the chapter's opening frame; a shaft-mouth marker prop at the spawn's +Z edge (toward Tier 1) would be the next-cheapest addition if a second prop is warranted, but is not added here as it has no registry key or canon citation of its own.

**Candor note — the Cairn war-room is entirely unbuilt, the same finding as Ch9's Beat 0.** The dialogue script stages `INT. THE CAIRN — WAR-ROOM HOLO-TABLE`, "a holo of a nine-tier shaft-complex," seven crew physically gathered (Morrigan, Sable, Gryph, Coral Vex, Kessler+Mira, Resh, Iris). None of that set exists in the build — the briefing plays as disembodied VO while the player stands alone at the Ninefold mine entrance. This is the same largest single set-level divergence Ch9's doc flags, now a repeated pattern across the whole Act III arc — a candidate future `WarRoomHoloTable`/shaft-complex-projection VFX pair, flagged here, not fixed.

#### d. Combat

None. Beat 0 has zero combat components.

#### e. Dialogue / VO

Set id **`ch10_beat0_briefing`**, position (0, 1, 4), 5 lines, advanced on **Left-Hand "Talk" (Y)**:

| Speaker | Line (condensed — see `Chapter10Lines.cs` for full text) | sec |
|---|---|---|
| Morrigan | "Sable gave us the gap in the Engine… The build-record was never one book… the nearest piece, Cipher, is filed down there. The Ninefold shafts. Nine tiers, and it gets older every level you drop." | 23 |
| Sable | "Down there is one of my own kind. Racked the way I was racked… She is the first of three I can feel. And she is the only one I think we bring up breathing." | 27 |
| Sable | "The other two I can feel are wrong… This one, the Ninefold one, she's still whole… Don't make me bury all three." | 18 |
| Echo | "All right. Down again… So let's free my sister, Cipher, and bring one of her kind up out of the ground breathing for once." | 18 |
| Ronin-7 | "Morrigan, plot the Ninefold. Gryph, you're on the descent. Take us down." | 7 |

Total runtime ≈ 93 s. This is a **near-voice-only cast** set — only Ronin-7 has a physical presence at build time, so this table's "position" column is a single shared anchor rather than per-speaker blocking, the same convention Ch9's Beat 0 table uses.

#### f. Audio / Haptics / VR Comfort

- No camera shake — the whole beat is a stationary conversation; the only feel to sell is spatial audio and the warm `SpawnLight` accent (0.9/0.7/0.4) against the chapter's baseline exponential fog.
- **Correction — grabbing/arming Echo is a live input during Beat 0, not "no grab/release input."** Echo is a free-standing `Grabbable` placed at (2,1,4) by Beat 0's art (§Beat 0c), and the ungated grab §Beat 0c already flags is available throughout the stationary VO — a player can arm the katana at any point during the briefing. Grabbing/arming Echo should carry the standard grab haptic pulse via the existing `Haptics` pipeline, the same as any other `Grabbable` pickup in the chapter; no bespoke haptic authoring needed beyond that.
- Comfort vignette is inert — the player does not move during Beat 0.

---

### Beat 1 — The Ninefold Shafts (Descent / Traversal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitive cubes for the tier floors, rocks, or `SealedProgramDoor` — read `Props.TierPlatformKit`/`Props.OreCart`/`Props.DeadLiftCage`/`Props.SealedProgramDoor` from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (six tier platforms + their rock props, the six ramps, `SealedProgramDoor`) and **`BuildBeat1Logic()`** (three reach points, both dialogue players, the syndicate-guard and mine-automaton spawns and `DefeatEnemies` steps, the extra Tier-4 gang-war pocket). **Tiers 1–3 read as a rusted working mine (warm tint); Tiers 4–6 cool toward Program-original strongroom (blue tint) — the gradient is the beat's entire "environment tells the reveal" mechanism and must not collapse to a fixed palette.**

#### a. Narrative purpose & emotional target

Beat 1 is the chapter's environmental-storytelling stretch, structured almost identically to Ch9's Beat 1 but longer and with two live combat gates instead of zero: the production note is explicit that "the descent itself argues that this stopped being a mine and became a vault for the Dominion's crimes, told through art and Echo's reads, never exposited flat." The three sub-beats escalate in order — an honest mine (Tiers 1, dialogue only), a syndicate skirmish that reads as squatters "sitting on a played-out hole they can charge tolls through" (Tier 2), and the hard reveal at Tier 3's `SealedProgramDoor`: "that door, Cipher, no handle. Not on our side… that's how you lock up something you're terrified somebody will read." Coral Vex's comm line reframes the whole descent from the forebear's lived experience ("a handle-less door. I know those… you're past the part anyone was meant to reach"), and Sable's closing line personalizes the stakes right before the mine-automaton fight and the shaft-mouth into the Archive: "they never leave a node like her unguarded. Get to her."

**Candor note — the canonical rig-ride opening is unbuilt, the same class of gap as the Beat 0 war-room (§Beat 0c).** Canon opens Beat 1 with "RONIN-7 and GRYPH ride a salvage rig down past the shaft-mouth into the first tier" (script line 218); the build instead spawns the player standing on `SpawnGround` to walk down under their own power via `Ramp0`. A framed entrance — riding down, not walking on — is on record here as a deferred set-level gap, not silently dropped.

**Candor note — the canonical descent's hazard-escalation is unbuilt, the one canon-objective-level divergence with no corresponding note until this pass.** `Ch10_The_Ledger_of_Rust.md`'s GAME NARRATIVE DESIGN objective (1) reads "Descend the nine shafts (**escalating environmental hazards**)," and its Environment line names the mine as "`[CAVE]` — nine-tier vertical mine (**collapsing shafts, lift-puzzles**, deeper = older tech)." The production note goes further, calling for a TRAVERSAL KIT of "collapsing shafts, lift-puzzles (re-power and ride dead lift-cages between tiers), ore-cart rail-runs, gaps crossed on failing mine infrastructure" (script line 220). As-built, the descent is pure sloped-ramp traversal plus two `DefeatEnemies` gates — no collapse event, no lift-cage re-powering, no ore-cart rail-run, no environmental hazard of any kind. Escalation is realized entirely through the rust→Program-blue tint gradient (§3) and the two combat gates; the hazard-escalation *axis* of the canonical descent is a greybox simplification, deferred rather than dropped. This sits beside the open-edge fall risk (§2) and the rig-ride note above as a third, distinct Beat 1 traversal gap — building genuine hazard/puzzle mechanics is new gameplay-systems surface, out of scope for an art/registry pass.

**Candor note — the descent's total tier count is itself a canon compression, and this beat is where the player hears it contradicted.** Canon's nine tiers (the chapter's title, and Morrigan's voiced "Nine tiers, and it gets older every level you drop" from Beat 0, §Beat 0e) become six built platforms plus the Archive at "shaft seven" — full reconciliation in §3's candor-note cluster, cross-referenced from §2. Unlike the rig-ride and hazard-escalation gaps above, which are silent (nothing on screen contradicts them), this one is on-ear: the player descends exactly six platforms after being told there are nine.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** free-walks the descent from spawn (z≈2) through all six tiers and their ramps on continuous locomotion + snap-turn, climbing sloped ramp colliders exactly as any floor. No scripted path.
- **`SyndicateAreaReachPoint`** (-2,-2,48), radius 5 m — gates the syndicate-guard `DefeatEnemies` step on the player physically reaching Tier 2.
- **`StrongroomReachPoint`** (1,-3.5,62), radius 5 m — gates the Strongroom-reveal dialogue on the player reaching Tier 3.
- **`AutomataAreaReachPoint`** (2,-6.5,90), radius 5 m — gates the mine-automaton `DefeatEnemies` step on the player reaching Tier 5.
- **Gryph:** placed once at (2, 0, 18) — near the descent entrance, just south of Tier 1 — as an `AllyCombatant` (mirrors Ch9's Gryph/Rook: no `Health`, cannot be damaged, retargets and fights the nearest live `Enemy` automatically). Per the class-header GRYPH PHYSICAL-PLACEMENT DECISION, he stays here for both mook encounters and is **never given a walker** — he does not physically follow into the Archive (Sever's fight is a solo operative duel; see §Beat 2b). All of Gryph's later lines (the descent-guide barks, the ship-entrance approval, the target-list heading) play as decoupled `DialoguePlayer`s at their beat's location, the same "dialogue panel decoupled from the physical NPC" convention Ch9 uses for Coral Vex and, in Ch7/Ch9, for single-placement NPCs' later lines.
- **⚠ Verification gap — Gryph's ability to physically descend the switchback to reach the Tier 2 and Tier 5 fights is asserted (§Beat 1d), not confirmed.** He is a rangeless `AllyCombatant` placed once at (2,0,18), and this project has **no `NavMesh`** (CLAUDE.md). Reaching the Tier 2 guards (z≈44-50, ~26 m and two ramps down) and the Tier 5 automata (z≈86-92, four ramps down) requires his retarget/steer logic to descend the alternating-X switchback past the same open, kerb-less tier edges §2 flags for the player, with no bespoke pathing to keep him on the ramps. Should be confirmed in-editor (§8 item 10) — if he stalls at the top or walks off a tier edge, the "fights alongside" premise, and his whole justification for physical placement (§Beat 1a), silently fails.
- **Syndicate guards ×3** at (-4,-3,44), (0,-3,46), (3,-3,50) — Tier 2's floor y=-3, positions clustered there. Built `SetActive(false)`; `MissionDirector.BeginDefeatEnemies` activates all three when the `DefeatEnemies` step begins (no separate Trigger step, mirrors Ch4's snatch-team convention).
- **Mine automata ×2** at (-2,-7.5,86), (3,-7.5,92) — Tier 5's floor y=-7.5. Built `SetActive(false)`, same auto-activation idiom.
- **⚠ Flagged gap — the z=86 automaton sits behind the reach-point center, not on an earlier tier.** Tier 4 spans z[71,81] and Tier 5 spans z[85,95]; both automaton positions (z=86, z=92) sit at y=−7.5 (Tier 5's own floor height), inside Tier 5's z-range — **neither automaton is on Tier 4 or its approach ramp.** The real, minor wrinkle: the z=86 unit sits ~4 m south of `AutomataAreaReachPoint`'s center (2,-6.5,90) and just outside its 5 m radius (horizontal distance ≈5.7 m), so a player crossing the reach point at z≈90 may already have walked past it. Both automata are still built inactive and only wake on the `DefeatEnemies` step itself, so no player can be ambushed early. Flagged, not fixed.
- **Tier-4 gang-war pocket** (`Ch10BuildGangWarPocket`, called unconditionally at the end of the build, not from either `BuildBeat1Art`/`Logic`): two rival 3-brawler `FactionCombatant` crews pre-placed inactive on Tier 4's landing (y=-6+0.9=-5.1, z=74/77/79.5) — **asymmetric, not mirrored**: `RustCrewBrawler0-2` (faction 0) hug the west edge at x=−4.5/−3.5/−4.5, while `DrifterCrewBrawler0-2` (faction 1) sit center-east at x=+2.5/+1.5/+2.5; the player arrives from Ramp3 at x≈−1, i.e. **lands in the gap between the two crews.** Woken by a self-armed `EnemyWaveSpawner` + `ActivationRelay` at (-1,-6,71) with a 9 m proximity poll — `EnemyWaveSpawner.BeginRoutine` zeroes the y component before comparing (`toCamera.y = 0f`), so the poll is horizontal-distance only, and Tier 4's entire mandatory crossing from Ramp3's mouth to Ramp4's mouth falls within 0–5 m of the trigger point, well inside the 9 m radius. **No mission step references this encounter, so chapter gating is entirely untouched — but the brawl always activates when the player traverses Tier 4;** it is unavoidably triggered and witnessed, even though nothing forces the player to fight (the crews mutually wipe each other; see §Beat 1d). Mirrors Ch7's gang-war pocket pattern exactly.
- **Open-edge fall risk across all six tiers, plus the P0 progression block behind it — full writeup in §1.1/§2.** No tier has an edge kerb, and the one safety net (`ZoneBounds`'s Y-based fall-catch teleport) is left at its class default `fallResetY` of −3, above Tier 3 and everything below it — which does not merely misfire on comfort, it locks the rig on Tier 2 permanently the moment the player descends past it. Flagged in full in §1.1/§2, not fixed here.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Descent` (`ch10_beat1_descent`) — Echo + Gryph as the descent begins |
| 2 | ReachTrigger | Gates on `SyndicateAreaReachPoint` (-2,-2,48), radius 5 |
| 3 | DefeatEnemies | Syndicate Guards ×3 — activates and waits for all three `Health`s |
| 4 | ReachTrigger | Gates on `StrongroomReachPoint` (1,-3.5,62), radius 5 |
| 5 | Dialogue | `Dialogue_Beat1_Strongroom` (`ch10_beat1_strongroom`) — Echo/Coral/Sable name the sealed door; ends on Ronin-7's "Gryph, find me the way down past a door with no handle." |
| 6 | ReachTrigger | Gates on `AutomataAreaReachPoint` (2,-6.5,90), radius 5 |
| 7 | DefeatEnemies | Mine Automata ×2 — activates and waits for both `Health`s |
| 8 | ReachTrigger | Gates on `ArchiveReachPoint` (0,-8,100), radius 6 — the beat's exit into Beat 2 |

**What changes during the beat:** nothing in the tier geometry itself is added or removed — the rust→blue floor/light gradient is static from build time. State changes are purely: the two enemy groups flipping active per their `DefeatEnemies` steps, and (ambiently, off the mission spine) the gang-war brawlers waking if the player nears Tier 4's landing.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Spawn Ground (10×20 floor) | center (0,0,10) | `Rooms.NinefoldSpawnGround` | `…/Art/Generated/Rooms/NinefoldSpawnGround.prefab` | **MISSING** |
| Tier 1 platform + rocks | center (2,-1.5,34) | `Props.TierPlatformKit` | `…/Art/Generated/Props/TierPlatformKit.prefab` | **MISSING** |
| Tier 2 platform + rocks | center (-2,-3,48) | `Props.TierPlatformKit` | same | **MISSING** |
| Tier 3 platform + rocks | center (1,-4.5,62) | `Props.TierPlatformKit` | same | **MISSING** |
| Tier 4 platform + rocks | center (-1,-6,76) | `Props.TierPlatformKit` | same | **MISSING** |
| Tier 5 platform + rocks | center (2,-7.5,90) | `Props.TierPlatformKit` | same | **MISSING** |
| Tier 6 platform + rocks | center (0,-9,104) | `Props.TierPlatformKit` | same | **MISSING** |
| `Tier1_OreCart` | Tier 1 center + (-3,0.45,2.8) = (-1,-1.05,36.8) | `Props.OreCart` | `…/Art/Generated/Props/OreCart.prefab` | **MISSING** |
| `Tier1_DeadLiftCage` | Tier 1 center + (3,1.2,-2.8) = (5,-0.3,31.2) | `Props.DeadLiftCage` | `…/Art/Generated/Props/DeadLiftCage.prefab` | **MISSING** |
| `Tier2_OreCart` | Tier 2 center + (-3,0.45,2.8) = (-5,-2.55,50.8) | `Props.OreCart` | same | **MISSING** |
| `Tier2_DeadLiftCage` | Tier 2 center + (3,1.2,-2.8) = (1,-1.8,45.2) | `Props.DeadLiftCage` | same | **MISSING** |
| `SyndicateGuardPost` | (0, -2.6, 47) — Tier 2, grounding the three guards at (-4/0/3,-3,44–50) | `Props.SyndicateGuardPost` | `…/Art/Generated/Props/SyndicateGuardPost.prefab` | **MISSING** |
| `Tier3_OreCart` | Tier 3 center + (-3,0.45,2.8) = (-2,-4.05,64.8) | `Props.OreCart` | same | **MISSING** |
| `Tier3_DeadLiftCage` | Tier 3 center + (3,1.2,-2.8) = (4,-3.3,59.2) | `Props.DeadLiftCage` | same | **MISSING** |
| `Ramp0`–`Ramp5` (×6) | see §2 ramps table | `Props.MineRampSection` | `…/Art/Generated/Props/MineRampSection.prefab` | **MISSING** |
| `SealedProgramDoor` | (1,-2.7,67) | `Props.SealedProgramDoor` | `…/Art/Generated/Props/SealedProgramDoor.prefab` | **MISSING** |
| `SpawnLight` (accent) | (0, 2.4, 6) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |
| `Tier1Light`…`Tier5Light` (accent ×5) | see Appendix A.1 | — | `ChapterEnvironmentProfile.accentLights["Tier1"…"Tier5"]` | profile |
| `TierN_PatrolLight` (accent ×6, one per tier) | tier center + (0,2.2,0) | — | `ChapterEnvironmentProfile.accentLights["TierNPatrol"]`, color = the same gradient value as that tier's floor tint | profile |
| Gryph | (2, 0, 18) | `Named.Gryph` | `…/Art/Generated/Characters3D/Named/Gryph.prefab` | **EXISTS** *(shared Ch9 prefab)* |
| Syndicate guard ×3 | see Logic table | `Enemies.SyndicateGuard` | additively resolved by `EnemyArtWirer` → `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` or `Ash-World_Scavenger.prefab` | **EXISTS**, see note |
| Mine automaton ×2 | see Logic table | `Enemies.MineAutomaton` | additively resolved the same way | **EXISTS**, see note |
| Tier-4 gang-war brawlers ×6 (ambient) | see §Beat 1b | `Enemies.SyndicateGuard` / `Enemies.MineAutomaton` (faction-based) | same additive resolution | **EXISTS**, see note |

**Candor note — the mine's signature props were named but never placed.** `Props.OreCart`, `Props.DeadLiftCage`, and `Props.SyndicateGuardPost` appear in §1.3's folder-tree additions, §1.5's fallback code sample, and this beat's refactoring instruction, but until this pass they were absent from this table, from Appendix A.2's geometry, and from Appendix B's registry inventory — as-built, Tiers 1–3 render as a floor slab plus two tinted rocks with none of the "dead lift-cages, ore-carts seized on their rails, syndicate guard-posts squatting in the bones of an industry that left" texture the SETTING block repeats three times. The rows above are the reconciliation: one ore-cart and one dead lift-cage per Tier 1–3 platform, and a single `SyndicateGuardPost` on Tier 2 grounding the three guards' positions. **None of the three keys resolve today and none has a primitive fallback authored yet** — see Appendix A.2 for the target fallback geometry and Appendix B for the registry rows. This is the single largest immersion gap in the chapter and should be the first commission alongside `Props.TierPlatformKit`.

**Note on Gryph's "watch the shoring" line lacking a prop referent.** `Dialogue_Beat1_Descent`'s Gryph line (script line 230) closes on "Drop the next two cages and watch the shoring" — shoring being the mine's timber/strut roof-supports, a specific canon noun distinct from the `DeadLiftCage` props the line's first clause already grounds. Neither the art table above nor Appendix A.2 places anything reading as shoring. The cheapest reconciliation is folding a couple of angled shoring-strut primitives into `Props.TierPlatformKit`'s Tier 1–3 dressing (where the rock is still "honest mine," per §Beat 1a) — cross-braced timber propping the rock at a tier's rear/side wall — giving the deep-shaft warrior's one grounding bark something to point at. Not placed here; noted alongside the ore-cart/lift-cage/guard-post reconciliation above as the next-cheapest addition.

**Low-priority immersion note — two cheap focal props the SETTING block implies but nothing anchors.** (a) `SyndicateGuardPost` (Tier 2) has no habitation read of its own — a small warm/flickering scavenged-lamp accent, distinct from the tier's cool `Tier2Light`/patrol light, would sell "squatters living in the bones of a dead industry" (repeated three times in the SETTING block) and visually distinguish the occupied tier from the empty ones. (b) The Tier-4 gang war (§Beat 1b/1d) is canon "rival salvage crews brawling over the ledger's scrap," but no scrap/loot prop exists for them to brawl over — a single junk-pile prop at the crews' midpoint (x≈-1, z≈77, where the player lands in the gap) would give the otherwise-abstract capsule brawl a diegetic reason and focal point. Both are optional, flagged as low-priority additions alongside the ore-cart/lift-cage/guard-post reconciliation above — not required for buildability.

**Note on the tier-gradient's technical debt.** `Ch10BuildTier`'s tint is computed once per tier at build time (`Color.Lerp(rustColor, vaultColor, Mathf.InverseLerp(0, 5, i))`) and baked into both the floor's `TintShared` MPB override and the matching `_PatrolLight`'s `BuildAccentPointLight` color argument — a prefab replacement for `Props.TierPlatformKit` **must** accept a per-instance tint override the same way, or the "the deeper it goes" read collapses to one fixed color across all six tiers, exactly the risk Ch9's rack-row note flags for `Props.SalvageRack`.

**Note on the gradient's legibility against fog.** §3's "the room ages under your feet across six tiers" read is asserted but depends on the player actually being able to *see* it. Exponential fog at density 0.015 (Appendix A.1) drops distant visibility to roughly 35% by 70 m and washes far tiers toward the dark-rust fog color `(0.06, 0.05, 0.05)`; the switchback alternating-X layout also breaks any straight-down sightline across more than one or two tiers. The reveal must therefore read across **adjacent** tier transitions — each ramp descent showing the next tier a shade cooler than the one just left — rather than as a whole-shaft panorama glimpsed from the top. This should be verified at the first fresh build (screenshot from each tier looking down-ramp at the next) before assuming the gradient communicates; if the fog swallows the color shift before the eye reads it, the chapter's central "environment tells the reveal" mechanism is invisible.

**Note on `SealedProgramDoor` blocking Ramp3 — full geometry in §1.2/§3.** The door's default `BoxCollider` (position (1,-2.7,67), scale (9,3.4,0.4), x∈[-3.5,5.5]) sits astride `Ramp3`'s x∈[-5,5] span at z=67, leaving only a ~1.5 m western sliver passable. This is the level's only realization of canon's handle-less-door-you-route-around beat, but as-built it reads as a hard squeeze on the ramp's open edge with no Phase-step yet available — a soft-block and fall-comfort risk that must be verified in-headset, not assumed safe because the door is "just set dressing."

**Note on the missing discrete rust/machined seam for Echo's "see the line of it" beat.** Echo's Strongroom line (script line 237) deictically points at a sharp boundary — "see the line of it, like a tide-mark in the stone" — distinct from `SealedProgramDoor` itself; canon frames the door as a separate, later image ("and that door, Cipher, no handle"). `Ch10BuildTier`'s per-tier `Color.Lerp` is a smooth blend with no discrete edge, and Tier 3's floor tint (t=0.4) is still mid-transition when the line plays, so there is no visible "tide-mark" for the player to look at. A decal strip or a tinted floor-edge band at the Tier 3 threshold — where the honest-mine tint meets Program-metal — would give this pointed line something to land on; currently the reveal's single most explicit "look here" beat is invisible. Flagged, not fixed.

**Note on the enemy-art assignment being index-parity, not type-aware.** `EnemyArtWirer`'s Ch10 table entry is `("Ch10_LedgerOfRust", "Coil_Syndicate_Ganger", "Ash-World_Scavenger")` — narratively correct as a *pair* (a syndicate ganger mesh and a scrapper/automaton-adjacent mesh) — but the wirer assigns primary/secondary by **each `Enemy` component's index in the scene's global `FindObjectsByType<Enemy>` order**, not by which `EnemyDefinition` it carries. In build order the three syndicate guards, two automata, Sever, and Vess are all `Enemy` components in the same scene; Sever and Vess already carry real/placeholder meshes so the wirer's idempotent "skip if it already has a SkinnedMeshRenderer" check spares them the visual swap (they still receive an added `NpcWalkAnimator`), but nothing guarantees a *syndicate guard* gets `Coil_Syndicate_Ganger` rather than `Ash-World_Scavenger` specifically — it depends on scene-object enumeration order, which is not asserted anywhere in this document or the builder. The gang-war brawlers are wired more reliably (by `FactionCombatant.FactionId`, not index), so their pairing is deterministic. Flagged, not fixed — mirrors Ch9's Coil-raider art-mismatch note in spirit.

#### d. Combat — two mook gates plus one ambient pocket

Both `DefeatEnemies` gates use the existing melee-AI/`BladeDamager` pipeline, no bespoke logic. Syndicate guards (`Ch10SyndicateGuard.asset`: maxHealth 50, damage 8, moveSpeed 1.5, attackCooldown 0.9) are the chapter's softest fight — roughly Ch9 Coil-raider-tier. Mine automata (`Ch10MineAutomaton.asset`: maxHealth 130, damage 15, moveSpeed 1, attackCooldown 1.1) are notably tankier and hit harder but move slower — reading mechanically as "the strongroom's immune system," per the production note, rather than a human skirmish. Gryph fights alongside the player in both encounters as an `AllyCombatant` (moveSpeed 1.4, attackRange 1.6, damagePerHit 8, attackInterval 1.4, per the shared component defaults — no per-chapter override in `Ch10PlaceAlly`). **This assumes he successfully descends the switchback to reach both fights — unconfirmed, no `NavMesh` in this project; see §Beat 1b's flagged verification gap and §8 item 10.**

**⚠ Flagged gap, carried from Ch9's identical finding — but the gap is tighter here than Ch9's, not looser.** `AllyCombatant.RetargetNearestEnemy` has no max engagement range. Gryph's *build-time* placement at (2,0,18) is 86 m from Sever's Archive fight (z=104) at spawn — but that spawn-to-boss distance is not the number that matters, because Gryph is a rangeless `AllyCombatant` who retargets the nearest live `Enemy`: during the mine-automaton `DefeatEnemies` step (step 7, automata at z≈86/92) he walks up to fight them, so by the time Sever activates at z=104 he is only **~12–18 m away**, through the Archive's fully open south side (no wall, no barrier — §2). This is *tighter* than Ch9's cited ~28 m Overlook-to-Vane spacing, not more comfortable — the "large gap gives ample time" framing does not hold once Gryph's post-automata position is accounted for. Still a real, unfixed structural gap in the shared component; not addressed here, but this strengthens the case for the manual check in §8 item 5.

**Tier-4 gang war** uses `FactionCombatant` (mirrors Ch7's pocket): each brawler carries 60 HP (vs Ch7's 45 — per the builder's own comment, "3v3 at point-blank the crews mutually wiped in ~4s during QA; the extra pool keeps the brawl alive long enough for the arriving player to see it"). This encounter has no bearing on mission gating and is non-participatory — the crews mutually wipe each other — but it is **not skippable**: `EnemyWaveSpawner`'s 9 m horizontal-distance trigger radius around (-1,-6,71) covers Tier 4's entire mandatory crossing from Ramp3 to Ramp4 (§Beat 1b), so every player traversing the chapter triggers and witnesses it. Correct read: unavoidably triggered and witnessed, non-mission-gating — not skippable.

#### e. Dialogue / VO

`Dialogue_Beat1_Descent` (`ch10_beat1_descent`), position (0, 1, 20), 2 lines, ≈37 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "We're in, Cipher. First tier. It's a mine, an honest one, or it was… Keep dropping. Gryph says it stays a mine for a while yet. I'll tell you the second it stops being one." | 16 |
| Gryph | "This is good rock, Cipher… Drop the next two cages and watch the shoring." | 21 |

`Dialogue_Beat1_Strongroom` (`ch10_beat1_strongroom`), position (1, -3.5, 62), 4 lines, ≈69 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Stop. There it is. The rust ends right here… We just crossed out of the mine and into the vault." | 20 |
| Coral Vex | "A handle-less door. I know those… Keep going. The deeper it gets, the closer you are to the count." | 20 |
| Sable | "You are close now. Three tiers, maybe less… They never leave a node like her unguarded. Get to her." | 22 |
| Ronin-7 | "Gryph, find me the way down past a door with no handle." | 7 |

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point, both mook fights included.
- No dedicated ambience layer covers the upper tiers (§3's flagged audio gap) — the fight/traversal audio here rests on `Haptics`/`AudioDirector` combat stingers alone, no persistent bed.
- `Tier4Light` carries `behaviour: AmbientPulse(period: 7s)` — the beat's one lighting event, sitting right at the tier the gang war is also live on, a coincidental doubling of "unstable/contested" texture that reads well even though it's not scripted as deliberate.
- `ArchiveLight0` carries `behaviour: ConsoleFlicker(seed: 110)` — technically a Beat 2 light (it sits at the Archive), but it is built in this same lighting pass; noted here so the full accent-light inventory reads in one place.
- Comfort vignette engages on every ramp transition and tier turn — the most snap-turn-dense traversal stretch in the chapter, six ramps in a row.
- **Comfort watch-point — open tier edges, not just ramp vection.** Six tiers of open 10×10 platform with an exposed edge beside every ramp mouth (§2) mean an *uncontrolled* downward fall is possible on top of the intentional, comfortable ramp descent — exactly the uncushioned-vertical-vection class the chapter's comfort notes otherwise guard against, and canon's own production note flags "tune fall-damage." Should be verified in-headset alongside the ramp-transition vignette tuning, not assumed safe because the ramps themselves are gentle.
- **Comfort gap — the `ZoneBounds` fall-recovery teleport is itself an uncushioned blink, the same discomfort class Phase-step's mandatory screen-fade (§1.1) exists to guard against.** `ZoneBounds.LateUpdate`'s recovery path toggles the `CharacterController` off, snaps `transform.position` to `lastSafePosition`, and toggles it back on — an instantaneous rig relocation with zero comfort treatment, firing on exactly the open tier edges the bullet above flags. Once `fallResetY` is fixed (§1.1, §2) so the fall-catch can actually do its intended job as a genuine off-edge recovery rather than a permanent lockout, that recovery snap should carry a fade/vignette pulse of its own — or at minimum be documented as an uncushioned blink — otherwise the chapter guards one instantaneous translation (Phase-step) while shipping a second, unguarded one (fall-catch) side by side. Proposed comfort layer, flagged, not built.

---

### Beat 2 — Cassie-04 and Her Keeper (Shaft Seven — Boss, Ally #7, Phase-step)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Rooms.NinefoldArchiveShell` and `Props.ArchiveRack` from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (Archive shell, rack row, Cassie-04's inert placement, Sever's inactive placement) and **`BuildBeat2Logic()`** (the confrontation dialogue, the `DefeatEnemies` boss step, the Phase-step `AbilityGranter` trigger, the recruit dialogue).
> **Sever's `SetActive(true)` is not a separate Trigger step** — `MissionDirector.BeginDefeatEnemies` activates his `Health` itself when the step starts. Do not add a redundant activation trigger.
> **Sever CANNOT be spared, talked down, or subdued.** The source script is explicit and load-bearing: there is no mercy branch for this fight, unlike Vess's later in the chapter.

#### a. Narrative purpose & emotional target

This is the chapter's spine, structurally identical to Ch9's Vane duel but tonally sharper: a boss with **no mercy branch**, deliberately built "fresh, not a copy" of Vane. Cassie-04 introduces herself first, dry and gallows-funny ("I'd hate to have to write you up"), immediately establishing her as Sable's tonal opposite — precision and irony where Sable is exhausted grief. Sever's flat refusal ("the asset stays on the rack… I have kept it through six") is followed by Cassie's own plea for the mercy of ending him, not winning against him, and Echo's read reframes him not as empty (like Vane) but as "almost present" — "there's somebody in there pressing against the leash… and it never gives." The fight's emotional peak lands mid-confrontation, before the blade is even drawn: Sever's line stutters — "The asset stays on the. The asset. Stays. I almost. No." — the chapter's single most quietly devastating beat, a man getting within one syllable of a different sentence and being hauled back to the order. The kill is staged explicitly as grief, not triumph ("I'm sorry it was me. Rest, brother."), and only *after* that beat has its breath does Echo name the freed gift: Phase-step, "the only soft thing he had to give." Echo's own voice-direction for the reveal frames it as "the third inheritance reach across the cold" (script line 306) — the class header's own term for Ch10 is "the third permanent ability unlock," and §Beat 2d's WeakpointSight (Ch7) / OverdriveController (Ch9) / PhaseStepController (Ch10) list is that saga pattern's mechanical face: three keeper-shadows, three gifts, each freed only after its keeper's death has been mourned, never taken as a trophy. Cassie's recruitment closes the beat on relief rather than dread — Sable's comm callback ("you can be more than the thing they made you… I came up. So can you") pays her own Ch9 line forward sister-to-sister for the first time in the saga.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Player:** arrives at the Archive via `ArchiveReachPoint` (0,-8,100), the beat's entry gate, carried over from Beat 1's step 8.
- **Cassie-04:** placed once at archiveCenter + (0,0,4) = (0,-9,112), `StoryNpc` only — **no `Health`, no combat component** (canon: she is racked in cabling, not a combatant). Built **active** from scene start, mirroring Ch9's Sable-at-the-Tide-Depths staging exactly (no "free her from the rack" animation system — greybox scope cut, consistent with that precedent). **Rotation must be Euler(0,180,0), facing -Z toward the Tier-6 approach — the same convention Sever uses two lines below.** `Ch10PlaceStoryNpc` (`Chapter10Builder.cs:597-611`) currently sets only position, never rotation, so Cassie keeps `InstantiateNpc`'s default +Z facing — the player, approaching from -Z, sees her back through the whole of Beat 2's four dialogue sets and Beat 3's two (including the 157 s reading of the names). This is a real build gap, not a documentation choice: unlike Sever (explicitly rotated to face his -Z approach), Cassie — who carries far more on-screen dialogue than any other NPC in the chapter — has no facing set at all. **Secondary issue:** even once rotated, Cassie's body at z=112 sits ~10–12 m from the `Dialogue_Beat2_Keeper` anchor at (0,-8,102) — far for the intimate "another one come to die on the rack-room floor" first-contact beat; consider whether her placement or the dialogue anchor should move closer for the confrontation set specifically.
- **Sever / Ninja-2 (the boss):** built at archiveCenter + (0,0,-4) = (0,-9,104) — precisely at Tier 6's z-position, the threshold where the descent's shaft-mouth opens into the Archive — rotated to face -Z (Euler(0,180,0), the approach direction from Tier 6). Carries a synthesized `CapsuleCollider` (center (0,1.1,0), height 2.4, radius 0.5), a `Health` (via `Ch10EnsureSeverDefinition`: maxHealth 300, damage 26, moveSpeed 1.7, attackCooldown 0.75 — the highest-HP, highest-damage, *and* fastest enemy definition in the chapter, reading mechanically as "Ninja make: quicker/evasive than Vane's heavier press" per the builder's own comment), and a synthesized `ArmR/Sword/Blade/BladeTip` chain the same way `Ch9BuildVane` does for placeholder-mesh bosses. Built `SetActive(false)` immediately after construction — he stays invisible through the entire Confrontation dialogue (step 9) and only pops into existence when the `DefeatEnemies` step (step 10) begins, the same reveal timing Ch8/Ch9 use for their bosses. **⚠ Flagged, not fixed — the confrontation's staging language assumes a visible keeper the build doesn't show yet.** The source script has Cassie gesture at "the armored gentleman between us… doesn't blink" during step 9, but there is no armored gentleman on screen at that point — the player hears a disembodied Sever voice from empty space until the reveal-on-fight-step convention pops him in at step 10. Consistent with the house "boss appears on the fight step" idiom used across the saga, but worth naming since Sever's dialogue explicitly deixis-points at him before he exists.
- **⚠ Flagged, not fixed — the fight-start reveal is a close-range pop-in, a distinct VR looming/comfort risk from the invisibility-during-dialogue gap above.** Sever activates at (0,-9,104) when step 10 begins; `Dialogue_Beat2_Keeper`'s anchor sits at (0,-8,102) and `ArchiveReachPoint` at (0,-8,100), both roughly 2 m away. A player who has drifted toward the dialogue anchor across the confrontation's ~109 s runtime (§Beat 2e) is standing close enough that a 300-HP armored boss materializing directly in front of them reads as a sudden face-to-face pop-in rather than an approach/reveal — personal-space/looming discomfort on top of the weak reveal itself. A cheap mitigation worth naming: move Sever's spawn a few meters north, e.g. (0,-9,107–108) — still canon "between her and the crew" (Cassie at z=112) — for a comfortable 5–6 m reveal distance from the z≈102 confrontation spot instead of the current ~2 m.
- **Phase-step `AbilityGranter`:** a `GameObject` ("PhaseStepGranter") carrying `AbilityGranter` (`abilityId = AbilityId.PhaseStep`), built `SetActive(false)`, activated by a dedicated `Trigger` step (step 12) immediately after Sever's death and the aftermath dialogue — **not** collapsed into the kill itself.
- **⚠ Invariant — step 11 must precede step 12, same protection class as Ch9's Overdrive-ordering invariant.** The class-header/CUTSCENE staging is explicit: "Hold this beat before the shadow rises" (paraphrased from the dialogue script's own stage direction pattern, matching Ch9's word-for-word). Step 11 (`Dialogue: Rest, Brother` — the aftermath grief beat) strictly precedes step 12 (the Phase-step grant). A future edit optimizing "grant Phase-step on kill" for gameplay snappiness would silently destroy the same emotional beat Ch9's doc protects for Overdrive. Do not reorder without a story-side sign-off.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 9 | Dialogue | `Dialogue_Beat2_Keeper` (`ch10_beat2_keeper`) — Cassie's introduction, Sever's refusal, Cassie's plea, Echo's read, Ronin-7's answer, Sever's stutter (Sever still invisible) |
| 10 | DefeatEnemies | Sever/Ninja-2 — activates (`SetActive(true)`) and waits for his single `Health` to reach zero — the boss duel |
| 11 | Dialogue | `Dialogue_Beat2_Kill` (`ch10_beat2_kill`) — "Rest, brother," then Echo feeling the shadow reach for it |
| 12 | Trigger | Activates the Phase-step `AbilityGranter` — unlocks the ability immediately via `OnEnable` |
| 13 | Dialogue | `Dialogue_Beat2_PhaseStep` (`ch10_beat2_phasestep`) — Echo names the gift |
| 14 | Dialogue | `Dialogue_Beat2_Recruit` (`ch10_beat2_recruit`) — Cassie's reaction, Sable's comm callback, Cassie's acceptance — Ally #7 |

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Archive shell (20×24, W/E/N walls + floor + ceiling, open S) | center (0,-9,108) | `Rooms.NinefoldArchiveShell` | `…/Art/Generated/Rooms/NinefoldArchiveShell.prefab` | **MISSING** |
| `ArchiveRack` ×8 (4 z-positions × 2 sides, x=∓8, z=100/104/112/116) | tint teal (0.15,0.55,0.6), fixed (not gradient-lerped, unlike the tier floors) | `Props.ArchiveRack` | `…/Art/Generated/Props/ArchiveRack.prefab` | **MISSING** |
| `ArchiveLight0` (accent) | (-4, -6.4, 104) | — | `ChapterEnvironmentProfile.accentLights["Archive0"]` | profile |
| `ArchiveLight1` (accent) | (4, -6.4, 112) | — | `ChapterEnvironmentProfile.accentLights["Archive1"]` | profile |
| `ArchiveAmbience` (audio) | (-4, -6.4, 104), inner 5 / outer 18, max vol 0.4 | — | `BuildAmbienceLayer` (audio, not a registry prop) | audio |
| Cassie-04 | (0, -9, 112), **Euler(0,180,0) target — currently unset, see note below** | `Named.Cassie04` | `…/Art/Generated/Characters3D/Named/Cassie-04.prefab` | **EXISTS** |
| `CassieCablingRack` (central cabling armature, frames her torso) | (0, -9, 112) — same point as Cassie-04, not the 8 m-off wall racks | `Props.CassieCablingRack` | `…/Art/Generated/Props/CassieCablingRack.prefab` | **MISSING — no primitive fallback authored yet** |
| Sever / Ninja-2 (boss) | (0, -9, 104), Euler(0,180,0) | `Named.SeverNinja2` | `…/Art/Generated/Characters3D/Named/Sever_Ninja-2.prefab` | **EXISTS** |

**Note on the rack row's fixed tint vs. the tier floors' gradient.** Unlike the six tier platforms (§Beat 1c), `Ch10BuildArchiveRackRow` uses one flat teal tint across all eight racks — no lerp, no per-instance variation. This is a deliberate contrast, not an oversight: the Archive is a single, unified strongroom rather than a transition zone, so a uniform "catalogued and orderly" palette reads correctly against the six-tier gradient's "aging" story. A prefab replacement should preserve the flat tint, not import the tier gradient's per-instance override machinery.

**Note on the racks' inertness during the duel — confirmed correct, not a gap.** The boss production note is emphatic that the eight `ArchiveRack` props are "fragile cataloged shadows that should NOT be destroyed casually — the Archive is the thing they came to save," and the Phase-step unlock note reuses the same language for crossing "the cataloged racks without destroying them." `Ch10BuildArchiveRackRow` builds each rack via the shared `BuildProp` helper (`Chapter10Builder.cs:721-722`) — a tinted primitive cube carrying only its default `BoxCollider`, no `Health`/`IDamageable` component of any kind. The racks are therefore already, correctly, inert non-damageable cover: `BladeDamager` has nothing to hit on them, so nothing in the Sever duel (§Beat 2d) lets a player smash the ledger they came to rescue, even accidentally. This must hold through the art migration too — a `Props.ArchiveRack` replacement prefab must not add a `Health` component, or this correct-by-omission behavior silently regresses into destructible geometry.

**Note on Tier 6 and the Archive's geometric overlap.** Tier 6's 10×10 platform (center (0,-9,104)) sits *inside* the Archive's own 20×24 floor extent (center (0,-9,108), which spans z[96,120]) — both floor primitives occupy the same y=-9 plane across roughly z[99,109]. This is not flagged as an error anywhere in the builder's own comments and appears harmless (the overlap is invisible — both floors share the same tint-adjacent Y and the player never sees a seam), but it is a genuine double-build worth noting for anyone converting these to prefabs: a `Rooms.NinefoldArchiveShell` prefab that includes its own floor at that full 20×24 extent will double-render against `Props.TierPlatformKit`'s Tier 6 instance unless one of the two is suppressed at the join.

**Note on Cassie-04's missing facing rotation.** See §Beat 2b — `Ch10PlaceStoryNpc` sets position only, so Cassie retains `InstantiateNpc`'s default +Z facing rather than the Euler(0,180,0) she needs to face the player's -Z approach. She delivers the chapter's largest NPC dialogue load (four sets in Beat 2, two in Beat 3) with her back to the player until this is fixed.

**Note on Cassie-04's missing cabling-rack prop.** As-built, Cassie stands alone at the Archive's center — the eight `ArchiveRack` props sit 8 m off at the walls (x=∓8), and nothing stands around her body. Canon stages her four times as physically racked *at the center*: "hangs in a rack of cabling at its heart" (SETTING line 40), "racked in cabling" (CASSIE-04 STAGING), and her own first line — "another one come to die on the **rack-room floor**." The CASSIE-04 STAGING note correctly cuts the "free her from the rack" *animation*, but a static central cabling-armature prop is set dressing, not animation, and is exactly the kind of cheap immersion the ore-cart/lift-cage/guard-post reconciliation already added for the upper tiers (§Beat 1c). `Props.CassieCablingRack` (art table above, Appendix A.3, Appendix B) targets (0,-9,112), coincident with Cassie herself, with a primitive fallback — a cluster of thin vertical cabling cubes framing her torso — pending a dedicated prefab. **Colliders must be stripped/disabled on the fallback cubes, the same discipline the `ArchiveRack`-inertness note above applies to the rack row.** Via raw primitives the cabling cluster would carry default `BoxCollider`s standing exactly where the player must approach Cassie for six dialogue sets (§Beat 2e, §Beat 3e) and where Sever's duel (§Beat 2d) ranges around the Archive center; left solid, the cluster blocks the player's approach to Cassie and snags pathing at the room's heart — pure set-dressing, not a physical obstacle. This is the single strongest unbuilt visual read for the character carrying the chapter's heaviest dialogue load; it pairs with the under-skin-glow gap immediately below — cabling and glow together are what sell "living node."

**Note on Cassie-04's under-skin data-light signature.** Canon describes her identically to Sable — "data-light pulsing under her skin like Sable's" — and stages the kill's aftermath with "the cataloged shadows around them dimming a fraction as the index reroutes through her," making the glow the visual mechanism of the sister-to-sister pairing the whole beat turns on. Neither `Cassie-04.prefab` nor `Sable.prefab` shows any emission-material reference on disk (a text search for "emiss" returns zero hits on both, and neither character folder carries a dedicated material asset), and Ch9's own doc never confirms Sable's own skin-glow either — its §3 covers only the Construction Core's environmental cyan accents (`CoreLight0/1`), not a character shader effect. This reads as an unaddressed cross-chapter character-VFX gap shared by both living-archive nodes, not a Ch10-specific oversight. Flagged, not fixed: for a node character carrying the chapter's heaviest dialogue load, the archival glow is her single strongest silent read.

**Note on Sever's "almost-present" visual tell having no on-screen anchor.** The production note is emphatic and repeated: "eyes flat with conditioning," "a flicker of something underneath the order, a man almost reaching the door before the leash hauls him back," "Tragedy in armor, never a sneer." As-built, Sever is a Named mesh carrying only the synthesized `CapsuleCollider`/`ArmR-Sword-Blade-BladeTip` combat rig (Appendix A.3) — no head-turn-toward-the-door hesitation, no eye/emission tell, no idle break timed to the stutter line. §Beat 2d already flags the stutter itself as pure VO with no mechanical stagger-window; this is that gap's companion on the *visual* side — the "almost present, unlike the empty Vane" distinction the entire Sever/Vane contrast rests on has no silent read to carry it, so the boss reads as an ordinary flat-conditioned mook. Belongs in the same character-VFX cluster as the Cassie-04 cabling/glow gaps above: flag, don't fix.

**Note on Sever's approach-facing rotation matching the geometry.** Sever's Euler(0,180,0) faces -Z (south, toward Tier 6/the shaft-mouth) — exactly the direction the player physically arrives from after Beat 1's descent, so the "posted to guard the node" staging lands correctly without any additional blocking work.

**Note on the descent reveal itself — the player's first sight of the Archive shows Cassie unguarded, distinct from the mid-dialogue deixis gap above.** Because Sever is built `SetActive(false)` until step 10 (§Beat 2b), the player's first view into the Archive — descending `Ramp5` onto Tier 6 and looking north across the z=104 threshold toward Cassie at z=112 — is the cold cyan strongroom with Cassie alone in her rack and no armored figure between them. Canon's establishing image is explicit: "a single armored figure standing between her and the world with a blade already drawn." This is the descent-reveal beat itself, not the confrontation dialogue's deixis problem already flagged above — the whole "keeper posted between you and the node" first impression never actually lands as first sight, because Sever isn't there to be seen. Compounds with the fight-start pop-in gap now noted in §Beat 2b: the boss is invisible for both the descent-reveal and the confrontation dialogue, then appears abruptly at close range only when the fight begins. Flagged, not fixed.

**Note on Sever's blade persisting after the kill — confirmed correct, not a gap.** Canon repeats three times that the ability, not the weapon, is the inheritance — "the blade itself is left among the racks," "the freed shadow is not a second blade Ronin-7 keeps… Sever's blade is left among the racks." Sever's death routes through the same shared `MeleeAttacker`/`Enemy` FSM every enemy in the saga uses; `Health`'s death event never destroys or deactivates the GameObject, and `MeleeAttacker.OnDeathPose`'s lethal branch simply topples it in place (`transform.rotation = Euler(85, y, 0)`) and tints it gray. Because Sever's synthesized `ArmR/Sword/Blade/BladeTip` chain (Appendix A.3) is a child of that same GameObject, it topples and remains with him rather than being destroyed or hidden — so the build already, correctly, leaves an inert blade-and-body prop among the racks after the fight, the physical anchor canon calls for. No additional authoring needed; noted here because nothing in the doc previously confirmed the corpse — and its blade — persists rather than being cleaned up.

#### d. Combat — the Sever/Ninja-2 duel

Sever fights as a full `Enemy` using the existing melee-AI/`BladeDamager` systems, no bespoke boss logic — the same "reuse, don't reinvent" approach Ch9's Vane duel takes. His `EnemyDefinition` (maxHealth 300, damage 26, moveSpeed 1.7) is the chapter's hardest fight by every stat, roughly 6× a syndicate guard's HP and notably faster than either mook type — the mechanical expression of "the Ninja make: quicker/evasive than Vane's heavier press" the production note calls for. Per the production note's phase suggestion (design intent, not asserted as implemented mechanics): (1) a straight duel against the older, evasive school; (2) Sever weaponizing the racks and vanish/re-entry patterns, `WeakpointSight` cueing openings; (3) a final committed exchange with longer, more anguished "stutter" windows. `WeakpointSight` (Ch7) and `OverdriveController` (Ch9, self-gating, live for the rest of the game from unlock) are both usable in this fight; `PhaseStepController` is present on the rig from scene start but **not** usable until this very fight's aftermath grants it — the player cannot Phase-step against Sever himself, only after, the same "granted after, not during" structure Ch9 uses for Overdrive against Vane.

**No mercy branch exists in the build**, matching the canon constraint exactly. `AuthorDefeatStep` simply waits for Sever's single `Health` to hit zero.

**Note on the stutter beats being pure VO, not a mechanical stagger-window.** The production note's phase-suggestion calls for the man-behind-the-leash "stutters" to be *mechanical* stagger-windows the player can read as openings ("these stutter-windows are the player's openings and they should feel like mercy and horror at once, not just a parry-gap"). The as-built fight is a plain `Enemy`/`BladeDamager` encounter with no bespoke stagger-state machine — Sever's stutter ("The asset stays on the. The asset. Stays. I almost. No.") is delivered entirely through the pre-fight confrontation dialogue (step 9, while he is still invisible/inactive), not as an in-combat mechanical beat. This is a real narrative-vs-mechanic gap, the same class of finding as Ch1's rifle-vs-blade trooper mismatch and Ch9's frozen-projectile-for-Overdrive gap: flagged, not fixed — building a genuine mid-fight stagger-window system is new combat-mechanic surface, out of scope for an art/registry pass.

#### e. Dialogue / VO

Both sets advance on the **Left-Hand "Talk" (Y) input**, one line at a time, via `PromptInputAdvancer`/`DialoguePlayer`.

`Dialogue_Beat2_Keeper` (`ch10_beat2_keeper`), position (0, -8, 102), 6 lines, ≈109 s:

| Speaker | Line | sec |
|---|---|---|
| Cassie-04 | "Oh, good. Another one come to die on the rack-room floor… I'd hate to have to write you up." | 24 |
| Sever | "The asset stays on the rack. That is the order. I have kept it through six. I will keep it through you." | 8 |
| Cassie-04 | "Listen to him. Six words, every time… Trust the clerk on this one. I've read his whole file." | 24 |
| Echo | "She's right, Cipher. I can feel him from here, same as I felt Vane, except this one's worse to feel… Run the read with me." | 22 |
| Ronin-7 | "Ninja-2. You came two makes before me… I'm sorry, brother. Rest is the only door I've got." | 22 |
| Sever | "The asset stays on the. The asset. Stays. I almost. No. The asset stays on the rack. That is the order." | 9 |

`Dialogue_Beat2_Kill` (`ch10_beat2_kill`), position (0, -8, 104), 2 lines, ≈29 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "There it is. The first time your face has been quiet… Rest, brother. You finally get to." | 13 |
| Echo | "His shadow's free, Cipher… He left you a step through walls." | 16 |

`Dialogue_Beat2_PhaseStep` (`ch10_beat2_phasestep`), position (0, -8, 106), 1 line, ≈21 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Feel that. You just stepped through a wall like it owed you the right of way… Use it gently. It's the only soft thing he had to give." | 21 |

`Dialogue_Beat2_Recruit` (`ch10_beat2_recruit`), position (0, -8, 110), 3 lines, ≈70 s:

| Speaker | Line | sec |
|---|---|---|
| Cassie-04 | "Well. Eleven thousand and one… I did not have a column for that." | 22 |
| Sable | "Cassie. It's Sable… Let him pull the wires, Cassie. Come up with us." | 24 |
| Cassie-04 | "More than the thing they made me… let me show you what I've been counting." | 24 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point**, including the Phase-step activation — the blink effect is sold by the world briefly smearing dark/cold plus `AudioDirector` cues and haptics, never a camera trick, matching the same rule Ch9 states for Overdrive. **The dark/cold smear must double as a comfort screen-fade across the instantaneous positional translation** (§1.1) — Phase-step relocates the player's rig through solid matter with zero frames of travel, exactly the discomfort class the chapter's own "no teleport locomotion" stance otherwise bans; the fade is what distinguishes this ability-blink from a bare positional teleport. This is a required spec on `Vfx.PhaseStepBlink` (Appendix B), not an optional flourish.
- `ArchiveLight0` carries `behaviour: ConsoleFlicker(seed: 110)` — the Archive's one lighting event, an "old machine still humming" register (matching the SETTING block's "dry, cold, humming order") rather than a failing-power flicker.
- **Missing dynamic cue — the rack/light dimming as the index reroutes through Cassie.** Canon (dialogue-script line 334) explicitly stages "the cataloged shadows around them dimming a fraction as the index reroutes through her instead of the strongroom" at the recruit moment (step 14, `Dialogue_Beat2_Recruit`/Cassie's acceptance). This is cheap and high-read: dip `ArchiveLight0/1`'s intensity and/or the eight `ArchiveRack` teal tint one notch on that step to visually sell the ledger transferring from the vault into Cassie — the whole point of Ally #7. Pairs with the already-flagged under-skin-glow gap (§Beat 2c) so both character and environment carry the reroute together. Not built today.
- `ArchiveAmbience` (inner 5 / outer 18, max vol 0.4) provides the "a machine that has been counting the dead for a very long time" bed under the whole beat.
- Haptics carry every blade hit in the Sever duel per the existing `BladeDamager`/`Haptics` pipeline — no new haptic authoring needed.
- **Missing cue — the shadow-inheritance moment (steps 11→12) and the first Phase-step blink each want their own haptic/audio cue, not just a visual.** The bullet above covers blade-hit feedback only; the inheritance is a distinct event, and canon stages it explicitly as "cold and sudden" (screenplay line 67, `Vfx.ShadowInheritance`, §9/Appendix B), while the first blink is staged as "the world smearing dark and cold." A cold/pulse haptic plus an `AudioDirector` sting at the inheritance beat, and the same pairing on the first `Vfx.PhaseStepBlink` alongside its mandatory comfort screen-fade (§1.1), would carry the "cold and sudden" the VO keeps naming rather than leaving those two beats silent. Not built today.
- `ReverbZonePlacer.AutoTagInteriorVolumes()` + `PlaceReverbZonesForInteriorVolumes()` run at the end of the build, tagging the Archive as a distinct, larger/drier-reading interior volume than the open tier platforms — reinforcing the "dry, cold, humming order" contrast against the mine above.

---

### Beat 3 — Reading the First Names (Shaft Seven, After the Kill)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> **No new art for this beat** — it reuses Beat 2's Archive geometry unmodified. Create **`BuildBeat3Logic()`** only (both dialogue players and their mission steps). This beat is pure dialogue; do not add combat, reach gates, or new props here.

#### a. Narrative purpose & emotional target

Beat 3 is the chapter's data-payload beat and the direct sibling of Ch9's Concord Engine reveal — the two facts land here once, never re-disclosed elsewhere: the Ninja program's own erasure (an entire generation struck off the books for being "a weapon you can't be sure you still own"), and the birth of the LEDGER hub system, "a searchable list of the taken" that foreshadows Soren in Ch16. Ronin-7 asks for the Ninja program's history *before* the names, framing the reading that follows as an act of accountability rather than curiosity. Cassie's reading itself is deliberately restrained — the production note calls for "the WEIGHT of names on a man who was the instrument of exactly this disposal," not a data-wall spectacle. The beat's single most load-bearing continuity beat is Ronin-7's own file: sealed from the inside, unreadable even by Cassie's lattice — the explicit, quietly-planted Ch16 hook ("Somewhere there's a room deeper than this one where that lock opens. I'll find it. Not today."). Cassie's refusal to be carried up "like the last of the furniture" closes the beat on her walking out on her own feet, ledger live in her spine — Ally #7 stated on her own terms.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **No new NPCs, no new reach points.** Cassie-04 and the (now-resolved) Sever remain exactly where Beat 2 left them; the player is still inside the Archive.
- **Player:** free-standing inside the Archive for the whole beat; no positional gate exists between Beat 2's recruit dialogue and Beat 3's two dialogue sets — both play back-to-back, purely player-paced on the Talk input.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 15 | Dialogue | `Dialogue_Beat3_NinjaHistory` (`ch10_beat3_ninja_history`) — Ronin-7 asks; Cassie explains the erased Ninja program |
| 16 | Dialogue | `Dialogue_Beat3_Reading` (`ch10_beat3_reading`) — the ledger opens; the first names read aloud; the sealed-file beat; Cassie claims her place on the crew |

**What changes during the beat:** nothing in the set dressing — no props are added, no lighting event fires. The "vault door swinging on the whole war" the production note describes is realized entirely through dialogue and VO, with no accompanying data-light or index-reveal VFX built (§9).

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

**No new art.** The Archive — its shell, rack row, both accent lights, `ArchiveAmbience` — is entirely Beat 2's geometry, reused unmodified, the same "beat that reuses, doesn't rebuild" idiom Ch1's Beat 3→Beat 1 kill-box reuse and Ch9's Beat 5 overlook reuse both use.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(none — see above)* | — | — | — | n/a |

**Candor note — the ledger-reveal has no dedicated visual.** The production note explicitly calls for "the ledger opens here as a diegetic data-event, Cassie surfacing the index across the Archive as readable light, names and disposal-sites and shadow-files scrolling into legibility for the first time." No such VFX exists in the build — this is the chapter's equivalent of Ch9's missing Concord Engine data-light gap, and should be weighed alongside it when prioritizing the art backlog (§9, Appendix B).

#### d. Combat

None. Sever is already resolved; no new enemies spawn.

#### e. Dialogue / VO

`Dialogue_Beat3_NinjaHistory` (`ch10_beat3_ninja_history`), position (0, -8, 112), 2 lines, ≈43 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "Before the names. The one I just put down… Tell me what the Ninja program was. I want to know what I killed, and what you're carrying." | 11 |
| Cassie-04 | "The Ninja program. Two builds before your Ronin line, Cipher, the Dominion's first real attempt at a blade nobody could trace… You just gave the last of them the only rest the Dominion ever left on the shelf." | 32 |

`Dialogue_Beat3_Reading` (`ch10_beat3_reading`), position (0, -8, 114), 9 lines, ≈157 s — the longest single set in the chapter:

| Speaker | Line | sec |
|---|---|---|
| Cassie-04 | "All right. Watch the light. This is the part they buried a mine on top of so it could never be read aloud… So. Where do you want to start." | 26 |
| Ronin-7 | "From the start. The oldest first. Read them like they mattered, because somebody on my ship made me promise they would… Were they someone like me." | 12 |
| Cassie-04 | "The oldest first. Then you'll want to sit, metaphorically, because the old ones are the worst… because somebody finally should." | 26 |
| Echo | "I'm reading with you, Cipher. I can't not… This is the heaviest thing we'll ever carry and it's also the only map that gets us to them." | 19 |
| Ronin-7 | "Find me one more. Find me mine… I want to know if I had one too, before they made me a number." | 11 |
| Cassie-04 | "I was waiting for you to ask… Whatever they did to you, they didn't want it read even by their own clerk." | 25 |
| Ronin-7 | "Sealed from the inside. Of course it is… Not today. Today we read the others." | 14 |
| Cassie-04 | "Then I'm not staying down here to wait for you to carry me up like the last of the furniture… I've got a galaxy of names to say out loud and I'd like to start before the next century." | 18 |
| Ronin-7 | "Then you're with us. Stay on Gryph's shoulder and keep reading. We climb." | 6 |

**Note on "stay on Gryph's shoulder."** Ronin-7's closing line stages Cassie physically walking out at Gryph's shoulder, but per §5 no `NpcWalker` exists for either Cassie or Gryph — both remain static transforms after this point (Cassie's Beat 5 appearance simply reuses her build-time-adjacent placement conceptually; the actual ship-entrance staging is a fresh, separately-authored dialogue anchor, not a physical walk). Flagged as narrative/build divergence, not fixed.

#### f. Audio / Haptics / VR Comfort

- No camera shake — this is the chapter's one long stretch of pure dialogue with no combat feedback to drive, matching Ch1's Command Room / Ch9's Concord Engine reveal precedent.
- `ArchiveAmbience` and `ArchiveLight0`'s `ConsoleFlicker` continue unchanged from Beat 2 — no new audio/lighting authored for this beat.
- **The broken-seal alarm-tone begins here, the chapter's one dynamic non-combat audio event.** As Cassie stands clear of the rack and the crew turns to leave (the cutscene direction closing `ch10_beat3_reading`, step 16), canon has "a low alarm-tone thread up through the strongroom, its broken seals registering the intrusion behind them" — see §7's `ReturnClimbAlarmTone` entry. It should be armed (audible) at this beat's close rather than from build time, so it reads as the vault reacting to Cassie's freedom rather than a baseline drone running under the whole Archive.
- No haptics scripted.
- Comfort vignette is inert — the player is free to stand and turn but has no required traversal.

---

### Beat 4 — Vess's Vengeance (The Ship Entrance — Boss, DuelYield, Ally #8)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Props.SalvageRig` from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (ship-entrance ground, `SalvageRig`) and **`BuildBeat4Logic()`** (the climb-out ramp, the reach point, Vess's inactive `DuelYield` spawn, three dialogue sets, the yield/accept mechanic itself).
> **The mercy/spare choice is mechanically live, not scripted**, but inverted from every other `DuelYield` fight in the saga: canon frames **Vess** as the one extending mercy (she lowers a blade already at Cipher's throat), while the underlying mechanic is the same low-health-yield-then-accept FSM every other `DuelYield` boss uses (`yieldThreshold = 0.2`, sword-sheathe or `autoAcceptSeconds` accepts). Do not hardcode a forced-outcome cutscene when patching this beat — the accept is still a player action (sheathe the sword), even though the narrative framing has reversed who is "granting" the mercy.

#### a. Narrative purpose & emotional target

Beat 4 is the chapter's thematic capstone and its one true inversion: every prior keeper-kill in the saga (Vane, and now Sever) offered its target only a clean death as mercy; Vess is "the first one that chose to spare you back," per Echo's closing line. The staging deliberately reconciles a scripted ambush ("blade to his neck before he can raise the katana") with a player-fought duel by having Ronin-7 offer a flat, unbargained trade first — "kill me, it's fair" — then lay the true cost of taking his life on the table (the names die with him). Vess's flint cracks exactly once ("don't you tell me you're the champion of the galaxy… I am not buying it") before Ronin-7's answer lands and she reverses the blade hilt-first. Her mercy is explicitly **not forgiveness** — "that's worse than killing you… you don't get to die until every last one has been spoken out loud to the whole galaxy" — a sentence, not a pardon, and the dialogue is careful to keep her hard and unsoftened even in alliance. Gryph's one line places the crew's earlier choice (sparing Kerrax-adjacent syndicate figures, the whole Act III "protector, not predator" arc) directly beside hers: "I know that choice when I see it twice in one day."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **`ReturnRamp`:** the single long ramp abstracting the climb back up all six tiers (§2) — built in `BuildBeat4Art()` conceptually, though the builder constructs it in the same pass as the descent geometry; documented here because it is this beat's traversal, not Beat 1's. The source script's "the crew starts the long climb back up the nine tiers" is compressed into one traversal segment plus a reach gate, the same abstraction Ch9's "climb out" reach point uses for its own return trip.
- **⚠ P0 (independent of `ZoneBounds`, see §1.1/§2) — `Archive_WallN` physically seals `ReturnRamp`'s low end.** Both sit at z=120: the ramp's y=-9 origin falls inside the wall's y∈[-9,-5.4] span, with no aperture. As built, this whole beat — the climb-out, Vess, the target list, the ending — is unreachable behind the wall, even once `fallResetY` is fixed. Must be confirmed in-editor (§8 item 9), not assumed fixed by patching `ZoneBounds` alone.
- **`ShipEntranceReachPoint`** (0,1,170), radius 6 m — gates Vess's ambush dialogue on the player physically reaching the ship entrance.
- **Vess (the boss):** built at shipEntranceCenter + (0,0,4) = (0,0,174), rotated Euler(0,180,0) (facing -Z, the approach direction — the player arrives from the south, up the return ramp). Carries the same synthesized `CapsuleCollider`/`ArmR-Sword-Blade-BladeTip` chain as Sever, wired to `Ch10EnsureVessDefinition` (maxHealth 240, damage 20, moveSpeed 1.5, attackCooldown 0.85). A `StoryNpc` (displayName "Vess") and a `DuelYield` component are added: `opponent` = her own `Health`, `yieldThreshold = 0.2` (yields at 20% health, not death), `disableOnYield` = `[vess]` (the `Enemy` behavior itself disables on yield), `sword` = the player's `Grabbable` sword (yield is accepted once the player sheathes it), `autoAcceptSeconds = 30` (a 30 s safety-net auto-accept if the player never re-sheathes). Built `SetActive(false)` — revealed only by the Trigger step, mirroring `Ch4BuildKerrax`'s confront-then-reveal timing exactly.
- **Mercy mechanic is mechanically identical to Ch4's Kerrax fight** (§1.2). The production note's three-phase "ambush / railed duel / scripted mercy" staging is honored at the **narrative layer only** — the dialogue frames Vess as extending the mercy, while the underlying FSM is the same reused component. Building a bespoke "player cannot die, opponent cannot be executed, force-resolve at a clash-lock" rail (as the source's production note literally describes) would duplicate `DuelYield`'s own job with a second FSM for one fight — the class-header comment calls this "simplest mechanization, matching the project's precedent."
- **The duel's acceptance advances the mission spine directly:** `UnityEventTools.AddPersistentListener(duelYield.onAccepted, missionDirector.AdvanceFromPrompt)` — the null-dialogue `Prompt` step (step 20) blocks until the player sheathes the sword after Vess yields.
- **⚠ Flagged gap — the cold-open ambush is unstaged, and the persuasion beat plays after the duel rather than before it.** The source script's "blade to his neck before he can raise the katana" is a scripted-ambush image with no mechanical or blocking counterpart: Vess is built inactive at (0,0,174) and reveals a full 4 m in front of the player at (0,1,170) when the Trigger step fires (step 19) — there is no "blade already at the throat" moment, only a reveal at range immediately followed by a standard duel. More load-bearing: the script places Vess's flint-crack ("don't you tell me you're the champion of the galaxy") and Ronin-7's answering surrender line *before any fighting* — it is what makes her lower the blade in canon — but in the build these lines sit in `Dialogue_Beat4_Choice` (step 21), which plays **after** the entire mechanical duel (steps 19–20) resolves via the yield/sheathe FSM. The reveal timing inverts the scripted emotional order for the chapter's thematic capstone. Flagged, not fixed — restaging this correctly (an unloseable pre-duel exchange, then a fight, or folding the duel itself into the persuasion) is new mission-spine surface, out of scope for an art/registry pass. **Forward-looking VR-comfort caveat for whoever eventually stages it:** the canonical image — a blade snapped to the player's own throat, an NPC inside arm's reach unbidden — is exactly the looming/personal-space discomfort class the chapter otherwise guards against (§1.1, §Beat 1f's comfort watch-points). Any future restaging of "blade to his neck" should keep Vess's blade and body outside the player's immediate personal space (a held-at-range block, not a face-cam knife) rather than a literal close-quarters weapon-at-throat moment.
- **⚠ Flagged gap — the hilt-first blade-offer, canon's literal mercy-mirror image, is unstaged.** Canon's thematic climax is a specific physical inversion, not just a change of heart: Vess "reverses her grip and offers it, hilt-first" (screenplay line 88; dialogue-script line 433's stage direction). As-built, `DuelYield` resolves the fight to its generic disable-and-yield pose — no blade-reversal or hilt-offer animation of any kind — so the chapter's defining "she spares *you*" image has no on-screen anchor, only the VO that names it after the fact ("Take the blade," `Dialogue_Beat4_Choice`). This is the same character-VFX cluster as the Cassie-04 under-skin-glow gap and Sever's "almost-present" tell (both §Beat 2c) — the one entry in that cluster that lands squarely on the chapter's own title theme. The bullet above already flags the ambush's *timing*; this flags the climactic gesture itself. Flag, don't fix — a bespoke yield-pose animation is new character-rig surface, out of scope for an art/registry pass.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 17 | ReachTrigger | Gates on `ShipEntranceReachPoint` (0,1,170), radius 6 — the beat's entry, the payoff of the climb-out |
| 18 | Dialogue | `Dialogue_Beat4_Ambush` (`ch10_beat4_ambush`) — Vess's ambush monologue, Ronin-7's flat acceptance and counter-offer (Vess still invisible/inactive) |
| 19 | Trigger | Activates `vessGo` — the boss fight begins |
| 20 | Prompt | Blocks on `DuelYield.onAccepted` (sheathe after yield), not a `PromptInputAdvancer` — same idiom as Ch4's Kerrax step 18 |
| 21 | Dialogue | `Dialogue_Beat4_Choice` (`ch10_beat4_choice`) — the mercy-mirror exchange, Vess's sentence, Ally #8, Gryph's approval, Echo's closing read |

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Ship Entrance ground (16×24, no walls/ceiling) | center (0,0,170) | `Rooms.ShipEntranceGround` | `…/Art/Generated/Rooms/ShipEntranceGround.prefab` | **MISSING** |
| `SalvageRig` | shipEntranceCenter + (3,1.4,8) = (3,1.4,178) | `Props.SalvageRig` | `…/Art/Generated/Props/SalvageRig.prefab` | **MISSING** |
| `ShipEntranceLight0` (accent) | (-4, 2.4, 145) | — | `ChapterEnvironmentProfile.accentLights["ShipEntrance0"]` | profile |
| `ShipEntranceLight1` (accent) | (4, 2.4, 155) | — | `ChapterEnvironmentProfile.accentLights["ShipEntrance1"]` | profile |
| Vess (boss) | (0, 0, 174), Euler(0,180,0) | `Named.Vess` | `…/Art/Generated/Characters3D/Named/Vess.prefab` | **EXISTS**, placeholder-quality, see note |

**Note on `SalvageRig` lacking a boarding-ramp/ship-silhouette read.** Canon frames the ship as the visible goal of the whole climb-out, repeatedly — "the boarding ramp in sight" (line 405), "the rig's boarding ramp" (line 455), "turns from the dead mine toward the rig's boarding ramp" (line 495). As-built, `Props.SalvageRig` is a single 3×2.8×4 tinted block (Appendix A.4) alongside the flat ground, warm `ShipEntranceLight0/1`, and the inactive completion canvas — nothing reads as "the way off this dead world," only brighter light. The chapter's whole arc is "down into the dark, then up and out," and the exit destination has no object to walk toward. A `Props.SalvageRig` replacement should read as a boardable rig with a visible ramp/ship mass oriented toward +Z, giving the climb-out a physical goal, not just improving light. Cheap, high-value against the "gutted-sky escape" beat §3 already discusses. Flagged, not fixed.

**Note on Vess's placeholder mesh.** `Ch10VessPrefab` resolves to `PlaceholderCharacterFolder + "/Vess.prefab"` — `PlaceholderCharacterFolder` is `Assets/Ronin7/Art/Generated/Characters3D/Named`, the same folder Gryph/Cassie-04/Sever/Echo live in, so this row is technically **EXISTS**, but per `PlaceholderCharacterBuilder`'s own scope this is a "Humanoid archetype" primitive stand-in, not a Tripo image→3D mesh like her castmates. A future art pass should replace this file's *contents* in place (same path, real mesh) rather than repointing the registry key — same guidance as Ch9's note on Vane's placeholder mesh.

**Note on the `ShipEntranceLight0/1` positions sitting on the return ramp, not the ship-entrance ground.** Both lights are built at z=145/155, which is inside the `ReturnRamp`'s z-span (120→158), not the ship-entrance ground's z-span (158→182). This reads intentionally as "the light gets warmer as you climb toward the exit" rather than a placement error, but is worth flagging since the naming ("ShipEntranceLight") implies a position at the entrance itself.

#### d. Combat — the Vess duel

Mechanically a standard `Enemy`/`BladeDamager` fight up to the 20% health threshold, at which point `DuelYield.EnterYield()` fires, disabling Vess's `Enemy` behavior and locking her in a non-aggressive yield pose. The player must **sheathe the sword** (release the `Grabbable`, `IsHeld == false`) to accept — or wait out the 30 s auto-accept safety net. Vess's `EnemyDefinition` (maxHealth 240, damage 20, moveSpeed 1.5, attackCooldown 0.85) sits between the syndicate guards and Sever in difficulty, reading as a genuine but not chapter-topping skill-check, consistent with the mercy resolution being the point of the fight rather than raw difficulty.

**No lethal outcome exists in the build for this fight** — `DuelYield.OnOpponentDied` treats a same-hit overkill as an automatic yield rather than a death (`EnterYield()` fires even on `opponent.Died`, per the shared component's own safety comment: "Overkill in one hit: yield instead of death. Builder should ensure opponent health is high enough to avoid crossing from above-threshold to 0 in one damage.") — Vess's 240 HP pool at a 20% (48 HP) threshold gives ample margin against most single-hit overkill scenarios, but this is worth re-verifying against the live `BladeDamager` EMA swing-speed model's maximum single-hit damage if that model is ever retuned.

#### e. Dialogue / VO

`Dialogue_Beat4_Ambush` (`ch10_beat4_ambush`), position (0, 1, 172), 3 lines, ≈47 s:

| Speaker | Line | sec |
|---|---|---|
| Vess | "Don't move. Don't you dare move that blade… Give me one reason. You took everyone I had." | 15 |
| Ronin-7 | "I won't give you one. You're right… Kill me. It's fair." | 11 |
| Ronin-7 | "But know what dies with me before you do it… Your revenge is real. It's just very, very small next to what we could do with my life instead of my death." | 21 |

→ combat trigger (step 19 activates Vess; the duel begins).

`Dialogue_Beat4_Choice` (`ch10_beat4_choice`), position (0, 1, 174), 6 lines, ≈102 s:

| Speaker | Line | sec |
|---|---|---|
| Vess | "Don't. Don't you tell me you're the champion of the galaxy. I am not buying it." | 9 |
| Ronin-7 | "I was built to be that. I'm not it anymore, and I'm trying to pay for what I was. Let me put down the thing the Dominion's building, and then my life's yours." | 21 |
| Vess | "Then you carry them too… That's worse than killing you. Good. Take the blade…" | 24 |
| Ronin-7 | "I'll wear it. Every name, out loud, until the dark gives all of them back… Take a berth on my ship. The strays read the dead together up there. There's room for one more." | 16 |
| Gryph | "A year ago I'd have called that the stupidest thing I ever watched a man do… I know that choice when I see it twice in one day. You'll do." | 13 |
| Echo | "She spared you, Cipher. Took the killing blow and chose not to land it… She's the first one that chose to spare you back." | 19 |

**Note on the audit-fixed line.** Ronin-7's second Beat 4 line ("I was built to be that…") was rewritten per `story ouput/audit/Ch10_audit.md`'s fix #3: the source script's original line had three typos ("i am" → "I'm," "abonimation" → "abomination," "dominion" → "Dominion"), dropped contractions into a register that clashed with his eloquence elsewhere, and was the only line in the chapter missing a voice-direction note. `Chapter10Lines.cs` documents this fix in its own header comment; both the source script's original wording (§ dialogue script) and the corrected build-time wording (above) are recorded so a reviewer can see exactly what changed.

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point** — the ambush, the duel, and the mercy exchange are all carried by `Haptics`/`AudioDirector`/`CombatFeedbackController`, never the camera.
- **The alarm-tone continues fading through this beat, the sonic counterpart to the climb-out.** Canon opens Beat 4 with "the alarm-tone fading away below them in the deep" and closes it with "the last of the alarm-tone dies away down in the shafts," right at the end of `ch10_beat4_choice` before Beat 5 begins — see §7's `ReturnClimbAlarmTone`. Its attenuation across the `ReturnRamp` should track the same 38 m climb the comfort watch-point below already flags, giving the ramp a matching audio arc (rising dread → fading relief) alongside the vection risk.
- Haptics carry every blade hit through the duel and (distinctly) the yield-state transition, per the existing pipeline — no new haptic authoring needed beyond what `DuelYield`/`BladeDamager` already drive.
- `ShipEntranceLight0/1` carry `behaviour: None` — level, warm daylight, no flicker; the beat's one "event" is entirely the duel and dialogue, not a lighting cue, the same "no lighting event, combat itself is the event" pattern Ch9's Beat 2 raid uses.
- Comfort vignette engages normally through the duel's frequent turns.
- **Comfort watch-point — `ReturnRamp` is the chapter's longest single continuous-vection leg and isn't covered by Beat 1's "most snap-turn-dense stretch" note.** It recovers 9 m over 38 m at ~13.3° in one uninterrupted uphill run (§2) — sustained forward-vection while looking up-slope during the climb-out is a distinct comfort case from the six short (~5–6°) descent ramps, and should be included alongside them when comfort-vignette tuning is verified in-headset.
- `ReverbZonePlacer` does not tag the ship entrance as an interior volume (it is exterior, no walls) — the reverb here should read open/outdoor by contrast with every enclosed room in the chapter, reinforcing "climbing back out into the sky."

---

### Beat 5 — The Target List (The Ship Entrance — Topside Staging)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> **No new art for this beat** — it reuses Beat 4's ship-entrance geometry unmodified. Create **`BuildBeat5Logic()`** only (the dialogue player and the chapter-outro trigger).

#### a. Narrative purpose & emotional target

The chapter's strategic payoff and closing beat: Cassie's ledger cross-indexed against the Concord Engine schematic (Ch9) lights two new destinations — a leviathan bone-canyon "older than the Dominion" (Ch11) and a Dominion cryo-command vault (Ch12) — turning "a wound into a target list," per the production note. The beat's emotional hook-out belongs entirely to Sable: her Beat 0 line ("the only one I think we bring up breathing") pays off as dread rather than relief when she reports the bone-canyon node "reads wrong… like she's already half gone and doesn't know it." Ronin-7's closing heading ("then the canyon's first… take us up") folds every thread of the chapter — the ledger, Vess's sentence, Sable's fear — into a single course, and Echo's final line explicitly names the chapter's tonal arc: "a win that walks out on its own legs… the canyon's going to make it a race. But that's tomorrow's hole. Today we ride up clean."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **No new NPCs, no new reach points.** Cassie-04, Gryph, and Vess (post-yield, allied) are all conceptually present for this beat's dialogue via decoupled `DialoguePlayer`s at the ship entrance — none of them physically relocate here (Cassie's and Gryph's Beat 1/2/3 build-time transforms are their only placements in the entire scene; Vess remains at her Beat 4 build position).
- **`ChapterOutro`:** at shipEntranceCenter + (0,1,9) = (0,1,179), inactive. `CampaignFlagSetter` sets **three** flags on activation — `ch10_complete`, `cassie_recruited`, `vess_recruited` — combining the completion flag with both this chapter's ally recruits in one step, the same pattern Ch7/Ch9 use for their own ally-flag combinations. `completeCanvas` ref = the "CHAPTER 10 COMPLETE" world-space canvas at shipEntranceCenter + (0,1.4,10) = (0,1.4,180).

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 22 | Dialogue | `Dialogue_Beat5_TargetList` (`ch10_beat5_targetlist`) — the cross-index reveal, both Ch11/Ch12 nodes lit, Sable's dread |
| 23 | Trigger | Activates `ChapterOutro` — sets the three flags, reveals the complete canvas, fades to black, publishes `ZoneCompleted` |

`ChapterOutro`'s fade timing is not explicitly overridden in the builder — it relies on the component's own defaults, the same unverified-default note Ch9's doc carries for its own outro *(inferred: verify against the live component before assuming a specific fade duration)*.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

**No new art.** The ship entrance — ground, `SalvageRig`, both accent lights — is entirely Beat 4's geometry, reused unmodified. The only new object is the inactive completion canvas:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| "CHAPTER 10 COMPLETE" canvas | (0, 1.4, 180) | — | built inline via `Ch10BuildCompleteCanvas` (worldspace `Canvas` + `Image` + `Text`, `LegacyRuntime.ttf`) | n/a — UI, not registry art |

**Candor note — the cross-index reveal's visual is entirely unbuilt.** The production note calls for "the Engine schematic from Ch9 re-projects, and the cross-index lights TWO remaining scattered-archive nodes on it" — a holographic map/meta-objective UI moment. No such VFX or UI exists in the build; the reveal is carried entirely by dialogue. This is the third of this chapter's three "big reveal, no matching visual" gaps, alongside the Phase-step blink effect's missing concept VFX and Beat 3's missing ledger-reveal VFX (§9).

**Cross-reference — Ronin-7's closing line names the rig aloud, sharpening the §Beat 4c `SalvageRig` gap at the chapter's final image.** His Beat 5 heading continues past the condensed table text below: "Gryph, get us aboard the rig… Take us up" (script line 486). §Beat 4c already flags that `Props.SalvageRig` reads as a tinted block with no boardable-rig/ramp silhouette; this is the beat where the object is spoken as the literal destination, making the missing boarding-read most visible at the exact moment it should land. Same gap, not a new one — noted here so the two mentions reinforce each other.

#### d. Combat

None. All hostiles from Beats 1, 2, and 4 are already resolved.

#### e. Dialogue / VO

`Dialogue_Beat5_TargetList` (`ch10_beat5_targetlist`), position (0, 1, 176), 6 lines, ≈133 s:

| Speaker | Line | sec |
|---|---|---|
| Cassie-04 | "Now for the part I've actually been looking forward to… each one a piece of the instructions for taking this thing apart." | 21 |
| Morrigan | "I see them. Two nodes, lit against the Engine frame… We don't just have a node out there. We have the beginning of it." | 22 |
| Morrigan | "And the second. Deeper inside Dominion ground… Then the build-record's whole and the Engine comes apart." | 20 |
| Sable | "I've felt them the whole time you've been down there, Cipher… I think I'm going to lose a sister before I ever reach her, and I think the canyon is where it happens." | 26 |
| Ronin-7 | "Then the canyon's first… Now we do it twice more, and the next one's running out of time. Take us up." | 20 |
| Echo | "Up, for once, and not empty-handed… But that's tomorrow's hole. Today we ride up clean." | 24 |

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics scripted — the chapter's quiet close, matching every prior chapter's Beat-4/5 "the fight is over, now deal with what it cost" pacing note.
- `ShipEntranceLight0/1` remain unchanged from Beat 4 — no new lighting event authored.
- This is the chapter's closing beat: `ChapterOutro`'s fade is the last thing the player sees before the smash-to-black the dialogue script calls for.

## 5. Character travel-route master table

**No NPC in Chapter 10 uses `NpcWalker`.** Same structural finding as Ch9: every named character — Gryph, Cassie-04, Sever, Vess — is placed exactly once at build time and never relocated by a scripted walk leg. The only entity that travels the chapter's full run (spawn z=2 down through the tiers, into the Archive, back up the return ramp, to the ship entrance) is the player.

| Character | Spawn position | Travel mechanism | Notes |
|---|---|---|---|
| Ronin-7 (player) | (0, 0, 2) | continuous locomotion + snap-turn, sloped-ramp climbing, no scripted path | walks the full descent-then-climb-out run across all six beats |
| Gryph | (2, 0, 18) | none — static placement, `AllyCombatant` idle/combat AI only | present physically for Beat 1's two mook fights only; comm-only/decoupled `DialoguePlayer` voice for Beats 2–5, per the class-header GRYPH PHYSICAL-PLACEMENT DECISION — he deliberately does not follow into the Archive (Sever's fight is written as a solo duel) or to the ship entrance |
| Cassie-04 | (0, -9, 112) | none — static placement, `StoryNpc`, no walker, no combat | present physically only in the Archive (Beats 2–3); her Beat 5 "target list" lines are a decoupled `DialoguePlayer` at the ship entrance despite the narrative staging her physically present there, reading the cross-index — the same "voice follows the beat, body doesn't" pattern flagged for Ch9's Sable at the Construction Core. **No rotation is set (§Beat 2b/2c) — she faces +Z, away from the player's -Z approach, for her entire on-screen presence.** |
| Sever / Ninja-2 | (0, -9, 104) | none — static placement, built inactive, `SetActive(true)` by the `DefeatEnemies` step itself | present only in Beat 2; dies in place, no death-drag or corpse relocation |
| Vess | (0, 0, 174) | none — static placement, built inactive, `SetActive(true)` by a dedicated Trigger step | present physically only at the ship entrance (Beats 4–5) |

**This is a deliberate, documented decision, not an oversight** — the class-header CREW-PRESENCE, GRYPH PHYSICAL-PLACEMENT, and CASSIE-04 STAGING comments all call it out explicitly, extending the same "place once, dialogue plays as a decoupled `DialoguePlayer`" convention Ch7/Ch9 establish for their own single-placement NPCs. Any future patch giving Cassie a walker from the Archive to the ship entrance (matching Ronin-7's "stay on Gryph's shoulder" line and her Beat 5 dialogue's physical framing) would need a `kesslerFloorY`-style Y-invariant of its own, since the Archive floor (y=-9) and ship-entrance floor (y=0) differ by 9 m — any such walker's waypoints would need to interpolate Y across the return ramp exactly the way the ramp itself does, or Cassie would clip through or float above the ramp geometry.

**Open question — lip-sync/talk-animation coverage**, same open item Ch9's doc carries forward. Gryph, Cassie-04, and Vess are physically on-screen through substantial dialogue (Beat 1's two sets, Beat 2's four sets, Beat 4's two sets respectively), but `Ch10PlaceAlly`/`Ch10PlaceStoryNpc`/`Ch10BuildNamedBoss` add only `AllyCombatant`/`StoryNpc`/`Enemy` — no `NpcTalkAnimator` or `Rig_Jaw` lip-sync. Whether a post-build wiring pass (the way `EnemyArtWirer` additively wires `NpcWalkAnimator`) also covers talk-animation for these three is not confirmed anywhere in this document — should be checked and recorded, not assumed.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Accent entries | Behaviour | Backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Cairn (briefing) | warm, close, stationary | `accentLights["Spawn"]` | `None` | none — voice-only, no exterior view | none; the beat plays out entirely as VO |
| 1 — The Ninefold Shafts (descent) | rust cooling to Program-blue, tier by tier | `accentLights["Tier1"…"Tier5"]` + six `TierN_PatrolLight` entries (color = the tier gradient value) | `Tier4Light`: `AmbientPulse(period:7s)`; others `None` | the six-tier tint gradient, static once built, plus the ambient Tier-4 gang war | **DefeatEnemies (steps 3, 7):** syndicate guards then mine automata spawn and fight; no lighting event marks either — combat itself is the event, same as Ch9's Beat 2 |
| 2 — Cassie-04 and Her Keeper (Archive) | cold, dry, data-glow cyan | `accentLights["Archive0/1"]` | `Archive0`: `ConsoleFlicker(seed:110)` | eight racks, catalogued and static; Sever invisible until the `DefeatEnemies` step | **implicit, via `BeginDefeatEnemies`:** Sever `SetActive(true)` — the beat's reveal moment, not a lighting change. **Missing:** canon stages the racks/`ArchiveLight0/1` dimming a fraction as the index reroutes through Cassie at the recruit moment (step 14) — not yet built, see §Beat 2f |
| 3 — Reading the First Names (Archive, reused) | same as Beat 2 | `accentLights["Archive0/1"]` | unchanged | unchanged | none scripted — the ledger reveal is carried entirely by dialogue, a real gap against the production note's "readable light" visual direction (§9) |
| 4 — Vess's Vengeance (ship entrance) | open, warm, exterior daylight | `accentLights["ShipEntrance0/1"]` | `None` | `SalvageRig`, static | **Trigger (step 19):** Vess `SetActive(true)` — the ambush's payoff, not a lighting change |
| 5 — The Target List (ship entrance, reused) | same as Beat 4 | `accentLights["ShipEntrance0/1"]` | unchanged | unchanged | **Trigger (step 23):** `ChapterOutro` — flags set, canvas revealed, fade to black, `ZoneCompleted` |

Fog is the same baseline exponential bed in every beat — a single profile value, never overridden per-tier/zone, matching every prior chapter's convention.

## 7. Audio / VO manifest cross-reference

Twelve canonical dialogue sets, defined in `Chapter10Lines.cs` and consumed via `Chapter10Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch10_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch10_beat1_descent` | 1 | (0, 1, 20) — `Dialogue_Beat1_Descent` |
| `ch10_beat1_strongroom` | 1 | (1, -3.5, 62) — `Dialogue_Beat1_Strongroom` |
| `ch10_beat2_keeper` | 2 | (0, -8, 102) — `Dialogue_Beat2_Keeper` |
| `ch10_beat2_kill` | 2 | (0, -8, 104) — `Dialogue_Beat2_Kill` |
| `ch10_beat2_phasestep` | 2 | (0, -8, 106) — `Dialogue_Beat2_PhaseStep` |
| `ch10_beat2_recruit` | 2 | (0, -8, 110) — `Dialogue_Beat2_Recruit` |
| `ch10_beat3_ninja_history` | 3 | (0, -8, 112) — `Dialogue_Beat3_NinjaHistory` |
| `ch10_beat3_reading` | 3 | (0, -8, 114) — `Dialogue_Beat3_Reading` |
| `ch10_beat4_ambush` | 4 | (0, 1, 172) — `Dialogue_Beat4_Ambush` |
| `ch10_beat4_choice` | 4 | (0, 1, 174) — `Dialogue_Beat4_Choice` |
| `ch10_beat5_targetlist` | 5 | (0, 1, 176) — `Dialogue_Beat5_TargetList` |

Each is built by the local `Ch10BuildDialogue` wrapper (the same "build via shared helper, wire clips ourselves" pattern every chapter uses): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch10WireVoiceClips`, resolving each line's `AudioClip` from `Chapter10Lines.ClipName(setId, index, speaker)` — pattern `ch10_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all twelve `DialoguePlayer`s.

**Dialogue is data, not art.** None of this changes in the refactor — the twelve set ids, their positions, and the clip-resolution pattern are canon.

**Applied naturalness/consistency fixes** (per `story ouput/audit/Ch10_audit.md`, all already landed in `Chapter10Lines.cs`, not open items):

| Fix | What changed |
|---|---|
| Hard error #3 — Beat 4, Ronin-7's surrender line | source had three typos ("i am" → "I'm," "abonimation" → "abomination," "dominion" → "Dominion"), missing contractions, and no voice-direction note — rewritten per the audit's suggested fix (§Beat 4e) |
| Hard ban — em-dashes in character speech | verified already clean (0 violations) for this chapter's source script |
| Speaker labels — "(comm)" stage direction | "Coral Vex (comm)" / "Sable (comm)" recorded under their plain names in `Chapter10Lines.cs`, matching every other chapter's comm-speaker convention |

**Missing gap — the canonical "comm degrades with depth" audio treatment is unbuilt.** The table above records the "(comm)" speaker labels for Coral Vex, Sable, and Morrigan, but nothing in the build applies any radio-degradation DSP to those lines. Canon states this twice: the SETTING block ("the channel thins with depth as it did in the Tide") and the CREW COMM production note ("the channel degrades with depth as established… the channel thins with depth"). The comm lines in `ch10_beat1_strongroom` (Coral Vex, Sable) and `ch10_beat5_targetlist` (Morrigan, Sable) should carry progressive depth-attenuation/artifacting versus the clean, close-mic topside briefing (`ch10_beat0_briefing`) — increasing through the descent, clearing again at the ship entrance. This is a cheap, high-immersion audio cue (a depth-driven comm-degradation bus keyed to the speaking character's `(comm)` tag) that the doc currently omits; it should sit alongside the missing upper-mine ambience bed below as an audio-pass priority.

**Missing gap — Echo/comm VO spatialization vs. physically-present speakers is unaddressed.** Canon is explicit that Echo is heard by Ronin-7 alone, wired to his optic feed (SETTING; every Echo voice-direction line in the source script closes "To Cipher alone"), and the crew (Morrigan/Coral/Sable) speak "over comm" — neither is a physically-present diegetic point source the way Gryph, Cassie-04, Sever, and Vess are. Yet every line in the chapter, Echo's included, routes through the same world-space `DialoguePlayer` anchor as every other speaker in that set — e.g. `Dialogue_Beat1_Descent` at (0,1,20), ~18 m ahead of the player still standing near spawn (0,0,2) when step 1 fires, so Echo's "We're in, Cipher. First tier" would emit spatially from down the ramp rather than read as an in-head voice. A single anchor-based `AudioSource` cannot be correct for both an in-head AI/comm voice (wants a 2D or heavily head-locked blend) and a physically-present speaker (wants full 3D spatialization at the NPC's body) sharing one dialogue set. This is the chapter's most-repeated VO subject — Echo speaks in every beat — and the current uniform spatial-blend setting on `BuildDialoguePlayer` treats his optic-feed voice like a diegetic point source. Flagged, not fixed: a per-line (or at minimum per-speaker) spatial-blend override is new `DialoguePlayer`/`Chapter10Lines` surface, not an art/registry change.

**VO production note not otherwise captured here.** Cassie-04's defining vocal signature — "the same faint archival doubling [as Sable], but the affect is bone-dry gallows wit, exact and quick" (script line 267) — is a load-bearing, repeated voice-direction note across every one of her lines in the source script. Without a deliberate doubling/layering post-pass consistent with (but tonally distinct from) Sable's own established treatment, this signature will not survive edge-tts and will be silently flattened into a generic voice. The VO director should preserve the doubling while keeping the *delivery* dry and ironic, not warm/grieving like Sable's — the two archive-node voices must read as siblings, not twins.

**Further voice-direction line-number citations, for the VO director's convenience — the same convention this document already uses for dialogue-script line numbers (e.g. 218, 237, 256, 334, 405).** The script's richest performance notes are otherwise referenced only narratively elsewhere in this document, not cited by line: Sable's Beat 0 "the doubled timbre pulled in close" direction (script line 200, §Beat 0a); Cassie's plea-for-mercy beat, "the doubled timbre carrying real, buried pity under the sarcasm" (script line 277, §Beat 2a); Sever's stutter direction, "Do not let him complete a free sentence; the leash always wins until death" (script line 292, §Beat 2a/2d); Echo's Phase-step-grant direction, "the awe steadied into purpose… the dead brother's whole function inverted into the exact thing he was built to deny" (script line 313, §Beat 2f); Vess's establishing direction, "establish her hard and fast and lethal… a grief that has compacted into flint over eleven years… righteous, not villainous… New distinct register, nothing archival, all blood and dust and patience" (script line 412, §Beat 4a/4c) — the chapter's one brand-new voice and a placeholder-mesh boss (§Beat 4c), so this direction carries the full characterization load the missing model can't; Ronin-7's Beat 4 surrender line, "Acceptance, not surrender" (script line 417, §Beat 4a/4e). None of these change the beat write-ups above — they are navigation aids only.

SFX / ambience beds, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip / component | Used for |
|---|---|
| `ArchiveAmbience` (`BuildAmbienceLayer`) | (-4,-6.4,104), inner 5 / outer 18, max vol 0.4 — the Archive's "counting the dead" hum bed |
| `DeepTierDreadAmbience` (`BuildAmbienceLayer`) | (2,-5.5,90), inner 5 / outer 18, max vol 0.4 — the deep-tier dread bed under the mine-automaton fight |
| `ReturnClimbAlarmTone` (`BuildAmbienceLayer`, proposed — not yet built) | near Tier 6/the Archive threshold (0,-9,104), armed at the Beat 3→4 transition (`ch10_beat3_reading`'s closing cutscene, step 16) and fading to silence across the `ReturnRamp`'s 38 m climb, inaudible by the close of `ch10_beat4_choice` (step 21) — the chapter's only dynamic, rising-then-fading non-combat audio event, and the sonic counterpart to Echo's closing "today we ride up clean" |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | runs once at the end of the build, wiring any procedurally-generated stingers the two ambience layers or accent lights reference |
| `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` | auto-tags the Archive as a distinct interior reverb volume; the six open tiers and the ship entrance are exterior/uncontained by design |

**Missing gap — the broken-seal alarm-tone is unbuilt.** The dialogue script establishes this as a recurring diegetic motif at three points — Beat 3's close ("a low alarm-tone threads up through the strongroom, its broken seals registering the intrusion behind them"), Beat 4's open ("the alarm-tone fading away below them in the deep"), and Beat 4's close ("the last of the alarm-tone dies away down in the shafts") — but no ambience layer or `AudioSource` in the build carries it. It is the chapter's only canonical non-combat audio *event* (everything else audio-wise is either a static bed or combat-driven), and its rise-then-fade is the sonic counterpart to "today we ride up clean": the vault registering the intrusion as the crew climbs, fading to nothing as they leave the dead behind. Should sit alongside the missing upper-mine ambience bed and the comm-degradation DSP below as an audio-pass priority (§Beat 3f, §Beat 4f).

**Missing gap — the upper-mine ambience bed does not exist**, the same class of finding as Ch9's missing `RustfangHoldAmbience`. Tiers 1–3 (where the player spends the syndicate-guard fight and the "honest mine" traversal) have no `BuildAmbienceLayer` of their own — only the two deep-tier/Archive beds exist. The SETTING block stages "dead lift-cages, ore-carts seized on their rails" with implied mechanical creak/settle texture that is currently silent except for combat SFX. A `NinefoldUpperMineAmbience` layer (centered near Tier 2) should be prioritized alongside the VFX commissions in §9.

**Missing gap — three canonical position-triggered bark pools are entirely unbuilt**, mirroring Ch9's identical finding. The production note for Beat 1 specifies a full crew-comm/Echo bark pool triggered by descent depth (sample: Sable "she's below us, three tiers, maybe four," Gryph "that gallery's shored wrong, go wide," Echo "this door's got no handle, Cipher… doors like that are built so the only way through is permission"), a full Echo bark set for the two mook fights, and a full Echo boss-bark set for the Sever duel. The build has only the two fixed Beat 1 dialogue sets — the walk between them and both entire mook fights and the boss duel play with zero reactive VO today, the same "author against final geometry" deferred work Ch9's doc flags.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 10 — The Ledger of Rust** (`XRRigBuilder.BuildChapter10LedgerOfRust()`).
2. **EditMode is the gate.** Per `CLAUDE.md`, the project baseline is **842 tests green, 0 skips**; PlayMode is **70/70 green**. Ch10 itself contributes **14 fixtures** to the suite (`PhaseStepSolver` — 7 — and `Chapter10Lines` — 7 — per `Project/Docs/CHAPTER-BUILD-LEDGER.md`'s 2026-07-03 entry, landing the running total at 496 at that snapshot: 493 pass / 3 skip / 0 failed). Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot, identical to every prior chapter's doc.** **No EditMode test invokes `BuildChapter10LedgerOfRust()` or loads `Ch10_LedgerOfRust.unity`.** `Chapter10LinesTests` covers only the dialogue-data module; `PhaseStepSolverTests` covers only the pure blink-geometry/cooldown math. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
4. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty mine, never an exception.
5. **`AllyCombatant` engagement-range regression watch**, same manual check Ch9's doc prescribes — **higher-priority here than in Ch9**, since Gryph's post-automata position (§Beat 1d) leaves him only ~12–18 m from Sever's Archive fight, tighter than Ch9's own spacing. After any change touching `AllyCombatant`, `EnemyWaveSpawner`, or Sever/Vess's activation timing, manually verify Gryph does **not** walk toward the Archive once Sever activates. There is currently no automated test guarding this.
6. **`DuelYield` overkill-margin check.** After any change to `Ch10Vess.asset`'s `maxHealth` or to `BladeDamager`'s EMA swing-speed model, manually verify a single maximum-damage hit cannot cross Vess from above her 20% yield threshold straight to 0 without ever entering the `Yielded` state — `DuelYield.OnOpponentDied` treats that case as an automatic yield rather than a death, but this is a safety net, not a guarantee the encounter reads correctly at the table if it fires.
7. **Perf reference bar:** **none recorded for Ch10 yet** (§1.6). Take a `UnityStats` reading via `script-execute` immediately after the first fresh build under this document and record drawCalls/setPassCalls/tris/verts here before any prefab lands. The Tier-4 gang-war pocket, layered on top of whatever mission-spine encounter is nearby, is the most likely spot to threaten the 72 Hz floor first — measure there specifically, not just at spawn.
8. **Console check:** `Ch10WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild, across all twelve sets.
9. **`Archive_WallN`/`ReturnRamp` collision check (new).** After fixing `ZoneBounds.fallResetY` (§1.1), walk the Archive to its north end and confirm the player can physically enter `ReturnRamp` rather than being blocked by `Archive_WallN` (§2, §9) — this is a second, independent P0 finding with no automated coverage; confirm in-editor before treating the chapter as completable.
10. **Gryph switchback-descent check (new).** With no `NavMesh` in this project, manually confirm Gryph's `AllyCombatant` retargeting actually walks him down the alternating-X ramps to reach the Tier 2 guards and Tier 5 automata, rather than stalling at his (2,0,18) spawn or walking off an open tier edge (§Beat 1b/1d) — there is no automated test guarding this.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception** — identical to every other chapter. Re-running `BuildChapter10LedgerOfRust()` wipes generated content. Patch additively in the live editor, or fix `Chapter10Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** ~288 unreferenced material variants exist project-wide but are regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Same project-wide rule as every other chapter — tier platforms, ramps, the Archive shell, and racks all get primitive colliders when they land as prefabs.
- **The `AllyCombatant` engagement-range gap is real and open**, carried forward from Ch9's identical finding (§Beat 1b, §8 item 5). Do not "quietly" tighten `AllyCombatant.RetargetNearestEnemy` while doing an unrelated art pass on this chapter; it is a shared component with 14-chapter blast radius and needs its own reviewed change.
- **The syndicate-guard/mine-automaton art assignment is index-parity, not type-aware**, a variant of Ch9's Coil-raider mismatch finding (§Beat 1c). `EnemyArtWirer`'s Ch10 entry names the narratively-correct pair (`Coil_Syndicate_Ganger`, `Ash-World_Scavenger`) but assigns them by scene-wide `Enemy`-component index parity, not by `EnemyDefinition`. Flagged, not silently "fixed" here — it is a one-line change in a shared table that needs its own sign-off, same guidance as Ch9's doc.
- **Sever's mid-fight "stutter" is pure VO, not a mechanical stagger-window** (§Beat 2d). The production note calls for the stutters to be player-readable in-combat openings; the as-built fight has no bespoke stagger-state machine, and the stutter line plays entirely in the pre-fight confrontation dialogue instead. Building the mechanical version is new combat-system surface — flag, don't fix, in an art/registry pass.
- **Three separate "big reveal, no matching visual" gaps exist in this chapter**, worth weighing together when prioritizing the art backlog: the Phase-step blink effect has no VFX concept yet (§Appendix B), Beat 3's ledger-reveal has no data-light/index visual (§Beat 3c), and Beat 5's cross-index map reveal has no holographic UI (§Beat 5c). None of the three currently degrade to even a crude primitive — they degrade to nothing, the same class of gap Ch9's doc flags for its own water-surface/data-light/lattice-tether trio. **Two further, related gaps belong in this cluster.** First, the canon Phase-step *demonstration* — Ronin-7 blinking through a filed rack so Echo can narrate "you just stepped through a wall" — is entirely unstaged. As-built, the `AbilityGranter` (step 12) simply unlocks the input while `Dialogue_Beat2_PhaseStep` (step 13) narrates over nothing; the player discovers the blink unaided, with neither a scripted demo action nor VFX to anchor Echo's line. Second, the death-moment shadow-transfer itself has no visual treatment: canon (screenplay line 67, SETTING lines 86-87) stages "his blade goes dark, then his shadow lifts off it and floods into Echo, cold and sudden" — the inheritance *moment*, distinct from the ability's later *use*. It lands between step 11 (`Dialogue_Beat2_Kill`'s "his shadow's free" line) and step 12 (the Phase-step grant) with nothing on screen, the direct sibling of Ch9's Vane shadow-transfer. §Beat 2c's corpse-and-blade note confirms the body persists after the kill but says nothing about the shadow-lift having any accompanying treatment — this is the fourth "reveal, no visual" in the chapter, not a third-of-three.
- **Cassie-04's "stay on Gryph's shoulder" staging is unbuilt.** Her Beat 3 dialogue and Ronin-7's closing line both frame her as physically walking out with the crew at Gryph's shoulder; the as-built scene keeps her a static transform in the Archive with her Beat 5 lines delivered as a decoupled `DialoguePlayer` at the ship entrance. A future fix (an `NpcWalker` leg from the Archive to the ship entrance) would need to interpolate Y across the 9 m of the return ramp's rise, the same kind of Y-invariant Ch1's `kesslerFloorY` protects — flagged, not fixed here.
- **Vess's DuelYield reuse is a deliberate mechanization choice, not a scope cut.** The source production note describes a bespoke three-phase rail (ambush / un-loseable duel / scripted mercy); the build instead reuses `DuelYield` exactly as Ch4's Kerrax fight does, honoring the narrative inversion (Vess is framed as the one sparing Cipher) without duplicating a second FSM. Any future patch "fixing" this to match the production note's literal staging more closely should weigh the cost of a second boss-mercy component against the value of matching the note precisely — not treated as an obvious bug to close.
- **The Tier-4 gang war is genuinely ambient, but not skippable** — unwired to any mission step, yet `EnemyWaveSpawner`'s 9 m horizontal-distance trigger radius around (-1,-6,71) covers the entirety of Tier 4's mandatory crossing (§Beat 1b/1d), so it always activates when the player traverses Tier 4, even though nothing forces the player to fight it. A future pass tightening chapter pacing should budget it as always-on simultaneous-actor load (consistent with §1.6's perf note), not as content a cautious player can avoid triggering.
- **`ZoneBounds`'s fall-catch threshold is a P0 progression blocker, not a misconfiguration to fold in among other open items (§1.1, §2).** `Chapter10Builder.cs:226-228` never overrides `fallResetY` from its class default of −3, and the intended floor drops to y=−9 at the Archive — every chapter that has shipped `ZoneBounds` so far stayed at or above y≈0, so this has never mattered before Ch10. As shipped, the reset fires every frame the rig is below y=−3, and the safe-position update cannot run while it does, so a player descending past Tier 2 is teleported back permanently — **the chapter is uncompletable past Tier 2 today.** Set `bounds.fallResetY` explicitly (e.g. `-12f`, comfortably below −9) before anything else in this chapter is verified; treat this as the top item in any Ch10 fix pass, not one bullet among several.
- **`Archive_WallN` sealing `ReturnRamp`'s low end is a second, independent P0 progression blocker (§1.1, §2, §Beat 4b) — not yet confirmed in-editor.** The wall (world Y=-7.2, corrected from this doc's earlier "1.8" typo — §2) sits exactly astride the ramp's z=120 low end, x∈[-10,10] fully covering the ramp's x∈[-8,8] width, with no aperture or side gap. Even once `ZoneBounds.fallResetY` is fixed, a player reaching the Archive and turning to climb out would, as built, walk into a solid wall instead of `ReturnRamp` — Beats 4–5 (climb-out, Vess, the target list, the ending) are unreachable behind it. Cut a ramp-width aperture in the wall at z=120, or start the ramp a couple meters north (z≈122). Verify in-editor before assuming the `ZoneBounds` fix alone restores completability — see §8 item 9.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All non-character props are cheap tinted primitives (`BuildProp`/raw `CreatePrimitive` calls) rather than unique materials — the same `TintShared`-adjacent economy every prior chapter documents, extended here with a six-tier procedurally-lerped floor/light gradient.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch10Environment.asset`. Currently set inline at the top of `BuildChapter10LedgerOfRust` (`Chapter10Builder.cs:126-151`).

| | Value |
|---|---|
| Directional key | color (0.75, 0.65, 0.5), intensity 0.4, rotation Euler(55, -35, 0) |
| Ambient | mode **Flat**, color (0.07, 0.06, 0.06) |
| Fog | mode **Exponential**, color (0.06, 0.05, 0.05), density 0.015 |
| Tier floor tint (procedural) | `Color.Lerp((0.42,0.28,0.16), (0.2,0.26,0.36), InverseLerp(0,5,tierIndex))` — rust at Tier 1, Program-blue by Tier 6 |
| Archive floor / ceiling tint | (0.05, 0.07, 0.09) / (0.03, 0.04, 0.05) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`), **sixteen total** — the most of any chapter to date:

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 6) | (0.9, 0.7, 0.4) | 1 | 10 | none |
| `Tier1Light` | (2, 0.5, 34) | (0.85, 0.6, 0.35) | 1.2 | 12 | none |
| `Tier2Light` | (-2, -1, 48) | (0.8, 0.55, 0.3) | 1.3 | 12 | none |
| `Tier3Light` | (1, -2.5, 62) | (0.5, 0.55, 0.65) | 1.4 | 12 | none |
| `Tier4Light` | (-1, -4, 76) | (0.35, 0.5, 0.7) | 1.5 | 12 | `AddAmbientPulse(period: 7f)` |
| `Tier5Light` | (2, -5.5, 90) | (0.3, 0.5, 0.75) | 1.6 | 12 | none |
| `ArchiveLight0` | (-4, -6.4, 104) | (0.3, 0.85, 0.9) | 1.8 | 16 | `AddConsoleFlicker(seed: 110f)` |
| `ArchiveLight1` | (4, -6.4, 112) | (0.3, 0.85, 0.9) | 1.8 | 16 | none |
| `ShipEntranceLight0` | (-4, 2.4, 145) | (0.9, 0.85, 0.7) | 1.4 | 16 | none |
| `ShipEntranceLight1` | (4, 2.4, 155) | (0.9, 0.85, 0.7) | 1.4 | 16 | none |
| `Tier1_PatrolLight` | (2, 0.7, 34) | tier-gradient value at t=0 | 1.2 | 9 | none |
| `Tier2_PatrolLight` | (-2, -0.8, 48) | tier-gradient value at t=0.2 | 1.2 | 9 | none |
| `Tier3_PatrolLight` | (1, -2.3, 62) | tier-gradient value at t=0.4 | 1.2 | 9 | none |
| `Tier4_PatrolLight` | (-1, -3.8, 76) | tier-gradient value at t=0.6 | 1.2 | 9 | none |
| `Tier5_PatrolLight` | (2, -5.3, 90) | tier-gradient value at t=0.8 | 1.2 | 9 | none |
| `Tier6_PatrolLight` | (0, -6.8, 104) | tier-gradient value at t=1.0 | 1.2 | 9 | none |

**Note on the two animated behaviours and `LightBudget` — confirmed correct, not a gap.** `Tier4Light`'s `AmbientPulse(7s)` and `ArchiveLight0`'s `ConsoleFlicker(seed 110)` are built via the shared, frozen `AddAmbientPulse`/`AddConsoleFlicker` helpers (`ChapterSharedBuilders.cs`), which attach `AmbientLightPulse`/`ConsoleFlickerLight` (`Scripts/World/`). Both components already gate their own `Update()` through `LightBudget.ShouldAnimate(GraphicsRuntime.Quality, isGameplaySignal: false)` — falling back to a static mid-range intensity and skipping the per-frame Perlin/sine drive whenever the Quest-tier budget says not to animate. Ch10 has the most animated decorative lights of any chapter to date, but nothing in this chapter needs to re-route or duplicate that gating — it is already inherited for free from the shared component, the same "reuse, don't reinvent" list CLAUDE.md names `LightBudget.ShouldAnimate` on.

**No event light exists this chapter** — same finding as Ch9. Sever's reveal, the Phase-step burst, and Vess's ambush are all non-lighting events.

**Note on Tier 6 having no explicit `TierNLight`, unlike Tiers 1–5.** Tiers 1–5 each get an explicit `TierNLight` accent (table above) *plus* their own `_PatrolLight`; Tier 6 gets only `Tier6_PatrolLight`. This reads fine in practice — `ArchiveLight0` sits at the same z (104) as Tier 6 and spills onto it, so the shaft-mouth is lit without a redundant fixture — but the asymmetry isn't stated anywhere, so a future editor "fixing" the apparent gap by adding a `Tier6Light` could over-light the threshold. Noted here to close that risk: Tier 6's lighting is deliberately carried by Archive spillover, not an omission.

**Note on the accent-light intensity ramp increasing with depth.** `Tier1Light` → `Tier5Light` climb 1.2 → 1.3 → 1.4 → 1.5 → 1.6, and the Archive sits at 1.8 — the deeper the player drops, the *brighter* the point lights get. Canon frames the deep repeatedly as darker and more oppressive: "going down and down into signal-dead dark," "each one a held breath," "the deeper the truer." The rising intensity is defensible as a deliberate choice — cold artificial vault-light replacing dim, failing rust-lamps — but no prior pass in this document has named that read. Either state the ramp is intentionally "artificial light replaces failing mine-lamps," or re-tune it so depth reads as encroaching dark; as authored, the chapter's own lighting data quietly argues the opposite of its SETTING block, and this should be reconciled one way or the other before `Ch10Environment.asset` is authored (§3.1).

### A.2 World root & the descent geometry

| Item | Value | Source |
|---|---|---|
| World root | `GameObject "Ninefold"` | `Chapter10Builder.cs:154-155` |
| Spawn Ground | `BuildFloorCeiling(world, "SpawnGround", (0,0,10), (10,0,20), (0.16,0.12,0.08), (0.06,0.05,0.05))` | `Chapter10Builder.cs:158-159` |
| Six tiers | `Ch10BuildTier(world, "Tier{1-6}", center, tierColor)` for centers (2,-1.5,34), (-2,-3,48), (1,-4.5,62), (-1,-6,76), (2,-7.5,90), (0,-9,104); `tierColor = Color.Lerp(rust, vault, InverseLerp(0,5,i))` | `Chapter10Builder.cs:163-179` |
| Each tier's floor cube | center + (0,-0.2,0), scale (10,0.4,10), `TintShared` to `tierColor` | `Ch10BuildTier`, `Chapter10Builder.cs:680-692` |
| Each tier's two rock props | center + (-2.2,0.5,-1.5) and (2,0.4,1.6), scale ~(0.8,1,0.8)/(0.7,0.8,0.7), tint `tierColor * 0.7` | `Ch10BuildTier` |
| Each tier's patrol light | center + (0,2.2,0), color `tierColor`, intensity 1.2, range 9 | `Ch10BuildTier` |
| `Ramp0`–`Ramp5` | tilted `BoxCollider` cubes, width 10, computed rise/run/angle per §2's ramps table | `Ch10BuildRamp`, `Chapter10Builder.cs:180-182, 696-711` |
| `SealedProgramDoor` | (1,-2.7,67), scale (9,3.4,0.4), tint (0.22,0.26,0.34) — `BuildProp`'s default `BoxCollider` blocks x∈[-3.5,5.5] of `Ramp3`'s x∈[-5,5] span, leaving a ~1.5 m west sliver (§1.2, §3, §Beat 1c) | `Chapter10Builder.cs:185-186` |
| Ore carts, Tiers 1–3 (×3, target fallback) | tier center + (-3,0.45,2.8), scale (1.2,0.9,1.8), fixed rust-metal tint (0.32,0.24,0.16) — **not** gradient-lerped; seized machinery reads the same regardless of depth | not yet in `Chapter10Builder.cs` — target primitive fallback for `Props.OreCart` (§1.5, §Beat 1c); absent from the build today |
| Dead lift-cages, Tiers 1–3 (×3, target fallback) | tier center + (3,1.2,-2.8), scale (1.5,2.4,1.5), fixed tint (0.25,0.2,0.15) | not yet in `Chapter10Builder.cs` — target primitive fallback for `Props.DeadLiftCage`; absent from the build today |
| `SyndicateGuardPost`, Tier 2 (target fallback) | (0,-2.6,47), scale (3,1.8,3), fixed scavenged tint (0.3,0.28,0.22) | not yet in `Chapter10Builder.cs` — target primitive fallback for `Props.SyndicateGuardPost`; absent from the build today |

**Note — Tiers 4–6's rock props read tonally wrong for a "machined Program strongroom."** Every tier, including the two lowest, gets the same two loose `_Rock0/1` props tinted `tierColor * 0.7` above. Canon is explicit that Tiers 4–6 are "signal-dead vaults wrapped in Program-original metal… rock machined into strongroom" — natural rockfall dressing in a milled accounts-vault reads as an art error once the tint gradient has cooled to Program-blue. `Props.TierPlatformKit` (or its primitive fallback) should swap loose-rock dressing for machined-panel/seam/debris props on Tiers 4–6, so the prop *language* shifts with the color gradient rather than staying "rusty mine rock" all the way to the Archive threshold.

### A.3 Beat 2/3 — The Archive props

| Item | Value | Source |
|---|---|---|
| Archive floor/ceiling | `BuildFloorCeiling(world, "Archive", (0,-9,108), (20,0,24), (0.05,0.07,0.09), (0.03,0.04,0.05))` | `Chapter10Builder.cs:191-192` |
| Archive walls | `Archive_WallW` (-10,-7.2,108) size(0.2,3.6,24); `Archive_WallE` (10,-7.2,108) size(0.2,3.6,24); `Archive_WallN` (0,-7.2,120) size(20,3.6,0.2) — no south wall. **Y = `archiveCenter.y`(-9) + `RoomH/2`(1.8), not the raw 1.8 local offset** — see §2/§9 for the `Archive_WallN`/`ReturnRamp` collision this places at the ramp's own low end | `Chapter10Builder.cs:193-195` |
| Archive rack row | `Ch10BuildArchiveRackRow(world, (0,-9,108))` — 4 z-offsets (-8,-4,4,8) × 2 sides (x=∓8) = 8 `ArchiveRack` props, each (0.4,2.2,1.2), y-center archiveCenter.y+1.1, flat tint (0.15,0.55,0.6) | `Chapter10Builder.cs:196, 713-724` |
| Cassie-04 | `Ch10PlaceStoryNpc(Ch10CassiePrefab, (0,-9,112), "Cassie-04")` — `InstantiateNpc` + `FitNamedCharacter` + re-add pos.y + `StoryNpc`, no combat. **No rotation argument exists on `Ch10PlaceStoryNpc`** — Cassie keeps `InstantiateNpc`'s default +Z facing; target is Euler(0,180,0) (§Beat 2b) | `Chapter10Builder.cs:268, 596-611` |
| Cassie's cabling rack (target fallback) | (0,-9,112), coincident with Cassie herself — a cluster of thin vertical cabling cubes (e.g. 4–6× scale ~(0.08,2.2,0.08), radial spacing ~0.3–0.5 m) framing her torso, tint dark cable-gray with a teal tint-lerp toward `ArchiveRack`'s (0.15,0.55,0.6) at the tips. **Colliders must be stripped/disabled — pure set-dressing, not blocking geometry (§Beat 2c)** | not yet in `Chapter10Builder.cs` — target primitive fallback for `Props.CassieCablingRack` (§Beat 2c); absent from the build today |
| Sever / Ninja-2 | `Ch10BuildNamedBoss(Ch10SeverPrefab, (0,-9,104), "Sever / Ninja-2", severDef, playerHealth)`, rotated Euler(0,180,0); `CapsuleCollider` center(0,1.1,0) height2.4 radius0.5; synthesized `ArmR`(0.35,1.4,0)→`Sword`→`Blade`→`BladeTip`(local 0,0,0.55); `Enemy` wired; built `SetActive(false)` | `Chapter10Builder.cs:273-274, 634-672` |
| Sever `EnemyDefinition` | `Ch10Sever.asset`: maxHealth 300, damage 26, moveSpeed 1.7, attackCooldown 0.75 | `Ch10EnsureSeverDefinition`, `Chapter10Builder.cs:527-542` |
| Phase-step granter | `GameObject "PhaseStepGranter"` + `AbilityGranter` (`abilityId = AbilityId.PhaseStep`), built `SetActive(false)` | `Chapter10Builder.cs:277-282` |
| `ArchiveAmbience` | (-4,-6.4,104), inner 5 / outer 18, max vol 0.4 | `Chapter10Builder.cs:398` |

### A.4 Beat 4/5 — The Ship Entrance props

| Item | Value | Source |
|---|---|---|
| Return ramp | `Ch10BuildRamp(world, "ReturnRamp", (0,-9,120), (0,0,158), 16)` — one long ~13.3° ramp abstracting the six-tier climb-out. **Its low end (0,-9,120) sits exactly inside `Archive_WallN`'s span (§2/§9) — a second P0 progression blocker, unfixed** | `Chapter10Builder.cs:203` |
| Ship-entrance ground | raw `PrimitiveType.Cube` at (0,-0.1,170), scale (16,0.2,24), `TintShared` (0.4,0.36,0.3) — no `BuildFloorCeiling`, exterior, no ceiling | `Chapter10Builder.cs:207-212` |
| `SalvageRig` | (3,1.4,178), scale (3,2.8,4), tint (0.35,0.32,0.28) | `Chapter10Builder.cs:213` |
| Vess | `Ch10BuildNamedBoss(Ch10VessPrefab, (0,0,174), "Vess", vessDef, playerHealth)`, rotated Euler(0,180,0); `StoryNpc` displayName "Vess"; `DuelYield` (`opponent`=own `Health`, `yieldThreshold`=0.2, `disableOnYield`=[vessGo], `sword`=player's `Grabbable`, `autoAcceptSeconds`=30); built `SetActive(false)` | `Chapter10Builder.cs:286-301` |
| Vess `EnemyDefinition` | `Ch10Vess.asset`: maxHealth 240, damage 20, moveSpeed 1.5, attackCooldown 0.85 | `Ch10EnsureVessDefinition`, `Chapter10Builder.cs:544-559` |
| "CHAPTER 10 COMPLETE" canvas | worldspace `Canvas` (700×220, scale 0.0015) at (0,1.4,180), rot Euler(0,180,0) facing -Z; `Image` bg (0.04,0.05,0.08,0.9); child `Text` "CHAPTER 10 COMPLETE", 54pt, color (0.9,0.92,1), `LegacyRuntime.ttf`; built `SetActive(false)` | `Ch10BuildCompleteCanvas`, `Chapter10Builder.cs:727-760` |
| `ChapterOutro` | (0,1,179), inactive; `CampaignFlagSetter` flags `[ch10_complete, cassie_recruited, vess_recruited]`; `completeCanvas` wired; `OnActivated` → `flagSetter.SetFlags` | `Chapter10Builder.cs:330-349` |

### A.5 Reach points, ability rig, and misc

| Item | Value | Source |
|---|---|---|
| Reach points | `SyndicateAreaReachPoint` (-2,-2,48); `StrongroomReachPoint` (1,-3.5,62); `AutomataAreaReachPoint` (2,-6.5,90); `ArchiveReachPoint` (0,-8,100); `ShipEntranceReachPoint` (0,1,170) | `Chapter10Builder.cs:304-313` |
| Player rig | `BuildRig(refs, addLocomotion:true)`, spawn (0,0,2); `EchoPresence`; `ZoneBounds` center(0,-4,84) radius150, `fallResetY` left at the class default of −3 (§1.1's P0 finding — the intended floor drops to y=-9, so the chapter is uncompletable past Tier 2 as shipped); `AttachPlayerAbilities(rig, refs)` (WeakpointSight + OverdriveController + PhaseStepController, all self-gating) | `Chapter10Builder.cs:223-230` |
| Katana "Echo" | `BuildSword((2,1,4), Euler(-90,0,0), weapon, Ch10EchoBladePrefab)` | `Chapter10Builder.cs:234` |
| Gryph | `Ch10PlaceAlly(Ch9GryphPrefab, (2,0,18), "Gryph")` — reuses the Ch9 prefab constant, no new Ch10-local one | `Chapter10Builder.cs:240` |
| Syndicate guards ×3 | positions (-4,-3,44), (0,-3,46), (3,-3,50); `BuildEnemy(pos, playerHealth, guardDef)`, built inactive | `Chapter10Builder.cs:245-255` |
| Mine automata ×2 | positions (-2,-7.5,86), (3,-7.5,92); same treatment | `Chapter10Builder.cs:257-264` |
| Syndicate guard `EnemyDefinition` | `Ch10SyndicateGuard.asset`: maxHealth 50, damage 8, moveSpeed 1.5, attackCooldown 0.9 | `Ch10EnsureSyndicateGuardDefinition`, `Chapter10Builder.cs:493-508` |
| Mine automaton `EnemyDefinition` | `Ch10MineAutomaton.asset`: maxHealth 130, damage 15, moveSpeed 1, attackCooldown 1.1 | `Ch10EnsureMineAutomatonDefinition`, `Chapter10Builder.cs:510-525` |
| Ambience layers | `ArchiveAmbience` (-4,-6.4,104) inner5/outer18/vol0.4; `DeepTierDreadAmbience` (2,-5.5,90) inner5/outer18/vol0.4 | `Chapter10Builder.cs:398-399` |
| Tier-4 gang-war pocket | `Ch10BuildGangWarPocket()` — 3v3 `FactionCombatant` brawlers at y=-5.1, z=74/77/79.5; `RustCrewBrawler0-2` (faction 0) x=−4.5/−3.5/−4.5, `DrifterCrewBrawler0-2` (faction 1) x=+2.5/+1.5/+2.5 (asymmetric — player crosses through the gap at x≈−1); `EnemyWaveSpawner`+`ActivationRelay` at (-1,-6,71), 9m horizontal-distance trigger radius covering Tier 4's mandatory crossing, unwired to any mission step | `Chapter10Builder.cs:408, 434-489` |
| Mission steps | 24 total, indices 0–23 | `Chapter10Builder.cs:358-384` |

### A.6 Scene root hierarchy (current)

`BuildChapter10LedgerOfRust()` creates these as **siblings**, not nested: `Directional Light`, `Ninefold` (world root — spawn ground, six tiers, six ramps, `SealedProgramDoor`, the Archive shell/racks, the ship-entrance ground/`SalvageRig`, `ReturnRamp`), the sixteen accent lights, `Game`, the player rig, Gryph, the three syndicate guards, the two mine automata, Cassie-04, Sever, `PhaseStepGranter`, Vess, the six gang-war brawlers, `Tier4GangWarSpawner`, the five reach points, twelve `DialoguePlayer`s, the two `BuildAmbienceLayer` instances, the "CHAPTER 10 COMPLETE" canvas, `ChapterOutro`, `Mission`, and `XR Interaction Manager`. Ch10 is, like Ch9, unusually loose at the root by comparison to the target contract — **none of the above are parented under `[STATIC_ART_DO_NOT_DELETE]` or a `[BEAT_N_LOGIC]` root**, both of which §1.4/§1.2 prescribe. Making that split real is part of this refactor's scope, not an incidental cleanup.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` (the six tier platforms + ramps, `SealedProgramDoor`, the Archive shell + rack row, the ship-entrance ground + `SalvageRig`, and Cassie-04's inert placement per §1.2) and six `[BEAT_N_LOGIC]` roots (0–5, minus Beats 3/5 which own no art of their own), and moves the reach points, `AllyCombatant`/`Enemy`/`DuelYield` spawns, ability granter, gang-war spawner, dialogue players, and `ChapterOutro`/`Mission` into the matching logic root for their beat.

---

## Appendix B — ArtAssetRegistry key inventory

| Registry Key | Category | Resolves To | Status |
|---|---|---|---|
| `Rooms.NinefoldSpawnGround` | Rooms | `…/Art/Generated/Rooms/NinefoldSpawnGround.prefab` | **MISSING** |
| `Rooms.NinefoldArchiveShell` | Rooms | `…/Art/Generated/Rooms/NinefoldArchiveShell.prefab` | **MISSING** |
| `Rooms.ShipEntranceGround` | Rooms | `…/Art/Generated/Rooms/ShipEntranceGround.prefab` | **MISSING** |
| `Props.TierPlatformKit` | Props | `…/Art/Generated/Props/TierPlatformKit.prefab` | **MISSING** — must accept a per-instance rust→Program-blue tint override (§Beat 1c note), not a fixed color |
| `Props.MineRampSection` | Props | `…/Art/Generated/Props/MineRampSection.prefab` | **MISSING** — a modular sloped-ramp mesh matching the seven rise/run/angle combinations in §2 |
| `Props.SealedProgramDoor` | Props | `…/Art/Generated/Props/SealedProgramDoor.prefab` | **MISSING** — must read as handle-less, machined, Program-original per the source script's repeated emphasis; one instance standing in for canon's plural/recurring handle-less-door motif (§3) |
| `Props.ArchiveRack` | Props | `…/Art/Generated/Props/ArchiveRack.prefab` | **MISSING** — flat teal tint, not gradient-lerped (§Beat 2c note) |
| `Props.SalvageRig` | Props | `…/Art/Generated/Props/SalvageRig.prefab` | **MISSING** — the crew's salvage/mining rig waiting at the mine's one way out; must read as a boardable rig with a visible boarding ramp/ship mass toward +Z (§Beat 4c note), not just a tinted block, so the climb-out has a destination object to walk toward |
| `Props.OreCart` | Props | `…/Art/Generated/Props/OreCart.prefab` | **MISSING — no primitive fallback authored yet.** Referenced by name in §1.3/§1.5/§Beat 1c's refactoring instruction since this doc's earlier drafts but never placed until this pass; target placements are one per Tier 1–3 (§Beat 1c, Appendix A.2) |
| `Props.DeadLiftCage` | Props | `…/Art/Generated/Props/DeadLiftCage.prefab` | **MISSING — no primitive fallback authored yet.** Same history as `Props.OreCart`; target placements one per Tier 1–3 |
| `Props.SyndicateGuardPost` | Props | `…/Art/Generated/Props/SyndicateGuardPost.prefab` | **MISSING — no primitive fallback authored yet.** Target placement grounds the three Tier-2 syndicate guards (§Beat 1c) |
| `Props.KatanaWorkbench` | Props | `…/Art/Generated/Props/KatanaWorkbench.prefab` | **MISSING** — voice-only Beat 0 dressing (§1.3, §Beat 0c); target placement at the katana's feet, (2,0,5) |
| `Vfx.PhaseStepBlink` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** The blink-through-matter effect the Phase-step unlock needs (world smearing dark/cold, per §1.1's VR-comfort framing) has no visual treatment of any kind today; must be authored non-occluding and free of any camera-shake/punch, per the same VR-comfort guardrail Ch9's water-surface note establishes. **Must double as a comfort screen-fade/vignette pulse across the translation itself** (§1.1, §Beat 2f) — an uncushioned instantaneous positional blink is a nausea risk. Separately, the canon demonstration beat (Ronin-7 blinking through a filed rack while Echo narrates "you just stepped through a wall") also has no scripted action to trigger it — the grant currently plays as narration over an unarmed ability with nothing to see (§9) |
| `Vfx.LedgerReveal` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** Beat 3's "readable light… names and disposal-sites… scrolling into legibility" reveal (§Beat 3c) |
| `Vfx.CrossIndexMap` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** Beat 5's Concord-Engine-schematic-plus-two-lit-nodes reveal (§Beat 5c), the direct sibling of Ch9's missing `Vfx.ConcordEngineDataLight` |
| `Vfx.ShadowInheritance` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** Sever's blade going dark and his shadow lifting into Echo at the kill (script line 67, SETTING lines 86-87) — the inheritance moment itself, landing between steps 11 and 12 (§9); the direct sibling of Ch9's Vane shadow-transfer |
| `Props.CassieCablingRack` | Props | `…/Art/Generated/Props/CassieCablingRack.prefab` | **MISSING — no primitive fallback authored yet.** Central cabling armature framing Cassie-04 at (0,-9,112), coincident with her own placement — canon's most-repeated image of her (§Beat 2c) |
| `Named.Cassie04` | Named characters | `…/Art/Generated/Characters3D/Named/Cassie-04.prefab` | **EXISTS** (real Tripo mesh, confirmed on disk at authoring time) |
| `Named.SeverNinja2` | Named characters | `…/Art/Generated/Characters3D/Named/Sever_Ninja-2.prefab` | **EXISTS** (real Tripo mesh, confirmed on disk at authoring time) |
| `Named.Vess` | Named characters | `…/Art/Generated/Characters3D/Named/Vess.prefab` | **EXISTS**, but content is a `PlaceholderCharacterBuilder` "Humanoid archetype" primitive stand-in, not Tripo art — replace the file's contents in place when real art lands, do not repoint the key |
| `Named.Gryph` | Named characters | `…/Art/Generated/Characters3D/Named/Gryph.prefab` | **EXISTS** (shared with Ch9 and every chapter that reuses him — same prefab constant, no Ch10-local duplicate) |
| `Named.Echo` | Named characters | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** (shared with every other chapter that places the katana) |
| `Enemies.SyndicateGuard` | Enemies | additively resolved by `EnemyArtWirer`'s per-scene table → `Coil_Syndicate_Ganger.prefab` or `Ash-World_Scavenger.prefab` (index-parity assigned, not type-aware) | **EXISTS**, assignment-mechanism gap flagged in §Beat 1c and §9 |
| `Enemies.MineAutomaton` | Enemies | same additive resolution, same two candidate meshes | **EXISTS**, same flagged gap |

**Eight of twenty-three keys resolve today** (five Named + the additively-wired Enemies slot counted once, six if Vess's placeholder mesh is counted as a genuine resolve), against fifteen `MISSING` rows — a similar starting ratio to Ch9's "five of twenty," carried forward for the same reason: Ch10 inherits the Named-character pipeline that had already matured by the time it was built. Of those `MISSING` rows, six were reconciled into this inventory across two passes: four (`Props.OreCart`, `Props.DeadLiftCage`, `Props.SyndicateGuardPost`, `Props.KatanaWorkbench`) in the prior pass — previously referenced by name elsewhere in the doc but absent from both this table and the per-beat art tables entirely (§Beat 1c, §Beat 0c) — and two more (`Vfx.ShadowInheritance`, `Props.CassieCablingRack`) in this pass (§Beat 2c, §9). The four VFX rows with no fallback at all (`Vfx.PhaseStepBlink`, `Vfx.LedgerReveal`, `Vfx.CrossIndexMap`, `Vfx.ShadowInheritance`) remain the highest-priority commissions in this inventory — see §9.

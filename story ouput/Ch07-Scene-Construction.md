# Chapter 7 — Scene Construction

*The architectural contract for `Ch07_ForgottenNames.unity`: what Chapter 7 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 7 ("Forgotten Names") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants, mission triggers, or the mindspace dive's teleport/fog contract.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter7Builder.cs`, entry point `XRRigBuilder.BuildChapter7ForgottenNames()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 07 — Forgotten Names**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch07_Forgotten_Names.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled rack, blade, or hall prop reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The outer-stacks gauntlet, the gang-war pocket, and the mindspace duel against The Previous Owner are all sold entirely through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never by moving the camera. This applies equally to the mindspace's entry/exit teleports (instant position sets, not lerps — see §4 Beat 4).
- **Traversal in Ch7 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)` — **no `WallClimbLocomotion`, no teleport locomotion, no NavMesh, no parkour** anywhere in this chapter's real-world hall (unlike Ch4/Ch6's climb kits). The **one exception is the mindspace dive itself**, which is not player locomotion at all: `MemoryDiveController.EnterDive()`/`ExitDive()` perform an instant `Transform.SetPositionAndRotation` on the rig root (CharacterController toggled off around the write), comfort-safe by construction because there is no continuous motion to shroud. Do not introduce any other teleport/climb mechanic when patching this scene.
- **The player rig carries every shipped permanent-ability component from Ch7 onward, not just this chapter's own.** `AttachPlayerAbilities` (§1.2's frozen helper) adds `WeakpointSight` (Ch7), `OverdriveController` (Ch9), `PhaseStepController` (Ch10), `UnbrokenWard` (Ch11), and `MirrorSummonController` (Ch12) to the rig unconditionally — each self-gates in its own `Awake()` on `CampaignState.HasAbility(...)`, so on a save that hasn't reached those later chapters they are present but inert. **Do not hand-add ability components per chapter** — call the shared helper, which already exists and is frozen (§1.2).

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | hall shells, rack rows, the oldest-blade prop, the read bench, VFX, backdrops, decorative lights, the mindspace's own geometry island | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC spawns (Coral Vex, the mindspace boss), enemy spawns + wave-spawner arming, the `MemoryDiveController`/entry/exit triggers, the `AbilityGranter`, reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**Chapter 7 has no doors to split art/logic across.** The whole hall is one continuous vaulted corridor (§2) with no lockable geometry anywhere. The closest analogues, mirroring how Chapter 6 treats its `EnemyWaveSpawner`s as the "door" for a chapter with none:

- **`OuterStacksWaveSpawner`** is the outer-stacks gauntlet's gate: the rack-lined hall and its doorway-free footprint are *art*; the spawner's trigger volume, its three waves, and the `Begin()` wiring on mission step 2 are *logic*.
- **`MemoryDiveController` + `MemoryDiveEntryTrigger`/`MemoryDiveExitTrigger`** are the mindspace's gate, exactly like Ch3's Kethel-7 playback and Ch5's massacre dive: the mindspace's own floor/walls/light are *art* (built once, `SetActive(false)` on the root); the trigger objects that call `EnterDive()`/`ExitDive()`, and the `AbilityGranter` that rides alongside the exit, are *logic*.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildWaveSpawner`, `AttachPlayerAbilities`, `Author*Step` — ripples across all of them. The chapter-agnostic runtime components this chapter reuses from Ch3/Ch5's precedent — `MemoryDiveController`, `MemoryFlashbackController`, `MemoryDiveEntryTrigger`, `MemoryDiveExitTrigger`, `AbilityGranter`, `FactionCombatant` — live in `Ronin7.World`/`Ronin7.World.Story`/`Ronin7.Enemies`, not editor code, but are load-bearing across any future chapter's memory-space or gang-war beat exactly like a `ChapterSharedBuilders` helper is.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, plus the runtime components named above.
- **Free to restructure:** the Ch7-local helpers, called only from `BuildChapter7ForgottenNames()` — `Ch7EnsureScavengerDefinition`/`Ch7EnsureAutomatonDefinition`/`Ch7EnsurePreviousOwnerDefinition`, `Ch7BuildDialogue`, `Ch7WireVoiceClips`, `Ch7PlaceStoryNpc`, `Ch7BuildWaveEnemies`, `Ch7BuildGangWarPocket`, `Ch7BuildFactionCombatant`, `Ch7BuildMindspaceBoss`, `Ch7BuildRackRow`, `Ch7BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs` or a runtime component under `Ronin7.World.Story`, stop — you have left Chapter 7 and are now silently rebuilding thirteen other chapters (or breaking Chapter 3/5's memory-dive beats).

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two ScriptableObjects carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch7Environment.asset` | directional key (frost-blue, low intensity), ambient mode + color, fog mode/color/density (cold archive haze), seven accent-light entries (spawn → outer stacks → tended core ×2 → deep archive ×2 → oldest blade, brightening/warming toward the core), the mindspace's own `MemoryFlashbackController` fog/ambient override values |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared with all other chapters, extended with Ch7-specific keys) |

Neither Ch7-specific asset exists yet (`Ch1Environment.asset`/`Ch6Environment.asset` are the pattern to copy).

Prefab **paths never appear in builder code.** The builder asks the registry for `Props.Rack`; the registry asset holds the path. This is the whole point of the indirection — art can re-point a prefab without touching a `.cs` file or this document.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live. New environment folders are siblings of `Characters3D/`:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Echo's blade lives here; Coral Vex + The Previous Owner do not yet)
  Rooms/                                    ← new — the reliquary hall shell, the mindspace shell
  Props/                                    ← new — racks, the oldest blade, the read bench
  VFX/                                      ← new — hilt-light glow, mindspace fracture dressing, outer-stacks frost/rime decal, breach cold-inrush
```

**No `Doors/` folder is needed for this chapter** — see §1.2 and §2's Gates table.

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter7ForgottenNames()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter7Builder.cs:111`) — the same pattern as every other chapter builder. That call discards the entire scene rather than deleting objects from it. There is nothing left to search for after it runs.
>
> Making the safe zone real requires **replacing the wipe strategy**, one of:
>
> 1. `EditorSceneManager.OpenScene(Ch7ScenePath)`, then `DestroyImmediate` each **generated root by name** (`DataReliquary`, `Mindspace_PreviousOwner`, `Game`, `Mission`, the rig, the accent lights, `OuterStacksWaveSpawner`, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist. `EnemyArtWirer.cs`/`CrowdArtWirer.cs`/`ParkourLevelBuilder.PatchCh06SummitRoute()` already demonstrate the open-in-place / mutate-idempotently / save pattern — reuse it rather than inventing a fourth.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred.**

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero Ch7 environment prefabs exist.** No reliquary hall shell, rack, oldest-blade prop, read-bench prop, or mindspace shell. Two named-cast prefabs are also missing — see below.

A builder that instantiates from an empty registry produces **an empty hall** — the first run of the refactored builder would destroy Chapter 7's gauntlet, its reveal beats, and the mini-boss dive.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props.Rack);
if (prefab == null) {
    Debug.LogWarning($"[Ch7] {ArtKey.Props.Rack} unresolved — primitive fallback.");
    Ch7BuildRackPrimitive(world, pos, hiltColor);   // Appendix A geometry
} else {
    InstantiateAt(prefab, world, pos, Quaternion.identity);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`). **Two named-character slots are already flagged as missing by the builder's own log message** — `Coral-Vex.prefab` and `The-Previous-Owner.prefab` are not yet baked to disk; `Ch7CoralPrefab`'s absence falls back to `InstantiateNpc`'s capsule placeholder, and `Ch7PreviousOwnerPrefab` falls back to `PlaceholderCharacterBuilder`'s already-defined `Ethereal`-archetype spec (ghost-blue glass orbs, primary (0.50, 0.68, 0.88) / secondary (0.75, 0.85, 0.95)) — that spec exists in `PlaceholderCharacterBuilder.cs` today but has not been baked to a `.prefab` asset. The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). Treat 90 FPS as the ceiling to protect and 72 Hz (`QualityBootstrap`'s shipped default) as the floor.
- **No recorded greybox baseline exists yet for this scene.** Capture one (`UnityStats` in edit mode, per `Project/Docs/CHAPTER-BUILD-LEDGER.md`'s Chapter 1 precedent) the first time this document's checklist (§8) is run.
- **This is the widest single-hall interior built to date.** The reliquary hall spans z[-2, 142] with racks lining both walls every 10 m from z=8 to z=134 (13 rack-pairs, 26 rack props total) plus 7 real-time point lights — draw calls and overdraw discipline matter here even before the mindspace island (a second, fully separate 14×14 room, offset to z=250, invisible until dive entry) is added on top. **That 10 m spacing is uniform end-to-end — rack *count* never climbs toward the core, only tint does.** Budget for this changing: §3.1's proposed taper/infill fix would add rack props specifically in the z88–134 deep-archive stretch, on top of (not instead of) the 26 already counted here.
- Every accent light in this chapter is a real-time `Light`: `SpawnLight`, `OuterStacksLight`, `TendedCoreLight0/1`, `DeepArchiveLight0/1`, `OldestBladeLight` (7), plus `MindspaceLight` inside the (normally inactive) dive island — 8 total. Re-measure draw calls/setPassCalls after every prefab lands.
- Prefabs replacing primitives carry their own materials and will not `TintShared`-batch (§3.1) — the same cost every other chapter's budget clause flags. `Ch7BuildRackRow`'s 26 rack props are the single highest-count set-dressing element in the chapter and the first thing to re-measure once a `Props.Rack` prefab lands.
- **The mindspace island being fully inactive until dive entry (`mindspaceGo.SetActive(false)`) means its geometry and lighting cost nothing until the player grips the oldest blade** — do not "helpfully" pre-warm it active; that defeats the entire reason it is a separate offset room rather than a subdivision of the main hall.

## 2. Chapter spatial map

Chapter 7 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch07_ForgottenNames.unity`, shaped as a **single vaulted hall running along +Z, with one self-contained side-room (the mindspace) offset far off to the side of the hall's coordinate space** rather than physically along it. There is no branching in the real world: the breach/spawn, the outer-stacks gauntlet, the gang-war pocket, the tended core (Coral Vex), the deep archive, and the read bench are all one hall, walked in a straight line with no doors and no way to skip ahead — the wave spawner and each `ReachTrigger` gate the mission's *advance*, never the player's physical footing. The mindspace is reached only by the mission's own Trigger step (grip the oldest blade), never by walking to it.

```
 -Z (the breach)                                                                                                          +Z (the deep archive)
 Spawn/Breach   Outer Stacks Gauntlet (racks z 8→134, both walls)        Gang War    Tended Core         Deep Archive      Oldest Blade    Read Bench   Outro
 z≈2–8          scavengers z18-24 · automata z34-38 · trigger (0,0,22)r12  Pocket    (Coral Vex, z65)    (z88-108)         anchor (z108-114) z 122-125   z 127-128
 x[-10,10]      x[-10,10]                                                z44-50     x[-10,10]           x[-10,10]         x[-10,10]         x[-10,10]

 wall S z=-2 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────── wall N z=142
                                                                                                          ▲
                                                                                       MemoryDiveController.TeleportRig — instant, not walked ▲
                                                                                                          |
                                                                          Mindspace_PreviousOwner (offset island, z=250, inactive until dive entry)
                                                                          entry (0,1,247) → boss (0,0,253) → exit teleports back to (0,1,112)
```

| Beat | Zone | Approx. z-range | World Y | Notable anchors |
|---|---|---|---|---|
| 0 | The Cairn (voice-only briefing — no physical set) | — | — | — |
| 1 | Breach/Spawn | z 2–8 | 0 | player spawn (0,0,2); katana (2,1,4); `SpawnLight` z=4 |
| 1 | Outer Stacks Gauntlet | z 8–50 | 0 | `OuterStacksLight` z=28; wave 0 (scavengers) z18-24; wave 1 (automata) z34-38; wave 2 gang-war pocket z44-50; `OuterStacksWaveSpawner_Trigger` (0,0,22), r=12 |
| 2–3 | Tended Core (Coral Vex — meeting, turn, forebear reveal) | z 58–80 | 0 | `TendedCoreReachPoint` (0,1,58) r=5; `TendedCoreLight0/1` z=65/68; Coral Vex spawn (0,0,65) |
| 4 | Deep Archive (kept-shadows reveal) | z 88–108 | 0 | `DeepArchiveReachPoint` (0,1,88) r=6; `DeepArchiveLight0/1` z=100/108 |
| 4 | The Oldest Blade (grip point) | z 108–114 | 0 | `OldestBladeReachPoint` (0,1,111) r=4; `OldestBlade` prop + `OldestBladeLight` z=113 |
| 4 | Mindspace (mini-boss dive island) | offset z=250 (not on the hall's z-axis) | 0 (mindspace-local) | entry (0,1,247); boss (0,0,253); exit teleports the rig to (0,1,112), back in the real hall |
| 5 | Read Bench (sabotage reveal, hook out) | z 122–125 | 0 | `ReadBench` z=122; `ReadBench_Rack` z=123.5 |
| 5 | Outro | z 127–128 | 0 | `ChapterOutro` (0,1,127); complete canvas (0,1.4,128) |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, used for the reliquary hall's own walls and the mindspace's walls alike — the mindspace is not a taller/lower space, deliberately reading as an interior of the same scale as the hall it was pulled out of.

**The whole hall's floor/ceiling is one `BuildFloorCeiling` call**, not four subdivided rooms like Chapter 1's — `BuildFloorCeiling(world, "ReliquaryHall", (0,0,70), (20, 0, 146), …)`, meaning the floor spans world z≈[-3, 143]. Four side walls (`ReliquaryHall_WallW/E/S/N`) bound `x[-10,10]`, `z[-2,142]`. **No `BuildDoorwayWall` call exists anywhere in this chapter** — there is no aperture to preserve, unlike Chapter 6's open wall-gaps. A room-shell prefab reproducing this hall must be one continuous shell, not four stitched sub-rooms; the "zone" rows in the table above are lighting/dressing/mission-gate boundaries, not physical wall boundaries.

**Gates** — no doors exist. Every gate in this chapter is either a proximity-armed `EnemyWaveSpawner` or a `ReachTrigger`/`Trigger` mission step, exactly the pattern Chapter 6 establishes for a door-free chapter:

| Gate | Position | Kind | Gating mechanism |
|---|---|---|---|
| Outer Stacks Gauntlet | `OuterStacksWaveSpawner_Trigger` (0,0,22) | `EnemyWaveSpawner`, `triggerRadius` 12 | Armed by mission step 2's `DefeatWaves`; polls `Camera.main`'s horizontal distance to the trigger point, then runs wave0 (scavengers) → wave1 (automata) → wave2 (gang-war pocket) in sequence, each wave gated on all its `Health`s reaching zero |
| The Tended Core | `TendedCoreReachPoint` (0,1,58) | `ReachTrigger`, radius 5 | Mission step 4 — player must physically walk to within 5 m before `Dialogue_Beat2_Archivist` (step 5) can play |
| The Deep Archive | `DeepArchiveReachPoint` (0,1,88) | `ReachTrigger`, radius 6 | Mission step 7 |
| The Oldest Blade | `OldestBladeReachPoint` (0,1,111) | `ReachTrigger`, radius 4 | Mission step 9 |
| Grip the Blade (mindspace entry) | `EnterMindspaceTrigger`, built inactive | `Trigger` | Mission step 11 activates it; its `OnEnable` calls `MemoryDiveController.EnterDive()` |
| Exit the Mindspace | `ExitMindspaceTrigger` + `WeakpointSightGranter`, built inactive | `Trigger` (dual-target) | Mission step 14 activates both simultaneously — `ExitDive()` and the ability grant fire together |

**A note on canon's "descent."** The dialogue script frames Beat 4 as descending through "a sealed hatch into the deep archive," "the bottom of the reliquary." The hall as built has no doors (above) and sits at a single flat world-Y=0 throughout — that descent and the sealed hatch are narrative framing carried entirely by the dialogue anchors moving north (deeper into the barge), not a physical drop in world-space (see also §9).

**A note on canon's "cross-hall."** `Dialogue_Beat1_Breach`'s wired line 4 says "something older walking the cross-hall past them" (of the automata), and the deferred gauntlet-bark sample pool includes "Automaton on the cross-hall." The hall as built is a single straight corridor (`x[-10,10]`, no branching) — the automata spawn at (-2,0,34)/(2,0,38), on the main spine, not a perpendicular passage. Same class of gap as the descent note above: either treat the cross-hall as dialogue-only framing (an implied space the player never walks, matching how the descent is handled), or have a future `ReliquaryHallShell` prefab dress a perpendicular alcove/cross-passage near z=34 so the automaton-patrol read has a real space to walk. Not yet resolved; see §9.

**A note on the oldest blade's terminus.** Canon stages the oldest sword as the end of the line — "the oldest sword in the hall, mounted alone on the back wall," "the deep archive, the bottom of the reliquary." As built, `OldestBlade` sits at (0,1.2,113) dead-center on the spine, but `WallN` sits at z=142 — 29 m further north — and the read bench (z=122) plus `ChapterOutro`/the complete canvas (z=127–128) both sit past it on that same open corridor, so the prop the entire mini-boss beat hinges on is a freestanding object in a continuing hallway, not a mounted terminus. §2's descent note above only excuses the flat-Y framing, not this. See §4 Beat 4c for the proposed fix.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring), `ZoneBounds` center **(0, 3, 128), radius 155** — a single bounding sphere loosely enclosing the entire real-world hall *and* the offset mindspace island (horizontal XZ distance from the bounds center to the mindspace boss at (0,0,253) is |253−128| = **125 m** (125.03 m in full 3D, including the 3 m Y offset), comfortably inside the 155 m radius; ZoneBounds clamps the rig's XZ every `LateUpdate` regardless of dive state, so it must reach the mindspace too, or the very first frame after `EnterDive()` teleports the rig straight back out of it). `AttachPlayerAbilities(rig, refs)` adds every shipped permanent ability (§1.1) including this chapter's own `WeakpointSight`. The katana rides from scene start at (2, 1, 4) — no rack-wake beat, matching the Act II "cost, not initiation" precedent Ch5/Ch6 established.

## 3. Global environment & backdrop

**A tomb that someone tends, not a dungeon.** Per the dialogue script's SETTING block, the reliquary should never read as a level to clear — it is "a cathedral hush over a warehouse of the erased," a salvager's hoard built inside a gutted Program records-barge. Vaulted halls of mounted blades run into the dark in every direction; each hilt carries a small steady light, each light a kept witness. The reliquary is cold, low-lit, and quiet to the point of pressure at the outer stacks, where frost rimes the dead conduits; the salvager's own patched power runs warm only at the core, where the racks are densest and the hilt-lights brightest. **Chapter 6 dressed its horror as beauty; Chapter 7 dresses it as devotion — the place is lovingly kept, and what it keeps is unforgivable.** Sound design should carry a constant low undertone throughout: a faint chorus of shelved shadow-AIs, awake in the dark inside their blades, never fully silent, rising near dense racks and going briefly, terribly clear during the mindspace duel and any blade-rescue interaction.

**The breach itself, at the spawn point.** The chapter's founding image is Kessler's wired line — "we can't dock, we can't fly into a debris belt to pull you, so once you're in, you're in" — and canon's Beat 1 has "a breaching pod... punch him through into the outer stacks." As built, the player spawns at (0,0,2) with a solid, undressed `ReliquaryHall_WallS` at z=-2 directly behind them; nothing marks where Cipher came through. §4 Beat 1c now carries the fix as a set-dressing brief: a torn-hull `Props.BreachAperture` on/through `ReliquaryHall_WallS` (z≈-2), a `Rooms.BreachBackdrop` debris-field/starfield visible through it, and a cold-air `VFX.BreachInrush` at the entry — so the one place the chapter's founding image should be literal doesn't read as "materialized in a finished hallway." **The pod itself is still missing as a prop**, and it bookends the chapter on both ends — Kessler's "Pod's away… once you're in, you're in" opens it, and the final stage direction, "The pod takes Cipher and Coral off the reliquary and back across the debris field," closes it. §4 Beat 1c below now proposes a `Props.BreachPod`, docked/embedded at or through the aperture, so both the founding and the closing image have a literal object to point at.

**Patchy gravity, felt but not modeled.** Distinct from §9's traversal cut — canon's wall-runs and ledge-jumps across broken-gravity shelving, correctly flattened to flat-floor continuous locomotion for this project — canon's "gravity gone patchy" is also *environmental*: the derelict itself should read as spinning-dead, not merely dim. As built, the outer stacks (z8–50) are an ordinary level floor with nothing implying broken gravity beyond `FrostRime`'s cold-conduit dressing. A purely visual, non-traversal dressing layer — slowly drifting blade fragments/dust, or a canted, half-toppled rack among the outer stacks — would sell "gravity gone patchy" as felt atmosphere without touching the continuous-locomotion contract. See §4 Beat 1c's proposed `VFX.DriftDebris`.

**The realized gradient.** `Ch7BuildRackRow(world, zStart: 8, zEnd: 134, spacing: 10)` places a rack pair (both walls, x=∓9) every 10 m from the breach through to just past the read bench, tinting each pair by `Mathf.InverseLerp(8, 134, z)` between a dim blue-grey hilt color `(0.28, 0.32, 0.48)` at the outer stacks and a bright gold hilt color `(0.95, 0.82, 0.5)` at the deep archive/read-bench end — the script's "reliquary of saints" image, done with cheap prop tint rather than a real light per rack (§1.6's VR-perf note: this hall would otherwise need dozens of point lights). Seven real-time accent lights layer the same gradient at coarser resolution: `SpawnLight`/`OuterStacksLight` a matching cool blue (0.55, 0.7, 0.9), `TendedCoreLight0/1` warm gold (1, 0.88, 0.6), `DeepArchiveLight0/1` warmer still (1, 0.9, 0.65), and `OldestBladeLight` a hard warning red (0.9, 0.25, 0.2) marking the one blade that has gone wrong.

**Six segments, per the dialogue script's own structure**, realized as this chapter's six beats (§4): Segment 0 is the voice-only Cairn briefing; Segment 1 is the breach and the outer-stacks combat-and-traversal gauntlet (rival scavengers vs. archive-defense automata, plus the gang-war pocket); Segment 2 is the tended core meeting Coral Vex and her trust-test turn; Segment 3 is the deeper-core forebear reveal (Ally #4); Segment 4 is the deep archive's kept-shadows reveal and the mini-boss dive; Segment 5 is the read-bench sabotage reveal and the Silent Garden hook into Chapter 8.

**Crew-presence decision (documented in the builder's class summary).** Canon has Cipher breach the hulk alone — "Cipher breaches the hull alone and works inward... Kessler, Iris, and Mera hold the comm from the ship" — so Kessler/Mera Voss/Morrigan (and, in the Beat 0 briefing only, Resh/Iris/Mira) are `DialoguePlayer` speaker labels only, never physical NPCs, the same convention Ch5/Ch6 use for crew who stay aboard. **Only Ronin-7 (the player), Coral Vex, and the mindspace boss get physical placement in this scene.**

**Coral Vex physical-placement decision.** Coral is placed **once**, at the tended core (0, 0, 65) — where she is first met — rather than walked or teleported to each subsequent beat location. Every later Coral line (the forebear reveal, the kept-shadows reveal, the read-bench sabotage reveal) plays as a disembodied `DialoguePlayer` positioned at that beat's own anchor, the "dialogue panel decoupled from the physical NPC" convention every chapter already uses for crew comm lines, and, in Ch6, for Master Kaelen's post-fight confession versus his fixed tower position. Simpler than authoring a walk cycle for a single scene, and consistent with the established pattern — see §5 for the full accounting.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter7Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch7Environment.asset`. Its schema (identical shape to Chapter 1/6's, new instance):

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` per zone, ×7, plus the mindspace's own light |
| `eventLights[]` | — | unused this chapter — no alarm/red-wash event light; `OldestBladeLight`'s warning red is a static accent, not a triggered event light |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("TendedCoreLight0", seed: 77f)` and `AddAmbientPulse("MindspaceLight", periodSeconds: 6.4f)` calls with data. Every other accent light in the chapter carries `Behaviour: None`. **The `AddAmbientPulse("MindspaceLight", …)` call itself is a confirmed as-built no-op** — see §4 Beat 4f and §9 for why, and do not carry the bug forward into the data-driven `behaviour` field when this migrates.

The eight accent-light entries (7 in the real-world hall + `MindspaceLight` inside the dive island) are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass.

**Material / tint palette:** set-dressing props today are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials — this keeps draw-call count low and keeps regeneration cheap. The rack-row gradient (dim blue-grey → bright gold, above) is this chapter's signature tint scheme and the single highest-value thing a prefab pass must preserve or deliberately supersede — losing the gradient collapses the "devotion grows as horror grows" read the whole chapter is built around. **Prefabs replacing primitives must carry their own materials and will not batch this way** — the perf cost §1.6's budget clause exists to police.

**`VFX.HiltGlow` — the hilt-as-witness motif, now keyed.** §1.3's VFX-folder listing promises "hilt-light glow" as a commission alongside the mindspace fracture dressing and the outer-stacks frost/rime decal, but until this pass neither it nor `VFX.MindspaceFracture` had a corresponding row in Appendix B — only `VFX.FrostRime` did. Both are now registered (Appendix B). `VFX.HiltGlow` is a small emissive/glow overlay `Ch7BuildRackRow`'s rack row (or the `Props.Rack` prefab itself) carries per hilt — far cheaper than a real point light per rack (§1.6's VR-perf note), but the difference between a lit witness and a colored bar. "A small steady light burns in every hilt, each light a kept witness" is the SETTING block's defining image and the accusation the whole chapter rests on; today it is delivered only by flat prop tint, with no emission or glow at all. See §4 Beat 1c's art table.

**`VFX.FrostRime` — now placed, not just keyed.** Unlike `HiltGlow` above, `FrostRime` already carried an Appendix B row before this pass but had never been placed in any beat's art table — a commission with a key and no brief. §4 Beat 1c's art table now anchors it to the cold outer end (z≈8–34, on the dim-blue conduits/walls, thinning out as the rack gradient warms toward the core), the visual half of the temperature story this section otherwise tells only in prose ("frost rimes the dead conduits") and in `SpawnLight`/`OuterStacksLight`'s cool color — the paired cold-atmosphere companion to `HiltGlow`'s warm one.

**Known conflict — the gradient contradicts Beat 3's Wraith-line reveal.** `Ch7BuildRackRow`'s curve is monotonic: it only brightens from z=8 to z=134, with no local dip anywhere. Beat 3's anchor (z=75, `InverseLerp(8,134,75)` ≈ 0.53) already reads as a warm mid-tone, and the gradient only gets brighter past that point. But Coral's forebear-reveal line points at "the old racks, the blades at the back, the dim ones whose hilts I can't keep lit anymore. Those are mine. My run. The Wraith line" — there is no dim/dying-hilt rack cluster anywhere near z=75 for that line to describe, and the built gradient says the opposite of what the VO does. This is the chapter's one Wraith-line visual motif and it currently has zero representation. **Proposed fix:** a small cluster of dim/dead-hilt "Wraith rack" props near z=75 — either a deliberate local dip in the tint curve or a wall-offset back row reusing the outer-stacks' dim-blue tint — so Coral's forebear line has something true to point at. See §9.

**Stripped vs. pristine racks — encoded only in tint today.** Canon distinguishes the deep archive's racks as "not stripped or salvaged but pristine, Program-original," against the outer stacks' "dark and stripped bare" racks — a looted-vs.-kept contrast the whole chapter's meaning rests on. The single uniform `Props.Rack` prop plus the tint-only gradient (above) encodes brightness but not stripped-vs-pristine geometry or empty-vs-full brackets. A prefab pass should consider a stripped/empty-bracket `Props.Rack` variant for the z8–34 outer end, so the "looted vs. kept" contrast reads in geometry, not just in color.

**Rack density stays flat — canon's "denser toward the core" has no built representation.** `Ch7BuildRackRow(world, zStart: 8, zEnd: 134, spacing: 10)` places a rack pair every 10 m across the *entire* hall — the tended core and deep archive are only brighter/warmer than the outer stacks (above), never fuller. But canon insists on increasing physical density at every core beat: Beat 1's stage direction calls the core "the densest blade-field yet, hilt-lights packed close and bright"; Beat 2 describes "mounted swords packed close"; Beat 4 stages "rank on rank… stretching into the dark… the densest and most reverent space"; and the RECURRING-ELEMENTS block states plainly that "the deeper Cipher goes, the denser and brighter the blade-fields get, until the core is lit like a reliquary of saints." At the fixed 10 m spacing, the deep archive (z88–134, ~5 pairs) is actually the *sparsest* stretch of hall in absolute rack count — the signature "packed close" read is carried entirely by color, never by fullness. **Proposed fix:** taper `Ch7BuildRackRow`'s spacing tighter toward the core (e.g. 10 m at the outer stacks narrowing to 5–6 m from the tended core inward), or add an infill/back-row rack pass across z88–134, so the "reliquary of saints" reads as increasing fullness, not just increasing gold. This is the single largest unflagged divergence from the SETTING block's defining image; see §1.6's budget note and §9.

## 4. Per-beat scene spec

The chapter plays as six beats, mirroring the dialogue script's SEGMENT 0–5 structure: a voice-only briefing, the breach-and-gauntlet, the archivist's meeting-and-turn, the forebear reveal, the kept-shadows reveal culminating in the mindspace duel, and the sabotage reveal with its hook into Chapter 8. Each beat is documented with the same a–f structure used in every other chapter's Scene Construction doc.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the gradient invariant.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the prefab is already on disk via the Tripo pipeline.
- **All twelve dialogue sets below (Beats 0–5 alike) are the shipped, post-audit `Chapter7Lines.cs` text**, per the editorial note under Beat 0e — not just the one table it's attached to. Every line-table in this section will differ, sometimes substantially, from the raw dialogue-script prose quoted in §3/§9's narrative citations (e.g. Beat 3's "you're the fix they made after me" vs. the script's "my correction," Beat 5's "this one didn't fail on its own" vs. the script's "it was failed, by one of their own"). A future pass diffing any beat's table against the dialogue script is checking against superseded prose, not finding a canon violation.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> This beat builds **no physical set** — do not add a Cairn command-room shell for Chapter 7; that set belongs to Chapter 1/4/5/6's builders. Your objective is only to separate the dialogue wiring (`BuildBeat0Logic()`) from nothing, because there is no `BuildBeat0Art()` to write.

#### a. Narrative purpose & emotional target

Morrigan's analysis bench is newly bolted into the Cairn's reignited control tier and already running hot, and this is the first time the crew reads Cipher's sabotaged killswitch through her eyes. Her opening verdict is the beat's spine: "That is not a scavenger getting lucky. Somebody who builds leashes for a living did this. An insider." Mera Voss names the danger without heat ("either the best news we've had or a very well-built trap"); the crew agrees to chase the papers rather than the insider directly, and one name surfaces out of the operative-record black market — Coral Vex. Resh places the reliquary on the map with wary respect ("a place you don't loot"); Iris reads past the transaction to the motive under it ("nobody hoards the dead for profit... you hoard them because somebody has to remember"). Mira's one line — "Is that the thing in his head?" — does the beat's thematic work in the fewest words, an innocent naming the leash plainly while the adults talk around it. Echo's unease is new: "a woman with a barge full of the erased is exactly who I'd be afraid of, if I were the kind of thing that could be shelved. Which I am" — the chapter's dread stated once, by the one character who has the most to lose in the room they're about to enter. Ronin-7 closes on the mission as a personal stake, not a hope: "Somebody opened my door. I'm done not knowing who stands on the other side of it. Plot the reliquary."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **No physical NPCs.** Morrigan, Mera Voss, Resh, Iris, Kessler, and Mira are `DialoguePlayer` speaker labels only, per §3's crew-presence decision — there is nothing here to spawn, ground, or wander.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4) — the same world position as the player's own spawn point in Beat 1, so the briefing is heard while the rig already exists at its landing spot, not from a bodiless briefing state.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` — set `ch7_beat0_briefing`, 13 lines — plays in full before the outer-stacks gauntlet is reachable |

**What changes during the beat:** nothing in the world — there is no world yet from the player's point of view. This step exists purely to seat the mission's premise (chase Coral Vex, chase the insider) before Beat 1 hands over control.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(none — no physical set this beat)* | — | — | — | — |

#### d. Combat

None.

#### e. Dialogue / VO

Dialogue set id: **`ch7_beat0_briefing`**, built at (0, 1, 4), 13 lines, ≈162 s, advanced on **Left-Hand "Talk" (Y)**. Full line set (Speaker | Line | seconds), per `Chapter7Lines.GetBeat0BriefingLines()` (shipped, post audit-fix text — see the editorial note below):

| Speaker | Line | sec |
|---|---|---|
| Morrigan | First thing the bench tells me, and you're not going to like it. Whoever cut your leash knew this firmware better than the people who shipped it. Every block they touched, they touched in the right order. No scavenger gets that lucky. Somebody who builds leashes for a living did this. An insider. | 19 |
| Mera Voss | An insider who breaks the Program's own leashes. That's either the best news we've had or a very well-built trap. From here I can't tell which. | 9 |
| Morrigan | Which is why we leave the insider for now and go after the papers that would name them. There's a collector who trades them. Operative records, the real ones, the erased ones, bought and hoarded off every black market that handles dead Program assets. One name keeps surfacing under all of it. Coral Vex. | 18 |
| Resh | Vex. Yeah. I've heard salvagers say the name in a low voice, like you'd talk about a place you don't loot. She runs a reliquary out in the dead lanes, an old records-barge she gutted and made her own. Buys the paper of dead operatives and never sells a sheet of it back. Hoards it. Nobody's worked out why. | 18 |
| Iris | A collector who keeps records of the erased and won't trade them. That's not about money. It's a reason. Nobody hoards the dead for profit. You hoard them because somebody has to remember. I want to know who she's remembering. | 14 |
| Kessler | And I want to know which she is before we knock. Mera's right. You can't tell a friend from a hunter on paper. | 13 |
| Mera Voss | You tell them apart inside. An ally lets you leave. A hunter lets you get in deep first. So we go in expecting both and we keep a route back to the hull. | 11 |
| Mira | Is that the thing in his head? | 2 |
| Morrigan | It's the thing they put in his head, and the thing somebody else took out for him. Both. That's what doesn't fit, little one. The Program builds these to never come off. His came off clean. Someone wanted it to. | 14 |
| Echo | Cipher, I don't love walking toward somebody who keeps shadows for a living. A woman with a barge full of the erased is exactly who I'd be afraid of, if I were the kind of thing that could be shelved. Which I am. | 16 |
| Ronin-7 | Then we go meet the one person who might know what they did with the rest of you. | 6 |
| Ronin-7 | Somebody opened my door. I'm done not knowing who stands on the other side of it. Plot the reliquary. | 8 |
| Kessler | All right. Dead lanes, slow approach, no dock. We put you on the hull and we hold off it, comm open, route home kept clear. Same as the mountain. You go in, Cipher, you find out which she is, you come back. | 14 |

**Editorial note:** `Chapter7Lines.cs` documents that `story ouput/audit/Ch07_audit.md` graded the source script C on naturalness (one em-dash at the Beat 3 line about "the first attempt," roughly fourteen "not X, it's Y" antitheses, aphorism-stacking on Coral Vex, a reused records/paper/relics triad, and a couple of on-the-nose emotion tags). Every rewrite the audit table proposed is applied — the table above is the **shipped, post-fix text**, not the raw dialogue-script prose quoted in §3/§9's narrative citations.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — this is a pure dialogue beat with no combat and no traversal.
- No ambient bed is built for this beat specifically; there is no physical Cairn set to carry room tone.
- Comfort vignette behaves per the standard posture guard; the player is free to walk during the dialogue — `BuildRig(addLocomotion: true)` grants full locomotion from spawn, and nothing gates on movement this beat (matches Ch1's own §f phrasing for its equivalent briefing).
- **Accepted immersion cost, not fixed:** the warm Cairn crew-council — Morrigan's bench, Mira under Kessler's arm — is heard while the player physically stands in the cold reliquary breach, since no Cairn set is built this chapter (per the `[CRITICAL CLAUDE REFACTORING INSTRUCTION]` above). The crew's own space is voice-only color for the rest of the chapter.

---

### Beat 1 — The Outer Stacks (The Breach → the Gauntlet)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (the reliquary hall shell, the rack-row gradient, the katana's resting spot) and **`BuildBeat1Logic()`** (the wave spawner's three waves, the gauntlet bark, the dialogue anchors). **`OuterStacksWaveSpawner` is this beat's "door"** (§1.2) — art builds the hall the gauntlet plays inside; logic decides when it turns hostile and in what order the waves resolve.

#### a. Narrative purpose & emotional target

The Cairn cannot dock; Cipher breaches the hull alone and works inward through the outer stacks. This is the chapter's combat-and-traversal core: rival scavengers (lightly armed, will flee) prying blades off the racks for resale, and archive-defense automata (relentless, no allegiance) still running dead patrol routes a lifetime after the barge's crew died — the two factions fight each other as readily as they fight Cipher, and the level design intent is explicit that "the player can play the two factions against each other." Echo's unease from the briefing fully arrives here: "Those racks aren't dead. They're blades, rows of them, and some are awake the way I'm awake. Every one of them is one of me, and I can feel them feeling me." The looting thins out, the automata fall or lose the trail, and the halls go from stripped-and-dark to tended-and-lit — Echo hands the moment over as the gauntlet ends: "That's the core, Cipher... After this it stops being a fight. It's whatever she's been waiting to do."

**The trust test (production note, scoped out of this pass — see §9).** The source script calls for a real, tracked spare-vs-kill choice across the gauntlet: whether Cipher lets fleeing scavengers escape or hunts them down determines which variant of Coral's opening Beat 2 line plays. `Chapter7Builder`'s class summary documents this as a **deliberate scope cut** — no tracking system exists, and `Chapter7Lines` wires only the warm/spare-path variant unconditionally. This is flagged for the reviewer, not silently resolved.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player rig:** spawns at world (0, 0, 2) via `BuildRig`'s shared on-foot default (`OnFootSpawnZ = 2`). The katana rides from the start at (2, 1, 4), alongside spawn — no rack-wake ceremony this chapter.
- **`OuterStacksWaveSpawner`:** trigger point (0, 0, 22), `triggerRadius` 12 m (horizontal distance only). Built **active-idle**, not inactive — an inactive spawner cannot `StartCoroutine` (the lesson Ch4's `HunterWave` established and every later chapter's wave spawner comment repeats). `Begin()` is called by mission step 2's `DefeatWaves` kind, which arms the spawner's own proximity poll; the actual wave-0 spawn only fires once the player physically walks within 12 m of (0,0,22).
- **Three waves, resolved in sequence** (§4d below has the full combat breakdown): wave 0 (3 scavengers, `Ch7Scavenger` definition), wave 1 (2 automata, `Ch7Automaton` definition), wave 2 (the gang-war pocket, 6 `FactionCombatant`s split into a turncoat-scavenger cell and a rogue-drone cell that hunt each other as well as the player). All enemies in all three waves are built **inactive**; `EnemyWaveSpawner.StartWave` activates each wave's `Health` GameObjects only when the prior wave is fully cleared.
- **Wave-0 bark:** `dlgGauntletBark` (`ch7_beat1_gauntlet_bark`) is wired as wave 0's `bark` field — `EnemyWaveSpawner.StartWave` calls `bark.Play()` non-blocking the instant wave 0 activates, so it fires alongside the first scavenger contact rather than gating anything.
- **Dialogue anchors:** `Dialogue_Beat1_Breach` at (0,1,8) plays before the gauntlet triggers (mission step 1, ahead of step 2's `DefeatWaves`); `Dialogue_Beat1_CoreAhead` at (0,1,50) plays after the gauntlet resolves, handing the beat off to the tended core.
- **The shrine-core reveal is a designed sightline/beacon (blocking note, not yet enforced in code).** The dialogue script's stage direction (line 230) stages the gauntlet's end as an explicit threshold: "The halls go from stripped and dark to tended and lit. The frost gives way to the salvager's own patched warmth. Ahead, the densest blade-field yet, hilt-lights packed close and bright, a shrine in the dark, and one figure standing in it, unhurried, waiting, watching him come." The warm `TendedCoreLight0`/`TendedCoreLight1` pool (§3.1, z=65/68) should be readable as a distant warm beacon at the dark far end of the corridor from z≈44 onward — the gang-war pocket, the last stretch of frost — pulling the player forward toward the shrine Coral tends, exactly the reveal-timing/draw-the-player-forward blocking the Ch01 template foregrounds. This is the visual payoff of the cold→warm gradient (§3.1) the whole chapter is engineered around; nothing in the builder currently tunes light range/fog falloff to guarantee that pool is visible that far back, so it should be verified in-headset alongside Beat 2b's fog/occlusion reveal-timing note below, not assumed from light range alone.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Breach` — set `ch7_beat1_breach`, 4 lines — Kessler's comm hand-off ("once you're in, you're in") + Echo's first read of the racks |
| 2 | DefeatWaves | "Beat1: The Outer Stacks Gauntlet (scavengers + automata)" — arms `OuterStacksWaveSpawner.Begin()`; the step advances only once `EnemyWaveSpawner.Completed` fires, i.e. all three waves (scavengers → automata → gang-war pocket) are cleared |
| 3 | Dialogue | `Dialogue_Beat1_CoreAhead` — set `ch7_beat1_core_ahead`, 1 line — Echo hands the moment over: "It stops being a fight" |
| 4 | ReachTrigger | "ReachTrigger: The Tended Core" — gates on distance to `TendedCoreReachPoint` (0,1,58), radius 5 — this beat's exit, handing off to Beat 2 |

**What changes during the beat:** nothing in the set dressing itself — the rack-lined hall is static geometry from the moment it is built. The only state that changes is the three waves' `Health` GameObjects flipping active in sequence as the player advances, and the mission director stepping through dialogue/reach gates around that combat.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Reliquary hall shell (floor/ceiling, x[-10,10] z[-2,142]) | center (0,0,70) | `Rooms.ReliquaryHallShell` | `…/Art/Generated/Rooms/ReliquaryHallShell.prefab` | **MISSING** |
| Breach aperture (torn-hull prop, set into/on `ReliquaryHall_WallS`) | (0,1.5,-2), facing -Z toward the debris backdrop | `Props.BreachAperture` | `…/Art/Generated/Props/BreachAperture.prefab` | **MISSING** |
| Debris-field/starfield backdrop, visible through the aperture | behind `ReliquaryHall_WallS`, z<-2, unwalkable skybox-style dressing | `Rooms.BreachBackdrop` | `…/Art/Generated/Rooms/BreachBackdrop.prefab` | **MISSING** |
| Breach pod (docked/embedded, visible at or through the aperture) | (0,1.5,-3), nose toward `ReliquaryHall_WallS`/`Props.BreachAperture` | `Props.BreachPod` | `…/Art/Generated/Props/BreachPod.prefab` | **MISSING** |
| Cold-inrush VFX at the breach (frost/fog puff around spawn) | (0,1,1), around player spawn (0,0,2) | `VFX.BreachInrush` | `…/Art/Generated/VFX/BreachInrush.prefab` | **MISSING** |
| Rack pairs ×13 (both walls, z=8…134 step 10) | (-9,1.1,z) / (9,1.1,z), tint lerps dim-blue→bright-gold | `Props.Rack` | `…/Art/Generated/Props/Rack.prefab` | **MISSING** |
| Hilt-glow overlay (per-hilt emissive, rack pairs ×13) | same positions as rack pairs above | `VFX.HiltGlow` | `…/Art/Generated/VFX/HiltGlow.prefab` | **MISSING** |
| Frost-rime decal (cold-end conduits, thinning as the rack gradient warms) | outer-stacks walls/conduits, z≈8–34, dim-blue end of the gradient | `VFX.FrostRime` | `…/Art/Generated/VFX/FrostRime.prefab` | **MISSING** |
| Drifting debris/dust (atmosphere only, non-traversal — sells "gravity gone patchy") | scattered through the outer stacks, z≈8–50, above head height | `VFX.DriftDebris` | `…/Art/Generated/VFX/DriftDebris.prefab` | **MISSING** |
| Katana "Echo" | (2,1,4), rot Euler(-90,0,0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnLight` (accent) | (0,2.4,4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |
| `OuterStacksLight` (accent) | (0,2.4,28) | — | `ChapterEnvironmentProfile.accentLights["OuterStacks"]` | profile |
| Scavenger ×3 | (-3,0,18) / (0,0,21) / (3,0,24) | `Enemies.RivalScavenger` | `…/Art/Generated/Characters3D/Enemies/RivalScavenger.prefab` | **MISSING** |
| Automaton ×2 | (-2,0,34) / (2,0,38) | `Enemies.ArchiveDefenseAutomaton` | `…/Art/Generated/Characters3D/Enemies/ArchiveDefenseAutomaton.prefab` | **MISSING** |
| Turncoat scavenger ×3 (gang-war pocket) | (-3,0,44) / (-1,0,47) / (-3,0,50) | `Enemies.RivalScavenger` *(shared with wave 0 — see §9)* | same as above | **MISSING** |
| Rogue drone ×3 (gang-war pocket) | (3,0,44) / (1,0,47) / (3,0,50) | `Enemies.RogueDrone` | `…/Art/Generated/Characters3D/Enemies/RogueDrone.prefab` | **MISSING** |

**`Props.BreachPod` — the chapter's founding and closing image, currently unbuilt as a prop.** Kessler's "Pod's away… once you're in, you're in" opens the chapter and the final stage direction — "The pod takes Cipher and Coral off the reliquary and back across the debris field" — closes it, but no pod prop exists anywhere in the hall today. A docked/embedded pod at the breach end, visible at or through `Props.BreachAperture`, makes the founding image literal; see §4 Beat 5c for why the outro itself, 130 m north, does not also stage against it.

**`VFX.DriftDebris` is atmosphere-only, not traversal.** Flagged explicitly so it is not confused with §9's broken-gravity wall-run/ledge-jump cut — this is pure dressing (slow-drifting fragments/dust, optionally paired with a canted `Props.Rack` variant half-toppled in the outer stacks) meant to sell "gravity gone patchy" as a felt visual, with zero interaction and zero effect on the flat-floor continuous-locomotion hall.

**The breach dressing above sits behind the player's spawn-facing, not in front of it — a blocking call the art table doesn't make explicit.** Player spawn is (0,0,2) facing +Z into the hall (`OnFootSpawnZ`'s default), and `Props.BreachAperture`/`Props.BreachPod`/`Rooms.BreachBackdrop` all sit at z≈-2 to -3 — at the player's back. As placed, the chapter's founding image (Kessler's "Pod's away… once you're in, you're in") is only seen on a deliberate 180° look-back/snap-turn, never in the first frame. **Call: accept this as a look-back reward, not a defect to fix by reorienting spawn.** Cipher works inward per canon, and a player who never turns around still carries the founding image as Kessler's VO alone — the same "narrative color the player may or may not physically confirm" treatment §3's crew-presence decision already accepts elsewhere. Do not "fix" this by rotating spawn to face -Z; that would stage the player looking at the breach instead of into the gauntlet, undercutting the +Z advance the whole beat is built around.

**Notes on the transition.** Every enemy today is a tinted capsule primitive (§Appendix A). The rack row is the chapter's single highest-value art commission — its 13-pair, 26-prop gradient carries the entire "reliquary of saints" read described in §3, and a prefab replacing it must reproduce or improve on the dim-blue-to-bright-gold `Color.Lerp` curve, not just supply a static rack mesh. The gang-war pocket's `FactionCombatant`s (§4d) currently have **no distinct visual identity from the wave-0/wave-1 enemies** — both scavenger factions (wave 0's rival looters and wave 2's turncoat cell) are literally the same primitive-capsule shape with no faction-color tint, a gap worth flagging for the art pass since the mechanic (two factions hunting each other) has nothing visual distinguishing them today. A `VFX.HiltGlow` overlay (art table above, and §3.1) is the cheaper companion commission to the rack mesh itself — without it, even a fully-modeled `Props.Rack` prefab still reads as painted cubes rather than lit witnesses.

**Scavenger looting activity is unbuilt.** Canon's Beat 1 has the scavengers "work the near stacks with cutting torches and grav-sleds, prying blades off the brackets" — today the wave-0 spawns are static tinted capsules in an otherwise static hall, with no torch glow, spark VFX, or sled hum. A torch-glow point light or spark VFX on the wave-0 scavenger positions (z18-24) would give the outer stacks a concrete sensory identity distinct from the automata, and would partially close the faction-legibility gap noted above — a torch-lit looter reads instantly differently from a patrol-routine automaton or a rogue drone. See §9.

#### d. Combat — the outer-stacks gauntlet

Player damage output is via `BladeDamager`'s EMA swing-speed model (**existing system — reuse, don't reinvent**). Player `Health` lives on the rig, and is the `playerHealth` reference every scavenger/automaton's `Enemy` component targets.

- **Wave 0 — Rival Scavengers** (`Ch7Scavenger`: maxHealth 50, damage 8, moveSpeed 1.6, attackCooldown 0.9s): three plain `Enemy`s at (-3,0,18)/(0,0,21)/(3,0,24), lightly armed, "will flee" per canon — though the as-built `Enemy` AI has no distinct flee-state; a scavenger that breaks off simply stops attacking under normal `Enemy` combat logic, not a scripted retreat animation *(flagged, not fixed — see §9)*.
- **Wave 1 — Archive-Defense Automata** (`Ch7Automaton`: maxHealth 140, damage 16, moveSpeed 1.0, attackCooldown 1.0s): two plain `Enemy`s at (-2,0,34)/(2,0,38), heavier and slower than the scavengers, "relentless, no allegiance" — mechanically identical to any other chapter's `Enemy`, the canon distinction (a security system, not a person) is narrative color, not a combat-behavior difference.
- **Wave 2 — The Gang War Pocket** (`Ch7BuildGangWarPocket`): six `FactionCombatant`s (not `Enemy`) — three turncoat-scavenger capsules at faction 0 (moveSpeed 1.4, attackRange 1.6, damagePerHit 8, attackInterval 1.4s, maxHealth 45) and three rogue-drone capsules at faction 1, same stats, different faction id **(of these, `Ch7BuildFactionCombatant` (`Chapter7Builder.cs:543`) only authors `maxHealth` and the faction id — `moveSpeed`/`attackRange`/`damagePerHit`/`attackInterval` are `FactionCombatant`'s own serialized-field defaults (`FactionCombatant.cs:20-23`), not builder-set values; a future edit to those defaults would silently re-balance Ch7's gang-war without touching `Chapter7Builder.cs`, worth knowing given §1.2's scope-discipline policy around the frozen shared component)**. **This is the class doc's own claim made mechanical**: `Enemy`-type combatants (waves 0/1) only ever target the player, but `FactionCombatant` identifies a valid target as *either* the player *or* another `FactionCombatant` with a different `factionId` — so the turncoat cell and the rogue-drone cell genuinely fight each other, not just the player, letting the player "play the two factions against each other" for real rather than as narrated flavor.
- **The one shipped visual payoff: `FactionCombatant.OnDied()` topples and greys the corpse.** Verified at `FactionCombatant.cs:64` — on death the body rotates face-down and tints to (0.3, 0.3, 0.3) via `RendererTint`, the same MPB-batched tint helper the rack gradient uses (§3.1). §9 already flags that the two factions have no distinct visual identity *while alive*; this topple-and-grey is the one concrete, already-shipping cue that a kill actually happened between the cells rather than against the player, and the art pass should preserve or build on it — a faction-tinted corpse (rather than a uniform grey one), once the faction-tint gap above is closed, would read even better. **Unaddressed: those corpses persist.** Up to 6 toppled-and-greyed `FactionCombatant` bodies plus however many of the wave-0/1 `Enemy` corpses the player leaves behind litter the one straight, doorless corridor (§2) the player must keep walking north through for Beats 2–5 — a tonal snag against the "cathedral hush" the SETTING block asks for on the approach to Coral's shrine, and a small draw-call/overdraw tail §1.6 doesn't currently count. Worth a decision: accept it, or fade/sink corpses after a delay so the tended-core approach reads the way canon stages it ("the looting thins out behind him"). Not yet resolved.
- **`RetargetNearestEnemy()` treats the player as an always-valid target, which can pull both cells onto Cipher instead of onto each other.** Every `retargetInterval` (1 s, `FactionCombatant.cs:24`) each combatant re-picks the nearest live hostile, and the player's `CharacterController` always counts as one (`FactionCombatant.cs:12,144`) — so a Cipher who wades into the pocket at close range is likely to draw both cells onto himself rather than watch them bleed each other. The "let the machine and the looters bleed each other, then walk through" fantasy (Echo's wired `ch7_beat1_gauntlet_bark` line 1, §4e) only reads if the player hangs back at range long enough for the two cells to close on each other first — a real approach/blocking nuance this document hasn't previously named.
- **Sequencing:** `EnemyWaveSpawner` runs the three waves strictly in order — wave 1 does not activate until every wave-0 `Health` is dead, and wave 2 does not activate until every wave-1 `Health` is dead. There is no way to skip ahead to the gang-war pocket without first clearing the scavengers and automata.

#### e. Dialogue / VO

Both sets advance on **Left-Hand "Talk" (Y)**, via the shared `PromptInputAdvancer`/`DialoguePlayer`. Clips resolve from `Assets/Ronin7/Art/Generated/Audio/Voice` through `Chapter7Lines.ClipName`.

**`Dialogue_Beat1_Breach`** (`ch7_beat1_breach`, at (0,1,8), 4 lines, ≈51 s):

| Speaker | Line | sec |
|---|---|---|
| Kessler | Pod's away and we're holding in the field. We can't dock that hulk and we can't fly into a debris belt to pull you, so once you're in, you're in. Comm stays open as long as her hull lets it. Find out which she is, Cipher. Then find the door. | 15 |
| Echo | We're inside it now, Cipher, and I want you to hear what I hear. Under the hum. That low layer, all through the dark. Those racks aren't dead. They're blades, rows of them, and some are awake the way I'm awake. Every one of them is one of me, and I can feel them feeling me. | 16 |
| Ronin-7 | Then we go quiet and we go fast. Read me the room. | 4 |
| Echo | Scavengers ahead, three of them, prying blades off the racks for resale. And something older walking the cross-hall past them. The barge's own defense automata, still on patrol a lifetime after the crew died. They don't care who you are. They kill the looters too. Use that. | 16 |

**`Dialogue_Beat1_GauntletBark`** (`ch7_beat1_gauntlet_bark`, at (0,1,18), 2 lines) — wave 0's non-blocking bark; the source script defers the full position-triggered Echo bark pool to level-geometry time, and only these 2 lines from its own sample pool are wired (§9):

| Speaker | Line | sec |
|---|---|---|
| Echo | Let the machine and the looters bleed each other. Then walk through. | 5 |
| Echo | Scavenger's running. Let him. He's not the job. | 4 |

**`Dialogue_Beat1_CoreAhead`** (`ch7_beat1_core_ahead`, at (0,1,50), 1 line, ≈13 s):

| Speaker | Line | sec |
|---|---|---|
| Echo | That's the core, Cipher. And there's someone standing in it who isn't running and isn't shooting. She watched you come the whole way in. Heads up. After this it stops being a fight. It's whatever she's been waiting to do. | 13 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point in the gauntlet or the gang-war pocket** — combat feel is carried entirely by `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle, per the non-negotiable VR constraint.
- `ReliquaryAmbience` (`BuildAmbienceLayer`, (-4, 2.6, 65), inner 5 / outer 18 / vol 0.4) is centered at the tended core, not the outer stacks — the gauntlet itself carries no dedicated ambient bed of its own (§9).
- **Proposed torch/sled audio layer (z8-24), not yet built.** Pairs with the outer-stacks ambient-bed gap above and §4c's looting-activity note — cutting-torch sizzle and grav-sled hum on the scavenger spawn positions would give the wave-0 encounter a concrete audible identity ahead of any combat SFX, and reinforce (rather than replace) the still-missing outer-stacks ambient bed.
- **Wave-2 cue proposed, not yet built.** Canon stages the gang-war pocket precisely — "an archive-defense automaton rounds the cross-hall and opens up on the scavenger crew; the looters scatter and fire back. Cipher drops into the gap" — and `FactionCombatant`'s mutual-targeting (§4d) makes it mechanically real, but wave 2's activation has no `AudioDirector` cue distinct from wave 0/1's combat bed. A cross-fire/faction-conflict sting on the gang-war pocket's activation (`EnemyWaveSpawner.StartWave` for wave 2) would let the "play them against each other" beat register on the player's ears, not just in the mechanic — ties to the faction-legibility gap already flagged in §9, on the audio axis.
- Comfort vignette engages normally on player snap-turns through the gauntlet's back-and-forth combat, no beat-specific override.
- 90 FPS is the design target for this encounter (a concurrent peak of **6** live combatants, not more — §4d's sequencing runs the three waves strictly in order, so wave 0's 3 scavengers and wave 1's 2 automata are never alive alongside wave 2's 6 `FactionCombatant`s; the gang-war pocket's mutual targeting is the actual worst case, plus blade VFX and the rack-row gradient's 26 tinted props) — the shipped `QualityBootstrap` default is 72 Hz. This is the first beat most likely to stress the frame budget once high-fidelity rack prefabs land (§1.6).

---

### Beat 2 — The Archivist (The Tended Core: Meeting + Turn)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (no new geometry — this beat plays entirely inside Beat 1's rack-lined hall, at its densest/brightest stretch) and **`BuildBeat2Logic()`** (Coral Vex's placement, the dialogue anchor). **Coral Vex builds nothing physical beyond herself** — do not re-instantiate hall geometry for this beat.

#### a. Narrative purpose & emotional target

Coral Vex stands among the racks with a hand resting on one lit hilt, unstartled, having watched Cipher come the whole way in through the barge's own surviving sensors. She opens with the trust-test's verdict: "You spared the ones who ran. The looters... I had to see that before I let you all the way in... You left witnesses." Ronin-7 cuts straight to the one strange thing about her — the records she keeps and never sells back. Coral lets the cover story stand a beat longer ("Records. Paper. Relics. I let them say it, because the truth scares thieves off better than any lock would") before turning it: "because I knew you'd come eventually. Not you. Someone like you... I've been waiting in this barge for one of you to find the door for longer than you've been alive." Echo clocks it first, off the optic feed alone: "That's no archivist who studied operatives. She is one." The beat's turn lands in four words each: Ronin-7's "You're not a collector of the dead," answered by Coral's "I keep this place so well because it's where I belong. I'm one of the erased, operative. The one who walked out of the dark instead of being filed in it." A silence; the blades hum around them; Cipher's hand has not left his own katana, but it has not lifted it either.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Coral Vex:** placed once, at (0, 0, 65) — see §3's placement decision. `Ch7PlaceStoryNpc(Ch7CoralPrefab, (0,0,65), "Coral Vex")` — `InstantiateNpc` (capsule fallback while `Coral-Vex.prefab` is unbaked, §1.5), `FitNamedCharacter` (grounds feet to world y=0, which already matches this chapter's single flat floor — no re-add needed, unlike Ch6's elevated citadel), then `StoryNpc` with `displayName = "Coral Vex"`. **No `StoryNpcWander`** is added — she is a fixed reverent presence among her own racks, not a wandering NPC.
- **Reveal timing (blocking note, not yet enforced in code).** Coral is active from scene start at z=65, on the hall's spine. With `fogDensity 0.022` (Exponential) and rack-row occlusion, she is intended to read as a distant lit silhouette that resolves into a clear figure only as the player clears the gang-war pocket (z44-50) — the timing `ch7_beat1_core_ahead` (played at z=50) explicitly cues: "She watched you come the whole way in... Heads up." She is deliberately visible-but-unresolved from a distance, not a late spawn; nothing in the builder currently verifies this fog/occlusion read, so it should be checked in-headset rather than assumed.
- **Intended blocking pose (also not yet enforced in code).** Canon's stage direction has her stand "with a hand resting on one lit hilt, unstartled… She is not armed. She does not need to be to be dangerous" — a reverent-keeper contact pose, not a combat-ready or idle one. Nothing in `Ch7PlaceStoryNpc` currently authors a pose or a held/touched prop; this is a placement brief for whichever prefab/animation lands, alongside the facing fix below.
- **Coral's placement straddles the hall's only through-line (blocking note, not yet resolved).** She stands at (0,0,65), dead-center on the spine (`x[-10,10]`, no branching, §2) the player must keep walking north through for the rest of the chapter — past z=88/111 to the deep archive and the oldest blade (§4 Beat 4b), and she never moves (§5). This document covers her *facing* and the reveal-*timing* above, but not that she also physically sits in the player's only path forward. If the eventual `Coral-Vex.prefab`/`InstantiateNpc` capsule carries a collider, she becomes a soft mid-hall obstacle the player squeezes around for every beat from here to the outro; if not, the player simply walks through her model. Recommend either a small +X/−X offset off the spine, or an explicit note accepting that the player routes around a collider-bearing Coral. Not yet resolved.
- **Player:** arrives at the tended core on foot, having crossed `TendedCoreReachPoint` (0,1,58) r=5 at the end of Beat 1 (step 4). No further reach gate exists for this beat — the player is free to walk anywhere near Coral for the dialogue's duration.
- **Dialogue anchor:** `Dialogue_Beat2_Archivist` at (0, 1, 62) — just south of Coral's own position, so the conversation reads as happening a few meters in front of her, not on top of her.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 5 | Dialogue | `Dialogue_Beat2_Archivist` — set `ch7_beat2_archivist`, 9 lines — the meeting, the trust-test verdict, and the turn |

**What changes during the beat:** nothing in the set dressing — this beat plays entirely inside geometry Beat 1 already built (the densest, brightest stretch of the rack row, per §3.1's gradient). The only new object is Coral Vex herself.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Coral Vex | (0,0,65), rot Euler(0,180,0) — faces −Z, toward the player's approach | `Named.CoralVex` | `…/Art/Generated/Characters3D/Named/Coral-Vex.prefab` | **MISSING** *(falls back to `InstantiateNpc`'s capsule)* |
| `TendedCoreLight0` (accent) | (-4,2.6,65) | — | `ChapterEnvironmentProfile.accentLights["TendedCore0"]` | profile |
| `TendedCoreLight1` (accent) | (4,2.6,68) | — | `ChapterEnvironmentProfile.accentLights["TendedCore1"]` | profile |

`TendedCoreLight0` carries `ConsoleFlicker(seed: 77)` (§3.1, §6) — the one deliberate lighting texture at the meeting point, a subtle "patched power" flicker under an otherwise steady, reverent glow.

**A note on the -X bias (blocking note, not yet resolved).** `ReliquaryAmbience` (§4f, centered (-4,2.6,65)) and `TendedCoreLight0` above both sit 4 m west of the spine (x=0) Coral is placed on and the player actually walks; only `TendedCoreLight1` (x=4,z=68) balances from the east, and it is neither centered on Coral nor paired with a matching east-side ambience source. Net effect: the warm light pool is roughly symmetric, but the reverent audio bed is not — it leans west of the meeting the chapter treats as its single most important. Whether the bias is deliberate (Coral tends a specific rack on the west wall) or should be re-centered on (0,2.6,65) is worth a one-line call before `Ch7Environment.asset` is authored (§3.1); marginal, but this is the "shrine you walk into" beat.

**Coral's facing (target state, not yet built).** The row above documents the *intended* rotation — `Euler(0,180,0)`, facing −Z toward the player, who approaches walking +Z — consistent with the stage direction "she turns from the rack and looks at him directly for the first time" and Echo's "she watched you come the whole way in" (`ch7_beat1_core_ahead`). **`Ch7PlaceStoryNpc(Ch7CoralPrefab, (0,0,65), "Coral Vex")` (Chapter7Builder.cs:199, 492–505) takes no rotation argument and instantiates at identity**, so as-built she faces +Z — her back to the player for the entire meeting. This is a real, currently-unfixed gap; see §9.

#### d. Combat

None. Beat 2 is pure dialogue; no enemies are spawned or activated (all outer-stacks combatants belong to Beat 1's already-resolved `DefeatWaves` step).

#### e. Dialogue / VO

Dialogue set id: **`ch7_beat2_archivist`**, built at (0, 1, 62), 9 lines, ≈107 s, advanced on **Left-Hand "Talk" (Y)**:

| Speaker | Line | sec |
|---|---|---|
| Coral Vex | You spared the ones who ran. The looters. You could have cut them down and you let them go. I had to see that before I let you all the way in. A man they sent would have killed everything that moved in here. You left witnesses. | 16 |
| Ronin-7 | They weren't the job. You are. They say you keep the records of dead operatives and never sell them back. | 7 |
| Coral Vex | They say that, do they. Records. Paper. Relics. I let them say it, because the truth scares thieves off better than any lock would. Yes. I keep what's left of the erased. Every operative the Program ever wrote out of the world, I have something of theirs in this hull. I am the only one who does. | 18 |
| Ronin-7 | Why keep them. | 1 |
| Coral Vex | Because somebody has to. Because the Program writes them off as waste and waste is the one thing it never actually gets rid of. And because I know exactly what it costs to be written out of the world. I should know. I've been carrying the cost a long time. | 15 |
| Coral Vex | And because I knew you'd come eventually. Not you. Someone like you. One of the ones after me, with the same walk and the same dead-careful hands and a leash that finally, somehow, slipped. I've been waiting in this barge for one of you to find the door for longer than you've been alive. | 17 |
| Echo | Cipher. Listen to her cadence. The way she stands. The pauses. That's no archivist who studied operatives. She is one. She moves exactly like you. And there's something else, under her voice, the way you hear me. I think she's carrying one too. | 16 |
| Ronin-7 | You're not a collector of the dead. | 3 |
| Coral Vex | I keep this place so well because it's where I belong. I'm one of the erased, operative. The one who walked out of the dark instead of being filed in it. | 14 |

**Note on the trust-test kill-path variant.** Per §4a/§9, only the spare-path warm opening line above is wired. `Chapter7Lines.cs` explicitly documents that authoring a cold-path variant (naming the kills before letting Cipher in) plus the tracking system to select between them is a deliberate scope cut, not an oversight.

#### f. Audio / Haptics / VR Comfort

- No camera shake — a still two-hander, the same deliberate-stillness treatment Ch1 Beat 2's Main Hold and Ch6 Beat 2's Morrigan's Spine use for their own two-hander scenes.
- `ReliquaryAmbience` (centered at (-4,2.6,65), inner 5/outer 18/vol 0.4) is built specifically to cover this beat's position — the reverent, tended-core hum under the conversation.
- `TendedCoreLight0`'s `ConsoleFlicker(seed:77)` is the only visual event during the beat — a small unsteady flicker, not a scripted cue, reading as "patched power" rather than anything dramatic.
- Comfort vignette behaves per the standard posture guard; the player is free to walk during the dialogue.

---

### Beat 3 — What Came Before Him (The Core, Deeper) — Ally #4

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (no new geometry) and **`BuildBeat3Logic()`** (the dialogue anchor only). **This beat builds nothing physical** — Coral Vex is already placed (Beat 2); the "deeper into the core" the screenplay describes is staged entirely through the dialogue anchor's position, not new geometry.

#### a. Narrative purpose & emotional target

Coral leads Cipher among the oldest, dimmest blades she keeps — her own line's — and reveals what she is: **the Wraith line**, the predecessor variant the Ronin line was refined from, presumed scrapped. "We were the first attempt at what you are. The first make they built that could feel." Framing throughout is forebear, never sibling: "You're not family, operative. You're the fix they made after me." The how is the beat's hardest fact — no insider freed her: "I reached in and I tore the switch out myself, with my own hands, while it was still live. It nearly killed me." She kept her own shadow rather than let the Program shelve it, the same bond Cipher has with Echo, mirrored across a generation: "The only two of our kind who ever walked out still carrying the thing that watched us." Ronin-7's certainty about his own singularity comes apart in his hands: "They told me I was the first to slip... I'm not the first of anything." Coral asks in — **Ally #4**, stated on her own terms, not pleading but offering — and immediately turns him toward the worse truth waiting below: "You came here for records. The records aren't paper, Cipher. Come down to the deep archive."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **No new NPC placement.** Coral Vex remains at her Beat 2 position (0,0,65); this beat's dialogue plays as a disembodied `DialoguePlayer` further north, per §3's placement decision — the "leading him among the old racks" staging is narrative color carried by the VO, not a walked blocking move.
- **Dialogue anchor:** `Dialogue_Beat3_Forebear` at (0, 1, 75) — 10 m further into the hall than Beat 2's anchor, reading as "deeper into the core" without new geometry.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 6 | Dialogue | `Dialogue_Beat3_Forebear` — set `ch7_beat3_forebear`, 10 lines — the Wraith-line reveal and Ally #4's recruitment |

**What changes during the beat:** nothing in the world. This is the chapter's second consecutive pure-dialogue beat — no new props, no new lighting event, no combat.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(none — reuses Beat 1/2's rack-lined hall unmodified)* | — | — | — | — |

#### d. Combat

None.

#### e. Dialogue / VO

Dialogue set id: **`ch7_beat3_forebear`**, built at (0, 1, 75), 10 lines, ≈161 s, advanced on **Left-Hand "Talk" (Y)** — the longest dialogue set in the chapter's first half:

| Speaker | Line | sec |
|---|---|---|
| Coral Vex | Look at the old racks. The blades at the back, the dim ones whose hilts I can't keep lit anymore. Those are mine. My run. The Wraith line. You won't have heard the name, they made sure of that. There were colder makes before mine. Knight, then Ninja, old and long retired, tools that were never built to feel anything. We were the first attempt at what you are. The first make they built that could feel. | 17 |
| Ronin-7 | The first attempt. | 2 |
| Coral Vex | The failed one. We felt too much, too soon, the seam opened in too many of us at once. So they scrapped the line and they studied why. Every way the Wraith run broke, they wrote down, and they built the next run not to break the same way. The Ronin line. You. You're what they made once they'd learned from me. You're not family, operative. You're the fix they made after me. | 27 |
| Echo | She's telling the truth, Cipher. The thing behind her eyes is older than me. Same architecture, an earlier draft. It knows me. And I know it. Her shadow knows mine. | 12 |
| Ronin-7 | They scrapped your whole line. How are you standing here. | 4 |
| Coral Vex | Because I didn't wait for anyone to free me. There was no insider for us, no door held open, no slipped leash. When I felt the seam open and knew they'd scrap me for it, I reached in and I tore the switch out myself, with my own hands, while it was still live. It nearly killed me. Then I hollowed out a dead salvager's name, climbed into it, and let the Program file me as scrapped. I've worn a corpse's life ever since. | 32 |
| Coral Vex | And I kept my shadow. They shelve yours when they're done with you. I wouldn't let them have mine. So it's been with me the whole time, the way yours is with you. The only two of our kind who ever walked out still carrying the thing that watched us. | 15 |
| Ronin-7 | They told me I was the first to slip. The flaw that finally opened. You've been out here longer than I've been alive. I'm not the first of anything. | 11 |
| Coral Vex | I've waited in this tomb a lifetime for one of the corrected runs to slip the leash and come find me. I'd half decided I'd die first. And here you are, with my mistake bred out of you and the seam open anyway. So. I'll come with you. Whatever you're hunting, I've been underneath it longer than anyone alive. Let me out of this barge. | 19 |
| Ronin-7 | Everyone we take in lives aboard the Cairn. You'd berth with the rest of the strays. A defected tracker, a rogue engineer, a smuggler, a child. And now whatever you are. | 10 |
| Coral Vex | Whatever I am. I like that better than what they called me. A berth on a dead ship full of the ones who got away. Yes. I'll bring what's portable. Your crew's been saying a name down the comm the whole way in. Cipher. I'll use it, then. But before we leave, you need to see the rest of what I keep. You came here for records. The records aren't paper, Cipher. Come down to the deep archive. Then decide what you're hunting. | 22 |

#### f. Audio / Haptics / VR Comfort

- No camera shake — another still two-hander.
- `ReliquaryAmbience`'s coverage (inner 5/outer 18 centered at z=65) reaches z=75 at reduced volume, consistent with the beat reading as "still inside the tended core, a little further in."
- No new lighting event — the beat trusts the dialogue and the rack gradient already established, not a cue.
- Comfort vignette behaves normally; no beat-specific override.

---

### Beat 4 — The Kept Shadows (The Deep Archive) — Reveal + Mini-Boss

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (the oldest-blade prop, the mindspace's own floor/walls/light) and **`BuildBeat4Logic()`** (the two reach points, the `MemoryDiveController`/entry/exit triggers, the mindspace boss, the `AbilityGranter`, the dialogue chain). **The mindspace root (`Mindspace_PreviousOwner`) must stay `SetActive(false)` until the dive's entry trigger fires** — its child `Enemy`/`Health` never `Awake()`s (and cannot be targeted) until then; no separate "activate the boss" trigger is needed, the cascade from `SetActive(true)` covers it.

#### a. Narrative purpose & emotional target

This is the chapter's central reveal, in two parts. First, the horror: Coral shows Cipher the truth she has guarded — erased operatives aren't deleted, their shadow-AIs are seated in blades and racked, every witness shelved and still awake, the katana writ large and made literal. "The Program never deletes the erased, Cipher. It shelves their swords instead." **This is Ladder E, rung 2** — canon's CONTINUITY NOTES tag this exact reveal as advancing "Ladder E → rung 2 (operatives carry shadow-AIs seated in their blades…)," the chapter's other canon-ladder advance alongside Beat 5a's Ladder A, rung 3 (§4 Beat 5a). Ronin-7's two-word repetition, "Still running," carries the dread the rest of the reveal has to sit inside. Echo's line here is the most exposed it gets in the chapter: "This is what I'd have been, Cipher... They'd have racked the katana with me still in it, on a shelf like these, and left me running in the dark with no eyes to see through." Second, the mechanism: the oldest blade in the hall has gone berserk from decades awake with nothing to witness. Coral can't kill it clean from the outside — only another operative's feed reaches a starving shadow, and gripping it throws Cipher into a mindspace duel against the blade's dead previous owner, "a man who was never allowed to stop," fighting the loop of the last fight he ever had. Victory is the mercy, not a kill; the pacified shadow-AI gifts Echo its accumulated combat-read, unlocking permanent weakpoint-sight. Coral racks the calmed blade "the way you take a sleeping child," and the beat closes on a vow: "We come back. All of them." — seeding Chapter 16's payoff.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Player:** walks from the forebear-reveal anchor (z=75) north to `DeepArchiveReachPoint` (0,1,88) r=6 (step 7), then further to `OldestBladeReachPoint` (0,1,111) r=4 (step 9) — both on-foot, no scripted path.
- **The oldest blade** (art, §c) sits at (0,1.2,113), lit red by `OldestBladeLight`. **Gripping it is a `ReachTrigger` + `Trigger` step, not a bespoke VR grab interaction** — mirrors the project's "no new locomotion/interaction mechanics" precedent from Ch4/Ch6: walk up (step 9's reach), hear the choice play out (step 10's dialogue), then the mission's own Trigger step (11) is what actually activates the dive, standing in for the physical grip.
- **The oldest blade's reveal doesn't land from its own anchor — a blocking gap parallel to Beat 1b's core-beacon note and Beat 2b's fog/occlusion note.** `Dialogue_Beat4_KeptShadows` is anchored at (0,1,92); it is from there, 21 m south of the blade, that Coral's closing line points straight at it: "That one. The oldest blade I keep... its hilt-light cracked and pulsing wrong." But `OldestBladeLight`'s range is only 10 m (Appendix A.1), reaching back to roughly z=103 — short of z=92 — with `fogDensity 0.022` and rack-row occlusion between the anchor and the blade besides. The one deliberate warning-red landmark the whole mini-boss beat hinges on is outside its own light's range from the anchor where its reveal line plays: Coral says "that one" and there is nothing legibly wrong at the end of the corridor yet. Verify in-headset whether the blade still reads as a distinct landmark at 21 m through fog and rack occlusion; if not, lengthen `OldestBladeLight`'s range and/or lean on the proposed agitated pulse (§4c, below) — an irregular pulse is also the cheapest way to make a static light read at distance. See §6, §9.
- **The mindspace** (`Mindspace_PreviousOwner`): a small fractured dreamscape offset far from the main hall at (0,0,250) — still inside `ZoneBounds`' radius (§2) but not reachable by walking; entry is exclusively via the dive. Built fully **`SetActive(false)`** at scene-build time. Contains its own floor/ceiling (14×14), four walls, `MindspaceLight`, and the mini-boss.
- **The arena is deliberately close-quarters, and materially tighter than the real hall.** The hall is 20 m wide (x[-10,10]) and effectively unbounded in length; the mindspace is a 14×14 box (x/z[-7,7]). The rig teleports in at local (0,1,-3) — only ~4 m from `MindspaceWallS` — to fight a moveSpeed-1.4 boss 6 m away at local (0,0,3). This is a real spatial constraint, not an oversight: it reads as "the walls of the dead man's last memory closing in," standing-ground rather than kiting. See §4f for the in-headset comfort/readability implication.
- **`MemoryDiveController`** (`MindspaceDive` GameObject): wires `diveRoot` = `Mindspace_PreviousOwner`, `diveEntryPoint` = `MindspaceEntryPoint` (0,1,247), `diveExitPoint` = `MindspaceExitPoint` (0,1,112) — back in the real hall, right in front of the now-calmed oldest blade, facing Euler(0,180,0) (**−Z — away from the blade at z=113 and the `Dialogue_Beat4_Gift` panel at z=114; see §8.3's regression check**) — `rigRoot` = the player rig's own transform, `flashback` = the `MemoryFlashbackController` living on `Mindspace_PreviousOwner`.
- **`EnterMindspaceTrigger`** and **`ExitMindspaceTrigger`**: both built `SetActive(false)`; each holds only a `dive` reference. `OnEnable` calls `EnterDive()`/`ExitDive()` respectively — the same "inactive-until-a-Trigger-step-activates-it" idiom `NpcWalker` uses elsewhere, so `MissionDirector` never has to know `MemoryDiveController` exists.
- **`EnterDive()`** snapshots the current `RenderSettings` fog/ambient *before* activating `diveRoot` (so `MemoryFlashbackController`'s own `Awake()`-time treatment application, which fires the instant `SetActive(true)` runs, doesn't get captured as the "pre-dive" baseline), then activates the mindspace root, re-applies the flashback treatment, and teleports the rig — an instant `Transform.SetPositionAndRotation` with the rig's `CharacterController` toggled off around the write, never a lerp, so it is comfort-safe by construction (§1.1).
- **`MemoryFlashbackController`** (unmodified defaults — no fields overridden in the builder): `ExponentialSquared` fog, color (0.35, 0.37, 0.42), density 0.045; flat ambient (0.30, 0.30, 0.34) — the same "desaturated, wrong, edges that smear" treatment Ch5's massacre dive uses, achieved entirely through `RenderSettings`, not a post-process volume.
- **`Ch7BuildMindspaceBoss`:** instantiates `The-Previous-Owner` (falls back to `PlaceholderCharacterBuilder`'s already-defined `Ethereal` spec while unbaked, §1.5) at mindspace-local (0,0,3) = world (0,0,253), rotated to face -Z (Euler(0,180,0), toward the entry point at (0,1,247)). `FitNamedCharacter` grounds it to world y=0, which already matches the mindspace floor's height — no re-add needed, unlike Ch6's elevated citadel. A `CapsuleCollider` (center (0,1,0), height 2, radius 0.4) plus a synthesized `ArmR/Sword/Blade/BladeTip` hierarchy (the same technique `Ch6BuildMasterEnemy` uses for Named-mesh masters with no combat rig of their own) let `Enemy` drive it with the standard `BladeDamager`-readable weapon chain.
- **`Dialogue_Beat4_MindspaceIntro` is a scene-active object built at `diveEntryPointGo.transform.position`, not a child of the (normally inactive) mindspace island — a sibling co-located with the entry point.** It sits live at world (0,1,247) from build time onward; mission step 12 is what actually gates when it plays, so this is correct runtime behavior. But because it is a *sibling* of `mindspaceGo`/`diveRoot`, not a child, a future edit that reparents dialogue anchors under their beat's logic root — or that sweeps `world`'s remaining direct children wholesale into a safe zone or logic root (§A.6) — must not accidentally move it under the inactive mindspace island, where `SetActive(false)` would prevent it from ever firing. Same class of caution §A.6 already flags for the wave-0/1 enemies riding as direct children of `world`.
- **`ExitDive()`** deactivates `diveRoot`, teleports the rig to `diveExitPoint`, and restores the exact pre-dive fog/ambient snapshot — "the color flooding back into the real scene."
- **Weakpoint-sight ability grant:** `WeakpointSightGranter` (`AbilityGranter`, `abilityId = AbilityId.WeakpointSight`) is activated by the *same* mission Trigger step as `ExitMindspaceTrigger` (step 14, a dual-target Trigger). `AbilityGranter.OnEnable` calls `Grant()` → `CampaignState.UnlockAbility(...)` immediately — the rig's already-resident (but previously self-disabled, §1.1) `WeakpointSight` component becomes live for the rest of the game from that exact frame.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 7 | ReachTrigger | "ReachTrigger: The Deep Archive" — gates on distance to `DeepArchiveReachPoint` (0,1,88), radius 6 |
| 8 | Dialogue | "Beat4: The Kept Shadows (reveal)" — `Dialogue_Beat4_KeptShadows` |
| 9 | ReachTrigger | "ReachTrigger: The Oldest Blade" — gates on distance to `OldestBladeReachPoint` (0,1,111), radius 4 |
| 10 | Dialogue | "Beat4: Quiet It (the choice)" — `Dialogue_Beat4_QuietIt` |
| 11 | Trigger | "Trigger: Grip the Blade (enter the mindspace)" — activates `EnterMindspaceGo` → `MemoryDiveController.EnterDive()` |
| 12 | Dialogue | "Beat4: The Mindspace Duel Begins" — `Dialogue_Beat4_MindspaceIntro`, played inside the mindspace at the entry point |
| 13 | DefeatEnemies | "Beat4: The Mindspace Duel (mini-boss)" — waits for The Previous Owner's `Health` to reach zero |
| 14 | Trigger | "Trigger: Exit the Mindspace (weakpoint-sight granted)" — activates both `ExitMindspaceGo` (→ `ExitDive()`) and `WeakpointSightGranter` in the same step |
| 15 | Dialogue | "Beat4: The Gift (weakpoint-sight, the vow)" — `Dialogue_Beat4_Gift`, played back in the real hall at the (now calmed) oldest blade |

**What changes during the beat:** the oldest blade's `OldestBladeLight` stays a static warning red throughout (no scripted color-shift on pacification, §9); the real change is entirely off-hall — the mindspace island activates, hosts the fight, and deactivates again, and the rig's `WeakpointSight` component flips from self-disabled to live.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `OldestBlade` prop | (0,1.2,113) | `Props.OldestBlade` | `…/Art/Generated/Props/OldestBlade.prefab` | **MISSING** |
| `OldestBladeLight` (accent, warning red) | (0,2.2,113) | — | `ChapterEnvironmentProfile.accentLights["OldestBlade"]` | profile |
| `DeepArchiveLight0` (accent) | (-4,2.6,100) | — | `ChapterEnvironmentProfile.accentLights["DeepArchive0"]` | profile |
| `DeepArchiveLight1` (accent) | (4,2.6,108) | — | `ChapterEnvironmentProfile.accentLights["DeepArchive1"]` | profile |
| Mindspace floor/ceiling (14×14) | mindspace-local center (0,0,0) = world (0,0,250) | `Rooms.MindspaceShell` | `…/Art/Generated/Rooms/MindspaceShell.prefab` | **MISSING** |
| Mindspace walls ×4 | local ∓7 x / ∓7 z | `Rooms.MindspaceShell` *(shared with floor/ceiling)* | same | **MISSING** |
| `MindspaceLight` (accent) | (0,2.6,0) + mindspace.position = (0,2.6,250) | — | `ChapterEnvironmentProfile.accentLights["Mindspace"]` | profile |
| The Previous Owner | mindspace-local (0,0,3) = world (0,0,253), facing -Z | `Named.ThePreviousOwner` | `…/Art/Generated/Characters3D/Named/The-Previous-Owner.prefab` | **MISSING** *(falls back to `PlaceholderCharacterBuilder`'s Ethereal spec)* |

**Constraints the prefabs must respect.** `OldestBladeLight`'s hard red (0.9, 0.25, 0.2) is the one deliberate departure from the chapter's warm-toward-the-core gradient (§3.1) — a wrongness marker, not a continuation of it; a prefab pass must not accidentally fold it back into the gradient curve. The mindspace shell is deliberately built at the same `RoomH` (3.6 m) as the real-world hall, "a fractured dreamscape built from the dead owner's last memories" per canon, colder and broken but not larger — any geometry fracturing/re-forming the screenplay describes for the mindspace is not currently modeled (a static 14×14 box today, §9). **The Previous Owner swings an invisible blade.** `Ch7BuildMindspaceBoss` (§4b) synthesizes his `ArmR/Sword/Blade/BladeTip` chain as empty GameObjects with no renderer — the same technique `Ch6BuildMasterEnemy` uses purely to give `Enemy`/`BladeDamager` a weapon-transform chain to read. Canon renders him "in full, armed, locked in the loop of a fight that ended decades ago" (dialogue script line 412), and he is the chapter's only boss. The `Named.ThePreviousOwner` prefab commission (or, while it's unbaked, the synthesized rig itself) needs a visible blade mesh on that chain so "armed" reads — the one prop the chapter's central fight is staged around currently makes unarmed swings.

**The oldest blade has no back-wall terminus — it is a freestanding prop mid-corridor, not the end of the line.** Canon: "the oldest sword in the hall, mounted alone on the back wall," "the deep archive, the bottom of the reliquary." As built, `OldestBlade` (0,1.2,113) sits on the open spine with 29 m of hall — including the read bench (z=122) and the outro (z=127–128) — continuing north of it to `WallN` (z=142); §2's descent note only excuses the flat-Y framing, not this. **Proposed fix:** dress a terminal alcove/back-wall recess directly behind the oldest blade (a shallow niche cut into the spine around z=113–116, or a partial cross-wall just past it) so it reads as the bottom-of-the-reliquary terminus canon describes, while the read bench — already off-spine at x=6 (§4 Beat 5c) — reads as a side-corner tucked past that terminus rather than a continuation of the main hall. **This fix must also reconcile with `Ch7BuildRackRow`'s own footprint, not just the read bench.** The rack row's last two pairs sit at z=118 and z=128 (x=∓9, on the spine walls, per §3.1/Appendix A.2's `zEnd: 134` sweep) — both north of the proposed z=113–116 terminus, and both lit, pristine gold-tinted racks. A terminal alcove there with two more lit rack pairs mounted 5–15 m further north still reads as "the hall simply continues," not a tucked side-corner — the read bench is the only off-spine element the terminus proposal currently excuses. Closing this requires either dropping `Ch7BuildRackRow`'s `zEnd` to ~114 (removing the z=118/z=128 pairs) or relocating those two pairs off-spine alongside the read bench, so no lit rack row extends past the terminus once it is built — this ties the terminus fix to §3.1's density-taper proposal instead of leaving the two in tension. Not yet built; see §9.

**The pre-fight blade reads as calm, not berserk — a gap beyond the already-flagged pacification miss.** `OldestBladeLight` is built with `Behaviour: None` — a steady red glow throughout, before *and* after the mindspace duel. Canon's Beat 4 is emphatic pre-fight: "its hilt-light cracked and pulsing wrong, the steel shivering on its brackets… the blade rattles like it wants out… the undertone curdles into something that is almost a scream." The one prop the entire mini-boss hinges on currently reads as an ordinary red accent, not a warning. A prefab/lighting pass should give it a fast, irregular/agitated pulse — distinct from `MindspaceLight`'s slow 6.4s `AmbientPulse` and `TendedCoreLight0`'s `ConsoleFlicker` — as its default pre-pacification state, settling to a calm, even glow once `Health.Died` fires on the mini-boss (§9). **The gap extends past the light to the blade itself.** Canon's "the steel shivering on its brackets… the blade rattles like it wants out" describes the `OldestBlade` prop physically acting, not just its light — nothing represents that today. Because camera shake is banned, this is exactly the sensory substitute the no-shake rule calls for: a localized positional audio rattle plus a subtle micro-jitter on the `OldestBlade` prop (or the `Props.OldestBlade` prefab, once it lands) as its pre-dive state, settling — on the same `Health.Died` listener already proposed for the light, above — once the mini-boss is defeated.

#### d. Combat — the mindspace duel

Player damage output is via `BladeDamager`'s EMA swing-speed model (**existing system — reuse, don't reinvent**). **The Previous Owner** (`Ch7PreviousOwner` definition: maxHealth 220, damage 18, moveSpeed 1.4, attackCooldown 0.8s) is a plain `Enemy`, synthesized with an `ArmR/Sword/Blade/BladeTip` rig (§4b) — the chapter's mini-boss and its only single-target boss fight. **No spare condition exists** — canon: "the victory itself is the mercy," so this is the one enemy in the chapter with no duel-yield/mercy mechanic, consistent with the encounter's own thesis (a witness left awake too long, looping its last fight, freed only by being beaten).

Because the whole `Mindspace_PreviousOwner` root starts `SetActive(false)`, the boss's `Enemy`/`Health` components never run their own `Awake()` (and cannot be found/targeted by anything, including `Health.Active`'s static registry) until the dive's entry trigger activates the root — no separate "arm the boss" step is needed; the cascade from `MemoryDiveController.EnterDive()`'s `diveRoot.SetActive(true)` covers it entirely.

**Open question: the boss is live during his own intro line.** `diveRoot.SetActive(true)` (step 11) cascades the boss's `Enemy.Awake()` immediately, but `Dialogue_Beat4_MindspaceIntro` ("Another one. They keep sending you…") is step 12 and `DefeatEnemies` is step 13 — so The Previous Owner is already free to pursue and swing while delivering his own line, though canon stages it as "he speaks, *then* control passes to the player." The explanation above covers why the boss can't be pre-targeted *before* dive entry; it does not address this overlap *after* entry. Verify whether the boss should hold (or be briefly frozen) until the intro dialogue completes, or accept the overlap as-is. **The cheapest concrete lever, if a fix is wanted:** disable The Previous Owner's `Enemy` component (or hold it at zero move/attack speed) the instant `diveRoot.SetActive(true)` runs, and re-enable it on `Dialogue_Beat4_MindspaceIntro`'s completion callback — mirroring how other beats in this chapter already gate state changes on dialogue-complete, rather than leaving this as an open verify-or-accept.

#### e. Dialogue / VO

Four sets, all advanced on **Left-Hand "Talk" (Y)**:

**`Dialogue_Beat4_KeptShadows`** (at (0,1,92), 5 lines, ≈86 s):

| Speaker | Line | sec |
|---|---|---|
| Coral Vex | You think these are files. Records. Paper. Look closer. They're blades, every one of them, and a shadow lives in each. The watcher they seat in the steel and bond behind an operative's eyes, the same thing your katana carries, kept after the body's gone. The Program never deletes the erased, Cipher. It shelves their swords instead. Every blade on these racks is somebody's witness, powered and still running, decades on. | 21 |
| Ronin-7 | Still running. | 2 |
| Coral Vex | Awake. Some of them. In the dark, in the steel. No host, no feed, no voice anyone bothered to wire up. Just the watching, racked year after year, because the Program never wastes an asset and a witness is an asset even when there's nothing left to witness. Your sword isn't rare, Cipher. It's one of thousands. A sample. They kept the blade of every single one they ever killed, and the shadow still living in it. | 28 |
| Echo | This is what I'd have been, Cipher. If you'd died on Velorum, or in the bay, or any of the times. They'd have racked the katana with me still in it, on a shelf like these, and left me running in the dark with no eyes to see through. I'm hearing them. They know I got out. They want to know how. | 17 |
| Coral Vex | That one. The oldest blade I keep. It's been breaking for years. A witness left awake too long with nothing to witness goes wrong, the way anything would, and that one's the longest awake of all of them. It can't tell a living thing from the dark anymore. It only knows it wants a host, and it has forgotten how to take one without tearing the host apart. | 18 |

**`Dialogue_Beat4_QuietIt`** (at (0,1,111), 4 lines, ≈49 s):

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | Then how do we quiet it? | 2 |
| Coral Vex | You let it have you. A shadow that far gone can't be talked down and can't be killed clean, not from the outside. The only thing that reaches it is a feed. A host. Pick it up, open your eyes to it, and let it bond the way the katana bonded to you. A civilian's hands are dead steel to it. But yours aren't. You're an operative. To a starving shadow you look like home. It'll take the feed in a heartbeat. And then it'll fight you for the right to keep it, because there's a dead man still printed in there who thinks it's his. Beat him, and the blade is quiet. Lose, and it wears you the way it wore him. | 28 |
| Echo | She's right, and I hate that she's right. I can feel it from here, Cipher. It's me with the lights off and the door welded shut. If you take it, I'll be in there with you, but the dead man's the one holding the ground, and he's been holding it for decades. Don't go easy on him. The kindest thing you can do for that blade is win fast. | 17 |
| Ronin-7 | Then I won't go easy. | 2 |

**`Dialogue_Beat4_MindspaceIntro`** (at `MindspaceEntryPoint` = (0,1,247), 2 lines, ≈16 s):

| Speaker | Line | sec |
|---|---|---|
| The Previous Owner | Another one. They keep sending you, and I keep putting you down, and the dark always comes back. You're not taking my blade. | 8 |
| Echo | Stay with me, Cipher. Don't owe that memory anything. It's just the wall keeping the blade asleep. Take it down. | 8 |

**`Dialogue_Beat4_Gift`** (at (0,1,114), 7 lines, ≈74 s):

| Speaker | Line | sec |
|---|---|---|
| The Previous Owner | It's quiet now. Thank you. Take what it knows. It was the only thing I had left to give anyone. | 6 |
| Echo | It just handed me everything it learned watching its operative kill, Cipher, decades of it, every fight read down to where a body breaks. I can run that now. Where they're weak, you'll see it. Look at anything and I'll show you the seam. Call it a parting gift. It died owing the dark, and it paid us instead. | 18 |
| Coral Vex | I've never had the nerve to do that. Or the right host to risk it on. You quieted the oldest grief in this hull, and it gave you its eyes on the way out. Keep them. You'll need them. Don't keep the blade, though. It's earned its rest. Leave it with me. | 14 |
| Ronin-7 | How many. | 1 |
| Coral Vex | I've never finished counting. This barge holds a fraction. The Program has vaults of them, somewhere, every operative it ever erased, its blade shelved and the shadow in it awake. I've spent a lifetime freeing the ones I could reach, one blade at a time, and I've barely touched it. That's the work, Cipher. That's what I keep. A debt I'll die owing. | 20 |
| Echo | Cipher. Whatever we do after this, we come back for them. All of them. I'm not leaving a hall of my own kind running in the dark and calling it someone else's problem. Promise me that much. | 12 |
| Ronin-7 | We come back. All of them. | 3 |

**Note:** `The Previous Owner`'s two lines are the only lines in the chapter spoken by a character who exists solely inside the mindspace and never returns — his dialogue clips are named through the same `Chapter7Lines.ClipName` pattern as everyone else, sanitized speaker `"thepreviousowner"` *(verify exact `Sanitize("The Previous Owner")` output against the live build before generating VO — see §9)*.

#### f. Audio / Haptics / VR Comfort

- **No camera shake anywhere in this beat**, including the mindspace's entry/exit teleports — those are instant position/rotation writes with the `CharacterController` toggled off around them, not camera motion, and comfort-safe by construction (§1.1, §4b).
- **No haptic or audio sting is authored for the grip/whiteout itself.** Canon's most visceral moment — "Cipher closes his hand on the berserk sword. The hilt-light flares, the optic feed whites out, and the deep archive drops away" — currently has nothing selling it beyond the fog/ambient `RenderSettings` swap; `ExitDive()`'s "color flooding back into the real scene" is equally unscored. Because camera shake is banned, this is exactly the sensory substitute the no-shake rule requires: a strong `Haptics` pulse + `AudioDirector` bond/whiteout sting on `EnterDive()`, and a gentler settle cue on `ExitDive()`. **That settle cue should read as the loop breaking, not just a scene transition** — canon stages a distinct mercy beat before the whiteout that the build currently collapses into a single frame: "the owner falls to one knee, the loop broken at last… the dreamspace stills… he looks at Cipher with something like relief, and lets go," and only then does the mindspace dissolve, but the as-built mission spine runs `DefeatEnemies` (step 13) straight into `ExitDive()` (step 14) with nothing marking the gap between them. A brief hold or quieting of the duel's stinger/chorus on `Health.Died`, ahead of the exit teleport, would give the encounter's own thesis — "the victory itself is the mercy," not a kill — a diegetic beat instead of one frame. Not yet built.
- **`MindspaceDreadAmbience` is built at the wrong coordinates — a confirmed as-built bug, not an atmosphere gap.** `Chapter7Builder.cs:379` calls `BuildAmbienceLayer("MindspaceDreadAmbience", new Vector3(0f, 2.6f, 0f), 4f, 14f, 0.4f)` — unlike `MindspaceLight` two lines earlier (`Chapter7Builder.cs:230`), which is correctly offset `+ mindspace.position`, this ambience layer's position is **not** offset by `mindspace.position` (z=250) and it is not parented to `mindspaceGo`. As built it is a standalone, always-active `AudioSource` sitting at **world** (0, 2.6, 0) — i.e. at the breach/player-spawn end of the real hall, not inside the mindspace. `ProximityAmbienceLayer` drives volume by listener distance (outer radius 14 m), so this "dread bed" is audible during **Beat 1's breach** (player at z≈2–14, well within 14 m) and **silent during the actual mindspace duel** (player teleported to z≈247–253, ~247–253 m from the bed → volume 0). The intent is exactly inverted: the fix is to build it at `(0, 2.6, 0) + mindspace.position` = `(0, 2.6, 250)` (or parent it to `mindspaceGo` so it activates/deactivates with the root), matching `MindspaceLight`'s own pattern. See §9.
- **`MindspaceLight`'s `AmbientPulse` is a confirmed no-op — the component is never actually attached.** `AddAmbientPulse` (`ChapterSharedBuilders.cs:267`) resolves its target via `GameObject.Find("MindspaceLight")`, which does **not** return inactive objects. `MindspaceLight` is a child of `mindspaceGo`, and `mindspaceGo.SetActive(false)` fires at `Chapter7Builder.cs:237` — well before the `AddAmbientPulse("MindspaceLight", periodSeconds: 6.4f)` call at `Chapter7Builder.cs:382`. By the time `AddAmbientPulse` runs, `Find` returns null, the method early-returns, and the `AmbientLightPulse` component is silently never added. The mindspace's one dynamic lighting cue does not exist as built — the duel plays under a dead-steady light, not the "slow, uneasy breathing light" this document (and §3.1, §6, Appendix A.1) describes. Fix: attach the pulse before `mindspaceGo.SetActive(false)` runs, or have `AddAmbientPulse` search with `includeInactive`. See §9. (`AddConsoleFlicker("TendedCoreLight0")` is unaffected — that light is root-level and active, so `Find` resolves it fine.)
- **The fog/ambient swap on dive entry/exit is instantaneous** (a same-frame `RenderSettings` write, not a fade) — flagged in §9 as an open comfort question, the same one Ch5's massacre dive already carries unresolved: whether an instant full-scene color/fog swap during an otherwise-comfort-safe teleport warrants a `ScreenFader` blink.
- Combat feel in the duel is carried entirely by `Haptics`/`AudioDirector`/`CombatFeedbackController`'s reticle, same as every other fight in the chapter — **but the tone should not be.** Canon is explicit that "the encounter audio reads as grief, not triumph… the kept shadows feeling one of their own resist," and that the archive's undertone/chorus (§7) should "go briefly, terribly clear" during this fight specifically. `AudioDirector` should carry a tonally distinct, mournful/low stinger set for The Previous Owner duel rather than the generic gauntlet bed — not yet built; ties directly to the unbuilt chorus (§7, §9).
- **A distinct pre-grip chorus spike, proposed and not yet built.** Beyond the general dense-rack rise and the duel's own clarifying (above), canon localizes a sharper chorus event specifically at the oldest blade, just ahead of the grip: "the undertone curdles into something that is almost a scream" as Cipher nears it. This belongs to the approach, gated on proximity to `OldestBladeReachPoint`/`OldestBlade` (z=111/113), not the duel itself — a proximity-gated chorus spike here would give the pre-grip dread a concrete anchor rather than folding it into the general chorus rise. Ties to the unbuilt chorus (§7, §9).
- **The permanent control this grant adds is never named in this section.** Per the builder's class summary (`Chapter7Builder.cs` lines 78–79), `WeakpointSight` is "a togglable focus mode (left controller X)" that goes live from the moment `AbilityGranter.Grant()` fires — the one permanent control Chapter 7 adds to the player's input set for the rest of the game, and this document never states the binding anywhere in §4 or §5. **Left-controller X toggles the focus overlay once granted.**
- **The weakpoint-sight grant itself has no audio/haptic sting authored, and canon's first-activation image cannot render through the shipped ability even if a sting were added.** `AbilityGranter.Grant()` is a silent state change; the reveal is carried entirely by the following dialogue (`Dialogue_Beat4_Gift`), not a UI/audio cue on unlock. But the dialogue script's own ON-THE-BREAK stage direction (line 426) is specific and cinematic about what the grant should look like: "a new layer threads into Cipher's vision, faint geometry laid over the racks, the world, Coral herself, lines and points the way a shadow reads a target." `WeakpointSight` only ever paints live `Health`s pulled from the `Health.Active` registry (`WeakpointSight.cs`); at the Gift moment the mini-boss is already dead and the deep archive holds no other live combatant (Coral's placeholder carries no `Health` of her own), so there is structurally **nothing for the overlay to paint** — the racks, the world, and Coral herself cannot be swept by the ability as shipped. This is the chapter's single mechanical reward and its most cinematic beat, and today it is both a silent state flip *and* unable to show the image canon describes for its own birth. Two ways to close it: **(a)** accept the reveal as VO-only, with the overlay's first real activation deferred to whenever the player next meets a live enemy; or **(b)** author a one-shot cosmetic overlay pulse — the weakpoint "seam" grammar swept once over the deep-archive racks and Coral, driven directly off `AbilityGranter.Grant()` rather than `WeakpointSight`'s normal enemy-only scan — so the moment canon treats as the ability's birth actually reads as one. Not yet decided; see §9.
- **Flag for in-headset check: the close-quarters arena (§4b) under the flashback fog.** `MemoryFlashbackController`'s density-0.045 `ExponentialSquared` fog makes the already-tight 14×14 box (vs. the 20 m-wide real hall) harder to read at range; with continuous locomotion, no camera shake, and no minimap/compass in a mindspace, a player retreating during the duel can back into an unseen wall within a couple of steps. The arena is designed for standing ground against the boss rather than kiting, per §4b — worth confirming that read holds once the fog is felt in headset, not just reasoned about on paper.

---

### Beat 5 — Sabotage Was Dissent (The Read Bench) — Reveal + Hook Out

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (the read bench + its blade rack, the complete canvas) and **`BuildBeat5Logic()`** (the dialogue chain, `ChapterOutro`).

#### a. Narrative purpose & emotional target

Coral turns her hard-won expertise on the one thing Cipher actually came for: his switch. She is the only person alive who knows, from the inside, what a leash looks like when it comes off on purpose versus when someone else does the cutting — "I tore one of these out of my own skull with no help and no warning... Yours is the second kind." Morrigan confirms from the Cairn in parallel, the two engineers of the leash — the one who escaped one and the one who builds them — reading the same wound from two ends. The reveal lands whole and precise: "Someone on the inside had the keys and chose to use them on you... The thing you've been running from isn't a wall. It's got a crack in it." **This is Ladder A, rung 3** — sabotage confirmed as internal dissent, a faction inside the Program deliberately breaking leashes. Morrigan's closing line ties it back to Chapter 6's own unresolved thread: "I read a hand outside the Program on the mountain. Vex just read a hand inside it here. Those aren't the same machine, Cipher. There's more than one will pulling at you." **The saboteur is not named** — that identity lands in Chapter 13 (Dr. Heris). The hook out redirects the hunt: Coral cannot point to where the dissenters scatter, but she names an old legend, the **Silent Garden**, a dead-signal world said to show one seeker one true thing — and sends Cipher there.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **No new NPC placement.** Coral Vex remains at (0,0,65); this beat's dialogue plays as a disembodied `DialoguePlayer` at the read bench, further north still, per §3's placement decision.
- **Dialogue anchors:** `Dialogue_Beat5_Sabotage` at (6,1,123), `Dialogue_Beat5_Hookout` at (6,1,125) — both off-center on the +X side, at the `ReadBench`'s own position (§c), the one prop in the chapter not centered on the hall's spine.
- **`ChapterOutro`:** at (0,1,127), built inactive. `CampaignFlagSetter` wired to `OnActivated` sets **two flags in the same step**: `"ch7_complete"` and `"coral_vex_recruited"` — combining the ally-recruit flag with the chapter-completion flag at the same trigger, mirroring how the chapter finales that recruit an ally do it elsewhere.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 16 | Dialogue | "Beat5: Sabotage Was Dissent (the read bench)" — `Dialogue_Beat5_Sabotage` |
| 17 | Dialogue | "Beat5: The Silent Garden (hook out)" — `Dialogue_Beat5_Hookout` |
| 18 | Trigger | "Trigger: Chapter Outro (flags + fade + canvas)" — activates `ChapterOutro` |

Step 18 both ends Beat 5 and ends Chapter 7: `ChapterOutro.OnEnable` invokes its wired `onActivated` (a persistent-listener call into `CampaignFlagSetter.SetFlags`, setting `ch7_complete` and `coral_vex_recruited` together), reveals the complete canvas, and fades — the same signal `GameFlowManager`'s normal mission-complete handling listens for elsewhere in the game.

**What changes during the beat:** nothing in the set dressing — the read bench and its rack are static geometry built once. The only state change is the mission director stepping through the closing dialogue and firing the outro.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `ReadBench` | (6,0.5,122) | `Props.ReadBench` | `…/Art/Generated/Props/ReadBench.prefab` | **MISSING** |
| `ReadBench_Rack` | (6,1.1,123.5) | `Props.ReadBenchRack` | `…/Art/Generated/Props/ReadBenchRack.prefab` | **MISSING** |
| `CHAPTER 7 COMPLETE` canvas | worldspace, (0,1.4,128), rotation Euler(0,180,0) (faces -Z toward the player) | — | inline UI, not a registry-driven asset | built |

**The outro has no physical anchor of its own.** The chapter's closing image — the proposed `Props.BreachPod` (§4 Beat 1c) carrying Cipher and Coral "off the reliquary and back across the debris field" — sits 130 m south, back at the breach, not near `ChapterOutro`/the complete canvas here at z=127–128. The fade-to-complete at the read bench is an implicit hand-off to that off-screen return trip, the same narrative ellipsis every other chapter's outro already uses for the walk back to the ship — not a gap that needs new geometry at this end of the hall, but worth naming so the pod (once built) isn't expected to double as set-dressing at both ends.

#### d. Combat

None.

#### e. Dialogue / VO

**`Dialogue_Beat5_Sabotage`** (at (6,1,123), 6 lines, ≈106 s):

| Speaker | Line | sec |
|---|---|---|
| Coral Vex | Now let me look at the thing you actually came for. Your switch. Hold still. I tore one of these out of my own skull with no help and no warning, which means I'm the one person alive who knows what it looks like when a leash comes off on purpose, and what it looks like when someone else does the cutting. Yours is the second kind. I can see it already. | 21 |
| Morrigan | Confirming from this end. I flagged it the day you came aboard. The blocks were touched in build order, by someone fluent in firmware they had no business being fluent in. I called it an insider. Vex, you've cut one of these by hand. Tell me what I can't see from a schematic. | 17 |
| Coral Vex | What you can't see from a schematic is the hand. I can. When I cut my own, I was inside it, frantic, no time, no finesse. It was butchery that happened to work. This was calm. Unhurried. Someone with all the time in the world and full authority over the system, reaching in and opening one door with the care of a person who builds these for a living and has decided, just this once, not to. | 31 |
| Ronin-7 | One door. Mine. | 2 |
| Coral Vex | Yours. Deliberately. By someone on the inside who had the keys and chose to use them on you. The Program builds these to never fail, Cipher, and this one didn't fail on its own. Someone on the inside made it fail. Which means the thing you've been running from isn't a wall. It's got a crack in it. Someone behind that wall is breaking leashes from the inside. | 21 |
| Morrigan | That fits the thing I couldn't make fit. I read a hand outside the Program on the mountain. Vex just read a hand inside it here. Those aren't the same machine, Cipher. There's more than one will pulling at you. | 14 |

**`Dialogue_Beat5_Hookout`** (at (6,1,125), 5 lines, ≈81 s):

| Speaker | Line | sec |
|---|---|---|
| Echo | Someone inside the Program risked everything to let you out, Cipher. That's the first time anyone's said it and meant it. You didn't just slip the leash, Cipher. Somebody reached in and chose to free you. There's a hand in that machine on our side. | 14 |
| Ronin-7 | Then the hunt changes. We've been looking for the others like me. Now we look for the ones inside who break the leashes. Where do they go, Coral. People inside the Program who've stopped believing in it. Where do they run. | 14 |
| Coral Vex | They don't run loud. A leash-breaker can't go anywhere a leash can reach. So they scatter where the channels are dead, and I can't point you to them. But I can point you somewhere better. There's a place I've heard of my whole life and never let myself believe in. A dead-signal world, off every chart. The story goes that something keeps it, older than the Program, older than any of us, and that it will show one seeker one true thing they were never meant to see. They call it the Silent Garden. I've never let myself go looking. But I've spent a lifetime reading the dead lanes, and I know roughly where it sits. You want the hand that cut your leash? Don't chase the dissenters into the dark. Go let the Garden show you. | 27 |
| Ronin-7 | They didn't even let them die. And one of their own couldn't stomach it and opened my door. So we find that one. We find the quiet place. And we start pulling the machine apart from the crack they left us. | 13 |
| Echo | A whole machine, Cipher, with a crack running through it. We came down to ask the dead a question, and they handed us a living one. Down the quiet channels next. The Silent Garden. | 13 |

#### f. Audio / Haptics / VR Comfort

- No camera shake — the chapter's final beat is dialogue-only.
- `ReliquaryAmbience`'s coverage does not extend this far north (centered z=65, outer radius 18 → reaches to z≈83) — this beat carries **no dedicated ambient bed of its own** (§9).
- Comfort vignette behaves normally through any final repositioning; no beat-specific override.
- `ChapterOutro`'s fade (`ScreenFader`, `fadeDelay`/`fadeDuration` per the shared `ChapterOutro` component's own defaults — see Ch1/Ch6 precedent) is the chapter's final visual event, closing on the two flags set together.

## 5. Character travel-route master table

Chapter 7 has **no walked NPC travel at all** — a first among the chapters documented so far. Every named character is either a fixed placement, a voice-only comm presence, or a mission-scripted teleport (the mindspace dive), never a walked `NpcWalker` leg.

| Character | Travel | Position(s) | Activated by |
|---|---|---|---|
| **Coral Vex** | None — fixed in place for the entire chapter after her one placement | spawn = final position, (0, 0, 65) | n/a — no `StoryNpcWander`, no `NpcWalker` |
| **The Previous Owner** (mindspace boss) | None — fixed at mindspace-local (0,0,3) = world (0,0,253) until its `Health` reaches zero | (0,0,253), facing -Z | n/a — combat AI (`Enemy`), not `NpcWalker`-driven |
| **Ronin-7 (player)** | Continuous locomotion + snap-turn through the entire real-world hall, **plus two instant `MemoryDiveController.TeleportRig` calls** (dive entry (0,1,247), dive exit (0,1,112)) | spawn (0,0,2) → the full hall → (mindspace round-trip) → the outro | player input (real world); mission Trigger steps 11/14 (the two teleports) |
| **Resh / Mera Voss / Iris / Kessler / Mira / Morrigan** | None — never physically present, voice-only via `DialoguePlayer` labels (Morrigan appears in Beat 0 *and* Beat 5, still never physically) | n/a | n/a |

**No Y-invariant applies this chapter in the Kessler/Chapter-1 sense** — there is no `FitNamedCharacter`-then-recompensate pattern to guard, because the entire real-world hall sits at a single flat world-Y=0 floor (no elevated upper citadel the way Chapter 6 has) and Coral Vex/The Previous Owner are each placed once and never travel. The one invariant that *does* matter is `MemoryDiveController`'s teleport pair: `diveEntryPoint`/`diveExitPoint` must each carry a valid rotation as well as position (the rig's forward-facing snap on arrival), and `ZoneBounds`' radius (155 m from center (0,3,128)) must keep covering the offset mindspace island — see §2's note on this exact distance check. Any future patch that moves the mindspace further from (0,0,250) must re-verify that distance against `ZoneBounds.radius`. **The entry half of the teleport pair is confirmed correct**, for symmetry with §8.3's exit-rotation regression check below: `MindspaceEntryPoint` is built at `Quaternion.identity` (`Chapter7Builder.cs:233`) — +Z, which faces the boss at local z=+3 / world z=253 from the entry at z=247 — so only the exit rotation is at issue.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Zone | Mood | Accent entry | Behaviour | What changes |
|---|---|---|---|---|
| Breach/Spawn (Beat 1 start) | cold, frost-blue, low intensity | `accentLights["Spawn"]` | `None` | none — static from spawn |
| Outer Stacks Gauntlet (Beat 1) | same cold blue, rack gradient at its dimmest | `accentLights["OuterStacks"]` | `None` | wave 0 → wave 1 → wave 2 activate in sequence as the player advances; no lighting event tied to any wave |
| Tended Core (Beats 2–3) | warm gold, patched-power reverence | `accentLights["TendedCore0"]`, `accentLights["TendedCore1"]` | `TendedCore0`: `ConsoleFlicker(seed 77)`; `TendedCore1`: `None` | none — a still two-hander across both beats, same deliberate stillness Ch1 Beat 2/Ch6 Beat 2 use |
| Deep Archive (Beat 4) | warmer still, densest/brightest rack stretch | `accentLights["DeepArchive0"]`, `accentLights["DeepArchive1"]` | `None` (both) | the kept-shadows reveal plays against a static baseline — the one visual event is the oldest blade's warning red, not a change here |
| The Oldest Blade (Beat 4) | hard warning red, the one deliberate break from the warm gradient | `accentLights["OldestBlade"]` | `None` *(proposed: fast/irregular agitated pulse pre-duel, settling to a calm steady glow on `Health.Died` — §9)* | **Trigger (step 11):** the mindspace activates (off-hall, invisible from here); **Trigger (step 14):** the mindspace deactivates and the rig returns — the light itself never changes: it does not read as agitated/wrong before the duel, nor does it calm afterward (§9). The `WeakpointSightGranter` firing on the same step 14 also produces no light/VFX event here — see §4 Beat 4f's weakpoint-sight overlay gap |
| Mindspace (Beat 4, dive-only) | cold, unstable, dreamlike | `accentLights["Mindspace"]` | `AmbientPulse(6.4s)` **(confirmed no-op as-built — §4 Beat 4f, §9: `GameObject.Find` can't see `MindspaceLight` because `mindspaceGo` is already inactive when `AddAmbientPulse` runs, so the `AmbientLightPulse` component is never attached and the light stays dead-steady)** | fully inactive except during the dive; `MemoryFlashbackController` overrides fog/ambient to `ExponentialSquared` (0.35,0.37,0.42) density 0.045, flat ambient (0.30,0.30,0.34) on entry, restores the real world's pre-dive values on exit |
| Read Bench (Beat 5) | same warm gold as the tended core, no new accent | *(reuses `DeepArchive1`'s reach — no dedicated light)* | — | none — the chapter's closing beats trust the established gradient rather than a new cue |

**Lighting gap, unresolved.** `DeepArchiveLight1` (z=108, range 18) reaches only to z≈126 — "reuses `DeepArchive1`'s reach" holds for `ReadBench` itself (z=122/123.5) but not for `ChapterOutro` (z=127) or the `CHAPTER 7 COMPLETE` canvas (z=128), which sit past the light's falloff and are lit only by the 0.35-intensity directional key plus fog. If this is an intentional fade-to-black outro, it should be stated as such; otherwise the chapter's final beats — the sabotage reveal and the hook-out — play in near-dark. Not yet resolved; see §9.

**Also unresolved: the oldest blade's own reveal doesn't reach its anchor.** `OldestBladeLight`'s 10 m range does not reach `Dialogue_Beat4_KeptShadows`' anchor at z=92, ~21 m south of the blade (z=113) — Coral's "that one" line points at a landmark that is still outside its own light's falloff when it plays. See §4 Beat 4b, §9.

**Also unresolved: the read bench itself has no prop-specific light.** `ReadBench`/`ReadBench_Rack` (z=122/123.5) sit within `DeepArchiveLight1`'s falloff (same light the gap above concerns) but get no dedicated accent of their own — Beat 5's switch-analysis scene, canon's deep-archive twin of Morrigan's hot analysis bench in Beat 0 (dialogue script line 475: "jury-built Program hardware wired into a stable rack of coherent blades"), plays under generic hall light with nothing marking it as a working instrument. A small cool console/screen accent on the bench (proposed `ReadBenchLight`, Appendix A.1) would light Beat 5's most intimate reveal, give the prop a working-console identity rather than a cube, and visually rhyme the read bench with Morrigan's bench — "two engineers of the leash reading the same wound from two ends," the framing Beat 5a's dialogue is built on.

Fog is the same baseline cold, frost-blue exponential bed across the entire real-world hall — a single profile value, never overridden per-zone — except inside the mindspace, where `MemoryFlashbackController` temporarily overrides it entirely for the duration of the dive.

## 7. Audio / VO manifest cross-reference

Twelve canonical dialogue sets, defined in `Chapter7Lines.cs` and consumed via `Chapter7Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position | Lines | ≈sec |
|---|---|---|---|---|
| `ch7_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` | 13 | 162 |
| `ch7_beat1_breach` | 1 | (0, 1, 8) — `Dialogue_Beat1_Breach` | 4 | 51 |
| `ch7_beat1_gauntlet_bark` | 1 | (0, 1, 18) — `Dialogue_Beat1_GauntletBark` | 2 | 9 |
| `ch7_beat1_core_ahead` | 1 | (0, 1, 50) — `Dialogue_Beat1_CoreAhead` | 1 | 13 |
| `ch7_beat2_archivist` | 2 | (0, 1, 62) — `Dialogue_Beat2_Archivist` | 9 | 107 |
| `ch7_beat3_forebear` | 3 | (0, 1, 75) — `Dialogue_Beat3_Forebear` | 10 | 161 |
| `ch7_beat4_kept_shadows` | 4 | (0, 1, 92) — `Dialogue_Beat4_KeptShadows` | 5 | 86 |
| `ch7_beat4_quiet_it` | 4 | (0, 1, 111) — `Dialogue_Beat4_QuietIt` | 4 | 49 |
| `ch7_beat4_mindspace_intro` | 4 | `MindspaceEntryPoint` (0,1,247) — `Dialogue_Beat4_MindspaceIntro` | 2 | 16 |
| `ch7_beat4_gift` | 4 | (0, 1, 114) — `Dialogue_Beat4_Gift` | 7 | 74 |
| `ch7_beat5_sabotage` | 5 | (6, 1, 123) — `Dialogue_Beat5_Sabotage` | 6 | 106 |
| `ch7_beat5_hookout` | 5 | (6, 1, 125) — `Dialogue_Beat5_Hookout` | 5 | 81 |

**Chapter total: 12 sets, ~915 s (~15.3 min) of authored VO across 68 lines** (summed from the per-set totals above). Use this as the target denominator when sanity-checking a TTS batch against §8's clip-resolution console check.

Each is built by the local `Ch7BuildDialogue` wrapper (mirrors Chapter 1's `BuildChapter1Dialogue`, Chapter 5/6's equivalents): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch7WireVoiceClips`, resolving each line's `AudioClip` from `Chapter7Lines.ClipName(setId, index, speaker)` — pattern `ch7_{setId}_{index:00}_{speaker_sanitized}` (note `setId` already carries the `ch7_` prefix in `Chapter7Lines.SetIds`, so the resolved clip filename literally doubles the prefix, e.g. `ch7_ch7_beat0_briefing_00_morrigan` — the same shape every other chapter's clip-naming already produces, not a Ch7-specific quirk) — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all twelve `DialoguePlayer`s. There is no `PromptInputAdvancer`/release-prompt this chapter — no equivalent gated player-input moment exists (the gauntlet is gated by combat, the dive by mission-step Triggers, not a single button prompt).

**Dialogue is data, not art.** None of this changes in the refactor — the twelve set ids, their positions, and the clip-resolution pattern are canon.

**The undertone (the chorus) is the chapter's single most load-bearing recurring sound cue and is currently unbuilt.** Per the dialogue script's INTRUDING/RECURRING ELEMENTS block: "a constant low layer of shelved shadow-AIs, too quiet to make out, never silent. It rises when Cipher nears a dense rack and goes briefly, terribly clear during the mini-boss and the rescues." No `AudioSource`/ambient layer in the as-built scene carries this — `ReliquaryAmbience` and `MindspaceDreadAmbience` are generic room-tone beds (§below), not the specific "chorus of shelved shadows" cue the script treats as the chapter's pulse. Flagged for the art/audio pass; see §9. Canon also localizes a distinct pre-grip spike within this same cue, not just the general dense-rack rise: as Cipher nears the oldest blade, "the undertone curdles into something that is almost a scream" — a concrete proximity-gated event (near `OldestBladeReachPoint`/`OldestBlade`, z=111/113) distinct from the mini-boss's own "briefly, terribly clear" clarifying, and the chorus commission's single most concrete anchor point. See §4 Beat 4f.

SFX/ambience bed, all under `Assets/Ronin7/Art/Generated/Audio` (or procedurally generated):

| Clip / source | Used for |
|---|---|
| `ReliquaryAmbience` (`BuildAmbienceLayer`) | Beats 2/3's tended-core interior bed — inner 5 / outer 18 / vol 0.4, centered (-4,2.6,65) |
| `MindspaceDreadAmbience` (`BuildAmbienceLayer`) | **Intended** as Beat 4's mindspace interior bed — inner 4 / outer 14 / vol 0.4 — but **as-built, centered at world (0,2.6,0), not mindspace-local (0,2.6,0)+mindspace.position.** Confirmed as-built bug: audible during Beat 1's breach (player at z≈2–14), silent during the actual mindspace duel (player at z≈247–253, ~250 m from the bed). See §4 Beat 4f, §9 |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | fills any remaining procedurally-sourced SFX slots chapter-wide — same call every other chapter builder makes |
| `WaveAlarm.wav` (`waveSting`) | `OuterStacksWaveSpawner`'s wave-transition sting, loaded from `Assets/Ronin7/Audio/WaveAlarm.wav` if present — shared across every chapter's `BuildWaveSpawner` call, not Ch7-specific |
| `ReverbZonePlacer.AutoTagInteriorVolumes()` + `PlaceReverbZonesForInteriorVolumes()` | reverb zones over the hall's (and mindspace's) interior volumes, run at the tail of the build (Appendix A.6) — the acoustic "cathedral hush over a warehouse of the erased" the dialogue script's SETTING block names |
| **The undertone/chorus** *(proposed, not built)* | the script's own core atmospheric cue — a constant low layer of shelved shadow-AIs, rising near dense racks, clarifying briefly during the mini-boss; currently absent (see the note above and §9) |
| **Outer-stacks combat bed** *(proposed, not built)* | neither `ReliquaryAmbience` nor `MindspaceDreadAmbience` covers z 8-50 (the gauntlet/gang-war zone) — the chapter's biggest single-beat footprint currently has no dedicated ambience layer |

**The frost→warmth transition itself is unbuilt as a crossfade, not just an unbuilt bed.** Canon (dialogue script line 230) names the threshold explicitly: "The frost gives way to the salvager's own patched warmth." Beyond the missing outer-stacks bed above, nothing in the as-built audio actually crossfades the two temperature beds into each other — a frost-wind/rime layer (paired with `VFX.FrostRime`, z≈8–34, §3.1) thinning out as `ReliquaryAmbience`'s warm reliquary hum swells in, crossing the gang-war-pocket threshold (z≈44–58) where the visual beacon (§4 Beat 1b) also resolves — would let the temperature story land as a felt threshold on the ears, not just in prop tint and light color.

**The reverb zones were previously missing from this manifest.** `BuildChapter7ForgottenNames` calls `ReverbZonePlacer.AutoTagInteriorVolumes()` then `PlaceReverbZonesForInteriorVolumes()` at the tail of the build (Appendix A.6), but until now this table listed only ambience beds and SFX — the "cathedral hush" *is* this reverb, not a room-tone loop. Because both calls tag interior volumes generically, the mindspace island should get a **distinct** reverb profile — tighter/deader/broken, matching the "colder and broken, not larger" shell constraint (§4 Beat 4c) — rather than inheriting the vast hall's long reverberant tail; verify this against the live build rather than assuming the auto-tag pass differentiates the two rooms correctly.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 07 — Forgotten Names** (`XRRigBuilder.BuildChapter7ForgottenNames()`).
2. **EditMode is the gate.** Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call. Consult the project's current baseline count (`CLAUDE.md`) rather than a number frozen at this document's writing.

   > ⚠ **Coverage blind spot (same class of gap every other chapter flags).** No EditMode test invokes `BuildChapter7ForgottenNames()` or loads `Ch07_ForgottenNames.unity`. **A green suite says nothing about whether this scene still builds, still gauntlets correctly, or still dives into and out of the mindspace correctly.** Every structural change in this refactor must be verified by opening the scene and walking it: clear all three gauntlet waves in order, confirm the tended-core/deep-archive reach gates fire at the right distances, grip the oldest blade and confirm the mindspace teleport (both directions) lands the rig at the right position with the right rotation, and confirm the fog/ambient treatment applies and restores correctly.
3. **Mindspace round-trip regression check (new, chapter-specific — no automated test exists today).** Enter the dive, defeat The Previous Owner, and confirm: (a) the rig teleports back to exactly (0,1,112) facing Euler(0,180,0), not somewhere near it; (b) `RenderSettings.fog/fogMode/fogColor/fogDensity/ambientMode/ambientLight` all match their pre-dive values exactly (not just "close" — `MemoryDiveController` snapshots and restores precisely); (c) `Mindspace_PreviousOwner` is fully `SetActive(false)` again — walking back toward z=250 should show nothing; (d) **verify what the exit facing actually points at.** `MindspaceExitPoint` sits at z=112 while `OldestBlade` (z=113) and `Dialogue_Beat4_Gift`'s panel (z=114) are both north of it; `Euler(0,180,0)` faces −Z, i.e. away from the calmed blade and the Gift dialogue, back down the hall toward the read bench, during the beat's emotional payoff. This is a concrete regression, not a stylistic choice — either rotate the exit point to identity (+Z, facing the blade and panel) or move the exit point itself north of z=114, and re-verify (a) against whichever fix lands.
4. **Ability-grant regression check (new).** Confirm `WeakpointSightGranter` and `ExitMindspaceGo` both activate on the *same* Trigger step (14) — if a future patch splits them into separate steps, verify the ordering still reads as "the gift" landing right as the player returns, not before or after a noticeable gap.
5. **Safe-zone survival test.** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
6. **Fallback audibility test.** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry — the full hall, all 26 rack props, the mindspace island, the mini-boss) plus one `LogWarning` per unresolved key — never an empty hall, never an exception.
7. **Perf reference bar.** No baseline exists yet for this scene (§1.6) — capture one (`UnityStats` in edit mode) the first time this checklist runs and record it here for future re-measurement.
8. **Console check:** `Ch7WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild, across all twelve dialogue sets. §7's consolidated total (12 sets, ~915 s, ~68 lines) is the denominator to sanity-check the batch against.

## 9. Additive-only cautions & open questions

- **[CONFIRMED AS-BUILT DEFECT] `MindspaceDreadAmbience` is built at world (0,2.6,0) — the breach — not inside the mindspace, and the mindspace duel plays with no dread bed at all.** `Chapter7Builder.cs:379` omits the `+ mindspace.position` offset that `MindspaceLight` two lines earlier (`:230`) correctly applies, and doesn't parent the ambience source to `mindspaceGo` either. As built, the bed is audible during Beat 1's breach (player spawns 2–14 m from it) and inaudible during the actual mini-boss duel (player is teleported ~250 m away, past `ProximityAmbienceLayer`'s 14 m outer radius) — the intent is exactly inverted. This is a live coordinate bug, not an atmosphere gap: fix the position to `(0, 2.6, 250)` or parent it under `mindspaceGo`. See §4 Beat 4f, §6, §7.
- **[CONFIRMED AS-BUILT DEFECT] `MindspaceLight`'s `AmbientPulse` is never attached — the mindspace duel plays under a dead-steady light, not the "breathing" cue this document describes elsewhere.** `AddAmbientPulse` (`ChapterSharedBuilders.cs:267`) resolves its target via `GameObject.Find("MindspaceLight")`, which cannot see inactive objects; `mindspaceGo.SetActive(false)` (`Chapter7Builder.cs:237`) runs before the `AddAmbientPulse("MindspaceLight", …)` call (`:382`), so `Find` returns null and the method silently no-ops — the `AmbientLightPulse` component is never added. §3.1, §6, and Appendix A.1 all assert the pulse exists; it does not, as built. Fix: attach the pulse before the mindspace root goes inactive, or have `AddAmbientPulse` search `includeInactive`. See §4 Beat 4f.
- **The additive-patch rule, and its one exception.** Re-running `BuildChapter7ForgottenNames()` wipes generated content, the same as every other chapter builder. The house rule remains: patch additively in the live editor, or fix `Chapter7Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** Regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Room shells and props get primitive colliders; the rack row, the oldest-blade prop, and the mindspace shell in particular should stay on primitive colliders for the same VR performance/physics-stability reasons every other chapter's ban exists to prevent.
- **The trust-test's spare/kill tracking is not implemented — a real feature gap, not a documentation gap.** The source script's production note calls for a genuine tracked choice across the gauntlet (whether Cipher lets fleeing scavengers escape or hunts them down), swapping Coral's opening Beat 2 line between warm and cold variants, with at least one scavenger crew always scripted to escape uncatchable so a witness always survives and Coral's "you left witnesses" always reads true. None of that tracking exists today; `Chapter7Lines` wires only the warm/spare-path line unconditionally, and no scavenger is marked uncatchable. This is documented as a deliberate scope cut in the builder's own class summary, not silently resolved here — flagged for a combat-systems/dialogue-branching decision.
- **The gang-war pocket's two factions have no distinct visual identity.** Both the wave-0 rival scavengers and the wave-2 turncoat-scavenger cell resolve to the same `Enemies.RivalScavenger` registry key with no faction-color tint distinguishing "looter I'm fighting" from "looter fighting on my side against the drones." A prefab/material pass should give the turncoat cell and the rogue-drone cell distinct reads so the "play the two factions against each other" mechanic (§4d) is legible at a glance, not just mechanically true. A cheap partial fix: torch-glow/spark VFX on the wave-0 scavenger spawns (§4 Beat 1c/1f, proposed) would at least separate looters from automata/drones visually, even before a full faction-tint pass exists.
- **Rival scavengers have no scripted flee behavior.** Canon describes them as "lightly armed, will flee" — the as-built `Enemy` AI has no distinct retreat state; a scavenger that stops attacking under low health simply idles rather than running. This matters doubly here because the (unimplemented, above) trust-test depends on scavengers being genuinely catchable-or-escapable, not just passively standing down.
- **Segment 1's broken-gravity fight-and-climb fantasy is flattened to flat-floor walking — the chapter's largest unlisted environment/traversal divergence from canon.** The dialogue script describes the outer stacks as "wall-runs and ledge-jumps across broken-gravity shelving, climbs along toppled racks, drops through gutted decking" (plus a cut Echo bark, "Gravity's dead past the arch, run the shelf"). §1.1's continuous-locomotion-only decision is correct for this project, but the gauntlet as built is a flat continuous-locomotion hall from z=8 to z=50 — the "toppled racks / gutted decking" imagery is dressing-only, not traversal. Noted here alongside the trust-test/bark/blade-rescue cuts above so a reader isn't surprised by the gap. A separate, purely visual dressing layer for the patchy-gravity read itself — distinct from this traversal cut — is proposed in §3/§4 Beat 1c (`VFX.DriftDebris`, optionally a canted rack variant).
- **Coral Vex has no facing set — she stands with her back to the player for the entire Beat 2/3 meeting.** `Ch7PlaceStoryNpc(Ch7CoralPrefab, (0,0,65), "Coral Vex")` (Chapter7Builder.cs:199, 492–505) takes no rotation argument and instantiates at identity, so as-built she faces +Z while the player approaches from −Z walking +Z — directly contradicting canon's "she turns from the rack and looks at him directly for the first time" and Echo's "she watched you come the whole way in" (`ch7_beat1_core_ahead`). The target rotation, `Euler(0,180,0)` (facing −Z, toward the incoming player), is documented in §4b Beat 2's art table but not yet built.
- **Rack density is flat across the entire hall — only tint increases toward the core, never rack count.** `Ch7BuildRackRow(world, 8f, 134f, 10f)` places one rack pair every 10 m from z=8 to z=134 with no taper — canon's SETTING block ("the deeper Cipher goes, the denser and brighter the blade-fields get, until the core is lit like a reliquary of saints") and every core-beat stage direction (Beat 1's "densest blade-field yet, hilt-lights packed close and bright," Beat 2's "mounted swords packed close," Beat 4's "rank on rank… the densest and most reverent space") call for the tended core and deep archive to physically fill in, not just warm up; at the fixed spacing, the deep archive (z88–134, ~5 pairs) is the sparsest stretch of hall in absolute count. See §1.6 and §3.1 for the proposed taper/infill fix.
- **The oldest blade has no back-wall terminus — it sits mid-corridor with 29 m of hall (including the read bench and outro) continuing north of it.** Canon stages it as the reliquary's literal bottom — "mounted alone on the back wall," "the bottom of the reliquary" — but `OldestBlade` (z=113) sits well short of `WallN` (z=142), with the read bench (z=122) and `ChapterOutro` (z=127) both further along the same open spine. §2's descent note excuses the flat-Y framing but not this. See §4 Beat 4c for the proposed terminal-alcove fix.
- **The rack-row gradient contradicts Beat 3's Wraith-line reveal — no dim/dying-hilt racks exist anywhere near where Coral describes them.** `Ch7BuildRackRow` brightens monotonically from dim blue-grey (z=8) to bright gold (z=134); Beat 3 (anchor z=75, `InverseLerp(8,134,75)` ≈ 0.53) already reads as a warm mid-tone there, and the gradient only gets brighter past that point. But Coral's forebear-reveal line points at "the old racks, the blades at the back, the dim ones whose hilts I can't keep lit anymore... The Wraith line" — the chapter's one Wraith-line visual motif has zero representation, and the gradient says the opposite of what the VO describes. See §3.1 for the proposed fix (a local dim/dead-hilt rack cluster near z=75).
- **Coral's own dim Wraith-line blade — her one physical object, and the beat's clearest piece of character thesis — is absent.** Canon has her lift "one of her own dim old blades from the back rack, a Wraith-line sword whose hilt-light has all but died" in Beat 3, then gather "the dim old blade she carried down, her own line's last shadow" again in Beat 5 — her own erased line made literal, foreshadowing "keep your shadow." Nothing in the current build represents this prop; Coral is placed once at (0,0,65) holding nothing. A static dim-hilt Wraith blade prop in her hands (or racked beside her, per §4 Beat 2c), reused as set-dressing at the read bench, would close this cheaply — and pairs naturally with the Wraith-rack-cluster gap above. **The same gap extends to her exit.** Canon's Beat 5 also has her gather "the portable racks she means to take" alongside that blade ("I'll bring what's portable," Beat 3) — a small portable-crate/rack prop near the read bench would let her departure with the crew read as physical, not VO-only. Marginal on its own, but it pairs cheaply with the breach-pod proposal above (§4 Beat 1c).
- **The Gift beat's Coral action cannot physically stage.** Canon has Coral *handle* the calmed blade — "Coral takes the calmed old blade from him gently, the way you take a sleeping child, and racks it" — but §3's "placed once at z=65" decision pins her 47 m south of the `OldestBlade` prop (z=113); with no walked NPC travel in this chapter (§5), that action has nothing to stage against, and `Dialogue_Beat4_Gift` plays as a disembodied panel at z=114 while the freed blade simply sits in-world. Options: accept it as pure VO with the blade left in place (current), or author a single scripted Coral placement/short walk to the oldest blade for Beats 4-Gift and 5 — which would also close the read-bench reach-gate gap below.
- **Beat 5's read-bench intimacy has no reach gate.** Every earlier reveal (Beats 2/3/4) is preceded by a `ReachTrigger` (steps 4, 7, 9), but `Dialogue_Beat5_Sabotage` (step 16) follows directly from the Gift dialogue at hall-center (0,1,114) with no reach step — Coral's "Now let me look at the thing you actually came for. Your switch. Hold still" can fire while the player is still 10 m south at the spine, never having walked to the off-center `ReadBench` (6,0.5,122). Add a read-bench reach gate before step 16, or accept the omission as intentional — pairs with the Coral-placement gap above.
- **The chapter's own "undertone/chorus" atmospheric cue — its most-repeated diegetic element per the script — is currently silent.** No `AudioSource`/ambient layer models "a constant low layer of shelved shadow-AIs, too quiet to make out, never silent... rises when Cipher nears a dense rack and goes briefly, terribly clear during the mini-boss." `ReliquaryAmbience`/`MindspaceDreadAmbience` are generic room-tone beds, not this specific cue. See §7.
- **The outer-stacks gauntlet zone (z 8-50, the chapter's largest single-beat footprint) has no dedicated ambient bed.** `ReliquaryAmbience` is centered at the tended core (z=65) and only reaches back to roughly z=47-50 at reduced volume; the breach and the bulk of the combat gauntlet play against silence beyond combat SFX.
- **The Echo in-combat bark pool is explicitly deferred, and only 2 of the source script's sample lines are wired.** The dialogue script itself defers the "FINAL set of Echo barks and their trigger points... authored once the level geometry is built" — position/event-triggered calls for an automaton waking, a scavenger fleeing, a dense rack passed, plus idle/ambient lines on dwell or backtrack. `ch7_beat1_gauntlet_bark` wires only 2 representative lines from the script's own sample pool as wave 0's single non-blocking bark; the rest of the pool (and any position-triggered wiring) is unbuilt. Same class of deferred-pool gap Ch6 flags for its own climb barks.
- **Blade-rescue side-objectives are explicitly out of scope for this pass.** The source script's deep-archive side content — freeing still-coherent kept blades, no duel required, seeding a Chapter 16 payoff — is non-critical/collectible-style by the script's own description. The deep archive is dressed with inert lit-rack props for atmosphere only; no rescue-interaction system, no `MemoryEchoVignette`-style trigger, and no Echo reaction-line pool for it exists in the current build. Flagged as future work, not a bug.
- **The oldest blade's reveal line is delivered from outside its own light's range.** `Dialogue_Beat4_KeptShadows` is anchored at (0,1,92) and its closing line points directly at the blade ("That one. The oldest blade I keep... its hilt-light cracked and pulsing wrong"), but `OldestBladeLight`'s 10 m range reaches back only to roughly z=103 — 11 m short of the anchor at z=92 — with fog and rack occlusion between them besides. The landmark the line describes isn't legibly wrong yet at the moment the line names it. See §4 Beat 4b, §6.
- **`OldestBladeLight` doesn't read as berserk in the first place, and doesn't change on pacification either.** Canon's Beat 4 is emphatic pre-fight — "its hilt-light cracked and pulsing wrong, the steel shivering on its brackets… the blade rattles like it wants out" — then steadies "to a calm, even glow" once the mindspace duel resolves. The as-built `OldestBladeLight` is a static accent with `Behaviour: None` throughout: it never carries the agitated pre-fight wrongness canon describes, and it has no runtime listener on the mini-boss's `Health.Died` to calm down afterward either. The one prop the entire mini-boss hinges on currently reads as an ordinary red accent light, not "a witness left awake too long." The same class of gap Ch6 flags for its own "tower goes dark on master death" reveal — a fast/irregular pulse behaviour as the default state, plus a small per-light listener subscribed to `previousOwner.GetComponent<Health>().Died` that swaps it to a slow calm glow, would close both halves at once. Canon's language also describes the blade itself, not just its light, shivering/rattling on its brackets — a gap the light-only fix above doesn't close; see §4 Beat 4c for a proposed audio-rattle + prop micro-jitter companion, settling on the same `Health.Died` listener.
- **The mindspace's "geometry fracturing and re-forming" is not modeled.** The screenplay describes the dreamspace as "the same visual grammar as the Ch3 playback but colder and broken, geometry fracturing and re-forming" as the fight progresses. The as-built mindspace is a static 14×14 box with four walls — no fracture VFX, no dynamic geometry change tied to fight progress exists today. A `VFX.MindspaceFracture` registry key now exists (Appendix B) as a concrete commission slot for this, mirroring how `VFX.FrostRime` anchors the outer-stacks frost/rime note above.
- **The instant fog/ambient swap on dive entry/exit has no `ScreenFader` blink — an open VR-comfort question, same class Ch5's massacre dive already carries unresolved.** `EnterDive()`/`ExitDive()` perform a same-frame `RenderSettings` write alongside the rig teleport; whether that warrants a brief fade-to-black (rather than an instant full-scene color/fog swap in the player's field of view) has not been signed off for either chapter. Worth a joint decision rather than two independent ones.
- **`The Previous Owner`'s clip-name sanitization should be verified against the live build before generating VO** (§4e) — `Chapter7Lines.Sanitize("The Previous Owner")` strips non-alphanumerics and lowercases, producing `thepreviousowner`; confirm the actual generated clip filenames match before wiring a VO batch.
- **The weakpoint-sight Gift moment cannot show canon's own image of the ability's birth — a mismatch, not just a missing sting.** Canon's ON-THE-BREAK stage direction (dialogue script line 426) describes the overlay sweeping "the racks, the world, Coral herself" the instant the ability activates; `WeakpointSight` only paints live `Health`s, and at the Gift moment nothing live remains in the deep archive to paint. See §4 Beat 4f for the two proposed resolutions (VO-only vs. a one-shot cosmetic overlay pulse on `AbilityGranter.Grant()`).
- **Coral-Vex.prefab and The-Previous-Owner.prefab are not yet baked to disk** (§1.5) — the builder's own closing `Debug.Log` states this explicitly. Both fall back to placeholders (`InstantiateNpc`'s capsule / `PlaceholderCharacterBuilder`'s existing `Ethereal` spec) until the art pass runs; neither fallback is a narrative problem (the chapter remains fully playable), but both are the highest-value character-art commissions for this chapter given how much of Beats 2-5 rests on Coral's physical presence.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch7Environment.asset`. Currently set inline at the top of `BuildChapter7ForgottenNames` (`Chapter7Builder.cs:120–145`).

| | Value |
|---|---|
| Directional key | color (0.6, 0.68, 0.82), intensity 0.35, rotation Euler(55, -35, 0) |
| Ambient | mode **Flat**, color (0.08, 0.09, 0.13) |
| Fog | mode **Exponential**, color (0.1, 0.12, 0.16), density 0.022 |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.55, 0.7, 0.9) | 1 | 10 | none |
| `OuterStacksLight` | (0, 2.4, 28) | (0.55, 0.7, 0.9) | 1.2 | 14 | none |
| `TendedCoreLight0` | (-4, 2.6, 65) | (1, 0.88, 0.6) | 1.6 | 16 | `AddConsoleFlicker(seed: 77f)` |
| `TendedCoreLight1` | (4, 2.6, 68) | (1, 0.88, 0.6) | 1.6 | 16 | none |
| `DeepArchiveLight0` | (-4, 2.6, 100) | (1, 0.9, 0.65) | 2 | 18 | none |
| `DeepArchiveLight1` | (4, 2.6, 108) | (1, 0.9, 0.65) | 2 | 18 | none |
| `OldestBladeLight` | (0, 2.2, 113) | (0.9, 0.25, 0.2) | 1.8 | 10 | none *(proposed: fast/irregular agitated pulse pre-duel, settling to a calm steady glow on `Health.Died` — §9)* |
| `MindspaceLight` *(inside the normally-inactive dive island)* | (0, 2.6, 0) + mindspace.position = (0, 2.6, 250) | (0.5, 0.6, 0.9) | 1.4 | 12 | `AddAmbientPulse(period: 6.4f)` — **confirmed no-op as-built**: `GameObject.Find("MindspaceLight")` (`ChapterSharedBuilders.cs:267`) can't see the light because `mindspaceGo.SetActive(false)` (`Chapter7Builder.cs:237`) already ran by the time this call fires (`Chapter7Builder.cs:382`); `AmbientLightPulse` is never attached. See §4 Beat 4f, §9 |
| `ReadBenchLight` *(proposed, not built — §6)* | (6, 1.4, 122) | (0.5, 0.75, 0.95) — cool console tint, distinct from the warm deep-archive gold | ~1.2 | ~8 | none |

No event lights this chapter (`ChapterEnvironmentProfile.eventLights` unused). `ReadBenchLight` is a proposal only — it does not change the "seven accent lights in the real-world hall + `MindspaceLight`" count elsewhere in this document (§1.6, §3.1) until built.

### A.2 Beat 1 — Breach + Outer Stacks

| Element | Coordinates / value | Component / method |
|---|---|---|
| `ReliquaryHall_Floor`/`_Ceiling` | center (0,0,70), size (20,0,146) — floor tint (0.06,0.07,0.09), ceiling tint (0.03,0.03,0.05) | `BuildFloorCeiling(world, "ReliquaryHall", ...)` |
| `ReliquaryHall_WallW`/`WallE` | (∓10, RoomH/2, 70), size (0.2, RoomH, 146) | `BuildWall` |
| `ReliquaryHall_WallS`/`WallN` | (0, RoomH/2, -2) / (0, RoomH/2, 142), size (20, RoomH, 0.2) | `BuildWall` |
| Rack pairs ×13 | (-9,1.1,z) / (9,1.1,z) for z = 8,18,28,...,128 (step 10, up to `zEnd=134`); scale (0.4,2.2,1.4); tint `Color.Lerp((0.28,0.32,0.48), (0.95,0.82,0.5), InverseLerp(8,134,z))` | `Ch7BuildRackRow(world, 8f, 134f, 10f)` |
| Katana "Echo" | (2,1,4), rot Euler(-90,0,0) | `BuildSword(pos, rot, weapon, Ch7EchoBladePrefab)` — `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` |
| Scavenger def (`Ch7Scavenger.asset`) | maxHealth 50, damage 8, moveSpeed 1.6, attackCooldown 0.9s | `Ch7EnsureScavengerDefinition` |
| Automaton def (`Ch7Automaton.asset`) | maxHealth 140, damage 16, moveSpeed 1.0, attackCooldown 1.0s | `Ch7EnsureAutomatonDefinition` |
| Scavenger positions ×3 | (-3,0,18), (0,0,21), (3,0,24) | `scavengerPos[]` → `Ch7BuildWaveEnemies` |
| Automaton positions ×2 | (-2,0,34), (2,0,38) | `automatonPos[]` → `Ch7BuildWaveEnemies` |
| Turncoat scavenger ×3 (faction 0) | (-3,0,44), (-1,0,47), (-3,0,50); capsule scale (0.5,0.9,0.5); `FactionCombatant` maxHealth 45 | `Ch7BuildGangWarPocket` → `Ch7BuildFactionCombatant` |
| Rogue drone ×3 (faction 1) | (3,0,44), (1,0,47), (3,0,50); same shape/stats, faction 1 | `Ch7BuildGangWarPocket` → `Ch7BuildFactionCombatant` |

*(`Ch7BuildFactionCombatant` authors only `maxHealth` and the faction id above — `moveSpeed`/`attackRange`/`damagePerHit`/`attackInterval` come from `FactionCombatant`'s own serialized-field defaults, not from this builder; see §4 Beat 1d.)*
| `OuterStacksWaveSpawner` | trigger (0,0,22), radius 12; waves = [scavengers, automata, gangWarHealths]; wave0 bark = `dlgGauntletBark` | `BuildWaveSpawner("OuterStacksWaveSpawner", ...)` |
| Dialogue players | `Dialogue_Beat1_Breach` (0,1,8) set `ch7_beat1_breach`; `Dialogue_Beat1_GauntletBark` (0,1,18) set `ch7_beat1_gauntlet_bark`; `Dialogue_Beat1_CoreAhead` (0,1,50) set `ch7_beat1_core_ahead` | `Ch7BuildDialogue` |
| Reach point | `TendedCoreReachPoint` (0,1,58), radius 5 | inline GameObject |
| Mission steps | indices 1–4 of 19 | `AuthorDialogueStep`/`wavesStep` (`DefeatWaves` kind)/`AuthorReachStep` |

**Density is fixed by spacing, not by z.** The rack-pairs row above uses a single uniform `spacing: 10f` for the whole `zStart:8` → `zEnd:134` sweep — nothing in `Ch7BuildRackRow`'s signature currently varies spacing by z, so the deep-archive end has exactly as many rack pairs per meter as the outer stacks. See §3.1's density-gap note for the proposed taper/infill fix.

### A.3 Beat 2/3 — The Tended Core (Coral Vex, meeting + forebear reveal)

| Element | Coordinates / value | Component / method |
|---|---|---|
| Coral Vex | spawn (0,0,65); `StoryNpc` displayName "Coral Vex"; no `StoryNpcWander` | `Ch7PlaceStoryNpc(Ch7CoralPrefab, (0,0,65), "Coral Vex")` — falls back to `InstantiateNpc`'s capsule while `Coral-Vex.prefab` is unbaked |
| Dialogue players | `Dialogue_Beat2_Archivist` (0,1,62) set `ch7_beat2_archivist`; `Dialogue_Beat3_Forebear` (0,1,75) set `ch7_beat3_forebear` | `Ch7BuildDialogue` |
| Mission steps | indices 5–6 of 19 | `AuthorDialogueStep` |

*(No new geometry beyond `TendedCoreLight0`/`TendedCoreLight1`, already listed in A.1 — this pair of beats reuses Beat 1's hall unmodified.)*

### A.4 Beat 4 — The Deep Archive, the Oldest Blade, and the Mindspace

| Item | Value | Source |
|---|---|---|
| `OldestBlade` prop | (0,1.2,113), scale (0.06,0.02,1.1), tint (0.9,0.25,0.2) — `PrimitiveType.Cube` | inline in `BuildChapter7ForgottenNames` |
| Reach points | `DeepArchiveReachPoint` (0,1,88) radius 6; `OldestBladeReachPoint` (0,1,111) radius 4 | inline GameObjects |
| Previous-Owner def (`Ch7PreviousOwner.asset`) | maxHealth 220, damage 18, moveSpeed 1.4, attackCooldown 0.8s | `Ch7EnsurePreviousOwnerDefinition` |
| Mindspace root | `Mindspace_PreviousOwner`, position (0,0,250); `MemoryFlashbackController` (default fields: fogColor (0.35,0.37,0.42), density 0.045, ambient (0.30,0.30,0.34)); built `SetActive(false)` at the end of assembly | inline |
| Mindspace floor/walls | `BuildFloorCeiling(mindspace, "MindspaceFloor", Vector3.zero, (14,0,14), (0.12,0.13,0.18), (0.07,0.07,0.1))`; 4× `BuildWall` at local ∓7 x/z | inline |
| `MindspaceEntryPoint` | mindspace.position + (0,1,-3) = (0,1,247) | inline GameObject |
| `MindspaceExitPoint` | (0,1,112), rotation Euler(0,180,0) — real-world, in front of the oldest blade | inline GameObject |
| The Previous Owner | `InstantiateNpc(Ch7PreviousOwnerPrefab, mindspace.position + (0,0,3), "The Previous Owner")`, parented to mindspace, `FitNamedCharacter`, rotated Euler(0,180,0); `CapsuleCollider` center (0,1,0) height 2 radius 0.4; synthesized `ArmR/Sword/Blade/BladeTip` chain; `Enemy` wired to `previousOwnerDef`/`playerHealth` | `Ch7BuildMindspaceBoss` |
| `MemoryDiveController` (`MindspaceDive`) | `diveRoot`=mindspaceGo, `diveEntryPoint`=entry, `diveExitPoint`=exit, `rigRoot`=rig.transform, `flashback`=mindspaceFlashback | inline |
| `EnterMindspaceTrigger` / `ExitMindspaceTrigger` | both `SetActive(false)`, `dive` ref wired; `MemoryDiveEntryTrigger`/`MemoryDiveExitTrigger` | inline |
| `WeakpointSightGranter` | `AbilityGranter`, `abilityId = AbilityId.WeakpointSight`; built `SetActive(false)` | inline |
| Dialogue players | `Dialogue_Beat4_KeptShadows` (0,1,92) set `ch7_beat4_kept_shadows`; `Dialogue_Beat4_QuietIt` (0,1,111) set `ch7_beat4_quiet_it`; `Dialogue_Beat4_MindspaceIntro` at entry point (0,1,247) set `ch7_beat4_mindspace_intro`; `Dialogue_Beat4_Gift` (0,1,114) set `ch7_beat4_gift` | `Ch7BuildDialogue` |
| Mission steps | indices 7–15 of 19 | `AuthorReachStep`/`AuthorDialogueStep`/`AuthorTriggerStep`/`AuthorDefeatStep` |

### A.5 Beat 5 — The Read Bench

| Element | Coordinates / value | Component / method |
|---|---|---|
| `ReadBench` | (6,0.5,122), scale (1.6,1,1), tint (0.2,0.22,0.26) | inline `PrimitiveType.Cube` |
| `ReadBench_Rack` | (6,1.1,123.5), scale (1.2,1.6,0.3), tint (0.85,0.75,0.5) | inline `PrimitiveType.Cube` |
| Dialogue players | `Dialogue_Beat5_Sabotage` (6,1,123) set `ch7_beat5_sabotage`; `Dialogue_Beat5_Hookout` (6,1,125) set `ch7_beat5_hookout` | `Ch7BuildDialogue` |
| `CHAPTER 7 COMPLETE` canvas | worldspace, (0,1.4,128), rotation Euler(0,180,0) (faces -Z toward the player), built inactive | `Ch7BuildCompleteCanvas` |
| `ChapterOutro` | (0,1,127); `CampaignFlagSetter` flags `["ch7_complete", "coral_vex_recruited"]` wired to `OnActivated`; `completeCanvas` ref = the complete canvas; built inactive | `ChapterOutro` component |
| Mission steps | indices 16–18 of 19 | `AuthorDialogueStep`/`AuthorTriggerStep` |

### A.6 Scene root hierarchy (current)

`BuildChapter7ForgottenNames()` creates these as **siblings**, not nested: `Directional Light`, `DataReliquary` (the whole hall shell, all 26 rack props, `OldestBlade`, `ReadBench`/`ReadBench_Rack`), seven accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig, Coral Vex, the scavenger/automaton/gang-war-pocket enemy GameObjects (built as direct children of `world` rather than under a named sub-branch), `Mindspace_PreviousOwner` (containing its own floor/walls/light/boss, `SetActive(false)`), `MindspaceEntryPoint`/`MindspaceExitPoint`, `MindspaceDive`, `EnterMindspaceTrigger`/`ExitMindspaceTrigger`, `WeakpointSightGranter`, three reach-point GameObjects, twelve dialogue-player roots, `OuterStacksWaveSpawner` (+ its `_Trigger` child), the complete canvas, `ChapterOutro`, and `Mission`. `SettingsPanelBuilder.BuildSettingsPanel()`, `XRRigBuilder.RewireOpenScene()`, the XR interaction-manager/UI event-system setup, `BuildAmbienceLayer` ×2 (`ReliquaryAmbience`, `MindspaceDreadAmbience`), `ProceduralAudioClipBuilder.AssignGeneratedClips()`, `AddConsoleFlicker`/`AddAmbientPulse`, and `ReverbZonePlacer`'s two static calls all run at the tail of the build, in that order, before the final `EditorSceneManager.SaveScene`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots (0 through 5), and moves `DataReliquary`'s static geometry (plus the mindspace's own static shell) into the former. **The scavenger/automaton/gang-war-pocket enemy GameObjects must not ride along with that move.** They are currently direct children of `world` alongside `DataReliquary`'s own subtree (above), but per §1.2's contract they are *logic* (enemy spawns), not static art — if the split migrates `world`'s children wholesale into `[STATIC_ART_DO_NOT_DELETE]`, the wave-0/1 enemies and the gang-war pocket survive the logic wipe and corrupt the fallback/rebuild story §1.4 and §8's fallback-audibility check (item 6) depend on. Re-parent them explicitly to `[BEAT_1_LOGIC]` during the split; only `DataReliquary`'s hall shell, rack props, `OldestBlade`, and `ReadBench`/`ReadBench_Rack` belong in the safe zone.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Two resolve (Echo's blade, reused from earlier chapters); everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Named.CoralVex` | `Art/Generated/Characters3D/Named/Coral-Vex.prefab` | MISSING *(falls back to `InstantiateNpc`'s capsule)* |
| `Named.ThePreviousOwner` | `Art/Generated/Characters3D/Named/The-Previous-Owner.prefab` | MISSING *(falls back to `PlaceholderCharacterBuilder`'s already-defined Ethereal spec — ghost-blue glass orbs — not yet baked to a `.prefab`)* |
| `Enemies.RivalScavenger` | `Art/Generated/Characters3D/Enemies/RivalScavenger.prefab` | MISSING |
| `Enemies.ArchiveDefenseAutomaton` | `Art/Generated/Characters3D/Enemies/ArchiveDefenseAutomaton.prefab` | MISSING |
| `Enemies.RogueDrone` | `Art/Generated/Characters3D/Enemies/RogueDrone.prefab` | MISSING |
| `Rooms.ReliquaryHallShell` | `Art/Generated/Rooms/ReliquaryHallShell.prefab` | MISSING |
| `Rooms.MindspaceShell` | `Art/Generated/Rooms/MindspaceShell.prefab` | MISSING |
| `Rooms.BreachBackdrop` | `Art/Generated/Rooms/BreachBackdrop.prefab` | MISSING *(proposed — the debris-field/starfield dressing visible through the breach aperture, behind `ReliquaryHall_WallS`; see §3, §4 Beat 1c)* |
| `Props.Rack` | `Art/Generated/Props/Rack.prefab` | MISSING |
| `Props.OldestBlade` | `Art/Generated/Props/OldestBlade.prefab` | MISSING |
| `Props.ReadBench` | `Art/Generated/Props/ReadBench.prefab` | MISSING |
| `Props.ReadBenchRack` | `Art/Generated/Props/ReadBenchRack.prefab` | MISSING |
| `Props.BreachAperture` | `Art/Generated/Props/BreachAperture.prefab` | MISSING *(proposed — the torn-hull opening on `ReliquaryHall_WallS` marking where Cipher breached in; see §3, §4 Beat 1c)* |
| `Props.BreachPod` | `Art/Generated/Props/BreachPod.prefab` | MISSING *(proposed — the vessel of both the chapter's founding line ("Pod's away…") and its closing stage direction; docked/embedded at the breach end, visible at or through `Props.BreachAperture`; see §3, §4 Beat 1c/5c)* |
| `VFX.BreachInrush` | `Art/Generated/VFX/BreachInrush.prefab` | MISSING *(proposed — cold-air/frost-inrush VFX at the breach point around player spawn; see §3, §4 Beat 1c)* |
| `VFX.DriftDebris` | `Art/Generated/VFX/DriftDebris.prefab` | MISSING *(proposed — atmosphere-only drifting fragments/dust selling canon's "gravity gone patchy," distinct from the traversal cut in §9; see §3, §4 Beat 1c)* |
| `VFX.FrostRime` | `Art/Generated/VFX/FrostRime.prefab` | MISSING *(now placed — the outer stacks' cold-end conduits, z≈8–34, see §4 Beat 1c; the "frost rimes the dead conduits" anchor previously had no coordinates, only prose and light color)* |
| `VFX.HiltGlow` | `Art/Generated/VFX/HiltGlow.prefab` | MISSING *(named in §1.3's VFX-folder listing but absent from this inventory until now; per-hilt emissive/glow overlay for `Ch7BuildRackRow`/`Props.Rack` — see §3.1, §4 Beat 1c)* |
| `VFX.MindspaceFracture` | `Art/Generated/VFX/MindspaceFracture.prefab` | MISSING *(named in §1.3's VFX-folder listing but absent from this inventory until now; the commission slot for §9's "geometry fracturing and re-forming is not modeled" open question)* |

**Reuse notes.**

- `Named.Echo` is the only key this chapter shares with every other chapter's katana placement — no chapter-specific variant needed.
- `Enemies.RivalScavenger` is proposed to serve **both** wave 0's rival looters and wave 2's turncoat-scavenger cell (§9 flags the resulting lack of visual distinction as a gap worth a dedicated faction-tint variant, not a registry-key split).
- `Rooms.ReliquaryHallShell` is one continuous prefab for the entire hall (§2) — there is no per-zone room-shell split the way Chapter 1's four rooms have distinct shells; a prefab pass could still choose to author it as several modular tileable segments internally, as long as the exterior footprint (x[-10,10], z[-2,142]) is reproduced exactly.
- `Rooms.MindspaceShell` is deliberately a separate, smaller room (14×14) at the same `RoomH`, not a variant of the reliquary hall shell — see §4 Beat 4's constraint note on why it must not simply reuse hall geometry (canon wants it colder/broken, not a re-skin).
- `Props.Rack` is proposed to cover both the stripped outer stacks (z8-34) and the pristine deep archive (z88-108+) with tint alone (§3.1) — a stripped/empty-bracket variant for the outer end, rather than a registry-key split, would let the "looted vs. kept" contrast read in geometry as well as color.

---

*Files consulted: `Project/Assets/Ronin7/Scripts/Editor/Chapter7Builder.cs`, `Chapter7Lines.cs`, `ChapterSharedBuilders.cs`, `XRRigBuilder.cs`, `Project/Assets/Ronin7/Scripts/World/Story/MemoryDiveController.cs`, `MemoryFlashbackController.cs`, `AbilityGranter.cs`, `Project/Assets/Ronin7/Scripts/Player/WeakpointSight.cs`, `Project/Assets/Ronin7/Scripts/Enemies/FactionCombatant.cs`, `Project/Assets/Ronin7/Scripts/World/EnemyWaveSpawner.cs`, `Project/Assets/Ronin7/Scripts/World/Story/MissionDirector.cs`, `Project/Assets/Ronin7/Scripts/Editor/PlaceholderCharacterBuilder.cs`, `story ouput/Ch07_Forgotten_Names.md`, `story ouput/Ch07_Forgotten_Names_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`, and `Ch01-Scene-Construction.md`/`Ch06-Scene-Construction.md` as structural templates.*

# Chapter 6 — Scene Construction

*The architectural contract for `Ch06_IronDojo.unity`: what Chapter 6 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 6 ("The Iron Dojo") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants, mission triggers, or the any-order tower gate.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter6Builder.cs`, entry point `XRRigBuilder.BuildChapter6IronDojo()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 06 — The Iron Dojo**. The summit-route parkour retrofit lives in a sibling file, `ParkourLevelBuilder.cs`, entry point `AddDojoSummitRoute()` (also exposed standalone as **Tools → Space Samurai → Chapters → Patch Ch06 Summit Route (additive)**), and is called from inside `BuildChapter6IronDojo()` so a fresh rebuild always includes it.
- **Source of truth for *content*:** this document plus the canon story files (`Ch06_The_Iron_Dojo.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or terrace reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** All three master fights, the tower-gauntlet combat, and the long fall-hazard of the climb are sold entirely through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never by moving the camera. This applies equally to Kaelen's death cutscene and the citadel-collapse VFX in the evacuation.
- **Traversal in Ch6 is continuous locomotion + snap-turn on the flat sections, plus grip-based `WallClimbLocomotion` on anything tagged `Climbable`.** This is the one chapter in the project whose builder *does* use a climb mechanic — the switchback ascent's terrace decks/rocks/bell tower and the summit hold-ladder are deliberately marked `Climbable` (`ParkourLevelBuilder.AddDojoSummitRoute`), per the canon "vertical-parkour traversal showcase" design intent and Ch4's Deepworks-descent precedent (same kit, inverted). This is **not** teleport locomotion and **not** NavMesh — the CharacterController climbs tilted ramp colliders and hand-over-hand grip holds the same way it walks a flat floor; there is still no teleport and no NavMesh anywhere in this chapter. Do not introduce either.
- **The mandatory ascent is walkable; grip-climbing is optional, never a progression gate.** `Ramp0–3` (`Ch6BuildRamp`) are gentle tilted slopes — e.g. Terrace0→Terrace1 is rise 3 / run 14, ≈12° — that the `CharacterController` simply walks, and `WindowReachPoint`, the Beat 1 mission gate (§4 Beat 1 §b, step 3), sits on Terrace4 at (0,13,80), reachable entirely on foot up that ramp spine. The `Climbable`-tagged terrace faces and the `SummitHold0–4`→`SummitDeck` ladder are an *optional* parkour showcase layered on top of the ramp spine — a player who never grips a hold can still complete the chapter end to end. This matters for accessibility (a sole grip-climb path would lock out seated/limited-mobility/one-controller players from the entire chapter) and it scopes the fall hazard discussed in §4 Beat 1 §f/§9 to the optional grip routes, not the walk-only completion path.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | terrace/room shells, props, VFX, backdrops, decorative lights, the climbable-marker pass | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC spawns + wander/walker wiring, enemy spawns (cadre + masters), wave-spawner arming, reach points, dialogue players, prompts, mission-spine steps | `[BEAT_N_LOGIC]` |

**Chapter 6 has no doors to split art/logic across** (§2) — the closest analogue is each tower's `EnemyWaveSpawner`, whose trigger volume and `Begin()` wiring are *logic*, while the arena shell and doorway gap it guards are *art*. Treat the spawner exactly the way Ch1 treats a door: art builds the room and the doorway aperture; logic decides when the room turns hostile.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildDoorwayWall`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildEnemy`, `BuildWaveSpawner`, `BuildDialoguePlayer`, `Author*Step` — ripples across all of them. `BuildNpcWalker` is technically a **Chapter1Builder.cs-local** private static (not `ChapterSharedBuilders.cs`), but Chapter 6 already calls it across the partial-class boundary for its evacuation trainees — treat it as frozen too; it is load-bearing for two builders now, not one.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, plus `Chapter1Builder.cs`'s `BuildNpcWalker` (cross-chapter dependency, see above).
- **Free to restructure:** the Ch6-local helpers, called only from `BuildChapter6IronDojo()` — `Ch6EnsureHespaDefinition`/`Ch6EnsureCaradocDefinition`/`Ch6EnsureKaelenDefinition`, `Ch6BuildDialogue`, `Ch6WireVoiceClips`, `Ch6PlaceStoryNpc`, `Ch6BuildTrainee`, `Ch6BuildCadre`, `Ch6BuildMasterEnemy`, `Ch6BuildTerrace`, `Ch6BuildRamp`, `Ch6BuildGroundStrip`, `Ch6BuildTowerArena`, `Ch6BuildCompleteCanvas`. `AddDojoSummitRoute` and `BuildClimbProp` in `ParkourLevelBuilder.cs` are also chapter-scoped (Ch6-only) despite living in a separate file.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs` or `Chapter1Builder.cs`'s `BuildNpcWalker`, stop — you have left Chapter 6 and are now silently rebuilding thirteen other chapters (or breaking Chapter 1's Kessler routing).

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two new ScriptableObjects carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch6Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, per-zone accent lights (forest/spine/yard/terrace patrol), per-light behaviour (`AmbientPulse`/`ConsoleFlicker`) |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared with all other chapters, extended with Ch6-specific keys) |

Both are net-new for this chapter (`Ch1Environment.asset` already exists as the pattern to copy). Neither Ch6-specific asset exists yet.

Prefab **paths never appear in builder code.** The builder asks the registry for `Rooms.TerraceDeck`; the registry asset holds the path. This is the whole point of the indirection — art can re-point a prefab without touching a `.cs` file or this document.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live (Morrigan, Matron-Hespa, Drillmaster-Caradoc, Master-Kaelen, and Echo's blade already resolve there — see Appendix B). New environment folders are siblings of `Characters3D/`:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (masters + Morrigan + Echo live here)
  Rooms/                                    ← new — terraces, spine, tower shells
  Props/                                    ← new — trees, rocks, ramps, bells, holds
  VFX/                                      ← new — dust/hologram/collapse effects
```

**No `Doors/` folder is needed for this chapter.** Unlike Chapter 1's three lockable sliding doors, every room-to-room transition in Chapter 6 is an open aperture (`BuildDoorwayWall`'s static gap) — see §2's Room Boundaries & Gates table. Gating is achieved entirely through proximity-armed `EnemyWaveSpawner`s and the `MultiObjectiveGate`, not physical locks.

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter6IronDojo()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter6Builder.cs:96`) — the same pattern as every other chapter builder. That call discards the entire scene rather than deleting objects from it. There is nothing left to search for after it runs.
>
> Making the safe zone real requires **replacing the wipe strategy**, one of:
>
> 1. `EditorSceneManager.OpenScene(Ch6ScenePath)`, then `DestroyImmediate` each **generated root by name** (`IronDojo`, `Game`, `Mission`, the rig, the accent lights, `KillListGate`, `TowerArm`, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist. `ParkourLevelBuilder.PatchCh06SummitRoute()` already demonstrates the open-in-place / mutate-idempotently / save pattern for this exact scene — reuse it rather than inventing a third.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred**, and this chapter already has a working precedent for it (`PatchCh06SummitRoute`) more directly applicable than any other chapter's.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero Ch6 environment prefabs exist.** No terrace deck, ramp, tree, rock, tower-arena shell, holo-slate, or summit bell. The four named-cast prefabs (Morrigan, Matron-Hespa, Drillmaster-Caradoc, Master-Kaelen) and Echo's blade **do** exist (Tripo pipeline) — see Appendix B for the full inventory.

A builder that instantiates from an empty registry produces **an empty mountain** — the first run of the refactored builder would destroy Chapter 6's climb and all three towers.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Rooms_TerraceDeck);
if (prefab == null) {
    Debug.LogWarning($"[Ch6] {ArtKey.Rooms_TerraceDeck} unresolved — primitive fallback.");
    Ch6BuildTerracePrimitive(ascent, name, center);   // Appendix A geometry
} else {
    InstantiateAt(prefab, ascent, center, Quaternion.identity);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`) and the `ArtPrefabRegistry.TryInstantiateOrFallback` idiom `BuildEnemy` already uses for cadre bodies. **The `Climbable` marker pass must survive the swap unconditionally** — whichever geometry lands on a terrace deck, rock, or the summit hold ladder, `AddDojoSummitRoute`'s tagging pass (§Appendix A) must still find a collider to mark, or the ascent becomes unclimbable.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). Treat 90 FPS as the ceiling to protect and 72 Hz (`QualityBootstrap`'s shipped default) as the floor.
- **This chapter is the widest exterior footprint built to date** — a forest slope, a 5-terrace switchback climb spanning ~80 vertical/lateral meters, an open-sky Iron Yard, and three separate tower arenas, all live in one scene with no room-to-room occlusion doors to cull behind. `ZoneBounds` alone spans center (0, 8, 90) radius 100 — nearly triple Chapter 1's radius-45 sphere. Draw-call and overdraw discipline matters more here than in any interior chapter: prefer per-zone streaming/LOD over "everything visible from the Iron Yard at once" if the greybox tri count trends high once real prefabs land.
- Every accent/patrol light in this chapter is a real-time `Light` — 5 terrace `PatrolLight`s + `ForestLight0` + `SpineLight0/1` + `IronYardLight0/1` is **10 built today**; the count rises to **13** once the three proposed tower-interior accent lights (`CradleLight0`/`ProvingLight0`/`VestingLight0`, §4 Beat 3 §c/§f, §6, §A.1) are added — re-measure draw calls / setPassCalls after every prefab lands, the same discipline Chapter 1's budget clause established. **No recorded greybox baseline exists yet for this scene** — capture one (`UnityStats` in edit mode, per `Project/Docs/CHAPTER-BUILD-LEDGER.md`'s Chapter 1 precedent) the first time this document's checklist (§8) is run, and re-measure again once the tower lights land, treating that later number as this chapter's own perf bar going forward.
- Prefabs replacing primitives carry their own materials and will not `TintShared`-batch (§3.1) — the same cost Chapter 1's budget clause flags.
- **The directional key light's shadow mode is an unmade per-tier call.** `Chapter6Builder.cs` sets the sun's color/intensity/rotation but never touches `light.shadows` — for a `ZoneBounds` radius-100 open-sky exterior at the widest footprint built to date, a low-angle (50°) sun with real-time cascaded shadows is a genuine Quest-tier cost, while no shadows at all reads flat and undercuts §3's "beauty IS the horror" brief. Record the decision in `Ch6Environment.asset`, per-tier: Quest may drop to `LightShadows.Hard` at reduced resolution/distance or off entirely, PCVR keeps `Soft` — the same tier-split discipline `CLAUDE.md` already applies to bloom/motion-blur. See Appendix A.1 for the proposed literal.

## 2. Chapter spatial map

Chapter 6 is **one continuous exterior/interior scene**, `Assets/Ronin7/Scenes/Ch06_IronDojo.unity`, shaped as a **linear climb that opens into a three-way branch**: a forest slope at the mountain's foot, a 5-terrace switchback ascent rising from world y=0 to y=12, Morrigan's spine room at the top of the climb, and an open Iron Yard from which three tower corridors branch — west to the Cradle, east to the Vesting, and north (further out) to the Proving. There is no way to skip the climb, but once at the Iron Yard the three towers are genuinely free-roam, in any order, with no barrier preventing a player from walking straight past all three cadre encounters to peek at a locked tower before doubling back.

```
                                                          +X (Vesting) →
                                                                 |
  Forest Slope        5-Terrace Switchback Ascent           Iron Yard ── Vesting Corridor ── Vesting Tower
  z[-4,24], y=0        z 18→84, y 0→12 (rising)              z[100,122]   x[14,38] z[106,116]  x[38,54] z[103,119]
  x[-10,10]            (alternating x offsets)                x[-15,15]        (Drillmaster) (Master Kaelen)
        |                     |                                   |
        └── Ramp0‑3 (tilted) ─┴── Terrace4 "WindowLedge" ─── MorriganSpine ──┬── Proving Corridor ── Proving Tower
            connect Terrace0‑4    (0,12,80) — Morrigan's        x[-6,6]      |   x[-5,5] z[121,141]  x[-8,8] z[142,158]
            centers in sequence   window, entry point           z[84,100]    |        (Caradoc)
                                                                              |
                                                                        Cradle Corridor ── Cradle Tower
                                                                        x[-38,-14] z[106,116]  x[-54,-38] z[103,119]
                                                                              (Matron Hespa)
```

| Beat | Zone | Footprint | Floor center / size | World Y |
|---|---|---|---|---|
| 0 | The Cairn (voice-only briefing — no physical set) | — | — | — |
| 1 | Forest Slope (spawn) | x[-10,10], z[-4,24] | center (0,0,10), scale (20,1,28) ground slab | 0 |
| 1 | Ascent — Terrace0…Terrace4 | 8×8 each, centers listed in Appendix A.2 | rising y 0→3→6→9→12 | 0, 3, 6, 9, 12 |
| 2 | Morrigan's Spine (the engineering core) | x[-6,6], z[84,100] | center (0,0,92), 12×16 | 12 |
| 2–5 | Iron Yard (central muster ground, open sky) | x[-15,15], z[100,122] | center (0,-0.1,111), 30×22 | ~11.9 |
| 3A | The Cradle — corridor + tower arena | corridor x[-38,-14] z[106,116]; arena x[-54,-38] z[103,119] | corridor center (-26,0,111) 24×10; arena center (-46,0,111) 16×16 | 12 |
| 3B | The Proving — corridor + tower arena | corridor x[-5,5] z[121,141]; arena x[-8,8] z[142,158] | corridor center (0,0,131) 10×20; arena center (0,0,150) 16×16 | 12 |
| 3C | The Vesting — corridor + tower arena | corridor x[14,38] z[106,116]; arena x[38,54] z[103,119] | corridor center (26,0,111) 24×10; arena center (46,0,111) 16×16 | 12 |
| 5 | The Iron Yard again (evacuation) — same footprint as above | — | — | 12 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, used for every enclosed room in this chapter (Morrigan's Spine, the three tower arenas). The Iron Yard and forest slope are **open-sky exteriors with no ceiling** — this is the one chapter to date where a room-shell helper is deliberately *not* called for a whole zone.

**All coordinates for the upper citadel (Morrigan's Spine, the Iron Yard, and all three towers) are authored as `UpperCitadel`-local positions, offset by `Ch6UpperY = 12f` at the parent transform.** The table above already folds that offset into "World Y." A registry-driven refactor must preserve this parenting scheme unchanged — `BuildFloorCeiling`/`BuildWall`/`BuildDoorwayWall` hard-code floor/ceiling Y relative to their parent, so moving the upper citadel's contents out from under a single y=12-offset parent (e.g. to make each tower a separately-loaded sub-scene) would require re-deriving every local Y in Appendix A.2.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience.

**Room Boundaries & Gates** — unlike Chapter 1, **this chapter has no lockable sliding doors.** Every room-to-room transition is a static wall gap (`BuildDoorwayWall`), and every combat gate is a proximity-armed `EnemyWaveSpawner`, not a `Controller`/lock:

| Boundary | Position | Aperture | Gating mechanism |
|---|---|---|---|
| `MorriganSpine_WallN` | (0, RoomH/2 + 12, 100) | 12 m wall, 3 m door gap | none — open from the start; Morrigan's window (Beat 1→2 transition) is a separate climb-in point, not this doorway |
| Cradle tower arena, east wall | (-38, RoomH/2 + 12, 111) | 16 m wall, 4 m door gap | `CradleWaveSpawner`, `triggerRadius` 9 m centered on the Cradle corridor (-26, 12, 111) |
| Proving tower arena, south wall | (0, RoomH/2 + 12, 142) | 16 m wall, 4 m door gap | `ProvingWaveSpawner`, `triggerRadius` 9 m centered on the Proving corridor (0, 12, 131) |
| Vesting tower arena, west wall | (38, RoomH/2 + 12, 111) | 16 m wall, 4 m door gap | `VestingWaveSpawner`, `triggerRadius` 9 m centered on the Vesting corridor (26, 12, 111) |

**Morrigan's Spine has no south wall by design.** The builder calls `BuildWall` for `MorriganSpine_WallW`/`WallE` and `BuildDoorwayWall` for `WallN` only — there is no `WallS` call. South face z=84 is intentionally open: this is the window Echo hands the climb off to (§4 Beat 1), not the `WallN` doorway the table above lists, and the summit route lands the player on `SummitDeck` (z≈98) inside the spine's open south footprint. A room-shell prefab reproducing this table must leave that face unwalled, not fill it in as a missing row.

All three spawners are built **armed-idle** (waiting on `Begin()`, not yet polling) and only start their 9 m proximity poll once the single **"Trigger: Activate the Three Towers"** mission step fires `TowerArm`'s `ActivationRelay`, which calls all three `Begin()`s at once via persistent listeners. From that point on, each tower's cadre+master activate **only when the player walks toward that specific tower's own corridor** — walking near the Cradle corridor cannot pull the Vesting fight early, and vice versa. This is the mechanism that makes the "any order" design work without a single scripted branch.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring) plus `WallClimbLocomotion` for the ascent and summit route. `ZoneBounds` is set to **center (0, 8, 90), radius 100** — a single bounding sphere loosely enclosing the forest slope through the farthest tower arena (the Proving, the chapter's deepest point at z=158). **What `EchoPresence` actually emits, chapter-wide:** per `EchoPresence.cs`, it is purely combat-reactive — it speaks only on an `EntityDied` (enemy killed), a low-health `EntityDamaged` (player hurt), or an `AbilityActivated` event, and stays silent while a mission `DialoguePlayer` is playing. The climb has no combat (§4 Beat 1 §d), so `EchoPresence` fires nothing during the ascent itself — the position-triggered route-call/idle-bark pool §4 Beat 1 §a promises for the climb ("Echo reading the rock the whole way up") is the separate, still-unimplemented pool §4 Beat 1 §e and §9 flag, not something `EchoPresence` already covers.

## 3. Global environment & backdrop

**The Iron Dojo reads as beautiful on purpose — that is the whole design brief.** Per the dialogue script's SETTING block: "Nothing here should look evil. That is the point." A mountain spur under an old-Earth-climate sky — breathable air, a real sun, weather that turns, pine forest on the lower slopes, snow on the high ridges — carved into three granite peaks, each crowned by a tower, terraced training yards stepping down between them, aqueducts threading the rock, bells ringing the hours over the central Iron Yard. Golden daylight, gentle bells, clean air; underneath all of it, a machine that erases children's names. The atmosphere pass has to sell warmth and craft the whole way up the climb, then let the interiors of the three towers curdle that same warmth into something clinical, brutal, or coldly analytical depending on which master's domain the player is standing in.

**No Cairn set this chapter.** Per the builder's own header comment (a documented **crew-presence decision**): canon frames this as a solo climb — "only one body fits the route… Cipher goes up alone on foot" — with the rest of the crew (Kessler, Resh, Iris, Mera Voss, Mira) holding comm from the ship for the entire chapter, the same "no body in the scene" convention Chapter 5 used for Iris/Mera Voss, extended chapter-wide. Their Beat 0 briefing lines and Beat 1 comm-lines are `DialoguePlayer` speaker labels only — no physical NPCs, no command-room set to build. Only Ronin-7 (the player), Morrigan, and the three masters get physical placement in this scene.

**The bells** are a recurring motif per the dialogue script's INTRUDING/RECURRING ELEMENTS block: gentle and regular through the climb and the towers (the `BellTower` prop at the Iron Yard's center, and implied ambience along the ascent), then in the evacuation they ring wrong — a single long unbroken call, realized in the builder as `PurgeAlarm`'s `EvacuationTimer` countdown prop, an ambient "bells ringing wrong" dressing element that never gates a fail state (trainees carry no `Health` component at all, so there is structurally no escort-objective failure path — see §9). **Neither ring is currently audible.** `BellTower` (§4 Beat 3, Appendix A.3) is a silent primitive with no `AudioSource` at all, and `PurgeAlarm`'s `EvacuationTimer.alarmSource` is left null — the chapter's single most-repeated diegetic element produces no sound today, only visual/implied presence (§9).

**The Iron Yard is the chapter's thesis space and is empty of children until they leave it.** The screenplay names "terraced gardens, children in ordered lines" as the citadel's recurring "beauty IS the horror" image (script 89-92), and the Iron Yard is explicitly the muster ground where the bells "order the hours and the children" (script 19, 93) — the central square this whole chapter is staged around. As-built, the Iron Yard is ground + the silent `BellTower` and nothing else until the two evacuation walkers appear in Beat 5 (§4 Beat 5 §b). A school full of children has no children visible in its own square until the moment they leave it. See §4 Beat 3 §c for a proposed row closing this gap.

**Backdrop / skybox.** The screenplay's SETTING block is insistent on the open sky — "breathable air, a real sun, weather that turns... snow on the high ridges" — and the evacuation closes against "the clean mountain sky" (script line 551). The Iron Yard, forest slope, and every tower approach are open-sky exteriors (§2) — roughly two-thirds of the chapter's footprint has no ceiling and reads the sky directly. As-built, the scene uses Unity's default procedural sky with no dedicated skybox asset, no weather system, and no authored snow-capped-ridge backdrop — §6's "real sky" backdrop entries describe the intended read, not a built asset. Flagged here for a dedicated mountain skybox (and, lower priority, a weather-turn pass) so the "beauty is the horror" open-sky read has an owner going into the art pass.

**The Beat 1 establishing image — "citadel golden above the forest slope" — has no built silhouette and is actively fogged out.** The screenplay's opening Beat 1 image is load-bearing: "Far above, golden in the afternoon light, the Iron Dojo steps up the mountain: terraced yards, a curtain wall, aqueduct spans, three towers crowning three peaks" — the thesis shot for "beauty is the horror," delivered as a look-up-from-spawn vista. §6's Forest Slope row asserts the backdrop is "the citadel visible high above," but no distant tower/peak silhouette geometry exists anywhere in the builder — the three tower arenas are enclosed boxes at z 106–158, nothing stands in for them at range — and the chapter's exponential fog at density 0.018 (Appendix A.1) attenuates anything that far out: forest spawn is z≈10, the nearest citadel massing would read at z≈90+, and e^(−0.018·80) ≈ 0.24, i.e. roughly 76% fogged even before accounting for the vertical distance. Placeholder geometry alone would wash out under the current fog curve. This needs either a low-density fog exception carved out for the establishing sightline, or a distant citadel-massing backdrop prop authored to read through the haze — without one, a player looking up from spawn sees golden fog and nothing else, undercutting the chapter's opening image before the climb even starts.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter6Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch6Environment.asset`. Its schema (identical shape to Chapter 1's, new instance):

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` per zone + per terrace |
| `eventLights[]` | — | unused this chapter (no red-alarm event light; `PurgeAlarm` is a `TextMesh`+`EvacuationTimer`, not a light) |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddAmbientPulse("SpineLight0", periodSeconds: 6.2f)` and `AddConsoleFlicker("IronYardLight0", seed: 55f)` calls with data.

The thirteen accent-light entries (5 terrace patrol lights + `ForestLight0` + `SpineLight0`/`SpineLight1` + `IronYardLight0`/`IronYardLight1` — 10 built today — plus the three proposed tower-interior lights `CradleLight0`/`ProvingLight0`/`VestingLight0` — see §4 Beat 3 §c/§f, §6) are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass. Retuning the look afterward is a profile edit with no code change and no rebuild.

**Material / tint palette:** set-dressing props today are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials — this keeps the chapter's draw-call count low and keeps regeneration cheap if colors need retuning. Each zone carries its own accent color per the builder's tower-differentiation scheme (`cradleColor` (0.45, 0.35, 0.4) — warm rose-grey, the "nursery" register; `vestingColor` (0.3, 0.35, 0.45) — cold clinical blue-grey; `provingColor` (0.4, 0.3, 0.25) — hard rust-brown), a deliberate color-coding of each master's domain that a prefab pass must preserve or intentionally supersede, not lose by accident. **Prefabs replacing primitives must carry their own materials and will not batch this way** — the perf cost the budget clause (§1.6) exists to police.

## 4. Per-beat scene spec

The chapter plays as six beats: a voice-only briefing, the climb, Morrigan's kill-list, the three towers (taken in any order), Kaelen's confession, and the evacuation. Each beat is documented with the same a–f structure used in every other chapter's Scene Construction doc.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the Y-invariant.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the prefab is already on disk via the Tripo pipeline.
- All positions below are **world-space** unless marked "local to `UpperCitadel`" — remember the +12 Y offset (§2) when reasoning about the as-built local coordinates in Appendix A.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> This beat builds **no physical set** — do not add a Cairn command-room shell for Chapter 6; that set belongs to Chapter 1/4/5's builders. Your objective is only to separate the dialogue wiring (`BuildBeat0Logic()`) from nothing, because there is no `BuildBeat0Art()` to write. If a future patch adds a physical Cairn cutaway to this chapter, that is a scope decision for the writers' room, not an oversight to silently fix here.

#### a. Narrative purpose & emotional target

For once Resh is not reading a job — he is settling a debt he has carried for years, naming the one address every child he has ever pulled from the markets was tagged for: the Iron Dojo. This is the chapter's mission statement delivered as an ensemble scene, not a solo brief: Mera prices the garrison with a hunter's flat competence, Iris wants the citadel's tech with the conscience of someone who knows exactly what that tech does to people, Kessler counts the bodies it will cost and keeps Mira sealed aboard rather than let her near what the place is. Mira's two lines — "It looks like a school" and "But the other children. Somebody's getting them out." — do the chapter's thematic work in the fewest words in the whole script: the youngest voice in the room states the horror and the mercy in one breath, uncomplicated by adult rationalization. Ronin-7's "Somebody is." is the first thing he says this chapter like a vow, and "We go shut down the place that's still turning out more like me" turns dread into a heading — his habitual move, established since Chapter 1's ultimatum and repeated at Chapter 5's grave.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** `BuildRig` runs unconditionally, with no `SetActive(false)` — it is not scene-gated to appear only once Beat 0 ends, and it is built *earlier* in `BuildChapter6IronDojo()`'s call order than Beat 0's own dialogue player. At runtime the rig exists at its spawn point from frame 1, so the briefing is the first thing the player hears while already standing on the forest slope (spawn (0,0,2), §4 Beat 1 §b — right beside the Beat 0 dialogue anchor at (0,1,4)), before any locomotion/objective is available — not a bodiless briefing state. "Not yet spawned in the physical scene" describes narrative framing, not runtime fact; the beat's dialogue anchor position is chosen independent of rig placement, but the rig is already there to hear it.
- **No physical NPCs.** Resh, Mera Voss, Iris, Kessler, and Mira are `DialoguePlayer` speaker labels only, per §3's crew-presence decision — there is nothing here to spawn, ground, or wander.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` — set `ch6_beat0_briefing`, 12 lines — plays in full before the player is dropped on the slope |

**What changes during the beat:** nothing in the world — there is no world yet from the player's point of view. This step exists purely to seat the mission's premise before Beat 1 hands over control.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(none — no physical set this beat)* | — | — | — | — |

**If a future cutaway pass is ever scoped (§9), the single highest-value element to build is the holo-table's turning citadel projection.** The screenplay's Beat 0 image is specifically "the salvaged holo-table throws up a mountain citadel turning slowly in pale projection, three peaks and a central yard" (script 37-38) — "beauty is the horror" delivered at scale before the climb even starts. This connects to a second, separately-flagged gap: §3's Beat 1 note that no distant citadel silhouette exists at range and the fog curve washes it out even if one did. A single turning-hologram asset built once could conceivably serve both the Beat 0 cutaway (if ever built) and, re-skinned as a distant massing prop, the Beat 1 establishing sightline — one potential asset closing two flagged gaps, not a scope decision made here.

#### d. Combat

None.

#### e. Dialogue / VO

Dialogue set id: **`ch6_beat0_briefing`**, built at (0, 1, 4), 12 lines, advanced on **Left-Hand "Talk" (Y)**. Full line set (Speaker | Line | seconds), per `Chapter6Lines.GetBeat0BriefingLines()`:

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Resh | Every kid I ever pulled out of those markets was tagged for one address. This one. They don't sell children here. They make them. Whole. | 14 |
| Resh | One card left to play. A Program engineer inside the walls, been slipping me children for years and never asked for anything back. A name and nothing else. Morrigan. | 13 |
| Mera Voss | Then understand what the name buys you. This isn't a free-port, Resh. Real soldiers. Real air cover. Nowhere to disappear if it goes wrong. | 12 |
| Iris | That spine is where they do everything. The switches, the bonding, the conditioning. All of it, one mountain. Whatever they did to Cipher, they do it here, to a new batch every season. I want it read. | 15 |
| Kessler | I hear all of that, and I'm counting the bodies it costs. Tell me why we walk into the Dominion's own house instead of around it. | 11 |
| Resh | Because around it just means more of them grow up into the thing we keep fighting. You want to stop bleeding downstream. This is the top of it. There's nothing above this. | 12 |
| Mira | It looks like a school. | 2 |
| Kessler | It's made to look like one. That's the trick of it. And it's why you're staying sealed aboard with the hatch shut and the comm open. | 11 |
| Mira | Okay. But the other children. Somebody's getting them out. | 3 |
| Ronin-7 | Somebody is. | 1 |
| Ronin-7 | We go shut down the place that's still turning out more like me. | 5 |
| Kessler | All right. We do it Resh's way. Plot the slope, drop him low and far, let the mountain be the door. | 10 |

Total runtime ≈ 109 s (summed from the listed durations).

**Editorial note:** `Chapter6Lines.cs` documents that `story ouput/audit/Ch06_audit.md` graded the source script C- on naturalness (13 "not X, it's Y" antitheses, tricolon/aphorism-stacking) and that every flagged line in this set has already been rewritten per the audit's recommendation — the table above is the **shipped, post-fix text**, not the raw dialogue-script prose quoted in the screenplay excerpt.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — this is a pure dialogue beat with no combat and no traversal.
- No ambient bed is built for this beat specifically; whatever room-tone the eventual physical-Cairn scope decision (§9) would carry is out of scope for the current builder.
- Comfort vignette is inert — the player has no locomotion input available yet.

---

### Beat 1 — The Drop & the Climb (Forest Slope → Mountain Face)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (forest ground + pines, 5 terrace decks + rocks + patrol lights, 4 connecting ramps, the summit hold-ladder + deck + bell) and **`BuildBeat1Logic()`** (the two reach-gated dialogue anchors, the ascent-midpoint and window reach points). **The `Climbable` marker pass is logic-adjacent but must run against whatever geometry `BuildBeat1Art()` produces** — do not let a prefab swap silently drop the climb kit's colliders.

#### a. Narrative purpose & emotional target

The wake at Beat 1 of Chapter 1 sold *who* Ronin-7 physically is; this climb sells *what he can do with a body nobody built him a leash for.* It is explicitly the mirror of Chapter 4's Drovis Deepworks descent — same partnership (Echo reading the rock, calling the routes), same "no full combat, light ambient threat" pacing — inverted: down became up, black water became open air and a long fall, and the destination is not an artifact but a person. Echo's tone is allowed its Chapter 4 warmth back for exactly this stretch ("the wry warmth allowed in for the climb before the towers take it away") — the last easy stretch of the chapter before three straight scenes of institutionalized cruelty. The climb's closing beat — "That's her window. No light, no guard, cracked open from the inside a while now… After this it's just you, the cold, and the woman in the dark" — hands the scene off from partnership-banter to solo confrontation in one line.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player rig:** spawns on the forest slope at world **(0, 0, 2)** — `BuildRig`'s shared on-foot default (`XRRigBuilder.cs`'s `OnFootSpawnZ` = 2, root position `(0,0,OnFootSpawnZ)`; Chapter 6 does not override it), drops via the boarding shuttle per the screenplay (no scripted drop animation — the rig simply exists at spawn; `BuildRig(refs, addLocomotion: true)` + `EchoPresence` + `WallClimbLocomotion` + `ZoneBounds` center (0,8,90) radius 100, authored once, chapter-wide). This grounds the blocking of the beat's clustered anchors: the katana sits 2 m ahead and 2 m to the side of spawn, the Beat 0 dialogue anchor 2 m straight ahead, and the Beat 1 climb-start anchor 8 m further ahead again — spawn, sword, and both dialogue anchors form one small forward-facing cluster at the base of the slope, not scattered blocking. **The katana rides from the start** — `BuildSword` places it at world (2, 1, 4) alongside the rig, per the builder's own comment: "no rack-wake beat, matching Ch5's 'cost, not initiation' precedent." Unlike Chapter 1's rack-wake weapon-pickup beat, there is no equivalent ceremony here; see §c's art table for the row.
- **Two `ReachTrigger` gates** bracket the climb rather than a single arrival point, unlike Chapter 1's single-`ReachTrigger`-per-room pattern:
  - `AscentMidReachPoint` at `Terrace2 + (0,1,0)` = (-2, 7, 50), radius 5 — a checkpoint partway up, gating nothing narratively but confirming the player is actually climbing rather than glitching past the ascent.
  - `WindowReachPoint` at `Terrace4 + (0,1,0)` = (0, 13, 80), radius 5 — Morrigan's window, the climb's actual destination.
- **No enemies, no full combat this beat** — per the design intent, "light ambient threat only (garrison patrols and lookouts to slip past, environmental hazard)." The five `PatrolLight`s dressing each terrace (§3.1, Appendix A.2) are the only "patrol" presence actually built; there is no patrolling NPC/AI to slip past in the current implementation — a gap between the screenplay's stage direction and the as-built scene, flagged for awareness in §9, not proposed as a fix here.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Climb` — set `ch6_beat1_climb`, 5 lines, at (0,1,10) — Kessler's comm hand-off + Echo's route-call opener, plays as the player begins climbing |
| 2 | ReachTrigger | Ascent Midpoint — gates on distance to `AscentMidReachPoint` (-2,7,50), radius 5 |
| 3 | ReachTrigger | Morrigan's Window — gates on distance to `WindowReachPoint` (0,13,80), radius 5 |
| 4 | Dialogue | `Dialogue_Beat1_Window` — set `ch6_beat1_window`, 1 line, at `Terrace4 + (0,1,0)` — Echo's "last reach" line, plays once the player has physically reached the window |

**What changes during the beat:** nothing in the set dressing — the ascent is static geometry from the moment it is built. The only state that changes is the mission director advancing through its two reach gates as the player physically climbs.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Forest ground slab | (0, -0.5, 10) | `Rooms.ForestGround` | `…/Art/Generated/Rooms/ForestGround.prefab` | **MISSING** |
| Pine ×6 | (-6,1.2,2), (6,1.4,4), (-7,1.1,12), (7,1.3,10), (-4,1.2,15), (4,1.1,16) | `Props.PineTree` | `…/Art/Generated/Props/PineTree.prefab` | **MISSING** |
| Terrace0 deck | (0, -0.2, 22) | `Rooms.TerraceDeck` | `…/Art/Generated/Rooms/TerraceDeck.prefab` | **MISSING** |
| Terrace1 deck | (2, 2.8, 36) | `Rooms.TerraceDeck` | `…/Art/Generated/Rooms/TerraceDeck.prefab` | **MISSING** |
| Terrace2 deck | (-2, 5.8, 50) | `Rooms.TerraceDeck` | `…/Art/Generated/Rooms/TerraceDeck.prefab` | **MISSING** |
| Terrace3 deck | (1, 8.8, 64) | `Rooms.TerraceDeck` | `…/Art/Generated/Rooms/TerraceDeck.prefab` | **MISSING** |
| Terrace4 deck ("WindowLedge") | (0, 11.8, 80) | `Rooms.TerraceDeck` | `…/Art/Generated/Rooms/TerraceDeck.prefab` | **MISSING** |
| Terrace rocks ×10 (2 per terrace) | offsets (∓3.5/±3.2, +0.5–0.6, ∓2/±2.5) from each terrace center | `Props.TerraceRock` | `…/Art/Generated/Props/TerraceRock.prefab` | **MISSING** |
| Ramp0–3 (tilted connectors) | midpoints between consecutive terrace centers, width 6 | `Props.MountainRamp` | `…/Art/Generated/Props/MountainRamp.prefab` | **MISSING** |
| `AqueductSpan` (upper-terrace spillway — sources `AqueductWaterAmbience`, §f/§7) | span following Terrace2→Terrace3, roughly (0,9,50)→(0.5,10.5,72) | `Props.Aqueduct` | `…/Art/Generated/Props/Aqueduct.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `SummitHold0`–`4` (grip ladder) | (-3.5,12.70,100.22) → (-2.7,15.30,100.22 area), alternating x | `Props.ClimbHold` | `…/Art/Generated/Props/ClimbHold.prefab` | **MISSING** |
| `SummitDeck` | (-3.1, 15.7, 98.0) | `Rooms.SummitDeck` | `…/Art/Generated/Rooms/SummitDeck.prefab` | **MISSING** |
| `SummitBell` + `SummitBellPost` | (-3.1, 16.15, 97.0) / (-3.1, 16.6, 97.0) | `Props.SummitBell` | `…/Art/Generated/Props/SummitBell.prefab` | **MISSING** |
| `ForestLight0` | (-4, 2.2, 8) | — | `ChapterEnvironmentProfile.accentLights["Forest0"]` | profile |
| 5× `TerraceN_PatrolLight` | terrace center + (0,2.4,0) | — | `ChapterEnvironmentProfile.accentLights["TerraceN"]` | profile |
| `ForestGardenAmbience` | (-4, 2.2, 8) | — | `AudioSource`, inner 6 / outer 24 / vol 0.4 (no prefab) | audio |
| `WindAmbience` *(proposed — see §4 Beat 1 §f, §7)* | upper ascent, e.g. Terrace3/Terrace4 span ~(0.5, 10.5, 72), rising volume with altitude | — | `AudioSource`, proposed radii/volume TBD (no prefab) | **MISSING** |
| Katana (Echo's blade) | (2, 1, 4), rot Euler(-90,0,0) | `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** — rides from the start, no rack-wake beat (`Chapter6Builder.cs:225`) |

**Notes on the transition.** The `Climbable` marker pass (`AddDojoSummitRoute`, currently a separate patch function called from inside the main builder) walks the entire `IronDojo` hierarchy post-hoc and tags anything named `BellTower`, `TerraceN` (a deck), or `TerraceN_RockM` with a `Climbable` component **if it already has a `Collider`.** A registry-driven refactor that swaps a terrace deck for a prefab must preserve both the exact object *name* (`Terrace0`…`Terrace4`) and a live collider on it, or the tagging pass silently finds nothing to mark and the ascent becomes unclimbable with no error logged. **This is the single highest-risk regression in this chapter's art migration** — flag any prefab swap here for manual verification (§8) rather than trusting the automated pass alone.

#### d. Combat

None. Per design intent this is a traversal showcase, not a combat encounter — "light ambient threat, no full combat."

#### e. Dialogue / VO

Two dialogue sets, both advanced by **Left-Hand "Talk" (Y)**:

- **`ch6_beat1_climb`** (`Dialogue_Beat1_Climb`, at (0,1,10), 5 lines, ≈43 s): Kessler's comm hand-off ("Past the treeline you're on the rock and on your own feet. Climb careful.") → Echo re-establishing the Chapter 4 partnership ("Same as the caves, only this time we go up… Let me drive the route. You keep the blade.") → Ronin-7's flat mirror-line ("Drovis was a climb down. This one's up.") → Echo's route-call/theme-landing line ("She's at the top, and so is everything they buried in you… Who climbs a mountain to break IN?") → Ronin-7's "Then I'm nobody. Get me up there."
- **`ch6_beat1_window`** (`Dialogue_Beat1_Window`, at `Terrace4+(0,1,0)`, 1 line, 12 s): Echo alone — "That's her window… Last reach, Cipher. After this it's just you, the cold, and the woman in the dark."

**Production note carried over from the dialogue script, not yet realized in the builder:** the screenplay calls for a pool of **position-triggered Echo route-calls and idle/ambient barks** fired at specific points along the climb (route reads, hazard warnings, patrol callouts, beauty-of-the-place reactions) — explicitly flagged in the script as *"representative samples… the FINAL set of Echo barks and their exact trigger points are to be authored once the Climb's level geometry is designed."* **The current builder does not implement this bark pool** — only the two bracketing `DialoguePlayer`s above exist, and `EchoPresence` (§2) does not fill the gap either, being combat-reactive only and silent through a combat-free climb. This is a scoped, acknowledged gap, not an oversight (§9).

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the climb's vertigo and the "long fall into pine and air" hazard read entirely through height, wind audio, and the `WallClimbLocomotion` fling-on-release mechanic, never through moving the player's view.
- **Ambient bed:** `ForestGardenAmbience` loops at (-4, 2.2, 8), inner radius 6 / outer 24 / max volume 0.4 — a garden/forest bed under the lower half of the climb.
- **Missing sensory layer — wind.** The bullet above credits "wind audio," alongside height and the fling mechanic, as one of the three things selling the climb's vertigo and long-fall hazard, but no wind ambient source exists anywhere in the audio manifest — only `ForestGardenAmbience` (a garden bed) and the proposed `AqueductWaterAmbience` (§7) are specced for this beat. Canon leans on wind harder than water for this stretch: "Wind. A clean sun. A very long way down" (script 198), Echo's "a real sky to fall out of" (206), and "Wind's coming up the gorge, wait for the lull before the jump" (script 229) — the last of which implies gusts the player times a jump against, which a static bed cannot deliver. Add a proposed `WindAmbience` (rising with altitude, or a dedicated high-terrace bed) to §7 and the Beat 1 §c art table with `MISSING` status, the same way `AqueductWaterAmbience` is flagged — otherwise this beat's comfort contract promises a sound layer with no owner.
- **Missing sensory layer — running water, and nothing to source it from.** Echo's route-call names "Left of the spillway, then the wall," and both canon SETTING blocks put "aqueducts thread the rock" through the climb, but no water ambient — and no aqueduct/spillway geometry at all — exists anywhere in the manifest; only `ForestGardenAmbience` and (further up) `MorriganSpineAmbience` are built. As specced, the water sound would emit from nothing, the same disconnect this document refuses to allow for the Cradle lullaby (§4 Beat 3 §c insists the reading-voice ride the visible `IntakeNameSlate`). §c above now carries a proposed `Props.Aqueduct` row (upper-terrace span, ~z 50–72) for exactly this reason: `AqueductWaterAmbience` should sit on that prop, not float free, making the spillway Echo references both visible and audible — same "sound on the visible prop" rule. Noted as `MISSING` per §1.5's fallback-rule convention, not silently absent (§7, §9).
- **Lighting:** `SpineLight0` (near the top of the ascent, at world (-3, 14.4, 92)) carries `AmbientPulse(period: 6.2s)`; the five terrace `PatrolLight`s carry no behaviour (steady warm light, `None`) — the climb stays visually calm so the one pulsing light at the top reads as "you're near something alive again" after a stretch of static terrace lighting.
- **The five `PatrolLight`s being steady drops the screenplay's signature traversal-tension image.** Canon's climb is timed against a *moving* light, not a static one: "swings under it as a patrol light sweeps the terrace above and moves on" (script 225), with a sample Echo bark built around waiting it out — "Patrol light, hold under the span, let it pass" (script 229). The "steady... visually calm" justification above is a real design rationale, but it trades away the only thing making the combat-free climb tense rather than a scenic walk, and it compounds the already-flagged absence of a patrolling NPC (§b above). Two ways forward, either is legitimate: (1) add a `Sweep`/`Patrol` behaviour to `ChapterEnvironmentProfile.behaviour` (a rotating or oscillating cone per terrace light) so "hold under the span until it passes" exists as real gameplay, timed to the Echo bark above once the bark pool (§9) lands; or (2) keep the lights steady and record that decision here explicitly as a scoped omission rather than let this document read as if steady lights were the intended design. As written today it reads as (2) without saying so — this note exists to make that an explicit call, not an accident.
- **Comfort:** `WallClimbLocomotion`'s hand-over-hand grip model freezes stick movement and gravity while gripping (`ContinuousLocomotion.MovementSuspended`) and imparts a capped launch impulse on release (`maxFlingSpeed` 5.5 m/s) — this is the chapter's one departure from pure walk+snap-turn locomotion, and it is a **documented, deliberate exception** per §1.1, not a violation of the project's no-teleport/no-NavMesh rule. Comfort vignette still engages normally on snap-turns during the climb.
- **Missing haptic layer on the grip/release, the chapter's one interactive traversal mechanic.** The comfort and audio spec above covers the climb's movement-suspend and fling-on-release, but nothing here specifies `Haptics` — every combat beat's §f leans on it, and the project's no-camera-shake rule (§1.1) exists precisely so feel is carried by `Haptics`/`AudioDirector`/the reticle instead. Add a short grip-confirm pulse on each `SummitHold`/`Climbable` grab and a stronger pulse on fling-release, so the climb has the same tactile feedback contract as every other interactive system in this document — a silent grab currently reads as a dropped input in a headset.
- **Open question — no fall-recovery behavior is specified for the ~12 m-tall switchback (max ~12 m fall from the top terrace, over ~66 m of z-travel), and the hazard scopes almost entirely to the optional grip routes.** The ascent rises y=0→12 (§2's own "z 18→84, y 0→12," Appendix A.2's terrace centers at y 0/3/6/9/12) — the "~80" figure in §1.6's performance-budget note is the combined z-span/lateral footprint, not a vertical measurement; the worst-case fall off Terrace 4 is ~12 m, not 80 m. A 12 m fall is still a real VR-comfort event worth the sign-off below, but the number should stay true to §2's geometry. The wide `Ramp0–3` connectors a walking player rides up the mandatory spine (§1.1) present almost no fall exposure by comparison — this hazard really belongs to the `Climbable`-tagged terrace faces and the summit hold-ladder, the optional parkour layer, not the walk-only completion path. Canon frames the falls themselves as the climb's hazard ("long falls into pine and air," "loose-rock and weather hazards"), and this document leans on that verbally (§a), but nothing in the builder specifies what happens when the player actually falls off the ascent (on either route). `AscentMidReachPoint` (-2,7,50) is described above as "a checkpoint partway up," yet nothing respawn-related is wired to it, and `ZoneBounds` is a boundary sphere, not a catch. In VR an uncontrolled long fall is a comfort/sickness event, so this absence is load-bearing, not cosmetic. Flagged for sign-off (§9), not resolved here: does the climb want a kill-floor + respawn at the last-reached terrace (reusing `AscentMidReachPoint`/`WindowReachPoint` as respawn anchors), a soft re-grip catch, or genuinely nothing — and if nothing, that should be an explicit decision, since "hazard with no consequence" is itself a design call.
- **A second, smaller drop the fall-recovery question above doesn't cover: the summit route's own landing.** `SummitDeck` sits at world (-3.1, 15.7, 98) (Appendix A.2), roughly 3.7 m above Morrigan's spine floor at world y=12 across z[84,100] (Appendix A.3) — a player who takes the summit hold-ladder to its top lands on the deck, not on the spine floor they still need to reach to talk to Morrigan at (0,12,97). That 3.7 m is a real, uncommanded vertical delta on the sanctioned traversal path, not an off-route fall, and it needs its own answer: a stepped ledge down from the deck, a short re-grip section, or an intentional soft-drop, rather than being left as an implicit fall players simply step off of.

---

### Beat 2 — Morrigan's Room (The Kill-List)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (the spine room shell, cracked-slate holo dressing, the doorway to the Iron Yard) and **`BuildBeat2Logic()`** (Morrigan's placement, the two dialogue anchors, the tower-activation Trigger step).

#### a. Narrative purpose & emotional target

Morrigan does not startle when Cipher comes through the window — she has been waiting a long time for someone like him. This is a two-hander in the mold of Chapter 1's Beat 2 (Kessler/Ronin-7 in the Main Hold): a still, dialogue-heavy scene that reframes everything the player has climbed toward. Her self-indictment ("Nine years, one child out the bottom whenever I could stomach the math. Don't call that a clean conscience. It's just what I pay myself to get out of bed.") establishes her not as a righteous ally but as a woman with no illusions about her own complicity — which is exactly what makes her trustworthy. The kill-list itself is delivered as a schematic, not a speech: three towers, three masters, named with the exact weight of what each one does to children. Ronin-7's one-word interjection — "The children." — and Morrigan's immediate, hard answer — "Not yours to touch, and you knew that before you said it" — states the chapter's central moral rule before a single tower unlocks: the blade is for the cadre, the garrison, the machines, never the trainees.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Morrigan spawn:** (0, 12, 97), fixed in place — `Ch6PlaceStoryNpc(Ch6MorriganPrefab, pos, "Morrigan", wanderRadius: 0f)`. Unlike Kessler in Chapter 1, **Morrigan never travels** — `wanderRadius: 0` means no `StoryNpcWander` component is even added, and she has no `NpcWalker` leg anywhere in this chapter. She is waiting in her spine from the moment the scene loads and stays there through the whole chapter (including the Beat 5 return).
- **Blocking note — no authored rotation.** `Ch6PlaceStoryNpc` sets only position, never rotation (`Chapter6Builder.cs:228`), so Morrigan's facing today is whatever the prefab's default orientation happens to be. The player arrives either through the south window (the climb-in point at z=84, §2) or, on the sanctioned summit route, drops onto `SummitDeck` at z≈98 — north of her. For a still, held two-hander built on "she does not startle when he comes through the window" (script 244), her facing sells the "she's been waiting" read: she should face south, toward the window/spine door, not an unspecified prefab default.
- **Player:** arrives via the climb (Beat 1's last reach) and physically walks through the window into the spine — no scripted teleport-in, no cutscene camera cut backing it in the builder (the screenplay's "CUT inside" is a narrative transition, not an engine event).
- **`TowerArm` activation:** built inactive at build time (§4, Beat 3); this beat's closing Trigger step is what flips it — the moment the towers become reachable is the last line of Beat 2's kill-list dialogue, not a separate beat boundary.
- **Any-order legibility at the yard.** The kill-list's "take them in whatever order you want. All three are open" (`ch6_beat2_killlist`, line 2) only lands as a deliberate spatial reveal if the player can read three distinct destinations at a glance from yard-center the instant step 7 arms the towers — each corridor mouth (Cradle west/x−26, Vesting east/x+26, Proving north/z+131) needs a legible landmark visible from (0,12,111), not just the per-tower accent colors, which only read once a player is already inside a corridor (§3.1). Fold into the tower-shell/corridor prefab pass alongside the accent-color constraint (§4 Beat 3 §c) rather than treating it as automatically solved by the color scheme.
- **Proposed — step 7's one diegetic confirmation: the holo-slate brightens.** Both the bullet above and §6's Iron-Yard row currently note "no visual change in the yard itself" when the towers arm — true of the yard, but canon stages an explicit visual confirmation elsewhere, on Morrigan's own slate, the instant the kill-list ends: "the three towers separate and brighten, each one selectable. The bridges and the cable-tram between the peaks light up as the routes" (script 293). Step 7 should flip a brighten-state toggle on `Props.HoloSlateCluster` (§c below) alongside arming the three spawners, so the moment the player is handed the free-order choice has an in-world beat, not just a dialogue line finishing. This needs no `ActivationRelay` change — `AuthorTriggerStep` already accepts multiple targets (step 12 passes three, `alarmGo, walker0, walker1`, `Chapter6Builder.cs:403`), so the slate object can ride step 7 as a second target alongside `towerArmGo`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 5 | Dialogue | `Dialogue_Beat2_MorriganMeet` — set `ch6_beat2_morrigan_meet`, 4 lines, at (0,13,96) |
| 6 | Dialogue | `Dialogue_Beat2_KillList` — set `ch6_beat2_killlist`, 5 lines, at (0,13,97) |
| 7 | Trigger | Activate the Three Towers — activates `TowerArm`, whose `ActivationRelay` calls `Begin()` on all three `EnemyWaveSpawner`s simultaneously. **Proposed:** the same step also targets `Props.HoloSlateCluster`'s brighten-state toggle (§b above), the same variadic-target pattern step 12 already uses |

**What changes during the beat:** at step 7, all three towers go from "built but dormant, unreachable" to "armed and proximity-polling" in one instant — nothing visually changes in the Iron Yard itself; the change is entirely in what happens if the player subsequently walks toward any of the three corridors. **Proposed exception:** the slate-brighten toggle above would put the one legible confirmation on Morrigan's `HoloSlateCluster` in the Spine, not the yard — the towers arm silently at range, but the player reads it on the prop the kill-list was just delivered from.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Morrigan's Spine shell (12×16, walls + doorway) | center (0,12,92) | `Rooms.MorriganSpineShell` | `…/Art/Generated/Rooms/MorriganSpineShell.prefab` | **MISSING** |
| Cracked-slate holo dressing (`BuildRoomDetails` accent) | center (0,12,92), half-extents (6,8) | `Props.HoloSlateCluster` | `…/Art/Generated/Props/HoloSlateCluster.prefab` | **MISSING** |
| Bound intercept-drive stack (by the door — screenplay detail) | near (0,12,99), unbuilt today | `Props.InterceptDriveStack` | `…/Art/Generated/Props/InterceptDriveStack.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| Morrigan (Tripo mesh) | (0, 12, 97) | `Named.Morrigan` | `Art/Generated/Characters3D/Named/Morrigan.prefab` | **EXISTS** |
| `MorriganSpineAmbience` | (0, 1.5, 92) *(as shipped — see §9 Y-offset flag)* | — | `AudioSource`, inner 4 / outer 16 / vol 0.4 | audio |
| `SpineLight0` / `SpineLight1` | (-3, 14.4, 92) / (3, 14.4, 98) | — | `ChapterEnvironmentProfile.accentLights["Spine0"/"Spine1"]` | profile |

**Proposed — the `HoloSlateCluster` brighten toggle (see §b's step-7 note above, this document's highest-priority open item).** The row above builds the slate as static `BuildRoomDetails` dressing only; realizing the step-7 reveal needs one additional piece — an emissive material swap or a child overlay object on `HoloSlateCluster` that activates alongside the tower-arm relay, so "the three towers separate and brighten, each one selectable" (script 293) has something to switch on. No new registry key: this rides the same `Props.HoloSlateCluster` prefab row, just with a second discrete visual state.

**Optional set-dressing note, lowest priority of this document's open items.** §2 documents `WallS`'s omission as deliberate — z=84 is the climb-in window, not a missing wall — but a fully open 12 m face reads as an unfinished room, not the screenplay's "a single dark window… cracked open from the inside" (script 231, 244). A window-frame/aperture prop spanning that opening (rather than leaving it fully unwalled) would make the canon "her window" legible as an actual window. Could be folded into the `Rooms.MorriganSpineShell` prefab spec above rather than commissioned as a separate registry key.

#### d. Combat

None. Morrigan is unarmed, non-hostile, and — like Kessler and Khall before her — carries no `Health` component; this scene cannot become combat regardless of player action.

#### e. Dialogue / VO

Two dialogue sets, both advanced on **Left-Hand "Talk" (Y)**:

- **`ch6_beat2_morrigan_meet`** (at (0,13,96), 4 lines, ≈33 s): Morrigan's opening ("Cipher. You came up the cliff. Good. Sit, don't sit, I don't care. We have a small window and a large mountain.") — the brusque, no-ceremony fragment carrying the "nine years of waiting with no patience left" read §a is built on — → Ronin-7 naming Resh as the reason for trust → Morrigan's self-indictment ("I can't empty this place alone. You can.") → Ronin-7's "Tell me where it breaks."
- **`ch6_beat2_killlist`** — the chapter's structural hinge (the tower/master breakdown every player's run is organized around) and one of the two sets downstream chapters reference for continuity, promoted to a full table below rather than paraphrased.

**`ch6_beat2_killlist`** (`Dialogue_Beat2_KillList`, at (0,13,97), 5 lines, per `Chapter6Lines.GetBeat2KillListLines()`):

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Morrigan | Three towers, three masters. The Cradle takes the youngest and unmakes them, Matron Hespa runs it, and she'll smile while she tells you it's kindness. The Proving breaks the rest into shape, Drillmaster Caradoc, no smile at all. The Vesting finishes them, the bonding, the switch, the last hollowing. That one's Master Kaelen, the architect. | 24 |
| Morrigan | Cut one tower and the school staggers. Cut all three and it falls, and there's no one left to stop me opening every door. Kill Hespa, kill Caradoc, kill Kaelen. Take them in whatever order you want. All three are open. | 15 |
| Ronin-7 | The children. | 1 |
| Morrigan | They're not yours to touch, and you knew that before you said it. The cadre, the garrison, the machines, cut all of it down. Not one trainee. The moment the third master falls, you come back here and I empty the mountain. | 14 |
| Echo | She's clean, Cipher, or as clean as anyone gets who lived this long inside it. I know what's in all three towers. Wherever you want to start, I'll walk it with you. | 12 |

Total = 66 s (summed from the listed durations).

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** This is the chapter's other pure two-hander dialogue beat, mirroring Chapter 1 Beat 2's "biggest set piece has no combat in it at all" pattern.
- `SpineLight0` carries `AmbientPulse(6.2s)` (established in Beat 1, keeps running here); `SpineLight1` carries no behaviour — one pulsing, one steady, the same "one live thing in a room full of cold machinery" read Chapter 1 used for the Medbay.
- No haptics scripted for this beat — the chapter's only two silent-of-combat-feel beats are this one and Beat 0.

---

### Beat 3 — The Three Towers (Player-Chosen Order)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Because all three towers are instantiated in a single pass regardless of play order, split by **zone**, not by "first/second/third taken": **`BuildBeat3Art()`** builds all three corridor+arena shells and their cadre/master placements as inert geometry; **`BuildBeat3Logic()`** wires all three `EnemyWaveSpawner`s, the `TowerArm` relay, and the `MultiObjectiveGate`. **Do not build three separate copies of a generic tower** — Cradle, Proving, and Vesting each have a distinct footprint, door-facing, and accent color; a "loop 3 times" refactor that collapses them into one templated call must still preserve those three per-tower deltas.

#### a. Narrative purpose & emotional target

Three self-contained sub-beats, written to read correctly in any order the player takes them, each opening with a traversal stretch across the curtain wall/bridge to the tower's mouth, running a combat gauntlet against that tower's adult cadre, and ending in the master's confrontation and death. Each tower gives the Program's machine a different face: Matron Hespa's genuine, unshakeable certainty that erasure is kindness (the chapter's central horror, delivered in a grandmother's register, never a villain's); Drillmaster Caradoc's unrepentant creed that cruelty is the only honest mercy, and the only master who wants the fight on his own terms; Master Kaelen's detached, curious intelligence — the only one of the three who has read his own schematic and still builds it, which is exactly why he is the right mouth for Beat 4's reveal. The children throughout are rescue objectives, never targets — the design intent is explicit that "the blade is for the cadre, the garrison, and the machines," and the one exception, an optional mercy beat against the Vesting's vested cadets (the trainees most like Ronin-7 himself), is the chapter's only spare condition against any enemy.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **`TowerArm`** — a single `GameObject` carrying `ActivationRelay`, built inactive, its sole target of the step-7 Trigger. Because `MissionStepKind.Trigger` only `SetActive(true)`s its target objects (it cannot call arbitrary methods), `ActivationRelay.OnEnabled` is wired via persistent listeners to all three spawners' `Begin()` methods — turning one activation into three independent proximity-arm events, the same idiom `HeatMeter.onThreshold` uses elsewhere in the project.
- **Three `EnemyWaveSpawner`s** (`CradleWaveSpawner`, `ProvingWaveSpawner`, `VestingWaveSpawner`), each built **active-idle** (not inactive — an inactive spawner cannot `StartCoroutine`, a lesson the builder's own comment attributes to Chapter 4's `HunterWave`), each with a 9 m `triggerRadius` centered on its own corridor midpoint, each waiting on its `Begin()` call before it starts polling for the player.
- **Two-wave structure per tower:** wave 0 = that tower's cadre (3 `Enemy`s), wave 1 = that tower's master (1 `Enemy`, custom `EnemyDefinition`). Each spawner's wave-0 bark is its tower's intro dialogue set (`dlgHespaIntro`/`dlgCaradocIntro`/`dlgKaelenIntro`) — these were "previously authored but unwired" per the builder's own comment, now wired as `Wave.bark` on each spawner's first wave.
- **`MultiObjectiveGate`** — a single always-enabled component (its `Died` subscriptions must be live *before* any tower activates, since the player could clear a tower faster than expected) watching all three masters' `Health` components. Fires `onAllComplete` — wired to `MissionDirector.AdvanceFromPrompt` — the instant the third master's `Health` reaches zero, **regardless of order**. This is the same "gate a null-`promptObject` Prompt step" idiom Chapter 4's `DuelYield.onAccepted` uses.
- **Tower-light death cue (proposed, not yet built — see §4 Beat 3 §c/§f, §6, §A.1, §9).** Each tower's new arena light (`CradleLight0`/`ProvingLight0`/`VestingLight0`, §c below) should extinguish, or dim to a low ember value, the instant its own tower's master dies. `Health.Died` is a plain C# event (`event Action`), not a `UnityEvent` — so this is a small sibling `MonoBehaviour` subscribing directly to that master's `Health.Died` in its own `OnEnable`/`OnDisable`, the exact same runtime-subscription idiom `MultiObjectiveGate` itself uses, one instance per tower, toggling or dimming its own `Light`. The dialogue script calls this out explicitly, twice — "A tower goes dark behind him. CUT to the bridge." after both the Cradle and Proving kills (script lines 351, 398) — and because the kill-list is free-order with no HUD tracker, "one tower dark → two dark → three dark" becomes the only diegetic readout the player gets that they are on the chapter's last master.
- **Trainees** — six decorative capsules (`Trainee_Cradle0`/`Cradle1`, `Trainee_Proving0`, `Trainee_Vesting0`, plus `Trainee_Eldest`/`Trainee_Young` reserved for Beat 5) placed inside/near each tower arena, **carry no `Health` component at all** — `Ch6BuildTrainee` builds a plain capsule primitive with a tint and nothing else, so `BladeDamager`'s `OnTriggerEnter` → `GetComponentInParent<Health>` chain structurally finds nothing to damage. The "never raise the blade to them" rule is enforced by absence-of-component, not by AI avoidance or a scripted foul state.
- **Cadre enemies** (9 total, 3 per tower) — built via the shared `BuildEnemy(pos, playerHealth, enemyDef)` helper, `SetActive(false)` immediately after (`Ch6BuildCadre`). All nine share **one generic `EnemyDefinition`** (`EnsureEnemyDefinition()`, the same default stats every other chapter's rank-and-file Dominion trooper uses) — see §9 for the canon/mechanic gap this creates against the dialogue script's differentiated cadre roles (warden-nurses, conditioning-enforcers, suppression drones, instructor-cadre, senior cadet packs, handler-cadre, vested operatives).
- **Master enemies** — `Ch6BuildMasterEnemy` instantiates each master's Tripo mesh, re-grounds it (§9's Y-invariant), synthesizes an `ArmR/Sword/Blade/BladeTip` chain the way `BuildDominionEnemy`'s fallback block does for rigged-less prefabs, and wires `Enemy` with a **per-master custom `EnemyDefinition`**:

| Master | maxHealth | damage | moveSpeed | attackCooldown | Extra components |
|---|---|---|---|---|---|
| Matron Hespa | 180 | 12 | 1.1 | 1.1 s | plain `Enemy` — the softest, most evasive-feeling master by the numbers |
| Drillmaster Caradoc | 240 | 20 | 1.6 | 0.7 s | `Enemy` + `PatternedDuelist` (default params) — "predicts repeated-side hits and refunds them," a mechanical fit for a drillmaster who reads a fighter's patterns |
| Master Kaelen | 280 | 22 | 1.3 | 0.9 s | plain `Enemy` — the tankiest, hits hardest, but moves at a mid pace befitting an architect, not a brawler |

  **Design decision recorded in the builder's header comment:** all three masters are kills — "no spare condition for the bosses" — so none use `DuelYield` (reserved for a yield-then-spare boss FSM, which none of these are).

**Mission-spine steps (spanning Beat 3):**

| Step | Type | What happens |
|---|---|---|
| 7 | Trigger | Activate the Three Towers — see Beat 2 |
| 8 | Prompt (null `promptObject`) | "Clear the Three Towers (any order)" — the mission sits here, doing nothing on its own, until `MultiObjectiveGate.onAllComplete` calls `MissionDirector.AdvanceFromPrompt` |

**What changes during the beat:** everything — this is the chapter's only combat content. Each tower transitions from dormant → armed (step 7) → wave 0 cadre active (proximity) → wave 1 master active (wave 0 cleared) → master dead → (once all three) step 8 auto-advances.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

**3A — The Cradle (Intake Tower)**

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Cradle corridor ground strip | center (-26,12,111) | `Rooms.TowerCorridorGround` | `…/Art/Generated/Rooms/TowerCorridorGround.prefab` | **MISSING** |
| Cradle tower arena shell (16×16, door east) | center (-46,12,111) | `Rooms.CradleTowerShell` | `…/Art/Generated/Rooms/CradleTowerShell.prefab` | **MISSING** |
| Cradle cadre ×3 | (-20,12,108), (-30,12,116), (-42,12,111) | `Enemies.DominionTrooper` | `…/Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** *(generic — see §9)* |
| Matron Hespa | (-46, 12, 111) | `Named.MatronHespa` | `Art/Generated/Characters3D/Named/Matron-Hespa.prefab` | **EXISTS** |
| `Trainee_Cradle_Lap` (the lap-child — screenplay: Hespa "sitting, calm, with a child on her knee"; stage direction "He lifts the child off her lap" on Ronin-7's "Step away from the child") | (-46,12,110) — adjacent to Hespa, not among the two arena-perimeter trainees below | `Props.TraineeUniform_Young` | `…/Art/Generated/Props/TraineeUniform_Young.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `Trainee_Cradle0` / `Trainee_Cradle1` | (-44,12,105) / (-48,12,117) | `Props.TraineeUniform_Young` | `…/Art/Generated/Props/TraineeUniform_Young.prefab` | **MISSING** |
| `CradleCrib` ×6 (ordered rows) | (-50,12,106), (-46,12,106), (-42,12,106), (-50,12,116), (-46,12,116), (-42,12,116) | `Props.CradleCrib` | `…/Art/Generated/Props/CradleCrib.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `NurseryMobile` ×3 (turning, overhead) | (-50,13.5,111), (-46,13.5,111), (-42,13.5,111) | `Props.NurseryMobile` | `…/Art/Generated/Props/NurseryMobile.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `SuppressionDrone` ×2–3 (drifting, overhead, non-combatant set-dressing — distinct from the unbuilt drone *combatant* archetype §9 flags for the Cradle cadre) | (-48,14.5,108), (-44,14.5,114), (-50,14.5,111) | `Props.SuppressionDrone` | `…/Art/Generated/Props/SuppressionDrone.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `IntakeNameSlate` (the name-crossing slate — screenplay: "a gentle voice reads names off a list and crosses them out one by one, replacing each with a number") — **should carry the spatial reading-voice `AudioSource`** (name→number reading + child echo), unifying the visual name-crossing and the conditioning-lullaby cue (§4 Beat 3 §f, §7) as one diegetic event rather than two disconnected specs | (-46,12.5,115) | `Props.IntakeNameSlate` | `…/Art/Generated/Props/IntakeNameSlate.prefab` | **MISSING** *(not yet in the builder — see §9)* |

**3B — The Proving (Trial Tower)**

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Proving corridor ground strip | center (0,12,131) | `Rooms.TowerCorridorGround` | `…/Art/Generated/Rooms/TowerCorridorGround.prefab` | **MISSING** |
| Proving tower arena shell (16×16, door south) | center (0,12,150) | `Rooms.ProvingTowerShell` | `…/Art/Generated/Rooms/ProvingTowerShell.prefab` | **MISSING** |
| Proving cadre ×3 | (0,12,126), (0,12,134), (0,12,146) | `Enemies.DominionTrooper` | `…/Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** *(generic — see §9)* |
| Drillmaster Caradoc | (0, 12, 150) | `Named.DrillmasterCaradoc` | `Art/Generated/Characters3D/Named/Drillmaster-Caradoc.prefab` | **EXISTS** |
| `Trainee_Proving0` | (3,12,144) | `Props.TraineeUniform_Cadet` | `…/Art/Generated/Props/TraineeUniform_Cadet.prefab` | **MISSING** |
| `Trainee_Proving_Wall` ×2 (proposed — wall-pinned witnessing trainees) | (-7,12,145), (7,12,145) | `Props.TraineeUniform_Cadet` *(reuse)* | `…/Art/Generated/Props/TraineeUniform_Cadet.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `SparringPit` ×2 (dug into stone) | (-4,12,146), (4,12,146) | `Props.SparringPit` | `…/Art/Generated/Props/SparringPit.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `LiveFireGauntlet` ×2 (rack) | (-6,12,154), (6,12,154) | `Props.LiveFireGauntlet` | `…/Art/Generated/Props/LiveFireGauntlet.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `ClimbingCourse` (real-drop climbing course) | (0,12,145) | `Rooms.ClimbingCourse` | `…/Art/Generated/Rooms/ClimbingCourse.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `DrillmasterDais` (Caradoc's raised stone) | (0,11.8,150) | `Props.DrillmasterDais` | `…/Art/Generated/Props/DrillmasterDais.prefab` | **MISSING** *(not yet in the builder — see §9)* |

**3C — The Vesting (Graduation Tower)**

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Vesting corridor ground strip | center (26,12,111) | `Rooms.TowerCorridorGround` | `…/Art/Generated/Rooms/TowerCorridorGround.prefab` | **MISSING** |
| Vesting tower arena shell (16×16, door west) | center (46,12,111) | `Rooms.VestingTowerShell` | `…/Art/Generated/Rooms/VestingTowerShell.prefab` | **MISSING** |
| Vesting cadre ×3 | (20,12,108), (30,12,116), (42,12,111) | `Enemies.DominionTrooper` | `…/Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** *(generic — see §9)* |
| Master Kaelen | (46, 12, 111) | `Named.MasterKaelen` | `Art/Generated/Characters3D/Named/Master-Kaelen.prefab` | **EXISTS** |
| `Trainee_Vesting0` | (44,12,105) | `Props.TraineeUniform_VestedCadet` | `…/Art/Generated/Props/TraineeUniform_VestedCadet.prefab` | **MISSING** |
| `BondingCradle` ×3 (shadow-AI bonding cradles) | (42,12,106), (46,12,106), (50,12,106) | `Props.BondingCradle` | `…/Art/Generated/Props/BondingCradle.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `KillswitchTable` ×2 (implantation tables) | (40,12,116), (52,12,116) | `Props.KillswitchTable` | `…/Art/Generated/Props/KillswitchTable.prefab` | **MISSING** *(not yet in the builder — see §9)* |
| `KaelenConsole` (screenplay: "calm at a console of his own design") — **position constraint:** must sit directly behind/adjacent to Kaelen's spawn (46,12,111), not the 2 m clearance implied by the coordinate below; see the staging note under §c below | (46,12,109) *(2 m south of Kaelen's spawn — move to abut (46,12,111) or move Kaelen's spawn to meet it)* | `Props.KaelenConsole` | `…/Art/Generated/Props/KaelenConsole.prefab` | **MISSING** *(not yet in the builder — see §9)* |

**Common accents:**

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Iron Yard ground | (0, 11.9, 111) | `Rooms.IronYardGround` | `…/Art/Generated/Rooms/IronYardGround.prefab` | **MISSING** |
| `BellTower` *(shares its literal name with the summit-route bell — the `Climbable` tagging pass tags both; see Appendix A.2's naming-collision caution)* | (0, 13.4, 111) | `Props.BellTower` | `…/Art/Generated/Props/BellTower.prefab` | **MISSING** |
| `IronYardLight0` / `IronYardLight1` | (-8,15,108) / (8,15,114) | — | `ChapterEnvironmentProfile.accentLights["IronYard0"/"IronYard1"]` | profile |
| `CradleLight0` (warm rose, matches `cradleColor`) | (-46, 14.5, 111) — Cradle arena center, ~2.5 m up | — | `ChapterEnvironmentProfile.accentLights["Cradle0"]` | **MISSING** — new, see §6/§A.1 |
| `ProvingLight0` (harsh rust, matches `provingColor`) | (0, 14.5, 150) — Proving arena center, ~2.5 m up | — | `ChapterEnvironmentProfile.accentLights["Proving0"]` | **MISSING** — new, see §6/§A.1 |
| `VestingLight0` (cold blue-grey, matches `vestingColor`) | (46, 14.5, 111) — Vesting arena center, ~2.5 m up | — | `ChapterEnvironmentProfile.accentLights["Vesting0"]` | **MISSING** — new, see §6/§A.1 |
| Muster-line trainees (proposed, Beats 2–3 only) — small rows of static decorative capsules standing in "ordered lines" (script 89-92) in the Iron Yard, reusing `Ch6BuildTrainee` (no `Health`, same "rescue, never target" convention), cleared/converted to the two evacuation walkers at step 12 | scattered in ordered rows across the yard, e.g. flanking (−6…6, 12, 105…118) | `Props.TraineeUniform_Young`/`_Cadet` *(reuse)* | `…/Art/Generated/Props/TraineeUniform_*.prefab` | **MISSING** *(not yet in the builder — see §3, §9)* |

**Staging constraints the prefabs must respect.** Each tower's accent color is a deliberate character read (§3.1) — a prefab pass must not flatten Cradle/Proving/Vesting into visually identical rooms. **All three arena floors currently share the same generic `spineFloor` tint (0.2,0.19,0.22) via `BuildFloorCeiling`** — only the `RoomDetails` accent and corridor strip carry each tower's per-color distinctiveness; the floor slab itself is undifferentiated. The Proving specifically calls this out: the screenplay gives it its own floor read — "the floor stained dark" from blood, "his own bloodied floor" (script 357, 386, 398) — that a shared grey slab cannot deliver. A blood-darkened floor tint for the Proving arena, distinct from the shared `spineFloor` value, should be added alongside the accent-color constraint above, so the prefab pass doesn't leave the one tower whose canon specifically calls out its floor with the same generic slab as the Cradle nursery. **The Cradle intro is currently unmotivated as blocked.** The dialogue script's BEAT 3A stages Hespa "sitting, calm, with a child on her knee," and Ronin-7's opening line, "Step away from the child," is the stage direction's "He lifts the child off her lap" acted out — but as-built, Hespa sits alone at (-46,12,111) with the two Cradle trainees ~6–7 m away at (-44,12,105) and (-48,12,117); there is no child at her knee for the line to act on. The `Trainee_Cradle_Lap` row above exists to close that gap — this is a **position** constraint, not just a prefab-fidelity one, and without it the chapter's central-horror tableau ("a grandmother's register, never a villain's") has no lap-child for the blade to lift past. **The trainee placeholder capsules are the highest-narrative-risk row in this whole document** to leave as bare tinted primitives: they are the chapter's entire "rescue, never the target" payoff, and a player reading them as generic grey capsules rather than children undercuts Beat 3's central moral beat far more than a plain grey wall would undercut Beat 1's climb. The interior prop rows added above (cribs/mobiles/name-slate/suppression-drones for the Cradle; sparring pits/gauntlet racks/climbing course/dais for the Proving; bonding cradles/killswitch tables/console for the Vesting) exist to back that same warning with something concrete to build: without them, each tower resolves to `{Tower}TowerShell` + cadre + master and nothing else — three identically-empty boxes distinguished only by an accent tint, which loses the "a fight you do not want to be having in a room this gentle" tension §a explicitly targets.

**`KaelenConsole` needs the same kind of position constraint as `Trainee_Cradle_Lap` above.** Beat 4's whole tableau is staged on Kaelen's body reading as fallen against the machine he built — "Kaelen is down against his own console" (script 437), "Master Kaelen is down against the console he built" (script 443) — but Kaelen's spawn (46,12,111) and the `KaelenConsole` row's coordinate (46,12,109) sit 2 m apart, not adjacent. For Beat 4's confession (a still tableau, no camera cut, no re-blocking) to read as intended, the console must be placed directly behind or beside Kaelen's spawn point, or Kaelen's spawn must move to meet it — this is a **position** constraint on the prop, not just a prefab-fidelity gap, the same class of fix `Trainee_Cradle_Lap` closes for Hespa's lap-child staging.

**The Proving is under-populated with the witnessing children its master-kill payoff leans on.** `Trainee_Proving0` alone is one trainee, against the Cradle's three (`Trainee_Cradle0`/`Cradle1`/`Trainee_Cradle_Lap`) and the general "trainee capsules are the highest-narrative-risk row" warning above — but the Proving specifically is the one tower whose emotional beat *is* the trainees watching: "The adolescent trainees stay pinned to the walls as rescue objectives, watching the man who broke them get broken… They just watch the hardest man in their world fall" (script 386, 398). The `Trainee_Proving_Wall` row above adds two more wall-pinned adolescents (reusing `Ch6BuildTrainee`/`TraineeUniform_Cadet`, no new registry key) so Caradoc's death lands in front of a small witnessing row rather than a single capsule.

#### d. Combat

**Cadre gauntlets:** each tower's wave 0 is 3 melee `Enemy` AIs sharing one generic definition, using the same `BladeDamager` EMA swing-speed model every other chapter's combat reuses (**existing system — reuse, don't reinvent**). Once wave 0's three `Health`s reach zero, `EnemyWaveSpawner` advances to wave 1 and activates that tower's master.

**Boss fights:** Hespa and Kaelen are plain `Enemy`s differentiated only by stat tuning (§b's table) — Hespa the softest and most fragile numerically (matching her "soft-voiced, certain she's kind" characterization with the least combat-imposing stat block of the three), Kaelen the tankiest (matching "the architect… detached fascination," the fight you have to grind through rather than out-duel). Caradoc alone carries `PatternedDuelist`, layering a "predicts and refunds repeated-side hits" mechanic onto the base `Enemy` AI without a bespoke duel FSM — the one master whose fight punishes button-mashing specifically, matching his "you kill with my spacing, my economy, my count" dialogue.

**The Vesting's optional mercy mechanic** (per the dialogue script's explicit callout: *"this is the ONE place in the citadel where a spare is mechanically available against an enemy, and only against the vested cadets… broken cadets who are spared sink down and do not rejoin the fight"*) is **not present in the current builder** — the Vesting tower's cadre are the same generic, kill-only `Enemy` instances as every other tower's cadre, with no spare/yield state machine wired onto any of them. This is a **scoped, documented gap** (§9), not a silent omission — flagging it here rather than proposing an ad hoc fix, since a spare mechanic likely wants `DuelYield` or a purpose-built variant, and that is a combat-system decision above a scene-construction document's scope.

#### e. Dialogue / VO

Three per-tower intro sets, each fired as its spawner's wave-0 bark the instant the player enters that tower's 9 m corridor radius. Carrying the chapter's central per-master characterizations and the cross-check target for per-tower blocking, all three are promoted to full tables below rather than paraphrased, per `Chapter6Lines.cs:187–230`.

**`ch6_beat3_hespa_intro`** (`Dialogue_Beat3_HespaIntro`, at (-38,13,111), 5 lines, per `Chapter6Lines.GetBeat3HespaIntroLines()`):

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Matron Hespa | There now. Don't mind the noise, my loves. Eyes on me. We were on our numbers, weren't we. | 6 |
| Ronin-7 | Step away from the child. | 2 |
| Matron Hespa | I don't hurt them. I take the hurting things away from them. I'm the kindest thing that ever happens to them. You walked in here with a sword. Which of us is the cruel one? | 16 |
| Echo | She means it, Cipher, that's the worst of it. She did it to a thousand kids and tucked every one in after. Don't let the soft voice slow your hand. | 11 |
| Ronin-7 | I had a number once. Somebody soft as you gave it to me. You don't get to keep doing it. | 7 |

Total = 42 s (summed from the listed durations).

**`ch6_beat3_caradoc_intro`** (`Dialogue_Beat3_CaradocIntro`, at (0,13,142), 6 lines, per `Chapter6Lines.GetBeat3CaradocIntroLines()`):

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Drillmaster Caradoc | Stand down, all of you. I've watched you cut through my floor, and I want a turn. You kill with my spacing, my count. That's my work standing in front of me with a sword. | 14 |
| Ronin-7 | I move like yours because you made me move like yours. Don't put your name on it. | 6 |
| Drillmaster Caradoc | Proud? There's no proud in it, boy. Some break and some get broke. Somebody's got to do the breaking or the galaxy eats them whole. I made you hard enough to stand here. You're welcome. | 14 |
| Echo | He ran the floor you were beaten on, Cipher. I logged every hour of it. Don't trade blows with him to prove a point. Just end it. | 9 |
| Drillmaster Caradoc | Should've known. Soft hands at the top, soft hands at the bottom. Go on. Do the honest thing, at least. | 9 |
| Ronin-7 | The honest thing is the children walking out of here. You don't get to be part of that. | 7 |

Total = 59 s (summed from the listed durations).

**`ch6_beat3_kaelen_intro`** (`Dialogue_Beat3_KaelenIntro`, at (38,13,111), 4 lines, per `Chapter6Lines.GetBeat3KaelenIntroLines()`):

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Echo | Look at them, Cipher. That's you, a year before the bay. You don't have to kill the ones still mostly children under it. The metal cadre, yes. Them, you can choose. | 12 |
| Master Kaelen | You can stop swinging. They'll keep coming until I tell them not to, and I'm not going to. Unless you're the rare thing the floor reports say you are. The hand that hesitates. Go on. Spare one. I'd genuinely like to see it. | 17 |
| Ronin-7 | They're children with your knives put in them. The knives are yours. So you're the one I came up here for. | 7 |
| Master Kaelen | My knives, yes, I'll own them. Hespa thinks she's a mother. Caradoc thinks he's a forge. Me, I've read the plans, and I build it anyway. Come and read it back to me. | 16 |

Total = 52 s (summed from the listed durations).

**`ch6_beat3_kaelen_intro` is the only one of the three intro sets that opens on Echo's voice rather than the master's own.** Hespa's and Caradoc's intro sets both open on the master speaking first; the Vesting's opens on Echo's spare-prompt line ("You don't have to kill the ones still mostly children under it. The metal cadre, yes. Them, you can choose."), immediately followed by Kaelen's "Go on. Spare one. I'd genuinely like to see it." This unique structure is the diegetic setup for the optional mercy mechanic — the dialogue actively invites a spare the scene cannot mechanically honor, since (per §b's cadre note and §9) all nine cadre share one generic `EnemyDefinition` with no spare/mercy state wired anywhere in the builder. The set's shape and the unbuilt mechanic are the same gap, not two separate ones.

**Cadre bark confirmation (per `Chapter6Lines.cs:187–230`):** none of the three wired sets above include the cadre bark line the dialogue script writes for each tower — the Cradle Warden's "Intruder in the halls... keep the little ones calm," the Proving Instructor's "Cadets to the wall, eyes down," and the Vesting Handler's "Vested forward, contain the operative" are each explicitly marked in the script as "representative of the [tower] cadre bark pool," but all three are absent from every wired `DialogueLine[]` array — each set opens directly on the master's own first line instead. The cadre have no wired voice at all on contact today, the same class of deferred-and-acknowledged gap as the Echo climb-bark pool (§4 Beat 1 §e, §9) rather than a subset folded silently into the intro sets.

**Staging divergence, not flagged elsewhere: the master-intro sets fire as each spawner's wave-0 entry bark, not as the screenplay's post-gauntlet confrontation.** §4 Beat 3 §e and Appendix A.4 wire `ch6_beat3_hespa_intro`/`caradoc_intro`/`kaelen_intro` as the first wave's `bark`, firing the instant the player enters that tower's 9 m corridor radius — before the cadre gauntlet, and before that tower's wave-1 master activation. But the three tables above are *confrontation* dialogue (Hespa's "Step away from the child" / "Which of us is the cruel one?", Caradoc's "I've watched you cut through my floor," Kaelen's "You can stop swinging"), and the dialogue script stages all three as post-gauntlet cutscenes fired once the cadre is down. §a above likewise describes each tower as "running a combat gauntlet… and ending in the master's confrontation." This is a deliberate compression, not a contradiction to resolve here: the master speaks his full characterization on approach, before the cadre fight rather than after it, so by the time the player actually duels him the confrontation lines have already played. Reader beware — §a's "ending in the master's confrontation" describes the screenplay's blocking, not when these lines fire in the as-built scene.

**Routing note carried from the dialogue script, not modeled by any builder state:** if the Vesting is taken before the other two towers, Kaelen is meant to be "broken and left mortally wounded but hold his confession until Hespa and Caradoc have also fallen and Cipher returns to him to finish it" — implemented as a return-trigger so the confession always plays as the third master's truth. **The current builder does not implement a per-tower "wounded but alive" intermediate state for Kaelen** — his `Health` simply reaches zero on the boss fight's normal defeat flow like any other master, and `Dialogue_Beat4_Confession` (Beat 4, fixed at his tower position) is gated purely by `MultiObjectiveGate.onAllComplete`, which already guarantees it fires only after all three deaths regardless of order. In practice this produces the *narratively correct outcome* (confession always plays last) through a simpler mechanism than the screenplay's "wounded, waiting" staging — worth flagging as a difference from the script's stage direction, not a bug, since the gate already achieves the required ordering guarantee (§9). **That guarantee is ordering only, not location.** The screenplay's "return to him to finish it" solves a spatial problem the gate does not: nothing requires the player to be standing at Kaelen's tower (46,13,111) when the third master dies. See §4 Beat 4 §b for the resulting gap when the Vesting is not taken last.

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point** in any of the three fights — combat feel is carried entirely by `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle, per the project's non-negotiable VR constraint.
- `IronYardLight0` carries `ConsoleFlicker(seed: 55)` — a deliberate "something's wrong with the power even here" unease bleeding into the otherwise-idyllic Iron Yard, planted before the player even enters a tower.
- **The three tower arenas need their own interior light, not just the shared `IronYardLight`s.** `IronYardLight0`/`IronYardLight1` sit at (-8,15,108)/(8,15,114) — near the Iron Yard's own center, not the Cradle (x=-46), Proving (z=150), or Vesting (x=+46) arena centers — and all three arenas are enclosed rooms with a real `RoomH` 3.6 ceiling the directional key cannot reach. Lit by Flat ambient (0.28,0.26,0.22) alone, the three master fights currently happen in near-dark, and the "warm-nursery vs cold-clinical vs hard-rust" register §3.1 establishes lives only in surface tint, not in light — undermining both combat readability and the "beauty IS the horror" payoff these rooms exist to deliver. Add `CradleLight0` / `ProvingLight0` / `VestingLight0` to `Ch6Environment.asset`'s `accentLights[]` (§4 Beat 3 §c, §A.1), each centered on its arena ~2.5 m up and colored to match `cradleColor`/`provingColor`/`vestingColor`. This raises the chapter's real-time light count from 10 to 13 — re-measure per §1.6 once added.
- **`BellTower` is currently silent.** The prop at the Iron Yard's center (Appendix A.3) has no `AudioSource` — the gentle hour-bells the dialogue script keeps as a constant background presence through the climb and towers exist as a visual prop only, with nothing generating the sound. Needs a spatial looping `AudioSource` (gentle bell bed) — flagged in §9 alongside `PurgeAlarm`'s missing evac-alarm audio.
- **No per-tower ambient bed exists — this audio spec is currently identical (silent) across all three towers.** Only `MorriganSpineAmbience` and `ForestGardenAmbience` (§7) are built as `AudioSource` ambience layers; the three arenas and the Iron Yard have none. Per the dialogue script's atmosphere for each room, a differentiated pass should add: **Cradle** — soft room-tone, turning-mobile chimes, and the conditioning-lullaby cue (script: "a gentle voice reads names off a list and crosses them out one by one, replacing each with a number, and the children repeat the numbers back like a lullaby") — the chapter's hero immersion beat, ranked above generic room-tone, and a single diegetic event with the `IntakeNameSlate` prop (§4 Beat 3 3A): the slate's name-crossing visual and the lullaby's reading voice are the same in-world thing and should share one spatial `AudioSource` on that prop, not a disconnected ambience bed; **Proving** — live-fire gauntlet cracks, distant impacts, hard stone reverb; **Vesting** — the bonding cradles' low clinical hum (the script names "the bonding cradles hum low" in both Beat 3 and Beat 4 — Beat 4 §f already leans on this cue with no source specified). Note these three beds as `MISSING`, the same way §1.5's fallback rule treats a missing prefab, not as a silent gap (§7, §9).
- 90 FPS is the design target for each tower fight (3 cadre `Enemy` AIs + 1 master `Enemy` + blade VFX, per tower); this is the chapter's most likely spot for a high-fidelity prop swap to break frame budget, matching Chapter 1's Beat 3 boarding-fight precedent.
- Comfort vignette engages normally through each tower's traversal-into-combat transition.

---

### Beat 4 — Kaelen's Confession (The Vesting, Top of the Tower)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> This beat builds **no new geometry** — it plays entirely inside the Vesting tower arena Beat 3 already built. Your objective is only to separate `BuildBeat4Logic()` (the confession dialogue anchor and its gate wiring) from nothing new to draw. If a future patch wants a dedicated "console Kaelen dies against" hero prop, that is additive art on top of the existing Vesting arena, not a new room.

#### a. Narrative purpose & emotional target

This is the saga's one disclosure of Ladder B rung 4 — the Program engineered emotionlessness on purpose, and empathy is the designed flaw, not damage done to the man who has it. Per the dialogue script's explicit framing, this must play only once, regardless of which tower the player took the Vesting in relative to the other two, so the reveal always lands as the chapter's (and the towers') final word. The scene is a formal confession structure: Kaelen names the buried premise ("You think the feeling is a wound. A thing that happened to you."), Ronin-7's two-word "Isn't it." carries the weight of every unanswered question since the Kerrax mercy in Chapter 4, and Kaelen's answer runs the full architecture of the lie — hollow is not a side effect, hollow is the product; the seam that holds weak in the rare ones is mercy, not damage; Ronin-7's hesitation was never a fault, it was "the prayer that didn't take." Ronin-7's "Then I'm exactly what you were afraid of" and Kaelen's dying "You're exactly what we made. And exactly what we couldn't stand to admit we made" closes the confession on the chapter's single most important line. Echo's closing bark — indicting its own old reading of that same hesitation as a fault — is the only voice left standing to carry the weight forward.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **No new NPCs, no new geometry.** Kaelen's body and the Vesting arena are already in place from Beat 3.
- **Gating:** the confession's dialogue player is a normal Dialogue-kind mission step — **not** itself gated by a separate reach trigger or trigger step; its position in the ordered `steps` array (step 9, immediately after step 8's `Prompt`) is what guarantees it only plays after `MultiObjectiveGate.onAllComplete` has already advanced the director out of the "clear the three towers" prompt. Because the gate only fires once all three masters are dead, step 9 structurally cannot play early.
- **Unflagged blocking gap: the gate guarantees order, not location.** `MultiObjectiveGate.onAllComplete` fires the instant the third master's `Health` reaches zero, wherever the player physically is at that moment — there is no `ReachTrigger` gating step 9 the way `MorriganReturnReachPoint`/step 10 gates the return to Morrigan in Beat 5. If the player kills Kaelen first and finishes the kill-list on Hespa (Cradle, x=-46) or Caradoc (Proving, z=150), step 9's `Dialogue_Beat4_Confession` fires immediately at Kaelen's fixed anchor (46,13,111) while the player is standing over the wrong body, up to ~92 m away — Kaelen's own corpse is off-screen and never witnessed. §c/§d below (and §a's "delivered over Kaelen's already-fallen body") describe the intended staging, which only holds when the Vesting happens to be cleared last. Two ways forward, either is legitimate: **(a)** insert a `KaelenReturnReachPoint` reach step at the Vesting before step 9 — mirrors Beat 5's `MorriganReturnReachPoint`/step 10 exactly (one GameObject + one `AuthorReachStep`), and is the screenplay-faithful "return to Kaelen to finish it" the script's stage direction calls for (§4 Beat 3 §e's routing note); or **(b)** treat the confession as intentionally position-independent VO and say so here plainly, rather than let §c/§d's "over his body" wording imply a staging the builder does not reliably deliver.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 9 | Dialogue | `Dialogue_Beat4_Confession` — set `ch6_beat4_confession`, 8 lines, at (46, 13, 111) — fixed at Kaelen's Vesting position regardless of when the Vesting was cleared; **not reach-gated** — see the blocking gap noted above when the Vesting isn't the last tower cleared |

**What changes during the beat:** nothing spatially — this is dialogue-only, delivered over Kaelen's already-fallen body **when the Vesting is the last tower cleared**; otherwise it plays as position-independent VO over the wrong body (see the blocking-gap bullet above).

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(none — reuses the Vesting arena built in Beat 3)* | — | — | — | — |

#### d. Combat

None. Kaelen is already down from Beat 3's boss fight; this beat is a cutscene-style dialogue exchange over his body — reliably, only when the Vesting was the last of the three towers cleared (§b).

#### e. Dialogue / VO

**`ch6_beat4_confession`** (`Dialogue_Beat4_Confession`, at (46,13,111), 8 lines, ≈81.5 s — one of the longest single sets in the chapter):

| Speaker | Line | sec |
|---|---|---|
| Master Kaelen | There. Now I can say the part I built my whole life around not saying. You think the feeling is a wound. A thing that happened to you. | 9 |
| Ronin-7 | Isn't it. | 1 |
| Master Kaelen | No. We built you hollow on purpose. A soldier who feels, hesitates. A soldier who hesitates, fails. So we reach in and cut it out, every time, by hand. The hollow isn't a side effect. We were aiming for it. | 18 |
| Master Kaelen | But you can't pour nothing into a vessel and seal it shut forever. Somewhere along the seam the weld stays weak, and in the rare ones, it opens. And what comes through the seam isn't damage. It's the thing we worked hardest to keep out. Mercy. | 21 |
| Master Kaelen | Your hesitation, the thing the floor reports flagged as a fault. It was never a fault. The seam was just giving way. That's all you are. The prayer that didn't take. | 15 |
| Ronin-7 | Then I'm exactly what you were afraid of. | 4 |
| Master Kaelen | You're exactly what we made. And exactly what we couldn't stand to admit we made. | 5 |
| Echo | I logged that second as a fault, Cipher, same as they did. Neither of us knew what we were looking at. | 8 |

Total ≈ 81 s.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the entire beat is a still, held moment; the only motion is Kaelen's dying breath and the bonding-cradles' ambient hum described in the screenplay (not a distinct scripted audio cue in the current builder — carried by whatever room-tone the Vesting arena's reverb zone provides; see §4 Beat 3 §f for a proposed dedicated bonding-cradle hum bed covering this same cue).
- No haptics scripted — matching Beat 0 and Beat 2, this is one of the chapter's silent-of-combat-feel beats, and here the silence is the point: the fight is over, the only thing left is the truth.
- `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` (run once, chapter-wide, at the end of the builder) auto-tags the Vesting arena as its own interior reverb volume — the confession should read acoustically distinct from the open-sky Iron Yard the player just walked out of.

---

### Beat 5 — The Evacuation (Return to Morrigan → the Iron Yard)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (the `PurgeAlarm` text prop, the complete canvas) and **`BuildBeat5Logic()`** (the return reach point, the evacuation Trigger, the two trainee walkers, the Iron Yard reach point, the closing dialogue chain, `ChapterOutro`).

#### a. Narrative purpose & emotional target

All three masters dead, Cipher returns to Morrigan, who has been waiting nine years for exactly this moment. The evacuation plan she lays out — "We're not carrying anyone. We let them walk, and we trust them to, which this place never once did. Eldest at the front and the back, counting heads the whole way down" — is deliberately the *opposite* of the Cradle's institutionalized control: dignity as the point, not an afterthought. The scene then does double duty as Ally #3's recruitment and the chapter's forward hook: Morrigan chooses to walk up the mountain rather than down with the children, naming two unresolved unease that will shape everything after — someone outside the Program is steering events (seeding Chapter 16's Obsidian Synod), and the citadel's own work traces back to one insider she means to find (seeding Chapter 13's Dr. Heris reveal). Ronin-7's closing line — "We came to break the place that keeps making me. It's broken. And whoever made it has someone over THEM. So we don't stop here." — turns the chapter's victory into the next chapter's premise in real time, the same move he made at the end of Chapter 1's ultimatum.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **Player:** no scripted path back to Morrigan's spine — the player walks/climbs there under their own locomotion, the same as every other traversal stretch in this chapter.
- **`MorriganReturnReachPoint`** at (0, 13, 96), radius 5 — gates the "you're back" dialogue.
- **`PurgeAlarm`** — built inactive at (0, 15, 111), carrying `EvacuationTimer` (a `TextMesh` countdown display + optional looping alarm `AudioSource`). Activated by the same Trigger step that starts the trainee walkers. Per the builder's documented **evacuation-timer decision**: this is purely an ambient "bells ringing wrong" countdown prop — it **never gates a fail state**. Trainees carry no `Health`, so there is structurally no escort-objective/`ProtectNpcObjective` failure path to wire; the timer dresses the scene, nothing more.
- **Bell hand-off, once both audio sources exist (§4 Beat 5 §f, §9).** The screenplay treats the Iron Yard's bell as one instrument transforming, not two: gentle-and-regular through the climb and towers → "a single long unbroken call" once the evacuation starts (script 495, 512) → silence once the towers fall (script 551) — never two beds sounding at once. As specced, `BellTower`'s proposed gentle loop (§4 Beat 3 §f) and `PurgeAlarm`'s proposed `alarmSource` wrong call would otherwise overlap after step 12 fires. The Trigger step 12 that activates `PurgeAlarm` should also stop `BellTower`'s gentle loop in the same step, and step 17's `ChapterOutro` should silence `PurgeAlarm.alarmSource` — so the sequence reads as one bell going wrong, then finally stopping, matching the mission-spine table above.
- **Two trainee walkers** (`TraineeWalker0` on `Trainee_Eldest`, `TraineeWalker1` on `Trainee_Young`), both inactive `NpcWalker`s built via the cross-chapter `BuildNpcWalker` helper (§1.2), sharing one waypoint list computed by walking the five ascent terraces **in reverse** (top to bottom) plus a start point near Morrigan's spine and an end point back on the forest slope:

```
descentWaypoints (shared by both trainee walkers):
  (0, 13, 96)                      // MorriganSpine landing, just past the door
  (0, 13, 80)   // Terrace4 + up1  // WindowLedge
  (1, 10, 64)   // Terrace3 + up1
  (-2, 7, 50)   // Terrace2 + up1
  (2, 4, 36)    // Terrace1 + up1
  (0, 1, 22)    // Terrace0 + up1
  (0, 0.5, 8)   // back on the forest slope
```

  **The shared first waypoint routes both walkers upslope into the spine before the descent begins.** The trainees spawn in the Iron Yard at (±2,12,113), but `descentWaypoints[0]` is `(0,13,96)` — inside Morrigan's spine, behind `MorriganSpine_WallN` (z=100) and north (higher z) of the trainees' own spawn. On activation both children first walk north into the spine, then back out through Terrace 4 and down the switchback — the "children walking free down the forest roads" beat visibly opens by walking into the fortress before it turns and descends (§9).

  Both trainees walk this **identical** waypoint list simultaneously once activated — there is no stagger, no "eldest leads, youngest follows a beat later" offset in the current implementation, despite the screenplay's "eldest at the front and the back, counting heads the whole way" staging describing bracketing, not synchronized parallel walking. Flagged for awareness in §9.
- **Unflagged blocking gap: Morrigan is physically absent from the Iron Yard farewell where her own recruitment dialogue plays.** Morrigan is welded to `(0,12,97)` in the spine (`Ch6PlaceStoryNpc`, `wanderRadius: 0`, no `NpcWalker`, no re-parent — §5) and never travels. But steps 14–16 (`Dialogue_Beat5_Descent` at (0,13,111), `Dialogue_Beat5_MorriganJoins` at (0,13,112), `Dialogue_Beat5_OutroHook` at (0,13,113)) all play in the Iron Yard, ~14 m north of her body and behind `MorriganSpine_WallN` (z=100, §2). The Ally #3 recruitment two-hander — "You're not going with them." / "They get a normal life. I don't, not yet." — plays with Morrigan's voice reaching the yard while she is, in fact, still standing in another room the player can turn around and see is empty of her. The screenplay stages her *in* the yard for this exchange: "Morrigan stands at the edge of the yard and watches the smallest of them disappear... then turns and walks toward the shuttle with Cipher" (script 512, 551) — this is the same class of blocking gap §4 Beat 4 §b flags for Kaelen's confession, just unflagged here until now. Two legitimate fixes, either closes the gap: **(a)** give Morrigan a Beat-5 walk/re-parent leg from the spine to the yard, fired by the same step-12 evacuation Trigger that starts the trainee walkers — this would also let the bound intercept-drive stack (§9) actually move "under her arm" as the screenplay stages it, one fix closing both gaps; or **(b)** explicitly document the yard farewell as voice-only-with-absent-body, and either relocate `Dialogue_Beat5_Descent`/`MorriganJoins`/`OutroHook` back to the spine near her (0,~13,97), or accept the divergence in writing. As currently documented, §a's staging implies a presence the builder cannot deliver.
- **`IronYardReachPoint`** at (0, 13, 111), radius 6 — gates the farewell dialogue once the player is back in the open yard.
- **`ChapterOutro`** at (0, 13, 114), inactive. `CampaignFlagSetter` flag `"ch6_complete"` wired to `OnActivated`; `completeCanvas` ref = the "CHAPTER 6 COMPLETE" world-space canvas at (0, 13.4, 115).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 10 | ReachTrigger | Return to Morrigan — gates on `MorriganReturnReachPoint` (0,13,96), radius 5 |
| 11 | Dialogue | `Dialogue_Beat5_Evacuation` — set `ch6_beat5_evacuation`, 3 lines, at (0,13,96) |
| 12 | Trigger | Iron Yard Evacuation Begins — activates `PurgeAlarm` **and** both trainee walkers in one step. **Proposed, once `BellTower`/`PurgeAlarm` audio lands (§4 Beat 3 §f, §4 Beat 5 §f):** this same step should also stop `BellTower`'s gentle loop, so the wrong call replaces the gentle bed rather than layering under it |
| 13 | ReachTrigger | The Iron Yard — gates on `IronYardReachPoint` (0,13,111), radius 6 |
| 14 | Dialogue | `Dialogue_Beat5_Descent` — set `ch6_beat5_descent`, 2 lines, at (0,13,111) — "You're not going with them." |
| 15 | Dialogue | `Dialogue_Beat5_MorriganJoins` — set `ch6_beat5_morrigan_joins`, 5 lines, at (0,13,112) — the two seeds, Ally #3 confirmed |
| 16 | Dialogue | `Dialogue_Beat5_OutroHook` — set `ch6_beat5_outro`, 2 lines, at (0,13,113) — the hunt widens |
| 17 | Trigger | Chapter Outro — activates `ChapterOutro` (flag `ch6_complete` + fade + complete canvas). **Proposed:** also silences `PurgeAlarm.alarmSource`'s wrong call here, so "the bells finally stop" (script 551) lands on this exact step |

**What changes during the beat:** step 12 is the beat's single visible event — the alarm prop activates and both trainees begin their scripted walk down the mountain simultaneously with the closing dialogue chain playing. Nothing else in the set dressing changes; the towers are not visually shown "coming apart in fire" as the screenplay describes (§9).

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `PurgeAlarm` text prop | (0, 15, 111) | — | `TextMesh` + `EvacuationTimer`, no prefab | logic-adjacent |
| `CHAPTER 6 COMPLETE` canvas | (0, 13.4, 115) | — | worldspace `Canvas`, built inactive | UI |
| Citadel-collapse VFX *(screenplay-only, not built)* | across the three tower positions | `Vfx.TowerCollapse` | `…/Art/Generated/VFX/TowerCollapse.prefab` | **MISSING** *(not yet referenced anywhere in the builder — see §9)* |

#### d. Combat

None. Any surviving garrison members implied by the screenplay ("before the garrison works out there's no one left giving them orders") are not modeled — every cadre `Enemy` this chapter belongs to one of the three towers and is already resolved by this point.

#### e. Dialogue / VO

Four dialogue sets close the chapter, all advanced on **Left-Hand "Talk" (Y)**:

- **`ch6_beat5_evacuation`** (3 lines, ≈42 s): Morrigan's "All three… you did it in an afternoon" → Ronin-7's logistics question ("Hundreds of them, and one shuttle. So how does it walk?") → Morrigan's dignity-first plan ("We're not carrying anyone. We let them walk, and we trust them to…"), the set's longest line at 22 s.
- **`ch6_beat5_descent`** (2 lines, ≈18 s): Ronin-7's "You're not going with them." → Morrigan's "They get to walk away from this. I don't, not yet… your leash didn't slip on its own."
- **`ch6_beat5_morrigan_joins`** — the longest set in Beat 5 and, with the kill-list above, one of the two sets downstream chapters reference for continuity (the two seeds land here), promoted to a full table below rather than paraphrased. *(Not the longest set in the chapter by either measure — `ch6_beat4_confession` runs longer at 81 s/8 lines, and `ch6_beat0_briefing` has more lines at 12.)*

**`ch6_beat5_morrigan_joins`** (`Dialogue_Beat5_MorriganJoins`, at (0,13,112), 5 lines, per `Chapter6Lines.GetBeat5MorriganJoinsLines()`):

| Speaker | Line (as shipped, post audit-fix) | sec |
|---|---|---|
| Morrigan | I've read everything that's come through this rock for years, and two things never fit. One, the Program's own actions don't add up to what happened to you. Someone outside the Program is steering this. I don't know who. But the pattern's not theirs. | 18 |
| Morrigan | Two. Everything built in this rock, the switches, the bonding, all of it, traces back through every revision to one mind. One insider on the Program's own side. I've chased him nine years and never got a name. I mean to. | 17 |
| Echo | That's the first time anyone's said out loud this is bigger than your handler and your switch, Cipher. She's not wrong, and she's the first one with the eyes to see it. Keep her. | 12 |
| Ronin-7 | Then you berth aboard the Cairn with the rest of us. There's a training bay in her belly we never named. We'll call it after the thing we just brought down, so we remember what it was for. | 14 |
| Morrigan | A dead leviathan full of strays, and now a rogue engineer too. Fine. Give me a bench and the time, and I'll take your leash apart. Then I'll take apart the man who designed it. | 13 |

Total = 74 s (summed from the listed durations; seed 1 — line 1 — sets up Ch16's Obsidian Synod, seed 2 — line 2 — sets up Ch13's Dr. Heris reveal). **A third, smaller continuity plant sits in line 4** — Ronin-7's "There's a training bay in her belly we never named. We'll call it after the thing we just brought down" christens a persistent, named Cairn location ("the Iron Dojo") in dialogue only; nothing in this or any other chapter's builder currently instantiates or references it, but if a later chapter's builder ever adds a Cairn training-bay set, this line is where the name originates.
- **`ch6_beat5_outro`** (2 lines, ≈23 s): Ronin-7's forward-turn ("whoever made it has someone over THEM. So we don't stop here.") → Echo's closing line, the hunt widened past the Program for the first time.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the closing beats are pure dialogue/traversal, same as Beats 0 and 2.
- `PurgeAlarm`'s `EvacuationTimer` optionally drives a looping alarm `AudioSource` (wired via `SetObjectRef(evacSo, "textMesh", alarmTm)` — note only the `textMesh` field is wired in the current build; `alarmSource` is left null, so **no actual alarm audio plays today**, only the visible countdown text — flagged in §9). This is also the payoff slot for the bell motif's turn: the script's evacuation bells are "a single long unbroken call" that "finally stop[s]" once the towers come apart (script line 551) — wiring `alarmSource` to a long, unbroken bell tone at step 12 and silencing it at `ChapterOutro` (step 17) would realize both the missing alarm audio and the bell motif's emotional close in one fix. **This fix must also silence the gentle `BellTower` bed, not just add the wrong call on top of it** — the screenplay stages this as one bell transforming (gentle → wrong call → silence, script 94–96, 551), not two beds overlapping, so step 12 should stop `BellTower`'s loop in the same instant it starts `PurgeAlarm.alarmSource` (§b above); otherwise the gentle bed keeps running under the wrong call and the motif's turn reads as muddied rather than as a hand-off.
- Comfort vignette engages normally on the walk back to the Iron Yard.
- `ReverbZonePlacer`'s auto-tagging (run once at the end of the build) covers the Morrigan Spine and all three tower arenas as distinct interior volumes; the Iron Yard and forest slope, having no ceiling, are correctly left untagged as exterior space.

## 5. Character travel-route master table

Unlike Chapter 1 (where Kessler is the only traveling NPC, walked twice on rails), Chapter 6 has **two** NPC-travel mechanisms and one deliberately-static named character:

| Character | Travel | Waypoints | Activated by |
|---|---|---|---|
| **Morrigan** | None — fixed in place the entire chapter *(includes Beat 5: she is physically absent from her own Iron Yard farewell dialogue — see §4 Beat 5 §b)* | spawn = final position, (0, 12, 97) | n/a — `wanderRadius: 0`, no `StoryNpcWander`, no `NpcWalker` |
| **Trainee_Eldest** | Descent — Morrigan's Spine → forest slope, once, on rails | `(0,13,96)→(0,13,80)→(1,10,64)→(-2,7,50)→(2,4,36)→(0,1,22)→(0,0.5,8)` | Mission step 12 Trigger (`TraineeWalker0`) |
| **Trainee_Young** | Descent — identical route to `Trainee_Eldest`, run in parallel | same list as above (`TraineeWalker1`) | Mission step 12 Trigger (`TraineeWalker1`) |
| **Matron Hespa / Drillmaster Caradoc / Master Kaelen** | None — each fixed at their tower's center until their `Health` reaches zero | (-46,12,111) / (0,12,150) / (46,12,111) | n/a — combat AI (`Enemy`), not `NpcWalker`-driven |
| **The 4 tower-side trainees** (`Trainee_Cradle0/1`, `Trainee_Proving0`, `Trainee_Vesting0`) | None — decorative, fixed | listed in §4 Beat 3's art tables | n/a |
| **Resh / Mera Voss / Iris / Kessler / Mira** | None — never physically present, voice-only via `DialoguePlayer` labels | n/a | n/a |

**Y-invariant (must hold or a named character sinks/floats — the critical invariant for THIS chapter):** unlike Chapter 1's flat single-floor ship, Chapter 6 has **five different world-Y floor levels** (forest y=0, five terraces at y=0/3/6/9/12, and the entire upper citadel at y=12). `FitNamedCharacter` — the shared grounding helper every Tripo-mesh character uses — **unconditionally grounds a character's feet to world y=0**, regardless of where it is instantiated. Every call site in this chapter that places a named-mesh character above the forest floor must **manually re-add the floor height after fitting**:

```csharp
FitNamedCharacter(go);
// FitNamedCharacter grounds the feet at world y=0; this chapter's upper-citadel floor sits
// at y=Ch6UpperY (12), not y=0 — re-add it or the character sinks 12 m into the mountain.
go.transform.position += Vector3.up * pos.y;
```

This pattern appears **twice** in the current builder: `Ch6PlaceStoryNpc` (Morrigan) and `Ch6BuildMasterEnemy` (all three masters). **Any patch that adds a new named character to this chapter's upper citadel — or moves an existing one — must repeat this exact compensation**, or the character will be grounded to the forest floor 12 m below its intended tower. This is the direct analogue of Chapter 1's `kesslerFloorY` invariant, but structural rather than parametric: Chapter 1 threads a computed `floorY` through waypoint-generating functions; Chapter 6 has no equivalent test coverage (§8) and relies entirely on each call site remembering to re-add `pos.y` by hand. **A refactor that centralizes named-character placement into a shared helper should promote this from "remembered convention" to "parameter you cannot forget" — e.g. `Ch6PlaceStoryNpc`/`Ch6BuildMasterEnemy` collapsing into one helper that takes `floorY` explicitly, the same shape Chapter 1's `KesslerToHoldWaypoints(floorY)` already models.**

The trainee capsules (`Ch6BuildTrainee`) do **not** go through `FitNamedCharacter` at all — being plain primitives, their vertical placement is `pos + Vector3.up * scale`, computed directly from the passed-in floor point, with no grounding-then-recompensation step. They are not subject to this invariant, but also cannot benefit from any future primitive→prefab swap that would introduce one.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Zone | Mood | Key/accent entries | Behaviour | Backdrop state | What changes |
|---|---|---|---|---|---|
| Forest Slope (Beat 1 start) | golden, open-sky, deceptively gentle | `ForestLight0` | `None` | pine forest, real sky, the citadel visible high above *(intended read only — no silhouette geometry exists and the fog curve suppresses it, see §3)* | none — static from spawn |
| Ascent (Beat 1) | golden daylight throughout, patrol-lit terraces | 5× `TerraceN_PatrolLight` | `None` (all five) *(canon wants a sweeping/moving patrol light players time the climb against, script 225/229 — see §4 Beat 1 §f)* | terraces rising toward the spine, bells audible distantly | none lighting-wise; the ascent itself is the content |
| Morrigan's Spine (Beat 2) | cold, cracked-slate glow, contained fury | `SpineLight0`, `SpineLight1` | `SpineLight0`: `AmbientPulse(6.2s)`; `SpineLight1`: `None` | interior, no exterior view | none — a still two-hander, same as Ch1 Beat 2's deliberately static Hold lighting |
| Iron Yard (Beats 2→5) | golden, gentle-bell exterior, "the beauty IS the horror" | `IronYardLight0`, `IronYardLight1` | `IronYardLight0`: `ConsoleFlicker(seed 55)`; `IronYardLight1`: `None` | open sky, `BellTower` at center | **Trigger (step 7):** the three towers arm (no visual change in the yard itself); **Trigger (step 12):** `PurgeAlarm` activates, two trainees begin the visible descent |
| The Cradle / Proving / Vesting (Beat 3) | rose-grey nursery / rust-brown hard-edge / cold blue-grey clinical — three distinct accent tints | per-tower `RoomDetails` accent color (§3.1) + new `CradleLight0`/`ProvingLight0`/`VestingLight0` accent lights, each centered on its arena and colored to match (§4 Beat 3 §c/§f, §A.1) | `None` *(proposed: extinguish/dim on that tower's master `Health.Died`, §4 Beat 3 §b — not yet built)* | enclosed 16×16 arenas, `RoomH` 3.6 | cadre → master activation per tower, independently; **proposed:** that tower's arena light goes dark on its master's death — the chapter's only diegetic any-order progress readout (script 351, 398), not yet built |
| The Vesting, Beat 4 | same as Beat 3's Vesting — no lighting change for the confession | (reused) | (reused) | reused Vesting arena | Kaelen's body remains; no new visual event |

Fog is the same baseline golden-haze exponential bed across the entire chapter — a single profile value, never overridden per-zone, unlike Chapter 1's uniform interior fog this reads as literal mountain-haze atmosphere rather than a stylistic constant.

**The Spine row's cool-blue `SpineLight0`/`SpineLight1` (0.6, 0.75, 1 — Appendix A.1) are the one deliberate departure from that warm bed, and they are the correct instinct, not an unmotivated color choice.** Canon's climb payoff line is "just you, the cold, and the woman in the dark," and the SETTING block names "snow on the high ridges" — the only place in this chapter's lighting that actually reads cold is Morrigan's summit. Credit that pairing explicitly: the blue spine lights are the "warmth drops away as you reach her window" cue. One gap worth flagging alongside it — nothing between Terrace 0 and Terrace 4 grades from the ascent's warm patrol-light tint toward that cool blue; the five `PatrolLight`s (Appendix A.1) hold a single warm value the whole climb, so the transition to cold happens as a hard cut at the spine rather than a climb-long cooling. Not proposed as a fix here, only as attribution the existing lights currently lack.

**A complementary surface gap sits underneath that lighting gap: nothing in the geometry itself grades with altitude either.** Canon is explicit — "pine forest cloaking the lower slopes, snow on the high ridges" (script 16) — but all five terrace decks share one tint (0.32, 0.3, 0.26, Appendix A.2) and the six pines cluster only at the forest base (Appendix A.2); no terrace or prop currently reads pine-littered/mossy at low altitude grading toward bare-granite/snow-dusted at high altitude. A per-terrace tint gradient (lower terraces warmer/mossier, Terrace3/4 cooler/greyer) and/or a snow decal on Terrace3/4 would let altitude read in the surfaces the player climbs past, not only in the lighting gradient flagged above — reinforcing the "beauty" the chapter is thesis-bound to sell the whole way up. Not proposed as a required fix, only as the dressing-side half of the gap this section's lighting note already names.

**Lighting differentiates the three towers; audio currently does not.** The accent-color column above gives the Cradle/Proving/Vesting three distinct visual reads, but none of the three arenas (nor the Iron Yard, nor `BellTower` at its center) carries a dedicated ambient `AudioSource` — see §4 Beat 3 §f and §7 for the three proposed per-tower beds and the bell-motif audio gap.

**The "Backdrop state" column's "real sky" entries describe intended set dressing, not a built asset** — see §3's backdrop/skybox note for the as-built default-procedural-sky gap against the screenplay's snow-ridge, weather-turn mountain sky.

## 7. Audio / VO manifest cross-reference

Thirteen canonical dialogue sets, defined in `Chapter6Lines.cs` and consumed via `Chapter6Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch6_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch6_beat1_climb` | 1 | (0, 1, 10) — `Dialogue_Beat1_Climb` |
| `ch6_beat1_window` | 1 | `Terrace4+(0,1,0)` = (0,13,80) — `Dialogue_Beat1_Window` |
| `ch6_beat2_morrigan_meet` | 2 | (0, 13, 96) — `Dialogue_Beat2_MorriganMeet` |
| `ch6_beat2_killlist` | 2 | (0, 13, 97) — `Dialogue_Beat2_KillList` |
| `ch6_beat3_hespa_intro` | 3A | (-38, 13, 111) — `Dialogue_Beat3_HespaIntro` |
| `ch6_beat3_caradoc_intro` | 3B | (0, 13, 142) — `Dialogue_Beat3_CaradocIntro` |
| `ch6_beat3_kaelen_intro` | 3C | (38, 13, 111) — `Dialogue_Beat3_KaelenIntro` |
| `ch6_beat4_confession` | 4 | (46, 13, 111) — `Dialogue_Beat4_Confession` |
| `ch6_beat5_evacuation` | 5 | (0, 13, 96) — `Dialogue_Beat5_Evacuation` |
| `ch6_beat5_descent` | 5 | (0, 13, 111) — `Dialogue_Beat5_Descent` |
| `ch6_beat5_morrigan_joins` | 5 | (0, 13, 112) — `Dialogue_Beat5_MorriganJoins` |
| `ch6_beat5_outro` | 5 | (0, 13, 113) — `Dialogue_Beat5_OutroHook` |

**Chapter total: 13 sets, ~654 s (~11 min) of authored VO across ~62 lines** (summed from the per-set totals given throughout §4). Use this as the target denominator when sanity-checking a TTS batch against §8.8's clip-resolution console check, rather than re-summing thirteen tables by hand.

Each is built by the local `Ch6BuildDialogue` wrapper (mirrors Chapter 1's `BuildChapter1Dialogue`, Chapter 5's equivalent): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch6WireVoiceClips`, resolving each line's `AudioClip` from `Chapter6Lines.ClipName(setId, index, speaker)` — pattern `ch6_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all thirteen `DialoguePlayer`s. Unlike Chapter 1, this chapter has **no `PromptInputAdvancer`/release-prompt** — there is no equivalent gated player-input moment (the towers are gated by combat, not a single button prompt).

**Dialogue is data, not art.** None of this changes in the refactor — the thirteen set ids, their positions, and the clip-resolution pattern are canon.

**Echo's delivery tone shifts by beat — the VO pipeline must not render it uniformly.** Per the SETTING block: "The wry warmth of Ch4 is allowed back at the edges of the climb, then drops to bare witness in the towers and the confession" (script 102-105). §4 Beat 1 §a already notes the warmth returning for the climb, and §4 Beat 4 §a notes Echo's closing line carrying the weight forward, but nothing here consolidates the arc for whoever records the actual clips. When `Ch6WireVoiceClips`/the edge-tts generation pass renders Echo's lines, `ch6_beat1_climb`/`ch6_beat1_window` (wry warmth, partnership banter) and `ch6_beat5_outro` (warmth allowed back, script ~539/561) should sound different from `ch6_beat3_hespa_intro`/`caradoc_intro`/`kaelen_intro` and `ch6_beat4_confession` (flatter, colder, "bare witness, no jokes inside these walls," script ~299/443) — same clip-name pattern (`ch6_{setId}_{index:00}_{speaker}`), deliberately different delivery direction. Flagged here so a single uniform Echo voice pass doesn't flatten the chapter's most deliberate character-voice arc.

SFX/ambience bed, all under `Assets/Ronin7/Art/Generated/Audio` (or procedurally generated):

| Clip / source | Used for |
|---|---|
| `MorriganSpineAmbience` (`BuildAmbienceLayer`) | Beat 2/4/5's spine interior bed — inner 4 / outer 16 / vol 0.4 |
| `ForestGardenAmbience` (`BuildAmbienceLayer`) | Beat 1's forest-slope bed — inner 6 / outer 24 / vol 0.4 |
| `AqueductWaterAmbience` *(proposed, not built)* | Beat 1's climb — running water for the "aqueducts thread the rock" canon detail and Echo's spillway route-call; sources from the proposed `Props.Aqueduct` span, not free-floating (§4 Beat 1 §c/§f) |
| `WindAmbience` *(proposed, not built)* | Beat 1's climb — rising-with-altitude wind bed for "Wind. A clean sun. A very long way down" (script 198) and Echo's gust-timing bark "Wind's coming up the gorge, wait for the lull before the jump" (script 229); §4 Beat 1 §c/§f |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | fills any remaining procedurally-sourced SFX slots (bells, wind, combat stingers) chapter-wide — same call every other chapter builder makes; **but `BellTower` has no `AudioSource` for it to fill** (below) |
| `BellTower` `AudioSource` | **does not exist** — the Iron Yard's `BellTower` prop (Appendix A.3) has no sound source; the chapter's recurring gentle hour-bells (§3) are currently silent (§9) |
| `EvacuationTimer.alarmSource` | **wired to `null` in the current build** — the `PurgeAlarm` prop displays its countdown text but plays no alarm tone; also the slot that would carry the evac's "wrong bell" (§4 Beat 5 §f, §9) |
| `CradleAmbience` *(proposed, not built)* | Beat 3A's arena bed — soft room-tone, turning-mobile chimes; the conditioning-lullaby cue itself belongs on `IntakeNameSlate`'s own `AudioSource`, not this bed (§4 Beat 3 §f) |
| `ProvingAmbience` *(proposed, not built)* | Beat 3B's arena bed — live-fire gauntlet cracks, distant impacts, hard stone reverb (§4 Beat 3 §f) |
| `VestingBondingHum` *(proposed, not built)* | Beat 3C/4's arena bed — the bonding cradles' low clinical hum, referenced by the script in both Beat 3 and Beat 4 (§4 Beat 3 §f, Beat 4 §f) |

**Mixing note, once `WindAmbience` and `BellTower`'s `AudioSource` both land: the wind carries the bells, they are not two independent beds.** Canon ties them together directly — "Bells ring the hour, gentle and regular, carried down the slope on the wind" (script 198) — so `BellTower`'s gentle loop should read as attenuated by/riding on `WindAmbience` (audible-but-distant on the lower slope, thinning with altitude — "The bells thin out," script 231) rather than as two independently-specced loops mixed at fixed volume. A mixing note, not a new asset.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 06 — The Iron Dojo** (`XRRigBuilder.BuildChapter6IronDojo()`). This internally calls `AddDojoSummitRoute()` — a standalone additive patch (**Tools → Space Samurai → Chapters → Patch Ch06 Summit Route (additive)**) also exists for re-applying the summit route to an already-shipped scene without a full rebuild.
2. **EditMode is the gate.** Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call. Consult the project's current baseline count (`CLAUDE.md`) rather than a number frozen at this document's writing.

   > ⚠ **Coverage blind spot (same class of gap Chapter 1 flags).** No EditMode test invokes `BuildChapter6IronDojo()` or loads `Ch06_IronDojo.unity`. **A green suite says nothing about whether this scene still builds, still climbs, or still gates its towers correctly.** Every structural change in this refactor must be verified by opening the scene and walking it — climb the ascent, confirm all five terraces and the summit ladder are still `Climbable`, confirm all three towers still arm on the kill-list trigger, confirm the gate still fires regardless of tower order.
3. **Any-order regression check (new, chapter-specific — no automated test exists today).** Manually clear the three towers in at least two different orders (e.g. Vesting → Cradle → Proving, and Cradle → Proving → Vesting) across two separate playthroughs/build verifications. Confirm: (a) each tower only activates when approached, never early; (b) `MultiObjectiveGate.onAllComplete` fires exactly once, on the third death, regardless of order; (c) `Dialogue_Beat4_Confession` always plays after all three are dead, never mid-tower-clearing.
4. **Climbable-tagging survival test (new).** After any prefab swap on a terrace deck, rock, or the summit ladder, confirm `AddDojoSummitRoute`'s tagging pass still finds and marks a live `Collider` on the new geometry — the pass is name-and-collider-dependent and fails silently (§1.5, §4 Beat 1) if either is missing.
5. **Safe-zone survival test.** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
6. **Fallback audibility test.** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry, all five terraces climbable, all three towers built and armable) plus one `LogWarning` per unresolved key — never an empty mountain, never an exception.
7. **Perf reference bar.** No baseline exists yet for this scene (§1.6) — capture one (`UnityStats` in edit mode) the first time this checklist runs and record it here for future re-measurement. Given this chapter's exterior scale (§1.6), pay particular attention to overdraw from the Iron Yard looking simultaneously at all three tower entrances.
8. **Console check:** `Ch6WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild, across all thirteen dialogue sets. §7's consolidated total (13 sets, ~654 s, ~62 lines) is the denominator to sanity-check the batch against.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter6IronDojo()` wipes generated content, the same as every other chapter builder. The house rule remains: patch additively in the live editor, or fix `Chapter6Builder.cs`/`ParkourLevelBuilder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** Regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Room shells and props get primitive colliders; this matters more than usual in this chapter because the `Climbable` marker pass and `WallClimbLocomotion`'s grip-detection both key off collider presence — an accidental `MeshCollider` on a terrace deck would still technically be climbable but is a performance and physics-stability regression the project's existing MeshCollider ban already exists to prevent.
- **Cadre are mechanically undifferentiated across all three towers (flagged, not proposed as a fix here).** The dialogue script and story treatment describe three distinct cadre archetypes per tower — warden-nurses/conditioning-enforcers/suppression drones (Cradle), instructor-cadre/senior cadet packs (Proving), handler-cadre/vested young operatives (Vesting) — but the as-built code spawns nine mechanically identical `Enemy`s from one shared `EnemyDefinition` regardless of tower. This is the same class of narrative/mechanic gap Chapter 1's Appendix flags for its "melee vs. rifle-carrying" troopers — noted for awareness, not silently resolved here. This flag covers drones only as an unbuilt combatant archetype; the separate ambient reading of drones as overhead set-dressing is covered by the proposed `Props.SuppressionDrone` row in §4 Beat 3 §c (3A).
- **The Vesting's optional mercy/spare mechanic is not implemented.** The dialogue script is explicit that this is the *one* place in the citadel where a spare is mechanically available (against vested cadets only), with broken-and-spared cadets sinking down and leaving the fight. No `DuelYield`-equivalent or spare-state wiring exists on any Vesting cadre `Enemy` today. This is a real feature gap, not a documentation gap — flagged for a combat-systems decision, not fixed here.
- **Kaelen's "wounded but waiting" staging is not modeled; the gate achieves the ordering guarantee more simply, but not the spatial one — this is a real blocking gap, not just a documentation gap.** The screenplay calls for Kaelen to be left alive-but-mortally-wounded if the Vesting is cleared before the other two towers, with his confession implemented as a return-trigger. The builder instead lets Kaelen die normally on defeat and relies on `MultiObjectiveGate` to delay the confession dialogue step (step 9) until all three masters are dead — which produces the correct player-facing *ordering* without a distinct "wounded" enemy state. What it does **not** produce is the correct player-facing *location*: step 9 has no `ReachTrigger` (unlike Beat 5's `MorriganReturnReachPoint`/step 10), so if the Vesting isn't the last tower cleared, `Dialogue_Beat4_Confession` fires at Kaelen's fixed anchor (46,13,111) while the player is up to ~92 m away at whichever tower they finished on — the confession plays over the wrong body, off-screen from Kaelen's. §4 Beat 4 §b details the gap and its two fixes: add a `KaelenReturnReachPoint` mirroring `MorriganReturnReachPoint`, or explicitly document the confession as position-independent VO. Worth a writers'-room sign-off on which of those two — not just on whether the simpler "wounded" substitute is acceptable — since the current doc's §a/§c/§d wording implies the return-to-body staging without the builder reliably delivering it.
- **The tower approaches and the climb are a flat-corridor simplification of the screenplay's vertical inter-peak crossings — deliberately unbuilt, not overlooked.** The dialogue script repeatedly stages traversal as "wall-runs, ledge-jumps between aqueduct spans, climbs along the curtain wall" for the ascent (Beat 1), and "parkour across the curtain walls and bridges" over "high bridges and a cable-tram" spanning the gorges between peaks for the tower approaches (Beat 3, §2's own SETTING block). The as-built (and §2's spatial map) render the ascent as terraces + tilted ramps at rising Y and the three tower approaches as flat `Ch6BuildGroundStrip` corridors at a single Y (world y=12, §2) — no aqueduct spans, curtain-wall traversal, or cable-tram exist anywhere in the current geometry. Flagged here so a reader comparing §2 to the dialogue script knows the gorges/bridges/aqueducts are a scoped simplification, not a gap in this document's coverage.
- **No fall-recovery behavior exists for the ascent (open VR-comfort question, not yet decided) — and the exposure is really an optional-route problem.** Nothing catches or respawns a player who falls off the switchback climb — `AscentMidReachPoint`/`WindowReachPoint` are reach gates, not respawn anchors, and `ZoneBounds` is a loose boundary sphere, not a catch volume. The mandatory `Ramp0–3` walk-only spine (§1.1) has almost no fall exposure; the hazard belongs to the `Climbable` terrace faces and summit ladder, the optional grip layer. Canon treats the fall itself as the climb's hazard, and an uncontrolled long fall is a VR comfort/sickness event, so this still needs an explicit sign-off (kill-floor + respawn at the last terrace, a soft re-grip catch, or a deliberate "no consequence" call), scoped to the optional routes, rather than staying silent (§4 Beat 1 §f).
- **Echo's position-triggered climb bark pool is unimplemented.** The dialogue script explicitly defers this ("the FINAL set of Echo barks and their exact trigger points are to be authored once the Climb's level geometry is designed") — only the two bracketing `DialoguePlayer`s (`ch6_beat1_climb`, `ch6_beat1_window`) exist today. A future pass adding position-triggered barks along the ascent is in-scope future work, not a bug.
- **The tower cadre have no wired voice on contact.** Each tower's cadre bark (Cradle Warden, Proving Instructor, Vesting Handler — each explicitly marked in the script as "representative of the [tower] cadre bark pool") is absent from all three wired `ch6_beat3_*_intro` sets (confirmed against `Chapter6Lines.cs`, §4 Beat 3 §e) — the wave-0 bark plays the master's own intro line straight, with no cadre line ever fired. Same class of deferred-pool gap as the Echo climb barks above, not an oversight silently folded into the existing sets.
- **The bell motif is silent end-to-end — no `AudioSource` exists for either the gentle hour-bells or the evacuation's wrong bell.** `BellTower` (Appendix A.3, Iron Yard center) is a bare primitive with no `AudioSource` at all, so the "gentle and regular through the climb and the towers" bells (§3) never actually sound. `PurgeAlarm`'s `EvacuationTimer.alarmSource` is likewise never assigned (only `textMesh` is), so the evacuation's "single long unbroken call" — the script's "the bells finally stop" turn (line 551) — has no audio payoff either, only the visible countdown text. The chapter's single most-repeated diegetic element currently produces zero sound. Two paired fixes: add a spatial looping `AudioSource` (gentle bell bed) to `BellTower`, and assign an `alarmSource` on `PurgeAlarm` for the evac's long call, so the bells can audibly ring wrong and then stop when the towers fall. **A third fix is required alongside those two, not implied by them:** the Trigger step that arms `PurgeAlarm` (mission step 12) must also stop `BellTower`'s gentle loop, or the two sources simply layer instead of one bell transforming into the other — see §4 Beat 5 §b/§f for the explicit hand-off spec.
- **Cradle/Proving/Vesting have no per-tower ambient audio bed, so the three towers currently sound identical (silent) despite three distinct accent colors.** Only `MorriganSpineAmbience` and `ForestGardenAmbience` are built as `AudioSource` layers; proposed per-tower beds (`CradleAmbience`, `ProvingAmbience`, `VestingBondingHum`) are specced in §4 Beat 3 §f and §7 but not yet built. The Cradle's conditioning-lullaby cue in particular is one of the script's sharpest horror beats and is currently unrepresented anywhere in the as-built scene.
- **No per-tower light extinguishes on its master's death — the chapter's own "a tower goes dark" reveal is unbuilt.** The dialogue script punctuates the kill-list twice with this exact image (script lines 351, 398), and it is the only diegetic progress readout the free-order, no-HUD-tracker kill-list gives the player that they are on the last master. `CradleLight0`/`ProvingLight0`/`VestingLight0` (§4 Beat 3 §c, §A.1) are currently specced as static, `Behaviour: None` lights with no death-linkage. §4 Beat 3 §b proposes the fix: a small per-tower listener subscribed to that master's `Health.Died` (the same runtime-subscription idiom `MultiObjectiveGate` already uses), toggling or dimming its own light. Not yet built.
- **`MorriganSpineAmbience`'s Y position may be missing the `Ch6UpperY` offset.** `BuildAmbienceLayer("MorriganSpineAmbience", new Vector3(0f, 1.5f, 92f), …)` places the ambience source at world y=1.5 — near the forest/ascent-base height, not near the spine's actual floor at world y≈12. Every other position in the spine (dialogue anchors, Morrigan's own placement, the accent lights) correctly reads world y≈12–14. This reads as a plausible oversight rather than intentional design (the source is 2D-adjacent enough at inner/outer radius 4/16 that it may still be audible regardless of the exact Y), but is called out here rather than silently corrected — verify against the live scene before patching (§8).
- **The Iron Yard has no children in it before the evacuation.** The screenplay's recurring "beauty IS the horror" image is "terraced gardens, children in ordered lines" (script 89-92) in the citadel's central muster ground — the Iron Yard itself (script 19, 93). As-built the Iron Yard holds only `BellTower` and ground until the two evacuation walkers appear at mission step 12; nothing stands in for a populated muster ground during Beats 2-3. §4 Beat 3 §c proposes a row of static decorative trainee capsules (reusing `Ch6BuildTrainee`) standing in ordered lines, cleared/converted to the walkers at step 12 — flagged here as a real gap, not proposed as a required fix.
- **The trainee evacuation walk is fully synchronized, not staggered.** Both `Trainee_Eldest` and `Trainee_Young` share the identical waypoint list and start simultaneously, despite the screenplay's "eldest at the front and the back, counting heads" staging implying a bracketing formation rather than two NPCs walking the same line in lockstep. The shared waypoint list also routes both walkers to their first waypoint, `(0,13,96)` — inside Morrigan's spine, north of the trainees' own Iron Yard spawn at (±2,12,113) — so on activation both children first walk upslope into the fortress before turning and descending through Terrace 4 and down; the "children walking free down the forest roads" beat visibly opens walking the wrong way. Cosmetic, not a functional bug, but it compounds the lockstep-parallel issue above — flagged for awareness.
- **The bound intercept-drive stack (`Props.InterceptDriveStack`) is a strong Morrigan characterization prop worth building.** Per the dialogue script, Morrigan's quarters (Beat 2) hold "a stack of intercept-drives... bound and ready by the door, like a bag packed long ago," and she is carrying it under her arm by Beat 5 ("the bound stack of intercept-drives now under her arm"). §4 Beat 2 §c already carries an unbuilt row for this prop; it is called out here explicitly (rather than left as a dangling `see §9` pointer with nothing on the other end) because the detail does real character work — nine years of a woman who packed her escape and never used it — that a bare `MorriganSpineShell` interior otherwise loses entirely. **Scope note:** Morrigan is a fixed `StoryNpc` (`wanderRadius: 0`, §5) with no animation and no Beat-5 re-parenting step anywhere in the builder, so as specced this prop can only ever be static Beat-2 door-dressing — the screenplay's Beat-5 payoff of the stack moving to "now under her arm" (script 495, 524, 547) is not achievable on a static mesh without an explicit re-parent-to-Morrigan step wired to the evacuation Trigger (mission step 12). Building the prop alone realizes the Beat-2 detail, not the "packed bag she finally picks up" beat. **This is the same root cause behind Morrigan's physical absence from her own Beat 5 farewell dialogue in the Iron Yard (§4 Beat 5 §b)** — the two gaps share one fix: a Beat-5 walk/re-parent leg from the spine to the Iron Yard, fired off the step-12 evacuation Trigger.
- **The citadel does not visually come apart in fire.** The screenplay's closing image — "the three towers come apart in slow fire against the clean mountain sky" — is not modeled as a VFX event anywhere in the builder; `Vfx.TowerCollapse` (Appendix B) is a commissioned-but-unbuilt registry key with no call site yet. The evacuation's only visible in-engine event is the two trainee walkers descending and the `PurgeAlarm` text activating. **Neither the insertion nor the exfil shuttle is a built prop, either.** The Beat 1 arrival shuttle is already acknowledged as unbuilt-by-design (§4 Beat 1 §b, "the rig simply exists at spawn"), and the screenplay's closing "the shuttle lifts off the burning citadel" (script 563) is arguably covered by `ChapterOutro`'s fade + complete canvas rather than a built asset. Not proposed as a fix here — low value given the fade already closes the loop.
- **No physical Cairn briefing set exists for Beat 0**, consistent with the documented crew-presence decision (§3) that this chapter is voice-only for the rest of the crew. If a future chapter-cutscene pass wants a physical cutaway to the command room for this beat, that is new scope, not a bug in the current builder. If it is ever scoped, §4 Beat 0 §c notes the highest-value single element is the holo-table's turning citadel projection (script 37-38) — it doubles as a potential source asset for §3's separately-flagged "citadel never seen at scale" gap in the Beat 1 establishing sightline.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch6Environment.asset`. Currently set inline at the top of `BuildChapter6IronDojo` (`Chapter6Builder.cs:106–128`).

| | Value |
|---|---|
| Directional key | color (1, 0.92, 0.78), intensity 1.15, rotation Euler(50, -35, 0) |
| Directional key — shadows | **unset today** (`light.shadows` never touched by `Chapter6Builder.cs`); proposed per-tier (§1.6): PCVR `LightShadows.Soft`, Quest `LightShadows.Hard` at reduced resolution/distance, or `None` if overdraw trends high once real prefabs land |
| Ambient | mode **Flat**, color (0.28, 0.26, 0.22) |
| Fog | mode **Exponential**, color (0.55, 0.5, 0.4), density 0.018 |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `ForestLight0` | (-4, 2.2, 8) | (1, 0.9, 0.6) | 1 | 12 | none |
| `SpineLight0` | (-3, 14.4, 92) | (0.6, 0.75, 1) | 1.2 | 12 | `AddAmbientPulse(period: 6.2f)` |
| `SpineLight1` | (3, 14.4, 98) | (0.6, 0.75, 1) | 1.2 | 12 | none |
| `IronYardLight0` | (-8, 15, 108) | (1, 0.9, 0.65) | 1.4 | 16 | `AddConsoleFlicker(seed: 55f)` |
| `IronYardLight1` | (8, 15, 114) | (1, 0.9, 0.65) | 1.4 | 16 | none |
| `TerraceN_PatrolLight` ×5 | each terrace center + (0,2.4,0) | (1, 0.85, 0.5) | 1 | 10 | none |
| `CradleLight0` *(new — not yet in code, see §4 Beat 3 §c/§f, §6)* | (-46, 14.5, 111) — Cradle arena center, ~2.5 m up | (0.45, 0.35, 0.4) — matches `cradleColor` | 1.3 | 14 | **proposed:** extinguish/dim on Matron Hespa's `Health.Died` (§4 Beat 3 §b) — not yet built |
| `ProvingLight0` *(new — not yet in code, see §4 Beat 3 §c/§f, §6)* | (0, 14.5, 150) — Proving arena center, ~2.5 m up | (0.4, 0.3, 0.25) — matches `provingColor` | 1.3 | 14 | **proposed:** extinguish/dim on Drillmaster Caradoc's `Health.Died` (§4 Beat 3 §b) — not yet built |
| `VestingLight0` *(new — not yet in code, see §4 Beat 3 §c/§f, §6)* | (46, 14.5, 111) — Vesting arena center, ~2.5 m up | (0.3, 0.35, 0.45) — matches `vestingColor` | 1.3 | 14 | **proposed:** extinguish/dim on Master Kaelen's `Health.Died` (§4 Beat 3 §b) — not yet built |

No event lights this chapter (`ChapterEnvironmentProfile.eventLights` unused — the evacuation alarm is a `TextMesh`, not a light).

### A.2 Beat 1 — Forest Slope + Switchback Ascent

| Element | Coordinates / value | Component / method |
|---|---|---|
| `ForestGround` | local (0,-0.5,10), scale (20,1,28), tint (0.14,0.24,0.1) | inline `PrimitiveType.Cube` in `BuildChapter6IronDojo` |
| Pine ×6 | (-6,1.2,2), (6,1.4,4), (-7,1.1,12), (7,1.3,10), (-4,1.2,15), (4,1.1,16); scale (0.8, y×2, 0.8), tint (0.1,0.22,0.12) | `BuildProp(forest, "Pine", pos, scale, color)` |
| Terrace centers | Terrace0 (0,0,22); Terrace1 (2,3,36); Terrace2 (-2,6,50); Terrace3 (1,9,64); Terrace4 (0,12,80) | `terraces[]` array, `Ch6BuildTerrace` |
| Terrace deck | 8×8, floor at center + (0,-0.2,0), tint (0.32,0.3,0.26) | `Ch6BuildTerrace` |
| Terrace rocks ×2/terrace | offsets (-3.5,0.6,-2) / (3.2,0.5,2.5) from center, scale ~1×1.2×1 / 0.9×1×0.9, tint (0.22,0.2,0.18) | `Ch6BuildTerrace` |
| Terrace patrol light | center + (0,2.4,0), warm (1,0.85,0.5) i1 r10 | `Ch6BuildTerrace` → `BuildAccentPointLight` |
| Ramp0–3 | tilted `Cube`, positioned/rotated to bridge consecutive terrace centers, width 6, tint (0.28,0.26,0.22) | `Ch6BuildRamp` |
| Summit holds ×5 | (-3.5,12.70,100.22) → (-2.7,15.30… alternating), scale (0.4,0.22,0.25), tint (0.55,0.42,0.28) | `AddDojoSummitRoute` → `BuildClimbProp` |
| `SummitDeck` | (-3.1,15.7,98.0), scale (5,0.2,4), tint (0.32,0.26,0.2), `Climbable` | `AddDojoSummitRoute` |
| `SummitBell` / `SummitBellPost` | (-3.1,16.15,97.0) cylinder / (-3.1,16.6,97.0) post; non-climbable | `AddDojoSummitRoute` |
| `ForestGardenAmbience` | (-4,2.2,8), inner 6 / outer 24 / vol 0.4 | `BuildAmbienceLayer` |
| Reach points | `AscentMidReachPoint` = Terrace2+(0,1,0) = (-2,7,50); `WindowReachPoint` = Terrace4+(0,1,0) = (0,13,80) | inline GameObjects |
| Dialogue players | `Dialogue_Beat1_Climb` (0,1,10) set `ch6_beat1_climb`; `Dialogue_Beat1_Window` (0,13,80) set `ch6_beat1_window` | `Ch6BuildDialogue` |
| Climbable tagging | any child named `BellTower`, `TerraceN` (no `_`), or `TerraceN_Rock*` with a live `Collider` | `AddDojoSummitRoute`, deep `GetComponentsInChildren<Transform>(true)` search |
| Mission steps | indices 1–4 of 18 | `AuthorDialogueStep`/`AuthorReachStep` |

**Naming collision, not yet flagged elsewhere.** The tagging pass above matches on literal object name only (`n == "BellTower"`, `ParkourLevelBuilder.cs:51`), with no scope restriction to the summit route's own hierarchy — so it also finds and tags the Iron Yard's decorative `BellTower` cylinder (§4 Beat 3 §c, Appendix A.3), which shares that exact name and, as a `PrimitiveType.Cylinder`, carries a live `Collider` by default. A player who grips it in the middle of the muster ground drops into `WallClimbLocomotion`'s grip-mode (movement suspended) on a prop with nothing to climb — harmless narratively, but a real VR annoyance, and a live, un-flagged side effect of the name-keyed tagging pass this document otherwise treats as high-risk (§4 Beat 1's "Notes on the transition"). Fix by renaming one of the two `BellTower`s (the summit-route bell is the better candidate — it is new, purpose-built geometry) or by scoping `AddDojoSummitRoute`'s tagging walk to the `SummitRoute`/`Ascent` subtree instead of the whole `IronDojo` hierarchy.

### A.3 Beat 2 — Morrigan's Spine + Iron Yard shell

| Object / Method | Value |
|---|---|
| Upper citadel parent | `UpperCitadel` GameObject, `localPosition (0, Ch6UpperY=12, 0)` — every child position below is local to this transform (add 12 to Y for world space) |
| Spine floor/ceiling | `BuildFloorCeiling(upper, "MorriganSpine", (0,0,92), (12,0,16), (0.2,0.19,0.22), (0.1,0.1,0.12))` |
| Spine walls | `MorriganSpine_WallW` (-6,1.8,92) size(0.2,3.6,16); `MorriganSpine_WallE` (6,1.8,92) size(0.2,3.6,16); `MorriganSpine_WallN` doorway (0,1.8,100) width 12, gap 3 — **no `WallS` call exists; south face z=84 is open by design (§2), the climb-in window** |
| Spine details | `BuildRoomDetails(upper, "MorriganSpine", (0,0,92), (6,8), (0.35,0.4,0.55))` |
| Iron Yard ground | (0,-0.1,111), scale (30,0.2,22), tint (0.42,0.4,0.34) | inline `PrimitiveType.Cube` |
| `BellTower` | (0,1.4,111), cylinder scale (0.6,1.4,0.6), tint (0.55,0.5,0.3) | inline `PrimitiveType.Cylinder` |
| Morrigan | spawn (0,12,97) *(world — `Ch6PlaceStoryNpc` re-adds `pos.y` after `FitNamedCharacter`)*, `StoryNpc` displayName "Morrigan", no `StoryNpcWander` (radius 0) | `Ch6PlaceStoryNpc(Ch6MorriganPrefab, ...)` |
| `MorriganSpineAmbience` | (0,1.5,92) — **see §9 Y-offset flag** | `BuildAmbienceLayer` |
| Accent lights | `SpineLight0` (-3,14.4,92); `SpineLight1` (3,14.4,98) | `BuildAccentPointLight` |
| Reach point | `MorriganReturnReachPoint` (0,13,96) | inline GameObject |
| Dialogue players | `Dialogue_Beat2_MorriganMeet` (0,13,96) set `ch6_beat2_morrigan_meet`; `Dialogue_Beat2_KillList` (0,13,97) set `ch6_beat2_killlist` | `Ch6BuildDialogue` |
| Mission steps | indices 5–7 of 18 | `AuthorDialogueStep`/`AuthorTriggerStep` |

### A.4 Beat 3 — The Three Towers

| Item | Value | Source |
|---|---|---|
| Cradle corridor | `Ch6BuildGroundStrip(upper, "CradleCorridor", (-26,0,111), (24,10), (0.45,0.35,0.4))` |
| Cradle arena | `Ch6BuildTowerArena(upper, "Cradle", (-46,0,111), half 8, doorSide "east", spineFloor, spineCeil, cradleColor)` |
| Cradle cadre positions | (-20,12,108), (-30,12,116), (-42,12,111) | `cradleCadrePos[]` → `Ch6BuildCadre` |
| Matron Hespa | (-46,12,111), `EnemyDefinition` maxHealth 180 / damage 12 / moveSpeed 1.1 / cooldown 1.1s | `Ch6BuildMasterEnemy(Ch6HespaPrefab, ...)` |
| `Trainee_Cradle0` / `_Cradle1` | (-44,12,105) scale 0.5 / (-48,12,117) scale 0.5 | `Ch6BuildTrainee` |
| Proving corridor | `Ch6BuildGroundStrip(upper, "ProvingCorridor", (0,0,131), (10,20), (0.4,0.3,0.25))` |
| Proving arena | `Ch6BuildTowerArena(upper, "Proving", (0,0,150), half 8, doorSide "south", ...)` |
| Proving cadre positions | (0,12,126), (0,12,134), (0,12,146) | `provingCadrePos[]` |
| Drillmaster Caradoc | (0,12,150), `EnemyDefinition` 240/20/1.6/0.7s, + `PatternedDuelist` | `Ch6BuildMasterEnemy(Ch6CaradocPrefab, ...)` |
| `Trainee_Proving0` | (3,12,144) scale 0.75 | `Ch6BuildTrainee` |
| Vesting corridor | `Ch6BuildGroundStrip(upper, "VestingCorridor", (26,0,111), (24,10), (0.3,0.35,0.45))` |
| Vesting arena | `Ch6BuildTowerArena(upper, "Vesting", (46,0,111), half 8, doorSide "west", ...)` |
| Vesting cadre positions | (20,12,108), (30,12,116), (42,12,111) | `vestingCadrePos[]` |
| Master Kaelen | (46,12,111), `EnemyDefinition` 280/22/1.3/0.9s | `Ch6BuildMasterEnemy(Ch6KaelenPrefab, ...)` |
| `Trainee_Vesting0` | (44,12,105) scale 0.75 | `Ch6BuildTrainee` |
| `KillListGate` | `MultiObjectiveGate` watching [hespaEnemy.Health, caradocEnemy.Health, kaelenEnemy.Health]; `onAllComplete` → `MissionDirector.AdvanceFromPrompt` | inline in `BuildChapter6IronDojo` |
| `TowerArm` | `ActivationRelay`, `OnEnabled` → 3× spawner `Begin()`; built `SetActive(false)` | inline |
| `CradleWaveSpawner` / `ProvingWaveSpawner` / `VestingWaveSpawner` | trigger pos = each corridor midpoint, `triggerRadius` 9; wave0 = cadre Health[], wave1 = [master.Health]; bark = per-tower intro dialogue | `BuildWaveSpawner` |
| Intro dialogue players | `Dialogue_Beat3_HespaIntro` (-38,13,111); `Dialogue_Beat3_CaradocIntro` (0,13,142); `Dialogue_Beat3_KaelenIntro` (38,13,111) | `Ch6BuildDialogue` |
| Mission steps | indices 7–8 of 18 (step 7 shared with Beat 2's close) | `AuthorTriggerStep`/`AuthorPromptStep` |

### A.5 Beat 4 — Kaelen's Confession

| Element | Coordinates / value | Component / method |
|---|---|---|
| *(no new geometry — reuses the Vesting arena from A.4)* | — | — |
| Dialogue player | `Dialogue_Beat4_Confession` (46,13,111), set `ch6_beat4_confession`, 8 lines | `Ch6BuildDialogue` |
| Mission step | index 9 of 18 | `AuthorDialogueStep` |

### A.6 Beat 5 — The Evacuation

| Element | Coordinates / value | Component / method |
|---|---|---|
| `PurgeAlarm` | (0,15,111); `TextMesh` child "Text", fontSize 48, color (1,0.35,0.3); `EvacuationTimer` (textMesh wired, alarmSource **not** wired — see §9); built `SetActive(false)` | inline in `BuildChapter6IronDojo` |
| Descent waypoints | `(0,13,96)`, then Terrace4..Terrace0 centers each `+(0,1,0)` in reverse order, then `(0,0.5,8)` | `descentWaypoints` list, built by iterating `terraces[]` backward |
| `Trainee_Eldest` / `Trainee_Young` | spawn (2,12,113) scale 0.9 / (-2,12,113) scale 0.6 | `Ch6BuildTrainee` |
| `TraineeWalker0` / `TraineeWalker1` | inactive `NpcWalker`, target = trainee transform, waypoints = `descentWaypoints` (shared list, both walkers) | `BuildNpcWalker(world, ..., ...)` — cross-chapter helper, §1.2 |
| Reach points | `MorriganReturnReachPoint` (0,13,96) *(reused from Beat 2)*; `IronYardReachPoint` (0,13,111) | inline GameObjects |
| Dialogue players | `Dialogue_Beat5_Evacuation` (0,13,96); `Dialogue_Beat5_Descent` (0,13,111); `Dialogue_Beat5_MorriganJoins` (0,13,112); `Dialogue_Beat5_OutroHook` (0,13,113) | `Ch6BuildDialogue` |
| `CHAPTER 6 COMPLETE` canvas | worldspace, position (0,13.4,115), rotation Euler(0,180,0) (faces -Z toward the player), built inactive | `Ch6BuildCompleteCanvas` |
| `ChapterOutro` | (0,13,114); `CampaignFlagSetter` flag `"ch6_complete"` wired to `OnActivated`; `completeCanvas` ref = the complete canvas; built inactive | `ChapterOutro` component |
| Mission steps | indices 10–17 of 18 | `AuthorReachStep`/`AuthorDialogueStep`/`AuthorTriggerStep` |

### A.7 Scene root hierarchy (current)

`BuildChapter6IronDojo()` creates these as **siblings**, not nested: `Directional Light`, `IronDojo` (containing `ForestSlope`, `Ascent`, `UpperCitadel`, and — added by the summit-route patch — `SummitRoute`; also the two evacuation `TraineeWalker`s and the trainee capsules, which are built as direct children of `world` rather than under a named sub-branch), the five accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig, four reach-point GameObjects, thirteen dialogue-player roots, `KillListGate`, `TowerArm`, `PurgeAlarm`, the complete canvas, `ChapterOutro`, and `Mission`. `SettingsPanelBuilder.BuildSettingsPanel()`, `XRRigBuilder.RewireOpenScene()`, `BuildAmbienceLayer` ×2, `ProceduralAudioClipBuilder.AssignGeneratedClips()`, `AddConsoleFlicker`/`AddAmbientPulse`, `ReverbZonePlacer`'s two static calls, and `AddDojoSummitRoute(world)` all run at the tail of the build, in that order, before the final `EditorSceneManager.SaveScene`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots (0 through 5), and moves `IronDojo`'s static geometry into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Six resolve (five named-cast prefabs plus the shared generic trooper); everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Morrigan` | `Art/Generated/Characters3D/Named/Morrigan.prefab` | **EXISTS** |
| `Named.MatronHespa` | `Art/Generated/Characters3D/Named/Matron-Hespa.prefab` | **EXISTS** |
| `Named.DrillmasterCaradoc` | `Art/Generated/Characters3D/Named/Drillmaster-Caradoc.prefab` | **EXISTS** |
| `Named.MasterKaelen` | `Art/Generated/Characters3D/Named/Master-Kaelen.prefab` | **EXISTS** |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Enemies.DominionTrooper` | `Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** *(generic — applied additively by `EnemyArtWirer`, same mechanism Ch1 troopers use; not tower-differentiated, see §9)* |
| `Rooms.ForestGround` | `Art/Generated/Rooms/ForestGround.prefab` | MISSING |
| `Rooms.TerraceDeck` | `Art/Generated/Rooms/TerraceDeck.prefab` | MISSING |
| `Rooms.SummitDeck` | `Art/Generated/Rooms/SummitDeck.prefab` | MISSING |
| `Rooms.MorriganSpineShell` | `Art/Generated/Rooms/MorriganSpineShell.prefab` | MISSING |
| `Rooms.IronYardGround` | `Art/Generated/Rooms/IronYardGround.prefab` | MISSING |
| `Rooms.TowerCorridorGround` | `Art/Generated/Rooms/TowerCorridorGround.prefab` | MISSING |
| `Rooms.CradleTowerShell` | `Art/Generated/Rooms/CradleTowerShell.prefab` | MISSING |
| `Rooms.ProvingTowerShell` | `Art/Generated/Rooms/ProvingTowerShell.prefab` | MISSING |
| `Rooms.VestingTowerShell` | `Art/Generated/Rooms/VestingTowerShell.prefab` | MISSING |
| `Props.PineTree` | `Art/Generated/Props/PineTree.prefab` | MISSING |
| `Props.TerraceRock` | `Art/Generated/Props/TerraceRock.prefab` | MISSING |
| `Props.MountainRamp` | `Art/Generated/Props/MountainRamp.prefab` | MISSING |
| `Props.ClimbHold` | `Art/Generated/Props/ClimbHold.prefab` | MISSING |
| `Props.SummitBell` | `Art/Generated/Props/SummitBell.prefab` | MISSING |
| `Props.Aqueduct` | `Art/Generated/Props/Aqueduct.prefab` | MISSING *(not yet referenced by any builder call site — see §9; sources `AqueductWaterAmbience`)* |
| `Props.BellTower` | `Art/Generated/Props/BellTower.prefab` | MISSING |
| `Props.HoloSlateCluster` | `Art/Generated/Props/HoloSlateCluster.prefab` | MISSING |
| `Props.InterceptDriveStack` | `Art/Generated/Props/InterceptDriveStack.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.CradleCrib` | `Art/Generated/Props/CradleCrib.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.NurseryMobile` | `Art/Generated/Props/NurseryMobile.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.SuppressionDrone` | `Art/Generated/Props/SuppressionDrone.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.IntakeNameSlate` | `Art/Generated/Props/IntakeNameSlate.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.SparringPit` | `Art/Generated/Props/SparringPit.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.LiveFireGauntlet` | `Art/Generated/Props/LiveFireGauntlet.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Rooms.ClimbingCourse` | `Art/Generated/Rooms/ClimbingCourse.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.DrillmasterDais` | `Art/Generated/Props/DrillmasterDais.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.BondingCradle` | `Art/Generated/Props/BondingCradle.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.KillswitchTable` | `Art/Generated/Props/KillswitchTable.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.KaelenConsole` | `Art/Generated/Props/KaelenConsole.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |
| `Props.TraineeUniform_Young` | `Art/Generated/Props/TraineeUniform_Young.prefab` | MISSING |
| `Props.TraineeUniform_Cadet` | `Art/Generated/Props/TraineeUniform_Cadet.prefab` | MISSING |
| `Props.TraineeUniform_VestedCadet` | `Art/Generated/Props/TraineeUniform_VestedCadet.prefab` | MISSING |
| `Vfx.TowerCollapse` | `Art/Generated/VFX/TowerCollapse.prefab` | MISSING *(not yet referenced by any builder call site — see §9)* |

**Reuse notes.**

- `Rooms.TowerCorridorGround` serves all three tower corridors (Cradle/Proving/Vesting) — footprint and orientation are per-call-site parameters, not per-prefab variants.
- `Props.TerraceRock` and `Props.PatrolLight`-adjacent fixtures serve all five terraces identically.
- **`EnemyFootPrefabPath`** (`Assets/Ronin7/Prefabs/Art/EnemyFoot.prefab`) — the generic body `BuildEnemy` tries before falling back to a capsule — **is not on disk**, mirroring Chapter 1's `DominionTrooperPrefabPath` gap. The trooper art players actually see is applied additively post-build by `EnemyArtWirer.cs` from `Enemies.DominionTrooper`. Folding that wirer's work into the registry is a natural follow-up, not in this refactor's scope.
- No `Doors.*` keys exist for this chapter (§1.3) — there is nothing to commission here that Chapter 1's `Doors.SlidingDoor_Standard` doesn't already cover for chapters that need one.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter6Builder.cs`, `Project/Assets/Ronin7/Scripts/Editor/ParkourLevelBuilder.cs`, `Project/Assets/Ronin7/Scripts/Editor/ChapterSharedBuilders.cs`, `Project/Assets/Ronin7/Scripts/Editor/Chapter6Lines.cs`, `Project/Assets/Ronin7/Scripts/Editor/Chapter1Builder.cs` (`BuildNpcWalker`), `Project/Assets/Ronin7/Scripts/World/EnemyWaveSpawner.cs`, `Project/Assets/Ronin7/Scripts/World/Story/{MultiObjectiveGate,ActivationRelay,EvacuationTimer}.cs`, `Project/Assets/Ronin7/Scripts/Player/{Climbable,WallClimbLocomotion}.cs`, `story ouput/Ch06_The_Iron_Dojo.md`, `story ouput/Ch06_The_Iron_Dojo_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`.*

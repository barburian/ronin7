# Chapter 3 — Scene Construction

*The architectural contract for `Ch03_SwordRemembers.unity`: what Chapter 3 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 3 ("The Sword Remembers") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants, mission triggers, or the memory-dive teleport contract.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter3Builder.cs`, entry point `XRRigBuilder.BuildChapter3SwordRemembers()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 03 — The Sword Remembers**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch03_The_Sword_Remembers.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it. A mis-scaled ghost figure, a wrongly proportioned corridor, or an over-tall doorway reads as physically wrong to a headset wearer in a way it never would on a monitor.
- **No camera shake, ever.** This chapter has zero combat, but it has the single most emotionally loaded set piece in the game so far — the killswitch trigger in Khall's Bay. That beat's impact is carried entirely by lighting (`MemoryFlashbackController`'s fog/ambient treatment), audio (VO performance, the footage "cracking"), and the ghost figure's fall — **never** by moving the camera.
- **Traversal is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with the standard comfort vignette. There is **no NavMesh, no parkour/climb/wall-run** anywhere in this chapter. There is also, notably, **no NPC-walker traversal at all** — unlike Chapter 1's Kessler, nobody in Chapter 3 physically walks anywhere via the `NpcWalker`/`MissionDirector`-`Trigger` idiom. The only non-player movement in the whole chapter is (a) ambient `StoryNpcWander` idling for three of the four crew, and (b) the two `RedactionSentinel`s' self-driven ping-pong patrol inside the playback (§5). The player's own "travel" between the chapter's two environments is not walked at all — it is an instant, comfort-safe teleport authored by `MemoryDiveController` (§1.2, §5). Do not introduce a walked corridor between the Hold and the playback; the disconnect is the point.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | room shells, props, ghost/memory-cast figures, VFX, backdrops, decorative lights | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | dialogue players, reach points, prompts, dive entry/exit anchors, `RedactionSentinel` wiring, `CampaignFlagSetter`s, mission-spine steps | `[BEAT_N_LOGIC]` |

**As-built, this chapter does not yet observe the split at all.** `BuildChapter3SwordRemembers()` is one large monolithic method (`Chapter3Builder.cs:47–284`) that interleaves lighting, room geometry, cast placement, the playback island, dialogue, and all 18 mission steps in build order. The refactor target is four beat pairs — `BuildBeat0Art/Logic` (the council), `BuildBeat1Art/Logic` (the bonding), `BuildBeat2Art/Logic` (the playback — all four sub-scenes share one pair, since the mission-spine groups them under one "Beat2:" label prefix), `BuildBeat3Art/Logic` (carrying the witness) — mirroring the four beat prefixes already visible in the mission step labels (`"Beat0: …"`, `"Beat1: …"`, `"Beat2: …"`, `"Beat3: …"`).

**The one helper that spans both today, and must be split, is `Ch3BuildPlayback()` (`Chapter3Builder.cs:351–416`).** It currently builds the three playback rooms' shells, props, ghost figures, and lights (art) in the same method that also creates `DiveEntryPoint` (a logic anchor consumed by `MemoryDiveController`). `BuildBeat2Art()` must instantiate the room shells, the static-glitch blocks, the ghost figures, the kill-order glyph, and the playback accent lights, returning handles; `BuildBeat2Logic()` must own `DiveEntryPoint`, both `RedactionSentinel`s (waypoints + detection wiring + the `OnSpotted → dive.ResetToEntry` listener), and the reach points. **The `RedactionSentinel`'s censor-slab geometry is the same art/logic split as Chapter 1's door:** `BuildBeat2Art()` instantiates the slab + static-band visual and returns its handle; `BuildBeat2Logic()` attaches the `RedactionSentinel` component and wires its waypoints/target/event. Art builds the thing; logic decides what it does.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildDoorwayWall`, `BuildProp`, `BuildAccentPointLight`, `BuildRoomDetails`, `AddConsoleFlicker`/`AddAmbientPulse`, `InstantiateNpc`, `FitNamedCharacter`, `BuildSword`, `BuildDialoguePlayer`, `Author*Step`, `BuildAmbienceLayer` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`. Also frozen for the same reason, even though they live under `Ronin7.World.Story` rather than the editor assembly: `MemoryDiveController`, `MemoryDiveEntryTrigger`, `MemoryDiveExitTrigger`, `MemoryFlashbackController`, and `RedactionSentinel`. Their class docs are explicit that they are **chapter-agnostic** ("First used by Ch03 … reusable by any later chapter's memory-space beat") — treat them exactly like a `ChapterSharedBuilders` helper, not a Ch3-local one.
- **Free to restructure:** the Ch3-local helpers, called only from `BuildChapter3SwordRemembers()` — `Ch3BuildDialogue`, `Ch3WireVoiceClips`, `Ch3PlaceStoryNpc`, `Ch3BuildPlayback`, `Ch3BuildPlaybackLight`, `Ch3BuildGhostFigure`, `Ch3BuildRedactionSentinel`, `Ch3BuildHoldStory`, `Ch3BuildPrompt`, `Ch3BuildCompleteCanvas`.

This refactor lives entirely in the second list (plus the split of `Ch3BuildPlayback` described above). If you find yourself editing `ChapterSharedBuilders.cs` or the `Ronin7.World.Story` memory-dive family's public contracts, stop — you have left Chapter 3 and are now silently touching every chapter that will ever reuse a memory-dive beat.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** One new ScriptableObject carries what the builder currently types inline for the Cairn Hold:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch3Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, floor + ceiling tint, the three Hold accent lights, their behaviours |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared asset across all chapters, same as Ch1) |

**Chapter 3 does not need a second profile for the playback.** Unlike the Hold, the playback's mood is *already* data-driven — `MemoryFlashbackController`'s own serialized fields (`fogColor`, `fogDensity`, `ambientColor`, an optional `heartbeatLoop` clip) carry exactly the values `ChapterEnvironmentProfile` would otherwise duplicate. The correct refactor is to leave the playback's atmosphere on that component (tune it via its inspector, or wire its fields from a shared "memory-dive tone" asset if a future chapter wants to share one) rather than inventing a redundant profile asset. `Ch3Environment.asset` need only cover the Hold.

Neither `Ch3Environment.asset` nor `ArtAssetRegistry.asset` exists yet. Prefab paths never appear in builder code — the builder asks the registry for `Props.WeaponRack_Frame`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching Ch1's convention:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Kessler/Iris/Resh/Mira/Khall/Echo all present)
  Rooms/                                    ← new (shared across chapters)
  Props/                                    ← new
  VFX/                                      ← new
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — same caveat as Chapter 1.** `BuildChapter3SwordRemembers()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter3Builder.cs:51`), which discards the whole scene rather than deleting named objects from it. Converting the safe zone from decorative to real requires the same fix documented in `Ch01-Scene-Construction.md` §1.4: open the existing scene and `DestroyImmediate` each generated root by name (`CairnHold`, `Playback_Kethel7`, `Game`, `Mission`, the rig, the accent lights, dialogue players, reach/dive anchors), falling back to `NewScene` only when the scene file does not yet exist. **Option 1 (open-in-place) is preferred**, per `EnemyArtWirer.cs`/`CrowdArtWirer.cs`'s existing pattern.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable. **One nuance specific to this chapter:** the entire `Playback_Kethel7` root is built **inactive** (`diveRootGo.SetActive(false)` at `Chapter3Builder.cs:101`) and only ever activated at runtime by `MemoryDiveController.EnterDive()`. A safe-zone-preserving rebuild must re-parent this root's *art* children under `[STATIC_ART_DO_NOT_DELETE]` while leaving it inactive; the two `RedactionSentinel`s and `DiveEntryPoint` (logic) still live inside that same hierarchy today (§1.2) and must be extracted to `[BEAT_2_LOGIC]` as part of the split, not left orphaned mid-tree.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for this chapter.** No room shell, no weapon rack, no drive stack, no ghost-figure mesh, no redaction-sentinel mesh. See Appendix B for the full inventory: the **six named-cast prefabs** (Kessler, Iris, Resh, Mira, Khall, Echo) are the only resolvable keys; every set-dressing prop is a commission.

A builder that instantiates from an empty registry produces **an empty room** — the first run of the refactored builder would destroy Chapter 3, including its two most distinctive spaces (the desaturated corridor and Khall's Bay).

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_WeaponRackFrame);
if (prefab == null) {
    Debug.LogWarning($"[Ch3] {ArtKey.Props_WeaponRackFrame} unresolved — primitive fallback.");
    BuildWeaponRackPrimitive(staticArtRoot);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`). The chapter must remain playable — including a fully navigable playback island — at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). As with every other chapter, treat 90 FPS as the ceiling to protect and `QualityBootstrap`'s 72 Hz default as the floor actually shipped against today.
- **No scene-specific greybox baseline is recorded for Chapter 3** in `Project/Docs/CHAPTER-BUILD-LEDGER.md` — that ledger tracks EditMode test-count deltas across the chapter migration (Ch3's fixture pass landed at 452 tests, PASS 2026-07-03), not `UnityStats` draw-call/tri numbers the way Ch1's ledger entry does. **Do not invent a number here.** Before the first prefab lands in this chapter's registry slots, capture a `UnityStats` reading (drawCalls/setPassCalls/tris/verts) at greybox as the reference bar, the same way Ch1's §1.6/§8 do, and record it in the ledger.
- Chapter 3 is comparatively cheap to begin with: one compact 10×14 room plus three playback rooms that are **inactive and therefore non-rendering** until the dive fires, with zero enemy AI, zero `BladeDamager` VFX, and only two humanoid-shaped primitives moving at a time (the two `RedactionSentinel`s). The main perf risk when swapping in prefabs is the same as everywhere else: **set-dressing props here are cheap primitives tinted via the shared `TintShared`/`BuildProp` helper (MaterialPropertyBlock batching)** rather than unique materials. Prefabs replacing them will not batch this way — re-measure after every swap.

## 2. Chapter spatial map

Chapter 3 is **one scene**, `Assets/Ronin7/Scenes/Ch03_SwordRemembers.unity`, but unlike Chapter 1's single continuous corridor, it is **two disconnected environments** separated by a large, deliberately unbuilt gap in +Z (z 10 to z 40) — there is no walkable path between them. The Cairn Hold is where the player spends Beats 0, 1, and 3; the Playback island exists only as an inactive GameObject tree until `MemoryDiveController.EnterDive()` activates it and **teleports** the rig directly into it. There is no corridor to walk, no door to open, between the two.

```
 THE CAIRN HOLD (real, warm, z[-4,10])                    ← unbuilt gap, z(10,40) →   THE PLAYBACK — KETHEL-7 (inactive until dive, z[40,80])

 x[-5,5] z[-4,10]                                                                     Threshold Corridor   Aftermath Room      Khall's Bay
 center (0,0,3), 10x14                                                                x[-3,3] z[40,56]     x[-5,5] z[56,68]    x[-4,4] z[68,80]
 SOLID WALLS — no doors, nothing boards this ship this chapter                        center (0,0,48)      center (0,0,62)     center (0,0,74)
                                                                                       6x16                 10x12               8x12

 Player teleports in at DiveEntryPoint (0,0,42) on EnterDive(); teleports back to DiveExitPoint (0,0,8) on ExitDive() — never walked, no lerp.
```

| Beat | Room | Footprint | Floor center / size |
|---|---|---|---|
| 0, 1, 3 | The Cairn Hold | x[-5,5], z[-4,10] | center (0,0,3), 10×14 |
| 2A | Playback — Threshold Corridor | x[-3,3], z[40,56] | center (0,0,48), 6×16 |
| 2B | Playback — Aftermath Room | x[-5,5], z[56,68] | center (0,0,62), 10×12 |
| 2C/2D | Playback — Khall's Bay | x[-4,4], z[68,80] | center (0,0,74), 8×12 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, same as every other chapter.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly.

**Doors:** none. This is the load-bearing spatial difference from Chapter 1 — no `SlidingDoor` component is ever instantiated in this chapter. The Hold's four walls (`Hold_WallW`/`_WallE`/`_WallFront`/`_WallBack`) are all fully solid; the corridor's front wall (`Corridor_WallFront`, z=40) is fully solid; only the two internal thresholds *inside* the playback carry an aperture, and those are built with `BuildDoorwayWall` (a passable gap, not a `SlidingDoor` prefab — there is nothing to lock or unlock):

| Threshold | Position | Gap width | Purpose |
|---|---|---|---|
| `Corridor_WallBack` | (0, RoomH/2, 56) | 1.6 m | the half-shut door the caretaker stands before; children read as beyond it — **gap: no door-leaf prop occupies the aperture yet (§4 Beat2c)** |
| `Aftermath_WallBack` | (0, RoomH/2, 68) | 2.0 m | Aftermath Room → Khall's Bay, the stealth-approach threshold `Redaction_BayApproach` guards |

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (Echo's head-locked ambient combat-commentary subtitle — additive, no inspector wiring, dormant until `ch3_complete`; see §4 Beat3b for what that flag turns on). `ZoneBounds` is set to **center (0, 0, 38), radius 55** — one bounding sphere loosely enclosing both the Hold and the entire playback island, since the rig physically occupies both at different times via teleport. The rig's own spawn position is not overridden in this builder (inherits whatever default `BuildRig` and the scene-load spawn convention supply, same as other chapters).

## 3. Global environment & backdrop

**The Cairn, running quiet after Velorum:** per the dialogue script's SETTING block, this is the same salvaged leviathan from Chapter 1, but no longer a tomb with one tired man in it — the crew recovered at Velorum live here now. The engines idle low; the adrenaline of the auction is a few hours astern. Power is steadier than Ch1's "power is thin" texture, but the ship is still secondhand everywhere — "patched conduit, mismatched panels," per the SETTING block, and the smell (implied) of ozone and old coffee. That surface texture isn't yet passed to art anywhere below; fold it into the `Rooms.CairnHoldShell` commission direction (§4 Beat0c, Appendix B) as mismatched/patched panel materials rather than a uniform clean shell — it's the cheapest way to make "still secondhand everywhere" read, and it's what distinguishes this Hold from Ch1's tomb-version of the same leviathan. The Hold reads as a work-and-rest space carved out of the salvage core: Iris's bench and single work lamp, Resh's drive stacks (the copied vault files being sorted), Kessler's battered pot and three cups, a bulkhead weapon rack where the katana hangs, and a quiet back corner — the playback seat — where Ronin-7 settles to let the AI replay its footage. The chapter's dread is structural: this room is meant to read as safe, which is exactly what makes the wake frightening. The threat this time is not coming up a clamp from outside; it is already in the room, in his hand, behind his own eyes.

**The Playback — an abstract recording, not a place:** desaturated to near monochrome, color drained to the bone except for two things — the red kill-order glyph, and, later, blood (the aftermath room's dismembered dead, carried as narrative color, not modeled geometry — see Beat 2's `d. Combat` note). The geometry is the geometry of a dead mission rendered as failing footage: misaligned "static block" frames along the corridor walls, edges that don't quite meet, blocks of the image scrubbed where the conditioning redacted the record. Sound is close and padded, "like a held breath behind glass." Ronin-7 walks through it as a witness who can be touched — the system does not want this footage seen and pushes back via the `RedactionSentinel`s.

**The Aftermath's canon "smoke" is currently just the shared playback fog (gap, not a bug — see §4 Beat2c).** The screenplay names "smoke" three times specifically for the Aftermath Room ("Smoke hangs," "smoke hangs in the drained air," "Smoke."). As-built, the room relies solely on `MemoryFlashbackController`'s single `ExponentialSquared` fog treatment — the same uniform bed shared by every other playback room — so the Aftermath's specific hanging-smoke character is lost in the general island haze.

**Canon tension — the missing viewport (flagged, not resolved).** The dialogue script's SETTING and stage directions call for a viewport three separate times: the opening SETTING block ("outside is open transit-black, stars sliding slow"), the Hold's pre-dive note ("the wreck-field beyond the viewport is gone"), and Beat 3's closing image ("the open black sliding past the viewport"). But §2 states the Hold's four walls are `SOLID WALLS`, and §6's lighting table reads "none — enclosed room, no exterior view" for every Hold beat — as-built, the room has no exterior view at all. This is the single largest sensory element in the script's SETTING block the current geometry drops. The screenplay's viewport is deliberately unbuilt today; it is not a bug to silently fix by punching a hole in a solid wall (that would break the "SOLID WALLS — no doors, nothing boards this ship this chapter" footprint in §2), but it is the strongest immersion opportunity in this chapter. Recommended resolution when art lands: a fixed, non-opening emissive starfield panel on `Hold_WallBack` (z10) or `Hold_WallFront` (z−4) — no parallax, no camera-relative motion needed, just a slow-scrolling star texture on an unlit material. It costs nothing in comfort (it's a flat backlit panel, not a window the player can lean through) and gives the Beat 3 "warm secondhand light" a cold exterior to read against, exactly as the surfacing dialogue implies.

**Canon tension — the playback's blue-grey light "bleeds back" into the Hold at the climax (flagged, not resolved; see also §6).** The dialogue script's SETTING block states this plainly, and it is the screenplay's one explicit sensory bridge between the chapter's two otherwise-disconnected environments: "The playback's blue-grey light bleeds back into this room at the climax" (`Ch03_The_Sword_Remembers_Dialogue_Script.md:50`). This is a distinct claim from the missing viewport above and from §6's hard fog cut — it's the one place the screenplay says the Hold and the playback are not fully sealed off from each other, which cuts directly against this document's own thesis (§1.1, §2) that the two environments are disconnected. As-built and as-targeted it cannot happen: `Playback_Kethel7` is inactive and 30 m up +Z of the Hold for the whole of Beat 2, and the rig is teleported bodily into it rather than remaining in the Hold to watch light spill across — there is no shared volume for light to cross back through. Treat this as a deliberate simplification, not a bug to chase with a cross-scene light leak. The comfort-safe additive, parallel to the viewport recommendation: a subtle cold blue-grey color/intensity lerp ramped onto `HoldLampSeat` (the seat corner, z8, where the player will shortly surface) timed to `ch3_beat2_execution`/`ch3_beat2_burndown` — a lighting foreshadow of the coming surface that costs nothing in comfort (a color/intensity animation on an existing point light, no camera motion), safe by the same rule as the glyph-pulse additive (§9).

### 3.1 `ChapterEnvironmentProfile` — the master palette (the Hold only)

**No lighting value, color, or fog density for the Hold is typed into `Chapter3Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch3Environment.asset`. Its schema mirrors Ch1's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` | `Color` | the Hold's `BuildFloorCeiling` call |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` × 3 |

The current literal values (recorded in Appendix A.1) are a lift-and-shift, not a re-lighting pass:

| | Value |
|---|---|
| Directional key | color (1, 0.85, 0.65), intensity 0.45, rotation Euler(50, -35, 0) |
| Ambient | mode **Flat**, color (0.14, 0.12, 0.10) |
| Fog | mode **Exponential**, color (0.17, 0.14, 0.11), density 0.018 |
| Floor tint | (0.2, 0.17, 0.14) |
| Ceiling tint | (0.1, 0.09, 0.08) |

**Accent lights** (three, all in the Hold — the "running-quiet twilight" per the builder's own comment: dim warm key + low warm ambient, the safe domestic room the wake is about to make strange):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `HoldLampBench` | (-3, 1.6, 2) | (1, 0.8, 0.55) | 1.6 | 8 | `ConsoleFlicker(seed: 33)` — Iris's single work lamp |
| `HoldLampSeat` | (0, 2.4, 8) | (1, 0.75, 0.5) | 1.2 | 9 | `AmbientPulse(period: 6s)` — the playback seat corner |
| `HoldLampRack` | (4, 2.2, 6) | (0.85, 0.8, 0.7) | 1 | 7 | none — the weapon rack, deliberately steady |

**Affirm, don't fix — the Hold's front (z −4) is intentionally unlit "warm dark."** The dialogue script calls for this explicitly: "Nobody is on watch" in the Beat 1 SETTING block, and Kessler later "drifts back into the warm dark of the hold, present but unseen" (`Ch03_The_Sword_Remembers_Dialogue_Script.md:39,149`). All three accent lights above cluster at z2–8 (bench, seat, rack); nothing is placed toward `Hold_WallFront` (z −4), so the front third of the room already falls to unlit warm shadow as a byproduct of that placement. This gradient is desirable, not a gap — it is the screenplay's "warm dark" existing as a happy accident of the lamp layout. When the `Rooms.CairnHoldShell` prefab and its baked lighting land, preserve this falloff rather than "fixing" it with a fourth fill light toward the front wall.

The playback's own three lights (`PlaybackLight_Corridor`/`_Aftermath`/`_Bay`) are parented directly under the inactive `Playback_Kethel7` root rather than authored as standalone profile entries — they only exist while the dive is active, and their cold, drained values (documented in Appendix A.4) are cheap point lights with shadows off, not profile-driven. This is a deliberate difference from the Hold, not an oversight: a light that only ever exists while its parent GameObject is active does not need a data-driven behaviour enum the way an always-on room light does.

## 4. Per-beat scene spec

The chapter plays as four beats, matching the four `"BeatN: …"` prefixes already visible in the mission-spine step labels. Each beat is documented with the same a–f structure. Beat 2 covers all four narrative sub-scenes (Threshold / Aftermath / Khall's Bay / Burn-Down) inside one Art/Logic pair, since the playback is one contiguous island built and torn down as a unit.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the memory-dive teleport contract.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row.

---

### Beat 0 — The Council (A Heading, Not Yet a Wake)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes, cylinders, or hardcoded sizes for any props. You must read from the centralized `ArtAssetRegistry` ScriptableObject for all environment prefabs. Create separate methods: **`BuildBeat0Art()`** for the Hold's static room/props, and **`BuildBeat0Logic()`** for the crew spawns, the briefing dialogue, and the mission-spine's first step.

#### a. Narrative purpose & emotional target

This is the crew's first real council since Velorum — a day off the rock, the adrenaline drained out, the kind of quiet that only lands once the danger is a few hours astern. The scene has to do two things before the wake ever happens: establish the room as ordinary, and plant the chapter's second thread (the hunt for other leashed operatives) a full beat before the katana's own reveal makes it personal. Resh wants a heading; Iris has one, buried in the copied Velorum files — "your switch fired and failed, and yours is the only one on record that did… but you are not the only designation in here." Kessler settles the council with a captain's plain authority: rest first, chase it rested. Ronin-7 says nothing through all of it — he sits apart with the wrapped katana across his knees, present but withheld, which the player should read in hindsight as the calm before the beat that breaks it.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** `BuildRig(addLocomotion: true)` + `EchoPresence` + `ZoneBounds` center (0,0,38) radius 55 (authored once, chapter-wide). The player does not need to travel for this beat to play; the crew's dialogue plays wherever the player is standing in the Hold.
- **Crew spawns**, all via `Ch3PlaceStoryNpc` (`StoryNpc` + optional `StoryNpcWander`):

  | Character | Position | Wander radius | Notes |
  |---|---|---|---|
  | Kessler | (-1.6, 0, 4.5) | 0.7 m | pours from a battered pot, "two cups out of old habit, then a third" |
  | Iris | (-3.2, 0, 2.6) | **0** (no wander component added) | fixed at her bench the entire chapter — the only crew member who never idles-wanders |
  | Resh | (2, 0, 3) | 0.8 m | sorting the copied drive stacks into piles |
  | Mira | (1.2, 0, 0.5) | 1.0 m | the stowaway, underfoot — no lines this beat, and no lines anywhere else in this chapter either (§5) |

- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 3).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `ch3_beat0_briefing` — Resh/Iris/Kessler's council on the vault files and the "if one leash slipped, others can" thread; Kessler settles it: rest first |

**What changes during the beat:** nothing in the set dressing — the room is exactly as built, no props added or removed. The only state that changes across the whole chapter's opening stretch is narrative: the crew stops talking and Ronin-7 crosses, unprompted, to the weapon rack, which is Beat 1's opening image.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

All environment prefabs instantiate from the `ArtAssetRegistry` and parent to `[STATIC_ART_DO_NOT_DELETE]`.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (10×14, walls + floor + ceiling, all solid) | center (0,0,3) | `Rooms.CairnHoldShell` | `…/Art/Generated/Rooms/CairnHoldShell.prefab` | **MISSING** |
| Iris's bench | (-3.6, 0.45, 2.2) | `Props.WorkBench_Salvage` | `…/Art/Generated/Props/WorkBench_Salvage.prefab` | **MISSING** |
| Stripped board (on the bench) | (-3.5, 0.95, 2.2) | `Props.StrippedBoard` | `…/Art/Generated/Props/StrippedBoard.prefab` | **MISSING** |
| Iris's cracked slate (the Velorum files she's reading) | (-3.7, 0.95, 2.35), on the bench beside the stripped board | `Props.Slate` | `…/Art/Generated/Props/Slate.prefab` | **MISSING** |
| Drive stack ×3 (the copied vault files) | (2.4,0.15,2.4) / (2.9,0.1,2.6) / (2.6,0.42,2.45) | `Props.DriveStack` | `…/Art/Generated/Props/DriveStack.prefab` | **MISSING** |
| Galley shelf | (-1.5, 0.45, 8.8) | `Props.GalleyShelf` | `…/Art/Generated/Props/GalleyShelf.prefab` | **MISSING** |
| Kessler's battered pot | (-1.5, 0.96, 9.0), on the shelf beside the cups | `Props.BatteredPot` | `…/Art/Generated/Props/BatteredPot.prefab` | **MISSING** |
| Dented cup ×3 (two out of habit, then a third) | (-1.8,0.96,8.8) / (-1.5,0.96,8.8) / (-1.2,0.96,8.8) | `Props.DentedCup` | `…/Art/Generated/Props/DentedCup.prefab` | **MISSING** |
| Weapon rack (back panel + two pegs) | (4.85,1.2,6) / pegs at (4.6,1.12,5.6) & (4.6,1.12,6.4) | `Props.WeaponRack_Frame` | `…/Art/Generated/Props/WeaponRack_Frame.prefab` | **MISSING** |
| Playback seat + backrest | (0, 0.3, 8.6) / (0, 0.85, 9.05) | `Props.PlaybackSeat` | `…/Art/Generated/Props/PlaybackSeat.prefab` | **MISSING** |
| Iris's improvised vitals rig (monitors him from outside the dive) | (0.6, 0.5, 8.8), near the seat corner | `Props.VitalsRig` | `…/Art/Generated/Props/VitalsRig.prefab` | **MISSING** |
| Katana "Echo," resting on the rack | (4.3, 1.2, 6), rot identity | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| Kessler | (-1.6, 0, 4.5) | `Named.Kessler` | `…/Art/Generated/Characters3D/Named/Kessler.prefab` | **EXISTS** |
| Iris | (-3.2, 0, 2.6) | `Named.Iris` | `…/Art/Generated/Characters3D/Named/Iris.prefab` | **EXISTS** |
| Resh | (2, 0, 3) | `Named.Resh` | `…/Art/Generated/Characters3D/Named/Resh.prefab` | **EXISTS** |
| Mira | (1.2, 0, 0.5) | `Named.Mira` | `…/Art/Generated/Characters3D/Named/Mira.prefab` | **EXISTS** |
| `HoldLampBench`/`HoldLampSeat`/`HoldLampRack` | see §3.1 table | — | read from `ChapterEnvironmentProfile.accentLights[]` | profile |
| `HoldAmbience` | (0, 1.5, 3) | — | `AudioSource`, procedural ambience layer (no prefab) — a low engine-idle bed, per the dialogue script's "engines on a low idle hum," deliberately contrasted against the playback's `PlaybackDreadAmbience`, "close and padded, like a held breath behind glass" (§7) | audio |

**Notes on the transition.** The katana rests flat on the rack "along the pegs" at rack height, `Quaternion.identity` — the prefab's native forward axis is expected to already read as lying along the rack's own long axis (the pegs run z 5.6→6.4, a ~0.8 m span); if a future prefab swap lands with a different native orientation, that rotation is the first thing to re-check, not the position. `BuildRoomDetails(hold, "Hold", center (0,0,3), halfExtents (5,7), accent (0.38,0.3,0.24))` scatters additional generic salvage-core dressing across the floor and is a single call, not itemized per-prop here — treat it as one more registry-driven scatter pass once a "detail prop set" key exists.

**The katana row above is registry-shaped in this table but not registry-driven in code — flag before refactoring.** Unlike every other row in this table, the katana is not resolved via `ArtAssetRegistry.Resolve` with a primitive fallback; it is instantiated by the frozen `BuildSword(pos, rot, weapon, Ch3EchoBladePrefab)` helper (§1.2), which carries a hardcoded prefab path *and* wires the `WeaponDefinition` component a registry lookup would not supply. A refactor must not reroute the player sword through `Resolve`/primitive-fallback — it stays on `BuildSword`, with `Named.Echo` above naming the prefab that helper points at, not a registry key it reads from.

**Kessler's battered pot is missing from the as-built galley dressing.** §3 names it explicitly ("Kessler's battered pot and three cups") and the screenplay stages his one characterizing gesture around it ("pours from a battered pot") — but `Ch3BuildHoldStory` (`Chapter3Builder.cs:509-544`) builds `GalleyShelf` and the three `Cup_0/1/2` cylinders and nothing else; there is no pot object anywhere in the as-built hierarchy or Appendix A.2. This is the same shape of gap as the missing third-cup staging and the missing threshold ghost below — an object the prose and this document's own §3 call for that the geometry omits. The `Props.BatteredPot` row above is the fix.

**Iris's cracked slate is also missing, and it motivates the entire briefing beat.** She reads the Velorum files "off a cracked slate" and the stage direction has her tap it ("(taps the slate)") while she delivers the beat's key line — "I've been reading the files since we burned out of orbit." `Ch3BuildHoldStory` builds `StrippedBoard` on her bench, but that is the electronics she's repairing, a different object from the file-reading slate the briefing dialogue is staged around. Without it, the object that visually motivates Beat 0's entire premise (Iris found the heading in the files) has no prop on her bench at all. The `Props.Slate` row above is the fix.

**Iris's vitals rig is staged in dialogue but has no prop — the same gap family as the pot/slate above.** §3's SETTING line ("Iris's improvised rig monitors his vitals from outside") and Beat 1's "Watch my vitals. If I stop answering, don't try to pull me out" both pivot on a vitals-monitor object near the playback seat, and Beat 3's surfacing line ("you were gone four minutes and your eyes were moving the whole time," §4 Beat3e) is the payoff of that same object having been watched the whole dive. `Ch3BuildHoldStory` builds no such prop today. The `Props.VitalsRig` row above is the fix — placed near the seat corner so Iris's staging and the surfacing beat have a physical anchor, and so the "improvised rig" reads as secondhand salvage (§3 texture), not clean hardware.

**The third-cup gesture is currently un-staged spatially.** The screenplay's inclusion beat is specific: Kessler pours "two cups out of old habit, then a third he sets near Ronin-7." As-built, all three cups cluster on the galley shelf 0.3 m apart (`Cup_0/1/2`, x −1.8/−1.5/−1.2, Appendix A.2) — none placed near Ronin-7's apart-seat position, so the gesture reads only in the VO, not in the room. Suggested target-state fix: relocate `Cup_2` toward the playback seat (~0, 0.96, 8.6, beside where he "sits apart with the katana across his knees") so the inclusion reads spatially as well as narratively.

#### d. Combat

None. This beat is pure dialogue/exploration; the entire chapter has zero enemy spawns and zero `BladeDamager` fights (§9).

#### e. Dialogue / VO

Dialogue set id **`ch3_beat0_briefing`**, position (0, 1, 3), advanced by the Left-Hand **Talk** (Y) action — 6 lines, ~52 s total:

| Speaker | Line | sec |
|---|---|---|
| Resh | We came off that rock with a freed tech, a stowaway, and a vault's worth of dead men's files. Generous haul. What I don't have is a heading. | 10 |
| Iris | I might have one. I've been reading the files since we burned out of orbit. Your switch fired and failed, and yours is the only one on record that did. I still can't tell you why. But you are not the only designation in here. | 14 |
| Kessler | Meaning what. | 1 |
| Iris | Meaning if one leash slipped, others can. Somewhere out there are operatives still wearing theirs, not knowing it can break. | 9 |
| Resh | So we go find them. That's a heading I can work with. Beats bailing one kid out of the dark at a time. | 8 |
| Kessler | After everyone sleeps. The ship's been running on his nerves and my coffee for a day. Whatever we go after, we go after it rested. | 10 |

**Text per shipped `Chapter3Lines.cs`, audit-thinned relative to the original screenplay** — e.g. Kessler's closing line above ("Whatever we go after, we go after it rested") is shorter than the screenplay's more symmetrical original; see §9's canon-soft-spot note for why. This and every later dialogue table in this document quote the shipped, thinned text, not the screenplay's — a future editor spot-checking against `Ch03_The_Sword_Remembers_Dialogue_Script.md` should expect the divergence rather than "fix" the wording back.

Ronin-7 has no lines in this beat — the screenplay stage direction is explicit that he "says nothing through all of it," sitting apart with the wrapped katana across his knees. Do not add lines here; his silence is load-bearing (§9).

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — pure dialogue beat.
- **Ambient bed:** `HoldAmbience` (`BuildAmbienceLayer("HoldAmbience", (0,1.5,3), inner 3, outer 10, maxVolume 0.4)`) runs continuously, unbroken across all three Hold beats.
- **Lighting:** `HoldLampBench` runs `ConsoleFlicker(seed: 33)` and `HoldLampSeat` runs `AmbientPulse(period: 6s)` from the moment the scene loads — both are *deliberately already running* under this beat, not triggered by it; they are the room's ambient "lived-in" texture, not an event cue.
- **Comfort:** the player is free to walk during the dialogue (no reach gate on this beat); the comfort vignette engages normally on any snap-turn/movement input.
- **Haptics:** none — no combat, no grip event yet.

---

### Beat 1 — The Bonding (The Wake, at the Weapon Rack)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. **This beat builds no new geometry** — create **`BuildBeat1Logic()`** only, for the rack reach point, the grip prompt, the two dialogue sets, and the trigger that starts the memory dive. `BuildBeat1Art()` should be a stub that documents the reuse, exactly as Chapter 1's Beat 3 medbay kill-box reuses Beat 1's room without rebuilding it.

#### a. Narrative purpose & emotional target

The wake. Ronin-7 crosses to the rack unprompted and reaches for the katana; the instant his hand closes on the grip, a low tone rises — not in the room, behind his eyes. Iris, at the bench, doesn't react; neither does Kessler — and the shipped `ch3_beat1_bonding` set opens on exactly that non-reaction, Iris's "What. You hear something?" registering only that Ronin-7 has heard *something*, not what. (Iris's spawn is fixed ~7 m from the rack; the exchange is staged entirely through VO, not a walked approach — see §5's crew-proximity note.) **Factual correction — the "It's a sword" hand-off is screenplay-only, not shipped.** The beats screenplay (`Ch03_The_Sword_Remembers.md:58-61`) stages a physical proof beyond the non-reaction: Ronin-7 hands the blade to Iris, "she takes it. Nothing. Dead weight in her hands. IRIS: It's a sword." — but that hand-off was thinned out of the shipped VO. `Chapter3Lines.cs`'s `ch3_beat1_bonding` set contains no "It's a sword" line at all (a grep for the phrase returns zero hits), and §4's own line table below correctly omits it. As-built, **the "alone rule" has no spoken mechanical proof in this beat** — it is carried entirely by Iris/Kessler's non-reaction to the tone, plus Shadow's own line in Beat 3 ("In anyone else's hand I'm a very good knife and nothing else"). Do not restore the "It's a sword" line or commission a two-hand blade-transfer prop/interaction for this beat — it was deliberately authored away, not omitted by accident. The emotional target is disorientation curdling into intimacy — a weapon that has always been a stranger's eyes on him, revealed the instant he actually listens. Shadow introduces itself dry, warm, and wry from the first syllable, "already a person" per the dialogue script's direction — the horror of this beat is not a monster in the steel, it's that the watcher has been kind the whole time and nobody told him.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to a `[BEAT_1_LOGIC]` root object.

- **`RackReachPoint`** at (4, 1, 6), radius **2.5 m** — gates the beat's opening on the player physically walking to the rack, matching the screenplay's "he crosses to the weapon rack and reaches for the katana."
- **`GripKatanaPrompt`** (worldspace `TextMesh`, "Grip the Katana (Y)") at (4, 1.6, 6), created **inactive**, carrying a `PromptInputAdvancer` wired to the shared Left-Hand Talk action and to `MissionDirector`. Activated only for step 2.
- **Dialogue anchors:** `Dialogue_Beat1_Bonding` at (3.5, 1, 6); `Dialogue_Beat1_ShadowExplains` at (2, 1, 7).
- **`EnterDiveTrigger`** — an inactive `GameObject` carrying `MemoryDiveEntryTrigger` (wired to the `MemoryDiveController`). Its `OnEnable` calls `dive.EnterDive()` directly — this is the "inactive until a Trigger step activates it" idiom shared with `NpcWalker`, letting a plain `MissionStepKind.Trigger` step start a memory dive without `MissionDirector` ever needing to know what a dive is.
- **Implementation note — the alone-rule is not mechanized, it is written, and it has no spoken hand-off proof at all.** As §a now notes, there is no "It's a sword" hand-off in the shipped VO; as-built (and as-targeted) there is (and as-targeted) there is no per-listener gating anywhere in this chapter, and no scripted blade-transfer either — every AI line, including the ones only Ronin-7 is meant to hear, plays through one ordinary `DialoguePlayer`/`AudioSource` that any NPC in range would hear if it were listening, and the katana never physically changes hands (there is no grab/transfer event, scripted or otherwise). The rule is carried entirely by writing and NPC non-reaction — Iris and Kessler simply have no line acknowledging the tone. Do not build a two-hand sword hand-off interaction or a spatial-audio occlusion system for this beat — the chapter neither has one nor needs one; see the same note echoed against Beat 3's private asides (§Beat3e).
- **Implementation note — the dive is not staged from the seat, only the surfacing is.** The screenplay stages the dive seated ("sets the katana flat across the seat… lays his palm flat on the wrap… Close your eyes"), but there is no reach gate to the playback seat (0, 0.3, 8.6) between step 4 (`ch3_beat1_shadow_explains`, anchor (2,1,7)) and step 5 (`EnterDiveTrigger`) — the player dives from wherever they were standing when the dialogue advanced, then *surfaces* at the seat corner (`DiveExitPoint`, (0,0,8)) on the way out. This in/out asymmetry (dive-in unstaged, dive-out staged at the seat) is a deliberate simplification, not a bug — anchor (2,1,7) sits beside the seat's own corner, so the mismatch is minor — but a future pass adding a seat reach-gate before step 5 would close it if the posture ever needs to read literally.
- **Wayfinding gap (flagged, not fixed) — nothing draws the player to the rack after the briefing ends.** When `ch3_beat0_briefing` ends, step 1 reach-gates the player on `RackReachPoint`, but nothing in the room actively directs them there: `GripKatanaPrompt` only appears *after* they arrive (step 2), the katana hasn't woken yet, and Shadow is still silent. The only diegetic draw is `HoldLampRack` (§3.1) — the one accent light the profile deliberately keeps steady, unflickering, unpulsed, a calm pool of light resting on the player's own weapon. `HoldLampRack`'s steadiness is therefore doing wayfinding double-duty: it both reads as the room's "safe, unremarkable" baseline (§3.1's stated intent) and is the sole visual pull toward step 1's objective. A future lighting pass should preserve that steadiness *and* its role as the post-briefing anchor, rather than dimming `HoldLampRack` toward the "warm dark" front falloff (§3.1) for mood reasons alone.
- **Immersion opportunity (flagged, not built) — the dive-in has no volitional trigger.** The screenplay begins the dive on a deliberate act — Ronin-7 "lays his palm flat on the wrap… control narrows to a single input" — but step 5 as-built is a plain `Trigger` step whose `EnterDiveTrigger.OnEnable` fires the instant step 4's dialogue completes; nothing requires the player to *do* anything to cross into the memory. The beat already has the exact idiom this wants: `PromptInputAdvancer`/`Ch3BuildPrompt`, the same "grip prompt" mechanism gating step 2. A "press to dive" prompt reusing that idiom — activated after step 4, advancing the director into step 5 only on a deliberate Left-Hand Talk (Y) press — would turn crossing into the memory into a chosen, embodied act rather than an automatic cut, echoing Shadow's own "Close your eyes" as a beat the player enacts rather than merely watches. Recommended as a future juice pass, in the same spirit as this document's other flagged additive opportunities (the wake tone above, the glyph pulse in §9) — not asserted as present today. **If this pass lands, it should bookend both ends of the memory, not just the entrance** — see §5's matching gap on the burn-down's exit side, where the same auto-fire pattern collapses the screenplay's walked "final short push toward the exit-light."

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | ReachTrigger: The Weapon Rack | gates on `Camera.main` distance to `RackReachPoint` (4,1,6), radius 2.5 |
| 2 | Prompt: Grip the Katana (the wake) | `GripKatanaPrompt` activates; Left-Hand Talk (Y) via `PromptInputAdvancer` advances the director |
| 3 | Dialogue | `ch3_beat1_bonding` — the tone, the Iris handoff proof, Kessler and Iris realizing they hear nothing |
| 4 | Dialogue | `ch3_beat1_shadow_explains` — what a shadow-AI is, the offer to dive, Ronin-7 telling Iris to watch his vitals and not pull him out |
| 5 | Trigger: Enter the Playback (dive in) | activates `EnterDiveGo` → `MemoryDiveEntryTrigger.OnEnable` → `dive.EnterDive()`: activates `Playback_Kethel7`, applies `MemoryFlashbackController`'s fog/ambient treatment, and **teleports the rig instantly** to `DiveEntryPoint` (0,0,42) — no lerp, `CharacterController` toggled off/on around the position set, comfort-safe by construction |

**What changes during the beat:** the room's set dressing is untouched — every prop from Beat 0 is exactly where it was. The only physical event is the rig's teleport at step 5, which is also the beat's exit; there is no walked transition between the Hold and the playback.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

**This beat instantiates nothing.** The Cairn Hold, the weapon rack, and the katana are Beat 0's geometry, reused unmodified — exactly the same relationship Chapter 1's Beat 3 medbay kill-box has to its Beat 1 room. A refactored `BuildBeat1Art()` should be an explicit no-op (or a short comment stub) rather than re-instantiating a single Hold prop; if it does, the room has been double-built.

#### d. Combat

None. There is no combat anywhere in this chapter (§9) — the "wake" is a dialogue-and-prompt beat, not a grapple tutorial the way Ch1's Beat 1 was.

#### e. Dialogue / VO

Two dialogue sets, advanced by Left-Hand **Talk** (Y):

- **`ch3_beat1_bonding`**, at (3.5, 1, 6) — 12 lines, ~91.5 s. Iris's "What. You hear something?" opens it; Shadow's first line ("Don't put me down… Hello, Cipher." — Cipher is Ronin-7's own Program codename, confirmed when Khall addresses him by it directly in Beat 2's recorded footage; not a third party being spoken to) is the chapter's first spoken proof of the alone-rule; the exchange runs through Ronin-7 confirming "You're in the blade," Kessler's "He talking to the sword, or is the sword talking to him?", and closes on Shadow's "I had a front-row seat to your execution. You got to sleep through yours. I didn't." — the first crack of grief under the dry warmth.
- **`ch3_beat1_shadow_explains`**, at (2, 1, 7) — 12 lines, ~105 s. "What are you." through the canon tagline ("I'm what they really keep… Kill the operative, keep the shadow"), Ronin-7 asking what it's called and being told "leave it there for now," and closing on the dive setup: Ronin-7's "Watch my vitals… don't try to pull me out," Iris's "Fine. I hate it, but fine," Kessler's "Do it where I can put my hand on you," and Shadow's "Close your eyes. You don't need them in here. In here you'll be looking through his."

**Note on speaker labeling:** every AI line in Beats 1–2 is labeled `Shadow` in `Chapter3Lines.cs`, not `Echo` — the naming only happens in Beat 3. Do not surface "Echo" as a speaker label anywhere before that beat's dialogue set (§9, mirroring Ch1's Khall-naming discipline).

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** The wake's disorientation is carried by the VO performance ("a low TONE rises — not in the room, behind his eyes") and the prompt UI, never by moving the player's view.
- **The wake tone itself is unauthored — this chapter's signature sound has no cue (flagged, the least-supported "no camera shake, feel via audio" beat in the chapter).** The screenplay's central Beat 1 mechanic is non-verbal: a low TONE rises "not in the room, behind his eyes," recedes, then "returns, climbing, shaping itself into words" — and it is the alone-rule's own proof-in-sound, going *dead* the instant the katana is in Iris's hands and *climbing back* the instant it returns to Ronin-7's palm. Folding this into "the VO performance" (above) undersells it: the tone is pre-verbal and recurs across the hand-off, and nothing in the builder authors it — there is no audio cue tied to step 2 (`GripKatanaPrompt` advancing), the Iris hand-off line, or Shadow's first line. Recommended fix: a dedicated comfort-safe rising sub-tone cue, fired on the grip prompt's advance (step 2), silenced across the Iris hand-off line in `ch3_beat1_bonding`, and re-climbing into Shadow's first line ("Don't put me down… Hello, Cipher.") — a low-frequency hum with no directional/camera-relative component, safe by the same "audio and haptics, never motion" rule as everything else in this chapter.
- **Shadow's own spoken voice has no head-locked/2D audio treatment — the chapter's defining "not in the room, behind his eyes" direction is unrealized for the AI's actual voice (flagged; thematically the most central audio omission in this chapter).** The dialogue script directs Shadow "pitched under hearing, heard by Ronin-7 alone" (`Ch03_The_Sword_Remembers_Dialogue_Script.md:161`), and this document itself quotes "not in the room, behind his eyes" at §1.1 and §a — but every Shadow line, in this beat and every later one (both `ch3_beat2_*` set and, as Echo, both Beat 3 sets), plays through the same world-anchored spatial `DialoguePlayer` `AudioSource` as the crew's own lines (e.g. `ch3_beat1_bonding` at (3.5,1,6)) — a fixed point the player can walk away from or turn their head relative to, the opposite of a voice that is definitionally inside the player's own skull. This is a different concern from the alone-rule's occlusion note above (§Beat1b) — that note is about *others* not hearing the AI; this one is about how the AI's voice sounds to Ronin-7 himself. Recommended fix: `spatialBlend = 0` / head-locked routing specifically on Shadow/Echo lines, so the voice sits with the player's head rather than a room point — honestly caveated, per this document's own convention, that the shared `DialoguePlayer` likely cannot vary `spatialBlend` per line within a mixed-speaker set today, so this is a flagged juice opportunity (possibly via a dedicated head-locked AI channel, cf. the dormant `EchoPresence`), not a present behaviour.
- **Ambient bed:** `HoldAmbience` continues unbroken from Beat 0.
- **Lighting:** unchanged from Beat 0 — `HoldLampBench`'s `ConsoleFlicker` and `HoldLampSeat`'s `AmbientPulse` keep running under the whole beat; there is no lighting event tied to the wake itself (a candidate for a future subtle sting if the beat needs more juice, but nothing is authored today — inferred). **`HoldLampRack` also carries the beat's only wayfinding pull toward step 1** (§4 Beat1b) — its steadiness is load-bearing for two reasons at once, not just mood.
- **Comfort:** the reach-gate at step 1 requires actual player locomotion to the rack, engaging the standard vignette on approach; the grip prompt itself requires no movement.
- **Haptics:** none authored. The wake has no `Haptics` call in the builder — a candidate for a controller pulse on grip if this beat needs more physical "click," flagged for consideration, not asserted as present.

---

### Beat 2 — The Playback (Threshold → Aftermath → Khall's Bay → Burn-Down)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (the three room shells, the static-glitch blocks, the ghost figures, the kill-order glyph, the killswitch console, the playback accent lights) and **`BuildBeat2Logic()`** (`DiveEntryPoint`, both `RedactionSentinel`s' waypoints/detection wiring, all three reach points, all four dialogue sets, the exit trigger).
> **The censor-slab visual is the one object that spans both** — `BuildBeat2Art()` instantiates the slab + static-band geometry and returns its handle; `BuildBeat2Logic()` attaches `RedactionSentinel` and wires `waypointA`/`waypointB`/`target`/`OnSpotted`.

#### a. Narrative purpose & emotional target

This is Ronin-7's own erased memory, walked as a witness who can be touched. The design intent is explicit in the beat treatment: no combat, no enemies, no boss — puzzle/stealth traversal through redacted, fracturing archived footage, in service of one reveal: he was the first defector, and the death this footage shows him is the one his own killswitch failed to give. The sub-scenes escalate in a straight emotional line — recognition ("That's my face") → refusal ("No.") → the cost made visible in total silence (the empty, wrecked Aftermath room) → the execution itself, where the chapter's real hinge lands not on Ronin-7 but on Khall: "I know." — agreement, not defense, a trapped man triggering a switch he believes will kill an operative he agrees was right. The burn-down hands down the chapter's charge: not a rescue mission for the other leashed operatives, but a war on the Dominion that built them all.

**Respectful staging, by design.** Per the code's own comment on `Ch3BuildPlayback`: "the caretaker and children are still silhouettes at the threshold; the aftermath room is EMPTY — the massacre is carried by dialogue and vacancy, never shown." The only color break in the entire desaturated island is the red kill-order glyph and, implicitly, the blood the dialogue names but the geometry does not model. Do not add gore geometry to the Aftermath room when authoring prefabs for this beat — its emptiness is the point, not a placeholder for content to come.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to a `[BEAT_2_LOGIC]` root object.

- **`DiveEntryPoint`** at (0, 0, 42) — just inside the corridor mouth, facing +Z (up the recording). This is both the initial teleport target for step 5 and the reset target every time a `RedactionSentinel` spots the player.
- **Reach points**, gating dialogue progression as the player walks deeper into the recording:

  | Reach point | Position | Radius | Gates |
  |---|---|---|---|
  | `ThresholdReachPoint` | (0, 1, 52.5) | 3.5 m | step 6, before `ch3_beat2_threshold` |
  | `AftermathReachPoint` | (0, 1, 61) | 3.5 m | step 8, before `ch3_beat2_aftermath` |
  | `BayReachPoint` | (0, 1, 73) | 3.5 m | step 10, before `ch3_beat2_execution` |

  **This is the canon "light puzzle layer," not a generic distance gate.** The dialogue script names Sub-scene A's mechanic explicitly: "the footage only advances when the player approaches the truth instead of retreating from it" (`Ch03_The_Sword_Remembers_Dialogue_Script.md:299`). A reach-gate — advance only on approach, no timer, no trigger volume that could fire on a retreat or a sidestep — is the faithful mechanical adaptation of that line, and of the beat treatment's "puzzle/stealth traversal" framing (§a). A future pass should not read the three reach points above as placeholder plumbing and layer a separate "puzzle" system on top; the puzzle is the gating.

- **Reveal-timing intent: don't occlude the back-doorway sightline.** The Aftermath beat ("grief in empty space," player at z61–62 per `AftermathReachPoint`) works *because* the 1.6 m `Corridor_WallBack` gap (z56) keeps the caretaker/children tableau glimpsable behind the player as they stand in the empty room — turn back, see through the door, and the room you're standing empty in is the room they died in. But nothing mechanically pulls the player into that ~180° turn-back: the player arrives at `AftermathReachPoint` (0,1,61) walking +Z, with the tableau and glyph 5–7 m behind them at z54–57. The already-flagged `KillOrderGlyph` pulse (§4 Beat2c, §9) is the natural fix — as the island's one saturated color, visible through the z56 door gap, it is the sole diegetic draw that can motivate turning back from the empty room, so it is doing wayfinding double-duty here, not just "juice." A future art/lighting pass on the corridor's back wall or the Aftermath's front wall must not wall off or occlude that gap. Relatedly, `Ghost_Child0`/`Ghost_Child1` (z57/57.4) technically sit just inside the Aftermath Room's own footprint (z56–68), which softly complicates the "deliberately EMPTY" framing (§9) — they read as part of the threshold tableau, beyond the half-shut door, not as Aftermath dressing; keep them conceptually and visually grouped with `Ghost_Caretaker`, not with the empty room they happen to share a floor with.
- **`Redaction_Corridor`** — a `RedactionSentinel` patrolling (-2.4, 0, 49) ↔ (2.4, 0, 49), inside the Threshold Corridor, ahead of the reach point. Target = the rig's head/camera transform (`rigHead`). On sighting (`CanSee`, pure FOV+range dot-product check, default 8 m range / 45° half-angle), fires `OnSpotted` → wired directly to `dive.ResetToEntry()` — teleporting the player back to (0,0,42) with **no game-over, no health loss**. This is the failure state for the "light puzzle layer" the beat treatment describes: being seen resets the witness to the dive entry, it does not fail the mission. `RedactionSentinel` also holds `spottedCooldown = 3f` — three seconds after `OnSpotted` fires, `CheckDetection` is skipped entirely (`Time.time < cooldownUntil`), so a just-reset player gets a guaranteed 3 s grace window standing at `DiveEntryPoint` before the same sentinel can re-catch them. This is not tunable per-instance in this chapter (both sentinels share the field default) and should be confirmed in the manual playtest pass (§8.7).
- **First-arrival spot risk (flagged, distinct from the reset-grace note above): `DiveEntryPoint` sits inside `Redaction_Corridor`'s own detection radius.** `DiveEntryPoint` (0,0,42) is only 7 m from the sentinel's patrol line (z49) — inside the default 8 m `detectionRange`. The `spottedCooldown` grace only exists *after* a reset has already fired once; there is no cooldown active on the very first `EnterDive` teleport at step 5. If the sentinel's patrol phase happens to face −Z at the instant the rig materializes, the player is spotted-and-reset (back to the same spot) on arrival — during the dive-in transition itself, before the player has taken a single step. Recommend confirming the sentinel's patrol phase/facing at the moment of `EnterDive` (or nudging `DiveEntryPoint` to ~z41, or pushing the corridor patrol deeper) so the initial arrival sits outside the cone by construction rather than by patrol-timing luck; call this out explicitly alongside the §8.7 teleport playtest check.
- **`Redaction_BayApproach`** — a second `RedactionSentinel` patrolling (-3, 0, 66) ↔ (3, 0, 66), positioned in the Aftermath Room just short of the Bay threshold (z=68) — "the scrub strongest closest to the killswitch moment," per the dialogue script's stage direction that this stretch is a denser, more whole patrol than the corridor's. Same `spottedCooldown = 3f` applies.
- **Tonal/gameplay intrusion (flagged, not fixed): `Redaction_BayApproach`'s default detection range reaches back into the Aftermath "silence" beat.** At the default `detectionRange = 8f`, a sentinel patrolling z66 detects back to ~z58 — inside `AftermathReachPoint` (z61) and the `ch3_beat2_aftermath` dialogue anchor (z62). Both sentinels are already running (self-driven `Update()` patrol, never paused) from the moment `Playback_Kethel7` activates at step 5 to the moment it deactivates at step 13, so a spotted-reset can fire *during* step 9 — the beat the dialogue script explicitly stages as the playback's first hard gut-punch, delivered in silence, "no fight, no enemies." Being teleported out of the record mid-grief directly contradicts that intent. Recommend gating `Redaction_BayApproach` inactive (or its detection check disabled) until after step 9 completes — it only needs to guard the Bay approach, i.e. steps 10+ — or pushing its patrol line to z67+ so its FOV cone clears the aftermath anchor entirely. This preserves the one beat canon marks as inviolably silent.
- **Note on the dialogue script's `[CUTSCENE]` tags.** Sub-scenes B (Aftermath) and C (Khall's Bay) are marked `[CUTSCENE]` in `Ch03_The_Sword_Remembers_Dialogue_Script.md`, but nothing in this chapter is a non-interactive cutscene — as-built and as-targeted, they are `AftermathReachPoint`/`BayReachPoint` reach-gates feeding ordinary `DialoguePlayer` sets, walked by the player on foot with full locomotion live throughout. There is no Timeline asset, no cutscene controller, and no camera hand-off anywhere in this chapter. Don't go looking for one, and don't build one: the intentional adaptation is "witness who can be touched" (§a) — the player retains full control through every one of these beats.
- **`ExitDiveTrigger`** — an inactive `GameObject` carrying `MemoryDiveExitTrigger` (wired to the same `MemoryDiveController`). Its `OnEnable` calls `dive.ExitDive()` — deactivates `Playback_Kethel7`, teleports the rig to `DiveExitPoint` (0,0,8) (facing `Euler(0,180,0)`, i.e. facing back into the Hold, toward the crew), and restores the exact fog/ambient values captured *before* `EnterDive` first activated the flashback treatment — the "color floods back" beat, authored entirely by `MemoryDiveController`'s pre-dive snapshot, not by the builder re-setting `RenderSettings` by hand.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 6 | ReachTrigger: The Threshold | gates on distance to `ThresholdReachPoint` (0,1,52.5), radius 3.5 |
| 7 | Dialogue | `ch3_beat2_threshold` — the refusal, ending on "No." |
| 8 | ReachTrigger: The Aftermath | gates on distance to `AftermathReachPoint` (0,1,61), radius 3.5 |
| 9 | Dialogue | `ch3_beat2_aftermath` — grief in empty space |
| 10 | ReachTrigger: Khall's Bay (deepest point) | gates on distance to `BayReachPoint` (0,1,73), radius 3.5 |
| 11 | Dialogue | `ch3_beat2_execution` — Khall's "I know," the killswitch |
| 12 | Dialogue | `ch3_beat2_burndown` — the charge |
| 13 | Trigger: Exit the Playback (surface) | activates `ExitDiveGo` → `dive.ExitDive()`: deactivates the island, teleports the rig to `DiveExitPoint` (0,0,8), restores pre-dive fog/ambient |

**What changes during the beat:** the entire playback island goes from inactive to active at step 5 (Beat 1's exit) and back to inactive at step 13 (this beat's exit). Within it, nothing is added or removed by the mission steps themselves — the reach/dialogue steps are pure gates on player position and VO, and the only "hostile" state change is whichever `RedactionSentinel` fires `OnSpotted`, which is a *reset*, not a build-time mutation.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

All environment prefabs instantiate from the `ArtAssetRegistry` and parent under the (inactive) `Playback_Kethel7` root, itself under `[STATIC_ART_DO_NOT_DELETE]`.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Threshold Corridor shell (6×16, solid front, 1.6 m gap back) | center (0,0,48) | `Rooms.PlaybackCorridorShell` | `…/Art/Generated/Rooms/PlaybackCorridorShell.prefab` | **MISSING** |
| `Props.HalfShutDoor` (partially occludes the 1.6 m `Corridor_WallBack` aperture) | (0, RoomH/2, 56), partially closed across the gap | `Props.HalfShutDoor` | `…/Art/Generated/Props/HalfShutDoor.prefab` | **MISSING** |
| `StaticBlock0`/`1`/`2` (misaligned glitch frames) | (-2.6,1.6,44) / (2.6,0.8,47) / (-2.5,2.2,51) | `Vfx.StaticGlitchBlock` | `…/Art/Generated/VFX/StaticGlitchBlock.prefab` | **MISSING** |
| `Ghost_FootageRonin_Threshold` (the frozen operative, katana raised — "That's my face") | (0, 0, 51.5), rot Euler(0,135,0) — a ~3/4 turn: profile/face reads toward the entering player on approach, while the raised blade still angles toward the caretaker tableau at z54 (supersedes a pure +Z back-facing pose — see facing note below) | `Vfx.GhostOperative` | `…/Art/Generated/VFX/GhostOperative.prefab` | **MISSING** |
| `Ghost_Caretaker` (hands open, no threat) | (0, 0, 54), rot Euler(0,180,0) — faces **−Z**, toward the corridor mouth and the incoming player | `Vfx.GhostFigure` | `…/Art/Generated/VFX/GhostFigure.prefab` | **MISSING** |
| `Ghost_Child0` / `Ghost_Child1` | (-0.7,0,57) / (0.6,0,57.4) — small shapes beyond the half-shut door; no facing requirement, they read as background silhouettes rather than a staged confrontation | `Vfx.GhostFigure` | `…/Art/Generated/VFX/GhostFigure.prefab` | **MISSING** |
| `KillOrderGlyph` (the one color the footage keeps) | (0, 2.1, 53.4) | `Vfx.KillOrderGlyph` | `…/Art/Generated/VFX/KillOrderGlyph.prefab` | **MISSING** |
| Aftermath Room shell (10×12, deliberately EMPTY of props) | center (0,0,62) | `Rooms.PlaybackAftermathShell` | `…/Art/Generated/Rooms/PlaybackAftermathShell.prefab` | **MISSING** |
| Khall's Bay shell (8×12, solid back wall) | center (0,0,74) | `Rooms.PlaybackBayShell` | `…/Art/Generated/Rooms/PlaybackBayShell.prefab` | **MISSING** |
| `Bay_KillswitchConsole` | (0, 0.55, 74.5) | `Props.KillswitchConsole` | `…/Art/Generated/Props/KillswitchConsole.prefab` | **MISSING** |
| `Ghost_FootageRonin` (mission-dirt, **fallen/collapsed pose** — not standing; the execution's death-beat, not a static bystander — katana lowered/dropped at rest) | (0, 0, 72.8), rot identity — collapsed toward the console at z 74.5 (facing no longer meaningful once down; retain the +Z orientation as the fallen figure's "downward" axis) | `Vfx.GhostOperative` | `…/Art/Generated/VFX/GhostOperative.prefab` | **MISSING** |
| `Ghost_Khall` (post-trigger pose: averted gaze, weight on the hand planted on the killswitch console — "he cannot look at it") | (0, 0, 76.2), rot Euler(0,180,0) — faces **−Z**, toward the footage-Ronin and the console, per the screenplay's "Khall and the footage-Ronin face each other across the console" | `Named.Khall` *(ghost-material override)* | `…/Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `RedactionSentinel` censor-slab + static-band (×2) | see logic table | `Vfx.RedactionSentinel` | `…/Art/Generated/VFX/RedactionSentinel.prefab` | **MISSING** |
| `PlaybackLight_Corridor` / `_Aftermath` / `_Bay` | (0,2.6,48) / (0,2.6,62) / (0,2.6,74) | — | cheap point lights, parented under the dive root, not profile-driven (§3.1) | authored per-light |

**Notes on the transition.** `Ghost_Khall` is the one row that is *already* an existing named-cast prefab rather than a commission — the builder instantiates the real `Khall.prefab`, runs it through the standard `FitNamedCharacter` grounding pass, then swaps every renderer's material to the shared ghost material (`MemoryFlashbackController.MakeGhostMaterial()` — a transparent URP Unlit, pale blue-white, ~0.35 alpha). This is a distinct mechanism from Ch1/Ch4's "hologram" Khall: here he is memory-cast, not a projected hologram, and the material swap is the entire visual treatment — no separate hologram VFX or `HologramOn.wav`-style cue is authored for this appearance. `Ghost_FootageRonin` is Ronin-7's own past self and is built as a generic humanoid ghost silhouette (capsule torso + sphere head, same primitive shape as the caretaker/children), not the player's own rig or any named prefab — he is a recording, never controllable. **Recorded-Khall is comms-only VO at the Threshold, by design — no figure is built there.** `ch3_beat2_threshold`'s four Khall lines ("Cipher. The order is live," etc.) play over comms, slightly thinned, with no corresponding ghost in the corridor; his sole embodied appearance in this chapter is this Bay ghost. A future pass adding a corridor Khall figure "to match" the Bay would be introducing a figure canon never stages there. The kill-order glyph is the playback's single saturated color against total desaturation and must stay that way through any art pass — a "red glyph" registry key whose material should not inherit the ghost-figure treatment.

**The footage-Ronin ghosts must not share the civilian silhouette (registry split, not just a coordinate fix).** As-built, both `Ghost_FootageRonin_Threshold` and `Ghost_FootageRonin` would resolve to the identical generic capsule+sphere silhouette as `Ghost_Caretaker`/`Ghost_Child0`/`Ghost_Child1` if built from the same key — but the screenplay's whole recognition beat is "a single operative: same build, same augment-seams as Ronin-7," and Shadow's "It's the first time I ever saw one of you stop" only lands if the figure reads as *him*. A generic child-shaped silhouette standing in for "that's my face" points the line at a shape with no relationship to the player's own body. The two rows above are split onto a distinct `Vfx.GhostOperative` key (vs. the civilians' `Vfx.GhostFigure`) for exactly this reason — the commission direction for that prefab should carry Ronin-7's own silhouette proportions and augment-seam detailing, not a copy of the generic civilian shape. See Appendix B for the resulting two-key split (`Vfx.GhostFigure` ×3 civilian instances, `Vfx.GhostOperative` ×2 operative instances) in place of the single five-instance key.

**The `Vfx.GhostOperative` commission must also include a ghost-tinted katana in the figure's hands — the same blade, not a generic weapon (new, high-value).** The screenplay stages the entire threshold refusal around a visible blade — "The footage-Ronin raises the blade and freezes there, hand not moving," then "The blade lowers. The refusal is small and total" — and Shadow's "It's where I'm kept… I rode every second of it" means the katana in footage-Ronin's hands *is* the same physical sword the player is holding as Echo. As-built (and as currently spec'd above before this pass), `Ch3BuildGhostFigure` is body+head only, with no weapon — the same gap family as the missing pot/slate/half-shut-door, but thematically the strongest of them: the player holding Echo, watching footage-Ronin wield Echo, is the chapter's central image made literal. The threshold instance holds the blade raised (mid-refusal); the Bay instance holds it dropped/lowered beside the fallen body (per the two rows above — the Bay pose is now collapsed, not standing at rest; see the fallen-pose note below). Do not commission a weaponless silhouette for this key.

**The Bay `Ghost_FootageRonin` must resolve to a fallen/collapsed pose, not a standing figure — the execution's death-beat has no body to land on otherwise (new, high-value).** As-spec'd above (and as-built), the Bay instance is a single standing ghost — but `ch3_beat2_execution` is built entirely around the fall: the stage direction has him "go down between one breath and the next," the line "That's me. That's where I was supposed to stop." names the floor, not a standing figure, and Shadow's own redirection depends on there being a body to *not* look at ("The body on that floor and the man standing next to me are the same man," `Ch03_The_Sword_Remembers_Dialogue_Script.md:441`). A figure left standing through the killswitch line contradicts the words the player is hearing in real time — the death is the beat, not an implication layered over an unchanged pose. This is distinct from (and does not relax) the Aftermath's deliberately-EMPTY rule (§9/§a) — the Aftermath's dead are carried by vacancy, but the Bay execution is the one place canon explicitly wants a watched body on the floor. Fix: commission (or pose) `Ghost_FootageRonin` collapsed at (0,0,72.8) — fallen toward the console, blade dropped beside him — rather than standing; if a future juice pass can animate it, a two-state pre-trigger-standing/post-trigger-collapsed reveal keyed to the killswitch fire is the ideal target, but a static fallen pose is the correct baseline today. **Bonus: this also softens the already-flagged sightline problem below** — `BayReachPoint`'s 0.2 m proximity to `Ghost_FootageRonin` (§ below) currently puts the player inside a standing torso during the execution; a fallen body at the same coordinate is something the player steps *near* or *over*, not through, which reads as witnessing rather than clipping.

**`Ghost_Khall`'s commissioned pose should read as "cannot look at it," not a neutral face-off (new).** The Euler(0,180,0) fix above corrects Khall's *yaw* — squared up across the console toward the footage-Ronin — but the beat's real hinge is Khall, not Ronin-7 (§a): the set closes on Shadow's "Watch him. Not the floor. Watch what it costs the man who pulls it," and the screenplay's payoff image is specific — after the trigger, "Khall does not watch it. He cannot look at it. His hand stays on the console like it is the only thing holding him up" (`Ch03_The_Sword_Remembers_Dialogue_Script.md:450`; beats `Ch03_The_Sword_Remembers.md:126,133`). A frozen ghost simply squared up to Ronin-7 gives that redirection nothing to point at. Fix: extend the `Ghost_Khall` commission with a pose direction on top of the yaw — gaze averted (down or aside, not locked on the footage-Ronin), weight visibly planted on one hand against `Bay_KillswitchConsole` — so "watch what it costs the man who pulls it" reads visually, not only in VO. Low-risk since it is a static-pose commission note on the one embodied Khall appearance in this chapter, not a new mechanic.

**The glyph is static as-built, despite canon language calling it a pulse.** Both the dialogue script ("a red kill-glyph pulses in the HUD overlay") and the builder's own inline comment ("the red kill-order glyph, pulsed still over the scene") describe motion, but `KillOrderGlyph` (`Chapter3Builder.cs:376-382`) is a plain unlit primitive with no animation component wired to it — it does not pulse today. Treat "pulsed" in the source comment as aspirational, not a description of current behaviour; §9 lists the fix as a comfort-safe additive.

**Optional juice idea, with a canon tension flagged honestly (not a recommendation): a faint red spill from the glyph onto the caretaker tableau.** The glyph is "the one color the footage keeps," pulsing over `Ghost_Caretaker` — thematically it marks him for death. A faint low-range red point light co-located with the glyph would spill that single color onto the caretaker/children, visually tying the kill-order to its targets. The tension: canon states color is drained "except for two things — the red glyph, and, later, blood" (§3) — any red *spill* beyond the glyph's own geometry is a mild stretch of that strict two-things rule. Present this as an optional idea for a future juice pass with the tension noted up front, not as a recommendation, to stay consistent with the chapter's otherwise strict desaturation discipline.

**The glyph is also a diegesis adaptation, separate from the pulse gap: canon calls for a HUD overlay, as-built is a fixed worldspace object.** The dialogue script's language is specifically a HUD element ("pulses in the HUD overlay"); as-built and as-targeted, `KillOrderGlyph` is a fixed worldspace primitive at (0, 2.1, 53.4), not a screen-space UI element — this chapter (and this game) has no diegetic-HUD system for it to live on. This is a deliberate adaptation, not an oversight, and is consistent with the chapter's general avoidance of screen-locked UI elsewhere (the grip/dive prompts are worldspace `TextMesh`s, not canvas overlays); it is recorded here so a future pass doesn't "fix" the glyph into a screen-space HUD element that would fight the chapter's worldspace-only UI convention.

**Glitch-block dressing is corridor-only; the deeper rooms rely on ghost-material and fog alone (recommendation, not a bug).** All three `StaticBlock`s (Appendix A.3) sit in the Threshold Corridor (z44–51); the Aftermath Room and Khall's Bay carry zero `Vfx.StaticGlitchBlock` dressing — only the ghost figures' material and the room fog sell "recording" in the chapter's two emotional-climax rooms. When art lands, distribute glitch-block instances into both deeper rooms, canon-weighted rather than evenly: the beat treatment's own steadiness gradient (already noted above for `Ghost_Khall`) plus the script's "the footage steadier here" for the Bay argue for sparse, subtle glitch dressing there; put the denser, rawer glitch texture in the Aftermath Room instead, where the "failing footage" signature should be strongest. This is the natural companion to the burn-down edge-dissolve VFX candidate already flagged in §5.

**Companion recommendation: give the Aftermath's canon "smoke" a distinct, localized treatment rather than folding it into the shared island fog (§3).** Recommend a subtle *localized* drifting haze/smoke element authored in the Aftermath Room only — slow drift, no fast particles, no camera-relative motion, comfort-safe by the same rule as everything else in this chapter — distinct from the uniform `MemoryFlashbackController` fog every room shares. This lets the "grief in empty space" room read as the *after* of something that burned, not just another patch of the same drained haze, and pairs with the denser-glitch recommendation immediately above: both target the Aftermath Room as the place to spend the extra atmosphere budget.

**Facing bug in the current primitive (fix in the prefab, don't preserve it).** `InstantiateNpc` never sets a rotation, so as-built `Ghost_Khall` sits at `Quaternion.identity` — facing **+Z**, i.e. toward the back wall (z80), away from Ronin-7, not across the console at him. The screenplay is explicit the two face each other; the target-state rotation above (`Euler(0,180,0)`) corrects this. Likewise `Ch3BuildGhostFigure` never sets a rotation for any ghost silhouette, so the as-built `Ghost_Caretaker` also defaults to identity (+Z, facing away from the corridor mouth the player enters through) rather than the −Z the reveal image calls for (the unarmed man facing the approaching witness, children behind him). Both are real facing errors in the current primitive path, not merely undocumented gaps — a prefab commission that just copies the as-built transform will reproduce the same blocking mistake.

**The "half-shut door" is a repeatedly-named canon image with no prop — the same gap family as the missing pot/slate (§4 Beat0c).** The dialogue script's SETTING block, Sub-scene A, and the beat treatment all name "a half-shut door" with children behind it — it is one of the chapter's signature images (§2's doors table). As-built, `Corridor_WallBack` is a plain `BuildDoorwayWall` open 1.6 m aperture; there is no door leaf anywhere in the hierarchy, so nothing is actually "half-shut" — the gap simply reads as an open doorway. The `Props.HalfShutDoor` row above is the fix: a leaf partially occluding the aperture, both honoring the canon image and strengthening the reveal-timing note above (the children should read as *glimpsed behind* a door, not standing in a fully open gap).

**`Ghost_FootageRonin_Threshold` is a missing figure, not a facing bug — the largest content gap in this chapter.** `ch3_beat2_threshold` contains Ronin-7's "That's my face" and Shadow's "It's the first time I ever saw one of you stop," and the screenplay stages "a single operative: same build, same augment-seams… The footage-Ronin raises the blade and freezes there, hand not moving" directly ahead of the player in the corridor. But `Ch3BuildPlayback` (`Chapter3Builder.cs:372-376`) instantiates only `Ghost_Caretaker` and the two children there; the sole `Ghost_FootageRonin` build call in the whole chapter is in the Bay, at z72.8 (`Chapter3Builder.cs:398`). As-built, "That's my face" is spoken at an empty spot in the corridor. This is an omission in the as-built primitive path, not a rotation to correct — a prefab pass that faithfully copies the current transforms will still leave the line pointing at nothing. The row above (`Ghost_FootageRonin_Threshold`) is the fix: a fourth threshold ghost, distinct from the Bay's `Ghost_FootageRonin`.

**The Threshold operative's facing should not be a pure +Z back-turn — the player needs to read the face during "That's my face" (facing note, new).** The player teleports in at `DiveEntryPoint` (z42) facing +Z and walks to `ThresholdReachPoint` (z52.5) as `ch3_beat2_threshold` fires the recognition line; a pure +Z-facing pose — justified solely by "raises the blade toward the caretaker" — puts the back of the operative's head toward the approaching player for the entire line, and you cannot register "my face" from behind. The row above corrects this to a ~3/4 turn (`Euler(0,135,0)`) rather than a full about-face: the raised blade still angles toward the caretaker tableau, honoring the refusal's threat-gesture, while the profile/face reads on approach, honoring the recognition line. Note this explicitly so a future prefab pass doesn't lock in a pure back-facing pose. **This resolves the yaw, not the gesture — the raised-blade pose is a deliberate pre-refusal capture, not the instant the beat actually resolves on.** The screenplay's threshold is a micro-sequence — raises the blade and freezes, Khall's order, then "the blade lowers. The refusal is small and total" — and `ch3_beat2_threshold`'s final line is "No.," i.e. the beat lands on the *lowering*. A blade raised at an unarmed caretaker with children behind him risks reading as threat during the very line ("That's my face") that is meant to land as recognition, not menace. This pose is kept because a mid-motion freeze is the more legible "this is a paused recording" read than a passive lowered-at-rest figure would be — but if a future playtest reads it as staging a threat rather than a refusal, retarget this ghost to the lowered-blade instant instead (the same at-rest handling already used for the Bay's `Ghost_FootageRonin`).

**`Ghost_FootageRonin_Threshold`'s blocking has the milder version of the Bay issue below — deliberately staged for the approach, not the arrival.** The proposed `Ghost_FootageRonin_Threshold` at z51.5 (§4 Beat2c) sits just *behind* `ThresholdReachPoint` (z52.5) — a player who walks all the way to the reach anchor ends up past their own frozen face during "That's my face." The 3.5 m reach radius means the line in practice fires on approach, with the figure still ahead of the player, so this mostly works — but as with the Bay note below, it is worth flagging explicitly that z51.5 is staged for the *approach* read, not the arrival read, so a future spacing pass doesn't "fix" the ordering and break the read that currently works by radius alone.

**Sightline/blocking gap: the Bay reach point puts the player inside his own execution-self.** `BayReachPoint` (§4 Beat2b) is (0,1,73), only 0.2 m from `Ghost_FootageRonin` at (0,0,72.8) — with colliders stripped by design, the player is reach-gated to within, and can walk straight through, his own frozen body during `ch3_beat2_execution`. Front-to-back the geometry runs `Ghost_FootageRonin` (72.8) → player reach point (73) → `Bay_KillswitchConsole` (74.5) → `Ghost_Khall` (76.2), which inverts the screenplay's "Khall and the footage-Ronin face each other across the console" framing from the witness's eye — Ronin-7 should be looking *past* his own past self at Khall, not standing inside him. If the intended blocking is witness-over-the-shoulder, nudge `BayReachPoint` back to roughly z71.3 (or move `Ghost_FootageRonin` forward toward the console) before authoring a prefab pass that would lock the current spacing in; confirm the intended blocking first. **This interacts with the fallen-pose fix above:** once `Ghost_FootageRonin` resolves to a collapsed body rather than a standing torso, the same 0.2 m proximity reads as the player standing close over his own fallen self rather than clipping through it — the fallen pose does not replace the spacing fix (the inverted over-the-shoulder framing relative to `Ghost_Khall` still holds and should still be considered), but it meaningfully softens the worst part of the problem on its own.

#### d. Combat

**None.** Per the beat treatment's explicit design intent: "no combat in the playback… the redactions are non-combat obstacles… not enemies to fight; no roster boss." The `RedactionSentinel`s are stealth-detection hazards, not `Enemy`/`Health`-driven combatants — there is no `BladeDamager`, no weapon-shouldered trooper, no `DefeatEnemies` mission step anywhere in this chapter. The Aftermath room's "dismembered dead" and the fight that produced them are narrative color carried entirely by dialogue and staged vacancy (§a) — no combat geometry, gore decals, or fight choreography should be added here in a future pass; that would contradict the beat's own design note.

#### e. Dialogue / VO

Four dialogue sets, all advanced by Left-Hand **Talk** (Y):

- **`ch3_beat2_threshold`**, position (0, 1, 53), 8 lines, ~40 s: Shadow's "That's you… I rode every second of it. Walk it with me," Ronin-7's "That's my face," and the recorded exchange — Khall's "Cipher. The order is live. Complete the sweep," Ronin-7's refusal, and Khall's flat "The order does not have a door in it… That's twice I've said it" — closing on the recorded Ronin-7's single word, "No."
- **`ch3_beat2_aftermath`**, position (0, 1, 62), 6 lines, ~49.5 s: Shadow's "They tell you the feeling is malfunction… Look at what the static was trying to stop," Ronin-7's "My no came too late for this room," and Shadow's answer, thinned per the audit's craft note: "It always comes a beat too late. You carried this every second after. So did I. The difference is they let you forget."
- **`ch3_beat2_execution`**, position (0, 1, 74), 12 lines, ~63.5 s — the chapter's longest and heaviest set: Khall's "One word into your comm and the sweep stops clean… Why did you refuse the order?"; Ronin-7's escalating refusal ("Because it's not right" through "And you knew. You've always known."); Khall's "I know." (two words, agreement not defense); Ronin-7's present-tense recognition ("That's the switch. The one they put in me." / "That's me. That's where I was supposed to stop."); Shadow's closing redirection, "Watch him. Not the floor. Watch what it costs the man who pulls it."
- **`ch3_beat2_burndown`**, position (0, 1, 75), 4 lines, ~36 s — no new geometry, VO only over the breaking footage: Shadow's creed ("They tell you the feeling is malfunction. It's the only part of you that was ever telling the truth…"), Ronin-7's "They put that switch in me for it," and the charge: "You were the first. You won't be the only one. But the others aren't the target. They're just the hands. The Dominion's the thing holding the knife."

**Speaker label note:** every recorded/past-tense line in this beat (Khall's, and the footage-Ronin's) is authored under the plain speaker names `Khall` and `Ronin-7` in `Chapter3Lines.cs` — there is no separate "(recorded)" tag baked into the data the way the dialogue script's prose parentheticals suggest; if a UI/subtitle pass wants to visually distinguish recorded dialogue from present-tense narration, that distinction must be added at the UI layer, not assumed to already exist in the line data *(inferred)*.

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point**, including the killswitch trigger and the burn-down's fracturing footage — those beats are carried by `MemoryFlashbackController`'s fog/ambient treatment, VO performance, and (for the ghost figures) the "goes down between one breath and the next" staging, never by moving the player's view.
- **`PlaybackDreadAmbience`** (`BuildAmbienceLayer("PlaybackDreadAmbience", (0,1.5,62), inner 3, outer 12, maxVolume 0.4)`) runs the whole time the dive is active, positioned at the Aftermath Room's center — the geographic and emotional middle of the island.
- **Coverage gap: a single source at (0,1.5,62) cannot cover the 40 m island, and it is silent at the two moments that matter most (flagged, not fixed).** Outer radius 12 from z62 reaches only ~z50–74. But the rig teleports in at `DiveEntryPoint` z42 — 20 m outside the falloff, effectively silent — and the entire Khall's Bay execution climax (`ch3_beat2_execution`, `ch3_beat2_burndown`, the killswitch trigger) plays at z74–76, at or beyond the outer edge. The "held breath behind glass" bed — this chapter's defining sonic signature for the playback — is inaudible exactly at the dive-in arrival and through Khall's "I know" and the killswitch. §7's "runs the whole time the dive is active" framing describes the source's active-time, not its actual audible footprint, and hides this gap. Recommended fix, mirroring the per-room point lights (§3.1/§4 Beat2c): author one dread bed per room (`PlaybackDreadAmbience_Corridor`/`_Aftermath`/`_Bay`) rather than one shared source, so the padded close air is present at both the threshold and the execution — or, as a one-line interim fix, widen `outer` to ~20+ so the existing single source's falloff spans the whole island. **A cheaper fix than either already exists on the frozen component itself — see the `heartbeatLoop` note below.**
- **`MemoryFlashbackController`**'s default treatment (not customized per-field in this builder, so its own component defaults apply): fog `ExponentialSquared`, color (0.35, 0.37, 0.42), density 0.045; ambient `Flat`, color (0.30, 0.30, 0.34); no `heartbeatLoop` clip wired. This is markedly heavier fog than the Hold's 0.018 density — the playback should read as visually "closer" and more oppressive than the real ship, consistent with "sound is close and padded, like a held breath behind glass."
- **The unwired `heartbeatLoop` above is the coverage gap's real fix, not just a bare fact about an unset field.** `MemoryFlashbackController` (frozen, on `diveRootGo`) already has a serialized `heartbeatLoop` clip + `heartbeatVolume` (0.35) that, once a clip is assigned, plays on `Awake()` at `spatialBlend = 0` (`MemoryFlashbackController.cs:15-16, 24-36`) — a **2D, zero-falloff loop audible island-wide** for the entire time `Playback_Kethel7` is active, immune by construction to the point-source falloff the coverage-gap bullet above describes. It rides the dive-activation lifecycle for free: no extra `AudioSource` to author, no per-room bed to place. This is the cheapest, most literal realization of the screenplay's defining playback signature — "sound is close and padded, like a held breath behind glass" — a slow, sub-audible heartbeat/held-breath loop is exactly that. **Recommend wiring a clip here as the primary fix for the coverage gap above**, with the per-room `PlaybackDreadAmbience` split or the `outer`-widen as a secondary/companion measure rather than the primary one (§9).
- **Recorded/comms voices get no filtered/thinned audio treatment and are not emitted from their ghost positions — the doc's own §4 Beat2c note quotes the direction without flagging it as unbuilt.** §4 Beat2c cites Khall's threshold lines as playing "over comms, slightly thinned" to justify building no corridor figure there — but as-built, every recorded line (Khall's threshold comms, and, in `ch3_beat2_execution`, the recorded Khall at ghost z76.2 and the recorded footage-Ronin) plays through the same unprocessed `DialoguePlayer` `AudioSource` at the single beat anchor (0,1,74) as present-tense Shadow and present-tense Ronin-7 — identical processing, identical (non-ghost) emission point, so the player cannot hear the difference between the memory and the man standing inside it. Recommended fix, the audio twin of the "(recorded)" subtitle distinction already flagged at the UI layer (§Beat2e): a filtered/comms-EQ pass on the recorded-line entries, ideally emitted from their corresponding ghost positions (Khall's Bay lines from `Ghost_Khall` at z76.2, footage-Ronin's from `Ghost_FootageRonin`/`_Threshold` at z72.8/z51.5) rather than the shared beat anchor, distinct from present-tense Shadow/Ronin-7. Flagged as a future pass, not built today.
- **The killswitch-fire and the footage-Ronin's collapse — the chapter's single most emotionally loaded moment (§1.1) — have no dedicated cue (flagged, second-highest-value audio/haptic addition in this chapter, after the wake tone).** `ch3_beat2_execution`'s "That's the switch. The one they put in me." and the body going down are currently folded entirely into "VO performance" and the "goes down between one breath and the next" staging (above) — no audio sting and no haptic pulse is tied to the trigger firing itself; §9's haptic-cue map, too, only places a pulse under Khall's earlier "I know." line, before the switch actually fires. Recommended fix, comfort-safe by the same rule as everything else in this chapter: a single low, close audio "cut" with no directional/camera-relative component on the switch-fire, paired with a short haptic drop (a brief zero, not a buzz) timed to the footage-Ronin falling. See §9 for the companion haptic-map entry.
- **`RedactionSentinel` detection is comfort-safe by construction:** on a sighting, the response is an instant `SetPositionAndRotation` teleport back to `DiveEntryPoint`, the same no-lerp mechanism `MemoryDiveController.EnterDive`/`ExitDive` use — never a forced camera pan or a punish-shake. **This comfort-safety does not make the reset tonally safe everywhere it can fire** — see §4 Beat2b's flag that `Redaction_BayApproach`'s default 8 m range reaches back into the Aftermath dialogue zone, risking a mid-grief reset during the chapter's one explicitly silent, no-enemies beat.
- **Haptics:** none authored for this beat. There is no combat to carry haptic feedback, and no haptic cue is wired to a `RedactionSentinel` sighting, or to the killswitch-fire itself (see the bullet above) — both are candidates for a pulse, flagged for consideration, not present today.
- **90 FPS:** the playback is comparatively cheap (three small rooms, two patrolling primitives, a handful of static ghost figures, no particle-heavy VFX) — the far more likely frame-budget risk in this chapter is a high-fidelity room-shell prefab landing with baked lighting/shadow-casting geometry across three simultaneously-active rooms once a player is deep in the island.

---

### Beat 3 — Carrying the Witness (The Naming)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. **This beat builds no new geometry** — create **`BuildBeat3Logic()`** only, for the naming/debrief dialogue, the `ch3_echo_named` flag, and the chapter outro.
> **The Hold is Beat 0's room, reused.** `BuildBeat3Art()` must not re-instantiate a single Hold prop.

#### a. Narrative purpose & emotional target

The surfacing. Ronin-7 comes up off the playback seat hard, breathing, gripping the wrap — the warm secondhand light of the real ship "almost violent" after the drained grey of the recording. Iris and Kessler have spent four minutes watching a man's eyes move behind closed lids, reading something they can't see; their fear is real and their questions come out sideways as precision. (Both spawns are fixed ~6 m from the playback seat; "half out of her chair" and the "heavy hand on his shoulder" read through VO and stage direction, not literal proximity — see §5's crew-proximity note.) The beat's real work is the naming: Ronin-7 asks the AI what it's called, and for the first time the thing that has always been assigned things (a mission, a codename, a switch) is *given* something instead — "You're not just a shadow. You're the one piece of me they couldn't scrub. An echo of who I was." The AI's reply, caught off guard and covering with a joke ("...Echo. Yeah. I can wear that one. First thing anybody's handed me instead of stamping on me. Don't make it weird."), is the chapter's warmest beat and its structural hinge: **from this line forward, the speaker label in every subsequent chapter's dialogue is `Echo`, never `Shadow` again.** The debrief that follows lands the chapter's charge with the whole crew present — not a rescue, a war on the Dominion — and closes on Kessler sending everyone to bed and Echo's quiet, private "I've got your eyes when you open them. I always do."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to a `[BEAT_3_LOGIC]` root object.

- **Dialogue anchors:** `Dialogue_Beat3_Naming` at (0, 1, 8); `Dialogue_Beat3_Debrief` at (0, 1, 5).
- **`EchoNamedFlag`** — an inactive `GameObject` carrying `CampaignFlagSetter` (`flags = ["ch3_echo_named"]`, `setOnEnable = true`). Activated by step 15; its `OnEnable` sets the flag directly (the `setOnEnable` opt-in path, distinct from Ch1's `ChapterOutro.OnActivated`-driven pattern). **No ability unlocks on this flag this chapter** — `EchoPresence`'s pools gate on the later `ch3_complete` flag, not on the naming moment itself; the naming beat is pure story-flag bookkeeping. **This is not a dropped canonical objective — it is deferred one flag over.** The story bible's GAME NARRATIVE DESIGN section lists Objective 3 as "Recover Ronin-7's own full record of the Kethel-7 mission → unlock the katana's 'Witness' abilities," and that "the katana gains the shadow-AI mechanic" (`Ch03_The_Sword_Remembers.md:178, 182`). That objective is realized here, just not as an in-chapter pickup: `EchoPresence` (§2) *is* Echo's ongoing "Witness" voice — ambient combat-commentary, silent until `ch3_complete` (`EchoPresence.cs:8-10`) — and `ch3_complete` is exactly the flag `ChapterOutro` sets at step 17, not `ch3_echo_named`. The naming beat is bookkeeping; the chapter-complete flag is the "Witness ability" unlock, realized as a saga-wide flag rather than a weapon upgrade.
- **`ChapterOutro`** at (0, 1, 8), inactive. `CampaignFlagSetter` (`flags = ["ch3_complete"]`) wired to `OnActivated`; `completeCanvas` ref = the "CHAPTER 3 COMPLETE" world-space canvas at (0, 1.4, 9); `fadeDelay`/`fadeDuration` are left at `ChapterOutro`'s own component defaults (1.5 s / 2 s — not re-specified in this builder, unlike Ch1's explicit values, though they resolve to the same numbers); `publishZoneCompleted` defaults to `true`.

**Mission-spine steps:**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 14 | Dialogue | "Beat3: The Naming (Shadow becomes Echo)" | `ch3_beat3_naming` |
| 15 | Trigger | "Trigger: Set ch3_echo_named" | activates `EchoNamedFlag` |
| 16 | Dialogue | "Beat3: The Debrief (a target, not an answer)" | `ch3_beat3_debrief` |
| 17 | Trigger | "Trigger: Chapter Outro (flag + fade + canvas)" | activates `ChapterOutro` |

Step 17 both ends Beat 3 and ends Chapter 3: `ChapterOutro.OnEnable` invokes its wired `onActivated` (a persistent-listener call into `CampaignFlagSetter.SetFlags`, setting `ch3_complete`), reveals the complete canvas, waits `fadeDelay`, fades to black over `fadeDuration` via `ScreenFader`, and finally publishes `ZoneCompleted` — the same signal `GameFlowManager`'s mission-complete handling listens for elsewhere in the game.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

**This beat instantiates nothing new.** The Cairn Hold, the playback seat, and the weapon rack are Beat 0's geometry. The only "new" object visible this beat — the "CHAPTER 3 COMPLETE" canvas — is inactive until step 17 and is documented here for completeness rather than as new set dressing:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| "CHAPTER 3 COMPLETE" world-space canvas | (0, 1.4, 9), facing -Z (rot Euler(0,180,0)) | — | UI canvas, built by `Ch3BuildCompleteCanvas` (no registry entry — UI, not environment art) | authored inline |

#### d. Combat

None.

#### e. Dialogue / VO

Two dialogue sets, both advanced by Left-Hand **Talk** (Y):

- **`ch3_beat3_naming`**, position (0, 1, 8) — 7 lines, ~59 s: Kessler's "Easy. Easy. You're on the ship. You came back," Iris's "You were gone four minutes and your eyes were moving the whole time," Ronin-7's question to the blade ("What's your name?"), Shadow's admission ("I don't have one… It was never a name. Just the word they stamped on the leash."), Ronin-7's naming line, and Echo's first line under its new name.
- **`ch3_beat3_debrief`**, position (0, 1, 5) — 14 lines, ~115.5 s: Ronin-7 lays out the AI to the crew ("There's an AI in the blade. I'm calling it Echo…"), Kessler and Iris react ("An AI. In the sword I cleaned the blood off…" / "So it's a leash."), Echo's private aside to Kessler ("I didn't pick him. I'm wired to him. But for what it's worth, old man, I'd have picked him." — heard by Ronin-7 alone, the others get only his pause; mechanically this is the same unmediated `DialoguePlayer` line every NPC could hear if they were listening, not audio occlusion — see the alone-rule note in §Beat1b), the tie-back to Chapter 1 ("Back on the rig I said I wanted to know why I went easy…"), Iris naming the stakes ("We lit something at Velorum. It's still burning, and it's pointed at us."), Ronin-7's closing resolve ("Then we move before they do…"), Kessler sending everyone to bed, and Echo's closing private line.

**Naming discipline:** every line in `ch3_beat3_naming` up to and including Ronin-7's "Echo. I'm calling you Echo from now on." is still labeled `Shadow`; every AI line from that point on — in this set, in `ch3_beat3_debrief`, and in every later chapter — is labeled `Echo`. Do not relabel the pre-naming lines to `Echo` retroactively; the speaker-label change *is* the naming beat playing out in the data (§9).

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the surfacing, the naming, and the debrief are dialogue/VO beats carried by performance and lighting, never camera motion.
- **Lighting:** the Hold's three accent lights continue exactly as authored in Beat 0 — no lighting event is tied to the naming itself; the "almost violent" warmth-after-grey contrast the screenplay describes is achieved entirely by the cut from the playback's heavy `ExponentialSquared` fog back to the Hold's light `Exponential` baseline via `MemoryDiveController.ExitDive`'s restore, not by any new light in this beat.
- **Ambient bed:** `HoldAmbience` resumes/continues (it was never stopped — it runs under the Hold at all times; the playback's own `PlaybackDreadAmbience` was the only bed active during Beat 2).
- **Comfort:** standard vignette; no reach gates in this beat, dialogue advances entirely on Talk (Y).
- **Haptics:** none authored — consistent with the rest of the chapter's complete absence of combat feedback. **A gentle settle/exhale pulse on `ExitDive` (a soft return, not a buzz) is the natural fifth entry in §9's haptic-cue map**, bookending the wake's own grip pulse (§9 entry a) at the other end of the memory — the screenplay stages the surfacing as the chapter's most physical beat ("comes up off the seat hard, breathing, gripping the wrap," the warm light "almost violent" after the grey), and the hard `ExitDive` fog cut already does the audio work; this would give it a matching felt cue. Flagged for symmetry, low value, not present today.

## 5. Character travel-route master table

**No NPC in Chapter 3 uses the `NpcWalker` + `MissionDirector`-`Trigger` idiom that carries Kessler across Chapter 1.** This is a deliberate structural difference: nothing boards this ship and nobody needs to be walked into place for a reveal — the crew is already gathered for the council, and the "threat" this chapter is internal (the blade), not something that arrives. The chapter's only two forms of non-player movement are ambient idling and the redaction sentinels' self-driven patrol:

| Character / object | Movement mechanism | Detail |
|---|---|---|
| Kessler | `StoryNpcWander`, radius 0.7 m | ambient idle around (-1.6, 0, 4.5); never scripted-walked |
| Resh | `StoryNpcWander`, radius 0.8 m | ambient idle around (2, 0, 3) |
| Mira | `StoryNpcWander`, radius 1.0 m | ambient idle around (1.2, 0, 0.5) |
| Iris | **none** | fixed exactly at her bench (-3.2, 0, 2.6) for the entire chapter — no wander component is added (`wanderRadius: 0f`) |
| `Redaction_Corridor` | self-driven `Update()` ping-pong, `RedactionSentinel.Patrol()` | (-2.4,0,49) ↔ (2.4,0,49), moveSpeed 1.2 m/s — **not** an `NpcWalker`; it re-targets its own destination every time it arrives, forever, which an `NpcWalker`'s one-shot waypoint-array design cannot do |
| `Redaction_BayApproach` | same mechanism | (-3,0,66) ↔ (3,0,66) |
| Player (Ronin-7) | `ContinuousLocomotion` (walk + snap-turn) within each environment; **instant teleport** between environments via `MemoryDiveController` | see below |

**Canon tension — the crew never stages close for the hand-off or the surfacing (flagged, not resolved).** The table above is accurate — no NPC relocates — but it silently contradicts the screenplay's two most intimate stagings. Iris is fixed at her bench (-3.2,0,2.6) for the whole chapter: ~7 m from the weapon rack (4,1,6) where Beat 1's wake and the "hands it to Iris / *It's a sword*" proof play, and ~6 m from the playback seat (0,0.3,8.6) where Beat 3's "Iris is half out of her chair" / Kessler's "heavy hand on his shoulder" surfacing plays. As with the missing viewport (§3), this is treated here as a deliberate simplification rather than a bug to silently fix: per §Beat1b's "the alone-rule is written, not mechanized" logic, the hand-off is dialogue, not a physical object transfer, so Iris's literal distance from the rack doesn't break the beat as authored. Recommended resolution when art lands: either accept the VO-only staging as-is, or add a second seat-side idle position/offset for Kessler and Iris for Beat 3 (and a closer wander waypoint for Iris toward the rack for Beat 1), so a future builder doesn't read "close"/"half out of her chair" and assume the fixed 6–7 m spawns already stage it.

**The same flag extends to Resh and Mira for the Beat 3 debrief.** §4 Beat3a describes `ch3_beat3_debrief` as landing the chapter's charge "with the whole crew present," not just Iris and Kessler at the seat — but Resh (2,0,3) and Mira (1.2,0,0.5) never leave their Beat 0 council spawns, which sit 5–8 m from the debrief anchor (0,1,5). The same recommendation above applies to all four crew, not two: accept the VO-only staging, or give Resh and Mira a seat-side idle offset for Beat 3 alongside Kessler and Iris's.

**Mira has zero VO across the whole chapter, not merely "no lines this beat."** She is the only crew member with no speaker entry anywhere in `Chapter3Lines.cs` — none of the nine dialogue sets author a Mira line, in Beat 0's council or Beat 3's "whole crew present" debrief. A builder hunting Beat 3's debrief for a missing Mira set should stop: none was ever authored, in this chapter or (per her Beat 0 row's "no lines this beat") the beat where that phrasing might otherwise imply she speaks later.

**The player's cross-environment "travel"** is the one movement pattern genuinely unique to this chapter:

| Transition | Mechanism | Target | Trigger |
|---|---|---|---|
| Hold → Playback (dive in) | `MemoryDiveController.EnterDive()` | `DiveEntryPoint` (0, 0, 42) | mission step 5 (`EnterDiveTrigger`'s `OnEnable`) |
| Spotted by a `RedactionSentinel` | `MemoryDiveController.ResetToEntry()` | `DiveEntryPoint` (0, 0, 42) | `RedactionSentinel.OnSpotted` (either sentinel) — **not** a game-over, a soft reset |
| Playback → Hold (surface) | `MemoryDiveController.ExitDive()` | `DiveExitPoint` (0, 0, 8), facing Euler(0,180,0) | mission step 13 (`ExitDiveTrigger`'s `OnEnable`) |

Every one of these three teleports is an instant `Transform.SetPositionAndRotation`, with the rig's `CharacterController` disabled for the single frame of the write and re-enabled immediately after — the same idiom `ZoneBounds`' fall-reset teleport already uses elsewhere in the codebase. **This is comfort-safe by construction**, not because of any special-cased fade or vignette: there is no lerp, no camera pan, nothing for the vestibular system to disagree with. A future patch must not "smooth" this into an animated transition — that would be strictly worse for comfort, not better.

**Reconciling the screenplay's "stutter" with the instant teleport.** The dive-in stage direction describes "the image stuttering and dropping frames as it resolves into the desaturated playback… a held breath behind glass, not a clean cut" — but `EnterDive` is, as above, an instant hard `SetPositionAndRotation` with no visual frame-drop authored anywhere in the pipeline. This is deliberate, not a gap: the frame-drop stutter described in prose is **not built today**, and any future juice pass toward it must be a fixed-frame effect — a `ScreenFader`/`MemoryFlashbackController`-driven fade or a static-texture flash held for a fixed duration — never a moving-camera effect, a simulated frame-rate drop, or anything that touches view-space during the transition; that would reintroduce exactly the vestibular disagreement §1 forbids. The same applies to the Burn-Down (Sub-scene D) stage direction "cracks like ice… burns at the edges" — currently VO-only, with no VFX authored on the footage geometry. This is the strongest candidate in the chapter for a comfort-safe additive: a fixed-frame edge-dissolve effect triggered on `ExitDive`, applied as a shader/material transition on the static-glitch geometry rather than any camera-relative or parallax effect.

**The burn-down's own walked exit is likewise collapsed into the auto-fire trigger — the exit-side twin of §Beat1b's dive-in gap (marginal, flagged).** The screenplay ends the playback on a walked moment, "a final short push toward the exit-light as the geometry dissolves," but step 12→13 (§4 Beat2b) auto-fires `ExitDiveTrigger` the instant `ch3_beat2_burndown`'s VO ends — there is no light beacon and no short traversal beat, so that final push never plays. If the volitional dive-in juice pass above is ever built, it should bookend both ends of the memory: a "press to surface" prompt (reusing the same `PromptInputAdvancer` idiom), or a short walk toward an emissive exit-light panel before `ExitDiveTrigger` fires, so leaving the memory is as chosen an act as entering it. Low priority — largely folded into the dive-in and edge-dissolve additives already flagged above.

## 6. Lighting & background progression table

All Hold light values below are **read from `ChapterEnvironmentProfile`** (once `Ch3Environment.asset` exists), never typed into the builder; the playback's lights are authored directly on `MemoryFlashbackController` and the three per-room point lights (§3.1). Current literals are in Appendix A.1/A.4.

| Beat | Mood | Key/accent entry | Behaviour | Backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Council | warm, domestic, exhausted-safe | `accentLights["HoldLampBench"/"HoldLampSeat"/"HoldLampRack"]` | Bench: `ConsoleFlicker(33)`; Seat: `AmbientPulse(6s)`; Rack: none | none — enclosed room, no exterior view | none; the room is static throughout |
| 1 — The Bonding | same warm baseline, tension entirely in VO | same three accents, unchanged | unchanged | unchanged | no lighting event; the wake is carried by audio, not light |
| 2 — The Playback | drained grey, heavy close fog, one red accent (the glyph) | `MemoryFlashbackController` (fog `ExponentialSquared` 0.045, ambient Flat (0.30,0.30,0.34)) + 3 cheap point lights | none (static per-room point lights, no flicker/pulse) | the three playback rooms activate/deactivate as a unit with the dive | **Trigger (step 5):** `Playback_Kethel7` activates, flashback treatment applies, rig teleports in. **Trigger (step 13):** island deactivates, pre-dive fog/ambient restores, rig teleports out |
| 3 — Carrying the Witness | warm light again, "almost violent" after the grey | same three Hold accents, unchanged, now read as home again by contrast | unchanged | unchanged | none lighting-wise; the contrast is entirely the cut from Beat 2's heavy fog back to Beat 0/1's light baseline |

Fog is the same baseline exponential bed in the Hold across Beats 0, 1, and 3 — a single profile value, never overridden per-room within the Hold. The playback's much denser `ExponentialSquared` fog is a hard cut in and a hard cut back, not a gradual transition, mirroring the dialogue script's "not a clean cut" framing for the *dive-in* (a stuttering, frame-dropping resolve) versus the clean "SMASH UP into the warm light" on the way out.

**The table's Beat 2 row is a hard cut in both directions, which is also why the screenplay's "blue-grey light bleeds back into this room at the climax" (§3) cannot happen as-built** — there is no gradual bleed authored anywhere in this progression, only the two instantaneous triggers at steps 5 and 13. See §3's flagged tension and its `HoldLampSeat` color-lerp recommendation for the comfort-safe way to foreshadow it without adding a literal cross-scene light.

**The "drained grey, heavy close fog" mood entry above is a single shared value across all three playback rooms** — it does not yet distinguish the Aftermath Room's canon "smoke" from the corridor/Bay's generic fog. See §3/§4 Beat2c for the flagged localized-haze recommendation specific to that room.

## 7. Audio / VO manifest cross-reference

Nine canonical dialogue sets, defined in `Chapter3Lines.cs` and consumed via `Chapter3Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch3_beat0_briefing` | 0 | (0, 1, 3) — `Dialogue_Beat0_Briefing` |
| `ch3_beat1_bonding` | 1 | (3.5, 1, 6) — `Dialogue_Beat1_Bonding` |
| `ch3_beat1_shadow_explains` | 1 | (2, 1, 7) — `Dialogue_Beat1_ShadowExplains` |
| `ch3_beat2_threshold` | 2 | (0, 1, 53) — `Dialogue_Beat2_Threshold` |
| `ch3_beat2_aftermath` | 2 | (0, 1, 62) — `Dialogue_Beat2_Aftermath` |
| `ch3_beat2_execution` | 2 | (0, 1, 74) — `Dialogue_Beat2_Execution` |
| `ch3_beat2_burndown` | 2 | (0, 1, 75) — `Dialogue_Beat2_Burndown` |
| `ch3_beat3_naming` | 3 | (0, 1, 8) — `Dialogue_Beat3_Naming` |
| `ch3_beat3_debrief` | 3 | (0, 1, 5) — `Dialogue_Beat3_Debrief` |

Each is built by the local `Ch3BuildDialogue` wrapper (not the shared `BuildDialoguePlayer` clip loader, which looks in the wrong folder for this chapter — same pattern as Ch1's `BuildChapter1Dialogue`): it calls the shared player builder with `clipSetId: null`, then wires clips itself via `Ch3WireVoiceClips`, resolving each line's `AudioClip` from `Chapter3Lines.ClipName(setId, index, speaker)` — pattern `ch3_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line and the grip prompt is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all nine `DialoguePlayer`s and the `PromptInputAdvancer`.

**Dialogue is data, not art.** None of this changes in the refactor — the nine set ids, their positions, and the clip-resolution pattern are canon.

SFX / ambience beds:

| Clip / source | Used for |
|---|---|
| `HoldAmbience` (procedural, `BuildAmbienceLayer`) | the Hold's 3D ambient bed, running throughout Beats 0/1/3 — a low engine-idle hum, the "running quiet" texture of a ship a few hours off a hard run, not a generic room tone |
| `PlaybackDreadAmbience` (procedural, `BuildAmbienceLayer`) | the playback's 3D ambient bed, running only while the dive is active — close and padded, "like a held breath behind glass," the deliberate cold/oppressive contrast to `HoldAmbience`'s warm idle hum. **Single-source coverage gap** — one source at the Aftermath Room's center (outer 12) does not reach `DiveEntryPoint` (z42) or Khall's Bay (z74–76); see §4 Beat2f. The coverage-gap-immune companion fix is `MemoryFlashbackController`'s own unwired `heartbeatLoop` (2D, `spatialBlend = 0`, island-wide) — see §4 Beat2f/§9 |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | assigns generated clip assets to any `AudioSource` left unresolved by the above, same call every other chapter builder makes |
| `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` | auto-tags the Hold and all three playback rooms as distinct interior reverb volumes — the playback's smaller, tighter rooms should read audibly more contained than the Hold |

No `WaveAlarm.wav`/`Landing.wav`/`HologramOn.wav`/`DoorSlide.wav`-style event stingers exist in this chapter — there are no doors, no alarms, and no literal hologram reveal (Ghost_Khall is a material-swapped memory-cast figure, not a projected hologram with its own audio cue, §4 Beat 2c).

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 03 — The Sword Remembers** (`XRRigBuilder.BuildChapter3SwordRemembers()`).
2. **EditMode is the gate.** Current project-wide baseline per `CLAUDE.md`: **842 tests green, 0 skips**; PlayMode 70/70. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot, same shape as Ch1's.** No EditMode test invokes `BuildChapter3SwordRemembers()` or loads `Ch03_SwordRemembers.unity` directly. `Project/Docs/CHAPTER-BUILD-LEDGER.md` records Chapter 3's *fixture* test count (452 tests, PASS 2026-07-03) — meaning unit tests exist for pieces this chapter depends on (dialogue data, shared builders, `RedactionSentinel.CanSee`, `MemoryDiveController`'s pure logic), not an end-to-end "does the scene still build" assertion. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **Memory-dive regression coverage.** `RedactionSentinel.CanSee` is a pure static method (no scene dependency) — any patch to detection range/angle must keep its existing EditMode coverage green. `MemoryDiveController`'s pre-dive-settings snapshot/restore logic is likewise unit-testable in isolation from a running scene; verify `EnterDive()`→`ExitDive()` round-trips restore the exact pre-dive fog/ambient values, not just "some" values, if this component is touched.
4. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
5. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) — the Hold *and* the full three-room playback island — plus one `LogWarning` per unresolved key. Never an empty room, never an exception, and never a playback island that fails to activate because a room-shell prefab silently resolved to `null` geometry.
6. **Perf reference bar.** No chapter-specific `UnityStats` baseline exists yet (§1.6) — capture one at greybox before the first prefab lands, and record it in `CHAPTER-BUILD-LEDGER.md` the way Ch1's entry does. The 72 Hz floor is not negotiable.
7. **Manual playthrough check specific to this chapter:** confirm the three teleports (dive-in, sentinel-spotted reset, dive-out) never leave the rig standing over deactivated/non-existent geometry — each of `EnterDive`/`ResetToEntry`/`ExitDive` depends on its target `Transform` still resolving after a scene edit; a dangling `diveEntryPoint`/`diveExitPoint` reference fails silently (`TeleportRig` early-returns on a null target) rather than throwing, so this will not show up as a console error. **Also confirm `Redaction_Corridor`'s patrol phase/facing at the instant of the step-5 `EnterDive` teleport** — `DiveEntryPoint` (z42) sits inside its default 8 m detection range (§4 Beat2b), and unlike a post-reset arrival there is no `spottedCooldown` grace on the very first dive-in, so a badly-timed facing could spot-and-reset the player before they take a single step.
8. **Console check:** `Ch3WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter3SwordRemembers()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter3Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Consolidated haptic-cue map (none authored today, flagged for a future pass).** Every beat above independently notes "Haptics: none" — this is accurate, but scattering that flag beat-by-beat leaves no single place a builder can find the intended cue set. CLAUDE.md is explicit that non-combat feel is carried by `Haptics`/`AudioDirector`/reticle, never camera shake, and this chapter has the game's most emotionally loaded non-combat beat with zero haptic authoring. If/when this chapter gets a juice pass, four comfort-safe pulses are the recommended set: (a) a short grip pulse the instant `GripKatanaPrompt` (step 2) advances — the physical "click" of the wake; (b) a short warning pulse alongside `RedactionSentinel.OnSpotted`/`dive.ResetToEntry()`, so the reset registers as a felt correction, not just a silent teleport; (c) a low sustained pulse under Khall's "I know." line in `ch3_beat2_execution`; (d) a short, sharp haptic drop timed to the switch-fire and the footage-Ronin's collapse later in that same line set ("That's the switch...") — arguably the second-highest-value cue in this list, since it is the literal beat §1.1 names as the chapter's single most emotionally loaded set piece, and it currently has no pulse of its own even though (c) precedes it; (e) a gentle settle/exhale pulse (a soft return, not a buzz) on `ExitDive` at Beat 3's surfacing, bookending (a)'s wake-grip pulse at the other end of the memory — low value, listed for symmetry. All five are flagged as future work, not present in the builder today.
- **The killswitch-fire's audio sting is the second-highest-value audio addition in this chapter, after the wake tone (see §4 Beat2f).** The switch firing and the footage-Ronin's collapse — the single most emotionally loaded set piece in the game so far (§1.1) — currently carry no dedicated audio cue, only VO performance and staging. A single low, close "cut" sting, comfort-safe by construction (no directional/camera-relative component), is the recommended fix; pair it with haptic-map entry (d) above.
- **The dive-in has no volitional trigger (see §Beat1b).** Step 5 auto-fires the memory dive the instant step 4's dialogue ends; the screenplay stages it as a deliberate palm-on-the-wrap act. A "press to dive" prompt reusing the existing `PromptInputAdvancer`/`Ch3BuildPrompt` idiom (already used for the grip prompt at step 2) would make the crossing a chosen embodied VR moment rather than an automatic cut — flagged as a future immersion pass, not built today.
- **The wake tone is the chapter's signature sound and has no cue authored (see §Beat1f).** The screenplay's rising, then Iris-silenced, then re-climbing TONE is the alone-rule's own sonic proof — currently folded into "VO performance" with nothing dedicated wired to the grip-prompt advance or the hand-off line. Flagged as the highest-value audio addition in this chapter, ahead of the glyph pulse below.
- **The playback's unwired `heartbeatLoop` is this chapter's highest-value audio fix, and the wake tone's natural companion.** `MemoryFlashbackController` (§4 Beat2f) already carries a `heartbeatLoop` clip + `heartbeatVolume` field that plays 2D and island-wide on `Awake()` the moment `Playback_Kethel7` activates — unlike the point-source `PlaybackDreadAmbience`, it does not attenuate across the 40 m island, so it is audible at `DiveEntryPoint`, through the Aftermath silence, and across Khall's execution alike, with zero new authoring beyond assigning a clip. Wire this before reaching for a per-room ambience split (§4 Beat2f) — it is the cheaper, comfort-safe (2D, no motion), canon-literal ("a held breath behind glass") realization of the same gap.
- **The kill-order glyph is static as-built; a pulse is the single cheapest comfort-safe additive in this chapter.** Both the dialogue script and the builder's own source comment describe the glyph as pulsing, but `KillOrderGlyph` (§4 Beat2c) is a static unlit primitive with no animation component. Because it is the one saturated color in an otherwise desaturated island, a shared `AmbientPulse`-style emissive-intensity throb (no camera motion, no combat gating) is the obvious place to spend a small juice pass — flagged for future work, not present today. **This pulse is not only mood** — per §4 Beat2b, it is also the only thing that can motivate the Aftermath's unmechanized "turn back through the door" reveal, so a future lighting pass must keep the glyph's sightline from `AftermathReachPoint` through the `Corridor_WallBack` gap clear, not just add the throb.
- **Do not auto-delete orphan materials.** Same standing caution as every other chapter (`Project/Docs/IMPROVEMENT-SUMMARY.md`) — reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Same standing prevention rule as every other chapter; the ghost figures and censor slabs are collider-stripped by design (they are memory-cast, never obstacles) — a prefab swap must preserve that, not add a collider where the primitive fallback deliberately has none.
- **Speaker-label discipline: `Shadow` before the naming, `Echo` after.** Every AI line in `ch3_beat0_briefing` (n/a — the AI hasn't woken yet), `ch3_beat1_bonding`, `ch3_beat1_shadow_explains`, and all four `ch3_beat2_*` sets is labeled `Shadow`. The switch to `Echo` happens mid-`ch3_beat3_naming`, at the exact line where Ronin-7 says "Echo. I'm calling you Echo from now on." — every AI line at or after that point, in this set and `ch3_beat3_debrief` and every later chapter, is `Echo`. **Do not retroactively relabel pre-naming lines, and do not let a future chapter's dialogue call the AI "Shadow."** This mirrors Ch1's "Khall stays unnamed on screen" discipline: the label change is itself part of the story being told, not a data-cleanliness issue to normalize away.
- **Objective 3's "Witness abilities / shadow-AI mechanic" is not missing — it lives on `ch3_complete`, not `ch3_echo_named` (see §4 Beat3b).** The story bible's GAME NARRATIVE DESIGN numbers this chapter's Objective 3 as unlocking the katana's "Witness" abilities; a reader cross-checking that canon against this document's flat "no ability unlocks on this flag this chapter" (§4 Beat3b) could conclude the objective was dropped. It wasn't — `EchoPresence`, dormant until `ch3_complete`, is that unlock, realized as a saga-wide flag rather than an in-chapter weapon pickup. Any future patch to `EchoNamedFlag`/`ChapterOutro` must keep the two flags distinct: naming is story bookkeeping, `ch3_complete` is the mechanical unlock.
- **`Khall` is named on screen in this chapter, unlike Ch1.** The dialogue script and `Chapter3Lines.cs` both use the plain speaker name `Khall` throughout Beat 2 — this is the archived recording of a mission that predates the present-day chapters where Khall stays unnamed to the player (Ch1, per that document's §9). Do not "fix" this into `"Handler (recorded)"` to match Ch1's convention; the two chapters have different naming rules for a reason (Ch3 is Ronin-7's own memory, which already knows the name, even if the present-day Ronin-7 does not consciously recall it until later chapters name Khall aloud).
- **No combat exists anywhere in this chapter, by design.** Confirm this stays true through any future patch: no `Enemy`/`Health` pair, no `BladeDamager`, no `DefeatEnemies` mission step. The `RedactionSentinel`s are a stealth-detection hazard with a soft-reset failure state, not a combat encounter — do not "upgrade" them into fightable enemies; that would contradict the beat treatment's explicit "no combat, no enemies, no boss" design intent.
- **The Aftermath room's emptiness is authored, not a placeholder.** The code comment is explicit: "Deliberately EMPTY — smoke-grey, nothing staged." A future art pass should not fill this room with gore geometry or additional ghost figures — the massacre is meant to be carried entirely by dialogue and the room's staged vacancy.
- **Canon soft spot (flagged, not fixed):** `Chapter3Lines.cs`'s own header notes that `story ouput/audit/Ch03_audit.md` found **zero hard consistency errors** in this chapter, but flagged a recurring craft issue — an "it's not X, it's Y" antithesis addiction across 8+ lines. Every flagged instance has already been thinned in the shipped `Chapter3Lines.cs` (see the inline `// Audit fix:` comments throughout that file) — this document's dialogue tables above reflect the **already-corrected** line text, not the dialogue script's original phrasing where the two differ. If a future editor touches these lines again, preserve the thinned phrasing rather than reverting to the more symmetrical original.
- **The RETCON is load-bearing for this chapter specifically.** Per `00_STORY_BIBLE.md` §0: "Kethel-7" is the planet/mission, not a separate operative; the playback is Ronin-7's own pre-killswitch memory, and he is the first defector. This chapter is where that retcon is *shown*, not just stated — any future patch to the playback's staging or dialogue must keep the footage unambiguously read as Ronin-7's own erased past, not a third party's story he is merely witnessing secondhand.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared`/`BuildProp` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals (the Cairn Hold)

Currently set inline at the top of `BuildChapter3SwordRemembers` (`Chapter3Builder.cs:60–92`).

| | Value |
|---|---|
| Directional key | color (1, 0.85, 0.65), intensity 0.45, rotation Euler(50, -35, 0) |
| Ambient | mode **Flat**, color (0.14, 0.12, 0.10) |
| Fog | mode **Exponential**, color (0.17, 0.14, 0.11), density 0.018 |
| Floor tint | (0.2, 0.17, 0.14) |
| Ceiling tint | (0.1, 0.09, 0.08) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `HoldLampBench` | (-3, 1.6, 2) | (1, 0.8, 0.55) | 1.6 | 8 | `AddConsoleFlicker(seed: 33f)` |
| `HoldLampSeat` | (0, 2.4, 8) | (1, 0.75, 0.5) | 1.2 | 9 | `AddAmbientPulse(period: 6f)` |
| `HoldLampRack` | (4, 2.2, 6) | (0.85, 0.8, 0.7) | 1 | 7 | none |

### A.2 Beat 0/1/3 — The Cairn Hold

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-5,5], z[-4,10], center (0,0,3), 10×14, `RoomH`=3.6 | `Chapter3Builder.cs:82-92` |
| Walls (all solid, no gaps) | `Hold_WallW` x=-5; `Hold_WallE` x=5; `Hold_WallFront` z=-4; `Hold_WallBack` z=10 — each (0.2 or full-width, RoomH, 14 or 10) | `BuildWall` |
| Detail scatter | center (0,0,3), halfExtents (5,7), accent (0.38,0.3,0.24) | `BuildRoomDetails` |
| Iris's bench | (-3.6,0.45,2.2) scale(1.8,0.9,0.8), worn (0.34,0.29,0.24) | `Ch3BuildHoldStory` → `IrisBench` |
| Stripped board | (-3.5,0.95,2.2) scale(0.5,0.05,0.35), steel (0.4,0.42,0.46) | `Ch3BuildHoldStory` → `StrippedBoard` |
| **Gap: no cracked slate** | Iris reads the Velorum files "off a cracked slate" and taps it mid-line, but `Ch3BuildHoldStory` builds only the bench and the (unrelated) stripped board — target state adds `Props.Slate` (~(-3.7,0.95,2.35), beside the stripped board) per §4 Beat0c | `Chapter3Builder.cs:509-544` (gap) |
| Drive stacks ×3 | (2.4,0.15,2.4) / (2.9,0.1,2.6) / (2.6,0.42,2.45) | `Ch3BuildHoldStory` → `DriveStack0/1/2` |
| Galley shelf | (-1.5,0.45,8.8) scale(1.4,0.9,0.5), worn*0.9 | `Ch3BuildHoldStory` → `GalleyShelf` |
| **Gap: no battered pot** | §3 and the screenplay both name Kessler's pot; `Ch3BuildHoldStory` builds the shelf and three cups but no pot object — target state adds `Props.BatteredPot` (~(-1.5,0.96,9.0), on the shelf) per §4 Beat0c | `Chapter3Builder.cs:509-544` (gap) |
| Cups ×3 | (-1.8+i*0.3, 0.96, 8.8), scale(0.07,0.06,0.07), cupColor (0.45,0.43,0.4) | `Ch3BuildHoldStory` → `Cup_0/1/2` (`PrimitiveType.Cylinder`, `TintShared`) |
| Weapon rack | back (4.85,1.2,6) scale(0.1,0.7,1.4); pegs (4.6,1.12,5.6)/(4.6,1.12,6.4) scale(0.4,0.05,0.05) | `Ch3BuildHoldStory` → `WeaponRack_Back/PegA/PegB` |
| Playback seat | (0,0.3,8.6) scale(0.9,0.6,0.9); back (0,0.85,9.05) scale(0.9,0.9,0.15) | `Ch3BuildHoldStory` → `PlaybackSeat`/`PlaybackSeat_Back` |
| **Gap: no vitals rig** | Iris's "improvised rig" monitors his vitals per §3/Beat1's dialogue, but `Ch3BuildHoldStory` builds the seat and nothing else near it — target state adds `Props.VitalsRig` (~(0.6,0.5,8.8), seat corner) per §4 Beat0c | `Chapter3Builder.cs:509-544` (gap) |
| Katana "Echo" | (4.3,1.2,6), rot identity | `BuildSword(pos, Quaternion.identity, weapon, Ch3EchoBladePrefab)` |
| Kessler / Iris / Resh / Mira | (-1.6,0,4.5) w0.7 / (-3.2,0,2.6) w0 / (2,0,3) w0.8 / (1.2,0,0.5) w1.0 | `Ch3PlaceStoryNpc` ×4 |
| RackReachPoint | (4,1,6), radius 2.5 | `AuthorReachStep(steps, 1, …, rackReachGo.transform, 2.5f)` |
| GripKatanaPrompt | (4,1.6,6), scale 0.012, text "Grip the Katana  (Y)", inactive | `Ch3BuildPrompt` |
| Dialogue anchors | (0,1,3) briefing; (3.5,1,6) bonding; (2,1,7) shadow explains; (0,1,8) naming; (0,1,5) debrief | `Ch3BuildDialogue` ×5 (Hold-side sets) |
| EchoNamedFlag | inactive; `CampaignFlagSetter` flags=["ch3_echo_named"], setOnEnable=true | `Chapter3Builder.cs:191-199` |
| ChapterOutro | (0,1,8), inactive; flags=["ch3_complete"]; completeCanvas=(0,1.4,9) | `Chapter3Builder.cs:201-219` |
| Mission steps | indices 0-4, 14-17 of 18 | `AuthorDialogueStep`/`AuthorReachStep`/`AuthorPromptStep`/`AuthorTriggerStep` |
| Player rig | `BuildRig(addLocomotion:true)` + `EchoPresence`; `ZoneBounds` center (0,0,38) r55 | — |

### A.3 Beat 2 — The Playback (Threshold / Aftermath / Khall's Bay / Burn-Down)

| Item | Value | Source |
|---|---|---|
| `Playback_Kethel7` root | inactive at build time (`diveRootGo.SetActive(false)`) | `Chapter3Builder.cs:97-101` |
| `MemoryFlashbackController` | on `diveRootGo`; defaults: fog `ExponentialSquared` (0.35,0.37,0.42) density 0.045; ambient Flat (0.30,0.30,0.34); no heartbeat clip | `Chapter3Builder.cs:99` |
| Threshold Corridor shell | center (0,0,48), 6×16, x[-3,3] z[40,56]; `Corridor_WallW` x=-3; `Corridor_WallE` x=3; `Corridor_WallFront` z=40 (solid); `Corridor_WallBack` z=56 (doorway, gap 1.6) | `Ch3BuildPlayback`, `Chapter3Builder.cs:359-363` |
| **Gap: no half-shut door leaf** | §2's doors table and the screenplay both name "a half-shut door" at `Corridor_WallBack`; as-built the aperture is a plain open `BuildDoorwayWall` gap with no leaf object — target state adds `Props.HalfShutDoor` (~(0, RoomH/2, 56), partially closed) per §4 Beat2c | `Chapter3Builder.cs:359-363` (gap) |
| Static glitch blocks ×3 | (-2.6,1.6,44) scale(0.3,1.2,0.8); (2.6,0.8,47) scale(0.3,1.6,0.6) *0.8 tint; (-2.5,2.2,51) scale(0.4,0.9,0.9) *1.15 tint | `StaticBlock0/1/2` |
| Ghost figures (Caretaker/Child0/Child1) | (0,0,54) h1.75; (-0.7,0,57) h1.1; (0.6,0,57.4) h1.0 — capsule torso + sphere head, `MakeGhostMaterial()`, colliders stripped; **`Ch3BuildGhostFigure` never sets a rotation, so all three sit at `Quaternion.identity` (facing +Z) regardless of `pos` — the Caretaker reads facing away from the incoming player, a facing bug the target-state rotation (§4 Beat2c) corrects, not a deliberate choice** | `Ch3BuildGhostFigure` |
| **Gap: no Threshold `Ghost_FootageRonin`** | as-built, `Ch3BuildPlayback` places only Caretaker/Child0/Child1 in the corridor — the frozen operative Ronin-7's "That's my face" points at is never instantiated there; the chapter's only `Ghost_FootageRonin` build call is in the Bay (z72.8, below). This is a missing figure, not a facing bug — target state adds a fourth ghost, `Ghost_FootageRonin_Threshold` (~(0,0,51.5)), per §4 Beat2c | `Chapter3Builder.cs:372-376` (gap) |
| KillOrderGlyph | (0,2.1,53.4) scale(0.35,0.35,0.05), unlit red (0.9,0.1,0.08), collider stripped | `Chapter3Builder.cs:376-382` |
| Aftermath Room shell | center (0,0,62), 10×12, x[-5,5] z[56,68]; `Aftermath_WallW` x=-5; `Aftermath_WallE` x=5; `Aftermath_WallBack` z=68 (doorway, gap 2.0); deliberately no props | `Chapter3Builder.cs:385-388` |
| Khall's Bay shell | center (0,0,74), 8×12, x[-4,4] z[68,80]; `Bay_WallW` x=-4; `Bay_WallE` x=4; `Bay_WallBack` z=80 (solid) | `Chapter3Builder.cs:392-395` |
| `Bay_KillswitchConsole` | (0,0.55,74.5) scale(1.4,1.1,0.6), greyProp tint | `Chapter3Builder.cs:396` |
| `Ghost_FootageRonin` | (0,0,72.8) h1.8; **as-built is a standing figure — `ch3_beat2_execution` lands on the fall ("goes down between one breath and the next"), so target state resolves this instance to a fallen/collapsed pose, not standing (§4 Beat2c)** | `Ch3BuildGhostFigure` |
| `Ghost_Khall` | (0,0,76.2); `Khall.prefab` instantiated + `FitNamedCharacter`, all renderers set to `ghostMat`; **`InstantiateNpc` sets no rotation, so this sits at identity (facing +Z, the back wall) — the screenplay's "face each other across the console" blocking is not honored as-built; target-state rotation is `Euler(0,180,0)`, plus a commissioned pose (averted gaze, hand planted on the console — "cannot look at it") (§4 Beat2c)** | `Chapter3Builder.cs:400-404` |
| Playback lights | `PlaybackLight_Corridor` (0,2.6,48) (0.55,0.6,0.7) i1.2; `PlaybackLight_Aftermath` (0,2.6,62) (0.5,0.52,0.58) i0.9; `PlaybackLight_Bay` (0,2.6,74) (0.6,0.65,0.75) i1.3 — all point, range 12, shadows off, parented under `diveRoot` | `Ch3BuildPlaybackLight` ×3 |
| `DiveEntryPoint` | (0,0,42), rot identity, parented under `diveRoot` | `Chapter3Builder.cs:413-415` |
| `Redaction_Corridor` | waypoints (-2.4,0,49)/(2.4,0,49); censor slab (0.8,2.2,0.25) dark (0.08,0.08,0.1) + static band (0.85,0.25,0.28) light-grey (0.5,0.52,0.58); `RedactionSentinel` target=rigHead, `OnSpotted`→`dive.ResetToEntry` | `Ch3BuildRedactionSentinel` |
| `Redaction_BayApproach` | waypoints (-3,0,66)/(3,0,66); same slab/band construction | `Ch3BuildRedactionSentinel` |
| `MemoryDiveController` | `diveRoot`=`diveRootGo`; `diveEntryPoint`; `diveExitPoint`=`exitPointGo` (0,0,8) rot Euler(0,180,0); `rigRoot`=rig transform; `flashback`=`MemoryFlashbackController` | `Chapter3Builder.cs:129-140` |
| `EnterDiveTrigger` | inactive; `MemoryDiveEntryTrigger.dive`=the controller | `Chapter3Builder.cs:142-147` |
| `ExitDiveTrigger` | inactive; `MemoryDiveExitTrigger.dive`=the controller | `Chapter3Builder.cs:149-154` |
| Reach points | `ThresholdReachPoint` (0,1,52.5) r3.5; `AftermathReachPoint` (0,1,61) r3.5; `BayReachPoint` (0,1,73) r3.5 | `Chapter3Builder.cs:164-171` |
| Dialogue anchors | (0,1,53) threshold; (0,1,62) aftermath; (0,1,74) execution; (0,1,75) burndown | `Ch3BuildDialogue` ×4 (playback-side sets) |
| Mission steps | indices 5-13 of 18 | `AuthorTriggerStep`/`AuthorReachStep`/`AuthorDialogueStep` |
| Ambience | `PlaybackDreadAmbience` (0,1.5,62), inner 3, outer 12, vol 0.4 | `BuildAmbienceLayer` |

### A.4 Scene root hierarchy (current)

`BuildChapter3SwordRemembers()` creates these as siblings: `Directional Light`, `CairnHold` (Hold geometry + dressing), the three Hold accent lights, `Playback_Kethel7` (inactive; the entire playback island including both `RedactionSentinel`s and `DiveEntryPoint`, all as descendants), `Game` (`GameState` + `CombatFeedbackController`), the player rig, the katana, the four crew NPCs, `MemoryDive` (the controller), `EnterDiveTrigger`, `ExitDiveTrigger`, `DiveExitPoint`, both reach-point families, nine dialogue-player roots, `GripKatanaPrompt`, `EchoNamedFlag`, the complete canvas, `ChapterOutro`, `XR Interaction Manager` (if absent), and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and four `[BEAT_N_LOGIC]` roots, and — per §1.2/§1.4 — splits `Playback_Kethel7`'s current mixed contents so its art (rooms, ghost figures, glyph, censor-slab visuals, lights) moves under the static-art root while its logic (`DiveEntryPoint`, both `RedactionSentinel` components and their waypoint transforms) moves under `[BEAT_2_LOGIC]`, with the now-pared-down `Playback_Kethel7` GameObject itself remaining the single activate/deactivate switch `MemoryDiveController` toggles.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key this chapter's target-state builder resolves, its expected path, and its current status. **Six of twenty-five distinct keys already resolve** — every named-cast prefab this chapter uses was already produced for earlier chapters — everything else is a commission.

| Category.Key | Resolves to | Status | Used by |
|---|---|---|---|
| `Rooms.CairnHoldShell` | `Assets/Ronin7/Art/Generated/Rooms/CairnHoldShell.prefab` | **MISSING** | Beat 0 (reused Beats 1/3) — commission direction: patched conduit, mismatched panel materials per the SETTING block, not a uniform clean shell (§3) |
| `Rooms.PlaybackCorridorShell` | `Assets/Ronin7/Art/Generated/Rooms/PlaybackCorridorShell.prefab` | **MISSING** | Beat 2A |
| `Rooms.PlaybackAftermathShell` | `Assets/Ronin7/Art/Generated/Rooms/PlaybackAftermathShell.prefab` | **MISSING** | Beat 2B |
| `Rooms.PlaybackBayShell` | `Assets/Ronin7/Art/Generated/Rooms/PlaybackBayShell.prefab` | **MISSING** | Beat 2C/D |
| `Props.WorkBench_Salvage` | `Assets/Ronin7/Art/Generated/Props/WorkBench_Salvage.prefab` | **MISSING** | Beat 0 (Iris's bench) |
| `Props.StrippedBoard` | `Assets/Ronin7/Art/Generated/Props/StrippedBoard.prefab` | **MISSING** | Beat 0 |
| `Props.Slate` | `Assets/Ronin7/Art/Generated/Props/Slate.prefab` | **MISSING** | Beat 0 (Iris's cracked slate — no as-built equivalent exists yet; §4 Beat0c) |
| `Props.DriveStack` | `Assets/Ronin7/Art/Generated/Props/DriveStack.prefab` | **MISSING** | Beat 0 (×3 instances) |
| `Props.GalleyShelf` | `Assets/Ronin7/Art/Generated/Props/GalleyShelf.prefab` | **MISSING** | Beat 0 |
| `Props.BatteredPot` | `Assets/Ronin7/Art/Generated/Props/BatteredPot.prefab` | **MISSING** | Beat 0 (Kessler's pot — no as-built equivalent exists yet; §4 Beat0c) |
| `Props.DentedCup` | `Assets/Ronin7/Art/Generated/Props/DentedCup.prefab` | **MISSING** | Beat 0 (×3 instances; candidate to share Ch1's equivalent key once authored) |
| `Props.WeaponRack_Frame` | `Assets/Ronin7/Art/Generated/Props/WeaponRack_Frame.prefab` | **MISSING** | Beat 0 |
| `Props.PlaybackSeat` | `Assets/Ronin7/Art/Generated/Props/PlaybackSeat.prefab` | **MISSING** | Beat 0 |
| `Props.KillswitchConsole` | `Assets/Ronin7/Art/Generated/Props/KillswitchConsole.prefab` | **MISSING** | Beat 2C |
| `Props.HalfShutDoor` | `Assets/Ronin7/Art/Generated/Props/HalfShutDoor.prefab` | **MISSING** | Beat 2A (the half-shut door leaf at `Corridor_WallBack` — no as-built equivalent exists yet; §4 Beat2c) |
| `Props.VitalsRig` | `Assets/Ronin7/Art/Generated/Props/VitalsRig.prefab` | **MISSING** | Beat 0/1 (Iris's improvised vitals monitor near the playback seat — no as-built equivalent exists yet; §4 Beat0c/Beat1b) |
| `Vfx.StaticGlitchBlock` | `Assets/Ronin7/Art/Generated/VFX/StaticGlitchBlock.prefab` | **MISSING** | Beat 2A (×3 instances today; target state should add sparse instances in 2B/Aftermath and subtler ones in 2C/Bay — §4 Beat2c) |
| `Vfx.GhostFigure` | `Assets/Ronin7/Art/Generated/VFX/GhostFigure.prefab` | **MISSING** | Beat 2A (×3 civilian instances: Caretaker, Child0, Child1 — §4 Beat2c) |
| `Vfx.GhostOperative` | `Assets/Ronin7/Art/Generated/VFX/GhostOperative.prefab` | **MISSING** | Beat 2A/2C (×2 instances: FootageRonin_Threshold, FootageRonin_Bay — distinct from the civilian `Vfx.GhostFigure` key so "that's my face" reads against Ronin-7's own silhouette/augment-seams, not a generic shape; commission direction includes a ghost-tinted katana in the figure's hands — raised for the threshold instance, dropped/lowered beside the body for the Bay instance — since it is the same blade now named Echo; **the Bay instance must be a fallen/collapsed pose, not standing** — the execution's death-beat has no body to land on otherwise; §4 Beat2c) |
| `Vfx.KillOrderGlyph` | `Assets/Ronin7/Art/Generated/VFX/KillOrderGlyph.prefab` | **MISSING** | Beat 2A |
| `Vfx.RedactionSentinel` | `Assets/Ronin7/Art/Generated/VFX/RedactionSentinel.prefab` | **MISSING** | Beat 2 (×2 instances) |
| `Named.Kessler` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab` | **EXISTS** | Beat 0 |
| `Named.Iris` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Iris.prefab` | **EXISTS** | Beat 0 |
| `Named.Resh` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Resh.prefab` | **EXISTS** | Beat 0 |
| `Named.Mira` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Mira.prefab` | **EXISTS** | Beat 0 |
| `Named.Khall` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** | Beat 2C (`Ghost_Khall`, ghost-material override; commissioned pose adds averted gaze + hand planted on `Bay_KillswitchConsole` on top of the Euler(0,180,0) yaw fix — "cannot look at it" — §4 Beat2c) |
| `Named.Echo` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** | Beat 0 (the katana itself) — note: not registry-`Resolve`d; instantiated via the frozen `BuildSword` helper (§4 Beat0c notes), which also wires `WeaponDefinition` |

Note that `Named.Khall` is used twice across the saga in two structurally different ways — as a projected hologram in Ch1/Ch4 (renderer intact, `HologramOn.wav` cue) and as a memory-cast ghost here in Ch3 (renderer material swapped to `MakeGhostMaterial()`, no audio cue) — the same prefab, two different presentation treatments applied at instantiation time, not two separate registry keys.

---

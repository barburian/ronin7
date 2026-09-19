# Chapter 4 — Scene Construction

*The architectural contract for `Ch04_OverseersHunt.unity`: what Chapter 4 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 4 ("The Overseer's Hunt") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter4Builder.cs`, entry point `XRRigBuilder.BuildChapter4OverseersHunt()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 04 — The Overseer's Hunt**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch04_The_Overseers_Hunt.md`, `Ch04_The_Overseers_Hunt_Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** Combat and impact feedback in this scene come from `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never from moving the camera. This applies to the Sink snatch-team skirmish (Beat 2), the two heat-triggered hunter-wave ambushes, and the Kerrax duel (Beat 5).
- **Traversal in Ch4 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`. **There is no teleport locomotion, no NavMesh, and — despite what the screenplay describes — no parkour/climb/wall-run system anywhere in this chapter.** This is a documented, deliberate decision, not an oversight: the builder's own header comment (`Chapter4Builder.cs:30-39`) records that `ZeroGGrabLocomotion` (the project's only climbing-style mechanic) is scoped to weightless/space interiors, was judged wrong for a gravity-bound flooded cave, and that **no new climbing system was built either**. The Deepworks — where the dialogue script calls for "wall-runs, ledge-jumps, collapsing footing, drops over black water" — is built instead as **one open, unwalled cave floor** (mirroring the project's exterior ground-plane pattern) threaded with static rock-pillar obstacles that force a foot-level switchback path, walked with the same `ContinuousLocomotion` as every other room in the game, plus a single `FloodingWaterHazard` trigger volume near the bottom for tension (chip damage while submerged, never a drowning fail state). **Do not describe wall-runs or ledge-jumps as implemented gameplay when patching this scene** — they are screenplay stage direction realized through darkening lighting, a narrowing palette, and one hazard volume, not through movement code.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | room shells, props, VFX, backdrops, decorative lights *(there are no doors in this chapter — see §2)* | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC spawns + wander, enemy spawns (inactive), scan-drone volumes, heat-meter wiring, reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**Chapter 4 has no door objects, so the "one object spans both" exception from Chapter 1 does not apply here.** Room-to-room transitions are open archways (`BuildDoorwayWall`, a wall with a lintel over a fixed-width gap) with no `ProximityDoor`, no lock state, and no unlock trigger anywhere in the chapter — see §2 for the full implication.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildDoorwayWall`, `BuildProp`, `BuildAccentPointLight`, `BuildShipDrone`, `BuildEnemy`, `BuildWaveSpawner`, `BuildHologram`, `Author*Step` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`.
- **Free to restructure:** the Ch4-local helpers, called only from `BuildChapter4OverseersHunt()` — `Ch4EnsureKerraxDefinition`, `Ch4BuildDialogue`, `Ch4WireVoiceClips`, `Ch4PlaceStoryNpc`, `Ch4BuildScanDrone`, `Ch4BuildSinkStalls`, `Ch4BuildDeepworksProps`, `Ch4BuildKerraxHoldDetails`, `Ch4BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 4 and are now silently rebuilding thirteen other chapters.

**As of today, `Chapter4Builder.cs` is one method.** `BuildChapter4OverseersHunt()` is a single ~360-line function with no `BuildBeatNArt`/`BuildBeatNLogic` split anywhere — exactly the state Chapter 1's document described before its own refactor. This document specifies the target split; Appendix A is where the current one-method reality is recorded, as it is for Chapter 1.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** The same two ScriptableObjects introduced for Chapter 1 carry everything this builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch4Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, floor + ceiling tint, per-room accent lights, per-room `ConsoleFlicker`/`AmbientPulse` behaviour |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document — the **same asset** Chapter 1 reads from, namespaced by key, not duplicated per chapter |

**Neither class exists in the codebase today** (confirmed by search — no `ChapterEnvironmentProfile`, no `ArtAssetRegistry` type anywhere under `Scripts/`), exactly the situation Chapter 1's document describes. This is still net-new work, not yet started for any chapter.

Prefab paths never appear in builder code. The builder asks the registry for `Props.CavePillar`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`.** As of this writing `Rooms/`, `Props/`, `Doors/`, and `VFX/` do not exist under that folder (confirmed on disk) — the same "zero environment prefabs" state Chapter 1 is in. Only `Characters3D/{Named,Enemies,Diversity}/` exist, and Chapter 4 draws from both.

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter4OverseersHunt()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter4Builder.cs:59`) — the same call Chapter 1 makes. That call does not *delete objects from* the scene — it **discards the entire scene** and opens a fresh empty one. There is nothing left to search for. A naïve `GameObject.Find("[STATIC_ART_DO_NOT_DELETE]")` after `NewScene` will always return `null`.
>
> Making the safe zone real requires **replacing the wipe strategy**, one of:
>
> 1. `EditorSceneManager.OpenScene(Ch4ScenePath)`, then `DestroyImmediate` each **generated root by name** (`Drovis`, `Deepworks`, `Game`, `Mission`, the rig, the accent lights, `HeatMeter`, the wave spawners, all dialogue players), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred**, mirroring `EnemyArtWirer.cs` and `CrowdArtWirer.cs`, both of which already open shipped scenes in place, mutate them idempotently, and `SaveScene`.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for this chapter either.** No dock gantry, bazaar stall, relay column, cave pillar, or trophy crate. See Appendix B for the full inventory.

A builder that instantiates from an empty registry produces **an empty room** — the first run of the refactored builder would destroy Chapter 4.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_CavePillar);
if (prefab == null) {
    Debug.LogWarning($"[Ch4] {ArtKey.Props_CavePillar} unresolved — primitive fallback.");
    BuildCavePillarPrimitive(deepworks, pos);   // Appendix A geometry
} else {
    InstantiateAt(prefab, deepworks, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No recorded greybox baseline exists for Chapter 4** in `Project/Docs/CHAPTER-BUILD-LEDGER.md` as of this writing (Ch1's entry — drawCalls 189 / setPassCalls 17 / tris 9,198 / verts 13,092 — has no Ch4 counterpart). This is a real gap, not an omission in this document: **capture a `UnityStats` reading on the next fresh rebuild before any prefab swap-in**, so future swaps have something to regress against. Chapter 4 is a substantially larger scene than Chapter 1 (a single ~136 m linear run across five zones vs. Chapter 1's ~46 m across four — see §2), plus two active `EnemyWaveSpawner`s, five `ScanDroneVolume`s, and a wrist-anchored `HeatMeter` UI quad that all run every frame; expect a materially higher baseline than Ch1's.
- Set-dressing props here are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock-style shared-material batching), same as every other chapter. **Prefabs replacing them must carry their own materials and will not batch this way** — re-measure after every prefab lands.

## 2. Chapter spatial map

Chapter 4 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch04_OverseersHunt.unity`, laid out as **five zones strung along a single linear +Z run** — no branching, no vertical stacking, no returning to an earlier zone except by walking back down the same line. The player spawns at z=2 (inside the Throat) and the story pushes them monotonically toward z≈132 (the back wall of Kerrax's Hold). At **136 m end to end this is roughly three times the length of Chapter 1's four-room corridor (~46 m)** — Drovis is a city, not a ship.

```
 -Z                                                                                                                      +Z
 The Throat ──(open,w14)── The Sink ──(open,w18)── The Mast ──(open,w10)── The Deepworks (open cave, no walls) ──(open,w14)── Kerrax's Hold
  x[-7,7] z[-4,16]         x[-9,9] z[16,42]        x[-5,5] z[42,60]        x[-8,8] z[60,112]                               x[-7,7] z[112,132]
  center (0,0,6), 14x20    center (0,0,29), 18x26  center (0,0,51), 10x18  center (0,0,86), 16x52, no walls/ceiling        center (0,0,122), 14x20, dead end
```

| Beat/Segment | Zone | Footprint | Floor center / size |
|---|---|---|---|
| 0–1 | The Throat (dock arrival, spawn) | x[-7,7], z[-4,16] | center (0,0,6), 14×20 |
| 2 | The Sink (black-market bazaar) | x[-9,9], z[16,42] | center (0,0,29), 18×26 |
| 3 | The Mast (relay chamber) | x[-5,5], z[42,60] | center (0,0,51), 10×18 |
| 4 | The Deepworks (flooded cave) | x[-8,8], z[60,112] | center (0,0,86), 16×52, no walls/ceiling |
| 5 | Kerrax's Hold (syndicate keep) | x[-7,7], z[112,132] | center (0,0,122), 14×20, dead end |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, same as every chapter. The Deepworks has no ceiling at all — it is an open-sky/open-cave volume, the one room in the chapter without one.

**Floor Y-invariant:** every walkable surface in this chapter sits at **world Y = 0** — the Throat/Sink/Mast/Kerrax's Hold floors (`BuildFloorCeiling`, floor top at `center.y - 0.1 + 0.1 = 0`) and the Deepworks cave floor (a bespoke cube at local Y=-0.1, scale Y=0.2, top surface at Y=0) all agree. This is a single flat floor for the whole chapter, exactly as in Chapter 1 — there is no vertical drop in the geometry despite the "descent" framing; see §4, Beat 4.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly. Note the width mismatches at two boundaries — the Throat (14 m) opens into the wider Sink (18 m) at z=16, and the Sink narrows into the Mast (10 m) at z=42 — each room only walls its own width, so the boundary reads as an architectural step rather than a uniform corridor; this is consistent with "the free-port's mouth" opening into "a churning bazaar," and should not be corrected to a single uniform width when patching this scene.

**This widening cuts against canon's own description of the Sink as tighter, not looser.** SEGMENT 2 of the dialogue script calls the Sink "Lower and tighter than the Throat... harder to see across," but the shell went the other way — 18 m is the widest footprint in the chapter, at the same uniform `RoomH` = 3.6 as every walled room, so as bare geometry the Sink reads *more* open than the Throat, not less. "Lower, tighter, harder to see across" cannot come from the shell; it has to be delivered by dressing — low-hung canopies, sightline-breaking stall clutter, and a denser crowd (§4 Beat 2 §a/§c). The tighter read is a density/occlusion job, not a footprint job, which also reinforces §3's recommendation to weight the Sink's decorative crowd against the Throat's.

**There are no doors, sliding or otherwise, anywhere in Chapter 4.** Unlike Chapter 1's three `ProximityDoor`-driven `SlidingDoor` instances (each with a lock state resolved by a mission-spine Trigger step), every zone boundary in this chapter is a `BuildDoorwayWall` — a fixed wall segment with a lintel over a permanently open gap. There is no registry key for a door in this chapter and no lock-state row to author:

| Boundary | Position | Aperture width | State |
|---|---|---|---|
| Throat → Sink | (0, RoomH/2, 16) | 3 m (Throat side) / 3 m (Sink side) | always open, no controller |
| Sink → Mast | (0, RoomH/2, 42) | 3 m (Sink side) / 3 m (Mast side) | always open, no controller |
| Mast → Deepworks | (0, RoomH/2, 60) | 3 m (Mast side; Deepworks has no wall to match) | always open, no controller |
| Deepworks → Kerrax's Hold | (0, RoomH/2, 112) | 4 m (Kerrax's Hold side; Deepworks has no wall to match) | always open, no controller |

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body), spawning at world (0, 0, 2) — inside the Throat, just past its z=-4 front wall — plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring — though see §3.2/§9 for a real gap in that "no extra wiring": its optional `missionDialogue` anti-overlap field is never set). `ZoneBounds` is set to **center (0, 0, 64), radius 90** — one bounding sphere loosely enclosing the entire Throat-to-Kerrax's-Hold run (z ≈ -26 to 154).

**Spawn facing is assumed to be +Z** (toward the Sink, the chapter's whole line of travel) — `BuildRig` does not configure a distinct spawn orientation, but several sensory/blocking claims elsewhere in this document silently depend on it: the katana at (2,1,0) reading as "behind and to the right" of spawn and requiring the player to turn (§4 Beat 0 §b), and the chapter's "monotonic +Z push" framing (above) itself. Stating it here as the invariant those claims rest on.

## 3. Global environment & backdrop

**Chapter 2 was a market that sold people; Chapter 4 is a city that sells everything else, and tonight it is selling him.** The whole chapter unfolds in **Drovis**, a lawless syndicate free-port run by **the Coil**, built down the walls of a canyon packed with grounded, cannibalized shipwrecks — dead capital hulls welded into tenements, salvage stacked into a whole city. Drovis is *neutral ground*: no Dominion garrison, which is exactly why a Program courier on the run comes here to sell and vanish, and exactly why Khall must outsource the hunt to a recovery bounty the Coil is glad to collect.

Per the dialogue script's tone note, the chapter is a **descent out of noise into silence** — from a thousand faces scanning his in the Throat's crowd, down through the tightening bazaar of the Sink, up into the Mast's wind-loud isolation, down again into the total silence of the flooded Deepworks, ending in the still, cold-lit quiet of Kerrax's Hold where "there is no one left to perform for." The lighting, fog, and accent-color progression across §6 is built to carry that arc on its own, independent of any single dialogue line landing correctly.

**As built, that arc is not lighting-only — the decorative crowd also carries it, unevenly.** `PlaceDecorativeCrowd` seeds four NPCs in the Throat and four in the Sink, then none at all in the Mast, Deepworks, or Kerrax's Hold (§4 Beat 1/2 §b) — a 4/4/0/0/0 taper that is a genuine, if unlabeled, spatial realization of "noise into silence," running alongside the lighting arc rather than being purely visual dressing on its own. The taper is flat where it should be steepest, though: the Throat is canonically "a thousand faces," "the roar of a city that runs all night," while the Sink is a tighter, sunk-in bazaar — yet both rooms get the same four bodies. Weighting the Throat denser than the Sink would let the crowd, not just the lighting, descend.

**It is also not actually descending in audio.** The builder places just two ambience beds in the whole chapter: `SinkDreadAmbience` (z=29) and `KerraxHoldAmbience` (z=122), both the same "DreadDrone" clip. The loud opening Throat ("the roar of a city that runs all night"), the wind-loud Mast, and the drip-silent Deepworks all have **no ambience source at all** — see Beat 1/3/4 §f, which lean on "the baseline fog/ambient carries the read," but fog is visual; it cannot be heard. `BuildAmbienceLayer` is already in scope (used twice) and is the natural vehicle for a loud `ThroatCityRoar` bed, a `MastWind` bed, and a sparse Deepworks drip bed that thins toward the bottom — so the descent is actually heard, rather than the loudest room in the chapter being exactly as silent as the "silent" one.

**The one bed that does exist is tonally inverted for its room.** `SinkDreadAmbience` resolves, via `ProceduralAudioClipBuilder`'s name-substring convention ("dread" → `DreadDrone`, §7), to the same dread/horror theme as `KerraxHoldAmbience` — but the Sink is the chapter's loudest, most crowded room: the dialogue script's own stage direction calls for "the roar of haggling under a low welded ceiling" and "chop-shop sparks," not dread. A dread drone is the tonal opposite of a market roar. Recommend a `SinkBazaarRoar` bed (crowd-haggle + welding-spark composite) as the room's baseline; the current `DreadDrone` character may still be defensible as a brief overlay for the moment the snatch-team closes on Tessa, but not as the room's continuous soundscape — see §4 Beat 2 §f.

**Manhunt-as-weather, not manhunt-as-enemy.** Per the dialogue script's intruding-elements note, the ambient manhunt — scan-drones, Coil enforcer patrols, the recovery notice live on every slate — is meant to read as *weather*, not a killable threat. The as-built `HeatMeter`/`ScanDroneVolume` system (§3.2) executes this directly: standing in a drone's cone raises a wrist-worn percentage meter, not a health bar, and only a maxed meter converts the pressure into an actual fight.

**Cast presence.** Kessler, Iris, and Resh are placed once, near the Throat spawn, and are the only "crew" bodies that ever appear physically in this scene — Mira (per canon, sealed aboard the Cairn with the hatch shut) is never instantiated anywhere in the chapter; her one line in Beat 0 plays with no body attached to it, which is *consistent* with her staying behind, not a gap. See §5 and §9 for how far the crew's *physical* presence actually extends across the five zones — it is considerably less than the dialogue's blocking implies.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter4Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch4Environment.asset`. Its schema is identical in shape to Chapter 1's (§1.3 of that document):

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` | `Color` | every `BuildFloorCeiling` call |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` per zone |

Unlike Chapter 1 (four distinct per-room floor/ceiling tints), **Chapter 4 uses one uniform floor tint and one uniform ceiling tint across every walled room** (Throat/Sink/Mast/Kerrax's Hold all share `floorColor`/`ceilColor`; the Deepworks cave floor reuses a near-black variant of the same tint). This is a genuine as-built simplification worth preserving intentionally rather than "fixing" into per-room variety — the color *story* here is carried entirely by the accent point lights and fog, not the floor material.

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("DeepworksLight0", seed: 44f)` and `AddAmbientPulse("KerraxLight0", periodSeconds: 6f)` calls with data. Both of these calls apply to exactly **one** light each (not a whole room), unlike Chapter 1 where every room got its own behaviour. Current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored.

### 3.2 The manhunt / heat mechanic (chapter-wide system)

This is Chapter 4's headline gameplay system and has no Chapter 1 equivalent, so it is documented once here rather than repeated per beat.

- **`HeatMeter`** (`Scripts/World/HeatMeter.cs`) lives on the player rig, wrist-anchored to the left hand (`vrRig.LeftHand`, local offset (0, 0.02, 0.06)). It renders a small quad + percent readout that lerps green→red with rising heat. Internally it wraps a pure `HeatLogic`: `gainPerDetection = 0.6`, `decayPerSecond = 0.04`, two thresholds `{0.5, 1.0}`.
- **`ScanDroneVolume`** (on every `BuildShipDrone`-built drone) distance-checks the player's head transform every `Update` (a proximity poll, like `ProximityDoor`, not a physics trigger) and calls `heat.ReportDetection(detectionPerSecond * Time.deltaTime)` while in range, pulsing its own renderer green→red as a diegetic tell with no UI. Five are placed: three in the Throat (`ScanDrone_Throat0/1/2`), two in the Deepworks (`ScanDrone_Deepworks0/1`).
- **Two `EnemyWaveSpawner`s** ("HunterWaveA"/"HunterWaveB") sit armed-but-idle from build time — their trigger radius is a deliberately absurd **5000 m** (per the builder's own comment: *"makes the spawner's own proximity poll pass immediately once armed, so the ambush reads as heat-triggered rather than position-triggered"*). `heatMeter.OnThresholdEvent(0)` (50% heat) is wired via `UnityEventTools.AddPersistentListener` straight to `waveASpawner.Begin`; `OnThresholdEvent(1)` (100% heat, "maxed") to `waveBSpawner.Begin`.
  - **Wave A** (2 enemies at (-4,0,12) and (4,0,13), inside the Throat near its z=16 exit) is the first-tier ambush, reachable from crowd exposure alone in the Throat/early Sink.
  - **Wave B** (3 enemies at (-3,0,66), (3,0,67), (0,0,70), just inside the Deepworks entrance) is the maxed-heat ambush — the chapter's harshest punishment for staying "hot" too long, landing right as the player commits to the cave descent.
- Bleeding heat down (crowd density / breaking line of sight, per the design doc) is not separately modeled by any component — the meter's own `decayPerSecond` constant is the entire "lose them in the crowd" mechanic. There is no crowd-density detector; the `PlaceDecorativeCrowd` NPCs dressing the Throat and Sink are purely visual and do not interact with `HeatMeter` in any way.
- **Feedback is visual-only — a real gap for a wrist-anchored meter.** Verified against source: `HeatMeter.cs` invokes only `onThreshold[]` `UnityEvent`s (wired to the two wave spawners) — no `Haptics`, no `AudioSource` anywhere in the component. `ScanDroneVolume.cs` has no haptic or audio path at all; its only tell is the `telegraphRenderer` green→red tint on the drone body itself. A player who isn't looking at their left wrist, and isn't facing a drone, gets **no felt or heard signal** that heat is climbing until a hunter wave spawns on top of them. **Recommended, additive fix (not yet built):** a short `Haptics` pulse on the left controller the first instant a `ScanDroneVolume` catches the player, plus a subtle rising haptic/audio cue tied to the gradual pre-threshold heat climb itself (`HeatLogic`'s continuous 0→1 value, not just its two threshold events). **Narrow this to the two channels that are actually silent — the pre-threshold climb and drone-cone entry — not the threshold crossing itself:** `EnemyWaveSpawner`'s `waveSting` (`WaveAlarm.wav`, §7) already fires an audible cue on both `Begin()` calls, which are wired directly to `onThreshold(0)`/`onThreshold(1)` (above), so a threshold crossing is already heard, simultaneously with the ambush it triggers; a second, redundant `AudioDirector` sting at that same instant would add nothing. This is still the single largest immersion gap in the chapter's headline mechanic — every §f below asserts combat feel "comes from Haptics," but for the heat climb and its detectors that claim is currently untrue. A complementary, visual half of the same fix is recommended at §4 Beat 1 §c: giving `Vfx.ScanDroneBody` a visible downward scan-grid/light-cone projector (the screenplay's own "pale grid across passing faces") turns the detection volume itself into something the player can see and step out of, rather than relying on haptics/audio alone to cover for an invisible trigger volume.
- **The canon-intended audio channel for this already exists, unauthored — and it isn't `DialoguePlayer`, it's `EchoPresence`.** The dialogue script's Beat 1 PLAYABLE note — the same note that tutorializes the heat mechanic (§4 Beat 1 §a) — specifies it directly: "Echo may surface short ambient flags ('Drone, your three.' 'Crowd's thicker left, lose them in it.') as systemic barks, heard by Ronin-7 alone." These lines are not among the twelve authored `Chapter4Lines` sets (§7), and they should not be — a fixed-position `DialoguePlayer` set is the wrong vehicle for a dwell/proximity-triggered bark. `EchoPresence` (`Scripts/World/Story/EchoPresence.cs`) is the codebase's existing idiom for exactly this: it renders a head-locked, cyan, player-only subtitle (distinct from `DialoguePlayer`'s white panel), cooldown-gated (`cooldownSeconds = 20f`), sourced from `EchoLines.PoolsFor(CampaignState.HasFlag)` — pools keyed by event kind (`enemy_killed`, `player_hurt`, `ability_activated`, `idle_hint` today). It is already on the Ch4 rig (`Chapter4Builder.cs:168`, `rig.AddComponent<EchoPresence>()`) and is already *live* in this chapter — `EchoLines` un-silences at `ch3_complete`, which Ch4 assumes is set (§8). The concrete, cheap realization of the manhunt-bark note is therefore to **add a `drone_detected` (or similar) event kind to `EchoLines` plus a heat-climb pool, and have `ScanDroneVolume`/`HeatMeter` raise it through `EchoPresence`** — extending a shipped component with one new event kind, not inventing a new dwell/proximity system or reaching for the `ch4_beat4_descent_calls` `DialoguePlayer` idiom (§4 Beat 4 §e), which is fixed-position and un-cooldown-gated, the wrong shape for a repeating ambient tell. Wiring this turns the `Haptics`-only recommendation above from an invented addition into "author the audio the script already specified, through the component built for it," and is what would make the manhunt tutorial actually audible, not just felt.
- **A live wiring gap this surfaces: `EchoPresence.missionDialogue` is never set by `Chapter4Builder.cs`.** That field is `EchoPresence`'s own anti-overlap guard — `TrySpeak` no-ops while `missionDialogue.IsPlaying` — and it exists precisely so Echo's ambient voice never talks over a scripted conversation. The builder never assigns it (confirmed: no `missionDialogue` reference anywhere in `Chapter4Builder.cs`), and `EchoPresence` fires on enemy kills regardless of what else is happening. `HunterWaveA`/`HunterWaveB` can spawn at any heat-threshold moment, including mid-set — killing one of those enemies while `ch4_beat1_throat`, `ch4_beat3_khall`, or any of the other eleven `Chapter4Lines` sets is playing lets Echo's `enemy_killed` bark speak over it. In a chapter whose fiction is "Echo is the only voice in his ear," two Echo-adjacent voices overlapping is a specific, noticeable immersion break — worth flagging even though the field itself can only reference one `DialoguePlayer`, so it cannot cleanly suppress against all twelve sets at once (a chapter-wide "is any dialogue currently playing" flag would be the complete fix; wiring it to any single set is a partial one). See §9.

## 4. Per-beat scene spec

The chapter plays as six beats along the linear +Z run — Beat 0 (the Cairn briefing) through Beat 5 (Kerrax's Hold) — matching the six `Beat0:`…`Beat5:` label prefixes actually authored on the `MissionDirector.steps` array. Each beat is documented with the same a–f structure used for Chapter 1.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the Y-invariant.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the asset is on disk today.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes, cylinders, or hardcoded sizes for any props. You must read from the centralized `ArtAssetRegistry` ScriptableObject for all environment prefabs. Create separate methods: **`BuildBeat0Art()`** for static environment/prefabs, and **`BuildBeat0Logic()`** for the crew's spawn positions and the briefing dialogue.

#### a. Narrative purpose & emotional target

The last quiet before the city becomes the net. Per the dialogue script, this is Kessler's command room, "more of it lit than a chapter ago" — a salvaged holo-plate throws Drovis up in projection while the crew, "four and a child now, still new to each other," sets the job. Resh reads Drovis like a port he half-knows; Iris names the prize (Tessa Rin's Program internals); Kessler weighs the ship and the child against the lead and orders Mira sealed aboard with the comm open; Echo, heard by Ronin-7 alone, flags the walk into a market that buys faces as its own kind of bait. The beat closes on Ronin-7 turning a briefing into a heading: *"Then we reach her first. Take us down."*

Mira's one line here (*"Is it like Velorum. The loud part."*) is the only trace of her in the whole chapter — she never leaves the ship, and correctly, never appears as a body anywhere in this scene.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at world (0, 0, 2) — see §2. `BuildRig(addLocomotion: true)` + `EchoPresence` + `ZoneBounds` center (0,0,64) radius 90 (authored once, chapter-wide). The player does not move during this beat; it plays out as a stationary huddle before the crowd/manhunt gameplay begins.
- **Crew spawn (shared with every later beat — see §5):** Kessler at (-1.5, 0, 3), Iris at (1.5, 0, 3), Resh at (0, 0, 5), all with `StoryNpc` + `StoryNpcWander` (radius 0.6 / 0.6 / 0.8 respectively). **No separate "Cairn command room" geometry is built anywhere in the chapter** — the briefing dialogue plays with the crew standing at their permanent Throat-spawn positions, inside the same room that will read as the dock a moment later. This is a real as-built compression of the screenplay's INT. THE CAIRN — COMMAND ROOM scene into a dialogue-only beat layered on top of Throat geometry; see §9.
- **The katana ("Echo"):** built via `BuildSword` at (2, 1, 0), Euler(-90, 0, 0), grabbable — the player already possesses the blade at the start of the chapter, unlike Chapter 1 where its discovery was itself a story beat. It sits **~3 m from spawn (0,0,2), behind and to the right** — a few steps away, not literally within arm's reach; the player must turn to find it. **Deliberate divergence, same pattern as §c's Cairn-room compression and §e's dropped lines:** the screenplay keeps the blade "wrapped and slung" from the Cairn all the way to Kerrax's Hold, unwrapping it only at the duel's thesis moment ("drawn now, lowered"); as built the katana is fully drawn and grabbable from spawn, since the game has no wrapped-prop/unwrap-gesture state to represent the screenplay's staged reveal. Flagging this as a knowing cut rather than an oversight, so a future pass considering a wrapped-blade prop + unwrap gesture at Beat 5 knows the divergence was already noted here.

**Mission-spine step:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` — the crew council; blocks further progress until it finishes |

**What changes during the beat:** nothing in the geometry. The state change is entirely narrative: the player leaves this beat already knowing the destination (Drovis/the Sink) and already holding the sword.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

No dedicated Beat 0 geometry exists — this beat reuses the Throat's art wholesale (built in Beat 1's `BuildBeat1Art()`). The only object that is meaningfully "Beat 0's" is the dialogue player and the crew's initial placement, both of which are logic, not art. If a future pass gives Beat 0 its own command-room set (per the screenplay's holo-plate/battered console tier staging), that geometry belongs here, parented under `[STATIC_ART_DO_NOT_DELETE]`, and should not be folded into the Throat's art method.

**Recommended, cheap addition: a holo-plate projection of Drovis at the crew huddle.** The full command-room set is a legitimate future-pass scope, but the one prop that *makes* this beat — "a salvaged holo-plate throws Drovis up in projection... a canyon city of grounded, cannibalized hulls" — is specific and does not require it. `BuildHologram` is already in scope (used at (0,0,58) for Khall's Beat 3 relay voice, §4 Beat 3 §c); the same idiom, reused here for a canyon-city projection at roughly the crew huddle (~(0, 0.5, 4)), gives the briefing its defining image and a concrete visual anchor for Resh "reading the port off the plate" — without committing to a full command-room set this pass.

**Wire the deactivation to `dlgBriefing`'s own completion event, not a new mission-spine step.** Steps 0 and 1 are both `Dialogue` steps with nothing between them (§b above, §4 Beat 1 §b) — inserting a dedicated `Trigger: Deactivate Holo-Plate` step would push `steps.arraySize` from 23 to 24 and reindex every downstream step, and every §-table index in this document that references one. The cheap, additive path is a persistent listener on `Dialogue_Beat0_Briefing`'s own completion callback calling `holoPlateGo.SetActive(false)` — the same object `MissionDirector` already uses to gate progress out of Beat 0 — so the deactivation rides an existing event instead of rippling the mission spine.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Katana "Echo" | (2, 1, 0), Euler(-90,0,0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| *(recommended addition — Drovis canyon-city holo-plate, `BuildHologram` idiom, deactivated via `dlgBriefing`'s completion event, not a new step)* | ~(0, 0.5, 4), at the crew huddle | `Vfx.HologramProjection` | *(same shared primitive as the Mast's Khall hologram, §4 Beat 3 §c)* | **MISSING** |

#### d. Combat

None.

#### e. Dialogue / VO

**`ch4_beat0_briefing`** (`Dialogue_Beat0_Briefing`), position (0, 1, 1), 7 lines — Resh names Drovis and the Coil; Iris names Tessa Rin and the stakes ("if anything names who cut your leash, it's in what she's carrying"); Kessler weighs neutral ground's double edge; Echo flags the bait, addressing Ronin-7 as "Cipher" for the first time in the chapter (the crew has not adopted the name yet — that happens in Beat 3); Mira asks her one question; Kessler answers and seals her aboard; Ronin-7 closes the council. Condensed from the script: `Chapter4Lines.cs` deliberately drops Resh's second scripted line ("Then we move quiet, reach her before the Coil does...") and, in the following Beat 1 set, Iris's scripted "recovery notice" line ("A recovery notice. Somebody priced you, and a port like this runs it free...") — both present in `Ch04_The_Overseers_Hunt_Dialogue_Script.md` but absent from the code, consistent with this document's practice elsewhere of noting code-vs-script deltas.

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** Nothing in this beat calls for combat feedback of any kind.
- No dedicated ambience is built specifically for Beat 0 — the Throat's own accent lights and fog (§4, Beat 1) are already present since no separate room exists for this beat.
- Comfort vignette is inert; the player does not move.

---

### Beat 1 — The Throat (Grounding + Ambient Manhunt)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (dock shell, crowd, scan-drone visuals) and **`BuildBeat1Logic()`** (the three Throat `ScanDroneVolume`s, the heat meter wiring, dialogue).

#### a. Narrative purpose & emotional target

The crew's first read of Drovis: "smoke, sodium glare, hanging cargo, the roar of a city that runs all night." A scan-drone throws a pale grid across the crowd; a Coil enforcer thumbs a slate showing Ronin-7's own recovery notice. Echo names the scale of it flatly ("Whole city's been handed your picture"), and Ronin-7's answer is procedural, not alarmed: keep moving, don't look up. The beat's real payload is the watcher at the rail — a figure who studies him a beat too long, doesn't check a slate, and vanishes: the first, unnamed glimpse of Mera Voss, seeded here and paid off in Beat 5. Ronin-7 clocks the difference between a hired enforcer and someone "committed" and files it without alarm, keeping the crew moving toward the courier.

Design intent (per the game-narrative-design notes): this is where the heat/wanted meter is tutorialized — scan-drone cones and patrol line-of-sight raise heat, crowd density and broken sightlines bleed it down, maxed heat triggers a Coil ambush. It is the chapter's title mechanic, introduced here and recurring through the Sink and the Deepworks.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Scan-drones** (`Ch4BuildScanDrone`, §3.2): `ScanDrone_Throat0` (-3, 2.4, 4), `ScanDrone_Throat1` (3, 2.6, 10), `ScanDrone_Throat2` (0, 2.5, 14) — radius 6/6/5 m, detection rate 0.2/0.2/0.2 per second. Together they cover most of the Throat's floor.
- **Decorative crowd:** `PlaceDecorativeCrowd` at (-4,0,3), (4,0,5), (-3,0,11), (3,0,12) — up to four generic NPCs pulled from `Data/CharacterSpecs/Decorative/*.json`, skipping any named "Kessler"/"Khall"/"Iris". Purely visual dressing; see §3.2 for why they do not interact with the heat system.
- **`HunterWaveA`'s two enemies** are pre-placed at (-4,0,12) and (4,0,13) — inside this room, near its exit — but built `SetActive(false)` and never touched by any step in this beat; they surface only if heat crosses 50% anywhere in the Throat or early Sink (§3.2).
- **Player:** free-roam continuous locomotion from spawn (0,0,2) toward the z=16 opening into the Sink. No reach-point gate exists for leaving the Throat — the very next mission step is a `ReachTrigger` for the *Sink*, not for exiting the Throat, so the player can dawdle here indefinitely without penalty beyond rising heat.
- **Missing: a physical anchor for "the watcher at the rail."** Echo's lines here ("Rail. High left. The one not buying anything") and their Beat 5 callback (`ch4_beat5_mercy`'s "The rail. The one who already knew your face") both point at a rail and a figure that the builder never places — the Throat is a flat room with no elevated rail geometry and no watcher body; Mera Voss is only instantiated later, inactive, in Kerrax's Hold (-5,0,118). The seed→payoff sightline this beat's emotional target (§a) depends on is currently dialogue-only. See §c for the recommended fix and §9 for the flag.

**Mission-spine step:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Throat` — plays as the crowd/drone/watcher beat resolves |

**What changes during the beat:** nothing structural. The only state change is heat accumulation, which is continuous and player-exposure-driven rather than tied to this beat's own step.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (14×20, walls + floor + ceiling) | center (0,0,6) | `Rooms.ThroatShell` | `…/Art/Generated/Rooms/ThroatShell.prefab` | **MISSING** |
| `Throat_WallFront` (solid, no doorway — the shuttle has already lifted off) | (0, RoomH/2, -4) | `Rooms.ThroatShell` (part of shell) | — | **MISSING** |
| `Throat_WallBack` (open archway to the Sink, gap 3m) | (0, RoomH/2, 16) | `Rooms.ThroatArch` | `…/Art/Generated/Rooms/ThroatArch.prefab` | **MISSING** |
| Room detail cluster (crates, console, pipe, signage) | center (0,0,6), accent tint (0.4,0.35,0.5) | `Props.DockDetailKit` | `…/Art/Generated/Props/DockDetailKit.prefab` | **MISSING** |
| *(recommended addition to the same kit — hanging cargo + gantry structure)* | overhead, within `RoomH` = 3.6 m | `Props.DockDetailKit` | *(same prefab, extended commission)* | **MISSING** |
| *(recommended addition to the same kit — a rail/gantry catwalk platform, the watcher's literal stand)* | elevated, **−x** side, near the z=16 back wall, walkway top ~y=2 | `Props.DockDetailKit` | *(same prefab, extended commission)* | **MISSING** |
| *(recommended addition — a recovery-notice holo-slate/signage prop showing Cipher's own face)* | near the z=16 chokepoint, wall- or standmounted | `Props.DockDetailKit` | *(same prefab, extended commission)* | **MISSING** |
| *(recommended addition — a decorative enforcer figure thumbing a slate at the chokepoint)* | near the z=16 chokepoint, alongside the recovery-notice prop | `Diversity.*` (per-spec, same idiom as the decorative crowd) | *(baked from `Data/CharacterSpecs/Decorative`)* | **MISSING** |
| *(recommended addition — inactive decorative watcher figure, seeds Mera Voss, standing on the catwalk above)* | on the catwalk, −x side near z=16 | `Diversity.*` (per-spec, same idiom as the decorative crowd) | *(baked from `Data/CharacterSpecs/Decorative`)* | **MISSING** |
| Decorative crowd (up to 4) | (-4,0,3), (4,0,5), (-3,0,11), (3,0,12) | `Diversity.*` (per-spec, baked from `Data/CharacterSpecs/Decorative`) | `Assets/Ronin7/Prefabs/Art/Generated/*.prefab` | **conditional — EXISTS only for baked specs** |
| Scan-drone visual ×3 | see logic positions | `Vfx.ScanDroneBody` | `…/Art/Generated/VFX/ScanDroneBody.prefab` (currently `BuildShipDrone` primitive; art-wired post-build by `EnemyArtWirer` to `Dominion_Scan-Drone.prefab` on odd-indexed `Enemy` bodies only — **not** on these `ScanDroneVolume` drones, which are never `Enemy` components; see §9) | **MISSING** as a registry entry |
| *(recommended addition to the same commission — a downward light-cone / scan-grid projector)* | beneath each drone body, angled floorward | `Vfx.ScanDroneBody` | *(same prefab, extended commission)* | **MISSING** |
| `ThroatLight0` / `ThroatLight1` | (-4,2.6,4) / (4,2.6,10) | — | `ChapterEnvironmentProfile.accentLights["Throat0"/"Throat1"]` | profile |
| Katana "Echo" | (2, 1, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** *(placed once, spans Beats 0–1)* |

**`Vfx.ScanDroneBody`'s commission should include the drone's defining tell, not just its own hull.** As built, `ScanDroneVolume`'s only visible feedback is a green→red `RendererTint` on the drone body itself (§3.2) — legible only if the player is already looking at the drone. The screenplay's defining image is the drone "throwing a pale grid across passing faces," a projected floor/face-level scan pattern, not a color change on a small flying prop. **Recommended addition:** commission `Vfx.ScanDroneBody` to include a downward light-cone / scan-grid projector, so the detection volume reads as a visible cone the player can see on the floor and physically step out of. Beyond immersion, this directly softens the "no seen/felt/heard heat signal" gap §3.2 flags as the chapter's largest immersion problem — a cone underfoot is legible in VR in a way a tint on a drone behind the player's head never is, and it turns "break line of sight / lose them in the crowd" into a spatial decision instead of an invisible meter tick.

**`Props.DockDetailKit`'s commission should specify overhead dressing, not just floor-level clutter.** The kit as named is "crates, console, pipe, signage" — all ground-level. The script's Throat is defined by verticality instead: "hanging cargo," "gantries bolted into dead hulls," a wreck-canyon rising "on every side." With `RoomH` = 3.6 m there is headroom for hung silhouettes; overhead clutter is what makes the Throat read as a canyon "mouth" rather than a flat box, and gives the scan-drones (which drift at y=2.4–2.6, §3.2) something to pass beneath.

**The screenplay's kinetic arrival is elided into a cold start — the same deliberate-omission pattern as Beat 0's Cairn command-room (§4 Beat 0 §c).** The script opens Beat 1 with "the shuttle's ramp drops into noise and smoke," the crew descending mid-motion; as built, `Throat_WallFront` is a solid wall ("the shuttle has already lifted off," above) and the player simply starts grounded — no ramp, no descent, no set-piece for the drop itself. Noting this so a future pass considering a shuttle/ramp arrival set-piece knows it was a deliberate cut for this pass, not an oversight.

**This fix needs a literal platform, not just a figure — the Throat is flat Y=0 end to end (§2), and nothing built anywhere in this chapter is elevated.** Canon is explicit that the watcher stands on "a high rail above the press"; a figure placed at an "elevated Throat position" with nothing under it would simply hover. Fold a rail/gantry catwalk into `Props.DockDetailKit`'s commission (above) — a walkway at roughly y=2, on the **−x** side near the z=16 back wall — as the watcher's literal stand.

**Recommended addition (not yet built):** an inactive, decorative watcher figure standing on that catwalk — e.g. near the z=16 back wall, offset **−x** for "high left" — that deactivates as the player advances past it, mirroring Mera Voss's own "activate/deactivate in place" idiom (§5) used for her Beat 5 reveal. **The offset must be −x, not +x:** spawn facing is +Z (§2), so the player's left hand is toward −X, matching Echo's exact line "Rail. High left." It also has to agree with the payoff — Mera Voss's own inactive spawn is at (−5,0,118), i.e. −X, so "the rail" Beat 5 callback (§9) already lives on the left side of the room. Placing the seed figure at +x would put the watcher on the *right*, contradicting both the line and Mera's own position. This gives the Beat 1 seed and the Beat 5 "the rail" callback (§9) something the player actually saw, rather than a sightline that exists only in dialogue.

**Missing: a physical recovery notice, and a face-checking enforcer.** This is the single most literal expression of "manhunt-as-weather" (§3), named in both the screenplay ("a Coil enforcer thumbs a slate showing Ronin-7's recovery notice") and Echo's own Beat 1 line ("That's your face on his slate. And on the drone."), but nothing in `BuildRoomDetails`/`Props.DockDetailKit` places it — the commission is generic "crates, console, pipe, signage," and the decorative crowd (§b) is generic pass-through NPCs with no distinguishing prop. **Recommended addition:** a recovery-notice signage/holo-slate prop at the z=16 chokepoint showing Cipher's own likeness, plus at least one decorative figure staged holding/thumbing a slate beside it. This diegetically tutorializes the heat mechanic the beat exists to introduce, instead of leaving "the whole city has your picture" as a spoken abstraction with nothing to look at.

**One clause on the likeness asset itself, since the project has no in-game player-avatar face to source it from (§2).** The player rig is head + two hands with no visible body anywhere in the project — there is nothing to capture a face from. The slate's likeness has to come from Ronin-7's character-art source instead: the Tripo `Named` pipeline's Cipher/Ronin-7 reference, or promotional art, not a screenshot of the faceless rig — so the commission is unambiguous and the prop doesn't ship blank.

#### d. Combat

None scripted for this beat specifically — the two `HunterWaveA` enemies are pre-placed here but only activate on the heat threshold event (§3.2), which can fire at any point from here through the early Sink.

#### e. Dialogue / VO

**`ch4_beat1_throat`** (`Dialogue_Beat1_Throat`), position (0, 1, 8), 9 lines — Echo names the scale of the manhunt; Ronin-7 sets the pace; Resh reads the choke-point setup; Echo spots the watcher at the rail; Ronin-7 marks it without alarm; Kessler asks if it's an enforcer and Ronin-7 corrects him — "that one chose the rail," the first tell that this isn't a generic bounty hunter.

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** The scan-drone telegraph (idle-gray → pulsing-red on detection) is the beat's only "threat" feedback, and it is a `RendererTint` pulse on the drone itself, never a camera effect.
- No dedicated ambience source is built for the Throat specifically; the chapter's baseline fog/ambient (§Appendix A.1) carries the "smoke, sodium glare" read visually, but not audibly — see §3 for the missing `ThroatCityRoar` bed the "roar of a city that runs all night" line promises.
- **No haptic or audio tell on heat gain or drone-cone entry** (§3.2) — the player's only warning that a `ScanDroneVolume` has caught them is the drone's own renderer tinting, which requires facing it. This is the beat that tutorializes the heat mechanic, which makes the missing felt/heard channel most acute here — the gap is specifically the climb and the drone-cone entry; the threshold crossing itself already gets an audible cue from `EnemyWaveSpawner`'s wave alarm (§3.2/§7). The dialogue script already names the intended channel — Echo's systemic barks ("Drone, your three." "Crowd's thicker left, lose them in it.") heard by Ronin-7 alone (§3.2) — and the concrete vehicle for it is `EchoPresence`, already on the rig (§3.2): a new event kind + `EchoLines` pool, raised by `ScanDroneVolume`/`HeatMeter`, not a new system. Filling this gap is a wiring task, not an invented one.
- Comfort vignette engages normally on the player's first walking/snap-turning in the chapter.

---

### Beat 2 — The Sink (Tessa Rin + the Sabotage + Snatch-Team Skirmish)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (bazaar stalls, Tessa's fence-stall, room detail) and **`BuildBeat2Logic()`** (Tessa's spawn, the Sink reach point, the snatch-team `DefeatEnemies` step, both dialogue sets).

#### a. Narrative purpose & emotional target

Lower, tighter, louder than the Throat — "a churning bazaar sunk into the canyon's mid-tier... stacked stalls, fences, chop-shops." The crew reaches courier Tessa Rin mid-panic; the screenplay stages a **four**-strong Coil snatch-team closing on her at the same time ("Four Coil enforcers in salvage-plate push through") and Echo's line names the count directly — *"That's the team. Four."* — but the builder spawns only **three** `BuildEnemy` instances for this fight (§b, §d). The fight is the chapter's first true combat regardless (a bazaar skirmish through stall cover, non-lethal-capable enforcers — the lethality is the player's choice, not the script's), but a player who counts hears four and fights three; see §9 for the recommended fix. The real reveal lands *after* the fight: Iris cracks the data and finds Ronin-7's own termination record rewritten from the inside — the killswitch wasn't a malfunction, it was **sabotage**, someone inside the Program broke his leash on purpose. Echo's confession ("I had a front-row seat to your execution, Cipher, and I missed that") lands the emotional gut-punch: even his closest witness didn't know. The beat's exit line turns the shock into a heading — trace the relay on the Mast back to whoever is running the bounty.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **`SinkReachPoint`** at (0, 1, 20), radius 5 m — gates the sabotage arc behind the player physically reaching the bazaar's interior.
- **Tessa Rin:** `Ch4PlaceStoryNpc` at (3, 0, 32), `wanderRadius: 0` (stationary), positioned at her own fence-stall. She has **no exit mechanism** anywhere in the builder — no `SetActive(false)`, no walker — despite the screenplay's "slips into the churn of the Sink and is gone." She remains visible, standing at her stall, for the rest of the chapter; see §9.
- **Sink snatch-team:** three `BuildEnemy` instances at (-3,0,30), (3,0,30), (0,0,34), built `SetActive(false)` — **three bodies, not the four Echo's line and the screenplay both name** (§a, §9). Unlike Chapter 1's `DefeatEnemies` step (which waits on already-active troopers), this step's own activation *is* the trigger — `MissionDirector.BeginDefeatEnemies` sets each `Health`'s GameObject active when the step is reached, exactly mirroring Chapter 2's Auction fight; no separate `Trigger` step is authored for it.
- **Same "enemy announced in VO before its body exists" idiom as Kerrax's confront line (§4 Beat 5 §b), milder here.** Step 3 (`ch4_beat2_tessa_meet`) has Echo call out the snatch-team ("That's the team. Four.") while all three bodies are still inactive; they only appear at step 4's `DefeatEnemies` activation. More forgivable than the Kerrax case, since the screenplay itself has them "push through the crowd" mid-line rather than standing revealed from the start — but it is the same structural gap, worth being aware of if either is patched.
- **`HunterWaveA`'s two enemies** (see Beat 1) remain armed and can still fire here if heat crosses 50% while the player lingers in the Sink.
- **Decorative crowd:** a second `PlaceDecorativeCrowd` call at (-5,0,20), (5,0,22), (-6,0,36), (6,0,38).

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 2 | `ReachTrigger: The Sink` | Gates on `Camera.main` distance to `SinkReachPoint` (0,1,20), radius 5 |
| 3 | `Dialogue: Beat2: Tessa Rin (the meet)` | Plays `ch4_beat2_tessa_meet` in full |
| 4 | `DefeatEnemies: Sink Snatch-Team` | Activates and waits on all 3 snatch-team `Health` components |
| 5 | `Dialogue: Beat2: The Sabotage (the killswitch reveal)` | Plays `ch4_beat2_sabotage` in full |

**What changes during the beat:** the snatch-team goes from inactive to active the instant step 4 is reached (not a separate visible cue); once all three are dead the dialogue resumes automatically.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (18×20, walls + floor + ceiling) | center (0,0,29) | `Rooms.SinkShell` | `…/Art/Generated/Rooms/SinkShell.prefab` | **MISSING** |
| `Sink_WallFront` / `Sink_WallBack` (open archways, gap 3m each) | (0,RoomH/2,16) / (0,RoomH/2,42) | `Rooms.SinkArch` | `…/Art/Generated/Rooms/SinkArch.prefab` | **MISSING** |
| `SinkStall_Counter` ×4 | (-6,0,22), (6,0,24), (-6,0,34), (6,0,36) | `Props.SinkStall_Counter` | `…/Art/Generated/Props/SinkStall_Counter.prefab` | **MISSING** |
| `SinkStall_Canopy` ×4 | 1.4m above each counter | `Props.SinkStall_Canopy` | `…/Art/Generated/Props/SinkStall_Canopy.prefab` | **MISSING** |
| `TessaStall_Counter` | (3, 0.5, 33) | `Props.SinkStall_Counter` *(fence-stall variant)* | `…/Art/Generated/Props/SinkStall_Counter.prefab` | **MISSING** |
| `TessaStall_Screen` | (3, 1.4, 33.5) | `Props.StallScreen` | `…/Art/Generated/Props/StallScreen.prefab` | **MISSING** |
| *(recommended addition — a small grabbable/static data-slate prop, the literal sabotage MacGuffin)* | ~(3, 1, 32.5), at Tessa's stall | `Props.DataSlate` | *(new commission)* | **MISSING** |
| Room detail cluster | center (0,0,29), accent tint (0.5,0.2,0.4) | `Props.BazaarDetailKit` | `…/Art/Generated/Props/BazaarDetailKit.prefab` | **MISSING** |
| *(recommended addition — chop-shop spark emitter, visual half of the audio-only fix at §f)* | near a `SinkStall_Counter`, e.g. (6,0,24) or (-6,0,34) | `Vfx.WeldingSparks` | *(new commission)* | **MISSING** |
| Tessa Rin | (3, 0, 32) | `Named.TessaRin` | `…/Art/Generated/Characters3D/Named/Tessa-Rin.prefab` | **EXISTS** |
| *(recommended addition — a go-bag prop on Tessa's shoulder, her defining courier silhouette)* | on/attached to Tessa Rin at (3,0,32) | `Props.GoBag` | *(new commission)* | **MISSING** |
| Sink snatch-team ×3 | (-3,0,30), (3,0,30), (0,0,34) | `Enemies.CoilGanger` | `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS**, but see §9 — not what actually gets wired |
| `SinkLight0` / `SinkLight1` | (-5,2.4,24) / (5,2.4,34) | — | `ChapterEnvironmentProfile.accentLights["Sink0"/"Sink1"]` | profile |
| `SinkDreadAmbience` | (-5, 2.4, 29) | — | `AudioSource` + `ProximityAmbienceLayer`, procedurally-generated "DreadDrone" theme clip (inner 4m/outer 14m/max vol 0.4) | audio |

**Canon's "chop-shop sparks" has an audio fix recommended below (§f) but no visual counterpart — the same one-sided gap this document already closes for the scan-drone's detection cone (§4 Beat 1 §c).** The dialogue script names sparks twice (the Beat 2 stage direction and its own establishing image), but nothing in `Ch4BuildSinkStalls`/`Props.BazaarDetailKit` places an emitter. **Recommended addition (above):** a cheap looping spark-emitter VFX, `Vfx.WeldingSparks`, at one chop-shop stall — it also doubles as a warm, intermittent flicker light source, giving the room a heat-by-implication read canon calls loud, hot, and cramped.

**`Props.BazaarDetailKit`'s canopy/stall commission should lean into low overhead hang-height and cross-aisle occlusion, not read as a generic cluster.** See §2 for why: the shell widened the Sink to 18 m, the chapter's widest footprint, which cuts against canon's "lower and tighter... harder to see across" — the tighter read has to be a dressing job (low-hung canopies breaking sightlines, denser clutter) since the footprint itself went the other way.

**No physical data-slate prop exists for the sabotage reveal — the literal object the whole beat turns on.** Tessa is introduced "a battered data-slate clutched to her chest" (script line 220); Kessler's authored line is *"get her and the slate behind the plate-stacks"* (§e); the reveal is Iris decrypting that slate. The builder places Tessa's stall counter and screen but no slate and no hand-off object. **Recommended addition (above):** a small `data-slate` prop at Tessa's stall, grabbable or static, so "the slate" everyone names and the killswitch-sabotage MacGuffin are something the player can actually see change hands, rather than the chapter's central plot object being spoken-only.

**Two more characterizing Beat 2 props are unplaced, and the snatch-team's weapon type doesn't match canon.** Tessa is introduced with "a go-bag already on her shoulder" — her defining silhouette as a courier poised to bolt — and the enforcers are staged with "stun-poles up," the cutscene capping the fight on "the fallen poles." Neither prop exists: Tessa has no go-bag (recommended addition, above), and the snatch-team is built as generic sword-melee `BuildEnemy` bodies (`ArmR/Sword/Blade/BladeTip` chain, §d) rather than stun-pole-armed enforcers, so the fallen-poles image the script caps the fight on isn't deliverable with the current greybox weapon. Both are low-priority "true-to-this-chapter" set-dressing flags, not blockers.

#### d. Combat

Player damage output is via `BladeDamager`'s EMA swing-speed model (**existing system — reuse, don't reinvent**). The three snatch-team enemies (one short of the screenplay's/Echo's "four" — §a, §9) use `BuildEnemy` — a generic melee body (capsule greybox, `ArmR/Sword/Blade/BladeTip` chain) sharing the chapter's single default `EnemyDefinition` (maxHealth 60 / damage 12 / moveSpeed 1.4 / attackCooldown 0.8, the same asset every other melee-enemy chapter reuses). The screenplay calls this a "tight bazaar skirmish... stall-cover and choke aisles" and specifies the enforcers as "non-lethal-capable... which keeps the lethality the player's choice, not the script's" — the actual `Enemy`/`BladeDamager` combat loop does not model a distinct non-lethal enforcer variant; it is the same lethal melee AI used everywhere else in the game. Flagging the narrative/mechanic gap for awareness, not proposing a change (mirrors Chapter 1's flagged "melee vs. rifle" trooper note). The same gap extends to the visible weapon: canon stages the enforcers with "stun-poles up" and caps the fight cutscene on "the fallen poles," but the built `Enemy` bodies carry the shared sword chain, not a pole, so that specific image is not currently deliverable as built (§c).

**Combat trigger:** per the screenplay's explicit direction, a non-verbal cue starts the fight — "an enforcer's slate flares red on Ronin-7's face. He flips it to the squad." No distinct combat-trigger component exists for this; it is realized by the `DefeatEnemies` step's own activation (§b).

#### e. Dialogue / VO

**`ch4_beat2_tessa_meet`** (`Dialogue_Beat2_TessaMeet`), position (3, 1, 31), 7 lines — Tessa's bluff, Resh's offer, her recognition of Ronin-7's own bounty, his flat counter-offer, Echo calling the incoming snatch-team, Kessler splitting the crew ("Iris, get her and the slate behind the plate-stacks. Resh, with me.").

**Tessa's recognition has no visual referent as built — the same divergence Beat 0 §b already flags, breaking forward into this beat.** The screenplay stages "That's the face on the bounty" physically: "Tessa's eyes go to the wrapped katana at his hip, then to his face, then very wide" (script line 237) — recognition driven by the blade at his hip. As built the player is a head+hands rig with no body/hip and no face (§2), and the katana is a free grabbable the player may have left anywhere, not a hip-slung prop; the drawn-vs-wrapped katana divergence noted at Beat 0 §b is what breaks this specific staging. The line has to carry the recognition unassisted — VO-only, consistent with the chapter's other faceless-player compressions (§4 Beat 0 §c/§e) — not a staging the builder can currently realize.

**`ch4_beat2_sabotage`** (`Dialogue_Beat2_Sabotage`), position (0, 1, 36), 12 lines — Tessa hands off the data and immediately disclaims involvement; Iris identifies the termination record; a short Iris/Ronin-7 exchange ("Hold on. This is wrong." / "Wrong how.") pivots the beat before the reveal lands; Iris names the sabotage itself ("Somebody got inside the firmware and broke it on purpose"); Echo's confession that it missed the tampering; Ronin-7 reframing luck as intent; Kessler's one-word "Who?"; Iris naming the dead end (whoever did it scrubbed themselves from the record too); Tessa's exit line; Resh's parting professional courtesy; Ronin-7 turning the shock into the next objective — the Mast.

Both sets follow the same audit-fixed antithesis rewrites noted in `Chapter4Lines.cs`'s header (four "not X, that's Y" lines thinned per `story ouput/audit/Ch04_audit.md`'s suggested rewrites — Beat1/Resh, Beat2/Ronin-7, Beat3/Kessler, Beat5/Mera Voss).

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point in the skirmish — combat feel is carried entirely by `Haptics`, `AudioDirector` stingers, and the `CombatFeedbackController` reticle.
- `SinkDreadAmbience` runs continuously under both the fight and the sabotage reveal, its "DreadDrone" theme clip inferred from the source object's own name by `ProceduralAudioClipBuilder.AssignGeneratedClips`.
- **This bed is tonally wrong for the room (§3).** The Sink's canon soundscape is crowd-haggle and welding-spark chaos ("the roar of haggling under a low welded ceiling," "chop-shop sparks"), not a horror drone. Recommend re-authoring the bed as `SinkBazaarRoar` for the room's baseline, reserving `DreadDrone`, if kept at all, for the few seconds the snatch-team is actually closing in.
- **The welding-spark composite in that recommended bed has a visual counterpart, not just an audio one — see §4 Beat 2 §c's `Vfx.WeldingSparks` recommendation**, closing the same one-sided gap this document already flags for the scan-drone's detection cone (§4 Beat 1 §c).
- Comfort vignette engages on the frequent snap-turns a tight, multi-attacker bazaar fight demands — standard behavior, not a special case for this beat.
- 90 FPS is the design target for this encounter (3 active `Enemy` AIs + stall geometry + blade VFX); no measured baseline exists yet (§1.6) — this is a natural candidate for the first `UnityStats` capture.

---

### Beat 3 — The Mast (Khall / The Naming) — REVEAL

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (relay chamber shell, the hologram column, the relay drone) and **`BuildBeat3Logic()`** (the Mast reach point, both dialogue sets).

#### a. Narrative purpose & emotional target

A relay spire crowning the wreck-stacks, "high, exposed, wind-loud, strung with cable" — a Coil-tapped Overseer comms mast running the bounty. Under the Coil's own traffic runs a second, older, military signal. Ronin-7 reaches the relay core and the hum resolves into a voice: **Khall**, flat and omnidirectional, who names him "Cipher" before the crew has ever used the word. The whole scene turns on a controlled man's control slipping — "a pause too long for a machine," the tell that Khall is not reading from procedure so much as fighting to stay inside it. Ronin-7 refuses the leash before he even knows whose it is ("That's not my name"), then presses the wound Echo just handed him in Beat 2 (the recognition that this is the same voice from the bay, the handler who pulled his switch and couldn't watch him fall). Khall's composure closes back over the crack, but the plea underneath the order is audible — he wants Cipher *brought in*, not destroyed, and the guilt in that distinction seeds Chapter 8. The crew adopts the name "Cipher" in the beat immediately after — Resh dryly, Kessler warily ("A handler that chases a man he already killed once... I don't trust grief that gets people killed"), Ronin-7 turning it over himself for the first time.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **`MastReachPoint`** at (0, 1, 46), radius 4.5 m.
- **The hologram / Khall's voice:** `BuildHologram(interior, (0, 0, 58))` — a generic cyan projection-column-and-node primitive (`FloatingArrow` for the slow spin/bob shimmer), **not** an instance of the `Named.Khall` prefab. Khall is never given a physical body anywhere in this chapter, which is the correct read for "heard only as a voice through the Mast relay" — but it also means the `Named.Khall.prefab` asset that exists on disk (used in Chapter 1 as a hologram NPC with a likeness) goes entirely unused here. See §9.
- **`RelayDrone`** — a `BuildShipDrone` instance at (1.5, 2.4, 55), tint (0.5,0.7,1), purely decorative (the Coil's tap on the relay), unrelated to the `HeatMeter`/`ScanDroneVolume` system.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 6 | `ReachTrigger: The Mast` | Gates on `Camera.main` distance to `MastReachPoint` (0,1,46), radius 4.5 |
| 7 | `Dialogue: Beat3: Khall (the naming, Cipher)` | Plays `ch4_beat3_khall` in full |
| 8 | `Dialogue: Beat3: Aftermath (the crew adopts Cipher)` | Plays `ch4_beat3_aftermath` in full |

**What changes during the beat:** nothing in the geometry — the hologram is present and animating (via `FloatingArrow`) from build time; there is no activate/reveal trigger for it the way Chapter 1 gated Khall's hologram behind a step. The voice simply plays over the dialogue set once the reach point is satisfied. **It also never turns back off** — see §c for the recommended fix and the canon line it contradicts as built.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (10×18, walls + floor + ceiling) | center (0,0,51) | `Rooms.MastShell` | `…/Art/Generated/Rooms/MastShell.prefab` | **MISSING** |
| `Mast_WallFront` / `Mast_WallBack` (open archways, gap 3m each) | (0,RoomH/2,42) / (0,RoomH/2,60) | `Rooms.MastArch` | `…/Art/Generated/Rooms/MastArch.prefab` | **MISSING** |
| Room detail cluster | center (0,0,51), accent tint (0.3,0.4,0.6) | `Props.RelayDetailKit` | `…/Art/Generated/Props/RelayDetailKit.prefab` | **MISSING** |
| Hologram column + node | (0, 0, 58) | `Vfx.HologramProjection` | `…/Art/Generated/VFX/HologramProjection.prefab` | **MISSING** *(currently a shared primitive helper, not chapter-specific)* |
| `RelayDrone` | (1.5, 2.4, 55) | `Props.RelayDrone` | `…/Art/Generated/Props/RelayDrone.prefab` | **MISSING** |
| `MastLight0` | (0, 3, 54) | — | `ChapterEnvironmentProfile.accentLights["Mast0"]` | profile |

**The hologram never deactivates, even though canon has Ronin-7 cut the relay.** The dialogue script's Beat 3 exit stage direction is explicit: *"He cuts the relay. The omnidirectional hum dies. The chamber is just wind and cable again"* (script line 401). As built, the cyan `BuildHologram` column at (0,0,58) keeps spinning and glowing for the entire rest of the chapter — through the Deepworks descent and into Kerrax's Hold behind the player — since nothing ever calls `SetActive(false)` on it. **This is the exact same gap the doc already fixes for the Beat 0 holo-plate (§4 Beat 0 §c); apply the same fix here:** wire `holoGo.SetActive(false)` — plus, ideally, a "hum dies" audio stinger — to `dlgKhall`'s own completion event, not a new mission-spine step, so "the chamber is just wind and cable again" is realized instead of contradicted for the rest of the chapter.

**`Props.RelayDetailKit` should commission the two named Mast set-pieces, not a generic cluster.** The script's description of the room is specific about two props: "cable strung wall to wall" and "the canyon-city far below through a cracked port." The cracked viewport is the only place in the four walled rooms that shows Drovis's vertical canyon scale from the inside, and strung cable is what physically sells "high, exposed, wind-loud, strung with cable." The kit's commission brief should name both — cable runs threading the room, and a cracked-port window prop — rather than leaving it a generic "detail cluster"; the viewport is the visual counterpart to the missing `MastWind` ambience bed flagged in §f below.

#### d. Combat

None. Beat 3 is dialogue-only.

#### e. Dialogue / VO

**`ch4_beat3_khall`** (`Dialogue_Beat3_Khall`), position (0, 1, 57), 10 lines — Khall names Ronin-7 "Cipher" and declares him a "difficult fiction to maintain"; Ronin-7 refuses the name; Khall's "It is the one I gave you," then the too-long pause; Khall's plea dressed as procedure ("I am not trying to kill you, Cipher. I am trying to bring you home..."); Ronin-7's indictment ("Home doesn't put a switch in your skull"); Khall's quiet, costly agreement ("No. It doesn't."); Echo's private recognition of the voice from the bay; Ronin-7 pressing with that knowledge; Khall retreating into rank while the seams show; Ronin-7 ending the call on his own terms, naming the real target — Kerrax, at the bottom of the caves.

**`ch4_beat3_aftermath`** (`Dialogue_Beat3_Aftermath`), position (0, 1, 58), 5 lines — Iris tries the names out loud (Cipher, Khall); Resh adopts "Cipher" dryly; Kessler reads the danger in a handler who chases a man he already killed once; Ronin-7 turns his own name over; Echo reframes the leash-name as his to keep and turns him toward the descent.

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** The reveal is carried entirely by voice performance and the hologram's slow shimmer.
- **Khall's "flat, omnidirectional voice… coming from everywhere at once" (script line 339) is already correctly delivered — this is an inherent property of the dialogue system, not something this beat needs to engineer.** All `Chapter4Lines` sets, including `ch4_beat3_khall`, play through `BuildDialoguePlayer`'s 2-D `AudioSource` (§7) — Khall's line was never going to sound like it came from the hologram column specifically, because no Ch4 dialogue does. Omnidirectionality is only half of what the script's own line 344 annotation asks for, though — "through the relay, slightly thinned" names a band-limited EQ character the 2-D source alone does not supply; see §7 for the recommended relay-filter treatment.
- No dedicated ambience source is built specifically for the Mast; the chapter's baseline fog/ambient carries the "wind against the hull plate" read visually only — see §3 for the missing `MastWind` bed that would make "high, exposed, wind-loud" audible, not just described.
- **The "second signal under the Coil's traffic" (§a) is an unrealized layered-audio opportunity.** The script is specific: *"under the Coil's traffic runs a second signal, cleaner, older, military… the spire's air hums. Then the hum resolves into a voice"* (script line 339). As built there is no `MastWind` bed at all (above), so there is nothing for a second signal to run *under* — the reveal has a built-in two-layer audio design (a faint buried carrier tone beneath the relay hum, resolving into Khall's voice) that would make the naming land as something heard emerging, not just a dialogue set that starts on reach. This is the audible counterpart to the cracked-viewport prop §c already recommends.
- Comfort vignette behaves normally; no beat-specific override.

---

### Beat 4 — The Deepworks (Parkour Traversal — Echo Calls the Routes)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (cave floor, rock pillars, the rising-water visual) and **`BuildBeat4Logic()`** (the three Deepworks reach points, the two Deepworks scan-drones, `FloodingWaterHazard`, `HunterWaveB`, the three dialogue sets).
> **This is not a climbing level.** No `ZeroGGrabLocomotion`, no wall-run, no ledge-grab component exists anywhere in this chapter — see §1.1. Do not introduce one when patching this beat; the traversal reads through lighting, obstacles, and one water hazard, not through new movement code.

#### a. Narrative purpose & emotional target

"The city's net thinned the deeper he went; down here it's just him, the dark, and the man he came for." The crew rigs a line at the top and holds an upper landing on comm — Kessler, Iris, and Resh do not physically descend; Cipher goes down alone on foot, "where only one body fits and only one voice can read the dark."

**No physical referent exists for the line, or the crew's stated position.** Kessler's `ch4_beat4_descent_intro` line ("We've got the line anchored up top… after that it's you and the sword") and the screenplay's "the crew rigs a descent; Kessler, Iris, and Resh hold an upper landing" have zero geometry behind them — the Mast→Deepworks threshold (z≈60) is a bare `BuildDoorwayWall` gap, and Kessler/Iris/Resh remain at their Throat spawn the whole chapter (§5). Recommend a cheap rigged-line/winch-anchor prop at the Deepworks mouth (~z=60–63), commissioned under a new `Props.DescentRig` key (or folded into `Props.RelayDetailKit`, §4 Beat 3 §c) — see §c below — so "the line anchored up top" and the upper landing the crew holds both have a diegetic object, rather than being asserted by dialogue alone. Echo takes over completely here, the only voice left in his ear, reading the cave and calling the routes; the production note in the dialogue script is explicit that the written route-call lines are *representative samples*, not a final position-triggered set — the actual `ch4_beat4_descent_calls` set (4 short ambient lines) is deliberately generic rather than tied to specific geometry. The segment is a movement-and-tension beat, not a battle: light ambient threat (two more scan-drones, one heat-triggered hunter-wave ambush, one rising-water hazard) rather than a scripted fight, pacing the lull between the Mast reveal and the Kerrax boss. It closes on the first sight of Kerrax's cold blue Dominion-scrap light at the bottom — "Last drop, Cipher. After this there's no more route to call. Just him and you and whatever you decide he is."

**This reveal is verified deliverable, not just asserted.** `KerraxLight0`/`KerraxLight1` (both blue, at z=118/126, §6/Appendix A.1) sit just past `KerraxHold_WallFront`'s **4 m aperture at z=112 — the widest doorway in the chapter** (§2, Beat 5 §c) — and that width is presumably *why* it is the widest: it is what lets the cold-blue glow spill back up the last stretch of the Deepworks toward `DeepworksDepthReachPoint` (z=107, radius 6), where step 14's `dlgDescentEnd` fires (§b/§e below). The three builder facts — the two Kerrax lights, the oversized aperture, and the reach point's position just short of it — are what make Echo's line land on real geometry rather than a spoken assertion; naming them here protects the reveal from a future prefab pass that narrows the arch or re-tints the lights without knowing why they're built that way. Exponential fog over the ~11 m gap (density 0.04, §6) dims the glow to roughly 60% by the time it reaches the reach point — an intended atmospheric attenuation consistent with "first sight," not a bug to brighten away.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Three reach points** gate the descent into thirds: `DeepworksEntranceReachPoint` (0,1,63) radius 4, `DeepworksMidReachPoint` (0,1,86) radius 6, `DeepworksDepthReachPoint` (0,1,107) radius 6.
- **Two scan-drones** (§3.2): `ScanDrone_Deepworks0` (-2,2,72) radius 6, detection 0.25/s; `ScanDrone_Deepworks1` (2,2,96) radius 6, detection 0.25/s — a higher detection rate than the Throat's drones, making the cave hotter per second of exposure.
- **`HunterWaveB`** — three enemies at (-3,0,66), (3,0,67), (0,0,70), built inactive, wired to the 100%-heat threshold (§3.2). Positioned just inside the Deepworks entrance, so a player who enters already hot gets ambushed almost immediately.
- **`Deepworks_RisingWater`** (`FloodingWaterHazard`): a 10×3×8 m trigger volume at (0, -0.3, 106), rising from local Y=-1.5 to Y=-0.2 over 20 s (**shallow — never fully submerges the player**, per the builder's own comment), dealing 3 damage per 1.5 s tick while the `CharacterController` root stays inside the volume. **`autoStart` defaults true, so the rise begins the instant the scene loads — and the 20 s climb is over long before any player can possibly reach it.** z=106 sits behind ~104 m of traversal, six gated reach-points, and Beats 0–3's own dialogue sets (roughly 380 s of VO between them alone); against that, a 20 s rise finishes minutes before arrival. The "rising water" the hazard is named for is therefore never witnessed — every player only ever sees a static, fully-risen shallow pool. Recommend gating `Configure`/start on `DeepworksEntranceReachPoint` or `DeepworksMidReachPoint` (step 9 or 11, §b below) instead of scene load, so the rise actually plays out in front of the descending player rather than being discarded before they get there.
- **Cave pillars** (`Ch4BuildDeepworksProps`): six static rock-pillar props threading a switchback path across the open floor — (-4,1.1,66), (3.5,1.3,74), (-4.5,1,82), (2.5,1.6,90), (-2,1.2,98), (3,1,104) — alternating left/right of center, forcing the player to weave rather than walk a straight line.
- **Player:** free continuous locomotion the entire way; no scripted path.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 9 | `ReachTrigger: Deepworks Entrance` | (0,1,63), radius 4 |
| 10 | `Dialogue: Beat4: Descent Intro (Echo takes over)` | Plays `ch4_beat4_descent_intro` |
| 11 | `ReachTrigger: Deepworks Midpoint` | (0,1,86), radius 6 |
| 12 | `Dialogue: Beat4: Echo Route-Calls` | Plays `ch4_beat4_descent_calls` |
| 13 | `ReachTrigger: Deepworks Depth (past the water)` | (0,1,107), radius 6 |
| 14 | `Dialogue: Beat4: The Last Drop` | Plays `ch4_beat4_descent_end` |

**Step 13's "past the water" label overstates the player's actual progress.** `DeepworksDepthReachPoint` (0,1,107) radius 6 triggers from z≈101, but `Deepworks_RisingWater` (center z=106, size-Z 8, §b/Appendix A.5) spans **z=102–110** — so the reach trigger can satisfy while the player is still inside or upstream of the hazard, not past it. Low priority — this is a builder-label inaccuracy the doc transcribes faithfully, not a doc error — but worth naming so a future patch doesn't trust "past the water" as a safe-past-hazard checkpoint.

**The hazard is also flankable, which undercuts "past the water" as a gate in a different way.** `Deepworks_RisingWater`'s size-X is 10 (spanning x[-5,5]), inside a 16 m cave floor (x[-8,8], §c) — leaving a clear ~3 m dry margin on each flank. A player can walk the switchback around the pillars (§b above) and skirt the trigger volume entirely without ever taking a tick of chip damage, so the cave's one hazard is a static pool that need not be crossed at all. Recommend either widening the trigger to the cave's full width or adding a pillar pair that funnels the switchback path through the x[-5,5] band, so reaching step 13 actually requires passing through the water rather than around it.

**What changes during the beat:** the rising-water volume's local Y climbs continuously and independently of the mission steps; nothing else in the geometry changes.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Cave floor (16×52, no walls/ceiling) | center (0,-0.1,86) | `Rooms.DeepworksFloor` | `…/Art/Generated/Rooms/DeepworksFloor.prefab` | **MISSING** |
| `CavePillar` ×6 | see logic positions | `Props.CavePillar` | `…/Art/Generated/Props/CavePillar.prefab` | **MISSING** |
| `Deepworks_RisingWater` visual | (0, -0.3, 106) | `Vfx.RisingWaterSurface` | `…/Art/Generated/VFX/RisingWaterSurface.prefab` | **MISSING** |
| `DeepworksLight0` / `1` / `2` | (0,2.2,68) / (0,2.2,88) / (0,2.2,104) | — | `ChapterEnvironmentProfile.accentLights["Deepworks0/1/2"]` | profile |
| Hunter-wave (B) enemies ×3 | see logic positions | `Enemies.CoilGanger` | `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS**, but see §9 |
| *(recommended addition — rigged-line/winch-anchor prop, the crew's "line anchored up top")* | ~(0, 1, 61), at the Deepworks mouth | `Props.DescentRig` | *(new commission, or folded into `Props.RelayDetailKit`)* | **MISSING** |

**Constraints the prefabs must respect.** The three accent lights dim progressively deeper into the cave (intensity 1 → 0.8 → 0.7, range 12 → 12 → 10, color drifting cooler/darker) — this is the entire visual mechanism for "the descent"; a prefab pass must not brighten the bottom of the cave relative to its mouth or the darkening-into-danger read breaks. `DeepworksLight0` alone carries `ConsoleFlicker(seed: 44)` — a single flickering light near the top of the descent, not a chapter-wide effect. **The Kerrax-light reveal at the bottom of this table depends on the Beat 5 aperture staying wide** — see §a above for the three builder facts (both `KerraxLight`s, the 4 m `KerraxHold_WallFront` gap, fog attenuation) that make "first sight of his cold blue light" a real, verified sightline rather than a spoken assertion.

**`Vfx.RisingWaterSurface`'s commission should be translucent, not an opaque volume — a VR-comfort requirement, not just an art upgrade.** As built, `Deepworks_RisingWater`'s visual is a `TintShared` opaque cube at (0,-0.3,106), scale (10,3,8), whose top surface sits roughly 1.2 m off the floor and rises through the player's lower field of view over the hazard's 20 s climb (§b). In a headset this both occludes the player's own feet — making the chip-damage boundary invisible from inside — and passes a solid plane up through the viewpoint, reading worse than the "never fully submerges the player" framing (§b) implies. The replacement prefab should be a translucent surface so the floor and the player's own footing stay visible through it, and the rising line reads as water, not a wall.

#### d. Combat

No scripted fight. `HunterWaveB` is the only combat possibility in this beat, and it is entirely conditional on the player's heat state carried over from Beats 1–2 (§3.2) — a player who kept heat low the whole chapter may never see it.

#### e. Dialogue / VO

**`ch4_beat4_descent_intro`** (`Dialogue_Beat4_DescentIntro`), position (0, 1, 62), 2 lines — Kessler over comm, hating that the route only fits one man; Echo settling into the one role no one else can fill.

**`ch4_beat4_descent_calls`** (`Dialogue_Beat4_DescentCalls`), position (0, 1, 86), 4 lines — Echo's ambient route-calls ("Footing's loose on the left," "Drone, your three," "Water's rising ahead," "Crowd's long gone"). Per the production note in the dialogue script, these are representative/generic rather than tied to exact trigger geometry — the mission step plays them as one dialogue set at the midpoint reach, not as position-triggered barks scattered across the cave.

**Three of the four lines map onto real built geometry, though, making a position-triggered version cheaper than "representative/generic" implies.** "Footing's loose on the left" has a literal referent in the six cave pillars, which alternate left/right of center (§4 Beat 4 §b); "Drone, your three" has a literal referent in the two real `ScanDrone_Deepworks0/1` at (-2,2,72)/(2,2,96); "Water's rising ahead" has a literal referent in the real `FloodingWaterHazard` at z=106. Small trigger volumes at the first pillar, each drone's radius, and the water's upstream edge would turn these three filler lines into diegetic route-calls tied to what the player is actually approaching — via the same `EchoPresence` event-kind path §3.2 already recommends for the drone-detection barks — rather than being blocked on "level geometry yet to be designed," since the geometry already exists.

**`ch4_beat4_descent_end`** (`Dialogue_Beat4_DescentEnd`), position (0, 1, 108), 1 line — Echo naming Kerrax's cold blue Dominion-scrap light below, handing the moment over: "No more route to call after this."

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** The FloodingWaterHazard's chip damage is communicated via `Health` damage feedback (existing systems), never a camera effect.
- The fog density (0.04, Appendix A.1) reads heaviest here, per the builder's own comment, precisely because there is no ceiling geometry to otherwise imply enclosure — fog *is* the cave's roof, but see §3: the "total silence of the flooded Deepworks" this fog is meant to accompany has no ambience bed to be silent *against* — a sparse drip bed thinning toward the bottom would sell the descent audibly, not just visually.
- **`FloodingWaterHazard` is silent and un-felt.** Verified against source: `FloodingWaterHazard.cs` carries no `AudioSource` and no `Haptics` call — the rising pool is a tinted trigger volume only. In a headset a player can't easily see their own feet, so a rising, chip-damaging hazard with no rising-water/lap sound and no per-tick controller pulse is nearly invisible as a threat, and Echo's "Water's rising ahead" bark promises a hazard the environment never actually sounds. Recommend a looping rising-water SFX on the `Deepworks_RisingWater` object and a light `Haptics` pulse on each damage tick (3 dmg / 1.5 s).
- **No haptic or audio tell on the two Deepworks scan-drones or their higher detection rate** (§3.2) — same visual-only gap as Beat 1, worse here because the room is otherwise darker and quieter.
- Comfort vignette engages normally on the switchback weave between pillars; no special-case override for the open-floor traversal.
- **The flat-floor realization is a VR-comfort win, not only a scope cut.** §2 and §1.1 both frame the Deepworks' flat cave floor as a concession against the screenplay's literal "drops over black water" and "Last drop, Cipher." The flip side is worth stating explicitly: a literal falling/vertical-drop traversal would induce vection and motion sickness in a headset and would sit awkwardly against this chapter's own no-camera-shake/comfort mandate (§1.1, `CLAUDE.md`'s VR constraints). The flat, walked realization is therefore *also* the VR-correct choice for this beat, not purely a shortfall against the screenplay — closing the loop with the chapter's own comfort constraints rather than reading as a shortfall alone.
- 90 FPS is a real concern here: two active `ScanDroneVolume`s, a continuously-updating `FloodingWaterHazard`, and (conditionally) three active `Enemy` AIs can all be live simultaneously; no baseline exists yet (§1.6).

---

### Beat 5 — Kerrax's Hold (The Mercy / Mera Defects) — REVEAL + ALLY #2

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (trophy-keep shell, plundered plate, trophy crates, relay console) and **`BuildBeat5Logic()`** (Kerrax's inactive spawn + `DuelYield`, Mera Voss's inactive spawn, the Kerrax reach point, three dialogue sets, the mercy/kill mechanic itself).
> **The mercy/kill choice is mechanically live, not scripted.** `DuelYield.yieldThreshold = 0.2` means Kerrax's guard drops (via `disableOnYield`) once his health crosses 20% — the player can still land a killing blow after that point, or sheathe the sword to accept the yield. Do not hardcode a forced-spare cutscene when patching this beat; the spare is a player action.

#### a. Narrative purpose & emotional target

A syndicate bolt-hole "sunk at the foot of the Deepworks: a salvaged keep of plundered plate, trophy-cargo, and the cold light of stolen Dominion gear." Kerrax, "heavyset, scarred... never once been the one on the floor," is mid-transaction — selling Cipher's face up a wire to Khall — when Cipher drops in from the dark. A full boss encounter follows (Kerrax plus his guard, fought across the trophy-keep). Once broken, the Program-conditioned answer is one motion; Echo, quiet, names the conditioning without pushing either way ("You don't have to listen to your hands, Cipher. Not anymore."). The choice is deliberate, sober, slow — not a glitch. Ronin-7 spares him, sheathes the blade, and the whole chapter's thesis lands in five words: *"Then I'll keep malfunctioning."*

Watching from a high ledge, unseen until this moment, is **Mera Voss** — the tracker from the Throat, an ex-Dominion hunter sent to confirm the kill, "yours or his, didn't matter which." The mercy breaks something she has carried for nine years: "Metal doesn't hesitate like that. You did." She defects — **Ally #2** — offered the same mercy she just watched Kerrax receive, scaled up: *"I'm not going to make you choose at gunpoint. That's their method, not mine."* Kessler pressing over comm about Coil movement up top keeps the scene from lingering; the beat, and Act I, close on the climb back out, Kerrax left alive on the floor as "a boss left breathing as a question no one in Drovis can answer."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **`KerraxHoldReachPoint`** at (0, 1, 116), radius 5 m.
- **Kerrax:** built via `BuildEnemy` at (0, 0, 122) against `Ch4Kerrax.asset` (`EnsureEnemyDefinition`-style asset at `Assets/Ronin7/Data/Ch4Kerrax.asset`, maxHealth 260 / damage 18 / moveSpeed 1 / attackCooldown 1 — a high-HP variant mirroring the pattern used for other chapter bosses), then given a `StoryNpc` (`displayName = "Kerrax"`) and a `DuelYield` component wired to his own `Health` (`opponent`), `yieldThreshold = 0.2`, `disableOnYield = [the Enemy component]` (his AI stops attacking once yielded), `sword = the player's Grabbable sword`, `autoAcceptSeconds = 30` (a 30 s safety timer accepts the yield automatically if the player never re-sheathes). Built `SetActive(false)` — activated only by step 17.
- **Kerrax delivers his entire confrontation line to an empty floor.** Step 16 (`Dialogue_Beat5_KerraxConfront`) plays with `kerraxGo` still `SetActive(false)` — he is only activated by step 17, the very next step. The screenplay stages the opposite order: "He turns as Cipher drops in from the dark," *then* speaks, *then* "triggers his guard." As built, "The recovery walks in on its own legs..." is heard while there is nothing standing at (0,0,122) to have said it, and Kerrax pops into existence only once the fight starts. The structural tension: activating the actual `BuildEnemy`/`Enemy` body a step early would start his combat AI mid-dialogue, since nothing currently distinguishes "present and talking" from "present and fighting." A candidate fix is a separate, non-hostile idle body (or the same body with its `Enemy` component disabled) visible from step 16, with the `Enemy` AI/aggression enabling only at step 17 — the same present-but-not-yet-hostile idiom the recommended Mera fix below uses for her weapon-raised silhouette.
- **Mera Voss:** `Ch4PlaceStoryNpc` at (-5, 0, 118), `wanderRadius: 0`, then explicitly `SetActive(false)` — hidden until step 19 reveals her **in place** (no walk-out animation, no `NpcWalker` — same "activate in place" idiom Chapter 1 used for Khall's hologram reveal). **This keeps her fully inactive through the boss fight itself** — the dialogue script stages her on the ledge, weapon half-raised, tracking the fight from cover throughout the duel (steps 17–18), with her aim wavering and lowering only at the mercy; as built, the player has no visible witness to the choice until after it has already resolved. See §9 for the recommended fix (a static, weapon-raised silhouette active from step 17, transitioning to her reveal pose only at step 19) and its required sightline — behind the player's spare-facing, elevated, on −x — so "with your back to my gun" reads literally rather than merely putting a body in the room. **That fix also needs literal elevated geometry that doesn't exist:** `Ch4BuildKerraxHoldDetails` builds only floor-level props (`PlunderedPlate`/`TrophyCrate`/`RelayConsole`, §c) — there is no ledge anywhere in Kerrax's Hold for a "high ledge, weapon-raised silhouette" to stand on. Fold a ledge/catwalk into `Props.TrophyKeepDetailKit`'s commission (§c), on the same −z/−x entrance-side sightline, so the fix places her on something rather than at a coordinate with nothing under it.
- **The silhouette fix above must carry an actual weapon prop, not just an elevated pose.** Her whole Beat 5 arc is built on a visible, aimed firearm — "weapon half-raised" (script line 461), "steadies her aim on Cipher" (line 480), "her aim does not fire. It wavers. It lowers" (line 504), "with your back to my gun" (line 529), "holsters the weapon for good" (line 543). Elevation alone does not deliver any of that; the `Named.MeraVoss` prefab (or the recommended silhouette variant) needs a visibly aimed weapon model through steps 17–18, transitioning to a lowered/holstered pose at step 19 — otherwise "with your back to my gun" and the wavering-aim beat that is the mercy's emotional engine point at a witness holding nothing.
- **The duel's own acceptance advances the mission director directly:** `UnityEventTools.AddPersistentListener(duelYield.onAccepted, missionDirector.AdvanceFromPrompt)` is wired outside the `steps` array itself. Step 18 is authored as a `Prompt` step with `promptObject: null` — unlike every other Prompt step in the codebase (which waits on a `PromptInputAdvancer`), this one has no visible prompt object at all; it blocks purely until `DuelYield.Accept()` fires its event, which happens when the player sheathes the sword after Kerrax yields (or after the 30 s auto-accept). **This is a duel-specific override of the standard Prompt-step contract, not a bug** — flag it clearly if reusing this pattern elsewhere.

**Kerrax is not staged at the thing he's mid-transaction on.** The screenplay opens the beat with Kerrax "stands over a relay-rig wired to sell a face up a wire to Khall" — he is introduced *at* the console, not near it. The builder spawns him at (0,0,122) and the `RelayConsole` prop at (0,0.6,125) (§c) — roughly 3 m apart, with Kerrax's default facing toward the entrance rather than the console. As built, the console reads as unrelated dressing rather than the thing his opening line is about. Recommend moving Kerrax's spawn to at/over the `RelayConsole` (or the console to him) and orienting him to face away from it, toward the entrance, so "selling you up a wire" and "he turns as Cipher drops in" both read physically, not just in the dialogue.

**Enemy build note:** unlike Chapter 1's dedicated boss/kill-box enemies (`BuildDominionEnemy`, which tries a specific trooper prefab first), Kerrax is built with the same generic `BuildEnemy` helper as every rank-and-file enemy in this chapter — greybox capsule body, `ArmR/Sword/Blade/BladeTip` chain, no attempt to load `Named.Kerrax.prefab` directly. His likeness is applied (if at all) only through `EnemyArtWirer`'s post-build pass — see §9 for why that pass does not actually give him his own face.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 15 | `ReachTrigger: Kerrax's Hold` | (0,1,116), radius 5 |
| 16 | `Dialogue: Beat5: Kerrax Confronts` | Plays `ch4_beat5_kerrax_confront` |
| 17 | `Trigger: Activate Kerrax (the boss)` | Activates `kerraxGo` — the boss fight begins |
| 18 | `Prompt: Kerrax Duel (yield + sheathe)` | Blocks on `DuelYield.onAccepted`, not a `PromptInputAdvancer` |
| 19 | `Trigger: Mera Voss Steps Out` | Activates `meraGo` in place |
| 20 | `Dialogue: Beat5: The Mercy` | Plays `ch4_beat5_mercy` |
| 21 | `Dialogue: Beat5: Mera Recruited (Ally #2, closes Act I)` | Plays `ch4_beat5_mera_recruit` |
| 22 | `Trigger: Chapter Outro (flag + fade + canvas)` | Activates `outroGo` |

Step 22 both ends Beat 5 and ends Chapter 4: `ChapterOutro.OnEnable` invokes its wired `onActivated` (a persistent-listener call into `CampaignFlagSetter.SetFlags`, setting `ch4_complete`), reveals the "CHAPTER 4 COMPLETE" world-space canvas at (0, 1.4, 128), waits `fadeDelay` 1.5 s, fades to black over `fadeDuration` 2 s via `ScreenFader`, and finally publishes `ZoneCompleted` — the same signal `GameFlowManager`'s normal mission-complete handling listens for elsewhere in the game. Neither `fadeDelay` nor `fadeDuration` is overridden by the builder; both are `ChapterOutro`'s own component defaults.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (14×20, walls + floor + ceiling, dead end) | center (0,0,122) | `Rooms.KerraxHoldShell` | `…/Art/Generated/Rooms/KerraxHoldShell.prefab` | **MISSING** |
| `KerraxHold_WallFront` (open archway from the Deepworks, gap 4m — the widest aperture in the chapter) | (0, RoomH/2, 112) | `Rooms.KerraxHoldArch` | `…/Art/Generated/Rooms/KerraxHoldArch.prefab` | **MISSING** |
| Room detail cluster | center (0,0,122), accent tint (0.2,0.35,0.55) | `Props.TrophyKeepDetailKit` | `…/Art/Generated/Props/TrophyKeepDetailKit.prefab` | **MISSING** |
| *(recommended addition to the same kit — ledge/catwalk platform, Mera's stand)* | elevated, −z/−x entrance side, near (−5, ~2, 116) | `Props.TrophyKeepDetailKit` | *(same prefab, extended commission)* | **MISSING** |
| `PlunderedPlate0` / `PlunderedPlate1` | (-5,0.6,116) / (5,0.6,116) | `Props.PlunderedPlate` | `…/Art/Generated/Props/PlunderedPlate.prefab` | **MISSING** |
| `TrophyCrate0` / `TrophyCrate1` | (-4,0.4,126) / (4,0.4,128) | `Props.TrophyCrate` | `…/Art/Generated/Props/TrophyCrate.prefab` | **MISSING** |
| `RelayConsole` | (0, 0.6, 125) | `Props.RelayConsole` | `…/Art/Generated/Props/RelayConsole.prefab` | **MISSING** |
| `KerraxLight0` / `KerraxLight1` | (-4,2.6,118) / (4,2.6,126) | — | `ChapterEnvironmentProfile.accentLights["Kerrax0"/"Kerrax1"]` | profile *(`KerraxLight0` carries `AmbientPulse(6s)`)* |
| `KerraxHoldAmbience` | (-4, 2.6, 122) | — | `AudioSource` + `ProximityAmbienceLayer`, procedurally-generated "DreadDrone" theme clip (inner 4m/outer 14m/max vol 0.4) | audio |
| Kerrax (boss) | (0, 0, 122) | `Named.Kerrax` | `…/Art/Generated/Characters3D/Named/Kerrax.prefab` | **EXISTS**, but not wired to the in-scene boss body — see §9 |
| Mera Voss | (-5, 0, 118) | `Named.MeraVoss` | `…/Art/Generated/Characters3D/Named/Mera-Voss.prefab` | **EXISTS** |

#### d. Combat

The boss fight itself: `BladeDamager`'s EMA swing-speed model, same as every other combat encounter, against a single high-HP `Enemy`. Per the screenplay, "a full boss encounter, Kerrax plus Coil muscle" is expected — **the as-built encounter is Kerrax alone; no additional Coil enforcers are spawned for this fight.** This is a real content gap between the screenplay's staging and the mission-spine's authored enemy list, worth flagging (mirrors Chapter 1's flagged trooper melee/rifle gap) rather than silently treated as equivalent. The same script line stages a second, equally unrealized element alongside the missing enforcers: Kerrax "triggers his guard *and the keep's defenses*," with the fight itself described as "Kerrax plus Coil muscle plus the stolen Dominion gear." The cold-blue `RelayConsole` and the rest of the Beat 5 detail kit (§c) are dressing only — nothing in the fight activates "the keep's defenses" — so the flag above is only half the gap; it is Kerrax alone against *both* the missing muscle and the inert stolen gear.

The mercy/kill choice is `DuelYield`'s state machine (§b): at 20% health, Kerrax's `Enemy` AI disables and he stops fighting back; the *kill* path is simply continuing to land blows past that point (his `Health.Died` event still fires normally and would also trigger `EnterYield` defensively if health somehow reaches zero in one hit — see `DuelYield.OnOpponentDied`, a redundant safety net, not the intended path); the *mercy* path is sheathing the sword (`Grabbable.IsHeld` becomes false), which `Accept()`s the yield and advances the mission.

#### e. Dialogue / VO

**`ch4_beat5_kerrax_confront`** (`Dialogue_Beat5_KerraxConfront`), position (0, 1, 117), 3 lines — Kerrax's opening menace ("The recovery walks in on its own legs... You've cost me four good enforcers and a courier I had three-quarters sold" — a second, independent reference to the Sink snatch-team's headcount, spoken after the player has already fought it as three; see §9); Ronin-7's flat warning that both buyers want him for reasons Kerrax doesn't know; Kerrax's dismissal ("Reasons are for buyers. I just collect.") — the last comfortable thing he says before the fight.

**`ch4_beat5_mercy`** (`Dialogue_Beat5_Mercy`), position (0, 1, 121), 9 lines — Echo naming the conditioning without pushing a choice; Ronin-7 to the beaten Kerrax ("Tell them what you saw"); Kerrax's disbelief ("A killer that stops is a broken killer"); Ronin-7's thesis line; Mera Voss's first words from the ledge; Echo connecting her to the Throat's watcher; Mera's confession of nine years hunting operatives and never seeing one stop; Ronin-7's quiet confirmation; Mera's closing line, rewritten per the audit fix to drop the "not a fault in the metal, that's a man" antithesis ("Metal doesn't hesitate like that. You did.").

**`ch4_beat5_mera_recruit`** (`Dialogue_Beat5_MeraRecruit`), position (0, 1, 122), 9 lines — Kessler's comm-urgency about Coil movement up top; Ronin-7 offering Mera the same choice, not a gun; her decision to defect, stating her value plainly; Echo's dry mark of Ally #2; Ronin-7's welcome (the Hub-is-home-for-everyone-recruited rule, stated explicitly); Mera's dry take on the crew; Kessler's wary comm check; Ronin-7 confirming and choosing to leave Kerrax alive "for the Coil to find."

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point — the boss fight, the mercy, and the recruitment are all carried by `Haptics`/`AudioDirector`/`CombatFeedbackController` and VO performance, never the camera.
- `KerraxHoldAmbience`'s "DreadDrone" bed and `KerraxLight0`'s slow `AmbientPulse(6s)` are the room's only ongoing "juice" once combat ends — a deliberate come-down after the fight, echoing Chapter 1's post-fight `AmbientPulse` beat.
- **The dialogue script's own sensory layer for this room is narrower and more specific than "DreadDrone."** Its Beat 5 setting calls for "a slow drip of water in the dark, the low hum of that stolen [Dominion] gear" — a water-drip plus a stolen-gear hum, distinct from the generic dread bed currently wired here. Worth authoring as candidate layered SFX on `KerraxHoldAmbience` (or a second source) rather than relying on the one shared theme clip. The script's opening image for the room names a distinct sensory quality on top of that: SEGMENT 5 sets the scene as "the air is close and still" — a dead-quiet, unventilated, sealed-at-the-bottom-of-the-world read, separate from the cold-blue *light* the rest of this document leans on (§6). This is the arc's terminal "silence," the one room where "there is no one left to perform for" — so the post-fight come-down already cited above (`KerraxLight0`'s `AmbientPulse(6s)`) should target *stillness* specifically in audio: a near-total ambience floor with only the drip and the gear-hum poking through, not merely a quieter dread bed.
- **The mercy itself — the chapter's thesis moment — has no dedicated feedback.** Verified: `DuelYield.cs` fires no haptic or audio on `Accept()`, only its `onAccepted` `UnityEvent` (wired to `MissionDirector.AdvanceFromPrompt`). Sheathing the sword to spare Kerrax is the one deliberate player action this beat exists to deliver, and it currently lands in complete silence. Recommend a single soft `Haptics` confirmation pulse on sheathe/`onAccepted` — a felt full-stop for "Then I'll keep malfunctioning."
- **"Sheathes the blade" has no sheath to sheathe into — the gesture is realized as letting go.** The mercy precondition is `Grabbable.IsHeld == false` (§b above): there is no holster/back-mount prop in the rig and no snap-to-sheathe target, so the player satisfies it by simply releasing the katana, which then falls to the floor under normal physics. That reads as *dropping the weapon in defeat*, not the deliberate, reverent sheathe the screenplay stages — absent a holster prop or a snap-to-sheathe target, "sheathe" and "drop" are currently indistinguishable, which slightly undercuts the beat the whole chapter builds to.
- Comfort vignette engages normally through the boss fight's frequent turns; no special case.
- This is the chapter's — and Act I's — closing beat: `ChapterOutro`'s fade is the last thing the player sees before the smash-to-black the dialogue script calls for.

## 5. Character travel-route master table

**Chapter 4 has no `NpcWalker` legs anywhere.** This is the single largest structural difference from Chapter 1's travel model: Chapter 1's Kessler physically walks twice, on rails, via the `NpcWalker` + `MissionDirector` Trigger idiom, with a hard Y-invariant (`kesslerFloorY`) tested by `Chapter1BuilderTests.cs`. **`Chapter4Builder.cs` contains zero calls to `BuildNpcWalker`.** Every NPC in this chapter is instantiated exactly once, at a single fixed position, and never moves beyond its own local `StoryNpcWander` radius (or doesn't wander at all).

| NPC | Spawn | Wander radius | Physical travel | Notes |
|---|---|---|---|---|
| Kessler | (-1.5, 0, 3) | 0.6 m | **none** | Present only at the Throat spawn for the whole chapter. His `(comm)`-tagged lines in Beats 4–5 are voiced without any change to his physical position — he is meant to be reading as remote by then, and the geometry accidentally supports that (he never left the Throat to begin with), but nothing in the builder *establishes* he stayed at an "upper landing" the way the screenplay stages it. |
| Iris | (1.5, 0, 3) | 0.6 m | **none** | Same as Kessler — present at spawn only. Her Beat 2 dialogue implies she is physically in the Sink protecting Tessa; no geometry places her there. She is also staged at the Mast relay console in Beat 3 — script line 339: *"Iris works the console; the spire's air hums"* — a second, unrealized physical-presence gap on top of the Sink one; the relay/hologram reveal implies an operator body that, like the rest of the crew, never leaves the Throat spawn. |
| Resh | (0, 0, 5) | 0.8 m | **none** | Same pattern. |
| Tessa Rin | (3, 0, 32) | 0 m (static) | **none** | No exit mechanism (§9) — remains visible at her Sink stall through the rest of the chapter, contradicting her scripted exit. |
| Mera Voss | (-5, 0, 118) | 0 m (static) | **none** | Built inactive; step 19 reveals her **in place**, not via a walk-out from cover — the same "activate in place" idiom Chapter 1 used for Khall's hologram reveal. |
| Kerrax | (0, 0, 122) | — (combat AI, not `StoryNpcWander`) | **none** (fights in place) | Built inactive; step 17 activates him. |
| Khall | *(never instantiated)* | — | — | Realized purely as V.O. through a generic `BuildHologram` primitive at (0,0,58) — no `StoryNpc`, no prefab, no position of his own beyond the hologram column. |
| Mira | *(never instantiated)* | — | — | Correctly absent — stays sealed aboard the Cairn per canon; her one Beat 0 line plays with no body. |

**If a future pass wants any of Kessler/Iris/Resh physically accompanying the player past the Throat** (to sell the Sink fight's "Iris behind the plate-stacks, Resh with me" blocking, or the Deepworks' "upper landing" staging literally), that requires new `NpcWalker` legs with the same Y-invariant discipline Chapter 1 established — floor Y is a constant 0 across this entire chapter (§2), so the invariant is trivially satisfiable here, but it does not exist today and nothing currently tests for it.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat/Segment | Mood | Accent entries | Behaviour | What changes during the beat |
|---|---|---|---|---|
| 0 — The Cairn (briefing) | quiet, stationary | *(reuses the Throat's own lights — no dedicated Beat 0 lighting)* | — | none |
| 1 — The Throat | sodium glare, smoky, watched | `ThroatLight0` (violet), `ThroatLight1` (orange) | `None` | scan-drone telegraphs pulse green→red on player proximity; no scripted light events |
| 2 — The Sink | tighter, louder, magenta/cyan clash | `SinkLight0` (magenta), `SinkLight1` (cyan) | `None` | none scripted; the snatch-team fight and sabotage reveal carry no lighting change of their own |
| 3 — The Mast | high, cold, wind-loud steel-blue | `MastLight0` | `None` | the hologram's own cyan glow (via `BuildHologram`) is the only "event," and it is present from build time, not step-gated |
| 4 — The Deepworks | darkening cave, teal → near-black | `DeepworksLight0/1/2` (progressively dimmer/cooler) | `DeepworksLight0`: `ConsoleFlicker(seed 44)`; others `None` | the rising-water volume's Y climbs continuously (independent of any dialogue step); heaviest fog in the chapter here (no ceiling to imply enclosure otherwise) |
| 5 — Kerrax's Hold | cold Dominion-blue, still | `KerraxLight0` (blue), `KerraxLight1` (blue) | `KerraxLight0`: `AmbientPulse(6s)`; `KerraxLight1`: `None` | none lighting-wise; the mercy and Mera's reveal are carried by dialogue/VO, not a light cue |

**Crowd density is a second, cruder version of this same descent (§3).** `PlaceDecorativeCrowd` seeds 4 NPCs in the Throat, 4 in the Sink, and 0 in the Mast/Deepworks/Kerrax's Hold — a 4/4/0/0/0 taper running alongside the lighting progression above. It is currently flat between the two crowded rooms; see §3 for the recommendation to weight the Throat denser than the Sink so the crowd count, not lighting alone, carries the "thousand faces" read.

**Global values, same across every beat (unlike Chapter 1's per-room variety):** directional key color (0.85, 0.75, 0.5), intensity 0.45, rotation Euler(55,-35,0) — a dim, warm sodium-glare key, roughly half the intensity of Chapter 1's; ambient Flat mode, color (0.1, 0.09, 0.09) — near-black, notably darker than Chapter 1's (0.15, 0.16, 0.19); fog Exponential, color (0.05, 0.045, 0.045), density 0.04 — **more than double** Chapter 1's 0.018, reflecting the dock haze and cave enclosure. Floor tint (0.17, 0.15, 0.14) / ceiling tint (0.08, 0.07, 0.07) are identical in every walled room; there is no per-room floor/ceiling variety in this chapter.

## 7. Audio / VO manifest cross-reference

Twelve canonical dialogue sets, defined in `Chapter4Lines.cs` and consumed via `Chapter4Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position | Runtime expectation (script `seconds:` sum) |
|---|---|---|---|
| `ch4_beat0_briefing` | 0 | (0, 1, 1) — `Dialogue_Beat0_Briefing` | ~69 s / 7 lines |
| `ch4_beat1_throat` | 1 | (0, 1, 8) — `Dialogue_Beat1_Throat` | ~72 s / 9 lines |
| `ch4_beat2_tessa_meet` | 2 | (3, 1, 31) — `Dialogue_Beat2_TessaMeet` | ~59 s / 7 lines |
| `ch4_beat2_sabotage` | 2 | (0, 1, 36) — `Dialogue_Beat2_Sabotage` | ~103 s / 12 lines |
| `ch4_beat3_khall` | 3 | (0, 1, 57) — `Dialogue_Beat3_Khall` | ~84 s / 10 lines |
| `ch4_beat3_aftermath` | 3 | (0, 1, 58) — `Dialogue_Beat3_Aftermath` | ~32 s / 5 lines |
| `ch4_beat4_descent_intro` | 4 | (0, 1, 62) — `Dialogue_Beat4_DescentIntro` | ~21 s / 2 lines |
| `ch4_beat4_descent_calls` | 4 | (0, 1, 86) — `Dialogue_Beat4_DescentCalls` | not timed in canon — a representative/generic sample, not the script's final authored text (§4 Beat 4 §e) |
| `ch4_beat4_descent_end` | 4 | (0, 1, 108) — `Dialogue_Beat4_DescentEnd` | ~14 s / 1 line |
| `ch4_beat5_kerrax_confront` | 5 | (0, 1, 117) — `Dialogue_Beat5_KerraxConfront` | ~32 s / 3 lines |
| `ch4_beat5_mercy` | 5 | (0, 1, 121) — `Dialogue_Beat5_Mercy` | ~62 s / 9 lines |
| `ch4_beat5_mera_recruit` | 5 | (0, 1, 122) — `Dialogue_Beat5_MeraRecruit` | ~89 s / 9 lines |

Runtimes are summed from `Ch04_The_Overseers_Hunt_Dialogue_Script.md`'s per-line `seconds:` annotations, counting only the lines that survive into `Chapter4Lines.cs` — the two documented full-line drops (§4 Beat 0 §e: Resh's second briefing line, 11 s; Iris's Beat 1 "recovery notice" line, 10 s) are excluded from their sets' totals. The four audit-thinned antithesis lines (§9: Beat1/Resh, Beat2/Ronin-7, Beat3/Kessler, Beat5/Mera Voss) carry shorter code text than the script's, so their sets' totals are an upper bound, not exact. Use this column as a length expectation against `Ch4WireVoiceClips`'s "N/M resolved" console warning (§8 item 7) when sanity-testing a fresh VO batch — a set that resolves the full clip count but runs wildly short or long against this figure is worth a manual listen.

Each is built by the local `Ch4BuildDialogue` wrapper (mirroring Chapter 1's `BuildChapter1Dialogue` pattern, not the shared clip-loader): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch4WireVoiceClips`, resolving each line's `AudioClip` from `Chapter4Lines.ClipName(setId, index, speaker)` — pattern `ch4_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all twelve `DialoguePlayer`s.

**Dialogue is data, not art.** None of this changes in the refactor — the twelve set ids, their positions, and the clip-resolution pattern are canon.

**All twelve Ch4 dialogue sets play 2-D (non-positional).** Verified against source: `BuildDialoguePlayer` (`ChapterSharedBuilders.cs:754`) adds its `AudioSource` with `playOnAwake = false` and never touches `spatialBlend`, which stays at its component default of 0 — full 2-D. This is distinct from the two ambience beds (`SinkDreadAmbience`, `KerraxHoldAmbience`), which are also `spatialBlend = 0` sources but are wrapped in `ProximityAmbienceLayer` and driven by distance (inner/outer falloff, §4 Beat 2/5 §c). This one fact reframes two things elsewhere in this document rather than opening a new gap: it means Khall's "flat, omnidirectional voice… coming from everywhere at once" (script line 339, §4 Beat 3 §f) is already delivered correctly by the 2-D source, not something that needs engineering; and it means none of the disembodied lines in this chapter — Mira's Beat 0 line, or any `(comm)`-tagged Kessler line in Beats 4–5 (§5) — read as emanating from empty air where no body stands, because dialogue VO in this chapter was never positional to begin with.

**What 2-D omnidirectionality does not deliver is the diegetic band-limited treatment the script separately asks for on top of it.** Khall's `ch4_beat3_khall` lines are annotated "through the relay, slightly thinned and omnidirectional" (script line 344), and every `(comm)`-tagged Kessler line in Beats 4–5 is annotated "over comm, thinned by depth" (Beat 4, script line 437) or "over comm" (Beat 5, script lines 534, 567) — a second, distinct property that nothing in `Ch4WireVoiceClips`/`BuildDialoguePlayer` currently authors: a relay/comm EQ pass (rolled-off highs and lows, a faint carrier hiss), baked onto the clips themselves or applied via an `AudioLowPassFilter`/`AudioHighPassFilter` pair on the relevant `DialoguePlayer`s. This is the chapter whose central reveal *is* a voice on a hijacked relay, and whose captain narrates the entire Deepworks/Hold sequence from an upper landing he never physically occupies (§5) — without the filter, "thinned by the relay/by depth" is asserted by the script annotation but not heard. It also gives the Beat 3 hologram-deactivation fix (§4 Beat 3 §c) a second payoff: cutting the relay would silence the filtered voice character along with the hum, so "the chamber is just wind and cable again" reads as a transmission actually ending, not just a dialogue set finishing.

SFX / ambience bed:

| Clip / source | Used for |
|---|---|
| `Assets/Ronin7/Audio/WaveAlarm.wav` | `EnemyWaveSpawner`'s `waveSting` one-shot (both HunterWaveA and HunterWaveB), if present at that path — note this is a different folder than Chapter 1's door-audio `DoorSlide.wav`, which lives under `Art/Generated/Audio/` |
| `SinkDreadAmbience` / `KerraxHoldAmbience` | procedurally-generated "DreadDrone" theme clips, inferred by name from `ProceduralAudioClipBuilder.AssignGeneratedClips` (the theme-by-name-substring convention: names containing "dread"/"throne"/"vault" pick `DreadDrone`; anything else defaults to `HangarHum`) — note this gives the Sink the same dread theme as Kerrax's Hold despite the two rooms being canon opposites (bazaar roar vs. terminal silence); see §3 and §4 Beat 2 §f |
| `HologramOn.wav` equivalent | **not present** — unlike Chapter 1's Khall reveal, no dedicated hologram-activation SFX is wired for the Mast's relay voice |

`ReverbZonePlacer.AutoTagInteriorVolumes()` + `.PlaceReverbZonesForInteriorVolumes()` run at the end of the build, auto-tagging the Throat/Sink/Mast/Kerrax's Hold as distinct interior reverb volumes — the Deepworks, having no walls, is not a candidate for this system and gets no reverb zone of its own.

**That is an acoustic inversion worth flagging as an immersion cost, not a neutral fact.** A flooded cave is canonically the *most* reverberant space in the chapter — the script's Segment 5/4 sensory note is "a slow drip of water in the dark" and "drops over black water," echoing stone-and-water being the defining sound of a cave — yet it is the one room that ends up acoustically dead while the four boxy walled rooms get reverb. Because it has no walls for `AutoTagInteriorVolumes()` to find, it can't be auto-tagged; **recommend a manual cave/underground reverb zone authored by hand in the Deepworks volume, under `[STATIC_ART_DO_NOT_DELETE]`**, paired with the sparse drip bed §3 already recommends. Without it, Echo's route-calls and the water hazard play bone-dry in the exact space that should sound cavernous.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 04 — The Overseer's Hunt** (`XRRigBuilder.BuildChapter4OverseersHunt()`).
2. **EditMode is the gate.** Follow the project's current baseline (see `CLAUDE.md` / `Project/Docs/IMPROVEMENT-SUMMARY.md` for the number in force at the time of the change). Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot, same as every other chapter.** No EditMode test invokes `BuildChapter4OverseersHunt()` or loads `Ch04_OverseersHunt.unity`. A green suite says nothing about whether the scene still builds correctly. Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **No Y-invariant regression test exists for this chapter**, and none is needed for NPC waypoints specifically — there are no `NpcWalker` legs to test (§5). The one Y-sensitive system that *does* exist here is `FloodingWaterHazard`'s `startY`/`endY` pair (-1.5 → -0.2, local space); a patch that changes the hazard's parent transform position must keep those local offsets consistent with the chapter-wide floor-Y=0 invariant (§2).
4. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
5. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty room, never an exception.
6. **Perf reference bar: not yet recorded (§1.6).** Capture a `UnityStats` reading (drawCalls / setPassCalls / tris / verts) on the next fresh rebuild and record it in `Project/Docs/CHAPTER-BUILD-LEDGER.md` before landing the first prefab swap — there is nothing to regress against today. The 72 Hz floor is not negotiable once a baseline exists.
7. **Console check:** `Ch4WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild.
8. **Heat/wave sanity check (chapter-specific, new).** After a rebuild, manually drive the player's head transform inside a `ScanDroneVolume`'s radius for ~6 s (per the tuning-contract comment in `HeatMeter.cs`, threshold 0 should cross around then) and confirm `HunterWaveA` begins. This is the one gameplay system in this chapter with no other verification path — it is entirely runtime-driven (`EnemyWaveSpawner.Begin`'s coroutine polls `Camera.main`), so a batch-mode EditMode/PlayMode test cannot easily exercise it end to end.
9. **`EchoPresence` precondition (new).** `EchoLines.PoolsFor` returns every pool empty until `CampaignState.HasFlag("ch3_complete")` is true (`EchoLines.cs` — Echo hasn't woken/been named until Ch3). A standalone playtest of `Ch04_OverseersHunt.unity` with no campaign flags set gets a **completely mute Echo** for the whole chapter — no `enemy_killed`/`player_hurt`/`ability_activated` callouts, and none of the drone-cone/heat-climb barks recommended in §3.2 once built. Before testing any Echo-dependent behavior in isolation, set `ch3_complete` (and `ch4_complete` if testing the post-naming "Cipher" lines) in `CampaignState` first — otherwise a silent Echo reads as broken when it is actually flag-gated-off as designed.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter4OverseersHunt()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter4Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** Reversible cleanup only, per project-wide policy.
- **Reject any prefab import that introduces a `MeshCollider`.** Room shells and props get primitive colliders.
- **The crew's physical presence does not extend past the Throat (§5).** Kessler, Iris, and Resh are spawned once at chapter start and never move again, despite dialogue that places them in the Sink fight, the Deepworks' upper landing, and (implicitly) the climb back out with Mera. This reads as *correct* for the Deepworks/Kerrax's Hold beats (their lines are explicitly `(comm)`-tagged there, matching "the crew holds an upper landing"), but it is an unaddressed gap for Beat 2's Sink fight, where the screenplay stages Iris and Resh physically in the room. Do not silently add `NpcWalker` legs to "fix" this without confirming intent — it may be a deliberate scope cut for this pass, not an oversight.
- **`EchoPresence.missionDialogue` is unwired — a live dialogue-overlap risk, not just a missing feature.** `Chapter4Builder.cs` never assigns `EchoPresence`'s optional `missionDialogue` field (§3.2), so its anti-overlap guard never engages. Either `HunterWaveA` or `HunterWaveB` can spawn on a heat threshold at any point in the chapter, including while any of the twelve `Chapter4Lines` sets is playing; killing one of those enemies mid-set fires Echo's `enemy_killed` bark over the scripted line. Because the field references a single `DialoguePlayer`, wiring it to any one set only partially closes the gap — a chapter-wide "dialogue currently playing" flag would be the complete fix. Worth closing before the recommended drone-cone/heat-climb `EchoPresence` pool (§3.2) is authored, since that pool would make Echo speak far more often and raise the odds of an overlap.
- **The Beat 1 "watcher at the rail" has no physical anchor, so the Beat 5 callback references geometry the player never saw.** Echo's Beat 1 lines ("Rail. High left. The one not buying anything") and Beat 5's `ch4_beat5_mercy` callback ("The rail. The one who already knew your face") both point at a rail/figure that `Chapter4Builder.cs` never places — the Throat has no elevated rail geometry and no watcher body; Mera Voss is only instantiated, inactive, at (-5,0,118) in Kerrax's Hold. At minimum this is a seed→payoff visual that is currently dialogue-only; see §4 Beat 1 §b/§c for a recommended inactive-figure fix mirroring Mera's own "activate in place" idiom.
- **Kerrax speaks before he exists.** Step 16 (`Dialogue_Beat5_KerraxConfront`) plays while `kerraxGo` is still `SetActive(false)` from build time; step 17 is the `Trigger` that activates him. The result: his opening menace lands on an empty room and he only appears as the fight starts, inverting the screenplay's "he turns as Cipher drops in, *then* speaks, *then* triggers his guard." See §4 Beat 5 §b for the structural tension (activating his real `Enemy` body a step early would start his AI mid-dialogue) and the candidate fix (a non-hostile idle body present at step 16, aggression enabling at 17). The Sink snatch-team has a milder version of the same idiom — announced by Echo in step 3's dialogue while inactive, appearing only at step 4 (§4 Beat 2 §b).
- **Mera Voss's in-fight cover presence is unrealized — a bigger gap than the rail-watcher above, and the same character and the same seed.** The dialogue script stages her on the ledge *throughout the boss fight*: "From a high ledge in cover, unseen... MERA VOSS... weapon half-raised," her aim "tracking the fight, never firing," then, on the mercy, "her aim does not fire. It wavers. It lowers." That wavering-aim payoff is the emotional engine of the mercy — Mera is the witness the choice is performed in front of. The builder keeps `meraGo` fully `SetActive(false)` through steps 16–18 and only activates her at step 19, *after* the duel has already resolved (§4 Beat 5 §b), so the player never sees the gun the mercy is performed in front of. A proper realization stages Mera as a static, weapon-raised silhouette at an elevated `KerraxHold` position, active from step 17 (boss activation) through step 18 (the duel prompt), and only transitions to her "steps out, weapon down" pose at step 19 — giving her Beat 5 lines ("A machine doesn't choose twice," "you spared him slow, with your back to my gun") a gun the player actually watched, rather than a witness who was never there. **The elevation is necessary but not sufficient — the silhouette needs a visibly aimed weapon in hand, not just a raised-arm figure.** "With your back to my gun" (line 529) and the wavering-aim beat at the mercy (line 504) are the load-bearing property of her entire arc; the `Named.MeraVoss` prefab or its silhouette variant must carry the weapon prop itself through steps 17–18 and lower/holster it at step 19, or the payoff is a witness standing empty-handed. **The silhouette's position has to match "with your back to my gun," not just be "elevated."** The player faces Kerrax at (0,0,122) to deliver the spare (spawn facing +Z, §2), so the line only reads literally if Mera sits **behind that facing — toward −Z, the entrance side — elevated, and on −x**, consistent with the Beat 1 rail-watcher fix above. Her actual spawn (−5,0,118) is roughly abreast of the player at the spare, which reads as "beside my gun," not "back to." Pinning the recommended silhouette to the −Z/−x elevated sightline, rather than any elevated Kerrax's-Hold position, is what makes the fix deliver the line's own geometry. **Like the Beat 1 rail-watcher, this recommendation currently has nothing to stand on.** `Ch4BuildKerraxHoldDetails` builds only floor props; Kerrax's Hold, like every room in this chapter, has no ledge geometry. The −Z/−x sightline above needs a companion ledge/catwalk folded into `Props.TrophyKeepDetailKit` (§4 Beat 5 §c) — the same fix §4 Beat 1 §c recommends for the Throat's rail — without it, "a high ledge" and "the rail" are both dialogue pointing at empty air.
- **The Sink snatch-team is one enemy short of what the VO and screenplay both name.** Echo's `ch4_beat2_tessa_meet` line is literally *"That's the team. Four."* and the screenplay stages "Four Coil enforcers in salvage-plate push through" — but `Chapter4Builder.cs` spawns exactly three `BuildEnemy` instances for this fight (§4 Beat 2 §b/§d). This is distinct from the already-flagged "Kerrax fought alone, no Coil muscle" gap in §4 Beat 5 §d — it is a count mismatch in a beat the doc itself once described inconsistently (§4 Beat 2 §a now reconciles this). **The count mismatch surfaces a second time, independently, in Kerrax's own Beat 5 line.** `ch4_beat5_kerrax_confront`'s opening line has him say *"You've cost me four good enforcers and a courier I had three-quarters sold"* (§4 Beat 5 §e) — a later, retrospective reference to the same headcount, spoken after the player has already fought and killed three. A fix touching only Echo's Beat 2 line still leaves Kerrax narrating "four" over a fight the player counted as three; both lines need to move together. Recommend either adding a fourth `BuildEnemy` at a fourth snatch-team position to match the line, or rewriting both Echo's and Kerrax's dialogue/the screenplay's count so "four" reads as one enforcer fled or held back off-screen rather than a player-countable discrepancy.
- **Tessa Rin has no exit.** The screenplay has her "slip into the churn of the Sink and is gone" after handing off the data; the builder never deactivates or removes her. She remains standing, visible, at her Sink stall for the rest of the chapter, including through the Beat 5 finale that takes place in an entirely different room. This is silently harmless (she's simply out of the player's way by then) but is a loose end worth closing with a `SetActive(false)` on the sabotage dialogue's completion, if a future pass touches Beat 2.
- **`EnemyArtWirer`'s Ch4 art mapping does not match this chapter's antagonists.** `EnemyArtWirer.cs`'s per-scene map assigns `Ch04_OverseersHunt` → primary `Dominion_Trooper`, secondary `Dominion_Scan-Drone`, applied by odd/even index parity to **every** `Enemy` component found in the scene — the Sink snatch-team, both hunter waves, *and Kerrax himself*. But per both the story bible and this chapter's own premise ("no Dominion garrison" in Drovis is the entire reason the hunt has to be outsourced to a bounty), none of Chapter 4's combatants are Dominion troops — they are Coil enforcers, and `Coil_Syndicate_Ganger.prefab` already exists in the same folder, used correctly by `Ch02_Auction`'s mapping. Worse, an odd-indexed melee `Enemy` body can end up wearing the `Dominion_Scan-Drone` mesh (a flying-drone shape) standing in for a humanoid swordsman. **Kerrax additionally never gets his own `Named.Kerrax.prefab` likeness** — his boss body is a generic `BuildEnemy` capsule, and the wirer's index-parity swap gives him a trooper or drone mesh instead of his own face, even though `Named.Kerrax.prefab` exists on disk and is used correctly for his `StoryNpc` displayName elsewhere. Flagging this content/wiring gap for awareness — it is not something this document's scope authorizes fixing (`EnemyArtWirer.cs` is a separate, cross-chapter tool), but any pass that touches Chapter 4's combat art should not assume the current wiring is correct.
- **Khall's `Named.Khall.prefab` likeness goes unused in his own naming chapter.** The prefab exists (and is used, correctly, as a hologram NPC in Chapter 1's Command Room), but Chapter 4's Mast relay hologram is a generic cyan `BuildHologram` primitive with no `StoryNpc`, no displayName, and no connection to the Khall asset at all. This may be intentional — "heard only as a voice" fits a faceless hologram arguably *better* than a likeness — but it is worth a deliberate call rather than an assumed one if a future pass adds a visual identity to the Mast reveal.
- **No parkour/climbing system in the Deepworks (§1.1, §4 Beat 4).** Restated here because it is the single biggest gap between the screenplay ("wall-runs, ledge-jumps, collapsing footing, drops over black water") and the as-built traversal (an open flat cave floor with static pillar obstacles and one shallow water hazard). This is a documented, deliberate design decision in the builder's own comments, not an oversight — but it means any future "make the Deepworks feel more like the screenplay" pass needs a genuinely new climbing/traversal system, which does not exist anywhere in the codebase today (the only comparable mechanic, `ZeroGGrabLocomotion`, is explicitly scoped to weightless interiors and was rejected for this use).
- **Canon soft spot (flagged, not fixed):** `story ouput/audit/Ch04_audit.md` found zero hard consistency errors and one soft watch-item concerning a later chapter's disclosure (not Chapter 4's own content) — not addressed here; see the audit directly. Separately, the same audit's four flagged "not X, that's Y" antithesis lines (Beat1/Resh, Beat2/Ronin-7, Beat3/Kessler, Beat5/Mera Voss) have already been thinned in `Chapter4Lines.cs` per the audit's own suggested rewrites — do not re-introduce the antithesis phrasing if touching those lines again.
- **Kerrax's `DuelYield.autoAcceptSeconds = 30`** means a player who defeats Kerrax to yield-state and then simply stands there (sword still drawn) for 30 seconds will have the mercy auto-accepted for them, advancing the mission whether or not they consciously chose to sheathe the blade. This is presumably an anti-softlock safety net, not a narrative statement that "waiting counts as mercy" — worth being aware of if the beat's design intent (a *deliberate, sober, slow* choice) is ever tightened.
- **Open question: the free-standing katana has no tether, so a player can reach Kerrax weaponless, and the mercy precondition is then already satisfied by default.** The katana at (2,1,0) is a loose grabbable the player must turn to find (§2, §4 Beat 0 §b); nothing prevents leaving it behind in the Throat. A player who never picks it up cannot drive `BladeDamager` against Kerrax's 260 HP, and `DuelYield`'s mercy precondition (`Grabbable.IsHeld == false`, §4 Beat 5 §d) is then trivially true from the start — the duel's own state contract assumes a held sword that may not exist. This is a cross-chapter gap shared by every grabbable-sword chapter, but it bites hardest here because the duel is the one encounter whose resolution keys on sword-held state; flagged as an open question, not a fix authorized by this document's scope.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All set-dressing props are cheap primitives tinted via the shared `TintShared` helper (shared-material batching) rather than unique materials, same as Chapter 1.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch4Environment.asset`. Currently set inline at the top of `BuildChapter4OverseersHunt` (`Chapter4Builder.cs:68-98`).

| | Value |
|---|---|
| Directional key | color (0.85, 0.75, 0.5), intensity 0.45, rotation Euler(55, -35, 0) |
| Ambient | mode **Flat**, color (0.1, 0.09, 0.09) |
| Fog | mode **Exponential**, color (0.05, 0.045, 0.045), density 0.04 |
| Floor tint | (0.17, 0.15, 0.14) |
| Ceiling tint | (0.08, 0.07, 0.07) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `ThroatLight0` | (-4, 2.6, 4) | (0.7, 0.5, 0.9) | 1.6 | 12 | none |
| `ThroatLight1` | (4, 2.6, 10) | (1, 0.7, 0.3) | 1.8 | 14 | none |
| `SinkLight0` | (-5, 2.4, 24) | (1, 0.3, 0.6) | 2 | 14 | none |
| `SinkLight1` | (5, 2.4, 34) | (0.2, 0.85, 0.9) | 2 | 14 | none |
| `MastLight0` | (0, 3, 54) | (0.5, 0.65, 1) | 2.2 | 16 | none |
| `DeepworksLight0` | (0, 2.2, 68) | (0.3, 0.5, 0.55) | 1 | 12 | `AddConsoleFlicker(seed: 44f)` |
| `DeepworksLight1` | (0, 2.2, 88) | (0.25, 0.45, 0.5) | 0.8 | 12 | none |
| `DeepworksLight2` | (0, 2.2, 104) | (0.2, 0.4, 0.5) | 0.7 | 10 | none |
| `KerraxLight0` | (-4, 2.6, 118) | (0.3, 0.55, 1) | 1.8 | 14 | `AddAmbientPulse(period: 6f)` |
| `KerraxLight1` | (4, 2.6, 126) | (0.3, 0.55, 1) | 1.8 | 14 | none |

### A.2 The Throat

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-7,7], z[-4,16], center (0,0,6), 14×20 | `BuildFloorCeiling(interior, "Throat", ...)` |
| Walls | `Throat_WallW` x=-7; `Throat_WallE` x=7; `Throat_WallFront` z=-4 (solid); `Throat_WallBack` z=16 (doorway, gap 3) | `BuildWall` / `BuildDoorwayWall` |
| Room details | center (0,0,6), half-extents (7,10), accent (0.4,0.35,0.5) | `BuildRoomDetails` |
| Decorative crowd | (-4,0,3), (4,0,5), (-3,0,11), (3,0,12) | `PlaceDecorativeCrowd` |
| Scan-drones | `ScanDrone_Throat0/1/2` — see §3.2 for positions/radii | `Ch4BuildScanDrone` |
| HunterWaveA spawn positions | (-4,0,12), (4,0,13) | `BuildEnemy`, inactive |
| Player spawn | (0, 0, 2) | `BuildRig(addLocomotion:true)` |
| Katana "Echo" | (2, 1, 0), Euler(-90,0,0) | `BuildSword(..., Ch4EchoBladePrefab)` |
| Kessler / Iris / Resh | (-1.5,0,3) / (1.5,0,3) / (0,0,5) | `Ch4PlaceStoryNpc`, wander 0.6/0.6/0.8 |
| Dialogue players | `Dialogue_Beat0_Briefing` (0,1,1); `Dialogue_Beat1_Throat` (0,1,8) | `Ch4BuildDialogue` |
| Mission steps | indices 0–1 of 23 | `AuthorDialogueStep(0, dlgBriefing)`, `AuthorDialogueStep(1, dlgThroat)` |

### A.3 The Sink

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-9,9], z[16,42], center (0,0,29), 18×26 | `BuildFloorCeiling(interior, "Sink", ...)` |
| Walls | `Sink_WallFront` z=16 (doorway, gap 3); `Sink_WallBack` z=42 (doorway, gap 3); `Sink_WallW` x=-9; `Sink_WallE` x=9 | `BuildDoorwayWall` / `BuildWall` |
| Room details | center (0,0,29), half-extents (9,13), accent (0.5,0.2,0.4) | `BuildRoomDetails` |
| Sink stalls ×4 | counters (-6,0,22)/(6,0,24)/(-6,0,34)/(6,0,36), canopies 1.4m above | `Ch4BuildSinkStalls` |
| Tessa's fence-stall | counter (3,0.5,33), screen (3,1.4,33.5) | `Ch4BuildSinkStalls` |
| Decorative crowd | (-5,0,20), (5,0,22), (-6,0,36), (6,0,38) | `PlaceDecorativeCrowd` |
| Tessa Rin | (3, 0, 32), no wander | `Ch4PlaceStoryNpc` |
| Sink snatch-team ×3 | (-3,0,30), (3,0,30), (0,0,34) | `BuildEnemy`, inactive |
| `SinkReachPoint` | (0, 1, 20), radius 5 | `AuthorReachStep(2, ...)` |
| `SinkDreadAmbience` | (-5, 2.4, 29), inner4/outer14/vol0.4 | `BuildAmbienceLayer` |
| Dialogue players | `Dialogue_Beat2_TessaMeet` (3,1,31); `Dialogue_Beat2_Sabotage` (0,1,36) | `Ch4BuildDialogue` |
| Mission steps | indices 2–5 | `AuthorReachStep(2)` · `AuthorDialogueStep(3, dlgTessaMeet)` · `AuthorDefeatStep(4, sinkEnemyHealths)` · `AuthorDialogueStep(5, dlgSabotage)` |

### A.4 The Mast

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-5,5], z[42,60], center (0,0,51), 10×18 | `BuildFloorCeiling(interior, "Mast", ...)` |
| Walls | `Mast_WallFront` z=42 (doorway, gap 3); `Mast_WallBack` z=60 (doorway, gap 3); `Mast_WallW` x=-5; `Mast_WallE` x=5 | `BuildDoorwayWall` / `BuildWall` |
| Room details | center (0,0,51), half-extents (5,9), accent (0.3,0.4,0.6) | `BuildRoomDetails` |
| Hologram (Khall's relay voice) | (0, 0, 58) — cyan column + node, `FloatingArrow` shimmer | `BuildHologram` |
| `RelayDrone` | (1.5, 2.4, 55), tint (0.5,0.7,1) | `BuildShipDrone` |
| `MastReachPoint` | (0, 1, 46), radius 4.5 | `AuthorReachStep(6, ...)` |
| Dialogue players | `Dialogue_Beat3_Khall` (0,1,57); `Dialogue_Beat3_Aftermath` (0,1,58) | `Ch4BuildDialogue` |
| Mission steps | indices 6–8 | `AuthorReachStep(6)` · `AuthorDialogueStep(7, dlgKhall)` · `AuthorDialogueStep(8, dlgAftermath)` |

### A.5 The Deepworks

| Element | Coordinates / value | Component / method |
|---|---|---|
| Cave floor (no walls/ceiling) | local (0,-0.1,86), scale (16,0.2,52) → x[-8,8], z[60,112] | inline `PrimitiveType.Cube`, tint (0.1,0.09,0.09) |
| Cave pillars ×6 | (-4,1.1,66), (3.5,1.3,74), (-4.5,1,82), (2.5,1.6,90), (-2,1.2,98), (3,1,104) | `Ch4BuildDeepworksProps` → `BuildProp("CavePillar", ...)`, tint (0.13,0.12,0.11) |
| Scan-drones | `ScanDrone_Deepworks0/1` — see §3.2 | `Ch4BuildScanDrone` |
| `Deepworks_RisingWater` | trigger (0,-0.3,106) size(10,3,8); rises local Y -1.5→-0.2 over 20s; 3 dmg/1.5s tick | `FloodingWaterHazard`, `Configure(playerHealth)` |
| HunterWaveB spawn positions | (-3,0,66), (3,0,67), (0,0,70) | `BuildEnemy`, inactive |
| Reach points | `DeepworksEntranceReachPoint` (0,1,63) r4; `DeepworksMidReachPoint` (0,1,86) r6; `DeepworksDepthReachPoint` (0,1,107) r6 | `AuthorReachStep(9/11/13, ...)` |
| Dialogue players | `Dialogue_Beat4_DescentIntro` (0,1,62); `Dialogue_Beat4_DescentCalls` (0,1,86); `Dialogue_Beat4_DescentEnd` (0,1,108) | `Ch4BuildDialogue` |
| Mission steps | indices 9–14 | `AuthorReachStep(9)` · `AuthorDialogueStep(10, dlgDescentIntro)` · `AuthorReachStep(11)` · `AuthorDialogueStep(12, dlgDescentCalls)` · `AuthorReachStep(13)` · `AuthorDialogueStep(14, dlgDescentEnd)` |

### A.6 Kerrax's Hold

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-7,7], z[112,132], center (0,0,122), 14×20, dead end | `BuildFloorCeiling(interior, "KerraxHold", ...)` |
| Walls | `KerraxHold_WallFront` z=112 (doorway, gap 4 — widest in the chapter); `KerraxHold_WallBack` z=132 (solid); `_WallW` x=-7; `_WallE` x=7 | `BuildDoorwayWall` / `BuildWall` |
| Room details | center (0,0,122), half-extents (7,10), accent (0.2,0.35,0.55) | `BuildRoomDetails` |
| Plundered plate / trophy crates / relay console | see §4 Beat 5 art table | `Ch4BuildKerraxHoldDetails` |
| Kerrax | (0, 0, 122), `Ch4Kerrax.asset` (260 HP / 18 dmg / 1 spd / 1s cooldown); `DuelYield` yieldThreshold 0.2, autoAcceptSeconds 30 | `BuildEnemy` + `Ch4EnsureKerraxDefinition` + `DuelYield`; built inactive |
| Mera Voss | (-5, 0, 118), no wander | `Ch4PlaceStoryNpc`; built inactive |
| `KerraxHoldReachPoint` | (0, 1, 116), radius 5 | `AuthorReachStep(15, ...)` |
| `KerraxHoldAmbience` | (-4, 2.6, 122), inner4/outer14/vol0.4 | `BuildAmbienceLayer` |
| `CHAPTER 4 COMPLETE Canvas` | (0, 1.4, 128), inactive | `Ch4BuildCompleteCanvas` |
| `ChapterOutro` | (0, 1, 126), inactive; flag `"ch4_complete"` | `ChapterOutro` + `CampaignFlagSetter` |
| Dialogue players | `Dialogue_Beat5_KerraxConfront` (0,1,117); `Dialogue_Beat5_Mercy` (0,1,121); `Dialogue_Beat5_MeraRecruit` (0,1,122) | `Ch4BuildDialogue` |
| Mission steps | indices 15–22 | `AuthorReachStep(15)` · `AuthorDialogueStep(16, dlgKerraxConfront)` · `AuthorTriggerStep(17, kerraxGo)` · `AuthorPromptStep(18, null)` · `AuthorTriggerStep(19, meraGo)` · `AuthorDialogueStep(20, dlgMercy)` · `AuthorDialogueStep(21, dlgMeraRecruit)` · `AuthorTriggerStep(22, outroGo)` |

### A.7 Chapter-wide systems (heat, waves, sword, XR infra)

| System | Value | Component / method |
|---|---|---|
| Player weapon | katana at (2, 1, 0), Euler(-90,0,0), `Named.Echo` visual | `BuildSword(..., weapon, Ch4EchoBladePrefab)` |
| `HeatMeter` | wrist-anchored (left hand), gain 0.6/detection, decay 0.04/s, thresholds {0.5, 1.0} | `HeatMeter` on a standalone `HeatMeter` GameObject |
| `HunterWaveA` | trigger (0,1,10) r5000 (heat-gated, not position-gated); 2 enemies | `BuildWaveSpawner`, wired to `heatMeter.OnThresholdEvent(0)` |
| `HunterWaveB` | trigger (0,1,68) r5000; 3 enemies | `BuildWaveSpawner`, wired to `heatMeter.OnThresholdEvent(1)` |
| `Ch4Kerrax.asset` | maxHealth 260, damage 18, moveSpeed 1, attackCooldown 1 | `Ch4EnsureKerraxDefinition` → `Assets/Ronin7/Data/Ch4Kerrax.asset` |
| Default `EnemyDefinition` | maxHealth 60, damage 12, moveSpeed 1.4, attackCooldown 0.8 | `EnsureEnemyDefinition` (shared across chapters) |
| XR UI infra | `XRInteractionManager`, XR UI Event System, right-hand ray interactor | `EnsureXRUIEventSystemMenu`, `WireRightHandRayInteractorMenu` |
| Post-process retrofit | `SinkDreadAmbience`, `KerraxHoldAmbience`, `ProceduralAudioClipBuilder.AssignGeneratedClips`, `AddConsoleFlicker("DeepworksLight0", 44)`, `AddAmbientPulse("KerraxLight0", 6)`, `ReverbZonePlacer.AutoTagInteriorVolumes()` + `.PlaceReverbZonesForInteriorVolumes()` | end of `BuildChapter4OverseersHunt` |

### A.8 Scene root hierarchy (current)

`BuildChapter4OverseersHunt()` creates these as **siblings**, not nested: `Directional Light`, `Drovis` (the Throat/Sink/Mast/Kerrax's Hold geometry, props, NPCs, drones), `Deepworks` (the open cave floor, pillars, water hazard), the ten accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig (with `HeatMeter` and the Sword as descendants/siblings), `HunterWaveA`/`HunterWaveB` + their trigger points, six reach-point GameObjects, twelve dialogue-player roots, the `CHAPTER 4 COMPLETE Canvas`, `ChapterOutro`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots, and moves `Drovis`'s and `Deepworks`'s art contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Character and enemy prefabs resolve; every environment prefab is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Kessler` | `Art/Generated/Characters3D/Named/Kessler.prefab` | **EXISTS** |
| `Named.Iris` | `Art/Generated/Characters3D/Named/Iris.prefab` | **EXISTS** |
| `Named.Resh` | `Art/Generated/Characters3D/Named/Resh.prefab` | **EXISTS** |
| `Named.TessaRin` | `Art/Generated/Characters3D/Named/Tessa-Rin.prefab` | **EXISTS** |
| `Named.MeraVoss` | `Art/Generated/Characters3D/Named/Mera-Voss.prefab` | **EXISTS**, but the silhouette variant needs a visibly aimed weapon prop, not just elevation (§4 Beat 5 §b/§9) |
| `Named.Kerrax` | `Art/Generated/Characters3D/Named/Kerrax.prefab` | **EXISTS**, but not wired to the in-scene boss body (§9) |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Named.Khall` | `Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS**, but unused in this chapter (§9) — Khall is a generic hologram primitive here |
| `Enemies.DominionTrooper` | `Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** — wired by `EnemyArtWirer`, but see §9 for the mismatch |
| `Enemies.DominionScanDrone` | `Art/Generated/Characters3D/Enemies/Dominion_Scan-Drone.prefab` | **EXISTS** — wired by `EnemyArtWirer`, but see §9 for the mismatch |
| `Enemies.CoilGanger` | `Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** — the correct narrative fit, currently unused by Ch4's `EnemyArtWirer` mapping |
| `Rooms.ThroatShell` | `Art/Generated/Rooms/ThroatShell.prefab` | MISSING |
| `Rooms.ThroatArch` | `Art/Generated/Rooms/ThroatArch.prefab` | MISSING |
| `Rooms.SinkShell` | `Art/Generated/Rooms/SinkShell.prefab` | MISSING |
| `Rooms.SinkArch` | `Art/Generated/Rooms/SinkArch.prefab` | MISSING |
| `Rooms.MastShell` | `Art/Generated/Rooms/MastShell.prefab` | MISSING |
| `Rooms.MastArch` | `Art/Generated/Rooms/MastArch.prefab` | MISSING |
| `Rooms.DeepworksFloor` | `Art/Generated/Rooms/DeepworksFloor.prefab` | MISSING |
| `Rooms.KerraxHoldShell` | `Art/Generated/Rooms/KerraxHoldShell.prefab` | MISSING |
| `Rooms.KerraxHoldArch` | `Art/Generated/Rooms/KerraxHoldArch.prefab` | MISSING |
| `Props.DockDetailKit` | `Art/Generated/Props/DockDetailKit.prefab` | MISSING |
| `Props.BazaarDetailKit` | `Art/Generated/Props/BazaarDetailKit.prefab` | MISSING |
| `Props.RelayDetailKit` | `Art/Generated/Props/RelayDetailKit.prefab` | MISSING |
| `Props.TrophyKeepDetailKit` | `Art/Generated/Props/TrophyKeepDetailKit.prefab` | MISSING |
| `Props.SinkStall_Counter` | `Art/Generated/Props/SinkStall_Counter.prefab` | MISSING |
| `Props.SinkStall_Canopy` | `Art/Generated/Props/SinkStall_Canopy.prefab` | MISSING |
| `Props.StallScreen` | `Art/Generated/Props/StallScreen.prefab` | MISSING |
| `Props.DataSlate` | `Art/Generated/Props/DataSlate.prefab` | MISSING — recommended addition, §4 Beat 2 §c |
| `Props.GoBag` | `Art/Generated/Props/GoBag.prefab` | MISSING — recommended addition, §4 Beat 2 §c |
| `Props.CavePillar` | `Art/Generated/Props/CavePillar.prefab` | MISSING |
| `Props.RelayDrone` | `Art/Generated/Props/RelayDrone.prefab` | MISSING |
| `Props.PlunderedPlate` | `Art/Generated/Props/PlunderedPlate.prefab` | MISSING |
| `Props.TrophyCrate` | `Art/Generated/Props/TrophyCrate.prefab` | MISSING |
| `Props.RelayConsole` | `Art/Generated/Props/RelayConsole.prefab` | MISSING |
| `Props.DescentRig` | `Art/Generated/Props/DescentRig.prefab` | MISSING — recommended addition, §4 Beat 4 §a/§c |
| `Vfx.HologramProjection` | `Art/Generated/VFX/HologramProjection.prefab` | MISSING |
| `Vfx.RisingWaterSurface` | `Art/Generated/VFX/RisingWaterSurface.prefab` | MISSING — must be translucent, not opaque (§4 Beat 4 §c) |
| `Vfx.ScanDroneBody` | `Art/Generated/VFX/ScanDroneBody.prefab` | MISSING (currently a shared `BuildShipDrone` primitive) |
| `Vfx.WeldingSparks` | `Art/Generated/VFX/WeldingSparks.prefab` | MISSING — recommended addition, §4 Beat 2 §c |

**Reuse notes.**

- `Props.SinkStall_Counter` serves both the four generic bazaar stalls and Tessa's fence-stall variant — the same registry key, distinguished only by which props are layered on top (a screen vs. nothing).
- `Rooms.*Arch` keys are new for this chapter — Chapter 1 had no equivalent because every one of its room boundaries was a locking `SlidingDoor`, not an open archway (§2).
- **`Enemies.DominionTrooper`/`Enemies.DominionScanDrone`** are the keys `EnemyArtWirer.cs` actually wires into this scene today, by its hardcoded per-scene map — not `Enemies.CoilGanger`, which is the narratively correct choice and already exists unused. Folding the wirer's per-scene map into the registry (so a fresh build produces correctly-skinned Coil enforcers directly, and gives Kerrax his own likeness) is a natural follow-up to this refactor, but is **not** in its scope. See §9.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,Doors,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter4Builder.cs`, `Chapter4Lines.cs`, `ChapterSharedBuilders.cs`, `XRRigBuilder.cs`, `EnemyArtWirer.cs`, `Scripts/World/HeatMeter.cs`, `Scripts/World/ScanDroneVolume.cs`, `Scripts/World/FloodingWaterHazard.cs`, `Scripts/World/EnemyWaveSpawner.cs`, `Scripts/Combat/DuelYield.cs`, `Scripts/Player/ChapterOutro.cs`, `story ouput/Ch04_The_Overseers_Hunt.md`, `story ouput/Ch04_The_Overseers_Hunt_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`.*

# Chapter 8 — Scene Construction

*The architectural contract for `Ch08_SilentGarden.unity`: what Chapter 8 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 8 ("The Silent Garden") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter8Builder.cs`, entry point `XRRigBuilder.BuildChapter8SilentGarden()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 08 — The Silent Garden**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch08_The_Silent_Garden.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **Chapter entry scene:** the campaign enters Chapter 8 through `Assets/Ronin7/Scenes/Ch08_Prologue.unity`, not `Ch08_SilentGarden.unity` directly. It is the standard `ChXX_Prologue` ship scene (`ShipPrologueBuilder.cs`'s `Prologues` table, `id = "CH08"`) — Kessler alone, two lines ("A garden where nobody speaks above the wind. Khall's grief grows here." / "Walk it quiet, Cipher. Some answers only come to those who stop swinging."), then a DESCEND console that chains via `StoryTransition.LoadOnFootScene` into `Ch08_SilentGarden.unity`, the scene this whole document specs. This document does not cover `Ch08_Prologue.unity`'s own build (it is generic, shared machinery — §1.2's scope-discipline list already frozen it); see Beat 0(a) for how its existence bears on the in-garden Beat-0 briefing.
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled headstone or a barrow that reads the wrong size breaks the "endless graveyard" illusion the whole chapter depends on. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The Warden fight is this chapter's only combat encounter of consequence; its impact feedback comes from `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never from moving the camera. Same rule applies to the buried-guardian waves in Beat 2.
- **Traversal in Ch8 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with the standard comfort vignette. There is **no teleport locomotion in the outdoor world and no NavMesh anywhere** — the one exception is the **comfort-safe instant teleport** `MemoryDiveController` performs into/out of the Vision Dive island (§4, Beat 4), which is a deliberate non-lerp position/rotation set, not player-driven locomotion. Do not introduce any other teleport, parkour, or wall-run mechanic when patching this scene.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | ground/headstones/barrow shells, props, VFX, backdrops, decorative lights | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | reach points, the riddle trial, enemy spawns (buried + Warden), the Mourners' activation, dialogue players, prompts, the vision-dive wiring, mission-spine steps | `[BEAT_N_LOGIC]` |

**Chapter 8 has no doors**, so unlike Chapter 1's door idiom there is no object that spans both categories by contract — the closest analogue is the `RiddleTrial` gate object itself (`GraveRiddleTrial`), whose answer-pad *props* are art (`Ch8BuildAnswerPad`'s stone + label) but whose pass/fail logic (`RiddleTrial`/`RiddleTrialLogic`, the `onWrongAnswer`/`onRightAnswer` wiring) is logic. Art builds the pads; logic decides what standing on one means.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildWaveSpawner`, `BuildDialoguePlayer`, `Author*Step` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, plus the chapter-agnostic runtime components `RiddleTrial`/`RiddleTrialLogic` (`Scripts/World/Story/`) and `MemoryDiveController`/`MemoryFlashbackController` (first authored for Ch3's Kethel-7 playback, reused verbatim here).
- **Free to restructure:** the Ch8-local helpers, all prefixed `Ch8` and called only from `BuildChapter8SilentGarden()` — `Ch8EnsureBuriedDefinition`, `Ch8EnsureWardenDefinition`, `Ch8BuildDialogue`, `Ch8WireVoiceClips`, `Ch8ScatterHeadstones`, `Ch8BuildAnswerPad`, `Ch8BuildWarden`, `Ch8BuildMournersRing`, `Ch8BuildVisionRooms`, `Ch8BuildGhostFigure`, `Ch8BuildVisionLight`, `Ch8BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs` or the `RiddleTrial`/`MemoryDiveController` runtime components, stop — you have left Chapter 8 and are now silently rebuilding thirteen other chapters (and Chapter 3, which also depends on `MemoryDiveController`).

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** The same two ScriptableObjects introduced by the Chapter 1 document carry everything this builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch8Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, ground + headstone tint, per-zone accent lights |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared across all 14 chapters — Ch8 adds new `Rooms.*`/`Props.*`/`Vfx.*` keys to the same asset Ch1 established) |

**Neither exists yet, same as Chapter 1.** Prefab **paths never appear in builder code.** The builder asks the registry for `Props.HeadstoneGeneric`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching the folder layout Ch1 established:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Echo, Khall, The-Warden, The-Mourners all live under Named/)
  Rooms/                                    ← used by the vision-dive rooms and the exterior ground slab (Ch8 has no walled exterior rooms, but `Rooms.SilentGardenGround` lives here too — see Beat 1c/Appendix B)
  Props/                                    ← new: headstones, the barrow, the riddle pads, vision-dive dressing
  VFX/                                      ← new: fog gate, running-fog treatment
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

Identical contract to Chapter 1 (§1.4 of `Ch01-Scene-Construction.md`), reproduced here for a builder working from this document alone: the builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**, and the wipe strategy must be converted from `EditorSceneManager.NewScene(...)` (`Chapter8Builder.cs:85`, same discard-the-whole-scene pattern as every other chapter builder) to an open-and-selectively-`DestroyImmediate` strategy before that root can mean anything. **As of this writing, Chapter 8 has not been converted either** — this is a chapter-spanning refactor, not something to solve locally in `Chapter8Builder.cs`.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for Chapter 8.** No headstone, no barrow shell, no fog-gate marker, no riddle-pad stone, no vision-dive room dressing. See Appendix B for the full inventory: four keys resolve (all shared, Named-character prefabs, two of them procedural placeholders); everything else is a commission.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently — this is the same guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`) and the same rule Ch1's document establishes. The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame); `QualityBootstrap`'s default of 72 Hz is the floor actually shipped against today (same framing as Ch1 §1.6).
- **No recorded greybox baseline exists for Ch8 yet** in `Project/Docs/CHAPTER-BUILD-LEDGER.md` at the time of writing — unlike Chapter 1's measured 189 drawCalls / 17 setPassCalls / 9,198 tris / 13,092 verts, Chapter 8 has not had an edit-mode `UnityStats` pass recorded. **Flagging this as an open task**, not asserting a number: before any prefab swap lands, capture a baseline the same way Ch1's was captured, then treat it as the bar to protect.
- Chapter 8's headstone field (`Ch8ScatterHeadstones`, two rows spanning z=14 to z=62 at 4 m spacing — the loop runs 13 iterations → 13 pairs, 26 headstone primitives, exactly matching Appendix A.2) is the largest single primitive cluster in the scene, though at 26 instances (not the 50 an earlier draft of this section cited) a naive one-prefab-per-headstone swap is a smaller risk than previously framed. GPU instancing or a batched multi-mesh prefab is still worth considering for the headstone field specifically, rather than 26 independent prefab instances, but this is prudent headroom, not an urgent bottleneck.
- All set-dressing today is tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) exactly as in Chapter 1 — prefabs replacing it will not batch this way and must be re-measured.

## 2. Chapter spatial map

Chapter 8 is **one continuous open-air scene**, `Assets/Ronin7/Scenes/Ch08_SilentGarden.unity` — **no rooms, no walls, no ceilings, and no doors anywhere in the outdoor world.** This is a structural departure from every room-based chapter (Ch1's four-room corridor, etc.): the Silent Garden is a single flat ground plane the player walks across in +Z, with zones defined by mission-spine reach points and prop clusters rather than by geometry that blocks movement. The one exception to "no rooms" is the **Vision Dive island** (§4, Beat 4), a small pair of walled rooms built far off the main plain's Z-axis and reached only by a comfort-safe teleport, never by walking.

```
 -Z (behind spawn)                                                                          +Z (deep garden)
 Player spawn / Briefing   Fog Gate       Grave-Paths + Riddle Trial      Deep Garden        Warden Arena   The Still Center
 z≈0-4                     z=10           z=14-42 (pads z=35)            z=44-64             z=66           / Barrow, z=68
 (voice-only, no geo)      (marker only,  (headstone field both sides;   (reach point         (The Warden,   (Barrow mound;
                            no collider)   answer pads; buried spawn      z=60)                inactive       Mourners ring,
                                           z=40-42)                                             until step 9)  inactive until
                                                                                                                 step 11)

                                                                                    ⇢ teleport only, not on this axis ⇢

                                                                          Vision Dive Island (offset to z=250-280)
                                                                          Vision A: the sterile room, world z≈250-260
                                                                          Vision B: the handler's bay, world z≈265-279
```

Ground plane: a single tinted `GardenGround` cube, center `(0, -0.5, 45)`, scale `(30, 1, 100)` → spans **x[-15,15], z[-5,95]**, top surface at y=0. This is the entire walkable outdoor world; there is no second floor slab anywhere in the plain.

| Beat | Zone | Approx. footprint | Key anchor(s) |
|---|---|---|---|
| 0 | The Cairn (briefing) | no physical geometry — voice-only | dialogue anchor (0,1,4) |
| 1 | The Gate of Fog | fog-gate marker at z=10 | `FogGateWall` (0,1.8,10); dialogue anchors (0,1,9) and (0,1,15) |
| 2 | The Grave-Paths / Trial of Mind | headstone field x≈±4 (jittered ±2.5), z=14–62 | `GravePathReachPoint` (0,1,25) r5; answer pads (±2.5,0,35); buried spawns z=40–42; wave-spawner trigger (0,0,38) r10 |
| 3 | The Deep Garden / Warden Arena | z=44–68 | `DeepGardenReachPoint` (0,1,60) r6; `The Warden` (0,0,66) |
| 4 | The Still Center / Barrow | barrow mound center (0,-1,68) | `Barrow` sphere; `MournersRing` center (0,0,68) r5, 4 figures |
| 4 (dive) | Vision Dive Island | offset entirely off-axis | dive root (0,0,250); Vision A room center (0,0,255); Vision B bay center (0,0,272); `VisionEntryPoint` (0,1,252); `VisionExitPoint` (6,1,68) |
| 5 | The Leaving / Reunion | back near the gate, z=11–13 | `GateReturnReachPoint` (0,1,12) r5; `ChapterOutro`/complete canvas (0,1,13) |

**These anchor positions are load-bearing and survive the refactor unchanged.** A ground-shell prefab or headstone-field prefab must fit the plain's footprint exactly; the spatial map is the contract, not the prefab's convenience.

**Doors** — **none.** Chapter 8 has no `BuildSlidingDoor` call anywhere in `Chapter8Builder.cs`. The gate is entirely narrative: `FogGateWall` is a plain tinted cube at (0, 1.8, 10) with its `Collider` explicitly stripped (`Object.DestroyImmediate(fogGateGo.GetComponent<Collider>())`) — it is a **visual marker the player walks straight through**, not a physical or logical barrier. The "the crew is held at the gate" beat is entirely a dialogue-and-VO event (Beat 1's `Dialogue_Beat1_Gate` set); no `Trigger`/lock-state component enforces it. If a future pass wants the gate to visibly resist the player before the Beat-1 dialogue completes, that requires new logic — not present today.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` and every previously-shipped ability via `AttachPlayerAbilities` — Ch8 ships **no new ability**; weakpoint-sight (earned Ch7) is present and self-gates on `CampaignState.HasAbility`, and is the showcase mechanic for the Warden fight. `ZoneBounds` is set to **center (0, 3, 125), radius 180** — large enough to cover both the outdoor plain (z 0–95) and the offset Vision Dive island (z up to ≈280), because `ZoneBounds` clamps XZ every frame regardless of dive state (a lesson carried forward from Chapter 7's precedent, per the builder's own comment).

## 3. Global environment & backdrop

**The Silent Garden reads as the opposite of every prior chapter's setting.** Per the dialogue script's SETTING block: "there is no sky... light has no source; it is just present, flat and grey, the light of an overcast with no cloud above it." Sound design is deliberately the inverse of Chapter 7's undertone — "where the reliquary was a chorus that never went quiet, the Garden is a silence so total it has pressure, broken only by Ronin-7's own footfalls, his own breath, and the Mourners' voice." **Treat the fog as an active character, never as weather** — it is "the Garden's hand": it holds the crew at the gate, opens paths on a true answer, draws back to raise the Warden, and at the reveal becomes the literal window into both visions.

**This chapter's signature visual — the waist-high standing fog itself — is not yet a distinct built element.** Per the dialogue script's SETTING block, "fog stands waist-high over the grave-rows and never moves unless the place wants it to," parting one pace ahead of the player and closing behind, and recoiling "like a tide pulled out" to bare the barrow before the Warden fight. None of that exists in `Chapter8Builder.cs` today: the only fog the scene actually has is the uniform, static `RenderSettings` exponential fog (§3.1) plus the collider-less `FogGateWall` marker (Beat 1c). There is no waist-high ground-hugging fog layer, and no fog *motion* of any kind — parting, closing, and recoiling are all narrative-only. A `Vfx.GroundFog` registry key (a waist-high volumetric/particle layer over the plain, added to Beat 1c and Appendix B below) is the commission target; until it lands and gains scripted behavior, treat every "the fog does X" line in this section as intent, not implementation — the same honesty already applied to the Warden's unmechanized phase fight (§4, Beat 3d).

**`Vfx.GroundFog` has a specific, load-bearing Beat-3 configuration the commission must account for, not just "a waist-high layer over the plain."** At the riddle trial's end, canon has the fog "recoil, drawing back across the ground like a tide pulled out," and for the Warden fight it settles into an arena shape: "the fog has drawn back into a low ring around a bare circle of grave-ground, an arena with the barrow at its head... the fog-ring is the arena boundary." Whoever builds the `Vfx.GroundFog` VFX must give it a Beat-3 state — the layer clears a bare circle around the barrow (0,-1,68) and settles into a ring at that circle's edge — not just uniform haze over the headstone field. The fog is "the Garden's hand" (above); its withdrawal *is* the transition into the boss, and the fog-ring is the only "arena wall" a wall-less chapter has for the Warden fight (§4, Beat 3d).

**A fourth scripted state closes the fog's arc at Beat 5: the exit aisle.** Canon does not leave the Beat-3 ring standing once the barrow's business is done — the dialogue script's Beat 5 stage direction has the Mourners' ring part and the fog "stand[ing] open in a long aisle running back the way he came, toward the gate." The commission target for `Vfx.GroundFog` needs this as its fourth and final state, alongside the Beat-1 hold, the Beat-2 recoil/open, and the Beat-3 arena-ring: on the walk out, the fog opens a straight passable corridor from the barrow back to z=10, the bookend to Beat 1's fog wall holding the crew at the gate. Without it the one fog beat with no scripted treatment is the exit — the chapter would end on fog reverting to uniform haze instead of deliberately opening the path home. See Beat 5c below for where this lands in the per-beat table.

**Two more SETTING details have no home in the build yet (marginal, flagged for the art/audio pass).** Canon's "wet grass that makes no noise underfoot" (Beat 1's stage direction — a ground texture plus a footstep-audio treatment) and "cold the way a held breath is cold" (the opening line of this section — breath-vapor VFX or a dedicated cold-grade cue) are strong sensory hooks absent from both `GardenGround` (a flat dark-grey tinted cube with no grass texture or footstep override) and `Ch8Environment.asset` (the cold reads only from the palette, §3.1). Neither is required for the chapter to read correctly; both are cheap wins worth an explicit line item so a future pass treats them as intended, not incidental.

**Canon tension worth surfacing, not resolving here:** "makes no noise underfoot" (Beat 1) and the SETTING block's own thesis — the silence is "broken only by Ronin-7's own footfalls, his own breath, and the Mourners' voice" — are in tension. Read literally, "no noise underfoot" argues for a *suppressed* footstep treatment; but the SETTING block names footfalls and breath as the deliberate audible pulse of the whole chapter, not things to mute. When this lands, lean **muffled, not muted**: soft, damp footfalls plus a low, audible breath layer, quiet enough to read as "wet grass" rather than boot-on-gravel, but present enough to be the body-as-pulse the SETTING block calls for. A future pass that reads "makes no noise" too literally and strips footfall/breath audio entirely would remove the very cue canon calls the chapter's heartbeat.

The scene's directional light is deliberately weak and flat — intensity 0.3 (a third of Chapter 1's 0.9) with a desaturated grey-blue tint (0.6, 0.62, 0.66) — married to a dense exponential fog (density 0.032, nearly double Ch1's 0.018) so nothing reads with a hard shadow or a horizon line. This is the "endless headstones running off into grey in every direction" the dialogue script calls for: fog density, not draw distance, is what limits the player's sightline.

**CREW-PRESENCE DECISION (carried from the builder's own class-summary comment, load-bearing for this document):** unlike Ch4/Ch5's landing parties, **nothing of the crew — Kessler, Coral, Mera, Morrigan, Iris, Resh, Mira — gets physical placement anywhere in this scene.** Every crew line, at the Beat-0 briefing and the Beat-5 reunion, is a voice-only `DialoguePlayer`. This extends Chapter 6's "no body in the scene" convention (there used for a solo climb) chapter-wide, for a solo trial. **Do not add crew NPC prefabs to this chapter** — it would contradict both the canon ("the crew is held at the gate as surely as a wall") and the builder's explicit design decision.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter8Builder.cs` in the target state.** The builder reads `Assets/Ronin7/Data/Ch8Environment.asset`. Its schema mirrors Ch1's exactly (same asset type, new instance):

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `groundTint`, `headstoneTint`, `barrowTint`, `fogGateTint` | `Color` | `Ch8ScatterHeadstones`, the ground/barrow cubes |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` for `GateLight`/`TrialLight`/`BarrowLight0`/`BarrowLight1` |

`behaviour` is the same enum as Ch1 — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("TrialLight", seed: 88f)` and `AddAmbientPulse("BarrowLight0", periodSeconds: 6.6f)` calls with data. **Ch8 has no `eventLights[]` entries** — unlike Ch1's `DockingAlarmLight`, nothing in this chapter is an inactive-until-triggered *light*; the Mourners' manifestation (step 11) and the Warden's activation (step 9's `DefeatEnemies` setup) are handled by activating GameObjects, not by flipping a light's active state.

The four accent-light entries are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1**.

**Material / tint palette:** identical convention to Chapter 1 — cheap primitives tinted via `TintShared` (MaterialPropertyBlock batching), not unique materials, to keep draw calls low and regeneration cheap. Prefabs replacing them must carry their own materials and will not batch this way (§1.6).

## 4. Per-beat scene spec

The chapter plays as six beats along a mostly-linear +Z path, with one off-axis teleport detour (the vision dive) inside Beat 4. Each beat is documented with the same a–f structure used in the Chapter 1 document.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the chapter's Y-invariant (every ground-level object sits at y=0, since the plain is flat and unlike Kessler's Ch1 `kesslerFloorY`, **no character in Ch8 walks via `NpcWalker`** — see §5).
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes, cylinders, or hardcoded sizes for any props. You must read from the centralized `ArtAssetRegistry` ScriptableObject for all environment prefabs. Create separate methods: **`BuildBeat0Art()`** for static environment/prefabs (there are none — this beat is pure voice-over), and **`BuildBeat0Logic()`** for the dialogue player and its mission-spine step.

#### a. Narrative purpose & emotional target

**Quoting note:** unless cited as `Chapter8Lines.cs`, the quotes in this section cite the screenplay/dialogue script for their narrative intent, not necessarily the wired VO — cross-check `Chapter8Lines.cs` before treating any of them as what the player literally hears, per the quoting rule this beat's own audit note (e, below) states.

This is the last beat of the chapter that happens with the whole crew present, and the last time in Act II the player hears the full ensemble voice before Ronin-7 is cut off from all of them but Echo. Coral names the legend she has "never let myself believe in" — the forebear's own hidden-life reverence surfacing for the first time as something she says *out loud*. The beat's job is to seed dread through **absence of information**: Mera can't price a threat with no walls or guns, Morrigan's instruments return nothing at all (screenplay: "I would rather face a wall with guns on it than a place my whole bench says is empty and a woman I believe says is not" — the wired `ch8_beat0_briefing` line, `Chapter8Lines.cs:125`, drops this phrasing entirely), and Iris reframes the graveyard's scale as intent, not death. Kessler's objection is voiced and then set down — the vow that recurs at the Beat-5 reunion is planted here: *"we hold the gate, you come back through it."* Echo's warning, heard by Cipher alone, is the chapter's real thesis statement — screenplay: *"the trained answer and the honest one are going to be two different things, and the trained one will get you killed"*; the wired `ch8_beat0_briefing` line (`Chapter8Lines.cs:133`) ends at "two different things," dropping the kill-clause.

**A structural note the narrative purpose above doesn't resolve on its own: this ensemble scene's canonical home is the ship, not the garden.** Canon's own Beat 0 / Scene 1 is explicitly "INT. THE CAIRN — COMMAND ROOM… the briefing, before the heading" — the stage direction places Coral at the holo-table over "a blank where a world should be," Ronin-7 "at the table's far side, the wrapped katana slung," the whole scene happening *before* the Cairn descends. But `Chapter8Builder.cs` plays the entire 14-line `ch8_beat0_briefing` set as voice-only at (0,1,4) inside `Ch08_SilentGarden` — i.e. on the fog plain, after arrival — while the crew debates whether to "set a heading" and "take us down." `Ch08_Prologue.unity` (§1.1), the scene that actually sits between the hub and this one, does not stage this scene either: it is a separate, generic, two-line Kessler-only descent hook, not a command-room recap, and it is not a stub — it is the same real, shared `ShipPrologueBuilder` machinery every other chapter uses. So the in-garden ensemble briefing is neither a deliberate recap of the prologue nor a straightforward duplicate to prune — it is canon's command-room scene, played in the wrong location: voice-over heard standing on the destination instead of staged aboard the departing ship. Whether that's an acceptable compression (the crew's voices carrying forward over the fog as Ronin-7 walks) or a gap worth closing (staging the command-room beat physically inside `Ch08_Prologue.unity`, with the 14-line set moved or split) is a decision this document flags, not makes — see §9, and note it is also the reason the garden's spawn/gate area has no ship or ramp to visually anchor a briefing that canon says happens elsewhere (§9's "no Cairn/ramp backdrop" question).

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** no scripted spawn override in `Chapter8Builder.cs` — `BuildRig` places the rig at its standard default, consistent with the Beat-0 dialogue anchor at (0, 1, 4) *(inferred: no explicit spawn transform is set for Ch8, unlike Ch1's exam-table spawn)*.
- **Dialogue anchor:** (0, 1, 4).
- **No physical geometry, no NPCs, no props** — this beat is entirely a voice-only ensemble scene per the CREW-PRESENCE DECISION (§3). Nothing in `BuildBeat0Art()` has anything to instantiate.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` (`ch8_beat0_briefing`) — the full-crew briefing plays in full |

**What changes during the beat:** nothing spatial — this is the one beat in the chapter with zero art, zero triggers beyond the single dialogue step, and zero state change. It exists purely to land the legend, the crew's unease, and Echo's warning before the descent.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

**Nothing to build.** This beat has no art table — it is the one beat in the chapter (and one of few in the whole game, alongside Ch1 Beat 0-equivalent hub scenes) that instantiates zero environment geometry of its own; it plays entirely over whatever the player is already standing in (the Ch8 world's spawn area, itself dressed by Beat 1's art).

#### d. Combat

None.

#### e. Dialogue / VO

Dialogue set id: **`ch8_beat0_briefing`**, position (0, 1, 4), 14 lines, ≈169 s total (summed from `Chapter8Lines.GetBeat0BriefingLines()`: 20+4+18+14+16+13+14+5+16+8+20+6+9+6). Advance input: Left-Hand **Talk** (Y), same `PromptInputAdvancer`/`DialoguePlayer` pattern as every prior chapter. Full speaker order: Coral Vex → Ronin-7 → Coral Vex → Mera Voss → Morrigan → Iris → Resh → Mira → Kessler → Ronin-7 → Echo → Ronin-7 → Coral Vex → Ronin-7 (per the dialogue script's Beat 0 and `Chapter8Lines.GetBeat0BriefingLines()`).

**Audit note (carried in `Chapter8Lines.cs`'s own doc comment, load-bearing for this section):** `story ouput/audit/Ch08_audit.md` graded the source screenplay **C on naturalness** — 0 em-dashes, 0 hard script/canon errors, but heavy antithesis/aphorism-stacking and a uniform register across very different speakers. The lines actually wired into this dialogue set are **rewrites**, not verbatim transcriptions of the screenplay in §"KEY SCENES"/the dialogue script: Mera/Resh/Iris's lines are roughened toward plainer, less quotable speech; Coral's "worse than blood" epigram and the Mourners' later "stronger... cleverer" stack are thinned; Mira's blunt "you're sad" line (Beat 5) keeps its first sentence but softens the second. **When quoting Ch8 dialogue for any downstream purpose, quote `Chapter8Lines.cs`, not the screenplay file** — they diverge by design.

**A fourth-wall break in the wired opening line, flagged nowhere else in this document.** The wired `ch8_beat0_briefing` line 0 (`Chapter8Lines.cs:119`) has Coral *say*, in-scene, "I named the Silent Garden at the end of **Chapter 7**, and I told you I never let myself believe in it." — a character speaking a chapter number aloud. Neither the dialogue script's own Coral line (which uses the in-world referent "at the end of all that") nor the screenplay's stage direction ("at the close of Chapter 7," a direction, not spoken dialogue) breaks diegesis this way, and no other line in the chapter names an out-of-world chapter number. In a level whose whole thesis is a pressurized, immersive silence (§3), this lands in the very first spoken word of the chapter. Recommend rewording the wired line to an in-world referent — the screenplay's "at the end of all that," or "after the reliquary" — consistent with how this document otherwise treats `Chapter8Lines.cs` as the authoritative, player-facing text.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — a pure dialogue beat, no combat feel to sell.
- Ambient bed: `SilentGardenWindAmbience` (built at (0, 2.2, 10), covering the whole approach) is already present in the world at build time and audible from the player's Beat-0 position, establishing "the silence has pressure" before a single word of Coral's line plays.
- No haptics scripted for this beat.
- Standard comfort vignette; the player is not expected to move during this beat, though nothing prevents it.

---

### Beat 1 — The Gate of Fog (Separation)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (the ground plain, the fog-gate marker, the near headstone rows, the katana at the hip) and **`BuildBeat1Logic()`** (the two dialogue players, the mission-spine steps). **The fog gate has no lock state and no `Trigger` step** — do not add one; the beat's gating is dialogue-paced, not door-paced.

#### a. Narrative purpose & emotional target

**Quoting note:** unless cited as `Chapter8Lines.cs`, the quotes in this section cite the screenplay/dialogue script for their narrative intent, not necessarily the wired VO — cross-check `Chapter8Lines.cs` before treating any of them as what the player literally hears, per the quoting rule flagged in Beat 0e's audit note above.

The Cairn sets down, the crew comes down the ramp, and the gate stops them "as surely as a wall" — a pressure with no visible surface. This is the first time since Chapter 3 that Ronin-7 walks toward an answer with no one at his back but Echo. Coral's line does the heaviest lifting: the screenplay reads *"Everything I know about the Program ends at this gate... you go through"* — the wired `ch8_beat1_gate` line (`Chapter8Lines.cs:147`) keeps the first half but replaces "you go through" with "From here you walk it the way the first seeker walked it, with whatever's in your own skull" — the forebear who spent a lifetime finding thresholds like this and turning back from every one, watching someone else finally cross one. Kessler's vow ("we hold the gate, you come back through it") is restated here in the moment it costs something, not just as a plan. Once through, Echo's private line to Cipher alone reframes the solitude — not fear of the Garden, but the strangeness of a threat neither of them can route around: *"First time since the playback it's only been us, walking at something."* The Mourners' first line closes the beat and sets the whole trial's rule in one utterance: step on a lie, and the dead who were slandered by it rise to answer for you.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** free-walks from the Beat-0 spawn area to the gate at z=10 — **no reach-point gate on this crossing**; the beat advances purely on dialogue completion, not on player position. This is a deliberate contrast with every reach-triggered crossing later in the chapter (Beats 2, 3, 5 all gate on a `ReachTrigger`).
- **Dialogue anchors:** `Dialogue_Beat1_Gate` at (0, 1, 9) (just short of the fog-gate marker at z=10 — the crew's last words before the gate seals); `Dialogue_Beat1_Alone` at (0, 1, 15) (past the gate — Echo's private line, then the Mourners' first appearance).
- **No lock state, no `Trigger` object for the gate itself.** The gate's "closing behind him" is narrated by the dialogue and the VO alone — `FogGateWall` never changes state (it has no `Trigger`/`Controller`/active-state wiring at all in `Chapter8Builder.cs`). *(Flagged for a future pass: if a builder wants the gate to visibly pulse or thicken on the crew's side once Ronin-7 crosses, that is new logic, not present today.)*

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Gate` (`ch8_beat1_gate`) — Mera, Coral, Kessler, Ronin-7's terse reply "I heard you. Hold the gate." |
| 2 | Dialogue | `Dialogue_Beat1_Alone` (`ch8_beat1_alone`) — Echo, Ronin-7, then the Mourners' first line, setting the trial's rule |

**What changes during the beat:** nothing in the set dressing. The only "event" is narrative: the crew's voice channel goes silent between the two dialogue sets (per the VO direction, "Comm's gone"), which is carried entirely by the absence of any further crew lines for the rest of the chapter except the Beat-5 reunion.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `GardenGround` (whole-plain slab) | center (0,-0.5,45) | `Rooms.SilentGardenGround` | `…/Art/Generated/Rooms/SilentGardenGround.prefab` | **MISSING** |
| `FogGateWall` (marker, collider stripped) | (0, 1.8, 10) | `Vfx.FogGateWall` | `…/Art/Generated/VFX/FogGateWall.prefab` | **MISSING** |
| `GroundFog` (waist-high layer over the whole plain — see §3) | whole plain, y≈0–1, z[0,95] | `Vfx.GroundFog` | `…/Art/Generated/VFX/GroundFog.prefab` | **MISSING** *(no registry key or art row exists in the as-built code today — only global `RenderSettings` exponential fog covers the plain; this row is new this pass)* |
| Katana "Echo" | (2, 1, 4), Euler(-90,0,0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| Near headstone rows (z=14 start of the field) | see Beat 2's full field table | `Props.HeadstoneGeneric` | `…/Art/Generated/Props/HeadstoneGeneric.prefab` | **MISSING** |
| `GateLight` | (0, 2.2, 10) | — | `ChapterEnvironmentProfile.accentLights["Gate"]` | profile |

**Notes on the transition.** `FogGateWall` is a visual-only marker: it must never regain a collider when swapped for a prefab, because the beat's entire "the crew is held, Ronin-7 isn't" staging depends on the player walking through it unobstructed while the *crew* (never physically present, per §3) is narratively stopped. A prefab here should read as a standing wall of fog denser than the ambient haze, not as a solid gate — no hinge, no slide, no `ProximityDoor`. Canon is more specific than "a wall": "the fog thickens into a standing wall... rising into a soft grey rampart with a single dark gap in it: the gate," and Ronin-7 "steps to the dark gap in the fog-wall" — the gate reads as a tall fog rampart with one dark, passable gap in it, not a uniform flat slab. The `Vfx.FogGateWall` prefab should present that gap visibly (the thing the player is actually seen stepping through), reinforcing the "held at the gate / only one passes" staging rather than a plain haze-wall the player just walks into.

The katana rides from the start of the chapter (`BuildSword` at (2,1,4), no rack-wake beat) — per the builder's comment, this matches "Ch5-7's 'cost, not initiation' precedent": deep into Act II, the blade is a given, not a reveal.

**The wrapped→drawn→sheathed staging in the script is deliberately not built — noted so no future pass tries to wire an unwrap beat.** Canon threads the blade's visible state through the whole chapter: it is "the wrapped katana slung" at the Beat-0 briefing and "the wrapped katana at his hip" through Beat 1 and the trial, Ronin-7 "draws the katana" only when the Warden rises (Beat 3), stands afterward with "the spent blade," and "sheathes the blade" at Beat 5's close. The build gives a single fully functional, unwrapped `BuildSword` live from spawn at (2,1,4) — there is no wrap state, no draw animation/beat tied to the Warden's rise, and no sheathe beat at the exit. This is a legitimate simplification, not an oversight, but it means a future art or animation pass reading the dialogue script should not assume a wrapped-blade prop or a draw/sheathe trigger exist to hook into — they don't; the blade is live and drawn from the first frame of the chapter.

#### d. Combat

None.

#### e. Dialogue / VO

Two dialogue sets, both advanced on Left-Hand **Talk** (Y):

- **`ch8_beat1_gate`** (`Dialogue_Beat1_Gate`, position (0,1,9)) — 4 lines, ≈54 s: Mera Voss ("There's a wall here...") → Coral Vex ("It picks. I told you it would...") → Kessler ("Comm's going to static...we hold the gate. You come back through it.") → Ronin-7 ("I heard you. Hold the gate.").
- **`ch8_beat1_alone`** (`Dialogue_Beat1_Alone`, position (0,1,15)) — 3 lines, ≈41 s: Echo ("Comm's gone. It's just the two of us now, Cipher...") → Ronin-7 ("Then stay close and stay quiet...") → The Mourners ("You were made to walk where you were sent. So walk...").

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** The gate crossing is sold entirely by the dialogue's silence-then-Mourners beat and the `SilentGardenWindAmbience` bed continuing unbroken through it — there is no scripted audio "seal" sting when the gate closes behind the player.
- **Comm-to-static bookend (unspecified — new recommendation).** Canon stages the silence-break as a deliberate, symmetrical audio arc: Kessler's `ch8_beat1_gate` line is written degrading to static mid-delivery ("Comm's already going to static and you're six steps in"), and its Beat-5 mirror — the reunion's opening Kessler line — is stage-directed "(comm, then voice)," "the first sound from outside in the whole chapter, faint, then growing." Nothing in `Chapter8Builder.cs` treats this as an audio event; `Dialogue_Beat1_Gate`'s clip plays at full clarity like every other line. Recommend a concrete cue here: a comm-degradation treatment on `ch8_beat1_gate`'s Kessler line, resolving to static/silence as the player crosses toward the fog gate — the fall half of the arc the Beat-5 swell (§f, Beat 5) answers. See the new `CommFadeToStatic`/`CommSwellReturn` rows in §7's SFX bed.
- `GateLight` is a flat, un-flickering accent (`behaviour: None`, per Appendix A.1) — the gate reads as a still threshold, not an unstable one; instability is reserved for the barrow's lights later.
- Comfort vignette engages normally on the walk from spawn to the gate; nothing beat-specific.
- Haptics: none scripted.

---

### Beat 2 — The Trial of Mind (The Grave-Paths, The Riddle Gauntlet)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (the headstone field, the two answer-pad stones + labels) and **`BuildBeat2Logic()`** (the `RiddleTrial` component + its answer points, the buried-guardian spawns, the `EnemyWaveSpawner`, dialogue players, mission-spine steps).
> **Puzzle scope cut, load-bearing for this beat:** the source design frames **three** seed puzzles (Grave of True Names, The Honest Order, The Fog's Question). Only **Puzzle C, "The Fog's Question,"** is built as the interactive gate. Puzzles A and B exist **only as narrative color in the story/dialogue files** — do not build set-dressing or mechanics for them without a scope decision; see (a) below.

#### a. Narrative purpose & emotional target

The Program trained Ronin-7 to obey without thinking; the Garden will not let him pass until he can think without being told. This is the thematic spine of the entire chapter, and per both the treatment and the dialogue script's own production notes, it was **designed as three riddles** — the Grave of True Names (distrust the clean, official name), The Honest Order (choose the unflattering true sequence of your own past over the flattering lie), and The Fog's Question (the obedient answer is always the wrong one). **`Chapter8Builder.cs` builds exactly one of the three**, matching Chapter 7's precedent of scoping a side-objective mechanic down to what has a fully authored answer. The chosen riddle — *"When the leash is pulled, what is the duty of the thing on the end of it?"* — is explicitly called out in the source script's own production note as **"the thematic spine of the whole trial,"** which is presumably why it is the one the builder mechanizes: Ronin-7's answer — the wired `ch8_beat2_riddle_answer` line, *"A thing on a leash has no duty. That duty was a lie they put in the collar. The only thing it owes is the leash itself, broken… That's my answer. None."* (`Chapter8Lines.cs:192–193`; the screenplay's punchier "None. I owe the leash nothing but the end of it." did not survive into the wired line above, which is the phrasing this document's own quote-the-wired-line rule (Beat 0e) requires citing) — is the chapter's title line delivered as gameplay input, not just VO.

The narrative describes the trial as getting harder as it goes ("the deeper he goes, the less the puzzles test what he knows and the more they test what he refuses to admit") and describes wrong answers waking "the buried" — grievable, not evil, spectral guardians doing their job. The built version compresses this into a single riddle with a binary choice and a single guardian wave, which is a legitimate, flagged simplification (see (d) and the `RiddleTrial` class doc), not an oversight.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Player:** must physically walk from the Beat 1 gate area (z≈15) up the headstone-lined grave-path into `GravePathReachPoint` (0, 1, 25), radius **5**, to satisfy step 3 before the riddle-pose dialogue plays.
- **`RiddleTrial`** (`GraveRiddleTrial` GameObject, built **inactive**, `SetActive(false)` at build time): a `RiddleTrial` component with two `answerPoints` —
  - index 0: `AnswerPad_Obey_AnswerPoint` at (-2.5, 1, 35) — the trained/wrong answer
  - index 1: `AnswerPad_None_AnswerPoint` at (2.5, 1, 35) — the true/correct answer, `correctAnswerIndex = 1`

  `answerRadius = 1.75f`. Every `Update()`, while the trial is active, `RiddleTrial` polls `Camera.main`'s horizontal distance to each answer point; stepping into radius of a pad **submits** it. This is the same distance-poll idiom `ReachTrigger` and `ProximityZoneExit` already use — no new interaction system.

  **The pads *are* the built realization of canon's "fork."** Canon closes Beat 1 on the field firming "into a fork: two paths, two ways the grave-rows run," and poses the riddle at that fork. The build renders this as a left/right binary choice between two pads on a single shared path (-2.5,0,35)/(2.5,0,35), not two diverging causeways the player walks down. Note this explicitly so a future pass doesn't read "fork" literally and try to construct branching path geometry — the pads are the fork.

  **That fork image is a different canon moment than Puzzle C itself, though, and the more precise anchor cuts the other way.** The dialogue script stages the *mechanized* riddle, "The Fog's Question," as explicitly prop-less: "no stones here, only fog… it has no props at all… no object to manipulate; the input is the answer Cipher gives." The fork is Beat 1's transition image into the trial, not Puzzle C's own staging. So the two physical stone pads are a buildable affordance for what canon writes as a spoken/selected answer over open fog, not a literal build of the fork (correctly not wanted, per the paragraph above) or of Puzzle C's own scene (which canon deliberately keeps prop-less). A future fidelity pass wanting to close that gap could render the two choices as two patches of parting fog, or two directions opening in the grey, rather than carved stones — closer to canon's "no props at all" than any physical pad, including the carved-stone treatment recommended below (c).
  - **Wiring pitfall this component was explicitly built to avoid:** the trial GameObject is built inactive and only `SetActive(true)`'d by the mission-spine **Prompt** step (step 5) beginning — `MissionDirector.BeginPrompt` does this, and `AdvanceFromPrompt` `SetActive(false)`'s it again on the correct answer. Without this gating, a player who wandered onto the correct pad *before* the Prompt step began would silently latch `RiddleTrial.Passed` while `AdvanceFromPrompt` no-ops off-step, soft-locking the trial. **Do not build the trial GameObject pre-activated.**
- **The buried (spectral guardians):** 3 `Enemy`+`Health` instances (`Ch8EnsureBuriedDefinition`: 45 HP, 9 dmg, 1.3 move speed, 0.85s attack cooldown) at (-2,0,40), (0,0,42), (2,0,40), all built **inactive**.
- **`BuriedWaveSpawner`** (`EnemyWaveSpawner`, built **active-idle** at (0,0,38), trigger radius 10, one wave = all 3 buried, bark = `dlgWrongBark`): its `Begin()` is wired to `RiddleTrial.onWrongAnswer` via a persistent listener — stepping on the wrong pad calls `Begin()` directly (not proximity-polled; the player is already standing on the wrong pad the instant the event fires). **`Begin()` is idempotent** — a spawner only ever runs its waves once, so a *second* wrong answer after the guardians are already up is a no-op, not a second wave. This is the `RiddleTrial` class doc's own flagged simplification of the source design's "disturbance loop" (guardians respawning on every retry): here, guardians rise exactly once, on the first wrong answer, and stay down once cleared — a failed retry after that does not resurrect them.
- **`RiddleTrial.onRightAnswer`** is wired directly to `MissionDirector.AdvanceFromPrompt` — the correct pad both ends the trial (`RiddleTrial.Passed = true`) and advances the mission in the same event, with no separate Trigger step needed. **This is also the natural trigger for the fog's own reward.** Per the dialogue script, "solve true and the fog opens the way on" — the grey wall of fog "recoil[s], drawing back across the ground like a tide pulled out" the instant the correct pad is submitted (§3, Beat 2c below). `onRightAnswer` already fires exactly once, on the correct submission, so a `Vfx.GroundFog` recoil call belongs on the same persistent-listener event as `AdvanceFromPrompt`, not a new trigger — today it drives only the mission advance, with zero environmental response.

**Mission-spine steps:**

| # | Step | Kind | Fires on |
|---|---|---|---|
| 3 | ReachTrigger: The Grave-Paths | ReachTrigger | player enters `GravePathReachPoint` (0,1,25), radius 5 |
| 4 | Beat2: The Fog's Question (the riddle pose) | Dialogue | plays `ch8_beat2_riddle_pose` — the Mourners pose the riddle |
| 5 | Prompt: Answer the Riddle (RiddleTrial gate) | Prompt | `promptObject = trialGo`; activates `GraveRiddleTrial` on begin; the *player's own pad choice*, not an input action, resolves this step via `RiddleTrial.onRightAnswer → AdvanceFromPrompt` |
| 6 | Beat2: True (the answer, the trial ends) | Dialogue | plays `ch8_beat2_riddle_answer` — Ronin-7's answer, the Mourners' acknowledgment |

*(The wrong-answer bark, `ch8_beat2_wrong_bark`, is not a mission-spine step — it plays as wave 0's `bark` inside `BuriedWaveSpawner`, firing only if the player answers wrong at least once. This is the same "per-encounter bark, not a spine step" convention Ch6/Ch7 use.)*

**What changes during the beat:** the headstone field is static throughout. State changes are: (1) `GraveRiddleTrial` flips inactive→active at step 5's Prompt begin; (2) if the player steps on `AnswerPad_Obey`, the 3 buried guardians and `dlgWrongBark` fire once, and the player must clear the wave in melee combat before the puzzle can be retried (mechanically: nothing blocks a retry — the pads are always walkable — but narratively the trial is not "passed" until the correct pad is submitted); (3) stepping on `AnswerPad_None` (correct) ends the trial and advances the mission regardless of whether the wrong pad was ever tried.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Headstone field (13 pairs / 26 primitives, z=14→62 step 4, x≈±4 jittered ±2.5) | see Appendix A.2 for the full per-row jitter formula | `Props.HeadstoneGeneric` | `…/Art/Generated/Props/HeadstoneGeneric.prefab` | **MISSING** |
| `AnswerPad_Obey_Stone` | (-2.5, 0.5, 35) | `Props.RiddlePadStone` | `…/Art/Generated/Props/RiddlePadStone.prefab` | **MISSING** |
| `AnswerPad_Obey_Label` ("OBEY") | (-2.5, 1.3, 35) | — | `TextMesh`, no prefab | greybox |
| `AnswerPad_None_Stone` | (2.5, 0.5, 35) | `Props.RiddlePadStone` | `…/Art/Generated/Props/RiddlePadStone.prefab` | **MISSING** |
| `AnswerPad_None_Label` ("NONE") | (2.5, 1.3, 35) | — | `TextMesh`, no prefab | greybox |
| `TrialLight` | (0, 2.2, 35) | — | `ChapterEnvironmentProfile.accentLights["Trial"]` | profile |
| The Buried ×3 (spectral guardians) | (-2,0,40) / (0,0,42) / (2,0,40) | `Enemies.Buried` | `…/Art/Generated/Characters3D/Enemies/Buried.prefab` | **MISSING** *(uses a generic `Enemy` build path, no Named/placeholder mesh assigned — see Appendix A.2)* |
| `GroundFog` (trial-zone recoil state) | grave-path field, y≈0–1, z[14,62] | `Vfx.GroundFog` | `…/Art/Generated/VFX/GroundFog.prefab` | **MISSING** *(no row or wiring exists today; recoils/opens on `RiddleTrial.onRightAnswer` — see (b) above. The one built puzzle's canon payoff — "solve true and the fog opens the way on" — currently has no environmental response at all)* |

**Staging constraint the `Props.HeadstoneGeneric` prefab must respect.** Canon makes carved names load-bearing, not decorative: the field is described as "every one bearing a name worn too faint to read until he is right on it," "no two alike" (Beat 1 stage direction), and the Mourners' own rule is "the dead remember every name that was ever taken from them" — with the built Puzzle C literally a true-vs-false-name mechanic. The prefab should carry a worn, near-illegible carved name per instance (and per-instance variation across the field), not a blank tinted slab — a field of 26 identical unmarked stones undercuts the one premise ("the dead remember every name") the chapter's single built puzzle is standing on.

**The `AnswerPad_Obey`/`AnswerPad_None` labels should be carved into the pad stones, not floated as clean UI text.** This chapter's own thesis for the pads — quoted above (a) and in the source's production notes — is to "distrust a name precisely because it looks official and new… the false names are the clean, official, freshly-cut ones." A crisp, floating, uppercase `TextMesh` is exactly the clean-and-issued register the Garden tells the player to reject. Recommend carving the labels into the `Props.RiddlePadStone` faces with the same worn treatment recommended for `Props.HeadstoneGeneric` immediately above — same art pass, consistent tone — rather than floating them as non-diegetic UI. The labels remain load-bearing for the mechanic (the player must still be able to read the choice at a glance), so this is a "carve, don't float" note, not a "remove" note. Note this is still a physical-pad solution: per (b) above, Puzzle C's own canon staging has no props at all, so carved stone is the better of two physicalizations, not a reconciliation with "no props" — only the parting-fog/parted-grey alternative flagged in (b) would actually satisfy that line.

**Staging constraints the prefabs must respect.** Per the dialogue script, "the buried" should read as "grave-grey, half-fog, wearing the worn shape of whoever the lie slandered" — grievable, not monstrous — and should **sink back into their graves rather than dying a second death** on defeat. The current `BuildEnemy` path gives them a standard `Health`/`Enemy` rig with no special death VFX; a prefab pass replacing this should pair the mesh with a "sink into the ground" death treatment rather than the default enemy death handling, to honor the source's "not evil, doing their job" framing. This is new visual behavior, not present in the builder today — flagged, not built.

**Puzzles A and B are unbuilt.** Nothing in this beat's art table represents the Grave of True Names causeway or The Honest Order's scattered fragments — they exist only as the narrative color a player never interacts with. A future pass that mechanizes them would add new art rows here and a second/third `RiddleTrial` (or an extended one), not modify this one.

**Reveal timing: the pads and their labels are visible — and legible — long before the riddle is posed, pre-spoiling the chapter's own answer.** `Ch8BuildAnswerPad` parents both the stone and its `TextMesh` label directly under `world`, not under `trialGo` (the `GraveRiddleTrial` GameObject that (b) above establishes stays inactive until step 5's Prompt) — so unlike the trial *logic*, the pad *art* is active and visible from the moment the chapter loads. The player crosses the Beat-1 gate at z≈10 and walks the headstone field toward `GravePathReachPoint` (0,1,25) r5, with the pads sitting a further 10 m ahead at z=35; at the beat's fog density 0.032 (§3.1/Appendix A.1) the exponential fog factor over that 10–25 m range is still ≈0.45–0.73 — legible, not obscured. That means the word **"NONE"** — the exact climactic answer Ronin-7 is meant to arrive at himself in `ch8_beat2_riddle_answer` (`Chapter8Lines.cs:192-193`), the chapter's title line delivered as gameplay — is readable on a stone before the Mourners even pose the riddle at step 4, pre-spoiling the beat's own dramatic engine (Ronin unlearning the obedience reflex in real time). Recommend gating the pad art with the Prompt the same way the logic is gated — parent the stones/labels under `trialGo` instead of `world` so they activate on step 5 alongside `GraveRiddleTrial` — or, at minimum, keep the labels blank or illegible until the riddle is posed.

#### d. Combat — the buried-guardian wave (conditional)

Only fires if the player steps on the wrong pad. `Enemy` AI (existing system, reused) drives the 3 buried guardians once `Begin()` activates them; player damage output is `BladeDamager`'s EMA swing-speed model, same as every prior chapter. Per the `Ch8EnsureBuriedDefinition` data (45 HP / 9 dmg / 1.3 move speed each), this is a lighter encounter than a Ch1 trooper (compare Ch1's per-trooper stats, not documented in this file but generally tougher) — consistent with the source's "grievable, not evil" framing: a punishment, not the trial's real challenge.

Weakpoint-sight (earned Ch7, still resident on the rig) works on the buried per the source script's own note ("combat is allowed help, the trial is not") — Echo's silence rule (see f. below) is explicitly lifted during this wave.

#### e. Dialogue / VO

- **`ch8_beat2_riddle_pose`** (`Dialogue_Beat2_RiddlePose`, (0,1,30)) — 1 line, ≈22 s: the Mourners pose the full riddle.
- **`ch8_beat2_wrong_bark`** (`Dialogue_Beat2_WrongBark`, (0,1,36)) — 1 line, ≈5 s: "Wrong. The dead remember what you just said. Rise, and answer for him." — fires only as `BuriedWaveSpawner`'s wave-0 bark.
- **`ch8_beat2_riddle_answer`** (`Dialogue_Beat2_RiddleAnswer`, (0,1,36)) — 3 lines, ≈40 s: Ronin-7's two-part answer, then the Mourners' acknowledgment ending on "Now the Garden asks the older question, the one it asks with its hands" — the boss hand-off line.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point in the wave-fight or the puzzle-solving.
- **Echo's silence rule (narrative, not enforced in code):** per the dialogue script's production note, Echo is meant to be mechanically and narratively silent during puzzle-solving itself, returning only during a spectral wave (combat barks) or in between-puzzle "pressure-valve" lines. `Chapter8Lines.cs` does **not** author a between-puzzle Echo bark pool — only the wave-context lines exist implicitly via `EnemyWaveSpawner`'s single `dlgWrongBark`. **This is narrative guidance carried by VO absence, not a scripted silence system** — there is no `EchoPresence` mute/unmute call gated to this beat.
- `TrialLight` carries `behaviour: ConsoleFlicker(seed: 88)` (Appendix A.1) — a deliberately unstable light at the riddle pads, mechanically identical to Chapter 1's Command Room flicker but re-purposed here as "the fog testing you" unease rather than "failing ship power."
- Haptics: standard `BladeDamager`/combat feedback during the buried wave only; **none during puzzle-solving today — flagged as a gap.** Stepping onto `AnswerPad_None` is the chapter's thesis delivered as player input, the physical act immediately preceding Ronin-7's wired answer, ending *"…The only thing it owes is the leash itself, broken… None."* (`Chapter8Lines.cs:192–193`). Recommend a soft, non-shake confirming haptic pulse on `RiddleTrial.onRightAnswer` and a distinct, heavier pulse on `onWrongAnswer` (as the buried rise) — consistent with "feel comes from Haptics" (§1.1) and the exact parallel this document already recommends for the dive-teleport/Mourners-manifest moments in Beat 4f. Right now the single most important input in the chapter lands with no felt response at all.
- **Ambience dead zone over this beat's own combat trigger — see §7.** The riddle pads/buried spawns (z=35–42) sit in the ≈12 m gap between `SilentGardenWindAmbience`'s falloff (z≈36) and `BarrowDreadAmbience`'s reach (z≈48) — the pressure bed thins out exactly where the trial's stakes peak. See §7 for the fix options (widen the beds' overlap, or add a third bed centered near (0,2.2,40)).
- Comfort vignette behaves normally through the walk and the wave-fight's snap-turns.

---

### Beat 3 — The Warden (The Deep Garden) — BOSS

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (the barrow mound's approach dressing, the Warden's placeholder mesh instantiation as art) and **`BuildBeat3Logic()`** (the Warden's `Enemy`/`Health` wiring, the `DefeatEnemies` step, dialogue). **The Warden is built inactive; nothing here should pre-activate it** — the class summary is explicit that it stays `SetActive(false)` until step 9's `DefeatEnemies` setup.

#### a. Narrative purpose & emotional target

A full beat of cerebral puzzle-solving gives way, deliberately, to the chapter's sole large-scale physical encounter — the source script calls this out explicitly as intended gameplay contrast. The Warden has no spoken lines and no spare condition; it is "the Garden's mechanism, not a person," and putting it down is the intended and only outcome. This is the **showcase fight for the Chapter 7 weakpoint-sight ability**, run diegetically through Echo, who is freed from the trial's silence the instant combat starts and returns "like a held breath let go." The mind was proven in Beat 2; now the Garden takes the body — the two halves of the trial (§a of Beat 2) are explicitly paired by the Mourners' own framing: *"Strength was given to you the way thought was."*

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **Player:** must walk from the riddle-pad area (z≈35) up to `DeepGardenReachPoint` (0, 1, 60), radius **6**, satisfying step 7 before the boss-intro dialogue plays.
- **The Warden:** `Ch8BuildWarden` instantiates the `The-Warden.prefab` placeholder (`PlaceholderArchetype.Massive`, grave-iron green tints, resolved via `PlaceholderCharacterBuilder`) at (0, 0, 66), rotated **Euler(0, 180, 0)** to face the player's approach from -Z. `FitNamedCharacter` grounds it; a `CapsuleCollider` (center (0,1.2,0), height 2.6, radius 0.55) is added on top of the Named-mesh convention (mirrors Ch6's `BuildMasterEnemy`/Ch7's `BuildMindspaceBoss` pattern — a boss built from a Named-character mesh rather than a dedicated combat rig). Wired: `Health`, `Enemy` (`definition` = `Ch8EnsureWardenDefinition`: 340 HP / 26 dmg / 0.9 move speed / 1.1s attack cooldown), `weapon`/`bladeTip`/`bodyRenderer` on a synthetic `ArmR/Sword/Blade/BladeTip` chain (no skinned weapon mesh — matches the trooper-rig convention from Ch1). **Built `SetActive(false)`** — the boss is invisible and inert until step 9 begins.
- **Reveal mechanism:** `AuthorDefeatStep(steps, 9, "Beat3: The Warden (boss)", new List<Object> { wardenEnemy.GetComponent<Health>() })` — this is the same `DefeatEnemies` idiom Ch1 Beat 3 uses for the boarding party: the Warden's own `SetActive(true)` on step-9 begin is what `DefeatEnemies` steps do (mirroring the boarding troopers' group-activate), not a separate visible Trigger.
- **The Warden's entrance is unstaged, and the boss-intro dialogue plays before the Warden exists — the mirror of the death-sink gap already flagged for this beat (§4, Beat 3d), but on the encounter's birth rather than its death.** Canon repeats the rise as an image, not a pop: the barrow's "grave-iron door [is] already grinding open… Something is coming up out of it," and "Out of the open grave-iron door rises THE WARDEN." As built, step 8's intro dialogue (`ch8_beat3_warden_intro`) — Echo's "There you are, fight… I've got the read back the second it moved" and the Mourners' "This is the warden of the last grave" — plays while `wardenEnemy` is still `SetActive(false)`; the figure only appears when step 9's `DefeatEnemies` activates it. The player hears the boss announced over empty fog, then it hard-pops in the instant combat starts. **Recommend inserting a `Trigger` step ("The Warden Rises") between the reach (7) and the intro (8)** that `SetActive(true)`'s `wardenEnemy` as a rise-from-barrow moment — the same additive `Trigger`-step idiom this document already endorses for the Mourners' manifestation (step 11, §4b) — so the intro VO has its subject visibly on screen instead of narrating an empty arena. Fog density 0.032 (§3) already hides the barrow until the player is within ≈6 m of it (z≈60), so a staged rise reads as a genuine looming reveal — which is exactly why a bare `SetActive` pop squanders it. **Mechanical cost of this insertion, note for whoever implements it:** adding this step bumps `steps.arraySize` from 22 to 23 and renumbers every step from 8 onward — every mission-step table in this document from Beat 3 through Beat 5 (§4), and the Beat 3/4/5 step numbers cited in prose throughout, would need reindexing to match. `Chapter8LinesTests`' coverage is unaffected (it asserts on dialogue set IDs, not step indices), so this desync would not be caught by the test suite — a builder implementing the recommendation must update the tables by hand.

**Mission-spine steps:**

| # | Step | Kind | Fires on |
|---|---|---|---|
| 7 | ReachTrigger: The Deep Garden | ReachTrigger | player enters `DeepGardenReachPoint` (0,1,60), radius 6 |
| 8 | Beat3: The Warden (boss intro) | Dialogue | plays `ch8_beat3_warden_intro` — the Mourners frame the fight; Echo runs the weakpoint-sight read |
| 9 | Beat3: The Warden (boss) | DefeatEnemies | activates the Warden; waits for its `Health` to reach zero |
| 10 | Beat3: Down It Goes (boss defeated) | Dialogue | plays `ch8_beat3_warden_defeat` — the Mourners' acknowledgment, "we're coming to look at you" |

**What changes during the beat:** the Warden flips inactive→active at step 9. Nothing else in the set dressing changes; the barrow mound and its two accent lights are present and lit from the start of the chapter (they are outdoor-world objects built once, not beat-gated).

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Barrow mound (approach only — the full barrow is Beat 4's centerpiece, see that section) | (0,-1,68) | `Props.BarrowMound` | `…/Art/Generated/Props/BarrowMound.prefab` | **MISSING** |
| The Warden | (0,0,66), Euler(0,180,0) | `Enemies.TheWarden` (currently `Named.TheWarden` placeholder) | `…/Art/Generated/Characters3D/Named/The-Warden.prefab` | **EXISTS** *(procedural placeholder mesh, not final art)* |
| `BarrowLight0` | (-4, 2.6, 66) | — | `ChapterEnvironmentProfile.accentLights["Barrow0"]` | profile |
| `BarrowLight1` | (4, 2.6, 70) | — | `ChapterEnvironmentProfile.accentLights["Barrow1"]` | profile |

**Notes on the transition.** `The-Warden.prefab` resolves today via `PlaceholderCharacterBuilder`'s spec table (`PlaceholderArchetype.Massive`, grave-iron green (0.22,0.32,0.25)/(0.12,0.18,0.14)) — this is a **procedurally generated placeholder mesh**, not commissioned art, but it is a real, loadable prefab (Status **EXISTS**), distinguishing it from every `MISSING` row elsewhere in this document that has no asset at all. Replacing it with bespoke art is a straight prefab swap at the same registry key, no logic change.

**The commission brief for the real mesh needs two spec points a flat tint can't carry — the marquee asset is under-briefed by "grave-iron green" alone.** (1) Canon describes the Warden as a *composite*, not a sculpted monster: "a towering guardian assembled from grave-iron, headstone, root, and packed fog" (Beat 3 stage direction) — the mesh should read as accreted from the graveyard's own materials (iron, stone, root, haze all visibly distinct), not as a single continuous surface the way the placeholder's flat two-tone paint job reads. (2) It wears "the half-shape of the oldest thing buried here, a thing that may once have been an operative or a Mourner or neither, now only the Garden's fist" — a partial, unfinished humanoid, not a complete figure. Point (2) also sets up the weakpoint fight's own framing (§4, Beat 3d): the production note's structural seams are explicitly "joints, the cracked headstone at its core, fog-gaps where the body hasn't fully knit," which only reads as a targetable weakness if the base mesh is visibly incomplete/composite to begin with, not a smooth continuous body with damage decals painted on after the fact. A generic "big green golem" delivered at the `Enemies.TheWarden` registry key would satisfy the Status table but miss both of these — flag them for the art commission alongside the palette.

**`Props.BarrowMound` needs a grave-iron door face, not a bare mound.** Canon names the door three times across this beat and the next: "grave-iron door already grinding open" at the approach, "Out of the open grave-iron door rises THE WARDEN," and — already flagged above (b) — the Warden "sinks back toward its barrow" on defeat, an aperture it sinks back into. The primitive fallback is a plain tinted sphere (Appendix A.4) with no door element at all, so today's build has no opening for either the rise or the already-flagged sink-death. The door should read closed at Beat 1–2 range and be grinding open by the time the player is deep enough into Beat 3 to see it (i.e., roughly the `DeepGardenReachPoint` z=60 crossing) — it is load-bearing for both the entrance (above) and the exit (d, below), not incidental mound detail.

This is also the arena the `Vfx.GroundFog` layer (§3) must reach: by this beat the fog has recoiled off the bare ground and settled into a ring around the barrow, the only "wall" the Warden's arena has — see §3 for the full spec. No such geometry or fog-motion exists in the builder today; flagged there, not duplicated here.

#### d. Combat — the Warden fight (the weakpoint-sight showcase)

Per the source script's production note (reproduced for the builder's benefit): the Warden's grave-iron body armors it against frontal damage; **Echo lights structural seams** (joints, the cracked core-stone, fog-gaps) that the player targets with weakpoint-sight (Ch7) to stagger and break it. Suggested phase structure — (1) seam-breaking on the limbs, (2) the exposed core-stone re-armored with fog between windows, (3) a final committed strike when the core is fully bared — is **narrative/design guidance, not mechanized in the builder**: `Ch8BuildWarden` wires a single flat `Enemy`/`Health` pair with no phase logic, no seam sub-colliders, and no core-stone object. **The phased, seam-based fight described in the dialogue script is not yet built** — today's Warden is a standard `Enemy` AI encounter at 340 HP, distinguished from a trash mob only by its stat block and its Named-mesh placeholder. Flagging this gap for whoever picks up combat-encounter work on this chapter: the weakpoint-sight "showcase" framing is currently aspirational.

Player damage output remains `BladeDamager`'s EMA swing-speed model throughout, same as every chapter. No camera shake at any point.

**Defeat treatment — the same "put back, not destroyed" gap Beat 2c already flags for the buried, but on the marquee kill.** Canon makes an identical, explicit demand for the Warden that Beat 2c's art note makes for the buried: on the break, the Warden "does not explode; it comes apart into grave-iron and falls still, and sinks back toward its barrow, put back, not destroyed, consistent with the Garden's whole grammar of the dead being settled rather than slain." `Ch8BuildWarden` wires a plain `Enemy`/`Health` pair with no death-VFX hook of any kind, so today's Warden dies via whatever the default `Enemy` death handling does (ragdoll/despawn) — the same mob-style death Beat 2c already flags as wrong for the buried, but here landing on the chapter's single most-watched kill. A prefab/death-VFX pass replacing default enemy death on the Warden specifically must instead play a "comes apart into grave-iron, falls still, sinks back into the open barrow" treatment, paired with the cracked-bell `WardenToll` one-shot already specified in (f) below — the two effects (visual settling + the toll going silent) are the same beat, described together in the source stage direction, and should land as one death moment, not two independently-timed cues.

**Missing mid-fight Echo bark — the "showcase" fight is VO-silent past the intro (parallel gap to Beat 2f's flag).** The dialogue script's Beat 3 authors a distinct mid-fight Echo line that never made it into a built dialogue set: *"Core's open, Cipher… the cracked stone behind the iron, right there in the chest of it, that's the grave it's built around. The fog keeps trying to close back over it. Don't trade blows with the body, wait for the gap, and when it's bare you put everything through it. One clean strike. Make it the last thing."* — the call for the fight's actual weakpoint window, distinct from the seam-read already wired into `ch8_beat3_warden_intro` ("work the seams"). `Chapter8Lines.cs` authors only `ch8_beat3_warden_intro` (2 lines) and `ch8_beat3_warden_defeat` (1 line) for this beat — no combat-bark pool exists for the Warden's seam/core reads. Concretely: once the intro dialogue finishes, Echo says nothing else for the entire fight, so the weakpoint-sight "showcase" the fight is designed around has no diegetic voice calling the seams or the core window while the player is actually fighting. Authoring this line (and ideally a short seam-bark pool) as a new dialogue/bark set is the single highest-value VO gap in this beat.

#### e. Dialogue / VO

- **`ch8_beat3_warden_intro`** (`Dialogue_Beat3_WardenIntro`, (0,1,60)) — 2 lines, ≈36 s: the Mourners frame the physical test; Echo's weakpoint-sight read comes back online.
- **`ch8_beat3_warden_defeat`** (`Dialogue_Beat3_WardenDefeat`, (0,1,66)) — 1 line, ≈16 s: the Mourners' acknowledgment, ending on the ring's manifestation cue — quoting `Chapter8Lines.GetBeat3WardenDefeatLines()` exactly (no "up," matching the step-10 table above): "we're coming to look at you with what's left of our eyes."
- **No mid-fight bark set.** Echo's core-window call ("Core's open, Cipher…") is present in the screenplay but has no `ch8_beat3_*` set ID and no combat-bark pool wired to the Warden's `Enemy`/`Health` — see (d) above.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the Warden's hits, staggers, and the final core-break are carried entirely by Haptics, AudioDirector stingers, and the CombatFeedbackController reticle.
- **`WardenToll` — the chapter's marquee sound, not yet built.** The dialogue script's Beat 3 stage directions make the Warden's toll the one sound licensed to break the Garden's silence: it "tolls, a deep grave-bell sound that is the closest the silent world comes to a roar" on activation and its attacks, and on the kill "the Warden's grave-bell toll cracks and goes silent." Nothing in `Chapter8Builder.cs` today wires an `AudioDirector` cue to the Warden's activation, attacks, or death. This must be a `WardenToll` cue: a low grave-bell stinger on step-9 activation (and optionally repeating on attack windups) and a distinct cracked-bell one-shot on `Health` reaching zero — see the new SFX-bed row in §7. In a level whose thesis is "the silence is the chapter's pulse" (§3), this is the single most load-bearing sound cue in the chapter and it is currently unspecified in code. **Pair the cracked-bell one-shot with the "sinks into the barrow" death-VFX treatment flagged in (d) above** — canon describes the toll cracking silent and the body coming apart into grave-iron as the same moment, not two separately-timed events. **Pair the toll with a felt half, not just a heard one:** since the chapter forbids camera shake and routes impact through Haptics (§1.1), recommend a sub-bass controller haptic pulse synchronized to the toll's step-9 activation strike and its cracked-bell death one-shot — consistent with this document's own haptic-gap recommendations elsewhere in Beats 2f and 4f, and the felt half of the one sound licensed to break the silence.
- **Two more sounds the Garden makes besides the toll are currently unspecified: the barrow door's grind and the trial's fog recoil.** Canon gives the barrow door its own sound distinct from the toll — "its grave-iron door already grinding open" (§c above, and the new entrance-`Trigger` recommendation in (b)) — and treats the trial-ending fog withdrawal as an event in its own right ("when it moves, the Garden has decided something," §Beat 2). Neither is the Warden's bell and neither should be folded into it. New `BarrowDoorGrind` (stone-on-stone, on the Warden's rise and its mirrored sink-close) and `FogRecoil` (a low withdrawing one-shot on `RiddleTrial.onRightAnswer`, §Beat 2b/2c) rows are added to the §7 SFX bed so a future pass doesn't drop them or collapse them into `WardenToll`.
- `BarrowLight0` carries `behaviour: AmbientPulse(period: 6.6s)` (Appendix A.1) — a slow "breathing" light near the barrow, distinct from `TrialLight`'s flicker; `BarrowLight1` is flat (`None`), so the pulse reads as coming from one specific direction rather than the whole arena.
- 90 FPS is the design target for this encounter; this is the chapter's single largest concurrent-AI + VFX load (one boss-tier `Enemy`), the analogous "most likely to break the frame budget" beat per Ch1's own framing of its Beat 3.
- Comfort vignette engages normally on snap-turns during the fight, which will be frequent in an open-arena boss encounter with no cover.

---

### Beat 4 — The One Who Defies (The Still Center) — REVEAL

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (the Mourners' ring figures, the barrow's final reveal state, the two vision-dive rooms and their dressing) and **`BuildBeat4Logic()`** (the ring's activation Trigger, the `MemoryDiveController`/entry-exit triggers, dialogue). **Vision A must stage no face and no full body for the woman** — the vision never finds her face; this is a content rule, not a missing-asset gap. Do not add either. Canon's actual subject is narrower and *is* buildable: "only her hands and forearms ever in the light, gloved," seating the killswitch into the child's skull — see §4c Notes for the unbuilt hands/killswitch staging. **This is distinct from the child on the table, who *is* part of the vision** ("A CHILD lies on a table, very small, face turned away, the base of the skull bared" — dialogue script VISION A block) and is currently unbuilt; see §4c Notes below.

#### a. Narrative purpose & emotional target

**Quoting note:** unless cited as `Chapter8Lines.cs`, the quotes in this section cite the screenplay/dialogue script for their narrative intent, not necessarily the wired VO — cross-check `Chapter8Lines.cs` before treating any of them as what the player literally hears, per the quoting rule flagged in Beat 0e's audit note above.

The chapter's reveal and its reason for existing. The Mourners manifest as bodies for the first time (they were pure voice through Beats 1–3), ring the barrow, and pronounce the chapter's title line as a verdict, not a compliment — the wired `ch8_beat4_naming` line (`Chapter8Lines.cs:226`, an audit-fix thinning of the screenplay's "Not the strongest seeker... not the cleverest..." stack, already noted in Beat 0e's audit summary above): *"We've buried stronger seekers than you, and cleverer ones too... You are the one who defies."* Ronin-7 refuses to be flattered off his actual question — *"I came for one thing. The hand that put the switch in me. And the hand that pulled it. Show me those"* — and the Garden grants exactly that, on its own terms: *"we give sight, not answers."*

The two visions are **seen but not named** by design (per the Continuity Notes in `Ch08_The_Silent_Garden.md`): Vision A shows the faceless woman who seated the killswitch — foreshadowing Ch13's named reveal of Dr. Heris without spending that reveal here — and Vision B shows Khall's grief and doubt in the moment of the switch, advancing **Ladder D to rung 2** without disclosing the Ch16 forged-order twist. Ronin-7's demand ("Turn. Let me see your face.") goes unanswered — the fog does not obey him, which is the point: the Garden gives what was earned, not what was wanted. The Mourners' closing line of this beat reframes the withheld face as a debt Ronin-7 will collect himself, not a gift denied — the chapter's thesis in miniature.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **`MournersRing`** (`Ch8BuildMournersRing`): 4 `The-Mourners.prefab` instances arranged in a ring, center (0,0,68), radius 5, evenly spaced at 90° increments, each `LookAt`-oriented toward the barrow center. Built **inactive** (`ringGo.SetActive(false)`), activated by step 11's `Trigger`.
- **The vision dive** (`MemoryDiveController`, chapter-agnostic, first authored for Ch3's Kethel-7 playback — frozen, reuse-only per §1.2). **Reconciling §3's "literal window into both visions" framing with this mechanism:** §3 quotes canon's "fog thickens into a standing pane... a vision forms in it," describing the fog as the window the visions appear *in*. The built beat does not construct a fog-pane at the barrow that the player looks into — it teleports the rig via `MemoryDiveController` into the walkable Vision A/B rooms, the same idiom Ch3 uses. This is a deliberate adaptation, not a contradiction: the "fog window" is the narrative frame, and the walk-in dive is how it is realized in playable VR space. A future pass should not read §3 literally and attempt to *also* build a fog-pane VFX at the barrow, nor treat the dive mechanism as failing to honor canon — the dive *is* the built form of the window.
  - `diveRoot` = `VisionDive` GameObject at world (0,0,250), holding both vision rooms, built `SetActive(false)`.
  - `diveEntryPoint` = `VisionEntryPoint`, local (0,1,2) inside Vision A's room bounds — **must** stay inside Vision A's z[0,10] span, because `VisionA_WallS` at local z=0 is a solid collider; an entry point outside it spawns the rig embedded in the wall (a documented pitfall in the builder's own comment).
  - `diveExitPoint` = `VisionExitPoint`, world (6, 1, 68), Euler(0,180,0) — offset **x=6** specifically to clear the barrow mound's collider footprint (the barrow sphere is centered at x=0 with ≈4 m radius); an exit point at x=0 would rematerialize the rig embedded in the mound.
  - **The x=6 offset has a staging consequence the collider fix alone doesn't address.** Euler(0,180,0) rematerializes the rig facing −Z — toward the gate, not toward the ring. At world (6,1,68) the player stands roughly 1 m past the east Mourner (which stands at (5,0,68) on the ring's r5 circumference) and outside the ring itself, with the barrow and most of the Mourners behind and to his right rather than in front of him. Canon's Beat-4 close stages the opposite: Ronin standing "in the bare circle, the ring of Mourners unmoved around him," and ends the beat "Looking, at the last, at the ring of Mourners" — during the very `ch8_beat4_aftermath` dialogue (step 17) that plays immediately after this teleport. As built, the aftermath plays with Ronin outside the ring and facing away from it. Either re-aim `diveExitPoint`'s rotation toward the ring center (roughly −X-ish, e.g. Euler(0,130,0)) so the aftermath lands with Ronin looking at the Mourners as canon describes, or accept that the exit deliberately trades that framing for pre-orienting him toward the Beat-5 departure aisle — today it silently does the latter, with no note either way.
  - `rigRoot` = the player rig's own root transform; `flashback` = `MemoryFlashbackController` on the dive root (reapplies fog/ambient treatment on every `EnterDive`, restores pre-dive `RenderSettings` on `ExitDive` — the "color floods back" beat, same mechanism Ch3 uses).
  - `EnterVisionTrigger`/`ExitVisionTrigger` (`MemoryDiveEntryTrigger`/`MemoryDiveExitTrigger`), both built **inactive**, each wired to `visionDiveController` and activated by mission steps 13 and 16 respectively — activating either trigger GameObject is what fires the dive's enter/exit (not player proximity).
- **The Vision A → Vision B walk (undescribed, and ungated).** Neither room has a wall separating it from the other: Vision A spans local z[0,10] with walls W/E/S only (`VisionA_WallS` at z=0 is the only cross-wall it has — no north wall), and Vision B spans local z[15,29] with walls W/E/N only (`VisionB_WallN` at z=29 — no south wall). The player is intended to walk north through the z=10→15 gap between them, out of the sterile room and into the handler's bay, to physically arrive at Vision B before Khall's lines play. **But steps 14 (`ch8_beat4_vision_a`) and 15 (`ch8_beat4_vision_b`) are both button-advanced `Dialogue` steps with no `ReachTrigger` between them** (`AuthorDialogueStep` → `AuthorDialogueStep` directly, `Chapter8Builder.cs`) — a player can advance Khall's entire Ladder-D vision without ever making that walk, still standing in Vision A facing the wrong way, having never seen `Ghost_Khall` or `VisionB_Console`. Recommend a `VisionB` reach point (≈local (0,1,15), just past the gap) gating step 15's dialogue on the player actually entering the handler's bay — the same fix pattern this document already recommends for the unenforced fog-gate in Beat 1/§9, applied to the chapter's most important vision.
- **The same z[10,15] gap has no floor or side walls at all — a genuine buildability hole, not just an unenforced beat.** Vision A's floor spans local z[0,10] (`BuildFloorCeiling` center z=5, size z=10) and Vision B's spans local z[15,29] (center z=22, size z=14); the intervening z[10,15] band has no floor and no W/E walls — `VisionA_WallW`/`_WallE` end at z=10, `VisionB_WallW`/`_WallE` begin at z=15. The walk the paragraph above requires crosses exactly this band: a 5 m hole flanked by open void on both ±x sides. Because `ZoneBounds` clamps XZ but not Y (§2/§8), a rig that ever applies gravity here has nothing to catch it; even without a fall, the reveal's most important traversal shows a gap in the memory-space floor with open sides the player can wander off of. **Close this alongside the `VisionB` reach point above:** either add a connecting floor strip (and W/E walls) across local z[10,15], or explicitly stage the gap as the intentional "the white room folds / the pane reforms" threshold between the two visions — but pick one; today it is neither, just an unbuilt hole. See §8 for the added verification step.
- **Dialogue anchors:** `dlgNaming` (0,1,68) — inside the ring, at the barrow; `dlgVisionA` at `visionDive.position + (0,1,5)` = world (0,1,255) — inside Vision A's room; `dlgVisionB` at `visionDive.position + (0,1,22)` = world (0,1,272) — inside Vision B's bay; `dlgAftermath` (0,1,69) — back at the barrow, post-dive.

**Mission-spine steps:**

| # | Step | Kind | Fires on |
|---|---|---|---|
| 11 | Trigger: The Mourners Manifest | Trigger | activates `MournersRing` |
| 12 | Beat4: The One Who Defies (the naming) | Dialogue | plays `ch8_beat4_naming` |
| 13 | Trigger: Enter the Vision | Trigger | activates `enterVisionGo` → `MemoryDiveController.EnterDive()`: activates `VisionDive`, teleports the rig to `VisionEntryPoint` |
| 14 | Beat4: Vision A (the faceless hand) | Dialogue | plays `ch8_beat4_vision_a`, inside the dive |
| 15 | Beat4: Vision B (Khall, the moment of the switch) | Dialogue | plays `ch8_beat4_vision_b`, inside the dive |
| 16 | Trigger: Exit the Vision | Trigger | activates `exitVisionGo` → `MemoryDiveController.ExitDive()`: deactivates `VisionDive`, teleports the rig to `VisionExitPoint`, restores pre-dive `RenderSettings` |
| 17 | Beat4: The Aftermath (I couldn't see her) | Dialogue | plays `ch8_beat4_aftermath`, back at the barrow |

**What changes during the beat:** (1) `MournersRing` inactive→active at step 11; (2) the entire `VisionDive` island inactive→active at step 13, then active→inactive at step 16 — the player is physically elsewhere in the scene hierarchy for the span of steps 13–16, comfort-safe (instant, non-lerp teleport both ways, per `MemoryDiveController`'s own doc comment); (3) `RenderSettings` (fog/ambient) are snapshotted before `EnterDive` and restored on `ExitDive`, so the outdoor Garden's mood reasserts itself the instant the player returns.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

**Barrow / still-center dressing:**

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `Barrow` (full mound, reveal state) | (0,-1,68) | `Props.BarrowMound` | `…/Art/Generated/Props/BarrowMound.prefab` | **MISSING** |
| `MournersRing` ×4 | ring center (0,0,68) r5, 90° spacing | `Named.TheMourners` (currently placeholder) | `…/Art/Generated/Characters3D/Named/The-Mourners.prefab` | **EXISTS** *(procedural placeholder, bone-white tints)* |

**"Reveal state" for the barrow means its grave-iron door (§3c above) stands open through Beat 4** — it ground open for the Warden's rise and stayed open through the fight and the sink-death, and remains the open aperture the Mourners ring in front of during the naming. The same `Props.BarrowMound` prefab/key covers both beats; only the door's open/closed state differs.

**The Mourners mesh needs a color correction and an explicit faceless content rule, both load-bearing for the commission.** (1) **Color:** canon describes them, at the exact moment they manifest, as "robed grey figures with no faces to find" and, again at the Beat-3 closing stage direction, "robed... grey, faceless, rising not from the graves but from the fog itself" — they should read as the fog resolving into shape, not as pale figures walking in from elsewhere. `The-Mourners.prefab`'s current placeholder tint is bone-white ((0.80,0.77,0.70)/(0.68,0.65,0.58), Appendix A.4) — a warm, pale palette that fights that read. The commission target should shift the tint toward the fog's own grey (≈(0.52,0.54,0.57), the fog color in Appendix A.1) so the manifestation reads as the Garden's fog given shape, not as separate pale-robed figures arriving on top of it. (2) **Faceless is a content rule, not an incidental placeholder gap:** the same canon lines above are emphatic and repeat it twice — "no faces to find," and manifesting "gave them shape, not mouths" (the latter already quoted in this document at Beat 4f, but there only in service of a lighting/fade-in recommendation, never stated as a mesh rule). §4's header instruction already states the "no face" content rule explicitly for the Vision-A woman; it does not yet state it for the Mourners, who are on-screen far longer (all of Beats 4–5, the chapter's entire back half) and are full bodies, not glimpsed hands. A commissioned Mourners mesh must be faceless by design — a smooth, featureless head under the hood, not a hooded-but-visible face — the same rule already governing the Vision-A woman, stated explicitly here for the figures who actually carry the chapter's second half.

**Vision A — the sterile room** (local z[0,10], world z[250,260]):

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (floor/ceiling, 8×10) | center local (0,0,5) → world (0,0,255) | `Rooms.VisionA_SterileRoom` | `…/Art/Generated/Rooms/VisionA_SterileRoom.prefab` | **MISSING** |
| `VisionA_WallW`/`_WallE`/`_WallS` | x=∓4, z=0, all `RoomH`=3.6 tall | `Rooms.VisionA_SterileRoom` (bundled with the shell) | — | **MISSING** |
| `VisionA_Table` | local (0,0.4,7) → world (0,0.4,257) | `Props.SterileTable` | `…/Art/Generated/Props/SterileTable.prefab` | **MISSING** |
| `VisionA_ChildFigure` *(unbuilt — see Notes)* | on `VisionA_Table`, local (0,0.4,7) → world (0,0.4,257), still, face turned away | `Vfx.GhostFigure`-analog (no dedicated key yet) | procedural, mirroring `Ch8BuildGhostFigure`/`Ghost_FootageRonin` | **UNBUILT** — no such object exists in `Ch8BuildVisionRooms` today |
| `VisionA_Light` | local (0,2.4,7) → world (0,2.4,257) | — | point light, no prefab | greybox |

**The `Rooms.VisionA_SterileRoom` commission brief needs "clean steel," not just white.** Canon's VISION A block calls this "a sterile room, all white light and clean steel" — a surgical suite — but the current table only carries `whiteFloor`/`whiteCeil` tint values (Appendix A.4), and a bright white room is not yet a *clinical* one. The commission should render the shell as hard, cold, clean surgical steel — the deliberate anti-graveyard register, the operating table where the killswitch was seated — reinforcing the horror of "a woman's gloved hands seating the switch into a child's skull" that this room exists to stage (see the gloved-hands/killswitch staging in Notes, below).

**Vision B — Khall's handler's bay** (local z[15,29], world z[265,279]):

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (floor/ceiling, 8×14) | center local (0,0,22) → world (0,0,272) | `Rooms.VisionB_HandlerBay` | `…/Art/Generated/Rooms/VisionB_HandlerBay.prefab` | **MISSING** |
| `VisionB_WallW`/`_WallE`/`_WallN` | x=∓4, z=29, all `RoomH`=3.6 tall | `Rooms.VisionB_HandlerBay` (bundled with the shell) | — | **MISSING** |
| `VisionB_Console` | local (0,0.55,26.5) → world (0,0.55,278.5) | `Props.HandlerConsole` | `…/Art/Generated/Props/HandlerConsole.prefab` | **MISSING** |
| `Ghost_FootageRonin` | local (0,0,20) → world (0,0,270), ghost material | `Vfx.GhostFigure` | procedural (`Ch8BuildGhostFigure`) | greybox *(intentional — see notes)* |
| `Ghost_Khall` | local (0,0,25) → world (0,0,275), Euler(0,180,0), ghost material | `Named.Khall` | `…/Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `VisionB_KillOrder` *(unbuilt — see Notes)* | in `Ghost_Khall`'s hands, local ≈(0,1.1,25) → world ≈(0,1.1,275) | `Props.KillOrder` | `…/Art/Generated/Props/KillOrder.prefab` | **UNBUILT** — no such prop or registry key exists in `Ch8BuildVisionRooms` today |
| `VisionB_Light` | local (0,2.4,22) → world (0,2.4,272) | — | point light, no prefab | greybox |

**The `Rooms.VisionB_HandlerBay` commission should echo Ch3's handler-bay geometry, not just its ghost tint.** Canon frames this vision as "the Ch3 confrontation now seen from the other side... a ship's interior he half-knows," and the build already reuses Ch3's ghost-cast *material* (`MakeGhostMaterial()`, applied to `Ghost_Khall`/`Ghost_FootageRonin`) for the "recording, not reality" read. But the room brief only specifies a generic grey shell (`greyFloor`/`greyCeil`, Appendix A.4) with no instruction to match Ch3's actual bay layout. A returning player recognizing the space — "I've stood here before, from the other side" — is a free continuity payoff a generic grey room throws away. The commission for `Rooms.VisionB_HandlerBay` should reference Ch3's handler-bay geometry (console placement, proportions, silhouette), not just Ch3's ghost tint, so the room reads as the same bay seen from the other side.

**Notes on the transition.** The content rule for Vision A is narrower than "stage nothing of the woman": canon is **"never her face / never her full body,"** not "never any part of her." The camera "never once find[s] her face," but "only her hands and forearms ever [are] in the light, gloved, precise, terribly careful," holding "a small dark object, the killswitch," and seating it "into the base of the child's skull." The gloved hands at work and the killswitch prop *are* the vision's actual subject — the act the vision exists to show — and both are currently unbuilt; a future pass should stage **the act** (a pair of gloved hands over the table, animated or posed at the incision, plus a small dark killswitch prop) while continuing to omit any face or full body for her. Do not conflate "no face" with "stage nothing" — an empty table under a light is not a faceless-hands vision, it's just an empty room.

This is a separate question from the **child on the table, who is currently missing from the build entirely** — canon stages "A CHILD lies on a table, very small, face turned away, the base of the skull bared," and without that figure the vision's emotional center (a woman's hands seating the killswitch into a child's skull) has no subject to seat it into. Today `Ch8BuildVisionRooms` stages only the room and `VisionA_Table`, empty. Add a `VisionA_ChildFigure` row (a still, face-turned-away silhouette lying on the table, built the same cheap procedural way as `Ghost_FootageRonin` below — colliders stripped, never speaks, never moves) or, at minimum, flag it in-scene the same way `Ghost_FootageRonin` is flagged as an intentional placeholder — do not let the bare room read as a complete staging of Vision A. `Ghost_FootageRonin` is a deliberately cheap procedural silhouette (a capsule + sphere in `MemoryFlashbackController.MakeGhostMaterial()`, colliders stripped, mirroring Ch3's `Ch3BuildGhostFigure`) — since footage-Ronin never speaks and is only ever seen from Khall's vantage, a full mesh swap here is optional polish, not required for the beat to read. `Ghost_Khall` reuses the same Named prefab as Beat 5's voice-only Khall reference (there is no separate "young Khall" asset) with `MakeGhostMaterial()` applied to every renderer for the "recording, not reality" read Chapter 3 established.

**Relative orientation for the child/hands tableau, pinning the one interpretation gap left in the staging above.** Canon has the hands working "the base of the skull bared," so `VisionA_ChildFigure` should lie prone on `VisionA_Table` (world (0,0.4,257)) with the base of the skull oriented toward the gloved-hands/killswitch prop recommended above, not face-up or side-on to it. The player enters via `diveEntryPoint` at world (0,1,252) facing +Z — approaching the table and the act head-on down the room's long axis — so "face turned away" should read specifically as turned away from that entry sightline (i.e., away from -Z), not merely averted in some unspecified direction. This pins the tableau to one buildable arrangement instead of leaving the spatial relationship of child, hands, and player entry to a second interpretation pass.

**`Ghost_FootageRonin`'s pose is a staging gap, not just an art-fidelity placeholder.** `Ch8BuildGhostFigure` builds it as an upright capsule+sphere at height 1.8 — a standing silhouette. But Vision B's emotional center is the body on the floor: "RONIN-7, across the bay, drops... folding to the deck," and Khall reads the kill-order and speaks three of his four `ch8_beat4_vision_b` lines with his "eyes stay fixed on the console, unable to fall to the body on the deck." A standing figure gives Khall no fallen body to avert his eyes from, gutting the staging of the vision's longest fuse. Pose `Ghost_FootageRonin` prone/collapsed at low y (lying on the floor, not standing) — the dominant state across the whole of Vision B — or at minimum flag the standing pose as a staging placeholder alongside its existing art-fidelity flag. This is a cheap position/rotation change on the existing procedural figure, not a commission.

**`Ghost_Khall`'s blocking faces away from the console it is supposed to be triggering — and the rationale runs deeper than the trigger hand alone.** Canon stages Khall with "one hand resting on the console where the trigger is," and "Khall's hand closes on the console" is what fires the switch. But canon's blocking logic is richer than anchoring a hand: Khall's "eyes stay fixed on the console, unable to fall to the body on the deck" (dialogue script, Beat 4 stage direction) — he faces the console specifically to avoid looking at the body he believes he just killed, and he speaks three of his four `ch8_beat4_vision_b` lines while fixed that way. In the build, `Ghost_Khall` sits at local (0,0,25) rotated Euler(0,180,0) — facing −Z, toward the room's entry and toward `Ghost_FootageRonin` at z=20 — while `VisionB_Console` sits behind him at local (0,0.55,26.5). As built, Khall has it backwards on both axes at once: he faces the fallen figure he can't bear to look at, and turns his back on the console he can't stop staring at. Either move `Ghost_Khall` to face the console (or stand beside it, hand toward it) or move `VisionB_Console` into his facing/reach, so the "hand resting on the trigger" the vision turns on has a physical anchor — as staged today the console reads as unrelated set-dressing during the exact line ("You were right... and I'm still going to do this anyway," `Chapter8Lines.cs:250` — the screenplay's "and I still have to do this" did not survive into the wired line, per the Beat 0e quoting rule) where it should be the scene's focal prop. **This fix dovetails with the prone-pose fix immediately above:** once Khall faces +Z toward the console, `Ghost_FootageRonin` lying at z=20 is correctly positioned *behind* him — the body on the deck his eyes can't fall to. Applied together, the two fixes stage exactly the blocking canon describes; applied alone, either one leaves Khall's gaze pointed at nothing in particular.

**Vision B's second half is entirely VO-carried and has no physical anchor for its own plot beat.** Khall's doubt over the forged order — the longest fuse in the chapter, paid off at Ch16 — turns on a specific physical object: he "draws a folded document from inside his coat, the kill-order, and turns it over in his hands, reading it again, and again" before asking "Who forges a leash." `VisionB_Console` anchors the trigger half of the vision; nothing anchors this half. A `Props.KillOrder` row — a small folded-document prop, either parented to `Ghost_Khall`'s hand or built as its own ghost-material object near it — gives this beat's longest fuse a visual anchor the same way the console anchors the trigger, instead of leaving it as pure narration over an empty-handed ghost.

#### d. Combat

None. Beat 4 is reveal/dialogue/traversal only.

#### e. Dialogue / VO

Six dialogue sets in sequence, all advanced on Left-Hand **Talk** (Y):

- **`ch8_beat4_naming`** (0,1,68) — 3 lines, ≈47 s: the Mourners name him; Ronin-7 states his ask; the Mourners grant it on their own terms.
- **`ch8_beat4_vision_a`** (0,1,255) — 3 lines, ≈9 s: The Woman's two whispered lines ("There. That's the last of it." / "I hope you will save us.") and Ronin-7's unanswered demand ("Turn. Let me see your face.").
- **`ch8_beat4_vision_b`** (0,1,272) — 4 lines, ≈28 s: Khall's confession, apology, doubt over the kill-order, and the unanswered question "Who forges a leash."
- **`ch8_beat4_aftermath`** (0,1,69) — 4 lines, ≈47 s: Ronin-7's admission he still couldn't see her face; the Mourners' answer reframing the debt; Echo's promise to keep the prayer; Ronin-7's reframe ("I've been chasing the wrong shape").

**Audit note:** per `Chapter8Lines.cs`'s comment, the Beat-4 Aftermath opening line was rewritten from the screenplay's more on-the-nose "I couldn't see her... I still couldn't see her face" restatement to "She was right there. The one who made me. And I still couldn't see her face." — quote the wired line, not the screenplay, per the same rule flagged in Beat 0.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point — the reveal, the vision teleports, and the aftermath are carried by lighting, spatial audio, and the `MemoryDiveController`'s comfort-safe instant position set.
- **Comfort-safe vision dive:** per `MemoryDiveController`'s own doc comment, both `EnterDive`/`ExitDive` are instant, non-lerp position/rotation sets — **no forced camera motion, no fade-through-black requirement**, consistent with the VR constraint against motion that isn't player-initiated. `MemoryFlashbackController`'s fog/ambient reapplication on every `EnterDive` and restoration on `ExitDive` is a `RenderSettings` change only, not a camera move.
- **The garden→Vision-A luminance jump is a separate VR-comfort axis from the motion-comfort case above — currently unaddressed.** The dive teleport itself is correctly motion-comfort-safe (instant, no fade, no forced camera move — immediately above). But the *content* either side of that cut is a hard dark→bright swing: `VisionA_Light` intensity 1.8 against `whiteFloor`/`whiteCeil` ≈0.75–0.88 (Appendix A.4), entered by an instant teleport straight out of the dark, dense-fog plain (ambient 0.22, fog density 0.032, §3.1). VR guidance treats a sudden luminance spike as a photosensitivity/flash-comfort hazard on par with unrequested motion, and nothing in `MemoryDiveController`/`MemoryFlashbackController` addresses it — the "no fade-through-black requirement" above is correct for motion comfort but leaves this axis unflagged on the one hard dark→white cut in the chapter. Recommend a brief exposure ramp (or a short brightness-only fade-up, not a full scene fade) specifically on `EnterDive` into Vision A. **Correction: the Vision A→B change is not a second cut.** `VisionA_Light` (intensity 1.8, range 12, local z=7) reaches to z≈19 and `VisionB_Light` (intensity 1.2, range 12, local z=22) reaches back to z≈10, so the z[10,15] band between the rooms is co-lit by both lights and the white→grey shift happens as a continuous walk-blend, not an instant swap — only the dark-plain→Vision-A leg on `EnterDive` is a true teleport cut. The exposure-ramp recommendation above should be scoped to that `EnterDive` cut only; a future pass should not add an unneeded ramp to the A→B walk, which is already gradual.
- `BarrowLight0`/`BarrowLight1` continue their pulse/flat split from Beat 3 unchanged through the ring's manifestation — no dedicated "reveal lighting" event exists in the builder; the Mourners' visual arrival is carried entirely by the ring GameObjects activating, not by a lighting cue. **Canon is specific enough to turn this into a concrete recommendation, not just a flagged gap:** the Mourners are described "rising not from the graves but from the fog itself, a circle of figures closing in," and manifesting "gave them shape, not mouths" — a *resolving*, not a snap-in. `ringGo.SetActive(true)` today is a hard pop with no lead-in. Pair the activation with a brief fade-in on the ring's renderers (or a matching fog-dissipation treatment around the ring, once `Vfx.GroundFog` exists per §3) so the figures resolve out of the fog rather than appear instantaneously — the reveal is the chapter's dramatic apex and a hard pop undersells it.
- No haptics scripted for this beat.
- **Candidate addition, not yet built:** the vision-dive enter/exit (steps 13/16) and the Mourners' manifestation (step 11) are the chapter's two biggest state changes and currently carry zero feedback of any kind. A soft, non-shake haptic pulse on the teleport (both directions) and on the ring's activation — paired with the fade-in/dissipation treatment above for step 11 specifically — would be a cheap, comfort-safe immersion win in a level this sensory-sparse — consistent with "feel comes from Haptics, never camera shake" (§1.1). Flagged as a recommendation for whoever picks up combat/feel tuning on this chapter, not implemented today.

---

### Beat 5 — The Leaving (The Gate of Fog, The Reunion)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (there is no new geometry — this beat reuses the Beat 1 gate area and the Beat 0 spawn area unmodified) and **`BuildBeat5Logic()`** (the reach point, dialogue, the `ChapterOutro`/flag/canvas). **Nothing is built for the crew** — the reunion, like the briefing, is entirely voice-only per the CREW-PRESENCE DECISION (§3).

#### a. Narrative purpose & emotional target

The Mourners' farewell refuses to soften the trial's cost — *"we don't give you peace... peace is for the buried"* — and delivers the descent hook as prophecy, not an infrastructure inventory: *"the hand that made you and the hand that doubts you both reach up from below... go down to them."* This closes Act II and hands directly into Chapter 9. The reunion at the gate is the beat's emotional payoff: Kessler's vow from Beat 0/1 is fulfilled ("we held the gate, every hour of it"), and Mira's plain child's read of Ronin-7's face ("You're sad... So why do you look like that.") is the only crew reaction that lands without any strategic framing at all — everyone else processes the reveal as information; only Mira reads it as grief. The chapter's very last line reframes the whole trail: *"So we stop hunting the surface. We go down."*

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **Player:** free-walks back from the barrow (z≈69) to `GateReturnReachPoint` (0, 1, 12), radius **5**, satisfying step 19 before the reunion dialogue plays. No scripted path; continuous locomotion + snap-turn only, same as every traversal stretch in this chapter.
- **`mournersRingGo` never deactivates — the ring has no Beat-5 inverse of its own manifestation.** `MournersRing` is `SetActive(true)`'d at step 11 (§4, Beat 4b) and never `SetActive(false)`'d anywhere in `Chapter8Builder.cs`; the four figures are still standing at the barrow, ring-formed, for the rest of the chapter's runtime. Canon's Beat 5 stage direction is explicit that they don't stay: "The Mourners are gone, sunk back into the standing fog, the ring dissolved." Recommend deactivating (or fading out, mirroring the fade-*in* already recommended for step 11 in Beat 4f) `mournersRingGo` as the player departs the barrow — the inverse bookend of the manifestation, and the companion half of a "resolve out of fog on arrival, sink back into fog on departure" treatment that today is only half-flagged. **Mitigating factor:** fog density 0.032 (§3) obscures the ring within roughly 6 m, so a player walking straight back toward the gate (z=12) loses sight of the barrow naturally and the gap is only visible to a player who lingers or turns to look — worth a fix, not urgent.
- **`ChapterOutro`** at (0, 1, 13), built **inactive**. `CampaignFlagSetter` flag `"ch8_complete"` wired to `OnActivated` via a persistent listener (`UnityEventTools.AddPersistentListener(outro.OnActivated, flagSetter.SetFlags)`); `completeCanvas` ref = the "CHAPTER 8 COMPLETE" world-space canvas at (0, 1.4, 13). **No `AbilityGranter`** (Ch8 ships no new ability) and **no ally-recruit flag** (no ally is recruited this chapter) — both explicit omissions per the builder's class-summary comment, distinguishing this outro from chapters that grant an ability or unlock a crew member.

**Mission-spine steps:**

| # | Step | Kind | Fires on |
|---|---|---|---|
| 18 | Beat5: The Leaving (the Mourners' farewell) | Dialogue | plays `ch8_beat5_leaving` |
| 19 | ReachTrigger: The Gate (the way back) | ReachTrigger | player enters `GateReturnReachPoint` (0,1,12), radius 5 |
| 20 | Beat5: The Reunion (the crew, the report) | Dialogue | plays `ch8_beat5_reunion` |
| 21 | Trigger: Chapter Outro (flag + fade + canvas) | Trigger | activates `outroGo` |

Step 21 both ends Beat 5 and ends Chapter 8: activating `ChapterOutro` invokes its wired `OnActivated` (setting campaign flag `ch8_complete`), reveals the complete canvas, and (per `ChapterOutro`'s standard behavior, shared with every other chapter's outro — see Ch1 §4 Beat 4 for the fully documented fade/`ZoneCompleted` sequence) fades to black and publishes `ZoneCompleted`.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

**Nothing new geometrically** — Beat 5 plays out entirely inside geometry Beat 1 and Beat 0 already built (the fog-gate area, the open spawn ground). The only new object is the (inactive-until-triggered) complete canvas. **The fog is not "nothing new," though**: per §3's fourth `Vfx.GroundFog` state, canon has the ring part and the fog "stand open in a long aisle running back the way he came, toward the gate" as the player departs the barrow — a VFX state change riding the existing geometry, not new geometry of its own. This is the exit-aisle state `Vfx.GroundFog` must carry once built (§3); it belongs conceptually at the top of Beat 5, ahead of the walk back to `GateReturnReachPoint`.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| "CHAPTER 8 COMPLETE" canvas | (0, 1.4, 13) | — | `Ch8BuildCompleteCanvas`, worldspace `Canvas`+`Image`+`Text`, no prefab | greybox (UI, not environment art) |
| `GroundFog` (Beat-5 exit-aisle state — see §3) | aisle from barrow (0,-1,68) back to gate (0,1.8,10) | `Vfx.GroundFog` | `…/Art/Generated/VFX/GroundFog.prefab` | **MISSING** *(fourth scripted fog state; no row or wiring exists today — see §3)* |

#### d. Combat

None.

#### e. Dialogue / VO

- **`ch8_beat5_leaving`** (`Dialogue_Beat5_Leaving`, (0,1,70)) — 3 lines, ≈44 s: the Mourners' farewell and descent hook; Ronin-7 pressing for plainness; the Mourners' final "go down to them."
- **`ch8_beat5_reunion`** (`Dialogue_Beat5_Reunion`, (0,1,11)) — 9 lines, ≈93 s: Kessler's relief, Ronin-7's report, Coral's question, Ronin-7's fuller report (naming what he saw without naming who), Mera's synthesis, Mira's plain read, Ronin-7's answer to her, Ronin-7's reframe to the crew, Echo's closing line into Act III.

#### f. Audio / Haptics / VR Comfort

- **No camera shake.**
- `SilentGardenWindAmbience` continues through the walk back, giving way narratively (not mechanically — no crossfade is scripted) to the returning crew voices at the reunion.
- **The comm-static bookend's return half (unspecified — new recommendation, mirrors Beat 1f above).** `ch8_beat5_reunion`'s opening Kessler line is stage-directed "(comm, then voice)" — "the first sound from outside in the whole chapter, faint, then growing" — the payoff of the vow Kessler makes twice (Beat 0, Beat 1). As built, the line plays at the same full clarity as every other dialogue clip, with no proximity-driven treatment. Recommend a mirrored comm-static-into-clear-voice swell keyed to the player's approach to `GateReturnReachPoint` (0,1,12) — filtered/faint at the edge of the trigger radius, clearing to full voice by the time `ch8_beat5_reunion` begins — so the line arrives "faint, then growing" rather than hard-cut. Hard-cutting the crew back in throws away the chapter's single most load-bearing audio arc (§3's "the silence is the chapter's pulse" thesis).
- No haptics scripted.
- Standard comfort vignette on the return walk.
- **Reverb:** `ReverbZonePlacer.AutoTagInteriorVolumes()` + `PlaceReverbZonesForInteriorVolumes()` run once at the end of the build (§4 builder call order) — since the outdoor plain has no walls, this call's practical effect in Ch8 is limited to the two enclosed Vision Dive rooms (Beat 4), which read as tighter/boomier than the open Garden, the same contrast Ch1's Command Room vs. corridor achieves with real architecture.

## 5. Character travel-route master table

**Chapter 8 breaks the `NpcWalker` convention entirely.** Unlike Chapter 1 (Kessler's two scripted legs) or later chapters that stage NPC movement via inactive `NpcWalker` + `MissionDirector` Trigger, **`Chapter8Builder.cs` contains zero `BuildNpcWalker` calls.** Every named presence in this chapter is either voice-only or statically placed:

| Character | Presence | Position(s) | Movement |
|---|---|---|---|
| The crew (Kessler, Coral Vex, Mera Voss, Morrigan, Iris, Resh, Mira) | voice-only, never instantiated as GameObjects | — | none — no physical placement anywhere in the scene (§3, CREW-PRESENCE DECISION) |
| Khall (as `Ghost_Khall`) | statically placed inside the Vision Dive island only | local (0,0,25) → world (0,0,275), Beat 4 | none — instantiated once, faces the entry point, never moves |
| The Mourners (×4, `MournersRing`) | statically placed, ring around the barrow | ring center (0,0,68), radius 5, 90° spacing | none — built inactive, `SetActive(true)` in place at step 11; no walk-in |
| The Warden | statically placed at the barrow approach | (0,0,66) | none in the build sense — its `Enemy` AI moves it during combat once active (standard chase/attack behavior), but it is placed, not walked to its mark |
| The Buried ×3 | statically placed near the riddle pads | (-2,0,40) / (0,0,42) / (2,0,40) | none in the build sense — same as the Warden, `Enemy` AI governs in-combat movement only |

**There is no Y-invariant to guard in this chapter the way Ch1's `kesslerFloorY` must be threaded through two `NpcWalker` waypoint arrays** — because nothing walks a scripted route, there is no waypoint math to regression-test. `RiddleTrialLogicTests.cs` and `Chapter8LinesTests.cs` are the chapter's only pure-logic EditMode coverage (§8); neither touches character placement.

**If a future pass wants Khall or the Mourners to walk into place** (e.g. the ring visibly closing in rather than popping active, or footage-Ronin/Khall performing blocking inside the vision beyond their current static poses), that is new logic — the `BuildNpcWalker` helper exists and is frozen/reusable (§1.2), but nothing in Ch8 currently calls it.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`** in the target state, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Key/accent entry | Behaviour | Backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Cairn (briefing) | flat, sourceless grey; no exterior view | (chapter-wide key light, no beat-specific accent yet reached) | — | none — voice-only | none |
| 1 — The Gate of Fog | still, cool, threshold-quiet | `accentLights["Gate"]` | `None` | the fog-gate marker, static | none — the gate never changes visual state |
| 2 — The Trial of Mind | unstable, testing | `accentLights["Trial"]` | `ConsoleFlicker(seed: 88)` | headstone field, static | **Prompt (step 5):** `GraveRiddleTrial` inactive→active; **conditional:** wrong answer wakes the buried (3 inactive→active) |
| 3 — The Warden | held-breath stillness, then combat | `accentLights["Barrow0"]` (pulse) / `accentLights["Barrow1"]` (flat) | `AmbientPulse(6.6s)` / `None` | barrow mound, static | **DefeatEnemies (step 9):** the Warden inactive→active |
| 4 — The One Who Defies | ceremonial, then two vision moods (sterile white / dim grey-blue) | same barrow accents, unchanged | unchanged | barrow → Vision A (white, `whiteFloor`/`whiteCeil`) → Vision B (grey, `greyFloor`/`greyCeil`) → barrow again | **Trigger (step 11):** `MournersRing` inactive→active. **Trigger (step 13):** `VisionDive` inactive→active, rig teleports in, `RenderSettings` snapshotted+reapplied. **Trigger (step 16):** `VisionDive` active→inactive, rig teleports out, `RenderSettings` restored |
| 5 — The Leaving | unchanged from Beat 1's gate mood, now walked in reverse | `accentLights["Gate"]` | `None` | gate area, static | **Trigger (step 21):** `ChapterOutro` inactive→active — flag set, canvas revealed, fade to black, `ZoneCompleted` published |

Fog is the same baseline exponential bed throughout — density 0.032, never overridden per-zone, reinforcing the "silence has pressure everywhere, uniformly" read (§3). This differs from Ch1's pattern of a beat-specific event light (`DockingAlarmLight`): **Ch8 has no event lights** — every state change in this chapter is a GameObject activation (an NPC, an enemy, a dive root), not a lighting cue.

**The Beat-4 white/grey vision swap is also a VR-comfort item, not just a mood shift — but only its first leg is a cut.** The table's "sterile white / dim grey-blue" row spans two different kinds of transition: the plain→Vision-A leg is a true instant cut on `EnterDive` (ambient 0.22 dense-fog plain → `VisionA_Light` intensity 1.8/`whiteFloor`≈0.75–0.88, ranged from local z=7), while the Vision-A→B leg is a continuous walk-blend, not a cut — `VisionA_Light`'s range-12 falloff (reaching to z≈19) and `VisionB_Light`'s range-12 falloff (reaching back to z≈10) overlap across the z[10,15] band, so white gives way to grey (`VisionB_Light` intensity 1.2/`greyFloor`≈0.22) gradually as the player walks it. The dark→white leg on `EnterDive` is the largest luminance delta in the chapter and is currently un-ramped; see Beat 4f for the concrete recommendation (a brief exposure/brightness ramp on `EnterDive` into Vision A only — the white→grey walk needs no such ramp).

## 7. Audio / VO manifest cross-reference

Fourteen canonical dialogue sets, defined in `Chapter8Lines.cs` and consumed via `Chapter8Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch8_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch8_beat1_gate` | 1 | (0, 1, 9) — `Dialogue_Beat1_Gate` |
| `ch8_beat1_alone` | 1 | (0, 1, 15) — `Dialogue_Beat1_Alone` |
| `ch8_beat2_riddle_pose` | 2 | (0, 1, 30) — `Dialogue_Beat2_RiddlePose` |
| `ch8_beat2_wrong_bark` | 2 | (0, 1, 36) — `Dialogue_Beat2_WrongBark` *(wave-0 bark, not a spine step)* |
| `ch8_beat2_riddle_answer` | 2 | (0, 1, 36) — `Dialogue_Beat2_RiddleAnswer` |
| `ch8_beat3_warden_intro` | 3 | (0, 1, 60) — `Dialogue_Beat3_WardenIntro` |
| `ch8_beat3_warden_defeat` | 3 | (0, 1, 66) — `Dialogue_Beat3_WardenDefeat` |
| `ch8_beat4_naming` | 4 | (0, 1, 68) — `Dialogue_Beat4_Naming` |
| `ch8_beat4_vision_a` | 4 | (0, 1, 255) — `Dialogue_Beat4_VisionA` *(inside the Vision Dive)* |
| `ch8_beat4_vision_b` | 4 | (0, 1, 272) — `Dialogue_Beat4_VisionB` *(inside the Vision Dive)* |
| `ch8_beat4_aftermath` | 4 | (0, 1, 69) — `Dialogue_Beat4_Aftermath` |
| `ch8_beat5_leaving` | 5 | (0, 1, 70) — `Dialogue_Beat5_Leaving` |
| `ch8_beat5_reunion` | 5 | (0, 1, 11) — `Dialogue_Beat5_Reunion` |

Each is built by the local `Ch8BuildDialogue` wrapper (mirroring Ch1's `BuildChapter1Dialogue` pattern exactly): it calls the shared player builder with `clipSetId: null`, then wires clips itself via `Ch8WireVoiceClips`, resolving each line's `AudioClip` from `Chapter8Lines.ClipName(setId, index, speaker)` — pattern `ch8_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all fourteen `DialoguePlayer`s.

**Known cast** (asserted by `Chapter8LinesTests.EverySpeaker_IsInKnownCast`, must exactly match `Audio/Tools/generate_voice.py`'s `SPEAKER_VOICES`): Kessler, Ronin-7, Iris, Resh, Mira, Echo, Mera Voss, Morrigan, Coral Vex, Khall, The Mourners, The Woman. **Note:** Khall's name appears in `KnownCast` for TTS-casting purposes, but per the "seen but not named" continuity rule (§9), his lines inside the Vision B dive are spoken as himself (unlike Ch1's "Handler (Hologram)" pseudonym-in-dialogue-label convention) — the *player character* never learns the name "Khall" from this chapter; the speaker label existing internally is an authoring/TTS convenience, not proof the name reaches the player. *(Flagging this for cross-chapter consistency review — Ch1 deliberately mislabels Khall as "Handler (Hologram)" in dialogue data to keep him unnamed on-screen; Ch8's vision dialogue labels him "Khall" directly. Whether this is an intentional escalation — "you will see him again before you're told his name" — or a continuity gap depends on writers'-room intent not resolved in the sources reviewed for this document.)*

SFX bed, all under `Assets/Ronin7/Art/Generated/Audio` (unless noted):

| Clip | Used for |
|---|---|
| `WaveAlarm.wav` (`Assets/Ronin7/Audio/WaveAlarm.wav`) | `BuriedWaveSpawner`'s wave sting, reused from the same asset Ch1's docking alarm uses |
| `SilentGardenWindAmbience` (procedural, via `BuildAmbienceLayer`) | 2D-ish 3D ambience bed at (0,2.2,10), covering the gate/spawn approach |
| `BarrowDreadAmbience` (procedural, via `BuildAmbienceLayer`) | 3D ambience bed at (-4,2.6,66), covering the barrow/Warden arena |
| `WardenToll` | **MISSING** *(new key this pass — no code references it yet; see Beat 3f)*. The Warden's grave-bell: a low toll `AudioDirector` stinger on step-9 activation/attacks, a distinct cracked-bell one-shot on defeat — the one sound canon licenses to break the Garden's silence |
| `BarrowDoorGrind` | **MISSING** *(new key this pass — no code references it yet; see Beat 3b/3c/3f)*. Stone-on-stone grind on the barrow's grave-iron door: on the Warden's rise (the new entrance-`Trigger` recommendation) and its mirror on the sink-death close |
| `FogRecoil` | **MISSING** *(new key this pass — no code references it yet; see Beat 2b/2c)*. The trial-ending fog "recoil… like a tide pulled out" on `RiddleTrial.onRightAnswer` — distinct from `WaveAlarm.wav` and from `WardenToll` |
| `CommFadeToStatic` / `CommSwellReturn` | **MISSING** *(new keys this pass — no code references them yet; see Beat 1f/Beat 5f)*. The chapter's audio bookend: `ch8_beat1_gate`'s Kessler line degrading to static as the player crosses toward the gate, mirrored by a proximity-driven static-into-clear swell on approach to `GateReturnReachPoint` ahead of `ch8_beat5_reunion`'s "comm, then voice" opening line |

**Tonal tension flagged, not fixed.** Reusing Ch1's mechanical docking-alarm klaxon as the buried-wave sting cuts against this chapter's own sound-design thesis: per the dialogue script, "when the dead rise on a wrong answer, the silence does not break with sound; it breaks with the headstones, which is worse," and the buried encounter is meant to read as sorrow, guardians that "sink, never gib." A recycled mechanical alarm is the opposite register. Recommend a sorrow-toned or near-subliminal cue — or a purely visual disturbance with no sting at all — in place of `WaveAlarm.wav` for this wave specifically; at minimum, note that the current wiring undercuts "the silence is the chapter's pulse" (§3) on the one combat trigger in an otherwise silent level.

**A second tonal tension, same species as the alarm above: the ambience beds are named and built as wind.** `SilentGardenWindAmbience` (`Chapter8Builder.cs:342`, table above) is named and functions as a wind bed. But canon is emphatic that this chapter has no weather at all: "There is no sound but the one Ronin-7 brings with him" (dialogue script SETTING block, line 22), and §3 of this document already instructs treating the fog itself "never as weather." A literal wind layer is weather-sound, cutting against the chapter's own thesis the same way `WaveAlarm.wav` does. Recommend both `SilentGardenWindAmbience` and `BarrowDreadAmbience` be treated, on the next audio pass, as pressurized, sub-audible dread — "silence with weight," not a wind treatment — so the commission doesn't take the `Wind` name literally and undercut "the silence is the chapter's pulse" with an audible weather cue the setting explicitly denies.

**A third ambience-coverage gap, distinct from the two tonal ones above: the two beds leave a dead zone across the trial's combat heart.** `SilentGardenWindAmbience` (position z=10, outer radius 26 → falls to silence by z≈36) and `BarrowDreadAmbience` (position z=66, outer radius 18 → reaches back to z≈48) leave roughly z[36,48] uncovered by either bed. The riddle pads (z=35), the buried spawns (z=40–42), and the wrong-answer wave all sit inside or adjacent to that ≈12 m gap — the exact dramatic center of the one built puzzle (Beat 2). Given this document's own §3 thesis that the pressurized silence *is* the ambience, a bare dead zone here risks reading as ordinary "empty/quiet" rather than "silence with weight" at the one combat trigger the chapter has. Flag it as a decision, not an accident: either widen the two beds' outer radius so they overlap across the midfield, or add a third `BuildAmbienceLayer` centered near (0,2.2,40) covering the trial zone, so the pressure bed doesn't thin out on the chapter's only combat trigger.

**VR-audio directive — confirmed, not a gap.** Canon repeatedly specifies the Mourners' braided voice "arrives from no direction and no mouth" (dialogue script SETTING block and Beat 1). Checked against the as-built code: `BuildDialoguePlayer`'s `AudioSource` (`ChapterSharedBuilders.cs`) never sets `spatialBlend`, so it carries Unity's default of `0` (fully 2D / non-positional) — the same is true of every `DialoguePlayer` in every chapter, not a Ch8-specific override. All fourteen dialogue sets in this chapter, including the crew, Echo, and the Mourners, already play non-spatialized: nothing pans or localizes as the player snap-turns. No new logic is needed to satisfy "no direction and no mouth" — this is an existing invariant worth stating explicitly so a future pass doesn't "fix" it by adding 3D spatialization to the Mourners specifically, which would contradict canon.

`ProceduralAudioClipBuilder.AssignGeneratedClips()` runs once at the end of the build to backfill any procedurally-generated clips the ambience layers reference. No dedicated door-slide, alarm-clank, or hologram-bloom one-shots exist in this chapter (no doors, no alarm light, no hologram reveal) — Ch8's SFX footprint is deliberately sparser than Ch1's, consistent with "the silence is the chapter's pulse."

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 08 — The Silent Garden** (`XRRigBuilder.BuildChapter8SilentGarden()`).
2. **EditMode is the gate.** Per `CLAUDE.md`'s stated baseline: **842 tests green, 0 skips**; PlayMode: **70/70 green**. Re-verify the live count with `tests-run` before trusting this number — chapter builds land regularly and the baseline drifts upward; treat the CLAUDE.md figure as a floor, not a live count. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot, same shape as Chapter 1's.** **No EditMode test invokes `BuildChapter8SilentGarden()` or loads `Ch08_SilentGarden.unity`.** The suite covers pure logic only: `Chapter8LinesTests.cs` (14 set IDs, speaker/text/seconds validity, no em-dashes, unique clip names, known-cast membership, builder/data set-ID sync) and `RiddleTrialLogicTests.cs` (the `RiddleTrialLogic` pure-C# submit/pass/fail state machine, no scene involved). **A green suite says nothing about whether the scene still builds correctly, whether the Warden's `Enemy` wiring resolves, or whether the vision-dive teleport lands the rig where expected.** Every structural change in this refactor must be verified by opening the scene and looking at it.
3. **Riddle-trial regression coverage.** `RiddleTrialLogicTests.cs` tests the state machine `RiddleTrial` wraps (`Submit`/`Passed`/correct-vs-wrong branching) in isolation from the scene. **It does not test `Ch8BuildAnswerPad`'s pad placement, `answerRadius` tuning, or the `onWrongAnswer`/`onRightAnswer` UnityEvent wiring** — those are only exercisable by building the scene and physically walking to each pad. Any patch to the riddle's answer-pad geometry or wiring must be manually verified in-editor.
4. **Dialogue-data regression coverage.** `Chapter8LinesTests.cs`'s `EveryBuilderReferencedSetId_ExistsInSetIds` test is the one automated guard against the builder and the dialogue data drifting apart — if a future patch adds a new `Ch8BuildDialogue(...)` call with a new set ID, that ID must also be added to `Chapter8Lines.SetIds` and to the test's `BuilderReferencedSetIds` array, or the sync check silently stops catching drift for the new set.
5. **Safe-zone survival test (deferred, same as Chapter 1 — see §1.4).** Not yet applicable: the wipe strategy for Ch8, like every other chapter, is still `EditorSceneManager.NewScene(...)`, so `[STATIC_ART_DO_NOT_DELETE]` cannot yet be verified to survive a rebuild because it does not yet exist in this scene.
6. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty plain, never an exception.
7. **Perf reference bar — not yet recorded (§1.6).** Capture an edit-mode `UnityStats` pass (drawCalls, setPassCalls, tris, verts) at the current greybox state and add it to `Project/Docs/CHAPTER-BUILD-LEDGER.md` before any prefab lands, then re-measure after every swap. The headstone field (§1.6) is the first place to look if the count comes back high.
8. **Console check:** `Ch8WireVoiceClips`'s per-set warning (`only N/M voice clips resolved for set '...'`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild.
9. **Vision-dive teleport check (chapter-specific, new).** Because `MemoryDiveController` moves the rig outside `ZoneBounds`' normal working range (z=250+ vs. the plain's z=0–95), any change to `bounds.radius` (currently 180, centered at (0,3,125)) must be re-verified to still cover both the plain and the dive island's full extent (Vision B's far wall sits at world z≈279, distance ≈154 from the bounds center) — shrinking the radius without checking this will clip the player at the edge of the vision rooms.
10. **Vision A→B floor check (chapter-specific, new — see §4, Beat 4b/4c).** Walk from `VisionEntryPoint` (local z=2) north through the z[10,15] gap between the two vision rooms and confirm the rig stays on a floor the whole way; today that band has no floor and no side walls. Re-run this check after any fix to the gap (a connecting floor strip, or a deliberately staged void threshold).

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one (not-yet-real) exception.** Re-running `BuildChapter8SilentGarden()` wipes generated content via `NewScene`. The house rule remains: patch additively in the live editor, or fix `Chapter8Builder.cs` and treat a rebuild as a deliberate, scoped action. Unlike the note in §1.4, **there is no sanctioned safe zone yet for this chapter** — the `[STATIC_ART_DO_NOT_DELETE]` exception described in §1.4 is aspirational until the wipe-strategy conversion lands for Ch8 specifically (it has not landed for any chapter as of this writing).
- **Do not auto-delete orphan materials.** Same project-wide caution as every other chapter document — ~288 unreferenced material variants exist project-wide but are regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Same project-wide rule as Ch1 §9 — the current MeshCollider-free audit covers the 14 shipped scenes as of 2026-07-04; a high-fidelity art pass on the headstone field or the Warden is exactly the kind of import that could reintroduce one.
- **Puzzle scope cut is the single biggest content gap in this chapter (flagged repeatedly above, consolidated here).** The source design specifies three riddles; the builder mechanizes one. If a future content pass wants to build Puzzle A (Grave of True Names) or Puzzle B (The Honest Order, including its "Phantom of the Obedient Cipher" mirror-fight), that is net-new `RiddleTrial`/`EnemyWaveSpawner` wiring plus new art, not a patch to the existing `GraveRiddleTrial` object. Do not silently repurpose the existing trial's answer pads for a different puzzle's content.
- **The Warden's phased/seam-based fight described in the dialogue script is not mechanized (§4, Beat 3d).** The built encounter is a flat `Enemy`/`Health` fight at 340 HP with a Named-mesh placeholder. Anyone picking up combat-encounter work on this chapter should treat the "weakpoint-sight showcase" framing as a design target, not a shipped feature. **The Warden's defeat also has no "sinks into the barrow" death treatment (§4, Beat 3d)** — it dies via default `Enemy`/`Health` handling today, the same mob-style-death gap Beat 2c flags for the buried, but on the chapter's single most-watched kill; this is a separate gap from the phase mechanics and should be fixed even if the phased fight stays unmechanized. **The Warden's entrance has the identical gap on the other end of its life (§4, Beat 3b/3c):** the boss-intro dialogue plays before `wardenEnemy` is ever activated, and the barrow's own grave-iron door (canon names it three times) doesn't exist in the build at all — no opening for the rise, none for the sink. All three (entrance, phased fight, death treatment) are separate, independently fixable gaps on the same encounter; fixing one does not require fixing the others. **The entrance fix specifically (the recommended "Warden Rises" `Trigger` step, §4 Beat 3b) renumbers every mission-spine step from 8 onward** — see the mechanical-cost note there before implementing it in isolation.
- **Khall naming inconsistency across chapters (flagged in §7).** Ch1 keeps Khall unnamed in dialogue *data* via the "Handler (Hologram)" label; Ch8's vision dialogue labels him "Khall" directly in `Chapter8Lines.cs`. Whether the player-facing experience is meant to differ (a vision showing his true self vs. a live hologram staying anonymous) is a writers'-room call not resolved by the sources reviewed for this document — **do not silently normalize one convention into the other** when touching either chapter's dialogue data.
- **No greybox performance baseline recorded for Ch8 (§1.6, §8).** This is the first action item for whoever picks up the art-registry work on this chapter — every other chapter document in this series (starting with Ch1) can cite a measured drawCalls/setPassCalls/tris/verts bar; Ch8 cannot yet.
- **No Cairn/ramp backdrop exists at the spawn/gate area — open question, not a fix (§2, §3).** The CREW-PRESENCE DECISION (§3) correctly explains why no crew *bodies* appear, but the ship and its ramp are set-dressing, not characters, and both bookend beats lean on them: the briefing frame has "the ramp comes down onto wet grass," and the Beat-5 reunion has Ronin-7 walk "up the ramp into the Cairn." Today the player returns to z=11–13 to an entirely empty plain — no ship, no ramp, nothing at the gate area beyond `FogGateWall` and the two Beat-0/Beat-1 dialogue anchors. Whether a static Cairn/ramp backdrop prop at the gate/spawn area would anchor the Act-II bookends, or whether the deliberate "endless empty plain" read is meant to win outright, is a decision to surface, not silently leave as bare ground — this document takes no position on it. **This dovetails with Beat 0's own gap (§4, Beat 0a):** the reason the garden briefing has no ship to stand beside is that canon's command-room scene canonically happens aboard the Cairn, in `Ch08_Prologue.unity`, before the descent this backdrop question is about — a Cairn/ramp prop here would be dressing a scene the ship itself never physically occupies in the build.
- **The fog-gate has no physical or logical barrier (§2).** If future playtesting shows players walking past the "held" crew's dialogue before it finishes (since nothing blocks forward movement at z=10), that is a real UX gap worth flagging upstream — this document only records that the gate is unenforced today, it does not fix it.
- **The Vision A → Vision B walk inside the dive has the same shape of gap, on a higher-stakes beat (§4, Beat 4b).** Steps 14/15 (`ch8_beat4_vision_a`/`ch8_beat4_vision_b`) are two button-advanced `Dialogue` steps with no `ReachTrigger` walking the player from Vision A into Vision B between them, so nothing stops a player from advancing Khall's entire vision without ever entering the handler's bay or seeing `Ghost_Khall`. Recommended fix (a `VisionB` reach point at ≈local (0,1,15)) is noted at the point of the gap; this line only flags it here for visibility alongside the other unenforced-gate items.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> Unlike Chapter 1, `Chapter8Builder.cs` is **one monolithic method** (`BuildChapter8SilentGarden()`) — there are no `BuildBeat0Art`/`BuildBeat0Logic`-style method pairs today. Every row below cites the actual helper/inline block inside that single method, or the chapter-local `Ch8*` helper it calls.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials, exactly as in Chapter 1.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch8Environment.asset`. Currently set inline at the top of `BuildChapter8SilentGarden` (`Chapter8Builder.cs:95-113`).

| | Value |
|---|---|
| Directional key | color (0.6, 0.62, 0.66), intensity 0.3, rotation Euler(60, -20, 0) |
| Ambient | mode **Flat**, color (0.22, 0.23, 0.25) |
| Fog | mode **Exponential**, color (0.52, 0.54, 0.57), density 0.032 |
| Ground tint (`GardenGround`) | (0.16, 0.18, 0.17) |
| Fog-gate tint (`FogGateWall`) | (0.58, 0.6, 0.63) |
| Barrow tint | (0.2, 0.24, 0.2) |
| Headstone tint | (0.35, 0.36, 0.38) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour | Read |
|---|---|---|---|---|---|---|
| `GateLight` | (0, 2.2, 10) | (0.6, 0.64, 0.7) | 1 | 12 | none | still, cool threshold |
| `TrialLight` | (0, 2.2, 35) | (0.6, 0.64, 0.7) | 1 | 14 | `AddConsoleFlicker(seed: 88f)` | unstable, testing |
| `BarrowLight0` | (-4, 2.6, 66) | (0.7, 0.68, 0.62) | 1.4 | 16 | `AddAmbientPulse(period: 6.6f)` | slow-breathing warmth near the barrow |
| `BarrowLight1` | (4, 2.6, 70) | (0.7, 0.68, 0.62) | 1.4 | 16 | none | flat counterpart, keeps the pulse directional |

**No event lights.** Chapter 8 has no `eventLights[]`-style inactive-until-triggered light (contrast Ch1's `DockingAlarmLight`).

### A.2 Beat 1–2 — The Gate, The Grave-Paths, The Riddle Trial

| Element | Coordinates / value | Component / method |
|---|---|---|
| Ground | center (0,-0.5,45), scale (30,1,100) → x[-15,15], z[-5,95] | `GardenGround` primitive Cube |
| Fog gate marker | (0,1.8,10), scale (14,3.6,0.6), collider stripped | `FogGateWall` primitive Cube |
| Headstone field | for z from 14 to 62 step 4: `t=(z-14)/4`; `jitterA = sin(t*2.3)*2.5`; `jitterB = cos(t*1.7)*2.5`; west stone at `(-4-jitterA, 0.4, z)`, east stone at `(4+jitterB, 0.4, z)`, both scale (0.5,0.8,0.2), tint (0.35,0.36,0.38) | `Ch8ScatterHeadstones(world, 14f, 62f, 4f)` — 13 loop iterations × 2 = 26 headstone primitives |
| Answer pad "OBEY" | stone (-2.5,0.5,35) scale (0.8,1,0.25) tint (0.4,0.38,0.36); label (-2.5,1.3,35) scale 0.02, TextMesh "OBEY"; answer point (-2.5,1,35) | `Ch8BuildAnswerPad(world, "AnswerPad_Obey", (-2.5,0,35), "OBEY", (0.4,0.38,0.36))` |
| Answer pad "NONE" | stone (2.5,0.5,35) same scale/tint; label (2.5,1.3,35); answer point (2.5,1,35) | `Ch8BuildAnswerPad(world, "AnswerPad_None", (2.5,0,35), "NONE", (0.4,0.38,0.36))` |
| `GraveRiddleTrial` | `answerPoints[0]`=Obey point, `[1]`=None point; `correctAnswerIndex`=1; `answerRadius`=1.75; built `SetActive(false)` | `RiddleTrial` component + `SerializedObject` wiring |
| The Buried ×3 | (-2,0,40), (0,0,42), (2,0,40); `Ch8Buried.asset`: maxHealth 45, damage 9, moveSpeed 1.3, attackCooldown 0.85; built inactive | `BuildEnemy(pos, playerHealth, buriedDef)` × 3 |
| `BuriedWaveSpawner` | trigger (0,0,38), triggerRadius 10, 1 wave (all 3 buried), bark = `dlgWrongBark`; `waveSting` = `Assets/Ronin7/Audio/WaveAlarm.wav` if present | `BuildWaveSpawner("BuriedWaveSpawner", ...)` |
| Wiring | `riddleTrial.onWrongAnswer → buriedSpawner.Begin`; `riddleTrial.onRightAnswer → missionDirector.AdvanceFromPrompt` | `UnityEventTools.AddPersistentListener` ×2 |
| `GravePathReachPoint` | (0,1,25), reach radius 5 | `AuthorReachStep(steps, 3, ...)` |
| Dialogue players | `Dialogue_Beat1_Gate` (0,1,9) `ch8_beat1_gate`; `Dialogue_Beat1_Alone` (0,1,15) `ch8_beat1_alone`; `Dialogue_Beat2_RiddlePose` (0,1,30) `ch8_beat2_riddle_pose`; `Dialogue_Beat2_WrongBark` (0,1,36) `ch8_beat2_wrong_bark`; `Dialogue_Beat2_RiddleAnswer` (0,1,36) `ch8_beat2_riddle_answer` | `Ch8BuildDialogue(...)` ×5 |
| Katana "Echo" | (2,1,4), rot Euler(-90,0,0) | `BuildSword(pos, rot, weapon, Ch8EchoBladePrefab)` |
| Mission steps | indices 1–6 of 22 | `AuthorDialogueStep`/`AuthorReachStep`/`AuthorPromptStep` |

### A.3 Beat 3 — The Warden

| Item | Value | Source |
|---|---|---|
| `Ch8Warden.asset` | maxHealth 340, damage 26, moveSpeed 0.9, attackCooldown 1.1 | `Ch8EnsureWardenDefinition` |
| The Warden GameObject | `InstantiateNpc(Ch8WardenPrefab, (0,0,66), "The Warden")`, `FitNamedCharacter`, rotation Euler(0,180,0); `CapsuleCollider` center (0,1.2,0) height 2.6 radius 0.55; `Health`; synthetic `ArmR/Sword/Blade/BladeTip` chain (`BladeTip` local (0,0,0.6) under `Blade` under `Sword` under `ArmR` local (0.4,1.6,0)); `Enemy` wired `definition`/`weapon`/`bladeTip`/`bodyRenderer`/`target`; built `SetActive(false)` | `Ch8BuildWarden(pos, def, playerHealth)` |
| `The-Warden.prefab` spec | `PlaceholderArchetype.Massive`, primary (0.22,0.32,0.25), secondary (0.12,0.18,0.14) — "grave-iron green" | `PlaceholderCharacterBuilder.cs:52-53` |
| Commission target (Beat 3c) | composite of grave-iron/headstone/root/packed fog; a partial/unfinished humanoid ("half-shape... may once have been an operative or a Mourner or neither"), not a complete figure — see Beat 3c Notes | canon, not yet mechanized in the placeholder |
| `DeepGardenReachPoint` | (0,1,60), reach radius 6 | `AuthorReachStep(steps, 7, ...)` |
| Dialogue players | `Dialogue_Beat3_WardenIntro` (0,1,60) `ch8_beat3_warden_intro`; `Dialogue_Beat3_WardenDefeat` (0,1,66) `ch8_beat3_warden_defeat` | `Ch8BuildDialogue(...)` ×2 |
| Mission steps | indices 7–10 of 22 | `AuthorReachStep`/`AuthorDialogueStep`/`AuthorDefeatStep` |

### A.4 Beat 4 — The Still Center + Vision Dive

| Item | Value | Source |
|---|---|---|
| `Barrow` | (0,-1,68), scale (8,3,8), primitive Sphere, tint (0.2,0.24,0.2) | inline in `BuildChapter8SilentGarden` |
| `MournersRing` | center (0,0,68), radius 5, count 4; each figure at `center + (sin(angle)*r, 0, cos(angle)*r)` for `angle = i*(360/4)*Deg2Rad`, `LookAt` the center; built `SetActive(false)` | `Ch8BuildMournersRing(world, (0,0,68), 5f, 4)` |
| `The-Mourners.prefab` spec | `PlaceholderArchetype.Hooded`, primary (0.80,0.77,0.70), secondary (0.68,0.65,0.58) — "bone white" | `PlaceholderCharacterBuilder.cs:50-51` |
| Commission target (Beat 4c) | tint shifted toward the fog's own grey (≈(0.52,0.54,0.57)), not bone-white; faceless by design ("no faces to find") — see Beat 4c Notes | canon, not yet mechanized in the placeholder |
| `VisionDive` root | world (0,0,250); holds `MemoryFlashbackController`; built `SetActive(false)` | inline |
| Vision A room | floor/ceiling center local (0,0,5) size (8,0,10), colors whiteFloor (0.75,0.76,0.78)/whiteCeil (0.85,0.86,0.88); walls W/E at x=∓4, S at z=0, all `RoomH`=3.6; `VisionA_Table` local (0,0.4,7) size (1.6,0.8,0.7) tint (0.9,0.9,0.92); `VisionA_Light` local (0,2.4,7) color (0.95,0.96,1) intensity 1.8, range 12, no shadows | `Ch8BuildVisionRooms` |
| Vision B room | floor/ceiling center local (0,0,22) size (8,0,14), colors greyFloor (0.22,0.23,0.26)/greyCeil (0.13,0.14,0.16); walls W/E at x=∓4, N at z=29, all `RoomH`=3.6; `VisionB_Console` local (0,0.55,26.5) size (1.4,1.1,0.6) tint (0.3,0.31,0.34); `Ghost_FootageRonin` local (0,0,20), height 1.8, ghost material; `Ghost_Khall` local (0,0,25) rot Euler(0,180,0), Khall prefab with every renderer set to `MakeGhostMaterial()`; `VisionB_Light` local (0,2.4,22) color (0.55,0.6,0.7) intensity 1.2, range 12, no shadows | `Ch8BuildVisionRooms` |
| `Ch8BuildGhostFigure` geometry | capsule "Body" local (0, torsoH, 0) scale (h*0.22, torsoH, h*0.22) where `torsoH = h*0.42`; sphere "Head" local (0, h*0.9, 0) scale `h*0.16`; both colliders stripped, both `ghostMat` | `Ch8BuildGhostFigure(parent, name, pos, height, ghostMat)` |
| `VisionEntryPoint` | local (0,1,2), inside Vision A's z[0,10] bounds | inline |
| `VisionExitPoint` | world (6,1,68), Euler(0,180,0) — x=6 clears the barrow's collider | inline |
| `MemoryDiveController` | `diveRoot`=VisionDive; `diveEntryPoint`=VisionEntryPoint; `diveExitPoint`=VisionExitPoint; `rigRoot`=player rig transform; `flashback`=the `MemoryFlashbackController` on the dive root | `SerializedObject` wiring |
| `EnterVisionTrigger` / `ExitVisionTrigger` | both built inactive, each wired `dive`=`visionDiveController` | `MemoryDiveEntryTrigger` / `MemoryDiveExitTrigger` |
| Dialogue players | `Dialogue_Beat4_Naming` (0,1,68) `ch8_beat4_naming`; `Dialogue_Beat4_VisionA` (0,1,255) `ch8_beat4_vision_a`; `Dialogue_Beat4_VisionB` (0,1,272) `ch8_beat4_vision_b`; `Dialogue_Beat4_Aftermath` (0,1,69) `ch8_beat4_aftermath` | `Ch8BuildDialogue(...)` ×4 |
| Mission steps | indices 11–17 of 22 | `AuthorTriggerStep`/`AuthorDialogueStep` |

### A.5 Beat 5 — The Leaving + Reunion + Outro

| Element | Coordinates / value | Component / method |
|---|---|---|
| `GateReturnReachPoint` | (0,1,12), reach radius 5 | `AuthorReachStep(steps, 19, ...)` |
| Dialogue players | `Dialogue_Beat5_Leaving` (0,1,70) `ch8_beat5_leaving`; `Dialogue_Beat5_Reunion` (0,1,11) `ch8_beat5_reunion` | `Ch8BuildDialogue(...)` ×2 |
| "CHAPTER 8 COMPLETE" canvas | worldspace, position (0,1.4,13), rot Euler(0,180,0), size 700×220 scale 0.0015; bg color (0.04,0.05,0.08,0.9); label "CHAPTER 8 COMPLETE" fontSize 54 color (0.9,0.92,1); built `SetActive(false)` | `Ch8BuildCompleteCanvas((0,1.4,13))` |
| `ChapterOutro` | (0,1,13), built inactive; `CampaignFlagSetter.flags = ["ch8_complete"]`; `completeCanvas` ref wired; `OnActivated → flagSetter.SetFlags` persistent listener | `ChapterOutro` + `CampaignFlagSetter` |
| Mission steps | indices 18–21 of 22 | `AuthorDialogueStep`/`AuthorReachStep`/`AuthorTriggerStep` |

### A.6 Scene root hierarchy (current)

`BuildChapter8SilentGarden()` creates these as **siblings**, not nested: `Directional Light`, `SilentGarden` (ground, fog gate, barrow, headstone field, answer pads, the Mourners ring), the four accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig, the two riddle-trial answer points, `GraveRiddleTrial`, the 3 Buried enemies, `The Warden`, `VisionDive` (with its two rooms and the ghost figures), `VisionExitPoint`, `VisionDiveController`, `EnterVisionTrigger`, `ExitVisionTrigger`, three reach points, fourteen dialogue-player roots, `BuriedWaveSpawner` (+ its trigger child), the complete canvas, `ChapterOutro`, `XR Interaction Manager`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots (0–5), and moves `SilentGarden`'s contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Four resolve — `Named.Echo`, `Named.Khall`, `Named.TheWarden`, `Named.TheMourners` (all shared Named-character prefabs, two of them procedural placeholders); everything environment-specific is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Named.Khall` | `Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `Named.TheWarden` (target: `Enemies.TheWarden`) | `Art/Generated/Characters3D/Named/The-Warden.prefab` | **EXISTS** *(procedural placeholder — grave-iron green, not commissioned art; commission brief needs the composite/partial-humanoid spec, not just the tint — see Beat 3c Notes)* |
| `Named.TheMourners` | `Art/Generated/Characters3D/Named/The-Mourners.prefab` | **EXISTS** *(procedural placeholder — bone white, not commissioned art; commission brief needs a fog-grey tint shift and an explicit faceless rule — see Beat 4c Notes)* |
| `Enemies.Buried` | `Art/Generated/Characters3D/Enemies/Buried.prefab` | MISSING *(no mesh assigned at all today — generic `Enemy` build path with no Named/placeholder reference)* |
| `Rooms.SilentGardenGround` | `Art/Generated/Rooms/SilentGardenGround.prefab` | MISSING |
| `Vfx.FogGateWall` | `Art/Generated/VFX/FogGateWall.prefab` | MISSING |
| `Vfx.GroundFog` | `Art/Generated/VFX/GroundFog.prefab` | MISSING *(new key this pass — no code references it yet; see §3's fog note and Beat 1c)* |
| `Props.HeadstoneGeneric` | `Art/Generated/Props/HeadstoneGeneric.prefab` | MISSING |
| `Props.RiddlePadStone` | `Art/Generated/Props/RiddlePadStone.prefab` | MISSING |
| `Props.BarrowMound` | `Art/Generated/Props/BarrowMound.prefab` | MISSING |
| `Rooms.VisionA_SterileRoom` | `Art/Generated/Rooms/VisionA_SterileRoom.prefab` | MISSING |
| `Props.SterileTable` | `Art/Generated/Props/SterileTable.prefab` | MISSING |
| `Rooms.VisionB_HandlerBay` | `Art/Generated/Rooms/VisionB_HandlerBay.prefab` | MISSING |
| `Props.HandlerConsole` | `Art/Generated/Props/HandlerConsole.prefab` | MISSING |
| `Props.KillOrder` | `Art/Generated/Props/KillOrder.prefab` | MISSING *(new key this pass — no code references it yet; see Beat 4c Notes)* |
| `Vfx.GhostFigure` | *(procedural — `Ch8BuildGhostFigure`, capsule+sphere, no prefab planned)* | greybox by design, not a commission |

**Cross-chapter note:** `Named.Echo` and `Named.Khall` are the same assets Chapter 1's registry (Appendix B of `Ch01-Scene-Construction.md`) already lists as **EXISTS** — this document does not duplicate their commission status, only their reuse here. `The-Warden.prefab` and `The-Mourners.prefab` are new to Chapter 8 but resolve via the same `PlaceholderCharacterBuilder` mechanism that already backs several other chapters' bosses/NPCs — they are real, buildable assets today, just not final art.

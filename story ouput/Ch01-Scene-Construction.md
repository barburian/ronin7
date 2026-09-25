# Chapter 1 — Scene Construction

*The architectural contract for `Galaxy1_Ch1_Hub.unity`: what Chapter 1 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 1 ("The Salvager's Debt") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter1Builder.cs`, entry point `XRRigBuilder.BuildChapter1Hub()`, invoked from the Unity menu **Tools → Space Samurai → Galaxy 1 → Build Chapter 1 (Fresh)**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch01_The_Salvagers_Debt.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** Combat and impact feedback in this scene come from `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never from moving the camera. This applies to the Beat 3 boarding fight and any future impact tuning in this chapter.
- **Traversal in Ch1 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no parkour/climb/wall-run** anywhere in this chapter — the only NPC movement mechanism is the inactive-`NpcWalker`-plus-`MissionDirector`-`Trigger` idiom described in §5. Do not introduce any of the excluded mechanics when patching this scene.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | room shells, props, doors *(the physical object)*, VFX, backdrops, decorative lights | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | door lock state, NPC spawns + walkers, enemy spawns, reach points, dialogue players, prompts, mission-spine steps | `[BEAT_N_LOGIC]` |

**The one object that spans both is a door.** `BuildBeatNArt()` instantiates the door and returns its handle; `BuildBeatNLogic()` sets `startLocked` and wires the `Trigger` step that unlocks it. Art builds the thing; logic decides what it does.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildSlidingDoor`, `BuildProp`, `BuildAccentPointLight`, `Author*Step` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`.
- **Free to restructure:** the Ch1-local helpers, called only from `BuildChapter1Hub()` — `BuildMedbayProps`, `BuildRevivalBayStory`, `BuildHoldViewport`, `BuildHoldStory`, `BuildCairnAtmosphere`, `BuildConduitSpark`, `BuildDeadDeckHatch`, `BuildChapter1Dialogue`, `BuildReleasePrompt`, `BuildNpcWalker`, `FitNamedCharacter`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 1 and are now silently rebuilding thirteen other chapters.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two new ScriptableObjects carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch1Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, floor + ceiling tint, per-room accent lights, event lights |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document |

Both are net-new (`Assets/Ronin7/Data/` is where `EnemyDefinition`, `WeaponDefinition`, and `ZoneDefinition` instances already live). Neither exists yet.

Prefab **paths never appear in builder code.** The builder asks the registry for `Props.ExamTable`; the registry asset holds the path. This is the whole point of the indirection — art can re-point a prefab without touching a `.cs` file or this document.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live. New environment folders are siblings of `Characters3D/`:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today
  Rooms/                                    ← new
  Props/                                    ← new
  Doors/                                    ← new
  VFX/                                      ← new
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter1Hub()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter1Builder.cs:47`). That call does not *delete objects from* the scene — it **discards the entire scene** and opens a fresh empty one. There is nothing left to search for. A naïve `GameObject.Find("[STATIC_ART_DO_NOT_DELETE]")` after `NewScene` will always return `null`, and the instruction will silently do nothing.
>
> Making the safe zone real requires **replacing the wipe strategy**, one of:
>
> 1. `EditorSceneManager.OpenScene(Ch1HubScenePath)`, then `DestroyImmediate` each **generated root by name** (`ShipInterior`, `Game`, `Mission`, the rig, the accent lights, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred.** `EnemyArtWirer.cs` and `CrowdArtWirer.cs` already open shipped scenes in place, mutate them idempotently, and `SaveScene` — reuse that pattern rather than inventing a third.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist.** No exam table, IV rack, workbench, casket, sliding door, room shell, or dead-deck hatch. See Appendix B for the full inventory: five keys resolve; everything else is a commission.

A builder that instantiates from an empty registry produces **an empty room** — the first run of the refactored builder would destroy Chapter 1.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_ExamTable);
if (prefab == null) {
    Debug.LogWarning($"[Ch1] {ArtKey.Props_ExamTable} unresolved — primitive fallback.");
    BuildExamTablePrimitive(staticArtRoot);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** (`Project/Docs/GraphicsRoadmap-GrittyCyber.md`) — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **Recorded greybox baseline** (Ch1 hub, edit-mode `UnityStats`, 2026-07-02, `Project/Docs/CHAPTER-BUILD-LEDGER.md`): **drawCalls 189 · setPassCalls 17 · tris 9,198 · verts 13,092.**

Replacing ~37 tinted primitives with high-fidelity prefabs is *exactly* the change that breaks this bar. Every prefab landing in the registry must be re-measured against it. A prefab that looks correct and drops the scene below 72 Hz is a regression, not an upgrade — the primitives it replaced were cheap for a reason (shared `TintShared` MaterialPropertyBlock batching, see §3).

## 2. Chapter spatial map

Chapter 1 is **one continuous scene**, `Assets/Ronin7/Scenes/Galaxy1_Ch1_Hub.unity`, laid out as **four rooms strung along a single linear +Z corridor** — there is no branching, no vertical stacking, and no returning to an earlier room by any route other than walking back down the same corridor. The player wakes at z≈0 and the story pushes them monotonically toward z≈34.

```
 -Z                                                                      +Z
 Revival/Medbay ──[MedbayDoor z=4, locked]── Main Hold ──[AirlockInnerDoor z=16, unlocked]── Airlock ──[CommandDoor z=26, locked]── Corridor → Command Room
   x[-4,4] z[-4,4]                          x[-6,6] z[4,16]                                x[-2,2] z[16,26]                          x[-9,9] z[26,42]
   center (0,0,0), 8x8                      center (0,0,10), 12x12                          center (0,0,21), 4x10                    center (0,0,34), 18x16
```

| Beat | Room | Footprint | Floor center / size |
|---|---|---|---|
| 1 | Revival / Medical Bay | x[-4,4], z[-4,4] | center (0,0,0), 8×8 |
| 2 | Main Hold (wreck-field viewport, west wall) | x[-6,6], z[4,16] | center (0,0,10), 12×12 |
| 3 | Airlock corridor (boarding) + the Medbay kill-box | x[-2,2], z[16,26] | center (0,0,21), 4×10 |
| 4 | Corridor → Command Room | x[-9,9], z[26,42] | center (0,0,34), 18×16 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m** for every room in the chapter.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience. Door apertures are 2.4 m wide.

**Doors** — three, all the same registry key, differing only in lock state (logic, not art):

| Door | Position | Registry Key | startLocked | Unlocked by |
|---|---|---|---|---|
| `MedbayDoor` | (0, 0, 4) | `Doors.SlidingDoor_Standard` | **true** | Mission step 3 Trigger (opens door + starts Kessler leg-1 walk) |
| `AirlockInnerDoor` | (0, 0, 16) | `Doors.SlidingDoor_Standard` | **false** | — open from the start |
| `CommandDoor` | (0, 0, 26) | `Doors.SlidingDoor_Standard` | **true** | Mission step 11 Trigger (unlocks + starts Kessler leg-2 walk) |

Each is wired via `WireDoorAudio(door, DoorSlide.wav)`.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring). `ZoneBounds` is set to **center (0, 0, 19), radius 45** — a single bounding sphere loosely enclosing all four rooms along the corridor's midpoint.

## 3. Global environment & backdrop

**The wreck-field (seen through the Main Hold's west viewport):** dead hulls turning slow in the dark, lit by a single thin, dirty star — the visual and narrative "graveyard" the ship sits in. Per the dialogue script's SETTING block, the rig survives specifically *because* it reads as one more dead hull in a field of them; the first break in that stillness is **"a running light that isn't a wreck"** — steady, purposeful, getting closer — which is the player's first sight of the Dominion boarding party, revealed by the Beat 2 Trigger step (§4).

**The Cairn — lived-in-core aesthetic:** per `17_THE_HUB_Kesslers_Ship.md` §4, in Chapter 1 the ship reads as a battered salvage barge because *most of the hull is dark, unpowered, and scanning as scrap* — Kessler only ever lit the few compartments he needed (revival bay, medical bay, a jury-rigged command room, one hangar throat/airlock). "The grandeur is latent" — this chapter's job is only to establish a warm, lived-in island inside a cold, dead leviathan, never to reveal the ship's true (capital-class) scale. The atmosphere pass executes this directly: a looping 2D `OnFootAmbience.wav` bed at volume 0.35 on the `Game` root; two sealed, unpowered `DeadDeckHatch` frames behind the revival bay and off the command room, implying hull the player never enters this chapter; a weathered **"THE CAIRN"** hull stencil on the airlock's west wall; and two `ConduitSpark` props reading as "power is thin: flickers, shorted lines."

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter1Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch1Environment.asset`. Its schema:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` | `Color` | every `BuildFloorCeiling` call |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` per room |
| `eventLights[]` | `{ name, position, color, intensity, range, startsInactive }` | `DockingAlarmLight` (Beat 3) |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddAmbientPulse("MedbayLight", 7f)` and `AddConsoleFlicker("CommandLight", 11f)` calls with data.

The four per-room accent entries and the alarm event light are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass. Retuning the look afterward is a profile edit with no code change and no rebuild.

**Material / tint palette:** set-dressing props today are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching, reused from `RendererTint` conventions) rather than unique materials — this keeps the chapter's draw-call count low (§1.6) and keeps regeneration cheap if colors need retuning. **Prefabs replacing them must carry their own materials and will not batch this way.** That is the perf cost the budget clause exists to police.

## 4. Per-beat scene spec

The chapter plays as four beats along the linear +Z run. Each beat is documented with the same a–f structure.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the Y-invariant.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row.

---

### Beat 1 — Revival / Medical Bay (The Wake)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes, cylinders, or hardcoded sizes for any props. You must read from the centralized `ArtAssetRegistry` ScriptableObject for all environment prefabs. Create separate methods: **`BuildBeat1Art()`** for static environment/prefabs, and **`BuildBeat1Logic()`** for doors, AI, triggers, and dialogue.

#### a. Narrative purpose & emotional target

This is Ronin-7's birth scene as a *person* rather than a weapon. Three weeks of failed wakes (per Kessler: "Four, five times... you'd come up off the table thrashing, eyes wide open and nobody behind them... and then you'd just go back under") have led to this one morning where "somebody's home behind the eyes." The beat has to sell two things simultaneously: reflexive lethality (he has Kessler by the throat before he's oriented) and the first crack of a conscience (he releases on the player's own input, not a cutscene). The emotional target is *disorientation resolving into trust* — a man with no name, no memory, and a stranger's mercy in his own hands, choosing not to use them. Kessler is the counterweight: calm, rehearsed, a man who has done this vigil enough times to know exactly how to talk down a weapon he cannot overpower.

Per the dialogue script's SETTING block, Beat 1 is "welded into the guts of the rig; cramped, improvised, not a real med-bay" — a salvage cutter's workshop repurposed to keep one body alive, not a clinic. Nothing here is clean or new.

This room is also a promise the player doesn't know they're being given yet: it comes back as the Beat‑3 kill‑box. Don't undercut that here — the fight belongs entirely to the Beat 3 subsection; this section only plants the geometry (no cover, one hatch in/out, cramped bulkheads) that later pays off.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to a `[BEAT_1_LOGIC]` root object.

- **Player rig:** spawns lying on the exam table at approximately (0, table-top, -0.2). `BuildRig(addLocomotion: true)` + `EchoPresence` + `ZoneBounds` center (0,0,19) radius 45 (authored once, chapter-wide). Locomotion is continuous walk + snap-turn. The player does not travel in Beat 1; the whole beat plays out standing at/near the table, ending with a look at the workbench and the katana.
- **Kessler spawn:** (-1.6, 0, -0.2), facing the table — "beside the exam table, where he kept vigil." `StoryNpc` (displayName "Kessler", `remote=false`), grounded to `kesslerFloorY` via `FitNamedCharacter`. Base idle is `StoryNpcWander` (radius **0.7 m**) so he never clips the table or the cramped bay walls.
- **`MedbayDoor` lock state:** `startLocked: true`. The player cannot leave until step 3 fires. *(The door object itself is instantiated in `BuildBeat1Art()`; logic only sets its state and wires the trigger.)*
- **`ReleasePrompt`:** worldspace TextMesh "Release (Y)" at (0, 1.4, 1), driven by `PromptInputAdvancer`, created **inactive** and enabled only for step 1.
- **Dialogue anchors:** both at (0, 1, 1).

**Kessler Leg-1 walk** — inactive `NpcWalker` (target = Kessler's transform), activated by step 3:

```
(0, kesslerFloorY, 2) → (0, kesslerFloorY, 6) → (-2, kesslerFloorY, 9.5)   // by the wreck-field viewport
```

> **CRITICAL Y-INVARIANT:** every waypoint Y **must** equal `kesslerFloorY` (his grounded root Y after `FitNamedCharacter`), because `NpcWalker` drags the full waypoint position including Y — a mismatch sinks or floats him. This walk is initiated by this beat's Trigger step but physically carries him out of the room; arrival/blocking at the viewport belongs to Beat 2.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat1_Wake` — the grapple-struggle VO plays while control is with the player |
| 1 | Prompt | Release Grapple — `ReleasePrompt` activates; player's Left-Hand "Talk" (Y) input on `PromptInputAdvancer` ends the struggle and advances the director |
| 2 | Dialogue | `Dialogue_Beat1_Settle` — Kessler's follow-up lines once the grip is released |
| 3 | Trigger | Opens `MedbayDoor` (unlocks the z=4 gap) **and** activates `KesslerToHold`, sending Kessler on his leg-1 walk into the Main Hold — this is the beat's exit, handing off to Beat 2 |

**What changes during the beat:** nothing in the set dressing itself — no props are added or removed. The state changes are: (1) `MedbayDoor` flips from locked to open at step 3; (2) Kessler's `StoryNpcWander` idle is superseded the instant his `KesslerToHold` walker activates; (3) `ReleasePrompt` goes inactive → active mid-struggle → inactive once triggered. The room's next visual change — the same geometry read as a kill-box — is Beat 3's job.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

All environment prefabs instantiate from the `ArtAssetRegistry` and parent to `[STATIC_ART_DO_NOT_DELETE]`. Do not use `scale()` or `size()`; rely on the prefab's native scale.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (8×8, walls + floor + ceiling) | center (0,0,0) | `Rooms.MedbayShell` | `…/Art/Generated/Rooms/MedbayShell.prefab` | **MISSING** |
| `MedbayDoor` | (0, 0, 4) | `Doors.SlidingDoor_Standard` | `…/Art/Generated/Doors/SlidingDoor_Standard.prefab` | **MISSING** |
| Exam Table | (0, 0.5, -0.2) | `Props.ExamTable` | `…/Art/Generated/Props/ExamTable.prefab` | **MISSING** |
| Dominion Casket (open) | (-1.4, 0, 1.0) | `Props.DominionCasket_Open` | `…/Art/Generated/Props/DominionCasket_Open.prefab` | **MISSING** |
| IV Rack | (1.1, 0, -0.7) | `Props.Medical_IVRack` | `…/Art/Generated/Props/Medical_IVRack.prefab` | **MISSING** |
| Wall Monitor (east, fore) | (3.85, 1.7, -1) | `Props.WallMonitor` | `…/Art/Generated/Props/WallMonitor.prefab` | **MISSING** |
| Wall Monitor (east, aft) | (3.85, 1.7, 0.6) | `Props.WallMonitor` | `…/Art/Generated/Props/WallMonitor.prefab` | **MISSING** |
| `RevivalMonitor` (west wall) | (-3.85, 1.6, 0.8) | `Props.RevivalMonitor` | `…/Art/Generated/Props/RevivalMonitor.prefab` | **MISSING** |
| Workbench | (2.6, 0.45, -2.6) | `Props.Workbench_Dirty` | `…/Art/Generated/Props/Workbench_Dirty.prefab` | **MISSING** |
| `CuttingTorch` | (2.1, 0.98, -2.5) | `Props.CuttingTorch` | `…/Art/Generated/Props/CuttingTorch.prefab` | **MISSING** |
| `PryBar` | (2.9, 0.96, -2.55) | `Props.PryBar` | `…/Art/Generated/Props/PryBar.prefab` | **MISSING** |
| `TableCable` | (0.5, 0.45, 0.4) | `Props.TableCable` | `…/Art/Generated/Props/TableCable.prefab` | **MISSING** |
| Katana "Echo" | (2.15, 0.93, -2.6), Euler(0,90,0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `DeadDeckHatch` | (0, 1.3, -3.88) | `Props.DeadDeckHatch` | `…/Art/Generated/Props/DeadDeckHatch.prefab` | **MISSING** |
| `ConduitSpark` | (3.6, 2.4, -3.7) | `Vfx.ConduitSpark` | `Assets/Ronin7/Resources/Vfx/SwordSpark.prefab` | **EXISTS** *(borrowed)* |
| `MedbayLight` | (0, 2.6, 0) | — | read from `ChapterEnvironmentProfile.accentLights["Medbay"]` | profile |
| `MedbayAmbience` | (3, 1, -3.3) | — | `AudioSource`, `medbay_hum.wav` (no prefab) | audio |

**Notes on the transition.** The casket's four sub-objects (`Casket_Shell` / `_Interior` / `_Lid` / `_Seal`) collapse into a single `DominionCasket_Open` prefab — the lid hinged open and tilted back at 70°, the brass Dominion seal (0.7, 0.5, 0.16) baked into the asset. Likewise `ExamTableBase` folds into `ExamTable`, and the IV rack's pole/foot/fluid-bag folds into `Medical_IVRack`. The katana **rests flat** on the workbench at bench height so it reads as *laid down*, not displayed at eye level ("that came out of the box with you… I cleaned the blood off it"). This is the katana's introduction; its shadow-AI nature is not revealed yet (§9).

#### d. Combat

None in this beat proper. The wake-fight is a **non-lethal grapple tutorial**, not combat with damage/health — see e./f. below for how it plays. This same room becomes the Beat‑3 kill‑box once the Dominion boards; that fight (enemy spawns, activation, kill-box geometry) is documented entirely in the Beat‑3 section — do not duplicate it here.

#### e. Dialogue / VO

Two dialogue sets, both anchored at (0, 1, 1), advanced by **Left-Hand "Talk" (Y)** via the shared `talkRef` input action, clips resolved from `Assets/Ronin7/Art/Generated/Audio/Voice` through `Chapter1Lines.ClipName`:

- **`ch1_beat1_wake`** (`Dialogue_Beat1_Wake`) — plays *during* the playable grapple struggle. Per the dialogue script this covers Kessler's "There it is." / "Come on. Stay this time." through the pinned exchange:
  - Ronin-7: *"Where am I."* → Kessler: *"You're on my ship."* → Ronin-7: *"That's not an answer."* → Kessler: *"It's the only one I've got that's true."* → Kessler: *"You can crush my windpipe... But you already did the hard part."* → Ronin-7: *"What part."* → Kessler: *"You didn't kill me. Last time we met, you haven't done that either."* → Ronin-7: *"I don't know you."* → Kessler: *"I know. They took that part out. They're good at it."*
  - This is where the player's own release input (step 1) actually ends the struggle — the line *"My hands knew what to do. I didn't tell them"* comes only after release, so it likely spans the wake/settle boundary; treat the exact clip split as builder-authored via `Chapter1Lines` (inferred: verify the wake/settle line boundary against the live `MissionDirector` dialogue-set contents rather than assuming the split above is exact).
- **`ch1_beat1_settle`** (`Dialogue_Beat1_Settle`) — the calmer aftermath once Ronin-7 is up and orienting: finding the throat scar (*"This."* / *"That was there when I found you..."*), discovering the katana on the workbench (*"That came out of the box with you. I cleaned the blood off it..."* → Ronin-7 picks it up and sets it back within reach: *"I don't remember this. But my hands do."*), naming Kessler (*"What do I call you." / "Kessler. And before you ask, no..."*), and the walk-it-off close (*"Come on. Walk it off before you fall down and I have to start over."*).

The **wake-fight tutorial** itself is scripted as: control passes to the player as Ronin-7 lunges and pins Kessler against the bulkhead; Kessler's lines play *over* the struggle teaching grip/break/release inputs, non-lethal throughout; the release at the end is the **player's own input**, not a scripted beat — matching mission step 1's Prompt.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the pin/struggle and the shorted-conduit sparks read entirely through `Haptics` (grip pressure/release feedback on the controller), `AudioDirector` stingers (the IV rack clatter, the shorted-line spark crackle), and `CombatFeedbackController` reticle/UI cues where applicable. Never shake, per the non-negotiable VR constraint.
- **Ambient bed:** `MedbayAmbience` loops `medbay_hum.wav` continuously at (3, 1, -3.3) — coolant hiss and the slow tick of machinery run too long.
- **Lighting pulse:** the `Medbay` accent entry carries `behaviour: AmbientPulse(period: 7s)` — a slow breathing warmth against the cold ambient/fog baseline, reading as the one nursed, cared-for corner of the ship. **Authored in the profile asset, not in code.**
- **Comfort:** the player is stationary throughout (lying down → standing at the table), so the snap-turn/comfort-vignette locomotion guard is largely inert here; it becomes relevant only once the player starts walking after step 3 opens the door into Beat 2.

**Cross-reference:** the exact same room geometry (`ExamTable`, `IVRack`, monitors, the single door at z=4) is reused unmodified as the Beat‑3 kill‑box once the boarding party pushes in. This section only establishes the space; it does not describe the fight.

---

### Beat 2 — Main Hold (Three Weeks Adrift)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (viewport frame, backdrop, wrecks, salvage, workbench) and **`BuildBeat2Logic()`** (reach point, dialogue, the running-light trigger). The `RunningLight` mote is *art*; its inactive state and step-6 activation are *logic*.

#### a. Narrative purpose & emotional target

The wake (Beat 1) established *who* Ronin-7 physically is; Beat 2 is the room where he learns *what he did*. It is a long, still, two-hander scene — the biggest set piece in Chapter 1 has no combat in it at all. The emotional arc runs backward-to-forward: Kessler starts with a mundane offer of hospitality ("You hungry? Thirsty?"), walks the casket discovery back to its origin, and lands on the reveal that six years ago this exact man knelt him at gunpoint during a "clean sweep" and **went mercy** instead of firing. Ronin-7 spends the whole scene "examining a wound to see how deep, not whether it hurts" — flat, procedural, working the logic of his own erased kindness. The scene's dread is a slow-burn: the audience/player watches the two men talk themselves into a hypothesis ("if they sealed me and threw me away, they think the job's done... they'll come to finish it") a beat before the world confirms it. The close is a single visual sting through the viewport — a running light that isn't a wreck — landing exactly on Kessler's dread line: **"...Sooner than I'd like."**

The Main Hold is the largest interior space in the ship (12×12, the widest footprint of the four rooms) and reads as a working salvage floor, not living quarters: "salvage everywhere, sorted and half-stripped. Copper, stripped hull plate, other men's garbage in graded piles" (dialogue-script SETTING block).

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Kessler's leg-1 arrival** (activated by step 3's Trigger, *before* this beat's dialogue plays): starting beside the exam table in the Medbay, he is dragged through `(0, y, 2) → (0, y, 6) → (-2, y, 9.5)`, ending by the west viewport — matching the SETTING block's "Ronin-7 stays standing near the viewport... Kessler fills two dented cups" staging. On activation his `StoryNpcWander` idle is disabled and `NpcWalkAnimator` drives the walk cycle once through the array.
  - **Y invariant:** every waypoint Y = `kesslerFloorY`, never a hardcoded height — mismatch buries or floats him.
  - *(inferred)* The screenplay's stage direction has Kessler later "sit heavy on the edge of the bench, turning the cup" — the built walker leg only carries him to (-2, y, 9.5) by the viewport, **not** to the workbench at (3.6, 0.45, 7.5); treat the bench line as unstaged narrative color the dialogue audio carries on its own, not a second walk leg.
- **Player rig:** enters the Hold on foot through the Medbay doorway (z=4) after step 3 unlocks `MedbayDoor`, and must cross into `HoldReachPoint` (0, 1, 10) radius **4.5** to satisfy step 4 before the dialogue can start. Once inside, the player is free to walk anywhere in the 12×12 floor for the dialogue's duration — there is no second position gate; the "stays standing near the viewport, katana at the waist" staging is narrative guidance for where a player is expected to gravitate, not an enforced trigger.
- **`RunningLight` state:** built **inactive**. Its `PlanetOrbit` component orbits `RunningLightHub` (-9, 1.8, 10) at radius **2.2**, angular speed **18°/s**, yOffset 0, startAngle 0. Step 6 flips it active. This is the Dominion patrol's first visible sign, "steady, purposeful, wrong against the lifeless drift."
- **`AirlockInnerDoor` lock state:** `startLocked: false` — open from the start.

**Mission-spine steps:**

| # | Step | Detail |
|---|---|---|
| 4 | `ReachTrigger: Main Hold` | Gates on `Camera.main` distance to `HoldReachPoint` (0,1,10), radius 4.5 |
| 5 | `Dialogue: Beat2 Adrift` | Plays dialogue set `ch1_beat2_adrift` in full, advanced line-by-line |
| 6 | `Trigger: Wreck-field Running Light` | Activates the inactive `RunningLight` GameObject — the beat's dread sting, and its close |

*(Steps 0–3 belong to Beat 1; step 7 "Trigger: Boarding Alarm" belongs to Beat 3.)*

**What changes during the beat:** for the entire dialogue the viewport is inert — three dead wrecks and a dim star. Only the running light changes, and only at step 6, immediately following the dialogue's closing line.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (12×12) | center (0,0,10) | `Rooms.MainHoldShell` | `…/Art/Generated/Rooms/MainHoldShell.prefab` | **MISSING** |
| Viewport frame (sill, header, two pillars) | west wall, opening z[7,13] y[0.8,2.8] | `Rooms.HoldViewportFrame` | `…/Art/Generated/Rooms/HoldViewportFrame.prefab` | **MISSING** |
| `AirlockInnerDoor` | (0, 0, 16) | `Doors.SlidingDoor_Standard` | `…/Art/Generated/Doors/SlidingDoor_Standard.prefab` | **MISSING** |
| `WreckFieldBackdrop` | (-8, 1.8, 10), Euler(0,-90,0) | `Props.WreckFieldBackdrop` | `…/Art/Generated/Props/WreckFieldBackdrop.prefab` | **MISSING** |
| `Wreck0` | (-7.2, 1.4, 8.4) | `Props.WreckSilhouette` | `…/Art/Generated/Props/WreckSilhouette.prefab` | **MISSING** |
| `Wreck1` | (-7.4, 2.3, 11.6) | `Props.WreckSilhouette` | `…/Art/Generated/Props/WreckSilhouette.prefab` | **MISSING** |
| `Wreck2` | (-7.0, 1.0, 12.4) | `Props.WreckSilhouette` | `…/Art/Generated/Props/WreckSilhouette.prefab` | **MISSING** |
| `DirtyStar` | (-9.5, 2.6, 6.5) | `Props.DirtyStar` | `…/Art/Generated/Props/DirtyStar.prefab` | **MISSING** |
| `RunningLightHub` | (-9, 1.8, 10) | — | empty transform (orbit anchor) | logic |
| `RunningLight` mote | (-7, 1.8, 10) | `Vfx.RunningLightMote` | `…/Art/Generated/VFX/RunningLightMote.prefab` | **MISSING** |
| `HoldWorkbench` | (3.6, 0.45, 7.5) | `Props.Workbench_Dirty` | `…/Art/Generated/Props/Workbench_Dirty.prefab` | **MISSING** |
| `Cup_A` | (3.4, 0.98, 7.4) | `Props.DentedCup` | `…/Art/Generated/Props/DentedCup.prefab` | **MISSING** |
| `Cup_B` | (3.7, 0.98, 7.6) | `Props.DentedCup` | `…/Art/Generated/Props/DentedCup.prefab` | **MISSING** |
| `Salvage_HullPlate0` | (4.6, 0.15, 12.5) | `Props.Salvage_HullPlate` | `…/Art/Generated/Props/Salvage_HullPlate.prefab` | **MISSING** |
| `Salvage_HullPlate1` | (4.6, 0.28, 12.4) | `Props.Salvage_HullPlate` | `…/Art/Generated/Props/Salvage_HullPlate.prefab` | **MISSING** |
| `Salvage_HullPlate2` | (4.55, 0.4, 12.6) | `Props.Salvage_HullPlate` | `…/Art/Generated/Props/Salvage_HullPlate.prefab` | **MISSING** |
| `Salvage_CopperCoil0` | (3.4, 0.25, 13.4) | `Props.Salvage_CopperCoil` | `…/Art/Generated/Props/Salvage_CopperCoil.prefab` | **MISSING** |
| `Salvage_CopperCoil1` | (3.9, 0.25, 13.5) | `Props.Salvage_CopperCoil` | `…/Art/Generated/Props/Salvage_CopperCoil.prefab` | **MISSING** |
| `HoldLight` | (0, 2.8, 10) | — | read from `ChapterEnvironmentProfile.accentLights["Hold"]` | profile |

**Staging constraints the prefabs must respect.** The `DirtyStar` is the single thin, dirty light source of the whole field per the SETTING block ("no planet, no traffic, no rescue... lit by a single thin, dirty star") — its emissive intensity and warm color come from the profile, not the prefab material. The three wreck silhouettes drift **between** the backdrop and the glass, near-black, "a graveyard of dead hulls turning slow in the dark." Graded salvage piles must keep clear of both doorways and the viewport sightline. The two dented cups are the physical read of "he fills two cups out of habit" — static props, not an animated hand-off.

#### d. Combat

None. Beat 2 is pure dialogue/exploration; the boarding party (3 troopers + Squad Leader) exists inactive in the Airlock corridor but is not touched until Beat 3.

#### e. Dialogue / VO

Dialogue set id: **`ch1_beat2_adrift`**, built at (0, 1, 10). Advance input: Left-Hand **Talk** action (Y button), same `PromptInputAdvancer`/dialogue-player pattern as every other beat. Scene direction per the script: *"Interior: main hold. Salvage everywhere, sorted and half-stripped. A grimy viewport on the wreck-field: dead hulls turning slow in the dark, lit by a thin dirty star. Kessler fills two dented cups out of habit. Ronin-7 stays standing near the viewport, katana at the waist, watching the wrecks."*

Full line set (Speaker | Line | seconds):

| Speaker | Line | sec |
|---|---|---|
| Kessler | You hungry? Thirsty? I don't actually know what you run on yet. | 5 |
| Ronin-7 | Tell me how I got here. | 2.5 |
| Kessler | Three weeks ago I was working this field. Pulling copper out of a freighter that's been dead longer than my daughter's been alive. And my hook snags something it shouldn't. Off on its own. Nothing for a hundred klicks but it. | 16 |
| Ronin-7 | A casket. | 1 |
| Kessler | A casket. Sealed. Dominion locks, fresh ones, not salvage-old. A sealed pod drifting clean out here means somebody paid to be sure it never opened. | 10 |
| Ronin-7 | And there was no wreck. | 2 |
| Kessler | No wreck. Nobody jettisons a sealed military pod into open black by accident. Somebody put you in a box and threw the box away. Took the trouble to lock it first. | 12 |
| Ronin-7 | You should have left it shut. | 2.5 |
| Kessler | That's what every smart year of my life told me. Cheap skin over augments somebody spent a fortune on. A throat cut and closed. A man flatlined so long the table kept telling me to stop. My instinct said burn it and don't ask. | 16.5 |
| Ronin-7 | But you opened it. | 1.5 |
| Kessler | I owed it open. | 2 |
| Ronin-7 | Owed who. | 1 |
| Kessler | You. Though you'll tell me you don't remember, and I'll believe you, because you've been telling me nothing for three weeks straight. | 8.5 |
| Kessler | You've woken before. Did you know that? Four, five times... Once you put me into that wall hard enough I saw the next morning sideways. And then you'd just go back under. Like a tide. No name. Nothing a man could use. | 22 |
| Ronin-7 | And this morning? | 1.5 |
| Kessler | This morning somebody's home behind the eyes. First time. Three weeks I've been talking to a body, hoping there was still a man in it. | 10 |
| Ronin-7 | You said you owed me. These hands cut throats. I felt it tonight, on yours. Why would anyone owe that a debt? | 8.5 |
| Kessler | Six years ago you did. | 2 |
| Kessler | Six years ago I was on the wrong end of a clean sweep. Nobody walks out... So they sent a man to close the door. | 19 |
| Ronin-7 | Me. | 0.5 |
| Kessler | You. You had me on my knees. Hand on the back of my head. I felt the muzzle. And then, nothing. No shot... you were just standing there. Looking at your own hand. | 20 |
| Kessler | Then you left. Reported the sweep clean... They say one of you went mercy. Like it's a fault in the metal. | 28 |
| Ronin-7 | I don't remember sparing you. | 2 |
| Kessler | I know. | 1 |
| Ronin-7 | I don't remember being a man who would. | 3 |
| Kessler | Neither do they... I'm betting a thing that broke once can break again. | 14 |
| Ronin-7 | If they sealed me and threw me away, then they think the job's done. | 5.5 |
| Kessler | That'd be my read. | 1.5 |
| Ronin-7 | Then when they find out it isn't, they'll come to finish it. | 4.5 |
| Kessler | ...Sooner than I'd like. | 2 |

Total runtime ≈ 222.5s (inferred, summed from the listed durations). The last line ("...Sooner than I'd like") is timed to land as the running light becomes visible through the viewport — step 6's Trigger should fire right at/after this line, not before.

#### f. Audio / Haptics / VR Comfort

- No camera shake, ever — this is a dialogue beat with zero combat feel to sell, so the only "juice" is environmental: the ambient bed (coolant hiss / machine-tick loop, `OnFootAmbience.wav`) keeps running under the conversation, unbroken.
- The `Hold` accent entry carries `behaviour: None` — no flicker, no pulse. The Hold's lighting stays level and neutral through the whole scene so the one visual event (the running light) reads clearly against a static baseline. **This is a deliberate authored value in the profile, not an omission.**
- Comfort vignette behaves per the standard `ContinuousLocomotion` posture guard; since the player is free to walk during the dialogue, snap-turn and vignette engage normally on rotation/movement input, same as any other room — no beat-specific override.
- Haptics: none scripted for this beat (no combat, no door-slam grapple); the only tactile beats in Chapter 1 fire in Beats 1 and 3.

---

### Beat 3 — Airlock Corridor + Medbay Kill-Box (The Boarding)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (airlock shell, outer hatch, boarding clamp, hull stencil) and **`BuildBeat3Logic()`** (alarm light state, four inactive troopers, `DefeatEnemies`, dialogue).
> **The Medbay kill-box builds nothing.** It is Beat 1's room, reused. `BuildBeat3Art()` must not re-instantiate a single Medbay prop — if it does, you have double-built the room.

#### a. Narrative purpose & emotional target

The rig's one piece of safety — Kessler's stalling, the sealed hatches, three weeks of quiet — gets physically cracked open. This is the chapter's **first true combat encounter**, and the design intent (per `Ch01_The_Salvagers_Debt.md`) is explicit: *"the first true combat encounter, fought in the cramped medical bay and corridors (teaches the katana in real stakes)."* Tone target is **economical, without anger — a body executing what the mind never learned.** Ronin-7 isn't angry or heroic here; he is a weapon remembering its function while the man attached to it watches it happen.

The emotional turn lands *after* the blades stop: the fight was never really about a routine "sweep." A trooper's visor-scan snags on Ronin-7's face, escalates past the squad to an unnamed colder voice up the chain (the Handler, later named Khall — kept unnamed on screen per the Ladder-D continuity note), and the order comes back flat: *"Confirmed. Terminate. Recover the remains intact."* The beat's real reveal isn't the fight, it's the sentence Kessler says once it's over: *"They mobilized a sweep for a dead man."* Ronin-7 is not hiding from a manhunt — he is a filed execution that failed, and the file just found out. That dread, not the combat, is what Beat 3 is for.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **Player:** arrives already standing near `HoldReachPoint` (0, 1, 10) from Beat 2's dialogue. Travel from there to the airlock corridor is **player-driven continuous locomotion + snap-turn** through the already-unlocked `AirlockInnerDoor` (z=16) — there is no scripted waypoint path for the player.
- **Kessler** does **not** physically walk to the inner hatch for this beat. His Beat-2 walker leg already ended at (-2, `kesslerFloorY`, 9.5) — beside the Hold viewport — and he stays there through the whole boarding fight under his `StoryNpcWander` idle (radius 0.7 m). The cinematic staging in the dialogue script ("Kessler is at the inner hatch, hands up and empty, putting his body between the airlock and the corridor") is conveyed **entirely through the pre-fight VO track**, not a physical relocation. *(Flagged for the builder: if a future pass wants Kessler visibly at the hatch during the pre-fight lines, that needs a new short walker leg — not present today.)*
- **`DockingAlarmLight` state:** built **inactive** at (0, 2.6, 21), with two play-on-enable children (`WaveAlarm.wav`, looping docking-alarm tone; `Landing.wav`, one-shot clamp/forced-seal clank). Its color/intensity/range come from `ChapterEnvironmentProfile.eventLights["DockingAlarm"]`. Step 7 activates it: the corridor goes from cool `AirlockLight` wash to a hard red overlay.
- **`CommandDoor` lock state:** `startLocked: true`, unlocked at step 11.
- **Kessler leg-2 walker:** inactive, activated by step 11. See §5.
- **Y-invariant reminder:** every waypoint's Y must equal `kesslerFloorY` or he sinks/floats when the leg activates.

**Enemy spawns** — all four built **inactive**, flipped active as a group by the `DefeatEnemies` step's setup (not by a separate visible Trigger), gating the fight behind the pre-fight VO finishing:

| Role | Position | Notes |
|---|---|---|
| Trooper 1 | (-1.0, 0, 20) | flanks the hatch mouth (west) |
| Trooper 2 | (1.0, 0, 20) | flanks the hatch mouth (east) |
| Trooper 3 | (0, 0, 22) | mid-corridor, pushing north |
| Squad Leader | (0, 0, 23.5) | "in behind the line" — the comm voice from the pre-fight VO given a body |

Each: root `GameObject` + `Health` + `Enemy` wired to a `weapon` Transform / `bladeTip` / `bodyRenderer`, sharing one `EnemyDefinition`. All four spawn inside the airlock corridor, south of `AirlockInnerDoor` (z=16) — they board through the side hatch at z=21 and push north toward the player arriving from the Hold.

**Mission-spine steps:**

| # | Step | Kind | Fires on |
|---|---|---|---|
| 7 | Trigger: Boarding Alarm (red wash) | Trigger | activates `DockingAlarmLight` (red light + `WaveAlarm.wav` loop + `Landing.wav` clank); advances immediately |
| 8 | Beat3: Boarding (pre-fight VO) | Dialogue | `Dialogue_Beat3_BoardPre` at (0, 1, 13), set `ch1_beat3_board_pre`; player-paced, advances per line on Talk |
| 9 | DefeatEnemies: Boarding Party (3 troopers + Squad Leader) | DefeatEnemies | waits for all 4 trooper `Health` components to reach zero |
| 10 | Beat3: Boarding (aftermath) | Dialogue | `Dialogue_Beat3_BoardPost` at (0, 1, 11), set `ch1_beat3_board_post` |
| 11 | Trigger: Unlock Corridor + Kessler Walks | Trigger | activates `CommandDoor`'s `Controller` (unlocks it) **and** the `KesslerWalker` leg-2 GameObject in the same step; advances immediately |

Both dialogue trigger points (z=13, z=11) sit just south of the Hold viewport, inside the Main Hold rather than physically at the airlock or medbay — they are proximity/zone dialogue points, not literal blocking marks for where the conversation "happens" on screen.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Airlock shell (4×10) | center (0,0,21) | `Rooms.AirlockShell` | `…/Art/Generated/Rooms/AirlockShell.prefab` | **MISSING** |
| `CommandDoor` | (0, 0, 26) | `Doors.SlidingDoor_Standard` | `…/Art/Generated/Doors/SlidingDoor_Standard.prefab` | **MISSING** |
| `OuterDockHatch` (+ brass seal ring) | (1.95, 1.4, 21), east wall | `Props.OuterDockHatch` | `…/Art/Generated/Props/OuterDockHatch.prefab` | **MISSING** |
| `BoardingClamp` | (2.05, 1.4, 21) | `Props.BoardingClamp` | `…/Art/Generated/Props/BoardingClamp.prefab` | **MISSING** |
| `HullStencil_TheCairn` | (-1.88, 1.9, 24), facing +X | `Props.HullStencil_TheCairn` | `…/Art/Generated/Props/HullStencil_TheCairn.prefab` | **MISSING** |
| `ConduitSpark` (airlock) | (-1.7, 2.3, 18) | `Vfx.ConduitSpark` | `Assets/Ronin7/Resources/Vfx/SwordSpark.prefab` | **EXISTS** *(borrowed)* |
| `AirlockLight` | (0, 2.6, 21) | — | `ChapterEnvironmentProfile.accentLights["Airlock"]` | profile |
| `DockingAlarmLight` | (0, 2.6, 21) | — | `ChapterEnvironmentProfile.eventLights["DockingAlarm"]` | profile *(inactive; see logic)* |
| Trooper ×4 | see logic table | `Enemies.DominionTrooper` | `…/Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** |

**The `OuterDockHatch` is a side hatch mid-corridor**, not either end of the room — *"they arrive by clamp and hatch; they do not knock."* The corridor's two ends are the (already-open) `AirlockInnerDoor` at z=16 leading north into the Main Hold, and the (still-locked) `CommandDoor` at z=26 leading south. The `BoardingClamp` seats on the outer hatch; today it is four separate pieces (`Clamp_Ring` / `_ArmTop` / `_ArmBot` / `_ArmL`) that collapse into one prefab.

**Canon note on the stencil:** the ship's proper name is on the hull; in dialogue Kessler only ever calls it "the rig."

> **The Medbay kill-box is Beat 1's room, unmodified.** `BuildBeat3Art()` instantiates **nothing** for it. The exam table, monitors, and workbench (with the katana already lifted off it in Beat 1) are now the fight's only geometry. Critically, the Medbay's front wall (z=-4) is **plain** while the back wall (z=4) carries the only door aperture in the room — there is exactly **one door in this room, period**, matching the treatment doc's "one hatch in and one hatch out" as a *single chokepoint*, not two separate doors. Cornered here, the player has their back to a dead end with a single exit. The "kill-box" read comes entirely from combat flowing backward into a room the player already knows as safe.

#### d. Combat — the first katana fight

Player damage output is via `BladeDamager`'s EMA swing-speed model (**existing system — reuse, don't reinvent**). Player `Health` lives on the rig (`BuildRig`), and is the `playerHealth` reference every trooper's `Enemy` component is wired against.

**Combat mechanic note:** the trooper builder's own comment calls this "a melee Dominion trooper" — despite the dialogue script describing them as rifle-carrying ("weapon shouldered"), the actual fight is blade-to-blade (each trooper rigged with an `ArmR/Sword/Blade/BladeTip` chain, same collision shape family the player's `BladeDamager` reads). Flagging this narrative/mechanic gap for awareness, not proposing a change.

**Kill-box geometry:** the airlock corridor is a tight 4 m-wide hallway (good knife-range/no-retreat lane). Once active, the troopers chase the player (`Enemy` AI) — the fight is expected to flow north through the open `AirlockInnerDoor`, through the Hold, and potentially through the already-open `MedbayDoor` into the single-exit Medbay. That room's single doorway (z=4) and dead walls make it the natural last stand. **This routing is level-design intent, not an enforced trigger** — `DefeatEnemies` simply waits for all four `Health` components to reach zero, wherever that happens.

**Combat trigger:** per the script's explicit direction, prefer a **non-verbal cue** to flip cutscene → playable — an audio sting, the draw of the katana, or a lighting shift (the red `DockingAlarmLight` already primes this moment). A single fallback VO line exists only if the engine needs a spoken trigger: *"You shouldn't have come aboard."* — and if used, it is the **only** line at the hand-off; no other dialogue plays mid-fight.

#### e. Dialogue / VO

Both sets advance on the **Left-Hand "Talk" (Y) input**, one line at a time, via `PromptInputAdvancer`/`DialoguePlayer`. Clips are `.mp3`/`.wav` files in `Assets/Ronin7/Art/Generated/Audio/Voice`, resolved by `Chapter1Lines.ClipName(setId, index, speaker)`.

`Dialogue_Beat3_BoardPre` — set `ch1_beat3_board_pre`, position (0, 1, 13) — 13 lines, ≈55 s total:

| Speaker | Line | sec |
|---|---|---|
| Trooper 1 | "Salvage registry. We're conducting a sweep. Stand clear of the hatch." | 4 |
| Kessler | "Sweep for what? I'm licensed in this field. Manifest's logged, tariffs are clean. You can pull it from here." | 7 |
| Squad Leader (V.O.) | "Manifest doesn't cover what we're looking for. Step back." | 3.5 |
| Kessler | "I've got a dead reactor and a hold full of other men's garbage. There's nothing on this rig worth two troopers and a clamp." | 9 |
| Trooper 1 | "Then it won't take long." | 2 |
| Kessler | "Hey, listen, there's medical gear back there, half of it live, you go poking it..." | 5.5 |
| Trooper 2 | "Stay. There." | 1.5 |
| Trooper 1 | "Command. I've got a face-match flagged here." | 3 |
| Squad Leader (V.O.) | "Match to what." | 1.5 |
| Trooper 1 | "Flag says decedent. Repeat. The file says this man is deceased. Closed. He's standing in front of me." | 7.5 |
| Comm (V.O.) [the Handler, unnamed] | "Hold position. Confirm the face. Stream it. Now, to me. Direct." | 5 |
| Trooper 1 | "...Confirming. Stream is live, sir." | 2.5 |
| Comm (V.O.) [the Handler] | "Confirmed. Terminate. Recover the remains intact." | 3 |

→ combat trigger (non-verbal preferred; fallback single line as above).

`Dialogue_Beat3_BoardPost` — set `ch1_beat3_board_post`, position (0, 1, 11) — 7 lines, ≈35 s total:

| Speaker | Line | sec |
|---|---|---|
| Kessler | "...They mobilized a sweep for a dead man." | 3 |
| Ronin-7 | "Not a sweep. They were looking for me." | 3 |
| Kessler | "The file said deceased. I heard it. They've got you written down closed and buried, and they still sent armed men aboard the second your face turned up. That's not how you treat a corpse. That's how you treat a mistake you thought you'd already cleaned up." | 18 |
| Ronin-7 | "The box didn't hold." | 2 |
| Ronin-7 | "They know now." | 1.5 |
| Kessler | "Yeah. They surely do." | 2 |
| Kessler | "Come on. This conversation's about to get worse, and I'd rather have it sitting down." | 5.5 |

*(The "shuts the visor off with two fingers, the red glyph dies" beat is prose color in the treatment doc — there is no distinct visor-light GameObject on the trooper prefabs to switch off; it's narration carried by the VO line "The box didn't hold," not a scripted visual event. Inferred: no engine object backs this specific image today.)*

#### f. Audio / Haptics / VR Comfort

- Step 7's `DockingAlarmLight` fires both `WaveAlarm.wav` (looping alarm tone) and `Landing.wav` (one-shot clamp/forced-seal clank) as play-on-enable one-shots — the only "camera-independent" impact cue for the boarding itself.
- Combat feel is carried entirely by **Haptics** (controller pulse per hit/parry), **AudioDirector** stingers (blade-clash / kill stingers), and the **CombatFeedbackController** reticle — **no camera shake at any point in this fight**, per the project's non-negotiable VR constraint.
- The `Medbay` accent entry's `AmbientPulse(7s)` keeps running through the fight if it spills back into the Revival Bay — the same slow "machine that revived him" pulse from Beat 1, now lit over a fight instead of a wake.
- Comfort vignette engages on player snap-turns, which will be frequent in a tight, multi-attacker corridor fight — this is the system's normal behavior, not a special case for this beat.
- 90 FPS is the design target for this encounter (four active `Enemy` AIs + blade VFX + the alarm light); the shipped `QualityBootstrap` default is 72 Hz. **This is the beat where a high-fidelity prop swap is most likely to break the frame budget** (§1.6).

---

### Beat 4 — Corridor → Command Room (The Ultimatum)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (command shell, windshield, chair, holotable, drone + rail, cracks) and **`BuildBeat4Logic()`** (Khall's inactive reveal, reach point, dialogue, `ChapterOutro`).
> The `Windshield_Glass` pane is **collider-only, renderer stripped** — the prefab must not occlude the galaxy backdrop behind it.

#### a. Narrative purpose & emotional target

Beat 4 is the chapter's landing gear coming down: no more reflex, no more blade — just two men walking and talking, and then a third voice that isn't in the room. Kessler needs to keep moving ("the walk of a man who needs to keep moving") because standing still means facing what just happened; the corridor walk is a decompression beat between the fight and the reveal. Ronin-7 sheathes the katana and wipes it clean — the body settling back into stillness the way it settled into violence, without asking permission either time.

The Command Room payload has two hits, and both land on Kessler, not Ronin-7: the hologram addresses him by name ("Kessler."), and the ultimatum is built entirely around a debt Ronin-7 didn't know existed. The emotional pivot of the whole chapter is here — Ronin-7 offers a clean, arithmetic trade ("My life for hers... this I can do") and Kessler refuses it flat ("No. I didn't pull you out of a coffin to ship you back to one."). That refusal is the first evidence in the game that mercy might not be a one-way transaction from Ronin-7 outward; someone is choosing to extend it back to him. The beat — and the chapter — ends on Ronin-7's one unprompted want ("I spared a man I don't remember. I want to know why I went easy") and Kessler's plain answer ("Then we ask Velorum"), followed by the drone's flat "Heading laid. Velorum." and Ronin-7's "Take us out." The wreck-field slides away; smash to black.

Target feel: the fight is over but the danger widened instead of closing — from "am I safe" to "is anyone I've just met safe because of me." Cold courtesy from the Handler should read as the most unsettling thing in the chapter precisely because it never raises its voice.

The beat spans two rooms: the back half of the **Airlock corridor** (Beat 3's kill-box, now emptied of the fight, walked through rather than fought through) and the **Command Room** — the chapter's visual and emotional terminus. **Nothing new is built in the airlock for this beat.**

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Kessler leg-2 walk** — inactive `NpcWalker` on the same Kessler GameObject used since Beat 1, activated by the step-11 Trigger that also unlocks `CommandDoor`:

```
KesslerWalkerWaypoints(floorY):
  (0, floorY, 10)   // still in the Main Hold, picking up from wherever leg-1 left him
  (0, floorY, 21)   // through the airlock corridor (mid-kill-box)
  (0, floorY, 28)   // through CommandDoor, into the Command Room
  (2, floorY, 31)   // settles near the captain's chair / holotable
```

  `floorY` = `kesslerFloorY`, the exact grounded root Y `FitNamedCharacter` computed for Kessler back in Beat 1. Every waypoint reuses that same Y — **a hard invariant**: `NpcWalker` drags the *full* waypoint `Vector3` including Y, so any waypoint off that value buries or floats him the instant the leg starts. Since every room in Chapter 1 shares one continuous flat floor (y=0), the same `floorY` is valid across all four rooms without adjustment.

- **Ronin-7 (player):** no scripted path — walk + snap-turn only. Starting position is wherever Beat 3's fight ended (inside the airlock kill-box, roughly z 16–24). To progress, the player must physically walk north through the now-open `CommandDoor` (z=26) into `CommandReachPoint` (0, 1, 30) within a **4.5 m** radius (step 13) — no teleport, no NavMesh pulling them there. Screenplay direction has Ronin-7 "follow[ing]" Kessler with the katana sheathed, wiping the blade clean — flavor/staging only; no builder component enforces a follow behavior or a blade-wipe animation *(inferred, VO-pacing guidance rather than a scripted event)*.
- **Khall:** instantiated at (0, 0, 33) beside the holotable, `StoryNpc` displayName "Khall", `remote=true`, `AudioSource` (`HologramOn.wav`, playOnAwake, `spatialBlend = 1`). Built **`SetActive(false)`**, revealed in place by step 14. **No waypoints, no `NpcWalker`.** On screen this chapter he is **never named to the player** — dialogue lines label him only "Handler (Hologram)"; "Khall" is authoring metadata (§9).
- **`ChapterOutro`** at (0, 1, 30), inactive. `CampaignFlagSetter` flag `"ch1_complete"` wired to `OnActivated`; `completeCanvas` ref = the "CHAPTER 1 COMPLETE" world-space canvas at (0, 1.4, 30); `fadeDelay` 1.5 s, `fadeDuration` 2 s; publishes `ZoneCompleted` post-fade.

**Mission-spine steps:**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 11 *(Beat 3→4 boundary, listed for continuity)* | Trigger | "Trigger: Unlock Corridor + Kessler Walks" | unlocks `CommandDoor` (z=26) + activates `KesslerWalker` (leg 2) |
| 12 | Dialogue | "Beat4: Walk to Command" (`ch1_beat4_walk`) | plays over the corridor walk |
| 13 | ReachTrigger | "ReachTrigger: Command Room" | player enters `CommandReachPoint` (0,1,30), radius 4.5 |
| 14 | Trigger | "Trigger: Drone Message + Khall Hologram" | activates `khallGo` (reveals Khall + fires his `HologramOn.wav`) |
| 15 | Dialogue | "Beat4: Ultimatum" (`ch1_beat4_ultimatum`) | the handler's ultimatum through to "Take us out." |
| 16 | Trigger | "Trigger: Chapter Outro (flag + fade + canvas)" | activates `ChapterOutro` (0,1,30) |

Step 16 both ends Beat 4 and ends Chapter 1: `ChapterOutro.OnEnable` invokes its wired `onActivated` (a persistent-listener call into `CampaignFlagSetter.SetFlags`, setting `ch1_complete`), reveals the complete canvas, waits `fadeDelay`, then fades to black over `fadeDuration` via `ScreenFader`, and finally publishes `ZoneCompleted` — the same signal `GameFlowManager`'s normal mission-complete handling listens for elsewhere in the game.

The closing stage direction — "Ronin-7 takes a last look at the revival bay through the open hatch... then he turns toward the long dark ahead" — has no dedicated trigger or animation; the rig is head + hands only, so this beat is realized diegetically: the revival bay really is still open and visible back down the shared +Z corridor, so a player who turns to look gets the callback for free *(inferred)*.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Command Room shell (18×16) | center (0,0,34) | `Rooms.CommandRoomShell` | `…/Art/Generated/Rooms/CommandRoomShell.prefab` | **MISSING** |
| Windshield frame + collider-only glass | back wall, z=42 | `Rooms.CommandWindshield` | `…/Art/Generated/Rooms/CommandWindshield.prefab` | **MISSING** |
| Galaxy backdrop | (0, 1.8, 45) | `Props.GalaxyBackdrop` | `…/Art/Generated/Props/GalaxyBackdrop.prefab` | **MISSING** |
| ↳ backdrop material | — | — | `Assets/Ronin7/Art/Materials/Galaxy1View.mat` | **EXISTS** |
| `ViewscreenCracks` | across the glass, near z=41.8 | `Props.ViewscreenCracks` | `…/Art/Generated/Props/ViewscreenCracks.prefab` | **MISSING** |
| Captain's chair | (0, 0, 30) | `Props.CaptainsChair_Salvaged` | `…/Art/Generated/Props/CaptainsChair_Salvaged.prefab` | **MISSING** |
| `Holotable` | (0, 0.5, 33) | `Props.Holotable` | `…/Art/Generated/Props/Holotable.prefab` | **MISSING** |
| `CommandDrone` | (-6, 1.6, 38) | `Props.CommandDrone` | `…/Art/Generated/Props/CommandDrone.prefab` | **MISSING** |
| `DroneRail` + two wall brackets | rail at (-6, 1.78, 38); brackets ∓2 m fore/aft | `Props.DroneRail` | `…/Art/Generated/Props/DroneRail.prefab` | **MISSING** |
| `DeadDeckHatch` (command side) | (8.78, 1.3, 36), Euler(0,90,0) | `Props.DeadDeckHatch` | `…/Art/Generated/Props/DeadDeckHatch.prefab` | **MISSING** |
| Khall (hologram) | (0, 0, 33) | `Named.Khall` | `…/Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `CommandLight` | (0, 2.6, 34) | — | `ChapterEnvironmentProfile.accentLights["Command"]` | profile |

**Constraints the prefabs must respect.**

- **Windshield:** a structural frame (side pillars at x=±8, header near the ceiling, sill low, two vertical mullions at x=±2.7) glazed with an **invisible collider pane** — renderer stripped so it does not occlude the view. This reads as a wide "open canopy" onto the galaxy beyond the dead leviathan. The galaxy backdrop sits further out at z=45.
- **Viewscreen cracks:** three thin dark bars laid across the glass at odd rotations (roughly 18°, -28°, 70° roll) — the crack Ronin-7 catches his faint reflection in near the beat's end. Static; not a shader effect.
- **Captain's chair:** salvaged, held together with strapping — seat, back, worn pedestal, dull worn-brown. Kessler drops into it mid-scene; there is **no scripted animation** for this, it is staging guidance for whatever idle/seated pose his rig defaults to *(inferred)*.
- **`CommandDrone`:** rides its worn rail in the room's corner, spinning slowly via `TurntableRotator` *(component, not prefab geometry)*. Per canon this drone is the ship's "only other voice" this chapter and the seed of the persistent Hub-AI mascot it becomes later.
- **`DeadDeckHatch`:** a sealed hatch on the command room's east side into the rest of the cold, dark leviathan hull. Pure atmosphere, reinforcing "most of this ship is dead and untoured."
- **`CommandLight`:** the `Command` accent entry carries `behaviour: ConsoleFlicker(seed: 11)` — the light drifts between roughly 1.8 and 2.88 intensity on a per-seed curve. **No camera shake**; the flicker itself carries the "failing power" unease, consistent with the Cairn's "power is thin" texture.

#### d. Combat

None. Beat 4 is dialogue-and-traversal only — no enemies are spawned or activated (the boarding party all belong to Beat 3's `DefeatEnemies` step, already resolved before step 11 fires).

#### e. Dialogue / VO

Two dialogue sets, both authored in `Chapter1Lines.cs` and voiced through the shared `DialoguePlayer` (clips wired by `WireChapter1Clips`, resolving `Assets/Ronin7/Art/Generated/Audio/Voice/ch1_{setId}_{index:00}_{speaker-sanitized}.mp3|.wav` per line). Advance input is the same throughout the chapter: Left-Hand "Talk" (Y).

**`Dialogue_Beat4_Walk`** (`ch1_beat4_walk`, at (0,1,21), 8 lines, ~66.5 s) — Kessler explains the visors streamed everything up the chain live; Ronin-7 identifies the second voice as "not a commander... an owner"; Kessler half-confesses the debt is deeper than he's said ("I needed to believe a thing like you could exist. A killer that stops..."), ending on Ronin-7's flat "What man."

**`Dialogue_Beat4_Ultimatum`** (`ch1_beat4_ultimatum`, at (0,1,32), 26 lines, ~168.5 s — the longest set in the chapter):

1. **Drone**: "Message incoming. Priority override. I couldn't refuse it." *(this is the same reveal moment as mission step 14 activating `khallGo`)*
2. **Handler (Hologram)** — three lines of cold, unhurried ultimatum: names Kessler, states the Dominion's claim on "something of ours," gives three days, then names **Iris** — Kessler's daughter, held at Velorum — as the collateral.
3. Ronin-7's quiet "Iris?" breaks the hologram's exit; Kessler's 25-second confession names her as his daughter and the six years he's spent trying to buy her back.
4. Ronin-7 offers the trade ("Then it's simple. You give them what they came for. Me." / "My life for hers. A clean trade...") — Kessler refuses it flat ("No."), then argues the bluff ("They don't trade. They collect...").
5. Closes on "Then we don't trade" / "We go to Velorum, and we take her back" / Ronin-7's "I spared a man I don't remember. I want to know why I went easy" / Kessler's "Then we ask Velorum" / **Drone**: "Heading laid. Velorum." / **Ronin-7**: "Take us out."

The dialogue anchor points bracket the walk: `Dialogue_Beat4_Walk` at (0,1,21) sits inside the airlock corridor (where the walk starts), and `Dialogue_Beat4_Ultimatum` at (0,1,32) sits inside the Command Room near the holotable/chair cluster (where it ends).

**Speaker label note:** the Handler's lines are tagged `"Handler (Hologram)"` in data and VO direction, never `"Khall"` — this is the mechanism by which the character stays unnamed to the player this chapter while the `StoryNpc.displayName` ("Khall") and prefab path exist for continuity/authoring purposes only.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point — the hologram reveal, the ultimatum, and Ronin-7's offer/refusal are all carried by lighting (`ConsoleFlicker` on `CommandLight`), spatial audio, and VO performance, never by moving the player's view.
- **`HologramOn.wav`** plays from Khall's own transform (0,0,33) with `spatialBlend = 1` (full 3D) the instant step 14 activates him — a spatialized "bloom" cue roughly 3 m from the player's `CommandReachPoint` position, so the sound arrives from the holotable before the visual fully resolves.
- **`DoorSlide.wav`** fires from `CommandDoor`'s `AudioSource` (3D spatial, `playOnAwake=false`, driven by its `ProximityDoor.openClip`) as the door unlocks and slides open at the top of the beat.
- **No haptics** are authored for this beat — it is the chapter's one stretch without `BladeDamager`/combat feedback, and that silence is itself part of the "the fight is over, now deal with what it cost" pacing. `CombatFeedbackController` remains resident on the `Game` root but has nothing to drive here.
- Standard **comfort vignette** from `ContinuousLocomotion` applies through the corridor walk into the Command Room, same as every other traversal stretch.
- **Room tone:** `ReverbZonePlacer` auto-tags both the Airlock corridor (tight) and Command Room (open) as distinct interior volumes, so the Command Room's reverb reads noticeably larger/boomier than the corridor the player just walked out of — reinforcing that this is the ship's one "big" lit space.

## 5. Character travel-route master table

The **only** NPC who physically travels during Chapter 1 is Kessler, and he travels exactly twice, on rails, via the `NpcWalker` + `MissionDirector` Trigger idiom: `BuildNpcWalker` creates an inactive `GameObject` holding an `NpcWalker` component targeting Kessler's transform and an ordered array of child waypoint `Transform`s; a mission-spine `Trigger` step later calls `SetActive(true)` on that walker, which then drags Kessler's transform through the waypoints **once**, in order, and stops.

| Leg | Waypoints (x, y=`kesslerFloorY`, z) | Activated by | Builder object |
|---|---|---|---|
| Leg 1 — Revival Bay → Main Hold | (0, y, 2) → (0, y, 6) → (-2, y, 9.5) *(by the wreck-field viewport)* | Mission step 3 Trigger | `KesslerToHold` (`NpcWalker`, waypoints from `KesslerToHoldWaypoints(floorY)`) |
| Leg 2 — Main Hold → Command Room | (0, y, 10) → (0, y, 21) → (0, y, 28) → (2, y, 31) | Mission step 11 Trigger | `KesslerWalker` (`NpcWalker`, waypoints from `KesslerWalkerWaypoints(floorY)`) |
| Idle (no travel) | `StoryNpcWander` radius **0.7 m** around his spawn point (-1.6, 0, -0.2), beside the exam table | always active until Leg 1 fires | on the `Kessler` GameObject itself |

**Y-invariant (must hold or Kessler sinks/floats):** Kessler's Tripo mesh has a **centered pivot**, so `FitNamedCharacter` grounds his root to `kesslerFloorY = kesslerGo.transform.position.y` *after* scaling/grounding — not necessarily 0. `NpcWalker` drags the **full** waypoint `Vector3`, including Y, so every waypoint in both legs must carry that same `kesslerFloorY`, never a hard-coded 0. This is exactly the regression `Chapter1BuilderTests` guards against (§8) — both `KesslerToHoldWaypoints(floorY)` and `KesslerWalkerWaypoints(floorY)` take `floorY` as a parameter for this reason, and any patch that reintroduces a literal `0f` in a waypoint Y is the bug this whole mechanism exists to prevent.

> **This invariant survives the refactor unchanged.** Swapping Kessler's prefab, or moving his instantiation into `BuildBeatNLogic()`, does not license changing these two method signatures — they are the only part of `BuildChapter1Hub()` under test.

Khall is the only other named character present in Ch1, and he does **not** travel — he is a stationary hologram: instantiated at (0, 0, 33) beside the `Holotable`, `SetActive(false)` at build time, and revealed in place by the step-14 Trigger. No waypoints, no `NpcWalker`.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Key/accent entry | Behaviour | Window / backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 1 — Revival/Wake, Settle | warm, clinical, close | `accentLights["Medbay"]` | `AmbientPulse(7s)` | none — enclosed room, no exterior view | none lighting-wise; the room itself is static (open casket, katana resting on the workbench) |
| 2 — Main Hold, Adrift | neutral, vast, quietly ominous | `accentLights["Hold"]` | `None` *(deliberate)* | wreck-field viewport: dead hulls + `DirtyStar`, static | **Trigger (step 6):** `RunningLight` (inactive → active) begins orbiting past the viewport — the first visible sign of the Dominion's arrival |
| 3 — Airlock, Boarding | cool corridor tightening into red alarm | `accentLights["Airlock"]` | `None` | none (corridor has no exterior view) | **Trigger (step 7):** `eventLights["DockingAlarm"]` activates — red wash — with looping `WaveAlarm.wav` and one-shot `Landing.wav` (clamp clank) firing on enable |
| 4 — Command Room, Ultimatum | cold, command, unstable power | `accentLights["Command"]` | `ConsoleFlicker(seed: 11)` | cracked galaxy viewscreen: `Galaxy1View.mat` behind the windshield, overlaid with static `ViewscreenCracks` | **Trigger (step 14):** Khall (`SetActive(false)`→`true`) reveals as a hologram with `HologramOn.wav`; the drone is already present and rotating throughout the beat. *(Any additional bloom/glow on the hologram reveal beyond the base material is not asserted in the builder — mark as (inferred) if a beat-owner wants to add a bloom pass here.)* |

Fog is the same baseline exponential bed in every beat — a single profile value, never overridden per-room.

## 7. Audio / VO manifest cross-reference

Seven canonical dialogue sets, defined in `Chapter1Lines.cs` and consumed via `Chapter1Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch1_beat1_wake` | 1 | (0, 1, 1) — `Dialogue_Beat1_Wake` |
| `ch1_beat1_settle` | 1 | (0, 1, 1) — `Dialogue_Beat1_Settle` |
| `ch1_beat2_adrift` | 2 | (0, 1, 10) — `Dialogue_Beat2_Adrift` |
| `ch1_beat3_board_pre` | 3 | (0, 1, 13) — `Dialogue_Beat3_BoardPre` |
| `ch1_beat3_board_post` | 3 | (0, 1, 11) — `Dialogue_Beat3_BoardPost` |
| `ch1_beat4_walk` | 4 | (0, 1, 21) — `Dialogue_Beat4_Walk` |
| `ch1_beat4_ultimatum` | 4 | (0, 1, 32) — `Dialogue_Beat4_Ultimatum` |

Each is built by the local `BuildChapter1Dialogue` wrapper (not the shared `BuildDialoguePlayer` clip loader, which looks in the wrong folder for this chapter): it calls the shared player builder with `clipSetId: null`, then wires clips itself via `WireChapter1Clips`, resolving each line's `AudioClip` from `Chapter1Lines.ClipName(setId, index, speaker)` — pattern `ch1_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist, so a partial VO batch is loud, not silent. **Advance input for every dialogue line and the release prompt is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all seven `DialoguePlayer`s and the `PromptInputAdvancer`.

**Dialogue is data, not art.** None of this changes in the refactor — the seven set ids, their positions, and the clip-resolution pattern are canon.

SFX bed, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip | Used for |
|---|---|
| `DoorSlide.wav` | all three sliding doors (`WireDoorAudio`) |
| `WaveAlarm.wav` | looping docking-alarm tone (Beat 3 red wash) |
| `Landing.wav` | one-shot boarding-clamp clank (fires alongside the alarm) |
| `HologramOn.wav` | Khall's hologram reveal (Beat 4) |
| `OnFootAmbience.wav` | the Cairn's 2D ambient bed (loop, vol 0.35, on the `Game` root) |
| `SFX/medbay_hum.wav` | revival-bay 3D ambience layer (at (3, 1, -3.3), 2–6 m falloff, vol 0.45) |

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Galaxy 1 → Build Chapter 1 (Fresh)** (`XRRigBuilder.BuildChapter1Hub()`).
2. **EditMode is the gate.** Baseline is **842 tests green, 0 skips**; PlayMode is **70/70 green**. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot.** **No EditMode test invokes `BuildChapter1Hub()` or loads `Galaxy1_Ch1_Hub.unity`.** The suite covers pure logic only — the Kessler waypoint math, `StableHash`, `PickRoomDetailArchetypes`, and `Chapter1Lines` data. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **Y-invariant regression coverage:** `Project/Assets/Ronin7/Tests/EditMode/Chapter1BuilderTests.cs` directly tests `XRRigBuilder.KesslerToHoldWaypoints(floorY)` and `KesslerWalkerWaypoints(floorY)` at floorY ∈ {0, 0.87, 1.23}, asserting every waypoint's Y equals the given `floorY` and that the authored X/Z layout (viewport approach, corridor line, command-room offset) is untouched. **Any patch to Kessler's routes must keep these tests green.** This is the one regression the suite actually catches.
4. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
5. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty room, never an exception.
6. **Perf reference bar** (Ch1 hub scene, edit-mode `UnityStats` at greybox, 2026-07-02, `Project/Docs/CHAPTER-BUILD-LEDGER.md`): drawCalls 189, setPassCalls 17, tris 9,198, verts 13,092. **Re-measure after every prefab lands.** Prefabs carry their own materials and will not `TintShared`-batch; a swap that meaningfully exceeds this bar must be investigated before shipping. The 72 Hz floor is not negotiable.
7. **Console check:** `WireChapter1Clips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter1Hub()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter1Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild. Nothing outside it survives. See `Project/Docs/IMPROVEMENT-SUMMARY.md`.
- **Do not auto-delete orphan materials.** ~288 unreferenced material variants exist but are regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** The 14 current scenes are MeshCollider-free (2026-07-04 audit). A high-fidelity art pass is exactly the vector that reintroduces one — check FBX import settings' "Generate Colliders" on every prop landing in the registry. Room shells and props get primitive colliders; the windshield glass gets a box collider with its renderer stripped.
- **Canon soft spot (flagged, not fixed):** `story ouput/audit/Ch01_audit.md` §3 flags a low-severity tension in Beat 4 between Kessler's line that Iris "was in a Dominion school, but they probably took her in Velorum's markets" and the story bible's framing of her as already "held as collateral" in Velorum. The audit's recommendation — drop "probably" so the school→market move reads as deliberate leverage, or (writers'-room call) cut the "Dominion school" detail entirely and have her already in the markets — is **not yet resolved**; do not silently pick one interpretation when touching Beat 4 dialogue.
- **Khall stays unnamed on screen in Ch1.** He is instantiated and tagged internally as `StoryNpc` `displayName = "Khall"`, but every on-screen dialogue label in `Chapter1Lines.cs` refers to him only as `"Handler (Hologram)"` / `"Comm (V.O.)"`. Naming him is reserved for Chapter 4 — do not surface "Khall" in any UI, subtitle, or VO line within this chapter.
- **The katana's shadow-AI nature stays dormant/unrevealed in Ch1.** Per the audit's positive confirmation #3, the blade is treated purely as "a plain katana... feels like yours" with no shadow-AI dialogue or memory-vessel claim anywhere in this chapter's lines or builder wiring (`Named.Echo` is placed as an inert prop). Any future patch that adds shadow-AI behavior or dialogue to the Ch1 blade would break this canon boundary.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials — this is why the greybox scene hits drawCalls 189.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch1Environment.asset`. Currently set inline at the top of `BuildChapter1Hub` (`Chapter1Builder.cs:57–84`).

| | Value |
|---|---|
| Directional key | color (0.82, 0.88, 1.0), intensity 0.9, rotation Euler(50, -30, 0) |
| Ambient | mode **Flat**, color (0.15, 0.16, 0.19) |
| Fog | mode **Exponential**, color (0.16, 0.17, 0.20), density 0.018 |
| Floor tint | (0.19, 0.21, 0.25) |
| Ceiling tint | (0.11, 0.12, 0.15) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour | Read |
|---|---|---|---|---|---|---|
| `MedbayLight` | (0, 2.6, 0) | (1, 0.82, 0.6) | 2.2 | 11 | `AddAmbientPulse(period: 7f)` | warm revival bay |
| `HoldLight` | (0, 2.8, 10) | (0.72, 0.82, 0.95) | 1.8 | 16 | none | neutral hold |
| `AirlockLight` | (0, 2.6, 21) | (0.6, 0.78, 1) | 1.5 | 12 | none | cool corridor |
| `CommandLight` | (0, 2.6, 34) | (0.5, 0.82, 1) | 2.4 | 16 | `AddConsoleFlicker(seed: 11f)` → drifts 1.8–2.88 | cold command |

**Event light:**

| Light | Position | Color | Intensity | Range | State |
|---|---|---|---|---|---|
| `DockingAlarmLight` | (0, 2.6, 21) | (1, 0.2, 0.15) | 3 | 12 | inactive; shadows off; activated by step 7 |

### A.2 Beat 1 — Revival / Medical Bay

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-4,4], z[-4,4], center (0,0,0), 8×8 | `Chapter1Builder` room-build block |
| Walls | `WallW` x=-4, `WallE` x=4, `WallFront` z=-4, doorway wall z=4 (2.4 m gap) | `BuildWall` / `BuildDoorwayWall` |
| Door | `MedbayDoor` (0,0,4), width 2.4, `startLocked: true` | `BuildSlidingDoor(interior, "MedbayDoor", new Vector3(0,0,4), 2.4f, true, startLocked: true)` |
| Accent light | (0,2.6,0), warm (1,0.82,0.6), i2.2, r11, pulse period 7s | `MedbayLight` + `AddAmbientPulse` |
| Ambience | (3,1,-3.3), `medbay_hum.wav` | `MedbayAmbience` AudioSource |
| Exam table | (0,0.5,-0.2) scale (0.9,0.12,2.0); base (0,0.25,-0.2) | `BuildMedbayProps` → `ExamTable`, `ExamTableBase` |
| Monitors | (3.85,1.7,-1), (3.85,1.7,0.6) cyan; `RevivalMonitor` (-3.85,1.6,0.8) cyan | `BuildMedbayProps` / `BuildRevivalBayStory` |
| Workbench | (2.6,0.45,-2.6) scale (1.6,0.9,0.7) | `BuildMedbayProps` |
| Casket | shell (-1.4,0,1.0) 0.85×0.55×2.1; interior 0.65×0.4×1.9; lid tilted 70° at local (-0.7,0.35,0); seal (0.44,0.55,0), Dominion lock color (0.7,0.5,0.16) | `BuildRevivalBayStory` → `DominionCasket`, `Casket_Shell/Interior/Lid/Seal` |
| IV rack | pole (0,0.8,0) 0.04×1.6×0.04; foot (0,0.03,0); pale-green bag (0.12,1.4,0) — root at (1.1,0,-0.7) | `IVRack` |
| Cutting torch / pry bar | (2.1,0.98,-2.5) / (2.9,0.96,-2.55) | `CuttingTorch`, `PryBar` |
| Table cable | (0.5,0.45,0.4) | `TableCable` |
| Katana "Echo" | (2.15,0.93,-2.6), rot Euler(0,90,0) | `BuildSword(..., Ch1EchoBladePrefab)` — `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` |
| Kessler | spawn (-1.6,0,-0.2); grounded Y = `kesslerFloorY` | `InstantiateNpc(Ch1KesslerPrefab, ...)`, `FitNamedCharacter`, `StoryNpc` (displayName "Kessler", remote=false), `StoryNpcWander` radius 0.7 |
| Kessler leg-1 walk | `(0,kesslerFloorY,2)→(0,kesslerFloorY,6)→(-2,kesslerFloorY,9.5)` | `KesslerToHoldWaypoints(floorY)`, `BuildNpcWalker(interior,"KesslerToHold",kesslerGo,...)` — inactive until step 3 |
| Conduit spark | (3.6,2.4,-3.7) — emissive nub + point light, plus `SwordSpark` VFX prefab if present | `BuildConduitSpark` |
| Dead deck hatch | (0,1.3,-3.88) — dark frame, single dead status light | `BuildDeadDeckHatch` |
| Dialogue anchors | (0,1,1) both | `BuildChapter1Dialogue("Dialogue_Beat1_Wake", (0,1,1), "ch1_beat1_wake", talkRef)`; `BuildChapter1Dialogue("Dialogue_Beat1_Settle", (0,1,1), "ch1_beat1_settle", talkRef)` |
| Release prompt | (0,1.4,1), scale 0.012, text "Release  (Y)" | `BuildReleasePrompt` → `TextMesh` + `PromptInputAdvancer`, wired to `MissionDirector` via `advancerSo` |
| Mission steps | indices 0–3 of 17 | `AuthorDialogueStep(0, dlgWake)`, `AuthorPromptStep(1, releasePromptGo)`, `AuthorDialogueStep(2, dlgSettle)`, `AuthorTriggerStep(3, medbayDoor, kesslerToHoldGo)` |
| Player rig | `BuildRig(addLocomotion:true)` + `EchoPresence`; `ZoneBounds` center (0,0,19) r45 | — |

### A.3 Beat 2 — Main Hold

| Object / Method | Value |
|---|---|
| Room floor/ceiling | `BuildFloorCeiling(interior, "Hold", (0,0,10), (12,0,12), floorColor, ceilColor)` |
| Doorway walls | `Hold_WallFront` (0,RoomH/2,4) width 12, gap 2.4; `Hold_WallBack` (0,RoomH/2,16) width 12, gap 2.4 |
| East wall | `Hold_WallE` (6,RoomH/2,10), size (0.2,RoomH,12) |
| Viewport (west wall) | `BuildHoldViewport(interior)` — Sill (-6,0.4,10) size(0.2,0.8,12); Header (-6,(2.8+RoomH)/2,10) size(0.2,RoomH-2.8,12); PillarA (-6,1.8,5.5) size(0.2,2,3); PillarB (-6,1.8,14.5) size(0.2,2,3). Clear opening z[7,13], y[0.8,2.8] |
| Backdrop / wrecks | `WreckFieldBackdrop` (-8,1.8,10) scale(11,5,1) rot(0,-90,0), color (0.05,0.06,0.12); `Wreck0` (-7.2,1.4,8.4); `Wreck1` (-7.4,2.3,11.6); `Wreck2` (-7.0,1.0,12.4) — all tint (0.12,0.13,0.16) |
| Running light | `RunningLightHub` (-9,1.8,10); `RunningLight` sphere (-7,1.8,10) scale 0.25, `Light` point (1,0.7,0.5) i1.2 r6, `PlanetOrbit` center=hub, radius=2.2, angularSpeedDeg=18, yOffset=0, startAngleDeg=0; built `SetActive(false)`; returned from `BuildHoldViewport` and passed to `AuthorTriggerStep(steps, 6, "Trigger: Wreck-field Running Light", runningLight)` |
| Room details / dressing | `BuildRoomDetails(interior, "Hold", (2,0,10), (3.5,5.5), (0.3,0.32,0.36))`; `BuildHoldStory(interior)` |
| Workbench + cups | `HoldWorkbench` (3.6,0.45,7.5) size(1.4,0.9,0.7); `Cup_A` (3.4,0.98,7.4) scale(0.07,0.06,0.07); `Cup_B` (3.7,0.98,7.6) same scale |
| Salvage piles | `Salvage_HullPlate0` (4.6,0.15,12.5); `Salvage_HullPlate1` (4.6,0.28,12.4); `Salvage_HullPlate2` (4.55,0.4,12.6); `Salvage_CopperCoil0` (3.4,0.25,13.4); `Salvage_CopperCoil1` (3.9,0.25,13.5) |
| DirtyStar | (-9.5,2.6,6.5) scale 0.5, tint (1,0.78,0.45); `Light` point (1,0.8,0.5) i1.1 r14, no shadows |
| Accent light | `BuildAccentPointLight("HoldLight", (0,2.8,10), (0.72,0.82,0.95), 1.8, 16)` |
| Kessler leg-1 walker | `BuildNpcWalker(interior, "KesslerToHold", kesslerGo, KesslerToHoldWaypoints(kesslerFloorY))` → waypoints `(0,y,2)`,`(0,y,6)`,`(-2,y,9.5)`; activated by step 3's `AuthorTriggerStep(..., medbayDoor, kesslerToHoldGo)` |
| Reach point | `HoldReachPoint` GameObject at (0,1,10); `AuthorReachStep(steps, 4, "ReachTrigger: Main Hold", holdReachGo.transform, 4.5f)` |
| Dialogue player | `BuildChapter1Dialogue("Dialogue_Beat2_Adrift", (0,1,10), "ch1_beat2_adrift", talkRef)`; `AuthorDialogueStep(steps, 5, "Beat2: Adrift", dlgAdrift)` |
| Running-light trigger | `AuthorTriggerStep(steps, 6, "Trigger: Wreck-field Running Light", runningLight)` |
| Doors bounding this room | `MedbayDoor` (0,0,4) startLocked=true (opened by step 3); `AirlockInnerDoor` (0,0,16) startLocked=false |

### A.4 Beat 3 — Airlock Corridor + Medbay Kill-Box

| Item | Value | Source |
|---|---|---|
| Airlock room | center (0,0,21), 4×10, x[-2,2] z[16,26], `RoomH`=3.6; floor/ceiling tint (0.19,0.21,0.25) / (0.11,0.12,0.15) | `Chapter1Builder.cs` |
| `AirlockInnerDoor` | (0,0,16), width 2.4, `startLocked:false` (already open at Beat 3) | `BuildSlidingDoor` |
| `CommandDoor` | (0,0,26), width 2.4, `startLocked:true`, unlocked at step 11 | `BuildSlidingDoor` |
| `OuterDockHatch` / `OuterDockHatchSeal` | (1.95,1.4,21) / (1.9,1.4,21), east wall, brass seal ring | `BuildAirlockHatch` |
| `BoardingClamp` | (2.05,1.4,21), 4 props (`Clamp_Ring/ArmTop/ArmBot/ArmL`) | `BuildBoardingClamp` |
| `HullStencil_TheCairn` | (-1.88,1.9,24), rot Y=90°, `TextMesh` "THE CAIRN", west wall facing +X | Cairn atmosphere pass |
| `ConduitSpark` (airlock) | (-1.7,2.3,18) — emissive nub + point light (0.8,0.85,1) i0.8 r3, plus `SwordSpark` VFX prefab if present | Cairn atmosphere pass |
| `AirlockLight` (accent) | (0,2.6,21), cool (0.6,0.78,1) i1.5 r12 — always on | `BuildAccentPointLight` |
| `DockingAlarmLight` | (0,2.6,21), red (1,0.2,0.15) i3 r12, inactive→step 7 | Chapter1Builder inline |
| `WaveAlarm.wav` / `Landing.wav` | loop / one-shot, play-on-awake children of `DockingAlarmLight` | `AddOneShotOnEnable` |
| Trooper spawns | (-1,0,20), (1,0,20), (0,0,22), (0,0,23.5) | `trooperPositions[]` |
| Enemy build fn | `BuildDominionEnemy(pos, playerHealth, enemyDef)` — `Health` + `Enemy`, `ArmR/Sword/Blade/BladeTip` chain | `ChapterSharedBuilders.cs:669` |
| Trooper prefab path | `ArtPrefabBuilder.DominionTrooperPrefabPath` → `Assets/Ronin7/Prefabs/Art/DominionTrooper.prefab` — **not on disk**; shipped art is `Characters3D/Enemies/Dominion_Trooper.prefab`, applied additively by `EnemyArtWirer` | see Appendix B |
| Medbay/Revival room (kill-box) | center (0,0,0), 8×8, single door gap at z=4 only (`Medbay_WallBack`), `Medbay_WallFront` (z=-4) solid | `Chapter1Builder.cs` |
| `Dialogue_Beat3_BoardPre` | (0,1,13), set `ch1_beat3_board_pre` | `BuildChapter1Dialogue` |
| `Dialogue_Beat3_BoardPost` | (0,1,11), set `ch1_beat3_board_post` | `BuildChapter1Dialogue` |
| Mission steps | 7 Trigger(alarmGo) · 8 Dialogue(dlgBoardPre) · 9 DefeatEnemies(trooperHealths) · 10 Dialogue(dlgBoardPost) · 11 Trigger(commandDoor, walkerGo) | `AuthorTriggerStep`/`AuthorDialogueStep`/`AuthorDefeatStep` |
| Kessler leg-2 walker | `KesslerWalkerWaypoints(floorY)`: (0,Y,10)→(0,Y,21)→(0,Y,28)→(2,Y,31) | `Chapter1Builder.cs:453` |

### A.5 Beat 4 — Corridor → Command Room

| Element | Coordinates / value | Component / method |
|---|---|---|
| Command Room floor | center (0,0,34), size 18×16 → x[-9,9], z[26,42] | `BuildFloorCeiling(..., "Command", ...)` |
| Command_WallW / WallE | (-9, 1.8, 34) / (9, 1.8, 34), size (0.2, 3.6, 16) | `BuildWall` |
| Command_WallFront (door gap) | (0, 1.8, 26), width 18, gap 2.4 | `BuildDoorwayWall` |
| CommandDoor | (0,0,26), width 2.4, `startLocked: true` | `BuildSlidingDoor("CommandDoor", ...)` + `WireDoorAudio` |
| Command Windshield frame | pillars x=±8, header y=`RoomH-0.25`, sill y=0.3, mullions x=±2.7, all z=42 | `BuildCommandWindshield(interior, galaxyMat)` |
| Windshield glass (collider-only) | (0, 1.8, 42), scale (18, 3.6, 0.05), renderer stripped | same method |
| Galaxy backdrop quad | (0, 1.8, 45), scale (22, 6, 0.1), mat `Assets/Ronin7/Art/Materials/Galaxy1View.mat` | same method |
| Viewscreen cracks (×3) | ~(-1.5,1.9,41.8) 18° / (0.6,2.2,41.8) -28° / (-0.4,1.4,41.8) 70°, color (0.03,0.03,0.05) | `BuildViewscreenCracks` |
| Captain's chair | seat/back/pedestal centered at (0,0,30), dull worn-brown tint | `BuildCaptainsChair(interior, new Vector3(0,0,30))` |
| Holotable | (0,0.5,33), scale (1.6,0.5,1.6), tint (0.15,0.18,0.22) | inline `PrimitiveType.Cylinder` in `BuildChapter1Hub` |
| Khall | spawn (0,0,33), `StoryNpc` displayName "Khall", `remote=true`; `AudioSource` (`HologramOn.wav`, playOnAwake, spatialBlend 1); built `SetActive(false)` | `InstantiateNpc(Ch1KhallPrefab, ...)`; prefab `Assets/Ronin7/Art/Generated/Characters3D/Named/Khall.prefab` |
| CommandDrone | (-6,1.6,38), sphere+antenna nub, glow tint (0.3,0.9,1), `TurntableRotator` | `BuildShipDrone` |
| DroneRail + brackets | rail (-6,1.78,38) scale (0.06,0.06,4); brackets at z∓2 | `BuildDroneRail` |
| CommandLight (accent) | (0,2.6,34), cold (0.5,0.82,1), intensity 2.4, range 16 | `BuildAccentPointLight` |
| Console flicker | `ConsoleFlickerLight` on `CommandLight`, min 1.8 / max 2.88, seed 11 | `AddConsoleFlicker("CommandLight", seed: 11f)` |
| DeadDeckHatch (command side) | (8.78,1.3,36), rotation `Euler(0,90,0)` | `BuildDeadDeckHatch` (part of `BuildCairnAtmosphere`) |
| KesslerWalker (leg 2) | waypoints (0,y,10)→(0,y,21)→(0,y,28)→(2,y,31), y=`kesslerFloorY`; inactive `NpcWalker`, target = Kessler's `Transform` | `BuildNpcWalker(interior, "KesslerWalker", kesslerGo, KesslerWalkerWaypoints(kesslerFloorY))` |
| CommandReachPoint | (0,1,30), reach radius 4.5 | `AuthorReachStep(steps, n++, "ReachTrigger: Command Room", commandReachGo.transform, 4.5f)` |
| Dialogue_Beat4_Walk | (0,1,21), set `ch1_beat4_walk`, 8 lines | `BuildChapter1Dialogue("Dialogue_Beat4_Walk", new Vector3(0,1,21), "ch1_beat4_walk", talkRef)` |
| Dialogue_Beat4_Ultimatum | (0,1,32), set `ch1_beat4_ultimatum`, 26 lines | `BuildChapter1Dialogue("Dialogue_Beat4_Ultimatum", new Vector3(0,1,32), "ch1_beat4_ultimatum", talkRef)` |
| ChapterOutro | (0,1,30), inactive; `CampaignFlagSetter` flag `"ch1_complete"` wired to `OnActivated`; `completeCanvas` ref = CHAPTER 1 COMPLETE canvas at (0,1.4,30); `fadeDelay` 1.5 s, `fadeDuration` 2 s; publishes `ZoneCompleted` post-fade | `ChapterOutro` (`Scripts/Player/ChapterOutro.cs`) |
| Mission steps | steps[11]–steps[16] of the 17-step `MissionDirector.steps` array | `AuthorTriggerStep` / `AuthorDialogueStep` / `AuthorReachStep` |

### A.6 Scene root hierarchy (current)

`BuildChapter1Hub()` creates these as **siblings**, not nested: `Directional Light`, `ShipInterior` (all geometry, props, doors, NPCs, drone, holotable, cracks, hatches), the four accent lights, `Game` (`GameState` + `CombatFeedbackController` + the 2D ambience source), the player rig, `HoldReachPoint`, `CommandReachPoint`, seven dialogue-player roots, `ReleasePrompt`, the complete canvas, `ChapterOutro`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and four `[BEAT_N_LOGIC]` roots, and moves `ShipInterior`'s contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Five resolve; everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Kessler` | `Art/Generated/Characters3D/Named/Kessler.prefab` | **EXISTS** |
| `Named.Khall` | `Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Enemies.DominionTrooper` | `Art/Generated/Characters3D/Enemies/Dominion_Trooper.prefab` | **EXISTS** |
| `Vfx.ConduitSpark` | `Resources/Vfx/SwordSpark.prefab` | **EXISTS** *(borrowed — not under `Art/Generated/`)* |
| `Rooms.MedbayShell` | `Art/Generated/Rooms/MedbayShell.prefab` | MISSING |
| `Rooms.MainHoldShell` | `Art/Generated/Rooms/MainHoldShell.prefab` | MISSING |
| `Rooms.HoldViewportFrame` | `Art/Generated/Rooms/HoldViewportFrame.prefab` | MISSING |
| `Rooms.AirlockShell` | `Art/Generated/Rooms/AirlockShell.prefab` | MISSING |
| `Rooms.CommandRoomShell` | `Art/Generated/Rooms/CommandRoomShell.prefab` | MISSING |
| `Rooms.CommandWindshield` | `Art/Generated/Rooms/CommandWindshield.prefab` | MISSING |
| `Doors.SlidingDoor_Standard` | `Art/Generated/Doors/SlidingDoor_Standard.prefab` | MISSING |
| `Props.ExamTable` | `Art/Generated/Props/ExamTable.prefab` | MISSING |
| `Props.DominionCasket_Open` | `Art/Generated/Props/DominionCasket_Open.prefab` | MISSING |
| `Props.Medical_IVRack` | `Art/Generated/Props/Medical_IVRack.prefab` | MISSING |
| `Props.WallMonitor` | `Art/Generated/Props/WallMonitor.prefab` | MISSING |
| `Props.RevivalMonitor` | `Art/Generated/Props/RevivalMonitor.prefab` | MISSING |
| `Props.Workbench_Dirty` | `Art/Generated/Props/Workbench_Dirty.prefab` | MISSING |
| `Props.CuttingTorch` | `Art/Generated/Props/CuttingTorch.prefab` | MISSING |
| `Props.PryBar` | `Art/Generated/Props/PryBar.prefab` | MISSING |
| `Props.TableCable` | `Art/Generated/Props/TableCable.prefab` | MISSING |
| `Props.DeadDeckHatch` | `Art/Generated/Props/DeadDeckHatch.prefab` | MISSING |
| `Props.WreckFieldBackdrop` | `Art/Generated/Props/WreckFieldBackdrop.prefab` | MISSING |
| `Props.WreckSilhouette` | `Art/Generated/Props/WreckSilhouette.prefab` | MISSING |
| `Props.DirtyStar` | `Art/Generated/Props/DirtyStar.prefab` | MISSING |
| `Props.DentedCup` | `Art/Generated/Props/DentedCup.prefab` | MISSING |
| `Props.Salvage_HullPlate` | `Art/Generated/Props/Salvage_HullPlate.prefab` | MISSING |
| `Props.Salvage_CopperCoil` | `Art/Generated/Props/Salvage_CopperCoil.prefab` | MISSING |
| `Props.OuterDockHatch` | `Art/Generated/Props/OuterDockHatch.prefab` | MISSING |
| `Props.BoardingClamp` | `Art/Generated/Props/BoardingClamp.prefab` | MISSING |
| `Props.HullStencil_TheCairn` | `Art/Generated/Props/HullStencil_TheCairn.prefab` | MISSING |
| `Props.GalaxyBackdrop` | `Art/Generated/Props/GalaxyBackdrop.prefab` | MISSING |
| `Props.ViewscreenCracks` | `Art/Generated/Props/ViewscreenCracks.prefab` | MISSING |
| `Props.CaptainsChair_Salvaged` | `Art/Generated/Props/CaptainsChair_Salvaged.prefab` | MISSING |
| `Props.Holotable` | `Art/Generated/Props/Holotable.prefab` | MISSING |
| `Props.CommandDrone` | `Art/Generated/Props/CommandDrone.prefab` | MISSING |
| `Props.DroneRail` | `Art/Generated/Props/DroneRail.prefab` | MISSING |
| `Vfx.RunningLightMote` | `Art/Generated/VFX/RunningLightMote.prefab` | MISSING |

**Reuse notes.**

- `Props.Workbench_Dirty` serves both the Medbay workbench (Beat 1) and `HoldWorkbench` (Beat 2).
- `Props.DeadDeckHatch` serves both hatches (Beat 1 at z=-3.88, Beat 4 at z=36).
- `Doors.SlidingDoor_Standard` serves all three doors; lock state is logic, not art.
- `Vfx.ConduitSpark` currently borrows `SwordSpark.prefab` from `Resources/Vfx/`. It is the one key that resolves outside `Art/Generated/`. Give it a dedicated prefab when the art pass reaches it.
- **`ArtPrefabBuilder.DominionTrooperPrefabPath`** points at `Assets/Ronin7/Prefabs/Art/DominionTrooper.prefab`, which **is not on disk**. The trooper art the player actually sees is applied additively by `EnemyArtWirer.cs` after the build, from `Characters3D/Enemies/Dominion_Trooper.prefab`. Folding that wirer's work into the registry — so a fresh build produces rigged troopers directly — is a natural follow-up to this refactor, but is **not** in its scope.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,Doors,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter1Builder.cs`, `ChapterSharedBuilders.cs`, `Chapter1Lines.cs`, `Scripts/Player/ChapterOutro.cs`, `Tests/EditMode/Chapter1BuilderTests.cs`, `story ouput/Ch01_The_Salvagers_Debt.md`, `story ouput/Ch01_The_Salvagers_Debt_Dialogue_Script.md`, `story ouput/audit/Ch01_audit.md`.*

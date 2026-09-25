# Chapter 16 — Scene Construction

*The architectural contract for `Ch16_ThroneOfAshes.unity`: what Chapter 16 (the saga finale) must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 16 ("The Throne of Ashes") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from. Chapter 16 is **the saga finale** — it merges the former Ch15 ("The Vault Within") and Ch16 ("The Throne of Ashes") into one continuous `[CAVE]` descent, closes Ladders C, D, and E, and closes Act IV and the story.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter16Builder.cs`, entry point `XRRigBuilder.BuildChapter16ThroneOfAshes()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 16 — The Throne of Ashes**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch16_The_Throne_of_Ashes.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** Every impact in this chapter — the Wardens, the Samurai-4 duel, the three Khall-image loop deaths, the three-phase Maelgorn boss, the Engine-break liberation burst — reads through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle, never through moving the camera. The Time-Loop trap in particular is a *lighting/set-dressing toggle only* — see §4, Beat 6 — precisely so the "dying and resetting" REVEAL never has to touch the player's head transform.
- **Traversal in Ch16 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. **There is no teleport locomotion and no NavMesh anywhere in this chapter.** Verticality across the ten-tier descent is delivered entirely by **walkable, comfort-safe sloped ramps** (`Ch16BuildRamp`) — never a forced camera motion, never a lift/elevator cutscene mid-descent. `AttachPlayerAbilities(rig, refs)` grants the full five-ability chain earned across the saga (weakpoint-sight Ch7, Overdrive Ch9, Phase-step Ch10, Unbroken Ch11, Mirror Ch12) for use against the descent's combat, but none of those abilities substitutes base locomotion — they are combat/traversal *modifiers* riding on top of walk + snap-turn, not a parkour/climb/wall-run system. Do not introduce any of the excluded mechanics when patching this scene.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | tier shells (floor/ceiling/walls), ramps, props, VFX, backdrops, decorative lights, the boss-mesh instantiation *(the physical object)* | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | enemy activation state, `DuelYield`/`LeashBreakController`/`TimeLoopController` wiring, NPC reveal/activation, reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**There is no door in this chapter to split across both** (§2 explains why — the Sepulcher has no sliding doors, only reach-gated tiers and `DefeatEnemies` gates). The nearest equivalent is a **named-cast reveal**: `BuildBeatNArt()` instantiates a decorative or combat NPC (Khall, Maelgorn, the Hollow Kings, each Khall-image, each Maelgorn phase) `SetActive(false)`; `BuildBeatNLogic()` wires the `Trigger` step that flips it active. Art builds the thing; logic decides when it appears.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildEnemy`, `Author*Step`, `InstantiateNpc`, `FitNamedCharacter` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`.
- **Free to restructure:** the Ch16-local helpers, called only from `BuildChapter16ThroneOfAshes()` — `Ch16BuildTier`, `Ch16BuildFloorOnly`, `Ch16BuildRamp`, `Ch16BuildAccentLight`, `Ch16BuildNamedBoss`, `Ch16BuildKhallImage`, `Ch16PlaceStoryNpc`, `Ch16PlaceGhostNpc`, `Ch16BuildDialogue`, `Ch16WireVoiceClips`, `Ch16BuildCompleteCanvas`, the four `Ch16Ensure*Definition` data-asset factories.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 16 and are now silently rebuilding thirteen other chapters.

**A note on the current method shape.** Unlike this document's target state, `BuildChapter16ThroneOfAshes()` today is **one monolithic method** — there is no `BuildBeat0Art`/`BuildBeat0Logic` through `BuildBeat9Art`/`BuildBeat9Logic` split in the shipped code. The class-summary comment documents a ten-entry **BEAT MAP** (0 briefing → 1 descent → 2 duel → 3 leash-break → 4 Soren reveal → 5 into the throne-core → 6 time-loop → 7 forged order → 8 true enemy → 9 climax) purely as *narrative bookkeeping* inside the single method — it is not yet a method boundary. This mirrors exactly where `Chapter1Builder.cs` stood before its own refactor (see the Ch1 companion document): the split described here is the goal, and Appendix A is the current, monolithic, primitive-only reality.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two new ScriptableObjects carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch16Environment.asset` | directional key, ambient mode + color, fog mode/color/density, per-tier floor/ceiling tint pairs, ten accent-light entries, the two Time-Loop fold-phase accent pairs, the throne-core ambience clip reference |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document |

Both are net-new (`Assets/Ronin7/Data/` is where `EnemyDefinition`, `WeaponDefinition`, and `ZoneDefinition` instances already live — including the four Ch16-specific `EnemyDefinition` assets this chapter's own `Ch16Ensure*Definition()` factories create: `Ch16Warden.asset`, `Ch16Samurai4.asset`, `Ch16KhallImage.asset`, `Ch16MaelgornPhase{1,2,3}.asset`). Neither `ChapterEnvironmentProfile` nor `ArtAssetRegistry` exists yet.

Prefab **paths never appear in builder code.** The builder asks the registry for `Rooms.SepulcherTier_Steel`; the registry asset holds the path. This is the whole point of the indirection — art can re-point a prefab without touching a `.cs` file or this document.

**Prefab root is `Assets/Ronin7/Art/Generated/`.** New environment folders are siblings of `Characters3D/`:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (all Ch16 Named cast is here already)
  Rooms/                                    ← new (tier shells, ramps, the throne-core cathedral)
  Props/                                    ← new (wards, cradle, lattice, throne, hatches)
  VFX/                                      ← new (fold accents, liberation burst, seam glow)
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter16ThroneOfAshes()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter16Builder.cs:135`), exactly like every other chapter builder in this codebase. That call does not *delete objects from* the scene — it **discards the entire scene** and opens a fresh empty one. There is nothing left to search for. A naïve `GameObject.Find("[STATIC_ART_DO_NOT_DELETE]")` after `NewScene` will always return `null`.
>
> Making the safe zone real requires **replacing the wipe strategy**, exactly as prescribed for Chapter 1 (see that document's §1.4): `EditorSceneManager.OpenScene(Ch16ScenePath)`, then `DestroyImmediate` each **generated root by name** (`IronSepulcher`, `Game`, `Mission`, the rig, the ten accent lights, dialogue players, reach points, `ChapterOutro`), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist. **Option 1 (in-place `OpenScene` + named-root teardown) is preferred**, reusing the `EnemyArtWirer.cs`/`CrowdArtWirer.cs` idiom already shipping elsewhere in this codebase.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for Chapter 16.** No tier-shell, no ramp, no ward, no cradle, no lattice spar, no throne, no fold-accent VFX. See Appendix B for the full inventory. **Every named-cast character prefab already exists** (Samurai-4, Maelgorn, Khall, The-Hollow-Kings, Ronin-7_Cipher_Soren, Sallow, Echo — all glob-confirmed on disk per the builder's own class-summary comment) — this is the one chapter in the saga where the entire principal cast needs zero fallback.

A builder that instantiates from an empty registry produces **an empty descent shaft** — the first run of the refactored builder would destroy the finale.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Rooms_SepulcherTier_Steel);
if (prefab == null) {
    Debug.LogWarning($"[Ch16] {ArtKey.Rooms_SepulcherTier_Steel} unresolved — primitive fallback.");
    Ch16BuildTier(world, "SpawnTier", topCenter, size, steelFloor, steelCeil);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`) and the identical clause in the Ch1 companion document (§1.5). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). Treat 90 FPS as the ceiling to protect and `QualityBootstrap`'s shipped **72 Hz default** as the floor you are actually shipping against today (`Project/Docs/GraphicsRoadmap-GrittyCyber.md`).
- **This is the largest and most performance-hostile scene in the saga.** Ten stacked tiers plus five ramps, ten accent point lights plus two fold-phase pairs plus a throne-core ambience layer, up to **seven simultaneously-relevant `Enemy`/`Health` rigs** at the busiest moment (three Wardens-B active + Samurai-4 pending is the worst case pre-loop; three sequential Maelgorn phases post-loop, one active at a time), a synthesized `ArmR/Sword/Blade/BladeTip` chain on every one of Samurai-4 / three Khall-images / three Maelgorn phases (seven boss rigs total, each built the same way as the four Ch1 troopers), and `ReverbZonePlacer.AutoTagInteriorVolumes()` auto-tagging every enclosed tier as its own reverb volume. **No baseline `UnityStats` measurement has been recorded for this scene** (unlike Ch1's documented 189 drawCalls / 9,198 tris baseline) — recording one on first successful build is a prerequisite for this chapter shipping, not an optional follow-up.
- **The auto-tagger skips both roofless shells, and nothing fills the gap (flagged).** `AutoTagInteriorVolumes()` only tags a tier with a matching `_Floor`/`_Ceiling` pair (`ReverbZonePlacer.cs`'s own `if (ceiling == null) continue; // open platform, not an enclosed room -- no reverb`); `SepulcherFloor` and `ThroneCore` are `Ch16BuildFloorOnly` shells with no ceiling at all (§2), so they are silently skipped and receive **zero reverb volume** — while every cramped enclosed steel/gold corridor above them gets one. The 92 m throne-core cathedral hosting the entire Maelgorn fight, the liberation, and the saga's closing five dialogue sets is the one space this chapter's acoustic hierarchy most needs to sound monumental, and today it is drier than the vault above it. See §7 for the recommended manual fix.
- Every prefab landing in the registry must be measured against whatever that first baseline turns out to be. A prefab that looks correct and drops the scene below 72 Hz is a regression, not an upgrade — the primitives it replaces are cheap for a reason (shared `TintShared` MaterialPropertyBlock batching, see §3).
- **The Named-cast prefabs are the one place this budget is already spent for real**, not hypothetically: seven boss rigs (Samurai-4, three Khall-images, three Maelgorn phases) all instantiate the *same* high-fidelity Tripo meshes the rest of the chapter is only planning to reach for props. Re-measure the moment any of them is active simultaneously with a Wardens encounter or the Time-Loop's fold-phase lights.

## 2. Chapter spatial map

Chapter 16 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch16_ThroneOfAshes.unity`, laid out as **ten stacked tiers strung along a single, monotonically descending +Z / −Y spiral** — the Iron Sepulcher's tiers step down in Y as they run forward in Z, connected by five walkable ramps, with **zero branching and zero backtracking**: the player spawns at z≈6, y=0 and the story pushes them monotonically toward z≈290, y=−30. This is the same "one continuous corridor, no return route" contract as Chapter 1's four rooms, scaled to ten tiers and roughly 2× the linear extent.

```
 z=6,y=0                                                                                        z=290,y=-30
 SpawnTier ─Ramp A─ UpperTierB ─Ramp B─ MidTierA ─Ramp C─ MidTierB ─Ramp D─ LowerTier ─Ramp E─ SepulcherFloor ─TheBigDescent─ ThroneCore
  (Briefing)         (Wardens A)        (dlg only)        (Wardens B)      (Samurai-4          (Soren reveal,      (no cut)         (loop, Khall,
                                                                             duel + break)        the cradle)                         Maelgorn, seam)
  z[-2,14]  y=0      z[22,38] y=-3      z[46,62] y=-6      z[70,86] y=-9    z[94,126] y=-12      z[142,166] y=-18                     z[198,290] y=-30
  steel, clinical     steel, wards      thinning, gold     memory-gold      near-pure memory,    open platform,                      monumental
                                                            nursery-echo     sourceless light      no walls/ceiling                    cathedral, no
                                                                                                                                        walls/ceiling
```

| Tier | Beat | Footprint (x, z) | Floor top-Y | Character |
|---|---|---|---|---|
| SpawnTier | 0 | x[-6,6], z[-2,14] | 0 | Program fortress-vault, clinical steel |
| RampA | 0→1 | width 12, z[14,22] | 0 → −3 | walkable descent, no forced camera move |
| UpperTierB | 1 | x[-6,6], z[22,38] | −3 | still steel; conditioning wards; Wardens A |
| RampB | 1 | width 12, z[38,46] | −3 → −6 | — |
| MidTierA | 1 | x[-6,6], z[46,62] | −6 | thinning into memory-space; dialogue-only, no combat |
| RampC | 1 | width 12, z[62,70] | −6 → −9 | — |
| MidTierB | 1 | x[-6,6], z[70,86] | −9 | nursery-echo memory-space; Wardens B |
| RampD | 1 | width 12, z[86,94] | −9 → −12 | — |
| LowerTier | 1–3 | x[-6,6], z[94,126] | −12 | near-pure memory-space, sourceless light; Samurai-4 duel *and* leash-break resolve here (one location) |
| RampE | 3→4 | width 12, z[126,142] | −12 → −18 | — |
| SepulcherFloor | 4 | x[-7,7], z[142,166] | −18 | open platform, no walls/ceiling — "the vault within"; the memory-cradle |
| TheBigDescent | 4→5 | width 14, z[166,198] | −18 → −30 | one long ramp, **no scene-cut** — the floor of the self opens onto the floor of the cage |
| ThroneCore | 5–9 | x[-16,16], z[198,290] | −30 | monumental open cathedral, no walls/ceiling — the scale-flip |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, applied only to the six *enclosed* tiers (SpawnTier, UpperTierB, MidTierA, MidTierB, LowerTier — each gets a floor+ceiling pair via `Ch16BuildTier`). `SepulcherFloor` and `ThroneCore` are built via `Ch16BuildFloorOnly` — **floor only, no walls, no ceiling** — because "the architecture stops being architecture entirely" at both the memory-floor and the throne-core, per the source script's own framing.

**Ramp grade (VR comfort note, not yet validated in headset).** Every ramp (`RampA`–`RampE`, `TheBigDescent`) resolves to the same sustained grade: rise/run of 3/8 (RampA–D), 6/16 (RampE), 12/32 (TheBigDescent) all reduce to atan(3/8) ≈ **20.6°**. `TheBigDescent` alone is 32 m of continuous 20.6° downhill with deliberately no scene-cut (§3 — "the floor of the self opens onto the floor of the cage"). Sustained downhill continuous locomotion is a known vection/discomfort risk even with a comfort vignette tuned for snap-turn/thrust; whether the vignette should also engage across sustained descent, not only on turn/thrust, is an open question this document does not resolve. Flag `TheBigDescent` specifically — and this chapter generally, given it has the most continuous ramp-walking in the saga — for in-headset comfort validation before shipping.

**These footprints and Y-values are load-bearing and survive the refactor unchanged.** A tier-shell prefab must fit its footprint and top-surface Y exactly; the spatial map is the contract, not the prefab's convenience.

**No sliding doors exist in this chapter.** Unlike Chapter 1's three lock-gated `SlidingDoor_Standard` instances, Chapter 16 gates progress entirely through `ReachTrigger` (walk into a radius) and `DefeatEnemies` (clear an encounter) mission steps — every tier transition is an open ramp, and the only two `Trigger`-activated *reveals* that gate story beats (`EnterFoldRelay`/`ExitFoldRelay` for the Time-Loop, and the cast-reveal triggers for Khall/Maelgorn/Hollow-Kings/Sallow) are **inactive-GameObject flips**, not lock states on a physical door. If a future pass wants a literal door prop anywhere in this descent (e.g. a sealed vault hatch dressing the SpawnTier→UpperTierB transition), it is new scope, not a gap in the current design.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` plus `AttachPlayerAbilities(rig, refs)` — the full five-ability chain, all earned by this point in the saga. `ZoneBounds` is set to **center (0, −15, 150), radius 200** — one bounding sphere loosely enclosing the entire ten-tier descent. The katana rides from the start at (2, 1, 4), Euler(−90, 0, 0), `Ch16EchoBladePrefab` — "deep into Act IV, no rack-wake beat, matching Ch9–13's 'cost, not initiation' precedent" (builder comment).

## 3. Global environment & backdrop

**The descent-as-self (the chapter's defining structure):** per the dialogue script's SETTING block, the Iron Sepulcher "is unlike any cave the saga has walked... a leash made of stone and light, built to keep a man out of himself." The upper tiers read as a buried fortress-vault — clinical Program steel fused into black rock, sealed conditioning-wards, institutional cold. The deeper the player descends, the more the architecture stops being architecture: walls thin into recollection, corridors run with sourceless light, rooms begin to be rooms Soren has stood in before and was made to forget. The builder executes this directly as a **three-stage color/lighting gradient down the descent**, not a narrative aside — see §3.1 and §6.

**The two-in-one structure:** the Sepulcher does not sit above the Concord Engine's throne-core — it *is* the Engine's spine, "the same structure, top to bottom" (Heris, Beat 0). This is why `TheBigDescent` ramp (§2) carries the player from `SepulcherFloor` straight into `ThroneCore` with **no scene-cut, no loading transition, no teleport** — the builder's own comment is explicit that this must be "one long ramp... never a forced camera motion." The player must arrive at this truth in their own legs, walking continuously downward, before anyone says it aloud.

**The scale-flip:** every enclosed tier from SpawnTier through LowerTier is built at the same 12 m corridor half-width (`Ch16CorridorHalfWidth` = 6 m). `SepulcherFloor` widens slightly to 14 m (`Ch16FloorHalfWidth` = 7 m) and drops its walls entirely. `ThroneCore` then jumps to a **32 m half-width** (`Ch16ThroneCoreHalfWidth` = 16 m) — more than double every tier above it — reading as the "monumental open cathedral... the lattice of every kept shadow-AI strung into one galaxy-sized throat" the moment the player crosses `TheBigDescent`.

**Temperature by implication (a free sensory read, no new assets).** The chapter's lighting and recommended ambience beds already carry an unstated cold→hot arc — `steel_vault_hum`'s "cold vault/HVAC" read up top (§7), the steel-blue accents of SpawnTier/UpperTierB, warming through memory-gold, into Maelgorn's furnace-orange `ThroneLight` and the throne-core's `throne_rumble` bed (§4 Beat 5.f) — present in color and audio but never named as a felt descent from institutional cold into furnace heat. Stating it explicitly costs nothing and gives `ForgedThrone`'s furnace-orange a sensory, not just visual, justification.

**The sourceless light and the sourceless tone are the same motif in two channels.** SETTING (lines 33–34) names "corridors [that] run with light that is not lit by anything" — the memory-gold accent read (§6) is the visual channel; `memory_tone.wav`'s recommended "distinct sourceless memory-space tone bed" (§7) is the audio channel. Light with no fixture, tone with no emitter: naming the pairing explicitly gives the recommended audio bed the same thematic justification this section already gives the cold→hot arc above.

**Program's-finest, self-integration, and liberation are the chapter's three environmental "events":** Samurai-4 standing in sourceless light on the widened LowerTier (the boss the descent has been building toward, §4 Beat 2); the memory-cradle opening on the open `SepulcherFloor` platform (the REVEAL, §4 Beat 4); and the Engine-break liberation burst of freed-shadow point lights around the seam on `ThroneCore` (the climax, §4 Beat 9). Everything else is texture in service of getting the player to those three places believing the descent that carried them there.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter16Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch16Environment.asset`. Its schema mirrors Ch1's `ChapterEnvironmentProfile` (see that document's §3.1) with two chapter-specific additions:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` — global baseline |
| `fogPerZone[]` | `{ tierName, fogDensity, fogColorTint }` | overrides the global `fogDensity`/`fogColor` per tier group: the current 0.012 baseline stays for the six *enclosed* steel tiers only (SpawnTier, UpperTierB); thinner and warmer through the memory-gold tiers (MidTierA/B, LowerTier, SepulcherFloor); thinner still and cooler through `ThroneCore` so the 72 m sightline to `ForgedThrone` (§4 Beat 5.c) reads as monumental, not murk |
| `tierPalettes[]` | `{ tierName, floorTint, ceilingTint }` | every `Ch16BuildTier`/`Ch16BuildFloorOnly` call — **three palettes**, not one: steel (Spawn/UpperTierB), memory-gold (MidTierA/MidTierB/LowerTier, with LowerTier at ×1.15 brightness), throne-violet (SepulcherFloor at ×1.3 gold, ThroneCore its own near-black violet) |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | ten entries, one per tier plus the throne itself — `BuildAccentPointLight` equivalents |
| `loopFoldAccents[]` | `{ normalPair, foldPair }` | `TimeLoopController`'s two `phaseGroups` — the only per-chapter *combat-mechanic* lighting state carried in the profile |

`behaviour` is the same enum as Ch1 — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("ThroneCoreLight0", seed: 151f)` and `AddAmbientPulse("SepulcherFloorLight", periodSeconds: 7.8f)` calls with data. Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass.

**The single directional key light is only physically live on two of the ten tiers, and nothing states this.** `keyLightColor`/`keyLightIntensity`/`keyLightRotation` is one global light (Appendix A.1: color (0.75, 0.8, 0.9), intensity 0.55, Euler(50, −30, 0)), but the six enclosed tiers (§2) each get a `Ch16BuildTier` ceiling that occludes it, while `SepulcherFloor` and `ThroneCore` are `Ch16BuildFloorOnly` — floor only, no ceiling (§2) — so the directional key is the only light physically reaching them from outside the accent-light set. Whether a cold directional rake cutting across the open memory-floor and the throne-core cathedral is an intended part of those two tiers' look, or an unexamined side effect of the roofless-shell choice, is not stated anywhere in this schema or in §6's progression table. Treat it as intended — the roofless architecture catching a cold external light is a fitting visual for "the architecture stops being architecture" — but a prefab author replacing `SepulcherFloor_MemorySpace` or `ThroneCore_Cathedral` with a roofed variant would silently lose it, so budget for it explicitly rather than rediscovering it by accident.

**Material / tint palette:** exactly as Ch1, every tier shell, ramp, and prop today is a cheap primitive tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than a unique material — this keeps the greybox scene's draw-call count survivable at ten-tier scale and keeps regeneration cheap during retuning. **Prefabs replacing them must carry their own materials and will not batch this way.** That is the perf cost §1.6's budget clause exists to police, doubly so here given the scene's size.

## 4. Per-beat scene spec

The chapter plays as ten beats along the descending spiral (Beat 0 through Beat 9), mirroring the dialogue script's own SEGMENT 0–9 numbering and the story-beat file's Scene 1–10 numbering one-to-one. Each beat is documented with the same a–f structure used in the Ch1 companion document.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the Y-invariant.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the builder already instantiates a real, non-primitive asset today.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes or hardcoded sizes for the spawn tier. You must read from the centralized `ArtAssetRegistry` ScriptableObject for the tier shell and the katana placement. Create separate methods: **`BuildBeat0Art()`** for the SpawnTier shell + `SpawnLight`, and **`BuildBeat0Logic()`** for the player rig, the katana, and the briefing dialogue.

#### a. Narrative purpose & emotional target

This is the saga's last Hub briefing, and the only one that escorts the protagonist to *himself* before it escorts him to the end of the war. Per the dialogue script's production note, it must read as a full-roster commitment, not an exposition dump: Heris lands the structural gut-punch in one breath (the Sepulcher does not sit above the Engine — it *is* the Engine's spine); Coral Vex marks that she has made this exact descent before, alone, and promises he will not have to hide the way she did; Mera Voss plants the Samurai-4 seed ("their finest hunter... they don't have a hitch you can talk to. Or they didn't, until him"); Cassie-04 and Sable seed the Ladder-E liberation payoff from opposite angles (the ledger vs. the felt wire); Vess and Gryph frame the contagion theme and the deep-places law. Kessler reduces the entire finale to one sentence — "Not the Engine, not the throne... You. Coming back up that lift" — and Ronin-7's reply folds the whole briefing into a single heading, ending on the saga's thesis stated plainly: **"As free people. Not as anyone's weapon."** The beat's emotional hinge is private and comes last: Echo, silent through the whole briefing, finally tells Cipher — alone — that there is a part of him "older than me... the piece even I couldn't carry," and sends him down.

Every other Ch16 beat is spoken through the speaker label **Ronin-7**; the switch to **Soren** does not happen until Beat 4. Ch16Lines' own class-summary comment is explicit that this labeling discipline must hold.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** `BuildRig(addLocomotion: true)` + `EchoPresence` + `AttachPlayerAbilities` (all five abilities already granted — no unlock gating this chapter) + `ZoneBounds` center (0, −15, 150) radius 200 (authored once, chapter-wide). The player does not travel in Beat 0 — the whole beat plays out standing on SpawnTier, ending with the player walking north toward `UpperTierReachPoint`.
- **Katana:** rides from the start, already equipped — `BuildSword((2, 1, 4), Euler(−90,0,0), weapon, Ch16EchoBladePrefab)`. No rack-wake beat; this is a deep-Act-IV chapter, matching the "cost, not initiation" precedent from Ch9–13.
- **No enemies, no NPCs, no reach gate within Beat 0 itself** — the crew referenced in the dialogue (Heris, Iris, Coral Vex, Cassie-04, Sable, Mera Voss, Vess, Gryph, Kessler) are all **voice-only** this chapter (per the class-summary's "CREW-PRESENCE DECISION," mirroring Ch13's convention) — none of them receives a physical placement anywhere in the scene.
- **This is the one beat where the location fold and the CREW-PRESENCE DECISION compound each other, and canon makes the physical roster the point.** The screenplay's Beat 0 is "INT. THE CAIRN — WAR-ROOM HOLO-TABLE," staging all fourteen named speakers physically ringed around the holo (dialogue line 221), with its own production note insisting on "full-roster commitment, not an exposition dump" (line 223). §9 already flags that this beat folds onto SpawnTier instead of a `Ch16_Prologue` scene, and Beat 0.c already flags that the room built there is bare (`DescentMapHolo`); neither of those notes connects to the fact that the *voice-only* crew is the specific thing canon does not do for this beat, unlike the descent's briefing-over-comm convention which canon supports. Own this as a conscious tradeoff: folding the location saved a scene transition, but it also quietly dropped the one beat where the full-roster staging was the emotional point, not incidental color. A few background crew figures placed on SpawnTier (even non-speaking) would restore the visual without reversing the location-fold decision.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4), set `ch16_beat0_briefing`, 12 lines.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Beat0: The Cairn (the briefing, the full crew commits)` — plays the full 12-line briefing |
| 1 | ReachTrigger | `ReachTrigger: The Upper Tier` — gates on distance to `UpperTierReachPoint` (0, 0, 14), radius 5 |

**What changes during the beat:** nothing in the set dressing — SpawnTier is static. The only state change is the player walking from the dialogue anchor at z=4 to the reach point at z=14, closing Beat 0 and opening RampA into Beat 1.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| SpawnTier shell (floor + ceiling, 12×16) | top-center (0, 0, 6) | `Rooms.SepulcherTier_Steel` | `…/Art/Generated/Rooms/SepulcherTier_Steel.prefab` | **MISSING** |
| `SpawnTier_WallW` | (−6, 1.8, 6) | `Rooms.SepulcherTier_Steel` *(wall variant)* | same prefab family | **MISSING** |
| `SpawnTier_WallE` | (6, 1.8, 6) | `Rooms.SepulcherTier_Steel` *(wall variant)* | same prefab family | **MISSING** |
| `SpawnTier_WallS` | (0, 1.8, −2) | `Rooms.SepulcherTier_Steel` *(wall variant)* | same prefab family | **MISSING** |
| `DescentLock` (decorative lift-cage the player stepped off) | (0, 0.5, −2), mounted on `SpawnTier_WallS` | `Props.DescentLock` | `…/Art/Generated/Props/DescentLock.prefab` | **MISSING** |
| `DescentMapHolo` (optional — the ten-tier shaft holo Heris narrates over) | (0, 1.4, 8) | `Props.DescentMapHolo` | `…/Art/Generated/Props/DescentMapHolo.prefab` | **MISSING** |
| `RampA` | mid (0, −1.5, 18), width 12 | `Rooms.DescentRamp_Standard` | `…/Art/Generated/Rooms/DescentRamp_Standard.prefab` | **MISSING** |
| Katana "Echo" | (2, 1, 4), Euler(−90, 0, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |

**Notes on the transition.** `SpawnTier_WallS` is the only fully solid wall in this room — the north end (z=14) is open onto RampA, matching the descent's one-way, no-backtrack contract. The tier's cold "clinical Program steel" read (per the SETTING block) comes entirely from `steelFloor`/`steelCeil` tint literals (Appendix A.1) today; a shell prefab replacing it must preserve that cold-blue-white palette, distinct from every tier below it.

**The lift, given a diegetic anchor.** The finale's single most-referenced motif is "the lift" — Kessler's Beat 0 thesis is "You. Coming back up that lift" (line 249), the screenplay transition has Ronin-7 stepping off it onto the first tier (line 287), Kessler patches in "from the top of the lift" for the very last homecoming beat (line 875), and Echo's final line pays off "the whole way back up" (line 877) — yet nothing in the scene has ever physically represented it; `SpawnTier_WallS` is otherwise blank. `Props.DescentLock` is recommended as a sealed lift-cage/descent-lock dressing that wall, purely decorative and non-functional (the one-way, no-backtrack descent contract is preserved — it is not a usable elevator), so the cast's recurring "way back up" has something concrete to point at.

**The bare briefing room, given something to be about.** At ≈396 s, Beat 0's briefing is by a wide margin the chapter's longest dialogue set (§4 Beat 0.e), yet SpawnTier carries no set dressing at all today — the whole beat plays to an empty steel box. The screenplay opens on a holo that "runs from a vault-mouth all the way down into the Concord Engine," with Heris marking the memory-core and the seam (lines 62–65): the visual spine of the entire descent the player is about to walk. `Props.DescentMapHolo` is recommended, not required, as a ten-tier shaft projection with the two marked points, giving the longest beat in the chapter a spatial subject and pre-visualizing the descent for the player before they take a single step. Optional — the existing decision to fold the briefing onto SpawnTier rather than a `Ch16_Prologue` scene (§9) is unaffected either way.

#### d. Combat

None. Beat 0 is dialogue-only.

#### e. Dialogue / VO

`Dialogue_Beat0_Briefing`, set `ch16_beat0_briefing`, position (0, 1, 4), **12 lines, ≈396 s total** — by a wide margin the longest single dialogue set in the chapter, matching its job as the finale's full-roster commit beat:

| Speaker | Line (abridged) | sec |
|---|---|---|
| Dr. Heris | "This is the last map I have for you... It does not sit above the Concord Engine. It is the Engine's spine... Strike it right and the whole machine comes apart, and every shadow it is strung through goes free." | 56 |
| Iris | "I ran the model three times... it just keeps falling. No break... I've got nothing." | 19 |
| Coral Vex | "Getting yourself back is the hardest climb there is, and I made it before any of you... We will be right behind him the whole way down." | 34 |
| Cassie-04 | "I'm a ledger... You break the Engine, you don't just kill a weapon. You unmake the thing that's holding thousands of them open." | 40 |
| Sable | "I'm wired close enough to feel them breathe... I would like, just once, to be the hand that sets the others loose instead of the wire that holds them." | 35 |
| Mera Voss | "They'll spend their best. Their finest hunter... they don't have a hitch you can talk to. Or they didn't, until him." | 36 |
| Vess | "Whatever they send down that hole to stop you, Cipher, it's just one more killer who was never asked... I almost didn't." | 25 |
| Gryph | "You don't go down alone, and you don't go down for nothing... Nobody gets down the shaft behind you that doesn't come through me first." | 24 |
| Kessler | "All right. Go down. Get who you are... You. Coming back up that lift. That's an order, from the man who owns the rig." | 35 |
| Ronin-7 | "Then I'll come back up... I'm going down to get the rest of me, and then we end it. As free people. Not as anyone's weapon." | 41 |
| Echo | (to Cipher alone) "This part's just for you... That's what's at the bottom of that vault. The piece even I couldn't carry. Go down and get it." | 44 |
| Ronin-7 | "Then let's go get it. Both of us. You carried it long enough. Take me down, Echo." | 7 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — this is the finale's own opening dialogue beat, carried entirely by VO performance and lighting.
- `SpawnLight` reads as the coldest, most neutral accent in the chapter — no pulse, no flicker, the plain institutional light of a launch point, contrasting deliberately with every accent below it warming toward memory-gold and then furnace-orange.
- (Recommended, not yet wired) `SFX/steel_vault_hum.wav` under SpawnTier via `BuildAmbienceLayer` — a cold vault/HVAC bed matching the institutional-cold read, giving the upper tiers the same sonic layering `ThroneCoreAmbience` already gives the climax. See §7.
- Comfort vignette is inert through the stationary dialogue and engages normally the moment the player begins walking toward `UpperTierReachPoint`.

---

### Beat 1 — The Iron Sepulcher (Descent / Traversal — Peeling the Leash)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all tier shells, ramps, and ward props from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (UpperTierB / MidTierA / MidTierB shells, `RampB`/`RampC`/`RampD`, `ConditioningWard0/1`, `MemoryDoorway`, `SealedCradleEcho0/1`) and **`BuildBeat1Logic()`** (the two Wardens encounters, the three reach points, the two descent-narration dialogue sets, Echo's Samurai-4 warning).

#### a. Narrative purpose & emotional target

The chapter's traversal segment, and its entire job — per the dialogue script's own production note — is to make the player *feel* the conditioning peel, tier by tier, in the controls, before anyone narrates it. The upper tiers (UpperTierB) read as Program fortress-vault: steel, wards, institutional cold. The mid tiers (MidTierA, MidTierB) begin to thin into memory-space: sourceless light, a doorway that is "suddenly the inside of a transport he was deployed from," a ward-room that becomes "a clean white nursery like the ones in Heris's lab." Echo grows quieter and closer with every level; Coral Vex's comm read is load-bearing because she is the one person alive who has made this exact climb. The beat closes on the reveal of Samurai-4 herself, standing in sourceless light at the far end of LowerTier — Echo's urgent private warning ("that's not a warden... they sent the best one they have left") is the hinge into Beat 2.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** no scripted path — walk + snap-turn through RampA → UpperTierB → RampB → MidTierA → RampC → MidTierB → RampD → arriving at LowerTier. All five abilities (§1.1) are available and expected to be cued against the two Wardens encounters.
- **Wardens A** (three, UpperTierB): built inactive, `Ch16Warden` `EnemyDefinition` (60 HP, 10 dmg, moveSpeed 1.4, attackCooldown 0.9). Flipped active as a group by the `DefeatEnemies` step's own setup, mirroring every earlier chapter's mook convention (no separate visible `Trigger` step).
- **Wardens B** (four, MidTierB): same definition, same activation idiom.
- **Reach points:** `UpperTierReachPoint` (0, 0, 14) r5 *(closes Beat 0)*; `MidTierBReachPoint` (0, −6, 62) r5; `LowerTierReachPoint` (0, −9, 86) r5. **Naming note:** `MidTierBReachPoint` sits at MidTierA's far edge (z[46,62], y=−6), not inside MidTierB's own footprint (z[70,86], y=−9) — the name reads as a misnomer worth flagging; it gates descent *toward* MidTierB, it is not a point within it.
- **Dialogue anchors:** `Dialogue_Beat1_DescentUpper` at (0, −3, 24), set `ch16_beat1_descent_upper` — plays over the RampA→UpperTierB transition; `Dialogue_Beat1_DescentLower` at (0, −9, 88), set `ch16_beat1_descent_lower` — plays the instant Samurai-4 comes into view at the head of LowerTier.
- **Samurai-4's own placement** (spawn (0, −12, 106), inactive, `DuelYield`+`LeashBreakController` wiring) is instantiated here in `BuildBeat1Art()`'s scope but not activated until Beat 2's Trigger step — see that beat for the full cast/combat treatment.
- **Reveal-timing gap (flagged, not yet resolved in the builder).** Because Samurai-4's whole GameObject is built inactive here and only flipped active by Beat 2's step-9 Trigger, both `Dialogue_Beat1_DescentLower` (step 7 — Echo's warning "Look at her... she's got a shadow of her own riding her eyes," entirely a visual read) and Beat 2's pre-fight dialogue (step 8) currently play to an **empty tier**. The canon stage direction (dialogue line 312) has her "standing in the sourceless light at the center of it, blade drawn, half-masked, pristine, waiting" *before* Echo says look at her. Recommend a **passive-then-hostile split**: instantiate her mesh active (visible, standing, non-threatening — `Enemy` component present but disabled, no `CapsuleCollider` engaged) from the moment the player crests LowerTier around step 6/7, so she is on screen for both dialogue beats; step 9's Trigger then flips her from inert to a live `Enemy`, rather than spawning her from nothing after two dialogue beats have already discussed her.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 2 | Dialogue | `Beat1: The Descent (upper tiers peel)` — `ch16_beat1_descent_upper` |
| 3 | DefeatEnemies | `Sepulcher Wardens A` — waits for all three Warden-A `Health` components to reach zero |
| 4 | ReachTrigger | `Mid Tier B` — (0, −6, 62), r5 |
| 5 | DefeatEnemies | `Sepulcher Wardens B` — waits for all four Warden-B `Health` components |
| 6 | ReachTrigger | `The Lower Tier` — (0, −9, 86), r5 |
| 7 | Dialogue | `Samurai-4 Named (Echo's warning)` — `ch16_beat1_descent_lower` |

**What changes during the beat:** the tint gradient itself — steel (UpperTierB) fading toward gold (MidTierA/MidTierB) — is the only continuous visual change; the two Wardens groups are the beat's only discrete state flips (inactive → active on `DefeatEnemies` setup → cleared).

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| UpperTierB shell (12×16) | top-center (0, −3, 30) | `Rooms.SepulcherTier_Steel` | `…/Art/Generated/Rooms/SepulcherTier_Steel.prefab` | **MISSING** |
| `ConditioningWard0` (decorative wall fixture — the sealed conditioning-ward of the SETTING block, distinct from the *Warden* enemy mooks that fight on this tier) | (−4.5, −2.5, 28) | `Props.ConditioningWard` | `…/Art/Generated/Props/ConditioningWard.prefab` | **MISSING** |
| `ConditioningWard1` | (4.5, −2.5, 32) | `Props.ConditioningWard` | `…/Art/Generated/Props/ConditioningWard.prefab` | **MISSING** |
| `RampB` | mid (0, −4.5, 42), width 12 | `Rooms.DescentRamp_Standard` | `…/Art/Generated/Rooms/DescentRamp_Standard.prefab` | **MISSING** |
| MidTierA shell (12×16) | top-center (0, −6, 54) | `Rooms.SepulcherTier_Gold` | `…/Art/Generated/Rooms/SepulcherTier_Gold.prefab` | **MISSING** |
| `MemoryDoorway` | (0, −5.4, 58) | `Props.MemoryDoorway` | `…/Art/Generated/Props/MemoryDoorway.prefab` | **MISSING** |
| `RampC` | mid (0, −7.5, 66), width 12 | `Rooms.DescentRamp_Standard` | same prefab | **MISSING** |
| MidTierB shell (12×16) | top-center (0, −9, 78) | `Rooms.SepulcherTier_Gold` | same prefab | **MISSING** |
| `SealedCradleEcho0` | (−5, −8.4, 76) | `Props.SealedCradleEcho` | `…/Art/Generated/Props/SealedCradleEcho.prefab` | **MISSING** |
| `SealedCradleEcho1` | (5, −8.4, 80) | `Props.SealedCradleEcho` | `…/Art/Generated/Props/SealedCradleEcho.prefab` | **MISSING** |
| `NurseryEcho` | (0, −8.4, 78) | `Props.NurseryEcho` | `…/Art/Generated/Props/NurseryEcho.prefab` | **MISSING** |
| `MemoryFaces` (wall treatment, MidTierA/MidTierB) | applied to `MidTierA_WallW/E`, `MidTierB_WallW/E` | `Vfx.MemoryFaces` | `…/Art/Generated/VFX/MemoryFaces.prefab` | **MISSING** |
| `RampD` | mid (0, −10.5, 90), width 12 | `Rooms.DescentRamp_Standard` | same prefab | **MISSING** |
| Warden ×3 (Wardens A) | (−3, −3, 27), (3, −3, 29), (0, −3, 33) | `Enemies.SepulcherWarden` | `…/Art/Generated/Characters3D/Enemies/SepulcherWarden.prefab` | **MISSING** |
| Warden ×4 (Wardens B) | (−3, −9, 75), (3, −9, 77), (0, −9, 81), (−2, −9, 82) | `Enemies.SepulcherWarden` | same prefab | **MISSING** |
| `UpperTierLight0/1` (accent) | (−3, 2.2, 26), (3, 2.2, 32) | — | `ChapterEnvironmentProfile.accentLights["UpperTier"]` | profile |
| `MidTierLight0/1` (accent) | (−3, −5.8, 50), (3, −8.8, 78) | — | `ChapterEnvironmentProfile.accentLights["MidTier"]` — memory-gold seeping in | profile |

**Staging constraint the prefabs must respect.** `Rooms.SepulcherTier_Gold` must visibly warm relative to `Rooms.SepulcherTier_Steel` — this is the single most legible "the conditioning is peeling" cue in the whole traversal (per the SETTING block's own framing) and must not be flattened into one reused shell with only a tint override on the accent light; the base floor/ceiling tints themselves shift (Appendix A.1).

**Set dressing for the two named memory-transformations.** Beat 1.a and the canon stage direction (dialogue line 312) name three specific memory transformations beyond the doorway: a transport-doorway (covered by `MemoryDoorway`), a "clean white nursery like the ones in Heris's lab," and "a tier where the walls are running with light and faces he cannot place." Only the doorway currently has a prop, even though MidTierB is already labeled "nursery-echo" on the §2 map. `Props.NurseryEcho` gives that label an actual callback to the Heris-lab nursery — thematically load-bearing, since it's where the operatives were made — and `Vfx.MemoryFaces` is a wall treatment for the near-pure memory tiers (MidTierA onward) so "architecture peeling into recollection" is *shown*, not just tinted.

**`SealedCradleEcho0/1` — unlike every other prop this beat, this pair has no specific canon line behind it.** `MemoryDoorway`, `NurseryEcho`, and `MemoryFaces` each trace to a named stage direction or beat label above; `SealedCradleEcho0/1` (MidTierB, flanking `NurseryEcho`) does not. The closest available anchor is SETTING's general "sealed doors and conditioning-wards" (dialogue line 31), which fits loosely, and the name itself pre-echoes Beat 4's `MemoryCradle` — a plausible read is that these are sealed, not-yet-open cradle-echoes foreshadowing the real one two tiers down, but that's an inference, not a source line. Recording it here so the pair isn't mistaken for canon-anchored set dressing the way its neighbors are: treat it as additive foreshadowing dressing unless a more specific source line turns up.

**The Wardens are the chapter's only combatant with no art-direction note, and that gap is thematically costly.** Samurai-4 (§4 Beat 2.c) gets a canon-specific presentation contract — half-mask, twin blade, nape killswitch-scar — and Maelgorn (§4 Beat 8.c) gets his own — eyeless obsidian, furnace-orange fault-veining, shadow-smoke edges — but `Enemies.SepulcherWarden` (Appendix B's one enemy-art gap) gets nothing beyond "plain capsule mooks... deliberately weaker" (§4 Beat 1.d). Canon calls them "sepulcher wardens" (dialogue line 99) and ties them to "sealed doors and conditioning-wards and the cold institutional cleanliness the make was [designed for]" (SETTING line 31). The Wardens should read as **impersonal, faceless institutional conditioning-constructs — hitch-less, issued, not people** — precisely so the contrast with Samurai-4 lands: Echo's whole thesis is that Samurai-4 is "what you were," a person with a hitch the player can reach, while the Wardens exist to be the *un*-reachable version, mowed down without mercy-weight. This facelessness is thematically load-bearing, not incidental, and the art brief for `Enemies.SepulcherWarden` should say so explicitly.

#### d. Combat

Two `DefeatEnemies` encounters, both plain melee mooks built via `BuildEnemy` off a shared `Ch16Warden` definition (60 HP / 10 dmg — deliberately weaker than every other combat encounter this chapter, since the traversal's job is rising pressure, not a set-piece). Player damage is `BladeDamager`'s existing EMA swing-speed model (reuse, don't reinvent). No haptics/audio departures from the standard combat feedback loop described in §1.1.

#### e. Dialogue / VO

`Dialogue_Beat1_DescentUpper` (`ch16_beat1_descent_upper`, (0, −3, 24), **2 lines, ≈83 s**): Echo names the descent-as-leash conceit directly ("Every tier we drop, something comes off you... They built this place to make a man turn around"); Coral Vex over comm warns him the memory-space tiers are "the only guard that knows your name."

`Dialogue_Beat1_DescentLower` (`ch16_beat1_descent_lower`, (0, −9, 88), **2 lines, ≈85 s**): Morrigan over comm confirms the architecture is "losing coherence... not damage. Design... the vault is made of it, the deeper it goes." Echo then delivers the beat's hinge line, revealing Samurai-4 by name and framing the coming duel's non-lethal thesis in advance: *"She's what you were. Same make as you, current issue, cleaner leash, same hitch buried in her somewhere whether she knows it or not... you do not have to kill her."*

> **AUDIT FIX #6 note (already applied in `Chapter16Lines.cs`):** the source script originally called Samurai-4 a "newer make" than Ronin-7, implying an undocumented fifth program; canon makes Ronin-7 the current/newest make (Knight → Ninja → Wraith → Ronin). The shipped line reads "Same make as you, current issue, cleaner leash" instead. Do not reintroduce "newer make" phrasing if this dialogue is ever re-touched.

**Split-membership caveat.** The dialogue script's descent block (lines 297–317) is one continuous run of four speaker turns — Echo, Coral Vex (comm), Morrigan (comm), Echo — with no marked scene boundary between them. The `ch16_beat1_descent_upper` / `ch16_beat1_descent_lower` split above (Echo+Coral Vex vs. Morrigan+Echo) is authored directly in `Chapter16Lines.cs` and was not independently re-verified line-by-line against the source script for this document — the same "inferred, confirm against the live `Chapter16Lines.cs` methods" hedge already applied to Beat 6.e and Beat 9.e (§4 Beat 6.e, Beat 9.e) applies here too, for consistency, before locking a VO batch.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the two Wardens fights read entirely through `Haptics`/`AudioDirector`/`CombatFeedbackController`, same as every other combat encounter in the saga.
- The accent lights' warming progression (`UpperTierLight0/1` cool blue-white → `MidTierLight0/1` memory-gold) is the beat's primary "juice," carried by lighting rather than any scripted VFX event.
- (Recommended, not yet wired) crossfade `SFX/steel_vault_hum.wav` into `SFX/memory_tone.wav` across UpperTierB→MidTierA→MidTierB→LowerTier — the audio equivalent of the tint gradient this beat already carries visually. See §7.
- Comfort vignette engages normally across the three ramp descents — this is the beat with the *most* snap-turn/vignette activity in the chapter, given the sustained continuous walking and the two mid-descent fights.

---

### Beat 2 — Samurai-4 (The Duel Becomes a Conversation)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives. Read the LowerTier shell from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (LowerTier shell, Samurai-4's mesh instantiation + synthesized `ArmR/Sword/Blade/BladeTip` rig) and **`BuildBeat2Logic()`** (the `DuelYield` component, its `Trigger`+`Prompt` step pair, the pre-fight dialogue).
> **`DuelYield` ends the duel on a YIELD only, never a kill.** `yieldThreshold` is set to 0.22 — Samurai-4's `Health` never reaches zero in this encounter; do not wire a `DefeatEnemies` step here.

#### a. Narrative purpose & emotional target

The definitive non-lethal duel of the saga, and per the production note it must read as **a conversation Cipher keeps refusing to end**, not a boss fight interrupted by cutscenes. Samurai-4 is "robotic certainty cracking into doubt" across the whole exchange, not a sudden turn — her denial ("I do not break") comes a half-step too fast, and Echo catches the tell in real time. The canon lines are load-bearing and must land at or near verbatim: her "Stand down or I complete the order," his "You feel it. The hitch before the kill... It is the only honest thing they left in you," her fear laid bare ("If I let go of the order I have nothing... You are offering me a fall with no bottom"), and his answer — delivered with his blade physically lowered first, in the middle of a fight he is winning — "You will have a choice. That is not nothing. That is everything... Take it, or take my head." Mercy as rebellion, applied for the first time in the saga to the protagonist's own executioner.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Samurai-4:** boss built via `Ch16BuildNamedBoss(Ch16Samurai4Prefab, (0, −12, 106), "Samurai-4", samurai4Def, playerHealth)` — instantiates the real Named mesh, synthesizes an `ArmR/Sword/Blade/BladeTip` chain (mirrors Ch9BuildVane/Ch11BuildNamedBoss's "boss from a Named-mesh prefab" idiom), adds a `CapsuleCollider` (center (0,1.1,0), height 2.4, radius 0.5), and wires `Enemy` against a dedicated 260 HP / 14 dmg `EnemyDefinition` — high enough that the 0.22 yield threshold never crosses to death in one hit. Faces −Z (Euler(0,180,0), toward the player's approach). Also carries a `StoryNpc` (`displayName = "Samurai-4"`). Built **inactive**; activated by a `Trigger` step, not by proximity. **Per Beat 1.b's reveal-timing note, this whole-object inactive/active flip is the thing recommended for splitting** — her mesh visible from Beat 1's approach, only her `Enemy`/combat readiness withheld until step 9.
- **`DuelYield`** on the Samurai-4 GameObject: `opponent` = her own `Health`; `disableOnYield` = her `Enemy` component; `sword` = the player's `Grabbable` katana; `yieldThreshold` 0.22; `autoAcceptSeconds` 30. `onAccepted` is wired via `UnityEventTools.AddPersistentListener` to `missionDirector.AdvanceFromPrompt` — the duel resolves the mission's null-`Prompt` step only on a yield, never a kill.
- **`Samurai4_LeashBreak`**, a child object carrying `LeashBreakController`, built inactive — see Beat 3 for its own activation and resolution (a deliberately *separate* FSM/beat from the duel's yield, per the builder's class-summary comment: the physical duel resolves on mercy first, then her leash strains and snaps afterward).
- **Dialogue anchor:** `Dialogue_Beat2_Duel` at (0, −12, 100), set `ch16_beat2_duel`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 8 | Dialogue | `Beat2: Samurai-4 (the duel becomes a dialogue)` — `ch16_beat2_duel` |
| 9 | Trigger | `Activate Samurai-4` — `SetActive(true)` on her GameObject |
| 10 | Prompt | `Samurai-4 Duel (DuelYield, yield + sheathe)` — a null-target `Prompt` step; the mission advances only when `DuelYield.onAccepted` fires |

**What changes during the beat:** Samurai-4 goes from inactive to an active, lethal-to-the-player `Enemy` the instant step 9 fires; the fight itself is real (player `Health` is at risk) until her yield threshold trips.

**Clarifying the "yield + sheathe" step label.** `DuelYield` resolves purely on the swing-output threshold: once `yieldThreshold` (0.22) is crossed against Samurai-4's `Health`, `disableOnYield` fires and `onAccepted` advances the mission automatically (or `autoAcceptSeconds` 30 times out). It is **not** gated on the player performing any physical sheathe or blade-lowering gesture — there is no sheathe-detection check on `DuelYield` today, and none should be wired for this beat. Canon's stage direction ("lowering his blade first") is the character's own animation/VO beat, not a player-input mechanic; the step's "yield + sheathe" label describes what happens on screen, not what the player must do with the controller.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| LowerTier shell (12×32, widened) | top-center (0, −12, 110) | `Rooms.SepulcherTier_MemoryPure` | `…/Art/Generated/Rooms/SepulcherTier_MemoryPure.prefab` | **MISSING** |
| `RampE` *(closes this beat's arena, opens Beat 3/4)* | mid (0, −15, 134), width 12 | `Rooms.DescentRamp_Standard` | same prefab as Beat 1's ramps | **MISSING** |
| Samurai-4 (boss instantiation) | (0, −12, 106), Euler(0,180,0) | `Named.Samurai4` | `…/Art/Generated/Characters3D/Named/Samurai-4.prefab` | **EXISTS** |
| `LowerTierLight0` (accent) | (0, −11.8, 106) | — | `ChapterEnvironmentProfile.accentLights["LowerTier"]` — "sourceless memory-light" | profile |

**Staging constraint.** LowerTier's floor/ceiling tint is `goldFloor`/`goldCeil` × 1.15 — the single brightest, most saturated memory-gold tier before the palette shifts to violet at the throne-core. This is deliberate: Samurai-4's introduction is staged as the descent's visual peak before it turns cold and monumental below.

**Staging constraint — she must read as the player's own mirror.** Canon's SETTING block (lines 44–46) is emphatic that Samurai-4 is "a near-mirror of what Ronin was... a pristine dark-armored echo of his own conditioning with a half-mask and a twin blade," sleeker and cleaner than the player rig. The duel's entire thesis — Beat 1.e's "She's what you were. Same make as you, current issue, cleaner leash" — depends on the player reading her silhouette as *themselves, cleaner*, the instant she comes into view. `Named.Samurai4` must present as a pristine, darker, half-masked, twin-bladed mirror of the player character, not a generically distinct boss design. This "sleeker/cleaner" read is correct and desirable per AUDIT FIX #6 (§4 Beat 1.e) — it must stop short of reading as a visually *newer model*, which would re-imply the retconned fifth-Program lineage the audit fix removed from the dialogue; the visual language and the corrected line must agree.

**Warning — the SETTING block itself still carries the unfixed phrase.** AUDIT FIX #6 corrected only `Chapter16Lines.cs`'s spoken dialogue ("newer make" → "same make as you, current issue, cleaner leash"); the SETTING block quoted immediately above was never touched, and its own line 45 literally reads "a near-mirror of what Ronin was... leashed, lethal, certain, **newer-make and sleeker than him**." Because this is the exact passage this section directs artists to for Samurai-4's design brief, reading it at face value re-imports the retconned "newer model" implication the audit fix deliberately removed from the shipped line. Treat "newer-make" in the SETTING block as loose narration-draft phrasing that predates the fix, not as an art-direction instruction: build her as **sleeker/cleaner, same make as Ronin-7** (per the corrected dialogue and AUDIT FIX #6), never as a visually newer or later model.

**The specific mesh detail the "mirror" thesis actually cashes out on.** Beyond the half-mask and twin blade, canon gives one more literal twinning cue: at the mask-removal (Beat 3), "the killswitch scar at the base of her skull [is] a twin of his" (dialogue line 391). This is the visible proof-of-make the duel's dialogue argues for in words ("same make as you") — cheap, canon-exact, and specific in a way "pristine dark-armored echo" alone is not. `Named.Samurai4` should carry a nape/base-of-skull killswitch scar as a modeled or textured detail, matching whatever the player-rig's own equivalent scar reads as (see the note below on why the player's own throat-scar can't be shown the same way). Add it to the presentation contract here and note it again at the reveal in Beat 3.c.

One more constraint follows directly from how she's built: `Ch16BuildNamedBoss` (§4 Beat 2.b) only synthesizes a bare `ArmR/Sword/Blade/BladeTip` transform chain for reach/damage purposes — no mesh, no material (Appendix B's reuse note, line 1200, is explicit the boss rigging is code, not art). The *visible* twin blade this staging constraint depends on therefore has to be baked into the `Named.Samurai4` prefab itself; if it ships weaponless, she duels with an invisible sword and the "mirror" read breaks at the most literal level. Confirm each combat-Named prefab carries its own visible weapon geometry before treating its beat as art-complete — the same requirement applies to `Named.Maelgorn`'s and `Named.Khall`'s combat instances (§4 Beat 6, Beat 9).

#### d. Combat

The saga's definitive **non-lethal boss duel**. `DuelYield` reads the player's blade-swing output against Samurai-4's `Health` exactly like any other `BladeDamager` encounter — the only mechanical difference is the 0.22 `yieldThreshold` clamp and `disableOnYield`, which disables her `Enemy` component the instant the threshold trips (rather than her `Health` reaching zero). No `DefeatEnemies` step is wired for this fight; the mission advances only through the `Prompt` step's `onAccepted` listener. **No camera shake** — the duel's escalating stakes read through `Haptics` clash-pulse feedback and `AudioDirector` blade-lock stingers only.

#### e. Dialogue / VO

`Dialogue_Beat2_Duel`, set `ch16_beat2_duel`, (0, −12, 100), **8 lines, ≈205 s**:

| Speaker | Line (abridged) | sec |
|---|---|---|
| Samurai-4 | "Cipher. Operative designation Ronin-7. You will not pass this tier... There is no hitch in me. Stand down." | 23 |
| Ronin-7 | "They told me the same thing about myself... You held a half-beat too long. That half-beat is the only honest thing they left in you." | 28 |
| Samurai-4 | "That was an inefficiency... Do not mistake a fractional delay for a soul, Cipher. I am not you. I do not break." | 11 |
| Echo | "Cipher, listen to her cadence, not her words... Every exchange you don't kill her in is an exchange the leash has to explain itself in, and the leash is a terrible liar." | 31 |
| Samurai-4 | "Why do you not finish it? You have had three openings... the order does not allow for two of us standing here unfinished." | 22 |
| Ronin-7 | "I don't take the openings because I'm not here to add you to a count... I'm going to do the thing they never let either of us see done." | 33 |
| Samurai-4 | "If I let go of the order I have nothing... You are offering me a fall with no bottom." | 22 |
| Ronin-7 | "You will have a choice. That is not nothing. That is everything... Take the choice, or take my head. Either way it's yours." | 35 |

> **AUDIT FIX #6 (minor half, already applied):** the source script has Samurai-4 address him "Ronin-7" twice mid-duel. Her **first** use ("Operative designation Ronin-7") is kept verbatim as a formal file-citation establishing her cold Program voice. Her **second**, purely address-form use is changed to "Cipher" per the saga's standing convention (the serial is narration-only; spoken address uses "Cipher"). See `Chapter16LinesTests` regression guard referenced in `Chapter16Lines.cs`'s class summary.

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point in the fight**, including the moment Ronin-7 lowers his blade — that beat is carried entirely by VO performance, a held silence in the audio bed, and (recommended, not yet wired) a `Haptics` cue distinct from combat-hit feedback marking the deliberate stillness.
- **The player's own half of the mirror beat cannot land visually, and that's expected, not a gap to fix.** Canon stages this moment with Ronin-7 standing "open, the throat-scar bare to her" (dialogue line 362) — but the player rig is head + two hands only, no visible body (§2), so there is no first-person mesh to bare a throat on. This beat is necessarily carried entirely by VO ("You feel it... it is the only honest thing they left in you") and by Samurai-4's own animation/reaction, never by a player-body reveal. Worth stating once so a future pass doesn't go looking for a body-reveal mechanism that the rig architecture rules out.
- No dedicated accent-light event fires during the duel — `LowerTierLight0` stays static throughout, keeping the visual field calm so the *dialogue* carries the escalation, not lighting.
- Comfort vignette is largely inert — the duel is fought within LowerTier's arena, with minimal player translation and whatever snap-turns tracking the fight requires.

---

### Beat 3 — Breaking Her Leash (Mercy as Contagion — Ally: Family)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> No new tier geometry in this beat — LowerTier is Beat 2's arena, reused. Create **`BuildBeat3Logic()`** only: the `Trigger`+`Prompt` pair around `LeashBreakController`, and the recruitment dialogue. **`BuildBeat3Art()` is scoped to exactly one prop** — `Props.SignalRelay`, the wall fixture Samurai-4's blade strikes to physically sever her leash — not the full-room build Beat 2 already did.

#### a. Narrative purpose & emotional target

The recruitment of Samurai-4 as **family — a capstone bond beyond the numbered ten**, and per the production note the break must be an *action*, not a speech: she turns her blade away from Ronin-7's throat and drives it into the Program's signal-relay in the tier wall, physically severing the leash, the way Coral Vex once tore out her own switch. Critically, she must not be written warm or grateful in the immediate aftermath — she is "robotic certainty newly without a floor," disoriented, fragmented ("I do not know what to do with my hands... What do I do now"). Coral Vex and Vess each answer from the two halves of the saga's own mercy-lineage: Coral's fifty-year-old relief, Vess's harder truth that "the choice doesn't make you clean." Samurai-4's own closing line — "That is my choice. The first one. I am keeping it." — is her arc's actual turn, not the relay strike itself.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **`LeashBreakController`** lives on the `Samurai4_LeashBreak` child object built in Beat 2, deliberately kept independently activatable *after* the duel accepts. `startingConviction` 1, `breakThreshold` 0.25, `convictionDrainPerSecond` 0.5 (breaks in ~1.5 s — "the held silence... her hand begins to shake"), `evidenceDrainAmount` 0.34, `autoAdvance` true. `onLeashBreak` is wired to `missionDirector.AdvanceFromPrompt`, resolving a second, distinct null-`Prompt` step from the duel's own.
- **Dialogue anchor:** `Dialogue_Beat3_LeashBreak` at (0, −12, 108), set `ch16_beat3_leash_break`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 11 | Trigger | `Activate Leash-Break` — `SetActive(true)` on `Samurai4_LeashBreak`, seeding `LeashBreakController`'s conviction on first `Awake` |
| 12 | Prompt | `The Quiet (LeashBreakController onLeashBreak)` — a null-target `Prompt` step resolved only by the drain crossing `breakThreshold` |
| 13 | Dialogue | `Breaking Her Leash (Ally: family)` — `ch16_beat3_leash_break` |

**What changes during the beat:** Samurai-4's `Enemy` component was already disabled by `DuelYield.disableOnYield` in Beat 2; this beat's only mechanical state change is `LeashBreakController` running its conviction-drain timer to completion. **Her `StoryNpc` and mesh do not move.** She has no `NpcWalker` and no follow behavior of any kind — her one GameObject stays frozen at (0, −12, 106), the coordinate `Ch16BuildNamedBoss` spawned her at in Beat 2. Every later reference to her being "at the player's shoulder" (this document's own prior phrasing included) describes a mechanism that does not exist in the builder; it is not a simplification of a working follow-leg, it is a gap. See §5's travel-route table and its immersion-cost note, which this document now scopes to **Beats 4–9**, not 8–9 alone — she has spoken lines in `ch16_beat4_soren` (anchor z=154) and `ch16_beat5_throne_core` (anchor z=200+), both delivered with her mesh stranded 48–95 m up-core on LowerTier.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `SignalRelay` (the handler's relay Samurai-4's blade strikes) | (6, −10.5, 106) on `LowerTier_WallE` | `Props.SignalRelay` | `…/Art/Generated/Props/SignalRelay.prefab` | **MISSING** |

The production note (dialogue line 377) is emphatic the break "must be an ACTION, not a speech: she turns her blade on the handler's signal-relay... seated in the tier wall." Without a struck prop, the leash-sever animates against bare wall. `SignalRelay` is now a listed prop with a position rather than a footnote; its spark/relay-death VFX (and a haptic cue for the strike) remain new scope — flagged in §9.

**Mask-removal mechanism gap.** Beat 3.e's canon line (dialogue 458), Ronin-7's *"Take your mask off. You don't need it anymore,"* is a visible transformation cue on `Named.Samurai4` with no backing mechanism — nothing in this beat or the builder toggles, hides, or swaps a mesh in response to it. Flagged alongside `ForgedThrone`'s "cools to ash" gap in §9. **What the mask-removal is meant to reveal is specific, not generic:** the stage direction (dialogue line 391) has her expose "the killswitch scar at the base of her skull, a twin of his" the instant the mask comes off — the same nape scar the presentation contract asks `Named.Samurai4` to carry from first appearance (§4 Beat 2.c). The mask toggle and the scar detail are two parts of one canon beat; authoring one without the other leaves the "twin" payoff half-built.

#### d. Combat

None. The duel already resolved in Beat 2; Samurai-4's `Enemy` component stays disabled for the rest of the chapter.

#### e. Dialogue / VO

`Dialogue_Beat3_LeashBreak`, set `ch16_beat3_leash_break`, (0, −12, 108), **6 lines, ≈203 s**:

| Speaker | Line (abridged) | sec |
|---|---|---|
| Samurai-4 | "It's quiet. The order... I do not know what to do with my hands... What do I do now. There is no order. What do I do." | 33 |
| Ronin-7 | "You stand there a second and you let it be quiet... You don't figure out what you're for in the next minute. You just choose the next thing... Take your mask off. You don't need it anymore." | 38 |
| Coral Vex (comm) | "I felt the relay die from up here... It ends. You hit ground eventually, and the ground is yours, and you build on it... She's got a ship full of us." | 42 |
| Vess (comm) | "The choice doesn't make you clean... Freedom isn't forgiveness and it isn't peace. It's just yours... That's where all of us started." | 32 |
| Samurai-4 | "They're talking to me... I'll come down with you. Not because of an order... That is my choice. The first one. I am keeping it." | 40 |
| Ronin-7 | "Then keep it, and come down... After today neither of us has a number anybody else gets to use." | 18 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the relay-death spark and Samurai-4's mask removal are visual beats carried by VFX/lighting only.
- No dedicated combat haptics fire in this beat; the only tactile cue implied by the screenplay (the blade striking `SignalRelay`, §4 Beat 3.c) is recommended future scope, not currently wired — see §9.
- Comfort vignette is inert; the beat is entirely stationary dialogue on LowerTier.

---

### Beat 4 — Reclaiming Soren (The Name Beneath the Number) — REVEAL, closes Ladder C

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives. Read `SepulcherFloor` (an open platform, no walls/ceiling) and the `MemoryCradle` prop from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (the floor-only platform, the cradle, the ghost-tinted Soren-memory figure) and **`BuildBeat4Logic()`** (the reach point, the reveal dialogue). **This is the ONE beat in the saga where the speaker label switches — from `Ronin-7` to `Soren` — mid-set.** Do not relabel any line before this beat or after it retroactively.

#### a. Narrative purpose & emotional target

The identity payoff of the whole saga, and the direct inversion of Chapter 12's source-horror into ownership: not the planted cover "Kael Vor" (Ch5), not just "the template" they printed an army from (Ch12) — beneath even the original, a stolen boy with a name the Program buried. Being the one they all came from is not an identity the Program gave him; the self under it is *recovered*. Per the production note, the reveal must land **diegetically** — the memory-core opening, light flooding in, the name spoken three times, each one steadier — not narrated after the fact. Echo's response is the beat's other load-bearing line, and it must **not** land clean: *"...Soren. Going to take me a minute to learn it."* The AI that named him Cipher since the bay is explicitly shown discovering that its own name for him was itself a leash.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Reach point:** `SepulcherFloorReachPoint` (0, −12, 130) r5 — note this does not sit on `SepulcherFloor` itself (z[142,166]); z=130 is partway down `RampE` (z[126,142]), so the player crosses it mid-ramp, before arriving at the memory-floor proper.
- **`Ronin-7_Cipher_Soren` ghost figure:** placed via `Ch16PlaceGhostNpc(Ch16SorenSelfPrefab, (0, −18, 156), "Soren (Memory)")` — mirrors Ch11's `PlaceGhostNpc` technique: real mesh, no `Health`/`Enemy`, all renderers swapped to `MemoryFlashbackController.MakeGhostMaterial()`, purely a visual dialogue-beat anchor. This is "the buried self, made visible," decorative only.
- **Dialogue anchor:** `Dialogue_Beat4_Soren` at (0, −18, 154), set `ch16_beat4_soren` — positioned exactly at the `MemoryCradle`'s own coordinates.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 14 | ReachTrigger | `The Sepulcher Floor` — (0, −12, 130), r5 |
| 15 | Dialogue | `Reclaiming Soren (REVEAL, closes Ladder C)` — `ch16_beat4_soren` |

**What changes during the beat:** no combat, no lock state — the entire beat is the player walking onto an open platform, touching the cradle diegetically (via dialogue/VO pacing rather than a scripted grab interaction — see §9), and the mission advancing on dialogue completion alone.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `SepulcherFloor` (open platform, no walls/ceiling, 14×24) | top-center (0, −18, 154) | `Rooms.SepulcherFloor_MemorySpace` | `…/Art/Generated/Rooms/SepulcherFloor_MemorySpace.prefab` | **MISSING** |
| `MemoryCradle` | (0, −17.4, 154) | `Props.MemoryCradle` | `…/Art/Generated/Props/MemoryCradle.prefab` | **MISSING** |
| `TheBigDescent` ramp *(opens Beat 5 — listed here for continuity)* | mid (0, −24, 182), width 14 | `Rooms.DescentRamp_Grand` | `…/Art/Generated/Rooms/DescentRamp_Grand.prefab` | **MISSING** |
| Soren (Memory) ghost figure | (0, −18, 156) | `Named.RoninCipherSoren` | `…/Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab` | **EXISTS** *(ghost-material override applied at runtime, not baked into the prefab)* |
| `SepulcherFloorLight` (accent) | (0, −17.8, 154) | — | `ChapterEnvironmentProfile.accentLights["SepulcherFloor"]`, `behaviour: AmbientPulse(7.8s)` — "sepulcher = tomb beat" | profile |

**The floor giving way, without a seam to show it.** Canon's image (dialogue line 450) is the cradle-chamber floor that "shudders, and splits" to reveal the throne-core below; this document correctly renders the transition as a forward-walking ramp for VR comfort rather than a vertical drop (§2), but the junction from `SepulcherFloor`'s far edge (z=166) into `TheBigDescent`'s head is currently a plain geometric butt-joint, identical to every earlier tier-to-tier ramp transition. Recommend a split-seam / opened-floor VFX or geometry cue at that junction — either on `SepulcherFloor`'s far edge or `TheBigDescent`'s head — so walking onto the grand ramp reads as "the floor opened," not "here's another ramp." Without it, the single most important structural beat in the chapter ("the floor of the self opens onto the floor of the cage," §3) is visually indistinguishable from the five earlier transitions. See §4 Beat 5.c.

**Notes on the transition.** `Rooms.SepulcherFloor_MemorySpace` ships **without perimeter walls** — there is no wall geometry here by design (the "stone is gone" per the screenplay). But an uncollided footprint is not itself a bound: `ZoneBounds` (center (0,−15,150), radius 200) is a single sphere loosely enclosing the entire ten-tier descent, and at this platform's z≈154 it permits x≈±199 of travel — nowhere near tight enough to stop a player walking off the platform's actual x=±7 edge into the void below. **An invisible, knee-to-waist-height collision lip at the floor's own footprint edge is required** — comfort-safe, non-visual, and consistent with "the stone is gone" (it must read as nothing, not as a wall) — or Beat 4's stationary reveal is one careless step from a fall-through-floor bug and a VR comfort hazard. `TheBigDescent`'s registry key is deliberately distinct from the standard `Rooms.DescentRamp_Standard` used everywhere else (`Rooms.DescentRamp_Grand`) because it is nearly **double the length** of every prior ramp (z[166,198], a 32 m run vs. every other ramp's 8 m) and needs its own art pass to avoid reading as a stretched/tiled reuse of the short ramps.

#### d. Combat

None.

#### e. Dialogue / VO

`Dialogue_Beat4_Soren`, set `ch16_beat4_soren`, (0, −18, 154), **5 lines, ≈126 s** — the shortest per-line-count beat in the chapter and, per the production note, deliberately unhurried:

| Speaker | Line | sec |
|---|---|---|
| Echo | "This is it, Cipher. And this one isn't mine to hand you... Put your hand on it. Let it come up to you. I'll be right here when it does." | 28 |
| Ronin-7 / **Soren** | "Soren. My name is Soren. There was a boy, and he had a world... But it's mine. It was always mine. Soren." | 20 |
| Samurai-4 | "Soren. I came down here to stop you from reaching that... I watched you do it, and now I know it can be done, and that is more than I had this morning." | 34 |
| Soren | "If there's a name under your number, it's down a vault like this one, and when this is over we'll go find it... That's the whole point of the ship at the top." | 13 |
| Echo | "...Soren. Going to take me a minute to learn it, I'll be honest with you... But the way down is still down. The floor under you isn't the bottom. Let's finish it. I'm right here, Soren." | 31 |

**This is the ONE chapter in the saga where "Soren" is spoken** — every dialogue set from `ch16_beat4_soren` onward legitimately keeps using it, which is continued use of an already-landed reveal, not a re-disclosure (per `Chapter16Lines.cs`'s own class-summary comment).

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the memory-flood is a lighting/VFX event (recommended: a bloom/glow pass over the cradle timed to Echo's "Put your hand on it" line), never a forced camera move.
- (Recommended, not yet wired) a distinct, non-combat `Haptics` pulse on Echo's "Put your hand on it" and the first "Soren" landing — the saga's emotional apex has no tactile marker today, and a pulse here is fully comfort-safe (no camera motion), the same kind of tactile marker this document recommends for Beat 2's "deliberate stillness" (§4 Beat 2.f).
- **The reveal itself plays in a ~28 m audio dead zone (flagged).** §7's recommended `memory_tone` emitters reach only as far as MidTierA (z=54), MidTierB (z=78), and LowerTier (z=110, range 5–16 → ≈z=126); the cradle anchor sits at z=154, past that falloff, and `throne_rumble` is not yet audible either (§4 Beat 5.f). So "Put your hand on it… Soren" lands with only `SepulcherFloorLight`'s pulse and no tone bed at all — undercutting §3's own thesis that "the sourceless light and the sourceless tone are the same motif in two channels." §7's table now adds a fourth `memory_tone` emitter at SepulcherFloor (0, −18, 154) to close this gap.
- `SepulcherFloorLight`'s `AmbientPulse(7.8s)` behaviour is the chapter's second slow-breathing accent light (after `MedbayLight`'s 7 s pulse in Ch1) — a deliberate structural echo, "the sepulcher as a tomb beat," landing right where the tomb gives the buried self back.
- Comfort vignette is inert; the beat is entirely stationary at the cradle.

---

### Beat 5 — Into the Throne-Core (The Bottom of the Self Is the Bottom of the Cage)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read the `ThroneCore` shell, `LatticeSpar0/1`, `SeamMarker`, and `ForgedThrone` from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (the monumental open-platform cathedral, the lattice spars, the seam marker, the throne) and **`BuildBeat5Logic()`** (the reach point, the transition dialogue). **The descent must not cut here — `TheBigDescent`'s ramp already carried the player across the beat boundary in Beat 4's art pass; this beat is purely what the player walks into at the bottom of it.**

#### a. Narrative purpose & emotional target

A short, weighted transition beat — per the production note, "keep this beat short and weighted, a held breath before the time-loop." The scale-flip from intimate memory-space to monumental war-machine cathedral is the whole point; Soren names the structural truth himself now that he is standing in it ("No door, no cut, no wall between the bottom of me and the bottom of this... That's not architecture. That's a confession"). Heris confirms the seam and sets the liberation condition that pays off in Beat 9 ("if you cut the seam wrong you kill them with it instead of freeing them... We break it together or we do not break it right"). Cassie-04 names the lattice as her own ledger made flesh. Samurai-4 grasps the true scale of what she was guarding for the first time, in halting, fragment-broken sentences — deliberately *not* a polished aphorism, per the production note's own instruction to keep her new-person voice raw.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **Reach point:** `ThroneCoreReachPoint` (0, −18, 168) r6 — at the top of `TheBigDescent` (z[166,198]), not yet inside `ThroneCore`'s own z-range (z[198,290]); consistent with §6's Beat 5 row, which correctly times the tint shift "to the reach-point crossing at the top of `TheBigDescent`."
- **Dialogue anchor:** `Dialogue_Beat5_ThroneCore` at (0, −30, 200), set `ch16_beat5_throne_core`.
- **Samurai-4 does not travel to the throne-core.** Her GameObject has no `NpcWalker` and no follow behavior — she remains exactly where Beat 2 placed her, (0, −12, 106) on LowerTier, roughly 94–134 m short of this beat's dialogue anchor. "Continues at the player's shoulder" is not a mechanic the builder implements; it is this document's prior shorthand for "her `StoryNpc` and combat state carry forward unmodified," corrected here and in §5. If this beat's dialogue is ever voiced as though she is present, that presence is unbacked until §5's recommended second-placement or follow-leg fix lands.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 16 | ReachTrigger | `The Throne-Core (no cut)` — (0, −18, 168), r6 |
| 17 | Dialogue | `Into the Throne-Core` — `ch16_beat5_throne_core` |

**What changes during the beat:** the tint shift from memory-gold to deep violet/furnace-orange is the single largest single-frame environmental shift in the chapter, timed to the reach-point crossing rather than any Trigger step.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `ThroneCore` (open platform, no walls/ceiling, 32×92) | top-center (0, −30, 244) | `Rooms.ThroneCore_Cathedral` | `…/Art/Generated/Rooms/ThroneCore_Cathedral.prefab` | **MISSING** |
| `LatticeSpar0` | (−10, −22, 220) | `Props.LatticeSpar` | `…/Art/Generated/Props/LatticeSpar.prefab` | **MISSING** |
| `LatticeSpar1` | (10, −22, 236) | `Props.LatticeSpar` | same prefab | **MISSING** |
| `SeamMarker` (Heris's flaw) | (3, −29.4, 228) | `Props.SeamMarker` | `…/Art/Generated/Props/SeamMarker.prefab` | **MISSING** |
| `ForgedThrone` | (0, −29, 270) | `Props.ForgedThrone` | `…/Art/Generated/Props/ForgedThrone.prefab` | **MISSING** |
| `ThroneCoreLight0/1` (accent) | (−8, −25, 220), (8, −25, 250) | — | `ChapterEnvironmentProfile.accentLights["ThroneCore"]`, `behaviour: ConsoleFlicker(seed: 151)` — "lattice violet" | profile |
| `ThroneLight` (accent) | (0, −22, 270) | — | `ChapterEnvironmentProfile.accentLights["Throne"]` — "Maelgorn's furnace-orange" | profile |
| `ThroneCoreAmbience` (audio layer) | (0, −29, 270) | — | `throne_rumble.wav` via `BuildAmbienceLayer` | audio |
| `KeptShadowLattice` | (0, −25, 230), populating the space around/between `LatticeSpar0/1` | `Vfx.KeptShadowLattice` | `…/Art/Generated/VFX/KeptShadowLattice.prefab` | **MISSING** |

**Staging constraint.** `Rooms.ThroneCore_Cathedral` is the one shell in the chapter that must ship **without any wall or ceiling geometry at all** — its read depends entirely on scale (32 m half-width vs. every prior tier's 6–7 m) and the two lattice spars plus accent lighting implying a structure the player cannot see the edges of. **The same fall hazard flagged for `SepulcherFloor` (§4 Beat 4.c) applies here with higher stakes**: the platform's actual footprint is x=±16 at z[198,290], well inside `ZoneBounds`' permissive radius-200 sphere, and this tier hosts the frantic three-phase Maelgorn boss fight (Beat 9) — an unexpected edge mid-combat is a worse comfort hazard than during a stationary reveal. `Rooms.ThroneCore_Cathedral` needs the same invisible knee-to-waist-height collision lip at its footprint edge before it ships. `ForgedThrone`'s color is authored as obsidian-black today (0.1, 0.08, 0.1) and must be able to **cool visibly to ash** at the climax (Beat 9) — either via a runtime material swap or a `RendererTint` blend, not a hard cut.

**The empty throne as sustained foreshadow.** Once `ThroneLight` starts banked rather than full-bright (§4 Beat 8.c), the dim, occupied-looking-but-empty `ForgedThrone` is deliberately visible on the ~72 m sightline from `ThroneCore`'s entrance through Beats 5–7 — a looming vacant seat the player registers long before Maelgorn rises from it in Beat 8, turning the reveal into the payoff of an established sightline rather than a spawn.

**The missing pre-liberation payoff anchor.** Neither `LatticeSpar0/1` nor any lighting currently represents the lattice as *full*. Cassie-04's "Every point of light in that thing is a name in my registry. Thousands of them... awake" (dialogue line 471) and Sable's line on the felt wire (dialogue line 642) both describe a structure dense with trapped, conscious shadow-lights, visible from the moment the player enters `ThroneCore` — but the build holds only two bare spar props and violet accents until Beat 9's `EngineBreakLiberation` switches on three `FreedShadowLight` points from nothing. Without a captive state to release *from*, the liberation frees lights the player never saw held. `Vfx.KeptShadowLattice` should be many dim, strung, static point-lights (or an equivalent particle field) populating the lattice from Beat 5's arrival onward; Beat 9's liberation Trigger (step 40) then animates it outward/brightens it, rather than the current "three lights appear" placeholder — see also Beat 9.c's separate note on the *post*-break burst needing more than three static points.

**The `SepulcherFloor` → `TheBigDescent` junction, again.** As flagged in §4 Beat 4.c, the ramp head where `TheBigDescent` meets `SepulcherFloor`'s far edge (z=166) currently reads as a plain ramp start rather than "the floor gave way." A split-seam/opened-floor cue at that junction — distinct from this beat's own `SeamMarker`, which anchors the Engine's seam further down at z=228 — would sell the structural inversion before the player ever reaches `ThroneCore`.

#### d. Combat

None. This is a pure traversal/dialogue transition beat.

#### e. Dialogue / VO

`Dialogue_Beat5_ThroneCore`, set `ch16_beat5_throne_core`, (0, −30, 200), **5 lines, ≈176 s** — matching Beat 4's per-line table format for parity, since both are equivalently-sized (5-line) sets and the doc elsewhere cross-references exact `seconds:` fields for VO-batch scoping (Beat 7.e, Beat 9.e):

| Speaker | Line (abridged) | sec |
|---|---|---|
| Soren | "There it is. No door, no cut, no wall between the bottom of me and the bottom of this... That's not architecture. That's a confession. They knew the two were the same thing." | 33 |
| Heris (comm) | "I can see your position against the structure from the seam-key, Soren... Get to the seam, hold there, and wait for the whole crew and Sallow. We break it together or we do not break it right." | 41 |
| Cassie-04 (comm) | "Soren, I can feel the lattice from up here through the open structure... Hold the seam. Let us catch up. Nobody breaks that thing alone, least of all today." | 37 |
| Samurai-4 | "This is what I was guarding. Not you. This... There are thousands of them in there. Thousands. And I stood at the door and called it an order. How many of us are doing that right now." | 29 |
| Echo | "Hold here, Soren. I can feel them too, the same way I can feel myself... Stay sharp. The cage doesn't have a wall left to stop you, which means whatever it's got left is going to be worse than a wall." | 36 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — the scale-flip reads entirely through the accent-light palette shift and the throne-core's own ambience bed (`throne_rumble.wav`, a 2D/3D blend via `BuildAmbienceLayer(range 5–18, vol 0.5)`).
- **No audio foreshadow during the descent the "vast and humming" reveal belongs to (flagged).** The screenplay twice frames the throne-core as "vast and humming and monumental" the instant the cradle-floor splits (dialogue lines 38 and 450) — but `ThroneCoreAmbience` is localized at (0, −29, 270) with range 5–18, inaudible across the entire `TheBigDescent` walk (z=166→198) and most of `ThroneCore`'s own near half, exactly the stretch §3's thesis says the player "must arrive at this truth in their own legs, walking continuously downward, before anyone says it aloud." Recommend the throne-hum rise under the descent — either widen `throne_rumble`'s range, or add a second low emitter near the top of `TheBigDescent` (≈(0, −24, 170)) — so the "humming" swells as the player descends, not only once the reveal is nearly over. See §7.
- `ThroneCoreLight0`'s `ConsoleFlicker(seed: 151)` is the chapter's coldest, most unstable-power light behaviour — the "lattice violet" reads as barely-contained rather than steady, distinct from every warm/steady accent above it.
- Comfort vignette engages normally through the reach-point approach; no beat-specific override.

---

### Beat 6 — The Time-Loop Trap (Will Against Erosion) — REVEAL / TWIST

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> No new tier geometry — this beat plays entirely within `ThroneCore`, already built in Beat 5. Create **`BuildBeat6Logic()`** only: the `TimeLoopController` + two `ActivationRelay`s, and the three ghost-tinted Khall-image `DefeatEnemies` encounters.
> **`TimeLoopController.ApplyPhase()` only ever calls `GameObject.SetActive` on its `phaseGroups` members — confirmed by reading its source. It never touches the camera, and `damageWhenOutOfPhase` is explicitly left OFF.** This is a pure lighting/set-dressing toggle. Any patch that adds a camera-affecting side effect to the fold transition violates the VR comfort constraint.

#### a. Narrative purpose & emotional target

The cage's last defense is not a wall — it is *repetition*, the erosion of choice. Soren is caught reliving the same fatal moment against an image of Khall, dying and resetting, while Maelgorn taunts through the loop from the throne above. Per the production note, this must be staged as **several distinct iterations**, each one Soren choosing a different end, not one fight repeated identically — the canon exchange is load-bearing: Maelgorn's *"Every operative breaks eventually. Loop them enough and the seam re-seals. You will choose the same end until choosing means nothing"* against Soren's *"No... I have carried worse than this. I will choose different until your trap runs out of room."* The mechanic and the meaning are the same act: he breaks the trap not by force but by refusing to converge on a single inevitable ending.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat6Logic()`

All logic components parent to `[BEAT_6_LOGIC]`.

- **`RealityFold` / `TimeLoopController`:** built with **exactly two phase groups** — `normalGroup` = `{LoopNormalAccent0, LoopNormalAccent1}` (both active from scene start), `foldGroup` = `{LoopFoldAccent0, LoopFoldAccent1}` (both built inactive). `phaseInterval` is set to 999999 s — effectively infinite, so the component's own per-frame auto-cycle **never fires in a real play session**; the fold is driven *only* by two explicit `AdvancePhase()` calls. `damageWhenOutOfPhase` = false. `RealityFold` itself stays active from scene start so `Start()` runs and seeds phase 0 (normal) — it is never the thing a `Trigger` step activates; only the two relays are.
- **`EnterFoldRelay`** and **`ExitFoldRelay`**: each a small `ActivationRelay`, built inactive, with `OnEnabled` wired via `UnityEventTools.AddPersistentListener` to `timeLoop.AdvancePhase`. A mission `Trigger` step `SetActive(true)`s each relay exactly once — entering the fold (step 20) and exiting it (step 30).
- **Three Khall-image encounters**, each a fresh `Ch16BuildNamedBoss(Ch16KhallPrefab, (0, −30, 228), "Khall (Loop Image N)", khallImageDef, playerHealth)` with every renderer swapped to `MemoryFlashbackController.MakeGhostMaterial()` — a shared `Ch16KhallImage` `EnemyDefinition` (90 HP, 12 dmg — quick, repeated fights, not attrition). Each built inactive, activated by its own `Trigger` step, and cleared by its own `DefeatEnemies` step. All three occupy the **same coordinates** — they are sequential encounters at one fixed seam-approach spot, not three separate arenas.
- **Dialogue anchors:** `Dialogue_Beat6_LoopTaunt` (0, −30, 212); `Dialogue_Beat6_LoopIter1/2` and `Dialogue_Beat6_LoopBreak`/`LoopConcede` all at (0, −30, 228) — the same fixed point as the Khall-images themselves, since the "location" never moves; only the loop iteration changes.
- **The 212→228 gap is deliberate, not a missing reach step.** Step 18's `ReachTrigger` fires at (0, −30, 210) r6, and step 19's `ch16_beat6_loop_taunt` (~51 s, one line) plays from the anchor at 212 — 16 m short of the fold swap and first Khall-image, both of which land at 228 the instant step 20 fires. Unlike the reach-timing called out for Beats 4 and 5, no bridging reach step sits between them: the taunt is meant to play *as the player closes the last 16 m to the seam* on foot, arriving at 228 roughly as Maelgorn's line ends, not while standing still at 212.
- **The real Khall:** instantiated via `Ch16PlaceStoryNpc(Ch16KhallPrefab, (0, −30, 228), "Khall")`, built **inactive** — revealed in place by the `ExitFoldRelay` Trigger step (step 30), at the exact spot the ghost images occupied. This is the mechanism by which "the figure in the loop... thins, wavers, and does not vanish, because it is becoming something else" (screenplay stage direction) resolves into a real character without a second travel/placement pass.
- **Confirm each Khall-image deactivates before the real Khall reveals in place (flagged — the same class of gap as Beat 9.b's Maelgorn presence hand-off, at a worse distance).** All three `Ch16BuildKhallImage` instances *and* the real `khallGo` occupy the identical coordinate (0, −30, 228) — not 10 m apart like the Maelgorn/Phase-1 pair below, but the exact same spot. `Health`/`Enemy` do not `SetActive(false)` their own GameObject on death (confirmed by reading both classes' source), so if a defeated Khall-image's GameObject is left active as a corpse after its `Health` reaches zero (steps 21/24/27), the `ExitFoldRelay` reveal (step 30) resolves "the image thins and becomes the real man" into the real Khall standing inside a pile of up to three of his own dead ghost-selves. Confirm each Khall-image is `SetActive(false)`d (or destroyed) on death before step 30 fires.
- **Per-iteration erosion (optional polish, not asserted as wired).** All three Khall-image fights share the same 90 HP definition, ghost material, and spot, with no differentiation between iterations, even though line 111's "slower each time, but he rises" implies escalating cost. A cheap per-iteration cue — `LoopFoldAccent0/1` intensity ramping up across iterations 1→2→3, or a progressively heavier reset-sting — would sell "the trap running out of room" without changing the deliberate visual repetition or adding mechanical difficulty. See §4 Beat 6.f.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 18 | ReachTrigger | `The Seam (triggers the fold)` — (0, −30, 210), r6 |
| 19 | Dialogue | `The Time-Loop Trap (Maelgorn taunts)` — `ch16_beat6_loop_taunt` |
| 20 | Trigger | `Enter the Fold (loop iteration 1)` — activates `EnterFoldRelay` (→`AdvancePhase()`, normal→fold) **and** `khallImage1` |
| 21 | DefeatEnemies | `Khall-Image, Loop Iteration 1` |
| 22 | Dialogue | `Loop Iteration 1 (naming the wound)` — `ch16_beat6_loop_iter1` |
| 23 | Trigger | `Loop Iteration 2` — activates `khallImage2` |
| 24 | DefeatEnemies | `Khall-Image, Loop Iteration 2` |
| 25 | Dialogue | `Loop Iteration 2 (the will-mechanic)` — `ch16_beat6_loop_iter2` |
| 26 | Trigger | `Loop Iteration 3` — activates `khallImage3` |
| 27 | DefeatEnemies | `Khall-Image, Loop Iteration 3` |
| 28 | Dialogue | `The Trap Breaks (Soren's resolve)` — `ch16_beat6_loop_break` |
| 29 | Dialogue | `Maelgorn Concedes the Loop` — `ch16_beat6_loop_concede` |
| 30 | Trigger | `Exit the Fold, Reveal Khall` — activates `ExitFoldRelay` (→`AdvancePhase()`, fold→normal) **and** the real `khallGo` |

**What changes during the beat:** the two fold-accent-light pairs swap (cool blue-violet normal ↔ hot red-magenta fold) exactly twice, bracketing three full `DefeatEnemies` encounters against the *same enemy definition and the same ghost material* — the repetition is deliberately visual as well as mechanical, since the player fights the identical-looking ghost three times before Maelgorn concedes.

#### c. Art & Environment Instantiation

**No new tier geometry.** The beat's only "art" is the fold-accent lighting pairs and the ghost-material application:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `LoopNormalAccent0/1` | (−4, −28, 228), (4, −28, 228) | — | `ChapterEnvironmentProfile.loopFoldAccents["Normal"]` — cool blue (0.4, 0.55, 0.9) | profile |
| `LoopFoldAccent0/1` | (−4, −28, 228), (4, −28, 228) *(same positions as normal — built inactive)* | — | `ChapterEnvironmentProfile.loopFoldAccents["Fold"]` — hot magenta-red (0.9, 0.2, 0.35) | profile |
| Khall-image ×3 (loop iterations) | (0, −30, 228), Euler(0,180,0) | `Named.Khall` | `…/Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** *(ghost-material override at runtime)* |
| Real Khall (revealed at exit) | (0, −30, 228) | `Named.Khall` | same prefab, no override | **EXISTS** |

#### d. Combat

Three sequential `DefeatEnemies` encounters against **the same `Ch16KhallImage` `EnemyDefinition`** (90 HP, 12 dmg) at the same coordinates — the encounter itself does not escalate mechanically; the escalation is entirely narrative (Soren "choosing differently" each time, per the dialogue). No `DuelYield` — these are lethal fights against images, resolved by clearing `Health` to zero exactly like any standard mook encounter.

#### e. Dialogue / VO

Five sets, all short, bracketing the three Khall-image fights:

- **`ch16_beat6_loop_taunt`** (1 line, ~51 s): Maelgorn's V.O. introduces the loop as a re-leashing trap — *"This is not a wall... This is the other kind of cage. The kind that does not stop you."*
- **`ch16_beat6_loop_iter1`** (2 lines, ~62 s): Soren recognizes the Khall-image for what it is (*"Of all the deaths they could make me die over and over, they picked the man who pulled my switch"*); Echo, cutting in and out through the fold, names the will-mechanic directly (*"you keep dying because you keep choosing the same thing... choose like a person, who can always choose again"*).
- **`ch16_beat6_loop_iter2`** (1 line, ~49 s): Soren's canon will-line — *"I will choose different until your trap runs out of room"* — landed as the spine of the beat.
- **`ch16_beat6_loop_break`** / **`ch16_beat6_loop_concede`**: Maelgorn's concession (*"Interesting. You vary... I did not design [the cage] for a man with a name"*) and his pivot to a direct confrontation (*"Come up to the throne. The hand in your way will step aside"*).

> **Split-membership caveat.** The dialogue script groups this exchange as one continuous scene; the five-set split (`taunt`/`iter1`/`iter2`/`break`/`concede`) is authored directly in `Chapter16Lines.cs` and was not independently re-verified line-by-line against the source script for this document — treat the exact per-line membership above as **inferred** from the segment groupings and confirm against the live `Chapter16Lines.cs` methods before recording a final VO batch, the same hedge the Ch1 companion document applies to its own wake/settle boundary.

#### f. Audio / Haptics / VR Comfort

- **No camera shake, and this is the beat where that constraint matters most.** "Dying and resetting" is exactly the kind of moment a less disciplined implementation would reach for a camera flash-cut or a forced snap; this builder instead implements the entire REVEAL as `TimeLoopController` toggling two light-pairs' `SetActive` state. Preserve this discipline in any future patch.
- **Naming the inversion precisely.** Mechanically each Khall-image "death" is the *player winning* the exchange — the loop clears via ordinary `DefeatEnemies` (§4 Beat 6.d) against the image's `Health`, not any player death. The screenplay's fiction runs the other way (Soren dying and resetting, dialogue lines 111–117); that fiction is carried entirely by the reset-sting and the fold-light swap *between* the three won encounters, never by an actual player-death state — which is exactly why a distinct reset-sting is load-bearing here: it is the only cue signaling "reset" when the mechanic itself says "victory." Each Khall-image kill and each loop reset should read through `Haptics` and `AudioDirector` stingers distinct from a normal combat hit/kill cue — a falling/reset-specific sting is recommended but not asserted as already wired; flag as inferred future scope if not present.
- Comfort vignette: because the loop fires exactly two hard lighting-state transitions (enter-fold, exit-fold) with **zero player-position changes attached to either**, there is no comfort concern here beyond the standard combat snap-turn cadence during the three fights.
- **The fold's hard hue/luminance flip is a comfort axis distinct from vection, and it is not yet examined.** `TimeLoopController.ApplyPhase()` does an instantaneous `SetActive` swap from cool blue-violet (`LoopNormalAccent`, intensity 1.4) to hot magenta-red (`LoopFoldAccent`, intensity 1.8) across the player's whole field — a sudden full-field hue-and-brightness snap. This has no camera motion and so satisfies the "no camera shake" constraint, but a full-field color/brightness jolt is its own photosensitivity/luminance-jump comfort concern, separate from motion-induced vection. If in-headset testing shows the hard flip reads as a jolt, the mitigation is a brief eased crossfade between the two light-pairs (new scope — `TimeLoopController` only does `SetActive` today), not a return to any camera effect.
- **Erosion is currently off the player's felt experience (optional polish, not a gap).** The only thing distinguishing the three loop fights today is the between-fight dialogue; a fold-light intensity ramp per iteration, or a heavier reset-sting on iterations 2 and 3, would give "slower each time" (dialogue line 111) a felt correlate without touching the deliberately-repeated visual staging or the mechanic itself.

---

### Beat 7 — The Forged Order (The Handler Who Faked His Death) — REVEAL, closes Ladder D

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> No new tier geometry. Create **`BuildBeat7Logic()`** only: the reveal dialogue anchored at the real Khall's position (already revealed by Beat 6's exit-fold Trigger).

#### a. Narrative purpose & emotional target

The gut-punch is not that Soren was made a weapon — it is that his *rebellion* was one too. The Kethel-7 order that began his guilt (Ch3/Ch6/Ch8) was forged by the Hollow Kings, purpose-built to fracture a mercy operative and aim the resulting rebel at the Concord Engine, because the Obsidian Synod feeds on perpetual war and a machine that could end it had to be broken by a blade the Synod "did not have to hold." Khall's history is the emotional spine: he too suspected the order, investigated it himself (a thing "handlers are killed for"), took a Hollow King prisoner, learned of the Engine, and has been helping Soren undercover ever since faking his own death — paying off Ch3's "I know," Ch8's grief-vision, and his guilt-logs across the saga. Soren holds him accountable before accepting anything ("Why should the man who killed me get to be the one who explains it") and, per the production note, absolution is explicitly **withheld** even as the alliance is accepted: *"I'm not going to forgive you, Khall... But you're right about the one thing that matters... So run with me."*

#### b. Mission Logic, Triggers & Blocking → `BuildBeat7Logic()`

All logic components parent to `[BEAT_7_LOGIC]`.

- **Khall** is already active and in place from Beat 6's `ExitFoldRelay` Trigger step — no new placement here.
- **Dialogue anchor:** `Dialogue_Beat7_ForgedOrder` at (0, −30, 230), set `ch16_beat7_forged_order`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 31 | Dialogue | `The Forged Order (Khall repents, allies; Ladder D3 + E)` — `ch16_beat7_forged_order` |

**What changes during the beat:** none in the physical scene — this is a pure dialogue beat, resolving on completion alone.

#### c. Art & Environment Instantiation

None new — Khall's mesh was placed in Beat 6.

#### d. Combat

None. Khall is unarmed and non-combat throughout the chapter — the builder's class-summary comment is explicit he never fights.

#### e. Dialogue / VO

`Dialogue_Beat7_ForgedOrder`, set `ch16_beat7_forged_order`, (0, −30, 230), **10 lines, ≈354 s** — the longest exchange-driven (not monologue-driven) set in the chapter:

| Speaker | Line (abridged) | sec |
|---|---|---|
| Khall | "It's really you... The Kethel-7 order... It was forged. The Hollow Kings wrote it... I have hated my own hands every day since." | 50 |
| Soren | "You pulled my switch, Khall... I have heard you say 'I know' in the dark for half a war... Why should the man who killed me get to be the one who explains it." | 25 |
| Khall | "Because I said 'I know' and meant it... I found the forgery. I traced it to the Hollow Kings. And then I did the one brave thing in my whole cowardly life. I took one of them prisoner." | 49 |
| Echo | "Soren. He's telling the truth... I'm not telling you to forgive him. That's yours, not mine... We need what he took off that prisoner." | 38 |
| Khall | "The prisoner told me what the order was for... the ten syndicates... all of it kept at each other's throats on purpose, because the war is the product... they would not dirty their own hands doing it." | 42 |
| Soren | "So they built a war to sell. And a machine that could end it was bad for business. That's what Kethel-7 was. A sales tool." | 9 |
| Khall | "That's the whole of it... I should have run to you instead of from you. Instead I ran... so I could say it to your face." | 44 |
| Soren | "So my whole life was a forged document... Even the mercy. They counted on the mercy... a tool in someone else's hand the entire time." | 28 |
| Khall | "Yes. And then you did something the forgery did not account for... Run with me now, Cipher... earning the right to stand at your shoulder for the end of it. Let me." | 30 |
| Soren | "I'm not going to forgive you, Khall... The mercy was mine, every time, and they never managed to hold it. So run with me... You've been clearing my roads all this while. Clear one more." | 39 |

**Note.** This corrects the prior approximate line/duration figures against the source script's exact per-line `seconds:` fields (`Ch16_The_Throne_of_Ashes_Dialogue_Script.md`, BEAT 7).

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** This is the chapter's heaviest single expository beat and is carried entirely by VO performance ("controlled and guilty and worn hollow... never a ranter, never begging" per the voice direction) and the throne-core's ambience bed continuing underneath.
- No haptics fire in this beat — consistent with Ch1's own precedent that its heaviest dialogue-only reveal beat (Ch1 Beat 4) is the chapter's one stretch without combat feedback.
- Comfort vignette is inert; stationary dialogue.

---

### Beat 8 — The True Enemy (The Outside Hand, Named) — REVEAL, Ladder E converge

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Create **`BuildBeat8Logic()`** (the reveal dialogue) and the **cast-reveal Trigger** for Maelgorn's presence + the Hollow Kings + Sallow (all inactive until this beat's Trigger step). No new tier geometry.

#### a. Narrative purpose & emotional target

Behind the Hollow Kings, behind the ten syndicates, behind the Program: Maelgorn and the Obsidian Synod, the architects who built the cage to keep the galaxies at war and the dead in storage forever — the "outside hand" Morrigan sensed at the Iron Dojo (Ch6), named at last. Per the production note Maelgorn must read as "cold sovereignty and the long view," never a ranter, absolute and unhurried and contemptuous of the rebellion he believes he authored. His canon line — *"I needed a blade I did not have to hold. So I broke one open and let it believe the breaking was its own."* — is the centerpiece. He deliberately denies Soren the reclaimed name throughout, calling him Cipher to the end; Soren turns that withholding into the proof of Maelgorn's own defeat (*"Say it. You can't. And that's how I know you've already lost"*). Morrigan's comm line gives the whole crew's four-chapter-long thread ("the outside hand") its landing.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat8Logic()`

All logic components parent to `[BEAT_8_LOGIC]`.

- **Maelgorn (presence):** `Ch16PlaceStoryNpc(Ch16MaelgornPrefab, (0, −30, 270), "Maelgorn")`, built inactive — a decorative/story NPC (no combat component), revealed by this beat's own Trigger step; this is distinct from the three combat-phase Maelgorn instances built separately for Beat 9.
- **The Hollow Kings:** `Ch16PlaceStoryNpc(Ch16HollowKingsPrefab, (−3, −30, 274), "The Hollow Kings")`, built inactive, same Trigger — per the class summary, "they never fight — ranged in hollow-crowned silence."
- **Sallow:** `Ch16PlaceStoryNpc(Ch16SallowPrefab, (−3, −30, 226), "Sallow")`, built inactive, activated by the *same* Trigger step as Maelgorn/Hollow-Kings even though her dialogue/narrative role (carrying the freed shadows) does not pay off until Beat 9 — she is present at the seam from this point forward. Canon fixes her physical read for that payoff precisely: dialogue-script line 221 stages her as "blank waxen face calm, **the empty cradle in its chest waiting**" — the literal chest-cradle Beat 9's liberation burst should target (see Beat 9.b/9.c). **Placement note:** this also puts her 44 m from the Maelgorn/Hollow-Kings cluster she is revealed alongside (z=226 vs. z=270–274), isolated during this beat's own "true enemy" dialogue (anchor z=234) — a deliberate pre-position for her Beat 9 role, not an unexplained placement, in the same "owned rather than silently accepted" spirit §5 already applies to Samurai-4/Khall.
- **Dialogue anchor:** `Dialogue_Beat8_TrueEnemy` at (0, −30, 234), set `ch16_beat8_true_enemy`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 32 | Trigger | `Maelgorn Rises` — activates `maelgornPresenceGo`, `hollowKingsGo`, `sallowGo` together |
| 33 | Dialogue | `The True Enemy Named (Ladder E converge)` — `ch16_beat8_true_enemy` |

**What changes during the beat:** three previously-inactive cast members appear simultaneously — the throne-core's furnace-orange `ThroneLight` accent is the visual anchor for Maelgorn's rise, already built and lit from Beat 5. Canon stages the rise itself as a dynamic light-swell, not a static reveal (see Beat 8.c's throne-rise VFX note below) — recommend gating a `ThroneLight` intensity ramp off this step's Trigger rather than treating the light as requiring no event.

#### c. Art & Environment Instantiation → `BuildBeat8Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Maelgorn (presence) | (0, −30, 270) | `Named.Maelgorn` | `…/Art/Generated/Characters3D/Named/Maelgorn.prefab` | **EXISTS** |
| The Hollow Kings | (−3, −30, 274) | `Named.TheHollowKings` | `…/Art/Generated/Characters3D/Named/The-Hollow-Kings.prefab` | **EXISTS** |
| Sallow | (−3, −30, 226) | `Named.Sallow` | `…/Art/Generated/Characters3D/Named/Sallow.prefab` | **EXISTS** |
| `HollowKingsLight` (accent, recommended) | (−3, −26, 274) | — | `ChapterEnvironmentProfile.accentLights["HollowKings"]` — cold white-blue | profile |

No prop instantiation this beat — every element is a cast reveal off an already-existing prefab.

**The Hollow Kings are lit backwards from canon (recommended fix).** The production note (dialogue line 592) is explicit the Hollow Kings must read "visibly lesser (cold white-blue seams to his furnace-orange)" against Maelgorn's "banked furnace-orange fault-light" (line 590). Today the only light anywhere near their position is `ThroneLight`'s furnace-orange (0, −22, 270), so they currently share Maelgorn's glow instead of contrasting with it — the exact opposite of "visibly lesser." `HollowKingsLight` (added above, ≈(−3, −26, 274), color near (0.45, 0.6, 0.9), low intensity) restores the canon hierarchy-of-power read and should be authored into `ChapterEnvironmentProfile.accentLights` alongside the existing ten entries (§3.1, §6, Appendix A.1).

**Author the throne-rise VFX (recommended, not yet wired).** The screenplay stages Maelgorn's reveal as dynamic, not static: "the banked furnace-orange light upon the forged throne brightens, uncoils, and rises. MAELGORN stands" (line 584), and the production note explicitly instructs "Author the throne-rise VFX" (line 592). Recommend the same treatment already prescribed elsewhere for lighting-only story beats — the loop-iteration lights (§4 Beat 6.f) and `ForgedThrone`'s cool-to-ash (§4 Beat 9.c, §9): a `ThroneLight` intensity ramp (banked → bright) driven off step 32's `Maelgorn Rises` Trigger — comfort-safe, lighting-only, no camera motion.

**This recommendation requires a matching change to `ThroneLight`'s own authored state, not just a runtime trigger.** Appendix A.1 currently records `ThroneLight` at a fixed intensity of 2.4, behaviour `none`, active from scene start (`Chapter16Builder.cs:172`) — full furnace-glow for the entire time the player crosses `ThroneCore` in Beats 5–7, before Maelgorn ever occupies the throne. A banked→bright ramp has nothing to animate unless `ThroneLight`'s initial (Beat-5) intensity is authored low/banked (≈0.8–1.0) and only raised to 2.4 when step 32's Trigger fires — otherwise the empty throne is already lit at full climax brightness through the loop and the forged-order beat, undercutting the reveal this ramp is meant to sell.

**Staging constraint — `Named.Maelgorn` needs the same presentation contract Beat 2.c gives `Named.Samurai4`.** Canon is just as specific about him: "a towering, crowned, eyeless obsidian sovereign veined with banked furnace-orange fault-light, edges bleeding shadow-smoke" (line 590), with the Hollow Kings' "cold white-blue seams" existing precisely as his contrast. `Named.Maelgorn` must carry the furnace-orange internal fault-veining and shadow-smoke edge read baked into the mesh/material itself, not left to the accent lights alone — and, per Beat 2.c's weapon note, a visible weapon for his three Beat 9 combat phases (`Ch16BuildNamedBoss` only synthesizes a bare, meshless reach-point rig; Appendix B's reuse note). This anchors the furnace-orange/cold-blue contrast in the character art, consistent with how this document already art-directs Samurai-4.

#### d. Combat

None. This is a confrontation/dialogue beat; the first Maelgorn combat phase does not begin until Beat 9.

#### e. Dialogue / VO

`Dialogue_Beat8_TrueEnemy`, set `ch16_beat8_true_enemy`, (0, −30, 234), **4 lines, ≈190 s**:

| Speaker | Line (abridged) | sec |
|---|---|---|
| Maelgorn | "So the blade arrives, sharpened on a grief I forged... Behind the Program that made you, there were the ten syndicates... on the throne at the bottom of everything, the Obsidian Synod, and at its head, myself. Maelgorn... the most precisely aimed." | 56 |
| Morrigan (comm) | "That's it. Soren, that's the thing. Listen to it. The outside hand... I'm naming it now. I'm hearing it... It ends there. On that throne. Cut its machine, Soren." | 45 |
| Maelgorn | "Your engineer flatters herself... I needed a blade I did not have to hold. So I broke one open and let it believe the breaking was its own... That is the only freedom I ever left you, and it was always mine." | 56 |
| Soren | "You keep calling me Cipher... Soren. Say it. You can't. And that's how I know you've already lost." | 33 |

#### f. Audio / Haptics / VR Comfort

- **No camera shake** — Maelgorn's "the menace all in the calm" register is explicitly a performance/VO direction, never a visual jolt.
- `ThroneCoreLight0`'s `ConsoleFlicker` continues running under this beat, giving the reveal a faint instability without any new scripted light event.
- Comfort vignette inert; stationary confrontation.

---

### Beat 9 — The Throne of Ashes (The Cage Broken on His Terms) — CLIMAX, closes Ladder E and the saga

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read the liberation-VFX light burst and the "CHAPTER 16 COMPLETE" canvas from `ArtAssetRegistry`/prefab conventions. Create **`BuildBeat9Art()`** (three sequential Maelgorn combat-phase instantiations, the liberation light burst, the complete canvas) and **`BuildBeat9Logic()`** (three `DefeatEnemies` steps with phase-transition dialogue between them, the liberation Trigger, the five closing dialogue sets, `ChapterOutro`).
> **This is a LETHAL fight — no `DuelYield` anywhere in Beat 9.** Maelgorn is the true enemy, not a mercy target; do not wire yield/mercy mechanics onto any of the three phase encounters.

#### a. Narrative purpose & emotional target

The finale, and it must land as **liberation, not destruction**. Heris hands Soren the choice her sabotage-key was built to allow — cut the seam to destroy (kills every kept shadow with the throat) or cut it to release (frees them alive, with Sallow's absolution-body taking the weight). Maelgorn presses for the destruction outcome he engineered; Soren refuses **both** wrong answers offered across the whole saga — Vale's conquest-path (Ch12, rejected) and Maelgorn's destruction-as-proxy — and names the third: *"I'm not going to destroy the Engine. I'm going to open it. Cut it to free them, not to kill them."* The liberation itself streams thousands of freed shadow-lights out through the breaking lattice; Sable and Cassie-04 each get a payoff beat from opposite angles (the felt wire vs. the closed ledger). Maelgorn, diminished but unbroken, refuses to concede the wider war. The true final refusal is Samurai-4's test of the empty throne — Soren does not climb it: *"The cage doesn't break when a better hand takes the throne. It breaks when the throne stays empty."* A cascade of closing beats (Mira, Resh, Coral Vex, Khall, Kessler) gives the saga's whole recruited roster a moment before Soren's own homecoming line and Echo's final speech, which closes the saga on the fully-learned name.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat9Logic()`

All logic components parent to `[BEAT_9_LOGIC]`.

- **Maelgorn, three combat phases**, each `Ch16BuildNamedBoss(Ch16MaelgornPrefab, pos, "Maelgorn (Phase N)", maelgornPNDef, playerHealth)`, built inactive, positioned progressively closer to the seam as he descends to stop the cut:
  - Phase 1: (0, −30, 260), `Ch16MaelgornPhase1` def (220 HP, 16 dmg)
  - Phase 2: (0, −30, 248), `Ch16MaelgornPhase2` def (260 HP, 19 dmg)
  - Phase 3: (0, −30, 236), `Ch16MaelgornPhase3` def (300 HP, 22 dmg — final)
  - `moveSpeed` and `attackCooldown` both escalate per phase (`1.3 + phase*0.02`, `0.9 - phase*0.05`) — deliberately increasing aggression as the fight progresses, mirroring a classic three-phase boss ramp. Each phase auto-activates as its own `DefeatEnemies` step's setup fires (no separate visible Trigger, matching the Wardens/every-mook convention).
  - **Presence hand-off (flagged, not yet wired).** `maelgornPresenceGo` (Beat 8's throne-seated NPC at (0,−30,270), activated at step 32) is never deactivated by the builder. Phase 1 spawns at (0,−30,260) — only 10 m away, identical prefab — so if Phase 1's `DefeatEnemies` setup activates its GameObject without also deactivating `maelgornPresenceGo`, two visually identical Maelgorns are onscreen simultaneously through Phase 1. Step 35's setup (or a step immediately before it) must `SetActive(false)` `maelgornPresenceGo`, or, more cheaply, reuse the presence GameObject itself as Phase 1 rather than spawning a fifth instance — so "descending from the throne to stop the cut" doesn't read as a visible duplication.
- **`EngineBreakLiberation`:** a parent object with three child `FreedShadowLight` point lights (offsets around the seam: (−2,−27,226), (2,−27,230), (0,−25,228); color (0.65, 0.85, 1), intensity 3, range 18, no shadows), built inactive, activated by a single Trigger step immediately after Phase 3 clears.
- **`CHAPTER 16 COMPLETE` canvas** (`Ch16BuildCompleteCanvas`, worldspace, (0, −28, 238), facing −Z, text "CHAPTER 16 COMPLETE / THE SAGA ENDS") and **`ChapterOutro`** at (0, −30, 236), inactive, `CampaignFlagSetter` flags `["ch16_complete", "galaxy1_complete", "samurai4_recruited", "khall_allied"]` — **the saga's very last flags**, all four set together at `OnActivated`.
- **The hub payoff these four flags unlock (out-of-scene, but the direct consequence — not resolved anywhere in this document otherwise).** `Chapter16Builder.cs`'s own class-summary comment (lines 105–108, "HUB INCREMENT") and `HubBuilder.cs:69–70, 370–375` show that `ch16_complete` gates a `FullyLit` root in the hub, and `Ch16FillFullyLit()` populates it with idle instances of `Named.Samurai4` and `Named.Khall` standing among the bright lights — the literal in-world payoff of `samurai4_recruited`/`khall_allied` and of the finale generally, mirroring the Ch2/Ch6/Ch7/Ch9/Ch10/Ch13 hub increments. Nothing in `Ch16_ThroneOfAshes.unity` itself shows this; a builder reproducing the chapter from a cold clone should know these four flags feed that hub state, not just a fade-to-black.
- **Dialogue anchors:** `Dialogue_Beat9_SeamChoice` (0,−30,226); `Dialogue_Beat9_PhaseTaunt1` (0,−30,255); `Dialogue_Beat9_PhaseTaunt2` (0,−30,244); `Dialogue_Beat9_Liberation` (0,−30,228); `Dialogue_Beat9_ThroneTest` (0,−30,232); `Dialogue_Beat9_ClosingCrew` (0,−30,233); `Dialogue_Beat9_Homecoming` (0,−30,234); `Dialogue_Beat9_EchoFinal` (0,−30,235) — eight distinct sets, all clustered within a few meters of the seam/throne, reflecting that the whole climax plays out in one fixed arena rather than traveling.
- **Samurai-4 and Khall are not physically present at this cluster** — see §5's immersion-cost note. `Dialogue_Beat9_ThroneTest`'s "The throne's open, Soren" and `Dialogue_Beat9_Homecoming`'s "Samurai, Khall, at my shoulder" both currently play with neither ally's mesh anywhere near the anchor.
- **The saga's full-roster convergence is voice-only — the largest instance of the CREW-PRESENCE DECISION, and the costliest.** The screenplay stages the whole recruited cast physically arriving for the climax, not just commenting on it: "the full roster spilling down the lattice-spars toward the seam" (dialogue line 614), and, moments later, "through the structure CORAL VEX, MERA VOSS, MORRIGAN, GRYPH, SABLE, CASSIE-04, VESS, RESH... above" (line 620). The build places **zero** of them — every one of these names delivers their Beat 9 lines as a comm voice-over, identical in kind to how the crew is handled for the Beat 0–8 descent (§4 Beat 0.b, "CREW-PRESENCE DECISION, mirroring Ch13's convention"). That convention is defensible for the descent, where the crew is genuinely elsewhere on the ship; it is not obviously defensible for the one moment canon explicitly stages them physically converging on the player. Recommend an explicit, deliberate choice rather than the current default-by-omission: either own the climax as VO-only (a legitimate budget/scope call, but state it), or add a cluster of static, non-interactive crew silhouettes along `LatticeSpar0/1` (z=220/236, §4 Beat 5.c) for Beats 8–9, so "everyone came down for this" is *seen* at the saga's final image, not just narrated over it. An empty cathedral behind a lone Soren undercuts the "he came up out of his descent with a galaxy" thesis (line 684) the whole chapter builds to.
- **Sallow's absolution-body — placed but silent (flagged, not yet resolved).** Sallow is activated by Beat 8's Trigger (step 32) at (−3, −30, 226) — directly beside the three `FreedShadowLight` points this beat's own liberation Trigger (step 40) activates at (−2,−27,226)/(2,−27,230)/(0,−25,228) — yet she carries zero dialogue lines anywhere in the chapter and no visual/mechanical tie to the liberation canon names her to carry ("Sallow's absolution body... converge to break the Engine," dialogue-script lines 18/78/146/194; this beat's own prose, §4 Beat 9.a, asserts "Sallow's absolution-body taking the weight" of the freed shadows). Canon fixes her precise physical read for exactly this moment: dialogue-script line 221 stages her as "blank waxen face calm, **the empty cradle in its chest waiting**" — the literal chest-cradle the freed shadow-lights are meant to stream into. Recommend one of two resolutions, made deliberately rather than left implicit: either build `Vfx.FreedShadowBurst`'s stream to visibly converge at/into that cradle — the three `FreedShadowLight` points sit only 2–4 m from her position, so the burst terminating at her chest rather than dispersing generically is cheap and canon-exact — or record plainly that her role is narration-only set-dressing at the climax. See §4 Beat 9.c and §9.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 34 | Dialogue | `The Seam Choice (the refusal)` — `ch16_beat9_seam_choice` |
| 35 | DefeatEnemies | `Maelgorn, Phase 1` |
| 36 | Dialogue | `Phase Transition 1` — `ch16_beat9_phase_taunt1` (short original bark) |
| 37 | DefeatEnemies | `Maelgorn, Phase 2` |
| 38 | Dialogue | `Phase Transition 2` — `ch16_beat9_phase_taunt2` (short original bark) |
| 39 | DefeatEnemies | `Maelgorn, Phase 3 (final)` |
| 40 | Trigger | `The Engine Breaks (liberation, not destruction)` — activates `EngineBreakLiberation` |
| 41 | Dialogue | `The Liberation (closes Ladder E)` — `ch16_beat9_liberation` |
| 42 | Dialogue | `The Throne Test (final refusal)` — `ch16_beat9_throne_test` |
| 43 | Dialogue | `The Closing Crew` — `ch16_beat9_closing_crew` |
| 44 | Dialogue | `The Homecoming` — `ch16_beat9_homecoming` |
| 45 | Dialogue | `Echo's Final Line` — `ch16_beat9_echo_final` |
| 46 | Trigger | `Chapter Outro (flags + fade + canvas)` — activates `outroGo`; `OnActivated` fires `CampaignFlagSetter.SetFlags` |

Step 46 both ends Beat 9 and ends the saga: `ChapterOutro.OnEnable` sets all four flags, reveals the complete canvas, fades to black, and publishes `ZoneCompleted` — the same signal `GameFlowManager`'s normal mission-complete handling listens for elsewhere in the game.

**What changes during the beat:** three sequential lethal boss encounters, then a single irreversible liberation Trigger, then five stationary dialogue sets carrying the entire cast to the saga's close. No further combat after Phase 3 clears.

**The seam-cut is diegetic-only, exactly like the cradle-touch (§4 Beat 4.b) — flagged so a builder doesn't wire a phantom interaction.** Soren's choice to "cut it to free them, not to kill them" (`ch16_beat9_seam_choice`, step 34) is spoken *before* the boss fight and resolved entirely through dialogue/VO pacing; mechanically the liberation fires as an automatic Trigger (step 40) the instant Maelgorn Phase 3's `Health` reaches zero (step 39), not from any cut-gesture. The `SeamMarker` prop (§4 Beat 5.c) is a sightline anchor only — it is never grabbed, cut, or otherwise interacted with, and no such mechanic should be added. The saga's single climactic "action" is gated on defeating Maelgorn, not on a scripted seam-cut.

#### c. Art & Environment Instantiation → `BuildBeat9Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Maelgorn Phase 1 | (0, −30, 260), Euler(0,180,0) | `Named.Maelgorn` | `…/Art/Generated/Characters3D/Named/Maelgorn.prefab` | **EXISTS** |
| Maelgorn Phase 2 | (0, −30, 248), Euler(0,180,0) | `Named.Maelgorn` | same prefab | **EXISTS** |
| Maelgorn Phase 3 | (0, −30, 236), Euler(0,180,0) | `Named.Maelgorn` | same prefab | **EXISTS** |
| `FreedShadowLight` ×3 (liberation burst) | (−2,−27,226), (2,−27,230), (0,−25,228) | `Vfx.FreedShadowBurst` | `…/Art/Generated/VFX/FreedShadowBurst.prefab` | **MISSING** *(currently three bare point lights, no particle/VFX asset)* |
| `ForgedThrone` (cools to ash) | (0, −29, 270) *(same object as Beat 5)* | `Props.ForgedThrone` | `…/Art/Generated/Props/ForgedThrone.prefab` | **MISSING** — see Beat 5's note on the runtime cool-to-ash requirement |
| `CHAPTER 16 COMPLETE` canvas | (0, −28, 238), facing −Z | — | `Ch16BuildCompleteCanvas` (code-generated UI, not a prefab) | code |

**Constraint the liberation VFX must respect.** The screenplay stages this as "thousands of points of light let go at once" streaming up and out through the breaking structure — three static point lights is a placeholder read, not the target fidelity. A `Vfx.FreedShadowBurst` prefab should be a particle system capable of a large, sustained stream, not a one-shot burst, since the dialogue continues for several more beats (Liberation → Throne Test → Closing Crew → Homecoming → Echo Final) with the screenplay's own stage direction noting "the freed shadows are still streaming out into open space" as late as the very last shot of the chapter. Cassie-04's Beat 5 line gives the sustained-stream requirement a more precise, character-owned fidelity target than the stage direction alone: *"I'll feel every single one let go, **one at a time**, the ones I was strung beside, my own make among them"* (dialogue-script line 246) — a continuous, individuated release, not a single flash. `Vfx.FreedShadowBurst` should read as that: many discrete points releasing in sequence over the sustained hold, not one undifferentiated glow.

**Sallow co-location, unexploited.** The three `FreedShadowLight` points (−2,−27,226)/(2,−27,230)/(0,−25,228) sit within 2–4 m of Sallow's own placement (−3,−30,226) — cheap to route the burst's visible origin through or at her position, specifically at **the empty cradle in her chest** (dialogue-script line 221), the literal image canon gives for "the absolution-body taking the weight" — so the moment reads as a staged event involving her, rather than three lights appearing beside an uninvolved bystander. See §4 Beat 9.b and §9.

**The seam itself is not an interactable.** `SeamMarker` (built in Beat 5, §4 Beat 5.c) is the only physical prop anchoring "the seam" in `ThroneCore`; Beat 9 adds no new geometry there and wires no grab/cut interaction to it — see Beat 9.b's diegetic-only note, which parallels Beat 4's cradle-touch treatment.

#### d. Combat

**The chapter's only lethal boss fight** (as distinct from Samurai-4's non-lethal `DuelYield` and the Khall-images' quick, repeated loop fights): three sequential `Enemy`/`Health` phases built from the same Named-mesh idiom as every other boss this chapter, escalating in both HP and aggression. Each phase is its own `DefeatEnemies` step with a short phase-transition bark between them — two brand-new, non-transcribed lines (*"You break like all the others. Differently. But you break."* / *"Come to the throne, then. Let us finish this where I built it."*), written in Maelgorn's established cold-regal register per `Chapter16Lines.cs`'s own class-summary note, since the source screenplay stages the throne-room confrontation as pure dialogue and never breaks it into discrete phases — the phase structure is this builder's own addition to satisfy the finale's GAME NARRATIVE DESIGN "final boss" requirement. **No camera shake at any point**, including the final phase transition into the liberation.

**The second bark's direction contradicts the phase geometry (flagged, not yet resolved).** "Come to the throne, then. Let us finish this where I built it" reads as Maelgorn inviting the player *up-core toward the throne* (z=270). But the three phase positions (§4 Beat 9.b) march the opposite way — 260 → 248 → 236 — descending *toward* the seam/player rather than retreating to it, and §5's own travel-route table reads this as "descending from the throne to stop the cut." The line asks for motion the geometry doesn't encode: nothing about the fight moves the player closer to z=270 after this bark plays. Reconcile one of two ways: either rewrite the bark to match the descent already staged ("I'll stop you at your seam, then" or similar), or, if the throne-ward read is preferred, re-stage Phase 2/3 to march up-core instead of down. An audible line that contradicts where the boss physically moves reads as a staging error in the saga's climactic fight — pick one and make the other agree with it.

#### e. Dialogue / VO

Eight sets close the chapter and the saga. The table below gives exact per-line durations, cross-referenced against the source script's `seconds:` fields (`Ch16_The_Throne_of_Ashes_Dialogue_Script.md`, BEAT 9), so the closing cascade — the saga's densest VO, 12 speakers across eight sets — can be scoped for a VO batch without re-deriving it from the script:

| Set | Speaker | Line (abridged) | sec |
|---|---|---|---|
| `ch16_beat9_seam_choice` | Heris | "The key is live and seated, Soren... If you cut it to release, the throat comes apart and the shadows come loose alive... Make it right." | 48 |
| `ch16_beat9_seam_choice` | Maelgorn | "Destroy it, Cipher. That is what you came for... Cut." | 22 |
| `ch16_beat9_seam_choice` | Soren | "No. You don't get this one either... I'm not going to destroy the Engine. I'm going to open it. Cut it to free them, not to kill them... This is the part you could never forge, Maelgorn." | 53 |
| `ch16_beat9_phase_taunt1` | Maelgorn | *(original, non-transcribed bark bridging Phase 1→2)* | — |
| `ch16_beat9_phase_taunt2` | Maelgorn | *(original, non-transcribed bark bridging Phase 2→3)* | — |
| `ch16_beat9_liberation` | Sable | "They're loose. Soren, they're loose... Look what I am tonight." | 38 |
| `ch16_beat9_liberation` | Cassie-04 | "I'm closing columns... For empty. For done. For all of them out." | 33 |
| `ch16_beat9_liberation` | Maelgorn | "You have not won. You have unmade one machine... I will outlast your mercy by ten thousand years." | 22 |
| `ch16_beat9_liberation` | Soren | "Maybe you will. I won't pretend one seam broke the whole war... I'm the first contingency you can't forge, Maelgorn, and there'll be more of me than you have years." | 28 |
| `ch16_beat9_throne_test` | Samurai-4 | "The throne's open, Soren. It's right there. You could take it... And that scares me. That you could. That we'd let you." | 25 |
| `ch16_beat9_throne_test` | Soren | "No. I climbed down a whole vault to stop being something other people sit on top of... The cage doesn't break when a better hand takes the throne. It breaks when the throne stays empty... The chair empty, and a galaxy of killers standing in front of it, choosing." | 47 |
| `ch16_beat9_closing_crew` | Mira (comm) | "You left the chair empty... I knew you wouldn't." | 18 |
| `ch16_beat9_closing_crew` | Resh | "I ran children out of those markets one hold at a time... tonight you closed it." | 18 |
| `ch16_beat9_closing_crew` | Coral Vex | "Fifty years, Soren... You're the one who made it stop being lonely." | 36 |
| `ch16_beat9_homecoming` | Khall | "I carried the order that broke you, Cipher. Soren. I'll learn it, same as your shadow is learning it... Thank you for letting me run the last road with you." | 42 |
| `ch16_beat9_homecoming` | Kessler | "Soren. I'm patching in from the top of the lift... Come home. That's the whole mission. It always was." | 35 |
| `ch16_beat9_homecoming` | Soren | "I'm coming up, Kessler... Samurai, Khall, at my shoulder. Everyone else, top of the lift... Take me up, Echo. One more time." | 38 |
| `ch16_beat9_echo_final` | Echo | "Soren. There. I said it like I meant it, and it only took me the whole way back up... Mercy was the rebellion. Choice is the victory... Let's go home." | 60 |

**Split-membership caveat (liberation set).** Soren's 28 s rebuttal to Maelgorn's refusal-to-concede line is grouped into `ch16_beat9_liberation` above (making it four lines, not the three its beat-summary prose implies) rather than into `ch16_beat9_throne_test`; this placement is inferred from the screenplay's own scene continuity, not independently re-verified line-by-line against the live `Chapter16Lines.cs` methods — the same hedge Beat 6's own split-membership caveat applies (§4 Beat 6.e). Confirm against the source before locking a final VO batch.

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point in the climax**, including the three boss-phase transitions, the liberation burst, and the throne cooling to ash — every one of these reads through `Haptics`, `AudioDirector`, `CombatFeedbackController`, and lighting/VFX only, consistent with the non-negotiable VR constraint holding all the way to the saga's final frame.
- `ThroneCoreAmbience`'s `throne_rumble.wav` bed should shift or fade as the Engine comes apart — no scripted audio transition is currently wired for this moment; flagged as inferred future scope (§9). Pair this explicitly with the recommended liberation swell (§7) so the hum dropping out and the swell rising are authored as one crossfade moment, not two independently-deferred TODOs — the sonic image is the cage's rumble replaced by thousands of freed voices, and that handoff is the single most important audio transition in the finale.
- Combat feel for all three Maelgorn phases is standard `BladeDamager`/`Haptics`/`AudioDirector` — reuse, don't reinvent, exactly as every prior boss in the saga.
- **A phase-transition reform/rise sting is recommended on the two transition barks (steps 36, 38) — the exact inversion of Beat 6.f's reset-sting reasoning.** Beat 6.f argues a distinct reset-sting is load-bearing there because each Khall-image "death" is mechanically the *player winning* while the fiction is Soren dying and resetting. Beat 9's three phases invert that: each `DefeatEnemies` clear (steps 35, 37) is a real kill of a boss who is canonically **not dead** — he "descends from the throne to stop the cut" and reforms closer to the seam (§4 Beat 9.b, §5). Without a sting distinct from a normal kill cue on steps 36/38's barks, each phase-death reads as "boss defeated, another appeared" rather than "he rises again, diminished." Recommended, not yet wired — same status as the reset-sting it mirrors.
- Comfort vignette engages normally through the boss phases' movement and snap-turns; the five closing dialogue sets are entirely stationary.

## 5. Character travel-route master table

Unlike Chapter 1 (where Kessler is the only NPC who physically travels, via the `NpcWalker` + `MissionDirector` Trigger idiom), **no NPC in Chapter 16 uses `NpcWalker` or rides waypoints.** This is a structural departure worth flagging explicitly:

| Character | Movement mechanism | Notes |
|---|---|---|
| Samurai-4 | **none — she does not move at all.** No `NpcWalker`, no follow behavior, no re-placement. One static mesh, frozen at (0, −12, 106) from Beat 2 onward. | No waypoints, and no fiction that she walks off-screen either: every later dialogue anchor (Beat 4's z=154, Beat 5's z=200+, Beat 8's z=234, Beat 9's z=226–270) is simply placed near where the *line* needs her to be, with her actual mesh 48–164 m away and never depicted arriving. This is not "presence achieved by proximity placement" — it is presence asserted by dialogue with nothing behind it. |
| Khall (loop images ×3, then the real Khall) | none — all four instances are placed at the identical fixed coordinate (0, −30, 228) and swapped active/inactive by Trigger steps | The "loop image thins and becomes the real man" effect is achieved by co-locating four separate GameObjects at one point and toggling visibility, not by a transform blend. **Each defeated image must be deactivated before the real Khall reveals — see Beat 6.b — or up to three dead ghost-selves are visible under him at the same coordinate.** |
| Maelgorn (presence, then three combat phases) | none — four separate GameObjects at four fixed positions (270 → 260 → 248 → 236), each toggled active by its own step | The "descending from the throne to stop the cut" read is implied by the four positions getting progressively closer to the seam (z=228), not by an animated walk. **The presence GameObject (z=270) is never deactivated when Phase 1 (z=260) activates — see Beat 9.b — so a hand-off step is required or two identical Maelgorns are visible at once.** |
| The Hollow Kings | none — static placement, revealed by the Beat 8 Trigger | Decorative presence only. |
| Sallow | none — static placement, revealed by the Beat 8 Trigger | **Un-flagged tension:** zero dialogue lines anywhere in the chapter (no `Speaker: Sallow` anywhere in the dialogue script), yet canon names her repeatedly as a load-bearing convergent climax force — story-beat Scene 10 ("Sallow's absolution"), dialogue-script lines 18/78/146/194 ("the absolution body (Sallow, Ally #10)... Sallow's absolution body... converge to break the Engine"). Beat 9.a's own prose asserts "Sallow's absolution-body taking the weight" of the freed shadows, but nothing in Beat 9.b/9.c dramatizes it — the liberation payoff lines go to Sable and Cassie-04 only. Her seam-side placement (z=226) also sits 44 m from the Maelgorn/Hollow-Kings reveal cluster (z=270–274) she is activated alongside in Beat 8 — a deliberate pre-position for this Beat 9 role, not an unexplained placement (§4 Beat 8.b). See §4 Beat 9.b/9.c and §9. |
| Wardens A/B, Khall-images | none — stationary `Enemy` AI at spawn, standard combat pathing only (not authored waypoints) | `Enemy`'s own internal chase/attack behavior is not a scripted route. |

**The full-roster convergence at the climax is the CREW-PRESENCE DECISION's single most expensive application.** Every crew member outside the physically-placed principal cast (Coral Vex, Mera Voss, Morrigan, Gryph, Sable, Cassie-04, Vess, Resh) is voice-only for the entire chapter, per the class-summary's stated convention. That reads as a reasonable, Ch13-mirrored choice for the descent, where the crew is genuinely elsewhere. It reads very differently at Beat 9, where canon stages them physically "spilling down the lattice-spars toward the seam" (dialogue line 614) to converge on the player for the saga's final image — see Beat 9.b for the specific recommendation (own the VO-only choice explicitly, or add static crew silhouettes at `LatticeSpar0/1`).

**Immersion cost, owned rather than silently accepted — and wider than the closing beats alone.** The two capstone allies the closing dialogue leans on hardest are also the two most physically absent from where they are said to stand, and the gap starts earlier than Beat 8. Samurai-4 has spoken lines from `ch16_beat4_soren` (anchor z=154) onward — the instant Beat 3 closes and she is asserted to be traveling with the player — with her mesh frozen at LowerTier's (0, −12, 106) for the rest of the chapter. The seam-approach/loop-entry beat sits squarely in this gap too: the dialogue script's Beat 5→6 transition stages "SOREN moves toward the bright seam-line along the lattice-spar, **SAMURAI-4 at his shoulder**, the crew converging from above" (dialogue line 485, z≈210–228) — the exact instant the Time-Loop trap springs — with her actual mesh still ~104–122 m up-core. By Beat 8's stage direction placing "SAMURAI-4 at his shoulders" alongside Khall before the throne, and Beat 9's "The throne's open, Soren... I'd follow you onto it" (dialogue line 664) and "Samurai, Khall, at my shoulder" (dialogue line 699), she is 120–164 m away and has been for five beats, not one. The real Khall's placement (0, −30, 228) is at least inside the Beat 9 cluster, though still well short of the throne itself (z=270) where the line is spoken. **Fix scope: Beats 4–9, not 8–9.** At minimum, add a second static placement for Samurai-4 (and Khall) at the seam/throne cluster, activated no later than Beat 4 (a second spawn deactivating or superseding the LowerTier instance the moment the leash-break closes), or give her a simple follow behavior from Beat 3 onward — the no-rails convention below should not be treated as a closed question for the finale's two most emotionally load-bearing allies, and should not be scoped to only the beats where she has the *most* lines when she is silently absent for the ones before it too.

**Y-invariant note (carried forward from every earlier chapter, but structurally inert here):** every `Ch16PlaceStoryNpc`/`Ch16BuildNamedBoss`/`Ch16PlaceGhostNpc` call re-adds `Vector3.up * pos.y` after `FitNamedCharacter` grounds the mesh (mirrors Ch9/Ch11's convention) — this is the mechanism that keeps every cast member's feet on their tier's actual floor-Y (0, −3, −9, −12, −18, −30, as appropriate) rather than on `FitNamedCharacter`'s locally-grounded Y of 0. Because no character in this chapter ever transitions between two different floor-Y values via a scripted walk, the classic "waypoint Y must equal floorY or they sink/float" regression (Ch1 §5) **cannot occur here** — every placement is a one-time static spawn at its own tier's Y, not a multi-waypoint drag. If a future patch *does* add an `NpcWalker` leg to this chapter (e.g. giving Samurai-4 a scripted walk-in for Beat 2 instead of relying on pre-placement), the standard Y-invariant discipline from Chapter 1 §5 applies in full and must be re-derived per tier.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat / Tier | Mood | Key/accent entry | Behaviour | Fog (recommended `fogPerZone[]`) | What changes during the beat |
|---|---|---|---|---|---|
| 0 — SpawnTier | cold, neutral, institutional | `accentLights["Spawn"]` | `None` | 0.012 (baseline, enclosed steel) | none — static launch point |
| 1 — UpperTierB | steel, wards, cold blue-white | `accentLights["UpperTier"]` | `None` | 0.012 (baseline, enclosed steel) | none — the two Wardens-A/B fights are the only state change |
| 1 — MidTierA / MidTierB | thinning, memory-gold seeping in | `accentLights["MidTier"]` | `None` | 0.009, warming | the floor/ceiling tint palette itself shifts steel→gold across this stretch — the beat's primary visual "juice" |
| 2/3 — LowerTier | near-pure memory, sourceless, brightest gold (×1.15) | `accentLights["LowerTier"]` | `None` | 0.006, warm | none lighting-wise; the duel and leash-break resolve entirely through dialogue/combat state |
| 4 — SepulcherFloor | open, warm, brightest gold (×1.3), tomb-still; **also the directional key's cold rake, unoccluded (roofless — §3.1)** | `accentLights["SepulcherFloor"]` | `AmbientPulse(7.8s)` | 0.005, warm | slow breathing pulse timed to the identity-reveal — structural echo of Ch1's `MedbayLight` 7 s pulse |
| 5 — ThroneCore (arrival) | monumental, cold violet lattice + furnace-orange throne; **also the directional key's cold rake, unoccluded (roofless — §3.1)** | `accentLights["ThroneCore"]`, `accentLights["Throne"]` | `ConsoleFlicker(seed: 151)` on the lattice lights | 0.002, cool — thin enough to sell the ~72 m sightline to `ForgedThrone` | the single largest tint jump in the chapter, timed to the reach-point crossing at the top of `TheBigDescent`; `Vfx.KeptShadowLattice` (§4 Beat 5.c) should populate here so the lattice reads as *full* before Beat 9 releases it |
| 6 — The Time-Loop | folds between cool blue-violet (normal) and hot red-magenta (fold) | `loopFoldAccents["Normal"/"Fold"]` | driven by `TimeLoopController.AdvancePhase()`, never automatic | 0.002, cool (unchanged from Beat 5) | **Trigger (steps 20, 30):** the fold swaps exactly twice, bracketing the three Khall-image fights — pure `SetActive` toggling, zero camera involvement |
| 7/8 — Forged Order, True Enemy | unchanged throne-core violet/orange; recommended cold white-blue `HollowKingsLight` contrast + `ThroneLight` banked→bright ramp on Maelgorn's rise | `accentLights["ThroneCore"]`, `["Throne"]`, `["HollowKings"]` (recommended) | `ConsoleFlicker` continues; `ThroneLight` ramp recommended, gated on step 32's Trigger | 0.002, cool (unchanged) | Beat 7: none. Beat 8, **Trigger (step 32):** cast reveal — recommend pairing it with `ThroneLight`'s intensity ramp and a new `HollowKingsLight` cold-blue accent so Maelgorn's rise and the Kings' "visibly lesser" contrast are lit, not just narrated (§4 Beat 8.c) |
| 9 — The Throne of Ashes | throne cools from furnace-orange to ash-cold; liberation burst | `accentLights["Throne"]`; `Vfx.FreedShadowBurst` | — | 0.002, cool (unchanged) | **Trigger (step 40):** three `FreedShadowLight` points activate around the seam and `Vfx.KeptShadowLattice` (populated since Beat 5) animates outward/thins to empty; `ForgedThrone`'s material must visibly cool across the closing dialogue sets. This is a **sustained state, not a one-frame event** — the screenplay's closing stage direction has "the freed shadows still streaming out into open space" running under all five remaining dialogue sets (Liberation → Throne Test → Closing Crew → Homecoming → Echo Final), so the VFX (and the recommended liberation audio swell, §7) must be scoped to hold through the entire closing cascade, not trigger-and-stop |

**Fog is currently the same baseline exponential bed in every beat** — a single profile value (0.18, 0.18, 0.24) density 0.012, never overridden per-tier (Appendix A.1). This flattens the throne-core's whole point: a 92 m-deep cathedral first seen from ~z=198, a 72 m sightline to `ForgedThrone` at z=270, is ~58% fogged at 0.012 exponential — the "monumental" reveal reads as murk, not vastness, and the steel→memory-gold→violet-cathedral progression gets no atmospheric differentiation to match its lighting one. Since `ChapterEnvironmentProfile` is being introduced precisely to data-drive the look, add the per-zone `fogPerZone[]` schema above (§3.1): the current density stays only for the enclosed steel tiers, thins and warms through memory-gold, and drops to near-zero and cools through the open `ThroneCore` cathedral.

## 7. Audio / VO manifest cross-reference

Twenty-two canonical dialogue sets, defined in `Chapter16Lines.cs` and consumed via `Chapter16Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position | Lines |
|---|---|---|---|
| `ch16_beat0_briefing` | 0 | (0, 1, 4) | 12 |
| `ch16_beat1_descent_upper` | 1 | (0, −3, 24) | 2 *(inferred split — see §4 Beat 1.e)* |
| `ch16_beat1_descent_lower` | 1 | (0, −9, 88) | 2 *(inferred split)* |
| `ch16_beat2_duel` | 2 | (0, −12, 100) | 8 |
| `ch16_beat3_leash_break` | 3 | (0, −12, 108) | 6 |
| `ch16_beat4_soren` | 4 | (0, −18, 154) | 5 |
| `ch16_beat5_throne_core` | 5 | (0, −30, 200) | 5 |
| `ch16_beat6_loop_taunt` | 6 | (0, −30, 212) | 1 |
| `ch16_beat6_loop_iter1` | 6 | (0, −30, 228) | 2 |
| `ch16_beat6_loop_iter2` | 6 | (0, −30, 228) | 1 |
| `ch16_beat6_loop_break` | 6 | (0, −30, 228) | ~1 *(inferred split — see §4 Beat 6.e)* |
| `ch16_beat6_loop_concede` | 6 | (0, −30, 228) | ~1 *(inferred split)* |
| `ch16_beat7_forged_order` | 7 | (0, −30, 230) | 10 |
| `ch16_beat8_true_enemy` | 8 | (0, −30, 234) | 4 |
| `ch16_beat9_seam_choice` | 9 | (0, −30, 226) | 3 |
| `ch16_beat9_phase_taunt1` | 9 | (0, −30, 255) | 1 *(original, non-transcribed)* |
| `ch16_beat9_phase_taunt2` | 9 | (0, −30, 244) | 1 *(original, non-transcribed)* |
| `ch16_beat9_liberation` | 9 | (0, −30, 228) | 4 *(split-membership caveat — §4 Beat 9.e)* |
| `ch16_beat9_throne_test` | 9 | (0, −30, 232) | 2 |
| `ch16_beat9_closing_crew` | 9 | (0, −30, 233) | 3 |
| `ch16_beat9_homecoming` | 9 | (0, −30, 234) | 3 |
| `ch16_beat9_echo_final` | 9 | (0, −30, 235) | 1 |

Each is built by the local `Ch16BuildDialogue` wrapper (mirrors Ch1's `BuildChapter1Dialogue` pattern): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch16WireVoiceClips`, resolving each line's `AudioClip` from `Chapter16Lines.ClipName(setId, index, speaker)` — pattern `ch16_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all twenty-two `DialoguePlayer`s.

**Dialogue is data, not art.** None of this changes in the refactor — the twenty-two set ids, their positions, and the clip-resolution pattern are canon.

SFX bed, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip | Used for |
|---|---|
| `SFX/throne_rumble.wav` | `ThroneCoreAmbience` layer at (0, −29, 270), range 5–18, vol 0.5 |
| `SFX/throne_rumble.wav` *(recommended second emitter, not wired)* | a low secondary emitter near the top of `TheBigDescent` (≈(0, −24, 170)), so the "vast and humming" hum swells across the 32 m no-cut descent instead of only becoming audible once the player is already on top of `ForgedThrone` — see §4 Beat 5.f |
| `SFX/steel_vault_hum.wav` *(recommended, not wired)* | cold vault/HVAC ambience bed for the enclosed steel tiers — one `BuildAmbienceLayer` at SpawnTier (0, 0, 6) and UpperTierB (0, −3, 30), range 5–14, vol 0.35 |
| `SFX/memory_tone.wav` *(recommended, not wired)* | distinct sourceless memory-space tone bed for the gold/memory tiers — one `BuildAmbienceLayer` per tier at MidTierA (0, −6, 54), MidTierB (0, −9, 78), LowerTier (0, −12, 110), and **SepulcherFloor (0, −18, 154)** — the fourth emitter closes the ~28 m dead zone past LowerTier's own range-16 falloff so the cradle-reveal apex isn't silent (§4 Beat 4.f) — range 5–16, vol 0.4, crossfading from `steel_vault_hum` as the player descends |

**The two roofless shells need a manually-placed `AudioReverbZone`, not just an ambience `AudioSource` (§1.6).** `AutoTagInteriorVolumes()` skips any tier without a matching ceiling, so `SepulcherFloor` and `ThroneCore` never get auto-tagged into a reverb volume. `ThroneCoreAmbience` (above) is a plain `AudioSource` playing `throne_rumble.wav` — it is a room-tone bed, not spatial reverb, and a future audio pass should not mistake it for a substitute. Recommend hand-placing two `AudioReverbZone`s: a large cathedral/cave preset centered at `ThroneCore` (0, −28, 244), and a smaller memory-space preset at `SepulcherFloor` (0, −18, 154). Cheap, and it makes the "monumental" reveal (§3) sound monumental, not drier than the enclosed vault above it.

No door-slide, alarm, or hologram-reveal SFX exist in this chapter (there are no doors and no Khall-hologram moment — Khall is a real physical reveal here, not a hologram as in Ch1 Beat 4). A dedicated relay-severing sting (Beat 3), a loop-reset sting (Beat 6), an Engine-liberation swell (Beat 9), and the two ambience beds above are all recommended but **not currently wired** — see §9. The chapter's entire thesis is a sonic/architectural *peel* — "clinical Program steel... cold institutional cleanliness" giving way to "corridors [that] run with light that is not lit by anything" (SETTING block) — and today only `ThroneCoreAmbience` gives that arc an audio layer at all; `steel_vault_hum`/`memory_tone` (reusing `BuildAmbienceLayer` the same way) would make the tint gradient (§3.1) audible, not just visible.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 16 — The Throne of Ashes** (`XRRigBuilder.BuildChapter16ThroneOfAshes()`).
2. **EditMode is the gate.** Confirm against the current repository baseline before and after any change (baseline drifts release-to-release — check `Project/Docs/CHAPTER-BUILD-LEDGER.md` for the number in force at commit time; do not hardcode a stale figure into a patch). Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot (same shape as Ch1's).** No EditMode test invokes `BuildChapter16ThroneOfAshes()` or loads `Ch16_ThroneOfAshes.unity`. Whatever pure-logic coverage exists (`Chapter16LinesTests.NoLine_CallsSamurai4ANewerMake`, `Chapter16LinesTests.SomeLine_ContainsSorenReveal`, referenced in `Chapter16Lines.cs`'s own comments) covers dialogue-data invariants only, not the scene build. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it — doubly important here given the scene's ten-tier size and the number of inactive-until-Trigger GameObjects that are easy to silently leave orphaned by a partial edit.
3. **Cast-reveal regression coverage (new, recommended).** Given this chapter's heavy reliance on the "instantiate inactive, `Trigger`-activate later" idiom (Khall ×4 instances, Maelgorn ×4 instances, Samurai-4, the Hollow Kings, Sallow, both fold-relay pairs, the liberation VFX, the outro), a build-time assertion that every inactive GameObject referenced by a `Trigger`/`ReachTrigger` step is reachable by exactly one step (never zero, never two) would catch the class of bug this chapter is most exposed to. No such test exists today.
4. **Safe-zone survival test (new, mirrors Ch1 §8.4).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
5. **Fallback audibility test (new, mirrors Ch1 §8.5).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox descent** (Appendix A geometry, all ten tiers, all five ramps) plus one `LogWarning` per unresolved key — never an empty shaft, never an exception. Given every Named-cast prefab already resolves (Appendix B), this test's real value is catching a regression in the *tier/prop* fallback path, not the cast.
6. **Perf reference bar — does not yet exist for this chapter.** Unlike Ch1 (drawCalls 189, tris 9,198 recorded 2026-07-02), **no `UnityStats` baseline has been recorded for Ch16.** Recording one at greybox on first successful build, and re-measuring after every subsequent geometry or lighting change, is a prerequisite — not optional — given §1.6's assessment that this is the largest, most performance-hostile scene in the saga.
7. **Console check:** `Ch16WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild, across all twenty-two sets.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception — identical to Ch1's.** Re-running `BuildChapter16ThroneOfAshes()` wipes generated content. Patch additively in the live editor, or fix `Chapter16Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** The ~288-unreferenced-material-variant caution from `Project/Docs/IMPROVEMENT-SUMMARY.md` applies chapter-wide, not just to Ch1 — this scene's ten tiers of `TintShared`-batched primitives are exactly the kind of asset that audit flags. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Same standing prevention rule as every other chapter (§ of the Ch1 companion document; `Project/Docs/IMPROVEMENT-SUMMARY.md`). This chapter's tier shells and ramps are the geometry most likely to tempt an FBX import with "Generate Colliders" left on, given their scale — check on every prop landing in the registry.
- **Open-platform edge guard — flagged, not yet implemented (VR safety, must-fix).** `SepulcherFloor` and `ThroneCore` are the chapter's only two floor-only shells (`Ch16BuildFloorOnly`, §2) and both ship with no wall/ceiling geometry by design. Their footprints are far smaller than `ZoneBounds`' single radius-200 sphere (§2 — center (0,−15,150)), so nothing today actually stops a player walking off either platform's real edge (`SepulcherFloor` x=±7, `ThroneCore` x=±16) into the void — a fall-through-floor bug during Beat 4's stationary reveal, and a real VR-comfort hazard during Beat 9's frantic three-phase Maelgorn fight. Both shells need an **invisible, knee-to-waist-height collision lip at the footprint edge** — comfort-safe, non-visual, and consistent with "the architecture stops being architecture" (it must read as nothing, not as a wall) — before either scene ships. See §4 Beat 4.c and Beat 5.c.
- **No `ChXX_Prologue` ship-scene precedes this chapter — flagged, not resolved.** Every other chapter in the saga (per this repository's own convention, documented in `CLAUDE.md` and the chapter-restructure memory) enters via a `ChXX_Prologue` ship scene: a Kessler briefing aboard the Cairn followed by a descent into the mission scene proper. Chapter 16's Beat 0 briefing plays **inside** `Ch16_ThroneOfAshes.unity` itself, on `SpawnTier`, rather than in a separate `Ch16_Prologue` scene — there is no `Ch16PrologueBuilder` anywhere in `Scripts/Editor/`. Whether this is a deliberate finale exception (the saga's last briefing folded directly into the last descent, avoiding one more scene transition right before the climax) or an unaddressed gap in the chapter-restructure pass is **not resolved by this document** — do not silently "fix" it by adding a prologue scene without confirming intent first.
- **The seam-cut — the saga's single climactic action — is diegetic-only, not a scripted interaction (flagged so a builder doesn't wire a phantom mechanic).** Unlike the equivalent note already carried for the memory-cradle touch (§4 Beat 4.b), Beat 9's seam-cut had no equivalent flag before this pass: Soren's "cut it to free them, not to kill them" (`ch16_beat9_seam_choice`, step 34) is spoken before the boss fight, and the liberation fires as an automatic Trigger (step 40) the instant Maelgorn Phase 3 dies (step 39) — the `SeamMarker` prop (§4 Beat 5.c) is a sightline anchor only. See §4 Beat 9.b/9.c.
- **"Coming back up that lift" is spoken at the bottom and resolved by fade only — flagged, not resolved.** The lift motif is given a diegetic anchor at Beat 0 (`Props.DescentLock`, §4 Beat 0.c), but the homecoming cascade leans on it hardest at the very end: Soren's "I'm coming up, Kessler... top of the lift" and Echo's "it only took me the whole way back up" (`ch16_beat9_homecoming`/`ch16_beat9_echo_final`) are both delivered roughly 260 m deep at the throne, after which `ChapterOutro` fades to black on the complete-canvas at z=238 (§4 Beat 9.b). The player never physically ascends. As with the no-`Ch16_Prologue` question above, this document does not resolve whether that's a deliberate fade-covered beat (acceptable) or unbudgeted scope for a visual/audio payoff of the saga's final emotional line — flag it as a conscious choice to make, not a silent omission.
- **The relay-severing action (Beat 3) now has a `Props.SignalRelay` prop with a position (§4 Beat 3.c); its spark/relay-death VFX and haptic cue, the loop-reset sting (Beat 6), the Engine-liberation audio swell (Beat 9), and the steel-vault/memory-tone ambience beds (Beat 0/1, §7) are all described by the screenplay/production notes as discrete moments but are not currently backed by dedicated SFX/VFX assets** — flagged individually in each beat's subsection above. This is new scope for an audio/VFX pass, not a defect in the current build. The Engine-liberation audio swell specifically should be scoped as a **sustained bed held through the closing cascade** (see §6's Beat 9 row), not a single trigger-and-stop flash — it needs to still be audible under the last five dialogue sets.
- **The `ForgedThrone`'s "cools to ash" requirement (Beats 5 and 9) has no confirmed implementation mechanism.** The builder instantiates one static-tinted primitive; nothing in `Chapter16Builder.cs` currently swaps its material or drives a runtime color blend at the climax. Whoever authors `Props.ForgedThrone` needs either a two-state material swap wired to the outro Trigger, or a `RendererTint`-driven blend — the choice is unresolved and should be made deliberately, not defaulted.
- **Sallow is placed but silent — the chapter's most under-realized principal (flagged, not yet resolved).** Her GameObject activates at Beat 8's Trigger (step 32) at (−3,−30,226), co-located with Beat 9's `FreedShadowLight` liberation cluster, yet she has zero dialogue lines anywhere in the chapter and no visual or mechanical tie to the liberation canon names her to carry (story-beat Scene 10 "Sallow's absolution"; dialogue-script lines 18/78/146/194, with line 221 fixing her physical read as "the empty cradle in its chest waiting"). Resolve deliberately, not by default: either build `Vfx.FreedShadowBurst` to visibly stream into that chest-cradle so "the absolution-body taking the weight" (Beat 9.a) is shown as the concrete image canon gives it, or record that her role is narration-only set-dressing at the climax. See §4 Beat 9.b/9.c and §5.
- **Samurai-4's mask removal (Beat 3) has the same class of gap.** Beat 3.e's canon line, "Take your mask off. You don't need it anymore," is spoken as a real, visible action, but `Named.Samurai4`'s prefab has no documented detachable or hideable half-mask sub-mesh, and no component in `Chapter16Builder.cs` toggles one. Either the character prefab needs a mask sub-mesh that can be disabled/swapped at the leash-break (mirroring the `ForgedThrone` two-state-swap-or-`RendererTint`-blend choice above), or the line is understood to be spoken to an unchanged face — the choice is unresolved and should be made deliberately, not defaulted.
- **The three Maelgorn combat phases are original scope, not a screenplay-mandated structure.** Per `Chapter16Lines.cs`'s own class-summary comment, the source screenplay stages the throne-room confrontation as pure dialogue and a refusal with no discrete combat phases; the three-phase boss and its two original transition barks exist specifically to satisfy the finale's GAME NARRATIVE DESIGN section's "final boss: Maelgorn / Obsidian Synod" requirement. Any future rebalance of Maelgorn's HP/damage curve is free to reshape this without touching canon dialogue.
- **AUDIT FIX #6 is load-bearing and must not regress.** `story ouput/audit/Ch16_audit.md` graded the source script C- on naturalness with exactly one hard consistency error: a line originally called Samurai-4 a "newer make" than Ronin-7, implying an undocumented fifth Program lineage. The fix (§4 Beat 1.e) is already applied in `Chapter16Lines.cs`; any future rewrite of that line must preserve "same make... current issue" phrasing. The audit's remaining findings (saga-wide antithesis-tic density, aphorism-stacking, Echo reciting the Story Bible's theme line near-verbatim) are explicitly deferred to a saga-wide naturalness pass, not a per-chapter fix — do not attempt to silently "clean up" this chapter's dialogue against those findings without that wider pass being commissioned. **The fix was applied to the spoken line only — the SETTING block itself (dialogue-script line 45) still reads "newer-make and sleeker than him" and was never corrected.** §4 Beat 2.c cites that same SETTING block as Samurai-4's art-direction source, so an artist reading it at face value re-imports the exact retconned "newer model" read the dialogue fix removed; see the warning added there.
- **No memory of a Quest/PCVR performance pass exists for this scene.** Given §1.6's assessment (seven simultaneous boss-rig-caliber `Enemy` instances possible across the chapter's life, ten accent lights, a throne-core ambience layer, `ReverbZonePlacer` auto-tagging every enclosed tier), a per-tier quality-tier gate (Quest drops something PCVR keeps, per the project's standing per-tier rendering convention) has not yet been designed for this specific scene and should be scoped before a hardware pass, not discovered during one.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All tier shells, ramps, and non-cast props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials. Every Named-cast character, by contrast, is already a real Tripo-pipeline mesh — this chapter has no primitive-capsule placeholder cast members anywhere.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch16Environment.asset`. Currently set inline at the top of `BuildChapter16ThroneOfAshes` (`Chapter16Builder.cs:149–172`).

| | Value |
|---|---|
| Directional key | color (0.75, 0.8, 0.9), intensity 0.55, rotation Euler(50, −30, 0) |
| Ambient | mode **Flat**, color (0.14, 0.14, 0.18) |
| Fog | mode **Exponential**, color (0.18, 0.18, 0.24), density 0.012 |
| Steel floor tint (SpawnTier, UpperTierB) | (0.8, 0.83, 0.88) |
| Steel ceiling tint | (0.86, 0.88, 0.92) |
| Gold floor tint (MidTierA, MidTierB) | (0.5, 0.44, 0.32) |
| Gold ceiling tint | (0.56, 0.5, 0.38) |
| LowerTier floor/ceiling tint | gold × 1.15 → (0.575, 0.506, 0.368) / (0.644, 0.575, 0.437) |
| SepulcherFloor tint | gold × 1.3 → (0.65, 0.572, 0.416) |
| ThroneCore floor tint | (0.16, 0.14, 0.22) *(independent violet-black, not gold-derived)* |
| Ramp tint (all five ramps, every tier) | (0.42, 0.4, 0.36) — a flat warm gray-brown, **not** tier-tinted |

**Accent point lights** (`BuildAccentPointLight`/`Ch16BuildAccentLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour | Read |
|---|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.82, 0.86, 0.95) | 1.2 | 10 | none | cold neutral launch point |
| `UpperTierLight0` | (−3, 2.2, 26) | (0.7, 0.78, 0.95) | 1.3 | 14 | none | steel, cold blue-white |
| `UpperTierLight1` | (3, 2.2, 32) | (0.7, 0.78, 0.95) | 1.3 | 14 | none | steel, cold blue-white |
| `MidTierLight0` | (−3, −5.8, 50) | (0.85, 0.72, 0.5) | 1.3 | 14 | none | memory-gold seeping in |
| `MidTierLight1` | (3, −8.8, 78) | (0.88, 0.75, 0.5) | 1.4 | 14 | none | memory-gold |
| `LowerTierLight0` | (0, −11.8, 106) | (0.92, 0.8, 0.55) | 1.6 | 16 | none | sourceless memory-light, brightest gold |
| `SepulcherFloorLight` | (0, −17.8, 154) | (0.95, 0.85, 0.6) | 1.8 | 16 | `AddAmbientPulse(period: 7.8f)` | tomb-still, breathing warmth |
| `ThroneCoreLight0` | (−8, −25, 220) | (0.35, 0.28, 0.55) | 2 | 26 | `AddConsoleFlicker(seed: 151f)` | lattice violet, unstable |
| `ThroneCoreLight1` | (8, −25, 250) | (0.35, 0.28, 0.55) | 2 | 26 | none | lattice violet |
| `ThroneLight` | (0, −22, 270) | (0.95, 0.5, 0.2) | 2.4 | 22 | none | Maelgorn's furnace-orange |
| `HollowKingsLight` *(recommended addition — not in `Chapter16Builder.cs` today)* | (−3, −26, 274) | (0.45, 0.6, 0.9) | 1.1 | 14 | none | cold white-blue, "visibly lesser" contrast to `ThroneLight`'s furnace-orange (§4 Beat 8.c) |

**Recommended lighting addition not yet in code:** beyond the `HollowKingsLight` row above, a `ThroneLight` intensity ramp (banked → bright) gated on step 32's `Maelgorn Rises` Trigger is recommended to author the canon throne-rise VFX — see §4 Beat 8.c. **This ramp needs its own starting value, not just an end value.** The `ThroneLight` row above records its *current, as-built* state — fixed intensity 2.4, behaviour `none`, on from scene start — which is the full-bright climax value, not a banked starting point; authoring the ramp means changing this row's Beat-5 intensity to ≈0.8–1.0 (banked) in addition to adding the step-32-gated rise to 2.4, or the throne reads at full furnace-glow through the whole of Beats 5–7 with nothing left for the ramp to animate.

**Time-Loop fold-accent pairs** (`Ch16BuildAccentLight`, referenced by `TimeLoopController.phaseGroups`):

| Light | Position | Color | Intensity | Range | Active at scene start? |
|---|---|---|---|---|---|
| `LoopNormalAccent0` | (−4, −28, 228) | (0.4, 0.55, 0.9) | 1.4 | 12 | **yes** (phase 0 = normal) |
| `LoopNormalAccent1` | (4, −28, 228) | (0.4, 0.55, 0.9) | 1.4 | 12 | **yes** |
| `LoopFoldAccent0` | (−4, −28, 228) | (0.9, 0.2, 0.35) | 1.8 | 14 | no — toggled on by `AdvancePhase()` |
| `LoopFoldAccent1` | (4, −28, 228) | (0.9, 0.2, 0.35) | 1.8 | 14 | no |

### A.2 Beat 0 — The Cairn / SpawnTier

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[−6,6], z[−2,14], top-center (0, 0, 6), 12×16 | `Ch16BuildTier(world, "SpawnTier", (0,0,6), (12,0,16), steelFloor, steelCeil)` |
| Walls | `SpawnTier_WallW` (−6,1.8,6); `SpawnTier_WallE` (6,1.8,6); `SpawnTier_WallS` (0,1.8,−2) — no north wall (open to RampA) | `BuildWall(world, name, pos, (0.2, RoomH, 16))` |
| Accent light | `SpawnLight` (0,2.4,4), (0.82,0.86,0.95), i1.2, r10 | `BuildAccentPointLight` |
| Katana "Echo" | (2,1,4), Euler(−90,0,0) | `BuildSword(..., Ch16EchoBladePrefab)` — `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` |
| Player rig | `BuildRig(addLocomotion:true)` + `EchoPresence` + `AttachPlayerAbilities`; `ZoneBounds` center (0,−15,150) r200 | — |
| Dialogue player | `Dialogue_Beat0_Briefing` (0,1,4), set `ch16_beat0_briefing`, 12 lines | `Ch16BuildDialogue` |
| Mission steps | indices 0–1 of 47 | `AuthorDialogueStep(0, dlgBriefing)`, `AuthorReachStep(1, upperTierReachGo, 5f)` |

### A.3 Beat 1 — The Descent (RampA through LowerTier's approach)

| Object / Method | Value |
|---|---|
| `RampA` | `Ch16BuildRamp(world, "RampA", (0,0,14), (0,−3,22), 12)` |
| UpperTierB | `Ch16BuildTier(world, "UpperTierB", (0,−3,30), (12,0,16), steelFloor, steelCeil)`; walls W/E |
| `ConditioningWard0/1` | `BuildProp(world, name, (−4.5,−2.5,28)/(4.5,−2.5,32), (1.2,1.6,0.4), steelCeil)` |
| `RampB` | `Ch16BuildRamp(world, "RampB", (0,−3,38), (0,−6,46), 12)` |
| MidTierA | `Ch16BuildTier(world, "MidTierA", (0,−6,54), (12,0,16), goldFloor, goldCeil)`; walls W/E |
| `MemoryDoorway` | `BuildProp(world, "MemoryDoorway", (0,−5.4,58), (2.4,2.6,0.3), goldCeil)` |
| `RampC` | `Ch16BuildRamp(world, "RampC", (0,−6,62), (0,−9,70), 12)` |
| MidTierB | `Ch16BuildTier(world, "MidTierB", (0,−9,78), (12,0,16), goldFloor, goldCeil)`; walls W/E |
| `SealedCradleEcho0/1` | `BuildProp(world, name, (−5,−8.4,76)/(5,−8.4,80), (0.6,1.2,0.6), goldCeil)` |
| `RampD` | `Ch16BuildRamp(world, "RampD", (0,−9,86), (0,−12,94), 12)` |
| Warden A ×3 | `BuildEnemy(pos, playerHealth, wardenDef)`, positions (−3,−3,27),(3,−3,29),(0,−3,33); built inactive |
| Warden B ×4 | same, positions (−3,−9,75),(3,−9,77),(0,−9,81),(−2,−9,82); built inactive |
| Accent lights | `UpperTierLight0/1`, `MidTierLight0/1` — see A.1 |
| Reach points | `UpperTierReachPoint` (0,0,14) r5; `MidTierBReachPoint` (0,−6,62) r5; `LowerTierReachPoint` (0,−9,86) r5 |
| Dialogue players | `Dialogue_Beat1_DescentUpper` (0,−3,24) `ch16_beat1_descent_upper`; `Dialogue_Beat1_DescentLower` (0,−9,88) `ch16_beat1_descent_lower` |
| Mission steps | indices 2–7 of 47 | `AuthorDialogueStep`/`AuthorDefeatStep`/`AuthorReachStep` |

### A.4 Beat 2/3 — Samurai-4, the Duel and the Leash-Break (LowerTier)

| Item | Value | Source |
|---|---|---|
| LowerTier | `Ch16BuildTier(world, "LowerTier", (0,−12,110), (12,0,32), goldFloor*1.15, goldCeil*1.15)`; walls W/E length 32 | `Chapter16Builder.cs:221` |
| `RampE` | `Ch16BuildRamp(world, "RampE", (0,−12,126), (0,−18,142), 12)` | — |
| Samurai-4 boss rig | `Ch16BuildNamedBoss(Ch16Samurai4Prefab, (0,−12,106), "Samurai-4", samurai4Def, playerHealth)`; `CapsuleCollider` center (0,1.1,0) h2.4 r0.5; faces Euler(0,180,0); built inactive | `Chapter16Builder.cs:287` |
| `DuelYield` | `opponent`=her Health, `disableOnYield`=[her Enemy], `sword`=player Grabbable, `yieldThreshold` 0.22, `autoAcceptSeconds` 30 | on Samurai-4 GO |
| `Samurai4_LeashBreak` child | `LeashBreakController`: `startingConviction` 1, `breakThreshold` 0.25, `convictionDrainPerSecond` 0.5, `evidenceDrainAmount` 0.34, `autoAdvance` true; built inactive | child of Samurai-4 GO |
| `Ch16Samurai4` def | 260 HP, 14 dmg, moveSpeed 1.5, attackCooldown 0.8 | `Ch16EnsureSamurai4Definition()` |
| Dialogue players | `Dialogue_Beat2_Duel` (0,−12,100) `ch16_beat2_duel`; `Dialogue_Beat3_LeashBreak` (0,−12,108) `ch16_beat3_leash_break` | — |
| Mission steps | indices 8–13 of 47 | `AuthorDialogueStep`/`AuthorTriggerStep`/`AuthorPromptStep` |

### A.5 Beat 4/5 — Reclaiming Soren, Into the Throne-Core

| Element | Coordinates / value | Component / method |
|---|---|---|
| SepulcherFloor (open, no walls/ceiling) | top-center (0,−18,154), 14×24 | `Ch16BuildFloorOnly(world, "SepulcherFloor", (0,−18,154), (14,0,24), goldFloor*1.3)` |
| `MemoryCradle` | (0,−17.4,154), scale (1.4,1,1.4), color (0.95,0.85,0.55) | `BuildProp` |
| Soren (Memory) ghost | (0,−18,156), all renderers → `MemoryFlashbackController.MakeGhostMaterial()` | `Ch16PlaceGhostNpc(Ch16SorenSelfPrefab, ...)` |
| `TheBigDescent` ramp | `Ch16BuildRamp(world, "TheBigDescent", (0,−18,166), (0,−30,198), 14)` — width from `Ch16FloorHalfWidth`, not `Ch16ThroneCoreHalfWidth` | `Chapter16Builder.cs:233` |
| ThroneCore (open, no walls/ceiling) | top-center (0,−30,244), 32×92 | `Ch16BuildFloorOnly(world, "ThroneCore", (0,−30,244), (32,0,92), (0.16,0.14,0.22))` |
| `LatticeSpar0/1` | (−10,−22,220)/(10,−22,236), scale (0.8,16,0.8), color (0.3,0.55,0.75) | `BuildProp` |
| `SeamMarker` | (3,−29.4,228), scale (0.3,1.2,0.3), color (1,0.9,0.4) | `BuildProp` |
| `ForgedThrone` | (0,−29,270), scale (2.2,2.4,2.2), color (0.1,0.08,0.1) | `BuildProp` |
| Accent lights | `SepulcherFloorLight`, `ThroneCoreLight0/1`, `ThroneLight` — see A.1 | — |
| `ThroneCoreAmbience` | (0,−29,270), `throne_rumble.wav`, range 5–18, vol 0.5 | `BuildAmbienceLayer` |
| Reach points | `SepulcherFloorReachPoint` (0,−12,130) r5; `ThroneCoreReachPoint` (0,−18,168) r6 | — |
| Dialogue players | `Dialogue_Beat4_Soren` (0,−18,154) `ch16_beat4_soren`; `Dialogue_Beat5_ThroneCore` (0,−30,200) `ch16_beat5_throne_core` | — |
| Mission steps | indices 14–17 of 47 | — |

### A.6 Beat 6 — The Time-Loop Trap

| Item | Value | Source |
|---|---|---|
| `RealityFold` / `TimeLoopController` | `phaseInterval` 999999, `damageWhenOutOfPhase` false; `phaseGroups[0]`={normalAccent0,normalAccent1}, `phaseGroups[1]`={foldAccent0,foldAccent1}; stays active from scene start | `Chapter16Builder.cs:332` |
| `EnterFoldRelay` / `ExitFoldRelay` | `ActivationRelay`, `OnEnabled` → `timeLoop.AdvancePhase`; both built inactive | `Chapter16Builder.cs:350` |
| Khall-image ×3 | `Ch16BuildKhallImage(khallImageDef, playerHealth, ghostMat, iteration)` → `Ch16BuildNamedBoss(Ch16KhallPrefab, (0,−30,228), "Khall (Loop Image N)", ...)`, all renderers → ghost material; built inactive | `Chapter16Builder.cs:758` |
| `Ch16KhallImage` def | 90 HP, 12 dmg, moveSpeed 1.4, attackCooldown 0.9 | `Ch16EnsureKhallImageDefinition()` |
| Real Khall | `Ch16PlaceStoryNpc(Ch16KhallPrefab, (0,−30,228), "Khall")`; built inactive | `Chapter16Builder.cs:368` |
| Dialogue players | `Dialogue_Beat6_LoopTaunt` (0,−30,212); `LoopIter1/2` and `LoopBreak/Concede` all at (0,−30,228) | — |
| Mission steps | indices 18–30 of 47 | — |

### A.7 Beat 7/8 — The Forged Order, The True Enemy

| Element | Coordinates / value | Component / method |
|---|---|---|
| Maelgorn (presence) | (0,−30,270); built inactive | `Ch16PlaceStoryNpc(Ch16MaelgornPrefab, ...)` |
| The Hollow Kings | (−3,−30,274); built inactive | `Ch16PlaceStoryNpc(Ch16HollowKingsPrefab, ...)` |
| Sallow | (−3,−30,226); built inactive | `Ch16PlaceStoryNpc(Ch16SallowPrefab, ...)` |
| Dialogue players | `Dialogue_Beat7_ForgedOrder` (0,−30,230); `Dialogue_Beat8_TrueEnemy` (0,−30,234) | — |
| Mission steps | indices 31–33 of 47 | — |

### A.8 Beat 9 — The Throne of Ashes

| Element | Coordinates / value | Component / method |
|---|---|---|
| Maelgorn Phase 1/2/3 | (0,−30,260)/(0,−30,248)/(0,−30,236), Euler(0,180,0); built inactive | `Ch16BuildNamedBoss(Ch16MaelgornPrefab, ...)` ×3 |
| `Ch16MaelgornPhase1/2/3` defs | 220/260/300 HP, 16/19/22 dmg, moveSpeed `1.3+phase*0.02`, attackCooldown `0.9-phase*0.05` | `Ch16EnsureMaelgornPhaseDefinition(phase, hp, dmg)` |
| `EngineBreakLiberation` | 3× `FreedShadowLight` at (−2,−27,226)/(2,−27,230)/(0,−25,228), color (0.65,0.85,1), i3, r18, no shadows; built inactive | `Chapter16Builder.cs:394` |
| `CHAPTER 16 COMPLETE` canvas | (0,−28,238), facing −Z, "CHAPTER 16 COMPLETE / THE SAGA ENDS"; built inactive | `Ch16BuildCompleteCanvas` |
| `ChapterOutro` | (0,−30,236); `CampaignFlagSetter` flags `[ch16_complete, galaxy1_complete, samurai4_recruited, khall_allied]`; built inactive | `ChapterOutro` |
| Dialogue players | eight sets, (0,−30,226)–(0,−30,255) — see §7 | — |
| Mission steps | indices 34–46 of 47 | — |

### A.9 Scene root hierarchy (current)

`BuildChapter16ThroneOfAshes()` creates these as **siblings**, not nested: `Directional Light`, `IronSepulcher` (all ten tier shells, five ramps, props, the full cast, dialogue players, reach points), ten accent lights plus two fold-accent pairs, `Game` (`GameState` + `CombatFeedbackController`), the player rig, six reach points, twenty-two dialogue-player roots, the `CHAPTER 16 COMPLETE` canvas, `ChapterOutro`, `RealityFold` (+ its two `ActivationRelay`s), `ThroneCoreAmbience`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and ten `[BEAT_N_LOGIC]` roots (Beat 0 through Beat 9), and moves `IronSepulcher`'s art contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Every Named-cast character key resolves; every environment/prop/VFX key is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Samurai4` | `Art/Generated/Characters3D/Named/Samurai-4.prefab` | **EXISTS** |
| `Named.Maelgorn` | `Art/Generated/Characters3D/Named/Maelgorn.prefab` | **EXISTS** |
| `Named.Khall` | `Art/Generated/Characters3D/Named/Khall.prefab` | **EXISTS** |
| `Named.TheHollowKings` | `Art/Generated/Characters3D/Named/The-Hollow-Kings.prefab` | **EXISTS** |
| `Named.RoninCipherSoren` | `Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab` | **EXISTS** |
| `Named.Sallow` | `Art/Generated/Characters3D/Named/Sallow.prefab` | **EXISTS** |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Enemies.SepulcherWarden` | `Art/Generated/Characters3D/Enemies/SepulcherWarden.prefab` | MISSING |
| `Rooms.SepulcherTier_Steel` | `Art/Generated/Rooms/SepulcherTier_Steel.prefab` | MISSING |
| `Rooms.SepulcherTier_Gold` | `Art/Generated/Rooms/SepulcherTier_Gold.prefab` | MISSING |
| `Rooms.SepulcherTier_MemoryPure` | `Art/Generated/Rooms/SepulcherTier_MemoryPure.prefab` | MISSING |
| `Rooms.SepulcherFloor_MemorySpace` | `Art/Generated/Rooms/SepulcherFloor_MemorySpace.prefab` | MISSING |
| `Rooms.ThroneCore_Cathedral` | `Art/Generated/Rooms/ThroneCore_Cathedral.prefab` | MISSING |
| `Rooms.DescentRamp_Standard` | `Art/Generated/Rooms/DescentRamp_Standard.prefab` | MISSING |
| `Rooms.DescentRamp_Grand` | `Art/Generated/Rooms/DescentRamp_Grand.prefab` | MISSING |
| `Props.ConditioningWard` | `Art/Generated/Props/ConditioningWard.prefab` | MISSING |
| `Props.MemoryDoorway` | `Art/Generated/Props/MemoryDoorway.prefab` | MISSING |
| `Props.SealedCradleEcho` | `Art/Generated/Props/SealedCradleEcho.prefab` | MISSING |
| `Props.MemoryCradle` | `Art/Generated/Props/MemoryCradle.prefab` | MISSING |
| `Props.LatticeSpar` | `Art/Generated/Props/LatticeSpar.prefab` | MISSING |
| `Props.SeamMarker` | `Art/Generated/Props/SeamMarker.prefab` | MISSING |
| `Props.ForgedThrone` | `Art/Generated/Props/ForgedThrone.prefab` | MISSING |
| `Props.SignalRelay` | `Art/Generated/Props/SignalRelay.prefab` | MISSING |
| `Props.NurseryEcho` | `Art/Generated/Props/NurseryEcho.prefab` | MISSING |
| `Props.DescentLock` | `Art/Generated/Props/DescentLock.prefab` | MISSING |
| `Props.DescentMapHolo` | `Art/Generated/Props/DescentMapHolo.prefab` | MISSING *(optional)* |
| `Vfx.MemoryFaces` | `Art/Generated/VFX/MemoryFaces.prefab` | MISSING |
| `Vfx.KeptShadowLattice` | `Art/Generated/VFX/KeptShadowLattice.prefab` | MISSING |
| `Vfx.FreedShadowBurst` | `Art/Generated/VFX/FreedShadowBurst.prefab` | MISSING |

**Reuse notes.**

- `Rooms.DescentRamp_Standard` serves all four short ramps (A–D); `Rooms.DescentRamp_Grand` is reserved for `TheBigDescent` alone, given its roughly 4× length. Do not collapse these into one key without confirming the art can tile/stretch convincingly at both scales.
- `Rooms.SepulcherTier_Steel` serves SpawnTier and UpperTierB; `Rooms.SepulcherTier_Gold` serves MidTierA and MidTierB; LowerTier gets its own `Rooms.SepulcherTier_MemoryPure` key rather than reusing Gold, since its tint is a distinct ×1.15 brightness variant and its footprint (32 m deep vs. every other enclosed tier's 16 m) is also non-standard.
- **Every combat-rig NPC in this chapter (Samurai-4, all three Khall-images, all three Maelgorn phases) is built by synthesizing an `ArmR/Sword/Blade/BladeTip` chain onto the Named mesh at runtime (`Ch16BuildNamedBoss`), not by resolving a separate "combat variant" registry key.** There is no `Enemies.Samurai4` or `Enemies.Maelgorn` key distinct from the `Named.*` key — the boss rigging is code, not art, and stays that way in the target state; do not invent redundant registry entries for it. This chain is a **pure empty-GameObject transform hierarchy — no mesh, no material** — it exists only to give `Enemy`/`BladeDamager` a reach point and contributes zero visible geometry. Every visible weapon in this chapter's boss fights (Samurai-4's canon twin blade in particular — §4 Beat 2.c) therefore has to be modeled into the corresponding `Named.*` prefab itself, not expected from the synthesized rig.
- `Enemies.SepulcherWarden` is the one enemy-art gap in an otherwise fully-cast-covered chapter — today `BuildEnemy` uses whatever generic mook mesh/rig that shared helper defaults to (see `ChapterSharedBuilders.cs`'s own `BuildEnemy` implementation for the current fallback shape); a dedicated Warden design is new scope — see §4 Beat 1.c for the art-direction brief (impersonal, faceless, institutional — the un-reachable contrast to Samurai-4's hitch), not just a generic mook silhouette.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`); every Ch16 Named-cast member is already baked. Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter16Builder.cs`, `Chapter16Lines.cs`, `ChapterSharedBuilders.cs`, `story ouput/Ch16_The_Throne_of_Ashes.md`, `story ouput/Ch16_The_Throne_of_Ashes_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`.*

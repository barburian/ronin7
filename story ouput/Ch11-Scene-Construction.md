# Chapter 11 — Scene Construction

*The architectural contract for `Ch11_GhostsAndOrigins.unity`: what Chapter 11 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 11 ("Ghosts and Origins") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing spatial invariants, mission triggers, or the dreamscape-dive lifecycle.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter11Builder.cs`, entry point `XRRigBuilder.BuildChapter11GhostsAndOrigins()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 11 — Ghosts and Origins**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch11_Ghosts_and_Origins.md`, `Ch11_Ghosts_and_Origins_Dialogue_Script.md`, `Chapter11Lines.cs`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** Aldric's monumental great-blade duel, the Dreaming Archive's second kill, and the dream's narcosis pressure are all sold by `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never by moving the camera.
- **Traversal in Ch11 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no climb/wall-run** anywhere in this chapter. The one exception to "pure walking" is the **instant, non-lerped rig teleport `MemoryDiveController` performs on dive entry/exit** — a hard cut, not a locomotion mode, and comfort-safe by design (no camera motion, an instant position/rotation set).
- **No NPC in this chapter ever physically walks.** Unlike Ch1/Ch9/Ch10's `NpcWalker`+`Trigger` idiom, every named character this chapter is either voice-only over comm (the whole crew pool) or a stationary anchor placed once (Kira, the Younger Self, Aldric, the Dreaming Archive). §5 documents this explicitly as a chapter-wide invariant, not an omission.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | canyon tiers/ramps, dreamscape room shells, props, VFX, ghost-anchor placement *(the physical object)*, decorative lights | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | dive entry/exit state, enemy spawns (Aldric, the ghost-manifestations, the Dreaming Archive), reach points, dialogue players, ability-grant triggers, mission-spine steps | `[BEAT_N_LOGIC]` |

**The dreamscape island itself spans both.** `BuildBeatNArt()` (Beat 2's) instantiates `Dreamscape`'s three rooms (`DreamscapeArena`/`ArkshipCore`/`Cradle`) and the ghost/boss placements and returns their handles; `BuildBeatNLogic()` wires `MemoryDiveController`, the entry/exit triggers, and the `DefeatEnemies`/`Trigger` steps that drive it. Art builds the rooms and the cast; logic decides when the dream takes the player and when it lets go.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildAmbienceLayer`, `BuildEnemy`, `Author*Step`, `AttachPlayerAbilities` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, and every shared runtime component (`MemoryDiveController`, `MemoryFlashbackController`, `MemoryDiveEntryTrigger`/`ExitTrigger`, `DreamPhantom`, `DreamReckoningTrigger`, `AbilityGranter`, `UnbrokenWard`) — these are chapter-agnostic and reused by Ch3, Ch7, Ch8, and Ch11 alike.
- **Free to restructure:** the Ch11-local helpers, called only from `BuildChapter11GhostsAndOrigins()` — `Ch11EnsureGhostManifestationDefinition`, `Ch11EnsureAldricDefinition`, `Ch11EnsureDreamingArchiveDefinition`, `Ch11BuildDialogue`, `Ch11WireVoiceClips`, `Ch11PlaceGhostNpc`, `Ch11BuildNamedBoss`, `Ch11BuildTier`, `Ch11BuildRamp`, `Ch11BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs` or the `MemoryDiveController` family, stop — you have left Chapter 11 and are now silently rebuilding every chapter that reuses a memory-dive island.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two ScriptableObjects should carry everything the builder currently types inline, matching the pattern already documented for Ch1/Ch8/Ch9:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch11Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, per-tier accent lights, dreamscape room accent lights, `MemoryFlashbackController`'s fog/ambient/heartbeat treatment |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document |

Neither exists yet for Ch11. Prefab **paths never appear in builder code.** The builder asks the registry for `Props.LeviathanRib`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Aldric, Kira, Ronin-7_Cipher_Soren, The-Dreaming-Archive, Echo all resolve)
  Rooms/                                    ← new (tiers, dreamscape shells)
  Props/                                    ← new (ribs, ramps, arkship hull, hologram dressing)
  VFX/                                      ← new (ghost-manifestation dissolve, broadcast-haze)
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter11GhostsAndOrigins()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter11Builder.cs:106`). That call discards the entire scene and opens a fresh empty one; there is nothing left to search for afterward. Making the safe zone real requires **replacing the wipe strategy** with `EditorSceneManager.OpenScene(Ch11ScenePath)` + `DestroyImmediate` on each generated root by name (`BoneCanyon`, `Dreamscape`, `Game`, `Mission`, the rig, the accent lights, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched, falling back to `NewScene` only when the scene file does not yet exist. `EnemyArtWirer.cs`/`CrowdArtWirer.cs` already open shipped scenes in place, mutate them idempotently, and `SaveScene` — reuse that pattern.
>
> **Naming note for the by-name wipe.** The names above are the **parent roots**; the primitives inside them carry their own suffixed names and are covered only transitively. `BuildFloorCeiling` names its two cubes `{name}_Floor`/`{name}_Ceiling` (so `SpawnGround` never exists as a bare GameObject — only `SpawnGround_Floor` and `SpawnGround_Ceiling`, both children of `BoneCanyon`), and `Ch11BuildTier` keeps the bare name on the tier floor itself (`Tier1`, `Tier2`, `Tier3`, `Threshold`) while its two rib props are `{name}_RibA`/`{name}_RibB`. Destroying `BoneCanyon`/`Dreamscape` by name removes all of this correctly — nothing breaks — but a future implementer extending this list should search for the root names above, not a literal `SpawnGround` GameObject, which does not exist.
>
> **Dreamscape-specific hazard:** the safe zone must survive a rebuild whether or not the dive was ever entered during the editor session that authored hand-tuned art in it — `Dreamscape`'s subtree (art destined for `[STATIC_ART_DO_NOT_DELETE]`) is built `SetActive(false)` from scene construction and stays that way until a player crosses the threshold at runtime. Do not gate the safe-zone preservation logic on `diveRoot.activeSelf`; an inactive dive island is still real content.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for Ch11.** No rib prop, no ramp prefab, no dreamscape room shell, no ghost-manifestation VFX. See Appendix B for the full inventory: five keys resolve (the four Named-cast prefabs plus Echo); everything else is a commission.

A builder that instantiates from an empty registry produces **an empty canyon** — the first run of the refactored builder would destroy Chapter 11.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_LeviathanRib);
if (prefab == null) {
    Debug.LogWarning($"[Ch11] {ArtKey.Props_LeviathanRib} unresolved — primitive fallback.");
    Ch11BuildRibPrimitive(staticArtRoot, pos, color);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping elsewhere (`ChapterSharedBuilders.cs:623`, `if (prefab == null) continue; // not baked yet`). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No Ch11-specific greybox `UnityStats` baseline has been recorded yet.** `Project/Docs/CHAPTER-BUILD-LEDGER.md` tracks Ch11's EditMode test contribution (+20 fixtures: `UnbrokenWard` 8, `Chapter11Lines` 8, `HealthTests` interceptor ×4, running total 506 as of 2026-07-03) but carries no draw-call/tris row the way it does for the Ch1 hub. **Establish one at the first `script-execute` `UnityStats` read of the fresh build** and record it here before any prefab swap — do not invent a number.
- **Two distinct live scenes to measure, not one.** The real bone-canyon (`BoneCanyon`) and the dreamscape island (`Dreamscape`) are never both fully active at once — `diveGo.SetActive(false)` keeps the entire dream subtree dormant until `MemoryDiveController.EnterDive()` fires, and `ExitDive()` deactivates it again. Measure both states independently: the canyon-only baseline (spawn through the threshold) and the dreamscape-active baseline (post-dive, all three rooms + ghost-manifestations + Aldric live). A perf regression that only shows up once the dive is entered will not appear in a canyon-only smoke test.
- Set-dressing today is cheap tinted primitives (`TintShared`/`GameObject.CreatePrimitive`) — the tier floors, ribs, ramps, and dreamscape walls all share this convention. **Prefabs replacing them must carry their own materials and will not batch this way.** Re-measure after every prefab lands, same as every other chapter's rule.

## 2. Chapter spatial map

Chapter 11 is **one scene with two disjoint spatial regions**, `Assets/Ronin7/Scenes/Ch11_GhostsAndOrigins.unity`: the real **bone-canyon** (`BoneCanyon`, world z ≈ −2 to 78, descending in Y as the player drops) and the **dreamscape island** (`Dreamscape`, offset wholesale to world z ≈ 298–364 at `dive.position = (0, 0, 300)`), connected only by the `MemoryDiveController` teleport — there is no walkable path between them. Both regions run along the same local +Z convention (spawn → deeper) so the spatial *logic* reads consistently even though the geometry is not contiguous.

```
REAL CANYON (descending in Y as Z increases) — no branching, no return route except the climb-out
 SpawnGround ──ramp── Tier1 ──ramp── Tier2 ──ramp── Tier3 ──ramp── Threshold
 (0,0,4) 16x12        (2,-1,22) 12x12 (-2,-2,40) 12x12 (1,-3,58) 12x12  (0,-4,72) 12x12
 z[-2,10] y=0          z[16,28] y=-1   z[34,46] y=-2    z[52,64] y=-3    z[66,78] y=-4

                                    [MemoryDiveController.EnterDive() — instant teleport, comm cuts]

DREAMSCAPE ISLAND (offset to world z+300, floor y=0 throughout) — one continuous dive, exited once
 DreamscapeArena ── ArkshipCore ── Cradle
 (0,0,316) 20x36     (0,0,342) 16x16  (0,0,357) 14x14
 world z[298,334]    world z[334,350] world z[350,364]

                                    [MemoryDiveController.ExitDive() — instant teleport back to ThresholdReachPoint area, comm restored]
```

| Beat | Region | Footprint | Floor center / size | Floor Y |
|---|---|---|---|---|
| 0 (briefing) | *(voice-only — no built room; see §Beat 0c)* | — | — | — |
| 1 (descent) | `BoneCanyon`: SpawnGround → Tier1 → Tier2 → Tier3 → Threshold | x[-8,8]→[-6,6] across five platforms, z[-2,78] | centers (0,0,4)/(2,-1,22)/(-2,-2,40)/(1,-3,58)/(0,-4,72) | 0 / −1 / −2 / −3 / −4 |
| 2 (ghosts, Younger Self, Aldric duel) | `Dreamscape` → `DreamscapeArena` | x[-10,10], z[298,334] (world) | center (0,0,316), 20×36 | 0 |
| 3 (origin reveal) | `Dreamscape` → `ArkshipCore` | x[-8,8], z[334,350] (world) | center (0,0,342), 16×16 | 0 |
| 4 (second kill, exit, homecoming) | `Dreamscape` → `Cradle`, then back to `BoneCanyon` Threshold | x[-7,7], z[350,364] (world); Threshold x[-6,6] z[66,78] | center (0,0,357), 14×14; (0,-4,72), 12×12 | 0; −4 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**. The dreamscape's three enclosed rooms use it for a ceiling as expected — but so, less obviously, does `SpawnGround`: it is built via the shared `BuildFloorCeiling` helper (Appendix A.2), which unconditionally emits a dark, mesh-only ceiling cube at `RoomH` regardless of the `size.y` argument passed in, so the spawn platform is a low, dark-roofed alcove, not open air (§Beat 0c has the fix scoped to that row). Only the **tiers and `Threshold`** — built by the floor-only `Ch11BuildTier` helper, which never calls `BuildFloorCeiling` — are genuinely ceiling-less open-air platforms (a canyon, not a corridor).

**These footprints are load-bearing and survive the refactor unchanged.** A tier or room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience. `Ch11TierHalfWidth = 6f` sizes every canyon platform identically (12×12); do not vary it per-tier without updating every downstream ramp/light/reach-point offset that assumes it.

**Ramps, not gaps.** Each tier connects to the next via a single solid, gently-sloped ramp (`Ch11BuildRamp`), never a jump or a gap requiring a traversal ability:

| Ramp | From → To | Rise | Run | Length | Grade |
|---|---|---|---|---|---|
| `Ramp0` | SpawnGround (0,0,10) → Tier1 (2,-1,22) | −1 m | 12 m | ≈12.04 m | ≈4.8° |
| `Ramp1` | Tier1 (2,-1,22) → Tier2 (-2,-2,40) | −1 m | 18 m | ≈18.03 m | ≈3.2° |
| `Ramp2` | Tier2 (-2,-2,40) → Tier3 (1,-3,58) | −1 m | 18 m | ≈18.03 m | ≈3.2° |
| `RampThreshold` | Tier3 (1,-3,58) → Threshold (0,-4,72) | −1 m | 14 m | ≈14.04 m | ≈4.1° |

**Candor note on the traversal-kit gap.** The beat treatment's production note calls for "climbs and traverses along ribs and spinal processes, gaps crossed on fallen bone and arkship wreckage… Phase-step (Ch10) available and cued here as a traversal aid (blink across bone-gaps)." The as-built canyon has **no gaps at all** — every tier-to-tier transition is a single continuous ramp, walkable on ordinary continuous locomotion with no ability required. Phase-step is present on the rig (self-gating on `CampaignState.HasAbility`, see §1.1) but nothing in the canyon's geometry calls for it; it remains simply *available*, matching the production note's permissive framing ("Phase-step is available and should be cued") rather than its more specific "blink across bone-gaps" image, which is unbuilt. This is consistent with the project's VR-comfort stance (gentle continuous-locomotion-friendly grades over parkour), so it is not a regression to fix casually — flagging it here as a narrative/build gap, not proposing a change.

**Reach-trigger gates** — four, the chapter's only progression gates besides the two `DefeatEnemies` steps and the dive triggers:

| Reach point | Position | Radius | Gates | Fires mission step |
|---|---|---|---|---|
| `MidCanyonReachPoint` | (1, -2, 58) *(Tier3 + (0,1,0))* | 5 m | the Arkship dialogue | step 2 |
| `ThresholdReachPoint` | (0, -3, 72) *(Threshold + (0,1,0))* | 5 m | `EnterDreamscapeTrigger` | step 4 |
| `ArkshipCoreReachPoint` | (0, 1, 340) *(dive.position + (0,1,40))* | 6 m | the origin-reveal dialogue | step 14 |
| `CradleReachPoint` | (0, 1, 352) *(dive.position + (0,1,52))* | 5 m | the Mercy dialogue | step 17 |

**Player rig:** `BuildRig(refs, addLocomotion: true)` + `EchoPresence` + the full ability chain (`AttachPlayerAbilities`: weakpoint-sight, `OverdriveController`, `PhaseStepController`, `UnbrokenWard` — see §Beat 2f — plus `MirrorSummonController`, self-gated and harmlessly present as Ch12's own ability; full five-entry list in A.5). `ZoneBounds` is set to **center (0, 0, 150), radius 260** — one bounding sphere loosely enclosing both regions (farthest real-canyon point `SpawnGround` (0,0,4) ≈ 146 m from center; farthest dreamscape point `Cradle` (0,0,357) ≈ 207 m from center — both comfortably inside the 260 m radius).

## 3. Global environment & backdrop

**The leviathan bone-canyon** reads as a ravine carved through a fossilized body rather than stone — ribs arching overhead, the spine as canyon floor, vertebrae the size of buildings, an ancient arkship of antique Program-metal impaled in the bone. Per the dialogue script's SETTING block, "the deeper the player goes, the less the rules hold" — the builder executes this as a straightforward **color and light-intensity gradient with depth**, not a geometry change: `Ch11BuildTier`'s per-tier color lerps from bone `(0.5, 0.48, 0.44)` toward a cold vault-blue `(0.24, 0.3, 0.4)` across the three tiers, and the five accent lights (`SpawnLight` → `ThresholdLight`) both cool in color and rise in intensity (1.0 → 1.6) with depth — "the light is the cold grey of a dream you cannot wake from" told through palette, not new prop types, the same economy Ch9's rack-row tint gradient uses.

**Candor note — dust is this chapter's signature medium and is currently absent from the build entirely.** Canon hammers dust relentlessly and specifically, not as generic haze: "the dust of ages packed into every joint," "bone and dust and silence," "packed mineral dust" (script 201/249/251). It is not decorative texture — it is the literal diegetic mechanism of the dream taking the player. At the dive threshold, "the dust is rising. Shapes are forming in it" (script 292); through Beat 2, "the dust still rises" (391); at the exit, "the dead go to dust" (443). The ghost-manifestations, Kira, and the Younger Self are canonically *made of* the rising dust — this is the connective sensory tissue between "fossil graveyard" and "the dead wearing faces," and no dust-mote VFX exists anywhere in the current build (haze/fog is authored via `RenderSettings` fog only). Two commissions close this: `Vfx.CanyonDustMotes` — ambient particulate drifting slowly through the accent-light beams down the length of the canyon, visible wherever a `Ch11BuildTier`/`SpawnLight`-style accent throws a shaft of light through the gloom — and `Vfx.RisingDustForms` — the threshold-and-dreamscape "dust rising into shapes" effect, staged at the dive entry and reprised (fainter, ambient) through `DreamscapeArena`, selling the idea that the ghosts are coalescing out of, and will eventually return to, the same rising dust. See Beat 1c, Beat 2c, and Appendix B.

**Candor note — the architecture reveal is nearly unbuilt.** The narrative's central environmental beat is "the architecture tells the reveal… this is older than the Dominion, older than the syndicates, the place the Program came from." The only physical prop standing in for the arkship is a single `ArkshipHull` block at `tiers[2] + (-6, 2, 6)` — one 3×4×10 tinted cube, visible from Tier 3 on. The SETTING block is more specific about how that hull should read than "impaled in the bone" alone suggests: it describes "a hull of antique Program-original metal **grown into the leviathan's cage of ribs like a splinter the body tried and failed to heal over**… half-swallowed by ages of **dust and mineral**" (script 27–30) — the hull should read as *fused into and overgrown by* the bone and dust-caked, not cleanly speared through it. This is meaningfully different, buildable art direction for `Props.ArkshipHullFragment` (Appendix B), and it ties the hull directly to the dust commission immediately above — the hull is "half-swallowed by dust," the same medium the ghost-manifestations coalesce out of. There is no rib/vertebra prop distinct from the tier floors themselves (the tiers *are* the ribs and spine, told entirely through color and the `Ch11BuildTier`'s two small `_RibA`/`_RibB` accent props per tier — small 0.6×1.4×0.6 blocks, not the "vaulting of a drowned cathedral" scale the SETTING block calls for), and no hull-breach geometry for "re-entry into the arkship hull through ancient breaches." The chapter's single most load-bearing visual idea — an entire dead leviathan the size of a mountain range — is currently told through five accent lights, three tinted floor platforms, and one hull block. Flagging this as the largest greybox gap in the chapter, worth prioritizing in any future art pass over smaller prop dressing.

**Candor note — the skull-dome dive threshold has no geometry of its own.** The chapter's single most important spatial transition — real canyon into dream — is canon-named specifically, not just described: Sable's Beat 1 Arkship line locates the node "in the skull, where the dark pools" (`ch11_beat1_arkship`, §Beat 1e), and the builder's own step-9 comment labels the gate literally, `"ReachTrigger: The Threshold (skull-dome, comm about to cut)"` (`Chapter11Builder.cs:368`). But `Threshold` (§2, §Beat 1c) is built by the same `Ch11BuildTier` helper as every other canyon platform — a flat 12×12 tinted primitive, no dome, no skull-mouth silhouette, nothing that reads as a portal the player dives *through* rather than a platform they merely stand on before the teleport fires. This document's own Beat 1a environment description never uses the word "skull" either — it survives only in the dialogue line and the builder's internal step label, not in any geometry or in this document's own prose. This is a distinct gap from the architecture-reveal note above: that note is about the canyon reading as a fossil at all; this one is about the single named threshold at the canyon's heart having no matching geometry — a natural `Props.SkullDomeThreshold` commission (see Appendix B), worth naming on its own rather than folding into the general ribs/vertebrae/hull gap.

**Candor note — the dim, failing node-pulse is canon's descent beacon and climax, and it is currently unbuilt as both a light and a sound.** The pulse is named repeatedly and specifically, not as ambient color: "the broken node a dim failing pulse strung into its hull" (script 201), visible far below during the descent (script 249), "The dim node-pulse beats far below in the skull-dome" (275) — the destination the player is walking *toward* for the whole of Beat 1 — and at the Beat 4 mercy-kill, "the dim pulse in the ancient cradle gutters, steadies for one last soft beat, and goes out" (443), the single image the chapter's climax is built on. Nothing in the current build carries this motif as a dynamic light or a dedicated sound. §7's audio manifest already notes `MemoryFlashbackController.heartbeatLoop` is left null; that field is the natural home for a slow, failing heartbeat bed that could falter further toward the Cradle. The *visual* half is equally unbuilt: `AddAmbientPulse` (the same behaviour already driving `CradleLight`'s slow breathing warmth, A.1) is the obvious existing tool for a pulsing light on or below `ArkshipHull` (Beat 1c) as the canon "dim node-pulse visible below" — currently there is no such light, so the descent has no visible destination beacon. And the pulse's gutter-out on the Dreaming Archive's death (Beat 4) — the chapter's actual climactic light-and-audio event — is presently unspecified anywhere in this document. This is the single most load-bearing dynamic light/sound gap in the chapter: it is simultaneously the thing the player descends toward and the thing whose death ends the dream. It is also the destination of a third unbuilt piece, the depth-triggered bark layer that is meant to track it going out as the player descends — see Beat 1f.

**The dreamscape's mood is owned by `MemoryFlashbackController`**, not by `ChapterEnvironmentProfile` — it reapplies its own fog/ambient treatment (`ExponentialSquared` fog, flat ambient, both configurable, plus an optional looping heartbeat bed) on every `EnterDive()`, and `MemoryDiveController` snapshots the pre-dive `RenderSettings` so `ExitDive()` can restore them exactly — "the color floods back" beat. This is the same component Ch3's Kethel-7 playback and Ch7's mindspace duel use; Ch11 is its third deployment, and per the class-header decision it spans **three contiguous rooms under one island** (extending Ch8's "two rooms under one dive" precedent by one more room).

**Candor note — the dreamscape's central surreality is unbuilt too.** §3's candor above covers the canyon's architecture-reveal gap; the dreamscape has a comparable one. Canon's defining conceit for "the saga's most surreal level" is that its geometry itself is unstable — the dialogue script's SETTING block states "footing that was bone becomes memory becomes bone again" (script 38–40), and its production notes call for the ground shifting between bone and memory as "a rules-bending sub-level" once the broadcast takes hold (script 143–149). The as-built `DreamscapeArena`/`ArkshipCore`/`Cradle` are three static, enclosed primitive rooms with fixed floors and plain walls, mood carried entirely by `MemoryFlashbackController`'s fog/ambient treatment rather than by any geometry change. The saga's most surreal level is currently spatially indistinguishable from any interior corridor. Flagging as the second major greybox gap in this chapter, after the canyon's architecture reveal, so a future art/systems pass knows the shifting-ground surreality is entirely unbuilt, not merely un-prefabbed.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter11Builder.cs`.** The builder should read `Assets/Ronin7/Data/Ch11Environment.asset`, same schema as every other chapter's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light — color (0.6, 0.62, 0.68), intensity 0.3, rotation Euler(55, -35, 0) |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` — Flat, (0.06, 0.06, 0.08) |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` — Exponential, (0.08, 0.08, 0.1), 0.018 (**real canyon only** — the dreamscape overrides this via `MemoryFlashbackController` on every dive entry) |
| `accentLights[]` | `{ name, position, color, intensity, range }` | `BuildAccentPointLight`, ten entries total (five canyon, five dreamscape — see Appendix A.1) |
| `eventLights[]` | — | **unused this chapter** — Ch11 has no docking-alarm-style event light; leave the array empty rather than omitting the field, so the schema stays uniform across chapters |

The dreamscape's own palette (fog color (0.35, 0.37, 0.42), density 0.045, ambient (0.30, 0.30, 0.34) — `MemoryFlashbackController`'s serialized defaults) is **not** part of `ChapterEnvironmentProfile` and should not be duplicated there; it belongs to the `MemoryFlashbackController` instance on the `Dreamscape` GameObject, consistent with how every other memory-dive chapter authors it.

## 4. Per-beat scene spec

The chapter plays as five beats: Beat 0 (voice-only briefing aboard the Cairn), Beat 1 (the bone-canyon descent), Beat 2 (the narcosis dreamscape — ghosts, the Younger Self, Aldric's duel, Unbroken), Beat 3 (the arkship origin record — the reveal), Beat 4 (the second kill, exit, homecoming, and the Ch12 hook-out). Each beat is documented with the same a–f structure.

**Table conventions, everywhere below** — identical to every other chapter's: art tables carry Position/Rotation, Registry Key, resolved path, and Status; art tables never carry `scale()`/`size()`/`PrimitiveType`; positions/rotations encode blocking and are kept; **Status `MISSING`** means the primitive fallback is active for that row.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes for props that will become prefabs. Create **`BuildBeat0Art()`** for the spawn-area set dressing (there is almost none — this beat is voice-only) and **`BuildBeat0Logic()`** for the single dialogue player and its mission step.

#### a. Narrative purpose & emotional target

Beat 0 is the fullest war-room the crew has fielded — Cassie-04 stands where Morrigan usually drives the table, three days off the rack (script 201), laying her cross-index over a node that "points back" instead of outward. The emotional job is dread-setting before the descent: Sable names the node before they go — a sister so long racked her shadows have begun leaking out of her, one they may not bring up alive — and Cassie supplies the survival rule the whole chapter will test the player against ("if a voice tells you something you want, that's when you run"). Coral warns against reverence for a thing this old; Vess claims the descent as the origin of the men who took her people and vows to say her dead in the canyon (paid off in Beat 1). Echo, for Cipher alone, names the one thing that makes this node different from every prior one: it predates the leash, predates Echo's own making. The beat closes on Ronin-7 folding every warning into a single heading — "we quiet her, and we don't stay long" — the flat finality of a man who has just heard "we may not be saving her" and is going anyway.

Per the class-header CREW-PRESENCE DECISION, Beat 0 is the only beat where the full crew (Cassie-04, Sable, Coral Vex, Vess, Echo, Ronin-7) speaks at all — from Beat 1 on, only Gryph, Coral, Vess, and Sable check in over a degrading comm, and Beat 2 onward the comm is cut entirely.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at the canyon mouth, effectively (0, 0, 4) — the katana "Echo" sits nearby at (2, 1, 4) (see Art, below). The player does not travel in Beat 0; this beat plays out entirely as a stationary war-room VO while the player stands at the chapter's spawn point.
- **Spawn-blocking note.** `BuildRig` spawns the rig with `Quaternion.identity` — facing +Z, unrotated. This is load-bearing, not an arbitrary default, the same protection class as Beat 2b's dive-entry and Beat 4b's dive-exit rotation notes: the katana at (2, 1, 4) reads as **on the player's right** (§c "Note on the katana", §f) and the canyon's descent reads as **dead ahead** (§a; §Beat 1a's "walks into the bones") only under this facing. Recording it here closes the doc's own protective pattern — spawn, dive-in, dive-out — over all three of this chapter's rig-rotation events instead of just two; a future patch to the shared rig spawn could otherwise silently put the katana behind or beside the player and turn the opening sightline away from the canyon without anything flagging it.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4).
- No NPCs are physically placed for this beat — every speaker (Cassie-04, Sable, Coral Vex, Vess, Echo) is voice-only, per the class-level CREW-PRESENCE DECISION. Unlike Ch9's Beat 0, which stages a literal, if unbuilt, holo-table war-room, Ch11's builder makes no attempt at that set at all — this is a deliberate continuation of the same convention, not a regression specific to this chapter.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` (`ch11_beat0_briefing`) — the full war-room briefing, 8 lines, ending on Ronin-7's "Take us to the bones." |

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Katana "Echo" | (2, 1, 4), Euler(-90, 0, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnGround` (16×12 platform — carries a mesh-only ceiling at y=3.6, not open air; see below) | center (0, 0, 4) | `Rooms.CanyonSpawnGround` | `…/Art/Generated/Rooms/CanyonSpawnGround.prefab` | **MISSING** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |

**Correction — `SpawnGround` is not ceiling-less.** `BuildFloorCeiling` (used only by `SpawnGround` among this chapter's canyon geometry — every other platform uses the floor-only `Ch11BuildTier`) unconditionally emits a `SpawnGround_Ceiling` cube at `(center.x, RoomH=3.6, center.z)`, scale (16, 0.2, 12), tinted the dark ceiling color (0.08, 0.08, 0.09) with its collider stripped. So the spawn point sits under a low, dark, mesh-only roof, not the open sky the "canyon mouth, ribs arching overhead" framing (§3, §a below) implies for the rest of the descent. Either read this as deliberate — a covering overhang the player starts beneath before the canyon opens up at Tier1 — or flag it for a future fix; this document does not resolve the tension, only records it accurately (§2's `RoomH` note carries the same correction). The natural resolution, worth stating rather than leaving open: `Rooms.CanyonSpawnGround`'s prefab commission should render that mandatory ceiling as an overhead rib-arch / bone-vault — "ribs arching overhead like the vaulting of a drowned cathedral" (script 25) — so the player starts beneath the leviathan's first arch of ribs before the canyon opens to the ceiling-less open-air tiers at Tier1, converting the collider-asymmetry constraint (A.3) that already forces a ceiling mesh to exist into intended blocking rather than a limitation to fight.

**Note on the katana.** Consistent with Ch9/Ch10's "cost, not initiation" precedent, the katana rides from the start — deep into Act III, there is no rack-wake beat. `BuildSword` places it at world (2, 1, 4), 2 m to the player's right at hand height, a free-standing grabbable the player must reach for, not controller-attached. As with Ch9, **the grab is ungated**: nothing in the mission spine requires the player to pick Echo up before proceeding into the canyon, so a player who ignores the free-standing blade could in principle reach Beat 2's ghost-manifestations unarmed. Flagged for awareness (this document does not fix it) — carried forward from the same gap already documented in Ch9's Scene-Construction doc, not new to this chapter.

**Candor note on the war-room.** Per (a) above, Beat 0 is framed narratively as a "war-room holo-table" scene with Cassie-04 driving the analysis and the whole roster physically present. None of that set exists in the build — there is no holo-table prop, no crew placement, no war-room geometry of any kind. The entire briefing plays as disembodied VO while the player stands alone at `SpawnGround`. This is the same divergence Ch9's Beat 0 candor note already flags for its own war-room briefing; Ch11 does not attempt to close the gap either. Candidate future props for a scoped follow-up: a `WarRoomHoloTable` and a `BoneCanyonCutawayProjection` VFX, matching the holo-table the dialogue script stages ("the holo-table throws a bone-canyon up over the bowl of light"). Flagged here, not fixed — building a whole second location is new set-art scope.

#### d. Combat

None. Beat 0 has zero combat components.

#### e. Dialogue / VO

Set id **`ch11_beat0_briefing`**, position (0, 1, 4), 8 lines, advanced on **Left-Hand "Talk" (Y)**:

| Speaker | Line (as authored in `Chapter11Lines.cs`) | sec |
|---|---|---|
| Cassie-04 | "Here's your second pin, Cipher, and I'll tell you up front it's the one I like least… The file doesn't call it a piece of the Program. It calls it the start of the Program." | 26 |
| Sable | "It's one of us. I can feel her from here… She's bleeding them into the canyon… It's going to show you your dead." | 30 |
| Sable | "And I have to say the rest of it, so hear me… We may not be going down there to save her. We may be going down there to free her, the only way that's left…" | 24 |
| Cassie-04 | "And because Sable will be too kind to say the hard part, I'll say it… If something down there has the face of someone you lost and it asks you to come closer, that's the node reaching for one more thing to keep." | 26 |
| Coral Vex | "I have survived old things… I've never stood in front of the first make. Knight… Don't think of it as a grandfather. Think of it as the oldest version of the cage, and remember you've already broken a newer one." | 24 |
| Vess | "The start of the thing that made the men who took my people… So I'm coming. I'll say my dead in that canyon if it shows me theirs." | 19 |
| Echo | "I'll say the thing none of them can feel, Cipher, because I'm the only one down there it can't lie to… You move, I read… I'm the one real thing you're bringing into that dream. Don't lose me in it." | 26 |
| Ronin-7 | "Then we go in clear-eyed, we quiet her, and we don't stay long… Echo's the only voice I trust past the rib-line… Take us to the bones." | 17 |

Total runtime ≈ 192 s. This is a **voice-only cast** dialogue set — no speaker except Ronin-7 has a physical presence in the scene, so this table's "position" column is a single shared anchor rather than per-speaker blocking, the same convention Ch9's Beat 0 uses.

#### f. Audio / Haptics / VR Comfort

- No camera shake — the whole beat is a stationary group VO, so the only feel to sell is spatial audio and the `SpawnLight` accent (warm/neutral 0.7/0.72/0.8) establishing the mood before the descent.
- No bespoke haptics scripted for this beat (no combat) — but there **is** a grab: the free-standing katana Echo at (2, 1, 4) (§c) is grabbable from the moment the player spawns, and gripping it fires the standard system-level XR interaction grab haptic, not a chapter-authored one. It is the player's first physical contact with the blade before walking into the bones — a small but real tactile beat, not the absence the previous wording of this line implied. It reads to the player's right, and the bones read dead ahead, only because of §b's load-bearing spawn-facing note — the same composed blocking, not a coincidence of layout.
- Comfort vignette is inert — the player does not move during Beat 0.

---

### Beat 1 — The Bone-Canyon (Descent / Traversal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitive cubes for the tier floors, ribs, or ramps — read `Props.LeviathanRib`, `Rooms.CanyonTier`, and `Props.CanyonRamp` from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (all five canyon platforms, all four ramps, the `ArkshipHull` prop) and **`BuildBeat1Logic()`** (both reach points, both dialogue players, the `EnterDreamscapeTrigger`).
> **`EnterDreamscapeTrigger`'s own `OnEnable` fires `MemoryDiveController.EnterDive()`** — do not add a second, redundant activation for the dive itself.

#### a. Narrative purpose & emotional target

Beat 1 is the chapter's environmental-storytelling descent: the architecture is meant to tell the reveal ("this is where it started… the place the first operatives, the Knights, were made") through scale and palette alone, never exposited flat. Canon opens the beat with "RONIN-7 rides a drop-line down into the bone-canyon" (script 249) — this arrival is deliberately not staged; the player simply spawns at `SpawnGround` already inside the canyon, consistent with the instant-teleport traversal model and the CREW-PRESENCE DECISION's voice-only crew (§1.1). Recorded here so a future pass reads this as a decision, not an oversight. The crew comm degrades with depth as established in Ch9/Ch10, but here layered with a second, eerier degradation — voices beginning to warp, double, and bleed half-heard names as the broadcast rises. Gryph reads the bone-traverse as a veteran ("bone breaks different than stone… test every grip"); Coral names the place's likely origin from a lifetime under the Program; Vess claims the descent as the near-end of the line that destroyed her people, paying off her Beat 0 vow. The beat's structural job is to slide the player continuously from "fossil graveyard" into "the broadcast becomes visible" without a hard cut — Echo's Arkship dialogue at Tier 3 is the beat's hinge, naming the metal as "the oldest Program metal I've ever read… we're standing at the root," and Sable's final comm line ("she'll wear us. She'll wear everyone. Hold on to Echo") is the last thing the crew says before the channel cuts for good at the threshold.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** free-walks the length of the canyon (z ≈ 4 → 78, descending in Y from 0 to −4 across five platforms and four ramps) on continuous locomotion + snap-turn. No scripted path.
- **`MidCanyonReachPoint`:** (1, -2, 58), radius 5 m — gates the Arkship dialogue on the player physically reaching Tier 3.
- **`ThresholdReachPoint`:** (0, -3, 72), radius 5 m — gates `EnterDreamscapeTrigger`, built inactive at (0, 0, 0) *(the trigger's own transform position is irrelevant; it only needs `OnEnable`)*, wired with `dive = diveController`.
- No NPCs are placed for this beat — Gryph, Coral, and Vess's Beat 1 lines are all comm-only, continuing the CREW-PRESENCE DECISION.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Descent` (`ch11_beat1_descent`) — Echo, Gryph, Coral, Vess as the descent begins |
| 2 | ReachTrigger | Gates on `MidCanyonReachPoint` (1,-2,58), radius 5 |
| 3 | Dialogue | `Dialogue_Beat1_Arkship` (`ch11_beat1_arkship`) — Echo reads the old metal, Sable's last comm before the cut, Ronin-7's committal |
| 4 | ReachTrigger | Gates on `ThresholdReachPoint` (0,-3,72), radius 5 |
| 5 | Trigger | Activates `EnterDreamscapeTrigger` → `MemoryDiveController.EnterDive()` — comm cuts, the rig teleports to `DreamscapeEntryPoint`, `MemoryFlashbackController` applies its fog/ambient treatment |

**What changes during the beat:** nothing is added or removed in the canyon itself — every platform, ramp, and light is static from build time. The only state change is step 5's dive activation, which is also the beat's exit: the player's next frame is inside `DreamscapeArena`, 300 m away in world space, with the real canyon now dormant behind them.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `Tier1` (12×12 platform + two rib props) | center (2, -1, 22) | `Rooms.CanyonTier` | `…/Art/Generated/Rooms/CanyonTier.prefab` | **MISSING** |
| `Tier2` (12×12 platform + two rib props) | center (-2, -2, 40) | `Rooms.CanyonTier` | `…/Art/Generated/Rooms/CanyonTier.prefab` | **MISSING** |
| `Tier3` (12×12 platform + two rib props) | center (1, -3, 58) | `Rooms.CanyonTier` | `…/Art/Generated/Rooms/CanyonTier.prefab` | **MISSING** |
| `Threshold` (12×12 platform + two rib props) | center (0, -4, 72) | `Rooms.CanyonTier` | `…/Art/Generated/Rooms/CanyonTier.prefab` | **MISSING** |
| `Ramp0`…`RampThreshold` ×4 | see §2 ramp table | `Props.CanyonRamp` | `…/Art/Generated/Props/CanyonRamp.prefab` | **MISSING** |
| `ArkshipHull` | tiers[2] + (-6, 2, 6) = (-5, -1, 64) | `Props.ArkshipHullFragment` | `…/Art/Generated/Props/ArkshipHullFragment.prefab` | **MISSING** |
| `Tier1Light`/`Tier2Light`/`Tier3Light`/`ThresholdLight` (accent) | see §3 | — | `ChapterEnvironmentProfile.accentLights["Tier1"/"Tier2"/"Tier3"/"Threshold"]` | profile |
| `CanyonDustMotes` (ambient particulate, one instance per tier or a single length-of-canyon system) | drifting through each accent-light beam, e.g. seeded at `Tier1Light`…`ThresholdLight` positions (see §3) | `Vfx.CanyonDustMotes` | `…/Art/Generated/VFX/CanyonDustMotes.prefab` | **MISSING** *(no primitive fallback — see §3 candor note, Appendix B)* |

**Notes on the transition.** `Rooms.CanyonTier` must preserve the per-instance tint override `Ch11BuildTier` currently applies (bone → vault-blue lerp across the three numbered tiers; `Threshold` gets its own fixed cold-blue tint outside the lerp) — a prefab replacement that bakes one fixed color loses the "the deeper it goes, the older/colder it reads" progression this chapter's palette is built entirely around. `Props.CanyonRamp` needs four instances at four different lengths/angles (§2's ramp table) from one prefab, matching the shared-prefab-per-instance-transform convention every other chapter's repeated props use.

**Flagged gap — no node-pulse light on `ArkshipHull`.** Per §3's candor note, canon's "dim failing pulse strung into its hull… far below in the skull-dome" is meant to be visible from Tier 3 on, the descent's actual navigational goal. `ArkshipHull` today is a single static tinted cube with no light or pulsing behaviour attached. Driving a dim, slow-failing point light on or just beneath it via the existing `AddAmbientPulse` behaviour (already used on `CradleLight`, A.1) is the natural fix — currently unbuilt.

#### d. Combat

Per the production note, "light combat is incidental here, not central" — the ghost-manifestations begin manifesting only at the dreamscape threshold, not in the canyon proper. **The as-built scene has zero combat components in Beat 1**; the "rising dread" the production note calls for (ghost-shapes at the edge of vision before the hard dive-in) is carried entirely by the comm-degradation dialogue, not by any spawned enemy or VFX. Flagging as unbuilt atmosphere, not a missing mechanic — the boss and both ghost-manifestations live entirely inside `Dreamscape`, inactive until Beat 2.

#### e. Dialogue / VO

Two sets, both advanced on Left-Hand "Talk" (Y):

`Dialogue_Beat1_Descent` (`ch11_beat1_descent`), position (0, 1, 12), 4 lines, ≈79 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "We're in, Cipher. And this isn't a canyon. Look at the walls. Those aren't cliffs, they're ribs… I'll tell you what it is when we're close enough that I'm sure." | 21 |
| Gryph | "Gryph here, Cipher. I've climbed down plenty of holes that were just rock being rock. This isn't that… Test every grip before you trust your weight to it." | 15 |
| Coral Vex | "I can hear the bones in your channel, Cipher, the way they swallow sound… You're not descending into a canyon. You're descending into the beginning." | 23 |
| Vess | "This is it, then. The bottom of the hole the whole rotten line crawled up out of… Let the thing that started it hear them once before you put it down." | 20 |

`Dialogue_Beat1_Arkship` (`ch11_beat1_arkship`), position (1, -2, 60), 3 lines, ≈61 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "There it is. I'm sure now. That's an arkship, Cipher, and it's Program metal, but it's the oldest Program metal I've ever read… We're not raiding a vault. We're standing at the root." | 22 |
| Sable | "Cipher. She's right below you now, in the skull, where the dark pools… She'll wear us. She'll wear everyone. Hold on to Echo. He's the one thing down there she can't copy." | 24 |
| Ronin-7 | "I hear you, Sable. Stay on the channel as long as it'll hold you… Echo. From here you're my eyes and my ears and the only thing I'm sure of. Read me down into the dark." | 15 |

#### f. Audio / Haptics / VR Comfort

- No camera shake, ever.
- The crew comm's "voices beginning to warp, double, and bleed into each other" as the broadcast rises is staged in the production note as a systemic audio-filter effect layered onto the existing per-depth attenuation Ch9/Ch10 use — **not asserted as implemented in the current build** (no filter component is wired in `Chapter11Builder.cs`); flagged as a future `AudioDirector` enhancement, matching how Ch9's Scene-Construction doc flags its own unbuilt depth-attenuation filter.
- **Candor note — the dead bleeding into the channel is a distinct, sharper commission from the crew-voice warp filter above.** Canon does not stop at crew voices warping and doubling; the production note is explicit that "the dead begin to bleed INTO the channel — half-heard whispers, names, fragments of voices Cipher knows, riding under the comm" (script 253), paid off at the threshold itself, where the comm "warps, doubles, fills with half-heard whispers and known names, and then cuts" (script 292). This is the diegetic proof of Sable's "she'll wear us. She'll wear everyone" (`ch11_beat1_arkship`, §e) — the dead already reaching through the channel before the player ever crosses into the dream. Distinct from a generic degradation filter, this wants its own layer: known-voice fragments and names surfacing under the comm, building toward the threshold cut. Flagged as its own `AudioDirector` commission, not folded into the crew-voice warp above.
- **Candor note — the depth-triggered descent bark layer is a distinct, unbuilt gap from the filter above.** The production note (script 253) does not stop at a degradation filter; it calls out a separate **POSITION/DEPTH-TRIGGERED bark system**: "Sable feeds proximity and the failing node ('She's below you, and she's fading, Cipher. Faster down here. I can feel her going out.')," alongside a full crew-pool descent read (Morrigan/Kessler on the arkship and depth-count, Coral on the Knight make, Vess on her dead). The as-built Beat 1 has only the two static, Talk-advanced `DialoguePlayer` sets (`ch11_beat1_descent`, `ch11_beat1_arkship`, §e) — no dynamic, proximity/depth-keyed bark pool exists anywhere in the build. This is the mechanism by which the player is meant to *feel* the sister spending herself as they get closer — the beacon dying faster the closer you get — and it has no expression today beyond two fixed dialogue checkpoints. Pairs directly with §3's node-pulse candor note (the failing beacon the player is walking toward); a future `AudioDirector`/bark-pool pass should treat the two as one system, not build the pulse without the barks that track it.
- `ThresholdLight` is the coldest, dimmest, and largest-range accent in the canyon ((0.3, 0.42, 0.55), intensity 1.6, range 12) — the last thing the player sees lit before the dive teleport takes them.
- **The canyon's own acoustic signature wants to be dry and sound-swallowing, not reverberant.** The SETTING block contrasts every chapter's silence by name ("the Tide was a drowned hush, the Ninefold a dry counting silence," script 37), and Coral's own Beat 1 line, already quoted in §e above, names it directly: "the way they swallow sound." `Chapter11Builder.cs` runs `ReverbZonePlacer.AutoTagInteriorVolumes()`/`PlaceReverbZonesForInteriorVolumes()` over the scene's interior volumes — correct for the dreamscape's enclosed rooms, but the upper canyon itself is open-air and wants the acoustic opposite: a near-anechoic, absorptive, bone-deadened ambience that swallows rather than returns sound, reinforcing the region boundary already established visually in §6 (cool/dark canyon vs. reverb-zoned dream). Currently unspecified as its own audio treatment, distinct from the reverb zones the interior rooms correctly get.
- **Flagged gap — the comm-cut at dive entry has no documented audio treatment, unlike its mirror.** Step 5's `EnterDreamscapeTrigger` fires `MemoryDiveController.EnterDive()`, and per Beat 2a, "the instant `EnterDive()` fires, the comm is gone" — the entire crew pool goes silent in a single frame, the most dramatic audio loss in the chapter. Beat 4f documents an `AudioDirector` comm-restore stinger for the mirror-image moment (`ExitDive`, "the color floods back"); no equivalent cut-to-silence/dropout sting is specified here. Add one: a distinct `AudioDirector` stinger on `EnterDive` should sell the diegetic loss of the whole channel the instant the threshold is crossed — the acoustic bookend to Beat 4's restore. Currently only the *return* half of that pair is documented.
- **Flagged gap — no railings, edge treatment, or documented fall/ledge behaviour on the canyon's open-air tiers.** Tier1/Tier2/Tier3/Threshold are 12×12 platforms with open edges over fog (§2) — `Ch11BuildTier` never builds a railing or edge lip, and the tiers are laterally offset from one another (Tier1 x=2, Tier2 x=-2, Tier3 x=1) with no floor between them outside each ramp's own footprint. `ZoneBounds` (center (0,0,150), radius 260, §2) is a single loose bounding sphere and does nothing to catch a player who steps sideways off a tier's lip rather than forward off the far end into a ramp. Nothing in the build documents the intended behaviour — soft push-back, a kill-floor-and-reset, or an accepted fall into the fog below — for a player on continuous locomotion who simply walks off the side of a tier. This is a real VR-comfort/frustration surface the rest of this document is otherwise careful about (the comfort vignette, the ramp-grade table, every reach-point radius); flagged here rather than assumed. See §8's verification checklist for the corresponding check.
- Comfort vignette engages normally across the ~74 m walk-and-descend from spawn to the threshold.

---

### Beat 2 — The Narcosis Dreamscape and the Keeper (Ghosts, the Younger Self, Aldric's Duel — Unbroken)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Rooms.DreamscapeArenaShell`, `Props.GhostManifestationVfx` from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (`DreamscapeArena`'s shell + walls, the ghost-anchor placements for Kira/Younger Self, Aldric's placement) and **`BuildBeat2Logic()`** (`MemoryDiveController` + both dive triggers, the two ghost-manifestation `DreamPhantom`s, the `DefeatEnemies` step, the `UnbrokenGranter` trigger, all five dialogue players for this beat).
> **Aldric's `SetActive(true)` is not a separate Trigger step** — `MissionDirector.BeginDefeatEnemies` activates every `Health` in the step's list itself when the step starts, exactly like every other chapter's boss reveal (Ch8's Warden, Ch9's Vane, Ch10's Sever).
> **Killing Aldric does NOT end the dream or restore comm** — that is a hard narrative/mechanical distinction from every prior keeper-kill in the saga; only Beat 4's second kill does. Do not collapse the two.

#### a. Narrative purpose & emotional target

The instant `EnterDive()` fires, the comm is gone — for the length of this beat, Echo is the *only* real voice, the first time in the saga a major sequence plays with the entire crew pool silent. The player walks through a dream that is not attacking so much as *reaching*: Kira offers rest ("come closer, it's cold out here… you don't have to carry any of it anymore"), and Echo's rule — "we don't fight the ones that only reach. We walk through" — makes Cassie's Beat 0 briefing warning mechanical rather than merely verbal. The Younger Self recurs as the one figure the dream will not let him keep: it tells him true things ("there was someone before the sword. Before the number") and withholds exactly one, his own name, planting the Soren thread (Ch16) without ever naming it. Aldric, the boss, is the chapter's hardest emotional swing — not a mirror or a brother almost reaching a door (Vane, Sever) but a ruin with "almost nothing left of him to free," so dissolved into the broadcast he half-speaks in stutters and cannot tell wanting rest from enforcing the order. His death is explicitly written as **grief, never triumph** — Echo: "there's no door left to reach… the kindest thing in the galaxy right now is to let the oldest cage finally stop standing." His freed blade-shadow grants **Unbroken**, the fourth permanent unlock — but the beat's own dialogue is explicit that the dream itself survives him: "he's down, but the dream isn't… killing him didn't quiet her," setting up Beat 3 and Beat 4 as still comm-dark, still inside the same unbroken dive.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **`MemoryDiveController`** (`DreamscapeDive`): `diveRoot` = `Dreamscape` (the whole three-room island), `diveEntryPoint` = `DreamscapeEntryPoint` (0, 1, 300), `diveExitPoint` = `DreamscapeExitPoint` (0, -3, 76) *(back near the real-canyon threshold — see Beat 4b)*, `rigRoot` = the player rig's transform, `flashback` = `Dreamscape`'s `MemoryFlashbackController`. `EnterDive()` (fired by Beat 1's step 5) activates `diveRoot`, snapshots and reapplies the dreamscape's fog/ambient treatment, and teleports the rig — instant, no lerp, comfort-safe.
- **Entry-blocking note.** `DreamscapeEntryPoint` teleports with `Quaternion.identity` — the player faces +Z, directly down the 36 m length of `DreamscapeArena` toward Aldric's placement at z=330. On arrival the ghost anchors flank that sightline: Kira left at (-4,0,306), the Younger Self ahead-right at (4,0,314) — "wake facing the length of the dream, the dead to either side" is composed, if nowhere stated as intentional. Recording it here protects the reveal framing against a future patch that changes the entry rotation without realizing it is load-bearing blocking, not an arbitrary default.
- **Ghost anchors — Kira and the Younger Self:** placed once each via `Ch11PlaceGhostNpc`, at world (-4, 0, 306) and (4, 0, 314) respectively. Each is a `StoryNpc` with **no `Health`, no `Enemy`** — purely a dialogue-beat anchor the player walks past, tinted with `MemoryFlashbackController.MakeGhostMaterial()` on every renderer. The source script's "walk through, don't fight" rule for these two is honored narratively: there is nothing on either GameObject that can block or attack the player.
  - **⚠ Flagged gap — neither ghost anchor is reparented under `Dreamscape`.** `Ch11PlaceGhostNpc` calls the shared `InstantiateNpc`, which always creates a scene-root GameObject at an absolute world position with no parent argument. Unlike Aldric and the Dreaming Archive (both explicitly `transform.SetParent(dive, true)` after construction, and both explicitly `SetActive(false)`), Kira and the Younger Self are never reparented and never individually deactivated. `diveGo.SetActive(false)` — called immediately after all four placements — therefore has **no effect on Kira or the Younger Self**: both remain active, unhidden scene-root objects at (-4,0,306)/(4,0,314) from the moment the scene loads, regardless of whether the player has entered the dive. In practice this is low-risk (the real canyon tops out at z≈78, and there is no walkable path to z≈306–314 outside the dive teleport), but it is a real correctness gap relative to the pattern the boss/archive placements establish, worth a one-line fix (`transform.SetParent(dive, true)` inside `Ch11PlaceGhostNpc`) rather than silently working around it here.
- **Ghost-manifestation combat texture** ("the dead keep interrupting the duel"): two generic enemies at world (-3, 0, 326) and (3, 0, 326), built via the shared `BuildEnemy(pos, playerHealth, ghostDef)` pipeline (`Ch11EnsureGhostManifestationDefinition`: maxHealth 35, damage 7, moveSpeed 1.3, attackCooldown 1.0 — the lightest enemy stat block in the chapter), then `AddComponent<DreamPhantom>()` on each. Both built `SetActive(false)`, correctly parented under the generic enemy hierarchy (which *is* under `dive` via the earlier `BuildEnemy` call sequence's own parenting — see Appendix A note). `DreamPhantom` cycles Solid (vulnerable) ↔ Phased (invulnerable, incoming damage refunded via `Health.Heal()`) every `phaseInterval` (default 2 s) while `autoCycle` is true — the mechanical expression of "the dead keep interrupting," ghosts the player can only land hits on during their Solid window.
- **Aldric / Knight-1 (the boss):** built via `Ch11BuildNamedBoss(Ch11AldricPrefab, dive.position + (0,0,30), "Aldric / Knight-1", aldricDef, playerHealth)` — resolving to the real, disk-confirmed `Aldric_Knight-1.prefab`, `FitNamedCharacter`-grounded, rotated to face -Z (the approach direction), fitted with a synthesized `CapsuleCollider` (center (0,1.1,0), height 2.4, radius 0.5) and a synthesized `ArmR/Sword/Blade/BladeTip` chain (the same idiom `Ch9BuildVane`/`Ch10`'s named-boss builders use for Named-mesh characters with no native combat rig), then `Enemy`-wired against `Ch11EnsureAldricDefinition` (maxHealth 360, damage 32, moveSpeed 0.9, attackCooldown 1.5 — **the heaviest, slowest, highest-HP stat block in the saga so far**, matching "the oldest, heaviest, most archaic school the player faces"). Explicitly reparented under `dive` and `SetActive(false)`. **He stays invisible through the entire Ghosts/Younger-Self/Keeper dialogue chain** (steps 6–8) and only pops into existence when the `DefeatEnemies` step (step 9) begins.
  - **⚠ Flagged gap — this is a reveal-timing divergence from canon, not just an activation convenience.** The dialogue script has Aldric visually resolve out of the half-light *before* he speaks a word — "out of it, immense and slow, the keeper resolves… his features flickering and de-resolving between man and shadow" (script 346) — then delivers his 52 s "Lie down. Be kept" monologue and his 28 s "Come and try, little newest thing" taunt (script 349/364, doubling as the combat trigger per §b's step-9 note) as a looming, visible presence throughout. In the build both lines play from an `Enemy` GameObject that is still `SetActive(false)`, so Aldric's taunt — the beat's own trigger into combat — is voiced from empty air, and his 80 s of monologue (steps 8's 52 s plus 28 s) plays with nothing on screen to look at. This is the same boss-activation convention Ch9/Ch10 already use (stay hidden until `DefeatEnemies` begins), but the cost is higher here: Aldric's monologue is written as the beat's emotional centerpiece, and canon wants him seen looming through all of it, not popping in only once the fight starts.
- **`UnbrokenGranter`:** a `GameObject` carrying `AbilityGranter` (`abilityId = AbilityId.Unbroken`), built `SetActive(false)`, activated by a dedicated `Trigger` step (step 11) *after* the Beat 2 Kill dialogue (step 10) — the same "grief before the gift" ordering invariant Ch9's Overdrive grant uses. **This ordering is load-bearing**, matching the pattern already documented and protected for Ch9's Overdrive grant: a future edit collapsing steps 10→11 for gameplay snappiness would destroy the beat's central "hold this beat before the shadow rises" pacing.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 6 | Dialogue | `Dialogue_Beat2_Ghosts` (`ch11_beat2_ghosts`) — Echo's rule, Kira's lure, Echo's correction |
| 7 | Dialogue | `Dialogue_Beat2_YoungerSelf` (`ch11_beat2_youngerself`) — the buried-self exchange, the withheld name |
| 8 | Dialogue | `Dialogue_Beat2_Keeper` (`ch11_beat2_keeper`) — Aldric's confrontation, Echo's grief-read, Ronin-7's refusal-to-free (Aldric still invisible) |
| 9 | DefeatEnemies | `Dialogue_Beat2_Keeper`'s follow-through: activates Aldric **and** both ghost-manifestation `Health`s together, waits for all three to reach zero — the boss duel, fought through the interrupting phantoms |
| 10 | Dialogue | `Dialogue_Beat2_Kill` (`ch11_beat2_kill`) — "Rest, old man," then Echo feeling the shadow arrive |
| 11 | Trigger | Activates `UnbrokenGranter` — unlocks Unbroken immediately via `OnEnable` |
| 12 | Dialogue | `Dialogue_Beat2_Unbroken` (`ch11_beat2_unbroken`) — Echo names the two-part gift |

> **⚠ Invariant — step 10 must precede step 11, same protection class as Ch9's Overdrive-grant ordering.** The dialogue script stages the kill, then a held beat over the dissolving keeper, and *only after* does the freed shadow rise. Do not reorder.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `DreamscapeArena` shell (20×36, floor/ceiling + 3 walls) | center world (0, 0, 316) | `Rooms.DreamscapeArenaShell` | `…/Art/Generated/Rooms/DreamscapeArenaShell.prefab` | **MISSING** |
| `ArenaLight0`/`ArenaLight1`/`AldricLight` (accent) | world (-4,2.2,308) / (4,2.2,322) / (0,2.4,330) | — | `ChapterEnvironmentProfile.accentLights["Arena0"/"Arena1"/"Aldric"]` | profile |
| Kira (ghost anchor) | world (-4, 0, 306) | `Named.KiraDusk` | `…/Art/Generated/Characters3D/Named/Kira-Dusk.prefab` | **EXISTS** |
| Younger Self (ghost anchor) | world (4, 0, 314) | `Named.YoungerSelf` *(reuses Soren mesh)* | `…/Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab` | **EXISTS** *(see casting note)* |
| Ghost-manifestation ×2 | world (-3,0,326) / (3,0,326) | `Enemies.GhostManifestation` | additively resolved by `EnemyArtWirer`, no dedicated mesh yet | **MISSING** *(placeholder capsule)* |
| Aldric / Knight-1 (boss) | world (0, 0, 330), Euler(0,180,0) | `Named.AldricKnight1` | `…/Art/Generated/Characters3D/Named/Aldric_Knight-1.prefab` | **EXISTS** |
| `RisingDustForms` (dust-coalescing-into-shapes VFX) | `DreamscapeEntryPoint` (0,1,300), reprised faint/ambient through `DreamscapeArena` | `Vfx.RisingDustForms` | `…/Art/Generated/VFX/RisingDustForms.prefab` | **MISSING** *(no primitive fallback — see §3 candor note, Appendix B)* |

**Casting note — the Younger Self mesh reuse is intentional and load-bearing.** `Ronin-7_Cipher_Soren.prefab` is the correct visual asset for "Ronin-7 before the Program, no augments, no scars" — but the filename is a **dev-facing asset path only**, never surfaced to the player. The in-scene `StoryNpc.displayName` stays `"Younger Self"`, and no dialogue line in `Chapter11Lines.cs` names the character, matching the source script's explicit "CRITICAL: never name Soren here." Do not let a future prefab-registry swap rename the displayed label to match the file path.

**Note on the ghost-manifestation dissolve.** Per `DreamPhantom`'s doc comment, "on death, the phantom dissolves (becomes inactive)" — this is a state change, not a VFX; there is currently no dedicated dissolve shader/particle keyed to `OnDissolved`, so a defeated ghost-manifestation today simply vanishes on the frame it dies. `Vfx.GhostManifestationDissolve` (Appendix B) is a natural commission for a future art pass, but is not required for the mechanic to function correctly. It pairs naturally with `Vfx.RisingDustForms` above — canon's dust is the shared substance the manifestations both coalesce out of and, per "the dead go to dust" (script 443), return to on death; a dissolve treatment that scatters back into drifting motes rather than a generic fade would tie the two commissions together as one visual grammar rather than two unrelated VFX asks.

**Flagged gap — Kira's walk-through-and-dissolve is a distinct, unbuilt beat, separate from both flagged gaps below.** Canon stages Kira as a figure the player walks bodily through: "Ronin-7 walks through Kira's reaching figure; she dissolves into dust behind him without a sound" (script 307/319) — the literal, physical payoff of Echo's `ch11_beat2_ghosts` line, "Walk through her. I know what it costs. Walk through her anyway." (§e). In the build Kira is a solid `StoryNpc` anchor (§b, above) the player stops and talks to, exactly like the Younger Self — there is no walk-through collision behaviour and no dissolve on pass-through. This is a specific line whose staged action has no expression in the scene, distinct from the *recurring-figure* gap (Younger Self, below) and the *missing-crowd* gap (the ledger dead, below): those are about who else should be present; this is about what Kira herself is supposed to do. Pairs naturally with the `Vfx.RisingDustForms`/`Vfx.GhostManifestationDissolve` commissions above — Kira should scatter into the same drifting motes the manifestations coalesce out of and return to, not simply be walked past.

**Flagged gap — the Younger Self is canonically recurring and receding, and is built as a single static anchor.** Canon is specific and repeated on this point: the Younger Self appears "recurring among them, never quite reached" (script 298), and the production note is explicit it "recurs throughout as a non-combat figure the player keeps almost reaching and never catching, culminating in the dialogue below — this is the Soren seed and must remain unresolved" (script 300). The as-built scene places one static `StoryNpc` once, at world (4, 0, 314) (§b, table above) — a fixed figure the player walks up to and talks to, not a figure glimpsed and lost repeatedly through the dream before the culminating exchange. §5's character table describes this placement as "a fragment of memory that cannot leave the place it is anchored to… never moves," which is accurate to what is built but should not be read as the intended final state — the recurring, receding quality is the entire delivery device for the withheld-name/Soren seed (Ch16), and losing it flattens `ch11_beat2_youngerself`'s "hunting strangers hoping one was you" image into an ordinary NPC conversation. Recorded here as an intentional-omission candor note, the same class as this document's other unbuilt-staging gaps (drop-line arrival, crew descent, war-room, shifting ground), so a future pass reads the single static anchor as a decision to revisit, not an oversight.

**Flagged gap — the referenced dream-dead never appear.** The two combat ghost-manifestations are faceless placeholder capsules, and Kira and the Younger Self are the *only* named dream-faces staged anywhere in `Dreamscape`. But the dreamscape's defining horror, per canon, is the dead wearing *specific known faces* — the stage directions have "the dead of Kethel-7 rise next, smaller shapes among them, the children of the orphanage-mission. Then the named faces from Cassie's ledger, the dead he came to learn the names of in Ch10" (script 319), and the Younger Self's own line names them explicitly: "You keep collecting the dead like they make a person. Kira. The little ones from the orphanage. All those names off the clerk's list" (`ch11_beat2_youngerself`, script 322). None of Kethel-7's children (Ch3) or Cassie's ledger dead (Ch10) are placed in the build at all — the Younger Self's line references ghosts the player cannot see anywhere in the scene. This is a dialogue-to-scene mismatch, not merely a missing-mesh gap, and belongs beside the placeholder-capsule note above as the fuller immersive case for the `Vfx`/roster commission: the ghost roster this beat needs is not just "a mesh for the two generic manifestations" but the specific named dead the dialogue already assumes are present.

**Flagged gap — Beat 3's origin hologram and cyan `ArkshipCoreLight` are live and partly visible throughout Beat 2's Aldric duel.** The three dreamscape rooms are one continuous open volume, not sealed sets — `DreamscapeArena` has no north wall and `ArkshipCore` has no north or south walls (Appendix A.3) — and the whole `Dreamscape` subtree activates together on dive entry (`diveRoot.SetActive(true)` inside `EnterDive()`), including `BuildHologram` at world (0,1.5,342) and `ArkshipCoreLight`, the chapter's brightest accent (intensity 1.8, range 14). So both are present for the entire length of Beat 2, not first appearing in Beat 3. `ArkshipCoreLight`'s 14 m range reaches back to z≈328, essentially Aldric's own z=330 placement, and from the Keeper/duel zone (z≈326–330) the hologram at z=342 is only ~12–16 m off — at the dreamscape's `ExponentialSquared` fog density of 0.045, transmittance at 14 m is still ≈0.67, so the cold cyan glow and the hologram's shimmer are clearly visible down-corridor during the chapter's climactic fight, well before the Beat 3 reveal proper. (The dive-entry point at z=300 is 42 m out and ~97% fogged, so the entry moment itself reads correctly.) Worth a deliberate call either way — accept it as intentional foreshadowing (a cold light waiting at the end of the dark, felt before it's explained) or gate `ArkshipCoreLight`/the hologram off until the Beat 3 reach step — but it should not stay silent, since a future prefab/lighting pass will otherwise not know whether the bleed is load-bearing or an oversight.

**Flagged gap — `AldricLight`'s red boss-glow burns over Aldric's empty placement for the whole of steps 6–8, before he is revealed at step 9.** `AldricLight` (A.1, red (0.7,0.2,0.18), the chapter's single hottest-colored accent) is part of the whole `diveRoot` art subtree and activates the instant `EnterDive()` fires — the same moment as every other dreamscape light, not the moment Aldric himself appears. Aldric is built `SetActive(false)` and only pops into existence when the `DefeatEnemies` step (step 9) begins (§b above). So a red warning-glow burns over world (0,2.4,330) throughout the Ghosts, Younger-Self, and Keeper dialogue chain (steps 6–8) while nothing is standing there, and Aldric then appears beneath a light that has already been announcing him for the length of three dialogue sets. This is the same class of gap as the `ArkshipCoreLight`/hologram bleed immediately above — a light that precedes what it lights — applied to the boss reveal itself rather than the Beat 3 preview, and deserves the same explicit disposition: accept it as intentional foreshadowing (the dream marking the spot before he resolves into it), or gate `AldricLight`'s activation to step 9 alongside Aldric's own reveal. See §6's Beat 2 lighting row for the same note carried into the lighting-progression table.

#### d. Combat — the Aldric / Knight-1 duel, fought through the interrupting dead

Aldric fights as a full `Enemy` using the existing melee-AI/`BladeDamager` systems, no bespoke boss logic — his `EnemyDefinition` (maxHealth 360, damage 32, moveSpeed 0.9, attackCooldown 1.5) is the chapter's, and the saga's, heaviest stat block: roughly 30% more HP and 33% more per-hit damage than Ch9's Vane, the previous high-water mark, traded against the slowest move speed and longest attack cooldown of any boss so far — "slow but world-ending if it lands." All three shipped abilities are live and useful in this fight: weakpoint-sight (Ch7) reads openings in his stutter; `OverdriveController` (Ch9, self-gated on `CampaignState.HasAbility`) opens windows in his monumental wind-ups; `PhaseStepController` (Ch10) is the primary survival tool against his committed arcs. `UnbrokenWard` is present on the rig from scene start but **not yet unlocked** — it self-gates the same way, so it cannot save the player from a lethal blow *in this very fight that grants it*, matching the narrative ("he's giving you the thing that kept him standing this long" — a gift received only after, never used during, his own death).

**The two ghost-manifestation `DreamPhantom`s fight alongside Aldric in the same `DefeatEnemies` step**, not a separate wave — this is the mechanical expression of "the dead keep interrupting" from the production note. Because `DreamPhantom.autoCycle` toggles Solid/Phased every 2 s independent of player action, a phantom struck while Phased simply heals the damage back (`Health.Heal()`), so a player fighting reactively rather than reading the phase timer will feel their hits "not count" roughly half the time by design — this is the intended texture, not a bug, but it is worth calling out explicitly here since nothing in the dialogue itself explains the phase mechanic to the player (Echo's Beat 2 Ghosts line covers the *narrative* rule — walk through the reaching dead, fight the ones sent to stop you — but never the *phase-cycle* mechanic). A future UI/haptic cue on phase transition (a distinct tell separate from Aldric's own combat feedback) is a reasonable follow-up, flagged here rather than added silently.

**Flagged gap — Aldric's signature flicker is unbuilt.** Canon writes Aldric as visually unstable throughout the fight — "his features flickering and de-resolving between man and shadow" (script 346), and the production note is explicit that this is a targeting mechanic, not just a visual: "Aldric himself flickers and de-resolves, sometimes a solid towering knight, sometimes half-dissolved into the broadcast, the player's targeting having to account for a boss who is partly not there… the player using weakpoint-sight to strike only when he is solid" (script 368). None of this exists in the build. Aldric is placed and fought as a plain, always-solid `Enemy` with no `DreamPhantom` (or any comparable Solid/Phased) component — unlike the two ghost-manifestations, which do carry exactly this Solid↔Phased cycle (see Beat 2b/2c above). The intended weakpoint-sight interaction — reading a flicker window to find the moment he is vulnerable — is therefore entirely absent: weakpoint-sight has no flicker to key off, and Aldric reads as a fully-solid enemy for the whole duel. Worth flagging alongside the ghost-phantom mechanic already documented, since it is the same idea (a boss who isn't always "there") applied to the wrong character.

**No mercy branch exists in the build**, matching the canon constraint exactly: `AuthorDefeatStep` simply waits for all three `Health` components (Aldric + both phantoms) to reach zero.

#### e. Dialogue / VO

Three sets, all advanced on Left-Hand "Talk" (Y), all comm-dark (Echo is the only voice in every line except Kira's and the Younger Self's, which are dream-voices, not crew):

`Dialogue_Beat2_Ghosts` (`ch11_beat2_ghosts`), position (0, 1, 304), 3 lines, ≈55 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Cipher. Comm's gone… It's just us now… So we don't fight the ones that only reach. We walk through. We only draw on the ones she sends to stop us." | 26 |
| Kira | "You came back. I knew you would. Come closer, it's cold out here… Just come closer." | 12 |
| Echo | "That's not her, Cipher… Anything down here that offers you rest is the dream trying to keep you. Walk through her. I know what it costs. Walk through her anyway." | 17 |

`Dialogue_Beat2_YoungerSelf` (`ch11_beat2_youngerself`), position (3, 1, 315), 5 lines, ≈90 s:

| Speaker | Line | sec |
|---|---|---|
| Younger Self | "You keep collecting the dead like they make a person… They're not you. They're just everyone you couldn't save." | 18 |
| Ronin-7 | "They're all I remember being… So who are you. You've got my face from before the scars." | 17 |
| Younger Self | "There was someone before the sword. Before the number… You already know where this ends." | 21 |
| Ronin-7 | "Then tell me his name. You're standing right there wearing it… Tell me his name." | 13 |
| Younger Self | "Not yet. You haven't earned him back… I'm sorry. That's the only kindness I'm allowed." | 21 |

`Dialogue_Beat2_Keeper` (`ch11_beat2_keeper`), position (0, 1, 327), 4 lines, ≈133 s:

| Speaker | Line | sec |
|---|---|---|
| Aldric | "Stay down here with the rest of them… I was the first. Knight. Knight-One… Lie down. Be kept. It is easier than what you are doing." | 52 |
| Echo | "Cipher, listen to me and don't argue. There's almost nothing in there… The kindest thing in the galaxy right now is to let the oldest cage finally stop standing… Him first." | 26 |
| Ronin-7 | "You've kept a grave. That's all this is… Rest is the only thing I've got that's true, and you've earned it longer than anyone." | 27 |
| Aldric | "Quiet me. They all say… Come and try, little newest thing. I will keep you with the rest." | 28 |

→ combat begins (step 9, non-verbal hand-off; Aldric's own last line doubles as the trigger).

`Dialogue_Beat2_Kill` (`ch11_beat2_kill`), position (0, 1, 330), 2 lines, ≈38 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "There. The first one gets to be the last one to put it down… Rest, old man. You were first. You were tired. You're done." | 16 |
| Echo | "It's coming to me, Cipher. His shadow… He's giving you the thing that kept him standing this long." | 22 |

`Dialogue_Beat2_Unbroken` (`ch11_beat2_unbroken`), position (0, 1, 332), 1 line, ≈26 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Feel that. The haze just let go of you. The dream can't hold you now, Cipher… Once. Spend it well. It's the only time the first of them ever got to give instead of keep." | 26 |

#### f. Audio / Haptics / VR Comfort

- **Flagged gap — `DreamscapeArena` has no ambience bed at all, the one dreamscape room that gets none.** The immersion-retrofit pass at the end of `BuildChapter11GhostsAndOrigins()` calls `BuildAmbienceLayer` exactly twice in this chapter — `ArkshipCoreAmbience` and `CradleDreadAmbience` (§7) — both for the quiet reveal/second-kill rooms; no equivalent call exists for `DreamscapeArena`, the 20×36 room where the Ghosts/Younger-Self/Keeper beats and the entire Aldric duel play out. Combined with two facts already on record elsewhere in this document — both existing ambience beds are mis-located into the real canyon rather than the dreamscape (§Beat 3c/4c), and `MemoryFlashbackController.heartbeatLoop` is left null (§7) — the net effect is that **the dreamscape currently has zero correctly-placed ambient audio anywhere in its three rooms**, including through its own climactic duel. A future pass should give the arena its own dread bed: a `DreamscapeArenaAmbience` layer at `dive.position + (0, 2.2, 16)` = world (0, 2.2, 316), matching the room's floor center. See §7's SFX table and reuse note for the fuller cross-reference tying all three gaps together.
- **No camera shake at any point**, including the moment the narcosis "lets go" of the player on the Unbroken grant — that effect is sold by the world's own drifting half-light easing (a `MemoryFlashbackController`/fog-adjacent visual cue, not asserted as a distinct scripted event in the current build) plus `AudioDirector` and haptics, never a camera trick.
- **The dive-entry teleport also carries a vertical delta, not just the 300 m horizontal jump.** `MemoryDiveController.EnterDive()` moves the rig from the Threshold area (`thresholdPos` y=−4, §2) up to `DreamscapeEntryPoint` (y=1, A.3) — a ~5 m rise layered onto the horizontal offset into dreamscape space. This is comfort-irrelevant for the same reason the horizontal jump is: it is an instant, non-lerped position/rotation set, not continuous locomotion, so there is no vestibular-mismatch motion for the comfort vignette to guard against. Stated here to close the one previously-unstated dimension of an otherwise fully-documented teleport.
- **Grant-moment haptic, keyed to the line.** `dlgUnbroken`'s single line is a direct invitation — "Feel that. The haze just let go of you." Because Unbroken is granted passively (`UnbrokenGranter`'s `AbilityGranter` fires on `OnEnable`, step 11 — no input, no toggle, per §9's ability-chain note), the gift is otherwise entirely un-felt in the player's hands: nothing in the current build distinguishes this moment from any other line landing. Specify a distinct `Haptics` relief-pulse — a soft, single release-of-tension pulse, not the hit-confirm pulse the combat pipeline already provides — fired alongside `UnbrokenGranter`'s activation, so the word "Feel" has something physical to land on. It is the one moment this passive gift can register in the body, not only on screen and in the ear.
- `UnbrokenWard`'s fear/narcosis-resistance half of the grant ("a mind it can't drown, a fear it can't pour into you") is **narrative-only** — per the component's own doc comment, there is no runtime fear/status/narcosis system in the codebase to hook, and per the Karpathy no-speculative-systems rule the component does not invent one. The mechanical half (survive one otherwise-lethal blow per life, registered as the rig `Health`'s `DeathInterceptor`) is fully implemented; the narrative half is carried entirely by dialogue.
- `AldricLight` is the single hottest-colored accent in the chapter ((0.7, 0.2, 0.18), intensity 1.4) — a red warning-glow specifically over the boss's placement, distinct from every other cool/neutral dreamscape light.
- Haptics carry every blade hit in the Aldric duel per the existing `BladeDamager`/`Haptics` pipeline — no new haptic authoring is required beyond what already exists, including for the ghost-manifestations' phase-cycle (a phased hit registers as a normal swing with no damage applied, which already reads correctly through the existing hit-feedback pipeline without special-casing).
- Comfort vignette will fire frequently in the Aldric duel — the heaviest, most attacker-dense fight in the chapter (boss + two cycling phantoms).
- **Candor note — Echo's comm-dark VO has no audio grammar distinguishing it as "the one real thing" in the dream.** Echo's Beat 0 line names the stakes directly — "I'm the one real thing you're bringing into that dream. Don't lose me in it" — and Sable's Beat 1 line underlines it: "Hold on to Echo. He's the one thing down there she can't copy." From the instant `EnterDive()` fires, Echo is the *only* real voice for the length of Beats 2–4 (§a) — everyone else the player hears is either a dream-figure (Kira, the Younger Self, Aldric) or, before the dive, the crew's degrading Beats 0–1 comm. Yet the rig's `EchoPresence` component (§2, Appendix A.5) is never tied to this distinction: Beat 2's dive dialogue sets (`ch11_beat2_ghosts`/`_youngerself`/`_keeper`/`_kill`/`_unbroken`, §e) emit Echo's VO from the same kind of distant, world-anchored `DialoguePlayer` position every other set in this chapter uses, spatially indistinguishable from the dream-voices around him or the crew comm that preceded the cut. So the one voice canon insists the player "hold on to" currently sounds exactly like everything else in the room. A future `AudioDirector` pass should route Echo's comm-dark VO through a close, in-head, or player-anchored mix — rig-relative rather than world-anchored — distinct from both the crew's world-anchored, degradation-filtered Beats 0–1 comm and the dream-figures' own placement, so "hold on to Echo" has an audio grammar to hold on to, not just a line on the page.

---

### Beat 3 — The Arkship Record (Origin Core — The Reveal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Rooms.ArkshipCoreShell` from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (`ArkshipCore`'s shell + walls, the hologram dressing) and **`BuildBeat3Logic()`** (the reach point, both dialogue players).
> **This beat has zero combat and zero new placements** — Aldric and both ghost-manifestations are already resolved (dead) by the time this beat's steps fire; the boss's `Health`/`Enemy` components remain on his now-inert corpse GameObject, simply never referenced again.

#### a. Narrative purpose & emotional target

With the keeper down but the dream still drifting, Ronin-7 walks lucidly (narcosis no longer touching him) into the arkship's dead heart and reads the origin record **alone** — Echo is explicit that this is the first major reveal in the saga carried by the two of them with no crew present at all, comm still cut. The reveal is staged as a diegetic, slow-resolving data-event rather than a lecture: rows of identical sleeping faces, then a lineage-tree (Knight → Ninja → Wraith → Ronin), then a scatter-map. This is the chapter's Ladder C rung-2 beat — the **informational** clone-truth reveal — with the **personal** detonation (that the last node holds Cipher's own face) explicitly deferred to Ch12. Echo turns the reveal on itself in the beat's second half, naming that the shadow-AI line was copied down the makes too — "every shadow I take in is a brother of mine I'm carrying out of the dark" — reframing every prior keeper-kill (Vane, Sever, Aldric) as ancestor-kills. Ronin-7's closing line is deliberately a refusal to carry the whole weight standing up: "whose face they cut mine from, I'll find when I've got the stomach for it" — planting, not resolving, the Ch12 question.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **Player:** free-walks from Aldric's death site (world z≈330) north to `ArkshipCoreReachPoint` (0, 1, 340), radius 6 m — the only gate in this beat.
- No new NPCs or enemies. Aldric's corpse GameObject remains in the scene (inactive `Health` post-death is not destroyed, matching the saga-wide convention of leaving defeated-boss GameObjects in place rather than despawning them).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 13 | Dialogue | `Dialogue_Beat3_Intro` (`ch11_beat3_intro`) — Echo corrects the assumption that killing Aldric ended the dream; names the breach ahead |
| 14 | ReachTrigger | Gates on `ArkshipCoreReachPoint` (0,1,340), radius 6 |
| 15 | Dialogue | `Dialogue_Beat3_Reveal` (`ch11_beat3_reveal`) — the sleeping-faces/lineage-tree/scatter-map data-event, read by Echo alone |
| 16 | Dialogue | `Dialogue_Beat3_EchoKin` (`ch11_beat3_echokin`) — Echo's own-shadow-is-also-copied turn; Ronin-7's banked, incomplete acceptance |

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `ArkshipCore` shell (16×16, floor/ceiling + 2 walls) | center world (0, 0, 342) | `Rooms.ArkshipCoreShell` | `…/Art/Generated/Rooms/ArkshipCoreShell.prefab` | **MISSING** |
| `ArkshipCoreLight` (accent) | world (0, 2.4, 342) | — | `ChapterEnvironmentProfile.accentLights["ArkshipCore"]` | profile |
| Origin-record hologram | world (0, 1.5, 342) | — | `BuildHologram(dive, (0,1.5,42))` — no registry key, procedural/shared helper | built |

**Note on the reveal's diegetic staging.** The production note is explicit that the reveal's visuals (sleeping faces → lineage-tree → scatter-map) should be a diegetic, slow-resolving data-event, not a wall of text. `BuildHologram` is the shared holographic-display helper already used elsewhere in the saga (e.g. Ch1's Khall reveal) — it is correctly parented to `dive` (so it is dreamscape-local, not world-space-absolute), but its content is generic; authoring the specific three-stage sleeping-faces/lineage-tree/scatter-map sequence onto it is new content-authoring scope, not a like-for-like art-prefab swap, and is out of this document's registry-refactor scope.

**Load-bearing content requirement — the lineage-tree stage must be branch-ordered Knight → Ninja → Wraith → Ronin.** This is the single most load-bearing piece of visual information in the entire chapter's reveal, and the doc has not said so until now: whoever authors `BuildHologram`'s content must render the four make-names as a legible, numbered/ordered branch sequence in that exact order. It is not optional set-dressing — Beat 4's homecoming line (`ch11_beat4_homecoming`, §Beat 4e) has Coral read it directly off the recovered fragment: "Go to the **third branch. Wraith. That's my make**." That line is only coherent if the tree the player saw here in Beat 3 was branch-ordered with Wraith third of four. A hologram authored without an explicit make order breaks a real cross-beat dependency, not just an aesthetic choice.

**Note on the hull-breach threshold.** §3's candor note flags "no hull-breach geometry for re-entry into the arkship hull through ancient breaches" as a canyon-wide gap, but does not restate it here where it actually matters most: `ArkshipCore`'s open south wall (A.3 — no north/south walls, open into `DreamscapeArena` and `Cradle`) is the one place in the build where the player physically passes from the open dreamscape arena into the sealed origin-record room, and canon frames that specific passage as *entry through a breach in the antique hull*. A future `Rooms.ArkshipCoreShell` commission should treat that south opening as the diegetic hull-breach threshold — the ancient wound the player walks through to reach the record — not merely as a missing wall left open for room-to-room flow.

**⚠ Flagged gap — `ArkshipCoreAmbience` is not offset into dreamscape space.** The immersion-retrofit pass at the end of `BuildChapter11GhostsAndOrigins()` calls `BuildAmbienceLayer("ArkshipCoreAmbience", new Vector3(0f, 2.4f, 42f), 5f, 16f, 0.4f)` — an **absolute world position**, unlike `BuildHologram(dive, …)`'s parented local offset immediately above it in the same file. World (0, 2.4, 42) sits in the **real bone-canyon**, between Tier2 (z=40) and Tier3 (z=58), not inside the dreamscape's `ArkshipCore` room the ambience layer is named for (world z=342). The ambience bed this beat is supposed to carry is therefore audible in the wrong location entirely — playing continuously in an empty stretch of the real canyon the player has already walked past by the time they reach this beat, and silent where the beat actually takes place. This is a confirmed, verifiable placement bug (not a stylistic gap): the fix is a one-line change to `dive.position + new Vector3(0f, 2.4f, 42f)`, but per this document's scope it is flagged, not fixed, here — it is exactly the class of change that needs its own reviewed pass rather than being bundled into an art-prefab refactor.

#### d. Combat

None. Beat 3 is pure reveal/traversal — no enemies exist to fight, matching the production note's framing of this beat as a diegetic data-event.

#### e. Dialogue / VO

Three sets, all advanced on Left-Hand "Talk" (Y), all still comm-dark:

`Dialogue_Beat3_Intro` (`ch11_beat3_intro`), position (0, 1, 335), 1 line, ≈23 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Don't mistake what just happened, Cipher. He's down, but the dream isn't… That's her. That's the one this whole canyon has been. Walk me to her." | 23 |

`Dialogue_Beat3_Reveal` (`ch11_beat3_reveal`), position (0, 1, 342), 2 lines, ≈55 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "It's waking up for you, Cipher, and only you… They didn't recruit an army, Cipher. Look at it. They copied one." | 24 |
| Echo | "And it didn't stop at copying. Watch the tree build under it… We haven't been killing strangers down here, Cipher. We've been killing your ancestors." | 31 |

`Dialogue_Beat3_EchoKin` (`ch11_beat3_echokin`), position (0, 1, 345), 2 lines, ≈43 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "I'm reading it with you, Cipher, the way I read the ledger… They're not just gifts. They're family coming home… We're gathering our own." | 25 |
| Ronin-7 | "Don't make me hold all of it standing up, Echo. Not yet… Whoever's still left to free down here is mine to protect. Starting with the one still dreaming in front of me." | 18 |

#### f. Audio / Haptics / VR Comfort

- No camera shake — this is a stationary reveal beat carried entirely by the hologram visual, `ArkshipCoreLight`'s cyan glow ((0.35, 0.9, 0.95), intensity 1.8 — the highest-intensity accent in the chapter), and VO performance.
- The canon node-pulse (§3's candor note) is a recurring anchor through both this beat and Beat 4, not just the descent — `ArkshipCoreLight`'s steady cyan is a distinct beat (the reveal's own light, no pulse behaviour) from the failing node-pulse proper, which lives ahead at `CradleLight` (Beat 4f) and gutters out only on the second kill.
- No haptics scripted (no combat, no grab/release input).
- Comfort vignette engages normally on the short walk from Aldric's death site to the reach point; the player is otherwise stationary for the reveal itself.

---

### Beat 4 — The Second Kill (Putting Down the Archive — Homecoming — Ch12 Hooks)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Rooms.CradleShell` from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (`Cradle`'s shell + walls, the completion canvas) and **`BuildBeat4Logic()`** (the reach point, the `DefeatEnemies` second-kill step, `ExitDreamscapeTrigger`, both homecoming dialogue players, `ChapterOutro`).
> **The Dreaming Archive is NOT a boss fight** — her `EnemyDefinition` (`Ch11EnsureDreamingArchiveDefinition`: maxHealth 1, damage 0, moveSpeed 0, attackCooldown 999) resolves the `DefeatEnemies` step on a single symbolic hit. Do not tune her like a combat encounter.
> **`ExitDreamscapeTrigger` must fire before the homecoming dialogue, never after** — the comm-restore beat is diegetically caused by her death, and the crew's Beat 4 lines assume the channel is already live.

#### a. Narrative purpose & emotional target

The reveal read, Ronin-7 turns to the dreamer who kept it — too far dissolved to bring up, the only mercy left is the same door he gave the keeper. This is staged as a **clean, quiet second kill**, explicitly not a boss fight and not a drawn-out eulogy (the chapter has already grieved once, over Aldric). On her death the broadcast ends for good: the dream collapses to cold real air, and the comm floods back all at once — the first time the channel has lived since the threshold of Beat 2. The grief here is kept light and trimmed by design: a brief Sable beat feeling her go, a brief Coral recognition of her own Wraith make off the recovered fragment (the reveal-anchor relocated here from Beat 3, per the production note), then Cassie closes the chapter with a plain "come home" folded directly into the Ch12 hooks in the same breath — the cryo-command vault, the Ronin shadows racked in the cold, and a rival force already racing for it. Echo's closing button plants the personal question the whole chapter has circled and leaves it open: "if the template was scattered, whose face is his?" No ally is gained this chapter, by design — the node is freed, not recruited, and Ch11 stays the saga's one origins/lore chapter with an unchanged roster.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Player:** free-walks from the reveal site (world z≈345) north to `CradleReachPoint` (0, 1, 352), radius 5 m.
- **The Dreaming Archive:** built via the same `Ch11BuildNamedBoss` helper as Aldric, at world (0, 0, 357), Euler(0,180,0), resolving to the real, disk-confirmed `The-Dreaming-Archive.prefab`. Reparented under `dive`, `SetActive(false)` until the `DefeatEnemies` step activates her. Her `EnemyDefinition` deliberately breaks the pattern every other boss in the saga sets: 1 HP, 0 damage, 0 move speed, 999 s attack cooldown — she never attacks, never moves, and dies on the first hit that lands. This is not a placeholder or an oversight; it is the direct mechanical expression of the production note's "NOT a boss fight… the only mercy left is the same door."
- **`ExitDreamscapeTrigger`:** built inactive, wired with `dive = diveController`, activated by step 20 immediately after the second kill and its mercy dialogue resolve. On `OnEnable`, `MemoryDiveController.ExitDive()` deactivates `diveRoot`, restores the pre-dive `RenderSettings` snapshot (fog/ambient — "the color floods back"), and teleports the rig to `diveExitPoint` = `DreamscapeExitPoint` (0, -3, 76), landing the player back in the real canyon a few meters past `ThresholdReachPoint`.
- **Exit-blocking note.** `DreamscapeExitPoint` teleports with `Quaternion.identity` (`Chapter11Builder.cs:264`), the same unrotated default `DreamscapeEntryPoint` uses — so on homecoming the player faces +Z, toward the platform's dead-end edge and the void beyond it where the homecoming/outro cluster hangs (see Beat 4c's candor note below), not toward canon's narrated climb direction ("the long climb back up through the leviathan's bones toward the canyon mouth," script 455 — i.e. −Z, back toward `SpawnGround`). Beat 2b's entry-blocking note treats the dive-entry rotation as load-bearing composed blocking, worth protecting from an unwitting future patch; the exit rotation deserves the identical protection but currently has none. It may be a deliberate choice — the crew descend to meet the player at the bottom rather than the player climbing to them (script 443) — but nothing in the build or this document records that as intentional until now. Recording it here for the same reason Beat 2b's note exists.
- **`ChapterOutro`:** at world `thresholdPos + (0,1,13)` = (0, -3, 85), inactive. `CampaignFlagSetter` sets **only** `"ch11_complete"` — no recruit flag, matching the chapter's ally-free design. `completeCanvas` ref = the `Ch11BuildCompleteCanvas` worldspace canvas at (0, -2.6, 86). `OnActivated` carries **two** persistent listeners this chapter, not the usual one: `CampaignFlagSetter.SetFlags` and `DreamReckoningTrigger.Acknowledge` (see below) — both fire together as the chapter closes.
- **Note on the unstaged crew arrival.** Canon closes the beat with "down a breach in the bone, headlamps appear: MORRIGAN and CORAL VEX picking their way down" (script 443) to the heart, paid off by Coral's Beat 4 homecoming line "we climb to you." This crew descent is deliberately not staged in the build — the whole beat plays out with the crew voice-only over the just-restored comm, consistent with the chapter-wide no-NPC-walk invariant (§1.1, §5). Recorded here alongside Beat 1a's drop-line note so both of the canon SETTING block's physical-arrival beats are on record as intentional omissions, matching how this document already dispositions the war-room and shifting-ground gaps.
- **`DreamReckoningTrigger`** (`DreamReckoning`): holds both ghost-manifestation `DreamPhantom` references and `acknowledgedFlag = "ch11_dream_ended"`. Per its own doc comment its `Acknowledge()` method is not self-firing (no `OnEnable` hook, unlike `AbilityGranter`/`MemoryDiveEntryTrigger`) — adding one would break its existing unit tests, which call `Acknowledge()` explicitly. Rather than invent a new self-firing wrapper for one beat, the class summary's decision wires it via `UnityEventTools.AddPersistentListener` onto `ChapterOutro.OnActivated`, the exact plumbing Ch7–Ch10 already use for `CampaignFlagSetter.SetFlags` at the same moment. **Note:** since both ghost-manifestation phantoms are already dead by this point (defeated in Beat 2's `DefeatEnemies` step, their `DreamPhantom.dissolved` already true), `Acknowledge()`'s dissolve call is effectively a no-op safety net for the ordinary playthrough — it exists to correctly close out the dream state regardless of how the two phantoms resolved, not because a live phantom is expected to be lingering here.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 17 | ReachTrigger | Gates on `CradleReachPoint` (0,1,352), radius 5 |
| 18 | Dialogue | `Dialogue_Beat4_Mercy` (`ch11_beat4_mercy`) — Ronin-7's mercy-kill line |
| 19 | DefeatEnemies | The Dreaming Archive's single symbolic hit |
| 20 | Trigger | Activates `ExitDreamscapeTrigger` → `MemoryDiveController.ExitDive()` — comm restored, dream ends, rig teleports back to the real canyon |
| 21 | Dialogue | `Dialogue_Beat4_Homecoming` (`ch11_beat4_homecoming`) — Sable feeling her go, Coral's Wraith recognition |
| 22 | Dialogue | `Dialogue_Beat4_TargetList` (`ch11_beat4_targetlist`) — Cassie's "come home" + the Ch12 hooks; Ronin-7's heading; Echo's closing question |
| 23 | Trigger | Activates `ChapterOutro` — sets `ch11_complete`, resolves `DreamReckoningTrigger.Acknowledge()`, reveals the complete canvas, fades, publishes `ZoneCompleted` |

> **⚠ Invariant — step 20 must precede step 21.** The homecoming dialogue is written to be delivered over a live, just-restored channel ("I felt her go, Cipher… the channel's clear, you're real again"); firing it before the dive exit would have Sable and Coral speaking over a comm that, per the narrative, does not yet exist.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `Cradle` shell (14×14, floor/ceiling + 3 walls) | center world (0, 0, 357) | `Rooms.CradleShell` | `…/Art/Generated/Rooms/CradleShell.prefab` | **MISSING** |
| `CradleLight` (accent) | world (0, 1.6, 357) | — | `ChapterEnvironmentProfile.accentLights["Cradle"]` | profile |
| The Dreaming Archive | world (0, 0, 357), Euler(0,180,0) | `Named.TheDreamingArchive` | `…/Art/Generated/Characters3D/Named/The-Dreaming-Archive.prefab` | **EXISTS** |
| "CHAPTER 11 COMPLETE" canvas | world (0, -2.6, 86) | — | `Ch11BuildCompleteCanvas` — procedural worldspace UI, no registry key | built |

**⚠ Flagged gap — `CradleDreadAmbience` shares the exact same offset bug as Beat 3's `ArkshipCoreAmbience`.** `BuildAmbienceLayer("CradleDreadAmbience", new Vector3(0f, 1.6f, 57f), 4f, 14f, 0.4f)` uses the raw world coordinate (0, 1.6, 57) — real-canyon space, between Tier2 (z=40) and Tier3 (z=58) — instead of `dive.position + new Vector3(0f, 1.6f, 57f)` = world (0, 1.6, 357), the actual `Cradle` room. Both ambience-layer calls in the immersion-retrofit pass share this bug; see Beat 3c for the fuller note. Flagged here for completeness, not duplicated as a separate fix.

**Candor note — the homecoming/outro cluster sits over void past the last built platform, and points the wrong way.** `Threshold` ends at its north edge z=78 (`Ch11BuildTier((0,-4,72))`, half-width 6 → z[66,78], §2). `Dialogue_Beat4_Homecoming` (0,-3,78) sits exactly on that edge; `Dialogue_Beat4_TargetList` (0,-3,82), `ChapterOutro` (0,-3,85), and `Ch11BuildCompleteCanvas` — the "CHAPTER 11 COMPLETE" canvas, world (0, -2.6, 86) — are all beyond it, floating over open space with no floor beneath them (§7, Appendix A.5). The player, landed by `ExitDive` at `DreamscapeExitPoint` (0, -3, 76), hears the chapter's entire homecoming/hook-out sequence emitting from 2–10 m ahead of them, over the terminal void past the canyon's dead end. An earlier version of this note described that space as "the same climb-out path the crew's homecoming dialogue narrates" — that was wrong: canon (script 455) is explicit the crew's climb home runs the *opposite* way, "the long climb back up through the leviathan's bones toward the canyon mouth" — i.e. −Z, back toward `SpawnGround`, not deeper past `Threshold`'s far edge. Recording this as an intentional-omission candor note, the same class as Beat 1a's drop-line note and Beat 4b's crew-descent note: the climb-out geometry canon narrates is entirely unbuilt, and the homecoming/outro cluster is currently staged over the terminal void rather than on any walkable ground, real or implied — not a documented decision, just an unreckoned gap. A future pass should either pull all four anchors back onto the `Threshold` platform (z ≤ 77, inside its x/z footprint) or build the up-canyon climb-out geometry the dialogue already assumes exists.

#### d. Combat — the second kill

**Not a fight.** The Dreaming Archive's `EnemyDefinition` (1 HP, 0 damage) means the `DefeatEnemies` step resolves on the first `BladeDamager` hit that lands, with no retaliation possible — the mechanical opposite of Aldric's Beat 2 duel. This is deliberate and matches the production note precisely: "stage this as a clean, quiet second kill, NOT a boss fight and NOT a drawn-out eulogy." No haptics beyond the single hit-confirmation pulse the existing `BladeDamager`/`Haptics` pipeline already provides for any kill; no bespoke feedback authored.

#### e. Dialogue / VO

Three sets, all advanced on Left-Hand "Talk" (Y). The first is still comm-dark; the second and third play over the just-restored channel:

`Dialogue_Beat4_Mercy` (`ch11_beat4_mercy`), position (0, 1, 355), 1 line, ≈18 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "I do for her what I did for him. The only door I've got… Whoever you were before they racked you, whatever name was yours, you go out as that, not as a node. I free you the only way that's left. Rest." | 18 |

→ combat resolves (step 19, single symbolic hit) → dive exits (step 20, comm restores).

`Dialogue_Beat4_Homecoming` (`ch11_beat4_homecoming`), position (0, -3, 78), 2 lines, ≈34 s:

| Speaker | Line | sec |
|---|---|---|
| Sable | "I felt her go, Cipher. The exact second there stopped being a person in there… The channel's clear, you're real again, and I'm not going to make you carry the rest of it alone down there in the dark. Come up." | 16 |
| Coral Vex | "Morrigan's pulling the fragment as we climb to you, Cipher, and I can see the tree on it from here. Go to the third branch. Wraith. That's my make… I'm not an exception. I'm an edition… Right now, climb." | 18 |

`Dialogue_Beat4_TargetList` (`ch11_beat4_targetlist`), position (0, -3, 82), 3 lines, ≈72 s:

| Speaker | Line | sec |
|---|---|---|
| Cassie-04 | "It's over with this one, Cipher… Climb out and come home… Two nodes down… One to go… The cryo-command vault. Deep in Dominion ground, in active custody, the Ronin shadows racked in the cold… So the next one isn't a dig. It's a race. But that's tomorrow. Tonight you come home." | 29 |
| Ronin-7 | "A race. Of course it is. The one node left holds my own make, the Ronin shadows, the latest of us… We rest the crew, we read the dead by name on the way like Vess sentenced me to, and then we go take the cold before anyone else can." | 21 |
| Echo | "We came down to gather an archive, Cipher, and we leave with the oldest truth in the galaxy and one more sister we couldn't save… So whose face is it, Cipher. The last node holds the Ronin shadows, and I've got a very bad feeling about what we see when we open the cold. Climb. We'll find out together." | 22 |

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point, including the comm-restore and the dive-exit teleport — the teleport itself is instant and comfort-safe by `MemoryDiveController`'s own design (no lerp, no camera motion), and the "broadcast ends" beat is sold by `RenderSettings` snapping back to the pre-dive canyon fog/ambient plus an `AudioDirector` comm-restore stinger, never a camera effect. The exit teleport carries its own small vertical delta, mirroring Beat 2f's entry note — `Cradle`'s floor (y=0) down to `DreamscapeExitPoint` (y=−3, §2/A.3), a ~3 m drop — comfort-safe for the identical instant-cut reason.
- `CradleLight` is the warmest, dimmest accent in the dreamscape ((0.4, 0.28, 0.15), intensity 1 — the lowest intensity of any dreamscape light), reading as a last, small warmth at the place a life ends rather than a dramatic climax light. `CradleLight` already carries `AddAmbientPulse` (period 7.2 s, A.1) — a slow breathing warmth that reads, unremarked until now, as exactly the "dim node-pulse" §3's candor note describes. **Flagged gap — the pulse's canon gutter-out has no scripted event.** Per script 443, on the Dreaming Archive's death "the dim pulse in the ancient cradle gutters, steadies for one last soft beat, and goes out" — a specific climactic light-and-audio beat, not merely the light staying static or being deactivated with the rest of `diveRoot` on `ExitDive()`. Nothing in the current build slows or stops `AddAmbientPulse`'s cycle, or pairs it with a matching audio cue, on the second kill; the light simply vanishes along with everything else under `dive` when `diveGo.SetActive(false)` fires. Specifying a distinct faltering-then-out light behaviour (and an `AudioDirector` sting to match) timed to step 19's `DefeatEnemies` resolution, ahead of step 20's `ExitDive`, would give the chapter's climactic image an actual implementation rather than an implicit deactivation.
- No haptics beyond the single kill-confirmation pulse (see d.).
- **Optional comm-restore body-haptic, paralleling the Unbroken grant-haptic (Beat 2f).** Sable's homecoming line is "the channel's clear, you're real again." A gentle single haptic pulse fired alongside `ExitDive` — distinct from the kill-confirmation pulse above — would give "you're real again" the same tactile anchor suggested for the Unbroken grant, reinforcing the return-to-body without camera motion. Optional; it closes the haptic bookend but is not required for the beat to read correctly.
- Comfort vignette resumes normal behavior once the player is back in the real canyon for the homecoming dialogue and the climb-out; the dive-exit teleport itself bypasses the vignette entirely, matching the same "instant cut, not a locomotion event" treatment as dive entry.

---

## 5. Character travel-route master table

**Chapter 11 has no `NpcWalker` instances at all — no named character physically walks anywhere in this chapter.** This is a hard invariant unique to Ch11 among the chapters documented so far (Ch1's Kessler, Ch9's Kessler-equivalent NPCs travel on rails via `NpcWalker`+`Trigger`; Ch11 has nothing analogous). Every character resolves one of two ways:

| Character | Presence | Placement |
|---|---|---|
| Cassie-04, Sable, Coral Vex, Vess, Gryph, Ronin-7's own crew pool | voice-only, comm | never physically instantiated; every line is a plain `DialoguePlayer` entry, speaker label recorded under the plain name (e.g. "Sable", not "Sable (comm)" — the "(comm)" tag is a stage direction, not part of the recorded speaker identity, per `Chapter11Lines.cs`'s header comment) |
| Kira | non-combat ghost anchor | placed once at world (-4, 0, 306), `StoryNpc` only, never moves |
| Younger Self | non-combat ghost anchor | placed once at world (4, 0, 314), `StoryNpc` only, never moves — reuses the `Ronin-7_Cipher_Soren.prefab` mesh (see Beat 2c casting note) |
| Aldric / Knight-1 | combat boss | placed once at world (0, 0, 330), inactive until Beat 2's `DefeatEnemies` step, then fights in place — never walks toward or away from the player beyond ordinary `Enemy` AI approach behavior |
| The Dreaming Archive | combat (single-hit) | placed once at world (0, 0, 357), inactive until Beat 4's `DefeatEnemies` step, never moves (her `EnemyDefinition.moveSpeed = 0`) |
| Ghost-manifestation ×2 | combat (`DreamPhantom`) | placed once at world (-3,0,326)/(3,0,326), inactive until Beat 2's `DefeatEnemies` step, fight via ordinary `Enemy` AI approach behavior — no scripted route |

This absence is thematically apt: every physically-present character in this chapter is either already dead (Aldric, the Dreaming Archive, the ghost-manifestations) or a fragment of memory that cannot leave the place it is anchored to (Kira, the Younger Self) — there is no one alive and mobile to script a walk for. The only "travel" any character undergoes is the **player's own** teleport via `MemoryDiveController` (Beat 1→2 entry, Beat 4→real-canyon exit), which is deliberately not modeled as an `NpcWalker`-style scripted path but as an instant, comfort-safe position/rotation set.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`** (canyon accents) or the local `MemoryFlashbackController` instance (dreamscape fog/ambient), never typed inline in a refactored builder. Their current literals are in Appendix A.1.

| Beat | Mood | Key/accent entries | Behaviour | What changes during the beat |
|---|---|---|---|---|
| 0 — The Cairn | neutral, war-room-adjacent | `accentLights["Spawn"]` | none | static; no lighting event this beat |
| 1 — Bone-Canyon | cooling, darkening with depth | `accentLights["Tier1"/"Tier2"/"Tier3"/"Threshold"]` | none authored | palette lerps bone→vault-blue across the three numbered tiers (art-authored gradient, not a runtime behaviour); no dynamic lighting event until the dive teleport |
| 2 — Dreamscape (Arena) | grey narcosis haze, red boss-glow | `MemoryFlashbackController`'s fog (0.35,0.37,0.42)/ambient (0.30,0.30,0.34); `accentLights["Arena0"/"Arena1"/"Aldric"]` | none authored on the accents | **On dive entry:** `RenderSettings` snap to the flashback treatment (no transition, applied in `Awake`/`EnterDive`). **On Aldric's death:** no lighting change — the dream's mood persists past the keeper-kill, matching "killing him didn't quiet her". **Unremarked until now:** `AldricLight` activates with the rest of the arena on dive entry and burns over Aldric's empty placement through steps 6–8, before he is revealed at step 9 — see Beat 2c's flagged gap |
| 3 — ArkshipCore | coldest, most saturated cyan | `accentLights["ArkshipCore"]` (0.35,0.9,0.95), intensity 1.8 — highest in the chapter | none authored | the hologram reveal is the beat's one visual event; no accompanying lighting cue beyond the hologram's own glow |
| 4 — Cradle → real canyon | warm-dim, then cold-real | `accentLights["Cradle"]` (0.4,0.28,0.15), intensity 1 — lowest in the chapter; then the canyon's baseline fog/ambient | none authored | **On the Dreaming Archive's death (`ExitDive`):** `RenderSettings` restore to the pre-dive canyon snapshot exactly — "the color floods back," a hard cut, not a fade |

Fog inside the dreamscape is `ExponentialSquared` (denser falloff, `MemoryFlashbackController`'s convention), distinct from the real canyon's `Exponential` fog — the two regions are visually and mechanically separate environments, never blended.

## 7. Audio / VO manifest cross-reference

Fourteen canonical dialogue sets, defined in `Chapter11Lines.cs` and consumed via `Chapter11Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch11_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch11_beat1_descent` | 1 | (0, 1, 12) — `Dialogue_Beat1_Descent` |
| `ch11_beat1_arkship` | 1 | (1, -2, 60) — `Dialogue_Beat1_Arkship` |
| `ch11_beat2_ghosts` | 2 | (0, 1, 304) — `Dialogue_Beat2_Ghosts` |
| `ch11_beat2_youngerself` | 2 | (3, 1, 315) — `Dialogue_Beat2_YoungerSelf` |
| `ch11_beat2_keeper` | 2 | (0, 1, 327) — `Dialogue_Beat2_Keeper` |
| `ch11_beat2_kill` | 2 | (0, 1, 330) — `Dialogue_Beat2_Kill` |
| `ch11_beat2_unbroken` | 2 | (0, 1, 332) — `Dialogue_Beat2_Unbroken` |
| `ch11_beat3_intro` | 3 | (0, 1, 335) — `Dialogue_Beat3_Intro` |
| `ch11_beat3_reveal` | 3 | (0, 1, 342) — `Dialogue_Beat3_Reveal` |
| `ch11_beat3_echokin` | 3 | (0, 1, 345) — `Dialogue_Beat3_EchoKin` |
| `ch11_beat4_mercy` | 4 | (0, 1, 355) — `Dialogue_Beat4_Mercy` |
| `ch11_beat4_homecoming` | 4 | (0, -3, 78) — `Dialogue_Beat4_Homecoming` |
| `ch11_beat4_targetlist` | 4 | (0, -3, 82) — `Dialogue_Beat4_TargetList` |

**Note — the last two rows sit at/past the `Threshold` platform's built edge (z=78).** `Dialogue_Beat4_Homecoming` (z=78) is right at the edge; `Dialogue_Beat4_TargetList` (z=82) is already 4 m into open space, as are `ChapterOutro` and the completion canvas beyond it (Appendix A.5). See Beat 4c's candor note for the full picture and the canon direction correction.

**Chapter total ≈ 919 s (15 min 19 s) across 14 sets / 41 lines** — summing the per-set runtimes already given in §4's dialogue tables. §7's manifest is otherwise the one table in this document that doesn't roll up to a chapter-wide figure the way it's diligent about per-beat totals elsewhere; this line lets a VO-batch reviewer sanity-check completeness at a glance without re-adding the column by hand.

Each is built by the local `Ch11BuildDialogue` wrapper (not the shared clip-loading path, which looks in the wrong folder for this chapter): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch11WireVoiceClips`, resolving each line's `AudioClip` from `Chapter11Lines.ClipName(setId, index, speaker)` — pattern `ch11_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all fourteen `DialoguePlayer`s.

**Speaker-label note.** Comm-tagged lines in the source dialogue script ("Sable (comm)", "Coral Vex (comm)", "Gryph (comm)", "Vess (comm)") are recorded in `Chapter11Lines.cs` under the character's plain name — "(comm)" is a stage direction, not part of the speaker's identity, matching every other chapter's convention (Ch1's "Handler (Hologram)" is the one deliberate exception, and that is a narrative concealment device, not a formatting inconsistency).

SFX/ambience bed, all under `Assets/Ronin7/Art/Generated/Audio` or built procedurally via `ProceduralAudioClipBuilder`:

| Clip / layer | Used for |
|---|---|
| `DreamscapeArenaAmbience` — **does not exist** | intended for the arena/duel room, where the Ghosts/Younger-Self/Aldric beats and the whole boss fight play out; **no `BuildAmbienceLayer` call exists for `DreamscapeArena` at all** — unlike the two rows below, which exist but are mislocated, the arena gets no ambience call whatsoever. See Beat 2f's flagged gap for the fuller picture and a proposed placement. |
| `ArkshipCoreAmbience` (`BuildAmbienceLayer`) | intended for the origin-reveal room; **currently placed in real-canyon space, not dreamscape space** — see Beat 3c's flagged gap |
| `CradleDreadAmbience` (`BuildAmbienceLayer`) | intended for the second-kill room; **same placement bug** — see Beat 4c |
| `MemoryFlashbackController`'s optional `heartbeatLoop` | the dreamscape's ambient dread bed, 2D, volume 0.35 — not wired to a specific clip in the current build (`heartbeatLoop` left null unless authored). Per §3's candor note, canon's "dim failing pulse" (script 201/249/275) is the natural clip to wire here — a slow, failing heartbeat that could falter further toward the Cradle — rather than a generic dread bed; currently unspecified beyond "left null." |
| `ConsoleFlicker` on `ArkshipCoreLight` (seed 121) | the origin-reveal room's one lighting-behaviour event |
| `AmbientPulse` on `CradleLight` (period 7.2s) | the second-kill room's one lighting-behaviour event, a slow breathing warmth |
| `ReverbZonePlacer.AutoTagInteriorVolumes()`/`PlaceReverbZonesForInteriorVolumes()` | tags and reverb-zones the dreamscape's three rooms — **not confirmed correct, only assumed; verify rather than trust.** `DreamscapeArena`/`ArkshipCore`/`Cradle` share open walls with one another (no north wall on Arena, no north/south walls on Core, Appendix A.3) — the same open-plan geometry Beat 2c's light-bleed note relies on — so interior-volume auto-tagging that keys on enclosure may merge the three into one zone or fail to tag the open-plan volumes at all, rather than producing three distinct reverb signatures. Confirm in the built scene (§8 checklist item 10) before relying on this row; the upper canyon is open-air regardless and gets no comparable dry/anechoic treatment of its own — see Beat 1f's canyon-acoustics note |

**Reuse note — the descent audio is one coherent commission, not three unrelated asks.** `heartbeatLoop`'s failing node-pulse (above), Sable's depth/proximity barks (Beat 1f), and the dead's half-heard whisper-bleed under the comm (Beat 1f) are a single descent soundscape: a dying beacon's slow failing pulse, a voice tracking it going out as the player gets closer, and the known dead surfacing in the channel until it cuts at the threshold. This mirrors how Appendix B groups the three dust/dissolve VFX keys as one commission rather than three — a future audio pass should build all three together, so the node-pulse heartbeat does not land as generic ambience without the bark and whisper layers that make it read as a beacon the player is descending toward and losing.

**The dreamscape half of the same picture: the dream is currently silent of ambience.** Where the canyon's descent soundscape above is three unbuilt layers waiting on one commission, the dreamscape's own ambient hole is a working absence hiding across three separate notes rather than stated once — `DreamscapeArena` gets no `BuildAmbienceLayer` call at all (row above), `ArkshipCoreAmbience`/`CradleDreadAmbience` exist but are mislocated into the real canyon (Beat 3c/4c), and `heartbeatLoop` is left null (row above). Net effect: the climactic duel, the reveal, and the second kill all currently play in an acoustically empty room. A future audio pass should treat this as one dreamscape-silence problem — the missing arena bed, the two location fixes, and the heartbeat wiring — as cleanly as the canyon-descent commission above treats its own three layers.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 11 — Ghosts and Origins** (`XRRigBuilder.BuildChapter11GhostsAndOrigins()`).
2. **EditMode is the gate.** Baseline as of 2026-07-03 is **506 tests** (503 pass / 3 skip / 0 failed) — see `Project/Docs/CHAPTER-BUILD-LEDGER.md`. Verify against the project's current baseline before treating a fresh run as green; do not assume 506 is still current without checking. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot.** No EditMode test invokes `BuildChapter11GhostsAndOrigins()` or loads `Ch11_GhostsAndOrigins.unity`. The suite's Ch11-specific coverage (`UnbrokenWard` 8 fixtures, `Chapter11Lines` 8 fixtures, `HealthTests` interceptor ×4) covers pure logic only — the death-interceptor decision seam, the dialogue-line data, and the interceptor registration lifecycle. **A green suite says nothing about whether the scene still builds correctly**, and nothing about whether the dreamscape's two flagged ambience-placement bugs (§Beat 3c/4c) are ever caught by automation — they are silent at the data level and only visible by listening in the built scene. Every structural change in this refactor must be verified by opening the scene and looking (and listening) at it.
3. **Dive lifecycle regression coverage.** `MemoryDiveController`'s `EnterDive`/`ExitDive` are chapter-agnostic and covered by whatever shared test suite backs Ch3/Ch7/Ch8's memory-dive usage; there is no Ch11-specific dive test. Manually verify: entering the dive via the Beat 1 threshold teleports the rig to `DreamscapeEntryPoint` with no visible camera cut/jolt beyond the instant position set; exiting via Beat 4 restores the real canyon's fog/ambient exactly (not a lerp, not a residual dreamscape tint).
4. **Safe-zone survival test.** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there**, and this must hold whether or not the dive was ever entered during the session that added it (§1.4's dreamscape-specific hazard).
5. **Fallback audibility test.** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry, both the real canyon and the dreamscape) plus one `LogWarning` per unresolved key — never an empty room, never an exception.
6. **Perf reference bar.** No baseline recorded yet (§1.6). **Establish one at the first `UnityStats` read** — measure both the canyon-only state and the dreamscape-active state separately, and record both here before any prefab swap.
7. **Console check:** `Ch11WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild.
8. **Ghost-anchor parenting check (new, targeted at the flagged gap in §Beat 2b).** After a fresh build, with the dive never entered, search the scene hierarchy for `Kira` and `Younger Self` — confirm whether they appear as scene-root objects (current, flagged behavior) or as children of `Dreamscape` (the fixed behavior, if a future patch addresses it). Either way, confirm both are inactive-consistent with player expectations before proceeding with further art work on this beat.
9. **Open-air tier edge-fall check (new, targeted at Beat 1f's flagged gap).** Walk off a tier's side edge (e.g. Tier1's x=-6 or x=8 boundary, not the ramp-connected ends) on continuous locomotion and confirm what actually happens — soft push-back, a kill-floor/reset, or an accepted fall through the fog. `ZoneBounds`'s single bounding sphere does not gate this; record the observed behaviour here once checked, since none is currently documented.
10. **Reverb-zone tagging check (new, targeted at §7's open-plan caveat).** `DreamscapeArena`/`ArkshipCore`/`Cradle` share open walls with one another (no north wall on Arena, no north/south walls on Core, Appendix A.3) — confirm in the built scene that `ReverbZonePlacer.AutoTagInteriorVolumes()` actually lands three distinct reverb zones for the three rooms, rather than merging them into one continuous zone or failing to tag the open-plan volumes at all, and confirm the acoustically-dry open canyon isn't accidentally swept into any of them.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter11GhostsAndOrigins()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter11Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** once the wipe strategy is converted — see `Project/Docs/IMPROVEMENT-SUMMARY.md`.
- **Do not auto-delete orphan materials.** Reversible cleanup only, matching the project-wide convention.
- **Reject any prefab import that introduces a `MeshCollider`.** All 14 current scenes are MeshCollider-free. Room shells and tier platforms get primitive colliders; nothing in this chapter needs a glass/collider-only pane the way Ch1's windshield does.
- **Two confirmed, verifiable placement bugs, flagged not fixed (this document's scope is content, not code):**
  1. `Ch11PlaceGhostNpc` never reparents Kira or the Younger Self under `Dreamscape`, so both remain active scene-root objects unaffected by `diveGo.SetActive(false)` — see §Beat 2b.
  2. `ArkshipCoreAmbience` and `CradleDreadAmbience` (`BuildAmbienceLayer` calls in the immersion-retrofit pass) use absolute world coordinates instead of `dive.position`-relative offsets, placing both ambience beds in the real bone-canyon instead of their named dreamscape rooms — see §Beat 3c/4c.

  Both are one-line fixes in `Chapter11Builder.cs`, but per this document's registry-refactor scope, a future reviewed change should address them rather than this pass silently patching code while documenting content.
- **Naturalness audit deferred, not resolved.** `story ouput/audit/Ch11_audit.md` graded the dialogue script C+ on naturalness with **zero hard script/canon errors and zero em-dash violations** in character speech — its findings (the saga-wide "not X, it's Y" antithesis tic appearing ~18 times, aphorism-stacking in the keeper-kill monologues, two reveal-dumps delivered as lecture rather than shown) are explicitly deferred by `00_AUDIT_SUMMARY.md` to "a dedicated pass," and `Chapter11Lines.cs` transcribes the source `Line:` text verbatim rather than pre-empting that pass, mirroring how Ch9Lines/Ch10Lines only fixed what their own audits flagged as hard errors. Do not silently rewrite dialogue lines while doing an art pass — that pass has its own scope and sign-off.
- **The ability chain and its ordering invariant.** Unbroken is the fourth permanent unlock (weakpoint-sight Ch7 → Overdrive Ch9 → Phase-step Ch10 → Unbroken Ch11 → Mirror Ch12), granted **passively** — no input action, no toggle, registering as the rig `Health`'s `DeathInterceptor` and firing at most once per life. `AttachPlayerAbilities` adds `UnbrokenWard` to the rig in every chapter (self-gating on `CampaignState.HasAbility(AbilityId.Unbroken)` in `Awake`, exactly like the prior three abilities), so it is present but inert before this chapter's `UnbrokenGranter` fires. Do not add a dedicated input binding for Unbroken in a future patch — its passivity is canon ("the mind-ward that lets you walk the still-active dream" is not something the player activates).
- **Ally-free chapter, by design.** Unlike every other Act III chapter documented so far, Ch11 recruits no one — `CampaignFlagSetter` sets only `"ch11_complete"`, no companion-unlock flag. The node is "too broken to recruit… this is the origins/lore chapter," per the beat treatment. Do not add a recruit flag or an `AllyCombatant` placement for any Ch11 character in a future patch without an explicit story-side sign-off; it would contradict the chapter's stated design intent.
- **The two-kill structure is load-bearing.** Killing Aldric (Beat 2) grants Unbroken but explicitly does **not** end the dream or restore comm — only the Dreaming Archive's death (Beat 4) does both. This is unique among the saga's memory-dive chapters and must not be collapsed into a single kill in any future rebalancing pass; it is the mechanism by which Beat 3's reveal is read in isolation (Echo alone, comm still cut) rather than with the crew present.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All canyon/dreamscape structural geometry is `GameObject.CreatePrimitive` cubes tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch11Environment.asset`. Currently set inline at the top of `BuildChapter11GhostsAndOrigins` (`Chapter11Builder.cs:117-135`).

| | Value |
|---|---|
| Directional key | color (0.6, 0.62, 0.68), intensity 0.3, rotation Euler(55, -35, 0) |
| Ambient (real canyon) | mode **Flat**, color (0.06, 0.06, 0.08) |
| Fog (real canyon) | mode **Exponential**, color (0.08, 0.08, 0.1), density 0.018 |

**Canyon accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range |
|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.7, 0.72, 0.8) | 1 | 10 |
| `Tier1Light` | (2, 1.4, 22) | (0.65, 0.68, 0.76) | 1.2 | 12 |
| `Tier2Light` | (-2, 0.4, 40) | (0.55, 0.6, 0.7) | 1.3 | 12 |
| `Tier3Light` | (1, -0.6, 58) | (0.35, 0.5, 0.65) | 1.5 | 14 |
| `ThresholdLight` | (0, -1.6, 72) | (0.3, 0.42, 0.55) | 1.6 | 12 |

**Dreamscape accent point lights** (positions are `dive.position + offset`, `dive.position = (0,0,300)`):

| Light | Local offset | World position | Color | Intensity | Range |
|---|---|---|---|---|---|
| `ArenaLight0` | (-4, 2.2, 8) | (-4, 2.2, 308) | (0.5, 0.5, 0.6) | 1.1 | 14 |
| `ArenaLight1` | (4, 2.2, 22) | (4, 2.2, 322) | (0.55, 0.45, 0.55) | 1.2 | 14 |
| `AldricLight` | (0, 2.4, 30) | (0, 2.4, 330) | (0.7, 0.2, 0.18) | 1.4 | 12 |
| `ArkshipCoreLight` | (0, 2.4, 42) | (0, 2.4, 342) | (0.35, 0.9, 0.95) | 1.8 | 14 |
| `CradleLight` | (0, 1.6, 57) | (0, 1.6, 357) | (0.4, 0.28, 0.15) | 1 | 10 |

**Lighting behaviours:** `AddConsoleFlicker("ArkshipCoreLight", seed: 121f)`; `AddAmbientPulse("CradleLight", periodSeconds: 7.2f)`. No other accent carries a behaviour.

### A.2 World root — the bone-canyon (`BoneCanyon`)

| Object / Method | Value |
|---|---|
| `SpawnGround` | `BuildFloorCeiling(world, "SpawnGround", (0,0,4), (16,0,12), (0.2,0.2,0.22), (0.08,0.08,0.09))` |
| Tier color lerp | `Color.Lerp(boneColor (0.5,0.48,0.44), vaultColor (0.24,0.3,0.4), t)`, `t = InverseLerp(0, 2, i)` for `i` in {0,1,2} |
| `Ch11BuildTier(parent, name, center, floorColor)` | floor: cube at `center + (0,-0.2,0)`, scale `(12, 0.4, 12)`; `{name}_RibA` prop at `center + (-2.4,0.6,-1.6)`, scale (0.6,1.4,0.6), tint `floorColor*0.7`; `{name}_RibB` prop at `center + (2.2,0.5,1.5)`, scale (0.55,1.2,0.55), tint `floorColor*0.7`; `{name}_Glow` accent light at `center + (0,2.2,0)`, tint `floorColor`, intensity 1, range 9 |
| `Tier1`/`Tier2`/`Tier3` centers | (2,-1,22) / (-2,-2,40) / (1,-3,58) |
| `Threshold` | `Ch11BuildTier(world, "Threshold", (0,-4,72), (0.18,0.24,0.32))` — outside the bone→vault lerp, its own fixed cold tint |
| `Ch11BuildRamp(parent, name, from, to, width)` | cube at midpoint, rotated `Euler(-angle,0,0)` where `angle = Atan2(rise,run)*Rad2Deg`, scale `(width, 0.4, length)`, tint `(0.3,0.3,0.32)`, `width = Ch11TierHalfWidth*2 = 12` |
| `Ramp0`…`RampThreshold` | see §2 ramp table for exact from/to/length/grade |
| `ArkshipHull` | `BuildProp(world, "ArkshipHull", tiers[2]+(-6,2,6) = (-5,-1,64), (3,4,10), (0.3,0.32,0.36))` |

### A.3 The dreamscape island (`Dreamscape`, offset to `(0,0,300)`)

| Object / Method | Value |
|---|---|
| `MemoryFlashbackController` | on the `Dreamscape` root; default fog (0.35,0.37,0.42) density 0.045, ambient (0.30,0.30,0.34), no `heartbeatLoop` wired |
| `DreamscapeArena` | `BuildFloorCeiling(dive, "DreamscapeArena", (0,0,16), (20,0,36), (0.14,0.14,0.18), (0.06,0.06,0.08))`; walls `Arena_WallW` (-10,1.8,16) (0.2,3.6,36), `Arena_WallE` (10,1.8,16) (0.2,3.6,36), `Arena_WallS` (0,1.8,-2) (20,3.6,0.2) — no north wall (open into `ArkshipCore`) |
| `ArkshipCore` | `BuildFloorCeiling(dive, "ArkshipCore", (0,0,42), (16,0,16), (0.04,0.06,0.08), (0.02,0.03,0.04))`; walls `Core_WallW` (-8,1.8,42) (0.2,3.6,16), `Core_WallE` (8,1.8,42) (0.2,3.6,16) — no north/south walls (open into `DreamscapeArena` and `Cradle`) |
| `BuildHologram(dive, (0,1.5,42))` | the origin-record display, parented (correctly) to `dive` |
| `Cradle` | `BuildFloorCeiling(dive, "Cradle", (0,0,57), (14,0,14), (0.05,0.05,0.06), (0.02,0.02,0.03))`; walls `Cradle_WallW` (-7,1.8,57) (0.2,3.6,14), `Cradle_WallE` (7,1.8,57) (0.2,3.6,14), `Cradle_WallN` (0,1.8,64) (14,3.6,0.2) — the chapter's dead end |
| Kira | `Ch11PlaceGhostNpc(Ch11KiraPrefab, dive.position+(-4,0,6), "Kira")` — **not reparented under `dive`** (§Beat 2b flagged gap) |
| Younger Self | `Ch11PlaceGhostNpc(Ch11YoungerSelfPrefab, dive.position+(4,0,14), "Younger Self")` — same flagged gap |
| Ghost-manifestation ×2 | `BuildEnemy(dive.position+(-3,0,26), playerHealth, ghostDef)` / `BuildEnemy(dive.position+(3,0,26), ...)`, each `+AddComponent<DreamPhantom>()`, `SetActive(false)` |
| Aldric / Knight-1 | `Ch11BuildNamedBoss(Ch11AldricPrefab, dive.position+(0,0,30), "Aldric / Knight-1", aldricDef, playerHealth)`, reparented under `dive`, `SetActive(false)` |
| The Dreaming Archive | `Ch11BuildNamedBoss(Ch11DreamingArchivePrefab, dive.position+(0,0,57), "The Dreaming Archive", archiveDef, playerHealth)`, reparented under `dive`, `SetActive(false)` |
| `diveGo.SetActive(false)` | called once, after all of the above — deactivates everything actually parented under `dive` (rooms, walls, lights, hologram, ghost-manifestations, Aldric, the Archive); does **not** deactivate Kira or Younger Self (see flagged gap) |
| `DreamscapeEntryPoint` | (0, 1, 300) — `dive.position + (0,1,0)` |
| `DreamscapeExitPoint` | (0, -3, 76) — `thresholdPos + (0,1,4)`, `thresholdPos = (0,-4,72)` |
| `DreamscapeDive` (`MemoryDiveController`) | `diveRoot`=`diveGo`, `diveEntryPoint`=`DreamscapeEntryPoint`, `diveExitPoint`=`DreamscapeExitPoint`, `rigRoot`=rig transform, `flashback`=`Dreamscape`'s `MemoryFlashbackController` |
| `EnterDreamscapeTrigger` / `ExitDreamscapeTrigger` | inactive `GameObject`s carrying `MemoryDiveEntryTrigger`/`MemoryDiveExitTrigger`, each wired `dive = diveController` |

**Collider convention.** `BuildFloorCeiling` keeps the floor cube's collider (walkable) but strips the ceiling cube's (`Object.DestroyImmediate(ceil.GetComponent<Collider>())` — visual-only). This applies to every room built with it in this chapter (`SpawnGround`, `DreamscapeArena`, `ArkshipCore`, `Cradle`). A future room-shell prefab replacing any of these must reproduce the same asymmetry — a walkable floor collider under a collider-free ceiling mesh — to preserve both collision behaviour and the chapter's MeshCollider-free invariant (§9).

### A.4 Data assets (`EnemyDefinition`s)

| Asset | maxHealth | damage | moveSpeed | attackCooldown | Notes |
|---|---|---|---|---|---|
| `Ch11GhostManifestation.asset` | 35 | 7 | 1.3 | 1.0 | lightest stat block in the chapter |
| `Ch11Aldric.asset` | 360 | 32 | 0.9 | 1.5 | heaviest/slowest in the saga to date |
| `Ch11DreamingArchive.asset` | 1 | 0 | 0 | 999 | never attacks; single symbolic hit ends the `DefeatEnemies` step |

### A.5 Reach points, ability rig, and misc

| Object | Value |
|---|---|
| `MidCanyonReachPoint` | `tiers[2] + (0,1,0)` = (1, -2, 58) |
| `ThresholdReachPoint` | `thresholdPos + (0,1,0)` = (0, -3, 72) |
| `ArkshipCoreReachPoint` | `dive.position + (0,1,40)` = (0, 1, 340) |
| `CradleReachPoint` | `dive.position + (0,1,52)` = (0, 1, 352) |
| Player rig | `BuildRig(refs, addLocomotion:true)` + `EchoPresence`; `ZoneBounds` center (0,0,150) radius 260; `AttachPlayerAbilities` adds `WeakpointSight`, `OverdriveController`, `PhaseStepController`, `UnbrokenWard` (self-gated), `MirrorSummonController` (self-gated, Ch12's ability, harmlessly present) |
| Katana "Echo" | `BuildSword((2,1,4), Euler(-90,0,0), weapon, Ch11EchoBladePrefab)` |
| `UnbrokenGranter` | `AbilityGranter` with `abilityId = AbilityId.Unbroken`, `SetActive(false)` |
| `DreamReckoning` (`DreamReckoningTrigger`) | `phantoms` = both ghost-manifestation `DreamPhantom`s, `acknowledgedFlag = "ch11_dream_ended"` |
| `ChapterOutro` | position `thresholdPos + (0,1,13)` = (0,-3,85); `completeCanvas` = `Ch11BuildCompleteCanvas` at `thresholdPos + (0,1.4,14)` = (0,-2.6,86); flags `["ch11_complete"]`; `OnActivated` → `CampaignFlagSetter.SetFlags` + `DreamReckoningTrigger.Acknowledge` (two persistent listeners) |
| `ArkshipCoreAmbience` | `BuildAmbienceLayer("ArkshipCoreAmbience", (0,2.4,42), innerRadius 5, outerRadius 16, maxVolume 0.4)` — **absolute world position, not dive-offset** (flagged gap) |
| `CradleDreadAmbience` | `BuildAmbienceLayer("CradleDreadAmbience", (0,1.6,57), innerRadius 4, outerRadius 14, maxVolume 0.4)` — same flagged gap |

**Note — `ChapterOutro`'s z-offsets (13 m/14 m past `thresholdPos`) place it and the completion canvas past `Threshold`'s built north edge (z=78, half-width 6 from center z=72).** See Beat 4c's candor note for the full accounting and the canon direction correction.

### A.6 Scene root hierarchy (current)

`BuildChapter11GhostsAndOrigins()` creates these as **siblings**, not nested: `Directional Light`, `BoneCanyon` (all canyon tiers/ramps/hull), five canyon accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig, `Dreamscape` (the three-room dive island, containing its own accent lights, hologram, ghost-manifestations, Aldric, the Dreaming Archive — plus Kira and Younger Self as **unparented scene-root siblings** despite reading as dreamscape content, per the flagged gap), `DreamscapeEntryPoint`, `DreamscapeExitPoint`, `DreamscapeDive`, `EnterDreamscapeTrigger`, `ExitDreamscapeTrigger`, `UnbrokenGranter`, `DreamReckoning`, four reach points, fourteen dialogue-player roots, the complete canvas, `ChapterOutro`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and five `[BEAT_N_LOGIC]` roots, moves `BoneCanyon`'s and `Dreamscape`'s art content into the former (with Kira/Younger Self correctly reparented under `Dreamscape` as part of the same pass that fixes the flagged gap), and leaves the ability/trigger/mission-logic objects under their respective `[BEAT_N_LOGIC]` roots.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Five resolve (the four Named-cast prefabs plus Echo); everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Named.AldricKnight1` | `Art/Generated/Characters3D/Named/Aldric_Knight-1.prefab` | **EXISTS** |
| `Named.KiraDusk` | `Art/Generated/Characters3D/Named/Kira-Dusk.prefab` | **EXISTS** |
| `Named.YoungerSelf` | `Art/Generated/Characters3D/Named/Ronin-7_Cipher_Soren.prefab` *(reused mesh — see Beat 2c casting note)* | **EXISTS** |
| `Named.TheDreamingArchive` | `Art/Generated/Characters3D/Named/The-Dreaming-Archive.prefab` | **EXISTS** |
| `Enemies.GhostManifestation` | *(no dedicated mesh — placeholder capsule via `InstantiateNpc`'s fallback path)* | MISSING |
| `Rooms.CanyonSpawnGround` | `Art/Generated/Rooms/CanyonSpawnGround.prefab` | MISSING |
| `Rooms.CanyonTier` | `Art/Generated/Rooms/CanyonTier.prefab` | MISSING |
| `Rooms.DreamscapeArenaShell` | `Art/Generated/Rooms/DreamscapeArenaShell.prefab` | MISSING |
| `Rooms.ArkshipCoreShell` | `Art/Generated/Rooms/ArkshipCoreShell.prefab` | MISSING |
| `Rooms.CradleShell` | `Art/Generated/Rooms/CradleShell.prefab` | MISSING |
| `Props.CanyonRamp` | `Art/Generated/Props/CanyonRamp.prefab` | MISSING |
| `Props.ArkshipHullFragment` | `Art/Generated/Props/ArkshipHullFragment.prefab` | MISSING |
| `Props.SkullDomeThreshold` | `Art/Generated/Props/SkullDomeThreshold.prefab` | MISSING *(proposed — not yet an actual registry key referenced by the builder; see §3 candor note)* |
| `Vfx.GhostManifestationDissolve` | `Art/Generated/VFX/GhostManifestationDissolve.prefab` | MISSING *(no fallback either — see Beat 2c note)* |
| `Vfx.CanyonDustMotes` | `Art/Generated/VFX/CanyonDustMotes.prefab` | MISSING *(no fallback — see §3 candor note, Beat 1c)* |
| `Vfx.RisingDustForms` | `Art/Generated/VFX/RisingDustForms.prefab` | MISSING *(no fallback — see §3 candor note, Beat 2c)* |

**Reuse notes.**

- `Rooms.CanyonTier` serves all four numbered/threshold platforms (Tier1/Tier2/Tier3/Threshold); lock the tint-override support in place or the depth-progression palette breaks (§Beat 1c).
- `Props.CanyonRamp` serves all four ramp connectors at four distinct lengths/angles from one prefab.
- `Named.YoungerSelf` is the one key in this table whose registry name deliberately does **not** match its resolved file's name — do not "fix" this mismatch by renaming the on-disk asset or the in-scene `displayName`; it is the mechanism that keeps Soren unnamed in Ch11 (§Beat 2c).
- Unlike Ch1's `Vfx.ConduitSpark`, `Vfx.GhostManifestationDissolve` has **no primitive fallback at all** in Appendix A — `DreamPhantom` simply deactivates its GameObject on death, with no particle/shader standing in for the missing prefab. Prioritize this over room-shell prefabs in any future art pass precisely because there is nothing today, not even a cheap primitive.
- `Vfx.CanyonDustMotes` and `Vfx.RisingDustForms` share that same zero-fallback status and the same priority case — dust is the chapter's signature medium (§3) and the literal substance the ghost-manifestations are made of, so these three VFX keys (plus `Vfx.GhostManifestationDissolve`) form one coherent commission, not three unrelated asks.
- `Props.ArkshipHullFragment` is a canon-specific brief, not a generic prop: per the SETTING block (script 27–30, §3), it should read as antique Program-metal *grown into* the leviathan's ribs and half-swallowed by dust, not a hull cleanly speared through the bone. Brief it alongside the dust VFX above rather than as an unrelated static prop — the hull is "half-swallowed by dust," the same medium the ghost-manifestations coalesce out of.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter11Builder.cs`, `Chapter11Lines.cs`, `ChapterSharedBuilders.cs`, `Scripts/World/Story/MemoryDiveController.cs`, `MemoryFlashbackController.cs`, `MemoryDiveEntryTrigger.cs`, `MemoryDiveExitTrigger.cs`, `Scripts/Enemies/DreamPhantom.cs`, `DreamReckoningTrigger.cs`, `Scripts/Player/UnbrokenWard.cs`, `Project/Docs/CHAPTER-BUILD-LEDGER.md`, `story ouput/Ch11_Ghosts_and_Origins.md`, `story ouput/Ch11_Ghosts_and_Origins_Dialogue_Script.md`, `story ouput/audit/Ch11_audit.md`.*

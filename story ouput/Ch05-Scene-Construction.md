# Chapter 5 — Scene Construction

*The architectural contract for `Ch05_DebtOfAshes.unity`: what Chapter 5 ("The Debt of Ashes") must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 5 from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter5Builder.cs`, entry point `XRRigBuilder.BuildChapter5DebtOfAshes()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 05 — The Debt of Ashes**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch05_The_Debt_of_Ashes.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`) and the audited, condensed line set in `Chapter5Lines.cs` (which is what actually ships as VO — see §7).
- **World scale is 1 unit = 1 meter.** Never break it. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** This chapter has no scripted combat feedback to carry (§4's per-beat "d. Combat" is `None` throughout — see §9), but any future juice pass (scavenger skirmishes, the dive's "limited combat" per the beat treatment) must route through `Haptics`/`AudioDirector`/`CombatFeedbackController`, never the camera.
- **Traversal in Ch5 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no parkour/climb/wall-run** anywhere in the real ash-world. **One narrow, deliberate exception:** `MemoryDiveController.TeleportRig()` performs an instant, non-player-invoked `SetPositionAndRotation` on the rig root at exactly two mission-Trigger moments (entering and exiting the memory-dive). This is a *scene transition*, not player-facing teleport locomotion — the player never presses a button to warp, there is no lerp, no camera motion, and `CharacterController` is toggled off/on around the write so it can't fight the direct position set. Do not confuse this with a locomotion mode; do not add a second one. **Open comfort question, not a settled fact:** that reasoning covers vection/sickness from the *position* jump, but `EnterDive()` (`MemoryDiveController.cs:53-71`) also activates `diveRoot` and applies the flashback fog/ambient treatment in the same frame as the teleport, and `TeleportRig()` (`:95-105`) is a bare `SetPositionAndRotation` with no screen fade — so the player experiences an instantaneous total visual-field swap (new geometry + new fog color/density + new ambient), not just a reposition. That is a stronger disorientation event than a same-room teleport, and the standard VR mitigation is a brief blink-to-black, which does not violate the no-camera-shake rule. **Weighed against that, on the mitigating side of the ledger:** both `DiveEntryPoint` (0,0,152) and `DiveExitPoint` (0,0,60) are built with `Quaternion.identity` (facing +Z), and the real-world approach the player walks before either teleport is also +Z — so heading is continuous (+Z → +Z → +Z, toward Vera throughout) across both teleports; there is no rotational vection component, only the translational jump plus the visual-field swap. That sharpens rather than closes the open question below: a blink, if added, would be guarding a visual-field swap only, not a reorientation — and it puts a fence around a future edit that casually rotates either anchor off +Z, which would introduce the more sickness-inducing rotational component this design currently avoids for free. Confirm whether a `ScreenFader` blink should wrap `EnterDive()`/`ExitDive()` before treating the current bare swap as comfort-safe; since `MemoryDiveController` is frozen shared infra (§1.2), any blink would have to be driven by the Trigger step, not added to the component.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | ground/terrain dressing, ruin props, grave props, ember lights, ghost-memory geometry *(the physical object)*, VFX | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC spawns, dive entry/exit trigger wiring, reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

Chapter 5 has **no doors**, so the "one object that spans both" idiom from other chapters (a door: art instantiates it, logic sets its lock state) has no equivalent object here. The nearest analog is the **memory-dive geometry island**: `BuildBeatNArt()` instantiates `MemoryDive_Massacre` and everything under it (inert, `SetActive(false)`); `BuildBeatNLogic()` owns the `MemoryDiveController`, the `EnterDiveTrigger`/`ExitDiveTrigger` objects, and the mission-spine `Trigger` steps that flip them on. Art builds the recording; logic decides when the player enters it.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildProp`, `BuildAccentPointLight`, `BuildAmbienceLayer`, `BuildDialoguePlayer`, `Author*Step` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, plus the chapter-agnostic runtime components this chapter reuses from Ch3's precedent — `MemoryDiveController`, `MemoryFlashbackController`, `MemoryDiveEntryTrigger`, `MemoryDiveExitTrigger` (all in `Ronin7.World.Story`, not editor code, but shared across any future chapter's memory-space beat exactly like a `ChapterSharedBuilders` helper is).
- **Free to restructure:** the Ch5-local helpers, called only from `BuildChapter5DebtOfAshes()` — `Ch5BuildSettlementRuins`, `Ch5BuildMassGrave`, `Ch5BuildMemoryDive`, `Ch5BuildPlaybackLight`, `Ch5BuildGhostFigure`, `Ch5BuildCompleteCanvas`, `Ch5PlaceStoryNpc`, `Ch5BuildDialogue`, `Ch5WireVoiceClips`.

This refactor lives entirely in the second list. **As of this writing `Chapter5Builder.cs` is fully monolithic** — one 250-line method builds lighting, terrain, every beat's props, every NPC, every dialogue player, and all 19 mission-spine steps in a single top-to-bottom pass with no `BuildBeatNArt`/`BuildBeatNLogic` split at all (see Appendix A). That split is this document's target state, exactly as it was for Chapter 1 at the time its own scene-construction doc was written — this is not a Ch5-specific regression, it is the same starting point every chapter builder begins the refactor from.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** The same two ScriptableObjects defined for Chapter 1 carry everything this builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch5Environment.asset` | directional key (overcast grey-white), ambient mode + color, fog mode/color/density (exponential, ash-pall), ground tint, per-zone ember accent lights, the dive's `MemoryFlashbackController` fog/ambient override values |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared registry asset across all 14 chapters) |

Neither exists yet — same as every other chapter's status quo (§1.5).

Prefab **paths never appear in builder code.** The builder asks the registry for `Props.CollapsedWallShell`; the registry asset holds the path. **Prefab root is `Assets/Ronin7/Art/Generated/`.** New environment folders for this chapter are siblings of the already-populated `Characters3D/`:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Kessler/Resh/Mira/Echo/Kira-Dusk/Vera-Dusk all present)
  Rooms/                                    ← new, not needed by Ch5 (exterior chapter, no room shells)
  Props/                                    ← new
  Terrain/                                  ← new (this chapter's addition — an open exterior ground plane
                                               is a different asset class than the "room shell" prefabs
                                               Ch1–Ch4 register under Rooms/)
  Vfx/                                      ← new
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

`BuildChapter5DebtOfAshes()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter5Builder.cs:67`) — the exact same discard-the-whole-scene strategy flagged as broken for Chapter 1's safe zone. The same fix applies unchanged: replace the wipe with `EditorSceneManager.OpenScene(Ch5ScenePath)` + `DestroyImmediate` on each generated root by name (`AshWorld`, `MemoryDive_Massacre`, `Game`, `Mission`, the rig, the landing-party NPCs, the dialogue players, the reach points, `MemoryDive`/`EnterDiveTrigger`/`ExitDiveTrigger`), falling back to `NewScene` only when the scene file doesn't exist yet. Reuse the `EnemyArtWirer.cs` / `CrowdArtWirer.cs` open-in-place pattern rather than inventing a third strategy.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for this chapter** — no ground plane, no collapsed wall shell, no grave marker, no cairn, no ID-chit prop, no ghost-figure mesh. `Assets/Ronin7/Art/Generated/{Rooms,Props,Terrain,Vfx}/` don't exist on disk. See Appendix B for the full key inventory: every environment key is a commission; only the **named character prefabs** (Kessler, Resh, Mira, Echo's katana, Kira Dusk, Vera Dusk) resolve, because they were built by a separate, already-shipped pipeline (Tripo image→3D + `PlaceholderCharacterBuilder`).

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently. Same guard idiom as every other chapter:

```csharp
var prefab = registry.Resolve(ArtKey.Props_CollapsedWallShell);
if (prefab == null) {
    Debug.LogWarning($"[Ch5] {ArtKey.Props_CollapsedWallShell} unresolved — primitive fallback.");
    BuildCollapsedWallShellPrimitive(staticArtRoot, pos);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This chapter must remain playable at every commit during the art migration.

**One deliberate non-candidate for the registry:** the memory-dive's ghost figures (`Ch5BuildGhostFigure`) and the villager/younger-Ronin/squad silhouettes are stylistically *load-bearing* primitives, not placeholders awaiting art — `MemoryFlashbackController.MakeGhostMaterial()` is a translucent, desaturated unlit material standing in for "a recording, not a place," and a naturalistic prefab mesh under that material would still read correctly. Unlike the exam table or the airlock door in Chapter 1, these are not expected to graduate to fully-authored meshes; if they ever do, they must keep the ghost-material treatment. Flag any future PR that gives the ghosts opaque, fully-lit materials as a tone regression, not a completion.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame); `QualityBootstrap`'s shipped default of 72 Hz is the floor actually being shipped against today (same standing note as every chapter).
- **No recorded greybox baseline exists for Ch5 specifically** in `Project/Docs/CHAPTER-BUILD-LEDGER.md` — the ledger's Ch5 rows cover EditMode test-count deltas (Ep10–11 deletion, +7 fixtures → 457 tests) and legacy Ep-builder cleanup, not `UnityStats` draw-call/tris numbers. **This is a gap to close, not a number to invent:** capture `drawCalls`/`setPassCalls`/`tris`/`verts` for `Ch05_DebtOfAshes.unity` at the next editor session and record it here and in the ledger before starting the art migration, exactly as Ch1's §1.6 baseline was captured on 2026-07-02.
- The chapter is comparatively cheap today: one continuous tinted ground-plane cube, ~14 tinted cube/cylinder props (wall shells, stalls, scavenger silhouettes, grave markers, cairn, ID-chit), 6 named-character prefab instances, 6 ember point lights (shadowless), 2 `ProximityAmbienceLayer` ambience beds, and — while the dive is inactive — zero additional draw calls from the ~20 ghost-figure primitives and dive props, since the whole `MemoryDive_Massacre` subtree starts `SetActive(false)`. **The dive doubles the chapter's prop count the instant it activates** (step 7); that is the moment most likely to regress the frame budget once real art lands, and the moment worth profiling first.
- **The VO manifest's ≈494 s (~8.2 min) (§7) is summed clip length, not chapter playtime** — Y-advance pacing and traversal make actual playthrough materially longer; don't feed the raw figure into a pacing or profiling estimate.

## 2. Chapter spatial map

Chapter 5 is **one continuous exterior scene**, `Assets/Ronin7/Scenes/Ch05_DebtOfAshes.unity`, built on the same **open ground-plane, no-walls-no-ceiling pattern as the Ch4 Deepworks exterior** — there are no rooms and no doors. The real ash-world runs along a single +Z line from the landing site to the mass grave; the memory-dive is a second, structurally identical but spatially disconnected line, at absolute **z[150,200]**, ~55 m past the real-world run's end (z≈95), so it can never overlap the real world, reachable only through `MemoryDiveController`'s instant teleport (§1.1), never by walking.

```
 -Z                                                          THE REAL ASH-WORLD                                        +Z
 Landing Site ──ReachTrigger(z=20,r=5)── Settlement Ruins ──ReachTrigger(z=58,r=5)── Mass Grave (Vera, Cairn, Kira's ID-chit)
   z[0,16]                                  z[16,50]                                   z[50,90]
   Kessler/Resh/Mira/katana spawn            6 CollapsedWallShell, 3 BurnedStall,        12 GraveMarker (3x4 grid, z 54-62),
   z=3-4.5; LandingEmber0/1                  2 Scavenger silhouettes; RuinsEmber0/1       Cairn+IDChit z=64, Vera Dusk z=66,
                                                                                           GraveEmber0/1; ChapterOutro z=68

                              ▲ MemoryDiveController.TeleportRig — instant, not walked ▲
                              (Trigger step 7 in / step 13 out; DiveExitPoint = z=60, back at the grave)
                                                          │
 ─────────────────────────────────────────────────────── ┼ ──────────────────────────────────────────────────────────
                                                          ▼
                                       THE MEMORY-DIVE ISLAND (offset, inactive until Trigger)
                              DiveEntryPoint z=152 → the lane, villagers, Ghost_YoungerRonin/Squad z=158-182
                                        → the well, Ghost_Kira z=186-188
                                        z[150,200], x[-9,9]  ("MemorySquare_Ground")
```

| Beat / Zone | Approx. bounds | What's there |
|---|---|---|
| Beat 0 — Landing Site | x[-20,20], z[0,16] | player spawn, katana, Kessler/Resh/Mira, `LandingEmber0/1` |
| Beat 1 — Settlement Ruins | x[-20,20], z[16,50] | 6 `CollapsedWallShell`, 3 `BurnedStall`, 2 `Scavenger_Silhouette`, `RuinsEmber0/1` |
| Beat 1 (cont.) / Beat 3–4 — Mass Grave | x[-20,20], z[50,90] | 12 `GraveMarker`, `Cairn`, `IDChit_Kira` + label, Vera Dusk, `GraveEmber0/1`, `ChapterOutro`, complete canvas |
| Beat 2 — Memory-Dive Island | x[-9,9], z[150,200] | `MemorySquare_Ground`, 3 `MemoryStall`, `MemoryWell`, 4 villager ghosts, `Ghost_YoungerRonin`, 2 squad ghosts, `Ghost_Kira` (real mesh, ghost-tinted), 3 playback lights |

`AshGround` — the single shared terrain cube every zone above sits on — is centered at (0, -0.5, 45) with scale (40, 1, 100), so its top face sits exactly at **y=0** across the whole real-world run (z[-5,95]); this is the chapter's Y-invariant (§5), the equivalent of Ch1's `kesslerFloorY`. The dive's `MemorySquare_Ground` is a second, separate cube at (0,-0.1,175) scale (18,0.2,50), top face also at **y=0** — the dive's floor matches the real world's floor height exactly, so the rig teleport at entry/exit never has to reconcile two different ground heights.

**No doors exist in this chapter.** The only gating mechanisms are four `ReachTrigger` mission steps (radius 5 m each: the Settlement, the Mass Grave, the dive's Lane, and the dive's Nearing-the-Girl point) and the two dive Trigger steps (`EnterDiveTrigger`/`ExitDiveTrigger`) — none of them lock geometry; they gate the mission-spine's advance, and the player is always physically free to walk anywhere within `ZoneBounds`.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence`. `Chapter5Builder.cs` sets no explicit spawn-position override on the rig, so it defaults to the XR Origin's own scene-origin transform — placing the player at the landing site alongside Kessler/Resh/Mira (spawn cluster z=3–4.5), consistent with the katana resting nearby at (2,1,1). `ZoneBounds` is **center (0, 0, 100), radius 115** — one bounding sphere loosely enclosing both the real ash-world (z up to ~95) *and* the dive island (z up to 200) on a single line, since the two are never simultaneously reachable.

## 3. Global environment & backdrop

**The ash-world is a single overcast exterior**, not a lit interior — the whole chapter (landing, ruins, grave) shares one directional light, one ambient color, and one fog bed, set once at the top of the builder rather than per-room the way Chapter 1's four enclosed compartments each got their own accent light regime. Per the dialogue script's SETTING block: *"a dead settlement on a grey planet scoured by his old operative line, where the soil is ash to the ankle and the wind carries it in slow grey curtains... The tone is elegiac — ash, weight, the quiet of a place where everything loud already happened."* There is no syndicate, no garrison, no bounty here; per the beat treatment's Game Narrative Design section, **the antagonist is grief**, and the level's job is to read as a wound, not a level — traversal and horror-adjacent stillness over spectacle.

**Recurring canon elements carried through the whole run** (per the dialogue script's INTRUDING/RECURRING ELEMENTS block):

- **The ash** — the planet's scoured surface as constant weather and accusation, "not buried here so much as spread thin over everything the crew touches," canon repeatedly framing it underfoot: "soil is ash to the ankle," "ankle-deep over everything," Beat 4's closing image of Ronin-7 walking "back… through the ankle-deep ash." **No drifting-ash particle system exists in the engine today** — `VfxPrefabBuilder` only generates combat-hit VFX, and no reusable "ash drift" helper exists in `ChapterSharedBuilders.cs` or `Editor/Art`. The builder's own doc comment explicitly notes this was *skipped rather than adding new particle infrastructure for one chapter.* Nor is there any footstep audio layer for the ash surface itself (§7) — the chapter's title image is stated in dialogue but not yet sensed underfoot, in the air, or on approach. This is the single biggest gap between the chapter's canon atmosphere and its current build — flagged again in §9.
- **The ledger of the dead** — Vera's record of the lost; this chapter plants the motif (full mechanic Ch10). No UI/inventory object backs it today, and canon makes it physical twice over: Beat 1's SETTING block describes Vera holding "a worn ledger in one hand and a drawn weapon in the other," and Beat 4 has her "lay the ledger on the cairn… both hands, like setting down a stone." Neither the ledger nor the weapon is a prop anywhere in the build today (§4 Beat 1c/4c, §9) — the chapter's climactic gesture currently has nothing on screen to gesture with.
- **Scavengers** — the only live "threat" on the ash-world, explicitly texture, not enemy. Represented by two static, componentless `Scavenger_Silhouette` props (§4, Beat 1) that scatter *in the fiction* (per the Beat 1 traversal barks) but never move or fight in the sim. Canon has them "scatter at the sight of armed bodies and [are] gone" — a distant, glimpsed-then-vanished impression the current placement undercuts (see §4 Beat 1c staging note): both silhouettes sit at |x|=7, close beside the player's own path, and never move, reading as static mannequins the player walks past rather than distant figures who plausibly fled.
- **Echo (the katana's shadow-AI)** — heard by Ronin-7 alone via `EchoPresence`, present in every beat including inside the dive; the wry Ch4 warmth deliberately drops away here (see §4's per-beat dialogue notes).

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter5Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch5Environment.asset`. Its schema mirrors Chapter 1's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `groundTint` | `Color` | `AshGround` and `MemorySquare_Ground` |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | the six ember lights + three playback lights |
| `diveFogColor`, `diveFogDensity`, `diveAmbientColor` | `Color`, `float`, `Color` | `MemoryFlashbackController`'s serialized fields (currently hardcoded on the component, not read from the chapter profile — see note below) |

Current literal values (Appendix A) must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass:

| Field | Value |
|---|---|
| Key light color | (0.75, 0.74, 0.72), intensity 0.6, rotation Euler(60, -40, 0) |
| Ambient mode / color | Flat / (0.16, 0.15, 0.15) |
| Fog mode / color / density | Exponential / (0.5, 0.48, 0.46) / 0.042 |
| Ground tint | (0.12, 0.11, 0.11) — `AshGround`'s own tint (`MemorySquare_Ground` uses a separate grey, (0.26, 0.27, 0.28), since it's a different asset entirely, not a re-skin) |

**Note on the dive's fog/ambient override:** `MemoryFlashbackController` (a runtime `Ronin7.World.Story` component, not editor code) carries its own serialized defaults — fog color (0.35, 0.37, 0.42), fog density 0.045 (`ExponentialSquared`), ambient color (0.30, 0.30, 0.34) — applied via `ApplyTreatment()` on `Awake()` and re-applied by `MemoryDiveController.EnterDive()` every time the dive activates, then restored to the pre-dive snapshot on `ExitDive()`. Because this component is frozen shared infrastructure (§1.2), the profile-driven refactor should treat these three fields as **profile-eligible but out of `Chapter5Builder.cs`'s direct control** — the correct data-driven fix is a `ChapterEnvironmentProfile.diveTreatment` sub-block that the builder writes into the `MemoryFlashbackController`'s serialized fields at scene-build time, not a change to the component itself.

**Material / tint palette:** every prop is a cheap primitive tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) — same cost-control rationale as every other chapter (§1.6). Prefabs replacing them must carry their own materials and will not batch this way; every prefab landing in the registry must be re-measured against whatever baseline gets captured per §1.6.

## 4. Per-beat scene spec

The chapter plays as five beats along the ash-world's +Z run (four real-world beats, one dive-island beat spatially nested between Beat 1 and Beat 3). Each beat is documented with the same a–f structure. Beat numbering matches `Chapter5Lines.cs`'s `SetIds` and the dialogue script's `BEAT 0–4` headers exactly.

**Table conventions, everywhere below:** identical to Ch1 — art tables carry Position/Rotation, a Registry Key, the path it resolves to, and a Status; no `scale()`/`size()`/`PrimitiveType` (native prefab scale only); **Status `MISSING`** means the primitive fallback (§1.5) is active for that row.

**Indexing note:** the mission-spine "Step" numbers below (1–19, matching the beat treatment's own numbering) are 1-based prose labels, not the serialized array index — `MissionDirector`'s `steps` array is zero-indexed (`arraySize = 19`, `n` from 0), so doc "Step 7" (Trigger: Enter the Memory-Dive) is `steps.GetArrayElementAtIndex(6)`, not `(7)`. Subtract one when mapping a doc step number onto the actual array.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Do not hardcode the directional light, fog, or ember-light values inline. Read them from `ChapterEnvironmentProfile`. Create **`BuildBeat0Art()`** (the ash-sky lighting rig, `LandingEmber0/1`, the landing-site slice of `AshGround`) and **`BuildBeat0Logic()`** (the landing-party spawn, the katana, the briefing dialogue, the mission step). Note that today the sky/fog/ground setup is chapter-global, not beat-scoped — assign it to Beat 0's Art method since it establishes the mood the instant the scene loads, and have every later beat simply build within the already-lit world rather than re-lighting it.

#### a. Narrative purpose & emotional target

Chapter 4 was a descent into noise that ended in a chosen mercy; Chapter 5 opens Act II by turning that empowerment around to face its bill. This beat plays **aboard the Cairn**, not the ash-world — the only interior-feeling beat in the chapter, though it is built as a plain exterior spawn point rather than a distinct command-room set (see Appendix A: no ship-interior geometry is built for this beat at all; see the CREW-PRESENCE DECISION note in the builder's class doc). The scene's job is to make the crew *choose* the job with open eyes: Mera Voss (newest aboard, still cold) reads the old kill-record fluently and warns them what they'll find; Iris flags the operative-of-record name that "won't resolve" — the seed of the whole chapter's reveal, planted here and paid off in Beat 3; Resh asks the practical question nobody wants to ("You sure this is the one we pick?"); Kessler, twice a father now, draws the line around Mira ("This one isn't for small eyes"). Ronin-7's own answer is the beat's emotional hinge: an honest "No. I don't want this one," immediately followed by "Set the course anyway" — an operative habit bent toward something harder than a mission, owning it rather than avoiding it. Echo closes the beat by naming the whole chapter's design intent to Cipher alone: *"This isn't a place you save. It's a place you answer for."*

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

- **Landing-party spawn:** Kessler (-1.5, 0, 3), Resh (1.5, 0, 3), Mira (0, 0, 4.5) — all `StoryNpc` via `Ch5PlaceStoryNpc`, `FitNamedCharacter`-grounded, each carrying a small `StoryNpcWander` idle (Kessler/Resh radius 0.6 m, Mira 0.5 m). No walker, no travel leg — see §5, this chapter's NPC movement model is fundamentally different from earlier chapters. **Facing is unset here too**, same root cause as Vera Dusk (§4 Beat 1b): `Ch5PlaceStoryNpc`/`InstantiateNpc` never write rotation, so all three spawn at prefab-default +Z, partly facing away from a player who spawns at ~z=0 looking toward them (§2). Lower stakes than Vera's confrontation since the trio wanders in place, which partially masks a fixed facing, but a briefing staged around a shared focus reads better if Kessler/Resh/Mira face −Z / inward toward the player from the start.
- **Blocking contradiction: Mira spawns in front of Kessler, not shielded behind him.** Canon is explicit and repeated — Mira "shielded behind him," "kept close at Kessler's side," "This one isn't for small eyes" (§4 Beat 0a/0e) — but Kessler/Resh spawn at z=3 and Mira spawns at **z=4.5** (`Chapter5Builder.cs:141-143`), i.e. Mira is the most +Z (grave-ward) member of the trio, ahead of both adults relative to the player (spawning ~z=0, §2) and to whatever danger lies north. For Mira to read as "shielded," her z should be *less* than Kessler's — e.g. (-1.5, 0, 2), tucked behind him rather than in front — not greater. As built, the child is placed at the front of the landing party, not the back.
- **Katana:** `BuildSword` at (2, 1, 1), Euler(-90, 0, 0), visual prefab `Echo.prefab` — the chapter opens **already armed**, no rack-wake beat; per the builder's own comment this matches "the cost, not initiation, tone of Act II opening." **Interaction expectation, for clarity:** the doc does not otherwise state whether grabbing the katana is required before the dive or purely decorative at spawn. Canon's dive trigger is narrated as Ronin-7 "closing a hand around the katana's wrap" — for a VR chapter whose emotional pivot is literally holding the blade, the intended reading is that the player is expected to physically grab it at some point before step 7's dive Trigger, not that it's an inert prop the mission-spine advances around regardless. The builder does not currently gate step 7 on a grab — the dive Trigger fires on completion of step 6 (`ch5_beat1_kira_named`, Vera's "Tell me, then"), *not* on `DiveNearGirlReachPoint` (that reach point is a later, dive-internal gate on step 12's hesitation, §4 Beat 2b) and not from holding the sword — so today it is functionally decorative at spawn even though the fiction assumes it's held. **Open question, not a hard fix:** the chapter's central gesture could become the mechanic — gate step 7's Trigger on a grab of the `Echo` katana (a grab-gated trigger ahead of `EnterDive()`'s activation) so the player physically closes a hand on the wrap to enter the recording, mirroring canon's "closes a hand around the katana's wrap and goes still." Flagged for the narrative owner alongside the other agency questions (control-lock, combat scope, §9), not prescribed here.
- **Iris and Mera Voss are voice-only** for this entire chapter — a deliberate `CREW-PRESENCE DECISION` documented in the builder's class comment: canon splits the landing party (Kessler/Resh/Mira go down; Iris/Mera stay aboard the Cairn on an open comm-line), and rather than build a second ship-interior scene for a two-line presence, both are `DialoguePlayer` speaker labels with no physical `StoryNpc` anywhere in the scene — the same "no body in the scene" convention `Echo` already uses chapter-wide.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 3), set `ch5_beat0_briefing`.

**Mission-spine steps (this beat):**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 1 | Dialogue | "Beat0: The Cairn (the briefing)" | plays `ch5_beat0_briefing` (11 lines) in full, advanced line-by-line on Left-Hand Talk (Y) |

**What changes during the beat:** nothing in the set dressing — the landing party stands still, Mira wanders in her tiny radius, the katana sits unclaimed. The only state change is the mission-spine advancing to step 2 once all 11 lines have played.

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Directional key light | Euler(60, -40, 0) | — | `ChapterEnvironmentProfile.keyLightColor/Intensity/Rotation` | profile |
| `AshGround` (shared terrain) | center (0, -0.5, 45), scale (40,1,100) | `Terrain.AshGround` | `…/Art/Generated/Terrain/AshGround.prefab` | **MISSING** |
| `LandingEmber0` | (-4, 1.6, 6) | — | `ChapterEnvironmentProfile.accentLights["LandingEmber0"]` | profile |
| `LandingEmber1` | (4, 1.6, 10) | — | `ChapterEnvironmentProfile.accentLights["LandingEmber1"]` | profile |
| `Props.LandingShuttle` | not yet placed — proposed ~(0, 0, -6), ramp facing +Z (toward the landing party) | `Props.LandingShuttle` | `…/Art/Generated/Props/LandingShuttle.prefab` | **MISSING — not yet in the registry, no instance built** |
| Kessler | (-1.5, 0, 3) | `Named.Kessler` | `…/Characters3D/Named/Kessler.prefab` | **EXISTS** |
| Resh | (1.5, 0, 3) | `Named.Resh` | `…/Characters3D/Named/Resh.prefab` | **EXISTS** |
| Mira | (0, 0, 4.5) | `Named.Mira` | `…/Characters3D/Named/Mira.prefab` | **EXISTS** |
| Katana "Echo" | (2, 1, 1), Euler(-90,0,0) | `Named.Echo` | `…/Characters3D/Named/Echo.prefab` | **EXISTS** |

**Notes on the transition.** `AshGround` today is one tinted `PrimitiveType.Cube` spanning the *entire* real ash-world run (z[-5,95]), not a Beat-0-local prop — assign its instantiation to Beat 0's Art method since it's the first thing the player sees, but treat it as chapter-shared geometry, not something re-built per beat. Kessler/Resh/Mira/the katana are the only fully-resolved art in this beat; there is no ship-interior set at all (see §9 for the open question this raises about whether Segment 0's "command room, more of its dead bridge lit" staging from the dialogue script's SETTING block is meant to be a distinct built space).

**No shuttle/dropship prop exists at the landing site, though both canon and this doc's own §5 treat one as present.** Canon opens Beat 1 and closes Beat 4 on the same concrete image — "The shuttle's ramp lowers" — and §5's Beat 3–4 blocking-gap note refers to Resh's mis-anchored line as playing "at the abandoned shuttle," as if a shuttle exists at the landing site. It doesn't: `Chapter5Builder.cs` builds no dropship anywhere, and the landing cluster (Kessler/Resh/Mira at z=3–4.5, `LandingEmber0/1`) sits on bare `AshGround` with nothing behind it to read as "we came down here." `Props.LandingShuttle` is added to the table above and to Appendix B as a **MISSING** key — a static grounded dropship, ramp down, placed behind the spawn cluster (proposed ~(0,0,-6), so it doesn't block the +Z sightline toward the ruins) — both the one hero prop that makes the landing site legible as a landing site and the concrete referent the "ramp lowers" opening image needs.

#### d. Combat

None. No `Enemy` component exists anywhere in this beat (or this chapter — see §9).

#### e. Dialogue / VO

Set `ch5_beat0_briefing`, anchor (0, 1, 3), 11 lines, ≈93 s (summed from `Chapter5Lines.cs`):

| Speaker | Line | sec |
|---|---|---|
| Mera Voss | I've read a hundred of these. Pacification logs. This one carries your old line's signature. In and out clean. A settlement scoured, and somebody rounded the count off after. | 14 |
| Iris | The operative-of-record is named Kael Vor. I've run it against everything we have and the name won't resolve. No file, no line, nothing under it. It's a wall. | 11 |
| Resh | So let me say the part everyone's circling. We'd be flying to a graveyard to read his name off the kill-record that put people in it. That's not a rescue. You sure this is the one we pick? | 11 |
| Kessler | We've buried a lot to get this crew breathing. Now you want me to fly us to a field of graves. Tell me what's worth the climb back out. | 9 |
| Mera Voss | The truth's at the bottom. You don't have to like digging for it. It's the work. | 7 |
| Mira | Is it the bad place? | 2 |
| Kessler | It's a sad place, little one. Not a loud one. You stay close to me the whole time. This one isn't for small eyes. | 8 |
| Echo | I'll say the quiet part, since nobody at that table will. You don't save this place, Cipher. You answer for it. The records have your hands in them. I was there for it, and I'll be with you in it again. | 14 |
| Ronin-7 | No. I don't want this one. | 2 |
| Ronin-7 | Set the course anyway. If my name was buried somewhere, it was buried with them. | 6 |
| Kessler | We go careful. Quiet. And nothing of ours stays down there. Iris, plot it, then you and Mera stay on the Cairn and keep tearing into those records. | 10 |

This is the **audited/thinned** line set that actually ships (per `Chapter5Lines.cs`'s header comment: `story ouput/audit/Ch05_audit.md` found zero consistency errors and flagged nine "not X, it's Y" antithesis lines plus aphorism-stacking; the shipped lines above already have those fixes applied — e.g. Mera's third line drops a third stacked maxim, Kessler's closing line flattens a tricolon). The fuller, unaudited version of this same scene lives in `Ch05_The_Debt_of_Ashes_Dialogue_Script.md`'s BEAT 0 section for narrative-color/staging reference only.

**The audit trim also dropped the comm callback Beat 3 pays off.** Kessler's closing line above ends on "…keep tearing into those records"; canon's fuller version (dialogue script line 154) continues "The second they come apart, you put it on my comm" — the explicit verbal plant for Iris/Mera's reveal breaking in over the comm mid-Beat 3 (§4 Beat 3f). With that clause thinned out, the dialogue no longer verbally sets up the callback, which raises the value of the audible comm-crackle onset §4 Beat 3f already flags as missing (§7) — that SFX cue is now doing the setup work alone, not reinforcing a spoken plant.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics scripted — this is the chapter's establishing dialogue beat.
- Ambient bed: `AshWorldGardenAmbience` (a `ProximityAmbienceLayer`, inner 6 m / outer 30 m / max volume 0.4) is centered at (0, 1.5, 40) — its inner radius doesn't yet reach the Beat 0 landing cluster at z=3–4.5, so the briefing plays under comparatively quiet ambience, thickening as the player walks north into the ruins. This is a **positional gap worth flagging**: per the SETTING block the wind/ash-drift should be audible from the moment the ramp lowers, and today it is not (see §9).
- Comfort vignette is inert here — the player has not yet moved.

---

### Beat 1 — The Settlement (The Approach & The Accusation)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read every prop (wall shells, burned stalls, scavenger silhouettes, grave markers, cairn, ID-chit) from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (the ruins + the grave's physical dressing) and **`BuildBeat1Logic()`** (the two `ReachTrigger` steps, Vera Dusk's spawn, the three dialogue sets). Vera Dusk's `StoryNpc` placement is *logic* (mission-relevant spawn); the cairn/grave markers she stands beside are *art*.

#### a. Narrative purpose & emotional target

This is the chapter's title beat made literal: the crew walks the ash-world's silence into an accounting. Per the beat treatment, this is *"the first mission the crew chooses with open eyes that it will cost them"* paid off — the traversal from the landing site through the scoured settlement (collapsed prefab shells, a market square gone to ash, doors standing open on rooms no one came back to) is unscripted, elegiac, horror-adjacent, and explicitly **not combat**: the two scavenger silhouettes are texture, not enemies, and scatter in the fiction alone. Echo's ambient barks ("Doors all open. Nobody left to close them.") are the only voice in the player's ear during the walk. Reaching the grave hands control to the scripted confrontation: **Vera Dusk**, ash in her hair, ledger in one hand, drawn weapon in the other, has been waiting at Kira's grave "since before we landed." She refuses comfort ("Don't say you're sorry, and don't say you're here to help") and names the crime in the Program's own sanitizing language — *"You logged this as pacification"* — before naming her sister: *Kira. Fourteen. She kept lists of the ships that came through, because she liked the names.* Ronin-7 refuses the cheap mercy of a denial (*"I won't lie to you. I don't remember her... I won't dress that up as anything but what it is"*) and offers the only thing he actually has: the katana's memory. The beat ends on Vera's dread-tinged demand for the truth and Ronin-7's hand closing on the wrap — the hand-off into Beat 2.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

- **`SettlementReachPoint`** (0, 1, 20), radius 5 — gates step 2; the player must walk from the landing site into the ruins before the Approach dialogue plays.
- **`GraveReachPoint`** (0, 1, 58), radius 5 — gates step 4; the player must reach the mass grave before the Accusation dialogue plays.
- **Vera Dusk spawn:** (0, 0, 66), `StoryNpc` via `Ch5PlaceStoryNpc`, `wanderRadius: 0` — **fixed in place, no wander**, the same "waiting at the destination from the start" convention used for Tessa Rin in Ch4. She stands just past the cairn (z=64), at the head of the grave. **Build bug, not fact:** the doc used to assert she "faces back down the approach," but `Ch5PlaceStoryNpc` calls `InstantiateNpc(prefab, position, name)` (`Chapter5Builder.cs:147`, `ChapterSharedBuilders.cs:648`), which sets only `transform.position` and never touches rotation — Vera spawns at her prefab's default forward (+Z), i.e. facing *away* from a player arriving from −Z. `StoryNpc` has no look-at/facing behavior of its own (confirmed: no rotation logic anywhere in `StoryNpc.cs`), and `wanderRadius: 0` means nothing ever reorients her afterward. For the chapter's central face-to-face beat — ledger in one hand, weapon aimed at the man before her — this is the single most important sightline in the level and it is currently wrong. **Fix:** add a rotation parameter to `Ch5PlaceStoryNpc`'s Vera Dusk call (or a one-line `transform.rotation = Quaternion.Euler(0, 180, 0)` after instantiation) so she faces −Z, toward the approach, at build time. Until that lands, treat "Vera faces the approach" as a build gap, not a shipped fact (tracked again in §9).
- **Dialogue anchors:** `Dialogue_Beat1_Approach` (0, 1, 24) — inside the ruins, plays over the tail of the walk; `Dialogue_Beat1_Accusation` (0, 1, 60) and `Dialogue_Beat1_KiraNamed` (0, 1, 62) — both at the grave.

**Mission-spine steps (this beat):**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 2 | ReachTrigger | "ReachTrigger: The Settlement" | player enters `SettlementReachPoint` (0,1,20), r=5 |
| 3 | Dialogue | "Beat1: The Approach (Echo flags Vera)" | plays `ch5_beat1_approach` (2 lines) |
| 4 | ReachTrigger | "ReachTrigger: The Mass Grave" | player enters `GraveReachPoint` (0,1,58), r=5 |
| 5 | Dialogue | "Beat1: The Accusation" | plays `ch5_beat1_accusation` (5 lines) |
| 6 | Dialogue | "Beat1: Kira Named (the sword remembers)" | plays `ch5_beat1_kira_named` (5 lines) — ends on Vera's "Tell me, then," handing off to Beat 2's Trigger |

**What changes during the beat:** the settlement/grave geometry is entirely static throughout (no prop appears/disappears); the only state changes are the mission-spine advancing through five steps and Vera's presence being revealed to the player as they cross into her reach radius (she was always active, simply distant). **The reveal mechanism is the exponential fog, not a spawn or activation event, and it's precisely tuned, not incidentally so:** at `SettlementReachPoint` (z=20) Vera is 46 m off; at density 0.042, `exp(-0.042×46) ≈ 0.145` — roughly 85% fogged, effectively invisible against the grey backdrop. By `GraveReachPoint` (z=58) she's 8 m off; `exp(-0.042×8) ≈ 0.71` — clearly readable. That is canon's "steps out of the ash" reveal (§3), but it is an emergent property of a single global fog-density value shared by the whole real-world run, not a protected, documented one. Any future retune of `Ch5Environment.asset`'s fog density must re-check both endpoints — too low and Vera is visible from the landing site; too high and she's still fogged out at the grave — the same way Ch1 protects its viewport reveal distance.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `CollapsedWallShell` ×6 | (-6,0.6,18) (6,0.5,22) (-5,0.8,30) (5,0.4,34) (-6,0.5,42) (6,0.7,46) | `Props.CollapsedWallShell` | `…/Art/Generated/Props/CollapsedWallShell.prefab` | **MISSING** |
| `BurnedStall0` | (-3, 0.3, 26) | `Props.BurnedStall` | `…/Art/Generated/Props/BurnedStall.prefab` | **MISSING** |
| `BurnedStall1` | (3, 0.3, 28) | `Props.BurnedStall` | `…/Art/Generated/Props/BurnedStall.prefab` | **MISSING** |
| `BurnedStall2` | (-2.5, 0.25, 38) | `Props.BurnedStall` | `…/Art/Generated/Props/BurnedStall.prefab` | **MISSING** |
| `Scavenger_Silhouette0` | (-7, 0.9, 20) | `Props.ScavengerSilhouette` | `…/Art/Generated/Props/ScavengerSilhouette.prefab` | **MISSING** |
| `Scavenger_Silhouette1` | (7, 0.9, 44) | `Props.ScavengerSilhouette` | `…/Art/Generated/Props/ScavengerSilhouette.prefab` | **MISSING** |
| `Props.OpenDoorframe` | none placed today — no positions assigned | `Props.OpenDoorframe` | `…/Art/Generated/Props/OpenDoorframe.prefab` | **MISSING — not yet in the registry, no instances built** |
| `RuinsEmber0` | (-5, 1.8, 26) | — | `ChapterEnvironmentProfile.accentLights["RuinsEmber0"]` | profile |
| `RuinsEmber1` | (5, 1.8, 36) | — | `ChapterEnvironmentProfile.accentLights["RuinsEmber1"]` | profile |
| `Props.GraveScar` | not yet placed — proposed long trench/decal under the marker grid, z≈52–64 | `Props.GraveScar` | `…/Art/Generated/Props/GraveScar.prefab` | **MISSING — not yet in the registry, no instance built** |
| `GraveMarker_{row}_{col}` ×12 | 3×4 grid, x = -4.5+col·3, z = 54+row·4 | `Props.GraveMarker` | `…/Art/Generated/Props/GraveMarker.prefab` | **MISSING** |
| `Cairn` | (0, 0.4, 64) | `Props.Cairn` | `…/Art/Generated/Props/Cairn.prefab` | **MISSING** |
| `IDChit_Kira` + label | (0, 0.85, 64) / label (0, 1.1, 64) | `Props.IDChit` | `…/Art/Generated/Props/IDChit.prefab` | **MISSING** |
| `GraveEmber0` | (-4, 1.6, 58) | — | `ChapterEnvironmentProfile.accentLights["GraveEmber0"]` | profile *(also carries `AmbientPulse(5s)`)* |
| `GraveEmber1` | (4, 1.6, 68) | — | `ChapterEnvironmentProfile.accentLights["GraveEmber1"]` | profile *(also carries `AmbientPulse(5.7s)`)* |
| Vera Dusk | (0, 0, 66) | `Named.VeraDusk` | `…/Characters3D/Named/Vera-Dusk.prefab` | **EXISTS** *(placeholder greybox — see note)* |
| `Props.VeraLedger` (held) | hand-attached, no standalone transform yet | `Props.VeraLedger` | `…/Art/Generated/Props/VeraLedger.prefab` | **MISSING — not yet in the registry at all** |
| Vera's drawn weapon (held) | hand-attached, no standalone transform yet | *(no key assigned yet)* | — | **MISSING — not yet in the registry at all** |

**Notes on the transition.** `CollapsedWallShell`'s current primitive scale is `(2.2, pos.y × 2, 0.4)` — the Y-scale is literally derived from the prop's own spawn-height literal (0.4–0.8), a shorthand that happens to produce short, varied-height rubble but has no semantic meaning once a real mesh replaces it; a prefab must not try to reproduce this coupling, it should simply ship a family of collapsed-shell silhouettes at varying heights. `IDChit_Kira` today is a small unlit-material cube (no collider, deliberately non-interactive) plus a separate child `TextMesh` reading "KIRA" — the registry migration should fold both into one prefab with the name baked into the mesh/decal rather than a runtime `TextMesh`, unless a future ledger-of-the-dead UI wants to swap the label dynamically per-language.

**The mass grave has no scar — only marker stones on flat ground.** Canon names the grave itself as physical geometry four times: "a long low scar in the ground," "the head of a long low scar," "a long low scar in the ash with the ledger laid on it." `Ch5BuildMassGrave` (`Chapter5Builder.cs:385-392`) represents it purely as a 3×4 grid of small (0.3,0.3,0.15) `GraveMarker` cubes over unbroken flat `AshGround` — there is no trench, depression, or dark ground decal, so the grave reads as twelve pebbles on level ash, not a mass grave. `Props.GraveScar` is added to the table above and to Appendix B as a **MISSING** key — a long, low, darker trench/decal running under the marker grid (z≈52–64) — cheap to add and it's the defining silhouette of the chapter's destination, the shape the cairn, the markers, and Vera are all arranged around.

**`IDChit_Kira` is the reveal's bright focal magnet, not just fog-thinning (§4 Beat 1b).** At unlit material (0.95,0.9,0.75), the chit is the single brightest non-ember point in the grey grave, sitting at (0,0.85,64) with Vera stood 2 m behind it at z=66. As the fog thins between `SettlementReachPoint` and `GraveReachPoint` (§4 Beat 1b's density math), the chit is the warm-bright object that catches the eye first and pulls the player's gaze toward Vera's exact position — the reveal is fog-thinning **plus** a bright focal anchor at the grave's head, not fog alone. Worth flagging so a future pass doesn't dim or relocate that chit without accounting for the sightline it currently anchors.

**The chit's "KIRA" label reads backwards to the approaching player — same unset-rotation bug family, worse placement.** `IDChit_Label` is created at (0,1.1,64) (`Chapter5Builder.cs:410-419`) with no rotation ever written, so the `TextMesh` sits at prefab/TextMesh default facing +Z — a player arriving from −Z (the whole real-world approach direction, §2) reads "KIRA" mirror-reversed, from behind the glyph. This is the same root cause the doc already flags for Vera Dusk (§4 Beat 1b) and the landing party (§4 Beat 0b), but it lands on the single most name-legible, canon-load-bearing prop in the level — canon: "a single worn ID-chit set at its head with the name KIRA on it." The fix and its precedent both already exist in-file: `Ch5BuildCompleteCanvas` sets its canvas's `rotation = Quaternion.Euler(0,180,0)` with the comment `// face -z, toward the player` (`Chapter5Builder.cs:552`) — the same one-line fix applies to `IDChit_Label`. Until it lands, the chit reads bright, but its name currently reads backwards.

**Scavenger silhouettes read as close static mannequins, not distant glimpsed figures.** `Scavenger_Silhouette0/1` sit at (-7, 0.9, 20) and (7, 0.9, 44) — right beside the player's path (|x|=7) and never move — while canon has them "scatter at the sight of armed bodies and [are] gone" before the confrontation. The doc correctly notes elsewhere that this scatter is in the fiction, not the sim (§3), but the placement itself works against that reading: a staging fix (no new component needed) would push both silhouettes to higher |x| or deeper into the fog band so they register as ambiguous, distant shapes rather than posed dummies passed at arm's length. **A cheaper, more canon-faithful additive goes one step further than repositioning alone:** place the silhouettes as distant fog-shapes further north, then `SetActive(false)` them the moment the player crosses `SettlementReachPoint` (z=20, step 2) — reusing the reach-trigger pattern already wired into the mission spine, no new component and no movement required. That delivers canon's "scatter at the sight of armed bodies and [are] gone" directly: ambiguous distant figures on approach, verifiably gone once the crew is in the ruins — an impression static repositioning alone can't produce, since a prop that never disappears can't deliver "gone." Positions are unchanged pending that pass.

**"Doors all open" has no visual referent.** Echo's Beat 1 bark — "Doors all open. Nobody left to close them." (§4 Beat 1e) — and the dialogue script's SETTING block ("doors standing open on rooms no one came back to") both foreground open doorways as the settlement's defining image. The ruins art today is six `CollapsedWallShell` fragments and three `BurnedStall`s — no doorframe or threshold prop anywhere, so the player hears the line with nothing in view to match it. `Props.OpenDoorframe` is added to the table above and to Appendix B as a **MISSING** key with no placements yet defined; a small number of standing frames (no door leaf, since none opens or closes) scattered through the ruins zone (z[16,50]) would give the line something to land on.

**Vera's two hand-props are unstaged.** Canon's Beat 1 SETTING has her holding "a worn ledger in one hand and a drawn weapon in the other," and she keeps the weapon aimed at the player through Beats 1 and 4 (`ch5_beat1_accusation`'s "that wrapped thing on your hip," `ch5_beat4_demand`'s "I have a gun"). Neither prop exists in the build — `Props.VeraLedger` and the held weapon are added to the table above as **MISSING** rows the registry doesn't even have keys for yet (not just unresolved slots, genuinely absent from `ArtAssetRegistry`'s key list — see Appendix B). Until they're built and attached (most likely as child transforms of the Vera-Dusk prefab's hand bones once real character art lands), the chapter's central accusation plays with an empty-handed NPC.

**The ledger carries its own uncaptured detail: a second KIRA chit, held rather than set.** Dialogue-script line 188 stages Vera mid-accusation with "the ledger out now, a worn ID-chit pressed flat against its cover under her thumb" — the same KIRA-chit motif already commissioned as `IDChit_Kira`/`Props.IDChit` for the cairn's head (line 303 above), but here worried under her thumb through the whole confrontation rather than fixed at the grave. The two props are currently being tracked as unrelated: the cairn chit is a **MISSING** environment row, `Props.VeraLedger` is a **MISSING** held prop with no chit noted at all. When `Props.VeraLedger` is commissioned, it should carry a worn ID-chit pressed against its cover per line 188 — the deliberate echo tying the ledger in her hand to the chit on the grave, not two unrelated objects.

**The weapon also has its own canon aim-state arc, parallel to the kneel→stand pose arc tracked in §9 — not just a static hand-attachment, and it's four states, not three.** Canon moves it through "the weapon not quite level" at Beat 1's confrontation (line 164) → **"forgotten in the grey beside her"** (line 298) as she kneels at the dive's end/Beat 3 — dropped, not merely lowered, which is the only reason it can be "recovered" next → "recovered and level now, aimed at the man" at Beat 4's open (line 348) → "She lowers the weapon all the way" before the verdict (line 372). The dropped/forgotten state is the prop-level echo of her kneel and should fire on the same mission steps (12/13) that stage the kneel itself, not a separate cue — the two prop/pose changes are one visual beat, not two. Once the prop exists, it needs the same mission-spine-driven state changes as the pose swap (dropped in the ash at step 12/13's kneel, recovered/leveled at step 15's demand, lowered before step 16's verdict) — a frozen static attachment would leave "recovered and level" (Beat 4's open) with nothing to recover *from*, and "the gun does not fire" / "she lowers it" playing against a prop that never visibly moves.

**On Vera Dusk's `EXISTS` status:** her prefab resolves today, but only to `PlaceholderCharacterBuilder`'s ash-grey greybox humanoid (a tinted capsule+sphere archetype with a floating `[PLACEHOLDER] Vera-Dusk` label) — not final character art. Mark this distinctly from a true `EXISTS`: it satisfies the fallback rule's "never an empty room" guarantee, but a pass replacing it with a real Tripo mesh is still outstanding work, tracked the same as any other `MISSING` row functionally, just not blocking playability.

#### d. Combat

None. This matches the beat treatment's explicit design intent (*"mostly traversal/horror-tone; optional scavenger threats; no roster boss"*) and Ch3's zero-combat precedent, cited directly in the builder's class doc comment. The two scavenger silhouettes carry no `Enemy` component, no AI, no collider interaction beyond set dressing.

#### e. Dialogue / VO

`ch5_beat1_approach` (0,1,24), 2 lines, ≈13 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | She's not a scavenger, not a picker. She's been standing at that grave since before we landed. Careful, Cipher. This isn't a fight. It's worse than one. | 10 |
| Echo | Doors all open. Nobody left to close them. | 3 |

**A second canon scavenger bark is unshipped.** The dialogue script's Beat 1 PLAYABLE block gives Echo a second systemic bark — "Tracks in the ash, days old. Pickers, not a patrol." — which directly reinforces the "scavengers are texture, already-fled" reading (§3, Beat 1c) but did not make the shipped set above. A legitimate additive ambient-bark candidate, not a spine step.

`ch5_beat1_accusation` (0,1,60), 5 lines, ≈43 s:

| Speaker | Line | sec |
|---|---|---|
| Vera Dusk | Don't. Don't say you're sorry, and don't say you're here to help. The last people who walked out of the sky carried that wrapped thing on your hip, and they didn't help. They counted us and they left. | 13 |
| Ronin-7 | I'm not going to say either of those. | 3 |
| Vera Dusk | You logged this as pacification. A clean word so the men who did it could sleep. There were children in that count. | 10 |
| Vera Dusk | This one was my sister. Kira. She was fourteen. She kept lists of the ships that came through, because she liked the names. And then a ship came through that didn't have a name, and it had you on it. | 14 |
| Ronin-7 | I won't lie to you. I don't remember her. I don't remember any of them. They took it out of me when it was finished. I won't dress that up as anything but what it is. | 13 |

`ch5_beat1_kira_named` (0,1,62), 5 lines, ≈47 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | What I have is the sword. It remembers what I can't. It says I was there. It says I was late. And late doesn't help her. Or you. | 9 |
| Vera Dusk | Late. You stood in my square with that sword and you were late. What does a thing like you even mean by late. | 8 |
| Echo | She wants to and she can't. Give her the truth. It's all you've got that's worth anything to her. | 7 |
| Ronin-7 | I can tell you what late meant. No one sees inside this but me and the blade. I'll walk it again and say every second out loud as I see it. If you want that, I won't make you. | 14 |
| Vera Dusk | Tell me, then. I've spent ten years not knowing how she went. Whatever it is, it can't be worse than the version I built in the dark. | 9 |

**The beat treatment's "say her name" exchange didn't survive into shipped VO.** The beat treatment's KEY SCENES screenplay writes this beat's signature line pair as Vera: *"Then say her name."* / Ronin: *"Kira Dusk. I was there. I was late."* The shipped `ch5_beat1_kira_named` set above restructures it — Vera names Kira herself back in `ch5_beat1_accusation` ("This one was my sister. Kira"), and Ronin never says "Kira Dusk" aloud anywhere in the chapter. This is the one canon dialogue divergence the doc hadn't reconciled elsewhere: the *dialogue script* (cross-referenced throughout this document) is the shipped source, not the beat treatment's more compressed screenplay pass.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics — pure traversal and confrontation dialogue.
- `AshWorldGardenAmbience` (center (0,1.5,40), inner 6/outer 30, vol 0.4) is centered almost exactly on the ruins, so it's at its loudest through Beat 1's traversal, thinning again as the player nears the grave at z=58+.
- `GraveEmber0`/`GraveEmber1` both carry `AddAmbientPulse` (periods 5 s and 5.7 s respectively, deliberately offset so they don't pulse in lockstep) — a slow, uneasy breathing light over the grave, distinct from the flatter Landing/Ruins embers which carry no behaviour.
- **Two SETTING-block layers are missing from this walk (§7, §9):** the ash-underfoot footstep surface for the ~60 m approach, and "the creak of dead structures" — Beat 1's SETTING lists "wind, ash-drift, the creak of dead structures" as its three atmospheric sounds, and today only wind (`AshWorldGardenAmbience`) plays.
- Comfort vignette engages normally on the ~60 m walk from landing to grave — the longest single unbroken traversal stretch in the chapter.

---

### Beat 2 — Walking the Dead (The Memory-Dive)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read the dive's structural geometry (ground, stalls, well) from `ArtAssetRegistry`; the ghost figures stay procedural (§1.5 — do not commission meshes for them). Create **`BuildBeat2Art()`** (`MemoryDive_Massacre` and everything under it, built inactive) and **`BuildBeat2Logic()`** (`MemoryDiveController`, `EnterDiveTrigger`/`ExitDiveTrigger`, the two dive-lane `ReachTrigger`s, the three dive dialogue sets). **The dive root must stay `SetActive(false)` until step 7's Trigger fires** — this is the one beat where building it active would be a correctness bug, not just a style issue, since `MemoryFlashbackController.Awake()` would apply the fog/ambient override to the real ash-world the instant the scene loads.

#### a. Narrative purpose & emotional target

Beat 1 named the wound; Beat 2 walks inside it. Per the dialogue script's SETTING block, this is *"not a place that still exists: the settlement as it was, the day it died, replayed off the katana's stored memory. Desaturated, wrong, edges that smear like a signal under strain."* Structurally the dive is Ronin-7's memory alone — Vera and the crew never leave the graveside; everything the player experiences here is narrated back to Vera in real time by Ronin-7's own voice, carried as a thread from the present into the recording. The player walks the dying square as a **witness, not an actor**: the conditioned younger self moves ahead on rails, always one lane away, never reachable, closing on the last knot of the living — Kira, at the well. The beat's whole design intent, stated explicitly in both the beat treatment and the builder's own class comment, is **ghost silhouettes only, no sentinels, no violence depicted** — this deliberately narrows the dialogue script's own stage direction (*"limited, dreamlike combat — the player cannot stop the massacre, only walk its length"*) down to pure traversal; see §9 for that gap. The beat resolves on the load-bearing hesitation: the conditioned hand rises, and *stops* — a fractional hitch, the first flicker of conscience in a body built to have none, arriving one breath too slow to matter. Echo frames it without letting it become redemption (*"It was the first true thing you ever did. And it was too late to matter. I can't make that into one thing"*), and Ronin-7 ties every later mercy in his life back to this exact failure as an unpaid debt, not a credit.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

- **`MemoryDiveController`** (`MemoryDive` GameObject): wires `diveRoot` = `MemoryDive_Massacre`, `diveEntryPoint` = `DiveEntryPoint` (0,0,152), `diveExitPoint` = `DiveExitPoint` (0,0,60) — back at the grave, facing Vera — `rigRoot` = the player rig's own transform, `flashback` = the `MemoryFlashbackController` living on `MemoryDive_Massacre`.
- **`EnterDiveTrigger`** and **`ExitDiveTrigger`**: both built `SetActive(false)`; each holds only a `dive` reference. `OnEnable` calls `EnterDive()`/`ExitDive()` respectively — the same "inactive-until-a-Trigger-step-activates-it" idiom `NpcWalker` uses elsewhere, so `MissionDirector` never has to know `MemoryDiveController` exists.
- **`DiveLaneReachPoint`** (0, 1, 165), radius 5 and **`DiveNearGirlReachPoint`** (0, 1, 188), radius 5 — both *inside* the dive island, gating the dive's own internal dialogue pacing.
- **Dialogue anchors:** `Dialogue_Beat2_DiveIntro` (0,1,157), `Dialogue_Beat2_CountingStock` (0,1,170), `Dialogue_Beat2_Hesitation` (0,1,188).
- **Sightline inversion at `DiveNearGirlReachPoint`.** The player enters the dive at `DiveEntryPoint` (0,0,152) facing +Z and walks north; `DiveNearGirlReachPoint` (0,1,188) and the hesitation dialogue anchor (0,1,188) sit at the same z as, or past, the beat's two load-bearing figures — `Ghost_YoungerRonin` (0,0,182) and `Ghost_Kira` (0.5,0,187) — so by the time the trigger fires and Echo says "Watch his hand… Not the blade. The hand" (`ch5_beat2_hesitation` line 1), both figures are *behind* the player, who is facing an empty +Z lane. Canon's framing is the reverse: the player approaches the conditioned self "closing on… the girl… between him and a doorway," with the tableau ahead, not behind. **This is the chapter's second "most important sightline is currently backwards" case, alongside Vera Dusk's facing (§4 Beat 1b, §9).** Fix by moving `DiveNearGirlReachPoint` and the hesitation dialogue anchor to ~z=180 — south of `Ghost_YoungerRonin` (182) — so the hand rises toward Kira with both figures ahead of the player in the +Z gaze direction.

**Mission-spine steps (this beat):**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 7 | Trigger | "Trigger: Enter the Memory-Dive (the massacre)" | activates `EnterDiveGo` → `MemoryDiveController.EnterDive()` — activates `MemoryDive_Massacre`, applies the flashback fog/ambient treatment, teleports the rig to `DiveEntryPoint` |
| 8 | Dialogue | "Beat2: Walking the Dead (dive opens)" | plays `ch5_beat2_dive_intro` (2 lines) |
| 9 | ReachTrigger | "ReachTrigger: Dive — The Lane" | player enters `DiveLaneReachPoint` (0,1,165), r=5 |
| 10 | Dialogue | "Beat2: Counting Stock" | plays `ch5_beat2_counting_stock` (3 lines) |
| 11 | ReachTrigger | "ReachTrigger: Dive — Nearing the Girl" | player enters `DiveNearGirlReachPoint` (0,1,188), r=5 |
| 12 | Dialogue | "Beat2: The Hesitation" | plays `ch5_beat2_hesitation` (5 lines) — the chapter's load-bearing beat |
| 13 | Trigger | "Trigger: Exit the Memory-Dive (surface)" | activates `ExitDiveGo` → `MemoryDiveController.ExitDive()` — deactivates `MemoryDive_Massacre`, restores pre-dive fog/ambient, teleports the rig to `DiveExitPoint` (0,0,60), back at the grave |

**What changes during the beat:** the entire dive geometry (§4c below) transitions from nonexistent-to-the-scene (inactive) to fully present the instant step 7 fires, and vanishes again at step 13 — the single largest single-frame prop-count swing in the chapter (§1.6). The rig itself physically relocates twice via instant teleport, not locomotion.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

All of the following live under `MemoryDive_Massacre`, built `SetActive(false)`:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `MemorySquare_Ground` | center (0,-0.1,175), scale (18,0.2,50) | `Terrain.MemorySquareGround` | `…/Art/Generated/Terrain/MemorySquareGround.prefab` | **MISSING** |
| `MemoryStall0` | (-4, 0.4, 158) | `Props.MemoryMarketStall` | `…/Art/Generated/Props/MemoryMarketStall.prefab` | **MISSING** |
| `MemoryStall1` | (4, 0.4, 162) | `Props.MemoryMarketStall` | `…/Art/Generated/Props/MemoryMarketStall.prefab` | **MISSING** |
| `MemoryStall2` | (-3, 0.4, 178) | `Props.MemoryMarketStall` | `…/Art/Generated/Props/MemoryMarketStall.prefab` | **MISSING** |
| `MemoryWell` | (1, 0.4, 186) | `Props.MemoryWell` | `…/Art/Generated/Props/MemoryWell.prefab` | **MISSING** |
| `Ghost_Villager0..3` | (-3.5,0,159) (3.5,0,163) (-2,0,176) (4,0,180) | *(procedural — not a registry candidate, §1.5)* | `MemoryFlashbackController.MakeGhostMaterial()` on capsule+sphere | **BY DESIGN** |
| `Ghost_Kira` | (0.5, 0, 187) | `Named.KiraDusk` | `…/Characters3D/Named/Kira-Dusk.prefab` | **EXISTS** *(real baked mesh, ghost-tinted)* |
| `Props.KiraSlate` | near `Ghost_Kira`, e.g. (0.5, 0.5, 187) — not yet placed | `Props.KiraSlate` | `…/Art/Generated/Props/KiraSlate.prefab` | **MISSING — not yet in the registry, no instance built** |
| `Props.MemoryDoorway` | just past `Ghost_Kira`, far (+Z) side of the well, e.g. (0.5, 0, 190) — not yet placed | `Props.MemoryDoorway` | `…/Art/Generated/Props/MemoryDoorway.prefab` | **MISSING — not yet in the registry, no instance built** |
| `Ghost_YoungerRonin` | (0, 0, 182) | *(procedural — not a registry candidate)* | ghost material on capsule+sphere | **BY DESIGN** |
| `Ghost_Squad0/1` | (-1.2,0,178) (1.4,0,179) | *(procedural — not a registry candidate)* | ghost material on capsule+sphere | **BY DESIGN** |
| `MemoryLight_Entry` | (0, 2.6, 158) | — | cold accent, parented under the dive (not a standalone light) | inline |
| `MemoryLight_Mid` | (0, 2.6, 174) | — | cold accent | inline |
| `MemoryLight_Well` | (0.5, 2.6, 187) | — | cold accent | inline |
| `DiveEntryPoint` | (0, 0, 152) | — | empty transform, dive teleport anchor | logic |

**Notes on the transition.** Unlike the real ash-world's `BuildAccentPointLight` (a standalone root-level GameObject), the three `MemoryLight_*` accents are built by `Ch5BuildPlaybackLight`, which explicitly parents them *under* the dive root — the builder's own comment explains why: a standalone light would illuminate the memory square even while the dive is inactive. This is a deliberate, correct divergence from the standard accent-light helper and must survive the registry refactor: dive-interior lights stay children of `MemoryDive_Massacre`, never root-level. `Ghost_Kira` is the one figure in the dive with real character art (`Kira-Dusk.prefab`, `FitNamedCharacter`-grounded) — every renderer on it is swapped to the shared ghost material at build time, same treatment as the procedural silhouettes, so it reads consistently with the rest of the recording despite having a real mesh underneath.

**`Ghost_Kira`'s facing is correct by accident, not by design — the same unset-rotation root cause the doc flags as a bug for Vera Dusk (§4 Beat 1b).** `InstantiateNpc(Ch5KiraDuskPrefab, (0.5,0,187), "Ghost_Kira")` (`Chapter5Builder.cs:467`) never writes a rotation either, but here the prefab-default +Z happens to face the proposed `Props.MemoryDoorway` placement (+Z, just past the well) — the tableau reads correctly as "caught one stride short of the threshold" purely because the two unset defaults happen to line up. That alignment is coincidental, not pinned: a future prefab-default change would silently turn Kira away from the doorway she dies reaching for. Recommend pinning her rotation explicitly (facing the `MemoryDoorway`) at build time, exactly as the Vera fix does, so the "reaching for the threshold" tableau is protected rather than accidental.

**Kira's slate — her one characterizing object — has no prop.** Canon returns to it three times: "a girl by the well with a slate, counting ships" (`ch5_beat2_dive_intro`), "she kept lists of the ships… because she liked the names" (§4 Beat 1e). It's the object that makes Kira a person rather than a body, and the cheapest, highest-characterization prop in the chapter — a small slate, ghost-tinted like everything else in the recording, held or resting beside `Ghost_Kira`. `Props.KiraSlate` is added to the table above and Appendix B as a **MISSING** key; unlike the villager/squad silhouettes (§1.5), this is not a "stays procedural by design" case — it's a genuinely unbuilt prop, not a deliberate stylistic choice.

**Verify-only: Kira's ghost height may not read as a fourteen-year-old.** Canon states plainly: "Kira. She was fourteen." The villager/squad ghosts get explicit heights 1.6–1.8 m via `Ch5BuildGhostFigure`, but `Ghost_Kira` is the `Kira-Dusk.prefab` mesh grounded by `FitNamedCharacter` (`Chapter5Builder.cs:467-473`) at whatever scale that helper normalizes to. If `FitNamedCharacter` grounds to a standard adult height, the fourteen-year-old the whole beat turns on would stand adult-sized among the villagers. Worth a one-line check next time the scene is opened in-editor: confirm `Ghost_Kira` renders visibly smaller than the adult silhouettes, matching Mira's child scale, not normalized to them.

**The memory doorway Kira dies reaching for has no representation.** Canon's Beat 2 cutscene turns on this geometry: "the girl with the slate, Kira, between him and a doorway… the doorway is not reached in time, Kira does not make it through." `Props.OpenDoorframe` (§4 Beat 1c) is commissioned only for the real-world ruins; the memory-dive doorway that makes "she doesn't make it through" spatially legible is absent from this table. `Props.MemoryDoorway` (ghost-tinted, same treatment as the rest of the recording) is added above, placed just past `Ghost_Kira` on the far (+Z) side of the well, so the frozen tableau reads as "Kira caught one stride short of the threshold" rather than a girl standing in open ground — as high-characterization and as cheap as `Props.KiraSlate` above.

**"Watch his hand" has no hand to watch — the chapter's load-bearing line points at an armless figure, and it's a two-part contrast, not a single callout.** `Ch5BuildGhostFigure` (`Chapter5Builder.cs:513`) builds only a `Body` capsule and a `Head` sphere — no arms, no hands — and `Ghost_YoungerRonin` (0,0,182) is built from this same template (`:478`). But `ch5_beat2_hesitation` L1 is Echo's full line — *"Watch his hand. **Forget the blade.** The hand"* — an explicit A-vs-B gaze cue, not just a hand callout, and canon separately describes the younger self "advancing… with a wrapped blade" (line 235). Ronin's own L2 turns entirely on the visible image — *"Something in me moved… a half second after it could have mattered"* — the hand rising and stopping is the chapter's thesis image, not incidental staging. §4 Beat 2f already notes the ghosts are "static, non-looping silhouettes," but that gap applies here even harder: the gaze-directing line currently has **zero referent geometry** for either half of its contrast. Recommend a concrete additive, not a new system: give `Ghost_YoungerRonin` a single distinguishing arm/hand element holding a ghost-tinted wrapped blade, posed mid-rise (still ghost-material, still static — a raised forearm gripping the blade off the standard `Ch5BuildGhostFigure` capsule+sphere template, not a rebuild of the shared helper), so "forget the blade / watch the hand" has two distinct referents for the gaze to move between. Without it, Echo directs the player's eyes to an armless capsule at the exact second the whole chapter is built to pivot on — and the half of the line telling the player to look past the blade points at nothing either.

#### d. Combat

**None built** — zero `Enemy` components exist inside the dive. This is a deliberate, explicit design decision recorded in the builder's own class doc comment (*"still ghost silhouettes only, NO sentinels and NO violence depicted, resolving on the hesitation beat"*), and it is a **narrower interpretation than the beat treatment/dialogue script**, which both describe *"limited combat"* / *"limited, dreamlike combat"* inside the dive. Flagging this as an intentional as-built narrowing, not an oversight to silently fix — see §9 for the open question of whether that gap should be closed.

#### e. Dialogue / VO

`ch5_beat2_dive_intro` (0,1,157), 2 lines, ≈28 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | This is the part I never wanted to play back for you. I rode behind those eyes when they did this. I logged every second and couldn't stop a single one of them. Stay with me, Cipher. We only have to watch. | 14 |
| Ronin-7 | The square's full. Stalls up, people trading. There's a girl by the well with a slate, counting ships. I come up the lane with the blade, and I'm not hurrying. I'm telling you what I see, Vera. | 14 |

`ch5_beat2_counting_stock` (0,1,170), 3 lines, ≈23 s:

| Speaker | Line | sec |
|---|---|---|
| Vera Dusk | You say it so flat. Like stock. That was a market. Those were people. That was my sister. And you walked through it counting. | 9 |
| Ronin-7 | I felt nothing. I know how that sounds. They built me so this would feel like nothing. | 7 |
| Ronin-7 | There's no monster here to find, Vera. Just a hand that did what it was told and never asked. | 7 |

**Vera's opening retort here has lost its shipped antecedent — the set's own name-phrase, no less.** "You say it so flat. Like stock." is a direct callback, but the line it calls back to was thinned out of the shipped VO: canon line 243 has Ronin say "…I'm telling you what I see, Vera. I move through them like a man counting stock," while the shipped `ch5_beat2_dive_intro` L2 above ends on "…I'm telling you what I see, Vera" and drops the "counting stock" clause entirely — the phrase this whole set is named `ch5_beat2_counting_stock` after. Vera's callback now lands on a word Ronin never actually says in the build. This is the same audit-trim failure mode the doc already flags for the dropped comm-plant clause in Beat 0 (§4 Beat 0e, line 249) — an additive VO candidate (restore the "counting stock" clause to `dive_intro` L2) or an accepted softening, but it should be noted rather than silent.

`ch5_beat2_hesitation` (0,1,188), 5 lines, ≈36 s — **the chapter's core landing beat**:

| Speaker | Line | sec |
|---|---|---|
| Echo | Here. This is the second you've been carrying without knowing the shape of it. Watch his hand. Forget the blade. The hand. | 9 |
| Ronin-7 | There. That's late. Something in me moved for the first time, and it moved a half second after it could have mattered. One breath too slow to save her. | 11 |
| Vera Dusk | You hesitated. | 1 |
| Ronin-7 | For the first time in my life. And it didn't save her. I've spared people since because of what started in this square. | 9 |
| Echo | I logged this as a fault for years, Cipher. It was the first true thing you ever did. And it was too late to matter. I can't make that into one thing. | 10 |

**Dwell/idle Echo barks are missing from the dive walk, though canon explicitly asks for them.** The dialogue script's Beat 2 PRODUCTION NOTE (lines 259-260) calls for "position-triggered lines… that double as the player's only anchor in a destabilizing memory" plus "idle/ambient Echo lines that fire on dwell to keep the dread alive." The build ships only the three scripted `DialoguePlayer` sets above (`dive_intro` z=157, `counting_stock` z=170, `hesitation` z=188); the ~35 m walk between them (entry z=152 to the well z=187) has no dwell-triggered ambient Echo layer, so a player who lingers between anchors gets silence, not the "Echo never lets the dread go quiet" canon asks for. Additive-only gap, mirroring the Beat 1 systemic scavenger-bark note (§4 Beat 1e); also tracked in §7's non-dialogue audio table.

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point — the hesitation, the load-bearing beat of the whole chapter, is carried entirely by VO performance, the fog/ambient shift, and the frozen-figure staging; never by moving the player's view.
- **Nothing currently locks player control for the hesitation.** Canon is explicit — "[CUTSCENE — ON REACHING THE GIRL: player control locks]" — but step 12 is a plain `AuthorDialogueStep`; continuous locomotion stays live through the whole of `ch5_beat2_hesitation`, so the player can simply walk away up the lane while Echo/Ronin narrate the chapter's thesis to an empty vantage. A comfort-safe locomotion freeze for the line's duration (freezing stick input, not the camera — this is not camera shake and is fully VR-comfort-legal) would guarantee the player is present for the beat; absent that, canon's "control locks" direction is silently not implemented today. Flagged as an open question in §9.
- **Two more canon dive-distortion cues are unbuilt, alongside the smear fog already captured above.** The dialogue script specifies the recording as "edges that smear and reassemble, sound coming a half-beat late" and "Ambient memory-distortion (smear, late audio, figures that loop) escalates as the player nears the center" (lines 235, 257). `MemoryFlashbackController.ApplyTreatment()` only ever touches `RenderSettings` (fog/ambient) — there is no delayed/detuned dive audio bus for the "late audio" cue, and the ghost figures (§4c) are static, non-looping silhouettes, not the looping-motion figures canon describes. As with this beat's zero-combat narrowing (§4d, §9), this is a deliberate as-built narrowing of canon's dive-fidelity spec, not an oversight — a reviewer should read the smooth static staging and unshifted audio as a known simplification alongside the combat gap, not a bug in either case.
- **Vera's in-dive VO needs the same spatialization fix already applied to the Beat 3 comm-relay (§4 Beat 3f).** Two of her lines play while she is not present in the dive space at all — `ch5_beat2_counting_stock` L1 ("You say it so flat… That was my sister") and `ch5_beat2_hesitation` L3 ("You hesitated") — from `DialoguePlayer` anchors at z=170 and z=188, deep inside the memory island. Canon is explicit that "Vera and the graveside are NOT present in the dive space; only Ronin-7 and Echo perceive it" — inside the dive, only Echo is truly *in* the space, and Ronin's narration and Vera's replies are the "thread carried from the present" (canon's own framing). Both should read non-spatialized, from Ronin's own vantage, not localized to the z=170/188 dive anchors — the direct analog of the Iris/Mera collar-comm treatment, left unflagged here until now.
- `MemoryFlashbackController.ApplyTreatment()` is the beat's core "juice": `ExponentialSquared` fog at density 0.045 (heavier and a different falloff curve than the real world's 0.042 `Exponential`), flat ambient (0.30, 0.30, 0.34) — the "desaturated, wrong, edges that smear" read is achieved entirely through `RenderSettings`, not a post-process volume or a shader effect.
- `GhostVillageDreadAmbience` (center (0,1.5,170), inner 6/outer 25, vol 0.4) is the dive's dedicated ambience bed, distinct from the real-world's `AshWorldGardenAmbience` — the two never play simultaneously since the dive and the real world are mutually exclusive active states.
- Comfort vignette applies normally through the dive's ~35 m internal walk (entry z=152 to the well at z=187); the two instant rig teleports (entry/exit) need no vignette, since there's no continuous motion to shroud — but that is not the same as comfort-safe outright: see §1.1's open question on whether `EnterDive()`/`ExitDive()`'s same-frame geometry+fog+ambient swap warrants a `ScreenFader` blink.
- **Haptics:** none scripted — matching §4d's zero-combat status. **One placement would be worth the exception:** at `ch5_beat2_hesitation` L2 ("There. That's late.") — the conditioned hand rising and stopping — a single soft controller pulse (a hitch, not a rumble) would physicalize the frozen half-second without any camera motion. Paired with `MemoryFlashbackController.heartbeatLoop` (§7, currently unwired), this hitch would give the chapter's core landing beat the two non-shake juice channels — haptic and audio — the project's stated philosophy calls for, both currently silent.

---

### Beat 3 — The Ledger ("Kael Vor" Exposed) — REVEAL

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> This beat builds **no new geometry at all** — it is a comm-relayed dialogue set played back at the grave, over the same static Beat-1 dressing. If a future pass adds any physical representation of Iris/Mera Voss (a holo-bust, a comm console prop), that is new art and belongs in this beat's `BuildBeat3Art()`; today there is nothing to build.

#### a. Narrative purpose & emotional target

The dive ends and the present rushes back in — Vera on her knees in the ash, hollowed out, the weapon forgotten beside her — a collapse-then-rise arc this beat and Beat 4 name in the fiction but the build does not stage: `Vera Dusk`'s `StoryNpc` (`wanderRadius: 0`, §4 Beat 1b) is a fixed standing figure from spawn to `ChapterOutro`, with no pose state that changes across steps 12→13→15 (see this beat's §b and Beat 4a below, and §9). The unstaged arc actually has four beats, not two: canon also gives her a deliberate forward advance mid-accusation back in Beat 1 — "She steps forward through the ash, the ledger out now" (dialogue script line 188) — distinct from this beat's kneel and Beat 4's rise, so the full pose-state list a future pass would need is step-forward (Beat 1) → kneel (Beat 2 end/Beat 3) → rise (Beat 4) → lay the ledger on the cairn (Beat 4's close), not just the kneel/stand pair. The chapter delivers its structural reveal not through action but through two voices that were never physically present: Iris and Mera Voss, still aboard the Cairn, have spent the whole landing-party sequence cross-referencing the massacre's command records against Ronin-7's own Program file, and the comm crackles the instant they finish. This is **Ladder C, rung 1**: the operative-of-record was logged as **"Kael Vor"** — not a misfiled name, a *planted* one, issued by the Program specifically so the real hand doing the killing could be "wiped clean and kept." The vertigo here is deeper than Chapter 4's "I don't remember my name" — it's the discovery that the name he'd half-believed was his own is a fabrication laid over the truth on purpose. Ronin-7 refuses to let his own stolen identity eclipse Vera's grief in the same breath: *"Even the name on it was a lie. They couldn't tell the truth about who killed her."* The true name doesn't land until Ch16; this beat only confirms there's a real one buried under the false one.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

- No new spawns. The scene is exactly Beat 1's grave, with Vera Dusk still present at (0,0,66) and the player now back at `DiveExitPoint` (0,0,60), having just been teleported out of the dive.
- **Dialogue anchor:** `Dialogue_Beat3_Ledger` at (0, 1, 61).

**Mission-spine steps (this beat):**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 14 | Dialogue | "Beat3: The Ledger — 'Kael Vor' Exposed" | plays `ch5_beat3_ledger` (8 lines, the longest single set in the chapter) |

**What changes during the beat:** nothing physical. The mission-spine's only state change is progressing to Beat 4.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

Nothing new. This beat plays entirely over Beat 1's already-built grave geometry (§4, Beat 1's art table) — **do not re-instantiate a single grave prop here.**

**On "the slate" (dialogue script line 342):** Beat 3's SETTING/action direction has Ronin "turn from the slate back to Vera, still kneeling in the ash." Read this as narrative gesture — or a callback to Kira's slate at the well (§4 Beat 2c's `Props.KiraSlate`, still inside the just-exited dive) — not a built records-display prop at the graveside; the reveal itself is comm-relayed audio from the Cairn (§4b), with no physical slate anywhere in the real world. Noted here so a future pass comparing this beat against the dialogue script doesn't flag it as a missing Beat-3 prop.

#### d. Combat

None.

#### e. Dialogue / VO

`ch5_beat3_ledger` (0,1,61), 8 lines, ≈76 s:

| Speaker | Line | sec |
|---|---|---|
| Iris | Cipher. I need you to hear this now, it changes the shape of what you just told her. We finished cross-checking the command records against your own Program file. They don't match. | 12 |
| Ronin-7 | Match how. | 1 |
| Iris | The operative-of-record for this massacre is logged as Kael Vor. The name I flagged at the briefing. It still won't resolve, because there's nothing under it to resolve to. Nobody misfiled this. They planted it. | 15 |
| Mera Voss | She's right, and I've seen the trick a hundred times without turning it over. We logged kills under cover-names so the count would belong to a man who didn't exist. Kael Vor was a coat the Program hung the bodies on. | 15 |
| Ronin-7 | So the name they hung this on isn't even mine. | 3 |
| Iris | It was never yours, Cipher. Someone printed Kael Vor to carry the blame so the hand that did this could be wiped clean and kept. | 9 |
| Echo | I carried that name behind your eyes for years and logged it as fact. They leave you a fake to hold so you never go looking for the real one. We go looking now. | 11 |
| Ronin-7 | Even the name on it was a lie. They couldn't tell the truth about who killed her. | 5 |

#### f. Audio / Haptics / VR Comfort

- No camera shake. Iris and Mera Voss's lines are voice-only (no spatial `AudioSource` on a physical NPC — see the CREW-PRESENCE DECISION note, §4 Beat 0) — they should read as coming from Ronin-7's own comm/collar, not from a point in space near the grave.
- **The reveal's arrival has no audible onset.** Canon marks it diegetically: "the comm at Ronin's collar crackles… Iris's voice comes down to the ground the moment they finally come apart in her hands" (line 298) — the crackle *is* the cue that the present has caught up with the dive. Today step 14 simply begins as a dialogue step over unchanged grave ambience; nothing marks the instant the comm opens. A short comm-static/crackle SFX at the head of `ch5_beat3_ledger` (Iris's first line) is cheap, canon-cited, and gives the chapter's structural reveal an audible "the present rushes back in" onset distinct from the graveside VO that precedes it — see §7 for the missing-layer entry.
- `GraveEmber0`/`GraveEmber1`'s ambient pulse continues uninterrupted through this beat — the same slow, uneasy breathing light as Beat 1, now underscoring a reveal instead of an accusation.
- Comfort vignette inert — the player has no reason to be moving during this beat.

---

### Beat 4 — The Reckoning (Graveside)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> No new geometry beyond the chapter-outro pair. Create **`BuildBeat4Art()`** (nothing beyond what Beat 1 already built, plus the "CHAPTER 5 COMPLETE" canvas) and **`BuildBeat4Logic()`** (the four remaining dialogue sets + the `ChapterOutro`/`CampaignFlagSetter` wiring that sets `ch5_complete`).

#### a. Narrative purpose & emotional target

Canon opens this beat with Vera having "gotten to her feet" — the rise half of Beat 3's collapse, and the largest visual grief beat in the back half of the chapter (§4 Beat 3a). As built, there is no standing-up moment to open on: Vera has been standing, unmoving, since her spawn at build time. The chapter's four other beats all build toward this one decision, which the player cannot force: Vera raises the weapon she's carried for ten years and demands a reason not to fire. Ronin-7 gives none — *"I won't give you one. You don't owe me a reason. If my life is what clears it, take it."* — the same deliberate, unflinching offer he made Kerrax, scaled to its hardest test. The gun does not fire. What breaks Vera's resolve isn't mercy, it's evidence: she just watched a Program weapon walk back into the worst second of her sister's life and narrate every piece of it without once looking away, and she names exactly the thing the Program feared — *"They built a hand that doesn't feel. And it felt something in my square... And killing it won't raise her."* She passes a harder sentence than death: **live, and pay a debt that never closes.** Ronin-7 accepts it without softening it into hope (*"I won't pretend I can earn it down to nothing, because I can't"*). Mera Voss, still listening over the open comm, has her second data point against nine years of professional certainty (Kerrax in the caves; now this). Resh asks the practical, decent question — do they take her with them — and Ronin-7 refuses on her behalf and his own: *"She's not crew. She's got her dead to stay with, and that's hers."* Vera confirms it and points the chapter at its hook: *"I'll know your name when it's a true one. Come back and tell me, when you've dug it up."* The chapter closes on Ronin-7 turning the wound into a heading — *"If the name they hung on me was a lie, then everything behind it is too... We start digging"* — and Echo closing the beat, and the chapter, in the same low, companionable register it opened in.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

- No new spawns — the scene is still Beat 1's grave, unchanged.
- **Dialogue anchors:** `Dialogue_Beat4_Demand` (0,1,63), `Dialogue_Beat4_Verdict` (0,1,64), `Dialogue_Beat4_Release` (0,1,65), `Dialogue_Beat4_Hook` (0,1,66) — four separate anchor points, each 1 m apart along z, walking the camera's attention slowly north through the beat's four emotional turns without any of them being a literal blocking mark.
- **`ChapterOutro`** at (0, 1, 68), inactive. Wires a `CampaignFlagSetter` (flag `"ch5_complete"`) to its `OnActivated` `UnityEvent` via `UnityEventTools.AddPersistentListener`; `completeCanvas` ref = the "CHAPTER 5 COMPLETE" world-space canvas at (0, 1.4, 70); `publishZoneCompleted` defaults `true` on `ChapterOutro`, so `ZoneCompleted` fires post-fade the same way every other chapter finale signals `GameFlowManager`.

**Mission-spine steps (this beat):**

| Step | Kind | Label (builder) | Fires on |
|---|---|---|---|
| 15 | Dialogue | "Beat4: The Demand (a life offered)" | plays `ch5_beat4_demand` (3 lines) |
| 16 | Dialogue | "Beat4: The Verdict (a harder sentence)" | plays `ch5_beat4_verdict` (4 lines) |
| 17 | Dialogue | "Beat4: The Release (Vera stays, he walks)" | plays `ch5_beat4_release` (3 lines) |
| 18 | Dialogue | "Beat4: The Hook (the identity hunt opens)" | plays `ch5_beat4_hook` (2 lines) |
| 19 | Trigger | "Trigger: Chapter Outro (flag + fade + canvas)" | activates `ChapterOutro` — sets `ch5_complete`, reveals the canvas, fades, publishes `ZoneCompleted` |

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| "CHAPTER 5 COMPLETE" canvas | (0, 1.4, 70), facing -Z (Euler 0,180,0) | — | built inline (`Canvas`/`Image`/`Text`, no prefab) | inline |

Nothing else — this beat is entirely dialogue over Beat 1's already-built grave. **One canon gesture has no staging at all:** in canon, Beat 4 ends with Vera laying "the ledger on the cairn… both hands, like setting down a stone" — the chapter's closing physical image. No mission-spine step, animation, or prop-reparent exists to represent it; even once `Props.VeraLedger` (§4 Beat 1c) is built and attached to her hand, nothing in `BuildBeat4Logic()` moves it to the cairn. Flagged here and in §9 as unstaged, not silently assumed to "just happen" off-screen.

#### d. Combat

None. Per the beat treatment's Game Narrative Design section, this is explicit: *"the player cannot 'win' by force"* — there is no failure state, no health drain, no `DefeatEnemies` step gating the outcome. Vera's weapon is a dialogue prop, not a gameplay hazard.

#### e. Dialogue / VO

`ch5_beat4_demand` (0,1,63), 3 lines, ≈36 s:

| Speaker | Line | sec |
|---|---|---|
| Vera Dusk | I've imagined this with a thousand faces on you. Now I have the real one, and I have what you did, and I have a gun. Give me one reason this isn't the simplest thing I've ever done. | 12 |
| Ronin-7 | I won't give you one. You don't owe me a reason. If my life is what clears it, take it. I won't lift the blade and I won't ask you not to. | 11 |
| Vera Dusk | I came here to kill you. I've planned it for ten years. And I just listened to you walk back into the worst second of her life and tell me every piece of it without flinching. | 13 |

`ch5_beat4_verdict` (0,1,64), 4 lines, ≈50 s:

| Speaker | Line | sec |
|---|---|---|
| Vera Dusk | They built a hand that doesn't feel. And it felt something in my square. No wonder they were afraid of you. And killing it won't raise her. | 12 |
| Vera Dusk | Dying's easy. You'd be gone in a second and I'd still be standing here. So you don't get easy. You stay alive, and every day you do, you owe her. You won't ever be done. | 15 |
| Ronin-7 | I'll carry it. I won't pretend I can earn it down to nothing, because I can't. But I'll carry her name as long as I've got a hand to carry it. That much I can actually promise. | 12 |
| Mera Voss | Twice now. Kerrax in the caves, this one over a grave. I spent nine years certain the metal couldn't choose twice. I don't know what I'm sure of anymore. | 11 |

`ch5_beat4_release` (0,1,65), 3 lines, ≈28 s:

| Speaker | Line | sec |
|---|---|---|
| Resh | Do we take her with us. We don't leave people standing alone in a place like this. | 6 |
| Ronin-7 | No. She's not crew. She's got her dead to stay with, and that's hers. | 6 |
| Vera Dusk | He's right. I'm not joining your war. I'm staying with her, and the others under this ash, until I know they're remembered by someone who isn't a record. But I'll know your name when it's a true one. Come back and tell me, when you've dug it up. | 16 |

`ch5_beat4_hook` (0,1,66), 2 lines, ≈21 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | If the name they hung on me was a lie, then everything behind it is too. The grave, the file, the man I thought I half-remembered. None of it real. | 9 |
| Echo | Forget who they told you you were. We find who's under it. I've got nothing but time, Cipher, and I was there for the start of it. We'll find the rest. | 12 |

**Echo's tonal loop closes here.** §3 establishes that "the wry Ch4 warmth deliberately drops away" for this chapter. Canon's voice direction for Echo's closing line above brings it back on purpose — "the wry warmth allowed back just at the edges now they're walking out of the grave" — the register shift landing on the chapter's very last line of dialogue. This is a VO direction note, not a build change: `Chapter5Lines.cs` carries the line text but not performance direction, so a VO director should read this final Echo take with that edge of warmth restored, distinct from the flatter register carried through Beats 1–4.

#### f. Audio / Haptics / VR Comfort

- No camera shake at the demand, the verdict, or the release — the entire climax is carried by VO performance and stillness, not motion.
- **Beat 4 mixes a physically-present speaker with a comm-relay speaker in adjacent lines, the same split §4 Beat 3f documents for the reveal.** `ch5_beat4_verdict` L4 ("Twice now. Kerrax in the caves, this one over a grave") is Mera Voss, still aboard the Cairn (§4 Beat 0b's CREW-PRESENCE DECISION) — it should read non-spatialized, from Ronin-7's own collar, the same as her and Iris's Beat 3 lines. Vera's and Ronin-7's lines in the same set localize normally at the grave anchor `Dialogue_Beat4_Verdict` (0,1,64). No other beat mixes the two speaker types this closely.
- No haptics scripted anywhere in this beat.
- `GraveEmber0`/`GraveEmber1`'s ambient pulse keeps running unbroken through the whole reckoning — the chapter never introduces a new lighting cue for this beat, letting the same slow breathing light that's been present since Beat 1 carry through to the fade.
- **Canon opens this beat on a sensory drop the build has no mechanism to produce.** Beat 4 begins *"The wind has thinned"* (dialogue script line 348) — a held-breath quiet distinct from the fuller wind of the Beat 1 approach, before rising again into the closing "slow curtain" the missing ash-VFX (§9) would carry at the smash-to-black. `AshWorldGardenAmbience` is a single static `ProximityAmbienceLayer` at (0,1.5,40) (§7) with no volume/parameter change across the whole real-world run, so it can neither thin for the reckoning nor rise for the close — canon's two-stage "wind thins, then rises into the curtain" arc is flattened to one constant bed today.
- Comfort vignette inert (no player movement expected during the four dialogue sets); standard fade-to-black via `ScreenFader` on step 19, same convention as every other chapter's `ChapterOutro`.

## 5. Character travel-route master table

**Chapter 5 has no `NpcWalker`-driven travel at all** — this is the single largest structural difference from Chapter 1's travel model. No character in this chapter follows a scripted waypoint leg; every named NPC is a fixed (or tiny-idle-radius) `StoryNpc` placed once at build time and never relocated by the mission-spine. The chapter's only "travel" is the **player's own continuous locomotion** through the open ash-world, plus **one non-locomotion mechanism**: `MemoryDiveController.TeleportRig`, an instant position/rotation set that moves the *rig itself*, not an NPC.

| Entity | Spawn | Movement model | Notes |
|---|---|---|---|
| Kessler | (-1.5, 0, 3) | `StoryNpcWander`, radius 0.6 m | fixed for the whole chapter; no walker leg |
| Resh | (1.5, 0, 3) | `StoryNpcWander`, radius 0.6 m | fixed for the whole chapter — **but see the Beat 3–4 blocking gap below**: Resh's `ch5_beat4_release` line ("Do we take her with us") plays from this position, ~62 m and ~93% fogged from the grave (§9) |
| Mira | (0, 0, 4.5) | `StoryNpcWander`, radius 0.5 m | fixed for the whole chapter — **blocking contradiction**: at z=4.5 she spawns more +Z (grave-ward) than Kessler (z=3) or Resh (z=3), i.e. in front of the landing party rather than "shielded behind him" per canon (§4 Beat 0b, §9) |
| Vera Dusk | (0, 0, 66) | none — `wanderRadius: 0` | fixed in place from build time, same "waiting at the destination" convention as Tessa Rin (Ch4); **facing is unset** — `InstantiateNpc` never writes rotation, so she spawns at prefab-default +Z (facing away from the approach) rather than the canonical −Z toward it (§4 Beat 1b, §9); **posture is also fixed** — canon has her kneel in the ash at the dive's end (Beat 3) and rise before Beat 4, but the build's `StoryNpc` has no pose state and stands unchanged from spawn through `ChapterOutro` (§4 Beat 3a/4a, §9) |
| Iris, Mera Voss | — | none — voice-only | no physical `StoryNpc`; comm-line `DialoguePlayer` speaker labels only (§4, Beat 0) |
| Ghost_Villager0–3, Ghost_YoungerRonin, Ghost_Squad0/1 | dive-local, see §4 Beat 2 art table | none — static silhouettes | inert the whole dive; the younger-Ronin ghost's "advances on rails" description in the dialogue script is **not implemented** as motion — it is a single frozen figure, matching the "everyone here is a still silhouette" convention |
| Ghost_Kira | (0.5, 0, 187) | none — static | real mesh (`Kira-Dusk.prefab`), ghost-tinted |
| Player rig | scene-origin default (no explicit override) | continuous locomotion + snap-turn (real world) **+** two instant `MemoryDiveController.TeleportRig` calls (dive entry z=152, dive exit z=60) | the only entity that "travels" in the Ch1 sense — and even then, the two long-distance jumps (into and out of the dive) are teleports, not walked legs |

**Beat 3–4 blocking gap: the landing party never travels to the grave.** Canon stages Kessler/Resh/Mira at the graveside for the reckoning — "The landing party has come quietly closer... Kessler with Mira shielded behind him, and Resh" — and Resh has a live spoken line there, `ch5_beat4_release` step 17 ("Do we take her with us"). But per the table above, Resh's `StoryNpc` never leaves its Beat 0 spawn at (1.5, 0, 3); its `DialoguePlayer` anchor for that line is `Dialogue_Beat4_Release` (0,1,65) — ~62 m north, and at fog density 0.042, `exp(-0.042×62) ≈ 0.074` — roughly 93% obscured. The line plays as if spoken by an invisible NPC standing at the abandoned shuttle, not the man supposedly beside Vera. This is a genuine as-built inconsistency with both canon blocking and a shipped VO line, not just missing dressing — tracked as an open question in §9, since closing it means picking one of three directions: (a) give the landing party a walker/relocation leg to the grave for Beats 3–4 — the single deliberate exception to this chapter's "no `NpcWalker`" rule, and it should be documented as exactly that; (b) treat Resh's Beat 4 line as comm-relayed like Iris/Mera Voss — but Resh is canonically physically present, so that's a downgrade, not a fix; or (c) re-spawn/relocate the landing party near the grave for the back half of the chapter. **"The abandoned shuttle" itself is a phrase of convenience here, not a built object** — no dropship/shuttle prop exists anywhere in `Chapter5Builder.cs`; see the `Props.LandingShuttle` gap flagged in §4 Beat 0c and §9.

**Y-invariant (this chapter's equivalent of `kesslerFloorY`):** every spawn position and both dive teleport anchors sit at **y=0**, matching `AshGround`'s top face (center y=-0.5, scale y=1 → top at y=0) and `MemorySquare_Ground`'s top face (center y=-0.1, scale y=0.2 → top at y=0). Because no `NpcWalker` exists in this chapter, there is no waypoint-Y hazard to guard against the way Ch1's `KesslerToHoldWaypoints(floorY)` does — the equivalent risk here is a future `TeleportRig` target whose Y drifts off 0 (e.g. a poorly-placed `DiveEntryPoint`), which would drop or float the player the instant a dive Trigger fires. Any new reach point, dialogue anchor, or dive anchor added to this chapter should default to y=0 (or y=1 for eye-height dialogue anchors, matching the existing convention) unless there's a specific reason to do otherwise.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Accent lights | Behaviour | Ground/backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Cairn (briefing) | overcast, muted, waiting | `LandingEmber0/1` | `None` | `AshGround` visible from spawn; directional key + exponential fog established here for the whole real-world run | none — the sky/fog/ground is set once and never changes for the rest of the chapter's real-world beats |
| 1 — The Settlement / The Accusation | scoured, quietly wrong, then confrontational | `RuinsEmber0/1`, then `GraveEmber0/1` | `None` (ruins) / `AmbientPulse` 5s + 5.7s (grave) | same ground/fog throughout; no visual event | Vera Dusk comes into view as the player crosses the grave `ReachTrigger` — she was always present, not spawned on arrival. This is the fog doing the reveal (density 0.042: ~85% obscured at 46 m from `SettlementReachPoint`, ~29% obscured at 8 m from `GraveReachPoint`, §4 Beat 1b) — a deliberate reveal-timing value, not incidental, and worth protecting the same way as any other load-bearing constant in this table |
| 2 — Walking the Dead (dive) | desaturated, wrong, dreamlike | `MemoryLight_Entry/Mid/Well` (dive-local, parented under the dive root) | `None`, but the **whole scene's fog/ambient** flips via `MemoryFlashbackController` on entry and restores on exit | `AshGround` → `MemorySquare_Ground`; real-world fog (Exponential, 0.042) swapped for dive fog (ExponentialSquared, 0.045) and flat ambient (0.30,0.30,0.34) | **Trigger (step 7):** dive activates, treatment applies, rig teleports in. **Trigger (step 13):** dive deactivates, treatment restores, rig teleports out — the single biggest lighting/ambient state change in the chapter |
| 3 — The Ledger (reveal) | still, comm-relayed | `GraveEmber0/1` (unchanged from Beat 1) | `AmbientPulse` continues | unchanged — back on `AshGround` at the grave | none visual; the "reveal" is entirely audio (comm VO), no lighting cue marks it |
| 4 — The Reckoning | held, then released | `GraveEmber0/1` (unchanged) | `AmbientPulse` continues | unchanged until the very end — canon's "wind has thinned" open (§4 Beat 4f) has no build mechanism; the ambient bed neither thins nor rises | **Trigger (step 19):** `ChapterOutro` reveals the "CHAPTER 5 COMPLETE" canvas and fades to black via `ScreenFader` |

Fog is the same baseline exponential bed for the entire real-world run (Beats 0, 1, 3, 4) — a single profile value, never overridden per-zone. The dive (Beat 2) is the chapter's only lighting/fog departure, and it is a *global* RenderSettings swap, not a per-light change.

## 7. Audio/VO manifest cross-reference

All twelve dialogue sets are built via `Ch5BuildDialogue`, which calls `Chapter5Lines.Get(setId)` for the line data and `Ch5WireVoiceClips` to resolve each line's `AudioClip` from `Assets/Ronin7/Art/Generated/Audio/Voice/{clipName}.mp3` (falling back to `.wav`), where `clipName = Chapter5Lines.ClipName(setId, index, speaker) = "ch5_{setId}_{index:00}_{speaker_sanitized}"`. **Note the double `ch5_` prefix by construction:** every `setId` in `Chapter5Lines.SetIds` already starts with `"ch5_"` (e.g. `"ch5_beat0_briefing"`), and `ClipName` prepends another `"ch5_"` on top of it — so the actual filenames on disk are `ch5_ch5_beat0_briefing_00_meravoss.mp3`, not `ch5_beat0_briefing_00_meravoss.mp3`. This is internally consistent (the same pattern generates the lookup and the shipped files) but is a naming quirk worth fixing for legibility in any future refactor pass — it should not be "fixed" without also renaming the 53 already-generated clip files to match.

**Current resolution status (verified against `Assets/Ronin7/Art/Generated/Audio/Voice/`):** all **53/53** lines across all 12 sets already have generated `.mp3` clips on disk — full VO coverage exists for this chapter today; `Ch5WireVoiceClips`'s "only N/M resolved" warning will not fire on a fresh build.

| Set ID | Dialogue player | Anchor | Lines | ≈ sec | Clips resolved |
|---|---|---|---|---|---|
| `ch5_beat0_briefing` | `Dialogue_Beat0_Briefing` | (0,1,3) | 11 | 93 | 11/11 |
| `ch5_beat1_approach` | `Dialogue_Beat1_Approach` | (0,1,24) | 2 | 13 | 2/2 |
| `ch5_beat1_accusation` | `Dialogue_Beat1_Accusation` | (0,1,60) | 5 | 43 | 5/5 |
| `ch5_beat1_kira_named` | `Dialogue_Beat1_KiraNamed` | (0,1,62) | 5 | 47 | 5/5 |
| `ch5_beat2_dive_intro` | `Dialogue_Beat2_DiveIntro` | (0,1,157) | 2 | 28 | 2/2 |
| `ch5_beat2_counting_stock` | `Dialogue_Beat2_CountingStock` | (0,1,170) | 3 | 23 | 3/3 |
| `ch5_beat2_hesitation` | `Dialogue_Beat2_Hesitation` | (0,1,188) | 5 | 36 | 5/5 |
| `ch5_beat3_ledger` | `Dialogue_Beat3_Ledger` | (0,1,61) | 8 | 76 | 8/8 |
| `ch5_beat4_demand` | `Dialogue_Beat4_Demand` | (0,1,63) | 3 | 36 | 3/3 |
| `ch5_beat4_verdict` | `Dialogue_Beat4_Verdict` | (0,1,64) | 4 | 50 | 4/4 |
| `ch5_beat4_release` | `Dialogue_Beat4_Release` | (0,1,65) | 3 | 28 | 3/3 |
| `ch5_beat4_hook` | `Dialogue_Beat4_Hook` | (0,1,66) | 2 | 21 | 2/2 |
| **Total** | | | **53** | **≈494 s (~8.2 min)** | **53/53** |

**≈494 s is summed clip length, not playthrough time.** Every set above advances line-by-line on Left-Hand Talk (Y), so actual playtime is player-gated and materially longer than the raw sum — plus ~120 m of round-trip real-world locomotion (landing to grave and, for the landing party's line, back) and the dive's two teleport-bounded traversal legs. Don't read the 8.2-minute figure as the chapter's wall-clock length in a pacing or profiling pass (§1.6).

**Non-dialogue audio:**

| Object | Type | Position | Params |
|---|---|---|---|
| `AshWorldGardenAmbience` | `ProximityAmbienceLayer` | (0, 1.5, 40) | inner 6 m / outer 30 m / max vol 0.4 — theme inferred `GardenWind` by `ProceduralAudioClipBuilder.AssignGeneratedClips()`; the name contains "garden", so `GardenWind` resolves — but it's a fragile substring match, not an explicit theme param (see §9) |
| `GhostVillageDreadAmbience` | `ProximityAmbienceLayer` | (0, 1.5, 170) | inner 6 m / outer 25 m / max vol 0.4 — theme inferred `DreadDrone` from the "dread" substring |
| *(no object — gap)* | dive dread-bed decay ("the square empties… the voices going out one by one") | dive-wide | **not built.** Canon's Beat 2 recording audibly drains as the massacre progresses (dialogue script lines 257, 292), but `GhostVillageDreadAmbience` plays at constant volume for the whole ~35 m dive walk (entry z=152 to the well z=187) — nothing thins or decays it as the player advances, so canon's "emptying" isn't audible. See §9. |
| *(no object — gap)* | dwell/idle Echo barks between the dive's three scripted sets | dive-wide, z 152-187 | **not built.** The dialogue script's Beat 2 PRODUCTION NOTE (lines 259-260) asks for idle/ambient Echo lines that fire on dwell to keep the dread alive between the position-triggered anchors; only the three scripted `DialoguePlayer` sets exist (§4 Beat 2e/2f), so a lingering player gets silence between them. |
| *(no object — gap)* | ash-underfoot footstep surface | player-following | **not built.** The dialogue script's SETTING block puts the player "ankle to the ash" for the entire ~60 m walk from landing to grave; Ch1's equivalent chapter carries an `OnFootAmbience` bed, but Ch5's audio layer is two positional wind/dread beds and nothing tied to the player's own footfalls. Cheap, high-value, and unbuilt — see §9. |
| *(no object — gap)* | "the creak of dead structures" | ruins-wide | **not built.** Beat 1's SETTING lists three atmospheric layers — "wind, ash-drift, the creak of dead structures" — and only wind (`AshWorldGardenAmbience`) is represented; no intermittent structural-creak layer exists over the ruins. See §9. |
| `MemoryFlashbackController`'s optional heartbeat loop | `AudioSource` (2D) | dive root | not wired in `Chapter5Builder.cs` today — the `heartbeatLoop` field is left null, so no heartbeat plays during the dive. Worth an explicit additive suggestion, not just a neutral status note: Beat 2's hesitation (§4 Beat 2f) is deliberately carried with zero haptics, and a low heartbeat rising into the frozen half-second is the one non-shake "juice" channel already supported by this frozen shared component — opt-in, profile-eligible, same treatment as the ash VFX gap in §9. |
| *(no object — gap)* | comm-crackle onset for the Beat 3 reveal ("the comm at Ronin's collar crackles," line 298) | at step 14 (`ch5_beat3_ledger` start) | **not built.** Iris/Mera's reveal (§4 Beat 3f) begins as a plain dialogue step over unchanged grave ambience; canon marks the comm opening with an audible crackle the instant Iris and Mera finish cross-referencing. A short static/crackle SFX at the head of the set is cheap and canon-cited. |

## 8. Build & verification checklist

1. Run **Tools → Space Samurai → Chapters → Build Chapter 05 — The Debt of Ashes**; confirm the console log ends with `"Opens Act II; sets ch5_complete."` and no errors.
2. Confirm `Ch5WireVoiceClips` logs **no** `"only N/M voice clips resolved"` warnings — all 53 lines across 12 sets should resolve on a clean asset database (§7).
3. Confirm `AshWorldGardenAmbience` resolved to `GardenWind` (not a default `HangarHum`) after `ProceduralAudioClipBuilder.AssignGeneratedClips()` runs — the substring-match theme inference is fragile (§7, §9).
4. Walk the real-world spine start to finish: landing (z≈0-16) → `SettlementReachPoint` (z=20) → ruins → `GraveReachPoint` (z=58) → grave. Confirm Vera Dusk is visible and stationary at (0,0,66) and both embers pulse independently (5 s / 5.7 s periods, visibly out of phase). Confirm Vera Dusk and `IDChit_Label` face **−Z**, toward the approach (not the prefab-default +Z) — the doc's two highest-priority sightline fixes (§4 Beat 1b/1c, §9).
5. Trigger the dive (step 7) and confirm: `MemoryDive_Massacre` activates, fog/ambient visibly shift (heavier, greyer, `ExponentialSquared`), the rig teleports instantly to (0,0,152) with no camera judder, and `AshGround`/the real-world props are no longer visible (they're behind/below, not deleted — confirm nothing bleeds through).
6. Walk the dive lane to `DiveNearGirlReachPoint` (z=188); confirm `Ghost_Kira` renders with the translucent ghost material identically to the procedural silhouettes despite using a real mesh. Confirm `Ghost_YoungerRonin`/`Ghost_Kira` sit **ahead of** the player (+Z) at the hesitation dialogue anchor, not behind it (§4 Beat 2b, §9).
7. Trigger dive exit (step 13); confirm the rig teleports back to (0,0,60), fog/ambient restore to the pre-dive real-world values exactly (not just "close" — `MemoryDiveController` snapshots and restores precisely), and `MemoryDive_Massacre` deactivates.
8. Play through Beats 3–4 to the `ChapterOutro` trigger (step 19); confirm the "CHAPTER 5 COMPLETE" canvas reveals, the scene fades via `ScreenFader`, and `ZoneCompleted` publishes (check `console-get-logs` or a `GameFlowManager` breakpoint) — this is what advances the campaign to Ch6.
9. Confirm `ch5_complete` is actually set on the save/campaign-flag system post-fade (`CampaignFlagSetter.SetFlags` fired via the `OnActivated` persistent listener).
10. Run the EditMode suite; confirm the chapter's own fixtures pass and the global baseline count matches whatever the ledger currently records (Ch5 contributed **457 tests, 0 failed, 3 skipped** as of the 2026-07-03 PASS entry in `CHAPTER-BUILD-LEDGER.md` — re-verify against the *current* baseline, not this historical number, before treating a mismatch as a regression).
11. **Before starting the art migration:** capture a `UnityStats` draw-call/tris/verts baseline for this scene (both dive-inactive and dive-active states) and record it in §1.6 and the ledger — none exists yet (§1.6).

## 9. Additive-only cautions & open questions

- **Vera Dusk's facing is a build bug, not a documented fact.** `Ch5PlaceStoryNpc(Ch5VeraDuskPrefab, (0,0,66), "Vera Dusk", 0f)` never sets a rotation, so she spawns at prefab-default +Z — facing away from a player who arrives from −Z — for the chapter's central face-to-face confrontation (§4 Beat 1b, §5). Fix by adding a rotation parameter to that call (`Euler(0,180,0)`, facing −Z toward the approach). This is the single highest-priority item in this section: it's the most important sightline in the level and it's currently wrong.
- **The same unset-rotation bug hits `IDChit_Label` too — the chit's "KIRA" text reads backwards.** `IDChit_Label` (`Chapter5Builder.cs:410-419`) never gets a rotation, so it sits at TextMesh-default +Z facing away from the −Z approach, same root cause as Vera's facing and the landing party's (§4 Beat 0b). It's a smaller fix (one `Quaternion.Euler(0,180,0)` line, with in-file precedent at `Ch5BuildCompleteCanvas`, `:552`) but it lands on the level's single most name-legible, canon-load-bearing prop (§4 Beat 1c) — worth batching with the Vera fix rather than fixing in isolation.
- **Mira spawns in front of the landing party, not shielded behind Kessler.** Canon repeatedly stages her "shielded behind him" / "kept close at Kessler's side," but `Ch5PlaceStoryNpc` puts Kessler/Resh at z=3 and Mira at z=4.5 (`Chapter5Builder.cs:141-143`) — the most +Z, grave-ward member of the trio, ahead of both adults (§4 Beat 0b, §5). A small blocking fix, not a new system: Mira's z should be *less* than Kessler's (e.g. (-1.5, 0, 2)) to read as tucked behind him rather than out front.
- **The landing party never travels to the grave for Beats 3–4, but Resh speaks from there.** Kessler/Resh/Mira are fixed at their Beat 0 spawn (z=3–4.5) for the entire chapter (§5) — canon stages them "quietly closer" at the graveside by the reckoning, and Resh's `ch5_beat4_release` line ("Do we take her with us") is voiced from a `DialoguePlayer` anchor 62 m from an NPC that's ~93% fogged out at that distance (§5). This is a structural blocking contradiction, not a dressing gap — as-built, Beats 3–4 play with an invisible landing party. It ranks with the Vera-facing bug as the chapter's most important open blocking question: needs a decision between a deliberate walker/relocation exception, a downgrade to comm-relayed, or a mid-chapter re-spawn, before it's closed. **Whichever path is chosen, canon has a specific pose to honor, not just a position:** Beat 3 stages Kessler as deliberately "keep[ing] Mira turned away" during the reveal (line 298), a protective facing distinct from Beat 0's "kept close." If the fix ends with Kessler/Mira staged at the graveside, Mira should be turned away from the grave, not merely present there — the eventual relocation shouldn't place a child staring into the mass grave that canon explicitly shields her from.
- **The hesitation reach-point overshoots the figures it's supposed to frame.** `DiveNearGirlReachPoint`/the hesitation dialogue anchor (0,1,188) sit at or past `Ghost_YoungerRonin` (0,0,182) and `Ghost_Kira` (0.5,0,187) on the player's approach line from `DiveEntryPoint` (0,0,152); by the time Echo says "Watch his hand… Not the blade. The hand," both load-bearing figures are behind the player (§4 Beat 2b). This is the chapter's *second* "most important sightline is currently backwards" case, alongside Vera Dusk's facing above — fix by moving the reach-point/anchor to ~z=180.
- **Vera's two hand-props — the ledger and the drawn weapon — don't exist, and they resolve differently at the close, not identically.** Canon stages both as physical and load-bearing through Beats 1 and 4 (ledger in one hand, weapon aimed at the player) — but only the ledger is set on the cairn: "lay the ledger on the cairn… both hands, like setting down a stone" (line 372). The weapon's own closing beat is separate and stays in her hand: "She lowers the weapon all the way" (line 372, same beat, different sentence) — lowered, never placed on the cairn. Keep the two hand-props' closing gestures distinct; the weapon's fuller four-state arc (not-quite-level → dropped in the ash at the kneel → recovered and level → lowered) is tracked in §4 Beat 1c. Neither prop has a registry key yet; `Props.VeraLedger` and the held weapon are tracked as new **MISSING** rows in §4 Beat 1c/4c and Appendix B. Until built, the accusation and the reckoning both play with an empty-handed NPC and no visible weapon to lower or ledger to lay down. **`Props.VeraLedger` also has an uncaptured detail once commissioned:** dialogue-script line 188 puts "a worn ID-chit pressed flat against its cover under her thumb" on the ledger itself — the same KIRA-chit motif as the cairn's `IDChit_Kira`, echoed in her hand rather than fixed at the grave (§4 Beat 1c).
- **Vera's pose arc is unimplemented, and it's four beats, not two.** Canon moves her through a deliberate forward step mid-accusation in Beat 1 ("She steps forward through the ash, the ledger out now," dialogue script line 188), a collapse to her knees at the dive's end (Beat 3), a rise again before Beat 4, and finally laying the ledger on the cairn at Beat 4's close (§4 Beat 1c/3a/4a/4c, §5) — the largest visual grief beat in the chapter's back half. The build's `StoryNpc` (`wanderRadius: 0`) has no pose state and stands unchanged from spawn through `ChapterOutro`. Open question for a future pass: is a four-state pose swap (step-forward → kneel → rise → lay-ledger, driven off mission-spine steps 5→12→13→15→19) worth building, given the reveal currently plays over a woman standing impassively throughout?
- **Nothing locks player control for the hesitation.** Canon stages Beat 2's reach-the-girl moment as "[CUTSCENE — ON REACHING THE GIRL: player control locks]," but the build's step 12 is a plain dialogue step — continuous locomotion stays live through the whole of `ch5_beat2_hesitation`, so the player is free to walk off and miss the chapter's core beat entirely (§4 Beat 2f). A comfort-safe locomotion freeze (stick input only, not the camera) for the line's duration would close this without touching the no-camera-shake rule; absent that, this is canon's stage direction silently going unimplemented.
- **"Watch his hand" has no hand — and no blade to contrast it against either.** `Ghost_YoungerRonin` (§4 Beat 2c) is built from the shared `Ch5BuildGhostFigure` template — capsule `Body` + sphere `Head` only, no arm or hand geometry — yet the chapter's core line, `ch5_beat2_hesitation` L1, is Echo's full contrast *"Watch his hand. Forget the blade. The hand,"* and canon separately stages the younger self "advancing… with a wrapped blade" (line 235); Ronin's own following line hinges on the same image. This is a staging hole on the beat the doc repeatedly calls load-bearing, not a nitpick: recommend a single posed arm/hand element added to `Ghost_YoungerRonin` specifically, holding a ghost-tinted wrapped blade (mid-rise, ghost-material, static — not a rework of the shared silhouette helper) so both halves of Echo's A-vs-B cue have a referent to move the gaze between, not just one.
- **Ash is stated in dialogue but not sensed.** No footstep audio layer exists for the ~60 m ash-underfoot walk, and no "creak of dead structures" ambience layer exists over the ruins, despite both being named explicitly in the SETTING block alongside the wind bed that *is* built (§3, §4 Beat 1f, §7). This is cheap, high-value, and — together with the missing ash-drift VFX below — the largest remaining gap between the chapter's stated atmosphere and what a VR player actually perceives.
- **No drifting-ash particle system exists.** The chapter's title and central atmospheric image — ash in slow grey curtains, underfoot and in the air — has zero VFX representation today; the builder's own comment explains this was skipped rather than adding new particle infrastructure for one chapter. This is the single highest-value additive VFX gap in the chapter. Any fix should land as a new, reusable `ChapterSharedBuilders` helper (an ash-drift particle system parameterized by density/wind), not a Ch5-only one-off, since Ch5's own doc comment already anticipates other chapters wanting the same treatment. If/when that helper is built, the `ChapterOutro` fade (step 19) is its single highest-value instance in this chapter: canon's closing stage direction is specific — "the grey wind carries the ash up in a slow curtain between the crew and the grave, and does not let it settle. SMASH TO BLACK." — a denser curtain rising between the player and the grave at the exact moment the chapter smash-cuts to black would close Ch5 on its own title image, turning an open-ended "build it someday" into one concrete, canon-cited placement.
- **The dive's combat scope is narrower than its own source material.** Both the beat treatment (*"limited combat"*) and the dialogue script's stage direction (*"limited, dreamlike combat — the player cannot stop the massacre, only walk its length"*) describe some player agency to engage inside the memory; the shipped builder deliberately implements **zero** `Enemy` components anywhere in the dive, per its own class-doc rationale ("NO sentinels and NO violence depicted"). This reads as an intentional tonal choice (violence *depicted* would undercut "he cannot change what he's watching"), but it is a real divergence from two of the four source documents and should be confirmed with the narrative owner before either side is "corrected."
- **No shuttle exists at the landing site, despite canon and this doc's own §5 treating one as present.** Canon opens Beat 1 and closes Beat 4 on "the shuttle's ramp lowers"; §5's Beat 3–4 blocking note refers to "the abandoned shuttle" as a landmark. `Chapter5Builder.cs` builds no dropship anywhere — the landing cluster sits on bare `AshGround`. `Props.LandingShuttle` is tracked as a new **MISSING** key in §4 Beat 0c and Appendix B; it's both the hero prop that makes the landing site read as a landing site and the referent the chapter's opening image needs.
- **The mass grave has marker stones but no scar in the ground.** Canon names the grave itself as physical geometry four times — "a long low scar in the ground," "the head of a long low scar," "a long low scar in the ash with the ledger laid on it" — but the build represents it purely as a 3×4 grid of small (0.3,0.3,0.15) `GraveMarker` cubes over unbroken flat `AshGround` (`Ch5BuildMassGrave`, `Chapter5Builder.cs:385-392`); there is no trench, depression, or dark ground decal. It reads as twelve marker stones on level ash, not a mass grave. A `Props.GraveScar` (a long, low, darker trench/decal running under the marker grid, roughly z 52–64) is tracked as a new **MISSING** key in §4 Beat 1c and Appendix B — it's the defining silhouette of the chapter's destination, currently absent, and cheap relative to its value: it's what the cairn, the markers, and Vera are all arranged around.
- **Segment 0's ship-interior staging is unbuilt.** The dialogue script's SETTING block describes Beat 0 as playing in *"the Cairn, the command room, more of its dead bridge lit than a chapter ago"* — an interior space with its own holo-table/lighting identity, distinct from Ch1's Command Room. `Chapter5Builder.cs` builds Beat 0 as a plain landing-site exterior spawn (the same `AshGround`/ember lighting as the rest of the chapter), with no ship-interior geometry at all. This is consistent with the CREW-PRESENCE DECISION's "no body in the scene for a two-line presence" logic extended one step further (no *room* either), but it means a player who expects to see "the Cairn's bridge" at the chapter's start will instead see the ash-world immediately. **This isn't only a cosmetic gap — the shipped Beat 0 VO itself contains broken deixis without it.** Echo's line "I'll say the quiet part, since nobody at that table will" (`ch5_beat0_briefing`, §4 Beat 0e) and Mera/Iris's reading of the kill-record and the operative-of-record name both assume the holo-table tableau described in the SETTING block; played on open ash with no table, "that table" has no referent in the built scene. Flag for a narrative-design decision: is a lightweight Cairn-interior vignette worth building for Beat 0, or is the exterior-only open consistent with the chapter's "we're already past the briefing, already committed" pacing? If this vignette is ever built, canon gives it one specific, buildable focal prop that Ronin's own blocking references: a salvaged holo-table throwing up "two overlaid record-sets… where the two sets meet, one settlement burns brighter than the rest," which Ronin stares at through his silence — worth building that specific readable image (a holo-table with a single glowing settlement marker), not just a generic room, and at minimum the prop the shipped dialogue's own references need to land on.
- **`AshWorldGardenAmbience`'s theme inference may be wrong.** `ProceduralAudioClipBuilder.AssignGeneratedClips()` infers its ambience theme from the object's own name — containing "garden"/"wind" picks `GardenWind`; "dread"/"throne"/"vault" picks `DreadDrone`; anything else defaults to `HangarHum`. `"AshWorldGardenAmbience"` contains "garden" (so it should correctly resolve to `GardenWind`), but this is a fragile substring match tied to a naming accident, not an explicit theme parameter — worth confirming the resolved clip actually sounds like the intended "wind over ash," not a default hangar hum, the next time this scene is opened in-editor.
- **Vera Dusk ships as a greybox placeholder**, not final art (§4, Beat 1c) — flagged there, repeated here since it's a visible, load-bearing character for the chapter's entire back half.
- **`Doors.SlidingDoor_Standard`-style shared door infrastructure has no equivalent need in this chapter** — confirmed no doors anywhere in the design; this is expected, not a gap, given the chapter is exterior-only end to end.
- Everything under §1.5's fallback rule applies here exactly as it does everywhere else: **do not delete or "clean up" the primitive props while they're still load-bearing.** A registry-driven pass that resolves `Props.CollapsedWallShell` etc. must go through the same `if (prefab == null) { fallback }` guard as Chapter 1, never a hard cutover.

## Appendix A — As-built primitive fallback

`Chapter5Builder.cs` is a single monolithic method, `BuildChapter5DebtOfAshes()` (~250 lines), with no `BuildBeatNArt`/`BuildBeatNLogic` split. This appendix records its actual literals, positions, tints, and hierarchy as they exist today — the primitive fallback path §1.5 requires stays correct.

**Hierarchy (top-level roots under the scene, build order):**

```
Directional Light                  — Light(Directional), color(0.75,0.74,0.72), intensity 0.6, rot Euler(60,-40,0)
LandingEmber0/1, RuinsEmber0/1,
GraveEmber0/1                      — 6× standalone Light(Point), shadows=None (BuildAccentPointLight)
AshWorld
 ├─ AshGround                      — Cube, local pos (0,-0.5,45), scale (40,1,100), tint (0.12,0.11,0.11)
 ├─ CollapsedWallShell ×6          — Cube, tint (0.15,0.13,0.12) ("burned")
 ├─ BurnedStall0/1/2               — Cube, tint (0.08,0.07,0.07) ("charred")
 ├─ Scavenger_Silhouette0/1        — Cube, tint (0.2,0.18,0.17) ("scavenger")
 ├─ GraveMarker_{row}_{col} ×12    — Cube, scale (0.3,0.3,0.15), tint (0.3,0.29,0.28) ("stone")
 ├─ Cairn                          — Cube, local pos (0,0.4,64), scale (1,0.8,1), tint (0.35,0.32,0.28)
 ├─ IDChit_Kira                    — Cube (collider stripped), scale (0.15,0.02,0.1), unlit mat (0.95,0.9,0.75)
 └─ IDChit_Label                   — TextMesh "KIRA", pos (0,1.1,64), fontSize 48, color (0.9,0.88,0.8)
MemoryDive_Massacre  [SetActive(false)]
 ├─ MemoryFlashbackController      — fogColor(0.35,0.37,0.42) density 0.045 (ExpSq), ambient(0.30,0.30,0.34)
 ├─ MemorySquare_Ground            — Cube, local pos (0,-0.1,175), scale (18,0.2,50), tint (0.26,0.27,0.28)
 ├─ MemoryStall0/1/2               — Cube, tint (0.32,0.32,0.34)
 ├─ MemoryWell                     — Cylinder (collider stripped), pos (1,0.4,186), scale (0.6,0.4,0.6)
 ├─ Ghost_Villager0..3              — Capsule(Body)+Sphere(Head), MakeGhostMaterial(), colliders stripped
 ├─ Ghost_Kira                     — Kira-Dusk.prefab instance, FitNamedCharacter, renderers→ghost material
 ├─ Ghost_YoungerRonin              — Capsule+Sphere, ghost material
 ├─ Ghost_Squad0/1                  — Capsule+Sphere, ghost material
 ├─ MemoryLight_Entry/Mid/Well      — Light(Point), shadows=None, parented under the dive (Ch5BuildPlaybackLight)
 └─ DiveEntryPoint                 — empty transform, pos (0,0,152)
Game                                — GameState, CombatFeedbackController
[rig]                               — BuildRig(addLocomotion:true) + EchoPresence + ZoneBounds(center(0,0,100),r=115)
Sword (Echo visual)                 — pos (2,1,1), Euler(-90,0,0)
Kessler / Resh / Mira                — Named prefabs + StoryNpc + StoryNpcWander (0.6/0.6/0.5 m)
Vera Dusk                            — Vera-Dusk.prefab (placeholder greybox) + StoryNpc, wanderRadius 0
MemoryDive                          — MemoryDiveController
DiveExitPoint                       — empty transform, pos (0,0,60)
EnterDiveTrigger / ExitDiveTrigger  — [SetActive(false)], MemoryDiveEntryTrigger / MemoryDiveExitTrigger
SettlementReachPoint / GraveReachPoint /
DiveLaneReachPoint / DiveNearGirlReachPoint  — empty transforms, (0,1,20) (0,1,58) (0,1,165) (0,1,188)
Dialogue_Beat0_Briefing … Dialogue_Beat4_Hook  — 12× DialoguePlayer (see §7)
CHAPTER 5 COMPLETE Canvas           — [SetActive(false)], world-space Canvas, pos (0,1.4,70)
ChapterOutro                        — [SetActive(false)], pos (0,1,68), CampaignFlagSetter("ch5_complete") + ChapterOutro
Mission                             — MissionDirector, 19 steps (see §4 per-beat tables)
```

**Ground plane math (Y-invariant source):** `AshGround` local pos y=-0.5, scale y=1 → top face at world y = -0.5 + 0.5×1 = **0**. `MemorySquare_Ground` local pos y=-0.1, scale y=0.2 → top face at world y = -0.1 + 0.5×0.2 = **0**. Both floors are exactly level with each other and with every spawn/anchor position in the chapter — the single Y-invariant this chapter's builder depends on (§5).

**`CollapsedWallShell`'s coupled scale quirk:** `BuildProp(parent, "CollapsedWallShell", pos, new Vector3(2.2f, pos.y * 2f, 0.4f), burned)` — the prop's own Y-scale is derived from its spawn position's Y literal (0.4–0.8), not an independent height parameter. This produces the intended varied-height rubble today but has no meaning to preserve once real meshes replace these cubes (§4, Beat 1c).

**A.1 — `ChapterEnvironmentProfile` literals to lift into `Ch5Environment.asset`:**

| Field | Value |
|---|---|
| Key light | color (0.75,0.74,0.72), intensity 0.6, rotation Euler(60,-40,0) |
| Ambient | Flat, (0.16,0.15,0.15) |
| Fog (real world) | Exponential, (0.5,0.48,0.46), density 0.042 |
| Fog (dive) | ExponentialSquared, (0.35,0.37,0.42), density 0.045 |
| Ambient (dive) | Flat, (0.30,0.30,0.34) |
| `LandingEmber0` | (-4,1.6,6), (1,0.45,0.2), intensity 1.2, range 10 |
| `LandingEmber1` | (4,1.6,10), (1,0.5,0.25), intensity 1.2, range 10 |
| `RuinsEmber0` | (-5,1.8,26), (0.9,0.4,0.15), intensity 1.4, range 12 |
| `RuinsEmber1` | (5,1.8,36), (0.9,0.4,0.15), intensity 1.4, range 12 |
| `GraveEmber0` | (-4,1.6,58), (0.6,0.55,0.6), intensity 1, range 12, `AmbientPulse(5s)` |
| `GraveEmber1` | (4,1.6,68), (0.6,0.55,0.6), intensity 1, range 12, `AmbientPulse(5.7s)` |
| `MemoryLight_Entry` | (0,2.6,158) local, (0.55,0.58,0.65), intensity 1, range 12 |
| `MemoryLight_Mid` | (0,2.6,174) local, (0.5,0.5,0.56), intensity 0.8, range 12 |
| `MemoryLight_Well` | (0.5,2.6,187) local, (0.6,0.62,0.7), intensity 1.1, range 12 |

## Appendix B — ArtAssetRegistry key inventory

| Key | Chapter usage | Status |
|---|---|---|
| `Terrain.AshGround` | real-world ground plane | **MISSING** |
| `Terrain.MemorySquareGround` | dive ground plane | **MISSING** |
| `Props.LandingShuttle` | the landing site's grounded dropship, the "ramp lowers" referent | **MISSING — key not yet defined** |
| `Props.CairnHoloTable` | the Beat 0 ship-interior vignette's focal prop — a holo-table throwing up two overlaid record-sets with one settlement burning brighter than the rest, the referent Echo's "nobody at that table" line assumes (§9) | **MISSING — conditional on the Beat 0 vignette decision** |
| `Props.CollapsedWallShell` | 6× settlement ruin fragments | **MISSING** |
| `Props.BurnedStall` | 3× burned market-stall debris | **MISSING** |
| `Props.ScavengerSilhouette` | 2× decorative scavenger silhouettes | **MISSING** |
| `Props.GraveScar` | the trench/decal under the marker grid the grave's silhouette turns on | **MISSING — key not yet defined** |
| `Props.GraveMarker` | 12× mass-grave marker stones | **MISSING** |
| `Props.Cairn` | the grave-head cairn of scrap and chits | **MISSING** |
| `Props.IDChit` | Kira's ID-chit + name label | **MISSING** |
| `Props.MemoryMarketStall` | 3× dive-interior market stalls | **MISSING** |
| `Props.MemoryWell` | the well Kira kept her count beside | **MISSING** |
| `Props.VeraLedger` | Vera's held ledger, Beats 1 and 4 (laid on the cairn at Beat 4's close) | **MISSING — key not yet defined** |
| *(no key assigned)* | Vera's held/drawn weapon, Beats 1 and 4 | **MISSING — key not yet defined** |
| `Props.OpenDoorframe` | standing doorframe/threshold, matches Echo's "doors all open" bark | **MISSING — key not yet defined** |
| `Props.KiraSlate` | Kira's slate, ghost-tinted, beside `Ghost_Kira` at the well | **MISSING — key not yet defined** |
| `Props.MemoryDoorway` | ghost-tinted doorway just past `Ghost_Kira`, the threshold she doesn't reach | **MISSING — key not yet defined** |
| `Named.Kessler` | landing party | **EXISTS** |
| `Named.Resh` | landing party | **EXISTS** |
| `Named.Mira` | landing party | **EXISTS** |
| `Named.Echo` | the katana's visual mesh | **EXISTS** |
| `Named.KiraDusk` | dive-only, ghost-tinted at build time | **EXISTS** (real baked mesh) |
| `Named.VeraDusk` | grave confrontation, present the whole back half | **EXISTS** (placeholder greybox only — see §9) |
| *(no key — procedural by design)* | `Ghost_Villager0-3`, `Ghost_YoungerRonin`, `Ghost_Squad0/1` | **BY DESIGN**, not a registry candidate (§1.5) |

**Summary: 10 environment keys, all MISSING; 6 character keys, all EXISTS (one real-baked, one placeholder-greybox, four production-ready); 7 dive figures deliberately excluded from the registry; plus 4 gaps a prior review pass surfaced (`Props.VeraLedger`, `Props.OpenDoorframe`, `Props.KiraSlate`, and Vera's held weapon), 2 more a later pass added (`Props.LandingShuttle`, `Props.GraveScar`), and one provisional key this pass adds (`Props.CairnHoloTable`, tracked only if the Beat 0 ship-interior vignette is ever greenlit — see §9) — the first 6 are canon-required and unconditional, the 7th is conditional but inventoried here rather than left to be rediscovered — tracked here so an art pass commissions them alongside the other 10, not after.** This chapter has no `Doors.*`, `Rooms.*`, or `Enemies.*` keys at all — the first entirely exterior, doorless, combat-free chapter in the sequence documented this way. (`Props.OpenDoorframe` is deliberately not a `Doors.*` key — it's a static threshold with no door leaf and no lock state, not mission-gating geometry; see §4 Beat 1c.)

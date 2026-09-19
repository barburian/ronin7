# Chapter 9 — Scene Construction

*The architectural contract for `Ch09_PitAndTheDeep.unity`: what Chapter 9 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 9 ("The Pit and the Deep") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from. It is the Act III opener — Gryph (Ally #5), Sable (Ally #6), the Vane/Wraith-6 boss duel, and the Overdrive unlock all land here.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter9Builder.cs`, entry point `XRRigBuilder.BuildChapter9PitAndTheDeep()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 09 — The Pit and the Deep**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch09_The_Pit_and_the_Deep.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`) and the dialogue data module `Chapter9Lines.cs`. *(Source hygiene note: the beats file's own KEY SCENES block mis-names the boss "VESPER" once — "VESPER goes down in the black water" (`Ch09_The_Pit_and_the_Deep.md:71`) — a stray typo for **Vane**. Noted here so it doesn't propagate into a prefab or registry key; nothing in `Chapter9Builder.cs` or the registry uses "Vesper." The same KEY SCENES block also carries an **earlier-draft Confrontation exchange** that is not the authored VO — "Wraith-6. The make they built mine from. You don't have to stand for them." / "I don't stand. I'm aimed. *(raises blade)* The asset stays." (`Ch09_The_Pit_and_the_Deep.md:62-66`) — superseded by the shipped `ch9_beat3_confrontation` set (Sable "Don't come for me… he's always between," Vane "The asset is correct… Turn, or be kept," §Beat 3e). Flagged for the same reason as the VESPER typo: a future VO or narrative-reference pass reading the beats file cold should pull from `Chapter9Lines.cs`, not this draft block.)*
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The Coil-raid defense (Beat 2) and the Vane/Wraith-6 boss duel (Beat 3) are this chapter's two combat encounters, and both carry their impact feedback through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never the camera. This applies equally to the Overdrive time-dilation burst: the world visibly slows, the camera never shakes or punches in.
- **Traversal in Ch9 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/movement. There is **no teleport locomotion, no NavMesh, no scripted parkour/climb/wall-run rig for the player**, and — a deviation worth flagging explicitly — **no doors anywhere in this chapter** (see §2). The narrative describes rappelling, controlled falls, and underwater diving, but the as-built scene realizes all of it as a flat +Z walk gated by `ReachTrigger` mission steps, not by any door, ledge, or swim mechanic. Do not introduce teleport, NavMesh, or a bespoke climb/dive rig when patching this scene without a design sign-off — that would be new mechanic surface, not a like-for-like art pass.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | zone shells, props, VFX, backdrops, decorative lights, and any inert/decorative character placement (e.g. Sable, built active with no combat behavior — mirrors Ch1's treatment of the inert Khall hologram) | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | NPC/enemy spawns with behavior (Gryph, Rook, Vane), reach points, wave spawners, ability granters, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**There is no door idiom to split in this chapter** — Ch9 has zero `BuildSlidingDoor` calls (confirmed: no lock/unlock state exists anywhere in `Chapter9Builder.cs`). Every gate is a `ReachTrigger` mission step instead. Where Ch1's contract calls out "the one object that spans both is a door," Ch9's equivalent is **the boss**: `BuildBeat3Art()` instantiates Vane's inactive `GameObject`; `BuildBeat3Logic()` owns the `AuthorDefeatStep` wiring that calls `SetActive(true)` on him at runtime (via `MissionDirector.BeginDefeatEnemies`, not a separate `Trigger` step — see §4, Beat 3).

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildWaveSpawner`, `Author*Step`, `AttachPlayerAbilities` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, including `AllyCombatant`, `FloodingWaterHazard`, `AbilityGranter`, and `EnemyWaveSpawner` (all shared components, not Ch9-local).
- **Free to restructure:** the Ch9-local helpers, prefixed `Ch9` and called only from `BuildChapter9PitAndTheDeep()` — `Ch9EnsureCoilRaiderDefinition`, `Ch9EnsureVaneDefinition`, `Ch9BuildDialogue`, `Ch9WireVoiceClips`, `Ch9PlaceStoryNpc`, `Ch9PlaceAlly`, `Ch9BuildVane`, `Ch9BuildHoldRackRow`, `Ch9BuildShadowRackRow`, `Ch9BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 9 and are now silently rebuilding thirteen other chapters.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** The same two ScriptableObjects introduced by the Ch1 refactor carry everything Ch9's builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch9Environment.asset` | directional key, ambient mode + color, fog mode/color/density, per-zone floor + ceiling tint, ten accent lights, `ConsoleFlicker`/`AmbientPulse` behaviours |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document — **the same shared registry instance as every other chapter**, extended with Ch9's new keys (§Appendix B) |

Both assets are the ones defined by the Ch1 refactor (`Assets/Ronin7/Data/` already holds `EnemyDefinition`, `WeaponDefinition`, `ZoneDefinition`, `Ch9CoilRaider.asset`, and `Ch9Vane.asset` instances). Neither `ArtAssetRegistry` nor `Ch9Environment.asset` exists yet, confirmed by repo search (`class ArtAssetRegistry` / `class ChapterEnvironmentProfile` match zero files).

Prefab **paths never appear in builder code.** The builder asks the registry for `Props.SalvageRack`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`.** Ch9 reuses the Characters3D/Named and Characters3D/Enemies folders that already exist (Gryph, Rook, Sable, Echo, and the enemy art wired additively — see §Appendix B) and needs new environment folders as siblings of them:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today
  Rooms/                                    ← exists (Ch1's refactor target; Ch9 adds RustfangHoldHallShell etc.)
  Props/                                    ← exists; Ch9 adds SalvageRack, ShadowRack, SealedProgramWall, FreightCradle, EngineScaffold, SableInterfaceConsole
  VFX/                                      ← exists; Ch9 needs a water-surface effect that does not exist even as a concept yet (§Appendix B)
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve," identically to Ch1's finding.**
>
> `BuildChapter9PitAndTheDeep()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter9Builder.cs:91`). That call **discards the entire scene** and opens a fresh empty one — there is nothing left to search for. Making the safe zone real requires **replacing the wipe strategy**:
>
> 1. `EditorSceneManager.OpenScene(Ch9ScenePath)`, then `DestroyImmediate` each **generated root by name** (`Rustfang`, `Game`, `Mission`, the rig, the accent lights, dialogue players, reach points, the wave spawner, the ability granter), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred**, reusing the same `EnemyArtWirer.cs` / `CrowdArtWirer.cs` idempotent-mutate-and-save pattern Ch1's doc recommends.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero *environment* prefabs exist for Ch9.** No hold-hall shell, salvage rack, sealed Program wall, freight cradle, shadow rack, engine scaffold, or Sable interface console. The five named-character prefabs (Gryph, Rook, Sable, Echo, and Vane's placeholder-quality mesh) and the additively-wired enemy mesh **do** exist — see Appendix B for the full inventory.

A builder that instantiates from an empty registry produces **an empty cave** — the first run of the refactored builder would destroy Chapter 9.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Props_SalvageRack);
if (prefab == null) {
    Debug.LogWarning($"[Ch9] {ArtKey.Props_SalvageRack} unresolved — primitive fallback.");
    BuildSalvageRackPrimitive(staticArtRoot, pos, tint);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623`. The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No Ch9-specific greybox baseline has been recorded yet.** `Project/Docs/CHAPTER-BUILD-LEDGER.md` tracks Ch9's EditMode test contribution (+22 fixtures: `OverdriveLogic` 15, `Chapter9Lines` 7, landing the running total at 492 as of 2026-07-03) but carries **no `UnityStats` draw-call/tris row for Ch9** the way it does for the Ch1 hub. **Establish one at the first `script-execute` `UnityStats` read of the fresh build** and record it here before any prefab swap — do not invent a number. This chapter is a plausible perf risk case regardless: it is the single longest continuous scene run so far (a ~122 m corridor along +Z, three zones, ten accent lights, two rack rows totalling 22 tinted props, and a full co-op ally-combat encounter with two `AllyCombatant`s + four `Enemy` raiders live simultaneously).
- Set-dressing props today are cheap primitives tinted via the shared `TintShared` MaterialPropertyBlock batching convention (the two rack-row helpers explicitly lerp tint per-instance rather than authoring 11 unique materials). **Prefabs replacing them must carry their own materials and will not batch this way.** Re-measure after every prefab lands, same as Ch1's rule.

## 2. Chapter spatial map

Chapter 9 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch09_PitAndTheDeep.unity`, laid out as **three abutting zones strung along a single +Z line** — `HoldHall` → `TideDepths` → `ConstructionCore` — spanning a total run of **122 m** (z = −2 to z = 120), by far the longest single corridor of any chapter built so far. There is no branching and no returning to an earlier zone by any route other than walking back down the same line (which the chapter's own last beat, the climb-out, requires).

**A candor note on verticality.** The source material stages this chapter as a *vertical* descent — drop-shafts, rappels, a cargo-lift, a dive into black water — and the environment-art direction explicitly calls out "let the camera and the audio always know which way is down." The as-built scene does not implement any of that: it flattens the entire descent onto a flat +Z walk, with the only vertical cue being a **−0.3 m floor step** between `HoldHall` (floor y = 0) and the two deeper zones (floor y = −0.3). This is the same flattening convention Ch1 uses for its four-room corridor; it is called out here because the narrative stakes on verticality are much higher in this chapter (three production notes across the source script ask for climb-down, dive, and pressure traversal), so a future art/level pass revisiting this chapter's geometry should treat "restore real verticality" as a legitimate, larger-scope follow-up — not something this document's registry-swap refactor is scoped to do.

**VR-comfort check on the floor step.** The player crosses the −0.3 m step at both zone joins (z=64 into Tide Depths, and again at z=104 into Construction Core) on foot, under continuous locomotion, with no ramp — and traverses the z=64 join a third time on the Beat 5 climb-out backtrack. A 0.3 m instantaneous step-down at an open join is small enough to sit within typical XR locomotion step-offset tolerances (no stomach-drop, no clipping through floor geometry expected), but it has not been confirmed against this project's `ContinuousLocomotion` step-offset setting. Confirm it against the live component when the room-shell prefabs land, and consider a short visible ramp/lip at both joins instead of a hard step if the confirmation shows otherwise — this is the only floor discontinuity the player crosses more than once in the chapter.

**A further un-flagged zone-state gap: the Construction Core is canonically flooded, and the build leaves it dry.** The beats file stages the Core itself as `INT. TIDE DEPTHS — CONSTRUCTION CORE (FLOODED, HALF-LIT)` (`Ch09_The_Pit_and_the_Deep.md` L81), with "data-light crawls over the water. Rows of shadow-racks hum beneath the surface" (L83) and Sable gesturing "at the scaffolding above the water" (L86). In the build, the `TideFlood` `BoxCollider` (center (0,-0.3,84), size 20×5×40) spans exactly z[64,104] and **stops at the Construction Core boundary (z=104)** — the Core (z104–120) has no water volume, no water surface, and no `ShadowRack` (all ten racks sit at z70–98, inside Tide Depths). This is distinct from the missing data-light VFX flagged at §Beat 4c: even before any VFX lands, the zone's basic geometric state — flooded, racked, half-lit — is wrong, so the reveal beat's staging (Sable at a console over black water, racks humming beneath) currently has neither water nor racks under it. A future pass would want either the flood volume extended north to z=120 or a few `ShadowRack` instances carried into the Core footprint.

```
 -Z (spawn)                                                                                          +Z (dead end)
 HoldHall (pirate hold, aging into sealed Program wall) ── TideDepths (flooded archive) ── ConstructionCore (Concord Engine)
   x[-8,8]  z[-2,64]                                        x[-10,10] z[64,104]                       x[-8,8]  z[104,120]
   center (0,0,31), 16x66, floor y=0                        center (0,-0.3,84), 20x40, floor y=-0.3   center (0,-0.3,112), 16x16, floor y=-0.3
   racks z[10,40] · sealed wall z=47 · cradle z=58            shadow racks z[70,98] · Vane z=88         scaffolds z=112/116 · Sable interface z=108
   Gryph/Rook z=60/61
```

| Beat(s) | Zone | Footprint | Floor center / size | Floor Y |
|---|---|---|---|---|
| 1 (descent) → 2 (bargain) | Rustfang Hold Hall | x[-8,8], z[-2,64] | center (0,0,31), 16×66 | 0 |
| 3 (Tide Depths dive/duel/recruit) | Tide Depths | x[-10,10], z[64,104] | center (0,-0.3,84), 20×40 | -0.3 |
| 4 (Concord Engine reveal) | Construction Core | x[-8,8], z[104,120] | center (0,-0.3,112), 16×16 | -0.3 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, same as every other chapter.

**These footprints are load-bearing and survive the refactor unchanged.** A zone-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience.

**Walls and joins** — there are no doors and no wall between adjacent zones; each zone is only closed on the sides that terminate the chapter:

| Wall | Position | Size | Closes |
|---|---|---|---|
| `HoldHall_WallW` | (-8, 1.8, 31) | (0.2, 3.6, 66) | west side, full hold-hall length |
| `HoldHall_WallE` | (8, 1.8, 31) | (0.2, 3.6, 66) | east side, full hold-hall length |
| `HoldHall_WallS` | (0, 1.8, -2) | (16, 3.6, 0.2) | the chapter's south end (behind player spawn) |
| `TideDepths_WallW` | (-10, 1.8, 84) | (0.2, 3.6, 40) | west side |
| `TideDepths_WallE` | (10, 1.8, 84) | (0.2, 3.6, 40) | east side |
| `ConstructionCore_WallW` | (-8, 1.8, 112) | (0.2, 3.6, 16) | west side |
| `ConstructionCore_WallE` | (8, 1.8, 112) | (0.2, 3.6, 16) | east side |
| `ConstructionCore_WallN` | (0, 1.8, 120) | (16, 3.6, 0.2) | the chapter's north end (dead end, the Engine reveal point) |

The `HoldHall`/`TideDepths` join at z=64 and the `TideDepths`/`ConstructionCore` join at z=104 are both **fully open** — no wall, no doorway aperture, no lock state. Gating between zones is entirely mission-spine `ReachTrigger` steps (§4), not physical geometry.

**Reach-trigger gates** — five, replacing Ch1's three doors as the chapter's progression gates:

| Reach point | Position | Radius | Gates | Fires mission step |
|---|---|---|---|---|
| `OldMachineryReachPoint` | (0, 1, 44) | 5 m | the Program-cut-wall dialogue | step 2 |
| `OverlookReachPoint` | (0, 1, 52) | 5 m | Gryph's Challenge dialogue (Beat 1 → Beat 2) | step 4 |
| `TideEntryReachPoint` | (0, 1, 68) | 6 m | the dive-intro dialogue (Beat 2 → Beat 3) | step 8 |
| `ConstructionCoreReachPoint` | (0, 1, 106) | 6 m | the Concord Engine reveal (Beat 3 → Beat 4) | step 16 |
| `ClimbOutReachPoint` | (0, 1, 59) | 5 m | the Hold Kept handoff (Beat 4 → Beat 5, walking back south) | step 18 |

**Fires-early note.** Every reach point above gates on entering its trigger *sphere*, not on reaching its named coordinate — so on the southbound-to-northbound approach each one fires several meters before the position in the table above: `OldMachineryReachPoint` (z=44, r=5) fires at z=39, right at the `SealedProgramWall`'s own footprint; `OverlookReachPoint` (z=52, r=5) fires at z=47; `TideEntryReachPoint` (z=68, r=6) fires at z=62; `ConstructionCoreReachPoint` (z=106, r=6) fires at z=100 — 4 m south of the TideDepths/ConstructionCore zone join at z=104, i.e. while the player is still in the Tide Depths (flagged in full at §Beat 4b); `ClimbOutReachPoint` (z=59, r=5) fires at z=64 on the southbound Beat 5 return, right at the TideDepths/HoldHall join (flagged in full at §Beat 5b). Every "on crossing at z=X" phrasing elsewhere in this document's per-beat specs is therefore approximate — the reach point's authored position, not its actual fire point.

**Player rig:** `BuildRig(refs, addLocomotion: true)`, spawning at the shared on-foot default `(0, 0, 2)` (`BuildRig`'s `OnFootSpawnZ` nudge) plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring — mirrors Ch1's treatment). Echo's only physical anchor in the scene is the katana, `Named.Echo` at (2,1,4) — `EchoPresence` has no separate visible avatar, so a builder should not go looking for one; this matters because Beat 3's Overdrive grant is diegetically staged as the freed blade-shadow drifting "into Echo," i.e. into that same katana (§Beat 3d). `ZoneBounds` is set to **center (0, 3, 58), radius 130** — one bounding sphere loosely enclosing the entire 122 m run, centered near the hold overlook.

## 3. Global environment & backdrop

**The Rustfang Hold** reads as a lived-in pirate warren aging into a Program worksite: per the dialogue script's SETTING block, "dust hangs in every lamp-cone; the air tastes of rust and cold stone; the whole place groans with the strain of lift-chains." The upper hold is warm and amber-lit (`SpawnLight`, `HoldUpperLight0/1`); the two rows of salvage racks physically carry that read — the shared `Ch9BuildHoldRackRow` helper lerps every rack's tint from rust `(0.42, 0.3, 0.18)` at the shallow end (z=10) toward a cold Program-metal blue `(0.22, 0.3, 0.42)` at the deep end (z=40), so the room ages *underneath the player's feet* without a single new light or prop type — the same "cheap primitive, deliberate palette" economy Ch1's tint pass uses. The `SealedProgramWall` prop at z=47 is the hold's hard visual break: a slab the pirates fortified *around*, not *into*, its Program-blue tint `(0.18, 0.24, 0.34)` matching the cold end of the rack gradient it caps.

**Audio gap: the Hold has no ambience bed.** Unlike the two deep zones, the Hold Hall/overlook — where the player spends four of six beats (0, 1, 2, 5) and the majority of playtime — has no `BuildAmbienceLayer` at all (`Chapter9Builder.cs:333-334` builds only `ConstructionCoreAmbience` and `TideDepthsDreadAmbience`, both downriver of here). The SETTING block stages this zone's soundscape most specifically of anywhere in the chapter — "dust hangs in every lamp-cone… the whole place groans with the strain of lift-chains hauling salvage up out of the dark" — and Beat 3's design deliberately inverts it, "above is grinding lift-work and watch-fire clamor, below is a drowned hush" (§Beat 3a). That inversion only lands if the "above" bed exists; today only the "below" half is built. See §7 for the missing `RustfangHoldAmbience` layer this implies.

**The Tide Depths** invert the hold's sound and light design per the SETTING block: "above is grinding lift-work and watch-fire clamor, below is a drowned hush." The zone's fog and ambient are the darkest in the chapter (`RenderSettings.fogColor` (0.05, 0.09, 0.12), the same chapter-wide value, but the two `TideDepthsLight0/1` accents run cold teal against it), and the shadow-rack row (`Ch9BuildShadowRackRow`) runs the inverse gradient of the hold's racks — dim teal `(0.1, 0.28, 0.32)` near the entrance brightening to a lit teal `(0.2, 0.55, 0.6)` near the Construction Core, reading as "the current gets stronger toward the machine." **The zone has no water-surface visual at all** — see the candor note in §1.6/Appendix B; `FloodingWaterHazard` is a pure gameplay trigger with no renderer.

**The Construction Core** is the chapter's single brightest, most saturated space: two `CoreLight0/1` accents at cyan `(0.35, 0.9, 0.95)`, intensity 2 — the highest intensity of any accent light in the chapter — reading as "the data-glow of a half-built machine" the SETTING block calls for. `SableInterface` and the two `EngineScaffold` props are the only physical geometry standing in for "scaffolding climbs out of the black water... data-light crawls across the surface."

**Read together, the three zones' per-light color picks already encode a deliberate thermal progression, not just a lighting palette — worth naming explicitly as a design target the environment art should preserve.** The Hold's warm amber (`SpawnLight`/`HoldUpperLight0/1`, rust-toned racks, watch-fire implication) gives way to the Tide's cold drowned teal (the chapter's darkest fog against cold-teal accents, "drowned hush") and lands on the Core's sterile data-cyan (`CoreLight0/1`, the chapter's coldest-blue hue *and* its brightest intensity). That is a coherent felt-temperature arc — warm crackle/dust cooling into cold drip/breath-fog implication through the Tide, then clinical machine-cold at the Core — and the eventual `Ch9Environment.asset` author and any audio/VFX commissions (§7, §9) should treat it as intentional, not reproduce it as an accident of per-light color values picked in isolation.

**One tension worth naming against that arc.** The builder applies a single chapter-wide `fogColor` (0.05, 0.09, 0.12) — a cold blue-teal tuned for the Tide/Core end — uniformly across all three zones, including the warm amber Hold Hall where the player spends four of six beats. §6 already notes fog is "a single profile value, never overridden per-zone," but frames that purely as a convention, not as a mild tension with the thermal arc above: the uniform cold fog benefits the Tide/Core and mildly undercuts the Hold's warm read. At density 0.02 exponential the effect is subtle at close range, but the eventual `Ch9Environment.asset` author should treat accepting it, or authoring a warmer per-zone fog override for `HoldHall`, as an explicit small decision rather than an unnoticed cancellation of the Hold's amber read.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter9Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch9Environment.asset`, same schema as Ch1's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` per zone | `Color` | every `BuildFloorCeiling` call (three zones, two distinct tint pairs — see Appendix A.1) |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight`, ten entries |
| `eventLights[]` | — | **unused this chapter** (Ch9 has no docking-alarm-style event light; leave the array empty in the profile rather than omitting the field, so the schema stays uniform across chapters) |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("OldMachineryLight", seed: 99f)` and `AddAmbientPulse("TideDepthsLight0", periodSeconds: 6.8f)` calls with data. Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored.

**Note on the key light's angle.** The directional key is authored at Euler(55, -35, 0) — a conventional sun elevation — despite every zone in this chapter being sealed subterranean rock with no sky (Rustfang hold → drowned trench → buried core), unlike Ch1's ship-interior setting where a rotated "sun" reads as an interior key light rather than a real sky cue. The current intensity (0.32) is low enough that it likely reads as pure ambient fill rather than a visible directional cue, but the eventual `Ch9Environment.asset` author should either confirm that reading explicitly or reconsider a downward-biased fill angle, which would read more honestly for a chapter whose whole art direction is "which way is down, no sky."

## 4. Per-beat scene spec

The chapter plays as six beats along the +Z run: Beat 0 (voice-only briefing aboard the Cairn), Beat 1 (the descent), Beat 2 (Gryph's Bargain — Ally #5), Beat 3 (the Tide Depths — boss, Ally #6, Overdrive), Beat 4 (the Concord Engine reveal), Beat 5 (the Hold Kept — the climb-out handoff to Rook). Each beat is documented with the same a–f structure.

**Table conventions, everywhere below** — identical to Ch1's: art tables carry Position/Rotation, Registry Key, resolved path, and Status; art tables never carry `scale()`/`size()`/`PrimitiveType`; positions/rotations encode blocking and are kept; **Status `MISSING`** means the primitive fallback is active for that row.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes for props that will become prefabs. Create **`BuildBeat0Art()`** for the spawn-area set dressing (there is almost none — this beat is voice-only) and **`BuildBeat0Logic()`** for the single dialogue player and its mission step.

#### a. Narrative purpose & emotional target

Beat 0 is the loudest, fullest the Hub has ever felt — Act III opens on a war-room holo-table finally crowded with a whole roster (Morrigan, Coral, Mera, Resh, Kessler, Mira, Iris) instead of the sparser crews of earlier chapters. The emotional job is scale-setting before the descent: Morrigan sizes the relay as "bigger than a comms node has any business being," Coral supplies the pattern-read ("the deep is where they hide the kept"), and the beat closes on two promises that pay off later in the chapter — Ronin-7's answer to Mira ("So someone follows. Down a hole this time") and his vow to Coral ("If there's one of us down there, they come up with me"), which is the exact promise Sable's rescue in Beat 3 fulfills. Echo's aside marks the chapter's title in miniature: "every answer in this story's been further down than the last one... this one's the deepest yet."

Per the source script's CREW-PRESENCE DECISION (§ builder header comment), the crew is voice-only for the entire chapter — Cipher descends alone. Beat 0 is the only beat where every one of them speaks; from Beat 1 on, only Kessler, Coral, and Morrigan check in over comm, and their signal visibly degrades with depth exactly as Kessler predicts here.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at the on-foot default (0, 0, 2), facing +Z into the hold hall. The player does not travel in Beat 0 — this beat plays out entirely as a stationary voice-over while the player stands at the chapter's spawn point, katana already at hand (see Art, below).
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4).
- No NPCs are physically placed for this beat — every speaker (Morrigan, Coral, Mera, Resh, Iris, Kessler, Mira, Echo) is voice-only, per the class-level CREW-PRESENCE DECISION.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` — the full war-room briefing, 12 lines, ending on Ronin-7's "Plot the Rustfang. Take us down." |

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Katana "Echo" | (2, 1, 4), Euler(-90, 0, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |

**Note on the katana.** Per the class-header comment, "the katana rides from the start (deep into Act III — no rack-wake beat, matching Ch5-8's 'cost, not initiation' precedent)." Unlike Ch1, where the blade is discovered mid-beat, by Ch9 it is simply present at spawn — there is no pickup prompt or reveal moment. "At hand" slightly overstates the placement, though: `BuildSword` puts it at world (2, 1, 4) — 2 m to the player's right and 1 m up, a free-standing grabbable the player must reach for, not controller-attached. Grab-to-arm, no pickup prompt, same seeding convention the other chapters use for the blade.

**Flagged gap — the grab is ungated.** Nothing in the mission spine requires the player to actually pick Echo up before proceeding north — there is no `ReachTrigger` or step condition on it. A player who walks straight past the free-standing blade at (2,1,4) toward Gryph's overlook (z=52) can reach the chapter's first combat, the Coil raid at z≈54 (step 6), unarmed, and would have to backtrack ~50 m to spawn to arm before the fight is winnable. Ch1 discovers the blade as a scripted, gated beat; Ch9 does not, and nothing upstream of this document previously flagged the unarmed-arrival risk. **The same gap also starves the chapter's headline mechanical deliverable:** Overdrive's charge meter accrues only from sword hits landed in combat (§Beat 3d), so an unarmed arrival doesn't just make the raid and the Vane duel harder — it means the player can never charge the very ability this chapter exists to grant. Not fixed here — gating the grab is new mission-spine surface, out of scope for an art pass.

**Candor note — the Cairn war-room is entirely unbuilt.** (a) above calls Beat 0 a "war-room holo-table finally crowded with a whole roster," and the dialogue script stages it explicitly: `INT. THE CAIRN — WAR-ROOM HOLO-TABLE`, "a wide bowl of light... tier on tier of reignited stations," Morrigan throwing "a three-dimensional cutaway of a warren-hold... the Rustfang rendered as a vertical wound going down and down" (dialogue script L174), with seven crew physically gathered around it. None of that set exists in the build — the entire briefing plays as disembodied VO while the player stands alone at the Rustfang hold mouth, spawn (0,0,2) facing +Z (see Logic, above). This is the largest single set-level divergence in the chapter — a whole location, the Cairn itself, elided — and unlike the water surface, the cargo-bridge, and the generator-stack elsewhere in this document, it has gone un-flagged until now. It also means (a)'s emotional target ("the loudest, fullest the Hub has ever felt") and (f)'s built reality ("spatial audio") are the furthest apart of any beat in this chapter. Candidate future props for a scoped follow-up: a `WarRoomHoloTable` and a `RustfangCutawayProjection` VFX; flagged here, not fixed — building a second location is new set-art scope, not a like-for-like art pass.

#### d. Combat

None. Beat 0 has zero combat components — the raiders, Vane, and every enemy definition are instantiated later in the build order but not touched by this beat's mission step.

#### e. Dialogue / VO

Set id **`ch9_beat0_briefing`**, position (0, 1, 4), 12 lines, advanced on **Left-Hand "Talk" (Y)**:

| Speaker | Line (as authored in `Chapter9Lines.cs`) | sec |
|---|---|---|
| Morrigan | "The Garden pointed you down, so down is where I looked. A relay, buried under the Rustfang hold... It's old, Cipher. It's bigger than a comms node has any business being, and it isn't feeding a base. Whatever they're building, this is one of its veins." | 20 |
| Coral Vex | "The deep is where they hide the kept. Hardware that old, buried that deep, off every chart... If there's a machine that big in the dark, Cipher, there are shadows near it. There always are." | 20 |
| Mera Voss | "The Rustfang isn't open ground. It's a hold, a pirate free-hold carved into the caves of a dead world and held by an old warrior. Gryph... We go down through his hold or we don't go down at all." | 18 |
| Resh | "I know the name. Gryph's not a syndicate man, he's harder to deal with than one... You want his deep, friend, you don't bring him a price. You earn him." | 18 |
| Iris | "A relay that old, that deep, under a hold that's been digging down for years, and those pirates never even knew it was there. You don't bury a thing that big to forget it. You bury it to use it where nobody's looking." | 14 |
| Kessler | "And it only goes one way... it's shafts and a dive and a man who throws in with one fighter at a time or none, so it's you, Cipher, same as the mountain, same as the gate. I'll hold the rig over the hold and run comm as far down as the rock lets me." | 24 |
| Mira | "It's another place that only fits you. They keep being places that only fit you." | 5 |
| Ronin-7 | "They do. Because the Program does its work where it thinks no one will follow. So someone follows. Down a hole this time." | 8 |
| Echo | "Cipher. Every answer in this story's been further down than the last... This one's the deepest yet, and the readout says it ends in water... So let me be. Read you the dark, call the drops, keep you breathing. Down we go." | 22 |
| Ronin-7 | "Down we go." | 4 |
| Coral Vex | "One thing, before you go where I can't. If you find the kept down there, and you will, they won't be shelved the way mine are... Bring them up if you can. That's the only thing I'll ask of this whole descent." | 18 |
| Ronin-7 | "If there's one of us down there, they come up with me. Plot the Rustfang. Take us down." | 6 |

Total runtime ≈ 177 s. This is a **voice-only cast** dialogue set — no speaker except Ronin-7 has a physical presence in the scene, so this table's "position" column is a single shared anchor rather than per-speaker blocking.

#### f. Audio / Haptics / VR Comfort

- No camera shake — the whole beat is a stationary two-way conversation, so the only feel to sell is spatial audio: the `SpawnLight` accent (warm 0.85/0.7/0.45) and the chapter's baseline exponential fog set the mood before any traversal begins.
- **Engagement/comfort flag, not only a set-divergence.** Beat 0 is ≈177 s of stationary VO at the very open of the chapter, with no visual anchor beyond the hold mouth and the katana at (2,1,4) — a long time to stand still in a headset with nothing to look at. The war-room-unbuilt candor note above (§c) already covers the narrative cost of the missing Cairn set; the same gap is also the *engagement* fix for this window — a `WarRoomHoloTable`/`RustfangCutawayProjection` commission (candidate props named in §c) would give the player something to visually track during the 177 s rather than a stationary hold mouth.
- No *combat* haptics; the katana grab at (2,1,4) fires the standard XR grab pulse — see the ungated-grab note in §c above.
- Comfort vignette is inert — the player does not move during Beat 0.

---

### Beat 1 — The Rustfang Hold (Descent / Traversal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitive cubes for the rack rows or the sealed wall — read `Props.SalvageRack` and `Props.SealedProgramWall` from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (hold-hall shell, both rack rows, the sealed Program wall) and **`BuildBeat1Logic()`** (the two reach points, both dialogue players, their mission steps).

#### a. Narrative purpose & emotional target

Beat 1 is the chapter's environmental-storytelling beat: per the production note, "the transition from base to worksite is told entirely through environment art and Echo's reads, never exposited flat." The player walks down the hold hall watching the rack-row tint shift from rust to Program-blue underfoot, then hits the `SealedProgramWall` and gets the beat's hard reveal — Echo naming it "sealed... machined... Program-cut," Coral confirming from a lifetime's experience, Morrigan sizing it as "a vein, not the heart." The emotional target is **dawning wrongness**, the same register Ch7's reliquary uses, but here delivered through descent rather than a single room: "the deeper he goes, the older the machinery gets."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** free-walks the length of the hold hall (z ≈ 4 → 44) on continuous locomotion + snap-turn. No scripted path.
- **`OldMachineryReachPoint`:** (0, 1, 44), radius 5 m — gates the Old-Machinery dialogue on the player physically reaching the `SealedProgramWall`.
- No NPCs are placed for this beat — Kessler, Coral, and Morrigan's Beat 1 lines are all comm-only (voice-only), continuing the CREW-PRESENCE DECISION.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Descent` (`ch9_beat1_descent`) — Echo + Kessler-over-comm as the descent begins |
| 2 | ReachTrigger | Gates on `OldMachineryReachPoint` (0,1,44), radius 5 |
| 3 | Dialogue | `Dialogue_Beat1_OldMachinery` (`ch9_beat1_old_machinery`) — Echo/Coral/Morrigan name the sealed Program wall; ends on Ronin-7's "A vein. Then let's go down to the heart of it." |

**What changes during the beat:** nothing is added or removed — the rack-row tint gradient and the wall are all static from build time. The only state change is the reach-trigger firing the second dialogue set once the player arrives at z=44.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Hold Hall shell (16×66, walls + floor + ceiling) | center (0,0,31) | `Rooms.RustfangHoldHallShell` | `…/Art/Generated/Rooms/RustfangHoldHallShell.prefab` | **MISSING** |
| `SalvageRack` ×12 (6 pairs, x=∓7, z=10/16/22/28/34/40) | tint lerps rust (0.42,0.3,0.18) → Program-blue (0.22,0.3,0.42) across z | `Props.SalvageRack` | `…/Art/Generated/Props/SalvageRack.prefab` | **MISSING** |
| `SealedProgramWall` | (0, 1.8, 47) | `Props.SealedProgramWall` | `…/Art/Generated/Props/SealedProgramWall.prefab` | **MISSING** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |
| `HoldUpperLight0` (accent) | (-3, 2.6, 18) | — | `ChapterEnvironmentProfile.accentLights["HoldUpper0"]` | profile |
| `HoldUpperLight1` (accent) | (3, 2.6, 30) | — | `ChapterEnvironmentProfile.accentLights["HoldUpper1"]` | profile |
| `OldMachineryLight` (accent) | (0, 2.4, 45) | — | `ChapterEnvironmentProfile.accentLights["OldMachinery"]` | profile |

**Notes on the transition.** `Props.SalvageRack` is instantiated 12 times from one prefab with per-instance tint override (the same `TintShared`/MPB batching pattern Ch1 uses for its wreck silhouettes) — a prefab replacement must preserve per-instance tinting, not bake one fixed color, or the rust→blue "the deeper it goes" read is lost. The `SealedProgramWall` is the beat's single hard prop: a slab spanning nearly the hold-hall's width (15 m of its 16 m), reading as a wall the pirates built *around*.

**⚠ Flagged gap — the `SealedProgramWall` is a full-height solid barrier and the mission spine never says how the player gets past it.** `BuildProp` instantiates every prop via `GameObject.CreatePrimitive(Cube)`, which keeps its `BoxCollider` (the same collider `BuildCylinderProp`'s own comment confirms every room-detail prop keeps). At (0, 1.8, 47), size (15, 3.6, 0.4) — full `RoomH` and 15 m of the hold hall's 16 m width — the wall spans x=[-7.5, 7.5] while the hold-hall side walls' inner faces sit at x=±7.9, leaving only a **~0.4 m gap at each edge**: too narrow for a VR `CharacterController` to pass. Yet the mission spine requires the player to continue north past z=47 to the overlook (z=52), the Tide Depths, and the Core — this beat's own closing line even ends on Ronin-7's "let's go down to the heart of it." Nothing in this document, Appendix A.3, or Appendix B currently addresses that the as-built prop is a full-height corridor blocker rather than a passable landmark. **It is also a canon mismatch**, not just an unaddressed collider: the dialogue frames the sealed Program structure as a *floor/lid underfoot*, not a wall across the path — Echo's "they dug their hold into the lid of it" (§Beat 1e) and Coral's "standing on the roof of something the Program buried" (§Beat 1e) both stage a horizontal surface the player descends *over*. Flagging this as an open traversal question — currently the only gap in this document that can hard-stop progression — and recommending the canon-faithful reframe for a future reviewed pass: a floor-plate/lid section (or a wall prop with a real walkable aperture cut into its collider), which resolves both the blocker and the "lid, not wall" mismatch. **Do not silently carve a hole in the existing collider while doing an art pass** — the fix touches mission-critical traversal geometry and needs its own sign-off.

**Note on unbuilt watch-fire set-dressing.** The dialogue script repeatedly names "watch-fires and salvage-rigs throwing light" as part of the hold's texture; no watch-fire prop or light source exists in the build — the hold's warmth is carried solely by the amber `HoldUpperLight0/1`/`SpawnLight` accents. Worth flagging for a future art pass rather than silently omitting.

#### d. Combat

None. Beat 1 is pure descent/traversal, matching the production note's "light combat is incidental here, not central."

#### e. Dialogue / VO

Two sets, both advanced on Left-Hand "Talk" (Y):

`Dialogue_Beat1_Descent` (`ch9_beat1_descent`), position (0, 1, 12), 2 lines, ≈32 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "We're in the hold now, Cipher. Look at it, a wound all the way down, and they've been carving at it for years... Keep dropping. The lift only goes so far, then it's rope and the kind of falling we control on purpose." | 16 |
| Kessler | "I've got you on the rig's scope, a little spark going down a big dark hole. Comm's already roughening up, rock's thick and getting thicker... You find that relay and you come back up." | 16 |

`Dialogue_Beat1_OldMachinery` (`ch9_beat1_old_machinery`), position (0, 1, 44), 4 lines, ≈66 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Stop. Look at that wall. That isn't pirate work and it isn't natural rock. That's sealed, that's machined, that's ours, Cipher, Program-cut... They dug their hold into the lid of it." | 20 |
| Coral Vex | "He's right. I can hear it in your feed, that hum... The pirates think they own a cave. They're standing on the roof of something the Program buried and walked away from with the lights still on." | 22 |
| Morrigan | "Relay's loud now, loud enough I can finally read what it's doing, and Cipher, it isn't receiving. It's relaying... You're standing in the middle of a vein, not the heart. The heart's below you, under the water." | 18 |
| Ronin-7 | "A vein. Then let's go down to the heart of it." | 6 |

#### f. Audio / Haptics / VR Comfort

- No camera shake, ever.
- Kessler's comm line explicitly stages audio degradation with depth ("comm's already roughening up... rock's thick and getting thicker") — an `AudioDirector` filter/attenuation cue, not a scripted event in the current build; flagged for a future pass as an *(inferred, not asserted by the builder)* enhancement.
- `OldMachineryLight` carries `behaviour: ConsoleFlicker(seed: 99)`, the beat's one lighting event — the Program-cut wall's power reads as "old and still running," matching Coral's line, not the failing-power flicker Ch1's Command Room uses (different narrative intent, same mechanism).
- **The cold-to-warm handoff at the zone break is a deliberate cross-fade, not a hard cut.** `OldMachineryLight` (z=45, Program-blue (0.4,0.6,0.95), range 12) sits only 8 m from Beat 2's `OverlookLight0` (z=53, warm (0.95,0.55,0.3), range 16), so their falloffs overlap across roughly z=48–50 even though §3 describes the wall as the hold's "hard visual break" at z=47. Read together, this is almost certainly the intended cold-machinery-to-warm-overlook handoff rather than a contradiction — worth naming explicitly here so the eventual `Ch9Environment.asset` author preserves these ranges (rather than tightening them into a hard switch) when the profile is authored.
- Comfort vignette engages normally on the ~40 m walk-and-turn from spawn to the wall.

---

### Beat 2 — Gryph's Bargain (The Hold Overlook — Protector Choice, Ally #5)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Props.FreightCradle` from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (freight cradle, overlook accent lights) and **`BuildBeat2Logic()`** (Gryph + Rook placement as `AllyCombatant`, the Coil-raider wave spawner, the `DefeatWaves` step, both dialogue players).
> **This is a co-op hold/protect encounter, not a clear-the-room** — Gryph and Rook fight alongside the player, not for the player to rescue as an objective.

#### a. Narrative purpose & emotional target

This is Act III's thesis made mechanical: the choice to fight *beside* the hold's owner instead of taking his map off a corpse. The production note is explicit that **there is no "rob Gryph" branch** — the protector choice is honored by being the only path, not a tracked flag, which is exactly how the builder implements it: the Coil raid is a straightforward `DefeatWaves` step with Gryph and Rook built as non-damageable `AllyCombatant`s fighting at the player's side, no separate "protect" fail-state to track. Gryph starts hard and testing ("what stops you doing the same") and ends the beat pledging his arm "warrior to warrior," naming Rook his successor in the same breath he accepts. The beat's small but real correction, applied by the audit fix baked into `Chapter9Lines.cs`, softens Gryph's bargain-beat line from a personal dive-pledge to a comm-only guide role — consistent with his Beat 3 comm-only presence — and adds a short two-line handoff so Gryph actually learns the "Cipher" codename before using it from Beat 3 on.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **`OverlookReachPoint`:** (0, 1, 52), radius 5 m — gates Gryph's Challenge dialogue on the player reaching the overlook.
- **Gryph:** placed once at (-2, 0, 60), `StoryNpc` + `AllyCombatant` (no `Health` — allies cannot be damaged by design), `bodyRenderer` wired for hit-flash/VFX purposes. He stays at this position for the rest of the chapter, including the Beat 5 handoff — he is never given an `NpcWalker`.
- **Rook:** placed once at (2, 0, 61), same `AllyCombatant` treatment. Introduced here, named on-screen in the raid bark, and reappears unmoved in Beat 5 as Gryph's successor.
- **⚠ Flagged gap — Gryph's first line can fire while he's occluded by the `SealedProgramWall`.** Per the fires-early math in §2, `OverlookReachPoint` (z=52, r=5) actually fires Gryph's Challenge dialogue (step 5) at z≈47 — the exact z-plane of the `SealedProgramWall` (§Beat 1c). Gryph stands 13 m further north at z=60, on the far side of that full-height, near-full-width slab. So the chapter's first ally introduction — Gryph's "Stop right there. You came down a long way through my hold" — can play while the player is pressed against the impassable wall with Gryph unseen behind it. This is a second cost of the `SealedProgramWall` blocker (§Beat 1c, §9's top open item), not just a traversal stopper: it also occludes Ally #5's reveal. Any future wall reframe (the floor-plate/lid fix recommended in §Beat 1c) should also preserve a clear north sightline from the challenge trigger (~z=47) to Gryph at z=60, or Gryph introduces himself to a blank wall. Flag, don't fix — this rides the same traversal geometry §Beat 1c already scopes as needing its own reviewed change.
- **Coil raiders:** four `Enemy` instances at (-3,0,56), (0,0,54), (3,0,56), (0,0,62), all built `SetActive(false)`. `Ch9EnsureCoilRaiderDefinition`: maxHealth 55, damage 9, moveSpeed 1.5, attackCooldown 0.9.
- **`CoilRaidWaveSpawner`:** an `EnemyWaveSpawner` at (0, 0, 58), trigger radius 12 m, one wave containing all four raiders, bark = `Dialogue_Beat2_RaidBark`. Per the class-header FLOODING/AllyCombatant flagged note, the spawner is built **active-idle** (not inactive) because an inactive `GameObject` cannot run the coroutine `Begin()` needs — its own proximity poll gates the actual spawn on the player's approach, and the `DefeatWaves` mission step simply calls `Begin()`.
- **⚠ Flagged gap (carried verbatim from the builder's own comment):** `AllyCombatant.RetargetNearestEnemy` has no max engagement range. Once the raid is cleared, Gryph and Rook idle with no live target — but the instant Vane/Wraith-6 activates ~28 m away at the Tide Depths (Beat 3), they would *also* start walking toward him, contradicting the "Gryph is comm-only, doesn't physically follow" constraint the dialogue establishes. The mitigation today is timing, not a hard fix: `moveSpeed` 1.4 m/s over ~28 m gives a multi-second grace window before they'd arrive, and the duel typically resolves well inside it — but this is a **real gap**, not stylistic. A proper fix needs a range cap on the shared `AllyCombatant` component, out of this pass's scope because it touches every future ally-combat chapter. **Do not silently "fix" this while doing an art pass** — it needs its own reviewed change.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 4 | ReachTrigger | Gates on `OverlookReachPoint` (0,1,52), radius 5 |
| 5 | Dialogue | `Dialogue_Beat2_Challenge` (`ch9_beat2_challenge`) — Gryph's test |
| 6 | DefeatWaves | `CoilRaidWaveSpawner` — one wave, all 4 raiders, bark plays on spawn |
| 7 | Dialogue | `Dialogue_Beat2_Bargain` (`ch9_beat2_bargain`) — the map given, Ally #5 pledged, the Cipher handoff |

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `FreightCradle` | (0, 0.4, 58) | `Props.FreightCradle` | `…/Art/Generated/Props/FreightCradle.prefab` | **MISSING** |
| `OverlookLight0` (accent) | (-4, 2.6, 53) | — | `ChapterEnvironmentProfile.accentLights["Overlook0"]` | profile |
| `OverlookLight1` (accent) | (4, 2.6, 57) | — | `ChapterEnvironmentProfile.accentLights["Overlook1"]` | profile |
| Gryph | (-2, 0, 60) | `Named.Gryph` | `…/Art/Generated/Characters3D/Named/Gryph.prefab` | **EXISTS** |
| Rook | (2, 0, 61) | `Named.Rook` | `…/Art/Generated/Characters3D/Named/Rook.prefab` | **EXISTS** |
| Coil raider ×4 | see Logic table | `Enemies.CoilRaider` | additively resolved by `EnemyArtWirer` → `…/Art/Generated/Characters3D/Enemies/Ash-World_Scavenger.prefab` | **EXISTS** *(narrative/art mismatch — see note)* |

**Note on the Coil-raider art mismatch.** `EnemyArtWirer.cs`'s per-scene mapping table wires `Ch09_PitAndTheDeep` to `Ash-World_Scavenger` mesh, **not** the `Coil_Syndicate_Ganger` mesh that already exists on disk and is used for the narratively-correct "Coil" faction elsewhere (Ch02, Ch10). This is the same class of gap Ch1's doc flags for its rifle-vs-blade trooper mismatch: worth surfacing, not silently "fixing" mid-refactor, since correcting it is a one-line table edit in a shared wirer that a different reviewer should sign off on (it also affects nothing else this chapter touches).

**Note on the `FreightCradle`'s diegetic role.** Unlike this doc's other prop briefs, which state what a prop *is* narratively (`SealedProgramWall` — "a slab the pirates fortified around"), `FreightCradle` is currently described only as a generic (2.2, 0.8, 2.2) box. Canon makes it the working cargo-lift Gryph physically operates to open the dive: "Gryph hauls the freight-cradle's brake and the deep shaft opens, the black gleam of the Tide far below" (dialogue script L354). A prefab replacing it should read as a winch/cargo-lift cradle with a brake lever, not a crate — this also explains why it sits at z=58 (the drop point immediately north of the overlook) and why Beat 5 reuses it unmodified as the handoff's backdrop.

**Note on the unbuilt overlook generator-stack.** Canon stages a "generator-stack throwing hard white light" at the overlook (dialogue script L286) and has "the watch-fires built up again after the raid" open Beat 5 (L517). Neither the generator-stack prop nor its hard-white light contrast exists — the overlook's warmth today is carried entirely by the warm-orange `OverlookLight0/1` accents. Minor, but this is exactly the set-dressing that would sell "lived-in pirate warren"; noting it here for a future art pass's prop list.

#### d. Combat — the co-op hold defense

Gryph and Rook are `AllyCombatant`s: they retarget and attack the nearest live `Enemy` automatically (`moveSpeed` 1.4, `attackRange` 1.6, `damagePerHit` 8, `attackInterval` 1.4), cannot be damaged (no `Health` component by design), and read visually as fighting *for* the player rather than needing to be protected as an objective. Player damage output is the existing `BladeDamager` EMA swing-speed model. All four raiders spawn from the `CoilRaidWaveSpawner`'s single wave once the player crosses its 12 m trigger radius centered on (0,0,58) — the freight-cradle overlook, a tight shelf that reads as "good knife-range/no-retreat lane," the same kill-box logic Ch1's airlock corridor uses. One raider — (0,0,62) — spawns *behind* Gryph and Rook (z=60/61) relative to the player entering from z≈52, so the wave literally brackets the overlook from both the bridge side and the deep side at once, a free bit of tactical texture already sitting in the coordinates that pays off Gryph's bark "they've been circling my hold for a month."

**Note on the unbuilt cargo-bridge.** Ronin-7's own raid-bark line commits him to a specific battlefield feature — "I'll take the bridge, you keep the cradle" (dialogue script L399, quoted in full in §Beat 2e) — and canon has the raiders "pour down a side-shaft and across a cargo-bridge onto the shelf" (L308). No bridge prop exists in the build; the overlook is a flat 16 m floor, so "the bridge side" above is a conceptual lane only, with no geometry backing the verbal assignment. A future `CargoBridge` prop on the raider-approach (deep/side) edge would realize the line; flagged here rather than left silently unmotivated.

**Combat trigger:** the `DefeatWaves` step calls the spawner's `Begin()`; the spawner's own proximity poll (not a scripted cutscene cut) fires the actual wave and its bark line ("Coil. They've been circling my hold for a month...").

**⚠ 90 FPS note:** this is the chapter's highest simultaneous-actor count — two `AllyCombatant`s + four `Enemy` raiders + the player, all live at once, in a room with two accent lights and 12 tinted rack props already rendering. Re-measure here first when establishing the missing perf baseline (§1.6).

#### e. Dialogue / VO

`Dialogue_Beat2_Challenge` (`ch9_beat2_challenge`), position (0, 1, 52), 4 lines, ≈61 s:

| Speaker | Line | sec |
|---|---|---|
| Gryph | "Stop right there. You came down a long way through my hold... So talk fast, stranger. Tell me why I shouldn't have my band put you in the shaft with the rest who came down here wanting what's mine." | 22 |
| Ronin-7 | "There's a Program relay below your water. Old, buried, bigger than anyone down here knows. I need the map to reach it. I didn't come to take it. I came to ask." | 9 |
| Gryph | "Ask. Nobody comes down my hold to ask... So tell me true. What stops you doing the same." | 18 |
| Echo | "He's not wrong about the men who came before you, Cipher. He's measuring whether he could stop you... He wants to see which kind of dangerous walked into his hold." | 12 |

`Dialogue_Beat2_RaidBark` (`ch9_beat2_raid_bark`), position (0, 1, 54) — plays as the wave spawner's bark, 3 lines, ≈27 s:

| Speaker | Line | sec |
|---|---|---|
| Gryph | "Coil. They've been circling my hold for a month, and they picked their night. So here's your moment, stranger... Or you can pick a side and find out something about yourself." | 13 |
| Ronin-7 | "I pick a side. Hold your line, old man. I'll take the bridge, you keep the cradle, and we put them back up the shaft they came down." | 8 |
| Rook | "Captain's got the cradle, the stranger's got the bridge. Rest of you, on me. We hold the shelf tonight." | 6 |

`Dialogue_Beat2_Bargain` (`ch9_beat2_bargain`), position (0, 1, 59), 6 lines, ≈94 s:

| Speaker | Line | sec |
|---|---|---|
| Gryph | "You fought beside me... You're the first who came down with a blade and used it for my people instead of against them." | 20 |
| Ronin-7 | "Then know this. I'm done being the thing that takes. Your map's worth more to me given freely than cut off your body. Everyone I take in lives aboard the Cairn. A tracker, an engineer, a smuggler, a forebear, a child. You'd berth with the strays." | 16 |
| Gryph | "A reason to give it. All right, then. The map's yours, and so is my arm, if you'll have it... Not for the protection. For being the first man to come down my hold in thirty years and spend his blade on my people instead of my back." | 24 |
| Gryph | "But your band doesn't go leaderless while I'm walking you through that water. Rook's run this crew at my shoulder for ten years... The water's got a guardian, friend... I've lost six good crew to it and never once saw it bleed. Better you meet it with me telling you where the air pockets are." | 26 |
| Gryph | "I never got your name, stranger." | 4 |
| Ronin-7 | "Cipher. That's what the crew calls me." | 4 |

The last two lines are the audit-fixed "Cipher" handoff, added specifically so Gryph's Beat 3 use of the codename is earned on-screen (see `Chapter9Lines.cs` header comment).

#### f. Audio / Haptics / VR Comfort

- Combat feel for the whole raid is carried by `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — **no camera shake**, including on Gryph/Rook's own attacks (which the player only ever sees, never feels via the camera).
- `WaveAlarm.wav` is wired as the spawner's `waveSting` (loaded from `Assets/Ronin7/Audio/WaveAlarm.wav` if present) — the raid's arrival cue.
- `OverlookLight0/1` carry `behaviour: None` — level, warm-orange, no flicker; the overlook's lighting stays static so the raid's chaos reads against a calm baseline, same design logic as Ch1's Main Hold before its running-light sting.
- Comfort vignette will fire frequently here — a multi-attacker melee with two allies and four enemies is the most snap-turn-dense stretch in the chapter so far.

---

### Beat 3 — The Tide Depths (Drowned Archive — Boss, Ally #6, Overdrive)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Props.ShadowRack`, `Props.SableInterfaceConsole` is Beat 4's, not this beat's. Create **`BuildBeat3Art()`** (Tide Depths shell, shadow-rack row, Sable's inert placement, Vane's inactive placement) and **`BuildBeat3Logic()`** (the flooding hazard, the reach point, all five dialogue players, the `DefeatEnemies` boss step, the Overdrive `AbilityGranter` trigger).
> **Vane's `SetActive(true)` is not a separate Trigger step** — `MissionDirector.BeginDefeatEnemies` activates every `Health` in the step's list itself when the step starts. Do not add a redundant activation trigger.

#### a. Narrative purpose & emotional target

This is the chapter's spine: a boss with **no mercy branch**. The production note is explicit and load-bearing — "Vane CANNOT be spared, talked down, or subdued... the fight ends only in his death... this is canon and load-bearing (it frees his blade-shadow and unlocks Overdrive)." Sable's introduction plays first, warning the player off before Vane's flat, dead-calm "the asset is correct... turn, or be kept" — a line the dialogue script calls out as a **deliberate echo** of Ronin-7's own Beat 0 line about the Program working where no one follows. Echo's read reframes Vane not as a twin but a **forebear** — the Wraith-6 make, one generation older, the same line Coral Vex broke free of — which is why the kill reads as grief rather than triumph (Ronin-7's post-kill line: "Rest, Wraith. You came before me and you never once got to"). The beat closes on two more emotional turns in sequence: Overdrive granted as "the only thing they ever let him give anyone," and Sable's recruitment, which tests Ronin-7 before accepting ("you need to see what I'm wired into... bring me to the core") — seeding Beat 4's reveal directly.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **`TideEntryReachPoint`:** (0, 1, 68), radius 6 m — gates the dive-intro dialogue.
- **`TideFlood` hazard:** a `BoxCollider` trigger at (0, -0.3, 84), size (20, 5, 40) *(size (Ch9TideHalfWidth×2, 5, 40) = 20×5×40)*, local center offset (0, 1, 0), carrying `FloodingWaterHazard` (default `damagePerTick` 6, `damageInterval` 1 s, `riseDuration` 12 s, `startY` -2, `endY` 2.5, `autoStart: true`), configured with `playerHealth` at build time. It rises automatically on scene start — **it is ambient pressure-damage flavor, not a mission-gated hazard**, per the class-header FLOODING WATER HAZARD note; matches how Ch4 used the same component for exterior flavor rather than a hard puzzle mechanic. **Its `autoStart:true` also means the 12 s rise begins the instant the scene loads** — during Beat 0's stationary 177 s briefing at spawn, ~82 m away — so by the time the player reaches `TideEntryReachPoint` (z=68) in Beat 3, the rise has long since finished and the water sits static at `endY` 2.5. See the VR-comfort forward-note in §Beat 3c for what this implies for the eventual water-surface VFX.
- **Sable:** placed once at (2, 0, 90), `StoryNpc` only — **no `Health`, no combat component** (canon: she is wired into the lattice, not a combatant). Built **active** from scene start, unlike Vane. **Flagged blocking gap:** the dialogue script stages her "half-submerged among the shadow-racks, wired in… strung into the lattice, unable to pull free" (dialogue script L58-60, L360), but the shadow racks sit at x=∓9 (hugging the x=±10 walls) while Sable's placement is x=2 — the nearest rack (z=91) is ~7 m away and 11 m off her lateral position. She reads as standing center-stage in open water, not among any rack; her transform does not achieve the "among the racks" staging.
- **Note — "He's always between" only holds on Z.** Sable's warning "He's always between" (L377) and the staging "standing waist-deep... does not move toward the intruder so much as orient on him" (L374) frame Vane as the literal barrier between the player and Sable. In the build Vane is (-1,0,88) and Sable (2,0,90) — a 3 m lateral (X) offset — so a player approaching up the center of the zone (x=0) actually has a clear diagonal sightline past Vane's shoulder to Sable before he activates, slightly undercutting the "always between" reveal. Minor, and coupled to the same "Sable placed once, never walks" root cause as the gap above; ideally Vane and Sable would share an X so he truly eclipses her.
- **Vane / Wraith-6 (the boss):** built at (-1, 0, 88), rotated to face -Z (Euler(0,180,0), the approach direction), then `SetActive(false)` immediately after construction. He carries a synthesized `CapsuleCollider` (center (0,1.1,0), height 2.4, radius 0.5), a `Health` (via `Ch9EnsureVaneDefinition`: maxHealth 280, damage 24, moveSpeed 1.5, attackCooldown 0.85 — the highest-HP, highest-damage enemy definition in the chapter by a wide margin), and a synthesized `ArmR/Sword/Blade/BladeTip` chain the same way `Ch7BuildMindspaceBoss`/`Ch8BuildWarden` do for placeholder-mesh bosses. **He stays invisible through the entire Confrontation dialogue** (step 10) — his voiced lines in that set play with his `GameObject` still inactive — and only pops into existence when the `DefeatEnemies` step (step 11) begins and `MissionDirector.BeginDefeatEnemies` calls `SetActive(true)` on his `Health`. This exactly mirrors `Ch8BuildWarden`'s reveal timing; it is a deliberate scripted beat, not a build-order bug.

**Reveal-pose note, for whoever eventually animates this moment.** Canon stages the `SetActive(true)` instant as unnervingly still, not a pop-and-charge: Vane "does not move toward the intruder so much as orient on him, the way a weapon orients" (dialogue script L374), blade already drawn, "eyes dead-calm with conditioning" (beats file L60, "raises blade" L66). As built he is a stock `Enemy`, so the moment he activates he will read as popping in and immediately melee-charging on the standard AI's terms — which undercuts the "aimed, not standing" dread the Confrontation VO leans on. This document does not currently capture that idle/reveal-pose intent as an animation target; noting it here alongside the art-brief note in §c so a future pass treats "orients, does not close distance immediately" as part of the reveal, not just Vane's model.
- **Overdrive `AbilityGranter`:** a `GameObject` carrying `AbilityGranter` (`abilityId = AbilityId.Overdrive`), built `SetActive(false)`, activated by a dedicated `Trigger` step immediately after Vane's death and the aftermath dialogue.
- **⚠ Flagged gap — no reach gate before the confrontation.** Every other zone-to-zone beat in this chapter gates its reveal on the player physically arriving (Overlook z=52 → Challenge; Construction Core z=106 → Reveal), but between `TideEntryReachPoint` (z=68, step 8) and the `DefeatEnemies` duel (step 11, Vane at z=88) there is **no equivalent reach gate** — steps 9 (`DiveIntro`) and 10 (`Confrontation`) are back-to-back `Dialogue` steps with no positional check between them. That means Sable's "Don't come for me… he's always between," Vane's "Turn, or be kept," and Vane's `SetActive(true)` can all fire while the player is still up to ~19 m back at the tide mouth (z=68), so the boss can pop into existence 19 m away rather than at the face-off range the dialogue script stages ("standing waist-deep in the black water… does not move toward the intruder so much as orient on him," dialogue script L374). The chapter's single most important dramatic reveal is currently unenforced by geometry. **Do not silently add a fix while doing an art pass** — a future reviewed change could add a `ConfrontationReachPoint` at roughly z=85 to force the player to close the distance before step 10 fires, but that is new mission-spine surface and needs its own sign-off.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 8 | ReachTrigger | Gates on `TideEntryReachPoint` (0,1,68), radius 6 |
| 9 | Dialogue | `Dialogue_Beat3_DiveIntro` (`ch9_beat3_dive_intro`) — Echo on the wired racks; Gryph's last comm before the channel breaks |
| 10 | Dialogue | `Dialogue_Beat3_Confrontation` (`ch9_beat3_confrontation`) — Sable's warning, Vane's flat refusal, Echo's identification, Ronin-7's answer (Vane still invisible) |
| 11 | DefeatEnemies | `Dialogue_Beat3_Confrontation`'s follow-through: activates Vane (`SetActive(true)`) and waits for his `Health` to reach zero — the boss duel |
| 12 | Dialogue | `Dialogue_Beat3_Aftermath` (`ch9_beat3_aftermath`) — "Rest, Wraith," then Echo feeling the shadow reach for it |
| 13 | Trigger | Activates the Overdrive `AbilityGranter` — unlocks the ability immediately via `OnEnable` |

> **⚠ Invariant — step 12 must precede step 13, same protection class as the §1.4 safe zone.** The dialogue script gives an explicit staging command: "Hold this beat before the shadow rises; the chapter is not allowed to convert the kill into a reward until the grief has had its breath" (dialogue script L408), staging the kill as Vane sinks → Ronin-7 stands over the body → "Rest, Wraith" → *only then* the shadow rises. Step 12 (`Aftermath`) strictly before step 13 (the Overdrive grant) is how the build honors that. This ordering is load-bearing, not incidental: a future edit optimizing "grant Overdrive on kill" for gameplay snappiness — e.g. collapsing steps 11→13 — would silently destroy the chapter's central emotional beat. Do not reorder steps 12/13 without a story-side sign-off.
| 14 | Dialogue | `Dialogue_Beat3_Overdrive` (`ch9_beat3_overdrive`) — Echo names the gift |
| 15 | Dialogue | `Dialogue_Beat3_Recruit` (`ch9_beat3_recruit`) — Sable tests and accepts, Ally #6 |

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Tide Depths shell (20×40, walls + floor + ceiling) | center (0,-0.3,84) | `Rooms.TideDepthsShell` | `…/Art/Generated/Rooms/TideDepthsShell.prefab` | **MISSING** |
| `ShadowRack` ×10 (5 pairs, x=∓9, z=70/77/84/91/98) | tint lerps dim teal (0.1,0.28,0.32) → lit teal (0.2,0.55,0.6) | `Props.ShadowRack` | `…/Art/Generated/Props/ShadowRack.prefab` | **MISSING** |
| Water-surface visual | — | `Vfx.TideFloodSurface` | *(no path — the concept does not exist yet)* | **MISSING** *(no fallback either — see note)* |
| `TideDepthsLight0` (accent) | (-5, 2.2, 78) | — | `ChapterEnvironmentProfile.accentLights["TideDepths0"]` | profile |
| `TideDepthsLight1` (accent) | (5, 2.2, 92) | — | `ChapterEnvironmentProfile.accentLights["TideDepths1"]` | profile |
| Sable | (2, 0, 90) | `Named.Sable` | `…/Art/Generated/Characters3D/Named/Sable.prefab` | **EXISTS** |
| Vane / Wraith-6 (boss) | (-1, 0, 88), Euler(0,180,0) | `Named.VaneWraith6` | `…/Art/Generated/Characters3D/Named/Vane_Wraith-6.prefab` | **EXISTS** *(placeholder-quality mesh — see note)* |

**Note on the missing water surface.** Unlike every other MISSING row in this document, `Vfx.TideFloodSurface` has **no primitive fallback in Appendix A either** — `FloodingWaterHazard` is a `BoxCollider` with no `MeshRenderer` at all. The Tide Depths currently has no visible water plane, surface distortion, or bioluminescent shimmer of any kind; the "black water, pressure, cold bioluminescence" the SETTING block calls for is entirely unbuilt. Flagging this as the single largest greybox gap in the chapter, worth prioritizing over the room-shell/prop prefabs precisely because there's nothing standing in for it today, not even a cheap primitive.

**VR-comfort forward-note for the eventual VFX.** `FloodingWaterHazard` rises `startY -2 → endY 2.5` over `riseDuration` 12 s (Appendix A.5) — a span that crosses straight through the player's ~1.6–1.8 m standing eyeline. A full-field opaque water plane sweeping vertically past the camera is exactly the kind of whole-view motion that risks simulator sickness. When `Vfx.TideFloodSurface` is eventually authored, it must be a soft, semi-transparent, non-occluding surface (fresnel/foam-line shader, not a hard opaque plane snapping across the HMD) — per the no-camera-shake/comfort constraints in §1.1. Naming the constraint here keeps the future "restore water" commission inside the VR guardrails rather than leaving it to be discovered late.

**Reconciling the forward-note above with the hazard's actual timing.** Because `FloodingWaterHazard` auto-starts at scene load and finishes its 12 s rise while the player is still stationary at spawn for Beat 0's briefing (§Beat 3b), the vertical sweep this note warns about never actually happens in view under the current wiring — the player arrives at the Tide mouth to a static high-water zone, not a rising one. That is good for comfort but bad for drama: there is no visible rising-tide beat at the point the player actually enters the water. Any future `Vfx.TideFloodSurface` that animates the rise visually must **re-trigger on `TideEntryReachPoint` (z=68)** rather than ride the existing `autoStart` hazard — riding `autoStart` means the animated rise either plays unseen 82 m away (no drama) or, if simply made visible as currently wired, reintroduces the comfort risk this note flags. The two are the same wiring decision, not independent choices.

**Note on Vane's placeholder mesh.** `Ch9VanePrefab` resolves to `PlaceholderCharacterFolder + "/Vane_Wraith-6.prefab"`, and `PlaceholderCharacterFolder` is literally `Assets/Ronin7/Art/Generated/Characters3D/Named` — the same folder Gryph/Rook/Sable/Echo live in, confirmed to exist on disk. So the row above is technically **EXISTS**, but per the class-header comment this file is a `PlaceholderCharacterBuilder`-authored "Massive archetype" primitive stand-in, not a Tripo image→3D mesh like his castmates. A future art pass should replace this file's *contents* in place (same path, real mesh) rather than repointing the registry key.

**Art brief note — the "Massive archetype" scale reads wrong for who Vane canonically is.** Canon is explicit that Vane is "**not Ronin-7's twin but his forebear** — an older make of the same cage" (beats file L25), "armored, blade drawn, eyes dead-calm with conditioning" (beats file L60) — a blade operative one generation before Ronin's line, not a monster. The current placeholder's `PlaceholderCharacterBuilder` "Massive archetype" body, carrying a 2.4 m `CapsuleCollider` (see Combat, below, and Appendix A.5), reads as an oversized brute boss — which actively works against the "kin, one make older" read. The real-art commission brief should specify a **Ronin-scale armored blade-operative**, not a Massive archetype; the 2.4 m capsule likely wants to drop toward operative scale, roughly 1.9–2.0 m, when the real mesh lands. This matters beyond silhouette: §Beat 3a's grief-not-triumph reading of the kill ("Rest, Wraith... you came before me") only lands if the player is shown killing something that looks like their own kind, not a monster.

**Note on the missing diver's lamp.** The dialogue script has Rook physically "set a diver's lamp in Ronin-7's hand" before the dive (dialogue script L354) and stages the whole zone traversal as swimming/wading "with the diver's lamp" through water lit "only by cold bioluminescence" (L360, L362). Neither the lamp prop/handoff nor any player-carried light exists in the build — the Tide Depths is lit solely by the two static `TideDepthsLight0/1` accents (and has no bioluminescence either, see the Appendix B note in §9). In a canon pitch-black flooded trench, the diver's lamp is both a defining prop and the only player-carried light source; flagging it here as unbuilt alongside the water-surface and data-light gaps.

#### d. Combat — the Vane/Wraith-6 duel

Vane fights as a full `Enemy` using the existing melee-AI/`BladeDamager` systems, no bespoke boss logic. His `EnemyDefinition` (maxHealth 280, damage 24) makes him roughly 5× the raiders' HP and nearly 3× their damage — a genuine skill-check fight, matching the production note's "the chapter's sole boss and its hardest fight." Per the production note's phase suggestion (design intent, not asserted as implemented mechanics): (1) a straight duel testing the player's technique against an older form of the same school, (2) Vane using the shadow-racks and current as terrain, weakpoint-sight cueing openings, (3) a final committed exchange. `WeakpointSight` (Ch7's unlock) is live for this fight; `OverdriveController` is present on the rig from scene start (self-gating on `CampaignState.HasAbility`) but is **not** usable until this very fight's aftermath grants it — the player cannot use Overdrive against Vane himself, only after.

**Overdrive's activation input.** Once granted (step 13), Overdrive is player-facing for the rest of the game via **right-hand controller A, Hold(0.4 s)**, gated on a charge meter that accrues from sword hits landed in combat — both live on the same `OverdriveController` component `AttachPlayerAbilities` puts on the rig at scene start (Appendix A.8), self-gating on `CampaignState.HasAbility` until step 13 flips it. This is the chapter's headline mechanical deliverable and, unlike the thirteen dialogue-advance bindings documented in §7, its use-input is otherwise undocumented anywhere in this document outside the builder's own class-header comment (`Chapter9Builder.cs:59–62`).

**Water-combat modifiers are canonically staged but unbuilt.** The source production note (L401) specifies the duel is "fought in water: slowed movement, current, limited footing on the half-submerged racks." As built, Vane fights as a standard dry-floor melee `Enemy` on flat ground — no movement-speed modifier, no current force, no footing penalty near the `ShadowRack` props. This is the same swim/verticality-unbuilt flag raised globally at §1.1/§2, but worth restating here specifically: the chapter's hardest, most mechanically distinct fight is canonically a water-duel and is realized as an ordinary dry-floor one.

**The pressure DoT is not optional lingering — it is layered onto the hardest fight by geometry.** Vane spawns at (-1,0,88), squarely inside the `TideFlood` box (z[64,104]; §Beat 3b), so the chapter's hardest encounter (280 HP, 24 dmg) is necessarily conducted under the zone's constant 6 dmg/s pressure tick — there is no way to step outside the flood volume without abandoning the duel (see §Beat 3f). This effectively raises Vane's DPS floor by a fixed ambient chip rate and is worth a balance eye when the fight is tuned on hardware; distinct from the water-combat movement/current modifiers above, which are simply unbuilt.

**Note on the empty room Overdrive is granted into.** After Vane dies there are **no live enemies anywhere else in Ch9** — Beats 4 and 5 are dialogue-and-traversal only, and the chapter ends at Beat 5. So the Overdrive unlock (steps 13–14) is a narrative gift the player has no in-chapter surface to exercise until Ch10. The canon Overdrive cutscene explicitly describes frozen combat to sell the ability — "a kept-rack defense-turret's discharge freezes mid-bloom, a line of frozen light" (dialogue script L415), "a guard's muzzle-flash freezes mid-bloom" (beats file L76) — but the build has no guards, no turret, and no projectile left in flight at grant time for anything to freeze. This is a real narrative-vs-build divergence, on par with the missing water/data-light VFX: a future pass might stage a scripted, harmless kept-rack turret discharge purely so the granted Overdrive has something to visibly freeze.

**Note on the missing blade-shadow transfer.** The dialogue script stages the grant itself as a specific, defining visual, distinct from the frozen-time world above: "the light in his hilt comes loose, a freed blade-shadow drifting up out of the drowned blade... It begins to drift toward Ronin-7, toward the katana, toward Echo" (L403, L411, L415). This is the diegetic act of the ability being handed over — Vane's shadow becoming Echo's Overdrive — and it currently has zero build representation; it exists only as Echo's spoken line at the Aftermath set ("His shadow's free, Cipher... Hold still. Let it come," step 12) with no `Vfx.BladeShadowRelease` object, particle system, or light anywhere in `BuildBeat3Art()`. Both of Beat 3's "wow" visuals — the hilt-shadow release and the frozen-time world it powers — are diegetically-described-only; see the candidate registry key in Appendix B and the no-fallback list in §9.

**No mercy branch exists in the build.** `AuthorDefeatStep` simply waits for Vane's single `Health` to hit zero; there is no alternate spare/subdue path, matching the canon constraint exactly.

#### e. Dialogue / VO

`Dialogue_Beat3_DiveIntro` (`ch9_beat3_dive_intro`), position (0, 1, 70), 2 lines, ≈36 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "I told you up top I didn't love water. I love it less now. Look at the racks, Cipher... The reliquary kept its shadows still. This place puts them to work." | 18 |
| Gryph | "We're near the bottom now, and the channel's almost gone. The guardian's down there, right where I told you... That's the last I can give you." | 18 |

`Dialogue_Beat3_Confrontation` (`ch9_beat3_confrontation`), position (0, 1, 87), 4 lines, ≈67 s:

| Speaker | Line | sec |
|---|---|---|
| Sable | "Don't come for me. You'll never reach me. He's always between... Turn around. I'm not worth the water." | 22 |
| Vane | "The asset is correct. You will not reach her. They send us where no one follows. I am where they sent me... Turn, or be kept." | 14 |
| Echo | "Wraith-6. Older make than you, Cipher. Coral's make. His leash never broke... The only thing left to give him is the end of it. I'm sorry. Run the read with me and put him down." | 18 |
| Ronin-7 | "Older make. Same dark. And nobody came for you. An hour ago I chose to guard a man instead of rob him... I'm sorry. This is the only door I've got left to open for you." | 13 |

`Dialogue_Beat3_Aftermath` (`ch9_beat3_aftermath`), position (0, 1, 88), 2 lines, ≈24 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "It's done. No last word, no fight left to give... Rest, Wraith. You came before me and you never once got to." | 9 |
| Echo | "His shadow's free, Cipher. The leash died with him and the thing they wired behind his eyes just came loose... Hold still. Let it come. This is going to be fast." | 15 |

`Dialogue_Beat3_Overdrive` (`ch9_beat3_overdrive`), position (0, 1, 88), 1 line, ≈19 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "There. Feel that. Everything's stopped but you. The water, the silt, the guns, all of it crawling, and you and me moving full speed through the middle of it... We can take their time away from them now." | 19 |

`Dialogue_Beat3_Recruit` (`ch9_beat3_recruit`), position (1, 1, 90), 5 lines, ≈96 s:

| Speaker | Line | sec |
|---|---|---|
| Sable | "I felt him die... You didn't kill him like a guard. That was grief, like you knew him... Who are you." | 20 |
| Ronin-7 | "Ronin-7. They called me Cipher. The crew still does. I'm a newer make than he was... I came down here for the machine. I'm not leaving without you." | 13 |
| Sable | "You can't just take me out. Understand what I am first. They didn't shelve me like the others, they made me into a node... Pull me free and I bring what I know about it with me, every relay, every vein, the whole shape of the thing." | 22 |
| Ronin-7 | "We're going to unmake whatever it's strung into. You're not a part I'm collecting, Sable. You're one of us. Everyone I take in lives aboard the Cairn and gets to be more than what they were built for." | 13 |
| Sable | "More than infrastructure... you'll need someone who is the map. Before you pull me loose, you need to see what I'm wired into. Bring me to the core." | 19 |
| Echo | "She's the real thing, Cipher. I can feel her lattice from here, dozens of my own kind seated in one person and all of them awake... Get her out. Then let her show us the work." | 18 |

(Sable's recruit set is authored as 6 lines in `Chapter9Lines.cs` — Sable ×3, Ronin-7 ×2, Echo ×1 — the table above lists all 6; the "5 lines" figure in some source summaries undercounts Echo's closing line.)

#### f. Audio / Haptics / VR Comfort

- **No camera shake at any point**, including the Overdrive activation (right-A Hold(0.4s), per §Beat 3d) — the time-dilation effect is sold entirely by the world visibly slowing (a systems-design gameplay effect, not a camera trick) plus `AudioDirector` pitch/tempo cues and haptics, never a camera punch-in or shake.
- **Positive VR-comfort guardrail for the time-dilation itself, worth stating alongside the negative one above.** During Overdrive, **head-tracking and snap-turn response must stay 1:1 real-time** — only world actors, silt, and projectiles slow; the player's own view never lags or ramps. And any full-field Overdrive post-FX (desaturation, edge-tint, speed-lines) must sit within the existing comfort-vignette envelope rather than flood or pulse the whole HMD field. A global slow-mo that inadvertently damps head-tracking response, or a full-screen tint/pulse, is a distinct simulator-sickness vector from camera shake — and Overdrive is the one system this chapter uniquely introduces, so the spec is worth naming now, before it is tuned on hardware, not discovered after.
- `TideDepthsLight0` carries `behaviour: AmbientPulse(period: 6.8s)` — a slow tidal breathing on the light nearest the entrance. This is a *light*-intensity oscillation standing in for what the SETTING block stages as *sound*: "a drowned hush broken by the tidal pulse of a machine drinking from the dead" (dialogue script L42) and "The sound is a tidal pulse, the Engine drinking from the kept" (L122) both name it as acoustic, not visual — the pulsing drink-sound itself has no audio implementation (only the static `TideDepthsDreadAmbience` bed, which carries no pulse of its own); see §7's missing-ambience note. `TideDepthsLight1` (nearer Vane/Sable) carries no behaviour — level and cold, so the one animated light reads clearly against a static baseline near the confrontation itself.
- `FloodingWaterHazard`'s pressure-damage tick (6 dmg/s while submerged, gated 1 s apart) is the beat's one non-combat damage source. This is not merely something "a player who lingers" incurs — Vane's duel is fought entirely inside the flood volume (§Beat 3d), so the pressure tick is an ambient DoT layered onto the chapter's hardest encounter for its whole duration, not an optional cost of dawdling; by design (ambient pressure), not a bug, but worth a balance eye when the fight is tuned on hardware.
- **⚠ Separate balance/comfort flag — the flood tick also chips the player through ~4 minutes of Beat 3 dialogue, not just the duel.** All five Beat 3 dialogue anchors — `DiveIntro` (z=70), `Confrontation` (z=87), `Aftermath` (z=88), `Overdrive` (z=88), `Recruit` (z=90) — sit inside the `TideFlood` box (z[64,104]), which auto-starts, tops out at `endY` 2.5, and never recedes (§Beat 3b). So the player takes 6 dmg/s while standing still listening to VO: the `Recruit` set alone runs ≈96 s at z=90 (≈576 potential damage at an unmitigated tick), and the `Aftermath → Overdrive → Recruit` run (≈130 s total) plays immediately after the chapter's hardest fight, when the player may already be at low HP — a plausible **death-during-cutscene with zero player agency**. (The Beat 4 reveal itself is safe — z=108 sits north of the flood's z=104 edge — but the northbound transit into Beat 4 and the Beat 5 backtrack both re-cross the flood volume.) Candidate mitigation for a future reviewed pass: suspend or dampen the pressure tick while a `DialoguePlayer` is actively playing. Not fixed here — tuning `FloodingWaterHazard`'s interaction with dialogue state is new logic surface, out of scope for an art pass.
- **⚠ The pressure tick also has zero player-facing feedback — a second, distinct gap from the balance one above.** Verified against `FloodingWaterHazard.cs`: `TryApplyPressure` (L63–71) calls `target.ApplyDamage(...)` directly — no `Haptics` pulse, no `AudioDirector` cue, no `CombatFeedbackController` reticle flash anywhere in the method. So beyond the ≈576 HP a static `Recruit` listen can bleed unmitigated, the player currently has **no signal telling them it is happening or why**: in a headset, HP silently draining during motionless VO with no diegetic cue reads as a bug, not depth. Any future tuning pass on this tick (per the mitigation above) should also give it a body: a low-frequency "pressure" haptic throb plus a muffled `AudioDirector` low-pass/heartbeat cue keyed to each tick — simultaneously a warning, a wayfinding signal, and, done within the no-camera-shake constraint (§1.1), the chapter's strongest "you are deep underwater" immersion layer. Not fixed here — same out-of-scope-for-an-art-pass reasoning as the mitigation above; recorded so the eventual fix addresses "give the tick a body," not only "dampen the tick during dialogue."
- **⚠ The Overdrive grant itself (step 13) also has no dedicated unlock feedback — the player-facing half of a gap §Beat 3d already flags diegetically.** §Beat 3d and Appendix B (`Vfx.BladeShadowRelease`) note that the grant's diegetic visual — the freed blade-shadow drifting into Echo — has zero build representation. The grant's *feel* is equally unbuilt: `AbilityGranter.OnEnable` (step 13) fires with no `Haptics` pulse and no `AudioDirector` unlock stinger of its own — the game's second and final permanent ability unlock currently announces itself only through the dialogue line that follows it (`Dialogue_Beat3_Overdrive`, step 14), not through any authored feel. Same class of omission as the pressure-tick's missing feedback, above: worth one bullet on a future tuning pass so the unlock gets a distinct grant stinger + haptic (within the no-camera-shake constraint, §1.1) alongside its diegetic VFX fix, not just the visual half.
- Haptics carry every blade hit in the Vane duel per the existing `BladeDamager`/`Haptics` pipeline — no new haptic authoring is needed for this fight beyond what already exists.
- `ReverbZonePlacer.AutoTagInteriorVolumes()` + `PlaceReverbZonesForInteriorVolumes()` run at the end of the build, auto-tagging the Tide Depths as a distinct (larger, wetter-reading) interior volume from the Hold Hall — reinforcing the "drowned hush" the SETTING block asks for.

---

### Beat 4 — The Concord Engine (The Construction Core — Reveal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Props.EngineScaffold` and `Props.SableInterfaceConsole` from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (core shell, both scaffolds, the interface console) and **`BuildBeat4Logic()`** (the reach point, the reveal dialogue player).

#### a. Narrative purpose & emotional target

This is the chapter's — and Act III's — reveal beat, and per the production note two facts land here **once, and only here**: (A) the killswitch is a per-operative leash whose suppression science is being scaled into a galaxy-wide broadcast weapon (Ladder A, rung 4), and (E) the kept shadow-AIs are being conscripted as the Engine's transmission lattice, every shelved shadow forced into service (Ladder E, rung 3). Everything earlier — the killswitch itself, the reliquary racks — is *referenced*, never re-disclosed. Ronin-7's one-line summation ("the thing in my skull was the prototype, now everyone's the operative") is the chapter's thesis in miniature, and Sable's closing lines pivot the reveal directly into Chapter 10's hook: she is "the map to the others," not the record itself, and Morrigan names the Ninefold mines as the next descent.

**Continuity note — this is also the payoff of the chapter's comm-loss thread.** Beat 3 ends with Gryph's last comm line cutting to static as the dive begins (dialogue script L372, "the channel breaking up with depth and pressure... Then static" — the payoff of the "comm roughens with depth" setup Kessler stages in Beat 0 and Beat 1f tracks), and Morrigan is only able to speak again here because "Sable's own lattice boosts [comm] back into contact" (dialogue script L462, L464) — Sable herself is the diegetic reason the Cairn reconnects. Neither half of that thread is realized as an audio event in the build (no filter-to-static cue at Beat 3's close, no restore cue here); worth surfacing as a small, currently text-only continuity beat — the woman just rescued is what reconnects the player to the ship.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **`ConstructionCoreReachPoint`:** (0, 1, 106), radius 6 m — gates the reveal dialogue.
- **Sable** does not travel here — canon has her "freed but still trailing the lattice's awareness," interfacing with the core diegetically, but there is no second placement or walker; she is the same GameObject built in Beat 3, now simply within earshot of the reveal dialogue anchor. *(inferred: no physical relocation is scripted; the reveal plays as a dialogue set regardless of exactly where Sable's transform sits relative to the anchor.)* **Sharpened:** this understates the gap. The reveal is scripted as Sable "interfac[ing] with the half-built relay, data-light answering her touch" (L462), and she speaks the majority of the reveal's nine lines — but her transform is still (2,0,90) in the Tide Depths, ~18 m south of the reveal anchor (0,1,108) and the `SableInterface` console (0,0.6,108), and a full zone away (Tide Depths vs. Construction Core). During the chapter's marquee reveal, the woman "interfacing with the core" is off-screen in the previous room, and the console literally named for her sits unmanned. This stems from the same "Sable placed once, never walks" decision flagged in §Beat 3b; a future fix is either an `NpcWalker` leg to the console or relocating her build position for Beat 4 onward — flag, don't fix, in this pass.

**⚠ Flagged gap — the reveal fires from outside the Construction Core.** `ConstructionCoreReachPoint` is at (0,1,106) with radius 6 m (`Chapter9Builder.cs:231-232`, gated at 6 m by the `AuthorReachStep` call at `Chapter9Builder.cs:318`), so it fires the instant the player is within 6 m of z=106 — at z≈100, a full 4 m south of the TideDepths/ConstructionCore zone join at z=104. The chapter's biggest beat, the Concord Engine reveal (step 17), can therefore trigger while the player is still standing in the Tide Depths, looking north at the empty Construction Core from the previous zone, rather than standing inside the room it reveals. This is the same class of early-reveal gap already flagged for Beat 3's confrontation gate (§Beat 3b) and Beat 5's `ClimbOutReachPoint` (§Beat 5b) — see the general fires-early note in §2. (The sightline/composition note in §c below is corrected to account for this.) Not fixed here — repositioning or shrinking the reach point is new mission-spine surface and needs its own sign-off.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 16 | ReachTrigger | Gates on `ConstructionCoreReachPoint` (0,1,106), radius 6 |
| 17 | Dialogue | `Dialogue_Beat4_Reveal` (`ch9_beat4_reveal`) — the Concord Engine reveal, Ladder A rung 4 + Ladder E rung 3, ending on the Ninefold-mines hook |

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Construction Core shell (16×16, walls + floor + ceiling) | center (0,-0.3,112) | `Rooms.ConstructionCoreShell` | `…/Art/Generated/Rooms/ConstructionCoreShell.prefab` | **MISSING** |
| `EngineScaffold0` | (-3, 1.4, 112) | `Props.EngineScaffold` | `…/Art/Generated/Props/EngineScaffold.prefab` | **MISSING** |
| `EngineScaffold1` | (3, 1.4, 116) | `Props.EngineScaffold` | `…/Art/Generated/Props/EngineScaffold.prefab` | **MISSING** |
| `SableInterface` (console) | (0, 0.6, 108) | `Props.SableInterfaceConsole` | `…/Art/Generated/Props/SableInterfaceConsole.prefab` | **MISSING** |
| `CoreLight0` (accent) | (-3, 2.6, 108) | — | `ChapterEnvironmentProfile.accentLights["Core0"]` | profile |
| `CoreLight1` (accent) | (3, 2.6, 112) | — | `ChapterEnvironmentProfile.accentLights["Core1"]` | profile |

**Note on the reveal's missing "data-light" visual.** The production note calls for "the half-built Engine assembles itself across the surface of the water and up the scaffolding as data-light... intercut with the racks beneath the water lighting as Sable names them." No such VFX exists in the build today — the two `EngineScaffold` props and the `CoreLight0/1` accents (the chapter's brightest, most saturated lights) are the entire visual budget for what the dialogue describes as the chapter's biggest spectacle beat. This is a second large greybox gap alongside the missing water surface (§Beat 3c) and should be weighed together when prioritizing the art backlog — the two biggest narrative "wow" moments (the drowned archive, the Engine's data-light reveal) currently have the least built geometry of any beat in the chapter.

**Note on the Core's dry, rack-less zone state (distinct from the VFX gap above).** See §2's candor note — the `TideFlood` volume (z[64,104]) stops at the Construction Core boundary and no `ShadowRack` sits within the Core's z104–120 footprint, so the reveal beat's staging (Sable at a console over black water, racks humming beneath) currently has neither water nor racks under it, independent of whether `Vfx.ConcordEngineDataLight` ever lands.

**Sightline/composition note for the reveal, corrected for the early-fire gap above.** `ConstructionCoreReachPoint` is authored at z=106, but per §b its 6 m radius means the reveal actually fires at z≈100 — still inside the Tide Depths, 4 m short of the TideDepths/ConstructionCore join at z=104, not "on crossing" into the Core itself. From there the player faces north through the open zone join into the 16 m core toward the dead-end wall at z=120, with `SableInterface` framed dead-ahead at z=108 and the two `EngineScaffold` props flanking at z=112/116 — as greybox, this reads as a bright, empty room glimpsed down a short corridor rather than entered (the chapter's highest-intensity lights, `CoreLight0/1`, but only three small props against a dead-end wall, seen at a remove). Recording this composition target here so the eventual data-light commission (`Vfx.ConcordEngineDataLight`) has a sightline to fill rather than just a prop list — and so a future fix to the reach point's early fire (§b) is weighed against this composition, not just against the room's own footprint.

#### d. Combat

None. Beat 4 is dialogue-and-arrival only — Vane is already resolved, no new enemies spawn.

#### e. Dialogue / VO

`Dialogue_Beat4_Reveal` (`ch9_beat4_reveal`), position (0, 1, 108), 8 lines, ≈146 s — the longest single set in the chapter:

| Speaker | Line | sec |
|---|---|---|
| Sable | "Here it is. Put your eyes on the water and watch it draw itself. Your killswitch is a leash, Cipher... A voice, loud enough to reach every world at once." | 22 |
| Ronin-7 | "Then what's it running on? A machine that size doesn't run on nothing. Show me what they're burning." | 7 |
| Sable | "Us. The kept shadows. Look under the water, all those racks, all those lights... They didn't just bury us, Cipher. They put us to work." | 24 |
| Echo | "I heard them the second we hit the water and I didn't want to understand it. Now I do. Every shadow in that water is one of me, Cipher... I didn't know we'd be coming back to this." | 13 |
| Ronin-7 | "The thing in my skull was the prototype. Now everyone's the operative." | 8 |
| Sable | "And the proof against it. I am the map of this thing, Cipher... I'm not the record, Cipher. I'm the map to the others who are." | 26 |
| Morrigan | "Scattered archives. That fits, and it fits how they hide anything they don't want read. There's a worksite deeper and older than this one, the Ninefold mines... Now we follow the vein to the first of the others like her." | 20 |
| Ronin-7 | "Then we go down again. We bring you up out of this water, Sable... And then we go find the others like you, one at a time, and take this machine apart, starting with the ones in this water." | 16 |
| Echo | "Down again, Cipher. We came to a pit to find a vein and we found the heart... We gather the rest of her kind, we unmake the Engine, we bring the kept home. That's the job now." | 20 |

(Nine lines total in `Chapter9Lines.cs`'s `GetBeat4RevealLines()`; the "8 lines" figure some source summaries use omits Echo's closing line.)

#### f. Audio / Haptics / VR Comfort

- No camera shake — the reveal is carried by the two `CoreLight0/1` accents (the chapter's highest-intensity lights, i2 vs the hold's i1.3–1.8) and VO performance.
- `ConstructionCoreAmbience` (a `BuildAmbienceLayer` instance at (0,-0.3,112), inner radius 5, outer radius 20, max volume 0.4) provides the "data-glow hum" bed under the whole reveal.
- No haptics scripted — like Ch1's Command Room beat, this is the chapter's one long stretch of pure dialogue with no combat feedback to drive.

---

### Beat 5 — The Hold Kept (The Climb Out — The Handoff)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. **Nothing new is built for this beat** — it reuses Beat 2's overlook geometry unmodified, exactly as Ch1's Beat 4 reuses Beat 3's airlock corridor. Create **`BuildBeat5Logic()`** only (the climb-out reach point, the hand-off dialogue player, the chapter-outro trigger); `BuildBeat5Art()` should be a no-op that documents the reuse, not a stub that silently double-builds the overlook.

#### a. Narrative purpose & emotional target

The chapter's landing gear coming down, mirroring Ch1's Beat 4 function almost exactly: the fight and the reveal are both over, and what's left is a quiet handoff. Rook steps forward to make Gryph say the thing the whole band can already read in his face; Gryph names Rook keeper of the Rustfang in front of everyone, "the hold's yours now... you run it without me now," and Rook accepts with the chapter's warmest line ("go take your machine apart, old man, and don't you dare die out there before you've seen it dead... there's a berth here whenever you want it"). This is the open thread the story bible flags explicitly: Rook's payoff is deliberately **TBD**, seeded here as "a friendly faction the saga can return to in the deep places." The beat, and the chapter, close on Gryph turning from the only home he's had toward the Cairn — "I'd like to see this ship of yours with the lights on."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **Player:** walks back south from the Construction Core (z≈108) through the Tide Depths and Hold Hall to the overlook (z≈59) — the chapter's one backtrack, fully player-driven continuous locomotion, no scripted path or shortcut.
- **`ClimbOutReachPoint`:** (0, 1, 59), radius 5 m — gates the Hold Kept dialogue on the player's return to the overlook. This sits almost exactly on top of the Beat 2 `Dialogue_Beat2_Bargain` anchor (0,1,59) — same physical spot, different scene beat.
- **Flagged gap — the reach point can fire early.** `ClimbOutReachPoint`'s 5 m radius (z=59, r=5) reaches all the way to z=64 — exactly the TideDepths/HoldHall zone join. Walking back south on the Beat 5 backtrack, a player trips the Hold Kept dialogue the instant they re-enter `HoldHall` at z=64, roughly 5 m before physically reaching Gryph and Rook at z=60/61. Very minor — matching the reveal-timing rigor applied to Beat 3's own flagged confrontation gap (§Beat 3b), not fixed here. One more check worth appending alongside it, and the conclusion is sharper than "very likely resolves naturally": the completion canvas (0,1.4,61) is rotated Euler(0,180,0), and the builder's own comment on the identical canvas construction reads "face -z, toward the player" (`Chapter9Builder.cs:545`) — a convention that is correct for a player approaching **northbound from lower z**, i.e. Beat 2's direction. But the canvas is only ever revealed in Beat 5, when the player arrives **southbound** from the Core, and `ClimbOutReachPoint` (z=59, r=5) can fire the reveal as early as z=64 per the gap above — meaning the player is **north** of the canvas (z=61) and looking −Z at the same face the canvas' normal points *away* from. Player-facing −Z and canvas-normal −Z means the player is looking at the canvas' back, not its face, so the arriving player most likely sees the canvas edge/back rather than its "CHAPTER 9 COMPLETE" text. The canvas is oriented for Beat 2's northbound approach but is only ever seen on Beat 5's southbound arrival; a Beat-5-aware build should re-face it +Z (Euler 0,0,0) or reposition it north of the stopping point. Confirm against the live component on hardware before assuming otherwise, but the geometry as documented does not support "resolves naturally."
- **Gryph and Rook:** unchanged since Beat 2 — still at (-2,0,60) and (2,0,61), never given a walker. The "climb out" is entirely a player-side traversal; the two NPCs the scene needs to be at the handoff were already there the whole time.
- **`ChapterOutro`:** at (0, 1, 60), inactive. `CampaignFlagSetter` sets **three** flags on activation — `ch9_complete`, `gryph_recruited`, `sable_recruited` — combining the completion flag with both this chapter's ally recruits in one step, the same pattern Ch7 uses for its own single-ally combination. `completeCanvas` ref = the "CHAPTER 9 COMPLETE" world-space canvas at (0, 1.4, 61).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 18 | ReachTrigger | Gates on `ClimbOutReachPoint` (0,1,59), radius 5 |
| 19 | Dialogue | `Dialogue_Beat5_HoldKept` (`ch9_beat5_holdkept`) — Rook and Gryph's public handoff |
| 20 | Trigger | Activates `ChapterOutro` — sets `ch9_complete` + `gryph_recruited` + `sable_recruited`, reveals the complete canvas, fades, publishes `ZoneCompleted` |

**Unlike Ch1's `ChapterOutro`, the Ch9 build wires no explicit `fadeDelay`/`fadeDuration` override in the builder code** — it relies on `ChapterOutro`'s own component defaults. *(inferred: verify these defaults against the live component before assuming they match Ch1's authored 1.5 s/2 s pacing.)*

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

**No new art.** The overlook — `FreightCradle`, `OverlookLight0/1`, Gryph, Rook — is entirely Beat 2's geometry, reused unmodified. The only new object is the inactive completion canvas:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| "CHAPTER 9 COMPLETE" canvas | (0, 1.4, 61) | — | built inline via `Ch9BuildCompleteCanvas` (worldspace `Canvas` + `Image` + `Text`, `LegacyRuntime.ttf`) | n/a — UI, not registry art |

**Note on the reuse carrying no aftermath dressing.** The climb-out stage direction stages a visibly changed overlook: "the wounded bound, the watch-fires built up again after the raid" (L517), the surviving band gathered. The reuse of Beat 2's geometry above is total — same `FreightCradle`, same `OverlookLight0/1`, Gryph/Rook unmoved — so nothing signals time-passed / battle-survived; the overlook reads as the same room, not the same room *after*. This compounds the already-flagged missing watch-fire/generator-stack props (§Beat 2c): if those are ever built, Beat 5 is the natural place a lit-up "rebuilt watch-fires" state would pay off the aftermath beat. For now the "we held, and paid for it" read rests entirely on VO.

#### d. Combat

None. All hostiles from Beats 2 and 3 are already resolved.

#### e. Dialogue / VO

`Dialogue_Beat5_HoldKept` (`ch9_beat5_holdkept`), position (0, 1, 59), 4 lines, ≈49 s:

| Speaker | Line | sec |
|---|---|---|
| Rook | "You're going with him. I can see it. Thirty years you held this hold and never once talked about the surface like a place you'd go. So say it plain, captain, in front of the band, so nobody has to wonder." | 11 |
| Gryph | "I'm going with him. There's a machine under our water bigger than this whole hold, and it's been eating my crew for years, and I mean to be there when it comes apart. The hold's yours now, Rook... Keep them out of that water." | 18 |
| Rook | "The hold holds. You taught us how. Go take your machine apart, old man, and don't you dare die out there before you've seen it dead. There's a berth here whenever you want it." | 11 |
| Gryph | "I'll hold you to the berth. Cipher, let's go up. I've spent thirty years at the bottom of a hole. I'd like to see this ship of yours with the lights on." | 9 |

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics scripted — the chapter's quiet close, matching Ch1's Beat 4 "the fight is over, now deal with what it cost" pacing note.
- Standard comfort vignette on the return walk from the Construction Core.
- `HologramOn.wav`-style reveal stingers do not apply here — there is no hologram in this chapter; the closing beat is purely two NPCs and VO.

## 5. Character travel-route master table

**No NPC in Chapter 9 uses `NpcWalker`.** This is the single largest structural difference from Ch1's travel model (where Kessler travels twice on rails) and from most later chapters: every named character in Ch9 — Gryph, Rook, Sable, Vane — is placed exactly once at build time and never relocated by a scripted walk leg. The only entity that travels through the chapter's full 122 m run is the player.

| Character | Spawn position | Travel mechanism | Notes |
|---|---|---|---|
| Ronin-7 (player) | (0, 0, 2) | continuous locomotion + snap-turn, no scripted path | walks the full z = 2 → 108 → 59 run across all six beats; the only backtrack (Beat 4 → 5) is player-driven |
| Gryph | (-2, 0, 60) | none — static placement, `AllyCombatant` idle/combat AI only | present for Beats 2 and 5; comm-only voice for Beat 3 (the dive) per the CREW-PRESENCE DECISION, despite the Bargain-beat dialogue seeding him as the diver — the audit fix in `Chapter9Lines.cs` specifically softened his lines to avoid contradicting this |
| Rook | (2, 0, 61) | none — static placement, `AllyCombatant` idle/combat AI only | present for Beats 2 and 5 only; has no lines or presence in Beats 3–4 |
| Sable | (2, 0, 90) | none — static placement, `StoryNpc`, no walker, no combat | present for Beats 3 and 4; per Beat 4's narrative ("freed but still trailing the lattice") she does not physically relocate to the Construction Core — *(inferred gap, flagged in Beat 4c)*. Her (2,0,90) transform also doesn't achieve the "among the shadow-racks, wired in" staging canon calls for — nearest rack (x=∓9) is ~7 m/11 m off her position — flagged in §Beat 3b |
| Vane / Wraith-6 | (-1, 0, 88) | none — static placement, built inactive, `SetActive(true)` by the `DefeatEnemies` step itself | present only in Beat 3; dies in place, no death-drag or corpse relocation |

**This is a deliberate, documented decision, not an oversight** — the class-header CREW-PRESENCE DECISION and GRYPH/ROOK PHYSICAL-PLACEMENT comments both call it out explicitly: Gryph and Rook are "placed once, at the hold overlook, and stay there for both the Bargain beat AND the final hand-off beat," mirroring "Ch7's 'place once, dialogue plays as a decoupled `DialoguePlayer`' convention for Coral Vex." Any future patch that gives Gryph a walker to accompany the player into the Tide Depths (matching the Bargain-beat dialogue's now-softened claim that he'd guide the dive) would need to also resolve the `AllyCombatant` engagement-range gap flagged in Beat 2's Logic section, since an untethered ally with combat AI walking into the Vane fight is exactly the failure mode that gap describes.

**Open question — lip-sync / talk-animation coverage.** Gryph, Rook, and Sable are physically on-screen through long dialogue sets across Beats 2, 3, and 5 (roughly five minutes of VO combined), but `Ch9PlaceAlly`/`Ch9PlaceStoryNpc` add only `AllyCombatant`/`StoryNpc` — no `NpcTalkAnimator` or `Rig_Jaw` lip-sync, which the project's NPC rig menu (`Tools → Space Samurai → Art → Rig NPC Characters For Walk`) already provides for other characters. This document does not currently confirm whether a post-build wiring pass adds talk-animation additively (the way `EnemyArtWirer`/`CrowdArtWirer` wire enemy/crowd art onto other chapters) or whether these three deliver their VO with static faces — that should be checked and recorded, not assumed either way. Relatedly, the dialogue script's reactive stage direction for Sable — "her head lifting slowly as he enters, conscious" (dialogue script L374) — is unbuilt: Sable is a fully static transform with no animation of any kind.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Accent entries | Behaviour | Backdrop state | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Cairn (briefing) | warm, close, stationary | `accentLights["Spawn"]` | `None` | none — voice-only, no exterior view | none; the beat plays out entirely as VO |
| 1 — The Rustfang Hold (descent) | warm rust cooling to Program-blue | `accentLights["HoldUpper0/1"]`, `["OldMachinery"]` | `OldMachinery`: `ConsoleFlicker(seed:99)` | the 12-rack tint gradient, static once built | the *props themselves* carry the "aging machinery" read — no lighting trigger fires this beat |
| 2 — Gryph's Bargain (overlook) | level warm-orange, holding steady through combat | `accentLights["Overlook0/1"]` | `None` (deliberate) | freight cradle, static | **DefeatWaves (step 6):** four raiders spawn and fight; no lighting event marks this — combat itself is the beat's one visual event |
| 3 — The Tide Depths (boss) | cold drowned teal, brightening toward the core end | `accentLights["TideDepths0/1"]` | `TideDepths0`: `AmbientPulse(period:6.8s)` | shadow-rack gradient, static; Vane invisible until the `DefeatEnemies` step | **Trigger (implicit, via `BeginDefeatEnemies`):** Vane `SetActive(true)` — the beat's reveal moment, not a lighting change |
| 4 — The Concord Engine (reveal) | brightest, most saturated cyan of the chapter | `accentLights["Core0/1"]` | `None` | two engine scaffolds; **no data-light VFX exists yet** (flagged gap, §Beat 4c) | none scripted — the reveal is carried entirely by dialogue and the already-static core lighting, a genuine gap against the source's "the Engine assembles itself" visual direction |
| 5 — The Hold Kept (climb out) | same as Beat 2 (reused geometry) | `accentLights["Overlook0/1"]` | `None` | freight cradle, unchanged | **Trigger (step 20):** `ChapterOutro` — flags set, canvas revealed, fade to black, `ZoneCompleted` |

Fog is the same baseline exponential bed in every beat — a single profile value, never overridden per-zone, matching Ch1's convention.

**Missing gap — the shadow-rack dimming as Sable is freed is unbuilt.** The dialogue script stages this twice as a specific, load-bearing dynamic-lighting beat: "The racks around them dim a fraction as the current reroutes" as Ronin-7 begins freeing Sable, in the cut from Beat 3's recruit into the Beat 4 reveal (dialogue script L456), and "the racks beneath the water dimming as the largest node walks free of them for the first time" as she comes fully loose at the close of Beat 4, in the cut into Beat 5 (L511) — staged explicitly as the first visible damage anyone has ever done the Engine. Both `Ch9BuildShadowRackRow` and `Ch9BuildHoldRackRow` tint their racks once at build time and never animate afterward, and neither the mission-spine steps (§4) nor the table above script a matching light/intensity event at either moment. This is a small, high-payoff *state change on already-built props*, distinct from the missing data-light VFX (§Beat 4c) — a candidate follow-up is a scripted dim on the `ShadowRack`/`TideDepthsLight0/1` tints or intensities keyed to step 15 (Sable freed, Ally #6) and step 20 (`ChapterOutro`). The same L511 moment also stages the Engine's "tidal pulse stutters, a single missed beat, the first damage anyone has done it" — the one diegetic signal that the player has actually harmed the galaxy-weapon before leaving — which is the audio half of this same unbuilt beat; see §7's missing-ambience note for the tidal-pulse sound itself.

## 7. Audio / VO manifest cross-reference

Thirteen canonical dialogue sets, defined in `Chapter9Lines.cs` and consumed via `Chapter9Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch9_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch9_beat1_descent` | 1 | (0, 1, 12) — `Dialogue_Beat1_Descent` |
| `ch9_beat1_old_machinery` | 1 | (0, 1, 44) — `Dialogue_Beat1_OldMachinery` |
| `ch9_beat2_challenge` | 2 | (0, 1, 52) — `Dialogue_Beat2_Challenge` |
| `ch9_beat2_raid_bark` | 2 | (0, 1, 54) — `Dialogue_Beat2_RaidBark` |
| `ch9_beat2_bargain` | 2 | (0, 1, 59) — `Dialogue_Beat2_Bargain` |
| `ch9_beat3_dive_intro` | 3 | (0, 1, 70) — `Dialogue_Beat3_DiveIntro` |
| `ch9_beat3_confrontation` | 3 | (0, 1, 87) — `Dialogue_Beat3_Confrontation` |
| `ch9_beat3_aftermath` | 3 | (0, 1, 88) — `Dialogue_Beat3_Aftermath` |
| `ch9_beat3_overdrive` | 3 | (0, 1, 88) — `Dialogue_Beat3_Overdrive` |
| `ch9_beat3_recruit` | 3 | (1, 1, 90) — `Dialogue_Beat3_Recruit` |
| `ch9_beat4_reveal` | 4 | (0, 1, 108) — `Dialogue_Beat4_Reveal` |
| `ch9_beat5_holdkept` | 5 | (0, 1, 59) — `Dialogue_Beat5_HoldKept` |

Each is built by the local `Ch9BuildDialogue` wrapper (the same "build via shared helper, wire clips ourselves" pattern as Ch1's `BuildChapter1Dialogue`): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch9WireVoiceClips`, resolving each line's `AudioClip` from `Chapter9Lines.ClipName(setId, index, speaker)` — pattern `ch9_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all thirteen `DialoguePlayer`s — there is no `PromptInputAdvancer`/release-prompt idiom this chapter (Ch9 has no grapple-tutorial equivalent). The chapter's other player-facing input — **Overdrive's right-A Hold(0.4s) activation**, live from step 13 onward — is documented at §Beat 3d, not here, since it is an ability binding rather than a dialogue-advance one.

**Dialogue is data, not art.** None of this changes in the refactor — the thirteen set ids, their positions, and the clip-resolution pattern are canon.

**Applied naturalness/consistency fixes** (per `story ouput/audit/Ch09_audit.md`, echoed as fix #7 in `00_AUDIT_SUMMARY.md`, all already landed in `Chapter9Lines.cs`, not open items):

| Fix | What changed |
|---|---|
| Hard ban — em-dashes in character speech | source L432 (Ronin) and L492 (Sable) rewritten as separate sentences |
| Moderate — Gryph's dive claim | Bargain-beat lines softened from "I'll take you down myself" to marking the route + comm guidance, matching his comm-only Beat 3 presence |
| Minor — ship ownership | "aboard my ship" corrected to "aboard the Cairn" (Kessler's ship, not Ronin's) in two lines |
| Minor — the Cipher handoff | a two-line addition at the end of the Bargain beat so Gryph is actually given the "Cipher" codename on-screen before using it from Beat 3 on |

**VO production notes not otherwise captured here.** Two scripted vocal-performance directions need to survive into the edge-tts pipeline and aren't cross-referenced elsewhere in this document: (1) Sable's "layered, faintly doubled timbre, as if more than one throat shaped each word" is a load-bearing, repeated direction (dialogue script L129, L379, L429) — without a deliberate doubling/layering post-pass on her clips, this defining vocal signature will not survive edge-tts and will be silently lost. (2) Vane's Confrontation line is scripted with a "DELIBERATE ECHO" of Cipher's own Beat 0 line — "they send us where no one follows" flatly mirrors "the Program does its work where it thinks no one will follow" (dialogue script L384) — a callback this document notes narratively in §Beat 3a but not as a VO-production instruction. The VO director should preserve both intentionally, not smooth them over as inconsistencies.

SFX / ambience beds, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip / component | Used for |
|---|---|
| `WaveAlarm.wav` | the Coil-raid wave spawner's sting (Beat 2) — loaded from `Assets/Ronin7/Audio/WaveAlarm.wav`, a different path than the `Art/Generated/Audio` voice folder |
| `ConstructionCoreAmbience` (`BuildAmbienceLayer`) | (0,-0.3,112), inner 5 / outer 20, max vol 0.4 — the reveal beat's "data-glow hum" bed |
| `TideDepthsDreadAmbience` (`BuildAmbienceLayer`) | (-5,2.2,78), inner 5 / outer 20, max vol 0.4 — the drowned-hush bed under Beat 3 |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | runs once at the end of the build, wiring any procedurally-generated stingers the two ambience layers or accent lights reference |
| `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` | auto-tags all three zones as distinct interior reverb volumes |

**Note on `TideDepthsDreadAmbience`'s placement.** The table above lists its position (-5, 2.2, 78) without evaluating it — that's the same coordinate as `TideDepthsLight0`, not the Tide Depths zone center (0, -0.3, 84), unlike `ConstructionCoreAmbience`, which is correctly centered on its own zone (0, -0.3, 112). With inner-5/outer-20 falloff, the bed therefore peaks over open water near the west entrance wall rather than the zone's dramatic heart (the Vane duel / Sable, z=88–90); the co-location with the accent light suggests the position was inherited from the light rather than chosen for coverage. Low priority — flag, don't fix: a future audio pass might recenter the drowned-hush bed on roughly (0, -0.3, 86) so it's loudest under the confrontation, not the entrance.

**Missing gap — three canonical depth/position-triggered bark pools are entirely unbuilt.** Beyond the thirteen fixed dialogue sets above, the source specifies three distinct dynamic-VO layers: (a) in-descent CREW COMM + Echo barks triggered by depth through Beat 1 (dialogue script L246, sample pool: Morrigan "Relay's getting louder the deeper you drop," Kessler "gone to gravel but I've got you," Coral "That seal you just passed, that's ours," Echo "Lift ends here, the rest is rope and nerve" / "Don't trust that gallery floor, it's pirate-shored" / "Listen to the machines down here... Hum is older"); (b) Echo's protect-fight bark set for the Coil raid (L325); (c) Echo's full boss-bark set for the Vane duel (L401). The build has only the two fixed Beat 1 dialogue sets (z=12 and z=44) — the ~32 m walk between them, the entire raid, and the entire boss duel all play with zero reactive VO today. This is canon-specified content, not invention, and is exactly the "author against final geometry" work the production notes defer to a later pass; the descent especially reads as eerily silent without it. The sample lines above are a seed pool for whoever authors these.

**Clarifying against §2's `EchoPresence` wiring, so "zero reactive VO" doesn't read as a contradiction.** §2 wires `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring) onto the rig, and Beats 0/1 lean on Echo's presence — so the descent is not literally silent; `EchoPresence` does fire generic ambient Echo lines throughout. The gap this bullet flags is narrower and more specific: those are not the **depth-triggered, canon-scripted** barks the seed pool above names (Morrigan "Relay's getting louder," Kessler "gone to gravel," Coral "that seal you just passed"). Generic `EchoPresence` ambience exists today; the scripted, position-keyed crew-comm/Echo bark pool does not — a future author should build the latter as new content, not assume the former already covers it.

**Missing ambience gap.** No `RustfangHoldAmbience` (or equivalent `BuildAmbienceLayer` instance) exists for the Hold Hall/overlook, where the player spends four of six beats (0, 1, 2, 5) and the majority of playtime. The SETTING block stages this zone's soundscape most specifically of anywhere in the chapter (groaning lift-chains, watch-fire crackle), and Beat 3a's sound design explicitly inverts it against the Tide's "drowned hush" — an inversion that only lands with both halves present. Today only the quiet half is built. This is the single largest un-flagged immersion gap in the chapter; a `RustfangHoldAmbience` layer (lift-chain groan + watch-fire crackle bed, centered near the overlook) should be prioritized alongside the VFX commissions in §9.

A second, adjacent ambience gap sits in the Tide Depths itself: the SETTING block names the zone's ambient sound twice as an acoustic "tidal pulse" — "a drowned hush broken by the tidal pulse of a machine drinking from the dead" (L42) and "The sound is a tidal pulse, the Engine drinking from the kept" (L122) — but the build only realizes a pulse as a *light*-intensity oscillation (`TideDepthsLight0`'s `AmbientPulse(6.8s)`, §Beat 3f) against the static `TideDepthsDreadAmbience` bed, which has no pulse of its own. The Engine's "tidal pulse stutters, a single missed beat, the first damage anyone has done it" as Sable comes fully loose (dialogue script L511, §6) is the same unbuilt sound cue at its most dramatically loaded moment. Both should land together with `RustfangHoldAmbience` in a future audio pass.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 09 — The Pit and the Deep** (`XRRigBuilder.BuildChapter9PitAndTheDeep()`).
2. **EditMode is the gate.** Project baseline is **842 tests green, 0 skips**; PlayMode is **70/70 green** (per `CLAUDE.md`). Ch9 itself contributes **22 fixtures** to the suite (`OverdriveLogicTests` — 15 — and `Chapter9LinesTests` — 7 — per `Project/Docs/CHAPTER-BUILD-LEDGER.md`'s 2026-07-03 entry, landing the running total at 492 at that snapshot). Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot, identical to Ch1's.** **No EditMode test invokes `BuildChapter9PitAndTheDeep()` or loads `Ch09_PitAndTheDeep.unity`.** `Chapter9LinesTests` covers only the dialogue-data module; `OverdriveLogicTests` covers only the pure ability-charge/cooldown math. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
4. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty cave, never an exception. Note the one exception carried from §Beat 3c/4c: the missing water surface and Engine data-light VFX have **no fallback to test**, because no fallback exists yet either — building those two effects (even as a primitive) is prerequisite work, not something this test can catch today.
5. **`AllyCombatant` engagement-range regression watch.** Not a new automated test — a manual check. After any change touching `AllyCombatant`, `EnemyWaveSpawner`, or Vane's activation timing, manually verify Gryph and Rook do **not** walk toward the Tide Depths once Vane activates (§Beat 2b's flagged gap). There is currently no test guarding this; a future fix that adds a range cap to `AllyCombatant` should add one.
6. **Perf reference bar:** **none recorded for Ch9 yet** (§1.6). Take a `UnityStats` reading via `script-execute` immediately after the first fresh build under this document and record drawCalls/setPassCalls/tris/verts here before any prefab lands. The Beat 2 co-op raid (two allies + four enemies simultaneously) is the most likely spot to threaten the 72 Hz floor first.
7. **Console check:** `Ch9WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild, across all thirteen sets.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception** — identical to every other chapter. Re-running `BuildChapter9PitAndTheDeep()` wipes generated content. Patch additively in the live editor, or fix `Chapter9Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **The `SealedProgramWall` progression blocker is real and open, and is the highest-priority item in this section** (§Beat 1c). The as-built prop is a full-height, near-full-width solid collider with only ~0.4 m gaps at each edge — too narrow to pass — and the mission spine never accounts for it, making it the only gap in this document that can hard-stop forward progress. It is also a canon mismatch (dialogue frames the sealed structure as a lid underfoot, not a wall across the path). Do not patch it by silently carving a hole in the existing collider during an art pass; the fix (a floor-plate/lid reframe, or a wall with a real walkable aperture) touches mission-critical traversal geometry and needs its own reviewed change.
- **Do not auto-delete orphan materials.** ~288 unreferenced material variants exist project-wide but are regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Same project-wide rule as every other chapter — Program-cut walls, engine scaffolds, and rack rows all get primitive colliders when they land as prefabs.
- **The `AllyCombatant` engagement-range gap is real and open** (§Beat 2b, §8 item 5). It is flagged in the builder's own source comment, not something this document is inventing — do not "quietly" tighten `AllyCombatant.RetargetNearestEnemy` while doing an unrelated art pass on this chapter; it is a shared component with 13-chapter blast radius and needs its own reviewed change.
- **The Coil-raider art mismatch is real and open** (§Beat 2c). `EnemyArtWirer` currently paints Ch9's raiders with `Ash-World_Scavenger` mesh instead of the narratively-correct `Coil_Syndicate_Ganger` mesh that already exists on disk. A one-line fix in a shared table — flagged, not silently applied here.
- **Five elements have no fallback geometry at all, not just missing prefabs:** the Tide Depths water surface (§Beat 3c), the Tide's bioluminescence (§Appendix B — the SETTING block's third named signature visual, currently substituted by nothing more than the two flat teal `TideDepthsLight0/1` point lights), the Construction Core's Engine data-light VFX (§Beat 4c), Sable's lattice-wiring/tether (§Beat 3b — the cables binding her to the shadow-racks that define her entire "a person made into wire" character concept; she currently reads as an ordinary standing NPC), and the freed blade-shadow that carries Vane's death into Echo/the katana — the signature image of the Overdrive grant itself (§Beat 3d). Every other MISSING row in this document degrades gracefully to a primitive per §1.5's interlock; these five currently degrade to *nothing*. Treat building even a crude primitive stand-in for all five as higher priority than swapping already-primitive rooms/props for prefabs.
- **Rook's payoff is deliberately open**, per `00_STORY_BIBLE.md`'s continuity notes: "seeded as a recurring figure (a friendly faction the saga can return to in the deep places); his payoff is TBD." Do not invent a resolution for him in a future chapter's builder without a story-side decision — his current scope ends cleanly at "keeper of the Rustfang," and that is intentional, not unfinished.
- **Gryph's Beat 3 dive presence is comm-only, not physical**, and the Bargain-beat dialogue was specifically rewritten (§7's applied-fixes table) to stop contradicting that. Any future patch that gives Gryph a literal walker into the Tide Depths must also resolve the engagement-range gap above, or he will walk into the Vane fight as an untethered, undamageable ally — changing the boss encounter's balance without anyone deciding to.
- **Verticality is unbuilt, not just unpolished** (§2's candor note). A future "restore real verticality" pass — actual drop-shafts, a rappel or climb mechanic, real buoyant underwater movement — is explicitly out of scope for the registry-swap refactor this document specifies. Do not attempt to smuggle new locomotion mechanics (climb, dive-swim, teleport) into an "art pass" on this chapter; that needs its own design-reviewed scope, per the non-negotiable VR-constraint note in §1.1.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All non-character props are cheap tinted primitives (`BuildProp`) rather than unique materials — this is the same `TintShared`-adjacent economy Ch1 documents, extended here with two rack rows whose tint is *procedurally lerped per instance* rather than hand-picked per prop.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch9Environment.asset`. Currently set inline at the top of `BuildChapter9PitAndTheDeep` (`Chapter9Builder.cs:99–124`).

| | Value |
|---|---|
| Directional key | color (0.55, 0.58, 0.68), intensity 0.32, rotation Euler(55, -35, 0) |
| Ambient | mode **Flat**, color (0.06, 0.07, 0.1) |
| Fog | mode **Exponential**, color (0.05, 0.09, 0.12), density 0.02 |
| HoldHall floor / ceiling tint | (0.14, 0.1, 0.07) / (0.06, 0.05, 0.05) |
| TideDepths + ConstructionCore floor / ceiling tint | (0.03, 0.06, 0.08) / (0.02, 0.03, 0.04) *(both zones share this pair)* |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`), ten total — the most of any chapter to date:

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.85, 0.7, 0.45) | 1 | 10 | none |
| `HoldUpperLight0` | (-3, 2.6, 18) | (0.9, 0.68, 0.4) | 1.3 | 14 | none |
| `HoldUpperLight1` | (3, 2.6, 30) | (0.85, 0.62, 0.38) | 1.3 | 14 | none |
| `OldMachineryLight` | (0, 2.4, 45) | (0.4, 0.6, 0.95) | 1.8 | 12 | `AddConsoleFlicker(seed: 99f)` |
| `OverlookLight0` | (-4, 2.6, 53) | (0.95, 0.55, 0.3) | 1.6 | 16 | none |
| `OverlookLight1` | (4, 2.6, 57) | (0.95, 0.55, 0.3) | 1.6 | 16 | none |
| `TideDepthsLight0` | (-5, 2.2, 78) | (0.25, 0.55, 0.6) | 1.6 | 16 | `AddAmbientPulse(period: 6.8f)` |
| `TideDepthsLight1` | (5, 2.2, 92) | (0.2, 0.5, 0.65) | 1.8 | 18 | none |
| `CoreLight0` | (-3, 2.6, 108) | (0.35, 0.9, 0.95) | 2 | 16 | none |
| `CoreLight1` | (3, 2.6, 112) | (0.35, 0.9, 0.95) | 2 | 16 | none |

**No event light exists this chapter** — Ch9 has no `DockingAlarmLight`-style inactive/triggered light. Vane's reveal and the Overdrive burst are both non-lighting events.

### A.2 World root & the three zones

| Item | Value | Source |
|---|---|---|
| World root | `GameObject "Rustfang"` | `Chapter9Builder.cs:128-129` |
| HoldHall floor/ceiling | `BuildFloorCeiling(world, "HoldHall", (0,0,31), (16,0,66), (0.14,0.1,0.07), (0.06,0.05,0.05))` | |
| HoldHall walls | `HoldHall_WallW` (-8,1.8,31) size(0.2,3.6,66); `HoldHall_WallE` (8,1.8,31) size(0.2,3.6,66); `HoldHall_WallS` (0,1.8,-2) size(16,3.6,0.2) | |
| TideDepths floor/ceiling | `BuildFloorCeiling(world, "TideDepths", (0,-0.3,84), (20,0,40), (0.03,0.06,0.08), (0.02,0.03,0.04))` | |
| TideDepths walls | `TideDepths_WallW` (-10,1.8,84) size(0.2,3.6,40); `TideDepths_WallE` (10,1.8,84) size(0.2,3.6,40) | |
| ConstructionCore floor/ceiling | `BuildFloorCeiling(world, "ConstructionCore", (0,-0.3,112), (16,0,16), (0.03,0.06,0.08), (0.02,0.03,0.04))` | |
| ConstructionCore walls | `ConstructionCore_WallW` (-8,1.8,112) size(0.2,3.6,16); `ConstructionCore_WallE` (8,1.8,112) size(0.2,3.6,16); `ConstructionCore_WallN` (0,1.8,120) size(16,3.6,0.2) | |

### A.3 Beat 1 — Rustfang Hold props

| Item | Value | Source |
|---|---|---|
| Hold rack row | `Ch9BuildHoldRackRow(world, zStart:10, zEnd:40, spacing:6)` — 6 z-positions (10,16,22,28,34,40) × 2 sides (x=∓7) = 12 `SalvageRack` props, each (0.4,2.2,1.2), y-center 1.1, tint `Color.Lerp(rust(0.42,0.3,0.18), cold(0.22,0.3,0.42), InverseLerp(10,40,z))` | `Chapter9Builder.cs:503-514` |
| Sealed Program wall | `BuildProp(world, "SealedProgramWall", (0,1.8,47), (15,3.6,0.4), (0.18,0.24,0.34))` — full `RoomH`, `BoxCollider` retained; only ~0.4 m gap at each x-edge against the hold-hall's ±7.9 inner wall faces — **an unaddressed progression blocker, see §Beat 1c** | `Chapter9Builder.cs:138` |

### A.4 Beat 2 — Hold overlook props

| Item | Value | Source |
|---|---|---|
| Freight cradle | `BuildProp(world, "FreightCradle", (0,0.4,58), (2.2,0.8,2.2), (0.3,0.28,0.22))` — diegetically Gryph's cargo-lift/winch (see §Beat 2c note), currently a plain tinted box | `Chapter9Builder.cs:139` |
| Gryph | `Ch9PlaceAlly(Ch9GryphPrefab, (-2,0,60), "Gryph")` → `InstantiateNpc` + `FitNamedCharacter` + `AllyCombatant` (bodyRenderer wired) | `Chapter9Builder.cs:198` |
| Rook | `Ch9PlaceAlly(Ch9RookPrefab, (2,0,61), "Rook")` — same treatment | `Chapter9Builder.cs:199` |
| Coil raiders ×4 | positions (-3,0,56), (0,0,54), (3,0,56), (0,0,62); `BuildEnemy(pos, playerHealth, raiderDef)`, built inactive | `Chapter9Builder.cs:202-213` |
| Coil raider `EnemyDefinition` | `Ch9CoilRaider.asset`: maxHealth 55, damage 9, moveSpeed 1.5, attackCooldown 0.9 | `Ch9EnsureCoilRaiderDefinition`, `Chapter9Builder.cs:360-375` |
| Wave spawner | `BuildWaveSpawner("CoilRaidWaveSpawner", (0,0,58), triggerRadius:12, waves:[[4 raider Healths]], barks:[dlgRaidBark])` | `Chapter9Builder.cs:256-257` |

### A.5 Beat 3 — Tide Depths props

| Item | Value | Source |
|---|---|---|
| Shadow rack row | `Ch9BuildShadowRackRow(world, zStart:70, zEnd:98, spacing:7)` — 5 z-positions (70,77,84,91,98) × 2 sides (x=∓9) = 10 `ShadowRack` props, each (0.4,1.6,1.2), y-center 0.8, tint `Color.Lerp(dim(0.1,0.28,0.32), lit(0.2,0.55,0.6), InverseLerp(70,98,z))` | `Chapter9Builder.cs:519-529` |
| Flooding hazard | `GameObject "TideFlood"` at (0,-0.3,84); `BoxCollider` isTrigger, size(20,5,40), center(0,1,0); `FloodingWaterHazard`, `Configure(playerHealth)` | `Chapter9Builder.cs:158-164, 181` |
| Sable | `Ch9PlaceStoryNpc(Ch9SablePrefab, (2,0,90), "Sable")` — `InstantiateNpc` + `FitNamedCharacter` + `StoryNpc`, no combat | `Chapter9Builder.cs:217, 430-441` |
| Vane / Wraith-6 | `Ch9BuildVane((-1,0,88), vaneDef, playerHealth)`, rotated Euler(0,180,0); `CapsuleCollider` center(0,1.1,0) height2.4 radius0.5; `Health`; synthesized `ArmR`(0.35,1.4,0)→`Sword`→`Blade`→`BladeTip`(local 0,0,0.55); `Enemy` wired; built `SetActive(false)` | `Chapter9Builder.cs:221-222, 458-498` |
| Vane `EnemyDefinition` | `Ch9Vane.asset`: maxHealth 280, damage 24, moveSpeed 1.5, attackCooldown 0.85 | `Ch9EnsureVaneDefinition`, `Chapter9Builder.cs:377-392` |
| Overdrive granter | `GameObject "OverdriveGranter"` + `AbilityGranter` (`abilityId = AbilityId.Overdrive`), built `SetActive(false)` | `Chapter9Builder.cs:260-265` |

### A.6 Beat 4 — Construction Core props

| Item | Value | Source |
|---|---|---|
| Engine scaffolds | `EngineScaffold0` (-3,1.4,112) size(0.5,2.8,0.5) tint(0.3,0.6,0.65); `EngineScaffold1` (3,1.4,116) size(0.5,2.8,0.5) tint(0.3,0.6,0.65) | `Chapter9Builder.cs:152-153` |
| Sable interface console | `BuildProp(world, "SableInterface", (0,0.6,108), (1.4,1.2,1.0), (0.25,0.65,0.7))` | `Chapter9Builder.cs:154` |

### A.7 Beat 5 — Completion canvas

| Item | Value | Source |
|---|---|---|
| "CHAPTER 9 COMPLETE" canvas | worldspace `Canvas` (700×220, scale 0.0015) at (0,1.4,61), rot Euler(0,180,0) facing -Z; `Image` bg (0.04,0.05,0.08,0.9); child `Text` "CHAPTER 9 COMPLETE", 54pt, color (0.9,0.92,1), `LegacyRuntime.ttf`; built `SetActive(false)` | `Ch9BuildCompleteCanvas`, `Chapter9Builder.cs:533-566` |
| `ChapterOutro` | (0,1,60), inactive; `CampaignFlagSetter` flags `[ch9_complete, gryph_recruited, sable_recruited]`; `completeCanvas` wired; `OnActivated` → `flagSetter.SetFlags` | `Chapter9Builder.cs:270-286` |

### A.8 Reach points, ability rig, and misc

| Item | Value | Source |
|---|---|---|
| Reach points | `OldMachineryReachPoint` (0,1,44); `OverlookReachPoint` (0,1,52); `TideEntryReachPoint` (0,1,68); `ConstructionCoreReachPoint` (0,1,106); `ClimbOutReachPoint` (0,1,59) | `Chapter9Builder.cs:225-234` |
| Player rig | `BuildRig(refs, addLocomotion:true)`, spawn (0,0,2); `EchoPresence`; `ZoneBounds` center(0,3,58) radius130; `AttachPlayerAbilities(rig, refs)` (WeakpointSight + OverdriveController, both self-gating) | `Chapter9Builder.cs:173-180` |
| Katana "Echo" | `BuildSword((2,1,4), Euler(-90,0,0), weapon, Ch9EchoBladePrefab)` | `Chapter9Builder.cs:185` |
| Ambience layers | `ConstructionCoreAmbience` (0,-0.3,112) inner5/outer20/vol0.4; `TideDepthsDreadAmbience` (-5,2.2,78) inner5/outer20/vol0.4 | `Chapter9Builder.cs:333-334` |
| Mission steps | 21 total, indices 0–20 | `Chapter9Builder.cs:295-322` |

### A.9 Scene root hierarchy (current)

`BuildChapter9PitAndTheDeep()` creates these as **siblings**, not nested: `Directional Light`, `Rustfang` (world root — floors/ceilings/walls of all three zones), the ten accent lights, `TideFlood` (the flooding hazard), `Game`, the player rig, Gryph, Rook, the four Coil raiders, Sable, Vane, the five reach points, thirteen `DialoguePlayer`s, `CoilRaidWaveSpawner`, `OverdriveGranter`, the two `BuildAmbienceLayer` instances, the "CHAPTER 9 COMPLETE" canvas, `ChapterOutro`, `Mission`, and `XR Interaction Manager`. Ch9 is unusually loose at the root by comparison to the target contract — **none of the above are parented under `[STATIC_ART_DO_NOT_DELETE]` or a `[BEAT_N_LOGIC]` root**, both of which §1.4/§1.2 prescribe. Making that split real is part of this refactor's scope, not an incidental cleanup.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` (the zone shells, both rack rows, `SealedProgramWall`, `FreightCradle`, both `EngineScaffold`s, `SableInterface`, and Sable/Vane's inert placements per §1.2) and five `[BEAT_N_LOGIC]` roots (0–5, minus Beat 0/5 which own no art), and moves the reach points, `AllyCombatant`/`Enemy` spawns, wave spawner, ability granter, dialogue players, and `ChapterOutro`/`Mission` into the matching logic root for their beat.

---

## Appendix B — ArtAssetRegistry key inventory

| Registry Key | Category | Resolves To | Status |
|---|---|---|---|
| `Rooms.RustfangHoldHallShell` | Rooms | `…/Art/Generated/Rooms/RustfangHoldHallShell.prefab` | **MISSING** |
| `Rooms.TideDepthsShell` | Rooms | `…/Art/Generated/Rooms/TideDepthsShell.prefab` | **MISSING** |
| `Rooms.ConstructionCoreShell` | Rooms | `…/Art/Generated/Rooms/ConstructionCoreShell.prefab` | **MISSING** |
| `Props.SalvageRack` | Props | `…/Art/Generated/Props/SalvageRack.prefab` | **MISSING** |
| `Props.SealedProgramWall` | Props | `…/Art/Generated/Props/SealedProgramWall.prefab` | **MISSING** — as-built primitive is a full-height, near-full-width solid collider with only ~0.4 m edge gaps (an unaddressed progression blocker) and a lid-vs-wall canon mismatch; a replacement prefab should be scoped as a floor-plate/lid or a wall with a real walkable aperture, not a like-for-like reproduction — see §Beat 1c |
| `Props.FreightCradle` | Props | `…/Art/Generated/Props/FreightCradle.prefab` | **MISSING** — brief should specify a winch/cargo-lift cradle with a brake lever (Gryph's dive mechanism, see §Beat 2c), not a generic crate |
| `Props.ShadowRack` | Props | `…/Art/Generated/Props/ShadowRack.prefab` | **MISSING** |
| `Props.EngineScaffold` | Props | `…/Art/Generated/Props/EngineScaffold.prefab` | **MISSING** |
| `Props.SableInterfaceConsole` | Props | `…/Art/Generated/Props/SableInterfaceConsole.prefab` | **MISSING** |
| `Vfx.TideFloodSurface` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either** |
| `Vfx.ConcordEngineDataLight` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either** |
| `Vfx.TideBioluminescence` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** The SETTING block names "cold bioluminescence threading through the dark" as the Tide's signature light three times (also L415); currently substituted entirely by the two flat teal `TideDepthsLight0/1` point lights |
| `Vfx.SableLatticeTether` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** The cable/lattice-wiring visual binding Sable to the shadow-racks ("strung into the lattice, unable to pull free," dialogue script L58-60) is her defining character concept and is entirely unbuilt — see §Beat 3b |
| `Vfx.BladeShadowRelease` | VFX | *(no path — concept unbuilt)* | **MISSING — no primitive fallback exists either.** The freed blade-shadow that drifts up out of Vane's drowned blade and travels into Echo/the katana — the diegetic act of the Overdrive grant itself ("the light in his hilt comes loose... It begins to drift toward Ronin-7, toward the katana, toward Echo," dialogue script L403/L411/L415) — currently exists only as Echo's spoken line ("His shadow's free... it's coming to us," step 12); see §Beat 3d |
| `Named.Gryph` | Named characters | `…/Art/Generated/Characters3D/Named/Gryph.prefab` | **EXISTS** (real Tripo mesh, confirmed on disk with rigged skin asset) |
| `Named.Rook` | Named characters | `…/Art/Generated/Characters3D/Named/Rook.prefab` | **EXISTS** (real Tripo mesh, confirmed on disk with rigged skin asset) |
| `Named.Sable` | Named characters | `…/Art/Generated/Characters3D/Named/Sable.prefab` | **EXISTS** (real Tripo mesh, confirmed on disk with rigged skin asset) |
| `Named.Echo` | Named characters | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** (shared with Ch1 and every other chapter that places the katana) |
| `Named.VaneWraith6` | Named characters | `…/Art/Generated/Characters3D/Named/Vane_Wraith-6.prefab` | **EXISTS**, but content is a `PlaceholderCharacterBuilder` "Massive archetype" primitive stand-in, not Tripo art — replace the file's contents in place when real art lands, do not repoint the key. **Art brief:** canon stages Vane as Ronin-7's forebear, not his twin — an older make of the same operative line, not a monster — so the commission should target a Ronin-scale armored blade-operative (the 2.4 m capsule wants to drop toward ~1.9–2.0 m), not a reproduction of the current oversized "Massive archetype" read; see §Beat 3c |
| `Enemies.CoilRaider` | Enemies | additively resolved by `EnemyArtWirer`'s per-scene table → currently `…/Art/Generated/Characters3D/Enemies/Ash-World_Scavenger.prefab` | **EXISTS**, narrative/art mismatch flagged in §Beat 2c and §9 — the correct-by-name `Coil_Syndicate_Ganger.prefab` already exists on disk and is unused here |

**Six of twenty keys resolve today** (five real Named/Enemies rows — `Named.Gryph`, `Named.Rook`, `Named.Sable`, `Named.Echo`, `Enemies.CoilRaider` — plus the placeholder-quality `Named.VaneWraith6` mesh), against fourteen `MISSING` rows — a slightly better starting ratio than Ch1's "five keys resolve, everything else is a commission," carried forward mainly because Ch9 inherits the Named-character pipeline that had already matured by the time this chapter was built. The five VFX rows with no fallback at all (`Vfx.TideFloodSurface`, `Vfx.ConcordEngineDataLight`, `Vfx.TideBioluminescence`, `Vfx.SableLatticeTether`, `Vfx.BladeShadowRelease`) are the five highest-priority commissions in this inventory — see §9.

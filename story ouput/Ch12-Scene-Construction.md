# Chapter 12 — Scene Construction

*The architectural contract for `Ch12_TheFracture.unity`: what Chapter 12 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 12 ("The Fracture") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing spatial invariants, mission triggers, or the two-phase Mirror-boss lifecycle.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter12Builder.cs`, entry point `XRRigBuilder.BuildChapter12TheFracture()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 12 — The Fracture**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch12_The_Fracture.md`, `Ch12_The_Fracture_Dialogue_Script.md`, `Chapter12Lines.cs`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The Edition duel — the chapter's sole boss, fought against the player's own mirrored school — and Vale's argument-beats are sold entirely by `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle, never by moving the camera.
- **Traversal in Ch12 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no climb/wall-run/parkour** anywhere in this chapter. Every tier-to-tier transition down the cryo-vault is a single solid ramp (`Ch12BuildRamp`), walkable on ordinary continuous locomotion — matching the project's VR-comfort stance and Ch9–11's precedent. Nothing in the geometry requires Phase-step, Overdrive, or any other ability to traverse; the full ability chain rides the rig purely because it is self-gated and always present from Act III onward (see §Beat 3d).
- **No NPC in this chapter ever physically walks.** Unlike Ch1/Ch9/Ch10's `NpcWalker`+`Trigger` idiom, the builder never constructs an `NpcWalker` at all: Vale is placed once, active from scene start, and never moves; the Ronin-7 Edition (the boss) is placed twice as two inactive `MirrorPhantom` phases at the same fixed point, and neither phase relocates. Every other named speaker (Cassie-04, Sable, Mera Voss, Kessler, Morrigan, Coral Vex, Vess, Gryph) is voice-only over comm for the entire chapter — none is physically placed. §5 documents this explicitly as a chapter-wide invariant, not an omission.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | tier platforms/ramps/pillars, cradle-row props, the throne-tier shell, Vale's and the Edition's mesh placement *(the physical object)*, decorative lights, ambience emitters | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | enemy spawns (skirmish trio, both Edition phases), the `MirrorPhantom`/`HiveCascadeController` wiring, ability-grant triggers, reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**The Ronin-7 Edition spans both.** `BuildBeatNArt()` (Beat 3's) instantiates both `Ch12BuildNamedBoss` phases and returns their handles; `BuildBeatNLogic()` wires the `MirrorPhantom` sequencer over them, the null-object Prompt step, and the `AbilityGranter` that follows. Art builds the two bodies; logic decides when the first activates, when the second takes over, and what the kill grants.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildAmbienceLayer`, `BuildEnemy`, `BuildHologram`, `Author*Step`, `AttachPlayerAbilities` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, and every shared runtime component this chapter reuses (`CryoChillController`, `HeatVent`, `MirrorPhantom`, `HiveCascadeController`, `PostureMeter`, `PatternedDuelist`, `AbilityGranter`, `ChapterOutro`) — these are chapter-agnostic and reused elsewhere in the project.
- **Free to restructure:** the Ch12-local helpers, called only from `BuildChapter12TheFracture()` — `Ch12EnsureSkirmisherDefinition`, `Ch12EnsureEditionDefinition`, `Ch12EnsureSentinelDuelistDefinition`, `Ch12UpgradeToSentinelDuelist`, `Ch12BuildHiveCascade`, `Ch12BuildDialogue`, `Ch12WireVoiceClips`, `Ch12PlaceStoryNpc`, `Ch12PlaceLastNode`, `Ch12BuildNamedBoss`, `Ch12BuildTier`, `Ch12BuildRamp`, `Ch12BuildCradleRow`, `Ch12BuildCompleteCanvas`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs` or the `MirrorPhantom`/`HiveCascadeController` components, stop — you have left Chapter 12 and are now silently rebuilding every chapter that reuses those systems.

**Candor note — the as-built code has no `BuildBeatNArt`/`BuildBeatNLogic` split at all.** `BuildChapter12TheFracture()` is a single ~280-line monolithic method that constructs lighting, geometry, the rig, the skirmish squad, Vale, the last node, both Edition phases, every dialogue player, and all 22 mission steps in one linear pass (`Chapter12Builder.cs:105–385`). The five `[CRITICAL CLAUDE REFACTORING INSTRUCTION]` blocks under each beat heading in §4 describe the **target** split, not work already done — this chapter has not yet received the same art/logic separation pass documented as aspirational for Ch1/Ch9/Ch11 either. Appendix A is this method's literal contents, organized by beat for readability.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two ScriptableObjects should carry everything the builder currently types inline, matching the pattern already documented for Ch1/Ch9/Ch11:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch12Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, six per-tier/throne accent lights, `ConsoleFlicker`/`AmbientPulse` behaviour tags |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document |

Neither exists yet for Ch12. Prefab **paths never appear in builder code.** The builder asks the registry for `Rooms.CryoVaultTier`; the registry asset holds the path.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Commander-Vale, Ronin-7_Edition_Clone, Echo all resolve)
  Rooms/                                    ← new (tier platforms, the throne-tier shell)
  Props/                                    ← new (pillars, ramps, cradle-row props)
  VFX/                                      ← new (Edition blade-shadow dissolve, hologram dressing beyond the shared BuildHologram)
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter12TheFracture()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter12Builder.cs:109`). That call discards the entire scene and opens a fresh empty one; there is nothing left to search for afterward. Making the safe zone real requires **replacing the wipe strategy** with `EditorSceneManager.OpenScene(Ch12ScenePath)` + `DestroyImmediate` on each generated root by name (`CryoVault`, `Game`, `Mission`, the rig, the six accent lights, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched, falling back to `NewScene` only when the scene file does not yet exist. `EnemyArtWirer.cs`/`CrowdArtWirer.cs` already open shipped scenes in place, mutate them idempotently, and `SaveScene` — reuse that pattern.
>
> **Naming note for the by-name wipe.** `BuildFloorCeiling` names its two cubes `{name}_Floor`/`{name}_Ceiling` (so `SpawnGround` never exists as a bare GameObject — only `SpawnGround_Floor` and `SpawnGround_Ceiling`, both children of `CryoVault`), and `ThroneTier` follows the same convention. `Ch12BuildTier` keeps the bare name on the tier floor itself (`Tier1`, `Tier2`, `Tier3`) while its two pillar props are `{name}_PillarA`/`{name}_PillarB` and its cradle-row props are `{name}_Cradle` (repeated `count` times per tier — see §Beat 1c). Destroying `CryoVault` by name removes all of this correctly — nothing breaks — but a future implementer extending this list should search for the root names above, not a literal `SpawnGround` GameObject, which does not exist.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero environment prefabs exist for Ch12.** No tier-platform shell, no ramp prefab, no cradle prop, no throne-tier room shell. See Appendix B for the full inventory: three keys resolve (the two Named-cast prefabs plus Echo); everything else is a commission.

A builder that instantiates from an empty registry produces **an empty vault** — the first run of the refactored builder would destroy Chapter 12.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Rooms_CryoVaultTier);
if (prefab == null) {
    Debug.LogWarning($"[Ch12] {ArtKey.Rooms_CryoVaultTier} unresolved — primitive fallback.");
    Ch12BuildTier(staticArtRoot, name, center, tierColor);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping elsewhere (`ChapterSharedBuilders.cs:623`, `if (prefab == null) continue; // not baked yet`). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No Ch12-specific greybox `UnityStats` baseline has been recorded yet.** Establish one at the first `script-execute` `UnityStats` read of the fresh build and record it here before any prefab swap — do not invent a number.
- **Set-dressing today is cheap tinted primitives** (`TintShared`/`GameObject.CreatePrimitive`) — every tier floor, pillar, ramp, cradle, and throne-tier wall shares this convention. **Prefabs replacing them must carry their own materials and will not batch this way.** Re-measure after every prefab lands, same as every other chapter's rule.
- **This is the chapter most likely to spike enemy-AI cost.** The skirmish squad (3 `Enemy`s) is the smallest roster of any chapter's mid-encounter, but it is the first chapter to layer `HiveCascadeController` (independent per-member state timers) and `PostureMeter`/`PatternedDuelist` (the elite Sentinel Duelist) on top of the base melee FSM — none of which existed as a combined stack before Ch12. Profile the Tier-2 skirmish specifically, not just the Edition duel, when re-measuring.

## 2. Chapter spatial map

Chapter 12 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch12_TheFracture.unity`, laid out as **a single descending shaft**: four open tier platforms connected by solid ramps, dropping in Y and advancing in Z from the spawn mouth down to an enclosed throne-tier room at the bottom. There is no branching and no return route other than the climb back up the same ramps (unmodeled — the chapter ends at the throne-tier).

```
SPAWN MOUTH → OPEN TIERS (descending in Y as Z increases) → ENCLOSED THRONE-TIER
 SpawnGround ──ramp0── Tier1 ──ramp1── Tier2 (skirmish) ──ramp2── Tier3 (repetition) ──rampThrone── ThroneTier (enclosed, W/E/N walls)
 (0,0,4) 16x12         (2,-1.5,26) 12x12  (-2,-3,42) 12x12          (1,-4.5,58) 12x12                (0,-6,74) 20x22
 z≈[-2,10] y=0          z≈[20,32] y=-1.5   z≈[36,48] y=-3             z≈[52,64] y=-4.5                 z≈[63,85] y=-6
```

| Beat | Region | Footprint | Floor center / size | Floor Y |
|---|---|---|---|---|
| 0 (briefing) | *(voice-only — no built room beyond `SpawnGround`; see §Beat 0c)* | — | — | 0 |
| 1 (descent — race, skirmish, repetition) | `CryoVault`: `SpawnGround` → `Tier1` → `Tier2` → `Tier3` | x[-6,6] widening to x[-8,8] at spawn, z≈[-2,64] | centers (0,0,4) / (2,-1.5,26) / (-2,-3,42) / (1,-4.5,58) | 0 / −1.5 / −3 / −4.5 |
| 2 (waking Vale, the crown, the trump card) | `CryoVault` → `ThroneTier` | x[-10,10], z[63,85] | center (0,-6,74), 20×22 | −6 |
| 3 (the gut-punch, the Edition duel, Mirror) | `ThroneTier` | same as above | center (0,-6,74), 20×22 | −6 |
| 4 (the refusal, the node cut loose, the hook) | `ThroneTier` | same as above | center (0,-6,74), 20×22 | −6 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, used only by `SpawnGround` and `ThroneTier` (both built via `BuildFloorCeiling`, which always emits a ceiling). `Tier1`/`Tier2`/`Tier3` are floor-only platforms (`Ch12BuildTier` never calls `BuildFloorCeiling`) — open-air, no ceiling, matching Ch11's `Ch11BuildTier` idiom for a canyon-style open descent.

**These footprints are load-bearing and survive the refactor unchanged.** A tier or room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience. `Ch12TierHalfWidth = 6f` sizes every canyon platform identically (12×12); `Ch12ThroneHalfWidth = 10f` sizes the throne-tier's 20 m width. Do not vary either without updating every downstream ramp/light/reach-point offset that assumes them.

**Ramps, not gaps.** Every tier-to-tier transition is a single solid, gently-sloped ramp (`Ch12BuildRamp`), never a jump or a gap requiring a traversal ability:

| Ramp | From → To | Rise | Run | Length | Grade |
|---|---|---|---|---|---|
| `Ramp0` | (0,0,10) *(spawn overhang lip)* → Tier1 (2,−1.5,26) | −1.5 m | 16 m | ≈16.07 m | ≈5.4° |
| `Ramp1` | Tier1 (2,−1.5,26) → Tier2 (−2,−3,42) | −1.5 m | 16 m | ≈16.07 m | ≈5.4° |
| `Ramp2` | Tier2 (−2,−3,42) → Tier3 (1,−4.5,58) | −1.5 m | 16 m | ≈16.07 m | ≈5.4° |
| `RampThrone` | Tier3 (1,−4.5,58) → (0,−6,63) *(throne-tier's south lip)* | −1.5 m | 5 m | ≈5.22 m | ≈16.7° |

All four ramps share width `Ch12TierHalfWidth * 2 = 12 m` — wide enough that no player heading needs correcting mid-descent.

**Note — ramp length/grade above are computed from the z-run only; three of the four ramps also carry lateral x-drift the table doesn't surface.** `Ch12BuildRamp` derives `length`/`angle` purely from `to.z − from.z` (and `to.y − from.y`), centers the ramp at the averaged `mid = (from + to) * 0.5`, and rotates about X only (`Chapter12Builder.cs:603–618`) — it never accounts for a tier-center x-offset. But the tier centers alternate in x (spawn-lip x=0 → Tier1 x=2 → Tier2 x=−2 → Tier3 x=1 → throne x=0), so `Ramp1` drifts 4 m in x across its run, `Ramp2` drifts 3 m, and `Ramp0` drifts 2 m (`RampThrone`'s 1 m drift is negligible by comparison). The ramp surface is therefore a straight −Z incline that does not align tier-center to tier-center; a player walking straight −Z down `Ramp1` lands on Tier2's off-center edge rather than its center. The shared 12 m width absorbs this without requiring a heading correction — which is why the line above is still true — but a future implementer sizing a `Props.CryoVaultRamp` prefab from length/grade alone would under-size it for the actual center-to-center span; the prefab's footprint needs to account for the drift, not just the table's length column.

**Reach-trigger gates** — two, the chapter's only progression gates besides the one `DefeatEnemies` step and the boss's null-object Prompt step:

| Reach point | Position | Radius | Gates | Fires mission step |
|---|---|---|---|---|
| `MidVaultReachPoint` | (−2, −2, 42) *(Tier2 + (0,1,0))* | 5 m | the skirmish `DefeatEnemies` step | step 2 |
| `ThroneTierReachPoint` | (0, −5, 68) *(throne-tier center + (0,1,−6), i.e. just past `RampThrone`'s top)* | 6 m | Vale's greeting dialogue | step 5 |

**Player rig:** `BuildRig(refs, addLocomotion: true)` + `EchoPresence` + the full ability chain (`AttachPlayerAbilities`: weakpoint-sight, `OverdriveController`, `PhaseStepController`, `UnbrokenWard` — all self-gated on `CampaignState.HasAbility`; Mirror is the fifth and is granted mid-chapter, see §Beat 3b) + `CryoChillController` (`chillRiseRate = 0.015` — the only field the builder overrides; see the candor note immediately below, this is not the mild atmospheric pressure it reads as). `ZoneBounds` is set to **center (0, −3, 60), radius 130** — one bounding sphere loosely enclosing the whole shaft (`SpawnGround` (0,0,4) is √(9+3136) ≈ 56 m from center; the outro anchor (0,−5,83) is √(4+529) ≈ 23 m — both comfortably inside the 130 m radius).

**Candor note — the cold is not mild; at these settings it inflicts lethal frostbite damage, silently, during the stationary Beat 0 briefing.** `CryoChillController` overrides only `chillRiseRate` (0.015); `warmRate` (0.35), `frostbiteThreshold` (0.85), `damagePerTick` (4), and `damageInterval` (1.5 s) all keep the component's defaults. Frostbite therefore triggers at 0.85 / 0.015 ≈ **57 s of continuous exposure**, after which the rig takes 4 dmg every 1.5 s (≈2.7 dmg/s) until it warms back below the threshold. Beat 0 is a ≈212 s stationary VO played at `SpawnGround` (§Beat 0e), and the chapter's only `HeatVent` (`NodeWarmth`) sits 74 m away at the throne-tier — nothing sheds chill anywhere near the spawn point. A player who simply listens to the briefing crosses the frostbite threshold at ~57 s and then bleeds roughly 2.7 dmg/s for the remaining ~150 s — hundreds of points of damage while standing still, before the descent even begins. This needs in-headset verification before ship; §3 below carries the mitigation recommendation and the matching feedback gap.

## 3. Global environment & backdrop

**The Dominion cryo-command vault** reads as "cold certainty made into architecture" — monolithic, bilaterally symmetrical, built by an empire that believed in order and meant the building to say so. Per the dialogue script's SETTING block, it is the deliberate tonal hard-edge after the bone-canyon's organic grief: "frost steams off black ferro-ceramic walls; stasis-rime crusts the rails; the only warmth in the place is the slow amber pulse of the last scattered archive… and the cold blue of ten thousand stasis cradles racked in tiers." The builder executes this as a straightforward **color and light gradient with depth**, exactly the same economy Ch9's rack-row and Ch11's tier-lerp use: `Ch12BuildTier`'s per-tier color lerps from a cool grey-blue upper tone `(0.22, 0.24, 0.28)` toward a deep stasis-blue `(0.1, 0.14, 0.24)` across the three numbered tiers, and the six accent lights (`SpawnLight` → `ThroneLight1`) cool and dim in most channels while the sole warm exception, `ThroneLight0` — the node's amber pulse `(0.9, 0.65, 0.3)`, `AmbientPulse(period: 7.4s)` — sits at the very bottom, the one warmth in the cold per the SETTING block's own image.

**Recommended edit — co-locate `ThroneLight0` with the node, not the throne.** Builder places `ThroneLight0` at (−4, −2.4, 74) — same x as Vale, who rises at (−4, −6, 80) — while the actual last-node hologram is 7 m across the room at (3, −4.8, 78) and the `NodeWarmth` `HeatVent` sits beside it at (3, −5, 78); the node itself is lit by the *cold* blue `ThroneLight1` at (4, −2.4, 74), not the amber. So the fiction's "the node's amber pulse" is, as built, positioned over the cryo-throne — and the chill-shedding fiction splits in two: the player *feels* warmth (`HeatVent`) at x=3 but *sees* the amber "warmth" at x=−4. The sanctioned fix (elevated from observation to recommendation, see §6): **move `ThroneLight0` to co-locate with the node/`HeatVent` at ~(3, −4.x, 78)**, not the alternative of merely relabeling it as Vale's light — co-location is the single change that makes three separate beats land visually at once: Beat 2's warmth framing, the shed-chill mechanic under the same light the fiction names, and Beat 4's node-cut having an actual light to kill (§Beat 4c/f).

**The move has an unstated side effect: it leaves Vale under-lit.** `ThroneLight0` currently shares Vale's x-coordinate (he rises at (−4,−6,80), the light sits at (−4,−2.4,74), ≈7 m away) and is his only warm key light. Move it to the node and Vale is lit only by the cold `ThroneLight1` at (4,−2.4,74) — ≈10.6 m away, on the opposite side of the room, at the edge of its range-14 falloff — plus the flat 0.28-intensity directional. Vale is the single most important face in Beats 2 and 4 (his entire "reasonable, almost-warm predator" performance is carried on face and voice, with no camera moves to help). The co-location recommendation should ship with a replacement key for Vale — a *cool*-toned one, not amber, so it reads as "cold command" rather than duplicating the node's warmth and reinstating the same conflation this fix is meant to resolve — or the throne end goes under-lit exactly where the chapter needs the face readable.

**The cradle-row reveal — the architecture tells it before Vale does.** Per the production notes, "let the player understand 'I am the template' by counting cradles before Vale ever says it." `Ch12BuildCradleRow` is the literal mechanism: each tier gets a row of small blue-lit stasis-cradle props whose **count grows with depth** — Tier1 gets 2, Tier2 gets 5, Tier3 gets 8 (`count = 2 + i * 3`) — a density curve that visually escalates the "how many of me are down here" question across the descent, paying off in Echo's Beat 1 repetition dialogue. All cradle props share one tint, `(0.3, 0.55, 0.85)`, the same cold blue named in the SETTING block.

**Candor note — the cradles are undifferentiated, not "one repeating face."** The narrative's core environmental beat is that the *upper* cradles read as mixed, anonymous makes, and only from the *mid*-vault down do they start repeating one specific silhouette — "the makes up top were mixed. Down here they're not mixed… These are you." (`ch12_beat1_repetition`). The as-built `Ch12BuildCradleRow` props are all identical tinted boxes with no face, mesh, or per-tier visual distinction beyond count — there is no mixed-vs-uniform read available to the player's eye today; the escalation is entirely numeric (2→5→8), not visual. This is the natural target for the `Props.StasisCradle`/`Props.StasisCradle_RoninFace` commission split in Appendix B: an anonymous-cradle prefab for Tier1 and a face-bearing Ronin-cradle prefab for Tier2/Tier3, so the reveal reads in silhouette the way the dialogue assumes it already does.

**Candor note — fifteen cradles cannot deliver the chapter's stated scale, and this is a bigger gap than the mixed-vs-uniform note above covers.** The SETTING block racks "**ten thousand** stasis cradles… down the throat of the vault"; INTRUDING ELEMENTS repeats "not one copy of him at the bottom, **ten thousand**"; and Echo's own `ch12_beat1_repetition` line states outright "**Hundreds I can see.** The shape of it says more than that, a lot more, down past the amber." `Ch12BuildCradleRow`'s `count = 2 + i * 3` across the three numbered tiers totals **fifteen** cradles, in three single rows — an inventory two to three orders of magnitude short of a line of dialogue this very document transcribes. Neither this section's mixed-vs-uniform note nor §Beat 1c's numeric-escalation note (2→5→8) addresses the sheer-quantity gap: no amount of face-mesh differentiation makes fifteen boxes read as an inventory. "Let the player understand 'I am the template' by counting cradles before Vale ever says it" is architecturally undeliverable at this count. See the `Props.StasisCradle` commission spec in Appendix B for the recommended fix.

**The throne-tier's warmth is mechanical, not just visual.** A `HeatVent` trigger volume (`NodeWarmth`, a 6×3×6 box at throne-tier + (3,1,4)) sheds the rig's `CryoChillController` chill while the player stands near the node — "the only warmth in the place is the slow amber pulse" made into a one-line tie between fiction and mechanic, per the class doc. It needs no wiring: `HeatVent.OnTriggerStay` falls back to `GetComponentInParent<CryoChillController>()` on whatever enters it. But the box's coverage does not reach where the player actually stands for most of Beats 2–3 — see the candor note immediately below.

**Candor note — frostbite is a real, lethal, and entirely feedbackless health drain.** Per §2's candor note, the rig can take hundreds of points of frostbite damage before the descent even begins, and `CryoChillController` exposes `onFrostbite`/`onCleared` UnityEvents that the builder wires to nothing. Per the VR no-screen-effect rule there is correctly no frost overlay, but that leaves the player losing health with zero legible signal in any channel. Recommended: fire an `AudioDirector` low breath/shiver sting plus a slow `Haptics` shudder on `onFrostbite`, cleared on `onCleared` — the one place the "felt cold" this section keeps invoking could actually be *felt*, through the sanctioned feel-vocabulary, rather than only asserted in prose. For the underlying exposure clock itself, pick one mitigation (do not stack): a spawn-area `HeatVent` twin to `NodeWarmth`, a `chill.Clear()` call at the top of Beat 0, or a further-reduced `chillRiseRate`.

**Candor note — `NodeWarmth`'s box does not cover where the player actually stands during Beats 2–3.** The vent spans x[0,6], z[75,81] (throne-tier + (3,1,4), 6×3×6) — but the Edition duel is fought at (0,−6,72), Vale's Beat 2/3 dialogue anchors sit at z=70–74, and `ThroneTierReachPoint` is z=68 — all outside the warm volume. The "stand in the node's warmth to shed the chill" tie between fiction and mechanic (above) never actually engages during the argument or the duel, exactly when a ~57 s exposure clock would matter; the player only reaches the vent by walking up to Vale/the node, past both fights. This makes the coverage fix higher-priority than the Beat-0 framing in §2 implies: the throne-tier's near-stationary stretch — Beat 2's ≈160 s of argument VO, Beat 3's ≈140 s of reveal VO plus the duel itself, and Beat 4's ≈124 s of decompression VO, all played on the vent-excluded floor — is the chapter's single longest continuous exposure window, larger in aggregate than Beat 0's own ≈212 s and unbroken by any walk between dialogue sets. The player bleeds frostbite through the entire back half of the chapter with the vault's one refuge sitting behind the boss. Either widen/re-center the box to reach the duel/argument floor, or accept — and document — that the vault's sole refuge sits behind the boss.

**Candor note — the cold is felt and hazy but never seen.** `CryoChillController` (chillRiseRate 0.015) makes the cold *felt*, and fog makes it *hazy*, but nothing renders the SETTING's "frost steams off… walls; stasis-rime crusts the rails." Recommended: a `Vfx.CryoFrostSteam` candidate (Appendix B) for rail/wall rime emitters, and/or a subtle chill-driven visual respecting the VR no-screen-effect constraint (decal/particle, never a vignette or camera-space effect) — the chapter's defining atmospheric texture is currently the one sensory layer with zero visual surface. A second, near-field candidate worth naming alongside it: the player's own breath fogging in front of the headset, the single most classic VR cold cue and — unlike a vignette — a world-space particle, not a screen-space one, so it respects the same no-camera-effect rule that keeps this game shake-free. It needs the same VR caveat as any near-camera particulate: sparse, soft, and low-density, never occluding, or it trades one discomfort for another. Gate it to read strongest in the un-vented spawn/descent stretch (§2/§3) where `CryoChillController`'s clock is accumulating unmitigated, thinning out once the player nears `NodeWarmth`.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter12Builder.cs`.** The builder should read `Assets/Ronin7/Data/Ch12Environment.asset`, same schema as every other chapter's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light — color (0.55, 0.6, 0.68), intensity 0.28, rotation Euler(55, −35, 0) |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` — Flat, (0.05, 0.06, 0.09) |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` — Exponential, (0.06, 0.07, 0.1), 0.016, uniform across the whole descent (no dive/override, unlike Ch11's dreamscape) |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight`, six entries — see Appendix A.1 |
| `eventLights[]` | — | **unused this chapter** — Ch12 has no docking-alarm-style event light; leave the array empty rather than omitting the field, so the schema stays uniform across chapters |

`behaviour` covers the two authored non-default entries: `ThroneLight0` carries `AmbientPulse(period: 7.4s)` (the node's amber pulse, per the fiction — see §3 candor note on its actual position over the throne, not the node) and `ThroneLight1` carries `ConsoleFlicker(seed: 131)` (the throne-tier's unstable secondary light). All four tier lights (`SpawnLight`/`Tier1Light`/`Tier2Light`/`Tier3Light`) plus each tier's own `_Glow` accent (added per-tier by `Ch12BuildTier`, tinted to that tier's own floor color) carry `behaviour: None`.

## 4. Per-beat scene spec

The chapter plays as five beats, matching both the story treatment's five scenes and the dialogue script's own Beat 0–4 numbering (which the builder's own step labels and code comments follow directly): Beat 0 (the Cairn briefing, voice-only), Beat 1 (the cryo-vault descent — race, skirmish, the cradle-repetition reveal), Beat 2 (waking Vale — the command-key and the crown), Beat 3 (the gut-punch and the Mirror boss), Beat 4 (refusing the throne — the node cut loose, the hook to Ch13). Each beat is documented with the same a–f structure.

**Table conventions, everywhere below** — identical to every other chapter's: art tables carry Position/Rotation, Registry Key, resolved path, and Status; art tables never carry `scale()`/`size()`/`PrimitiveType`; positions/rotations encode blocking and are kept; **Status `MISSING`** means the primitive fallback is active for that row.

---

### Beat 0 — The Cairn (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes for props that will become prefabs. Create **`BuildBeat0Art()`** for the spawn-area set dressing (there is almost none — this beat is voice-only) and **`BuildBeat0Logic()`** for the single dialogue player and its mission step.

#### a. Narrative purpose & emotional target

Beat 0 is the fullest war-room argument the crew has fielded, and it is the chapter's thesis stated out loud before the descent tests it. Cassie-04's cross-index lands the reveal's first half cold and clinical — this node holds not another older make, but "the Ronin make. Yours." — while Sable names the harder personal stake: the last of her own kind, wired not just into storage but into a *trigger* for the whole command-network, and she wants her "off it… before she's a weapon," not merely rescued. The roster's argument is staged as a genuine split rather than a unanimous refusal read aloud: Mera Voss names the live temptation plainly ("If someone has to hold this, I'd rather it was a hand I trust"), Morrigan and Coral Vex answer from the two extremes of institutional and personal knowledge of leashes (an engineer who built them for nine years; a survivor who cut her own and paid decades for it), Kessler asks the harder question before the easy one (what does the tool do to the hand that holds it, not what does it do for us), and Vess reframes her hunt as witnessing the warehouse come apart rather than seizing the prize. Echo's private aside to Cipher alone plants the chapter's real anchor early — "you came to free an army, not lead one" — so that Vale's offer, three beats later, is answering a question the player already heard posed honestly by their own crew.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at the canyon mouth, effectively (0, 0, 4) — the katana "Echo" sits nearby at (2, 1, 4) (see Art, below). The player does not travel in Beat 0; this beat plays out entirely as a stationary war-room VO while the player stands at the chapter's spawn point.
- **Dialogue anchor:** `Dialogue_Beat0_Briefing` at (0, 1, 4).
- No NPCs are physically placed for this beat — every speaker (Cassie-04, Sable, Mera Voss, Kessler, Morrigan, Coral Vex, Vess, Echo) is voice-only, per the class-level CREW-PRESENCE DECISION (§1.1). This is the only beat in the chapter where the wider crew pool speaks at all; from Beat 1 on only Echo (and occasionally Mera Voss/Gryph/Vess/Sable over a degrading comm) checks in, and from Beat 2 onward comm is effectively gone — only Vale, the Edition, and Echo remain.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` (`ch12_beat0_briefing`) — the full war-room briefing, 10 lines, ending on Ronin-7's "Take us down." |

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Katana "Echo" | (2, 1, 4), Euler(−90, 0, 0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnGround` (16×12 platform, with ceiling at RoomH) | center (0, 0, 4) | `Rooms.CryoVaultSpawnGround` | `…/Art/Generated/Rooms/CryoVaultSpawnGround.prefab` | **MISSING** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |

**Note on the katana.** Consistent with Ch9–11's "cost, not initiation" precedent, the katana rides from the start — deep into Act III, there is no rack-wake beat. `BuildSword` places it at world (2, 1, 4), 2 m to the player's right at hand height, a free-standing grabbable the player must reach for, not controller-attached. As with every prior Act III chapter, **the grab is ungated**: nothing in the mission spine requires the player to pick Echo up before descending, so a player who ignores the free-standing blade could in principle reach the Tier2 skirmish unarmed. Flagged for awareness, carried forward from the same gap already documented in Ch9's/Ch11's Scene-Construction docs.

**Candor note on the war-room.** Per (a) above, Beat 0 is framed narratively as a full holo-table scene with the roster physically present and arguing around a projected vault. None of that set exists in the build — there is no holo-table prop, no crew placement, no war-room geometry of any kind. The entire briefing plays as disembodied VO while the player stands alone at `SpawnGround`. This is the same divergence Ch9's/Ch11's Beat 0 candor notes already flag for their own war-room briefings; Ch12 does not attempt to close the gap either. A candidate future prop is a `WarRoomHoloTable` matching the dialogue script's "the holo-table throws a deep cryo-command vault up over the bowl of light." Flagged here, not fixed.

#### d. Combat

None. Beat 0 has zero combat components.

#### e. Dialogue / VO

Set id **`ch12_beat0_briefing`**, position (0, 1, 4), 10 lines, advanced on **Left-Hand "Talk" (Y)**:

| Speaker | Line (as authored in `Chapter12Lines.cs`) | sec |
|---|---|---|
| Cassie-04 | "Last pin, Cipher… It holds the Ronin make. Yours. The last door has your family behind it." | 20 |
| Cassie-04 | "And here's the part that makes it a job and not a funeral… We are not the only ones who read this map." | 26 |
| Sable | "She's the last of mine, Cipher… Get her off the network. Let her be a person before she's a weapon." | 24 |
| Mera Voss | "All of that is true and none of it matters if we get there second… I'm saying don't pretend breaking it is free." | 18 |
| Kessler | "I've given orders that didn't sit right later… I want that answer before I want the vault." | 20 |
| Morrigan | "I built leashes for nine years, Cipher, so let me tell you what you're looking at… The only safe version of that thing is a broken one." | 25 |
| Coral Vex | "I cut one leash in my life, Cipher. My own… We don't hold it. We don't carry it home." | 22 |
| Vess | "Down there is where they filed my people's killers… I've earned that much honesty." | 19 |
| Echo | "This part's only for you, Cipher… Hold on to which one you are. I'll hold it with you." | 18 |
| Ronin-7 | "Then we go down fast, we get there first, and we get her off the network… Echo past that. Take us down." | 20 |

Total runtime ≈ 212 s. This is a **voice-only cast** dialogue set — no speaker except Ronin-7 has a physical presence in the scene, so this table's "position" column is a single shared anchor rather than per-speaker blocking, the same convention every prior chapter's briefing uses.

> **Audit-fix note (Kessler's line).** Per `Chapter12Lines.cs`'s class doc, the source dialogue script has Ronin-7 answer "And Kessler. I heard you," but the source script's own Beat 0 has no `Speaker: Kessler` block for him to be answering — the audit flagged this as a hard script error. `Chapter12Lines.cs` adds a new short Kessler line (transcribed above) between Mera Voss and Morrigan so Ronin's closing line has an antecedent. This is new-authored text, not a verbatim transcription of the source `.md` — do not "correct" it back to match the source script, which is the thing being fixed.

#### f. Audio / Haptics / VR Comfort

- No camera shake — the whole beat is a stationary group VO, so the only feel to sell is spatial audio and the `SpawnLight` accent establishing the mood before the descent.
- **Location fiction.** The briefing plays diegetically aboard the Cairn's war-room while the player physically stands at the cryo-vault's `SpawnGround` (z≈4) — the same convention prior chapters use for a pre-mission VO played over the drop point rather than the ship interior. See item 1's spawn-ambience fix below: today this location fiction plays in silence, with no room tone under it.
- No bespoke haptics scripted for this beat (no combat) — but there **is** a grab: the free-standing katana Echo at (2, 1, 4) is grabbable from the moment the player spawns, firing the standard system-level XR interaction grab haptic.
- **The frostbite clock is live this beat, and it is the loudest missing cue in the chapter.** Per §2's candor note, a player who simply stands through Beat 0's ≈212 s VO crosses `CryoChillController`'s frostbite threshold at ~57 s and takes periodic silent damage for the remaining ~150 s, with `onFrostbite`/`onCleared` wired to nothing. This is the single worst beat for the gap to land in — the player is doing exactly what the design wants (standing still, listening) and is punished for it with no signal. Wire `onFrostbite` → `AudioDirector` shiver sting + `Haptics` shudder, `onCleared` → both stop (§3), at minimum for this beat, ahead of any chapter-wide fix.
- Comfort vignette is inert — the player does not move during Beat 0.

---

### Beat 1 — The Cryo-Vault Descent (Race / Traversal — Down Through the Inventory)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitive cubes for the tier floors, pillars, ramps, or cradle rows — read `Rooms.CryoVaultTier`, `Props.CryoVaultRamp`, and `Props.StasisCradle` from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (all three tier platforms, all three connecting ramps, every cradle-row prop, the pillar dressing) and **`BuildBeat1Logic()`** (`MidVaultReachPoint`, the skirmish squad + `HiveCascadeController` + Sentinel Duelist upgrade, the `DefeatEnemies` step, both dialogue players).
> **The skirmish squad's `HiveCascadeController` is built active even though every member starts inactive** — its own `OnEnable`/timer staggering is harmless while `SetActive(false)` on each member keeps the fight dormant; only the `DefeatEnemies` step's own auto-activation actually starts the fight. Do not add a second, redundant activation trigger for the squad.

#### a. Narrative purpose & emotional target

Beat 1 is the chapter's environmental-storytelling descent, staged explicitly as a **race**, not merely a traversal: a rival force is already inside the vault and ahead, and the production notes are explicit that "the race should be FELT, not just narrated." Crew comm holds through the upper tiers — Mera Voss reads the rival force's pace off telemetry and coaches "slip them, don't clear them"; Gryph reads the frost-slick footing with a veteran's specific craft; Vess asks Ronin-7 to narrate the scale of "the warehouse" that filed her people's killers, paying off her Beat 0 vow. The beat's real hinge is Echo's dawning realization at the Tier2/Tier3 transition: staged deliberately as *resistance*, not a single flat statement — Echo re-reads the cradle frames three times before naming what he already suspects, refusing to soften it ("I'm telling you the cold version first, from someone who loves you"), so that Vale's warmer framing in Beat 2 lands as a manipulation the player has already been inoculated against. Sable's final comm line, felt through the depth rather than heard clearly, is the last full crew contact before the throne-tier goes comm-dark but for Echo, Vale, and the Edition.

**Candor note — the race is purely narrated; it has no mechanical presence, parallel to the war-room gap.** The production note's "the race should be FELT, not just narrated" calls for encounter-or-evade beats where the player can engage the rival vanguard or slip past to save time, a "live and ahead" rival icon, and Mera's coaching to "slip them, don't clear them" — none of which exists in the build. There is no rival-force icon, no clock/timer, and no evade branch anywhere in Beat 1; the only mechanical encounter is the single static skirmish trio at Tier2 (§Beat 1b/c), which the mission spine's step 3 `DefeatEnemies` gate *forces* the player to clear before advancing — the exact opposite of Mera's "don't clear them, slip them." This is quoted approvingly above because the VO delivers it; the build does not.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **Player:** free-walks the length of the shaft (z ≈ 4 → 64, descending in Y from 0 to −4.5 across three platforms and three ramps) on continuous locomotion + snap-turn. No scripted path.
- **`MidVaultReachPoint`:** (−2, −2, 42) *(Tier2 + (0,1,0))*, radius 5 m — gates the `DefeatEnemies` skirmish step on the player physically reaching Tier2.
- **Note — the descent itself is ungated; only the repetition payoff is.** Step 1's `Dialogue_Beat1_Descent` fires immediately on Beat 0's exit, with no position gate stopping the player from walking (or sprinting) straight down the shaft while it plays — a parallel, unflagged sequencing point alongside the ungated katana (§Beat 0c). A rushed player skips the cradle-counting texture the descent is built to deliver. The chapter's load-bearing beat survives this, though: step 4's payoff line, `Dialogue_Beat1_Repetition` ("these are you"), **is** properly reach-gated behind `MidVaultReachPoint` (step 2) and the skirmish's `DefeatEnemies` resolution (step 3), so the repetition reveal cannot be short-circuited even by a player who sprints past the cradle rows.
- **Skirmish squad (3 `Enemy`s), built inactive:**

  | Role | Position | Notes |
  |---|---|---|
  | Skirmisher 0 | Tier2 + (−3, 0, 4) = (−5, −3, 46) | rival-force vanguard / cradle sentinel |
  | Skirmisher 1 | Tier2 + (3, 0, 4) = (1, −3, 46) | rival-force vanguard / cradle sentinel |
  | Skirmisher 2 → **Sentinel Duelist** | Tier2 + (0, 0, −3) = (−2, −3, 39) | upgraded elite — see below |

  All three share `Ch12EnsureSkirmisherDefinition` (maxHealth 60, damage 9, moveSpeed 1.6, attackCooldown 0.85) **except** Skirmisher 2, which `Ch12UpgradeToSentinelDuelist` renames `"SentinelDuelist"`, re-points at `Ch12EnsureSentinelDuelistDefinition` (maxHealth 150, moveSpeed 1.5, attackRange 1.8, telegraphTime 0.85, activeTime 0.8, recoverTime 0.65, staggerTime 1.3, attackCooldown 0.75, damage 16, `postureMaxFraction 0.5`), and adds `PostureMeter` + `PatternedDuelist` (mirrors Ch6's `caradocEnemy` idiom — `PatternedDuelist` reads repeated-side hits; `PostureMeter` is required for `postureMaxFraction` to do anything, since `Enemy.Awake` only calls `PostureMeter.Configure` when one is present on the same GameObject).
  - **`CradleHiveCascade`:** a `HiveCascadeController` wired over all three squad members (`Ch12BuildHiveCascade`), built **active**. Per the component's own doc, this is "the Fracture Protocol" made literal — the trio desyncs through Attacking/Frozen/Conflicted states instead of fighting in lockstep, "the visible signature of a failing collective," directly echoing the chapter's own title and the "one mind, many bodies" cradle-rack motif. It composes with, does not replace, `MirrorPhantom` (which owns the Beat 3 boss's dissolve sequencing, not this squad's behavior).
- No NPCs are placed for this beat beyond the skirmish squad — Mera Voss, Gryph, and Vess's Beat 1 lines are all comm-only, continuing the CREW-PRESENCE DECISION.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | Dialogue | `Dialogue_Beat1_Descent` (`ch12_beat1_descent`) — Echo, Mera Voss, Gryph, Vess as the race begins |
| 2 | ReachTrigger | Gates on `MidVaultReachPoint` (−2,−2,42), radius 5 |
| 3 | DefeatEnemies | `Rival Vanguard + Cradle Sentinels` — activates all 3 skirmish `Health`s, waits for all to reach zero |
| 4 | Dialogue | `Dialogue_Beat1_Repetition` (`ch12_beat1_repetition`) — Echo's dawning realization, Ronin-7's flat "I want a number," Sable's throne-tier warning |
| 5 | ReachTrigger | Gates on `ThroneTierReachPoint` (0,−5,68), radius 6 |

**What changes during the beat:** nothing is added or removed in the vault itself — every platform, ramp, cradle prop, and light is static from build time. The only state changes are step 3's squad activation (and its resolution once all three fall) and the beat's exit at step 5, handing off to Beat 2's throne-tier dialogue.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `Tier1` (12×12 platform + 2 pillars + 2 cradles) | center (2, −1.5, 26) | `Rooms.CryoVaultTier` | `…/Art/Generated/Rooms/CryoVaultTier.prefab` | **MISSING** |
| `Tier2` (12×12 platform + 2 pillars + 5 cradles) | center (−2, −3, 42) | `Rooms.CryoVaultTier` | `…/Art/Generated/Rooms/CryoVaultTier.prefab` | **MISSING** |
| `Tier3` (12×12 platform + 2 pillars + 8 cradles) | center (1, −4.5, 58) | `Rooms.CryoVaultTier` | `…/Art/Generated/Rooms/CryoVaultTier.prefab` | **MISSING** |
| `Ramp0`…`Ramp2` ×3 | see §2 ramp table | `Props.CryoVaultRamp` | `…/Art/Generated/Props/CryoVaultRamp.prefab` | **MISSING** |
| `Tier{n}_Cradle` ×15 total (2+5+8) | see §3's cradle-row description | `Props.StasisCradle` | `…/Art/Generated/Props/StasisCradle.prefab` | **MISSING** |
| `Tier1Light`/`Tier2Light`/`Tier3Light` (accent) | see §3.1 | — | `ChapterEnvironmentProfile.accentLights["Tier1"/"Tier2"/"Tier3"]` | profile |
| `Tier{n}_Glow` (accent, per-tier, tinted to that tier's floor color) | tier center + (0, 2.2, 0) | — | `ChapterEnvironmentProfile.accentLights["Tier{n}Glow"]` | profile |

**Notes on the transition.** `Rooms.CryoVaultTier` must preserve the per-instance tint override `Ch12BuildTier` currently applies (upper grey-blue `(0.22,0.24,0.28)` lerping to deep stasis-blue `(0.1,0.14,0.24)` across the three numbered tiers) — a prefab replacement that bakes one fixed color loses the "the deeper it goes, the colder/more uniform it reads" progression this chapter's palette is built around. `Props.StasisCradle` needs a per-tier instance count (2/5/8) from one prefab, and per the §3 candor note, a future Ronin-face variant is the natural split once the mixed-vs-uniform read matters visually rather than only numerically. Note for that split: the cradle-row prop is currently placed with a single fixed z-offset (`center + (x, 0.9, −2.5)`, one row along the **approach/near edge relative to the +Z walk direction** — see Appendix A.2), so a face-bearing variant should keep facing the walk line (−Z, toward the approaching player) for the silhouette-repetition read to work as the player descends past it, per the SETTING's "the same frame… again and again."

#### d. Combat — the skirmish squad (Fracture Protocol)

Per the chapter's own class doc, the squad is "kept light and cold" deliberately — the real fights are Beat 2's argument and Beat 3's boss, not this encounter. All three enemies fight via the existing melee-AI/`BladeDamager` systems, no bespoke boss logic. The tanky Sentinel Duelist (150 HP, `PostureMeter`-gated stagger via `postureMaxFraction 0.5`, `PatternedDuelist` reading repeated-side hits) sits between the base skirmisher mook (60 HP/9 dmg) and the chapter's actual boss (320 HP/28 dmg per Edition phase) — a deliberate mid-tier step, not an invented escalation. `HiveCascadeController`'s Attacking/Frozen/Conflicted desync means a player fighting the trio expecting lockstep aggression will find members periodically disengaging (Frozen, tinted cold) or turning inward (Conflicted, tinted hot) rather than pressing the attack — the mechanical texture of "a hive fracturing into individuals," and the first combined deployment of `HiveCascadeController` anywhere in the project (previously wired nowhere per the class doc).

**The Sentinel Duelist's position is a gate, not just a coordinate.** Per §Beat 1b, the elite sits at Tier2 + (0,0,−3) = (−2,−3,39) — 3 m ahead of both mooks (z=46) on the player's approach side (−Z), inside `MidVaultReachPoint`'s firing radius (z=42, r=5). Descending `Ramp1`, the tank is the first body the player reaches, gating the two skirmishers beyond it. This is deliberate encounter blocking, not an arbitrary offset — a future implementer should read it that way rather than "tidying" the three positions into a symmetric row.

**Candor note — the trio pops in rather than emerges, the same class of gap flagged elsewhere for the Edition and Vale.** All three skirmishers are built `SetActive(false)` and materialize from empty air the instant step 3's `DefeatEnemies` auto-activates them (`Chapter12Builder.cs:225–229`, 329) — no cradle, no fog, no rouse cue. Canon frames them as "a few auto-roused cradle-sentinels," operatives *waking out of the cradle racks* the player has just spent the whole descent counting, and the chapter's thesis is explicitly "count the cradles" (§3). Three of the player's own-make bodies appearing with no emergence staging is the same unrepresented beat the doc already flags for the Edition's cradle-less emergence (§Beat 3c) and Vale's un-warming cradle (§Beat 2b) — it just hasn't been called out here until now. Near-free fix: reuse the proposed `Vfx.EditionStasisFog` (Appendix B) as a small rouse-burst at each skirmisher's position on activation, and/or fire an `AudioDirector` cradle-rouse/stasis-crack stinger at step 3 — selling "the racks just woke three of your own face at you" rather than three enemies blinking into existence.

#### e. Dialogue / VO

Two sets, both advanced on Left-Hand "Talk" (Y):

`Dialogue_Beat1_Descent` (`ch12_beat1_descent`), position (0, 1, 14), 4 lines, ≈77 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "We're in. And it's the opposite of the last one, Cipher… We are not alone in here, and we are not first. Move." | 22 |
| Mera Voss | "I've got their pace off your drop-telemetry, Cipher, and they're good… Reach the bottom first and the fight up here stops mattering." | 20 |
| Gryph | "Gryph here. Cold deck's a liar, Cipher… Halfway is where the frost takes you." | 16 |
| Vess | "You're walking through their warehouse, Cipher… I want to hear it in your voice and not the count of a ledger." | 19 |

`Dialogue_Beat1_Repetition` (`ch12_beat1_repetition`), position Tier3 + (0,1,−4) = (1,−3.5,54), 5 lines, ≈92 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "Cipher. Hold up. I want to read something and I want to be wrong about it… The same man, racked, over and over, all the way down." | 25 |
| Ronin-7 | "I see them. Keep reading. I want a number." | 7 |
| Echo | "I can't give you one. The tiers go past where my read holds… Keep moving and let's go find out why." | 24 |
| Sable | "Cipher, she felt you stop. The node… You're almost on him." | 21 |
| Ronin-7 | "Then I stop counting and go meet him… Take me to the bottom." | 15 |

**Note — this set's anchor sits ~12 m ahead of where it fires.** `Dialogue_Beat1_Repetition` is anchored at Tier3 (1,−3.5,54), but mission step 4 fires immediately after the Tier2 skirmish resolves (step 3), while the player is still on Tier2 around z≈42–46 — so Echo's "the tiers go past where my read holds" emits from roughly 12 m ahead of and below the player. A mild spatialization oddity for an in-head companion — resolved as fact, not left open: `BuildDialoguePlayer`'s `AudioSource` (`ChapterSharedBuilders.cs:754`) never sets `spatialBlend`, which defaults to 2D/non-attenuated. Every dialogue set in the chapter, this one included, plays from an in-head, non-positional source regardless of anchor position, so the 12 m offset above is cosmetic (a text-panel/position mismatch, not an audible one). This same fact has a real consequence for the chapter's two *physically placed* speakers, Vale and the Edition — see §7.

#### f. Audio / Haptics / VR Comfort

- No camera shake, ever — the fight's feel is carried entirely by `Haptics`, `AudioDirector` blade-clash stingers, and `CombatFeedbackController`'s reticle.
- `BuildAmbienceLayer("FractureTierAmbience", (2,1.2,26), inner 5, outer 18, vol 0.4)` — a spatial ambience bed centered near Tier1, covering the upper descent with a frost/rime texture.
- `Tier3Light` is the coldest, dimmest-warm, and longest-range accent among the three tier lights ((0.28, 0.4, 0.65), intensity 1.5, range 14) — the last tier-light color before the throne-tier's amber breaks the pattern.
- Comfort vignette engages normally across the ~60 m walk-and-descend from spawn to the throne-tier's reach point.
- **`RampThrone`'s grade is worth a specific look in-headset.** Per §2's ramp table, `Ramp0`–`Ramp2` are all ≈5.4°, but `RampThrone` — the last ramp before Beats 2–4, and the one the player descends right at the step-5 handoff into Beat 2 — is ≈16.7°, roughly 3× as steep. It is still a short (≈5.2 m), walkable, continuous-locomotion descent and nothing here argues against the design, but it is the one ramp in the chapter that stands out numerically and merits a comfort check on hardware alongside the gentler upper descent.
- **Acoustic dead zone at spawn.** `FractureTierAmbience`'s outer radius (18) reaches only z≈8–44 from its (2,1.2,26) center; the Beat-0 spawn point (0,0,4) is ≈22 m away — outside the bed entirely. Beat 0's stationary VO and the first ramp both play in silence, with no frost/rime texture until the player nears Tier1. Recommended fix: add `BuildAmbienceLayer("FractureSpawnAmbience", (0,1,4), inner 5, outer 16, vol 0.35)` covering the spawn mouth and `Ramp0`, or widen `FractureTierAmbience`'s outer radius so the two beds meet.
- **The race has no sensory presence — cheapest fix.** Echo's "there's other light moving between us and it that isn't ours" and Mera's "two tiers up and moving clean" (both `ch12_beat1_descent`) describe a rival force the player never hears. Recommended: a spatial distant-combat ambience emitter placed low-and-ahead in the shaft (muffled blade-clashes / clipped rival comm-chatter, sold as the rival vanguard fighting the auto-roused sentinels ahead of the player) — a pure `BuildAmbienceLayer`/`AudioSource` add, no gameplay branch, closing the chapter's largest immersion gap with the tool the chapter already uses.
- **No posture-break cue for the Sentinel Duelist.** Beat 1d documents the elite's `PostureMeter` + `PatternedDuelist` stagger (`postureMaxFraction 0.5`, `staggerTime 1.3s`), but no audio/haptic bullet calls out the stagger moment itself. Add: when the Duelist's posture breaks and it enters its 1.3 s stagger window, fire a distinct `AudioDirector` guard-break stinger plus a heavier `Haptics` pulse — the same feel-vocabulary Ch6 uses for its patterned duelist, and the one legible "reward" beat in an otherwise light-and-cold skirmish. Without it the only tanky enemy in the descent has no distinct feedback signature and reads as a generic damage-sponge.
- **Missing cue.** The dialogue's Beat 1 stage direction has Sable, over comm, "reached toward you and pulled back" the moment "she felt you stop" — a felt-proximity beat tied to `ch12_beat1_repetition`'s trigger that has no audio representation today. A cheap, spatial add: a short comm-static flicker or half-beat dropout on Sable's line in that set, sold as her sensing the player stall at the cradle read.

---

### Beat 2 — Waking Vale (Dialogue / Confrontation — The Command-Key and the Crown)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Rooms.ThroneTierShell` and `Named.CommanderVale` from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (the throne-tier shell's floor/ceiling/three walls, Vale's placement) and **`BuildBeat2Logic()`** (`ThroneTierReachPoint`, all three dialogue players for this beat).
> **Vale is built active from scene start, not revealed by a Trigger step.** There is no "wake from the cryo-throne" animation system in this build — mirrors Ch10's Cassie-04-at-the-Archive precedent (greybox scope cut, not an oversight). Do not add a hidden/reveal step for him without also building the missing animation.

#### a. Narrative purpose & emotional target

This is the most dangerous beat in the chapter precisely because Vale never has to raise his voice. He is old-empire certainty, a strategist who has rehearsed this exact pitch across decades of cold, and the writing is explicit that a lesser story would call his offer victory: the army, the throne, the face on every soldier, delivered as reason rather than raving. Ronin-7 mostly listens and gives nothing — his three lines in this beat ("I know exactly what I'm standing in… don't dress it," "Keep talking. I'm still listening for the part where they get a choice," "You keep saying someone will hold it. You're the one who waited in the cold for it to be you") are refusals of the frame, not arguments won. Echo's two private asides do the real work of keeping the player honest: naming the manipulation in real time ("He's not telling you something you didn't know. He's telling you to feel good about it") and refusing to pretend Vale's stated danger is fake ("He's not wrong about the danger, Cipher. That's what makes him dangerous"), so the eventual refusal in Beat 4 is earned across the whole chapter, not won in a single clever line here. The beat ends on Vale's trump card, delivered as an almost-pleased reveal rather than a threat: he has one thing Ronin-7 has never had to put down on any prior keeper-kill, and he means to show it to him before letting him refuse.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Player:** arrives at `ThroneTierReachPoint` (0, −5, 68) via `RampThrone` and free-walks the enclosed throne-tier for the rest of the chapter. No further reach gates after step 5. *(Precision note: the point's r=6 radius means step 5 can fire as early as z≈62 — essentially at `RampThrone`'s lip, not "just past the top" — with Vale still ~14 m away at z=80. That is intentional greeting-from-afar staging, not the player already standing near him, and it is why "Don't stop on my account" reads correctly at the distance it actually fires.)*
- **Vale:** placed once via `Ch12PlaceStoryNpc(Ch12ValePrefab, throneCenter + (−4, 0, 6), "Vale")` = world (−4, −6, 80), resolving to the real, already-baked `Commander-Vale.prefab`. A **plain `StoryNpc`, no `Health`, no combat component of any kind** — canon is explicit that "he does not draw the command baton" and this beat is "an argument, not a fight." Built active from scene start, per the header note above. **Unconfirmed from code alone: whether `Commander-Vale.prefab` actually carries a modeled baton at his hip.** Canon twice specifies Vale "does not draw the command baton" — a deliberate restraint beat — but the "hand rests near it but never draws" staging can only land if the prop is present to not-draw. Worth a one-line confirmation against the prefab before ship; low priority, single-prop scope.
- **Candor note — Vale's facing is never set, and he may stand facing away from the player through his entire performance.** `Ch12PlaceStoryNpc` → `InstantiateNpc` (`ChapterSharedBuilders.cs:648–658`) sets only `position`, never `rotation` — Vale keeps `Commander-Vale.prefab`'s baked orientation, whatever that is. Contrast the Ronin-7 Edition: `Ch12BuildNamedBoss` calls the same `InstantiateNpc`, then explicitly rotates the result to `Quaternion.Euler(0, 180, 0)` — facing −Z, the approach from `RampThrone` (`Chapter12Builder.cs:555`) — a step Vale's placement never gets. Vale sits at (−4,−6,80); the player arrives from z≈68–74, south (−Z) of him. The dialogue script's stage direction is explicit that Vale has "cold compelling eyes that find RONIN-7 and do not leave him," and §3 already establishes this beat's "reasonable, almost-warm predator" pitch is "carried on face and voice, with no camera moves to help." If the Named-prefab convention here is the same "+Z forward" the Edition needed correcting from, Vale is turned away from the player for the whole greeting/offer/threat sequence. **Recommended fix:** apply the same `Euler(0, 180, 0)` in `Ch12PlaceStoryNpc`, or confirm `Commander-Vale.prefab`'s baked forward is already −Z and this is a non-issue — either way, this is the single strongest open item in the chapter (see §5). See §Beat 2c's note on the open south wall for how this compounds with the `RampThrone` descent sightline: the player watches Vale grow closer for the whole ramp before ever meeting him.
- No NPCs beyond Vale are placed for this beat — the rival force referenced in dialogue ("held at the tier's edge by the architecture and by Vale's evident control of the room" per the production note) has no physical representation in the build; it exists only as narrated pressure.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 6 | Dialogue | `Dialogue_Beat2_Greeting` (`ch12_beat2_greeting`) — Vale's welcome, Ronin-7's flat refusal of the frame |
| 7 | Dialogue | `Dialogue_Beat2_Offer` (`ch12_beat2_offer`) — the crown pitched as reason, Echo's private counter, Vale pressing harder |
| 8 | Dialogue | `Dialogue_Beat2_Threat` (`ch12_beat2_threat`) — Echo conceding the real danger, Ronin-7's cut, Vale's trump-card reveal |

**What changes during the beat:** nothing in the geometry. The only state change across all three steps is dialogue advancing; Vale never moves, never draws a weapon, and no combat component activates. The beat's exit is diegetic — Vale's last line ("Let me show you the part of the offer you haven't priced. Then refuse me, if you still can.") is immediately followed, in the fiction, by the cradle nearest the throne opening. Mechanically that is step 9, the first step of Beat 3.

**Candor note — the dialogue's emergence staging is unbuilt, and unlike the war-room and the race this gap is not otherwise flagged.** The dialogue's Beat 2 stage direction is specific: the cradle nearest the throne, "alone among the racks," has "its blue light begun to warm" through this beat, then "hisses open in a gout of stasis-fog" at the Beat 2→3 cut and the Edition steps out. The build has no throne-adjacent cradle prop for the Edition at all — step 9 simply `SetActive`s phase 1 in open floor at (0, −6, 72) via `MirrorPhantom.Begin()` (§Beat 3b) — so there is no pre-warming light cue during this beat and no stasis-fog emergence VFX at the cut. See §Beat 3c for the corresponding art-side gap and candidate props.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `ThroneTier` shell (20×22, floor + ceiling + W/E/N walls, open south to `RampThrone`) | center (0, −6, 74) | `Rooms.ThroneTierShell` | `…/Art/Generated/Rooms/ThroneTierShell.prefab` | **MISSING** |
| Vale (Commander Vale) | (−4, −6, 80) | `Named.CommanderVale` | `…/Art/Generated/Characters3D/Named/Commander-Vale.prefab` | **EXISTS** |
| `ThroneLight0` (accent, the node's amber pulse) | (−4, −2.4, 74) | — | `ChapterEnvironmentProfile.accentLights["Throne0"]` | profile |
| `ThroneLight1` (accent, unstable secondary) | (4, −2.4, 74) | — | `ChapterEnvironmentProfile.accentLights["Throne1"]` | profile |

**Note on the throne-tier's open south wall.** Only `Throne_WallW`, `Throne_WallE`, and `Throne_WallN` are built (§2) — there is no south wall, matching the room's function as the terminus of `RampThrone`, not a sealed vault. A player standing at the ramp's top can see the whole throne-tier interior — Vale, the node, and (once revealed) the Edition — before physically entering it, which is consistent with the beat's "you've walked a long way down to me… you're standing in the middle of the answer" staging.

This sightline is not incidental to the facing question §Beat 2b raises. Descending `RampThrone` — the chapter's one steep, ≈16.7° ramp — points the player's forward vector straight down the shaft at Vale's (−4,−6,80) position, framed the whole way in the open wall opening, so Vale is a *reveal-on-approach* rather than a static greeter, and "You've walked a long way down to me" (`ch12_beat2_greeting`) lands because the player has literally watched him grow closer across the ramp. If Vale's rotation is in fact unset per §Beat 2b, this sharpens the bug considerably: it is not merely that Vale is turned away during the argument, it is that the player descends the entire ramp staring at Vale's **back** before ever reaching him — the more damning framing, and one more argument for the `Euler(0,180,0)` fix already recommended there.

**Candor note — the cryo-throne itself is unbuilt; Vale rises from bare floor.** The room is named `ThroneTier` and canon centers a physical cryo-throne — "a single cryo-throne where the command-key sleeps"; at the wake, "the cryo-throne has opened and COMMANDER VALE stands out of it, frost sloughing off it in sheets." As built, Vale is a `StoryNpc` standing in open floor at (−4,−6,80) with nothing behind him — the doc commissions the Edition's emergence prop (`Props.EditionCradle`, Appendix B) but never the counterpart for Vale. Recommended: a `Props.CryoThrone` candidate (Appendix B), an open, frost-sloughing throne prop at ~(−4,−6,81) — just behind Vale on the approach axis — sized so he reads as having risen from stasis rather than materialized on bare deck. It can stay purely decorative, since Vale is built active with no wake animation to synchronize against. The throne is the architectural rhyme to the cradles racked above (Vale is the one body *enthroned*, not merely racked), and its absence is the largest un-flagged set-dressing gap in the chapter's most important room. **Cheap companion fix:** reuse `Vfx.CryoFrostSteam` (§3, already commissioned for rail/wall rime) as a low-rate emitter parented to Vale himself — and to this throne prop once it lands — so the dialogue's "frost still steaming off his antique rank-sash," "cryo-rasp under the polish" image has a visual layer, not just VO; the chapter's coldest character currently bleeds cold only in the script.

#### d. Combat

None. Vale carries no `Health` and no `Enemy`/`MeleeAttacker` component — this beat is architecturally incapable of becoming a fight, matching the canon direction that Vale "survives as a rival vision," not a boss.

#### e. Dialogue / VO

Three sets, all advanced on Left-Hand "Talk" (Y), Echo the only other voice (private, to Cipher):

`Dialogue_Beat2_Greeting` (`ch12_beat2_greeting`), position throneCenter + (0,1,−4) = (0,−5,70), 2 lines, ≈32 s:

| Speaker | Line | sec |
|---|---|---|
| Vale | "Don't stop on my account. You've walked a long way down to me… no idea what you're standing in." | 25 |
| Ronin-7 | "I know exactly what I'm standing in. I counted it on the way down. Say what you want and don't dress it." | 7 |

`Dialogue_Beat2_Offer` (`ch12_beat2_offer`), position throneCenter + (0,1,−2) = (0,−5,72), 5 lines, ≈78 s:

| Speaker | Line | sec |
|---|---|---|
| Vale | "Good. Plain, then, since you've earned plain… And then they did the only sane thing. They made more of you." | 27 |
| Echo | "There it is, Cipher. The warm version… That's the trap. Not the fact. The feeling he wants you to hang on the fact." | 15 |
| Vale | "I can hear you weighing it. The Program that made you has gone soft… answering the one man with the standing to lead it. You." | 23 |
| Ronin-7 | "Keep talking. I'm still listening for the part where they get a choice. I haven't heard it yet." | 6 |
| Vale | "Spoken like a man who hasn't held the door yet… I could simply take the key and never ask." | 26 |

`Dialogue_Beat2_Threat` (`ch12_beat2_threat`), position throneCenter + (0,1,0) = (0,−5,74), 3 lines, ≈50 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "He's not wrong about the danger, Cipher. That's what makes him dangerous… Don't let him make you solve his problem his way just because it's the only way he's offering." | 19 |
| Ronin-7 | "You keep saying someone will hold it. You're the one who waited in the cold for it to be you." | 7 |
| Vale | "No. I don't need you out of the way and I don't need you on the throne… Then refuse me, if you still can." | 24 |

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point — Vale's pitch and the tightening threat are carried entirely by VO performance and lighting, never camera motion.
- `ThroneLight0`'s `AmbientPulse(period: 7.4s)` runs continuously through this beat — read narratively as the node's slow amber breathing under Vale's argument, the one warm thing in a cold room making its own quiet counter-argument to his "I am the only kindness here" framing, though as built the light itself sits over Vale/the throne rather than the node (§3 candor note) — a player standing exactly at the node during this beat is lit by the cold `ThroneLight1`, not this warmth. Note that `ThroneLight0` is also, incidentally, Vale's only warm key light today (§3): the §3 recommendation to move it to the node must ship with a replacement key for Vale, or his face — the one thing carrying this entire beat — goes under-lit the moment the fix lands.
- `BuildAmbienceLayer("FractureThroneDreadAmbience", (−4,−2.4,74), inner 5, outer 18, vol 0.4)` — a dedicated dread bed under the whole throne-tier, distinct from the tier-ambience bed that covers the upper descent.
- No haptics scripted — this is an argument beat, consistent with the project's "combat feel only where there's combat" convention.
- Comfort vignette is largely inert; the player has already arrived and typically stays near the reach point for this stretch, though nothing blocks free movement.

---

### Beat 3 — The Gut-Punch and the Mirror (Dialogue / Reveal / Boss — Cut Down Your Own Face)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Read `Named.RoninEditionClone` from `ArtAssetRegistry` for both boss phases. Create **`BuildBeat3Art()`** (both `Ch12BuildNamedBoss` placements, the synthesized `ArmR/Sword/Blade/BladeTip` combat rig on each) and **`BuildBeat3Logic()`** (the `MirrorPhantom` sequencer, the null-object Prompt step, the `MirrorGranter` and its Trigger step, all three dialogue players for this beat).
> **The two Edition phases are NOT a health-scaled "phase 2 is weaker" pattern.** `finalChildScale` is forced to `1f` specifically so the second phase never visually shrinks — both phases share the identical `Ch12EnsureEditionDefinition` (320 HP / 28 dmg), because the horror is perfect symmetry, not a numbers ramp. Do not reintroduce a scale-down or stat-reduction on the second phase.
> **`MirrorPhantom.onSequenceCleared` must stay wired to `MissionDirector.AdvanceFromPrompt`**, not to a second, separate Trigger step — the null-object Prompt step (step 12) exists specifically so `BeginDefeatEnemies`'s force-activate-everything semantics never fire against `MirrorPhantom`'s own "one phantom active at a time" sequencing.

#### a. Narrative purpose & emotional target

The chapter's identity reveal lands in two channels at once, and canon is explicit that the beat must read as source-horror, never relief: "not 'I am someone,' but 'I am the well they poured all of it out of.'" Channel one is diegetic data — the Ronin-7 Edition's own conditioning-phrase lines ("Source-template confirmed. Original present. Designation holds.") stand in for the command-network readout the source script describes cascading with a single repeated designation (rows of one designation running the length of the readout); there is no built readout-holo surface today; `Vfx.CommandNetworkReadout` (Appendix B) is the natural commission. Channel two is the body standing in front of the player, his exact face with no one home.

**Placement spec for `Vfx.CommandNetworkReadout`.** Canon anchors it precisely and the commission should not ship without a position: Echo's `ch12_beat3_intro` line says it is "writing your answer on **the wall behind him**" (script 370), and "him" is the Edition at (0,−6,72), facing −Z toward the approaching player (§Beat 3c) — so "behind him" is the far wall on the +Z side, i.e. `Throne_WallN` (throneCenter + (0,1.8,11) = (0,−4.2,85), size 20×3.6×0.2, §2/Appendix A.3). Mount the readout flush against `Throne_WallN`'s south-facing surface, centered on the Edition's x=0 approach line, facing −Z so it reads to the player over the Edition's shoulder exactly as Echo's line describes. Size it to run the visible width of the wall (~18 of the wall's 20 m, matching the surface it is mounted to) so it reads as "rows of one designation running the length of the readout" (Production Note 360) rather than a small screen. **Its dissolve should be driven by the same node-cut event as `Vfx.NodeReleaseDissolve`** (Beat 4, step 17) — canon's Beat 4 opening has "the readout that cascaded Vale's case collapses to nothing" (script 448) at the same moment the node is cut loose, so one event should collapse both the wall readout behind the Edition and (once built) the node-release dissolve, tying the reveal's data-channel and the title action's payoff to the same built object.

Echo carries almost the entire emotional weight of the reveal alone, staged deliberately as *resistance breaking down*, not a clean announcement — three back-to-back private lines building from "don't go looking for yourself in those eyes" to the flat arithmetic of "one through six, flawed, culled… I'm so sorry. I wanted it to be the other answer." Ronin-7's own lines are almost nothing by design — "Say it again. The number. One through six" and "Six. They threw six people into the dark to get one that would hold still" — canon's explicit instruction is that he gives away no speech before the duel; the meaning is reclaimed only *after* the kill, earned by the act rather than asserted before it. The Edition itself never breaks character even once — it is "pure conditioning, no person left to free… there is no mercy branch and no almost" — so its death, and the Mirror unlock that follows, must land as grief for what Ronin-7 escaped, never triumph over an enemy defeated.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **Both Edition phases**, built inactive via `Ch12BuildNamedBoss(Ch12EditionPrefab, throneCenter + (0,0,−2), "Ronin-7 Edition", editionDef, playerHealth)` — both at the **identical** world position (0, −6, 72), resolving to the real, disk-confirmed `Ronin-7_Edition_Clone.prefab` ("it IS his face," per the class doc — not a separate art asset from Ronin-7's own). Each is `FitNamedCharacter`-grounded, rotated to face −Z (the approach from the throne-tier's open south side), fitted with a synthesized `CapsuleCollider` (center (0,1.1,0), height 2.4, radius 0.5) and a synthesized `ArmR/Sword/Blade/BladeTip` chain, then `Enemy`-wired against `Ch12EnsureEditionDefinition` (maxHealth 320, damage 28, moveSpeed 1.6, attackCooldown 0.8 — the same numbers used for both phases; see header note).
- **`EditionMirrorPhantom`:** a `GameObject` carrying `MirrorPhantom`, `phantoms = [editionPhase1.Health, editionPhase2.Health]`, `finalChildScale = 1f`. Built **inactive**; the Trigger step (step 9) activates it, which fires `Begin()` via `MirrorPhantom.OnEnable` — `Begin()` deactivates every phantom except index 0 and activates the first, matching the idiom `ProximityDoor`/`AbilityGranter` already use elsewhere (activate-on-enable rather than a separate initialization call).
- **`MirrorGranter`:** a `GameObject` carrying `AbilityGranter` (`abilityId = AbilityId.Mirror`), built inactive, activated by its own dedicated Trigger step (step 14) — the "grief before the gift" ordering invariant every prior ability-unlock chapter (Ch9's Overdrive, Ch11's Unbroken) also protects. **Do not collapse this into the same step as the duel's resolution** — the Aftermath dialogue (step 13) must play, and be seen to finish, before the grant fires.
- **The boss sequence's clearance advances the mission out of the null-prompt step:** `UnityEventTools.AddPersistentListener(editionPhantom.onSequenceCleared, missionDirector.AdvanceFromPrompt)` is wired once, at build time, not per-phase — the mission stays parked on step 12's Prompt (a `null` prompt object; nothing to interact with directly) until both phantoms are dead and `MirrorPhantom` fires its own clearance event.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 9 | Trigger | Activates `EditionMirrorPhantom` → fires `MirrorPhantom.Begin()` via `OnEnable`, revealing phase 1 |
| 10 | Dialogue | `Dialogue_Beat3_Intro` (`ch12_beat3_intro`) — the Edition's conditioning-phrase confrontation, Echo's first read of the readout |
| 11 | Dialogue | `Dialogue_Beat3_Reveal` (`ch12_beat3_reveal`) — Echo's full inversion, Vale's coronation-framing, Ronin-7's "Six," the Edition's engage line |
| 12 | Prompt | *(null prompt object)* — the mission parks here for the duration of the two-phase duel; advanced only by `MirrorPhantom.onSequenceCleared` → `AdvanceFromPrompt`, never by a player input |
| 13 | Dialogue | `Dialogue_Beat3_Aftermath` (`ch12_beat3_aftermath`) — "I don't fear him, Echo. I grieve him," Echo's bitter unlock framing |
| 14 | Trigger | Activates `MirrorGranter` → grants `AbilityId.Mirror` immediately via `OnEnable` |

> **⚠ Invariant — step 13 must precede step 14, same protection class as Ch9's Overdrive-grant and Ch11's Unbroken-grant ordering.** Do not reorder; the grant must follow the grief beat, not replace or precede it.

**Candor note — the Edition is a live, attacking `Enemy` from step 9, so the reveal plays under active melee pressure, not the standoff the dialogue script stages.** `DialoguePlayer` does not pause combat — no `Time.timeScale` change, no enemy freeze on activation — and `Enemy`/`MeleeAttacker` drops into Chase→Attack on its own tick the instant `SetActive(true)` fires. Step 9's Trigger activates phase 1 immediately, so steps 10–11 (`ch12_beat3_intro`, `ch12_beat3_reveal` — the chapter's emotional floor: "Say it again. The number. One through six" / "Six. They threw six people into the dark to get one that would hold still") play while phase 1 is already chasing and swinging, forcing the player to dodge live melee while advancing dialogue line-by-line on Left-Hand "Talk" (Y). This contradicts the dialogue script's own staging: the Edition's `"Engaging. The original resists removal. Escalating to terminate."` is the **last line of `ch12_beat3_reveal`** (step 11), and only then does the stage direction read `[They fight.]` — as built, the fight has already been underway for two full dialogue steps by the time the Edition says "Engaging." **The obvious fix — deferring the Trigger to step 12 — is worse than the bug it fixes, and is not this document's recommendation.** `MirrorPhantom.Begin()` (`MirrorPhantom.cs:31,43`) couples "visible" and "attacking" in one `SetActive(true)`; there is no third state between "not in the scene" and "hostile and swinging." Moving the Trigger from step 9 to immediately before step 12's Prompt does stop the early aggression, but it does not produce the standoff the dialogue script stages — it removes the Edition's body from the room entirely for the length of steps 10–11. Canon's stage direction (script line 358) puts the Edition physically "between RONIN-7 and the node… his exact mirror," and Echo's `ch12_beat3_intro` line is built around looking at that body — "Don't go looking for yourself in those eyes. Nobody's home… it's writing your answer on the wall behind him" (script 370) — a "him" who, under the deferred-Trigger version, is not standing anywhere to be described. That is a worse canon violation than the current early-aggression: it keeps the dialogue but deletes the one physical image the reveal is built around. **The genuinely faithful staging is present-but-passive** — the Edition's body visible and grounded at (0,−6,72) from step 9, melee AI suppressed until step 12's own Prompt releases it on the engage line — but the frozen `MirrorPhantom`→`Enemy` coupling cannot deliver that without new logic (a passive/idle gate distinct from the Health-sequencing itself) that sits outside this chapter's local scope (§1.2's frozen-helper list; `MirrorPhantom` is chapter-agnostic and reused elsewhere). **If constrained to the existing stack as-is, keep activation at step 9** — visible and wrongly aggressive — rather than deferring it to step 12 — correctly passive but invisible. The reveal's load-bearing image is the visible face, not the absence of a swing; this document's own earlier phrasing of "recommended fix" pointed at the wrong lever. See §Beat 3d and §Beat 3f for the combat-timing and cue consequences of leaving activation at step 9. A real fix would add a passive/idle gate to `MirrorPhantom` itself — out of scope here since the component is frozen and chapter-agnostic (§1.2); flagged for whoever owns cross-chapter enemy-sequencing work, not for this document to commission.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Ronin-7 Edition — phase 1 | (0, −6, 72), Euler(0,180,0) | `Named.RoninEditionClone` | `…/Art/Generated/Characters3D/Named/Ronin-7_Edition_Clone.prefab` | **EXISTS** |
| Ronin-7 Edition — phase 2 | (0, −6, 72), Euler(0,180,0) *(identical transform to phase 1)* | `Named.RoninEditionClone` | `…/Art/Generated/Characters3D/Named/Ronin-7_Edition_Clone.prefab` | **EXISTS** |

**No new environment geometry this beat.** `ThroneTier`'s shell, walls, and both accent lights are Beat 2's build and are reused unmodified — this beat instantiates only the two boss phases, matching the "Medbay kill-box is Beat 1's room, unmodified" convention Ch1 established for reused-geometry beats.

**Note on the identical placement.** Because both phases occupy the exact same world transform and only one is ever active at a time (`MirrorPhantom.Begin()` deactivates all but the current index), there is no visual pop or repositioning between phases from the player's perspective — the second body simply appears where the first fell, read as "the same enemy, still standing" rather than a new arrival. This is load-bearing for the "his exact school returned with no hitch and no mercy" read; a future patch that varies the two phases' positions would break that continuity.

**Candor note — no cradle, no emergence VFX.** Per the §Beat 2b candor note, the dialogue's reveal is a physical event — the throne-adjacent cradle warms, then hisses open in stasis-fog and the Edition steps out of it. As built, phase 1 activates directly at (0, −6, 72) in open floor; there is no `EditionCradle` prop anywhere on the throne-tier and no emergence VFX at step 9. Candidate commissions for Appendix B: a throne-adjacent `Props.StasisCradle_RoninFace`-family cradle (reusing the §3 face-bearing cradle split) sized to stand the Edition in, plus a `Vfx.EditionStasisFog` gout-of-fog burst timed to step 9's activation, so the reveal reads in space the way the dialogue assumes.

**Candor note — the dropped blade is not represented.** Canon stages the Edition's blade as a deliberate emotional image across three Beat 3–4 stage directions — "the Edition's blade is left on the vault floor," "he does not pick the blade up," and Beat 4 opens on "the RONIN-7 EDITION is down, his blade on the floor" — the copy that leaves him a copy, and the thing Ronin-7 refuses to take up. The builder synthesizes an `ArmR/Sword/Blade/BladeTip` chain on each Edition phase, but nothing persists it on death; the Edition simply deactivates with the rest of its body. A low-cost, high-symbolism candidate: `Props.EditionBladeDropped`, a static blade prop left at (0, −6, 72) once the second phase's `Health` reaches zero.

#### d. Combat — the Ronin-7 Edition (two-phase Mirror duel)

The Edition fights via the existing melee-AI/`BladeDamager` systems on both phases — no bespoke boss scripting beyond `MirrorPhantom`'s sequencing. Both phases share `Ch12EnsureEditionDefinition` (maxHealth 320, damage 28, moveSpeed 1.6, attackCooldown 0.8) — the same numbers on both, a deliberate absence of escalation between phases (see header note). This is the chapter's only true boss encounter and the culmination of the ability chain: weakpoint-sight (Ch7), `OverdriveController` (Ch9), `PhaseStepController` (Ch10), and `UnbrokenWard` (Ch11, unlocked and usable here, unlike Ch11's own fight where it was granted but not yet available) are all live and available; Mirror itself is granted only *after* this fight resolves, so the player cannot summon a phantom double during the very duel that earns it. Per canon, the Edition "speaks only in conditioning-phrases, never as a person," and there is explicitly "no mercy branch and no almost" — `AuthorPromptStep`'s null-object step and `MirrorPhantom`'s pure Health-sequencing enforce this mechanically: there is nothing for the player to interact with except landing the killing blow on each phase in turn.

**Combat is live two full dialogue steps early (§Beat 3b).** Because step 9's Trigger activates phase 1 before steps 10–11's dialogue plays, the player is dodging and taking melee damage from a fully aggroed `Enemy` throughout `ch12_beat3_intro` and `ch12_beat3_reveal` — roughly 104 s of combat pressure layered under dialogue the chapter needs the player to actually absorb, not merely survive. The duel proper (step 12's Prompt) is unaffected either way — both phases fight identically once engaged — but the *reveal* plays as a fight instead of a confrontation, which is the timing issue, not a combat-tuning one. **Per §Beat 3b, this is the lesser of two flaws, not a bug to reorder away.** Deferring activation to step 12 would remove the early-aggression cost documented here, but at the price of the Edition's body being absent from the scene for the whole reveal — a bigger loss than 104 s of unwanted combat pressure under dialogue the player can still hear and act on. The genuine fix is a passive-until-step-12 state on the Edition, not a relocated Trigger; until that exists, this combat-timing cost stands as accepted, not pending removal.

#### e. Dialogue / VO

Three sets, all advanced on Left-Hand "Talk" (Y):

`Dialogue_Beat3_Intro` (`ch12_beat3_intro`), position throneCenter + (0,1,−2) = (0,−5,72), 5 lines, ≈47 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 Edition | "The asset stays on the network. That is the order… Removal is." | 11 |
| Echo | "Cipher. Don't go looking for yourself in those eyes. Nobody's home… Rows of one make. Yours. All printed from one source." | 14 |
| Ronin-7 Edition | "Source-template confirmed. Original present. Designation holds." | 4 |
| Echo | "And the source has a tag on it. Seventh iteration. First that held. One through six, culled… then they printed all of this off you." | 12 |
| Ronin-7 | "Say it again. The number. One through six." | 6 |

`Dialogue_Beat3_Reveal` (`ch12_beat3_reveal`), position throneCenter + (0,1,−1) = (0,−5,73), 4 lines, ≈57 s — **Ladder C rung 2 delivered here, exactly once:**

| Speaker | Line | sec |
|---|---|---|
| Echo | "One through six. Flawed. Culled. You're seven, the first that worked… I'm so sorry. I wanted it to be the other answer." | 14 |
| Vale | "Now you understand what you're worth… Your only choice is whether the original dies in this room or rules from it." | 22 |
| Ronin-7 | "Six. They threw six people into the dark to get one that would hold still." | 6 |
| Ronin-7 Edition | "Engaging. The original resists removal. Escalating to terminate." | 5 |

`Dialogue_Beat3_Aftermath` (`ch12_beat3_aftermath`), position throneCenter + (0,1,1) = (0,−5,75), 2 lines, ≈36 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "That's what I'd have been. My hands, my blade, and nobody awake behind the eyes… Now help me get past him to her." | 13 |
| Echo | "It's done. He's down, and his shadow came loose, and… Let's get her loose and get out of your own face." | 23 |

**Note — Echo's "off your left" node callout (in the full `ch12_beat3_aftermath` line: "There she is, off your left, still on the network") assumes a facing the free-roaming player may not hold.** The last node sits at (3,−4.8,78) — +X of throne center. A player facing north toward the throne/Vale/Edition (+Z) has +X on their **right**, not left; it is *Vale* (x=−4) who sits to a throne-facing player's left. Because Ch12 is continuous locomotion with no forced heading, the player's actual orientation when this line fires is unconstrained, so the mismatch may never register in play — but it is the same class of anchor-vs-facing assumption already flagged for `ch12_beat1_repetition`'s anchor offset (§Beat 1e). As-built, a throne-facing player has the node screen-right, not screen-left: a candidate for either a small dialogue tweak ("off your right") or accepting it as Echo's loose in-head deixis rather than a literal spatial callout.

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point in the duel — the fight's feel is carried entirely by `Haptics` (controller pulse per hit/parry against a mirrored opponent), `AudioDirector` stingers (blade-clash, the kill sting), and `CombatFeedbackController`'s reticle.
- `ThroneLight1`'s `ConsoleFlicker(seed: 131)` runs through the whole beat, giving the throne-tier's secondary light an unstable, uneasy pulse under the reveal and the fight — deliberately distinct from `ThroneLight0`'s steady amber breathing, so the room reads as two competing moods (warmth at the node, instability everywhere else) exactly as the reveal splits the player's own read of themselves.
- No unique haptic is authored for the Mirror unlock itself (step 14's `AbilityGranter` is a silent state flip) — the *dialogue* (`ch12_beat3_aftermath`) carries the unlock's emotional weight, consistent with canon's instruction to author it "bitter, not triumphant." **That instruction rules out a triumphant cue, not a cue at all.** Canon's step-14 image — "for an instant a second Cipher flickers at his side before folding into him" — is the Mirror ability literally previewed at the moment it is granted, distinct from `Vfx.EditionBladeShadowDissolve` (the shadow *into Echo*, Appendix B) and currently unrepresented anywhere. A single cold, low `Haptics` pulse plus a hollow `AudioDirector` sting timed to step 14 would sell "the shadow entered you" without triumph; a `Vfx.MirrorPhantomPreview` candidate (Appendix B) would give the flicker itself a home.
- **Scope cut, with a near-free fix (see §6).** Canon's Beat 2→3 stage direction has "the node's amber pulse quickens" as the Edition wakes at step 9; as built, `ThroneLight0`'s `AmbientPulse` stays flat through the reveal. A one-shot brightening or a temporary period-drop on that pulse (7.4s → faster) at step 9's `MirrorPhantom.Begin()` would sell the line with no new asset, giving the reveal a lighting event to accompany the otherwise-silent `SetActive`.
- **The frostbite clock keeps running through the duel, with the same missing cue.** If the player has already crossed threshold earlier in the chapter (§2/§3 candor notes), the periodic frostbite damage continues silently under the fight's own `Haptics`/`AudioDirector` cues, indistinguishable from blade damage. Wiring `onFrostbite`/`onCleared` to a distinct shiver sting + slow shudder (§3) would keep the two damage sources legibly separate even mid-duel, not just during the quieter beats.
- Comfort vignette engages normally through the duel's movement.
- **The reveal plays under live melee pressure — see §Beat 3b/3d.** Step 9's Trigger activates the Edition before `ch12_beat3_intro`/`ch12_beat3_reveal` play, so the player is dodging an already-aggroed `Enemy` through both sets rather than standing in the standoff the dialogue script stages. Per §Beat 3b, this is not a sequencing fix to apply — relocating the Trigger to step 12 removes the Edition's body from the room for the whole reveal, a worse loss than the early aggression. The real fix is a passive-until-step-12 state on the Edition (outside this chapter's scope); until it exists, activation stays at step 9 and the two cues below both assume that.
- **Missing cue.** The dialogue's Beat 2→3 cut has "SABLE's voice breaks through the thin comm for half a second, a wordless sound, and is gone" at the exact moment the Edition emerges — unrepresented in the build (step 9's activation is silent apart from the dialogue sets themselves). A short, spatial half-second comm-break stinger over step 9's `MirrorPhantom.Begin()` activation would sell the node as a present character reacting to the reveal, despite never being placed as one. This cue is pinned to step 9, not a future reorder — per §Beat 3b, the activating Trigger stays there.

---

### Beat 4 — Refusing the Throne (Dialogue / The Refusal — Free, Never Command)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No new geometry this beat — the throne-tier shell and both accent lights are already built. Create **`BuildBeat4Logic()`** only: the remaining six dialogue players, the `ChapterOutro`/`CampaignFlagSetter`/`CHAPTER 12 COMPLETE` canvas chain, and the final Trigger step.
> **Vale survives.** There is no death, capture, or defeat state for him anywhere in this beat's steps — he remains the same active, Health-less `StoryNpc` placed in Beat 2, present through his parting lines and never removed from the scene. Do not add a `Health` component or a defeat condition to Vale in a future patch without a corresponding story change; his survival is load-bearing for the Act IV throughline (§9).

#### a. Narrative purpose & emotional target

The chapter's moral climax is played as quiet decision, not spectacle — Vale's final offer drops the "velvet" by his own admission and makes his strongest possible case at the exact moment canon says the protagonist is most vulnerable to it, having just grieved his own copy. Ronin-7's refusal is the chapter's thesis made flesh, answering Vale's strongest argument not with a better strategy but with a line he will not cross: "An army of the freed, commanded, is just the cage with a kinder face on the door." The node-cut that follows mirrors every prior keeper-kill's freed-shadow beat but is written as the heaviest of the chapter — Echo's "make it gentle" is the one moment of open tenderness in an otherwise flat, controlled chapter — because this fragment completes the Engine's build-record (every node, every conscript, the whole order of the build, finally whole). Sable's homecoming line closes her Beat 0 arc explicitly. Vale's parting is written as an undefeated withdrawal, not a loss — "You didn't beat the idea today. You just declined it" — planting him as the Act IV foil rather than a boss replaced by whoever comes next. Echo's final line of the chapter plants the Ch13 hook without naming the maker, closing Act III on what canon calls "the cruelest truth the saga has."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Player:** remains free-standing on the throne-tier; no further reach gates in this beat.
- **Vale:** unchanged from Beat 2's placement (−4, −6, 80) — present and speaking through steps 15 and 19, never removed or deactivated.
- **`CHAPTER 12 COMPLETE` canvas** (`Ch12BuildCompleteCanvas`): worldspace canvas at throneCenter + (0, 1.4, 10) = (0, −4.6, 84), built **inactive**, revealed by the outro.
- **`ChapterOutro`:** at throneCenter + (0, 1, 9) = (0, −5, 83), built inactive. Carries a `CampaignFlagSetter` (`flags = ["ch12_complete"]`) wired to `ChapterOutro.OnActivated` via a persistent listener at build time, and a `completeCanvas` reference to the canvas above. **Sets only `ch12_complete` — no recruit flag**, per the class doc: Vale is an antagonist/foil, not a recruitable ally, and the last node is freed, not recruited (Ch12 stays ally-free, matching Ch11's identical precedent).

**Candor note — nothing requires the player to actually approach the node, so the chapter's title action is skippable.** §3's candor note asserts "the player only reaches the vent by walking up to Vale/the node," and canon's own stage direction has Ronin-7 "**crosses to** the node and cuts her loose" (script 448) — a physical action — but every step from 15 through 21 advances purely on Left-Hand "Talk" (Y), and none of them is a reach gate; the "no further reach gates in this beat" line above is exact. A player who stands at `ThroneTierReachPoint` (0,−5,68) for the whole beat can complete Beat 4 end to end without ever walking into `NodeWarmth`'s x[0,6], z[75,81] volume or within reach of the node hologram at (3,−4.8,78). That means both the chapter's title action and the only in-chapter frostbite refuge (§3's three candor notes on `NodeWarmth`'s coverage) are optional, not staged. Recommended: either accept this as loose staging consistent with the chapter's free-roam convention, or add a soft proximity expectation — a small `ReachTrigger` at the node gating step 17 (`Dialogue_Beat4_Cut`) — so "crosses to the node" is something the mission spine actually asks the player to do, not just narrates.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 15 | Dialogue | `Dialogue_Beat4_Offer` (`ch12_beat4_offer`) — Vale's final offer, no velvet |
| 16 | Dialogue | `Dialogue_Beat4_Refusal` (`ch12_beat4_refusal`) — the refusal, spoken in full |
| 17 | Dialogue | `Dialogue_Beat4_Cut` (`ch12_beat4_cut`) — Echo confirms the choice, "cut her loose… make it gentle" |
| 18 | Dialogue | `Dialogue_Beat4_Homecoming` (`ch12_beat4_homecoming`) — Sable's gratitude, the last node freed |
| 19 | Dialogue | `Dialogue_Beat4_Parting` (`ch12_beat4_parting`) — Vale's undefeated withdrawal, Ronin-7's plain answer |
| 20 | Dialogue | `Dialogue_Beat4_Hook` (`ch12_beat4_hook`) — Echo's Ch13 hook, closing Act III |
| 21 | Trigger | Activates `ChapterOutro` — `OnActivated` fires `CampaignFlagSetter.SetFlags` (`ch12_complete`), reveals the complete canvas, and (per `ChapterOutro`'s standard fade/`ZoneCompleted` behavior, shared with every other chapter) fades to black and publishes `ZoneCompleted` |

**What changes during the beat:** no geometry changes at all — every dialogue step is pure VO advancing across a static throne-tier, and the beat's only state changes are the six dialogue advances and the final outro activation. The freeing of the last node (a `BuildHologram`-based decorative prop, never a combat target — see §Beat 4c) is narrated by `ch12_beat4_cut`/`ch12_beat4_homecoming`, not represented by any additional builder-side state change; there is no dedicated "node fragment collected" GameObject or flag beyond `ch12_complete` itself.

#### c. Art & Environment Instantiation (no `BuildBeat4Art` — geometry reused from Beat 2)

No new geometry. Two references from earlier beats are load-bearing here and are documented for completeness:

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| The last node (decorative hologram) | throneCenter + (3, 1.2, 4) = (3, −4.8, 78) | — | `BuildHologram(null, pos)` — a cyan `FloatingArrow`-driven projection, no dedicated art key | primitive *(shared helper, not a registry-backed prop)* |
| `NodeWarmth` (`HeatVent` trigger, unrelated to combat) | throneCenter + (3, 1, 4) = (3, −5, 78) | — | invisible trigger volume, no renderer | logic-only |

**Note on the last node's staging.** Per the class doc, unlike Cassie-04/the Dreaming Archive/Sever's Archive in prior chapters, this node is never given a personal name in dialogue (Sable only ever calls her "the last of mine") and is never fought — she is freed by dialogue plus the `ch12_complete` campaign flag, not combat. `Ch12PlaceLastNode` mirrors Ch11's `ArkshipCore` hologram treatment exactly: a decorative `BuildHologram` instance, not a `StoryNpc`.

**Candor note — the freed sister has no humanizing visual; she reads as a waypoint arrow, not a person.** `Ch12PlaceLastNode` → `BuildHologram(null, pos)` is the same generic cyan cylinder+sphere+`FloatingArrow` projection used for any decorative hologram in the project — nothing distinguishes this instance as a person. Sable's Beat 0 through-line is explicitly "let her be a person before she's a weapon," Echo's `ch12_beat4_cut` line is "cut her loose… make it gentle," and Sable's homecoming line pays off "I've got the last of mine" — yet unlike Cassie-04, the Dreaming Archive, or Sever's Archive in prior chapters, nothing in the build tells the player this projection *is* someone. Recommended: a faint human/figure silhouette read on the node's hologram (still decorative, still un-named, still `BuildHologram`-family) — a `Vfx`/hologram commission distinct from `Vfx.CommandNetworkReadout` — so "cut her loose" lands on a *someone* rather than an arrow.

**Candor note — the node-cut's visual climax has zero build representation, and the gap runs deeper than the missing event: the node has no static wiring to darken in the first place.** Beat 4b already notes the freeing is narrated, not state-changed; there is no dedicated node-fragment GameObject, no conduits-go-dark event, and (per the §3 candor note) `ThroneLight0` — the light the fiction calls the node's amber — actually sits over the throne, not this hologram, so there is nothing here to dim or kill on the cut even if wired. But canon repeatedly wires the node into "a lattice of command-conduits," and as built `Ch12PlaceLastNode` is a bare `BuildHologram(null, pos)` floating at (3,−4.8,78) with no geometry tethering it to anything — no conduits run from the node to the walls or the throne today. Without visible conduits to darken, "cut her loose from the network" has no physical referent for the player to read, independent of whether an event ever fires on them. `Props.CommandConduits` (Appendix B) — static wiring from the node outward — is the companion commission `Vfx.NodeReleaseDissolve` needs to have anything to act on; together they make the chapter's title action (severing the node from the net) legible in space.

#### d. Combat

None. No enemy is spawned or activated in Beat 4 — the Edition's two phases are already resolved before step 15 fires.

#### e. Dialogue / VO

Six sets, all advanced on Left-Hand "Talk" (Y):

`Dialogue_Beat4_Offer` (`ch12_beat4_offer`), throneCenter + (0,1,3) = (0,−5,77), 1 line, 24 s:

| Speaker | Line | sec |
|---|---|---|
| Vale | "You're standing exactly where I hoped you would… It is the most good you will ever do." | 24 |

`Dialogue_Beat4_Refusal` (`ch12_beat4_refusal`), throneCenter + (0,1,4) = (0,−5,78), 1 line, 20 s:

| Speaker | Line | sec |
|---|---|---|
| Ronin-7 | "You keep calling it the most good I'll ever do… And I'm not going to keep it. I'm going to break it." | 20 |

`Dialogue_Beat4_Cut` (`ch12_beat4_cut`), throneCenter + (2,1,4) = (2,−5,78), 1 line, 14 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "That's the man who walks back up the stairs, Cipher. Cut her loose… Do it." | 14 |

`Dialogue_Beat4_Homecoming` (`ch12_beat4_homecoming`), throneCenter + (2,1,5) = (2,−5,79), 1 line, 20 s:

| Speaker | Line | sec |
|---|---|---|
| Sable | "She's off. Cipher, she's off the network, I can feel her be just a person again… I will never be able to tell you what that's worth." | 20 |

`Dialogue_Beat4_Parting` (`ch12_beat4_parting`), throneCenter + (0,1,6) = (0,−5,80), 2 lines, 32 s:

| Speaker | Line | sec |
|---|---|---|
| Vale | "You think you've broken it… We'll see how long a galaxy of orphans lets you keep declining." | 20 |
| Ronin-7 | "Maybe. But I declined it… Stay in your cold. I have somewhere to be." | 12 |

`Dialogue_Beat4_Hook` (`ch12_beat4_hook`), throneCenter + (0,1,8) = (0,−5,82), 1 line, 14 s:

| Speaker | Line | sec |
|---|---|---|
| Echo | "The record's whole now, Cipher… We go to him. The man who made you. That's the last door, and it's the only one left." | 14 |

Total Beat 4 runtime ≈ 124 s across six dialogue players.

#### f. Audio / Haptics / VR Comfort

- No camera shake — the refusal, the node-cut, and Vale's parting are entirely VO- and lighting-carried.
- `ThroneLight0`'s `AmbientPulse` continues through the node-cut dialogue — narratively, the amber pulse that has been "the only warmth in the place" the whole chapter is the thing being freed at this exact moment, but per the §3 candor note the light is positioned over the throne, not the node, so this beat cannot currently be sold by the light actually changing state; co-locating `ThroneLight0` with the node (or a future `Vfx.NodeReleaseDissolve`, see Beat 4c) would let dimming/killing it on the cut make the line land visually rather than only in VO. Vale is present and speaking through this same beat (`ch12_beat4_parting`, step 19) — the co-location move must ship together with the replacement key light for him flagged in §3/Beat 2f, or his parting withdrawal loses its face-light at the same moment the node gains one.
- **Candor note — the node-cut's climactic visual (conduits dark, readout collapsed) has no representation.** Canon's title-moment stage direction — "her shadow lifts off the lattice; the command-conduits go dark; the readout that cascaded Vale's case collapses to nothing" — is narrated only by `ch12_beat4_cut`/`ch12_beat4_homecoming` (see Beat 4b/c); there is no dedicated node-fragment GameObject, no conduit-darkening event, and no VFX key for it in Appendix B, unlike the Edition's death, which does commission `Vfx.EditionBladeShadowDissolve`. A parallel `Vfx.NodeReleaseDissolve` (or `Vfx.CommandNetworkCollapse`) candidate is added to Appendix B.
- **Candor note — the node-cut, the chapter's title action, has no blade interaction and no cut cue.** Ronin-7's "I'm going to break it" (`ch12_beat4_refusal`) resolves mechanically as `ch12_beat4_cut` (step 17) advancing on the same Left-Hand "Talk" (Y) input as any other dialogue line — no grab, no swing, no `Haptics`, no `AudioDirector` sting marks the moral climax of Act III and the payoff of the whole "free, never command" spine. Recommended: pair a single decisive blade-cut `Haptics` pulse with an `AudioDirector` conduit-sever/lattice-collapse sting, both timed to step 17's advance, alongside the already-listed `Vfx.NodeReleaseDissolve` and the "command-conduits go dark" event above — so the chapter's defining action is felt through the sanctioned feel-vocabulary rather than being indistinguishable from reading a line.
- No haptics scripted for this beat — consistent with the project's "combat feel only where there's combat" rule; this is the chapter's decompression stretch, mirroring Ch1 Beat 4's "the fight is over, now deal with what it cost" pacing.
- **Recommended — a "warmth entering the vent" sensory beat.** Per §3's candor notes, `NodeWarmth` is effectively only reachable in Beat 4 (its box excludes the Beats 2–3 duel/argument floor), and the moment the player finally steps into it — "the only warmth in the place" — has no cue today. Wiring `onCleared` (§3) to a warm `AudioDirector` swell paired with the cold-shiver `Haptics` pulse *stopping* would let the vault's single warm spot register physically at the exact beat Echo says "cut her loose… make it gentle" (`ch12_beat4_cut`, step 17) — cheap, on-theme, and built entirely from tools already present in the project.
- Comfort vignette is largely inert; the player is not required to move for any of the six dialogue steps.
- `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` run once at the end of the build (chapter-wide, not beat-specific) — the enclosed, three-walled `ThroneTier` reads with a distinct, larger reverb than the open-air tiers above it, giving Beat 4's stillness a physically "bigger" acoustic space to sit in than the corridor-like descent.

---

## 5. Character travel-route master table

**No NPC physically travels during Chapter 12.** This is a chapter-wide invariant, not an omission — the builder never constructs an `NpcWalker`, unlike Ch1/Ch9/Ch10's `NpcWalker`+`Trigger` idiom.

| Character | Placement | Moves? | Builder call |
|---|---|---|---|
| Vale | (−4, −6, 80), active from scene start | **No** — stationary for the entire chapter, present through Beats 2 and 4 | `Ch12PlaceStoryNpc(Ch12ValePrefab, throneCenter + (−4,0,6), "Vale")` |
| Ronin-7 Edition (phase 1 + phase 2) | (0, −6, 72), both phases, built inactive | **No** — both phases occupy the identical transform; `MirrorPhantom` swaps which is active, neither relocates | `Ch12BuildNamedBoss(Ch12EditionPrefab, throneCenter + (0,0,−2), …)` ×2 |
| Cassie-04, Sable, Mera Voss, Kessler, Morrigan, Coral Vex, Vess, Gryph | *(no physical placement)* | Voice-only over comm, all beats they speak in | — (dialogue-only, per CREW-PRESENCE DECISION) |
| The last node | (3, −4.8, 78), decorative hologram | **No** — a `BuildHologram` prop, not an NPC | `Ch12PlaceLastNode(throneCenter + (3,1.2,4))` |

**Facing note (Vale) — unresolved, see §Beat 2b.** `Ch12PlaceStoryNpc` sets only `position`; it never overrides `rotation`, so Vale keeps `Commander-Vale.prefab`'s baked forward. The Ronin-7 Edition, by contrast, is explicitly rotated to face −Z (`Chapter12Builder.cs:555`) immediately after the same `InstantiateNpc` call the two share. Vale sits at (−4,−6,80); the player approaches and argues from z≈68–80, south (−Z) of him. Whether Vale currently faces the player through Beats 2 and 4 depends entirely on the prefab's baked forward, which cannot be confirmed from code alone — flagged here, alongside the recommended `Euler(0,180,0)` fix, as the strongest genuinely open item in this document.

**Y-invariant note (does not apply here).** Unlike every chapter that uses `NpcWalker` (whose waypoints must all carry the same grounded floor Y or the walked character sinks/floats), Chapter 12 has no waypoint math to protect — `Ch12PlaceStoryNpc` and `Ch12BuildNamedBoss` both re-add their placement's non-zero floor height once after `FitNamedCharacter` grounds the mesh to world y=0 (`go.transform.position += Vector3.up * pos.y`), the same one-line idiom Ch6/Ch9/Ch10/Ch11 use for non-zero-floor placements, but there is no walked path for it to protect over multiple waypoints.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Mood | Accent entry | Behaviour | What changes during the beat |
|---|---|---|---|---|
| 0 — The Cairn (briefing) | neutral, close, pre-descent | `accentLights["Spawn"]` | `None` | none — stationary VO beat |
| 1 — The Descent (race, skirmish, repetition) | cooling with depth, bone-grey to deep stasis-blue | `accentLights["Tier1"/"Tier2"/"Tier3"]` + per-tier `_Glow` | `None` on all | none lighting-wise; the tier color/cradle-count progression is static from build time, not a runtime trigger |
| 2 — Waking Vale (the crown) | cold command, one warm exception | `accentLights["Throne0"/"Throne1"]` | `AmbientPulse(7.4s)` on Throne0; `ConsoleFlicker(seed 131)` on Throne1 | none — both lights are already active and pulsing/flickering from scene start; Vale's arrival is narrated, not lit. **Recommended edit (§3):** co-locate `ThroneLight0` with the node/`HeatVent` at ~(3, −4.x, 78) instead of its current throne-adjacent (−4,−2.4,74) — the sanctioned fix, not just an observation, so "stand in the node's warmth" reads under the same light the fiction names. **This move requires a replacement cool-toned key for Vale** (currently `ThroneLight0` is his only warm key, ≈7 m away) or he loses his key light entirely, lit only by the cold `ThroneLight1` at ≈10.6 m — see §3 |
| 3 — The Gut-Punch and the Mirror | reveal, then duel | same two throne accents | unchanged | **Trigger (step 9):** the Edition's first phase is revealed (`SetActive` via `MirrorPhantom.Begin()`); no lighting event accompanies the reveal — canon calls for "the node's amber pulse quickens" as the Edition wakes, so the flat pulse here is a scope cut, not the intended read. **Recommended edit:** a one-shot brightening or a temporary period-drop on `ThroneLight0`'s `AmbientPulse` (7.4s → faster) at step 9 sells the line at near-zero cost, no new asset required. Per §Beat 3b, step 9 stays the activation point (not step 12) — this lighting beat lands on the same moment the Edition's body actually appears |
| 4 — Refusing the Throne | decompression, the node freed | same two throne accents | unchanged | **Trigger (step 21):** `ChapterOutro` fades to black over its standard fade duration, ending the chapter |

Fog is the same baseline exponential bed for the entire chapter — a single profile value, never overridden per-room (unlike Ch11's dreamscape, which swaps fog treatment on dive entry; Ch12 has no equivalent transition).

## 7. Audio / VO manifest cross-reference

Fifteen canonical dialogue sets, defined in `Chapter12Lines.cs` and consumed via `Chapter12Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch12_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch12_beat1_descent` | 1 | (0, 1, 14) — `Dialogue_Beat1_Descent` |
| `ch12_beat1_repetition` | 1 | (1, −3.5, 54) *(Tier3 + (0,1,−4))* — `Dialogue_Beat1_Repetition` |
| `ch12_beat2_greeting` | 2 | (0, −5, 70) *(throneCenter + (0,1,−4))* — `Dialogue_Beat2_Greeting` |
| `ch12_beat2_offer` | 2 | (0, −5, 72) *(throneCenter + (0,1,−2))* — `Dialogue_Beat2_Offer` |
| `ch12_beat2_threat` | 2 | (0, −5, 74) *(throneCenter + (0,1,0))* — `Dialogue_Beat2_Threat` |
| `ch12_beat3_intro` | 3 | (0, −5, 72) *(throneCenter + (0,1,−2))* — `Dialogue_Beat3_Intro` |
| `ch12_beat3_reveal` | 3 | (0, −5, 73) *(throneCenter + (0,1,−1))* — `Dialogue_Beat3_Reveal` |
| `ch12_beat3_aftermath` | 3 | (0, −5, 75) *(throneCenter + (0,1,1))* — `Dialogue_Beat3_Aftermath` |
| `ch12_beat4_offer` | 4 | (0, −5, 77) *(throneCenter + (0,1,3))* — `Dialogue_Beat4_Offer` |
| `ch12_beat4_refusal` | 4 | (0, −5, 78) *(throneCenter + (0,1,4))* — `Dialogue_Beat4_Refusal` |
| `ch12_beat4_cut` | 4 | (2, −5, 78) *(throneCenter + (2,1,4))* — `Dialogue_Beat4_Cut` |
| `ch12_beat4_homecoming` | 4 | (2, −5, 79) *(throneCenter + (2,1,5))* — `Dialogue_Beat4_Homecoming` |
| `ch12_beat4_parting` | 4 | (0, −5, 80) *(throneCenter + (0,1,6))* — `Dialogue_Beat4_Parting` |
| `ch12_beat4_hook` | 4 | (0, −5, 82) *(throneCenter + (0,1,8))* — `Dialogue_Beat4_Hook` |

Each is built by the local `Ch12BuildDialogue` wrapper (not the shared clip-loader default, which looks in the wrong folder for this chapter): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch12WireVoiceClips`, resolving each line's `AudioClip` from `Chapter12Lines.ClipName(setId, index, speaker)` — pattern `ch12_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist, so a partial VO batch is loud, not silent. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all fifteen `DialoguePlayer`s.

**Candor note — every dialogue set is 2D/non-spatial, which leaves Vale and the Edition, the chapter's only two physically placed speakers, voicing from centerline anchors offset from their own bodies.** Per §Beat 1e, `BuildDialoguePlayer`'s `AudioSource` never sets `spatialBlend` (default 2D) — the shipped house convention, and almost certainly correct for Echo's in-head companion voice. But it applies uniformly: Vale's six lines (`Dialogue_Beat2_Greeting/Offer/Threat`, `Dialogue_Beat4_Offer/Refusal/Parting`) all anchor on the room's x=0 centerline at z=70/72/74/77/78/80, while Vale himself stands at (−4,−6,80) — 4 to 11 m off that line depending on the step — and the Edition's three lines (`ch12_beat3_intro`/`_reveal`) anchor similarly offset from its own (0,−6,72) position. For an argument beat and a boss duel sold entirely on presence ("carried on face and voice, with no camera moves to help," §3), this is worth an explicit call rather than an implicit default: either accept the 2D convention as-is, or, for a future pass, co-locate Vale's and the Edition's anchors nearer their bodies so a 3D-voice option exists without a wholesale audio-architecture change.

**Dialogue is data, not art.** None of this changes in the refactor — the fifteen set ids, their positions, and the clip-resolution pattern are canon.

SFX/ambience bed, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip / emitter | Used for |
|---|---|
| `FractureSpawnAmbience` (`BuildAmbienceLayer`) *(recommended — see §Beat 1f)* | closes the acoustic dead zone at `SpawnGround`/`Ramp0`, outside `FractureTierAmbience`'s reach |
| `FractureTierAmbience` (`BuildAmbienceLayer`) | frost/rime ambience covering the upper descent, centered near Tier1 |
| `FractureThroneDreadAmbience` (`BuildAmbienceLayer`) | dread bed under the whole throne-tier |
| `ProceduralAudioClipBuilder.AssignGeneratedClips()` | fills in any procedurally-generated SFX slots left unassigned by the explicit builders above |

Unlike Ch1/Ch11, Chapter 12 has **no docking-alarm/event-light SFX pair** and **no dive-entry/dive-exit comm-cut stingers** — comm degrades gradually across Beat 1's dialogue lines (a narrative device, not an `AudioDirector` filter effect) rather than cutting on a single scripted trigger.

**Candor note — the comm-degradation texture is specifically authored and has zero build representation.** The dialogue script's CREW-COMM production note is emphatic that Ch12's thinning is its own distinct texture, not a reuse of an earlier chapter's — "clean and cold, not warped or haunted… comm thins to static and clipped fragments," depth-triggered, gone entirely by the time comm goes dark at the throne-tier in Beat 2. As built, every comm speaker (Mera Voss, Gryph, Vess, Sable) voices at full fidelity from the same 2D, non-attenuated `DialoguePlayer` convention as every other line in the chapter (see the spatial-audio candor note above) — there is no low-pass/static ramp by depth anywhere in Beat 1. The sentence above frames the absence as a design choice; it is better read as a gap — the one sensory layer the SETTING block explicitly distinguishes from Ch9–11 is narrated only, never heard. Recommended: a depth-keyed low-pass/static filter ramp on comm-tagged lines, thinning across Beat 1 and gone by Beat 2 as the fiction states — this needs per-set filter support the current `Ch12BuildDialogue` path lacks, so "the vault's depth thins it" is heard, not only narrated. The recommendation above is only the setup half of the arc; the payoff is `ch12_beat4_homecoming` ("she's off the network, I can feel her be just a person again"), which should punch through the established static as a momentary break in the dark comm — a beat of comparative clarity against the silence Beat 2 left behind — rather than arrive at the same full fidelity every other line in the chapter already has. Authored that way, the decay stops being a texture that thins and is never referenced again and becomes a dramatic device with a resolution: Sable breaking back through at the exact moment her node is freed, closing her Beat 0→4 arc in the audio channel the way the dialogue already closes it in text.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 12 — The Fracture** (`XRRigBuilder.BuildChapter12TheFracture()`).
2. **EditMode is the gate.** Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call. Consult `Project/Docs/CHAPTER-BUILD-LEDGER.md` for the current baseline count; do not invent a number here.

   > ⚠ **Coverage blind spot.** No EditMode test invokes `BuildChapter12TheFracture()` or loads `Ch12_TheFracture.unity`. The suite covers pure logic only — `Chapter12Lines` data shape and the `NoLine_MentionsSoren` guard (per the class doc, protecting Ladder C rung 2's canon boundary). **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **Soren-name regression coverage.** `Chapter12LinesTests.NoLine_MentionsSoren` (referenced in both `Chapter12Builder.cs`'s and `Chapter12Lines.cs`'s class docs) asserts the birth name never appears anywhere in `Chapter12Lines.cs` — it is reserved for Ch16. **Any patch to Beat 3's reveal dialogue must keep this test green**; this is the one canon-boundary regression the suite actually catches for this chapter.
4. **Mirror-sequence regression (manual, until an EditMode fixture exists).** Verify in the live editor that killing Edition phase 1 correctly activates phase 2 at the identical transform, and that killing phase 2 fires `onSequenceCleared` exactly once, advancing the mission out of step 12's null Prompt. A patch that reorders `phantoms` or changes `finalChildScale` away from `1f` should be caught by eye, not assumed safe.
5. **Safe-zone survival test (new, once §1.4 lands).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted and the safe zone is decorative.
6. **Fallback audibility test (new, once §1.5 lands).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty vault, never an exception.
7. **Perf reference bar — not yet recorded.** Establish one at the first `script-execute` `UnityStats` read of the fresh build (§1.6) and record it here before any prefab lands. Profile the Tier-2 `HiveCascadeController` skirmish specifically, not just the Edition duel — it is the newest combined AI stack in the project (§1.6).
8. **Console check:** `Ch12WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch did not fully land — check `console-get-logs` after a rebuild.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter12TheFracture()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter12Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild. Nothing outside it survives. See `Project/Docs/IMPROVEMENT-SUMMARY.md`.
- **Do not auto-delete orphan materials.** Reversible cleanup only, via `Editor/Art/ArtGenerationMenu` — consistent with every other chapter's rule.
- **Reject any prefab import that introduces a `MeshCollider`.** A high-fidelity art pass on the tier platforms, ramps, cradle rows, or throne-tier shell is exactly the vector that reintroduces one — check FBX import settings' "Generate Colliders" on every prop landing in the registry.
- **Vale's survival is canon-load-bearing — do not "fix" it into a fight.** §Beat 4a/b are explicit: Vale is never a boss to be killed, captured, or defeated in this chapter. He is "the wrong answer" the whole rebellion has been walking toward, and his undefeated withdrawal in `ch12_beat4_parting` sets up his role as the Act IV foil. A future patch that adds him a `Health` component or a defeat trigger breaks this continuity intentionally, not accidentally — flag it loudly if ever proposed.
- **The birth name "Soren" must never appear in this chapter's dialogue.** Per both builder and `Chapter12Lines.cs` class docs, Ladder C rung 2 (the template reveal) is delivered without ever naming him — the wound stays open, reclaimed only in Ch16. `Chapter12LinesTests.NoLine_MentionsSoren` is the regression guard (§8).
- **The Ronin-7 Edition prefab is literally Ronin-7's own mesh, not a separate asset.** `Ch12EditionPrefab` points at `Ronin-7_Edition_Clone.prefab`, distinct on disk from the player's own rig mesh but visually identical by design ("it IS his face," per the class doc). Do not "improve" this by giving the Edition a distinguishing visual tell (a scar, a different tint) without a corresponding story change — the horror is explicitly that there is no visible difference.
- **The cradle-count reveal (2/5/8 across Tier1/2/3) is currently numeric only, not visual** (§3 candor note). A future art pass that gives Tier1 mixed/anonymous cradle meshes and Tier2/3 face-bearing Ronin cradles would close a real gap between the dialogue's "the makes up top were mixed, down here they're not" and what the player can currently see, which is fifteen identical tinted boxes.
- **No mercy branch exists for the Edition, matching canon exactly.** `AuthorPromptStep`'s null-object step and `MirrorPhantom`'s pure Health-sequencing mean there is no dialogue choice, no yield state, and no alternate outcome to the duel — it ends only when both phases' `Health` reach zero. Do not add one; canon is explicit that "there is no mercy branch and no almost."

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch12Environment.asset`. Currently set inline at the top of `BuildChapter12TheFracture` (`Chapter12Builder.cs:118–140`).

| | Value |
|---|---|
| Directional key | color (0.55, 0.6, 0.68), intensity 0.28, rotation Euler(55, −35, 0) |
| Ambient | mode **Flat**, color (0.05, 0.06, 0.09) |
| Fog | mode **Exponential**, color (0.06, 0.07, 0.1), density 0.016 |
| Tier color lerp | upper (0.22, 0.24, 0.28) → deep (0.1, 0.14, 0.24), `Mathf.InverseLerp(0, tiers.Length−1, i)` across the 3 tiers |
| `SpawnGround` floor / ceiling tint | (0.16, 0.17, 0.2) / (0.05, 0.05, 0.06) |
| `ThroneTier` floor / ceiling tint | (0.05, 0.06, 0.08) / (0.02, 0.03, 0.04) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour |
|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.6, 0.65, 0.75) | 1.0 | 10 | none |
| `Tier1Light` | (2, 1.2, 26) | (0.55, 0.6, 0.72) | 1.2 | 12 | none |
| `Tier2Light` | (−2, −0.2, 42) | (0.4, 0.5, 0.7) | 1.3 | 12 | none |
| `Tier3Light` | (1, −1.6, 58) | (0.28, 0.4, 0.65) | 1.5 | 14 | none |
| `ThroneLight0` | (−4, −2.4, 74) | (0.9, 0.65, 0.3) | 1.6 | 16 | `AddAmbientPulse(period: 7.4f)` — "the node's amber pulse" |
| `ThroneLight1` | (4, −2.4, 74) | (0.35, 0.45, 0.7) | 1.4 | 14 | `AddConsoleFlicker(seed: 131f)` |

Per-tier `_Glow` accents (added inside `Ch12BuildTier`, one per tier, tinted to that tier's own lerped floor color, intensity 1, range 9): `Tier1_Glow` (2,0.7,26), `Tier2_Glow` (−2,−0.8,42), `Tier3_Glow` (1,−2.3,58).

No event lights this chapter (§3.1).

### A.2 Beat 0/1 — Spawn Ground and the Descent

| Element | Coordinates / value | Component / method |
|---|---|---|
| `SpawnGround` | center (0,0,4), size (16,0,12) → `BuildFloorCeiling(world, "SpawnGround", (0,0,4), (16,0,12), (0.16,0.17,0.2), (0.05,0.05,0.06))` | `Chapter12Builder.cs:146` |
| Katana "Echo" | (2,1,4), Euler(−90,0,0) | `BuildSword(pos, rot, weapon, Ch12EchoBladePrefab)` |
| `Tier1` | floor cube center (2,−1.5,26)+(0,−0.2,0), scale (12,0.4,12); 2 pillars; 2 cradles; `Tier1_Glow` | `Ch12BuildTier` + `Ch12BuildCradleRow(count:2)` |
| `Tier2` | floor cube center (−2,−3,42)+(0,−0.2,0), scale (12,0.4,12); 2 pillars; 5 cradles; `Tier2_Glow` | `Ch12BuildTier` + `Ch12BuildCradleRow(count:5)` |
| `Tier3` | floor cube center (1,−4.5,58)+(0,−0.2,0), scale (12,0.4,12); 2 pillars; 8 cradles; `Tier3_Glow` | `Ch12BuildTier` + `Ch12BuildCradleRow(count:8)` |
| Pillars (`{tier}_PillarA`/`_PillarB`) | center + (−3,0.9,−1.5) / center + (3,0.9,1.5), scale (0.5,1.8,0.5), tint floorColor×0.7 | `Ch12BuildTier` |
| Cradle props (`{tier}_Cradle` ×N) | center + (lerp(−5,5,i/(N−1)), 0.9, −2.5), scale (0.7,1.8,0.6), tint (0.3,0.55,0.85) | `Ch12BuildCradleRow` |
| `Ramp0` | mid = ((0,0,10)+(2,−1.5,26))/2, rotation from rise/run, scale (12,0.4,16.07) | `Ch12BuildRamp` |
| `Ramp1` | tiers[0]→tiers[1] | `Ch12BuildRamp` |
| `Ramp2` | tiers[1]→tiers[2] | `Ch12BuildRamp` |
| Skirmish squad ×3 | (−5,−3,46), (1,−3,46), (−2,−3,39); `Ch12EnsureSkirmisherDefinition` (60 HP/9 dmg/1.6 spd/0.85 CD); all `SetActive(false)` | `BuildEnemy` |
| Sentinel Duelist upgrade | renames skirmish[2] → `"SentinelDuelist"`; swaps to `Ch12EnsureSentinelDuelistDefinition` (150 HP, moveSpeed 1.5, range 1.8, telegraph 0.85, active 0.8, recover 0.65, stagger 1.3, CD 0.75, dmg 16, postureMaxFraction 0.5); adds `PostureMeter` + `PatternedDuelist` | `Ch12UpgradeToSentinelDuelist` |
| `CradleHiveCascade` | `HiveCascadeController` over all 3 skirmish `MeleeAttacker`s, built active | `Ch12BuildHiveCascade` |
| `MidVaultReachPoint` | (−2,−2,42) | reach step 2, radius 5 |
| Dialogue players | `Dialogue_Beat1_Descent` (0,1,14); `Dialogue_Beat1_Repetition` (1,−3.5,54) | `Ch12BuildDialogue` |
| Mission steps | 0–5 of 22 | `AuthorDialogueStep`/`AuthorReachStep`/`AuthorDefeatStep` |

### A.3 Beat 2/3/4 — The Throne-Tier

| Item | Value | Source |
|---|---|---|
| `ThroneTier` floor/ceiling | center (0,−6,74), size (20,0,22) → `BuildFloorCeiling(world, "ThroneTier", (0,−6,74), (20,0,22), (0.05,0.06,0.08), (0.02,0.03,0.04))` | `Chapter12Builder.cs:173` |
| `Throne_WallW` | throneCenter + (−10,1.8,0), size (0.2,3.6,22) | `BuildWall` |
| `Throne_WallE` | throneCenter + (10,1.8,0), size (0.2,3.6,22) | `BuildWall` |
| `Throne_WallN` | throneCenter + (0,1.8,11), size (20,3.6,0.2) | `BuildWall` — **no south wall**, open to `RampThrone` |
| `RampThrone` | tiers[2] (1,−4.5,58) → throneCenter + (0,0,−11) = (0,−6,63), scale (12,0.4,5.22) | `Ch12BuildRamp` |
| Vale | (−4,−6,80); `StoryNpc` displayName "Vale"; no Health, no combat | `Ch12PlaceStoryNpc(Ch12ValePrefab, ...)` |
| Last node (hologram) | (3,−4.8,78); cyan cylinder + sphere + `FloatingArrow` | `Ch12PlaceLastNode` → `BuildHologram(null, pos)` |
| Ronin-7 Edition phase 1 + 2 | both (0,−6,72), Euler(0,180,0); `Ch12EnsureEditionDefinition` (320 HP/28 dmg/1.6 spd/0.8 CD); synthesized `ArmR/Sword/Blade/BladeTip`; `SetActive(false)` | `Ch12BuildNamedBoss(Ch12EditionPrefab, ...)` ×2 |
| `EditionMirrorPhantom` | `MirrorPhantom`, `phantoms=[phase1.Health, phase2.Health]`, `finalChildScale=1`; inactive | build-inline |
| `MirrorGranter` | `AbilityGranter`, `abilityId=Mirror`; inactive | build-inline |
| `NodeWarmth` | (3,−5,78); `BoxCollider` trigger (6,3,6); `HeatVent` | build-inline |
| `ThroneTierReachPoint` | (0,−5,68) | reach step 5, radius 6 |
| Dialogue players | `Dialogue_Beat2_Greeting` (0,−5,70); `Dialogue_Beat2_Offer` (0,−5,72); `Dialogue_Beat2_Threat` (0,−5,74); `Dialogue_Beat3_Intro` (0,−5,72); `Dialogue_Beat3_Reveal` (0,−5,73); `Dialogue_Beat3_Aftermath` (0,−5,75); `Dialogue_Beat4_Offer` (0,−5,77); `Dialogue_Beat4_Refusal` (0,−5,78); `Dialogue_Beat4_Cut` (2,−5,78); `Dialogue_Beat4_Homecoming` (2,−5,79); `Dialogue_Beat4_Parting` (0,−5,80); `Dialogue_Beat4_Hook` (0,−5,82) | `Ch12BuildDialogue` ×12 |
| `CHAPTER 12 COMPLETE Canvas` | (0,−4.6,84); worldspace `Canvas`+`Image`+`Text`; inactive | `Ch12BuildCompleteCanvas` |
| `ChapterOutro` | (0,−5,83); `CampaignFlagSetter` flags=["ch12_complete"]; `completeCanvas` ref wired; inactive | build-inline |
| Mission steps | 6–21 of 22 | `AuthorDialogueStep`/`AuthorTriggerStep`/`AuthorPromptStep` |

### A.4 Scene root hierarchy (current)

`BuildChapter12TheFracture()` creates these as **siblings**, not nested: `Directional Light`, `CryoVault` (all tier/ramp/cradle/wall geometry), the six accent lights, `Game` (`GameState` + `CombatFeedbackController`), the player rig, `MidVaultReachPoint`, `ThroneTierReachPoint`, fifteen dialogue-player roots, `EditionMirrorPhantom`, `MirrorGranter`, `NodeWarmth`, `CradleHiveCascade`, the `CHAPTER 12 COMPLETE` canvas, `ChapterOutro`, and `Mission`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and five `[BEAT_N_LOGIC]` roots, and moves `CryoVault`'s contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Three resolve; everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.CommanderVale` | `Art/Generated/Characters3D/Named/Commander-Vale.prefab` | **EXISTS** |
| `Named.RoninEditionClone` | `Art/Generated/Characters3D/Named/Ronin-7_Edition_Clone.prefab` | **EXISTS** |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Rooms.CryoVaultSpawnGround` | `Art/Generated/Rooms/CryoVaultSpawnGround.prefab` | MISSING |
| `Rooms.CryoVaultTier` | `Art/Generated/Rooms/CryoVaultTier.prefab` | MISSING |
| `Rooms.ThroneTierShell` | `Art/Generated/Rooms/ThroneTierShell.prefab` | MISSING |
| `Props.CryoVaultRamp` | `Art/Generated/Props/CryoVaultRamp.prefab` | MISSING |
| `Props.StasisCradle` | `Art/Generated/Props/StasisCradle.prefab` | MISSING |
| `Props.StasisCradle_RoninFace` *(candidate split — see §3 candor note)* | `Art/Generated/Props/StasisCradle_RoninFace.prefab` | MISSING |
| `Props.EditionCradle` *(candidate — see §Beat 2b/3c candor notes)* | `Art/Generated/Props/EditionCradle.prefab` | MISSING |
| `Props.CryoThrone` *(candidate — see §Beat 2c candor note)* | `Art/Generated/Props/CryoThrone.prefab` | MISSING |
| `Props.EditionBladeDropped` *(candidate — see §Beat 3c candor note)* | `Art/Generated/Props/EditionBladeDropped.prefab` | MISSING |
| `Props.CommandConduits` *(candidate — see §Beat 4c candor note)* | `Art/Generated/Props/CommandConduits.prefab` | MISSING |
| `Vfx.EditionBladeShadowDissolve` | `Art/Generated/VFX/EditionBladeShadowDissolve.prefab` | MISSING |
| `Vfx.MirrorPhantomPreview` *(candidate — see §Beat 3f candor note)* | `Art/Generated/VFX/MirrorPhantomPreview.prefab` | MISSING |
| `Vfx.EditionStasisFog` *(candidate — see §Beat 3c candor note)* | `Art/Generated/VFX/EditionStasisFog.prefab` | MISSING |
| `Vfx.CryoFrostSteam` *(candidate — see §3 candor note)* | `Art/Generated/VFX/CryoFrostSteam.prefab` | MISSING |
| `Vfx.PlayerBreathFog` *(candidate — see §3 candor note)* | `Art/Generated/VFX/PlayerBreathFog.prefab` | MISSING |
| `Vfx.NodeReleaseDissolve` *(candidate — see §Beat 4c candor note)* | `Art/Generated/VFX/NodeReleaseDissolve.prefab` | MISSING |
| `Vfx.CommandNetworkReadout` *(candidate — placement spec in §Beat 3a)* | `Art/Generated/VFX/CommandNetworkReadout.prefab` | MISSING |

**Reuse notes.**

- `Props.StasisCradle` serves all 15 cradle-row instances across Tier1/Tier2/Tier3 (counts 2/5/8) — one prefab, per-instance transform, matching every other chapter's repeated-prop convention. **This covers only the near-field, individually-placed cradles.** Per §3's sheer-quantity candor note, the SETTING's "ten thousand" and Echo's "hundreds… a lot more" require a *second*, separate commission: a high-density, GPU-instanced/MPB-batched backdrop rack — multiple rows wall-lining the vault throat, receding into the exponential fog — sized to read as hundreds-to-thousands rather than fifteen. At the 90 FPS floor this is an instancing/impostor problem, not a prop-count problem: reuse `RendererTint`'s MaterialPropertyBlock batching pattern rather than placing thousands of discrete `BuildProp` cubes. No amount of face-mesh work on the near-field `StasisCradle`/`StasisCradle_RoninFace` pair below fixes the reveal if the backdrop rack never lands.
- `Named.RoninEditionClone` serves both Edition phases — one prefab, two placements at the identical transform (§Beat 3c).
- `Vfx.EditionBladeShadowDissolve` is a natural commission for the Edition's on-death blade-shadow-into-Echo beat (`ch12_beat3_aftermath`'s "his shadow came loose… it came to me"), currently unbuilt — the death is a plain `Health`-reaching-zero state change with no dedicated dissolve VFX.
- `Vfx.CommandNetworkReadout` mounts on `Throne_WallN` (§2/Appendix A.3, throneCenter + (0,1.8,11) = (0,−4.2,85)) — the wall behind the −Z-facing Edition — not a freestanding prop; see §Beat 3a for the full placement spec. Its dissolve should share the trigger event with `Vfx.NodeReleaseDissolve` at Beat 4 step 17, so one node-cut moment collapses both the readout and the node hologram, matching canon's "the readout that cascaded Vale's case collapses to nothing."
- `BuildHologram` (the last node, §Beat 4c) and `AbilityGranter`/`MirrorPhantom`/`HiveCascadeController`/`HeatVent`/`CryoChillController` are shared runtime components, not registry-backed art — they have no prefab key and are not candidates for this table.

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,Doors,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter12Builder.cs`, `ChapterSharedBuilders.cs`, `Chapter12Lines.cs`, `Scripts/Player/ChapterOutro.cs`, `Scripts/Enemies/MirrorPhantom.cs`, `Scripts/Enemies/HiveCascadeController.cs`, `Scripts/World/HeatVent.cs`, `Scripts/World/CryoChillController.cs`, `story ouput/Ch12_The_Fracture.md`, `story ouput/Ch12_The_Fracture_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`.*

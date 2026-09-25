# Chapter 2 — Scene Construction

*The architectural contract for `Ch02_Auction.unity`: what Chapter 2 must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 2 ("The Auction") from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **not a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter2Builder.cs`, entry point `XRRigBuilder.BuildChapter2Auction()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 02 — The Auction**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch02_The_Auction.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`, `audit/Ch02_audit.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The Auction Floor brawl and the Records Vault mini-boss fight are the only combat in this chapter, and both carry their impact feedback through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never through moving the camera.
- **Traversal in Ch2 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no parkour/climb/wall-run**, and — unlike Chapter 1 — **no `NpcWalker` scripted-travel legs at all**. Every named NPC this chapter is a stationary spawn (§5); the only things that move under script control are the `Enemy` AI combatants. Do not introduce any of the excluded mechanics when patching this scene.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | room shells, stalls, cages, racks, terminals, the overseer's box *(the physical object)*, VFX, backdrops, decorative lights | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | door lock state, NPC spawns, enemy spawns (including the inactive escape pursuers and the inactive Mira reveal), reach points, dialogue players, prompts, mission-spine steps | `[BEAT_N_LOGIC]` |

`N` runs **0 through 5**, matching the six `"Beat0:"`–`"Beat5:"` label prefixes already authored into `MissionDirector.steps` by the current code (§4) — this chapter should keep that numbering rather than re-adopting Chapter 1's 1-indexed convention, since the labels are already canon in the shipped mission spine.

**The two objects that span both are doors.** `BuildBeatNArt()` instantiates a door and returns its handle; `BuildBeatNLogic()` sets `startLocked` and wires the `Trigger` step that unlocks it. Art builds the thing; logic decides what it does. **This chapter only has two actual door objects** (`AuctionToCellsDoor`, `VaultToDockDoor`) — the other four room-to-room boundaries are unguarded open gaps with no door GameObject at all (§2). Do not add door art to those four boundaries without an explicit design call; they are load-bearing as *always-open* today.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildDoorwayWall`, `BuildSlidingDoor`, `BuildProp`, `BuildRoomDetails`, `PlaceDecorativeCrowd`, `BuildAccentPointLight`, `BuildEnemy`, `Author*Step` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, including `BuildEnemy`/`EnsureEnemyDefinition` (defined in `XRRigBuilder.cs` itself, not `ChapterSharedBuilders.cs`, but shared the same way).
- **Free to restructure:** the Ch2-local helpers, called only from `BuildChapter2Auction()` and all prefixed `Ch2` to avoid colliding with other partial-class files — `Ch2EnsureBodyguardDefinition`, `Ch2BuildDialogue`, `Ch2WireVoiceClips`, `Ch2BuildPrompt`, `Ch2BuildCompleteCanvas`, `Ch2BuildMarketStalls`, `Ch2BuildBrokerStall`, `Ch2BuildAuctionSet`, `Ch2BuildCells`, `Ch2BuildVault`, `Ch2BuildDock`.

**As of this writing there is no beat-method split at all** — `BuildChapter2Auction()` is a single ~350-line method that builds all seven rooms, all NPCs, all enemies, and the full 20-step mission spine in one linear pass, calling out to the small `Ch2Build*` dressing helpers above only for per-room prop clusters. This refactor lives entirely in decomposing that one method into the twelve `BuildBeatNArt`/`BuildBeatNLogic` pairs above. If you find yourself editing `ChapterSharedBuilders.cs` to do it, stop — you have left Chapter 2 and are now silently rebuilding thirteen other chapters.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two ScriptableObjects carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch2Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, per-room floor + ceiling tint, ten per-room accent lights, the two `ConsoleFlicker`/`AmbientPulse` behaviours |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document — **the same single registry asset Chapter 1 uses**, not a per-chapter copy; Appendix B lists only the keys this chapter consumes |

`Ch2Environment.asset` is net-new. `ArtAssetRegistry.asset` may already exist from the Chapter 1 pass — if so, this chapter only *adds* keys to it, it does not fork a second registry.

Prefab **paths never appear in builder code.** The builder asks the registry for `Rooms.MarketRowShell`; the registry asset holds the path. This is the whole point of the indirection — art can re-point a prefab without touching a `.cs` file or this document.

**Prefab root is `Assets/Ronin7/Art/Generated/`**, matching where the Tripo image→3D character prefabs already live — and, for this chapter, four of those character prefabs (`Resh`, `Iris`, `Mira`, `Velorum-Broker`) **already exist on disk**, a meaningfully better starting position than Chapter 1 had. Environment folders remain the commission:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (incl. this chapter's four Named characters)
  Rooms/                                    ← new
  Props/                                    ← new
  Doors/                                    ← new
  VFX/                                      ← new
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter2Auction()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter2Builder.cs:51`). That call does not *delete objects from* the scene — it **discards the entire scene** and opens a fresh empty one. There is nothing left to search for. A naïve `GameObject.Find("[STATIC_ART_DO_NOT_DELETE]")` after `NewScene` will always return `null`, and the instruction will silently do nothing.
>
> Making the safe zone real requires **replacing the wipe strategy**, one of:
>
> 1. `EditorSceneManager.OpenScene(Ch2ScenePath)`, then `DestroyImmediate` each **generated root by name** (`Undermarket`, `Game`, `Mission`, the rig, the ten accent lights, dialogue players, reach points), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred, and this chapter already proves it works.** `EnemyArtWirer.cs`, `CrowdArtWirer.cs`, and `ChapterRoomDetailsVarietyWirer.cs` **already open `Ch02_Auction.unity` in place today**, idempotently swap the enforcer/bodyguard visual (`Coil_Syndicate_Ganger`), drop decorative Diversity-folder crowd (`Vesh`, `Voll`) into Market Row, and vary the procedural room-detail scatter — then `SaveScene`, all without touching mission logic. That is exactly the surgical open-mutate-save pattern the safe zone needs; it is not hypothetical for this chapter, it is running. The catch is that **none of those three wirers' work survives a chapter rebuild** — `BuildChapter2Auction()`'s `NewScene` wipe discards it, and each wirer's own doc comment says as much ("re-run this after any (never-recommended) chapter rebuild to restore the art"). Converting the wipe strategy is what would let their output live under `[STATIC_ART_DO_NOT_DELETE]` and survive for good.

Everything `BuildBeatNArt()` instantiates goes under this root. Everything `BuildBeatNLogic()` authors goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero *environment* prefabs exist** for this chapter — no room shell, stall, cage, rack, terminal, or overseer's box. Four *character* prefabs and one *enemy* visual already resolve (Appendix B): a materially better starting position than Chapter 1's zero, but the environment art gap is identical in kind.

A builder that instantiates from an empty registry produces **an empty room** — the first run of the refactored builder would destroy Chapter 2.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Rooms_MarketRowShell);
if (prefab == null) {
    Debug.LogWarning($"[Ch2] {ArtKey.Rooms_MarketRowShell} unresolved — primitive fallback.");
    BuildMarketRowPrimitive(staticArtRoot);   // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This is not a new pattern to invent — the exact mechanism already ships as `Ronin7.Editor.Art.ArtPrefabRegistry.TryInstantiateOrFallback(prefabPath, greyboxFactory, parent)`, and `Chapter2Builder.cs`'s own `BuildEnemy` call chain uses it today for the enemy body prefab (`EnemyFootPrefabPath` → capsule greybox on miss, logging `"[ArtPrefabRegistry] greybox fallback: {prefabPath}"`). The environment-art registry this document specifies is the same idiom generalized from one prefab slot to the whole `Rooms`/`Props`/`Doors`/`Vfx` surface. The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No Ch2-specific perf baseline has been recorded.** `Project/Docs/CHAPTER-BUILD-LEDGER.md` carries only Chapter 1's number (`drawCalls 189 · setPassCalls 17 · tris 9,198 · verts 13,092`, 2026-07-02) under its one "Perf reference bar" heading — there is no equivalent line for `Ch02_Auction`. **Capturing that baseline via edit-mode `UnityStats` on a fresh build is a prerequisite for this refactor**, not an afterthought: Chapter 2 has seven rooms to Chapter 1's four, four stationary Named NPCs, up to five simultaneously active `Enemy` AIs at the Auction Floor peak, plus two more at the escape run, and a full decorative crowd/room-detail-variety pass on top — it should be assumed heavier than Chapter 1's bar until measured otherwise.
- Replacing the ~40+ tinted primitives across seven rooms with high-fidelity prefabs is *exactly* the change that breaks whatever bar gets set. Every prefab landing in the registry must be re-measured against it. A prefab that looks correct and drops the scene below 72 Hz is a regression, not an upgrade — the primitives it replaces are cheap for a reason (shared `TintShared` MaterialPropertyBlock batching, see §3).

## 2. Chapter spatial map

Chapter 2 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch02_Auction.unity`, laid out as **seven rooms strung along a single linear +Z run** — no branching, no vertical stacking, no returning to an earlier room by any route other than walking back down the same corridor. The player spawns at the world origin (z≈0, inside Docking Alley) and the story pushes them monotonically toward z≈92, where the back wall of the Escape/Dock room is solid — the chapter's terminus, "the ship, lifting off."

```
 -Z (spawn)
 Docking Alley ──[open gap, z=4]── Market Row ──[open gap, z=20]── Broker Front ──[open gap, z=30]── Auction Floor
  x[-4,4]  z[-4,4]                  x[-7,7]  z[4,20]                x[-5,5]  z[20,30]                 x[-9,9]  z[30,48]
  center (0,0,0), 8×8               center (0,0,12), 14×16          center (0,0,25), 10×10             center (0,0,39), 18×18

 Auction Floor ──[AuctionToCellsDoor z=48, locked]── Holding Cells ──[open gap, z=62]── Records Vault ──[VaultToDockDoor z=74, locked]── Escape/Dock
  x[-9,9]  z[30,48]                                   x[-6,6]  z[48,62]              x[-5,5]  z[62,74]                                  x[-6,6]  z[74,92]   +Z (lift-off, solid back wall)
  center (0,0,39), 18×18                              center (0,0,55), 12×14         center (0,0,68), 10×12                             center (0,0,83), 12×18
```

| Beat | Room | Footprint | Floor center / size |
|---|---|---|---|
| 0 | Docking Alley | x[-4,4], z[-4,4] | center (0,0,0), 8×8 |
| 1 | Market Row | x[-7,7], z[4,20] | center (0,0,12), 14×16 |
| 1 | Broker Front | x[-5,5], z[20,30] | center (0,0,25), 10×10 |
| 2 | Auction Floor | x[-9,9], z[30,48] | center (0,0,39), 18×18 |
| 3 | Holding Cells | x[-6,6], z[48,62] | center (0,0,55), 12×14 |
| 4 | Records Vault | x[-5,5], z[62,74] | center (0,0,68), 10×12 |
| 5 | Escape / Dock | x[-6,6], z[74,92] | center (0,0,83), 12×18 |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m**, the same value every other chapter uses. Every doorway gap, real or open, is **2.4 m** wide and centered on x=0.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience.

**Doors** — of the six room-to-room boundaries, only **two** carry an actual door GameObject. The other four are bare wall gaps (`BuildDoorwayWall`) with nothing in them — always open, no lock state, no art:

| Boundary | Position | Door object? | Registry Key | startLocked | Unlocked by |
|---|---|---|---|---|---|
| Alley → Market (z=4) | (0, 0, 4) | **none** — open gap | — | n/a | always open |
| Market → Broker Front (z=20) | (0, 0, 20) | **none** — open gap | — | n/a | always open |
| Broker Front → Auction (z=30) | (0, 0, 30) | **none** — open gap | — | n/a | always open |
| Auction → Cells (z=48) | (0, 0, 48) | `AuctionToCellsDoor` | `Doors.SlidingDoor_Standard` | **true** | Mission step 9 Trigger ("Trigger: Unlock Cells Route (Resh's back-channel)"), fired the instant the `DefeatEnemies` step (step 8) resolves |
| Cells → Vault (z=62) | (0, 0, 62) | **none** — open gap | — | n/a | always open |
| Vault → Dock (z=74) | (0, 0, 74) | `VaultToDockDoor` | `Doors.SlidingDoor_Standard` | **true** | Mission step 15 Trigger ("Trigger: Escape Run"), which simultaneously reveals Mira and activates both pursuers |

Both real doors are wired via `WireDoorAudio(door, DoorSlide.wav)`, the same clip and helper Chapter 1 uses. **Do not add door art or lock logic to the four open gaps without a deliberate design decision** — as built, they are the chapter's only free-walk stretches, and adding friction there is a scope change, not a bug fix.

**`AuctionToCellsDoor` carries no visual distinction for the fiction it's asked to carry.** Canon's Cells route is Resh's signature — smuggling routes "nobody's mapped, the ones that go under the cells" (line 247), reached via "a maintenance crawl beneath the market — one of Resh's back-channels" (line 401) — and step 9's own mission-step label names it directly ("Unlock Cells Route (Resh's back-channel)"). But the door object itself is the *same* `Doors.SlidingDoor_Standard` prefab as the public `VaultToDockDoor`, sitting in the Auction Floor's back wall on the same straight +Z run as every other room boundary — nothing in the geometry reads as concealed; the player walks a clean public corridor, never a hatch. `AuctionToCellsDoor` is a candidate for a distinct concealed-hatch/maintenance-grate art key (e.g. `Doors.BackChannelHatch`) rather than a second instance of `Doors.SlidingDoor_Standard`, so Resh's one defining trait — the thing that makes him structurally load-bearing to this chapter — reads in the room, not only in the step label. A hatch built this way — angling visibly downward, with a few descending steps into the maintenance crawl beyond it — would also be this chapter's one concrete, Y-invariant-safe way to make Velorum's tier-on-tier descent (§3) read in geometry rather than only in lighting and crowd density, without touching the flat y=0 floor the other six rooms share. See Appendix B.

**How a real door opens.** Both are `ProximityDoor` (`Scripts/World/ProximityDoor.cs`): "locked" means the controller `GameObject` is built inactive, and a `Trigger` mission step merely `SetActive(true)`s it — that arms the proximity check, it does not open the door outright. Once active, the two panels slide apart only when `Camera.main`'s *horizontal* distance to the door's transform drops to ≤ `triggerRadius` (default **3 m**, not overridden by either door in this chapter), and slide shut again the instant the player steps back outside that radius. Unlocking and opening are therefore two different moments — a door that has just been unlocked at range stays visibly, physically shut until the player walks within 3 m of it. See §4 Beat 2c and Beat 5b for the two places in the chapter where this timing is load-bearing.

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (ambient shadow-AI callouts, additive, no extra wiring, same as Chapter 1). No explicit spawn position is set — the rig instantiates at the world origin, which is the center of Docking Alley. `ZoneBounds` is set to **center (0, 0, 44), radius 55** — a single bounding sphere loosely enclosing all seven rooms along the corridor's midpoint (looser than Chapter 1's radius-45 sphere, because this chapter's corridor is more than twice as long).

## 3. Global environment & backdrop

Chapter 1 was one dead ship in empty black; Chapter 2 is the opposite. Per the dialogue script's SETTING block, Velorum "never stops moving," and "the only quiet is the quiet you buy." The crowd is meant to read as a fifth character — a sea of buyers and sold, indifferent, churning, loud enough to drown a scream and used to drowning them. Where the rig hid one body by accident, Velorum hides thousands on purpose and charges admission.

**Velorum, tier by tier:** the undermarket is "a market built down into itself: tier on tier of stalls, gantries, and cages bored into an old mining moon, lit by signage and slaughter-bright auction lamps." Up top it sells weapons and wonders; the deeper the player goes, the more it sells people and pretends it doesn't. The Dominion doesn't police Velorum — it feeds it, and the disposal trade that jettisons executed operatives' bodies *and* their termination manifests is what eventually washes Ronin-7's own file up here (the same trade that delivered his casket to Kessler in Ch1). The seven-room run is built to dramatize that descent in hardware: the Alley and Market Row read as an open, noisy, crowded upper tier (magenta/cyan neon, decorative crowd, stall canopies); Broker Front is a tighter, guarded transaction space; the Auction Floor is the single biggest room in the chapter and the most watched (an overseer's glass box looks down on it, per the SETTING block's description of Velorum's own "calm thing that owns the loud room"); Holding Cells and the Records Vault are the market's undertow — dark, wet, cold, the place the noise above is built to cover; and the Escape/Dock closes the loop back to something that is, for the first time this chapter, "theirs." **That dramatization is confined to lighting, crowd density, and floor tint — the geometry itself never descends.** All seven rooms sit on one flat y=0 +Z line (§2), with no room-to-room vertical offset marking the deeper tiers canon insists on ("tier on tier," line 19; "the deeper you go," line 22). The one boundary built to carry that fiction most directly is the Cells route — canon's smuggling path that literally "go[es] under the cells" (line 247) — and §2 already names the Y-invariant-safe lever for it: a `Doors.BackChannelHatch` that angles visibly downward, rather than a second `Doors.SlidingDoor_Standard` on the flat run. **The Dock room is staged as the ship's hold, not a threshold to it** — canon's Beat 5 SETTING (line 663) is explicit: "Interior: Kessler's ship, the hold... The crew comes up the ramp into the ship's quiet — crates, tie-downs, the same fold of tarp in the corner." The crew has already boarded by the time this room's dialogue plays; the room *is* the ship's interior, dressed as such (§4 Beat 5c).

**Temperature is implied, not simulated.** The dialogue script's SETTING block reads the Auction Floor as physically hot — "Heat, noise, the smell (implied) of bodies and ozone and frying oil" — against the Cells/Vault's "wet, low... cold." Fog and ambient light are a single uniform profile value chapter-wide (§3.1); the only build-time levers that already carry this hot/cold contrast are per-room floor tint and accent-light color/intensity — the Auction Floor's hard white `AuctionLight0` (the SETTING block's own "slaughter-bright auction lamps," i3/r22) and warm-toned accent tint (0.5, 0.45, 0.15) against the Cells' and Vault's darker, bluer floor tints (Appendix A.1) and dim teal/cold-cyan accents. No temperature system exists or is proposed here; this is a naming of which values already do that sensory work.

**The one location that isn't Velorum** is Beat 0's briefing — narratively aboard Kessler's salvage ship from Chapter 1, ***The Cairn*** (the story bible names the ship; in dialogue he calls it "the rig"), hours after the boarding. **As built, no distinct rig-interior geometry exists for this.** The Beat 0 dialogue plays with the player standing inside what is, spatially, the Docking Alley — the same room, same `AlleyLight` cool-blue accent, same neutral gray room-detail tint, that a moment later reads as Velorum's entry tier. See §9 for the flagged consequence of this collapse.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter2Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch2Environment.asset`. Its schema mirrors Chapter 1's:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` (per room — this chapter varies floor tint per room, unlike Ch1's single global pair) | `Color` | every `BuildFloorCeiling` call |
| `accentLights[]` | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` × 10 |
| `eventLights[]` | `{ name, position, color, intensity, range, startsInactive }` | *(unused this chapter — Ch2 has no event light comparable to Ch1's `DockingAlarmLight`; every accent light in this chapter is always-on from build)* |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddConsoleFlicker("BrokerLight", seed: 22f)` and `AddAmbientPulse("VaultLight", periodSeconds: 6.5f)` calls with data. These are the **only two** behaviour-bearing lights this chapter; the other eight accent lights are static.

The ten per-room accent entries are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass.

**Material / tint palette:** set-dressing props today are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials — the same reasoning and the same perf cost applies as Chapter 1 (§1.6). **Prefabs replacing them must carry their own materials and will not batch this way.**

## 4. Per-beat scene spec

The chapter plays as six beats along the linear +Z run, spanning seven rooms (Market Row and Broker Front both belong to Beat 1; the Auction Floor's `DefeatEnemies` combat has no `"Beat…:"` label of its own but is folded into Beat 2, which is where the mission spine physically continues after Resh is recruited). Each beat is documented with the same a–f structure.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and the Y-invariant (every prop in this chapter sits on a single shared flat floor, y=0, across all seven rooms — there is no per-room floor offset to track).
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the prefab is already on disk.

**Mission-spine step index — all 20 steps, in build order.** `MissionDirector.steps` is authored as one linear pass (`Chapter2Builder.cs:329–349`); the per-beat "Mission-spine steps" tables below re-slice this same sequence with fuller "what happens" detail, split across the six beats. This table is the single place the full 0→19 order is assembled in one read:

| Step | Type | Label | Beat |
|---|---|---|---|
| 0 | Dialogue | Beat0: Briefing (aboard the rig) | 0 |
| 1 | ReachTrigger | ReachTrigger: Market Row | 1 |
| 2 | Dialogue | Beat1: Undermarket (descent, naming Resh) | 1 |
| 3 | Dialogue | Beat2: Resh (confrontation) | 2 |
| 4 | Dialogue | Beat2: Resh (spared, recruited) | 2 |
| 5 | ReachTrigger | ReachTrigger: Broker Front | 1 |
| 6 | Dialogue | Beat1: Broker (the refusal) | 1 |
| 7 | ReachTrigger | ReachTrigger: Auction Floor | 2 |
| 8 | DefeatEnemies | DefeatEnemies: Syndicate Enforcers + Bodyguard | 2 |
| 9 | Trigger | Trigger: Unlock Cells Route (Resh's back-channel) | 2 |
| 10 | ReachTrigger | ReachTrigger: Holding Cells | 3 |
| 11 | Prompt | Prompt: Cut Iris Free | 3 |
| 12 | Dialogue | Beat3: Iris Freed (Kessler reunion) | 3 |
| 13 | ReachTrigger | ReachTrigger: Records Vault | 4 |
| 14 | Dialogue | Beat4: The Failed Execution (killswitch reveal) | 4 |
| 15 | Trigger | Trigger: Escape Run (unlock dock route, Mira revealed, pursuers) | 5 |
| 16 | ReachTrigger | ReachTrigger: Dock | 5 |
| 17 | Dialogue | Beat5: Reunion (Kessler + Iris, Mira found) | 5 |
| 18 | Dialogue | Beat5: Mira Kept | 5 |
| 19 | Trigger | Trigger: Chapter Outro (flag + fade + canvas) | 5 |

Note steps 3–6 interleave Beat 2 (Resh) between Beat 1's two reach/dialogue pairs — the physical room order (Market → Broker → Auction) and the step-index order diverge here, exactly the mismatch §9 flags under "Room order vs. dialogue order."

---

### Beat 0 — Aboard the Rig (The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Your objective for this beat is to separate the environment generation from the mission logic. Do not generate primitive cubes, cylinders, or hardcoded sizes for any props. You must read from the centralized `ArtAssetRegistry` ScriptableObject for all environment prefabs. Create separate methods: **`BuildBeat0Art()`** for static environment/prefabs, and **`BuildBeat0Logic()`** for doors, AI, triggers, and dialogue.

#### a. Narrative purpose & emotional target

Chapter 1 ended on Ronin-7's one unprompted want ("I want to know why I went easy") and Kessler's answer ("Then we ask Velorum"). Beat 0 is the quiet before the noise — aboard Kessler's rig, *The Cairn* — the chapter's *only* quiet, per the dialogue script's own framing — where that answer becomes a heading. The scene sells a partnership hours old finding its footing: Kessler needling Ronin-7 about trust ("You slept four hours and stood watch over my shoulder for the other six"), and Ronin-7 answering in the flat arithmetic of a clock ("The voice on the comm gave three days. I don't have six hours to spend not moving"). The emotional beat that matters is Kessler naming *why* he's willing to set foot on a rock he swore he'd never return to: "somebody has to find out who they buried in my casket. Might as well be the two of us." That's the whole partnership in one line — not debt, not obligation, just two men with nowhere else to start. Target feel: dry, weary, close-quarters trust, immediately before Velorum swallows it whole.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components parent to a `[BEAT_0_LOGIC]` root object.

- **Player rig:** spawns at the world origin (0, 0, 0) — no explicit position override in the builder, so this is simply `BuildRig`'s default placement, which happens to land inside the Docking Alley footprint. `BuildRig(addLocomotion: true)` + `EchoPresence` + `ZoneBounds` center (0,0,44) radius 55 (authored once, chapter-wide). Locomotion is continuous walk + snap-turn. The player does not travel during Beat 0 — the dialogue plays immediately at spawn, before any `ReachTrigger` gate.
- **No NPC is physically present.** Kessler has no `InstantiateNpc` call anywhere in `Chapter2Builder.cs` — every one of his Beat 0 lines is voiced dialogue only, with no body in the room. This is consistent with the story's framing (Kessler "stays with the ship" through the Velorum legs) but means Beat 0's "two men talking in a command room" staging has no second character standing next to the player; it plays as a solo dialogue-listening beat.
- **No door gates Beat 0.** The Alley→Market boundary (z=4) is an open gap from build; there is nothing to unlock.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `Dialogue_Beat0_Briefing` (`ch2_beat0_briefing`) plays in full, player-paced, before any reach gate — the chapter's opening beat |

**What changes during the beat:** nothing in the set dressing — no props, doors, or lights change state. The beat is pure dialogue over static geometry, and its only consequence is unblocking step 1's `ReachTrigger`, which the player is free to walk into the instant the dialogue ends (advancing dialogue does not itself gate movement).

#### c. Art & Environment Instantiation → `BuildBeat0Art()`

All environment prefabs instantiate from the `ArtAssetRegistry` and parent to `[STATIC_ART_DO_NOT_DELETE]`. Do not use `scale()` or `size()`; rely on the prefab's native scale.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (8×8, walls + floor + ceiling) | center (0,0,0) | `Rooms.DockingAlleyShell` | `…/Art/Generated/Rooms/DockingAlleyShell.prefab` | **MISSING** |
| Katana "Echo" (belt-fit) | (2.2, 0.9, -2.5), Euler(0,90,0) | `Named.Echo` | `…/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** *(not registry-resolved today — see note below)* |
| `WreckfieldViewport` (backdrop) | ~(0, 1.8, -3.9), on `Alley_WallFront` (Appendix A.2) | `VFX.WreckfieldViewport` | `…/Art/Generated/VFX/WreckfieldViewport.prefab` | **MISSING** — no rig-interior geometry exists to host it; see note below and §3/§9 |
| `AlleyLight` | (0, 2.6, 0) | — | read from `ChapterEnvironmentProfile.accentLights["Alley"]` | profile |

**Notes on the transition.** The room's procedural crate/console/pipe scatter (`BuildRoomDetails("Alley", …)`) and its `ChapterRoomDetailsVarietyWirer`-applied variant props are deliberately **not** listed as registry rows here — they are shared, deterministic, chapter-agnostic dressing generated by frozen `ChapterSharedBuilders.cs` code (see §1.2), not per-beat authored props, and are out of scope for this refactor. The katana is the room's only bespoke placement — but unlike the `Diversity.VelorumCrowd` and `Enemies.SyndicateEnforcer` rows above (§4 Beat 1/2), which are honestly marked "applied by `EnemyArtWirer`/`CrowdArtWirer`, not the builder itself," this row currently overstates its own resolution: the builder calls `BuildSword(pos, rot, weapon, Ch1EchoBladePrefab)` (`Chapter2Builder.cs:177`), a hardcoded Chapter-1-scoped path constant (`Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab`), not `registry.Resolve(ArtKey.Named_Echo)`. It happens to point at the *same* file the `Named.Echo` registry key names, so today's visual result is identical either way, but the katana does not yet flow through the registry the way this document's fallback rule (§1.5) describes for every other prop — migrating it from the `Ch1EchoBladePrefab` constant to a genuine `Named.Echo` registry lookup is part of this refactor's scope, not already done.

**`VFX.WreckfieldViewport` is the target key for Beat 0's one sensory callback to Chapter 1, and it is entered here for the record, not as a build instruction.** Canon's Beat 0 SETTING (dialogue line 108) frames the briefing inside "a single battered console lit inside a cold, half-powered hull" where "the wreck-field still turns slow in the viewport" — the chapter's only quiet and its one visual link back to Ch1's dead ship. As §3 and §9 already flag, no distinct rig-interior geometry exists today; the dialogue plays with the player standing inside the ordinary Docking Alley. The row above gives that unbuilt backdrop a real registry key and Appendix B entry instead of only the §9 prose mention, consistent with how every other named-but-unbuilt canon fixture in this chapter (`Props.WeaponRack`, `Props.StorageTarp`, `Props.AuctionCallerPulpit`) is tracked. Its **MISSING** status and wall-anchored position are provisional on the rig-interior split ever being built — see §9 for why that split is flagged, not proposed, here.

#### d. Combat

None. Beat 0 is dialogue-only.

#### e. Dialogue / VO

One dialogue set, anchored at (0, 1, 1), advanced by **Left-Hand "Talk" (Y)** via the shared `talkRef` input action:

- **`ch2_beat0_briefing`** (`Dialogue_Beat0_Briefing`) — 11 lines. Kessler pushes Ronin-7 to trust the hull; Ronin-7 counters with the three-day clock; Kessler lays out Velorum in one breath ("They sell weapons up top, people down the bottom, and file the paperwork on both"); Ronin-7 reads the distaste off him ("You know it."); Kessler's confession that he's "plotting a course back for a man I scraped out of a coffin"; Ronin-7's single-word "Why."; Kessler's answer naming the shared motive; closing on "Then set the course." / "Already set. Strap in."

#### f. Audio / Haptics / VR Comfort

- **No camera shake.** No combat, no haptics scripted for this beat — the first of two purely-dialogue stretches in the chapter (the other is Beat 4's reveal).
- **No ambient bed plays in this room.** Unlike Chapter 1's `MedbayAmbience`/`OnFootAmbience` pair, `Chapter2Builder.cs` wires exactly one 3D ambience layer chapter-wide (`DockAmbience`, §4 Beat 5) — the Docking Alley, Market Row, Broker Front, Auction Floor, Holding Cells, and Records Vault all currently play in silence apart from dialogue and SFX one-shots. Flagged as a gap in §9, not fixed here.
- `AlleyLight`'s `behaviour` is `None` in the profile — static, cool blue, no pulse or flicker.
- Comfort vignette is inert (player is stationary through the whole beat); it engages the instant the player walks toward the Market Row reach point after the dialogue ends.

---

### Beat 1 — The Undermarket (Descent + the Broker's Refusal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (Market Row stalls/canopies/crowd, Broker Front counter/glass) and **`BuildBeat1Logic()`** (the two reach points, the Undermarket and Broker dialogue sets). **Resh's own confrontation/recruit dialogue is Beat 2's — do not fold it into this beat even though Resh physically stands in the same room this beat dresses.**

#### a. Narrative purpose & emotional target

Beat 1 is the market teaching the player (and Ronin-7) how small it's built to make a newcomer feel. Kessler's blocking is protective and practiced — "Keep your hands where the crowd can't read them. Down here a drawn blade is a price tag" — while Ronin-7 reads the crowd the only way he knows how, as a tactical problem: "There are no sightlines. No exits I'd trust." The lead crystallizes into a name (the broker), and the broker's refusal ("Money's not what gets you in. That vault wants trust, and I've got none to spend on you") is the beat's hinge — it's what sends the crew looking for Resh in the first place. The broker's fear is meant to read as *earned*: he's not a puzzle to force, he's "a wall," per Kessler's own read, and the smart move is explicitly framed as going around him rather than through him.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components parent to `[BEAT_1_LOGIC]`.

- **The broker:** spawns at (-3.5, 0, 25.6), facing −Z toward the glass and the grille (target rotation Euler(0,180,0) — see the facing gap noted under §c below; as built, `InstantiateNpc` leaves him at identity +Z, back to the player), just inside the Broker Front counter. `InstantiateNpc(Ch2BrokerPrefab, …)`, `FitNamedCharacter`, `StoryNpc` (displayName "Broker"). **No `StoryNpcWander`** — he is fully static, consistent with "a paranoid shape behind smeared glass" who never leaves his counter.
- **Player:** must physically walk from Docking Alley through the open Alley→Market gap (z=4) into `MarketReachPoint` (0, 1, 12) radius **5** to satisfy step 1, then continue through the open Market→Broker gap (z=20) into `BrokerReachPoint` (0, 1, 25) radius **4.5** to satisfy step 5 — no teleport, no scripted path. Both are `ReachTrigger`s, not blocking; nothing stops the player from wandering ahead into the Auction Floor before dialogue resolves, other than mission-spine dialogue not having played yet.
- **No door gates either boundary in this beat** (§2).

**Mission-spine steps (this beat's, interleaved with Beat 2's — see the consolidated 20-step index at the top of §4 for the full linear order):**

| Step | Type | What happens |
|---|---|---|
| 1 | ReachTrigger: Market Row | Gates on `Camera.main` distance to `MarketReachPoint` (0,1,12), radius 5 |
| 2 | Dialogue: Beat1 Undermarket | Plays `ch2_beat1_undermarket` in full — the descent + naming Resh |
| 5 | ReachTrigger: Broker Front | Gates on distance to `BrokerReachPoint` (0,1,25), radius 4.5 |
| 6 | Dialogue: Beat1 Broker | Plays `ch2_beat1_broker` — the refusal, delivered in person |

*(Steps 3–4, Resh's confrontation/recruit dialogue, sit chronologically between this beat's two reach/dialogue pairs but are documented under Beat 2, below, matching their own `"Beat2:"` mission-step labels.)*

**What changes during the beat:** nothing in the set dressing — no props, doors, or lights change state; this is another pure-traversal-and-dialogue stretch.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (14×16) | center (0,0,12) | `Rooms.MarketRowShell` | `…/Art/Generated/Rooms/MarketRowShell.prefab` | **MISSING** |
| `Stall_0` (counter + canopy) | (-5.5, 0, 8) | `Props.MarketStall` | `…/Art/Generated/Props/MarketStall.prefab` | **MISSING** |
| `Stall_1` | (5.5, 0, 10) | `Props.MarketStall` | `…/Art/Generated/Props/MarketStall.prefab` | **MISSING** |
| `Stall_2` | (-5.5, 0, 16) | `Props.MarketStall` | `…/Art/Generated/Props/MarketStall.prefab` | **MISSING** |
| `Stall_3` | (5.5, 0, 18) | `Props.MarketStall` | `…/Art/Generated/Props/MarketStall.prefab` | **MISSING** |
| `MarketSignage` (hanging, overhead) | ~(0, 3.4, 12), spanning the lane between the stalls | `VFX.MarketSignage` | `…/Art/Generated/VFX/MarketSignage.prefab` | **MISSING** — no sign/banner geometry exists today; see note below |
| Decorative crowd ×6 | (-4,0,7), (4,0,8), (-5,0,12), (5,0,13), (-3,0,18), (3,0,17) | `Diversity.VelorumCrowd` (Vesh / Voll variants) | `…/Art/Generated/Characters3D/Diversity/{Vesh,Voll}/…` | **EXISTS** *(applied additively by `CrowdArtWirer`, not by the builder itself)* |
| `MarketLight0` | (-3, 2.8, 10) | — | `ChapterEnvironmentProfile.accentLights["Market0"]` | profile |
| `MarketLight1` | (3, 2.8, 16) | — | `ChapterEnvironmentProfile.accentLights["Market1"]` | profile |
| Room shell (10×10) | center (0,0,25) | `Rooms.BrokerFrontShell` | `…/Art/Generated/Rooms/BrokerFrontShell.prefab` | **MISSING** |
| `Broker_Counter` | (-3.5, 0.5, 25) | `Props.BrokerCounter` | `…/Art/Generated/Props/BrokerCounter.prefab` | **MISSING** |
| `Broker_Glass` | (-3.5, 1.4, 25) | `Props.BrokerGlass` | `…/Art/Generated/Props/BrokerGlass.prefab` | **MISSING** |
| Broker (NPC) | (-3.5, 0, 25.6), Euler(0,180,0) — facing −Z, toward `Broker_Glass`/the grille and the player | `Named.VelorumBroker` | `…/Art/Generated/Characters3D/Named/Velorum-Broker.prefab` | **EXISTS** |
| `BrokerLight` | (0, 2.6, 25) | — | `ChapterEnvironmentProfile.accentLights["Broker"]` — `behaviour: ConsoleFlicker(seed: 22)` | profile |

**Notes on the transition.** `Stall_Counter`/`Stall_Canopy` collapse into a single `MarketStall` prefab per instance (four placements, same key, matching how Chapter 1's `Doors.SlidingDoor_Standard` serves all three of its doors). The `Diversity.VelorumCrowd` row is unusual among this document's tables: it is **already resolved today**, but not by the builder — `CrowdArtWirer.WireCrowdArtIntoScenes()` patches it in as a post-build step, reading `("Ch02_Auction", new[] { "Vesh", "Voll" })` from its own scene map. Folding that wirer's placement logic into `BuildBeat1Art()` directly (so a fresh build produces dressed crowd without a second menu action) is a natural follow-up, but is not in this refactor's scope.

**The market's hanging signage — half of Velorum's signature look — has no geometry, only the glow it would cast.** Canon names it twice as a defining visual of this tier: "lit by signage and slaughter-bright auction lamps" (dialogue line 20) and "hanging signage" among the entry tier's defining fixtures, alongside "hawkers, vendor stalls jammed wall to wall" (line 41). `MarketLight0`/`MarketLight1` (magenta/cyan, §6) already supply the *glow* this fixture would cast, but no sign, banner, or holo-placard geometry exists anywhere in the builder. `VFX.MarketSignage` is this document's first populated row in the `VFX` registry category — declared in §1.2/§1.3/§1.5 as part of `BuildBeatNArt`'s remit and the `Art/Generated/VFX/` folder, but until now carried no per-beat inventory of its own.

**Market Row's crowd is also under-served against canon, a smaller sibling of the Auction gallery gap (§4 Beat 2c/§9).** The beat's own SETTING (dialogue lines 41–43) is as emphatic as the Auction Floor's: "choke-narrow lanes… A river of people… Built to make a newcomer small." Six `PlaceDecorativeCrowd` figures scattered across a 14×16 room will not read as "a river," and Ronin-7's tactical read of the room in §4a ("There are no sightlines. No exits I'd trust") only lands if the crowd genuinely presses in. Denser perimeter/lane-choke placement using the already-resolved `Diversity.VelorumCrowd` art is the concrete, in-scope-adjacent fix — not proposed here as a change, only named so the gap is tracked alongside the Auction Floor's.

**NPC facing is a real, chapter-wide gap, and the Broker is its acute case.** `InstantiateNpc(prefabPath, position, name)` (`ChapterSharedBuilders.cs:648`) sets **position only** — it never touches `transform.rotation`, so every spawned NPC sits at its prefab's identity rotation, i.e. facing world +Z. Every named NPC this chapter is approached by the player walking +Z from spawn (§2), so as built, the Broker, Resh, and Iris all face *away* from the player's approach. The Broker is the worst instance of it: he spawns at z=25.6, *behind* `Broker_Counter`/`Broker_Glass` at z=25, and canon stages him as "a paranoid shape behind smeared glass" who "refuses through the grille" (dialogue lines 197–222) — at default +Z he faces the back wall, back turned to both the glass and the player at the grille, directly contradicting the staged blocking. The art tables above now carry the intended facing (Euler Y≈180, facing −Z) on every affected NPC row and in §5's travel table, but **filling in the column is not the fix** — either the character prefabs must bake a −Z-facing forward (the Tripo pipeline's default orientation should be checked chapter-wide, not patched per-instance), or `InstantiateNpc`/the eventual registry-driven spawn call must take and apply a rotation argument. Ch1's own doc already treats this as load-bearing (its Kessler spawn note reads "facing the table"), and this chapter's own `Ch2BuildCompleteCanvas` already rotates the complete-canvas 180° "to face −z, toward the player" (`Chapter2Builder.cs:470`) — so facing-awareness exists in the codebase today, it just wasn't extended to the NPC spawns. This is a genuine Ch1-parity omission, not a cosmetic one.

**Two smaller sensory anchors from the refusal exchange are also unbuilt, minor next to the facing gap above.** A speaker grille "crackles before they've stopped walking" (line 195), and the broker "talks to the grille or you talk to nobody" (line 198) — the refusal is delivered *through the grille*, not the glass itself — and "the screen behind the glass goes dark" (line 222) at the moment he shuts them out. `Ch2BuildBrokerStall` builds only `Broker_Counter` and `Broker_Glass`; there is no grille/speaker prop and no scripted screen-dim event tied to the refusal dialogue. It's the difference between the broker reading as a paranoid shape behind glass (built) and one who actively shuts the crew out (canon's active beat).

#### d. Combat

None. The crowd includes a pickpocket beat in the dialogue script ("A child slips past them in the press, a hand light at Kessler's hip. He catches the wrist without looking.") but it has no corresponding GameObject or trigger in the builder — it is narration carried entirely by the VO track, not a scripted event *(inferred: no engine object backs this image today)*.

#### e. Dialogue / VO

Two dialogue sets, advanced on **Left-Hand "Talk" (Y)**:

- **`ch2_beat1_undermarket`** (`Dialogue_Beat1_Undermarket`, at (0,1,8)) — 11 lines. Kessler's crowd-craft warnings; Ronin-7 reading the market as a kill-box; the broker named as the only lead; Resh named as the way around him — "A smuggler with a conscience," Ronin-7 calls it, turning the idea over "like a strange tool. Not mockery — recognition."
- **`ch2_beat1_broker`** (`Dialogue_Beat1_Broker`, at (-3,1,26)) — 9 lines. The glass-stays-down refusal in person; Kessler's "We can pay" met with "Money's not what gets you in"; Kessler's pivot ("He's a wall. So we go around him.") and Ronin-7's "Around how."; Kessler's closing line — **note:** the shipped line reads *"Resh already gave us the door. His routes run under this stall. Let's move."*, which only makes narrative sense once Resh has already been recruited (Beat 2), confirming this dialogue set fires *after* Beat 2's, not before, despite `BrokerFront` being physically upstream of the Auction Floor in room order. See §9.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics — dialogue and traversal only.
- **The refusal's one physical beat — Kessler catching Ronin-7's forearm — is unstageable twice over and is VO-carried only.** Line 224: "Ronin-7's hand drifts toward the wrap. Kessler's catches his forearm — not stopping him, steering him," ahead of Kessler's "Don't. You cut that glass..." (line 227). Kessler has no physical `GameObject` in this scene (§5) and the player rig has no visible arms (§2, head + two hands only), so the restraint can't be modeled on either end. See §9 for the pattern this shares with Beat 4's throat-scar gesture.
- `MarketLight0`/`MarketLight1` are both `behaviour: None` (static magenta/cyan neon); `BrokerLight` carries `ConsoleFlicker(seed: 22)`, the chapter's first flicker — the paranoid, half-powered read of a man who has wired his own counter to look worse than it is.
- Comfort vignette engages normally as the player walks two full room-lengths (Alley→Market, Market→Broker) during this beat, the longest continuous traversal stretch in the chapter to this point.

---

### Beat 2 — Sparing Resh (Ally #1) + the Auction Floor

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat2Art()`** (the raised stage, lot pedestal, cage bars, overseer's box) and **`BuildBeat2Logic()`** (Resh's spawn/wander, the confront/recruit dialogue, the Auction Floor reach point, the enforcer + Bodyguard spawns, the `DefeatEnemies` gate, and the cells-route unlock Trigger). **This beat spans two rooms that are not adjacent in the mission spine's dialogue order** — Resh is recruited standing in Market Row (built in Beat 1), and the combat that follows happens two rooms downstream on the Auction Floor. Do not move Resh's dialogue anchors into the Auction Floor room; that is not where the builder places them.

#### a. Narrative purpose & emotional target

This is the chapter's ally-unlock beat, and the design intent is explicit in the treatment: the choice that *binds* Resh is mercy, not force — "a man with this kind of power choosing to spare rather than spend — exactly the thing Resh has never once seen from anyone standing above the gutter." Resh's confrontation dialogue is built almost entirely around his own certainty that this ends the way it's always ended for men like him ("Go on, then. You've got me. That's how it always ends with your kind."), and Ronin-7's one-word answer — "Go." — is the whole scene's thesis. What follows is Resh's arithmetic visibly breaking: "Nobody with their boot on my neck takes it off. Not once, not in my whole life down here." His recruitment is not gratitude, it's recognition of an opening — "You're the first crack I've seen in a wall I've spent twenty years smuggling around" — and his stated motive is explicit and audited-clean against the story bible: not a debt, but a shot at the Dominion machine that feeds the markets he's spent his life bailing water against.

The Auction Floor combat that follows in the room order is the chapter's first fight: syndicate enforcers plus a tougher Bodyguard mini-boss, fought in the open pit under "hard white light" with the overseer's glass box watching from above.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components parent to `[BEAT_2_LOGIC]`.

- **Resh:** spawns at (2, 0, 15), facing −Z toward the lane the player approaches from (target rotation Euler(0,180,0) — same unresolved `InstantiateNpc` facing gap as the Broker, §4 Beat 1c) — inside Market Row, near its second stall cluster. `InstantiateNpc(Ch2ReshPrefab, …)`, `FitNamedCharacter`, `StoryNpc` (displayName "Resh"), plus `StoryNpcWander` radius **0.8 m** — the only NPC in this chapter given a wander component, letting him read as "working the rail" rather than standing frozen. He never leaves Market Row; there is no `NpcWalker` leg carrying him to the Auction Floor or anywhere else in the chapter (§5) — once recruited he simply stops mattering to the mission spine as a physical object.
- **Player:** the confront/recruit dialogue fires immediately after Beat 1's Undermarket dialogue (steps 3–4), with no additional `ReachTrigger` between them — the player is already standing in Market Row from step 1. Note the ~3 m offset between `MarketReachPoint` (0,1,12), where the player gates, and `Dialogue_Beat2_ReshConfront`'s anchor at (2,1,14) near Resh's spawn (2,0,15): fine for talk-to-advance, but a builder should verify the confront set's audio falloff still reaches the reach point so the beat doesn't start inaudible (verification note, not a known defect). To reach the Auction Floor combat, the player must separately walk south past Broker Front into `AuctionReachPoint` (0, 1, 39) radius **5** (step 7), which only gates *after* Beat 1's Broker dialogue (step 6) has played.
- **Enforcers:** four `Enemy` instances built **inactive**, `EnsureEnemyDefinition()` (the shared generic melee definition), at (-3,0,34), (3,0,34), (-4,0,40), (4,0,40) — inside the Auction Floor footprint, flanking the stage.
- **Bodyguard:** one `Enemy` instance built **inactive** at (0,0,44), using a **dedicated, tougher** `EnemyDefinition` — `Ch2EnsureBodyguardDefinition()` creates `Assets/Ronin7/Data/Ch2Bodyguard.asset` with `maxHealth 220` (vs. the shared enforcer definition), `damage 22`, `moveSpeed 1.1` (slower), `attackCooldown 1`. **Note the divergence from the dialogue script:** the treatment stages the Bodyguard guarding the Records Vault terminal ("The broker's bodyguard is between them and the terminal"), but the shipped build folds him into this same Auction Floor `DefeatEnemies` gate alongside the four enforcers — the builder's own comment states this plainly: *"the script places the Bodyguard in the vault, but this build condenses the chapter's combat into one set-piece on the Auction Floor."* By the time the player reaches the Vault (Beat 4), it is already cleared of any fight. See §9.
- **`AuctionToCellsDoor` lock state:** `startLocked: true`, unlocked by step 9's Trigger, fired the moment step 8's `DefeatEnemies` resolves — the whole enforcer+Bodyguard group must die before the Cells route opens. Under the `ProximityDoor` model (§2), "unlocked" only arms the proximity check — the door stays visibly shut until the player is within 3 m of z=48, at the back of the 18×18 room, and does not pop open the instant the last combatant dies. A builder should expect a shut door immediately after the fight, opening only on approach; that is the correct, comfort-safe behavior and should be preserved, not mistaken for a bug.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 3 | Dialogue: Beat2 Resh (confrontation) | Plays `ch2_beat2_resh_confront` — Resh's refusal, the standoff, the mercy |
| 4 | Dialogue: Beat2 Resh (spared, recruited) | Plays `ch2_beat2_resh_recruit` — the recruitment, the lead into the cells |
| 7 | ReachTrigger: Auction Floor | Gates on distance to `AuctionReachPoint` (0,1,39), radius 5 |
| 8 | DefeatEnemies: Syndicate Enforcers + Bodyguard | Waits for all five `Health` components (4 enforcers + Bodyguard) to reach zero |
| 9 | Trigger: Unlock Cells Route (Resh's back-channel) | Unlocks `AuctionToCellsDoor`; advances immediately on the defeat step resolving |

**What changes during the beat:** Resh's `StoryNpcWander` never gets superseded by a walker (unlike Kessler in Ch1) — he simply stays where he is, wandering, for the rest of the chapter. The five enemies flip `SetActive(true)` as a group (not individually staggered) once the pre-fight dialogue and reach conditions are satisfied; `AuctionToCellsDoor` flips from locked to open the instant the last of them dies.

#### c. Art & Environment Instantiation → `BuildBeat2Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Resh (NPC) | (2, 0, 15), Euler(0,180,0) — facing −Z, toward the market lane the player approaches from | `Named.Resh` | `…/Art/Generated/Characters3D/Named/Resh.prefab` | **EXISTS** |
| Room shell (18×18) | center (0,0,39) | `Rooms.AuctionFloorShell` | `…/Art/Generated/Rooms/AuctionFloorShell.prefab` | **MISSING** |
| `Auction_Stage` | (0, 0.25, 39) | `Props.AuctionStage` | `…/Art/Generated/Props/AuctionStage.prefab` | **MISSING** |
| `Auction_LotPedestal` | (0, 0.9, 39) | `Props.AuctionLotPedestal` | `…/Art/Generated/Props/AuctionLotPedestal.prefab` | **MISSING** |
| `Auction_CageBar` ×4 | ring, radius 1.1 around (0, 1.6, 39) | `Props.AuctionCageBar` | `…/Art/Generated/Props/AuctionCageBar.prefab` | **MISSING** |
| Lot figure (decorative, inside cage ring) | ~(0, 0, 39) | `Diversity.VelorumCrowd` | `…/Art/Generated/Characters3D/Diversity/{Vesh,Voll}/…` | **MISSING** — no builder or wirer call places a figure here today |
| `OverseerBox` | (-8.5, 2.8, 44) | `Props.OverseerBox` | `…/Art/Generated/Props/OverseerBox.prefab` | **MISSING** |
| `AuctionCallerPulpit` | ~(6, 0.9, 36), facing the stage | `Props.AuctionCallerPulpit` | `…/Art/Generated/Props/AuctionCallerPulpit.prefab` | **MISSING** — no prop, no NPC, no VO exist for this today; see note below and §9 |
| `AuctionLight0` | (0, 3.2, 39) | — | `ChapterEnvironmentProfile.accentLights["Auction0"]` | profile |
| `AuctionLight1` (overseer box) | (-7, 3, 44) | — | `ChapterEnvironmentProfile.accentLights["Auction1"]` | profile |
| Syndicate Enforcer ×4 | (-3,0,34), (3,0,34), (-4,0,40), (4,0,40) | `Enemies.SyndicateEnforcer` | `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** *(applied additively by `EnemyArtWirer`, swapping the greybox capsule visual — see note below)* |
| Broker's Bodyguard | (0, 0, 44) | `Enemies.BrokerBodyguard` | `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** *(same visual as the enforcers — no distinct Bodyguard art; see note)* |
| `AuctionToCellsDoor` | (0, 0, 48) | `Doors.SlidingDoor_Standard` | `…/Art/Generated/Doors/SlidingDoor_Standard.prefab` | **MISSING** |

**Notes on the transition.** `EnemyArtWirer.cs`'s scene map wires `("Ch02_Auction", "Coil_Syndicate_Ganger", null)` — a **single** primary enemy type, no secondary — so it swaps the greybox capsule visual on *every* `Enemy` instance it finds in the scene, enforcers and Bodyguard alike. There is no mechanical or visual distinction between the trash-mob enforcers and the tougher Bodyguard beyond the `EnemyDefinition` stat block (§b) — flagging this as a narrative/mechanic gap, not proposing a fix, mirroring Chapter 1's note about the Dominion Trooper's "melee despite rifle-carrying dialogue" mismatch. A dedicated, visually distinct Bodyguard prefab (heavier build, different silhouette) is a natural commission once the environment art pass reaches this room.

**The auction block itself has no subject-of-sale — the room's centerpiece stages an auction with nothing being auctioned.** Canon frames the whole world as one that "sells people and pretends it doesn't" (§3), and the crowd itself as "a sea of buyers **and sold**" (dialogue line 14); the Beat 2 SETTING (line 50) puts "a raised block under hard light" at the center of "a packed gallery of bidders." `Ch2BuildAuctionSet` builds `Auction_LotPedestal` ringed by four `Auction_CageBar`s and stops there — a literal empty cage on a stage. This is distinct from the gallery-crowd gap below (that's the *buyers*; this is the *sold*), and arguably the single strongest uncovered image in the chapter's biggest, most-watched room: one non-interactive decorative figure from the already-resolved `Diversity.VelorumCrowd` art, placed inside the cage ring at ~(0, 0, 39), would read the room as "a market that eats people" rather than an empty rehearsal stage, for the cost of one more instantiation of art already on disk. See §9.

**The auction caller is a second, unbuilt diegetic voice, distinct from the overseer's PA.** SETTING Beat 2 names "a caller's pulpit" among the room's furniture, and dialogue line 347 has "the auction caller drones on, selling" running *underneath* Resh's recruitment lines — a live sale continuing in the background while the player's scene plays out in the foreground. `Ch2BuildAuctionSet` builds stage, pedestal, cage-ring, and overseer's box only; there is no pulpit prop, no caller NPC, and no caller VO loop anywhere in the builder. This is the chapter's biggest, most-watched room, and its second-most-obvious atmospheric fixture (after the overseer's PA, §f) is currently missing from the prop ledger entirely. See §9.

**The gallery itself — the room's defining crowd — is entirely absent.** SETTING Beat 2 (dialogue line 267) stages the Auction Floor as "a tiered amphitheater pit... a packed gallery of bidders," where "the noise is a physical thing." `PlaceDecorativeCrowd` (§1.2) is called exactly **once**, chapter-wide, for Market Row's six positions (`Chapter2Builder.cs:109`); `CrowdArtWirer`'s own scene map likewise resolves only `("Ch02_Auction", {Vesh, Voll})` into Market Row. No decorative-crowd call of any kind targets the Auction Floor. As built, the enforcer fight plays in an empty amphitheater — the chapter's single biggest (18×18), most-watched, most-canonically-crowded room has **zero spectators** — a rehearsal-hall reading, not a market that "eats people." This is the larger and more obvious of the room's three unbuilt atmosphere gaps (alongside the overseer's figure/PA and the caller's pulpit above): a second, additive `PlaceDecorativeCrowd` call, tiered/perimeter positions ringing the pit, using the already-resolved `Diversity.VelorumCrowd` art, is the concrete fix. See §9.

#### d. Combat — the sparing choice, and the first fight

**The sparing of Resh has no combat gate.** Although the dialogue script explicitly forks here ("PLAYER CHOICE — TWO PATHS TO RESH... Fought across cover and chokes around the block... Resh tries to slip the fight using the crowd... End state: Ronin-7 runs him down... blade drawn, Resh cornered... Hold there.") and the Game Narrative Design section calls out an "Auction-floor brawl OR bidding mini-system" branch, **`Chapter2Builder.cs` builds neither branch.** `ch2_beat2_resh_confront` plays start to finish as a single linear dialogue set with no player input beyond advancing lines, converging directly on Resh's "Go on, then... finish it" and Ronin-7's "Go." — the mercy choice is authored, the branching mechanism that would let a player *choose* combat over words is not. Flagged for design awareness in §9, not resolved here.

**The Auction Floor fight itself** plays via the standard `BladeDamager` EMA swing-speed model (**existing system — reuse, don't reinvent**). Player `Health` lives on the rig (`BuildRig`), wired as `playerHealth` to every enforcer's and the Bodyguard's `Enemy` component. All five combatants activate as a group once the pre-conditions (reach + preceding dialogue) are met; `DefeatEnemies` simply waits for all five `Health` components to hit zero, wherever in the 18×18 room that happens — the stage, pedestal, and cage-bar ring (§c) read as combat terrain/cover, not scripted blocking.

**The activation geometry is itself a deliberate ambush, even without scripted blocking — though the envelopment develops on approach rather than existing from the first frame.** `AuctionReachPoint` (0,1,39) sits *between* the entrance gap (z=30) and two of the four enforcers, at (-3,0,34) and (3,0,34) — the player walks past those two, still inactive, to trigger the reach step, which fires at the reach point's edge, roughly z≈34: level *beside* those two enforcers at the room mouth, not standing on the stage (`Auction_Stage` spans z≈36–42, ~5 m further in). At that instant the geometry reads beside-and-ahead — the two z=34 enforcers flanking the player's sides, the other two at (-4,0,40)/(4,0,40) and the Bodyguard at (0,0,44) all still ahead — and only becomes true front-and-back as the player advances past the z=34 pair toward the stage. The escalation-with-+Z-depth point still holds: the Bodyguard — the toughest combatant — waits deepest, out under the middle of the floor at the same depth (z=44) as the overseer's box (-8.5,2.8,44), 8.5 m east of it laterally, not beneath it, and a player standing on the stage can look up-west to the box throughout. A builder reading only the position table could miss that the reach-point placement *is* the encounter's blocking, and that the ambush's front-and-back character is a function of the player's own advance, not the activation instant.

**Unlike Beat 5's guaranteed-occluded reveal (§4 Beat 5b), this five-enemy activation is an unoccluded, in-view pop-in.** The reach step fires at roughly z≈34 — level with the two enforcers already flanking the player there — and the group `SetActive(true)` call flips all five combatants live in the same instant, in an open 18×18 room with no wall, door, or crowd (§c) between them and the player's forward view. This is the mirror image of Beat 5's `VaultToDockDoor`-gated reveal, where the door's 3 m proximity radius guarantees Mira and both pursuers pop in only behind a closed panel (§4 Beat 5b) — Beat 2 has no equivalent gate, so the same class of instantaneous multi-body spawn that Beat 5 handles safely is, here, a real in-headset pop-in risk. Concrete, in-scope-adjacent mitigations (named, not mandated): push `AuctionReachPoint` deeper — e.g. z≈42 — so the two forward enforcers sit behind the player's shoulder at trigger rather than in view, or stagger the group `SetActive` call over a few frames instead of firing all five at once.

**The fight also has no diegetic motivation in the shipped linear build.** Step 6 (the Broker's refusal) is the last line of dialogue before it; step 7→8 spawns five enemies with no line, PA, or stage-beat explaining who these enforcers are or why they attack a crew that has already recruited Resh and left the broker behind. Canon's Auction fight belongs to the unbuilt brawl fork above — it is Resh's *bought muscle*, called up the instant he signals for it ("Up in the gallery, syndicate muscle shifts. He's bought himself cover," line 303) — a cause that exists only in the fight-Resh path the build doesn't implement. In the shipped talked-down path, the enforcers have lost that motivating cause entirely; nothing in the room says why the market's floor turns on the crew the moment they arrive. The fix is already half-written elsewhere in this document: the overseer's unbuilt "calm house PA that answers when the floor's order is disturbed" (line 28, §f, §9) is the natural trigger line for this fight — one barked PA line would convert an unexplained ambush into the market defending its floor. The PA is therefore not just an atmosphere commission, it is this fight's missing motivation.

#### e. Dialogue / VO

Two dialogue sets, both advanced on **Left-Hand "Talk" (Y)**:

- **`ch2_beat2_resh_confront`** (`Dialogue_Beat2_ReshConfront`, at (2,1,14)) — 16 lines. Resh's brush-off, then his fear-driven read of Ronin-7 as "Program work," his bought-cover bravado ("this floor decides which of us it keeps"), the convergence on his own certainty of death, Ronin-7's "Go.", and Resh's disbelief.
- **`ch2_beat2_resh_recruit`** (`Dialogue_Beat2_ReshRecruit`, at (2,1,16)) — 11 lines. Resh naming what Ronin-7 is ("A Program weapon walking around with a salvage rat and an open hand"), his confession of the child-smuggling work, his explicit ask to join, Kessler's test ("This is bigger than robbing a stall. This is the Dominion."), Resh's answer, and his closing line naming the morning's lead into the cells — which is what makes step 9's Cells-route unlock make narrative sense once the Auction Floor is cleared.

**Mira's gallery plant is unstaged.** Canon plants Mira a second time here, watching from the crowd — "a small figure between the legs of the bidders... Watching Resh... The child knows that face" — ahead of her Beat 5 payoff ("she followed you from the floor"). As built, Mira exists only as the single, inactive spawn at (1.5,0,77) in the Dock (§4 Beat 5); there is no child object anywhere in the Auction Floor. Like Beat 1's pickpocket note (§4 Beat 1), the gallery plant is carried entirely by the recruit-beat VO and stage direction, not an engine object *(inferred: no engine object backs this image today)*.

**Continuity note (resolved):** the raw dialogue script's Beat 2 convergence block ships with corrupted, dropped-character text (`"i  ays ends"`, `"Resh, cor  ,"`, etc.) per `audit/Ch02_audit.md` §3. `Chapter2Lines.cs`'s header comment confirms this is **already fixed** in the shipped data (`"it always ends"`), so this is a resolved audit item, not an open one — cited here for the record, not flagged as outstanding.

#### f. Audio / Haptics / VR Comfort

- **No camera shake** at any point, including the fight — impact reads through `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle only, matching the project's non-negotiable VR constraint. The per-hit pulse itself is not authored per-chapter: it is inherited automatically through `BladeDamager` → `Haptics` on the player rig, the same wiring every `BuildEnemy`/`playerHealth` pairing gets chapter-wide — this fight has authored feedback, it just isn't bespoke to Ch2.
- No ambient bed plays in either Market Row or the Auction Floor (§9 flags the chapter-wide ambience gap).
- **The chapter's thesis line has an unbuilt audio-duck + light-tighten cue.** Dialogue-script line 316's CONVERGENCE stage direction — "Lighting tightens to the two of them; the floor noise drops back" — is the directorial punctuation for the mercy beat, landing on Resh's "Go on, then… finish it" (line 319) and Ronin-7's one-word "Go." (line 326), the moment the treatment itself names as the chapter's thesis (§a). Both lines play through `Dialogue_Beat2_ReshConfront`/`Dialogue_Beat2_ReshRecruit`, anchored in Market Row under static `MarketLight0`/`MarketLight1` (`behaviour: None`, §6) — there is no lighting event to tighten toward the two of them, and, per the ambient-bed gap above, no floor noise to drop back in the first place. This is the third and most dramatically load-bearing use of the same audio-ducking device this document tracks: canon also stages the roar rising on approach to the Auction Floor (line 261, §9) and falling away into the Cells and Vault (lines 401/523, §9) — this instance is tied to a specific line rather than a room transition, and today it has no cue of either kind to fire on.
- `AuctionLight0` and `AuctionLight1` both carry `behaviour: None` — static hard white over the stage, cooler blue over the overseer's box. `AuctionLight0` (white, i3, r22) is the fixture the dialogue script's SETTING block names directly — "slaughter-bright auction lamps." No flicker or pulse marks the fight; the tension is carried by the dialogue's own escalation and the fight itself, not a lighting cue.
- **The market's own voice is unbuilt — and it is not just atmosphere, it is the fight's missing motivation (§d).** Per the dialogue script's SETTING block, Velorum "has a voice" — when the Auction Floor's order is disturbed, "that voice comes over the house PA — never raised, never hurried... Velorum's version of the Handler: the calm thing that owns the loud room." Canon also stages "a still figure behind" the overseer-box glass. As built, `Ch2BuildAuctionSet` places only the `OverseerBox` primitive and `AuctionLight1` — there is no PA `AudioSource`, no overseer VO line, and no figure NPC inside the box. Because the fight already carries no lighting cue (above) and no dialogue lead-in (§d), the overseer's calm PA line is the chapter's single best unbuilt diegetic-audio opportunity, and a mechanical one — a natural companion to the ambient-bed gap (§9), not fixed here. The dialogue script also has an auction caller who "drones on, selling" underneath Resh's recruitment lines — a second, separate diegetic-PA texture (§c), and the single best candidate source for the Market/Auction ambient bed the chapter is missing: a looping caller-drone `AudioSource`, positioned at the (unbuilt) `AuctionCallerPulpit`, would fill both the caller gap and the ambient-bed gap (§9) with one asset.
- **VR-comfort asymmetry, cross-referenced:** the five-enemy group activation (§4 Beat 2d) is an unoccluded, in-view pop-in — the comfort parity gap against Beat 5's door-hidden reveal (§4 Beat 5b), which this document's comfort analysis otherwise only guards on one side of the chapter. See §4 Beat 2d for the geometry and named mitigations.
- **The fight plays to an empty house.** SETTING Beat 2's "packed gallery of bidders" and "the noise is a physical thing" (§c) have no audio counterpart either — with no gallery crowd built (§c), there is no crowd-reaction layer to author in the first place. A caller-drone bed (above) fills the room's baseline noise; a crowd murmur/reaction layer tied to the eventual gallery crowd is the natural second half of that same fix.
- Comfort vignette engages normally on the two-room walk from Market Row through Broker Front to the Auction Floor, and again during combat movement/snap-turns.

---

### Beat 3 — Iris Freed

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat3Art()`** (the cage-bar clusters) and **`BuildBeat3Logic()`** (Iris's spawn, the cells reach point, the free-Iris prompt, the reunion dialogue).

#### a. Narrative purpose & emotional target

The man built to erase families puts one back together. Iris's introduction is guarded and transactional — "If you're buyers, the auction's two tiers up. If you're something else, get it over with" — until Ronin-7 reads her collar-tag aloud ("The tag says Iris.") and Kessler's voice breaks open over the comm for the first time in six years. The reunion happens entirely through a wire: Kessler is not physically present, and the scene's whole emotional weight rests on voice alone — "Say that again. Say the name again." The beat closes with Iris choosing to stay and work rather than bolt for the surface the instant she's free ("If you're going to that vault, you'll want me at the terminal, not behind you"), which is her recruitment: crew conscience and systems hand, live from the moment the cage door opens.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components parent to `[BEAT_3_LOGIC]`.

- **Iris:** spawns at (1, 0, 54), facing −Z out of the cage toward the door the player enters from (target rotation Euler(0,180,0) — same unresolved `InstantiateNpc` facing gap, §4 Beat 1c) — inside a cage cluster near the room's center; canon has her "read the crew through the bars" (line 431), which only reads correctly if she's facing the entrance. `InstantiateNpc(Ch2IrisPrefab, …)`, `FitNamedCharacter`, `StoryNpc` (displayName "Iris"). **No `StoryNpcWander`** — static until freed, consistent with being caged.
- **Player:** must walk through the now-open `AuctionToCellsDoor` (z=48, unlocked at the end of Beat 2) into `CellsReachPoint` (0, 1, 55) radius **5** (step 10).
- **`CutIrisPrompt`:** worldspace TextMesh "Cut Iris Free (Y)" at (1, 1.4, 54), driven by `PromptInputAdvancer`, created **inactive**, enabled only for step 11. Advancing it via the Left-Hand Talk action is the player's own input that frees her — the same "player's own hand does the mercy" pattern as Chapter 1's Release-Grapple prompt.
- **Kessler:** still has no physical presence — his reunion lines are voiced-only ("Kessler (Comm)" in the raw script), tagged in the shipped data simply as speaker `"Kessler"` with no distinguishing suffix (§9).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 10 | ReachTrigger: Holding Cells | Gates on distance to `CellsReachPoint` (0,1,55), radius 5 |
| 11 | Prompt: Cut Iris Free | `CutIrisPrompt` activates; player's Left-Hand "Talk" (Y) input via `PromptInputAdvancer` cuts the lock and advances the director |
| 12 | Dialogue: Beat3 Iris Freed (Kessler reunion) | Plays `ch2_beat3_iris` in full |

**What changes during the beat:** nothing in the room's static geometry — the change is entirely in Iris's own state (caged → freed, no `StoryNpc` re-parenting or repositioning scripted) and the prompt's active/inactive toggle.

#### c. Art & Environment Instantiation → `BuildBeat3Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (12×14) | center (0,0,55) | `Rooms.HoldingCellsShell` | `…/Art/Generated/Rooms/HoldingCellsShell.prefab` | **MISSING** |
| Cage cluster ×4 | (-3.5,0,51), (3.5,0,51), (-3.5,0,58), (3.5,0,58) | `Props.CollateralCage` | `…/Art/Generated/Props/CollateralCage.prefab` | **MISSING** |
| Iris (NPC, caged) | (1, 0, 54), Euler(0,180,0) — facing −Z, out of the cage toward the door the player enters from | `Named.Iris` | `…/Art/Generated/Characters3D/Named/Iris.prefab` | **EXISTS** |
| `CellsLight` | (0, 2.4, 55) | — | `ChapterEnvironmentProfile.accentLights["Cells"]` | profile |

**Notes on the transition.** The four bar clusters (each built from four thin primitive props today) collapse into a single `CollateralCage` prefab per instance — the same one-key-many-placements pattern as `Doors.SlidingDoor_Standard` and `Props.MarketStall`. This room's floor tint is uniquely darker/wetter than the shared `floorColor` used elsewhere in the chapter (Appendix A.1) — a deliberate authored value, not an oversight, matching the SETTING block's "wet, low, ugly." The dialogue script grounds that wetness concretely: after freeing Iris, Ronin-7 cuts her collar-tag and it "hits the wet floor" — the physical detail the unique floor tint is built to support, even though the tag-cut itself has no engine prop (§d).

**The cages themselves carry no collar-tags, and the tag is the beat's load-bearing emotional prop.** Canon's Cells SETTING (line 60) names "Collateral cages, **collar-tags**" as the room's defining furniture, and the beat's payoff turns on one: Ronin-7 reading Iris's tag aloud (§a) and, later, cutting it free so it "hits the wet floor" (line 493, above). `Props.CollateralCage` is specced as bar clusters only (Appendix A.1, Appendix B) — no hanging tag element exists on the cage prop or on Iris herself. A small hanging collar-tag element on the `CollateralCage` prefab — the visible mark of "collateral," not cargo — is the concrete fix; the tag-cut event itself remains VO/prompt-only either way (§d).

#### d. Combat

None. The dialogue script stages a syndicate enforcer challenging the party on entry ("Channel's supposed to be dead down here. Who's moving?") and Resh talking past him, but no `Enemy` GameObject or combat gate exists for this room in the builder — the enforcer line is voiced dialogue only, resolved without a fight *(inferred: no engine object backs this encounter today)*. This is consistent with the beat reading as intimate/quiet rather than a second combat set-piece so soon after the Auction Floor.

The collar-tag cut itself is likewise narration-only: the script has Ronin-7 cut the tag off "with two fingers of pressure and a turn of the blade" immediately after freeing Iris, but there is no tag prop and no scripted cut event — the reunion's physical beat plays entirely through VO and the `CutIrisPrompt` interaction, not a modeled object *(inferred: no engine object backs this action today)*.

#### e. Dialogue / VO

One dialogue set, advanced on **Left-Hand "Talk" (Y)**:

- **`ch2_beat3_iris`** (`Dialogue_Beat3_Iris`, at (1,1,56)) — 16 lines. The enforcer's challenge, Resh's cover story, Iris's guarded opening line, Ronin-7 reading her tag, Kessler's voice breaking open over the comm, the "They told me you stopped looking" / "They lied" exchange, Ronin-7 cutting her loose, Iris choosing to go to the vault rather than straight to Kessler, and Kessler's edged closing line about the Handler's three-day clock now that Iris is off the board.

#### f. Audio / Haptics / VR Comfort

- No camera shake. `Ronin-7`'s "Hold still. I'm cutting you loose" line covers the lock-cutting beat; there is no scripted haptic pulse for it, matching Chapter 1's practice of reserving controller haptics for actual `BladeDamager` combat hits rather than every blade-adjacent narrative beat. **Available value-add, not a settled decision:** this is the chapter's single most tactile *agency* beat ("the player's own hand does the mercy," §a) and has no combat hit to inherit a haptic from the way the two brawls do — a short one-shot `Haptics` pulse fired on `PromptInputAdvancer` success would land the "you did this" moment in-headset. Offered for consideration, not mandated; the current silence is a deliberate, defensible Ch1-parity choice as written.
- No ambient bed in this room (§9).
- `CellsLight` carries `behaviour: None` — static, dim teal-green, no flicker.
- Comfort vignette applies normally on the walk from the Auction Floor into the Cells.

---

### Beat 4 — The Failed Execution (The Reveal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat4Art()`** (the records racks, disposal terminal, diagnostic table) and **`BuildBeat4Logic()`** (the vault reach point, the reveal dialogue). **No enemy is spawned or gated in this beat — the Bodyguard fight already resolved on the Auction Floor (Beat 2). Do not add a `DefeatEnemies` step here without reconciling it against that condensation first.**

#### a. Narrative purpose & emotional target

This is Ladder A, rung 1 — the chapter's central reveal, and per the story bible the mystery it opens ("why did it fail?") isn't answered until Ch13. After a chapter of crowds, the beat is deliberately intimate: "the two people who matter and one machine telling the truth." Iris reads the disposal terminal cold and procedural at first ("Every one of these is a person. Dead operatives, jettisoned, then bought back here as salvage") until she finds Ronin-7's own file and the register shifts. The reveal itself lands in three short exchanges: the discharge confirmed ("The killswitch discharged. Full sequence."), Ronin-7's flattest possible question ("Then why am I reading it?"), and Iris's answer breaking the clinical register for the first time ("Because it didn't take. It fired into your skull, full discharge, and you just kept breathing."). The stage direction immediately after (line 589) gives the reveal its one physical beat: "Ronin-7's hand rises slowly to his throat, finds the old scar there — the one Kessler noticed on the table three weeks ago, closed clean and opened again." Ronin-7's correction — "Mine's the only one they've found that failed" — is the beat's real turn: relief curdling immediately into a hunt, because if his leash slipped once, it can slip on others still leashed and unaware. Kessler's one comm line lands the mystery of Chapter 1's revival table at its true size: "I knew the box didn't hold. I didn't know nothing was ever supposed to come out of it at all."

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components parent to `[BEAT_4_LOGIC]`.

- **Player:** must walk through the open Cells→Vault gap (z=62, no door) into `VaultReachPoint` (0, 1, 68) radius **4.5** (step 13).
- **No enemy, no combat gate.** Resh and Iris are present narratively (Resh "works the outer door," Iris "goes straight to the terminal") but neither is repositioned by a scripted walk — both simply remain wherever the mission spine last placed them relative to the player's traversal; this room has no bespoke NPC blocking logic of its own beyond the dialogue player.
- **`VaultToDockDoor` lock state:** `startLocked: true`, unlocked only by step 15's Trigger at the *start* of the next beat, not by anything in this one.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 13 | ReachTrigger: Records Vault | Gates on distance to `VaultReachPoint` (0,1,68), radius 4.5 |
| 14 | Dialogue: Beat4 The Failed Execution (killswitch reveal) | Plays `ch2_beat4_reveal` in full — the chapter's longest dialogue set |

**What changes during the beat:** nothing in the set dressing. This is the third pure-dialogue beat in the chapter (after Beat 0 and part of Beat 1/3), and its emotional weight is carried entirely by performance and the cold, sealed room around it.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (10×12) | center (0,0,68) | `Rooms.RecordsVaultShell` | `…/Art/Generated/Rooms/RecordsVaultShell.prefab` | **MISSING** |
| `RecordsRack0` | (-4.0, 1.2, 63) | `Props.RecordsRack` | `…/Art/Generated/Props/RecordsRack.prefab` | **MISSING** |
| `RecordsRack1` | (-3.5, 1.2, 63) | `Props.RecordsRack` | `…/Art/Generated/Props/RecordsRack.prefab` | **MISSING** |
| `RecordsRack2` | (-3.0, 1.2, 63) | `Props.RecordsRack` | `…/Art/Generated/Props/RecordsRack.prefab` | **MISSING** |
| `DisposalTerminal` | (0, 0.9, 68) | `Props.DisposalTerminal` | `…/Art/Generated/Props/DisposalTerminal.prefab` | **MISSING** |
| `DiagnosticTable` | (2.5, 0.45, 70) | `Props.DiagnosticTable` | `…/Art/Generated/Props/DiagnosticTable.prefab` | **MISSING** |
| `VaultLight` | (0, 2.8, 68) | — | `ChapterEnvironmentProfile.accentLights["Vault"]` — `behaviour: AmbientPulse(period: 6.5s)` | profile |

**Notes on the transition.** The `DisposalTerminal`'s bright cyan emissive tint (0.2, 0.85, 1) is the one deliberately "lit" surface in an otherwise cold, dark room — it should read as the single point of truth in the vault, the machine "telling the truth" per the SETTING block. The `VaultLight`'s slow ambient pulse is the chapter's second and last behaviour-bearing light (after `BrokerLight`'s flicker), a quiet counterpoint to the room's stillness rather than a warning cue.

**The reveal's interaction locus is the terminal, not the table.** Canon foregrounds "a diagnostic table built to read an operative's termination record," but as built Iris "goes straight to the terminal," the `ch2_beat4_reveal` dialogue player sits at (0,1,69) beside the `DisposalTerminal`, and `DiagnosticTable` (2.5,0.45,70) sits apart as unlit dressing. A builder or artist should anchor the emissive/interaction treatment on the terminal — the table is off-axis set dressing, not the reveal's prop.

**The racks don't deliver canon's "among the racks of the dead" staging.** All three `RecordsRack` instances cluster on one edge — x=-4.0/-3.5/-3.0, all at z=63 — while the player, the terminal, and the reveal dialogue anchor sit at z=68, roughly 5 m away with the racks entirely to one side. Canon repeatedly stages Ronin-7 as surrounded by the dead, not standing near a shelf: "Ronin-7 stands among the racks of the cataloged dead" (line 523), "every one of them a file like his" (lines 544, 627, 644 make the same image). The reveal's emotional weight is the dead *watching* the reveal happen, not a rack visible off to one side. A prefab/blocking pass should distribute the racks around the terminal — or at minimum flank both sides of the z=68 approach — so the target staging is enclosure, not a single wall of shelving.

#### d. Combat

None. As noted in §b, the Bodyguard mini-boss the dialogue script stages guarding this room's terminal was already folded into Beat 2's Auction Floor `DefeatEnemies` gate; nothing contests the terminal here.

**The data drive Ronin-7 carries out of the vault has no engine object either.** Beat 4's dialogue closes on his order to copy every termination file (§e), and line 644's stage direction has him pause "at the lip of the route up, the drive in his hand" — every file a leash that held — before Beat 5 begins: the one physical object the whole beat's investigation produces, and what he's still carrying at the chapter's close. No prop of any kind backs it in the builder *(inferred: no engine object backs this image today)*, the same class as the pickpocket (§4 Beat 1d), the gallery plant (§4 Beat 2e), and the collar-tag cut (§4 Beat 3d). A small `Props.DataDrive` hand-prop is a candidate addition only if the reveal's payoff is ever staged rather than left VO-only.

#### e. Dialogue / VO

One dialogue set, the chapter's longest, advanced on **Left-Hand "Talk" (Y)**:

- **`ch2_beat4_reveal`** (`Dialogue_Beat4_Reveal`, at (0,1,69)) — 19 lines. Resh clearing the door and Iris reaching the terminal; Iris's read of the disposal trade; Ronin-7 connecting it to his own casket; Resh's grim recognition of what his own life's work runs on; Iris finding Ronin-7's file; the killswitch-fired-and-failed reveal; Ronin-7's correction that his is only the one *they've found* that failed; Kessler's comm line about the coffin; Ronin-7's order to copy every file in the room; Resh's closing line pointing the crew toward the escape route.

**Continuity note (resolved):** the raw dialogue script's Resh line here originally read "Let's get **the kid and** Kessler's girl home..." — a continuity error, since Mira is not discovered until the next beat and Resh's own surprise at finding her proves he doesn't know she exists yet. Per `audit/Ch02_audit.md` §3, `Chapter2Lines.cs` **already ships the fix** (drops "the kid and"), confirmed in its own header comment. Resolved, cited for the record.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics — the third dialogue-only beat.
- **The reveal's one physical beat — the throat-scar touch (line 589, §a) — cannot be embodied and is VO/performance-carried only.** The player *is* Ronin-7 (bodiless head+hands rig, §2), so the gesture has no engine object to play against, the same class of moment this document flags elsewhere as *inferred: no engine object backs this image* (the pickpocket, §4 Beat 1d; the gallery plant, §4 Beat 2e). **Available value-add, not a settled decision:** parallel to the Beat 3 §f Cut-Iris haptic offer, a single subtle one-shot `Haptics` pulse on Iris's "you just kept breathing" line (line 585) would give the player's own hands a physical anchor to the reveal — the one place in the chapter the medium can substitute for the lost gesture. Offered for consideration, not mandated.
- No ambient bed in this room (§9).
- `VaultLight`'s slow pulse is the only lighting motion in the scene; nothing else changes.
- Comfort vignette applies normally on the short walk from the Cells into the Vault.

---

### Beat 5 — Escape, Reunion & Mira Kept

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat5Art()`** (crates, tie-downs, the dock shell) and **`BuildBeat5Logic()`** (Mira's inactive reveal + `ProtectNpcObjective`, the two escape pursuers, the dock reach point, the reunion/kept dialogue, and `ChapterOutro`). **The escape-run pursuers and Mira's reveal share one Trigger step — do not split them without re-checking the `ProtectNpcObjective` wiring (§5), which depends on Mira's `Health` existing and being non-null before the pursuer that targets her is authored.**

#### a. Narrative purpose & emotional target

The chapter's final turn is homecoming, and it happens twice: Kessler finally holds the daughter he's only had on a wire ("Six years I had a voice on a wire and a coffin where you should have been. Stand still. Let me look at you with my own eyes."), and then, almost immediately after, the crew discovers they've picked up a second one. Resh's smuggler instincts catch the discrepancy before anyone else does — "I came up that ramp with one more set of footsteps than I should have" — and Mira's first line to him ("You're Resh. You're the one who gets us out.") reveals she's known his legend from the market-children's whisper network long before she ever followed him aboard. Kessler's hesitation is a father twice over in one day, and Ronin-7's refusal to send her back — flat, unprompted, immediate ("No.") — is explicitly called out by Resh as the second thing today "your kind doesn't do." The beat, and the chapter, closes on the crew becoming "a family of the unwanted," Ronin-7 laying the wrapped katana on the rack — the exact image Chapter 3 opens on.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat5Logic()`

All logic components parent to `[BEAT_5_LOGIC]`.

- **Mira:** spawns at (1.5, 0, 77), facing −Z toward the crew's approach (target rotation Euler(0,180,0) — same unresolved `InstantiateNpc` facing gap as the chapter's other three Named NPCs, §4 Beat 1c) — inside the Dock room, built **inactive**. `InstantiateNpc(Ch2MiraPrefab, …)`, `FitNamedCharacter`. Carries a `Health` (`Configure(40f)`) and a `ProtectNpcObjective` wired with `protectedHealth = miraHealth` and `playerEntity = rig` — **this is a live, working fail-state**: if she dies while the objective is enabled, `ProtectNpcObjective.OnProtectedDied()` republishes `EntityDied` for the player rig GameObject, which `GameFlowManager`'s existing `OnEntityDied` handling already treats as a game-over condition (the same path a player-rig death triggers). No new game-over code was written for this — it reuses the existing flow by pointing the *NPC's* death at the *player's* death event. `StoryNpc` displayName "Mira". **She is only revealed — `SetActive(true)` — by step 15's Trigger**, not discovered organically under a tarp as the dialogue script stages it. See §9.
- **Pursuer 1:** an `Enemy` instance, built **inactive** at (-2, 0, 78), wired against `playerHealth` — the escape run's threat to the player.
- **Pursuer 2:** an `Enemy` instance, built **inactive** at (2, 0, 80), wired against **`miraHealth`, not `playerHealth`** — this is the mechanical teeth behind the `ProtectNpcObjective`: this specific enemy's attacks target Mira's health pool, so losing this fight (rather than merely losing the player's own health) is what can fail the chapter here.
- **Player:** must walk through `VaultToDockDoor` (unlocked at the top of this beat) into `DockReachPoint` (0, 1, 85) radius **5** (step 16). **Unlike the Auction Floor's `DefeatEnemies` gate, the escape leg is not gated on defeating the pursuers** — per the builder's own comment, "the leg is timed by ReachTrigger: Dock," so a player can, in principle, outrun both pursuers to the reach point without killing either, provided Mira survives whatever damage she takes along the way.
- **`ChapterOutro`:** at (0, 1, 85), inactive. `CampaignFlagSetter` flag `"ch2_complete"` wired to `OnActivated`; `completeCanvas` ref = the "CHAPTER 2 COMPLETE" world-space canvas at (0, 1.4, 85); `publishZoneCompleted` defaults true (same mission-complete signal every other chapter finale uses).

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 15 | Trigger: Escape Run (unlock dock route, Mira revealed, pursuers) | Unlocks `VaultToDockDoor`; `SetActive(true)` on Mira, Pursuer 1, and Pursuer 2 simultaneously |
| 16 | ReachTrigger: Dock | Gates on distance to `DockReachPoint` (0,1,85), radius 5 |
| 17 | Dialogue: Beat5 Reunion (Kessler + Iris, Mira found) | Plays `ch2_beat5_reunion` |
| 18 | Dialogue: Beat5 Mira Kept | Plays `ch2_beat5_kept` |
| 19 | Trigger: Chapter Outro (flag + fade + canvas) | Activates `ChapterOutro` — sets `ch2_complete`, reveals the complete canvas, publishes `ZoneCompleted` |

**Step 15's door-still-closed state is load-bearing for VR comfort, and the `ProximityDoor` model (§2) makes it a guarantee, not a hedge.** The same Trigger step that unlocks `VaultToDockDoor` also `SetActive(true)`s Mira and both pursuers, at the same instant, while the player is still standing back at the Vault terminal (~z=69) — three bodies pop into existence from nothing. In-headset, an instantaneous spawn like that is exactly the kind of pop-in that breaks immersion if the player can see it happen. `VaultToDockDoor` sits at z=74; the player is ~5 m away at the moment of the reveal — outside the door's 3 m `triggerRadius` — so the door is **affirmatively fully shut**, not merely "still sliding open (or hasn't started)." It stays shut and only begins to slide once the player closes to within 3 m. The three-body reveal is therefore guaranteed occluded by the door mechanism itself, not by lucky frame timing. **A refactor must preserve the proximity-open behavior — door state driven by live distance to `Camera.main`, not an open-on-unlock event fired once at step 15** — switching to open-on-unlock would let the player watch Mira and both pursuers materialize in-headset through an already-open doorway gap.

**What changes during the beat:** `VaultToDockDoor` flips locked→open; Mira, Pursuer 1, and Pursuer 2 all flip inactive→active in the same instant (step 15); the reunion and kept dialogue sets play back to back once the player reaches the dock; the outro fires last.

#### c. Art & Environment Instantiation → `BuildBeat5Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| Room shell (12×18, solid back wall) | center (0,0,83) | `Rooms.EscapeDockShell` | `…/Art/Generated/Rooms/EscapeDockShell.prefab` | **MISSING** |
| `Crate0` | (-4, 0, 80) | `Props.DockCrate` | `…/Art/Generated/Props/DockCrate.prefab` | **MISSING** |
| `Crate1` | (4, 0, 82) | `Props.DockCrate` | `…/Art/Generated/Props/DockCrate.prefab` | **MISSING** |
| `Crate2` | (-3, 0, 88) | `Props.DockCrate` | `…/Art/Generated/Props/DockCrate.prefab` | **MISSING** |
| `TieDown` | (0, 0.05, 85) | `Props.TieDown` | `…/Art/Generated/Props/TieDown.prefab` | **MISSING** |
| `StorageTarp` (+ lockers) | ~(1.5, 0.1, 78), over/beside Mira's spawn | `Props.StorageTarp` | `…/Art/Generated/Props/StorageTarp.prefab` | **MISSING** — no tarp/locker prop or builder call exists today; the Trigger-reveal staging (§b) has no diegetic anchor object either way; see note below and §9 |
| `WeaponRack` (+ bench) | ~(0, 0.5, 87), near the tie-down | `Props.WeaponRack` | `…/Art/Generated/Props/WeaponRack.prefab` | **MISSING** — no rack/bench prop or builder call exists today; see note below and §9 |
| `VaultToDockDoor` | (0, 0, 74) | `Doors.SlidingDoor_Standard` | `…/Art/Generated/Doors/SlidingDoor_Standard.prefab` | **MISSING** |
| Mira (NPC, hidden then revealed) | (1.5, 0, 77), Euler(0,180,0) — facing −Z, toward `VaultToDockDoor`/the crew's approach | `Named.Mira` | `…/Art/Generated/Characters3D/Named/Mira.prefab` | **EXISTS** |
| Pursuer ×2 | (-2,0,78), (2,0,80) | `Enemies.SyndicateEnforcer` | `…/Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** *(same additive `EnemyArtWirer` swap as Beat 2's combatants)* |
| `DockLight` | (0, 2.6, 83) | — | `ChapterEnvironmentProfile.accentLights["Dock"]` | profile |
| `DockAmbience` | (0, 2.6, 83) | — | `AudioSource`, `dock_wind.wav` (no prefab; inner radius 2, outer 8, max volume 0.4) | audio |

**Notes on the transition.** This is the only room in the chapter with an ambient bed at all (§9). **The room's true identity is the ship's hold, not a dock approaching one.** Canon's Beat 5 SETTING (line 663) opens "Interior: Kessler's ship, the hold... The crew comes up the ramp into the ship's quiet" — the boarding has already happened by the time this room's dialogue plays. The solid back wall (z=92) is therefore the **hull**, not a threshold the player is still walking toward; the crates/tie-downs `Ch2BuildDock` builds (below) are ship-hold furniture, correctly dressed for that reading even though the room's own name (`Ch2BuildDock`, `EscapeDockShell`) and this document's "Escape/Dock" label both still frame it as a dock the ship sits at rather than the ship itself. This is a one-line correction to how the room is read, not a geometry change — it also strengthens the §4a "for the first time, theirs" note: the room isn't just *near* the ship, it *is* the crew's own space now. A future art pass could add a visible hull/bulkhead treatment to reinforce the reading, but none exists today and none is required to correct the framing. The chapter's closing image — Ronin-7 laying the wrapped katana on a rack (§4a) — is likewise unstaged: `Ch2BuildDock` builds only crates and a tie-down, with no weapon-rack/bench prop, and the only katana object in the chapter is the single belt-fit `Named.Echo` spawned in the Docking Alley (2.2,0.9,-2.5) at Beat 0. A dedicated rack prop is a natural companion commission to the ship-hold reading above — and a load-bearing one, since Chapter 3's opening scene is authored against this exact room and object.

**Mira's canon hiding place — the tarp — is also unbuilt, and it's the physical object the entire discovery beat is written around.** Canon names "storage lockers" and "a fold of tarp... big enough for a small body to hide" as this room's furniture (dialogue lines 76, 663), a recurring motif called back explicitly at the moment of discovery — "Resh crosses to the storage lockers, the fold of tarp. He stands over it a beat, then pulls it back" (line 689). `Ch2BuildDock` builds only crates and a tie-down (above); there is no locker or tarp prop of any kind. This is the same class of load-bearing-but-unbuilt canon furniture as the weapon rack above — except here the gap compounds with the staging divergence already flagged under §b and §9: the shipped build reveals Mira via a Trigger rather than a tarp pull, and *neither* staging currently has its diegetic anchor prop built — the Trigger-reveal has no tarp to *not* use, and a future tarp-discovery build would have nowhere to put her.

#### d. Combat

**Two enforcer-tier `Enemy` instances** (reusing the shared `EnemyDefinition`, not the tougher Bodyguard one), activated together with Mira's reveal. Pursuer 2's targeting of `miraHealth` instead of `playerHealth` makes this the chapter's only combat encounter with a fail-state that isn't the player's own death (§b). No `DefeatEnemies` step exists for either pursuer — the encounter is a timed chase resolved by reaching `DockReachPoint`, not a mandatory kill.

**Mira never moves — this is protect-in-place, not escort.** She is static at (1.5,0,77) for the whole beat (§5); nothing in the builder relocates her toward the exit. Pursuer 2's target is `miraHealth`, and `DockReachPoint` sits 8 m further downrange at z=85. Because the leg is gated by `ReachTrigger`, not `DefeatEnemies`, a player who reflexively sprints for the reach point to end the leg is running *away* from Mira and her hunter, not toward safety — leaving Pursuer 2 free to work on her unchallenged the whole way. That spatial tension, not the chase itself, is the encounter's real decision, and it is exactly what the §8.7 `ProtectNpcObjective` fail-state check exercises.

**The activation geometry is a forward gauntlet, not a rear chase, despite "pursuer" naming.** The player enters the Dock through `VaultToDockDoor` at z=74. Mira (z=77), Pursuer 1 (z=78), and Pursuer 2 (z=80) all sit *ahead* of that entrance, between the player and `DockReachPoint` (z=85) — nothing spawns or activates behind the player, and nothing chases from the Vault. The player walks *into* the encounter, not away from it: Pursuer 1 is the first body reached and stands nearest the door; Mira sits between Pursuer 1 and Pursuer 2, flanked on both sides; Pursuer 2, the one wired to `miraHealth`, stands *between* Mira and the reach point — so a player rushing z=85 to end the leg runs *past* Mira, *past* Pursuer 2, and deeper into the room, not toward safety. As in Beat 2 §d, the reach-point placement plus the three spawn depths *is* the encounter's blocking; a builder reading only the position table could miss that this is a gauntlet the player advances through, not a threat that follows them in.

#### e. Dialogue / VO

Two dialogue sets, both advanced on **Left-Hand "Talk" (Y)**:

- **`ch2_beat5_reunion`** (`Dialogue_Beat5_Reunion`, at (0,1,86)) — 10 lines. Iris's "I forgot quiet."; Kessler and Iris face to face for the first time this chapter (no comm between them now); Resh's count coming out wrong and his discovery of Mira; Mira's first two lines naming Resh's legend and her reason for following.
- **`ch2_beat5_kept`** (`Dialogue_Beat5_Kept`, at (0,1,87)) — 11 lines. Mira's small, plain fear ("...Is the loud part over? Up there. It's loud."); Kessler's protective surprise; Resh's frame of her as "market stock that walked off the shelf on her own legs"; Kessler's refusal to send a child back to a cage; Mira's direct question to Ronin-7 and his immediate, uncalculated "No."; Resh's "That's twice today"; Kessler formally taking her in; and Ronin-7's closing line, the chapter's last: "Get some rest. Velorum can keep the rest of it."

#### f. Audio / Haptics / VR Comfort

- **No camera shake** during the escape chase — pursuit and any hits taken read through `Haptics`/`AudioDirector`/`CombatFeedbackController` only. As in Beat 2 (§f), the per-hit pulse is inherited via `BladeDamager` → `Haptics` on the rig, not authored specially for the pursuers — including Pursuer 2's `miraHealth`-targeted hits.
- `DockAmbience` (`dock_wind.wav`, inner radius 2 / outer 8, max volume 0.4) is the chapter's one localized 3D ambience layer, and it plays only in this final room.
- `DockLight` carries `behaviour: None` — warm, static, the first genuinely warm-toned light since Beat 0's cool blue Alley accent, a deliberate palette closing note (home, not market).
- `ReverbZonePlacer.AutoTagInteriorVolumes()` / `PlaceReverbZonesForInteriorVolumes()` run once at the end of the whole build, auto-tagging all seven rooms as distinct interior reverb volumes — the Dock's larger footprint (12×18) should read audibly roomier than the tighter Cells/Vault the player just walked out of.
- Comfort vignette engages normally through the escape-run traversal, likely the chapter's most snap-turn-dense stretch given two active pursuers in an open room.

## 5. Character travel-route master table

**Unlike Chapter 1, no NPC in Chapter 2 uses the `NpcWalker` + `MissionDirector` Trigger idiom.** There is not a single `BuildNpcWalker` call anywhere in `Chapter2Builder.cs`. Every named character is either a stationary spawn or a stationary spawn with a small local wander radius; nothing walks a scripted multi-waypoint path. **Kessler himself has no physical GameObject in this scene at all** — every one of his lines is voice-only (`"Kessler (Comm)"` in the raw dialogue script, though the shipped `Chapter2Lines.cs` data drops the "(Comm)" suffix and tags him simply `"Kessler"` — see §9), consistent with the story's own framing that he "stays with the ship through the Velorum legs."

| Character | Spawn position | Facing (target) | Room | Wander? | Active from build? | Notes |
|---|---|---|---|---|---|---|
| Resh | (2, 0, 15) | −Z, Euler(0,180,0) | Market Row | `StoryNpcWander` radius 0.8 m | Yes | Only NPC with any movement component; never relocates rooms |
| Velorum-Broker | (-3.5, 0, 25.6) | −Z, Euler(0,180,0) | Broker Front | none | Yes | Fully static behind his own counter |
| Iris | (1, 0, 54) | −Z, Euler(0,180,0) | Holding Cells | none | Yes | Static, caged; no repositioning on release (freed in place) |
| Mira | (1.5, 0, 77) | −Z, Euler(0,180,0) | Escape / Dock | none | **No** — `SetActive(false)` at build, revealed by step 15's Trigger | Carries `Health(40)` + `ProtectNpcObjective` (fail-state, §4 Beat 5) |
| Kessler | — | — | — | — | — | **No physical GameObject.** Voice-only via dialogue lines tagged speaker `"Kessler"` throughout Beats 0, 3, and 4 |

**All four "Facing (target)" values are the target state, not today's build.** `InstantiateNpc(prefabPath, position, name)` (`ChapterSharedBuilders.cs:648`) sets position only, so as built every one of these four NPCs sits at prefab-identity rotation (+Z) — facing away from the player, who approaches each of them from -Z along the corridor (§2). See §4 Beat 1c for the full analysis and the refactor requirement (bake a −Z-facing forward into the prefab, or have the builder/registry instantiation set rotation explicitly).

**Y-invariant note:** because no waypoint math exists in this chapter, the Chapter 1-style "every waypoint Y must equal `floorY`" hazard does not apply here. All seven rooms share one continuous flat floor at y=0 (confirmed by every `BuildFloorCeiling` call in `Chapter2Builder.cs` using `y=0` uniformly), so `FitNamedCharacter`'s per-character grounding never has to account for a room-to-room floor offset. **This is a structural difference from Chapter 1 worth preserving during the refactor** — if a future beat ever adds scripted NPC travel to this chapter, it inherits Chapter 1's Y-invariant discipline (§5 of `Ch01-Scene-Construction.md`) from day one, not as an afterthought.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Room(s) | Mood | Accent entry(ies) | Behaviour | What changes during the beat |
|---|---|---|---|---|---|
| 0 — Briefing | Docking Alley | cool, quiet, undressed as "ship interior" | `accentLights["Alley"]` | `None` | none — pure dialogue over static geometry |
| 1 — Undermarket, Broker | Market Row, Broker Front | loud neon (magenta/cyan) tightening into a paranoid, flickering green | `accentLights["Market0"]`, `["Market1"]`, `["Broker"]` | `Market0`/`Market1`: `None`; `Broker`: `ConsoleFlicker(seed 22)` | none scripted — the flicker runs continuously from build, not triggered mid-beat |
| 2 — Resh, Auction Floor | Market Row (dialogue), Auction Floor (combat) | hard white stage light + cool overseer-box blue | `accentLights["Auction0"]`, `["Auction1"]` | both `None` | **Trigger (step 9):** `AuctionToCellsDoor` unlocks the instant `DefeatEnemies` (step 8) resolves — no lighting event accompanies it |
| 3 — Iris Freed | Holding Cells | dim, wet, teal-green | `accentLights["Cells"]` | `None` | none — Iris's release is a state change on her own NPC, not a lighting event |
| 4 — The Reveal | Records Vault | cold, sealed, cyan terminal glow against dark racks | `accentLights["Vault"]` | `AmbientPulse(period 6.5s)` | none triggered — the pulse runs continuously through the whole dialogue |
| 5 — Escape, Reunion, Kept | Escape / Dock | warm, settling, the chapter's one homecoming palette | `accentLights["Dock"]` | `None` | **Trigger (step 15):** Mira + two pursuers activate together, no lighting change; **Trigger (step 19):** `ChapterOutro` fades to black post-dialogue |

**`AuctionLight0`'s dual role is a lighting-pass trap worth naming before it's split.** The single fixture at (0,3.2,39) is currently both the "hard light" on the lot (SETTING line 50, "slaughter-bright auction lamps," §3) and the Beat 2 fight's combat key light (§4 Beat 2f) — one accent light, two readings, fused today. A future art pass adding a tighter, block-only spot to match the lot pedestal/cage ring (§4 Beat 2c) should not remove `AuctionLight0`'s room-wide coverage in the process, or the fight loses its key light — split the readings into two fixtures, don't replace one with the other.

Fog is the same baseline exponential bed in every beat — a single profile value (`(0.18, 0.14, 0.10)`, density `0.022`), never overridden per-room, giving the whole chapter its "grimy amber undermarket haze" per the builder's own inline comment.

## 7. Audio / VO manifest cross-reference

Nine canonical dialogue sets, defined in `Chapter2Lines.cs` and consumed via `Chapter2Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position | Lines |
|---|---|---|---|
| `ch2_beat0_briefing` | 0 | (0, 1, 1) — `Dialogue_Beat0_Briefing` | 11 |
| `ch2_beat1_undermarket` | 1 | (0, 1, 8) — `Dialogue_Beat1_Undermarket` | 11 |
| `ch2_beat2_resh_confront` | 2 | (2, 1, 14) — `Dialogue_Beat2_ReshConfront` | 16 |
| `ch2_beat2_resh_recruit` | 2 | (2, 1, 16) — `Dialogue_Beat2_ReshRecruit` | 11 |
| `ch2_beat1_broker` | 1 | (-3, 1, 26) — `Dialogue_Beat1_Broker` | 9 |
| `ch2_beat3_iris` | 3 | (1, 1, 56) — `Dialogue_Beat3_Iris` | 16 |
| `ch2_beat4_reveal` | 4 | (0, 1, 69) — `Dialogue_Beat4_Reveal` | 19 |
| `ch2_beat5_reunion` | 5 | (0, 1, 86) — `Dialogue_Beat5_Reunion` | 10 |
| `ch2_beat5_kept` | 5 | (0, 1, 87) — `Dialogue_Beat5_Kept` | 11 |
| **Total** | | | **114** |

Each is built by the local `Ch2BuildDialogue` wrapper (the chapter's own analogue of `BuildChapter1Dialogue`): it calls the shared `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch2WireVoiceClips`, resolving each line's `AudioClip` from `Chapter2Lines.ClipName(setId, index, speaker)` — pattern `ch2_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. Because `setId` already begins with `ch2_` (e.g. `ch2_beat0_briefing`), the resulting filenames carry a doubled prefix — `ch2_ch2_beat0_briefing_00_kessler.mp3` — which is exactly what ships on disk; this is a naming quirk, not a bug, and any manual clip authoring must match it exactly or the resolver will silently fall through to a `LogWarning`.

**VO coverage is complete as of this writing.** All **114/114** lines across the nine sets have a matching `.mp3` file under `Assets/Ronin7/Art/Generated/Audio/Voice` — every `ch2_ch2_*` clip resolves, so a fresh build produces zero `Ch2WireVoiceClips` warnings. This is a meaningfully better starting position than Chapter 1's VO pass was at the equivalent point in that chapter's own document.

**Advance input for every dialogue line and the Cut-Iris prompt is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all nine `DialoguePlayer`s and the one `PromptInputAdvancer`, matching Chapter 1's convention exactly.

**Dialogue is data, not art.** None of this changes in the refactor — the nine set ids, their positions, and the clip-resolution pattern are canon.

SFX / ambience bed, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip | Used for |
|---|---|
| `DoorSlide.wav` | both real sliding doors (`AuctionToCellsDoor`, `VaultToDockDoor`), via `WireDoorAudio` |
| `SFX/dock_wind.wav` | the chapter's one 3D ambience layer, `DockAmbience`, in the Escape/Dock room only |

**No 2D ambient bed plays anywhere in this chapter.** Chapter 1 wired a continuous `OnFootAmbience.wav` loop on its `Game` root, audible in every room. Chapter 2's `Game` root carries only `GameState` and `CombatFeedbackController` — no `AudioSource`, no equivalent loop. Six of the chapter's seven rooms (everything but the Dock) currently play in total ambient silence outside of dialogue and door SFX. Canon specifies this isn't a flat gap but a three-stage progression — full auction roar, muffled "pressure overhead" through the Cells, gone by the Vault — detailed in §9. Flagged there as a gap, not fixed here.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 02 — The Auction** (`XRRigBuilder.BuildChapter2Auction()`).
2. **EditMode is the gate.** Baseline is **842 tests green, 0 skips**; PlayMode is **70/70 green**. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot.** **No EditMode test invokes `BuildChapter2Auction()` or loads `Ch02_Auction.unity`.** The only test file touching this chapter's data is `Chapter2LinesTests.cs`, which validates `Chapter2Lines`' dialogue-set contents (every set ID non-empty, every speaker in a known-cast allowlist, the builder-referenced set-ID list kept in sync by hand against `Chapter2Lines.SetIds`) — it never opens the scene or exercises the builder. **A green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it. Unlike Chapter 1, there is also **no `Chapter2BuilderTests.cs`** analogous to `Chapter1BuilderTests.cs` — there is no waypoint-math regression coverage to preserve here (§5 explains why: no `NpcWalker` legs exist to test), but if a future patch *adds* one, it should ship with the same kind of floor-invariant test Chapter 1 has.
3. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative. This chapter has a stronger pre-existing signal to check against: also verify that `EnemyArtWirer`'s `Coil_Syndicate_Ganger` visual swap and `CrowdArtWirer`'s `Vesh`/`Voll` crowd placement **survive** a fresh rebuild once the safe zone is real — today they do not (§1.4).
4. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty room, never an exception. The four already-resolved Named-character keys and the `Enemies.SyndicateEnforcer` key (Appendix B) should NOT log a warning even on an otherwise-empty registry, since they resolve independently of the registry today (direct `AssetDatabase.LoadAssetAtPath` calls and the additive wirers, respectively) — confirm the eventual registry migration doesn't regress that.
5. **Perf reference bar — not yet captured for this chapter.** §1.6 flags this as a prerequisite: run edit-mode `UnityStats` on a fresh `Ch02_Auction` build and record `drawCalls`/`setPassCalls`/`tris`/`verts` in `Project/Docs/CHAPTER-BUILD-LEDGER.md` alongside Chapter 1's existing bar before any prefab lands. Re-measure after every prefab lands. Prefabs carry their own materials and will not `TintShared`-batch; a swap that meaningfully exceeds whatever bar gets set must be investigated before shipping. The 72 Hz floor is not negotiable.
6. **Console check:** `Ch2WireVoiceClips`'s per-set warning (`only N/M voice clips resolved`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild. As of this writing it should print nothing (§7 — 114/114 resolved).
7. **`ProtectNpcObjective` fail-state check (new, chapter-specific).** In a playmode pass, deliberately let Pursuer 2 kill Mira after step 15 activates her, and confirm `EntityDied` fires for the player rig GameObject and `GameFlowManager` ends the run — this is a real, wired fail-state (§4 Beat 5, §5), not decoration, and it is the one mechanic in this chapter with no EditMode coverage at all.
8. **Decorative-figure/combatant tag check (new, only relevant once the auction-block lot figure or gallery crowd is built, §4 Beat 2c/§9).** Confirm any added Auction Floor decorative figures (block lot + gallery) are `PlaceDecorativeCrowd`-style non-combatants and do **not** register a `Health` component in the `DefeatEnemies` set that step 8 waits on (§4 Beat 2b) — a decorative figure accidentally given the enemy tag would deadlock the fight's completion gate, since that step waits for *every* `Health` in the set to reach zero.

## 9. Additive-only cautions & open questions

- **The additive-patch rule, and its one exception.** Re-running `BuildChapter2Auction()` wipes generated content, **including the work of `EnemyArtWirer`, `CrowdArtWirer`, and `ChapterRoomDetailsVarietyWirer`**, all three of which currently target `Ch02_Auction.unity` by name and must be re-run after any rebuild to restore their art. The house rule remains: patch additively in the live editor, or fix `Chapter2Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree (and, ideally, the three wirers' output folded into it) is the sanctioned place for hand-tuned art that must survive a rebuild.
- **Do not auto-delete orphan materials.** Same standing rule as every other chapter — reversible cleanup only, via `Editor/Art/ArtGenerationMenu`.
- **Reject any prefab import that introduces a `MeshCollider`.** Room shells and props get primitive colliders, matching the project-wide 2026-07-04 MeshCollider-free audit.
- **Room order vs. dialogue order mismatch (flagged, not fixed).** Physically, the mission spine visits Market Row → Broker Front → Auction Floor in that order (§2), but the *dialogue* order is Market Row (Undermarket) → Market Row again (Resh confront/recruit) → Broker Front (the refusal, which explicitly references Resh already having been recruited) → Auction Floor (combat). The player therefore walks *past* Broker Front once during Beat 1's Undermarket dialogue, gets pulled aside for the entire Resh subplot without a room change, and only *then* proceeds forward into Broker Front to hear a refusal that has already been narratively obsoleted by Resh's recruitment. This is coherent as authored (Kessler's Broker Front line literally says "Resh already gave us the door") but means the Broker Front room is visited only *after* its narrative purpose (motivating the search for Resh) has already been served by dialogue that happened one room upstream. Do not silently reorder the mission steps to "fix" this without a writers'-room call — the dialogue data is internally consistent with the order as shipped.
- **Beat 0's rig-interior collapse (the consequence flagged in §3).** Canon stages the briefing inside Kessler's salvage rig — "a single battered console lit inside a cold, half-powered hull, the wreck-field still turning slow in the viewport" (Beat 0 SETTING block), a direct visual echo of Chapter 1's Main Hold. As built, no distinct rig-interior geometry exists: the dialogue plays with the player standing inside the Docking Alley, sharing its cool-blue `AlleyLight` accent and neutral room-detail tint with the neon entry tier of Velorum the player walks into moments later. The wreck-field-viewport callback — the chapter's only quiet, and its one sensory link back to Ch1's dead-ship setting — is therefore lost to the collapse. The SETTING's two named objects name their own low-cost fix without touching mission logic: a `Props.NavConsole` (the "battered console") and a `VFX/WreckfieldViewport` backdrop (the "wreck-field still turning slow in the viewport") — a future art pass would have a named target instead of prose, the same way Appendix B names `Props.WeaponRack` as the concrete unbuilt anchor for the Ch3 dependency. Flagged for design awareness; not proposing that either be built now.
- **The overseer's box has no voice and no figure — and the missing PA line is also the shipped `DefeatEnemies` fight's missing motivation, not just its missing texture.** As flagged in Beat 2 §f, canon gives Velorum's overseer a calm house-PA line that answers the Auction Floor brawl ("when the floor's order is disturbed, that voice comes over the house PA — never raised, never hurried"), an explicit Handler-analogue, plus "a still figure behind" the box's glass. `Ch2BuildAuctionSet` builds the box as an empty primitive with no `AudioSource`, no VO line, and no occupant NPC. Of everything this document's ledger tracks as unbuilt canon imagery, this is the one with the clearest atmospheric payoff — it is the chapter's signature diegetic-audio device, and it is currently silent. It is also load-bearing in a way the others aren't: step 8's five-enemy fight fires with zero dialogue lead-in in the shipped talked-down path (§4 Beat 2d) — canon's version of this fight is Resh's bought muscle answering *his* signal, a cause that belongs to the unbuilt brawl fork, not the shipped one — so the overseer's PA line is the natural, in-scope-adjacent way to give the shipped fight a reason to start at all.
- **The auction caller's pulpit is unbuilt, sibling to the overseer's box above.** SETTING Beat 2 names "a caller's pulpit" among the Auction Floor's furniture and dialogue line 347 has "the auction caller drones on, selling" underneath Resh's recruitment — a second, distinct diegetic voice from the overseer's PA, running continuously rather than reacting to an event. `Ch2BuildAuctionSet` has no pulpit prop, no caller NPC, and no caller VO of any kind (§4 Beat 2 §c/§f). After the overseer's PA, this is the richest unbuilt atmospheric element in the chapter's biggest room, and a caller-drone loop is the single best candidate source for the Market/Auction ambient bed the chapter is otherwise missing entirely (below).
- **The auction block has no lot figure — the chapter's thesis has no visible subject-of-sale.** `Ch2BuildAuctionSet` rings `Auction_LotPedestal` with four `Auction_CageBar`s and places nothing inside them: the centerpiece prop of the chapter's biggest, most-watched room stages an auction with nothing being auctioned, even though canon defines this whole world as one that "sells people and pretends it doesn't" (§3, line 22–23) and stages the crowd itself as "a sea of buyers **and sold**" (line 14). Distinct from the gallery-crowd gap below — that's the missing *buyers*, this is the missing *sold* — and the single strongest uncovered image this document tracks: one decorative, non-combatant figure from the already-resolved `Diversity.VelorumCrowd` art, placed inside the cage ring at ~(0, 0, 39) (§4 Beat 2c), fixes it for the cost of one instantiation. See §8 item 8 for the completion-gate caution that applies once this or the gallery crowd below is built.
- **The Auction Floor's gallery crowd — the room's single defining image — is unbuilt, and it is the biggest of the room's three atmosphere gaps.** SETTING Beat 2 stages "a packed gallery of bidders" where "the noise is a physical thing" (line 267); `PlaceDecorativeCrowd` is called exactly once, chapter-wide, for Market Row (`Chapter2Builder.cs:109`), and `CrowdArtWirer`'s scene map resolves `Diversity.VelorumCrowd` only into Market Row — none of it reaches the Auction Floor. The chapter's biggest (18×18), most-watched room therefore stages its combat as an empty amphitheater: no gallery ringing the pit, in addition to the already-flagged missing overseer's figure/PA and caller's pulpit above. Larger and more obvious than either of those two — an empty "auction" reads as a rehearsal, not a market. A second, additive `PlaceDecorativeCrowd` call (tiered/perimeter positions around the pit) using the already-resolved `Diversity.VelorumCrowd` art is the concrete, in-scope-adjacent fix. Flagged for design awareness; not proposing which positions, exactly, to build out.
- **Market Row's crowd density is thin against canon too, though it isn't as stark a gap as the Auction Floor's.** SETTING Beat 1 (dialogue lines 41–43) stages the room as "choke-narrow lanes… A river of people… Built to make a newcomer small" — six `PlaceDecorativeCrowd` figures in a 14×16 room read as a light scatter, not a river, and Ronin-7's "no sightlines. No exits I'd trust" (§4 Beat 1a) plays over an open lane rather than a pressing crowd. Same fix in kind as the Auction Floor's: denser perimeter/lane-choke placement using the already-resolved `Diversity.VelorumCrowd` art (§4 Beat 1c). Flagged for design awareness; not proposing exact positions here either.
- **The Auction-floor brawl-vs-bid branch is unbuilt.** The dialogue script and the Game Narrative Design section both describe a player choice — fight Resh's bought muscle or talk him down — that converges on the same mercy beat either way. `Chapter2Builder.cs` implements only the talked-down path, as a single linear dialogue set with no combat gate and no player choice UI. Canon stages the branch's economy path with its own furniture too — "cover and chokes for a brawl OR a bidder's rail and a paddle for the economy path" (dialogue line 54) — and neither the branch logic nor these art fixtures exist today: unlike its sibling fixture the `AuctionCallerPulpit`, which already carries an Appendix B ledger row (§4 Beat 2c), no `Props.BidderRail`/paddle key is tracked anywhere in this document. Flagged for design awareness; not proposing which branch (if either) should be built out.
- **The broker's Bodyguard mini-boss is condensed into the Auction Floor fight, not staged guarding the Vault terminal.** The builder's own comment states this is a deliberate condensation, not an oversight, but it means the Records Vault beat (Beat 4) plays with zero opposition despite the dialogue script's explicit "PLAYABLE COMBAT — MINI-BOSS: THE BROKER'S BODYGUARD... a single heavy, silent opponent fought in the close confines of the vault, around the racks of the dead" staging. If a future pass wants the Bodyguard back in the Vault, it needs its own spawn/activation logic separated out of the Auction Floor's `DefeatEnemies` step, and the Bodyguard currently shares its `Coil_Syndicate_Ganger` visual with every other syndicate combatant in the chapter — a dedicated silhouette would be a natural companion change.
- **Mira's reveal is a timed escape-chase Trigger, not "found hiding under a tarp."** The dialogue script stages Resh discovering her by pulling back a fold of tarp near the storage lockers, *after* the Kessler/Iris reunion has already begun to play out face to face. The shipped build instead reveals her — and activates two pursuing enemies simultaneously — the moment step 15's Trigger fires, *before* the player has even reached the Dock room, let alone before the reunion dialogue starts. Mechanically this converts a quiet discovery beat into a combat/protect-in-place beat — not an escort: Mira never moves, and the real tension is that `DockReachPoint` sits 8 m past her static spawn, so rushing the exit leaves Pursuer 2 unchallenged on her (§4 Beat 5). This is a meaningful staging divergence worth a deliberate call before art or additional VO gets built against either version. **Neither staging currently has its diegetic anchor prop built either:** the tarp/storage-lockers canon writes the discovery around (`Props.StorageTarp`, Appendix B) don't exist in the builder at all — so a future decision to keep the Trigger-reveal still leaves the room's furniture missing, and a future decision to build the tarp-discovery instead has no prop to stage it on (§4 Beat 5 §c).
- **The chapter's closing weapon rack is a cross-chapter load-bearing dependency, and it is unbuilt.** The chapter closes on Ronin-7 laying the wrapped katana "on the rack by the bench" (dialogue line 773), and Chapter 3 opens on "the exact room and object" — "Ronin-7 reaches for the katana on the rack, and it wakes." `Ch2BuildDock` builds only crates and a tie-down (§4 Beat 5 §c); there is no rack or bench prop anywhere in the chapter, and the only katana object that exists is the single belt-fit `Named.Echo` spawned in the Docking Alley at Beat 0. Unlike most of this ledger's gaps, which are atmosphere the chapter can ship without, this one is structural: without `Props.WeaponRack` (Appendix B), Chapter 3's opening scene has no object to open on. This is more load-bearing than several other items on this list and should be prioritized accordingly.
- **The player-embodiment gap: two stage directions the ledger flags individually but doesn't name as a pattern.** This document consistently marks third-party VO-only actions as *inferred: no engine object backs this image* (the pickpocket, §4 Beat 1d; the gallery plant, §4 Beat 2e; the collar-tag cut and the enforcer challenge, §4 Beat 3d). Two stage directions belong to the same class but touch the *player character's own body* rather than a third party's: Kessler catching Ronin-7's forearm at the Broker Front grille (line 224, §4 Beat 1f) and Ronin-7's own hand rising to the scar at his throat during the reveal (line 589, §4 Beat 4a/f). Both are unstageable twice over — Kessler has no physical `GameObject` in this scene (§5) and the player rig has no visible body or arms (§2, head + two hands only) — which is *why* they were never candidates for an engine object in the first place, unlike the third-party beats above, which merely lack one today. Naming that here completes the ledger's own logic: these two are permanently VO/performance-only, not pending props, for any future writer or artist reading this document.
- **Kessler's "(Comm)" tag is dropped in the shipped dialogue data.** The raw script consistently labels his Beats 0/3/4 lines `"Kessler (Comm)"` to distinguish voice-only radio dialogue from any future in-person Kessler line. `Chapter2Lines.cs` tags every one of his lines simply `"Kessler"`, with no mechanism distinguishing "voiced over comm" from "physically present" — since he is never physically present in this scene at all (§5), the distinction happens to not matter *yet*, but if a later patch ever gives Kessler a body in this scene (unlikely, given the story bible has him staying with the ship through Ch2), the speaker-tag collision would need resolving first, the same way Chapter 1 handled Khall's `"Handler (Hologram)"` vs. `"Khall"` split.
- **No ambient bed in six of seven rooms — and canon specifies a concrete three-stage progression for it, not just a generic "add crowd noise" gap.** §4 and §7 both flag the missing bed itself: only the Escape/Dock room has a wired `AudioSource` ambience layer (`DockAmbience`, `dock_wind.wav`). But the dialogue script's SETTING blocks stage the market/auction roar as a deliberate *dramatic arc*, not a flat loop: full roar on the Auction Floor — "the noise is a physical thing" (line 267) — **muffles to "a pressure overhead"** in the maintenance-crawl leg the Holding Cells beat plays out ("the auction roar reduced to a pressure overhead," line 401) — then **"is gone entirely"** by the Records Vault ("the auction roar is gone entirely," line 523). Docking Alley, Market Row, Broker Front, the Auction Floor, Holding Cells, and the Records Vault currently play in total silence outside of dialogue and the two door-slide one-shots — a market chapter explicitly built around "noise, commerce, and a machine that eats people" arguably needs an audible crowd/market bed more than any other chapter in the game, and currently has less ambient audio than Chapter 1's near-silent dead ship. The concrete spec: one looping crowd/caller `AudioSource` bed, full volume on the Auction Floor, low-pass-filtered and attenuated through the Cells, silenced by the Vault — which ties directly to §4 Beat 2 §f's own suggestion that the (also unbuilt) caller-drone loop at `AuctionCallerPulpit` is the single best source for that bed, and gives the descent's "market's undertow" theme (§3) an audible spine. **Canon also stages the roar rising on the approach, not just falling on the way out** — line 261: "They start down toward the auction lamps, the roar of the floor rising up the tiers to meet them." As built, Market Row and Broker Front are silent, so the player currently walks toward the Auction Floor in silence and the roar only appears on arrival. The same single looping source, attenuated *upward* toward the pit as the player advances through Market Row and Broker Front rather than only downward past the Auction Floor, would supply the approach crescendo canon stages alongside the existing downward falloff into the Cells and Vault. **A third use of the same ducking device sits independent of any room transition:** line 316's CONVERGENCE stage direction ("Lighting tightens to the two of them; the floor noise drops back") marks the mercy beat itself — Resh's "Go on, then… finish it" and Ronin-7's "Go." — and has no bed to duck today for the same reason Market Row has none at all (§4 Beat 2f). Flagged as a clear polish gap, not fixed here.
- **Both flagged audit items from `audit/Ch02_audit.md` are already resolved in shipped data.** The "the kid and" continuity error (§3, Beat 4) and the corrupted convergence-block text (§3, Beat 2) are both explicitly fixed in `Chapter2Lines.cs`'s own header comment and confirmed against the live line data in §4 above. Unlike Chapter 1's still-open Beat 4 canon soft spot, there is no unresolved audit item to carry forward for this chapter.
- **The katana's shadow-AI nature stays dormant/unrevealed in Ch2.** The audit's consistency check (§3, "Consistency checks PASSED") explicitly confirms the blade stays wrapped and silent throughout, with no AI/Echo/voice or memory-vessel language anywhere in this chapter's lines. `Named.Echo` is placed purely as a belt-fit prop (§4 Beat 0), same treatment as Chapter 1. Any future patch that adds shadow-AI behavior or dialogue to the Ch2 blade would break this canon boundary — it wakes only in Chapter 3. **Note this against `EchoPresence` on the rig (§2):** `BuildRig` attaches `EchoPresence` — "ambient shadow-AI callouts" — chapter-wide, the same additive, non-vocal component Chapter 1 carries pre-awakening. It does not constitute the blade "waking" as built; flagged here only so a future reader doesn't mistake its presence for a contradiction of this boundary, and to verify it stays non-vocal if it's ever extended.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch2Environment.asset`. Currently set inline at the top of `BuildChapter2Auction` (`Chapter2Builder.cs:59–84`).

| | Value |
|---|---|
| Directional key | color (0.9, 0.75, 0.55), intensity 0.5, rotation Euler(55, -40, 0) |
| Ambient | mode **Flat**, color (0.12, 0.10, 0.09) |
| Fog | mode **Exponential**, color (0.18, 0.14, 0.10), density 0.022 |
| Shared floor tint (Alley, Market, BrokerFront, Auction, Dock) | (0.16, 0.14, 0.13) |
| Shared ceiling tint (all rooms) | (0.08, 0.07, 0.07) |
| Cells floor tint *(unique, darker/wetter)* | (0.10, 0.11, 0.12) |
| Vault floor tint *(unique)* | (0.10, 0.10, 0.12) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour | Read |
|---|---|---|---|---|---|---|
| `AlleyLight` | (0, 2.6, 0) | (0.6, 0.75, 0.9) | 1.6 | 12 | none | cool, undressed entry |
| `MarketLight0` | (-3, 2.8, 10) | (1, 0.25, 0.7) | 2 | 14 | none | magenta market neon |
| `MarketLight1` | (3, 2.8, 16) | (0.2, 0.9, 1) | 2 | 14 | none | cyan market neon |
| `BrokerLight` | (0, 2.6, 25) | (0.3, 0.8, 0.5) | 1.4 | 10 | `AddConsoleFlicker(seed: 22f)` | paranoid green flicker |
| `AuctionLight0` | (0, 3.2, 39) | white | 3 | 22 | none | hard stage light |
| `AuctionLight1` | (-7, 3, 44) | (0.3, 0.6, 1) | 1.8 | 14 | none | overseer's box |
| `CellsLight` | (0, 2.4, 55) | (0.25, 0.55, 0.5) | 1.2 | 12 | none | dim teal cells |
| `VaultLight` | (0, 2.8, 68) | (0.6, 0.9, 1) | 1.8 | 12 | `AddAmbientPulse(periodSeconds: 6.5f)` | cold vault pulse |
| `DockLight` | (0, 2.6, 83) | (1, 0.82, 0.6) | 2 | 16 | none | warm homecoming |

**Event lights:** none. Unlike Chapter 1's `DockingAlarmLight`, this chapter has no `startsInactive` event light in the current build.

### A.2 Beat 0 — Docking Alley

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-4,4], z[-4,4], center (0,0,0), 8×8 | `Chapter2Builder` room-build block |
| Walls | `Alley_WallW` x=-4, `Alley_WallE` x=4, `Alley_WallFront` z=-4 (solid), doorway wall z=4 (2.4 m gap, no door) | `BuildWall` / `BuildDoorwayWall` |
| Room details | crate/console/pipe scatter, accent tint (0.35, 0.32, 0.3) | `BuildRoomDetails(interior, "Alley", (0,0,0), (4,4), …)` |
| Accent light | (0,2.6,0), (0.6,0.75,0.9), i1.6, r12 | `BuildAccentPointLight("AlleyLight", …)` |
| Player rig | spawns at world origin, no explicit position set | `BuildRig(refs, addLocomotion:true)` |
| Katana "Echo" | (2.2, 0.9, -2.5), rot Euler(0,90,0) | `BuildSword(pos, rot, weapon, Ch1EchoBladePrefab)` |
| Dialogue player | (0,1,1), set `ch2_beat0_briefing` | `Ch2BuildDialogue("Dialogue_Beat0_Briefing", …)` |
| Mission step | index 0 of 20 | `AuthorDialogueStep(steps, 0, "Beat0: Briefing (aboard the rig)", dlgBriefing)` |

### A.3 Beat 1 — Market Row

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-7,7], z[4,20], center (0,0,12), 14×16 | `Chapter2Builder` room-build block |
| Doorway walls | front z=4 width14 gap2.4 (open); back z=20 width14 gap2.4 (open) | `BuildDoorwayWall` |
| Side walls | `Market_WallW` x=-7, `Market_WallE` x=7, size (0.2, RoomH, 16) | `BuildWall` |
| Room details | accent tint (0.5, 0.2, 0.35) | `BuildRoomDetails(interior, "Market", (0,0,12), (7,8), …)` |
| Stalls | 4× `Stall_Counter` (1.6,0.9,0.8) color (0.4,0.18,0.12) + `Stall_Canopy` (1.8,0.1,1.0) color (0.6,0.15,0.4), at (-5.5,0,8), (5.5,0,10), (-5.5,0,16), (5.5,0,18) | `Ch2BuildMarketStalls(interior)` |
| Decorative crowd | 6 positions: (-4,0,7), (4,0,8), (-5,0,12), (5,0,13), (-3,0,18), (3,0,17) | `PlaceDecorativeCrowd(new[]{…})` |
| Accent lights | `MarketLight0` (-3,2.8,10) magenta i2 r14; `MarketLight1` (3,2.8,16) cyan i2 r14 | `BuildAccentPointLight` ×2 |
| Resh | spawn (2,0,15); `StoryNpc` "Resh"; `StoryNpcWander` radius 0.8 | `InstantiateNpc(Ch2ReshPrefab, …)`, `FitNamedCharacter` |
| Reach point | `MarketReachPoint` (0,1,12), radius 5 | `AuthorReachStep(steps, 1, "ReachTrigger: Market Row", …)` |
| Dialogue players | `Dialogue_Beat1_Undermarket` (0,1,8) set `ch2_beat1_undermarket`; `Dialogue_Beat2_ReshConfront` (2,1,14) set `ch2_beat2_resh_confront`; `Dialogue_Beat2_ReshRecruit` (2,1,16) set `ch2_beat2_resh_recruit` | `Ch2BuildDialogue` ×3 |
| Mission steps | indices 1–4 of 20 | `AuthorReachStep`, `AuthorDialogueStep` ×3 |

### A.4 Beat 1 — Broker Front

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-5,5], z[20,30], center (0,0,25), 10×10 | `Chapter2Builder` room-build block |
| Doorway walls | front z=20 width10 gap2.4 (open); back z=30 width10 gap2.4 (open) | `BuildDoorwayWall` |
| Side walls | `BrokerFront_WallW` x=-5, `BrokerFront_WallE` x=5, size (0.2, RoomH, 10) | `BuildWall` |
| Room details | accent tint (0.25, 0.45, 0.3) | `BuildRoomDetails(interior, "BrokerFront", (0,0,25), (5,5), …)` |
| Stall dressing | `Broker_Counter` (-3.5,0.5,25) scale (1.6,1,0.5) color (0.15,0.16,0.18); `Broker_Glass` (-3.5,1.4,25) scale (1.6,0.8,0.05) color (0.3,0.5,0.45) | `Ch2BuildBrokerStall(interior)` |
| Accent light | `BrokerLight` (0,2.6,25), (0.3,0.8,0.5), i1.4, r10, `ConsoleFlicker(seed:22)` | `BuildAccentPointLight` + `AddConsoleFlicker` |
| Broker | spawn (-3.5,0,25.6); `StoryNpc` "Broker"; no wander | `InstantiateNpc(Ch2BrokerPrefab, …)`, `FitNamedCharacter` |
| Reach point | `BrokerReachPoint` (0,1,25), radius 4.5 | `AuthorReachStep(steps, 5, "ReachTrigger: Broker Front", …)` |
| Dialogue player | `Dialogue_Beat1_Broker` (-3,1,26), set `ch2_beat1_broker` | `Ch2BuildDialogue` |
| Mission steps | indices 5–6 of 20 | `AuthorReachStep`, `AuthorDialogueStep` |

### A.5 Beat 2 — Auction Floor

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-9,9], z[30,48], center (0,0,39), 18×18 | `Chapter2Builder` room-build block |
| Doorway walls | front z=30 width18 gap2.4 (open); back z=48 width18 gap2.4 (`AuctionToCellsDoor` sits here) | `BuildDoorwayWall` |
| Side walls | `Auction_WallW` x=-9, `Auction_WallE` x=9, size (0.2, RoomH, 18) | `BuildWall` |
| Room details | accent tint (0.5, 0.45, 0.15) | `BuildRoomDetails(interior, "Auction", (0,0,39), (9,9), …)` |
| Stage | `Auction_Stage` (0,0.25,39) scale (6,0.5,6) color (0.45,0.4,0.15) | `Ch2BuildAuctionSet` |
| Lot pedestal | `Auction_LotPedestal` (cylinder) (0,0.9,39) scale (1.2,0.5,1.2), tint (0.2,0.2,0.22) | `Ch2BuildAuctionSet` |
| Cage bars | ×4, ring radius 1.1 around (0,1.6,39), scale (0.06,1.4,0.06), color (0.2,0.2,0.22) | `Ch2BuildAuctionSet` |
| Overseer's box | (-8.5,2.8,44) scale (1.2,1.4,2) color (0.15,0.2,0.3) | `Ch2BuildAuctionSet` |
| Accent lights | `AuctionLight0` (0,3.2,39) white i3 r22; `AuctionLight1` (-7,3,44) (0.3,0.6,1) i1.8 r14 | `BuildAccentPointLight` ×2 |
| Enforcers ×4 | (-3,0,34), (3,0,34), (-4,0,40), (4,0,40); inactive; `EnsureEnemyDefinition()` (`Bandit.asset`) | `BuildEnemy(pos, playerHealth, enemyDef)` |
| Bodyguard | (0,0,44); inactive; `Ch2EnsureBodyguardDefinition()` (`Ch2Bodyguard.asset`: maxHealth 220, damage 22, moveSpeed 1.1, attackCooldown 1) | `BuildEnemy(pos, playerHealth, bodyguardDef)` |
| `AuctionToCellsDoor` | (0,0,48), width 2.4, `startLocked:true` | `BuildSlidingDoor` + `WireDoorAudio` |
| Reach point | `AuctionReachPoint` (0,1,39), radius 5 | `AuthorReachStep(steps, 7, "ReachTrigger: Auction Floor", …)` |
| Mission steps | indices 3–4 (dialogue, in Market Row) and 7–9 of 20 | `AuthorDialogueStep` ×2, `AuthorReachStep`, `AuthorDefeatStep`, `AuthorTriggerStep` |

### A.6 Beat 3 — Holding Cells

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-6,6], z[48,62], center (0,0,55), 12×14 | `Chapter2Builder` room-build block |
| Floor/ceiling tint | (0.1, 0.11, 0.12) / (0.08, 0.07, 0.07) — unique floor tint | `BuildFloorCeiling(interior, "Cells", …)` |
| Doorway walls | front z=48 width12 gap2.4 (`AuctionToCellsDoor`); back z=62 width12 gap2.4 (open) | `BuildDoorwayWall` |
| Side walls | `Cells_WallW` x=-6, `Cells_WallE` x=6, size (0.2, RoomH, 14) | `BuildWall` |
| Room details | accent tint (0.2, 0.3, 0.32) | `BuildRoomDetails(interior, "Cells", (0,0,55), (6,7), …)` |
| Cage clusters ×4 | (-3.5,0,51), (3.5,0,51), (-3.5,0,58), (3.5,0,58); each 4 bars offset x -0.6..+0.6 step 0.4, y1.1, scale (0.05,2.2,0.05), color (0.18,0.2,0.22) | `Ch2BuildCells(interior)` |
| Accent light | `CellsLight` (0,2.4,55), (0.25,0.55,0.5), i1.2, r12 | `BuildAccentPointLight` |
| Iris | spawn (1,0,54); `StoryNpc` "Iris"; no wander | `InstantiateNpc(Ch2IrisPrefab, …)`, `FitNamedCharacter` |
| Cut-Iris prompt | (1, 1.4, 54), text "Cut Iris Free  (Y)"; inactive | `Ch2BuildPrompt(…)` → `TextMesh` + `PromptInputAdvancer` |
| Reach point | `CellsReachPoint` (0,1,55), radius 5 | `AuthorReachStep(steps, 10, "ReachTrigger: Holding Cells", …)` |
| Dialogue player | `Dialogue_Beat3_Iris` (1,1,56), set `ch2_beat3_iris` | `Ch2BuildDialogue` |
| Mission steps | indices 10–12 of 20 | `AuthorReachStep`, `AuthorPromptStep`, `AuthorDialogueStep` |

### A.7 Beat 4 — Records Vault

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-5,5], z[62,74], center (0,0,68), 10×12 | `Chapter2Builder` room-build block |
| Floor/ceiling tint | (0.1, 0.1, 0.12) / (0.08, 0.07, 0.07) — unique floor tint | `BuildFloorCeiling(interior, "Vault", …)` |
| Doorway walls | front z=62 width10 gap2.4 (open); back z=74 width10 gap2.4 (`VaultToDockDoor`) | `BuildDoorwayWall` |
| Side walls | `Vault_WallW` x=-5, `Vault_WallE` x=5, size (0.2, RoomH, 12) | `BuildWall` |
| Room details | accent tint (0.25, 0.5, 0.55) | `BuildRoomDetails(interior, "Vault", (0,0,68), (5,6), …)` |
| Records racks ×3 | (-4,1.2,63), (-3.5,1.2,63), (-3,1.2,63), scale (0.3,2.4,4), color (0.2,0.22,0.25) | `Ch2BuildVault(interior)` |
| Disposal terminal | (0,0.9,68), scale (0.8,1.1,0.5), tint (0.2,0.85,1) | `Ch2BuildVault(interior)` |
| Diagnostic table | (2.5,0.45,70), scale (1.6,0.1,0.7), color (0.2,0.22,0.25) | `Ch2BuildVault(interior)` |
| Accent light | `VaultLight` (0,2.8,68), (0.6,0.9,1), i1.8, r12, `AmbientPulse(period:6.5s)` | `BuildAccentPointLight` + `AddAmbientPulse` |
| `VaultToDockDoor` | (0,0,74), width 2.4, `startLocked:true` | `BuildSlidingDoor` + `WireDoorAudio` |
| Reach point | `VaultReachPoint` (0,1,68), radius 4.5 | `AuthorReachStep(steps, 13, "ReachTrigger: Records Vault", …)` |
| Dialogue player | `Dialogue_Beat4_Reveal` (0,1,69), set `ch2_beat4_reveal` | `Ch2BuildDialogue` |
| Mission steps | indices 13–14 of 20 | `AuthorReachStep`, `AuthorDialogueStep` |

### A.8 Beat 5 — Escape / Dock

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-6,6], z[74,92], center (0,0,83), 12×18 | `Chapter2Builder` room-build block |
| Doorway wall | front z=74 width12 gap2.4 (`VaultToDockDoor`) | `BuildDoorwayWall` |
| Back wall | solid, z=92, size (12, RoomH, 0.2) — no gap, "the ship, lifting off" | `BuildWall` |
| Side walls | `Dock_WallW` x=-6, `Dock_WallE` x=6, size (0.2, RoomH, 18) | `BuildWall` |
| Room details | accent tint (0.4, 0.36, 0.3) | `BuildRoomDetails(interior, "Dock", (0,0,83), (6,9), …)` |
| Crates ×3 | (-4,0,80), (4,0,82), (-3,0,88), scale (0.8,0.8,0.8), color (0.35,0.28,0.18) | `Ch2BuildDock(interior)` |
| Tie-down | (0,0.05,85), scale (3,0.05,0.15), color crate×0.7 | `Ch2BuildDock(interior)` |
| Accent light | `DockLight` (0,2.6,83), (1,0.82,0.6), i2, r16 | `BuildAccentPointLight` |
| Ambience | `DockAmbience` (0,2.6,83), inner 2 / outer 8, max vol 0.4, clip `dock_wind.wav` | `BuildAmbienceLayer(…)` |
| Mira | spawn (1.5,0,77); `Health(40)`; `ProtectNpcObjective` (protectedHealth=miraHealth, playerEntity=rig); `StoryNpc` "Mira"; built `SetActive(false)` | `InstantiateNpc(Ch2MiraPrefab, …)`, `FitNamedCharacter` |
| Pursuer 1 | (-2,0,78); inactive; targets `playerHealth` | `BuildEnemy(pos, playerHealth, enemyDef)` |
| Pursuer 2 | (2,0,80); inactive; targets `miraHealth` | `BuildEnemy(pos, miraHealth, enemyDef)` |
| Reach point | `DockReachPoint` (0,1,85), radius 5 | `AuthorReachStep(steps, 16, "ReachTrigger: Dock", …)` |
| Dialogue players | `Dialogue_Beat5_Reunion` (0,1,86) set `ch2_beat5_reunion`; `Dialogue_Beat5_Kept` (0,1,87) set `ch2_beat5_kept` | `Ch2BuildDialogue` ×2 |
| Complete canvas | "CHAPTER 2 COMPLETE", (0,1.4,85), rotated 180° Y, inactive | `Ch2BuildCompleteCanvas(…)` |
| `ChapterOutro` | (0,1,85), inactive; `CampaignFlagSetter` flag `"ch2_complete"` wired to `OnActivated`; `completeCanvas` ref; `publishZoneCompleted` default true | `ChapterOutro` (`Scripts/Player/ChapterOutro.cs`) |
| Mission steps | indices 15–19 of 20 | `AuthorTriggerStep` ×2, `AuthorReachStep`, `AuthorDialogueStep` ×2 |

### A.9 Scene root hierarchy (current)

`BuildChapter2Auction()` creates these as **siblings**, not nested: `Directional Light`, the ten accent lights, `Undermarket` (all geometry, stalls, cages, racks, terminal, doors, NPCs), `Game` (`GameState` + `CombatFeedbackController`, no ambience source), the player rig, six reach points (`MarketReachPoint`, `BrokerReachPoint`, `AuctionReachPoint`, `CellsReachPoint`, `VaultReachPoint`, `DockReachPoint`), nine dialogue-player roots, `CutIrisPrompt`, the "CHAPTER 2 COMPLETE" canvas, `ChapterOutro`, `Mission`, `XR Interaction Manager` (if not already present), and `DockAmbience`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots (N = 0..5), and moves `Undermarket`'s contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

Every key referenced by this document, its target path, and whether it resolves **today**. Six resolve; everything else is a commission for the art team, and until it lands the primitive fallback (§1.5) covers it.

All prefab paths are rooted at `Assets/Ronin7/Art/Generated/`.

| Key | Path (relative to `Assets/Ronin7/`) | Status |
|---|---|---|
| `Named.Resh` | `Art/Generated/Characters3D/Named/Resh.prefab` | **EXISTS** |
| `Named.Iris` | `Art/Generated/Characters3D/Named/Iris.prefab` | **EXISTS** |
| `Named.Mira` | `Art/Generated/Characters3D/Named/Mira.prefab` | **EXISTS** |
| `Named.VelorumBroker` | `Art/Generated/Characters3D/Named/Velorum-Broker.prefab` | **EXISTS** |
| `Named.Echo` | `Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** *(resolves today via the hardcoded `Ch1EchoBladePrefab` path constant through `BuildSword`, not via `registry.Resolve` — see §4 Beat 0 note; same underlying asset, but not yet a true registry lookup)* |
| `Enemies.SyndicateEnforcer` | `Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** *(applied additively by `EnemyArtWirer`, not resolved by the builder itself)* |
| `Enemies.BrokerBodyguard` | `Art/Generated/Characters3D/Enemies/Coil_Syndicate_Ganger.prefab` | **EXISTS** *(same visual as above — no distinct art)* |
| `Diversity.VelorumCrowd` (`Vesh`, `Voll`) | `Art/Generated/Characters3D/Diversity/{Vesh,Voll}/…` | **EXISTS** *(applied additively by `CrowdArtWirer`, not resolved by the builder itself)* |
| `Rooms.DockingAlleyShell` | `Art/Generated/Rooms/DockingAlleyShell.prefab` | MISSING |
| `Rooms.MarketRowShell` | `Art/Generated/Rooms/MarketRowShell.prefab` | MISSING |
| `Rooms.BrokerFrontShell` | `Art/Generated/Rooms/BrokerFrontShell.prefab` | MISSING |
| `Rooms.AuctionFloorShell` | `Art/Generated/Rooms/AuctionFloorShell.prefab` | MISSING |
| `Rooms.HoldingCellsShell` | `Art/Generated/Rooms/HoldingCellsShell.prefab` | MISSING |
| `Rooms.RecordsVaultShell` | `Art/Generated/Rooms/RecordsVaultShell.prefab` | MISSING |
| `Rooms.EscapeDockShell` | `Art/Generated/Rooms/EscapeDockShell.prefab` | MISSING |
| `Doors.SlidingDoor_Standard` | `Art/Generated/Doors/SlidingDoor_Standard.prefab` | MISSING |
| `Props.MarketStall` | `Art/Generated/Props/MarketStall.prefab` | MISSING |
| `Props.BrokerCounter` | `Art/Generated/Props/BrokerCounter.prefab` | MISSING |
| `Props.BrokerGlass` | `Art/Generated/Props/BrokerGlass.prefab` | MISSING |
| `Props.AuctionStage` | `Art/Generated/Props/AuctionStage.prefab` | MISSING |
| `Props.AuctionLotPedestal` | `Art/Generated/Props/AuctionLotPedestal.prefab` | MISSING |
| `Props.AuctionCageBar` | `Art/Generated/Props/AuctionCageBar.prefab` | MISSING |
| `Props.OverseerBox` | `Art/Generated/Props/OverseerBox.prefab` | MISSING |
| `Props.AuctionCallerPulpit` | `Art/Generated/Props/AuctionCallerPulpit.prefab` | MISSING — no builder call exists for this prop at all today (§4 Beat 2, §9) |
| `Props.CollateralCage` | `Art/Generated/Props/CollateralCage.prefab` | MISSING |
| `Props.RecordsRack` | `Art/Generated/Props/RecordsRack.prefab` | MISSING |
| `Props.DisposalTerminal` | `Art/Generated/Props/DisposalTerminal.prefab` | MISSING |
| `Props.DiagnosticTable` | `Art/Generated/Props/DiagnosticTable.prefab` | MISSING |
| `Props.DockCrate` | `Art/Generated/Props/DockCrate.prefab` | MISSING |
| `Props.TieDown` | `Art/Generated/Props/TieDown.prefab` | MISSING |
| `Props.StorageTarp` | `Art/Generated/Props/StorageTarp.prefab` | MISSING — canon's Mira-hiding-place prop, no builder call exists (§4 Beat 5, §9) |
| `Props.WeaponRack` | `Art/Generated/Props/WeaponRack.prefab` | MISSING — cross-chapter dependency, Ch3 opens on this object (§4 Beat 5, §9) |
| `VFX.MarketSignage` | `Art/Generated/VFX/MarketSignage.prefab` | MISSING — canon's "hanging signage" (dialogue lines 20, 41); `MarketLight0`/`MarketLight1` supply only the glow it would cast (§4 Beat 1c) |
| `VFX.WreckfieldViewport` | `Art/Generated/VFX/WreckfieldViewport.prefab` | MISSING — Beat 0's rig-interior backdrop, "the wreck-field still turns slow in the viewport" (dialogue line 108); no rig-interior geometry exists to host it (§3, §4 Beat 0c, §9) |

**Reuse notes.**

- `Doors.SlidingDoor_Standard` serves both real doors (`AuctionToCellsDoor`, `VaultToDockDoor`) — the same key Chapter 1 uses for all three of its doors. Lock state is logic, not art.
- **`AuctionToCellsDoor` is a candidate for a distinct concealed-hatch art key, not a second instance of `Doors.SlidingDoor_Standard`.** Resh's defining trait is hidden smuggling routes "nobody's mapped, the ones that go under the cells" (canon line 247), and the mission-step label carries that fiction ("Unlock Cells Route (Resh's back-channel)") while the geometry does not. A `Doors.BackChannelHatch` key (concealed hatch / maintenance grate) would let the one thing that makes Resh structurally load-bearing to this chapter and the saga read in the geometry, not only the label — the same class of "canon furniture named as a concrete art target" as `Props.WeaponRack`/`Props.StorageTarp` below. See §2.
- `Props.MarketStall` serves all four Market Row stall placements; `Props.CollateralCage` serves all four Holding Cells cage clusters; `Props.RecordsRack` serves all three vault racks — one-key-many-placements, matching Chapter 1's `Doors.SlidingDoor_Standard` pattern.
- `Enemies.SyndicateEnforcer` and `Enemies.BrokerBodyguard` currently point at the **same** prefab (`Coil_Syndicate_Ganger.prefab`) — there is no dedicated Bodyguard silhouette. Splitting them into visually distinct art is a natural follow-up once the environment art pass reaches this chapter, mirroring the note Chapter 1 left about `Enemies.DominionTrooper`.
- **`EnemyArtWirer.cs`** and **`CrowdArtWirer.cs`** already apply the `Enemies.SyndicateEnforcer`/`Enemies.BrokerBodyguard` and `Diversity.VelorumCrowd` keys' art additively, post-build, from their own hardcoded scene-name maps — not from the registry. Folding both wirers' placement logic into the registry (so a fresh build produces dressed enemies/crowd directly, without a second menu action) is a natural follow-up to this refactor, but is **not** in its scope, exactly as Chapter 1's Appendix B notes for `EnemyArtWirer` there.
- **`ChapterRoomDetailsVarietyWirer.cs`** also targets `Ch02_Auction.unity` by scene name today, varying the procedural crate/console/pipe scatter `BuildRoomDetails` produces per room. It has no registry key of its own in this document because its output is intentionally deterministic-random dressing, not a placed prop — it is out of scope for the registry migration entirely, not merely unresolved.
- `VFX.MarketSignage` and `VFX.WreckfieldViewport` are this document's only two `VFX` category keys — the category was declared in §1.2/§1.3/§1.5 but carried no per-beat inventory of its own until this pass. Both remain commissions like every other MISSING row above; nothing about adding the keys changes their fallback behavior (§1.5).

---

*Character art prefabs are produced by the Tripo image→3D pipeline (see `Tools/Space Samurai/Art`). Environment prefabs are expected to follow the same pipeline into `Art/Generated/{Rooms,Props,Doors,VFX}/`. Files consulted for the as-built appendix: `Project/Assets/Ronin7/Scripts/Editor/Chapter2Builder.cs`, `ChapterSharedBuilders.cs`, `XRRigBuilder.cs`, `Chapter2Lines.cs`, `EnemyArtWirer.cs`, `CrowdArtWirer.cs`, `ChapterRoomDetailsVarietyWirer.cs`, `Scripts/World/Story/ProtectNpcObjective.cs`, `Scripts/Player/ChapterOutro.cs`, `Scripts/Editor/Art/ArtPrefabRegistry.cs`, `Tests/EditMode/Chapter2LinesTests.cs`, `story ouput/Ch02_The_Auction.md`, `story ouput/Ch02_The_Auction_Dialogue_Script.md`, `story ouput/00_STORY_BIBLE.md`, `story ouput/audit/Ch02_audit.md`, `Project/Docs/CHAPTER-BUILD-LEDGER.md`.*

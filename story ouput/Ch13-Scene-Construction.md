# Chapter 13 — Scene Construction

*The architectural contract for `Ch13_SterileReckoning.unity`: what Chapter 13 ("The Sterile Reckoning") must become, what it is today, and the invariants that survive the transition.*

## 1. Purpose & how to use

This document lets a builder reproduce Chapter 13 from a cold clone: no Unity scene file is required as an input, only the builder script and the canon story files it draws from.

### 1.1 Refactoring Goal (the prime directive)

> **Your objective is to refactor this builder system to support high-fidelity art pipelines. You must transition the code away from generating primitives and toward instantiating predefined art prefabs, without breaking any of the existing Y-invariants or mission triggers.**

This document is **no longer a transcription of what the code does today.** It is the target state. Where the current implementation differs, the difference is recorded in **Appendix A (As-built primitive fallback)** — that appendix is the fallback path's source of truth, not a description of the goal.

Read this section as an instruction, not a description:

- **The `.unity` scene file is a generated artifact.** It is produced by running the builder and saving; it is never the thing you hand-edit to make a durable change — with exactly one exception, the artist safe zone (§1.4).
- **Source of truth for *code*:** `Project/Assets/Ronin7/Scripts/Editor/Chapter13Builder.cs`, entry point `XRRigBuilder.BuildChapter13SterileReckoning()`, invoked from the Unity menu **Tools → Space Samurai → Chapters → Build Chapter 13 — The Sterile Reckoning**.
- **Source of truth for *content*:** this document plus the canon story files (`Ch13_The_Sterile_Reckoning.md`, `..._Dialogue_Script.md`, `00_STORY_BIBLE.md`).
- **World scale is 1 unit = 1 meter.** Never break it — this is a VR project; a mis-scaled prop or room reads as physically wrong to a headset wearer in a way it never would on a monitor. **A prefab whose native scale violates this is a broken prefab; fix the asset, never the instantiation scale.**
- **No camera shake, ever.** The Redactor mini-boss fight's impact feedback comes from `Haptics`, `AudioDirector` stingers, and `CombatFeedbackController`'s reticle — never from moving the camera.
- **Traversal in Ch13 is continuous locomotion + snap-turn only**, built via `BuildRig(refs, addLocomotion: true)`, with a comfort vignette on turn/thrust. There is **no teleport locomotion, no NavMesh, no parkour/climb/wall-run**, and — unlike every earlier chapter in this saga — **no `NpcWalker` at all**. Neither Dr. Heris nor Sallow travels; both are static set dressing placed once at build time (§5). Do not introduce any of the excluded mechanics, or a waypoint walker, when patching this scene.
- **Minimal-new-code chapter.** Per the builder's own class summary: *"no new ability, no new mechanic, no new runtime component."* This is a story/dialogue chapter that completes the ten-ally roster using only the existing plain `Dialogue` / `ReachTrigger` / `DefeatEnemies` / `Trigger` mission-step vocabulary — there is no `DuelYield` or `MemoryDiveController` here, unlike the mindspace dives of Ch7/Ch8/Ch11. `AttachPlayerAbilities` still runs so all five previously-earned abilities (weakpoint-sight, Overdrive, Phase-step, Unbroken, Mirror) persist onto the rig, but none are granted here. **Screenplay/as-built tension, flagged so it isn't "restored" by mistake:** the breach production note (script ~line 296) calls Mirror "introduced into the traversal/combat sandbox here as the new chapter ability," but both the setting block (script line 17, "took Mirror off its freed blade-shadow" in Ch12) and this builder treat Mirror as a **Ch12 earn** re-attached, not a Ch13 grant — the builder runs no `AbilityGranter` anywhere. Do not add one to match the screenplay's wording; the as-built behavior is correct and the screenplay line is the stale one.

### 1.2 The method-separation contract

Mission logic and set dressing must not share a method. Every beat splits into exactly two entry points:

| Method | Owns | Parents its output under |
|---|---|---|
| `BuildBeatNArt(Transform staticArtRoot)` | room shells, props, VFX, backdrops, decorative lights *(no doors this chapter — see §2)* | `[STATIC_ART_DO_NOT_DELETE]` |
| `BuildBeatNLogic(Transform logicRoot, …)` | enemy spawns (inactive), reach points, dialogue players, mission-spine steps | `[BEAT_N_LOGIC]` |

**As-built reality check.** Unlike a chapter that has already begun this split, `Chapter13Builder.cs` has **not been decomposed at all** — `BuildChapter13SterileReckoning()` is one 220-line method that builds every zone's geometry, every dialogue player, every enemy, and the mission-spine steps inline, in narrative order. There are no `BuildBeat0Art`/`BuildBeat0Logic`-style pairs to point to; the "beats" below are the mission-spine step labels the code itself authors (`"Beat0: …"`, `"Beat1: …"`, `"Beat1B: …"`, `"Beat2: …"`, `"Beat3: …"`, `"Beat4: …"`) rather than separate methods. Splitting the monolith into the six art/logic pairs implied by those labels **is** the refactor this document specifies.

**Scope discipline (non-negotiable).** `XRRigBuilder` is a `partial class` shared by **14 chapter builders** plus `HubBuilder`, `ShipPrologueBuilder`, and `ParkourLevelBuilder`. Changing a signature in `ChapterSharedBuilders.cs` — `BuildFloorCeiling`, `BuildWall`, `BuildProp`, `BuildAccentPointLight`, `BuildAmbienceLayer`, `Author*Step`, `BuildEnemy` — ripples across all of them.

- **Frozen:** every helper in `ChapterSharedBuilders.cs` and `XRRigBuilder.cs`, plus `FitNamedCharacter` (authored in `Chapter1Builder.cs` but reused, unmodified, by every later chapter including this one).
- **Free to restructure:** the Ch13-local helpers, called only from `BuildChapter13SterileReckoning()` — `Ch13BuildDesignWardRow`, `Ch13BuildSealedNurseryRow`, `Ch13BuildHiveCascade`, `Ch13PlaceStoryNpc`, `Ch13BuildDialogue`, `Ch13WireVoiceClips`, `Ch13BuildCompleteCanvas`, `Ch13EnsureLabSecurityDefinition`, `Ch13EnsureRedactorDefinition`.

This refactor lives entirely in the second list. If you find yourself editing `ChapterSharedBuilders.cs`, stop — you have left Chapter 13 and are now silently rebuilding thirteen other chapters.

### 1.3 Data-driven environment: no hardcoded look

**Do not hardcode lighting values, colors, or fog densities directly into the builder script.** Two ScriptableObjects — new for this chapter, following the same schema already specified for Ch1 — carry everything the builder currently types inline:

| Asset | Type | Instance path | Holds |
|---|---|---|---|
| Environment profile | `ChapterEnvironmentProfile` | `Assets/Ronin7/Data/Ch13Environment.asset` | directional key (color/intensity/rotation), ambient mode + color, fog mode/color/density, per-room floor + ceiling tint, nine accent lights, zero event lights |
| Art registry | `ArtAssetRegistry` | `Assets/Ronin7/Data/ArtAssetRegistry.asset` | every `Category.Key → prefab` mapping referenced in this document (shared across all 14 chapters — this document adds Ch13's rows, it does not create a second registry asset) |

**As-built reality check — this chapter is further from the target than Ch1.** Ch1's primitives are at least tinted set-dressing built with the same `BuildProp`/`BuildWall` idiom this document assumes will migrate. Ch13 additionally routes its enemy *bodies* through a **second, already-existing** fallback mechanism — `ArtPrefabRegistry.TryInstantiateOrFallback(EnemyFootPrefabPath, bodyGreybox, …)` inside the shared `BuildEnemy` helper — which is a different, older indirection than the `ArtAssetRegistry` this section specifies. Do not confuse the two: `ArtPrefabRegistry` is generic infrastructure for every chapter's melee-enemy capsule body, already live; `ArtAssetRegistry` is the per-chapter room/prop/door registry this refactor introduces. Rooms, walls, and every named Ch13 prop (`DesignTable`, `InstrumentTray`, `SealedCradle`, `SterileTable`) currently go through **neither** — they are raw `PrimitiveType` calls with zero indirection. See Appendix B.

**Prefab root is `Assets/Ronin7/Art/Generated/`.** New environment folders for this chapter are siblings of the ones Ch1 defines:

```
Assets/Ronin7/Art/Generated/
  Characters3D/{Named,Enemies,Diversity}/   ← exists today (Sallow.prefab, Echo.prefab already baked flat under Named/; Dr-Heris.prefab is baked but mislocated at Named/Khall_Assets/Dr-Heris.prefab, not the flat path the builder reads — see §4 Beat 2b, Appendix B)
  Rooms/                                    ← new rows for SterileVault_*Shell
  Props/                                    ← new rows for DesignTable / InstrumentTray / SealedCradle / SterileTable
  Doors/                                    ← unused this chapter — no doors exist (§2)
  VFX/                                      ← unused this chapter — no VFX props are instantiated
```

### 1.4 The artist safe zone — `[STATIC_ART_DO_NOT_DELETE]`

The builder must create an empty GameObject named **`[STATIC_ART_DO_NOT_DELETE]`**. Before wiping the scene during a fresh build, the script must preserve this object and all of its children, wiping only the generated logic and trigger components.

> **⚠ IMPLEMENTATION NOTE — this cannot be done as a "search and preserve."**
>
> `BuildChapter13SterileReckoning()` currently wipes via `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` (`Chapter13Builder.cs:102`), the same discard-and-recreate strategy documented for Ch1. That call does not delete objects from the scene — it discards the entire scene and opens a fresh empty one. A naïve `GameObject.Find("[STATIC_ART_DO_NOT_DELETE]")` after `NewScene` will always return `null`.
>
> Making the safe zone real requires **replacing the wipe strategy**, exactly as specified for Ch1:
>
> 1. `EditorSceneManager.OpenScene(Ch13ScenePath)`, then `DestroyImmediate` each **generated root by name** (`SterileVault`, `Game`, `Mission`, the rig, the nine accent lights, the enemy set, `Dr. Heris`/`Sallow`, the three reach points, the fourteen dialogue players, the complete canvas, `ChapterOutro`, `LabCoreAmbience`, `SterileHiveCascade`), leaving `[STATIC_ART_DO_NOT_DELETE]` untouched. Fall back to `NewScene` only when the scene file does not yet exist.
> 2. Extract the static-art subtree to a temporary prefab before `NewScene`, re-instantiate after.
>
> **Option 1 is preferred**, per the same `EnemyArtWirer.cs`/`CrowdArtWirer.cs` precedent cited for Ch1.

Everything the target `BuildBeatNArt()` methods would instantiate goes under this root. Everything the target `BuildBeatNLogic()` methods would author goes under `[BEAT_N_LOGIC]` and is considered disposable.

### 1.5 The fallback rule (safety interlock)

**As of this writing, zero Ch13 environment prefabs exist** in the `ArtAssetRegistry` sense — no sterile-vault room shell, no design table, no instrument tray, no sealed cradle, no sterile table. See Appendix B for the full inventory: of the three Named-character keys, only `Named.Sallow` and `Named.Echo` actually resolve; `Named.DrHeris` is baked but sits at `Named/Khall_Assets/Dr-Heris.prefab`, one level below the flat path `Ch13HerisPrefab` reads, so it resolves to nothing at build time (§4 Beat 2b). Everything else is a commission.

A builder that instantiates from an empty registry produces **empty rooms** — the first run of a refactored builder would destroy Chapter 13.

Therefore: **when a registry slot is empty, the builder falls back to the existing primitive helper from Appendix A and logs a warning.** Never throw, never skip silently.

```csharp
var prefab = registry.Resolve(ArtKey.Rooms_SterileVault_LabCoreShell);
if (prefab == null) {
    Debug.LogWarning($"[Ch13] {ArtKey.Rooms_SterileVault_LabCoreShell} unresolved — primitive fallback.");
    BuildFloorCeiling(world, "LabCore", new Vector3(0f, 0f, 70f), new Vector3(14f, 0f, 20f), floorTint, ceilTint); // Appendix A geometry
} else {
    InstantiateAt(prefab, staticArtRoot, pos, rot);
}
```

This mirrors the guard already shipping in `ChapterSharedBuilders.cs:623` (`if (prefab == null) continue; // not baked yet`) and the enemy-body fallback already live in `BuildEnemy` (§1.3). The chapter must remain playable at every commit during the art migration.

### 1.6 Performance budget

- **90 FPS is the design target** (11.11 ms/frame). The scene as shipped runs under **`QualityBootstrap`'s default of 72 Hz** — treat 90 FPS as the ceiling to protect and 72 Hz as the floor you are actually shipping against today.
- **No chapter-specific `UnityStats` measurement has been recorded for Ch13** in `Project/Docs/CHAPTER-BUILD-LEDGER.md` (unlike Ch1's recorded greybox baseline of drawCalls 189 / setPassCalls 17 / tris 9,198 / verts 13,092). **Capture one on the next build** before evaluating any prefab swap against it — do not assume Ch1's numbers transfer; this chapter is roughly 2.3× Ch1's linear span (96 m along +Z vs. Ch1's 42 m) with five contiguous room volumes instead of four, so its greybox baseline will differ.
- The chapter's props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) exactly as in every other chapter — replacing them with high-fidelity prefabs is exactly the change that breaks the frame bar. Every prefab landing in the registry must be re-measured once a baseline exists.

## 2. Chapter spatial map

Chapter 13 is **one continuous scene**, `Assets/Ronin7/Scenes/Ch13_SterileReckoning.unity`, laid out as **five contiguous rooms strung along a single linear +Z corridor** — there is no branching, no vertical stacking (the class summary is explicit: *"no vertical descent — the chapter's horror is clinical order, not depth"*), and no returning to an earlier zone by any route other than walking back down the same corridor. The player spawns at z≈4 and the story pushes them monotonically toward z≈90.

**Structural deviation from every earlier chapter: there are zero doors.** `Chapter13Builder.cs` contains no `BuildSlidingDoor` call anywhere. The five zones are simply adjoining floor/ceiling volumes with side walls (`BuildWall` on the W/E flanks per room, mirroring "Ch9's HoldHall/TideDepths/ConstructionCore idiom" per the builder's own comment) and **no cross-wall or lock gates the corridor between them** — progression is paced entirely by `ReachTrigger` proximity zones and `DefeatEnemies` steps in the mission spine, not by physical door state. `SpawnGround` itself has no side walls at all (no `BuildWall` calls exist for it in the source) — it is open-flanked, unlike every other room in the chapter.

```
 -Z                                                                                                                              +Z
 Spawn Ground ──(open)── Outer Corridor (wards + lab security) ──(open)── Nurseries (recognition) ──(open)── Lab Core (Redactor, Heris) ──(open)── Annex (Sallow)
  x[-6,6] z[-2,10]         x[-6,6] z[10,34]                              x[-6,6] z[34,60]                    x[-7,7] z[60,80]                    x[-5,5] z[80,94]
  center (0,0,4), 12×12    center (0,0,22), 12×24                       center (0,0,47), 12×26              center (0,0,70), 14×20              center (0,0,87), 10×14
```

| Zone | Room-name in code | Footprint | Floor center / size | Occupies beats |
|---|---|---|---|---|
| 1 | `SpawnGround` | x[-6,6], z[-2,10] | center (0,0,4), 12×12 | Beat 0 (briefing plays here) |
| 2 | `OuterCorridor` | x[-6,6], z[10,34] | center (0,0,22), 12×24 | Beat 1 (breach, design wards, lab security) |
| 3 | `Nurseries` | x[-6,6], z[34,60] | center (0,0,47), 12×26 | Beat 1 (Echo's recognition), leads into Beat 1B |
| 4 | `LabCore` | x[-7,7], z[60,80] | center (0,0,70), 14×20 | Beat 1B (Redactor), Beat 2 (the Maker), Beat 3 (defection) |
| 5 | `Annex` | x[-5,5], z[80,94] | center (0,0,87), 10×14 | Beat 4 (Sallow) |

`RoomH` (ceiling height, shared constant in `ChapterSharedBuilders.cs`) = **3.6 m** for every room in every chapter, including this one.

**Stale inline comment, doc is correct.** `Chapter13Builder.cs:137` comments "Four contiguous zones along +Z," but the method that follows builds **five** (`SpawnGround`/`OuterCorridor`/`Nurseries`/`LabCore`/`Annex`, per the table above). This document's "five... instead of four" framing is accurate throughout; the stale "four" is a source-comment drift only — flagged here so a future editor patching the builder doesn't trust the comment and drop a zone.

**These footprints are load-bearing and survive the refactor unchanged.** A room-shell prefab must fit its footprint exactly; the spatial map is the contract, not the prefab's convenience. `LabCore` is the widest room (14 m, ±7) — it hosts both the mini-boss fight and the entire reckoning/defection dialogue run; `Annex` is the narrowest and is the chapter's dead end, walled on its north side (`Annex_WallN` at z=94) with no exit beyond it.

**Doors** — none. Unlike Ch1's three-door corridor, Chapter 13 has no `Doors.SlidingDoor_Standard` instances, no lock states, and no `WireDoorAudio` calls. A future patch that adds a physical gate (for instance, a sealed core door ahead of the Redactor — not merely a screenplay staging note but a door the *shipped* Beat 1B dialogue actively instructs the player to block and then narrates as open, with no door object anywhere in the geometry; see §4 Beat 1B/b, §9) must introduce a new registry key and a `startLocked`/`Trigger` pair; it does not yet exist as either art or logic. **The doors are not the only dropped screenplay traversal-kit element.** The breach production note (script ~line 296) also specifies "sterile-field locks and security gates" and "light verticality through service shafts" for the traversal segment; both are dropped as-built along with the doors — the corridor is flat (no verticality, consistent with the class summary's "no vertical descent," §2 above) and lockless throughout. Named here alongside the no-doors flag so the set of intentionally-dropped screenplay elements is enumerated in one place (see also §3's dropped sensing-SFX note and §7's declined sterile-air ambience).

**Player rig:** `BuildRig(refs, addLocomotion: true)` (head + two hands, no visible body) plus `EchoPresence` (additive, no extra wiring) and `AttachPlayerAbilities(rig, refs)` (re-attaches all five previously-earned abilities; none are new this chapter). `ZoneBounds` is set to **center (0, 1, 46), radius 105** — a single bounding sphere loosely enclosing all five rooms along the corridor's midpoint, generous enough to cover the full z[-2,94] span.

## 3. Global environment & backdrop

**The sterile vault — clinical horror as the chapter's defining mode.** Per the dialogue script's SETTING block, this is "a place of unbearable cleanliness… the tonal opposite of everything Act II and Act III walked him through." Where the Tide (Ch9) was drowned hush, the Ninefold (Ch10) a dry counting silence, the bone-canyon (Ch11) a grieving dream, and the cryo-vault (Ch12) cold industrial certainty, Ch13 is "the horror of the CLINICAL. This is not where people were killed. This is where people were DESIGNED." The builder executes this directly: near-white floor/ceiling tints climbing from a cool grey at spawn (0.82,0.85,0.9) to near-pure white at the Lab Core (0.92,0.94,0.98 / 0.95,0.96,1), an almost-imperceptible exponential fog (density 0.006 — "barely there, this place is clean, not hazy," versus Ch1's 0.018), and nine accent point lights that shift from neutral white-ish through a deepening sterile blue across the Nurseries and Annex before returning to the brightest, most surgical white at the Lab Core.

**Tiered clinical horror.** The production notes specify three horror tiers along the approach — OUTER (generic sterile research facility), MID (the design-horror of tables, trays, and charts), INNER (the sealed nurseries, small cradles, the unbearable proud cleanliness of a place children were designed and switched) — and the builder's zone layout follows this exactly: `OuterCorridor`'s design-ward row of tables/trays is the MID tier arriving early, and `Nurseries`' sealed-cradle row is the INNER tier. **The horror is cleanliness, never decay** — there is no rust, no rot, no ruin anywhere in this chapter's set dressing, a hard reversal of every room built for Ch9–Ch12.

**Even the comms are sterilized.** Every earlier chapter's crew channel carries some flavor of depth-degradation, haunting, or static — the signature sound of distance and dread. This one deliberately does not: the dialogue script notes *"the channel is clean here, no depth-degradation, no haunting… because the lab is live powered hardware"* (script ~line 298). §4 Beat 2f and §7's SFX table already treat the chapter's sparse audio bed as intentional; this is the same thesis one level up — the absence of the saga's usual comm-haunting is itself part of the clinical horror, not just an audio-engineering footnote, and the VO-mix pass should read it as authored rather than as an oversight to "fix" with the usual comm processing.

**Temperature is deliberately not a sensory register here, either.** Every earlier chapter's SETTING block leans on a thermal read — Ch12's cryo-command vault is "cold certainty made into architecture," its horror partly the cold itself (see `Ch12-Scene-Construction.md` §3). Ch13's SETTING block pointedly withholds that register: the horror here is cleanliness and filtered air — "the air itself filtered to nothing," "every surface scrubbed to a hush" (script lines 44–45) — never cold, never warm. §7 physicalizes the space through air (the declined sterile-air ambience layer), but the *absence* of a thermal cue is itself authored, not an oversight, exactly like the dropped comm-haunting above — a future art or audio pass should not "warm up" or "chill" the vault to match the sibling chapters on either side of it; doing so would break the clinical-neutral read the whole chapter is built on.

**No physical Cairn set this chapter.** Per the class summary's "CREW-PRESENCE DECISION," the full crew (Cassie-04, Sable, Kessler, Morrigan, Coral Vex, Mera Voss, Vess) speaks **voice-only** throughout — mirroring Ch11/Ch12 — during both the Beat 0 briefing and the Beat 1 breach comm barks. No holo-table, no war-room geometry, no ship-interior is built for this chapter; the Beat 0 sensing plays diegetically as VO over the player's own spawn position inside the sterile vault (`Dialogue_Beat0_Briefing` at (0,1,4), the same zone the katana rests in). Only Ronin-7 (the player), the Redactor, Dr. Heris, and Sallow get physical placement.

**A production note this decision silently drops.** The Beat 0 production note closes with *"Author the full crew briefing pool and the table's sensing-event SFX once the war-room geometry is set."* Since this chapter deliberately builds no war-room geometry (above), that sensing-event SFX is dropped along with it — the same kind of intentional, non-obvious omission §7 flags for the sterile-air ambience layer. Noted here so it reads as a consequence of the voice-only decision, not an oversight.

**A diegetic seam the voice-only decision leaves unaddressed.** The script's Beat 0 is staged *"Interior: the Cairn, the war-room. The holo-table still holds the freshly completed build-record…"* (script ~line 215) and closes with Cassie locking the trail to the table, the holo throwing up "a long sterile approach ending in a white sanctum," and *"The Cairn comes about toward the cleanest place in the galaxy, where a guilty hand has been waiting a long time. CUT to the breach"* (script ~line 288) — the fiction is a shipboard briefing followed by a deployment cut. As-built, the player hears all of this while already standing on the vault's own clean white floor at (0,1,4) (§4 Beat 0b), with no transit between the two. This is an accepted consequence of the voice-only decision above, not a bug for a future editor to "fix" by building a Cairn set — noted here so the VO-mix pass knows the crew comm should read as remote/aboard-ship during Beat 0, not co-located with the player.

**The same seam extends into Beat 1 — Ronin-7's closing Beat 0 line stages a boarding party that never physically forms.** Ronin-7's Beat 0 close explicitly assigns crew to the breach as if they will be there in person: *"Morrigan, you're with me on the breach, I want the firmware read by someone who knows the hand that wrote it. Mera, watch the door you think is open. Coral, you'll be close"* (script ~line 284). Yet as-built, every one of them — Morrigan included, despite being singled out as physically "with me" — is comm-only during Beat 1 (§4 Beat 1e's breach dialogue is four comm-only barks; no crew member is ever placed or `SetActive` anywhere in `Chapter13Builder.cs`). The §3 seam note above only covers the Cairn-vs-vault location mismatch for Beat 0's briefing; it should also flag this for the VO-mix pass — even the crew Ronin-7 orders *onto* the breach in his own closing line must still read as remote/aboard-ship through Beat 1, since none of them is ever physically instantiated in the vault.

### 3.1 `ChapterEnvironmentProfile` — the master palette

**No lighting value, color, or fog density is typed into `Chapter13Builder.cs`.** The builder reads `Assets/Ronin7/Data/Ch13Environment.asset`, same schema as every other chapter's profile:

| Field | Type | Read by |
|---|---|---|
| `keyLightColor`, `keyLightIntensity`, `keyLightRotation` | `Color`, `float`, `Vector3` | the scene's single directional light |
| `ambientMode`, `ambientColor` | `AmbientMode`, `Color` | `RenderSettings` |
| `fogMode`, `fogColor`, `fogDensity` | `FogMode`, `Color`, `float` | `RenderSettings` |
| `floorTint`, `ceilingTint` (×5, one pair per zone) | `Color` | every `BuildFloorCeiling` call |
| `accentLights[]` (×9) | `{ name, position, color, intensity, range, behaviour }` | `BuildAccentPointLight` per zone |
| `eventLights[]` | — | **empty this chapter** — no inactive alarm-style light exists (contrast Ch1's `DockingAlarmLight`) |

`behaviour` is an enum — `None` / `AmbientPulse(period)` / `ConsoleFlicker(seed)` — replacing the current inline `AddAmbientPulse("NurseryLight0", periodSeconds: 7.6f)` and `AddConsoleFlicker("CoreLight0", seed: 141f)` calls with data. Of the nine accent lights, only these two carry a non-`None` behaviour; the remaining seven are static.

The nine accent-light entries are **authored in the profile asset, not in code.** Their current literal values are recorded in **Appendix A.1** and must be reproduced exactly when the asset is first authored — this is a lift-and-shift, not a re-lighting pass.

**Material / tint palette:** exactly as Ch1, set-dressing props today are cheap primitives tinted via the shared `TintShared` helper rather than unique materials — this keeps the chapter's draw-call count low and keeps regeneration cheap if colors need retuning. **Prefabs replacing them must carry their own materials and will not batch this way.**

### 3.2 Look-brief for the commissioned room shells and props

**Every `Rooms.SterileVault_*Shell` and `Props.DesignTable`/`InstrumentTray`/`SealedCradle`/`SterileTable` row in Appendix B is `MISSING`, and none of them carries an artist reference beyond the primitive fallback's tint.** This document's entire purpose is commissioning those prefabs, so the dialogue script's own material language is recorded here rather than left implicit in the SETTING block:

- **Surfaces:** "pale grey panel and clinical steel" — the room shells' base material family; not brushed-industrial (contrast Ch12's cryo-vault) and not warm or organic (contrast Ch9/Ch11).
- **Lighting fixtures:** "recessed surgical light" — accent-light housings should read as flush-mounted medical fixtures, not exposed bulbs or hanging rigs.
- **Seam-glow (the chapter's defining signature):** "a sterile blue glow seeping from recessed seams" — panel-gap emissive lighting baked into the room shells' own materials. **This is not reducible to `NurseryLight0`/`NurseryLight1`, the two point lights the profile currently treats as the chapter's sole blue source (§3.1, Appendix A.1).** A commissioned shell prefab needs its own low-level emissive seam texture running through every room, not just the two Nursery accent lights standing in for it. Without this, a future art pass has only the greybox tint to work from and will miss the detail the "clinical horror, never decay" thesis (§3) actually rests on. **The Nurseries' seam-glow specifically has a second, more concrete anchor beyond that thesis: it is Echo's birth-light.** Echo's recognition line names the exact fixture — *"The first thing I ever saw, the very first, was this light. This exact light, this exact ceiling"* (`ch13_beat1_recognition`) — so the `Nurseries` ceiling and its seam-emissive are not just mood dressing but the specific object a shipped line points at, which is the reason `NurseryLight0`/`NurseryLight1` alone can't stand in for the room-wide emissive this bullet calls for.
- **Finish:** "every surface scrubbed to a hush" — no wear, no grime, no scuffing on any prop or shell; this chapter is the one hard reversal of every room built for Ch9–Ch12 (§3).
- **`Props.SealedCradle`:** "small cradles in filtered light" (script ~line 325) — small, sealed, and softly lit from within, not simply a tinted box; the INNER horror tier (§3) depends on these reading as nurseries, not storage.
- **`Props.DesignTable` / `Props.InstrumentTray` — the MID tier's depiction, not just its function or its tiering.** §4 Beat 1d already briefs `DesignTable`'s cover height for the lab-security fight and §3's tiered-horror paragraph already places both props at the MID tier; neither gives either prop a *look*. Canon's MID horror is "the design-horror of tables, trays, and charts" and the wards read "with instruments, with precision, with charts and tolerances" (script ~line 50) — `InstrumentTray` should read as a laid-out surgical/instrument tray (implements racked in rows, not a flat tinted slab) and `DesignTable` as a clinical design station with charts/schematics implied on its surface, not generic furniture. Without this, a future art pass has only "pale grey panel and clinical steel" to work from and the MID tier risks reading as an empty corridor of tables rather than the workstations where operatives were designed.
- **`Props.SterileTable` — the prop the player confronts at conversational range.** Unlike the rows above, this one has no per-item bullet despite being named in this section's header; it is the single prop the entire Beat 2 reckoning is staged against — canon's "DR. HERIS faces RONIN-7 across a sterile table" (script line 87) — and the table canon loads with the chapter's core horror: *"a child was laid on a table and a switch was seated in a sealed skull by careful gloved hands"* (script ~line 47–48). It should read as a clinical procedure/operating table — the table Ronin-7 was plausibly made on, not a generic conference or briefing surface — scrubbed to the same hush as every other surface in this chapter (§3), at a height that supports "across the table" eye contact at Heris and the player's roughly matching standing height (~1 m table height between two ~1.6–1.8 m figures, per the height spot-checks elsewhere in this section). **This ~1 m target intentionally raises the prop above the primitive fallback's `y=0.5`, size (1.8, 0.1, 0.9) greybox slab (Appendix A.5) — a ~0.55 m-tall surface never sized for eye contact, only to exist as a placeholder — so the commission should read as an improvement on the fallback's height, not a contradiction of it.** This is the highest-value missing depiction note in this list, since it is the prop the player stares at longest and the one that most reinforces "this is where people were designed."
- **`Named.Sallow` — `EXISTS`, but the status is not a fidelity guarantee.** Because the prefab is already baked, it gets none of the look-brief scrutiny above (this list otherwise covers only `MISSING` rooms/props) — yet Sallow carries the chapter's central image. Once baked, spot-check it against script line 502: it must read at **~1.8 m** (the 1u=1m VR check — see §1.1) with a **visibly open chest AI-cradle** and the spine/skull seating-ports called out in the same line and echoed at line 74. Echo's Beat 4 line "a hollow in its chest that's shaped exactly like what I am" (`ch13_beat4_introduce_sallow_01_echo`) and the whole absolution-body reframe (§4 Beat 4a) are meaningless if the cradle isn't visibly open on the mesh.
- **`Rooms.SterileVault_SpawnShell` — the chapter's first frame is the one this can't afford to get wrong.** §2 and Appendix A.2 note that the greybox `SpawnGround` is open-flanked — no W/E `BuildWall` calls target it — as a neutral structural fact. It is not neutral for the commission. The player spawns at (0,1,4) *inside* this room, fog is near-nil (density 0.006), and this is the player's literal first view of "unbearable cleanliness" before anyone has said a word (§3's SETTING-block thesis: "the womb of the make, white, quiet"). Two open edges into skybox/void at that exact moment is the antithesis of a sealed clinical womb — the one beat the chapter cannot afford to read as a greybox. The commissioned shell **must enclose the W/E flanks the primitive fallback omits**, so the opening frame reads as a sealed room with no edge-of-void sightline, even though the fallback geometry leaves them open.

These are commission notes, not new geometry requirements — footprints, positions, and counts in §4's art tables and Appendix A/B are unchanged.

## 4. Per-beat scene spec

The chapter plays as six mission-spine beats along the linear +Z run (`Beat0`, `Beat1`, `Beat1B`, `Beat2`, `Beat3`, `Beat4` — the exact labels the builder writes into `MissionDirector.steps`). Each beat is documented with the same a–f structure used for every other chapter.

**Table conventions, everywhere below:**

- Art tables carry **Position / Rotation**, a **Registry Key**, the path it **resolves to**, and a **Status**.
- Art tables never carry `scale()`, `size()`, or `PrimitiveType`. **Prefabs supply their own native scale.** The old primitive dimensions live in Appendix A.
- Positions and rotations *are* kept — they encode blocking, sightlines, and room geometry.
- **Status `MISSING`** means the prefab does not exist and the primitive fallback (§1.5) is active for that row. **Status `EXISTS`** means the prefab is already baked and glob-confirmed on disk. **Status `MISLOCATED`** means the prefab is baked and on disk, but not at the path the builder's constant reads — `AssetDatabase.LoadAssetAtPath` returns null and the row falls through to its fallback exactly as if `MISSING` (only `Named.DrHeris` carries this status — §4 Beat 2b).

---

### Beat 0 — The Cairn (The Sensing, The Briefing)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> This beat has no physical geometry of its own — it plays entirely as VO over the player's spawn position. Your objective is still to separate concerns: create **`BuildBeat0Logic()`** for the dialogue player and its mission step; there is no `BuildBeat0Art()` to write, because this beat borrows `SpawnGround`'s geometry (built by Beat 1's art pass) rather than owning any of its own.

#### a. Narrative purpose & emotional target

Act IV opens not on a descent but on a **sensing**. Cipher is barely off the Ch12 cryo-vault, the last fragment still in hand, when Cassie-04 and Sable — the two recruited living-archive keeper-hosts — go rigid at the same breath, feeling one archive out past the lattice larger than all four scattered nodes combined. The reveal is staged in five stages across the dialogue set: first the size ("bigger than the four of us combined"), then the kind — not a flat "transmitter" but Cassie's own load-bearing image, *"This one isn't built to keep anything… It's built to push. It's a throat, Cipher"* (`ch13_beat0_briefing`), an organ built to push voice out, pairing with Sable's later "one leash on one neck" — then the name ("the Concord Engine itself"), then the horror (scaled to do to a galaxy what a killswitch does to one neck), then the wall (Sable "can't feel a single seam anywhere on it"). Morrigan resolves the helplessness into a plan: *"You don't break a leash this size by force. You find the one person who built it and left a flaw."* The Ch12 command-network trail and the Ch7 dissent trail converge onto one lab, one name — Dr. Heris. Mera plants the mini-boss ("that reads like bait to me"); Echo, private to Cipher alone, anchors the dread and the hope in one line: *"Whoever's down there made the both of us."*

This is the chapter's only beat with zero physical staging — it is pure crew-voice, the calm before a very loud silence.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat0Logic()`

All logic components would parent to `[BEAT_0_LOGIC]`.

- **Player rig:** spawns inside `SpawnGround` near (0, 1, 4) — just east of, at (2, 1, 4), the katana "Echo" (§4, Beat 1; Appendix A.2). The player does not travel during Beat 0; the dialogue plays in place before the breach begins.
- **No enemy, no NPC, no reach point this beat.** The entire beat is a single dialogue set.

**Mission-spine step:**

| Step | Type | What happens |
|---|---|---|
| 0 | Dialogue | `"Beat0: The Cairn (the sensing, the briefing)"` — plays `Dialogue_Beat0_Briefing` (set `ch13_beat0_briefing`) in full before any traversal gate opens |

**What changes during the beat:** nothing physical. This is voice-only scene-setting — the player is nominally free to look around `SpawnGround` (and see the katana resting nearby) while the crew talks, but **nothing in the geometry actually confines the player to `SpawnGround` while that happens.** `SpawnGround` is open-flanked (§2, Appendix A.2) and the mission spine's first gate anywhere on the corridor is a `ReachTrigger` at z=34 (step 1), which *advances* the mission but does not *block* forward travel. During Beat 0's several-minute, 13-line VO, a curious player is physically free to walk the full z[-2,94] corridor and see the sealed cradles, the inactive Redactor, Heris, and Sallow before a single breach line plays — see the global no-doors consequence flagged in §9.

#### c. Art & Environment Instantiation → (none — borrows Beat 1's `SpawnGround` art)

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(no beat-owned geometry)* | — | — | — | — |

#### d. Combat

None.

#### e. Dialogue / VO

`Dialogue_Beat0_Briefing`, set **`ch13_beat0_briefing`**, position (0, 1, 4) — 13 lines. Speaker order: Cassie-04 → Sable → Cassie-04 → Kessler → Sable → Coral Vex → Kessler → Morrigan (×2, the second naming Heris) → Coral Vex → Mera Voss → Echo (private) → Ronin-7 (sets the heading: *"Take us to her."*). Advanced on **Left-Hand "Talk" (Y)** via `PromptInputAdvancer`, identical convention to every other chapter.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics this beat — it is pure dialogue.
- `SpawnLight` (accent light, behaviour `None`) is the only lighting cue active; no event light exists to punctuate the reveal.
- **The sensing's simultaneity has only VO to carry it.** §3 already notes the dropped table sensing-event SFX (no war-room, no holo). With Cassie-04 and Sable both voice-only, the production note's core staging — "two people freezing at the same nothing, before any name is put to it" (script ~line 217) — lands entirely through the beat's first two clips, `ch13_beat0_briefing_00_cassie04` and `_01_sable`. Worth directing the VO-mix pass to butt or slightly overlap those two clips (or give them a shared soft stinger in place of the dropped table SFX), so the simultaneity the Act IV cold-open depends on is audible rather than reading as two sequential status reports.
- Comfort vignette is inert — the player is stationary throughout.

---

### Beat 1 — The Sterile Vault (Breach / Traversal)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. No primitives, no hardcoded sizes. Read all environment prefabs from `ArtAssetRegistry`. Create **`BuildBeat1Art()`** (`SpawnGround` shell + `SpawnLight` + the katana "Echo" — resolving the Beat 0 borrow, see below — plus `OuterCorridor` + `Nurseries` shells, the design-ward row, the sealed-nursery row) and **`BuildBeat1Logic()`** (lab-security spawns + hive cascade, the two reach points, the breach and recognition dialogue, the `DefeatEnemies` step).
> **Ownership fix:** Beat 0's own instruction (above) states it "borrows `SpawnGround`'s geometry, built by Beat 1's art pass" — but the art table below previously omitted `SpawnGround` and the katana entirely, leaving both orphaned from any `BuildBeatNArt()`. They are now listed as the first three rows of `BuildBeat1Art()`'s table so every element in Appendix A.2 maps to exactly one instantiation site.

#### a. Narrative purpose & emotional target

The breach is the tonal inverse of every descent before it — the horror here is cleanliness, not rot. Echo's line frames it exactly: *"Men didn't kill people here. Men made people here, on tables, with charts, and then they scrubbed it down and felt good about a job done right."* Morrigan's comm bark lands the saga's one-doctor-per-make rule explicitly ("there was never one architect of all of us… every make had its own doctor, working alone… this woman built exactly one make, and it's yours"), while Coral Vex and Vess — speaking from a maker-lab that isn't even their own — deepen the design-horror from outside it. The beat's emotional spine is Echo's slow, resisted recognition, staged as *"I've been trying not to say this since the second ward and I can't hold it anymore"* — the realization that the sealed nurseries are the room both Ronin-7 *and* Echo were made in, landing only once he can no longer avoid it.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1Logic()`

All logic components would parent to `[BEAT_1_LOGIC]`.

- **Player:** travels on foot, continuous locomotion + snap-turn, from `SpawnGround` through `OuterCorridor` into `Nurseries`. No scripted waypoint path.
- **Lab security spawns** — three built **inactive**, flipped active as a group by the `DefeatEnemies` step:

| Role | Position | Notes |
|---|---|---|
| Lab Security 1 | (-3, 0, 22) | west flank, inside the design-ward corridor |
| Lab Security 2 | (3, 0, 22) | east flank |
| Lab Security 3 | (0, 0, 27) | mid-corridor, pushing toward the Nurseries boundary |

Each: `BuildEnemy(pos, playerHealth, labSecurityDef)` — root `GameObject` + `Health` + `MeleeAttacker`, generic capsule body via `ArtPrefabRegistry.TryInstantiateOrFallback(EnemyFootPrefabPath, …)`. `EnemyDefinition` `Ch13LabSecurity.asset`: `maxHealth 50, damage 8, moveSpeed 1.4, attackCooldown 0.9`.

- **`SterileHiveCascade`:** a `HiveCascadeController` wired over the three lab-security `MeleeAttacker`s (built **active**; harmless while its members are inactive — the `DefeatEnemies` step's auto-activation starts the fight). Mirrors `Ch12BuildHiveCascade`'s Attacking/Frozen/Conflicted desync retrofit — per the builder's own comment, *"the suppression science that fractured Ch12's cradle sentinels was scaled FROM these sterile levels."*
- **Reach points:**

| Object | Position | Radius |
|---|---|---|
| `DesignWardsReachPoint` | (0, 1, 34) | 5 m |
| `LabThresholdReachPoint` | (0, 1, 60) | 5 m *(gates into Beat 1B)* |

**Both of Beat 1's dialogue anchors sit forward of where their step actually fires (as-built gap).** `Dialogue_Beat1_Breach` (step 2) is anchored at (0,1,12), but it only plays after the `DesignWardsReachPoint` gate at z=34 (step 1) fires — so Echo's "look at it, the trays laid out straight" and Morrigan's "Look at the design-wards **as you pass them**" start only once the player has already walked the entire z{12–30} design-ward row to its far end, from an anchor ~22 m behind them. `Dialogue_Beat1_Recognition` (step 4) has the same shape one beat later: anchored at (0,1,44), mid-`Nurseries`, but triggered on the lab-security `DefeatEnemies` step, which resolves back at z≈22–30 — so Echo's "this is the room… where they made you" plays from ~10–15 m ahead, among cradles the player hasn't reached yet. Either accept both as gate/kill-then-narrate compression, or move the anchors toward the middle of the span each line describes (`Dialogue_Beat1_Breach` toward z≈22, `Dialogue_Beat1_Recognition` toward z≈34) so the "as you pass" commentary lands while the row it names is actually in front of the player. Worth resolving because the design-ward row is the MID clinical-horror tier (§3) the chapter's tiered reveal is built on — VO landing after the row is behind the player weakens that structure.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 1 | ReachTrigger | `"ReachTrigger: The Design Wards"` — gates on `DesignWardsReachPoint` (0,1,34), radius 5 |
| 2 | Dialogue | `"Beat1: The Sterile Vault (breach begins)"` — `Dialogue_Beat1_Breach` (set `ch13_beat1_breach`) |
| 3 | DefeatEnemies | `"DefeatEnemies: Lab Security"` — waits for all three lab-security `Health` components to reach zero |
| 4 | Dialogue | `"Beat1: The Nurseries (Echo's dawning recognition)"` — `Dialogue_Beat1_Recognition` (set `ch13_beat1_recognition`) |
| 5 | ReachTrigger | `"ReachTrigger: The Lab Threshold"` — gates on `LabThresholdReachPoint` (0,1,60), radius 5, the boundary into Beat 1B |

**What changes during the beat:** the three lab-security constructs go from inactive to active at step 3, then die; nothing else in the set dressing changes.

#### c. Art & Environment Instantiation → `BuildBeat1Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `SpawnGround` shell (12×12) *(Beat 0's borrowed geometry — owned here)* | center (0,0,4) | `Rooms.SterileVault_SpawnShell` | `…/Art/Generated/Rooms/SterileVault_SpawnShell.prefab` | **MISSING** |
| Katana "Echo" | (2, 1, 4), rot Euler(-90, 0, 0) | `Named.Echo` *(nominal — see note)* | `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `SpawnLight` (accent) | (0, 2.4, 4) | — | `ChapterEnvironmentProfile.accentLights["Spawn"]` | profile |
| `OuterCorridor` shell (12×24) | center (0,0,22) | `Rooms.SterileVault_CorridorShell` | `…/Art/Generated/Rooms/SterileVault_CorridorShell.prefab` | **MISSING** |
| `OuterCorridor_WallW` | (-6, RoomH/2, 22) | — | primitive wall (Appendix A) | **MISSING** |
| `OuterCorridor_WallE` | (6, RoomH/2, 22) | — | primitive wall (Appendix A) | **MISSING** |
| `OuterCorridor_WallS` *(south end-cap, at the chapter's spawn boundary)* | (0, RoomH/2, -2) | — | primitive wall (Appendix A) | **MISSING** |
| Design ward row — `DesignTable` ×4 | x=-4.5, y=0.5, z ∈ {12,18,24,30} | `Props.DesignTable` | `…/Art/Generated/Props/DesignTable.prefab` | **MISSING** |
| Design ward row — `InstrumentTray` ×4 | x=4.5, y=0.4, z ∈ {12,18,24,30} | `Props.InstrumentTray` | `…/Art/Generated/Props/InstrumentTray.prefab` | **MISSING** |
| `Nurseries` shell (12×26) | center (0,0,47) | `Rooms.SterileVault_NurseryShell` | `…/Art/Generated/Rooms/SterileVault_NurseryShell.prefab` | **MISSING** |
| `Nurseries_WallW` / `_WallE` | (∓6, RoomH/2, 47) | — | primitive walls (Appendix A) | **MISSING** |
| Sealed nursery row — `SealedCradle` ×10 | x=∓5, y=0.6, z ∈ {36,41,46,51,56} | `Props.SealedCradle` | `…/Art/Generated/Props/SealedCradle.prefab` | **MISSING** |
| Lab Security ×3 | see logic table | `Enemies.SterileLabSecurity` | *(no chapter-specific registry key exists yet — resolves via the shared, already-live `EnemyFootPrefabPath` capsule fallback, not `ArtAssetRegistry`)* | **MISSING** *(registry)* / capsule active |
| `CorridorLight0` / `CorridorLight1` | (-2,2.6,16) / (2,2.6,26) | — | `ChapterEnvironmentProfile.accentLights["Corridor0/1"]` | profile |
| `NurseryLight0` / `NurseryLight1` | (-3,2.4,40) / (3,2.4,54) | — | `ChapterEnvironmentProfile.accentLights["Nursery0/1"]`, `NurseryLight0` carries `AmbientPulse(7.6s)` | profile |

**Note on the katana row's "Registry Key" column.** Unlike the room and prop rows above and below it, `Named.Echo` is not actually consulted by `ArtAssetRegistry.Resolve` — as-built, the katana is `BuildSword((2,1,4), Euler(-90,0,0), weapon, Ch13EchoBladePrefab)` (`Chapter13Builder.cs:189`), which passes `Echo.prefab` straight in as the blade's *visual*, not as a registry lookup key (Appendix A.2 has this right). The row lists `Named.Echo` here only because it is the same on-disk asset the registry would eventually resolve to, not because the sword instantiates through the registry path the room/prop rows are migrating to. A future refactor should not try to route the sword through `ArtAssetRegistry.Resolve` — `BuildSword`'s direct prefab parameter is the correct mechanism to keep.

**Note on tiering.** The design-ward row (`DesignTable`/`InstrumentTray`) and the sealed-nursery row (`SealedCradle`) are the physical read of the dialogue script's MID and INNER clinical-horror tiers respectively — clean tables and trays first, then the small sealed cradles "in filtered air." Both rows are simple linear-spacing loops (`Ch13BuildDesignWardRow`, `Ch13BuildSealedNurseryRow`) with no per-instance variation; a prefab pass should consider adding rotation/offset jitter so the row doesn't read as a printed grid, but that is a discretionary improvement, not a spec requirement.

#### d. Combat

Player damage output is via `BladeDamager`'s EMA swing-speed model (**existing system — reuse, don't reinvent**). Three lab-security `MeleeAttacker`s (mook-tier, `maxHealth 50`) fight under a `HiveCascadeController` (`SterileHiveCascade`), giving the trio the same Attacking/Frozen/Conflicted state desync introduced for Ch12's cradle sentinels — reinforcing, mechanically, that this lab is the same suppression science's point of origin. The corridor's 12 m width (versus Ch1's tight 4 m airlock) gives more room to maneuver than the saga's earlier chokepoint fights. The trio spawns at z{22,22,27} — directly inside the design-ward row's z{12–30} span (§4c) — so the `DesignTable`/`InstrumentTray` line at x=±4.5 doubles as sightline-breaking cover for the fight, not just MID-tier set dressing; a prefab pass should keep the tables at a body-blocking height rather than reducing them to knee-high greybox, so the row still reads as cover once it's a commissioned asset.

#### e. Dialogue / VO

Both sets advance on **Left-Hand "Talk" (Y)**:

- **`ch13_beat1_breach`** (`Dialogue_Beat1_Breach`, (0,1,12), 5 lines) — Echo's assessment of the clean horror, then four comm-only barks (Morrigan on the one-doctor-per-make rule, Mera Voss reading the thin security as bait, Coral Vex asking what the nurseries look like, Vess marking the room as "close enough" to the sky-cage's origin).
- **`ch13_beat1_recognition`** (`Dialogue_Beat1_Recognition`, (0,1,44), 2 lines) — Echo's dawning recognition ("this is the room… where they made you… where they made me to ride inside you") and Ronin-7's steadying reply, closing on *"Where's the threat Mera felt"* — the hand-off into Beat 1B.

#### f. Audio / Haptics / VR Comfort

- No camera shake at any point. Melee feedback against the lab-security trio runs through `Haptics`, `AudioDirector`, and the `CombatFeedbackController` reticle.
- `NurseryLight0`'s `AmbientPulse(7.6s)` is the only moving light in the beat — a slow "filtered sterile-blue breathing" against the otherwise-static `Corridor`/`Nursery` accent pair.
- `ReverbZonePlacer.AutoTagInteriorVolumes()` + `PlaceReverbZonesForInteriorVolumes()` run once at the end of the build, auto-tagging all five zones — the Outer Corridor and Nurseries read as distinct, larger interior volumes than the tighter Lab Core/Annex further in.
- Comfort vignette engages normally on player snap-turns through the corridor and nursery rows.
- **Missing reveal cue, flagged for Beat 1B (as-built gap).** The screenplay's hand-off out of this beat is a specific visual beat: *"the clean light ahead flickers, once, wrong, as something heavy moves between him and the door"* (script ~line 337) — the Redactor's reveal is meant to read through a light flicker before the mini-boss fight starts. Nothing in this beat or the next authors that flicker; see §4 Beat 1B/f for the buildable fix.

---

### Beat 1B — The Redactor (Mini-Boss)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Create **`BuildBeat1BArt()`** (none — the fight happens inside the already-built `LabCore` shell) and **`BuildBeat1BLogic()`** (the Redactor spawn, its `DefeatEnemies` step, the intro/aftermath dialogue).
> **The Redactor drops no blade-shadow on defeat.** Per the source script, it is a construct, not a kept operative — do not wire a shadow-free step here; this is a deliberate narrative contrast against every earlier keeper-boss.

#### a. Narrative purpose & emotional target

The chapter's only true fight before the reckoning, and its meaning is thematic before it is mechanical: a Program enforcer sent to silence the maker before the crew can reach her — proof that Heris is now a target too. The Enforcer speaks *only* in clipped erasure-cant ("Source flagged for redaction… Obstruction will be cleared and unlogged"), "the Program's idea of a voice with nobody in it." Echo and Morrigan frame the fight's real stakes over comm: the Redactor doesn't want to fight Ronin-7, it wants to get past him to the door — *"Every second you spend trading hits is a second it spends inching toward her."* Its defeat is pointedly anticlimactic in exactly one respect: *"No shadow came off it… It was never a person… just a thing they built to make people stop being people."* That absence is the chapter's first quiet contrast against the keeper-bosses of Ch9–Ch12, all of whom left something behind.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat1BLogic()`

All logic components would parent to `[BEAT_1B_LOGIC]`.

- **The Redactor:** `BuildEnemy(new Vector3(0f, 0f, 62f), playerHealth, redactorDef)`, named `"Redactor"`, built **`SetActive(false)`**. `EnemyDefinition` `Ch13Redactor.asset`: `maxHealth 180, damage 16, moveSpeed 1.3, attackCooldown 1.0` — heavier than a lab-security mook, lighter than a full act-boss (Ch9's Vane, Ch11's Aldric). It stands at the `LabCore` zone's southern edge, just past the `LabThresholdReachPoint` gate (z=60) and well short of Heris herself (z=72) and the `SterileTable` (z=68) — the "threshold" the screenplay describes.
- **Reveal-timing gap, paired with the missing flicker cue (§4f below).** The Redactor spawns at z=62 — only ~2 m past both the `LabThresholdReachPoint` gate (z=60) and the `Dialogue_Beat1B_EnforcerIntro` anchor (z=60) — so as-built it effectively materializes on top of the player the instant the threshold is crossed, with no alcove and no light cue. This undercuts the screenplay's staging, where the Redactor "unfolds out of a sterile alcove" (script ~line 337) only after the clean light ahead flickers once, wrong; see the flicker-cue fix below.
- **Flagged mechanic gap (as-built).** Per the class summary: *"no special 'protect the door' tracking is added (out of scope for a minimal-code pass; the dialogue frames the stakes)."* The screenplay describes the Redactor throwing area-denial "silence-fields" that suppress player abilities and repeatedly trying to slip past the player toward a "sealed core door" that the player must body-block. **None of this is mechanized.** There is no silence-field ability, no door-protect tracking, and — critically — **no physical sealed core door object exists in the builder at all.** The fight is authored as one plain `DefeatEnemies` step against a single heavier-stat `MeleeAttacker`; the door-protect stakes live entirely in the dialogue's framing, not in gameplay systems. Flagging for awareness, not proposing a change (mirrors Ch1's flagged trooper narrative/mechanic gap).
- **This is not just a screenplay framing gap — it is an active audio-vs-geometry contradiction in shipped VO.** Four lines in the already-recorded `Chapter13Lines.cs` (not screenplay-only) narrate a physical door the player is standing in front of: Echo — *"It's going to try to go around you to the door. Don't let it… Put the double on the door."* — and Morrigan — *"It wants the door… Block the door, make it come to you."* — both in `ch13_beat1b_enforcer_intro`; Ronin-7 closes the same set with *"only one of us walks out of this door"*; and Echo's aftermath line in `ch13_beat1b_enforcer_defeated` states flatly, *"The door's open. She's right through it."* As-built there is no door object anywhere in `LabCore` (§2) — the shipped VO instructs the player to block a physical object, then narrates its state, in a room that does not contain one. This is the single strongest buildability/immersion argument for the §9 door patch, stronger than the screenplay-staging argument alone.

**Reveal-timing consequence of the missing door (as-built).** Dr. Heris is built **active** at (0,0,72) — see §4 Beat 2 — inside the same `LabCore` volume as the Redactor fight (z≈60–62), with no cross-wall or door of any kind gating the ~12 m sightline between them (§2, §9). Because nothing hides her, she is in plain, unobstructed view for the entire mini-boss fight, well before the reckoning dialogue ever plays. This directly undercuts the fight's own stakes — "guarding the door to the maker" reads weakly when the maker is visibly standing in the open the whole time — and pre-empts the Beat 2 reveal the screenplay stages as "behind the sealed door, the maker waits." This is a concrete case for the §9 "future sealed core door" patch, or, short of that, a simple sightline blocker (a wall segment or set-dressing silhouette) between z≈62 and z≈68; it should not be read as a neutral staging detail. **The same structural gap recurs one room further in, and worse** — see §4 Beat 4b for the parallel finding on Sallow, the chapter's climactic Ally #10 reveal, which canon stages behind a sealed door the as-built also drops.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 6 | Dialogue | `"Beat1B: The Redactor (mini-boss intro)"` — `Dialogue_Beat1B_EnforcerIntro` (set `ch13_beat1b_enforcer_intro`), activates the encounter narratively |
| 7 | DefeatEnemies | `"Beat1B: The Duel (Redactor, mini-boss)"` — waits on the Redactor's single `Health` component |
| 8 | Dialogue | `"Beat1B: No Shadow Came Off It (aftermath)"` — `Dialogue_Beat1B_EnforcerDefeated` (set `ch13_beat1b_enforcer_defeated`) |

**What changes during the beat:** the Redactor flips inactive → active; once its `Health` reaches zero, the `LabCore` sits empty until Heris's dialogue begins two dialogue-player positions further north.

#### c. Art & Environment Instantiation → `BuildBeat1BArt()` (none — reuses `LabCore`)

`LabCore`'s shell and `SterileTable` prop are built as part of the chapter's world-root pass (documented under Beat 2's art table, since Heris's reckoning is what the room is *for*). The mini-boss fight adds no geometry of its own — a future patch that wants to author a "sealed core door" prop for the Redactor to guard must add both the art (a new `Props.SealedCoreDoor` registry key) and a matching logic hook; neither exists today.

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| The Redactor | (0, 0, 62) | `Enemies.ProgramRedactor` | *(no chapter-specific registry key exists yet — resolves via the shared `EnemyFootPrefabPath` capsule fallback)* | **MISSING** *(registry)* / capsule active |
| `ThresholdLight` (accent) | (0, 2.4, 62) | — | `ChapterEnvironmentProfile.accentLights["Threshold"]` | profile |

**Redactor commission brief.** The generic capsule fallback is fine for playability, but a future `Enemies.ProgramRedactor` prefab should be commissioned against the script's specific description, not invented from scratch: *"tall, white-cased, faceless but for a single recessed scanning slit, its hands tooled not for combat but for erasure"* (script ~line 337), *"of a piece with the lab until it moves"* (script ~line 343). This is what preserves the "silence given a body" read (§4a) rather than reading as a generic mook.

#### d. Combat — the mini-boss fight

Player damage output is `BladeDamager`'s EMA swing-speed model, unchanged. The Redactor is a single `MeleeAttacker` with a heavier stat block (180 HP, 16 dmg, slower 1.3 move speed, 1.0 s attack cooldown) than the lab-security trio — the intended read is a slower, harder-hitting duel rather than a swarm. **Combat mechanic note:** despite the screenplay's silence-fields/door-protect framing, this resolves as a standard one-enemy `DefeatEnemies` step; there is no ability-suppression component and Mirror (cued narratively in the dialogue as "put the double on the door") is not scripted to trigger automatically — the player's five earned abilities are simply available as normal kit, same as any other fight this chapter. **Haptic-weight note (see §4f):** the heavier stat block above is not felt through the one channel the no-camera-shake mandate leaves for combat weight — no per-enemy `Haptics` differentiation is specified anywhere in this document; see §4f for the gap this leaves.

#### e. Dialogue / VO

- **`ch13_beat1b_enforcer_intro`** (`Dialogue_Beat1B_EnforcerIntro`, (0,1,60), 5 lines) — Enforcer (erasure-cant) → Echo (private) → Morrigan (comm, spec-reading) → Enforcer (mid-fight bark — see §4 Beat 1B/f, this line cannot actually fire mid-fight as-built) → Ronin-7 ("only one of us walks out of this door").
- **`ch13_beat1b_enforcer_defeated`** (`Dialogue_Beat1B_EnforcerDefeated`, (0,1,66), 1 line) — Echo's single aftermath line naming the absent shadow and turning the player toward Heris.

The Enforcer is the only speaker in the entire chapter who is *not* a named crew member or the two new allies — its label is literally `"Enforcer"`, never given a proper name, consistent with its function as "silence given a body."

#### f. Audio / Haptics / VR Comfort

- No camera shake — combat feel is `Haptics` + `AudioDirector` stingers + `CombatFeedbackController` reticle, same as every fight in the saga.
- `ThresholdLight` (behaviour `None`) is the ambient read for the duel; no dedicated alarm/event light exists for this fight (contrast Ch1's `DockingAlarmLight`).
- **No reveal cue is wired for the Redactor's activation (as-built gap, pairs with the death-stinger gap below).** The script stages the mini-boss's entrance as a specific light beat: *"the clean light ahead flickers, once, wrong, as something heavy moves between him and the door"* (script ~line 337), and the Redactor itself as *"of a piece with the lab until it moves"* (script ~line 343) — a construct meant to be missed until it activates. As-built, `ThresholdLight` never punctuates the Redactor's inactive→active flip; the encounter simply starts. This costs nothing against the no-camera-shake / no-event-light constraints — it is a one-shot flicker on an *existing* accent light, not a new alarm light — and should be treated as the reveal-side counterpart to the death-stinger gap immediately below: as-built, the fight has no authored punctuation on either end, no visual on the reveal, no audio on the kill.
- **No death stinger is wired for the Redactor (as-built gap).** The script's sharpest combat-audio beat is the Enforcer going "dark mid-sentence… deleting itself the way it was built to delete others," but neither this section nor §7's SFX table authors an `AudioDirector` cue for it — the only sound anywhere near the fight is the ongoing `labcore_hum.wav` bed. Given the no-camera-shake mandate makes audio the primary impact channel, a short deletion/power-down `AudioDirector` stinger on the Redactor's `Health` reaching zero — distinct from the hum, "the erasure erased" — is the chapter's one authored combat-punctuation gap worth closing.
- **No haptic-weight differentiation is authored for the fight itself, either (as-built gap, the ongoing-fight counterpart to the death-stinger gap above).** §4d calls the Redactor "a slower, harder-hitting duel rather than a swarm," but this section's own combat-feel line — `Haptics` + `AudioDirector` + reticle, "same as every fight in the saga" — is identical wording to the lab-security trio's Beat 1/f entry, and no per-hit/parry `Haptics` intensity difference is specified anywhere in this document. With camera shake banned, the haptic channel is the only place a player actually *feels* the difference between the swarm and the boss; a heavier per-hit/parry `Haptics` profile for the Redactor than the trio's would let "harder-hitting duel" read as designed, not just stated in the enemy-definition stat block (§4c, Appendix A.7). As-built, the fight now has no authored punctuation at *any* point — no reveal cue, no mid-fight VO, no death stinger, and no haptic weight distinct from a mook swarm.
- **The "mid-fight bark" (§4 Beat 1B/e) cannot fire mid-fight, as-built.** All five lines of `ch13_beat1b_enforcer_intro` — including the Enforcer's indifference-offer at line 4, "Obstruction persistent. Recalculating. The architect's record ends today. Yours is not required to end. Stand aside and remain unlogged" (script ~line 363) — are authored at step 6, which completes in full before the `DefeatEnemies` duel begins at step 7. So the exchange the label implies never happens: the fight itself carries no authored VO or audio at any point during the actual trading of blows, only before it (the intro/taunt, step 6) and after it (the aftermath, step 8). This completes the "no authored punctuation on either end" thesis above one level further — a future editor should not assume this line triggers on a health threshold or mid-duel event; nothing currently ties it to one.
- Comfort vignette engages normally through the fight's snap-turns.

---

### Beat 2 — The Maker (The Reveal — Ladder A Rung 5)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Create **`BuildBeat2Art()`** (the `LabCore` shell + `SterileTable`, shared with Beat 1B) and **`BuildBeat2Logic()`** (placing Dr. Heris, the five reckoning dialogue players and their steps). **This is a pure dialogue beat — no `BuildBeat2Art()` call should add geometry beyond what Beat 1B already built.**

#### a. Narrative purpose & emotional target

The chapter's true revelation, delivered as a reckoning, never a monologue, and never a confession-with-forgiveness. Dr. Heris — "slight, upright, fifties into sixties… sharp tired eyes carrying a guilt she has stopped trying to hide and refuses to perform" — admits across five separate dialogue sets, never one dump: she built Ronin-7; she built his killswitch; she planted the flaw in his suppression on purpose ("I did not sabotage someone else's work. I sabotaged my own"); she was then conscripted to scale the same science into the Concord Engine and hid a deniable seam in that too; she has held its location for years "on the single chance that the man I broke open would survive long enough to come back." This closes **Ladder A, rung 5** — the killswitch ladder opened in Ch2, sabotaged in Ch4, named-an-insider in Ch6, traced in Ch7 — delivered exactly once, across `ch13_beat2_opening`/`the_flaw`/`the_engine`/`the_ground`. The beat also resolves the Ch8 vision: Heris's raised gloved hands and her whispered *"I hope you will save us"* are the faceless woman Cipher has carried since the Garden. **The birth name "Soren" is never used anywhere in this chapter** — it is reserved for Ch16 and is directly regression-tested (`Chapter13LinesTests.NoLine_MentionsSoren`).

#### b. Mission Logic, Triggers & Blocking → `BuildBeat2Logic()`

All logic components would parent to `[BEAT_2_LOGIC]`.

- **Dr. Heris:** `Ch13PlaceStoryNpc(Ch13HerisPrefab, new Vector3(0f, 0f, 72f), "Dr. Heris")` — `InstantiateNpc` + `FitNamedCharacter` (grounds her root Y) + a `StoryNpc` component with `displayName = "Dr. Heris"`. Built **active from scene start** (mirrors Ch12's Vale precedent for a non-combat story NPC present as soon as the room is reachable) — she is standing at her position the moment the player enters `LabCore`, well before the Redactor fight resolves (see the reveal-timing note in §4 Beat 1B). **No `NpcWalker`, no travel, no combat component.**
- **Facing gap (as-built, blocking).** `InstantiateNpc` (`ChapterSharedBuilders.cs:648`) sets only `instance.transform.position` — it never touches rotation, so `Ch13PlaceStoryNpc` leaves Heris at the `Dr-Heris.prefab`'s native identity rotation (forward = +Z). The player approaches from −Z the entire chapter (spawn z=4 → z≈90), so as-built, Heris presents her **back** to the player for the reckoning, directly contradicting the screenplay's "faces Ronin-7 across a sterile table." Target fix: `Ch13PlaceStoryNpc` needs a rotation parameter, and Heris must be placed with `rot = Euler(0, 180, 0)` (facing −Z) — see the corrected art-table row below and the staging note.
- **Prefab-resolution gap (as-built, blocking — the reckoning currently plays against a capsule, not Heris).** `Ch13HerisPrefab` (`Chapter13Builder.cs:93`) is the flat path `Assets/Ronin7/Art/Generated/Characters3D/Named/Dr-Heris.prefab`. No file exists there — the only `Dr-Heris.prefab` on disk lives one level down, at `Named/Khall_Assets/Dr-Heris.prefab`. `AssetDatabase.LoadAssetAtPath` returns null, so `InstantiateNpc` (`ChapterSharedBuilders.cs:653-665`) falls through to its capsule fallback: the reckoning centerpiece currently renders as a primitive literally named `"Dr. Heris_Placeholder"`, not the baked character. (`Sallow.prefab` and `Echo.prefab` **are** correctly flat under `Named/` — verified — so only Heris is affected.) Fix: either move the prefab to `.../Named/Dr-Heris.prefab`, or repoint `Ch13HerisPrefab` at the `Khall_Assets/` subpath — see the §8 checklist item and Appendix B.
- **Dropped stage business — Heris's gloved-hands lift (as-built omission, mirrors §4 Beat 4b's Sallow flag).** The dialogue script's single most important stage direction in the whole chapter sits inside this beat: *"Heris lifts her gloved hands, slightly… and the motion catches the surgical light exactly as it did in a vision the player has carried since Ch8. RONIN-7 goes still"* (script ~line 419), landing right as `ch13_beat2_the_vision`'s VO quotes "you're looking at my hands." This *is* the Ch8-vision payoff this section's narrative purpose (§4a) calls the chapter's true revelation — yet even once the prefab-resolution gap above is fixed, Heris has no `Animator` (§4f) any more than Sallow does, so as-built the gesture never happens: the hands stay still and the line plays over a static (and, until both gaps above land, back-facing and capsule-bodied) placeholder. §4 Beat 4b flags Sallow's chest-cradle/palm gesture as "the single highest-value bit of authored motion a future animation pass could add" without mentioning this one at all — that framing should be read as a tie, not Sallow-first; see the amendment there. Three dependencies before this gesture could land as staged: (a) the prefab-resolution fix above, so it is Heris and not a capsule; (b) the facing fix above, so Heris is looking at the player when her hands lift, not away from him; and (c) `CoreLight0` — the "brightest, most surgical" white accent at z=68 (§4c below) — must be the light catching the gloves, since the vision's specific image is the surgical light on her hands, not any other accent in the room.
- **Costume and scale check, once the mislocation is fixed (art fidelity — not yet asked of anyone).** Resolving the prefab path is necessary but not sufficient: the baked `Dr-Heris.prefab` must be spot-checked to actually wear **visible gloves** and the **high-collared sterile lab coat over a dark under-suit** the script specifies (script lines 57–58), not just resolve by name. The entire Ch8-vision payoff (§4b above) is "her gloved hands catching the surgical light" — a resolved-but-gloveless prefab silently kills the marquee gesture even after the facing and animation dependencies land. §3.2 already gives `Named.Sallow` a height spot-check ("~1.8 m, the 1u=1m VR check") once baked, but no equivalent exists for Heris — even though `FitNamedCharacter` grounds Y without constraining scale, and the entire reckoning is staged "across a sterile table" at ~1 m of eye contact. The same spot-check applies here: confirm `Dr-Heris.prefab` reads at **~1.6–1.7 m** ("slight, upright, fifties into sixties," script line 55), so a mis-scaled asset (towering or shrunken across the table from the player) is caught alongside the gloves/coat check. §8 item 9 and §4c's status table verify only that the prefab *resolves*; this is the added check that it *reads* correctly, at the correct scale, once it does.
- **Player:** arrives at `LabCore` from `Nurseries` via the already-crossed `LabThresholdReachPoint` (Beat 1's gate); no additional reach point exists for the reckoning itself — the five dialogue players simply fire in sequence once the Beat 1B aftermath dialogue completes.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 9 | Dialogue | `"Beat2: The Maker (Heris opens the reckoning)"` — `Dialogue_Beat2_Opening` (set `ch13_beat2_opening`) |
| 10 | Dialogue | `"Beat2: The Flaw (she planted it on purpose)"` — `Dialogue_Beat2_TheFlaw` (set `ch13_beat2_the_flaw`) |
| 11 | Dialogue | `"Beat2: The Vision (Ch8 payoff, AUDIT FIX #4)"` — `Dialogue_Beat2_TheVision` (set `ch13_beat2_the_vision`) |
| 12 | Dialogue | `"Beat2: The Engine (the Concord Engine reveal)"` — `Dialogue_Beat2_TheEngine` (set `ch13_beat2_the_engine`) |
| 13 | Dialogue | `"Beat2: The Ground (Ladder A rung 5 closes)"` — `Dialogue_Beat2_TheGround` (set `ch13_beat2_the_ground`) |

**What changes during the beat:** nothing physical — Heris is already standing in place; this is five consecutive dialogue sets with no traversal or combat between them.

#### c. Art & Environment Instantiation → `BuildBeat2Art()` (shared with Beat 1B)

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `LabCore` shell (14×20) | center (0,0,70) | `Rooms.SterileVault_LabCoreShell` | `…/Art/Generated/Rooms/SterileVault_LabCoreShell.prefab` | **MISSING** |
| `LabCore_WallW` / `_WallE` | (∓7, RoomH/2, 70) | — | primitive walls (Appendix A) | **MISSING** |
| `SterileTable` | (0, 0.5, 68) | `Props.SterileTable` | `…/Art/Generated/Props/SterileTable.prefab` | **MISSING** |
| Dr. Heris | (0, 0, 72), rot **Euler(0,180,0)** target *(as-built: no rotation — see facing gap above)* | `Named.DrHeris` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Dr-Heris.prefab` *(no file at this path — only `Named/Khall_Assets/Dr-Heris.prefab` exists on disk)* | **MISLOCATED** — unresolved at the builder's path, falls back to a capsule (see prefab-resolution gap above) |
| `CoreLight0` (accent) | (-3, 2.6, 68) | — | `ChapterEnvironmentProfile.accentLights["Core0"]`, carries `ConsoleFlicker(seed 141)` — "brightest, most surgical" | profile |
| `CoreLight1` (accent) | (3, 2.6, 76) | — | `ChapterEnvironmentProfile.accentLights["Core1"]`, behaviour `None` | profile |
| `LabCoreAmbience` | (0, 1, 70) | — | `AudioSource`, `labcore_hum.wav` (no prefab), inner 3 m / outer 10 m falloff, vol 0.5 | audio |

**Staging note.** Heris (z=72) stands just past the `SterileTable` (z=68), which itself sits just past the Redactor's spawn (z=62) — the screenplay's "faces Ronin-7 across a sterile table" *positional* ordering reads correctly along the corridor's +Z axis without any additional logic. **Facing does not, and as-built, neither does identity.** `Ch13PlaceStoryNpc` passes no rotation, so a prefab whose forward axis is +Z — approaching player from −Z — reads backwards; the blocking is only half-correct until Heris is placed with `rot = Euler(0,180,0)` (see the facing gap above). More fundamentally, the mislocated prefab (see the prefab-resolution gap above) means there is currently no Heris mesh to face the wrong way at all — the reckoning plays against a capsule until `Ch13HerisPrefab` resolves. The gloved-hands gesture flagged in §4b above shares both dependencies — it only reads once Heris exists on screen and is looking at the player.

#### d. Combat

None. The Redactor fight (Beat 1B) is fully resolved before this beat's first dialogue line plays.

#### e. Dialogue / VO

All five sets are anchored inside `LabCore`, one meter apart along +Z (z=74 → 78), and advance on **Left-Hand "Talk" (Y)**:

**The five reckoning anchors sit north of Heris herself (as-built gap, the same scrutiny §4 Beat 1e applies to the breach/recognition anchors, not yet applied here).** Heris stands at z=72 (§4b); the five dialogue anchors run z=74 → 78 — 2 to 6 m *past* her, deeper into `LabCore`. The player confronts her "across the sterile table" (script line 87) from roughly z=66–68 facing +Z, so every reckoning line's spatial source sits *behind* Heris from the player's point of view, and the drift grows as the reckoning proceeds — `opening` at z=74 is 2 m behind her, `the_ground` at z=78 is 6 m behind her. This matters more here than for Beat 1's anchors, since Beat 2 is the dialogue centerpiece the whole chapter exists to deliver, and the two gaps compound: fix the facing gap (§4b) so Heris looks at the player, and the reckoning VO still won't localize to her mouth. Either accept as authoring compression (the same register the rest of the chapter's VO-over-static-anchor staging already uses), or pull the five anchors back to roughly z≈71–72 — at or just before Heris — so the reckoning reads as coming from the woman delivering it.

- **`ch13_beat2_opening`** (3 lines) — Heris's opening ("Ask the question you came with"); Ronin-7's "Who sabotaged my switch"; **Heris's confession, the literal delivery of Ladder A rung 5** — "I did. I built it… I did not sabotage someone else's work. I sabotaged my own" (script ~line 400, clip `ch13_beat2_opening_02_drheris`).
- **`ch13_beat2_the_flaw`** (3 lines) — Echo confirms she isn't lying; Ronin-7 demands "Why?"; Heris explains she seeded every operative she made after waking up with the same fine seam, and that Ronin-7 alone had the will to push through it.
- **`ch13_beat2_the_vision`** (3 lines) — **AUDIT FIX #4** (see below).
- **`ch13_beat2_the_engine`** (4 lines) — the Concord Engine reveal: Heris was conscripted to scale her suppression science into a galaxy-wide silencer, and hid a deniable seam in it too, held alone for years. **The set's fourth and closing line is the beat's actual prize and deserves naming alongside the summary above, not left implicit in it.** After Heris's confession, the line closes on Echo's private read to Cipher, compressing the whole reveal into the one operational fact the reckoning exists to deliver: *"The Engine has a flaw, and she is the flaw"* (script ~line 452) — the line that converts the reveal into the Engine-sabotage unlock (§4 Beat 3a) and the thread Ch16 pulls on. §7's voice-direction section already pins specific chapter-defining lines for VO casting; this is the same kind of load-bearing line and merits the same explicit cross-reference here, since it is what the beat exists to deliver.
- **`ch13_beat2_the_ground`** (2 lines) — Ronin-7 compresses the whole reveal into one flat sentence; Heris confirms it without softening.

**The chapter's central confession lives in `ch13_beat2_opening`, verified.** `Chapter13Lines.cs` (`GetBeat2OpeningLines`) confirms the set is three lines, not two: Heris's "Ask the question you came with," Ronin-7's "Who sabotaged my switch," and — as the set's third and closing line — Heris's confession, *"I did. I built it… I did not sabotage someone else's work. I sabotaged my own."* The confession is not a stray line stranded between two summaries; it is `opening`'s own payoff line, delivered before the beat ever hands off to `the_flaw`. Verified Beat 2 splits: **opening 3 / the_flaw 3 / the_vision 3 / the_engine 4 / the_ground 2 = 15 lines total** — see §7.

**AUDIT FIX #4 (correctness fix, applied in `Chapter13Lines.cs`, not the builder).** Per `Chapter13Builder.cs`'s class summary and `Chapter13Lines.cs`'s own header comment: the source dialogue script's Beat 2 Heris line originally read only *"You're looking at my hands. I wondered if you would. You've seen them before?"* — omitting the iconic *"I hope you will save us"* whisper entirely from the spoken `Line:` text (it appeared only in the voice-direction note, and Ronin-7's next line quoted it back with no antecedent). The duration was also mismatched (57 s against a ~14-word line). **Fixed:** the whisper is now restored into Heris's own line — *"You're looking at my hands. I hope you will save us. You've seen them before."* — with the duration corrected to 6 s to match the real ~15-word count at the script's own ~2.7 words/sec calibration. Regression-guarded by `Chapter13LinesTests.HerisLine_ContainsIHopeYouWillSaveUs`.

**Ladder A rung 5** is carried across the four Beat 2 sets, never delivered in one line — this document should not be read as licensing a "compress it into one line" simplification pass; the beat's own dialogue structure *is* the intended pacing.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics — this is the chapter's dialogue centerpiece, carried entirely by lighting and VO performance.
- `CoreLight0`'s `ConsoleFlicker(seed: 141)` is the room's one moving light cue, "the brightest, most surgical" light in the chapter, flickering subtly under the reckoning without ever reading as unstable enough to distract from the dialogue.
- **Dependency for a future animation pass:** the Ch8-vision gesture flagged in §4b (Heris's gloved-hands lift) must be lit by this same light — `CoreLight0`, not `CoreLight1` — since the script specifies the surgical light catching her gloves.
- `LabCoreAmbience` (`labcore_hum.wav`) loops continuously under the whole beat, a low mechanical hum consistent with "live powered hardware," per the dialogue script's note that the crew comm channel stays clean here (no depth-degradation, no haunting) because the lab is fully powered.
- **Considered and declined: a single soft haptic pulse on Heris's "goes still" beat.** The Ch8-vision recognition — Heris's gloved hands lifting into the surgical light, Ronin-7 "goes still" (script ~line 419) — is what §4a itself calls the chapter's true revelation, yet as noted above this beat is carried entirely by lighting and VO. §1.1's own thesis is that with camera shake banned, feel routes through `Haptics`/`AudioDirector`/the reticle; right now this peak has a lighting dependency (`CoreLight0` catching the gloves) and an animation dependency (the hands-lift, §4 Beat 2b) but zero authored non-visual punctuation. A single, very soft one-shot `Haptics` pulse timed to the "goes still" beat — not combat-weight, just enough to give the moment a non-visual counterpart to the gesture-lighting — would give it parity with the death-stinger (§4 Beat 1B/f) and sterile-air (§7) treatments elsewhere in this document. Offered as marginal, discretionary polish, not a requirement; if declined, the rationale is that this beat's register is stillness itself, and even a soft haptic risks reading as a mechanical interruption in the one moment written to be perfectly quiet.
- Comfort vignette is largely inert — the player is expected to stand still through five consecutive dialogue sets, same staging pattern as Ch1 Beat 2's Main Hold conversation.

---

### Beat 3 — Heris Defects (The Recruitment — Ally #9)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> No new art this beat — `BuildBeat3Art()` should be empty or absent. Create **`BuildBeat3Logic()`** for the single defection dialogue player and its step.

#### a. Narrative purpose & emotional target

Heris does not ask forgiveness; she asks for work. *"Don't pardon me. Use me. Let me help you tear down the thing I built before I die of having built it."* Ronin-7's acceptance is deliberately cold and pragmatic, never warm: *"I'm not taking you aboard because you deserve it. I'm taking you because you're right… Don't mistake a place on my ship for a pardon. You'll have one and never the other."* The recruitment unlocks **Engine-sabotage tech** keyed to the hidden seam Heris built into the Concord Engine (seeded from Ch9) — the gameplay payoff of the reckoning. **Narrative unlock only, as-built.** The builder sets flags only — `heris_recruited`, alongside `sallow_recruited`/`ch13_complete`, all via the single `ChapterOutro` trigger (§4 Beat 4b) — there is no `AbilityGranter` call and no sabotage-tech component anywhere in `Chapter13Builder.cs`, consistent with the chapter's minimal-code mandate (§1.1). Like Sallow's absolution mechanic (§9), the sabotage tool is narratively introduced here and mechanically built later, per the Ch16 turn; a future editor should not read this line as a shipped mechanic to go wire up.

#### b. Mission Logic, Triggers & Blocking → `BuildBeat3Logic()`

All logic components would parent to `[BEAT_3_LOGIC]`.

- **No new spawn, no new travel.** Heris is already standing at (0,0,72) from Beat 2; this beat is a single dialogue set with no blocking change.

**Mission-spine step:**

| Step | Type | What happens |
|---|---|---|
| 14 | Dialogue | `"Beat3: Heris Defects (Ally #9, work not forgiveness)"` — `Dialogue_Beat3_Defection` (set `ch13_beat3_defection`) |

**What changes during the beat:** narratively, Heris flips from "the maker being interrogated" to "Ally #9" — but this is not represented by any flag or component change at this step; the recruit flag (`heris_recruited`) is not set until the chapter's final `ChapterOutro` trigger (step 19), alongside `sallow_recruited` and `ch13_complete`.

#### c. Art & Environment Instantiation → `BuildBeat3Art()` (none)

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| *(no beat-owned geometry — reuses `LabCore`)* | — | — | — | — |

#### d. Combat

None.

#### e. Dialogue / VO

**`ch13_beat3_defection`** (`Dialogue_Beat3_Defection`, (0,1,80), 4 lines) — Heris's ask → Echo's honest ambivalence ("I want to hate her… it won't quite come") → Ronin-7's cold, weighted acceptance → Heris's closing "Tell me where to stand." The dialogue anchor sits at z=80, exactly on the `LabCore`/`Annex` boundary — the last line spoken in `LabCore` before the party moves into the annex for Beat 4.

#### f. Audio / Haptics / VR Comfort

No camera shake, no haptics, no new lighting cue — the beat rides the same `CoreLight1`/ambient bed established in Beat 2.

---

### Beat 4 — Sallow and the Absolution Body (The Last Reveal — Ally #10, The Ten Complete)

> **[CRITICAL CLAUDE REFACTORING INSTRUCTION]**
> Separate environment generation from mission logic. Create **`BuildBeat4Art()`** (`Annex` shell + placing Sallow) and **`BuildBeat4Logic()`** (the annex reach point, the three closing dialogue players, the `ChapterOutro` trigger that ends the chapter).

#### a. Narrative purpose & emotional target

The chapter's quiet redemptive turn. Sallow is "a blank, waxen, humanoid construct… a featureless face with dim empty eyes… a chest paneled open to show an empty AI-cradle" — an "absolution body" designed to host shadow-AIs, to bear every kept witness so a living host doesn't have to. The reframe lands on Ronin-7 himself: *"A body built to carry the dead so the living don't have to. Heris. That's what I am."* Heris confirms it without reaching for sentiment: *"Your whole make was a body designed to hold the shadows of the dead, and they meant it as a cage, and it was never only a cage… So are you, now."* Sallow speaks exactly once, "barely a voice: cracked, sparse, half-formed" — *"I will carry them. That is all I am for."* — and joins as **Ally #10**, completing the roster of ten. Echo's closing line hooks directly into the finale: *"The next door isn't a who anymore. It's the thing itself. The Engine."*

#### b. Mission Logic, Triggers & Blocking → `BuildBeat4Logic()`

All logic components would parent to `[BEAT_4_LOGIC]`.

- **Sallow:** `Ch13PlaceStoryNpc(Ch13SallowPrefab, new Vector3(0f, 0f, 88f), "Sallow")` — same idiom as Heris: `InstantiateNpc` + `FitNamedCharacter` + `StoryNpc`. Built **active from scene start**, standing in the `Annex` from the moment the room is reachable. **No `NpcWalker`, no travel, no combat component** — Sallow never fights this chapter (or, per its established character, likely ever).
- **Same facing gap as Heris (§4 Beat 2b).** `InstantiateNpc` sets position only, so Sallow is left at its prefab's native +Z-forward rotation while the player approaches from −Z. The screenplay's final-reveal blocking — Sallow "turns its blank waxen face toward him" — reads backwards as-built. Target fix: place Sallow with `rot = Euler(0,180,0)`, same as Heris.
- **Sightline gap — Sallow pre-empts the Ally #10 reveal (as-built, blocking; the Beat 1B reveal-timing finding generalized to the chapter's climactic reveal).** With zero doors and no cross-wall anywhere in the corridor (§2), Sallow at (0,0,88) sits only ~10–16 m up an open, near-fogless (density 0.006) straight line beyond Heris (z=72) — squarely in the player's forward sightline for the *entire* reckoning and defection (Beats 2–3, player at z≈70–80 facing +Z). This is a stronger case than the Heris-during-Redactor sightline note (§4 Beat 1B), because Sallow *is* the climactic reveal: the waxen absolution-body stands in plain view behind Heris while she confesses, spoiling "the ten complete" long before Beat 4 ever begins. Canon already has the fix the as-built dropped — Beat 3 closes on Heris turning to look "toward a sealed annex door at the far side of the clean room" (script ~line 496), and Beat 4 opens with her leading Ronin-7 to that door and unsealing it (script ~line 502); a sealed annex door at the z=80 `LabCore`/`Annex` boundary is canon staging, not a discretionary addition. Named here, alongside Heris (§4 Beat 1B), as the second concrete argument for the §9 "future sealed door" patch — at minimum a sightline blocker between z=80 and z=88 should be considered before the reveal is next touched. **The reveal is undercut a second way at the mechanical level, independent of the sightline.** `AnnexReachPoint` (below) sits at z=90 — 2 m *past* Sallow at z=88, and past two of the three Beat-4 dialogue anchors (`ch13_beat4_mechanic` at z=89, `ch13_beat4_complete` at z=90; only `ch13_beat4_introduce_sallow` at z=88 sits at or before the construct). So the player must physically walk up to, and slightly past, the waxen body before step 15's gate even fires and `Dialogue_Beat4_IntroduceSallow` ("the one right thing these hands ever made") ever plays — the reveal line arrives only after the reveal has already been walked past.
- **`AnnexReachPoint`:** (0, 1, 90), radius 5 — gates the transition from Beat 3's defection into Beat 4's reveal.
- **`ChapterOutro`:** built at (0, 1, 92), inactive, holding a `CampaignFlagSetter` with **three** flags — `"ch13_complete"`, `"heris_recruited"`, `"sallow_recruited"` — set together (mirrors Ch9/Ch10's combined completion + recruit-flag convention). `completeCanvas` ref = the `"CHAPTER 13 COMPLETE"` world-space canvas at (0, 1.4, 93). `OnActivated` is wired via `UnityEventTools.AddPersistentListener` directly to `flagSetter.SetFlags`.

**Mission-spine steps:**

| Step | Type | What happens |
|---|---|---|
| 15 | ReachTrigger | `"ReachTrigger: The Annex"` — gates on `AnnexReachPoint` (0,1,90), radius 5 |
| 16 | Dialogue | `"Beat4: Introduce Sallow"` — `Dialogue_Beat4_IntroduceSallow` (set `ch13_beat4_introduce_sallow`) |
| 17 | Dialogue | `"Beat4: The Absolution Mechanic (the reframe)"` — `Dialogue_Beat4_Mechanic` (set `ch13_beat4_mechanic`) |
| 18 | Dialogue | `"Beat4: The Ten Complete (Ally #10)"` — `Dialogue_Beat4_Complete` (set `ch13_beat4_complete`) |
| 19 | Trigger | `"Trigger: Chapter Outro (flags + fade + canvas)"` — activates `ChapterOutro`; sets all three flags, reveals the complete canvas, and ends the chapter |

**What changes during the beat:** Sallow is present from build time (no reveal animation is scripted — the "reveal" is entirely dialogue-carried, the player simply walking into the annex and seeing the construct already standing there). Step 19 is both the beat's and the chapter's terminus.

**Dropped stage business (as-built omission, flagged like §9's door-protect mechanic).** The screenplay gives Sallow the chapter's most affecting non-verbal moment: it touches its own open chest-cradle, extends a palm toward the katana "as if it can sense the shadows nested in the steel," and later gives "a bend at the waist that is almost a bow, almost an offering." None of this is authored — Sallow is a static prefab with no `Animator` driving it (correctly noted in §4f below), so the final reveal is carried by VO alone over an inert mannequin, and — until the facing gap above is fixed — one facing the wrong way. This is named here as an intentional as-built omission, not a regression to silently patch: the chest-cradle-touch → palm-toward-the-blade gesture is one of the two highest-value bits of authored motion a future animation pass could add — tied with Heris's gloved-hands lift (§4 Beat 2b), the Ch8 vision's marquee payoff — since it is the physical statement of the chapter's absolution-body reframe, not decoration on top of it.

#### c. Art & Environment Instantiation → `BuildBeat4Art()`

| Element | Position / Rotation | Registry Key | Resolves To | Status |
|---|---|---|---|---|
| `Annex` shell (10×14) | center (0,0,87) | `Rooms.SterileVault_AnnexShell` | `…/Art/Generated/Rooms/SterileVault_AnnexShell.prefab` | **MISSING** |
| `Annex_WallW` / `_WallE` | (∓5, RoomH/2, 87) | — | primitive walls (Appendix A) | **MISSING** |
| `Annex_WallN` *(north dead end)* | (0, RoomH/2, 94) | — | primitive wall (Appendix A) | **MISSING** |
| Sallow | (0, 0, 88), rot **Euler(0,180,0)** target *(as-built: no rotation — see facing gap above)* | `Named.Sallow` | `Assets/Ronin7/Art/Generated/Characters3D/Named/Sallow.prefab` | **EXISTS** |
| `AnnexLight0` (accent) | (0, 2.4, 87) | — | `ChapterEnvironmentProfile.accentLights["Annex0"]`, "Sallow's sterile blue," behaviour `None` | profile |
| `CHAPTER 13 COMPLETE` canvas | (0, 1.4, 93) | — | worldspace `Canvas`, `Image` bg + `Text` label, built inactive | canvas |

#### d. Combat

None. The chapter ends with zero active enemies remaining.

#### e. Dialogue / VO

Three sets, all in `Annex`, advancing on **Left-Hand "Talk" (Y)**:

- **`ch13_beat4_introduce_sallow`** (2 lines) — Heris introduces Sallow as "the one right thing these hands ever made"; Echo recognizes the hollow AI-cradle as kin.
- **`ch13_beat4_mechanic`** (3 lines) — Heris explains the absolution-body mechanic in full; Ronin-7 lands the reframe on his own body; Heris confirms ("So are you, now").
- **`ch13_beat4_complete`** (4 lines) — Echo names the weight Sallow can carry; **Sallow speaks its one line** ("I will carry them. That is all I am for."); Ronin-7 recruits it plainly ("That's nine and ten. That's everyone."); Echo closes the chapter, hooking directly into Ch16 ("There's only the cage left to break. Let's go home and aim the whole crew at it.").

**Sallow's speaker constraint.** Per both the dialogue script's production note and `Chapter13Lines.cs`'s data, Sallow has **exactly one line in the entire chapter** — do not add further Sallow dialogue in a future pass without deliberately revisiting this constraint; the character is written to be "largely non-verbal… a presence, not a talker."

**Heris's Beat 4 VO is disembodied (as-built gap, the no-travel decision's cost one beat further in than §5 states it).** Canon has Heris physically *lead* Ronin-7 into the annex: Beat 3 closes on her turning to look "toward a sealed annex door at the far side of the clean room" (script ~line 496), and Beat 4 opens with her "leads RONIN-7 to the annex door and unseals it" (script ~line 502) — she crosses the threshold with the player. As-built she never moves; `Ch13PlaceStoryNpc` places her once, at (0,0,72) in `LabCore`, and §5's travel table is correct that she is "stationary for the entire chapter." But she is also the opening speaker of `ch13_beat4_introduce_sallow` — *"the one right thing these hands ever made"* — and carries most of `ch13_beat4_mechanic`, both anchored at (0,1,88) and (0,1,89) in the `Annex`, ~16–17 m ahead of her body. So the chapter's redemptive turn is narrated by a voice with no source anywhere near it: a player looking toward the sound of Heris's own introduction of Sallow finds nothing there, because Heris is still a room behind, out of sight past the `LabCore`/`Annex` boundary. This is a consequence of the no-travel decision worth flagging explicitly, parallel to the Beat 0 diegetic-seam note (§3) — either accept it as VO-only compression (the same register the crew's comm-only presence already uses), or fix it: a second static Heris placement near z≈82 for Beat 4, or the sealed-annex-door patch §9 already contemplates, which would motivate her crossing the threshold and gives a natural point to relocate her.

#### f. Audio / Haptics / VR Comfort

- No camera shake, no haptics — the chapter's close is dialogue-only.
- `AnnexLight0`'s sterile-blue wash (behaviour `None`, no flicker/pulse) keeps the annex visually calm through the reveal, deliberately the least dramatic lighting cue in the chapter — Sallow's uncanny stillness is meant to read against a static backdrop, not a moving one.
- Standard comfort vignette applies through the final walk from `LabCore` into `Annex`.
- `ReverbZonePlacer`'s auto-tagged interior volume for `Annex` (the chapter's smallest room) should read tighter/more intimate than `LabCore`'s reverb, reinforcing that this is a small, private annex rather than the lab's main stage.

## 5. Character travel-route master table

**No NPC physically travels during Chapter 13.** This is a hard departure from every earlier chapter's convention: `Chapter13Builder.cs` contains no `BuildNpcWalker` call anywhere, and neither Dr. Heris nor Sallow is ever `SetActive(false)` and later revealed — both are placed via `Ch13PlaceStoryNpc` and are **built active from scene start**, standing at their final positions the moment their room is reachable (mirrors Ch12's Vale precedent for a stationary story NPC, taken one step further — Ch12 at least staged Vale's reveal; here there is no reveal beat at all, just presence).

| Character | Position | Built | Travel |
|---|---|---|---|
| Dr. Heris | (0, 0, 72), `LabCore` | active from scene start | none — stationary for the entire chapter |
| Sallow | (0, 0, 88), `Annex` | active from scene start | none — stationary for the entire chapter |

**Y-invariant note (applies, even without travel).** `FitNamedCharacter` still grounds both characters' root Y after scaling — the same grounding step Kessler receives in Ch1 — so a prefab swap for either the `Dr-Heris.prefab` or `Sallow.prefab` asset must keep a centered-or-consistent pivot for `FitNamedCharacter` to ground correctly; there is simply no second waypoint-Y to keep in sync, because there are no waypoints.

**Facing invariant — currently unmet (blocking, see §4 Beats 2b/4b).** With no travel, facing is set once at build time and never corrected, so it matters more here than in any chapter where an `NpcWalker` at least ends a route facing a scripted direction. As-built, `InstantiateNpc` sets position only; both NPCs sit at their prefab's native +Z-forward rotation while the player always approaches from −Z. **Target: both Heris and Sallow must be placed with `rot = Euler(0,180,0)`** — this applies at every site that calls `Ch13PlaceStoryNpc`/`InstantiateNpc` for either character, including the post-chapter Hub placement (§9).

**No-travel invariant's cost, Beat 4 (see §4 Beat 4e).** "Stationary for the entire chapter" is correct for Heris's *body*, but not for the source of her *voice* — canon has her lead Ronin-7 into the `Annex` (script ~line 502) and she is the opening speaker of both Beat 4 dialogue sets, anchored 16–17 m past where she actually stands. The table above states her position and travel status accurately; it does not by itself surface that gap, which is why it is flagged again at the VO level in §4 Beat 4e.

The Redactor and the three lab-security constructs are `MeleeAttacker`-driven combat AI, not scripted waypoint walkers — they move under their own AI once activated, with no authored path.

## 6. Lighting & background progression table

All light values below are **read from `ChapterEnvironmentProfile`**, never typed into the builder. Their current literals are in Appendix A.1.

| Beat | Zone | Mood | Accent entries | Behaviour | What changes during the beat |
|---|---|---|---|---|---|
| 0 — The Cairn (sensing) | `SpawnGround` | neutral-white, unremarkable | `SpawnLight` | `None` | none — voice-only beat |
| 1 — The Sterile Vault (breach) | `OuterCorridor` → `Nurseries` | cool white sharpening into sterile blue | `CorridorLight0/1`, `NurseryLight0/1` | `NurseryLight0`: `AmbientPulse(7.6s)`; rest `None` | lab-security trio flips inactive→active at the `DefeatEnemies` step |
| 1B — The Redactor | `LabCore` (threshold) | cool white, tightening | `ThresholdLight` | `None` (target: one-shot "wrong" flicker on activation, unbuilt — §4 Beat 1B/f) | Redactor flips inactive→active; fight resolves (target: paired death-stinger on defeat, unbuilt — §4 Beat 1B/f) |
| 2 — The Maker | `LabCore` (core) | brightest, most surgical white | `CoreLight0`, `CoreLight1` | `CoreLight0`: `ConsoleFlicker(seed 141)`; `CoreLight1`: `None` | no physical change — five consecutive dialogue sets |
| 3 — Heris Defects | `LabCore` (core, south edge of `Annex` boundary) | same as Beat 2 | same `CoreLight0/1` | unchanged | narrative-only recruitment; no lighting cue marks it |
| 4 — Sallow | `Annex` | deep sterile blue, calm | `AnnexLight0` | `None` | Sallow present from the start; `ChapterOutro` fires at the beat's end |

There is no red-alarm or event-light moment anywhere in this chapter (contrast Ch1's `DockingAlarmLight`) — the lighting arc is a single, continuous climb from neutral-white through blue-tinged sterile tones to the brightest surgical white at the reckoning, then back down to a calmer blue for the annex. Fog is the same baseline exponential bed throughout (density 0.006), never overridden per-zone.

## 7. Audio / VO manifest cross-reference

Fourteen canonical dialogue sets, defined in `Chapter13Lines.cs` and consumed via `Chapter13Lines.Get(setId)`:

| Set ID | Beat | `DialoguePlayer` position |
|---|---|---|
| `ch13_beat0_briefing` | 0 | (0, 1, 4) — `Dialogue_Beat0_Briefing` |
| `ch13_beat1_breach` | 1 | (0, 1, 12) — `Dialogue_Beat1_Breach` |
| `ch13_beat1_recognition` | 1 | (0, 1, 44) — `Dialogue_Beat1_Recognition` |
| `ch13_beat1b_enforcer_intro` | 1B | (0, 1, 60) — `Dialogue_Beat1B_EnforcerIntro` |
| `ch13_beat1b_enforcer_defeated` | 1B | (0, 1, 66) — `Dialogue_Beat1B_EnforcerDefeated` |
| `ch13_beat2_opening` | 2 | (0, 1, 74) — `Dialogue_Beat2_Opening` |
| `ch13_beat2_the_flaw` | 2 | (0, 1, 75) — `Dialogue_Beat2_TheFlaw` |
| `ch13_beat2_the_vision` | 2 | (0, 1, 76) — `Dialogue_Beat2_TheVision` |
| `ch13_beat2_the_engine` | 2 | (0, 1, 77) — `Dialogue_Beat2_TheEngine` |
| `ch13_beat2_the_ground` | 2 | (0, 1, 78) — `Dialogue_Beat2_TheGround` |
| `ch13_beat3_defection` | 3 | (0, 1, 80) — `Dialogue_Beat3_Defection` |
| `ch13_beat4_introduce_sallow` | 4 | (0, 1, 88) — `Dialogue_Beat4_IntroduceSallow` |
| `ch13_beat4_mechanic` | 4 | (0, 1, 89) — `Dialogue_Beat4_Mechanic` |
| `ch13_beat4_complete` | 4 | (0, 1, 90) — `Dialogue_Beat4_Complete` |

Each is built by the local `Ch13BuildDialogue` wrapper (not the shared clip-set loader): it calls `BuildDialoguePlayer` with `clipSetId: null`, then wires clips itself via `Ch13WireVoiceClips`, resolving each line's `AudioClip` from `Chapter13Lines.ClipName(setId, index, speaker)` — pattern `ch13_{setId}_{index:00}_{speaker_sanitized}` — under `Assets/Ronin7/Art/Generated/Audio/Voice`, trying `.mp3` first and falling back to `.wav`. A `Debug.LogWarning` fires per dialogue set if fewer clips resolve than lines exist. **Advance input for every dialogue line is the Left-Hand "Talk" action (Y button)**, resolved once via `FindRef(refs, "Left Hand", "Talk")` and shared across all fourteen `DialoguePlayer`s.

**Dialogue is data, not art.** None of this changes in the refactor — the fourteen set ids, their positions, and the clip-resolution pattern are canon.

**Voice direction — §7 resolves clip *names*, not *timbre*.** This section's clip-resolution mechanics tell a VO-generation pass which file to load; they say nothing about how a line should be read. For a chapter that is ~90% dialogue and carries its entire impact channel through VO (no camera shake, §1.1), that gap matters most for two chapter-defining voices the dialogue script directs explicitly and easily gets wrong if cast generically:

- **Enforcer** (`ch13_beat1b_enforcer_intro`/`_defeated`) — *"a flat synthetic erasure-cant, no warmth and no person… every word a deletion"* (script ~line 350). It is the only speaker in the chapter who is neither crew nor an ally (§4 Beat 1B/e already notes this structurally; this is the timbre it should carry).
- **Sallow** (`ch13_beat4_complete`, its one line) — *"a cracked half-formed synthetic whisper… more presence than person"* (script ~line 543).

For the reckoning, Heris's register is already load-bearing enough to be pinned down: *"cold precision… no drama, no plea"* through the confession (script ~line 402), softening only once, for the AUDIT FIX #4 line, into *"low and unhedged… never wept"* (script ~line 424) — never begging, never performed. A future VO-generation pass working from this document alone gets none of this; it should be pointed at `Ch13_The_Sterile_Reckoning_Dialogue_Script.md`'s per-line `voice:` notes rather than casting from the transcript text alone.

**Beat 2 per-set line-splits — verified against `Chapter13Lines.cs`.** The builder itself wires whole dialogue sets by id (`Ch13BuildDialogue`/`Ch13WireVoiceClips`) and has no visibility into which individual lines a given `setId` contains — but `Chapter13Lines.cs`, where that per-line breakdown lives, is present in this checkout at `Project/Assets/Ronin7/Scripts/Editor/Chapter13Lines.cs`. Cross-checking each `GetBeat2*Lines()` method confirms the split: **opening 3 / the_flaw 3 / the_vision 3 / the_engine 4 / the_ground 2 = 15 Beat-2 lines** (see §4 Beat 2e). Every other set's line count in this document (13/5/2/5/1/4/2/3/4 for the non-Beat-2 sets) is likewise confirmed against source.

**Comm-tagged speaker labels.** "Morrigan (comm)", "Mera Voss (comm)", "Coral Vex (comm)", "Vess (comm)" in the source dialogue script are recorded under their plain name in `Chapter13Lines.cs` — "(comm)" is stage direction, not part of the speaker identity, matching every other chapter's convention.

SFX bed, all under `Assets/Ronin7/Art/Generated/Audio`:

| Clip | Used for |
|---|---|
| `SFX/labcore_hum.wav` | `LabCoreAmbience` — 3D positional ambience at (0,1,70), inner radius 3 m, outer 10 m, vol 0.5 |

**No other SFX clips are wired by this builder.** There is no door-slide clip (no doors), no alarm/klaxon clip (no event light), no hologram-reveal clip (no hologram this chapter), and no chapter-wide 2D ambience bed on the `Game` root (contrast Ch1's `OnFootAmbience.wav`). The audio manifest for Ch13 is deliberately the sparsest in the saga so far — the clinical silence *is* the sound design.

- **Exception worth naming: no death stinger on the Redactor.** See §4 Beat 1B/f — the one authored combat-audio beat this chapter's script gives ("deleting itself the way it was built to delete others") has no `AudioDirector` cue; this is a gap, not a silence choice, and is called out separately from the deliberate sparseness above.
- **Considered and declined: a sterile-air ambient layer.** The dialogue script repeatedly physicalizes the space through air — "the air itself filtered to nothing," "every surface scrubbed to a hush," "filtered air." A near-subliminal filtration/HVAC hiss in `OuterCorridor` + `Nurseries`, well below the `LabCore` hum, would make "filtered to nothing" a felt sensation without breaking the clinical silence above. Offered as marginal, discretionary polish only — noted here as considered-and-declined (unless tuned very low) so the sparse-SFX choice above reads as deliberate rather than an oversight.
- **Considered: a hard-clinical footstep surface for the corridor floor.** Neither this table nor `Chapter13Builder.cs` says anything about locomotion footstep audio. "Pale grey panel and clinical steel… every surface scrubbed to a hush" (script line 44) is a floor-material read as much as a wall one, and a crisp, hard, reverberant tile footstep — played against the auto-tagged interior reverb zones §4f already places — would physicalize the sterile hardness for free if the global footstep system is surface-tagged (no new system, just a surface tag on Ch13's floor material). Note whether Ch13's floor is tagged for a hard-clinical footstep surface or left on the default, the same considered-and-declined register as the sterile-air layer above, so the choice reads as deliberate either way.

## 8. Build & verification checklist

1. **Build:** run the Unity menu item **Tools → Space Samurai → Chapters → Build Chapter 13 — The Sterile Reckoning** (`XRRigBuilder.BuildChapter13SterileReckoning()`, menu priority 213).
2. **EditMode is the gate.** Project baseline is **842 tests green, 0 skips**; PlayMode is **70/70 green**. Every open scene must be saved before running tests — a dirty scene aborts the `tests-run` MCP call.

   > ⚠ **Coverage blind spot.** No EditMode test invokes `BuildChapter13SterileReckoning()` or loads `Ch13_SterileReckoning.unity`. Coverage is limited to `Chapter13LinesTests.cs` (pure dialogue-data assertions) — **a green suite says nothing about whether the scene still builds correctly.** Every structural change in this refactor must be verified by opening the scene and looking at it.

3. **`Chapter13LinesTests.cs` coverage (what the suite actually checks):** `EverySetId_ReturnsNonEmptyArray`, `EveryLine_HasSpeakerTextAndPositiveSeconds`, `NoLine_ContainsAnEmDash`, `ClipNames_AreUniqueAcrossAllSets`, `EverySpeaker_IsInKnownCast`, `EveryBuilderReferencedSetId_ExistsInSetIds`, `UnknownSetId_ReturnsEmptyArray`, `NoLine_MentionsSoren` (the Ch16 birth-name reservation guard), and `HerisLine_ContainsIHopeYouWillSaveUs` (the AUDIT FIX #4 regression guard, §4 Beat 2e). **Any patch to `Chapter13Lines.cs` must keep all nine green**, especially the last two — they are the only tests that enforce a specific narrative constraint rather than a generic data shape.
4. **Safe-zone survival test (new).** Build fresh once. Manually add a child GameObject under `[STATIC_ART_DO_NOT_DELETE]`. Build fresh again. **The child must still be there.** If it is gone, the wipe strategy was not converted (§1.4) and the safe zone is decorative.
5. **Fallback audibility test (new).** With an empty `ArtAssetRegistry`, a fresh build must produce the **complete greybox chapter** (Appendix A geometry) plus one `LogWarning` per unresolved key — never an empty room, never an exception. This includes the two enemy-body registry keys (`Enemies.SterileLabSecurity`, `Enemies.ProgramRedactor`), which today fall back through the separate, already-live `ArtPrefabRegistry`/`EnemyFootPrefabPath` mechanism rather than `ArtAssetRegistry` — verify both fallback paths independently.
6. **Perf reference bar.** No chapter-specific `UnityStats` baseline exists yet for Ch13 (§1.6) — **capture one on the next build** (`drawCalls`/`setPassCalls`/`tris`/`verts` at greybox) and record it in `Project/Docs/CHAPTER-BUILD-LEDGER.md` before this document can claim a numeric bar. The 72 Hz floor is not negotiable regardless.
7. **Console check:** `Ch13WireVoiceClips`'s per-set warning (`only N/M voice clips resolved for set '…'`) is the fast signal that a VO batch didn't fully land — check `console-get-logs` after a rebuild. Fourteen sets means fourteen possible warnings; check all of them, not just the first.
8. **Ally-flag check:** confirm all three `CampaignFlagSetter` flags (`ch13_complete`, `heris_recruited`, `sallow_recruited`) fire together off the single `ChapterOutro.OnActivated` listener (step 19) — a patch that splits Heris's and Sallow's recruitment into separate flag-setters would desync the Hub's gate list (`HubBuilder`'s SurgeryReactor room, already wired to `"ch13_complete"`, expects both allies to land at once).
9. **Dr. Heris prefab-resolution fix (new, top priority).** `Ch13HerisPrefab` (`Chapter13Builder.cs:93`) points at `Assets/Ronin7/Art/Generated/Characters3D/Named/Dr-Heris.prefab`, but the only baked file on disk is `Named/Khall_Assets/Dr-Heris.prefab` — the reckoning currently renders against a capsule fallback named `"Dr. Heris_Placeholder"`, not the character (§4 Beat 2b, Appendix B). Before this document's `Named.DrHeris` status can read `EXISTS`, either move the prefab to the flat `Named/` path (matching `Sallow.prefab`/`Echo.prefab`'s precedent) or repoint the `Ch13HerisPrefab` constant at the `Khall_Assets/` subpath. Verify by rebuilding and confirming the scene hierarchy shows `"Dr. Heris"`, not `"Dr. Heris_Placeholder"`, at (0,0,72) — then confirm the mesh itself visibly wears gloves and the high-collared sterile lab coat (§4 Beat 2b), since a name-only check can pass on a mislabeled or wrong-costume asset. **Also confirm scale**, alongside costume: `Dr-Heris.prefab` should read at ~1.6–1.7 m (§4 Beat 2b), matching the height spot-check already required of `Sallow.prefab` (§3.2) — a mis-scaled Heris reads as physically wrong in exactly the way §1.1 warns about, and the reckoning is the one scene where the player stares at her at conversational range across the `SterileTable`.

## 9. Additive-only cautions & open questions

- **Dr. Heris resolves to a capsule, not a character (correctness — the single highest-impact buildability error in this document).** `Ch13HerisPrefab` (`Chapter13Builder.cs:93`) reads the flat path `Named/Dr-Heris.prefab`; the only baked file on disk is one level down, at `Named/Khall_Assets/Dr-Heris.prefab`. `InstantiateNpc` therefore falls through to its capsule fallback, and the chapter's reckoning centerpiece currently plays against a primitive named `"Dr. Heris_Placeholder"` — not the baked character. `Sallow.prefab` and `Echo.prefab` are unaffected (both correctly flat under `Named/`). Fix by moving the prefab to the flat path or repointing `Ch13HerisPrefab` at the `Khall_Assets/` subpath. See §4 Beat 2b, §8 item 9, and Appendix B.
- **NPC facing is unbuilt (blocking — the highest-impact open staging item in this document).** `InstantiateNpc` (`ChapterSharedBuilders.cs:648`) sets position only; it never sets rotation. Both Dr. Heris and Sallow are therefore left at their prefabs' native +Z-forward orientation while the player always approaches from −Z, so as-built both present their backs at the chapter's two emotional peaks — the reckoning ("faces Ronin-7 across a sterile table") and the final reveal ("turns its blank waxen face toward him"). Fix at every `Ch13PlaceStoryNpc`/`InstantiateNpc` call site for either character — including the post-chapter Hub placement immediately below — with `rot = Euler(0,180,0)`. See §4 Beats 2b/4b and §5 for the full detail.
- **The additive-patch rule, and its one exception.** Re-running `BuildChapter13SterileReckoning()` wipes generated content. The house rule remains: patch additively in the live editor, or fix `Chapter13Builder.cs` and treat a rebuild as a deliberate, scoped action. **The exception is `[STATIC_ART_DO_NOT_DELETE]` (§1.4)** — once the wipe strategy is converted, that subtree is the sanctioned place for hand-tuned art, prefab swaps, and lighting-bake work that must survive a rebuild.
- **Do not auto-delete orphan materials.** The saga-wide ~288 unreferenced material variants are regenerable via `Editor/Art/ArtGenerationMenu`. Reversible cleanup only.
- **Reject any prefab import that introduces a `MeshCollider`.** Room shells and props get primitive colliders. A high-fidelity art pass for the sterile-vault shells is exactly the vector that could reintroduce one — check FBX import settings' "Generate Colliders" on every prop landing in the registry.
- **No doors is intentional, not an oversight — but flag it before "fixing" it.** Unlike every other chapter, Chapter 13 has zero `Doors.SlidingDoor_Standard` instances and zero lock states. This may be a deliberate design choice (the sterile vault reads as an open, permissive facility precisely because "the door is left open on purpose," per Mera Voss's Beat 0 line) or it may be an unfinished pass that never got its gate logic. **Do not silently add a door** — if a future patch wants one (for instance, sealing the `LabCore` behind the Redactor fight, matching the screenplay's "sealed core door" the Enforcer is meant to guard), that is a writers'-room/design call, not a builder bug fix. **This is more than a screenplay-staging call — it is a shipped-VO contradiction sitting in the scene today.** `ch13_beat1b_enforcer_intro` has Echo and Morrigan repeatedly instruct the player toward a physical door ("Put the double on the door," "Block the door, make it come to you") and Ronin-7 name it directly ("only one of us walks out of this door"), and `ch13_beat1b_enforcer_defeated` narrates it opening ("The door's open. She's right through it.") — all against a `LabCore` with no door object anywhere in it (§4 Beat 1B/b). **Concrete cost of leaving it unbuilt:** with no door and no cross-wall, Heris is in unobstructed sightline for the entire Redactor fight (§4 Beat 1B) — the missing door isn't just an absent prop, it visibly pre-empts the Beat 2 reveal. The same gap is worse one room further in: Sallow, the climactic Ally #10 reveal, sits in the player's open forward sightline from (0,0,88) throughout the entire reckoning and defection (§4 Beat 4b), and canon already stages the fix — the script has Heris turn toward, then unseal, a "sealed annex door" at the `LabCore`/`Annex` boundary (script ~lines 496/502) that the as-built never models. **The consequence is also global, not just local to each reveal:** because no gate anywhere on the corridor blocks travel — the mission spine's first checkpoint is a z=34 `ReachTrigger` that advances the mission but does not stop forward movement — a player is physically free, from the instant Beat 0's VO begins, to walk the full z[-2,94] corridor and see the sealed cradles, the inactive Redactor, Heris, and Sallow before a single breach line plays (§4 Beat 0b). The no-doors design pre-empts not one reveal but the whole cast.
- **The screenplay's "protect the door" mini-boss mechanic is unbuilt.** §4 Beat 1B flags this in detail: no silence-field ability, no door-protect tracking, and no physical door object exist for the Redactor fight — it currently resolves as a plain single-enemy `DefeatEnemies` step. The dialogue's framing (Mirror "holding the door" while the player flanks) describes a mechanic that isn't there. Treat this as a known gap, not a regression to silently patch over.
- **The post-chapter Hub increment (`Ch13FillSurgeryReactor`) is not an empty shell.** `HubBuilder.cs:68/312`, gated by `ch13_complete`, fills the `SurgeryReactor` dark-shell room with a `SterileTable`, a cool `ReactorLight` (color (0.55,0.75,1), intensity 2, range 8), and idle Dr. Heris + Sallow (offset (−1.2,0,1.4) / (1.2,0,1.4) from the room root) — this is where the chapter's two new allies actually live aboard the Hub post-completion. §8 item 8's verification pass references the room once but this document never otherwise describes it as the chapter's payoff room; treat it as in-scope set dressing, not a future TODO. It reuses the same `InstantiateNpc` call with the same no-rotation gap flagged above — check facing here too when that fix lands. `HubBuilder.cs:329` also reads the same `Ch13HerisPrefab` constant, so the mislocated-prefab gap above is not chapter-local — Heris's idle Hub placement falls back to the same capsule until `Ch13HerisPrefab` is fixed; there is only the one call site to correct.
- **Sallow's one-line constraint (§4 Beat 4e).** The character is written to be non-verbal by design. Do not add further spoken lines for Sallow in `Chapter13Lines.cs` without deliberately revisiting this constraint with the writers' room — it is a structural choice ("more presence than person"), not an oversight.
- **The birth name "Soren" must never appear in this chapter's lines.** It is reserved for Ch16's reveal. Regression-guarded by `Chapter13LinesTests.NoLine_MentionsSoren` — any dialogue patch that reintroduces it breaks canon sequencing, not just a test.
- **The saga-wide "not X, it's Y" antithesis tic and aphorism-stacking flagged by `story ouput/audit/Ch13_audit.md`** are explicitly deferred by `00_AUDIT_SUMMARY.md` to "a dedicated pass" and are **not** fixed in `Chapter13Lines.cs` (only AUDIT FIX #4 was mandatory for this pass — see the file's own header comment). Do not silently "clean up" this prose style while touching unrelated lines; it is a known, deferred finding, not an open bug.
- **Minimal-new-code chapter — do not add systems here casually.** Per the builder's class summary, this chapter deliberately introduces no new ability, no new runtime component, and no new mission-step type. A future patch that wants Sallow's "absolution mechanic" (carrying kept shadow-AIs at scale, the Ch16 liberation tool per the dialogue script's gameplay-hooks note) to actually function in gameplay is by design **out of scope for Ch13's builder** — the mechanic is narratively introduced here and mechanically built later (Ch16), mirroring how Mirror was cued narratively before Ch12 shipped it.

---

## Appendix A — As-built primitive fallback (current state, being replaced)

> **This appendix describes what the code does *today*, not the target state.** It exists for two reasons: it is the geometry the fallback path (§1.5) builds when a registry slot is empty, and it is the specification each replacement prefab must reproduce or improve on. **It stays authoritative until every key in Appendix B resolves.** Delete a row only when its prefab ships.
>
> Unlike Ch1's Appendix A, which is organized by beat (because Ch1's builder groups geometry into beat-scoped local helpers), **this appendix is organized by physical zone**, because `Chapter13Builder.cs` builds the entire five-zone world root in one contiguous block, independent of the mission-spine beat labels. Cross-reference §2's zone-to-beat table to see which beats occupy which zone.
>
> All props are cheap primitives tinted via the shared `TintShared` helper (MaterialPropertyBlock batching) rather than unique materials.

### A.1 Global lighting / fog / tint literals

These are the values to author into `Ch13Environment.asset`. Currently set inline at the top of `BuildChapter13SterileReckoning` (`Chapter13Builder.cs:112–134`).

| | Value |
|---|---|
| Directional key | color (0.92, 0.94, 0.98), intensity 0.95, rotation Euler(55, -35, 0) |
| Ambient | mode **Flat**, color (0.5, 0.53, 0.58) |
| Fog | mode **Exponential**, color (0.85, 0.88, 0.94), density 0.006 |

**Per-zone floor/ceiling tint:**

| Zone | Floor tint | Ceiling tint |
|---|---|---|
| `SpawnGround` | (0.82, 0.85, 0.9) | (0.88, 0.9, 0.94) |
| `OuterCorridor` | (0.8, 0.83, 0.88) | (0.86, 0.88, 0.92) |
| `Nurseries` | (0.78, 0.83, 0.92) | (0.85, 0.88, 0.95) |
| `LabCore` | (0.92, 0.94, 0.98) | (0.95, 0.96, 1) |
| `Annex` | (0.7, 0.8, 0.95) | (0.78, 0.85, 0.97) |

**Accent point lights** (`BuildAccentPointLight(name, pos, color, intensity, range)`):

| Light | Position | Color | Intensity | Range | Behaviour | Read |
|---|---|---|---|---|---|---|
| `SpawnLight` | (0, 2.4, 4) | (0.95, 0.96, 1) | 1.2 | 10 | none | neutral spawn |
| `CorridorLight0` | (-2, 2.6, 16) | (0.9, 0.93, 1) | 1.3 | 14 | none | outer corridor |
| `CorridorLight1` | (2, 2.6, 26) | (0.85, 0.9, 1) | 1.3 | 14 | none | outer corridor |
| `NurseryLight0` | (-3, 2.4, 40) | (0.5, 0.75, 1) | 1.4 | 14 | `AddAmbientPulse(periodSeconds: 7.6f)` | sterile blue seams |
| `NurseryLight1` | (3, 2.4, 54) | (0.45, 0.7, 1) | 1.4 | 14 | none | sterile blue seams |
| `ThresholdLight` | (0, 2.4, 62) | (0.9, 0.9, 0.95) | 1.6 | 12 | none | lab threshold |
| `CoreLight0` | (-3, 2.6, 68) | (1, 1, 1) | 1.8 | 16 | `AddConsoleFlicker(seed: 141f)` | brightest, most surgical |
| `CoreLight1` | (3, 2.6, 76) | (1, 1, 1) | 1.8 | 16 | none | brightest, most surgical |
| `AnnexLight0` | (0, 2.4, 87) | (0.55, 0.75, 1) | 1.5 | 14 | none | Sallow's sterile blue |

**Event light:** none. Chapter 13 authors zero inactive/event-triggered lights (contrast Ch1's `DockingAlarmLight`).

### A.2 Zone: `SpawnGround`

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-6,6], z[-2,10], center (0,0,4), 12×12 | `BuildFloorCeiling(world, "SpawnGround", ...)` |
| Walls | **none** — no `BuildWall` calls target this zone; it is open-flanked | — |
| Katana "Echo" | (2, 1, 4), rot Euler(-90, 0, 0) | `BuildSword(pos, rot, weapon, Ch13EchoBladePrefab)` — rides from the start, no rack-wake beat (matches Ch9–12's "cost, not initiation" precedent) |
| Player rig spawn | inside this zone | `BuildRig(refs, addLocomotion: true)` |
| Dialogue anchor | (0, 1, 4) | `Dialogue_Beat0_Briefing`, set `ch13_beat0_briefing` |

### A.3 Zone: `OuterCorridor`

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-6,6], z[10,34], center (0,0,22), 12×24 | `BuildFloorCeiling(world, "OuterCorridor", ...)` |
| `OuterCorridor_WallW` | (-6, RoomH/2, 22), size (0.2, RoomH, 24) | `BuildWall` |
| `OuterCorridor_WallE` | (6, RoomH/2, 22), size (0.2, RoomH, 24) | `BuildWall` |
| `OuterCorridor_WallS` | (0, RoomH/2, -2), size (12, RoomH, 0.2) | `BuildWall` — the south end-cap for the whole level, at `SpawnGround`'s rear edge, despite the `OuterCorridor_` name prefix |
| Design ward row | `DesignTable` at (-4.5, 0.5, z), size (1.4, 0.1, 0.7), tint (0.88, 0.9, 0.94); `InstrumentTray` at (4.5, 0.4, z), size (0.8, 0.08, 0.5), same tint; z ∈ {12, 18, 24, 30} | `Ch13BuildDesignWardRow(world, zStart: 12f, zEnd: 32f, spacing: 6f)` |
| Lab security ×3 | (-3,0,22), (3,0,22), (0,0,27) | `BuildEnemy(pos, playerHealth, labSecurityDef)`, built inactive |
| `SterileHiveCascade` | wraps the 3 lab-security `MeleeAttacker`s | `Ch13BuildHiveCascade(labSecurityEnemies)` — `HiveCascadeController`, built active |
| Reach point | `DesignWardsReachPoint` (0, 1, 34), radius 5 | step 1 |
| Dialogue anchor | (0, 1, 12) | `Dialogue_Beat1_Breach`, set `ch13_beat1_breach` |

### A.4 Zone: `Nurseries`

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-6,6], z[34,60], center (0,0,47), 12×26 | `BuildFloorCeiling(world, "Nurseries", ...)` |
| `Nurseries_WallW` | (-6, RoomH/2, 47), size (0.2, RoomH, 26) | `BuildWall` |
| `Nurseries_WallE` | (6, RoomH/2, 47), size (0.2, RoomH, 26) | `BuildWall` |
| Sealed nursery row | `SealedCradle` ×2 per row at (-5, 0.6, z) and (5, 0.6, z), size (0.6, 1.2, 0.6), tint (0.55, 0.75, 0.95); z ∈ {36, 41, 46, 51, 56} | `Ch13BuildSealedNurseryRow(world, zStart: 36f, zEnd: 58f, spacing: 5f)` |
| Reach point | `LabThresholdReachPoint` (0, 1, 60), radius 5 | step 5 |
| Dialogue anchor | (0, 1, 44) | `Dialogue_Beat1_Recognition`, set `ch13_beat1_recognition` |

### A.5 Zone: `LabCore`

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-7,7], z[60,80], center (0,0,70), 14×20 | `BuildFloorCeiling(world, "LabCore", ...)` |
| `LabCore_WallW` | (-7, RoomH/2, 70), size (0.2, RoomH, 20) | `BuildWall` |
| `LabCore_WallE` | (7, RoomH/2, 70), size (0.2, RoomH, 20) | `BuildWall` |
| `SterileTable` | (0, 0.5, 68), size (1.8, 0.1, 0.9), tint (0.95, 0.96, 1) | `BuildProp` |
| The Redactor | (0, 0, 62), named `"Redactor"` | `BuildEnemy(pos, playerHealth, redactorDef)`, built inactive |
| Dr. Heris | (0, 0, 72), **no rotation set** (target: Euler(0,180,0) — facing gap, §4 Beat 2b); prefab path **mislocated**, resolves to `"Dr. Heris_Placeholder"` capsule, not the baked mesh (§4 Beat 2b) | `Ch13PlaceStoryNpc(Ch13HerisPrefab, pos, "Dr. Heris")`, built active |
| Dialogue anchors | (0,1,60) EnforcerIntro; (0,1,66) EnforcerDefeated; (0,1,74)/(0,1,75)/(0,1,76)/(0,1,77)/(0,1,78) the five reckoning sets; (0,1,80) Defection | seven `DialoguePlayer`s across Beats 1B/2/3 |

### A.6 Zone: `Annex`

| Element | Coordinates / value | Component / method |
|---|---|---|
| Room footprint | x[-5,5], z[80,94], center (0,0,87), 10×14 | `BuildFloorCeiling(world, "Annex", ...)` |
| `Annex_WallW` | (-5, RoomH/2, 87), size (0.2, RoomH, 14) | `BuildWall` |
| `Annex_WallE` | (5, RoomH/2, 87), size (0.2, RoomH, 14) | `BuildWall` |
| `Annex_WallN` | (0, RoomH/2, 94), size (10, RoomH, 0.2) | `BuildWall` — the chapter's dead end |
| Sallow | (0, 0, 88), **no rotation set** (target: Euler(0,180,0) — facing gap, §4 Beat 4b) | `Ch13PlaceStoryNpc(Ch13SallowPrefab, pos, "Sallow")`, built active |
| Reach point | `AnnexReachPoint` (0, 1, 90), radius 5 | step 15 |
| Dialogue anchors | (0,1,88) IntroduceSallow; (0,1,89) Mechanic; (0,1,90) Complete | three `DialoguePlayer`s |
| `CHAPTER 13 COMPLETE` canvas | (0, 1.4, 93), scale 0.0015, rot Euler(0,180,0) | `Ch13BuildCompleteCanvas`, built inactive |
| `ChapterOutro` | (0, 1, 92), inactive; `CampaignFlagSetter` flags `["ch13_complete","heris_recruited","sallow_recruited"]` wired to `OnActivated` | step 19 |

### A.7 Data assets

| Asset | Path | Values |
|---|---|---|
| `Ch13LabSecurity.asset` | `Assets/Ronin7/Data/Ch13LabSecurity.asset` | `EnemyDefinition`: maxHealth 50, damage 8, moveSpeed 1.4, attackCooldown 0.9 |
| `Ch13Redactor.asset` | `Assets/Ronin7/Data/Ch13Redactor.asset` | `EnemyDefinition`: maxHealth 180, damage 16, moveSpeed 1.3, attackCooldown 1.0 |

### A.8 Scene root hierarchy (current)

`BuildChapter13SterileReckoning()` creates these as **siblings**, not nested: `Directional Light`, `SterileVault` (all five zone floor/ceiling volumes, their walls, the design-ward and sealed-nursery rows, the `SterileTable`), nine accent-light GameObjects, `Game` (`GameState` + `CombatFeedbackController`), the player rig, four `Enemy` roots (three lab-security + the Redactor) plus `SterileHiveCascade`, `Dr. Heris`, `Sallow`, three reach points, fourteen dialogue-player roots, the complete canvas, `ChapterOutro`, `Mission`, the `XR Interaction Manager`, and `LabCoreAmbience`.

**Target hierarchy** adds `[STATIC_ART_DO_NOT_DELETE]` and six `[BEAT_N_LOGIC]` roots (`0`, `1`, `1B`, `2`, `3`, `4`), and moves `SterileVault`'s contents into the former.

---

## Appendix B — `ArtAssetRegistry` key inventory

| Registry Key | Category | Resolves to | Status |
|---|---|---|---|
| `Rooms.SterileVault_SpawnShell` | Room | `Assets/Ronin7/Art/Generated/Rooms/SterileVault_SpawnShell.prefab` | **MISSING** |
| `Rooms.SterileVault_CorridorShell` | Room | `Assets/Ronin7/Art/Generated/Rooms/SterileVault_CorridorShell.prefab` | **MISSING** |
| `Rooms.SterileVault_NurseryShell` | Room | `Assets/Ronin7/Art/Generated/Rooms/SterileVault_NurseryShell.prefab` | **MISSING** |
| `Rooms.SterileVault_LabCoreShell` | Room | `Assets/Ronin7/Art/Generated/Rooms/SterileVault_LabCoreShell.prefab` | **MISSING** |
| `Rooms.SterileVault_AnnexShell` | Room | `Assets/Ronin7/Art/Generated/Rooms/SterileVault_AnnexShell.prefab` | **MISSING** |
| `Props.DesignTable` | Prop | `Assets/Ronin7/Art/Generated/Props/DesignTable.prefab` | **MISSING** |
| `Props.InstrumentTray` | Prop | `Assets/Ronin7/Art/Generated/Props/InstrumentTray.prefab` | **MISSING** |
| `Props.SealedCradle` | Prop | `Assets/Ronin7/Art/Generated/Props/SealedCradle.prefab` | **MISSING** |
| `Props.SterileTable` | Prop | `Assets/Ronin7/Art/Generated/Props/SterileTable.prefab` | **MISSING** |
| `Enemies.SterileLabSecurity` | Enemy body | *(none — falls back through the pre-existing, chapter-agnostic `ArtPrefabRegistry`/`EnemyFootPrefabPath` capsule pipeline, not this registry)* | **MISSING** (this registry) |
| `Enemies.ProgramRedactor` | Enemy body | *(same as above — no chapter-specific key exists)* | **MISSING** (this registry) |
| `Named.DrHeris` | Named character | `Assets/Ronin7/Art/Generated/Characters3D/Named/Dr-Heris.prefab` *(no file at this path — the only `Dr-Heris.prefab` on disk is `Named/Khall_Assets/Dr-Heris.prefab`)* | **MISLOCATED** — falls back to capsule (§4 Beat 2b) |
| `Named.Sallow` | Named character | `Assets/Ronin7/Art/Generated/Characters3D/Named/Sallow.prefab` | **EXISTS** |
| `Named.Echo` | Named character (weapon-slung) | `Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab` | **EXISTS** |
| `Doors.*` | Door | — no door keys are consumed this chapter (§2, §9) | **N/A** |
| `Vfx.*` | VFX | — no VFX props are instantiated this chapter | **N/A** |

**Summary: two of sixteen listed keys resolve** (`Named.Sallow`, `Named.Echo`, reused from earlier chapters' asset pipeline). A third, `Named.DrHeris`, is baked but **mislocated** — the file exists at `Named/Khall_Assets/Dr-Heris.prefab`, not the flat `Named/Dr-Heris.prefab` path `Ch13HerisPrefab` reads, so it resolves to nothing at build time and the row falls back to a capsule exactly like a `MISSING` key (§4 Beat 2b). Every room shell and every non-character prop is a commission. This is a narrower art footprint than Ch1 (five keys resolved out of a larger inventory) but the *proportion* unresolved is comparable — Chapter 13, like Chapter 1, remains fully playable today entirely on primitive fallback geometry, per §1.5's safety interlock.

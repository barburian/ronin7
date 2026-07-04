# Space Samurai — Audit & Improvement Pass (Summary)

Branch: `audit/space-samurai-improvements` (off `main` @ `897b66f`). Every code change was
implemented by one Opus agent and independently reviewed by a second Opus agent, then integrated
and validated headless via Unity 6000.3.2f1 EditMode tests. **Test suite: 356 → 400 (all green).**

## What shipped (committed)
| Phase | Commit | Summary |
|---|---|---|
| 0 | `16ca4ab` | Harden `Health` (neg/zero/NaN damage can't heal or emit spurious events) + tests; validated the implement→review→test harness. |
| 1 | `f92f276` | `GameFlowManager` camera-wait diagnostic (honest, not a fake reveal); `BladeDamager` EMA swing-speed (frame spikes can't inflate melee dmg); `SaveSystem` atomic temp-write + migration seam; `ProjectilePool` O(1) double-return guard + capacity 64 + starvation warn; `ContinuousLocomotion` posture state guard; hot-path `HashSet`/cache cleanups. |
| 2 | `5132f1f` | Per-tier bloom `highQualityFiltering` override (Quest drops multi-pass HQ bloom; PCVR keeps it); motion-blur off in VR; GPU instancing on 6 materials. |
| 3 | `ea3d58c` | `SpaceEncounterManager` pool-safe wave clamp + data-driven elite variety (`ComposeWave`, tested); `ShipController` rotation normalization (no long-session drift). |
| 4 | `ae47e92` | Sun → navigation & strategy: design spec + unit-tested cores (`SunCompass`, `SunGravityWell`, `SunGlare`), additive/off-by-default. |

## Audit claims corrected during verification (the review loop working)
- **"609 materials = critical batching break"** → only 321/609 referenced (live in generated character
  prefabs, not scenes); 288 orphaned; runtime enemies already batch via `RendererTint` (MPB). Reframed as
  reversible orphan cleanup (NOT auto-deleted).
- **`ProjectilePool` "critical null-fire"** → effectively unreachable; real issue was an O(n) guard (fixed).
- **`EnemyShip` "no disabled telegraph"** → it already exists (`Disable()`→tint + event); the speculative
  on-hit flash was reverted as scope creep.
- **`SunLightAimer` "bug"** → correct as written.
- **Bloom "already dimmed per tier"** → only *intensity* was; `highQualityFiltering` was NOT — now fixed.

## Hygiene (Phase 5, objective)
- **0 missing-script references** across 292 prefabs / 212 scenes — clean.
- **MeshColliders in 7 scenes** (Galaxy-1 on-foot zone/planet terrain + Phase4_Zone) — flag for an
  in-editor pass; on static terrain, replacing with primitives/simplified colliders needs per-scene validation.
  **RESOLVED 2026-07-04 (by scene deletion):** all 7 flagged scenes were removed in the EP→chapter
  migration. A full audit of the current 14 scenes (grep + live in-editor scan + .meta addCollider
  check) found **zero MeshColliders** — content is exclusively Box/Capsule/Sphere/CharacterController
  (837/98/1/14). Going forward this is a prevention item (review any import enabling
  "Generate Colliders"), not a remediation task.

## Hand-off — requires Unity Editor / Quest hardware (cannot be done headless)
1. **Sun-nav integration & tuning (Phase 4):** wire `SunGravityWell` boost→`ShipController` (universe-frame
   velocity hook) and heat→`Health` (mirror `AsteroidHazard`); bind `SunGlare` to a head-pinned overlay
   quad; place `SunGravityWell` on the Galaxy-1 sun and tune radii/angles/brightness in-headset for comfort.
   See `Docs/SunNavigation-Design.md` §"Integration & hand-off".
2. **Combat juice (T3.4):** hit/kill haptics, reticle hit-confirm, audio stingers — infra exists
   (`CombatFeedbackController`, `Haptics`, `AudioDirector`); tune in-headset (no camera shake).
3. **Quest profiling (T0.3/T2.5):** capture frame-time/draw-calls/GC on-device to confirm the 90 FPS floor
   and quantify the bloom/instancing wins. Static baseline: 635 materials (288 orphan), suite 639
   EditMode tests (0 skips), PlayMode 70/70.
4. **PlayMode stress test (T6.2):** worst-case projectiles+enemies asserting frame budget (needs graphics).
5. **Scene smoke + dual-platform build (T7):** menu→ship-select→combat→sun-nav→save/load→game-over;
   Quest + PCVR builds.
6. **Orphan material cleanup (T2.1):** reversible deletion of 288 unreferenced variants (regenerable via
   `Editor/Art/ArtGenerationMenu`) — script provided in the Phase 2 review notes.
7. **Optional tuning flagged by reviews:** `BladeDamager.speedSmoothing` default vs the 6 m/s damage
   saturation; mesh-collider scenes above.

## How to re-run the gate
`Unity.exe -runTests -batchmode -projectPath "<proj>" -testPlatform EditMode -testResults res.xml`
(On Windows `Unity.exe` forks the editor and the launcher returns early — wait for the results XML, not the
launcher exit.)

## 2026-07-04 — Continuous studio pipeline (chapter-build sprints)

Since the audit pass above, ~11 sprints of chapter-build work landed via the same
implement→review→test agent loop, shipping chapters Ch02 through Ch16 (Galaxy1 saga complete)
plus an editor-wide immersion pass. **Test suite: 400 → 639 EditMode (0 skips); PlayMode 70/70 green.**

### What shipped
- **Immersion component pass** — all 14 scenes (`Galaxy1_Ch1_Hub`, `Ch02`..`Ch13`, `Ch16`) now carry
  `HeartbeatHaptics`, `NpcGazeGlance`, `AmbientLightPulse`, `ConsoleFlickerLight`, `ProximityGlowLight`,
  `DamageAlertLighting`. `ProximityAmbienceLayer` and `NpcFootstepCadence` exist but are unwired,
  pending audio clips.
- **Core infra** — `Health.Active` / `StoryNpc.Active` static registries replace per-frame
  `FindObjectsByType` lookups; `LightBudget.ShouldAnimate` gates decorative lights on Quest tier;
  per-target `BladeDamager` cooldown (fixes cleave over-damage); `ZeroG` fixes; `InputResolver`
  severity split; `GravityRig` / `DreamPhantom` guards; `GameFlowManager` transition guards +
  arrival autosave (`ShouldAutosaveOnArrival`).
- **Repo hygiene** — `UnityYAMLMerge` merge driver configured repo-locally; `.gitattributes` macros
  fixed (root file; the `lfs` macro is deliberately left undefined until a remote LFS store exists).
- **Restored test content** — `Planet_VariantA.prefab`, `CyberNinja.prefab` (`[Ignore]`s removed).
- NPC walk rig (procedural 5-bone skinning) and voice-over for all 13 story chapters committed.

### Still open
- `ProjectSettings/EditorBuildSettings.asset` carries 12 dangling zero-GUID scene entries
  (leftover from deleted EP-scenes) — discard vs. keep is a pending user decision.
- Orphan materials note still applies (~288 unreferenced, regenerable via `Editor/Art/ArtGenerationMenu`,
  reversible cleanup only).
- ~~MeshCollider pass~~ — closed 2026-07-04: resolved by scene deletion, zero MeshColliders in current content (see Hygiene above).
- Sun-nav still additive / off by default pending in-headset tuning.
- Combat juice infra still needs hardware tuning.
- A5 death-latch fix in progress.

## 2026-07-05 — Objectives run (5-team agent loop)

Continuous diagnose→implement→QA→commit loop over five objectives plus review passes.
**Test suite: 639 → 842 EditMode (0 skips); PlayMode 70/70 green.** 13 commits, `e07824a`..`392d397`.

### What shipped
- **Kessler floor-clip fix (critical)** — Ch1 NpcWalker waypoints hard-coded Y=0 vs his grounded
  centered-pivot root at y≈0.9; builder parameterized + shipped scene patched + regression tests.
- **VO wiring** — all 839 Ch2-16 dialogue clips existed on disk but 0 were wired in scenes;
  new `ChapterVoiceWirer` (additive open→wire→save) resolved 839/839. **Project law learned:**
  chapter Build menu items destructively `NewScene`-rebuild — never rebuild a shipped scene;
  patch additively AND mirror in builder source.
- **Enemy combat readability** — `EnemySwordVisual` runtime placeholder katanas under the existing
  `weapon` pivot (blade tip on the (0,0,0.5) parry contract); blade joins the windup/stagger/death
  telegraph tint. `PoseWeapon`'s rotation lerp gives the swing for free.
- **Enemy variety** — previously zero-call-site mechanics wired: Ch7 3v3 `FactionCombatant` gang-war
  wave; Ch12 `HiveCascadeController` sentinel squad + `Ch12SentinelDuelist` elite (first
  `postureMaxFraction` use) via additive `ChapterEnemyVarietyWirer`.
- **Talk animation** — `NpcTalkAnimator` amplitude-driven pitch nod on `Rig_Body` during VO
  (no jaw/blendshapes on Tripo rigs); DialoguePlayer VO switched `PlayOneShot`→`clip`+`Play()`
  (one-shots are invisible to `GetOutputData` and ignore `Stop`); cadence fallback when a device
  yields no output samples (this rig virtualizes all voices with no audio endpoint).
- **Immersion retrofit** — all 14 scenes: 52 auto-placed `AudioReverbZone`s (new
  `ReverbZonePlacer.AutoTagInteriorVolumes`), `ProximityAmbienceLayer` beds, conservative
  `ConsoleFlickerLight`/`AmbientLightPulse`, fog retrofit Hub+Ch2-7; all mirrored into builders.
  `TintShared` material-clone perf bug fixed (shared cache; was 50-164 embedded materials/scene);
  env primitives now use `LowPolyMeshes` per ArtDirection-Spec.
- **Silent-audio fix (audit catch)** — the 9 procedural clip `.asset`s were zero-length stubs
  (`AudioClip.Create`+`CreateAsset` never persists PCM); `ProceduralAudioClipBuilder` now writes
  real 16-bit WAVs through the AudioImporter; 47 scene refs rewired, hand-authored ambiences preserved.

### Still open (this run)
- Reverb preset variety collapses to Hallway/Hangar (`ReverbPresetSelector.RoomMaxSpan=6` is below
  real 8-20 m rooms) — ear-tune on hardware before changing.
- Talk-nod amplitude path needs an in-headset check (falls back to cadence until samples arrive).
- Two pre-existing ambience sources baked with `m_Volume: 0` (Hub Medbay, Ch02 Dock) — self-correct
  at runtime within ~2 s; evidence of play-mode state leaking into saved scenes.
- Prop variety beyond the 3-prop room template (survey rank 8) — needs new low-poly prop meshes.

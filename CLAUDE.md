# Ronin7

VR "Space Samurai" action game. Unity **6000.5.0f1**, URP, OpenXR targeting **Meta Quest + PCVR**.
World scale is **1 unit = 1 meter** (VR — never break this). Most editor work runs through the
`ai-game-developer` MCP against the live editor.

## Architecture

C# source lives under `Project/Assets/Ronin7/Scripts/<Area>/`, split into modular assemblies
(`Ronin7.*.csproj` at `Project/` root). Put new gameplay code in the owning assembly.

| Assembly | Folder | Owns |
|---|---|---|
| `Ronin7.Core` | `Scripts/Core` | `SaveSystem` (atomic write + migration), `RendererTint` (MaterialPropertyBlock batching), `Health` |
| `Ronin7.Combat` | `Scripts/Combat` | `BladeDamager` (EMA swing-speed damage) |
| `Ronin7.Player` | `Scripts/Player` | `ContinuousLocomotion`, `Haptics`, `CombatFeedbackController` |
| `Ronin7.Ship` | `Scripts/Ship` | `ShipController`, `ProjectilePool`, `SpaceEncounterManager`, `SunNavigation/` |
| `Ronin7.Enemies` | `Scripts/Enemies` | enemy ships / behavior |
| `Ronin7.World` | `Scripts/World` | world / hazards |
| `Ronin7.Audio` | `Scripts/Audio` | `AudioDirector` |
| `Ronin7.Flow` | `Scripts/Flow` | `GameFlowManager` (scene/flow orchestration) |
| `Ronin7.Editor` | `Scripts/Editor` | editor tooling (e.g. `Editor/Art/ArtGenerationMenu`) |

Tests: `Ronin7.Tests.EditMode` and `Ronin7.Tests.PlayMode`. The EditMode suite is the gate.
Scenes: `Assets/Ronin7/Scenes/ChNN_*.unity` (chapters + their `ChNN_Prologue` ship scenes).
`Galaxy1_Ch1_Hub` is both the Ch1 mission and the persistent post-Ch1 hub, mode-gated via
`HubStateController`.

## Build / Run / Test

EditMode gate (baseline: **842 tests green, 0 skips**; PlayMode: **70/70 green**):

```
Unity.exe -runTests -batchmode -projectPath "Project" -testPlatform EditMode -testResults res.xml
```

**Windows gotcha:** `Unity.exe` forks the editor and the launcher returns early. Wait for the
`res.xml` results file, not the launcher's exit code. PlayMode tests need a graphics device.

## Working through the MCP

The `ai-game-developer` MCP is the primary way to inspect and modify the running editor. Reach for
the dedicated Unity skill over `script-execute`-everything:

- Scenes: `scene-open`, `scene-get-data`, `scene-list-opened`, `scene-save`
- Objects: `gameobject-find`, `gameobject-component-get/modify/add`
- Code: `script-read`, `script-update-or-create`; `script-execute` runs arbitrary C# in-editor
- Diagnostics: `console-get-logs`, `screenshot-game-view`/`-camera`, `profiler-*`, `tests-run`

## VR constraints (non-negotiable)

- **90 FPS floor** = 11.11 ms frame budget. A single dropped frame is visible judder.
- **No camera shake** — induces sickness. Combat feel comes from haptics/audio/reticle, not shake.
- Per-tier rendering: Quest drops HQ bloom filtering and motion blur; PCVR keeps them.
- Locomotion: teleport / snap-turn with comfort vignette; respect the `ContinuousLocomotion` posture guard.
- Large, readable UI; everything scaled to real arm reach.

## Known pitfalls / current handoff

(from `Project/Docs/IMPROVEMENT-SUMMARY.md` — read it before touching these)

- **Orphan materials:** ~288 unreferenced material variants exist but are regenerable via
  `Editor/Art/ArtGenerationMenu`. Reversible cleanup only — **do not auto-delete.**
- **MeshColliders: resolved** — the 7 flagged scenes were deleted in the chapter migration; a
  2026-07-04 audit found zero MeshColliders in the current 14 scenes (all primitive colliders).
  Prevention only: reject any future import that introduces one (e.g. FBX "Generate Colliders").
- **Sun-nav** (`SunGravityWell` / `SunGlare` / `SunCompass`) is unit-tested but **additive and
  off by default** pending in-headset tuning. See `Project/Docs/SunNavigation-Design.md`.
- **Combat juice** infra exists (`CombatFeedbackController`, `Haptics`, `AudioDirector`) — needs
  tuning on hardware, no camera shake.
- **Reuse, don't reinvent:** `ProjectilePool` (pooling), `RendererTint` (MPB batching), `SaveSystem`,
  `Health.Active` / `StoryNpc.Active` (static registries — no per-frame `FindObjectsByType`),
  `LightBudget.ShouldAnimate` (Quest-tier decorative-light gating).

## Conventions

- Respect assembly boundaries; new code goes in the owning `Ronin7.*` asmdef.
- Add EditMode tests for pure logic — the suite is the gate that must stay green.
- Follow the global Karpathy guidelines: surgical changes, simplest solution, surface assumptions.

## Skills to reach for

- `code-review` / `simplify` on a diff before committing C# changes; `verify` / `run` to confirm
  a change works in the actual editor/headset, not just tests.
- `game-development` skill (`.claude/game-development/`) routes to `vr-ar`, `3d-games`,
  `game-design`, `game-art`, `game-audio` sub-skills with Ronin7-specific notes.
- Day-to-day editor work: the Unity MCP skills listed above.

## Pointers

- `Project/Docs/INDEX.md` — map of design/handoff docs
- `Project/Docs/IMPROVEMENT-SUMMARY.md` — audit results, handoff checklist, the test gate
- `Project/Docs/SunNavigation-Design.md` — sun-nav mechanic spec
- `story ouput/00_STORY_BIBLE.md` — narrative backbone (note: folder name is misspelled)

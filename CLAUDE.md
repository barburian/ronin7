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
Scenes: `Assets/Ronin7/Scenes/Galaxy1_EP01..EP06_*.unity`. `Galaxy1_EP01_Ship` is both the hub
and the EP01 combat intro — idling there is a game-over state, not a safe menu.

## Build / Run / Test

EditMode gate (baseline: **400 tests green**):

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
- **MeshColliders in 7 scenes** (on-foot zones / planet terrain) flagged for an in-editor pass;
  replacing needs per-scene validation.
- **Sun-nav** (`SunGravityWell` / `SunGlare` / `SunCompass`) is unit-tested but **additive and
  off by default** pending in-headset tuning. See `Project/Docs/SunNavigation-Design.md`.
- **Combat juice** infra exists (`CombatFeedbackController`, `Haptics`, `AudioDirector`) — needs
  tuning on hardware, no camera shake.
- **Reuse, don't reinvent:** `ProjectilePool` (pooling), `RendererTint` (MPB batching), `SaveSystem`.

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

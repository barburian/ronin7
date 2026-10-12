# G7 save-compatibility fixture — Phase 0

`meta.json` (MetaSaveData v1) and `run.json` (RunSnapshot v1) in the exact shape the Phase 0
(`roguelike-phase0`, 0.1.0) build writes them. `Tests/EditMode/Roguelike/Core/SaveCompatibilityTests.cs`
loads them through the real `MetaProgression.Load` / `RunSnapshot.Read` / `RunState.Restore` path.

Never edit these files to make a later phase pass. A later phase that changes a save format adds its
own `saves_phaseN/` fixture and keeps this one loading (append-only fields, or a migration).

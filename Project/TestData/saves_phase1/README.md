# G7 save-compatibility fixture — Phase 1 gate

`meta.json` (MetaSaveData v1) and `run.json` (RunSnapshot v1) in the exact shape the Phase 0 code
(a8f548a, the last build before Phase 1) writes them, with mid-run values. `run.json` deliberately has
NO `unbrokenUsed` field (added by P1-07, appended without a version bump). Loaded by
`Tests/EditMode/Roguelike/Core/SavesPhase1FixtureTests.cs`.

Never edit these files to make a later phase pass.

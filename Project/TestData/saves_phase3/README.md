# G7 save-compatibility fixture — Phase 3 gate

Phase 3 (enemy archetypes, weakness contract, elite affixes, boss phases) changed **no serialized save
fields**: `RunSnapshot.CurrentVersion` stays 2 and `MetaSaveData.CurrentVersion` stays 1. These files are
the shape the Phase 3 build writes (seed 2026, path 0>2>5>7>8, depth 4; meta includes `tutorialDone`).
Loaded by `Tests/EditMode/Roguelike/Core/SavesPhase3FixtureTests.cs`. The Phase 2 fixture must keep loading.

Never edit these files to make a later phase pass — migrate the format instead.

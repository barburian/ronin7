# G7 save-compatibility fixture — Phase 4 gate

Phase 4 bumped **`RunSnapshot.CurrentVersion` to 3** (P4-11: Shards split from Echoes, the deferred
P4-03..P4-09 run fields, and the BUG-35 `RunRng.ForNode` seed mix). `run.json` is the v3 shape: `shards`,
`weaponArtId`, `offhandArtId`, `temperLevels`, `loadoutId`, `heatRanks` appended; `echoesEarned` is written 0
(seed 2026, path 0>2>5>7>8, depth 4; loadout Tanto, heat total 4, two Keen Edge tempers).
`MetaSaveData.CurrentVersion` stays 1; `meta.json` populates every appended Phase 4 field (`seenWeaknesses`,
`unlockedPacks`, `unlockedWeapons`, `selectedLoadout`, `heatRanks`, `pastEpics`, `keepsakes`, `selectedKeepsake`,
`weaponArt`, `offhandArt`).
Loaded by `Tests/EditMode/Roguelike/Core/SavesPhase4FixtureTests.cs`. The Phase 1-3 fixtures must keep loading:
their v1/v2 `run.json` files are now discarded and their stored `echoesEarned` banked as a loss.

Never edit these files to make a later phase pass — migrate the format instead.

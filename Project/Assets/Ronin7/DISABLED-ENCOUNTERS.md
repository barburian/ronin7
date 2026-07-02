# Disabled Encounters

Temporarily disabled to keep galaxy combat at 2 ambient enemies; restore/rework later.

| GameObject | clearedFlag | Episode | Original shipCount | Builder |
| --- | --- | --- | --- | --- |
| Pursuit Encounter | ep02_pursuit_cleared | EP02 | 3 | Galaxy1Builder.cs |
| Velorum Blockade | ep02_blockade_cleared | EP02 | 3 | Galaxy1Builder.cs |
| Hollow Kings Pursuit | ep03_pursuit_cleared | EP03 | 3 | Galaxy1Builder.cs |
| Grimdock Blockade | ep03_blockade_cleared | EP03 | 3 | Galaxy1Builder.cs |
| Pale Choir Blockade | ep04_choir_cleared | EP04 | 3 | Galaxy1Builder.cs |
| Dominion Interceptor Pursuit | ep04_escape_cleared | EP04 | 3 | Galaxy1Builder.cs |
| Rust Picket Blockade | ep05_picket_cleared | EP05 | 3 | Galaxy1Builder.cs |
| Khall Retribution Pursuit | ep05_retribution_cleared | EP05 | 3 | Galaxy1Builder.cs |

## How to re-enable

Remove that encounter's `SetActive(false)` line in `Galaxy1Builder.cs` and rebuild Galaxy 1.

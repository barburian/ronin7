# Playtest notes: v0.6.0 (Phase 5 "Graphics"), Quest 3S

## Session

- Date: 2026-10-24
- Build: 0.6.0 / versionCode 6 (Quest APK)
- Device: Quest 3S
- Recording: `playtests/20261024-112403/screen.mkv` (5:54, one eye)
- Logs: `playtests/20261024-112403/logcat-unity.txt` (0 errors), `playtests/20261024-112403/runlog.jsonl`
- Run result (last run): win, depth 14, 43 kills, 19 boons, 320 echoes, 272 s, seed 1107460646

## Notes

- [00:53] BUG MED: entering a new area, the new biome textures don't load; the old greybox map is still around (BUG-42).
- [01:12] ART: red bar behind the enemy (an archetype mark).
- [01:34] BUG MED: upgraded health (Forge TEMPER Ironskin); HP doesn't update until the next zone, then shows 100 and jumps to 175. It should update as soon as health changes (BUG-43).
- [01:37] ART: the area shows the new textures again.
- [01:38] BUG MED: enemy attack arm motion is inconsistent; other creatures have it too (BUG-44).
- [02:04] FEEL: expected the Ganger to jump on me, but it has to be closer before it dashes. When the Ganger spawns it should wait 2 s, then dash to the player.
- [02:13] ART: the ranged enemy has a pink cylinder; remove it like the Ganger ring.
- [--:--] BUG MED: in the other biomes too, the first area shows the new textures and the next ones don't (BUG-42).
- [03:03] BUG MED: healing doesn't update HP until the next map (BUG-43).
- [03:14] BUG MED: look at how the boss attacks (arm pattern, BUG-44).
- [03:34] ART: pink thing across the enemy's mouth; remove it. Also remove the green thing on top of the drone. The enemy's hand motion isn't natural (BUG-44).
- [--:--] UI LOW: remove the red line from the hand while the sword is equipped; keep it only when unequipped (BUG-45).
- [04:19] BUG MED: the (elite?) enemy shouldn't have the line through it and is too flashy, "epilepsy vibes" (BUG-46).
- [04:54] BUG MED: the boss has the same arm pattern (BUG-44).
- Reviewer, from frames (not reported by the player): at 03:01 and 04:17 the view fills with a solid green/pink frame while walking through a path door. Does it bother you?

## Bugs

- **BUG-42 MED**: Elite (9 m) and Forge/Shop/Treasure/Event (5 m) rooms show the greybox. Shells only exist at `_8m` and `_11m`, so `ArenaRoomLibrary.PickRoomScene` returns null. Repro: any non-Combat/Boss node. Fix: those rooms move to 8 m.
- **BUG-43 MED**: Forge TEMPER on a max-HP boon only applies on the next node load, and every load briefly shows base 100 HP before the boon total (`RunDirector.ApplyTemper`, `ApplyBoonInventoryToRig`). Repro: TEMPER Ironskin at the Forge and watch the wrist HUD.
- **BUG-44 MED**: humanoid enemies and the boss stay T-posed; `MeleeAttacker.PoseWeapon` twists the T-pose arm with an absolute Euler and the left arm never moves. Repro: any humanoid attack.
- **BUG-45 LOW**: the XRI UI ray (red, invalid gradient) shows on the right hand while the sword is held. Repro: hold the sword in any room.
- **BUG-46 MED**: an elite renders blown out to white (white archetype yoke + HDR x2 white elite rim + bloom), which reads as strobing. Repro: Unflinching elite, 04:19.

## Phase 5 watch items

- Boot menu head tracking / selection: not seen
- Ganger recognisable without its waist ring: not seen (it also needs an opening dash, see 02:04)
- Sky sharpness (1536x768 sources): not seen
- Loadout rack blades readable: not seen
- Garden walls too green: not seen
- Rust alcove back wall looks see-through: not seen
- Hero props (Rust mech, Garden tree): not seen
- Curved boon cards readable: not seen

## Retest v0.6.1 (2026-10-24)

- Build 0.6.1 / versionCode 7, Quest 3S, recording `playtests/20261024-170551/screen.mkv` (4:00), last run: win, depth 14, 37 kills, 12 boons.
- Verdict (user): successful test.
- **BUG-47 MED**: in every boss fight, the boss's hands are reversed and feel wrong. Moved to Phase 6 (P6-00).

## Sign-off

Phase 5 signed off by the user on 2026-10-24 after the v0.6.1 retest. BUG-47 is carried to Phase 6.

# Playtest checklist: v0.8.0 (Phase 7 "v0.7.0 playtest fixes"), Quest 3S

Not yet built or installed. The G3/G4 builds are blocked (see section 3). Sign off here before tagging `roguelike-phase7`; the user tags, not the agent.

## Session

- Date:
- Build: 0.8.0 / versionCode 9 · Quest APK and PCVR exe: NOT BUILT YET
- Device: Quest 3S
- Recording / logs: `playtests/<stamp>/` (screen.mkv, logcat-unity.txt, runlog.jsonl)

Tick each box, or write a note with a timestamp in the same `[mm:ss] KIND SEVERITY: ...` style as v0.7.0.

## 1. Bug fixes (BUG-48..53)

- [ ] **BUG-48 (boon offer with enemy alive):** play several rooms, including a boss with adds and a drone/summoner room. Hang back and let enemies flank you. Pass: the boon panel never opens while any enemy is alive. The fix is a guard and the root cause was not reproduced, so if it still happens, note the time and pull logcat and grep for `BUG-48: ledger empty but`. That line means the guard held a clear.
- [ ] **BUG-49 (Arts and passives dead):** buy a Shrine Art slot. Pass: it shows an Art (ChargedSlash / KiaiPalm), not NONE, and the Art fires on its input. Clicking the slot again cycles the Arts, including NONE.
  - [ ] **Caveat:** if your existing save already owns a slot that shows NONE, click it once to equip an Art. That is expected, not a regression.
  - [ ] Pick up a passive mid-room. Pass: it works straight away, not only from the next room.
- [ ] **BUG-50 (boot menu placement):** launch the app facing a different yaw each time (3 launches). Pass: the menu is in front of you, about 1 m ahead and 1.2 m up, with no Meta-button recenter. It may take up to 5 s for the first tracked pose.
- [ ] **BUG-51 (old boons on a fresh run):** finish or abandon a run with several boons, then start a fresh run. Pass: HUD `BOONS` reads 0 at the start. Shrine boons (StartingBoon, Ironskin, KeenEdge, Keepsake) appear on a separate `SHRINE BOONS n` line, labelled, and still apply.
- [ ] **BUG-52 (sector exit gates):** clear a sector. Pass: the exit gates rise when the CHECKPOINT panel opens, before you press anything. Walking through goes to the next sector. CONTINUE and SAVE & QUIT still work.
  - [ ] **Arch behind the panel:** the opaque CHECKPOINT panel can hide an arch directly behind it. Check that you can still see or find a raised arch. If not, note it (tracked as P12-04).
- [ ] **BUG-53 (attack line):** across several enemy types. Pass: no blade-edge line on any windup. The rim pulse still speeds up (2 to 6 Hz) so windups stay readable. Note if windups are now hard to read; P8-04 brings the replacement.

## 2. Controls

- [ ] Nothing about controls changed in P7. No new Y-button behaviour. Flag anything that feels different.

## 3. Owed gate items

- [ ] **PlayMode:** not run yet (blocked). Run it and expect 16/16 or better.
- [ ] **G3 Quest build and G4 PCVR build:** NOT run. Blockers: compile errors in the uncommitted `Packages/manifest.json` upgrade (`com.ivanmurzak.unity.mcp` 1.0.1 -> 1.6.0, `com.unity.ai.assistant` 2.9.0-pre.1 -> 2.21.0-pre.1; the mcp sub-packages fail CS0103/CS0234) and empty keystore passwords. Resolve the package state (or revert the upgrade), re-enter the keystore passwords, then build.
- [ ] **G5 smoke:** both builds boot to the menu, enter a run, and quit cleanly.
- [ ] **G6** and **G8** (device gates from the Phase 6 export list): run on the new builds.
- Already done: EditMode 2013/0/0 (baseline 1997). Pre-build render check passed (seed 2026, Phase6_Boot -> RunArena): Mobile 49 draws / 28 SetPass / 22.5k tris / luma 0.165 / magenta 0; PC 78 / 44 / 40.3k / 0.159 / 0. PNGs: `Project/Logs/PerfProbe/p7gate_Mobile.png`, `p7gate_PC.png`.
- [ ] Steady 90 Hz in the Quest build (OVR Metrics if you have it).

## 4. Soak: 3 runs

| # | Run type | Result | Depth | Crash / soft-lock? | Notes |
|---|---|---|---|---|---|
| 1 | Fresh run (clean boons, Art slot, sector gates) | | | | |
| 2 | Die at node 1 (then start again: no old boons) | | | | |
| 3 | Full win (all 3 sectors) | | | | |

Afterwards, pull `runlog.jsonl` and `logcat`. Check for 0 errors, no crash and no soft-lock, and grep for `BUG-48`.

## Sign-off

- [ ] Phase 7 signed off. Then the user tags `roguelike-phase7`. Next: Phase 8.

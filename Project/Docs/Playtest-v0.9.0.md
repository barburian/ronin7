# Playtest checklist: v0.9.0 (Phase 8 "Enemy combat language"), Quest 3S

Windows dev and release builds exist. The Quest APK is NOT built (keystore passwords empty after the editor restart). Sign off here before tagging `roguelike-phase8`; the user tags, not the agent.

## Session

- Date:
- Build: 0.9.0 / versionCode 10 · Windows `Builds/Windows/Ronin7_0.9.0.exe` (dev: `Builds/Windows-Dev/Ronin7_0.9.0-dev.exe`) · Quest APK: NOT BUILT YET
- Device: Quest 3S
- Recording / logs: `playtests/<stamp>/` (screen.mkv, logcat-unity.txt, runlog.jsonl)

Tick each box, or write a note with a timestamp in the same `[mm:ss] KIND SEVERITY: ...` style as v0.7.0.

## 1. Phase 8 checks

- [ ] **P8-01/02 weapons (BUG-54):** every humanoid holds a weapon; beasts and wraiths use claws. With the real Tripo meshes: grip sits in the hand, blade points away from the fist (right side up), shield face points at you, not sideways. Note any enemy with a mesh floating or inverted.
- [ ] **Ranged reaction time:** crossbow bolt from about 6 m. Pass: about 0.6 s from the telegraph to impact is enough to dodge or block, and it reads in the dark arena.
- [ ] **Ranged kiting:** archers and blaster users backstep to keep range (KiteMax 7 m). In a small room they should not pin themselves in a corner or jitter. In a big room you can still close the gap.
- [ ] **10-enemy room pacing:** combat rooms spawn 10 + sector, 4 live, drip release. Pass: it never feels like a wall of enemies, gaps between releases are short, and 90 Hz holds with 4 live.
- [ ] **P8-03 patterns:** attack patterns vary with no immediate repeat; combos and the five directions read. Parry zone is the blade only: a hit on the hilt or body does not parry. Heavy strikes are dodge-only.
- [ ] **P8-04 telegraphs:** weapon-edge glow, heavy-strike pose and per-weapon windup sound. Readable without colour, and with Reduce Flashes on. No camera shake.
- [ ] **P8-05 shields:** shield shares the wearer's armour colour. The weak spot is a 0.2 m strip on the shield; hitting it breaks or opens the guard. Easy to find and hit?
- [ ] **Double-elite room:** on the optional final-sector branch the map label reads "ELITE x2". Pass: two distinct elites plus 4 escorts, pay-out and threat feel right.
- [ ] **Elite escorts 4/5/6 by sector** and **early elite rooms** (sector 0, depth 0 or 1): elites are slightly weaker than combat rooms there (user chose to leave it). Confirm it does not feel trivial or like a free skip.
- [ ] **Duelist:** the guard is readable (you can tell when it is up and from which side), frontal melee is deflected, the riposte and sidestep are fair.
- [ ] **Juggernaut:** the hyper-armour windup, the unparryable ground slam, and whether the slam is clearly dodge-only.
- [ ] **Leaper:** the leap is about 9 m/s with a 0.7 s crouch and growl. Pass: the crouch is readable in the dark arena and the leap is fair to dodge. Note if you cannot tell where it will land.
- [ ] **Banner Commander:** allies within 6 m wind up x0.8, with no Quick-Telegraph floor (user OK'd going below the floor). Check it still feels fair, especially the dagger flurry. Killing the banner ends the buff.
- [ ] **Controls:** nothing about controls changed in P8. Flag anything that feels different.

## 2. Gate items

- [ ] **Quest build (G3):** waiting for the keystore passwords. Re-enter them in Project Settings > Player > Android > Publishing Settings (keystore `cheiejocronin7.keystore`, alias `bobi`), then build `BuildMenu.Build(Android, false)`. A debug-signed APK cannot install over the headset app. Never uninstall to work around it; that wipes saves.
- [ ] **PCVR build (G4):** Windows release built (343.7 MB, 1:05). Boot it on the PC.
- [ ] **G5 smoke:** both builds boot to the menu, enter a run, and quit cleanly.
- [ ] **G6 / G8** device gates from the Phase 6 list, on the new builds.
- Already done: EditMode 2084/0/0, PlayMode 16/16 (`e501c95`). Pre-build render check (seed 2026, Phase6_Boot -> RunArena, spawn dissolve finished): Mobile 55 draws / 34 SetPass / luma 0.146 / magenta 0; PC 86 / 52 / 0.137 / 0. PNGs: `Project/Logs/PerfProbe/exitB3_Mobile.png`, `exitB3_PC.png`.
- [ ] Steady 90 Hz in the Quest build (OVR Metrics if you have it).

## 3. Carry-overs still open from Playtest-v0.8.0

- [ ] **BUG-48** boon offer never opens with an enemy alive (grep `BUG-48: ledger empty but`).
- [ ] **BUG-49** Shrine Art slot shows an Art, fires on its input; passives work mid-room.
- [ ] **BUG-50** boot menu in front of you, about 1 m ahead and 1.2 m up, on 3 launches at different yaws.
- [ ] **BUG-51** fresh run starts with BOONS 0; `SHRINE BOONS n` line is separate and still applies.
- [ ] **BUG-52** exit gates rise when the CHECKPOINT panel opens; check an arch is not hidden behind the panel (P12-04).
- [ ] **BUG-53** no blade-edge line on windups; P8-04 telegraphs replace it (check section 1).

## 4. Soak: 3 runs

| # | Run type | Result | Depth | Crash / soft-lock? | Notes |
|---|---|---|---|---|---|
| 1 | Fresh run (clean boons, Art slot, sector gates) | | | | |
| 2 | Die at node 1 (then start again: no old boons) | | | | |
| 3 | Full win (all 3 sectors, take the double-elite branch) | | | | |

Afterwards, pull `runlog.jsonl` and `logcat`. Check for 0 errors, no crash and no soft-lock, and grep for `BUG-48`.

## Sign-off

- [x] Phase 8 signed off by the user 2026-11-05 on Quest build 0.9.1 (includes the 0.2 m parry radius): nothing to fix. The boxes above were not ticked one by one. Then the user tags `roguelike-phase8`. Next: Phase 9 (design session first).

# Playtest checklist: v0.7.0 (Phase 6 "Audio, accessibility, narrative, store"), Quest 3S

Release candidate. Nothing has been submitted. Sign off here before the `roguelike-phase6` tag or any store work.

## Session

- Date:
- Build: 0.7.0 / versionCode 8 · Quest APK `Builds/Quest/Ronin7_0.7.0.apk` (141.1 MB, release-signed, installed on the Quest 3S) · PCVR `Builds/Windows/Ronin7_0.7.0.exe` (exe + `Ronin7_0.7.0_Data` 252.2 MB on disk; shares the folder's UnityPlayer.dll and MonoBleedingEdge)
- Device: Quest 3S
- Recording / logs: `playtests/<stamp>/` (screen.mkv, logcat-unity.txt, runlog.jsonl)

Tick each box, or write a note with a timestamp in the same `[mm:ss] KIND SEVERITY: ...` style as v0.6.0.

## 1. P6-00 carry-over

- [ ] **BUG-47:** every boss's windup raises the striking arm forward and overhead on its own side. No crossing or twisting backwards. Warden, Vane / Wraith-6, Maelgorn.
- [ ] Known and not a regression: the Warden's mesh stretches when its arm is raised (auto-rig weights). Flag it only if it bothers you.
- [ ] Shield Trooper: the cyan slab sits on the left forearm, follows the idle and the swing, and never floats.
- [ ] Trash enemies' windups are unchanged from v0.6.1.

## 2. Comfort options (Settings, column 2)

- [ ] **Haptics %:** 0 gives no rumble at all; 100 feels the same as v0.6.1; 50 is clearly softer.
- [ ] **Reduce Flashes ON:** no white hit flash, no vignette flash, no bloom pop on Overdrive. Elites show a steady rim and nothing strobes. Windup rims still pulse gently (2 Hz). The weak-point mark pulses slower.
- [ ] **Reduce Flashes OFF:** looks like v0.6.1.
- [ ] **Slow-mo:** at 0.25 (default) deflect, Zanshin and Overdrive feel like v0.6.1; at 0.5 they're noticeably less slow. Any nausea?
- [ ] **Vignette %:** 0 = none while moving; 100 = as before.
- [ ] **Snap turn** is the default on a fresh settings file. Your saved settings are untouched, so check that the toggle reads as you left it.

## 3. Accessibility

- [ ] **Telegraphs without colour:** a windup is readable from the rim pulse speeding up (2 to 6 Hz) and the edge line, even if you ignore the hue.
- [ ] Summary headline is blue/amber (no red/green). Boon cards show the tier word.
- [ ] **UI scale 1.5:** every panel scales (settings, wrist HUD, boon/shop/event/path, pause, summary, subtitles), stays reachable, and doesn't clip into walls. The settings panel is about 1.8 m wide at 1.5x; is that too big?
- [ ] **Damage taken 50%** and **Enemy cooldown x1.5** work. The summary says ASSISTED RUN; `runlog.jsonl` has `"assisted": true`; Echoes bank normally.
- [ ] **Subtitles:** the band is readable, sits low enough not to block combat, and the toggle hides it.
- [ ] **Replay tutorial (BUG-32):** turn it on, start a run, and the onboarding prompts show. The toggle is OFF again afterwards and `runsCompleted` hasn't changed.

## 4. Narrative

- [ ] Kessler speaks about 1.2 s after the run summary, with a line fitting the outcome (first run / died in sector N / won / won with heat), and no back-to-back repeat over several runs.
- [ ] Boss intro on entering each boss node, plus a line on each phase change. The voices are distinct (Warden, Vane, Maelgorn) and subtitled.
- [ ] **Renames (display only; veto any you dislike):**
  - Sectors: THE RUST becomes **THE RUSTFANG**; THE GARDEN becomes **THE SILENT GARDEN**; THE PROGRAM is unchanged.
  - Events: Blood Pact becomes **Synod Contract**, Gamble **Velorum Wager**, Meditate **Cairn Respite**, Forgotten Shrine **Kethel-7 Shrine**.
  - Boons: Echo Twin becomes **Shadow Twin**, Iron Wall **Iron Dojo Wall**, Blood Price **Coil Tithe**, Headhunter **Drovis Bounty**, Whetstone **Salvager's Whetstone**.

## 5. Audio mix

All music is synthesized placeholder (your decision). Licensing slots are in Roguelike-Design.md A14.23.

- [ ] Volume sliders (Master / Music / SFX / Ambience) each change only their own bus; 0 is silent.
- [ ] Music: explore crossfades to combat on aggro and back, with no gap or clash. A boss node plays the boss loop. Do the 9 loops sound like music, or grating? Note any that should be cut.
- [ ] Ducking: music dips briefly on a weakness hit, a posture break and a boss phase change.
- [ ] Cues: run start, room cleared, boon offered, boon chosen, run won / lost, enemy spawning. All tonal, none are noise bursts, none are too loud.
- [ ] Enemy windup voices: each archetype sounds different, and you can tell a windup is coming with your eyes closed.
- [ ] Low-HP heartbeat loop below 25% HP; it stops on heal.
- [ ] Menu ambience on the boot menu. VO level vs music.
- [ ] Anything clipping, too loud or panned wrong (3D falloff).

## 6. VRC device checks (from Store-Prep.md)

- [ ] Remove the headset (focus loss): the game pauses; it resumes cleanly when you put it back on.
- [ ] Meta button / system menu: the game pauses.
- [ ] Guardian / boundary shows when you approach it.
- [ ] Cover the controllers (tracking loss): the blade disarms with no ghost hits.
- [ ] Steady 90 Hz, no stale-frame hitches (OVR Metrics if you have it).
- [ ] Hands show correctly (the game draws its own hand meshes, not controller models).

## 7. Soak: 5 complete runs

| # | Result | Depth | Heat / Daily | Assisted | Crash / soft-lock? | Notes |
|---|---|---|---|---|---|---|
| 1 | | | | | | |
| 2 | | | | | | |
| 3 | | | | | | |
| 4 | | | | | | |
| 5 | | | | | | |

Afterwards, pull `runlog.jsonl` and `logcat`. Check for 0 errors, no crash and no soft-lock.

## Sign-off

- [ ] Phase 6 signed off. Then tag `roguelike-phase6`, and do the store work (art, IARC, upload) on your go.

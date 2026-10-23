# Playtest checklist: v0.5.0 (Phase 4), Quest 3S

Tick as you go. Say out loud what you see; the reviewer reads the video. Sources: Roadmap Phase 4 + Export Gate, Part B G5/G6/G8 carry-overs, Design Amendment 12.

## 1. Setup

- [ ] Headset charged, wifi on, Dev APK installed (v0.5.0, versionCode 5).
- [ ] Start recording: `powershell -ExecutionPolicy Bypass -File D:\ronin7\Tools\playtest\Record-QuestPlaytest.ps1` (first time on USB; later add `-Ip <ip>`). scrcpy 4.1 is installed.
- [ ] Framing wrong? The default crop is a Quest 2 guess; this is a 3S. Retry with `-Crop ''`.
- [ ] Know the **dev shortcuts** (Dev APK only). They can fire by accident:
  - Hold **both stick clicks 2 s**: jump to boss; hold again: spawn the G6 worst case.
  - Hold **right stick click 2 s**: clear the room.
  - If a fight ends or the boss appears unexpectedly, suspect these first.

## 2. Gate runs

### G5: device smoke run
- [ ] Run A: seed 2026, use the dev skip, all 15 nodes -> win -> summary -> menu.
- [ ] Run B: die at node 1 -> summary -> menu.
- [ ] No exceptions or "couldn't be loaded" (reviewer checks `adb logcat -s Unity`).
- Carry-overs, not yet seen in the headset:
  - [ ] Treasure: TAKE card works.
  - [ ] Sector card shows; CHECKPOINT works.
  - [ ] Forge MEND / TEMPER have a visible effect (BUG-30).
  - [ ] Onboarding prompts appear after the dev reset (BUG-32).
  - [ ] No green scene-name label in a release build (BUG-31; Dev APK may still show it, note it).
  - [ ] Drones and boss move smoothly (BUG-38).
  - [ ] Scavengers: T-posed (no Animator)? yes / no

### G6: frame budget (Quest 3S, OVR Metrics overlay on)
- [ ] Both stick clicks 2 s to the boss, again to spawn the worst case (boss P2 + 2 adds + Volatile ring + 8 bolts).
- [ ] Record **2 min** under OVR Metrics.
- Pass: stale frames <= 1%, GPU <= 10 ms, <= ~150 draw calls per eye-pass.
- Numbers: stale ___ %, GPU ___ ms, draws ___ (editor proxy was ~82 draws).

### G8: VRC sanity
- [ ] Quest menu button pauses.
- [ ] Headset off pauses.
- [ ] Controller tracking lost: no phantom swings.
- [ ] Resume works after each.

## 3. Phase 4 feel checks

### BUG-39 weakness tells
Goal: after any hit, you can say why it did more (or less) damage.
- [ ] Gold marker = open now; blue-grey = guarded.
- [ ] First encounter of each weakness kind: short wrist hint + marker pulse + 0.5x slow-mo on the first exploit (0.4 s).
- [ ] Exploit = double-beat haptic; resisted = tiny tick. Clearly different?
- [ ] A new haptic cuts off the previous one? note when.
- [ ] Heavy back-plate sliver visible from the front?
- [ ] Indigo vs violet and rose vs magenta marks tell apart?
- [ ] +3 dB exploit cue audible. It only applies below ~71% SFX volume, so test at normal volume.

### BUG-40 archetype marks and size
- [ ] Name each archetype at a glance (no cheating by sound).
- [ ] The Coil Syndicate Ganger has **no mark** any more (A13.8, ring removed 2026-10-24). Can you still pick it out (charger silhouette, telegraph, behaviour)?
- [ ] Enemies are now 1.8 m, drones 0.8 m (~40% bigger). Do fights feel right? too big / fine / too small.
- [ ] Enemies spawn **facing you**.

### The 8 skills
- [ ] Quick Draw: gesture threshold 4.5 m/s, reliable? no accidental fires?
- [ ] Charged Slash: release >= 5 m/s.
- [ ] Blade Throw: throw 4 m/s; flight and recall look right.
- [ ] Kiai Palm: palm 4 m/s; 0.4 m shove pop readable.
- [ ] Chain Hook: yank 2.5 m/s; chain line visible.
- [ ] Element Imbue: sweep gesture triggers.
- [ ] Zanshin and deflect slow-mo: stacking feels ok, not nauseating.
- [ ] Phase Step: goes where the stick points; i-frames work; **no hit buzz** during them.

### Boons
- [ ] Picks apply immediately.
- [ ] Reversal: direction feels right.
- [ ] Ghost Step: timing.
- [ ] Proc line is visible when a boon triggers.
- [ ] Last Stand balance.
- [ ] Duo offers appear (Thunder Riposte, Crimson Tide, Ghost Step).
- [ ] Locked packs (Bleed, Storm, Executioner) never offered until bought.

### Forge TEMPER
- [ ] Picker works, one temper per boon.
- [ ] Curses can be tempered.

### Loadouts
- [ ] Katana, Nodachi, Tanto feel distinct (all still use the katana mesh).
- [ ] Twin Wakizashi: left-hip draw works and is comfortable.
- [ ] Off-hand Art is blocked with Twin (label doesn't say so; note if confusing).
- [ ] Left-handed hip swap works.

### Heat (unlocked after first win)
- [ ] Shrine HEAT tab opens and ranks change.
- [ ] Quick Telegraph also shrinks the parry window: do 2 ranks feel brutal?
- [ ] Unforgiving Guard chip haptic lands on time.
- [ ] Swarm Protocol pacing ok.
- [ ] Daily Fracture starts.

### Shrine
- [ ] 4 tabs reachable at arm's reach.
- [ ] Buys are one tap, no confirm. Any accidental purchase?
- [ ] Art slot cycling works.
- [ ] Buy and equip a Keepsake; next run starts with that Epic.

### Currency
- [ ] Wrist HUD shows ◈ Shards.
- [ ] Shop title reads "SHOP · ◈ n SHARDS".
- [ ] Echo payout at run end: win 320, loss 60 + 6 x depth. Matches?
- [ ] Quit mid-run and resume: loadout, heat, Arts and tempers all kept.

## 4. After the session (reviewer)

- [ ] Ctrl+C the recorder. Video, logcat and `runlog.jsonl` land in `playtests/<timestamp>/`.
- [ ] Run `python Tools\playtest\analyze_runlog.py`.
- [ ] Boon pick rates within 5-40%? List outliers.
- [ ] Thunder Riposte rarity (Duo): how often offered?
- [ ] Archetype spawn vs kill counts line up?
- [ ] Resisted vs weakness hits ratio sane?
- [ ] Note: the offer-rate simulation used a bot heuristic. This is the **first real pick data**; one session is thin, don't retune on it alone.
- [ ] Fill the gate boxes in the Roadmap (G5, G6, G8) from the results above.

### Findings (feeds the next phase's bug list)

| ID | Sev | Where | What happened | Repro / video timestamp |
|---|---|---|---|---|
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |

Verdict: G5 pass / fail, G6 pass / fail, G8 pass / fail. Tag `roguelike-phase4` only if all pass.

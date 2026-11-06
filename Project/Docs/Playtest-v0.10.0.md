# Playtest checklist: v0.10.0 (Phase 9 "Player weapons and select"), Quest 3S

Nothing in Phase 9 has been played on a headset: the P9-02d and P9-02e stops were deferred to this playtest. Players are built (2026-11-06, `9185e7c`); the 0.10.0 dev APK is on the headset. Sign off here before the user tags `roguelike-phase9`; the user tags, not the agent. Rulings and numbers: Amendment 15 in `Roguelike-Design.md` (A15, R1-R7).

## Session

- Date:
- Build: 0.10.0 (not built yet), Windows dev + release, Quest APK
- Device: Quest 3S. Handedness tested: right / left
- Recording / logs: `playtests/<stamp>/` (screen.mkv, logcat-unity.txt, runlog.jsonl)

Notes style: `[mm:ss] KIND SEVERITY: ...` as in v0.7.0. Use the Results column or a note under each section.

## 0. A15.11 Whip 90 FPS proof (FIRST, deferred from P9-02d)

Pass table (the dev build logs these to logcat):

| Check | Pass | Logged as | Result |
|---|---|---|---|
| Whip code time | at most 0.5 ms average | `whipMsHeld` | |
| Missed frames | under 1% at 90 Hz AND no worse than the Katana baseline (check `hz=` says 90) | `missedPct` | |
| Extra draws | at most 2 over Katana (already passing on renderer count; the real Tripo meshes add about 10k tris, so also watch frame time) | PerfBudgetProbe + draws | |
| Allocations | 0 while swinging, compared to the Katana baseline | `gcFrames` | |

Steps:
1. Install a **dev build** (Quest). Start OVR Metrics and `adb logcat -s Unity`.
2. Reach the G6 worst-case room: hold **both sticks for 2 s**.
3. **Katana baseline first**, in that room, without killing much. Note the logged numbers.
4. Switch loadout to Whip, same room. Swing **nonstop for 2 minutes**.
5. Discard the loading window and the G6-trigger window from the numbers. Watch health (don't die mid-test).
6. Read `whipMsHeld`, `missedPct`, `hz=`, `gcFrames` for both.

On a fail, cut in this order: chain points 10 to 6; body checks to every other frame; body damage altogether (tip only). The crack and the lasso are never cut. After a cut, re-run this section.

Notes:

## 1. Whip crack (before anything else about the lasso)

- [ ] **Rest (R11):** holding it still, the chain arcs out ahead of the hand, tip about chest height, not on the floor. It sways when you move and then settles, with no jitter. Pointing the handle steers it, slowly (about a second to catch up). Too floaty or too stiff?
- [ ] **Launch (R11):** a forward flick or thrust throws the chain out to about 4 m, then it snaps back. No button. A sideways slash is not a launch. Does a real flick trigger it every time (`LaunchHandSpeed` 2.5 m/s), and does it fire by accident?
- [ ] **Spin vs flick (R11):** a lasso spin never launches; a flick never arms the lasso. Spin, then throw, still grabs.
- [ ] **Aim marker (R11):** a cyan ring on the upper body of the enemy a front attack will hit, Whip only. Readable, not flickering between neighbours, and clearly not the yellow weakness dot or a red telegraph. Hidden with no target, during a lasso, when dead, when not holding the Whip.
- [ ] **Steer (R11, replaces the 10 degree bend):** with a ring up, a flick's tip curves to that enemy and lands. Does it help without looking like homing or a teleport? Close enemies (under 2 m) are the hardest case: the tip has to curl back.
- [ ] Crack fires on a flick, not on a slow drag. `CrackSpeed` is 8 m/s tip speed; a 3.6 m/s sweep stays just under (7.8 m/s measured), and any launch flight touching an enemy cracks. Fine, or too easy? (open decision f)
- [ ] 25 damage from the last 0.5 m of chain, 0.6 s cooldown; sound, sparks, haptics land.
- [ ] A crack during an enemy windup cancels it (also through Resisted hits; not on super-armour such as Juggernaut's slam, R2).
- [ ] Body hits (flat 4 x damage multiplier) are feedback only: no Overdrive or combo from them, the crack earns those (R5). The 0.5 s per-enemy re-hit cooldown (R6) feels right with nonstop swinging.
- [ ] Chain reach: 3.9 m at rest, about 3.9-4 m on a launch from the hand, against A15's "about 4 m". Good enough? (open decision k)
- [ ] Chain readability: it is a line renderer. Does it read as a chain in the headset? If thin, a richer chain is a later art upgrade and must re-run section 0.
- [ ] Whip handle: Tripo mesh with the ring; **coil placeholder hidden when held**; coil visible on the hip holster.

## 2. Each weapon, no skills

Play at least one room per weapon with no skills. Is each fun and complete on its own?

| Weapon | Feels like | Parry / block as A15 says | Notes |
|---|---|---|---|
| Katana (8-40, parry x1) | | | |
| Double Katana (4.8-24 each, cooldown 0.2 s, parry x0.7, crimson blade) | | | |
| Polearm + Shield | | | |
| Whip | | no parry, no reflect | |

- [ ] Weapon Arts (Charged Slash, Blade Throw) work only on Katana and Double Katana and show as unavailable on the others (A15.18). Off-hand Arts work with an empty off hand on any weapon.
- [ ] Off-hand rule: holding the second blade or the shield = no off-hand skills; letting go mid-fight works.
- [ ] `Parry`-tagged boons are hidden in offers on Whip runs.

### Polearm feel (A15.6)
- [ ] Speed point is at the **damage zone** (front 1.1 m) for swings; **hand speed** is used for thrusts.
- [ ] Thrust: flat 30 when moving at least 2 m/s along the pole; head hit about 37.5 (R3). **Point-blank thrusts** still register.
- [ ] Swing 6-20, full at 7 m/s. Hit cooldown about 0.3 s. HeavyBreak enemies resist thrusts; a swing breaks them.
- [ ] Shield aim: faces along controller forward, held 0.12 m ahead of the hand. Raised shield blocks melee (stagger x0.25, perfect = full). A still shield absorbs bolts; 2.5 m/s or faster reflects. Heavy strikes stay dodge-only.
- [ ] Render note: in the editor Mobile render the shield read very dark (near black) and large. Readable in the headset in the dark arena?

## 3. Holsters, standing

- [ ] Standing only: no seated toggle in settings (A15.15). Eye-height calibration still works.
- [ ] Holster poses: Katana on the hip; crimson Katana on `OffhandHolster`; Polearm grip at the main-hand hip, pole diagonal, head behind the shoulder; Shield flat on the off-hand hip; Whip handle on the hip with the chain coiled.
- [ ] Each can be grabbed naturally and resheathes after release.
- [ ] Note any pose that clips the body, is hard to reach, or hides a controller.

## 4. Left-handed pass (one room per weapon)

- [ ] Katana, Double Katana, Polearm + Shield, Whip each: holsters mirror, off hand swaps, triggers and grabbers swap, Arts follow the new hands.
- [ ] **QuickDraw (known bug):** `QuickDrawController` is hardcoded to the right hand. Confirm what happens left-handed. (open decision h)

## 5. Whip against bosses

- [ ] **Iron Warden:** `ParryGate` is Neutral on a Whip run (A15.10), so it is beatable without parry. Beat it with the Whip.
- [ ] **Maelgorn final phase:** same, beatable on a Whip run.
- [ ] Polearm + Shield keeps the gate even with the shield holstered (R4).

## 6. Lasso (A15.9)

- [ ] Natural spin at **shoulder height** and **overhead**; arm and wrist both comfortable.
- [ ] **No self-throw** on the wind-up.
- [ ] **False grabs during crack spam.** A false grab costs 4 s of attacks unless you let go. How often? Unfair?
- [ ] **Orbit comfort** at 1.5-3 m around the head: no nausea, nothing clips the face, no camera shake.
- [ ] The grab yank at 12 m/s reads and is not jarring.
- [ ] Captive **parks when you stop spinning**; it does not drift off.
- [ ] Fling on release or after about 4 s; cooldown about 8 s; captive death ends it and starts the cooldown.
- [ ] Contact: the enemy hit staggers and takes about 30, the captive takes about 30, once per orbit (1.6 s).
- [ ] **Lasso list from below:** does it pick the enemy you aim at when it is below you?
- [ ] **Shield trooper** as captive; **boss with adds** (the boss itself should not be grabbed, but contact staggers it, decision e).

## 7. Unlocks, select screen and saves

- [ ] **Whip challenge counting:** win a full run with at most 3 enemy strikes; the Whip unlocks. Hazards, blasts and guard chip do not count. Hits taken survive Save & Quit. Best shows on the card (`WIN WITH 3 HITS MAX / BEST: n`).
- [ ] Prices: Double Katana 300 Echoes + 1 win; Polearm + Shield 450 Echoes + 2 wins. Buying is in Shrine > ARMORY, whose picker is buy-only.
- [ ] **Old-save fallback:** a save with Nodachi or Tanto selected falls back to Katana; Echo balance untouched; **resume** of a run in progress still works.
- [ ] **Select screen click path with each hand:** START RUN > WEAPON SELECT > card > START. Last pick preselected.
- [ ] **Menu button while the panel is open**; **BACK** returns to the menu.
- [ ] **Locked card** is dimmed, always visible; clicking it does not select.
- [ ] **RESUME RUN** and **Daily Fracture** skip the screen (Daily uses the last pick).
- [ ] Render note (editor, Mobile tier, real boot scene): the panel reads and models sit in front of the cards, but the steel looks dark and the "RONIN 7" title shows through the translucent panel behind the heading. Check both in the headset.

## 8. Open decisions

From Amendment 15 "Readings pending user confirmation" (a-f) plus extras. Write yes / no / change:

| # | Question | Decision |
|---|---|---|
| a | Spin gesture measures the turning rate of the Whip ring's path in rig space, not controller angular velocity | |
| b | Lasso collisions bypass weakness resist (`DamageType.Collision`): about 30 on every enemy; damage boons still apply | |
| c | "Once per enemy per swing" for the lasso = once per orbit (`LassoReHitSeconds` 1.6 s) | |
| d | "Release" = opening the grip on the Whip | |
| e | **Lasso contact staggers bosses** and super-armour enemies (`ForceStagger`), as A15.9 literally says | |
| f | Crack threshold 8 m/s tip speed (`CrackSpeed`): a hard slash and **any launch flight touching an enemy** crack, a 3.6 m/s sweep does not | |
| g | **Daily and assisted wins unlock the Whip.** Should they? | |
| h | **QuickDraw** is hardcoded to the right hand (left-handed bug): fix now or later | |
| i | **Crimson blade's trail and light are still cyan.** Make them crimson? | |
| j | **2048 textures on the Tripo meshes** if Quest is tight (only if section 0 or OVR shows a problem) | |
| k | **Chain reach** 3.9 m at rest, about 3.9-4 m on a launch, vs A15's "about 4 m" | |

## 9. Gate items

- [ ] Export Gate: EditMode 2327/0/0 baseline (re-run before the build), PlayMode (last known 16-17/17).
- [ ] Pre-build render check, editor, 2026-11-06, Phase6_Boot > RunArena, seed 2026, Mobile + PC tiers, Whip and Polearm + Shield loadouts: all PASS on draws, SetPass, luma, magenta (numbers in the prep report). Katana and Double Katana were not rendered. Editor numbers are a proxy; device-only causes (Vulkan, multiview, HDR) need the headset.
- [ ] **Quest build (G3)** and **PCVR build (G4)** at 0.10.0; G5 smoke: both boot, enter a run, quit cleanly.
- [ ] G6 / G8 device gates on the new builds. Steady 90 Hz in the Quest build.

## 10. Soak: 4 runs

| # | Run type | Result | Depth | Crash / soft-lock? | Notes |
|---|---|---|---|---|---|
| 1 | Katana run | | | | |
| 2 | Polearm + Shield run | | | | |
| 3 | Whip run (all sectors, at most 3 hits if you can) | | | | |
| 4 | Double Katana run, left-handed | | | | |

Afterwards, pull `runlog.jsonl` and logcat: 0 errors, no crash, no soft-lock.

## Results and sign-off

- Overall notes:
- [ ] Phase 9 signed off by the user on build ______ (date ______). Then the user tags `roguelike-phase9`. Next: Phase 10 (design session first).

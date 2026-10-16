# Ronin7 Roguelike — Review & Phased Implementation Roadmap

**Date:** 2026-10-11 · **Branch:** `roguelike` (never `main`) · **Targets:** Meta Quest 2/3/3S/Pro (Android, IL2CPP, Vulkan) + PCVR (Win64, OpenXR)
**Source:** five parallel read-only reviews of the working tree (enemies & weaknesses; skills, boons & stats; run flow & UX; graphics & performance; export, audio, comfort & everything else).
**Contract:** `Roguelike-Design.md` (incl. Amendments 1–10) stays authoritative. Where this roadmap changes behaviour it says so and the amendment must be appended when the change lands.

Paths are relative to `Project/Assets/Ronin7/` unless they start with `Project/`.

---

## How to read this

- **Part A** is the review: what exists today, with the bugs found.
- **Part B** defines the **Export Gate**. Every phase ends by passing it, so the game is shippable after each phase.
- **Part C** has the phases. Each task has an ID, the files it touches, step-by-step instructions, the tests to add, and acceptance criteria.
- **Part D** lists the decisions only you can make. Each has a recommended default, and the plan assumes that default.

Phase order and why:

| Phase | Theme | Why this order |
|---|---|---|
| **0** | Ship-safe: export blockers and data-loss bugs | Nothing else counts if a run erases the campaign save or the Quest build fails store checks |
| **1** | Combat foundation and balance | Every later feature (weaknesses, stats, boons) hooks into the damage pipeline, so it has to be correct first |
| **2** | Run flow and the meta loop | Completes the core roguelike loop: spend Echoes, branching map, readable rooms |
| **3** | Enemy diversity and weaknesses | Built on the Phase 1 damage filter |
| **4** | Player stats, skills and boon expansion | Built on the Phase 1 stat slots and the Phase 3 events |
| **5** | Graphics: "details everywhere" | Art is spent on a stable game, and the perf budget is measured on what will actually ship |
| **6** | Audio, comfort, accessibility, narrative, store submission | Release polish |

Effort in engineer-days (rough, one person with agents): P0 ≈ 4–5, P1 ≈ 6–8, P2 ≈ 8–10, P3 ≈ 10–12, P4 ≈ 10–14, P5 ≈ 15–25 (art-bound), P6 ≈ 6–8.

---

# PART A — Review: the current state

## A1. What exists and works

- **Run structure:**
  - 3 sectors × 5 nodes, linear: `[Combat, roll, roll, Forge, Boss]`.
  - Deterministic from a seed (`RunRng`, golden vectors).
  - Permadeath.
  - Echoes are persisted in `meta.json`, separate from the campaign `SaveData`.
- **Save:**
  - `SaveSystem` writes atomically and keeps a `.bak` fallback.
  - `RunSnapshot` (`run.json`) is written on every node entry.
  - The boot menu offers RESUME RUN / ABANDON RUN.
- **Combat:**
  - `BladeDamager`: EMA swing-speed damage, swept pass-through, once per enemy per swing.
  - Parry, perfect parry and blade clash.
  - `PostureMeter` (break → stagger).
  - Per-hit flinch (`FlinchTiming`).
  - Directional hit haptics and a vignette flash. These are scripted, but see the bugs below: they are not active in RunArena.
- **Abilities:** 5 abilities (Weakpoint Sight, Overdrive, Phase Step, Unbroken, Mirror), granted by Epic boons.
- **Boons:** 15 boons (7 Common, 3 Rare, 5 Epic), a 3-choice world panel, reroll tokens, and a 60 s auto-pick fallback.
- **Content:** 14 baked Tripo enemy prefabs plus a wrist HUD on the left controller.
- **Tests:** EditMode baseline **1281/1281**, PlayMode 70/70. Both need re-running against the uncommitted working tree.

## A2. Bugs found (ranked)

| ID | Sev | Bug | Where | Fixed in |
|---|---|---|---|---|
| BUG-01 | CRITICAL | Starting a run autosaves a **blank `CampaignState` over the player's campaign slot** on every node arrival (15× per run). The `.bak` rolls to blank as well. | `Flow/GameFlowManager.cs:361` (`ShouldAutosaveOnArrival`) | P0-01 |
| BUG-02 | CRITICAL | **Forge and Treasure fully heal** the player. `RunArenaController.Start` clears zero-enemy rooms *before* `onLoaded` restores the carried HP, so the reward reads the rig's prefab HP. | `RunDirector.cs:323-332, 551-555`; `RunArenaController.cs:83-94` | P0-02 |
| BUG-03 | CRITICAL | **No pause and no focus-loss handling.** With the Quest menu open or the headset off, enemies keep attacking, and the 60 s boon fallback auto-picks while suspended. This is a Meta VRC failure. | none exists | P0-03 |
| BUG-04 | HIGH | **No way out of a run** except death, a win, or killing the app. `AbandonRun()` has no caller. An enemy stuck out of bounds soft-locks the run. A future Return-to-Menu would leave `InRun == true` and dead-lock ENTER THE FRACTURE. | `RunDirector.cs:146,162,188` | P0-04 |
| BUG-05 | HIGH | **`CombatFeedbackController`, `HeartbeatHaptics`, `BladeClashFeedback` and `SettingsPanel` are missing from RunArena.** That removes hit haptics, the deflect slow-mo, the hit flash (the only off-screen hit cue) and in-run settings. | `Editor/Roguelike/RoguelikeArenaBuilder.cs:69-108` | P0-05 |
| BUG-06 | HIGH | **Post-processing never renders** in RunArena or the boot menu. The cameras have no `UniversalAdditionalCameraData`. Quest still pays for HDR and MSAA intermediates but gets no bloom or grade. | `Scenes/RunArena.unity`, `Phase6_Boot.unity`, `Editor/XRRigBuilder.BuildRig` | P0-06 |
| BUG-07 | HIGH | **Quest 90 Hz and foveation calls are no-ops.** They use `Unity.XR.Oculus` APIs while only the OpenXR loader is active. The `shadowDistance` clamp also does nothing, because URP reads the pipeline asset. | `Flow/QualityBootstrap.cs` | P0-07 |
| BUG-08 | HIGH | `Dream_Ghost_Manifestation` (elite) uses `Ch11DreamingArchive`: **1 HP, 0 dmg, 0 speed, cooldown 999**. It is an inert free kill. | `Data/Roguelike/EnemySpawnTable.asset:~67`; `Editor/Roguelike/RoguelikeDataBuilder.cs` | P0-08 |
| BUG-09 | HIGH | **Elite rooms are easier than Combat rooms in sector 0**: one trash mob at ×1.15, versus 3–4 trash mobs. Elite also pays more. | `RunScaling.EnemyCount`, `WaveComposer.Compose` | P1-09 |
| BUG-10 | HIGH | **Inverted boss curve.** Final boss Maelgorn ≈ 572 HP / 30 dmg; sector-0 Warden ≈ 568 HP / 37 dmg. Maelgorn phases 2 and 3 are never used. | `RunScaling`, `EnemySpawnTable.BossEntry` | P1-10 |
| BUG-11 | HIGH | **Universal attack lockout.** Any hit during Windup cancels the attack, and re-flinch immunity is only 0.4 s. Waggling the blade every 0.5 s stops *every* enemy, bosses included, from ever attacking. (You deferred the general fix; P1-04 fixes it per type only.) | `MeleeAttacker.cs:353-375`, `Enemy.cs:37` | P1-04 |
| BUG-12 | HIGH | **Weakpoint Sight is a free, permanent 2× damage toggle**: no cost, no duration, no cooldown. | `Player/WeakpointSight.cs:41,79-92` | P1-06 |
| BUG-13 | HIGH | **Unbroken resets every node**: about 14 lethal saves per run. The interceptor also spends Second Wind *before* the free Unbroken, and `ReviveFraction` stays at 0.3 after Second Wind is spent. | `Player/UnbrokenWard.cs:45`; `RunDirector.cs:741-743` | P1-07 |
| BUG-14 | HIGH | **Epic "Grant ability" boons can be dead picks.** `AbilityAccess.Has` also returns true for abilities unlocked by the campaign. | `Core/Roguelike/AbilityAccess.cs:15`; `BoonOfferPicker.Pick` | P1-08 |
| BUG-15 | HIGH | **Echoes cannot be spent.** `TrySpend` and `SetUpgradeLevel` have no callers, so meta-progression does not exist yet. | `Core/Roguelike/MetaProgression.cs` | P2-01 |
| BUG-16 | HIGH | **The boon panel follows head position but never rotation.** Turn >90° and it ends up behind you, mirrored and unclickable. Looking down pushes it into the floor. | `Flow/Roguelike/BoonOfferPanel.cs:94` | P1-13 |
| BUG-17 | MED | **The enemy telegraph tint is a no-op on art enemies.** `RendererTint` writes `_BaseColor`/`_Color`; the glTFast shader reads `baseColorFactor`. **Status:** the colour/rim, haptic and audio telegraph landed in this fix; the attack animation part is tracked by P1-15. | `Core/RendererTint.cs` | P1-11 || BUG-18 | MED | **Posture does not scale.** `postureMaxFraction = 0` everywhere, so posture breaks at a flat 100: trivial on bosses, irrelevant on trash. | all `EnemyDefinition` assets; `Enemy.cs:61-65` | P1-05 |
| BUG-19 | MED | The heal-it-back "weakness" pattern (story `DreamPhantom` etc.) cannot block a lethal hit, because `Current = 0` is set before `Damaged` fires. | `Combat/Health.cs:86-108` | P1-01 (pre-filter) |
| BUG-20 | MED | Combo boons do nothing against single targets, because a same-target hit resets the chain. That makes them dead on every boss. | `Player/ComboMomentumController.cs:93` | P4-05 (Reversal) |
| BUG-21 | MED | **Overdrive inflates blade speed** by 2.86×, because `SmoothSpeed` divides by the slowed `fixedDeltaTime`. Any flick reaches max damage, and the perfect-parry window grows to 0.34 s. | `Combat/BladeDamager.cs:78`, `Core/TimeScaleArbiter.cs:61` | P1-06 |
| BUG-22 | MED | **Rarity weights apply per boon, not per tier**, so adding boons silently changes the tier odds. Epics would vanish once the planned boons are added. | `Combat/Boons/BoonOfferPicker.cs:53-58` | P1-08 |
| BUG-23 | MED | Tripo enemy textures are probably **uncompressed RGBA32**: 3 × 2048² per enemy, roughly 64 MB per type. glTFast sub-assets bypass the TextureImporter. | `Art/Generated/Characters3D/**` | P1-12 |
| BUG-24 | MED | Reinforcement activation runs `FindObjectsByType<Health>()` in the killing-blow frame, which breaks the `Health.Active` rule. Reinforcements also appear at pre-rolled spots, often behind you, with no telegraph. | `Enemies/MeleeAttacker.cs:186,450-455`; `RunArenaController.ActivateFront` | P1-03, P2-08 |
| BUG-25 | MED | **Save edge cases:** discarding an old-version `run.json` loses unbanked Echoes; `MetaSaveData.version` is never checked; `Save()` before `Load()` zeroes `meta.json`; dying during the fade-in loses the run summary. | `RunSnapshot.Read`, `MetaProgression`, `RunDirector.SceneLoadRoutine:670` | P0-09, P2-03 |
| BUG-26 | LOW | Summary off-by-one: "ROOMS CLEARED" counts the room you died in, and BEST DEPTH is 0-based. | `Core/Roguelike/RunSummary.cs:61`; `MetaProgression.cs:76` | P2-03 |
| BUG-27 | LOW | ABANDON RUN sits 9 cm under RESUME RUN with no confirmation, and it immediately starts a new run. | `RoguelikeEntryPointPatcher.cs:89-153` | P0-04 |
| BUG-28 | LOW | The sword lost its roadmap `CyberSword` shader: "Build All Art Prefabs" reverts it to SamuraiToon. On PC (Forward+), custom shaders lack `_CLUSTER_LIGHT_LOOP`, so they get no point lights. | `Prefabs/Art/Sword_Katana.prefab`, `Art/Shaders/*` | P5-04 |
| BUG-29 | MED | Player feels a slight **delay receiving damage** (hit lands, damage/feedback feels late). Investigate only: enemy hit timing vs Active window, Health -> CombatFeedbackController/Haptics/audio path, any frame-delayed event. | `Enemies/MeleeAttacker.cs`, `Combat/Health.cs`, `Player/CombatFeedbackController.cs` | P2-10 |
| BUG-30 | HIGH | **Forge "no effect"** (Phase 2 headset playtest, 2026-10-14): the MEND/TEMPER panel appeared and was clicked, but no effect was visible. Cause unconfirmed. Suspects: MEND heals 60% of *missing* HP (0 at full HP) and shows no amount; TEMPER's +1 reroll is displayed nowhere; or the effect is not applied. | `Flow/Roguelike/` Forge panel; `RunDirector.cs` | P3-00 (TEMPER card: P4-07) |
| BUG-31 | MED | `SceneDebugOverlay` shows a green scene-name label ~0.6 m in front of the player in **release builds** (no `RONIN7_DEV` gate). Still visible to the user. | `Flow/SceneDebugOverlay.cs` | P3-00 |
| BUG-32 | LOW | **Onboarding cannot be re-verified**: the tutorial is skipped when `runsCompleted > 0` (by design, P2-08), so the Phase 2 prompts were never seen in the headset. | `Flow/Roguelike/` onboarding; `MetaProgression` | P3-00 (dev reset), P6-06 (Settings toggle) |
| BUG-33 | MED | **One currency name for two things**: the user could not use the Shop because they had spent their Echoes at the Shrine. Design decision D-11: fully separate currencies. | `MetaProgression.cs`, `RunState`, Shop/Shrine/HUD | P4-11 |
| BUG-34 | LOW | Hard-coded `LeftHand` haptics ignore `GameSettings.LeftHanded` (P2-09 fixed only `ParryFlowController`). | `Player/CombatFeedbackController.cs`, `AdrenalineFlow`, `BladeClashFeedback`, `HeartbeatHaptics` | P3-00 | Resolved P3-00: not reproducible — all four pulse both hands (side-weighted for hits), handedness-neutral; no code change. Guard test `HapticHand_FollowsLeftHanded` covers the shared `HandednessRig.OffHand` lookup.
| BUG-35 | LOW | `RunRng.ForNode` first draw barely varies across low seeds (seeds 1..64 gave the same Gamble outcome). Needs a seed mix; this changes goldens. | `Core/Roguelike/RunRng.cs` | P4-11 (same snapshot version bump) |
| BUG-36 | LOW | `BuildMenu`: release and dev Windows builds share `Builds/Windows`; a dev build overwrites `UnityPlayer.dll`. | `Editor/BuildMenu.cs` | P3-00 |
| BUG-37 | LOW | Quest logcat on every RunArena load: "Main Camera does not use a Tracked Pose Driver (Input System)". | `Editor/XRRigBuilder.BuildRig`; RunArena camera | P3-00 (resolved: harmless, camera driven by `PoseFollower`; see P3-00 item 6) |
| BUG-38 | LOW | Drones and bosses still move unsmoothly after P2-11 (humanoids are fine). User report, Phase 2 headset playtest. | `Enemies/MovementSmoothing.cs`, drone/boss behaviours | P3-09 |

## A3. Domain findings in brief

**Enemy diversity.**
- **All 14 enemies are the same enemy.** They share `Enemy : MeleeAttacker`, use one overhead chop, and have identical telegraph (0.8 s), range, posture and stagger. Only HP, speed, damage and mesh differ.
- The missing archetypes are: ranged, flying, shielded, charger, support, summoner, teleporter, armored, bomber, elite affixes and multi-phase bosses.
- Art enemies have **no visible blade**. The parry target is an invisible 0.5 m segment.
- "Drones" walk on the floor and stand 1.8 m tall.
- There is no damage-type or weakness system at all.

**Skills, boons and stats.**
- About half the boons are strictly dominated or numerically irrelevant: Keen Edge < Whetstone; Ironskin < Second Skin; Flow/Combo Initiate give +0–6%.
- Two Epics are overtuned (Weakpoint Sight, Unbroken) and one is weak late (Mirror: 8 dmg/1.4 s, unscaled).
- No damage-reduction hook exists.
- Every ability resets per node, because the rig is rebuilt each scene load.
- The free inputs on foot are **both triggers** and gestures. A/B/X are already overloaded with 2–4 interactions each.

**Run flow.**
- The map is linear and its next node is never shown.
- Forge and Treasure are granted in the dark (`skipReveal`).
- There is no sector transition beat, no death or victory beat, and no HP display.
- There is no onboarding.
- The 24 × 24 m greybox room is identical every node.
- Enemies spawn as a rolling drip, not readable waves.
- A run lasts about 25–35 min, which is long for a single Quest session; it needs a mid-run checkpoint.

**Graphics.**
- The arena is 6 runtime cubes plus 2 point lights, with no props (`propPrefabs: []`) and a ceiling that hides the sky.
- Nothing is baked, and there are no probes.
- Particles are untextured squares.
- UI uses legacy `Text`/Arial at 16 px with no icons.
- The hands cost **52 draw calls** (13 renderers × 2 materials × 2 hands).
- The custom shaders are SRP-Batcher-compatible and a decent base.

**Export and everything else.**
- The bundle ID is the Unity template default, there is no keystore, and the splash is the default.
- 30 scenes are in the build; only 2–3 are needed.
- AR OpenXR features are enabled on Android, which adds manifest permissions and review risk.
- There is no build script and no `link.xml`.
- The MCP NuGet DLLs may ship to Android.
- Audio:
  - It has no `AudioMixer`.
  - Most clip slots are empty: only 3 ambiences, 2 hits and footsteps exist.
  - It does not subscribe to `RoomCleared`, `RunStarted` or `RunEnded`.
  - There is no boss music.
- Accessibility covers only snap turn and the vignette. There is no seated mode, no left-handed mode and no "reduce flashes" option.
- There is no run telemetry.
- The roguelike does not use the story (Kessler and the bible are unused).

---

# PART B — The Export Gate (run at the end of every phase)

A phase is **done** only when all of the following pass. Add these to a checklist in the phase's PR or commit message.

| # | Check | How |
|---|---|---|
| G1 | **EditMode green**, 0 failures, 0 skips. Record the new count as the baseline in memory and `CLAUDE.md`. | Editor closed: `Unity.exe -runTests -batchmode -projectPath "D:\ronin7\Project" -testPlatform EditMode -testResults D:\ronin7\res.xml -logFile D:\ronin7\Logs\gate.log`, then wait for `res.xml` (the launcher returns early). Editor open: run via the `tests-run` MCP and read `Logs/Editor.log`. |
| G2 | **PlayMode green.** | Same command with `-testPlatform PlayMode`; it needs a GPU. |
| G3 | **PCVR build** succeeds. | `Tools/Ronin7/Build/Windows (PCVR)` (P0-12), or batchmode `-executeMethod Ronin7.Editor.BuildMenu.BuildWindows`. Exit code ≠ 0 on failure. |
| G4 | **Quest build** succeeds (Android, IL2CPP, ARM64) and installs. | `Tools/Ronin7/Build/Quest (APK)`, then `adb install -r`. Do this **after** any in-headset PCVR session, because switching target reimports. |
| G5 | **Device smoke run.** Full run on seed 2026, using the dev skip (P0-11) for speed: boot → 15 nodes → win → summary → menu. Then a second run that dies at node 1. | Quest + PCVR Link. `adb logcat -s Unity` shows no exceptions or `couldn't be loaded`. |
| G6 | **Frame budget.** Reference room with 4 enemies: Quest 3 at **90 Hz** with **≤ 1% stale frames over 2 min**; Quest 2 at 72–90 Hz. GPU ≤ 10 ms. | OVR Metrics Tool overlay; the Unity Profiler over ADB on a Development build. |
| G7 | **Save compatibility.** A `meta.json` and `run.json` written by the previous phase's build load correctly. Never bump `RunSnapshot.CurrentVersion` without the banking migration (P0-09). | Keep a `TestData/saves_phaseN/` fixture and add an EditMode test that loads it. |
| G8 | **VRC sanity:** Quest menu button → pause; headset off → pause; controller tracking lost → no phantom swings; resume works. | Manual on device. |
| G9 | **Ship it.** Bump `bundleVersion` (`0.N.0`) and `AndroidBundleVersionCode` (+1). Commit on `roguelike` with exact-path staging (no `.mcp.json` or NuGet noise). Tag it `roguelike-phaseN`. | `git tag roguelike-phase0` and so on |

**G5 carry-over checks (Phase 2 playtest, 2026-10-14; not observed in the headset, verify at the next gate):** Treasure TAKE card; sector card and CHECKPOINT; Forge MEND/TEMPER visible effect (BUG-30); onboarding prompts via the dev reset (BUG-32); no green scene-name label in a release build (BUG-31); drone and boss smoothness (BUG-38).

The **dev skip** used in G5 and all debug tooling sit behind the `RONIN7_DEV` scripting define, which is set only in Development builds, so release builds never contain them.

---

# PART C — Phases

Conventions for every task:
- **Assemblies:** respect the asmdef boundaries (`Ronin7.Core` ← `Combat` ← `Enemies` ← `World`/`Player`/`Ship` ← `Flow`). **Ship must never reference Player.**
- **Pure logic** goes in a static or plain class marked `internal`, with `InternalsVisibleTo("Ronin7.Tests.EditMode")`. MonoBehaviours stay thin.
- **Enums that are serialized** (`BoonEffectKind`, `BoonRarity`, `DamageType`, `TimeScaleChannel`, `RoomKind`, `WeaknessKind`) are **append-only**.
- **Save formats:** append fields with initializers (JsonUtility is forward-compatible). Do not bump versions without a migration.
- **Scenes:** never re-run destructive builders on polished scenes. Patch additively **and** mirror the change in the builder source. RunArena is builder-owned with no hand polish, so rebuilding it is allowed; the patcher re-adds the HUD.
- **No camera shake. No per-frame `Find*`.** Use `Health.Active`. No runtime `Shader.Find` or `new Material`; use serialized `.mat` assets.
- **Reuse first:** `ProjectilePool`, `RendererTint`, `SaveSystem`, `VfxPrefabBuilder`/`VfxPool`, `LightBudget.ShouldAnimate`, `ProceduralAudioSynth`, the panel-factory idiom (`RunSummaryPanelFactory`).

---

## PHASE 0 — Ship-safe (export blockers and data loss)

**Goal:** the current game, with no new features, is safe to put on a Quest and a PC. It loses no data, survives focus loss, has an exit, carries correct identity, and runs at 90 Hz with post-processing working.

### P0-00 Baseline the working tree
1. Run G1 and G2 against the current uncommitted tree (47 modified files). Fix anything red before touching anything else.
2. Commit the in-progress work (run-summary panel, `.bak`, `RunSnapshot`, flinch, swept damage) as `feat(roguelike): WIP baseline before roadmap`. Stage exact paths only, and leave out `Project/.mcp.json`, `Assets/Plugins/NuGet/*` and `CLAUDE.md` unless they are intended.
3. Add `.gitattributes`: `* text=auto` and `*.cs text eol=lf`, to stop the CRLF churn.

### P0-01 Stop the campaign autosave during runs (BUG-01)
- **File:** `Flow/GameFlowManager.cs`.
- **Steps:**
  1. Change the signature to `internal static bool ShouldAutosaveOnArrival(GameMode mode, bool quickBoot, bool inRun) => !quickBoot && mode != GameMode.Boot && !inRun;`.
  2. At the call site (line ~361), pass `RunState.InRun`. `GameFlowManager` lives in Flow, which can see Core.
  3. Update all existing call sites and tests to the new arity.
- **Tests:** `GameFlowManagerTests.ShouldAutosaveOnArrival_InRun_ReturnsFalse`; `..._NotInRun_OnFoot_ReturnsTrue` (regression).
- **Accept:** load campaign slot 1, note its contents, go to the menu, play 3 run nodes, quit. Slot 1 and its `.bak` are byte-identical to before.

### P0-02 Defer zero-enemy room clears until the rig is restored (BUG-02)
- **File:** `Flow/Roguelike/RunDirector.cs`.
- **Steps:**
  1. Add `private RoomCleared? deferredClear;`.
  2. Rename the body of `OnRoomCleared` to `OnRoomClearedNow(RoomCleared evt)`. The new `OnRoomCleared` begins with `if (transitioning && evt.NodeIndex == RunState.NodeIndex) { deferredClear = evt; return; }` and otherwise calls `OnRoomClearedNow`.
  3. Replace the `onLoaded` delegate passed to `LoadSceneFaded` (line ~660) with `OnArenaLoaded()`, which calls `ApplyBoonInventoryToRig();` and then `if (deferredClear is RoomCleared c) { deferredClear = null; OnRoomClearedNow(c); }`.
  4. Clear `deferredClear` in `StartRun`, `TryResumeRun` and `EndRun`.
  5. Extract `internal static float ForgeResultHealth(float stored, float max)` (carry-over plus 60% of missing, clamped) and `internal static bool ShouldDeferClear(bool transitioning, bool sameNode)`. Use them in the real code paths.
  6. `skipReveal` still works: `AdvanceNode` → `StartSceneLoad` latches `pendingScene` while `transitioning` is true.
- **Tests:**
  - EditMode: `ForgeResultHealth(40,100)==76`, `(100,100)==100`, plus a `ShouldDeferClear` truth table.
  - PlayMode (extend `Tests/PlayMode/RunArenaLifecycleTests.cs`): choose a seed whose node 3 is a Forge, `NotePlayerHealth(40)`, load node 3, assert `RunState.PlayerHealth ≈ 76`. Do the same for Treasure and assert HP stays 40.
- **Accept:** on device, entering a Forge at 40/100 HP leaves you at 76.

### P0-03 Pause and focus-loss handling (BUG-03, VRC)
- **New:** `Flow/Roguelike/RunPauseController.cs` on the persistent "Game" GameObject in `Phase6_Boot`. Add it via `RoguelikeEntryPointPatcher`, additively.
- **New enum value:** append `Pause` to `Core/TimeScaleChannel.cs`.
- **Steps:**
  1. Trigger `Pause()` from `OnApplicationFocus(false)`, `OnApplicationPause(true)`, and the left-controller **menu button**. For the menu button, add an `InputActionReference` to the action `UI/Pause` in `Settings/Ronin7Input.inputactions`, bound to `<XRController>{LeftHand}/menuButton`.
  2. `Pause()` does four things:
     - `TimeScaleArbiter.SetRequest(TimeScaleChannel.Pause, 0f)`
     - `RunDirector.Instance?.FreezeOfferTimer(true)`
     - `AudioListener.pause = true`
     - shows the pause panel
  3. `TimeScaleArbiter`: when the effective scale is ≤ 0, keep `fixedDeltaTime = baseline`. A value of 0 is invalid.
  4. `RunDirector.FreezeOfferTimer(bool)`: on freeze, store `remaining = offerFallbackDeadline - Time.unscaledTime`. On thaw, `offerFallbackDeadline = Time.unscaledTime + remaining`. Extract `internal static float RemainingOfferSeconds(float deadline, float now)`.
  5. **Pause panel:**
     - Build it with `RunPausePanelFactory`, using the same idiom as `RunSummaryPanelFactory`: head-anchored, `TrackedDeviceGraphicRaycaster`, Select-based press.
     - Buttons: **RESUME**, **SETTINGS** (instantiates the existing `Audio/SettingsPanel`), **RECALIBRATE HEIGHT**, **SAVE & QUIT** (P0-04 `SuspendRun`), **ABANDON RUN** (hold 1 s to confirm).
     - Resume happens **only on an explicit RESUME press**, never automatically when focus returns.
  6. On resume: clear the Pause channel, thaw the timer, `AudioListener.pause = false`.
  7. Also call `PlayerPrefs.Save()` on pause: Quest kills backgrounded apps without a quit callback.
  8. **Tracking loss:**
     - In the blade controller (the `Sword` grab follow), read `InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.isTracked, out bool t)`.
     - When it is false, set `BladeDamager.enabled = false` and reset the EMA. Re-enable after 3 consecutive tracked frames. This prevents phantom swings from pose jumps.
     - Extract `internal static bool BladeArmed(int trackedFrames)`.
- **Tests:**
  - `TimeScaleCompositionTests.Pause_ZeroWins` (Pause=0 with Overdrive=0.35 gives 0; clearing Pause gives 0.35)
  - `TimeScaleArbiterTests.ZeroScale_KeepsBaselineFixedDelta`
  - `RunDirectorTests.RemainingOfferSeconds_FreezeThaw`
  - `BladeTrackingTests.BladeArmed_RequiresThreeTrackedFrames`
- **Accept:** in a fight, press the Quest home button and wait 90 s. You take no damage, no boon is auto-picked, and RESUME continues the fight.

### P0-04 Exit paths, abandon safety, out-of-bounds kill (BUG-04, BUG-27)
- **Steps:**
  1. Add `RunDirector.SuspendRun()`. It calls `DismissOfferPanel()`, keeps the existing node-entry snapshot, calls `RunState.Reset()`, and runs `StartSceneLoad(runEndScene)` in `GameMode.Boot`. The menu then shows RESUME RUN.
  2. In `GameFlowManager.ReturnToMainMenu`, add `if (RunState.InRun && RunDirector.Instance != null) { RunDirector.Instance.SuspendRun(); return; }`.
  3. Wire the pause panel's ABANDON RUN to the existing `AbandonRun()`, behind a **1 s hold**. The boot-menu ABANDON gets a second-click "CONFIRM ABANDON?" state.
     - Abandon now **only** banks Echoes and returns to the menu. It no longer auto-starts a new run.
     - Move the menu ABANDON button to 25 cm below RESUME in `RoguelikeEntryPointPatcher` (it is currently 9 cm).
  4. In `RunArenaController`, maintain `List<Health> alive` (add on activate, remove on death) and run `InvokeRepeating(nameof(CheckBounds), 1f, 1f)`. Any enemy with `OutOfArena(localPos, halfExtent)` is killed by `ApplyDamage(Max)` so the normal clear path runs. Extract `internal static bool OutOfArena(Vector3 local, float halfExtent)` (|x| or |z| > halfExtent + 0.5, or y < −2).
- **Tests:**
  - `RunArenaControllerTests.OutOfArena_*` (table)
  - `RunDirectorTests.SuspendRun_LeavesSnapshot_ClearsInRun` (uses the SaveSystem `DirectoryOverride` temp-dir pattern)
  - `MainMenu_Abandon_DoesNotStartRun`
- **Accept:** SAVE & QUIT mid-run, then RESUME from the menu, puts you back at the node-entry state. ENTER THE FRACTURE always works afterwards.

### P0-05 Combat feedback components in RunArena (BUG-05)
- **Steps:**
  1. In `Editor/Roguelike/RoguelikeArenaBuilder.cs`, add `CombatFeedbackController` to a scene "Feedback" GameObject (the same way `Chapter16Builder.cs:246` does it), plus `HeartbeatHaptics` and `BladeClashFeedback` on the rig.
  2. Rebuild `RunArena.unity` with `Tools/Space Samurai/Roguelike/Build Run Arena Scene` (it is builder-owned), then run `RoguelikeEntryPointPatcher` again for the HUD.
  3. Add an EditMode scene-content test, `RunArenaSceneTests.ContainsRequiredFeedbackComponents`. It opens the scene via `EditorSceneManager.OpenScene(..., OpenSceneMode.Additive)` and asserts each component type exists, so this regression cannot return.
- **Accept:** blade hits pulse the controller, a deflect gives a short slow-mo, and taking a hit flashes the vignette edge.

### P0-06 Turn post-processing on (BUG-06)
- **Steps:**
  1. In `Editor/XRRigBuilder.BuildRig`, ensure the main camera has `UniversalAdditionalCameraData` with `renderPostProcessing = true`. Patch `RunArena.unity` and `Phase6_Boot.unity` additively, or rebuild RunArena.
  2. In `Flow/GraphicsDirector.SetQuality`, decide post per tier by toggling `cameraData.renderPostProcessing`:
     - PCVR on, with the full grade.
     - Quest 3 on, **bloom only** (intensity 0.35, HQ off, `skipIterations` 2), kept only if it measures ≤ 1.5 ms GPU.
     - Quest 2 off.
     - Tier detection: `SystemInfo.deviceModel` contains "Quest 3" (or 3S/Pro); `GraphicsRuntime.Quality` already exists.
  3. In `Mobile_RPAsset`: if Quest 2 runs without post, set HDR to follow the same tier switch at runtime (`UniversalRenderPipelineAsset.supportsHDR`). Otherwise set `m_SupportsHDR: 0`.
  4. Remove the leftover sample components from `Settings/DefaultVolumeProfile.asset`: CopyPasteTestComponent2, TestAnimationCurveVolumeComponent, OasisFog, Panini, FilmGrain.
- **Test:** `GraphicsDirectorTests.PostEnabledFor(Tier)` truth table, as a pure static.
- **Accept:** the Frame Debugger shows an `UberPost` pass on PC.

### P0-07 OpenXR refresh rate and foveation, real shadow distance (BUG-07)
- **File:** `Flow/QualityBootstrap.cs`, plus `Project/Assets/XR/Settings/OpenXR Package Settings.asset`.
- **Steps:**
  1. Delete the `Unity.XR.Oculus.Performance` and `Utils` calls. Get the display with `var d = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();`.
  2. Refresh: `d.TryRequestDisplayRefreshRate(90f)` (meta-openxr `MetaOpenXRDisplaySubsystemExtensions`; `DisplayUtilitiesFeature Android` is enabled). Log the actual rate.
  3. Foveation: `d.foveatedRenderingLevel = isQuest3 ? 0.75f : 0.5f; d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;`.
  4. In XR Plug-in Management > OpenXR > Android:
     - Enable **Foveated Rendering** and set API to **SRP Foveation** (`m_foveatedRenderingApi: 1`).
     - Enable **Automatic Dynamic Resolution**, range 0.7–1.0.
     - Keep **SpaceWarp off**: it smears fast blade motion.
  5. Disable the unneeded features on Android and Standalone: ARSession, ARCamera, ARPlane, ARMesh, AROcclusion, ARRaycast, ARAnchor, ARBoundingBox, ColocationDiscovery. Confirm the Meta Quest feature targets Quest 2, Pro, 3 and 3S, and that the Touch Plus and Touch Pro interaction profiles are **enabled**.
  6. Shadow distance: delete the `QualitySettings.shadowDistance` line. Set `Mobile_RPAsset.m_ShadowDistance` to `0` (no realtime shadows on Quest; blob shadows come in P5) and `PC_RPAsset` to 20 with 2 cascades.
  7. Once there are no remaining uses, drop the `Unity.XR.Oculus` reference from `Ronin7.Flow.asmdef`.
- **Accept:** OVR Metrics shows 90 Hz and foveation active. The logcat has no `[QualityBootstrap] Could not set` lines. `aapt dump permissions` lists no camera or scene permissions.

### P0-08 Fix the broken spawn data (BUG-08)
- **Source of truth:** `Editor/Roguelike/RoguelikeDataBuilder.cs`. Fix the builder, then re-run it. Do not hand-edit only the asset.
- **Steps:**
  1. Point `Dream_Ghost_Manifestation` at a combat-viable definition. Create `Data/Roguelike/Enemies/RL_DreamGhost.asset` as a clone of `Ch11GhostManifestation` with 90 HP, 12 dmg, speed 1.4, cooldown 1.0. **Do not edit the story `Ch*` assets**, because the chapters use them.
  2. Raise `Spectral_Grave-Guardian` to 120 HP via its own `RL_` clone.
- **Tests:** in `SpawnTableInvariantTests`, add:
  - `EveryDefinition_IsCombatViable` (damage > 0, moveSpeed > 0, maxHealth ≥ 20, cooldown < 5)
  - `EveryEliteOnlyEntry_IsTougherThanDepth0Trash`

### P0-09 Save hardening (BUG-25)
1. **Bank on discard:**
   - `RunSnapshot.Read`: when the version mismatches, call `MetaProgression.AddEchoes(s.echoesEarned); MetaProgression.NoteRunEnded(s.nodeIndex, false); MetaProgression.Save();` before deleting.
   - Test: `VersionMismatch_BanksEchoes`.
2. **Newer meta is read-only:**
   - In `MetaProgression.Load`, set a `saveBlocked` flag when `data.version > CurrentVersion`; `Save()` then logs and no-ops.
   - Test: `NewerVersion_SaveLeavesFileByteIdentical`.
3. **Save before Load:**
   - Add a `static bool loaded` flag; `Save()` returns with an error if it is not set.
   - Test: `SaveBeforeLoad_NoOp`.
4. **Summary lost during fade-in:**
   - In `RunDirector.SceneLoadRoutine`, gate `RunState.End()` and `ShowRunSummary()` on `ShouldFinalizeRunEnd(EndingRun, loadedScene, runEndScene)`.
   - Test: a table test.
5. **Unknown boon on resume:**
   - Log `Debug.LogWarning` once per unknown id.
   - Test: `LogAssert`.
6. **Quest `File.Replace` with backup:**
   - Verify once on device that writing `meta.json` twice leaves `meta.json.bak` in place.
   - If it doesn't, fall back to `File.Copy` + `File.Delete`, guarded by `#if UNITY_ANDROID`.
   - **Verified on device 2026-10-14:** `File.Replace` on Quest external storage logs an SELinux `link` denial, but the runtime falls back to copy; `meta.json.bak` is kept and saves are current. The fallback is not needed. The code comment on `SaveSystem.ReplaceWithBackup` is stale (docs-only change; fix it with the next code touch).
7. **Map drift:** next to the `RunMapGenerator` golden vectors, add the comment `// if these change, bump RunSnapshot.CurrentVersion (banking migration in Read handles old runs)`.

### P0-10 Identity, scene list, packaging (export)
1. **Player settings:**
   - Company `<YourStudio>`; Package `com.<studio>.ronin7` (see D-01); Version `0.1.0`; Bundle Version Code `1`, +1 on every upload.
   - **Keystore:** create it in Publishing Settings and store it outside the repo. Add `*.keystore` to `.gitignore`.
   - Icons: 512² store icon plus adaptive Android icons, generated with Unity AI. Splash: a custom logo, or hide the Unity splash if the license allows.
2. **Scene list** (`ProjectSettings/EditorBuildSettings.asset`):
   - Enable only `Phase6_Boot` (index 0) and `RunArena`, plus `Galaxy1_Ch1_Hub` only if D-02 = keep.
   - Disable every `Ch*`, `ChNN_Prologue` and `ParkourGrounds` scene. Leave the stray `1.unity` in place but unlisted.
   - Then grep `MissionLauncher`, `CampaignDirector`, `PrologueSceneNames` and `MainMenuController` for `LoadScene` calls to disabled scenes. Hide the campaign menu entries (CAMPAIGN / CONTINUE) when their scene isn't in the build: check `SceneUtility.GetBuildIndexByScenePath(path) >= 0`.
   - Change the menu copy "begin the campaign in Kessler's ship hub" to the roguelike pitch.
   - Test: `BuildSettingsTests.RoguelikeScenesPresent_AndBootIsIndex0`.
3. **Plugins:** in the import settings for `Assets/Plugins/NuGet/{McpPlugin,McpPlugin.Common,ReflectorNet}.dll`, set them to **Editor only**, excluding Android and Standalone. Verify the MCP packages' asmdefs are Editor-only.
4. **`Assets/link.xml`:** preserve `Ronin7.Core`, `Ronin7.Combat` and `Ronin7.Flow` (the JsonUtility serializable types). Keep the stripping level at **Low**.
5. **Texture compression:** Build Settings > Android > **ASTC**.

### P0-11 Dev tooling (behind `RONIN7_DEV`)
- **New:** `Flow/Roguelike/DevRunTools.cs` (compiled only with `#if RONIN7_DEV`).
  - Right-stick click held for 2 s **kills all enemies in the room**, which drives the G5 smoke run.
  - Left-stick click held for 2 s **grants a random boon**.
  - Wrist HUD prints the frame ms.
- **New:** `Core/Roguelike/RunLog.cs`.
  - `[Serializable] struct RunLogEntry { string utc; uint seed; bool won; int depth; int kills; int boons; int echoes; float seconds; string[] boonIds; string deathBy; string build; }`.
  - `RunLog.Append(entry)` → `File.AppendAllText(Path.Combine(Application.persistentDataPath,"runlog.jsonl"), JsonUtility.ToJson(e)+"\n")`.
  - Called from `RunDirector.EndRun` next to `RunSummary.Capture`. `deathBy` is the last `PlayerHit` source name.
  - This runs in **all** builds; it is local only and the file is tiny.
  - Pull it with `adb pull /sdcard/Android/data/<id>/files/runlog.jsonl`.
  - Test: `RunLogTests.Roundtrip_SingleLine_NoEmbeddedNewline`.

### P0-12 Build script
- **New:** `Scripts/Editor/BuildMenu.cs` (`Ronin7.Editor`).
- `[MenuItem("Tools/Ronin7/Build/Quest (APK)")]` and `[MenuItem("Tools/Ronin7/Build/Windows (PCVR)")]`, plus static `BuildQuest()` and `BuildWindows()` methods for `-executeMethod`.
- Each one:
  - reads the scenes from `EditorBuildSettings.scenes` where enabled;
  - for Quest, sets `EditorUserBuildSettings.selectedBuildTargetGroup = Android` and `buildAppBundle = false`;
  - outputs to `D:\ronin7\Builds\<target>\Ronin7_<version>.<apk|exe>`;
  - writes `BuildReport.summary` to `Builds/<target>/report.txt`;
  - calls `EditorApplication.Exit(1)` in batchmode when the result ≠ Succeeded.
- `Development` and `RONIN7_DEV` are toggled by a `-dev` command-line argument.
- Memory gotchas apply: wait for `!EditorApplication.isCompiling` after switching target, keep minSdk at 32, and never run CleanBuildCache.

### P0 exit
- Run the Export Gate G1–G9.
- **First Quest IL2CPP build of the roguelike branch.** This finally proves `RunRng`'s `Span`/`stackalloc` under IL2CPP.
- Tag `roguelike-phase0`, version `0.1.0`.

---

## PHASE 1 — Combat foundation and balance

**Goal:**
- Correct, extensible damage pipeline.
- No degenerate strategies.
- Readable enemies.
- Fair numbers.

Every later phase plugs into the hooks added here.

### P1-01 Damage context and pre-damage filter (the weakness/stat hook)
1. **`Core/IDamageable.cs`, `DamageInfo`:**
   - Add `public readonly float Speed;` as an optional constructor parameter `float speed = 0f`.
   - Add `public DamageInfo WithAmount(float a)`.
   - Existing `new DamageInfo(...)` calls still compile.
2. **`Combat/Health.cs`:**
   - Add `public Func<DamageInfo,float> DamageFilter { get; set; }`, the same single-slot idiom as `DeathInterceptor`.
   - Add `public float IncomingDamageMultiplier { get; set; } = 1f;`.
   - In `ApplyDamage`:
     ```csharp
     if (!IsAlive) return;
     float amount = (DamageFilter != null ? DamageFilter(info) : info.Amount) * IncomingDamageMultiplier;
     if (!(amount > 0f)) return;              // also rejects NaN; fully resisted = no events
     var applied = amount == info.Amount ? info : info.WithAmount(amount);
     // existing body uses `applied`
     ```
3. **`Combat/BladeDamager.cs:~341`:** pass the blade `speed` into `DamageInfo`.
4. **Overdrive speed inflation (BUG-21):**
   - Compute `SmoothSpeed` from **unscaled** dt (`Time.unscaledDeltaTime`, or `fixedDeltaTime / Time.timeScale`, guarded for 0).
   - Give Overdrive an explicit, documented damage bonus instead: `mods.OverdriveMultiplier = 1.5f` while active, inside the temporary group.
   - Keep the perfect-parry window in real time, using unscaled timing in `MeleeAttacker` for the parry window only.
- **Tests:**
  - `HealthDamageFilterTests`:
    - `Filter_Null_AppliesUnchanged`
    - `Filter_Zero_NoDamagedNoDied`
    - `Filter_NaN_Rejected`
    - `Filter_Scales_EventCarriesFilteredAmount`
    - `Filter_Lethal_StillRespectsDeathInterceptor`
    - `IncomingMultiplier_ScalesDamage`
  - `BladeDamagerTests.Speed_IndependentOfTimeScale`

### P1-02 Data foundation: roguelike-owned enemy definitions
- Create `Data/Roguelike/Enemies/RL_<Name>.asset` for all 14 enemies, cloning the current `Ch*` definitions. Repoint `EnemySpawnTable` via `RoguelikeDataBuilder`. From now on, story assets are never touched.
- **`Enemies/EnemyDefinition.cs`: new fields** with defaults that keep current behaviour:
  ```csharp
  [Header("Timing")] public float telegraphTime = 0.8f, activeTime = 0.8f, recoverTime = 0.6f, staggerTime = 1.2f;
  [Header("Poise")]  public bool superArmor; public float postureGainMultiplier = 1f;
  [Header("Weakness")] public WeaknessKind weakness; public float resistMultiplier = 0.15f, exploitMultiplier = 2f, heavySwingSpeed = 5.5f;
  public MaterialClass materialClass; // Organic, Mech, Spectral, Armored (VFX + audio)
  ```
  Move the hard-coded `MeleeAttacker` timings onto these fields (read in `Configure`).
- **Tests:** `EveryDefinition_TelegraphAtLeast055`; `RL_Definitions_DoNotReferenceStoryAssets`.

### P1-03 `FindPlayer` → `Health.Active`
- In `Enemies/MeleeAttacker.cs:450-455`, replace `FindObjectsByType<Health>()` with a loop over `Health.Active` that picks the entry with a `CharacterController`. Add a `// ponytail:` note: if a second player-like Health appears, tag it instead.
- Test: `FindPlayer_UsesActiveRegistry` (a pure helper over a list).

### P1-04 Super-armor (per-type fix for the lockout, BUG-11)
- In `MeleeAttacker`, add `protected virtual bool FlinchCancelsWindup => flinchCancelsWindup;` and use it in `OnDamagedFlinch`. `Enemy` overrides it with `=> !definition.superArmor && base.FlinchCancelsWindup`.
- Set `superArmor = true` on: Humanoid_Automaton, Iron_Dojo_Warden-Cadre, all bosses, and the Unflinching elite affix (P3).
- Trash keeps the flinch-cancel (your call to defer the global fix stands).
- Test: `FlinchTimingTests.SuperArmor_WindupHit_DoesNotCancel`.

### P1-05 Posture scaling (BUG-18)
- Set `postureMaxFraction` on every `RL_` definition: trash 0.6, heavies 0.5, bosses 0.35 of max HP.
- Add `PostureMeter.Configure(float max, float gainMultiplier)` and a settable `breakBonusDamage`.
- Tests: `PostureMeterTests.Accumulate_WithGainMultiplier`; `Boss_PostureMax_ScalesWithHealth`.

### P1-06 Ability rebalance: Weakpoint Sight and Overdrive (BUG-12)
- **Weakpoint Sight** becomes the precision ability (default, D-04).
  - It now **only multiplies the precision zone**, from 1.25 to 2.0×. That is a head hit (top 20% of the victim's collider bounds) or a thrust (`Dot(bladeVelocity.normalized, bladeAxis) > 0.7`).
  - It shows a gold rim on enemies, which is free once P1-11 adds `_RimColor`.
  - The markers sit on the precision zones.
  - Add `PlayerCombatModifiers.WeakpointSightActive` (read by P3 weaknesses).
  - Remove the flat 2× `WeakpointMultiplier` from `DamageMultiplier`.
  - Implement precision in `BladeDamager.TryHit` via a pure `internal static bool IsPrecisionHit(float hitY, Bounds victimBounds, Vector3 bladeVel, Vector3 bladeAxis)`.
- **Overdrive:** see P1-01 step 4.
- **Tests:** `IsPrecisionHit_*` table; `WeakpointSight_DoesNotChangeBodyHitDamage`.

### P1-07 Unbroken and the revive chain (BUG-13)
- **Unbroken is once per run in the roguelike.** Add `RunState.UnbrokenUsed` and snapshot it as an appended field.
- `UnbrokenWard` checks `RunState.InRun ? !RunState.UnbrokenUsed : !used`.
- Interceptor order: `Unbroken() || SecondWind()`. Set `ReviveFraction` **inside** each branch: Unbroken gives 1 HP (0), Second Wind gives 0.3.
- **Tests:**
  - `DeathInterceptorChain_UnbrokenFiresBeforeSecondWind`
  - `ReviveFraction_PerSource` (Unbroken → 1 HP, Second Wind → 30%)
  - `Unbroken_OncePerRun_SurvivesNodeReload`

### P1-08 Boon picker: tier-first, no dead Epics (BUG-14, BUG-22)
- `BoonOfferPicker.Pick`:
  1. Pick the **tier** with the existing per-node weights.
  2. Pick **uniformly within the tier**.
  3. Fall back to the next tier down if the tier is empty. Keep the A1.6 contract that an offer may return 0–3 boons.
  4. Filter out `GrantAbility` boons whose ability `RunState.HasGrantedAbility` already has.
  5. **Roguelike abilities come only from `RunState`.** `AbilityAccess.Has` returns `RunState.InRun ? RunState.HasGrantedAbility(id) : CampaignState.HasAbility(id)`. Default D-05.
- **Tests:**
  - `TierFirst_TierOddsIndependentOfPoolSize` (statistical over 10k seeded draws, ±2%)
  - `OwnedAbility_NeverOffered`
  - `AbilityAccess_InRun_IgnoresCampaign`

### P1-09 Elite rooms that are actually elite (BUG-09)
- `WaveComposer.Compose`, Elite branch:
  - **1 elite**: an eliteOnly pick when one is eligible, otherwise the strongest eligible trash.
  - **plus escorts**: 2 trash in sectors 0–1, 3 in sector 2.
  - Add `bool IsElite` to `SpawnRequest`.
- Make Caradoc (Warden-Cadre) and the Automaton **eliteOnly**, lowering minDepth to 2 so sector-0 elites exist.
- **Tests:** `Elite_FirstRequestIsElite`; `EliteThreat_GreaterOrEqualCombatThreat_SameDepth` (sum of maxHealth × multiplier).

### P1-10 Boss target bands (BUG-10)
- Add `RunScaling.BossTargetHealth(sector)` = {450, 650, 900} and `BossDamageMultiplier(sector)` = {0.9, 1.1, 1.3}. `RunArenaController.BuildScaledDefinition` uses them for Boss nodes, replacing the depth multipliers.
- Maelgorn's 3 phases arrive in P3-07. Until then its 900 sits on P1.
- **Test:** `BossTotalHealth_IsMonotonicBySector`.

### P1-11 Enemy readability shader: `Ronin7/EnemyPBR` (BUG-17)
This comes early because weakness tells (P3) depend on it.
1. **Shader:** new `Art/Shaders/EnemyPBR.shader`.
   - Copy the `GrittyCyber` skeleton.
   - Maps: base, normal, metallic-roughness.
   - Lighting: SH and probe lighting; `multi_compile_fog`; `multi_compile_instancing`; single-pass instanced; `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`.
   - Properties: `_RimColor` (HDR), `_RimPower`, `_FlashAmount` (lerps to white plus emission).
   - CBUFFER `UnityPerMaterial`.
2. **`Core/RendererTint.cs`:**
   - `Apply` also writes `_RimColor`, and `baseColorFactor` as a stop-gap.
   - Add `Flash(Renderer r, float amount)`, which writes `_FlashAmount` on the same shared MPB.
3. **`MeleeAttacker.Tint()`:**
   - Drives `_RimColor`: faction idle colour → orange (1, 0.4, 0.25) on windup → blue on stagger.
   - On `EntityDamaged`, flash 1 → 0 over 0.08 s.
4. **`EnemyPrefabBaker`:** swap `Visual_Skinned.sharedMaterial` to `<Name>_EnemyPBR.mat`. Texture extraction is in P1-12.
- **Test:** `RendererTintTests.Apply_WritesRimAndBaseColorFactor`.

### P1-12 Compress enemy textures (BUG-23)
- In `EnemyPrefabBaker`, add `ExtractTextures`:
  1. For each glTFast sub-asset texture, `EncodeToPNG` → `Art/Generated/Characters3D/<Group>/<Name>_Tex/{Albedo,MR,Normal}.png`.
  2. Set the TextureImporter: Normal = NormalMap; MR = sRGB off; max 1024; Android **ASTC 6×6** (normals ASTC 5×5); mipmaps on.
  3. Assign these maps to the `EnemyPBR` material.
- Re-bake all 14 enemies.
- **Accept:** the Memory Profiler shows ≤ 4 MB per enemy type.

### P1-13 Boon panel lazy follow (BUG-16) and HUD with HP
1. **`BoonOfferPanel`:**
   - Flatten forward: `f = ProjectOnPlane(cam.forward, up).normalized`.
   - Recenter only when `Angle(f, toPanelFlat) > 35°`. Lerp position **and** `LookRotation(panel − head)` over 0.25 s. Keep the panel at head height − 0.1 m.
   - Apply the same to `RunSummaryPanel`.
   - Pure helpers: `ShouldRecenter(...)`, `FollowPose(...)`.
   - Update the test `Reposition_MovesPositionOnly_NeverTouchesRotation`, since its intent changes.
2. **Boon panel content:**
   - Description ≥ 22 px.
   - Rarity colour: Common grey #B8C0CC, Rare blue #3FA9FF, Epic violet #B66BFF. Colour-blind-safe: also prefix the tier word.
   - Show "OWNED n/max" and "REROLL (n)", plus a countdown label for the last 10 s of the fallback.
3. **`RunHudPanel.Format`** becomes multi-line, with no line over 18 characters:
   ```
   HP 72/110
   NODE 6/15 · S2
   FOES 3 LEFT
   BOONS 4 · ◈ 85
   ```
   - HP comes from the player `Health` event, or a 0.25 s `InvokeRepeating` on `VRRig.Instance`.
   - FOES comes from a new `RoomProgress(int remaining)` event published by `RunArenaController` on activate and death.
   - It also refreshes on `RunNodeEntered`.
- **Tests:** `ShouldRecenter_20_40_180`; `FollowPose_LookingDown_KeepsHeadHeight`; `RunHudPanelFormatTests.FourLines_MaxWidth18`.

### P1-14 Visible enemy blade edge (parry target)
- `EnemyPrefabBaker` adds one 2-point `LineRenderer` (shared unlit additive `.mat`, width 0.03) from `Rig_ArmR`/`weapon` to `BladeTip`.
- `MeleeAttacker` enables it only during Windup and Active, coloured by `Tint`.
- Telegraph from outside the field of view: if the attacker is outside the player's ±50° yaw cone at `BeginAttack`, extend `telegraphTime` by +0.25 s. This uses pure `TelegraphFor(baseTime, angleDeg)`.
  - This is **not** an attack-slot cap. You rejected caps; this only lengthens the warning.
- **Test:** `TelegraphFor_OutsideCone_AddsGrace`.

### P1-15 Enemy attack animation (art enemies; follow-up to BUG-17)
Today the Tripo/glTF enemies show **no** attack animation. The windup is carried only by the P1-11 rim tint, the directional haptic tick (`CombatFeedbackController` on `AttackWindupStarted`) and the windup audio cue (`AudioDirector`). This item adds the body language.
1. **State sync:** windup, active and recover poses/clips driven from `MeleeAttacker` states (read the state; do not add timers).
   - Windup must have a readable silhouette: weapon and arm raise and draw back, so the swing direction is telegraphed before the strike.
   - Active is the strike; recover is the return to guard.
2. **Approach decision (do first, record in the PR):** procedural pose vs authored clips.
   - Tripo meshes use the procedural 6-bone NPC rig (`NpcWalkAnimator`, see `Rig NPC Characters For Walk`). Default is a procedural pose layer on those bones (arm/weapon bones, torso lean); authored clips only if the 6-bone rig cannot sell the windup.
   - Any new per-type pose data goes in the `EnemyDefinition`/baker, not in scene objects.
3. **Timing contract:** telegraph is 0.8 s (0.85 s for `Ch12SentinelDuelist` and `TrainingDummy`); the parry window opens at 0.75 × telegraph. The windup pose must reach full draw-back **before** the parry window opens, so the animation leads it. Scale pose speed from the actual `telegraphTime` (incl. the P1-14 +0.25 s grace), never a fixed clip length.
4. **Constraints:** no camera shake (animate the enemy, never the camera); Quest 90 FPS (11.11 ms), so no per-frame allocations, no new `FindObjectsByType`, and a small bone count only; MPB tint writes stay on `RendererTint`.
- **Dependencies:** P1-11 (rim tint is the colour half of the telegraph), P1-14 (blade edge, extended telegraph), P1-04 (windup stays uninterruptible only where super-armor says so; a flinch must cleanly cancel the pose).
- **Test (EditMode, pure logic):** `WindupPoseProgress_ReachesFullBeforeParryWindow`: for telegraph 0.8 and 0.85, pose progress is 1.0 at or before `0.75 × telegraph`. Rig and visual checks are not unit-testable.
- **Accept:** in-headset readability check on at least 3 enemy types (one humanoid, one drone, one boss): an unfamiliar tester can name the attack direction from the windup alone with the rim tint and audio muted. Profiler shows no added frame cost over the 11.11 ms budget on Quest.
- **Export Gate:** run it before closing the item; confirm no Quest regression and that the tag/versions below still hold.

### P1 exit
- Export Gate.
- Write the new numbers into `Roguelike-Design.md` as Amendment 8: Unbroken per-run, Weakpoint Sight as precision, tier-first picker, boss bands, abilities come only from `RunState`.
- Tag `roguelike-phase1`, version `0.2.0`.
- **Phase 1 sign-off (2026-10-14):** user signed off after Quest headset runs. **G5 PCVR/Link half deferred by user choice**; it moves to the Phase 2 Export Gate and is run on the Phase 2 build directly. Headset notes slotted: BUG-29 -> P2-10 (damage-receive latency), P2-11 (enemy movement smoothness).

---

## PHASE 2 — Run flow and the meta loop

**Goal:** a complete roguelike loop:
- spend Echoes between runs;
- choose a path;
- readable rooms, waves and transitions;
- a first-run tutorial;
- a mid-run checkpoint.

### P2-01 Echo shop: the "Shrine of Echoes" (BUG-15)
1. **`MetaProgression`:**
   - `public static int UpgradeCost(string id, int nextLevel)` with the price table below.
   - `public static bool TryPurchase(string id)`:
     ```
     level >= MaxLevel(id) → false
     !TrySpend(cost) → false
     SetUpgradeLevel(id, level+1); Save(); return true
     ```

   | Upgrade | Levels | Costs |
   |---|---|---|
   | Starting Health | 5 | 40 / 60 / 80 / 100 / 120 |
   | Starting Damage | 5 | 40 / 60 / 80 / 100 / 120 |
   | Starting Boon | 3 | 80 / 150 / 250 |
   | Reroll Tokens | 3 | 50 / 100 / 150 |

   Income is about 320 Echoes per win and 60–150 per loss. Power upgrades total about 1,580 Echoes, roughly 5 wins or 15 mixed runs. Variety unlocks come in P4-10.
2. **UI:**
   - Add a **SHRINE** button on the boot menu via `RoguelikeEntryPointPatcher`, additive.
   - It opens `MetaShopPanelFactory` (same idiom): 4 rows with name, level pips, cost and BUY. BUY is non-interactable when unaffordable.
   - Show "◈ ECHOES n" on the menu.
3. **Feedback:** a purchase triggers a haptic pulse (0.6, 0.1) and the `boonChosen` audio cue.
- **Tests:**
  - `TryPurchase_Insufficient_NoChange`
  - `TryPurchase_AtMax_False`
  - `TryPurchase_Success_DeductsLevelsPersists` (Save → Reset → Load round trip with `DirectoryOverride`)
  - `UpgradeCost_Table`

### P2-02 Reveal Forge and Treasure, sector cards, death and victory beats
1. **Forge** is no longer `skipReveal`. Load a small room and show a 2-card panel:
   - **MEND**: heal 60% of missing.
   - **TEMPER**: P4-07; until then, +1 reroll.
   - The P0-02 deferral already orders this correctly.
2. **Treasure** shows a single "TAKE" card with the boon (rarity-coloured burst VFX).
3. **Sector card:** after the sector-0/1 boss boon, show a 2.5 s world title "SECTOR 2 — THE PROGRAM" with the biome colour, heal 20% of missing, and offer **CONTINUE / SAVE & QUIT** (`SuspendRun`). This is the session-comfort checkpoint.
4. **Death beat:**
   - `EndRun(false)` latches `EndingRun` first (per A7.1).
   - It then waits 1.2 s unscaled with a desaturating vignette, a heartbeat haptic, and the `runLost` audio cue. **No shake.**
   - Implement as `StartCoroutine(DelayThen(1.2f, () => StartSceneLoad(runEndScene)))`.
5. **Victory beat:** 2 s "FRACTURE CLOSED" title plus the `runWon` cue.
- **Tests:** `RunDirectorTests.EndRun_LatchesBeforeDelay` (no double EndRun); `SectorHeal_20PercentOfMissing`.

### P2-03 Summary correctness (BUG-26)
- `RunSummary.RoomsClearedFor(depth, won)` returns `won ? 15 : depth`.
- `MetaProgression.NoteRunEnded(won ? TotalNodes : depth, won)`; display BEST as `n/15`.
- Update `RunSummaryTests` and `MetaProgressionTests`.

### P2-04 Branching map (replaces the linear map)
1. **Data, in `Core/Roguelike/`:**
   - Append `Shop` and `Event` to `RoomKind`.
   - `RunNode` gets `Lane`.
   - New `RunMap { RunNode[] Nodes; int[][] Next; int StartId; int Depth(int id); }`.
2. **Generator:** `RunMapGenerator.GenerateGraph(uint seed)`. Per sector, 5 rows:

   | row | lanes | kinds |
   |---|---|---|
   | 0 | 1 | Combat |
   | 1 | 2–3 | Combat 40 / Elite 25 / Event 15 / Treasure 10 / Shop 10 |
   | 2 | 2–3 | same; ≤1 Treasure and ≤1 Shop per sector |
   | 3 | 2 | {Forge, Shop} |
   | 4 | 1 | Boss |

   - Edges run from lane l to lanes l−1..l+1 of the next row.
   - Repair orphans by connecting to the nearest lane.
   - Depth = sector × 5 + row, so `RunScaling` is unchanged.
3. **`RunState`:**
   - `NodeIndex` becomes the node **id**; add `Depth`.
   - `Advance(int nextId)` validates the edge.
   - Add `List<int> Path`.
4. **`RunSnapshot`:** bump to v2 and add `path`. The P0-09 banking migration handles v1 runs. Update the G7 fixture.
5. **Path choice, two UIs:**
   - **Diegetic exit gates (default):**
     - `ArenaGeometryBuilder.BuildExitGates(parent, choices)` raises 1–3 arches on the north wall after the boon is picked.
     - Each arch has a big `TextMeshPro` label (kind plus a one-line hint, e.g. "ELITE — rarer boons") and a kind colour.
     - Walking into the arch's trigger box picks it.
   - **Panel fallback:** "CHOOSE YOUR PATH" with 2–3 big buttons, the same lazy-follow as P1-13. Used when `GameSettings.Seated` is on or the player hasn't moved for 6 s.
   - The wrist HUD gains a `NEXT:` line.
6. **Shop node:**
   - Spend **this run's unbanked Echoes** (bank-or-spend tension).
   - Offers: 3 boons priced by rarity (30 / 60 / 120) plus "MEND 25%" for 40.
   - Reuse `BoonOfferPanelFactory` with a price sub-label.
7. **Event node:** pure-data events with a 2-option panel. Each lives in `Data/Roguelike/Events/*.asset`: `RunEventDefinition { title, body, optionA{label, effects[]}, optionB{...} }`. Effects reuse boon effect kinds plus HP delta.
   - **Blood Pact:** −15 max HP → gain a Rare boon.
   - **Gamble:** 50% Epic / 50% take 25 damage.
   - **Meditate:** heal 30%.
   - **Forgotten Shrine:** spend 40 run-Echoes → +1 reroll and heal 15%.
- **Tests (`RunMapGraphTests`):**
  - golden kinds and edges for seeds 1 and 2026
  - BFS reaches every node
  - every non-final node has an out-edge, and the final node has none
  - exactly one Boss per sector, at row 4
  - row 3 contains Forge or Shop
  - per-sector caps on Treasure and Shop
  - `Advance(invalid)` returns false and does not move
  - snapshot v2 round-trip
  - v1 discard banks Echoes

### P2-05 Arena sizing by node kind (comfort)
- `RunScaling.ArenaHalfExtent(RoomKind)`: Combat 8, Elite 9, Boss 11, Forge/Treasure/Shop/Event 5 metres.
- `RunArenaController` uses it; the serialized field becomes a debug override when > 0.
- **Tests:** values per kind; `WaveComposer.Positions` stays in bounds at 5 and 8 m (extend the A5.8 test).

### P2-06 Front-arc spawning, telegraphed and deferred
- On `ActivateFront`, re-place each enemy with `WaveComposer.FrontArcPosition(headLocalXZ, headYawFwd, 4f, 7f, 70f, halfExtent, ref rng)`.
  - Use the substream `RunRng.ForNode(seed, nodeId*31+k)` so it stays deterministic.
  - Fall back to the pre-rolled point if no arc point fits.
- Activation is **queued**, not done inside the death callback:
  1. At `t + 1.2 s`, spawn a pooled emissive floor disc plus a spatial audio one-shot.
  2. Then `SetActive(true)`.
  - Pure helper: `ReleaseSchedule(now, deathTime, delay)`.
- **Tests:** `FrontArc_WithinCone_WithinRadius_InBounds_Y0`; `FrontArc_Deterministic`; `ReleaseSchedule_*`.

### P2-07 Explicit waves
- `WaveComposer.SplitWaves(N, cap 3)`. The next wave triggers when alive ≤ 1. The concurrency cap of 4 still holds by construction.
- **Tests:** sum == N, each wave ≤ 3, deterministic; the A5.8 gate still passes.

### P2-08 Onboarding (first run)
- Append `bool tutorialDone` to `MetaSaveData`.
- When it is false at node 0:
  - Compose 2 Scavengers.
  - Show a north-wall TMP sequence driven by events: "DRAW YOUR BLADE (grip at hip)" → `Grabbed`; "SWING TO CUT" → `SwordImpact`; "MEET THEIR STRIKE TO PARRY" → `SwordDeflected`; "POINT AND PULL TRIGGER TO CHOOSE" → `BoonChosen`.
- Set it to true on the first `RoomCleared`.
- **Tests:** `MetaSaveData_MissingTutorialField_DefaultsFalse`; `EnemyCount_FirstRun_Node0_IsTwo`.

### P2-09 Seated mode, handedness, teleport (comfort settings needed by the flow above)
Add to `GameSettings` and `SettingsPanel`:
- **Seated:** height offset via `XREyeHeightCalibrator.ApplyOffset(+0.45 m)`; relax the `ContinuousLocomotion` posture guard; gates fall back to the panel.
- **LeftHanded:**
  - swap the HUD mount to the right controller;
  - mirror the ray interactor and the katana holster side (`KatanaHolster`/`VRRig`);
  - `ParryFlowController.cs:24`'s hard-coded LeftHand reads the setting.
- **Locomotion: Smooth / Teleport.** Reuse the XRI teleport provider on the left stick, and keep snap-turn on the right. The arenas are small (P2-05), so teleport is cheap to support.
- **Tests:** `GameSettings_Roundtrip_NewFields`; `HudMountFor(LeftHanded)`.

### P2-10 Player damage-receive latency (BUG-29)
User report (Phase 1 headset runs): "a bit of delay in how the player receives damage". Investigate before changing anything; measure hit-lands to feedback in frames.
1. Check enemy hit timing vs the Active window: is damage applied at Active start, or only on a later overlap/physics tick (`MeleeAttacker`, `FixedUpdate` vs `Update`)?
2. Trace `Health.Damage` -> `Damaged` -> `CombatFeedbackController` / `Haptics` / `AudioDirector` for frame-delayed events (coroutines, queued events, pooled VFX warm-up, audio start latency).
3. Check the P1-01 pre-damage filter / `TimeScaleArbiter` hit-stop does not defer the player's feedback.
4. Fix only the confirmed cause; no camera shake, no per-frame allocs.
- **Test:** `PlayerHit_FeedbackFiresSameFrameAsDamage` (EditMode/PlayMode, event ordering).
- **Accept:** headset: hit, haptic and audio feel simultaneous with the blade connecting.

### P2-11 Enemy movement smoothness (visual polish)
User report: movement is functionally fine but visually ugly; needs to be much smoother.
1. Audit the current path: steering/turning (snap vs rate-limited), accel/decel, root motion vs procedural pose (`NpcWalkAnimator` 6-bone rig), foot sliding, sudden snaps on state change (windup/flinch/recover).
2. Pure-logic `MovementSmoothing` (internal): rate-limited yaw turn (deg/s), speed ramp with accel/decel limits, and walk-cycle phase driven by actual travelled distance to stop foot sliding.
3. Blend pose layers on state changes (walk -> windup -> recover) with a short ease instead of hard cuts; a flinch cancels cleanly (P1-04).
4. Constraints: Quest 90 FPS (11.11 ms), no per-frame allocations, no new `FindObjectsByType`, small bone count, no camera shake.
- **Tests:** `Smoothing_TurnRate_NeverExceedsLimit`; `Smoothing_SpeedRamp_NoStepChange`.
- **Accept:** headset: 3 enemy types (humanoid, drone, boss) move without visible snapping or foot sliding; Profiler shows no added frame cost or GC allocs on Quest.

### P2 exit
- Export Gate. G7 must cover the v1 → v2 snapshot migration.
- **G5 PCVR/Link smoke run (deferred from Phase 1):** run the full G5 on PCVR via Link on the Phase 2 build, in addition to the Quest run.
- Update `Roguelike-Design.md` §1/§4: runs return to `Phase6_Boot`, and the map is branching.
- Delete `RunLauncher` and the hub patch (default D-02). Remove its EditMode tests at the same time.
- Tag `roguelike-phase2`, version `0.3.0`.

---

## PHASE 3 — Enemy diversity and unique weaknesses

**Goal:**
- 11 distinct trash/elite archetypes and 3 multi-phase bosses on the **existing 14 Tripo prefabs**.
- Each has a unique, readable weakness, and every weakness also has a damage path that needs no ability.
- Elite affixes.

### P3-00 Phase 2 playtest carry-overs (do first)
Source: Phase 2 headset playtest, 2026-10-14. Items 1-2 are must-fix before any new Phase 3 work.
1. **Forge no-effect (BUG-30):** reproduce on device/PCVR and measure before changing anything. Check MEND amount at full vs missing HP, whether `Health.Current` actually changes, and whether TEMPER's +1 reroll is applied. Fix the confirmed cause, and make both effects visible (the healed amount, e.g. "+38 HP" or "HP FULL", and the reroll count on the HUD/panel).
2. **Gate `SceneDebugOverlay` behind `RONIN7_DEV` (BUG-31).** Test: `SceneDebugOverlay_AbsentInRelease` (define check).
3. **Dev tutorial reset (BUG-32), `RONIN7_DEV` only:** a dev action that sets `runsCompleted = 0` in memory so the P2-08 prompts can be verified in the headset.
4. **Handedness haptics (BUG-34):** route `CombatFeedbackController`, `AdrenalineFlow`, `BladeClashFeedback`, `HeartbeatHaptics` through the same handedness lookup as `ParryFlowController`. Test: `HapticHand_FollowsLeftHanded`. Resolved P3-00: not reproducible — all four pulse both hands (side-weighted for hits), handedness-neutral; no code change. Guard test `HapticHand_FollowsLeftHanded` covers the shared `HandednessRig.OffHand` lookup.
5. **Build output folders (BUG-36):** separate `Builds/Windows` (release) from `Builds/Windows-Dev`.
6. **Tracked Pose Driver warning (BUG-37):** investigate; add the Input System `TrackedPoseDriver` to the RunArena camera rig, or silence it with a note if it is harmless under OpenXR. Resolved P3-00: harmless, documented, no code change. The warning is a hardcoded type check in `XROrigin.Awake` (com.unity.xr.core-utils) for `UnityEngine.InputSystem.XR.TrackedPoseDriver`. Our rig tracks the camera with `Player/PoseFollower` (Input System Head actions, applied in Update + onBeforeRender, see `XRRigBuilder`); adding a TrackedPoseDriver would double-drive the head pose, so it is deliberately not added.
7. **Tuning knob to revisit (P2-10):** the parry window was shortened from ~1.0 s to ~0.48 s (`StrikeFraction` 0.35); the user said damage feels better. Re-check with the P3-02/P3-05 weakness and parry work; do not change it blind.
- **Accept:** Forge effect visible and confirmed on device; release build shows no debug label; EditMode green.

### P3-01 The weakness system (pure core)
- **New:** `Enemies/WeaknessRules.cs` (`Ronin7.Enemies`, pure static).
  ```csharp
  public enum WeaknessKind { None, Backstab, HeavyBreak, Recovery, Alternating, Reflect,
                             Linchpin, ParryGate, CounterHit, PostureOnly, Blink }   // append-only
  public enum WeaknessResult { Neutral, Resisted, Exploited }
  public readonly struct WeaknessContext {
      public readonly Vector3 EnemyPos, EnemyForward, SourcePos, HitPoint;
      public readonly float Speed; public readonly DamageType Type;
      public readonly bool SourceIsPlayer, SourceIsAlly, SightActive;
      public readonly bool Staggered, Attacking, Recovering, RecentlyBlinked;
      public readonly int HitSide, LastHitSide; public readonly float SinceLastHit; }
  public static class WeaknessRules {
      public static WeaknessResult Evaluate(WeaknessKind k, in WeaknessContext c, float heavySwingSpeed);
      public static float Multiplier(WeaknessResult r, float resist, float exploit);
      internal static bool IsBehind(Vector3 pos, Vector3 fwd, Vector3 src, float minAngleDeg = 110f);
      internal static int Side(Vector3 pos, Vector3 fwd, Vector3 hit); // reuse EchoHunter side math
  }
  ```
- **Rule table** (implement exactly this):

| Kind | Resisted when | Exploited when | Neutral otherwise |
|---|---|---|---|
| Backstab | from the front (< 110° off forward) and not staggered | from behind, or while staggered | — |
| HeavyBreak | front hit with `Speed < heavySwingSpeed` | front hit with `Speed ≥ heavySwingSpeed` (also force-staggers) | hit from behind |
| Recovery | during the charge (Attacking) | Recovering or Staggered | Chase/Idle |
| Alternating | same side as the last hit, within 4 s | opposite side within 4 s | after 4 s |
| Reflect | — | `Type == Projectile && SourceIsPlayer` (deflected bolt) | blade hits |
| Linchpin | — | — | always neutral; its death staggers buffed allies |
| ParryGate | not staggered | staggered | — |
| CounterHit | Idle/Chase (×0) | Attacking or Staggered, **or** `SightActive`, **or** `SourceIsAlly` | — |
| PostureOnly | always for HP (×0.2), but posture gain ×5 | — | — |
| Blink | — | within 1 s after a blink | otherwise |

### P3-02 Wiring into `Enemy`
1. In `Enemy.Awake`: `if (definition.weakness != WeaknessKind.None) health.DamageFilter = FilterDamage;`. This allocates one delegate per spawn and nothing per hit.
2. `FilterDamage(DamageInfo info)`:
   - Build the context from `state`, `transform`, `info`, `lastHitSide` and `lastHitTime`.
   - `SightActive` comes from `info.Source.TryGetComponent(out PlayerCombatModifiers m) && m.WeakpointSightActive`.
   - `SourceIsAlly` comes from `info.Source.TryGetComponent<AllyCombatant>(out _)`.
   - Evaluate the rule. Publish `HitResisted` or `WeaknessExploited`.
   - Set a `resistedThisHit` latch: `// ponytail: per-hit latch; move to a DamageInfo flag if a 2nd consumer appears`.
   - Return `Amount × Multiplier`. For `Reflect`, return `health.Max * exploitMultiplier`.
3. `Enemy.FlinchEnabled => !definition.superArmor && !ConsumeResisted();`. Resisted hits do not flinch and add no posture.
4. A HeavyBreak or ParryGate exploit calls the existing `ForceStagger()`.
   - Deviation: exploit force-staggers only if not already staggered - prevents stunlock. Melee backstab direction uses the hit point (not the player rig root).
5. **Events** (`Combat/CombatEvents.cs`, append): `HitResisted { Point, Victim }`, `WeaknessExploited { Point, Victim, Kind }`, `BossPhaseChanged { Boss, Phase }`, `EnemySpawning { Position }`, `RoomProgress { Remaining }` (if not added in P1).
- **Tests (`WeaknessRulesTests`):**
  - one test per table row, at both boundaries
  - `IsBehind_Boundary110`
  - `Side_Symmetry`
  - **`EveryWeaknessKind_HasAbilityFreeDamagePath`**: with `SightActive = false` and `SourceIsAlly = false`, some context must give a multiplier ≥ 1. This is the soft-lock gate.
  - `FilterDamage_Resisted_NoFlinchNoPosture` (EditMode with a GameObject; `Enemy` is testable through `InternalsVisibleTo`)

### P3-03 Weakness feedback (VR-safe)

| Event | Haptics (`CombatFeedbackController`) | Audio (`AudioDirector`; new slots `weaknessHit`, `hitResisted`) | VFX (`CombatVfxController`) |
|---|---|---|---|
| `HitResisted` | `PulseBoth(0.15, 0.03)`, a dull thud | low metallic clank, spatial | `DeflectFlash` at ¼ burst, blue-grey |
| `WeaknessExploited` | `PulseBoth(0.8, 0.12)` | bright "shing" plus a rising tone | gold `SwordSpark` max burst plus `RendererTint.Flash(1)` |

- Generate the clips with `ProceduralAudioSynth`.
- **Discoverability is in the world**: shield arc glow, emissive cracks, ghost opacity, floor glyph. No floating text.
- Tests: extend `CombatFeedbackControllerTests` and `CombatVfxTests` so each new event's `HandledCount` is incremented.

### P3-04 The archetype roster (data and behaviour)

All of these are `RL_` definitions plus at most one small `Enemy` subclass each. **No new state machine**: subclasses override `OnChaseTick`, `OnBeginAttack` and `OnActiveEnd`.

| # | Prefab | Archetype | Behaviour | Telegraph | Weakness | Key numbers |
|---|---|---|---|---|---|---|
| 1 | Ash-World_Scavenger | **Swarmer** | fast jab; the teaching enemy | 0.6 s, shrill cue | None (flinch on) | 30 HP, 2.0 m/s, active 0.35, cd 0.6, weight ↑ |
| 2 | Coil_Syndicate_Ganger | **Charger** (`ChargerEnemy`) | at 3–5 m: crouch, then a 4 m straight lunge, clamped to `dist + 1 m`; overshoots | 1.0 s + a ground streak `LineRenderer` | Recovery ×2.5 / ×0.5 while charging | 70 HP |
| 3 | Dominion_Trooper | **Shield trooper** | slow advance, 120° frontal guard (an emissive arc quad) | standard | HeavyBreak (5.5 m/s) ×0.15 / ×1.5 + guard break | 80 HP |
| 4 | Program_Operative_Grunt | **Pattern duelist** | reads your side | standard | Alternating ×0.2 / ×1.5 | 70 HP |
| 5 | Hunter_Drone | **Ranged gunner, flying** (`RangedEnemy`, P3-05) | hovers 1.6–2.0 m, kites to 5–7 m, slow bolt (4 m/s, r 0.12) | 0.8 s charge glow + rising tone | Reflect: a deflected bolt kills | 35 HP |
| 6 | Dominion_Scan-Drone | **Support** (`SupportDrone`) | hovers at the back; a beam to one ally gives it ×0.5 damage taken; flees at 4 m | visible beam `LineRenderer` | Linchpin: its death staggers the buffed ally | 25 HP |
| 7 | Humanoid_Automaton | **Armored heavy** (elite) | super-armor, big overhead | 1.2 s, deep cue | Backstab ×0.3 / ×2.5 | 160 HP, 18 dmg |
| 8 | Iron_Dojo_Warden-Cadre | **Parry master** (elite) | 2-hit chain from alternating sides | standard | ParryGate ×0.1 / ×2, stagger 2.0 s | 120 HP |
| 9 | Spectral_Grave-Guardian | **Phaser** (elite) | translucent drift; opaque during its own attack | it solidifies | CounterHit (Sight and Mirror always land) | 120 HP |
| 10 | Redaction_Construct | **Construct** (elite) | slow sweeping chop; emissive cracks grow on `PostureNearBreak` | cracks | PostureOnly; break bonus = 25% of max HP | 180 HP |
| 11 | Dream_Ghost_Manifestation | **Teleporter** (`BlinkEnemy`, elite) | blinks to a flank point 3 m away; cue plays 0.5 s before, *from the destination*, plus a floor glyph | glyph + positional audio | Blink ×1.75 within 1 s | 90 HP |
| B0 | The-Warden | **Gatekeeper** | P1 armored + super-armor; P2 at 50% calls 2 Scan-Drones | 1.0 s | Backstab, then Backstab + Linchpin adds | 450 |
| B1 | Vane_Wraith-6 | **Duelist** | P1 Alternating + feints (telegraph randomized 0.6–1.0 s, node RNG); P2 blinks behind the player | variable | Alternating → Blink | 650 |
| B2 | Maelgorn | **Final, 3 phases** | P1 HeavyBreak; P2 CounterHit + 2 Scavengers; P3 ParryGate 2-hit chain, super-armor | per phase | per phase | 300 / 300 / 300 |

Implementation notes:
- **Charger:**
  - Uses `Enemy.Displace(Vector3 delta, float seconds)` (new, in Enemies), which respects the `groundY` latch.
  - Clamp the lunge to player distance + 1 m and to `halfExtent − 0.5`. Pass the extent via `Enemy.Configure(def, arenaHalfExtent)`, because Enemies cannot see World.
- **Blink:** sets the transform with no fade. An enemy teleporting is comfort-safe; it is not camera motion. A pure `BlinkTarget(playerPos, playerFwd, side, 3f, halfExtent)` is tested for bounds.
- **Support beam:** keeps its single target reference, with no per-frame search. It re-targets on target death through `Health.Active`, at most once per second.
- **Hover drones:**
  - The baker offsets the `Visual_Skinned` and `Rig_Root` children and the capsule centre by `hoverHeight`.
  - The **root stays at y=0**, because the spawn math and A5.4 assume that.
  - Drones skip the 1.8 m normalisation and target about 0.8 m instead.
- **Spawn table:**
  - Update `RoguelikeDataBuilder` with the new weights: Swarmer 18, Charger 12, Trooper 12, Duelist 10, Gunner 10 (minDepth 2), Support 6 (minDepth 4, max 1 per room); the elites are eliteOnly.
  - Add the invariant `SupportDrone_AtMostOnePerRoom`.
- **Design review flag:** Construct `PostureOnly` net posture gain is x1.0 (the x5 posture multiplier is applied to the x0.2 HP damage). Unchanged; flagged for design review.

### P3-05 Ranged enemies and reflectable bolts
1. Add `"Ronin7.Enemies"` to `Scripts/Ship/Ronin7.Ship.asmdef`. There is no cycle: Enemies → Core and Combat only. **Ship still must not reference Player.**
2. **`Ship/RangedEnemy.cs : Enemy`:**
   - `IsInLandHitRange() => false`
   - `TryDetectParry() {}`
   - `OnChaseTick`: kite to 5–7 m
   - `OnActiveEnd`: `pool.Fire(gameObject, muzzle, LookRotation(head − muzzle), 4f, AttackDamage, 4f, 0.12f, color, null)`
   - The pool is found once in `Awake` with `FindAnyObjectByType<ProjectilePool>()` (ponytail note).
3. **`RoguelikeArenaBuilder`** bakes one `ProjectilePool` (capacity 8) into RunArena.
4. **`Ship/Projectile.cs` `TryHit`:**
   - Before the `IDamageable` lookup: if `other.GetComponentInParent<BladeDamager>()` has `Speed ≥ 2.5` and the bolt is not already reflected → `Reflect(playerRoot)`.
   - `Reflect` swaps the owner to the player, aims at the original firer, doubles the speed and sets `reflected = true`.
   - Pure helper: `ReflectDirection(boltPos, targetPos)`.
5. **Player feedback:** when the bolt hits a `CharacterController`, publish `PlayerHit(damage, point, ownerPos)`. That provides directional haptics and the vignette.
- **Tests:** `ReflectDirection_PointsAtTarget`, `ReflectDirection_ZeroDistance_Reverse`, `Reflect_PlayerProjectile_Exploits`.

### P3-06 Elite affixes
- **New:** `World/Roguelike/EliteAffixes.cs` (pure).
  - `enum EliteAffix { Armored, Swift, Unflinching, Vampiric, Volatile }` (append-only).
  - `Pick(int sector, ref RunRng rng)` uses a **separate stream**, `RunRng.ForNode(seed, nodeId ^ 0x0E117E)`, so the golden vectors don't move.
  - `Apply(EnemyDefinition clone, EliteAffix a)`.

| Affix | Effect | Tell (rim colour via MPB) |
|---|---|---|
| Armored | weakness None → Backstab; otherwise resist ×0.5 | steel blue |
| Swift | moveSpeed ×1.35, cooldown ×0.7; **telegraph unchanged** | yellow |
| Unflinching | superArmor, postureMaxFraction ×1.5 | white |
| Vampiric | heals 50% of landed damage (one line in `LandHit` behind `definition.vampiric`) | red |
| Volatile (bomber) | on death: a 1.2 s growing floor ring (pooled quad), then a 2 m blast via a `Health.Active` distance check | orange pulse |

- Count: 1 affix in sectors 0–1, 2 in sector 2. Never Swift together with Volatile.
- Applied in `RunArenaController.BuildScaledDefinition` when `request.IsElite`.
- **Tests:** `Pick_Deterministic` (golden), `Pick_Sector0_One`, `Apply_Swift_TelegraphUnchanged`, `Pick_NeverSwiftPlusVolatile`, `Volatile_BlastRadius` (pure).

### P3-07 Boss phases and adds
- `EnemySpawnTable.BossEntry` gains `EnemyDefinition[] phases`. Each phase is cloned and scaled to its share of `BossTargetHealth`.
- `Enemy.ConfigurePhases(EnemyDefinition[] next)`: sets `health.DeathInterceptor = AdvancePhase; health.ReviveFraction = 1f`.
- `AdvancePhase()`:
  - returns false when no phases remain;
  - otherwise: `definition = next[i++]`, `health.Configure(definition.maxHealth)`, re-configure posture and the weakness filter, `ForceStagger()` with a 2 s stagger, publish `BossPhaseChanged`, return true.
- **Adds:** `RunArenaController` subscribes to `BossPhaseChanged` → `SpawnAdds(entryIndex, count)` through its normal `SpawnEnemy` + `ActivateFront` path. This keeps the 4-alive cap and the clear logic. **The boss never instantiates adds itself** (A5.1).
- `OnEnemyDied` destroys every phase clone (A5.7).
- Boss bar: a diegetic bar on the north wall. A quad scaled by `HealthBarFill(current, max)` (clamped 0..1) via MPB. Segment markers at the phase thresholds.
- **Tests:** `Advance_TrueWhilePhasesRemain_FalseAfter`, `Advance_SwapsDefinitionInOrder`, `BossAdds_RespectConcurrentCap`, `HealthBarFill_Clamps`.

### P3-08 Enemy health bars (non-boss)
- Shown only after the first hit; hidden 4 s after the last.
- A yaw-billboard quad 0.25 m above the head, with the fill driven by MPB. No Canvas per enemy.
- **No damage numbers** (VR clutter).

### P3-09 Drone and boss movement smoothness (BUG-38)
User report, Phase 2 headset playtest: after P2-11 humanoids are fine, drones and bosses are not. Builds on `MovementSmoothing` (`Enemies/MovementSmoothing.cs`; per-definition `turnRate`/`accel`/`decel`; drones 300/3/3, bosses 150/2.5/4).
1. **Drones:** hover/bank/strafe feel instead of walking logic: bob and bank into the direction of travel, ease strafe direction changes, no instant reversals.
2. **Bosses:** weight: turn in place before travelling, anticipation before a charge, heavier decel; blend between phases and attack states (ties into P3-07 `ForceStagger`).
3. Tune per-definition values; keep it in `MovementSmoothing` plus thin per-type pose code. No new `Find*`, no per-frame allocations.
- **Tests:** `Smoothing_Drone_BankFollowsLateralAccel`; `Smoothing_Boss_TurnInPlaceBeforeMove`.
- **Accept:** headset confirms drones and bosses look smooth; no added frame cost or GC allocs (Profiler on Quest).

### P3 exit
- Export Gate.
- G6 perf check with the **worst case**: a boss in phase 2 with 2 adds, 1 Volatile ring, 8 bolts and VFX.
- Amendment 11 (the weakness contract and the soft-lock gate). (9 and 10 were already used by Phase 1/2 numbers.)
- Tag `roguelike-phase3`, version `0.4.0`.

---

## PHASE 4 — Player stats, skills and boon expansion

**Goal:**
- A stat sheet where every stat changes physical play.
- 8 new VR-native skills in input-safe slots.
- About 30 new boons forming 7 build archetypes.
- Forge tempering, curses and duos.
- Meta unlocks that add variety: loadouts, heat.

### P4-01 Generic stat slots (no class per stat)
1. **`BoonEffectKind` is append-only.** Add the comment and the test `BoonEffectKind_ExistingValues_ArePinned` (BladeDamageMultiplier=0 … GrantAbility=6).
2. In `BoonInventory`, make `Sum(kind)` and `Product(kind)` public. The named properties stay.
3. In `PlayerCombatModifiers`, add `float[] boonStats` with `Boon(kind)` and `internal SetBoon(kind, v)`. Each value is a **bonus fraction**, and 0 is neutral.
4. `RunDirector.ApplyBoonInventoryToRig` loops over the kinds once per load and once per pick.
5. **Rule:** consumers read their stat **at use time**, never cached in `Awake`. `onLoaded` runs after `Awake`, and `ParryFlowController:92` shows the correct pattern.

### P4-02 The stat sheet (append these `BoonEffectKind`s)

| Stat (kind) | Base | Cap | Consumer (exact site) | Physical meaning |
|---|---|---|---|---|
| Max HP (exists) | 100 | 220 | `RunDirector` → `Health.Configure` | buffer for unseen hits |
| Blade damage (exists) | 1.0 | 2.0 | `PlayerCombatModifiers.DamageMultiplier` → `BladeDamager.TryHit` | power curve |
| **EdgeSensitivity** | 1.0 | thresholds ×0.70 | `WeaponDefinition.DamageForSpeed(speed, thresholdScale)` overload; the sweep gate at `BladeDamager:98` uses the same effective min; swing-end dedup stays on the raw threshold | less arm fatigue; helps seated play; **does not** raise max damage |
| **PostureDamage** | 1.0 | 2.0 | `PostureMeter.OnDamaged:83`, resolving mods from `info.Source` (cached per meter) | rewards committed heavy strikes |
| **Precision** | ×1.25 | ×2.0 | `BladeDamager.IsPrecisionHit` (P1-06) | aim and technique instead of random crits |
| **ParryWindow** | 0.12 s | +0.04 s | `MeleeAttacker.Deflect:318` | covers controller latency |
| **StaggerDuration** | per-enemy | ×1.5 | `MeleeAttacker.Deflect:327` | time to step in and cut |
| **DamageReduction** | 0% | 40% | `Health.IncomingDamageMultiplier` (P1-01) | survive hits from behind without inflating HP |
| Heal on kill (exists) | 0 | 10 | `RunDirector.OnEntityDied`; **also check that the victim has an `Enemy`** (no healing from Mirror allies) | sustain through aggression |
| **AbilityHaste** | 0 | 40% | `PhaseStepController.BlinkRoutine:97`, `MirrorSummonLogic.TrySummon`, `OverdriveLogic.AddCharge(1+h)` | enables ability builds |
| **PhaseCharges** | 1 | 3 | `PhaseStepController.Update`: charges + recharge timer | dodge rhythm |
| **EchoGain** | 1.0 | 1.5 | `RunDirector.OnRoomCleared:290` | meta economy |
| **Luck** | 0 | 3 | `BoonOfferPicker.Pick(luck)`: +4 Rare / +2 Epic tier weight per point | build steering |

**Excluded on purpose:**
- move speed (comfort, A1.3)
- attack speed (it's the player's arm)
- blade reach or hitbox size (breaks proprioception)
- random crit
- flat armor

**Tests:** one per consumer, through pure helpers, e.g. `DamageForSpeed_ThresholdScale_ReachesMaxEarlier_SameMax`, `ParryQuality_WindowBonus`, `IncomingDamage_Capped40`, `PhaseCharges_RechargeSequence`.

### P4-03 Input slots (no binding collisions)
- **Weapon Art:** right trigger while the sword is held. One at a time; a new one replaces the old.
- **Off-hand Art:** left trigger with an empty left hand. One at a time.
- **Passives:** no input.
- **A, B and X get no new holds.** They already carry 2–4 interactions each.
- `RunState.WeaponArtId` and `OffhandArtId` are appended snapshot fields.
- Add `Ronin7Input` actions `Gameplay/WeaponArt` (`<XRController>{RightHand}/triggerPressed`) and `Gameplay/OffhandArt` (left). First verify that nothing on foot enables `Right/Activate` (see the note at `XRRigBuilder.cs:1136`).

### P4-04 New skills (`Ronin7.Player`; pure logic class + thin MonoBehaviour each)

| ID | Skill | Slot / input | Mechanic | Pure logic and tests |
|---|---|---|---|---|
| S1 | **Riposte** | passive | after a `PerfectParry`, the next hit on **that attacker** within 1.2 s does ×1.75 damage and +100% posture. Applied inside the **boon** group via `mods.DamageMultiplierFor(victim)` | `RiposteLogic`: `MatchingTargetInWindow`, `OtherTarget_None`, `AfterWindow_None`, `ConsumedOnFirstHit` |
| S2 | **Iaido quick-draw** | passive gesture | grab from `KatanaHolster` and reach ≥ 4.5 m/s within 0.25 s → the first hit within 0.6 s does ×2.5. Re-arms only after re-sheathing | `QuickDrawLogic` (Armed → Drawing → Primed → Spent): `FastDraw_Primes`, `SlowDraw_No`, `Spent_NeedsResheathe`, `Expires_0_6` |
| S3 | **Zanshin** | passive | perfect parry → time scale 0.4 for 0.6 s real; 5 s cooldown; appended `TimeScaleChannel.Zanshin`; `OnApplicationPause` failsafe | `CooldownBlocks`, `ReleasesOnDisable` |
| S4 | **Charged Slash** | Weapon Art: hold 0.8 s (haptic ramp), release during a ≥ 5 m/s swing | one `SphereCastNonAlloc` (r 0.4, 6 m) along the swing plane; 60% of `DamageForSpeed` × mods to each hit; 6 s cooldown (haste applies) | `ChargeLogic`: `EarlyRelease_No`, `SlowSwing_No`, `Fire_Cooldown`, `Haste` |
| S5 | **Blade Throw & Recall** | Weapon Art (alternative to S4): release grip while holding the trigger at ≥ 4 m/s; squeeze grip on an empty hand to recall over 0.35 s | `KatanaHolster.SuppressNextResheathe`; `BladeDamager` stores the **wielder root** on grab and uses it as `DamageInfo.Source` while thrown | `ThrowLogic`: `ReleaseNoTrigger_Resheathes`, `FastWithTrigger_Throws`, `Recall_WithinDuration` |
| S6 | **Kiai Palm** | Off-hand Art: left trigger + left hand moving ≥ 4 m/s | within 2.5 m: 25 posture, 0.4 m shove; staggers enemies near breaking; 8 s cooldown; iterates `Health.Active`; new `PostureMeter.AddPosture(float)` | `BelowSpeed_No`, `RadiusOnly`, `Cooldown` |
| S7 | **Chain Hook** | Off-hand Art: aim (ray, 8 m), then yank toward the body at ≥ 2.5 m/s | pulls the **enemy** 3 m toward the player over 0.25 s and staggers it; never inside 1.0 m. **Never pulls the player** (sickness) | `YankSlow_No`, `PullClampedToStandoff` |
| S8 | **Element Imbue** | Off-hand Art: hold the trigger and sweep the empty left hand from blade base to tip within 0.5 s | 8 s imbue with an owned element (Bleed/Storm); doubles proc rate; emissive tint via `RendererTint`, **no point light** | `BaseToTip_Triggers`, `TipToBase_No`, `Expires` |

**Fixes to the existing abilities:**
- **Phase Step:** direction = left stick when past the deadzone, otherwise head-forward. Add 0.2 s of invulnerability during the fade (`IncomingDamageMultiplier = 0`).
- **Mirror:** `AllyCombatant.Configure(float dmg)`, with damage = 8 × `RunScaling.HealthMultiplier(depth)` × `BoonMultiplier`.

**Unlock path:**
- S1, S2 and S3 come from boons (P4-05).
- S4–S8 are Weapon or Off-hand Arts, offered as Epic boons **only after** the matching slot is unlocked in the shrine (P4-10).

### P4-05 Boon system expansion
1. **`BoonDefinition`** (additive):
   - `[Flags] BoonTag tags` (Parry, Flow, Bleed, Storm, Executioner, Ability, Bulwark, Curse)
   - `string[] requiresTags`
   - `bool hasSecondary; BoonEffectKind secondaryEffect; float secondaryMagnitude`
   - `Sprite icon` (filled in P5)
2. Append `BoonRarity.Duo = 3`.
3. **Picker** (on top of P1-08):
   - ×1.5 weight within a tier for boons that share a tag with owned boons;
   - Duo boons are eligible only when every required tag is owned.
4. **One proc host:** `Player/RunBoonProcs.cs`, added by `RoguelikeArenaBuilder`.
   - Subscribes to `EntityDamaged` (Source == rig), `EntityDied`, `PerfectParry`, `BladeClash`, `PostureBroken` and `PlayerHit`.
   - Runs one DoT list (≤ 5 entries) ticking at 4 Hz.
5. **Status damage:**
   - Append `DamageType.Status`.
   - `MeleeAttacker.OnDamagedFlinch` and `PostureMeter.OnDamaged` early-return on `Status`; otherwise DoT would stun-lock.
   - `RunBoonProcs` ignores Status damage for its own procs, so they don't loop.
6. **Retunes:**
   - Keen Edge: +5% damage **and** +5% posture.
   - Ironskin: +10 max HP and heal 10.
   - Flow Initiate: streak idle timeout 6 → 9 s, +3%.
   - Combo Initiate becomes **Reversal**: combo also advances on a swing-direction reversal against the same target. This fixes single-target fights (BUG-20). `SwordImpact` gets an appended velocity field.
7. **New boons:** create them as assets via `BoonCatalogBuilder`.

| # | Name | Rarity | Tags | Effect |
|---|---|---|---|---|
| 1 | Iron Wall | C | Parry | +0.02 s perfect window (max 2) |
| 2 | Long Stagger | C | Parry | +20% stagger duration (max 2) |
| 3 | Riposte | R | Parry | grants S1 (×1.75; +0.25 per extra stack, max 2) |
| 4 | Counterweight | R | Parry | perfect parry heals 4 (max 2) |
| 5 | Zanshin | E | Parry | grants S3 |
| 6 | Quick Draw | R | Executioner | grants S2 |
| 7 | Momentum Keeper | C | Flow | combo window +0.4 s (max 2) |
| 8 | Whirlwind | R | Flow | a swing hitting ≥ 2 enemies does +30% to each |
| 9 | Serrated Edge | C | Bleed | bleed = 15% of the hit over 3 s (+10% per stack, max 3) |
| 10 | Open Veins | R | Bleed | bleeding targets take +20% blade damage |
| 11 | Exsanguinate | R | Bleed | killing a bleeding enemy heals 6 |
| 12 | Static Edge | C | Storm | every 4th hit chains to the nearest enemy within 4 m for 12 × BoonMult (one pooled `LineRenderer`) |
| 13 | Conductor | R | Storm | +1 chain jump, +1.5 m |
| 14 | Thunderclap | R | Storm | `BladeClash` → 2.5 m shock: 20 damage + 25 posture |
| 15 | Heavy Hand | C | Executioner | +25% posture damage (max 3) |
| 16 | Guillotine | R | Executioner | posture-break bonus ×3 |
| 17 | Headhunter | C | Executioner | precision +0.25 (max 3) |
| 18 | Quickened | C | Ability | 12% haste (max 3) |
| 19 | Afterimage | R | Ability | +1 Phase Step charge |
| 20 | Overclock | R | Ability | Overdrive charge +50%; heal 1 per hit during Overdrive |
| 21 | Echo Twin | R | Ability | Mirror +10 s; ally scales with depth |
| 22 | Lacquered Armor | C | Bulwark | −8% incoming damage (max 3) |
| 23 | Last Stand | R | Bulwark | below 30% HP: +35% damage, −15% incoming |
| 24 | Glass Edge | R | Curse | +35% damage, −25 max HP |
| 25 | Blood Price | R | Curse | heal on kill +8; Forge MEND disabled |
| 26 | Reckless Flow | R | Curse, Parry | +12% per streak stack, but a non-perfect deflect breaks the streak |
| 27 | Hollow Oath | R | Curse | Epic tier weight ×2 for the run, −20% max HP |
| 28 | Thunder Riposte | Duo | Parry + Storm | riposte hits chain to everything within 4 m |
| 29 | Crimson Tide | Duo | Bleed + Flow | combo ≥ 3 → bleed ×2 |
| 30 | Ghost Step | Duo | Ability + Parry | a Phase Step within 0.25 s before an enemy's Active phase counts as a perfect parry (needs public `MeleeAttacker.TimeToActive`) |

**Archetypes:**
- **Kensei** (parry): 1–5, 26, 28
- **Dancer** (flow): 7–8, Reversal, 29
- **Hemorrhage** (bleed): 9–11, 29
- **Storm**: 12–14, 28
- **Executioner**: 6, 15–17
- **Shadow** (ability): 18–21, 30
- **Bulwark**: 22–23

**Tests (`BoonCatalogInvariantTests` and others):**
- `Duo_RequiresTwoValidTags`
- `Curse_HasNegativeSecondary`
- `EveryId_Unique`
- `DuoIneligibleWithoutTags`
- `TagAffinity_RaisesWeight`
- `StatusDamage_DoesNotReproc`
- `FlinchTiming_StatusNeverFlinches`
- `LastStand_TogglesAt30Percent`

### P4-06 Phase Step and Overdrive tuning pass
Use `runlog.jsonl` (P0-11) to check pick rates: no boon should be picked under 5% or over 40% when offered. Adjust the magnitudes in the builder and re-bake.

### P4-07 Forge Tempering
- The Forge's TEMPER card raises one owned non-Epic boon's magnitude by ×1.5.
- Stored in `RunState.TemperLevels` (snapshotted as a list).
- `BoonInventory` multiplies by `1 + 0.5 × temper`.
- **Test:** `BoonInventory_Temper_ScalesMagnitude`.

### P4-08 Weapon loadouts
New `WeaponDefinition` assets:

| Weapon | Min / reference speed | Damage | Cooldown | Style |
|---|---|---|---|---|
| **Nodachi** | 2.0 / 7 m/s | 14–60 | 0.35 s | big committed swings |
| **Tanto** | 1.0 / 4.5 m/s | 6–28 | 0.18 s | flicks; seated-friendly |
| **Twin Wakizashi** | — | 6–26 each | — | a second `Sword` in the left hand: parry with one, cut with the other |

- `BladeDamager.SetDefinition(WeaponDefinition)` is called from `ApplyBoonInventoryToRig`, because `Awake` caches `definition` too early.
- `RunState.LoadoutId` is snapshotted. The visual meshes come from `ArtPrefabBuilder` variants (P5).
- The loadout is picked on a "LOADOUT" rack in the boot menu.
- **Test:** `Loadout_AppliesDefinitionAfterLoad`.

### P4-09 Fracture Pressure (heat), unlocked after the first win
- `MetaSaveData.heatRanks` (a list) is snapshotted into `RunSnapshot`.

| Rank | Effect | Max ranks |
|---|---|---|
| Sharpened Program | enemy damage +15% | 3 |
| Hardened Shells | enemy HP +15% | 3 |
| Quick Telegraph | telegraph ×0.9, **hard floor 0.6 s** | 2 |
| Swarm Protocol | +1 **total** enemies per Combat room (never raises max concurrent) | 2 |
| Rusted Forge | MEND 30% | 1 |
| No Second Chances | revives disabled | 1 |
| Unforgiving Guard | non-perfect deflects take 30% chip damage | 1 |
| Escorted Bosses | +1 trash add | 1 |

- Echo multiplier = 1 + 0.1 × total ranks.
- **Daily Fracture:** a fixed date-seed plus a fixed heat set. It is cheap because `RunRng` is deterministic.
- **Tests:** `Heat_TelegraphFloor_0_6`, `Heat_Swarm_NeverExceedsMaxConcurrent`, `Heat_EchoMultiplier_Linear`, `DailySeed_StableForDate`.

### P4-10 Shrine variety unlocks
Extend the P2-01 table:

| Unlock | Cost (Echoes) |
|---|---|
| Nodachi / Tanto | 150 each |
| Twin Wakizashi | 300 |
| Weapon Art slot | 250 |
| Off-hand Art slot | 250 |
| Archetype packs (Bleed / Storm / Executioner boons enter the pool only once bought) | 120 each |
| **Keepsake**: start the run with one Epic you have picked in a past run | 200 each |

### P4-11 Split currencies: Shards vs Echoes (BUG-33, BUG-35, D-11)
Decision D-11 (user, 2026-10-14): **fully separate**. Source: Phase 2 headset playtest.
1. **Shards** (working name): in-run currency, earned per room, spent at the Shop and in events (Forgotten Shrine), shown live on the wrist HUD, **lost when the run ends** and never converted to Echoes.
2. **Echoes:** only from the end-of-run result (keep totals near current: ~320 win, 60-150 loss), spent only at the Shrine.
3. Add shard fields to `RunState`/`RunSnapshot`. **Bump `RunSnapshot.CurrentVersion` together with the P0-09 banking migration** (v2 runs bank their unbanked echoes on discard). Add the `RunRng.ForNode` seed mix (BUG-35) in the same bump, because it changes the golden vectors (map-drift rule, P0-09 step 7).
4. Relabel Shop, events, HUD and summary so the two names never overlap.
- **Tests:** `Shards_LostOnRunEnd_NeverBanked`; `Echoes_FromResultOnly`; `VersionMismatch_BanksEchoes_V2`; `ForNode_LowSeeds_VaryFirstDraw`. Add a `TestData/saves_phase4/` fixture (G7).
- **Accept:** Shop usable regardless of Shrine spending; HUD shows live Shards; G7 loads the v2 fixture.

### P4 exit
- Export Gate.
- `MetaSaveData` stays v1, with appended fields only. Add the test `MetaSave_V1WithoutNewFields_LoadsDefaults`.
- Amendment 10 (stat sheet, input slots, Status damage).
- Tag `roguelike-phase4`, version `0.5.0`.

---

## PHASE 5 — Graphics: "details everywhere"

**Goal:** three distinct, baked, detailed biomes with a visible space vista, readable VFX for every combat event and ability, a hero blade and hands, and polished diegetic UI, all inside the Quest budget.

### P5-00 The performance budget (enforced at every step)

| Metric | Quest 2 | Quest 3 | PCVR |
|---|---|---|---|
| GPU frame | ≤ 10 ms @90 (12.5 ms @72) | ≤ 10 ms | ≤ 8 ms on min-spec |
| CPU main + render | ≤ 8 ms | ≤ 8 ms | ≤ 6 ms |
| Draw calls | ≤ 120 | ≤ 175 | ≤ 600 |
| SetPass | ≤ 35 | ≤ 50 | ≤ 150 |
| Triangles | ≤ 500k | ≤ 800k | ≤ 3M |
| Texture memory | ≤ 350 MB | ≤ 600 MB | ≤ 2 GB |
| Live particles | ≤ 120 | ≤ 200 | ≤ 800 |
| Realtime lights per object | ≤ 2 | ≤ 2 | ≤ 8 |
| Shader variants (Android) | ≤ 3k | ≤ 3k | — |

**Per-frame allocation on Quest 3:**

| Item | Draw calls | Triangles |
|---|---|---|
| Room shell (static-batched) | ≤ 60 | ≤ 150k |
| Props | ≤ 20 | ≤ 30k |
| Sky + vista | 3 | — |
| 4 enemies + blob shadows | 8 | ≤ 25k |
| Hands + sword | **≤ 6** | — |
| VFX | ≤ 15 | — |
| UI | ≤ 10 | — |
| **Total** | **≈ 130** | — |

### P5-01 URP and Quest settings trims
In `Project/Assets/Settings/Mobile_RPAsset.asset`, set:
- `m_SupportsLightCookies 0`, `m_SupportsLightLayers 0`, `m_SupportDataDrivenLensFlare 0`, `m_SupportsTerrainHoles 0`
- `m_EnableLODCrossFade 0`, `m_ReflectionProbeBlending 0`
- `m_AdditionalLightsPerObjectLimit 2`
- keep SRP Batcher on, depth and opaque textures off, MSAA 4
- render scale 1.0 with foveation and dynamic resolution on Quest 3; 0.85 via `XRSettings.eyeTextureResolutionScale` on Quest 2
- try `m_StoreActionsOptimization` = Store

Remove GLES3 from Android once Vulkan is proven on device; that halves the shader variants.

### P5-02 Hands: merge meshes (52 → ≤ 4 draws)
In `ArtPrefabBuilder`:
- `Mesh.CombineMeshes` all rigid parts of each hand into one mesh, with one SamuraiToon material plus one outline material.
- Make the builder **idempotent**, so it preserves custom sword materials.

### P5-03 VFX upgrade
1. Extend `VfxPrefabBuilder.Spec` with `texture`, `renderMode` (Billboard/Stretched), `lengthScale`, `sizeCurve` and `colorCurve`.
2. Generate `Art/Textures/Vfx/{SoftDot,SparkStreak,Ring,Glyph}.png` (64–128 px) **procedurally in editor code** (radial gradients; no AI needed). Assign them to `VfxNeon_Additive.mat` and `PlasmaTrail_Additive.mat`.
3. Effects. All are pooled through `VfxPool`, additive, `ZWrite Off`, with no soft particles, lights, collision or sub-emitters.

| Effect | Trigger | Budget | Look |
|---|---|---|---|
| Blade trail | existing `TrailRenderer` | ≤ 60 verts | gradient texture; `minVertexDistance` 0.03; emit only above 2 m/s |
| Hit sparks × `MaterialClass` | `SwordImpact` | ≤ 16 | Organic: red + amber; Mech: amber streaks + white flash; Spectral: cyan/violet upward wisps; Armored: white-gold, short |
| Weakness exploited | `WeaknessExploited` | ≤ 20 | gold burst + `_FlashAmount` 1 |
| Hit resisted | `HitResisted` | ≤ 8 | blue-grey dull puff |
| Parry flash | `SwordDeflected` | ≤ 28 + ring | 0.12 s expanding ring quad ≤ 0.5 m; **no screen flash** |
| Posture break | `PostureBroken` | ≤ 24 | white shatter shards + blue rim |
| Death dissolve | `EntityDied` | shader + ≤ 30 embers | swap to `EnemyPBR_Dissolve.mat` (alpha clip + noise + emissive edge); `_Dissolve` 0 → 1 over 0.6 s via MPB, then pool. Only dying enemies pay the clip cost. |
| Spawn portal | `EnemySpawning` (P2-06) | ring + ≤ 20 | floor disc + beam; reverse dissolve |
| Boon pickup | `BoonChosen` | ≤ 30 | rarity-coloured burst; Epic is magenta |
| Overdrive | active | aura + trail swap | trail turns magenta; PC adds +0.2 bloom; **no full-screen tint on Quest** |
| Phase Step | start and end | ≤ 20 + afterimage | frozen hands-and-sword ghost from a saved `.mat`, fading over 0.3 s |
| Weakpoint Sight | active | MPB rim only | gold `_RimColor` pulse; reticle billboard on precision zones |
| Unbroken | proc | 1 shell | fresnel open-front hemisphere, r 1.2 m, 0.4 s; never covers the eyes |
| Mirror | summon | ghost body | replace runtime `MakeGhostMaterial()` (`Shader.Find`; the transparent variant can be stripped) with a saved `MirrorGhost.mat` |
| Skills S1–S8 | per skill | ≤ 20 each | Riposte: red edge glint; Iaido: white flash line; Charged Slash: travelling crescent mesh; Kiai: ground ring; Chain: `LineRenderer`; Imbue: element emissive |
| Impact decals | `SwordImpact` on world | ≤ 16 live | pooled quads (`ProjectilePool` pattern), **not** the URP Decal feature |

4. **Tests:** extend `CombatVfxTests`: every prefab has `maxParticles ≤ 30`; emit counts scale with `ParticleScale`.

### P5-04 Hero blade and shader fixes (BUG-28)
- Re-apply `CyberSword` to `Sword_Katana` from the builder source, so a rebuild keeps it.
- Add `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP` to `GrittyCyber`, `CyberSword` and `EnemyPBR` (PC Forward+).
- Add `multi_compile_fog` to `SamuraiToon`.
- Loadout meshes for Nodachi, Tanto and Wakizashi come from `ArtPrefabBuilder` variants.

### P5-05 Biomes as baked room-shell scenes
- **Code (small):**
  - Add `string[] roomScenes; Material skybox;` to `ArenaRoomLibrary.Biome`.
  - `RunArenaController.Start` picks a scene with `RunRng` and loads it additively under the black fade, then calls `LightProbes.TetrahedralizeAsync()` and sets `RenderSettings.skybox = biome.skybox`.
  - If `roomScenes` is empty, it falls back to `ArenaGeometryBuilder` (the greybox stays as the fallback and the existing tests stay valid).
  - Room roots sit at world identity (A5.3).
  - Room footprints match `ArenaHalfExtent` (P2-05). Author the 8 m and 11 m variants.
- **Test:** `Biome_EmptyRoomScenes_UsesGreybox`; `AllRoomScenes_InBuildSettings` (otherwise lightmap stripping renders them unlit).

**Kit per biome:** one 1024² trim sheet, one tiling material, `CyberNeon` emissive, plus alpha-clip foliage for the Garden only. Target ≤ 4 materials per room.

| Piece | Rust: Ash-World foundry | Program: Dominion data-vault | Garden: Silent Garden bio-dome |
|---|---|---|---|
| Floor 2×2 m, 3 variants | grated steel, oil stains (200 tris) | black hex panels, cyan seams (150) | moss-cracked stone, roots (300) |
| Wall 4×4.5 m (straight, corner, door) | riveted bulkheads, pipes (≤ 1.5k) | server racks, emissive strips (1k) | overgrown lattice, vine cards (1.2k) |
| Pillars | I-beams, chains (600) | monolith data pillars, glyphs (400) | trellis arches, bonsai trunk (1.5k) |
| Roof (open to the sky) | gantry crane + skylight | holo-grid ring | glass dome panes |
| Hero prop (1 per room) | broken mech / forge crucible (3k) | floating AI eye core (2k) | bioluminescent tree (4k) |
| Fixtures (emissive mesh, baked light) | amber sodium cages | cyan light bars | green glow pods |
| Decals | scorch, rust drips, hazard stripes | glyph markings, scan lines | petals, puddles, lichen |
| Animated (gated by `LightBudget.ShouldAnimate`) | rotating vent fans | slow holo glyphs | vertex-sway vines (vertex colour mask) |
| Hazard (P2, depth ≥ 5) | `HeatVent` | `ShockMine` | `PollenHazeController` (verify no story coupling first) |

**"Details everywhere": the five layers in every room:**
1. Baked kit.
2. 6–10 seeded props from `propPrefabs`, with BoxColliders only.
3. 8–12 static decals from one atlas per biome, 1 cm floor offset.
4. 2–4 animated pieces.
5. Sky vista.

**Lighting recipe, per room scene:**
- **Lightmaps:**
  - Lighting Settings asset: GPU progressive, 12–16 texels/m, max 1024, **Non-Directional**, compressed, ≤ 2 lightmaps.
  - Mixed mode **Baked Indirect** (not Shadowmask).
  - 1 baked directional + 6–12 baked point/area lights at the fixtures. Emissive GI = Baked.
- **Light probes:** 2 m grid at 0.3 / 1.5 / 3.0 m (about 150).
- **Reflection probe:** 1 baked box probe (128 Quest / 256 PC).
- **Realtime:**
  - Remove the 2 runtime accent points from `ArenaGeometryBuilder` when a shell is used.
  - Keep **one** realtime point light for gameplay signals (Overdrive, boss telegraph), gated by `LightBudget.ShouldAnimate(tier, isGameplaySignal: true)`.
- **Shadows:**
  - Quest: realtime off. Enemies get a GPU-instanced blob-shadow quad (1 draw each).
  - PC: mixed directional, dynamic casters, 20 m.

**Order:** build **Program** end-to-end first (the cleanest emissive look), measure, then Rust, then Garden. Aim for 3–4 shells per biome.

### P5-06 Sky and vista
- Use `Skybox/Cubemap` per sector.
- **Asset pipeline:**
  - Unity AI GenerateImage, 4096×2048 equirectangular: "seamless 360 space panorama, <palette> nebula, distant ringed gas giant, stars, no horizon objects".
  - Import as Cube, Lat-Long, PC 2048 / Android 1024 **ASTC 8×8**, mipmaps off.
- **Vista:** 2–3 unlit far objects (gas giant on `LowPoly_PlanetSphere`, fleet silhouette, orbital ring), ≤ 2k tris, no lights or shadows.
- Camera far clip 6000 → **300 m**.
- Fix the boot menu's `SpaceNeonNebulaSkybox.mat` the same way.

### P5-07 Diegetic UI
- Move every roguelike panel (`BoonOfferPanelFactory`, `RunSummaryPanelFactory`, `RunHudPanel`, the shrine, pause, events, gates) from legacy `Text` to **TextMeshProUGUI**.
  - Font: one OFL display font (Rajdhani or Orbitron), SDF 1024, padding 9, with LiberationSans as fallback.
  - Sizes at 0.001 scale: titles ≥ 48 px, body ≥ 28 px; targets ≥ 280 × 120.
- **Frame:** 9-slice dark glass (0.85 alpha) plus a thin `CyberNeon` edge mesh in the rarity colour. Boon cards curve at ±12° yaw.
- **Icons:**
  - `BoonDefinition.icon` for all 45 boons: 256², neon line-art on transparent, made with Unity AI GenerateImage.
  - One SpriteAtlas, ASTC 4×4.
  - Assigned by `BoonCatalogBuilder`.
- Test: `BoonCatalog_EveryBoonHasIcon`.

### P5-08 Boot menu dressing
- Replace the 28 primitive renderers with Program-biome kit pieces and a baked lightmap.
- Move the 9 embedded scene materials to assets.
- Add a holo "map table" that shows the last run's path, using `runlog.jsonl` data.
- Show the loadout rack (P4-08).

### P5 exit
- Export Gate. **G6 is the critical one:** measure every biome on Quest 2 and Quest 3.
- Use a Memory Profiler snapshot for texture memory, and RenderDoc Meta for overdraw (≤ 2 full-screen layers).
- Tag `roguelike-phase5`, version `0.6.0`.

---

## PHASE 6 — Audio, accessibility, narrative, store submission

**Goal:** release candidate.

### P6-01 Audio
1. **Mixer:** create `Audio/Ronin7.mixer` with Master / Music / SFX / Ambience / UI groups.
   - Expose dB parameters.
   - `SettingsService` maps its 0–1 sliders to dB (`20*log10(max(v,0.0001))`).
   - Duck Music −6 dB for 0.3 s on `WeaknessExploited`, `PostureBroken` and `BossPhaseChanged`.
2. **New `AudioDirector` slots and subscriptions:**
   - `RunStarted`, `RoomCleared`, `RunEnded` (won/lost), `BoonOffered`, `HitResisted`, `WeaknessExploited`, `BossPhaseChanged`, `EnemySpawning`
   - a low-health loop below 25% HP, driven by the existing `Health` event and `HeartbeatHaptics`
   - a menu ambience (closes `XRRigBuilder.cs:882` TODO)
3. **Music:**
   - 3 sector explore/combat pairs plus 1 boss track per sector.
   - `OnRunNodeEntered` selects the boss track when the node kind is Boss; add `Kind` to the event if it is missing.
   - Source: licensed or commissioned tracks. Fill gaps with `ProceduralAudioSynth`.
4. **Enemy voice:** a per-archetype windup cue (distinct pitch and timbre). Telegraphs must work **without colour** (accessibility).
5. **Import settings for Quest:**
   - Vorbis, quality 0.5.
   - Mono for 3D one-shots.
   - Decompress On Load under 200 KB; Compressed In Memory for music and ambience.
   - Preload off for loops.
6. **Placeholders to replace:** `boonChosen`, `runLost`, `runWon`, `enemySpawning` are noise-burst procedural stand-ins (Phase 2 playtest, 2026-10-14); give them real or properly synthesised cues.
- **Tests:** `AudioDirector.MusicFor(kind, sector)` selector; `VolumeToDb_*`. Add a dev log on null clips in `PlayAt`.

### P6-02 Haptics and comfort options
Add these to `SettingsPanel`:
- **Haptic intensity** 0–100%: one multiplier inside `Haptics.Pulse`. Also cache the device per node.
- **Reduce flashes:** turns off the vignette flash and the bloom spikes.
- **Slow-mo strength:** 0.25–0.5, applied to deflect, Zanshin and Overdrive.
- **Vignette strength.**

### P6-03 Accessibility
- **Colour-blind-safe telegraphs:**
  - Every windup also has a shape or motion cue: the rim **pulses** 2 Hz → 6 Hz plus the edge line.
  - Never use a red/green pair for good/bad.
  - Rarity shows its text label as well as its colour.
- **UI scale:** a 1.0–1.5× multiplier on world-space panels.
- **Assist:** a "Damage taken" slider (50–100%) and "Enemy aggression" (cooldown ×1–1.5) in `GameSettings`.
  - When either is below default, the run is tagged "Assisted" in `runlog` and the summary.
  - Assisted runs bank Echoes normally (default D-06).
- **Subtitles** for any VO (P6-04): a head-locked lower band, 28 px, toggle.
- **Tests:** `Settings_Roundtrip`, `AssistMultipliers_Clamp`.

### P6-04 Light-touch narrative
- **`Flow/Roguelike/KesslerRunLines.cs`**: a static line bank, following the `ChNNLines` pattern, keyed by outcome (first run / died in sector N / won / won with heat), 3–4 variants each.
  - Spoken on the menu after the summary, using the existing `DialoguePlayer` and the VO pipeline (`generate_voice.py`, edge-tts; Davis and Jason voices are retired).
- **Boss intros:** one line per boss when its node is entered, plus one per phase change.
- **Naming:** boon, event and sector names drawn from the story bible's factions and items (content only).

### P6-05 Store and submission (Meta Quest Store / App Lab, plus Steam for PCVR)
1. **VRC checklist pass:**
   - focus loss and pause (P0-03)
   - system menu, guardian/boundary (`BoundaryVisibilityFeature`), tracking loss (P0-03)
   - 90 Hz and no stale-frame spikes
   - no camera shake
   - controller models
   - first-launch comfort rating (**Moderate**; Comfortable if teleport is the default)
   - age rating via IARC
2. **Store assets:**
   - Quest: icon, cover art (2560×1440), hero, 5+ screenshots via the RenderTexture capture recipe, trailer.
   - Steam: capsule images.
3. **Privacy:** `runlog` is local-only. State "no data collected" in the store form.
4. **Build:**
   - Release IL2CPP, stripping Low, `RONIN7_DEV` off.
   - Upload with `ovr-platform-util upload-quest-build`.
   - PC: SteamVR + OpenXR, with Index/Vive bindings checked in the OpenXR interaction profiles.
5. **Final soak:** 5 complete runs per platform, plus 10 runs with heat and the Daily seed. Read `runlog.jsonl`: no crash and no soft-lock.

### P6-06 Replay tutorial (BUG-32)
The tutorial is skipped when `runsCompleted > 0` (P2-08). Add a "Replay tutorial" toggle in `SettingsPanel` that forces the next run to show the prompts without touching `runsCompleted`. Placed in P6 (accessibility/onboarding) because players need it; the earlier verification need is covered by the dev reset in P3-00.
- **Test:** `Settings_ReplayTutorial_Roundtrip`. **Accept:** headset: prompts show on a non-first run with the toggle on.

### P6 exit
- Export Gate plus store submission.
- Tag `roguelike-1.0.0`, version `1.0.0`.

---

# PART D — Decisions for you (the plan assumes the defaults)

| ID | Decision | Default (assumed) | Alternative |
|---|---|---|---|
| D-01 | Package ID and studio name | `com.<studio>.ronin7`; you supply `<studio>` | — |
| D-02 | Keep the story campaign or hub in the roguelike build? | **No.** Ship `Phase6_Boot` + `RunArena` (+ room scenes). Delete `RunLauncher` and the hub patch. Story code stays compiled (size cost only). | Keep the hub as a between-runs space with Kessler (needs `RunLauncher` bound to an interactable, plus the hub in the build) |
| D-03 | Overdrive's hidden blade-speed inflation | **Fix** (unscaled speed), with an explicit ×1.5 damage while active | Keep the "hyper-reflex" fantasy and document it |
| D-04 | Weakpoint Sight rework | **Precision-zone multiplier only** (×2.0 on head and thrust hits) | 6 s burst at ×1.6, 12 s cooldown |
| D-05 | Abilities unlocked in the story apply in runs? | **No.** Roguelike abilities come only from `RunState`. | Yes, and filter the owned ones from offers |
| D-06 | Do assisted runs earn Echoes? | **Yes**, tagged "Assisted" | Reduced or none |
| D-07 | Windup-cancel spam (you deferred the global fix) | **Per-type super-armor only** (heavies, elites, bosses); trash stays cancellable | Global "one cancel per attack" |
| D-08 | Attack concurrency | **No cap** (you rejected one); out-of-view attackers get +0.25 s telegraph only | — |
| D-09 | Mid-run save-scum (resume at node entry) | **Allowed** (Hades-style) | Resume at min(entry HP, 50%) |
| D-10 | Snap vs teleport default | Smooth locomotion + snap turn + vignette; teleport optional | Teleport default (raises the comfort rating) |
| D-11 | Run currency vs Echoes | **Fully separate** (decided by user 2026-10-14): Shards are in-run (Shop, events, live HUD, lost at run end); Echoes come only from the end-of-run result and are spent at the Shrine. See P4-11. | Convert unspent Shards to Echoes at run end |
| D-12 | **Open question:** two Shops per sector are possible (row 3 is always Forge+Shop, and rows 1-2 can roll a Shop). Spec is ambiguous; the user has not decided. | Allow it until decided | Cap one Shop per sector |

---

## Appendix — Agent dispatch template per task

The routing in `CLAUDE.md` applies:
- **Sonnet:** edits, tests, data
- **Opus:** P0-02/P0-03 flow ordering, P1-01 damage pipeline, P2-04 map graph, P3-02/P3-07 weakness and phase wiring, and the shader work
- **Haiku:** lookups

Each agent prompt must:
1. Confirm `git branch --show-current` is `roguelike`.
2. Name the task ID and paste its section from this file.
3. List the tests to write **first**, then the implementation.
4. Run the EditMode gate. With the editor open, use the MCP `tests-run` and read `Logs/Editor.log`; with it closed, use batchmode and wait for `res.xml`.
5. Run `graphify update .` after the code changes.
6. Report changed files, the test count delta, and anything left for in-headset tuning.
7. Not commit. Commits happen at the phase exit, after the Export Gate.

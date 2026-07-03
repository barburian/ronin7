# Chapter Build Ledger

Tracker for the chapter-build production pass. Updated after every chapter's verification.
Gate rule: **zero EditMode failures + the chapter's new fixtures present** (never a raw
"== 400" comparison — legacy EpNN LinesTests shrink the count as batches are deleted).

## Canonical chapter scenes (CampaignDirector `ChapterCampaign.asset` order)

| Mission id | Entry scene | Chapter |
|---|---|---|
| — (flag `ch1_complete`) | Galaxy1_Ch1_Hub | Ch01 The Salvager's Debt (hub itself) |
| CH02 | Ch02_Auction | Ch02 The Auction |
| CH03 | Ch03_SwordRemembers | Ch03 The Sword Remembers |
| CH04 | Ch04_OverseersHunt | Ch04 The Overseer's Hunt |
| CH05 | Ch05_DebtOfAshes | Ch05 The Debt of Ashes |
| CH06 | Ch06_IronDojo | Ch06 The Iron Dojo |
| CH07 | Ch07_ForgottenNames | Ch07 Forgotten Names |
| CH08 | Ch08_SilentGarden | Ch08 The Silent Garden |
| CH09 | Ch09_PitAndTheDeep | Ch09 The Pit and the Deep |
| CH10 | Ch10_LedgerOfRust | Ch10 The Ledger of Rust |
| CH11 | Ch11_GhostsAndOrigins | Ch11 Ghosts and Origins |
| CH12 | Ch12_TheFracture | Ch12 The Fracture |
| CH13 | Ch13_SterileReckoning | Ch13 The Sterile Reckoning |
| CH16 | Ch16_ThroneOfAshes | Ch16 The Throne of Ashes (finale) |

Ch14 cut, Ch15 merged into Ch16 — never built. Completion flags: `chN_complete` per chapter
(hub room gating); scene-completion via ZoneCompleted for Ch2+.

## EditMode test-count ledger

| Milestone | Expected tests | Actual | Result |
|---|---|---|---|
| Baseline (pre-Phase 0) | 418 (415 pass / 3 skip) | 418, 0 failed, 3 skipped (CyberAssets ×2, PlanetAssets ×1 — skip by design) | PASS 2026-07-02 |
| Phase 0 (chapter flow foundation) | 426 (+8: HubStateControllerTests, ChapterOutroTests) | 426 / 0 failed / 3 skipped | PASS 2026-07-02 |
| Phase 1 (ability framework + Echo + placeholders) | 450 (+24: CampaignStateAbility 11, EchoCalloutSelector 7, EchoLines 6) | 450 / 0 failed / 3 skipped | PASS 2026-07-02 |
| Ch01 retrofit (Echo on rig, rewire gap closed, Ep01 batch deleted) | 450 (±0 — no Ep01LinesTests existed) | 450 / 0 failed / 3 skipped | PASS 2026-07-02 |
| Ch02 (Auction + ProtectNpcObjective) | 461 (+11) | 461 / 0 failed / 3 skipped | PASS 2026-07-02 |
| Ch02 leftovers + Ch03 fixtures (post Ep02–04 deletion −15, Ch03 +21) | 467 | 467 / 0 failed / 3 skipped | PASS 2026-07-03 |
| Ch03 (post Ep05–07 deletion −15) | 452 | 452 / 0 failed / 3 skipped | PASS 2026-07-03 |
| Ch04 (+18 fixtures; post Ep08–09 deletion −10) | 460 | 460 / 0 failed / 3 skipped | PASS 2026-07-03 |
| Ch05 (+7 fixtures; post Ep10–11 deletion −10) | 457 | 457 / 0 failed / 3 skipped | PASS 2026-07-03 |
| Ch06 (+17 fixtures: MultiObjectiveLogic 8, ActivationRelay 2, Chapter6Lines 7; post Ep12–13 deletion −10) | 464 | 464 (461 pass / 3 skip) / 0 failed | PASS 2026-07-03 |
| Ch07 (+21 fixtures: WeakpointSightLogic, PlayerCombatModifiers, Chapter7Lines, BladeDamager multiplier ×3; post Ep14–15 deletion −10) | 475 | 475 (472 pass / 3 skip) / 0 failed | PASS 2026-07-03 |
| Ch08 (+15 fixtures: RiddleTrialLogic 8, Chapter8Lines 7; post Ep16–17 deletion −10) | 480 | 480 (477 pass / 3 skip) / 0 failed | PASS 2026-07-03 |
| Ch09 (+22 fixtures: OverdriveLogic 15, Chapter9Lines 7; post Ep18–19 deletion −10) | 492 | 492 (489 pass / 3 skip) / 0 failed | PASS 2026-07-03 |
| Ch10 (+14 fixtures: PhaseStepSolver 7, Chapter10Lines 7; post Ep20–21 deletion −10) | 496 | 496 (493 pass / 3 skip) / 0 failed | PASS 2026-07-03 |

## Legacy deletion batches

| After | Delete | Done |
|---|---|---|
| Ch01 retrofit | Ep01 (Builder+VoiceManifest deleted; Ep01Lines KEPT — live dep of Galaxy1Builder + EnemyWarningBuilder) | 2026-07-02 |
| Ch02 | Ep02–Ep04 (builders+manifests+LinesTests; Ep02/03/04 Lines KEPT — live Galaxy1Builder deps; Ep04 scene builders moved to shared) | 2026-07-03 |
| Ch03 | Ep05–Ep07 (8 builders incl. surprise Ep07BuilderSpace + 3 manifests + 3 LinesTests; Lines KEPT — live Galaxy1Builder/EnemyWarningBuilder deps; Ep05–07 scene builders moved to shared) | 2026-07-03 |
| Ch04 | Ep08–Ep09 (5 builders incl. Ep08BuilderSpace + 2 manifests + 2 LinesTests; Lines KEPT — Galaxy1/Galaxy2Builder deps; Ep08 space builders moved to shared; PlayMode MechanicsTests kept — test generic components. Orphan candidates: Data/Ep08Reaper.asset, Data/Ep09Vera.asset) | 2026-07-03 |
| Ch05 | Ep10–Ep11 (4 builders + 2 manifests + 2 LinesTests; Lines KEPT — Galaxy2Builder deps; nothing needed moving; PlayMode MechanicsTests kept. Orphan candidate: Data/Ep10Enforcer.asset) | 2026-07-03 |
| Ch06 | Ep12–Ep13 (4 builders incl. Ep12/13BuilderFinale + 2 VoiceManifests + 2 LinesTests; Ep12/13 Lines KEPT — live Galaxy2Builder `space_ep12_post`/`space_ep13_post` deps) | 2026-07-03 |
| Ch07 | Ep14–Ep15 (4 builders incl. finales + 2 VoiceManifests + 2 LinesTests; Ep14/15 Lines KEPT — live Galaxy2Builder space_ep1N_post deps) | 2026-07-03 |
| Ch08 | Ep16–Ep17 (4 builders incl. finales + 2 VoiceManifests + 2 LinesTests; Ep16Lines KEPT — Galaxy2Builder dep; Ep17Lines KEPT — Galaxy3Builder dep) | 2026-07-03 |
| Ch09 | Ep18–Ep19 (4 builders incl. finales + 2 VoiceManifests + 2 LinesTests; Ep18/19Lines KEPT — live Galaxy3Builder space_ep1N_post deps) | 2026-07-03 |
| Ch10 | Ep20–Ep21 (4 builders incl. finales + 2 VoiceManifests + 2 LinesTests; Ep20/21Lines KEPT — live Galaxy3Builder space_ep2N_post deps) | 2026-07-03 |
| Ch11 | Ep22–Ep23 | |
| Ch12 | Ep24–Ep25 | |
| Ch13 | Ep26–Ep28 | |
| Ch16 | Ep29–Ep33 + repo-wide `Ep\d` sweep | |

Each batch = builders + Lines + VoiceManifests + LinesTests + all `.meta` files.
Before deleting any EpNN file: grep it for methods still called by Chapter*/Hub*/shared files.

## Ability-framework wiring (from Ch07 onward)

- Permanent abilities live on the rig via the shared `XRRigBuilder.AttachPlayerAbilities(rig, refs)`
  helper (ChapterSharedBuilders.cs) — every chapter builder from Ch07 on calls it so a later chapter
  can't drop an already-earned ability. GROW that helper's list as each ability ships (Overdrive Ch9,
  Phase-step Ch10, Unbroken Ch11, Mirror Ch12). Each ability component self-gates in Awake on
  `CampaignState.HasAbility`. Unlock stays per-chapter via an `AbilityGranter` on a Trigger/outro GO.
- Any new ability with an `InputActionReference` MUST get a repair loop in `XRRigBuilder.RewireOpenScene`
  (the input-import race nulls the wire at build time; RewireOpenScene re-wires before save). Ch07's
  `WeakpointSight.toggleAction` needed this — without it the toggle is null in the saved scene.
- Weakpoint-sight input: Left-X hosts Crouch (Tap) + Toggle Weakpoint Sight (Hold 0.6s) as two
  interactions on one control; both read via `WasPerformedThisFrame`. Follow this tap/hold overload
  pattern for future ability buttons (rig is input-saturated).
- `BladeDamager` reads the wielder's `PlayerCombatModifiers.DamageMultiplier` (resolved lazily at hit
  time — the sword is an unparented world Grabbable at Awake, so it must resolve from `transform.root`
  on the hit, not in Awake). Default 1.0 when absent, so pre-Ch7 scenes are unaffected.

## Known-warning whitelist (console noise accepted during verification)

- `Camera "Main Camera" does not use a Tracked Pose Driver (Input System)` — rig uses custom PoseFollower
- `XR: Error setting active audio output driver` + `OculusLoader.EditorLoadOVRPlugin` NRE — no headset attached
- Test-generated expected logs: SaveSystem error paths, GeminiClient aspect fallback, ProjectilePool starvation, ArtPrefabRegistry greybox fallback (Hand_L/R)
- `[Space Samurai] Input action not found` during any scene rebuild — pre-existing builder race;
  `RewireOpenScene()` repairs rig components, DialoguePlayers, PromptInputAdvancers, SettingsMenuToggle,
  and each ability's InputActionReference before save. Baseline 15 (14 legacy + "Left Hand/Talk"); the
  count grows by ONE per shipped ability action (Toggle Weakpoint Sight, Activate Overdrive, Phase Step,
  …) — expected, not a defect. Phase-step ground probe uses layer mask `~0` (can snap onto a dynamic
  body top; no-fall guarantee still holds) — give it a proper ground layer in the in-headset tuning pass.
- `console-clear-logs` MCP tool broken (file lock, HTTP 500) — use `lastMinutes` filtering instead.
- 4× `EnvironmentDependencyValues`/`graphicsApiMask 4 -> 262148` on playmode transitions — Unity 6 infra noise.
- `Assets/AI Toolkit/Temp/*.glb` IOException import-loop spam — pre-existing AI Toolkit scratch-file lock;
  path is gitignored. Console **Error Pause is disabled** (it froze automated playmode runs on this spam).
- ai-game.dev cloud bridge intermittently drops to 401 after long playmode sessions; refresh via plugin
  window (Ctrl+Alt+A) or editor restart. The unity-mcp relay bridge keeps working — use it as fallback.
- `Assert: "Access version should be odd when acquiring lock"` spam (~100×, ~11s) during a scene build —
  Unity 6 internal, transient, no functional impact (scene saves + assertions pass). It can evict builder
  Debug.Logs from the MCP log buffer; re-run the build in-process to recapture logs if needed.

## Perf reference bar (Ch1 hub scene, edit-mode UnityStats at greybox)

- drawCalls 189, setPassCalls 17, tris 9,198, verts 13,092 (2026-07-02)

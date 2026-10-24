# Store prep (P6-05, PREP ONLY)

Status 2026-10-24, build v0.6.1 (code 7). Nothing was uploaded, no listing or dashboard account was touched, no player was built. This is a read-only code and settings audit plus in-editor renders. Verdict key: **PASS** (code evidence), **NEEDS-DEVICE** (check in the headset), **GAP** (missing or wrong).

## 1. VRC checklist pass

| # | Item | Verdict | Evidence / what to do |
|---|---|---|---|
| 1 | Focus loss and pause | PASS + NEEDS-DEVICE | `Flow/Roguelike/RunPauseController.cs:77` `OnApplicationFocus(false)` pauses (skipped in the editor only), `:82` `OnApplicationPause`, `:87` Menu button. `Pause()` sets `TimeScaleChannel.Pause` to 0, freezes the offer timer, `AudioListener.pause = true`, `PlayerPrefs.Save()`. Device: take off the headset, open the Oculus/system menu mid-combat, return: the run is paused and RESUME works; audio is silent while paused. |
| 2 | System menu (Oculus button) | PASS + NEEDS-DEVICE | OS-owned. The app only has to yield cleanly: `OnApplicationFocus` covers it (item 1). Device: press the Oculus button during a boss fight, confirm no input is eaten on return. |
| 3 | Guardian / boundary | PASS + NEEDS-DEVICE | `BoundaryVisibilityFeature` is enabled for Android and Standalone (`Assets/XR/Settings/OpenXR Package Settings.asset:233-235` and `:380-382`). The game is stand-up, locomotion-driven; no code suppresses the boundary. Device: walk to the boundary edge, the guardian must show. |
| 4 | Tracking loss | PASS + NEEDS-DEVICE | `Player/Grabber.cs:61-79` `GuardHeldBlade`: an untracked hand disarms the held blade, re-arms after 3 tracked frames (`ArmAfterTrackedFrames`, tested). `RunPauseController` pauses on app pause. Device: cover a controller and the headset sensors; no phantom hits on return. Headset tracking loss itself is OS-handled. |
| 5 | 90 Hz | PASS (Quest) / NEEDS-DEVICE | `Flow/QualityBootstrap.cs:32` `targetRefreshRate = 90`, `:67` `vSyncCount = 0` (XR compositor paces), `:85-95` picks the nearest supported rate and calls `TryRequestDisplayRefreshRate`, logs the actual rate. Android only; PCVR follows the OpenXR runtime (headset setting). Device: OVR Metrics, confirm 90 Hz and 0 stale frames over a full run and a boss fight. |
| 6 | No stale-frame spikes | NEEDS-DEVICE | Editor proxy is `PerfBudgetProbe` (Quest 3 budget 175 draws / 50 SetPass; last measured 46-75 draws, 26-42 SetPass in P6-02/03). OpenXR: `m_renderMode 1` (single-pass instanced), AutomaticDynamicResolution + Foveated Rendering enabled for Android. Frame time on device is the truth (G6 worst case). |
| 7 | No camera shake | PASS | Grep of `Scripts/` for shake: only comments stating "no shake" (`RunDirector.cs:1404`, `CombatVfxController.cs:232`, `HeartbeatHaptics.cs:10`, `CombatFeedbackController.cs:139`, `PhaseStepController.cs:34`). Nothing writes to the camera or rig transform for feel. |
| 8 | Controller models | NEEDS-DEVICE | The rig builds its own hand visuals (`Editor/XRRigBuilder.cs:1987-2005`, art prefab with greybox fallback), not the runtime-provided controller model. Meta accepts this when the visual follows the controller and matches the held weapon. Interaction profiles enabled: Oculus Touch, Touch Plus, Touch Pro on both targets. Device: hands/sword line up with the real controllers, nothing floats. |
| 9 | Comfort rating | **GAP fixed**, then Moderate | D-10 says smooth locomotion + snap turn + vignette = **Moderate** (Comfortable only if teleport were the default). But `Core/GameSettings.cs:69` had `SnapTurn = false` and `SettingsService.Load` applies it (`SettingsService.cs:270`), so a first launch used continuous turning, which is Intense territory. Fixed (one line, `SnapTurn = true`) with test `GameSettings_Default_IsSmoothLocomotionWithSnapTurn`. Existing installs keep their saved choice. Vignette default on. Teleport optional. Declare **Moderate** in the store form. |
| 10 | Age rating (IARC) | TODO for user | Questionnaire in the dashboard (not touched). Suggested answers: fantasy/sci-fi violence (sword combat against humanoid and robot enemies, blood toggle `CombatBlood` default on), no gambling, no real-money purchases, no user-generated content, no online interaction, no location sharing. Expect a Teen-class rating; turn `CombatBlood` off by default if a lower class is wanted. |

## 2. Build config audit (item 4, read only)

Effective values read through `PlayerSettings` (ProjectSettings.asset has uncommitted changes and was not edited).

| Setting | Android (Quest) | Standalone (PCVR) | Verdict |
|---|---|---|---|
| Scripting backend | IL2CPP | **Mono2x** | Quest PASS. PC works but Meta/Steam do not require IL2CPP; consider IL2CPP for parity (exit step, optional). |
| IL2CPP compiler config | Release | Release | PASS |
| Code generation | OptimizeSpeed | OptimizeSpeed | PASS |
| Managed stripping | **Minimal** (unset in the asset, Unity default) | Disabled | **GAP**: set Low on Android (and Low on Standalone) at the exit step, then re-run the gate and a device smoke test, because Low can strip reflection-only types. |
| `RONIN7_DEV` | not in defines | not in defines | PASS. `BuildMenu` only passes it via `extraScriptingDefines` for `-dev` builds and `Development` option; release builds never get it. `DevRunTools`, `SceneDebugOverlay` and the HUD debug lines are `#if RONIN7_DEV`. |
| minSdk / targetSdk / arch | 32 / auto / ARM64 | n/a | PASS |
| Version | 0.6.1 code 7 | | Exit step bumps to 1.0.0 |
| OpenXR loader | OpenXR | OpenXR | PASS |
| PC interaction profiles (Index / Vive) | n/a | `ValveIndexControllerProfile Standalone` is present but **not enabled**; `HTCViveControllerProfile` is not enabled; only Oculus Touch / Touch Plus / Touch Pro are on | **GAP** for the exit step: enable `ValveIndexControllerProfile` and `HTCViveControllerProfile` (and `KHRSimpleController` as fallback) for Standalone in `Assets/XR/Settings/OpenXR Package Settings.asset`, then check bindings in SteamVR with an Index. Needs a headset. |

Other findings from the real v0.6.1 APK (`aapt dump permissions`):

- **GAP: `android.permission.INTERNET` is in the manifest.** The game never uses it (see section 3). Set `forceRemoveInternetPermission: 1` on `MetaQuestFeature Android` in the OpenXR settings, rebuild, re-check with aapt.
- **GAP: `com.oculus.permission.EYE_TRACKING` and a *required* `oculus.software.eye_tracking` feature.** Cause: the enabled `FoveatedRenderingFeature` declares `XR_META_foveation_eye_tracked`, and `ModifyAndroidManifestMeta.cs:142-160` adds the permission when Quest Pro is targeted. A required eye-tracking feature hides the app from Quest 2/3/3S, and the permission contradicts "no data collected". Exit step: stop targeting Quest Pro in the Meta Quest Support feature (or remove the eye-gaze extension), rebuild, confirm the manifest has neither entry. Needs verification on a build; not changed now.
- Dormant networking code in the player: the Unity-MCP packages (`com.ivanmurzak.unity.mcp*`, `Assets/Plugins/NuGet/` SignalR client) are compiled into player builds (their DLLs appear in earlier `Builds/*/Managed` folders). Nothing calls them at runtime (no `RuntimeInitializeOnLoadMethod` connects; `UnityMcpPlugin.Connect` is only called by the editor side), but they inflate the build and a store scanner may flag SignalR. Exit step: exclude them from player builds (platform checkboxes on the NuGet DLLs, or remove the packages from the release branch). Plugins/NuGet and the manifest have unrelated uncommitted changes, so this was not touched.

## 3. Privacy

Grep of all runtime code (`Scripts/` minus `Editor`, `Tests`): no `UnityWebRequest`, `HttpClient`, `WebClient`, `System.Net`, sockets, `Application.OpenURL`, Unity Analytics or Services, Firebase/Sentry/Crashlytics, Meta Platform SDK or entitlement checks. The only `UnityWebRequest` hits are in `Scripts/Editor/Art/` (Gemini art tooling), which is editor-only and never in a build. `runlog.jsonl`, the autosave snapshot and `PlayerPrefs` settings are written to local storage only. The built-in `com.unity.modules.unityanalytics` module is present but no analytics service is initialised. Caveat: fix the INTERNET and eye-tracking manifest entries in section 2 so the store's own manifest scan agrees with the statement.

**Privacy statement (for the store page and the privacy-policy URL):**

> Ronin 7 does not collect, transmit or share any personal data. The game works fully offline. Settings, run history and saves are stored only on your device and are removed when you uninstall the game. The game contains no analytics, advertising or tracking, and uses no microphone, camera, location or eye/face tracking.

**Exact store-form answers (Meta Quest, Data Use / Privacy section):**

| Question | Answer |
|---|---|
| Does the app collect or share user data? | No |
| Data types collected (account, device ID, location, usage, biometrics, audio, etc.) | None |
| Eye tracking / face tracking / hand tracking data | Not used (after the manifest fix) |
| Is data shared with third parties? | No |
| Is data encrypted in transit / can users request deletion? | Not applicable (no data leaves the device); uninstall removes everything |
| Privacy-policy URL | Required by the form even for "no data": host the statement above (user task) |
| Steam "data collection" questions | Same: no |

## 4. Store asset shot list

Meta Quest requirements (verify against the dashboard at upload time, they change): icon 1:1 (1024 x 1024, also 512 x 512), cover art 2560 x 1440 (landscape 16:9), hero 3000 x 900, square 1600 x 1600 (older spec), 5+ screenshots (2560 x 1440 or 1920 x 1080), trailer 1920 x 1080 mp4 (ideally 30 s to 2 min). Steam: header capsule 920 x 430, small capsule 462 x 174, main capsule 1232 x 706, vertical capsule 748 x 896, library hero 3840 x 1240, library logo 1280 x 720 (transparent), page background 1438 x 810, community icon 184 x 184, screenshots 1920 x 1080.

### Captured (in `Project/Docs/StoreShots/`, 2560 x 1440 JPG, PC tier, URP post stack, time frozen, Camera.main at 1.6 m, hand meshes hidden)

| File | Room | Composition | Luma / magenta | Use |
|---|---|---|---|---|
| `rust_combat.jpg` | Rust (The Rustfang), Combat | Two Gangers centred on the rust wall, hazard-striped floor, yellow strip lights | 0.131 / 0.0000 | Screenshot 1; good |
| `rust_elite.jpg` | Rust, Elite | Orange-rimmed Elite mech centre, second enemy clipped left | 0.147 / 0.0000 | Screenshot, reads as an elite fight; slightly dark |
| `rust_boss.jpg` | Rust, Boss | The boss crouched mid-room under a nebula sky, sector exit light behind | 0.140 / 0.0000 | Usable; boss is dark and small, a closer art shot is needed |
| `program_combat.jpg` | Program, Combat | Ganger and Brute across a cyan hex-floor server room, planet sky | 0.262 / 0.0000 | Best colour contrast, lead screenshot |
| `program_boss.jpg` | Program, Boss | Armoured boss centre, ringed planet above, red boss bar | 0.283 / 0.0000 | Good |
| `garden_combat.jpg` | Silent Garden, Combat | Trooper with cyan shield, two orange-rimmed enemies mid-leap | 0.287 / 0.0000 | Good; the enemies are frozen mid-air (shadows show the jump) |
| `garden_boss.jpg` | Silent Garden, Boss | Boss small in a vine hall, boss bar left | 0.272 / 0.0000 | Weak: boss is tiny in frame; replace with a closer art shot |
| `rust_boon.jpg` | Rust, after clear | The CHOOSE A BOON panel with three cards (Common / Rare labels), Reroll | 0.134 / 0.0000 | Strong UI shot, shows rarity text plus colour |

Rejected: the path-choice shot (two flat yellow COMBAT doors, no depth, looks greybox). Capture again when the map/path presentation is polished, or use the HUD/summary screen instead.

Magenta check: all 8 renders measured 0.0000 magenta ratio and the PNGs were inspected; the transient pink silhouette seen in an earlier P6-03 PC render did not reproduce (elite rims are orange, the Trooper shield cyan).

Notes: the captures are editor renders at the PC tier. Quest-tier store screenshots should still be captured on device (`adb shell screencap` or Meta Developer Hub) so the listing matches what the player sees. The editor is not at the Mobile tier here, deliberately, for quality.

### Still needed (user / artist tasks)

| Asset | Shot description | Scene / room | Composition |
|---|---|---|---|
| Icon 1024 x 1024 | The samurai helmet or blade silhouette against a cyan/orange split, no text | n/a (art) | Centred emblem, readable at 64 px |
| Cover art 2560 x 1440 | Hero mid-slash against a boss, rim-lit orange, "RONIN 7" logo top left | Program sector, ringed planet | Rule of thirds, hero left, boss right, logo clear of the title-safe edges |
| Hero 3000 x 900 | Wide banner: the hero's silhouette on the left, three sector colours (rust, cyan, green) fading across | Montage | Logo centre-right, nothing important within 150 px of the edges |
| Square 1600 x 1600 | Cover composition re-cropped, logo bottom | n/a | |
| Steam capsules (all sizes above) | Cover art re-cut per size, logo legible at 462 x 174 | n/a | One master layout, text outside the crop |
| Library hero / logo | Hero 3840 x 1240 without text; logo on transparent | n/a | |
| Quest-tier screenshots (2-3) | Same rooms as above, captured on device | Rust combat, Program elite, boon panel | First-person, hands visible |
| Trailer (30 to 60 s) | Beats: 1) menu / Kessler line (2 s); 2) first slash and a perfect deflect with slow-mo (6 s); 3) Rust combat, weakness hit, posture break (6 s); 4) boon choice panel (4 s); 5) Program elite fight (6 s); 6) boss intro and phase change (8 s); 7) low-health heartbeat, near death, comeback (5 s); 8) Garden vista and run summary (4 s); 9) logo and platforms (3 s) | Real in-headset capture (Meta Developer Hub or SteamVR recorder), not editor renders | Show hands and the real HUD; no camera shake exists, so keep cuts, not motion, for energy |

Nothing licensed or bought: music in the trailer is the synthesized placeholder set; commission or license the A14.23 slots before using it in marketing.

## 5. Exit-step list (everything bigger than a one-liner)

1. Android stripping to Low, then gate + device smoke.
2. `forceRemoveInternetPermission: 1`; verify the APK manifest has no INTERNET.
3. Remove the eye-tracking permission and required feature (do not target Quest Pro / drop the eye-tracked foveation extension); verify with aapt.
4. Exclude the Unity-MCP and SignalR DLLs from player builds.
5. Enable Index, Vive (and KHR simple) interaction profiles for Standalone; verify in SteamVR.
6. Optional: Standalone to IL2CPP.
7. Version 1.0.0, tag `roguelike-1.0.0`, upload with `ovr-platform-util` (user action).
8. Device passes for every NEEDS-DEVICE row above, plus the final soak (5 runs per platform, 10 with heat and the Daily seed, read `runlog.jsonl`).
9. IARC questionnaire, privacy-policy URL, all art assets, trailer (user / artist).

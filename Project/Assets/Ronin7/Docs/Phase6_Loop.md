# Phase 6 — Connecting the Loop (Landing) + Art/Audio Pass

Phase 6 turns the two isolated pillars (space flight, on-foot samurai combat) into a single
playable loop, and lays the audio foundation for the art/audio pass.

```
Boot ─▶ SpaceFlight ──(fly slow into planet + hold grip)──▶ OnFoot
                ▲                                              │
                └──────────(clear zone, dwell on extraction)──┘
```

## What was added (code)

| Piece | File | Role |
|-------|------|------|
| `LandingRequested` event | `Core/GameMode.cs` | Published when the player commits to landing. |
| Ship pose getters | `Ship/ShipController.cs` | `ShipPosition` / `ShipRotation` for proximity tests. |
| Zone events moved to Core | `Core/ZoneEvents.cs` | `ZoneCompleted` / `ObjectiveUpdated` now live in Core so Flow/Audio can subscribe without a World dependency. |
| `ScreenFader` | `Player/ScreenFader.cs` | Code-built full-screen black fade on the head camera (VR comfort across loads). |
| `GameFlowManager` | `Flow/GameFlowManager.cs` (`Ronin7.Flow` asmdef) | **The missing subscriber.** Persistent singleton; on `LandingRequested`/`ZoneCompleted` it fades, `LoadSceneAsync`, and `SetMode`. |
| `LandingApproach` | `Ship/LandingApproach.cs` | Detects slow approach to a landable planet; hold grip to publish `LandingRequested`. |
| `AudioDirector` | `Audio/AudioDirector.cs` (`Ronin7.Audio` asmdef) | Persistent, event-driven SFX + per-mode ambience. All clips optional. |
| Boot scene builder | `Editor/XRRigBuilder.cs` → **Build Phase 6 Boot Scene** | Builds `Phase6_Boot.unity` with `GameState`+`GameFlowManager`+`AudioDirector`. Flight builder now also adds `LandingApproach` + dashboard prompt. |

### Geometry note (why landing measures against the virtual ship)
Flight uses "fixed cockpit, moving universe": the rig never moves; the world (under `Universe`)
is rendered as the inverse of the virtual ship pose. So a planet at universe-local position `P`
sits a distance `|P − shipPos|` from the player. `LandingApproach` therefore compares
`ShipController.ShipPosition` to each landable planet's position captured in universe-local
space at `Start` — **not** the camera transform.

## Required Editor steps (cannot be scripted)

1. **Open the project in Unity** and let it compile + generate `.meta` files for the new
   scripts, the new `Ronin7.Flow` and `Ronin7.Audio` assemblies, and folders.
2. **Build the scenes** (menu **Tools → Space Samurai**):
   - *Build Phase 6 Boot Scene* → `Phase6_Boot.unity`.
   - The flight + zone scenes already exist; only rebuild the flight scene if you want the
     `LandingApproach` baked in by the builder (see ⚠️ below).
3. **Build Profiles → Scene List** — add, in this order: `Phase6_Boot`, `Phase5_Flight`,
   `Phase4_Zone`. `LoadSceneAsync(name)` only resolves scenes that are in this list (critical
   on-device).
4. **Verify input wiring in the Inspector** (the known builder hazard):
   - On the flight scene's `Flight Controller` → `ShipController`: `throttleAxis` =
     Left Hand/Move, `steerAxis` = Right Hand/Turn.
   - On `Landing Approach` → `LandingApproach`: `landAction` = Right Hand/Select (grip).
   - If any show `None`/`{fileID: 0}`, re-assign using the known-good action fileIDs recorded
     in project memory (input asset GUID `d5a2441a867c2484c888f3631c932d48`).

> ⚠️ **Do not rebuild existing scenes just to fix wiring.** The builder re-nulls every
> `InputActionReference` on (re)build (asset-import timing). **Preferred path:** add the
> `LandingApproach` component to the *existing, already-wired* `Phase5_Flight` by hand in the
> Inspector (Add Component → Landing Approach), then wire its 4 fields — this preserves the
> known-good `ShipController` input refs. Never hand-edit a `.unity` file while it is open in
> Unity.

## Test (exit criteria)

Play from **`Phase6_Boot`**:
1. Boot → flight loads, mode = `SpaceFlight`, sticks fly the ship (matches Phase 5).
2. Fly slowly toward the near planet → dashboard prompt appears → hold grip → screen fades to
   black → on-foot zone loads, mode = `OnFoot`, you can walk and draw the sword.
3. Defeat all enemies + collect relics → extraction pad turns green → dwell on it →
   `ZoneCompleted` → fade → flight scene reloads, mode = `SpaceFlight`. **Loop closes.**
4. Watch the console: no duplicate `GameState`/`GameFlowManager`/`AudioDirector` after a full
   loop (singleton guards), and no stale events firing in the wrong scene.
5. Comfort: every transition is covered by the fade; the flight vignette still drives; no
   black-screen hang (the camera-wait cap fades back in regardless).

## Art / Audio pass — remaining work

**Audio (code is ready — just assign clips):** select the persistent `Game` object in
`Phase6_Boot`, and on `AudioDirector` assign clips for sword deflect/impact, player hit,
landing, extraction, and the flight/on-foot ambience loops. Drop `.wav`/`.ogg` files into
`Assets/Ronin7/Audio`. Unassigned clips stay silent, so partial audio is fine.

**Still to do (asset-dependent — best done in-editor with the assets in hand):**
- **Models**: replace the primitive cockpit, planets, asteroids, enemy capsule, and sword cube
  with real meshes. Recommended refactor: convert the `XRRigBuilder` primitive helpers
  (`BuildCockpit`, `BuildPlanet`, `BuildEnemy`, `BuildSword`) to instantiate **prefab fields**
  so swapping art is a slot assignment, not a code change.
- **Skybox + lighting**: assign a starfield skybox material; tune the directional `Sun`; add a
  subtle URP **Bloom** Volume — keep within the Quest budget in `SETUP_Phase0.md` (HDR off,
  MSAA, no extra-light shadows).
- **VFX (Unity particle systems, no external assets)**: sparks on `SwordDeflected` at
  `e.Point`; ship thruster trail; landing dust; extraction-pad glow.
- **Engine audio coupling**: optionally drive a looping engine source's pitch/volume from
  `ShipController.CurrentSpeed` (kept out of `AudioDirector` to avoid an Audio→Ship assembly
  dependency; add it on the ship or via a small bridge component).

Recommended sequence: prove the loop fun with primitives first, then layer art so polish lands
on a design that won't churn.

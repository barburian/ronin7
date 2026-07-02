# Sun → Navigation & Strategy — Design Spec

> Phase 4 of the Space Samurai improvement plan. Turns the currently-decorative sun
> (`Ship/SunLightAimer.cs` re-aims the key light; `Ship/PlanetOrbit.cs` creeps the planets)
> into a **gameplay** element along three pillars chosen with the user: **orientation**,
> **gravity-assist slingshot**, and **glare / sun-side ambush**.

## Design goals & constraints
- **Comfort first (VR).** No new nausea sources. Glare ramps gently (no strobe); the slingshot's
  acceleration stays inside the band the existing `ComfortVignette` already mitigates. Reuse
  `Player/ScreenFader.cs` + `Ship/ComfortVignette.cs` infra rather than inventing new overlays.
- **Moving-universe model.** The rig is fixed at world origin; the *universe* transform moves under it
  (`ShipController`). The sun is a child of that universe root (see `SunLightAimer`/`PlanetOrbit`).
  All sun↔ship geometry must be computed in the universe frame, and any velocity boost applies to the
  **universe-frame motion**, never the rig.
- **Additive & safe.** The sun systems must be *off by default* (no assigned sun = no behavior change)
  and must not destabilize the flight loop. Flight-loop integration is a small, explicit hook, not a
  rewrite.

## Pillar 1 — Orientation (the sun as a compass)
The sun is a persistent, always-bright landmark. `SunCompass.BearingDeg(forward, toSun, up)` returns the
signed heading (−180..180°) from the ship's nose to the sun, and an elevation. Consumers:
- HUD/objective arrow (`Ship/ObjectiveArrowController.cs`, `Ship/PlanetTargetMarker.cs`) can phrase
  objectives sun-relative ("objective is 30° starboard of the sun") and the galaxy map can show
  sun-side routes.
- Gives the player a stable mental map in an otherwise feature-sparse starfield.

## Pillar 2 — Gravity-assist slingshot
A `SunGravityWell` zone centered on the sun visual, with three radii (universe units):
`outerRadius` (assist begins) > `dangerRadius` (heat begins) > `coreRadius` (max heat / lethal).
- **Boost:** `BoostFactor(distance)` ramps 0→`maxBoost` as the ship crosses from `outerRadius` inward to
  `dangerRadius` — diving toward the sun and whipping past grants extra speed/turn authority (the
  "slingshot"). Applied to the universe-frame velocity via a ShipController hook.
- **Risk:** `HeatPerSecond(distance)` ramps 0→`maxHeat` from `dangerRadius` inward to `coreRadius`;
  lingering too close cooks the hull (drives the existing player `Health`/damage path). High risk, high
  reward — the slingshot is fastest but skirts the danger band.
- **Tactics:** outrun a pursuer by slingshotting; or bait an enemy into the danger band.

## Pillar 3 — Glare / sun-side ambush
`SunGlare.Intensity(viewForward, toSun, startAngleDeg, fullAngleDeg)` returns 0..1 from the angle between
the look direction and the sun: 0 when the sun is outside `startAngleDeg`, ramping to 1 by `fullAngleDeg`
(looking straight at it). Drives a soft screen brighten/wash via the screen-overlay infra (comfort-safe,
no strobe).
- **For the player:** charging sun-ward washes out your view and your crosshair read — discouraged.
- **Symmetric advantage:** enemies attacking from the sun-side are harder to track; the AI can prefer a
  sun-ward approach for a brief targeting edge (a future `EnemyShip` hook). The counter-play, classic
  dogfighting: **keep the sun at your back.** This makes approach *angle* a tactical choice.

## Components (Phase 4 implementation)
Under `Assets/SpaceSamurai/Scripts/Ship/SunNavigation/`:
- `SunGravityWell.cs` — MonoBehaviour on the sun; pure static `BoostFactor(dist, outer, danger, maxBoost)`
  and `HeatPerSecond(dist, danger, core, maxHeat)`; exposes computed boost/heat for a world position.
  **Does not** modify `ShipController` directly — it provides values + a clear hook.
- `SunGlare.cs` — pure static `Intensity(...)` + a thin controller that drives the overlay.
- `SunCompass.cs` — pure static `BearingDeg(...)` / elevation helpers.

All three pure cores get EditMode tests (curve monotonicity, clamping at the radii/angles, zero outside
range, symmetry). These are objective regardless of final tuning.

## Integration & hand-off (in-headset pass — NOT done headless)
The math cores + glare overlay are implemented and unit-tested now. The following require an in-headset
tuning/playtest pass and are documented hooks, deliberately not blind-wired into the flight loop:
1. **Boost → flight:** add a single additive speed/turn multiplier hook in `ShipController` fed by
   `SunGravityWell` (the universe-frame velocity, per the moving-universe rule). Tune `maxBoost` so peak
   acceleration stays within the vignette-mitigated comfort band.
2. **Heat → health:** route `HeatPerSecond` into the existing player damage path (mirror
   `Ship/AsteroidHazard.cs`'s cooldown-damage pattern).
3. **Glare overlay:** bind `SunGlare.Intensity` to the screen-overlay material; tune `startAngleDeg`/
   `fullAngleDeg` and max brightness in-headset for comfort.
4. **Scene wiring:** place `SunGravityWell` on the Galaxy-1 sun; set radii relative to `PlanetOrbit`'s
   850-unit orbit; assign the sun transform to `SunCompass` consumers.
5. **AI sun-side approach** (`EnemyShip`): optional follow-up so enemies exploit glare angles.

## Test/verification plan
- **EditMode (now):** the three pure cores (boost/heat/glare/bearing curves) — included this phase.
- **In-headset (hand-off):** comfort of the slingshot acceleration and glare ramp; readability of the
  sun-at-your-back tactic; objective phrasing clarity. Tune the serialized radii/angles live.

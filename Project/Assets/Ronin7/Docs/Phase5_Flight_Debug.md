# Phase 5 Flight — Frozen View / No Head Tracking — Debug Report

Date: 2026-05-21
Scene: `Assets/Ronin7/Scenes/Phase5_Flight.unity`
Unity 6000.3.2f1 · OpenXR 1.15.1 · XRI 3.2.1 · Meta OpenXR 2.2.0 · XR Hands 1.6.1

---

## TL;DR (Root Cause)

The head camera in Phase5_Flight is driven by a custom `PoseFollower` component (this project does
**not** use XRI's `TrackedPoseDriver`; see `PoseFollower.cs`). In the Phase5 scene asset, the
PoseFollower on the **Main Camera** had **both of its input action references unassigned**:

```yaml
positionAction: {fileID: 0}
rotationAction: {fileID: 0}
```

With no `positionAction`/`rotationAction`, `PoseFollower.ApplyPose()` reads nothing and never writes
to the transform, so the camera stays pinned at its local origin (0,0,0 / identity) every frame.
Result: the view is frozen and head movement does nothing — exactly the reported symptoms.

This was not isolated to the camera. **Every** InputActionReference in the scene was null
(`{fileID: 0}`): both hand PoseFollowers, both hand Grabbers' grip actions, and the ShipController's
throttle/steer axes. This pattern means the input-action references simply failed to serialize when
`XRRigBuilder.BuildFlightScene()` generated the scene (the `.inputactions` sub-assets were not
resolved by `FindRef`, so every binding came back null). The working scenes (e.g. Phase2_Combat)
have all of these references correctly populated.

**Fix:** Wrote the correct action-reference fileIDs (copied from the working Phase2_Combat scene,
all pointing at the input-actions asset, guid `d5a2441a867c2484c888f3631c932d48`) into all 10
unassigned reference fields in the Phase5 scene YAML.

---

## Investigation Log

### 1. Scripts reviewed
- `Ship/ShipController.cs` — "fixed cockpit, moving universe" model. It only moves the assigned
  `universe` transform (`universe.SetPositionAndRotation(...)`), and bails out immediately if
  `universe == null`. It NEVER touches the XR rig or the camera. So it cannot, by itself, freeze the
  camera or break head tracking. It reads two InputActionReferences (`throttleAxis`, `steerAxis`).
- `Player/PoseFollower.cs` — the project's hand-rolled replacement for `TrackedPoseDriver`. On
  `Update()` and `onBeforeRender`, it reads `positionAction`/`rotationAction` and writes them to the
  transform (local space when `useLocalSpace`). **If the actions are null, it does nothing** — the
  transform is never updated. This is the head/camera driver.
- `Player/VRRig.cs` — passive reference holder; not involved in tracking.
- `Core/FollowTarget.cs` — copies a target transform in LateUpdate; **not present/used** in the
  Phase5 scene (the flight design keeps the rig stationary at the origin).
- `Editor/XRRigBuilder.cs` — the generator. `BuildFlightScene()` (priority 5) builds the rig via
  `BuildRig(refs, addLocomotion: false)`, then a `Cockpit`, a `Universe` (planets/asteroids), and a
  `Flight Controller` with a `ShipController`. `AddPoseDriver()` wires PoseFollower actions via
  `FindRef(refs, map, "Position"/"Rotation")`. If `FindRef` returns null (input asset not resolved),
  the references serialize as `{fileID: 0}` — which is exactly what we see.

### 2. Camera / rig hierarchy in Phase5_Flight (confirmed from YAML)
- `XR Origin (Space Samurai)` (`&1356970446`) with `XROrigin` component (`&1356970447`).
- `Camera Offset` (`&...830147241`).
- `Main Camera` (`&741643485`), tag `MainCamera`, components: Transform, Camera, AudioListener,
  PoseFollower (`&741643487`). Camera `m_TargetEye: 3` (Both eyes) — i.e. it IS the XR stereo
  camera, so OpenXR stereo presentation is fine. The freeze is purely the un-driven transform.
- `Left Hand Controller` (`&334834657`) and `Right Hand Controller` (`&765614993`), each with a
  PoseFollower + Grabber + HandVelocityTracker.
- `Flight Controller` (`&171435897`) with `ShipController` (`&171435898`); its `universe` ref IS set
  (`{fileID: 1844870622}` = the `Universe` root), only the input axes were null.

The hierarchy itself is correct and matches the working scenes. The only defect was the missing
input-action references.

### 3. Evidence — Phase5 (BROKEN) vs Phase2 (WORKING)

Phase5_Flight.unity, all action refs were null:
```
613:  throttleAxis: {fileID: 0}      # ShipController
614:  steerAxis:    {fileID: 0}      # ShipController
1649: gripAction:   {fileID: 0}      # Left Hand Grabber
1678: positionAction:{fileID: 0}     # Left Hand PoseFollower
1679: rotationAction:{fileID: 0}
3857: positionAction:{fileID: 0}     # Main Camera (HEAD) PoseFollower  <-- the reported bug
3858: rotationAction:{fileID: 0}
3967: gripAction:   {fileID: 0}      # Right Hand Grabber
3996: positionAction:{fileID: 0}     # Right Hand PoseFollower
3997: rotationAction:{fileID: 0}
```

Phase2_Combat.unity (works), the same PoseFollowers are populated, e.g. the Main Camera head:
```
362: positionAction: {fileID: 3581843739080334339, guid: d5a2441a867c2484c888f3631c932d48, type: 3}
363: rotationAction: {fileID: -6944014969908903718, guid: d5a2441a867c2484c888f3631c932d48, type: 3}
```

### 4. Action fileID map (from the working Phase2_Combat scene)
All reference the input-actions asset `guid: d5a2441a867c2484c888f3631c932d48`:

| Action            | fileID |
|-------------------|--------|
| Head / Position   | `3581843739080334339` |
| Head / Rotation   | `-6944014969908903718` |
| Left Hand / Position | `2554422074516807192` |
| Left Hand / Rotation | `6625066478895225713` |
| Right Hand / Position | `957495594928833404` |
| Right Hand / Rotation | `8366341614184261216` |
| Left Hand / Select (grip)  | `7134430119224436027` |
| Right Hand / Select (grip) | `-4634371063929252884` |
| Left Hand / Move (throttle) | `6007721145605796841` |
| Right Hand / Turn (steer)   | `8513782027740056310` |

Owner identification was verified by GameObject context (AudioListener sits on the Head camera;
`leftHand: 1` / `leftHand: 0` flags on the Grabbers; GameObject names `Left/Right Hand Controller`).

### 5. XR Plug-in Management / OpenXR settings check
`Assets/XR/Settings/OpenXR Package Settings.asset`:
- Contrary to the original concern, the **Android** OpenXR settings object (`m_Name: Android`,
  `&-1169634617618473745`) has a fully populated `features:` list (~33 entries), not `features: []`.
  So head tracking is not blocked by an empty Android feature set in the current file.
- NOTE (secondary, build-on-device only): the controller interaction profiles for **Android** are
  disabled — `OculusTouchControllerProfile Android` (`m_enabled: 0`) and
  `MetaQuestTouchPlusControllerProfile Android` (`m_enabled: 0`). On the Standalone group the same
  profiles are also disabled. This does NOT affect HMD head pose (head pose comes from the runtime
  regardless), but on an actual Quest build the **controller** pose/buttons may not bind without an
  enabled Touch controller profile. This is unrelated to the frozen-view bug but worth fixing before
  shipping to-device. See "Editor steps" below.

---

## Changes Made

File: `Assets/Ronin7/Scenes/Phase5_Flight.unity` (10 fields, 6 edits)

1. **Main Camera (Head) PoseFollower** `&741643487`
   - before: `positionAction: {fileID: 0}` / `rotationAction: {fileID: 0}`
   - after:  `positionAction: {fileID: 3581843739080334339, guid: d5a2441a867c2484c888f3631c932d48, type: 3}`
             `rotationAction: {fileID: -6944014969908903718, guid: d5a2441a867c2484c888f3631c932d48, type: 3}`
   - rationale: this is THE fix for the frozen view / no head tracking. Gives PoseFollower the HMD
     center-eye position/rotation actions to drive the camera transform.

2. **Left Hand PoseFollower** `&334834661`
   - after: `positionAction: 2554422074516807192`, `rotationAction: 6625066478895225713`
   - rationale: restores left-hand tracking.

3. **Left Hand Grabber gripAction** `&334834659` (`leftHand: 1`)
   - after: `gripAction: 7134430119224436027`
   - rationale: restores left-hand grab/Select.

4. **Right Hand Grabber gripAction** `&765614995` (`leftHand: 0`)
   - after: `gripAction: -4634371063929252884`
   - rationale: restores right-hand grab/Select.

5. **Right Hand PoseFollower** `&765614997`
   - after: `positionAction: 957495594928833404`, `rotationAction: 8366341614184261216`
   - rationale: restores right-hand tracking.

6. **ShipController** `&171435898`
   - after: `throttleAxis: 6007721145605796841` (Left/Move),
            `steerAxis: 8513782027740056310` (Right/Turn)
   - rationale: restores flight stick controls (throttle/roll + pitch/yaw). `universe` ref was
     already set, so flight rendering itself was fine — only the inputs were missing.

All edits only changed the `fileID`/`guid` of existing reference fields; no structural YAML changes,
no added/removed objects. The guid `d5a2441a867c2484c888f3631c932d48` is the project input-actions
asset, identical across all working scenes.

Post-edit verification (grep) confirmed zero remaining `{fileID: 0}` action references in the scene.

---

## Editor-UI steps the user should perform

1. **Reimport / let Unity pick up the scene edit.** Because the scene file was edited on disk while
   it may be open in the Editor, open Unity, and if Phase5_Flight was open, click "Reload" if
   prompted (or just reopen the scene). Verify in the Inspector that:
   - Main Camera → PoseFollower → Position Action / Rotation Action show **Head/Position** and
     **Head/Rotation** (not "None").
   - Left/Right Hand Controller → PoseFollower → actions show the matching hand Position/Rotation.
   - Flight Controller → ShipController → Throttle Axis = Left Hand/Move, Steer Axis = Right
     Hand/Turn.

2. **(Recommended, for the actual Quest build only — not needed for the frozen-view fix):**
   Project Settings → XR Plug-in Management → OpenXR → **Android** tab → under "Interaction Profiles"
   enable **Meta Quest Touch Plus Controller Profile** (and/or **Oculus Touch Controller Profile**).
   Do the same on the **Standalone** tab if you test over Quest Link. Without an enabled controller
   profile the HMD head pose still works, but controller pose/buttons may not bind on-device.

3. If you ever rebuild this scene from `Tools > Space Samurai > Build Phase 5 Flight Scene`, make
   sure `Assets/Ronin7/Settings/Ronin7Input.inputactions` is fully imported first
   (the builder's `FindRef` returning null is what produced the original null references). Watch the
   Console for `[Space Samurai] Input action not found:` warnings — if you see those, the action
   refs will again serialize as null.

---

## Verification checklist (in-headset / Play mode)

- [ ] Enter Play with the headset on. The view should now respond to head movement (look around;
      planets/asteroids/cockpit should shift correctly with your head). View is no longer frozen.
- [ ] The cockpit (floor, dashboard, rails, canopy) stays locked to your head/world frame as a
      stable comfort anchor.
- [ ] Both hand spheres track your controllers' position and rotation.
- [ ] Left stick: throttle forward/back + roll. Right stick: pitch + yaw. The universe should move
      around you (you fly through the asteroid field).
- [ ] Grab works on each hand (grip/Select) if you add anything grabbable.
- [ ] No Console errors about missing/unbound input actions.

---

# Phase 5 Flight Movement

Date: 2026-05-21
Scope: Trace + harden the ship movement (input -> motion chain) and add VR comfort options.
Author note: validated by static analysis only. Unity was NOT run; no runtime/Play-mode verification.

## 1. Input -> movement chain trace (CONFIRMED CORRECT end to end)

1. **Bindings** (`Settings/Ronin7Input.inputactions`):
   - `Left Hand/Move` (Value, Vector2) -> `<XRController>{LeftHand}/primary2DAxis`.
   - `Right Hand/Turn` (Value, Vector2) -> `<XRController>{RightHand}/primary2DAxis`.
   - Both confirmed by grepping the asset (paths + Vector2 expectedControlType).
2. **Wiring** (`Editor/XRRigBuilder.BuildFlightScene`): `ShipController.throttleAxis` <- Left Hand/Move,
   `steerAxis` <- Right Hand/Turn, `universe` <- the `Universe` root. Rig built with
   `addLocomotion: false`, so NO `ContinuousLocomotion` competes for the same sticks — correct: the
   cockpit/rig is stationary by design, only the universe moves.
3. **Enable**: `ShipController.OnEnable()` calls `.action.Enable()` on both refs and sets
   `GameMode.SpaceFlight`. `OnDisable()` disables them. Correct lifecycle.
4. **Read -> integrate** (`Update`): reads both sticks, computes a throttle target, eases
   `CurrentSpeed` toward it, integrates `shipRot` (pitch/yaw/roll) and `shipPos` (forward * speed),
   then writes `universe` as the INVERSE pose (`inv * -shipPos`, `inv`). This is the correct
   moving-universe transform: world rendered relative to a stationary player.

Stick-axis mapping: Left Y = throttle, Left X = roll; Right Y = pitch, Right X = yaw. Matches the
documented design.

### Bugs found
- **No true blocking bug.** The chain is correct (the earlier null-reference defect was already fixed
  in the section above). The runtime BLOCKER is environmental, not code — see "Required Editor step".
- **Comfort/quality issues (addressed, see below):**
  - Deadzone was a hard cutoff with NO rescale: input jumped discontinuously from 0 to ~`deadzone`
    magnitude at the edge of the deadzone (a small but real "snap" in control + world motion).
  - No response curve: linear sticks make fine rotation control near centre hard, encouraging
    over-rotation (a comfort hazard).
  - Steering applied raw each frame (no ramp): a stick flick started/stopped rotation abruptly, which
    is provocative in VR.
  - No comfort tunnelling / snap-rotation option at all, despite continuous yaw being the single
    biggest nausea trigger in 6DOF flight.

## 2. Changes made

### File: `Scripts/Ship/ShipController.cs` (rewritten, same model preserved)

- **Deadzone rescale + response curve** (`Read`):
  - before: `return v.magnitude < deadzone ? Vector2.zero : v;`
  - after: ignore below `deadzone`, then `InverseLerp(deadzone,1,mag)` rescales so output starts at 0
    at the deadzone edge (no jump), then `Pow(scaled, responseExponent)` applies a gentle curve.
  - rationale: removes the control discontinuity and gives precise, comfortable near-centre control.
- **Smoothed steering rates** (`pitchRate/yawRate/rollRate` via `MoveTowards`, new `steerSmoothing`):
  - before: `pitch/yaw/roll` computed directly from stick * speed * dt each frame.
  - after: each axis ramps toward its target rate, so rotation eases in/out instead of snapping.
  - rationale: abrupt angular velocity changes are a known nausea trigger; ramping is comfier and
    still responsive at the default `steerSmoothing = 6`.
- **Optional SNAP yaw** (`useSnapYaw`, `snapYawDegrees`, `snapYawThreshold`, `StepYaw`):
  - new. When enabled, yaw becomes discrete steps (re-armed at stick centre) instead of continuous.
    Pitch/roll stay continuous (less provocative, and needed for flight feel).
  - rationale: continuous yaw is the worst offender; snap yaw mirrors the proven on-foot
    `ContinuousLocomotion.HandleSnapTurn` pattern. Default OFF so the arcade feel is intact, but
    one toggle makes it comfortable for sensitive players.
- **Gentler defaults**: `acceleration 20 -> 12` (smoother speed ramp), `pitchSpeed 30 -> 25`,
  `yawSpeed 30 -> 22` (yaw is most provocative, so lowest), `rollSpeed 40 -> 50` (roll is the least
  nauseating rotation and helps banking feel). Added `[Range]`/tooltips to keep values sane.
- **Comfort vignette hook** (`useComfortVignette`, `vignetteRotationRef`, `vignetteAccelRef`,
  `EnsureVignette`, drive in `Update`): auto-creates a `ComfortVignette` on `Camera.main` at `Start`
  and feeds it `max(rotationIntensity, accelerationIntensity)` each frame.
  - rationale: tunnelling that closes during rotation/acceleration is the most effective, lowest-risk
    comfort measure. Built at runtime so NO scene/prefab edit is required.

### File: `Scripts/Ship/ComfortVignette.cs` (NEW)

- A self-contained tunnelling vignette: a code-built transparent annulus (ring) parented to the
  camera, ~0.5 m in front of the eyes, on an unlit transparent material that draws last
  (`RenderQueue.Overlay`, `ZWrite 0`, `ZTest Always`) so cockpit/world cannot poke through.
- `SetIntensity(0..1)` requests strength; the aperture eases (`MoveTowards`, `responsiveness`) toward
  it so the tunnel never pops. Intensity 0 = fully open (renderer disabled), 1 = tight tunnel
  (`minAperture`, default 0.45 of view).
- No new asmdef references (uses only UnityEngine, lives in `Ronin7.Ship`). Prefers the URP
  Unlit shader, falls back to built-in `Unlit/Color`.

### NOT done (and why)
- **No scene/prefab `.unity` edits.** The vignette is created at runtime, so `Phase5_Flight.unity`
  is untouched (avoids YAML-corruption risk and keeps the existing wiring intact).
- **Speed HUD**: deliberately deferred. `ShipController.CurrentSpeed` is already a public getter, so a
  HUD is trivial to add later, but a world-space cockpit canvas is a non-trivial scene edit and the
  task says HUD is a nice-to-have only if low risk. Movement feel was prioritized instead.

## 3. Comfort decisions (reasoning)
- Stable cockpit frame is the primary anchor (already in the design) — kept untouched.
- Lowest rate on yaw, highest on roll: matches how the vestibular system tolerates each rotation axis.
- Vignette ON by default; snap yaw OFF by default (keeps arcade feel, one toggle for sensitive users).
- Acceleration lowered so speed changes (a translational provocation) are gradual.
- All thresholds/rates are serialized with `[Range]` clamps so a designer cannot set nauseating values
  by accident.

## 4. REQUIRED Editor step (cannot be fixed from files) — controller input will not flow without it
In **Project Settings -> XR Plug-in Management -> OpenXR -> Interaction Profiles**, ENABLE a
controller profile (**Meta Quest Touch Plus Controller Profile** and/or **Oculus Touch Controller
Profile**) on the **Android** tab (and **Standalone** if testing over Quest Link). They are currently
`m_enabled: 0` in `Assets/XR/Settings/OpenXR Package Settings.asset`. HMD head pose works without
them, but `<XRController>{LeftHand/RightHand}/primary2DAxis` (the flight sticks) will NOT bind until a
controller interaction profile is enabled. This is the gate on ship movement responding at all.

## 5. Inspector fields the user can tune (Flight Controller -> ShipController)
- Input: `deadzone` (0.15), `responseExponent` (2 = gentle).
- Thrust: `maxSpeed` (35), `reverseFraction` (0.4), `acceleration` (12 = smooth).
- Steering: `pitchSpeed` (25), `yawSpeed` (22), `rollSpeed` (50), `invertPitch` (true),
  `steerSmoothing` (6 = ease in/out).
- Comfort/Yaw: `useSnapYaw` (false), `snapYawDegrees` (30), `snapYawThreshold` (0.7).
- Comfort/Vignette: `useComfortVignette` (true), `vignetteRotationRef` (35), `vignetteAccelRef` (8).
- Vignette tuning (ComfortVignette, auto-created child of Main Camera at runtime): `distance` (0.5),
  `minAperture` (0.45), `responsiveness` (8), `color` (black).

## 6. In-headset test/verification checklist
- [ ] FIRST do the Required Editor step above, then enter Play with the headset on.
- [ ] Left stick forward/back: world recedes/approaches smoothly; speed eases (no instant jerk).
- [ ] Left stick left/right: ship rolls (banks); roll eases in/out.
- [ ] Right stick: pitch (up/down) and yaw (left/right); rotation eases, not snappy.
- [ ] During any sustained rotation or hard throttle change, the screen edges darken (tunnel) and
      re-open when you stop. Tune `vignetteRotationRef`/`vignetteAccelRef` if too eager/too weak.
- [ ] Toggle `useSnapYaw` ON: yaw becomes discrete ~30 deg steps, re-arming at stick centre; brief
      vignette flash on each step. Confirm comfier for sensitive players.
- [ ] Near deadzone edge: control eases in from zero (no sudden snap into motion).
- [ ] Head tracking + cockpit anchor still rock-solid (unchanged by this work).
- [ ] No Console errors about missing input actions or null camera/vignette.

---

# Phase 5 Stick Input — Root Cause

Date: 2026-05-21
Symptom (precise): `[Ship] OnEnable` reports both references non-null, both actions enabled,
universe assigned — yet pushing the thumbsticks logs NO `[Ship] raw throttle=…` lines. The enabled
action reads ZERO while the stick is pushed. The SAME `Left Hand/Move` and `Right Hand/Turn` actions
read fine in `Phase2_Combat` (joystick locomotion works).

## Hypotheses tested + evidence

**(a) Scene running stale in-memory copy, not reloaded from disk — CONFIRMED as the trigger.**
On disk, `Phase5_Flight.unity` ShipController already references the correct sub-asset actions:
```
4159: throttleAxis: {fileID: 6007721145605796841, guid: d5a2441a…, type: 3}   # Left Hand/Move
4160: steerAxis:    {fileID: 8513782027740056310, guid: d5a2441a…, type: 3}   # Right Hand/Turn
```
These are the IDENTICAL fileIDs `ContinuousLocomotion` uses in `Phase2_Combat` (lines 1176–1177):
```
1176: moveAction: {fileID: 6007721145605796841, …}
1177: turnAction: {fileID: 8513782027740056310, …}
```
Both scripts read the same way (`action.Enable()` then `action.ReadValue<Vector2>()`), so the
on-disk Phase5 ShipController would read the sticks exactly as Phase2 does. Since runtime shows it
reading zero, the running scene is NOT the on-disk version — Unity kept the pre-edit in-memory copy
(a recompile does not reload an open scene).

**(b) MIXED embedded + sub-asset refs => two InputActionAsset instances => enabled ≠ read — CONFIRMED
as the underlying defect.** The Phase5 scene is genuinely mixed on disk:
- ShipController → proper sub-asset refs (`type: 3`).
- Head Main Camera PoseFollower (line 12245) → LOCAL fileIDs `510784604` / `1698033729`, which point
  at EMBEDDED `InputActionReference` MonoBehaviours (`m_GameObject: {fileID: 0}`, script guid
  `fc1515ab…`, e.g. `&510784604` "Head/Position", `m_ActionId: a1000000-…-0101`,
  `m_Asset: {fileID: -944628639613478452, guid: d5a2441a…}`).
- Other PoseFollowers/Grabbers (lines 1389, 1418–1419, 9726, 9755–9756) → `{fileID: 0}` (null).

`Phase2_Combat` by contrast has ZERO embedded `InputActionReference` MonoBehaviours — every field is
a clean sub-asset ref (`type: 3`). That is the real difference between the scenes. Embedded refs and
sub-asset refs can resolve to different `InputActionAsset` instances; enabling/reading via one
instance does not affect the other. The head (embedded) and ShipController (sub-asset) ended up on
different instances, so ShipController's enabled action was bound on an instance that the live
devices were not feeding — hence reads zero. This also reconciles the contradiction "embedded HMD ref
works but the controller action reads zero": they are simply two different asset instances, each
enabled independently, and only the embedded one was the live/bound instance in the running scene.

**(c) Phase2 has a component Phase5 lacks (InputActionManager / wholesale enable) — RULED OUT.**
Neither scene contains `InputActionManager`, `PlayerInput`, or `InputSystemUIInputModule`. Both rely
on each component calling `.action.Enable()` itself. Phase2 works this way, so the pattern is sound.

**(d) Single-action `.Enable()` not resolving controller bindings — RULED OUT as a binding problem.**
`ContinuousLocomotion` enables the single action and reads it successfully in Phase2, so single-action
enable does bind `<XRController>{…}/primary2DAxis`. The failure is instance identity (b), not binding
resolution. (The new fix enables the whole owning asset anyway, which is strictly safer.)

**(e) Project-wide `InputSystem_Actions` / control schemes / binding masks — RULED OUT.**
`Ronin7Input.inputactions` has `"controlSchemes": []` (no schemes ⇒ no binding mask). The
project-wide asset binds `Player/*`,`UI/*`, not these maps. No interference.

## Root cause

The Phase5 scene is a MIXED bag of serialized input references (embedded `InputActionReference`
MonoBehaviours for the head, proper sub-asset refs for ShipController, and some null fields). Embedded
vs sub-asset references can resolve to DIFFERENT `InputActionAsset` instances. ShipController enabled
and read its action on an instance that was not the live/bound one, so `ReadValue<Vector2>()` returned
zero even though the action was "enabled" and the stick was pushed. The on-disk ShipController wiring
is actually correct — the running scene was the stale, never-reloaded in-memory copy, which made the
mixed-instance defect visible.

## Change(s)

File: `Assets/Ronin7/Scripts/Ship/ShipController.cs` (CODE fix — works regardless of how the
refs are serialized, so it survives even if the scene keeps embedded/null refs).

- Added resolved live handles `private InputAction throttleResolved, steerResolved;` (read these,
  never the raw `InputActionReference`).
- New `Resolve(InputActionReference, mapName, actionName)` (called from `OnEnable`):
  - Takes `reference.action`, then re-fetches the CANONICAL instance from its owning
    `InputActionAsset` via `asset.FindAction(action.id)` and enables the WHOLE asset
    (`asset.Enable()`). This guarantees the handle we read is the same asset instance that is
    enabled and bound to live devices — eliminating the enabled-≠-read mismatch.
  - Fallback: if the reference is null/broken, locate the action by `FindActionMap(mapName)
    .FindAction(actionName)` on `reference.asset` and enable that asset (logs a warning). If even
    that fails, logs a clear error naming the fields to assign.
  - before: `OnEnable` did `throttleAxis?.action?.Enable(); steerAxis?.action?.Enable();` and
    `Update`/`Read` called `throttleAxis.action.ReadValue<…>()` directly off the serialized ref.
  - after: `OnEnable` resolves+enables via `Resolve(...)`; `Update` and `Read(InputAction)` read the
    resolved handles. `OnDisable` disables and clears the resolved handles. `Read` signature changed
    from `Read(InputActionReference)` to `Read(InputAction)`.
- Why: reading the canonical asset instance and enabling the whole asset makes stick input flow no
  matter whether the scene serialized the refs as embedded copies, sub-asset refs, or a mix — so the
  bug cannot recur from scene-serialization drift, and no `.unity` edit is required.
- Diagnostics retained but improved: `OnEnable` now also logs the resolved action's `actionMap.name`
  and enabled state; the per-frame `[Ship] raw throttle=…` log now reads the resolved handles.

No `.unity` file was edited (the open scene would clobber any disk edit, and the code fix removes the
need). No new asmdef references; uses only `UnityEngine.InputSystem`.

## Inspector steps (do in the Editor — required to clear the stale in-memory scene)

1. In Unity, with `Phase5_Flight` the active scene, **reload it from disk** so the in-memory mixed
   copy is discarded and the recompiled `ShipController` is used: `File > Open Scene` →
   `Assets/Ronin7/Scenes/Phase5_Flight.unity` (or right-click the scene in Project → "Reimport"
   then reopen). A bare recompile is NOT enough — the scene must be reopened.
2. Let scripts recompile (the new `ShipController` will appear). No field changes are needed: the fix
   resolves input itself. Optionally verify `Flight Controller → ShipController → Throttle Axis = Left
   Hand/Move`, `Steer Axis = Right Hand/Turn` (already correct on disk).
3. (Still recommended for an on-device build, not for Link testing of input) enable the Meta Quest
   Touch Plus / Oculus Touch interaction profiles on the Android and Standalone OpenXR tabs — see the
   earlier section.

## Verification checklist

- [ ] Reopen `Phase5_Flight`, enter Play with the headset on (Quest Link, Standalone OpenXR, Meta
      Quest Touch Plus profile enabled).
- [ ] Console shows `[Ship] OnEnable … throttleResolved=True throttleEnabled=True throttleMap=Left
      Hand steerResolved=True steerEnabled=True steerMap=Right Hand universe=True`.
- [ ] Push either thumbstick → Console logs `[Ship] raw throttle=(x,y) steer=(x,y)` with non-zero
      values. (This is the line that was previously missing — its appearance confirms the fix.)
- [ ] Left stick forward/back = throttle (world recedes/approaches); left stick X = roll. Right stick
      Y = pitch, X = yaw. The universe moves around you.
- [ ] No `[Ship] Could not resolve input action …` error and no "recovered by name" warning (a
      warning would mean the serialized ref is broken but the fallback saved it — still flies).
- [ ] Once confirmed in-headset, remove the two TEMP DIAGNOSTIC `Debug.Log` blocks in
      `ShipController.OnEnable` and `Update`.

---

# Controllers Inert — Session-Wide Input Loss

Date: 2026-05-21
Symptom (precise, project-wide — NOT a scene/code bug): In EVERY scene this session (incl.
`Phase2_Combat`, which worked before, and `Phase5_Flight`), the controllers deliver **no input at
all** — neither thumbstick NOR pose. HMD head pose is fine. Hands sit at the origin (PoseFollower
gets no pose), thumbstick locomotion/flight does nothing. ShipController diagnostics: Move/Turn
actions resolve, are enabled, on the correct maps, but bind to **0 controls** (`thrControls=0`) and
`ReadValue` returns (0,0). In the **Input Debugger**, two `MetaQuestTouchPlusControllerOpenXR`
devices are listed but show **NO usages (no LeftHand / RightHand)** and no live thumbstick control.
Console at play start: "This OpenXR runtime doesn't support XR_META_boundary_visibility … disabled"
and "XR: Error setting active audio output driver. Falling back to default."

## THE ROOT CAUSE (single sentence)

The OpenXR controller devices are coming up **without their handedness usages (LeftHand /
RightHand)**, so every binding in `Ronin7Input.inputactions` — which is written as
`<XRController>{LeftHand}/…` and `{RightHand}/…` — matches **zero controls** (`thrControls=0`). The
`{LeftHand}`/`{RightHand}` braces are **usage filters**; with no usage on the device, NOTHING binds:
not the thumbstick, not `devicePosition`/`deviceRotation` (hence hands at origin). The HMD has no
handedness requirement, so head pose still works. The trigger is the **interaction-profile state**
that got changed this session: **three** controller profiles are now enabled at once on the
Standalone group, which over Quest Link produces exactly this "device present, no usage, inert"
state when the runtime's active profile and Unity's enabled-profile set don't cleanly agree.

## Evidence from the config files

### Interaction profiles — Standalone group (`Assets/XR/Settings/OpenXR Package Settings.asset`)
The OLD note in this doc ("profiles are m_enabled: 0") is now STALE. Current Standalone state:

| Profile (Standalone)                                | fileID                  | m_enabled |
|-----------------------------------------------------|-------------------------|-----------|
| `OculusTouchControllerProfile Standalone`           | `2936819204837876351`   | **1**     |
| `MetaQuestTouchPlusControllerProfile Standalone`    | `-2284545406782384865`  | **1**     |
| `MetaQuestTouchProControllerProfile Standalone`     | `-5087427606352745180`  | **1**     |
| `HandTracking Standalone` (XR Hands)                | `1234`-region object    | 0         |
| `MetaHandTrackingAim Standalone`                    | `-1574091030223266113`  | 0         |

All THREE controller profiles appear in the Standalone `OpenXRSettings.features` list
(`&6028622385836451133`, render mode `m_renderMode: 1` = multipass — fine). So suspect (2) from the
brief is CONFIRMED: the profile set was changed and is now in a multi-profile state. XR Hands /
hand-tracking is OFF (suspect (e) RULED OUT — hands are not stealing the controllers).

### Active loader / input handler
- `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`: Standalone uses the OpenXR loader
  (`b0c371f55a25f2c4baeba4bd1dba4626`), `m_InitManagerOnStart: 1`. OpenXR IS the active runtime over
  Link. Good.
- `ProjectSettings/ProjectSettings.asset`: `activeInputHandler: 1` (Input System only — no
  legacy/Both ambiguity). Good.
- No project-wide `InputSystem_Actions.inputactions` asset exists in this project (Glob found none),
  so suspect "project-wide actions interfere" is RULED OUT for this project.

### Bindings (`Assets/Ronin7/Settings/Ronin7Input.inputactions`)
Confirmed paths are usage-filtered legacy XRController paths, e.g.
`<XRController>{LeftHand}/primary2DAxis`, `<XRController>{LeftHand}/devicePosition`,
`<XRController>{RightHand}/primary2DAxis`. `"controlSchemes": []` (no scheme/binding mask — fine).
These paths are CORRECT and worked before; the failure is the **device side losing its usage**, not
the binding text.

### Packages (`Packages/manifest.json`) — after the cache wipe
- `com.unity.inputsystem`: **1.17.0**  ← the brief/earlier notes assumed a different patch; this is
  the version now resolved after the cache re-download.
- XRI 3.2.1, OpenXR 1.15.1, Meta OpenXR 2.2.0, XR Hands 1.6.1, XR Management 4.5.1, core-utils 2.5.1,
  `com.unity.xr.oculus 4.5.4` is ALSO present.
- No obviously broken/incompatible pin, but the cache wipe (suspect (3)) is the most plausible reason
  a previously-working session regressed: re-resolved packages + a re-initialized OpenXR layer can
  reset the runtime/profile handshake, and the **Oculus XR plugin (4.5.4) sitting alongside OpenXR**
  can fight over which runtime owns the Link session.

## Suspects evaluated

- (a) Enabled profile doesn't match controllers / needed profile missing — **PARTLY**: the opposite
  problem — too many enabled at once (Oculus Touch + Touch Plus + Touch Pro). Over Link the Meta
  runtime advertises Touch Plus; with three profiles fighting, the device enumerated but came up
  with no usages. The fix is to converge on the matching set, not add more.
- (b) Conflicting / zero valid interaction profiles — **CONFIRMED (conflicting, not zero).** Three
  controller profiles enabled simultaneously is the regression.
- (c) An OpenXR feature/render setting suppressing controller input — render mode is fine; the only
  feature noise is `BoundaryVisibilityFeature`/Meta AR features enabled for Standalone (the console
  XR_META_boundary_visibility warning). These don't disable controller input but are pointless over
  Link and add failed-extension noise — disable to reduce variables (see fixes).
- (d) Package regression from cache wipe — **PLAUSIBLE TRIGGER.** The wipe is what turned a working
  session into a broken one; it likely reset the OpenXR/Oculus runtime selection and profile
  handshake. Verified versions are sane, so the fix is to re-assert the runtime + profile config,
  not downgrade packages.
- (e) XR Hands taking over — **RULED OUT** (`HandTracking Standalone` and `MetaHandTrackingAim
  Standalone` both m_enabled: 0).

## PRIORITIZED FIX LIST (exact Editor steps)

### FIX 1 (DO THIS FIRST) — Set the Play-Mode OpenXR Runtime to Meta, and converge the profiles
Reasoning: "device present, no usage, inert" over Link is almost always the editor talking to the
wrong/loosely-matched OpenXR runtime or fighting profiles. Forcing the Meta runtime + a single
matching profile makes the runtime hand Unity a properly-usage-tagged Touch device.

1. Edit ▸ **Project Settings ▸ XR Plug-in Management ▸ OpenXR**.
2. Select the **PC / Standalone** tab (the desktop/monitor icon) — this is what runs over Link.
3. **Play Mode OpenXR Runtime** dropdown (top of the OpenXR page): set it explicitly to
   **Oculus** / **Meta** (the Meta Quest Link runtime), NOT "System Default". If it currently shows
   another runtime (e.g. SteamVR, Virtual Desktop, Windows MR), that alone causes inert/usage-less
   controllers.
4. Under **Interaction Profiles** for the Standalone tab, **remove the duplicates**: keep
   **Oculus Touch Controller Profile** ENABLED; **disable Meta Quest Touch Pro Controller Profile**
   and **disable Meta Quest Touch Plus Controller Profile** for now. (Oculus Touch is the broadest
   match for Quest 3 over Link and is what worked historically.) Goal: exactly ONE controller
   profile enabled on Standalone.
   - In the asset this means: leave `OculusTouchControllerProfile Standalone` `m_enabled: 1`; set
     `MetaQuestTouchProControllerProfile Standalone` and `MetaQuestTouchPlusControllerProfile
     Standalone` to `m_enabled: 0`.
5. Press Play. **Verify (Input Debugger):** Window ▸ Analysis ▸ Input Debugger ▸ expand Devices ▸
   double-click the controller device. It should now read e.g. `XRController` with usages
   **LeftHand** (and a second device **RightHand**), and `primary2DAxis` should show **live changing
   values** as you push the stick. If usages now appear, the flight sticks and hand pose will work.

### FIX 2 — If FIX 1 still shows no usages, swap which single profile is enabled
The Quest 3 over current Link/runtime may want Touch Plus instead of Oculus Touch.
1. Same OpenXR ▸ Standalone tab. Disable **Oculus Touch**, ENABLE **Meta Quest Touch Plus Controller
   Profile** (only). Keep it to ONE profile.
2. Play; re-check the Input Debugger for LeftHand/RightHand usages + live `primary2DAxis`.
3. If that also fails, try ONE profile = **Oculus Touch + Touch Plus together** (the two-profile
   combo Meta documents for Quest), but never all three. Re-verify each time.

### FIX 3 — Resolve the OpenXR-vs-Oculus plugin runtime conflict (cache-wipe fallout)
`com.unity.xr.oculus` (4.5.4) is installed alongside OpenXR. Only ONE XR plugin must own Standalone.
1. Project Settings ▸ XR Plug-in Management ▸ **PC/Standalone** tab (the top plugin-provider list,
   above OpenXR settings). Ensure **OpenXR** is the ONLY checked provider and **Oculus** is
   UNCHECKED. If both are checked, the Link session handshake is ambiguous → inert devices.
2. If toggling did nothing, force a clean XR re-init: close Play, **Assets ▸ Reimport All** is
   overkill — instead just toggle the OpenXR provider checkbox off then on (this rewrites the loader
   list), then Play.
3. Verify as in FIX 1.

### FIX 4 — Reduce runtime noise (not the root cause, do after input returns)
On the Standalone tab, the Meta AR features are enabled and spam failed-extension warnings over Link
(the XR_META_boundary_visibility console line). They don't block input but add variables:
- Disable for **Standalone**: Meta Quest: Boundary Visibility, Meta Quest: Camera (Passthrough),
  Meta Quest: Meshing, Meta Quest: Session, Meta Quest: Bounding Boxes, Meta Quest: Anchors, Meta
  Quest: Raycasts, Meta Quest: Colocation Discovery. (Leave them on Android if you build to-device
  and actually use passthrough/anchors.) This is purely to clean the console; do it only after
  controllers work, so you don't change two things at once.

### FIX 5 — Editor restart + Quest Link restart (the cheap reset)
Because the package cache was wiped mid-session, the OpenXR layer may be half-initialized:
1. Fully **quit the Unity Editor**.
2. In the Meta Quest Link desktop app, ensure Link is active and the **OpenXR Runtime is set to
   Meta** (Quest Link app ▸ Settings ▸ General ▸ "OpenXR Runtime" → Set as active). A different app
   (SteamVR / Virtual Desktop) may have grabbed the active-runtime slot — that produces usage-less
   controllers in every Unity app.
3. Reopen Unity, Play, verify usages in the Input Debugger.

## The single most-likely first fix (and why)
**Set Play Mode OpenXR Runtime = Meta/Oculus AND reduce to a single enabled controller profile
(Oculus Touch) on the Standalone tab (FIX 1).** Reason: the failure signature is textbook —
*device enumerated but with no handedness usage and no live controls, HMD fine, in every scene*.
That is a device/runtime-side problem (a profile/runtime handshake), not a per-scene serialization
problem (which is what every previous section fixed). The two things that changed this session that
produce exactly this signature are (i) three controller profiles now enabled at once and (ii) the
cache wipe re-initializing the OpenXR/runtime selection. Converging on one matching profile + pinning
the Meta runtime addresses both at once and is reversible in seconds.

## How to verify EACH fix (Input Debugger — the authoritative check)
1. Window ▸ Analysis ▸ **Input Debugger**. Enter Play with the headset on over Link.
2. Under **Devices**, you must see the controller devices. Double-click each:
   - PASS = the device shows a **usage of LeftHand** (and the other RightHand), and pushing the
     thumbstick makes **`primary2DAxis` values change live**, and `devicePosition`/`deviceRotation`
     update as you move the controller.
   - FAIL = device listed but **Usages: (none)** and controls frozen at 0 → that fix didn't take;
     proceed to the next fix.
3. Cross-check in-game: hands leave the origin and track; ShipController logs
   `[Ship] raw throttle=(x,y) …` non-zero; `thrControls` is now > 0 (the diagnostic that read 0
   should now read 2 per stick action). HMD head tracking should remain unaffected throughout.

## Code-robustness note (secondary; config is the priority)
The bindings rely solely on the `{LeftHand}`/`{RightHand}` usage filter, which is exactly what fails
when the device loses its usage. To make input survive a usage-less device, you could add **fallback
bindings** in `Ronin7Input.inputactions` that don't depend on handedness, e.g. add
`<XRController>/primary2DAxis` and `<MetaQuestTouchPlusControllerOpenXR>{LeftHand}/thumbstick` /
device-specific paths alongside the existing `<XRController>{LeftHand}/primary2DAxis`. Adding the
concrete device layout (`MetaQuestTouchPlusControllerOpenXR`/`OculusTouchControllerProfile` thumbstick
paths) as additional bindings makes the action bind even when the generic `<XRController>` usage
fails to populate. This is a hardening measure only — it will NOT help if the runtime/profile
handshake gives a device with no controls at all (FIX 1–5 are still required); but with a single
correct profile, the device-specific path resolves reliably. Do the config fixes first; add fallback
bindings only if you keep seeing usage-less generic XRController devices.

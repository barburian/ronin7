using Ronin7.Ship;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Self-contained VR locomotion driven directly by the Input System (no dependency on
    /// XRI's version-sensitive locomotion providers). Handles head-relative continuous
    /// movement, snap turning around the head, gravity, and keeping the CharacterController
    /// capsule under the tracked head each frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ContinuousLocomotion : MonoBehaviour
    {
        /// <summary>The active on-foot locomotion in the current scene. Set in <see cref="OnEnable"/>,
        /// cleared in <see cref="OnDisable"/>. Lets the settings service push live changes without
        /// a per-call <c>FindAnyObjectByType</c>.</summary>
        public static ContinuousLocomotion Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction; // Vector2, left stick
        [SerializeField] private InputActionReference turnAction; // Vector2, right stick
        [SerializeField] private InputActionReference dashAction; // Button, right-hand A button
        [SerializeField] private InputActionReference runAction;  // Button, right joystick click: toggles run on/off
        [SerializeField] private InputActionReference crouchAction; // Button, left-hand X button: toggles crouch
        [SerializeField] private InputActionReference jumpAction; // Button, left thumbstick click

        [Header("Move")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Jump")]
        [Tooltip("Takeoff speed (m/s). 4.2 clears ~0.9m — a readable VR hop, not a rocket.")]
        [SerializeField] private float jumpSpeed = 4.2f;

        [Header("Wall Run")]
        [Tooltip("Gravity multiplier while wall-running. The camera NEVER rolls — comfort rule.")]
        [SerializeField, Range(0f, 1f)] private float wallRunGravityScale = 0.25f;
        [Tooltip("Max seconds a single wall run can defy gravity before it decays back to normal.")]
        [SerializeField] private float wallRunMaxSeconds = 2.5f;
        [Tooltip("How far to the side a wall must be (from the capsule center) to count as adjacent.")]
        [SerializeField] private float wallProbeDistance = 0.9f;
        [Tooltip("Minimum horizontal speed to sustain a wall run (run-speed territory).")]
        [SerializeField] private float wallRunMinSpeed = 3.5f;
        [Tooltip("Horizontal push-off speed when jumping out of a wall run.")]
        [SerializeField] private float wallJumpPushSpeed = 3.5f;
        [Tooltip("Slowest allowed fall while wall-running, so the run reads as a run, not a slide.")]
        [SerializeField] private float wallRunMaxFallSpeed = -1.5f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 6f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float dashCooldown = 0.6f;

        [Header("Run")]
        [Tooltip("Move-speed multiplier while run mode is toggled on.")]
        [SerializeField] private float runMultiplier = 2.2f;

        [Header("Crouch")]
        [Tooltip("How far the viewpoint drops while crouched or sliding (metres, negative = down).")]
        [SerializeField] private float crouchEyeOffset = -0.55f;
        [Tooltip("Move-speed multiplier while crouched.")]
        [SerializeField] private float crouchSpeedMultiplier = 0.5f;
        [Tooltip("SmoothDamp time for the crouch/slide eye-height transition.")]
        [SerializeField] private float crouchSmoothTime = 0.15f;

        [Header("Slide")]
        [Tooltip("Ground speed at the moment a run-dash slide starts; eases back down to run speed.")]
        [SerializeField] private float slideStartSpeed = 9f;
        [SerializeField] private float slideDuration = 0.9f;

        [Header("Turn")]
        [Tooltip("Use SNAP (discrete) turning instead of continuous. Snap is comfier; continuous is smoother. Settable live from the settings menu.")]
        [SerializeField] private bool useSnapTurn = true;
        [SerializeField] private float snapTurnDegrees = 45f;
        [Tooltip("Stick magnitude required to trigger a snap turn.")]
        [SerializeField] private float snapTurnThreshold = 0.7f;
        [Tooltip("Continuous turn rate (deg/sec) when snap turning is off.")]
        [SerializeField] private float continuousTurnSpeed = 90f;

        [Header("Comfort — Vignette")]
        [Tooltip("Auto-create and drive a tunnelling vignette on the head camera during turns. Settable live from the settings menu.")]
        [SerializeField] private bool useComfortVignette = true;

        [Header("Body")]
        [Tooltip("Minimum capsule height so the player can't shrink to nothing when crouching the headset.")]
        [SerializeField] private float minHeight = 0.6f;

        [Header("Zero-G")]
        [SerializeField] private float maxDriftSpeed = 2.5f;

        private CharacterController controller;
        private float verticalVelocity;
        private bool snapArmed = true;
        private ComfortVignette vignette;
        private float dashEndTime = -1f;
        private float nextDashTime;
        private Vector3 dashHorizontal;
        private bool isRunning;
        private bool isCrouching;
        private float slideEndTime = -1f;
        private Vector3 slideDirection;
        private float currentEyeOffset;
        private float eyeOffsetVelocity;
        private float appliedEyeOffset;
        private bool zeroGEnabled = false;
        private float zeroGDamping = 0.6f;
        private Vector3 driftVelocity = Vector3.zero;
        private bool movementSuspended;
        private Vector3 airImpulse;
        private float wallRunTimer;
        private Vector3 wallRunNormal;
        private bool wallRunActive;
        // Per-wall bookkeeping: the timer belongs to ONE wall (identified by its normal) and resets
        // when a different wall engages mid-air, so chained runs (A → wall-jump → B) each get a full
        // window. After a wall jump the jumped wall is banned until grounding or a different wall.
        private Vector3 lastWallNormal;
        private Vector3 wallJumpBanNormal;

        private static readonly RaycastHit[] WallProbeHits = new RaycastHit[4];

        private bool IsSliding => Time.time < slideEndTime;
        private bool IsDashing => Time.time < dashEndTime;
        /// <summary>True while a dash or slide burst is in progress. Posture toggles are ignored
        /// during this window so the burst is uninterruptible and sub-state transitions stay
        /// well-defined (no crouching mid-dash; slide takes precedence until it ends).</summary>
        private bool IsActionLocked => IsDashing || IsSliding;

        /// <summary>Live setters for the settings menu (applied via SettingsService).</summary>
        public void SetTurnStyle(bool snap, float snapDegrees)
        {
            useSnapTurn = snap;
            snapTurnDegrees = snapDegrees;
        }

        public void SetComfortVignette(bool on)
        {
            useComfortVignette = on;
            if (on) vignette = Ronin7.Ship.VignetteRig.Ensure(cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main);
            else if (vignette != null) vignette.SetIntensity(0f);
        }

        /// <summary>Enable/disable zero-gravity mode. When on, gravity is zeroed and stick input
        /// drives thrust accumulated into persistent drift velocity (all 3 axes, head-relative).
        /// When off, normal gravity behavior is restored.</summary>
        public void SetZeroG(bool enabled, float driftDamping)
        {
            zeroGEnabled = enabled;
            zeroGDamping = driftDamping;
            // Clear both on every toggle: entering zero-g must not inherit fall velocity,
            // and leaving it must not jerk the player with stale drift.
            verticalVelocity = 0f;
            driftVelocity = Vector3.zero;
        }

        /// <summary>Inject a velocity impulse (world-space) that will be applied via drift
        /// accumulation. Used when releasing from grab-locomotion in zero-g.</summary>
        public void AddDriftImpulse(Vector3 worldVelocity)
        {
            if (zeroGEnabled)
                driftVelocity += worldVelocity;
        }

        /// <summary>
        /// While true, normal stick movement, gravity, jump/dash and posture input are all frozen —
        /// <see cref="WallClimbLocomotion"/> owns the rig's motion (hand-anchored climbing). Turning
        /// and head-capsule tracking stay live. Entering/leaving suspension clears vertical and air
        /// velocity so climbing never inherits fall speed and vice versa.
        /// </summary>
        public bool MovementSuspended
        {
            get => movementSuspended;
            set
            {
                if (movementSuspended == value) return;
                movementSuspended = value;
                verticalVelocity = 0f;
                airImpulse = Vector3.zero;
                wallRunTimer = 0f;
            }
        }

        /// <summary>
        /// World-space launch velocity for parkour exits (climb fling, wall jump): the Y component
        /// replaces vertical velocity when rising, the horizontal part decays over ~half a second.
        /// </summary>
        public void AddAirImpulse(Vector3 worldVelocity)
        {
            if (worldVelocity.y > verticalVelocity) verticalVelocity = worldVelocity.y;
            airImpulse += new Vector3(worldVelocity.x, 0f, worldVelocity.z);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (Instance == null || Instance == this) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (useComfortVignette) vignette = Ronin7.Ship.VignetteRig.Ensure(cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main);
        }

        private void OnEnable()
        {
            moveAction?.action?.Enable();
            turnAction?.action?.Enable();
            dashAction?.action?.Enable();
            runAction?.action?.Enable();
            crouchAction?.action?.Enable();
            jumpAction?.action?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.action?.Disable();
            turnAction?.action?.Disable();
            dashAction?.action?.Disable();
            runAction?.action?.Disable();
            crouchAction?.action?.Disable();
            jumpAction?.action?.Disable();

            // Remove any crouch/slide offset we applied so it can't leak into the next scene's rig.
            var rig = VRRig.Instance;
            if (appliedEyeOffset != 0f && rig != null && rig.EyeHeightRoot != null)
            {
                var p = rig.EyeHeightRoot.localPosition;
                p.y -= appliedEyeOffset;
                rig.EyeHeightRoot.localPosition = p;
            }
            appliedEyeOffset = 0f;
            currentEyeOffset = 0f;
            eyeOffsetVelocity = 0f;
        }

        private void Update()
        {
            HandleTurn();
            UpdateEyeHeight();
            MatchCapsuleToHead();
            if (movementSuspended) return; // climbing owns motion; turn + head tracking stay live

            HandlePostureToggles();
            TryStartDash();
            HandleMove();
        }

        /// <summary>
        /// Joystick click toggles run, X button toggles crouch. Both presses are resolved together
        /// through <see cref="ResolvePosture"/> so overlapping presses and in-progress dashes/slides
        /// always collapse to a single well-defined posture (run and crouch never both active; a
        /// dash/slide can't be interrupted by a posture change).
        /// </summary>
        private void HandlePostureToggles()
        {
            bool runPressed = runAction != null && runAction.action != null && runAction.action.WasPressedThisFrame();
            // Crouch (Left X) carries a Tap interaction so it separates from weakpoint-sight's Hold on
            // the same button — read WasPerformedThisFrame so a long hold (weakpoint) doesn't also crouch.
            bool crouchPressed = crouchAction != null && crouchAction.action != null && crouchAction.action.WasPerformedThisFrame();
            if (!runPressed && !crouchPressed) return;

            (isRunning, isCrouching) = ResolvePosture(isRunning, isCrouching, runPressed, crouchPressed, IsActionLocked);
        }

        /// <summary>
        /// Pure precedence rule for the on-foot posture toggles, kept separate so it can be
        /// unit-tested without the Unity input/lifecycle. Run and crouch are mutually exclusive (if
        /// both are pressed on the same frame, crouch wins); while a dash or slide is in progress
        /// (<paramref name="actionLocked"/>) the posture is frozen so the burst can't be interrupted.
        /// </summary>
        public static (bool running, bool crouching) ResolvePosture(
            bool running, bool crouching, bool runPressed, bool crouchPressed, bool actionLocked)
        {
            if (actionLocked) return (running, crouching);

            if (runPressed)
            {
                running = !running;
                if (running) crouching = false;
            }
            if (crouchPressed)
            {
                crouching = !crouching;
                if (crouching) running = false;
            }
            return (running, crouching);
        }

        /// <summary>
        /// Smoothly sinks/raises the viewpoint for crouch and slide. Applied as a per-frame delta
        /// on <see cref="VRRig.EyeHeightRoot"/> so it composes with the absolute Y written by
        /// <see cref="XREyeHeightCalibrator.ApplyOffset"/> instead of clobbering it.
        /// </summary>
        private void UpdateEyeHeight()
        {
            float target = (isCrouching || IsSliding) ? crouchEyeOffset : 0f;
            currentEyeOffset = Mathf.SmoothDamp(currentEyeOffset, target, ref eyeOffsetVelocity, crouchSmoothTime);

            var rig = VRRig.Instance;
            if (rig == null || rig.EyeHeightRoot == null) return;

            var p = rig.EyeHeightRoot.localPosition;
            p.y += currentEyeOffset - appliedEyeOffset;
            rig.EyeHeightRoot.localPosition = p;
            appliedEyeOffset = currentEyeOffset;
        }

        private void TryStartDash()
        {
            if (dashAction == null || dashAction.action == null) return;
            // Dash (Right A) carries a Tap interaction so it separates from Overdrive's Hold(0.4s) on
            // the same button (Ch9) — read WasPerformedThisFrame so a held press (Overdrive) doesn't
            // also dash.
            if (!dashAction.action.WasPerformedThisFrame()) return;
            if (Time.time < nextDashTime) return;

            Vector2 input = moveAction != null && moveAction.action != null
                ? moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            Vector3 dir;
            if (input.magnitude < 0.1f)
                dir = forward;
            else
                dir = (forward * input.y + right * input.x).normalized;

            if (isRunning && input.magnitude >= 0.1f)
            {
                // Dashing at a run becomes a ground slide: locked direction, eased speed.
                slideDirection = dir;
                slideEndTime = Time.time + slideDuration;
                nextDashTime = slideEndTime + dashCooldown;
            }
            else
            {
                dashHorizontal = dir * dashSpeed;
                dashEndTime = Time.time + dashDuration;
                nextDashTime = dashEndTime + dashCooldown;
            }
        }

        /// <summary>Resize/reposition the capsule so it sits under the tracked head.</summary>
        private void MatchCapsuleToHead()
        {
            if (cameraTransform == null) return;

            float headHeight = Mathf.Max(minHeight, cameraTransform.localPosition.y + currentEyeOffset);
            controller.height = headHeight;
            controller.center = new Vector3(
                cameraTransform.localPosition.x,
                headHeight * 0.5f + controller.skinWidth,
                cameraTransform.localPosition.z);
        }

        private void HandleMove()
        {
            if (zeroGEnabled)
            {
                HandleZeroGMove();
            }
            else
            {
                HandleNormalMove();
            }
        }

        private void HandleNormalMove()
        {
            Vector3 horizontal;
            if (IsSliding)
            {
                // Ease-out from the slide burst back into run speed so the exit has no velocity pop.
                float t = 1f - (slideEndTime - Time.time) / slideDuration;
                float speed = Mathf.Lerp(slideStartSpeed, moveSpeed * runMultiplier, t * t);
                horizontal = slideDirection * speed;
                if (vignette != null) vignette.SetIntensity(0.4f * (1f - t));
            }
            else if (Time.time < dashEndTime)
            {
                horizontal = dashHorizontal;
            }
            else
            {
                Vector2 input = moveAction != null && moveAction.action != null
                    ? moveAction.action.ReadValue<Vector2>()
                    : Vector2.zero;

                Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
                Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
                forward.y = 0f; right.y = 0f;
                forward.Normalize(); right.Normalize();

                float speed = isRunning ? moveSpeed * runMultiplier
                    : isCrouching ? moveSpeed * crouchSpeedMultiplier
                    : moveSpeed;
                horizontal = (forward * input.y + right * input.x) * speed;
            }

            bool grounded = controller.isGrounded;
            if (grounded)
            {
                wallRunTimer = 0f;
                lastWallNormal = Vector3.zero;
                wallJumpBanNormal = Vector3.zero;
                if (verticalVelocity < 0f) verticalVelocity = -2f;
            }

            // Wall run: airborne + run toggle + real speed + a wall beside the capsule. Gravity is
            // scaled down (never rolled, never sideways-accelerated — comfort first) for a limited
            // PER-WALL window so a fast runner can carry along a wall, jump off it, and catch the
            // next wall with a fresh window (the hard course's transfer depends on this).
            wallRunActive = false;
            float gravityScale = 1f;
            if (!grounded && isRunning)
            {
                bool wallAdjacent = ProbeSideWall(out wallRunNormal);
                // The wall we just jumped off stays banned until grounding or a different wall.
                if (wallAdjacent && wallJumpBanNormal != Vector3.zero &&
                    Vector3.Dot(wallRunNormal, wallJumpBanNormal) > 0.9f)
                {
                    wallAdjacent = false;
                }
                if (wallAdjacent)
                {
                    if (Vector3.Dot(wallRunNormal, lastWallNormal) < 0.9f)
                    {
                        wallRunTimer = 0f; // a different wall: fresh window
                        wallJumpBanNormal = Vector3.zero;
                    }
                    lastWallNormal = wallRunNormal;
                }
                gravityScale = WallRunGravityScale(
                    grounded, isRunning, horizontal.magnitude, wallRunMinSpeed,
                    wallAdjacent, wallRunTimer, wallRunMaxSeconds, wallRunGravityScale);
                wallRunActive = gravityScale < 1f;
            }

            bool jumpPressed = jumpAction != null && jumpAction.action != null && jumpAction.action.WasPressedThisFrame();
            if (jumpPressed)
            {
                if (grounded)
                {
                    verticalVelocity = jumpSpeed;
                }
                else if (wallRunActive)
                {
                    Vector3 v = WallJumpVelocity(wallRunNormal, wallJumpPushSpeed, jumpSpeed);
                    verticalVelocity = v.y;
                    airImpulse += new Vector3(v.x, 0f, v.z);
                    // Ban THIS wall (can't re-latch it mid-air) but leave the timer free so the
                    // next, different wall gets its own full window — chained transfers work.
                    wallJumpBanNormal = wallRunNormal;
                    lastWallNormal = Vector3.zero;
                    wallRunActive = false;
                }
            }

            if (wallRunActive)
            {
                wallRunTimer += Time.deltaTime;
                if (vignette != null) vignette.SetIntensity(0.25f); // gentle tunnel, mirrors slide
            }

            verticalVelocity += gravity * gravityScale * Time.deltaTime;
            if (wallRunActive && verticalVelocity < wallRunMaxFallSpeed)
                verticalVelocity = wallRunMaxFallSpeed;

            // Parkour exit impulses (climb fling / wall jump) decay over ~half a second AIRBORNE
            // only — touchdown kills them outright. Residual impulse on the ground reads as
            // uncommanded sliding in VR (comfort violation) and drags landings off small pads.
            if (grounded) airImpulse = Vector3.zero;
            else airImpulse *= Mathf.Exp(-2.5f * Time.deltaTime);

            Vector3 motion = horizontal + airImpulse + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        /// <summary>True when solid geometry sits within <see cref="wallProbeDistance"/> directly to
        /// the capsule's left or right (head-flattened), returning the wall's horizontal normal.</summary>
        private bool ProbeSideWall(out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) return false;
            right.Normalize();

            Vector3 origin = transform.TransformPoint(controller.center);
            for (int side = 0; side < 2; side++)
            {
                Vector3 dir = side == 0 ? right : -right;
                int count = Physics.RaycastNonAlloc(origin, dir, WallProbeHits, wallProbeDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (WallProbeHits[i].transform.IsChildOf(transform)) continue;
                    Vector3 n = WallProbeHits[i].normal;
                    n.y = 0f;
                    if (n.sqrMagnitude < 0.25f) continue; // floors/ramps don't count as walls
                    normal = n.normalized;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pure wall-run rule: gravity is scaled only while airborne, in run mode, moving at least
        /// <paramref name="minSpeed"/> horizontally, with a wall adjacent, inside the per-wall time
        /// window. Everything else returns 1 (full gravity). Public + unit-tested, mirrors
        /// <see cref="ResolvePosture"/>'s pure-rule idiom.
        /// </summary>
        public static float WallRunGravityScale(
            bool grounded, bool running, float horizontalSpeed, float minSpeed,
            bool wallAdjacent, float timer, float maxSeconds, float scale)
        {
            if (grounded || !running || !wallAdjacent) return 1f;
            if (horizontalSpeed < minSpeed) return 1f;
            if (timer >= maxSeconds) return 1f;
            return Mathf.Clamp01(scale);
        }

        /// <summary>Pure wall-jump velocity: push off along the wall's flattened normal plus the
        /// standard takeoff. A degenerate normal still yields a clean vertical jump.</summary>
        public static Vector3 WallJumpVelocity(Vector3 wallNormal, float pushSpeed, float jumpSpeed)
        {
            wallNormal.y = 0f;
            Vector3 push = wallNormal.sqrMagnitude > 0.0001f ? wallNormal.normalized * pushSpeed : Vector3.zero;
            return push + Vector3.up * jumpSpeed;
        }

        private void HandleZeroGMove()
        {
            // Read stick input (head-relative, all 3 axes allowed).
            Vector2 input = moveAction != null && moveAction.action != null
                ? moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
            Vector3 up = cameraTransform != null ? cameraTransform.up : transform.up;

            // Accumulate thrust into drift velocity (horizontal stick = forward+right, no Y from stick).
            float thrustMagnitude = input.magnitude > 0.1f ? moveSpeed : 0f;
            Vector3 thrust = (forward * input.y + right * input.x).normalized * thrustMagnitude;
            driftVelocity += thrust * Time.deltaTime;

            // Apply exponential damping.
            driftVelocity *= Mathf.Exp(-zeroGDamping * Time.deltaTime);

            // Clamp drift speed.
            if (driftVelocity.magnitude > maxDriftSpeed)
                driftVelocity = driftVelocity.normalized * maxDriftSpeed;

            // Move the rig (no vertical gravity, no rotation).
            controller.Move(driftVelocity * Time.deltaTime);
        }

        private void HandleTurn()
        {
            float x = turnAction != null && turnAction.action != null
                ? turnAction.action.ReadValue<Vector2>().x
                : 0f;

            Vector3 pivot = cameraTransform != null ? cameraTransform.position : transform.position;
            float intensity = 0f;

            if (useSnapTurn)
            {
                if (Mathf.Abs(x) < snapTurnThreshold) snapArmed = true;
                else if (snapArmed)
                {
                    snapArmed = false;
                    transform.RotateAround(pivot, Vector3.up, snapTurnDegrees * Mathf.Sign(x));
                    intensity = 1f; // brief flash on each snap
                }
            }
            else if (Mathf.Abs(x) >= snapTurnThreshold * 0.3f) // small live deadzone for continuous turn
            {
                transform.RotateAround(pivot, Vector3.up, continuousTurnSpeed * x * Time.deltaTime);
                intensity = Mathf.Abs(x); // tunnel scales with turn rate
            }

            if (vignette != null) vignette.SetIntensity(intensity);
        }

    }
}

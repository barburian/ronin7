using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Ship
{
    /// <summary>
    /// Arcade 6DOF flight using the "fixed cockpit, moving universe" model: the player rig
    /// and cockpit stay put (so XR head tracking is rock-solid), and the world (the
    /// <see cref="universe"/> transform holding planets/asteroids) moves around them to
    /// create the sense of flight. The stable cockpit frame is the main comfort anchor.
    ///
    /// Left stick: throttle (Y) + roll (X). Right stick: pitch (Y) + yaw (X). Gentle rates.
    ///
    /// VR comfort: continuous rotation is the dominant nausea trigger, so this controller
    /// (a) keeps rotation rates conservative, (b) supports optional snap/step YAW instead of
    /// continuous yaw, and (c) drives a <see cref="ComfortVignette"/> tunnel that closes in
    /// during rotation and acceleration to cut peripheral optical-flow. All tunables are
    /// serialized with comfortable first-time-player defaults.
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        /// <summary>
        /// The active ship in the current scene. Set in <see cref="OnEnable"/>, cleared in
        /// <see cref="OnDisable"/>. Lets cross-system code (audio, settings, encounter, enemy
        /// ships) reach the ship without a scene-wide <c>FindAnyObjectByType</c> on hot paths.
        /// </summary>
        public static ShipController Instance { get; private set; }

        [Header("Input")]
        [SerializeField] private InputActionReference throttleAxis; // Left Hand "Move"
        [SerializeField] private InputActionReference steerAxis;    // Right Hand "Turn"
        [Tooltip("Stick magnitude below this is ignored; input above it is rescaled so there is no jump at the edge of the deadzone.")]
        [SerializeField, Range(0f, 0.4f)] private float deadzone = 0.15f;
        [Tooltip("Response curve exponent. 1 = linear, 2 = gentle/precise near centre (recommended for comfort).")]
        [SerializeField, Range(1f, 3f)] private float responseExponent = 1.5f;

        [Header("World")]
        [Tooltip("Root containing the moving environment (planets, asteroids).")]
        [SerializeField] private Transform universe;

        [Header("Thrust")]
        [SerializeField] private float maxSpeed = 35f;
        [Tooltip("Reverse speed as a fraction of max (gentle, mostly for nudging back).")]
        [SerializeField, Range(0f, 1f)] private float reverseFraction = 0.4f;
        [Tooltip("How fast current speed eases toward the throttle target (units/sec^2). Lower = smoother, comfier.")]
        [SerializeField] private float acceleration = 12f;

        [Header("Steering (deg/sec) — agile fighter, comfort-bounded")]
        // Sleek-fighter feel: rates bumped from the original gentle 40/35/50 for more agility (roll
        // bumped most — it's the least nausea-provoking; yaw least — it's the worst). The snap-yaw
        // option and the comfort vignette (which saturates at vignetteRotationRef) still bound nausea.
        // STARTING VALUES — tune in-headset.
        [SerializeField] private float pitchSpeed = 50f;
        [SerializeField] private float yawSpeed = 42f;
        [SerializeField] private float rollSpeed = 70f;
        [SerializeField] private bool invertPitch = true;
        [Tooltip("How fast steering rates ramp in/out (deg/sec ramp). Smooths stick flicks so rotation never starts/stops abruptly.")]
        [SerializeField] private float steerSmoothing = 8f;

        [Header("Comfort — Yaw")]
        [Tooltip("Use SNAP yaw (discrete steps) instead of continuous yaw. Continuous yaw is the strongest nausea trigger; snap is much comfier for sensitive players. Pitch/roll stay continuous (less provocative).")]
        [SerializeField] private bool useSnapYaw = false;
        [SerializeField] private float snapYawDegrees = 30f;
        [Tooltip("Stick deflection needed to trigger one snap step.")]
        [SerializeField, Range(0.3f, 0.95f)] private float snapYawThreshold = 0.7f;

        [Header("Sun Navigation (optional, OFF by default — see Docs/SunNavigation-Design.md)")]
        [Tooltip("The sun's SunGravityWell, if the scene has one. Required for enableSunBoost to have any effect.")]
        [SerializeField] private SunGravityWell sunWell;
        [Tooltip("Fold the sun's slingshot boost into forward speed. Additive, in-headset-tuned hand-off — off by default.")]
        [SerializeField] private bool enableSunBoost = false;

        [Header("Comfort — Vignette")]
        [Tooltip("Auto-create and drive a tunnelling vignette on the head camera. Strongly recommended for first-time players.")]
        [SerializeField] private bool useComfortVignette = true;
        [Tooltip("Rotation rate (deg/sec) at which the vignette reaches full strength.")]
        [SerializeField] private float vignetteRotationRef = 35f;
        [Tooltip("Acceleration magnitude (units/sec^2) at which the vignette reaches full strength.")]
        [SerializeField] private float vignetteAccelRef = 8f;

        public float CurrentSpeed { get; private set; }

        /// <summary>Multiplies forward speed before the sun-boost factor (see <see cref="EffectiveSpeed"/>).
        /// Exists so a future H4 trick system can compose with the sun-boost hook without touching the
        /// integration line in <see cref="Update"/> again. 1 = no trim (identity), the default.</summary>
        public float SpeedTrimMultiplier { get; set; } = 1f;

        /// <summary>Live setters for the settings menu (applied via SettingsService).</summary>
        public void SetTurnStyle(bool snap, float snapDegrees)
        {
            useSnapYaw = snap;
            snapYawDegrees = snapDegrees;
        }

        public void SetComfortVignette(bool on)
        {
            useComfortVignette = on;
            if (on) vignette = VignetteRig.Ensure(Camera.main);
            else if (vignette != null) vignette.SetIntensity(0f);
        }

        /// <summary>Top forward speed; lets audio/HUD normalise <see cref="CurrentSpeed"/> to 0..1.</summary>
        public float MaxSpeed => maxSpeed;

        /// <summary>
        /// The virtual ship's world-space position in <see cref="universe"/>-local terms. The rig
        /// never moves, so proximity to planets/asteroids (which live under <see cref="universe"/>)
        /// must be measured against this, not the camera transform.
        /// </summary>
        public Vector3 ShipPosition => shipPos;

        /// <summary>The virtual ship's orientation (the universe is rendered as its inverse).</summary>
        public Quaternion ShipRotation => shipRot;

        /// <summary>The moving-world root this ship drives. Public so encounter spawners and
        /// landing detectors can share the same frame of reference without their own field.</summary>
        public Transform Universe => universe;

        // Resolved, live action handles taken from the SAME InputActionAsset instance we enable.
        // We never read straight off the serialized InputActionReference, because a scene can end
        // up with mixed reference styles (embedded MonoBehaviour copies vs proper sub-asset refs).
        // Mixed styles can produce a situation where the action you *enable* is a different
        // instance from the one you *read* -> the read action stays at zero even though a stick is
        // pushed. Resolving + enabling the owning asset, then reading the resolved handle, makes
        // input flow regardless of how the references were serialized.
        private InputAction throttleResolved;
        private InputAction steerResolved;

        // Virtual ship pose; the universe is rendered as its inverse.
        private Vector3 shipPos;
        private Quaternion shipRot = Quaternion.identity;

        // Smoothed steering rates (deg/sec) so flicks don't jerk the world. Internal (not private)
        // so Teleport's rate-zeroing can be covered by an EditMode test.
        internal float pitchRate, yawRate, rollRate;
        private bool snapArmed = true;

        private ComfortVignette vignette;

        // Set Instance in Awake (not OnEnable) so other components reading it from their Awake
        // see a populated value regardless of script execution order — matches VRRig.Instance.
        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            throttleResolved = InputResolver.Resolve(throttleAxis, "Left Hand", "Move", "Ship");
            steerResolved = InputResolver.Resolve(steerAxis, "Right Hand", "Turn", "Ship");
            if (GameState.Instance != null) GameState.Instance.SetMode(GameMode.SpaceFlight);
        }

        private void OnDisable()
        {
            throttleResolved?.Disable();
            steerResolved?.Disable();
            throttleResolved = null;
            steerResolved = null;
        }

        private void Start()
        {
            if (useComfortVignette) vignette = VignetteRig.Ensure(Camera.main);
        }

        private void Update()
        {
            if (universe == null) return;

            Vector2 thr = Read(throttleResolved);
            Vector2 str = Read(steerResolved);
            float dt = Time.deltaTime;

            // --- Throttle: ease current speed toward the target (forward or gentle reverse). ---
            float target = thr.y >= 0f ? thr.y * maxSpeed : thr.y * maxSpeed * reverseFraction;
            float prevSpeed = CurrentSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, acceleration * dt);
            float accelMag = dt > 0f ? Mathf.Abs(CurrentSpeed - prevSpeed) / dt : 0f;

            // --- Steering: smooth target rates so rotation eases in/out. ---
            float pitchTarget = (invertPitch ? -1f : 1f) * str.y * pitchSpeed;
            float rollTarget = -thr.x * rollSpeed;
            pitchRate = Mathf.MoveTowards(pitchRate, pitchTarget, steerSmoothing * pitchSpeed * dt);
            rollRate = Mathf.MoveTowards(rollRate, rollTarget, steerSmoothing * rollSpeed * dt);

            float yawDelta;
            if (useSnapYaw)
            {
                yawRate = 0f;
                yawDelta = StepYaw(str.x);
            }
            else
            {
                float yawTarget = str.x * yawSpeed;
                yawRate = Mathf.MoveTowards(yawRate, yawTarget, steerSmoothing * yawSpeed * dt);
                yawDelta = yawRate * dt;
            }

            float pitch = pitchRate * dt;
            float roll = rollRate * dt;

            shipRot = AccumulateRotation(shipRot, pitch, yawDelta, roll);

            // Sun-nav preflight (OFF by default) + SpeedTrimMultiplier hook (future H4 trick system
            // lands here without touching this line again): forward speed is
            // CurrentSpeed * SpeedTrimMultiplier * (1 + boost), where boost is the sun's slingshot
            // assist (0 when enableSunBoost is off or no well is assigned — additive, never subtractive).
            float boost = (enableSunBoost && sunWell != null) ? sunWell.BoostAt(Vector3.zero) : 0f;
            shipPos += shipRot * (Vector3.forward * (EffectiveSpeed(CurrentSpeed, SpeedTrimMultiplier, boost) * dt));

            // Render the world relative to a stationary player = inverse of the ship pose.
            Quaternion inv = Quaternion.Inverse(shipRot);
            universe.SetPositionAndRotation(inv * (-shipPos), inv);

            // --- Comfort vignette: strongest of the rotation/acceleration provocations. ---
            if (ShouldDriveVignette(useComfortVignette, vignette != null))
            {
                // Continuous rotation magnitude this frame (deg/sec); snap yaw spikes briefly.
                float rotMag = (Mathf.Abs(pitchRate) + Mathf.Abs(yawRate) + Mathf.Abs(rollRate));
                if (useSnapYaw && Mathf.Abs(yawDelta) > 0f) rotMag += vignetteRotationRef; // brief flash on each snap
                float rotIntensity = vignetteRotationRef > 0f ? rotMag / vignetteRotationRef : 0f;
                float accelIntensity = vignetteAccelRef > 0f ? accelMag / vignetteAccelRef : 0f;
                vignette.SetIntensity(Mathf.Max(rotIntensity, accelIntensity));
            }
        }

        /// <summary>
        /// Integrate a local (pitch, yaw, roll) step (degrees) onto an orientation and renormalise.
        /// Accumulating rotation by repeated quaternion multiply slowly lets the quaternion drift off
        /// unit length over a long session, which would skew/scale the rendered world (this drives the
        /// universe's inverse pose). Renormalising each step keeps the orientation pure. Pure Unity-math,
        /// so it's unit-testable. Direction is unchanged by the normalise, so flight feel is identical.
        /// </summary>
        public static Quaternion AccumulateRotation(Quaternion current, float pitchDeg, float yawDeg, float rollDeg)
        {
            return (current * Quaternion.Euler(pitchDeg, yawDeg, rollDeg)).normalized;
        }

        /// <summary>
        /// Pure gate for the comfort-vignette drive in <see cref="Update"/>: only push intensity
        /// updates when the feature is enabled AND a vignette rig exists. Without the flag check,
        /// <see cref="SetComfortVignette"/>(false) (which zeroes intensity once) was immediately
        /// overridden back up by the very next frame's Update. Pure, so it is unit-testable.
        /// </summary>
        internal static bool ShouldDriveVignette(bool useFlag, bool hasVignette) => useFlag && hasVignette;

        /// <summary>Pure forward-speed composition: base * trim * (1 + boost). Extracted so
        /// <see cref="SpeedTrimMultiplier"/> and the sun-boost hook compose in <see cref="Update"/>'s
        /// integration line without ever needing to touch it again. Pure, so it is unit-testable.</summary>
        internal static float EffectiveSpeed(float baseSpeed, float trim, float boost) => baseSpeed * trim * (1f + boost);

        /// <summary>
        /// Wormhole jump entry point: instantly relocates the virtual ship to a new universe-local
        /// position, keeping the player's facing (<see cref="shipRot"/>) intact. Because the rig stays
        /// at the origin and the world is rendered as the inverse of the ship pose, moving the ship's
        /// internal position snaps a different part of the universe to the origin — a galaxy-scale jump
        /// with no scene load. Speed is gently zeroed for a calm arrival, and the universe transform is
        /// re-applied immediately (same two lines as <see cref="Update"/>) so there is no one-frame glitch.
        /// </summary>
        public void Teleport(Vector3 universeLocalPosition)
        {
            shipPos = universeLocalPosition;
            CurrentSpeed = 0f;
            pitchRate = 0f;
            yawRate = 0f;
            rollRate = 0f;

            if (universe == null) return;
            Quaternion inv = Quaternion.Inverse(shipRot);
            universe.SetPositionAndRotation(inv * (-shipPos), inv);
        }

        /// <summary>
        /// Teleport with rotation: relocates the virtual ship to a new universe-local position
        /// and sets its facing. Used by spawn placement to arrive facing a planet.
        /// </summary>
        public void Teleport(Vector3 universeLocalPosition, Quaternion rotation)
        {
            shipRot = rotation;
            Teleport(universeLocalPosition);
        }

        /// <summary>Discrete snap-yaw: fires one step when the stick is pushed past the threshold, re-arms at centre.</summary>
        private float StepYaw(float x)
        {
            if (Mathf.Abs(x) < snapYawThreshold)
            {
                snapArmed = true;
                return 0f;
            }
            if (!snapArmed) return 0f;
            snapArmed = false;
            return snapYawDegrees * Mathf.Sign(x);
        }

        /// <summary>Reads a stick with a rescaled deadzone (no edge jump) and a comfort response curve.</summary>
        private Vector2 Read(InputAction axis)
        {
            if (axis == null) return Vector2.zero;
            Vector2 v = axis.ReadValue<Vector2>();
            float mag = v.magnitude;
            if (mag < deadzone) return Vector2.zero;

            // Rescale [deadzone..1] -> [0..1] so output starts at 0 right at the deadzone edge,
            // then apply the response exponent for finer control near centre.
            float scaled = Mathf.InverseLerp(deadzone, 1f, mag);
            float curved = Mathf.Pow(scaled, responseExponent);
            return v.normalized * curved;
        }

    }
}

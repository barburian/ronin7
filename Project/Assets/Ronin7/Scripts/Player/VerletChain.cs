using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.8, R11: the Whip's chain. Plain verlet points with distance constraints, first point pinned to the handle
    /// ring, floor clamped, no walls. Light, lightest at the tip, and a constant lift ahead of the handle holds a resting chain in the air. Pure and allocation-free after construction. Steps in fixed substeps of real
    /// seconds, so the frame rate and Overdrive's time scale do not change how it moves; a hitch drops time
    /// past <see cref="MaxSubsteps"/> rather than catching up (no spiral of death).
    /// </summary>
    public sealed class VerletChain
    {
        public const int DefaultPoints = 10; // A15.11 first cut: 6
        public const float TotalLength = 4f; // A15.8 reach
        public const float SubstepSeconds = 1f / 120f;
        public const int MaxSubsteps = 4;
        public const int ConstraintIterations = 6;
        public const float Gravity = 9.81f;
        /// <summary>R11: share of <see cref="Gravity"/> per point, root to tip (lerped).</summary>
        public const float RootGravityScale = 0.3f, TipGravityScale = 0.03f;
        /// <summary>R11: acceleration (m/s2) along the lift direction per point, root to tip: what streams the chain out ahead of the hand.</summary>
        public const float RootLift = 4.5f, TipLift = 6f;
        /// <summary>R11: straight-up acceleration (m/s2) at the tip, none at the root: the resting chain dips, then rises to about chest height.</summary>
        public const float TipLiftUp = 2.75f;
        /// <summary>R11: tan of the steepest the handle's pitch tilts the lift (25 degrees). It never tilts below level, so a drooping wrist keeps the tip up.</summary>
        public const float LiftMaxPitchTan = 0.466f;
        /// <summary>Velocity kept per substep in the air (about 62% per second).</summary>
        public const float AirDrag = 0.996f;
        /// <summary>R11: below this point speed (m/s) a light chain settles on <see cref="SettleDrag"/> (about 16% per second): it sways, then stops, with no jitter, and a sweep's catch-up stays under the crack speed. Whip speeds keep <see cref="AirDrag"/>.</summary>
        public const float SettleSpeed = 6.5f, SettleDrag = 0.985f;
        /// <summary>Horizontal velocity kept per substep by a point on the floor.</summary>
        public const float FloorGrip = 0.8f;
        /// <summary>Hard speed cap (m/s) per point: what stops anything blowing up.</summary>
        public const float MaxPointSpeed = 60f;
        /// <summary>The pin moving further than this (m) in one tick is a teleport, not a swing: the chain is carried along with no velocity.</summary>
        public const float MaxPinJump = 1f;
        /// <summary>R11: launch impulse (m/s) at the tip, scaled down linearly to none at the pin. Straight ahead the near-taut chain keeps only 6 to 8; a steered tip has room and arrives faster (<see cref="SteerSpeed"/>).</summary>
        public const float LaunchSpeed = 14f;
        /// <summary>R11: upward impulse (m/s) mid-chain on a launch: a travelling arc, so straightening does not dive the tip.</summary>
        public const float LaunchLoft = 2f;
        /// <summary>R11: seconds after a launch before the snap back: an impulse (m/s at the tip) against the throw. <see cref="SnapBackDrag"/> runs for <see cref="SnapBackSeconds"/> after it and after any steer, so the chain cannot fly past the player or overhead.</summary>
        public const float LaunchFlightSeconds = 0.18f, SnapBackSpeed = 6f, SnapBackDrag = 0.95f, SnapBackSeconds = 0.4f;
        /// <summary>R11 (replaces A15.8's 10 degree bend): how long the tip steers, and the speed it steers to (m/s).</summary>
        public const float SteerSeconds = 0.35f, SteerSpeed = 18f;
        /// <summary>R11: the links that bend, tip first.</summary>
        public const int SteerLinks = 3;
        /// <summary>R11: the max steer strength (m/s2 at the tip, less down the steered links). A force over the flight, so it can never read as a teleport.</summary>
        public const float MaxSteerAcceleration = 90f;
        /// <summary>R11: 1/s, how hard the steer closes on its wanted velocity, up to <see cref="MaxSteerAcceleration"/>.</summary>
        public const float SteerGain = 25f;

        private readonly Vector3[] pos;
        private readonly Vector3[] prev;
        private readonly float segment;
        private float accumulator;
        private float maxSpeedSq;
        private bool tipPinned;
        private Vector3 tipTarget;
        private Vector3 liftDirection = Vector3.forward;
        private Vector3 steerTarget, launchDirection;
        private float flightLeft, snapLeft, steerLeft;

        public VerletChain(int points = DefaultPoints)
        {
            points = Mathf.Max(2, points);
            pos = new Vector3[points];
            prev = new Vector3[points];
            segment = TotalLength / (points - 1);
        }

        public int Count => pos.Length;

        /// <summary>The live positions (index 0 = pin, last = tip). Read only; never reallocated.</summary>
        public Vector3[] Positions => pos;

        /// <summary>Fastest non-pinned point (m/s) in the last substep.</summary>
        public float MaxSpeed => Mathf.Sqrt(maxSpeedSq);

        /// <summary>World velocity (m/s) of a point in the last substep.</summary>
        public Vector3 Velocity(int i) => (pos[i] - prev[i]) / SubstepSeconds;

        /// <summary>R11: true while the tip is steering at a target.</summary>
        public bool Steering => steerLeft > 0f;

        /// <summary>R11: true from a launch until its snap back.</summary>
        public bool InFlight => flightLeft > 0f;

        /// <summary>R11: where the last launch was thrown.</summary>
        public Vector3 LaunchDirection => launchDirection;

        /// <summary>Lays the chain straight out from <paramref name="pin"/> along flat <paramref name="layDirection"/>, close to its rest shape. Zero velocity.</summary>
        public void Reset(Vector3 pin, Vector3 layDirection, float floorY)
        {
            layDirection.y = 0f;
            layDirection = layDirection.sqrMagnitude > 1e-6f ? layDirection.normalized : Vector3.forward;
            for (int i = 0; i < pos.Length; i++) pos[i] = prev[i] = pin + layDirection * (i * segment);
            ClampToFloor(floorY);
            liftDirection = layDirection;
            accumulator = 0f;
            maxSpeedSq = 0f;
            CancelFlight();
        }

        /// <summary>R11: the chain streams out along the handle's flat forward, tilted up by its pitch (level to <see cref="LiftMaxPitchTan"/>). Handle straight up or down: no change.</summary>
        public void SetLiftDirection(Vector3 handleForward)
        {
            float flat = Mathf.Sqrt(handleForward.x * handleForward.x + handleForward.z * handleForward.z);
            if (flat < 0.05f) return;
            liftDirection = new Vector3(handleForward.x / flat, Mathf.Clamp(handleForward.y / flat, 0f, LiftMaxPitchTan), handleForward.z / flat).normalized;
        }

        /// <summary>
        /// R11: throws the chain, an impulse growing root to tip. With <paramref name="steer"/> each point flies at <paramref name="target"/> and the
        /// tip steers there; otherwise along <paramref name="direction"/>. Then it snaps back. Nothing moves here, only velocities, so nothing jumps.
        /// </summary>
        public void Launch(Vector3 direction, bool steer, Vector3 target)
        {
            int last = pos.Length - 1;
            for (int i = 1; i <= last; i++)
            {
                float t = i / (float)last;
                Vector3 to = target - pos[i];
                Vector3 along = steer && to.sqrMagnitude > 1e-6f ? to.normalized : direction;
                Vector3 velocity = along * (LaunchSpeed * t) + Vector3.up * (LaunchLoft * Mathf.Sin(Mathf.PI * t));
                prev[i] -= velocity * SubstepSeconds;
            }
            launchDirection = direction;
            flightLeft = LaunchFlightSeconds;
            snapLeft = 0f;
            if (steer) SteerTo(target);
        }

        /// <summary>R11: the tip steers at <paramref name="target"/> for <see cref="SteerSeconds"/>.</summary>
        public void SteerTo(Vector3 target)
        {
            steerTarget = target;
            steerLeft = SteerSeconds;
        }

        /// <summary>R11: a moving target, without extending the steer.</summary>
        public void UpdateSteerTarget(Vector3 target) => steerTarget = target;

        /// <summary>R11: the tip arrived, or its target is gone: stop steering and brake.</summary>
        public void EndSteer()
        {
            if (steerLeft <= 0f) return;
            steerLeft = 0f;
            snapLeft = SnapBackSeconds;
        }

        private void CancelFlight() => flightLeft = snapLeft = steerLeft = 0f;

        /// <summary>Advances by <paramref name="realDt"/> real seconds with the pin moving in a line to <paramref name="pin"/>.</summary>
        public void Tick(Vector3 pin, float realDt, float floorY)
        {
            Vector3 start = pos[0];
            Vector3 jump = pin - start;
            if (jump.sqrMagnitude > MaxPinJump * MaxPinJump)
            {
                for (int i = 0; i < pos.Length; i++) { pos[i] += jump; prev[i] += jump; }
                start = pin;
            }

            float carried = accumulator;
            float budget = Mathf.Min(Mathf.Max(0f, realDt), MaxSubsteps * SubstepSeconds - carried);
            accumulator = carried + budget;
            int steps = (int)(accumulator / SubstepSeconds);
            accumulator -= steps * SubstepSeconds;
            // Each substep sees the pin where it was at that substep's own time. Spreading the motion evenly over this
            // frame's substeps instead makes the pin's speed jitter at 72 Hz (1, 2, 2, 1 substeps), which halved the chain's speed there.
            for (int s = 1; s <= steps; s++) Step(Vector3.Lerp(start, pin, (s * SubstepSeconds - carried) / budget), floorY);
            pos[0] = pin;
            prev[0] = pin;
        }

        /// <summary>A15.9: holds the tip at <paramref name="target"/> until <see cref="UnpinTip"/>, at most <see cref="TotalLength"/> from the pin. The links relax between.</summary>
        public void PinTip(Vector3 target)
        {
            tipPinned = true;
            tipTarget = target;
            CancelFlight(); // R11: no launch or steer under a lasso
        }

        public void UnpinTip() => tipPinned = false;

        private void HoldTip(Vector3 pin)
        {
            if (!tipPinned) return;
            int tip = pos.Length - 1;
            pos[tip] = prev[tip] = pin + Vector3.ClampMagnitude(tipTarget - pin, TotalLength); // zero velocity: it leaves the pin with no kick
        }

        private void Step(Vector3 pin, float floorY)
        {
            int n = pos.Length;
            float maxStep = MaxPointSpeed * SubstepSeconds;
            float dtSq = SubstepSeconds * SubstepSeconds;
            float settleStepSq = SettleSpeed * SettleSpeed * dtSq;
            float toT = 1f / (n - 1);
            for (int i = 1; i < n; i++)
            {
                float t = i * toT;
                Vector3 v = pos[i] - prev[i];
                float keep = snapLeft > 0f ? SnapBackDrag : v.sqrMagnitude < settleStepSq ? SettleDrag : AirDrag;
                v = Vector3.ClampMagnitude(v * keep, maxStep);
                Vector3 accel = liftDirection * Mathf.Lerp(RootLift, TipLift, t);
                accel.y += TipLiftUp * t - Gravity * Mathf.Lerp(RootGravityScale, TipGravityScale, t);
                prev[i] = pos[i];
                pos[i] += v + accel * dtSq;
            }
            StepFlight(n, dtSq);
            pos[0] = pin;
            prev[0] = pin;

            for (int k = 0; k < ConstraintIterations; k++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 d = pos[i + 1] - pos[i];
                    float len = d.magnitude;
                    if (len < 1e-6f) continue; // coincident: no direction to push along
                    Vector3 fix = d * ((len - segment) / len);
                    if (i == 0) pos[1] -= fix; // the pin never moves
                    else { pos[i] += fix * 0.5f; pos[i + 1] -= fix * 0.5f; }
                }
                ClampToFloor(floorY);
                HoldTip(pin);
            }

            // Whatever the relaxation left over, the chain can never read longer than it is.
            for (int i = 1; i < n; i++)
            {
                Vector3 d = pos[i] - pos[i - 1];
                float len = d.magnitude;
                if (len > segment) pos[i] = pos[i - 1] + d * (segment / len);
            }
            ClampToFloor(floorY);
            HoldTip(pin);

            maxSpeedSq = 0f;
            float toSpeedSq = 1f / (SubstepSeconds * SubstepSeconds);
            for (int i = 1; i < n; i++)
            {
                float sq = (pos[i] - prev[i]).sqrMagnitude * toSpeedSq;
                if (sq > maxSpeedSq) maxSpeedSq = sq;
            }
        }

        // R11: forces only, so the bend grows over the flight and the constraints keep the links whole.
        private void StepFlight(int n, float dtSq)
        {
            if (steerLeft > 0f)
            {
                steerLeft -= SubstepSeconds;
                int first = Mathf.Max(1, n - SteerLinks);
                for (int i = first; i < n; i++)
                {
                    Vector3 to = steerTarget - pos[i];
                    float len = to.magnitude;
                    if (len < 1e-3f) continue;
                    Vector3 velocity = (pos[i] - prev[i]) / SubstepSeconds;
                    float weight = (i - first + 1f) / (n - first);
                    pos[i] += Vector3.ClampMagnitude((to / len * SteerSpeed - velocity) * SteerGain, MaxSteerAcceleration) * (weight * dtSq);
                }
                if (steerLeft <= 0f) snapLeft = SnapBackSeconds;
            }
            if (snapLeft > 0f) snapLeft -= SubstepSeconds;
            if (flightLeft <= 0f) return;
            flightLeft -= SubstepSeconds;
            if (flightLeft > 0f || steerLeft > 0f) return; // a steered flight ends in the brake above
            snapLeft = SnapBackSeconds;
            for (int i = 1; i < n; i++) prev[i] += launchDirection * (SnapBackSpeed * i / (n - 1f) * SubstepSeconds);
        }

        private void ClampToFloor(float floorY)
        {
            for (int i = 1; i < pos.Length; i++)
            {
                if (pos[i].y >= floorY) continue;
                pos[i].y = floorY;
                prev[i].y = floorY; // no bounce
                prev[i].x = pos[i].x + (prev[i].x - pos[i].x) * FloorGrip;
                prev[i].z = pos[i].z + (prev[i].z - pos[i].z) * FloorGrip;
            }
        }
    }
}

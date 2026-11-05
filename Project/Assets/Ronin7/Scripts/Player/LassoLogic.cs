using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.9 (pure): spin then throw. Fed one hand point per frame, in rig space so walking and snap turns do not count. The rotation rate is
    /// how fast the path of the Whip's ring turns: a wrist cannot twist continuously, so a real lasso circle tumbles the controller
    /// and its own angular velocity keeps changing axis, while the ring's path turns steadily about one axis, whether the arm or the wrist drives it.
    /// A spin needs the rate, the height and the axis held for <see cref="MinSpinSeconds"/> and <see cref="MinSpinDegrees"/>: one
    /// lobe of a figure-eight turns less than that before the direction flips. All numbers are first guesses, tune in the headset.
    /// </summary>
    public sealed class LassoGesture
    {
        /// <summary>Velocity is differenced at this interval, not per frame: tracking noise swamps a 90 Hz path angle.</summary>
        public const float SampleSeconds = 1f / 30f;
        public const float MinSpinDegreesPerSecond = 360f;
        public const float MinSpinSeconds = 0.6f;
        public const float MinSpinDegrees = 450f;
        /// <summary>Below this hand speed (m/s) the path has no meaningful direction.</summary>
        public const float MinHandSpeed = 1f;
        /// <summary>Cosine of how far the turn axis may stray from where the spin started (60 degrees).</summary>
        public const float AxisToleranceCos = 0.5f;
        /// <summary>Metres below the head that still counts as shoulder height.</summary>
        public const float ShoulderDrop = 0.35f;
        /// <summary>A spin survives a failed condition this long (a tilted circle dipping, a tracking blip).</summary>
        public const float GraceSeconds = 0.15f;
        public const float ThrowWindowSeconds = 0.6f;
        public const float ThrowSpeed = 3f;
        /// <summary>A throw beats the spin's peak hand speed by this much: an uneven (elliptical) spin is fastest on one side, and that side is not a throw.</summary>
        public const float ThrowOverSpinRatio = 1.1f;
        /// <summary>Cosine of the throw's flat angle from the head's forward (60 degrees).</summary>
        public const float ThrowConeCos = 0.5f;

        private Vector3 lastHand, spinAxis;
        private bool hasHand;
        private float elapsed, spinSeconds, spinRadians, spinPeak, badSeconds, armedLeft, armedSpeed;

        /// <summary>Last sampled hand velocity (m/s), in the space of the points given.</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Last sampled path rotation (rad/s, axis by <see cref="Vector3.Cross(Vector3, Vector3)"/>): +y turns +z toward +x.</summary>
        public Vector3 RotationRate { get; private set; }
        /// <summary>The flat direction of the last throw.</summary>
        public Vector3 ThrowDirection { get; private set; }
        public bool Armed => armedLeft > 0f;

        public void Reset()
        {
            hasHand = false;
            elapsed = armedLeft = armedSpeed = 0f;
            Velocity = RotationRate = Vector3.zero;
            ClearSpin();
        }

        /// <summary>True on the sample a throw lands.</summary>
        public bool Sample(Vector3 hand, Vector3 head, Vector3 headForward, float dt)
        {
            if (!hasHand)
            {
                hasHand = true;
                lastHand = hand;
                elapsed = 0f;
                return false;
            }
            elapsed += dt;
            if (elapsed < SampleSeconds) return false;
            float e = elapsed;
            elapsed = 0f;

            Vector3 before = Velocity;
            Velocity = (hand - lastHand) / e;
            lastHand = hand;
            float speed = Velocity.magnitude;
            RotationRate = Vector3.zero;
            if (speed >= MinHandSpeed && before.magnitude >= MinHandSpeed)
            {
                Vector3 cross = Vector3.Cross(before, Velocity);
                if (cross.sqrMagnitude > 1e-8f)
                    RotationRate = cross.normalized * (Mathf.Atan2(cross.magnitude, Vector3.Dot(before, Velocity)) / e);
            }

            if (armedLeft > 0f && IsThrow(speed, headForward))
            {
                armedLeft = 0f;
                ClearSpin();
                return true;
            }
            armedLeft -= e;

            float rate = RotationRate.magnitude;
            bool good = hand.y >= head.y - ShoulderDrop && rate >= MinSpinDegreesPerSecond * Mathf.Deg2Rad
                && (spinAxis == Vector3.zero || Vector3.Dot(RotationRate / rate, spinAxis) >= AxisToleranceCos);
            if (good)
            {
                if (spinAxis == Vector3.zero) spinAxis = RotationRate / rate;
                spinSeconds += e;
                spinRadians += rate * e;
                spinPeak = Mathf.Max(spinPeak, speed);
                badSeconds = 0f;
                if (spinSeconds >= MinSpinSeconds && spinRadians >= MinSpinDegrees * Mathf.Deg2Rad)
                {
                    armedLeft = ThrowWindowSeconds;
                    armedSpeed = spinPeak;
                }
            }
            else
            {
                badSeconds += e;
                if (badSeconds > GraceSeconds) ClearSpin();
            }
            return false;
        }

        private bool IsThrow(float speed, Vector3 headForward)
        {
            if (speed < ThrowSpeed || speed < ThrowOverSpinRatio * armedSpeed) return false;
            Vector3 flat = Velocity;
            flat.y = 0f;
            headForward.y = 0f;
            if (flat.sqrMagnitude < 1e-6f || headForward.sqrMagnitude < 1e-6f) return false;
            if (Vector3.Dot(flat.normalized, headForward.normalized) < ThrowConeCos) return false;
            ThrowDirection = flat.normalized;
            return true;
        }

        private void ClearSpin()
        {
            spinAxis = Vector3.zero;
            spinSeconds = spinRadians = spinPeak = badSeconds = 0f;
        }
    }

    /// <summary>A15.9 (pure): the captive's scripted path. Angles are about +y from +z toward +x. Only x/z are constrained; heights are the caller's.</summary>
    public static class LassoOrbit
    {
        public const float MinRadius = 2.5f, MaxRadius = 3f;
        /// <summary>Metres the body is lifted off its floor latch while it orbits.</summary>
        public const float Lift = 0.5f;
        /// <summary>Nothing comes closer to the head than this, measured flat (so a vertical cylinder, stricter than a sphere).</summary>
        public const float HeadSafeRadius = 1.5f;
        /// <summary>m/s. Also caps the orbit's angular speed, so a fast spin cannot teleport or tunnel.</summary>
        public const float MaxSpeed = 12f;
        public const float MaxAngularSpeed = MaxSpeed / MaxRadius;
        /// <summary>rad/s of orbit per rad/s of hand spin.</summary>
        public const float SpinGain = 1f;
        /// <summary>Metres of arm lever: a sideways hand sweep at v m/s drives v / this rad/s.</summary>
        public const float SweepLever = 0.5f;
        /// <summary>A spin axis tipped this far below level (fraction of the rate) turns the orbit backward. Less, like an upright wheel, stays forward: no flicker.</summary>
        public const float ReverseAxisShare = 0.25f;
        public const float FlingSeconds = 0.6f, FlingApex = 0.6f;
        /// <summary>Hand speed to fling speed: the chain is a lever.</summary>
        public const float FlingSpeedScale = 2f;
        public const float AimRange = 6f, AimHalfAngle = 35f;
        private const int SlideSteps = 24;

        /// <summary>Signed orbit rate from the hand: its spin plus any sideways sweep past the captive, capped.</summary>
        public static float AngularSpeed(Vector3 rotationRate, Vector3 handVelocity, Vector3 head, Vector3 captive)
        {
            float rate = rotationRate.magnitude;
            float spin = rotationRate.y < -ReverseAxisShare * rate ? -rate : rate;
            Vector3 radial = captive - head;
            radial.y = 0f;
            float sweep = radial.sqrMagnitude > 1e-6f ? Vector3.Dot(handVelocity, Vector3.Cross(Vector3.up, radial.normalized)) / SweepLever : 0f;
            return Mathf.Clamp(spin * SpinGain + sweep, -MaxAngularSpeed, MaxAngularSpeed);
        }

        /// <summary>The ring point at <paramref name="angle"/> (rad): out to <see cref="MaxRadius"/> the faster it spins.</summary>
        public static Vector3 RingPoint(Vector3 head, float angle, float angularSpeed, float groundY)
        {
            float r = Mathf.Lerp(MinRadius, MaxRadius, Mathf.Abs(angularSpeed) / MaxAngularSpeed);
            return new Vector3(head.x + Mathf.Sin(angle) * r, groundY + Lift, head.z + Mathf.Cos(angle) * r);
        }

        /// <summary>Inside the arena and outside the head radius. Pressed between the two, it slides around the head circle to the
        /// nearest open spot; an arena too small for both keeps the arena (a body past a wall is killed by CheckBounds).</summary>
        public static Vector3 Constrain(Vector3 p, Vector3 head, float halfExtent)
        {
            p = Enemy.ClampToArena(p, halfExtent);
            Vector2 d = new Vector2(p.x - head.x, p.z - head.z);
            if (d.sqrMagnitude >= HeadSafeRadius * HeadSafeRadius) return p;
            float from = d.sqrMagnitude > 1e-6f ? Mathf.Atan2(d.x, d.y) : 0f;
            for (int i = 0; i <= SlideSteps / 2; i++)
            {
                for (int side = i == 0 ? 1 : -1; side <= 1; side += 2)
                {
                    float a = from + side * i * (2f * Mathf.PI / SlideSteps);
                    Vector3 q = new Vector3(head.x + Mathf.Sin(a) * HeadSafeRadius, p.y, head.z + Mathf.Cos(a) * HeadSafeRadius);
                    if (Enemy.ClampToArena(q, halfExtent) == q) return q;
                }
            }
            return p;
        }

        /// <summary>One capped move toward a constrained target, itself constrained.</summary>
        public static Vector3 Step(Vector3 current, Vector3 target, Vector3 head, float halfExtent, float dt) =>
            Constrain(Vector3.MoveTowards(current, Constrain(target, head, halfExtent), MaxSpeed * dt), head, halfExtent);

        /// <summary>The fling's flat world velocity from the hand's: along the hand, capped.</summary>
        public static Vector3 FlingVelocity(Vector3 handVelocity)
        {
            handVelocity.y = 0f;
            return Vector3.ClampMagnitude(handVelocity * FlingSpeedScale, MaxSpeed);
        }

        /// <summary>The arc at <paramref name="t"/> s from <paramref name="start"/>, on the floor at <see cref="FlingSeconds"/>.</summary>
        public static Vector3 FlingPoint(Vector3 start, Vector3 flatVelocity, float groundY, float t)
        {
            float k = Mathf.Clamp01(t / FlingSeconds);
            Vector3 p = start + flatVelocity * (k * FlingSeconds);
            p.y = Mathf.Lerp(start.y, groundY, k) + 4f * FlingApex * k * (1f - k);
            return p;
        }

        /// <summary>True if <paramref name="position"/> is in range and in the cone around the flat <paramref name="aim"/>; <paramref name="angle"/> ranks "nearest the aim".</summary>
        public static bool InAim(Vector3 origin, Vector3 aim, Vector3 position, out float angle)
        {
            Vector3 to = position - origin;
            to.y = 0f;
            aim.y = 0f;
            angle = Vector3.Angle(aim, to);
            return to.sqrMagnitude <= AimRange * AimRange && angle <= AimHalfAngle;
        }
    }
}

using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.8: the Whip's chain. Plain verlet points with distance constraints, first point pinned to the handle
    /// ring, floor clamped, no walls. Pure and allocation-free after construction. Steps in fixed substeps of real
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
        /// <summary>Velocity kept per substep in the air (about 62% per second).</summary>
        public const float AirDrag = 0.996f;
        /// <summary>Horizontal velocity kept per substep by a point on the floor.</summary>
        public const float FloorGrip = 0.8f;
        /// <summary>Hard speed cap (m/s) per point: what stops anything blowing up.</summary>
        public const float MaxPointSpeed = 60f;
        /// <summary>The pin moving further than this (m) in one tick is a teleport, not a swing: the chain is carried along with no velocity.</summary>
        public const float MaxPinJump = 1f;

        private readonly Vector3[] pos;
        private readonly Vector3[] prev;
        private readonly float segment;
        private float accumulator;
        private float maxSpeedSq;

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

        /// <summary>Hangs the chain from <paramref name="pin"/> to the floor, the rest lying along <paramref name="layDirection"/>. Zero velocity.</summary>
        public void Reset(Vector3 pin, Vector3 layDirection, float floorY)
        {
            layDirection.y = 0f;
            layDirection = layDirection.sqrMagnitude > 1e-6f ? layDirection.normalized : Vector3.forward;
            Vector3 p = pin;
            pos[0] = prev[0] = pin;
            for (int i = 1; i < pos.Length; i++)
            {
                float height = Mathf.Max(0f, p.y - floorY);
                // Every link exactly one segment, the one across the corner too: a short one would spring out as a fake swing.
                p = height >= segment
                    ? p + Vector3.down * segment
                    : new Vector3(p.x, floorY, p.z) + layDirection * Mathf.Sqrt(segment * segment - height * height);
                pos[i] = prev[i] = p;
            }
            accumulator = 0f;
            maxSpeedSq = 0f;
        }

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

        private void Step(Vector3 pin, float floorY)
        {
            int n = pos.Length;
            float maxStep = MaxPointSpeed * SubstepSeconds;
            float gravityStep = Gravity * SubstepSeconds * SubstepSeconds;
            for (int i = 1; i < n; i++)
            {
                Vector3 v = Vector3.ClampMagnitude((pos[i] - prev[i]) * AirDrag, maxStep);
                prev[i] = pos[i];
                pos[i] += v;
                pos[i].y -= gravityStep;
            }
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
            }

            // Whatever the relaxation left over, the chain can never read longer than it is.
            for (int i = 1; i < n; i++)
            {
                Vector3 d = pos[i] - pos[i - 1];
                float len = d.magnitude;
                if (len > segment) pos[i] = pos[i - 1] + d * (segment / len);
            }
            ClampToFloor(floorY);

            maxSpeedSq = 0f;
            float toSpeedSq = 1f / (SubstepSeconds * SubstepSeconds);
            for (int i = 1; i < n; i++)
            {
                float sq = (pos[i] - prev[i]).sqrMagnitude * toSpeedSq;
                if (sq > maxSpeedSq) maxSpeedSq = sq;
            }
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

using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P2-11 pure kinematics for enemy movement polish: a rate-limited yaw turn, a speed ramp with
    /// accel/decel limits plus arrival braking, and a walk-cycle phase driven by travelled distance.
    /// Visual/kinematic only: nothing here touches attack timing or parry windows. No allocations.
    /// </summary>
    internal static class MovementSmoothing
    {
        /// <summary>Next yaw (degrees). Small corrections ease in exactly like the old
        /// Slerp(easeRate x dt) tracking; large ones are capped at <paramref name="maxDegPerSec"/>.</summary>
        internal static float StepYaw(float yaw, float targetYaw, float maxDegPerSec, float easeRate, float dt)
        {
            float err = Mathf.DeltaAngle(yaw, targetYaw);
            float step = Mathf.Min(maxDegPerSec * dt, Mathf.Abs(err) * (1f - Mathf.Exp(-easeRate * dt)));
            return yaw + Mathf.Sign(err) * step;
        }

        /// <summary>Next speed: moves toward <paramref name="targetSpeed"/> by at most accel x dt
        /// when speeding up and decel x dt when slowing down.</summary>
        internal static float StepSpeed(float speed, float targetSpeed, float accel, float decel, float dt)
            => Mathf.MoveTowards(speed, targetSpeed, (targetSpeed > speed ? accel : decel) * dt);

        /// <summary>Fastest speed that can still stop within <paramref name="distanceToStop"/> at
        /// <paramref name="decel"/>, capped at <paramref name="maxSpeed"/> (arrival braking).</summary>
        internal static float ArrivalSpeed(float maxSpeed, float distanceToStop, float decel)
            => Mathf.Min(maxSpeed, Mathf.Sqrt(2f * decel * Mathf.Max(0f, distanceToStop)));

        /// <summary>Walk-cycle phase (radians) after travelling <paramref name="travelled"/> metres:
        /// one full cycle (two steps) per 2 x stride, independent of frame rate or speed estimate.</summary>
        internal static float AdvanceWalkPhase(float phase, float travelled, float strideLength)
            => (phase + Mathf.PI * travelled / strideLength) % (2f * Mathf.PI);
    }
}

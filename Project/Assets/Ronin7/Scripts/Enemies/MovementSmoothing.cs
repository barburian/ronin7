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

        // ---- P3-09 (BUG-38): drone hover + boss weight ----
        internal const float MaxBankDegrees = 18f, BankAccelForMax = 3f, BankEase = 8f;
        internal const float HoverBobAmplitude = 0.05f, HoverBobHz = 0.5f;
        internal const float BossGateFullDeg = 10f, BossGateZeroDeg = 45f, BossBrakeScale = 0.7f;

        /// <summary>Next planar velocity: accel-limited toward <paramref name="want"/> (decel-limited when
        /// slowing), so a reversal passes through zero over several frames instead of flipping.</summary>
        internal static Vector3 StepVelocity(Vector3 v, Vector3 want, float accel, float decel, float dt)
            => Vector3.MoveTowards(v, want, (want.sqrMagnitude > v.sqrMagnitude ? accel : decel) * dt);

        /// <summary>Signed bank (degrees, + = lean right) for a body-right acceleration, clamped at MaxBankDegrees.</summary>
        internal static float BankTarget(float lateralAccel)
            => Mathf.Clamp(lateralAccel / BankAccelForMax, -1f, 1f) * MaxBankDegrees;

        /// <summary>Frame-rate independent exponential ease of <paramref name="cur"/> toward <paramref name="target"/>.</summary>
        internal static float StepEase(float cur, float target, float rate, float dt)
            => Mathf.Lerp(cur, target, 1f - Mathf.Exp(-rate * dt));

        /// <summary>Vertical hover bob (metres).</summary>
        internal static float HoverBob(float time, float phase)
            => Mathf.Sin(2f * Mathf.PI * HoverBobHz * time + phase) * HoverBobAmplitude;

        /// <summary>Boss travel gate 0..1: 0 while the yaw error exceeds BossGateZeroDeg (turn in place),
        /// 1 once it is under BossGateFullDeg.</summary>
        internal static float TurnGate(float yawErrorDeg)
            => 1f - Mathf.InverseLerp(BossGateFullDeg, BossGateZeroDeg, Mathf.Abs(yawErrorDeg));
    }
}

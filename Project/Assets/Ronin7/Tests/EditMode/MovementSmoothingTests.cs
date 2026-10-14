using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P2-11: enemy movement kinematics (turn rate, speed ramp, arrival braking) and a walk
    /// cycle driven by travelled distance, so feet don't slide while the body speeds up or slows down.</summary>
    public class MovementSmoothingTests
    {
        [Test]
        public void Smoothing_TurnRate_NeverExceedsLimit()
        {
            const float limit = 240f;
            foreach (float dt in new[] { 1f / 90f, 1f / 72f, 0.05f })
            {
                float yaw = 0f;
                for (int i = 0; i < 400; i++)
                {
                    float target = i < 200 ? 180f : -90f; // full reversal, then a 270 degree swing back
                    float next = MovementSmoothing.StepYaw(yaw, target, limit, 8f, dt);
                    Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(yaw, next)) / dt, limit + 1e-3f, $"dt {dt} frame {i}");
                    yaw = next;
                }
                Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, -90f), 0.5f, "still converges on the target");
            }
        }

        [Test]
        public void Smoothing_SpeedRamp_NoStepChange()
        {
            const float accel = 4f, decel = 6f, dt = 1f / 90f;
            float v = 0f;
            for (int i = 0; i < 300; i++)
            {
                float target = i < 150 ? 1.6f : 0f; // set off at full walk speed, then stop dead
                float next = MovementSmoothing.StepSpeed(v, target, accel, decel, dt);
                float limit = (next > v ? accel : decel) * dt;
                Assert.LessOrEqual(Mathf.Abs(next - v), limit + 1e-5f, $"frame {i}");
                v = next;
            }
            Assert.AreEqual(0f, v, 1e-5f);
        }

        [Test]
        public void Smoothing_ArrivalBraking_StopsAtRangeWithoutOvershoot()
        {
            const float range = 1.7f, maxSpeed = 1.6f, accel = 4f, decel = 6f, dt = 1f / 90f;
            float dist = 5f, v = 0f;
            int frames = 0;
            while (dist > range && frames++ < 2000)
            {
                float want = MovementSmoothing.ArrivalSpeed(maxSpeed, dist - range - v * dt, decel); // as Enemy.OnChaseTick
                float next = MovementSmoothing.StepSpeed(v, want, accel, decel, dt);
                Assert.LessOrEqual(Mathf.Abs(next - v), Mathf.Max(accel, decel) * dt + 1e-5f, $"frame {frames}");
                v = next;
                dist -= v * dt;
            }
            Assert.Less(frames, 2000, "arrives in finite time");
            Assert.Greater(dist, range - 0.01f, "brakes into range, does not slide past it");
            Assert.LessOrEqual(v, accel * dt + 1e-5f, "arrives nearly stopped (no hard stop from full speed)");
        }

        [Test]
        public void Smoothing_WalkPhase_DrivenByTravelledDistance()
        {
            var root = new GameObject("Npc");
            try
            {
                var rig = new GameObject(NpcWalkAnimator.RootBoneName).transform; rig.SetParent(root.transform);
                new GameObject(NpcWalkAnimator.LegLBoneName).transform.SetParent(rig);
                new GameObject(NpcWalkAnimator.LegRBoneName).transform.SetParent(rig);
                var walk = root.AddComponent<NpcWalkAnimator>();
                typeof(NpcWalkAnimator).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(walk, null);

                // 1.3 m = one full cycle at the default 0.65 m stride, travelled in uneven chunks
                // (speeding up and slowing down) at a fixed 90 Hz: the cycle must land exactly where
                // the distance says, not where a lagging speed estimate says.
                float[] chunks = { 0.05f, 0.3f, 0.01f, 0.2f, 0.24f, 0.1f, 0.15f, 0.05f, 0.2f };
                foreach (float d in chunks)
                {
                    root.transform.position += Vector3.forward * d;
                    walk.Tick(1f / 90f);
                }
                Assert.AreEqual(0f, Mathf.Sin(walk.Phase), 1e-3f);
                Assert.AreEqual(1f, Mathf.Cos(walk.Phase), 1e-3f);

                // Standing still never advances the cycle (feet stay planted).
                float before = walk.Phase;
                walk.Tick(1f / 90f);
                Assert.AreEqual(before, walk.Phase, 1e-6f);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

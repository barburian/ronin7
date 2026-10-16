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

        // ---- P3-09 (BUG-38) ----
        [Test]
        public void Smoothing_Drone_BankFollowsLateralAccel()
        {
            Assert.AreEqual(0f, MovementSmoothing.BankTarget(0f), 1e-5f);
            Assert.Greater(MovementSmoothing.BankTarget(1.5f), 0f, "right accel banks right");
            Assert.Less(MovementSmoothing.BankTarget(-1.5f), 0f, "left accel banks left");
            Assert.Greater(MovementSmoothing.BankTarget(2f), MovementSmoothing.BankTarget(1f), "monotone");
            Assert.AreEqual(MovementSmoothing.MaxBankDegrees, MovementSmoothing.BankTarget(99f), 1e-4f, "clamped");
            // Eased bank approaches the target without overshoot.
            float bank = 0f, target = MovementSmoothing.BankTarget(3f);
            for (int i = 0; i < 90; i++) { bank = MovementSmoothing.StepEase(bank, target, MovementSmoothing.BankEase, 1f / 90f); Assert.LessOrEqual(bank, target + 1e-4f); }
            Assert.AreEqual(target, bank, 0.1f * target);
        }

        [Test]
        public void Smoothing_Drone_ReversalHasNoInstantFlip()
        {
            const float dt = 1f / 90f, accel = 3f, decel = 3f;
            Vector3 v = Vector3.right * 1.3f, want = Vector3.left * 1.3f;
            for (int i = 0; i < 300; i++)
            {
                Vector3 next = MovementSmoothing.StepVelocity(v, want, accel, decel, dt);
                Assert.LessOrEqual((next - v).magnitude, accel * dt + 1e-5f, $"frame {i}");
                v = next;
            }
            Assert.AreEqual(-1.3f, v.x, 1e-3f, "reaches the reversed velocity");
        }

        [Test]
        public void Smoothing_Boss_TurnInPlaceBeforeMove()
        {
            Assert.AreEqual(0f, MovementSmoothing.TurnGate(90f), "no travel while far off-heading");
            Assert.AreEqual(0f, MovementSmoothing.TurnGate(-90f));
            Assert.AreEqual(1f, MovementSmoothing.TurnGate(3f), "full travel once facing");
            Assert.Greater(MovementSmoothing.TurnGate(25f), 0f);
            Assert.Less(MovementSmoothing.TurnGate(25f), 1f);
            // A 150 deg/s boss facing away needs time to turn before the gate opens.
            float yaw = 0f; int frames = 0;
            while (MovementSmoothing.TurnGate(Mathf.DeltaAngle(yaw, 180f)) <= 0f && frames < 1000)
            { yaw = MovementSmoothing.StepYaw(yaw, 180f, 150f, 8f, 1f / 90f); frames++; }
            Assert.Greater(frames, 60, "spends real time turning in place first");
        }
    }
}

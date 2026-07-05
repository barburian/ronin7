using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the pure rules behind the VR parkour kit: the wall-run gravity gate
    /// (<see cref="ContinuousLocomotion.WallRunGravityScale"/>), the wall-jump launch
    /// (<see cref="ContinuousLocomotion.WallJumpVelocity"/>), and the climb-release fling cap
    /// (<see cref="WallClimbLocomotion.ClampFling"/>).
    /// </summary>
    public class ParkourSolverTests
    {
        // ---- wall run: gravity is only ever reduced inside the full gate ----

        [Test]
        public void WallRun_AllConditionsMet_ScalesGravity()
        {
            float s = ContinuousLocomotion.WallRunGravityScale(
                grounded: false, running: true, horizontalSpeed: 5f, minSpeed: 3.5f,
                wallAdjacent: true, timer: 0.5f, maxSeconds: 2.5f, scale: 0.25f);
            Assert.AreEqual(0.25f, s, 1e-5f);
        }

        [Test]
        public void WallRun_GateFailures_KeepFullGravity()
        {
            // grounded / not running / no wall / too slow / window expired — each alone kills it.
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(true, true, 5f, 3.5f, true, 0f, 2.5f, 0.25f));
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(false, false, 5f, 3.5f, true, 0f, 2.5f, 0.25f));
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(false, true, 5f, 3.5f, false, 0f, 2.5f, 0.25f));
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(false, true, 2f, 3.5f, true, 0f, 2.5f, 0.25f));
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(false, true, 5f, 3.5f, true, 2.5f, 2.5f, 0.25f));
        }

        [Test]
        public void WallRun_ScaleIsClampedTo01()
        {
            Assert.AreEqual(1f, ContinuousLocomotion.WallRunGravityScale(false, true, 5f, 3.5f, true, 0f, 2.5f, 7f));
            Assert.AreEqual(0f, ContinuousLocomotion.WallRunGravityScale(false, true, 5f, 3.5f, true, 0f, 2.5f, -1f));
        }

        // ---- wall jump ----

        [Test]
        public void WallJump_PushesAlongFlattenedNormal_PlusTakeoff()
        {
            Vector3 v = ContinuousLocomotion.WallJumpVelocity(new Vector3(1f, 0.8f, 0f), 3.5f, 4.2f);
            Assert.AreEqual(3.5f, v.x, 1e-4f, "Push must use the FLAT normal, at full push speed.");
            Assert.AreEqual(4.2f, v.y, 1e-4f);
            Assert.AreEqual(0f, v.z, 1e-4f);
        }

        [Test]
        public void WallJump_DegenerateNormal_StillJumpsStraightUp()
        {
            Vector3 v = ContinuousLocomotion.WallJumpVelocity(Vector3.up, 3.5f, 4.2f);
            Assert.AreEqual(0f, new Vector3(v.x, 0f, v.z).magnitude, 1e-4f);
            Assert.AreEqual(4.2f, v.y, 1e-4f);
        }

        // ---- climb-release fling ----

        [Test]
        public void ClampFling_ScalesBelowCap_ClampsAboveIt()
        {
            Vector3 gentle = WallClimbLocomotion.ClampFling(new Vector3(0f, 2f, 0f), 1.1f, 5.5f);
            Assert.AreEqual(2.2f, gentle.y, 1e-4f, "Below the cap the fling scales linearly.");

            Vector3 spike = WallClimbLocomotion.ClampFling(new Vector3(0f, 40f, 0f), 1.1f, 5.5f);
            Assert.AreEqual(5.5f, spike.magnitude, 1e-3f, "A tracking spike must clamp to the max launch speed.");
            Assert.Greater(spike.y, 0f, "Clamping preserves direction.");
        }
    }
}

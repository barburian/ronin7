using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure yaw-fade math behind <see cref="NpcGazeGlance"/>. Mirrors
    /// <c>NpcRigSolverTests</c>' style: a plain-value solver with no MonoBehaviour involved.
    /// </summary>
    public class NpcGazeSolverTests
    {
        [Test]
        public void PlayerDirectlyAhead_ReturnsZero()
        {
            float yaw = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f), 4f, 35f);
            Assert.AreEqual(0f, yaw, 1e-4f);
        }

        [Test]
        public void PlayerToSide_ReturnsClampedSignedAngle()
        {
            // Target is 1m to the right: raw signed angle is 90°, clamped to maxYaw (35°),
            // then faded by (1 - dist/radius) = (1 - 1/4) = 0.75 → 35 * 0.75 = 26.25.
            float yaw = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 0f), 4f, 35f);
            Assert.AreEqual(26.25f, yaw, 1e-3f);
        }

        [Test]
        public void BeyondProximityRadius_ReturnsZero()
        {
            float yaw = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 5f), 4f, 35f);
            Assert.AreEqual(0f, yaw, 1e-4f);
        }

        [Test]
        public void AtProximityRadius_ReturnsZero()
        {
            float yaw = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 4f), 4f, 35f);
            Assert.AreEqual(0f, yaw, 1e-4f);
        }

        [Test]
        public void FadesLinearlyWithDistance()
        {
            // Same direction (raw angle always clamped to 35°) at two distances: fade should scale
            // the result linearly with (1 - dist/radius).
            float yawNear = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 0f), 4f, 35f); // fade 0.75 -> 26.25
            float yawFar = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(2f, 0f, 0f), 4f, 35f); // fade 0.50 -> 17.5

            Assert.AreEqual(26.25f, yawNear, 1e-3f);
            Assert.AreEqual(17.5f, yawFar, 1e-3f);
            Assert.AreEqual(1.5f, yawNear / yawFar, 1e-3f);
        }

        [Test]
        public void PlayerBehind_ClampsToMaxYaw()
        {
            // Target directly behind: raw signed angle is ±180° (sign is numerically ambiguous for
            // exactly-opposite vectors), clamped to ±maxYaw, then faded by (1 - 1/4) = 0.75.
            float yaw = NpcGazeSolver.SolveGazeYawDegrees(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 4f, 35f);
            Assert.AreEqual(26.25f, Mathf.Abs(yaw), 1e-3f);
        }
    }
}

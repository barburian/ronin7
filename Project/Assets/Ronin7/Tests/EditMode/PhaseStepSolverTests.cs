using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="PhaseStepSolver"/>'s pure destination math behind Ch10's Phase-step ability.
    /// Mirrors <c>WeakpointSightLogicTests</c>' style.
    /// </summary>
    public class PhaseStepSolverTests
    {
        [Test]
        public void ComputeDestination_BasicCase_MovesFullDistanceAlongDirection()
        {
            var origin = new Vector3(1f, 2f, 3f);
            var dest = PhaseStepSolver.ComputeDestination(origin, Vector3.forward, 5f);
            Assert.AreEqual(new Vector3(1f, 2f, 8f), dest);
        }

        [Test]
        public void ComputeDestination_ZeroDirection_ReturnsOriginUnchanged()
        {
            var origin = new Vector3(4f, 0f, -2f);
            var dest = PhaseStepSolver.ComputeDestination(origin, Vector3.zero, 5f);
            Assert.AreEqual(origin, dest);
        }

        [Test]
        public void ComputeDestination_NearZeroDirection_ReturnsOriginUnchanged()
        {
            var origin = new Vector3(4f, 0f, -2f);
            var dest = PhaseStepSolver.ComputeDestination(origin, new Vector3(0.0001f, 0f, 0f), 5f);
            Assert.AreEqual(origin, dest);
        }

        [Test]
        public void ComputeDestination_DirectionIsNormalized_RegardlessOfInputMagnitude()
        {
            var origin = Vector3.zero;
            var destShort = PhaseStepSolver.ComputeDestination(origin, new Vector3(2f, 0f, 0f), 3f);
            var destLong = PhaseStepSolver.ComputeDestination(origin, new Vector3(50f, 0f, 0f), 3f);
            Assert.AreEqual(destShort, destLong);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), destShort);
        }

        [Test]
        public void ComputeDestination_DiagonalDirection_NormalizedCorrectly()
        {
            var origin = Vector3.zero;
            var dest = PhaseStepSolver.ComputeDestination(origin, new Vector3(1f, 0f, 1f), Mathf.Sqrt(2f));
            Assert.AreEqual(1f, dest.x, 0.0001f);
            Assert.AreEqual(0f, dest.y, 0.0001f);
            Assert.AreEqual(1f, dest.z, 0.0001f);
        }

        [Test]
        public void ComputeDestination_NegativeMaxDistance_ClampsToZero()
        {
            var origin = new Vector3(1f, 1f, 1f);
            var dest = PhaseStepSolver.ComputeDestination(origin, Vector3.forward, -5f);
            Assert.AreEqual(origin, dest);
        }

        [Test]
        public void ComputeDestination_ZeroMaxDistance_ReturnsOrigin()
        {
            var origin = new Vector3(1f, 1f, 1f);
            var dest = PhaseStepSolver.ComputeDestination(origin, Vector3.forward, 0f);
            Assert.AreEqual(origin, dest);
        }
    }
}

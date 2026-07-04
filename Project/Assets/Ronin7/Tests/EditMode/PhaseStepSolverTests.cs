using NUnit.Framework;
using Ronin7.Combat;
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

    /// <summary>
    /// Guards <see cref="PhaseStepController.IsLandableGround"/>: the ground probe must land on world
    /// geometry only — never on enemies (anything with a <see cref="Health"/> in its hierarchy) or on
    /// loose non-kinematic physics props. The project defines no gameplay layers, so this component
    /// discrimination IS the probe's filtering.
    /// </summary>
    public class PhaseStepGroundProbeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void IsLandableGround_PlainStaticCollider_IsLandable()
        {
            _go = new GameObject("Floor");
            var collider = _go.AddComponent<BoxCollider>();

            Assert.IsTrue(PhaseStepController.IsLandableGround(collider));
        }

        [Test]
        public void IsLandableGround_ColliderWithHealth_IsNotLandable()
        {
            _go = new GameObject("Enemy");
            _go.AddComponent<Health>();
            var collider = _go.AddComponent<BoxCollider>();

            Assert.IsFalse(PhaseStepController.IsLandableGround(collider));
        }

        [Test]
        public void IsLandableGround_ColliderUnderHealthParent_IsNotLandable()
        {
            _go = new GameObject("EnemyRoot");
            _go.AddComponent<Health>();
            var limb = new GameObject("Limb");
            limb.transform.SetParent(_go.transform);
            var collider = limb.AddComponent<BoxCollider>();

            Assert.IsFalse(PhaseStepController.IsLandableGround(collider));
        }

        [Test]
        public void IsLandableGround_DynamicRigidbodyProp_IsNotLandable()
        {
            _go = new GameObject("Crate");
            var collider = _go.AddComponent<BoxCollider>();
            var body = _go.AddComponent<Rigidbody>();
            body.isKinematic = false;

            Assert.IsFalse(PhaseStepController.IsLandableGround(collider));
        }

        [Test]
        public void IsLandableGround_KinematicRigidbodyMover_IsLandable()
        {
            _go = new GameObject("Platform");
            var collider = _go.AddComponent<BoxCollider>();
            var body = _go.AddComponent<Rigidbody>();
            body.isKinematic = true;

            Assert.IsTrue(PhaseStepController.IsLandableGround(collider));
        }

        [Test]
        public void IsLandableGround_NullCollider_IsNotLandable()
        {
            Assert.IsFalse(PhaseStepController.IsLandableGround(null));
        }
    }
}

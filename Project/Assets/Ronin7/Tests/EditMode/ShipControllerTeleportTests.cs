using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="ShipController.Teleport(Vector3)"/>: a wormhole jump zeroes CurrentSpeed for
    /// a calm arrival, but was leaving the smoothed steering rates (pitchRate/yawRate/rollRate)
    /// untouched, so residual rotation from just before the jump kept spinning the world afterward.
    /// </summary>
    public class ShipControllerTeleportTests
    {
        private GameObject go;
        private ShipController controller;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Awake/OnEnable (which resolve input actions and log an error when
            // unassigned) never run in EditMode — Teleport doesn't depend on either having fired.
            go = new GameObject();
            go.SetActive(false);
            controller = go.AddComponent<ShipController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Teleport_ZeroesResidualSteeringRates()
        {
            controller.pitchRate = 12f;
            controller.yawRate = -7f;
            controller.rollRate = 3f;

            controller.Teleport(Vector3.zero);

            Assert.AreEqual(0f, controller.pitchRate);
            Assert.AreEqual(0f, controller.yawRate);
            Assert.AreEqual(0f, controller.rollRate);
        }
    }
}

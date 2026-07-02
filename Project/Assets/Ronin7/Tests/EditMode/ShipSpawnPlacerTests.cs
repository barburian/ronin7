using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ShipSpawnPlacerTests
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void ComputeSpawnPose_PlanetOffset_PositionsAtStandoff()
        {
            // Arrange
            Vector3 planetPos = new Vector3(1000, 0, 0);
            float standoff = 150f;

            // Act
            ShipSpawnPlacer.ComputeSpawnPose(planetPos, standoff, out Vector3 position, out Quaternion rotation);

            // Assert - position should be 150 units from planet, toward origin
            Assert.AreEqual(850f, position.x, Tolerance);
            Assert.AreEqual(0f, position.y, Tolerance);
            Assert.AreEqual(0f, position.z, Tolerance);

            float distanceToPlanet = Vector3.Distance(position, planetPos);
            Assert.AreEqual(standoff, distanceToPlanet, Tolerance);
        }

        [Test]
        public void ComputeSpawnPose_PlanetOffset_RotationPointsAtPlanet()
        {
            // Arrange
            Vector3 planetPos = new Vector3(1000, 0, 0);
            float standoff = 150f;

            // Act
            ShipSpawnPlacer.ComputeSpawnPose(planetPos, standoff, out Vector3 position, out Quaternion rotation);

            // Assert - rotation forward should point at the planet
            Vector3 forwardDir = rotation * Vector3.forward;
            Vector3 toPlanet = (planetPos - position).normalized;
            float dotProduct = Vector3.Dot(forwardDir, toPlanet);

            Assert.That(dotProduct, Is.GreaterThan(0.99f), "Rotation forward should point toward planet");
        }

        [Test]
        public void ComputeSpawnPose_PlanetAtOrigin_PositionsAtStandoffBack()
        {
            // Arrange
            Vector3 planetPos = Vector3.zero;
            float standoff = 150f;

            // Act
            ShipSpawnPlacer.ComputeSpawnPose(planetPos, standoff, out Vector3 position, out Quaternion rotation);

            // Assert - should be 150 units away
            float distanceFromOrigin = position.magnitude;
            Assert.AreEqual(standoff, distanceFromOrigin, Tolerance);
        }

        [Test]
        public void ComputeSpawnPose_PlanetAtOrigin_DoesNotThrow()
        {
            // Arrange
            Vector3 planetPos = Vector3.zero;
            float standoff = 150f;

            // Act & Assert
            Assert.DoesNotThrow(() => ShipSpawnPlacer.ComputeSpawnPose(planetPos, standoff, out Vector3 _, out Quaternion _));
        }

        [Test]
        public void ComputeSpawnPose_RotationHasZeroRoll_UpVectorUsed()
        {
            // Arrange
            Vector3 planetPos = new Vector3(500, 200, 300);
            float standoff = 100f;

            // Act
            ShipSpawnPlacer.ComputeSpawnPose(planetPos, standoff, out Vector3 position, out Quaternion rotation);

            // Assert - the rotation should have been constructed with Vector3.up, so up should be close to world up
            // (in practice, for any non-degenerate case, the up will be very close to world up due to Quaternion.LookRotation)
            Vector3 upDir = rotation * Vector3.up;
            Assert.That(Mathf.Abs(upDir.y), Is.GreaterThan(0.5f), "Up should have significant Y component");
        }
    }
}

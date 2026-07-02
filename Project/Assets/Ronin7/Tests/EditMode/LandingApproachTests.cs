using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    public class LandingApproachTests
    {
        private bool savedFirstPlanetDeparted;
        private int savedShipHullIndex;

        [SetUp]
        public void SetUp()
        {
            // Save current state
            savedFirstPlanetDeparted = Galaxy1Progress.FirstPlanetDeparted;
            savedShipHullIndex = ShipSelection.SelectedHullIndex;

            // Reset campaign state
            CampaignState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            // Reset state
            CampaignState.Reset();

            // Restore state
            Galaxy1Progress.FirstPlanetDeparted = savedFirstPlanetDeparted;
            ShipSelection.SelectedHullIndex = savedShipHullIndex;
        }

        // CanArm is the pure decision surface: arm only when in range AND slow AND skies clear.
        // The full 8-row truth table is enumerated as one [Test] per row for readability.

        [Test]
        public void CanArm_NearAndSlowAndClear_ReturnsTrue()
        {
            Assert.IsTrue(LandingApproach.CanArm(true, true, true));
        }

        [Test]
        public void CanArm_NearAndSlowAndHostiles_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(true, true, false));
        }

        [Test]
        public void CanArm_NearAndFastAndClear_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(true, false, true));
        }

        [Test]
        public void CanArm_NearAndFastAndHostiles_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(true, false, false));
        }

        [Test]
        public void CanArm_FarAndSlowAndClear_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(false, true, true));
        }

        [Test]
        public void CanArm_FarAndSlowAndHostiles_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(false, true, false));
        }

        [Test]
        public void CanArm_FarAndFastAndClear_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(false, false, true));
        }

        [Test]
        public void CanArm_FarAndFastAndHostiles_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.CanArm(false, false, false));
        }

        // ClearanceGranted: granted if requiredScene is empty or completed.

        [Test]
        public void ClearanceGranted_EmptyRequirement_ReturnsTrue()
        {
            Assert.IsTrue(LandingApproach.ClearanceGranted(""));
        }

        [Test]
        public void ClearanceGranted_SceneCompleted_ReturnsTrue()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);
            CampaignState.NoteZoneCompleted("Corsair");

            // Act & Assert
            Assert.IsTrue(LandingApproach.ClearanceGranted("PlanetA"));
        }

        [Test]
        public void ClearanceGranted_SceneNotCompleted_ReturnsFalse()
        {
            Assert.IsFalse(LandingApproach.ClearanceGranted("PlanetNotVisited"));
        }
    }
}

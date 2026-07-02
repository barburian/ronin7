using NUnit.Framework;
using Ronin7.World.Story;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the pure decision seams on <see cref="HubStateController"/> — whether the scene should
    /// present as the hub, and whether a given room gate should be open — without instantiating the
    /// MonoBehaviour or touching CampaignState.
    /// </summary>
    public class HubStateControllerTests
    {
        [Test]
        public void IsHubMode_Ch1CompleteSet_ReturnsTrue()
        {
            Assert.IsTrue(HubStateController.IsHubMode(f => f == "ch1_complete"));
        }

        [Test]
        public void IsHubMode_Ch1CompleteNotSet_ReturnsFalse()
        {
            Assert.IsFalse(HubStateController.IsHubMode(f => false));
        }

        [Test]
        public void ShouldEnableGate_FlagSet_ReturnsTrue()
        {
            Assert.IsTrue(HubStateController.ShouldEnableGate("ch2_complete", f => f == "ch2_complete"));
        }

        [Test]
        public void ShouldEnableGate_FlagNotSet_ReturnsFalse()
        {
            Assert.IsFalse(HubStateController.ShouldEnableGate("ch2_complete", f => false));
        }

        [Test]
        public void ShouldEnableGate_NullFlag_ReturnsFalse()
        {
            Assert.IsFalse(HubStateController.ShouldEnableGate(null, f => true));
        }

        [Test]
        public void ShouldEnableGate_EmptyFlag_ReturnsFalse()
        {
            Assert.IsFalse(HubStateController.ShouldEnableGate("", f => true));
        }
    }
}

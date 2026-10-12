using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P0-03: a held blade re-arms only after several consecutive tracked frames, so the pose
    /// jump when controller tracking returns cannot register as a max-speed swing.</summary>
    public class BladeTrackingTests
    {
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(100, true)]
        public void BladeArmed_RequiresThreeTrackedFrames(int trackedFrames, bool expected)
        {
            Assert.AreEqual(expected, Grabber.BladeArmed(trackedFrames));
        }
    }
}

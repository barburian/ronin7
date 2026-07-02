using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="GameFlowManager.ShouldReveal"/>, the pure gate that ends the post-load
    /// camera wait. Once the head camera exists the wait stops immediately; when it never appears
    /// the frame budget bounds the wait so the coroutine can't spin forever (with no camera there is
    /// nothing to fade — the caller logs a diagnostic and proceeds).
    /// </summary>
    public class GameFlowRevealTests
    {
        private const int MaxFrames = 30;

        [Test]
        public void ShouldReveal_CameraPresent_TrueRegardlessOfFramesWaited()
        {
            Assert.IsTrue(GameFlowManager.ShouldReveal(cameraPresent: true, framesWaited: 0, maxFrames: MaxFrames));
            Assert.IsTrue(GameFlowManager.ShouldReveal(cameraPresent: true, framesWaited: MaxFrames + 1, maxFrames: MaxFrames));
        }

        [Test]
        public void ShouldReveal_CameraAbsent_BelowBudget_KeepsWaiting()
        {
            Assert.IsFalse(GameFlowManager.ShouldReveal(cameraPresent: false, framesWaited: MaxFrames - 1, maxFrames: MaxFrames));
        }

        [Test]
        public void ShouldReveal_CameraAbsent_AtBudget_StopsWaiting()
        {
            // Boundary: framesWaited == maxFrames must end the wait (the gate is >=, not >).
            Assert.IsTrue(GameFlowManager.ShouldReveal(cameraPresent: false, framesWaited: MaxFrames, maxFrames: MaxFrames));
        }

        [Test]
        public void ShouldReveal_CameraAbsent_PastBudget_StopsWaiting()
        {
            Assert.IsTrue(GameFlowManager.ShouldReveal(cameraPresent: false, framesWaited: MaxFrames + 5, maxFrames: MaxFrames));
        }
    }
}

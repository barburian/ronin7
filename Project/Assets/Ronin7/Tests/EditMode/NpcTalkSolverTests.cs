using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure amplitude-to-nod math behind <see cref="NpcTalkAnimator"/>. Mirrors
    /// <c>NpcGazeSolverTests</c>' style: a plain-value solver with no MonoBehaviour/AudioSource involved.
    /// </summary>
    public class NpcTalkSolverTests
    {
        [Test]
        public void SilentRms_ReturnsZeroWeight()
        {
            float w = NpcTalkSolver.ComputeTalkWeight(0f, 0.01f, 0.15f);
            Assert.AreEqual(0f, w, 1e-4f);
        }

        [Test]
        public void RmsAtNoiseFloor_ReturnsZeroWeight()
        {
            float w = NpcTalkSolver.ComputeTalkWeight(0.01f, 0.01f, 0.15f);
            Assert.AreEqual(0f, w, 1e-4f);
        }

        [Test]
        public void RmsHalfwayBetweenFloorAndSaturation_ReturnsHalfWeight()
        {
            // noiseFloor 0.01, saturateRms 0.15: midpoint rms 0.08 -> t = (0.08-0.01)/(0.15-0.01) = 0.5.
            float w = NpcTalkSolver.ComputeTalkWeight(0.08f, 0.01f, 0.15f);
            Assert.AreEqual(0.5f, w, 1e-3f);
        }

        [Test]
        public void RmsAtOrAboveSaturation_ClampsToOne()
        {
            float atSaturation = NpcTalkSolver.ComputeTalkWeight(0.15f, 0.01f, 0.15f);
            float loud = NpcTalkSolver.ComputeTalkWeight(1f, 0.01f, 0.15f);
            Assert.AreEqual(1f, atSaturation, 1e-4f);
            Assert.AreEqual(1f, loud, 1e-4f);
        }

        [Test]
        public void SmoothTalkWeight_MovesTowardTargetAtFixedRate()
        {
            // Rising from 0 toward 1 at speed 6/s over 0.1s should advance by exactly 0.6.
            float w = NpcTalkSolver.SmoothTalkWeight(0f, 1f, 0.1f, 6f);
            Assert.AreEqual(0.6f, w, 1e-4f);
        }

        [Test]
        public void SmoothTalkWeight_NeverOvershootsTarget()
        {
            // A large deltaTime/speed must clamp at the target instead of overshooting past it.
            float w = NpcTalkSolver.SmoothTalkWeight(0f, 1f, 10f, 6f);
            Assert.AreEqual(1f, w, 1e-4f);
        }

        [Test]
        public void SmoothTalkWeight_EasesBackToZeroWhenSilent()
        {
            float w = NpcTalkSolver.SmoothTalkWeight(1f, 0f, 0.1f, 6f);
            Assert.AreEqual(0.4f, w, 1e-4f);
        }
    }
}

using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The pure sine-breathing core of AmbientLightPulse: phase sampling at the quarter-period marks,
    /// the output range bound, and the periodSeconds&lt;=0 guard. All side-effect free — no
    /// scene/MonoBehaviour instance needed.
    /// </summary>
    public class AmbientLightPulseTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Intensity_AtZero_IsMidpoint()
        {
            Assert.AreEqual(0.5f, AmbientLightPulse.Intensity(0f, 6f, 0f, 1f), Eps);
        }

        [Test]
        public void Intensity_AtQuarterPeriod_IsMax()
        {
            float period = 6f;
            Assert.AreEqual(1f, AmbientLightPulse.Intensity(period / 4f, period, 0f, 1f), Eps);
        }

        [Test]
        public void Intensity_AtThreeQuarterPeriod_IsMin()
        {
            float period = 6f;
            Assert.AreEqual(0f, AmbientLightPulse.Intensity(3f * period / 4f, period, 0f, 1f), Eps);
        }

        [Test]
        public void Intensity_StaysWithinMinMaxRange()
        {
            float period = 6f;
            for (float t = 0f; t <= 12f; t += 0.25f)
            {
                float v = AmbientLightPulse.Intensity(t, period, 2f, 5f);
                Assert.GreaterOrEqual(v, 2f);
                Assert.LessOrEqual(v, 5f);
            }
        }

        [Test]
        public void Intensity_NonPositivePeriod_ReturnsMinWithoutThrowing()
        {
            Assert.AreEqual(1f, AmbientLightPulse.Intensity(3f, 0f, 1f, 4f), Eps);
            Assert.AreEqual(1f, AmbientLightPulse.Intensity(3f, -2f, 1f, 4f), Eps);
        }
    }
}

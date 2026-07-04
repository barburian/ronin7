using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The pure distance-to-intensity core of ProximityGlowLight: full brightness inside the inner
    /// radius, easing to the minimum at/beyond the outer radius. All side-effect free — no
    /// scene/MonoBehaviour instance needed.
    /// </summary>
    public class ProximityGlowLightTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void InsideInnerRadius_ReturnsMax()
        {
            Assert.AreEqual(2f, ProximityGlowLight.GlowIntensity(0f, 2f, 12f, 0f, 2f), Eps);
            Assert.AreEqual(2f, ProximityGlowLight.GlowIntensity(2f, 2f, 12f, 0f, 2f), Eps);
        }

        [Test]
        public void AtOrBeyondOuterRadius_ReturnsMin()
        {
            Assert.AreEqual(0f, ProximityGlowLight.GlowIntensity(12f, 2f, 12f, 0f, 2f), Eps);
            Assert.AreEqual(0f, ProximityGlowLight.GlowIntensity(50f, 2f, 12f, 0f, 2f), Eps);
        }

        [Test]
        public void BetweenRadii_InterpolatesLinearly()
        {
            // Midpoint between inner=2 and outer=12 is 7.
            Assert.AreEqual(1f, ProximityGlowLight.GlowIntensity(7f, 2f, 12f, 0f, 2f), Eps);
        }

        [Test]
        public void StaysWithinMinMaxRange()
        {
            for (float d = -5f; d <= 20f; d += 0.25f)
            {
                float v = ProximityGlowLight.GlowIntensity(d, 2f, 12f, 0f, 2f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 2f);
            }
        }

        [Test]
        public void InnerGreaterThanOuter_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ProximityGlowLight.GlowIntensity(5f, 12f, 2f, 0f, 2f));
        }

        [Test]
        public void NegativeDistance_ClampsToMax()
        {
            Assert.AreEqual(2f, ProximityGlowLight.GlowIntensity(-10f, 2f, 12f, 0f, 2f), Eps);
        }
    }
}

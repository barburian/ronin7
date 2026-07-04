using NUnit.Framework;
using Ronin7.Audio;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The pure distance-to-volume core of ProximityAmbienceLayer: full volume inside the inner
    /// radius, easing to silence at/beyond the outer radius. All side-effect free — no
    /// scene/MonoBehaviour instance needed.
    /// </summary>
    public class ProximityAmbienceLayerTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void ComputeLayerVolume_AtZeroDistance_IsMaxVolume()
        {
            Assert.AreEqual(1f, ProximityAmbienceLayer.ComputeLayerVolume(0f, 2f, 12f, 1f), Eps);
        }

        [Test]
        public void ComputeLayerVolume_AtOrInsideInnerRadius_IsMaxVolume()
        {
            Assert.AreEqual(1f, ProximityAmbienceLayer.ComputeLayerVolume(2f, 2f, 12f, 1f), Eps);
            Assert.AreEqual(1f, ProximityAmbienceLayer.ComputeLayerVolume(1f, 2f, 12f, 1f), Eps);
        }

        [Test]
        public void ComputeLayerVolume_AtOuterRadius_IsZero()
        {
            Assert.AreEqual(0f, ProximityAmbienceLayer.ComputeLayerVolume(12f, 2f, 12f, 1f), Eps);
        }

        [Test]
        public void ComputeLayerVolume_BeyondOuterRadius_IsZero()
        {
            Assert.AreEqual(0f, ProximityAmbienceLayer.ComputeLayerVolume(50f, 2f, 12f, 1f), Eps);
        }

        [Test]
        public void ComputeLayerVolume_AtMidpoint_IsHalfMaxVolume()
        {
            // Midpoint between inner=2 and outer=12 is 7.
            Assert.AreEqual(0.5f, ProximityAmbienceLayer.ComputeLayerVolume(7f, 2f, 12f, 1f), Eps);
        }
    }
}

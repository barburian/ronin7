using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    public class QualityBootstrapRefreshRateTests
    {
        [Test] public void PicksTargetWhenSupported() =>
            Assert.AreEqual(90f, QualityBootstrap.PickRefreshRate(new[] { 72f, 90f, 120f }, 90f));
        [Test] public void Quest1CapsAt72() =>
            Assert.AreEqual(72f, QualityBootstrap.PickRefreshRate(new[] { 60f, 72f }, 90f));
        [Test] public void EmptyReturnsTarget() =>
            Assert.AreEqual(90f, QualityBootstrap.PickRefreshRate(new float[0], 90f));
        [Test] public void AllAboveTargetReturnsLowest() =>
            Assert.AreEqual(120f, QualityBootstrap.PickRefreshRate(new[] { 120f, 144f }, 90f));

        [TestCase(GraphicsDirector.DeviceTier.Quest3, 0.75f)]
        [TestCase(GraphicsDirector.DeviceTier.Quest2, 0.5f)]
        public void FoveationFor_Tier(GraphicsDirector.DeviceTier tier, float expected) =>
            Assert.AreEqual(expected, QualityBootstrap.FoveationFor(tier), 1e-6f);

        [TestCase(GraphicsDirector.DeviceTier.PCVR, 1f)]
        [TestCase(GraphicsDirector.DeviceTier.Quest3, 1f)]
        [TestCase(GraphicsDirector.DeviceTier.Quest2, 0.85f)]
        public void EyeResolutionScaleFor_Tier(GraphicsDirector.DeviceTier tier, float expected) =>
            Assert.AreEqual(expected, QualityBootstrap.EyeResolutionScaleFor(tier), 1e-6f);
    }
}

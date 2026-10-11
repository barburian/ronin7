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
    }
}

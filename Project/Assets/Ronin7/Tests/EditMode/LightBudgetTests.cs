using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// LightBudget.ShouldAnimate's four-quadrant truth table. Tier is always passed explicitly —
    /// never read/mutate GraphicsRuntime.Quality — to avoid static-state leakage across tests.
    /// </summary>
    public class LightBudgetTests
    {
        [Test]
        public void ShouldAnimate_HighTier_Decorative_ReturnsTrue()
        {
            Assert.IsTrue(LightBudget.ShouldAnimate(GraphicsQuality.High, isGameplaySignal: false));
        }

        [Test]
        public void ShouldAnimate_LowTier_Decorative_ReturnsFalse()
        {
            Assert.IsFalse(LightBudget.ShouldAnimate(GraphicsQuality.Low, isGameplaySignal: false));
        }

        [Test]
        public void ShouldAnimate_LowTier_GameplaySignal_ReturnsTrue()
        {
            Assert.IsTrue(LightBudget.ShouldAnimate(GraphicsQuality.Low, isGameplaySignal: true));
        }

        [Test]
        public void ShouldAnimate_HighTier_GameplaySignal_ReturnsTrue()
        {
            Assert.IsTrue(LightBudget.ShouldAnimate(GraphicsQuality.High, isGameplaySignal: true));
        }
    }
}

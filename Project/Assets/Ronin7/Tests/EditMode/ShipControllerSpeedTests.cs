using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="ShipController.EffectiveSpeed"/> — the pure forward-speed composition
    /// (base * trim * (1 + boost)) that folds the sun-nav boost hook and the future H4
    /// SpeedTrimMultiplier trick system into <see cref="ShipController.Update"/>'s single integration
    /// line without ever touching it again.
    /// </summary>
    public class ShipControllerSpeedTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void EffectiveSpeed_IdentityTrimAndNoBoost_ReturnsBaseSpeed()
        {
            Assert.AreEqual(10f, ShipController.EffectiveSpeed(10f, 1f, 0f), Eps);
        }

        [Test]
        public void EffectiveSpeed_TrimOnly_ScalesLinearly()
        {
            Assert.AreEqual(15f, ShipController.EffectiveSpeed(10f, 1.5f, 0f), Eps);
        }

        [Test]
        public void EffectiveSpeed_BoostOnly_AddsFraction()
        {
            Assert.AreEqual(12f, ShipController.EffectiveSpeed(10f, 1f, 0.2f), Eps);
        }

        [Test]
        public void EffectiveSpeed_TrimAndBoostCombined_Multiplies()
        {
            // 10 * 1.5 * (1 + 0.2) = 18
            Assert.AreEqual(18f, ShipController.EffectiveSpeed(10f, 1.5f, 0.2f), Eps);
        }
    }
}

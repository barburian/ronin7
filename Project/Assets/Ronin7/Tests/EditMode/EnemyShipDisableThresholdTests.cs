using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="EnemyShip.ShouldDisable"/>, the pure low-health disable-threshold check
    /// extracted out of <c>EnemyShip.Update</c> so it can be unit-tested without a live Health
    /// component or MonoBehaviour lifecycle.
    /// </summary>
    public class EnemyShipDisableThresholdTests
    {
        [Test]
        public void ShouldDisable_FlagOff_AlwaysFalse()
        {
            // Health is well below the fraction, but disableInsteadOfDestroy is off.
            Assert.IsFalse(EnemyShip.ShouldDisable(false, 1f, 100f, 0.18f));
        }

        [Test]
        public void ShouldDisable_AboveFraction_ReturnsFalse()
        {
            Assert.IsFalse(EnemyShip.ShouldDisable(true, 50f, 100f, 0.18f));
        }

        [Test]
        public void ShouldDisable_AtOrBelowFraction_ReturnsTrue()
        {
            // Exact boundary: currentHealth / maxHealth == disableFraction must count as "at or below".
            Assert.IsTrue(EnemyShip.ShouldDisable(true, 18f, 100f, 0.18f));

            // Below the boundary.
            Assert.IsTrue(EnemyShip.ShouldDisable(true, 10f, 100f, 0.18f));
        }

        [Test]
        public void ShouldDisable_MaxHealthZero_ReturnsFalse()
        {
            Assert.IsFalse(EnemyShip.ShouldDisable(true, 0f, 0f, 0.18f));
        }
    }
}

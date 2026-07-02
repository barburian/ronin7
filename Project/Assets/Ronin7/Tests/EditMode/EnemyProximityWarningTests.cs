using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    public class EnemyProximityWarningTests
    {
        [Test]
        public void ShouldWarn_ArmedAndEnemyInsideWarnDistance_ReturnsTrue()
        {
            Assert.IsTrue(EnemyProximityWarning.ShouldWarn(armed: true, nearestEnemyDistance: 100f, warnDistance: 160f));
        }

        [Test]
        public void ShouldWarn_Disarmed_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldWarn(armed: false, nearestEnemyDistance: 100f, warnDistance: 160f));
        }

        [Test]
        public void ShouldWarn_EnemyOutsideWarnDistance_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldWarn(armed: true, nearestEnemyDistance: 200f, warnDistance: 160f));
        }

        [Test]
        public void ShouldWarn_NoEnemies_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldWarn(armed: true, nearestEnemyDistance: float.PositiveInfinity, warnDistance: 160f));
        }

        [Test]
        public void ShouldRearm_EnemyStillInRearmRange_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldRearm(armed: false, nearestEnemyDistance: 180f, rearmDistance: 200f, timeSinceWarn: 100f, cooldownSeconds: 25f));
        }

        [Test]
        public void ShouldRearm_RangeClearButCooldownNotElapsed_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldRearm(armed: false, nearestEnemyDistance: float.PositiveInfinity, rearmDistance: 200f, timeSinceWarn: 10f, cooldownSeconds: 25f));
        }

        [Test]
        public void ShouldRearm_RangeClearAndCooldownElapsed_ReturnsTrue()
        {
            Assert.IsTrue(EnemyProximityWarning.ShouldRearm(armed: false, nearestEnemyDistance: float.PositiveInfinity, rearmDistance: 200f, timeSinceWarn: 30f, cooldownSeconds: 25f));
        }

        [Test]
        public void ShouldRearm_AlreadyArmed_ReturnsFalse()
        {
            Assert.IsFalse(EnemyProximityWarning.ShouldRearm(armed: true, nearestEnemyDistance: float.PositiveInfinity, rearmDistance: 200f, timeSinceWarn: 100f, cooldownSeconds: 25f));
        }
    }
}

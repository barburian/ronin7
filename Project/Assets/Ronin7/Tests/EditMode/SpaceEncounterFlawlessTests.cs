using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Pure-logic coverage for <see cref="SpaceEncounterManager.ShouldPublishFlawless"/>: the decision
    /// of whether a just-cleared encounter qualifies for the flawless-encounter campaign stat (zero
    /// player-ship damage across a FINITE encounter's waves).
    /// </summary>
    public class SpaceEncounterFlawlessTests
    {
        [Test]
        public void ShouldPublishFlawless_FiniteAndNoDamage_ReturnsTrue()
        {
            Assert.IsTrue(SpaceEncounterManager.ShouldPublishFlawless(finiteEncounter: true, tookNoDamage: true));
        }

        [Test]
        public void ShouldPublishFlawless_FiniteButDamageTaken_ReturnsFalse()
        {
            Assert.IsFalse(SpaceEncounterManager.ShouldPublishFlawless(finiteEncounter: true, tookNoDamage: false));
        }

        [Test]
        public void ShouldPublishFlawless_Endless_NeverPublishes_EvenWithoutDamage()
        {
            Assert.IsFalse(SpaceEncounterManager.ShouldPublishFlawless(finiteEncounter: false, tookNoDamage: true));
        }
    }
}

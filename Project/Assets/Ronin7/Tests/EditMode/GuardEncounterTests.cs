using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    public class GuardEncounterTests
    {
        // Truth table for ShouldActivate: all 8 combinations of (requiredDone, suppressedDone, alreadyCleared).
        // Activation requires: requiredDone=true, suppressedDone=false, alreadyCleared=false.

        [Test]
        public void ShouldActivate_AllConditionsMet_ReturnsTrue()
        {
            Assert.IsTrue(GuardEncounter.ShouldActivate(true, false, false));
        }

        [Test]
        public void ShouldActivate_RequiredNotDone_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(false, false, false));
        }

        [Test]
        public void ShouldActivate_Suppressed_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(true, true, false));
        }

        [Test]
        public void ShouldActivate_AlreadyCleared_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(true, false, true));
        }

        [Test]
        public void ShouldActivate_RequiredNotDoneAndSuppressed_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(false, true, false));
        }

        [Test]
        public void ShouldActivate_RequiredNotDoneAndAlreadyCleared_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(false, false, true));
        }

        [Test]
        public void ShouldActivate_SuppressedAndAlreadyCleared_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(true, true, true));
        }

        [Test]
        public void ShouldActivate_AllConditionsFailed_ReturnsFalse()
        {
            Assert.IsFalse(GuardEncounter.ShouldActivate(false, true, true));
        }
    }
}

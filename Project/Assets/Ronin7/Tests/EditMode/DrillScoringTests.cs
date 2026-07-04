using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class DrillScoringTests
    {
        [Test]
        public void Score_AllZero_IsZero()
        {
            Assert.AreEqual(0, DrillScoring.Score(0, 0, 0));
        }

        [Test]
        public void Score_WeightsImpactsPerfectParriesAndPostureBreaks()
        {
            Assert.AreEqual(10, DrillScoring.Score(1, 0, 0));
            Assert.AreEqual(25, DrillScoring.Score(0, 1, 0));
            Assert.AreEqual(100, DrillScoring.Score(0, 0, 1));
        }

        [Test]
        public void Score_CombinesAllThreeCounts()
        {
            // 3 impacts + 2 perfect parries + 1 posture break -> 30 + 50 + 100
            Assert.AreEqual(180, DrillScoring.Score(3, 2, 1));
        }
    }
}

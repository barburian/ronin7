using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class DrillScoringTests
    {
        [Test]
        public void Score_AllZero_IsZero()
        {
            Assert.AreEqual(0, DrillScoring.Score(0, 0, 0, flawless: false));
        }

        [Test]
        public void Score_WeightsImpactsPerfectParriesAndPostureBreaks()
        {
            Assert.AreEqual(10, DrillScoring.Score(1, 0, 0, flawless: false));
            Assert.AreEqual(25, DrillScoring.Score(0, 1, 0, flawless: false));
            Assert.AreEqual(100, DrillScoring.Score(0, 0, 1, flawless: false));
        }

        [Test]
        public void Score_CombinesAllThreeCounts()
        {
            // 3 impacts + 2 perfect parries + 1 posture break -> 30 + 50 + 100
            Assert.AreEqual(180, DrillScoring.Score(3, 2, 1, flawless: false));
        }

        [Test]
        public void Score_Flawless_OutscoresIdenticalNonFlawlessRun()
        {
            int flawlessScore = DrillScoring.Score(3, 2, 1, flawless: true);
            int normalScore = DrillScoring.Score(3, 2, 1, flawless: false);
            Assert.Greater(flawlessScore, normalScore);
        }

        [Test]
        public void Score_FlawlessWithZeroActions_StillScoresAboveZero()
        {
            int score = DrillScoring.Score(0, 0, 0, flawless: true);
            Assert.Greater(score, 0);
            Assert.AreEqual(200, score);
        }
    }
}

using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class CampaignStatsTests
    {
        [SetUp]
        public void SetUp() => CampaignStats.Reset();

        [TearDown]
        public void TearDown() => CampaignStats.Reset();

        [Test]
        public void RecordEnemyDefeated_IncrementsCount()
        {
            CampaignStats.RecordEnemyDefeated();
            CampaignStats.RecordEnemyDefeated();

            Assert.AreEqual(2, CampaignStats.EnemiesDefeated);
        }

        [Test]
        public void RecordPerfectParry_IncrementsCount()
        {
            CampaignStats.RecordPerfectParry();

            Assert.AreEqual(1, CampaignStats.PerfectParries);
        }

        [Test]
        public void RecordPostureBreak_IncrementsCount()
        {
            CampaignStats.RecordPostureBreak();

            Assert.AreEqual(1, CampaignStats.PostureBreaks);
        }

        [Test]
        public void RecordCombo_TracksHighestReached()
        {
            CampaignStats.RecordCombo(2);
            CampaignStats.RecordCombo(4);

            Assert.AreEqual(4, CampaignStats.BestCombo);
        }

        [Test]
        public void RecordCombo_LowerValueDoesNotLowerBest()
        {
            CampaignStats.RecordCombo(4);
            CampaignStats.RecordCombo(1);

            Assert.AreEqual(4, CampaignStats.BestCombo);
        }

        [Test]
        public void NextBestCombo_ReturnsMax()
        {
            Assert.AreEqual(5, CampaignStats.NextBestCombo(5, 3));
            Assert.AreEqual(7, CampaignStats.NextBestCombo(5, 7));
            Assert.AreEqual(5, CampaignStats.NextBestCombo(5, 5));
        }

        [Test]
        public void Reset_ZeroesAllCounters()
        {
            CampaignStats.RecordEnemyDefeated();
            CampaignStats.RecordPerfectParry();
            CampaignStats.RecordPostureBreak();
            CampaignStats.RecordCombo(3);

            CampaignStats.Reset();

            Assert.AreEqual(0, CampaignStats.EnemiesDefeated);
            Assert.AreEqual(0, CampaignStats.PerfectParries);
            Assert.AreEqual(0, CampaignStats.PostureBreaks);
            Assert.AreEqual(0, CampaignStats.BestCombo);
        }
    }
}

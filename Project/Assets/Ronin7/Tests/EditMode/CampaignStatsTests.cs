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
        public void RecordKillStreak_TracksHighestReached()
        {
            CampaignStats.RecordKillStreak(2);
            CampaignStats.RecordKillStreak(4);

            Assert.AreEqual(4, CampaignStats.BestKillStreak);
        }

        [Test]
        public void RecordKillStreak_LowerValueDoesNotLowerBest()
        {
            CampaignStats.RecordKillStreak(4);
            CampaignStats.RecordKillStreak(1);

            Assert.AreEqual(4, CampaignStats.BestKillStreak);
        }

        [Test]
        public void NextBestKillStreak_ReturnsMax()
        {
            Assert.AreEqual(5, CampaignStats.NextBestKillStreak(5, 3));
            Assert.AreEqual(7, CampaignStats.NextBestKillStreak(5, 7));
            Assert.AreEqual(5, CampaignStats.NextBestKillStreak(5, 5));
        }

        [Test]
        public void RecordNearMissStreak_TracksHighestReached()
        {
            CampaignStats.RecordNearMissStreak(2);
            CampaignStats.RecordNearMissStreak(4);

            Assert.AreEqual(4, CampaignStats.BestNearMissStreak);
        }

        [Test]
        public void RecordNearMissStreak_LowerValueDoesNotLowerBest()
        {
            CampaignStats.RecordNearMissStreak(4);
            CampaignStats.RecordNearMissStreak(1);

            Assert.AreEqual(4, CampaignStats.BestNearMissStreak);
        }

        [Test]
        public void NextBestNearMissStreak_ReturnsMax()
        {
            Assert.AreEqual(5, CampaignStats.NextBestNearMissStreak(5, 3));
            Assert.AreEqual(7, CampaignStats.NextBestNearMissStreak(5, 7));
            Assert.AreEqual(5, CampaignStats.NextBestNearMissStreak(5, 5));
        }

        [Test]
        public void RecordParryStreak_TracksHighestReached()
        {
            CampaignStats.RecordParryStreak(2);
            CampaignStats.RecordParryStreak(4);

            Assert.AreEqual(4, CampaignStats.BestParryStreak);
        }

        [Test]
        public void RecordParryStreak_LowerValueDoesNotLowerBest()
        {
            CampaignStats.RecordParryStreak(4);
            CampaignStats.RecordParryStreak(1);

            Assert.AreEqual(4, CampaignStats.BestParryStreak);
        }

        [Test]
        public void NextBestParryStreak_ReturnsMax()
        {
            Assert.AreEqual(5, CampaignStats.NextBestParryStreak(5, 3));
            Assert.AreEqual(7, CampaignStats.NextBestParryStreak(5, 7));
            Assert.AreEqual(5, CampaignStats.NextBestParryStreak(5, 5));
        }

        [Test]
        public void RecordFlawlessEncounter_IncrementsCount()
        {
            CampaignStats.RecordFlawlessEncounter();
            CampaignStats.RecordFlawlessEncounter();

            Assert.AreEqual(2, CampaignStats.FlawlessEncounters);
        }

        [Test]
        public void Reset_ZeroesAllCounters()
        {
            CampaignStats.RecordEnemyDefeated();
            CampaignStats.RecordPerfectParry();
            CampaignStats.RecordPostureBreak();
            CampaignStats.RecordCombo(3);
            CampaignStats.RecordKillStreak(3);
            CampaignStats.RecordNearMissStreak(3);
            CampaignStats.RecordParryStreak(3);
            CampaignStats.RecordFlawlessEncounter();

            CampaignStats.Reset();

            Assert.AreEqual(0, CampaignStats.EnemiesDefeated);
            Assert.AreEqual(0, CampaignStats.PerfectParries);
            Assert.AreEqual(0, CampaignStats.PostureBreaks);
            Assert.AreEqual(0, CampaignStats.BestCombo);
            Assert.AreEqual(0, CampaignStats.BestKillStreak);
            Assert.AreEqual(0, CampaignStats.BestNearMissStreak);
            Assert.AreEqual(0, CampaignStats.BestParryStreak);
            Assert.AreEqual(0, CampaignStats.FlawlessEncounters);
        }
    }
}

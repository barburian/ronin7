using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class DrillBestScoresTests
    {
        [SetUp]
        public void SetUp() => DrillBestScores.Reset();

        [TearDown]
        public void TearDown() => DrillBestScores.Reset();

        [Test]
        public void BestFor_UnknownId_ReturnsZero()
        {
            Assert.AreEqual(0, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void BestFor_NullOrEmptyId_ReturnsZero()
        {
            Assert.AreEqual(0, DrillBestScores.BestFor(null));
            Assert.AreEqual(0, DrillBestScores.BestFor(""));
        }

        [Test]
        public void RecordIfBetter_FirstScore_BecomesBest()
        {
            int best = DrillBestScores.RecordIfBetter("hub_dojo", 100);

            Assert.AreEqual(100, best);
            Assert.AreEqual(100, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void RecordIfBetter_HigherScore_Replaces()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 100);

            int best = DrillBestScores.RecordIfBetter("hub_dojo", 150);

            Assert.AreEqual(150, best);
            Assert.AreEqual(150, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void RecordIfBetter_LowerOrEqualScore_NeverLowers()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 150);

            int afterLower = DrillBestScores.RecordIfBetter("hub_dojo", 50);
            int afterEqual = DrillBestScores.RecordIfBetter("hub_dojo", 150);

            Assert.AreEqual(150, afterLower);
            Assert.AreEqual(150, afterEqual);
            Assert.AreEqual(150, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void RecordIfBetter_NullOrEmptyId_ReturnsZero_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => DrillBestScores.RecordIfBetter(null, 100));
            Assert.DoesNotThrow(() => DrillBestScores.RecordIfBetter("", 100));
            Assert.AreEqual(0, DrillBestScores.RecordIfBetter(null, 100));
        }

        [Test]
        public void RecordIfBetter_TracksDistinctIdsIndependently()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 100);
            DrillBestScores.RecordIfBetter("other_drill", 40);

            Assert.AreEqual(100, DrillBestScores.BestFor("hub_dojo"));
            Assert.AreEqual(40, DrillBestScores.BestFor("other_drill"));
        }

        [Test]
        public void ToSaveList_EmptyLedger_ReturnsEmptyList()
        {
            List<DrillScoreEntry> list = DrillBestScores.ToSaveList();

            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void ToSaveList_SortsById()
        {
            DrillBestScores.RecordIfBetter("zeta_drill", 10);
            DrillBestScores.RecordIfBetter("alpha_drill", 20);

            List<DrillScoreEntry> list = DrillBestScores.ToSaveList();

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("alpha_drill", list[0].id);
            Assert.AreEqual(20, list[0].best);
            Assert.AreEqual("zeta_drill", list[1].id);
            Assert.AreEqual(10, list[1].best);
        }

        [Test]
        public void ToSaveList_AndApplyFrom_RoundTrips()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 180);
            DrillBestScores.RecordIfBetter("outpost_dojo", 90);

            List<DrillScoreEntry> saved = DrillBestScores.ToSaveList();
            DrillBestScores.Reset();
            DrillBestScores.ApplyFrom(saved);

            Assert.AreEqual(180, DrillBestScores.BestFor("hub_dojo"));
            Assert.AreEqual(90, DrillBestScores.BestFor("outpost_dojo"));
        }

        [Test]
        public void ApplyFrom_EmptyList_ResultsInEmptyLedger()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 180);

            DrillBestScores.ApplyFrom(new List<DrillScoreEntry>());

            Assert.AreEqual(0, DrillBestScores.BestFor("hub_dojo"));
            Assert.AreEqual(0, DrillBestScores.ToSaveList().Count);
        }

        [Test]
        public void ApplyFrom_Null_ClearsLedger_DoesNotThrow()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 180);

            Assert.DoesNotThrow(() => DrillBestScores.ApplyFrom(null));

            Assert.AreEqual(0, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void ApplyFrom_EntryWithEmptyId_IsIgnored()
        {
            var entries = new List<DrillScoreEntry>
            {
                new DrillScoreEntry { id = "", best = 999 },
                new DrillScoreEntry { id = "hub_dojo", best = 50 },
            };

            DrillBestScores.ApplyFrom(entries);

            Assert.AreEqual(50, DrillBestScores.BestFor("hub_dojo"));
            Assert.AreEqual(1, DrillBestScores.ToSaveList().Count);
        }

        [Test]
        public void Reset_ClearsAllEntries()
        {
            DrillBestScores.RecordIfBetter("hub_dojo", 180);

            DrillBestScores.Reset();

            Assert.AreEqual(0, DrillBestScores.BestFor("hub_dojo"));
            Assert.AreEqual(0, DrillBestScores.ToSaveList().Count);
        }
    }
}

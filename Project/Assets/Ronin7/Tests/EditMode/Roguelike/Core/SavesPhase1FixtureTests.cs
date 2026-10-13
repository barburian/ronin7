using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>G7 for Phase 1: Phase 0 saves (no unbrokenUsed field) load through the current code.</summary>
    public class SavesPhase1FixtureTests
    {
        private string testDirectory;

        private static string Fixture(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase1", file));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7p1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveSystem.DirectoryOverride = testDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }

        [Test]
        public void MetaJson_Loads()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));

            MetaProgression.Load();

            Assert.AreEqual(310, MetaProgression.Echoes);
            Assert.AreEqual(11, MetaProgression.BestDepth);
            Assert.AreEqual(9, MetaProgression.RunsCompleted);
            Assert.AreEqual(2, MetaProgression.RunsWon);
            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingDamage));
            Assert.AreEqual(4, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
            Assert.AreEqual(1, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingBoon));
        }

        [Test]
        public void RunJson_ResumesWithUnbrokenUnused_AndRoundTrips()
        {
            Assert.IsFalse(File.ReadAllText(Fixture("run.json")).Contains("unbrokenUsed"), "fixture must predate P1-07");
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot);
            Assert.IsFalse(snapshot.unbrokenUsed);
            Assert.IsTrue(RunState.Restore(snapshot));

            Assert.AreEqual(777123u, RunState.Seed);
            Assert.AreEqual(9, RunState.NodeIndex);
            Assert.AreEqual(88, RunState.EchoesEarned);
            Assert.AreEqual(1, RunState.RerollTokens);
            Assert.AreEqual(0, RunState.RevivesSpent);
            Assert.AreEqual(74.25f, RunState.PlayerHealth, 1e-4f);
            Assert.AreEqual(2, RunState.BoonCount(BoonId.KeenEdge));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.SecondWind));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.Ironskin));
            Assert.IsTrue(RunState.HasGrantedAbility(AbilityId.Overdrive));
            Assert.IsFalse(RunState.UnbrokenUsed);
            Assert.AreEqual(41, snapshot.runKills);

            RunSnapshot again = RunState.ToSnapshot();
            Assert.AreEqual(RunSnapshot.CurrentVersion, again.version);
            Assert.AreEqual(9, again.nodeIndex);
            Assert.IsFalse(again.unbrokenUsed);
        }
    }
}

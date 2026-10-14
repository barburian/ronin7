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

        /// <summary>P2-04: v1 run.json can no longer resume (branching map, snapshot v2). The P0-09
        /// migration banks its 88 Echoes on top of the phase-1 meta as a loss at depth 9.</summary>
        [Test]
        public void RunJson_V1_IsDiscardedAndBankedIntoMeta()
        {
            Assert.IsFalse(File.ReadAllText(Fixture("run.json")).Contains("unbrokenUsed"), "fixture must predate P1-07");
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("discarding version 1"));
            Assert.IsNull(RunSnapshot.Read());

            Assert.AreEqual(310 + 88, MetaProgression.Echoes);
            Assert.AreEqual(11, MetaProgression.BestDepth, "depth 9 does not beat the saved best of 11");
            Assert.AreEqual(10, MetaProgression.RunsCompleted);
            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingDamage), "banking must keep the upgrades");
            Assert.IsFalse(File.Exists(Path.Combine(testDirectory, "run.json")));
        }
    }
}

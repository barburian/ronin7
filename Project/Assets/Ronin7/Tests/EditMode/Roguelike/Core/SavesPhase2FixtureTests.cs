using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>G7 for Phase 2 (P2-04, snapshot v1 -> v2). Fixtures in Project/TestData/saves_phase2/.</summary>
    public class SavesPhase2FixtureTests
    {
        private string testDirectory;

        private static string Fixture(string sub, string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase2", sub, file));

        private void Install(string sub, string file) =>
            File.Copy(Fixture(sub, file), Path.Combine(testDirectory, file));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7p2_" + Guid.NewGuid().ToString("N"));
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
        public void V1RunJson_IsDiscarded_AndItsEchoesBankedIntoMeta()
        {
            StringAssert.Contains("\"version\":1", File.ReadAllText(Fixture("v1", "run.json")));
            Install("v1", "meta.json");
            Install("v1", "run.json");

            LogAssert.Expect(LogType.Warning, new Regex("discarding version 1 .*banking its 64 Echoes"));
            Assert.IsNull(RunSnapshot.Read(), "a v1 run cannot resume on the branching map");

            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath), "discarded, so it never prompts RESUME again");
            Assert.AreEqual(205 + 64, MetaProgression.Echoes);
            Assert.AreEqual(9, MetaProgression.BestDepth, "depth 7 does not beat 9");
            Assert.AreEqual(7, MetaProgression.RunsCompleted);
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.AreEqual(2, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingDamage));

            // ...and persisted, not just in memory.
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(205 + 64, MetaProgression.Echoes);
        }

        [Test]
        public void V1Meta_Loads()
        {
            Install("v1", "meta.json");
            MetaProgression.Load();
            Assert.AreEqual(205, MetaProgression.Echoes);
            Assert.AreEqual(1, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void V2Pair_ResumesAndRoundTripsExactly()
        {
            Install("v2", "meta.json");
            Install("v2", "run.json");

            MetaProgression.Load();
            Assert.AreEqual(455, MetaProgression.Echoes);
            Assert.AreEqual(1, MetaProgression.UpgradeLevel(MetaUpgradeId.RerollTokens));

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot);
            Assert.IsTrue(RunState.Restore(snapshot));
            Assert.AreEqual(2026u, RunState.Seed);
            Assert.AreEqual(11, RunState.NodeIndex);
            Assert.AreEqual(6, RunState.Depth);
            CollectionAssert.AreEqual(new[] { 0, 2, 5, 7, 8, 9, 11 }, RunState.Path);
            Assert.AreEqual(73, RunState.EchoesEarned);
            Assert.AreEqual(1, RunState.RerollTokens);
            Assert.AreEqual(58.5f, RunState.PlayerHealth, 1e-4f);
            Assert.AreEqual(2, RunState.BoonCount(BoonId.KeenEdge));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.Ironskin));
            Assert.IsTrue(RunState.HasGrantedAbility(AbilityId.Overdrive));
            Assert.IsTrue(RunState.UnbrokenUsed);
            Assert.AreEqual(-15f, RunState.MaxHealthBonus);

            RunSnapshot again = RunState.ToSnapshot();
            again.runKills = snapshot.runKills;
            Assert.AreEqual(File.ReadAllText(Fixture("v2", "run.json")), JsonUtility.ToJson(again), "v2 must round-trip byte-for-byte");
        }

        [Test]
        public void Phase1Fixture_MetaStillLoads()
        {
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase1", "meta.json")),
                Path.Combine(testDirectory, "meta.json"));
            MetaProgression.Load();
            Assert.AreEqual(310, MetaProgression.Echoes);
            Assert.AreEqual(4, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }
    }
}

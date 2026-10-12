using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>
    /// Export Gate G7: saves written by an earlier phase's build must still load. Each phase leaves a
    /// fixture in Project/TestData/saves_phaseN/ (see its README); these tests load them through the
    /// real load paths. Never change a fixture to make a test pass — migrate the format instead.
    /// </summary>
    public class SaveCompatibilityTests
    {
        private string testDirectory;

        private static string FixtureDir(string phase) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", phase));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7_" + Guid.NewGuid().ToString("N"));
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

        private void Install(string phase, string file) =>
            File.Copy(Path.Combine(FixtureDir(phase), file), Path.Combine(testDirectory, file));

        [Test]
        public void Phase0_MetaJson_Loads()
        {
            Install("saves_phase0", "meta.json");

            MetaProgression.Load();

            Assert.AreEqual(123, MetaProgression.Echoes);
            Assert.AreEqual(7, MetaProgression.BestDepth);
            Assert.AreEqual(4, MetaProgression.RunsCompleted);
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.AreEqual(2, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
            Assert.AreEqual(1, MetaProgression.UpgradeLevel(MetaUpgradeId.RerollTokens));
        }

        [Test]
        public void Phase0_RunJson_ResumesTheSameRun()
        {
            Install("saves_phase0", "run.json");

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot, "A phase-0 run.json must still be resumable (or banked by a migration).");
            Assert.IsTrue(RunState.Restore(snapshot));

            Assert.AreEqual(2026u, RunState.Seed);
            Assert.AreEqual(6, RunState.NodeIndex);
            Assert.AreEqual(45, RunState.EchoesEarned);
            Assert.AreEqual(2, RunState.RerollTokens);
            Assert.AreEqual(1, RunState.RevivesSpent);
            Assert.AreEqual(61.5f, RunState.PlayerHealth, 1e-4f);
            Assert.AreEqual(2, RunState.BoonCount(BoonId.Ironskin));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.KeenEdge));
            Assert.AreEqual(19, snapshot.runKills);
        }
    }
}

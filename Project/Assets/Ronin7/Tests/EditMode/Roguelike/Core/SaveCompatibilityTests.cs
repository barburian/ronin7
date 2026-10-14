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

        /// <summary>P2-04 bumped RunSnapshot to v2 (branching map): a phase-0 (v1) run cannot resume on
        /// the new map, so the P0-09 migration banks its 45 Echoes as a loss at depth 6 and deletes it.</summary>
        [Test]
        public void Phase0_RunJson_IsBankedByTheV2Migration()
        {
            Install("saves_phase0", "run.json");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("discarding version 1"));
            Assert.IsNull(RunSnapshot.Read());

            Assert.AreEqual(45, MetaProgression.Echoes);
            Assert.AreEqual(6, MetaProgression.BestDepth);
            Assert.AreEqual(1, MetaProgression.RunsCompleted);
            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath));
        }
    }
}

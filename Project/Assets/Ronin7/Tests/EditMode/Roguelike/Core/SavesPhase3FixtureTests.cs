using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>G7 for Phase 3 (no save-format change; snapshot stays v2). Fixtures in Project/TestData/saves_phase3/.</summary>
    public class SavesPhase3FixtureTests
    {
        private string testDirectory;

        private static string Fixture(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase3", file));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7p3_" + Guid.NewGuid().ToString("N"));
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
        public void Phase3Pair_ResumesAndRoundTripsExactly()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));

            MetaProgression.Load();
            Assert.AreEqual(612, MetaProgression.Echoes);
            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
            Assert.IsTrue(MetaProgression.TutorialDone);

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(RunSnapshot.CurrentVersion, snapshot.version, "Phase 3 must not bump the snapshot version");
            Assert.IsTrue(RunState.Restore(snapshot));
            Assert.AreEqual(2026u, RunState.Seed);
            Assert.AreEqual(8, RunState.NodeIndex);
            Assert.AreEqual(4, RunState.Depth);
            CollectionAssert.AreEqual(new[] { 0, 2, 5, 7, 8 }, RunState.Path);
            Assert.AreEqual(88, RunState.EchoesEarned);
            Assert.AreEqual(2, RunState.BoonCount(BoonId.Ironskin));
            Assert.IsTrue(RunState.HasGrantedAbility(AbilityId.Overdrive));

            RunSnapshot again = RunState.ToSnapshot();
            again.runKills = snapshot.runKills;
            Assert.AreEqual(File.ReadAllText(Fixture("run.json")), JsonUtility.ToJson(again), "snapshot must round-trip byte-for-byte");
        }
    }
}

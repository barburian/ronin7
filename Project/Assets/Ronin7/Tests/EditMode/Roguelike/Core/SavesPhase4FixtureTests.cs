using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>G7 for Phase 4 (P4-11, snapshot v2 -> v3; meta stays v1 with appended fields). Fixtures in
    /// Project/TestData/saves_phase4/.</summary>
    public class SavesPhase4FixtureTests
    {
        private string testDirectory;

        private static string Fixture(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase4", file));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7p4_" + Guid.NewGuid().ToString("N"));
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
        public void Phase4Meta_LoadsEveryNewField()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            MetaProgression.Load();

            Assert.AreEqual(940, MetaProgression.Echoes);
            Assert.IsTrue(MetaProgression.HasSeen(1) && MetaProgression.HasSeen(2) && MetaProgression.HasSeen(6));
            Assert.IsFalse(MetaProgression.HasSeen(3));
            Assert.IsTrue(MetaProgression.IsPackUnlocked(BoonPackId.Bleed));
            Assert.IsTrue(MetaProgression.IsPackUnlocked(BoonPackId.Executioner));
            Assert.IsFalse(MetaProgression.IsPackUnlocked(BoonPackId.Storm));
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Nodachi));
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Tanto));
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi));
            Assert.AreEqual(WeaponId.Nodachi, MetaProgression.SelectedLoadout);
            Assert.AreEqual(2, MetaProgression.HeatRankOf(HeatRank.SharpenedProgram));
            Assert.AreEqual(1, MetaProgression.HeatRankOf(HeatRank.HardenedShells));
            Assert.AreEqual(1, MetaProgression.HeatRankOf(HeatRank.RustedForge));
            CollectionAssert.AreEqual(new[] { BoonId.GrantOverdrive, BoonId.GrantMirror }, MetaProgression.PastEpics);
            Assert.IsTrue(MetaProgression.HasKeepsake(BoonId.GrantOverdrive));
            Assert.IsFalse(MetaProgression.HasKeepsake(BoonId.GrantMirror));
            Assert.AreEqual(BoonId.GrantOverdrive, MetaProgression.SelectedKeepsake);
            Assert.AreEqual("art.charged_slash", MetaProgression.EquippedArt(true));
            Assert.AreEqual("art.kiai_palm", MetaProgression.EquippedArt(false));
        }

        [Test]
        public void Phase4Pair_ResumesAndRoundTripsExactly()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));
            MetaProgression.Load();

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(3, snapshot.version);
            Assert.IsTrue(RunState.Restore(snapshot));
            Assert.AreEqual(2026u, RunState.Seed);
            Assert.AreEqual(4, RunState.Depth);
            CollectionAssert.AreEqual(new[] { 0, 2, 5, 7, 8 }, RunState.Path);
            Assert.AreEqual(95, RunState.Shards);
            Assert.AreEqual(940, MetaProgression.Echoes, "resuming banks nothing");
            Assert.AreEqual(2, RunState.TemperLevel(BoonId.KeenEdge));
            Assert.AreEqual("art.charged_slash", RunState.WeaponArtId);
            Assert.AreEqual("art.kiai_palm", RunState.OffhandArtId);
            Assert.AreEqual(WeaponId.Tanto, RunState.LoadoutId, "the run's own weapon, not today's meta pick (Nodachi)");
            Assert.AreEqual(4, RunState.HeatTotal);

            RunSnapshot again = RunState.ToSnapshot();
            again.runKills = snapshot.runKills;
            Assert.AreEqual(File.ReadAllText(Fixture("run.json")), JsonUtility.ToJson(again), "v3 must round-trip byte-for-byte");
        }
    }
}

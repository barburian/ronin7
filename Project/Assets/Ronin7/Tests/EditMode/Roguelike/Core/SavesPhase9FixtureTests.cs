using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>G7 for Phase 9 (P9-03): a pre-P9 meta.json/run.json (no metaBestHits / hitsTaken) loads cleanly.
    /// Fixtures in Project/TestData/saves_phase9/.</summary>
    public class SavesPhase9FixtureTests
    {
        private string testDirectory;

        private static string Fixture(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestData", "saves_phase9", file));

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_g7p9_" + Guid.NewGuid().ToString("N"));
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
        public void PreP9Pair_LoadsAndResumes()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));
            MetaProgression.Load();

            Assert.AreEqual(-1, MetaProgression.BestHits, "no best score");
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.AreEqual(940, MetaProgression.Echoes);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi), "owned Twin stays unlocked");
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Nodachi));
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout, "owned Nodachi plays as the Katana");

            RunSnapshot snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot.hitsTaken);
            Assert.IsTrue(RunState.Restore(snapshot));
            Assert.AreEqual(0, RunState.HitsTaken);
            Assert.AreEqual(940, MetaProgression.Echoes, "resuming banks nothing");
        }

        [Test]
        public void NewFields_RoundTrip()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            MetaProgression.Load();
            MetaProgression.NoteRunEnded(15, true, 3);
            MetaProgression.Save();
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(3, MetaProgression.BestHits);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(2, MetaProgression.RunsWon);
        }
    }
}

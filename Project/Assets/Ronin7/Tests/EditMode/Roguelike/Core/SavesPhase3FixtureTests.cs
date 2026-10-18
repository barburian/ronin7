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

        /// <summary>P4-11: snapshot v3 discards the Phase 3 (v2) run.json and banks its stored 88 Echoes as a
        /// loss at depth 4. The Phase 3 meta.json still loads unchanged (MetaSaveData stays v1).</summary>
        [Test]
        public void Phase3Pair_MetaLoads_RunDiscardedAndBanked()
        {
            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            File.Copy(Fixture("run.json"), Path.Combine(testDirectory, "run.json"));

            MetaProgression.Load();
            Assert.AreEqual(612, MetaProgression.Echoes);
            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
            Assert.IsTrue(MetaProgression.TutorialDone);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("discarding version 2 .*banking its 88 Echoes"));
            Assert.IsNull(RunSnapshot.Read());
            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath));
            Assert.AreEqual(612 + 88, MetaProgression.Echoes);
            Assert.AreEqual(22, MetaProgression.RunsCompleted);
            Assert.AreEqual(14, MetaProgression.BestDepth);
        }

        /// <summary>P4-11: a Phase-3-era meta.json lacks every Phase 4 field; each must read as its default.</summary>
        [Test]
        public void MetaSave_V1WithoutNewFields_LoadsDefaults()
        {
            string json = File.ReadAllText(Fixture("meta.json"));
            foreach (string f in new[] { "seenWeaknesses", "unlockedPacks", "unlockedWeapons", "selectedLoadout", "heatRanks",
                         "pastEpics", "keepsakes", "selectedKeepsake", "weaponArt", "offhandArt" })
                StringAssert.DoesNotContain(f, json, "fixture must predate Phase 4");

            MetaSaveData data = JsonUtility.FromJson<MetaSaveData>(json);
            Assert.AreEqual(1, data.version);
            Assert.AreEqual(612, data.metaEchoes);
            CollectionAssert.IsEmpty(data.seenWeaknesses);
            CollectionAssert.IsEmpty(data.unlockedPacks);
            CollectionAssert.IsEmpty(data.unlockedWeapons);
            Assert.AreEqual(WeaponId.Katana, data.selectedLoadout);
            CollectionAssert.IsEmpty(data.heatRanks);
            CollectionAssert.IsEmpty(data.pastEpics);
            CollectionAssert.IsEmpty(data.keepsakes);
            Assert.AreEqual("", data.selectedKeepsake);
            Assert.AreEqual("", data.weaponArt);
            Assert.AreEqual("", data.offhandArt);

            File.Copy(Fixture("meta.json"), Path.Combine(testDirectory, "meta.json"));
            MetaProgression.Load();
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
            for (int i = 0; i < Heat.Count; i++) Assert.AreEqual(0, MetaProgression.HeatRankOf((HeatRank)i));
        }
    }
}

using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class RunSnapshotTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            testDirectory = Path.Combine(Path.GetTempPath(), "ss_runsnaptests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveSystem.DirectoryOverride = testDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            SaveSystem.DirectoryOverride = null;
            try
            {
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to delete test directory {testDirectory}: {ex.Message}");
            }
        }

        private static void PlayIntoMidRun()
        {
            RunState.Begin(12345u);
            RunState.Advance(RunState.NextChoices[RunState.NextChoices.Count - 1]);
            RunState.Advance(RunState.NextChoices[0]);
            RunState.AddMaxHealthBonus(-15f);
            RunState.AddShards(37);
            RunState.AddRerollToken(2);
            RunState.NoteReviveSpent();
            RunState.NotePlayerHealth(42.5f);
            RunState.AddBoon(BoonId.Ironskin);
            RunState.AddBoon(BoonId.Ironskin);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.GrantAbility("dash");
        }

        [Test]
        public void WriteThenRead_RestoresIdenticalRunState()
        {
            PlayIntoMidRun();
            var snapshot = RunState.ToSnapshot();
            snapshot.runKills = 7;
            int node = RunState.NodeIndex;
            var path = new System.Collections.Generic.List<int>(RunState.Path);
            RunSnapshot.Write(snapshot);
            RunState.Reset();

            var loaded = RunSnapshot.Read();
            Assert.IsNotNull(loaded);
            Assert.IsTrue(RunState.Restore(loaded));

            Assert.IsTrue(RunState.InRun);
            Assert.AreEqual(12345u, RunState.Seed);
            Assert.AreEqual(3, loaded.version);
            Assert.AreEqual(node, RunState.NodeIndex);
            Assert.AreEqual(2, RunState.Depth);
            Assert.AreEqual(2, loaded.depth);
            CollectionAssert.AreEqual(path, RunState.Path);
            CollectionAssert.AreEqual(RunMapGenerator.GenerateGraph(12345u).Nodes, RunState.Map);
            Assert.AreEqual(-15f, RunState.MaxHealthBonus);
            Assert.AreEqual(37, RunState.Shards);
            Assert.AreEqual(2, RunState.RerollTokens);
            Assert.AreEqual(1, RunState.RevivesSpent);
            Assert.AreEqual(42.5f, RunState.PlayerHealth);
            Assert.AreEqual(2, RunState.BoonCount(BoonId.Ironskin));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.KeenEdge));
            Assert.AreEqual(2, RunState.Boons.Count);
            Assert.IsTrue(RunState.HasGrantedAbility("dash"));
            Assert.AreEqual(7, loaded.runKills);
        }

        [Test]
        public void Read_NoSnapshot_ReturnsNull()
        {
            Assert.IsNull(RunSnapshot.Read());
        }

        [Test]
        public void Read_VersionMismatch_DiscardsAndDeletes()
        {
            PlayIntoMidRun();
            var snapshot = RunState.ToSnapshot();
            snapshot.version = RunSnapshot.CurrentVersion + 1;
            RunSnapshot.Write(snapshot);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*discarding version.*"));
            Assert.IsNull(RunSnapshot.Read());
            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath), "a stale snapshot must not survive to prompt again");
        }

        [Test]
        public void Restore_VersionMismatchOrBadNode_ReturnsFalseAndLeavesNoRun()
        {
            PlayIntoMidRun();
            var stale = RunState.ToSnapshot();
            stale.version = 0;
            var badNode = RunState.ToSnapshot();
            badNode.nodeIndex = 999;
            var badPath = RunState.ToSnapshot();
            badPath.path = new System.Collections.Generic.List<int> { 0, badPath.nodeIndex }; // skips a row: not an edge
            RunState.Reset();

            Assert.IsFalse(RunState.Restore(stale));
            Assert.IsFalse(RunState.InRun);
            Assert.IsFalse(RunState.Restore(badNode));
            Assert.IsFalse(RunState.InRun);
            Assert.IsFalse(RunState.Restore(badPath));
            Assert.IsFalse(RunState.InRun);
        }

        [Test]
        public void Delete_RemovesSnapshotAndBackup()
        {
            PlayIntoMidRun();
            RunSnapshot.Write(RunState.ToSnapshot());
            RunSnapshot.Write(RunState.ToSnapshot()); // second write leaves a .bak behind
            Assert.IsTrue(File.Exists(SaveSystem.RunSnapshotPath + ".bak"));

            RunSnapshot.Delete();

            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath));
            Assert.IsFalse(File.Exists(SaveSystem.RunSnapshotPath + ".bak"));
            Assert.IsNull(RunSnapshot.Read(), "an ended run must not be resurrected from its backup");
        }

        /// <summary>P4-11: a v2 (pre-split) run.json stored the run's unbanked Echoes; discarding it banks them (BUG-25).</summary>
        [Test]
        public void VersionMismatch_BanksEchoes_V2()
        {
            MetaProgression.ResetForTests();
            PlayIntoMidRun();
            var snapshot = RunState.ToSnapshot();
            snapshot.version = 2;
            snapshot.echoesEarned = 37;
            snapshot.shards = 0;
            RunSnapshot.Write(snapshot);
            RunState.Reset();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*discarding version 2.*"));
            Assert.IsNull(RunSnapshot.Read());

            Assert.AreEqual(37, MetaProgression.Echoes, "A discarded v2 run's Echoes must be banked, not lost.");
            Assert.AreEqual(1, MetaProgression.RunsCompleted);
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(37, MetaProgression.Echoes, "...and persisted to meta.json.");
        }

        /// <summary>P4-11: a v3+ snapshot holds Shards, which are never banked; discarding it banks the loss
        /// Echo result for the depth reached instead.</summary>
        [Test]
        public void VersionMismatch_V3Plus_BanksLossResultNotShards()
        {
            MetaProgression.ResetForTests();
            PlayIntoMidRun(); // 37 Shards at depth 2
            var snapshot = RunState.ToSnapshot();
            Assert.AreEqual(37, snapshot.shards);
            Assert.AreEqual(0, snapshot.echoesEarned, "v3 writes Shards to their own field");
            snapshot.version = 99;
            RunSnapshot.Write(snapshot);
            RunState.Reset();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*discarding version 99.*"));
            Assert.IsNull(RunSnapshot.Read());

            Assert.AreEqual(RunScaling.EchoResult(2, false), MetaProgression.Echoes);
            Assert.AreEqual(72, MetaProgression.Echoes, "60 + 6 x depth 2; the 37 Shards are dropped");
            Assert.AreEqual(1, MetaProgression.RunsCompleted);
        }

        /// <summary>P4-11 v3: every deferred run field survives ToSnapshot -> JSON -> Restore, and the snapshot's
        /// loadout/heat override what Begin read from today's meta.</summary>
        [Test]
        public void V3_RoundTrip_AllDeferredFields()
        {
            PlayIntoMidRun();
            RunState.AddTemper(BoonId.KeenEdge);
            RunState.AddTemper(BoonId.KeenEdge);
            RunState.AddTemper(BoonId.Ironskin);
            RunState.SetWeaponArt("art_weapon_test");
            RunState.SetOffhandArt("art_offhand_test");
            var snapshot = RunState.ToSnapshot();
            Assert.AreEqual(3, snapshot.version);
            snapshot.loadoutId = WeaponId.Nodachi;           // meta still says katana / no heat
            snapshot.heatRanks = new System.Collections.Generic.List<int> { 2, 0, 1, 0, 1 }; // short list: rest read 0
            string json = JsonUtility.ToJson(snapshot);
            RunState.Reset();

            Assert.IsTrue(RunState.Restore(JsonUtility.FromJson<RunSnapshot>(json)));
            Assert.AreEqual(37, RunState.Shards);
            Assert.AreEqual(2, RunState.TemperLevel(BoonId.KeenEdge));
            Assert.AreEqual(1, RunState.TemperLevel(BoonId.Ironskin));
            Assert.AreEqual("art_weapon_test", RunState.WeaponArtId);
            Assert.AreEqual("art_offhand_test", RunState.OffhandArtId);
            Assert.AreEqual(WeaponId.Nodachi, RunState.LoadoutId);
            Assert.AreEqual(2, RunState.HeatRankOf(HeatRank.SharpenedProgram));
            Assert.AreEqual(1, RunState.HeatRankOf(HeatRank.QuickTelegraph));
            Assert.AreEqual(1, RunState.HeatRankOf(HeatRank.RustedForge));
            Assert.AreEqual(0, RunState.HeatRankOf(HeatRank.EscortedBosses));
            Assert.AreEqual(4, RunState.HeatTotal);

            var again = RunState.ToSnapshot();
            CollectionAssert.AreEqual(new[] { 2, 0, 1, 0, 1, 0, 0, 0 }, again.heatRanks);
            CollectionAssert.AreEqual(snapshot.temperLevels, again.temperLevels);
            Assert.AreEqual(WeaponId.Nodachi, again.loadoutId);
            Assert.AreEqual(37, again.shards);
        }
    }
}

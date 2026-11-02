using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P7-04 (BUG-51): boons at a fresh run start are the Shrine's, labelled as such; no previous-run
    /// state leaks into a fresh run.</summary>
    public class FreshRunBoonTests
    {
        private string dir;
        private GameObject go;
        private RunDirector director;

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            dir = Path.Combine(Path.GetTempPath(), "ronin7_fresh_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            RunLog.DirectoryOverride = dir;
            MetaProgression.Load();
            go = new GameObject("RunDirector");
            director = go.AddComponent<RunDirector>();
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            var boons = new BoonDefinition[5];
            for (int i = 0; i < boons.Length; i++)
            {
                boons[i] = ScriptableObject.CreateInstance<BoonDefinition>();
                boons[i].id = "test_common_" + i;
                boons[i].rarity = BoonRarity.Common;
                boons[i].maxStacks = 3;
            }
            catalog.boons = boons;
            typeof(RunDirector).GetField("boonCatalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(director, catalog);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            UnityEngine.Object.DestroyImmediate(go);
            RunState.Reset();
            RunSummary.Clear();
            MetaProgression.ResetForTests();
            SaveSystem.DirectoryOverride = null;
            RunLog.DirectoryOverride = null;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        private void Start(uint seed)
        {
            LogAssert.ignoreFailingMessages = true; // "No GameFlowManager" on every scene load in EditMode
            director.StartRun(seed);
        }

        [Test]
        public void StartRun_Fresh_HasNoRunBoons()
        {
            Start(11u);
            Assert.AreEqual(0, RunState.Boons.Count);
            Assert.AreEqual(0, RunState.ShrineStacks);
            Assert.AreEqual(0, RunState.RunBoonCount);
        }

        [Test]
        public void StartRun_WithShrineStartingBoons_EveryBoonIsShrineTagged()
        {
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingBoon, 3);
            Start(11u);
            Assert.AreEqual(3, RunState.ShrineStacks);
            Assert.AreEqual(3, RunState.Boons.Count);
            foreach (string id in RunState.Boons)
                Assert.AreEqual(RunState.BoonCount(id), RunState.ShrineBoonCount(id), id + " must be all-shrine");
            Assert.AreEqual(0, RunState.RunBoonCount, "nothing non-shrine on a fresh run");
        }

        [Test]
        public void RunPickedBoon_IsNotShrine()
        {
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingBoon, 1);
            Start(11u);
            RunState.AddBoon("picked");
            Assert.AreEqual(1, RunState.RunBoonCount);
            Assert.AreEqual(1, RunState.ShrineStacks);
            Assert.AreEqual(0, RunState.ShrineBoonCount("picked"));
        }

        [Test]
        public void ShrineTags_SurviveSnapshotRoundTrip_AndLegacyLoadsEmpty()
        {
            RunState.Begin(5u);
            RunState.AddShrineBoon("a");
            RunState.AddBoon("a");
            RunState.AddBoon("b");
            var json = JsonUtility.ToJson(RunState.ToSnapshot());
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(JsonUtility.FromJson<RunSnapshot>(json)));
            Assert.AreEqual(1, RunState.ShrineBoonCount("a"));
            Assert.AreEqual(2, RunState.BoonCount("a"));
            Assert.AreEqual(2, RunState.RunBoonCount);

            var legacy = RunState.ToSnapshot();
            legacy.shrineBoons = null; // older run.json has no such field
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(legacy));
            Assert.AreEqual(0, RunState.ShrineStacks);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EndRun_DeathOrWin_LeavesNoSnapshot_NextRunClean(bool won)
        {
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingBoon, 2);
            Start(21u);
            RunState.AddBoon("picked");
            Assert.IsNotNull(RunSnapshot.Read());
            LogAssert.ignoreFailingMessages = true;
            director.EndRun(won);
            Assert.IsNull(RunSnapshot.Read());
            RunState.End(); // the menu load does this
            Assert.AreEqual(0, RunState.Boons.Count);
            Assert.AreEqual(0, RunState.ShrineStacks);

            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingBoon, 0);
            director.ClearEndingLatch();
            Start(22u);
            Assert.AreEqual(0, RunState.Boons.Count, "no boons leak from the previous run");
        }

        [Test]
        public void Abandon_LeavesNoSnapshot_NextRunClean()
        {
            Start(31u);
            RunState.AddBoon("picked");
            RunState.Reset(); // at the menu: saved, not live
            LogAssert.ignoreFailingMessages = true;
            director.AbandonSavedRun();
            Assert.IsNull(RunSnapshot.Read());
            Start(32u);
            Assert.AreEqual(0, RunState.Boons.Count);
        }

        [Test]
        public void StartRun_Seeded_IgnoresStaleSavedRun()
        {
            RunState.Begin(7u);
            RunState.AddBoon("stale_boon");
            RunSnapshot.Write(RunState.ToSnapshot());
            RunState.Reset();

            Start(8u);

            Assert.AreEqual(8u, RunState.Seed);
            Assert.AreEqual(0, RunState.BoonCount("stale_boon"));
            Assert.AreEqual(0, RunState.Boons.Count);
            Assert.AreEqual(8u, RunSnapshot.Read().seed, "the stale file is overwritten");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Tests.EditMode.Roguelike.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike
{
    /// <summary>P8-03b: the double-elite room — final sector only, one per run, on an optional side lane appended
    /// after the main grid (main-path ids/kinds/edges unchanged), two distinct elites + 4 escorts, paid as Elite.</summary>
    public class DoubleEliteRoomTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            RunState.Reset();
        }

        private static IEnumerable<uint> Seeds()
        {
            for (uint s = 1; s <= 300; s++) yield return s;
            yield return 0u;
            yield return uint.MaxValue;
        }

        private static List<RunNode> DoubleElites(RunMap map) =>
            new List<RunNode>(System.Array.FindAll(map.Nodes, n => n.Kind == RoomKind.DoubleElite));

        [Test]
        public void DoubleElite_OnlyInFinalSector()
        {
            foreach (uint seed in Seeds())
            {
                var map = RunMapGenerator.GenerateGraph(seed);
                foreach (var n in DoubleElites(map))
                {
                    Assert.AreEqual(RunMapGenerator.SectorCount - 1, n.Sector, $"seed {seed}");
                    Assert.AreEqual(n.Sector * RunMapGenerator.NodesPerSector + n.IndexInSector, n.Depth);
                    Assert.That(n.Depth, Is.InRange(0, RunMapGenerator.TotalNodes - 1));
                    Assert.AreEqual(map.Nodes.Length - 1, n.Index, $"seed {seed}: the branch node is appended last");
                    // Optional branch: reachable, has an exit one row down, and some sibling on its row is not it.
                    Assert.IsNotNull(map.PathTo(n.Index), $"seed {seed}: branch unreachable");
                    Assert.Greater(map.Next[n.Index].Length, 0, $"seed {seed}: branch is a dead end");
                    foreach (int to in map.Next[n.Index]) Assert.AreEqual(n.Depth + 1, map.Depth(to));
                    for (int id = 0; id < map.Nodes.Length; id++)
                        if (map.HasEdge(id, n.Index)) Assert.Greater(map.Next[id].Length, 1, $"seed {seed}: branch is not optional");
                }
            }
        }

        [Test]
        public void DoubleElite_AtMostOnePerRun()
        {
            bool sawOne = false;
            foreach (uint seed in Seeds())
            {
                int count = DoubleElites(RunMapGenerator.GenerateGraph(seed)).Count;
                Assert.LessOrEqual(count, 1, $"seed {seed}");
                sawOne |= count == 1;
            }
            Assert.IsTrue(sawOne, "the branch should appear across 300 seeds");
        }

        private EnemySpawnTable.Entry Entry(float hp, bool eliteOnly)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = hp;
            var prefab = new GameObject("DummyEnemy");
            created.Add(def);
            created.Add(prefab);
            return new EnemySpawnTable.Entry { prefab = prefab, definition = def, weight = 10, minDepth = 0, eliteOnly = eliteOnly };
        }

        private EnemySpawnTable Table(params EnemySpawnTable.Entry[] trash)
        {
            var t = ScriptableObject.CreateInstance<EnemySpawnTable>();
            t.trash = trash;
            created.Add(t);
            return t;
        }

        [TestCase(2)] // two elite-only entries: both lead
        [TestCase(1)] // one elite-only entry: the strongest remaining trash is the second lead
        public void DoubleElite_TwoDistinctElites(int eliteOnlyEntries)
        {
            var table = eliteOnlyEntries == 2
                ? Table(Entry(60f, false), Entry(100f, false), Entry(150f, true), Entry(180f, true))
                : Table(Entry(60f, false), Entry(100f, false), Entry(150f, true));
            var node = new RunNode(29, 2, 2, RoomKind.DoubleElite, 3, 12);
            for (uint seed = 1; seed <= 50; seed++)
            {
                var rng = new RunRng(seed);
                var result = WaveComposer.Compose(table, node, 8f, Vector3.zero, ref rng);
                Assert.AreEqual(8, result.Count, "2 elites + 6 escorts (sector 2)");
                Assert.IsTrue(result[0].IsElite && result[1].IsElite, "both elites lead, so both are in the first wave");
                Assert.AreNotEqual(result[0].EntryIndex, result[1].EntryIndex, "two DIFFERENT elite types");
                for (int i = 2; i < result.Count; i++) Assert.IsFalse(result[i].IsElite, $"escort {i}");
                if (eliteOnlyEntries == 2)
                    CollectionAssert.AreEquivalent(new[] { 2, 3 }, new[] { result[0].EntryIndex, result[1].EntryIndex });
                else
                    CollectionAssert.AreEquivalent(new[] { 2, 1 }, new[] { result[0].EntryIndex, result[1].EntryIndex });
            }
        }

        [Test]
        public void DoubleElite_OptionalBranch_MainPathUnchanged()
        {
            // Captured from the generator before P8-03b (seeds 1 and 2026 are RunMapGraphTests' goldens).
            var before = new Dictionary<uint, string>
            {
                [7u] = "CBT | ELT CBT ELT | ELT ELT TRS | FRG SHP | BOS | CBT | CBT ELT CBT | ELT CBT | FRG SHP | BOS | CBT | CBT CBT SHP | CBT CBT CBT | FRG SHP | BOS :: " +
                    "0>1,2,3 1>4,5 2>4,5,6 3>5,6 4>7,8 5>7,8 6>8 7>9 8>9 9>10 10>11,12,13 11>14,15 12>14,15 13>15 14>16,17 15>16,17 16>18 17>18 18>19 19>20,21,22 20>23,24 21>23,24,25 22>24,25 23>26,27 24>26,27 25>27 26>28 27>28 28>",
                [42u] = "CBT | ELT ELT | ELT CBT | FRG SHP | BOS | CBT | CBT EVT | ELT CBT TRS | FRG SHP | BOS | CBT | ELT CBT | ELT ELT | FRG SHP | BOS :: " +
                    "0>1,2 1>3,4 2>3,4 3>5,6 4>5,6 5>7 6>7 7>8 8>9,10 9>11,12 10>11,12,13 11>14,15 12>14,15 13>15 14>16 15>16 16>17 17>18,19 18>20,21 19>20,21 20>22,23 21>22,23 22>24 23>24 24>",
                [123456u] = "CBT | EVT EVT | EVT TRS ELT | FRG SHP | BOS | CBT | ELT TRS | CBT CBT | FRG SHP | BOS | CBT | SHP ELT EVT | CBT EVT CBT | FRG SHP | BOS :: " +
                    "0>1,2 1>3,4 2>3,4,5 3>6,7 4>6,7 5>7 6>8 7>8 8>9 9>10,11 10>12,13 11>12,13 12>14,15 13>14,15 14>16 15>16 16>17 17>18,19,20 18>21,22 19>21,22,23 20>22,23 21>24,25 22>24,25 23>25 24>26 25>26 26>",
            };
            foreach (var kv in before)
            {
                var map = RunMapGenerator.GenerateGraph(kv.Key);
                Assert.AreEqual(1, DoubleElites(map).Count, $"seed {kv.Key}: branch present");
                Assert.AreEqual(kv.Value, RunMapGraphTests.Describe(map), $"seed {kv.Key}: main grid changed");
            }
        }

        [Test]
        public void DoubleElite_SnapshotRoundTrip()
        {
            const uint seed = 2026u;
            RunState.Begin(seed);
            var branch = DoubleElites(RunState.Graph)[0];
            var walk = RunState.Graph.PathTo(branch.Index);
            for (int i = 1; i < walk.Count; i++) Assert.IsTrue(RunState.Advance(walk[i]));
            string json = JsonUtility.ToJson(RunState.ToSnapshot());
            RunState.Reset();

            var loaded = JsonUtility.FromJson<RunSnapshot>(json);
            Assert.AreEqual(3, loaded.version, "no version bump");
            Assert.IsTrue(RunState.Restore(loaded));
            Assert.AreEqual(branch.Index, RunState.NodeIndex);
            Assert.AreEqual(RoomKind.DoubleElite, RunState.CurrentNode.Kind);
            Assert.AreEqual(branch.Depth, RunState.Depth);

            // An old v3 save (main-path ids only, written before the branch existed) still replays.
            RunState.Reset();
            var old = new RunSnapshot { version = 3, seed = seed, nodeIndex = 2, depth = 1, path = new List<int> { 0, 2 } };
            Assert.IsTrue(RunState.Restore(old));
            Assert.AreEqual(2, RunState.NodeIndex);
        }

        [Test]
        public void DoubleElite_RewardMatchesElite()
        {
            for (int depth = 0; depth < RunMapGenerator.TotalNodes; depth++)
            {
                Assert.AreEqual(RunScaling.ShardReward(depth, RoomKind.Elite), RunScaling.ShardReward(depth, RoomKind.DoubleElite));
                Assert.AreEqual(RunScaling.HealthMultiplier(depth, RoomKind.Elite), RunScaling.HealthMultiplier(depth, RoomKind.DoubleElite));
                Assert.AreEqual(RunScaling.DamageMultiplier(depth, RoomKind.Elite), RunScaling.DamageMultiplier(depth, RoomKind.DoubleElite));
            }
            foreach (BoonRarity r in System.Enum.GetValues(typeof(BoonRarity)))
                Assert.AreEqual(BoonOfferPicker.RarityWeight(r, RoomKind.Elite), BoonOfferPicker.RarityWeight(r, RoomKind.DoubleElite), r.ToString());
            Assert.AreEqual(RunDirector.ResolvePostClear(RoomKind.Elite, false), RunDirector.ResolvePostClear(RoomKind.DoubleElite, false));
            Assert.AreEqual(RunScaling.ArenaHalfExtent(RoomKind.Elite), RunScaling.ArenaHalfExtent(RoomKind.DoubleElite));
            Assert.AreNotEqual(RoomKindText.Name(RoomKind.Elite), RoomKindText.Name(RoomKind.DoubleElite), "readable distinct label");
        }
    }
}

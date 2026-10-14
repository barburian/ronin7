using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>P2-04: the branching run map (<see cref="RunMapGenerator.GenerateGraph"/>) and
    /// <see cref="RunState.Advance(int)"/>.</summary>
    public class RunMapGraphTests
    {
        [TearDown]
        public void TearDown() => RunState.Reset();

        /// <summary>Rows joined by " | " (nodes as 3-letter codes, lanes left to right), then each node's
        /// out-edges "id>a,b".</summary>
        internal static string Describe(RunMap map)
        {
            var sb = new StringBuilder();
            int lastDepth = 0;
            foreach (var n in map.Nodes)
            {
                if (n.Index > 0) sb.Append(n.Depth != lastDepth ? " | " : " ");
                lastDepth = n.Depth;
                sb.Append(RoomKindText.Code(n.Kind));
            }
            sb.Append(" ::");
            for (int i = 0; i < map.Next.Length; i++)
                sb.Append(' ').Append(i).Append('>').Append(string.Join(",", map.Next[i]));
            return sb.ToString();
        }

        // if these change, bump RunSnapshot.CurrentVersion (banking migration in Read handles old runs)
        private const string GoldenSeed1 = 
            "CBT | TRS ELT CBT | CBT SHP ELT | FRG SHP | BOS | CBT | CBT SHP ELT | EVT ELT | FRG SHP | BOS | CBT | CBT CBT CBT | CBT TRS CBT | FRG SHP | BOS :: " +
            "0>1,2,3 1>4,5 2>4,5,6 3>5,6 4>7,8 5>7,8 6>8 7>9 8>9 9>10 10>11,12,13 11>14,15 12>14,15 13>15 14>16,17 15>16,17 16>18 17>18 18>19 19>20,21,22 20>23,24 21>23,24,25 22>24,25 23>26,27 24>26,27 25>27 26>28 27>28 28>";
        private const string GoldenSeed2026 = 
            "CBT | ELT SHP | CBT TRS ELT | FRG SHP | BOS | CBT | ELT ELT | EVT CBT CBT | FRG SHP | BOS | CBT | CBT SHP CBT | ELT TRS | FRG SHP | BOS :: " +
            "0>1,2 1>3,4 2>3,4,5 3>6,7 4>6,7 5>7 6>8 7>8 8>9 9>10,11 10>12,13 11>12,13,14 12>15,16 13>15,16 14>16 15>17 16>17 17>18 18>19,20,21 19>22,23 20>22,23 21>23 22>24,25 23>24,25 24>26 25>26 26>";

        [Test]
        public void Golden_Seed1_KindsAndEdges() => Assert.AreEqual(GoldenSeed1, Describe(RunMapGenerator.GenerateGraph(1u)));

        [Test]
        public void Golden_Seed2026_KindsAndEdges() => Assert.AreEqual(GoldenSeed2026, Describe(RunMapGenerator.GenerateGraph(2026u)));

        private static IEnumerable<uint> Seeds()
        {
            for (uint s = 1; s <= 300; s++) yield return s;
            yield return 0u;
            yield return uint.MaxValue;
        }

        [Test]
        public void Bfs_ReachesEveryNode()
        {
            foreach (uint seed in Seeds())
            {
                var map = RunMapGenerator.GenerateGraph(seed);
                Assert.AreEqual(0, map.StartId);
                for (int id = 0; id < map.Nodes.Length; id++)
                    Assert.IsNotNull(map.PathTo(id), $"seed {seed}: node {id} unreachable");
            }
        }

        [Test]
        public void EveryNonFinalNode_HasOutEdge_FinalHasNone_EdgesGoOneRowDown()
        {
            foreach (uint seed in Seeds())
            {
                var map = RunMapGenerator.GenerateGraph(seed);
                int final = map.Nodes.Length - 1;
                Assert.AreEqual(RoomKind.Boss, map.Nodes[final].Kind);
                Assert.AreEqual(RunMapGenerator.TotalNodes - 1, map.Depth(final));
                for (int id = 0; id < map.Nodes.Length; id++)
                {
                    if (id == final) { Assert.AreEqual(0, map.Next[id].Length, $"seed {seed}: final node has edges"); continue; }
                    Assert.Greater(map.Next[id].Length, 0, $"seed {seed}: node {id} is a dead end");
                    Assert.LessOrEqual(map.Next[id].Length, 3, $"seed {seed}: node {id} has > 3 exits");
                    foreach (int to in map.Next[id])
                        Assert.AreEqual(map.Depth(id) + 1, map.Depth(to), $"seed {seed}: edge {id}>{to} skips a row");
                }
            }
        }

        [Test]
        public void Rows_LaneCounts_AndDepthIsSectorTimesFivePlusRow()
        {
            foreach (uint seed in Seeds())
            {
                var map = RunMapGenerator.GenerateGraph(seed);
                var lanes = new int[RunMapGenerator.TotalNodes];
                foreach (var n in map.Nodes)
                {
                    Assert.AreEqual(n.Sector * RunMapGenerator.NodesPerSector + n.IndexInSector, n.Depth);
                    Assert.AreEqual(lanes[n.Depth], n.Lane, $"seed {seed}: lanes out of order");
                    lanes[n.Depth]++;
                }
                for (int d = 0; d < lanes.Length; d++)
                {
                    int row = d % RunMapGenerator.NodesPerSector;
                    if (row == 0 || row == 4) Assert.AreEqual(1, lanes[d], $"seed {seed} depth {d}");
                    else if (row == 3) Assert.AreEqual(2, lanes[d], $"seed {seed} depth {d}");
                    else Assert.That(lanes[d], Is.InRange(2, 3), $"seed {seed} depth {d}");
                }
            }
        }

        [Test]
        public void ExactlyOneBossPerSector_AtRow4_Row0IsCombat()
        {
            foreach (uint seed in Seeds())
            {
                var bosses = new int[RunMapGenerator.SectorCount];
                foreach (var n in RunMapGenerator.GenerateGraph(seed).Nodes)
                {
                    if (n.Kind == RoomKind.Boss)
                    {
                        bosses[n.Sector]++;
                        Assert.AreEqual(4, n.IndexInSector, $"seed {seed}: boss off row 4");
                    }
                    if (n.IndexInSector == 0) Assert.AreEqual(RoomKind.Combat, n.Kind);
                }
                foreach (int b in bosses) Assert.AreEqual(1, b, $"seed {seed}");
            }
        }

        [Test]
        public void Row3_IsForgeOrShop_AndContainsAForge()
        {
            foreach (uint seed in Seeds())
            {
                var forge = new bool[RunMapGenerator.SectorCount];
                foreach (var n in RunMapGenerator.GenerateGraph(seed).Nodes)
                {
                    if (n.IndexInSector != 3) continue;
                    Assert.IsTrue(n.Kind == RoomKind.Forge || n.Kind == RoomKind.Shop, $"seed {seed}: row 3 had {n.Kind}");
                    forge[n.Sector] |= n.Kind == RoomKind.Forge;
                }
                foreach (bool f in forge) Assert.IsTrue(f, $"seed {seed}: a sector has no Forge");
            }
        }

        [Test]
        public void PerSector_AtMostOneTreasureAndOneShop_InRolledRows()
        {
            bool sawShop = false, sawEvent = false, sawTreasure = false;
            foreach (uint seed in Seeds())
            {
                var treasure = new int[RunMapGenerator.SectorCount];
                var shop = new int[RunMapGenerator.SectorCount];
                foreach (var n in RunMapGenerator.GenerateGraph(seed).Nodes)
                {
                    if (n.IndexInSector != 1 && n.IndexInSector != 2) continue;
                    if (n.Kind == RoomKind.Treasure) { treasure[n.Sector]++; sawTreasure = true; }
                    if (n.Kind == RoomKind.Shop) { shop[n.Sector]++; sawShop = true; }
                    if (n.Kind == RoomKind.Event) sawEvent = true;
                    Assert.AreNotEqual(RoomKind.Forge, n.Kind);
                    Assert.AreNotEqual(RoomKind.Boss, n.Kind);
                }
                for (int s = 0; s < RunMapGenerator.SectorCount; s++)
                {
                    Assert.LessOrEqual(treasure[s], 1, $"seed {seed} sector {s}: two Treasures");
                    Assert.LessOrEqual(shop[s], 1, $"seed {seed} sector {s}: two Shops");
                }
            }
            Assert.IsTrue(sawShop && sawEvent && sawTreasure, "every rolled kind should appear across 300 seeds");
        }

        [Test]
        public void SameSeed_IdenticalGraph() =>
            Assert.AreEqual(Describe(RunMapGenerator.GenerateGraph(2026u)), Describe(RunMapGenerator.GenerateGraph(2026u)));

        [Test]
        public void Advance_Invalid_ReturnsFalse_AndDoesNotMove()
        {
            RunState.Begin(2026u);
            int start = RunState.NodeIndex;
            var map = RunState.Graph;
            int notAnEdge = map.Nodes.Length - 1; // the final boss is never adjacent to the start
            Assert.IsFalse(RunState.Advance(notAnEdge));
            Assert.IsFalse(RunState.Advance(-1));
            Assert.IsFalse(RunState.Advance(9999));
            Assert.IsFalse(RunState.Advance(start), "no self-loops");
            Assert.AreEqual(start, RunState.NodeIndex);
            Assert.AreEqual(0, RunState.Depth);
            CollectionAssert.AreEqual(new[] { start }, RunState.Path);
        }

        [Test]
        public void Advance_Valid_MovesAppendsPathAndDeepens()
        {
            RunState.Begin(2026u);
            int pick = RunState.NextChoices[RunState.NextChoices.Count - 1];
            Assert.IsTrue(RunState.Advance(pick));
            Assert.AreEqual(pick, RunState.NodeIndex);
            Assert.AreEqual(1, RunState.Depth);
            CollectionAssert.AreEqual(new[] { 0, pick }, RunState.Path);
        }
    }
}

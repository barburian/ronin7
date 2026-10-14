using System;
using System.Collections.Generic;

namespace Ronin7.Core
{
    /// <summary>
    /// P2-04: builds a run's branching map — 3 sectors x 5 rows, deterministic from a seed. Pure (no
    /// Unity API), so the same seed produces an identical map forever (daily seeds, replays, resume).
    ///
    /// Per sector: row 0 = 1 Combat; rows 1-2 = 2-3 lanes rolled Combat 40 / Elite 25 / Event 15 /
    /// Treasure 10 / Shop 10 (at most one Treasure and one Shop across rows 1-2 of a sector); row 3 =
    /// Forge + Shop; row 4 = 1 Boss. Edges run lane l -> lanes l-1..l+1 of the next row; a node left
    /// without an incoming edge is joined from the nearest lane of the row before. Depth = sector x 5 +
    /// row, so <see cref="RunScaling"/> is unchanged.
    /// </summary>
    public static class RunMapGenerator
    {
        public const int SectorCount = 3;
        public const int NodesPerSector = 5;          // rows per sector
        public const int TotalNodes = SectorCount * NodesPerSector; // 15 = depths on any path

        private static readonly int[] MidWeights = { 40, 25, 15, 10, 10 };
        private static readonly RoomKind[] MidKinds = { RoomKind.Combat, RoomKind.Elite, RoomKind.Event, RoomKind.Treasure, RoomKind.Shop };

        public static RunMap GenerateGraph(uint seed)
        {
            RunRng rng = new RunRng(seed);
            var nodes = new List<RunNode>(32);
            var rows = new List<int[]>(TotalNodes); // node ids per global row

            for (int sector = 0; sector < SectorCount; sector++)
            {
                bool treasureUsed = false, shopUsed = false;
                for (int row = 0; row < NodesPerSector; row++)
                {
                    int lanes = row == 0 || row == 4 ? 1 : row == 3 ? 2 : 2 + rng.NextInt(2);
                    int[] ids = new int[lanes];
                    for (int lane = 0; lane < lanes; lane++)
                    {
                        RoomKind kind;
                        if (row == 0) kind = RoomKind.Combat;
                        else if (row == 4) kind = RoomKind.Boss;
                        else if (row == 3) kind = lane == 0 ? RoomKind.Forge : RoomKind.Shop;
                        else
                        {
                            kind = RollMidKind(ref rng, treasureUsed, shopUsed);
                            if (kind == RoomKind.Treasure) treasureUsed = true;
                            if (kind == RoomKind.Shop) shopUsed = true;
                        }
                        ids[lane] = nodes.Count;
                        nodes.Add(new RunNode(nodes.Count, sector, row, kind, lane, sector * NodesPerSector + row));
                    }
                    rows.Add(ids);
                }
            }

            var next = new List<int>[nodes.Count];
            for (int i = 0; i < next.Length; i++) next[i] = new List<int>(3);
            for (int r = 0; r + 1 < rows.Count; r++)
            {
                int[] from = rows[r], to = rows[r + 1];
                var hasIn = new bool[to.Length];
                for (int l = 0; l < from.Length; l++)
                    for (int t = l - 1; t <= l + 1; t++)
                        if (t >= 0 && t < to.Length) { next[from[l]].Add(to[t]); hasIn[t] = true; }
                // Repair orphans: join from the nearest lane of the previous row (ties -> lower lane).
                for (int t = 0; t < to.Length; t++)
                    if (!hasIn[t]) next[from[Math.Min(t, from.Length - 1)]].Add(to[t]);
                // Every non-final node gets an out-edge by construction (lane l always reaches
                // min(l, last) or l-1), but guard anyway: join to the nearest lane of the next row.
                for (int l = 0; l < from.Length; l++)
                    if (next[from[l]].Count == 0) next[from[l]].Add(to[Math.Min(l, to.Length - 1)]);
            }

            var nextArr = new int[next.Length][];
            for (int i = 0; i < next.Length; i++)
            {
                next[i].Sort();
                nextArr[i] = next[i].ToArray();
            }
            return new RunMap(nodes.ToArray(), nextArr, 0);
        }

        /// <summary>Weighted rows-1-2 roll; Treasure/Shop weights drop to 0 once the sector has one.</summary>
        private static RoomKind RollMidKind(ref RunRng rng, bool treasureUsed, bool shopUsed)
        {
            Span<int> weights = stackalloc int[5];
            for (int i = 0; i < 5; i++) weights[i] = MidWeights[i];
            if (treasureUsed) weights[3] = 0;
            if (shopUsed) weights[4] = 0;
            int pick = rng.PickWeighted(weights);
            return pick < 0 ? RoomKind.Combat : MidKinds[pick];
        }
    }
}

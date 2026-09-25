using System;

namespace Ronin7.Core
{
    /// <summary>
    /// Builds a run's 15-node map: 3 sectors x 5 nodes, deterministic from a seed. Pure — no Unity
    /// API — so the same seed produces a byte-identical map forever (daily seeds, replays).
    /// </summary>
    public static class RunMapGenerator
    {
        public const int SectorCount = 3;
        public const int NodesPerSector = 5;
        public const int TotalNodes = SectorCount * NodesPerSector; // 15

        private const int MidCombatWeight = 55;
        private const int MidEliteWeight = 30;
        private const int MidTreasureWeight = 15;

        /// <summary>Deterministic: same seed =&gt; identical map. Always <see cref="TotalNodes"/> long.</summary>
        public static RunNode[] Generate(uint seed)
        {
            RunRng rng = new RunRng(seed);
            RunNode[] nodes = new RunNode[TotalNodes];

            int globalIndex = 0;
            for (int sector = 0; sector < SectorCount; sector++)
            {
                bool treasureUsed = false;
                for (int indexInSector = 0; indexInSector < NodesPerSector; indexInSector++)
                {
                    RoomKind kind;
                    switch (indexInSector)
                    {
                        case 0:
                            kind = RoomKind.Combat; // guaranteed soft opener
                            break;
                        case 3:
                            kind = RoomKind.Forge;
                            break;
                        case 4:
                            kind = RoomKind.Boss;
                            break;
                        default:
                            kind = RollMidKind(ref rng, treasureUsed);
                            if (kind == RoomKind.Treasure) treasureUsed = true;
                            break;
                    }

                    nodes[globalIndex] = new RunNode(globalIndex, sector, indexInSector, kind);
                    globalIndex++;
                }
            }

            return nodes;
        }

        /// <summary>Weighted Combat/Elite/Treasure roll for idx 1-2, with Treasure's weight zeroed
        /// once a sector has already rolled one (a sector never rolls two Treasures).</summary>
        private static RoomKind RollMidKind(ref RunRng rng, bool treasureUsed)
        {
            Span<int> weights = stackalloc int[3];
            weights[0] = MidCombatWeight;
            weights[1] = MidEliteWeight;
            weights[2] = treasureUsed ? 0 : MidTreasureWeight;

            int pick = rng.PickWeighted(weights);
            return pick switch
            {
                1 => RoomKind.Elite,
                2 => RoomKind.Treasure,
                _ => RoomKind.Combat, // pick == 0, or -1 defensive fallback (weights are never all <= 0 here)
            };
        }
    }
}

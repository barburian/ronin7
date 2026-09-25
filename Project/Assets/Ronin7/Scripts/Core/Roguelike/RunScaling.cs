using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Pure difficulty curve read by Module 3 (arena assembly) when it spawns/configures enemies
    /// for a node. depth is the global node index 0..14 (<see cref="RunNode.Index"/>).
    /// </summary>
    public static class RunScaling
    {
        private const int MaxDepth = RunMapGenerator.TotalNodes - 1; // 14

        /// <summary>A2.2/A5.5: hard cap on enemies active in the arena at once. Room-scale VR melee
        /// caps out around what this project's own hand-tuned content already uses (Ch2: 3
        /// enforcers + 1 bodyguard; Ch10: 3 guards then 2 automatons) — a player with a physical
        /// sword can only address ~120° of arc, and there is no off-screen hit feedback (no camera
        /// shake). Module 3 spawns the node's full <see cref="EnemyCount"/> but only ever activates
        /// up to this many at once, drip-releasing one more per death.</summary>
        public const int MaxConcurrentEnemies = 4;

        /// <summary>1.0 .. ~2.6. Linear depth curve, boosted further for Elite/Boss nodes.</summary>
        public static float HealthMultiplier(int depth, RoomKind kind)
        {
            float t = Mathf.Clamp01(depth / (float)MaxDepth);
            float baseMultiplier = Mathf.Lerp(1f, 2f, t);
            return baseMultiplier * KindHealthFactor(kind);
        }

        /// <summary>1.0 .. ~1.9. Linear depth curve, boosted further for Elite/Boss nodes.</summary>
        public static float DamageMultiplier(int depth, RoomKind kind)
        {
            float t = Mathf.Clamp01(depth / (float)MaxDepth);
            float baseMultiplier = Mathf.Lerp(1f, 1.5f, t);
            return baseMultiplier * KindDamageFactor(kind);
        }

        /// <summary>Trash count for Combat, elite count for Elite, always 1 for Boss. Forge/Treasure
        /// have no fight, so they always return 0 without touching rng.</summary>
        public static int EnemyCount(int depth, RoomKind kind, ref RunRng rng)
        {
            switch (kind)
            {
                case RoomKind.Forge:
                case RoomKind.Treasure:
                    return 0;
                case RoomKind.Boss:
                    return 1;
                case RoomKind.Elite:
                    // A second elite joins in the final sector.
                    return depth >= NodesPerSectorBoundary(2) ? 2 : 1;
                default: // Combat
                    int perSectorBonus = depth / RunMapGenerator.NodesPerSector; // +1 trash per sector reached
                    return 3 + perSectorBonus + rng.NextInt(2); // 3-4 base, plus depth scaling
            }
        }

        /// <summary>A2.3: Forge is a no-fight heal room and must always pay 0, regardless of depth —
        /// the depth bonus below is for nodes that were actually fought.</summary>
        public static int EchoReward(int depth, RoomKind kind)
        {
            if (kind == RoomKind.Forge) return 0;

            int baseReward = kind switch
            {
                RoomKind.Treasure => 15,
                RoomKind.Elite => 20,
                RoomKind.Boss => 40,
                _ => 10, // Combat
            };
            return baseReward + depth; // deeper nodes pay a little more regardless of kind
        }

        private static int NodesPerSectorBoundary(int sector) => sector * RunMapGenerator.NodesPerSector;

        private static float KindHealthFactor(RoomKind kind) => kind switch
        {
            RoomKind.Elite => 1.15f,
            RoomKind.Boss => 1.3f,
            _ => 1f,
        };

        private static float KindDamageFactor(RoomKind kind) => kind switch
        {
            RoomKind.Elite => 1.15f,
            RoomKind.Boss => 1.25f,
            _ => 1f,
        };
    }
}

using System;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P4-09 Fracture Pressure ranks. The int value indexes <see cref="MetaSaveData.heatRanks"/>,
    /// so this list is append-only: never reorder or remove an entry.</summary>
    public enum HeatRank
    {
        SharpenedProgram, // enemy damage +15% per rank
        HardenedShells,   // enemy HP +15% per rank
        QuickTelegraph,   // telegraph x0.9 per rank, hard floor 0.6 s
        SwarmProtocol,    // +1 total enemies per Combat room per rank (max concurrent unchanged)
        RustedForge,      // Forge MEND heals 30% instead of 60%
        NoSecondChances,  // revives disabled
        UnforgivingGuard, // non-perfect deflects take 30% chip damage
        EscortedBosses,   // +1 trash add on bosses that call adds
    }

    /// <summary>P4-09: pure heat rules (rank caps, per-rank effects, the Daily Fracture seed and heat set).
    /// The live run's ranks sit in <see cref="RunState.HeatRankOf"/>; the boot-menu pick in
    /// <see cref="MetaProgression.HeatRankOf"/>.</summary>
    public static class Heat
    {
        public const int Count = 8;
        private static readonly int[] MaxRanks = { 3, 3, 2, 2, 1, 1, 1, 1 };

        public const float TelegraphFloor = 0.6f;
        public const float ChipFraction = 0.3f;

        /// <summary>Unlocked after the first win.</summary>
        public static bool Unlocked(int runsWon) => runsWon > 0;

        public static int Max(HeatRank r) => (uint)r < Count ? MaxRanks[(int)r] : 0;

        public static float DamageFactor(int ranks) => 1f + 0.15f * Mathf.Max(0, ranks);
        public static float HealthFactor(int ranks) => 1f + 0.15f * Mathf.Max(0, ranks);

        /// <summary>x0.9 per rank, never below <see cref="TelegraphFloor"/> (a base already under the floor is left alone).</summary>
        public static float Telegraph(float baseTime, int ranks) =>
            ranks <= 0 ? baseTime : Mathf.Max(Mathf.Min(baseTime, TelegraphFloor), baseTime * Mathf.Pow(0.9f, ranks));

        public static float ForgeMendFraction(int rustedRanks) => rustedRanks > 0 ? 0.3f : 0.6f;

        // Daily Fracture. Random seeds come from Random.Range(1, int.MaxValue), so they never set the top
        // bit; a daily seed always does. That is how a resumed run knows it is a daily (no save field needed).
        private const uint DailyBit = 0x80000000u;
        private static readonly int[] DailyRanks = { 1, 1, 1, 1, 0, 0, 0, 0 }; // fixed set: 4 ranks, x1.4 Echoes

        /// <summary>Same UTC date -> same seed, on every device. Murmur3 finalizer so adjacent dates diverge.</summary>
        public static uint DailySeed(DateTime utc)
        {
            uint h = (uint)(utc.Year * 10000 + utc.Month * 100 + utc.Day);
            h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
            return h | DailyBit;
        }

        public static bool IsDailySeed(uint seed) => (seed & DailyBit) != 0;

        public static int DailyRank(HeatRank r) => (uint)r < Count ? DailyRanks[(int)r] : 0;
    }
}

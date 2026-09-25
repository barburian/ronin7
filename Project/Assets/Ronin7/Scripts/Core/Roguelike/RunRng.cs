using System;

namespace Ronin7.Core
{
    /// <summary>
    /// Deterministic, allocation-free PRNG (xorshift32) driving one run's map/spawns/boon offers.
    /// Same seed =&gt; same sequence, forever — this is the property the whole roguelike loop leans
    /// on (byte-identical maps for a given seed, daily-seed sharing, replayable runs). A struct so
    /// callers carry it by value or by <c>ref</c> without a heap allocation (VR frame budget).
    /// </summary>
    public struct RunRng
    {
        private uint state;

        public RunRng(uint seed)
        {
            // xorshift32 is undefined at state 0 (it stays 0 forever), so remap the zero seed to an
            // arbitrary nonzero constant rather than special-casing it in every Next* call.
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>Exposed so a run's RNG can be resumed/serialized (e.g. reroll tokens spent mid-run).</summary>
        public uint State => state;

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>maxExclusive &lt;= 0 =&gt; 0.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>Precondition: <c>maxExclusive - minInclusive</c> must fit in an <c>int</c> without
        /// overflowing (i.e. the range's width, not either endpoint alone, must stay within
        /// <see cref="int.MaxValue"/>). Not reachable from any current caller — every range in this
        /// codebase is small (node counts, weight totals) — so per Karpathy's "no speculative code"
        /// this is documented rather than widened to a <c>long</c> path nothing exercises.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>[0,1). Uses the top 24 bits — matches float mantissa precision for even coverage.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / (1 << 24));
        }

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>Weighted pick. Returns -1 for a null/empty span or when every weight is &lt;= 0.
        /// Precondition: the sum of the positive weights must fit in an <c>int</c> without
        /// overflowing — every caller in this codebase sums a handful of small hand-authored weights
        /// (rarity/room-kind tables), so per Karpathy's "no speculative code" this is documented
        /// rather than widened to a <c>long</c> accumulator nothing exercises.</summary>
        public int PickWeighted(ReadOnlySpan<int> weights)
        {
            if (weights.IsEmpty) return -1;

            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0) total += weights[i];
            }
            if (total <= 0) return -1;

            int roll = NextInt(total);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return weights.Length - 1; // unreachable given the totals above; defensive fallback
        }

        /// <summary>Stable per-node substream, so re-rolling one node's contents never disturbs
        /// another node's. A hash-style mix (not a plain add) so consecutive node indices don't
        /// produce correlated seeds.</summary>
        public static RunRng ForNode(uint runSeed, int nodeIndex)
        {
            uint mixed = runSeed ^ (uint)(nodeIndex * 0x9E3779B1u + 0x85EBCA6Bu);
            return new RunRng(mixed);
        }
    }
}

using System.Collections.Generic;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Picks which Echo callout line to speak for a gameplay event. Pure C# (no UnityEngine) so it is
    /// deterministic and unit-testable: pools are injected already chapter-gated (see
    /// <see cref="EchoLines"/>), so this class only owns pick order and pacing. Round-robins each
    /// pool (never repeats the immediately-previous line in that pool, for pools of 2+) and enforces
    /// one global cooldown shared across every event kind.
    /// </summary>
    public class EchoCalloutSelector
    {
        private readonly IReadOnlyDictionary<string, string[]> pools;
        private readonly float cooldownSeconds;
        private readonly Dictionary<string, int> lastIndex = new Dictionary<string, int>();
        private float nextAllowedSeconds = float.NegativeInfinity;

        public EchoCalloutSelector(IReadOnlyDictionary<string, string[]> pools, float cooldownSeconds = 20f)
        {
            this.pools = pools;
            this.cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// Attempt to pick a line for <paramref name="eventKind"/> at <paramref name="nowSeconds"/>.
        /// Returns false (and leaves <paramref name="line"/> null) if the cooldown hasn't elapsed, the
        /// kind is unknown, or its pool is empty.
        /// </summary>
        public bool TryPick(string eventKind, float nowSeconds, out string line)
        {
            line = null;

            if (string.IsNullOrEmpty(eventKind) || nowSeconds < nextAllowedSeconds)
            {
                return false;
            }

            if (pools == null || !pools.TryGetValue(eventKind, out string[] pool) || pool == null || pool.Length == 0)
            {
                return false;
            }

            int previous = lastIndex.TryGetValue(eventKind, out int prev) ? prev : -1;
            int next = previous + 1;
            if (next >= pool.Length)
            {
                next = 0;
            }

            line = pool[next];
            lastIndex[eventKind] = next;
            nextAllowedSeconds = nowSeconds + cooldownSeconds;
            return true;
        }
    }
}

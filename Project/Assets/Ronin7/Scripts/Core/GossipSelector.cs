using System;

namespace Ronin7.Core
{
    /// <summary>
    /// Pure selection logic for an NPC's gossip/dialogue variant list, gated by campaign story flags.
    /// Mirrors <c>Ronin7.World.Story.BriefingSelector</c>'s "first satisfied condition wins" idiom,
    /// generalized to an arbitrary list instead of BriefingSelector's fixed 8-slot ladder.
    /// </summary>
    public static class GossipSelector
    {
        /// <summary>
        /// Returns the index of the first variant whose required flag is null/empty (a universal
        /// fallback — always satisfied) or for which <paramref name="hasFlag"/> returns true.
        /// Returns -1 if no variant qualifies (including a null/empty <paramref name="requiredFlags"/>).
        /// </summary>
        public static int SelectIndex(string[] requiredFlags, Func<string, bool> hasFlag)
        {
            if (requiredFlags == null) return -1;

            for (int i = 0; i < requiredFlags.Length; i++)
            {
                string flag = requiredFlags[i];
                if (string.IsNullOrEmpty(flag) || (hasFlag != null && hasFlag(flag)))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

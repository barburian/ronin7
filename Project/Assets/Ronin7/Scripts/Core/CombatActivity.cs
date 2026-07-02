namespace Ronin7.Core
{
    /// <summary>
    /// Tracks count of on-foot enemies currently in aggro FSM state.
    /// Used by UI to determine "in combat" without scene scans.
    /// </summary>
    public static class CombatActivity
    {
        private static int onFootAggro;

        /// <summary>
        /// True if any on-foot enemies are in aggro state.
        /// </summary>
        public static bool OnFootAggro => onFootAggro > 0;

        /// <summary>
        /// Current count of on-foot enemies in aggro state.
        /// </summary>
        public static int OnFootAggroCount => onFootAggro;

        /// <summary>
        /// Increment the on-foot aggro count (called when an enemy enters aggro FSM).
        /// </summary>
        public static void Add() => onFootAggro++;

        /// <summary>
        /// Decrement the on-foot aggro count (called when an enemy leaves aggro FSM).
        /// Clamped to never go negative (prevents double-Remove races from despawns).
        /// </summary>
        public static void Remove()
        {
            if (onFootAggro > 0)
            {
                onFootAggro--;
            }
        }

        /// <summary>
        /// Reset aggro count to zero (called when returning to space or starting a new zone).
        /// </summary>
        public static void Reset() => onFootAggro = 0;
    }
}

using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Session-lifetime combat/campaign counters (mirrors <see cref="CampaignState"/>'s static-class
    /// pattern). Gameplay code reports into this via the Record* methods; <see cref="SaveData"/> /
    /// <see cref="CampaignState"/> carry the counters across saves (see <see cref="LoadFrom"/>).
    /// </summary>
    public static class CampaignStats
    {
        public static int EnemiesDefeated { get; private set; }
        public static int PerfectParries { get; private set; }
        public static int PostureBreaks { get; private set; }
        public static int BestCombo { get; private set; }

        public static void RecordEnemyDefeated() => EnemiesDefeated++;
        public static void RecordPerfectParry() => PerfectParries++;
        public static void RecordPostureBreak() => PostureBreaks++;

        /// <summary>Records a combo chain reaching <paramref name="reached"/>; BestCombo only ever climbs.</summary>
        public static void RecordCombo(int reached) => BestCombo = NextBestCombo(BestCombo, reached);

        /// <summary>Pure Max — the highest combo ever reached never decreases. Internal + unit-tested.</summary>
        internal static int NextBestCombo(int current, int reached) => Mathf.Max(current, reached);

        /// <summary>Restores counters from a loaded save file. Internal: a SaveData round-trip detail,
        /// not a gameplay-facing "record" action, so it stays out of the public API.</summary>
        internal static void LoadFrom(int enemiesDefeated, int perfectParries, int postureBreaks, int bestCombo)
        {
            EnemiesDefeated = enemiesDefeated;
            PerfectParries = perfectParries;
            PostureBreaks = postureBreaks;
            BestCombo = bestCombo;
        }

        public static void Reset()
        {
            EnemiesDefeated = 0;
            PerfectParries = 0;
            PostureBreaks = 0;
            BestCombo = 0;
        }

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak stats from a previous run into the next (mirrors EventBus's reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Reset();
    }
}

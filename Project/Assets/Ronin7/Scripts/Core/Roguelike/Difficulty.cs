using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>R13 difficulty modes. The int value is persisted (<see cref="MetaSaveData.difficulty"/>,
    /// <see cref="RunSnapshot.difficulty"/>), so this list is append-only.</summary>
    public enum DifficultyMode { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>R13: pure difficulty rules. Stacks on top of <see cref="Heat"/> (A15.19): enemy damage multiplies,
    /// telegraph is applied before Heat's (so Heat's 0.6 s floor still wins), room size is added to Swarm.
    /// The live run's mode is <see cref="RunState.Difficulty"/>; the select-screen pick is <see cref="MetaProgression.SelectedDifficulty"/>.</summary>
    public static class Difficulty
    {
        public const int Count = 3;
        public const int Unset = -1;

        public static float DamageFactor(DifficultyMode m) => m == DifficultyMode.Easy ? 0.75f : 1f;

        /// <summary>Easy +20%, Hard -15%. Hard never goes below <see cref="Heat.TelegraphFloor"/> (a base already under it is left alone).</summary>
        public static float Telegraph(float baseTime, DifficultyMode m)
        {
            if (m == DifficultyMode.Easy) return baseTime * 1.2f;
            if (m == DifficultyMode.Hard) return Mathf.Max(Mathf.Min(baseTime, Heat.TelegraphFloor), baseTime * 0.85f);
            return baseTime;
        }

        /// <summary>Combat-room enemy total change: Easy -1, Hard +1.</summary>
        public static int EnemyDelta(DifficultyMode m) => m == DifficultyMode.Easy ? -1 : m == DifficultyMode.Hard ? 1 : 0;

        /// <summary>A15.12: the Whip unlock and BEST hits count on Normal and up only.</summary>
        public static bool CountsForWhip(DifficultyMode m) => m != DifficultyMode.Easy;

        /// <summary>A save with no recorded mode: a new player (no runs) gets Easy, anyone with runs keeps Normal.</summary>
        public static DifficultyMode DefaultFor(int runsCompleted) => runsCompleted > 0 ? DifficultyMode.Normal : DifficultyMode.Easy;

        public static DifficultyMode From(int value) => (DifficultyMode)Mathf.Clamp(value, 0, Count - 1);

        public static string Label(DifficultyMode m) => m == DifficultyMode.Easy ? "EASY" : m == DifficultyMode.Hard ? "HARD" : "NORMAL";

        public static string Blurb(DifficultyMode m) =>
            m == DifficultyMode.Easy ? "-25% ENEMY DAMAGE, SLOWER TELLS, FEWER FOES (NO WHIP)"
            : m == DifficultyMode.Hard ? "MORE FOES, FASTER TELLS" : "THE INTENDED FIGHT";
    }
}

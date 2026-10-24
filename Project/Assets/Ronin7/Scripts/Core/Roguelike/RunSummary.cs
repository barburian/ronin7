using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// A snapshot of the run that just finished, kept so the main menu can report it AFTER
    /// <see cref="RunState.End"/> has wiped the live run state.
    ///
    /// This exists because <see cref="RunState"/> is cleared on the way out (ClearRun zeroes depth,
    /// echoes and boons), and <see cref="MetaProgression"/> only keeps lifetime totals — neither can
    /// answer "how did THAT run go?" once the run is over. <see cref="RunDirector"/> captures here
    /// before anything is cleared; the summary panel consumes it on arrival at the menu.
    ///
    /// Static rather than serialized: it is deliberately session-only. A summary is a "what just
    /// happened" screen, not save data — <see cref="MetaProgression"/> owns anything durable.
    /// </summary>
    public static class RunSummary
    {
        /// <summary>True once a run has finished and the result has not yet been consumed.</summary>
        public static bool HasResult { get; private set; }

        /// <summary>Zero-based index of the deepest node reached. Use <see cref="RoomsCleared"/> for display.</summary>
        public static int Depth { get; private set; }

        /// <summary>Whether the run was won (final node cleared) rather than ended by permadeath.</summary>
        public static bool Won { get; private set; }

        public static int EnemiesKilled { get; private set; }
        public static int BoonsHeld { get; private set; }
        public static int EchoesEarned { get; private set; }

        /// <summary>P6-03: an assist was on during the run. Echoes bank normally (D-06); this only labels the result.</summary>
        public static bool Assisted { get; private set; }

        /// <summary>P6-04: total Heat ranks on the run, and whether it was the player's first-ever run (picks Kessler's line).</summary>
        public static int Heat { get; private set; }
        public static bool FirstRun { get; private set; }

        public const string AssistedLabel = "ASSISTED RUN";

        /// <summary>P6-03: win/lose accent without a red/green pair: blue vs amber (distinct for all common colour-blindness).</summary>
        public static Color OutcomeAccent(bool won) => won ? new Color(0.4f, 0.75f, 1f) : new Color(1f, 0.7f, 0.25f);

        /// <summary>Records a finished run. Called by RunDirector.EndRun BEFORE RunState.End() runs.</summary>
        public static void Capture(int depth, bool won, int enemiesKilled, int boonsHeld, int echoesEarned, bool assisted = false, int heat = 0, bool firstRun = false)
        {
            Heat = Mathf.Max(0, heat);
            FirstRun = firstRun;
            Assisted = assisted;
            Depth = Mathf.Max(0, depth);
            Won = won;
            EnemiesKilled = Mathf.Max(0, enemiesKilled);
            BoonsHeld = Mathf.Max(0, boonsHeld);
            EchoesEarned = Mathf.Max(0, echoesEarned);
            HasResult = true;
        }

        /// <summary>Drops the pending result once the menu has shown it, so re-entering the menu for
        /// any other reason (Return to Menu, game over) does not resurrect a stale summary.</summary>
        public static void Clear()
        {
            HasResult = false;
            Depth = 0;
            Won = false;
            EnemiesKilled = 0;
            BoonsHeld = 0;
            EchoesEarned = 0;
            Assisted = false;
            Heat = 0;
            FirstRun = false;
        }

        // ---- Pure formatters. Public, not internal: the summary panel that consumes them lives in
        // Ronin7.Flow, and this project's InternalsVisibleTo only ever grants Ronin7.Tests.EditMode,
        // never another runtime assembly (see the note on Combat/ParryTiming.cs). ----

        /// <summary>Rooms actually cleared: the room you died in does not count, so a loss on the
        /// first node reads "0" (depth is the 0-based index of the death room). A win reads the full <see cref="RunMapGenerator.TotalNodes"/>.</summary>
        public static int RoomsClearedFor(int depth, bool won) =>
            won ? RunMapGenerator.TotalNodes : Mathf.Clamp(depth, 0, RunMapGenerator.TotalNodes);

        /// <summary>The headline. Deliberately unambiguous — there was previously no way to tell a
        /// win from a loss after the fact.</summary>
        public static string FormatOutcome(bool won) => won ? "RUN COMPLETE" : "RUN LOST";

        /// <summary>Sub-headline naming how the run ended, so "complete" can't be misread.</summary>
        public static string FormatOutcomeDetail(bool won, int depth) =>
            won
                ? $"The Fracture is closed — all {RunMapGenerator.TotalNodes} rooms cleared."
                : $"You fell in room {Mathf.Clamp(depth + 1, 1, RunMapGenerator.TotalNodes)} of {RunMapGenerator.TotalNodes}.";

        public static string FormatDepth(int depth, bool won) =>
            $"{RoomsClearedFor(depth, won)} / {RunMapGenerator.TotalNodes}";
    }
}

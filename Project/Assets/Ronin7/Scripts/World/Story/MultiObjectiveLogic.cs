namespace Ronin7.World.Story
{
    /// <summary>
    /// Pure any-order N-objective completion tracker for Chapter 6's three-tower kill-list (the
    /// masters can be taken in any order). Mirrors <see cref="Ronin7.World.HeatLogic"/>: no UnityEngine
    /// dependency, so it is deterministic and unit-testable in isolation from the MonoBehaviour
    /// that drives it (<see cref="MultiObjectiveGate"/>).
    /// </summary>
    public class MultiObjectiveLogic
    {
        private readonly bool[] complete;

        /// <summary>How many objectives are still outstanding.</summary>
        public int RemainingCount { get; private set; }

        /// <summary>True once every objective has been completed.</summary>
        public bool AllComplete => RemainingCount == 0;

        public MultiObjectiveLogic(int count)
        {
            if (count < 0) count = 0;
            complete = new bool[count];
            RemainingCount = count;
        }

        /// <summary>
        /// Marks objective <paramref name="index"/> complete. Idempotent: completing an
        /// already-complete index is a no-op. Out-of-range indices are ignored. Returns true only on
        /// the call that completes the LAST outstanding objective (i.e. exactly once per instance,
        /// regardless of completion order).
        /// </summary>
        public bool Complete(int index)
        {
            if (index < 0 || index >= complete.Length) return false;
            if (complete[index]) return false;

            complete[index] = true;
            RemainingCount--;
            return RemainingCount == 0;
        }
    }
}

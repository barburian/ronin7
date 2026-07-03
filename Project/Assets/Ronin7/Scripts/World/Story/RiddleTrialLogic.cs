namespace Ronin7.World.Story
{
    /// <summary>Outcome of a single <see cref="RiddleTrialLogic.Submit"/> call.</summary>
    public enum RiddleTrialResult
    {
        /// <summary>The correct option was submitted; the trial is now passed.</summary>
        Correct,
        /// <summary>An in-range but incorrect option was submitted; the buried should rise.</summary>
        Wrong,
        /// <summary>The trial was already passed; the submission had no effect.</summary>
        AlreadyPassed,
        /// <summary>The submitted index is outside the option range; no state changed.</summary>
        OutOfRange,
    }

    /// <summary>
    /// Pure decision logic for a Chapter 8 "Silent Garden" riddle: the Mourners pose a
    /// multiple-choice trial with exactly one true answer. No UnityEngine dependency, mirrors
    /// <see cref="MultiObjectiveLogic"/> / <c>Ronin7.World.HeatLogic</c> /
    /// <c>Ronin7.Player.WeakpointSightState</c> so it is deterministic and unit-testable apart from
    /// the MonoBehaviour that drives it (<see cref="RiddleTrial"/>).
    /// </summary>
    public class RiddleTrialLogic
    {
        private readonly int optionCount;
        private readonly int correctIndex;

        /// <summary>True once the trial has been solved.</summary>
        public bool Passed { get; private set; }

        /// <summary>How many in-range wrong answers have been submitted so far.</summary>
        public int WrongAttemptCount { get; private set; }

        public RiddleTrialLogic(int optionCount, int correctIndex)
        {
            this.optionCount = optionCount < 0 ? 0 : optionCount;
            this.correctIndex = correctIndex;
        }

        /// <summary>
        /// Submits an answer index. Once <see cref="Passed"/>, further submissions are ignored
        /// (idempotent — the correct answer can't re-trigger the mission advance, and a wrong answer
        /// after passing can't re-summon the buried). An index outside [0, optionCount) is rejected
        /// without touching state. The configured correct index passes the trial; any other in-range
        /// index is wrong and asks the caller to spawn guardians.
        /// </summary>
        public RiddleTrialResult Submit(int index)
        {
            if (Passed) return RiddleTrialResult.AlreadyPassed;
            if (index < 0 || index >= optionCount) return RiddleTrialResult.OutOfRange;

            if (index == correctIndex)
            {
                Passed = true;
                return RiddleTrialResult.Correct;
            }

            WrongAttemptCount++;
            return RiddleTrialResult.Wrong;
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Chapter 8 "Silent Garden" riddle trial: a set of worldspace answer points (greybox
    /// headstones/floor pads), one of which is the true answer. Which pad the player is standing
    /// nearest is polled each frame off <c>Camera.main</c> — the same distance check
    /// <see cref="ProximityZoneExit"/> and <c>MissionDirector</c>'s ReachTrigger step already use, so
    /// this needs no new interaction system. Stepping onto a new pad submits it to a
    /// <see cref="RiddleTrialLogic"/>: the correct pad fires <see cref="onRightAnswer"/> (the builder
    /// wires this to <c>MissionDirector.AdvanceFromPrompt</c>, the same "gate a null-promptObject
    /// Prompt step" idiom Ch4's DuelYield and Ch6's MultiObjectiveGate use); any other pad fires
    /// <see cref="onWrongAnswer"/> (wired to an <see cref="EnemyWaveSpawner"/>'s <c>Begin()</c> — the
    /// dead rising to answer for him, Ch6's <see cref="ActivationRelay"/> pattern). Stepping off a pad
    /// and back onto it resubmits it, so a failed attempt can be retried once its guardians are down.
    ///
    /// SCOPE NOTE: the source design's "disturbance loop" (a wrong answer's guardian wave respawning
    /// on every retry, the puzzle visibly resetting) is a production-note-level seed design, not
    /// mechanized here — <see cref="EnemyWaveSpawner.Begin"/> is itself idempotent (a spawner only
    /// ever runs its waves once), so <see cref="onWrongAnswer"/> spawns the guardians on the first
    /// wrong answer and is a no-op on repeats. Flagged as a deliberate simplification, mirroring
    /// Chapter7's trust-test scope cut.
    /// </summary>
    public class RiddleTrial : MonoBehaviour
    {
        [Tooltip("One answer point per option, in index order. correctAnswerIndex below picks which one is true.")]
        [SerializeField] private Transform[] answerPoints;
        [SerializeField] private int correctAnswerIndex;
        [Tooltip("How close (meters, horizontal) the head must be to a pad to submit it as an answer.")]
        [SerializeField] private float answerRadius = 1.5f;

        public UnityEvent onRightAnswer = new UnityEvent();
        public UnityEvent onWrongAnswer = new UnityEvent();

        private RiddleTrialLogic logic;
        private int currentZone = -1; // index of the pad currently occupied; -1 = none

        /// <summary>True once the trial has been solved.</summary>
        public bool Passed => logic != null && logic.Passed;

        private void Awake()
        {
            logic = new RiddleTrialLogic(answerPoints != null ? answerPoints.Length : 0, correctAnswerIndex);
        }

        private void Update()
        {
            if (logic == null || logic.Passed || answerPoints == null || Camera.main == null) return;

            int zone = FindOccupiedZone();
            if (zone == currentZone) return;
            currentZone = zone;
            if (zone >= 0) Submit(zone);
        }

        private int FindOccupiedZone()
        {
            Vector3 headPos = Camera.main.transform.position;
            for (int i = 0; i < answerPoints.Length; i++)
            {
                if (answerPoints[i] == null) continue;
                Vector3 to = answerPoints[i].position - headPos;
                to.y = 0f;
                if (to.magnitude <= answerRadius) return i;
            }
            return -1;
        }

        /// <summary>Submits an answer index directly — used by the pad-walk poll above, and available
        /// to any other input surface a chapter builder wants to wire to the same trial.</summary>
        public void Submit(int index)
        {
            if (logic == null) return;
            switch (logic.Submit(index))
            {
                case RiddleTrialResult.Correct:
                    onRightAnswer?.Invoke();
                    break;
                case RiddleTrialResult.Wrong:
                    onWrongAnswer?.Invoke();
                    break;
            }
        }
    }
}

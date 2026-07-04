using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Hub dojo practice drill: score a fixed time window against a training dummy and track a
    /// per-drill best score (see <see cref="DrillBestScores"/>). <see cref="BeginDrill"/> is a
    /// console-button entry point (mirrors <see cref="Ronin7.Flow.MissionLauncher.LaunchNext"/>).
    ///
    /// Counts only impacts/perfect parries/posture breaks landed on <see cref="dummyHealth"/>, via
    /// GameObject-filtered EventBus subscriptions (<see cref="SwordImpact.Victim"/>,
    /// <see cref="PerfectParry.Attacker"/>, <see cref="PostureBroken.Entity"/> are all the struck
    /// entity's own GameObject at the publish site), then scores with <see cref="DrillScoring"/>.
    /// </summary>
    public class DojoDrillController : MonoBehaviour
    {
        [SerializeField] private Health dummyHealth;
        [SerializeField] private float drillSeconds = 20f;
        [Tooltip("Optional wrist/panel readout for the end-of-drill score.")]
        [SerializeField] private TextMesh statusText;
        [Tooltip("Save-file key for this drill's best score (see DrillBestScores).")]
        [SerializeField] private string drillId = "hub_dojo";

        private bool running;
        private float timeRemaining;
        private int impacts;
        private int perfectParries;
        private int postureBreaks;
        private string lastDisplayed;

        /// <summary>Console-button entry point. Restarts cleanly (discarding the in-flight run,
        /// without scoring it) if a drill is already running.</summary>
        public void BeginDrill()
        {
            if (running) EndDrill(recordScore: false);

            impacts = 0;
            perfectParries = 0;
            postureBreaks = 0;
            timeRemaining = drillSeconds;
            running = true;

            if (dummyHealth != null) dummyHealth.ResetHealth();

            EventBus.Subscribe<SwordImpact>(OnSwordImpact);
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
        }

        private void Update()
        {
            if (!running) return;

            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f) EndDrill(recordScore: true);
        }

        private void OnDisable()
        {
            if (running) Unsubscribe();
            running = false;
        }

        private void OnSwordImpact(SwordImpact e)
        {
            if (dummyHealth != null && e.Victim == dummyHealth.gameObject) impacts++;
        }

        private void OnPerfectParry(PerfectParry e)
        {
            if (dummyHealth != null && e.Attacker == dummyHealth.gameObject) perfectParries++;
        }

        private void OnPostureBroken(PostureBroken e)
        {
            if (dummyHealth != null && e.Entity == dummyHealth.gameObject) postureBreaks++;
        }

        private void EndDrill(bool recordScore)
        {
            Unsubscribe();
            running = false;

            if (!recordScore) return;

            int score = DrillScoring.Score(impacts, perfectParries, postureBreaks);
            int best = DrillBestScores.RecordIfBetter(drillId, score);
            SetStatus($"SCORE {score} · BEST {best}");
        }

        private void Unsubscribe()
        {
            EventBus.Unsubscribe<SwordImpact>(OnSwordImpact);
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
        }

        private void SetStatus(string text)
        {
            if (text == lastDisplayed) return;
            lastDisplayed = text;
            if (statusText != null) statusText.text = text;
        }
    }
}

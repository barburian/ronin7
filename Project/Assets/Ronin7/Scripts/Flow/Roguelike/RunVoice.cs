using System.Collections;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// P6-04: speaks the <see cref="KesslerRunLines"/> bank: Kessler on the menu after the run summary, and the boss
    /// intro / phase lines in the arena. Added to the persistent RunDirector object at runtime (no scene patch).
    /// Clips load from Resources/RunVoice; a missing clip still shows the subtitle (silent). Each line publishes
    /// <see cref="SubtitleLine"/>, which SubtitleDisplay renders (the same event DialoguePlayer publishes).
    /// </summary>
    public class RunVoice : MonoBehaviour
    {
        private const string Folder = "RunVoice/";
        private const float SummaryDelay = 1.2f;

        private AudioSource source;
        private int sector;
        private readonly int[] lastVariant = { -1, -1, -1, -1, -1, -1 };

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RunNodeEntered>(OnNodeEntered);
            EventBus.Subscribe<BossPhaseChanged>(OnBossPhase);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RunNodeEntered>(OnNodeEntered);
            EventBus.Unsubscribe<BossPhaseChanged>(OnBossPhase);
        }

        private void OnNodeEntered(RunNodeEntered e)
        {
            sector = e.Sector;
            if (e.Kind == RoomKind.Boss) Speak(KesslerRunLines.BossIntro(e.Sector));
        }

        private void OnBossPhase(BossPhaseChanged e)
        {
            var line = KesslerRunLines.BossPhase(sector, e.Phase);
            if (line.HasValue) Speak(line.Value);
        }

        /// <summary>Kessler's line for this run result, after a short beat so the summary lands first.</summary>
        public void PlayOutcome(KesslerOutcome outcome) => StartCoroutine(OutcomeRoutine(outcome));

        private IEnumerator OutcomeRoutine(KesslerOutcome outcome)
        {
            yield return new WaitForSecondsRealtime(SummaryDelay);
            var variants = KesslerRunLines.Variants(outcome);
            int i = KesslerRunLines.NextVariant(variants.Length, lastVariant[(int)outcome], Random.Range(0, 1000));
            lastVariant[(int)outcome] = i;
            Speak(variants[i]);
        }

        private void Speak(RunVoiceLine line)
        {
            var clip = Resources.Load<AudioClip>(Folder + line.Clip);
            source.Stop();
            float seconds = Mathf.Max(3f, line.Text.Length / 14f);
            if (clip != null)
            {
                source.clip = clip;
                source.Play();
                seconds = clip.length + 0.4f;
            }
            EventBus.Publish(new SubtitleLine(line.Speaker, line.Text, seconds));
        }
    }
}

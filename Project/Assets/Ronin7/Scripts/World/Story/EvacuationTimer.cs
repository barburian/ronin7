using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Countdown timer that displays "PURGE IN MM:SS" on a TextMesh and plays an optional looping alarm.
    /// On enable, starts counting down from the configured duration. At zero, freezes at "PURGE IN 00:00"
    /// and stops counting.
    /// </summary>
    public class EvacuationTimer : MonoBehaviour
    {
        [SerializeField] private float duration = 90f;
        [SerializeField] private TextMesh textMesh;
        [SerializeField] private AudioSource alarmSource;

        private float remainingTime;
        private bool active;
        private int lastDisplayedTotalSeconds = -1;

        private void OnEnable()
        {
            Begin(duration);
        }

        /// <summary>Reset and start the countdown from the given duration (in seconds).</summary>
        public void Begin(float seconds)
        {
            remainingTime = Mathf.Max(0f, seconds);
            active = true;
            if (alarmSource != null && !alarmSource.isPlaying)
            {
                alarmSource.loop = true;
                alarmSource.Play();
            }
        }

        private void Update()
        {
            if (!active) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                active = false;
                if (alarmSource != null)
                    alarmSource.Stop();
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (textMesh == null) return;

            int totalSeconds = Mathf.CeilToInt(remainingTime);
            if (totalSeconds == lastDisplayedTotalSeconds) return;
            lastDisplayedTotalSeconds = totalSeconds;

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            textMesh.text = string.Format("PURGE IN {0:D2}:{1:D2}", minutes, seconds);
        }
    }
}

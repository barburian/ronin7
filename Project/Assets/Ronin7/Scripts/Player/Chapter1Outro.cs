using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Player
{
    /// <summary>
    /// Chapter-end cinematic. When this GameObject is enabled (by a <c>MissionDirector</c> Trigger
    /// step), it fires <see cref="onActivated"/> — wired at build time to a <c>CampaignFlagSetter</c>
    /// so the <c>ch1_complete</c> flag persists — reveals the "CHAPTER 1 COMPLETE" canvas, and fades
    /// the screen to black via <see cref="ScreenFader"/> for the hand-off to the next scene.
    ///
    /// The UnityEvent indirection keeps this in <c>Ronin7.Player</c> (where ScreenFader lives) while
    /// still reusing the existing <c>CampaignFlagSetter</c> in <c>Ronin7.World</c> — the editor wires
    /// the two together without either assembly referencing the other.
    /// </summary>
    public class Chapter1Outro : MonoBehaviour
    {
        [SerializeField] private GameObject completeCanvas;
        [Tooltip("Seconds to hold the completion canvas before the fade begins.")]
        [SerializeField] private float fadeDelay = 1.5f;
        [SerializeField] private float fadeDuration = 2f;
        [SerializeField] private UnityEvent onActivated = new UnityEvent();

        /// <summary>Build-time hook: wire CampaignFlagSetter.SetFlags here so it runs on activation.</summary>
        public UnityEvent OnActivated => onActivated;

        private void OnEnable()
        {
            onActivated?.Invoke();
            if (completeCanvas != null) completeCanvas.SetActive(true);
            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            yield return new WaitForSeconds(fadeDelay);
            var fader = ScreenFader.Ensure();
            if (fader != null) yield return fader.FadeOut(fadeDuration);
        }
    }
}

using System.Collections;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Player
{
    /// <summary>
    /// Chapter-end cinematic. When this GameObject is enabled (by a <c>MissionDirector</c> Trigger
    /// step), it fires <see cref="onActivated"/> — wired at build time to a <c>CampaignFlagSetter</c>
    /// so the chapter's completion flag persists — reveals the "CHAPTER COMPLETE" canvas, and fades
    /// the screen to black via <see cref="ScreenFader"/> for the hand-off to the next scene.
    ///
    /// Chapters after Ch1 double as the persistent hub (see <c>HubStateController</c>), so after the
    /// fade this also publishes <see cref="ZoneCompleted"/> — exactly what <c>StoryTransition.ReturnToSpace</c>
    /// does — when <see cref="publishZoneCompleted"/> is set, so GameFlowManager's normal
    /// mission-complete handling still fires without a separate transition box.
    ///
    /// The UnityEvent indirection keeps this in <c>Ronin7.Player</c> (where ScreenFader lives) while
    /// still reusing the existing <c>CampaignFlagSetter</c> in <c>Ronin7.World</c> — the editor wires
    /// the two together without either assembly referencing the other.
    /// </summary>
    public class ChapterOutro : MonoBehaviour
    {
        [SerializeField] private GameObject completeCanvas;
        [Tooltip("Seconds to hold the completion canvas before the fade begins.")]
        [SerializeField] private float fadeDelay = 1.5f;
        [SerializeField] private float fadeDuration = 2f;
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private bool publishZoneCompleted = true;

        /// <summary>Build-time hook: wire CampaignFlagSetter.SetFlags here so it runs on activation.</summary>
        public UnityEvent OnActivated => onActivated;

        /// <summary>Pure decision seam for the post-fade ZoneCompleted publish, so it's unit-testable
        /// without instantiating the MonoBehaviour.</summary>
        public static bool ShouldPublish(bool flagEnabled) => flagEnabled;

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
            if (ShouldPublish(publishZoneCompleted)) EventBus.Publish(new ZoneCompleted());
        }
    }
}

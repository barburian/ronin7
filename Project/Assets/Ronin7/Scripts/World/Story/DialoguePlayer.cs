using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Plays a sequence of dialogue lines as a white head-locked subtitle (no background) that sits below
    /// the player's eye line and follows the camera. Optional per-line audio plays alongside the text.
    /// Each line auto-advances after its duration plus a short beat; pressing Y (left controller) skips early.
    /// </summary>
    public class DialoguePlayer : MonoBehaviour
    {
        [SerializeField] private DialogueLine[] lines;
        [SerializeField] private TextMesh textMesh;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool playOnStart;

        [Header("Head-locked subtitle")]
        [Tooltip("Meters the subtitle floats in front of the eyes.")]
        [SerializeField] private float followDistance = 1.5f;
        [Tooltip("Meters below the eye line (negative = lower in view).")]
        [SerializeField] private float followVerticalOffset = -0.55f;
        [Tooltip("Catch-up speed as the head turns (higher = snappier).")]
        [SerializeField] private float followLerp = 12f;
        [Tooltip("Max characters per subtitle line before wrapping to the next line.")]
        [SerializeField] private int maxCharsPerLine = 36;
        [Tooltip("Extra beat (seconds) added to each line's duration before it auto-advances.")]
        [SerializeField] private float autoAdvanceBeat = 0.45f;

        [Tooltip("Press-to-advance action (Left Hand/Talk = Y). If unassigned, lines auto-advance on their timer.")]
        [SerializeField] private InputActionReference advanceAction;

        // Minimum time a line is shown before a Y press can advance it. Debounces the same press that
        // started the conversation (or advanced the previous line) so one tap never skips two lines.
        private const float MinLineDwell = 0.25f;

        public event System.Action Finished;

        /// <summary>True while a line is currently on screen — i.e. this dialogue is actively playing.</summary>
        public bool IsPlaying => isShowing;

        private InputAction advanceResolved;
        private InputAction ownedAdvance; // created when no asset reference resolves (mirrors SettingsMenuToggle)
        private Coroutine playCoroutine;
        private bool isShowing;  // a line is currently on screen (drives the head-follow in LateUpdate)
        private bool justShown;  // snap the subtitle into place on the first frame instead of sliding in
        private bool started;    // play-once guard: a dialogue never replays

        private void Awake()
        {
            // No background: keep the dark panel hidden and render the text plain white.
            if (textMesh != null)
            {
                textMesh.color = Color.white;
            }
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        public void Play()
        {
            if (started) return;
            started = true;

            if (advanceResolved == null)
            {
                advanceResolved = advanceAction != null
                    ? InputResolver.Resolve(advanceAction, "Left Hand", "Talk", "Dialogue")
                    : null;
                if (advanceResolved == null)
                {
                    // Reference missing/unresolved: bind the Y button (left secondary) directly so every
                    // dialogue is Y-to-advance regardless of editor-time wiring.
                    ownedAdvance = new InputAction("DialogueAdvance", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
                    ownedAdvance.Enable();
                    advanceResolved = ownedAdvance;
                }
            }

            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
            }
            playCoroutine = StartCoroutine(PlayRoutine());
        }

        private void OnDisable()
        {
            ownedAdvance?.Disable();
            ownedAdvance?.Dispose();
            ownedAdvance = null;
            advanceResolved = null;
        }

        private IEnumerator PlayRoutine()
        {
            if (lines == null || lines.Length == 0)
            {
                FinishDialogue();
                yield break;
            }

            foreach (var line in lines)
            {
                if (!isShowing)
                {
                    isShowing = true;
                    justShown = true;  // snap to the head this frame, then ease for the rest
                }

                if (textMesh != null)
                {
                    string composed = string.IsNullOrEmpty(line.speaker)
                        ? line.text
                        : $"{line.speaker}: {line.text}";
                    textMesh.text = WrapText(composed);
                }

                if (line.clip != null && audioSource != null)
                {
                    audioSource.PlayOneShot(line.clip);
                }

                // Calculate effective display duration: max of line.seconds or (clip.length + grace period).
                float displayDuration = line.seconds;
                if (line.clip != null)
                {
                    displayDuration = Mathf.Max(line.seconds, line.clip.length + 0.4f);
                }

                yield return WaitForAdvance(displayDuration);
            }

            FinishDialogue();
        }

        /// <summary>
        /// Hold the current line for its duration plus a short beat, then auto-advance — a line never
        /// waits indefinitely. Y is an optional early-skip: after a short minimum dwell (which debounces
        /// the press that opened the line), a fresh Y press advances immediately and stops any playing
        /// audio. Falls back to a plain timed wait of the same total when no advance action is wired.
        ///
        /// Ch9 overdrive audit: every wait here runs on unscaled time (WaitForSecondsRealtime,
        /// Time.unscaledDeltaTime) so subtitles hold their authored real-world duration instead of
        /// lingering ~3x longer while Time.timeScale is slowed by the Overdrive burst.
        /// </summary>
        private IEnumerator WaitForAdvance(float lineSeconds)
        {
            float total = lineSeconds + autoAdvanceBeat;

            if (advanceResolved == null)
            {
                yield return new WaitForSecondsRealtime(total);
                yield break;
            }

            // Debounce the press that started this line; counts toward the total so the full wait is honored.
            float dwell = Mathf.Min(total, MinLineDwell);
            yield return new WaitForSecondsRealtime(dwell);

            float elapsed = dwell;
            while (elapsed < total)
            {
                // WasPressedThisFrame fires only on a fresh down-transition, so the press that started
                // this line (an earlier frame) does not count — a new Y press skips early.
                if (advanceResolved.WasPressedThisFrame())
                {
                    // Stop audio if the player skipped (pressed Y before the line naturally ended).
                    if (audioSource != null && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Greedily word-wrap text to <see cref="maxCharsPerLine"/> characters per line. Existing newlines
        /// split paragraphs that wrap independently; a word longer than the limit stays on its own line.
        /// </summary>
        private string WrapText(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var sb = new System.Text.StringBuilder();
            string[] paragraphs = s.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                {
                    sb.Append('\n');
                }

                string[] words = paragraphs[p].Split(' ');
                int lineLen = 0;
                bool firstWordOnLine = true;
                foreach (var word in words)
                {
                    if (word.Length == 0)
                    {
                        continue;
                    }
                    int addition = firstWordOnLine ? word.Length : word.Length + 1; // +1 for joining space
                    if (!firstWordOnLine && lineLen + addition > maxCharsPerLine)
                    {
                        sb.Append('\n');
                        lineLen = 0;
                        firstWordOnLine = true;
                        addition = word.Length;
                    }

                    if (!firstWordOnLine)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(word);
                    lineLen += addition;
                    firstWordOnLine = false;
                }
            }

            return sb.ToString();
        }

        private void FinishDialogue()
        {
            isShowing = false;
            // Blank the text so the last line doesn't linger once the conversation ends.
            if (textMesh != null)
            {
                textMesh.text = "";
            }
            Finished?.Invoke();
        }

        // Head-locked subtitle: while a line is showing, place the text below the eye line in front of
        // the camera and ease it toward that pose each frame so it follows the head without snapping.
        private void LateUpdate()
        {
            if (!isShowing || !billboardToCamera || textMesh == null)
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var camT = cam.transform;
            Vector3 targetPos = camT.TransformPoint(new Vector3(0f, followVerticalOffset, followDistance));
            // Face the text back toward the head (TextMesh reads correctly when its +Z points away from the camera).
            Quaternion targetRot = Quaternion.LookRotation(targetPos - camT.position, camT.up);

            var t = textMesh.transform;
            // Ch9 overdrive audit: unscaled so the subtitle keeps pace with real head motion during a
            // time-slowed burst instead of visibly lagging/detaching from the view.
            float k = justShown ? 1f : 1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime);
            justShown = false;
            t.position = Vector3.Lerp(t.position, targetPos, k);
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
        }
    }
}

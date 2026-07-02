using UnityEngine;
using UnityEngine.InputSystem;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// A command-room computer the player hacks by holding B (right controller) while standing near
    /// it. On completion it projects a hologram and plays a reveal dialogue, then fires
    /// <see cref="Hacked"/> once that dialogue finishes. Driven by a mission Hack step:
    /// <see cref="Activate"/> arms it, and <see cref="Hacked"/> advances the mission.
    ///
    /// Note: the Hack action shares the right-hand secondaryButton binding with the flight-only
    /// "Recenter" action. That is safe here because CockpitRecenter does not exist in the on-foot
    /// hideout scene, so nothing else consumes the press.
    /// </summary>
    public class HackTerminal : MonoBehaviour
    {
        [Tooltip("Right Hand/Hack action (B button).")]
        [SerializeField] private InputActionReference hackAction;
        [Tooltip("How close (metres, horizontal) the head must be to hack.")]
        [SerializeField] private float hackRadius = 2.5f;
        [Tooltip("Seconds B must be held to complete the hack.")]
        [SerializeField] private float hackDuration = 1.5f;
        [Tooltip("Hologram projection, hidden until the hack completes.")]
        [SerializeField] private GameObject hologramRoot;
        [Tooltip("World-space prompt shown when in range.")]
        [SerializeField] private TextMesh promptText;
        [Tooltip("Reveal dialogue played when the hack completes.")]
        [SerializeField] private DialoguePlayer revealDialogue;
        [SerializeField] private AudioClip hackLoopClip;
        [SerializeField] private AudioClip hackSuccessClip;
        [SerializeField] private AudioSource audioSource;

        public event System.Action Hacked;

        private InputAction hackResolved;
        private InputAction owned; // created when no asset reference resolves (mirrors SettingsMenuToggle)
        private bool armed;
        private bool done;
        private float holdTimer;
        private bool isHolding;

        private void OnEnable()
        {
            hackResolved = hackAction != null ? InputResolver.Resolve(hackAction, "Right Hand", "Hack", "Hack") : null;
            if (hackResolved == null)
            {
                // Reference missing/unresolved: bind the B button (right secondary) directly.
                owned = new InputAction("Hack", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
                owned.Enable();
                hackResolved = owned;
            }
            if (hologramRoot != null) hologramRoot.SetActive(false);
            ShowPrompt(null);
        }

        private void OnDisable()
        {
            if (revealDialogue != null) revealDialogue.Finished -= OnRevealFinished;
            owned?.Disable();
            owned?.Dispose();
            owned = null;
            hackResolved = null;
        }

        /// <summary>Arm the terminal so it begins listening for the player to approach and hold B.</summary>
        public void Activate()
        {
            armed = true;
        }

        private void Update()
        {
            if (!armed || done) return;

            bool inRange = InRange();
            if (!inRange)
            {
                holdTimer = 0f;
                if (isHolding && audioSource != null)
                {
                    audioSource.Stop();
                    isHolding = false;
                }
                ShowPrompt(null);
                return;
            }

            bool pressed = hackResolved != null && hackResolved.IsPressed();
            if (pressed)
            {
                // Start looping audio on first press.
                if (!isHolding && hackLoopClip != null && audioSource != null)
                {
                    audioSource.loop = true;
                    audioSource.clip = hackLoopClip;
                    audioSource.Play();
                    isHolding = true;
                }

                holdTimer += Time.deltaTime;
                if (holdTimer >= hackDuration)
                {
                    CompleteHack();
                    return;
                }
                float pct = Mathf.RoundToInt(Mathf.Clamp01(holdTimer / hackDuration) * 100f);
                ShowPrompt($"Hacking… {pct}%");
            }
            else
            {
                // Stop looping audio on release.
                if (isHolding && audioSource != null)
                {
                    audioSource.Stop();
                    isHolding = false;
                }
                holdTimer = 0f;
                ShowPrompt("Hold B to hack");
            }
        }

        private void CompleteHack()
        {
            done = true;
            ShowPrompt(null);
            if (hologramRoot != null) hologramRoot.SetActive(true);

            // Stop the loop and play success sound if available.
            if (isHolding && audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
                isHolding = false;
            }
            if (hackSuccessClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(hackSuccessClip);
            }

            if (revealDialogue != null)
            {
                revealDialogue.Finished += OnRevealFinished;
                revealDialogue.Play();
            }
            else
            {
                Hacked?.Invoke();
            }
        }

        private void OnRevealFinished()
        {
            if (revealDialogue != null) revealDialogue.Finished -= OnRevealFinished;
            Hacked?.Invoke();
        }

        private bool InRange()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            return toCam.magnitude <= hackRadius;
        }

        private void ShowPrompt(string msg)
        {
            if (promptText == null) return;
            promptText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (string.IsNullOrEmpty(msg)) return;

            promptText.text = msg;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = promptText.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    promptText.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Ronin7.Core;
using Ronin7.World;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Lets the on-foot player talk to nearby <see cref="StoryNpc"/>s by pressing Y (left controller).
    /// Shows a "Press Y to talk" pop-up when near an un-talked NPC (mirrors the landing prompt) and
    /// starts that NPC's dialogue on Y. While a dialogue is playing, further Y presses advance its
    /// lines (handled by <see cref="DialoguePlayer"/>) rather than opening a second conversation.
    /// </summary>
    public class TalkInteractor : MonoBehaviour
    {
        [Tooltip("Left Hand/Talk action (Y button).")]
        [SerializeField] private InputActionReference talkAction;
        [Tooltip("How close (metres, horizontal) the head must be to an NPC to talk.")]
        [SerializeField] private float talkRadius = 2.5f;
        [Tooltip("World-space prompt shown when an un-talked NPC is in range.")]
        [SerializeField] private TextMesh promptText;
        [SerializeField] private string promptMessage = "Press Y to talk";

        private InputAction talkResolved;
        private InputAction owned; // created when no asset reference resolves (mirrors SettingsMenuToggle)
        private bool dialogueActive;
        private DialoguePlayer activeDialogue;
        private StoryNpcWander activeNpcWander;

        private void OnEnable()
        {
            talkResolved = talkAction != null ? InputResolver.Resolve(talkAction, "Left Hand", "Talk", "Talk") : null;
            if (talkResolved == null)
            {
                // Reference missing/unresolved: bind the Y button (left secondary) directly.
                owned = new InputAction("Talk", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
                owned.Enable();
                talkResolved = owned;
            }
            ShowPrompt(null);
        }

        private void OnDisable()
        {
            if (activeDialogue != null) activeDialogue.Finished -= OnDialogueFinished;
            owned?.Disable();
            owned?.Dispose();
            owned = null;
            talkResolved = null;
        }

        private void Update()
        {
            if (dialogueActive)
            {
                ShowPrompt(null);
                return;
            }

            StoryNpc target = NearestTalkable();
            ShowPrompt(target);

            if (target != null && talkResolved != null && talkResolved.WasPressedThisFrame())
            {
                StartConversation(target);
            }
        }

        private void StartConversation(StoryNpc npc)
        {
            npc.MarkTalked();
            ShowPrompt(null);

            activeDialogue = npc.Dialogue;
            if (activeDialogue == null) return;

            // Lazily attach the talk-nod animator so it samples this NPC's dialogue audio while it plays.
            NpcTalkAnimator.EnsureOn(npc.gameObject);

            // Pause the NPC's wandering if it has a StoryNpcWander component.
            activeNpcWander = npc.GetComponent<StoryNpcWander>();
            if (activeNpcWander != null)
            {
                activeNpcWander.Paused = true;
            }

            dialogueActive = true;
            activeDialogue.Finished += OnDialogueFinished;
            activeDialogue.Play();
        }

        private void OnDialogueFinished()
        {
            if (activeDialogue != null) activeDialogue.Finished -= OnDialogueFinished;
            activeDialogue = null;
            dialogueActive = false;

            // Resume the NPC's wandering.
            if (activeNpcWander != null)
            {
                activeNpcWander.Paused = false;
                activeNpcWander = null;
            }
        }

        private StoryNpc NearestTalkable()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            StoryNpc best = null;
            float bestDist = float.MaxValue;
            var npcs = StoryNpc.Active;
            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (npc == null || npc.Talked || npc.Remote || npc.Dialogue == null) continue;

                Vector3 toNpc = npc.transform.position - cam.transform.position;
                toNpc.y = 0f; // horizontal distance, like ProximityDoor
                float d = toNpc.magnitude;
                if (d <= talkRadius && d < bestDist)
                {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        // Show the prompt floating above the target NPC's head, billboarded to the camera. A null
        // target hides it.
        private void ShowPrompt(StoryNpc target)
        {
            if (promptText == null) return;
            promptText.gameObject.SetActive(target != null);
            if (target == null) return;

            promptText.text = promptMessage;
            promptText.transform.position = target.transform.position + Vector3.up * 2.2f;

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

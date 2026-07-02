using UnityEngine;
using UnityEngine.InputSystem;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Advances a <see cref="MissionDirector"/> Prompt step when the player presses the bound action
    /// (Y / Left-Hand Talk by default). It lives on the Prompt step's prompt object, which the
    /// MissionDirector activates only while that Prompt step is current — so input is polled only then,
    /// and goes quiet again once <see cref="MissionDirector.AdvanceFromPrompt"/> hides the prompt.
    /// Used for the cinematic "release the grapple" beat in Chapter 1.
    /// </summary>
    public class PromptInputAdvancer : MonoBehaviour
    {
        [SerializeField] private MissionDirector mission;
        [SerializeField] private InputActionReference advanceAction;

        private InputAction resolved;
        private InputAction owned; // created when no asset reference resolves (mirrors DialoguePlayer)

        private void OnEnable()
        {
            resolved = advanceAction != null
                ? InputResolver.Resolve(advanceAction, "Left Hand", "Talk", "Prompt")
                : null;
            if (resolved == null)
            {
                // Reference missing/unresolved (e.g. batchmode-built scene): bind the Y button directly
                // so the prompt is always advanceable regardless of editor-time wiring.
                owned = new InputAction("PromptAdvance", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
                owned.Enable();
                resolved = owned;
            }
        }

        private void OnDisable()
        {
            owned?.Disable();
            owned?.Dispose();
            owned = null;
            resolved = null;
        }

        private void Update()
        {
            if (mission != null && resolved != null && resolved.WasPressedThisFrame())
            {
                mission.AdvanceFromPrompt();
            }
        }
    }
}

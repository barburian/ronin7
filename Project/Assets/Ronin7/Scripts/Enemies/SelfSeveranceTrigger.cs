using UnityEngine;
using UnityEngine.Events;
using Ronin7.Core;

namespace Ronin7.Enemies
{
    /// <summary>
    /// EP16 self-severance beat: the player (Cipher) cuts his own spinal locator chip to sever
    /// the Dominion's tracking beacon. This component tracks the one-time scripted action and
    /// sets a story flag when activated, allowing dependent dialogue/encounters to key off the
    /// severance event.
    /// </summary>
    public class SelfSeveranceTrigger : MonoBehaviour
    {
        // Public so scene builders can wire persistent listeners (DialoguePlayer.Play,
        // MissionDirector.AdvanceFromPrompt) via UnityEventTools at build time.
        public UnityEvent onSevered = new UnityEvent();

        [SerializeField] private string severedFlag = "ep16_locator_severed";

        private bool severed;

        /// <summary>True if the locator has been severed.</summary>
        public bool HasSevered => severed;

        /// <summary>
        /// Sever the locator chip: idempotent, sets the campaign flag, and invokes onSevered.
        /// Safe to call multiple times; only fires once.
        /// </summary>
        public void Sever()
        {
            if (severed) return;

            severed = true;
            CampaignState.SetFlag(severedFlag);
            onSevered?.Invoke();
        }
    }
}

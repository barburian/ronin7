using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Enemies
{
    /// <summary>
    /// EP21 dream reckoning beat: when the player acknowledges the dream (rather than fighting),
    /// all linked DreamPhantoms dissolve and the story flag is set. This replaces combat with
    /// narrative closure, allowing the player to accept the illusion rather than resist it.
    /// </summary>
    public class DreamReckoningTrigger : MonoBehaviour
    {
        [SerializeField] private List<DreamPhantom> phantoms = new();
        [Tooltip("Optional campaign flag to set when acknowledged (leave empty to skip).")]
        [SerializeField] private string acknowledgedFlag = "";

        public UnityEvent onAcknowledged = new UnityEvent();

        private bool acknowledged;

        /// <summary>True if the dream has been acknowledged.</summary>
        public bool HasAcknowledged => acknowledged;

        /// <summary>Public so builders can AddListener after a runtime AddComponent.</summary>
        public UnityEvent OnAcknowledged => onAcknowledged;

        /// <summary>
        /// Acknowledge the dream: idempotent. Dissolves all linked phantoms, sets the
        /// campaign flag (if configured), and invokes onAcknowledged. Safe to call multiple times.
        /// </summary>
        public void Acknowledge()
        {
            if (acknowledged) return;

            acknowledged = true;

            // Dissolve all phantoms
            if (phantoms != null)
            {
                foreach (var phantom in phantoms)
                {
                    if (phantom != null)
                    {
                        phantom.Dissolve();
                    }
                }
            }

            // Set the campaign flag if configured
            if (!string.IsNullOrEmpty(acknowledgedFlag))
            {
                CampaignState.SetFlag(acknowledgedFlag);
            }

            onAcknowledged?.Invoke();
        }
    }
}

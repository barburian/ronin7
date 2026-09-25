using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Runtime hook for the post-run summary shown on the main menu. Mirrors
    /// <see cref="BoonOfferPanel"/>: the hierarchy is built by <see cref="RunSummaryPanelFactory"/>
    /// and this component only surfaces the dismiss button as an event.
    /// </summary>
    public class RunSummaryPanel : MonoBehaviour
    {
        // Assigned by the factory AFTER AddComponent — see WireButtons for why this cannot be Awake.
        public Button dismissButton;

        public event Action Dismissed;

        /// <summary>
        /// Wire the dismiss button. The builder MUST call this after assigning <see cref="dismissButton"/>.
        /// It cannot live in <c>Awake</c>: <c>AddComponent</c> on an active GameObject runs <c>Awake</c>
        /// synchronously, inside the AddComponent call, so an Awake-based wiring sees a null button and
        /// silently attaches nothing — the exact defect that made the boon panel unclickable.
        /// Idempotent.
        /// </summary>
        public void WireButtons()
        {
            if (dismissButton == null) return;
            dismissButton.onClick.RemoveAllListeners();
            dismissButton.onClick.AddListener(() => Dismissed?.Invoke());
        }
    }
}

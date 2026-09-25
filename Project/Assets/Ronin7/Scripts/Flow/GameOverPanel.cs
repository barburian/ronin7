using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Worldspace "Game Over" panel runtime hook. The visual hierarchy is built by the flow
    /// manager at runtime; this component just surfaces the "Return to Menu" button as an
    /// event the coroutine can race against its auto-dismiss timer.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        // Public so the runtime builder can assign it after AddComponent. The assignment necessarily
        // lands AFTER Awake has already run — see WireButtons.
        public Button returnButton;

        public event Action OnDismissed;

        /// <summary>
        /// Wire the return button. The builder MUST call this after assigning <see cref="returnButton"/>.
        /// This cannot be <c>Awake</c>: <c>AddComponent</c> on an active GameObject runs <c>Awake</c>
        /// synchronously, inside the AddComponent call, so the old Awake-based wiring observed a null
        /// button and attached no listener at all — RETURN TO MENU was dead, and only the auto-dismiss
        /// timer got the player out. Same defect that made the boon panel unclickable. Idempotent.
        /// </summary>
        public void WireButtons()
        {
            if (returnButton == null) return;
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(() => OnDismissed?.Invoke());
        }
    }
}

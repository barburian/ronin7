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
        // Public so the runtime builder in GameFlowManager can assign it after AddComponent.
        // (Awake is deferred until end-of-frame after AddComponent, so the assignment lands first.)
        public Button returnButton;

        public event Action OnDismissed;

        private void Awake()
        {
            if (returnButton != null)
                returnButton.onClick.AddListener(() => OnDismissed?.Invoke());
        }
    }
}

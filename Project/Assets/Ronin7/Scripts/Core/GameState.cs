using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Single source of truth for the current game context. Persists across scene
    /// loads and broadcasts <see cref="GameModeChanged"/> through the <see cref="EventBus"/>.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [SerializeField] private GameMode startMode = GameMode.Boot;

        public GameMode Mode { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Mode = startMode;
        }

        public void SetMode(GameMode mode)
        {
            if (mode == Mode) return;
            var previous = Mode;
            Mode = mode;
            EventBus.Publish(new GameModeChanged(previous, mode));
        }
    }
}

namespace Ronin7.Core
{
    /// <summary>High-level game contexts the player can be in. Drives the flow manager.</summary>
    public enum GameMode
    {
        Boot,
        GalaxyMap,
        SpaceFlight,
        Landing,
        OnFoot
    }

    /// <summary>Published whenever the active <see cref="GameMode"/> changes.</summary>
    public readonly struct GameModeChanged
    {
        public readonly GameMode Previous;
        public readonly GameMode Current;
        public GameModeChanged(GameMode previous, GameMode current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Published from flight when the player commits to landing on a planet. The flow manager
    /// listens, covers the transition with a comfort fade, and loads the on-foot zone scene.
    /// </summary>
    public readonly struct LandingRequested
    {
        /// <summary>Scene to load for the landing destination (empty = use the flow default).</summary>
        public readonly string DestinationScene;
        public LandingRequested(string destinationScene) { DestinationScene = destinationScene; }
    }
}

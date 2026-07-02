namespace Ronin7.Core
{
    /// <summary>
    /// Published when a zone's objective is met and the player extracts. Lives in Core (not
    /// World) so the flow manager and audio director can subscribe without an assembly
    /// dependency on World — the EventBus is the only coupling.
    /// </summary>
    public readonly struct ZoneCompleted { }

    /// <summary>Published whenever zone objective progress changes (for HUD/audio).</summary>
    public readonly struct ObjectiveUpdated
    {
        public readonly int EnemiesLeft;
        public readonly int RelicsLeft;
        public ObjectiveUpdated(int enemies, int relics) { EnemiesLeft = enemies; RelicsLeft = relics; }
    }
}

namespace Ronin7.Core
{
    /// <summary>The kind of encounter a <see cref="RunNode"/> holds. Drives arena assembly (Module 3),
    /// boon offer rarity (Module 2) and difficulty scaling (<see cref="RunScaling"/>).</summary>
    public enum RoomKind
    {
        Combat,
        Elite,
        Treasure,
        Forge,
        Boss
    }
}

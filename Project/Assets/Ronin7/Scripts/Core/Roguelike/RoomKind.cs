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
        Boss,
        Shop,  // P2-04: spend this run's Shards (P4-11)
        Event  // P2-04: a 2-option RunEventDefinition
    }

    /// <summary>P2-04: player-facing name, one-line hint, colour and 3-letter HUD code per kind —
    /// shared by the exit gates (World), the CHOOSE YOUR PATH panel and the wrist HUD (Flow).</summary>
    public static class RoomKindText
    {
        public static string Name(RoomKind k) => k switch
        {
            RoomKind.Combat => "COMBAT",
            RoomKind.Elite => "ELITE",
            RoomKind.Treasure => "TREASURE",
            RoomKind.Forge => "FORGE",
            RoomKind.Boss => "BOSS",
            RoomKind.Shop => "SHOP",
            RoomKind.Event => "EVENT",
            _ => k.ToString().ToUpperInvariant(),
        };

        public static string Hint(RoomKind k) => k switch
        {
            RoomKind.Combat => "a fight, then a boon",
            RoomKind.Elite => "rarer boons",
            RoomKind.Treasure => "a free boon",
            RoomKind.Forge => "heal and +1 reroll",
            RoomKind.Boss => "the sector guardian",
            RoomKind.Shop => "spend run Shards",
            RoomKind.Event => "an unknown choice",
            _ => string.Empty,
        };

        public static string Code(RoomKind k) => k switch
        {
            RoomKind.Combat => "CBT",
            RoomKind.Elite => "ELT",
            RoomKind.Treasure => "TRS",
            RoomKind.Forge => "FRG",
            RoomKind.Boss => "BOS",
            RoomKind.Shop => "SHP",
            RoomKind.Event => "EVT",
            _ => "???",
        };

        /// <summary>Kind colour; the kind name is always shown too, so nothing rides on colour alone.</summary>
        public static UnityEngine.Color Color(RoomKind k) => k switch
        {
            RoomKind.Combat => new UnityEngine.Color32(0xFF, 0x9A, 0x3D, 255),
            RoomKind.Elite => new UnityEngine.Color32(0xFF, 0x3D, 0x5A, 255),
            RoomKind.Treasure => new UnityEngine.Color32(0xFF, 0xD2, 0x4A, 255),
            RoomKind.Forge => new UnityEngine.Color32(0x3F, 0xA9, 0xFF, 255),
            RoomKind.Boss => new UnityEngine.Color32(0xFF, 0x20, 0x20, 255),
            RoomKind.Shop => new UnityEngine.Color32(0x3D, 0xDC, 0x97, 255),
            RoomKind.Event => new UnityEngine.Color32(0xB6, 0x6B, 0xFF, 255),
            _ => UnityEngine.Color.white,
        };
    }
}

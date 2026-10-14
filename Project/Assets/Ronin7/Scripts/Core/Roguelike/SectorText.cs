using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P2-02: player-facing sector names and colours for the sector card. The colours mirror
    /// the biome accents RoguelikeDataBuilder authors into the ArenaRoomLibrary (rust / program / garden);
    /// Flow cannot see World, so they are repeated here — keep the two in step.</summary>
    public static class SectorText
    {
        public static string Name(int sector) => (((sector % 3) + 3) % 3) switch
        {
            0 => "THE RUST",
            1 => "THE PROGRAM",
            _ => "THE GARDEN",
        };

        /// <summary>"SECTOR 2 — THE PROGRAM" for the 0-based sector 1.</summary>
        public static string Title(int sector) => "SECTOR " + (sector + 1) + " — " + Name(sector);

        public static Color Color(int sector) => (((sector % 3) + 3) % 3) switch
        {
            0 => new Color(0.95f, 0.45f, 0.15f),
            1 => new Color(0.25f, 0.75f, 0.95f),
            _ => new Color(0.55f, 0.90f, 0.55f),
        };
    }
}

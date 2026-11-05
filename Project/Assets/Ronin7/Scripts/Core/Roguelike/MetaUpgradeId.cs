namespace Ronin7.Core
{
    /// <summary>Stable meta-upgrade id strings persisted via <see cref="MetaProgression"/>. Each
    /// upgrade's per-level effect and max level are documented here; <see cref="MetaProgression.MaxLevel"/>
    /// is the single source of truth callers/tests should read instead of hardcoding these numbers.</summary>
    public static class MetaUpgradeId
    {
        public const string StartingHealth = "meta_starting_health";   // +10 max HP per level, max 5
        public const string StartingDamage = "meta_starting_damage";   // +5% blade damage per level, max 5
        public const string StartingBoon = "meta_starting_boon";       // start with N tier-1 boons, max 3
        public const string RerollTokens = "meta_reroll_tokens";       // +1 starting reroll, max 3
        public const string WeaponArtSlot = "meta_weapon_art_slot";    // P4-10 Shrine: 250 Echoes, max 1
        public const string OffhandArtSlot = "meta_offhand_art_slot";  // P4-10 Shrine: 250 Echoes, max 1
    }

    /// <summary>P4-08 stable loadout ids (persisted in meta.json; must match <c>WeaponDefinition.id</c>; never rename).</summary>
    public static class WeaponId
    {
        public const string Katana = "katana";                // always unlocked
        public const string Nodachi = "nodachi";              // P4-10 Shrine: 150 Echoes
        public const string Tanto = "tanto";                  // P4-10 Shrine: 150 Echoes
        public const string TwinWakizashi = "twin_wakizashi"; // P4-10 Shrine: 300 Echoes; (Double Katana)
        public const string PolearmShield = "polearm_shield"; // A15.1: Polearm.asset, P9-02c
        public const string Whip = "whip";                    // A15.1: no definition until P9-02d

        /// <summary>A15.1 roster: the only loadouts a run can start with. Nodachi/Tanto left; owned ones stay readable.</summary>
        public static bool OnRoster(string id) =>
            id == Katana || id == TwinWakizashi || id == PolearmShield || id == Whip;

        /// <summary>A15.1 fallback: an off-roster id (old save) plays as the Katana.</summary>
        public static string Effective(string id) => OnRoster(id) ? id : Katana;

        /// <summary>A15.10: the Whip run has no parry, so ParryGate reads Neutral.</summary>
        public static bool ParryAvailable(string id) => id != Whip;

        /// <summary>A15.18: Weapon Arts (Charged Slash, Blade Throw) fire only on Katana and Double Katana.</summary>
        public static bool AllowsWeaponArt(string id) { id = Effective(id); return id == Katana || id == TwinWakizashi; }
    }
}

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
    }
}

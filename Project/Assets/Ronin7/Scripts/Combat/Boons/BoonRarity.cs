namespace Ronin7.Combat
{
    /// <summary>
    /// Boon tier. Drives both the offer weighting (<see cref="BoonOfferPicker"/>) and, by convention,
    /// what kind of effect a boon carries: Common/Rare are stat boons, Epic grants one of the five
    /// already-built player abilities (see <c>BoonEffectKind.GrantAbility</c>).
    /// </summary>
    public enum BoonRarity
    {
        Common,
        Rare,
        Epic
    }
}

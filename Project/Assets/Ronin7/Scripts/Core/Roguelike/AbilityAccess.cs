namespace Ronin7.Core
{
    /// <summary>
    /// A7.7: whether the player currently has an ability, from either source that can grant one — a
    /// permanent campaign unlock (<see cref="CampaignState.HasAbility"/>) or a this-run-only boon grant
    /// (<see cref="RunState.HasGrantedAbility"/>). Epic roguelike boons must never mutate the campaign
    /// save (a run pick would otherwise permanently unlock a story ability, and a player who already
    /// finished the campaign would find the rarest boon tier a dead pick), so
    /// <c>RunDirector.GrantBoon</c> calls <see cref="RunState.GrantAbility"/> instead of
    /// <see cref="CampaignState.UnlockAbility"/>, and the five ability controllers' <c>Awake</c> gates
    /// call this instead of <c>CampaignState.HasAbility</c> directly.
    /// </summary>
    public static class AbilityAccess
    {
        public static bool Has(string id) => CampaignState.HasAbility(id) || RunState.HasGrantedAbility(id);
    }
}

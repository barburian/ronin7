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
        public static bool Has(string id) =>
            RunState.InRun ? RunState.HasGrantedAbility(id) : CampaignState.HasAbility(id);

        /// <summary>P7-02 (BUG-49): the ability controllers' <c>Awake</c> gate. Leaves <paramref name="b"/> on
        /// when <paramref name="id"/> is held, else turns it off until a run grant of that id turns it back
        /// on, so a boon picked mid-room works at once instead of from the next room's Awake.</summary>
        public static void EnableWhenGranted(UnityEngine.Behaviour b, string id)
        {
            if (Has(id)) return;
            b.enabled = false;
            System.Action<string> onGrant = null;
            onGrant = granted =>
            {
                if (b != null && granted != id) return;
                RunState.AbilityGranted -= onGrant; // ponytail: a destroyed controller's handler only drops on the next grant; fine at <=8 per room
                if (b != null) b.enabled = true;
            };
            RunState.AbilityGranted += onGrant;
        }
    }
}

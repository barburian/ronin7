namespace Ronin7.Core
{
    /// <summary>Stable boon id strings, tracked by <see cref="RunState"/> for the run in progress.
    /// Tier 3 ids grant the already-built abilities (see <see cref="AbilityId"/>) via
    /// <see cref="RunState.GrantAbility"/> (roguelike runs, via <see cref="AbilityAccess"/>) or
    /// <see cref="CampaignState.UnlockAbility"/> (story campaign).</summary>
    public static class BoonId
    {
        // Tier 1 (Common)
        public const string KeenEdge = "boon_keen_edge";
        public const string Whetstone = "boon_whetstone";
        public const string Ironskin = "boon_ironskin";
        public const string SecondSkin = "boon_second_skin";
        public const string Bloodletter = "boon_bloodletter";
        public const string FlowInitiate = "boon_flow_initiate";
        public const string ComboInitiate = "boon_combo_initiate";
        // Tier 2 (Rare)
        public const string SunderBeat = "boon_sunder_beat";
        public const string FollowThrough = "boon_follow_through";
        public const string SecondWind = "boon_second_wind";
        // Tier 3 (Epic) — grant the already-built abilities
        public const string GrantWeakpointSight = "boon_grant_weakpoint_sight";
        public const string GrantOverdrive = "boon_grant_overdrive";
        public const string GrantPhaseStep = "boon_grant_phase_step";
        public const string GrantUnbroken = "boon_grant_unbroken";
        public const string GrantMirror = "boon_grant_mirror";
    }
}

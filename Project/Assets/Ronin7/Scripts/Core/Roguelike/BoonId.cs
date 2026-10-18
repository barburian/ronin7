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

        // P4-05 archetype boons (30). Kensei (parry)
        public const string IronWall = "boon_iron_wall";
        public const string LongStagger = "boon_long_stagger";
        public const string Riposte = "boon_riposte";
        public const string Counterweight = "boon_counterweight";
        public const string Zanshin = "boon_zanshin";
        public const string RecklessFlow = "boon_reckless_flow";
        public const string ThunderRiposte = "boon_thunder_riposte";
        // Dancer (flow); ComboInitiate above is now "Reversal"
        public const string MomentumKeeper = "boon_momentum_keeper";
        public const string Whirlwind = "boon_whirlwind";
        public const string CrimsonTide = "boon_crimson_tide";
        // Hemorrhage (bleed pack)
        public const string SerratedEdge = "boon_serrated_edge";
        public const string OpenVeins = "boon_open_veins";
        public const string Exsanguinate = "boon_exsanguinate";
        // Storm (storm pack)
        public const string StaticEdge = "boon_static_edge";
        public const string Conductor = "boon_conductor";
        public const string Thunderclap = "boon_thunderclap";
        // Executioner (executioner pack)
        public const string QuickDraw = "boon_quick_draw";
        public const string HeavyHand = "boon_heavy_hand";
        public const string Guillotine = "boon_guillotine";
        public const string Headhunter = "boon_headhunter";
        // Shadow (ability)
        public const string Quickened = "boon_quickened";
        public const string Afterimage = "boon_afterimage";
        public const string Overclock = "boon_overclock";
        public const string EchoTwin = "boon_echo_twin";
        public const string GhostStep = "boon_ghost_step";
        // Bulwark
        public const string LacqueredArmor = "boon_lacquered_armor";
        public const string LastStand = "boon_last_stand";
        // Curses
        public const string GlassEdge = "boon_glass_edge";
        public const string BloodPrice = "boon_blood_price";
        public const string HollowOath = "boon_hollow_oath";
    }

    /// <summary>P4-05 archetype packs. Locked until bought in the P4-10 Shrine
    /// (<see cref="MetaProgression.IsPackUnlocked"/>); the offer picker never offers a locked pack's boons.</summary>
    public static class BoonPackId
    {
        public const string Bleed = "pack_bleed";
        public const string Storm = "pack_storm";
        public const string Executioner = "pack_executioner";
    }
}

namespace Ronin7.Core
{
    /// <summary>
    /// Stable ability id strings persisted via <see cref="CampaignState"/>. Each is a freed
    /// blade-shadow ability from the ability chain (see 00_STORY_BIBLE.md section 8):
    /// WeakpointSight (Ch7), Overdrive (Ch9), PhaseStep (Ch10), Unbroken (Ch11), Mirror (Ch12).
    /// </summary>
    public static class AbilityId
    {
        public const string WeakpointSight = "weakpoint_sight";
        public const string Overdrive = "overdrive";
        public const string PhaseStep = "phase_step";
        public const string Unbroken = "unbroken";
        public const string Mirror = "mirror";

        // P4-04 roguelike passives, granted by boons (P4-05) through RunState.GrantAbility.
        public const string Riposte = "riposte";
        public const string QuickDraw = "quick_draw";
        public const string Zanshin = "zanshin";
    }
}

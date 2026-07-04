namespace Ronin7.Core
{
    /// <summary>
    /// Pure scoring formula for dojo practice drills (see <see cref="Ronin7.Player.DojoDrillController"/>).
    /// Posture breaks are weighted highest (a forced stagger is the hardest thing to land), perfect
    /// parries next, plain impacts lowest. <paramref name="flawless"/> adds a flat "Flawless Run" bonus
    /// (the player took no damage during the drill) — a flat add rather than a multiplier so a flawless
    /// run always outscores an identical non-flawless one, even a scoreless one.
    /// </summary>
    public static class DrillScoring
    {
        public static int Score(int impacts, int perfectParries, int postureBreaks, bool flawless) =>
            impacts * 10 + perfectParries * 25 + postureBreaks * 100 + (flawless ? 200 : 0);
    }
}

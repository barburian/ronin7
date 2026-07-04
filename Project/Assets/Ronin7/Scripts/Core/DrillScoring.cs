namespace Ronin7.Core
{
    /// <summary>
    /// Pure scoring formula for dojo practice drills (see <see cref="Ronin7.Player.DojoDrillController"/>).
    /// Posture breaks are weighted highest (a forced stagger is the hardest thing to land), perfect
    /// parries next, plain impacts lowest.
    /// </summary>
    public static class DrillScoring
    {
        public static int Score(int impacts, int perfectParries, int postureBreaks) =>
            impacts * 10 + perfectParries * 25 + postureBreaks * 100;
    }
}

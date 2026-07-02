namespace Ronin7.Core
{
    /// <summary>
    /// Determine whether saving is allowed in the current game state.
    /// </summary>
    public static class SaveGate
    {
        /// <summary>
        /// True only if saving is allowed: calm space flight or calm on-foot play.
        /// (Other modes like Boot, GalaxyMap, Landing prevent saves.)
        /// </summary>
        public static bool CanSave(GameMode mode, bool spaceHostiles, bool onFootAggro)
        {
            if (mode == GameMode.SpaceFlight && !spaceHostiles)
            {
                return true;
            }

            if (mode == GameMode.OnFoot && !onFootAggro)
            {
                return true;
            }

            return false;
        }
    }
}

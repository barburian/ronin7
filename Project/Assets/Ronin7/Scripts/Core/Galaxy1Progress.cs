namespace Ronin7.Core
{
    /// <summary>
    /// Static in-session progress flags for the Galaxy 1 campaign loop. Scene objects are rebuilt
    /// on every scene load (the flow reloads the space scene after each on-foot zone), so state
    /// that must survive the space → zone → space round-trip lives here instead of on a component.
    /// Reset by the flow manager when a fresh campaign/run starts.
    /// </summary>
    public static class Galaxy1Progress
    {
        /// <summary>
        /// True once the player has landed on (and is therefore departing) the first story planet.
        /// <see cref="Ronin7.Ship.SpaceEncounterManager"/> gates enemy waves on this so the
        /// opening flight to the first planet stays calm.
        /// </summary>
        public static bool FirstPlanetDeparted;

        public static void Reset()
        {
            FirstPlanetDeparted = false;
        }
    }
}

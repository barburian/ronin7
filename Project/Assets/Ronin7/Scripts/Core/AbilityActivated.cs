namespace Ronin7.Core
{
    /// <summary>
    /// Published on the EventBus when a permanent ability (see <see cref="AbilityId"/>) fires — e.g.
    /// weakpoint-sight or Overdrive triggering in combat. The ability MonoBehaviours ship in their
    /// unlock chapters; this event exists now so listeners (e.g. EchoPresence) can wire up early.
    /// </summary>
    public readonly struct AbilityActivated
    {
        public readonly string Id;
        public AbilityActivated(string id) => Id = id;
    }
}

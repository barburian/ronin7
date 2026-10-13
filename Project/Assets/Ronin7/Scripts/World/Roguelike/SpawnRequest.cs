using UnityEngine;

namespace Ronin7.World
{
    /// <summary>Pure instruction from <see cref="WaveComposer"/>: what to spawn, and where.</summary>
    public readonly struct SpawnRequest
    {
        /// <summary>Index into the source table's trash[] (or bosses[] when <see cref="IsBoss"/>).</summary>
        public readonly int EntryIndex;
        public readonly Vector3 Position;
        public readonly bool IsBoss;
        /// <summary>P1-09: the lead enemy of an Elite room (escorts and everything else are false).</summary>
        public readonly bool IsElite;

        public SpawnRequest(int entryIndex, Vector3 position, bool isBoss, bool isElite = false)
        {
            EntryIndex = entryIndex;
            Position = position;
            IsBoss = isBoss;
            IsElite = isElite;
        }
    }
}

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

        public SpawnRequest(int entryIndex, Vector3 position, bool isBoss)
        {
            EntryIndex = entryIndex;
            Position = position;
            IsBoss = isBoss;
        }
    }
}

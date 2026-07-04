using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Marker for an enclosed interior space that <see cref="Ronin7.Editor.Art.ReverbZonePlacer"/>
    /// auto-places an <see cref="AudioReverbZone"/> onto. Purely a shape marker -- no runtime
    /// behavior, unlike <see cref="ZoneBounds"/> (which also clamps the player). Authored as a
    /// local center offset + size so <see cref="WorldBounds"/> can be recomputed after moving or
    /// duplicating the GameObject without re-authoring the footprint.
    /// </summary>
    public class InteriorVolume : MonoBehaviour
    {
        public Vector3 centerOffset = Vector3.zero;
        public Vector3 size = new Vector3(6f, 3f, 6f);

        public Bounds WorldBounds => new Bounds(transform.position + centerOffset, size);
    }
}

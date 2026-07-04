using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Pure classifier: maps a zone's horizontal footprint (given as a <see cref="Bounds"/>) to an
    /// <see cref="AudioReverbPreset"/> plus a sensible min/max falloff distance for an
    /// <see cref="AudioReverbZone"/>. No Unity object lookups, no editor deps — safe to unit test
    /// and safe to call from either runtime or editor code.
    /// </summary>
    public static class ReverbPresetSelector
    {
        // Footprint (longest horizontal side, in meters — 1 unit = 1m) tier boundaries.
        public const float RoomMaxSpan = 6f;      // small enclosed room / closet
        public const float HallwayMaxSpan = 20f;  // medium chamber / corridor
        public const float HangarMaxSpan = 80f;   // large hangar-scale interior
        // Above HangarMaxSpan the zone reads as open/outdoor space — no artificial reverb.

        // An elongated footprint (long side notably longer than the short side) reads
        // acoustically like a corridor even when compact, so it up-tiers Room -> Hallway.
        public const float HallwayAspectRatio = 3f;

        /// <summary>
        /// Classifies a zone's <see cref="Bounds"/> into a reverb preset and the min/max distance
        /// an <see cref="AudioReverbZone"/> placed at its center should use. Only the horizontal
        /// footprint (X/Z) drives the decision — height isn't a signal Unity's AudioReverbZone
        /// considers; it's a spherical falloff from the component's transform position. Returns
        /// <see cref="AudioReverbPreset.Off"/> with zero distances for zones too large to read as
        /// an enclosed space — callers should skip placing a zone in that case.
        /// </summary>
        public static (AudioReverbPreset preset, float minDistance, float maxDistance) Classify(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float longSide = Mathf.Max(size.x, size.z);
            float shortSide = Mathf.Max(Mathf.Min(size.x, size.z), 0.0001f);
            float aspect = longSide / shortSide;

            AudioReverbPreset preset;
            if (longSide > HangarMaxSpan)
                preset = AudioReverbPreset.Off;
            else if (longSide > HallwayMaxSpan)
                preset = AudioReverbPreset.Hangar;
            else if (longSide > RoomMaxSpan || aspect >= HallwayAspectRatio)
                preset = AudioReverbPreset.Hallway;
            else
                preset = AudioReverbPreset.Room;

            if (preset == AudioReverbPreset.Off)
                return (preset, 0f, 0f);

            // maxDistance is roughly half the footprint (the reverb fades out near the zone's
            // edge); minDistance is a fraction of that so there's a falloff band, not a hard cut.
            // Both are clamped so degenerate (near-zero) bounds never produce a zero or inverted pair.
            float maxDistance = Mathf.Max(longSide * 0.5f, 1f);
            float minDistance = Mathf.Max(maxDistance * 0.25f, 0.1f);
            return (preset, minDistance, maxDistance);
        }
    }
}

using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Palette + optional prop set per sector "biome" for the roguelike run arena. Authored data
    /// only — <see cref="ArenaGeometryBuilder"/> reads it to tint the greybox shell and, when
    /// <see cref="Biome.propPrefabs"/> is populated, scatter set dressing.
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin 7/Arena Room Library", fileName = "ArenaRoomLibrary")]
    public class ArenaRoomLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Biome
        {
            public string id; // "rust", "program", "garden"
            public Color floorColor = new Color(0.16f, 0.18f, 0.22f);
            public Color ceilColor = new Color(0.10f, 0.11f, 0.14f);
            public Color accentColor = new Color(0.4f, 0.7f, 1f);

            /// <summary>Optional. Empty/null falls back to a greybox room with no scatter props.</summary>
            public GameObject[] propPrefabs;
        }

        public Biome[] biomes;

        /// <summary>Biome for a run sector (0..2). Wraps when there are fewer biomes than sectors;
        /// returns null when <see cref="biomes"/> is empty so callers fall back to a greybox default.</summary>
        public Biome ForSector(int sector)
        {
            if (biomes == null || biomes.Length == 0) return null;
            int index = ((sector % biomes.Length) + biomes.Length) % biomes.Length;
            return biomes[index];
        }
    }
}

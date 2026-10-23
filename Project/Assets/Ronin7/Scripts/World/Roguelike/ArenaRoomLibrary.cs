using Ronin7.Core;
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

            /// <summary>P5-05 baked room-shell scene names (must be in Build Settings). Empty falls back to
            /// the <see cref="ArenaGeometryBuilder"/> greybox. Footprint is encoded in the name: a shell
            /// authored for a room half-extent of N metres ends in <c>_Nm</c> (e.g. <c>Room_Program_01_8m</c>);
            /// see <see cref="PickRoomScene"/>. Contract for a shell scene: see <c>RunArenaController.LoadShell</c>.</summary>
            public string[] roomScenes = new string[0];

            /// <summary>P5-05 optional skybox for this biome; applied only when non-null.</summary>
            public Material skybox;
        }

        /// <summary>P5-05 pure pick: among scenes whose name ends in <c>_{round(halfExtent)}m</c> (the
        /// room's <c>ArenaHalfExtent</c>), choose one with <paramref name="rng"/>. Null when none match
        /// (the caller then uses the greybox, which fits any half-extent). Draws from the rng only
        /// when at least one scene matches.</summary>
        public static string PickRoomScene(string[] roomScenes, float halfExtent, ref RunRng rng)
        {
            if (roomScenes == null || roomScenes.Length == 0) return null;
            string suffix = "_" + Mathf.RoundToInt(halfExtent) + "m";
            int count = 0;
            foreach (string s in roomScenes)
                if (!string.IsNullOrEmpty(s) && s.EndsWith(suffix, System.StringComparison.Ordinal)) count++;
            if (count == 0) return null;
            int pick = rng.NextInt(count);
            foreach (string s in roomScenes)
                if (!string.IsNullOrEmpty(s) && s.EndsWith(suffix, System.StringComparison.Ordinal) && pick-- == 0) return s;
            return null;
        }

        [Tooltip("Shipped URP material (Simple Lit) for the runtime-built room surfaces. Unity's default " +
                 "primitive material is unavailable in player builds, so without this the arena renders magenta on Quest.")]
        public Material surfaceMaterial;

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

using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Data-driven pool of spawnable enemies for the roguelike run: trash mobs (weighted, gated by
    /// depth/elite) and per-sector bosses. Authored data only — <see cref="WaveComposer"/> reads it
    /// to decide what to spawn for a node; <see cref="RunArenaController"/> instantiates the result.
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin 7/Enemy Spawn Table", fileName = "EnemySpawnTable")]
    public class EnemySpawnTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public GameObject prefab; // combat-ready enemy prefab
            public Ronin7.Enemies.EnemyDefinition definition;
            public int weight = 10;
            public int minDepth = 0;
            public bool eliteOnly;
        }

        [System.Serializable]
        public class BossEntry
        {
            public GameObject prefab;
            public Ronin7.Enemies.EnemyDefinition definition;
            public int sector; // which sector this boss caps
        }

        public Entry[] trash;
        public BossEntry[] bosses;
    }
}

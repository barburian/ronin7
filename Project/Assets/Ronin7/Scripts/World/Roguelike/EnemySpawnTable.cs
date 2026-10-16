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
            [Tooltip("Max copies of this entry in one room (0 = unlimited). Support drone = 1.")]
            public int maxPerRoom;
        }

        [System.Serializable]
        public class BossEntry
        {
            public GameObject prefab;
            public Ronin7.Enemies.EnemyDefinition definition;
            public int sector; // which sector this boss caps
            [Tooltip("P3-07: phases after the first, in order. Each runtime clone gets its share of " +
                     "RunScaling.BossTargetHealth, weighted by every phase's own maxHealth.")]
            public Ronin7.Enemies.EnemyDefinition[] phases;
            [Tooltip("P3-07: trash[] index of the adds called at the first phase change (-1 = none).")]
            public int addEntry = -1;
            public int addCount;
        }

        public Entry[] trash;
        public BossEntry[] bosses;
    }
}

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
            [Tooltip("P8-02 (D-17): weapon composed onto Socket_MainHand at spawn (claws for natural enemies).")]
            public Ronin7.Enemies.EnemyWeaponDefinition mainHand;
            [Tooltip("P8-02: optional off-hand (shield, second dagger) on Socket_OffHand.")]
            public Ronin7.Enemies.EnemyWeaponDefinition offHand;
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

        /// <summary>P8-02b: one rollable weapon loadout (main + optional off-hand).</summary>
        [System.Serializable]
        public struct Kit { public Ronin7.Enemies.EnemyWeaponDefinition main, off; }

        [Tooltip("P8-02b: every armed (non-natural) trash body rolls one of these per spawn. Built by EnemyWeaponBuilder.")]
        public Kit[] kits;

        /// <summary>P8-02b: the loadout for one spawn. Armed bodies roll uniformly from <see cref="kits"/>; natural
        /// bodies (and an empty kit list) keep the entry's own default.</summary>
        public Kit RollKit(Entry e, ref Ronin7.Core.RunRng rng) =>
            e.definition != null && !e.definition.naturalWeapon && kits != null && kits.Length > 0
                ? kits[rng.NextInt(kits.Length)]
                : new Kit { main = e.mainHand, off = e.offHand };

        public Entry[] trash;
        public BossEntry[] bosses;
    }
}

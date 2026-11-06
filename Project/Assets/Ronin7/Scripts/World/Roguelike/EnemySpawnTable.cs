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
            [Tooltip("P8-02c: never rolls; always carries mainHand/offHand (the body's sculpted weapon).")]
            public bool fixedKit;
            [Tooltip("P8-06: rolls melee kits only (Duelist).")]
            public bool meleeOnly;
        }

        [System.Serializable]
        public class BossEntry
        {
            public GameObject prefab;
            public Ronin7.Enemies.EnemyDefinition definition;
            public int sector; // which sector this boss caps
            [Tooltip("Optional kit composed at spawn for a clawed boss (The-Warden: Claws). Null = the prefab's own baked kit.")]
            public Ronin7.Enemies.EnemyWeaponDefinition mainHand;
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

        /// <summary>P8-02c: most ranged kits one room may roll (fixed kits still count, see RunArenaController).</summary>
        public const int MaxRangedPerRoom = 5;

        /// <summary>P8-02b: the loadout for one spawn. Armed bodies roll uniformly from <see cref="kits"/>; natural
        /// and fixed-kit bodies (and an empty kit list) keep the entry's own default. At or over
        /// <see cref="MaxRangedPerRoom"/> (or for a meleeOnly entry, P8-06) the roll is uniform over the melee kits only.</summary>
        public Kit RollKit(Entry e, ref Ronin7.Core.RunRng rng, int rangedSoFar = 0)
        {
            if (e.fixedKit || e.definition == null || e.definition.naturalWeapon || kits == null || kits.Length == 0)
                return new Kit { main = e.mainHand, off = e.offHand };
            if (!e.meleeOnly && rangedSoFar < MaxRangedPerRoom) return kits[rng.NextInt(kits.Length)];
            int melee = 0;
            foreach (Kit k in kits) if (k.main == null || !k.main.ranged) melee++;
            int n = rng.NextInt(melee);
            foreach (Kit k in kits) if ((k.main == null || !k.main.ranged) && n-- == 0) return k;
            return kits[0];
        }

        public Entry[] trash;
        public BossEntry[] bosses;
    }
}

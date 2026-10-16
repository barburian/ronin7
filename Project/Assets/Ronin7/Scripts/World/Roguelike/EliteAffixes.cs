using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>P3-06: elite modifiers. APPEND-ONLY (values are part of the seeded pick order).</summary>
    public enum EliteAffix { Armored, Swift, Unflinching, Vampiric, Volatile }

    /// <summary>P3-06: pure affix pick/apply. Picking draws from its OWN stream
    /// (<see cref="Stream"/>), never the map/spawn stream, so existing golden vectors cannot move.</summary>
    public static class EliteAffixes
    {
        /// <summary>XOR'd into the node index to derive the affix substream.</summary>
        public const int StreamSalt = 0x0E117E;
        public const float RingSeconds = 1.2f;
        public const float BlastRadius = 2f;

        private const int Count = 5;

        public static RunRng Stream(uint runSeed, int nodeIndex) => RunRng.ForNode(runSeed, nodeIndex ^ StreamSalt);

        /// <summary>1 affix in sectors 0-1, 2 in sector 2+. Never Swift together with Volatile.</summary>
        public static EliteAffix[] Pick(int sector, ref RunRng rng)
        {
            int n = sector >= 2 ? 2 : 1;
            var picked = new EliteAffix[n];
            picked[0] = (EliteAffix)rng.NextInt(Count);
            if (n == 2)
            {
                // Draw from the remaining pool (excluding the first and its forbidden partner).
                Span4 pool = default;
                for (int i = 0; i < Count; i++)
                {
                    var a = (EliteAffix)i;
                    if (a != picked[0] && !Conflicts(a, picked[0])) pool.Add(i);
                }
                picked[1] = (EliteAffix)pool.Get(rng.NextInt(pool.Length));
            }
            return picked;
        }

        public static bool Conflicts(EliteAffix a, EliteAffix b) =>
            (a == EliteAffix.Swift && b == EliteAffix.Volatile) || (a == EliteAffix.Volatile && b == EliteAffix.Swift);

        // 4 slots is enough: 5 affixes minus the first.
        private struct Span4
        {
            private int a, b, c, d;
            public int Length { get; private set; }
            public void Add(int v)
            {
                switch (Length++) { case 0: a = v; break; case 1: b = v; break; case 2: c = v; break; default: d = v; break; }
            }
            public int Get(int i) => i == 0 ? a : i == 1 ? b : i == 2 ? c : d;
        }

        /// <summary>Mutates the runtime CLONE only (never pass a shared asset).</summary>
        public static void Apply(EnemyDefinition clone, EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Armored:
                    if (clone.weakness == WeaknessKind.None) clone.weakness = WeaknessKind.Backstab;
                    else clone.resistMultiplier *= 0.5f;
                    clone.eliteRim = new Color(0.35f, 0.55f, 1f) * 2f; // steel blue
                    break;
                case EliteAffix.Swift:
                    clone.moveSpeed *= 1.35f;
                    clone.attackCooldown *= 0.7f; // telegraphTime deliberately untouched (fairness)
                    clone.eliteRim = new Color(1f, 0.9f, 0.1f) * 2f; // yellow
                    break;
                case EliteAffix.Unflinching:
                    clone.superArmor = true;
                    clone.postureMaxFraction *= 1.5f;
                    clone.eliteRim = Color.white * 2f;
                    break;
                case EliteAffix.Vampiric:
                    clone.vampiric = true;
                    clone.eliteRim = new Color(1f, 0.1f, 0.1f) * 2f; // red
                    break;
                case EliteAffix.Volatile:
                    clone.eliteRim = new Color(1f, 0.45f, 0.05f) * 2f; // orange
                    break;
            }
        }

        /// <summary>Volatile floor-ring diameter at normalised time 0..1 (grows to the blast radius).</summary>
        public static float RingDiameter(float t01) => Mathf.Lerp(0.3f, BlastRadius * 2f, Mathf.Clamp01(t01));

        /// <summary>Volatile blast hit test: horizontal (XZ) distance, so head height cannot dodge it.</summary>
        public static bool InBlast(Vector3 centre, Vector3 p)
        {
            float dx = p.x - centre.x, dz = p.z - centre.z;
            return dx * dx + dz * dz <= BlastRadius * BlastRadius;
        }
    }
}

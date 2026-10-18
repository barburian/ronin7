using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>P3-06 elite affixes (pure).</summary>
    public class EliteAffixesTests
    {
        private static EliteAffix[] PickFor(int sector, uint seed, int node)
        {
            RunRng rng = EliteAffixes.Stream(seed, node);
            return EliteAffixes.Pick(sector, ref rng);
        }

        [Test]
        public void Pick_Deterministic()
        {
            // Golden vectors (xorshift32 over ForNode(seed, node ^ 0x0E117E)); the enum is append-only.
            // Updated deliberately: BUG-35 seed mix (P4-11, RunSnapshot v3) changed ForNode streams.
            Assert.AreEqual(new[] { EliteAffix.Unflinching }, PickFor(0, 12345u, 3));
            Assert.AreEqual(new[] { EliteAffix.Unflinching, EliteAffix.Vampiric }, PickFor(2, 12345u, 3));
            Assert.AreEqual(new[] { EliteAffix.Unflinching, EliteAffix.Armored }, PickFor(2, 777u, 10));
            Assert.AreEqual(PickFor(2, 777u, 10), PickFor(2, 777u, 10));
        }

        [Test]
        public void Pick_Sector0_One()
        {
            Assert.AreEqual(1, PickFor(0, 1u, 1).Length);
            Assert.AreEqual(1, PickFor(1, 1u, 1).Length);
            Assert.AreEqual(2, PickFor(2, 1u, 1).Length);
        }

        [Test]
        public void Pick_NeverSwiftPlusVolatile_NorDuplicates()
        {
            for (uint seed = 1; seed < 400; seed++)
                for (int node = 0; node < 20; node++)
                {
                    EliteAffix[] p = PickFor(2, seed, node);
                    Assert.AreNotEqual(p[0], p[1]);
                    Assert.IsFalse(EliteAffixes.Conflicts(p[0], p[1]), $"seed {seed} node {node}");
                }
        }

        [Test]
        public void Apply_Swift_TelegraphUnchanged()
        {
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            float tele = d.telegraphTime, speed = d.moveSpeed, cd = d.attackCooldown;
            EliteAffixes.Apply(d, EliteAffix.Swift);
            Assert.AreEqual(tele, d.telegraphTime);
            Assert.AreEqual(speed * 1.35f, d.moveSpeed, 1e-5f);
            Assert.AreEqual(cd * 0.7f, d.attackCooldown, 1e-5f);
            Object.DestroyImmediate(d);
        }

        [Test]
        public void Apply_Armored_Unflinching_Vampiric()
        {
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            EliteAffixes.Apply(d, EliteAffix.Armored);
            Assert.AreEqual(WeaknessKind.Backstab, d.weakness);
            float r = d.resistMultiplier;
            EliteAffixes.Apply(d, EliteAffix.Armored); // weakness now set -> resist halves
            Assert.AreEqual(r * 0.5f, d.resistMultiplier, 1e-6f);
            d.postureMaxFraction = 0.4f;
            EliteAffixes.Apply(d, EliteAffix.Unflinching);
            Assert.IsTrue(d.superArmor);
            Assert.AreEqual(0.6f, d.postureMaxFraction, 1e-6f);
            EliteAffixes.Apply(d, EliteAffix.Vampiric);
            Assert.IsTrue(d.vampiric);
            Object.DestroyImmediate(d);
        }

        [Test]
        public void Apply_NeverMutatesSourceAsset()
        {
            var src = ScriptableObject.CreateInstance<EnemyDefinition>();
            string before = JsonUtility.ToJson(src);
            var clone = Object.Instantiate(src);
            foreach (EliteAffix a in System.Enum.GetValues(typeof(EliteAffix))) EliteAffixes.Apply(clone, a);
            Assert.AreEqual(before, JsonUtility.ToJson(src));
            Assert.AreNotEqual(before, JsonUtility.ToJson(clone));
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(clone);
        }

        [Test]
        public void Volatile_BlastRadius()
        {
            Vector3 c = new Vector3(5f, 0f, 5f);
            Assert.IsTrue(EliteAffixes.InBlast(c, c + new Vector3(1.99f, 1.6f, 0f))); // height ignored
            Assert.IsFalse(EliteAffixes.InBlast(c, c + new Vector3(2.01f, 0f, 0f)));
            Assert.AreEqual(EliteAffixes.BlastRadius * 2f, EliteAffixes.RingDiameter(1f), 1e-5f);
            Assert.AreEqual(0.3f, EliteAffixes.RingDiameter(-1f), 1e-5f);
        }
    }
}

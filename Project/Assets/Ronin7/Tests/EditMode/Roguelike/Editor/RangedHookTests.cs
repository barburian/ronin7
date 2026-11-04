using NUnit.Framework;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-02c: ranged strikes fire through Enemy.FireProjectile (a pooled, arcing bolt, never hitscan).</summary>
    public class RangedHookTests
    {
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";
        private int calls; private float lastSpeed, lastGravity;

        [SetUp]
        public void SetUp()
        {
            calls = 0;
            Enemy.FireProjectile = (owner, pos, rot, speed, dmg, life, radius, col, grav) => { calls++; lastSpeed = speed; lastGravity = grav; };
        }

        [TearDown] public void TearDown() => Enemy.FireProjectile = null;

        [Test]
        public void Ranged_FiresViaPoolHook()
        {
            var bow = EnemyWeaponBuilder.Load("Bow");
            if (bow == null) Assert.Fail("EW_Bow missing - run Compose Enemy Weapons (additive).");
            var go = new GameObject("RangedTest");
            go.SetActive(false); // no Awake
            try
            {
                var e = go.AddComponent<Enemy>();
                e.Configure(ScriptableObject.CreateInstance<EnemyDefinition>());
                e.FireStrike(bow.patterns[0].strikes[0]);
                Assert.AreEqual(1, calls);
                Assert.Greater(lastSpeed, 0f);
                Assert.Greater(lastGravity, 0f);
                e.FireStrike(Strike.Chop); // melee never fires
                Assert.AreEqual(1, calls);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("Bow", 2f)]
        [TestCase("Crossbow", 2f)]
        [TestCase("Staff", 2f)]
        [TestCase("Blaster", 1.5f)]
        public void RangedKit_DamagePinned(string kit, float expected)
        {
            var w = EnemyWeaponBuilder.Load(kit);
            Assert.IsNotNull(w, kit);
            Assert.IsTrue(w.ranged, kit);
            Assert.AreEqual(expected, w.damage, 0.0001f, kit);
        }

        [Test]
        public void Ranged_ProjectileNotHitscan()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            int ranged = 0;
            foreach (var kit in table.kits)
                foreach (var p in kit.main.patterns)
                    foreach (var s in p.strikes)
                        if (s.Ranged)
                        {
                            ranged++;
                            Assert.That(s.boltSpeed, Is.GreaterThan(0f).And.LessThanOrEqualTo(12f), kit.main.name);
                            Assert.IsTrue(kit.main.ranged, kit.main.name + " has a ranged strike but the kit flag is off");
                        }
            Assert.Greater(ranged, 0);
        }

        [TestCase(10f, 0f, 8f, 6f)]
        [TestCase(2f, -1f, 4f, 6f)]
        [TestCase(12f, 0.5f, 10f, 3f)]
        public void BallisticDirection_HitsTarget(float dx, float dy, float speed, float gravity)
        {
            var from = Vector3.zero; var to = new Vector3(dx, dy, 0f);
            Vector3 v = Enemy.BallisticDirection(from, to, speed, gravity) * speed, p = from;
            float best = float.MaxValue;
            for (int i = 0; i < 1000 && p.y > -5f; i++)
            {
                v += Vector3.down * (gravity * 0.01f);
                p += v * 0.01f;
                best = Mathf.Min(best, Vector3.Distance(p, to));
            }
            Assert.Less(best, 0.2f);
        }

        [Test]
        public void BallisticDirection_NoGravityIsStraight()
        {
            var d = Enemy.BallisticDirection(Vector3.zero, new Vector3(3f, 1f, 4f), 5f, 0f);
            Assert.That(Vector3.Distance(d, new Vector3(3f, 1f, 4f).normalized), Is.LessThan(1e-4f));
        }
    }
}

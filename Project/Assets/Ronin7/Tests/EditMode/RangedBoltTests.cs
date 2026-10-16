using System;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P3-05: reflectable ranged bolts.</summary>
    public class RangedBoltTests
    {
        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test] public void ReflectDirection_PointsAtTarget()
        {
            Vector3 d = Projectile.ReflectDirection(new Vector3(1, 0, 0), new Vector3(1, 0, 5), Vector3.right);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.forward, d), 1e-5f);
        }

        [Test] public void ReflectDirection_ZeroDistance_Reverse()
        {
            Vector3 d = Projectile.ReflectDirection(Vector3.one, Vector3.one, Vector3.forward);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.back, d), 1e-5f);
        }

        [Test] public void Reflect_PlayerProjectile_Exploits()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.weakness = WeaknessKind.Reflect; def.exploitMultiplier = 2f; def.maxHealth = 100f;
            var enemyGo = new GameObject("ranged-test");
            enemyGo.SetActive(false);
            var enemy = enemyGo.AddComponent<Enemy>();
            var col = enemyGo.AddComponent<BoxCollider>();
            enemy.Configure(def);
            enemyGo.SetActive(true);
            typeof(Health).GetMethod("Awake", Bf).Invoke(enemyGo.GetComponent<Health>(), null);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);

            var player = new GameObject("player");
            player.AddComponent<PlayerCombatModifiers>();
            var boltGo = new GameObject("bolt");
            var bolt = boltGo.AddComponent<Projectile>();
            typeof(Projectile).GetMethod("Awake", Bf).Invoke(bolt, null);
            bolt.Launch(null, enemyGo, new Vector3(0, 0, 3), Quaternion.LookRotation(Vector3.back), 4f, 5f, 4f, 0.12f, Color.red, null);
            var health = enemyGo.GetComponent<Health>();
            int exploited = 0;
            Action<WeaknessExploited> on = e => { if (e.Victim == enemyGo) exploited++; };
            EventBus.Subscribe(on);
            try
            {
                // Un-reflected: the firer's own bolt never hurts it.
                Assert.IsFalse((bool)typeof(Projectile).GetMethod("TryHit", Bf).Invoke(bolt, new object[] { col, Vector3.zero, Vector3.back }));
                Assert.AreEqual(100f, health.Current);

                bolt.Reflect(player);
                Assert.IsTrue(bolt.Reflected);
                Assert.IsTrue((bool)typeof(Projectile).GetMethod("TryHit", Bf).Invoke(bolt, new object[] { col, Vector3.zero, Vector3.back }));
                Assert.AreEqual(1, exploited);
                Assert.IsFalse(health.IsAlive); // Reflect = Max x exploitMultiplier
            }
            finally
            {
                EventBus.Unsubscribe(on);
                UnityEngine.Object.DestroyImmediate(enemyGo); UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(boltGo); UnityEngine.Object.DestroyImmediate(def);
            }
        }
    }
}

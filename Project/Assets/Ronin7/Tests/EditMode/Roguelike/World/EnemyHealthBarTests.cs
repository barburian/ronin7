using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    public class EnemyHealthBarTests
    {
        [Test]
        public void Visible_HiddenUntilHit_ShownThenHiddenAfter4s_ReshownOnHit()
        {
            Assert.IsFalse(EnemyHealthBar.Visible(100f, float.NegativeInfinity)); // never hit
            Assert.IsTrue(EnemyHealthBar.Visible(10f, 10f));
            Assert.IsTrue(EnemyHealthBar.Visible(13.9f, 10f));
            Assert.IsFalse(EnemyHealthBar.Visible(14f, 10f));
            Assert.IsTrue(EnemyHealthBar.Visible(14.1f, 14f)); // re-hit restarts the timer
        }

        [Test]
        public void Boss_GetsNoBar()
        {
            var a = new GameObject("a").AddComponent<Health>();
            var b = new GameObject("b").AddComponent<Health>();
            Assert.IsTrue(EnemyHealthBar.Wants(a, b));
            Assert.IsFalse(EnemyHealthBar.Wants(b, b));
            Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject);
        }

        [Test]
        public void Attach_HiddenUntilHit_FillUsesHealthBarFill()
        {
            var arena = new GameObject("arena").transform;
            var h = new GameObject("enemy").AddComponent<Health>();
            h.Configure(100f);
            EnemyHealthBar.Attach(h, arena);
            Transform bar = arena.Find("EnemyHealthBar");
            Assert.IsNotNull(bar);
            Assert.IsFalse(bar.gameObject.activeSelf);
            h.ApplyDamage(new DamageInfo(25f, Vector3.zero, Vector3.up, null));
            Assert.IsTrue(bar.gameObject.activeSelf);
            Assert.AreEqual(EnemyHealthBar.Width * RunArenaController.HealthBarFill(75f, 100f),
                bar.GetChild(1).localScale.x, 1e-5f);
            Object.DestroyImmediate(h.gameObject); Object.DestroyImmediate(arena.gameObject);
        }
    }
}

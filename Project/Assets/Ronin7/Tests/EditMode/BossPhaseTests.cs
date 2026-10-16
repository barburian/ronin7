using System;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P3-07: <see cref="Enemy.ConfigurePhases"/> / <see cref="Enemy.AdvancePhase"/> and the Vane
    /// feint telegraph. EditMode GameObject, lifecycle invoked by reflection (WeaknessRulesTests pattern).</summary>
    public class BossPhaseTests
    {
        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;

        static EnemyDefinition Def(string name, float hp, WeaknessKind weakness = WeaknessKind.None)
        {
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            d.name = name; d.maxHealth = hp; d.weakness = weakness; d.superArmor = true;
            d.resistMultiplier = 0.5f; d.exploitMultiplier = 2f;
            return d;
        }

        static Enemy Spawn(EnemyDefinition first, params EnemyDefinition[] next)
        {
            var go = new GameObject("boss-phase-test");
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            enemy.Configure(first);
            if (next.Length > 0) enemy.ConfigurePhases(next); // before Awake, as RunArenaController does (A3.1)
            go.SetActive(true);
            typeof(Health).GetMethod("Awake", Bf).Invoke(go.GetComponent<Health>(), null);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);
            return enemy;
        }

        static EnemyDefinition DefOf(Enemy e) => (EnemyDefinition)typeof(Enemy).GetField("definition", Bf).GetValue(e);
        static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", Bf).GetValue(e).ToString();

        static void Destroy(params UnityEngine.Object[] objs) { foreach (var o in objs) if (o != null) UnityEngine.Object.DestroyImmediate(o); }

        [Test] public void Advance_TrueWhilePhasesRemain_FalseAfter()
        {
            EnemyDefinition p1 = Def("P1", 100f), p2 = Def("P2", 100f), p3 = Def("P3", 100f);
            var e = Spawn(p1, p2, p3);
            try
            {
                Assert.IsTrue(e.AdvancePhase());
                Assert.IsTrue(e.AdvancePhase());
                Assert.IsFalse(e.AdvancePhase());
                Assert.IsFalse(e.AdvancePhase());
            }
            finally { Destroy(e.gameObject, p1, p2, p3); }
        }

        [Test] public void Advance_SwapsDefinitionInOrder()
        {
            EnemyDefinition p1 = Def("P1", 100f), p2 = Def("P2", 200f), p3 = Def("P3", 300f);
            var e = Spawn(p1, p2, p3);
            var phases = new System.Collections.Generic.List<int>();
            Action<BossPhaseChanged> onPhase = c => { if (c.Boss == e.gameObject) phases.Add(c.Phase); };
            EventBus.Subscribe(onPhase);
            try
            {
                Health h = e.GetComponent<Health>();
                Assert.AreSame(p1, DefOf(e));
                e.AdvancePhase();
                Assert.AreSame(p2, DefOf(e));
                Assert.AreEqual(200f, h.Max); Assert.AreEqual(200f, h.Current);
                e.AdvancePhase();
                Assert.AreSame(p3, DefOf(e));
                Assert.AreEqual(300f, h.Max);
                CollectionAssert.AreEqual(new[] { 1, 2 }, phases);
            }
            finally { EventBus.Unsubscribe(onPhase); Destroy(e.gameObject, p1, p2, p3); }
        }

        [Test] public void LethalHit_AdvancesPhase_RefillsAndStaggers_DiesOnlyAfterLast()
        {
            EnemyDefinition p1 = Def("P1", 100f), p2 = Def("P2", 150f);
            var e = Spawn(p1, p2);
            Health h = e.GetComponent<Health>();
            int died = 0;
            h.Died += () => died++;
            try
            {
                h.ApplyDamage(new DamageInfo(500f, e.transform.position, Vector3.forward, null));
                Assert.AreEqual(0, died, "phase 1 must not die");
                Assert.AreEqual(150f, h.Current, 1e-4f, "refilled to phase 2's max (ReviveFraction 1)");
                Assert.AreEqual("Stagger", StateOf(e));
                Assert.AreEqual(Enemy.PhaseStaggerSeconds, (float)typeof(MeleeAttacker).GetField("staggerLen", Bf).GetValue(e), 1e-4f);

                h.ApplyDamage(new DamageInfo(500f, e.transform.position, Vector3.forward, null));
                Assert.AreEqual(1, died, "last phase dies");
                Assert.IsFalse(h.IsAlive);
            }
            finally { Destroy(e.gameObject, p1, p2); }
        }

        [Test] public void PhaseSwap_ResetsWeaknessFilter()
        {
            EnemyDefinition p1 = Def("P1", 100f, WeaknessKind.Alternating), p2 = Def("P2", 100f),
                p3 = Def("P3", 100f, WeaknessKind.Alternating);
            var e = Spawn(p1, p2, p3);
            Health h = e.GetComponent<Health>();
            var src = new GameObject("src");
            try
            {
                Assert.IsNotNull(h.DamageFilter);
                // Same-side hit primes the Alternating latch (lastHitSide) in phase 1.
                Vector3 right = e.transform.position + Vector3.right * 0.5f + Vector3.up;
                h.ApplyDamage(new DamageInfo(10f, right, Vector3.left, src));

                e.AdvancePhase(); // None: filter cleared
                Assert.IsNull(h.DamageFilter);

                e.AdvancePhase(); // Alternating again: filter back, latch fresh
                Assert.IsNotNull(h.DamageFilter);
                int resisted = 0;
                Action<HitResisted> onResisted = r => { if (r.Victim == e.gameObject) resisted++; };
                EventBus.Subscribe(onResisted);
                try { h.ApplyDamage(new DamageInfo(10f, right, Vector3.left, src)); }
                finally { EventBus.Unsubscribe(onResisted); }
                Assert.AreEqual(0, resisted, "a stale phase-1 latch must not make the first phase-3 hit a repeat");
                Assert.AreEqual(90f, h.Current, 1e-4f, "neutral first hit");
            }
            finally { Destroy(e.gameObject, src, p1, p2, p3); }
        }

        [Test] public void FeintTelegraph_InRangeAndSeeded()
        {
            EnemyDefinition d = Def("Vane", 100f);
            d.telegraphTime = 0.6f; d.telegraphMaxTime = 1f;
            var a = Spawn(d);
            var b = Spawn(d);
            try
            {
                PropertyInfo tele = typeof(Enemy).GetProperty("TelegraphTime", Bf);
                a.SeedTelegraph(42u); b.SeedTelegraph(42u);
                bool varied = false;
                float first = (float)tele.GetValue(a);
                Assert.AreEqual(first, (float)tele.GetValue(b), "same seed, same draw");
                for (int i = 0; i < 50; i++)
                {
                    float t = (float)tele.GetValue(a);
                    Assert.GreaterOrEqual(t, 0.6f); Assert.LessOrEqual(t, 1f);
                    varied |= !Mathf.Approximately(t, first);
                }
                Assert.IsTrue(varied);
            }
            finally { Destroy(a.gameObject, b.gameObject, d); }
        }
    }
}

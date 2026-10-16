using System;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class WeaknessRulesTests
    {
        const float Heavy = 5.5f;
        static readonly Vector3 Fwd = Vector3.forward;
        static readonly Vector3 Front = new Vector3(0, 0, 5), Back = new Vector3(0, 0, -5);

        static WeaknessContext C(Vector3? src = null, float speed = 0f, DamageType type = DamageType.Melee,
            bool player = true, bool ally = false, bool sight = false, bool stag = false, bool atk = false,
            bool rec = false, bool blink = false, int side = 1, int last = 0, float since = 99f) =>
            new WeaknessContext(Vector3.zero, Fwd, src ?? Front, src ?? Front, speed, type, player, ally, sight,
                stag, atk, rec, blink, side, last, since);

        static WeaknessResult E(WeaknessKind k, WeaknessContext c) => WeaknessRules.Evaluate(k, c, Heavy);

        [Test] public void Backstab()
        {
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.Backstab, C(Front)));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Backstab, C(Back)));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Backstab, C(Front, stag: true)));
        }

        [Test] public void HeavyBreak()
        {
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.HeavyBreak, C(Front, speed: Heavy - 0.01f)));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.HeavyBreak, C(Front, speed: Heavy)));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.HeavyBreak, C(Back, speed: 99f)));
        }

        [Test] public void Recovery()
        {
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.Recovery, C(atk: true)));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Recovery, C(rec: true)));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Recovery, C(stag: true)));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Recovery, C()));
        }

        [Test] public void Alternating()
        {
            const WeaknessKind k = WeaknessKind.Alternating;
            Assert.AreEqual(WeaknessResult.Resisted, E(k, C(side: 1, last: 1, since: 3.99f)));
            Assert.AreEqual(WeaknessResult.Exploited, E(k, C(side: -1, last: 1, since: 3.99f)));
            Assert.AreEqual(WeaknessResult.Neutral, E(k, C(side: 1, last: 1, since: 4f)));
            Assert.AreEqual(WeaknessResult.Neutral, E(k, C(side: -1, last: 1, since: 4f)));
            Assert.AreEqual(WeaknessResult.Neutral, E(k, C(side: 1, last: 0, since: 0f)));
        }

        [Test] public void Reflect()
        {
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Reflect, C(type: DamageType.Projectile, player: true)));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Reflect, C(type: DamageType.Projectile, player: false)));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Reflect, C(type: DamageType.Melee, player: true)));
        }

        [Test] public void Linchpin_AlwaysNeutral()
        {
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Linchpin, C()));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Linchpin, C(Back, stag: true)));
        }

        [Test] public void ParryGate()
        {
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.ParryGate, C()));
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.ParryGate, C(stag: true)));
        }

        [Test] public void CounterHit()
        {
            const WeaknessKind k = WeaknessKind.CounterHit;
            Assert.AreEqual(WeaknessResult.Resisted, E(k, C()));
            Assert.AreEqual(WeaknessResult.Exploited, E(k, C(atk: true)));
            Assert.AreEqual(WeaknessResult.Exploited, E(k, C(stag: true)));
            Assert.AreEqual(WeaknessResult.Exploited, E(k, C(sight: true)));
            Assert.AreEqual(WeaknessResult.Exploited, E(k, C(ally: true)));
        }

        [Test] public void PostureOnly()
        {
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.PostureOnly, C()));
            Assert.AreEqual(WeaknessResult.Resisted, E(WeaknessKind.PostureOnly, C(Back, stag: true, speed: 99f)));
            Assert.AreEqual(0.2f, WeaknessRules.PostureOnlyHpMultiplier);
            Assert.AreEqual(5f, WeaknessRules.PostureOnlyPostureMultiplier);
        }

        [Test] public void Blink()
        {
            Assert.AreEqual(WeaknessResult.Exploited, E(WeaknessKind.Blink, C(blink: true)));
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.Blink, C(blink: false)));
        }

        [Test] public void None_Neutral() =>
            Assert.AreEqual(WeaknessResult.Neutral, E(WeaknessKind.None, C(atk: true)));

        [Test] public void Multiplier_MapsResults()
        {
            Assert.AreEqual(0.2f, WeaknessRules.Multiplier(WeaknessResult.Resisted, 0.2f, 2f));
            Assert.AreEqual(2f, WeaknessRules.Multiplier(WeaknessResult.Exploited, 0.2f, 2f));
            Assert.AreEqual(1f, WeaknessRules.Multiplier(WeaknessResult.Neutral, 0.2f, 2f));
        }

        [Test] public void IsBehind_Boundary110()
        {
            Vector3 At(float deg) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad)) * 5f;
            Assert.IsFalse(WeaknessRules.IsBehind(Vector3.zero, Fwd, At(109.5f)));
            Assert.IsTrue(WeaknessRules.IsBehind(Vector3.zero, Fwd, At(110.5f)));
            Assert.IsFalse(WeaknessRules.IsBehind(Vector3.zero, Fwd, At(0f)));
            Assert.IsTrue(WeaknessRules.IsBehind(Vector3.zero, Fwd, At(180f)));
            Assert.IsTrue(WeaknessRules.IsBehind(Vector3.zero, Fwd, At(-110.5f)));
        }

        [Test] public void Side_Symmetry()
        {
            var r = new Vector3(2, 0, 1);
            var l = new Vector3(-2, 0, 1);
            Assert.AreEqual(1, WeaknessRules.Side(Vector3.zero, Fwd, r));
            Assert.AreEqual(-1, WeaknessRules.Side(Vector3.zero, Fwd, l));
            // facing +X, "right" is -Z, so a +Z point is on the left
            Assert.AreEqual(-1, WeaknessRules.Side(Vector3.zero, Vector3.right, new Vector3(0, 0, 3)));
            Assert.AreEqual(1, WeaknessRules.Side(Vector3.zero, Vector3.right, new Vector3(0, 0, -3)));
        }

        // Soft-lock gate: every kind (incl. future appended ones) needs a path that deals >= full damage with no ability.
        [Test] public void EveryWeaknessKind_HasAbilityFreeDamagePath()
        {
            foreach (WeaknessKind k in Enum.GetValues(typeof(WeaknessKind)))
            {
                // PostureOnly's path is the posture break (x5), not HP.
                bool ok = k == WeaknessKind.PostureOnly && WeaknessRules.PostureOnlyPostureMultiplier >= 1f;
                for (int bits = 0; bits < 256 && !ok; bits++)
                    foreach (var src in new[] { Front, Back })
                    foreach (var speed in new[] { 0f, 10f })
                    foreach (var since in new[] { 0f, 10f })
                    {
                        var c = C(src, speed, (bits & 1) != 0 ? DamageType.Projectile : DamageType.Melee,
                            player: (bits & 2) != 0, ally: false, sight: false, stag: (bits & 4) != 0,
                            atk: (bits & 8) != 0, rec: (bits & 16) != 0, blink: (bits & 32) != 0,
                            side: (bits & 64) != 0 ? 1 : -1, last: (bits & 128) != 0 ? 1 : 0, since: since);
                        if (WeaknessRules.Multiplier(WeaknessRules.Evaluate(k, c, Heavy), 0.2f, 1.5f) >= 1f) { ok = true; break; }
                    }
                Assert.IsTrue(ok, $"{k} has no ability-free damage path (soft-lock)");
            }
        }
        // ---- P3-02: Enemy.FilterDamage wiring (EditMode GameObject; lifecycle invoked by reflection) ----

        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;

        static Enemy Spawn(WeaknessKind kind, bool superArmor = false, float postureMaxFraction = 0f)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.weakness = kind; def.superArmor = superArmor; def.postureMaxFraction = postureMaxFraction;
            def.resistMultiplier = 0.5f; def.exploitMultiplier = 2f; def.maxHealth = 100f;
            var go = new GameObject("weakness-test");
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            var posture = postureMaxFraction > 0f ? go.AddComponent<PostureMeter>() : null;
            enemy.Configure(def);
            go.SetActive(true);
            typeof(Health).GetMethod("Awake", Bf).Invoke(go.GetComponent<Health>(), null);
            if (posture != null)
            {
                typeof(PostureMeter).GetMethod("Awake", Bf).Invoke(posture, null);
                typeof(PostureMeter).GetMethod("OnEnable", Bf).Invoke(posture, null);
            }
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);
            return enemy;
        }

        static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", Bf).GetValue(e).ToString();

        static void Hit(Enemy e, GameObject src, float amount = 10f, float speed = 0f) =>
            e.GetComponent<Health>().ApplyDamage(new DamageInfo(amount, HitPointToward(e, src),
                Vector3.forward, src, DamageType.Melee, speed));

        // Melee backstab reads the hit point, so land it on the side of the enemy facing the source.
        static Vector3 HitPointToward(Enemy e, GameObject src)
        {
            Vector3 d = src.transform.position - e.transform.position; d.y = 0f;
            return e.transform.position + Vector3.up + d.normalized * 0.5f;
        }

        static GameObject Source(Vector3 pos) { var g = new GameObject("src"); g.transform.position = pos; return g; }

        static void Destroy(params UnityEngine.Object[] objs) { foreach (var o in objs) if (o != null) UnityEngine.Object.DestroyImmediate(o); }

        [Test] public void FilterDamage_Resisted_NoFlinchNoPosture()
        {
            var e = Spawn(WeaknessKind.Backstab, postureMaxFraction: 1f);
            var src = Source(Front);
            int resisted = 0;
            Action<HitResisted> onResisted = r => { if (r.Victim == e.gameObject) resisted++; };
            EventBus.Subscribe(onResisted);
            try
            {
                Hit(e, src);
                Assert.AreEqual(95f, e.GetComponent<Health>().Current, 1e-4f); // 10 x resist 0.5
                Assert.AreEqual("Idle", StateOf(e));                            // trash enemy, yet no flinch
                Assert.AreEqual(0f, e.GetComponent<PostureMeter>().Current);
                Assert.AreEqual(1, resisted);
            }
            finally { EventBus.Unsubscribe(onResisted); Destroy(e.gameObject, src); }
        }

        [Test] public void FilterDamage_Exploited_MultipliesAndPublishes()
        {
            var e = Spawn(WeaknessKind.Backstab);
            var src = Source(Back);
            int kind = -1;
            Action<WeaknessExploited> onExploit = x => { if (x.Victim == e.gameObject) kind = x.Kind; };
            EventBus.Subscribe(onExploit);
            try
            {
                Hit(e, src);
                Assert.AreEqual(80f, e.GetComponent<Health>().Current, 1e-4f); // 10 x exploit 2
                Assert.AreEqual((int)WeaknessKind.Backstab, kind);
                Assert.AreEqual("Stagger", StateOf(e)); // a non-resisted hit still flinches
            }
            finally { EventBus.Unsubscribe(onExploit); Destroy(e.gameObject, src); }
        }

        [Test] public void FilterDamage_NoneWeakness_NoFilter()
        {
            var e = Spawn(WeaknessKind.None);
            try { Assert.IsNull(e.GetComponent<Health>().DamageFilter); }
            finally { Destroy(e.gameObject); }
        }

        [Test] public void FilterDamage_HeavyBreakExploit_ForceStaggers()
        {
            var e = Spawn(WeaknessKind.HeavyBreak, superArmor: true); // super-armor: only ForceStagger can stagger
            var src = Source(Front);
            try
            {
                Hit(e, src, speed: Heavy - 0.1f);
                Assert.AreEqual("Idle", StateOf(e));
                Hit(e, src, speed: Heavy);
                Assert.AreEqual("Stagger", StateOf(e));
            }
            finally { Destroy(e.gameObject, src); }
        }

        [Test] public void FilterDamage_ParryGateExploit_DoesNotRestartStagger()
        {
            var e = Spawn(WeaknessKind.ParryGate);
            var src = Source(Front);
            var timer = typeof(MeleeAttacker).GetField("realTimer", Bf);
            try
            {
                e.ForceStagger();
                timer.SetValue(e, 0.3f);
                Hit(e, src); // exploited (already staggered) - must not re-enter Stagger
                Assert.AreEqual("Stagger", StateOf(e));
                Assert.AreEqual(0.3f, (float)timer.GetValue(e), 1e-5f);
            }
            finally { Destroy(e.gameObject, src); }
        }

        [Test] public void Backstab_UsesHitPoint_NotRigRoot()
        {
            var e = Spawn(WeaknessKind.Backstab);
            var src = Source(Front); // rig root in front...
            try
            {
                e.GetComponent<Health>().ApplyDamage(new DamageInfo(10f, e.transform.position + Back.normalized * 0.5f + Vector3.up,
                    Vector3.forward, src, DamageType.Melee)); // ...but the blade landed on the back
                Assert.AreEqual(80f, e.GetComponent<Health>().Current, 1e-4f); // exploited x2
            }
            finally { Destroy(e.gameObject, src); }
        }

        [Test] public void FilterDamage_PostureOnly_HpFifthPostureTimesFive()
        {
            var e = Spawn(WeaknessKind.PostureOnly, postureMaxFraction: 1f);
            var src = Source(Back);
            try
            {
                Hit(e, src);
                Assert.AreEqual(98f, e.GetComponent<Health>().Current, 1e-4f); // 10 x 0.2
                // posture gain = applied 2 HP x5 x PostureMeter's default 0.9 per damage
                Assert.AreEqual(2f * 5f * 0.9f, e.GetComponent<PostureMeter>().Current, 1e-4f);
            }
            finally { Destroy(e.gameObject, src); }
        }
    }
}

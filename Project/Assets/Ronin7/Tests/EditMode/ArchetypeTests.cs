using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P3-04 Part A: archetype behaviour code (Displace, Charger, Blink, Support, Construct, chain).</summary>
    public class ArchetypeTests
    {
        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;
        readonly System.Collections.Generic.List<GameObject> spawned = new();

        [SetUp] public void SetUp() => Health.Active.Clear();

        [TearDown] public void TearDown()
        {
            foreach (var g in spawned) if (g != null) Object.DestroyImmediate(g);
            spawned.Clear();
            Health.Active.Clear();
        }

        T Spawn<T>(Vector3 pos, float halfExtent = 0f, WeaknessKind weakness = WeaknessKind.None,
            float postureMaxFraction = 0f, float bonusFraction = 0f, float maxHealth = 100f) where T : Enemy
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.weakness = weakness; def.maxHealth = maxHealth; def.moveSpeed = 2f;
            def.postureMaxFraction = postureMaxFraction; def.postureBreakBonusFraction = bonusFraction;
            var go = new GameObject(typeof(T).Name);
            spawned.Add(go);
            go.transform.position = pos;
            go.SetActive(false);
            var enemy = go.AddComponent<T>();
            var posture = postureMaxFraction > 0f ? go.AddComponent<PostureMeter>() : null;
            enemy.Configure(def, halfExtent);
            go.SetActive(true);
            var h = go.GetComponent<Health>();
            typeof(Health).GetMethod("Awake", Bf).Invoke(h, null);
            typeof(Health).GetMethod("OnEnable", Bf).Invoke(h, null);
            if (posture != null)
            {
                typeof(PostureMeter).GetMethod("Awake", Bf).Invoke(posture, null);
                typeof(PostureMeter).GetMethod("OnEnable", Bf).Invoke(posture, null);
            }
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);
            return enemy;
        }

        static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", Bf).GetValue(e).ToString();

        // ---- Displace / arena clamp ----

        [Test] public void Displace_ClampsToArenaAndLatchesGroundY()
        {
            var e = Spawn<Enemy>(new Vector3(0f, 0.25f, 0f), halfExtent: 10f);
            e.Displace(new Vector3(100f, 5f, -100f), 0f);
            Vector3 p = e.transform.position;
            Assert.AreEqual(9.5f, p.x, 1e-4f);
            Assert.AreEqual(-9.5f, p.z, 1e-4f);
            Assert.AreEqual(0.25f, p.y, 1e-4f);
        }

        [Test] public void Displace_UnboundedWhenNoExtent()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            e.Displace(new Vector3(100f, 0f, 0f), 0f);
            Assert.AreEqual(100f, e.transform.position.x, 1e-4f);
        }

        [Test] public void ClampToArena_InsideUnchanged()
        {
            var p = new Vector3(3f, 1f, -4f);
            Assert.AreEqual(p, Enemy.ClampToArena(p, 12f));
        }

        // ---- Charger ----

        [Test] public void Charger_LungeClampedToDistancePlusOne()
        {
            Assert.AreEqual(4f, ChargerEnemy.LungeDistance(10f), 1e-5f);
            Assert.AreEqual(4f, ChargerEnemy.LungeDistance(3f), 1e-5f);
            Assert.AreEqual(2.5f, ChargerEnemy.LungeDistance(1.5f), 1e-5f);
        }

        [Test] public void Charger_TriggerBand_3To5()
        {
            Assert.IsFalse(ChargerEnemy.InChargeBand(2.99f));
            Assert.IsTrue(ChargerEnemy.InChargeBand(3f));
            Assert.IsTrue(ChargerEnemy.InChargeBand(5f));
            Assert.IsFalse(ChargerEnemy.InChargeBand(5.01f));
        }

        // ---- Blink ----

        [Test] public void BlinkTarget_StaysInsideArena()
        {
            const float half = 12f;
            for (float x = -half; x <= half; x += 2f)
            for (float z = -half; z <= half; z += 2f)
            for (int a = 0; a < 360; a += 45)
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 p = BlinkEnemy.BlinkTarget(new Vector3(x, 0f, z), Quaternion.Euler(0, a, 0) * Vector3.forward, side, 3f, half);
                Assert.LessOrEqual(Mathf.Abs(p.x), half - 0.5f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(p.z), half - 0.5f + 1e-4f);
            }
        }

        [Test] public void BlinkTarget_SideSymmetry()
        {
            Vector3 r = BlinkEnemy.BlinkTarget(Vector3.zero, Vector3.forward, 1, 3f, 12f);
            Vector3 l = BlinkEnemy.BlinkTarget(Vector3.zero, Vector3.forward, -1, 3f, 12f);
            Assert.AreEqual(3f, r.x, 1e-4f);
            Assert.AreEqual(-3f, l.x, 1e-4f);
            Assert.AreEqual(0f, r.z + l.z, 1e-4f);
        }

        [Test] public void BlinkTarget_FlipsSideWhenWallBlocks()
        {
            // Player 1 m from the east wall facing +z: right (+x) flank is out of bounds, so use the left.
            Vector3 p = BlinkEnemy.BlinkTarget(new Vector3(11f, 0f, 0f), Vector3.forward, 1, 3f, 12f);
            Assert.AreEqual(8f, p.x, 1e-4f);
        }

        [Test] public void BehindTarget_IsBehindPlayer_AndInsideArena()
        {
            Vector3 p = BlinkEnemy.BehindTarget(Vector3.zero, Vector3.forward, 3f, 12f);
            Assert.AreEqual(-3f, p.z, 1e-4f);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Vector3 wall = BlinkEnemy.BehindTarget(new Vector3(0f, 0f, -11f), Vector3.forward, 3f, 12f); // back to the south wall
            Assert.AreEqual(-11.5f, wall.z, 1e-4f);
        }

        // ---- Construct: posture break bonus = 25% of max HP ----

        [Test] public void Construct_PostureBreakBonus_IsQuarterMaxHealth()
        {
            var e = Spawn<Enemy>(Vector3.zero, weakness: WeaknessKind.PostureOnly, postureMaxFraction: 0.5f, bonusFraction: 0.25f, maxHealth: 200f);
            Assert.AreEqual(50f, e.GetComponent<PostureMeter>().BreakBonusDamage, 1e-4f);
        }

        [Test] public void Construct_PostureBreakBonus_PassesFilterUnchanged()
        {
            // postureMax 20 (10% of 200); a 30-damage hit breaks it (x5 gain), bonus 50 must land in full despite PostureOnly's x0.2.
            var e = Spawn<Enemy>(Vector3.zero, weakness: WeaknessKind.PostureOnly, postureMaxFraction: 0.1f, bonusFraction: 0.25f, maxHealth: 200f);
            var h = e.GetComponent<Health>();
            var src = new GameObject("src"); spawned.Add(src);
            src.transform.position = new Vector3(0, 0, 5);
            h.ApplyDamage(new DamageInfo(30f, new Vector3(0, 1, 0.5f), Vector3.forward, src, DamageType.Melee, 0f));
            // 30 x0.2 = 6 hp, then break bonus 50 unfiltered.
            Assert.AreEqual(200f - 6f - 50f, h.Current, 1e-3f);
        }

        // ---- Support drone ----

        [Test] public void Support_BeamHalvesDamage_OnSingleTarget()
        {
            var drone = Spawn<SupportDrone>(new Vector3(0, 0, 5));
            var a = Spawn<Enemy>(new Vector3(0, 0, 1));
            Assert.IsTrue(drone.TryRetarget(0f));
            Assert.AreSame(a.GetComponent<Health>(), drone.Buffed);
            Assert.AreEqual(0.5f, a.GetComponent<Health>().IncomingDamageMultiplier, 1e-5f);
        }

        [Test] public void Support_RetargetsOnDeath_AtMostOncePerSecond()
        {
            var drone = Spawn<SupportDrone>(new Vector3(0, 0, 5));
            var a = Spawn<Enemy>(new Vector3(0, 0, 1));
            var b = Spawn<Enemy>(new Vector3(0, 0, -2));
            Assert.IsTrue(drone.TryRetarget(0f));
            Assert.AreSame(a.GetComponent<Health>(), drone.Buffed);

            Assert.IsFalse(drone.TryRetarget(5f), "live target: no retarget");
            a.GetComponent<Health>().ApplyDamage(new DamageInfo(1000f, a.transform.position, Vector3.forward, null));
            Assert.IsFalse(drone.TryRetarget(0.5f), "within 1 s of the last pick");
            Assert.IsTrue(drone.TryRetarget(1.01f));
            Assert.AreSame(b.GetComponent<Health>(), drone.Buffed);
            Assert.AreEqual(0.5f, b.GetComponent<Health>().IncomingDamageMultiplier, 1e-5f);
        }

        [Test] public void Support_Death_StaggersBuffedAlly_AndReleasesBuff()
        {
            var drone = Spawn<SupportDrone>(new Vector3(0, 0, 5));
            var a = Spawn<Enemy>(new Vector3(0, 0, 1));
            drone.TryRetarget(0f);
            Assert.AreNotEqual("Stagger", StateOf(a));
            drone.GetComponent<Health>().ApplyDamage(new DamageInfo(1000f, drone.transform.position, Vector3.forward, null));
            Assert.AreEqual("Stagger", StateOf(a));
            Assert.AreEqual(1f, a.GetComponent<Health>().IncomingDamageMultiplier, 1e-5f);
        }

        [Test] public void Support_NeverTargetsItselfOrAnotherSupport()
        {
            var d1 = Spawn<SupportDrone>(new Vector3(0, 0, 5));
            Spawn<SupportDrone>(new Vector3(0, 0, 4));
            Assert.IsFalse(d1.TryRetarget(0f));
            Assert.IsNull(d1.Buffed);
        }

        // ---- Review-fix regressions ----

        static readonly BindingFlags BfAll = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static object Call(object o, string name, params object[] a) => typeof(Enemy).GetMethod(name, BfAll).Invoke(o, a);
        static T Field<T>(object o, System.Type t, string name) => (T)t.GetField(name, BfAll).GetValue(o);
        static void SetState(Enemy e, string name) =>
            typeof(MeleeAttacker).GetField("state", Bf).SetValue(e, System.Enum.Parse(typeof(MeleeAttacker).GetNestedType("State", BfAll), name));

        [Test] public void Chain_EndsAfterChainHits_AndAppliesCooldown()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            var tgt = Spawn<Enemy>(new Vector3(0, 0, 1));
            typeof(MeleeAttacker).GetField("target", BfAll).SetValue(e, tgt.GetComponent<Health>());
            var def = Field<EnemyDefinition>(e, typeof(Enemy), "definition");
            def.chainHits = 2; def.attackRange = 50f; def.attackCooldown = 3f;

            for (int swing = 0; swing < 2; swing++)
            {
                SetState(e, "Chase");
                Call(e, "OnChaseTick");
                Assert.AreEqual("Windup", StateOf(e), $"swing {swing + 1} starts");
                Call(e, "OnActiveEnd");
            }
            Assert.Greater(Field<float>(e, typeof(Enemy), "nextAttackTime"), Time.time, "cooldown applied after chainHits swings");
            SetState(e, "Chase");
            Call(e, "OnChaseTick");
            Assert.AreEqual("Chase", StateOf(e), "a 3rd immediate swing must not start");
        }

        [Test] public void ForceStagger_DuringActive_PublishesNoPerfectParry()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            int parries = 0;
            System.Action<PerfectParry> on = _ => parries++;
            EventBus.Subscribe(on);
            try
            {
                SetState(e, "Active");
                e.ForceStagger();
                Assert.AreEqual("Stagger", StateOf(e));
                Assert.AreEqual(0, parries);

                SetState(e, "Active"); // control: a real deflect in the same state does publish
                typeof(MeleeAttacker).GetMethod("Deflect", BfAll, null, new[] { typeof(Vector3) }, null).Invoke(e, new object[] { Vector3.zero });
                Assert.AreEqual(1, parries);
            }
            finally { EventBus.Unsubscribe(on); }
        }

        [Test] public void Charger_StreakDropped_WhenStaggeredMidCrouch()
        {
            var c = Spawn<ChargerEnemy>(Vector3.zero);
            var lr = c.gameObject.AddComponent<LineRenderer>();
            typeof(ChargerEnemy).GetField("streak", BfAll).SetValue(c, lr);
            lr.enabled = true;
            typeof(ChargerEnemy).GetField("charging", BfAll).SetValue(c, true);
            SetState(c, "Stagger");
            Call(c, "BeforeStateTick");
            Assert.IsFalse(lr.enabled);
            Assert.IsFalse(Field<bool>(c, typeof(ChargerEnemy), "charging"));
        }

        [Test] public void Blink_PendingBlinkCancelled_WhenStaggered()
        {
            var b = Spawn<BlinkEnemy>(Vector3.zero);
            var glyph = new GameObject("glyph"); spawned.Add(glyph);
            typeof(BlinkEnemy).GetField("glyph", BfAll).SetValue(b, glyph.transform);
            typeof(BlinkEnemy).GetField("pending", BfAll).SetValue(b, true);
            glyph.SetActive(true);
            SetState(b, "Stagger");
            Call(b, "BeforeStateTick");
            Assert.IsFalse(Field<bool>(b, typeof(BlinkEnemy), "pending"));
            Assert.IsFalse(glyph.activeSelf);
        }

        [Test] public void Vampiric_HealsHalfOfHpActuallyTaken()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            var tgt = Spawn<Enemy>(new Vector3(0, 0, 1));
            var th = tgt.GetComponent<Health>();
            th.IncomingDamageMultiplier = 0.5f; // target takes half of the nominal hit
            typeof(MeleeAttacker).GetField("target", BfAll).SetValue(e, th);
            Field<EnemyDefinition>(e, typeof(Enemy), "definition").vampiric = true;
            var h = e.GetComponent<Health>();
            h.SetCurrent(50f);
            float before = th.Current;
            typeof(MeleeAttacker).GetMethod("LandHit", BfAll).Invoke(e, null);
            float taken = before - th.Current;
            Assert.Greater(taken, 0f);
            Assert.AreEqual(50f + taken * 0.5f, h.Current, 1e-3f);
        }

        [Test] public void Support_SecondDrone_PrefersUnbeamedTarget()
        {
            var d1 = Spawn<SupportDrone>(new Vector3(0, 0, 5));
            var d2 = Spawn<SupportDrone>(new Vector3(0, 0, 4));
            var boss = Spawn<Enemy>(new Vector3(0, 0, 1));
            var other = Spawn<Enemy>(new Vector3(0, 0, -6));
            Assert.IsTrue(d1.TryRetarget(0f));
            Assert.IsTrue(d2.TryRetarget(0f));
            Assert.AreSame(boss.GetComponent<Health>(), d1.Buffed);
            Assert.AreSame(other.GetComponent<Health>(), d2.Buffed);
            Assert.AreEqual(0.5f, boss.GetComponent<Health>().IncomingDamageMultiplier, 1e-5f);
        }
    }
}

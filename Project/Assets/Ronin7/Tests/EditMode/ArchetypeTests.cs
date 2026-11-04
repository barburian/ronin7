using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
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

        [Test] public void Charger_OpeningDash_UncappedAndOnceAfterDelay()
        {
            Assert.AreEqual(11f, ChargerEnemy.OpeningDashDistance(10f), 1e-5f); // not capped at LungeLength 4
            Assert.AreEqual(1f, ChargerEnemy.OpeningDashDistance(0f), 1e-5f);
            Assert.IsFalse(ChargerEnemy.OpeningDashDue(1.99f, false));
            Assert.IsTrue(ChargerEnemy.OpeningDashDue(2f, false));
            Assert.IsFalse(ChargerEnemy.OpeningDashDue(5f, true), "fires once");
        }

        [Test] public void RigArmAngle_RestZero_OverheadNegative_StrikeForwardDown()
        {
            Assert.AreEqual(0f, MeleeAttacker.RigArmAngle(15f, 15f, 55f, 60f), 1e-5f);
            Assert.AreEqual(-155f, MeleeAttacker.RigArmAngle(-140f, 15f, 55f, 60f), 1e-5f);
            Assert.AreEqual(-60f, MeleeAttacker.RigArmAngle(55f, 15f, 55f, 60f), 1e-5f);
            Assert.AreEqual(0f, MeleeAttacker.RigArmAngle(30f, 15f, 15f, 60f), 1e-5f, "degenerate span");
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

        [Test] public void LandHit_DuringIFrames_PublishesNoPlayerHit()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            var tgt = Spawn<Enemy>(new Vector3(0, 0, 1));
            var th = tgt.GetComponent<Health>();
            typeof(MeleeAttacker).GetField("target", BfAll).SetValue(e, th);
            int hits = 0;
            System.Action<PlayerHit> on = _ => hits++;
            EventBus.Subscribe(on);
            try
            {
                th.IncomingDamageMultiplier = 0f; // Phase Step i-frames
                typeof(MeleeAttacker).GetMethod("LandHit", BfAll).Invoke(e, null);
                Assert.AreEqual(0, hits);
                Assert.AreEqual(th.Max, th.Current, 1e-4f);

                th.IncomingDamageMultiplier = 1f;
                typeof(MeleeAttacker).GetMethod("LandHit", BfAll).Invoke(e, null);
                Assert.AreEqual(1, hits);
            }
            finally { EventBus.Unsubscribe(on); }
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

        // ---- Room-clear soft-lock: one death per life, however the damage re-enters ----

        [Test] public void LethalPostureBreak_FiresDiedAndEntityDiedExactlyOnce()
        {
            // postureMax 20: a 30-damage hit (x0.2 PostureOnly = 6 hp) leaves it alive at 24, then the break bonus (50) kills from inside Damaged.
            var e = Spawn<Enemy>(Vector3.zero, weakness: WeaknessKind.PostureOnly, postureMaxFraction: 0.1f, bonusFraction: 0.25f, maxHealth: 200f);
            var h = e.GetComponent<Health>();
            h.SetCurrent(30f);
            int died = 0, entityDied = 0;
            h.Died += () => died++;
            System.Action<EntityDied> onEntityDied = _ => entityDied++;
            EventBus.Subscribe(onEntityDied);
            try { h.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null, DamageType.Melee, 0f)); }
            finally { EventBus.Unsubscribe(onEntityDied); }
            Assert.IsFalse(h.IsAlive);
            Assert.AreEqual(1, died, "Died");
            Assert.AreEqual(1, entityDied, "EntityDied");
        }

        [Test] public void LethalProcInsideEntityDamaged_FiresDiedAndEntityDiedExactlyOnce()
        {
            // RunBoonProcs.Status's shape: a proc lands from an EntityDamaged subscriber, before the outer death check.
            var go = new GameObject("victim"); spawned.Add(go);
            var h = go.AddComponent<Health>();
            h.Configure(100f);
            int died = 0, entityDied = 0;
            h.Died += () => died++;
            System.Action<EntityDied> onEntityDied = _ => entityDied++;
            System.Action<EntityDamaged> proc = d =>
            {
                if (d.Info.Type != DamageType.Status) h.ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.zero, null, DamageType.Status));
            };
            EventBus.Subscribe(onEntityDied);
            EventBus.Subscribe(proc);
            try { h.ApplyDamage(new DamageInfo(1f, Vector3.zero, Vector3.forward, null)); }
            finally { EventBus.Unsubscribe(proc); EventBus.Unsubscribe(onEntityDied); }
            Assert.AreEqual(1, died, "Died");
            Assert.AreEqual(1, entityDied, "EntityDied");
        }

        [Test] public void BossPhaseRevive_DiedFiresOnlyOnTheFinalPhase()
        {
            var go = new GameObject("boss"); spawned.Add(go);
            var h = go.AddComponent<Health>();
            h.Configure(100f);
            int phasesLeft = 1, died = 0;
            h.Died += () => died++;
            h.DeathInterceptor = () =>
            {
                if (phasesLeft-- <= 0) return false;
                h.Configure(100f); // Enemy.AdvancePhase's shape
                return true;
            };
            h.ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(0, died, "phase 1 lethal hit revives");
            Assert.IsTrue(h.IsAlive);
            h.ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(1, died, "final phase dies");
        }

        [TestCase("Configure")] [TestCase("ResetHealth")] [TestCase("SetCurrent")]
        public void Restore_ArmsDeathAgain(string restore)
        {
            var go = new GameObject("respawner"); spawned.Add(go);
            var h = go.AddComponent<Health>();
            h.Configure(100f);
            int died = 0;
            h.Died += () => died++;
            h.ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null));
            if (restore == "Configure") h.Configure(100f);
            else if (restore == "ResetHealth") h.ResetHealth();
            else h.SetCurrent(50f);
            h.ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(2, died);
        }

        // ---- One clamp for every movement write (halfExtent 8: limit +-7.5) ----

        static void Late(Enemy e) => typeof(Enemy).GetMethod("LateUpdate", Bf).Invoke(e, null);

        [Test] public void Clamp_Chase_HeldInsideWallMargin()
        {
            var e = Spawn<Enemy>(Vector3.zero, halfExtent: 8f);
            e.transform.position = new Vector3(20f, 0f, -20f); // what Enemy's unclamped chase step can write
            Late(e);
            Assert.AreEqual(new Vector3(7.5f, 0f, -7.5f), e.transform.position);
        }

        [Test] public void Clamp_KiteBackstep_TimedSlideEndsInside()
        {
            var e = Spawn<Enemy>(new Vector3(7f, 0f, 0f), halfExtent: 8f);
            e.Displace(new Vector3(1.5f, 0f, 0f), 0.35f); // Enemy's kite backstep
            var vel = (Vector3)typeof(Enemy).GetField("displaceVel", Bf).GetValue(e);
            Assert.AreEqual(7.5f, 7f + vel.x * 0.35f, 1e-4f);
        }

        [Test] public void Clamp_LeapLanding_KeepsArcHeight()
        {
            var e = Spawn<LeaperEnemy>(new Vector3(7f, 0f, 0f), halfExtent: 8f);
            Vector3 mid = LeaperEnemy.LeapArc(new Vector3(7f, 0f, 0f), new Vector3(12f, 0f, 0f), 0.5f, 1.5f);
            Assert.Greater(mid.y, 0.5f, "mid-air");
            e.transform.position = mid;
            Late(e);
            Assert.AreEqual(7.5f, e.transform.position.x, 1e-4f);
            Assert.AreEqual(mid.y, e.transform.position.y, 1e-4f, "the arc's height survives");
        }

        [Test] public void Clamp_LeaperRetreat_HeldInside()
        {
            var e = Spawn<LeaperEnemy>(new Vector3(0f, 0f, 7f), halfExtent: 8f);
            e.Displace(new Vector3(0f, 0f, 7f), 0f); // RetreatTarget past the wall
            Late(e);
            Assert.AreEqual(7.5f, e.transform.position.z, 1e-4f);
        }

        [Test] public void Clamp_DuelistSidestep_HeldInside()
        {
            var e = Spawn<DuelistEnemy>(new Vector3(-7f, 0f, 0f), halfExtent: 8f);
            e.Displace(new Vector3(-3f, 0f, 0f), 0f);
            Late(e);
            Assert.AreEqual(-7.5f, e.transform.position.x, 1e-4f);
        }

        [Test] public void Clamp_Flinch_HeldInside()
        {
            var e = Spawn<Enemy>(Vector3.zero, halfExtent: 8f);
            typeof(MeleeAttacker).GetField("flinchStartPos", Bf).SetValue(e, new Vector3(7.45f, 0f, 0f));
            typeof(MeleeAttacker).GetField("flinchDir", Bf).SetValue(e, Vector3.right);
            typeof(MeleeAttacker).GetMethod("PoseFlinch", Bf).Invoke(e, new object[] { 1f }); // pushes to 7.57
            Assert.Greater(e.transform.position.x, 7.5f, "the pose write itself is unclamped");
            Late(e);
            Assert.AreEqual(7.5f, e.transform.position.x, 1e-4f);
        }

        [Test] public void Clamp_Spawn_FallbackPointInsideOnTheFloor()
        {
            Assert.AreEqual(new Vector3(7.5f, 0f, -7.5f), RunArenaController.SpawnFloorPoint(new Vector3(9f, 3f, -9f), 8f));
            Assert.AreEqual(new Vector3(1f, 0f, 2f), RunArenaController.SpawnFloorPoint(new Vector3(1f, 0f, 2f), 8f));
        }

        [Test] public void Clamp_Floor_NeverBelowGround()
        {
            var e = Spawn<Enemy>(new Vector3(0f, 0.25f, 0f), halfExtent: 8f);
            e.transform.position = new Vector3(1f, -3f, 1f);
            Late(e);
            Assert.AreEqual(0.25f, e.transform.position.y, 1e-4f);
        }

        [Test] public void Clamp_NoArena_Unbounded()
        {
            var e = Spawn<Enemy>(Vector3.zero);
            e.transform.position = new Vector3(100f, -3f, 100f);
            Late(e);
            Assert.AreEqual(new Vector3(100f, -3f, 100f), e.transform.position);
        }

        [Test] public void Clamp_Dead_LeavesTheBodyAlone()
        {
            var e = Spawn<Enemy>(Vector3.zero, halfExtent: 8f);
            e.GetComponent<Health>().KillSilently();
            e.transform.position = new Vector3(20f, -1f, 0f);
            Late(e);
            Assert.AreEqual(new Vector3(20f, -1f, 0f), e.transform.position);
        }

        // ---- Arena failsafe ----

        RunArenaController Arena(float halfExtent = 8f)
        {
            var go = new GameObject("arena"); spawned.Add(go);
            var arena = go.AddComponent<RunArenaController>();
            typeof(RunArenaController).GetField("halfExtent", Bf).SetValue(arena, halfExtent);
            return arena;
        }

        static void Call(RunArenaController a, string method) => typeof(RunArenaController).GetMethod(method, Bf).Invoke(a, null);

        [Test] public void Failsafe_OutOfBounds_DiesPastFilterAndInterceptor_NoEntityDied_RoomClears()
        {
            var arena = Arena();
            var e = Spawn<Enemy>(Vector3.zero, halfExtent: 8f);
            e.transform.SetParent(arena.transform, true);
            e.transform.position = new Vector3(30f, 0f, 0f);
            var h = e.GetComponent<Health>();
            h.DamageFilter = _ => 0f;
            h.IncomingDamageMultiplier = 0f;
            h.DeathInterceptor = () => true;
            int died = 0, entityDied = 0;
            h.Died += () => died++;
            System.Action<EntityDied> onEntityDied = _ => entityDied++;
            EventBus.Subscribe(onEntityDied);
            try { Call(arena, "CheckBounds"); }
            finally { EventBus.Unsubscribe(onEntityDied); }
            Assert.IsFalse(h.IsAlive);
            Assert.AreEqual(1, died, "the ledger hears about it");
            Assert.AreEqual(0, entityDied, "no rewards");
            Call(arena, "CheckStrays");
            Assert.IsTrue(arena.IsCleared);
        }

        [Test] public void Failsafe_InBoundsStray_HeldForGrace_ThenKilledAndRoomClears()
        {
            var arena = Arena();
            var e = Spawn<Enemy>(Vector3.zero, halfExtent: 8f);
            e.transform.SetParent(arena.transform, true);
            var h = e.GetComponent<Health>();
            Call(arena, "CheckStrays");
            Assert.IsTrue(h.IsAlive, "the BUG-48 seam still holds during the grace period");
            Assert.IsFalse(arena.IsCleared);
            typeof(RunArenaController).GetField("strayHeldSince", Bf).SetValue(arena, Time.time - 5.1f);
            Call(arena, "CheckStrays");
            Assert.IsFalse(h.IsAlive);
            Call(arena, "CheckStrays");
            Assert.IsTrue(arena.IsCleared);
        }

        [Test] public void Failsafe_NegativeLedger_NoEnemyLeft_StillClears()
        {
            // The v0.9.0 playtest's end state: double Died took the ledger to -1, then the last enemy died.
            var arena = Arena();
            typeof(RunArenaController).GetField("ledgerReady", Bf).SetValue(arena, true);
            typeof(RunArenaController).GetField("aliveCount", Bf).SetValue(arena, -1);
            Call(arena, "Update");
            Assert.IsTrue(arena.IsCleared);
        }
    }
}

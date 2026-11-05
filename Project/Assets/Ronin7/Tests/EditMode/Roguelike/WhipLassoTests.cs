using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-02e (A15.9): the Whip lasso. Gesture traces, orbit, target pick, lifecycle, collisions.</summary>
    public class WhipLassoTests
    {
        private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Dt = 1f / 90f;
        private const float HeadY = 1.6f;
        private static readonly Vector3 Head = new Vector3(0f, HeadY, 0f);

        // --- gesture ---

        private static bool Run(LassoGesture g, Func<float, Vector3> path, float seconds, ref bool armedEver, float dt = Dt)
        {
            bool thrown = false;
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i <= n; i++)
            {
                thrown |= g.Sample(path(i * dt), Head, Vector3.forward, dt);
                armedEver |= g.Armed;
            }
            return thrown;
        }

        private static Func<float, Vector3> Circle(float y, float radius, float revsPerSecond) => t =>
            new Vector3(Mathf.Sin(t * revsPerSecond * 2f * Mathf.PI) * radius, y, Mathf.Cos(t * revsPerSecond * 2f * Mathf.PI) * radius);

        private static Func<float, Vector3> Wheel(float y, float radius, float revsPerSecond) => t =>
            new Vector3(0.3f + Mathf.Sin(t * revsPerSecond * 2f * Mathf.PI) * radius, y + Mathf.Cos(t * revsPerSecond * 2f * Mathf.PI) * radius, 0.3f);

        private static bool Spin(LassoGesture g, Func<float, Vector3> path, float seconds, out Vector3 end)
        {
            bool armed = false;
            Assert.IsFalse(Run(g, path, seconds, ref armed), "a spin alone never throws");
            int n = Mathf.CeilToInt(seconds / Dt);
            end = path(n * Dt);
            return armed;
        }

        private static bool Flick(LassoGesture g, Vector3 from, Vector3 velocity, float seconds = 0.2f)
        {
            bool armed = false;
            return Run(g, t => from + velocity * (t + Dt), seconds, ref armed);
        }

        [Test]
        public void Gesture_OverheadSpinArms()
        {
            Assert.IsTrue(Spin(new LassoGesture(), Circle(2f, 0.3f, 1.5f), 1.2f, out _));
        }

        [Test]
        public void Gesture_ShoulderHeightSpinArms_FlatOrWheel()
        {
            Assert.IsTrue(Spin(new LassoGesture(), Circle(HeadY - 0.25f, 0.3f, 1.5f), 1.2f, out _), "flat circle");
            Assert.IsTrue(Spin(new LassoGesture(), Wheel(HeadY - 0.05f, 0.2f, 1.5f), 1.2f, out _), "a tilted wheel at shoulder height");
        }

        [Test]
        public void Gesture_BelowShoulderDoesNotCount()
        {
            Assert.IsFalse(Spin(new LassoGesture(), Circle(HeadY - LassoGesture.ShoulderDrop - 0.05f, 0.3f, 1.5f), 3f, out _));
            Assert.IsFalse(Spin(new LassoGesture(), Circle(1f, 0.3f, 2f), 3f, out _), "waist height");
        }

        [Test]
        public void Gesture_TooSlowOrTooShortDoesNotArm()
        {
            Assert.IsFalse(Spin(new LassoGesture(), Circle(2f, 0.4f, 0.5f), 4f, out _), "180 deg/s");
            Assert.IsFalse(Spin(new LassoGesture(), Circle(2f, 0.3f, 3f), 0.35f, out _), "a short burst");
        }

        [Test]
        public void Gesture_FigureEightNeverArms()
        {
            foreach (float f in new[] { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f })
            {
                float w = f * 2f * Mathf.PI;
                bool armed = false;
                Run(new LassoGesture(), t => new Vector3(0.4f * Mathf.Sin(w * t), 1.9f, 0.2f * Mathf.Sin(2f * w * t)), 6f, ref armed);
                Assert.IsFalse(armed, $"flat figure-eight at {f} Hz");
                armed = false;
                Run(new LassoGesture(), t => new Vector3(0.4f * Mathf.Sin(w * t), 1.9f + 0.2f * Mathf.Sin(2f * w * t), 0.2f), 6f, ref armed);
                Assert.IsFalse(armed, $"upright figure-eight at {f} Hz");
                armed = false;
                Run(new LassoGesture(), t => new Vector3(0.6f * Mathf.Sin(w * t), 1.9f, 0.05f * Mathf.Sin(2f * w * t)), 6f, ref armed);
                Assert.IsFalse(armed, $"narrow figure-eight at {f} Hz");
            }
        }

        [Test]
        public void Gesture_NormalSwingingNeverArmsOrThrows()
        {
            foreach (float f in new[] { 0.75f, 1.5f, 2.5f })
            {
                float w = f * 2f * Mathf.PI;
                var g = new LassoGesture();
                bool armed = false;
                // Back and forth along one bowed arc overhead: the path curves one way, then the other.
                bool thrown = Run(g, t =>
                {
                    float s = Mathf.Sin(w * t);
                    return new Vector3(0.7f * s, 1.9f - 0.3f * s * s, 0.3f + 0.3f * s * s);
                }, 6f, ref armed);
                Assert.IsFalse(armed, $"bowed slash at {f} Hz");
                Assert.IsFalse(thrown);
                armed = false;
                thrown = Run(new LassoGesture(), t => new Vector3(0f, 1.9f, 0.5f * Mathf.Sin(w * t)), 6f, ref armed);
                Assert.IsFalse(armed, $"straight thrust at {f} Hz");
                Assert.IsFalse(thrown, "a thrust forward is no throw without a spin");
                thrown = Run(new LassoGesture(), Circle(1.9f, 0.25f, f), 6f, ref armed);
                Assert.IsFalse(thrown, $"a wind-up circle at {f} Hz is no throw by itself");
            }
        }

        [Test]
        public void Gesture_UnevenSpinNeverThrowsItself_AndARealFlickStillDoes()
        {
            // Fore-aft ellipses: fastest at the two ends of the long (z) axis, so the forward end is a "flick" to a mean-speed test.
            foreach (var (a, b, hz) in new[] { (0.20f, 0.40f, 2f), (0.15f, 0.30f, 2f), (0.20f, 0.45f, 1.5f) })
            {
                Func<float, Vector3> ellipse = t => new Vector3(Mathf.Sin(t * hz * 2f * Mathf.PI) * a, 2f, Mathf.Cos(t * hz * 2f * Mathf.PI) * b);
                var g = new LassoGesture();
                bool armed = Spin(g, ellipse, 3f, out Vector3 end); // Spin asserts no throw
                // The 0.20 x 0.45 m / 1.5 Hz path is too eccentric to hold the turn rate on its flat sides, so it never arms: no throw is the whole test.
                if (b < 0.45f) Assert.IsTrue(armed, $"ellipse {a}x{b} at {hz} Hz arms");
                if (armed) Assert.IsTrue(Flick(g, end, Vector3.forward * 12f), $"ellipse {a}x{b} at {hz} Hz then a flick");
            }
        }

        [Test]
        public void Gesture_FlickWithoutSpinIsNoThrow()
        {
            Assert.IsFalse(Flick(new LassoGesture(), new Vector3(0f, 2f, 0f), Vector3.forward * 8f));
        }

        [Test]
        public void Gesture_SpinThenForwardFlickThrows()
        {
            var g = new LassoGesture();
            Assert.IsTrue(Spin(g, Circle(2f, 0.3f, 1.5f), 1.2f, out Vector3 end));
            Assert.IsTrue(Flick(g, end, new Vector3(0.5f, -2f, 6f)));
            Assert.Less(Vector3.Angle(g.ThrowDirection, new Vector3(0.5f, 0f, 6f)), 1f);
            Assert.AreEqual(0f, g.ThrowDirection.y);
            Assert.IsFalse(g.Armed, "spent");
        }

        [Test]
        public void Gesture_BackwardFlickIsNoThrow()
        {
            var g = new LassoGesture();
            Spin(g, Circle(2f, 0.3f, 1.5f), 1.2f, out Vector3 end);
            Assert.IsFalse(Flick(g, end, Vector3.back * 6f));
        }

        [Test]
        public void Gesture_ThrowWindowCloses()
        {
            var g = new LassoGesture();
            Spin(g, Circle(2f, 0.3f, 1.5f), 1.2f, out Vector3 end);
            bool armed = false;
            Run(g, t => end, LassoGesture.ThrowWindowSeconds + LassoGesture.GraceSeconds + 0.2f, ref armed);
            Assert.IsFalse(g.Armed);
            Assert.IsFalse(Flick(g, end, Vector3.forward * 8f));
        }

        [Test]
        public void Gesture_FastSteadySpinDoesNotThrowItself()
        {
            // 5 m/s of hand: well over ThrowSpeed every time the circle swings forward.
            Assert.IsFalse(Run(new LassoGesture(), Circle(2f, 0.4f, 2f), 5f, ref Dummy));
            Assert.Greater(0.4f * 2f * 2f * Mathf.PI, LassoGesture.ThrowSpeed);
        }

        private static bool Dummy;

        [Test]
        public void Gesture_HoldsAtDifferentFrameRates()
        {
            foreach (float hz in new[] { 72f, 90f, 120f })
            {
                bool armed = false;
                Run(new LassoGesture(), Circle(2f, 0.3f, 1.5f), 1.2f, ref armed, 1f / hz);
                Assert.IsTrue(armed, $"{hz} Hz");
            }
        }

        [Test]
        public void Gesture_ResetForgetsTheSpin()
        {
            var g = new LassoGesture();
            Spin(g, Circle(2f, 0.3f, 1.5f), 1.2f, out Vector3 end);
            g.Reset();
            Assert.IsFalse(g.Armed);
            Assert.AreEqual(Vector3.zero, g.Velocity);
            Assert.IsFalse(Flick(g, end, Vector3.forward * 8f));
        }

        [Test]
        public void Gesture_AllocatesNothing()
        {
            var g = new LassoGesture();
            Func<float, Vector3> path = Circle(2f, 0.3f, 1.5f);
            Vector3[] trace = new Vector3[500];
            for (int i = 0; i < trace.Length; i++) trace[i] = path(i * Dt);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < trace.Length; i++) g.Sample(trace[i], Head, Vector3.forward, Dt);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // --- orbit ---

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        [Test]
        public void Orbit_RingIsAboutTwoAndAHalfToThreeMetres()
        {
            for (float w = -LassoOrbit.MaxAngularSpeed; w <= LassoOrbit.MaxAngularSpeed; w += 0.5f)
                for (float a = 0f; a < 6.3f; a += 0.4f)
                {
                    Vector3 p = LassoOrbit.RingPoint(Head, a, w, 0f);
                    Assert.GreaterOrEqual(Flat(p, Head), 2.5f - 1e-3f);
                    Assert.LessOrEqual(Flat(p, Head), 3f + 1e-3f);
                    Assert.AreEqual(LassoOrbit.Lift, p.y, 1e-4f);
                }
            Assert.AreEqual(3f, Flat(LassoOrbit.RingPoint(Head, 0f, LassoOrbit.MaxAngularSpeed, 0f), Head), 1e-3f);
            Assert.AreEqual(2.5f, Flat(LassoOrbit.RingPoint(Head, 0f, 0f, 0f), Head), 1e-3f);
        }

        [Test]
        public void Orbit_AngularSpeed_FollowsTheHandAndIsCapped()
        {
            Vector3 captive = new Vector3(0f, 0.5f, 2.8f);
            Assert.AreEqual(0f, LassoOrbit.AngularSpeed(Vector3.zero, Vector3.zero, Head, captive), 1e-4f, "still hand: still captive");
            Assert.Greater(LassoOrbit.AngularSpeed(Vector3.up * 2f, Vector3.zero, Head, captive), 0f);
            Assert.Less(LassoOrbit.AngularSpeed(Vector3.down * 2f, Vector3.zero, Head, captive), 0f);
            Assert.AreEqual(LassoOrbit.MaxAngularSpeed, LassoOrbit.AngularSpeed(Vector3.up * 50f, Vector3.zero, Head, captive), 1e-4f);
            Assert.Greater(LassoOrbit.AngularSpeed(Vector3.zero, Vector3.right, Head, captive), 0f, "a sweep toward +x from +z");
            Assert.Greater(LassoOrbit.AngularSpeed(new Vector3(0f, -0.1f, 5f), Vector3.zero, Head, captive), 0f, "an upright wheel has one direction, no flicker");
            Assert.LessOrEqual(LassoOrbit.MaxAngularSpeed * LassoOrbit.MaxRadius, LassoOrbit.MaxSpeed + 1e-3f);
        }

        [Test]
        public void Orbit_ConstrainKeepsTheArenaAndTheHeadRadius_EvenInACorner()
        {
            const float half = 8f;
            const float lim = 7.5f; // Enemy.ArenaWallMargin
            foreach (Vector3 head in new[] { Head, new Vector3(7.4f, HeadY, 7.4f), new Vector3(-7.5f, HeadY, 0f), new Vector3(0f, HeadY, 7.5f) })
                for (float a = 0f; a < 6.3f; a += 0.1f)
                    foreach (float r in new[] { 0f, 0.5f, 1.4f, 2.5f, 3f, 20f })
                    {
                        Vector3 p = LassoOrbit.Constrain(head + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r), head, half);
                        Assert.LessOrEqual(Mathf.Abs(p.x), lim + 1e-3f);
                        Assert.LessOrEqual(Mathf.Abs(p.z), lim + 1e-3f);
                        Assert.GreaterOrEqual(Flat(p, head), LassoOrbit.HeadSafeRadius - 1e-3f, $"head {head}, angle {a}, r {r}");
                    }
        }

        [Test]
        public void Orbit_StepIsSpeedCappedAndNeverCrossesTheHead()
        {
            Vector3 p = new Vector3(-3f, 0.5f, 0.01f);
            Vector3 target = new Vector3(3f, 0.5f, 0.01f);
            for (int i = 0; i < 200; i++)
            {
                Vector3 next = LassoOrbit.Step(p, target, Head, 8f, Dt);
                Assert.GreaterOrEqual(Flat(next, Head), LassoOrbit.HeadSafeRadius - 1e-3f);
                Assert.LessOrEqual(Vector3.Distance(p, next), LassoOrbit.MaxSpeed * Dt + 0.05f, "no teleport");
                p = next;
            }
            Vector3 far = LassoOrbit.Step(new Vector3(-6f, 0f, 6f), new Vector3(6f, 0f, 6f), Head, 8f, Dt);
            Assert.AreEqual(LassoOrbit.MaxSpeed * Dt, Vector3.Distance(new Vector3(-6f, 0f, 6f), far), 1e-3f);
        }

        [Test]
        public void Fling_FollowsTheHandInAnArcAndLands()
        {
            Vector3 v = LassoOrbit.FlingVelocity(new Vector3(3f, 4f, 0f));
            Assert.AreEqual(0f, v.y);
            Assert.AreEqual(3f * LassoOrbit.FlingSpeedScale, v.x, 1e-4f);
            Assert.AreEqual(LassoOrbit.MaxSpeed, LassoOrbit.FlingVelocity(Vector3.right * 100f).magnitude, 1e-3f, "capped");

            Vector3 start = new Vector3(0f, 0.5f, 3f);
            Vector3 mid = LassoOrbit.FlingPoint(start, v, 0f, LassoOrbit.FlingSeconds * 0.5f);
            Assert.Greater(mid.y, start.y, "an arc");
            Vector3 end = LassoOrbit.FlingPoint(start, v, 0f, LassoOrbit.FlingSeconds + 1f);
            Assert.AreEqual(0f, end.y, 1e-4f, "on the floor");
            Assert.AreEqual(start.x + v.x * LassoOrbit.FlingSeconds, end.x, 1e-3f);
        }

        // --- chain tip ---

        [Test]
        public void Chain_PinTip_HoldsThenLetsGo_AndNeverOverstretches()
        {
            var c = new VerletChain();
            c.Reset(new Vector3(0f, 1.5f, 0f), Vector3.forward, 0f);
            Vector3 target = new Vector3(0f, 1f, 3f);
            c.PinTip(target);
            for (int i = 0; i < 40; i++) c.Tick(new Vector3(0f, 1.5f, 0f), Dt, 0f);
            Assert.AreEqual(0f, Vector3.Distance(c.Positions[c.Count - 1], target), 1e-3f);

            c.PinTip(new Vector3(0f, 1.5f, 50f));
            for (int i = 0; i < 40; i++) c.Tick(new Vector3(0f, 1.5f, 0f), Dt, 0f);
            Assert.LessOrEqual(Vector3.Distance(c.Positions[c.Count - 1], new Vector3(0f, 1.5f, 0f)), VerletChain.TotalLength + 1e-3f);

            c.UnpinTip();
            Vector3 was = c.Positions[c.Count - 1];
            for (int i = 0; i < 90; i++) c.Tick(new Vector3(0f, 1.5f, 0f), Dt, 0f);
            Assert.Less(c.Positions[c.Count - 1].y, was.y, "falls once free");
            Assert.Less(c.MaxSpeed, VerletChain.MaxPointSpeed);
        }

        // --- WhipController ---

        private GameObject rig;
        private WhipController whip;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            Health.Active.Clear();
            rig = new GameObject("Rig");
            XRRigBuilder.RogueEnsureWhip(rig.transform);
            whip = rig.GetComponent<PolearmShieldGear>().whip.GetComponent<WhipController>();
            var vr = rig.AddComponent<VRRig>();
            var head = new GameObject("Head").transform;
            head.SetParent(rig.transform);
            head.position = Head;
            vr.Head = head;
            whip.SetHeld(true);
            whip.Step(Dt);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            MetaProgression.ResetForTests();
            RunState.Reset();
            Object.DestroyImmediate(rig);
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            Health.Active.Clear();
        }

        private Enemy MakeEnemy(Vector3 at, Action<EnemyDefinition> setup = null, float halfExtent = 0f, float hp = 100f)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = hp;
            setup?.Invoke(def);
            var go = new GameObject("lasso-enemy", typeof(BoxCollider));
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            made.Add(go); made.Add(def);
            enemy.Configure(def, halfExtent);
            go.SetActive(true);
            go.transform.position = at;
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 2f, 0.6f);
            box.center = Vector3.up;
            var h = go.GetComponent<Health>();
            typeof(Health).GetMethod("Awake", NonPublic).Invoke(h, null);
            typeof(Health).GetMethod("OnEnable", NonPublic).Invoke(h, null);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", NonPublic).Invoke(enemy, null);
            Physics.SyncTransforms();
            return enemy;
        }

        private static Health H(Enemy e) => e.GetComponent<Health>();
        private static float StaggerLen(Enemy e) => (float)typeof(MeleeAttacker).GetField("staggerLen", NonPublic).GetValue(e);
        private static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", NonPublic).GetValue(e).ToString();
        private static void SetState(Enemy e, string state)
        {
            var f = typeof(MeleeAttacker).GetField("state", NonPublic);
            f.SetValue(e, Enum.Parse(f.FieldType, state));
        }

        private void StepFor(float seconds)
        {
            int n = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < n; i++) whip.Step(Dt);
        }

        private bool Grab(Vector3 aim) => whip.TryBeginLasso(Head, aim);

        [Test]
        public void Constants_AreA15()
        {
            Assert.AreEqual(4f, WhipController.LassoSeconds);
            Assert.AreEqual(8f, WhipController.LassoCooldown);
            Assert.AreEqual(30f, WhipController.CollisionDamage);
            Assert.AreEqual(1.6f, WhipController.LassoReHitSeconds);
            Assert.AreEqual(2.5f, LassoOrbit.MinRadius);
            Assert.AreEqual(3f, LassoOrbit.MaxRadius);
            Assert.Less(LassoOrbit.HeadSafeRadius, LassoOrbit.MinRadius);
            Assert.Less(LassoOrbit.MaxRadius, VerletChain.TotalLength, "the chain reaches the captive");
        }

        [Test]
        public void Target_NearestTheAim_NotNearestThePlayer()
        {
            var close = MakeEnemy(new Vector3(1.5f, 0f, 3f)); // about 27 degrees off
            var onAim = MakeEnemy(new Vector3(0.2f, 0f, 5.5f));
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.AreSame(onAim, whip.Captive);
            Assert.AreNotSame(close, whip.Captive);
        }

        [Test]
        public void Target_ElitesAndBossesAreNeverPicked()
        {
            MakeEnemy(new Vector3(0f, 0f, 4f), d => d.isElite = true);
            MakeEnemy(new Vector3(0f, 0f, 4f), d => d.isBoss = true);
            var phased = MakeEnemy(new Vector3(0f, 0f, 4f));
            var phase = ScriptableObject.CreateInstance<EnemyDefinition>();
            made.Add(phase);
            phased.ConfigurePhases(new[] { phase });
            Assert.IsFalse(Grab(Vector3.forward));
            Assert.AreEqual(0f, whip.LassoCooldownLeft, "a miss costs nothing");

            var trash = MakeEnemy(new Vector3(1f, 0f, 4f));
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.AreSame(trash, whip.Captive);
        }

        [Test]
        public void Target_RunSpawnerMarksElitesAndBosses()
        {
            var method = typeof(RunArenaController).GetMethod("BuildScaledDefinition", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "the flags are set on the spawner's clone");
            var source = ScriptableObject.CreateInstance<EnemyDefinition>();
            made.Add(source);
            var node = new RunNode(0, 1, 0, RoomKind.Combat, 0, 1);
            foreach (var (elite, boss) in new[] { (true, false), (false, true), (false, false) })
            {
                var clone = (EnemyDefinition)method.Invoke(null, new object[] { source, node, elite, boss, 1u, false });
                made.Add(clone);
                Assert.AreEqual(elite, clone.isElite);
                Assert.AreEqual(boss, clone.isBoss);
            }
            Assert.IsFalse(source.isElite || source.isBoss, "the shared asset is untouched");
        }

        [Test]
        public void Target_DeadOutOfConeOutOfRangeOrThePlayer_Miss()
        {
            var dead = MakeEnemy(new Vector3(0f, 0f, 4f));
            H(dead).KillSilently();
            MakeEnemy(new Vector3(4f, 0f, 2f)); // 63 degrees off
            MakeEnemy(new Vector3(0f, 0f, LassoOrbit.AimRange + 1f));
            MakeEnemy(new Vector3(0f, 0f, -4f));
            Assert.IsFalse(Grab(Vector3.forward));
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(0f, whip.LassoCooldownLeft);
        }

        [Test]
        public void EndToEnd_SpinAndThrowFromTheHandGrabs()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Func<float, Vector3> circle = Circle(2f, 0.3f, 1.5f);
            int frames = Mathf.CeilToInt(1.3f / Dt);
            for (int i = 0; i < frames; i++)
            {
                whip.transform.localPosition = circle(i * Dt);
                whip.Step(Dt);
            }
            Assert.IsFalse(whip.LassoBusy, "a spin alone does not grab");
            Vector3 at = circle(frames * Dt);
            for (int i = 1; i < 20 && !whip.LassoBusy; i++)
            {
                whip.transform.localPosition = at + Vector3.forward * (6f * i * Dt);
                whip.Step(Dt);
            }
            Assert.AreSame(e, whip.Captive);
            Assert.IsTrue(e.IsStaggered);
        }

        [Test]
        public void Lifecycle_FourSecondsThenFlingThenLandsAndRestores()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.IsTrue(e.IsStaggered);
            StepFor(WhipController.LassoSeconds - 0.05f);
            Assert.IsTrue(whip.LassoOrbiting);
            Assert.Greater(e.transform.position.y, e.GroundY, "lifted, not on its latch");
            float flat = Flat(e.transform.position, Head);
            Assert.GreaterOrEqual(flat, LassoOrbit.MinRadius - 0.05f);
            Assert.LessOrEqual(flat, LassoOrbit.MaxRadius + 0.05f);

            StepFor(0.1f);
            Assert.IsTrue(whip.LassoFlinging);
            Assert.AreEqual(WhipController.LassoCooldown, whip.LassoCooldownLeft, 0.2f);

            StepFor(LassoOrbit.FlingSeconds + 0.05f);
            Assert.IsFalse(whip.LassoBusy);
            Assert.IsNull(whip.Captive);
            Assert.AreEqual(e.GroundY, e.transform.position.y, 1e-4f, "on the floor");
            Assert.AreEqual("Stagger", StateOf(e), "stagger ends normally");
            Assert.AreEqual(WhipController.LandStaggerSeconds, StaggerLen(e), 1e-4f);
            Assert.AreEqual(100f, H(e).Current, 1e-4f, "no collision, no damage");
        }

        [Test]
        public void Lifecycle_ReleaseFlingsAlongTheHand_AndFinishesUnheld()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Assert.IsTrue(Grab(Vector3.forward));
            StepFor(0.6f);
            // The hand drives +x for a few samples, then the grip opens.
            for (int i = 1; i <= 12; i++)
            {
                whip.transform.localPosition = Vector3.right * (0.02f * i);
                whip.Step(Dt);
            }
            Vector3 before = e.transform.position;
            whip.SetHeld(false);
            Assert.IsTrue(whip.LassoFlinging, "release is the fling");
            Assert.AreEqual(WhipController.LassoCooldown, whip.LassoCooldownLeft, 1e-3f);
            int n = Mathf.CeilToInt((LassoOrbit.FlingSeconds + 0.05f) / Dt);
            for (int i = 0; i < n; i++) whip.StepUnheld(Dt);
            Assert.IsFalse(whip.LassoBusy);
            Assert.Greater(e.transform.position.x, before.x + 0.5f, "along the hand's +x");
            Assert.AreEqual(e.GroundY, e.transform.position.y, 1e-4f);
        }

        [Test]
        public void Lifecycle_CaptiveDeathEndsItAndStartsTheCooldown()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(0.5f);
            H(e).KillSilently();
            whip.Step(Dt);
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(WhipController.LassoCooldown, whip.LassoCooldownLeft, 1e-3f);
            Assert.AreEqual(e.GroundY, e.transform.position.y, 1e-4f, "no floating corpse");
            Assert.AreEqual("Dead", StateOf(e), "not staggered back to life");
        }

        [Test]
        public void Lifecycle_CaptiveDestroyedCannotThrow()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(0.2f);
            Object.DestroyImmediate(e.gameObject);
            Assert.DoesNotThrow(() => whip.Step(Dt));
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(WhipController.LassoCooldown, whip.LassoCooldownLeft, 1e-3f);

            Health.Active.RemoveAll(h => h == null); // edit mode skips the OnDisable that does this in play
            var f = MakeEnemy(new Vector3(0f, 0f, 4f));
            typeof(WhipController).GetField("lassoCooldownLeft", NonPublic).SetValue(whip, 0f);
            Grab(Vector3.forward);
            whip.SetHeld(false); // into the fling
            Object.DestroyImmediate(f.gameObject);
            Assert.DoesNotThrow(() => whip.StepUnheld(Dt));
            Assert.IsFalse(whip.LassoBusy);
        }

        [Test]
        public void Lifecycle_CooldownBlocksReEntry_ThenReopens_AndRunsHolstered()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            H(e).KillSilently();
            whip.Step(Dt);
            var next = MakeEnemy(new Vector3(0f, 0f, 4f));
            Assert.IsFalse(Grab(Vector3.forward), "blocked");
            StepFor(WhipController.LassoCooldown - 0.5f);
            Assert.IsFalse(Grab(Vector3.forward), "still blocked");
            whip.SetHeld(false);
            for (int i = 0; i < 60; i++) whip.TickLassoCooldown(Dt); // holstering does not reset or stop it
            whip.SetHeld(true);
            Assert.AreEqual(0f, whip.LassoCooldownLeft, 1e-3f);
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.AreSame(next, whip.Captive);
        }

        [Test]
        public void Lifecycle_PauseDrainsNothingAndMovesNothing()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(1f);
            Vector3 at = e.transform.position;
            Time.timeScale = 0f;
            StepFor(10f);
            Assert.AreEqual(at, e.transform.position);
            Assert.IsTrue(whip.LassoOrbiting, "the pause did not drain it");
            Time.timeScale = 1f;
            StepFor(WhipController.LassoSeconds - 1.1f);
            Assert.IsTrue(whip.LassoOrbiting);
            StepFor(0.2f);
            Assert.IsTrue(whip.LassoFlinging);
            float cooldown = whip.LassoCooldownLeft;
            Time.timeScale = 0f;
            StepFor(5f);
            Assert.AreEqual(cooldown, whip.LassoCooldownLeft);
        }

        [Test]
        public void Lifecycle_PauseDropsTheStaleHandSample_SoResumingIsNoThrow()
        {
            MakeEnemy(new Vector3(0f, 0f, 4f));
            Func<float, Vector3> circle = Circle(2f, 0.3f, 1.5f);
            int frames = Mathf.CeilToInt(1.3f / Dt);
            for (int i = 0; i < frames; i++)
            {
                whip.transform.localPosition = circle(i * Dt);
                whip.Step(Dt);
            }
            Time.timeScale = 0f;
            whip.Step(Dt);
            Vector3 moved = circle(frames * Dt) + Vector3.forward * 3f;
            whip.transform.localPosition = moved;
            whip.Step(Dt);
            Time.timeScale = 1f;
            for (int i = 0; i < 10; i++)
            {
                whip.transform.localPosition = moved;
                whip.Step(Dt);
            }
            Assert.IsFalse(whip.LassoBusy, "moving during the pause is not a flick");
        }

        [Test]
        public void Lifecycle_SlowTimeDoesNotStretchIt()
        {
            MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            Time.timeScale = 0.5f; // Overdrive: real time still runs
            StepFor(WhipController.LassoSeconds + 0.05f);
            Assert.IsTrue(whip.LassoFlinging);
        }

        [Test]
        public void Lifecycle_ABrokenHeldStaggerIsReapplied()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            whip.Step(Dt);
            SetState(e, "Idle"); // what a shortened stagger running out leaves
            whip.Step(Dt);
            Assert.IsTrue(e.IsStaggered);
            Assert.Greater(StaggerLen(e), WhipController.LassoSeconds);
        }

        [Test]
        public void Lifecycle_PlayerDeathDropsTheCaptive()
        {
            var playerHealth = rig.AddComponent<Health>();
            playerHealth.Configure(100f);
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(0.5f);
            playerHealth.KillSilently();
            whip.Step(Dt);
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(e.GroundY, e.transform.position.y, 1e-4f);
            Assert.AreEqual(WhipController.LandStaggerSeconds, StaggerLen(e), 1e-4f, "released, not held");
        }

        [Test]
        public void Lifecycle_DisableReleasesTheCaptive_AndIsSafeWithItGone()
        {
            var onDisable = typeof(WhipController).GetMethod("OnDisable", NonPublic);
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(0.5f);
            onDisable.Invoke(whip, null);
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(e.GroundY, e.transform.position.y, 1e-4f);
            Assert.AreEqual(WhipController.LandStaggerSeconds, StaggerLen(e), 1e-4f);

            typeof(WhipController).GetField("lassoCooldownLeft", NonPublic).SetValue(whip, 0f);
            Assert.IsTrue(Grab(Vector3.forward));
            Object.DestroyImmediate(e.gameObject);
            Assert.DoesNotThrow(() => onDisable.Invoke(whip, null));
            Assert.IsFalse(whip.LassoBusy);
        }

        [Test]
        public void Lifecycle_StaysInTheArenaAndOutOfTheHead_SoTheRoomCanStillClear()
        {
            // The player backed into a corner of an 8 m room, spinning hard: the bounds check must never kill it, it stays a live enemy.
            rig.transform.position = new Vector3(7f, 0f, 7f);
            whip.SetHeld(false); whip.SetHeld(true); whip.Step(Dt);
            var e = MakeEnemy(new Vector3(7f, 0f, 3f), halfExtent: 8f);
            Vector3 head = rig.transform.position + Vector3.up * HeadY;
            Assert.IsTrue(whip.TryBeginLasso(head, Vector3.back));
            Func<float, Vector3> circle = Circle(2f, 0.4f, 2f);
            for (int f = 0; f < WhipController.LassoSeconds / Dt + 80; f++)
            {
                whip.transform.localPosition = circle(f * Dt);
                whip.Step(Dt);
                Vector3 p = e.transform.position;
                Assert.IsFalse(RunArenaController.OutOfArena(p, 8f), $"frame {f}: {p}");
                Assert.GreaterOrEqual(Flat(p, head), LassoOrbit.HeadSafeRadius - 1e-3f, $"frame {f}");
                Assert.LessOrEqual(Mathf.Abs(p.x), 7.5f + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(p.z), 7.5f + 1e-3f);
                Assert.IsTrue(H(e).IsAlive);
                Assert.Contains(H(e), Health.Active);
            }
            Assert.IsFalse(whip.LassoBusy, "it ends on its own");
            bool died = false;
            H(e).Died += () => died = true;
            H(e).ApplyDamage(new DamageInfo(1000f, e.transform.position, Vector3.forward, rig));
            Assert.IsTrue(died, "killable as usual after, so the ledger clears");
        }

        [Test]
        public void Lifecycle_KillingACaptiveFiresDiedNormally()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            bool died = false;
            H(e).Died += () => died = true;
            Grab(Vector3.forward);
            StepFor(0.3f);
            H(e).ApplyDamage(new DamageInfo(1000f, e.transform.position, Vector3.forward, rig));
            Assert.IsTrue(died, "the room's ledger sees a captive die");
            whip.Step(Dt);
            Assert.IsFalse(whip.LassoBusy);
        }

        [Test]
        public void Chain_TipFollowsTheCaptiveDuringTheOrbit_AndNoChainHits()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 2.8f));
            Grab(Vector3.forward);
            StepFor(0.5f);
            Vector3 tip = whip.Chain.Positions[whip.Chain.Count - 1];
            Assert.Less(Vector3.Distance(tip, e.transform.position + Vector3.up * WhipController.LassoHitHeight), 0.2f);
            Assert.AreEqual(1, whip.LastChecks, "one overlap, the chain itself checks nothing");
        }

        // A captive already on the ring in front, a second enemy touching it.
        private Enemy Captured(out Enemy other, float otherHp = 100f, Action<EnemyDefinition> otherSetup = null)
        {
            var e = MakeEnemy(new Vector3(0f, 0f, LassoOrbit.MinRadius));
            other = MakeEnemy(new Vector3(0.5f, 0f, LassoOrbit.MinRadius), otherSetup, hp: otherHp);
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.AreSame(e, whip.Captive);
            return e;
        }

        [Test]
        public void Collision_ThirtyToBoth_OtherStaggers_OnceWithinTheCooldown()
        {
            var e = Captured(out var other);
            whip.Step(Dt);
            Assert.AreEqual(100f - WhipController.CollisionDamage, H(other).Current, 1e-3f);
            Assert.AreEqual(100f - WhipController.CollisionDamage, H(e).Current, 1e-3f);
            Assert.IsTrue(other.IsStaggered);
            StepFor(WhipController.LassoReHitSeconds - 0.1f);
            Assert.AreEqual(70f, H(other).Current, 1e-3f, "once per orbit, not every ReHitCooldown");
            Assert.AreEqual(70f, H(e).Current, 1e-3f);
            StepFor(0.2f);
            Assert.AreEqual(40f, H(other).Current, 1e-3f, "the lasso re-hit window reopens");
        }

        [Test]
        public void Collision_RunsThroughDamageBoons_RigAsSource_CollisionWithRealSpeed()
        {
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            mods.BoonMultiplier = 1.5f;
            var e = Captured(out var other);
            DamageInfo seen = default;
            H(other).Damaged += i => seen = i;
            whip.Step(Dt);
            Assert.AreEqual(100f - 45f, H(other).Current, 1e-3f);
            Assert.AreEqual(100f - 45f, H(e).Current, 1e-3f);
            Assert.AreEqual(DamageType.Collision, seen.Type);
            Assert.AreSame(rig, seen.Source);
            Assert.Less(seen.Speed, LassoOrbit.MaxSpeed + 0.01f);
        }

        [Test]
        public void Collision_PublishesOneNormalImpact_ForTheEnemyHit()
        {
            var e = Captured(out var other);
            var victims = new List<GameObject>();
            Action<SwordImpact> sub = s => { Assert.IsFalse(s.FeedbackOnly); victims.Add(s.Victim); };
            EventBus.Subscribe(sub);
            try { whip.Step(Dt); }
            finally { EventBus.Unsubscribe(sub); }
            CollectionAssert.AreEqual(new[] { other.gameObject }, victims, "one impact: one Overdrive charge, one combo count, one haptic");
            Assert.AreEqual(70f, H(e).Current, 1e-3f, "the captive still takes its damage");
        }

        [TestCase(WeaknessKind.HeavyBreak)]
        [TestCase(WeaknessKind.Backstab)]
        [TestCase(WeaknessKind.Recovery)]
        [TestCase(WeaknessKind.CounterHit)]
        [TestCase(WeaknessKind.PostureOnly)]
        [TestCase(WeaknessKind.ParryGate)]
        public void Collision_IgnoresWeaknessRules_AboutThirtyToBoth(WeaknessKind kind)
        {
            rig.AddComponent<PlayerCombatModifiers>().BoonMultiplier = 1.5f;
            var e = MakeEnemy(new Vector3(0f, 0f, LassoOrbit.MinRadius), d => d.weakness = kind);
            var other = MakeEnemy(new Vector3(0.5f, 0f, LassoOrbit.MinRadius), d => d.weakness = kind);
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.IsTrue(e.IsStaggered, "a staggered captive is what Backstab / Recovery / CounterHit exploit");
            int events = 0;
            Action<HitResisted> r = _ => events++;
            Action<WeaknessExploited> x = _ => events++;
            EventBus.Subscribe(r);
            EventBus.Subscribe(x);
            try { whip.Step(Dt); }
            finally { EventBus.Unsubscribe(r); EventBus.Unsubscribe(x); }
            Assert.AreEqual(100f - 45f, H(other).Current, 1e-3f, kind.ToString());
            Assert.AreEqual(100f - 45f, H(e).Current, 1e-3f, kind.ToString());
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Collision_HeavyBreakIsThirtyAtAnySpeed_ButASlowMeleeSwingStillResists()
        {
            var e = MakeEnemy(Vector3.zero, d => d.weakness = WeaknessKind.HeavyBreak);
            foreach (float speed in new[] { 0f, 12f })
            {
                float before = H(e).Current;
                H(e).ApplyDamage(new DamageInfo(30f, e.transform.position + Vector3.up, Vector3.forward, rig, DamageType.Collision, speed));
                Assert.AreEqual(30f, before - H(e).Current, 1e-3f, $"speed {speed}");
            }
            float b = H(e).Current;
            H(e).ApplyDamage(new DamageInfo(30f, e.transform.position + Vector3.up, Vector3.forward, rig, DamageType.Melee, 0f));
            Assert.Less(b - H(e).Current, 30f, "a slow Melee swing is still resisted");
        }

        [Test]
        public void Collision_DeadCaptiveTakesNothingMore()
        {
            // Two others touch at once; the first kills the captive, the second must not hit the body.
            var e = MakeEnemy(new Vector3(0f, 0f, LassoOrbit.MinRadius), hp: 30f);
            MakeEnemy(new Vector3(0.5f, 0f, LassoOrbit.MinRadius));
            MakeEnemy(new Vector3(-0.5f, 0f, LassoOrbit.MinRadius));
            Assert.IsTrue(Grab(Vector3.forward));
            int hits = 0;
            H(e).Damaged += _ => hits++;
            whip.Step(Dt);
            Assert.IsFalse(H(e).IsAlive);
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Collision_SuperArmourStillStaggers_AsA15Says()
        {
            Captured(out var other, otherSetup: d => d.superArmor = true);
            whip.Step(Dt);
            Assert.IsTrue(other.IsStaggered, "ForceStagger has no armor gate; reported, not changed");
        }

        [Test]
        public void Collision_KillingTheOtherDoesNotStaggerACorpse()
        {
            Captured(out var other, otherHp: 10f);
            whip.Step(Dt);
            Assert.IsFalse(H(other).IsAlive);
            Assert.AreEqual("Dead", StateOf(other));
        }

        [Test]
        public void Collision_CaptiveKilledByItEndsTheLasso()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, LassoOrbit.MinRadius), hp: 20f);
            MakeEnemy(new Vector3(0.5f, 0f, LassoOrbit.MinRadius));
            Assert.IsTrue(Grab(Vector3.forward));
            Assert.AreSame(e, whip.Captive);
            whip.Step(Dt);
            Assert.IsFalse(H(e).IsAlive);
            Assert.IsFalse(whip.LassoBusy);
            Assert.AreEqual(WhipController.LassoCooldown, whip.LassoCooldownLeft, 1e-3f);
        }

        [Test]
        public void Collision_PropsWithHealthAndThePlayerAreNotEnemies()
        {
            var playerHealth = rig.AddComponent<Health>();
            playerHealth.Configure(100f);
            rig.AddComponent<BoxCollider>().center = new Vector3(0f, 1f, LassoOrbit.MinRadius);
            var prop = new GameObject("prop", typeof(BoxCollider), typeof(Health));
            made.Add(prop);
            prop.transform.position = new Vector3(0.5f, 1f, LassoOrbit.MinRadius);
            prop.GetComponent<Health>().Configure(100f);
            Physics.SyncTransforms();
            var e = MakeEnemy(new Vector3(0f, 0f, LassoOrbit.MinRadius));
            Assert.IsTrue(Grab(Vector3.forward));
            whip.Step(Dt);
            Assert.AreEqual(100f, playerHealth.Current, 1e-3f);
            Assert.AreEqual(100f, prop.GetComponent<Health>().Current, 1e-3f);
            Assert.AreEqual(100f, H(e).Current, 1e-3f);
        }

        [Test]
        public void Lasso_WarmedOrbitFrameAllocatesNothing()
        {
            MakeEnemy(new Vector3(0f, 0f, 4f));
            Grab(Vector3.forward);
            StepFor(0.2f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) whip.Step(Dt);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void Lasso_WarmedCollisionFrameAllocatesNothing()
        {
            var e = Captured(out var other);
            H(e).Configure(1000000f);
            H(other).Configure(1000000f);
            whip.Step(Dt); // warm-up: the first hit primes the tables
            Assert.Less(H(other).Current, 1000000f);
            float was = H(other).Current;
            long before = GC.GetAllocatedBytesForCurrentThread();
            int frames = 0;
            while (H(other).Current >= was && frames++ < 200) whip.Step(Dt);
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Less(H(other).Current, was, "a second hit landed");
            Assert.AreEqual(0L, used);
        }
    }
}

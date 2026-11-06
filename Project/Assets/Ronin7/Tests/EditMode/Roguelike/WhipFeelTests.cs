using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode
{
    /// <summary>R11: the light airborne chain, the launch from the grip, the steer and the aim marker.</summary>
    public class WhipFeelTests
    {
        private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Dt = 1f / 90f;
        private const float HandY = 1.2f;
        private static readonly Vector3 Pin = new Vector3(0f, HandY, 0f);

        private GameObject rig;
        private WhipController whip;
        private Transform head;
        private readonly List<Object> made = new List<Object>();

        // --- the chain alone ---

        private static float Length(VerletChain c)
        {
            float sum = 0f;
            for (int i = 1; i < c.Count; i++) sum += Vector3.Distance(c.Positions[i], c.Positions[i - 1]);
            return sum;
        }

        private static VerletChain Settled(int hz, float seconds = 4f, float y = HandY)
        {
            var c = new VerletChain();
            var pin = new Vector3(0f, y, 0f);
            c.Reset(pin, Vector3.forward, 0f);
            for (int f = 0; f < seconds * hz; f++) c.Tick(pin, 1f / hz, 0f);
            return c;
        }

        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void Rest_ArcsOutAhead_OffTheFloor_AndStaysThere(int hz)
        {
            var c = Settled(hz);
            int tip = c.Count - 1;
            for (int f = 0; f < hz * 10; f++)
            {
                c.Tick(Pin, 1f / hz, 0f);
                Assert.Less(c.MaxSpeed, 0.5f, "settled, no jitter: frame " + f);
                for (int i = 1; i < c.Count; i++) Assert.Greater(c.Positions[i].y, 0.7f, "point " + i + " off the floor");
            }
            Assert.That(c.Positions[tip].y, Is.InRange(1.0f, 1.7f), "the tip is at waist to chest height");
            Assert.That(c.Positions[tip].z, Is.InRange(3.2f, 4f), "out ahead of the hand");
            Assert.AreEqual(0f, c.Positions[tip].x, 1e-3f);
            Assert.AreEqual(VerletChain.TotalLength, Length(c), 0.1f);
        }

        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void Rest_IsTheSameAtEveryRate(int hz)
        {
            Vector3 at90 = Settled(90, 8f).Positions[9];
            Assert.Less(Vector3.Distance(Settled(hz, 8f).Positions[9], at90), 0.05f);
        }

        [Test]
        public void Rest_ADroopingHandleKeepsTheTipUp_ARaisedOneLiftsIt()
        {
            var c = Settled(90, 1f, 0.9f);
            var pin = new Vector3(0f, 0.9f, 0f);
            Vector3 down = new Vector3(0f, -0.8f, 0.6f).normalized;
            for (int f = 0; f < 90 * 5; f++) { c.SetLiftDirection(down); c.Tick(pin, Dt, 0f); }
            float drooped = c.Positions[9].y;
            Assert.Greater(drooped, 0.7f);

            Vector3 up = new Vector3(0f, 0.4f, 0.9f).normalized;
            for (int f = 0; f < 90 * 5; f++) { c.SetLiftDirection(up); c.Tick(pin, Dt, 0f); }
            Assert.Greater(c.Positions[9].y, drooped + 0.5f);

            Vector3 lift = new Vector3(1f, 0f, 0f);
            for (int f = 0; f < 90 * 6; f++) { c.SetLiftDirection(lift); c.Tick(pin, Dt, 0f); }
            Assert.Greater(c.Positions[9].x, 3f, "it follows the handle around");
        }

        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void Launch_ReachesAbout4m_Forward_ThenComesBack_NeverOnTheFloor(int hz)
        {
            var c = Settled(hz);
            Vector3 dir = new Vector3(0f, 0.1f, 1f).normalized;
            c.Launch(dir, false, Vector3.zero);
            float reach = 0f, peak = 0f, last = 0f, lowest = 9f;
            for (int f = 0; f < hz * 2; f++)
            {
                c.Tick(Pin, 1f / hz, 0f);
                Vector3 tip = c.Positions[9];
                reach = Mathf.Max(reach, tip.z);
                peak = Mathf.Max(peak, c.Velocity(9).magnitude);
                lowest = Mathf.Min(lowest, tip.y);
                last = tip.z;
                Assert.AreEqual(0f, tip.x, 1e-2f, "straight along the throw");
                Assert.LessOrEqual(Length(c), VerletChain.TotalLength + 0.2f);
            }
            Assert.That(reach, Is.InRange(3.8f, 4.05f), "about the chain's 4 m");
            Assert.GreaterOrEqual(peak, 5.5f, "straight ahead the taut chain has little room; a launch flight cracks anyway (Crack_ tests)");
            Assert.Greater(lowest, 0.3f);
            Assert.Greater(last, 2.5f, "and back ahead of the hand, not past the player");
            Assert.IsFalse(c.InFlight);
            Assert.Less(c.MaxSpeed, 2.5f, "the chain has settled");
        }

        [Test]
        public void Launch_SurvivesDtSpikes_AndRepeatedThrows()
        {
            var c = Settled(90, 1f);
            float[] dts = { 0f, 0.0001f, 0.05f, 0.25f, 1.5f, Dt };
            for (int f = 0; f < 600; f++)
            {
                if (f % 7 == 0) c.Launch(Quaternion.Euler(0f, f * 13f, 0f) * Vector3.forward, f % 2 == 0, new Vector3(Mathf.Sin(f), 1.2f, Mathf.Cos(f)) * 3f);
                c.Tick(Pin + Vector3.right * Mathf.Sin(f * 0.3f), dts[f % dts.Length], 0f);
                foreach (Vector3 p in c.Positions) Assert.IsFalse(float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z), "frame " + f);
                Assert.LessOrEqual(c.MaxSpeed, VerletChain.MaxPointSpeed * 3f);
                // Not the first link: frames too short for a substep move the pin with no relaxation, launch or not, and the next substep fixes it.
                float rest = Length(c) - Vector3.Distance(c.Positions[0], c.Positions[1]);
                Assert.LessOrEqual(rest, VerletChain.TotalLength * 8f / 9f + 0.1f);
                foreach (Vector3 p in c.Positions) Assert.GreaterOrEqual(p.y, 0f);
            }
        }

        [TestCase(0f, 1.4f, 1.5f)]
        [TestCase(0f, 1.4f, 3f)]
        [TestCase(0f, 1.4f, 4f)]
        [TestCase(1.2f, 1.4f, 3f)]
        [TestCase(-2f, 1.4f, 2.8f)]
        [TestCase(2.2f, 1.3f, 3.2f)]
        public void Steer_BringsTheTipThere_AtCrackSpeed_WithoutATeleport(float x, float y, float z)
        {
            var c = Settled(90);
            var target = new Vector3(x, y, z);
            Vector3 was = c.Positions[9];
            c.Launch(Vector3.forward, true, target);
            Assert.IsTrue(c.Steering);
            bool arrived = false;
            for (int f = 0; f < 90 && !arrived; f++)
            {
                c.Tick(Pin, Dt, 0f);
                Vector3 tip = c.Positions[9];
                Assert.LessOrEqual(Vector3.Distance(tip, was), VerletChain.MaxPointSpeed * Dt * 1.5f, "bends over the flight, never jumps");
                was = tip;
                bool inZone = Vector3.Distance(tip, target) < 0.45f || Vector3.Distance(c.Positions[8], target) < 0.45f; // 0.3 m body + BodyCheckRadius
                arrived = inZone && (c.InFlight || c.Velocity(9).magnitude >= WhipController.CrackSpeed);
            }
            Assert.IsTrue(arrived, "the crack zone gets there in the flight, or fast enough to crack");
        }

        [TestCase(0f, 1.4f, 2f)]
        [TestCase(0f, 1.4f, 3.2f)]
        [TestCase(-2f, 1.4f, 2.8f)]
        public void Steer_ThenTheBrake_NeverFliesOverThePlayer_AndComesBack(float x, float y, float z)
        {
            var c = Settled(90);
            var target = new Vector3(x, y, z);
            c.Launch(Vector3.forward, true, target);
            bool arrived = false;
            for (int f = 0; f < 90 * 4; f++)
            {
                c.Tick(Pin, Dt, 0f);
                Vector3 tip = c.Positions[9];
                Assert.Less(tip.y, 3f, "frame " + f);
                Assert.Greater(tip.z, -0.5f, "frame " + f);
                if (!arrived && Vector3.Distance(tip, target) < 0.45f) { arrived = true; c.EndSteer(); }
            }
            Assert.That(c.Positions[9].z, Is.GreaterThan(2.8f), "back out ahead");
            Assert.That(c.Positions[9].y, Is.InRange(0.9f, 2f));
        }

        [Test]
        public void Steer_Expires_AndPinningTheTipCancelsAFlight()
        {
            var c = Settled(90, 1f);
            c.SteerTo(new Vector3(1f, 1f, 2f));
            for (int f = 0; f < 45; f++) c.Tick(Pin, Dt, 0f);
            Assert.IsFalse(c.Steering, "about 0.35 s");

            c.Launch(Vector3.forward, true, new Vector3(1f, 1f, 2f));
            Assert.IsTrue(c.InFlight);
            c.PinTip(new Vector3(0f, 1f, 2f));
            Assert.IsFalse(c.InFlight);
            Assert.IsFalse(c.Steering, "no launch or steer under a lasso");
            c.UnpinTip();
        }

        [Test]
        public void Chain_LaunchSteerAndLift_AllocateNothing()
        {
            var c = Settled(90, 0.1f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 500; f++)
            {
                if (f % 50 == 0) c.Launch(Vector3.forward, f % 100 == 0, Vector3.one);
                c.SetLiftDirection(Vector3.forward);
                c.Tick(Pin, Dt, 0f);
            }
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // --- on the rig ---

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
            head = new GameObject("Head").transform;
            head.SetParent(rig.transform);
            head.position = new Vector3(0f, 1.6f, 0f);
            vr.Head = head;
            whip.transform.SetParent(rig.transform, false);
            whip.transform.localPosition = Pin;
            whip.transform.rotation = Quaternion.identity;
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

        private Enemy MakeEnemy(Vector3 at, float hp = 100f)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = hp;
            var go = new GameObject("feel-enemy", typeof(BoxCollider));
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            made.Add(go); made.Add(def);
            enemy.Configure(def, 0f);
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

        private void Steps(float seconds, float dt = Dt)
        {
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < n; i++) whip.Step(dt);
        }

        private void Rest(float seconds = 3f) => Steps(seconds);

        /// <summary>Moves the hand from where it is along <paramref name="velocity"/>. True if a launch began.</summary>
        private bool Move(Vector3 velocity, float seconds, float dt = Dt)
        {
            bool launched = false;
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < n; i++)
            {
                whip.transform.localPosition += velocity * dt;
                whip.Step(dt);
                launched |= whip.Chain.InFlight;
            }
            return launched;
        }

        private LassoGesture Gesture => (LassoGesture)typeof(WhipController).GetField("lassoGesture", NonPublic).GetValue(whip);

        [Test]
        public void Launch_AForwardFlickThrowsIt_NoButtons()
        {
            Rest();
            Assert.IsTrue(Move(Vector3.forward * 4f, 0.2f));
            Vector3 tip = whip.Chain.Positions[9];
            Assert.Greater(tip.z, 3f);
        }

        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void Launch_OncePerFlick_ThenRearms(int hz)
        {
            float dt = 1f / hz;
            Steps(1f, dt);
            Assert.IsTrue(Move(Vector3.forward * 4f, 0.15f, dt));
            whip.transform.localPosition = Pin;
            Move(Vector3.zero, VerletChain.LaunchFlightSeconds + 0.05f, dt);
            Assert.IsFalse(whip.Chain.InFlight);
            Assert.IsFalse(Move(Vector3.forward * 4f, 0.1f, dt), "still re-arming");
            Move(Vector3.zero, WhipController.LaunchRearmSeconds, dt);
            Assert.IsTrue(Move(Vector3.forward * 4f, 0.15f, dt), "re-armed");
        }

        [Test]
        public void Launch_NotFromASlowPush_ASideSweep_OrWalking()
        {
            Rest();
            Assert.IsFalse(Move(Vector3.forward * 1.5f, 0.5f), "slow");
            whip.transform.localPosition = Pin;
            Rest(0.2f);
            Assert.IsFalse(Move(Vector3.right * 5f, 0.3f), "a sideways slash is a slash");
            whip.transform.localPosition = Pin;
            Rest(0.2f);
            for (int i = 0; i < 40; i++)
            {
                rig.transform.position += Vector3.forward * 0.1f; // 9 m/s of rig, the hand still in it
                whip.Step(Dt);
                Assert.IsFalse(whip.Chain.InFlight, "walking");
            }
        }

        [TestCase(2f, 0.3f, 1.5f)]   // overhead
        [TestCase(1.4f, 0.3f, 1.5f)] // shoulder height
        [TestCase(1.4f, 0.2f, 2.5f)] // tight and fast
        [TestCase(1f, 0.3f, 1.5f)]   // below the shoulder: no lasso, still not a launch
        public void Launch_ASpinNeverLaunches(float y, float radius, float revs)
        {
            Rest(1f);
            for (int i = 0; i < 1.5f / Dt; i++)
            {
                float a = i * Dt * revs * 2f * Mathf.PI;
                whip.transform.localPosition = new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
                whip.Step(Dt);
                Assert.IsFalse(whip.Chain.InFlight, "frame " + i);
            }
        }

        [Test]
        public void Lasso_AFlickNeverArmsIt_AndSpinThenThrowStillGrabs()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Rest(1f);
            Assert.IsTrue(Move(Vector3.forward * 5f, 0.25f));
            Assert.IsFalse(Gesture.Armed);
            Assert.IsFalse(whip.LassoBusy, "a flick is not a lasso");

            whip.transform.localPosition = Pin;
            Rest(1f);
            Vector3 hand = Vector3.zero;
            for (int i = 0; i < 1.3f / Dt; i++)
            {
                float a = i * Dt * 1.5f * 2f * Mathf.PI;
                hand = new Vector3(Mathf.Sin(a) * 0.3f, 2f, Mathf.Cos(a) * 0.3f);
                whip.transform.localPosition = hand;
                whip.Step(Dt);
            }
            Assert.IsTrue(Gesture.Armed);
            for (int i = 1; i < 20 && !whip.LassoBusy; i++)
            {
                whip.transform.localPosition = hand + Vector3.forward * (6f * i * Dt);
                whip.Step(Dt);
            }
            Assert.AreSame(e, whip.Captive);
            Assert.IsFalse(whip.Chain.InFlight, "the throw is the lasso's, not a launch");
            Assert.IsFalse(whip.Chain.Steering);
        }

        [Test]
        public void Launch_DoesNothingWhileALassoRuns()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 4f));
            Assert.IsTrue(whip.TryBeginLasso(head.position, Vector3.forward));
            Steps(0.2f);
            Assert.IsFalse(Move(Vector3.forward * 5f, 0.3f));
            Assert.IsFalse(whip.Chain.Steering);
            Assert.IsTrue(H(e).IsAlive);
        }

        // --- crack through the launch ---

        [Test]
        public void Crack_NotAtRest_EvenWithTheTipInside_ButAFlickLands()
        {
            Rest();
            var e = MakeEnemy(whip.Chain.Positions[9] - Vector3.up);
            Rest(1f);
            Assert.AreEqual(100f, H(e).Current, 1e-3f, "resting in the enemy deals nothing");
            Assert.IsTrue(Move(Vector3.forward * 4f, 0.15f));
            Rest(0.3f);
            Assert.Less(H(e).Current, 100f);
            Assert.AreEqual(25f, 100f - H(e).Current, 4f * 3f, "25 from the crack, plus body feedback at most");
        }

        [TestCase(0f, 2f)]
        [TestCase(0f, 3.5f)]
        [TestCase(1.5f, 3f)]
        [TestCase(-1.5f, 2.5f)]
        public void Crack_ASteeredLaunchLandsOnTheMarkedEnemy(float x, float z)
        {
            var e = MakeEnemy(new Vector3(x, 0f, z));
            Rest();
            Assert.AreSame(H(e), whip.MarkedTarget);
            float cracks = 0f;
            Action<EntityDamaged> onDamaged = d => { if (d.Info.Type == DamageType.Melee) cracks += d.Info.Amount; };
            EventBus.Subscribe(onDamaged);
            try
            {
                Assert.IsTrue(Move(Vector3.forward * 4f, 0.15f));
                Rest(0.5f);
            }
            finally { EventBus.Unsubscribe(onDamaged); }
            Assert.AreEqual(WhipController.CrackDamage, cracks, 1e-3f, "one crack, the real path");
            Assert.IsFalse(whip.Chain.Steering, "it arrived: the steer has ended");
        }

        [Test]
        public void Crack_AnEnemyOutOfReachIsNotHit_EvenSteered()
        {
            head.position = new Vector3(0f, 1.6f, 0f);
            var e = MakeEnemy(new Vector3(0f, 0f, 4.4f)); // marked, but 4.4 m from the hand with a 0.3 m body
            whip.transform.localPosition = new Vector3(0f, HandY, -0.5f);
            Rest();
            Assert.AreSame(H(e), whip.MarkedTarget);
            whip.Chain.Launch(Vector3.forward, true, e.transform.position + Vector3.up * WhipController.MarkFallbackHeight);
            Rest(0.5f);
            Assert.AreEqual(100f, H(e).Current, 1e-3f, "the tip must arrive: 4 m of chain, 4.9 m away");
        }

        // --- marker ---

        [Test]
        public void Mark_PicksTheEnemyNearestTheAim_NotTheNearest()
        {
            var close = MakeEnemy(new Vector3(1.2f, 0f, 2f));
            var onAim = MakeEnemy(new Vector3(0.1f, 0f, 4f));
            whip.Step(Dt);
            Steps(WhipController.MarkInterval * 2f);
            Assert.AreSame(H(onAim), whip.MarkedTarget);
            Assert.IsTrue(whip.MarkerVisible);
            Assert.IsTrue(H(close).IsAlive);
        }

        [Test]
        public void Mark_RangeAndCone()
        {
            var far = MakeEnemy(new Vector3(0f, 0f, WhipController.MarkRange + 0.2f));
            var wide = MakeEnemy(new Vector3(3f, 0f, 1f));
            var behind = MakeEnemy(new Vector3(0f, 0f, -2f));
            Steps(0.2f);
            Assert.IsNull(whip.MarkedTarget);
            Assert.IsFalse(whip.MarkerVisible);

            far.transform.position = new Vector3(0f, 0f, WhipController.MarkRange - 0.2f);
            Steps(0.2f);
            Assert.AreSame(H(far), whip.MarkedTarget);
            Assert.IsTrue(H(wide).IsAlive && H(behind).IsAlive);
        }

        [Test]
        public void Mark_SitsOnTheUpperBody_TowardTheEye()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 3f));
            Steps(0.2f);
            Vector3 p = whip.MarkerPosition;
            Assert.AreEqual(WhipController.MarkFallbackHeight, p.y, 0.1f);
            Assert.Less(p.z, 3f, "in front of the body");
            Assert.AreEqual(1, whip.transform.Find("WhipAimMarker").GetComponents<MeshRenderer>().Length);
            Assert.AreSame(whip.GetComponentInChildren<LineRenderer>(true).sharedMaterial, whip.transform.Find("WhipAimMarker").GetComponent<MeshRenderer>().sharedMaterial, "no new material");
            Assert.IsNull(whip.transform.Find("WhipAimMarker").GetComponent<Collider>());
            Assert.IsTrue(H(e).IsAlive);
        }

        [Test]
        public void Mark_DoesNotFlicker_BetweenNeighbours_OrAtTheEdge()
        {
            var a = MakeEnemy(new Vector3(-0.5f, 0f, 3f));
            var b = MakeEnemy(new Vector3(0.5f, 0f, 3f));
            a.transform.position = new Vector3(-0.3f, 0f, 3f);
            Steps(0.2f);
            Assert.AreSame(H(a), whip.MarkedTarget);
            for (int i = 0; i < 60; i++)
            {
                a.transform.position = new Vector3(-0.3f - Mathf.Abs(Mathf.Sin(i * 0.2f)) * 0.3f, 0f, 3f); // now b is a little nearer the aim
                Steps(0.05f);
                Assert.AreSame(H(a), whip.MarkedTarget, "frame " + i);
            }
            a.transform.position = new Vector3(-2f, 0f, 3f); // clearly off
            Steps(0.2f);
            Assert.AreSame(H(b), whip.MarkedTarget);

            // Just past the cone's edge it holds; well past, it lets go.
            float edge = Mathf.Tan((WhipController.MarkHalfAngle + 2f) * Mathf.Deg2Rad) * 3f;
            Object.DestroyImmediate(a.gameObject);
            b.transform.position = new Vector3(edge, 0f, 3f);
            Steps(0.2f);
            Assert.AreSame(H(b), whip.MarkedTarget);
            b.transform.position = new Vector3(3f, 0f, 1f);
            Steps(0.2f);
            Assert.IsNull(whip.MarkedTarget);
        }

        [Test]
        public void Mark_HiddenWhenTheTargetDies_ThePlayerDies_OrThePauseMenuIsUp()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 3f), 10f);
            Steps(0.2f);
            Assert.IsTrue(whip.MarkerVisible);

            H(e).ApplyDamage(new DamageInfo(100f, Vector3.zero, Vector3.up, null, DamageType.Melee, 0f));
            whip.Step(Dt);
            Assert.IsFalse(whip.MarkerVisible, "dead enemy, same frame");

            var other = MakeEnemy(new Vector3(0f, 0f, 3f));
            Steps(0.2f);
            Assert.IsTrue(whip.MarkerVisible);
            Time.timeScale = 0f;
            whip.Step(Dt);
            Assert.IsFalse(whip.MarkerVisible, "paused");
            Time.timeScale = 1f;

            var player = rig.AddComponent<Health>();
            player.Configure(10f);
            typeof(Health).GetMethod("Awake", NonPublic).Invoke(player, null);
            Steps(0.2f);
            Assert.IsTrue(whip.MarkerVisible);
            player.ApplyDamage(new DamageInfo(100f, Vector3.zero, Vector3.up, null, DamageType.Melee, 0f));
            Steps(0.2f);
            Assert.IsFalse(whip.MarkerVisible, "dead player");
            Assert.IsTrue(H(other).IsAlive);
        }

        [Test]
        public void Mark_HiddenWhenUnheld_AndWhileALassoRuns()
        {
            var e = MakeEnemy(new Vector3(0f, 0f, 3f));
            Steps(0.2f);
            Assert.IsTrue(whip.MarkerVisible);

            whip.SetHeld(false);
            Assert.IsFalse(whip.MarkerVisible);
            Assert.IsNull(whip.MarkedTarget);
            whip.SetHeld(true);
            whip.Step(Dt);
            Steps(0.2f);
            Assert.IsTrue(whip.MarkerVisible);

            Assert.IsTrue(whip.TryBeginLasso(head.position, Vector3.forward));
            whip.Step(Dt);
            Assert.IsFalse(whip.MarkerVisible, "not during the lasso");
            Assert.AreSame(e, whip.Captive);
        }

        [Test]
        public void Mark_OnlyEnemies_NeverTheRigOrAProp()
        {
            var prop = new GameObject("Prop", typeof(BoxCollider), typeof(Health));
            made.Add(prop);
            prop.transform.position = new Vector3(0f, 0f, 3f);
            var h = prop.GetComponent<Health>();
            typeof(Health).GetMethod("Awake", NonPublic).Invoke(h, null);
            typeof(Health).GetMethod("OnEnable", NonPublic).Invoke(h, null);
            Steps(0.2f);
            Assert.IsNull(whip.MarkedTarget);
        }

        [Test]
        public void WarmedFrame_WithMarkerLaunchAndSteer_AllocatesNothing()
        {
            MakeEnemy(new Vector3(0f, 0f, 3.5f));
            MakeEnemy(new Vector3(1f, 0f, 3.5f));
            Rest(1f);
            Move(Vector3.forward * 4f, 0.1f); // warm every path once
            Rest(0.6f);
            whip.transform.localPosition = Pin;
            Rest(0.6f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int f = 0; f < 14; f++) { whip.transform.localPosition += Vector3.forward * (4f * Dt); whip.Step(Dt); }
                whip.transform.localPosition = Pin;
                for (int f = 0; f < 60; f++) whip.Step(Dt);
            }
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, used);
        }
    }
}

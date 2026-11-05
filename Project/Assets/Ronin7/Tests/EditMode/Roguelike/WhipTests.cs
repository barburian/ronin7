using System;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.EditorTools;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-02d (A15.8, A15.10, A15.11, A15.12, A15.14, A15.18): the verlet chain, the body hit, the handle and the Armory rules.</summary>
    public class WhipTests
    {
        private const float NoFloor = -100f;

        private static float ChainLength(VerletChain c)
        {
            float sum = 0f;
            for (int i = 1; i < c.Count; i++) sum += Vector3.Distance(c.Positions[i], c.Positions[i - 1]);
            return sum;
        }

        // A hand swinging hard enough to be a real whip stroke: about 8 m/s.
        private static Vector3 SwingPin(float t) => new Vector3(Mathf.Sin(t * 6f) * 0.9f, 1.3f + Mathf.Cos(t * 6f) * 0.5f, 0f);

        private static VerletChain Swing(int hz, float seconds, float floorY)
        {
            var c = new VerletChain();
            c.Reset(SwingPin(0f), Vector3.forward, floorY);
            int frames = Mathf.RoundToInt(seconds * hz);
            for (int f = 1; f <= frames; f++) c.Tick(SwingPin(f / (float)hz), 1f / hz, floorY);
            return c;
        }

        private static void AssertFinite(VerletChain c)
        {
            foreach (var p in c.Positions)
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z), p.ToString());
        }

        // --- chain ---

        [TestCase(72)]
        [TestCase(90)]
        [TestCase(120)]
        public void Chain_HoldsItsLength_WhileSwinging(int hz)
        {
            var c = new VerletChain();
            c.Reset(SwingPin(0f), Vector3.forward, NoFloor);
            for (int f = 1; f <= hz * 5; f++)
            {
                c.Tick(SwingPin(f / (float)hz), 1f / hz, NoFloor);
                Assert.AreEqual(VerletChain.TotalLength, ChainLength(c), 0.2f, "frame " + f);
                Assert.AreEqual(SwingPin(f / (float)hz), c.Positions[0], "the first point stays pinned");
            }
            AssertFinite(c);
        }

        [Test]
        public void Chain_PointCount_And_Reach_MatchA15()
        {
            Assert.AreEqual(10, VerletChain.DefaultPoints);
            Assert.AreEqual(4f, VerletChain.TotalLength);
            var c = new VerletChain();
            c.Reset(Vector3.up * 50f, Vector3.forward, 0f);
            Assert.AreEqual(4f, Vector3.Distance(c.Positions[0], c.Positions[9]), 1e-3f, "hanging free it reaches 4 m");
        }

        [Test]
        public void Chain_NeverGoesThroughTheFloor()
        {
            var c = new VerletChain();
            c.Reset(SwingPin(0f), Vector3.forward, 0f);
            for (int f = 1; f <= 90 * 5; f++)
            {
                c.Tick(SwingPin(f / 90f), 1f / 90f, 0f);
                for (int i = 1; i < c.Count; i++) Assert.GreaterOrEqual(c.Positions[i].y, 0f, "point " + i);
            }
        }

        [Test]
        public void Chain_Reset_HasNoSwingSpike_AndSettlesBelowSwingSpeed()
        {
            var c = new VerletChain();
            c.Reset(new Vector3(0f, 1.2f, 0f), Vector3.forward, 0f);
            Assert.AreEqual(0f, c.MaxSpeed);
            for (int f = 0; f < 90 * 2; f++)
            {
                c.Tick(new Vector3(0f, 1.2f, 0f), 1f / 90f, 0f);
                Assert.Less(c.MaxSpeed, WhipController.SwingSpeed, "still hand, chain at rest: frame " + f);
            }

            c = Swing(90, 3f, 0f);
            Assert.Greater(c.MaxSpeed, WhipController.SwingSpeed, "a real swing reads as moving");
            Vector3 still = SwingPin(3f);
            for (int f = 0; f < 90 * 8; f++) c.Tick(still, 1f / 90f, 0f);
            Assert.Less(c.MaxSpeed, WhipController.SwingSpeed, "stopping the hand ends the swing");
        }

        [Test]
        public void Chain_SurvivesDtSpikes()
        {
            var c = new VerletChain();
            c.Reset(SwingPin(0f), Vector3.forward, 0f);
            float t = 0f;
            foreach (float dt in new[] { 0.5f, 0f, 2f, 1f / 90f, 10f, -1f, 0.2f, 1f / 90f, 1f / 90f, 30f, 1f / 90f })
            {
                t += Mathf.Max(0f, dt);
                c.Tick(SwingPin(t), dt, 0f);
                AssertFinite(c);
                Assert.LessOrEqual(ChainLength(c), VerletChain.TotalLength + 0.2f, "dt " + dt);
                Assert.LessOrEqual(c.MaxSpeed, VerletChain.MaxPointSpeed * 3f, "dt " + dt);
            }
        }

        [Test]
        public void Chain_TrackingJump_CarriesTheChain_WithoutASwing()
        {
            var c = new VerletChain();
            var a = new Vector3(0f, 1.2f, 0f);
            c.Reset(a, Vector3.forward, 0f);
            for (int f = 0; f < 90; f++) c.Tick(a, 1f / 90f, 0f);
            var b = new Vector3(40f, 1.2f, -25f);
            c.Tick(b, 1f / 90f, 0f);
            Assert.Less(c.MaxSpeed, WhipController.SwingSpeed, "a teleport is not a swing");
            Assert.LessOrEqual(Vector3.Distance(c.Positions[c.Count - 1], b), VerletChain.TotalLength + 0.01f);
            AssertFinite(c);
        }

        [Test]
        public void Chain_MovesAlike_At72_90_120Hz()
        {
            Vector3 tip90 = Swing(90, 4f, NoFloor).Positions[9];
            float d72 = Vector3.Distance(Swing(72, 4f, NoFloor).Positions[9], tip90);
            float d120 = Vector3.Distance(Swing(120, 4f, NoFloor).Positions[9], tip90);
            Assert.Less(d72, 0.4f);
            Assert.Less(d120, 0.4f);
        }

        [Test]
        public void Chain_TickAllocatesNothing()
        {
            var c = new VerletChain();
            c.Reset(SwingPin(0f), Vector3.forward, 0f);
            c.Tick(SwingPin(0.01f), 1f / 90f, 0f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 2; f < 2000; f++) c.Tick(SwingPin(f / 90f), 1f / 90f, 0f);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // --- WhipController ---

        private GameObject rig;
        private WeaponDefinition def;
        private WhipController whip;
        private GameObject victim;

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            rig = new GameObject("Rig");
            XRRigBuilder.RogueEnsureWhip(rig.transform);
            var gear = rig.GetComponent<PolearmShieldGear>();
            whip = gear.whip.GetComponent<WhipController>();
            def = gear.whip.GetComponent<Sword>().Definition;
            victim = null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            MetaProgression.ResetForTests();
            Object.DestroyImmediate(rig);
            if (victim != null) Object.DestroyImmediate(victim);
        }

        private Health MakeVictim(Vector3 at, Vector3 size)
        {
            victim = new GameObject("Victim", typeof(BoxCollider), typeof(Health));
            victim.transform.position = at;
            victim.GetComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
            var h = victim.GetComponent<Health>();
            h.Configure(100f);
            return h;
        }

        private void DragRig(int frames, float metresPerFrame)
        {
            for (int f = 0; f < frames; f++)
            {
                rig.transform.position += Vector3.right * metresPerFrame;
                Physics.SyncTransforms();
                whip.Step(1f / 90f);
            }
        }

        private LineRenderer Line => whip.GetComponentInChildren<LineRenderer>(true);
        private GameObject Coil => whip.transform.Find("WhipVisual/Coil").gameObject;

        [Test]
        public void Gear_IsIdempotent_InactiveOnTheHip_OneLine_NoExtraColliders()
        {
            Assert.IsFalse(XRRigBuilder.RogueEnsureWhip(rig.transform), "idempotent");
            var gear = rig.GetComponent<PolearmShieldGear>();
            Assert.IsTrue(gear.WhipComplete);
            Assert.IsFalse(gear.whip.gameObject.activeSelf);
            Assert.AreEqual(gear.whipAnchor, gear.whip.transform.parent);
            Assert.AreEqual(rig.transform, gear.whipAnchor.parent);
            Assert.Greater(gear.whipAnchor.localPosition.x, 0f, "main-hand hip; SetMirrored flips it");

            var line = Line;
            Assert.AreEqual(1, whip.GetComponentsInChildren<LineRenderer>(true).Length);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, line.shadowCastingMode);
            Assert.IsFalse(line.receiveShadows);
            Assert.AreEqual(UnityEngine.Rendering.LightProbeUsage.Off, line.lightProbeUsage);
            Assert.AreEqual(UnityEngine.Rendering.ReflectionProbeUsage.Off, line.reflectionProbeUsage);
            Assert.IsNotNull(line.sharedMaterial, "the shared chain material");
            Assert.IsNull(line.GetComponent<Collider>(), "the chain has no collider");
            Assert.IsNull(line.GetComponent<BladeDamager>());
            Assert.AreEqual(2, gear.whip.GetComponentsInChildren<Collider>(true).Length, "grip + handle only");
            Assert.AreEqual(1, gear.whip.GetComponentsInChildren<BladeDamager>(true).Length);
            Assert.IsNotNull(gear.whip.GetComponentInChildren<HeldOnlyBlade>(true));
        }

        [Test]
        public void Handle_DealsNothing_CannotParry_IsNotAShield_AndWeaponArtsAreOff()
        {
            Assert.AreEqual("Assets/Ronin7/Data/WhipHandle.asset", AssetDatabase.GetAssetPath(def));
            Assert.AreEqual(WeaponId.Whip, def.id);
            Assert.AreEqual(0f, def.minDamage);
            Assert.AreEqual(0f, def.maxDamage);
            Assert.AreEqual(0f, def.thrustDamage);
            Assert.IsFalse(def.canParry);

            var blade = whip.GetComponentInChildren<BladeDamager>(true);
            blade.SetDefinition(def);
            Assert.IsFalse(blade.CanParry);
            Assert.IsFalse(blade.IsShield);
            Assert.AreEqual(0f, blade.SwingDamageNow());
            Assert.IsFalse(WeaponId.AllowsWeaponArt(WeaponId.Whip));
            Assert.IsFalse(WeaponId.ParryAvailable(WeaponId.Whip), "A15.10: ParryGate reads Neutral on a Whip run");
        }

        [TestCase("GripCollider")]
        [TestCase("Blade")]
        public void Bolt_PassesThroughTheHandle_WielderUnharmed(string colliderName)
        {
            var health = rig.AddComponent<Health>();
            health.Configure(10f);
            var boltGo = new GameObject("Bolt", typeof(Projectile));
            try
            {
                whip.gameObject.SetActive(true);
                var col = whip.transform.Find(colliderName).GetComponent<Collider>();
                var blade = whip.GetComponentInChildren<BladeDamager>(true);
                blade.SetDefinition(def);
                var bolt = boltGo.GetComponent<Projectile>();
                typeof(Projectile).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bolt, 3f);
                bool consumed = (bool)typeof(Projectile).GetMethod("TryHit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bolt, new object[] { col, Vector3.zero, Vector3.forward });
                Assert.IsFalse(consumed);
                Assert.AreEqual(10f, health.Current, 1e-4f);
            }
            finally { Object.DestroyImmediate(boltGo); }
        }

        [Test]
        public void Holstered_ShowsTheCoil_Held_ShowsTheChain()
        {
            whip.SetHeld(false);
            Assert.IsFalse(Line.enabled);
            Assert.IsTrue(Coil.activeSelf);

            whip.SetHeld(true);
            Assert.IsTrue(Line.enabled);
            Assert.IsFalse(Coil.activeSelf);
            Assert.AreEqual(VerletChain.DefaultPoints, Line.positionCount);

            whip.SetHeld(false);
            Assert.IsFalse(Line.enabled);
            Assert.IsTrue(Coil.activeSelf);
        }

        [Test]
        public void Grab_ResetsAtTheHand_WithoutASwingSpike()
        {
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            rig.transform.position = new Vector3(30f, 0f, 30f); // handle moved between holster and hand
            whip.SetHeld(false);
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            var ring = whip.transform.Find("ChainRing");
            Assert.AreEqual(ring.position, whip.Chain.Positions[0]);
            Assert.Less(whip.Chain.MaxSpeed, WhipController.SwingSpeed);
            Assert.AreEqual(0, whip.LastChecks, "no checks on the grab frame");
        }

        [Test]
        public void BodyHit_Is4_Status_AndHasAPerEnemyReHitCooldown()
        {
            var h = MakeVictim(new Vector3(0f, 1f, 0f), Vector3.one);
            var col = victim.GetComponent<Collider>();
            DamageInfo got = default;
            int impacts = 0;
            Action<EntityDamaged> onDamaged = e => { if (e.Entity == victim) got = e.Info; };
            Action<SwordImpact> onImpact = e => impacts++;
            EventBus.Subscribe(onDamaged);
            EventBus.Subscribe(onImpact);
            try
            {
                whip.SetHeld(true);
                Assert.IsTrue(whip.TryBodyHit(col, Vector3.up, Vector3.right, 10f));
                Assert.AreEqual(96f, h.Current, 1e-4f);
                Assert.AreEqual(DamageType.Status, got.Type);
                Assert.AreEqual(4f, got.Amount, 1e-4f);
                Assert.AreSame(rig, got.Source);
                Assert.AreEqual(1, impacts, "feedback event");

                Assert.IsFalse(whip.TryBodyHit(col, Vector3.up, Vector3.right, 10f), "R6: not twice inside the cooldown");
                Assert.AreEqual(96f, h.Current, 1e-4f);
                Assert.AreEqual(1, impacts);

                StepFor(WhipController.ReHitCooldown - 0.1f);
                Assert.IsFalse(whip.TryBodyHit(col, Vector3.up, Vector3.right, 10f), "still inside the cooldown");
                StepFor(0.2f);
                Assert.IsTrue(whip.TryBodyHit(col, Vector3.up, Vector3.right, 10f), "R6: again once it has passed, with no speed reset");
                Assert.AreEqual(92f, h.Current, 1e-4f);
            }
            finally { EventBus.Unsubscribe(onDamaged); EventBus.Unsubscribe(onImpact); }
        }

        // Steps the (at rest) chain so the controller's real-time clock advances by about this long.
        private void StepFor(float seconds)
        {
            int n = Mathf.CeilToInt(seconds * 90f);
            for (int i = 0; i < n; i++) whip.Step(1f / 90f);
        }

        [Test]
        public void BodyHit_CooldownIsPerEnemy()
        {
            var a = MakeVictim(new Vector3(0f, 1f, 0f), Vector3.one);
            var colA = victim.GetComponent<Collider>();
            var firstVictim = victim;
            var b = MakeVictim(new Vector3(5f, 1f, 0f), Vector3.one);
            var colB = victim.GetComponent<Collider>();
            try
            {
                whip.SetHeld(true);
                Assert.IsTrue(whip.TryBodyHit(colA, Vector3.up, Vector3.right, 10f));
                Assert.IsTrue(whip.TryBodyHit(colB, Vector3.up, Vector3.right, 10f), "another enemy is independent");
                Assert.IsFalse(whip.TryBodyHit(colA, Vector3.up, Vector3.right, 10f));
                Assert.AreEqual(96f, a.Current, 1e-4f);
                Assert.AreEqual(96f, b.Current, 1e-4f);
                whip.SetHeld(false);
                whip.SetHeld(true);
                Assert.AreEqual(0, whip.StruckCount, "release clears the table");
            }
            finally { Object.DestroyImmediate(firstVictim); }
        }

        [Test]
        public void BodyHit_NonstopSwinging_KeepsHitting()
        {
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(10f, 3f, 8f)); // wide: the chain stays inside it
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            DragRig(100, 0.08f); // 7 m/s for 1.1 s, never dropping below SwingSpeed
            Assert.Greater(whip.Chain.MaxSpeed, WhipController.SwingSpeed);
            Assert.LessOrEqual(h.Current, 92f, "R6: a second hit after 0.5 s of continuous swing");
        }

        [Test]
        public void BodyHit_IsFeedbackOnly_BuildsNoOverdriveOrCombo_AndDoesNotResetThem()
        {
            Assert.IsFalse(new SwordImpact(Vector3.zero, 5f, null, Vector3.zero).FeedbackOnly, "a normal impact still credits");

            var go = new GameObject("Credit", typeof(OverdriveController), typeof(ComboMomentumController));
            var enemy = new GameObject("E");
            try
            {
                var od = go.GetComponent<OverdriveController>();
                var combo = go.GetComponent<ComboMomentumController>();
                const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(OverdriveController).GetField("logic", F).SetValue(od, new OverdriveLogic(0.1f, 0.1f, 0.5f));
                var odImpact = typeof(OverdriveController).GetMethod("OnSwordImpact", F);
                var comboImpact = typeof(ComboMomentumController).GetMethod("OnSwordImpact", F);
                var comboState = typeof(ComboMomentumController).GetField("state", F);
                var normal = new SwordImpact(Vector3.zero, 5f, enemy, Vector3.zero);
                var chip = new SwordImpact(Vector3.zero, 5f, enemy, Vector3.zero, true);

                odImpact.Invoke(od, new object[] { normal });
                float charge = od.Charge;
                Assert.Greater(charge, 0f, "normal hit charges");
                odImpact.Invoke(od, new object[] { chip });
                Assert.AreEqual(charge, od.Charge, "chip adds none");

                comboImpact.Invoke(combo, new object[] { normal });
                Assert.AreEqual(1, ((ComboState)comboState.GetValue(combo)).Count);
                comboImpact.Invoke(combo, new object[] { chip }); // same target: a normal hit would reset the chain to 0
                Assert.AreEqual(1, ((ComboState)comboState.GetValue(combo)).Count, "chip neither extends nor resets");
                comboImpact.Invoke(combo, new object[] { normal });
                Assert.AreEqual(0, ((ComboState)comboState.GetValue(combo)).Count, "a normal same-target hit still resets");
            }
            finally { Object.DestroyImmediate(enemy); Object.DestroyImmediate(go); }
        }

        [Test]
        public void Step_SkipsBodyChecks_WhileTheHandleBladeIsDisabled()
        {
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(1f, 3f, 8f));
            whip.GetComponentInChildren<BladeDamager>(true).enabled = false; // Grabber.GuardHeldBlade during tracking loss
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            DragRig(60, 0.08f);
            Assert.AreEqual(100f, h.Current, 1e-4f);
            Assert.AreEqual(0, whip.LastChecks);
        }

        [Test]
        public void BodyHit_SkipsTheRig_AndAColliderWithoutHealth()
        {
            var own = rig.AddComponent<Health>();
            own.Configure(50f);
            whip.SetHeld(true);
            Assert.IsFalse(whip.TryBodyHit(whip.transform.Find("GripCollider").GetComponent<Collider>(), Vector3.zero, Vector3.right, 10f));
            Assert.AreEqual(50f, own.Current, 1e-4f);

            victim = new GameObject("Wall", typeof(BoxCollider));
            Assert.IsFalse(whip.TryBodyHit(victim.GetComponent<Collider>(), Vector3.zero, Vector3.right, 10f));
        }

        [Test]
        public void BodyHit_AppliesTheMultiplier_WithoutSpendingRiposteOrQuickDraw()
        {
            var h = MakeVictim(Vector3.up, Vector3.one);
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            mods.OverdriveMultiplier = 2f;
            int asked = 0;
            mods.RiposteBonus = (GameObject v, out float p) => { asked++; p = 1f; return 5f; };
            mods.QuickDrawBonus = (GameObject v, out float p) => { asked++; p = 1f; return 5f; };
            whip.SetHeld(true);
            whip.TryBodyHit(victim.GetComponent<Collider>(), Vector3.up, Vector3.right, 10f);
            Assert.AreEqual(92f, h.Current, 1e-4f, "4 x 2");
            Assert.AreEqual(0, asked, "those bonuses are spent by asking for them");
            Assert.AreNotEqual(mods.LastRiposteVictim, victim);
        }

        [Test]
        public void Step_OverlapsThePoints_WhileMoving_ThenStopsChecking()
        {
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(10f, 3f, 8f));
            float status = 0f;
            Action<EntityDamaged> onDamaged = e => { if (e.Info.Type == DamageType.Status) status += e.Info.Amount; };
            EventBus.Subscribe(onDamaged);
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            Assert.AreEqual(0, whip.LastChecks, "at rest: no checks");

            try { DragRig(100, 0.08f); } // 7 m/s through the box
            finally { EventBus.Unsubscribe(onDamaged); }
            Assert.Less(h.Current, 100f, "the swing hits");
            // P9-02e: this drag also snaps the tip past the crack speed (25, Melee); the body hits are the Status ones.
            Assert.Greater(status, 0f);
            Assert.AreEqual(0f, status % 4f, 1e-3f, "4 per hit");
            Assert.AreEqual(VerletChain.DefaultPoints - 1, whip.LastChecks, "one check per unpinned point");
            Assert.AreEqual(1, whip.StruckCount);

            DragRig(90 * 8, 0f);
            Assert.AreEqual(0, whip.LastChecks);
        }

        [Test]
        public void Step_DoesNotDependOnTheTimeScale()
        {
            Vector3 Run(float scale)
            {
                Time.timeScale = scale;
                rig.transform.position = Vector3.zero;
                whip.SetHeld(false);
                whip.SetHeld(true);
                whip.Step(1f / 90f);
                DragRig(60, 0.08f);
                return whip.Chain.Positions[whip.Chain.Count - 1];
            }
            Vector3 full = Run(1f);
            Vector3 overdrive = Run(0.3f);
            Assert.AreEqual(0f, Vector3.Distance(full, overdrive), 1e-4f);
        }

        [Test]
        public void Step_WhilePaused_ChecksNothing()
        {
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(1f, 3f, 8f));
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            Time.timeScale = 0f;
            DragRig(60, 0.08f);
            Assert.AreEqual(100f, h.Current, 1e-4f);
            Assert.AreEqual(0, whip.LastChecks);
        }

        private void SetCut(string field, int value, bool isBool = false)
        {
            var so = new SerializedObject(whip);
            if (isBool) so.FindProperty(field).boolValue = value != 0;
            else so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Cuts_FewerPoints_And_NoBodyDamage()
        {
            SetCut("chainPoints", 6);
            SetCut("bodyDamage", 0, true);
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(1f, 3f, 8f));
            whip.SetHeld(true);
            Assert.AreEqual(6, whip.Chain.Count);
            Assert.AreEqual(6, Line.positionCount);
            whip.Step(1f / 90f);
            DragRig(60, 0.08f);
            Assert.AreEqual(100f, h.Current, 1e-4f);
            Assert.AreEqual(0, whip.LastChecks);
            Assert.AreEqual(4f, ChainLength(whip.Chain), 0.25f, "same reach on fewer points");
        }

        [Test]
        public void Step_WhileSwinging_AllocatesNothing()
        {
            MakeVictim(new Vector3(500f, 500f, 500f), Vector3.one);
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            DragRig(5, 0.08f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 200; f++)
            {
                rig.transform.position += Vector3.right * 0.08f;
                whip.Step(1f / 90f);
            }
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(VerletChain.DefaultPoints - 1, whip.LastChecks);
            Assert.AreEqual(0L, used);
        }

        [Test]
        public void Step_WhenAHitLands_AllocatesNothing()
        {
            var h = MakeVictim(new Vector3(3f, 1f, 0f), new Vector3(10f, 3f, 8f));
            whip.SetHeld(true);
            whip.Step(1f / 90f);
            DragRig(10, 0.08f); // warm-up: the first hit lands and primes the tables
            Assert.Less(h.Current, 100f, "warm-up hit landed");
            h.Configure(1000000f);
            float before = h.Current;
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            int frames = 0;
            while (h.Current >= before && frames++ < 400) // runs until the cooldown reopens inside the box and a hit lands
            {
                rig.transform.position += Vector3.right * 0.08f;
                Physics.SyncTransforms();
                whip.Step(1f / 90f);
            }
            long used = GC.GetAllocatedBytesForCurrentThread() - a0;
            Assert.Less(h.Current, before, "a second hit landed inside the measured window");
            Assert.AreEqual(0L, used, "hit frames allocate nothing");
        }

        // --- Armory (A15.12) ---

        [Test]
        public void Purchase_IsRefused_NoEchoPrice_NothingSpent()
        {
            MetaProgression.AddEchoes(5000);
            int echoes = MetaProgression.Echoes;
            Assert.AreEqual(0, MetaProgression.UpgradeCost(WeaponId.Whip, 1));
            Assert.IsFalse(MetaProgression.TryPurchase(WeaponId.Whip));
            Assert.AreEqual(echoes, MetaProgression.Echoes);
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.IsFalse(MetaProgression.TrySelectLoadout(WeaponId.Whip));
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
        }

        [Test]
        public void DevUnlock_SelectsTheWhip_InMemoryOnly()
        {
            MetaProgression.DevUnlockAllWeapons = true;
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.Whip));
            Assert.AreEqual(WeaponId.Whip, MetaProgression.SelectedLoadout);
            MetaProgression.DevUnlockAllWeapons = false;
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
        }

        [Test]
        public void ArmoryButton_OnlyWorksWhenUnlocked_ForTheWhip()
        {
            int whipCost = MetaProgression.UpgradeCost(WeaponId.Whip, 1);
            Assert.IsFalse(MetaShopPanel.LoadoutInteractable(false, 99999, whipCost), "locked: never for sale");
            Assert.IsTrue(MetaShopPanel.LoadoutInteractable(true, 0, whipCost), "dev unlock selects");
            Assert.IsTrue(MetaShopPanel.LoadoutInteractable(false, 300, 300), "priced weapons still buy");
            Assert.IsFalse(MetaShopPanel.LoadoutInteractable(false, 299, 300));
            Assert.AreEqual("WHIP\nLOCKED", MetaShopPanel.LoadoutText("WHIP", false, false, whipCost));
        }
    }
}

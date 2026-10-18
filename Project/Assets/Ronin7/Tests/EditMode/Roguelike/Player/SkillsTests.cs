using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P4-04: pure logic of the eight skills (S1-S8), the Phase Step / Mirror fixes and the Art modal gate.</summary>
    public class SkillsTests
    {
        private GameObject a, b;

        [SetUp]
        public void SetUp()
        {
            a = new GameObject("A");
            b = new GameObject("B");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            ArtSlots.ModalOpen = null;
            TimeScaleArbiter.ResetForTests();
            Time.timeScale = 1f;
        }

        // ---- S1 Riposte ----
        [Test]
        public void Riposte_MatchingTargetInWindow()
        {
            var r = new RiposteLogic();
            r.Arm(a, 10f);
            Assert.AreEqual(1.75f, r.Consume(a, 11f, out float posture), 1e-5f);
            Assert.AreEqual(2f, posture, 1e-5f);
        }

        [Test]
        public void Riposte_OtherTarget_None_AndKeepsTheRiposte()
        {
            var r = new RiposteLogic();
            r.Arm(a, 10f);
            Assert.AreEqual(1f, r.Consume(b, 10.5f, out _));
            Assert.AreEqual(1.75f, r.Consume(a, 10.6f, out _), 1e-5f);
        }

        [Test]
        public void Riposte_AfterWindow_None()
        {
            var r = new RiposteLogic();
            r.Arm(a, 10f);
            Assert.AreEqual(1f, r.Consume(a, 11.3f, out float posture));
            Assert.AreEqual(1f, posture);
        }

        [Test]
        public void Riposte_ConsumedOnFirstHit()
        {
            var r = new RiposteLogic();
            r.Arm(a, 10f);
            r.Consume(a, 10.1f, out _);
            Assert.AreEqual(1f, r.Consume(a, 10.2f, out _));
        }

        [Test]
        public void Riposte_AppliesInsideBoonGroup_UnderItsCap()
        {
            var mods = a.AddComponent<PlayerCombatModifiers>();
            mods.BoonMultiplier = 1.5f;
            var r = new RiposteLogic();
            r.Arm(b, 0f);
            mods.RiposteBonus = (GameObject v, out float p) => r.Consume(v, 0.1f, out p);
            Assert.AreEqual(2f, mods.DamageMultiplierFor(b, out float posture), 1e-5f, "1.5 x 1.75 capped at the 2x boon cap");
            Assert.AreEqual(2f, posture, 1e-5f);
            Assert.AreEqual(1.5f, mods.DamageMultiplierFor(b, out _), 1e-5f, "consumed");
        }

        // ---- S2 Quick Draw ----
        [Test]
        public void QuickDraw_FastDraw_Primes()
        {
            var q = new QuickDrawLogic();
            q.BeginDraw(0f);
            Assert.IsTrue(q.Tick(5f, 0.2f));
            Assert.AreEqual(2.5f, q.Consume(0.4f), 1e-5f);
        }

        [Test]
        public void QuickDraw_SlowDraw_No()
        {
            var q = new QuickDrawLogic();
            q.BeginDraw(0f);
            q.Tick(3f, 0.1f);
            Assert.IsFalse(q.Tick(6f, 0.3f), "fast too late");
            Assert.AreEqual(1f, q.Consume(0.35f));
        }

        [Test]
        public void QuickDraw_Spent_NeedsResheathe()
        {
            var q = new QuickDrawLogic();
            q.BeginDraw(0f);
            q.Tick(5f, 0.1f);
            q.Consume(0.2f);
            q.BeginDraw(1f); // still drawn: no re-arm
            Assert.AreEqual(QuickDrawState.Spent, q.State);
            q.Resheathe();
            q.BeginDraw(2f);
            Assert.IsTrue(q.Tick(5f, 2.1f));
        }

        [Test]
        public void QuickDraw_Expires_0_6()
        {
            var q = new QuickDrawLogic();
            q.BeginDraw(0f);
            q.Tick(5f, 0.1f);
            Assert.AreEqual(1f, q.Consume(0.61f));
            Assert.AreEqual(QuickDrawState.Spent, q.State);
        }

        // ---- S3 Zanshin ----
        [Test]
        public void Zanshin_CooldownBlocks_AndHasteShortensIt()
        {
            var z = new ZanshinLogic();
            Assert.IsTrue(z.TryTrigger(0f, 1f));
            Assert.IsFalse(z.TryTrigger(4.9f, 1f));
            Assert.IsTrue(z.TryTrigger(5f, 1f));
            var h = new ZanshinLogic();
            h.TryTrigger(0f, PlayerCombatModifiers.CooldownScale(0.4f));
            Assert.IsTrue(h.TryTrigger(3f, 1f), "40% haste: 3 s cooldown");
        }

        [Test]
        public void Zanshin_ExpiresAfter0_6Real()
        {
            var z = new ZanshinLogic();
            z.TryTrigger(0f, 1f);
            Assert.IsFalse(z.Expired(0.59f));
            Assert.IsTrue(z.Expired(0.6f));
        }

        [Test]
        public void Zanshin_ArbitratesWithDeflectAndWeaknessTeach()
        {
            var c = new TimeScaleComposition();
            c.SetRequest(TimeScaleChannel.WeaknessTeach, 0.5f);
            c.SetRequest(TimeScaleChannel.Zanshin, ZanshinLogic.Scale);
            c.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);
            Assert.AreEqual(0.25f, c.EffectiveScale, 1e-5f);
            c.ClearRequest(TimeScaleChannel.DeflectSlowMo); // deflect ends first: Zanshin keeps holding
            Assert.AreEqual(0.4f, c.EffectiveScale, 1e-5f);
            c.ClearRequest(TimeScaleChannel.Zanshin);
            Assert.AreEqual(0.5f, c.EffectiveScale, 1e-5f);
        }

        [Test]
        public void Zanshin_ReleasesOnDisable()
        {
            TimeScaleArbiter.ResetForTests();
            var z = a.AddComponent<ZanshinController>(); // EditMode: no lifecycle callbacks run on their own
            z.OnPerfectParry(new PerfectParry(Vector3.zero, b, 1f));
            Assert.AreEqual(0.4f, TimeScaleArbiter.EffectiveScale, 1e-5f);
            typeof(ZanshinController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(z, null);
            Assert.AreEqual(1f, TimeScaleArbiter.EffectiveScale, 1e-5f);
        }

        // ---- S4 Charged Slash ----
        [Test]
        public void Charge_EarlyRelease_No()
        {
            var c = new ChargeLogic();
            c.Begin(0f);
            Assert.IsFalse(c.Release(0.7f, 9f, 1f));
            c.Begin(0.8f); // no cooldown was spent
            Assert.IsTrue(c.Charging);
        }

        [Test]
        public void Charge_SlowSwing_No()
        {
            var c = new ChargeLogic();
            c.Begin(0f);
            Assert.IsFalse(c.Release(1f, 4.9f, 1f));
        }

        [Test]
        public void Charge_Fire_Cooldown()
        {
            var c = new ChargeLogic();
            c.Begin(0f);
            Assert.IsTrue(c.Release(1f, 6f, 1f));
            c.Begin(6.9f);
            Assert.IsFalse(c.Charging);
            c.Begin(7f);
            Assert.IsTrue(c.Charging);
        }

        [Test]
        public void Charge_Haste()
        {
            var c = new ChargeLogic();
            c.Begin(0f);
            c.Release(1f, 6f, PlayerCombatModifiers.CooldownScale(0.4f)); // 6 s -> 3.6 s
            c.Begin(4.61f);
            Assert.IsTrue(c.Charging);
        }

        [Test]
        public void Charge_DirectionLiesInSwingPlane()
        {
            Vector3 d = ChargeLogic.SlashDirection(new Vector3(0f, 0.5f, 1f), Vector3.forward, Vector3.right);
            Assert.AreEqual(0f, d.y, 1e-4f, "horizontal swing -> horizontal wave");
            Assert.Greater(d.z, 0.99f);
        }

        // ---- S5 Blade Throw ----
        [Test]
        public void Throw_ReleaseNoTrigger_Resheathes()
        {
            Assert.IsFalse(ThrowLogic.ShouldThrow(false, 9f));
            Assert.IsFalse(ThrowLogic.ShouldThrow(true, 3.9f));
        }

        [Test]
        public void Throw_FastWithTrigger_Throws()
        {
            Assert.IsTrue(ThrowLogic.ShouldThrow(true, 4f));
            Assert.AreEqual(9f, ThrowLogic.ThrowVelocity(new Vector3(6f, 0f, 0f)).magnitude, 1e-4f);
        }

        [Test]
        public void Throw_Recall_WithinDuration()
        {
            Vector3 from = new Vector3(0f, 0f, 5f), hand = Vector3.zero;
            Assert.IsFalse(ThrowLogic.RecallDone(0.3f));
            Assert.AreEqual(2.5f, ThrowLogic.RecallPosition(from, hand, 0.175f).z, 1e-4f);
            Assert.IsTrue(ThrowLogic.RecallDone(0.35f));
            Assert.AreEqual(hand, ThrowLogic.RecallPosition(from, hand, 0.5f));
        }

        // ---- S6 Kiai Palm ----
        [Test]
        public void Kiai_BelowSpeed_No()
        {
            Assert.IsFalse(new KiaiLogic().TryFire(3.9f, 0f, 1f));
        }

        [Test]
        public void Kiai_RadiusOnly()
        {
            Assert.IsTrue(KiaiLogic.InRadius(Vector3.zero, new Vector3(2.5f, 0f, 0f)));
            Assert.IsFalse(KiaiLogic.InRadius(Vector3.zero, new Vector3(2.6f, 0f, 0f)));
            Assert.AreEqual(0.4f, KiaiLogic.ShoveDelta(Vector3.zero, new Vector3(1f, 1f, 0f)).magnitude, 1e-5f);
        }

        [Test]
        public void Kiai_Cooldown()
        {
            var k = new KiaiLogic();
            Assert.IsTrue(k.TryFire(5f, 0f, 1f));
            Assert.IsFalse(k.TryFire(5f, 7.9f, 1f));
            Assert.IsTrue(k.TryFire(5f, 8f, 1f));
        }

        // ---- S7 Chain Hook ----
        [Test]
        public void Chain_YankSlow_No()
        {
            Vector3 hand = new Vector3(0f, 1.2f, 0.6f), head = new Vector3(0f, 1.6f, 0f);
            Assert.IsFalse(ChainLogic.IsYank(new Vector3(0f, 0f, -2f), hand, head, Vector3.forward));
            Assert.IsTrue(ChainLogic.IsYank(new Vector3(0f, 0f, -3f), hand, head, Vector3.forward));
            Assert.IsFalse(ChainLogic.IsYank(new Vector3(3f, 0f, 0f), hand, head, Vector3.forward), "sideways is not a yank");
        }

        [Test]
        public void Chain_PullClampedToStandoff()
        {
            Assert.AreEqual(3f, ChainLogic.PullDelta(Vector3.zero, new Vector3(0f, 0f, 7f)).magnitude, 1e-4f);
            Vector3 near = ChainLogic.PullDelta(Vector3.zero, new Vector3(0f, 0f, 2.5f));
            Assert.AreEqual(1.5f, near.magnitude, 1e-4f, "stops at 1 m");
            Assert.AreEqual(Vector3.zero, ChainLogic.PullDelta(Vector3.zero, new Vector3(0f, 0f, 0.8f)));
        }

        // ---- S8 Element Imbue ----
        [Test]
        public void Imbue_BaseToTip_Triggers()
        {
            var i = new ImbueLogic();
            Assert.IsFalse(i.Tick(0.1f, 0.05f, true, 0f));
            Assert.IsFalse(i.Tick(0.5f, 0.05f, true, 0.2f));
            Assert.IsTrue(i.Tick(0.9f, 0.05f, true, 0.4f));
        }

        [Test]
        public void Imbue_TipToBase_No_AndTooSlow_No()
        {
            var i = new ImbueLogic();
            Assert.IsFalse(i.Tick(0.9f, 0.05f, true, 0f));
            Assert.IsFalse(i.Tick(0.1f, 0.05f, true, 0.2f));
            var slow = new ImbueLogic();
            slow.Tick(0.1f, 0.05f, true, 0f);
            Assert.IsFalse(slow.Tick(0.9f, 0.05f, true, 0.6f));
            Assert.AreEqual(0.5f, ImbueLogic.Along(new Vector3(0.1f, 0f, 0.5f), Vector3.zero, Vector3.forward, out float d), 1e-5f);
            Assert.AreEqual(0.1f, d, 1e-5f);
        }

        [Test]
        public void Imbue_Expires_AndPicksOwnedElement()
        {
            var i = new ImbueLogic();
            i.Begin(0f);
            Assert.IsTrue(i.Active(7.9f));
            Assert.IsFalse(i.Expired(7.9f));
            Assert.IsTrue(i.Expired(8f));
            Assert.IsFalse(i.Active(8f));
            Assert.AreEqual(ImbueElement.None, ImbueLogic.Pick(ImbueElement.None, ImbueElement.None));
            Assert.AreEqual(ImbueElement.Storm, ImbueLogic.Pick(ImbueElement.Storm, ImbueElement.Storm));
            Assert.AreEqual(ImbueElement.Storm, ImbueLogic.Pick(ImbueElement.Bleed | ImbueElement.Storm, ImbueElement.Bleed));
            var mods = a.AddComponent<PlayerCombatModifiers>();
            Assert.AreEqual(1f, mods.ProcRateScale);
            mods.Imbued = ImbueElement.Bleed;
            Assert.AreEqual(2f, mods.ProcRateScale);
        }

        // ---- Fixes and gate ----
        [Test]
        public void PhaseStep_StickPastDeadzone_ElseHeadForward()
        {
            Vector3 fwd = new Vector3(0f, -0.3f, 1f), right = Vector3.right;
            Assert.AreEqual(Vector3.forward, PhaseStepSolver.BlinkDirection(new Vector2(0.2f, 0f), fwd, right, 0.3f));
            Vector3 d = PhaseStepSolver.BlinkDirection(new Vector2(1f, 0f), fwd, right, 0.3f);
            Assert.AreEqual(1f, d.x, 1e-5f);
        }

        [Test]
        public void PhaseStep_IFrames_0_2s()
        {
            Assert.IsFalse(PhaseStepSolver.IsInvulnerable(1f, -1f));
            Assert.IsTrue(PhaseStepSolver.IsInvulnerable(1.19f, 1f));
            Assert.IsFalse(PhaseStepSolver.IsInvulnerable(1.2f, 1f));
        }

        [Test]
        public void Mirror_AllyDamage_ScalesWithDepthAndBoons()
        {
            Assert.AreEqual(8f, MirrorSummonController.AllyDamage(1f, 1f), 1e-5f);
            Assert.AreEqual(8f * 1.5f * 1.2f, MirrorSummonController.AllyDamage(1.5f, 1.2f), 1e-4f);
        }

        [Test]
        public void ArtSlots_ModalGate_BlocksFire()
        {
            RunState.Reset();
            int fired = 0;
            ArtSlots.Register("test.modal", () => fired++);
            ArtSlots.Equip(ArtSlot.Weapon, "test.modal");
            try
            {
                ArtSlots.ModalOpen = () => true;
                Assert.IsFalse(ArtSlots.TryFire(ArtSlot.Weapon, true, false));
                ArtSlots.ModalOpen = () => false;
                Assert.IsTrue(ArtSlots.TryFire(ArtSlot.Weapon, true, false));
                Assert.AreEqual(1, fired);
            }
            finally
            {
                ArtSlots.Unregister("test.modal");
                RunState.Reset();
            }
        }
    }
}

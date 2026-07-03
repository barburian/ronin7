using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    public class OverdriveLogicTests
    {
        private static OverdriveLogic Make(float chargePerHit = 0.25f, float drainPerSecond = 0.5f, float threshold = 1f)
        {
            return new OverdriveLogic(chargePerHit, drainPerSecond, threshold);
        }

        [Test]
        public void InitialState_ChargeZero_NotActive()
        {
            var logic = Make();
            Assert.AreEqual(0f, logic.Charge);
            Assert.IsFalse(logic.IsActive);
        }

        [Test]
        public void AddCharge_BuildsTowardThreshold()
        {
            var logic = Make(chargePerHit: 0.25f);
            logic.AddCharge();
            Assert.AreEqual(0.25f, logic.Charge, 1e-5f);
            logic.AddCharge();
            Assert.AreEqual(0.5f, logic.Charge, 1e-5f);
        }

        [Test]
        public void AddCharge_NonPositiveHits_IsIgnored()
        {
            var logic = Make();
            logic.AddCharge(0f);
            logic.AddCharge(-1f);
            Assert.AreEqual(0f, logic.Charge);
        }

        [Test]
        public void AddCharge_ClampsAtOne()
        {
            var logic = Make(chargePerHit: 0.9f);
            logic.AddCharge();
            logic.AddCharge();
            Assert.AreEqual(1f, logic.Charge);
        }

        [Test]
        public void TryActivate_BelowThreshold_Fails()
        {
            var logic = Make(chargePerHit: 0.25f, threshold: 1f);
            logic.AddCharge();
            Assert.IsFalse(logic.TryActivate());
            Assert.IsFalse(logic.IsActive);
        }

        [Test]
        public void TryActivate_AtThreshold_Succeeds()
        {
            var logic = Make(chargePerHit: 0.5f, threshold: 1f);
            logic.AddCharge();
            logic.AddCharge();
            Assert.IsTrue(logic.TryActivate());
            Assert.IsTrue(logic.IsActive);
        }

        [Test]
        public void TryActivate_AboveThreshold_Succeeds()
        {
            var logic = Make(chargePerHit: 1f, threshold: 0.5f);
            logic.AddCharge();
            Assert.IsTrue(logic.TryActivate());
        }

        [Test]
        public void TryActivate_WhileAlreadyActive_Fails()
        {
            var logic = Make(chargePerHit: 1f, threshold: 0.5f);
            logic.AddCharge();
            Assert.IsTrue(logic.TryActivate());
            Assert.IsFalse(logic.TryActivate(), "A second activation attempt while already active must fail.");
        }

        [Test]
        public void AddCharge_WhileActive_IsIgnored()
        {
            var logic = Make(chargePerHit: 0.5f, threshold: 0.5f);
            logic.AddCharge();
            logic.TryActivate();
            logic.AddCharge(); // should not build charge mid-burst
            Assert.AreEqual(0.5f, logic.Charge, 1e-5f);
        }

        [Test]
        public void Tick_DrainsChargeWhileActive()
        {
            var logic = Make(chargePerHit: 1f, drainPerSecond: 0.5f, threshold: 1f);
            logic.AddCharge();
            logic.TryActivate();
            logic.Tick(1f);
            Assert.AreEqual(0.5f, logic.Charge, 1e-5f);
            Assert.IsTrue(logic.IsActive);
        }

        [Test]
        public void Tick_ChargeReachesZero_AutoEndsAndReturnsTrue()
        {
            var logic = Make(chargePerHit: 1f, drainPerSecond: 0.5f, threshold: 1f);
            logic.AddCharge();
            logic.TryActivate();
            bool ended = logic.Tick(5f); // far more drain than remaining charge
            Assert.IsTrue(ended);
            Assert.IsFalse(logic.IsActive);
            Assert.AreEqual(0f, logic.Charge);
        }

        [Test]
        public void Tick_PartialDrain_DoesNotEndYet()
        {
            var logic = Make(chargePerHit: 1f, drainPerSecond: 0.25f, threshold: 1f);
            logic.AddCharge();
            logic.TryActivate();
            bool ended = logic.Tick(1f);
            Assert.IsFalse(ended);
            Assert.IsTrue(logic.IsActive);
        }

        [Test]
        public void Tick_WhileNotActive_ReturnsFalseAndNoOp()
        {
            var logic = Make();
            Assert.IsFalse(logic.Tick(1f));
            Assert.AreEqual(0f, logic.Charge);
        }

        [Test]
        public void Tick_NonPositiveDt_IsIgnored()
        {
            var logic = Make(chargePerHit: 1f, drainPerSecond: 0.5f, threshold: 1f);
            logic.AddCharge();
            logic.TryActivate();
            logic.Tick(0f);
            logic.Tick(-1f);
            Assert.AreEqual(1f, logic.Charge);
            Assert.IsTrue(logic.IsActive);
        }

        [Test]
        public void Deactivate_ForcesInactive_Idempotent()
        {
            var logic = Make(chargePerHit: 1f, threshold: 0.5f);
            logic.AddCharge();
            logic.TryActivate();
            logic.Deactivate();
            Assert.IsFalse(logic.IsActive);
            logic.Deactivate(); // idempotent, no throw
            Assert.IsFalse(logic.IsActive);
        }
    }
}

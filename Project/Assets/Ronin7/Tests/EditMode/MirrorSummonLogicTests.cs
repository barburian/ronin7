using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    public class MirrorSummonLogicTests
    {
        private static MirrorSummonLogic Make(float activeDuration = 15f, float cooldownSeconds = 30f)
        {
            return new MirrorSummonLogic(activeDuration, cooldownSeconds);
        }

        [Test]
        public void InitialState_NotActive_CanSummon()
        {
            var logic = Make();
            Assert.IsFalse(logic.IsActive);
            Assert.IsTrue(logic.CanSummon);
        }

        [Test]
        public void TrySummon_WhenAllowed_StartsActiveWindow()
        {
            var logic = Make();
            Assert.IsTrue(logic.TrySummon());
            Assert.IsTrue(logic.IsActive);
            Assert.IsFalse(logic.CanSummon);
        }

        [Test]
        public void TrySummon_WhileAlreadyActive_Fails()
        {
            var logic = Make();
            Assert.IsTrue(logic.TrySummon());
            Assert.IsFalse(logic.TrySummon(), "A second summon attempt while already active must fail.");
        }

        [Test]
        public void TrySummon_DuringCooldownAfterDespawn_Fails()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();
            logic.Tick(5f); // active window ends; 5s of cooldown remains
            Assert.IsFalse(logic.IsActive);
            Assert.IsFalse(logic.CanSummon, "Cooldown remainder after despawn must still block a re-summon.");
            Assert.IsFalse(logic.TrySummon());
        }

        [Test]
        public void Tick_ActiveWindowExpires_ReturnsTrueOnce()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();

            Assert.IsFalse(logic.Tick(3f), "Active window has not elapsed yet.");
            Assert.IsTrue(logic.IsActive);

            Assert.IsTrue(logic.Tick(2f), "Active window should end exactly as remaining time elapses.");
            Assert.IsFalse(logic.IsActive);

            Assert.IsFalse(logic.Tick(1f), "Tick must not report 'just ended' twice for the same summon.");
        }

        [Test]
        public void Tick_OverdrainsActiveWindow_EndsExactlyOnce()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();
            Assert.IsTrue(logic.Tick(999f), "A large dt spanning well past the active window must still end it exactly once.");
            Assert.IsFalse(logic.IsActive);
        }

        [Test]
        public void CanSummon_TrueAgain_OnceFullCooldownElapses()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();
            logic.Tick(5f); // active window ends, 5s cooldown remains
            Assert.IsFalse(logic.CanSummon);

            logic.Tick(4.9f);
            Assert.IsFalse(logic.CanSummon, "Cooldown has not fully elapsed yet.");

            logic.Tick(0.2f);
            Assert.IsTrue(logic.CanSummon, "Cooldown should be clear once the full duration has elapsed.");
        }

        [Test]
        public void Tick_NonPositiveDt_IsIgnored()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();
            Assert.IsFalse(logic.Tick(0f));
            Assert.IsFalse(logic.Tick(-1f));
            Assert.IsTrue(logic.IsActive, "A non-positive dt must not advance the active window.");
        }

        [Test]
        public void ForceEnd_EndsActiveWindow_WithoutTouchingCooldown()
        {
            var logic = Make(activeDuration: 15f, cooldownSeconds: 30f);
            logic.TrySummon();
            logic.ForceEnd();

            Assert.IsFalse(logic.IsActive);
            Assert.IsFalse(logic.CanSummon, "ForceEnd is a despawn failsafe, not a cooldown reset — the full cooldown still applies.");
        }

        [Test]
        public void ForceEnd_Idempotent_NoThrow()
        {
            var logic = Make();
            logic.ForceEnd();
            Assert.DoesNotThrow(() => logic.ForceEnd());
            Assert.IsFalse(logic.IsActive);
        }

        [Test]
        public void ReSummon_AfterFullCooldown_StartsNewActiveWindow()
        {
            var logic = Make(activeDuration: 5f, cooldownSeconds: 10f);
            logic.TrySummon();
            logic.Tick(10f); // active window ends, full cooldown elapses too

            Assert.IsTrue(logic.CanSummon);
            Assert.IsTrue(logic.TrySummon());
            Assert.IsTrue(logic.IsActive);
        }
    }
}

using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The pure attack/hold/decay envelope core of DamageAlertLighting: ramp up, hold at peak, ramp
    /// back down, and the zero-length-phase guards. All side-effect free — no scene/MonoBehaviour
    /// instance needed.
    /// </summary>
    public class DamageAlertLightingTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Negative_ReturnsZero()
        {
            Assert.AreEqual(0f, DamageAlertLighting.DecayEnvelope(-1f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void AtZero_WithPositiveAttack_ReturnsZero()
        {
            Assert.AreEqual(0f, DamageAlertLighting.DecayEnvelope(0f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void MidAttack_RampsLinearly()
        {
            // Halfway through a 0.1s attack should be at half intensity.
            Assert.AreEqual(0.5f, DamageAlertLighting.DecayEnvelope(0.05f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void DuringHold_ReturnsOne()
        {
            Assert.AreEqual(1f, DamageAlertLighting.DecayEnvelope(0.2f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void MidDecay_RampsDownLinearly()
        {
            // attack=0.1, hold=0.25 -> decay starts at 0.35; halfway through a 1.5s decay is 0.35+0.75=1.1.
            Assert.AreEqual(0.5f, DamageAlertLighting.DecayEnvelope(1.1f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void AfterTotalDuration_ReturnsZero()
        {
            Assert.AreEqual(0f, DamageAlertLighting.DecayEnvelope(10f, 0.1f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void ZeroAttackTime_JumpsToOneImmediately()
        {
            Assert.AreEqual(1f, DamageAlertLighting.DecayEnvelope(0f, 0f, 0.25f, 1.5f), Eps);
        }

        [Test]
        public void ZeroDecayTime_DropsToZeroRightAfterHold()
        {
            // attack + hold = 0.35; with no decay, the envelope is already 0 at that instant.
            Assert.AreEqual(0f, DamageAlertLighting.DecayEnvelope(0.35f, 0.1f, 0.25f, 0f), Eps);
        }

        [Test]
        public void StaysWithinZeroToOneRange()
        {
            for (float t = -1f; t <= 3f; t += 0.05f)
            {
                float v = DamageAlertLighting.DecayEnvelope(t, 0.1f, 0.25f, 1.5f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void NegativeAttackOrDecay_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => DamageAlertLighting.DecayEnvelope(0.5f, -1f, 0.25f, -1f));
        }
    }
}

using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// EP28 failsafe erosion pulse: covers the vision-degradation meter honoring a custom
    /// TriggerPulse(duration) override rather than always dividing by the configured pulseDuration.
    /// Mirrors CryoChillControllerTests conventions (AutoAdvance=false for deterministic ticking).
    /// </summary>
    public class FailsafeErosionPulseTests
    {
        private GameObject go;
        private FailsafeErosionPulse pulse;

        [SetUp]
        public void Setup()
        {
            go = new GameObject();
            pulse = go.AddComponent<FailsafeErosionPulse>();
            pulse.AutoAdvance = false; // deterministic: no Update()-driven ticks
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TriggerPulse_CustomDurationShorterThanConfigured_VisionStartsAtFullStrength()
        {
            // Default configured pulseDuration is 30s; trigger with a much shorter override.
            pulse.TriggerPulse(5f);

            Assert.AreEqual(1f, pulse.VisionDegradation, 0.001f);
        }

        [Test]
        public void Tick_PastCustomDuration_Deactivates()
        {
            pulse.TriggerPulse(5f);

            pulse.Tick(6f); // past the 5s custom duration

            Assert.IsFalse(pulse.IsActive);
            Assert.AreEqual(0f, pulse.VisionDegradation, 0.001f);
        }
    }
}

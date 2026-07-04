using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="BladeDamager.SmoothSpeed"/>, the pure exponential-moving-average step that
    /// feeds the swing-speed → damage mapping. The point is that a single frame-time/tracking spike
    /// (common on Quest at 72/90Hz) must not push the smoothed speed up to the raw instantaneous
    /// value, which would otherwise inflate damage.
    /// </summary>
    public class BladeDamagerTests
    {
        // Runs a sequence of (position, dt) samples through the smoothing step, exactly as FixedUpdate
        // does, and returns the final smoothed speed.
        private static float RunSequence(float smoothing, (Vector3 pos, float dt)[] samples)
        {
            float speed = 0f;
            Vector3 last = samples[0].pos;
            for (int i = 1; i < samples.Length; i++)
            {
                speed = BladeDamager.SmoothSpeed(last, samples[i].pos, samples[i].dt, speed, smoothing);
                last = samples[i].pos;
            }
            return speed;
        }

        [Test]
        public void SmoothSpeed_SpikeFrame_DoesNotReachRawInstantaneous()
        {
            const float dt = 0.02f;        // 50Hz fixed step
            const float step = 0.1f;       // 0.1 m / step => 5 m/s steady swing
            const float spikeDt = 0.002f;  // a stutter: same motion sampled over 1/10th the time

            float rawSpike = step / spikeDt; // 50 m/s instantaneous on the spike frame

            // Five steady samples to warm up the average, then one spike frame.
            var samples = new (Vector3, float)[]
            {
                (Vector3.right * 0.0f, dt),
                (Vector3.right * 0.1f, dt),
                (Vector3.right * 0.2f, dt),
                (Vector3.right * 0.3f, dt),
                (Vector3.right * 0.4f, dt),
                (Vector3.right * 0.5f, dt),
                (Vector3.right * 0.6f, spikeDt), // spike: 0.1 m over 0.002 s
            };

            float smoothed = RunSequence(0.5f, samples);

            Assert.Less(smoothed, rawSpike,
                "Smoothed speed must stay below the raw instantaneous spike.");
            Assert.Less(smoothed, 0.7f * rawSpike,
                "Smoothing must meaningfully attenuate a single-frame spike, not merely shave it.");
            Assert.Greater(smoothed, step / dt,
                "A real spike should still register above the steady swing speed (no full suppression).");
        }

        [Test]
        public void SmoothSpeed_ZeroSmoothing_ReturnsRawInstantaneous()
        {
            // smoothing = 0 is the original (un-smoothed) behaviour: speed == raw magnitude / dt.
            float speed = BladeDamager.SmoothSpeed(Vector3.zero, Vector3.right * 0.1f, 0.02f, 123f, 0f);
            Assert.That(speed, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void SmoothSpeed_SteadySwing_ConvergesToTrueSpeed()
        {
            // A sustained, even swing must still converge to its real speed so legitimate hits keep
            // dealing their proper damage — smoothing rejects spikes, it doesn't permanently bias.
            const float dt = 0.02f;
            const float step = 0.1f; // => 5 m/s
            var samples = new (Vector3, float)[20];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (Vector3.right * (step * i), dt);

            float smoothed = RunSequence(0.5f, samples);

            Assert.That(smoothed, Is.EqualTo(step / dt).Within(0.05f));
        }

        [Test]
        public void SmoothSpeed_NonPositiveDt_KeepsPreviousSpeed()
        {
            Assert.AreEqual(7f, BladeDamager.SmoothSpeed(Vector3.zero, Vector3.right, 0f, 7f, 0.5f));
            Assert.AreEqual(7f, BladeDamager.SmoothSpeed(Vector3.zero, Vector3.right, -0.01f, 7f, 0.5f));
        }

        // Guards the Ch7 weakpoint-sight damage-multiplier hook: absent an ability, the multiplier
        // must be a no-op (1) so every pre-Ch7 scene/test is unaffected.
        [Test]
        public void ApplyWielderMultiplier_DefaultMultiplierOfOne_LeavesDamageUnchanged()
        {
            Assert.AreEqual(15f, BladeDamager.ApplyWielderMultiplier(15f, 1f));
        }

        [Test]
        public void ApplyWielderMultiplier_WeakpointSightMultiplier_DoublesDamage()
        {
            Assert.AreEqual(30f, BladeDamager.ApplyWielderMultiplier(15f, 2f));
        }

        [Test]
        public void ApplyWielderMultiplier_ZeroDamage_StaysZero()
        {
            Assert.AreEqual(0f, BladeDamager.ApplyWielderMultiplier(0f, 2f));
        }

        // ---- TryRegisterHit (per-target hit debounce). ----
        // Guards the fix for a bug where a single global lastHitTime meant one swing could only ever
        // hit one enemy: every other target touched in the same swing was silently debounced away.

        [Test]
        public void TryRegisterHit_SameTargetWithinCooldown_ReturnsFalse()
        {
            var lastHitTimes = new Dictionary<Health, float>();
            var target = new GameObject("Target").AddComponent<Health>();

            Assert.IsTrue(BladeDamager.TryRegisterHit(lastHitTimes, target, 0f, 0.5f));
            Assert.IsFalse(BladeDamager.TryRegisterHit(lastHitTimes, target, 0.2f, 0.5f));

            Object.DestroyImmediate(target.gameObject);
        }

        [Test]
        public void TryRegisterHit_SameTargetAfterCooldown_ReturnsTrue()
        {
            var lastHitTimes = new Dictionary<Health, float>();
            var target = new GameObject("Target").AddComponent<Health>();

            Assert.IsTrue(BladeDamager.TryRegisterHit(lastHitTimes, target, 0f, 0.5f));
            Assert.IsTrue(BladeDamager.TryRegisterHit(lastHitTimes, target, 0.6f, 0.5f));

            Object.DestroyImmediate(target.gameObject);
        }

        [Test]
        public void TryRegisterHit_DifferentTargetsSameFrame_BothReturnTrue()
        {
            var lastHitTimes = new Dictionary<Health, float>();
            var targetA = new GameObject("TargetA").AddComponent<Health>();
            var targetB = new GameObject("TargetB").AddComponent<Health>();

            Assert.IsTrue(BladeDamager.TryRegisterHit(lastHitTimes, targetA, 1f, 0.5f));
            Assert.IsTrue(BladeDamager.TryRegisterHit(lastHitTimes, targetB, 1f, 0.5f));

            Object.DestroyImmediate(targetA.gameObject);
            Object.DestroyImmediate(targetB.gameObject);
        }
    }
}

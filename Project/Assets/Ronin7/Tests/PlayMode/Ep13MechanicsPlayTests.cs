using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP13 MirrorPhantom and CarouselHazard mechanics.
    /// </summary>
    public class Ep13MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        private static DamageInfo Hit(float amount) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, null);

        [Test]
        public void CarouselHazard_SpeedRampsAndClamps()
        {
            Assert.AreEqual(12f, CarouselHazard.SpeedAt(0f, 12f, 60f, 120f), 0.01f,
                "At t=0, speed should be minSpeed");

            Assert.AreEqual(36f, CarouselHazard.SpeedAt(60f, 12f, 60f, 120f), 0.01f,
                "At t=60 (halfway), speed should be halfway between min and max");

            Assert.AreEqual(60f, CarouselHazard.SpeedAt(120f, 12f, 60f, 120f), 0.01f,
                "At t=rampDuration, speed should be maxSpeed");

            Assert.AreEqual(60f, CarouselHazard.SpeedAt(240f, 12f, 60f, 120f), 0.01f,
                "After rampDuration, speed should remain clamped to maxSpeed");

            Assert.AreEqual(50f, CarouselHazard.SpeedAt(5f, 10f, 50f, 0f), 0.01f,
                "When ramp<=0, speed should immediately return maxSpeed");
        }

        [UnityTest]
        public IEnumerator MirrorPhantom_SequenceAdvancesOnDeath()
        {
            // Build 3 phantoms, each with Health.
            var phantoms = new List<Health>();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Phantom_{i}");
                _spawned.Add(go);
                var health = go.AddComponent<Health>();
                health.Configure(10f);
                phantoms.Add(health);
            }

            // Create the MirrorPhantom holder.
            var holderGo = new GameObject("MirrorPhantom_Holder");
            _spawned.Add(holderGo);
            var phantom = holderGo.AddComponent<MirrorPhantom>();

            // Reflectively set the phantoms list.
            typeof(MirrorPhantom)
                .GetField("phantoms", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(phantom, phantoms);

            // Re-enable to run Begin() with the wired phantoms.
            phantom.enabled = false;
            phantom.enabled = true;
            yield return null;

            // Verify initial state: ActiveIndex=0, only phantom[0] active.
            Assert.AreEqual(0, phantom.ActiveIndex, "Should start with ActiveIndex=0");
            Assert.IsTrue(phantoms[0].gameObject.activeSelf, "Phantom 0 should be active");
            Assert.IsFalse(phantoms[1].gameObject.activeSelf, "Phantom 1 should be inactive");
            Assert.IsFalse(phantoms[2].gameObject.activeSelf, "Phantom 2 should be inactive");

            // Kill phantom 0.
            phantoms[0].ApplyDamage(Hit(100f));
            yield return null;

            // Verify sequence advanced: ActiveIndex=1, phantom[1] active.
            Assert.AreEqual(1, phantom.ActiveIndex, "After phantom[0] dies, ActiveIndex should be 1");
            Assert.IsFalse(phantoms[0].gameObject.activeSelf, "Phantom 0 should be deactivated");
            Assert.IsTrue(phantoms[1].gameObject.activeSelf, "Phantom 1 should be active");
            Assert.IsFalse(phantoms[2].gameObject.activeSelf, "Phantom 2 should still be inactive");

            // Kill phantom 1.
            phantoms[1].ApplyDamage(Hit(100f));
            yield return null;

            // Verify final phantom is active and IsFinalChildActive is true.
            Assert.AreEqual(2, phantom.ActiveIndex, "After phantom[1] dies, ActiveIndex should be 2");
            Assert.IsTrue(phantom.IsFinalChildActive, "IsFinalChildActive should be true");
            Assert.IsTrue(phantoms[2].gameObject.activeSelf, "Phantom 2 should be active");

            // Subscribe to onSequenceCleared and kill the final phantom.
            int clearedCount = 0;
            phantom.onSequenceCleared.AddListener(() => clearedCount++);
            phantoms[2].ApplyDamage(Hit(100f));
            yield return null;

            // Verify onSequenceCleared fired exactly once.
            Assert.AreEqual(1, clearedCount, "onSequenceCleared should fire exactly once");

            // Verify killing again doesn't re-fire the event.
            phantoms[2].ApplyDamage(Hit(50f)); // Already dead, but try again.
            Assert.AreEqual(1, clearedCount, "onSequenceCleared must be one-shot");
        }
    }
}

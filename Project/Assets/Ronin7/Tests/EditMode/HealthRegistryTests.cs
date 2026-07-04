using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="Health.Active"/>, the static registry added so FactionCombatant/
    /// AllyCombatant/TalkInteractor/WeakpointSight can stop scanning the scene with
    /// FindObjectsByType every tick. Mirrors <c>EnemyShip.Active</c>/<c>Asteroid.Active</c>'s idiom.
    ///
    /// EditMode does not auto-invoke Awake/OnEnable/OnDisable on AddComponent/DestroyImmediate (same
    /// caveat as ProtectNpcObjectiveTests/UnbrokenWardTests), so lifecycle methods are driven directly
    /// via reflection.
    /// </summary>
    public class HealthRegistryTests
    {
        private GameObject _go;
        private Health _health;

        private static void Life(Health h, string method) =>
            typeof(Health).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(h, null);

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            // Defensive: guarantee no leakage into other tests regardless of what ran above.
            Health.Active.Clear();
        }

        [Test]
        public void OnEnable_AddsToActiveRegistry()
        {
            _go = new GameObject("HealthRegistryTarget");
            _health = _go.AddComponent<Health>();

            Life(_health, "OnEnable");

            Assert.IsTrue(Health.Active.Contains(_health));
        }

        [Test]
        public void OnEnable_CalledTwice_DoesNotDuplicate()
        {
            _go = new GameObject("HealthRegistryTarget");
            _health = _go.AddComponent<Health>();

            Life(_health, "OnEnable");
            Life(_health, "OnEnable");

            int count = 0;
            foreach (var h in Health.Active)
                if (h == _health) count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void OnDisable_RemovesFromActiveRegistry()
        {
            _go = new GameObject("HealthRegistryTarget");
            _health = _go.AddComponent<Health>();
            Life(_health, "OnEnable");

            Life(_health, "OnDisable");

            Assert.IsFalse(Health.Active.Contains(_health));
        }

        [Test]
        public void DestroyedComponent_NoLongerInActiveRegistry()
        {
            _go = new GameObject("HealthRegistryTarget");
            _health = _go.AddComponent<Health>();
            Life(_health, "OnEnable");
            Assert.IsTrue(Health.Active.Contains(_health));

            // Mirror Unity's real teardown order (OnDisable fires before OnDestroy on a live object).
            Life(_health, "OnDisable");
            Object.DestroyImmediate(_go);
            _go = null;

            Assert.IsFalse(Health.Active.Contains(_health));
        }
    }
}

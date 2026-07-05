using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the membership check added to SpaceEncounterManager.OnEnemyDestroyed:
    /// the wave counter must only decrement for ships that belong to THIS manager's current wave,
    /// mirroring the sibling guard in GuardEncounter.OnEnemyDestroyed. Before the fix,
    /// any <c>EnemyShipDestroyed</c> on the EventBus — even from an unrelated encounter — decremented
    /// this manager's counter.
    ///
    /// Drives the private method/fields directly via reflection (no OnEnable/EventBus subscription
    /// needed since OnEnemyDestroyed is invoked directly), matching the reflection idiom used by
    /// PostureMeterTests/DialoguePlayerTests.
    /// </summary>
    public class SpaceEncounterManagerOnEnemyDestroyedTests
    {
        private GameObject _managerGo;
        private GameObject _ownShipGo;
        private GameObject _foreignShipGo;

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            if (_ownShipGo != null) Object.DestroyImmediate(_ownShipGo);
            if (_foreignShipGo != null) Object.DestroyImmediate(_foreignShipGo);
            EventBus.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} field not found — update this test.");
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void InvokeOnEnemyDestroyed(SpaceEncounterManager manager, EnemyShipDestroyed evt)
        {
            var method = typeof(SpaceEncounterManager).GetMethod("OnEnemyDestroyed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "SpaceEncounterManager.OnEnemyDestroyed method not found — update this test.");
            method.Invoke(manager, new object[] { evt });
        }

        private EnemyShip MakeShip(string name, out GameObject go)
        {
            go = new GameObject(name);
            go.AddComponent<Health>();
            return go.AddComponent<EnemyShip>();
        }

        [Test]
        public void ForeignShipDestroyed_DoesNotDecrementAliveCount()
        {
            _managerGo = new GameObject("SpaceEncounterManagerTestTarget");
            var manager = _managerGo.AddComponent<SpaceEncounterManager>();

            var ownShip = MakeShip("OwnShip", out _ownShipGo);
            var alive = (List<EnemyShip>)GetField(manager, "alive");
            alive.Add(ownShip);
            SetField(manager, "aliveCount", 1);
            SetField(manager, "waveIndex", 0);

            var foreignShip = MakeShip("ForeignShip", out _foreignShipGo); // not in this manager's alive list

            InvokeOnEnemyDestroyed(manager, new EnemyShipDestroyed(foreignShip.gameObject, Vector3.zero));

            Assert.AreEqual(1, GetField(manager, "aliveCount"), "A foreign ship's death must not decrement this manager's wave counter.");
            Assert.AreEqual(1, alive.Count, "The foreign ship must not be removed from this manager's alive list.");
        }

        [Test]
        public void OwnShipDestroyed_DecrementsAliveCount()
        {
            _managerGo = new GameObject("SpaceEncounterManagerTestTarget2");
            var manager = _managerGo.AddComponent<SpaceEncounterManager>();

            var ownShip = MakeShip("OwnShip2", out _ownShipGo);
            var alive = (List<EnemyShip>)GetField(manager, "alive");
            alive.Add(ownShip);
            SetField(manager, "aliveCount", 1);
            SetField(manager, "waveIndex", 0);

            InvokeOnEnemyDestroyed(manager, new EnemyShipDestroyed(ownShip.gameObject, Vector3.zero));

            Assert.AreEqual(0, GetField(manager, "aliveCount"));
            Assert.AreEqual(0, alive.Count);
        }
    }
}

using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ProtectNpcObjectiveTests
    {
        private GameObject _protectedGo;
        private GameObject _playerGo;
        private GameObject _objectiveGo;
        private Health _health;
        private ProtectNpcObjective _objective;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _protectedGo = new GameObject("Protected");
            _health = _protectedGo.AddComponent<Health>();
            _health.Configure(50f); // EditMode skips Awake; seed via Configure per Health's contract.

            _playerGo = new GameObject("Player");

            _objectiveGo = new GameObject("Objective");
            _objective = _objectiveGo.AddComponent<ProtectNpcObjective>();
            SetField("protectedHealth", _health);
            SetField("playerEntity", _playerGo);

            // EditMode skips OnEnable too; invoke it directly so the Died subscription is live (same
            // reflection idiom TimeLoopControllerTests uses to reach a private method under test).
            InvokePrivate("OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_protectedGo != null) Object.DestroyImmediate(_protectedGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_objectiveGo != null) Object.DestroyImmediate(_objectiveGo);
            EventBus.Clear();
        }

        private void SetField(string name, Object value)
        {
            typeof(ProtectNpcObjective).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_objective, value);
        }

        private void InvokePrivate(string methodName)
        {
            typeof(ProtectNpcObjective).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_objective, null);
        }

        private static DamageInfo Lethal() => new DamageInfo(999f, Vector3.zero, Vector3.forward, null);
        private static DamageInfo NonLethal() => new DamageInfo(10f, Vector3.zero, Vector3.forward, null);

        [Test]
        public void ProtectedDying_InvokesFailed()
        {
            bool failed = false;
            _objective.Failed += () => failed = true;

            _health.ApplyDamage(Lethal());

            Assert.IsTrue(failed);
        }

        [Test]
        public void ProtectedDying_RepublishesEntityDiedForTheWiredPlayer()
        {
            // Health also publishes EntityDied for the protected NPC itself (after Died fires), so
            // collect every event and assert the player's republish is among them.
            var reported = new System.Collections.Generic.List<GameObject>();
            EventBus.Subscribe<EntityDied>(e => reported.Add(e.Entity));

            _health.ApplyDamage(Lethal());

            Assert.Contains(_playerGo, reported);
        }

        [Test]
        public void ProtectedTakingNonLethalDamage_DoesNotFail()
        {
            bool failed = false;
            _objective.Failed += () => failed = true;

            _health.ApplyDamage(NonLethal());

            Assert.IsFalse(failed);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void DisabledObjective_DoesNotReactToProtectedDying()
        {
            InvokePrivate("OnDisable");

            bool failed = false;
            _objective.Failed += () => failed = true;

            _health.ApplyDamage(Lethal());

            Assert.IsFalse(failed);
        }
    }
}

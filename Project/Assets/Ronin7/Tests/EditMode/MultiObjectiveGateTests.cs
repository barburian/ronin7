using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Chapter 6 any-order three-tower gate: covers the OnEnable subscription loop resolving a
    /// null/pre-destroyed objective slot as already-complete instead of permanently blocking
    /// onAllComplete. Reflection idiom mirrors ActivationRelayTests/ProtectNpcObjectiveTests
    /// (EditMode doesn't reliably run Unity lifecycle callbacks on AddComponent).
    /// </summary>
    public class MultiObjectiveGateTests
    {
        private GameObject _gateGo;
        private GameObject _liveGo;
        private MultiObjectiveGate _gate;
        private Health _liveHealth;

        [SetUp]
        public void SetUp()
        {
            _gateGo = new GameObject("Gate");
            _gate = _gateGo.AddComponent<MultiObjectiveGate>();

            _liveGo = new GameObject("LiveObjective");
            _liveHealth = _liveGo.AddComponent<Health>();
            _liveHealth.Configure(50f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gateGo != null) Object.DestroyImmediate(_gateGo);
            if (_liveGo != null) Object.DestroyImmediate(_liveGo);
        }

        private void SetObjectives(Health[] objectives)
        {
            typeof(MultiObjectiveGate).GetField("objectives", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_gate, objectives);
        }

        private void InvokeOnEnable()
        {
            typeof(MultiObjectiveGate).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_gate, null);
        }

        private static DamageInfo Lethal() => new DamageInfo(999f, Vector3.zero, Vector3.forward, null);

        [Test]
        public void OnEnable_NullObjective_CountsAsAlreadyComplete()
        {
            SetObjectives(new[] { null, _liveHealth });
            InvokeOnEnable();

            bool fired = false;
            _gate.onAllComplete.AddListener(() => fired = true);

            Assert.IsFalse(fired, "Must not fire before the remaining live objective completes");

            _liveHealth.ApplyDamage(Lethal());

            Assert.IsTrue(fired, "The null slot should already count as complete, so killing the last live objective must fire onAllComplete");
        }
    }
}

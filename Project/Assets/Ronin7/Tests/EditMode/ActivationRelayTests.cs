using System.Reflection;
using NUnit.Framework;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ActivationRelayTests
    {
        private GameObject _go;
        private ActivationRelay _relay;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Relay");
            _relay = _go.AddComponent<ActivationRelay>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private void InvokeOnEnable()
        {
            // EditMode doesn't reliably run Unity lifecycle callbacks on AddComponent; hand-invoke the
            // private method, the same reflection idiom ProtectNpcObjectiveTests uses.
            typeof(ActivationRelay).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_relay, null);
        }

        [Test]
        public void OnEnable_InvokesOnEnabledEvent()
        {
            bool invoked = false;
            _relay.OnEnabled.AddListener(() => invoked = true);

            InvokeOnEnable();

            Assert.IsTrue(invoked);
        }

        [Test]
        public void OnEnable_WithNoListeners_DoesNotThrow()
        {
            Assert.DoesNotThrow(InvokeOnEnable);
        }
    }
}

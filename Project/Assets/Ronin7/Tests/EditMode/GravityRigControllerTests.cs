using System.Reflection;
using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    // Guards against a designer clearing the "cycle" array in the inspector: Awake used to index
    // cycle[0] and Advance used to modulo by cycle.Length, both of which would throw on an empty cycle.
    public class GravityRigControllerTests
    {
        private GameObject _go;
        private GravityRigController _controller;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GravityRig");
            _controller = _go.AddComponent<GravityRigController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private void SetCycle(GravityRigController.GravityState[] cycle)
        {
            typeof(GravityRigController).GetField("cycle", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, cycle);
        }

        private void InvokeAwake() =>
            typeof(GravityRigController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_controller, null);

        [Test]
        public void Awake_EmptyCycle_FallsBackToNormal_DoesNotThrow()
        {
            SetCycle(new GravityRigController.GravityState[0]);

            Assert.DoesNotThrow(InvokeAwake);
            Assert.AreEqual(GravityRigController.GravityState.Normal, _controller.CurrentState);
        }

        [Test]
        public void Advance_EmptyCycle_IsNoOp_DoesNotThrow()
        {
            SetCycle(new GravityRigController.GravityState[0]);
            InvokeAwake();

            Assert.DoesNotThrow(() => _controller.Advance());
            Assert.AreEqual(GravityRigController.GravityState.Normal, _controller.CurrentState);
        }
    }
}

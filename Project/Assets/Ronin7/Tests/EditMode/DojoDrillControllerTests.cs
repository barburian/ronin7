using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the "Flawless Run" tracking added to <see cref="DojoDrillController"/>: any
    /// <see cref="EntityDamaged"/> against the player rig during a run clears the flag; damage to
    /// anything else does not. Drives the private <c>flawless</c> field via reflection, mirroring
    /// <c>PostureMeterTests</c>' lifecycle-via-reflection idiom.
    /// </summary>
    public class DojoDrillControllerTests
    {
        private GameObject _rigGo;
        private GameObject _controllerGo;

        [TearDown]
        public void TearDown()
        {
            if (_controllerGo != null) Object.DestroyImmediate(_controllerGo);
            if (_rigGo != null) Object.DestroyImmediate(_rigGo);
            EventBus.Clear();
        }

        private static bool GetFlawless(DojoDrillController controller) =>
            (bool)typeof(DojoDrillController)
                .GetField("flawless", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);

        private static void Life(MonoBehaviour c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

        private DojoDrillController BeginTestDrill()
        {
            _rigGo = new GameObject("DojoDrillControllerTestRig");
            var rig = _rigGo.AddComponent<VRRig>();
            Life(rig, "Awake");

            _controllerGo = new GameObject("DojoDrillControllerTestHost");
            var controller = _controllerGo.AddComponent<DojoDrillController>();
            controller.BeginDrill();
            return controller;
        }

        [Test]
        public void BeginDrill_StartsFlawless()
        {
            var controller = BeginTestDrill();
            Assert.IsTrue(GetFlawless(controller));
        }

        [Test]
        public void EntityDamaged_OnPlayerRig_ClearsFlawless()
        {
            var controller = BeginTestDrill();

            EventBus.Publish(new EntityDamaged(VRRig.Instance.gameObject,
                new DamageInfo(10f, Vector3.zero, Vector3.forward, null), 90f, 100f));

            Assert.IsFalse(GetFlawless(controller), "Damage to the player rig should clear flawless.");
        }

        [Test]
        public void EntityDamaged_OnNonPlayerEntity_DoesNotClearFlawless()
        {
            var controller = BeginTestDrill();
            var dummy = new GameObject("DojoDrillControllerTestDummy");

            EventBus.Publish(new EntityDamaged(dummy,
                new DamageInfo(10f, Vector3.zero, Vector3.forward, null), 90f, 100f));

            Assert.IsTrue(GetFlawless(controller), "Damage to a non-player entity must not clear flawless.");
            Object.DestroyImmediate(dummy);
        }
    }
}

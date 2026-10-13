using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the <see cref="PostureBroken"/> haptics subscription added to
    /// <see cref="CombatFeedbackController"/>: it subscribes in OnEnable, unsubscribes in OnDisable,
    /// and publishing the event never throws (Haptics.Pulse no-ops without an XR device in EditMode).
    /// Drives lifecycle via reflection, mirroring <c>PostureMeterTests</c>' <c>Life</c> helper.
    /// </summary>
    public class CombatFeedbackControllerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            EventBus.Clear();
            TimeScaleArbiter.ResetForTests();
        }

        private static void Life(MonoBehaviour c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

        private static bool HasSubscriber<T>()
        {
            var handlersField = typeof(EventBus).GetField("Handlers", BindingFlags.Static | BindingFlags.NonPublic);
            var handlers = (IDictionary)handlersField.GetValue(null);
            return handlers.Contains(typeof(T));
        }

        private CombatFeedbackController StartController()
        {
            _go = new GameObject("CombatFeedbackControllerTestHost");
            var controller = _go.AddComponent<CombatFeedbackController>();
            Life(controller, "Awake");
            Life(controller, "OnEnable");
            return controller;
        }

        [Test]
        public void OnEnable_SubscribesToPostureBroken_PublishDoesNotThrow()
        {
            StartController();

            Assert.IsTrue(HasSubscriber<PostureBroken>());
            Assert.DoesNotThrow(() => EventBus.Publish(new PostureBroken(_go)));
        }

        [Test]
        public void OnDisable_UnsubscribesFromPostureBroken()
        {
            var controller = StartController();
            Life(controller, "OnDisable");

            Assert.IsFalse(HasSubscriber<PostureBroken>());
            Assert.DoesNotThrow(() => EventBus.Publish(new PostureBroken(_go)));
        }

        [Test]
        public void OnEnable_SubscribesToPostureNearBreak_PublishDoesNotThrow()
        {
            StartController();

            Assert.IsTrue(HasSubscriber<PostureNearBreak>());
            Assert.DoesNotThrow(() => EventBus.Publish(new PostureNearBreak(_go)));
        }

        [Test]
        public void OnDisable_UnsubscribesFromPostureNearBreak()
        {
            var controller = StartController();
            Life(controller, "OnDisable");

            Assert.IsFalse(HasSubscriber<PostureNearBreak>());
            Assert.DoesNotThrow(() => EventBus.Publish(new PostureNearBreak(_go)));
        }

        // HitSideWeights: head at origin facing +Z, right = +X. Returns (left, right).
        private const float FarMin = 0.3f;
        private static Vector2 Weights(Vector3 source) =>
            CombatFeedbackController.HitSideWeights(Vector3.zero, Vector3.right, source, FarMin);

        [Test]
        public void HitSideWeights_HitFromLeft_FavoursLeftHand()
        {
            var w = Weights(new Vector3(-2f, 0f, 0f));
            Assert.AreEqual(1f, w.x, 1e-4f);
            Assert.AreEqual(FarMin, w.y, 1e-4f);
        }

        [Test]
        public void HitSideWeights_HitFromRight_FavoursRightHand()
        {
            var w = Weights(new Vector3(2f, 0f, 0f));
            Assert.AreEqual(FarMin, w.x, 1e-4f);
            Assert.AreEqual(1f, w.y, 1e-4f);
        }

        [Test]
        public void HitSideWeights_StraightAhead_IsEven()
        {
            var w = Weights(new Vector3(0f, 0f, 2f));
            Assert.AreEqual(1f, w.x, 1e-4f);
            Assert.AreEqual(1f, w.y, 1e-4f);
        }

        [Test]
        public void HitSideWeights_FromBehind_IsEven()
        {
            var w = Weights(new Vector3(0f, 0f, -2f));
            Assert.AreEqual(1f, w.x, 1e-4f);
            Assert.AreEqual(1f, w.y, 1e-4f);
        }

        [Test]
        public void HitSideWeights_DiagonalRight_FarHandAboveMinimum()
        {
            var w = Weights(new Vector3(1f, 0f, 1f));
            Assert.AreEqual(1f, w.y, 1e-4f);
            Assert.Greater(w.x, FarMin);
            Assert.Less(w.x, 1f);
        }

        [Test]
        public void OnEnable_SubscribesToAttackWindupStarted_PublishDoesNotThrow()
        {
            StartController();

            Assert.IsTrue(HasSubscriber<AttackWindupStarted>());
            Assert.DoesNotThrow(() => EventBus.Publish(new AttackWindupStarted(Vector3.forward, _go)));
        }

        [Test]
        public void OnDisable_UnsubscribesFromAttackWindupStarted()
        {
            var controller = StartController();
            Life(controller, "OnDisable");

            Assert.IsFalse(HasSubscriber<AttackWindupStarted>());
        }

        [Test]
        public void PlayerHit_TwoArgCtor_SourceDefaultsToPoint()
        {
            var e = new PlayerHit(5f, new Vector3(1f, 2f, 3f));
            Assert.AreEqual(e.Point, e.Source);
        }
    }
}

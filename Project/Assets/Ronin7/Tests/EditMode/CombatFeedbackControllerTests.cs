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
    }
}

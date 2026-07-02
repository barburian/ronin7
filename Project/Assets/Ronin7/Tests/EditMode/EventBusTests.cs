using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class EventBusTests
    {
        // Unique payload per test class to avoid cross-test bleed even if Clear were ever forgotten.
        private struct Ping { public int Value; }
        private struct Pong { public string Tag; }

        [SetUp]
        public void SetUp() => EventBus.Clear();

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void Subscribe_ThenPublish_InvokesHandlerWithPayload()
        {
            int received = 0;
            EventBus.Subscribe<Ping>(p => received = p.Value);

            EventBus.Publish(new Ping { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_InvokesAllSubscribers()
        {
            int aCalls = 0, bCalls = 0;
            EventBus.Subscribe<Ping>(_ => aCalls++);
            EventBus.Subscribe<Ping>(_ => bCalls++);

            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(1, aCalls);
            Assert.AreEqual(1, bCalls);
        }

        [Test]
        public void Unsubscribe_StopsDeliveryToThatHandlerOnly()
        {
            int aCalls = 0, bCalls = 0;
            System.Action<Ping> handlerA = _ => aCalls++;
            System.Action<Ping> handlerB = _ => bCalls++;
            EventBus.Subscribe(handlerA);
            EventBus.Subscribe(handlerB);

            EventBus.Unsubscribe(handlerA);
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(0, aCalls);
            Assert.AreEqual(1, bCalls);
        }

        [Test]
        public void Publish_WithNoSubscribers_IsNoOp()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new Pong { Tag = "nobody-home" }));
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            int calls = 0;
            EventBus.Subscribe<Ping>(_ => calls++);
            EventBus.Subscribe<Pong>(_ => calls++);

            EventBus.Clear();
            EventBus.Publish(new Ping { Value = 1 });
            EventBus.Publish(new Pong { Tag = "x" });

            Assert.AreEqual(0, calls);
        }
    }
}

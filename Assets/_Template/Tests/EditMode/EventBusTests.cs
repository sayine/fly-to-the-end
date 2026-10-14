using NUnit.Framework;
using Template.Core;

namespace Template.Tests
{
    public class EventBusTests
    {
        private struct TestEvent : IEvent { public int Value; }
        private struct OtherEvent : IEvent { }

        [SetUp, TearDown]
        public void Reset() => EventBus.ClearAll();

        [Test]
        public void Raise_InvokesSubscriberWithPayload()
        {
            var received = 0;
            EventBus<TestEvent>.Subscribe(e => received = e.Value);
            EventBus<TestEvent>.Raise(new TestEvent { Value = 42 });
            Assert.AreEqual(42, received);
        }

        [Test]
        public void Raise_WithoutSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus<TestEvent>.Raise(default));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var calls = 0;
            void Handler(TestEvent e) => calls++;
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Raise(default);
            EventBus<TestEvent>.Unsubscribe(Handler);
            EventBus<TestEvent>.Raise(default);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, EventBus<TestEvent>.HandlerCount);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_RegistersOnce()
        {
            var calls = 0;
            void Handler(TestEvent e) => calls++;
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Raise(default);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Channels_AreIsolatedByType()
        {
            var calls = 0;
            EventBus<TestEvent>.Subscribe(_ => calls++);
            EventBus<OtherEvent>.Raise(default);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void UnsubscribeDuringRaise_IsSafe_AndOtherHandlersStillRun()
        {
            var first = 0;
            var second = 0;
            void First(TestEvent e)
            {
                first++;
                EventBus<TestEvent>.Unsubscribe(First);
            }
            EventBus<TestEvent>.Subscribe(First);
            EventBus<TestEvent>.Subscribe(_ => second++);

            EventBus<TestEvent>.Raise(default);
            EventBus<TestEvent>.Raise(default);

            Assert.AreEqual(1, first);
            Assert.AreEqual(2, second);
            Assert.AreEqual(1, EventBus<TestEvent>.HandlerCount);
        }

        [Test]
        public void SubscribeDuringRaise_RunsFromNextRaise()
        {
            var late = 0;
            void Late(TestEvent e) => late++;
            EventBus<TestEvent>.Subscribe(_ => EventBus<TestEvent>.Subscribe(Late));

            EventBus<TestEvent>.Raise(default);
            Assert.AreEqual(0, late);
            EventBus<TestEvent>.Raise(default);
            Assert.AreEqual(1, late);
        }

        [Test]
        public void ClearAll_RemovesHandlersOfAllTypes()
        {
            EventBus<TestEvent>.Subscribe(_ => { });
            EventBus<OtherEvent>.Subscribe(_ => { });
            EventBus.ClearAll();
            Assert.AreEqual(0, EventBus<TestEvent>.HandlerCount);
            Assert.AreEqual(0, EventBus<OtherEvent>.HandlerCount);
        }
    }
}

using System;
using System.Threading;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Utilities.UnitTest
{
    [TestFixture]
    public class Class_EventBus
    {
        [Test]
        public void publishing_without_subscribing_does_not_throw_exception()
        {
            Assert.DoesNotThrow(() => EventBus<Object>.Publish(new Object()));
        }

        [Test]
        public void publishing_reaches_one_subscriber()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);

            Assert.IsTrue(wasCalled);
        }

        [Test]
        public void publishing_reaches_multiple_subscribers()
        {
            bool firstWasCalled = false;
            bool secondWasCalled = false;
            Action<Object> firstCallback = _ => firstWasCalled = true;
            Action<Object> secondCallback = _ => secondWasCalled = true;
            EventBus<Object>.Subscribe(firstCallback);
            EventBus<Object>.Subscribe(secondCallback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(firstCallback);
            EventBus<Object>.Unsubscribe(secondCallback);

            Assert.IsTrue(firstWasCalled);
            Assert.IsTrue(secondWasCalled);
        }

        [Test]
        public void publishing_after_unsubscribe_does_not_callback()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new Object());

            Assert.IsFalse(wasCalled);
        }

        [Test]
        public void publishing_after_publishing_and_unsubscribing_does_not_callback()
        {
            int callCount = 0;
            Action<Object> callback = _ => ++callCount;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new Object());

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void publishing_wrong_event_does_not_callback()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<String>.Publish(String.Empty);
            EventBus<Object>.Unsubscribe(callback);

            Assert.IsFalse(wasCalled);
        }

        [Test]
        public void subscribing_twice_results_in_one_callback()
        {
            int callCount = 0;
            Action<Object> callback = _ => ++callCount;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void subscribing_twice_requires_one_unsubscribes()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new object());

            Assert.IsFalse(wasCalled);
        }

        [Test]
        public void unsubscribing_a_callback_that_is_not_subscribed_does_not_throw()
        {
            EventBus<Object>.Unsubscribe(_ => { });
        }

        [Test]
        public void unsubscribing_twice_after_subscribing_once_does_not_throw()
        {
            Action<Object> callback = _ => { };
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
        }

        [Test]
        public void exception_thrown_from_one_subscriber_still_calls_other_subscribers()
        {
            var firstCalled = false;
            var secondCalled = false;
            using (new EventSubscription<Object>(_ => firstCalled = true))
            using (new EventSubscription<Object>(_ => { throw new Exception(); }))
            using (new EventSubscription<Object>(_ => secondCalled = true))
            {
                EventBus<Object>.Publish(new Object());
            }

            Assert.IsTrue(firstCalled);
            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void exception_thrown_from_subscriber_writes_error_log_message()
        {
            using (var logger = new Core.UnitTest.Fixtures.Logging())
            using (new EventSubscription<Object>(_ => { throw new Exception(); }))
            {
                EventBus<Object>.Publish(new object());

                Assert.AreEqual(1, logger.ErrorCount);
            }
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Utilities.UnitTest
{
    [TestFixture]
    public class Class_EventBus
    {
        [Test]
        public void publishing_without_subscribing_does_not_throw_exception()
        {
            Assert.DoesNotThrow(() => EventBus<object>.Publish(new object()));
        }

        [Test]
        public void publishing_reaches_one_subscriber()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);

            Assert.That(wasCalled, Is.True);
        }

        [Test]
        public void publishing_reaches_multiple_subscribers()
        {
            bool firstWasCalled = false;
            bool secondWasCalled = false;
            Action<object> firstCallback = _ => firstWasCalled = true;
            Action<object> secondCallback = _ => secondWasCalled = true;
            EventBus<object>.Subscribe(firstCallback);
            EventBus<object>.Subscribe(secondCallback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(firstCallback);
            EventBus<object>.Unsubscribe(secondCallback);

            Assert.Multiple(() =>
            {
                Assert.That(firstWasCalled, Is.True);
                Assert.That(secondWasCalled, Is.True);
            });
        }

        [Test]
        public void publishing_after_unsubscribe_does_not_callback()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.That(wasCalled, Is.False);
        }

        [Test]
        public void publishing_after_publishing_and_unsubscribing_does_not_callback()
        {
            int callCount = 0;
            Action<object> callback = _ => ++callCount;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void publishing_wrong_event_does_not_callback()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<string>.Publish(string.Empty);
            EventBus<object>.Unsubscribe(callback);

            Assert.That(wasCalled, Is.False);
        }

        [Test]
        public void subscribing_twice_results_in_one_callback()
        {
            int callCount = 0;
            Action<object> callback = _ => ++callCount;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void subscribing_twice_requires_one_unsubscribes()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.That(wasCalled, Is.False);
        }

        [Test]
        public void unsubscribing_a_callback_that_is_not_subscribed_does_not_throw()
        {
            EventBus<object>.Unsubscribe(_ => { });
        }

        [Test]
        public void unsubscribing_twice_after_subscribing_once_does_not_throw()
        {
            Action<object> callback = _ => { };
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Unsubscribe(callback);
        }

        [Test]
        public void exception_thrown_from_one_subscriber_still_calls_other_subscribers()
        {
            var firstCalled = false;
            var secondCalled = false;
            using (new EventSubscription<object>(_ => firstCalled = true))
            using (new EventSubscription<object>(_ => { throw new Exception(); }))
            using (new EventSubscription<object>(_ => secondCalled = true))
            {
                EventBus<object>.Publish(new object());
            }

            Assert.Multiple(() =>
            {
                Assert.That(firstCalled, Is.True);
                Assert.That(secondCalled, Is.True);
            });
        }

        [Test]
        public void exception_thrown_from_subscriber_writes_error_log_message()
        {
            using (var logger = new TestUtilities.Logging())
            using (new EventSubscription<object>(_ => { throw new Exception(); }))
            {
                EventBus<object>.Publish(new object());

                Assert.That(logger.ErrorCount, Is.EqualTo(1));
            }
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities.UnitTest
{
    [TestFixture]
    public class Class_EventSubscriptions
    {
        [Test]
        public void subscribing_one_handler_and_unsubscribing_collection_results_in_no_callback()
        {
            var wasCalled = false;
            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<object>(_ => wasCalled = true);
            }

            EventBus<object>.Publish(new object());

            Assert.IsFalse(wasCalled);
        }

        [Test]
        public void subscribing_two_handlers_and_unsubscribing_collection_results_in_no_callback()
        {
            var wasCalled1 = false;
            var wasCalled2 = false;

            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<object>(_ => wasCalled1 = true);
                subscriptions.Add<object>(_ => wasCalled2 = true);
            }

            EventBus<object>.Publish(new object());

            Assert.IsFalse(wasCalled1);
            Assert.IsFalse(wasCalled2);
        }

        [Test]
        public void subscribing_two_handlers_results_in_both_callbacks()
        {
            var wasCalled1 = false;
            var wasCalled2 = false;

            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<object>(_ => wasCalled1 = true);
                subscriptions.Add<object>(_ => wasCalled2 = true);
                EventBus<object>.Publish(new object());
            }

            Assert.IsTrue(wasCalled1);
            Assert.IsTrue(wasCalled2);
        }

        [Test]
        public void subscribing_null_callback_does_not_crash()
        {
            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<object>(null);
                EventBus<object>.Publish(new object());
            }
        }

    }
}

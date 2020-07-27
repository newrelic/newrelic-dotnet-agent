using System;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
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
                subscriptions.Add<Object>(_ => wasCalled = true);
            }

            EventBus<Object>.Publish(new Object());

            Assert.IsFalse(wasCalled);
        }

        [Test]
        public void subscribing_two_handlers_and_unsubscribing_collection_results_in_no_callback()
        {
            var wasCalled1 = false;
            var wasCalled2 = false;

            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<Object>(_ => wasCalled1 = true);
                subscriptions.Add<Object>(_ => wasCalled2 = true);
            }

            EventBus<Object>.Publish(new Object());

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
                subscriptions.Add<Object>(_ => wasCalled1 = true);
                subscriptions.Add<Object>(_ => wasCalled2 = true);
                EventBus<Object>.Publish(new Object());
            }

            Assert.IsTrue(wasCalled1);
            Assert.IsTrue(wasCalled2);
        }

        [Test]
        public void subscribing_null_callback_does_not_crash()
        {
            using (var subscriptions = new Subscriptions())
            {
                subscriptions.Add<Object>(null);
                EventBus<Object>.Publish(new Object());
            }
        }

    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Utilities.UnitTest
{
    [TestFixture]
    public class Class_EventSubscription
    {
        [Test]
        public void publishing_outside_using_statement_results_in_no_callback()
        {
            var wasCalled = false;
            using (new EventSubscription<object>(_ => wasCalled = true))
            {
            }

            EventBus<object>.Publish(new object());

            ClassicAssert.IsFalse(wasCalled);
        }

        [Test]
        public void publishing_inside_using_statement_results_in_callback()
        {
            var wasCalled = false;
            using (new EventSubscription<object>(_ => wasCalled = true))
            {
                EventBus<object>.Publish(new object());
            }

            ClassicAssert.IsTrue(wasCalled);
        }

        [Test]
        public void two_disposables_with_same_callback_are_called_once()
        {
            var callCount = 0;
            Action<object> callback = _ => ++callCount;
            using (new EventSubscription<object>(callback))
            using (new EventSubscription<object>(callback))
            {
                EventBus<object>.Publish(new object());
            }

            ClassicAssert.AreEqual(1, callCount);
        }
    }
}

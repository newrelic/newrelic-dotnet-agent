// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

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

            Assert.That(wasCalled, Is.False);
        }

        [Test]
        public void publishing_inside_using_statement_results_in_callback()
        {
            var wasCalled = false;
            using (new EventSubscription<object>(_ => wasCalled = true))
            {
                EventBus<object>.Publish(new object());
            }

            Assert.That(wasCalled, Is.True);
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

            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}

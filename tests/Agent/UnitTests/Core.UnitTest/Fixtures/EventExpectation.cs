// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Fixtures
{
    public class EventExpectation<T> : IDisposable
    {
        private readonly EventSubscription<T> _subscription;
        private bool _eventWasFired;

        public EventExpectation()
        {
            _subscription = new EventSubscription<T>(_ => _eventWasFired = true);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            Assert.That(_eventWasFired, "Expected event {0} was not fired", typeof(T).Name);
        }
    }
}

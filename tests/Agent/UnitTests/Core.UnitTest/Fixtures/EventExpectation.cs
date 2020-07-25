using System;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Fixtures
{
    public class EventExpectation<T> : IDisposable
    {
        private readonly EventSubscription<T> _subscription;
        private Boolean _eventWasFired;

        public EventExpectation()
        {
            _subscription = new EventSubscription<T>(_ => _eventWasFired = true);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            Assert.True(_eventWasFired, "Expected event {0} was not fired", typeof(T).Name);
        }
    }
}

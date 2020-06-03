using System;
using System.Collections.Generic;

namespace NewRelic.Api.Agent
{
    internal class NoOpTransaction : ITransaction
    {
        private static IDistributedTracePayload _noOpDistributedTracePayload = new NoOpDistributedTracePayload();
        private static ISpan _noOpSpan = new NoOpSpan();

        public void AcceptDistributedTracePayload(string payload, TransportType transportType = TransportType.Unknown)
        {
        }

        public IDistributedTracePayload CreateDistributedTracePayload()
        {
            return _noOpDistributedTracePayload;
        }

        public ITransaction AddCustomAttribute(string key, object value)
        {
            return this;
        }

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
        }

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType)
        {
        }

        public ISpan CurrentSpan => _noOpSpan;
    }
}

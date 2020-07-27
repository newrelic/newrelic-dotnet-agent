using System;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectorExceptionEnvelope
    {
        public readonly string Exception;

        public CollectorExceptionEnvelope(Exception exception)
        {
            Exception = null;
        }
    }
}

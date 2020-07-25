using System;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectorExceptionEnvelope
    {
        public readonly String Exception;

        public CollectorExceptionEnvelope(Exception exception)
        {
            Exception = null;
        }
    }
}

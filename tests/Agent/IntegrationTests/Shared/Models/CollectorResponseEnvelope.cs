using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectorResponseEnvelope<T>
    {
        [JsonProperty("exception", NullValueHandling = NullValueHandling.Ignore)]
        public readonly CollectorExceptionEnvelope CollectorExceptionEnvelope;

        [JsonProperty("return_value")]
        public readonly T ReturnValue;

        public CollectorResponseEnvelope(CollectorExceptionEnvelope collectorExceptionEnvelope, T returnValue)
        {
            CollectorExceptionEnvelope = collectorExceptionEnvelope;
            ReturnValue = returnValue;
        }
    }
}

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    public class CollectorResponseEnvelope<T>
    {
        [JsonProperty("return_value")]
        public readonly T ReturnValue;

        public CollectorResponseEnvelope(T returnValue)
        {
            ReturnValue = returnValue;
        }
    }
}

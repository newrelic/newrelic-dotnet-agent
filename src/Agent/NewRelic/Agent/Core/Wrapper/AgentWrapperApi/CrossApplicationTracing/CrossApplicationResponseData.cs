using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    // Note: this data is referred to as "AppData" in the CAT spec.
    [JsonConverter(typeof(JsonArrayConverter))]
    public class CrossApplicationResponseData
    {
        [JsonArrayIndex(Index = 0)]
        public readonly string CrossProcessId;
        [JsonArrayIndex(Index = 1)]
        public readonly string TransactionName;
        [JsonArrayIndex(Index = 2)]
        public readonly float QueueTimeInSeconds;
        [JsonArrayIndex(Index = 3)]
        public readonly float ResponseTimeInSeconds;
        [JsonArrayIndex(Index = 4)]
        public readonly long ContentLength;
        [JsonArrayIndex(Index = 5)]
        public readonly string TransactionGuid;
        [JsonArrayIndex(Index = 6)]
        public readonly bool Unused;

        // For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
        }

        // For backwards compatibility we need to support deserializing AppData that is missing field 6
        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
            TransactionGuid = transactionGuid;
        }

        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid, bool unused)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
            TransactionGuid = transactionGuid;
            Unused = unused;
        }
    }
}

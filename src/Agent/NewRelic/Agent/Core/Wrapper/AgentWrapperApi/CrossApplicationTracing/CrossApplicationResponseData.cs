using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    // Note: this data is referred to as "AppData" in the CAT spec.
    [JsonConverter(typeof(JsonArrayConverter)), UsedImplicitly]
    public class CrossApplicationResponseData
    {
        [NotNull, JsonArrayIndex(Index = 0), UsedImplicitly]
        public readonly String CrossProcessId;
        [NotNull, JsonArrayIndex(Index = 1), UsedImplicitly]
        public readonly String TransactionName;
        [JsonArrayIndex(Index = 2), UsedImplicitly]
        public readonly Single QueueTimeInSeconds;
        [JsonArrayIndex(Index = 3), UsedImplicitly]
        public readonly Single ResponseTimeInSeconds;
        [JsonArrayIndex(Index = 4), UsedImplicitly]
        public readonly long ContentLength;
        [CanBeNull, JsonArrayIndex(Index = 5), UsedImplicitly]
        public readonly String TransactionGuid;
        [JsonArrayIndex(Index = 6), UsedImplicitly]
        public readonly Boolean Unused;

        // For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
        public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, long contentLength)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
        }

        // For backwards compatibility we need to support deserializing AppData that is missing field 6
        public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, long contentLength, String transactionGuid)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
            TransactionGuid = transactionGuid;
        }

        public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, long contentLength, String transactionGuid, Boolean unused)
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

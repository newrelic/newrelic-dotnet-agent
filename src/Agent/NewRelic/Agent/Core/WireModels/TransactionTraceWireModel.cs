using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    /// <summary>
    /// Jsonable object containing all of the things necessary to serialize a transaction sample for the transaction_sample_data collector command.
    /// </summary>
    /// <remarks>https://pdx-hudson.datanerd.us/job/collector-master/javadoc/com/nr/collector/datatypes/TransactionSample.html</remarks>
    [JsonConverter(typeof(JsonArrayConverter))]
    public class TransactionTraceWireModel
    {
        // See spec for details on these fields: https://source.datanerd.us/agents/agent-specs/blob/master/Transaction-Trace-LEGACY.md
        [JsonArrayIndex(Index = 0)]
        [DateTimeSerializesAsUnixTime]
        public virtual DateTime StartTime { get; }

        [JsonArrayIndex(Index = 1)]
        [TimeSpanSerializesAsMilliseconds]
        public virtual TimeSpan Duration { get; }

        [JsonArrayIndex(Index = 2)]
        public virtual String TransactionMetricName { get; }

        [JsonArrayIndex(Index = 3)]
        public virtual String Uri { get; }

        [JsonArrayIndex(Index = 4)]
        public virtual TransactionTraceData TransactionTraceData { get; }

        [JsonArrayIndex(Index = 5)]
        public virtual String Guid { get; }

        [JsonArrayIndex(Index = 6)]
        public virtual Object Unused1 { get; } = null;

        // Deprecated (used to be called ForcePersist and was related to RUM)
        [JsonArrayIndex(Index = 7)]
        public virtual Boolean Unused2 { get; } = false;

        // Not used by the .NET agent (because we don't support xray sessions)
        [JsonArrayIndex(Index = 8)]
        public virtual UInt64? XraySessionId { get; }

        // Set if X-NewRelic-Synthetics header is present
        [JsonArrayIndex(Index = 9)]
        public virtual String SyntheticsResourceId { get; }

        [JsonIgnore]
        public Boolean IsSynthetics { get; }

        public TransactionTraceWireModel(DateTime startTime, TimeSpan duration, String transactionMetricName, String uri, TransactionTraceData transactionTraceData, String guid, UInt64? xraySessionId, String syntheticsResourceId, Boolean isSynthetics)
        {
            StartTime = startTime;
            Duration = duration;
            TransactionMetricName = transactionMetricName;
            Uri = uri;
            TransactionTraceData = transactionTraceData;
            Guid = guid;
            XraySessionId = xraySessionId;
            SyntheticsResourceId = syntheticsResourceId;
            IsSynthetics = isSynthetics;
        }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionTraceData
    {
        [JsonArrayIndex(Index = 0)]
        [DateTimeSerializesAsUnixTime]
        public virtual DateTime StartTime { get; }

        [JsonArrayIndex(Index = 1)]
        public virtual Object UnusedArray1 { get; } = new Object();

        [JsonArrayIndex(Index = 2)]
        public virtual Object UnusedArray2 { get; } = new Object();

        [JsonArrayIndex(Index = 3)]
        public virtual TransactionTraceSegment RootSegment { get; }

        [JsonArrayIndex(Index = 4)]
        public virtual TransactionTraceAttributes Attributes { get; }

        [JsonObject(MemberSerialization.OptIn)]
        public class TransactionTraceAttributes
        {
            [JsonProperty("agentAttributes")]
            public virtual ReadOnlyDictionary<String, Object> AgentAttributes { get; }

            [JsonProperty("userAttributes")]
            public virtual ReadOnlyDictionary<String, Object> UserAttributes { get; }

            [JsonProperty("intrinsics")]
            public virtual ReadOnlyDictionary<String, Object> Intrinsics { get; }

            public TransactionTraceAttributes(IEnumerable<KeyValuePair<String, Object>> agentAttributes, IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, IEnumerable<KeyValuePair<String, Object>> userAttributes)
            {
                AgentAttributes = agentAttributes.ToReadOnlyDictionary();
                Intrinsics = intrinsicAttributes.ToReadOnlyDictionary();
                UserAttributes = userAttributes.ToReadOnlyDictionary();
            }
        }

        public TransactionTraceData(DateTime startTime, TransactionTraceSegment rootSegment, IEnumerable<KeyValuePair<String, Object>> agentAttributes, IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, IEnumerable<KeyValuePair<String, Object>> userAttributes)
        {
            StartTime = startTime;
            RootSegment = rootSegment;
            Attributes = new TransactionTraceAttributes(agentAttributes, intrinsicAttributes, userAttributes);
        }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class TransactionTraceSegment
    {
        [JsonArrayIndex(Index = 0)]
        [TimeSpanSerializesAsMilliseconds]
        public TimeSpan TimeBetweenTransactionStartAndSegmentStart { get; }

        [JsonArrayIndex(Index = 1)]
        [TimeSpanSerializesAsMilliseconds]
        public TimeSpan TimeBetweenTransactionStartAndSegmentEnd { get; }

        [JsonArrayIndex(Index = 2)]
        public String Name { get; }

        [JsonArrayIndex(Index = 3)]
        public IDictionary<String, Object> Parameters { get; }

        [JsonArrayIndex(Index = 4)]
        public IList<TransactionTraceSegment> Children { get; }

        [JsonArrayIndex(Index = 5)]
        public String ClassName { get; }

        [JsonArrayIndex(Index = 6)]
        public String MethodName { get; }


        public TransactionTraceSegment(TimeSpan timeBetweenTransactionStartAndSegmentStart, TimeSpan timeBetweenTransactionStartAndSegmentEnd, String name, IDictionary<String, Object> parameters, IEnumerable<TransactionTraceSegment> children, String className, String methodName)
        {
            TimeBetweenTransactionStartAndSegmentStart = timeBetweenTransactionStartAndSegmentStart;
            TimeBetweenTransactionStartAndSegmentEnd = timeBetweenTransactionStartAndSegmentEnd;
            Name = name;
            Parameters = new Dictionary<String, Object>(parameters);
            Children = new List<TransactionTraceSegment>(children);
            ClassName = className;
            MethodName = methodName;
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    /// <summary>
    /// Jsonable object containing all of the things necessary to serialize a transaction sample for the transaction_sample_data collector command.
    /// </summary>
    /// <remarks>https://pdx-hudson.datanerd.us/job/collector-master/javadoc/com/nr/collector/datatypes/TransactionSample.html</remarks>
    [JsonConverter(typeof(JsonArrayConverter))]
    public class TransactionTraceWireModel :IWireModel
    {
        // See spec for details on these fields: https://source.datanerd.us/agents/agent-specs/blob/master/Transaction-Trace-LEGACY.md
        [JsonArrayIndex(Index = 0)]
        [DateTimeSerializesAsUnixTimeMilliseconds]
        public virtual DateTime StartTime { get; }

        [JsonArrayIndex(Index = 1)]
        [TimeSpanSerializesAsMilliseconds]
        public virtual TimeSpan Duration { get; }

        [JsonArrayIndex(Index = 2)]
        public virtual string TransactionMetricName { get; }

        [JsonArrayIndex(Index = 3)]
        public virtual string Uri { get; }

        [JsonArrayIndex(Index = 4)]
        public virtual TransactionTraceData TransactionTraceData { get; }

        [JsonArrayIndex(Index = 5)]
        public virtual string Guid { get; }

        [JsonArrayIndex(Index = 6)]
        public virtual object Unused1 { get; } = null;

        // Deprecated (used to be called ForcePersist and was related to RUM)
        [JsonArrayIndex(Index = 7)]
        public virtual bool Unused2 { get; } = false;

        // Not used by the .NET agent (because we don't support xray sessions)
        [JsonArrayIndex(Index = 8)]
        public virtual ulong? XraySessionId { get; }

        // Set if X-NewRelic-Synthetics header is present
        [JsonArrayIndex(Index = 9)]
        public virtual string SyntheticsResourceId { get; }

        [JsonIgnore]
        public bool IsSynthetics { get; }

        public TransactionTraceWireModel(DateTime startTime, TimeSpan duration, string transactionMetricName, string uri, TransactionTraceData transactionTraceData, string guid, ulong? xraySessionId, string syntheticsResourceId, bool isSynthetics)
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
        [DateTimeSerializesAsUnixTimeMilliseconds]
        public virtual DateTime StartTime { get; }

        [JsonArrayIndex(Index = 1)]
        public virtual object UnusedArray1 { get; } = new object();

        [JsonArrayIndex(Index = 2)]
        public virtual object UnusedArray2 { get; } = new object();

        [JsonArrayIndex(Index = 3)]
        public virtual TransactionTraceSegment RootSegment { get; }

        [JsonArrayIndex(Index = 4)]
        public virtual TransactionTraceAttributes Attributes { get; }

        [JsonObject(MemberSerialization.OptIn)]
        public class TransactionTraceAttributes
        {
            [JsonProperty("agentAttributes")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> AgentAttributes { get; }

            [JsonProperty("userAttributes")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> UserAttributes { get; }

            [JsonProperty("intrinsics")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> Intrinsics { get; }

            public TransactionTraceAttributes(IAttributeValueCollection attribValues)
            {
                var filteredAttribs = new AttributeValueCollection(attribValues, AttributeDestinations.TransactionTrace);

                filteredAttribs.MakeImmutable();

                AgentAttributes =  new ReadOnlyDictionary<string, object>(filteredAttribs.GetAttributeValuesDic(AttributeClassification.AgentAttributes));
                Intrinsics = new ReadOnlyDictionary<string, object>(filteredAttribs.GetAttributeValuesDic(AttributeClassification.Intrinsics));
                UserAttributes = new ReadOnlyDictionary<string, object>(filteredAttribs.GetAttributeValuesDic(AttributeClassification.UserAttributes));
            }
        }

        public TransactionTraceData(DateTime startTime, TransactionTraceSegment rootSegment, IAttributeValueCollection attribValues)
        {
            StartTime = startTime;
            RootSegment = rootSegment;
            Attributes = new TransactionTraceAttributes(attribValues);
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
        public string Name { get; }

        [JsonArrayIndex(Index = 3)]
        [JsonConverter(typeof(EventAttributesJsonConverter))]
        public ReadOnlyDictionary<string, object> Parameters { get; }

        [JsonArrayIndex(Index = 4)]
        public IList<TransactionTraceSegment> Children { get; }

        [JsonArrayIndex(Index = 5)]
        public string ClassName { get; }

        [JsonArrayIndex(Index = 6)]
        public string MethodName { get; }


        public TransactionTraceSegment(TimeSpan timeBetweenTransactionStartAndSegmentStart, TimeSpan timeBetweenTransactionStartAndSegmentEnd, string name, IDictionary<string, object> parameters, IEnumerable<TransactionTraceSegment> children, string className, string methodName)
        {
            TimeBetweenTransactionStartAndSegmentStart = timeBetweenTransactionStartAndSegmentStart;
            TimeBetweenTransactionStartAndSegmentEnd = timeBetweenTransactionStartAndSegmentEnd;
            Name = name;
            Parameters = new ReadOnlyDictionary<string, object>(parameters);
            Children = new List<TransactionTraceSegment>(children);
            ClassName = className;
            MethodName = methodName;
        }
    }
}

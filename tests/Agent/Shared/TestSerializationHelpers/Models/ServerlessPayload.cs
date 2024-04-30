// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Globalization;
using NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ServerlessPayload
    {
        [JsonArrayIndex(Index = 0)]
        public int Version { get; set; }

        [JsonArrayIndex(Index = 1)]
        public string ServerlessType { get; set; }

        [JsonArrayIndex(Index = 2)]
        public ServerlessMetadata Metadata { get; set; }

        [JsonArrayIndex(Index = 3)]
        public ServerlessTelemetryPayloads Telemetry { get; set; }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };

        public static ServerlessPayload FromJson(string json) => JsonConvert.DeserializeObject<ServerlessPayload>(json, Settings);
    }

    public class ServerlessMetadata
    {
        [JsonProperty("arn")]
        public string Arn { get; set; }

        [JsonProperty("protocol_version")]
        public int ProtocolVersion { get; set; }

        [JsonProperty("function_version")]
        public string FunctionVersion { get; set; }

        [JsonProperty("execution_environment")]
        public string ExecutionEnvironment { get; set; }

        [JsonProperty("agent_version")]
        public string AgentVersion { get; set; }

        [JsonProperty("metadata_version")]
        public int MetadataVersion { get; set; }

        [JsonProperty("agent_language")]
        public string AgentLanguage { get; set; }
    }

    public class ServerlessTelemetryPayloads
    {
        [JsonProperty("metric_data")]
        public MetricData MetricsPayload { get; set; }

        [JsonProperty("analytic_event_data")]
        public TransactionEventPayload TransactionEventsPayload { get; set; }

        [JsonProperty("custom_event_data")]
        public CustomEventPayload CustomEventsPayload { get; set; }

        [JsonProperty("error_event_data")]
        public ErrorEventPayload ErrorEventsPayload { get; set; }

        [JsonProperty("span_event_data")]
        public SpanEventPayload SpanEventsPayload { get; set; }

        [JsonProperty("sql_trace_data")]
        public SqlTracePayload SqlTracePayload { get; set; }

        [JsonProperty("error_data")]
        public ErrorTracePayload ErrorTracePayload { get; set; }

        [JsonProperty("transaction_sample_data")]
        public TransactionTracePayload TransactionTracePayload { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class TransactionEventPayload
    {
        [JsonArrayIndex(Index = 0)]
        public object AgentRunId { get; set; }

        [JsonArrayIndex(Index = 1)]
        public object Additions { get; set; }

        [JsonArrayIndex(Index = 2)]
        public TransactionEvent[] TransactionEvents { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class SpanEventPayload
    {
        [JsonArrayIndex(Index = 0)]
        public object AgentRunId { get; set; }

        [JsonArrayIndex(Index = 1)]
        public object Additions { get; set; }

        [JsonArrayIndex(Index = 2)]
        public SpanEvent[] SpanEvents { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class SqlTracePayload
    {
        [JsonArrayIndex(Index = 0)]
        public SqlTrace[] SqlTraces { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class ErrorTracePayload
    {
        [JsonArrayIndex(Index = 0)]
        public object AgentRunId { get; set; }

        [JsonArrayIndex(Index = 1)]
        public ErrorTrace[] ErrorTraces { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class TransactionTracePayload
    {
        [JsonArrayIndex(Index = 0)]
        public object AgentRunId { get; set; }

        [JsonArrayIndex(Index = 1)]
        public TransactionSample[] TransactionSamples { get; set; }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class CustomEventPayload
    {
        [JsonArrayIndex(Index = 0)]
        public object AgentRunId { get; set; }

        [JsonArrayIndex(Index = 1)]
        public CustomEventData[] CustomEvents { get; set; }
    }
}

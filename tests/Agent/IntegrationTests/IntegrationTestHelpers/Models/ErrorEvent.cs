using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ErrorEventPayload
    {
        [JsonArrayIndex(Index = 0)] public readonly object AgentRunId;

        [JsonArrayIndex(Index = 1)] public readonly ErrorEventAdditions Additions;

        [JsonArrayIndex(Index = 2)] public IList<ErrorEventEvents> Events;

        public ErrorEventPayload()
        {
        }

        public ErrorEventPayload(long agentRunId, ErrorEventAdditions additions, IList<ErrorEventEvents> events)
        {
            AgentRunId = agentRunId;
            Additions = additions;
            Events = events;
        }

    }

    public class ErrorEventAdditions
    {
        [JsonProperty("reservoir_size")] public readonly uint ReservoirSize;

        [JsonProperty("events_seen")] public readonly uint EventsSeen;

        public ErrorEventAdditions()
        {
        }

        public ErrorEventAdditions(uint reservoirSize, uint eventsSeen)
        {
            ReservoirSize = reservoirSize;
            EventsSeen = eventsSeen;
        }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class ErrorEventEvents
    {
        [JsonArrayIndex(Index = 0)] public readonly IDictionary<String, Object> IntrinsicAttributes;

        [JsonArrayIndex(Index = 1)] public readonly IDictionary<String, Object> UserAttributes;

        [JsonArrayIndex(Index = 2)] public readonly IDictionary<String, Object> AgentAttributes;

        public ErrorEventEvents()
        {

        }

        public ErrorEventEvents(IDictionary<String, Object> intrinsicAttributes, IDictionary<String, Object> userAttributes, IDictionary<String, Object> agentAttributes)
        {
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            AgentAttributes = agentAttributes;
        }

        [NotNull]
        public IDictionary<String, Object> GetByType(EventAttributeType attributeType)
        {
            IDictionary<String, Object> attributes;
            switch (attributeType)
            {
                case EventAttributeType.Intrinsic:
                    attributes = IntrinsicAttributes;
                    break;
                case EventAttributeType.Agent:
                    attributes = AgentAttributes;
                    break;
                case EventAttributeType.User:
                    attributes = UserAttributes;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return attributes ?? new Dictionary<String, Object>();
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
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
        [JsonArrayIndex(Index = 0)] public readonly IDictionary<string, object> IntrinsicAttributes;

        [JsonArrayIndex(Index = 1)] public readonly IDictionary<string, object> UserAttributes;

        [JsonArrayIndex(Index = 2)] public readonly IDictionary<string, object> AgentAttributes;

        public ErrorEventEvents()
        {

        }

        public ErrorEventEvents(IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes, IDictionary<string, object> agentAttributes)
        {
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            AgentAttributes = agentAttributes;
        }


        public IDictionary<string, object> GetByType(EventAttributeType attributeType)
        {
            IDictionary<string, object> attributes;
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

            return attributes ?? new Dictionary<string, object>();
        }
    }
}

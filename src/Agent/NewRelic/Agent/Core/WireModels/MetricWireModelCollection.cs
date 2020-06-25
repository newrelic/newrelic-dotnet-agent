/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(MetricWireModelCollectionJsonConverter))]
    public class MetricWireModelCollection
    {
        public MetricWireModelCollection(string agentRunId, double beginEpoch, double endEpoch, IEnumerable<MetricWireModel> metrics)
        {
            AgentRunID = agentRunId;
            StartEpochTime = beginEpoch;
            EndEpochTime = endEpoch;
            Metrics = metrics;
        }

        public string AgentRunID { get; private set; }

        public double StartEpochTime { get; private set; }

        public double EndEpochTime { get; private set; }

        public IEnumerable<MetricWireModel> Metrics { get; private set; }
    }
}

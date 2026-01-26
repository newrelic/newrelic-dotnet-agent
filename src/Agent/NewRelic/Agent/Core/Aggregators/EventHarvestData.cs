// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Aggregators;

public struct EventHarvestData
{
    [JsonProperty("reservoir_size")]
    public int ReservoirSize { get; private set; }
    [JsonProperty("events_seen")]
    public int EventsSeen { get; private set; }

    public EventHarvestData(int reservoirSize, int eventsSeen)
    {
        ReservoirSize = reservoirSize;
        EventsSeen = eventsSeen;
    }
}
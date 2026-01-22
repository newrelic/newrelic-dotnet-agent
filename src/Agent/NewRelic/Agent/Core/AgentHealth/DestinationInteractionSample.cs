// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.AgentHealth;

public class DestinationInteractionSample
{
    public readonly string Api;
    public readonly string ApiArea;
    public readonly long BytesSent;
    public readonly long BytesReceived;

    public DestinationInteractionSample(string api, string apiArea, long dataSent, long dataReceived)
    {
        Api = api;
        ApiArea = apiArea;
        BytesSent = dataSent;
        BytesReceived = dataReceived;
    }
}
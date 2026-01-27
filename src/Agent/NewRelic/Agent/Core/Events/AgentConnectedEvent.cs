// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.Events;

public class AgentConnectedEvent
{
    public IConnectionInfo ConnectInfo { get; set; }
}

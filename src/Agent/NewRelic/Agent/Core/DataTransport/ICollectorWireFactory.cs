// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.DataTransport;

public interface ICollectorWireFactory
{
    ICollectorWire GetCollectorWire(IConfiguration configuration, IAgentHealthReporter agentHealthReporter);
    ICollectorWire GetCollectorWire(IConfiguration configuration, Dictionary<string, string> requestHeadersMap, IAgentHealthReporter agentHealthReporter);
}

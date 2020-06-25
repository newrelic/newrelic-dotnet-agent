/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWireFactory : ICollectorWireFactory
    {
        public ICollectorWire GetCollectorWire(IConfiguration configuration, IAgentHealthReporter agentHealthReporter)
        {
            return new HttpCollectorWire(configuration, agentHealthReporter);
        }

        public ICollectorWire GetCollectorWire(IConfiguration configuration, Dictionary<string, string> requestHeadersMap, IAgentHealthReporter agentHealthReporter)
        {
            return new HttpCollectorWire(configuration, requestHeadersMap, agentHealthReporter);
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using System.Collections.Generic;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWireFactory : ICollectorWireFactory
    {
        IHttpClientFactory _httpClientFactory;

        public HttpCollectorWireFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public ICollectorWire GetCollectorWire(IConfiguration configuration, IAgentHealthReporter agentHealthReporter)
        {
            return new HttpCollectorWire(configuration, agentHealthReporter, _httpClientFactory);
        }

        public ICollectorWire GetCollectorWire(IConfiguration configuration, Dictionary<string, string> requestHeadersMap, IAgentHealthReporter agentHealthReporter)
        {
            return new HttpCollectorWire(configuration, requestHeadersMap, agentHealthReporter, _httpClientFactory);
        }
    }
}

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

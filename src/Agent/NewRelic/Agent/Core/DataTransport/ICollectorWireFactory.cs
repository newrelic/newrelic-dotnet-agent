using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWireFactory
    {
        ICollectorWire GetCollectorWire(IConfiguration configuration);
    }
}

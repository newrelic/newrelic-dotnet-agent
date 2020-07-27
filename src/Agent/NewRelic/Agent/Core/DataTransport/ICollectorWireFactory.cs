using JetBrains.Annotations;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWireFactory
    {
        [NotNull]
        ICollectorWire GetCollectorWire([NotNull] IConfiguration configuration);
    }
}

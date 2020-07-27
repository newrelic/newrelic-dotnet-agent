using JetBrains.Annotations;
using NewRelic.Agent.Core.Configuration;

namespace NewRelic.Agent.Core.Events
{
    public class ServerConfigurationUpdatedEvent
    {
        [NotNull]
        public readonly ServerConfiguration Configuration;

        public ServerConfigurationUpdatedEvent([NotNull] ServerConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}

using NewRelic.Agent.Core.Configuration;

namespace NewRelic.Agent.Core.Events
{
	public class ServerConfigurationUpdatedEvent
	{
		public readonly ServerConfiguration Configuration;

		public ServerConfigurationUpdatedEvent(ServerConfiguration configuration)
		{
			Configuration = configuration;
		}
	}
}

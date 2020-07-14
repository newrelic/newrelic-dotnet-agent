using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Events
{
	public class ConfigurationDeserializedEvent
	{
		[NotNull]
		public Config.configuration Configuration;

		public ConfigurationDeserializedEvent([NotNull] Config.configuration configuration)
		{
			Configuration = configuration;
		}
	}
}

using JetBrains.Annotations;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Events
{
	public class ConfigurationUpdatedEvent
	{
		[NotNull]
		public readonly IConfiguration Configuration;
		public readonly ConfigurationUpdateSource ConfigurationUpdateSource;

		public ConfigurationUpdatedEvent([NotNull] IConfiguration configuration, ConfigurationUpdateSource configurationUpdateSource)
		{
			Configuration = configuration;
			ConfigurationUpdateSource = configurationUpdateSource;
		}
	}

	public enum ConfigurationUpdateSource
	{
		Unknown,
		Server,
		Local,
		RunTime
	}
}

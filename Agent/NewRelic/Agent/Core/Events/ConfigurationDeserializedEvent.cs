namespace NewRelic.Agent.Core.Events
{
	public class ConfigurationDeserializedEvent
	{
		public Config.configuration Configuration;

		public ConfigurationDeserializedEvent(Config.configuration configuration)
		{
			Configuration = configuration;
		}
	}
}

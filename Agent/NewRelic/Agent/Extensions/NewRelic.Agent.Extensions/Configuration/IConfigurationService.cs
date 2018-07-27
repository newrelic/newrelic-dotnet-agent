using JetBrains.Annotations;

namespace NewRelic.Agent.Configuration
{
	public interface IConfigurationService
	{
		[NotNull]
		IConfiguration Configuration { get; }
	}
}

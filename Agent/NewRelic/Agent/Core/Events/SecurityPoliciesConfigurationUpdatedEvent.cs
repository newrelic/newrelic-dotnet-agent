using JetBrains.Annotations;
using NewRelic.Agent.Core.Configuration;


namespace NewRelic.Agent.Core.Events
{
    public class SecurityPoliciesConfigurationUpdatedEvent
    {
		[NotNull]
		public readonly SecurityPoliciesConfiguration Configuration;

		public SecurityPoliciesConfigurationUpdatedEvent([NotNull] SecurityPoliciesConfiguration configuration)
		{
			Configuration = configuration;
		}
	}
}

using NewRelic.Agent.Core.Configuration;


namespace NewRelic.Agent.Core.Events
{
    public class SecurityPoliciesConfigurationUpdatedEvent
    {
        public readonly SecurityPoliciesConfiguration Configuration;

        public SecurityPoliciesConfigurationUpdatedEvent(SecurityPoliciesConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}

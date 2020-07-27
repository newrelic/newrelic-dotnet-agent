using System;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Fixtures
{
    public class ConfigurationAutoResponder : IDisposable
    {
        [NotNull] public IConfiguration Configuration;
        [NotNull] private Subscriptions _subscriptions = new Subscriptions();

        public ConfigurationAutoResponder(IConfiguration configuration = null)
        {
            Configuration = configuration ?? DefaultConfiguration.Instance;
            _subscriptions.Add<GetCurrentConfigurationRequest, IConfiguration>(OnGetCurrentConfiguration);
        }

        private void OnGetCurrentConfiguration([NotNull] GetCurrentConfigurationRequest requestData, [NotNull] RequestBus<GetCurrentConfigurationRequest, IConfiguration>.ResponseCallback callback)
        {
            callback(Configuration);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}

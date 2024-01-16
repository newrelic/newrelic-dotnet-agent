// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Fixtures
{
    public class ConfigurationAutoResponder : IDisposable
    {
        public IConfiguration Configuration;
        private Subscriptions _subscriptions = new Subscriptions();

        public ConfigurationAutoResponder(IConfiguration configuration = null)
        {
            Configuration = configuration ?? DefaultConfiguration.Instance;
            _subscriptions.Add<GetCurrentConfigurationRequest, IConfiguration>(OnGetCurrentConfiguration);
        }

        private void OnGetCurrentConfiguration(GetCurrentConfigurationRequest requestData, RequestBus<GetCurrentConfigurationRequest, IConfiguration>.ResponseCallback callback)
        {
            callback(Configuration);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}

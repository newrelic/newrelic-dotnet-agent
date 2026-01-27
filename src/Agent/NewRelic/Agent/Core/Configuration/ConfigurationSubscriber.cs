// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Configuration;

public class ConfigurationSubscriber : IDisposable
{
    public IConfiguration Configuration { get; private set; }
    private readonly EventSubscription<ConfigurationUpdatedEvent> _configurationSubscription;

    public ConfigurationSubscriber()
    {
        Configuration = RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest()) ?? DefaultConfiguration.Instance;

        _configurationSubscription = new EventSubscription<ConfigurationUpdatedEvent>(OnConfigurationUpdated);
    }

    private void OnConfigurationUpdated(ConfigurationUpdatedEvent eventData)
    {
        // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
        // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

        if (eventData.Configuration.ConfigurationVersion <= Configuration.ConfigurationVersion)
            return;

        Configuration = eventData.Configuration;
    }

    public void Dispose()
    {
        _configurationSubscription.Dispose();
    }
}
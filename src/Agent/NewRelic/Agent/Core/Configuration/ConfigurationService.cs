// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using System;
using System.Linq;
using System.Reflection;

namespace NewRelic.Agent.Core.Configuration
{
    public class ConfigurationService : IConfigurationService, IDisposable
    {
        private readonly IEnvironment _environment;
        private configuration _localConfiguration = new configuration();
        private ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
        private RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();
        private readonly Subscriptions _subscriptions = new Subscriptions();
        private readonly IProcessStatic _processStatic;
        private readonly IHttpRuntimeStatic _httpRuntimeStatic;
        private readonly IConfigurationManagerStatic _configurationManagerStatic;
        private readonly IDnsStatic _dnsStatic;

        public IConfiguration Configuration { get; private set; }

        public ConfigurationService(IEnvironment environment, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic)
        {
            _environment = environment;
            _processStatic = processStatic;
            _httpRuntimeStatic = httpRuntimeStatic;
            _configurationManagerStatic = configurationManagerStatic;
            _dnsStatic = dnsStatic;

            Configuration = new InternalConfiguration(_environment, _localConfiguration, _serverConfiguration, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, dnsStatic);

            _subscriptions.Add<ConfigurationDeserializedEvent>(OnConfigurationDeserialized);
            _subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);
            _subscriptions.Add<AppNameUpdateEvent>(OnAppNameUpdate);
            _subscriptions.Add<ErrorGroupCallbackUpdateEvent>(OnErrorGroupCallbackUpdate);
            _subscriptions.Add<GetCurrentConfigurationRequest, IConfiguration>(OnGetCurrentConfiguration);
            _subscriptions.Add<SecurityPoliciesConfigurationUpdatedEvent>(OnSecurityPoliciesUpdated);
        }

        private void OnSecurityPoliciesUpdated(SecurityPoliciesConfigurationUpdatedEvent securityPoliciesConfigurationUpdatedEvent)
        {
            _securityPoliciesConfiguration = securityPoliciesConfigurationUpdatedEvent.Configuration;
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.SecurityPolicies);
        }

        private void OnConfigurationDeserialized(ConfigurationDeserializedEvent configurationDeserializedEvent)
        {
            _localConfiguration = configurationDeserializedEvent.Configuration;
            UpdateLogLevel(_localConfiguration);
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.Local);
        }

        private static void UpdateLogLevel(configuration localConfiguration)
        {
            Log.Info("The log level was updated to {0}", localConfiguration.LogConfig.LogLevel);
            LoggerBootstrapper.SetLoggingLevel(localConfiguration.LogConfig.LogLevel);
        }

        private void OnServerConfigurationUpdated(ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
        {
            try
            {
                _serverConfiguration = serverConfigurationUpdatedEvent.Configuration;
                UpdateAndPublishConfiguration(ConfigurationUpdateSource.Server);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Unable to parse the Configuration data from the server so no server side configuration was applied");
            }
        }

        private void OnAppNameUpdate(AppNameUpdateEvent appNameUpdateEvent)
        {
            if (_runTimeConfiguration.ApplicationNames.SequenceEqual(appNameUpdateEvent.AppNames))
                return;

            _runTimeConfiguration = new RunTimeConfiguration(appNameUpdateEvent.AppNames, _runTimeConfiguration.ErrorGroupCallback);
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.RunTime);
        }

        private void OnErrorGroupCallbackUpdate(ErrorGroupCallbackUpdateEvent errorGroupCallbackUpdateEvent)
        {
            if (_runTimeConfiguration.ErrorGroupCallback == errorGroupCallbackUpdateEvent.ErrorGroupCallback)
                return;

            _runTimeConfiguration = new RunTimeConfiguration(_runTimeConfiguration.ApplicationNames, errorGroupCallbackUpdateEvent.ErrorGroupCallback);
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.RunTime);
        }

        private void UpdateAndPublishConfiguration(ConfigurationUpdateSource configurationUpdateSource)
        {
            Configuration = new InternalConfiguration(_environment, _localConfiguration, _serverConfiguration, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            var configurationUpdatedEvent = new ConfigurationUpdatedEvent(Configuration, configurationUpdateSource);
            EventBus<ConfigurationUpdatedEvent>.Publish(configurationUpdatedEvent);
        }

        private void OnGetCurrentConfiguration(GetCurrentConfigurationRequest eventData, RequestBus<GetCurrentConfigurationRequest, IConfiguration>.ResponseCallback callback)
        {
            callback(Configuration);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}

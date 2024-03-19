// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Config
{
    public interface IBootstrapConfiguration
    {
        bool ServerlessModeEnabled { get; }
        int DebugStartupDelaySeconds { get; }
        string ConfigurationFileName { get; }
        bool AgentEnabled { get; }
        string AgentEnabledAt { get; }
    }

    /// <summary>
    /// This configuration class is used during the bootstrapping process before the configuration service is available.
    /// This configuration is made available to the configuration service through a static property on the ConfigurationLoader
    /// which is using during the bootstrapping process. The configuration in this class should not change at runtime and is
    /// mostly meant for things that must be configured before the configuration service is available and cannot be changed
    /// at a later point in time.
    /// </summary>
    public class BootstrapConfiguration : IBootstrapConfiguration
    {
        IConfigurationManagerStatic _configurationManagerStatic;
        Func<string, ValueWithProvenance<string>> _getWebConfigSettingWithProvenance;
        ValueWithProvenance<bool> _agentEnabledWithProvenance;
        bool _agentEnabledValueFromLocalConfig;

        /// <summary>
        /// This constructor is used to create a default instance of bootstrap configuration with reasonable default values.
        /// </summary>
        private BootstrapConfiguration()
        {
            _agentEnabledWithProvenance = new ValueWithProvenance<bool>(true, "Default value");
        }

        /// <summary>
        /// This is the constructor that should be used to create an instance of the bootstrap configuration.
        /// </summary>
        /// <param name="localConfiguration">The local configuration object to use.</param>
        /// <param name="configurationFileName">The name and path of the local configuration file.</param>
        public BootstrapConfiguration(configuration localConfiguration, string configurationFileName)
            : this(localConfiguration, configurationFileName, ConfigurationLoader.GetWebConfigAppSetting, new ConfigurationManagerStatic())
        { }

        /// <summary>
        /// This construction should only be used by the unit tests and allows the tests to mock some dependencies.
        /// </summary>
        /// <param name="localConfiguration"></param>
        /// <param name="configurationFileName"></param>
        /// <param name="getAppSettingWithProvenance"></param>
        public BootstrapConfiguration(configuration localConfiguration, string configurationFileName, Func<string, ValueWithProvenance<string>> getWebConfigSettingWithProvenance, IConfigurationManagerStatic configurationManagerStatic)
        {
            ServerlessModeEnabled = CheckServerlessModeEnabled(localConfiguration);
            DebugStartupDelaySeconds = localConfiguration.debugStartupDelaySeconds;
            ConfigurationFileName = configurationFileName;

            // The AgentEnabled properties are lazy loaded so that logging will be available before they are initialized and we can capture the errors.
            _configurationManagerStatic = configurationManagerStatic;
            _getWebConfigSettingWithProvenance = getWebConfigSettingWithProvenance;
            // Reading the value from local config now so that the bootstrap config does not need to keep a reference to the original local config object.
            _agentEnabledValueFromLocalConfig = localConfiguration.agentEnabled;
            _agentEnabledWithProvenance = null;
        }

        /// <summary>
        /// This helper method is meant to define a set of default values useful for the unit tests, and a fallback in case
        /// something changes in the agent and the ConfigurationLoader.BootstrapConfig property is not set.
        /// </summary>
        /// <returns>A bootstrap configuration instance with reasonable default values.</returns>
        public static IBootstrapConfiguration GetDefault()
        {
            return new BootstrapConfiguration();
        }

        /// <summary>
        /// Gets whether or not serverless mode is enabled.
        /// </summary>
        public bool ServerlessModeEnabled { get; private set; }

        /// <summary>
        /// Gets the debug startup delay in seconds. This is used primarily for debugging of AWS Lambda functions.
        /// </summary>
        public int DebugStartupDelaySeconds { get; private set; }

        /// <summary>
        /// Gets the name of the configuration file that was used to load this configuration.
        /// </summary>
        public string ConfigurationFileName { get; private set; }

        public bool AgentEnabled
        {
            get
            {
                if (_agentEnabledWithProvenance == null)
                {
                    SetAgentEnabledValues();
                }
                return _agentEnabledWithProvenance.Value;
            }
        }

        public string AgentEnabledAt
        {
            get
            {
                if (_agentEnabledWithProvenance == null)
                {
                    SetAgentEnabledValues();
                }
                return _agentEnabledWithProvenance.Provenance;
            }
        }

        private bool CheckServerlessModeEnabled(configuration localConfiguration)
        {
            // according to the spec, environment variable takes precedence over config file
            var serverlessModeEnvVariable = ConfigurationLoader.GetEnvironmentVar("NEW_RELIC_SERVERLESS_MODE_ENABLED");

            if (serverlessModeEnvVariable.TryToBoolean(out var enabledViaEnvVariable))
            {
                return enabledViaEnvVariable;
            }

            // env variable is not set, check for function name
            var awsLambdaFunctionName = ConfigurationLoader.GetEnvironmentVar("AWS_LAMBDA_FUNCTION_NAME");
            if (!string.IsNullOrEmpty(awsLambdaFunctionName))
                return true;

            // fall back to config file
            return localConfiguration.serverlessModeEnabled;
        }

        private void SetAgentEnabledValues()
        {
            _agentEnabledWithProvenance = TryGetAgentEnabledFromWebConfig();
            if (_agentEnabledWithProvenance != null)
            {
                return;
            }

            _agentEnabledWithProvenance = TryGetAgentEnabledFromAppSettings();
            if (_agentEnabledWithProvenance != null)
            {
                return;
            }

            _agentEnabledWithProvenance = new ValueWithProvenance<bool>(_agentEnabledValueFromLocalConfig, ConfigurationFileName);
        }

        private ValueWithProvenance<bool> TryGetAgentEnabledFromWebConfig()
        {
            return TryGetAgentEnabledSetting(_getWebConfigSettingWithProvenance);
        }

        private ValueWithProvenance<bool> TryGetAgentEnabledFromAppSettings()
        {
            return TryGetAgentEnabledSetting(getStringValueWithProvenance);

            ValueWithProvenance<string> getStringValueWithProvenance(string key)
            {
                return new ValueWithProvenance<string>(_configurationManagerStatic.GetAppSetting(key), _configurationManagerStatic.AppSettingsFilePath);
            }
        }

        private ValueWithProvenance<bool> TryGetAgentEnabledSetting(Func<string, ValueWithProvenance<string>> getStringValueWithProvenance)
        {
            try
            {
                var stringValueWithProvenance = getStringValueWithProvenance(Constants.AppSettingsAgentEnabled);
                if (stringValueWithProvenance != null && stringValueWithProvenance.Value != null && bool.TryParse(stringValueWithProvenance.Value, out var value))
                {
                    return new ValueWithProvenance<bool>(value, stringValueWithProvenance.Provenance);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to read {Constants.AppSettingsAgentEnabled} from local config.");
            }

            return null;
        }
    }
}

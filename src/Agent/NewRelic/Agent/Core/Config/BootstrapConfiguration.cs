// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Config
{
    public interface IBootstrapConfiguration
    {
        bool ServerlessModeEnabled { get; }
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
        private BootstrapConfiguration() { }
        public BootstrapConfiguration(configuration localConfiguration)
        {
            ServerlessModeEnabled = CheckServerlessModeEnabled(localConfiguration);
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
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Configuration
{
    public static class AppSettingsConfigResolveWhenUsed
    {
        private static IConfiguration _configuration;
        private static string _appSettingsFilePaths;

        private static IConfiguration Configuration
        {
            get
            {
                return _configuration ?? (_configuration = InitializeConfiguration());
            }
        }

        public static string AppSettingsFilePath => _appSettingsFilePaths;

        private static IConfigurationRoot InitializeConfiguration()
        {
            // Get application base directory, where appsettings*.json will be if they exist
            var applicationDirectory = string.Empty;
            try
            {
                applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }
            catch (AppDomainUnloadedException)
            {
                // Fall back to previous behavior of agents <=8.35.0
                applicationDirectory = Directory.GetCurrentDirectory();
            }

            // add default appsettings.json files to config builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(applicationDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            _appSettingsFilePaths = Path.Combine(applicationDirectory, "appsettings.json");

            // Determine if there is a .NET environment configured, or default to "Production"
            var environment = GetDotnetEnvironment();
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
            var appSettingsEnvPath = Path.Combine(applicationDirectory, $"appsettings.{environment}.json");
            _appSettingsFilePaths = string.Join(", ", _appSettingsFilePaths, appSettingsEnvPath);

            return builder.Build();
        }

        private static string GetDotnetEnvironment()
        {
            var env = new SharedInterfaces.Environment();
            // Determine the environment (e.g. Production, Development, Staging, etc.) by considering the following env vars in order
            // "DOTNET_ENVIRONMENT" takes precedence over "ASPNETCORE_ENVIRONMENT", even for ASP.NET Core applications
            // EnvironmentName is proprietary to our agent and the behavior as of version 10.20 is to not take precedence over the .NET builtins
            var envVarsToCheck = new List<string>() { "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT", "EnvironmentName" };
            foreach ( var envVar in envVarsToCheck )
            {
                var environment = env.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(environment))
                {
                    Log.Debug($".NET environment set to '{environment}' from env var '{envVar}'");
                    return environment;
                }
            }
            Log.Finest("No .NET environment configured in DOTNET_ENVIRONMENT, ASPNETCORE_ENVIRONMENT, or EnvironmentName. Defaulting to 'Production'");
            return "Production";
        }

        public static string GetAppSetting(string key)
        {
            if (key == null)
            {
                return null;
            }
            
            var value = Configuration[key];

            if (Log.IsDebugEnabled)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Log.Debug($"Reading value from appsettings.json and appsettings.*.json: '{key}' not defined. Searched: {_appSettingsFilePaths}.");
                }
                else
                {
                    var valueToLog = value;
                    if (key.Equals(Constants.AppSettingsLicenseKey))
                    {
                        valueToLog = Strings.ObfuscateLicenseKey(value);
                    }
                    Log.Debug($"Reading value from appsettings.json and appsettings.*.json: '{key}={valueToLog}'");
                }
            }

            return value;
        }
    }
}
#endif

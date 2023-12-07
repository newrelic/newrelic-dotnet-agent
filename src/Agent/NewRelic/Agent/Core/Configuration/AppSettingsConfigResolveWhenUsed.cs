// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using NewRelic.Core;
using NewRelic.Core.Logging;

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

            // add default appsettings*.json files to config builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(applicationDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: false);

            // add default appsettings*.json files to list of file paths that we log were searched
            var appSettingsPath = Path.Combine(applicationDirectory, "appsettings.json");
            var appSettingsProductionPath = Path.Combine(applicationDirectory, "appsettings.Production.json");
            _appSettingsFilePaths = string.Join(", ", appSettingsPath, appSettingsProductionPath);


            // Determine if there might be an environment-specific appsettings file
            var environment = GetDotnetEnvironment();
            if (!string.IsNullOrEmpty(environment) && environment != "Production")
            {
                builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
                var appSettingsEnvPath = Path.Combine(applicationDirectory, $"appsettings.{environment}.json");
                _appSettingsFilePaths = string.Join(", ", _appSettingsFilePaths, appSettingsEnvPath);
            }

            return builder.Build();
        }

        private static string GetDotnetEnvironment()
        {
            var env = new SystemInterfaces.Environment();
            // Determine the environment (e.g. Production, Development, Staging, etc.) by considering the following env vars in order
            // "EnvironmentName" is a New Relic proprietary variable and should take precedence over the .NET builtins
            // "DOTNET_ENVIRONMENT" takes precedence over "ASPNETCORE_ENVIRONMENT", even for ASP.NET Core applications
            var envVarsToCheck = new List<string>() { "EnvironmentName", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT" };
            foreach ( var envVar in envVarsToCheck )
            {
                var environment = env.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(environment))
                {
                    return environment;
                }
            }
            return null;
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
                    if (key.Equals(Constants.AppSettingsLicenseKey))
                    {
                        value = Strings.ObfuscateLicenseKey(value);
                    }
                    Log.Debug($"Reading value from appsettings.json and appsettings.*.json: '{key}={value}'");
                }
            }

            return value;
        }
    }
}
#endif

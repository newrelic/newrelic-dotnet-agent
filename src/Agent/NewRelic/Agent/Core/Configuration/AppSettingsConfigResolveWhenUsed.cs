// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.IO;
using Microsoft.Extensions.Configuration;
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

        private static IConfigurationRoot InitializeConfiguration()
        {
            var env = new SystemInterfaces.Environment();
            var applicationDirectory = string.Empty;
            try
            {
                applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }
            catch (AppDomainUnloadedException)
            {
                // Fall back to previous behavior
                applicationDirectory = Directory.GetCurrentDirectory();
            }
            var environment = env.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.IsNullOrEmpty(environment))
            {
                environment = env.GetEnvironmentVariable("EnvironmentName");
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(applicationDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(environment))
            {
                builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
            }

            var appSettingsPath = Path.Combine(applicationDirectory, "appsettings.json");
            var appSettingsEnvPath = Path.Combine(applicationDirectory, $"appsettings.{environment}.json");
            _appSettingsFilePaths = !string.IsNullOrEmpty(environment) ? string.Join(", ", appSettingsPath, appSettingsEnvPath) : appSettingsPath;

            return builder.Build();
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
                    Log.Debug($"Reading value from appsettings.json and appsettings.*.json: '{key}={value}'");
                }
            }

            return value;
        }
    }
}
#endif

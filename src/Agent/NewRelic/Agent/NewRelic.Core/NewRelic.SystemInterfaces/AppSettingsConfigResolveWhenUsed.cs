// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System.IO;
using Microsoft.Extensions.Configuration;

namespace NewRelic.SystemInterfaces
{
    public static class AppSettingsConfigResolveWhenUsed
    {
        private static IConfiguration _configuration;

        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    var env = new Environment();
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddEnvironmentVariables()
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .AddJsonFile($"appsettings.{env.GetEnvironmentVariable("EnvironmentName")}.json", optional: true, reloadOnChange: false);

                    _configuration = builder.Build();
                }
                return _configuration;
            }
        }

        public static string GetAppSetting(string key)
        {
            if (key == null)
            {
                return null;
            }
            
            return Configuration[key];
        }
    }
}
#endif

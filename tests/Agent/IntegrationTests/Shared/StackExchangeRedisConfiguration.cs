// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class StackExchangeRedisConfiguration
    {
        private static string _stackExchangeRedisConnectionString;
        private static string _stackExchangeRedisServer;
        private static string _stackExchangeRedisPort;
        private static string _stackExchangeRedisPassword;

        // example: "1.2.3.4:4444"
        public static string StackExchangeRedisConnectionString
        {
            get
            {
                if (_stackExchangeRedisConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("StackExchangeRedisTests");
                        _stackExchangeRedisConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("StackExchangeRedisConnectionString configuration is invalid.", ex);
                    }
                }

                return _stackExchangeRedisConnectionString;
            }
        }

        public static string StackExchangeRedisServer
        {
            get
            {
                if (_stackExchangeRedisServer == null)
                {
                    try
                    {
                        var uri = new UriBuilder(StackExchangeRedisConnectionString);
                        _stackExchangeRedisServer = uri.Host;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("StackExchangeRedisServer configuration is invalid.", ex);
                    }
                }

                return _stackExchangeRedisServer;
            }
        }

        public static string StackExchangeRedisPort
        {
            get
            {
                if (_stackExchangeRedisPort == null)
                {
                    try
                    {
                        var uri = new UriBuilder(StackExchangeRedisConnectionString);
                        _stackExchangeRedisPort = uri.Port.ToString();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("StackExchangeRedisPort configuration is invalid.", ex);
                    }
                }

                return _stackExchangeRedisPort;
            }
        }
        public static string StackExchangeRedisPassword
        {
            get
            {
                if (_stackExchangeRedisPassword == null)
                {
                    var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("StackExchangeRedisTests");
                    _stackExchangeRedisPassword = testConfiguration["Password"];
                }

                return _stackExchangeRedisPassword;
            }
        }
    }
}

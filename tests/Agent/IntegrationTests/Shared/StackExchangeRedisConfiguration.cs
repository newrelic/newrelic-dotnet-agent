/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class StackExchangeRedisConfiguration
    {
        private static string _stackExchangeRedisConnectionString;
        private static string _stackExchangeRedisServer;
        private static string _stackExchangeRedisPort;

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
                        var index = StackExchangeRedisConnectionString.IndexOf(":");
                        _stackExchangeRedisServer = StackExchangeRedisConnectionString.Substring(0, index);
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
                        var index = StackExchangeRedisConnectionString.IndexOf(":") + 1;
                        _stackExchangeRedisPort = StackExchangeRedisConnectionString.Substring(index);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("StackExchangeRedisPort configuration is invalid.", ex);
                    }
                }

                return _stackExchangeRedisPort;
            }
        }
    }
}

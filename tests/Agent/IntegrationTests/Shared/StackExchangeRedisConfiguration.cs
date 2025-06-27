// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class StackExchangeRedisConfiguration
    {
        private static string _stackExchangeRedisConnectionString;
        private static string _stackExchangeRedisServer;
        private static string _stackExchangeRedisPort;
        private static string _stackExchangeRedisPassword;
        private static bool _parsedHostPort = false;

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
                EnsureHostPortParsed();
                return _stackExchangeRedisServer;
            }
        }

        public static string StackExchangeRedisPort
        {
            get
            {
                EnsureHostPortParsed();
                return _stackExchangeRedisPort;
            }
        }

        // Ensures host and port are parsed and cached only once
        private static void EnsureHostPortParsed()
        {
            if (!_parsedHostPort)
            {
                try
                {
                    ParseHostAndPort(StackExchangeRedisConnectionString, out _stackExchangeRedisServer, out _stackExchangeRedisPort);
                    _parsedHostPort = true;
                }
                catch (Exception ex)
                {
                    throw new Exception("StackExchangeRedisServer or StackExchangeRedisPort configuration is invalid.", ex);
                }
            }
        }

        // Parses host and port from a connection string like host:port
        private static void ParseHostAndPort(string connectionString, out string host, out string port)
        {
            var match = Regex.Match(connectionString, @"^(?<host>[^:/\[]+(?:\.[^:/\[]+)*)[:](?<port>\d+)");
            if (match.Success)
            {
                host = match.Groups["host"].Value;
                port = match.Groups["port"].Value;
            }
            else
            {
                host = string.Empty;
                port = string.Empty;
                throw new FormatException($"Could not parse host and port from connection string: {connectionString}");
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

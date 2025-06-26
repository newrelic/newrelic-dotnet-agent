// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public static class OracleConfiguration
    {
        private static string _oracleConnectionString;
        private static string _oracleDataSource;
        private static string _oracleServer;
        private static string _oraclePort;
        private static bool _parsedHostPort = false;

        // example: "Data Source=1.2.3.4:4444/XE;User Id=SYSTEM;Password=oraclePassword;"
        public static string OracleConnectionString
        {
            get
            {
                if (_oracleConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("OracleTests");
                        _oracleConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("OracleConnectionString configuration is invalid.", ex);
                    }
                }

                return _oracleConnectionString;
            }
        }

        public static string OracleDataSource
        {
            get
            {
                if (_oracleDataSource == null)
                {
                    try
                    {
                        var builder = new DbConnectionStringBuilder { ConnectionString = OracleConnectionString };
                        _oracleDataSource = builder["Data Source"].ToString();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("OracleServer configuration is invalid.", ex);
                    }
                }

                return _oracleDataSource;
            }
        }

        public static string OracleServer
        {
            get
            {
                EnsureHostPortParsed();
                return _oracleServer;
            }
        }

        public static string OraclePort
        {
            get
            {
                EnsureHostPortParsed();
                return _oraclePort;
            }
        }

        // Ensures host and port are parsed and cached only once
        private static void EnsureHostPortParsed()
        {
            if (!_parsedHostPort)
            {
                try
                {
                    ParseHostAndPort(OracleDataSource, out _oracleServer, out _oraclePort);
                    _parsedHostPort = true;
                }
                catch (Exception ex)
                {
                    throw new Exception("OracleServer or OraclePort configuration is invalid.", ex);
                }
            }
        }

        // Parses host and port from a data source string like host:port/service
        private static void ParseHostAndPort(string dataSource, out string host, out string port)
        {
            // Regex matches host (hostname or IPv4), port, and ignores service name
            var match = Regex.Match(dataSource, @"^(?<host>[^:/\[]+(?:\.[^:/\[]+)*)[:](?<port>\d+)");
            if (match.Success)
            {
                host = match.Groups["host"].Value;
                port = match.Groups["port"].Value;
            }
            else
            {
                host = string.Empty;
                port = string.Empty;
                throw new FormatException($"Could not parse host and port from data source: {dataSource}");
            }
        }
    }
}

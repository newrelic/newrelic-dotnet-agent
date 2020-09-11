// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class PostgresConfiguration
    {
        private static string _postgresConnectionString;
        private static Dictionary<string, string> _connectionStringValues;
        private static string _postgresServer;
        private static string _postgresPort;

        // example: "Server=1.2.3.4;Port=4444;User Id=pgUser;Password=pgPW;Database=dbName;"
        public static string PostgresConnectionString
        {
            get
            {
                if (_postgresConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("PostgresTests");
                        _postgresConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("PostgresConnectionString configuration is invalid.", ex);
                    }
                }

                return _postgresConnectionString;
            }
        }

        public static Dictionary<string, string> ConnectionStringValues
        {
            get
            {
                if (_connectionStringValues == null)
                {
                    try
                    {
                        _connectionStringValues = ConfigUtils.GetKeyValuePairsFromConnectionString(PostgresConnectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to parse connection string.", ex);
                    }
                }

                return _connectionStringValues;
            }
        }

        public static string PostgresServer
        {
            get
            {
                if (_postgresServer == null)
                {
                    try
                    {
                        _postgresServer = ConfigUtils.GetConnectionStringValue("Server", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("PostgresServer configuration is invalid.", ex);
                    }
                }

                return _postgresServer;
            }
        }

        public static string PostgresPort
        {
            get
            {
                if (_postgresPort == null)
                {
                    try
                    {
                        _postgresPort = ConfigUtils.GetConnectionStringValue("Port", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("PostgresPort configuration is invalid.", ex);
                    }
                }

                return _postgresPort;
            }
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.Common;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class PostgresConfiguration
    {
        private static string _postgresConnectionString;
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

        public static string PostgresServer
        {
            get
            {
                if (_postgresServer == null)
                {
                    try
                    {
                        var builder = new DbConnectionStringBuilder { ConnectionString = PostgresConnectionString };
                        _postgresServer = builder["Server"].ToString();
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
                        var builder = new DbConnectionStringBuilder { ConnectionString = PostgresConnectionString };
                        _postgresPort = builder["Port"].ToString();
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

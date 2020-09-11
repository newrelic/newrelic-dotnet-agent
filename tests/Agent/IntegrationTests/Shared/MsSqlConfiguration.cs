// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MsSqlConfiguration
    {
        private static string _msSqlConnectionString;
        private static string _msSqlServer;
        private static Dictionary<string, string> _connectionStringValues;

        // example: "Server=1.2.3.4;Database=DBName;User ID=sa;Password=password;Trusted_Connection=False;Encrypt=False;Connection Timeout=30;"
        public static string MsSqlConnectionString
        {
            get
            {
                if (_msSqlConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MSSQLTests");
                        _msSqlConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MsSqlConnectionString configuration is invalid.", ex);
                    }
                }

                return _msSqlConnectionString;
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
                        _connectionStringValues = ConfigUtils.GetKeyValuePairsFromConnectionString(MsSqlConnectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to parse connection string.", ex);
                    }
                }

                return _connectionStringValues;
            }
        }


        public static string MsSqlServer
        {
            get
            {
                if (_msSqlServer == null)
                {
                    try
                    {
                        _msSqlServer = ConfigUtils.GetConnectionStringValue("Server", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MsSqlServer configuration is invalid.", ex);
                    }
                }

                return _msSqlServer;
            }
        }
    }
}

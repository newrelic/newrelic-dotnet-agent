// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MySqlTestConfiguration
    {
        private static string _mySqlConnectionString;
        private static Dictionary<string, string> _connectionStringValues;
        private static string _mySqlServer;
        private static string _mySqlPort;
        private static string _mySqlDbName;

        // example: "Network Address=1.2.3.4;Port=4444;Initial Catalog=CatalogName;Persist Security Info=no;User Name=root;Password=password"
        public static string MySqlConnectionString
        {
            get
            {
                if (_mySqlConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MySQLTests");
                        _mySqlConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MySqlConnectionString configuration is invalid.", ex);
                    }
                }

                return _mySqlConnectionString;
            }
        }

        public static Dictionary<string,string> ConnectionStringValues
        {
            get
            {
                if (_connectionStringValues == null)
                {
                    try
                    {
                        _connectionStringValues = ConfigUtils.GetKeyValuePairsFromConnectionString(MySqlConnectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to parse connection string.", ex);
                    }
                }

                return _connectionStringValues;
            }
        }

        public static string MySqlServer
        {
            get
            {
                if (_mySqlServer == null)
                {
                    try
                    {
                        _mySqlServer = ConfigUtils.GetConnectionStringValue("Network Address", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MySqlServer configuration is invalid.", ex);
                    }
                }

                return _mySqlServer;
            }
        }

        public static string MySqlPort
        {
            get
            {
                if (_mySqlPort == null)
                {
                    try
                    {
                        _mySqlPort = ConfigUtils.GetConnectionStringValue("Port", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MySqlPort configuration is invalid.", ex);
                    }
                }

                return _mySqlPort;
            }
        }

        public static string MySqlDbName
        {
            get
            {
                if (_mySqlDbName == null)
                {
                    try
                    {
                        _mySqlDbName = ConfigUtils.GetConnectionStringValue("Initial Catalog", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MySqlDbName configuration is invalid.", ex);
                    }
                }

                return _mySqlDbName;
            }
        }

    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.Common;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MySqlTestConfiguration
    {
        private static string _mySqlConnectionString;
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

        public static string MySqlServer
        {
            get
            {
                if (_mySqlServer == null)
                {
                    try
                    {
                        var builder = new DbConnectionStringBuilder { ConnectionString = MySqlConnectionString };
                        _mySqlServer = builder["Network Address"].ToString();
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
                        var builder = new DbConnectionStringBuilder { ConnectionString = MySqlConnectionString };
                        _mySqlPort = builder["Port"].ToString();
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
                        var builder = new DbConnectionStringBuilder { ConnectionString = MySqlConnectionString };
                        _mySqlDbName = builder["Initial Catalog"].ToString();
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

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

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
                        var subParts = MySqlConnectionString.Split(';');
                        var index = subParts[0].IndexOf('=') + 1;
                        _mySqlServer = subParts[0].Substring(index);
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
                        var subParts = MySqlConnectionString.Split(';');
                        var index = subParts[1].IndexOf('=') + 1;
                        _mySqlPort = subParts[1].Substring(index);
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
                        var subParts = MySqlConnectionString.Split(';');
                        var index = subParts[2].IndexOf('=') + 1;
                        _mySqlDbName = subParts[2].Substring(index);
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

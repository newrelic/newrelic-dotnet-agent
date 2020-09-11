// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class Db2Configuration
    {
        private static string _db2ConnectionString;
        private static string _db2Server;
        private static Dictionary<string, string> _connectionStringValues;


        // example: "Server=1.2.3.4;Database=SAMPLE;UserID=db2User;Password=db2password"
        public static string Db2ConnectionString
        {
            get
            {
                if (_db2ConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("Db2Tests");
                        _db2ConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Db2ConnectionString configuration is invalid.", ex);
                    }
                }

                return _db2ConnectionString;
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
                        _connectionStringValues = ConfigUtils.GetKeyValuePairsFromConnectionString(Db2ConnectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to parse connection string.", ex);
                    }
                }

                return _connectionStringValues;
            }
        }


        public static string Db2Server
        {
            get
            {
                if (_db2Server == null)
                {
                    try
                    {
                        _db2Server = ConfigUtils.GetConnectionStringValue("Server", ConnectionStringValues);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Db2Server configuration is invalid.", ex);
                    }
                }

                return _db2Server;
            }
        }
    }
}

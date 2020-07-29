/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class OracleConfiguration
    {
        private static string _oracleConnectionString;
        private static string _oracleServer;
        private static string _oraclePort;

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

        public static string OracleServer
        {
            get
            {
                if (_oracleServer == null)
                {
                    try
                    {
                        var indexFrom = OracleConnectionString.IndexOf("Data Source=") + "Data Source=".Length;
                        var indexTo = OracleConnectionString.IndexOf(":");
                        _oracleServer = OracleConnectionString.Substring(indexFrom, indexTo - indexFrom);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("OracleServer configuration is invalid.", ex);
                    }
                }

                return _oracleServer;
            }
        }

        public static string OraclePort
        {
            get
            {
                if (_oraclePort == null)
                {
                    try
                    {
                        var indexFrom = OracleConnectionString.IndexOf(":") + 1;
                        var indexTo = OracleConnectionString.IndexOf("/");
                        _oraclePort = OracleConnectionString.Substring(indexFrom, indexTo - indexFrom);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("OraclePort configuration is invalid.", ex);
                    }
                }

                return _oraclePort;
            }
        }
    }
}

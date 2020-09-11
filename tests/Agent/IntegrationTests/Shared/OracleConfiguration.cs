// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.Common;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class OracleConfiguration
    {
        private static string _oracleConnectionString;
        private static string _oracleDataSource;
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
                if (_oracleServer == null)
                {
                    try
                    {
                        _oracleServer = OracleDataSource.Split(':')[0];
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
                        var indexFrom = OracleDataSource.IndexOf(":") + 1;
                        var indexTo = OracleDataSource.IndexOf("/");
                        _oraclePort = OracleDataSource.Substring(indexFrom, indexTo - indexFrom);
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

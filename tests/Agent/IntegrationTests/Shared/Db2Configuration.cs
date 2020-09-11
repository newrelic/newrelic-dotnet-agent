// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.Common;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class Db2Configuration
    {
        private static string _db2ConnectionString;
        private static string _db2Server;


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

        public static string Db2Server
        {
            get
            {
                if (_db2Server == null)
                {
                    try
                    {
                        var builder = new DbConnectionStringBuilder { ConnectionString = Db2ConnectionString };
                        _db2Server = builder["Server"].ToString();
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

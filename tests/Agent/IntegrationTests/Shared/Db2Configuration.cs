/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;

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
                        var subParts = Db2ConnectionString.Split(';');
                        var index = subParts[0].IndexOf('=') + 1;
                        _db2Server = subParts[0].Substring(index);
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

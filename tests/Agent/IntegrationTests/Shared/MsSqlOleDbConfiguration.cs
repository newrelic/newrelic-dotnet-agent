// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MsSqlOleDbConfiguration
    {
        private static string _msSqlOleDbConnectionString;
        private static string _msSqlOleDbServer;

        // example:  "PROVIDER=SQLXXXX11;Server=1.2.3.4;Database=DBName;Trusted_Connection=no;UID=sa;PWD=password;Encrypt=no;Timeout=30;"
        public static string MsSqlOleDbConnectionString
        {
            get
            {
                if (_msSqlOleDbConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MSSQLOleDbTests");
                        _msSqlOleDbConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MsSqlOleDbConnectionString configuration is invalid.", ex);
                    }
                }

                return _msSqlOleDbConnectionString;
            }
        }
    }
}

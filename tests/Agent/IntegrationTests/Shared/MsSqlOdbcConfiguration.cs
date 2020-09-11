// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class MsSqlOdbcConfiguration
    {
        private static string _msSqlOdbcConnectionString;

        // example: "DRIVER={SQL Server Native Client 11.0};Server=1.2.3.4;Database=DBName;Trusted_Connection=no;UID=sa;PWD=password;Encrypt=no;"
        public static string MsSqlOdbcConnectionString
        {
            get
            {
                if (_msSqlOdbcConnectionString == null)
                {
                    try
                    {
                        var testConfiguration = IntegrationTestConfiguration.GetIntegrationTestConfiguration("MSSQLOdbcTests");
                        _msSqlOdbcConnectionString = testConfiguration["ConnectionString"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("MsSqlOdbcConnectionString configuration is invalid.", ex);
                    }
                }

                return _msSqlOdbcConnectionString;
            }
        }
    }
}

//Copyright 2020 New Relic, Inc. All rights reserved.
//SPDX-License-Identifier: Apache-2.0

using System;
using MySqlConnector;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public static class MsSqlWarmupHelper
    {
        public static void WarmupMySql()
        {
            try
            {
                using var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString);
                using var command = new MySqlCommand("SELECT _date FROM dates LIMIT 1", connection);
                connection.Open();
                command.ExecuteScalar();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }
}

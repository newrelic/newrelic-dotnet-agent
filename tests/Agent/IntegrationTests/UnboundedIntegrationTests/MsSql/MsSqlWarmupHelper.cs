//Copyright 2020 New Relic, Inc. All rights reserved.
//SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.Data.SqlClient;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public static class MsSqlWarmupHelper
    {
        public static void WarmupMsSql()
        {
            try
            {
                using var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString);
                using var command = new SqlCommand("SELECT TOP 1 * FROM NewRelic.dbo.TeamMembers", connection);
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

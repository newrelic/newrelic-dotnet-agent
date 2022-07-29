// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MySql
{
    [Library]
    public class MySqlExerciser
    {

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SingleDateQuery()
        {
            var dates = new List<string>();

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
            {
                connection.Open();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                }
            }
            ConsoleMFLogger.Info(string.Join(",", dates));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async void SingleDateQueryAsync()
        {
            var dates = new List<string>();

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 10000", connection))
            {
                connection.Open();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                }
            }

            ConsoleMFLogger.Info(string.Join(",", dates));
        }
    }
}

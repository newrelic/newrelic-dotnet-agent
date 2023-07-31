// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }
            ConsoleMFLogger.Info(string.Join(",", dates));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SingleDateQueryAsync()
        {
            var dates = new List<string>();

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 10000", connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }

            ConsoleMFLogger.Info(string.Join(",", dates));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void CreateAndExecuteStoredProcedure(string procedureName, bool paramsWithAtSigns)
        {
            CreateProcedure(procedureName);

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(procedureName, connection))
            {
                connection.Open();
                command.CommandType = System.Data.CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.MySqlParameters)
                {
                    var sqlParam = paramsWithAtSigns
                        ? new MySqlParameter(parameter.ParameterName, parameter.Value)
                        : new MySqlParameter(parameter.ParameterName.TrimStart('@'), parameter.Value);

                    command.Parameters.Add(sqlParam);
                }

                ConsoleMFLogger.Info(command.ExecuteNonQuery().ToString());
            }
        }

        private static readonly string CreateProcedureStatement = @"CREATE PROCEDURE `{0}`.`{1}`({2}) BEGIN END;";

        private void CreateProcedure(string procedureName)
        {
            var parameters = string.Join(", ", DbParameterData.MySqlParameters.Select(x => $"{x.ParameterName} {x.DbTypeName}"));
            var statement = string.Format(CreateProcedureStatement, MySqlTestConfiguration.MySqlDbName, procedureName, parameters);
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

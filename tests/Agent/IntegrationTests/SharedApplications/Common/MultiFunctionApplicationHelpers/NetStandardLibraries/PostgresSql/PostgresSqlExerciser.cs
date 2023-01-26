// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Npgsql;
using NpgsqlTypes;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql
{
    [Library]
    internal class PostgresSqlExerciser
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SimpleQuery()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            ConsoleMFLogger.Info(string.Join(",", teamMembers));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SimpleQueryAsync()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            ConsoleMFLogger.Info(string.Join(",", teamMembers));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ParameterizedStoredProcedure(string procedureName)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);

            CreateProcedure(procedureName);

            try
            {
                using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
                using (var command = new NpgsqlCommand(procedureName, connection))
                {
                    connection.Open();
                    command.CommandType = CommandType.StoredProcedure;

                    foreach (var p in DbParameterData.PostgresParameters)
                    {
                        if (p.Value is DateTime)
                            command.Parameters.AddWithValue(p.ParameterName, NpgsqlDbType.Date, p.Value);
                        else
                            command.Parameters.AddWithValue(p.ParameterName, p.Value);
                    }

                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                DropProcedure(procedureName);
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ParameterizedStoredProcedureAsync(string procedureName)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);

            CreateProcedure(procedureName);

            try
            {
                using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
                using (var command = new NpgsqlCommand(procedureName, connection))
                {
                    await connection.OpenAsync();
                    command.CommandType = CommandType.StoredProcedure;

                    foreach (var p in DbParameterData.PostgresParameters)
                    {
                        if (p.Value is DateTime)
                            command.Parameters.AddWithValue(p.ParameterName, NpgsqlDbType.Date, p.Value);
                        else
                            command.Parameters.AddWithValue(p.ParameterName, p.Value);
                    }


                    ConsoleMFLogger.Info((await command.ExecuteNonQueryAsync()).ToString());
                }
            }
            catch (Exception e)
            {
                ConsoleMFLogger.Error(e);
            }
            finally
            {
                DropProcedure(procedureName);
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteScalar()
        {
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command =
                   new NpgsqlCommand("SELECT lastname FROM newrelic.teammembers WHERE firstname = 'Matthew'",
                       connection))
            {
                connection.Open();
                var result = (string)command.ExecuteScalar();
                ConsoleMFLogger.Info(result);
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExecuteScalarAsync()
        {
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command =
                   new NpgsqlCommand("SELECT firstname FROM newrelic.teammembers WHERE lastname = 'Sneeden'",
                       connection))
            {
                await connection.OpenAsync();
                var result = (string)await command.ExecuteScalarAsync();
                ConsoleMFLogger.Info(result);
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void IteratorTest()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'",
                       connection))
            {
                connection.Open();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                }
            }

            ConsoleMFLogger.Info(string.Join(",", teamMembers));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task AsyncIteratorTest()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            ConsoleMFLogger.Info(string.Join(",", teamMembers));
        }


        private const string createProcedureStatement = @"CREATE FUNCTION {0} ({1}) RETURNS void AS $$ BEGIN END; $$ LANGUAGE plpgsql;";
        private const string dropProcedureStatement = @"DROP FUNCTION {0} ({1});";

        private void CreateProcedure(string procedureName)
        {
            var parameters = string.Join(", ", DbParameterData.PostgresParameters.Select(x => $"\"{x.ParameterName}\" {x.DbTypeName}"));
            var statement = string.Format(createProcedureStatement, procedureName, parameters);
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command = new NpgsqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private void DropProcedure(string procedureName)
        {
            var parameters = string.Join(", ", DbParameterData.PostgresParameters.Select(x => $"{x.DbTypeName}"));
            var statement = string.Format(dropProcedureStatement, procedureName, parameters);
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command = new NpgsqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

    }
}

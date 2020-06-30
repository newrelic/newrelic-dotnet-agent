/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Linq;
using NewRelic.Agent.IntegrationTests.Shared;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace BasicMvcCoreApplication.Controllers
{
    public class PostgresController : Controller
    {

        [HttpGet]
        public string Postgres()
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

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> PostgresAsync()
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

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public void PostgresParameterizedStoredProcedure(string procedureName)
        {
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

        [HttpGet]
        public async Task PostgresParameterizedStoredProcedureAsync(string procedureName)
        {
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

                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                DropProcedure(procedureName);
            }
        }

        [HttpGet]
        public string PostgresExecuteScalar()
        {
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command = new NpgsqlCommand("SELECT lastname FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                connection.Open();
                var result = (string)(command.ExecuteScalar());
                return result;
            }
        }

        [HttpGet]
        public async Task<string> PostgresExecuteScalarAsync()
        {
            using (var connection = new NpgsqlConnection(PostgresConfiguration.PostgresConnectionString))
            using (var command = new NpgsqlCommand("SELECT firstname FROM newrelic.teammembers WHERE lastname = 'Sneeden'", connection))
            {
                await connection.OpenAsync();
                var result = (string)(await (command.ExecuteScalarAsync()));
                return result;
            }
        }

        [HttpGet]
        public void PostgresIteratorTest()
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
        }

        [HttpGet]
        public async Task<List<string>> PostgresAsyncIteratorTest()
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

            return teamMembers;
        }

        private readonly string createProcedureStatement = @"CREATE FUNCTION {0} ({1}) RETURNS void AS $$ BEGIN END; $$ LANGUAGE plpgsql;";
        private readonly string dropProcedureStatement = @"DROP FUNCTION {0} ({1});";

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

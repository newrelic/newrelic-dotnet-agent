// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using MySqlConnector;
using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class MySqlConnectorController : Controller
    {
        [HttpGet]
        public string ExecuteReader()
        {
            return ExecuteCommand(command =>
            {
                var dates = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                return string.Join(",", dates);
            });
        }

        [HttpGet]
        public string ExecuteScalar() => ExecuteCommand(command => (string)command.ExecuteScalar());

        [HttpGet]
        public string ExecuteNonQuery() => ExecuteCommand(command =>
        {
            command.ExecuteNonQuery();
            return "done";
        });

        [HttpGet]
        public async Task<string> ExecuteReaderAsync()
        {
            return await ExecuteCommandAsync(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                return string.Join(",", dates);
            });
        }

        [HttpGet]
        public async Task<string> ExecuteScalarAsync() => await ExecuteDbCommandAsync(async command => (string) await command.ExecuteScalarAsync());

        [HttpGet]
        public async Task<string> ExecuteNonQueryAsync() => await ExecuteDbCommandAsync(async command =>
        {
            await command.ExecuteNonQueryAsync();
            return "done";
        });

        [HttpGet]
        public string DbCommandExecuteReader()
        {
            return ExecuteDbCommand(command =>
            {
                var dates = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                return string.Join(",", dates);
            });
        }

        [HttpGet]
        public string DbCommandExecuteScalar() => ExecuteDbCommand(command => (string)command.ExecuteScalar());

        [HttpGet]
        public string DbCommandExecuteNonQuery() => ExecuteDbCommand(command =>
        {
            command.ExecuteNonQuery();
            return "done";
        });

        [HttpGet]
        public async Task<string> DbCommandExecuteReaderAsync()
        {
            return await ExecuteDbCommandAsync(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                return string.Join(",", dates);
            });
        }

        [HttpGet]
        public async Task<string> DbCommandExecuteScalarAsync() => await ExecuteDbCommandAsync(async command => (string)await command.ExecuteScalarAsync());

        [HttpGet]
        public async Task<string> DbCommandExecuteNonQueryAsync() => await ExecuteDbCommandAsync(async command =>
        {
            await command.ExecuteNonQueryAsync();
            return "done";
        });

        private string ExecuteCommand(Func<MySqlCommand, string> action) =>
            ExecuteCommandAsync(command => Task.FromResult(action(command))).GetAwaiter().GetResult();

        private async Task<string> ExecuteCommandAsync(Func<MySqlCommand, Task<string>> action)
        {
            string result;

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
            {
                connection.Open();
                result = await action(command);
            }

            return result;
        }

        private string ExecuteDbCommand(Func<DbCommand, string> action) => ExecuteCommand(action);

        private async Task<string> ExecuteDbCommandAsync(Func<DbCommand, Task<string>> action) =>
            await ExecuteCommandAsync(async x => await action(x));

        [HttpGet]
        public int MySqlParameterizedStoredProcedure(string procedureName, bool paramsWithAtSigns)
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

                return command.ExecuteNonQuery();
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

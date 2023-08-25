// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MySqlConnector;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MySql
{
    [Library]
    public class MySqlConnectorExerciser
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteReader()
        {
            ExecuteCommand(command =>
            {
                var dates = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                string res = string.Join(",", dates);
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteScalar()
        {
            ExecuteCommand(command =>
            {
                string res = (string)command.ExecuteScalar();
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteNonQuery() => ExecuteCommand(command =>
        {
            command.ExecuteNonQuery();
            ConsoleMFLogger.Info("done");
            return "done";
        });

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExecuteReaderAsync()
        {
            await ExecuteCommandAsync(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                string res = string.Join(",", dates);
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExecuteScalarAsync()
        {
            await ExecuteDbCommandAsync(async command =>
            {
                string res = (string)await command.ExecuteScalarAsync();
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExecuteNonQueryAsync()
        {
            await ExecuteDbCommandAsync(async command =>
            {
                await command.ExecuteNonQueryAsync();
                ConsoleMFLogger.Info("done");
                return "done";
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteReader()
        {
            ExecuteDbCommand(command =>
            {
                var dates = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                string res = string.Join(",", dates);
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteScalar()
        {
            ExecuteDbCommand(command =>
            {
                string res = (string)command.ExecuteScalar();
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteNonQuery()
        {
            ExecuteDbCommand(command =>
            {
                command.ExecuteNonQuery();
                ConsoleMFLogger.Info("done");
                return "done";
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task DbCommandExecuteReaderAsync()
        {
            await ExecuteDbCommandAsync(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                string res = string.Join(",", dates);
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task DbCommandExecuteScalarAsync()
        {
            await ExecuteDbCommandAsync(async command =>
            {
                string res = (string)await command.ExecuteScalarAsync();
                ConsoleMFLogger.Info(res);
                return res;
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task DbCommandExecuteNonQueryAsync()
        {
            await ExecuteDbCommandAsync(async command =>
            {
                await command.ExecuteNonQueryAsync();
                ConsoleMFLogger.Info("done");
                return "done";
            });
        }

        private const string Query = "SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1";

        private string ExecuteCommand(Func<MySqlCommand, string> action)
        {
            string result;

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(Query, connection))
            {
                connection.Open();
                result = action(command);
            }

            return result;
        }

        private async Task<string> ExecuteCommandAsync(Func<MySqlCommand, Task<string>> action)
        {
            string result;

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(Query, connection))
            {
                await connection.OpenAsync();
                result = await action(command);
            }

            return result;
        }

        private string ExecuteDbCommand(Func<DbCommand, string> action) => ExecuteCommand(action);

        private async Task ExecuteDbCommandAsync(Func<DbCommand, Task<string>> action)
        {
            await ExecuteCommandAsync(async x => await action(x));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void MySqlParameterizedStoredProcedure(string procedureName, bool paramsWithAtSigns)
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

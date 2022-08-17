// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using MySql.Data.MySqlClient;
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
                ConsoleMFLogger.Info(string.Join(",", dates));
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteScalar()
        {
            ExecuteCommand(command =>
            {
                ConsoleMFLogger.Info((string)command.ExecuteScalar());
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteNonQuery() => ExecuteCommand(command =>
        {
            command.ExecuteNonQuery();
            ConsoleMFLogger.Info("done");
        });

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteReaderAsync()
        {
            ExecuteCommand(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                ConsoleMFLogger.Info(string.Join(",", dates));
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteScalarAsync() => ExecuteDbCommand(async command =>
        {
            string res = (string)await command.ExecuteScalarAsync();
            ConsoleMFLogger.Info(res);
        });

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExecuteNonQueryAsync() => ExecuteDbCommand(async command =>
        {
            await command.ExecuteNonQueryAsync();
            ConsoleMFLogger.Info("done");
        });

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
                ConsoleMFLogger.Info(string.Join(",", dates));
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteScalar() => ExecuteDbCommand(command => ConsoleMFLogger.Info((string)command.ExecuteScalar()));

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteNonQuery() => ExecuteDbCommand(command =>
        {
            command.ExecuteNonQuery();
            ConsoleMFLogger.Info("done");
        });

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteReaderAsync()
        {
            ExecuteDbCommand(async command =>
            {
                var dates = new List<string>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
                ConsoleMFLogger.Info(string.Join(",", dates));
            });
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteScalarAsync() => ExecuteDbCommand(async command => ConsoleMFLogger.Info((string)await command.ExecuteScalarAsync()));

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void DbCommandExecuteNonQueryAsync() => ExecuteDbCommand(async command =>
        {
            await command.ExecuteNonQueryAsync();
            ConsoleMFLogger.Info("done");
        });

        private void ExecuteCommand(Action<MySqlCommand> action)
        {
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
            {
                connection.Open();
                action(command);
            }
        }

        private void ExecuteDbCommand(Action<DbCommand> action) => ExecuteCommand(action);

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

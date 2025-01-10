// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Data;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Threading;
using System.Data.Odbc;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql
{

    [Library]
    public class SystemDataOdbcExerciser : MsSqlExerciserBase
    {
        private static string _connectionString = MsSqlOdbcConfiguration.MsSqlOdbcConnectionString;

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string MsSql(string tableName)
        {
            var teamMembers = new List<string>();

            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();

                using (var command = new OdbcCommand(SelectPersonByFirstNameMsSql, connection))
                {

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (reader.NextResult())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new OdbcCommand(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new OdbcCommand(countSql, connection))
                {
                    var teamMemberCount = command.ExecuteScalar();
                }

                using (var command = new OdbcCommand(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQuery();
                }
            }

            return string.Join(",", teamMembers);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> MsSqlAsync(string tableName)
        {
            var teamMembers = new List<string>();

            using (var connection = new OdbcConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new OdbcCommand(SelectPersonByLastNameMsSql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (await reader.NextResultAsync())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new OdbcCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new OdbcCommand(countSql, connection))
                {
                    var teamMemberCount = await command.ExecuteScalarAsync();
                }

                using (var command = new OdbcCommand(deleteSql, connection))
                {
                    var deleteCount = await command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string MsSqlWithParameterizedQuery(bool paramsWithAtSign)
        {
            var teamMembers = new List<string>();

            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();

                using (var command = new OdbcCommand(SelectPersonByParameterizedFirstNameMsSql, connection))
                {
                    command.Parameters.Add(new OdbcParameter(paramsWithAtSign ? "@FN" : "FN", "O'Keefe"));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (reader.NextResult())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

            }

            return string.Join(",", teamMembers);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> MsSqlAsync_WithParameterizedQuery(bool paramsWithAtSign)
        {
            var teamMembers = new List<string>();

            using (var connection = new OdbcConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new OdbcCommand(SelectPersonByParameterizedLastNameMsSql, connection))
                {
                    command.Parameters.Add(new OdbcParameter(paramsWithAtSign ? "@LN" : "LN", "Lee"));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (await reader.NextResultAsync())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }
            }

            return string.Join(",", teamMembers);
        }

        //[LibraryMethod]
        //[Transaction]
        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public void MsSqlParameterizedStoredProcedure(string procedureNameWith, string procedureNameWithout)
        //{
        //    ExecuteParameterizedStoredProcedure(procedureNameWith, true);
        //    ExecuteParameterizedStoredProcedure(procedureNameWithout, false);
        //}

        //private int ExecuteParameterizedStoredProcedure(string procedureName, bool paramsWithAtSign)
        //{
        //    EnsureProcedure(procedureName, DbParameterData.MsSqlParameters);
        //    using (var connection = new OdbcConnection(_connectionString))
        //    using (var command = new OdbcCommand(procedureName, connection))
        //    {
        //        connection.Open();
        //        command.CommandType = CommandType.StoredProcedure;
        //        foreach (var parameter in DbParameterData.MsSqlParameters)
        //        {
        //            var paramName = paramsWithAtSign
        //                ? parameter.ParameterName
        //                : parameter.ParameterName.TrimStart('@');

        //            command.Parameters.Add(new OdbcParameter(paramName, parameter.Value));
        //        }

        //        return command.ExecuteNonQuery();
        //    }
        //}

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void OdbcParameterizedStoredProcedure(string procedureNameWith, string procedureNameWithout)
        {
            ExecuteOdbcParameterizedStoredProcedure(procedureNameWith, true);
            ExecuteOdbcParameterizedStoredProcedure(procedureNameWithout, false);
        }

        private void ExecuteOdbcParameterizedStoredProcedure(string procedureName, bool paramsWithAtSign)
        {
            EnsureProcedure(procedureName, DbParameterData.OdbcMsSqlParameters);

            var parameterPlaceholder = string.Join(",", DbParameterData.OdbcMsSqlParameters.Select(_ => "?"));

            using (var connection = new OdbcConnection(MsSqlOdbcConfiguration.MsSqlOdbcConnectionString))
            using (var command = new OdbcCommand($"{{call {procedureName}({parameterPlaceholder})}}", connection))
            {
                connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.OdbcMsSqlParameters)
                {
                    var paramName = paramsWithAtSign
                        ? parameter.ParameterName
                        : parameter.ParameterName.TrimStart('@');

                    command.Parameters.Add(new OdbcParameter(paramName, parameter.Value)); ;
                }

                command.ExecuteNonQuery();
            }
        }

        [LibraryMethod]
        public void CreateTable(string tableName)
        {
            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();

                var createTable = string.Format(CreatePersonTableMsSql, tableName);
                using (var command = new OdbcCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void DropTable(string tableName)
        {
            var dropTableSql = string.Format(DropPersonTableMsSql, tableName);

            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();

                using (var command = new OdbcCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void DropProcedure(string procedureName)
        {
            var dropProcedureSql = string.Format(DropProcedureSql, procedureName);

            using (var connection = new OdbcConnection(_connectionString))
            {
                connection.Open();

                using (var command = new OdbcCommand(dropProcedureSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void Wait(int millisecondsTimeOut)
        {
            Thread.Sleep(millisecondsTimeOut);
        }

        private void EnsureProcedure(string procedureName, DbParameter[] dbParameters)
        {
            var parameters = string.Join(", ", dbParameters.Select(x => $"{x.ParameterName} {x.DbTypeName}"));
            var statement = string.Format(CreateProcedureStatement, procedureName, parameters);
            using (var connection = new OdbcConnection(_connectionString))
            using (var command = new OdbcCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

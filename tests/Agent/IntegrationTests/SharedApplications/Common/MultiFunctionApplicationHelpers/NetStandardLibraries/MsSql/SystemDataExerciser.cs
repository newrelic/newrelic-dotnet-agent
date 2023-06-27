// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Threading;
using System.Data.OleDb;
using System.Data.Odbc;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql
{
    [Library]
    public class SystemDataExerciser
    {
        private const string InsertPersonMsSql = "INSERT INTO {0} (FirstName, LastName, Email) VALUES('Testy', 'McTesterson', 'testy@mctesterson.com')";
        private const string DeletePersonMsSql = "DELETE FROM {0} WHERE Email = 'testy@mctesterson.com'";
        private const string CountPersonMsSql = "SELECT COUNT(*) FROM {0} WITH(nolock)";
        private static readonly string CreateProcedureStatement = @"CREATE OR ALTER PROCEDURE [dbo].[{0}] {1} AS RETURN 0";
        private const string CreatePersonTableMsSql = "CREATE TABLE {0} (FirstName varchar(20) NOT NULL, LastName varchar(20) NOT NULL, Email varchar(50) NOT NULL)";
        private const string DropPersonTableMsSql = "IF (OBJECT_ID('{0}') IS NOT NULL) DROP TABLE {0}";
        private const string DropProcedureSql = "IF (OBJECT_ID('{0}') IS NOT NULL) DROP PROCEDURE {0}";

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string MsSql(string tableName)
        {
            var teamMembers = new List<string>();

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'John'", connection))
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

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = command.ExecuteScalar();
                }

                using (var command = new SqlCommand(deleteSql, connection))
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

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'John'", connection))
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

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = await command.ExecuteScalarAsync();
                }

                using (var command = new SqlCommand(deleteSql, connection))
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

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN", connection))
                {
                    command.Parameters.Add(new SqlParameter(paramsWithAtSign ? "@FN" : "FN", "O'Keefe"));
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
        public async Task<string> MsSqlAsync_WithParameterizedQuery(string tableName, bool paramsWithAtSign)
        {
            var teamMembers = new List<string>();

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN", connection))
                {
                    command.Parameters.Add(new SqlParameter(paramsWithAtSign ? "@FN" : "FN", "O'Keefe"));
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

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = await command.ExecuteScalarAsync();
                }

                using (var command = new SqlCommand(deleteSql, connection))
                {
                    var deleteCount = await command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int MsSqlParameterizedStoredProcedure(string procedureName, bool paramsWithAtSign)
        {
            EnsureProcedure(procedureName, DbParameterData.MsSqlParameters);
            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            using (var command = new SqlCommand(procedureName, connection))
            {
                connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.MsSqlParameters)
                {
                    var paramName = paramsWithAtSign
                        ? parameter.ParameterName
                        : parameter.ParameterName.TrimStart('@');

                    command.Parameters.Add(new SqlParameter(paramName, parameter.Value));
                }

                return command.ExecuteNonQuery();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int MsSqlParameterizedStoredProcedureUsingOdbcDriver(string procedureName, bool paramsWithAtSign)
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

                return command.ExecuteNonQuery();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public int MsSqlParameterizedStoredProcedureUsingOleDbDriver(string procedureName, bool paramsWithAtSign)
        {
            EnsureProcedure(procedureName, DbParameterData.OleDbMsSqlParameters);

            using (var connection = new OleDbConnection(MsSqlOleDbConfiguration.MsSqlOleDbConnectionString))
            using (var command = new OleDbCommand(procedureName, connection))
            {
                connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.OleDbMsSqlParameters)
                {
                    var paramName = paramsWithAtSign
                        ? parameter.ParameterName
                        : parameter.ParameterName.TrimStart('@');

                    command.Parameters.Add(new OleDbParameter(paramName, parameter.Value));
                }

                return command.ExecuteNonQuery();
            }
        }

        [LibraryMethod]
        public void CreateTable(string tableName)
        {
            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                var createTable = string.Format(CreatePersonTableMsSql, tableName);
                using (var command = new SqlCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void DropTable(string tableName)
        {
            var dropTableSql = string.Format(DropPersonTableMsSql, tableName);

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void DropProcedure(string procedureName)
        {
            var dropProcedureSql = string.Format(DropProcedureSql, procedureName);

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(dropProcedureSql, connection))
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
            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            using (var command = new SqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

#endif

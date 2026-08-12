// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql;

[Library]
public class MicrosoftDataSqlClientExerciser : MsSqlExerciserBase
{
    private static string _connectionString = MsSqlConfiguration.MsSqlConnectionString;


    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public string MsSql(string tableName)
    {
        var teamMembers = new List<string>();

        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(SelectPersonByFirstNameMsSql, connection))
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

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new SqlCommand(SelectPersonByLastNameMsSql, connection))
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

        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(SelectPersonByParameterizedFirstNameMsSql, connection))
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
    public async Task<string> MsSqlAsync_WithParameterizedQuery(bool paramsWithAtSign)
    {
        var teamMembers = new List<string>();

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new SqlCommand(SelectPersonByParameterizedLastNameMsSql, connection))
            {
                command.Parameters.Add(new SqlParameter(paramsWithAtSign ? "@LN" : "LN", "Lee"));
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
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void MsSqlParameterizedStoredProcedure(string procedureNameWith, string procedureNameWithout)
    {
        ExecuteParameterizedStoredProcedure(procedureNameWith, true);
        ExecuteParameterizedStoredProcedure(procedureNameWithout, false);
    }

    private void ExecuteParameterizedStoredProcedure(string procedureName, bool paramsWithAtSign)
    {
        EnsureProcedure(procedureName, DbParameterData.MsSqlParameters);

        using (var connection = new SqlConnection(_connectionString))
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

            command.ExecuteNonQuery();
        }
    }

    [LibraryMethod]
    public void CreateTable(string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
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

        using (var connection = new SqlConnection(_connectionString))
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

        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(dropProcedureSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    [LibraryMethod]
    public void CreateDatabaseAndTable(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            try
            {
                using (var command = new SqlCommand($"IF DB_ID('{databaseName}') IS NULL CREATE DATABASE [{databaseName}]", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException)
            {
                // database already exists - harmless
            }

            connection.ChangeDatabase(databaseName);

            using (var command = new SqlCommand($"IF OBJECT_ID('dbo.{tableName}') IS NULL CREATE TABLE {tableName} (FirstName varchar(20) NOT NULL)", connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqlCommand($"INSERT INTO {tableName} (FirstName) VALUES ('Switched')", connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    [LibraryMethod]
    public void DropTableInDatabase(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            connection.ChangeDatabase(databaseName);

            using (var command = new SqlCommand(string.Format(DropPersonTableMsSql, tableName), connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void MsSqlDatabaseSwitch(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            // Runs against the database named in the connection string.
            using (var command = new SqlCommand(SelectPersonByFirstNameMsSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }

            // Change the active database on the SAME open connection. This does not
            // change the connection string.
            connection.ChangeDatabase(databaseName);

            // Runs against the switched-to database.
            using (var command = new SqlCommand($"SELECT * FROM {tableName} WITH(nolock)", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void MsSqlDatabaseSwitchViaUseStatement(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(SelectPersonByFirstNameMsSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }

            // Change the active database with a raw USE statement rather than the
            // client API. MSSQL reflects this in SqlConnection.Database.
            using (var command = new SqlCommand($"USE [{databaseName}]", connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SqlCommand($"SELECT * FROM {tableName} WITH(nolock)", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }
        }
    }

#if NET8_0_OR_GREATER
    // ChangeDatabaseAsync is a .NET 5+ BCL API on DbConnection and does not exist on
    // .NET Framework, so this method is compiled out there. Test classes that drive it
    // must bind to a Core fixture only.
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task MsSqlDatabaseSwitchAsync(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new SqlCommand(SelectPersonByFirstNameMsSql, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) { }
            }

            await connection.ChangeDatabaseAsync(databaseName);

            using (var command = new SqlCommand($"SELECT * FROM {tableName} WITH(nolock)", connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) { }
            }
        }
    }
#endif

    [LibraryMethod]
    public void Wait(int millisecondsTimeOut)
    {
        Thread.Sleep(millisecondsTimeOut);
    }

#if NET10_0
        [LibraryMethod]
        public async Task MsSqlCreateStoredProcWithTempTable(string procedureName)
        {
            var createProcedure = $@"
                CREATE OR ALTER PROCEDURE {procedureName}
                AS
                BEGIN
                    -- Create a temporary table and insert data into it using SELECT INTO
                    SELECT 
                        Id,
                        FirstName
                    INTO #TempTable
                    FROM 
                        TeamMembers;

                    -- Select all rows from the temporary table
                    SELECT * FROM #TempTable;
                END;";

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(createProcedure, connection);
            await command.ExecuteNonQueryAsync();

        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task MsSqlStoredProcWithTempTable(string procedureName)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = System.Data.CommandType.StoredProcedure;

            // execute the command
            await using var reader = await command.ExecuteReaderAsync();

        }

        [LibraryMethod]
        public async Task MsSqlDropStoredProcWithTempTable(string procedureName)
        {
            var dropProcedureSql = string.Format(DropProcedureSql, procedureName);
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(dropProcedureSql, connection);
            await command.ExecuteNonQueryAsync();
        }
#endif

    private void EnsureProcedure(string procedureName, DbParameter[] dbParameters)
    {
        var parameters = string.Join(", ", dbParameters.Select(x => $"{x.ParameterName} {x.DbTypeName}"));
        var statement = string.Format(CreateProcedureStatement, procedureName, parameters);
        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(statement, connection))
        {
            connection.Open();
            command.ExecuteNonQuery();
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public string MsSqlWithLongQuery()
    {
        var longQuery = GenerateLongSqlQuery();
            
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(longQuery, connection))
            {
                // Execute the query - it will return no results because the WHERE conditions won't match
                using (var reader = command.ExecuteReader())
                {
                    // Read through any results (there shouldn't be any)
                    while (reader.Read())
                    {
                        // Just iterate, don't need to process results
                    }
                }
            }
        }

        return "LongQueryExecuted";
    }
}

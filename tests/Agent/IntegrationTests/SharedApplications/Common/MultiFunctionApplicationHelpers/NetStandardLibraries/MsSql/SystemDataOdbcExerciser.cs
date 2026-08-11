// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
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
public class SystemDataOdbcExerciser : MsSqlExerciserBase
{
    private static string _connectionString = MsSqlOdbcConfiguration.MsSqlOdbcConnectionString;
    // used only by CreateDatabaseAndTable to create a database, since ODBC can't create a database directly
    private static string _sqlConnectionString = MsSqlConfiguration.MsSqlConnectionString;

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

    // odbc can't create a database directly, so use the _sqlConnectionString to connect to the default database and create the new database
    [LibraryMethod]
    public void CreateDatabaseAndTable(string databaseName, string tableName)
    {
        using (var connection = new SqlConnection(_sqlConnectionString))
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
        using (var connection = new OdbcConnection(_connectionString))
        {
            connection.Open();
            connection.ChangeDatabase(databaseName);

            using (var command = new OdbcCommand(string.Format(DropPersonTableMsSql, tableName), connection))
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
        using (var connection = new OdbcConnection(_connectionString))
        {
            connection.Open();

            // Runs against the database named in the connection string.
            using (var command = new OdbcCommand(SelectPersonByFirstNameMsSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }

            // Change the active database on the SAME open connection. This does not
            // change the connection string.
            connection.ChangeDatabase(databaseName);

            // Runs against the switched-to database.
            using (var command = new OdbcCommand($"SELECT * FROM {tableName} WITH(nolock)", connection))
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
        using (var connection = new OdbcConnection(_connectionString))
        {
            connection.Open();

            using (var command = new OdbcCommand(SelectPersonByFirstNameMsSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }

            // Change the active database with a raw USE statement rather than the
            // client API. The SQL Server ODBC driver reflects this in .Database.
            using (var command = new OdbcCommand($"USE [{databaseName}]", connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new OdbcCommand($"SELECT * FROM {tableName} WITH(nolock)", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
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

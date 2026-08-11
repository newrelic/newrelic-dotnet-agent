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

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MySql;

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
        using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
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
    public void CreateAndExecuteStoredProcedures(string procedureNameWith, string procedureNameWithout)
    {
        CreateProcedure(procedureNameWith);
        ExecuteProcedure(procedureNameWith, true);
        CreateProcedure(procedureNameWithout);
        ExecuteProcedure(procedureNameWithout, false);
    }

    private static void ExecuteProcedure(string procedureName, bool paramsWithAtSigns)
    {
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
        var dropStatement = $"DROP PROCEDURE IF EXISTS `{MySqlTestConfiguration.MySqlDbName}`.`{procedureName}`;";

        // Setup-only operation: retry on transient MySQL connection/packet-read faults with a fresh connection.
        // DROP IF EXISTS first so a retry is safe even if a prior attempt created the procedure server-side
        // before the client lost the response.
        MySqlRetryHelper.ExecuteWithRetry(() =>
        {
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            {
                connection.Open();
                using (var dropCommand = new MySqlCommand(dropStatement, connection))
                {
                    dropCommand.ExecuteNonQuery();
                }
                using (var command = new MySqlCommand(statement, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        });
    }

    [LibraryMethod]
    public void CreateDatabaseAndTable(string databaseName, string tableName)
    {
        // Setup-only operation: retry on transient MySQL connection/packet-read faults with a
        // fresh connection. Every statement uses IF NOT EXISTS, so a retry is safe even if a
        // prior attempt succeeded server-side before the client lost the response. The database
        // is intentionally left in place on cleanup so repeated runs against the long-lived
        // shared container reuse it instead of accumulating databases.
        MySqlRetryHelper.ExecuteWithRetry(() =>
        {
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            {
                connection.Open();

                using (var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {databaseName}", connection))
                {
                    command.ExecuteNonQuery();
                }

                connection.ChangeDatabase(databaseName);

                using (var command = new MySqlCommand($"CREATE TABLE IF NOT EXISTS {tableName} (FirstName varchar(20) NOT NULL)", connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new MySqlCommand($"INSERT INTO {tableName} (FirstName) VALUES ('Switched')", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        });
    }

    [LibraryMethod]
    public void DropTableInDatabase(string databaseName, string tableName)
    {
        MySqlRetryHelper.ExecuteWithRetry(() =>
        {
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            {
                connection.Open();
                connection.ChangeDatabase(databaseName);

                using (var command = new MySqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        });
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void MySqlDatabaseSwitch(string databaseName, string tableName)
    {
        using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
        {
            connection.Open();

            // Runs against the database named in the connection string.
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }

            // Change the active database on the SAME open connection. MySQL does this with
            // COM_INIT_DB on the live session, so the connection string does not change - which
            // is exactly the condition NR-576099 is about.
            connection.ChangeDatabase(databaseName);

            // Runs against the switched-to database. Identifiers are unquoted so the agent's SQL
            // parser reports the bare table name in the segment.
            using (var command = new MySqlCommand($"SELECT * FROM {tableName}", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) { }
            }
        }
    }
}
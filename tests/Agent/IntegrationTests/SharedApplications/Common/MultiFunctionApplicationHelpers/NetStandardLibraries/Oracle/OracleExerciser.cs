// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using OracleConfiguration = NewRelic.Agent.IntegrationTests.Shared.OracleConfiguration;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;


namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle
{
    [Library]
    public class OracleExerciser : IDisposable
    {
        private const string CreateHotelTableOracleSql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
                                                         "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
        private const string DropHotelTableOracleSql = "DROP TABLE {0}";
        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";
        private const string SelectFromUserTablesOracleSql = "SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1";


        private string _tableName;
        private string _storedProcedureName;

        [LibraryMethod]
        public void InitializeTable(string tableName)
        {
            _tableName = tableName;
            CreateTable();
        }

        [LibraryMethod]
        public void InitializeStoredProcedure(string storedProcName)
        {
            _storedProcedureName = storedProcName;
            CreateProcedure();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void ExerciseSync()
        {
            if (string.IsNullOrEmpty(_tableName))
                throw new Exception("Initialize table before exercising.");

            var connectionString = OracleConfiguration.OracleConnectionString;

            using var connection = new OracleConnection(connectionString);
            connection.Open();

            using (var command = new OracleCommand(SelectFromUserTablesOracleSql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var foo = reader.GetString(reader.GetOrdinal("DEGREE"));
                }
            }

            var insertSql = string.Format(InsertHotelOracleSql, _tableName);
            var countSql = string.Format(CountHotelOracleSql, _tableName);
            var deleteSql = string.Format(DeleteHotelOracleSql, _tableName);

            using (var command = new OracleCommand(insertSql, connection))
            {
                var insertCount = command.ExecuteNonQuery();
            }

            using (var command = new OracleCommand(countSql, connection))
            {
                var hotelCount = command.ExecuteScalar();
            }

            using (var command = new OracleCommand(deleteSql, connection))
            {
                var deleteCount = command.ExecuteNonQuery();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExerciseAsync()
        {
            if (string.IsNullOrEmpty(_tableName))
                throw new Exception("Initialize table before exercising.");

            var connectionString = OracleConfiguration.OracleConnectionString;

            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            using (var command = new OracleCommand(SelectFromUserTablesOracleSql, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var foo = reader.GetString(reader.GetOrdinal("DEGREE"));
                }
            }

            var insertSql = string.Format(InsertHotelOracleSql, _tableName);
            var countSql = string.Format(CountHotelOracleSql, _tableName);
            var deleteSql = string.Format(DeleteHotelOracleSql, _tableName);

            using (var command = new OracleCommand(insertSql, connection))
            {
                var insertCount = await command.ExecuteNonQueryAsync();
            }

            using (var command = new OracleCommand(countSql, connection))
            {
                var hotelCount = await command.ExecuteScalarAsync();
            }

            using (var command = new OracleCommand(deleteSql, connection))
            {
                var deleteCount = await command.ExecuteNonQueryAsync();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ExerciseStoredProcedure()
        {
            if (string.IsNullOrEmpty(_storedProcedureName))
                throw new Exception("Initialize stored procedure before exercising.");

            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            {
                using var command = new OracleCommand(_storedProcedureName, connection);

                await connection.OpenAsync();

                command.CommandType = CommandType.StoredProcedure;

                foreach (var p in DbParameterData.OracleParameters)
                {
                    command.Parameters.Add(p.ParameterName, p.Value);
                }

                await command.ExecuteNonQueryAsync();
            }
        }

        private void CreateTable()
        {
            var createTable = string.Format(CreateHotelTableOracleSql, _tableName);

            var connectionString = OracleConfiguration.OracleConnectionString;

            using var connection = new OracleConnection(connectionString);
            connection.Open();

            using var command = new OracleCommand(createTable, connection);
            command.ExecuteNonQuery();
        }

        private void DropTable()
        {
            if (!string.IsNullOrEmpty(_tableName))
            {
                var dropTableSql = string.Format(DropHotelTableOracleSql, _tableName);

                using var connection = new OracleConnection(OracleConfiguration.OracleConnectionString);
                connection.Open();

                using var command = new OracleCommand(dropTableSql, connection);
                command.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            DropTable();
            DropProcedure();
            _tableName = null;
            _storedProcedureName = null;
        }

        private readonly string createProcedureStatement = @"CREATE PROCEDURE {0} ({1}) IS BEGIN NULL; END {0};";
        private readonly string dropProcedureStatement = @"DROP PROCEDURE {0}";

        private void CreateProcedure()
        {
            var parameters = string.Join(", ", DbParameterData.OracleParameters.Select(x => $"{x.ParameterName} IN {x.DbTypeName}"));
            var statement = string.Format(createProcedureStatement, _storedProcedureName, parameters);
            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            using (var command = new OracleCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private void DropProcedure()
        {
            if (!string.IsNullOrEmpty(_storedProcedureName))
            {
                var statement = string.Format(dropProcedureStatement, _storedProcedureName);
                using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
                using (var command = new OracleCommand(statement, connection))
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}

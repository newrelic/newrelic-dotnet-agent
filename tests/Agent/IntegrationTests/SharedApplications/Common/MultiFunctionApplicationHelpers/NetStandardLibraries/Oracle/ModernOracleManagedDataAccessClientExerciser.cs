// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// can only test "modern" oracle on net472+ and .NET6/8/+
#if NET472_OR_GREATER || NET
using System;
using OracleConfiguration = NewRelic.Agent.IntegrationTests.Shared.OracleConfiguration;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;


namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle
{
    [Library]
    public class ModernOracleManagedDataAccessClientExerciser : IDisposable
    {
        private const string CreateHotelTableOracleSql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
                                                         "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
        private const string DropHotelTableOracleSql = "DROP TABLE {0}";
        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";


        private string _tableName;

        [LibraryMethod]
        public void Initialize(string tableName)
        {
            _tableName = tableName;
            CreateTable();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string ExerciseSync(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = OracleConfiguration.OracleConnectionString;

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();

                using (var command = new OracleCommand("SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("DEGREE")));
                        }
                    }
                }

                var insertSql = string.Format(InsertHotelOracleSql, tableName);
                var countSql = string.Format(CountHotelOracleSql, tableName);
                var deleteSql = string.Format(DeleteHotelOracleSql, tableName);

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

            return string.Join(",", teamMembers);
        }

        private void CreateTable()
        {
            var createTable = string.Format(CreateHotelTableOracleSql, _tableName);

            var connectionString = OracleConfiguration.OracleConnectionString;

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();

                using (var command = new OracleCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropTable()
        {
            var dropTableSql = string.Format(DropHotelTableOracleSql, _tableName);

            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            {
                connection.Open();

                using (var command = new OracleCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void Dispose()
        {
            DropTable();
        }
    }
}
#endif

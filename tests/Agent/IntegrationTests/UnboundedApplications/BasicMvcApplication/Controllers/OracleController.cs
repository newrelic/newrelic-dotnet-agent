// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using NewRelic.Agent.IntegrationTests.Shared;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class OracleController : Controller
    {
        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";

        [HttpGet]
        public string Oracle(string tableName)
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

        [HttpGet]
        public async Task<string> OracleAsync(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = OracleConfiguration.OracleConnectionString;

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new OracleCommand("SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public string EnterpriseLibraryOracle(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionStringSettings = new ConnectionStringSettings("OracleConnection", OracleConfiguration.OracleConnectionString, "Oracle.ManagedDataAccess.Client");
            var connectionStringsSection = new ConnectionStringsSection();
            connectionStringsSection.ConnectionStrings.Add(connectionStringSettings);
            var dictionaryConfigSource = new DictionaryConfigurationSource();
            dictionaryConfigSource.Add("connectionStrings", connectionStringsSection);
            var dbProviderFactory = new DatabaseProviderFactory(dictionaryConfigSource);
            var oracleDatabase = dbProviderFactory.Create("OracleConnection");

            using (var reader = oracleDatabase.ExecuteReader(CommandType.Text, "SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1"))
            {
                while (reader.Read())
                {
                    teamMembers.Add(reader.GetString(reader.GetOrdinal("DEGREE")));
                }
            }

            var insertSql = string.Format(InsertHotelOracleSql, tableName);
            var countSql = string.Format(CountHotelOracleSql, tableName);
            var deleteSql = string.Format(DeleteHotelOracleSql, tableName);

            var insertCount = oracleDatabase.ExecuteNonQuery(CommandType.Text, insertSql);
            var hotelCount = oracleDatabase.ExecuteScalar(CommandType.Text, countSql);
            var deleteCount = oracleDatabase.ExecuteNonQuery(CommandType.Text, deleteSql);

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public void OracleParameterizedStoredProcedure(string procedureName)
        {
            CreateProcedure(procedureName);

            try
            {
                using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
                using (var command = new OracleCommand(procedureName, connection))
                {
                    connection.Open();
                    command.CommandType = CommandType.StoredProcedure;

                    foreach (var p in DbParameterData.OracleParameters)
                    {
                        command.Parameters.Add(p.ParameterName, p.Value);
                    }

                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                DropProcedure(procedureName);
            }
        }

        private readonly string createProcedureStatment = @"CREATE PROCEDURE {0} ({1}) IS BEGIN NULL; END {0};";
        private readonly string dropProcedureStatement = @"DROP PROCEDURE {0}";

        private void CreateProcedure(string procedureName)
        {
            var parameters = string.Join(", ", DbParameterData.OracleParameters.Select(x => $"{x.ParameterName} IN {x.DbTypeName}"));
            var statement = string.Format(createProcedureStatment, procedureName, parameters);
            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            using (var command = new OracleCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private void DropProcedure(string procedureName)
        {
            var statement = string.Format(dropProcedureStatement, procedureName);
            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            using (var command = new OracleCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

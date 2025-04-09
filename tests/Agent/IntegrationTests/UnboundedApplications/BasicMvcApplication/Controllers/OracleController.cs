// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.Mvc;
using NewRelic.Api.Agent;
using Oracle.ManagedDataAccess.Client;

namespace BasicMvcApplication.Controllers
{
    public class OracleController : Controller
    {
        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";

        private const string CreateHotelTableOracleSql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
                                                         "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
        private const string DropHotelTableOracleSql = "DROP TABLE {0}";


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
        public void CreateTable(string tableName)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction(); // this is a setup method, don't need a transaction for it

            CreateTableInternal(tableName);
        }

        [HttpGet]
        public void DropTable(string tableName)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction(); // this is a teardown method, don't need a transaction for it

            DropTableInternal(tableName);
        }

        private void CreateTableInternal(string tableName)
        {
            var createTable = string.Format(CreateHotelTableOracleSql, tableName);
            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            {
                connection.Open();

                using (var command = new OracleCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropTableInternal(string tableName)
        {
            var dropTableSql = string.Format(DropHotelTableOracleSql, tableName);

            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            {
                connection.Open();

                using (var command = new OracleCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

    }
}

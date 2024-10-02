// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class OracleController : Controller
    {
        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";

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
    }
}

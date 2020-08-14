// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class OracleBasicMvcFixture : RemoteApplicationFixture
    {
        private const string CreateHotelTableOracleSql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
                                                         "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
        private const string DropHotelTableOracleSql = "DROP TABLE {0}";

        private readonly string _tableName;
        public string TableName
        {
            get { return _tableName; }
        }

        public OracleBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
            _tableName = GenerateTableName();
            CreateTable();
        }

        public void GetOracle()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/Oracle?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetOracleAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/OracleAsync?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetEnterpriseLibraryOracle()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/EnterpriseLibraryOracle?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void OracleParameterizedStoredProcedure(string procedureName)
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/OracleParameterizedStoredProcedure?procedureName={procedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        private static string GenerateTableName()
        {
            //Oracle tables must start w/ character and be <= 30 length. Table name = H{tableId}
            var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
            return $"h{tableId}";
        }

        private void CreateTable()
        {
            var createTable = string.Format(CreateHotelTableOracleSql, TableName);
            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
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
            var dropTableSql = string.Format(DropHotelTableOracleSql, TableName);

            using (var connection = new OracleConnection(OracleConfiguration.OracleConnectionString))
            {
                connection.Open();

                using (var command = new OracleCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DropTable();
        }
    }
}

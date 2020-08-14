// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.SqlClient;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MsSqlBasicMvcFixture : RemoteApplicationFixture, IMsSqlClientFixture
    {

        private const string CreatePersonTableMsSql = "CREATE TABLE {0} (FirstName varchar(20) NOT NULL, LastName varchar(20) NOT NULL, Email varchar(50) NOT NULL)";
        private const string DropPersonTableMsSql = "IF(OBJECT_ID('{0}') IS NOT NULL) DROP TABLE {0}";
        private const string DropProcedureSql = "IF(OBJECT_ID('{0}') IS NOT NULL) DROP PROCEDURE {0}";

        public string TableName { get; }

        public string ProcedureName { get; }

        public MsSqlBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
            TableName = GenerateTableName();
            ProcedureName = GenerateProcedureName();

            CreateTable();
            //The procedure is created in the controller action
        }

        public void GetMsSql()
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSql?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMsSqlAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSqlAsync?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMsSql_WithParameterizedQuery(bool paramsWithAtSign)
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSql_WithParameterizedQuery?tableName={TableName}&paramsWithAtSign={paramsWithAtSign}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMsSqlAsync_WithParameterizedQuery(bool paramsWithAtSign)
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSqlAsync_WithParameterizedQuery?tableName={TableName}&paramsWithAtSign={paramsWithAtSign}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetEnterpriseLibraryMsSql()
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/EnterpriseLibraryMsSql?tableName={TableName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMsSqlParameterizedStoredProcedure(bool paramsWithAtSign)
        {
            var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSqlParameterizedStoredProcedure?procedureName={ProcedureName}&paramsWithAtSign={paramsWithAtSign}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        private static string GenerateTableName()
        {
            var tableId = Guid.NewGuid().ToString("N").ToLower();
            return $"person{tableId}";
        }

        private static string GenerateProcedureName()
        {
            var procId = Guid.NewGuid().ToString("N").ToLower();
            return $"pTestProc{procId}";
        }


        private void CreateTable()
        {
            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                var createTable = string.Format(CreatePersonTableMsSql, TableName);
                using (var command = new SqlCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropTable()
        {
            var dropTableSql = string.Format(DropPersonTableMsSql, TableName);

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DropProcedure()
        {
            var dropProcedureSql = string.Format(DropProcedureSql, ProcedureName);

            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(dropProcedureSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            DropTable();
            DropProcedure();
        }
    }
}

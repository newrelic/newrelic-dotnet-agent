// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using MySqlConnector;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MySqlConnectorBasicMvcFixture : RemoteApplicationFixture
    {
        public string ProcedureName { get; }

        public MySqlConnectorBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
            ProcedureName = GenerateProcedureName();
        }

        public void GetMySql()
        {
            var address = $"http://{DestinationServerName}:{Port}/MySqlConnector/MySql";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMySqlAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/MySqlConnector/MySqlAsync";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMySqlParameterizedStoredProcedure(bool paramsWithAtSigns)
        {
            var address = $"http://{DestinationServerName}:{Port}/MySqlConnector/MySqlParameterizedStoredProcedure?procedureName={ProcedureName}&paramsWithAtSigns={paramsWithAtSigns}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void DropMySqlStoredProcedure(string procedureName)
        {
            var address = $"http://{DestinationServerName}:{Port}/MySqlConnector/MySqlDropProcedure?procedureName={ProcedureName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }

        }

        private static string GenerateProcedureName()
        {
            var procId = Guid.NewGuid().ToString("N").ToLower();
            return $"pTestProc{procId}";
        }

        private void DropProcedure()
        {
            var dropProcedureSql = $"DROP PROCEDURE IF EXISTS {ProcedureName}";

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            {
                connection.Open();

                using (var command = new MySqlCommand(dropProcedureSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DropProcedure();
        }
    }
}

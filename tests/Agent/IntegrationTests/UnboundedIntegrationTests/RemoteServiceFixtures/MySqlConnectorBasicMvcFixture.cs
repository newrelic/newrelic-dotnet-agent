// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Runtime.CompilerServices;
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

        public void GetExecuteNonQuery() => GetUrl();
        public void GetExecuteNonQueryAsync() => GetUrl();
        public void GetExecuteReader() => GetUrl();
        public void GetExecuteReaderAsync() => GetUrl();
        public void GetExecuteScalar() => GetUrl();
        public void GetExecuteScalarAsync() => GetUrl();
        public void GetDbCommandExecuteNonQuery() => GetUrl();
        public void GetDbCommandExecuteNonQueryAsync() => GetUrl();
        public void GetDbCommandExecuteReader() => GetUrl();
        public void GetDbCommandExecuteReaderAsync() => GetUrl();
        public void GetDbCommandExecuteScalar() => GetUrl();
        public void GetDbCommandExecuteScalarAsync() => GetUrl();

        public void GetMySqlParameterizedStoredProcedure(bool paramsWithAtSigns) => GetUrl("MySqlParameterizedStoredProcedure?procedureName={ProcedureName}&paramsWithAtSigns={paramsWithAtSigns}");
        public void DropMySqlStoredProcedure(string procedureName) => GetUrl("MySqlDropProcedure?procedureName={ProcedureName}");

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

        private void GetUrl([CallerMemberName] string pathSuffix = null)
        {
            var address = $"http://{DestinationServerName}:{Port}/MySqlConnector/{pathSuffix.Replace("Get", "")}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }
    }
}

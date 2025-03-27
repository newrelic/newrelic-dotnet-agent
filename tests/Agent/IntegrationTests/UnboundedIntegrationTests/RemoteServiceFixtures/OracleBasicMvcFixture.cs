// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class OracleBasicMvcFixture : RemoteApplicationFixture
    {
        public string TableName { get; }

        public OracleBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
        {
            TableName = GenerateTableName();
        }

        public void CreateTable()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/CreateTable?tableName={TableName}";

            GetAndAssertStatusCode(address, HttpStatusCode.OK);
        }

        public void DropTable()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/DropTable?tableName={TableName}";

            GetAndAssertStatusCode(address, HttpStatusCode.OK);
        }
        public void GetEnterpriseLibraryOracle()
        {
            var address = $"http://{DestinationServerName}:{Port}/Oracle/EnterpriseLibraryOracle?tableName={TableName}";

            GetStringAndAssertIsNotNull(address);
        }

        private static string GenerateTableName()
        {
            //Oracle tables must start w/ character and be <= 30 length. Table name = H{tableId}
            var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
            return $"h{tableId}";
        }
    }
}

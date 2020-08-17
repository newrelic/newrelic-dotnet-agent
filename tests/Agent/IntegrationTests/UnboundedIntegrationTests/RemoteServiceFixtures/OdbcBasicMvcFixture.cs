// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using Xunit;

public class OdbcBasicMvcFixture : MsSqlBasicMvcFixture
{
    public void GetMsSqlParameterizedStoredProcedureUsingOdbcDriver(bool paramsWithAtSign)
    {
        var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSqlParameterizedStoredProcedureUsingOdbcDriver?procedureName={ProcedureName}&paramsWithAtSign={paramsWithAtSign}";

        using (var webClient = new WebClient())
        {
            var responseBody = webClient.DownloadString(address);
            Assert.NotNull(responseBody);
        }
    }
}

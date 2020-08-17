// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using Xunit;

public class OleDbBasicMvcFixture : MsSqlBasicMvcFixture
{
    public override string TestSettingCategory { get { return "MSSQLOleDbTests"; } }

    public void GetMsSqlParameterizedStoredProcedureUsingOleDbDriver(bool paramsWithAtSign)
    {
        var address = $"http://{DestinationServerName}:{Port}/MsSql/MsSqlParameterizedStoredProcedureUsingOleDbDriver?procedureName={ProcedureName}&paramsWithAtSign={paramsWithAtSign}";

        using (var webClient = new WebClient())
        {
            var responseBody = webClient.DownloadString(address);
            Assert.NotNull(responseBody);
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System.Net.Http;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreBasicWebApiApplicationFixture : RemoteApplicationFixture
    {
        public AspNetCoreBasicWebApiApplicationFixture() : base(new RemoteService("AspNetCoreBasicWebApiApplication", "AspNetCoreBasicWebApiApplication.exe", "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void MakeExternalCallUsingHttpClient(string baseAddress, string path)
        {
            var address = $"http://{DestinationServerName}:{Port}/api/default/MakeExternalCallUsingHttpClient?baseAddress={baseAddress}&path={path}";
            GetStringAndAssertContains(address, "Worked");
        }

        public string GetTraceId()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/default/GetTraceId";
            return GetStringAndAssertIsNotNull(address);
        }
    }
}

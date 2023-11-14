// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcFrameworkFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AspNetCoreMvcFrameworkApplication";
        private const string ExecutableName = "AspNetCoreMvcFrameworkApplication.exe";
        private const string TargetFramework = "net462";

        public AspNetCoreMvcFrameworkFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, false, true))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/";
            GetStringAndAssertContains(address, "<html>");
        }

        public void GetCORSPreflight()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/About";

            using (var request = new HttpRequestMessage(HttpMethod.Options, address))
            {
                request.Headers.Add("Origin", "http://example.com");
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                }
            }
        }

        public void ThrowException()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/ThrowException";
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
        }

        public void GetWithData(string requestParameter)
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/Query?data={requestParameter}";
            GetStringAndAssertContains(address, "<html>");
        }

        public void GetCallAsyncExternal()
        {
            var address = $"http://{DestinationServerName}:{Port}/DetachWrapper/CallAsyncExternal";
            GetStringAndAssertEqual(address, "Worked");
        }
    }
}

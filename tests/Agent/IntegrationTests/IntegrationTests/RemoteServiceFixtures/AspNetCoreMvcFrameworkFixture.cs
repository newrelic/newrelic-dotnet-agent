// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
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
            var address = $"http://localhost:{Port}/";
            DownloadStringAndAssertContains(address, "<html>");
        }

        public void GetCORSPreflight()
        {
            var address = $"http://localhost:{Port}/Home/About";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Options, address))
                {
                    request.Headers.Add("Origin", "http://example.com");
                    request.Headers.Add("Access-Control-Request-Method", "GET");
                    request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

                    using (var response = client.SendAsync(request).Result)
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }

        public void ThrowException()
        {
            var address = $"http://localhost:{Port}/Home/ThrowException";
            using (var client = new HttpClient())
            {
                Assert.Throws<AggregateException>(() => client.GetStringAsync(address).Wait());
            }
        }

        public void GetWithData(string requestParameter)
        {
            var address = $"http://localhost:{Port}/Home/Query?data={requestParameter}";
            DownloadStringAndAssertContains(address, "<html>");
        }

        public void GetCallAsyncExternal()
        {
            var address = $"http://localhost:{Port}/DetachWrapper/CallAsyncExternal";
            DownloadStringAndAssertEqual(address, "Worked");
        }
    }
}

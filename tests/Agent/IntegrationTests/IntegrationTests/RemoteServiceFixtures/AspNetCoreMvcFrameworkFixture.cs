/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcFrameworkFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AspNetCoreMvcFrameworkApplication";
        private const string ExecutableName = "AspNetCoreMvcFrameworkApplication.exe";
        private const string TargetFramework = "net461";

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
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Method = "OPTIONS";
            request.Headers.Add("Origin", "http://example.com");
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

            var response = (HttpWebResponse)request.GetResponse();
            Assert.True(response.StatusCode == HttpStatusCode.NoContent);
        }

        public void ThrowException()
        {
            var address = $"http://localhost:{Port}/Home/ThrowException";
            var webClient = new WebClient();

            Assert.Throws<System.Net.WebException>(() => webClient.DownloadString(address));
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

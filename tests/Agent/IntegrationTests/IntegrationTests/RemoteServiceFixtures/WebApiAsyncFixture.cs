// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class WebApiAsyncFixture : RemoteApplicationFixture
    {

        public WebApiAsyncFixture() : base(new RemoteWebApplication("WebApiAsyncApplication", ApplicationType.Bounded))
        {
        }

        public void GetIoBoundNoSpecialAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/IoBoundNoSpecialAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/IoBoundConfigureAwaitFalseAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwait/CpuBoundTasksAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskRunBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskRunBlocked";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskFactoryStartNewBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskFactoryStartNewBlocked";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualNewThreadStartBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/NewThreadStartBlocked";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_AwaitedAsync()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Async_AwaitedAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_FireAndForget()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Async_FireAndForget";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_Sync()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Async_Sync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_AwaitedAsync()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Sync_AwaitedAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_FireAndForget()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Sync_FireAndForget";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_Sync()
        {
            var address = $"http://localhost:{Port}/AsyncFireAndForget/Sync_Sync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void ExecuteResponseTimeTestOperation(int delayDurationSeconds)
        {
            var address = $"http://localhost:{Port}/ResponseTime/CallsOtherMethod/{delayDurationSeconds}";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public string Request(HttpMethod method)
        {
            using (var httpClient = new HttpClient())
            {
                var requestMessage = new HttpRequestMessage(method, $"http://localhost:{Port}/AsyncFireAndForget/Sync_Sync");
                var result = httpClient.SendAsync(requestMessage).Result;

                return result.Content.ReadAsStringAsync().Result;
            }
        }

        public void Get404(string Path = "DoesNotExist")
        {
            try
            {
                new WebClient().DownloadString($"http://localhost:{Port}/{Path}");
            }
            catch (WebException) { }
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

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
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/IoBoundConfigureAwaitFalseAsync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/CpuBoundTasksAsync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskRunBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/TaskRunBlocked";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskFactoryStartNewBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/TaskFactoryStartNewBlocked";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetManualNewThreadStartBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/NewThreadStartBlocked";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_AwaitedAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Async_AwaitedAsync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_FireAndForget()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Async_FireAndForget";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetAsync_Sync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Async_Sync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_AwaitedAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Sync_AwaitedAsync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_FireAndForget()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Sync_FireAndForget";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void GetSync_Sync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Sync_Sync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void ExecuteResponseTimeTestOperation(int delayDurationSeconds)
        {
            var address = $"http://{DestinationServerName}:{Port}/ResponseTime/CallsOtherMethod/{delayDurationSeconds}";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public string Request(HttpMethod method)
        {
            using (var requestMessage = new HttpRequestMessage(method, $"http://{DestinationServerName}:{Port}/AsyncFireAndForget/Sync_Sync"))
            {
                var result = _httpClient.SendAsync(requestMessage).Result;
                return result.Content.ReadAsStringAsync().Result;
            }
        }

        public void Get404(string Path = "DoesNotExist")
        {
            GetAndAssertStatusCode($"http://{DestinationServerName}:{Port}/{Path}", HttpStatusCode.NotFound);
        }
    }
}

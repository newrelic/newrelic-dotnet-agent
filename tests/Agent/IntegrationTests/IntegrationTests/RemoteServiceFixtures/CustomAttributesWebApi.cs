// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class CustomAttributesWebApi : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "CustomAttributesWebApi";
        private const string ExecutableName = "NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi.exe";
        private const string TargetFramework = "net462";


        public readonly string ExpectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributes";

        public readonly string ExpectedTracedErrorPathLegacy = @"DotNet/Microsoft.Owin.Host.HttpListener.OwinHttpListener/StartProcessingRequest";

        public readonly string ExpectedTracedErrorPathAsync = @"WebTransaction/WebAPI/My/CustomErrorAttributes";

        public CustomAttributesWebApi() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }

        public void SpinupRequest()
        {
            var address = string.Format("http://{0}:{1}/api/IgnoreTransaction", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }

        public void WaitForStartup()
        {
            AgentLog.WaitForConnect(Timing.TimeToConnect);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        var address = string.Format("http://{0}:{1}/api/IgnoreTransaction", DestinationServerName, Port);
                        httpClient.Timeout = TimeSpan.FromSeconds(1);
                        Task.Run(() => httpClient.GetStringAsync(address)).Wait();
                        return;
                    }
                }
                catch (Exception)
                {
                    // try again in a bit, until we get a response in under 1 second (meaning server is up and stable) or 5 minutes passes
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }

            Assert.Fail(@"Did not receive a stable response (less than 1 second) after 5 minutes of attempts every 6 seconds.");
        }

        public void Get()
        {
            var address = string.Format("http://{0}:{1}/api/CustomAttributes", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }

        public void GetCustomErrorAttributes()
        {
            var address = string.Format("http://{0}:{1}/api/CustomErrorAttributes", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }

        public void Get404()
        {
            var address = string.Format("http://{0}:{1}/api/CustomErrorAttributes", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }

        public void GetKeyNull()
        {
            var address = string.Format("http://{0}:{1}/api/CustomAttributesKeyNull", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }

        public void GetValueNull()
        {
            var address = string.Format("http://{0}:{1}/api/CustomAttributesValueNull", DestinationServerName, Port);
            GetJsonAndAssertEqual(address, "success");
        }
    }

    public class HSMCustomAttributesWebApi : CustomAttributesWebApi
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }

    public class SecurityPoliciesCustomAttributesWebApi : CustomAttributesWebApi
    {
        public override string TestSettingCategory { get { return "CSP"; } }
    }
}

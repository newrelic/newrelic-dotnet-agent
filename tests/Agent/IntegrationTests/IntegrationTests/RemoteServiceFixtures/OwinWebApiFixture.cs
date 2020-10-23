// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class OwinWebApiFixture : RemoteApplicationFixture
    {
        // This base fixture class targets the Owin2 WebApi test application; its derived classes target Owin3 and 4
        private const string ApplicationDirectoryName = @"Owin2WebApi";
        private const string ExecutableName = @"Owin2WebApi.exe";
        private const string TargetFramework = "net451";

        public string AssemblyName;

        public OwinWebApiFixture() :
            this(ApplicationDirectoryName, ExecutableName, TargetFramework)
        {
            AssemblyName = @"Owin2WebApi";
        }
        protected OwinWebApiFixture(string ApplicationDirectoryName, string ExecutableName, string TargetFramework) : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = string.Format("http://{0}:{1}/api/Values", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<List<string>>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("value 1", result[0]);
            Assert.Equal("value 2", result[1]);
        }

        public void Get404()
        {
            var address = string.Format(@"http://{0}:{1}/api/404/", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            try
            {
                webClient.DownloadString(address);
                Assert.True(false, "Expected a 404 WebException.");
            }
            catch (WebException exception)
            {
                var httpWebResponse = exception.Response as HttpWebResponse;
                Assert.NotNull(httpWebResponse);
                Assert.Equal(HttpStatusCode.NotFound, httpWebResponse.StatusCode);
            }
        }

        public void GetId()
        {
            const string id = "5";
            var address = string.Format("http://{0}:{1}/api/Values/{2}", DestinationServerName, Port, id);
            DownloadJsonAndAssertEqual(address, id);
        }

        public void GetData()
        {
            const string expected = "mything";
            var address = string.Format("http://{0}:{1}/api/Values?data={2}", DestinationServerName, Port, expected);
            DownloadJsonAndAssertEqual(address, expected);
        }

        public void Post()
        {
            const string body = "stuff";
            var address = string.Format("http://{0}:{1}/api/Values/", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            webClient.Headers.Add("content-type", "application/json");

            var serializedBody = JsonConvert.SerializeObject(body);
            Contract.Assert(serializedBody != null);
            var resultJson = webClient.UploadString(address, serializedBody);
            var result = JsonConvert.DeserializeObject<string>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(body, result);
        }

        public void ThrowException()
        {
            var address = string.Format(@"http://{0}:{1}/api/ThrowException/", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }

        public void InvokeBadMiddleware()
        {
            var address = string.Format(@"http://{0}:{1}/AsyncAwait/UseBadMiddleware", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }

        public void Async()
        {
            var address = string.Format("http://{0}:{1}/api/Async", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<List<string>>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
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
        public void GetCustomMiddlewareIoBoundNoSpecialAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync";
            DownloadJsonAndAssertEqual(address, "Worked");
        }

        public void ErrorResponse()
        {
            var address = $"http://localhost:{Port}/AsyncAwait/ErrorResponse";

            var webClient = new WebClient();
            try
            {
                var response = webClient.DownloadString(address);
            }
            catch (WebException)
            {
                // This is expected behavior.  We need to catch this exception here to make sure it doesn't
                // bubble up to the test framework and fail the test.
            }
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

        public void GetBogusPath(string bogusPath)
        {
            var address = string.Format(@"http://{0}:{1}/{2}", DestinationServerName, Port, bogusPath);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }
    }

    public class Owin3WebApiFixture : OwinWebApiFixture
    {
        private const string ApplicationDirectoryName = @"Owin3WebApi";
        private const string ExecutableName = @"Owin3WebApi.exe";
        private const string TargetFramework = "net451";

        public Owin3WebApiFixture()
            : base(ApplicationDirectoryName, ExecutableName, TargetFramework)
        {
            AssemblyName = @"Owin3WebApi";
        }
    }
    public class Owin4WebApiFixture : OwinWebApiFixture
    {
        private const string ApplicationDirectoryName = @"Owin4WebApi";
        private const string ExecutableName = @"Owin4WebApi.exe";
        private const string TargetFramework = "net451";

        public Owin4WebApiFixture()
            : base(ApplicationDirectoryName, ExecutableName, TargetFramework)
        {
            AssemblyName = @"Owin4WebApi";
        }
    }

    public class HSMOwinWebApiFixture : OwinWebApiFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }

}

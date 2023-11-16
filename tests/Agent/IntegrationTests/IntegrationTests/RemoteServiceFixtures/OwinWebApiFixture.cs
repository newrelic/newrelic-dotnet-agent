// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private const string TargetFramework = "net462";

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

            var result = GetJson<List<string>>(address);
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("value 1", result[0]);
            Assert.Equal("value 2", result[1]);
        }

        public void Get404()
        {
            var address = string.Format(@"http://{0}:{1}/api/404/", DestinationServerName, Port);

            GetAndAssertStatusCode(address, HttpStatusCode.NotFound);
        }

        public void GetId()
        {
            const string id = "5";
            var address = string.Format("http://{0}:{1}/api/Values/{2}", DestinationServerName, Port, id);
            GetJsonAndAssertEqual(address, id);
        }

        public void GetData()
        {
            const string expected = "mything";
            var address = string.Format("http://{0}:{1}/api/Values?data={2}", DestinationServerName, Port, expected);
            GetJsonAndAssertEqual(address, expected);
        }

        public void Post()
        {
            var address = string.Format("http://{0}:{1}/api/Values/", DestinationServerName, Port);
            PostImpl(address);
        }

        public void PostAsync()
        {
            var address = string.Format("http://{0}:{1}/AsyncAwait/SimplePostAsync", DestinationServerName, Port);
            PostImpl(address);
        }

        private void PostImpl(string address)
        {
            const string body = "stuff";

            using (var request = new HttpRequestMessage(HttpMethod.Post, address))
            {
                request.Headers.Referrer = new Uri("http://example.com/");
                request.Headers.Add("User-Agent", "FakeUserAgent");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Host = "fakehost:1234";
                request.Headers.Add("foo", "bar");

                var serializedBody = JsonConvert.SerializeObject(body);

                request.Content = new StringContent(serializedBody, Encoding.Default, "application/json");

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    var resultJson = response.Content.ReadAsStringAsync().Result;
                    var result = JsonConvert.DeserializeObject<string>(resultJson);
                    Assert.NotNull(result);
                    Assert.Equal(body, result);
                }
            }
        }

        public void ThrowException()
        {
            var address = string.Format(@"http://{0}:{1}/api/ThrowException/", DestinationServerName, Port);
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError, GetHeaders());
        }

        public void InvokeBadMiddleware()
        {
            var address = string.Format(@"http://{0}:{1}/AsyncAwait/UseBadMiddleware", DestinationServerName, Port);
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError, GetHeaders());
        }

        public void Async()
        {
            var address = string.Format("http://{0}:{1}/api/Async", DestinationServerName, Port);

            var result = GetJson<List<string>>(address, GetHeaders());
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
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
        public void GetCustomMiddlewareIoBoundNoSpecialAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync";
            GetJsonAndAssertEqual(address, "Worked");
        }

        public void ErrorResponse()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/ErrorResponse";

            GetAndAssertSuccessStatus(address, false, GetHeaders());
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

        public void GetBogusPath(string bogusPath)
        {
            var address = $@"http://{DestinationServerName}:{Port}/{bogusPath}";
            GetAndAssertStatusCode(address, HttpStatusCode.NotFound, GetHeaders());
        }

        public IEnumerable<KeyValuePair<string, string>> GetHeaders()
        {
            return new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("accept", "application/json"),
                new KeyValuePair<string, string>("referer", "http://example.com"),
                new KeyValuePair<string, string>("user-agent", "FakeUserAgent"),
                new KeyValuePair<string, string>("host", "fakehost"),
                new KeyValuePair<string, string>("foo", "bar")
            };
        }
    }

    public class Owin3WebApiFixture : OwinWebApiFixture
    {
        private const string ApplicationDirectoryName = @"Owin3WebApi";
        private const string ExecutableName = @"Owin3WebApi.exe";
        private const string TargetFramework = "net462";

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
        private const string TargetFramework = "net462";

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

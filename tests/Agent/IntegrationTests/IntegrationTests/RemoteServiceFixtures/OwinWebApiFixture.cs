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
            using (var client = new HttpClient())
            {
                var resultJson = client.GetStringAsync(address).Result;
                var result = JsonConvert.DeserializeObject<List<string>>(resultJson);

                Assert.NotNull(result);
                Assert.Equal(2, result.Count);
                Assert.Equal("value 1", result[0]);
                Assert.Equal("value 2", result[1]);
            }
        }

        public void Get404()
        {
            var address = string.Format(@"http://{0}:{1}/api/404/", DestinationServerName, Port);

            using (var client = new HttpClient())
            {
                using (var response = client.GetAsync(address).Result)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    }
                    else
                        Assert.True(false, "Expected a 404 status code.");
                }
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

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    request.Headers.Referrer = new Uri("http://example.com/");
                    request.Headers.Add("User-Agent", "FakeUserAgent");
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Host = "fakehost:1234";
                    request.Headers.Add("foo", "bar");

                    var serializedBody = JsonConvert.SerializeObject(body);

                    request.Content = new StringContent(serializedBody, Encoding.Default, "application/json");

                    using (var response = client.SendAsync(request).Result)
                    {
                        var resultJson = response.Content.ReadAsStringAsync().Result;
                        var result = JsonConvert.DeserializeObject<string>(resultJson);
                        Assert.NotNull(result);
                        Assert.Equal(body, result);
                    }
                }
            }
        }

        public void ThrowException()
        {
            var address = string.Format(@"http://{0}:{1}/api/ThrowException/", DestinationServerName, Port);
            using (var client = GetHttpClient())
            {
                Assert.Throws<AggregateException>(() => client.GetStringAsync(address).Wait());
            }
        }

        public void InvokeBadMiddleware()
        {
            var address = string.Format(@"http://{0}:{1}/AsyncAwait/UseBadMiddleware", DestinationServerName, Port);
            using (var client = GetHttpClient())
            {
                Assert.Throws<AggregateException>(() => client.GetStringAsync(address).Wait());
            }
        }

        public void Async()
        {
            var address = string.Format("http://{0}:{1}/api/Async", DestinationServerName, Port);

            using (var client = GetHttpClient())
            {
                var resultJson = client.GetStringAsync(address).Result;

                var result = JsonConvert.DeserializeObject<List<string>>(resultJson);

                Assert.NotNull(result);
                Assert.Equal(2, result.Count);
            }
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

            using (var client = GetHttpClient())
            {
                using (var response = client.GetAsync(address).Result)
                {
                    Assert.False(response.IsSuccessStatusCode);
                }
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
            using (var client = GetHttpClient())
            {
                Assert.Throws<AggregateException>(() => client.GetStringAsync(address).Wait());
            }
        }

        public HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("referer", "http://example.com");
            client.DefaultRequestHeaders.Add("user-agent", "FakeUserAgent");
            client.DefaultRequestHeaders.Add("host", "fakehost");
            client.DefaultRequestHeaders.Add("foo", "bar");

            return client;
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

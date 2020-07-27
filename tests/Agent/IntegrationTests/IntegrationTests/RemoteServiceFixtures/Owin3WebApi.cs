using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class Owin3WebApi : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = @"Owin3WebApi";
        private const String ExecutableName = @"Owin3WebApi.exe";
        private const String TargetFramework = "net451";

        public Owin3WebApi() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = String.Format("http://{0}:{1}/api/Values", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<List<String>>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("value 1", result[0]);
            Assert.Equal("value 2", result[1]);
        }

        public void Get404()
        {
            var address = String.Format(@"http://{0}:{1}/api/404/", DestinationServerName, Port);
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
            var address = String.Format("http://{0}:{1}/api/Values/{2}", DestinationServerName, Port, id);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<String>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(id, result);
        }

        public void GetData()
        {
            const string expected = "mything";
            var address = String.Format("http://{0}:{1}/api/Values?data={2}", DestinationServerName, Port, expected);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<String>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(expected, result);
        }

        public void Post()
        {
            const string body = "stuff";
            var address = String.Format("http://{0}:{1}/api/Values/", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            webClient.Headers.Add("content-type", "application/json");

            var serializedBody = JsonConvert.SerializeObject(body);
            Contract.Assert(serializedBody != null);
            var resultJson = webClient.UploadString(address, serializedBody);
            var result = JsonConvert.DeserializeObject<String>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(body, result);
        }

        public void ThrowException()
        {
            var address = String.Format(@"http://{0}:{1}/api/ThrowException/", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");
            Assert.Throws<System.Net.WebException>(() => webClient.DownloadString(address));
        }

        public void Async()
        {
            var address = String.Format("http://{0}:{1}/api/Async", DestinationServerName, Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<List<String>>(resultJson);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }
    }
}

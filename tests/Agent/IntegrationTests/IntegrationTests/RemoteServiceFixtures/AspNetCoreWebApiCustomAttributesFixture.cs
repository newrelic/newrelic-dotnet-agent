using System;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreWebApiCustomAttributesFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "AspNetCoreWebApiCustomAttributesApplication";
        private const string ExecutableName = "AspNetCoreWebApiCustomAttributesApplication.exe";

        public AspNetCoreWebApiCustomAttributesFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
        {
        }

        public void Get()
        {
            var address = string.Format("http://localhost:{0}/api/CustomAttributes", Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<string>(resultJson);

            Assert.Equal("success", result);
        }
    }

    public class HSMAspNetCoreWebApiCustomAttributesFixture : AspNetCoreWebApiCustomAttributesFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}

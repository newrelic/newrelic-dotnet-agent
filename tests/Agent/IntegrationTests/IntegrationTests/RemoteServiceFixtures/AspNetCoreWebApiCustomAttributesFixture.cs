using System;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreWebApiCustomAttributesFixture : RemoteApplicationFixture
    {
        private const String ApplicationDirectoryName = "AspNetCoreWebApiCustomAttributesApplication";
        private const String ExecutableName = "AspNetCoreWebApiCustomAttributesApplication.exe";

        public AspNetCoreWebApiCustomAttributesFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
        {
        }

        public void Get()
        {
            var address = String.Format("http://localhost:{0}/api/CustomAttributes", Port);
            var webClient = new WebClient();
            webClient.Headers.Add("accept", "application/json");

            var resultJson = webClient.DownloadString(address);
            var result = JsonConvert.DeserializeObject<String>(resultJson);

            Assert.Equal("success", result);
        }
    }

    public class HSMAspNetCoreWebApiCustomAttributesFixture : AspNetCoreWebApiCustomAttributesFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}

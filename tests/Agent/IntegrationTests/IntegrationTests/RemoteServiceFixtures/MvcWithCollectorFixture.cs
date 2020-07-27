using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class MvcWithCollectorFixture : HttpCollectorFixture
    {
        public MvcWithCollectorFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default";
            var webClient = new WebClient();

            var responseBody = webClient.DownloadString(address);

            Assert.NotNull(responseBody);
            Assert.Contains("Worked", responseBody);
        }
    }
}

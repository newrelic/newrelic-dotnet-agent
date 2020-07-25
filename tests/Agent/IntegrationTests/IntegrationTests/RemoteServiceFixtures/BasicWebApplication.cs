using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicWebApplication : RemoteApplicationFixture
    {
        public String ResponseBody { get; private set; }

        public BasicWebApplication() : base(new RemoteWebApplication("BasicWebApplication", ApplicationType.Bounded))
        {
            Actions
            (
                exerciseApplication: Get
            );
        }

        public void Get()
        {
            // Two additional considerations being tested here:
            // 1. Metric is named as "DefAult.aspx".ToLower() (or "default.aspx") to keep casing clean (AspPagesTransactionNameWrapper.cs)
            // 2. Prevent a server redirect - Server strips the .aspx suffix and redirects to just "Default" before matching on "Default.aspx"
            var address = String.Format("http://{0}:{1}/DefAult", DestinationServerName, Port);
            var webClient = new WebClient();

            ResponseBody = webClient.DownloadString(address);

            Assert.NotNull(ResponseBody);
            Assert.Contains("<html", ResponseBody);
        }
    }
}

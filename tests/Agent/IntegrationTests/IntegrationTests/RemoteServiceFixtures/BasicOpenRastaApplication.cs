using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicOpenRastaApplication : RemoteApplicationFixture
    {

        [CanBeNull]
        public String ResponseBody { get; private set; }

        public BasicOpenRastaApplication() : base(new RemoteWebApplication("OpenRastaWebApplication", ApplicationType.Bounded))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/home";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Contains("GET", result);
        }


    }
}

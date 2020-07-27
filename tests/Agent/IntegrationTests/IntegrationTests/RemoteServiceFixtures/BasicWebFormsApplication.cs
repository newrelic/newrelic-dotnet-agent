using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicWebFormsApplication : RemoteApplicationFixture
    {
        public BasicWebFormsApplication() : base(new RemoteWebApplication("BasicWebFormsApplication", ApplicationType.Bounded))
        {
            Actions
            (
                exerciseApplication: Get
            );
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebForm1.aspx";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
        }

        public void GetSlow()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebFormSlow.aspx";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
        }

        public void Get404()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebFormThatDoesNotExist.aspx";

            try
            {
                new WebClient().DownloadString(address);
            }
            catch (Exception e)
            {
                // swallow
            }
        }

        public void GetWithQueryString([NotNull] IEnumerable<KeyValuePair<String, String>> parameters, Boolean expectException)
        {
            var parametersAsStrings = parameters.Select(param => $"{param.Key}={param.Value}");
            var parametersAsString = String.Join("&", parametersAsStrings);
            var address = $"http://{DestinationServerName}:{Port}/WebForm1.aspx?{parametersAsString}";

            var exceptionOccurred = false;
            try
            {
                var result = new WebClient().DownloadString(address);
                Assert.NotNull(result);
            }
            catch
            {
                exceptionOccurred = true;
            }

            Assert.Equal(expectException, exceptionOccurred);
        }
    }
}

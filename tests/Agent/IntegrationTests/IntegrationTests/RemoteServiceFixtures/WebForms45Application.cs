using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class WebForms45Application : RemoteApplicationFixture
	{
		public WebForms45Application() : base(new RemoteWebApplication("WebForms45Application", ApplicationType.Bounded))
		{
		}

		public void Get()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default.aspx";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
		}

		public void GetWithQueryString([NotNull] IEnumerable<KeyValuePair<String, String>> parameters, Boolean expectException)
		{
			var parametersAsStrings = parameters.Select(param => $"{param.Key}={param.Value}");
			var parametersAsString = String.Join("&", parametersAsStrings);
            var address = $"http://{DestinationServerName}:{Port}/Default?{parametersAsString}";

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

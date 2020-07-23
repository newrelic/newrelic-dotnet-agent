using System;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class AspNetCoreMvcBasicRequestsFixture : RemoteApplicationFixture
	{
		private const String ApplicationDirectoryName = @"AspNetCoreMvcBasicRequestsApplication";
		private const String ExecutableName = @"AspNetCoreMvcBasicRequestsApplication.exe";
		public AspNetCoreMvcBasicRequestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true))
		{
		}

		[NotNull]
		public String Get()
		{
			var address = $"http://localhost:{Port}/";
			var webClient = new WebClient();

			var responseBody = webClient.DownloadString(address);

			Assert.NotNull(responseBody);
			Assert.Contains("<html>", responseBody);

			return responseBody;
		}

		public void ThrowException()
		{
			var address = $"http://localhost:{Port}/Home/ThrowException";
			var webClient = new WebClient();
			Assert.Throws<System.Net.WebException>(() => webClient.DownloadString(address));
		}

		public string GetWithData(string requestParameter)
		{
			var address = $"http://localhost:{Port}/Home/Query?data={requestParameter}";
			var webClient = new WebClient();

			var result = webClient.DownloadString(address);

			Assert.NotNull(result);
			Assert.Contains("Query", result);

			return result;
		}
	}

	public class HSMAspNetCoreMvcBasicRequestsFixture : AspNetCoreMvcBasicRequestsFixture
	{
		public override string TestSettingCategory { get { return "HSM"; } }
	}
}

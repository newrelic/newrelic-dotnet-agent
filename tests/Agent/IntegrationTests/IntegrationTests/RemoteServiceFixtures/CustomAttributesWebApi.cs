using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class CustomAttributesWebApi : RemoteApplicationFixture
	{
		private const String ApplicationDirectoryName = "CustomAttributesWebApi";
		private const String ExecutableName = "NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi.exe";
		private const String TargetFramework = "net451";

		[NotNull]
		public readonly String ExpectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributes";
		[NotNull]
		public readonly String ExpectedTracedErrorPathLegacy = @"DotNet/Microsoft.Owin.Host.HttpListener.OwinHttpListener/StartProcessingRequest";
		[NotNull]
		public readonly String ExpectedTracedErrorPathAsync = @"WebTransaction/WebAPI/My/CustomErrorAttributes";

		public CustomAttributesWebApi() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
		{
		}

		public void SpinupRequest()
		{
			var address = String.Format("http://{0}:{1}/api/IgnoreTransaction", DestinationServerName, Port);
			var webClient = new WebClient();
			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}

		public void WaitForStartup()
		{
			AgentLog.WaitForConnect(Timing.TimeToConnect);

			var stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
			{
				try
				{
					var httpClient = new HttpClient();
					var address = String.Format("http://{0}:{1}/api/IgnoreTransaction", DestinationServerName, Port);
					httpClient.Timeout = TimeSpan.FromSeconds(1);
					Task.Run(() => httpClient.GetStringAsync(address)).Wait();
					return;
				}
				catch (Exception)
				{
					// try again in a bit, until we get a response in under 1 second (meaning server is up and stable) or 5 minutes passes
					Thread.Sleep(TimeSpan.FromSeconds(5));
				}
			}

			Assert.True(false, @"Did not receive a stable response (less than 1 second) after 5 minutes of attempts every 6 seconds.");
		}

		public void Get()
		{
			var address = String.Format("http://{0}:{1}/api/CustomAttributes", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}

		public void GetCustomErrorAttributes()
		{
			var address = String.Format("http://{0}:{1}/api/CustomErrorAttributes", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}

		public void Get404()
		{
			var address = String.Format("http://{0}:{1}/api/CustomErrorAttributes", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}

		public void GetKeyNull()
		{
			var address = String.Format("http://{0}:{1}/api/CustomAttributesKeyNull", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}

		public void GetValueNull()
		{
			var address = String.Format("http://{0}:{1}/api/CustomAttributesValueNull", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("success", result);
		}
	}

	public class HSMCustomAttributesWebApi : CustomAttributesWebApi
	{
		public override string TestSettingCategory { get { return "HSM"; } }
	}
}

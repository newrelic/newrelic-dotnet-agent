using System;
using System.Collections.Generic;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Models;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class HttpCollectorFixture : RemoteApplicationFixture
	{
		internal RemoteWebApplication HttpCollectorApplication { get; set; }

		public HttpCollectorFixture(RemoteApplication application)
			: base (application)
		{
			HttpCollectorApplication = new RemoteWebApplication("HttpCollector", ApplicationType.Bounded);
			HttpCollectorApplication.CopyToRemote();
			HttpCollectorApplication.Start(String.Empty);

			Actions(
				setupConfiguration: () =>
				{
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "ssl", "false");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "host", HttpCollectorApplication.DestinationServerName);
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "port", HttpCollectorApplication.Port);
				},
				exerciseApplication: () =>
				{
					WarmUpCollector();
				}
			);
		}

		public String WarmUpCollector()
		{
			var address = $"http://{HttpCollectorApplication.DestinationServerName}:{HttpCollectorApplication.Port}/agent_listener/WarmUpCollector";
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
			return result;
		}

		public IEnumerable<CollectedRequest> GetCollectedRequests()
		{
			var address = $"http://{HttpCollectorApplication.DestinationServerName}:{HttpCollectorApplication.Port}/agent_listener/CollectedRequests";
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
			var collectedRequests = JsonConvert.DeserializeObject<List<CollectedRequest>>(result);
			return collectedRequests;
		}

		public void TriggerThreadProfile()
		{
			var address = $"http://{HttpCollectorApplication.DestinationServerName}:{HttpCollectorApplication.Port}/agent_listener/TriggerThreadProfile";
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
		}

		public override void Dispose()
		{
			HttpCollectorApplication.Shutdown();
			HttpCollectorApplication.Dispose();
			base.Dispose();
		}
	}
}

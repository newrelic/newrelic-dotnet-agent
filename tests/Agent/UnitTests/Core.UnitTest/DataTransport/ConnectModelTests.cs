using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
	[TestFixture]
	public class ConnectModelTests
	{
		[Test]
		public void serializes_correctly()
		{
			var config = Mock.Create<IConfiguration>();

			using (new ConfigurationAutoResponder(config))
			{
				var utilitizationConfig = new UtilitizationConfig("my-host", 1, 2048);
				var connectModel = new ConnectModel
					(
					1,
					"dotnet",
					"myHost",
					new[] {"name1", "name2"},
					"1.0",
					0,
					new SecuritySettingsModel(true, new TransactionTraceSettingsModel("raw")),
					true,
					"myIdentifier",
					new[] {new Label("type1", "value1")},
					new JavascriptAgentSettingsModel(true, "full"),
					new UtilizationSettingsModel(2, 3, "myHost2", null, new[] {new AwsVendorModel("myId", "myType", "myZone")}, utilitizationConfig),
					null
					);

				var json = JsonConvert.SerializeObject(connectModel);

				const String expectedJson = @"{""pid"":1,""language"":""dotnet"",""host"":""myHost"",""app_name"":[""name1"",""name2""],""agent_version"":""1.0"",""agent_version_timestamp"":0,""build_timestamp"":0,""security_settings"":{""capture_params"":true,""transaction_tracer"":{""record_sql"":""raw""}},""high_security"":true,""identifier"":""myIdentifier"",""labels"":[{""label_type"":""type1"",""label_value"":""value1""}],""settings"":{""browser_monitoring.loader_debug"":true,""browser_monitoring.loader"":""full""},""utilization"":{""metadata_version"":3,""logical_processors"":2,""total_ram_mib"":0,""hostname"":""myHost2"",""config"":{""hostname"":""my-host"",""logical_processors"":1,""total_ram_mib"":2048},""vendors"":{""aws"":{""id"":""myId"",""type"":""myType"",""zone"":""myZone""}}}}";
				Assert.AreEqual(expectedJson, json);
			}
		}
	}
}

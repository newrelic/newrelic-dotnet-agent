using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.DistributedTracing;

namespace NewRelic.Agent.Core.CrossAgentTests.RumTests
{
	//https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/rum_client_config.json
	[TestFixture]
	public class RumClientConfigTests
	{
		private IConfiguration _configuration;

		private IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		private ITransactionMetricNameMaker _transactionMetricNameMaker;

		private ITransactionAttributeMaker _transactionAttributeMaker;

		private IAttributeService _attributeService;

		private IAttributeDefinitionService _attribDefSvc;
		private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(true);
			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(() => _configuration);

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			_transactionAttributeMaker = Mock.Create<ITransactionAttributeMaker>();
			_attributeService = Mock.Create<IAttributeService>();

			_browserMonitoringScriptMaker = new BrowserMonitoringScriptMaker(configurationService, _transactionMetricNameMaker, _transactionAttributeMaker, _attributeService);
			_attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
		}

		[Test]
		public void JsonCanDeserialize()
		{
			JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
		}

		[Test]
		[TestCaseSource(typeof(RumClientConfigTests), nameof(TestCases))]
		public void Test(TestCase testCase)
		{
			// ARRANGE
			Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(testCase.LicenseKey);
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns("JSAGENT");
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentFile).Returns(testCase.ConnectReply.JsAgentFile);
			Mock.Arrange(() => _configuration.BrowserMonitoringBeaconAddress).Returns(testCase.ConnectReply.Beacon);
			Mock.Arrange(() => _configuration.BrowserMonitoringErrorBeaconAddress).Returns(testCase.ConnectReply.ErrorBeacon);
			Mock.Arrange(() => _configuration.BrowserMonitoringKey).Returns(testCase.ConnectReply.BrowserKey);
			Mock.Arrange(() => _configuration.BrowserMonitoringApplicationId).Returns(testCase.ConnectReply.ApplicationId);

			var transactionMetricName = GetTransactionMetricName(testCase.TransactionName);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
				.Returns(transactionMetricName);

			var attributes = new AttributeCollection();
			if (testCase.BrowserMonitoringAttributesEnabled)
			{
				testCase.UserAttributes.ForEach(attr => attributes.Add(Attribute.BuildCustomAttribute(attr.Key, attr.Value)));
			}
			Mock.Arrange(() => _transactionAttributeMaker.GetUserAndAgentAttributes(Arg.IsAny<ITransactionAttributeMetadata>()))
				.Returns(attributes);
			Mock.Arrange(() => _attributeService.FilterAttributes(Arg.IsAny<AttributeCollection>(), Arg.IsAny<AttributeDestinations>()))
				.Returns(attributes);

			ITimer timer = Mock.Create<ITimer>();
			var responseTime = TimeSpan.FromMilliseconds(testCase.ApplicationTimeMilliseconds);
			Mock.Arrange(() => timer.Duration).Returns(responseTime);

			ITransactionName name = TransactionName.ForWebTransaction(transactionMetricName.Prefix, transactionMetricName.UnPrefixedName);
			var priority = 0.5f;
			IInternalTransaction tx = new Transaction(_configuration, name, timer, DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);
			tx.TransactionMetadata.SetQueueTime(TimeSpan.FromMilliseconds(testCase.QueueTimeMilliseconds));
			testCase.UserAttributes.ForEach(attr => tx.TransactionMetadata.AddUserAttribute(attr.Key, attr.Value));
			tx.TransactionMetadata.SetCrossApplicationReferrerTripId("");
			// ACT
			var browserMonitoringScript = _browserMonitoringScriptMaker.GetScript(tx);

			// ASSERT
			var extractedConfigurationDataJson = Regex.Match(browserMonitoringScript, @"NREUM.info = (\{.+\})").Groups[1].Value;
			var actualConfigurationData = JsonConvert.DeserializeObject<ExpectedBrowserMonitoringConfigurationData>(extractedConfigurationDataJson);

			NrAssert.Multiple(
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.Agent, actualConfigurationData.Agent),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.ApplicationId, actualConfigurationData.ApplicationId),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.ApplicationTimeMilliseconds, actualConfigurationData.ApplicationTimeMilliseconds),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.Beacon, actualConfigurationData.Beacon),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.BrowserLicenseKey, actualConfigurationData.BrowserLicenseKey),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.ErrorBeacon, actualConfigurationData.ErrorBeacon),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.ObfuscatedTransactionName, actualConfigurationData.ObfuscatedTransactionName),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.ObfuscatedUserAttributes, actualConfigurationData.ObfuscatedUserAttributes),
				() => Assert.AreEqual(testCase.ExpectedConfigurationData.QueueTimeMilliseconds, actualConfigurationData.QueueTimeMilliseconds)
				);
		}

		private static TransactionMetricName GetTransactionMetricName(string transactionName)
		{
			var segments = transactionName.Split('/');
			var prefix = segments[0];
			var suffix = string.Join("/", segments.Skip(1));
			return new TransactionMetricName(prefix, suffix);
		}

		#region JSON test case data
		public class TestCase
		{
			[JsonProperty(PropertyName = "testname")]
			public readonly string TestName;
			[JsonProperty(PropertyName = "apptime_milliseconds")]
			public readonly int ApplicationTimeMilliseconds;
			[JsonProperty(PropertyName = "queuetime_milliseconds")]
			public readonly int QueueTimeMilliseconds;
			[JsonProperty(PropertyName = "browser_monitoring.attributes.enabled")]
			public readonly bool BrowserMonitoringAttributesEnabled;
			[JsonProperty(PropertyName = "transaction_name")]
			public readonly string TransactionName;
			[JsonProperty(PropertyName = "license_key")]
			public readonly string LicenseKey;
			[JsonProperty(PropertyName = "connect_reply")]
			public readonly ConnectReply ConnectReply;
			[JsonProperty(PropertyName = "user_attributes")]
			public readonly Dictionary<string, string> UserAttributes;
			[JsonProperty(PropertyName = "expected")]
			public readonly ExpectedBrowserMonitoringConfigurationData ExpectedConfigurationData;

			public override string ToString()
			{
				return TestName;
			}
		}

		public class ConnectReply
		{
			[JsonProperty(PropertyName = "beacon")]
			public readonly string Beacon;
			[JsonProperty(PropertyName = "browser_key")]
			public readonly string BrowserKey;
			[JsonProperty(PropertyName = "application_id")]
			public readonly string ApplicationId;
			[JsonProperty(PropertyName = "error_beacon")]
			public readonly string ErrorBeacon;
			[JsonProperty(PropertyName = "js_agent_file")]
			public readonly string JsAgentFile;
		}

		public class ExpectedBrowserMonitoringConfigurationData
		{
			[JsonProperty("beacon")]
			public string Beacon { get; set; }

			[JsonProperty("errorBeacon")]
			public string ErrorBeacon { get; set; }

			[JsonProperty("licenseKey")]
			public string BrowserLicenseKey { get; set; }

			[JsonProperty("applicationID")]
			public string ApplicationId { get; set; }

			[JsonProperty("transactionName")]
			public string ObfuscatedTransactionName { get; set; }

			[JsonProperty("queueTime")]
			public int QueueTimeMilliseconds { get; set; }

			[JsonProperty("applicationTime")]
			public int ApplicationTimeMilliseconds { get; set; }

			[JsonProperty("agent")]
			public string Agent { get; set; }

			[JsonProperty("atts", NullValueHandling = NullValueHandling.Ignore)]
			public string ObfuscatedUserAttributes { get; set; }
		}

		public static IEnumerable<TestCase[]> TestCases
		{
			get
			{
				var testCases = JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
				Assert.NotNull(testCases);
				return testCases
					.Where(testCase => testCase != null)
					.Select(testCase => new [] { testCase });
			}
		}

		private const string JsonTestCaseData = @"
[
  {
    ""testname"":""all fields present"",

    ""apptime_milliseconds"":5,
    ""queuetime_milliseconds"":3,
    ""browser_monitoring.attributes.enabled"":true,
    ""transaction_name"":""WebTransaction/brink/of/glory"",
    ""license_key"":""0000111122223333444455556666777788889999"",
    ""connect_reply"":
    {
      ""beacon"":""my_beacon"",
      ""browser_key"":""my_browser_key"",
      ""application_id"":""my_application_id"",
      ""error_beacon"":""my_error_beacon"",
      ""js_agent_file"":""my_js_agent_file""
    },
    ""user_attributes"":{""alpha"":""beta""},
    ""expected"":
    {
      ""beacon"":""my_beacon"",
      ""licenseKey"":""my_browser_key"",
      ""applicationID"":""my_application_id"",
      ""transactionName"":""Z1VSZENQX0JTUUZbXF4fUkJYX1oeXVQdVV9fQkk="",
      ""queueTime"":3,
      ""applicationTime"":5,
      ""atts"":""SxJREgtKE19AHEZAWkB5VBILExNMHhBHEAlLElFcQVlQEwgQUFdHURJNTQ=="",
      ""errorBeacon"":""my_error_beacon"",
      ""agent"":""my_js_agent_file""
    }
  },
  {
    ""testname"":""browser_monitoring.attributes.enabled disabled"",

    ""apptime_milliseconds"":5,
    ""queuetime_milliseconds"":3,
    ""browser_monitoring.attributes.enabled"":false,
    ""transaction_name"":""WebTransaction/brink/of/glory"",
    ""license_key"":""0000111122223333444455556666777788889999"",
    ""connect_reply"":
    {
      ""beacon"":""my_beacon"",
      ""browser_key"":""my_browser_key"",
      ""application_id"":""my_application_id"",
      ""error_beacon"":""my_error_beacon"",
      ""js_agent_file"":""my_js_agent_file""
    },
    ""user_attributes"":{""alpha"":""beta""},
    ""expected"":
    {
      ""beacon"":""my_beacon"",
      ""licenseKey"":""my_browser_key"",
      ""applicationID"":""my_application_id"",
      ""transactionName"":""Z1VSZENQX0JTUUZbXF4fUkJYX1oeXVQdVV9fQkk="",
      ""queueTime"":3,
      ""applicationTime"":5,
      ""atts"":""SxJREgtKE19AHEZAWkB5VBILExNMTw=="",
      ""errorBeacon"":""my_error_beacon"",
      ""agent"":""my_js_agent_file""
    }
  }
]
";

		#endregion JSON test case data
	}
}

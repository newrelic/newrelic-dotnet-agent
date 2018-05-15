using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

namespace NewRelic.Agent.Core.CrossAgentTests.RumTests
{
	//https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/rum_client_config.json
	[TestFixture]
	public class RumClientConfigTests
	{
		[NotNull]
		private IConfiguration _configuration;

		[NotNull]
		private IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		[NotNull]
		private ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private ITransactionAttributeMaker _transactionAttributeMaker;

		[NotNull]
		private IAttributeService _attributeService;

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
		}

		[Test]
		public void JsonCanDeserialize()
		{
			JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
		}

		[Test]
		[TestCaseSource(typeof(RumClientConfigTests), nameof(TestCases))]
		public void Test([NotNull] TestCase testCase)
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

			var attributes = new Attributes();
			if (testCase.BrowserMonitoringAttributesEnabled)
			{
				testCase.UserAttributes.ForEach(attr => attributes.Add(Attribute.BuildCustomAttribute(attr.Key, attr.Value)));
			}
			Mock.Arrange(() => _transactionAttributeMaker.GetUserAndAgentAttributes(Arg.IsAny<ITransactionAttributeMetadata>()))
				.Returns(attributes);
			Mock.Arrange(() => _attributeService.FilterAttributes(Arg.IsAny<Attributes>(), Arg.IsAny<AttributeDestinations>()))
				.Returns(attributes);

			ITimer timer = Mock.Create<ITimer>();
			var responseTime = TimeSpan.FromMilliseconds(testCase.ApplicationTimeMilliseconds);
			Mock.Arrange(() => timer.Duration).Returns(responseTime);

			ITransactionName name = new WebTransactionName(transactionMetricName.Prefix, transactionMetricName.UnPrefixedName);
			var priority = 0.5f;
			ITransaction tx = new Transaction(_configuration, name, timer, DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority);
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

		private static TransactionMetricName GetTransactionMetricName([NotNull] String transactionName)
		{
			var segments = transactionName.Split('/');
			var prefix = segments[0];
			var suffix = String.Join("/", segments.Skip(1));
			return new TransactionMetricName(prefix, suffix);
		}

		#region JSON test case data
		public class TestCase
		{
			[JsonProperty(PropertyName = "testname"), NotNull, UsedImplicitly]
			public readonly String TestName;
			[JsonProperty(PropertyName = "apptime_milliseconds"), UsedImplicitly]
			public readonly Int32 ApplicationTimeMilliseconds;
			[JsonProperty(PropertyName = "queuetime_milliseconds"), UsedImplicitly]
			public readonly Int32 QueueTimeMilliseconds;
			[JsonProperty(PropertyName = "browser_monitoring.attributes.enabled"), UsedImplicitly]
			public readonly Boolean BrowserMonitoringAttributesEnabled;
			[JsonProperty(PropertyName = "transaction_name"), NotNull, UsedImplicitly]
			public readonly String TransactionName;
			[JsonProperty(PropertyName = "license_key"), NotNull, UsedImplicitly]
			public readonly String LicenseKey;
			[JsonProperty(PropertyName = "connect_reply"), NotNull, UsedImplicitly]
			public readonly ConnectReply ConnectReply;
			[JsonProperty(PropertyName = "user_attributes"), NotNull, UsedImplicitly]
			public readonly Dictionary<String, String> UserAttributes;
			[JsonProperty(PropertyName = "expected"), NotNull, UsedImplicitly]
			public readonly ExpectedBrowserMonitoringConfigurationData ExpectedConfigurationData;

			public override String ToString()
			{
				return TestName;
			}
		}

		public class ConnectReply
		{
			[JsonProperty(PropertyName = "beacon"), NotNull, UsedImplicitly]
			public readonly String Beacon;
			[JsonProperty(PropertyName = "browser_key"), NotNull, UsedImplicitly]
			public readonly String BrowserKey;
			[JsonProperty(PropertyName = "application_id"), NotNull, UsedImplicitly]
			public readonly String ApplicationId;
			[JsonProperty(PropertyName = "error_beacon"), NotNull, UsedImplicitly]
			public readonly String ErrorBeacon;
			[JsonProperty(PropertyName = "js_agent_file"), NotNull, UsedImplicitly]
			public readonly String JsAgentFile;
		}

		public class ExpectedBrowserMonitoringConfigurationData
		{
			[JsonProperty("beacon")]
			[NotNull]
			public String Beacon { get; set; }

			[JsonProperty("errorBeacon")]
			[NotNull]
			public String ErrorBeacon { get; set; }

			[JsonProperty("licenseKey")]
			[NotNull]
			public String BrowserLicenseKey { get; set; }

			[JsonProperty("applicationID")]
			[NotNull]
			public String ApplicationId { get; set; }

			[JsonProperty("transactionName")]
			[NotNull]
			public String ObfuscatedTransactionName { get; set; }

			[JsonProperty("queueTime")]
			public Int32 QueueTimeMilliseconds { get; set; }

			[JsonProperty("applicationTime")]
			public Int32 ApplicationTimeMilliseconds { get; set; }

			[JsonProperty("agent")]
			[NotNull]
			public String Agent { get; set; }

			[JsonProperty("atts", NullValueHandling = NullValueHandling.Ignore)]
			[CanBeNull]
			public String ObfuscatedUserAttributes { get; set; }
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

		private const String JsonTestCaseData = @"
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

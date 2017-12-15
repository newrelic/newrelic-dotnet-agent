using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	[TestFixture]
	public class BrowserMonitoringScriptMakerTests
	{
		[NotNull]
		private BrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		[NotNull]
		private IConfiguration _configuration;

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
			Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license key");
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns("the agent");

			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(new TransactionMetricName("prefix", "suffix"));

			_transactionAttributeMaker = Mock.Create<ITransactionAttributeMaker>();
			_attributeService = Mock.Create<IAttributeService>();

			_browserMonitoringScriptMaker = new BrowserMonitoringScriptMaker(configurationService, _transactionMetricNameMaker, _transactionAttributeMaker, _attributeService);
		}

		[Test]
		public void GetScript_ReturnsValidScript_UnderNormalConditions()
		{
			var transaction = BuildTestTransaction(queueTime:TimeSpan.FromSeconds(1), applicationTime:TimeSpan.FromSeconds(2));

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			const String expectedScript = @"<script type=""text/javascript"">window.NREUM||(NREUM={});NREUM.info = {""beacon"":"""",""errorBeacon"":"""",""licenseKey"":"""",""applicationID"":"""",""transactionName"":""HBsGAwcLSlMeAx8FEQ=="",""queueTime"":1000,""applicationTime"":2000,""agent"":"""",""atts"":""""}</script><script type=""text/javascript"">the agent</script>";
			Assert.AreEqual(expectedScript, script);
		}

		[Test]
		public void GetScript_DefaultsToQueueTimeZero_IfQueueTimeIsNotSet()
		{
			var transaction = BuildTestTransaction(applicationTime: TimeSpan.FromSeconds(2));

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			const String expectedScript = @"<script type=""text/javascript"">window.NREUM||(NREUM={});NREUM.info = {""beacon"":"""",""errorBeacon"":"""",""licenseKey"":"""",""applicationID"":"""",""transactionName"":""HBsGAwcLSlMeAx8FEQ=="",""queueTime"":0,""applicationTime"":2000,""agent"":"""",""atts"":""""}</script><script type=""text/javascript"">the agent</script>";
			Assert.AreEqual(expectedScript, script);
		}

		[Test]
		public void GetScript_IncludesObfuscatedAttributes_IfAttributesReturnedFromAttributeService()
		{
			var mockAttributes = new Attributes();
			mockAttributes.Add(Attribute.BuildOriginalUrlAttribute("http://www.google.com"));
			mockAttributes.Add(Attribute.BuildCustomAttribute("foo", "bar"));

			Mock.Arrange(() => _transactionAttributeMaker.GetUserAndAgentAttributes(Arg.IsAny<ITransactionAttributeMetadata>()))
				.Returns(mockAttributes);
			Mock.Arrange(() => _attributeService.FilterAttributes(Arg.IsAny<Attributes>(), AttributeDestinations.JavaScriptAgent))
				.Returns(mockAttributes);

			var transaction = BuildTestTransaction(queueTime: TimeSpan.FromSeconds(1), applicationTime: TimeSpan.FromSeconds(2));
			var tripId = transaction.TransactionMetadata.CrossApplicationReferrerTripId ?? transaction.Guid;

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			var expectedFormattedAttributes = @"{""a"":{""original_url"":""http://www.google.com"",""nr.tripId"":" + $"\"{tripId}\"" + @"},""u"":{""foo"":""bar""}}";
			var expectedObfuscatedFormattedAttributes = Strings.ObfuscateStringWithKey(expectedFormattedAttributes, "license key");
			var actualObfuscatedFormattedAttributes = Regex.Match(script, @"""atts"":""([^""]+)""").Groups[1].Value;
			Assert.AreEqual(expectedObfuscatedFormattedAttributes, actualObfuscatedFormattedAttributes);
		}

		[Test]
		public void GetScript_ObfuscatesTransactionMetricNameWithLicenseKey()
		{
			var transaction = BuildTestTransaction();

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			var expectedObfuscatedTransactionMetricName = Strings.ObfuscateStringWithKey("prefix/suffix", "license key");
			var actualObfuscatedTransactionMetricName = Regex.Match(script, @"""transactionName"":""([^""]+)""").Groups[1].Value;
			Assert.AreEqual(expectedObfuscatedTransactionMetricName, actualObfuscatedTransactionMetricName);
		}

		[Test]
		public void GetScript_ReturnsNull_IfBrowserMonitoringJavaScriptAgentIsNull()
		{
			var transaction = BuildTestTransaction();
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns(null as String);

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			Assert.Null(script);
		}

		[Test]
		public void GetScript_ReturnsNull_IfBrowserMonitoringJavaScriptAgentIsEmpty()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns(String.Empty);
			var transaction = BuildTestTransaction();

			var script = _browserMonitoringScriptMaker.GetScript(transaction);

			Assert.Null(script);
		}

		[Test]
		public void GetScript_Throws_IfAgentLicenseKeyIsNull()
		{
			Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfAgentLicenseKeyIsEmpty()
		{
			Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(String.Empty);
			var transaction = BuildTestTransaction();

			Assert.Throws<Exception>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfBrowserMonitoringBeaconAddressIsNull()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringBeaconAddress).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfBrowserMonitoringErrorBeaconAddressIsNull()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringErrorBeaconAddress).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfBrowserMonitoringKeyIsNull()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringKey).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfBrowserMonitoringApplicationIdIsNull()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringApplicationId).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[Test]
		public void GetScript_Throws_IfBrowserMonitoringJavaScriptAgentFileIsNull()
		{
			Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentFile).Returns(null as String);
			var transaction = BuildTestTransaction();

			Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction));
		}

		[NotNull]
		private ITransaction BuildTestTransaction(TimeSpan? queueTime = null, TimeSpan? applicationTime = null)
		{
			var name = new WebTransactionName("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var guid = Guid.NewGuid().ToString();
			var time = applicationTime ?? TimeSpan.FromSeconds(1);

			ITimer timer = Mock.Create<ITimer>();
			Mock.Arrange(() => timer.Duration).Returns(time);

			var tx = new Transaction(_configuration, name, timer, DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());

			if (queueTime != null)
				tx.TransactionMetadata.SetQueueTime(queueTime.Value);

			return tx;
		}
	}
}

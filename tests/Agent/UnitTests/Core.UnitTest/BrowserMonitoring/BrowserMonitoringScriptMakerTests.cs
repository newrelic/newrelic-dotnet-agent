// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Core;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    [TestFixture]
    public class BrowserMonitoringScriptMakerTests
    {
        private BrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

        private IConfiguration _configuration;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;

        private ITransactionAttributeMaker _transactionAttributeMaker;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;
        private ConfigurationAutoResponder _configurationAutoResponder;

        private int _configVersion = 0;

        private void UpdateDefaultConfiguration()
        {
            _configVersion++;

            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ConfigurationVersion).Returns(_configVersion);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license key");
            Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns("the agent");
            Mock.Arrange(() => _configuration.CaptureBrowserMonitoringAttributes).Returns(true);
            Mock.Arrange(() => _configuration.CaptureAttributes).Returns(() => true);
        }

        [SetUp]
        public void SetUp()
        {
            UpdateDefaultConfiguration();

            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(() => _configuration);

            _configurationAutoResponder = new ConfigurationAutoResponder(_configuration);

            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(new TransactionMetricName("prefix", "suffix"));

            _transactionAttributeMaker = Mock.Create<ITransactionAttributeMaker>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _browserMonitoringScriptMaker = new BrowserMonitoringScriptMaker(configurationService, _transactionMetricNameMaker, _transactionAttributeMaker, _attribDefSvc);
        }

        [TearDown]
        public void TearDown()
        {
            _configurationAutoResponder?.Dispose();
        }

        [Test]
        public void GetScript_ReturnsValidScript_UnderNormalConditions()
        {
            UpdateDefaultConfiguration();
            Mock.Arrange(() => _configuration.CaptureAttributes).Returns(false);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));

            var transaction = BuildTestTransaction(queueTime: TimeSpan.FromSeconds(1), applicationTime: TimeSpan.FromSeconds(2));

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            const string expectedScript = @"<script type=""text/javascript"">window.NREUM||(NREUM={});NREUM.info = {""beacon"":"""",""errorBeacon"":"""",""licenseKey"":"""",""applicationID"":"""",""transactionName"":""HBsGAwcLSlMeAx8FEQ=="",""queueTime"":1000,""applicationTime"":2000,""agent"":"""",""atts"":""""}</script><script type=""text/javascript"">the agent</script>";
            Assert.AreEqual(expectedScript, script);
        }

        [Test]
        public void GetScript_ReturnsValidScriptWithNonce_UnderNormalConditions()
        {
            UpdateDefaultConfiguration();
            Mock.Arrange(() => _configuration.CaptureAttributes).Returns(false);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));

            var transaction = BuildTestTransaction(queueTime: TimeSpan.FromSeconds(1), applicationTime: TimeSpan.FromSeconds(2));

            var script = _browserMonitoringScriptMaker.GetScript(transaction, "TmV3IFJlbGlj");

            const string expectedScript = @"<script type=""text/javascript"" nonce=""TmV3IFJlbGlj"">window.NREUM||(NREUM={});NREUM.info = {""beacon"":"""",""errorBeacon"":"""",""licenseKey"":"""",""applicationID"":"""",""transactionName"":""HBsGAwcLSlMeAx8FEQ=="",""queueTime"":1000,""applicationTime"":2000,""agent"":"""",""atts"":""""}</script><script type=""text/javascript"" nonce=""TmV3IFJlbGlj"">the agent</script>";
            Assert.AreEqual(expectedScript, script);
        }

        [Test]
        public void GetScript_DefaultsToQueueTimeZero_IfQueueTimeIsNotSet()
        {
            UpdateDefaultConfiguration();
            Mock.Arrange(() => _configuration.CaptureAttributes).Returns(false);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));

            var transaction = BuildTestTransaction(applicationTime: TimeSpan.FromSeconds(2));

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            const string expectedScript = @"<script type=""text/javascript"">window.NREUM||(NREUM={});NREUM.info = {""beacon"":"""",""errorBeacon"":"""",""licenseKey"":"""",""applicationID"":"""",""transactionName"":""HBsGAwcLSlMeAx8FEQ=="",""queueTime"":0,""applicationTime"":2000,""agent"":"""",""atts"":""""}</script><script type=""text/javascript"">the agent</script>";
            Assert.AreEqual(expectedScript, script);
        }

        [Test]
        public void GetScript_IncludesObfuscatedAttributes_IfAttributesReturnedFromAttributeService()
        {
            IAttributeValueCollection mockAttributes;

            Mock.Arrange(() => _transactionAttributeMaker.SetUserAndAgentAttributes(Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<ITransactionAttributeMetadata>()))
                .DoInstead<IAttributeValueCollection, ITransactionAttributeMetadata>((attribVals, txMetadata) =>
                {
                    mockAttributes = attribVals;
                    _attribDefs.OriginalUrl.TrySetValue(attribVals, "http://www.google.com");
                    _attribDefs.GetCustomAttributeForTransaction("foo").TrySetValue(attribVals, "bar");
                });

            var transaction = BuildTestTransaction(queueTime: TimeSpan.FromSeconds(1), applicationTime: TimeSpan.FromSeconds(2));
            var tripId = transaction.TransactionMetadata.CrossApplicationReferrerTripId ?? transaction.Guid;

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            var expectedFormattedAttributes = $"{{\"a\":{{\"nr.tripId\":\"{tripId}\",\"original_url\":\"http://www.google.com\"}},\"u\":{{\"foo\":\"bar\"}}}}";

            var expectedObfuscatedFormattedAttributes = Strings.ObfuscateStringWithKey(expectedFormattedAttributes, "license key");
            var actualObfuscatedFormattedAttributes = Regex.Match(script, @"""atts"":""([^""]+)""").Groups[1].Value;
            Assert.AreEqual(expectedObfuscatedFormattedAttributes, actualObfuscatedFormattedAttributes);
        }

        [Test]
        public void GetScript_ObfuscatesTransactionMetricNameWithLicenseKey()
        {
            var transaction = BuildTestTransaction();

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            var expectedObfuscatedTransactionMetricName = Strings.ObfuscateStringWithKey("prefix/suffix", "license key");
            var actualObfuscatedTransactionMetricName = Regex.Match(script, @"""transactionName"":""([^""]+)""").Groups[1].Value;
            Assert.AreEqual(expectedObfuscatedTransactionMetricName, actualObfuscatedTransactionMetricName);
        }

        [Test]
        public void GetScript_ReturnsNull_IfBrowserMonitoringJavaScriptAgentIsNull()
        {
            var transaction = BuildTestTransaction();
            Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns(null as string);

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            Assert.Null(script);
        }

        [Test]
        public void GetScript_ReturnsNull_IfBrowserMonitoringJavaScriptAgentIsEmpty()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgent).Returns(string.Empty);
            var transaction = BuildTestTransaction();

            var script = _browserMonitoringScriptMaker.GetScript(transaction, null);

            Assert.Null(script);
        }

        [Test]
        public void GetScript_Throws_IfAgentLicenseKeyIsNull()
        {
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfAgentLicenseKeyIsEmpty()
        {
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns(string.Empty);
            var transaction = BuildTestTransaction();

            Assert.Throws<Exception>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfBrowserMonitoringBeaconAddressIsNull()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringBeaconAddress).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfBrowserMonitoringErrorBeaconAddressIsNull()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringErrorBeaconAddress).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfBrowserMonitoringKeyIsNull()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringKey).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfBrowserMonitoringApplicationIdIsNull()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringApplicationId).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        [Test]
        public void GetScript_Throws_IfBrowserMonitoringJavaScriptAgentFileIsNull()
        {
            Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentFile).Returns(null as string);
            var transaction = BuildTestTransaction();

            Assert.Throws<NullReferenceException>(() => _browserMonitoringScriptMaker.GetScript(transaction, null));
        }

        private IInternalTransaction BuildTestTransaction(TimeSpan? queueTime = null, TimeSpan? applicationTime = null)
        {
            var name = TransactionName.ForWebTransaction("foo", "bar");
            var time = applicationTime ?? TimeSpan.FromSeconds(1);

            ISimpleTimer timer = Mock.Create<ISimpleTimer>();
            Mock.Arrange(() => timer.Duration).Returns(time);

            var priority = 0.5f;
            var tx = new Transaction(_configuration, name, timer, DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);

            if (queueTime != null)
            {
                tx.TransactionMetadata.SetQueueTime(queueTime.Value);
            }

            return tx;
        }
    }
}

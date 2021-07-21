// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests
{
    [TestFixture]
    public class EventHarvestConfigSupportabilityMetricsTests
    {
        private CompositeTestAgent _compositeTestAgent;
        private ICollectorWire _collectorWire;
        private IAgentHealthReporter _agentHealthReporter;
        private ConnectionHandler _connectionHandler;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            var environment = _compositeTestAgent.Container.Resolve<IEnvironment>();
            var collectorWireFactory = Mock.Create<ICollectorWireFactory>();
            _collectorWire = Mock.Create<ICollectorWire>();
            var systemInfo = Mock.Create<ISystemInfo>();
            var processStatic = Mock.Create<IProcessStatic>();
            var configurationService = Mock.Create<IConfigurationService>();
            var agentEnvironment = new NewRelic.Agent.Core.Environment(systemInfo, processStatic, configurationService);

            Mock.Arrange(() => collectorWireFactory.GetCollectorWire(null, Arg.IsAny<IAgentHealthReporter>())).IgnoreArguments().Returns(_collectorWire);
            Mock.Arrange(() => _collectorWire.SendData("preconnect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>()))
                .Returns("{'return_value': { 'redirect_host': ''}}");

            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            _connectionHandler = new ConnectionHandler(new JsonSerializer(), collectorWireFactory, Mock.Create<IProcessStatic>(), Mock.Create<IDnsStatic>(),
                Mock.Create<ILabelsService>(), agentEnvironment, systemInfo, _agentHealthReporter, Mock.Create<IEnvironment>());
        }

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void MissingEventHarvestConfig_ShouldNotGenerateSupportabilityMetrics()
        {
            ConnectRespondsWithEventHarvestConfig(null);

            _connectionHandler.Connect();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public void MissingReportPeriod_ShouldNotGenerateSupportabilityMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = null
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            _connectionHandler.Connect();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public void ShouldGenerateReportPeriodSupportabilityMetric()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            _connectionHandler.Connect();

            ShouldGenerateSupportabilityMetric(MetricNames.SupportabilityEventHarvestReportPeriod, 5);
        }

        [Test]
        public void MissingHarvestLimit_ShouldNotGenerateHarvestLimitMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                HarvestLimits = null
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            _connectionHandler.Connect();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public void MissingReportPeriod_ShouldNotGenerateHarvestLimitMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                HarvestLimits = new Dictionary<string, int>
                {
                    { EventHarvestConfig.ErrorEventHarvestLimitKey, 1 },
                    { EventHarvestConfig.CustomEventHarvestLimitKey, 2 },
                    { EventHarvestConfig.TransactionEventHarvestLimitKey, 3 },
                    { EventHarvestConfig.SpanEventHarvestLimitKey, 4 }
                }
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            _connectionHandler.Connect();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [TestCase(EventHarvestConfig.ErrorEventHarvestLimitKey, MetricNames.SupportabilityEventHarvestErrorEventHarvestLimit)]
        [TestCase(EventHarvestConfig.CustomEventHarvestLimitKey, MetricNames.SupportabilityEventHarvestCustomEventHarvestLimit)]
        [TestCase(EventHarvestConfig.TransactionEventHarvestLimitKey, MetricNames.SupportabilityEventHarvestTransactionEventHarvestLimit)]
        [TestCase(EventHarvestConfig.SpanEventHarvestLimitKey, MetricNames.SupportabilityEventHarvestSpanEventHarvestLimit)]
        public void ShouldGenerateHarvestLimitSupportabilityMetric(string eventType, string expectedMetricName)
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { eventType, 10 } }
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            _connectionHandler.Connect();

            ShouldGenerateSupportabilityMetric(MetricNames.SupportabilityEventHarvestReportPeriod, 5000);
            ShouldGenerateSupportabilityMetric(expectedMetricName, 10);
        }

        private void ConnectRespondsWithEventHarvestConfig(EventHarvestConfig eventHarvestConfig)
        {
            if (eventHarvestConfig != null)
            {
                _compositeTestAgent.ServerConfiguration.EventHarvestConfig = eventHarvestConfig;
            }

            var serverConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(_compositeTestAgent.ServerConfiguration);
            Mock.Arrange(() => _collectorWire.SendData("connect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>()))
                .Returns($"{{'return_value': {serverConfigJson} }}");
        }

        private static bool HasEventHarvestMetricPrefix(string metricName)
        {
            return metricName.StartsWith(MetricNames.SupportabilityEventHarvestPs);
        }

        private void ShouldNotGenerateAnyEventHarvestSupportabilityMetrics()
        {
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCountMetric(Arg.Matches<string>(x => HasEventHarvestMetricPrefix(x)), Arg.AnyInt), Occurs.Never());
        }

        private void ShouldGenerateSupportabilityMetric(string expectedMetricName, int expectedMetricValue)
        {
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCountMetric(expectedMetricName, expectedMetricValue), Occurs.Once());
        }
    }
}

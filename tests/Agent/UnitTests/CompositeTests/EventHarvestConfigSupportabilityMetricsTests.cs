// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using Telerik.JustMock.Helpers;

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
            Mock.Arrange(() => _collectorWire.SendDataAsync("preconnect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
                .ReturnsAsync("{'return_value': { 'redirect_host': ''}}");
            
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
        public async Task MissingEventHarvestConfig_ShouldNotGenerateSupportabilityMetrics()
        {
            ConnectRespondsWithEventHarvestConfig(null);

            await _connectionHandler.ConnectAsync();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public async Task MissingReportPeriod_ShouldNotGenerateSupportabilityMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = null
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            await _connectionHandler.ConnectAsync();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public async Task ShouldGenerateReportPeriodSupportabilityMetric()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            await _connectionHandler.ConnectAsync();

            ShouldGenerateSupportabilityMetric(MetricNames.SupportabilityEventHarvestReportPeriod, 5);
        }

        [Test]
        public async Task MissingHarvestLimit_ShouldNotGenerateHarvestLimitMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                HarvestLimits = null
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            await _connectionHandler.ConnectAsync();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [Test]
        public async Task MissingReportPeriod_ShouldNotGenerateHarvestLimitMetrics()
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                HarvestLimits = new Dictionary<string, int>
                {
                    { EventHarvestConfig.ErrorEventHarvestLimitKey, 1 },
                    { EventHarvestConfig.CustomEventHarvestLimitKey, 2 },
                    { EventHarvestConfig.TransactionEventHarvestLimitKey, 3 }
                }
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            await _connectionHandler.ConnectAsync();

            ShouldNotGenerateAnyEventHarvestSupportabilityMetrics();
        }

        [TestCase("error_event_data", MetricNames.SupportabilityEventHarvestErrorEventHarvestLimit)]
        [TestCase("custom_event_data", MetricNames.SupportabilityEventHarvestCustomEventHarvestLimit)]
        [TestCase("analytic_event_data", MetricNames.SupportabilityEventHarvestTransactionEventHarvestLimit)]
        [TestCase("span_event_data", MetricNames.SupportabilitySpanEventsLimit)]
        public async Task ShouldGenerateHarvestLimitSupportabilityMetric(string eventType, string expectedMetricName)
        {
            var eventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { eventType, 10 } }
            };
            ConnectRespondsWithEventHarvestConfig(eventHarvestConfig);

            await _connectionHandler.ConnectAsync();

            ShouldGenerateSupportabilityMetric(MetricNames.SupportabilityEventHarvestReportPeriod, 5000);
            ShouldGenerateSupportabilityMetric(expectedMetricName, 10);
        }

        private void ConnectRespondsWithEventHarvestConfig(EventHarvestConfig eventHarvestConfig)
        {
            if (eventHarvestConfig != null)
            {
                _compositeTestAgent.ServerConfiguration.EventHarvestConfig = eventHarvestConfig;

                if (eventHarvestConfig.HarvestLimits != null && eventHarvestConfig.HarvestLimits.ContainsKey("span_event_data"))
                {
                    _compositeTestAgent.ServerConfiguration.SpanEventHarvestConfig = new SingleEventHarvestConfig()
                    {
                        ReportPeriodMs = 60000,
                        HarvestLimit = eventHarvestConfig.HarvestLimits["span_event_data"]
                    };

                }
            }

            var serverConfigJson = Newtonsoft.Json.JsonConvert.SerializeObject(_compositeTestAgent.ServerConfiguration);
            Mock.Arrange(() => _collectorWire.SendDataAsync("connect", Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
                .ReturnsAsync($"{{'return_value': {serverConfigJson} }}");
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

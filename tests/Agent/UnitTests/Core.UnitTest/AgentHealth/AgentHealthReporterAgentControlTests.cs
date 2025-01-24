// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentHealth
{
    public class AgentHealthReporterAgentControlTests
    {
        private AgentHealthReporter _agentHealthReporter;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private List<MetricWireModel> _publishedMetrics;

        private void Setup(bool agentControlEnabled, string deliveryLocation, int frequency)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AgentControlEnabled).Returns(agentControlEnabled);
            Mock.Arrange(() => configuration.HealthDeliveryLocation).Returns(deliveryLocation);
            Mock.Arrange(() => configuration.HealthFrequency).Returns(frequency);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _publishedMetrics = new List<MetricWireModel>();
            var scheduler = Mock.Create<IScheduler>();

            _agentHealthReporter = new AgentHealthReporter(metricBuilder, scheduler);
            _agentHealthReporter.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [TearDown]
        public void TearDown()
        {
            _agentHealthReporter?.Dispose();
            _configurationAutoResponder?.Dispose();
        }

        [Test]
        public void AgentControlMetricPresent_WhenAgentControlEnabled()
        {
            _agentHealthReporter.CollectMetrics();
            Assert.That(
                _publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/AgentControl/Health/enabled"),
                Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenDeliveryLocationDoesNotExist()
        {
            Setup(true, "file://foo", 12);

            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenDeliveryLocationIsNotAFileURI()
        {
            Setup(true, "http://foo", 12);

            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

    }
}

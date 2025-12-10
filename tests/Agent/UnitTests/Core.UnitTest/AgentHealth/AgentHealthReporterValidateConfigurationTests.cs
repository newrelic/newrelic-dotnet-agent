// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.Time;
using System.Linq;

namespace NewRelic.Agent.Core.AgentHealth
{
    [TestFixture]
    public class AgentHealthReporterValidateConfigurationTests
    {
        private AgentHealthReporter _agentHealthReporter;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private IConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.AgentControlEnabled).Returns(true);
            Mock.Arrange(() => _configuration.AgentEnabledAt).Returns("newrelic.config");
            Mock.Arrange(() => _configuration.HealthDeliveryLocation).Returns("file://foo");
            Mock.Arrange(() => _configuration.HealthFrequency).Returns(10);

            _configurationAutoResponder = new ConfigurationAutoResponder(_configuration);
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _agentHealthReporter = new AgentHealthReporter(metricBuilder, Mock.Create<IScheduler>(), Mock.Create<IFileWrapper>(), Mock.Create<IDirectoryWrapper>());
        }

        [TearDown]
        public void TearDown()
        {
            _configurationAutoResponder.Dispose();
            _agentHealthReporter.Dispose();
        }

        // JustMock Lite-compatible helpers for arranging out parameter calls (no OnInvoke / Helpers extensions).
        private static void ArrangeAppNamesSuccess(IConfiguration config)
        {
            IEnumerable<string> names = ["MyApp"];
            Mock.Arrange(() => config.TryGetApplicationNames(out names)).Returns(true);
        }

        private static void ArrangeAppNamesFailure(IConfiguration config)
        {
            IEnumerable<string> names = null;
            Mock.Arrange(() => config.TryGetApplicationNames(out names)).Returns(false);
        }

        [Test]
        public void Validate_Fails_WhenAgentDisabled()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(false);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(false);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("REPLACE_WITH_LICENSE_KEY");
            ArrangeAppNamesSuccess(_configuration); // should not be reached

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.False);
        }

        [Test]
        public void Validate_Fails_WhenLicenseKeyMissing_NonServerless()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(false);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("REPLACE_WITH_LICENSE_KEY");
            ArrangeAppNamesSuccess(_configuration); // not used after license check

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.False);
        }

        [Test]
        public void Validate_Fails_WhenApplicationNameMissing()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(false);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("1234567890123456789012345678901234567890");
            ArrangeAppNamesFailure(_configuration);

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.False);
        }

        [Test]
        public void Validate_Succeeds_WithValidConfiguration()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(false);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("1234567890123456789012345678901234567890");
            ArrangeAppNamesSuccess(_configuration);

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.True);
        }

        [Test]
        public void Validate_Succeeds_InServerless_WithPlaceholderLicenseKey()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("REPLACE_WITH_LICENSE_KEY");
            ArrangeAppNamesSuccess(_configuration);

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.True);
        }

        [Test]
        public void Validate_Fails_InServerless_WhenAppNameMissing()
        {
            Mock.Arrange(() => _configuration.AgentEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("REPLACE_WITH_LICENSE_KEY");
            ArrangeAppNamesFailure(_configuration);

            var result = _agentHealthReporter.ValidateAgentConfiguration();

            Assert.That(result, Is.False);
        }
    }
}

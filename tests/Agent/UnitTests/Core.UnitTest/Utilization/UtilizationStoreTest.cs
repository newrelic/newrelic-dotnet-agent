// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class UtilizationStoreTest
    {
        private ISystemInfo _systemInfo;
        private IDnsStatic _dnsStatic;
        private IAgentHealthReporter _agentHealthReporter;
        private IConfiguration _configuration;

        [SetUp]
        public void Setup()
        {
            _systemInfo = Mock.Create<ISystemInfo>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            Mock.Arrange(() => _systemInfo.GetTotalLogicalProcessors()).Returns(6);
            Mock.Arrange(() => _systemInfo.GetTotalPhysicalMemoryBytes()).Returns((ulong)16000 * 1024 * 1024);

            _dnsStatic = Mock.Create<IDnsStatic>();
            Mock.Arrange(() => _dnsStatic.GetHostName()).Returns("Host-Name");
            Mock.Arrange(() => _dnsStatic.GetFullHostName()).Returns("Host-Name.Domain");
            Mock.Arrange(() => _dnsStatic.GetIpAddresses()).Returns(new List<string> { "127.0.0.1", "0.0.0.0" });

        }

        [Test]
        public void when_calling_utilization_logical_cores_are_calculated_accurately()
        {
            _configuration = Mock.Create<IConfiguration>();
            var service = new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.That(settings.LogicalProcessors, Is.EqualTo(6), string.Format("Expected {0}, but was {1}", 8, settings.LogicalProcessors));
        }

        [Test]
        public void when_calling_utilization_total_ram_is_calculated_accurately()
        {
            _configuration = Mock.Create<IConfiguration>();
            var service = new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.That(settings.TotalRamMebibytes, Is.EqualTo(16000), string.Format("Expected {0}, but was {1}", 16000, settings.TotalRamMebibytes));
        }

        [Test]
        public void when_calling_utilization_hostname_is_set()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.UtilizationHostName).Returns("Host-Name");
            var service = new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.That(settings.Hostname, Is.EqualTo("Host-Name"), string.Format("Expected {0}, but was {1}", "Host-Name", settings.Hostname));
        }

        [Test]
        public void when_calling_utilization_fullhostname_is_set()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.UtilizationFullHostName).Returns("Host-Name.Domain");
            var service = new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.That(settings.FullHostName, Is.EqualTo("Host-Name.Domain"), string.Format("Expected {0}, but was {1}", "Host-Name.Domain", settings.FullHostName));
        }

        [Test]
        public void when_calling_utilization_ipaddresses_is_set()
        {
            _configuration = Mock.Create<IConfiguration>();

            var service = new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.That(settings.IpAddress, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(settings.IpAddress[0], Is.EqualTo("127.0.0.1"), string.Format("Expected {0}, but was {1}", "127.0.0.1", settings.IpAddress[0]));
                Assert.That(settings.IpAddress[1], Is.EqualTo("0.0.0.0"), string.Format("Expected {0}, but was {1}", "0.0.0.0", settings.IpAddress[1]));
            });
        }
    }
}

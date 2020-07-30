/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class UtilizationStoreTest
    {
        private ISystemInfo _systemInfo;
        private IDnsStatic _dnsStatic;
        private IAgentHealthReporter _agentHealthReporter;

        [SetUp]
        public void Setup()
        {
            _systemInfo = Mock.Create<ISystemInfo>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            Mock.Arrange(() => _systemInfo.GetTotalLogicalProcessors()).Returns(6);
            Mock.Arrange(() => _systemInfo.GetTotalPhysicalMemoryBytes()).Returns((ulong)16000 * 1024 * 1024);

            _dnsStatic = Mock.Create<IDnsStatic>();
            Mock.Arrange(() => _dnsStatic.GetHostName()).Returns("Host-Name");
        }

        [Test]
        public void when_calling_utilization_logical_cores_are_calculated_accurately()
        {
            var service = new UtilizationStore(_systemInfo, _dnsStatic, null, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.AreEqual(6, settings.LogicalProcessors, string.Format("Expected {0}, but was {1}", 8, settings.LogicalProcessors));
        }

        [Test]
        public void when_calling_utilization_total_ram_is_calculated_accurately()
        {
            var service = new UtilizationStore(_systemInfo, _dnsStatic, null, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.AreEqual(16000, settings.TotalRamMebibytes, string.Format("Expected {0}, but was {1}", 16000, settings.TotalRamMebibytes));
        }

        [Test]
        public void when_calling_utilization_hostname_is_set()
        {
            var service = new UtilizationStore(_systemInfo, _dnsStatic, null, _agentHealthReporter);
            var settings = service.GetUtilizationSettings();

            Assert.AreEqual("Host-Name", settings.Hostname, string.Format("Expected {0}, but was {1}", "Host-Name", settings.Hostname));
        }
    }
}

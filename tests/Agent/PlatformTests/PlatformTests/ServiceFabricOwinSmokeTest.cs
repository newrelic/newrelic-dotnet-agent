// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using PlatformTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace PlatformTests
{
    public class ServiceFabricOwinSmokeTest : IClassFixture<ServiceFabricFixture>
    {
        private ServiceFabricFixture _fixture;

        private AgentLogString _agentLog;

        public ServiceFabricOwinSmokeTest(ServiceFabricFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Exercise = () =>
            {
                _fixture.WarmUp();
                var agentLogString = _fixture.GetAgentLog();
                _agentLog = new AgentLogString(agentLogString);
            };

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var connectData = _agentLog.GetConnectData();
            Assert.True(connectData.UtilizationSettings.Vendors.ContainsKey("azure"));
            Assert.NotEmpty(connectData.UtilizationSettings.Vendors["azure"].Location);
            Assert.NotEmpty(connectData.UtilizationSettings.Vendors["azure"].Name);
            Assert.NotEmpty(connectData.UtilizationSettings.Vendors["azure"].VmId);
            Assert.NotEmpty(connectData.UtilizationSettings.Vendors["azure"].VmSize);
        }
    }
}

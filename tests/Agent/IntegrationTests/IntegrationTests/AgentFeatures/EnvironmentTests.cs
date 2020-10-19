// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    [NetFrameworkTest]
    public class EnvironmentTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public EnvironmentTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.Get();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var connectData = _fixture.AgentLog.GetConnectData();

            var plugins = connectData?.Environment.GetPluginList();

            Assert.NotEmpty(plugins);

            var hasSystem = plugins.Any(plugin => plugin.Contains("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
            var hasCore = plugins.Any(plugin => plugin.Contains("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
            var hasNrAgentCore = plugins.Any(plugin => plugin.Contains("NewRelic.Agent.Core, Version="));

            NrAssert.Multiple(
                () => Assert.True(hasSystem),
                () => Assert.True(hasCore),
                () => Assert.True(hasNrAgentCore)
            );
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class ConfigBuilderDeadlock : IClassFixture<ConsoleDynamicMethodFixtureFW>
    {
        protected readonly ConsoleDynamicMethodFixtureFW _fixture;

        public ConfigBuilderDeadlock(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.AddCommand($"ConfigBuilderDeadlock Run");
                    _fixture.SetTimeout(TimeSpan.FromMinutes(1));
                    _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_DELAY_AGENT_INIT_METHOD_LIST", "ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.ConfigBuilderDeadlock.DoTransaction");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.Equal(0, _fixture.RemoteApplication.ExitCode.Value);
        }
    }
}

/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class HighSecurityModeServerDisabled : IClassFixture<RemoteServiceFixtures.BasicWebApi>
    {
        private readonly RemoteServiceFixtures.BasicWebApi _fixture;

        public HighSecurityModeServerDisabled(RemoteServiceFixtures.BasicWebApi fixture, ITestOutputHelper output)
        {

            _fixture = fixture;
            _fixture.BypassAgentConnectionErrorLineRegexCheck = true;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetData();
                    _fixture.Get();
                    _fixture.Get404();
                    _fixture.GetId();
                    _fixture.Post();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var notConnectedLogLine = _fixture.AgentLog.TryGetLogLine(@".*? NewRelic INFO: Shutting down: Account Security Violation:*?");
            Assert.NotNull(notConnectedLogLine);
        }
    }
}

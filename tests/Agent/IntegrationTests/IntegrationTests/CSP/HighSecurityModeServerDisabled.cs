// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetFrameworkTest]
    public class HighSecurityModeServerDisabled : IClassFixture<RemoteServiceFixtures.OwinWebApiFixture>
    {

        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        public HighSecurityModeServerDisabled(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetLogLevel("debug");
                    configModifier.SetHighSecurityMode(true);
                    configModifier.SetEnableRequestParameters(true);
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
            // This test looks for the connect response body that was intended to be removed in P17, but was not.  If it does get removed this will fail.
            var notConnectedLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.ErrorLogLinePrefixRegex + "Received HTTP status code Gone with message {\"exception\":{\"message\":\"Account Security Violation: *?");
            Assert.NotNull(notConnectedLogLine);
        }
    }
}

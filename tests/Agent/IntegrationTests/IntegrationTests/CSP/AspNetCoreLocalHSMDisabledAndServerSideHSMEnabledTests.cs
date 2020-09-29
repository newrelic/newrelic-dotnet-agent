// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetCoreTest]
    public class AspNetCoreLocalHSMDisabledAndServerSideHSMEnabledTests : IClassFixture<RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture>
    {
        private const string QueryStringParameterValue = @"my thing";


        private readonly RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreLocalHSMDisabledAndServerSideHSMEnabledTests(RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("debug");
                    configModifier.SetEnableRequestParameters(true);
                    configModifier.SetHighSecurityMode(false);
                },
                exerciseApplication: () => _fixture.GetWithData(QueryStringParameterValue)
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

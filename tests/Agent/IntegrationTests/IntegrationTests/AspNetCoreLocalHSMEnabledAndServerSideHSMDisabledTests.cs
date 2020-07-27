﻿using System;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTests : IClassFixture<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private const String QueryStringParameterValue = @"my thing";

        [NotNull]
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTests([NotNull] RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.BypassAgentConnectionErrorLineRegexCheck = true;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "service" }, "ssl", "false");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetWithData(QueryStringParameterValue);
                    _fixture.ThrowException();
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

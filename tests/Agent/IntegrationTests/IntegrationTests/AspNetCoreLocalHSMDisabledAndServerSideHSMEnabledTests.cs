using System;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class AspNetCoreLocalHSMDisabledAndServerSideHSMEnabledTests : IClassFixture<RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture>
    {
        private const string QueryStringParameterValue = @"my thing";

        private readonly RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreLocalHSMDisabledAndServerSideHSMEnabledTests(RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
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
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "false");
                },
                exerciseApplication: () => _fixture.GetWithData(QueryStringParameterValue)
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

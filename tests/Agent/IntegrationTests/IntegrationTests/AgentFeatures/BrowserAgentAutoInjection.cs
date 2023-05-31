// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    [NetFrameworkTest]
    public class BrowserAgentAutoInjection : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        private string _htmlContent;

        public BrowserAgentAutoInjection(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetLogger(output);
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.AutoInstrumentBrowserMonitoring(true);
                    configModifier.BrowserMonitoringEnableAttributes(true);

                },
                exerciseApplication: () =>
                {
                    _htmlContent = _fixture.Get();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            NrAssert.Multiple(
                () => Assert.NotNull(_htmlContent)
            );

            var connectResponseData = _fixture.AgentLog.GetConnectResponseData();

            var jsAgentFromConnectResponse = connectResponseData.JsAgentLoader;

            var jsAgentFromHtmlContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);

            Assert.Equal(jsAgentFromConnectResponse, jsAgentFromHtmlContent);
        }
    }
}

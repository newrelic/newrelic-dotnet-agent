// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public abstract class BrowserAgentAutoInjectionBase : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        private string _htmlContent;

        protected BrowserAgentAutoInjectionBase(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output, string loaderType)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.AutoInstrumentBrowserMonitoring(true);
                    configModifier.BrowserMonitoringEnableAttributes(true);

                    if (!string.IsNullOrEmpty(loaderType))
                    {
                        configModifier.BrowserMonitoringLoader(loaderType);
                    }
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

    [NetFrameworkTest]
    public class BrowserAgentAutoInjectionDefault : BrowserAgentAutoInjectionBase
    {
        public BrowserAgentAutoInjectionDefault(BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, null /* use default loader */)
        {
        }
    }

    [NetFrameworkTest]
    public class BrowserAgentAutoInjectionRum : BrowserAgentAutoInjectionBase
    {
        public BrowserAgentAutoInjectionRum(BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "rum")
        {
        }
    }

    [NetFrameworkTest]
    public class BrowserAgentAutoInjectionFull : BrowserAgentAutoInjectionBase
    {
        public BrowserAgentAutoInjectionFull(BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "full")
        {
        }
    }

    [NetFrameworkTest]
    public class BrowserAgentAutoInjectionSpa : BrowserAgentAutoInjectionBase
    {
        public BrowserAgentAutoInjectionSpa(BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "spa")
        {
        }
    }
}

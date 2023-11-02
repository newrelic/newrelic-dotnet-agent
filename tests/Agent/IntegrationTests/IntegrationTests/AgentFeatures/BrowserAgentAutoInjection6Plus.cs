// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public abstract class BrowserAgentAutoInjection6PlusBase : NewRelicIntegrationTest<BasicAspNetCoreRazorApplicationFixture>
    {
        private readonly BasicAspNetCoreRazorApplicationFixture _fixture;
        private string _htmlContent;
        private string _staticContent;

        protected BrowserAgentAutoInjection6PlusBase(BasicAspNetCoreRazorApplicationFixture fixture,
            ITestOutputHelper output, bool enableResponseCompression, string loaderType = "rum")
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

                    configModifier.BrowserMonitoringLoader(loaderType);
                },
                exerciseApplication: () =>
                {
                    _htmlContent = _fixture.Get("Index"); // get a razor page
                    _staticContent = _fixture.Get("static.html"); // get static content
                }
            );

            _fixture.SetResponseCompression(enableResponseCompression);

            _fixture.Initialize();

        }

        [Fact]
        public void Test()
        {
            NrAssert.Multiple(
                () => Assert.NotNull(_htmlContent),
                () => Assert.NotNull(_staticContent)
            );

            var connectResponseData = _fixture.AgentLog.GetConnectResponseData();

            var jsAgentFromConnectResponse = connectResponseData.JsAgentLoader;

            var jsAgentFromHtmlContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);
            var jsAgentFromStaticContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_staticContent);

            Assert.Equal(jsAgentFromConnectResponse, jsAgentFromHtmlContent);
            Assert.Equal(jsAgentFromConnectResponse, jsAgentFromStaticContent);
        }
    }

    [NetCoreTest]
    public class BrowserAgentAutoInjection6PlusRumUnCompressed : BrowserAgentAutoInjection6PlusBase
    {
        public BrowserAgentAutoInjection6PlusRumUnCompressed(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class BrowserAgentAutoInjection6PlusRumCompressed : BrowserAgentAutoInjection6PlusBase
    {
        public BrowserAgentAutoInjection6PlusRumCompressed(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }


    [NetCoreTest]
    public class BrowserAgentAutoInjection6PlusSpa : BrowserAgentAutoInjection6PlusBase
    {
        public BrowserAgentAutoInjection6PlusSpa(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output, true, "spa")
        {
        }
    }

    [NetCoreTest]
    public class BrowserAgentAutoInjection6PlusFull : BrowserAgentAutoInjection6PlusBase
    {
        public BrowserAgentAutoInjection6PlusFull(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output, true, "full")
        {
        }
    }
}

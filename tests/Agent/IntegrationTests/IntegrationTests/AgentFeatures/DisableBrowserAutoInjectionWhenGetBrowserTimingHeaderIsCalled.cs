// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class DisableBrowserAutoInjectionWhenGetBrowserTimingHeaderIsCalled : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        private string _browserTimingHeader;
        private string _htmlContentAfterCallToGetBrowserTiming;

        public DisableBrowserAutoInjectionWhenGetBrowserTimingHeaderIsCalled(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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

                },
                exerciseApplication: () =>
                {
                    _browserTimingHeader = _fixture.GetBrowserTimingHeader();
                    _htmlContentAfterCallToGetBrowserTiming = _fixture.GetHtmlWithCallToGetBrowserTimingHeader();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            NrAssert.Multiple(
                () => Assert.NotNull(_browserTimingHeader),
                () => Assert.NotNull(_htmlContentAfterCallToGetBrowserTiming),
                () => Assert.Contains("NREUM", _browserTimingHeader),
                ShouldNotAutoInstrumentAfterCallToGetBrowserTimingHeader
            );

            var browserMonitoringConfig = JavaScriptAgent.GetJavaScriptAgentConfigFromSource(_browserTimingHeader);

            NrAssert.Multiple(
                () => Assert.Contains("beacon", browserMonitoringConfig.Keys),
                () => Assert.Contains("errorBeacon", browserMonitoringConfig.Keys),
                () => Assert.Contains("licenseKey", browserMonitoringConfig.Keys),
                () => Assert.Contains("applicationID", browserMonitoringConfig.Keys),
                () => Assert.Contains("transactionName", browserMonitoringConfig.Keys),
                () => Assert.Contains("queueTime", browserMonitoringConfig.Keys),
                () => Assert.Contains("applicationTime", browserMonitoringConfig.Keys),
                () => Assert.Contains("agent", browserMonitoringConfig.Keys),
                () => Assert.Contains("atts", browserMonitoringConfig.Keys)
            );

            var attrsDict = HeaderEncoder.DecodeAndDeserialize<Dictionary<string, IDictionary<string, object>>>(browserMonitoringConfig["atts"], _fixture.TestConfiguration.LicenseKey, 13);
            Assert.Contains("a", attrsDict.Keys);
            IDictionary<string, object> agentAttrsDict = attrsDict["a"];
            Assert.Contains("nr.tripId", agentAttrsDict.Keys);

            NrAssert.Multiple(
                () => Assert.NotNull(browserMonitoringConfig["beacon"]),
                () => Assert.NotNull(browserMonitoringConfig["errorBeacon"]),
                () => Assert.NotNull(browserMonitoringConfig["licenseKey"]),
                () => Assert.NotNull(browserMonitoringConfig["applicationID"]),
                () => Assert.NotNull(browserMonitoringConfig["transactionName"]),
                () => Assert.NotNull(browserMonitoringConfig["queueTime"]),
                () => Assert.NotNull(browserMonitoringConfig["applicationTime"]),
                () => Assert.NotNull(browserMonitoringConfig["agent"])
            );
        }

        private void ShouldNotAutoInstrumentAfterCallToGetBrowserTimingHeader()
        {
            Assert.DoesNotContain("NREUM", _htmlContentAfterCallToGetBrowserTiming);
        }
    }
}

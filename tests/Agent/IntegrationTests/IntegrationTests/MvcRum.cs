using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class MvcRum : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        private string _responseBodyForHtmlContent;
        private string _responseBodyForNonHtmlContent;

        public MvcRum(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.DelayKill = true;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _responseBodyForHtmlContent = _fixture.Get();
                    _responseBodyForNonHtmlContent = _fixture.GetNotHtmlContentType();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            NrAssert.Multiple(
                () => Assert.Contains("NREUM", _responseBodyForHtmlContent),
                () => Assert.DoesNotContain("NREUM", _responseBodyForNonHtmlContent)
                );

            var browserMonitoringConfig = JavaScriptAgent.GetJavaScriptAgentConfigFromSource(_responseBodyForHtmlContent);

            NrAssert.Multiple(
                () => Assert.Contains("beacon", browserMonitoringConfig.Keys),
                () => Assert.Contains("errorBeacon", browserMonitoringConfig.Keys),
                () => Assert.Contains("licenseKey", browserMonitoringConfig.Keys),
                () => Assert.Contains("applicationID", browserMonitoringConfig.Keys),
                () => Assert.Contains("transactionName", browserMonitoringConfig.Keys),
                () => Assert.Contains("queueTime", browserMonitoringConfig.Keys),
                () => Assert.Contains("applicationTime", browserMonitoringConfig.Keys),
                () => Assert.Contains("agent", browserMonitoringConfig.Keys)

                // "atts" will be missing if there are no javascript attributes
                //() => Assert.Contains("atts", browserMonitoringConfig.Keys)

                // It's not guaranteed that "sslForHttp" will be present (depends on configuration)
                //() => Assert.Contains("sslForHttp", browserMonitoringConfig.Keys)
                );

            NrAssert.Multiple(
                () => Assert.NotNull(browserMonitoringConfig["beacon"]),
                () => Assert.NotNull(browserMonitoringConfig["errorBeacon"]),
                () => Assert.NotNull(browserMonitoringConfig["licenseKey"]),
                () => Assert.NotNull(browserMonitoringConfig["applicationID"]),
                () => Assert.NotNull(browserMonitoringConfig["transactionName"]),
                () => Assert.NotNull(browserMonitoringConfig["queueTime"]),
                () => Assert.NotNull(browserMonitoringConfig["applicationTime"]),
                () => Assert.NotNull(browserMonitoringConfig["agent"])

                //() => Assert.NotNull(browserMonitoringConfig["agent"]),
                //() => Assert.NotNull(browserMonitoringConfig["agent"])
                );
        }
    }
}

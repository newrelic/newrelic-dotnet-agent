using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class AgentApiTests : IClassFixture<RemoteServiceFixtures.AgentApiExecutor>
    {
        private readonly RemoteServiceFixtures.AgentApiExecutor _fixture;

        public AgentApiTests(RemoteServiceFixtures.AgentApiExecutor fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, new[] { new KeyValuePair<string, string>("autoStart", "false") });
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new[]
            {
                new Assertions.ExpectedMetric{ metricName = "Custom/MyMetric", callCount = 1}
            };

            var expectedErrorEventIntrinsicAttributes = new Dictionary<string, string>
            {
                { "error.class", "System.Exception" },
                { "error.message", "Rawr!" },
                { "type", "TransactionError" }
            };

            var expectedErrorEventCustomAttributes = new Dictionary<string, string>
            {
                {"hey", "dude"},
                {"faz", "baz"}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var errorEventPayload = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();

            NrAssert.Multiple
                (
                    () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                    () => Assert.NotNull(errorTrace),
                    () => Assert.Equal("100", errorEventPayload.Additions.ReservoirSize.ToString()),
                    () => Assert.Equal("2", errorEventPayload.Additions.EventsSeen.ToString()),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[0]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventIntrinsicAttributes, EventAttributeType.Intrinsic, errorEventPayload.Events[1]),
                    () => Assertions.ErrorEventHasAttributes(expectedErrorEventCustomAttributes, EventAttributeType.User, errorEventPayload.Events[1])
                );
        }
    }
}

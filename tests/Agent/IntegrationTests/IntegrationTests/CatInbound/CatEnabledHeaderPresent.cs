using System.Linq;
using System.Net.Http.Headers;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    public class CatEnabledHeaderPresent : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        [NotNull]
        private HttpResponseHeaders _responseHeaders;

        public CatEnabledHeaderPresent([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration" }, "crossApplicationTracingEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "crossApplicationTracer" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetIgnored();
                    _responseHeaders = _fixture.GetWithCatHeader();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var catResponseHeader = _responseHeaders.GetValues(@"X-NewRelic-App-Data")?.FirstOrDefault();
            Assert.NotNull(catResponseHeader);

            var catResponseData = HeaderEncoder.DecodeAndDeserialize<CrossApplicationResponseData>(catResponseHeader, HeaderEncoder.IntegrationTestEncodingKey);

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/Index");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Index");
            var metrics = _fixture.AgentLog.GetMetrics();

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assert.Equal($"{_fixture.AgentLog.GetAccountId()}#{_fixture.AgentLog.GetApplicationId()}", catResponseData.CrossProcessId),
                () => Assert.Equal("WebTransaction/MVC/DefaultController/Index", catResponseData.TransactionName),
                () => Assert.True(catResponseData.QueueTimeInSeconds >= 0),
                () => Assert.True(catResponseData.ResponseTimeInSeconds >= 0),
                () => Assert.Equal(-1, catResponseData.ContentLength),
                () => Assert.NotNull(catResponseData.TransactionGuid),
                () => Assert.Equal(false, catResponseData.Unused),

                // Trace attributes
                () => Assertions.TransactionTraceHasAttributes(Expectations.ExpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(Expectations.UnexpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),

                // Event attributes
                () => Assertions.TransactionEventHasAttributes(Expectations.ExpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEvent),

                () => Assertions.MetricsExist(Expectations.ExpectedMetricsCatEnabled, metrics)
            );
        }
    }
}

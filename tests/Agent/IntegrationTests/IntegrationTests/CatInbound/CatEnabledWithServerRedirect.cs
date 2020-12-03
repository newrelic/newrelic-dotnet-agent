// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using System.Net.Http.Headers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    [NetFrameworkTest]
    public class CatEnabledWithServerRedirect : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        private HttpResponseHeaders _responseHeaders;

        public CatEnabledWithServerRedirect(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _responseHeaders = _fixture.GetWithCatHeaderWithRedirect();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        [Trait("feature", "CAT-DistributedTracing")]
        public void Test()
        {
            var catResponseHeader = _responseHeaders.GetValues(@"X-NewRelic-App-Data")?.FirstOrDefault();
            Assert.NotNull(catResponseHeader);

            var catResponseData = HeaderEncoder.DecodeAndDeserialize<CrossApplicationResponseData>(catResponseHeader, HeaderEncoder.IntegrationTestEncodingKey);

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/Index");
            var transactionEventIndex = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Index");
            var transactionEventRedirect = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/DoRedirect");
            var metrics = _fixture.AgentLog.GetMetrics();

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEventRedirect),
                () => Assert.NotNull(transactionEventIndex)
            );

            NrAssert.Multiple
            (
                () => Assert.Equal(_fixture.AgentLog.GetCrossProcessId(), catResponseData.CrossProcessId),
                () => Assert.Equal("WebTransaction/MVC/DefaultController/Index", catResponseData.TransactionName),
                () => Assert.True(catResponseData.QueueTimeInSeconds >= 0),
                () => Assert.True(catResponseData.ResponseTimeInSeconds >= 0),
                () => Assert.Equal(-1, catResponseData.ContentLength),
                () => Assert.NotNull(catResponseData.TransactionGuid),
                () => Assert.False(catResponseData.Unused),

                // Trace attributes
                () => Assertions.TransactionTraceHasAttributes(Expectations.ExpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(Expectations.UnexpectedTransactionTraceIntrinsicAttributesCatEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),

                // transactionEventIndex attributes
                () => Assertions.TransactionEventHasAttributes(Expectations.ExpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventIndex),
                () => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventIndex),

                // transactionEventRedirect attributes
                () => Assertions.TransactionEventHasAttributes(Expectations.ExpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventRedirect),
                () => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesCatEnabled, TransactionEventAttributeType.Intrinsic, transactionEventRedirect),

                () => Assertions.MetricsExist(Expectations.ExpectedMetricsCatEnabled, metrics)
            );
        }
    }
}

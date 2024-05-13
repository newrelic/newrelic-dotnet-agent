// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net.Http.Headers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    [NetFrameworkTest]
    public class CatEnabledHeaderPresentDistributedTraceSettingTrue : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;
        private HttpResponseHeaders _responseHeaders;

        public CatEnabledHeaderPresentDistributedTraceSettingTrue(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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

                    configModifier.ForceTransactionTraces();
                    configModifier.EnableCat();
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
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
        [Trait("feature", "CAT-DistributedTracing")]
        public void Test()
        {
            Assert.False(_responseHeaders.Contains(@"X-NewRelic-App-Data"));

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
                // Trace attributes
                () => Assertions.TransactionTraceDoesNotHaveAttributes(Expectations.UnexpectedTransactionTraceIntrinsicAttributesDistributedTracingEnabled, TransactionTraceAttributeType.Intrinsic, transactionSample),

                // Event attributes
                () => Assertions.TransactionEventDoesNotHaveAttributes(Expectations.UnexpectedTransactionEventIntrinsicAttributesDistributedTracingEnabled, TransactionEventAttributeType.Intrinsic, transactionEvent),

                () => Assertions.MetricsDoNotExist(Expectations.UnexpectedMetricsDistributedTracingEnabled, metrics)
            );
        }
    }
}

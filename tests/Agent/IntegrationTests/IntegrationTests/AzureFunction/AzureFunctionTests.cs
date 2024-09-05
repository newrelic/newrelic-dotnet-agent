// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AzureFunction
{
    public abstract class AzureFunctionTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AzureFunctionApplicationFixture
    {
        private readonly TFixture _fixture;

        protected AzureFunctionTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .ConfigureFasterTransactionTracesHarvestCycle(20)
                        .ConfigureFasterMetricsHarvestCycle(15)
                        .ConfigureFasterSpanEventsHarvestCycle(15)
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get("api/function1");
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionEventAttributes = new List<string>
            {
                "faas.coldStart",
                "faas.invocation_id",
                "faas.name",
                "faas.trigger",
                "cloud.resource_id"
            };

            var expectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/Function1", callCount = 1},
                new() {metricName = "DotNet/Function1", metricScope = "WebTransaction/AzureFunction/Function1", callCount = 1},
                new() {metricName = "WebTransaction/AzureFunction/Function1", callCount = 1},
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assertions.MetricsExist(expectedMetrics, metrics);

            // TODO: when interaction with aspnetcore instrumentation is resolved, we should expect to see the Azure function transaction get sampled
            //var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/AzureFunction/Function1");
            //Assert.NotNull(transactionSample);

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/AzureFunction/Function1");
            Assert.NotNull(transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedTransactionEventAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, transactionEvent);

            Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("cloud.resource_id", out var cloudResourceIdValue));
            Assert.Equal("/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/Function1", cloudResourceIdValue);
            Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("faas.name", out var faasNameValue));
            Assert.Equal("Function1", faasNameValue);
            Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
            Assert.Equal("http", faasTriggerValue);
        }
    }

    [NetCoreTest]
    public class AzureFunctionTestsCoreLatest : AzureFunctionTestsBase<AzureFunctionApplicationFixture_Function1_CoreLatest>
    {
        public AzureFunctionTestsCoreLatest(AzureFunctionApplicationFixture_Function1_CoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

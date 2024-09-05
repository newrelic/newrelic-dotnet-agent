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
    public abstract class AzureFunctionHttpTriggerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AzureFunctionApplicationFixture
    {
        private readonly TFixture _fixture;

        protected AzureFunctionHttpTriggerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.Get("api/httpTriggerFunction");
                    _fixture.Get("api/httpTriggerFunction"); // make a second call to verify coldStart is not sent
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var firstTransactionExpectedTransactionEventAttributes = new List<string>
            {
                "faas.coldStart",
                "faas.invocation_id",
                "faas.name",
                "faas.trigger",
                "cloud.resource_id"
            };

            var secondTransactionUnexpectedTransactionEventAttributes = new List<string>
            {
                "faas.coldStart"
            };

            var transactionName = "WebTransaction/AzureFunction/HttpTriggerFunction";
            var expectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/HttpTriggerFunction", callCount = 2},
                new() {metricName = "DotNet/HttpTriggerFunction", metricScope = transactionName, callCount = 2},
                new() {metricName = transactionName, callCount = 2},
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            if (_fixture.AzureFunctionModeEnabled) // if instrumentation is disabled, no metrics should exist
                Assertions.MetricsExist(expectedMetrics, metrics);
            else
                Assertions.MetricsDoNotExist(expectedMetrics, metrics);

            // TODO: when interaction with aspnetcore instrumentation is resolved, we should expect to see the Azure function transaction get sampled
            //var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/AzureFunction/HttpTriggerFunction");
            //Assert.NotNull(transactionSample);

            var transactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == transactionName)
                .OrderBy(x => x.IntrinsicAttributes?["timestamp"])
                .ToList();

            var firstTransaction = transactionEvents.FirstOrDefault();
            var secondTransaction = transactionEvents.Skip(1).FirstOrDefault();

            if (_fixture.AzureFunctionModeEnabled)
            {
                Assert.NotNull(firstTransaction);
                Assert.NotNull(secondTransaction);

                Assertions.TransactionEventHasAttributes(firstTransactionExpectedTransactionEventAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, firstTransaction);
                Assertions.TransactionEventDoesNotHaveAttributes(secondTransactionUnexpectedTransactionEventAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, secondTransaction);

                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("cloud.resource_id", out var cloudResourceIdValue));
                Assert.Equal("/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/HttpTriggerFunction", cloudResourceIdValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.name", out var faasNameValue));
                Assert.Equal("HttpTriggerFunction", faasNameValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
                Assert.Equal("http", faasTriggerValue);
            }
            else
            {
                Assert.Empty(transactionEvents); // there should be no transactions when azure function mode is disabled
            }

            if (!_fixture.AzureFunctionModeEnabled) // look for a specific log line that indicates azure function mode is disabled
            {
                var disabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.AzureFunctionModeDisabledLogLineRegex);
                Assert.NotNull(disabledLogLine);
            }
        }
    }

    [NetCoreTest]
    public class AzureFunctionHttpTriggerTestsCoreOldest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureHttpTriggerCoreOldest>
    {
        public AzureFunctionHttpTriggerTestsCoreOldest(AzureFunctionApplicationFixtureHttpTriggerCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class AzureFunctionHttpTriggerTestsCoreLatest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureHttpTriggerCoreLatest>
    {
        public AzureFunctionHttpTriggerTestsCoreLatest(AzureFunctionApplicationFixtureHttpTriggerCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

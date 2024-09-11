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
    public abstract class AzureFunctionQueueTriggerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AzureFunctionApplicationFixture
    {
        private readonly TFixture _fixture;

        protected AzureFunctionQueueTriggerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.PostToAzureFuncTool("QueueTriggerFunction", "test message");

                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var transactionExpectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "faas.coldStart",
                "faas.invocation_id",
                "faas.name",
                "faas.trigger",
                "cloud.resource_id"
            };

            var transactionTraceExpectedAttributes = new Dictionary<string, object>()
            {
                { "faas.coldStart", true},
                //new("faas.invocation_id", "test_invocation_id"), This one is a random guid, not something we can specifically look for
                { "faas.name", "QueueTriggerFunction" },
                { "faas.trigger", "datasource" },
                { "cloud.resource_id", "/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/QueueTriggerFunction" }
            };

            var transactionName = "OtherTransaction/AzureFunction/QueueTriggerFunction";
            var expectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/QueueTriggerFunction", callCount = 1},
                new() {metricName = "DotNet/QueueTriggerFunction", metricScope = transactionName, callCount = 1},
                new() {metricName = transactionName, callCount = 1},
            };


            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(transactionName);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transaction = _fixture.AgentLog.TryGetTransactionEvent(transactionName);

            if (_fixture.AzureFunctionModeEnabled)
            {
                Assertions.MetricsExist(expectedMetrics, metrics);

                Assert.NotNull(transactionSample);
                Assert.NotNull(transaction);

                Assertions.TransactionTraceHasAttributes(transactionTraceExpectedAttributes, Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Intrinsic, transactionSample);

                Assertions.TransactionEventHasAttributes(transactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, transaction);

                Assert.True(transaction.IntrinsicAttributes.TryGetValue("cloud.resource_id", out var cloudResourceIdValue));
                Assert.Equal("/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/QueueTriggerFunction", cloudResourceIdValue);
                Assert.True(transaction.IntrinsicAttributes.TryGetValue("faas.name", out var faasNameValue));
                Assert.Equal("QueueTriggerFunction", faasNameValue);
                Assert.True(transaction.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
                Assert.Equal("datasource", faasTriggerValue);
            }
            else
            {
                Assertions.MetricsDoNotExist(expectedMetrics, metrics);
                Assert.Null(transactionSample);

                Assert.Null(transaction);
            }

            if (!_fixture.AzureFunctionModeEnabled) // look for a specific log line that indicates azure function mode is disabled
            {
                var disabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.AzureFunctionModeDisabledLogLineRegex);
                Assert.NotNull(disabledLogLine);
            }
        }
    }

    [NetCoreTest]
    public class AzureFunctionQueueTriggerTestsCoreOldest : AzureFunctionQueueTriggerTestsBase<AzureFunctionApplicationFixtureQueueTriggerCoreOldest>
    {
        public AzureFunctionQueueTriggerTestsCoreOldest(AzureFunctionApplicationFixtureQueueTriggerCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class AzureFunctionQueueTriggerTestsCoreLatest : AzureFunctionQueueTriggerTestsBase<AzureFunctionApplicationFixtureQueueTriggerCoreLatest>
    {
        public AzureFunctionQueueTriggerTestsCoreLatest(AzureFunctionApplicationFixtureQueueTriggerCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

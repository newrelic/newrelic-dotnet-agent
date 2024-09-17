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
                    _fixture.Get("api/httpTriggerFunctionUsingAspNetCorePipeline");
                    _fixture.Get("api/httpTriggerFunctionUsingAspNetCorePipeline"); // make a second call to verify coldStart is not sent
                    _fixture.Get("api/httpTriggerFunctionUsingSimpleInvocation"); // invoke an http trigger function that does not use the aspnet core pipeline
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var firstTransactionExpectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "faas.coldStart",
                "faas.invocation_id",
                "faas.name",
                "faas.trigger",
                "cloud.resource_id"
            };

            var secondTransactionUnexpectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "faas.coldStart"
            };

            var simpleTransactionExpectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "faas.invocation_id",
                "faas.name",
                "faas.trigger",
                "cloud.resource_id"
            };

            var expectedAgentAttributes = new Dictionary<string, string>
            {
                { "request.uri", "/Unknown"}
            };

            var transactionTraceExpectedAttributes = new Dictionary<string, object>()
            {
                { "faas.coldStart", true},
                //new("faas.invocation_id", "test_invocation_id"), This one is a random guid, not something we can specifically look for
                { "faas.name", "HttpTriggerFunctionUsingAspNetCorePipeline" },
                { "faas.trigger", "http" },
                { "cloud.resource_id", "/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/HttpTriggerFunctionUsingAspNetCorePipeline" }
            };

            var pipelineTransactionName = "WebTransaction/AzureFunction/HttpTriggerFunctionUsingAspNetCorePipeline";
            var pipelineExpectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/HttpTriggerFunctionUsingAspNetCorePipeline", callCount = 2},
                new() {metricName = "DotNet/HttpTriggerFunctionUsingAspNetCorePipeline", metricScope = pipelineTransactionName, callCount = 2},
                new() {metricName = pipelineTransactionName, callCount = 2},
            };

            var simpleTransactionName = "WebTransaction/AzureFunction/HttpTriggerFunctionUsingSimpleInvocation";
            var simpleExpectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/HttpTriggerFunctionUsingSimpleInvocation", callCount = 1},
                new() {metricName = "DotNet/HttpTriggerFunctionUsingSimpleInvocation", metricScope = simpleTransactionName, callCount = 1},
                new() {metricName = simpleTransactionName, callCount = 1},
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(pipelineTransactionName);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var pipelineTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == pipelineTransactionName)
                .OrderBy(x => x.IntrinsicAttributes?["timestamp"])
                .ToList();

            var firstTransaction = pipelineTransactionEvents.FirstOrDefault();
            var secondTransaction = pipelineTransactionEvents.Skip(1).FirstOrDefault();

            var simpleTransaction = _fixture.AgentLog.TryGetTransactionEvent(simpleTransactionName);

            if (_fixture.AzureFunctionModeEnabled)
            {
                Assertions.MetricsExist(pipelineExpectedMetrics, metrics);
                Assertions.MetricsExist(simpleExpectedMetrics, metrics);

                Assert.NotNull(transactionSample);
                Assert.NotNull(firstTransaction);
                Assert.NotNull(secondTransaction);
                Assert.NotNull(simpleTransaction);

                Assertions.TransactionTraceHasAttributes(firstTransactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Intrinsic, transactionSample);
                Assertions.TransactionTraceHasAttributes(expectedAgentAttributes, Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Agent, transactionSample);

                Assertions.TransactionEventHasAttributes(firstTransactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, firstTransaction);
                Assertions.TransactionEventHasAttributes(expectedAgentAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Agent, firstTransaction);

                Assertions.TransactionEventDoesNotHaveAttributes(secondTransactionUnexpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, secondTransaction);

                Assertions.TransactionEventHasAttributes(simpleTransactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, simpleTransaction);

                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("cloud.resource_id", out var cloudResourceIdValue));
                Assert.Equal("/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/HttpTriggerFunctionUsingAspNetCorePipeline", cloudResourceIdValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.name", out var faasNameValue));
                Assert.Equal("HttpTriggerFunctionUsingAspNetCorePipeline", faasNameValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
                Assert.Equal("http", faasTriggerValue);
            }
            else
            {
                Assertions.MetricsDoNotExist(pipelineExpectedMetrics, metrics);
                Assertions.MetricsDoNotExist(simpleExpectedMetrics, metrics);
                Assert.Null(transactionSample);

                Assert.Empty(pipelineTransactionEvents); // there should be no transactions when azure function mode is disabled
                Assert.Null(simpleTransaction);
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

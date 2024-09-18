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
    public enum AzureFunctionHttpTriggerTestMode
    {
        AspNetCorePipeline,
        SimpleInvocation
    }
    public abstract class AzureFunctionHttpTriggerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AzureFunctionApplicationFixture
    {
        private readonly TFixture _fixture;
        private readonly AzureFunctionHttpTriggerTestMode _testMode;

        protected AzureFunctionHttpTriggerTestsBase(TFixture fixture, ITestOutputHelper output, AzureFunctionHttpTriggerTestMode testMode) : base(fixture)
        {
            _fixture = fixture;
            _testMode = testMode;
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
                    if (_testMode == AzureFunctionHttpTriggerTestMode.AspNetCorePipeline)
                    {
                        _fixture.Get("api/httpTriggerFunctionUsingAspNetCorePipeline?someParameter=foo");
                        _fixture.Get("api/httpTriggerFunctionUsingAspNetCorePipeline?someParameter=bar"); // make a second call to verify coldStart is not sent
                        _fixture.Get("api/httpTriggerFunctionUsingSimpleInvocation"); // invoke an http trigger function that does not use the aspnet core pipeline, even in pipeline test mode
                    }
                    else
                    {
                        _fixture.Get("api/httpTriggerFunctionUsingSimpleInvocation");
                        _fixture.Get("api/httpTriggerFunctionUsingSimpleInvocation"); // make a second call to verify coldStart is not sent
                    }
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [SkippableFact]
        public void Test_SimpleInvocationMode()
        {
            Skip.IfNot(_testMode == AzureFunctionHttpTriggerTestMode.SimpleInvocation);

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

            var expectedAgentAttributes = new Dictionary<string, object>
            {
                { "request.uri", "/api/httpTriggerFunctionUsingSimpleInvocation"},
                { "request.method", "GET" },
                { "http.statusCode", 200 }
            };

            var simpleTransactionName = "WebTransaction/AzureFunction/HttpTriggerFunctionUsingSimpleInvocation";
            var simpleExpectedMetrics = new List<Assertions.ExpectedMetric>()
            {
                new() {metricName = "DotNet/HttpTriggerFunctionUsingSimpleInvocation", callCount = 2},
                new() {metricName = "DotNet/HttpTriggerFunctionUsingSimpleInvocation", metricScope = simpleTransactionName, callCount = 2},
                new() {metricName = simpleTransactionName, callCount = 2},
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(simpleTransactionName);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var simpleTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == simpleTransactionName)
                .OrderBy(x => x.IntrinsicAttributes?["timestamp"])
                .ToList();

            var firstTransaction = simpleTransactionEvents.FirstOrDefault();
            var secondTransaction = simpleTransactionEvents.Skip(1).FirstOrDefault();

            if (_fixture.AzureFunctionModeEnabled)
            {
                Assertions.MetricsExist(simpleExpectedMetrics, metrics);

                Assert.NotNull(transactionSample);
                Assert.NotNull(firstTransaction);
                Assert.NotNull(secondTransaction);

                Assertions.TransactionTraceHasAttributes(firstTransactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Intrinsic, transactionSample);
                Assertions.TransactionTraceHasAttributes(expectedAgentAttributes, Tests.TestSerializationHelpers.Models.TransactionTraceAttributeType.Agent, transactionSample);

                Assertions.TransactionEventHasAttributes(firstTransactionExpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, firstTransaction);
                Assertions.TransactionEventHasAttributes(expectedAgentAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Agent, firstTransaction);

                Assertions.TransactionEventDoesNotHaveAttributes(secondTransactionUnexpectedTransactionEventIntrinsicAttributes, Tests.TestSerializationHelpers.Models.TransactionEventAttributeType.Intrinsic, secondTransaction);


                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("cloud.resource_id", out var cloudResourceIdValue));
                Assert.Equal("/subscriptions/subscription_id/resourceGroups/my_resource_group/providers/Microsoft.Web/sites/IntegrationTestAppName/functions/HttpTriggerFunctionUsingSimpleInvocation", cloudResourceIdValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.name", out var faasNameValue));
                Assert.Equal("HttpTriggerFunctionUsingSimpleInvocation", faasNameValue);
                Assert.True(firstTransaction.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
                Assert.Equal("http", faasTriggerValue);
            }
            else
            {
                Assertions.MetricsDoNotExist(simpleExpectedMetrics, metrics);
                Assert.Null(transactionSample);

                Assert.Empty(simpleTransactionEvents); // there should be no transactions when azure function mode is disabled
            }

            if (!_fixture.AzureFunctionModeEnabled) // look for a specific log line that indicates azure function mode is disabled
            {
                var disabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.AzureFunctionModeDisabledLogLineRegex);
                Assert.NotNull(disabledLogLine);
            }
        }


        [SkippableFact]
        public void Test_PipelineMode()
        {
            Skip.IfNot(_testMode == AzureFunctionHttpTriggerTestMode.AspNetCorePipeline);

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

            var expectedAgentAttributes = new Dictionary<string, object>
            {
                { "request.uri", "/api/httpTriggerFunctionUsingAspNetCorePipeline"},
                { "request.method", "GET" },
                { "http.statusCode", 200 }
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

    // The net6 target builds the function app without the aspnetcore pipeline package included
    [NetCoreTest]
    public class AzureFunctionHttpTriggerTestsCoreOldest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureHttpTriggerCoreOldest>
    {
        public AzureFunctionHttpTriggerTestsCoreOldest(AzureFunctionApplicationFixtureHttpTriggerCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, AzureFunctionHttpTriggerTestMode.SimpleInvocation)
        {
        }
    }

    // the net8 target builds the function app with the aspnetcore pipeline package
    [NetCoreTest]
    public class AzureFunctionHttpTriggerTestsCoreLatest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureHttpTriggerCoreLatest>
    {
        public AzureFunctionHttpTriggerTestsCoreLatest(AzureFunctionApplicationFixtureHttpTriggerCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, AzureFunctionHttpTriggerTestMode.AspNetCorePipeline)
        {
        }
    }
}

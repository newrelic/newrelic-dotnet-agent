// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AzureFunction;

public abstract class AzureFunctionServiceBusTriggerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : AzureFunctionApplicationFixture
{
    const string TestTraceId = "12345678901234567890123456789012";
    const string SpanId = "27ddd2d8890283b4";
    const string TransactionId = "5569065a5b1313bd";
    const bool Sampled = true;
    const string Priority = "1.23456";

    private readonly TFixture _fixture;

    protected AzureFunctionServiceBusTriggerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.SetAdditionalEnvironmentVariable("ServiceBus", AzureServiceBusConfiguration.ConnectionString);

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetOrDeleteSpanEventsEnabled(true);
                configModifier
                    .ForceTransactionTraces()
                    .ConfigureFasterTransactionTracesHarvestCycle(20)
                    .ConfigureFasterMetricsHarvestCycle(25)
                    .ConfigureFasterSpanEventsHarvestCycle(15)
                    .SetLogLevel("finest");

                // This is a bit of a kludge. When azure function instrumentation is disabled,
                // the agent instruments *two* processes: the azure function host (func.exe) and the actual function app.
                // Both processes use the same config files, so explicitly setting the log file name forces both
                // processes to log to the same file, which makes it easier to verify that the
                // actual function app is not being instrumented when the Invoke() method gets hit.
                //
                // Ideally, we'd prefer to look for the specific log file for the azure function app, but that's less trivial
                // and not worth the effort for this one test.
                if (!_fixture.AzureFunctionModeEnabled)
                {
                    configModifier.SetLogFileName("azure_function_instrumentation_disabled.log");
                }
            },
            exerciseApplication: () =>
            {
                // Invokes a function, sending distributed tracing headers in the HTTP trigger, that then creates a new Service Bus message
                // which is picked up by the Service Bus trigger function. This lets us verify that DT information makes it all the way through
                // from HTTP to Service Bus message creation to Service Bus message receive.
                _fixture.Post("api/HttpTrigger_SendServiceBusMessage", "test message");

                _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ServiceBusTriggerFunctionTest()
    {
        // other tests are verifying the expected Azure function attributes; we just need to make sure that we have a transaction
        // for sending the service bus message and a transaction for receiving the service bus message
        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();

        var sendMessageTransactionName = "WebTransaction/AzureFunction/HttpTrigger_SendServiceBusMessage";
        var receiveMessageTransactionName = "OtherTransaction/AzureFunction/ServiceBusTriggerFunction";

        var sendServiceBusMessageTransaction = transactionEvents.SingleOrDefault(e => e.IntrinsicAttributes["name"].ToString() == sendMessageTransactionName);
        var receiveServiceBusMessageTransaction = transactionEvents.SingleOrDefault(e => e.IntrinsicAttributes["name"].ToString() == receiveMessageTransactionName);

        // verify the expected metrics
        var metrics = _fixture.AgentLog.GetMetrics();
        var expectedMetrics = new List<Assertions.ExpectedMetric> {
            new() { metricName = "DotNet/HttpTrigger_SendServiceBusMessage"},
            new() { metricName = "DotNet/HttpTrigger_SendServiceBusMessage", metricScope = sendMessageTransactionName},
            new() { metricName = "DotNet/ServiceBusTriggerFunction"},
            new() { metricName = "DotNet/ServiceBusTriggerFunction", metricScope = receiveMessageTransactionName},
            new() { metricName = sendMessageTransactionName},
            new() { metricName = receiveMessageTransactionName},
        };

        Assert.Multiple(
            () => Assert.NotEmpty(transactionEvents),
            () => Assert.NotNull(sendServiceBusMessageTransaction),
            () => Assert.NotNull(receiveServiceBusMessageTransaction),
            () =>
            {
                Assert.True(receiveServiceBusMessageTransaction.IntrinsicAttributes.TryGetValue("faas.trigger", out var faasTriggerValue));
                Assert.Equal("pubsub", faasTriggerValue);
            },
            () => Assert.NotEmpty(metrics),
            () => Assertions.MetricsExist(expectedMetrics, metrics)
        );
    }

    [Fact]
    public void DistributedTraceHeadersArePropagated()
    {
        // get the transaction events and verify that all of them have the expected DT attributes
        var transactionEvents = _fixture.AgentLog.GetTransactionEvents();
        var spanEvents = _fixture.AgentLog.GetSpanEvents();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = "Supportability/DistributedTrace/CreatePayload/Success"},
            new() { metricName = "Supportability/TraceContext/Create/Success"},
            new() { metricName = "Supportability/TraceContext/Accept/Success"},
        };


        Assert.Multiple(
            () => Assert.NotEmpty(transactionEvents),
            () => Assert.NotEmpty(spanEvents),

            () => Assert.All(transactionEvents, transactionEvent =>
            {
                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("traceId", out var actualTraceId));
                Assert.Equal(TestTraceId, actualTraceId);

                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("priority", out var actualPriority));
                Assert.Equal(Priority, actualPriority.ToString().Substring(0, 7)); // keep the values the same length

                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("sampled", out var actualSampled));
                Assert.Equal(Sampled, actualSampled);
            }),

            // get the span events and verify that all of them have the expected DT attributes
            () => Assert.All(spanEvents, spanEvent =>
            {
                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("traceId", out var actualTraceId));
                Assert.Equal(TestTraceId, actualTraceId);

                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("priority", out var actualPriority));
                Assert.Equal(Priority, actualPriority.ToString().Substring(0, 7)); // keep the values the same length

                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("sampled", out var actualSampled));
                Assert.Equal(Sampled, actualSampled);
            }),

            () => Assertions.MetricsExist(expectedMetrics, _fixture.AgentLog.GetMetrics())
        );
    }
}

[NetCoreTest]
public class AzureFunctionServiceBusTriggerTestInProcCoreOldest : AzureFunctionServiceBusTriggerTestsBase<AzureFunctionApplicationFixtureServiceBusTriggerInProcCoreOldest>
{
    public AzureFunctionServiceBusTriggerTestInProcCoreOldest(AzureFunctionApplicationFixtureServiceBusTriggerInProcCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

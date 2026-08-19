// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AzureFunction;

/// <summary>
/// An isolated-worker HTTP trigger function writes a log message and then throws. The agent wrapper
/// (AzureFunctionIsolatedInvokeAsyncWrapper) ends the transaction in its finally block on a faulted
/// invocation, so the log written before the exception should still reach the collector.
/// </summary>
public class AzureFunctionHttpTriggerUnhandledExceptionTests : NewRelicIntegrationTest<AzureFunctionApplicationFixtureHttpTriggerThrowsCoreLatest>
{
    private const string TriggerTransactionName = "WebTransaction/AzureFunction/HttpTriggerFunctionThatThrows";

    private readonly AzureFunctionApplicationFixtureHttpTriggerThrowsCoreLatest _fixture;

    public AzureFunctionHttpTriggerUnhandledExceptionTests(AzureFunctionApplicationFixtureHttpTriggerThrowsCoreLatest fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .EnableApplicationLogging()
                    .EnableLogForwarding()
                    .EnableLogMetrics()
                    .ConfigureFasterMetricsHarvestCycle(15)
                    .SetLogLevel("debug");
            },
            exerciseApplication: () =>
            {
                // Control: proves log forwarding worked in this run before checking for the subject event.
                _fixture.Get("api/httpTriggerFunctionUsingSimpleInvocation");

                // An unhandled exception in an isolated HTTP function returns HTTP 500; that is expected here.
                _fixture.GetAndAssertStatusCode("api/httpTriggerFunctionThatThrows", HttpStatusCode.InternalServerError);

                // Gate teardown on the asserted data itself: the log, metric, and transaction-event harvests.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegexFor(AzureFunctionConfiguration.FuncTestPreExceptionLogMessage), TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForMetricAggregateCallCount(TriggerTransactionName, 1, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void PreExceptionLogEventIsForwarded()
    {
        var forwardedMessages = _fixture.AgentLog.GetLogEventDataLogLines().Select(logLine => logLine.Message).ToList();

        // Control: the HTTP trigger control function completes normally, so its log event proves that log
        // forwarding worked in this run. Without it, an absent subject log event would prove nothing.
        Assert.Contains(forwardedMessages, message => message != null && message.Contains(AzureFunctionConfiguration.FuncTestControlLogMessage));

        Assert.Contains(forwardedMessages, message => message != null && message.Contains(AzureFunctionConfiguration.FuncTestPreExceptionLogMessage));
    }

    [Fact]
    public void TransactionAndErrorAreRecorded()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = TriggerTransactionName },
            new() { metricName = "Errors/all" },
            new() { metricName = "Errors/" + TriggerTransactionName },
        };

        Assertions.MetricsExist(expectedMetrics, metrics);

        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
        Assert.Contains(transactionEvents, transactionEvent => transactionEvent.IntrinsicAttributes["name"].ToString() == TriggerTransactionName);
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AzureFunction;

// Each fixture here creates its own Service Bus queue and deletes it afterward, so these tests need no
// pre-created queue and cannot affect the tests that use the shared one. The connection string in the
// AzureServiceBusTests test configuration needs Manage rights on the namespace.

/// <summary>
/// An isolated-worker Service Bus trigger function writes a log message and then throws. The agent
/// wrapper ends the transaction in its finally block on a faulted invocation, so the log written
/// before the exception should reach the collector.
/// </summary>
public class AzureFunctionServiceBusTriggerUnhandledExceptionTests : NewRelicIntegrationTest<AzureFunctionApplicationFixtureServiceBusTriggerThrowsCoreLatest>
{
    private const string TriggerTransactionName = "OtherTransaction/AzureFunction/ServiceBusTriggerFunction_Throws";

    private readonly AzureFunctionApplicationFixtureServiceBusTriggerThrowsCoreLatest _fixture;

    public AzureFunctionServiceBusTriggerUnhandledExceptionTests(AzureFunctionApplicationFixtureServiceBusTriggerThrowsCoreLatest fixture, ITestOutputHelper output)
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
                _fixture.Post("api/HttpTrigger_SendServiceBusMessage", "test message");

                // The trigger function runs after the message makes a round trip through the Service Bus.
                // Its supportability metric proves the function ran and that a metric harvest carried its
                // metrics, which puts the transaction end well behind us.
                _fixture.AgentLog.WaitForMetricAggregateCallCount("Supportability/Dotnet/AzureFunction/Trigger/ServiceBus", 1, TimeSpan.FromMinutes(2));

                // Gate teardown on the asserted data itself: the log harvest that carries the pre-failure message.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegexFor(AzureServiceBusConfiguration.FuncTestPreExceptionLogMessage), TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void PreExceptionLogEventIsForwarded()
    {
        var forwardedMessages = _fixture.AgentLog.GetLogEventDataLogLines().Select(logLine => logLine.Message).ToList();

        // Control: the HTTP trigger transaction completes normally, so its log event proves that log
        // forwarding worked in this run. Without it, an absent trigger log event would prove nothing.
        Assert.Contains(forwardedMessages, message => message != null && message.Contains(AzureServiceBusConfiguration.FuncTestSendMessageLogMessage));

        Assert.Contains(forwardedMessages, message => message != null && message.Contains(AzureServiceBusConfiguration.FuncTestPreExceptionLogMessage));
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

/// <summary>
/// An isolated-worker Service Bus trigger function writes a log message and then blocks past the
/// host's functionTimeout. The invocation never returns, so the agent never ends the transaction that
/// holds the log event and the log never reaches the collector.
/// </summary>
public class AzureFunctionServiceBusTriggerTimeoutTests : NewRelicIntegrationTest<AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest>
{
    private const string TriggerTransactionName = "OtherTransaction/AzureFunction/ServiceBusTriggerFunction_Timeout";

    // The Functions host logs this when an invocation outlives functionTimeout.
    private const string HostTimeoutOutputMarker = "Timeout value of";

    // Waiting the timeout again plus 60 s covers the timeout and many more than the 5 s log send cycle,
    // so a log event that was going to be forwarded already was.
    private static readonly TimeSpan WaitPastTimeoutAndHarvests =
        AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest.FunctionTimeout + TimeSpan.FromSeconds(60);

    private readonly AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest _fixture;

    public AzureFunctionServiceBusTriggerTimeoutTests(AzureFunctionApplicationFixtureServiceBusTriggerTimeoutCoreLatest fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        // The abandoned transaction is the behavior under test, so the default known-problem check for
        // a garbage-collected transaction would fail this test for doing its job.
        _fixture.SetKnownProblems(keepDefaults: false);

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
                _fixture.Post("api/HttpTrigger_SendServiceBusMessage", "test message");

                // The trigger function never finishes, so there is no metric or log event of its own to
                // wait for. Wait for the send-side log event instead: it proves log forwarding works, and
                // it lands after the trigger function has already started.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegexFor(AzureServiceBusConfiguration.FuncTestSendMessageLogMessage), TimeSpan.FromMinutes(2));

                Thread.Sleep(WaitPastTimeoutAndHarvests);
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void PreTimeoutLogEventIsNotForwarded()
    {
        var hostOutput = _fixture.FunctionHostOutput;

        // Premise 1: the trigger function ran and wrote its log message. The host relays worker logs.
        Assert.Contains(AzureServiceBusConfiguration.FuncTestPreTimeoutLogMessage, hostOutput);

        // Premise 2: the host enforced functionTimeout, so the invocation never returned.
        Assert.Contains(HostTimeoutOutputMarker, hostOutput);

        var forwardedMessages = _fixture.AgentLog.GetLogEventDataLogLines().Select(logLine => logLine.Message).ToList();

        // Control: log forwarding worked in this run.
        Assert.Contains(forwardedMessages, message => message != null && message.Contains(AzureServiceBusConfiguration.FuncTestSendMessageLogMessage));

        // The transaction never ended, so its log event never reached the aggregator or the collector.
        Assert.DoesNotContain(forwardedMessages, message => message != null && message.Contains(AzureServiceBusConfiguration.FuncTestPreTimeoutLogMessage));
    }

    [Fact]
    public void NoTransactionIsRecordedForTheTimedOutInvocation()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        Assertions.MetricsDoNotExist(new List<Assertions.ExpectedMetric> { new() { metricName = TriggerTransactionName } }, metrics);

        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
        Assert.DoesNotContain(transactionEvents, transactionEvent => transactionEvent.IntrinsicAttributes["name"].ToString() == TriggerTransactionName);
    }
}

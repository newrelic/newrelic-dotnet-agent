// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.WCF.Service.Self;

/// <summary>
/// Regression coverage for the Wcf3 MethodInvokerWrapper null-dereference on
/// OperationContext.Current.
///
/// MethodInvokerWrapper.CaptureHttpRequestHeadersAndMethod reads
/// OperationContext.Current with no null check. It is reached only when no
/// transaction is found in context storage - and because WCF transaction storage
/// is itself backed by OperationContext.Current (OperationContextStorage,
/// priority 5) and HttpContext.Current (HttpContextStorage, priority 10), a
/// thread with neither ambient context fails the lookup AND the dereference.
///
/// The service here (a minimal Echo contract - see NullOperationContextEcho.cs)
/// is hosted with an IOperationInvoker that dispatches the instrumented
/// SyncMethodInvoker.Invoke onto a fresh thread, which strips both.
///
/// Without a fix this produces "Tracer invocation error" /
/// NullReferenceException on every call and, after WrapperExceptionLimit
/// (default 5) consecutive failures, the agent swaps in the NoOp wrapper for
/// SyncMethodInvoker.Invoke - permanently losing WebTransaction/WCF naming for
/// the life of the process.
/// </summary>
public class WCFService_Self_NullOperationContext : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
{
    // Must exceed WrapperExceptionLimit (default 5) so that an unfixed agent
    // reaches the disable threshold rather than merely logging a few errors.
    private const int CallCount = 8;

    private readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

    public WCFService_Self_NullOperationContext(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.SetTimeout(TimeSpan.FromMinutes(3));

        const string relativePath = "WCFServiceNullOpContext";

        // NetTcp deliberately, not BasicHttp: self-hosting an http:// endpoint
        // needs an HTTP.sys URL reservation, which fails with "HTTP could not
        // register URL ... Access is denied" on an unelevated dev machine.
        // net.tcp needs no reservation. The binding is irrelevant to the defect -
        // the null dereference happens before anything HTTP-specific is read.
        var binding = WCFBindingType.NetTcp;
        var port = _fixture.RemoteApplication.Port;

        _fixture.AddCommand($"WCFServiceSelfHosted StartServiceWithOffThreadInvoker {binding} {port} {relativePath}");
        _fixture.AddCommand($"NullOperationContextEchoClient InitializeClient {port} {relativePath}");

        for (var i = 0; i < CallCount; i++)
        {
            _fixture.AddCommand($"NullOperationContextEchoClient Echo {i}");
        }

        _fixture.AddCommand("WCFServiceSelfHosted StopService");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                _fixture.RemoteApplication.NewRelicConfig.ConfigureFasterMetricsHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void WrapperDoesNotThrowOnNullOperationContext()
    {
        var tracerErrors = _fixture.AgentLog.GetTracerInvocationErrorLineCount();

        Assert.True(tracerErrors == 0,
            $"Expected no tracer invocation errors, but found {tracerErrors}. " +
            "The Wcf3 MethodInvokerWrapper most likely dereferenced a null OperationContext.Current.");
    }

    [Fact]
    public void WrapperIsNotDisabled()
    {
        var disabled = _fixture.AgentLog.GetWrapperDisabledLines().ToList();

        Assert.True(disabled.Count == 0,
            "The agent disabled a wrapper due to consecutive exceptions, which permanently " +
            "reduces instrumentation for the life of the process. Disabled: " +
            string.Join("; ", disabled.Select(m => m.Value.Trim())));
    }

    [Fact]
    public void NullOperationContextSupportabilityMetric_IsRecordedForEveryCall_AndInvocationStyleIsAbsent()
    {
        // The wrapper records the invoked method name as a fourth metric segment
        // (Invoke, InvokeBegin, InvokeEnd, InvokeAsync, or Other as a fallback for
        // an unrecognized method). This test drives synchronous Echo calls only,
        // so the expected variant is Invoke.
        //
        // Expected count is CallCount (8): the exercised operation
        // (NullOperationContextEchoClient/INullOperationContextEchoService.Echo)
        // has no dependency on OperationContext.Current, incoming headers, or any
        // external call, so all 8 queued off-thread invocations complete and each
        // one bails out on the null OperationContext.Current, recording the
        // metric once per call.
        var expectedMetrics = new[]
        {
            new Assertions.ExpectedMetric { metricName = "Supportability/WCFService/NullOperationContext/Invoke", CallCountAllHarvests = CallCount }
        };

        // The bail-out returns before ReportSupportabilityMetric_InvocationMethod
        // runs, so a call that hit the null-OperationContext path should never
        // also record an invocation style. Confirmed absent from the log.
        //
        // The Other variant would only appear if the invoked-method-name lookup
        // failed to recognize SyncMethodInvoker.Invoke, which would itself be a
        // real defect in the wrapper. Confirmed absent from the log.
        var unexpectedMetrics = new[]
        {
            new Assertions.ExpectedMetric { metricName = "Supportability/WCFService/InvocationStyle/Sync" },
            new Assertions.ExpectedMetric { metricName = "Supportability/WCFService/NullOperationContext/Other" }
        };

        var actualMetrics = _fixture.AgentLog.GetMetrics();

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
            () => Assertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
        );
    }

    // Deliberately NOT asserting that WebTransaction/WCF/* metrics are present.
    //
    // That would be the most direct expression of customer impact - the disable
    // makes WCF transactions collapse into the generic host-level name - but it
    // depends on a full harvest round-tripping through the collector, which is
    // timing-sensitive and the usual source of flakiness in the WCF metric
    // assertions. This test is about a deterministic null dereference, so it
    // should not inherit that timing dependency.
    //
    // The two log-based assertions above hold regardless of harvest timing. The
    // Supportability/WCFService/NullOperationContext/Invoke assertion in
    // NullOperationContextSupportabilityMetric_IsRecordedOnce_AndInvocationStyleIsAbsent
    // is a different, safe case even though it does depend on a harvest: the fast
    // ConfigureFasterMetricsHarvestCycle(10) plus the WaitForLogLine wait on
    // MetricDataLogLineRegex already gate the whole test on that harvest having
    // completed before any [Fact] runs, so there is no additional timing
    // dependency being introduced here.
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation;

/// <summary>
/// Covers instrumentation of runtime-async methods. The application is built for
/// net10.0 with runtime-async enabled as a preview feature, so this needs no newer SDK.
///
/// The assertion that matters is the scoped count of the nested InnerAsync segment. InnerAsync
/// is called twice per OuterAsync invocation, both times after a suspension point, so its
/// continuations resume on whatever thread the pool supplies. When a runtime-async transaction
/// was left stranded in its creating thread's primary storage, roughly a quarter of those
/// nested segments were silently dropped -- no error, no unfinished segment, just a lower
/// count. Only an exact count catches that.
///
/// Requires a profiler that can instrument runtime-async methods. Against an older profiler
/// these methods are rewritten incorrectly and the application dies with
/// InvalidProgramException rather than failing an assertion -- if that is what you are seeing,
/// check the profiler build rather than this test.
/// </summary>
public class RuntimeAsyncTests : NewRelicIntegrationTest<RuntimeAsyncTestsFixture>
{
    private readonly RuntimeAsyncTestsFixture _fixture;

    // Must match RuntimeAsyncUseCases.Iterations in the test application.
    private const int Iterations = 25;

    // OuterAsync calls InnerAsync twice per invocation.
    private const int ExpectedNestedSegmentCount = Iterations * 2;

    // Attribute-instrumented methods name their transaction with the Custom/ category but
    // their segments with the DotNet/ prefix.
    private const string OuterTransactionName = @"OtherTransaction/Custom/RuntimeAsyncApplication.RuntimeAsyncUseCases/OuterAsync";
    private const string OuterSegmentName = @"DotNet/RuntimeAsyncApplication.RuntimeAsyncUseCases/OuterAsync";
    private const string InnerSegmentName = @"DotNet/RuntimeAsyncApplication.RuntimeAsyncUseCases/InnerAsync";

    // A plain synchronous transaction, run once, that acts as the positive control for thread.id.
    private const string ControlTransactionName = @"OtherTransaction/Custom/RuntimeAsyncApplication.RuntimeAsyncUseCases/SynchronousControl";
    private const string ControlSegmentName = @"DotNet/RuntimeAsyncApplication.RuntimeAsyncUseCases/SynchronousControl";

    private const string ThreadIdAttributeName = "thread.id";

    public RuntimeAsyncTests(RuntimeAsyncTestsFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                // Runtime-async IL is rejected at type load on .NET 10 without this. The
                // failure is a TypeLoadException before any assertion runs, so a missing
                // variable fails the test loudly rather than quietly weakening it.
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("DOTNET_RuntimeAsync", "1");

                // Matches NetCoreAsyncTests, the closest sibling: the event listener samplers
                // add unrelated background activity to a short-lived console app.
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.DisableEventListenerSamplers();

                // thread.id lives on span events, so they have to be on to assert about it.
                configModifier.SetOrDeleteSpanEventsEnabled(true);

                // Every transaction sampled, so every one of them produces a span. Without this the
                // default adaptive sampler takes only about ten per minute and the application would
                // have to be ordered so the transactions being asserted on come first -- a
                // constraint that is invisible from the test and easy to break by adding a use case.
                configModifier.SetRootSamplerAlwaysOn();
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
            }
        );

        _fixture.AddActions
        (
            exerciseApplication: () =>
            {
                // Without waiting for the span harvest the assertions run against an empty
                // span collection, which fails as "collection was empty" rather than anything
                // to do with thread.id.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        Assert.NotNull(metrics);

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(_expectedMetrics, metrics)
        );
    }

    /// <summary>
    /// The agent records thread.id only for non-async segments (Segment.cs gates it on !IsAsync),
    /// because an async segment can finish on a thread other than the one that started it, which
    /// would make the value misleading. WrapperService promotes runtime-async methods to IsAsync
    /// before building the MethodCall that Segment.IsAsync comes from, so they must omit it too.
    ///
    /// SynchronousControl is what keeps this honest. Asserting only that an attribute is absent
    /// passes just as happily when the attribute is never emitted at all, so the control asserts
    /// the opposite: a segment that genuinely should carry thread.id does carry it.
    ///
    /// The control is synchronous rather than state-machine async because runtime-async is enabled
    /// for this whole assembly, so a state-machine async method cannot be written here. That the
    /// two async forms agree follows from their sharing the one !IsAsync gate.
    /// </summary>
    [Fact]
    public void RuntimeAsyncSpansOmitThreadId()
    {
        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

        Assert.NotEmpty(spanEvents);

        var controlSpans = SpansNamed(spanEvents, ControlSegmentName);
        var outerSpans = SpansNamed(spanEvents, OuterSegmentName);
        var innerSpans = SpansNamed(spanEvents, InnerSegmentName);

        // Span naming is easy to get wrong and the harvested names are not otherwise visible
        // once the test working directory is cleaned up, so report them on failure.
        var available = string.Join(", ", spanEvents
            .Select(s => s.IntrinsicAttributes.TryGetValue("name", out var n) ? n as string : "<unnamed>")
            .Distinct()
            .OrderBy(n => n));

        NrAssert.Multiple
        (
            // Positive control: a synchronous segment does carry thread.id.
            () => Assert.True(controlSpans.Count > 0, $"No span named {ControlSegmentName}. Harvested span names: {available}"),
            () => Assert.True(outerSpans.Count > 0, $"No span named {OuterSegmentName}. Harvested span names: {available}"),
            () => Assert.True(innerSpans.Count > 0, $"No span named {InnerSegmentName}. Harvested span names: {available}"),

            () => Assert.All(controlSpans, span =>
                Assert.True(span.IntrinsicAttributes.ContainsKey(ThreadIdAttributeName),
                    $"{ControlSegmentName} is synchronous and should carry {ThreadIdAttributeName}. " +
                    "If this fails the attribute may have stopped being emitted at all, which would " +
                    "make the runtime-async assertions below meaningless rather than passing.")),

            // The actual assertion: runtime-async segments omit it, as state-machine async does.
            () => Assert.All(outerSpans, span =>
                Assert.False(span.IntrinsicAttributes.ContainsKey(ThreadIdAttributeName),
                    $"{OuterSegmentName} is runtime-async and should omit {ThreadIdAttributeName}.")),

            () => Assert.All(innerSpans, span =>
                Assert.False(span.IntrinsicAttributes.ContainsKey(ThreadIdAttributeName),
                    $"{InnerSegmentName} is runtime-async and should omit {ThreadIdAttributeName}."))
        );
    }

    private static List<SpanEvent> SpansNamed(IEnumerable<SpanEvent> spanEvents, string name) =>
        spanEvents
            .Where(span => span.IntrinsicAttributes.TryGetValue("name", out var spanName)
                           && name.Equals(spanName as string))
            .ToList();

    private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
    {
        // Exactly one transaction per iteration. InnerAsync carries [Transaction] too, so if
        // nesting broke and it started its own transaction instead of recording a segment,
        // this count would come in at three times the expected value.
        new Assertions.ExpectedMetric { metricName = OuterTransactionName, CallCountAllHarvests = Iterations },
        // Iterations async transactions plus the one synchronous control transaction.
        new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", CallCountAllHarvests = Iterations + 1 },
        new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", CallCountAllHarvests = Iterations + 1 },
        new Assertions.ExpectedMetric { metricName = ControlTransactionName, CallCountAllHarvests = 1 },

        new Assertions.ExpectedMetric { metricName = OuterSegmentName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, CallCountAllHarvests = ExpectedNestedSegmentCount },

        // The load-bearing assertions: both segments recorded *within* OuterAsync's
        // transaction, the nested one on every single invocation.
        new Assertions.ExpectedMetric { metricName = OuterSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = ExpectedNestedSegmentCount },
    };
}

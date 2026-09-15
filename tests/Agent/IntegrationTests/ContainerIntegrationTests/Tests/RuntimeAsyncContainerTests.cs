// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

/// <summary>
/// The Linux counterpart of the host-run RuntimeAsyncTests. Runtime-async instrumentation is
/// implemented in the native profiler, and the Linux profiler is a separate binary from the Windows
/// one, so passing on Windows says nothing about whether the IL rewriting is correct here.
///
/// Asserts the same two things the host-run test does: that nested segments survive (a stranded
/// runtime-async transaction silently dropped roughly a quarter of them, which only an exact count
/// catches), and that runtime-async spans omit thread.id while a synchronous control carries it.
///
/// Requires a profiler that can instrument runtime-async methods. Against an older one these
/// methods are rewritten incorrectly and the application dies with InvalidProgramException rather
/// than failing an assertion -- if that is what you are seeing, check the agent home this mounted
/// rather than this test.
/// </summary>
[Trait("Architecture", "amd64")]
[Trait("TestArea", "Core")]
public class RuntimeAsyncContainerTests : NewRelicIntegrationTest<RuntimeAsyncContainerTestFixture>
{
    private readonly RuntimeAsyncContainerTestFixture _fixture;

    // Must match RuntimeAsyncUseCases.Iterations in the test application.
    private const int Iterations = 25;

    // OuterAsync calls InnerAsync twice per invocation.
    private const int ExpectedNestedSegmentCount = Iterations * 2;

    private const string OuterTransactionName = @"OtherTransaction/Custom/RuntimeAsyncTestApp.RuntimeAsyncUseCases/OuterAsync";
    private const string OuterSegmentName = @"DotNet/RuntimeAsyncTestApp.RuntimeAsyncUseCases/OuterAsync";
    private const string InnerSegmentName = @"DotNet/RuntimeAsyncTestApp.RuntimeAsyncUseCases/InnerAsync";

    private const string ControlTransactionName = @"OtherTransaction/Custom/RuntimeAsyncTestApp.RuntimeAsyncUseCases/SynchronousControl";
    private const string ControlSegmentName = @"DotNet/RuntimeAsyncTestApp.RuntimeAsyncUseCases/SynchronousControl";

    private const string ThreadIdAttributeName = "thread.id";

    public RuntimeAsyncContainerTests(RuntimeAsyncContainerTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                // The event listener samplers add unrelated background activity to a short-lived app.
                configModifier.DisableEventListenerSamplers();

                // thread.id lives on span events, so they have to be on to assert about it.
                configModifier.SetOrDeleteSpanEventsEnabled(true);

                // Every transaction sampled, so every one produces a span regardless of how many
                // ran before it. Without this the default adaptive sampler takes only about ten per
                // minute and the synchronous control -- which runs last -- would produce no span.
                configModifier.SetRootSamplerAlwaysOn();

                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.ExerciseApplication();

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));

                _fixture.ShutdownRemoteApplication();
            });

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

    [Fact]
    public void RuntimeAsyncSpansOmitThreadId()
    {
        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

        Assert.NotEmpty(spanEvents);

        var controlSpans = SpansNamed(spanEvents, ControlSegmentName);
        var outerSpans = SpansNamed(spanEvents, OuterSegmentName);
        var innerSpans = SpansNamed(spanEvents, InnerSegmentName);

        var available = string.Join(", ", spanEvents
            .Select(s => s.IntrinsicAttributes.TryGetValue("name", out var n) ? n as string : "<unnamed>")
            .Distinct()
            .OrderBy(n => n));

        NrAssert.Multiple
        (
            // Positive control: without it, asserting an attribute is absent would pass just as
            // happily if the attribute were never emitted at all.
            () => Assert.True(controlSpans.Count > 0, $"No span named {ControlSegmentName}. Harvested span names: {available}"),
            () => Assert.True(outerSpans.Count > 0, $"No span named {OuterSegmentName}. Harvested span names: {available}"),
            () => Assert.True(innerSpans.Count > 0, $"No span named {InnerSegmentName}. Harvested span names: {available}"),

            () => Assert.All(controlSpans, span =>
                Assert.True(span.IntrinsicAttributes.ContainsKey(ThreadIdAttributeName),
                    $"{ControlSegmentName} is synchronous and should carry {ThreadIdAttributeName}.")),

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
        new Assertions.ExpectedMetric { metricName = OuterTransactionName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = ControlTransactionName, CallCountAllHarvests = 1 },

        new Assertions.ExpectedMetric { metricName = OuterSegmentName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, CallCountAllHarvests = ExpectedNestedSegmentCount },

        // The load-bearing assertions: both segments recorded *within* OuterAsync's transaction,
        // the nested one on every single invocation.
        new Assertions.ExpectedMetric { metricName = OuterSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = ExpectedNestedSegmentCount },
    };
}

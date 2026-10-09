// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry;

public abstract class ActivityParentIndexTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private const int Iterations = 3;
    private const string ActivitySourceName = "ActivityParentIndexExerciser";
    private readonly TFixture _fixture;

    protected ActivityParentIndexTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        // The bridge subscribes its ActivityListener during agent startup.
        _fixture.AddCommand("RootCommands DelaySeconds 10");
        _fixture.AddCommand($"ActivityParentIndexExerciser RunWithTransactionAFinished {Iterations}");
        _fixture.AddCommand($"ActivityParentIndexExerciser RunWithTransactionARunning {Iterations}");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .EnableOpenTelemetry(true)
                    .EnableOpenTelemetryTracing(true)
                    .IncludeActivitySource(ActivitySourceName)
                    .SetRootSamplerAlwaysOn()
                    .ConfigureFasterSpanEventsHarvestCycle(15)
                    .SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void NoBridgeOrCallStackErrorsAreLogged()
    {
        var errors = _fixture.AgentLog.TryGetLogLines(AgentLogBase.ErrorLogLinePrefixRegex + ".*(OpenTelemetry bridge|ArgumentOutOfRangeException)").ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void OrphanUnderFinishedTransactionA_GoesToTransactionB()
    {
        var spans = _fixture.AgentLog.GetSpanEvents().ToList();

        for (var i = 1; i <= Iterations; i++)
        {
            var orphan = FindSpan(spans, $"Finished-B-orphan-{i}");
            var consumerB = FindSpan(spans, $"Finished-B-consume-{i}");

            Assert.Equal(consumerB.IntrinsicAttributes["traceId"], orphan.IntrinsicAttributes["traceId"]);
        }
    }

    [Fact]
    public void OrphanUnderRunningTransactionA_GoesToTransactionA_WithDeepSegmentAsParent()
    {
        var spans = _fixture.AgentLog.GetSpanEvents().ToList();

        for (var i = 1; i <= Iterations; i++)
        {
            var orphan = FindSpan(spans, $"Running-B-orphan-{i}");
            var deep = FindSpan(spans, $"Running-A-deep-{i}");

            Assert.Equal(deep.IntrinsicAttributes["traceId"], orphan.IntrinsicAttributes["traceId"]);
            Assert.Equal(deep.IntrinsicAttributes["guid"], orphan.IntrinsicAttributes["parentId"]);
        }
    }

    // Exact match: a Consumer activity also yields a root span whose name is the transaction name and contains the activity name.
    private static SpanEvent FindSpan(IEnumerable<SpanEvent> spans, string activityName)
    {
        return Assert.Single(spans, s => string.Equals(s.IntrinsicAttributes["name"] as string, activityName, StringComparison.Ordinal));
    }
}

public class ActivityParentIndexTests_CoreLatest : ActivityParentIndexTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public ActivityParentIndexTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Hangfire;

public abstract class HangfireTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly Version _version;

    // Expected call counts
    private readonly int _maxFailureCount = 2; // 1 sync and 1 async failing job
    private readonly int _maxCallCount = 8; // 3 of each simplejob * 2 (sync and async) + 2 failed jobs


    protected HangfireTestsBase(TFixture fixture, ITestOutputHelper output, Version version) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;
        _version = version;

        _fixture.AddCommand("HangfireExerciser StartHost");
        _fixture.AddCommand("HangfireExerciser EnqueueJobs");
        _fixture.AddCommand("RootCommands DelaySeconds 10");
        _fixture.AddCommand("HangfireExerciser StopHost");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ForceTransactionTraces().SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                // Reduce the wait times as we check for different payloads.
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ErrorEventDataLogLineRegex, TimeSpan.FromSeconds(30));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ErrorTraceDataLogLineRegex, TimeSpan.FromSeconds(15));
                
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        // Due to how jobs are run and complete, the counts are not deterministic so we are using ranges.

        var metricPrefix = "DotNet/Hangfire/MultiFunctionApplicationHelpers.NetStandardLibraries.Hangfire.TestJobs";

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".SimpleJob"},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".FailingJob"},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".SimpleAsyncJob"},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".FailingAsyncJob"},
        };

        Assertions.MetricsExist(expectedMetrics, metrics);

        var errorEvents = _fixture.AgentLog.GetErrorEvents().Where(e => e.IntrinsicAttributes["transactionName"].ToString().Contains("Hangfire")).ToList();
        var errorTraces = _fixture.AgentLog.GetErrorTraces().Where(t => t.Path.Contains("Hangfire")).ToList();

        Assert.InRange(errorEvents.Count, 1, _maxFailureCount);
        Assert.InRange(errorTraces.Count, 1, _maxFailureCount);

        var spans = _fixture.AgentLog.GetSpanEvents().Where(s => s.AgentAttributes.ContainsKey("workflow.platform.name")).ToList();
        Assert.InRange(spans.Count, 6, _maxCallCount);
        foreach (var span in spans)
        {
            Assert.Equal("hangfire", span.AgentAttributes["workflow.platform.name"]);
            Assert.NotNull(span.AgentAttributes["workflow.task.name"]);
            Assert.NotNull(span.AgentAttributes["workflow.task.id"]);
            if (_version.Minor >= 8) // 1.7 does not pass the server into the PerformContext.
            {
                Assert.NotNull(span.AgentAttributes["workflow.task.server"]);
            }

            var result = span.AgentAttributes["workflow.execution.result"].ToString();
            Assert.NotNull(result);
            if (result == "failure")
            {
                Assert.Equal("System.InvalidOperationException", span.AgentAttributes["error.class"]);
                Assert.Equal("Job intentionally failed", span.AgentAttributes["error.message"]);
            }
        }

        var transactions = _fixture.AgentLog.GetTransactionEvents()
            .Where(t => t.IntrinsicAttributes["name"].ToString().StartsWith("OtherTransaction/Hangfire"))
            .ToList();
        Assert.InRange(transactions.Count, 6, _maxCallCount);
    }
}

public class HangfireTests_CoreOldest : HangfireTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public HangfireTests_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output, new Version(1,7))
    {
    }
}

public class HangfireTests_CoreLatest : HangfireTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public HangfireTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output, new Version(1, 8))
    {
    }
}

public class HangfireTests_FWOldest : HangfireTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public HangfireTests_FWOldest(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output, new Version(1, 7))
    {
    }
}

public class HangfireTests_FWLatest : HangfireTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public HangfireTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, new Version(1, 8))
    {
    }
}

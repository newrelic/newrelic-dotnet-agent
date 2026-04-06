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
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(2));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var metricPrefix = "DotNet/Hangfire/MultiFunctionApplicationHelpers.NetStandardLibraries.Hangfire.TestJobs";

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // We expect 3 calls to the simple jobs (parent, child, and standalone) and 1 call to the failing job. The async variants should have the same counts.
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".SimpleJob", CallCountAllHarvests = 3},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".FailingJob", CallCountAllHarvests = 1},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".SimpleAsyncJob", CallCountAllHarvests = 3},
            new Assertions.ExpectedMetric {metricName = metricPrefix + ".FailingAsyncJob", CallCountAllHarvests = 1},
        };

        Assertions.MetricsExist(expectedMetrics, metrics);

        var spans = _fixture.AgentLog.GetSpanEvents().Where(s => s.AgentAttributes.ContainsKey("workflow.platform.name")).ToList();
        Assert.Equal(8, spans.Count);
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
                Assert.Equal("JobPerformanceException", span.AgentAttributes["error.type"]);
            }
        }

        var transactions = _fixture.AgentLog.GetTransactionEvents()
            .Where(t => t.IntrinsicAttributes["name"].ToString().StartsWith("OtherTransaction/Hangfire"))
            .ToList();
        Assert.Equal(8, transactions.Count);
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

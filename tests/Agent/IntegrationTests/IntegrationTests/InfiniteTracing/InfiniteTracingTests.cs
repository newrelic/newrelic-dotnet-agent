// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.InfiniteTracing
{
    public abstract class InfiniteTracingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        const int ExpectedSentCount = 2;

        public InfiniteTracingTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"InfiniteTracingTester StartAgent");

            // Give the agent time to warm up... If we send a span too soon, it will be sent via DT (span_event_data) instead of 8T (gRPC)
            _fixture.AddCommand("RootCommands DelaySeconds 15"); 

            _fixture.AddCommand($"InfiniteTracingTester Make8TSpan");

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.ForceTransactionTraces()
                    .EnableDistributedTrace()
                    .EnableInfiniteTracing(_fixture.TestConfiguration.TraceObserverUrl, _fixture.TestConfiguration.TraceObserverPort)
                    .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    // wait up to 2 minutes for the correct number of server response "success" messages
                    var waitUntil = DateTime.Now.AddMinutes(2);
                    while (DateTime.Now < waitUntil)
                    {
                        var successCount = 0;
                        var matches = _fixture.AgentLog.WaitForLogLines(AgentLogBase.SpanStreamingSuccessfullyProcessedByServerResponseLogLineRegex, TimeSpan.FromMinutes(2));
                        foreach (var match in matches)
                        {
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var matchValue))
                                successCount += matchValue;
                        }

                        // kick out of the loop if we found the right number of successes
                        if (successCount == ExpectedSentCount)
                            break;

                        // wait a bit before checking again
                        Thread.Sleep(1000);
                    }
                }

            );

            _fixture.Initialize();
        }

        [SkipOnAlpineFact("See https://github.com/newrelic/newrelic-dotnet-agent/issues/289")]
        public void Test()
        {
            //1 span count for the Make8TSpan method, another span count for the root span.
            var expectedSeenCount = 2;
            var expectedReceivedCount = 2;

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", callCount = expectedSeenCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", callCount = ExpectedSentCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Received", callCount = expectedReceivedCount }
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(actualMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class InfiniteTracingFWLatestTests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public InfiniteTracingFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }


    [NetFrameworkTest]
    public class InfiniteTracingFW471Tests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public InfiniteTracingFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class InfiniteTracingFW462Tests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public InfiniteTracingFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingNetCoreLatestTests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public InfiniteTracingNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingNetCore50Tests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public InfiniteTracingNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingNetCore31Tests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public InfiniteTracingNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

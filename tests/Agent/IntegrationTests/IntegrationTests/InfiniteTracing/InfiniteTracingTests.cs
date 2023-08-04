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
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand("InfiniteTracingTester StartAgent");

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
                    // Wait for 8T to connect
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanStreamingServiceConnectedLogLineRegex, TimeSpan.FromSeconds(15));
                    // Now send the command to make the 8T Span
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    // Now wait to see that the 8T spans were sent successfully
                    _fixture.AgentLog.WaitForLogLinesCapturedIntCount(AgentLogBase.SpanStreamingSuccessfullySentLogLineRegex, TimeSpan.FromMinutes(1), ExpectedSentCount);
                }

            );

            _fixture.Initialize();
        }

        [SkipOnAlpineFact("See https://github.com/newrelic/newrelic-dotnet-agent/issues/289")]
        public void Test()
        {
            //1 span count for the Make8TSpan method, another span count for the root span.
            var expectedSeenCount = 2;

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", callCount = expectedSeenCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", callCount = ExpectedSentCount },
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
    public class InfiniteTracingNetCoreOldestTests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public InfiniteTracingNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

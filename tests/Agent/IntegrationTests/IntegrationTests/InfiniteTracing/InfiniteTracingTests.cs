// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
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

        public InfiniteTracingTestsBase(TFixture fixture, ITestOutputHelper output):base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"InfiniteTracingTester StartAgent");
            _fixture.AddCommand($"InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand($"InfiniteTracingTester Wait");


            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.ForceTransactionTraces()
                    .EnableDistributedTrace()
                    .EnableInfinteTracing(_fixture.TestConfiguration.TraceObserverUrl)
                    .SetLogLevel("finest");
                }
            );

            _fixture.Initialize();
        }

        [SkipOnAlpineFact("See https://github.com/newrelic/newrelic-dotnet-agent/issues/289")]
        public void Test()
        {
            //1 span count for the Make8TSpan method, another span count for the root span.
            var expectedSeenCount = 2;
            var expectedSentCount = 2;
            var expectedReceivedCount = 2;

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", callCount = expectedSeenCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", callCount = expectedSentCount },
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

    [NetCoreTest]
    public class InfiniteTracingNetCore21Tests : InfiniteTracingTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public InfiniteTracingNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

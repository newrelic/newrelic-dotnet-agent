// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NServiceBus.Features;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.InfiniteTracing
{
    public abstract class InfiniteTracingFlakyTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public InfiniteTracingFlakyTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(5));

            // Ensure the trace observer will throw an error on every request
            _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY", "100");

            // set the code to be returned when the trace observer throws an error
            // must be a value from 0 to 16 as per https://github.com/grpc/grpc/blob/master/doc/statuscodes.md
            // use the StatusCode enum from gRPC 
            _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY_CODE",
                ((int)Grpc.Core.StatusCode.Internal).ToString());

            _fixture.AddCommand("InfiniteTracingTester StartAgent");

            _fixture.AddCommand("RootCommands DelaySeconds 15"); // give the agent time to warm up

            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");
            _fixture.AddCommand("InfiniteTracingTester Make8TSpan");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                        .ForceTransactionTraces()
                        .EnableDistributedTrace()
                        .EnableInfiniteTracing(_fixture.TestConfiguration.TraceObserverUrl, _fixture.TestConfiguration.TraceObserverPort)
                        .SetLogLevel("finest");
                }
                ,
                exerciseApplication: () =>
                {
                    // wait up to 75 seconds for the harvest cycle to complete and emit the supportability metrics we're expecting
                    var waitUntil = DateTime.Now.AddSeconds(75);
                    while (DateTime.Now <= waitUntil
                           && !_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "Supportability/InfiniteTracing/Span/gRPC/INTERNAL")
                           && !_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "Supportability/InfiniteTracing/Span/Response/Error"))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                }
            );

            _fixture.Initialize();
        }

        [SkipOnAlpineFact("See https://github.com/newrelic/newrelic-dotnet-agent/issues/289")]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // can't look for specific counts, as errors will cause the counts to vary
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen" },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent" },

                // these two, however, should reliably appear
                new Assertions.ExpectedMetric() { metricName = "Supportability/InfiniteTracing/Span/Response/Error"},
                new Assertions.ExpectedMetric() { metricName = "Supportability/InfiniteTracing/Span/gRPC/INTERNAL"}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, actualMetrics);
        }
    }

    [NetFrameworkTest]
    public class InfiniteTracingFlakyFWLatestTests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public InfiniteTracingFlakyFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }


    [NetFrameworkTest]
    public class InfiniteTracingFlakyFW471Tests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public InfiniteTracingFlakyFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class InfiniteTracingFlakyFW462Tests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public InfiniteTracingFlakyFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingFlakyNetCoreLatestTests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public InfiniteTracingFlakyNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingFlakyNetCore50Tests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public InfiniteTracingFlakyNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class InfiniteTracingFlakyNetCore31Tests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public InfiniteTracingFlakyNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

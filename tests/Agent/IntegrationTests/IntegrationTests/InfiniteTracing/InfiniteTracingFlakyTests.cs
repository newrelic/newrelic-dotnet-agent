// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
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
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Ensure the trace observer will throw an error on every request
            _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY", "100");

            // set the code to be returned when the trace observer throws an error
            // must be a value from 0 to 16 as per https://github.com/grpc/grpc/blob/master/doc/statuscodes.md
            // use the StatusCode enum from gRPC 
            _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY_CODE",
                ((int)Grpc.Core.StatusCode.Internal).ToString());

            _fixture.AddCommand("InfiniteTracingTester StartAgent");

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
                    // Wait for 8T to connect
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanStreamingServiceConnectedLogLineRegex, TimeSpan.FromSeconds(15));
                    // Now send the command to make the 8T Span
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    _fixture.SendCommand("InfiniteTracingTester Make8TSpan");
                    // Now wait to see that the 8T spans were sent successfully
                    _fixture.AgentLog.WaitForLogLinesCapturedIntCount(AgentLogBase.SpanStreamingSuccessfullySentLogLineRegex, TimeSpan.FromSeconds(45), 12);
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.SpanStreamingResponseGrpcError, TimeSpan.FromSeconds(45));
                }
            );

            _fixture.Initialize();
        }

        [SkipOnAlpineFact("See https://github.com/newrelic/newrelic-dotnet-agent/issues/289")]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // Flaky mode accepts data, but errors out on the server responses. These metrics are for upload, not response, so we can expect counts
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", CallCountAllHarvests = 12 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", CallCountAllHarvests = 12 },

                // The response error metrics are dependant on the number of consumers that claim spans, so we can not check count
                new Assertions.ExpectedMetric() { metricName = "Supportability/InfiniteTracing/Span/Response/Error" },                
                new Assertions.ExpectedMetric() { metricName = "Supportability/InfiniteTracing/Span/gRPC/INTERNAL" }
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
    public class InfiniteTracingFlakyNetCoreOldestTests : InfiniteTracingFlakyTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public InfiniteTracingFlakyNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

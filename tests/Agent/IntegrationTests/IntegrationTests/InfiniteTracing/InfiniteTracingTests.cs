// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.InfiniteTracing
{
    public class InfiniteTracingTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureCoreLatest>
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        public InfiniteTracingTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output):base(fixture)
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

        [Fact]
        public void Test()
        {
            var sendCount = _fixture.AgentLog.GetInfiniteTracingAttemptToSendCount();
            var sentCount = _fixture.AgentLog.GetInfiniteTracingAttemptToSendSuccessCount();
            var receivedCount = _fixture.AgentLog.GetInfiniteTracingGrpcServerReceivedCount();

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Seen", callCount = sendCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Sent", callCount = sentCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/InfiniteTracing/Span/Received", callCount = receivedCount }
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(actualMetrics, metrics);
        }
    }
}

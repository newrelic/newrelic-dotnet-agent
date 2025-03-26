// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetCoreTest]
    public class SerilogSumologicSyncTests : NewRelicIntegrationTest<RemoteServiceFixtures.SerilogSumologicFixture>
    {
        private readonly RemoteServiceFixtures.SerilogSumologicFixture _fixture;

        public SerilogSumologicSyncTests(RemoteServiceFixtures.SerilogSumologicFixture fixture, ITestOutputHelper output, bool synchronousMethodFirst = true) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    if (synchronousMethodFirst)
                    {
                        _fixture.SyncControllerMethod();
                        _fixture.AsyncControllerMethod();
                    }
                    else
                    {
                        _fixture.AsyncControllerMethod();
                        _fixture.SyncControllerMethod();
                    }
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assert.NotNull(metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(_expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(_unexpectedMetrics, metrics)
                );
        }

        private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET"},
        };

        private readonly List<Assertions.ExpectedMetric> _unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"External/endpoint3.collection.us2.sumologic.com/Stream/POST"}
        };
    }

    [NetCoreTest]
    public class SerilogSumologicAsyncTests : SerilogSumologicSyncTests
    {
        public SerilogSumologicAsyncTests(RemoteServiceFixtures.SerilogSumologicFixture fixture, ITestOutputHelper output)
            : base(fixture, output, synchronousMethodFirst: false)
        {
        }
    }
}

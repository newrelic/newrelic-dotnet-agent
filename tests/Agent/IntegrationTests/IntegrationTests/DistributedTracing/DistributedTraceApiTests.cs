// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{

    public class DistributedTracingApiTests_W3C : DtApiTestBase
    {
        public DistributedTracingApiTests_W3C(RemoteServiceFixtures.DistributedTracingApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output, TracingTestOption.W3cAndNewrelicHeaders)
        {
        }

        [Fact]
        public override void Metrics()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/InsertDistributedTraceHeaders", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/AcceptDistributedTraceHeaders", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CurrentTransaction", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

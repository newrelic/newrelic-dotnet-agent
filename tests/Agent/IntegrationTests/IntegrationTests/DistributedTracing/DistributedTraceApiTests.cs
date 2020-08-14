// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    [NetFrameworkTest]
    public class DistributedTracingApiTests_Legacy : DtApiTestBase
    {
        public DistributedTracingApiTests_Legacy(RemoteServiceFixtures.DistributedTracingApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output, TracingTestOption.Legacy)
        {
        }

        [Fact]
        public override void Metrics()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CreateDistributedTracePayload", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/AcceptDistributedTracePayload", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CurrentTransaction", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/AcceptPayload/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }

    [NetFrameworkTest]
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
                new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CurrentTransaction", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

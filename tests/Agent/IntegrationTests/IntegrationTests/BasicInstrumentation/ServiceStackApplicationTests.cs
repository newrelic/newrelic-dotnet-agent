// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    public class ServiceStackApplicationTests : NewRelicIntegrationTest<ServiceStackApplicationFixture>
    {

        private readonly ServiceStackApplicationFixture _fixture;
        private const int ExpectedTransactionCount = 1;

        public ServiceStackApplicationTests(ServiceStackApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.GetHello();
                }
            );
            _fixture.Initialize();
        }

        /// <summary>
        /// The SeqRequestLogsFeature in ServiceStack will serialize items stored in HttpContext. 
        /// Our usage of a circular reference property was causing StackOverflow exceptions to be thrown. 
        /// StackOverflow exceptions cannot be caught. 
        /// This test intends to verify we do not reintroduce the problem.
        /// 
        /// When reintroduced, the test will likely fail with:
        /// "Hosted Web Core log failed validation: file ended early"
        /// 
        /// AND
        /// 
        /// ====== RemoteApplication: standard error log =====
        /// Process is terminated due to StackOverflowException.
        /// ====== RemoteApplication: end of standard error log  =====
        /// 
        /// 
        /// We do not currently work fully with ServiceStack apps. 
        /// Transactions will show up as /WebTransaction/ASP/RestHandler
        /// </summary>
        [Fact]
        public void ShouldNotStackOverflowWithSerializingLogger()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            Assertions.MetricsExist(_generalMetrics, metrics);

            //Basic pipepline metrics are captured, just not appropriate transaction naming or actions
            Assertions.MetricsExist(_pipelineMetrics, metrics);
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _pipelineMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"DotNet/AuthenticateRequest", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AuthorizeRequest", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ResolveRequestCache", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/MapRequestHandler", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/AcquireRequestState", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ExecuteRequestHandler", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/ReleaseRequestState", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/UpdateRequestCache", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/EndRequest", callCount = 1 }
        };

    }
}

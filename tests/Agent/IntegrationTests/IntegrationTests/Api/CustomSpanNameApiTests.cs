// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.Api
{
    [NetFrameworkTest]
    public class CustomSpanNameApiTestsFWLatest : CustomSpanNameApiTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public CustomSpanNameApiTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class CustomSpanNameApiTestsCoreLatest : CustomSpanNameApiTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public CustomSpanNameApiTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public abstract class CustomSpanNameApiTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly TFixture _fixture;

        public CustomSpanNameApiTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"AttributeInstrumentation TransactionWithCustomSpanName CustomSpanName");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void SupportabilityMetricExists()
        {
            var expectedMetric = new Assertions.ExpectedMetric { metricName = $"Supportability/ApiInvocation/SpanSetName", callCount = 1 };
            Assertions.MetricExists(expectedMetric, _fixture.AgentLog.GetMetrics());
        }

        [Fact]
        public void MethodMetricsHaveCustomSpanName()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"DotNet/CustomSpanName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/CustomSpanName", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation/TransactionWithCustomSpanName", callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }

        [Fact]
        public void TransactionTraceContainsSegmentWithCustomSpanName()
        {
            var transactionTrace = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionTrace);

            transactionTrace.TraceData.ContainsSegment("CustomSpanName");
        }

        [Fact]
        public void SpanEventDataHasCustomSpanName()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents();
            Assert.Contains(spanEvents, x => (string)x.IntrinsicAttributes["name"] == "CustomSpanName");
        }
    }
}

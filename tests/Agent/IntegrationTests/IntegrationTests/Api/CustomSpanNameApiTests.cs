// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.Models;

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
        protected readonly TFixture Fixture;

        public CustomSpanNameApiTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand($"AttributeInstrumentation TransactionWithCustomSpanName CustomSpanName");
            Fixture.AddCommand("RootCommands DelaySeconds 5");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void SupportabilityMetricExists()
        {
            var expectedMetric = new Assertions.ExpectedMetric { metricName = $"Supportability/ApiInvocation/SpanSetName", callCount = 1 };
            Assertions.MetricExists(expectedMetric, Fixture.AgentLog.GetMetrics());
        }

        [Fact]
        public void MethodMetricsHaveCustomSpanName()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"DotNet/CustomSpanName", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/CustomSpanName", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation/TransactionWithCustomSpanName", callCount = 1 }
            };

            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }

        [Fact]
        public void TransactionTraceContainsSegmentWithCustomSpanName()
        {
            var transactionTrace = Fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionTrace);

            transactionTrace.TraceData.ContainsSegment("CustomSpanName");
        }

        [Fact]
        public void SpanEventDataHasCustomSpanName()
        {
            var spanEvents = Fixture.AgentLog.GetSpanEvents();
            Assert.Contains(spanEvents, x => (string)x.IntrinsicAttributes["name"] == "CustomSpanName");
        }
    }
}

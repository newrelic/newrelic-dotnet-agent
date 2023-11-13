// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;
using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.MassTransit
{
    public abstract class MassTransitTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public MassTransitTestsBase(TFixture fixture, ITestOutputHelper output, bool useStartBus) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            if (useStartBus)
            {
                _fixture.AddCommand($"MassTransitExerciser StartBus");
            }
            else
            {
                _fixture.AddCommand("MassTransitExerciser StartHost");
            }
            _fixture.AddCommand("MassTransitExerciser Publish publishedMessageOne");
            _fixture.AddCommand("MassTransitExerciser Publish publishedMessageTwo");
            _fixture.AddCommand("MassTransitExerciser Send sentMessageOne");
            _fixture.AddCommand("MassTransitExerciser Send sentMessageTwo");

            if (useStartBus)
            {
                _fixture.AddCommand($"MassTransitExerciser StopBus");
            }
            else
            {
                _fixture.AddCommand("MassTransitExerciser StopHost");
            }

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {

            var massTransitMetricNameRegexBase = @"MessageBroker\/MassTransit\/Queue\/";
            var queueNameRegex = @"Named\/(.{26})"; // The auto-generated in-memory queue names have 26 chars
            var massTransitProduceMetricNameRegex = massTransitMetricNameRegexBase + @"Produce\/" + queueNameRegex;
            var massTransitConsumeMetricNameRegex = massTransitMetricNameRegexBase + @"Consume\/" + queueNameRegex;

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = massTransitConsumeMetricNameRegex, callCount = 4, IsRegexName = true},
                new Assertions.ExpectedMetric { metricName = massTransitProduceMetricNameRegex, callCount = 4, IsRegexName = true},

                new Assertions.ExpectedMetric { metricName = massTransitConsumeMetricNameRegex, callCount = 4, IsRegexName = true, metricScope = @"OtherTransaction\/Message\/MassTransit\/Queue\/" + queueNameRegex, IsRegexScope = true},
                new Assertions.ExpectedMetric { metricName = massTransitProduceMetricNameRegex, callCount = 2, IsRegexName = true, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Publish"},
                new Assertions.ExpectedMetric { metricName = massTransitProduceMetricNameRegex, callCount = 2, IsRegexName = true, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Send"},
            };

            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Publish");

            Assert.NotNull( transactionEvent );
        }
    }

    // Tests using StartHost (hosted service configuration method)
    [NetFrameworkTest]
    public class MassTransitTests_StartHost_FW462 : MassTransitTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MassTransitTests_StartHost_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }
    [NetFrameworkTest]
    public class MassTransitTests_StartHost_FWLatest : MassTransitTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MassTransitTests_StartHost_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitTests_StartHost_Core60 : MassTransitTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MassTransitTests_StartHost_Core60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitTests_StartHost_CoreLatest : MassTransitTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MassTransitTests_StartHost_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    // Tests using StartBus (bus factory configuration method)
    [NetFrameworkTest]
    public class MassTransitTests_StartBus_FW462 : MassTransitTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MassTransitTests_StartBus_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetFrameworkTest]
    public class MassTransitTests_StartBus_FWLatest : MassTransitTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MassTransitTests_StartBus_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitTests_StartBus_Core60 : MassTransitTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MassTransitTests_StartBus_Core60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitTests_StartBus_CoreLatest : MassTransitTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MassTransitTests_StartBus_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

}

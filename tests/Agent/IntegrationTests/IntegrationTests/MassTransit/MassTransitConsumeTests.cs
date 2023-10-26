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
    public abstract class MassTransitConsumeTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public MassTransitConsumeTestsBase(TFixture fixture, ITestOutputHelper output, bool useStartBus) : base(fixture)
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

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker\/MassTransit\/Queue\/Consume\/Named\/(.+)", callCount = 4, IsRegexName = true},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker\/MassTransit\/Queue\/Produce\/Named\/(.+)", callCount = 4, IsRegexName = true},

                new Assertions.ExpectedMetric { metricName = @"MessageBroker\/MassTransit\/Queue\/Consume\/Named\/(.+)", callCount = 4, IsRegexName = true, metricScope = @"OtherTransaction\/Message\/MassTransit\/Queue\/Named\/(.+)", IsRegexScope = true},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker\/MassTransit\/Queue\/Produce\/Named\/(.+)", callCount = 2, IsRegexName = true, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Publish"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker\/MassTransit\/Queue\/Produce\/Named\/(.+)", callCount = 2, IsRegexName = true, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Send"},
            };

            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransitExerciser/Publish");

            Assert.NotNull( transactionEvent );
        }
    }
    [NetFrameworkTest]
    public class MassTransitConsumeTestsFW462 : MassTransitConsumeTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MassTransitConsumeTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetFrameworkTest]
    public class MassTransitConsumeTestsFWLatest : MassTransitConsumeTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MassTransitConsumeTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitConsumeTestsCore60 : MassTransitConsumeTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MassTransitConsumeTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    [NetCoreTest]
    public class MassTransitConsumeTestsCoreLatest : MassTransitConsumeTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MassTransitConsumeTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

}

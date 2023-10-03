// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.NServiceBus
{
    public abstract class NsbPublishTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NsbPublishTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithoutHandlers");
            _fixture.AddCommand("NServiceBusDriver PublishEventInTransaction");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(30));
                    _fixture.SendCommand("NServiceBusDriver StopNServiceBus");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Event", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Event", callCount = 1, metricScope = "OtherTransaction/Custom/NsbTests.NServiceBusDriver/PublishEventInTransaction"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Event"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/NsbTests.NServiceBusDriver/PublishEventInTransaction");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/NsbTests.NServiceBusDriver/PublishEventInTransaction");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }

    [NetFrameworkTest]
    public class NsbPublishTestsFW471 : NsbPublishTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NsbPublishTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbPublishTestsFW48 : NsbPublishTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NsbPublishTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbPublishTestsFWLatest : NsbPublishTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NsbPublishTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbPublishTestsCoreOldest : NsbPublishTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NsbPublishTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbPublishTestsCoreLatest : NsbPublishTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NsbPublishTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

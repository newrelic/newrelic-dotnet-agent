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
    public abstract class NsbAsyncEventHandlerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NsbAsyncEventHandlerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithAsyncEventHandler");
            _fixture.AddCommand("NServiceBusDriver PublishEvent");

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
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Event"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Event",
                                                metricScope = @"OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Event"},
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Event"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Event"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Event");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Event");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );

            Assert.DoesNotContain("Transaction was garbage collected without ever ending", _fixture.AgentLog.GetFullLogAsString());
        }
    }

    [NetFrameworkTest]
    public class NsbAsyncEventHandlerTestsFW471 : NsbAsyncEventHandlerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NsbAsyncEventHandlerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbAsyncEventHandlerTestsFW48 : NsbAsyncEventHandlerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NsbAsyncEventHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbAsyncEventHandlerTestsFWLatest : NsbAsyncEventHandlerTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NsbAsyncEventHandlerTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbAsyncEventHandlerTestsCoreOldest : NsbAsyncEventHandlerTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NsbAsyncEventHandlerTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbAsyncEventHandlerTestsCoreLatest : NsbAsyncEventHandlerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NsbAsyncEventHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

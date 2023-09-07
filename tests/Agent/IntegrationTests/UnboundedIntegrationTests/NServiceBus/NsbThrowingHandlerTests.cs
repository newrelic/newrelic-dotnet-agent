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
    public abstract class NsbThrowingHandlerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NsbThrowingHandlerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithThrowingCommandHandler");
            _fixture.AddCommand("NServiceBusDriver SendCommand");

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
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Command"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Command",
                                                metricScope = @"OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Command"},
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Command"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Consume/Named/NsbTests.Command"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Command");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Command");
            var errorTrace =
                _fixture.AgentLog.TryGetErrorTrace(
                    "OtherTransaction/Message/NServiceBus/Queue/Named/NsbTests.Command");

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent),
                () => Assert.NotNull(errorTrace)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );

            Assert.DoesNotContain("Transaction was garbage collected without ever ending", _fixture.AgentLog.GetFullLogAsString());
        }
    }


    /// <summary>
    /// This harness targets to NServiceBus 6.5.10
    /// </summary>
    [NetFrameworkTest]
    public class NsbThrowingHandlerTestsFW471 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NsbThrowingHandlerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    /// <summary>
    /// This harness, and all the others, target to NServiceBus 7.5
    /// </summary>
    [NetFrameworkTest]
    public class NsbThrowingHandlerTestsFW48 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NsbThrowingHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbThrowingHandlerTestsFWLatest : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NsbThrowingHandlerTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCoreOldest : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NsbThrowingHandlerTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCoreLatest : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NsbThrowingHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

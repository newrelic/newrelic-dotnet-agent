﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
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
            _fixture.AddCommand("RootCommands DelaySeconds 5");
            _fixture.AddCommand("NServiceBusDriver StopNServiceBus");

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

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore21 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NsbThrowingHandlerTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore22 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NsbThrowingHandlerTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore31 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NsbThrowingHandlerTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore50 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NsbThrowingHandlerTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore60 : NsbThrowingHandlerTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NsbThrowingHandlerTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
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

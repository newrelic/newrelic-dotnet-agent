// Copyright 2020 New Relic, Inc. All rights reserved.
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
    public abstract class NsbThrowingHandlerTests<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NsbThrowingHandlerTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
        public void ThrowingCommandHandlerInstrumentationWorks()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Temp"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Temp",
                                                metricScope = @"OtherTransaction/Message/NServiceBus/Queue/Temp"},
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Temp"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Consume/Temp"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Message/NServiceBus/Queue/Temp");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Message/NServiceBus/Queue/Temp");

            // TODO: Currently we aren't getting any error trace data for this instrumentation, I do see the exception is captured
            // in the span_event_data though... leaving this test broken as we likely want the behavior to be consistent between NSB5 and 7
            var errorTrace =
                _fixture.AgentLog.TryGetErrorTrace(
                    "MessageBroker/NServiceBus/Queue/Consume/Temp");

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
        }
    }


    /// <summary>
    /// This harness targets to NServiceBus 6.5.10
    /// </summary>
    [NetFrameworkTest]
    public class NsbThrowingHandlerTestsFW471 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureFW471>
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
    public class NsbThrowingHandlerTestsFW48 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureFW48>
    {
        public NsbThrowingHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore21 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCore21>
    {
        public NsbThrowingHandlerTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore22 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCore22>
    {
        public NsbThrowingHandlerTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore31 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCore31>
    {
        public NsbThrowingHandlerTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore50 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCore50>
    {
        public NsbThrowingHandlerTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCore60 : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCore60>
    {
        public NsbThrowingHandlerTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbThrowingHandlerTestsCoreLatest : NsbThrowingHandlerTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NsbThrowingHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

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
    public abstract class NServiceBusThrowingCommandHandlerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusThrowingCommandHandlerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(10));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithThrowingCommandHandler");

            // Execute tests
            _fixture.AddCommand("NServiceBusDriver SendCommand");

            // Wait...
            _fixture.AddCommand("RootCommands DelaySeconds 5");

            // Shut down
            _fixture.AddCommand("NServiceBusDriver StopNServiceBus");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
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

    // This test is commented out because the .NET Framework 4.6.2 tests use version 5 of NServiceBus.
    // The tests in this file are meant for version 6/7+ of NServiceBus.

    //[NetFrameworkTest]
    //public class NServiceBusTestsFW462 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureFW462>
    //{
    //    public NServiceBusTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
    //        : base(fixture, output)
    //    {
    //    }
    //}

    [NetFrameworkTest]
    public class NServiceBusThrowingCommandHandlerTestsFW471 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusThrowingCommandHandlerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusThrowingCommandHandlerTestsFW48 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusThrowingCommandHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCore21 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusThrowingCommandHandlerTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCore22 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusThrowingCommandHandlerTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCore31 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusThrowingCommandHandlerTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCore50 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusThrowingCommandHandlerTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCore60 : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusThrowingCommandHandlerTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusThrowingCommandHandlerTestsCoreLatest : NServiceBusThrowingCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusThrowingCommandHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

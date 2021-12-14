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
    public abstract class NServiceBusAsyncCommandHandlerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusAsyncCommandHandlerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithAsyncCommandHandler");

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
        public void AsyncCommandHandlerInstrumentationWorks()
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
    public class NServiceBusAsyncCommandHandlerTestsFW471 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusAsyncCommandHandlerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusAsyncCommandHandlerTestsFW48 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusAsyncCommandHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCore21 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusAsyncCommandHandlerTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCore22 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusAsyncCommandHandlerTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCore31 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusAsyncCommandHandlerTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCore50 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusAsyncCommandHandlerTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCore60 : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusAsyncCommandHandlerTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusAsyncCommandHandlerTestsCoreLatest : NServiceBusAsyncCommandHandlerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusAsyncCommandHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

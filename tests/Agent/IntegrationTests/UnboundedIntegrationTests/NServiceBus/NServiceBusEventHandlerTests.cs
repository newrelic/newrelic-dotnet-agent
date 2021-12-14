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
    public abstract class NServiceBusEventHandlerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusEventHandlerTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithEventHandler");

            // Execute tests
            _fixture.AddCommand("NServiceBusDriver PublishEvent");

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
        public void EventHandlerInstrumentationWorks()
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
    public class NServiceBusEventHandlerTestsFW471 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusEventHandlerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusEventHandlerTestsFW48 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusEventHandlerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCore21 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusEventHandlerTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCore22 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusEventHandlerTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCore31 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusEventHandlerTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCore50 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusEventHandlerTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCore60 : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusEventHandlerTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusEventHandlerTestsCoreLatest : NServiceBusEventHandlerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusEventHandlerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

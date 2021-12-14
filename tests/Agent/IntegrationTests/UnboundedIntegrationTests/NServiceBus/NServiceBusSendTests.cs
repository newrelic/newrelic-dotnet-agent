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
    public abstract class NServiceBusSendTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusSendTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithoutHandlers");

            // Execute tests
            _fixture.AddCommand("NServiceBusDriver SendCommandInTransaction");

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
        public void SendInstrumentationWorks()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Command", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Command", callCount = 1, metricScope = "OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/SendCommandInTransaction"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Command"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/SendCommandInTransaction");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/SendCommandInTransaction");

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
    public class NServiceBusSendTestsFW471 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusSendTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusSendTestsFW48 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusSendTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCore21 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusSendTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCore22 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusSendTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCore31 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusSendTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCore50 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusSendTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCore60 : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusSendTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusSendTestsCoreLatest : NServiceBusSendTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusSendTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

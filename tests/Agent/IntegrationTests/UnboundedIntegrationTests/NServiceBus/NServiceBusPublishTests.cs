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
    public abstract class NServiceBusPublishTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusPublishTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithoutHandlers");

            // Execute tests
            _fixture.AddCommand("NServiceBusDriver PublishEventInTransaction");

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
        public void PublishInstrumentationWorks()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Event", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Event", callCount = 1, metricScope = "OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/PublishEventInTransaction"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Produce/Named/NServiceBusTests.Event"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/PublishEventInTransaction");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/NServiceBusTests.NServiceBusDriver/PublishEventInTransaction");

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
    public class NServiceBusPublishTestsFW471 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusPublishTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusPublishTestsFW48 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusPublishTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCore21 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusPublishTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCore22 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusPublishTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCore31 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusPublishTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCore50 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusPublishTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCore60 : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusPublishTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusPublishTestsCoreLatest : NServiceBusPublishTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusPublishTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

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
    public abstract class NServiceBusTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBusTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            // Startup
            _fixture.AddCommand("NServiceBusDriver StartNServiceBus");

            // Execute tests
            _fixture.AddCommand("NServiceBusDriver PublishEvent");
            _fixture.AddCommand("NServiceBusDriver SendCommand");

            _fixture.AddCommand("NServiceBusDriver PublishMessage");
            _fixture.AddCommand("NServiceBusDriver SendMessage");

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
        public void AllTheInstrumentationWorks()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.NServiceBusExerciser/Nope";
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/NServiceBus/DontThinkSo", metricScope = expectedTransactionName, callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/NServiceBus/DefinitelyNot", metricScope = expectedTransactionName, callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            // TODO-JODO: What is this doing?
            //var traceId = spanEvents?.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals(expectedTransactionName)).FirstOrDefault().IntrinsicAttributes["traceId"];
            //var operationDatastoreSpans = spanEvents?.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/operation/NServiceBus"));

            //Assertions.MetricsExist(expectedMetrics, metrics);
            //Assert.Equal(6, operationDatastoreSpans.Count());
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
    public class NServiceBusTestsFW471 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NServiceBusTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NServiceBusTestsFW48 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NServiceBusTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCore21 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public NServiceBusTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCore22 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public NServiceBusTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCore31 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NServiceBusTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCore50 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NServiceBusTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCore60 : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NServiceBusTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NServiceBusTestsCoreLatest : NServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NServiceBusTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

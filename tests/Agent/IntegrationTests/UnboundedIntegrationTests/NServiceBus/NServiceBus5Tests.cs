// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.NServiceBus
{
    public abstract class NServiceBus5TestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBus5TestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand("NServiceBusReceiverHost Start");
            _fixture.AddCommand("NServiceBusService Start");
            _fixture.AddCommand("NServiceBusService Send");
            _fixture.AddCommand("NServiceBusService Stop");
            _fixture.AddCommand("NServiceBusReceiverHost Stop");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
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
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.NServiceBusService/Send"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Produce/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.NServiceBusService/Send");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.NServiceBusService/Send");

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

    [NetFrameworkTest]
    public class NServiceBus5OnFW462Tests : NServiceBus5TestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NServiceBus5OnFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}

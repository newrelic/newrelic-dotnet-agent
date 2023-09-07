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
using Assert = Xunit.Assert;

namespace NewRelic.Agent.UnboundedIntegrationTests.NServiceBus5
{
    public abstract class NServiceBus5ReceiveTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NServiceBus5ReceiveTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand("NServiceBusSetup Setup");
            _fixture.AddCommand("NServiceBusReceiverHost Start");
            _fixture.AddCommand("NServiceBusService Start");
            _fixture.AddCommand("NServiceBusService SendValid");
            _fixture.AddCommand("NServiceBusService SendInvalid");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    // There will be two transactions created by the reciever, one for the valid message and one for the invalid message
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(30), 2);
                    _fixture.SendCommand("NServiceBusService Stop");
                    _fixture.SendCommand("NServiceBusReceiverHost Stop");
                }
            );

            _fixture.Initialize();
            // _fixture.AgentLog.WaitForLogLine(AgentLogFile.ErrorTraceDataLogLineRegex, TimeSpan.FromMinutes(2));
        }


        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2"},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2",
                                                metricScope = @"OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2"},
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2"},
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Consume/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2");
            var errorTrace =
                _fixture.AgentLog.TryGetErrorTrace(
                    "OtherTransaction/Message/NServiceBus/Queue/Named/MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5.SampleNServiceBusMessage2");

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

    [NetFrameworkTest]
    public class NServiceBus5ReceiveOnFW462Tests : NServiceBus5ReceiveTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NServiceBus5ReceiveOnFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}

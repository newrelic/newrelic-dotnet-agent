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

namespace NewRelic.Agent.UnboundedIntegrationTests.NServiceBus
{
    public abstract class NsbSendTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected NsbSendTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));

            _fixture.AddCommand("NServiceBusDriver StartNServiceBusWithoutHandlers");
            _fixture.AddCommand("NServiceBusDriver SendCommandInTransaction");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
                    configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(30));
                    _fixture.SendCommand("NServiceBusDriver StopNServiceBus");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Command", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Command", callCount = 1, metricScope = "OtherTransaction/Custom/NsbTests.NServiceBusDriver/SendCommandInTransaction"}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                @"MessageBroker/NServiceBus/Queue/Produce/Named/NsbTests.Command"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/NsbTests.NServiceBusDriver/SendCommandInTransaction");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/NsbTests.NServiceBusDriver/SendCommandInTransaction");

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
    public class NsbSendTestsFW471 : NsbSendTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NsbSendTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbSendTestsFW48 : NsbSendTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public NsbSendTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class NsbSendTestsFWLatest : NsbSendTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NsbSendTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbSendTestsCoreOldest : NsbSendTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NsbSendTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NsbSendTestsCoreLatest : NsbSendTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NsbSendTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

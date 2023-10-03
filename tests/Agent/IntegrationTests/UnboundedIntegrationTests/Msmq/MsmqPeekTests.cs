// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Msmq
{
    public abstract class MsmqPeekTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly int _queueNum = MsmqHelper.GetNextQueueNum();

        public MsmqPeekTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"MSMQExerciser Create {_queueNum}");
            _fixture.AddCommand($"MSMQExerciser Send {_queueNum} true");
            _fixture.AddCommand($"MSMQExerciser Peek {_queueNum}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            string transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MSMQExerciser/Peek";
            string metricName = @"MessageBroker/Msmq/Queue/Peek/Named/private$\nrtestqueue" + _queueNum.ToString();
            string segmentName = @"MessageBroker/Msmq/Queue/Peek/Named/private$\nrtestqueue" + _queueNum.ToString();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = metricName, callCount = 1},
                new Assertions.ExpectedMetric { metricName = metricName, callCount = 1, metricScope = transactionName}
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                segmentName
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(transactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(transactionName);

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
    public class MsmqPeekTestsFW462 : MsmqPeekTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsmqPeekTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqPeekTestsFW471 : MsmqPeekTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MsmqPeekTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqPeekTestsFW48 : MsmqPeekTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MsmqPeekTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqPeekTestsFWLatest : MsmqPeekTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsmqPeekTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

}

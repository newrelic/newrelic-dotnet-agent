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
    /// <summary>
    /// Integration Test for MSMQ Consume message.
    /// </summary>
    /// <remarks>
    /// Although this test also uses the MSMQ Send endpoint, it is important to keep the Send and Consume tests as separate fixtures
    /// in order to separately test the existence of a Consume segment in the transaction trace. Only a single TT is being saved per test.
    /// </remarks>
    public abstract class MsmqConsumeTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly int _queueNum = MsmqHelper.GetNextQueueNum();

        public MsmqConsumeTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"MSMQExerciser Create {_queueNum}");
            _fixture.AddCommand($"MSMQExerciser Send {_queueNum} true");
            _fixture.AddCommand($"MSMQExerciser Receive {_queueNum}");

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
            string transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MSMQExerciser/Receive";
            string metricName = @"MessageBroker/Msmq/Queue/Consume/Named/private$\nrtestqueue" + _queueNum.ToString();
            string segmentName = @"MessageBroker/Msmq/Queue/Consume/Named/private$\nrtestqueue" + _queueNum.ToString();

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
    public class MsmqConsumeTestsFW462 : MsmqConsumeTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsmqConsumeTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqConsumeTestsFW471 : MsmqConsumeTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MsmqConsumeTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqConsumeTestsFW48 : MsmqConsumeTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MsmqConsumeTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MsmqConsumeTestsFWLatest : MsmqConsumeTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsmqConsumeTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }
}

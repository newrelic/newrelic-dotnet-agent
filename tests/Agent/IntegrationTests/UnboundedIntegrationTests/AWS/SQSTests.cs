// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.AWS.SQS
{
    public abstract class SQSTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        private readonly string _testQueueName = $"TestQueue-{Guid.NewGuid()}";
        private readonly string _metricScopeBase = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AWS.AwsSdkExerciser";


        protected SQSTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            //_fixture.AddCommand("RootCommands LaunchDebugger");
            _fixture.AddCommand($"AwsSdkExerciser SQS_Initialize {_testQueueName}");
            _fixture.AddCommand("AwsSdkExerciser SQS_SendMessage TestMessage");
            _fixture.AddCommand("AwsSdkExerciser SQS_Teardown");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}", callCount = 1, metricScope = $"{_metricScopeBase}/SQS_SendMessage"},
            };

            var sendMessageTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"{_metricScopeBase}/SQS_SendMessage");

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_metricScopeBase}/SQS_SendMessage");
            var expectedTransactionTraceSegments = new List<string>
            {
                $"MessageBroker/SQS/Queue/Produce/Named/{_testQueueName}"
            };

            Assertions.MetricsExist(expectedMetrics, metrics);
            NrAssert.Multiple(
                () => Assert.True(sendMessageTransactionEvent != null, "sendMessageTransactionEvent should not be null"),
                () => Assert.True(transactionSample != null, "transactionSample should not be null"),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }

    [NetCoreTest]
    public class SQSTestsCoreLatest : SQSTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SQSTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SqsTestsCoreOldest : SQSTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SqsTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus
{
    public abstract class AzureServiceBusProcessorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly string _queueOrTopicName;
        private readonly string _destinationType;

        private readonly string _consumeMetricNameBase;
        private readonly string _processMetricNameBase;
        private readonly string _settleMetricNameBase;
        private readonly string _transactionNameBase;

        protected AzureServiceBusProcessorTestsBase(TFixture fixture, string destinationType, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _queueOrTopicName = $"test-queue-{Guid.NewGuid()}";
            _destinationType = destinationType;

             _consumeMetricNameBase = $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named";
             _processMetricNameBase = $"MessageBroker/ServiceBus/{_destinationType}/Process/Named";
             _settleMetricNameBase = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named";
             _transactionNameBase = $"OtherTransaction/Message/ServiceBus/{_destinationType}/Named";

            _fixture.AddCommand($"AzureServiceBusExerciser Initialize{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser ExerciseServiceBusProcessorFor{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser Delete{_destinationType} {_queueOrTopicName}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                        .SetLogLevel("finest")
                        .EnableDistributedTrace()
                        .ForceTransactionTraces()
                        .ConfigureFasterMetricsHarvestCycle(20)
                        .ConfigureFasterSpanEventsHarvestCycle(20)
                        .ConfigureFasterTransactionTracesHarvestCycle(25)
                        ;
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex,
                        TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            // 2 messages, 1 consume segment, 1 process segment, 1 settle segment per message
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = $"{_consumeMetricNameBase}/{_queueOrTopicName}", callCount = 2 },
                new()
                {
                    metricName = $"{_consumeMetricNameBase}/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}"
                },
                new()
                {
                    metricName = $"{_processMetricNameBase}/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}"
                },
                new()
                {
                    metricName = $"{_settleMetricNameBase}/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}"
                },
            };

            var expectedTransactionEvent =
                _fixture.AgentLog.TryGetTransactionEvent($"{_transactionNameBase}/{_queueOrTopicName}");

            var expectedTransactionTraceSegments = new List<string>
            {
                $"{_consumeMetricNameBase}/{_queueOrTopicName}",
                $"{_processMetricNameBase}/{_queueOrTopicName}",
                "DotNet/ServiceBusProcessor/OnProcessMessageAsync",
                $"{_settleMetricNameBase}/{_queueOrTopicName}",
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_transactionNameBase}/{_queueOrTopicName}");

            var queueConsumeSpanEvent = _fixture.AgentLog.TryGetSpanEvent($"{_consumeMetricNameBase}/{_queueOrTopicName}");
            var queueProcessSpanEvent = _fixture.AgentLog.TryGetSpanEvent($"{_processMetricNameBase}/{_queueOrTopicName}");

            var expectedConsumeAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };

            var expectedIntrinsicAttributes = new List<string> { "span.kind", };

            Assertions.MetricsExist(expectedMetrics, metrics);

            NrAssert.Multiple(
                () => Assert.NotNull(expectedTransactionEvent),
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(queueConsumeSpanEvent),
                () => Assert.NotNull(queueProcessSpanEvent),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

                () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvent),
                () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvent),
                () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvent),
                () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvent)
            );
        }
    }

    #region Queue Tests

    public class
        AzureServiceBusProcessorQueueTestsFWLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusProcessorQueueTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorQueueTestsFW462 : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusProcessorQueueTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
            base(fixture, "Queue", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorQueueTestsCoreOldest : AzureServiceBusProcessorTestsBase<
        ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusProcessorQueueTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorQueueTestsCoreLatest : AzureServiceBusProcessorTestsBase<
        ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusProcessorQueueTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output) : base(fixture, "Queue", output)
        {
        }
    }

    #endregion Queue Tests

    #region Topic Tests

    public class
        AzureServiceBusProcessorTopicTestsFWLatest : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusProcessorTopicTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorTopicTestsFW462 : AzureServiceBusProcessorTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusProcessorTopicTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) :
            base(fixture, "Topic", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorTopicTestsCoreOldest : AzureServiceBusProcessorTestsBase<
        ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusProcessorTopicTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    public class
        AzureServiceBusProcessorTopicTestsCoreLatest : AzureServiceBusProcessorTestsBase<
        ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusProcessorTopicTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture,
            ITestOutputHelper output) : base(fixture, "Topic", output)
        {
        }
    }

    #endregion Topic Tests

}

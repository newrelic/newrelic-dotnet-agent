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
    public abstract class AzureServiceBusTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly string _queueOrTopicName;
        private readonly string _destinationType;

        protected AzureServiceBusTestsBase(TFixture fixture, string destinationType, ITestOutputHelper output) :
            base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _queueOrTopicName = $"test-queue-{Guid.NewGuid()}";
            _destinationType = destinationType;

            _fixture.AddCommand($"AzureServiceBusExerciser Initialize{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser ScheduleAndCancelAMessage {_queueOrTopicName}");
            if (_destinationType == "Queue")
            {
                _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAndDeadLetterAMessageForQueue {_queueOrTopicName}");
            }
            
            _fixture.AddCommand($"AzureServiceBusExerciser ReceiveAndAbandonAMessageFor{_destinationType} {_queueOrTopicName}");
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
                }
            );

            _fixture.Initialize();
        }

        private readonly string _metricScopeBase =
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser";

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}", callCount = _destinationType == "Queue" ? 4 : 3
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}",
                    callCount = 1,
                    metricScope =
                        $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}",
                    callCount = 1,
                    metricScope = $"{_metricScopeBase}/ScheduleAndCancelAMessage"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}",
                    callCount = 1,
                    metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}",
                    callCount = _destinationType == "Queue" ? 6 : 5
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}",
                    callCount = 3,
                    metricScope =
                        $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Peek/Named/{_queueOrTopicName}",
                    callCount = 1
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Peek/Named/{_queueOrTopicName}",
                    callCount = 1,
                    metricScope =
                        $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named/{_queueOrTopicName}",
                    callCount = _destinationType == "Queue" ? 5 : 4
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope =
                        $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}"
                },
                new()
                {
                    metricName = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_metricScopeBase}/ReceiveAndAbandonAMessageFor{_destinationType}"
                },
            };

            if (_destinationType == "Queue")
            {
                expectedMetrics.Add(
                    new()
                    {
                        metricName = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named/{_queueOrTopicName}",
                        callCount = 1,
                        metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessageFor{_destinationType}"
                    });
                expectedMetrics.Add(
                    new()
                    {
                        metricName = $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}",
                        callCount = 1,
                        metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessageFor{_destinationType}"
                    });
                expectedMetrics.Add(
                    new()
                    {
                        metricName = $"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}",
                        callCount = 1,
                        metricScope = $"{_metricScopeBase}/ReceiveAndDeadLetterAMessageFor{_destinationType}"
                    });
                expectedMetrics.Add(
                    new()
                    {
                        metricName = $"MessageBroker/ServiceBus/{_destinationType}/Cancel/Named/{_queueOrTopicName}",
                        callCount = 1
                    });
                expectedMetrics.Add(
                    new()
                    {
                        metricName = $"MessageBroker/ServiceBus/{_destinationType}/Cancel/Named/{_queueOrTopicName}",
                        callCount = 1,
                        metricScope = $"{_metricScopeBase}/ScheduleAndCancelAMessage"
                    });
            }

            var exerciseMultipleReceiveOperationsOnAMessageTransactionEvent =
                _fixture.AgentLog.TryGetTransactionEvent(
                    $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}");

            var expectedTransactionTraceSegments = new List<string>
            {
                $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}"
            };

            var transactionSample =
                _fixture.AgentLog.TryGetTransactionSample(
                    $"{_metricScopeBase}/ExerciseMultipleReceiveOperationsOnAMessageFor{_destinationType}");

            var queueProduceSpanEvents =
                _fixture.AgentLog.TryGetSpanEvent($"MessageBroker/ServiceBus/Queue/Produce/Named/{_queueOrTopicName}");
            var queueConsumeSpanEvents =
                _fixture.AgentLog.TryGetSpanEvent(
                    $"MessageBroker/ServiceBus/{_destinationType}/Consume/Named/{_queueOrTopicName}");
            var queuePeekSpanEvents =
                _fixture.AgentLog.TryGetSpanEvent(
                    $"MessageBroker/ServiceBus/{_destinationType}/Peek/Named/{_queueOrTopicName}");
            var queueSettleSpanEvents =
                _fixture.AgentLog.TryGetSpanEvent(
                    $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named/{_queueOrTopicName}");
            var queueCancelSpanEvents =
                _fixture.AgentLog.TryGetSpanEvent(
                    $"MessageBroker/ServiceBus/{_destinationType}/Cancel/Named/{_queueOrTopicName}");

            var expectedProduceAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };

            var expectedConsumeAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };


            var expectedPeekAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };

            var expectedSettleAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };

            var expectedCancelAgentAttributes = new List<string> { "server.address", "messaging.destination.name", };

            var expectedIntrinsicAttributes = new List<string> { "span.kind", };

            Assertions.MetricsExist(expectedMetrics, metrics);

            NrAssert.Multiple(
                () => Assert.True(exerciseMultipleReceiveOperationsOnAMessageTransactionEvent != null,
                    "ExerciseMultipleReceiveOperationsOnAMessageTransactionEvent should not be null"),
                () => Assert.True(transactionSample != null, "transactionSample should not be null"),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

                () => Assertions.SpanEventHasAttributes(expectedProduceAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueProduceSpanEvents),
                () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueProduceSpanEvents),

                () => Assertions.SpanEventHasAttributes(expectedConsumeAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueConsumeSpanEvents),
                () => Assertions.SpanEventHasAttributes(expectedIntrinsicAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Intrinsic, queueConsumeSpanEvents),

                () => Assertions.SpanEventHasAttributes(expectedPeekAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queuePeekSpanEvents),

                () => Assertions.SpanEventHasAttributes(expectedSettleAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueSettleSpanEvents)
            );

            if (_destinationType == "Queue")
            {
                Assertions.SpanEventHasAttributes(expectedCancelAgentAttributes,
                    Tests.TestSerializationHelpers.Models.SpanEventAttributeType.Agent, queueCancelSpanEvents);
            }
        }
    }

    #region Queue Tests

    public class AzureServiceBusQueueTestsFWLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusQueueTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) :
            base(fixture, "Queue", output)
        {
        }
    }

    public class AzureServiceBusQueueTestsFW462 : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusQueueTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(
            fixture, "Queue", output)
        {
        }
    }

    public class AzureServiceBusQueueTestsCoreOldest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusQueueTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) :
            base(fixture, "Queue", output)
        {
        }
    }

    public class AzureServiceBusQueueTestsCoreLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusQueueTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) :
            base(fixture, "Queue", output)
        {
        }
    }

    #endregion Queue Tests

    #region Topic Tests

    public class AzureServiceBusTopicTestsFWLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureServiceBusTopicTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) :
            base(fixture, "Topic", output)
        {
        }
    }

    public class AzureServiceBusTopicTestsFW462 : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public AzureServiceBusTopicTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(
            fixture, "Topic", output)
        {
        }
    }

    public class AzureServiceBusTopicTestsCoreOldest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureServiceBusTopicTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) :
            base(fixture, "Topic", output)
        {
        }
    }

    public class AzureServiceBusTopicTestsCoreLatest : AzureServiceBusTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureServiceBusTopicTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) :
            base(fixture, "Topic", output)
        {
        }
    }

    #endregion Topic Tests

}

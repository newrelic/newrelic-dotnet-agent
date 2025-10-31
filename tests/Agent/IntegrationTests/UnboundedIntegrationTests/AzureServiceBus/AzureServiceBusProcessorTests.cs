// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus
{
    public abstract class AzureServiceBusProcessorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly string _queueOrTopicName;
        private readonly string _destinationType;

        private readonly string _processMetricNameBase;
        private readonly string _settleMetricNameBase;
        private readonly string _transactionNameBase;
        private readonly string _topicScopeSuffix;
        private readonly string _onProcessMessageMethodSegmentMetricName;

        protected AzureServiceBusProcessorTestsBase(TFixture fixture, string destinationType, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _queueOrTopicName = $"test-queue-{Guid.NewGuid()}";
            _destinationType = destinationType;

            _topicScopeSuffix = null;
            if (_destinationType == "Topic")
            {
                _topicScopeSuffix = "/Subscriptions/test";
            }

            _processMetricNameBase = $"MessageBroker/ServiceBus/{_destinationType}/Process/Named";
            _onProcessMessageMethodSegmentMetricName = "DotNet/ServiceBusProcessor/OnProcessMessageAsync";
            _settleMetricNameBase = $"MessageBroker/ServiceBus/{_destinationType}/Settle/Named";
            _transactionNameBase = $"OtherTransaction/Message/ServiceBus/{_destinationType}/Named";

            _fixture.AddCommand($"AzureServiceBusExerciser Initialize{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser ExerciseServiceBusProcessor_SendMessagesFor{_destinationType} {_queueOrTopicName}");
            _fixture.AddCommand($"AzureServiceBusExerciser ExerciseServiceBusProcessor_ReceiveMessagesFor{_destinationType} {_queueOrTopicName}");
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
                        .ConfigureFasterTransactionTracesHarvestCycle(25);

                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex,
                        TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // Local helper to trim priority to max 7 chars
            string TrimPriority(object val)
            {
                var s = val?.ToString();
                return string.IsNullOrEmpty(s) ? s : (s.Length > 7 ? s.Substring(0, 7) : s);
            }

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            // 2 messages, 1 process segment, 1 method segment, 1 settle segment per message
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new()
                {
                    metricName = $"{_processMetricNameBase}/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}{_topicScopeSuffix}"
                },
                new()
                {
                    metricName = _onProcessMessageMethodSegmentMetricName,
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}{_topicScopeSuffix}"
                },
                new()
                {
                    metricName = $"{_settleMetricNameBase}/{_queueOrTopicName}",
                    callCount = 2,
                    metricScope = $"{_transactionNameBase}/{_queueOrTopicName}{_topicScopeSuffix}"
                },
                new() { metricName = "Supportability/TraceContext/Accept/Success"},
            };

            // get the send transaction events to retrieve the expected DT attributes
            var expectedSendTransactionEvent =
                _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus.AzureServiceBusExerciser/ExerciseServiceBusProcessor_SendMessagesFor{_destinationType}");

            Assert.NotNull(expectedSendTransactionEvent);

            var expectedTraceId = expectedSendTransactionEvent.IntrinsicAttributes["traceId"].ToString();
            var expectedPriority = TrimPriority(expectedSendTransactionEvent.IntrinsicAttributes["priority"]);
            var expectedSampled = expectedSendTransactionEvent.IntrinsicAttributes["sampled"];

            // there should be two processor transactions (one for each message processed)
            var processorTransactionEvents =
                _fixture.AgentLog.GetTransactionEvents()
                    .Where(te => te.IntrinsicAttributes["name"].ToString() ==
                                          $"{_transactionNameBase}/{_queueOrTopicName}{_topicScopeSuffix}").ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            var expectedTransactionTraceSegments = new List<string>
            {
                $"{_processMetricNameBase}/{_queueOrTopicName}",
                _onProcessMessageMethodSegmentMetricName,
                $"{_settleMetricNameBase}/{_queueOrTopicName}",
            };

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"{_transactionNameBase}/{_queueOrTopicName}{_topicScopeSuffix}");
            var queueProcessSpanEvent = _fixture.AgentLog.TryGetSpanEvent($"{_processMetricNameBase}/{_queueOrTopicName}");


            Assert.Multiple(
                () => Assert.Equal(2, processorTransactionEvents.Count),
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(queueProcessSpanEvent),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.NotEmpty(spanEvents));

            // verify processor transaction events have the expected DT attributes
            Assert.All(processorTransactionEvents, transactionEvent =>
            {
                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("traceId", out var actualTraceId));
                Assert.Equal(expectedTraceId, actualTraceId);

                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("priority", out var actualPriority));
                Assert.Equal(expectedPriority, TrimPriority(actualPriority));

                Assert.True(transactionEvent.IntrinsicAttributes.TryGetValue("sampled", out var actualSampled));
                Assert.Equal(expectedSampled, actualSampled);
            });

            // verify span events have the expected DT attributes
            Assert.All(spanEvents, spanEvent =>
            {
                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("traceId", out var actualTraceId));
                Assert.Equal(expectedTraceId, actualTraceId);

                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("priority", out var actualPriority));
                Assert.Equal(expectedPriority, TrimPriority(actualPriority));

                Assert.True(spanEvent.IntrinsicAttributes.TryGetValue("sampled", out var actualSampled));
                Assert.Equal(expectedSampled, actualSampled);
            });
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

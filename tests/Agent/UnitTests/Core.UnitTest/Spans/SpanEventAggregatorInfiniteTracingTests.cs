// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Segments;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Collections;
using NewRelic.Agent.Core.Time;

namespace NewRelic.Agent.Core.Spans.Tests
{
    [TestFixture]
    internal class SpanEventAggregatorInfiniteTracingTests
    {
        private IConfigurationService _mockConfigService;
        private IConfiguration _currentConfiguration => _mockConfigService?.Configuration;
        private IAgentHealthReporter _mockAgentHealthReporter;
        private IScheduler _mockScheduler;

        [SetUp]
        public void SetUp()
        {
            var defaultConfig = GetDefaultConfiguration();
            _mockConfigService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _mockConfigService.Configuration).Returns(defaultConfig);
            _mockAgentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _mockScheduler = Mock.Create<IScheduler>();
        }

        [TearDown]
        public void TearDown()
        {
            _mockAgentHealthReporter = null;
        }

        private IConfiguration GetDefaultConfiguration()
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.SpanEventsEnabled).Returns(true);
            Mock.Arrange(() => config.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => config.InfiniteTracingTraceCountConsumers).Returns(1);
            Mock.Arrange(() => config.InfiniteTracingQueueSizeSpans).Returns(10);
            Mock.Arrange(() => config.InfiniteTracingTraceObserverHost).Returns("Seven8TNine");
            Mock.Arrange(() => config.InfiniteTracingTraceObserverPort).Returns("443");
            Mock.Arrange(() => config.InfiniteTracingPartitionCountSpans).Returns(62);
            return config;
        }

        private IDataStreamingService<Span, SpanBatch, RecordStatus> GetMockStreamingService(bool enabled, bool available)
        {
            var streamingSvc = Mock.Create<IDataStreamingService<Span, SpanBatch, RecordStatus>>();
            Mock.Arrange(() => streamingSvc.IsServiceEnabled).Returns(enabled);
            Mock.Arrange(() => streamingSvc.IsServiceAvailable).Returns(available);
            Mock.Arrange(() => streamingSvc.IsStreaming).Returns(true);

            return streamingSvc;
        }

        private ISpanEventAggregatorInfiniteTracing CreateAggregator(IDataStreamingService<Span, SpanBatch, RecordStatus> streamingSvc)
        {
            var aggregator = new SpanEventAggregatorInfiniteTracing(streamingSvc, _mockConfigService, _mockAgentHealthReporter, _mockScheduler);
            return aggregator;
        }

        private void FireAgentConnectedEvent()
        {
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        /// <summary>
        /// If the queue size is based on the config setting
        /// </summary>
        [Test]
        public void Queue_SizeIsBasedOnConfig()
        {
            var actualQueue = null as PartitionedBlockingCollection<Span>;
            var streamingSvc = GetMockStreamingService(true, true);
            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) =>
                {
                    actualQueue = c;
                });

            var expectedQueueCapacity = _currentConfiguration.InfiniteTracingQueueSizeSpans;

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            Assert.That(actualQueue.Capacity, Is.EqualTo(expectedQueueCapacity), "Queue Capacity");
        }

        [Test]
        public void DataStreamingService_Calls_Wait_On_Exit()
        {
            var streamingSvc = GetMockStreamingService(true, true);

            Mock.Arrange(() => _currentConfiguration.CollectorSendDataOnExit).Returns(true);

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());

            Mock.Assert(() => streamingSvc.Wait(Arg.IsAny<int>()), Occurs.Once());
        }

        /// <summary>
        /// If the queue size is changed
        /// 1.  The data streaming service should restart with a new queue that has the new size
        /// </summary>
        [Test]
        public void Queue_SizeRespondsToConfigChange()
        {
            var actualQueue = null as PartitionedBlockingCollection<Span>;
            var streamingSvc = GetMockStreamingService(true, true);
            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) =>
                {
                    actualQueue = c;
                });

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            var expectedCapacityInitial = _currentConfiguration.InfiniteTracingQueueSizeSpans;
            var actualCapacityInitial = actualQueue.Capacity;

            var expectedCapacityUpdated = _currentConfiguration.InfiniteTracingQueueSizeSpans * 3;
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(expectedCapacityUpdated);
            FireAgentConnectedEvent();

            var actualCapacityUpdated = actualQueue.Capacity;

            NrAssert.Multiple
            (
                () => Assert.That(actualCapacityInitial, Is.EqualTo(expectedCapacityInitial), "Initial Queue Capacity"),
                () => Assert.That(actualCapacityUpdated, Is.EqualTo(expectedCapacityUpdated), "Updated Queue Capacity")
            );
        }

        /// <summary>
        /// If the queue size is invalid (<=0), 
        /// 1.  The data streaming service should stop streaming events
        /// 2.  Items in the queue should be dropped
        /// 3.  Supportabiilty metric should be recorded
        /// </summary>
        [TestCase(-1, false, "Invalid Config Option")]
        [TestCase(0, false, "Invalid Config Option")]
        [TestCase(1, true, "Queue Capacity Smaller than Requests (5)")]
        [TestCase(2, true, "Queue Capacity Smaller than Requests (5)")]
        [TestCase(5, true, "Queue Capacity >= requests (5)")]
        [TestCase(6, true, "Queue Capacity >= requests (5)")]
        public void Queue_ValidConfigSetting(int configQueueSize, bool expectedIsAvailable, string scenarioName)
        {
            var countShutdown = 0;
            var countStartConsuming = 0;
            var actualQueue = null as PartitionedBlockingCollection<Span>;

            var streamingSvc = GetMockStreamingService(true, true);
            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) =>
                {
                    countStartConsuming++;
                    actualQueue = c;
                });

            Mock.Arrange(() => streamingSvc.Shutdown(Arg.IsAny<bool>())).DoInstead(() =>
             {
                 countShutdown++;
             });


            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(configQueueSize);

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            for (var i = 0; i < 5; i++)
            {
                aggregator.Collect(new SpanAttributeValueCollection());
            }

            NrAssert.Multiple
            (
                () => Assert.That(aggregator.IsServiceAvailable, Is.EqualTo(expectedIsAvailable), "Aggregator is Available"),
                () => Assert.That(countShutdown, Is.EqualTo(1), "Number of times Shutdown was called")
            );

            if (expectedIsAvailable)
            {
                NrAssert.Multiple
                (
                    () => Assert.That(countStartConsuming, Is.EqualTo(1), "StartConsuming SHOULD have been called"),
                    () => Assert.That(actualQueue, Is.Not.Null),
                    () => Assert.That(actualQueue.Capacity, Is.EqualTo(configQueueSize)),
                    () => Assert.That(actualQueue.Count, Is.EqualTo(Math.Min(5, configQueueSize)), "Items in the queue")
                );
            }
            else
            {
                NrAssert.Multiple
                (
                    () => Assert.That(countStartConsuming, Is.EqualTo(0), "StartConsuming should NOT have been called."),
                    () => Assert.That(actualQueue, Is.Null)
                );
            }
        }


        /// <summary>
        /// When the Queue Size goes from bigger to smaller, 
        /// 1.  Queue should resize
        /// 2.  Keep the items that we can
        /// 3.  Drop extra items, but record supportability metric indicating so.
        /// </summary>
        [TestCase(5, 5, 2, 2, 0, "Updated Config with no Size Change")]
        [TestCase(5, 3, 2, 2, 2, "Bigger to Smaller")]      //We should preserve the first items and discard the rest.
        [TestCase(4, 5, 2, 2, 0, "Smaller to Bigger")]      //We should preserve all of the existing items in order
        [TestCase(0, 5, 2, 1, 0, "Invalid to Valid")]       //a new valid config should start the streaming consumer
        [TestCase(5, 0, 2, 1, 5, "Valid to invalid")]      //a new invalid config should prevent restart of the consumer
        public void Queue_SizeConfigChangeScenarios(int expectedInitialCapacity, int expectedUpdatedCapacity, int expectedShutdownCalls, int expectedStartCalls, int expectedDroppedSpans, string _)
        {
            var actualQueue = null as PartitionedBlockingCollection<Span>;
            var actualStartConsumingCalls = 0;
            var actualShudownCalls = 0;
            long actualCountSpansSeen = 0;
            long actualCountSpansDropped = 0;

            //Set up Streaming Service that does not dequeue items, that captures a reference to the collection
            var streamingSvc = GetMockStreamingService(true, true);
            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) =>
                {
                    actualStartConsumingCalls++;
                    actualQueue = c;
                });

            Mock.Arrange(() => streamingSvc.Shutdown(Arg.IsAny<bool>()))
                .DoInstead(() =>
                {
                    actualShudownCalls++;
                });

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(expectedInitialCapacity);

            Mock.Arrange(() => _mockAgentHealthReporter.ReportInfiniteTracingSpanEventsDropped(Arg.IsAny<long>()))
                .DoInstead<long>((countSpans) => { actualCountSpansDropped += countSpans; });

            Mock.Arrange(() => _mockAgentHealthReporter.ReportInfiniteTracingSpanEventsSeen(Arg.IsAny<long>()))
                .DoInstead<long>((countSpans) => { actualCountSpansSeen += countSpans; });

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            var testItems = new List<ISpanEventWireModel>();

            //add too many items to the queue w/o servicing it.
            for (var i = 0; i < expectedInitialCapacity; i++)
            {
                testItems.Add(new SpanAttributeValueCollection());
            }

            aggregator.Collect(testItems);

            var initialQueue = actualQueue;
            var initialQueueItems = actualQueue?.ToList();

            actualQueue = null;

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(expectedUpdatedCapacity);
            FireAgentConnectedEvent();

            var updatedQueue = actualQueue;
            var updatedQueueItems = actualQueue?.ToList();
            actualQueue = null;

            NrAssert.Multiple
            (
                () => Assert.That(actualShudownCalls, Is.EqualTo(expectedShutdownCalls)),
                () => Assert.That(actualStartConsumingCalls, Is.EqualTo(expectedStartCalls))
            );

            // If the INITIAL config was valid, test that the queue is correctly 
            // configured and has the correct items
            if (expectedInitialCapacity > 0)
            {
                var expectedQueueItems = testItems
                    .Take(expectedInitialCapacity)
                    .Select(x => x.Span).ToList();
                NrAssert.Multiple
                (
                    () => Assert.That(initialQueue.Capacity, Is.EqualTo(expectedInitialCapacity), $"Initial Queue Size."),
                    () => Assert.That(initialQueueItems, Has.Count.EqualTo(expectedInitialCapacity)),
                    () => Assert.That(initialQueueItems, Is.EqualTo(expectedQueueItems).AsCollection, "Initial Queue Items")
                );
            }
            else
            {
                NrAssert.Multiple
                (
                    () => Assert.That(initialQueue, Is.Null)
                );
            }

            // If the UPDATED config was valid, test that the queue is correctly 
            // configured and has the correct items
            if (expectedUpdatedCapacity > 0)
            {
                var expectedQueueItems = testItems
                    .Take(Math.Min(expectedInitialCapacity, expectedUpdatedCapacity))
                    .Select(x => x.Span).ToList();

                NrAssert.Multiple
                (
                    () => Assert.That(updatedQueue.Capacity, Is.EqualTo(expectedUpdatedCapacity), $"Updated Queue Size."),
                    () => Assert.That(updatedQueueItems, Has.Count.EqualTo(Math.Min(expectedInitialCapacity, expectedUpdatedCapacity))),
                    () => Assert.That(updatedQueueItems, Is.EqualTo(expectedQueueItems).AsCollection, "Items In updated queue")
                );
            }


            //If the config sizes are the same, the queue should not have changed
            NrAssert.Multiple
            (
                () => Assert.That(expectedInitialCapacity != expectedUpdatedCapacity || initialQueue == updatedQueue, Is.True),
                () => Assert.That(expectedInitialCapacity == expectedUpdatedCapacity || initialQueue != updatedQueue, Is.True),
                () => Assert.That(actualCountSpansSeen, Is.EqualTo(expectedInitialCapacity), "Count Seen Items"),
                () => Assert.That(actualCountSpansDropped, Is.EqualTo(expectedDroppedSpans), "Count Dropped")
            );
        }


        [Test]
        public void AggregatorIsAvailableRespondsToStreamingService
        (
            [Values(true, false)] bool streamingSvcAvailable,
            [Values(true, false)] bool streamingSvcEnabled,
            [Values(true, false)] bool aggregatorHasValidConfig
        )
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(streamingSvcEnabled ? "infiniteTracing.net" : null as string);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(aggregatorHasValidConfig ? 10 : -1);

            var streamingSvc = GetMockStreamingService(streamingSvcEnabled, streamingSvcAvailable);

            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            NrAssert.Multiple
            (
                () => Assert.That(streamingSvcAvailable && streamingSvcEnabled && aggregatorHasValidConfig, Is.EqualTo(aggregator.IsServiceAvailable)),
                () => Assert.That(streamingSvcEnabled, Is.EqualTo(aggregator.IsServiceEnabled))
            );
        }

        [Test]
        public void AggregatorIsEnabledShouldRespondsToConfig
        (
            [Values(true, false)] bool streamingSvcEnabled,
            [Values(true, false)] bool distributedTracingEnabled,
            [Values(true, false)] bool spanEventsEnabled)
        {
            bool expectedIsServiceEnabledValue = streamingSvcEnabled && distributedTracingEnabled && spanEventsEnabled;

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(streamingSvcEnabled ? "infiniteTracing.net" : null);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(10);
            Mock.Arrange(() => _currentConfiguration.DistributedTracingEnabled).Returns(distributedTracingEnabled);
            Mock.Arrange(() => _currentConfiguration.SpanEventsEnabled).Returns(spanEventsEnabled);

            var streamingSvc = GetMockStreamingService(streamingSvcEnabled, true);

            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            NrAssert.Multiple
            (
                () => Assert.That(aggregator.IsServiceEnabled, Is.EqualTo(expectedIsServiceEnabledValue))
            );
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="queueSize">The capacity of the queue </param>
        /// <param name="expectedSeen">The number of items to "collect"</param>
        /// <param name="addAsSingleItems">Whether or not to add them individually or as a collection</param>
        [TestCase(3, 0, true)]
        [TestCase(3, 1, true)]
        [TestCase(3, 3, true)]
        [TestCase(3, 5, true)]
        [TestCase(3, 0, false)]
        [TestCase(3, 1, false)]
        [TestCase(3, 3, false)]
        [TestCase(3, 5, false)]
        public void SupportabilityMetrics_SeenAndDropped(int queueSize, int expectedSeen, bool addAsSingleItems)
        {
            long actualCountSpansSeen = 0;
            long actualCountSpansDropped = 0;

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(queueSize);

            Mock.Arrange(() => _mockAgentHealthReporter.ReportInfiniteTracingSpanEventsDropped(Arg.IsAny<long>()))
                .DoInstead<long>((countSpans) => { actualCountSpansDropped += countSpans; });

            Mock.Arrange(() => _mockAgentHealthReporter.ReportInfiniteTracingSpanEventsSeen(Arg.IsAny<long>()))
                .DoInstead<long>((countSpans) => { actualCountSpansSeen += countSpans; });

            var expectedCountDrops = Math.Max(expectedSeen - queueSize, 0);

            var streamingSvc = GetMockStreamingService(true, true);
            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            //Act
            var items = new List<ISpanEventWireModel>();
            for (var i = 0; i < expectedSeen; i++)
            {
                var item = new SpanAttributeValueCollection();
                if (addAsSingleItems)
                {
                    aggregator.Collect(item);
                }

                items.Add(item);
            }

            if (!addAsSingleItems)
            {
                aggregator.Collect(items);
            }

            //Assert
            NrAssert.Multiple
            (
                () => Assert.That(actualCountSpansSeen, Is.EqualTo(expectedSeen), $"{(addAsSingleItems ? "Single Adds" : "Collection")} - Count Seen Items"),
                () => Assert.That(actualCountSpansDropped, Is.EqualTo(expectedCountDrops), $"{(addAsSingleItems ? "Single Adds" : "Collection")} - Count Dropped")
            );
        }

        [Test]
        public void SupportabilityMetrics_SpanQueueSize()
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(1000);

            int? rptQueueSize = null;
            Mock.Arrange(() => _mockAgentHealthReporter.ReportInfiniteTracingSpanQueueSize(Arg.IsAny<int>()))
                .DoInstead<int>((queueSize) =>
                {
                    rptQueueSize = queueSize;
                });


            const int collectionSize = 72;
            var streamingSvc = GetMockStreamingService(true, true);

            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            for (var i = 0; i < collectionSize; i++)
            {
                aggregator.Collect(new SpanAttributeValueCollection());
            }

            aggregator.ReportSupportabilityMetrics();

            Assert.That(rptQueueSize, Is.EqualTo(collectionSize), "Queue Size Supportability Metric");
        }


        /// <summary>
        /// If the queue size is invalid (<=0), 
        /// 1.  The data streaming service should stop streaming events
        /// 2.  Items in the queue should be dropped
        /// 3.  Supportabiilty metric should be recorded
        /// </summary>
        [TestCase(-1, ExpectedResult = false)]
        [TestCase(0, ExpectedResult = false)]
        [TestCase(1, ExpectedResult = true)]
        [TestCase(2, ExpectedResult = true)]
        [TestCase(62, ExpectedResult = true)]
        [TestCase(63, ExpectedResult = false)]
        [TestCase(200, ExpectedResult = false)]
        public bool PartitionCount_ValidConfigSetting(int configPartitionCount)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingPartitionCountSpans).Returns(configPartitionCount);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(10000);

            var streamingSvc = GetMockStreamingService(true, true);

            var aggregator = CreateAggregator(streamingSvc);
            FireAgentConnectedEvent();

            return aggregator.IsServiceAvailable;
        }


        [TestCase(10000, 10, ExpectedResult = 10)]
        [TestCase(10000, null, ExpectedResult = 62)]
        [TestCase(20, 62, ExpectedResult = 20)]
        [TestCase(20, 10, ExpectedResult = 10)]
        public int PartitionCount_IsApplied(int configQueueSize, int? configPartitionCount)
        {
            if (configPartitionCount.HasValue)
            {
                Mock.Arrange(() => _currentConfiguration.InfiniteTracingPartitionCountSpans).Returns(configPartitionCount.Value);
            }

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(configQueueSize);

            var streamingSvc = GetMockStreamingService(true, true);

            PartitionedBlockingCollection<Span> actualCollection = default;

            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) => { actualCollection = c; });

            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            return (actualCollection?.PartitionCount).GetValueOrDefault(0);
        }

        [TestCase(10000, 10)]
        [TestCase(10000, 62)]
        [TestCase(10000, 7)]
        [TestCase(9999, 2)]
        [TestCase(20, 62)]
        [TestCase(20, 10)]
        public void QueueSize_PartitionedCorrectly(int configQueueSize, int configPartitionCount)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingPartitionCountSpans).Returns(configPartitionCount);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(configQueueSize);

            var streamingSvc = GetMockStreamingService(true, true);

            PartitionedBlockingCollection<Span> actualCollection = default;

            Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<PartitionedBlockingCollection<Span>>()))
                .DoInstead<PartitionedBlockingCollection<Span>>((c) => { actualCollection = c; });

            var aggregator = CreateAggregator(streamingSvc);

            FireAgentConnectedEvent();

            NrAssert.Multiple
            (
                () => Assert.That(aggregator.Capacity, Is.EqualTo(configQueueSize)),
                () => Assert.That(actualCollection.Capacity, Is.EqualTo(configQueueSize))
            );
        }

        

    }
}

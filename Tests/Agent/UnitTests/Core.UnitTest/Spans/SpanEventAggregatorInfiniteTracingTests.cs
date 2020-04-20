using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Segments;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;
using System.Collections.Concurrent;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.Spans.Tests
{
	[TestFixture]
	internal class SpanEventAggregatorInfiniteTracingTests
	{
		private IConfigurationService _mockConfigService;
		private IConfiguration _currentConfiguration => _mockConfigService?.Configuration;
		private IAgentHealthReporter _mockAgentHealthReporter;

		[SetUp]
		public void SetUp()
		{
			var defaultConfig = GetDefaultConfiguration();
			_mockConfigService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _mockConfigService.Configuration).Returns(defaultConfig);
			_mockAgentHealthReporter = Mock.Create<IAgentHealthReporter>();
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
			return config;
		}

		private IDataStreamingService<Span, RecordStatus> GetMockStreamingService(bool enabled, bool available)
		{
			var streamingSvc = Mock.Create<IDataStreamingService<Span, RecordStatus>>();
			Mock.Arrange(() => streamingSvc.IsServiceEnabled).Returns(enabled);
			Mock.Arrange(() => streamingSvc.IsServiceAvailable).Returns(available);

			return streamingSvc;
		}

		private ISpanEventAggregatorInfiniteTracing CreateAggregator(IDataStreamingService<Span,RecordStatus> streamingSvc)
		{
			var aggregator = new SpanEventAggregatorInfiniteTracing(streamingSvc, _mockConfigService, _mockAgentHealthReporter);
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
			var actualQueue = null as BlockingCollection<Span>;
			var streamingSvc = GetMockStreamingService(true, true);
			Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<BlockingCollection<Span>>()))
				.DoInstead<BlockingCollection<Span>>((c) =>
				{
					actualQueue = c;
				});

			var expectedQueueCapacity = _currentConfiguration.InfiniteTracingQueueSizeSpans;

			var aggregator = CreateAggregator(streamingSvc);
			FireAgentConnectedEvent();

			Assert.AreEqual(expectedQueueCapacity, actualQueue.BoundedCapacity, "Queue Capacity");
		}

		/// <summary>
		/// If the queue size is changed
		/// 1.  The data streaming service should restart with a new queue that has the new size
		/// </summary>
		[Test]
		public void Queue_SizeRespondsToConfigChange()
		{
			var actualQueue = null as BlockingCollection<Span>;
			var streamingSvc = GetMockStreamingService(true, true);
			Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<BlockingCollection<Span>>()))
				.DoInstead<BlockingCollection<Span>>((c) =>
				{
					actualQueue = c;
				});

			var aggregator = CreateAggregator(streamingSvc);
			FireAgentConnectedEvent();

			var expectedCapacityInitial = _currentConfiguration.InfiniteTracingQueueSizeSpans;
			var actualCapacityInitial = actualQueue.BoundedCapacity;

			var expectedCapacityUpdated = _currentConfiguration.InfiniteTracingQueueSizeSpans * 3;
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(expectedCapacityUpdated);
			FireAgentConnectedEvent();

			var actualCapacityUpdated = actualQueue.BoundedCapacity;

			NrAssert.Multiple
			(
				() => Assert.AreEqual(expectedCapacityInitial, actualCapacityInitial, "Initial Queue Capacity"),
				() => Assert.AreEqual(expectedCapacityUpdated, actualCapacityUpdated, "Updated Queue Capacity")
			);	
		}

		/// <summary>
		/// If the queue size is invalid (<=0), 
		/// 1.  The data streaming service should stop streaming events
		/// 2.  Items in the queue should be dropped
		/// 3.  Supportabiilty metric should be recorded
		/// </summary>
		[TestCase(-1, false,	"Invalid Config Option")]
		[TestCase(0,  false,	"Invalid Config Option")]
		[TestCase(1,  true,		"Queue Capacity Smaller than Requests (5)")]
		[TestCase(2,  true,		"Queue Capacity Smaller than Requests (5)")]
		[TestCase(5,  true,		"Queue Capacity >= requests (5)")]
		[TestCase(6,  true,		"Queue Capacity >= requests (5)")]
		public void Queue_ValidConfigSetting(int configQueueSize, bool expectedIsAvailable, string scenarioName)
		{
			var countShutdown = 0;
			var countStartConsuming = 0;
			var actualQueue = null as BlockingCollection<Span>;

			var streamingSvc = GetMockStreamingService(true, true);
			Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<BlockingCollection<Span>>()))
				.DoInstead<BlockingCollection<Span>>((c) =>
				{
					countStartConsuming++;
					actualQueue = c;
				});

			Mock.Arrange(() => streamingSvc.Shutdown()).DoInstead(() =>
			 {
				 countShutdown++;
			 });


			Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(configQueueSize);

			var aggregator = CreateAggregator(streamingSvc);
			FireAgentConnectedEvent();

			for (var i = 0; i < 5; i++)
			{
				aggregator.Collect(new Span());
			}

			NrAssert.Multiple
			(
				() => Assert.AreEqual(expectedIsAvailable, aggregator.IsServiceAvailable, "Aggregator is Available"),
				() => Assert.AreEqual(1, countShutdown, "Number of times Shutdown was called")
			);

			if (expectedIsAvailable)
			{
				NrAssert.Multiple
				(
					() => Assert.AreEqual(1, countStartConsuming, "StartConsuming SHOULD have been called"),
					() => Assert.IsNotNull(actualQueue),
					() => Assert.AreEqual(configQueueSize, actualQueue.BoundedCapacity),
					() => Assert.AreEqual(Math.Min(5, configQueueSize), actualQueue.Count, "Items in the queue")
				);
			}
			else
			{
				NrAssert.Multiple
				(
					() => Assert.AreEqual(0, countStartConsuming, "StartConsuming should NOT have been called."),
					() => Assert.IsNull(actualQueue)
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
		[TestCase(5, 3, 2, 2, 2, "Bigger to Smaller")]		//We should preserve the first items and discard the rest.
		[TestCase(4, 5, 2, 2, 0, "Smaller to Bigger")]		//We should preserve all of the existing items in order
		[TestCase(0, 5, 2, 1, 0, "Invalid to Valid")]		//a new valid config should start the streaming consumer
		[TestCase(5, 0, 2, 1, 5, "Valid to invalid")]      //a new invalid config should prevent restart of the consumer
		public void Queue_SizeConfigChangeScenarios(int expectedInitialCapacity, int expectedUpdatedCapacity, int expectedShutdownCalls,  int expectedStartCalls, int expectedDroppedSpans, string _)
		{
			var actualQueue = null as BlockingCollection<Span>;
			var actualStartConsumingCalls = 0;
			var actualShudownCalls = 0;
			long actualCountSpansSeen = 0;
			long actualCountSpansDropped = 0;

			//Set up Streaming Service that does not dequeue items, that captures a reference to the collection
			var streamingSvc = GetMockStreamingService(true, true);
			Mock.Arrange(() => streamingSvc.StartConsumingCollection(Arg.IsAny<BlockingCollection<Span>>()))
				.DoInstead<BlockingCollection<Span>>((c) =>
				{
					actualStartConsumingCalls++;
					actualQueue = c;
				});

			Mock.Arrange(() => streamingSvc.Shutdown())
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

			var testItems = new List<Span>();

			//add too many items to the queue w/o servicing it.
			for(var i = 0; i < expectedInitialCapacity; i++)
			{
				testItems.Add(new Span());
			}

			aggregator.Collect(testItems);

			var initialQueue = actualQueue;
			actualQueue = null;

			Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(expectedUpdatedCapacity);
			FireAgentConnectedEvent();

			var updatedQueue = actualQueue;
			actualQueue = null;

			NrAssert.Multiple
			(
				() => Assert.AreEqual(expectedShutdownCalls, actualShudownCalls),
				() => Assert.AreEqual(expectedStartCalls, actualStartConsumingCalls)
			);

			// If the INITIAL config was valid, test that the queue is correctly 
			// configured and has the correct items
			if (expectedInitialCapacity > 0)
			{
				var expectedQueueItems = testItems.Take(expectedInitialCapacity).ToList();

				NrAssert.Multiple
				(
					() => Assert.AreEqual(expectedInitialCapacity, initialQueue.BoundedCapacity, $"Initial Queue Size."),
					() => Assert.AreEqual(expectedInitialCapacity, initialQueue.Count),
					() => CollectionAssert.AreEqual(expectedQueueItems, initialQueue.ToList(), "Initial Queue Items")
				);
			}
			else
			{
				NrAssert.Multiple
				(
					() => Assert.IsNull(initialQueue)
				);
			}

			// If the UPDATED config was valid, test that the queue is correctly 
			// configured and has the correct items
			if (expectedUpdatedCapacity > 0)
			{
				var expectedQueueItems = testItems.Take(Math.Min(expectedInitialCapacity, expectedUpdatedCapacity)).ToList();

				NrAssert.Multiple
				(
					() => Assert.AreEqual(expectedUpdatedCapacity, updatedQueue.BoundedCapacity, $"Updated Queue Size."),
					() => Assert.AreEqual(Math.Min(expectedInitialCapacity, expectedUpdatedCapacity), updatedQueue.Count),
					() => CollectionAssert.AreEqual(expectedQueueItems, updatedQueue.ToList(),"Items In updated queue")
				);
			}


			//If the config sizes are the same, the queue should not have changed
			NrAssert.Multiple
			(
				() => Assert.IsTrue(expectedInitialCapacity != expectedUpdatedCapacity || initialQueue == updatedQueue),
				() => Assert.IsTrue(expectedInitialCapacity == expectedUpdatedCapacity || initialQueue != updatedQueue),
				() => Assert.AreEqual(expectedInitialCapacity, actualCountSpansSeen,"Count Seen Items"),
				() => Assert.AreEqual(expectedDroppedSpans, actualCountSpansDropped, "Count Dropped")
			);
		}


		[Test]
		public void AggregatorIsAvailableRespondsToStreamingService
		(
			[Values(true,false)] bool streamingSvcAvailable, 
			[Values(true,false)] bool streamingSvcEnabled, 
			[Values(true,false)] bool aggregatorHasValidConfig
		)
		{
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(streamingSvcEnabled ? "infiniteTracing.net" : null as string);
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingQueueSizeSpans).Returns(aggregatorHasValidConfig ? 10 : -1);

			var streamingSvc = GetMockStreamingService(streamingSvcEnabled, streamingSvcAvailable);

			var aggregator = CreateAggregator(streamingSvc);
			
			FireAgentConnectedEvent();

			NrAssert.Multiple
			(
				() => Assert.AreEqual(aggregator.IsServiceAvailable, streamingSvcAvailable && streamingSvcEnabled && aggregatorHasValidConfig),
				() => Assert.AreEqual(aggregator.IsServiceEnabled, streamingSvcEnabled)
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
		public void SupportabilityMetrics(int queueSize, int expectedSeen, bool addAsSingleItems)
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
			var items = new List<Span>();
			for (var i = 0; i < expectedSeen; i++)
			{
				var item = new Span();
				if(addAsSingleItems)
				{
					aggregator.Collect(item);
				}

				items.Add(item);
			}

			if(!addAsSingleItems)
			{
				aggregator.Collect(items);
			}

			//Assert
			NrAssert.Multiple
			(
				() => Assert.AreEqual(expectedSeen, actualCountSpansSeen, $"{(addAsSingleItems ? "Single Adds" : "Collection")} - Count Seen Items"),
				() => Assert.AreEqual(expectedCountDrops, actualCountSpansDropped, $"{(addAsSingleItems ? "Single Adds" : "Collection")} - Count Dropped")
			);
		}
	}
}

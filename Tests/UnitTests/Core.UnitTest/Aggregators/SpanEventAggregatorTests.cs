using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
	internal class SpanEventAggregatorTests
	{
		private IDataTransportService _dataTransportService;
		private IAgentHealthReporter _agentHealthReporter;
		private SpanEventAggregator _spanEventAggregator;
		private ConfigurationAutoResponder _configurationAutoResponder;
		private Action _harvestAction;

		private const string TimeStampKey = "timestamp";
		private const string PriorityKey = "priority";
		private static readonly long TestTime = DateTime.UtcNow.ToUnixTimeMilliseconds();

		private static readonly SpanEventWireModel[] SpanEvents = {
			Mock.Create<SpanEventWireModel>(new Dictionary<string, object> { {TimeStampKey, TestTime}, {PriorityKey, 0.654321f} }),
			Mock.Create<SpanEventWireModel>(new Dictionary<string, object> {{TimeStampKey, TestTime + 1}, {PriorityKey, 0.654321f}}),
			Mock.Create<SpanEventWireModel>(new Dictionary<string, object> {{TimeStampKey, TestTime}, {PriorityKey, 0.1f}})
		};

		#region Helpers
		private static IConfiguration GetDefaultConfiguration(int? versionNumber = null)
		{
			var configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => configuration.SpanEventsEnabled).Returns(true);
			Mock.Arrange(() => configuration.SpanEventsMaxSamplesStored).Returns(1000);
			if (versionNumber.HasValue)
				Mock.Arrange(() => configuration.ConfigurationVersion).Returns(versionNumber.Value);
			return configuration;
		}

		private void CollectSpanEvents(int n)
		{
			if (n <= SpanEvents.Length)
			{
				_spanEventAggregator.Collect(SpanEvents.Take(n));
			}
		}
		#endregion

		[SetUp]
		public void SetUp()
		{
			var configuration = GetDefaultConfiguration();
			Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
			Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
			_configurationAutoResponder = new ConfigurationAutoResponder(configuration);

			_dataTransportService = Mock.Create<IDataTransportService>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			var processStatic = Mock.Create<IProcessStatic>();

			var scheduler = Mock.Create<IScheduler>();
			Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
				.DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestAction = action);
			_spanEventAggregator = new SpanEventAggregator(_dataTransportService, scheduler, processStatic, _agentHealthReporter);
		}

		[TearDown]
		public void TearDown()
		{
			_spanEventAggregator.Dispose();
			_configurationAutoResponder.Dispose();
		}

		[Test]
		public void SpanEventAggregatorTests_EventsSendOnHarvest()
		{
			// Arrange
			const int EventCount = 3;
			const uint ExpectedReservoirSize = 1000u;
			const int ExpectedEventsSeen = EventCount;
			var actualReservoirSize = uint.MaxValue;
			var actualEventsSeen = uint.MaxValue;
			var sentEvents = null as IEnumerable<SpanEventWireModel>;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.DoInstead<EventHarvestData, IEnumerable<SpanEventWireModel>>((eventHarvestData, events) =>
				{
					sentEvents = events;
					actualReservoirSize = eventHarvestData.ReservoirSize;
					actualEventsSeen = eventHarvestData.EventsSeen;
				});

			CollectSpanEvents(EventCount);

			// Act
			_harvestAction();

			// Assert
			var spanEventWireModels = sentEvents as SpanEventWireModel[] ?? sentEvents.ToArray();
			Assert.That(spanEventWireModels, Has.Exactly(EventCount).Items);
			Assert.That(spanEventWireModels, Is.EqualTo(SpanEvents));
			Assert.That(actualReservoirSize, Is.EqualTo(ExpectedReservoirSize));
			Assert.That(actualEventsSeen, Is.EqualTo(ExpectedEventsSeen));
		}

		[Test]
		public void SpanEventAggregatorTests_EventSeenReportedOnCollect()
		{
			const int eventCount = 1;
			// Act
			CollectSpanEvents(eventCount);

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
		}

		[Test]
		public void SpanEventAggregatorTests_EventsSentReportedOnHarvest()
		{
			const int eventCount = 1;
			// Arrange
			CollectSpanEvents(eventCount);

			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(eventCount));
		}

		[Test]
		public void SpanEventAggregatorTests_EventsAreNotSentIfThereAreNoEventsToSend()
		{
			// Arrange
			var sendCalled = false;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sendCalled = true;
					return DataTransportResponseStatus.RequestSuccessful;
				});

			// Act
			_harvestAction();

			// Assert
			Assert.That(sendCalled, Is.False);
		}

		[Test]
		public void SpanEventAggregatorTests_EventsAreNotRetainedAfterHarvestIfResponseEquals(
			[Values(DataTransportResponseStatus.RequestSuccessful, 
				DataTransportResponseStatus.OtherError)] DataTransportResponseStatus response)
		{
			// Arrange
			IEnumerable<SpanEventWireModel> sentEvents = null;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sentEvents = events;
					return response;
				});

			CollectSpanEvents(2);
			_harvestAction();
			sentEvents = null; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.That(sentEvents, Is.Null);
		}

		[Test]
		public void SpanEventAggregatorTests_EventsAreRetainedAfterHarvestIfResponseEquals(
			[Values(DataTransportResponseStatus.ConnectionError, 
					DataTransportResponseStatus.ServerError,
					DataTransportResponseStatus.CommunicationError,
					DataTransportResponseStatus.RequestTimeout
				)] DataTransportResponseStatus response)
		{
			const int eventCount = 2;
			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sentEventCount = events.Count();
					return response;
				});

			CollectSpanEvents(eventCount);

			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.That(sentEventCount, Is.EqualTo(eventCount));
		}


		[Test]
		public void SpanEventAggregatorTests_MetricsAreCorrectAfterHarvestRetryIfSendResponseEquals(
			[Values(DataTransportResponseStatus.ConnectionError,
				DataTransportResponseStatus.ServerError,
				DataTransportResponseStatus.CommunicationError,
				DataTransportResponseStatus.RequestTimeout
			)] DataTransportResponseStatus response)
		{
			const int eventCount = 2;
			// Arrange
			var firstTime = true;
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sentEventCount = events.Count();
					var returnValue = (firstTime) ? response : DataTransportResponseStatus.RequestSuccessful;
					firstTime = false;
					return returnValue;
				});

			CollectSpanEvents(eventCount);

			//this harvest's Send() will fail with the value in response variable
			_harvestAction(); 

			//EventsSent should not happen due to error
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(Arg.IsAny<int>()), Occurs.Never());

			sentEventCount = int.MinValue; // reset

			// Act
			//this harvest's Send() will succeeed
			_harvestAction();

			Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(eventCount));

			// Assert
			Assert.That(sentEventCount, Is.EqualTo(eventCount));
		}

		[Test]
		public void SpanEventAggregatorTests_HalfOfTheEventsAreRetainedAfterHarvestIfResponseEqualsPostTooBigError()
		{
			const int eventCount = 2;

			// Arrange
			var sentEventCount = int.MinValue;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sentEventCount = events.Count();
					return DataTransportResponseStatus.PostTooBigError;
				});

			CollectSpanEvents(eventCount);

			_harvestAction();
			sentEventCount = int.MinValue; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.That(sentEventCount, Is.EqualTo(eventCount / 2));
		}

		[Test]
		public void SpanEventAggregatorTests_ZeroEventsAreRetainedAfterHarvestIfResponseEqualsPostTooBigErrorWithOnlyOneEventInPost()
		{
			const int eventCount = 1;
			// Arrange
			IEnumerable<SpanEventWireModel> sentEvents = null;
			Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<SpanEventWireModel>>()))
				.Returns<EventHarvestData, IEnumerable<SpanEventWireModel>>((_, events) =>
				{
					sentEvents = events;
					return DataTransportResponseStatus.PostTooBigError;
				});

			CollectSpanEvents(eventCount);

			_harvestAction();
			sentEvents = null; // reset

			// Act
			_harvestAction();

			// Assert
			Assert.That(sentEvents, Is.Null);
		}

		[Test]
		public void SpanEventAggregatorTests_WhenNoEventsArePublishedThenNoEventsAreReportedToAgentHealth()
		{
			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(Arg.IsAny<int>()), Occurs.Never());
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(Arg.IsAny<int>()), Occurs.Never());
		}

		[Test]
		public void SpanEventAggregatorTests_WhenEventIsCollectedThenEventsSeenIsReportedToAgentHealth()
		{
			const int eventCount = 1;
			// Act
			CollectSpanEvents(eventCount);

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventCollected(eventCount));
		}

		[Test]
		public void SpanEventAggregatorTests_WhenHarvestingEventsThenEventSentIsReportedToAgentHealth()
		{
			const int eventCount = 3;
			// Arrange
			CollectSpanEvents(eventCount);
			
			// Act
			_harvestAction();

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSpanEventsSent(eventCount));
		}

	}
}
